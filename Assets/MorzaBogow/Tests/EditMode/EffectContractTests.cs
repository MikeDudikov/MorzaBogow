using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MorzaBogow.Simulation;
using MorzaBogow.Simulation.Effects;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// P02-C: the shared contract of effects (Runtime/Simulation/Effects) - the order of protections, "the strongest of a
    /// family, never the sum", whom an effect covers, the absolute timeline, the record's binary form and the executor
    /// that admits what it cannot do yet. The definitions below are fixtures written from docs/KATALOG_MECHANIK.md
    /// section 6; they are not the catalogue, and no power is carried out here (P07).
    /// </summary>
    public sealed class EffectContractTests
    {
        const int Target = 40, Other = 41;
        const long Hundred = 100 * Rules.Centi;

        sealed class Definitions : IEffectDefinitions
        {
            readonly Dictionary<string, EffectDefinition> byId = new Dictionary<string, EffectDefinition>();
            public EffectDefinition Add(EffectDefinition definition) { byId.Add(definition.Id, definition); return definition; }
            public EffectDefinition Find(string id) => byId.TryGetValue(id, out var definition) ? definition : null;
        }

        static EffectDefinition Protection(string id, EffectKind kind, ProtectionFamily family, params ChannelResponse[] responses) =>
            new EffectDefinition
            {
                Id = id, Kind = kind, Family = family, Responses = responses,
                Duration = EffectDuration.ForSeconds(45), AnnounceTicks = 15, TargetFilter = TargetFilter.OwnShip,
            };

        static EffectDefinition Effect(string id, EffectKind kind, params EffectChannel[] channels) =>
            new EffectDefinition
            {
                Id = id, Kind = kind, Channels = ChannelMask.Of(channels),
                Duration = EffectDuration.ForSeconds(45), AnnounceTicks = 15, TargetFilter = TargetFilter.OwnShip,
            };

        static EffectDefinition Shield(string id, ProtectionFamily family, int permille) =>
            Protection(id, EffectKind.Reduction, family, ChannelResponse.Reduce(EffectChannel.OrdinaryWeapon, permille));

        /// <summary>M11, M12 as one definition with its matrix, a weaker ward of M11's family, and protections of other families.</summary>
        static Definitions Catalogue()
        {
            var definitions = new Definitions();
            definitions.Add(Shield("M11", ProtectionFamily.OrdinaryWard, 800));
            definitions.Add(Shield("WARD50", ProtectionFamily.OrdinaryWard, 500));
            definitions.Add(Shield("AMULET50", ProtectionFamily.Artifact, 500));
            definitions.Add(Shield("WALL100", ProtectionFamily.Structure, 1000));
            definitions.Add(Protection("M12", EffectKind.Ward, ProtectionFamily.MagicWard,
                ChannelResponse.Neutralize(EffectChannel.Thunder),
                ChannelResponse.Neutralize(EffectChannel.EmpoweredBonus),
                ChannelResponse.Neutralize(EffectChannel.GorgonGaze),
                ChannelResponse.Neutralize(EffectChannel.SirenDamage),
                ChannelResponse.Reduce(EffectChannel.SummonedCreature, 800),
                ChannelResponse.Reduce(EffectChannel.FireZone, 800),
                ChannelResponse.Reduce(EffectChannel.WaterspoutDamage, 800)));
            definitions.Add(Protection("HERO", EffectKind.Immunity, ProtectionFamily.Hero,
                ChannelResponse.Immune(EffectChannel.OrdinaryWeapon), ChannelResponse.Immune(EffectChannel.Ram),
                ChannelResponse.Immune(EffectChannel.Thunder), ChannelResponse.Immune(EffectChannel.FireZone)));
            definitions.Add(Protection("PILOT", EffectKind.Immunity, ProtectionFamily.Crew,
                ChannelResponse.Immune(EffectChannel.WhirlpoolPull), ChannelResponse.Immune(EffectChannel.WhirlpoolDamage),
                ChannelResponse.Immune(EffectChannel.ReefBarrier), ChannelResponse.Immune(EffectChannel.ReefImpact)));
            return definitions;
        }

        /// <summary>An effect on one entity, used at tick 85: announced until 100, active until 1450.</summary>
        static EffectInstance On(Definitions definitions, string definitionId, int id, int target = Target) =>
            EffectInstance.Schedule(definitions.Find(definitionId), id, 9000 + id, EffectSourceKind.Sanctuary, 7, Owners.Human, EffectTarget.Entity(target), 85);

        /// <summary>A hit on the human's ship at the origin, at tick 100, with everybody at war.</summary>
        static HitEvent Hit(EffectChannel channel, long amount = Hundred, long hitId = 1, int tick = 100, int target = Target) =>
            new HitEvent
            {
                HitId = hitId, TargetId = target, TargetOwner = Owners.Human, Relations = SideRelations.AllHostile,
                Channel = channel, AmountCenti = amount, Tick = tick, TargetIsLegal = true,
            };

        static HitOutcome Evaluate(Definitions definitions, HitEvent hit, params EffectInstance[] effects) =>
            ProtectionResolver.Evaluate(hit, effects, definitions);

        // ---------- The strongest of a family, never the sum ----------

        [Test]
        public void Reduction_TwoEqualWardsOfOneFamily_CountAsOne()
        {
            var definitions = Catalogue();
            var first = On(definitions, "M11", 5);
            var second = On(definitions, "M11", 3);

            var outcome = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), first, second);
            var reversed = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), second, first);

            // 80% once leaves 20; applied twice it would leave 4, summed it would heal 60.
            Assert.That(outcome.Verdict, Is.EqualTo(HitVerdict.Applied));
            Assert.That(outcome.AmountCenti, Is.EqualTo(20 * Rules.Centi));
            Assert.That(outcome.ReducingEffectIds, Is.EqualTo(new[] { 3 }), "a tie is decided by the lower ID");
            Assert.That(reversed.AmountCenti, Is.EqualTo(outcome.AmountCenti));
            Assert.That(reversed.ReducingEffectIds, Is.EqualTo(new[] { 3 }), "and not by the order of the list");
        }

        [Test]
        public void Reduction_WeakerWardOfTheFamily_NeitherAddsNorReplaces()
        {
            var definitions = Catalogue();
            var strong = On(definitions, "M11", 8);
            var weak = On(definitions, "WARD50", 2);

            foreach (var effects in new[] { new[] { strong, weak }, new[] { weak, strong } })
            {
                var outcome = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), effects);
                Assert.That(outcome.AmountCenti, Is.EqualTo(20 * Rules.Centi));
                Assert.That(outcome.ReducingEffectIds, Is.EqualTo(new[] { 8 }));
            }
            Assert.That(Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), weak).AmountCenti, Is.EqualTo(50 * Rules.Centi));
        }

        [Test]
        public void Reduction_DifferentFamilies_Multiply()
        {
            var definitions = Catalogue();
            var outcome = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), On(definitions, "M11", 1), On(definitions, "AMULET50", 2), On(definitions, "WARD50", 3));

            Assert.That(outcome.AmountCenti, Is.EqualTo(10 * Rules.Centi), "100 x 0.20 x 0.50; the weaker ward of M11's family is left out");
            Assert.That(outcome.ReducingEffectIds, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void Reduction_WardOnAnotherShip_DoesNotProtectTheTarget()
        {
            var definitions = Catalogue();
            var outcome = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), On(definitions, "M11", 1, Other));

            Assert.That(outcome.AmountCenti, Is.EqualTo(Hundred));
            Assert.That(outcome.ReducingEffectIds, Is.Empty);
        }

        [Test]
        public void ProtectionFamily_TableHoldsEveryFamilyIncludingTheLast()
        {
            foreach (ProtectionFamily family in Enum.GetValues(typeof(ProtectionFamily)))
                Assert.That((int)family, Is.LessThan(ProtectionFamilies.TableLength), family.ToString());

            // A reduction of every family is counted, whatever numbers the families have.
            var definitions = new Definitions();
            var effects = new List<EffectInstance>();
            foreach (ProtectionFamily family in Enum.GetValues(typeof(ProtectionFamily)))
            {
                if (family == ProtectionFamily.None) continue;
                definitions.Add(Shield("F" + (int)family, family, 500));
                effects.Add(On(definitions, "F" + (int)family, 10 + (int)family));
            }
            var outcome = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon, amount: 1L << effects.Count), effects.ToArray());

            Assert.That(outcome.ReducingEffectIds.Length, Is.EqualTo(effects.Count));
            Assert.That(outcome.AmountCenti, Is.EqualTo(1), "halved once by each family");
        }

        // ---------- Whole numbers: the plan's example, the single rounding, the hull's limit ----------

        static HitEvent Armoured(HitEvent hit, int permille)
        {
            hit.HullShieldPermille = permille;
            hit.HullShieldChannels = ChannelMask.Of(EffectChannel.OrdinaryWeapon);
            return hit;
        }

        [Test]
        public void Hull_ArmourFifteenPercentAndM11_LeaveSeventeenOfAHundred()
        {
            var definitions = Catalogue();
            var hit = Armoured(Hit(EffectChannel.OrdinaryWeapon), 150);

            var both = Evaluate(definitions, hit, On(definitions, "M11", 1));
            var hullOnly = Evaluate(definitions, hit);

            // docs/PARAMETRY_ROBOCZE.md: 100 x 0.85 x 0.20 = 17, and M11 without armour gives 20.
            Assert.That(both.AmountCenti, Is.EqualTo(17 * Rules.Centi));
            Assert.That(both.HullShieldApplied, Is.True);
            Assert.That(hullOnly.AmountCenti, Is.EqualTo(85 * Rules.Centi));
            Assert.That(Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), On(definitions, "M11", 1)).AmountCenti, Is.EqualTo(20 * Rules.Centi));
        }

        [Test]
        public void Hull_ArmourAgainstAnotherChannel_IsNotApplied()
        {
            var definitions = Catalogue();
            var outcome = Evaluate(definitions, Armoured(Hit(EffectChannel.Thunder), 150));

            Assert.That(outcome.AmountCenti, Is.EqualTo(Hundred));
            Assert.That(outcome.HullShieldApplied, Is.False);
        }

        [Test]
        public void Hull_ArmourAboveItsMaterialLimit_IsHeldAtTheLimit()
        {
            var definitions = Catalogue();
            Assert.That(ProtectionResolver.MaxHullShieldPermille, Is.EqualTo(700));
            Assert.That(ProtectionResolver.MaxHullShieldPermille, Is.EqualTo(Rules.MaxHullReduction * 1000).Within(1e-6), "the same limit as Rules.MaxHullReduction");

            long Left(int permille) => Evaluate(definitions, Armoured(Hit(EffectChannel.OrdinaryWeapon), permille)).AmountCenti;
            Assert.That(new[] { 690, 700, 701, 900, 1000, int.MaxValue }.Select(Left), Is.EqualTo(new[]
            {
                31 * Rules.Centi, 30 * Rules.Centi, 30 * Rules.Centi, 30 * Rules.Centi, 30 * Rules.Centi, 30 * Rules.Centi,
            }));

            // The limit binds the armour only: the ward on top of it still takes its own 80%, and a full ward still stops everything.
            Assert.That(Evaluate(definitions, Armoured(Hit(EffectChannel.OrdinaryWeapon), 900), On(definitions, "M11", 1)).AmountCenti, Is.EqualTo(6 * Rules.Centi));
            Assert.That(Evaluate(definitions, Armoured(Hit(EffectChannel.OrdinaryWeapon), 900), On(definitions, "WALL100", 2)).AmountCenti, Is.EqualTo(0));
        }

        [Test]
        public void Rounding_HappensOnceAtTheEndAndDownwards()
        {
            var definitions = Catalogue();
            var hit = Armoured(Hit(EffectChannel.OrdinaryWeapon, amount: 5), 600);

            // 0.05 x 0.50 x 0.40 = 0.01 exactly. Rounding after the ward first (2.5 -> 2 -> 0.8) would lose it.
            Assert.That(Evaluate(definitions, hit, On(definitions, "WARD50", 1)).AmountCenti, Is.EqualTo(1));

            // 0.07 x 0.20 = 0.014: down to 0.01, never up.
            Assert.That(Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon, amount: 7), On(definitions, "M11", 1)).AmountCenti, Is.EqualTo(1));
            Assert.That(Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon, amount: 4), On(definitions, "M11", 1)).AmountCenti, Is.EqualTo(0));
        }

        [Test]
        public void Reduction_FullAndStacked_NeverGoesBelowZeroAndSurvivesTheLargestAmount()
        {
            var definitions = Catalogue();
            var all = new[] { On(definitions, "WALL100", 1), On(definitions, "M11", 2), On(definitions, "AMULET50", 3) };

            Assert.That(Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), all).AmountCenti, Is.EqualTo(0));

            // Two families and the hull on the largest long (M12 is present but does not answer ordinary weapons):
            // the product must not overflow into a negative loss.
            var huge = Armoured(Hit(EffectChannel.OrdinaryWeapon, amount: long.MaxValue), 150);
            var outcome = Evaluate(definitions, huge, all[1], all[2], On(definitions, "M12", 4));
            Assert.That(outcome.ReducingEffectIds, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(outcome.AmountCenti, Is.EqualTo(long.MaxValue / 1000 * 85 + long.MaxValue % 1000 * 85 / 1000), "x 0.20 x 0.50 x 0.85 = x 0.085");
        }

        // ---------- Channels ----------

        [Test]
        public void Channels_EveryChannelBelongsToExactlyOneGroup()
        {
            foreach (EffectChannel channel in Enum.GetValues(typeof(EffectChannel)))
            {
                var groups = new[] { EffectChannels.IsDamage(channel), EffectChannels.IsControl(channel), EffectChannels.IsObservation(channel) }.Count(member => member);
                Assert.That(groups, Is.EqualTo(channel == EffectChannel.None ? 0 : 1), channel.ToString());
                Assert.That(EffectChannels.IsDefined(channel), Is.EqualTo(channel != EffectChannel.None), channel.ToString());
            }
            Assert.That(EffectChannels.Damage.Overlaps(EffectChannels.Control) || EffectChannels.Damage.Overlaps(EffectChannels.Observing)
                        || EffectChannels.Control.Overlaps(EffectChannels.Observing), Is.False);
            Assert.That(EffectChannels.All, Is.EqualTo(EffectChannels.Damage | EffectChannels.Control | EffectChannels.Observing));

            // The gaze is control, not damage; an independent creature's attack is damage of its own channel.
            Assert.That(EffectChannels.IsControl(EffectChannel.GorgonGaze), Is.True);
            Assert.That(EffectChannels.IsDamage(EffectChannel.CreatureAttack), Is.True);
            // Numbers are what a save would hold: the gaze keeps its number, the new channel is the last.
            Assert.That(((int)EffectChannel.GorgonGaze, (int)EffectChannel.CreatureAttack), Is.EqualTo((11, 25)));
            Assert.That(Enum.GetValues(typeof(EffectChannel)).Cast<EffectChannel>().Max(), Is.EqualTo(EffectChannel.CreatureAttack));
        }

        [Test]
        public void Channels_OrdinaryWardAndMagicWard_DoNotCoverEachOther()
        {
            var definitions = Catalogue();
            var aegis = On(definitions, "M11", 1);
            var seal = On(definitions, "M12", 2);

            Assert.That(Evaluate(definitions, Hit(EffectChannel.FireZone), aegis).AmountCenti, Is.EqualTo(Hundred), "M11 does nothing against fire from the sky");
            Assert.That(Evaluate(definitions, Hit(EffectChannel.Thunder), aegis).Verdict, Is.EqualTo(HitVerdict.Applied));
            Assert.That(Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), seal).AmountCenti, Is.EqualTo(Hundred), "M12 does nothing against ordinary weapons");

            var underBoth = Evaluate(definitions, Hit(EffectChannel.FireZone), aegis, seal);
            Assert.That(underBoth.AmountCenti, Is.EqualTo(20 * Rules.Centi), "two wards of 80% on different channels are not 96% and not 160%");
            Assert.That(underBoth.ReducingEffectIds, Is.EqualTo(new[] { 2 }));

            // An independent creature's attack is neither an ordinary weapon nor the summoned creature of M06.
            foreach (var channel in new[] { EffectChannel.CreatureAttack, EffectChannel.SummonedCreature, EffectChannel.Ram })
                Assert.That(Evaluate(definitions, Hit(channel), aegis).AmountCenti, Is.EqualTo(Hundred), channel.ToString());
            Assert.That(Evaluate(definitions, Hit(EffectChannel.CreatureAttack), seal).AmountCenti, Is.EqualTo(Hundred), "the fixture's M12 lists the summoned creature only");
        }

        [Test]
        public void MagicWardMatrix_OneDefinitionNeutralizesReducesAndLeavesTheSeaDangerous()
        {
            var definitions = Catalogue();
            var definition = definitions.Find("M12");
            Assert.That(definition.Problem(), Is.Null);
            Assert.That((definition.Kind, definition.Responses.Length, definition.Channels.IsEmpty), Is.EqualTo((EffectKind.Ward, 7, true)));
            var seal = On(definitions, "M12", 3);

            foreach (var channel in new[] { EffectChannel.Thunder, EffectChannel.EmpoweredBonus, EffectChannel.GorgonGaze, EffectChannel.SirenDamage })
            {
                Assert.That(definition.Respond(channel), Is.EqualTo(ChannelResponse.Neutralize(channel)));
                var outcome = Evaluate(definitions, Hit(channel), seal);
                Assert.That((outcome.Verdict, outcome.AmountCenti, outcome.DecidingEffectId), Is.EqualTo((HitVerdict.Neutralized, 0L, 3)), channel.ToString());
            }
            foreach (var channel in new[] { EffectChannel.SummonedCreature, EffectChannel.FireZone, EffectChannel.WaterspoutDamage })
            {
                Assert.That(definition.Respond(channel), Is.EqualTo(ChannelResponse.Reduce(channel, 800)));
                var outcome = Evaluate(definitions, Hit(channel), seal);
                Assert.That((outcome.Verdict, outcome.AmountCenti), Is.EqualTo((HitVerdict.Applied, 20 * Rules.Centi)), channel.ToString());
                Assert.That(outcome.ReducingEffectIds, Is.EqualTo(new[] { 3 }));
            }

            // Reefs and whirlpools stay dangerous; the siren still pulls; the waterspout still turns the ship.
            foreach (var channel in new[]
                     {
                         EffectChannel.ReefImpact, EffectChannel.WhirlpoolDamage, EffectChannel.WhirlpoolPull, EffectChannel.ReefBarrier,
                         EffectChannel.SirenPull, EffectChannel.WaterspoutCourse, EffectChannel.OrdinaryWeapon,
                     })
            {
                Assert.That(definition.Respond(channel).Answer, Is.EqualTo(ChannelAnswer.None), channel.ToString());
                var outcome = Evaluate(definitions, Hit(channel), seal);
                Assert.That((outcome.Verdict, outcome.AmountCenti), Is.EqualTo((HitVerdict.Applied, Hundred)), channel.ToString());
            }
        }

        // ---------- Control is not damage ----------

        [Test]
        public void Control_IsNeverWeakenedByAShare_OnlyBlockedOutright()
        {
            var definitions = Catalogue();
            // A share declared over a control channel: the definition is refused and cannot be scheduled. Even if such
            // a record reached the resolver, neither it nor the hull would weaken the pull or the gaze.
            foreach (var channel in new[] { EffectChannel.WhirlpoolPull, EffectChannel.GorgonGaze })
            {
                var broken = definitions.Add(Protection("BROKEN" + (int)channel, EffectKind.Reduction, ProtectionFamily.MagicWard, ChannelResponse.Reduce(channel, 800)));
                Assert.That(broken.Problem(), Is.Not.Null, channel.ToString());
                Assert.Throws<ArgumentException>(() => On(definitions, broken.Id, 1));

                var smuggled = On(definitions, "M11", 1);
                smuggled.DefinitionId = broken.Id;
                var hit = Hit(channel);
                hit.HullShieldPermille = 700;
                hit.HullShieldChannels = EffectChannels.All;
                var outcome = Evaluate(definitions, hit, smuggled);
                Assert.That((outcome.Verdict, outcome.AmountCenti, outcome.HullShieldApplied), Is.EqualTo((HitVerdict.Applied, Hundred, false)), channel.ToString());
            }

            // The invulnerable hero takes no thunder but is still pulled and still held by the gaze;
            // the pilot is the one the whirlpool cannot hold, and M12 the one that cancels the gaze.
            var hero = On(definitions, "HERO", 2);
            Assert.That(Evaluate(definitions, Hit(EffectChannel.Thunder), hero).Verdict, Is.EqualTo(HitVerdict.Immune));
            Assert.That(Evaluate(definitions, Hit(EffectChannel.WhirlpoolPull), hero).Verdict, Is.EqualTo(HitVerdict.Applied));
            Assert.That(Evaluate(definitions, Hit(EffectChannel.GorgonGaze), hero).Verdict, Is.EqualTo(HitVerdict.Applied));
            var pilot = Evaluate(definitions, Hit(EffectChannel.WhirlpoolPull), hero, On(definitions, "PILOT", 3));
            Assert.That((pilot.Verdict, pilot.DecidingEffectId), Is.EqualTo((HitVerdict.Immune, 3)));
            Assert.That(Evaluate(definitions, Hit(EffectChannel.GorgonGaze), hero, On(definitions, "M12", 4)).Verdict, Is.EqualTo(HitVerdict.Neutralized));
        }

        // ---------- The order of the steps ----------

        [Test]
        public void Order_LegalityThenImmunityThenNeutralizationThenReduction()
        {
            var definitions = Catalogue();
            var hero = On(definitions, "HERO", 9);
            var seal = On(definitions, "M12", 4);
            var aegis = On(definitions, "M11", 1);
            // A second neutralization of thunder with a higher priority, to see who is named.
            var mirror = Protection("MIRROR", EffectKind.Neutralization, ProtectionFamily.Artifact,
                ChannelResponse.Neutralize(EffectChannel.Thunder), ChannelResponse.Neutralize(EffectChannel.GorgonGaze));
            mirror.Priority = 5;
            definitions.Add(mirror);

            var illegal = Hit(EffectChannel.Thunder);
            illegal.TargetIsLegal = false;
            Assert.That(Evaluate(definitions, illegal, hero, seal).Verdict, Is.EqualTo(HitVerdict.Illegal), "legality comes before any protection");
            Assert.That(Evaluate(definitions, Hit(EffectChannel.None), hero).Verdict, Is.EqualTo(HitVerdict.Illegal));
            Assert.That(Evaluate(definitions, Hit((EffectChannel)13), hero).Verdict, Is.EqualTo(HitVerdict.Illegal), "a number that is no channel");
            Assert.That(Evaluate(definitions, Hit(EffectChannel.Thunder, amount: -1)).Verdict, Is.EqualTo(HitVerdict.Illegal));
            foreach (var owner in new[] { -1, 3 })
            {
                var ownerless = Hit(EffectChannel.Thunder);
                ownerless.TargetOwner = owner;
                Assert.That(Evaluate(definitions, ownerless).Verdict, Is.EqualTo(HitVerdict.Illegal), "a target of no owner slot");
            }

            var immune = Evaluate(definitions, Hit(EffectChannel.Thunder), seal, hero);
            Assert.That((immune.Verdict, immune.DecidingEffectId, immune.AmountCenti), Is.EqualTo((HitVerdict.Immune, 9, 0L)), "immunity before neutralization");

            var neutralized = Evaluate(definitions, Hit(EffectChannel.Thunder), seal, On(definitions, "MIRROR", 6));
            Assert.That((neutralized.Verdict, neutralized.DecidingEffectId), Is.EqualTo((HitVerdict.Neutralized, 6)), "the higher priority is named");

            var immuneToWeapons = Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon), aegis, hero);
            Assert.That((immuneToWeapons.Verdict, immuneToWeapons.ReducingEffectIds.Length), Is.EqualTo((HitVerdict.Immune, 0)), "no reduction is counted once the hit is stopped");

            Assert.That(Evaluate(definitions, Hit(EffectChannel.SirenDamage), hero).Verdict, Is.EqualTo(HitVerdict.Applied), "immunity covers only its own channels");
        }

        // ---------- Whom an effect covers ----------

        [Test]
        public void Scope_SideProtection_CoversItsOwnSideAndNotTheEnemy()
        {
            var definitions = new Definitions();
            var banner = definitions.Add(Shield("BANNER", ProtectionFamily.OrdinaryWard, 800));
            banner.TargetFilter = TargetFilter.OwnSide;
            var onHumans = EffectInstance.Schedule(banner, 1, 1, EffectSourceKind.Hero, 2, Owners.Human, EffectTarget.Side(Owners.Human), 85);

            long LeftFor(int owner, bool structure = false)
            {
                var hit = Hit(EffectChannel.OrdinaryWeapon);
                hit.TargetOwner = owner;
                hit.TargetIsStructure = structure;
                return Evaluate(definitions, hit, onHumans).AmountCenti;
            }
            Assert.That(LeftFor(Owners.Human), Is.EqualTo(20 * Rules.Centi));
            Assert.That(LeftFor(Owners.Human, structure: true), Is.EqualTo(20 * Rules.Centi), "the whole side, its structures too");
            Assert.That(LeftFor(Owners.MainAi), Is.EqualTo(Hundred), "the enemy is not protected");
            Assert.That(LeftFor(Owners.Independent), Is.EqualTo(Hundred));

            // Laid over the whole map but admitting own ships only: the enemy's ship and one's own port are left out.
            var fleetWard = definitions.Add(Shield("FLEET", ProtectionFamily.OrdinaryWard, 800));
            var everywhere = EffectInstance.Schedule(fleetWard, 2, 2, EffectSourceKind.Hero, 2, Owners.MainAi, EffectTarget.WholeMap(), 85);
            long Under(int owner, bool structure)
            {
                var hit = Hit(EffectChannel.OrdinaryWeapon);
                hit.TargetOwner = owner;
                hit.TargetIsStructure = structure;
                return Evaluate(definitions, hit, everywhere).AmountCenti;
            }
            Assert.That(Under(Owners.MainAi, false), Is.EqualTo(20 * Rules.Centi));
            Assert.That(Under(Owners.MainAi, true), Is.EqualTo(Hundred));
            Assert.That(Under(Owners.Human, false), Is.EqualTo(Hundred));
        }

        [Test]
        public void Scope_AreaProtection_CoversOnlyWhatIsWithinItsRadius()
        {
            var definitions = new Definitions();
            var dome = definitions.Add(Shield("DOME", ProtectionFamily.OrdinaryWard, 800));
            dome.TargetFilter = TargetFilter.SeaArea;
            var harbour = definitions.Add(Shield("HARBOUR", ProtectionFamily.OrdinaryWard, 800));
            harbour.TargetFilter = TargetFilter.SeaArea | TargetFilter.OwnShip;
            var forAll = EffectInstance.Schedule(dome, 1, 1, EffectSourceKind.Sanctuary, 7, Owners.Human, EffectTarget.Area(100, 0, 50), 85);
            var forOwn = EffectInstance.Schedule(harbour, 2, 2, EffectSourceKind.Sanctuary, 7, Owners.Human, EffectTarget.Area(100, 0, 50), 85);

            long LeftAt(EffectInstance effect, double x, double y, int owner = Owners.Human)
            {
                var hit = Hit(EffectChannel.OrdinaryWeapon);
                hit.TargetX = x;
                hit.TargetY = y;
                hit.TargetOwner = owner;
                return Evaluate(definitions, hit, effect).AmountCenti;
            }
            Assert.That(LeftAt(forAll, 100, 0), Is.EqualTo(20 * Rules.Centi));
            Assert.That(LeftAt(forAll, 150, 0), Is.EqualTo(20 * Rules.Centi), "the edge is inside");
            Assert.That(LeftAt(forAll, 130, 40), Is.EqualTo(20 * Rules.Centi), "30-40-50");
            Assert.That(LeftAt(forAll, 150.5, 0), Is.EqualTo(Hundred), "outside the radius");
            Assert.That(LeftAt(forAll, 140, 40), Is.EqualTo(Hundred), "inside the square around it, outside the circle");
            Assert.That(LeftAt(forAll, 0, 0), Is.EqualTo(Hundred));

            // A filter that names only the place covers whoever is there; one that also names own ships leaves the enemy out.
            Assert.That(LeftAt(forAll, 100, 0, Owners.MainAi), Is.EqualTo(20 * Rules.Centi));
            Assert.That(LeftAt(forOwn, 100, 0, Owners.Human), Is.EqualTo(20 * Rules.Centi));
            Assert.That(LeftAt(forOwn, 100, 0, Owners.MainAi), Is.EqualTo(Hundred));
        }

        [Test]
        public void Scope_HostileMeansAtWar_NotMerelyAnotherOwner()
        {
            HitEvent Of(int owner, SideRelations relations, bool structure = false)
            {
                var hit = Hit(EffectChannel.OrdinaryWeapon);
                hit.TargetOwner = owner;
                hit.Relations = relations;
                hit.TargetIsStructure = structure;
                return hit;
            }
            var war = SideRelations.AllHostile;
            var truceWithIndependents = SideRelations.AllHostile.With(Owners.Human, Owners.Independent, false);

            Assert.That(EffectScope.Admits(TargetFilter.HostileShip, Owners.Human, Of(Owners.MainAi, war)), Is.True);
            Assert.That(EffectScope.Admits(TargetFilter.HostileShip, Owners.Human, Of(Owners.MainAi, SideRelations.NoneHostile)), Is.False);
            Assert.That(EffectScope.Admits(TargetFilter.HostileShip, Owners.Human, Of(Owners.Human, war)), Is.False, "nobody is hostile to itself");
            Assert.That(EffectScope.Admits(TargetFilter.HostileShip, Owners.Human, Of(Owners.MainAi, war, structure: true)), Is.False);
            Assert.That(EffectScope.Admits(TargetFilter.HostileStructure, Owners.Human, Of(Owners.MainAi, war, structure: true)), Is.True);

            // The third slot is not everybody's enemy: at peace it is reached only by a filter that names independents.
            Assert.That(EffectScope.Admits(TargetFilter.HostileShip, Owners.Human, Of(Owners.Independent, truceWithIndependents)), Is.False);
            Assert.That(EffectScope.Admits(TargetFilter.HostileShip, Owners.Human, Of(Owners.Independent, war)), Is.True);
            Assert.That(EffectScope.Admits(TargetFilter.IndependentActor, Owners.Human, Of(Owners.Independent, truceWithIndependents)), Is.True);
            Assert.That(EffectScope.Admits(TargetFilter.IndependentActor, Owners.Independent, Of(Owners.Independent, war)), Is.False, "its own is not 'an independent' to it");
            Assert.That(EffectScope.Admits(TargetFilter.OwnShip, Owners.Independent, Of(Owners.Independent, war)), Is.True);
        }

        // ---------- Once per hit, target and channel ----------

        [Test]
        public void Settle_SameHitTargetAndChannelOfferedAgain_ChangesNothing()
        {
            var definitions = Catalogue();
            var effects = new[] { On(definitions, "M11", 1) };
            var ledger = new HitLedger();

            var first = ProtectionResolver.Settle(Hit(EffectChannel.OrdinaryWeapon, hitId: 77), effects, definitions, ledger);
            var again = ProtectionResolver.Settle(Hit(EffectChannel.OrdinaryWeapon, hitId: 77), effects, definitions, ledger);
            var next = ProtectionResolver.Settle(Hit(EffectChannel.OrdinaryWeapon, hitId: 78), effects, definitions, ledger);

            Assert.That((first.Verdict, first.AmountCenti), Is.EqualTo((HitVerdict.Applied, 20 * Rules.Centi)));
            Assert.That((again.Verdict, again.AmountCenti), Is.EqualTo((HitVerdict.AlreadySettled, 0L)));
            Assert.That((next.Verdict, next.AmountCenti), Is.EqualTo((HitVerdict.Applied, 20 * Rules.Centi)));
            Assert.That(ledger.Count, Is.EqualTo(2));
        }

        [Test]
        public void Settle_EmpoweredShot_SettlesItsOrdinaryPartAndItsBonusPart()
        {
            var definitions = Catalogue();
            var effects = new[] { On(definitions, "M11", 1) };
            var ledger = new HitLedger();
            // One shot of an empowered weapon (M07, x1.5): 100 through the ordinary channel and 50 through the bonus one.
            var ordinary = Hit(EffectChannel.OrdinaryWeapon, amount: Hundred, hitId: 300);
            var bonus = Hit(EffectChannel.EmpoweredBonus, amount: Hundred / 2, hitId: 300);

            var ordinaryOutcome = ProtectionResolver.Settle(ordinary, effects, definitions, ledger);
            var bonusOutcome = ProtectionResolver.Settle(bonus, effects, definitions, ledger);

            Assert.That((ordinaryOutcome.Verdict, ordinaryOutcome.AmountCenti), Is.EqualTo((HitVerdict.Applied, 20 * Rules.Centi)));
            Assert.That((bonusOutcome.Verdict, bonusOutcome.AmountCenti), Is.EqualTo((HitVerdict.Applied, 50 * Rules.Centi)), "the bonus part is not lost to the shot's ID being used");
            Assert.That(ledger.Count, Is.EqualTo(2));

            Assert.That(ProtectionResolver.Settle(ordinary, effects, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.AlreadySettled));
            Assert.That(ProtectionResolver.Settle(bonus, effects, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.AlreadySettled));
            Assert.That(ledger.Count, Is.EqualTo(2));
        }

        [Test]
        public void Settle_OneShotReachingTwoTargets_IsSettledForEachOfThem()
        {
            var definitions = Catalogue();
            var effects = new[] { On(definitions, "M11", 1) };
            var ledger = new HitLedger();
            var onWarded = Hit(EffectChannel.OrdinaryWeapon, hitId: 300, target: Target);
            var onBare = Hit(EffectChannel.OrdinaryWeapon, hitId: 300, target: Other);

            var warded = ProtectionResolver.Settle(onWarded, effects, definitions, ledger);
            var bare = ProtectionResolver.Settle(onBare, effects, definitions, ledger);

            Assert.That((warded.Verdict, warded.AmountCenti), Is.EqualTo((HitVerdict.Applied, 20 * Rules.Centi)));
            Assert.That((bare.Verdict, bare.AmountCenti), Is.EqualTo((HitVerdict.Applied, Hundred)), "the second target is not skipped");
            Assert.That(ProtectionResolver.Settle(onWarded, effects, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.AlreadySettled));
            Assert.That(ProtectionResolver.Settle(onBare, effects, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.AlreadySettled));
            Assert.That(ledger.Count, Is.EqualTo(2));
            Assert.That(ledger.IsSettled(new HitKey(300, Target, EffectChannel.OrdinaryWeapon)), Is.True);
            Assert.That(ledger.IsSettled(new HitKey(300, Target, EffectChannel.Ram)), Is.False);
            Assert.That(ledger.IsSettled(new HitKey(301, Target, EffectChannel.OrdinaryWeapon)), Is.False);
        }

        [Test]
        public void Settle_IllegalHitRecordsNothing_ButAStoppedHitIsRecorded()
        {
            var definitions = Catalogue();
            var ledger = new HitLedger();
            var hero = new[] { On(definitions, "HERO", 1) };

            var illegal = Hit(EffectChannel.OrdinaryWeapon, hitId: 5);
            illegal.TargetIsLegal = false;
            Assert.That(ProtectionResolver.Settle(illegal, hero, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.Illegal));
            Assert.That(ledger.IsSettled(new HitKey(5, Target, EffectChannel.OrdinaryWeapon)), Is.False);

            // The same hit, now legal: the immune answer is given once, so its "blocked" event cannot repeat either.
            Assert.That(ProtectionResolver.Settle(Hit(EffectChannel.OrdinaryWeapon, hitId: 5), hero, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.Immune));
            Assert.That(ProtectionResolver.Settle(Hit(EffectChannel.OrdinaryWeapon, hitId: 5), hero, definitions, ledger).Verdict, Is.EqualTo(HitVerdict.AlreadySettled));
        }

        // ---------- The timeline ----------

        [Test]
        public void Timeline_AnnouncedThenActiveThenExpired_OnAbsoluteTicks()
        {
            var definitions = Catalogue();
            var aegis = EffectInstance.Schedule(definitions.Find("M11"), 1, 500, EffectSourceKind.Sanctuary, 7, Owners.Human, EffectTarget.Entity(Target), 100);

            Assert.That((aegis.AnnounceTick, aegis.StartTick, aegis.EndTick, aegis.CancelTick),
                Is.EqualTo((100, 115, 115 + 45 * Rules.TickRate, EffectInstance.NotCancelled)));
            Assert.That(new[] { 100, 114, 115, 1464, 1465, 9999 }.Select(aegis.PhaseAt), Is.EqualTo(new[]
            {
                EffectPhase.Announced, EffectPhase.Announced, EffectPhase.Active, EffectPhase.Active, EffectPhase.Expired, EffectPhase.Expired,
            }));

            // The ward protects exactly while active: not during its announcement, not at its end tick.
            long At(int tick) => Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon, tick: tick), aegis).AmountCenti;
            Assert.That(new[] { 114, 115, 1464, 1465 }.Select(At), Is.EqualTo(new[] { Hundred, 20 * Rules.Centi, 20 * Rules.Centi, Hundred }));

            // A pause runs no tick: asking again at the same tick, however often, moves nothing.
            Assert.That(Enumerable.Range(0, 5).Select(_ => aegis.PhaseAt(114)).Distinct().Single(), Is.EqualTo(EffectPhase.Announced));
        }

        [Test]
        public void Timeline_MatchLongAndInstant()
        {
            var reef = Effect("M02", EffectKind.TerrainChange, EffectChannel.ReefBarrier);
            reef.Duration = EffectDuration.UntilMatchEnd;
            reef.AnnounceTicks = 90;
            var thunder = Effect("M08", EffectKind.Strike, EffectChannel.Thunder);
            thunder.Duration = EffectDuration.Instant;
            thunder.AnnounceTicks = 90;

            var standing = EffectInstance.Schedule(reef, 1, 1, EffectSourceKind.Sanctuary, 7, 0, EffectTarget.Area(10, 20, 25), 0);
            Assert.That(standing.EndTick, Is.EqualTo(EffectInstance.NoEndTick));
            Assert.That(standing.PhaseAt(int.MaxValue - 1), Is.EqualTo(EffectPhase.Active));

            var strike = EffectInstance.Schedule(thunder, 2, 2, EffectSourceKind.Sanctuary, 7, 0, EffectTarget.Area(10, 20, 60), 0);
            Assert.That(new[] { 89, 90, 91 }.Select(strike.PhaseAt), Is.EqualTo(new[] { EffectPhase.Announced, EffectPhase.Active, EffectPhase.Expired }));

            Assert.Throws<ArgumentOutOfRangeException>(() => EffectInstance.Schedule(reef, 3, 3, EffectSourceKind.Sanctuary, 7, 3, EffectTarget.WholeMap(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EffectInstance.Schedule(reef, 3, 3, EffectSourceKind.Sanctuary, 7, -1, EffectTarget.WholeMap(), 0));
        }

        [Test]
        public void Timeline_Cancelled_FromItsCancelTickOnAndNotBefore()
        {
            var definitions = Catalogue();
            var aegis = On(definitions, "M11", 1);

            Assert.That(aegis.Cancel(84), Is.False, "before it was announced");
            Assert.That(aegis.Cancel(1450), Is.False, "it has run out already");
            Assert.That(aegis.CancelTick, Is.EqualTo(EffectInstance.NotCancelled));

            Assert.That(aegis.Cancel(600), Is.True);
            Assert.That(aegis.Cancel(700), Is.False, "cancelled once");
            Assert.That(aegis.CancelTick, Is.EqualTo(600));
            Assert.That(new[] { 99, 100, 599, 600, 1449, 1450, 9999 }.Select(aegis.PhaseAt), Is.EqualTo(new[]
            {
                EffectPhase.Announced, EffectPhase.Active, EffectPhase.Active, EffectPhase.Cancelled, EffectPhase.Cancelled, EffectPhase.Cancelled, EffectPhase.Cancelled,
            }), "what it did before the cancel tick stays done");
            Assert.That(aegis.IsPendingOrActiveAt(600), Is.False);

            long At(int tick) => Evaluate(definitions, Hit(EffectChannel.OrdinaryWeapon, tick: tick), aegis).AmountCenti;
            Assert.That(new[] { 599, 600 }.Select(At), Is.EqualTo(new[] { 20 * Rules.Centi, Hundred }));

            // Cancelled while still announced: it never becomes active.
            var never = On(definitions, "M11", 2);
            Assert.That(never.Cancel(90), Is.True);
            Assert.That(new[] { 89, 90, 100, 200 }.Select(never.PhaseAt), Is.EqualTo(new[]
            {
                EffectPhase.Announced, EffectPhase.Cancelled, EffectPhase.Cancelled, EffectPhase.Cancelled,
            }));
        }

        [Test]
        public void Limits_OwnersPlacesAreFreedByExpiryAndCancelAndNotSharedBetweenOwners()
        {
            var whirlpool = Effect("M01", EffectKind.HazardZone, EffectChannel.WhirlpoolPull, EffectChannel.WhirlpoolDamage);
            whirlpool.Duration = EffectDuration.ForSeconds(60);
            whirlpool.AnnounceTicks = 90;
            whirlpool.MaxActivePerOwner = 2;
            whirlpool.TargetFilter = TargetFilter.SeaArea;
            EffectInstance Cast(int id, int owner, int tick) =>
                EffectInstance.Schedule(whirlpool, id, id, EffectSourceKind.Sanctuary, 7, owner, EffectTarget.Area(0, 0, 35), tick);
            var effects = new List<EffectInstance> { Cast(1, 0, 0), Cast(2, 0, 600), Cast(3, 1, 600) };

            Assert.That(EffectLimits.HasRoom(whirlpool, 0, effects, 650), Is.False, "two held, one of them still only announced");
            Assert.That(EffectLimits.HasRoom(whirlpool, 1, effects, 650), Is.True);
            Assert.That(EffectLimits.HasRoom(whirlpool, 0, effects, 1889), Is.False);
            Assert.That(EffectLimits.HasRoom(whirlpool, 0, effects, 1890), Is.True, "the first one ended at 90 + 1800");

            Assert.That(effects[1].Cancel(700), Is.True);
            Assert.That(EffectLimits.HasRoom(whirlpool, 0, effects, 699), Is.False);
            Assert.That(EffectLimits.HasRoom(whirlpool, 0, effects, 700), Is.True, "a dispelled whirlpool frees its place at once");
        }

        // ---------- Definitions ----------

        [Test]
        public void Definition_Protection_RefusesWhatBreaksTheContract()
        {
            Assert.That(Catalogue().Find("M11").Problem(), Is.Null);
            Assert.That(Catalogue().Find("M11").Stack, Is.EqualTo(StackRule.StrongestInFamily), "the default");
            Assert.That(Catalogue().Find("M11").Status, Is.EqualTo(EffectStatus.ContractOnly), "a definition is not a working effect");

            EffectDefinition Broken(Action<EffectDefinition> change)
            {
                var definition = Shield("X", ProtectionFamily.OrdinaryWard, 800);
                change(definition);
                return definition;
            }
            ChannelResponse[] Rows(params ChannelResponse[] rows) => rows;

            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce(EffectChannel.OrdinaryWeapon, 1001))).Problem(), Is.Not.Null, "a reduction above 100% would heal");
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce(EffectChannel.OrdinaryWeapon, 0))).Problem(), Is.Not.Null, "a reduction that reduces nothing");
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce(EffectChannel.OrdinaryWeapon, 1000))).Problem(), Is.Null);
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce(EffectChannel.Wind, 800))).Problem(), Is.Not.Null, "control is not reduced");
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce(EffectChannel.Observation, 800))).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce(EffectChannel.Ram, 800), ChannelResponse.Reduce(EffectChannel.Ram, 500))).Problem(), Is.Not.Null,
                "one channel, one answer");
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Reduce((EffectChannel)13, 800))).Problem(), Is.Not.Null, "a number that is no channel");
            Assert.That(Broken(d => d.Responses = Rows(default(ChannelResponse))).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Responses = Rows()).Problem(), Is.Not.Null, "a protection that answers nothing");
            Assert.That(Broken(d => d.Responses = null).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Responses = Rows(ChannelResponse.Neutralize(EffectChannel.Thunder))).Problem(), Is.Not.Null, "a mixed matrix is a Ward, not a Reduction");
            Assert.That(Broken(d => d.Kind = EffectKind.Immunity).Problem(), Is.Not.Null, "an Immunity cannot reduce");
            Assert.That(Broken(d => d.Kind = EffectKind.Ward).Problem(), Is.Null);
            Assert.That(Broken(d => d.Family = ProtectionFamily.None).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Family = (ProtectionFamily)99).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Stack = StackRule.Independent).Problem(), Is.Not.Null, "reductions never add up");
            Assert.That(Broken(d => d.Channels = ChannelMask.Of(EffectChannel.OrdinaryWeapon)).Problem(), Is.Not.Null, "the matrix is the only place for a protection's channels");
            Assert.That(Broken(d => d.MagnitudePermille = 800).Problem(), Is.Not.Null, "and for its strength");
            Assert.That(Broken(d => d.Id = " ").Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Kind = EffectKind.None).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Kind = (EffectKind)200).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.Duration = default).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.TargetFilter = TargetFilter.None).Problem(), Is.Not.Null);
            Assert.That(Broken(d => d.AnnounceTicks = -1).Problem(), Is.Not.Null);
        }

        [Test]
        public void Definition_KindAndChannel_MustAgree()
        {
            string Of(EffectKind kind, params EffectChannel[] channels) => Effect("X", kind, channels).Problem();

            Assert.That(Of(EffectKind.Blockade, EffectChannel.WhirlpoolPull, EffectChannel.GorgonGaze), Is.Null);
            Assert.That(Of(EffectKind.Blockade, EffectChannel.OrdinaryWeapon), Is.Not.Null, "a blockade is control, not damage");
            Assert.That(Of(EffectKind.Blockade, EffectChannel.WhirlpoolPull, EffectChannel.WhirlpoolDamage), Is.Not.Null);
            Assert.That(Of(EffectKind.Invisibility, EffectChannel.Observation), Is.Null);
            Assert.That(Of(EffectKind.Invisibility, EffectChannel.OrdinaryWeapon), Is.Not.Null);
            Assert.That(Of(EffectKind.Reveal, EffectChannel.Observation), Is.Null);
            Assert.That(Of(EffectKind.Reveal, EffectChannel.Wind), Is.Not.Null);
            Assert.That(Of(EffectKind.Empowerment, EffectChannel.EmpoweredBonus), Is.Null);
            Assert.That(Of(EffectKind.Empowerment, EffectChannel.OrdinaryWeapon), Is.Not.Null, "the ordinary part is not the empowerment's");
            Assert.That(Of(EffectKind.Empowerment, EffectChannel.EmpoweredBonus, EffectChannel.OrdinaryWeapon), Is.Not.Null);
            Assert.That(Of(EffectKind.HazardZone, EffectChannel.WhirlpoolPull, EffectChannel.WhirlpoolDamage), Is.Null, "a zone may pull and harm");
            Assert.That(Of(EffectKind.HazardZone), Is.Not.Null, "no channel at all");

            var withMatrix = Effect("X", EffectKind.Strike, EffectChannel.Thunder);
            withMatrix.Responses = new[] { ChannelResponse.Immune(EffectChannel.Thunder) };
            Assert.That(withMatrix.Problem(), Is.Not.Null, "only a protection answers channels");

            // A definition that breaks the contract cannot be put into the world.
            Assert.Throws<ArgumentException>(() => EffectInstance.Schedule(
                Effect("X", EffectKind.Blockade, EffectChannel.OrdinaryWeapon), 1, 1, EffectSourceKind.Sanctuary, 7, 0, EffectTarget.Entity(Target), 0));
            Assert.Throws<ArgumentNullException>(() => EffectInstance.Schedule(null, 1, 1, EffectSourceKind.Sanctuary, 7, 0, EffectTarget.Entity(Target), 0));
        }

        // ---------- The record in a save ----------

        static byte[] Save(params EffectInstance[] effects)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream)) EffectSerializer.Write(writer, effects);
                return stream.ToArray();
            }
        }

        static List<EffectInstance> Load(byte[] bytes)
        {
            using (var reader = new BinaryReader(new MemoryStream(bytes))) return EffectSerializer.Read(reader);
        }

        /// <summary>
        /// The layout written by hand, field by field. It pins the format: changing what EffectSerializer writes without
        /// raising its version makes these bytes unreadable or different.
        /// </summary>
        static byte[] Raw(byte version, int count, Action<BinaryWriter> records)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(version);
                    writer.Write(count);
                    records(writer);
                }
                return stream.ToArray();
            }
        }

        static void RawRecord(BinaryWriter writer, int id = 4, string definitionId = "M11", byte source = 1, int owner = 0, byte targetKind = 1,
            int entity = Target, double radius = 0, int announce = 85, int start = 100, int end = 1450, int cancel = -1)
        {
            writer.Write(id);
            writer.Write(definitionId);
            writer.Write(source);
            writer.Write(7);
            writer.Write(owner);
            writer.Write(targetKind);
            writer.Write(entity);
            writer.Write(0.0);
            writer.Write(0.0);
            writer.Write(radius);
            writer.Write(announce);
            writer.Write(start);
            writer.Write(end);
            writer.Write(cancel);
            writer.Write(9004L);
        }

        [Test]
        public void Record_SavedAndLoaded_IsTheSameRecordAndProtectsTheSame()
        {
            var definitions = Catalogue();
            var aegis = On(definitions, "M11", 4);
            var area = EffectInstance.Schedule(definitions.Find("M12"), 6, long.MaxValue, EffectSourceKind.Artifact, 31, Owners.Independent,
                EffectTarget.Area(-512.25, 0.1, 60.5), 3);
            var side = EffectInstance.Schedule(definitions.Find("HERO"), 7, -1, EffectSourceKind.Hero, 2, Owners.MainAi, EffectTarget.Side(Owners.MainAi), 0);
            Assert.That(side.Cancel(20), Is.True);

            var bytes = Save(aegis, area, side);
            var loaded = Load(bytes);

            Assert.That(loaded.Count, Is.EqualTo(3));
            foreach (var (before, after) in new[] { aegis, area, side }.Zip(loaded, (saved, read) => (saved, read)))
            {
                Assert.That((after.Id, after.DefinitionId, after.SourceKind, after.SourceId, after.Owner),
                    Is.EqualTo((before.Id, before.DefinitionId, before.SourceKind, before.SourceId, before.Owner)));
                Assert.That(after.Target, Is.EqualTo(before.Target));
                Assert.That((after.AnnounceTick, after.StartTick, after.EndTick, after.CancelTick, after.EventId),
                    Is.EqualTo((before.AnnounceTick, before.StartTick, before.EndTick, before.CancelTick, before.EventId)));
                Assert.That(new[] { 0, 19, 20, 100, 5000 }.Select(after.PhaseAt), Is.EqualTo(new[] { 0, 19, 20, 100, 5000 }.Select(before.PhaseAt)));
            }
            Assert.That(loaded[2].PhaseAt(20), Is.EqualTo(EffectPhase.Cancelled), "the cancel tick survives the save");
            Assert.That(Save(loaded.ToArray()), Is.EqualTo(bytes), "saving what was loaded gives the same bytes");

            // The loaded ward decides a hit exactly as the original did, and the hand-written layout is the saved one.
            var hit = Hit(EffectChannel.OrdinaryWeapon);
            Assert.That(ProtectionResolver.Evaluate(hit, loaded, definitions).AmountCenti, Is.EqualTo(20 * Rules.Centi));
            Assert.That(EffectSerializer.FormatVersion, Is.EqualTo(2), "version 1 held a phase byte where the cancel tick now is");
            Assert.That(Raw(EffectSerializer.FormatVersion, 1, writer => RawRecord(writer)), Is.EqualTo(Save(aegis)));
        }

        [Test]
        public void Record_Loading_RejectsOwnersOutsideTheThreeSlots([Values(-1, 3, int.MaxValue)] int owner)
        {
            var bytes = Raw(EffectSerializer.FormatVersion, 1, writer => RawRecord(writer, owner: owner));
            Assert.Throws<InvalidDataException>(() => Load(bytes));
        }

        [Test]
        public void Record_Loading_AcceptsEveryLegalOwner([Values(0, 1, 2)] int owner)
        {
            var loaded = Load(Raw(EffectSerializer.FormatVersion, 1, writer => RawRecord(writer, owner: owner)));
            Assert.That(loaded.Single().Owner, Is.EqualTo(owner));
        }

        [Test]
        public void Record_Loading_RejectsUnknownEnumsAndDamagedData()
        {
            void Rejected(string what, byte[] bytes) => Assert.Throws<InvalidDataException>(() => Load(bytes), what);
            byte[] One(Action<BinaryWriter> record) => Raw(EffectSerializer.FormatVersion, 1, record);

            Rejected("source None", One(writer => RawRecord(writer, source: 0)));
            Rejected("source unknown", One(writer => RawRecord(writer, source: 200)));
            Rejected("target None", One(writer => RawRecord(writer, targetKind: 0)));
            Rejected("target unknown", One(writer => RawRecord(writer, targetKind: 9)));
            Rejected("no ID", One(writer => RawRecord(writer, id: 0)));
            Rejected("no definition", One(writer => RawRecord(writer, definitionId: "")));
            Rejected("definition ID too long", One(writer => RawRecord(writer, definitionId: new string('M', EffectSerializer.MaxDefinitionIdLength + 1))));
            Rejected("entity target without entity", One(writer => RawRecord(writer, entity: 0)));
            Rejected("side target outside the slots", One(writer => RawRecord(writer, targetKind: 3, entity: 3)));
            Rejected("negative radius", One(writer => RawRecord(writer, radius: -1)));
            Rejected("NaN radius", One(writer => RawRecord(writer, radius: double.NaN)));
            Rejected("starts before its announcement", One(writer => RawRecord(writer, announce: 101)));
            Rejected("ends when it starts", One(writer => RawRecord(writer, end: 100)));
            Rejected("negative announcement", One(writer => RawRecord(writer, announce: -1)));
            Rejected("cancelled before its announcement", One(writer => RawRecord(writer, cancel: 84)));
            Rejected("cancelled after it ran out", One(writer => RawRecord(writer, cancel: 1450)));
            Rejected("a negative cancel tick that is not the marker", One(writer => RawRecord(writer, cancel: -2)));
            Rejected("the previous version", Raw(1, 1, writer => RawRecord(writer)));
            Rejected("a later version", Raw((byte)(EffectSerializer.FormatVersion + 1), 1, writer => RawRecord(writer)));
            Rejected("negative count", Raw(EffectSerializer.FormatVersion, -1, writer => { }));
            Rejected("absurd count", Raw(EffectSerializer.FormatVersion, EffectSerializer.MaxRecords + 1, writer => { }));
            Rejected("the same ID twice", Raw(EffectSerializer.FormatVersion, 2, writer => { RawRecord(writer); RawRecord(writer); }));

            Assert.Throws<EndOfStreamException>(() => Load(Raw(EffectSerializer.FormatVersion, 2, writer => RawRecord(writer))), "fewer records than promised");

            // The edges that are legal: cancelled at its announcement, and at its last tick.
            Assert.That(Load(One(writer => RawRecord(writer, cancel: 85))).Single().PhaseAt(85), Is.EqualTo(EffectPhase.Cancelled));
            Assert.That(Load(One(writer => RawRecord(writer, cancel: 1449))).Single().PhaseAt(1448), Is.EqualTo(EffectPhase.Active));
        }

        [Test]
        public void Record_Saving_RefusesARecordTheWorldCouldNotHold()
        {
            var definitions = Catalogue();
            var outside = On(definitions, "M11", 4);
            outside.Owner = 3;
            Assert.Throws<InvalidOperationException>(() => Save(outside));
            Assert.Throws<InvalidOperationException>(() => Save(On(definitions, "M11", 4), On(definitions, "M12", 4)));
        }

        // ---------- The executor ----------

        sealed class Recording : IEffectExecutor
        {
            public readonly List<string> Calls = new List<string>();
            public EffectResult Answer = EffectResult.Ok;
            public EffectKind Kind { get; set; }
            public EffectResult Validate(in EffectContext context) { Calls.Add("Validate"); return Answer; }
            public EffectResult Activate(in EffectContext context) { Calls.Add("Activate"); return Answer; }
            public EffectResult Step(in EffectContext context) { Calls.Add("Step"); return Answer; }
            public EffectResult Expire(in EffectContext context) { Calls.Add("Expire"); return Answer; }
        }

        static EffectContext Context(EffectDefinition definition, EffectInstance instance, int tick, IReadOnlyList<EffectInstance> effects = null) =>
            new EffectContext { Definition = definition, Instance = instance, Tick = tick, Effects = effects ?? new[] { instance } };

        static EffectResult[] WholeLife(EffectExecutors executors, EffectDefinition definition, EffectInstance instance) => new[]
        {
            executors.Validate(Context(definition, instance, instance.AnnounceTick)),
            executors.Activate(Context(definition, instance, instance.StartTick)),
            executors.Step(Context(definition, instance, instance.StartTick + 1)),
            executors.Expire(Context(definition, instance, instance.EndTick)),
        };

        /// <summary>A definition of the kind that keeps the contract, whatever the kind asks of its channels.</summary>
        static EffectDefinition ValidOf(EffectKind kind)
        {
            var id = "K" + (int)kind;
            switch (kind)
            {
                case EffectKind.Reduction: return Shield(id, ProtectionFamily.OrdinaryWard, 800);
                case EffectKind.Immunity: return Protection(id, kind, ProtectionFamily.Hero, ChannelResponse.Immune(EffectChannel.Thunder));
                case EffectKind.Neutralization: return Protection(id, kind, ProtectionFamily.Artifact, ChannelResponse.Neutralize(EffectChannel.GorgonGaze));
                case EffectKind.Ward:
                    return Protection(id, kind, ProtectionFamily.MagicWard, ChannelResponse.Neutralize(EffectChannel.Thunder), ChannelResponse.Reduce(EffectChannel.FireZone, 800));
                case EffectKind.Blockade: return Effect(id, kind, EffectChannel.WhirlpoolPull);
                case EffectKind.Invisibility:
                case EffectKind.Reveal: return Effect(id, kind, EffectChannel.Observation);
                case EffectKind.Empowerment: return Effect(id, kind, EffectChannel.EmpoweredBonus);
                default: return Effect(id, kind, EffectChannel.OrdinaryWeapon);
            }
        }

        [Test]
        public void Executor_KindWithoutOne_SaysNotImplementedAtEveryStage()
        {
            var executors = new EffectExecutors();
            var kinds = Enum.GetValues(typeof(EffectKind)).Cast<EffectKind>().Where(kind => kind != EffectKind.None).ToArray();
            Assert.That(kinds.Length, Is.GreaterThanOrEqualTo(16));

            foreach (var kind in kinds)
            {
                var definition = ValidOf(kind);
                Assert.That(definition.Problem(), Is.Null, kind.ToString());
                // Even marked executable: what is missing is the executor, and that is said aloud.
                definition.Status = EffectStatus.Executable;
                var instance = EffectInstance.Schedule(definition, 1, 1, EffectSourceKind.Sanctuary, 7, 0, EffectTarget.Entity(Target), 10);

                Assert.That(executors.IsImplemented(kind), Is.False, kind.ToString());
                Assert.That(WholeLife(executors, definition, instance), Is.All.EqualTo(EffectResult.NotImplemented), kind.ToString());
            }
        }

        [Test]
        public void Executor_DefinitionMarkedContractOnly_IsNotCarriedOutEvenWithAnExecutor()
        {
            var executors = new EffectExecutors();
            var ward = new Recording { Kind = EffectKind.Reduction };
            executors.Register(ward);
            var definitions = Catalogue();
            var aegis = definitions.Find("M11");
            Assert.That(aegis.Status, Is.EqualTo(EffectStatus.ContractOnly));

            Assert.That(WholeLife(executors, aegis, On(definitions, "M11", 1)), Is.All.EqualTo(EffectResult.NotImplemented));
            Assert.That(ward.Calls, Is.Empty, "the executor is not even asked");

            aegis.Status = EffectStatus.Executable;
            Assert.That(WholeLife(executors, aegis, On(definitions, "M11", 2)), Is.All.EqualTo(EffectResult.Ok));
            Assert.That(ward.Calls.Count, Is.EqualTo(4));
        }

        [Test]
        public void Executor_Registered_AnswersForItsKindOnly()
        {
            var executors = new EffectExecutors();
            var ward = new Recording { Kind = EffectKind.Reduction };
            executors.Register(ward);
            var definitions = Catalogue();
            var aegis = definitions.Find("M11");
            var seal = definitions.Find("M12");
            aegis.Status = EffectStatus.Executable;
            seal.Status = EffectStatus.Executable;

            Assert.That(WholeLife(executors, aegis, On(definitions, "M11", 1)), Is.All.EqualTo(EffectResult.Ok));
            Assert.That(ward.Calls, Is.EqualTo(new[] { "Validate", "Activate", "Step", "Expire" }));

            Assert.That(WholeLife(executors, seal, On(definitions, "M12", 2)), Is.All.EqualTo(EffectResult.NotImplemented));
            Assert.That(ward.Calls.Count, Is.EqualTo(4), "the Reduction's executor is not asked about a Ward");

            ward.Answer = EffectResult.Rejected(EffectRejection.IllegalTarget);
            Assert.That(executors.Validate(Context(aegis, On(definitions, "M11", 3), 85)), Is.EqualTo(EffectResult.Rejected(EffectRejection.IllegalTarget)));

            Assert.Throws<InvalidOperationException>(() => executors.Register(new Recording { Kind = EffectKind.Reduction }));
            Assert.Throws<ArgumentException>(() => executors.Register(new Recording { Kind = EffectKind.None }));
            Assert.Throws<ArgumentException>(() => EffectResult.Rejected(EffectRejection.None));
        }

        [Test]
        public void Executor_SharedContractIsCheckedBeforeTheExecutorIsAsked()
        {
            var executors = new EffectExecutors();
            var zone = new Recording { Kind = EffectKind.TerrainChange };
            executors.Register(zone);
            var reef = Effect("M02", EffectKind.TerrainChange, EffectChannel.ReefBarrier, EffectChannel.ReefImpact);
            reef.Duration = EffectDuration.UntilMatchEnd;
            reef.AnnounceTicks = 90;
            reef.MaxActivePerOwner = 3;
            reef.TargetFilter = TargetFilter.SeaArea;
            reef.Status = EffectStatus.Executable;
            EffectInstance Cast(int id, int owner = 0) =>
                EffectInstance.Schedule(reef, id, id, EffectSourceKind.Sanctuary, 7, owner, EffectTarget.Area(id * 100, 0, 25), 0);

            // Three own reefs stand; the fourth is refused before payment, the opponent's first is not.
            var standing = new List<EffectInstance> { Cast(1), Cast(2), Cast(3) };
            Assert.That(executors.Validate(Context(reef, Cast(4), 500, standing)), Is.EqualTo(EffectResult.Rejected(EffectRejection.OwnerLimit)));
            Assert.That(executors.Validate(Context(reef, Cast(4, 1), 500, standing)), Is.EqualTo(EffectResult.Ok));
            Assert.That(executors.Validate(Context(reef, standing[2], 500, standing)), Is.EqualTo(EffectResult.Ok), "the third does not count against itself");
            Assert.That(zone.Calls, Is.EqualTo(new[] { "Validate", "Validate" }));

            var other = Effect("M01", EffectKind.TerrainChange, EffectChannel.WhirlpoolPull);
            other.Status = EffectStatus.Executable;
            Assert.That(executors.Activate(Context(other, Cast(5), 90)), Is.EqualTo(EffectResult.Rejected(EffectRejection.DefinitionMismatch)));

            var damaged = Cast(6);
            damaged.Owner = 3;
            Assert.That(executors.Activate(Context(reef, damaged, 90)), Is.EqualTo(EffectResult.Rejected(EffectRejection.BrokenRecord)));

            var sound = Cast(7);
            reef.MagnitudePermille = -1;
            Assert.That(executors.Activate(Context(reef, sound, 90)), Is.EqualTo(EffectResult.Rejected(EffectRejection.BrokenDefinition)));
            reef.MagnitudePermille = 0;

            // Each stage belongs to its phase: no step during the announcement, no end while it stands.
            var fresh = Cast(8);
            Assert.That(executors.Activate(Context(reef, fresh, 89)), Is.EqualTo(EffectResult.Rejected(EffectRejection.WrongPhase)));
            Assert.That(executors.Step(Context(reef, fresh, 89)), Is.EqualTo(EffectResult.Rejected(EffectRejection.WrongPhase)));
            Assert.That(executors.Expire(Context(reef, fresh, 5000)), Is.EqualTo(EffectResult.Rejected(EffectRejection.WrongPhase)));
            Assert.That(fresh.Cancel(4000), Is.True);
            Assert.That(executors.Step(Context(reef, fresh, 4000)), Is.EqualTo(EffectResult.Rejected(EffectRejection.WrongPhase)), "a cancelled effect steps no more");
            Assert.That(executors.Expire(Context(reef, fresh, 5000)), Is.EqualTo(EffectResult.Ok));
            Assert.That(zone.Calls.Count, Is.EqualTo(3), "only the legal end reached the executor");
        }
    }
}
