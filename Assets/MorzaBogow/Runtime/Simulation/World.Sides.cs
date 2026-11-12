using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The three fixed owner slots (docs/PLAN_WDROZENIA.md, P02): the human, the main opponent, and the independent
    /// actors. Only the first two take part in the match's result; the third never wins or loses it.
    /// </summary>
    public static class Owners
    {
        public const int Human = 0, MainAi = 1, Independent = 2, SlotCount = 3;

        public static bool IsValid(int owner) => owner >= 0 && owner < SlotCount;

        /// <summary>One of the two sides the match's result is decided between.</summary>
        public static bool IsMainParticipant(int owner) => owner == Human || owner == MainAi;

        /// <summary>The other main side. The independent slot has no single opponent: asking for one is an error.</summary>
        public static int MainOpponentOf(int owner)
        {
            if (owner == Human) return MainAi;
            if (owner == MainAi) return Human;
            throw new ArgumentOutOfRangeException(nameof(owner), owner, "Only the two main sides have a main opponent.");
        }
    }

    /// <summary>
    /// Which pairs of owner slots are at war: one bit a pair ({0,1}, {0,2}, {1,2}). Symmetric; a slot is never hostile
    /// to itself (the independent actors among themselves neither), and a slot outside the three is hostile to none.
    /// </summary>
    public readonly struct SideRelations : IEquatable<SideRelations>
    {
        const int PairBits = 0b111;

        /// <summary>The hostile pairs, bit 0 for {0,1}, bit 1 for {0,2}, bit 2 for {1,2}.</summary>
        public readonly int HostilePairs;

        public SideRelations(int hostilePairs) => HostilePairs = hostilePairs & PairBits;

        /// <summary>Every pair at war: a match of two sides, and the default wherever a scenario says nothing else.</summary>
        public static SideRelations AllHostile => new SideRelations(PairBits);

        /// <summary>Nobody at war with anybody.</summary>
        public static SideRelations NoneHostile => new SideRelations(0);

        /// <summary>The pair's bit, in either order; -1 for a slot with itself or outside the three.</summary>
        static int PairBit(int a, int b)
        {
            if (!Owners.IsValid(a) || !Owners.IsValid(b) || a == b) return -1;
            // {0,1} -> 0, {0,2} -> 1, {1,2} -> 2.
            return a + b - 1;
        }

        public bool IsHostile(int a, int b)
        {
            var bit = PairBit(a, b);
            return bit >= 0 && (HostilePairs & (1 << bit)) != 0;
        }

        /// <summary>The same relations with one pair set at war or at peace; a slot with itself or outside the three changes nothing.</summary>
        public SideRelations With(int a, int b, bool hostile)
        {
            var bit = PairBit(a, b);
            if (bit < 0) return this;
            return new SideRelations(hostile ? HostilePairs | (1 << bit) : HostilePairs & ~(1 << bit));
        }

        public bool Equals(SideRelations other) => HostilePairs == other.HostilePairs;
        public override bool Equals(object other) => other is SideRelations relations && Equals(relations);
        public override int GetHashCode() => HostilePairs;
    }

    /// <summary>
    /// The one place that says who is whose enemy, who commands what, who may take a ship and who the match is decided
    /// between (P02). Rules ask here instead of comparing owners, so a third side does not become everybody's enemy,
    /// or nobody's, by accident.
    /// </summary>
    public sealed partial class World
    {
        /// <summary>
        /// The relations between the owner slots in this match: set when the match is made and constant through it; no
        /// order changes them. Saved since format 20.
        /// </summary>
        public SideRelations Relations = SideRelations.AllHostile;

        /// <summary>The two owners are at war: each may shoot at, ram and attack the other's ships and ports.</summary>
        public bool IsHostile(int a, int b) => Relations.IsHostile(a, b);

        /// <summary>The side giving an order commands what this owner holds: only its own, and only a real slot.</summary>
        public bool CanControl(int issuer, int owner) => Owners.IsValid(issuer) && issuer == owner;

        /// <summary>A side may take this ship by boarding: an enemy's, and only a main side takes prizes.</summary>
        public bool CanCapture(int capturer, Ship target) =>
            target != null && Owners.IsMainParticipant(capturer) && IsHostile(capturer, target.Owner);

        /// <summary>One of the two sides the match's result is decided between.</summary>
        public bool IsMainParticipant(int owner) => Owners.IsMainParticipant(owner);
    }
}
