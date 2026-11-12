using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MorzaBogow.Simulation.Catalog
{
    /// <summary>
    /// How far a statement about the world behind a definition is established (docs/HISTORIA_I_ZRODLA.md). A status is
    /// about the motif or the material fact a definition leans on; costs, ranges, times and effects are rules of the game
    /// whatever the status. Code() gives the protocol's own word.
    /// </summary>
    public enum SourceStatus { ConfirmedInScope, Reconstruction, MythTradition, GameRule, Undetermined, BlockedResearch }

    /// <summary>Which layer of the game's world a definition belongs to: what there was, what the myths tell, or a rule of play.</summary>
    public enum WorldLayer { Material, Mythic, GameRule }

    /// <summary>The status of what a definition leans on, the layer it belongs to and where the source was read.</summary>
    public sealed class SourceNote
    {
        /// <summary>Undetermined until a definition says otherwise: a note that was never filled in claims nothing.</summary>
        public SourceStatus Status = SourceStatus.Undetermined;

        /// <summary>
        /// A rule of the game until a definition says otherwise: a note that was never filled in does not pass for a
        /// statement about the material world.
        /// </summary>
        public WorldLayer Layer = WorldLayer.GameRule;

        /// <summary>The source and the place in it, or the project document that sets the rule.</summary>
        public string Locator;

        public static string Code(SourceStatus status)
        {
            switch (status)
            {
                case SourceStatus.ConfirmedInScope: return "POTWIERDZONE_W_ZAKRESIE";
                case SourceStatus.Reconstruction: return "REKONSTRUKCJA";
                case SourceStatus.MythTradition: return "TRADYCJA_MITOLOGICZNA";
                case SourceStatus.GameRule: return "REGUŁA_GRY";
                case SourceStatus.Undetermined: return "NIEUSTALONE";
                default: return "BLOCKED_RESEARCH";
            }
        }

        internal void Write(CanonicalWriter writer, string name) =>
            writer.F(name + ".Status", Status.ToString()).F(name + ".Layer", Layer.ToString()).F(name + ".Locator", Locator);
    }

    /// <summary>
    /// The two playable port factions of the game's fiction: A is the Assembly of the Ports of Attica (Zgromadzenie
    /// Portów Attyki), B the League of the Aegean Isles (Przymierze Wysp Egejskich). Any means both.
    /// </summary>
    public enum FactionId { Any, A, B }

    /// <summary>
    /// What stands in a port, by the letters of docs/PARAMETRY_ROBOCZE.md: O settlement, P harbour, S shipyard, W
    /// workshop, K forge, M town, U knowledge; and the dock and the two sanctuaries, which have no letter there.
    /// </summary>
    [Flags]
    public enum PortBuildingSet
    {
        None = 0, Settlement = 1, Harbour = 2, Shipyard = 4, Workshop = 8, Forge = 16, Town = 32, Knowledge = 64,
        Dock = 128, SeaSanctuary = 256, WarSanctuary = 512,
    }

    /// <summary>The sets of rules a match is played under. A skirmish has no date; a campaign operation may have one.</summary>
    public static class Rulesets
    {
        public const string Skirmish = "skirmish", CampaignMaterial = "campaign-material", CampaignMythic = "campaign-mythic";
        public static readonly string[] All = { Skirmish, CampaignMaterial, CampaignMythic };
    }

    /// <summary>The game's epoch, the fourth century BC, 400 to 301 with both ends (docs/MAPY_I_SCENARIUSZE.md).</summary>
    public static class GameEpoch
    {
        public const int EarliestYearBc = 400, LatestYearBc = 301;
    }

    /// <summary>
    /// Years BC between which a thing is to be had: from Earliest (the larger number) down to Latest. A year is inside
    /// when Latest ≤ year ≤ Earliest.
    /// </summary>
    public sealed class DateWindow
    {
        public int EarliestYearBc, LatestYearBc;

        public bool Contains(int yearBc) => LatestYearBc <= yearBc && yearBc <= EarliestYearBc;
    }

    /// <summary>
    /// When a definition may be used. Buildings and Technologies are what the running game asks for today; Target* is
    /// what docs/PARAMETRY_ROBOCZE.md plans for it (P04, P06) and is data only until those stages bring it in.
    /// </summary>
    public sealed class Availability
    {
        /// <summary>The running game has the thing. A definition not yet implemented is never available.</summary>
        public bool Implemented;

        /// <summary>What the port of production must have, and what its side must have researched (technology IDs).</summary>
        public PortBuildingSet Buildings;
        public string[] Technologies = Array.Empty<string>();

        /// <summary>Only the side's home port, never a bridgehead.</summary>
        public bool HomePortOnly;

        /// <summary>The requirements planned in the parameters document, not asked for yet.</summary>
        public PortBuildingSet TargetBuildings;
        public string[] TargetTechnologies = Array.Empty<string>();

        public FactionId Faction;

        /// <summary>The owner slots that may have it, one bit a slot (bit 0 the human, 1 the main opponent, 2 the independent).</summary>
        public int OwnerSlots = MainSlots;

        /// <summary>Null: no date asked. Otherwise the years of a dated operation in which it is to be had.</summary>
        public DateWindow Date;

        /// <summary>The rulesets it belongs to; empty means every ruleset.</summary>
        public string[] Rulesets = Array.Empty<string>();

        public const int MainSlots = 0b011, IndependentSlot = 0b100, AllSlots = 0b111;

        internal void Write(CanonicalWriter writer, string name)
        {
            writer.F(name + ".Implemented", Implemented).F(name + ".Buildings", (long)Buildings).F(name + ".Technologies", Technologies)
                .F(name + ".HomePortOnly", HomePortOnly).F(name + ".TargetBuildings", (long)TargetBuildings).F(name + ".TargetTechnologies", TargetTechnologies)
                .F(name + ".Faction", Faction.ToString()).F(name + ".OwnerSlots", OwnerSlots)
                .F(name + ".Date", Date == null ? null : Date.EarliestYearBc.ToString(CultureInfo.InvariantCulture) + ".." + Date.LatestYearBc.ToString(CultureInfo.InvariantCulture))
                .F(name + ".Rulesets", Rulesets);
        }
    }

    /// <summary>Who asks, where and when: the port, what its side has researched, the faction, the slot, the ruleset and its year.</summary>
    public sealed class AvailabilityContext
    {
        public PortBuildingSet Buildings;
        public string[] Technologies = Array.Empty<string>();
        public bool IsOutpost;
        public FactionId Faction;
        public int OwnerSlot;
        public string Ruleset = Rulesets.Skirmish;

        /// <summary>The year of the operation, BC; null where the ruleset has no date (a skirmish).</summary>
        public int? YearBc;

        /// <summary>Ask by the planned requirements (Target*) instead of those of the running game.</summary>
        public bool ByTarget;
    }

    /// <summary>Why a definition is not available; None when it is.</summary>
    [Flags]
    public enum Unavailable
    {
        None = 0, NotImplemented = 1, MissingBuilding = 2, MissingTechnology = 4, NotHomePort = 8, WrongFaction = 16,
        WrongSlot = 32, OutsideDate = 64, WrongRuleset = 128,
    }

    /// <summary>The one rule of availability: a pure function of the definition's conditions and the asker's context.</summary>
    public static class AvailabilityRules
    {
        public static Unavailable Evaluate(Availability availability, AvailabilityContext context)
        {
            var result = Unavailable.None;
            if (!availability.Implemented) result |= Unavailable.NotImplemented;
            var buildings = context.ByTarget ? availability.TargetBuildings : availability.Buildings;
            if ((context.Buildings & buildings) != buildings) result |= Unavailable.MissingBuilding;
            foreach (var technology in context.ByTarget ? availability.TargetTechnologies : availability.Technologies)
            {
                if (Array.IndexOf(context.Technologies, technology) < 0) { result |= Unavailable.MissingTechnology; break; }
            }
            if (availability.HomePortOnly && context.IsOutpost) result |= Unavailable.NotHomePort;
            if (availability.Faction != FactionId.Any && availability.Faction != context.Faction) result |= Unavailable.WrongFaction;
            if (!Owners.IsValid(context.OwnerSlot) || (availability.OwnerSlots & (1 << context.OwnerSlot)) == 0) result |= Unavailable.WrongSlot;
            // A ruleset without a date asks for none; a dated one keeps to the window.
            if (availability.Date != null && context.YearBc.HasValue && !availability.Date.Contains(context.YearBc.Value)) result |= Unavailable.OutsideDate;
            if (availability.Rulesets.Length > 0 && Array.IndexOf(availability.Rulesets, context.Ruleset) < 0) result |= Unavailable.WrongRuleset;
            return result;
        }

        public static bool IsAvailable(Availability availability, AvailabilityContext context) => Evaluate(availability, context) == Unavailable.None;

        /// <summary>
        /// What a port of the running game has, in the catalogue's terms. Every port is a settlement with a harbour (they
        /// are not separate objects today); the forge and the sanctuaries do not exist yet.
        /// </summary>
        public static PortBuildingSet BuildingsOf(Settlement port)
        {
            var buildings = PortBuildingSet.Settlement | PortBuildingSet.Harbour;
            if (port.HasShipyard) buildings |= PortBuildingSet.Shipyard;
            if (port.HasWorkshop) buildings |= PortBuildingSet.Workshop;
            if (port.IsTown) buildings |= PortBuildingSet.Town;
            if (port.HasSageSchool) buildings |= PortBuildingSet.Knowledge;
            if (port.HasDock) buildings |= PortBuildingSet.Dock;
            return buildings;
        }

        /// <summary>The context of an order given at a port of the running game, in a skirmish.</summary>
        public static AvailabilityContext ContextOf(World world, Settlement port, FactionId faction = FactionId.Any) => new AvailabilityContext
        {
            Buildings = BuildingsOf(port),
            Technologies = world.HasHeavyArms(port.Owner) ? new[] { TechnologyIds.HeavyArms } : Array.Empty<string>(),
            IsOutpost = port.IsOutpost,
            Faction = faction,
            OwnerSlot = port.Owner,
            Ruleset = Rulesets.Skirmish,
        };
    }

    /// <summary>
    /// The IDs by which the catalogue names effects. An owner with one effect names it by its own ID ("M11", "T05"); an
    /// owner with several names each by its ID, a dot and the kind in capital ASCII letters and digits ("M01.ZONE",
    /// "H06.RENEW"). The effect contract keeps the same IDs, and they go into a save, hence the limit of length.
    /// </summary>
    public static class EffectIds
    {
        /// <summary>The longest ID an effect record keeps.</summary>
        public const int MaxLength = 64;

        public static string Of(string ownerId, string kind) => ownerId + "." + kind;

        /// <summary>The ID is the owner's own, or the owner's with a kind of capital ASCII letters and digits after a dot, and not too long.</summary>
        public static bool IsValidFor(string ownerId, string effectId)
        {
            if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(effectId) || effectId.Length > MaxLength) return false;
            if (effectId == ownerId) return true;
            if (effectId.Length <= ownerId.Length + 1 || !effectId.StartsWith(ownerId + ".", StringComparison.Ordinal)) return false;
            for (var i = ownerId.Length + 1; i < effectId.Length; i++)
            {
                var character = effectId[i];
                if (!((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9'))) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The channels of the effect contract, by name. Text on purpose: the catalogue names a channel without depending on
    /// the contract's types, and its check knows which names there are.
    /// </summary>
    public static class EffectChannelNames
    {
        public const string OrdinaryWeapon = "OrdinaryWeapon", Ram = "Ram", Boarding = "Boarding", EmpoweredBonus = "EmpoweredBonus", Thunder = "Thunder",
            FireZone = "FireZone", SummonedCreature = "SummonedCreature", WaterspoutDamage = "WaterspoutDamage", WhirlpoolDamage = "WhirlpoolDamage",
            ReefImpact = "ReefImpact", GorgonGaze = "GorgonGaze", SirenDamage = "SirenDamage", WhirlpoolPull = "WhirlpoolPull", ReefBarrier = "ReefBarrier",
            WaterspoutCourse = "WaterspoutCourse", SirenPull = "SirenPull", Wind = "Wind", Observation = "Observation", CreatureAttack = "CreatureAttack";

        public static readonly string[] All =
        {
            OrdinaryWeapon, Ram, Boarding, EmpoweredBonus, Thunder, FireZone, SummonedCreature, WaterspoutDamage, WhirlpoolDamage, ReefImpact, GorgonGaze,
            SirenDamage, WhirlpoolPull, ReefBarrier, WaterspoutCourse, SirenPull, Wind, Observation, CreatureAttack,
        };
    }

    /// <summary>A named number of a definition (a multiplier, a share, seconds): a rule of the game, kept as data.</summary>
    public sealed class Parameter
    {
        public string Name;
        public double Value;

        public Parameter() { }
        public Parameter(string name, double value) { Name = name; Value = value; }
    }

    /// <summary>
    /// The catalogue written out as text in one fixed form, for its hash: every definition a line of its type, ID and
    /// fields in the order its Write gives them, numbers in the invariant culture. No time, no machine, no order of a
    /// dictionary enters it.
    /// </summary>
    public sealed class CanonicalWriter
    {
        readonly StringBuilder text = new StringBuilder();

        public CanonicalWriter Begin(string type, string id)
        {
            text.Append(type).Append('#');
            Escape(id);
            text.Append('{');
            return this;
        }

        public CanonicalWriter End()
        {
            text.Append("}\n");
            return this;
        }

        public CanonicalWriter F(string name, string value)
        {
            text.Append(name).Append('=');
            Escape(value);
            text.Append(';');
            return this;
        }

        public CanonicalWriter F(string name, long value) => Raw(name, value.ToString(CultureInfo.InvariantCulture));
        public CanonicalWriter F(string name, bool value) => Raw(name, value ? "1" : "0");
        public CanonicalWriter F(string name, double value) => Raw(name, Number(value));

        /// <summary>
        /// A fraction as text that is the same on every runtime: fifteen significant digits, and only where those give
        /// the very same number back. Runtimes differ in how they print the sixteenth and seventeenth digit, so a value
        /// that needs them (a third, a sum that did not come out even) is refused aloud instead of being written one
        /// way here and another way there. The catalogue's numbers are written by hand and never need more.
        /// </summary>
        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidOperationException("Not a number the catalogue can write.");
            // Zero has one text: runtimes differ in whether they print the sign of a negative zero.
            if (value == 0) return "0";
            var written = value.ToString("G15", CultureInfo.InvariantCulture);
            if (double.Parse(written, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
                throw new InvalidOperationException("The number " + written + "… needs more than fifteen digits and would be written differently on different runtimes; give the catalogue a value that fifteen digits hold.");
            return written;
        }

        public CanonicalWriter F(string name, IEnumerable<string> values)
        {
            text.Append(name).Append("=[");
            if (values == null) text.Append('~');
            else foreach (var value in values) { Escape(value); text.Append(','); }
            text.Append("];");
            return this;
        }

        public CanonicalWriter F(string name, IEnumerable<Parameter> values)
        {
            text.Append(name).Append("=[");
            if (values == null) text.Append('~');
            else foreach (var value in values) { Escape(value.Name); text.Append(':').Append(Number(value.Value)).Append(','); }
            text.Append("];");
            return this;
        }

        CanonicalWriter Raw(string name, string value)
        {
            text.Append(name).Append('=').Append(value).Append(';');
            return this;
        }

        /// <summary>Null is written apart from the empty text; the marks of the form are escaped.</summary>
        void Escape(string value)
        {
            if (value == null) { text.Append('~'); return; }
            text.Append('"');
            foreach (var character in value)
            {
                if (character == '"' || character == '\\') text.Append('\\');
                if (character == '\n') text.Append("\\n"); else text.Append(character);
            }
            text.Append('"');
        }

        public override string ToString() => text.ToString();

        /// <summary>FNV-1a, 64 bits, over the UTF-8 bytes of the text.</summary>
        public static ulong Hash(string canonical)
        {
            var hash = 14695981039346656037UL;
            foreach (var value in Encoding.UTF8.GetBytes(canonical))
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
