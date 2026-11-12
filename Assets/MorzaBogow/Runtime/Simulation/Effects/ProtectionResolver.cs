using System;
using System.Collections.Generic;
using System.Numerics;

namespace MorzaBogow.Simulation.Effects
{
    /// <summary>One thing about to reach one target through one channel.</summary>
    public struct HitEvent
    {
        /// <summary>
        /// The ID of the shot, strike or contact. One hit may reach several targets and carry several channels (an
        /// empowered shot has an ordinary and a bonus part); each target and channel of it is settled once.
        /// </summary>
        public long HitId;
        public int TargetId;
        /// <summary>The target's owner slot, 0..2.</summary>
        public int TargetOwner;
        /// <summary>A port, fort or other structure rather than a ship or creature.</summary>
        public bool TargetIsStructure;
        public double TargetX, TargetY;
        /// <summary>Who is at war with whom at this tick; the default is nobody.</summary>
        public SideRelations Relations;
        public EffectChannel Channel;
        /// <summary>Damage in hundredths for a damage channel; for the others any non-negative strength, passed through whole.</summary>
        public long AmountCenti;
        public int Tick;
        /// <summary>Whether the target may be reached by this event at all (decided by the caller from the attacker's side).</summary>
        public bool TargetIsLegal;
        /// <summary>Material hull armour in thousandths, and the channels it stands against. Not an effect; never stacks with itself.</summary>
        public int HullShieldPermille;
        public ChannelMask HullShieldChannels;
    }

    public enum HitVerdict : byte
    {
        /// <summary>Not a legal target or channel: nothing happens and nothing is recorded.</summary>
        Illegal = 1,
        /// <summary>The target is fully insensitive to the channel.</summary>
        Immune = 2,
        /// <summary>The influence was cancelled by a neutralization.</summary>
        Neutralized = 3,
        /// <summary>It goes through with the amount left after the reductions (which may be all of it, or zero).</summary>
        Applied = 4,
        /// <summary>This hit has been settled for this target and channel already: no effect, no loss, no event.</summary>
        AlreadySettled = 5,
    }

    public struct HitOutcome
    {
        public HitVerdict Verdict;
        public long AmountCenti;
        /// <summary>The effect that stopped the hit, or 0.</summary>
        public int DecidingEffectId;
        /// <summary>The reductions that counted, one per family, in the order of the families. Never null.</summary>
        public int[] ReducingEffectIds;
        /// <summary>Whether the hull's own armour was one of the factors.</summary>
        public bool HullShieldApplied;
    }

    /// <summary>What is settled once: one hit reaching one target through one channel.</summary>
    public readonly struct HitKey : IEquatable<HitKey>
    {
        public readonly long HitId;
        public readonly int TargetId;
        public readonly EffectChannel Channel;

        public HitKey(long hitId, int targetId, EffectChannel channel) { HitId = hitId; TargetId = targetId; Channel = channel; }

        public bool Equals(HitKey other) => HitId == other.HitId && TargetId == other.TargetId && Channel == other.Channel;
        public override bool Equals(object obj) => obj is HitKey other && Equals(other);
        public override int GetHashCode() => (HitId.GetHashCode() * 397 ^ TargetId) * 397 ^ (int)Channel;
    }

    /// <summary>The hits already settled. Part of the world's state once the executor runs (P07).</summary>
    public sealed class HitLedger
    {
        readonly HashSet<HitKey> settled = new HashSet<HitKey>();

        public int Count => settled.Count;
        public bool IsSettled(HitKey key) => settled.Contains(key);
        /// <summary>True the first time a key is offered, false ever after.</summary>
        public bool TrySettle(HitKey key) => settled.Add(key);
    }

    public interface IEffectDefinitions
    {
        /// <summary>The definition with this catalogue ID, or null.</summary>
        EffectDefinition Find(string id);
    }

    /// <summary>Whether an effect reaches a target: by its target (entity, area, side, whole map) and its definition's filter.</summary>
    public static class EffectScope
    {
        const TargetFilter Relational =
            TargetFilter.OwnShip | TargetFilter.HostileShip | TargetFilter.IndependentActor |
            TargetFilter.OwnStructure | TargetFilter.HostileStructure | TargetFilter.OwnSide;

