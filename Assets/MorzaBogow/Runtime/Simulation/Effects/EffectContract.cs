using System;

namespace MorzaBogow.Simulation.Effects
{
    // P02-C: the contract every power, ward, hero trait and artifact shares (docs/NOWY_PLAN_GRY.md, sections 8 and 12).
    // These are data and rules of the game; nothing here is wired into World, and no effect is carried out yet (P07).
    // Every enum below is written to saves as its number: add new values at the end, never reorder.

    /// <summary>What an effect does. Each kind needs its own executor (P07); until one exists the kind is not implemented.</summary>
    public enum EffectKind : byte
    {
        None = 0,
        /// <summary>A protection whose every answer is a reduction by a share.</summary>
        Reduction = 1,
        /// <summary>A protection whose every answer is full insensitivity: a hero's invulnerability, a pilot before whirlpools.</summary>
        Immunity = 2,
        /// <summary>A protection whose every answer cancels one named influence, as the mirror cancels the gorgon.</summary>
        Neutralization = 3,
        /// <summary>Strengthens own weapons through a channel of its own (M07).</summary>
        Empowerment = 4,
        Accuracy = 5,
        /// <summary>Stops, holds or pulls: control of a ship, separate from damage.</summary>
        Blockade = 6,
        Invisibility = 7,
        Reveal = 8,
        Restoration = 9,
        Disruption = 10,
        /// <summary>A zone that keeps harming whatever is inside it (whirlpool core, waterspout, fire).</summary>
        HazardZone = 11,
        /// <summary>One strike after its announcement (M08).</summary>
        Strike = 12,
        Summon = 13,
        TerrainChange = 14,
        WindField = 15,
        /// <summary>A protection that answers different channels differently (M12: cancels some, reduces others).</summary>
        Ward = 16,
    }

    /// <summary>
    /// The way an influence reaches its target. Protections name the channels they answer; a channel nobody names
    /// goes through untouched. Every channel belongs to exactly one group: damage, control or observation.
    /// </summary>
    public enum EffectChannel : byte
    {
        None = 0,
        OrdinaryWeapon = 1,
        Ram = 2,
        Boarding = 3,
        /// <summary>The added part of an empowered weapon (M07); the ordinary part stays on its own channel.</summary>
        EmpoweredBonus = 4,
        Thunder = 5,
        FireZone = 6,
        /// <summary>The creature called by M06.</summary>
        SummonedCreature = 7,
        WaterspoutDamage = 8,
        WhirlpoolDamage = 9,
        ReefImpact = 10,
        /// <summary>Control, not damage: the gaze holds a ship still. It is blocked outright or it works.</summary>
        GorgonGaze = 11,
        SirenDamage = 12,

        WhirlpoolPull = 16,
        ReefBarrier = 17,
        WaterspoutCourse = 18,
        SirenPull = 19,
        Wind = 20,

        /// <summary>Ordinary sight and target choice; neither damage nor control.</summary>
        Observation = 24,

        /// <summary>The attack of an independent creature: not the summoned one (M06) and not an ordinary weapon.</summary>
        CreatureAttack = 25,
    }

    /// <summary>A set of channels, one bit a channel.</summary>
    public readonly struct ChannelMask : IEquatable<ChannelMask>
    {
        public readonly uint Bits;

        public ChannelMask(uint bits) => Bits = bits;

        public static readonly ChannelMask Empty = new ChannelMask(0);

        public static ChannelMask Of(params EffectChannel[] channels)
        {
            uint bits = 0;
            foreach (var channel in channels)
            {
                if (!EffectChannels.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channels), channel, "Not a channel.");
                bits |= 1u << (int)channel;
            }
            return new ChannelMask(bits);
        }

        public bool Contains(EffectChannel channel) => EffectChannels.IsDefined(channel) && (Bits & 1u << (int)channel) != 0;
        public bool Overlaps(ChannelMask other) => (Bits & other.Bits) != 0;
        public bool IsWithin(ChannelMask other) => (Bits & ~other.Bits) == 0;
        public bool IsEmpty => Bits == 0;

        public bool Equals(ChannelMask other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is ChannelMask other && Equals(other);
        public override int GetHashCode() => (int)Bits;
        public static ChannelMask operator |(ChannelMask left, ChannelMask right) => new ChannelMask(left.Bits | right.Bits);
    }

