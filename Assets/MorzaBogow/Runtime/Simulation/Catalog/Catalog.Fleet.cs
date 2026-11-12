using System;
using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation.Catalog
{
    /// <summary>The kind of a fleet role: two working ships, six warships common to both factions, four specialisations.</summary>
    public enum RoleKind { Economic, Combat, Specialisation }

    /// <summary>
    /// One of the twelve fleet roles N01-N12 (docs/KATALOG_MECHANIK.md §3): the role, and the configuration that fills it.
    /// N01-N07 are the seven classes of the running game, value for value (ShipCatalog); N08-N12 are planned, with the
    /// prices and times of docs/PARAMETRY_ROBOCZE.md §1 and the movement profile of the class named there.
    /// </summary>
    public sealed class ShipVariantDefinition
    {
        public string Id, Name, Summary;
        public RoleKind Role;

        /// <summary>The class of the running game this role is (IsRuntimeClass), or whose movement profile it takes.</summary>
        public ShipClass HullClass;
        public bool IsRuntimeClass;

        public SlotSize[] Slots = Array.Empty<SlotSize>();
        public WeaponType[] DefaultLoadout = Array.Empty<WeaponType>();

        /// <summary>The special weapon built into the hull, never taken off it (C04); null for none.</summary>
        public string BuiltInWeaponId;

        /// <summary>The full price in units of wood and silver, the building time and the men the hull holds.</summary>
        public int CostWood, CostSilver, MaxCrew;
        public double BuildSeconds;

        public int ArtifactSlots, HeroSlots;
        public bool CanBeFlagship;

        /// <summary>A boarding that is won takes the ship (C13).</summary>
        public bool Capturable = true;

        /// <summary>Posts, crew or built-in weapon are a first guess for trials, not given by the parameters document.</summary>
        public bool Provisional;

        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("ShipVariant", Id).F("Name", Name).F("Summary", Summary).F("Role", Role.ToString())
                .F("HullClass", HullClass.ToString()).F("IsRuntimeClass", IsRuntimeClass)
                .F("Slots", Slots.Select(slot => slot.ToString())).F("DefaultLoadout", DefaultLoadout.Select(weapon => weapon.ToString()))
                .F("BuiltInWeaponId", BuiltInWeaponId).F("CostWood", CostWood).F("CostSilver", CostSilver).F("MaxCrew", MaxCrew)
                .F("BuildSeconds", BuildSeconds).F("ArtifactSlots", ArtifactSlots).F("HeroSlots", HeroSlots).F("CanBeFlagship", CanBeFlagship)
                .F("Capturable", Capturable).F("Provisional", Provisional);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>
    /// A historical hull type and the years it is attested in the Athenian fleet. Reference data for dated operations
    /// only: the fleet roles are roles, not these types, and nothing in the running game asks for a date.
    /// </summary>
    public sealed class HullProfileReference
    {
        public string Id, Name;

        /// <summary>Null: attested through the whole of the years the source covers.</summary>
        public DateWindow Date;
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("HullProfile", Id).F("Name", Name)
                .F("Date", Date == null ? null : Date.EarliestYearBc + ".." + Date.LatestYearBc);
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>
    /// One of the six families of throwing machines (C03) and the machine types of the running game that belong to it.
    /// The deck ballista and the lead ballista are two variants of the heavy bolt-thrower family.
    /// </summary>
    public sealed class WeaponFamilyDefinition
    {
        public string Id, Name;
        public WeaponType[] RuntimeTypes = Array.Empty<WeaponType>();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("WeaponFamily", Id).F("Name", Name).F("RuntimeTypes", RuntimeTypes.Select(type => type.ToString()));
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>One of the two special weapons (C04): built into its own ship, never moved to another hull.</summary>
    public sealed class SpecialWeaponDefinition
    {
        public string Id, Name, CarrierVariantId;
        public string[] EffectRefs = Array.Empty<string>();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("SpecialWeapon", Id).F("Name", Name).F("CarrierVariantId", CarrierVariantId).F("EffectRefs", EffectRefs);
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    public enum UpgradeKind { Armor, Ram }

    /// <summary>
    /// One of the three grades of hull protection (C06) or of the ram (C05). Grade I is what every hull has; II and III
    /// are researched. Value: for armour, what is added to the hull's projectile protection; for the ram, the multiplier.
    /// </summary>
    public sealed class UpgradeLevelDefinition
    {
        public string Id, Name;
        public UpgradeKind Kind;
        public int Level;
        public double Value;

        /// <summary>The research that opens the grade; null for grade I.</summary>
        public string TechnologyId;
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("UpgradeLevel", Id).F("Name", Name).F("Kind", Kind.ToString()).F("Level", Level).F("Value", Value).F("TechnologyId", TechnologyId);
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>A building stands in one port; a research serves the whole side (D11).</summary>
    public enum TechnologyScope { PortBuilding, SideResearch }

    public enum WorkshopAxis { Power, Range, Cycle }

    /// <summary>
    /// A building of a port (B01-B10) or a research of a side (docs/PARAMETRY_ROBOCZE.md §2). RuntimeProject names the
    /// project of the running game that is this technology; None where the game has none yet.
    /// </summary>
    public sealed class TechnologyDefinition
    {
        public string Id, Name, Summary;
        public TechnologyScope Scope;

        /// <summary>What a building adds to its port.</summary>
        public PortBuildingSet Grants;

        /// <summary>The price in units and the time in seconds.</summary>
        public int CostWood, CostSilver;
        public double Seconds;
        public ProjectKind RuntimeProject;
        public Parameter[] Parameters = Array.Empty<Parameter>();
        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Technology", Id).F("Name", Name).F("Summary", Summary).F("Scope", Scope.ToString()).F("Grants", (long)Grants)
                .F("CostWood", CostWood).F("CostSilver", CostSilver).F("Seconds", Seconds).F("RuntimeProject", RuntimeProject.ToString())
                .F("Parameters", Parameters);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>The IDs of the researches. Buildings keep the catalogue's own IDs, B01-B10.</summary>
    public static class TechnologyIds
    {
        public const string HeavyArms = "TECH-HEAVY-ARMS", Command = "TECH-COMMAND";
        public const string Armor2 = "TECH-ARMOR-2", Armor3 = "TECH-ARMOR-3", Ram2 = "TECH-RAM-2", Ram3 = "TECH-RAM-3";
        public const string Drive1 = "TECH-DRIVE-1", Drive2 = "TECH-DRIVE-2";
        public const string SpecA1 = "TECH-SPEC-A1", SpecA2 = "TECH-SPEC-A2", SpecB1 = "TECH-SPEC-B1", SpecB2 = "TECH-SPEC-B2";

        /// <summary>The learning of one power, e.g. TECH-LEARN-M01.</summary>
        public static string Learn(string abilityId) => "TECH-LEARN-" + abilityId;

        /// <summary>One grade of one workshop axis for one group of weapons, e.g. TECH-WS-MEDIUM-RANGE-2.</summary>
        public static string Workshop(SlotSize group, WorkshopAxis axis, int grade) =>
            "TECH-WS-" + group.ToString().ToUpperInvariant() + "-" + axis.ToString().ToUpperInvariant() + "-" + grade;
    }

    /// <summary>
    /// One of the three classes of fort (F01-F03, docs/PARAMETRY_ROBOCZE.md §3). The price is the bare structure;
    /// weapons and men are paid for apart. A fort is neither rammed nor boarded, holds one artefact and no hero.
    /// </summary>
    public sealed class FortDefinition
    {
        /// <summary>A settlement's points for the forts built from its own pool: n1 + 2·n2 + 3·n3 ≤ 15.</summary>
        public const int SettlementPool = 15;

        public string Id, Name, Summary;
        public int PoolCost, CostWood, CostSilver;
        public double BuildSeconds, Hull;
        public SlotSize[] Slots = Array.Empty<SlotSize>();
        public int ArtifactSlots = 1, HeroSlots;
        public bool CanBeRammed, CanBeBoarded;

        /// <summary>A fort raised by an expedition is never sold; one built from the settlement's pool may be.</summary>
        public bool ExpeditionFortSellable;
        public Availability Availability = new Availability();
        public SourceNote Source = new SourceNote();

        internal void Write(CanonicalWriter writer)
        {
            writer.Begin("Fort", Id).F("Name", Name).F("Summary", Summary).F("PoolCost", PoolCost).F("CostWood", CostWood).F("CostSilver", CostSilver)
                .F("BuildSeconds", BuildSeconds).F("Hull", Hull).F("Slots", Slots.Select(slot => slot.ToString())).F("ArtifactSlots", ArtifactSlots)
                .F("HeroSlots", HeroSlots).F("CanBeRammed", CanBeRammed).F("CanBeBoarded", CanBeBoarded).F("ExpeditionFortSellable", ExpeditionFortSellable);
            Availability.Write(writer, "Availability");
            Source.Write(writer, "Source");
            writer.End();
        }
    }

    /// <summary>The data of the fleet, its weapons, its research and the forts.</summary>
    static partial class CatalogData
    {
        internal const string CatalogDoc = "docs/KATALOG_MECHANIK.md", ParametersDoc = "docs/PARAMETRY_ROBOCZE.md";
        internal const string Research0504 = "docs/research/sessions/2026-11-12-0504-p02.md";
        internal const string Research0642 = "docs/research/sessions/2026-11-12-0642-p02-recenzent.md";

        internal static SourceNote Rule(string locator) => new SourceNote { Status = SourceStatus.GameRule, Layer = WorldLayer.GameRule, Locator = locator };
        internal static SourceNote MythRule(string locator) => new SourceNote { Status = SourceStatus.GameRule, Layer = WorldLayer.Mythic, Locator = locator };
        internal static SourceNote Myth(string locator) => new SourceNote { Status = SourceStatus.MythTradition, Layer = WorldLayer.Mythic, Locator = locator };

        const PortBuildingSet O = PortBuildingSet.Settlement, P = PortBuildingSet.Harbour, S = PortBuildingSet.Shipyard, W = PortBuildingSet.Workshop,
            K = PortBuildingSet.Forge, M = PortBuildingSet.Town, U = PortBuildingSet.Knowledge;

        // ---------- Fleet roles ----------

        /// <summary>A role that is a class of the running game: every number is the class's own.</summary>
        static ShipVariantDefinition Runtime(string id, string name, RoleKind role, ShipClass shipClass, string summary,
            PortBuildingSet enforced, string[] enforcedTechnologies, PortBuildingSet target, params string[] targetTechnologies)
        {
            var definition = ShipCatalog.Get(shipClass);
            return new ShipVariantDefinition
            {
                Id = id, Name = name, Summary = summary, Role = role, HullClass = shipClass, IsRuntimeClass = true,
                Slots = (SlotSize[])definition.Slots.Clone(), DefaultLoadout = (WeaponType[])definition.DefaultLoadout.Clone(),
                CostWood = definition.CostWood, CostSilver = definition.CostSilver, MaxCrew = definition.MaxCrew, BuildSeconds = definition.BuildSeconds,
                ArtifactSlots = 1, HeroSlots = definition.IsEconomic ? 0 : 1,
                Availability = new Availability { Implemented = true, Buildings = enforced, Technologies = enforcedTechnologies, TargetBuildings = target, TargetTechnologies = targetTechnologies },
                Source = Rule(CatalogDoc + " §3; " + ParametersDoc + " §1; ShipCatalog (" + shipClass + "). A role of the game, not a historical hull type."),
            };
        }

        /// <summary>A planned role: price and time from the parameters document, the rest taken from the class whose movement profile it has.</summary>
        static ShipVariantDefinition Planned(string id, string name, RoleKind role, ShipClass profile, string summary, int wood, int silver, double seconds,
            FactionId faction, SlotSize[] slots, string builtIn, PortBuildingSet target, params string[] targetTechnologies)
        {
            var definition = ShipCatalog.Get(profile);
            return new ShipVariantDefinition
            {
                Id = id, Name = name, Summary = summary, Role = role, HullClass = profile, IsRuntimeClass = false,
                Slots = (SlotSize[])slots.Clone(), DefaultLoadout = new WeaponType[slots.Length], BuiltInWeaponId = builtIn,
                CostWood = wood, CostSilver = silver, MaxCrew = definition.MaxCrew, BuildSeconds = seconds,
                ArtifactSlots = 1, HeroSlots = 1, Provisional = true,
                Availability = new Availability { Implemented = false, TargetBuildings = target, TargetTechnologies = targetTechnologies, Faction = faction },
                Source = Rule(CatalogDoc + " §3; " + ParametersDoc + " §1 (working numbers, movement profile of " + profile + "). Posts and crew are a first guess for trials."),
            };
        }

        internal static List<ShipVariantDefinition> ShipVariants()
        {
            var none = Array.Empty<string>();
            var boarderSlots = (SlotSize[])ShipCatalog.Get(ShipClass.Boarder).Slots.Clone();
            var screenSlots = (SlotSize[])ShipCatalog.Get(ShipClass.CrewDestroyer).Slots.Clone();
            var command = Planned("N08", "Okręt dowodzenia", RoleKind.Combat, ShipClass.Boarder,
                "Uniwersalna obsada, bohaterowie, dwa miejsca artefaktów i prawo flagowca.", 300, 200, 50, FactionId.Any, boarderSlots, null,
                O | S | W | K, TechnologyIds.Command);
            command.ArtifactSlots = 2;
            command.CanBeFlagship = true;
            return new List<ShipVariantDefinition>
            {
                Runtime("N01", "Łódź przybrzeżna", RoleKind.Economic, ShipClass.Cutter, "Szybki zbiór, rozpoznanie, mały ładunek.", PortBuildingSet.None, none, O | S),
                Runtime("N02", "Statek kupiecki", RoleKind.Economic, ShipClass.Merchant, "Duży ładunek, pełny transfer i budowa wyprawowa.", PortBuildingSet.None, none, M | S),
                Runtime("N03", "Okręt przełamujący", RoleKind.Combat, ShipClass.Ram, "Manewr i taran, wyjście z kontaktu.", S, none, M | S),
                Runtime("N04", "Okręt osłonowy", RoleKind.Combat, ShipClass.CrewDestroyer, "Ostrzał ludzi, ochrona jednostek gospodarczych.", S, none, O | S | W),
                Runtime("N05", "Okręt dalekiego ostrzału", RoleKind.Combat, ShipClass.ShipKiller, "Kadłuby i moduły, utrzymanie właściwego dystansu.", S, none, O | S | W),
                Runtime("N06", "Okręt oblężniczy", RoleKind.Combat, ShipClass.Bomber, "Silny przeciw umocnieniom, powolny, ograniczony minimalnym zasięgiem.",
                    S | W, new[] { TechnologyIds.HeavyArms }, M | S | W | U, TechnologyIds.HeavyArms, TechnologyIds.Armor2),
                Runtime("N07", "Okręt szturmowy", RoleKind.Combat, ShipClass.Boarder, "Silny oddział abordażowy, mała mobilność.", S, none, O | S | W | K),
                command,
                Planned("N09", "Nosiciel Płomienia", RoleKind.Specialisation, ShipClass.ShipKiller, "Wbudowane, niewyjmowalne stanowisko ognia.",
                    260, 180, 45, FactionId.A, Array.Empty<SlotSize>(), SpecialWeaponIds.Fire, M | S | W | U, TechnologyIds.SpecA1),
                Planned("N10", "Okręt Białej Mgły", RoleKind.Specialisation, ShipClass.ShipKiller, "Czasowe obezwładnienie statku lub budowli; jedno stanowisko wbudowane i jeden dodatkowy moduł.",
                    280, 200, 45, FactionId.A, new[] { SlotSize.Medium }, SpecialWeaponIds.Mist, M | S | W | U, TechnologyIds.SpecA2),
                Planned("N11", "Okręt Echa", RoleKind.Specialisation, ShipClass.CrewDestroyer, "Zakłócenie rozkazów i wabienie w strefie.",
                    240, 160, 45, FactionId.B, screenSlots, null, M | S | W | U, TechnologyIds.SpecB1),
                Planned("N12", "Okręt Żałobny", RoleKind.Specialisation, ShipClass.Boarder, "Duża odporność na abordaż i wyraźna podatność na taran.",
                    280, 220, 50, FactionId.B, boarderSlots, null, M | S | W | U, TechnologyIds.SpecB2),
            };
        }

        internal static List<HullProfileReference> HullProfiles()
        {
            // The years are the first mentions in the Athenian inventories that survive (a terminus ante quem for Athens): not
            // the year a type was brought in, and nothing about fleets elsewhere.
            // Who read what: pages 37-40 the coordinator (report 0504, which says pages 41-48 were not read); page 43 note 32
            // and Diodorus the reviewer (report 0642). The last two stand with the fours and fives only.
            const string gabrielsen = "V. Gabrielsen, „The Piraeus and the Athenian navy: recent archaeological and historical advances”, Proceedings of the Danish Institute at Athens 7 (2014), s. 37–48: s. 39 Fig. 2, s. 40, s. 43 przyp. 32."
                + " s. 37–40: odczyt koordynatora, " + Research0504 + "; s. 43 przyp. 32 oraz Diod. 14.41.3/14.42.2: odczyt recenzenta, " + Research0642;
            const string firstMention = " To pierwsze poświadczenie w zachowanych inwentarzach ateńskich (terminus ante quem), nie data wprowadzenia typu ani zasięg poza Atenami."
                + " Diod. 14.41.3 i 14.42.2 podają sporną, późną tradycję syrakuzańską ok. 399 r. (Loeb 1954, LacusCurtius; odczyt recenzenta, " + Research0642 + ")."
                + " Koniec okna to koniec epoki gry, nie świadectwo.";
            SourceNote Inventories(string what) => new SourceNote { Status = SourceStatus.ConfirmedInScope, Layer = WorldLayer.Material, Locator = gabrielsen + ". " + what };
            // The threes lean on Fig. 2 alone, in the coordinator's reading (report 0504). Page 43 note 32 and Diodorus are
            // about the first fours and fives and say nothing of the threes, so they are not named here. The reviewer's report is named
            // for one thing only: its second reading of the rows of Fig. 2.
            const string trieres = "V. Gabrielsen, „The Piraeus and the Athenian navy: recent archaeological and historical advances”, Proceedings of the Danish Institute at Athens 7 (2014), s. 37–48: s. 39 Fig. 2."
                + " s. 37–40: odczyt koordynatora, " + Research0504 + "."
                + " Triery w każdym roku Fig. 2 (378/7–323/2); 357/6: 283 triery; 325/4: 360 trier."
                + " Część liczb Fig. 2 to rekonstrukcje (w nawiasach kwadratowych); 326/5 i 323/2 są niepełne (0504)."
                + " Wiersze Fig. 2 sprawdził też recenzent: " + Research0642 + " (przegląd P02-D).";
            return new List<HullProfileReference>
            {
                new HullProfileReference { Id = "HULL-TRIERES", Name = "Triera", Date = null,
                    Source = new SourceNote { Status = SourceStatus.ConfirmedInScope, Layer = WorldLayer.Material, Locator = trieres } },
                new HullProfileReference { Id = "HULL-TETRERES", Name = "Tetrera", Date = new DateWindow { EarliestYearBc = 330, LatestYearBc = GameEpoch.LatestYearBc },
                    Source = Inventories("Tetrery w inwentarzach od 330/29." + firstMention) },
                new HullProfileReference { Id = "HULL-PENTERES", Name = "Pentera", Date = new DateWindow { EarliestYearBc = 325, LatestYearBc = GameEpoch.LatestYearBc },
                    Source = Inventories("Pentery w inwentarzach od 325/4." + firstMention) },
            };
        }

        // ---------- Weapons ----------

        internal static List<WeaponFamilyDefinition> WeaponFamilies()
        {
            // Machines aboard ships in missions of the material world: a rule of the game. Arrian II.21 tells of machines
            // on ships at the siege of Tyre (332), a siege in another theatre, not the fitting of the Attic fleet; the
            // dates of the single families were not read.
            var source = new SourceNote
            {
                Status = SourceStatus.Undetermined, Layer = WorldLayer.GameRule,
                Locator = CatalogDoc + " §4 C03. Machina pokładowa: REGUŁA_GRY; daty rodzin NIEUSTALONE (" + Research0504 + ": Arrian, Anabaza II.21.1–2 i II.21.8 tylko jako kontekst oblężenia Tyru).",
            };
            WeaponFamilyDefinition Family(string id, string name, params WeaponType[] types) =>
                new WeaponFamilyDefinition { Id = id, Name = name, RuntimeTypes = types, Source = new SourceNote { Status = source.Status, Layer = source.Layer, Locator = source.Locator } };
            return new List<WeaponFamilyDefinition>
            {
                Family("C03-1", "Lekki miotacz bełtów", WeaponType.Scorpion),
                Family("C03-2", "Łuk mechaniczny", WeaponType.Gastraphetes),
                Family("C03-3", "Miotacz kamieni", WeaponType.StoneCatapult),
                Family("C03-4", "Ciężki miotacz bełtów", WeaponType.DeckBallista, WeaponType.LeadBallista),
                Family("C03-5", "Ciężki miotacz jednoramienny", WeaponType.Onager),
                Family("C03-6", "Ciężki miotacz pocisków", WeaponType.HeavyCatapult),
            };
        }

        internal static List<SpecialWeaponDefinition> SpecialWeapons() => new List<SpecialWeaponDefinition>
        {
            new SpecialWeaponDefinition { Id = SpecialWeaponIds.Fire, Name = "Stanowisko ognia", CarrierVariantId = "N09", EffectRefs = new[] { SpecialWeaponIds.Fire },
                Source = MythRule(CatalogDoc + " §3 N09, §4 C04: fikcja mitologiczna, niewyjmowalne.") },
            new SpecialWeaponDefinition { Id = SpecialWeaponIds.Mist, Name = "Obezwładniająca mgła", CarrierVariantId = "N10", EffectRefs = new[] { SpecialWeaponIds.Mist },
                Source = MythRule(CatalogDoc + " §3 N10, §4 C04: fikcja mitologiczna, niewyjmowalne.") },
        };

        internal static List<UpgradeLevelDefinition> UpgradeLevels()
        {
            var source = ParametersDoc + " §2 (kuźnia)";
            return new List<UpgradeLevelDefinition>
            {
                new UpgradeLevelDefinition { Id = "C06-1", Name = "Osłona I", Kind = UpgradeKind.Armor, Level = 1, Value = 0, Source = Rule(source) },
                new UpgradeLevelDefinition { Id = "C06-2", Name = "Osłona II", Kind = UpgradeKind.Armor, Level = 2, Value = 0.10, TechnologyId = TechnologyIds.Armor2, Source = Rule(source) },
                new UpgradeLevelDefinition { Id = "C06-3", Name = "Osłona III", Kind = UpgradeKind.Armor, Level = 3, Value = 0.20, TechnologyId = TechnologyIds.Armor3, Source = Rule(source) },
                new UpgradeLevelDefinition { Id = "C05-1", Name = "Taran I", Kind = UpgradeKind.Ram, Level = 1, Value = 1.0, Source = Rule(source) },
                new UpgradeLevelDefinition { Id = "C05-2", Name = "Taran II", Kind = UpgradeKind.Ram, Level = 2, Value = 1.15, TechnologyId = TechnologyIds.Ram2, Source = Rule(source) },
                new UpgradeLevelDefinition { Id = "C05-3", Name = "Taran III", Kind = UpgradeKind.Ram, Level = 3, Value = 1.30, TechnologyId = TechnologyIds.Ram3, Source = Rule(source) },
            };
        }

        // ---------- Buildings and research ----------

        static TechnologyDefinition Technology(string id, string name, TechnologyScope scope, int wood, int silver, double seconds, string summary,
            Availability availability, PortBuildingSet grants = PortBuildingSet.None, ProjectKind project = ProjectKind.None, params Parameter[] parameters) =>
            new TechnologyDefinition
            {
                Id = id, Name = name, Summary = summary, Scope = scope, Grants = grants, CostWood = wood, CostSilver = silver, Seconds = seconds,
                RuntimeProject = project, Parameters = parameters, Availability = availability, Source = Rule(CatalogDoc + " §2; " + ParametersDoc + " §2"),
            };

        static Availability Now(PortBuildingSet enforced, PortBuildingSet target, bool homeOnly = false, string[] enforcedTechnologies = null, params string[] targetTechnologies) =>
            new Availability { Implemented = true, Buildings = enforced, Technologies = enforcedTechnologies ?? Array.Empty<string>(), HomePortOnly = homeOnly, TargetBuildings = target, TargetTechnologies = targetTechnologies };

        static Availability Later(PortBuildingSet target, FactionId faction, params string[] targetTechnologies) =>
            new Availability { Implemented = false, TargetBuildings = target, TargetTechnologies = targetTechnologies, Faction = faction };

        internal static List<TechnologyDefinition> Technologies()
        {
            const TechnologyScope building = TechnologyScope.PortBuilding, research = TechnologyScope.SideResearch;
            var list = new List<TechnologyDefinition>
            {
                // The founding of a port and its shipyard are paid as the running game pays them (Rules.Outpost*, Rules.Shipyard*).
                Technology("B01", "Osada portowa", building, (int)(Rules.OutpostWood / Rules.Centi), (int)(Rules.OutpostSilver / Rules.Centi), Rules.OutpostBuildSeconds,
                    "Zakłada i finansuje obiekty, szkoli wojowników, zarządza lokalnym zapasem.", Now(PortBuildingSet.None, PortBuildingSet.None), O),
                Technology("B02", "Przystań", building, 0, 0, 0, "Transfer ludzi, towaru, machin i przedmiotów. Dziś każdy port przeładowuje D/S; osobnego obiektu nie ma.",
                    Later(O, FactionId.Any), P),
                Technology("B03", "Stocznia", building, (int)(Rules.ShipyardWood / Rules.Centi), (int)(Rules.ShipyardSilver / Rules.Centi), Rules.ShipyardBuildSeconds,
                    "Produkcja okrętów; jedna linia wojenna na stocznię.", Now(O, O), S),
                Technology("B04", "Warsztat", building, 150, 100, 40, "Wytwarzanie modułów i dziewięć osi usprawnień.", Now(S, S), W, ProjectKind.Workshop),
                Technology("B05", "Kuźnia", building, 160, 140, 45, "Trzy stopnie osłony i trzy stopnie taranu.", Later(O | S, FactionId.Any), K),
                Technology("B06", "Ośrodek miejski", building, 300, 400, 60, "Awans osady po ukończeniu przystani, stoczni, kuźni i warsztatu.",
                    Now(S | W, P | S | W | K, homeOnly: true), M, ProjectKind.Town),
                Technology("B07", "Dok", building, 200, 300, 45, "Rozbudowa stoczni w mieście; naprawa kadłuba i modułów.", Now(M | S, M | S), PortBuildingSet.Dock, ProjectKind.Dock),
                Technology("B08", "Ośrodek wiedzy", building, 200, 500, 60, "Dostęp do ciężkiego uzbrojenia, zaawansowanych osłon, taranów, napędu i specjalizacji.",
                    Now(M, M), U, ProjectKind.SageSchool),
                Technology("B09", "Sanktuarium Morza", building, 180, 300, 60, "Nauka sześciu mocy akwenu i prawo ich użycia w zasięgu.", Later(M, FactionId.Any), PortBuildingSet.SeaSanctuary),
                Technology("B10", "Sanktuarium Wojny", building, 180, 300, 60, "Nauka sześciu mocy walki i ochrony.", Later(M, FactionId.Any), PortBuildingSet.WarSanctuary),

                Technology(TechnologyIds.HeavyArms, "Ciężkie uzbrojenie", research, 150, 300, 90, "Ciężkie machiny i okręt oblężniczy dla całej strony.",
                    Now(U | W, U | W), PortBuildingSet.None, ProjectKind.HeavyArmsResearch),
                Technology(TechnologyIds.Command, "Organizacja dowodzenia", research, 80, 120, 45, "Otwiera okręt dowodzenia.", Later(W | K, FactionId.Any)),
                Technology(TechnologyIds.SpecA1, "Specjalizacja A1", research, 100, 180, 60, "Otwiera Nosiciela Płomienia.", Later(U | W, FactionId.A)),
                Technology(TechnologyIds.SpecA2, "Specjalizacja A2", research, 100, 180, 60, "Otwiera Okręt Białej Mgły.", Later(U | W, FactionId.A)),
                Technology(TechnologyIds.SpecB1, "Specjalizacja B1", research, 100, 180, 60, "Otwiera Okręt Echa.", Later(U | W, FactionId.B)),
                Technology(TechnologyIds.SpecB2, "Specjalizacja B2", research, 100, 180, 60, "Otwiera Okręt Żałobny.", Later(U | W, FactionId.B)),

                Technology(TechnologyIds.Armor2, "Osłona II", research, 60, 100, 45, "Dziesięć punktów procentowych ochrony pociskowej ponad podstawę.", Later(K, FactionId.Any)),
                Technology(TechnologyIds.Armor3, "Osłona III", research, 90, 160, 60, "Dwadzieścia punktów procentowych ochrony pociskowej ponad podstawę.", Later(K | U, FactionId.Any, TechnologyIds.Armor2)),
                Technology(TechnologyIds.Ram2, "Taran II", research, 60, 100, 45, "Mnożnik tarana ×1,15.", Later(K, FactionId.Any)),
                Technology(TechnologyIds.Ram3, "Taran III", research, 90, 160, 60, "Mnożnik tarana ×1,30.", Later(K | U, FactionId.Any, TechnologyIds.Ram2)),
                Technology(TechnologyIds.Drive1, "Napęd I", research, 60, 100, 45, "Prędkość bazowa +5%.", Later(U, FactionId.Any), PortBuildingSet.None, ProjectKind.None, new Parameter("SpeedFactor", 1.05)),
                Technology(TechnologyIds.Drive2, "Napęd II", research, 90, 150, 60, "Prędkość bazowa +10% łącznie.", Later(U, FactionId.Any, TechnologyIds.Drive1), PortBuildingSet.None, ProjectKind.None, new Parameter("SpeedFactor", 1.10)),
            };

            // Nine workshop axes, two grades each: power, range and cycle for the light posts, the medium and the heavy
            // machines. The second grade's value is the axis's whole value, not added to the first.
            foreach (var group in new[] { SlotSize.Light, SlotSize.Medium, SlotSize.Heavy })
            {
                foreach (var axis in new[] { WorkshopAxis.Power, WorkshopAxis.Range, WorkshopAxis.Cycle })
                {
                    var first = axis == WorkshopAxis.Power ? 1.10 : axis == WorkshopAxis.Range ? 1.05 : 0.95;
                    var second = axis == WorkshopAxis.Power ? 1.20 : axis == WorkshopAxis.Range ? 1.10 : 0.90;
                    var groupName = group == SlotSize.Light ? "obsada lekka" : group == SlotSize.Medium ? "machiny średnie" : "machiny ciężkie";
                    var axisName = axis == WorkshopAxis.Power ? "siła" : axis == WorkshopAxis.Range ? "zasięg" : "cykl";
                    list.Add(Technology(TechnologyIds.Workshop(group, axis, 1), $"Warsztat: {axisName}, {groupName} I", research, 50, 80, 40, "Pierwszy stopień osi warsztatu.",
                        Later(W, FactionId.Any), PortBuildingSet.None, ProjectKind.None, new Parameter(axis + "Factor", first)));
                    list.Add(Technology(TechnologyIds.Workshop(group, axis, 2), $"Warsztat: {axisName}, {groupName} II", research, 80, 120, 60, "Drugi stopień osi warsztatu: wartość końcowa osi.",
                        Later(W | U, FactionId.Any, TechnologyIds.Workshop(group, axis, 1)), PortBuildingSet.None, ProjectKind.None, new Parameter(axis + "Factor", second)));
                }
            }

            // The learning of each of the twelve powers, at its sanctuary.
            foreach (var ability in Abilities())
            {
                var sanctuary = ability.Sanctuary == SanctuaryKind.Sea ? PortBuildingSet.SeaSanctuary : PortBuildingSet.WarSanctuary;
                list.Add(Technology(TechnologyIds.Learn(ability.Id), "Nauka mocy: " + ability.Name, research, 50, 100, 60, "Nauka jednej mocy w jej sanktuarium.", Later(sanctuary, FactionId.Any)));
            }
            return list;
        }

        // ---------- Forts ----------

        internal static List<FortDefinition> Forts()
        {
            FortDefinition Fort(string id, string name, string summary, int pool, int wood, int silver, double seconds, double hull, int light, int medium, int heavy,
                PortBuildingSet target, params string[] technologies) => new FortDefinition
            {
                Id = id, Name = name, Summary = summary, PoolCost = pool, CostWood = wood, CostSilver = silver, BuildSeconds = seconds, Hull = hull,
                Slots = Enumerable.Repeat(SlotSize.Light, light).Concat(Enumerable.Repeat(SlotSize.Medium, medium)).Concat(Enumerable.Repeat(SlotSize.Heavy, heavy)).ToArray(),
                Availability = new Availability { Implemented = false, TargetBuildings = target, TargetTechnologies = technologies, OwnerSlots = Availability.MainSlots },
                Source = Rule(CatalogDoc + " §2 (forty); " + ParametersDoc + " §3"),
            };
            return new List<FortDefinition>
            {
                Fort("F01", "Mały posterunek", "Obserwacja i bliska osłona przystani.", 1, 180, 100, 45, 1200, 2, 2, 0, O),
                Fort("F02", "Bateria przybrzeżna", "Kontrola przejścia i osłona przeładunku.", 2, 300, 180, 60, 2200, 2, 3, 1, O | W | K),
                Fort("F03", "Twierdza portowa", "Silny, wolno rozwijany punkt obrony.", 3, 460, 280, 90, 3600, 2, 3, 3, O | W | K | U, TechnologyIds.HeavyArms),
            };
        }
    }

    public static class SpecialWeaponIds
    {
        public const string Fire = "C04-FIRE", Mist = "C04-MIST";
    }
}