        /// <summary>
        /// An effect placed on one entity covers that entity and nothing else; the filter was asked when it was placed.
        /// An area covers what is within its radius (the edge included), a side what belongs to that slot, the whole
        /// map everything - each of the three only where the filter admits the target.
        /// </summary>
        public static bool Covers(EffectInstance effect, EffectDefinition definition, in HitEvent hit)
        {
            switch (effect.Target.Kind)
            {
                case EffectTargetKind.Entity:
                    return effect.Target.EntityId == hit.TargetId;
                case EffectTargetKind.Area:
                    var dx = hit.TargetX - effect.Target.X;
                    var dy = hit.TargetY - effect.Target.Y;
                    return dx * dx + dy * dy <= effect.Target.Radius * effect.Target.Radius && Admits(definition.TargetFilter, effect.Owner, hit);
                case EffectTargetKind.Side:
                    return effect.Target.EntityId == hit.TargetOwner && Admits(definition.TargetFilter, effect.Owner, hit);
                case EffectTargetKind.WholeMap:
                    return Admits(definition.TargetFilter, effect.Owner, hit);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the filter, read from the effect's owner, admits the target. Own means the same owner slot; hostile
        /// means at war by the hit's relations, never merely "the other one". A filter naming only places admits whoever is there.
        /// </summary>
        public static bool Admits(TargetFilter filter, int effectOwner, in HitEvent hit)
        {
            var asked = filter & Relational;
            if (asked == TargetFilter.None) return true;
            if (effectOwner == hit.TargetOwner)
                return (asked & (TargetFilter.OwnSide | (hit.TargetIsStructure ? TargetFilter.OwnStructure : TargetFilter.OwnShip))) != 0;
            if (hit.TargetOwner == Owners.Independent && (asked & TargetFilter.IndependentActor) != 0) return true;
            return hit.Relations.IsHostile(effectOwner, hit.TargetOwner)
                   && (asked & (hit.TargetIsStructure ? TargetFilter.HostileStructure : TargetFilter.HostileShip)) != 0;
        }
    }

    /// <summary>
    /// The one order in which protections answer a hit (docs/NOWY_PLAN_GRY.md, "Kolejność ochron"):
    /// legality, full insensitivity, neutralization, the strongest reduction of each family, the hull, one settlement.
    /// Whole numbers only: reductions are thousandths, the amount hundredths, and the product of what every family
    /// lets through is rounded down once, at the end.
    /// </summary>
    public static class ProtectionResolver
    {
        const int Full = EffectDefinition.FullPermille;

        /// <summary>The most the material hull armour may take away (Rules.MaxHullReduction). Wards and immunities are not bound by it.</summary>
        public const int MaxHullShieldPermille = (int)(Rules.MaxHullReduction * Full + 0.5);

        static readonly int[] NoEffects = new int[0];

        /// <summary>
        /// What happens to the hit, without recording it. Effects that are not active at the hit's tick, do not cover
        /// the target (see <see cref="EffectScope"/>), are of an unknown definition, or do not answer the channel are ignored.
        /// </summary>
        public static HitOutcome Evaluate(in HitEvent hit, IReadOnlyList<EffectInstance> effects, IEffectDefinitions definitions)
        {
            var outcome = new HitOutcome { ReducingEffectIds = NoEffects };

            // 1. Legality of the target and of the channel.
            if (!hit.TargetIsLegal || !Owners.IsValid(hit.TargetOwner) || !EffectChannels.IsDefined(hit.Channel) || hit.AmountCenti < 0)
            {
                outcome.Verdict = HitVerdict.Illegal;
                return outcome;
            }

            EffectInstance immunity = null, neutralization = null;
            EffectDefinition immunityDefinition = null, neutralizationDefinition = null;
            var strongest = new EffectInstance[ProtectionFamilies.TableLength];
            var strength = new int[ProtectionFamilies.TableLength];

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect.IsActiveAt(hit.Tick)) continue;
                var definition = definitions.Find(effect.DefinitionId);
                if (definition == null || !EffectScope.Covers(effect, definition, hit)) continue;
                var response = definition.Respond(hit.Channel);

                switch (response.Answer)
                {
                    case ChannelAnswer.Immune:
                        if (Precedes(effect, definition, immunity, immunityDefinition)) { immunity = effect; immunityDefinition = definition; }
                        break;
                    case ChannelAnswer.Neutralize:
                        if (Precedes(effect, definition, neutralization, neutralizationDefinition)) { neutralization = effect; neutralizationDefinition = definition; }
                        break;
                    case ChannelAnswer.Reduce:
                        var family = (int)definition.Family;
                        if (family <= 0 || family >= strongest.Length) break;
                        var share = Math.Min(Full, Math.Max(0, response.ReducePermille));
                        var current = strongest[family];
                        // The strongest of the family; of two equal ones the lower ID, so the answer does not depend on order.
                        if (current == null || share > strength[family] || share == strength[family] && effect.Id < current.Id)
                        {
                            strongest[family] = effect;
                            strength[family] = share;
                        }
                        break;
                }
            }

            // 2. Full insensitivity to the channel.
            if (immunity != null)
            {
                outcome.Verdict = HitVerdict.Immune;
                outcome.DecidingEffectId = immunity.Id;
                return outcome;
            }

            // 3. Neutralization of this influence.
            if (neutralization != null)
            {
                outcome.Verdict = HitVerdict.Neutralized;
                outcome.DecidingEffectId = neutralization.Id;
                return outcome;
            }

            outcome.Verdict = HitVerdict.Applied;
            outcome.AmountCenti = hit.AmountCenti;

            // Only damage is weakened by a share. Control and observation are blocked above or work whole.
            if (!EffectChannels.IsDamage(hit.Channel)) return outcome;

            // 4. The strongest reduction of each family; families multiply, equal shields do not add up.
            var numerator = new BigInteger(hit.AmountCenti);
            var denominator = BigInteger.One;
            var counted = new List<int>();
            for (var family = 1; family < strongest.Length; family++)
            {
                if (strongest[family] == null) continue;
                numerator *= Full - strength[family];
                denominator *= Full;
                counted.Add(strongest[family].Id);
            }
            if (counted.Count > 0) outcome.ReducingEffectIds = counted.ToArray();

            // 5. The hull's own armour, against the channels it covers, never beyond its material limit.
            if (hit.HullShieldPermille > 0 && hit.HullShieldChannels.Contains(hit.Channel))
            {
                numerator *= Full - Math.Min(MaxHullShieldPermille, hit.HullShieldPermille);
                denominator *= Full;
                outcome.HullShieldApplied = true;
            }

            // One rounding, down. Every factor is within 0..1000, so the result is within 0..amount.
            outcome.AmountCenti = (long)BigInteger.Divide(numerator, denominator);
            return outcome;
        }

        /// <summary>
        /// 6. Evaluates the hit and records it for its target and channel: the same hit offered again for the same
        /// target and channel changes nothing and is reported as already settled. An illegal hit records nothing.
        /// </summary>
        public static HitOutcome Settle(in HitEvent hit, IReadOnlyList<EffectInstance> effects, IEffectDefinitions definitions, HitLedger ledger)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            var outcome = Evaluate(hit, effects, definitions);
            if (outcome.Verdict == HitVerdict.Illegal) return outcome;
            if (ledger.TrySettle(new HitKey(hit.HitId, hit.TargetId, hit.Channel))) return outcome;
            return new HitOutcome { Verdict = HitVerdict.AlreadySettled, ReducingEffectIds = NoEffects };
        }

        static bool Precedes(EffectInstance effect, EffectDefinition definition, EffectInstance best, EffectDefinition bestDefinition)
        {
            if (best == null) return true;
            if (definition.Priority != bestDefinition.Priority) return definition.Priority > bestDefinition.Priority;
            return effect.Id < best.Id;
        }
    }
}