    public static class EffectChannels
    {
        // Channels 1..10, 12 and 25; channels 11 and 16..20; channel 24.
        const uint DamageBits = 0x7FEu | 1u << 12 | 1u << 25;
        const uint ControlBits = 1u << 11 | 0x1F0000u;
        const uint ObservationBits = 1u << 24;

        /// <summary>Channels whose amount is hull, crew or module loss.</summary>
        public static readonly ChannelMask Damage = new ChannelMask(DamageBits);
        /// <summary>Channels that move, hold, steer or immobilize.</summary>
        public static readonly ChannelMask Control = new ChannelMask(ControlBits);
        public static readonly ChannelMask Observing = new ChannelMask(ObservationBits);
        public static readonly ChannelMask All = new ChannelMask(DamageBits | ControlBits | ObservationBits);

        public static bool IsDefined(EffectChannel channel) => Has(DamageBits | ControlBits | ObservationBits, channel);

        public static bool IsDamage(EffectChannel channel) => Has(DamageBits, channel);

        /// <summary>A control channel is never weakened by a share; it is either blocked or it works.</summary>
        public static bool IsControl(EffectChannel channel) => Has(ControlBits, channel);

        public static bool IsObservation(EffectChannel channel) => Has(ObservationBits, channel);

        static bool Has(uint bits, EffectChannel channel) => (int)channel < 32 && (bits & 1u << (int)channel) != 0;
    }

    /// <summary>
    /// Whose protection it is. Within one family only the strongest active reduction of a channel counts; different
    /// families multiply. The material hull armour is not an effect and is applied as its own step.
    /// </summary>
    public enum ProtectionFamily : byte
    {
        None = 0,
        /// <summary>Wards against ordinary weapons (M11 and whatever else gives the same kind of shield).</summary>
        OrdinaryWard = 1,
        /// <summary>Wards against magic (M12).</summary>
        MagicWard = 2,
        Hero = 3,
        Artifact = 4,
        Crew = 5,
        Structure = 6,
    }

    public static class ProtectionFamilies
    {
        /// <summary>One more than the largest family number: the length of a table indexed by family.</summary>
        public static readonly int TableLength = Largest() + 1;

        static int Largest()
        {
            var largest = 0;
            foreach (ProtectionFamily family in Enum.GetValues(typeof(ProtectionFamily))) largest = Math.Max(largest, (int)family);
            return largest;
        }
    }

    public enum StackRule : byte
    {
        /// <summary>The strongest active one of the family answers the channel; equal ones do not add up.</summary>
        StrongestInFamily = 1,
        /// <summary>Using it again on the same target restarts its time instead of adding a second one.</summary>
        Refresh = 2,
        /// <summary>Every use is its own effect (zones, summoned creatures), within the owner's limit.</summary>
        Independent = 3,
    }

    public enum EffectDurationKind : byte
    {
        /// <summary>A number of 30 Hz ticks.</summary>
        Ticks = 1,
        /// <summary>Until the operation ends or the effect is legally removed.</summary>
        UntilMatchEnd = 2,
        /// <summary>As long as its source exists.</summary>
        WhileSource = 3,
        /// <summary>One tick: the strike itself.</summary>
        Instant = 4,
    }

    public readonly struct EffectDuration : IEquatable<EffectDuration>
    {
        public readonly EffectDurationKind Kind;
        public readonly int Ticks;

        EffectDuration(EffectDurationKind kind, int ticks) { Kind = kind; Ticks = ticks; }

        public static EffectDuration ForTicks(int ticks)
        {
            if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "A timed effect lasts at least one tick.");
            return new EffectDuration(EffectDurationKind.Ticks, ticks);
        }

        public static EffectDuration ForSeconds(int seconds) => ForTicks(checked(seconds * Rules.TickRate));
        public static readonly EffectDuration UntilMatchEnd = new EffectDuration(EffectDurationKind.UntilMatchEnd, 0);
        public static readonly EffectDuration WhileSource = new EffectDuration(EffectDurationKind.WhileSource, 0);
        public static readonly EffectDuration Instant = new EffectDuration(EffectDurationKind.Instant, 0);

        public bool Equals(EffectDuration other) => Kind == other.Kind && Ticks == other.Ticks;
        public override bool Equals(object obj) => obj is EffectDuration other && Equals(other);
        public override int GetHashCode() => (int)Kind * 397 ^ Ticks;
    }

    /// <summary>
    /// What may be chosen as the target, and whom an area, side or map-wide effect covers. The flags naming sides and
    /// kinds of object are read from the effect's owner; with none of them set, whoever is in the place is covered.
    /// Being listed here is not being visible: fog is checked separately.
    /// </summary>
    [Flags]
    public enum TargetFilter : ushort
    {
        None = 0,
        OwnShip = 1,
        HostileShip = 2,
        IndependentActor = 4,
        OwnStructure = 8,
        HostileStructure = 16,
        SeaArea = 32,
        OwnSide = 64,
        WholeMap = 128,
    }

    /// <summary>How far the definition has got. A definition being present does not mean its effect works.</summary>
    public enum EffectStatus : byte
    {
        /// <summary>Data and contract only; no executor carries it out, even where one is registered for its kind.</summary>
        ContractOnly = 1,
        /// <summary>An executor exists and has been accepted (P07 at the earliest).</summary>
        Executable = 2,
    }

    public enum ChannelAnswer : byte
    {
        None = 0,
        /// <summary>Lets through what is left after the share; damage channels only.</summary>
        Reduce = 1,
        /// <summary>Cancels this influence.</summary>
        Neutralize = 2,
        /// <summary>Full insensitivity to the channel.</summary>
        Immune = 3,
    }

    /// <summary>One row of a protection's matrix: what it answers to one channel.</summary>
    public readonly struct ChannelResponse : IEquatable<ChannelResponse>
    {
        public readonly EffectChannel Channel;
        public readonly ChannelAnswer Answer;
        /// <summary>The share taken away, in thousandths; 0 unless the answer is Reduce.</summary>
        public readonly int ReducePermille;

        ChannelResponse(EffectChannel channel, ChannelAnswer answer, int permille) { Channel = channel; Answer = answer; ReducePermille = permille; }

        public static ChannelResponse Reduce(EffectChannel channel, int permille) => new ChannelResponse(channel, ChannelAnswer.Reduce, permille);
        public static ChannelResponse Neutralize(EffectChannel channel) => new ChannelResponse(channel, ChannelAnswer.Neutralize, 0);
        public static ChannelResponse Immune(EffectChannel channel) => new ChannelResponse(channel, ChannelAnswer.Immune, 0);

        public bool Equals(ChannelResponse other) => Channel == other.Channel && Answer == other.Answer && ReducePermille == other.ReducePermille;
        public override bool Equals(object obj) => obj is ChannelResponse other && Equals(other);
        public override int GetHashCode() => ((int)Channel << 8 | (int)Answer) * 397 ^ ReducePermille;
    }

    /// <summary>
    /// One effect as data, one per catalogue ID ("M11", "M12"); this module knows nothing else about the catalogue.
    /// A protection says what it answers channel by channel in <see cref="Responses"/>; every other kind names the
    /// channels it works through in <see cref="Channels"/>.
    /// </summary>
    public sealed class EffectDefinition
    {
        public const int FullPermille = 1000;

        public string Id;
        public EffectKind Kind;
        /// <summary>The channels a non-protection works through. Empty for a protection.</summary>
        public ChannelMask Channels;
        /// <summary>A protection's matrix, a channel at most once; a channel not listed is not answered. Null or empty otherwise.</summary>
        public ChannelResponse[] Responses;
        public ProtectionFamily Family;
        public StackRule Stack = StackRule.StrongestInFamily;
        /// <summary>Strength in thousandths of what is not a protection: 1500 is an empowerment of x1.5. Zero for a protection.</summary>
        public int MagnitudePermille;
        public EffectDuration Duration;
        /// <summary>Ticks between the paid use and the start, during which the effect is shown but does nothing.</summary>
        public int AnnounceTicks;
        public TargetFilter TargetFilter;
        /// <summary>Among protections of one step the higher priority is named as the deciding one.</summary>
        public int Priority;
        /// <summary>How many an owner may have announced or active at once; 0 is no limit.</summary>
        public int MaxActivePerOwner;
        public EffectStatus Status = EffectStatus.ContractOnly;

        public bool IsProtection =>
            Kind == EffectKind.Reduction || Kind == EffectKind.Immunity || Kind == EffectKind.Neutralization || Kind == EffectKind.Ward;

        /// <summary>What this definition answers to a channel; Answer is None where the matrix is silent.</summary>
        public ChannelResponse Respond(EffectChannel channel)
        {
            if (Responses != null)
                for (var i = 0; i < Responses.Length; i++)
                    if (Responses[i].Channel == channel) return Responses[i];
            return default;
        }

        /// <summary>Null when the definition keeps the contract, otherwise what is wrong with it.</summary>
        public string Problem()
        {
            if (string.IsNullOrWhiteSpace(Id)) return "An effect needs an ID.";
            if (Kind == EffectKind.None || !Enum.IsDefined(typeof(EffectKind), Kind)) return $"{Id}: unknown kind.";
            if (!Enum.IsDefined(typeof(ProtectionFamily), Family)) return $"{Id}: unknown family.";
            if (!Enum.IsDefined(typeof(StackRule), Stack)) return $"{Id}: unknown stacking rule.";
            if (!Enum.IsDefined(typeof(EffectDurationKind), Duration.Kind)) return $"{Id}: no duration.";
            if (Duration.Kind == EffectDurationKind.Ticks && Duration.Ticks <= 0) return $"{Id}: a timed effect lasts at least one tick.";
            if (!Enum.IsDefined(typeof(EffectStatus), Status)) return $"{Id}: unknown status.";
            if (AnnounceTicks < 0 || MaxActivePerOwner < 0 || MagnitudePermille < 0) return $"{Id}: negative number.";
            if (TargetFilter == TargetFilter.None) return $"{Id}: nothing can be its target.";
            return IsProtection ? MatrixProblem() : ChannelProblem();
        }

        string MatrixProblem()
        {
            if (Responses == null || Responses.Length == 0) return $"{Id}: a protection answers at least one channel.";
            if (!Channels.IsEmpty) return $"{Id}: a protection names its channels in the matrix only.";
            if (MagnitudePermille != 0) return $"{Id}: a protection's strength is in the matrix only.";
            uint seen = 0;
            foreach (var response in Responses)
            {
                if (!EffectChannels.IsDefined(response.Channel)) return $"{Id}: answers a channel that does not exist.";
                var bit = 1u << (int)response.Channel;
                if ((seen & bit) != 0) return $"{Id}: answers {response.Channel} twice.";
                seen |= bit;
                switch (response.Answer)
                {
                    case ChannelAnswer.Reduce:
                        // A reduction can stop a channel entirely but can never turn harm into healing.
                        if (response.ReducePermille <= 0 || response.ReducePermille > FullPermille) return $"{Id}: a reduction outside 0.1-100%.";
                        if (!EffectChannels.IsDamage(response.Channel)) return $"{Id}: only damage can be reduced by a share.";
                        if (Family == ProtectionFamily.None) return $"{Id}: a reduction belongs to a family.";
                        if (Stack != StackRule.StrongestInFamily) return $"{Id}: reductions never add up.";
                        if (Kind != EffectKind.Reduction && Kind != EffectKind.Ward) return $"{Id}: {Kind} cannot reduce.";
                        break;
                    case ChannelAnswer.Neutralize:
                        if (response.ReducePermille != 0) return $"{Id}: a share on an answer that is not a reduction.";
                        if (Kind != EffectKind.Neutralization && Kind != EffectKind.Ward) return $"{Id}: {Kind} cannot neutralize.";
                        break;
                    case ChannelAnswer.Immune:
                        if (response.ReducePermille != 0) return $"{Id}: a share on an answer that is not a reduction.";
                        if (Kind != EffectKind.Immunity && Kind != EffectKind.Ward) return $"{Id}: {Kind} cannot make immune.";
                        break;
                    default:
                        return $"{Id}: a row of the matrix without an answer.";
                }
            }
            return null;
        }

        string ChannelProblem()
        {
            if (Responses != null && Responses.Length != 0) return $"{Id}: only a protection has a matrix of answers.";
            if (Channels.IsEmpty || !Channels.IsWithin(EffectChannels.All)) return $"{Id}: no channel, or one that does not exist.";
            switch (Kind)
            {
                case EffectKind.Blockade:
                    if (!Channels.IsWithin(EffectChannels.Control)) return $"{Id}: a blockade works through control channels.";
                    break;
                case EffectKind.Invisibility:
                case EffectKind.Reveal:
                    if (!Channels.IsWithin(EffectChannels.Observing)) return $"{Id}: {Kind} works through observation.";
                    break;
                case EffectKind.Empowerment:
                    if (!Channels.Equals(ChannelMask.Of(EffectChannel.EmpoweredBonus))) return $"{Id}: an empowerment works through its bonus channel.";
                    break;
            }
            return null;
        }
    }
}
