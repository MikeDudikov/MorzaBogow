using System;

namespace MorzaBogow.Simulation
{
    public enum ShipClass { Cutter, Ram, CrewDestroyer, Boarder, Merchant }
    public enum ResourceType { Wood, Silver }

    /// <summary>Banks of archers and javelin men are manned posts; the machines are built in the workshop (docs/UZBROJENIE.md).</summary>
    public enum WeaponType { None, ArcherBank, JavelinBank, Scorpion, Gastraphetes, DeckBallista, StoneCatapult, LeadBallista, HeavyCatapult, Onager }

    public enum SlotSize { Light, Medium, Heavy }

    /// <summary>Which men a post draws: a bank only its own trade, a machine any fighter.</summary>
    public enum WeaponCrew { Archers, Javelins, Any }

    /// <summary>
    /// One weapon (docs/UZBROJENIE.md, after the unit catalogue of Morza Bogów, tuned to this game): the post it needs, the
    /// men who work it, its range and cycle, and the packet one hit deals to hull, men and module (10 men-HP = one man).
    /// Shots fly: a bolt straight at its speed, a lobbed stone or lead shot on an arc that takes at least ArcMinSeconds.
    /// Dispersion is the radius of the scatter at full range; a splash weapon hurts everyone within Splash once.
    /// </summary>
    public sealed class WeaponDefinition
    {
        public WeaponType Type;
        public string Name;
        public SlotSize Slot;
        public WeaponCrew CrewKind;
        public int Crew;
        public double MinRange, MaxRange, Cycle, Hull, Men, Module, ArcDegrees, Speed, Dispersion, Splash, BuildingMultiplier;
        public bool Lobbed;
        public int CostWood, CostSilver;
        public double WorkshopSeconds;

        public double FlightSeconds(double distance) => Lobbed ? Math.Max(Rules.ArcMinSeconds, distance / Speed) : distance / Speed;
        public bool IsMachine => CrewKind == WeaponCrew.Any;
    }

    public static class WeaponCatalog
    {
        static readonly WeaponDefinition[] All =
        {
            new WeaponDefinition { Type = WeaponType.None, Name = "—" },
            // Tuned so a crew destroyer's four banks hurt crews as its 29 archers did in stage 2 (docs/UZBROJENIE.md).
            new WeaponDefinition { Type = WeaponType.ArcherBank, Name = "Stanowisko łuczników", Slot = SlotSize.Light, CrewKind = WeaponCrew.Archers, Crew = 4,
                MinRange = 15, MaxRange = 100, Cycle = 2, Hull = 2, Men = 3.5, Module = 2, ArcDegrees = 360, Speed = 45, Dispersion = 6, BuildingMultiplier = 1 },
            new WeaponDefinition { Type = WeaponType.JavelinBank, Name = "Stanowisko oszczepników", Slot = SlotSize.Light, CrewKind = WeaponCrew.Javelins, Crew = 4,
                MinRange = 5, MaxRange = 45, Cycle = 3, Hull = 3, Men = 6, Module = 2, ArcDegrees = 360, Speed = 28, Dispersion = 3, BuildingMultiplier = 1 },
            new WeaponDefinition { Type = WeaponType.Scorpion, Name = "Skorpion", Slot = SlotSize.Medium, CrewKind = WeaponCrew.Any, Crew = 3,
                MinRange = 15, MaxRange = 140, Cycle = 3, Hull = 12, Men = 8, Module = 6, ArcDegrees = 270, Speed = 70, Dispersion = 4, BuildingMultiplier = 1, CostWood = 25, CostSilver = 15, WorkshopSeconds = 20 },
            new WeaponDefinition { Type = WeaponType.Gastraphetes, Name = "Gastrafetes", Slot = SlotSize.Medium, CrewKind = WeaponCrew.Any, Crew = 2,
                MinRange = 5, MaxRange = 90, Cycle = 1.5, Hull = 3, Men = 10, Module = 2, ArcDegrees = 270, Speed = 60, Dispersion = 3, BuildingMultiplier = 0.5, CostWood = 15, CostSilver = 20, WorkshopSeconds = 20 },
            new WeaponDefinition { Type = WeaponType.DeckBallista, Name = "Balista pokładowa", Slot = SlotSize.Medium, CrewKind = WeaponCrew.Any, Crew = 6,
                MinRange = 30, MaxRange = 180, Cycle = 5, Hull = 45, Men = 4, Module = 18, ArcDegrees = 270, Speed = 75, Dispersion = 4, BuildingMultiplier = 1.25, CostWood = 35, CostSilver = 30, WorkshopSeconds = 30 },
            new WeaponDefinition { Type = WeaponType.StoneCatapult, Name = "Katapulta kamienna", Slot = SlotSize.Medium, CrewKind = WeaponCrew.Any, Crew = 6,
                MinRange = 40, MaxRange = 200, Cycle = 8, Hull = 40, Men = 10, Module = 15, ArcDegrees = 120, Speed = 45, Dispersion = 9, Splash = 4, Lobbed = true, BuildingMultiplier = 2, CostWood = 40, CostSilver = 15, WorkshopSeconds = 30 },
            new WeaponDefinition { Type = WeaponType.LeadBallista, Name = "Balista z pociskami ołowianymi", Slot = SlotSize.Heavy, CrewKind = WeaponCrew.Any, Crew = 6,
                MinRange = 50, MaxRange = 240, Cycle = 6, Hull = 70, Men = 4, Module = 25, ArcDegrees = 180, Speed = 80, Dispersion = 4, BuildingMultiplier = 1.25, CostWood = 50, CostSilver = 45, WorkshopSeconds = 40 },
            new WeaponDefinition { Type = WeaponType.HeavyCatapult, Name = "Ciężka katapulta (ołów)", Slot = SlotSize.Heavy, CrewKind = WeaponCrew.Any, Crew = 8,
                MinRange = 60, MaxRange = 260, Cycle = 10, Hull = 100, Men = 20, Module = 30, ArcDegrees = 120, Speed = 45, Dispersion = 12, Splash = 8, Lobbed = true, BuildingMultiplier = 2, CostWood = 60, CostSilver = 50, WorkshopSeconds = 45 },
            new WeaponDefinition { Type = WeaponType.Onager, Name = "Onager (ołów)", Slot = SlotSize.Heavy, CrewKind = WeaponCrew.Any, Crew = 8,
                MinRange = 100, MaxRange = 320, Cycle = 14, Hull = 160, Men = 12, Module = 30, ArcDegrees = 120, Speed = 40, Dispersion = 14, Splash = 6, Lobbed = true, BuildingMultiplier = 2.5, CostWood = 70, CostSilver = 60, WorkshopSeconds = 55 },
        };

        public static WeaponDefinition Get(WeaponType type) => All[(int)type];

        /// <summary>A weapon fits a post of its own size; a heavy machine never goes into a medium post.</summary>
        public static bool Fits(WeaponType type, SlotSize slot)
        {
            if (type == WeaponType.None) return true;
            var weapon = Get(type);
            return weapon.Slot == slot;
        }
    }

    /// <summary>Complete template values from ECONOMY_DESIGN.md E02 and E04. Radius, mass and turn rate are M1 tuning values.</summary>
    public sealed class ShipDefinition
    {
        public ShipClass Class;
        public double Hull, Speed, BuildSeconds, Radius, Mass, TurnRadiansPerSecond, Vision;

        /// <summary>Width of the hull and its depth below the waterline when empty, in metres.</summary>
        public double Beam, Draft;
        public int MaxCrew, CostWood, CostSilver, Capacity, WoodWork, SilverWork;

        /// <summary>
        /// Weapon posts in a fixed order, and what a new hull carries in them (priced into the template). Hull damage from
        /// projectiles is reduced by ProjectileArmor (never rams); every warship has a ram, RamMultiplier strong.
        /// </summary>
        public SlotSize[] Slots = Array.Empty<SlotSize>();
        public WeaponType[] DefaultLoadout = Array.Empty<WeaponType>();
        public double ProjectileArmor, RamMultiplier;

        public bool IsEconomic => Capacity > 0;
        public bool CanRam => !IsEconomic && RamMultiplier > 0;
        public bool CanHarvest => WoodWork > 0 || SilverWork > 0;
        public int BaseSailors => (int)Math.Ceiling(MaxCrew * 0.25);
        public int StandardSailors => IsEconomic ? MaxCrew : BaseSailors;
        public int StandardSoldiers => MaxCrew - StandardSailors;

        /// <summary>
        /// The crew a new hull of this class carries (docs/ZALOGA_I_ABORDAZ.md): the crew destroyer is a ship of archers,
        /// the boarder of swordsmen, the ram of both; working ships are all sailors. Every default keeps the 25% sailors
        /// E04 prices, so template prices are unchanged.
        /// </summary>
        public CrewPreset DefaultPreset =>
            IsEconomic ? CrewPreset.Seamen : Class == ShipClass.CrewDestroyer ? CrewPreset.Volley : Class == ShipClass.Boarder ? CrewPreset.Boarding : CrewPreset.Balanced;

        /// <summary>A full crew trained to a preset. Sailors never fall below the 25% a hull needs to keep its speed.</summary>
        public CrewPlan PlanFor(CrewPreset preset)
        {
            if (IsEconomic) return new CrewPlan { Sailors = MaxCrew, Preset = preset };
            var sailors = preset == CrewPreset.Seamen ? (int)Math.Ceiling(MaxCrew * Rules.SeamenSailorShare) : BaseSailors;
            var fighters = MaxCrew - sailors;
            // Shares of the fighting part: swordsmen, javelin men, archers (the rest).
            var (swordShare, javelinShare) = preset == CrewPreset.Boarding ? (0.60, 0.30) : preset == CrewPreset.Volley ? (0.15, 0.20) : (0.40, 0.25);
            var swordsmen = (int)Math.Round(fighters * swordShare, MidpointRounding.AwayFromZero);
            var javelins = (int)Math.Round(fighters * javelinShare, MidpointRounding.AwayFromZero);
            return new CrewPlan { Sailors = sailors, Swordsmen = swordsmen, Javelins = javelins, Archers = fighters - swordsmen - javelins, Preset = preset };
        }

        /// <summary>A plan given in numbers, held to the hull's size and to the sailors it needs.</summary>
        public CrewPlan Clamp(CrewPlan plan)
        {
            if (IsEconomic) return new CrewPlan { Sailors = MaxCrew, Preset = CrewPreset.Custom };
            var sailors = Math.Max(BaseSailors, Math.Min(MaxCrew, plan.Sailors));
            var swordsmen = Math.Max(0, Math.Min(MaxCrew - sailors, plan.Swordsmen));
            var javelins = Math.Max(0, Math.Min(MaxCrew - sailors - swordsmen, plan.Javelins));
            var archers = Math.Max(0, Math.Min(MaxCrew - sailors - swordsmen - javelins, plan.Archers));
            return new CrewPlan { Sailors = sailors, Swordsmen = swordsmen, Javelins = javelins, Archers = archers, Preset = CrewPreset.Custom };
        }

        /// <summary>
        /// What each fitted item of the default loadout and the ram is worth: the template's equipment share split by the
        /// catalogue's weights (light 1, medium 3, heavy 6, ram 2), wood and silver apart, remainders handed out in post
        /// order so the parts add up exactly to the equipment share. Index Slots.Length is the ram.
        /// </summary>
        public (long wood, long silver)[] ItemValues()
        {
            var weights = new int[Slots.Length + 1];
            for (var i = 0; i < Slots.Length; i++)
                weights[i] = DefaultLoadout.Length > i && DefaultLoadout[i] != WeaponType.None ? (Slots[i] == SlotSize.Light ? 1 : Slots[i] == SlotSize.Medium ? 3 : 6) : 0;
            weights[Slots.Length] = CanRam ? 2 : 0;
            var total = 0;
            foreach (var weight in weights) total += weight;
            var values = new (long wood, long silver)[weights.Length];
            if (total == 0) return values;
            long Split(long amount, int index) => amount * weights[index] / total;
            var wood = TemplatePricing.EquipmentWood(this);
            var silver = TemplatePricing.EquipmentSilver(this);
            long usedWood = 0, usedSilver = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                values[i] = (Split(wood, i), Split(silver, i));
                usedWood += values[i].wood;
                usedSilver += values[i].silver;
            }
            // The remainders go one by one to weighted items in order.
            for (var i = 0; usedWood < wood; i = (i + 1) % weights.Length) if (weights[i] > 0) { values[i].wood++; usedWood++; }
            for (var i = 0; usedSilver < silver; i = (i + 1) % weights.Length) if (weights[i] > 0) { values[i].silver++; usedSilver++; }
            return values;
        }
    }

    public static class ShipCatalog
    {
        static readonly ShipDefinition[] Definitions =
        {
            // Posts, default weapons, projectile armour and ram strength after the unit catalogue (docs/UZBROJENIE.md).
            new ShipDefinition { Class = ShipClass.Cutter, Hull = 180, Speed = 10, MaxCrew = 8, CostWood = 60, CostSilver = 20, BuildSeconds = 18, Capacity = 60, WoodWork = 6, SilverWork = 3, Radius = 6, Mass = 180, TurnRadiansPerSecond = 0.75, Vision = 250, Beam = 3.4, Draft = 0.7 },
            new ShipDefinition { Class = ShipClass.Ram, Hull = 760, Speed = 8.2, MaxCrew = 36, CostWood = 240, CostSilver = 100, BuildSeconds = 35, Radius = 10, Mass = 760, TurnRadiansPerSecond = 0.55, Vision = 200, Beam = 5.6, Draft = 1.2,
                Slots = new[] { SlotSize.Light, SlotSize.Medium }, DefaultLoadout = new[] { WeaponType.ArcherBank, WeaponType.None }, ProjectileArmor = 0.15, RamMultiplier = 1.5 },
            new ShipDefinition { Class = ShipClass.CrewDestroyer, Hull = 520, Speed = 6.8, MaxCrew = 52, CostWood = 180, CostSilver = 90, BuildSeconds = 30, Radius = 11, Mass = 520, TurnRadiansPerSecond = 0.5, Vision = 200, Beam = 6.2, Draft = 1.3,
                Slots = new[] { SlotSize.Light, SlotSize.Light, SlotSize.Light, SlotSize.Light }, DefaultLoadout = new[] { WeaponType.ArcherBank, WeaponType.ArcherBank, WeaponType.ArcherBank, WeaponType.ArcherBank }, ProjectileArmor = 0.10, RamMultiplier = 0.6 },
            new ShipDefinition { Class = ShipClass.Boarder, Hull = 780, Speed = 5.2, MaxCrew = 90, CostWood = 200, CostSilver = 180, BuildSeconds = 40, Radius = 13, Mass = 780, TurnRadiansPerSecond = 0.35, Vision = 200, Beam = 10.9, Draft = 2.2,
                Slots = new[] { SlotSize.Light, SlotSize.Light, SlotSize.Medium }, DefaultLoadout = new[] { WeaponType.JavelinBank, WeaponType.JavelinBank, WeaponType.None }, ProjectileArmor = 0.15, RamMultiplier = 0.7 },
            new ShipDefinition { Class = ShipClass.Merchant, Hull = 420, Speed = 7, MaxCrew = 12, CostWood = 140, CostSilver = 60, BuildSeconds = 28, Capacity = 480, Radius = 12, Mass = 420, TurnRadiansPerSecond = 0.4, Vision = 200, Beam = 10.1, Draft = 2.0, ProjectileArmor = 0.05 },
        };

        public static ShipDefinition Get(ShipClass shipClass) => Definitions[(int)shipClass];
    }

    /// <summary>
    /// The seabed around every shore: depth below the surface by distance from the waterline. Ships keep off water
    /// shallower than their draft; the presentation draws the same profile, so what is shallow looks shallow.
    /// </summary>
    public static class Seabed
    {
        public static readonly double[] Offset = { -3, 0, 3, 7, 13, 21, 31, 44 };
        public static readonly double[] Depth = { 0.5, 0.8, 1.4, 2.6, 4.4, 6.8, 9.6, 13 };

        /// <summary>Water kept under the keel.</summary>
        public const double KeelClearance = 0.3;

        public static double DepthAt(double fromWaterline)
        {
            if (fromWaterline <= Offset[0]) return Depth[0];
            for (var i = 1; i < Offset.Length; i++)
            {
                if (fromWaterline > Offset[i]) continue;
                var t = (fromWaterline - Offset[i - 1]) / (Offset[i] - Offset[i - 1]);
                return Depth[i - 1] + (Depth[i] - Depth[i - 1]) * t;
            }
            return Depth[Depth.Length - 1];
        }

        /// <summary>How far from the waterline the water first becomes deep enough for this draft.</summary>
        public static double ShoalWidth(double draft)
        {
            var needed = draft + KeelClearance;
            if (needed <= Depth[1]) return 0;
            for (var i = 2; i < Depth.Length; i++)
            {
                if (needed > Depth[i]) continue;
                var t = (needed - Depth[i - 1]) / (Depth[i] - Depth[i - 1]);
                return Offset[i - 1] + (Offset[i] - Offset[i - 1]) * t;
            }
            return Offset[Offset.Length - 1];
        }
    }

    /// <summary>Splits a complete template price without charging any part twice (E04). Amounts in 0.01 units.</summary>
    public static class TemplatePricing
    {
        public static long CrewSilver(ShipDefinition definition) =>
            definition.StandardSailors * Rules.Centi / 2 + definition.StandardSoldiers * Rules.Centi;

        public static long ConstructionWood(ShipDefinition definition) => definition.CostWood * Rules.Centi * 80 / 100;

        public static long ConstructionSilver(ShipDefinition definition) =>
            (definition.CostSilver * Rules.Centi - CrewSilver(definition)) * 40 / 100;

        public static long EquipmentWood(ShipDefinition definition) => definition.CostWood * Rules.Centi - ConstructionWood(definition);

        public static long EquipmentSilver(ShipDefinition definition) =>
            definition.CostSilver * Rules.Centi - CrewSilver(definition) - ConstructionSilver(definition);
    }

    /// <summary>How long a scenario is meant to last; it sets the map's size, its deposits and the opponent's pace.</summary>
    public enum MatchLength { Short, Standard, Long }

    /// <summary>
    /// Scale of one scenario (user decision of 2026-11-09, docs/BALANS_I_SKALA.md): a game lasts 30-90 minutes and the
    /// map's deposits pay for 30-55 balanced warships per side before they run dry. Spread multiplies every distance on
    /// the map; deposits are in units; the opponent waits for an attack group of this many warships.
    /// </summary>
    public sealed class MatchProfile
    {
        public MatchLength Length;
        public int TargetMinutes, TargetFleet;
        public double Spread, WoodYield = 1, SilverYield = 1, HomeWalls = 1;
        public long HomeWood, HomeSilver, PairWood, PairSilver, CentreWood, CentreSilver, StartWood, StartSilver;
        public int AttackGroup, CutterTarget;

        static readonly MatchProfile[] All =
        {
            // Calibrated with the opponent playing both sides without attacking (economy ceiling), seeds 9 and 23, on
            // the map with hinterlands: silver below 10% at 39 / 66 / 91 min; warships built a side 23-29 / 29-49 /
            // 49-59. Yield slows felling and smelting so the deposits last the scenario. docs/BALANS_I_SKALA.md.
            new MatchProfile { Length = MatchLength.Short, TargetMinutes = 30, TargetFleet = 30, Spread = 1.0,
                HomeWood = 3000, HomeSilver = 1600, PairWood = 1700, PairSilver = 850, CentreWood = 2250, CentreSilver = 1100,
                StartWood = 900, StartSilver = 450, WoodYield = 0.7, SilverYield = 0.7, HomeWalls = 2.5, AttackGroup = 5, CutterTarget = 8 },
            new MatchProfile { Length = MatchLength.Standard, TargetMinutes = 60, TargetFleet = 42, Spread = 1.3,
                HomeWood = 3300, HomeSilver = 1650, PairWood = 2550, PairSilver = 1400, CentreWood = 3400, CentreSilver = 1750,
                StartWood = 360, StartSilver = 160, WoodYield = 0.53, SilverYield = 0.53, HomeWalls = 3, AttackGroup = 6, CutterTarget = 7 },
            new MatchProfile { Length = MatchLength.Long, TargetMinutes = 90, TargetFleet = 55, Spread = 1.6,
                HomeWood = 3700, HomeSilver = 1750, PairWood = 3400, PairSilver = 1700, CentreWood = 4800, CentreSilver = 2300,
                StartWood = 360, StartSilver = 160, WoodYield = 0.42, SilverYield = 0.42, HomeWalls = 4.5, AttackGroup = 8, CutterTarget = 7 },
        };

        public static MatchProfile Of(MatchLength length) => All[(int)length];

        public static string Name(MatchLength length) =>
            length == MatchLength.Short ? "Krótka" : length == MatchLength.Long ? "Długa" : "Standardowa";
    }

    public static class Rules
    {
        public const int TickRate = 30;
        public const double Dt = 1.0 / TickRate;
        public const long Centi = 100;

        public const int FleetLimit = 100;
        public const int PortBerths = 3;
        public const double HandlingPerSecond = 60;
        public const double PortRange = 8;
        public const double NodeWorkRange = 8;

        // A work point and a port lie on water. Their land sits beside them, so ships can moor without entering it.
        public const double SiteLandOffset = 28;
        public const double SiteLandRadius = 20;
        public const double SettlementLandOffset = 26;
        public const double SettlementLandRadius = 20;

        /// <summary>A resource islet is an ellipse, wider along the shore than toward the sea.</summary>
        public const double SiteIsletHalfWidth = 19;
        public const double SiteIsletHalfDepth = 14;

        /// <summary>A loaded hold presses the hull deeper: up to this share of the empty draft.</summary>
        public const double DraftPerLoad = 0.65;

        /// <summary>
        /// A cutter at a work point first puts its crew ashore, and after the last load waits for them to come back
        /// aboard: no work while landing, the berth kept while embarking. Manoeuvre detail the income model leaves out.
        /// </summary>
        public const double LandCrewSeconds = 3;
        public const double EmbarkSeconds = 4;

        /// <summary>How far a rowed hull's oars reach out from its side. Passing hulls leave room for them.</summary>
        public static double OarReach(ShipDefinition definition) => Math.Max(2.5, definition.Radius * 2 * 0.28);

        /// <summary>
        /// Slipways stand on the settlement's land, across the shore from the land's centre toward the port, and run
        /// out sideways from the jetty: the war line's to one side, the port line's to the other. A finished hull
        /// slides down its slipway and floats off its end, bow to the sea.
        /// </summary>
        public const double SlipAlong = 6;
        public const double SlipCentre = 12.5;

        /// <summary>A settlement's land lies on the side away from the map center.</summary>
        public static double LandX(double portX) => portX + (portX < 0 ? -SettlementLandOffset : SettlementLandOffset);

        public static double SettlementLandX(Settlement settlement) => LandX(settlement.X);

        // E05: a bridgehead fits a merchant's hold and rises in 35 s after arrival. No warship line of its own.
        public const long OutpostWood = 300 * Centi;
        public const long OutpostSilver = 100 * Centi;
        public const double OutpostBuildSeconds = 35;
        public const double OutpostHull = 900;

        // E04: another shipyard adds one war line.
        public const long ShipyardWood = 200 * Centi;
        public const long ShipyardSilver = 100 * Centi;
        public const double ShipyardBuildSeconds = 40;

        // E03: escorts stay with the transport and do not chase beyond their zone.
        public const double EscortZone = 100;
        public const double EscortStation = 30;

        public const int WorkBerths = 2;
        public const int WoodNodeCap = 12;
        public const int SilverNodeCap = 4;
        public const long WarehouseWood = 6000 * Centi;
        public const long WarehouseSilver = 3000 * Centi;
        public const long StartWood = 360 * Centi;
        public const long StartSilver = 160 * Centi;
        public const long ReserveWood = 60 * Centi;
        public const long ReserveSilver = 20 * Centi;
        public const double SettlementHull = 1800, SettlementVision = 300;

        public const double ArcherRange = 100, ArcherMinRange = 15, ArcherReloadSeconds = 2, ArcherAccuracy = 0.75;
        public const double SoldierHitPoints = 10;

        public const double RamBase = 120, RamReferenceSpeed = 8, RamMaxDamage = 400, RamMinClosing = 3, RamRecoilFraction = 0.2, RamCooldownSeconds = 8, RamBackoffSeconds = 3;
        public const int BoardMinSoldiers = 10;
        public const double BoardGap = 4, BoardRelativeSpeed = 1.5, BoardHookSeconds = 3, BoardControlSeconds = 3;

        // ---------- Crew by trade and the fight on deck (docs/ZALOGA_I_ABORDAZ.md) ----------

        // ---------- Weapons in posts (docs/UZBROJENIE.md) ----------

        /// <summary>A lobbed stone or lead shot is in the air at least this long.</summary>
        public const double ArcMinSeconds = 1.5;

        /// <summary>All protection together never takes more than this share off a hull's damage.</summary>
        public const double MaxHullReduction = 0.7;

        /// <summary>Every post's module; at zero it is out of action until a port repairs it.</summary>
        public const double ModuleHitPoints = 100;

        /// <summary>A port mends a module for this share of its item value per restored part, and at most this long for a whole one.</summary>
        public const double ModuleRepairFraction = 0.25, ModuleRepairSeconds = 10;

        /// <summary>
        /// A ram's charge is called at least this long before it may strike (the telegraph), and is called when the
        /// target is this many seconds' sailing away. Another charge needs the cooldown and a hull's length of sea room.
        /// </summary>
        public const double RamTelegraphSeconds = 1.2, RamCallSeconds = 2.5;

        /// <summary>A hit from astern counts this much; abeam fully, head on half.</summary>
        public const double RamSternFactor = 0.75;

        /// <summary>The Seamen crew: 40% sailors, and the spare oars make the hull this much faster.</summary>
        public const double SeamenSailorShare = 0.4, SeamenSpeedBonus = 0.08;

        /// <summary>Men who can fight across the rail at once: this many plus one per metre of the narrower deck.</summary>
        public const int BoardFrontBase = 4;

        /// <summary>Hand-to-hand worth of each trade, attacking and defending its own deck.</summary>
        public const double SwordAttack = 1.2, SwordDefence = 1.1, JavelinAttack = 0.9, JavelinDefence = 1.0, ArcherAttack = 0.4, ArcherDefence = 0.9, SailorAttack = 0.25, SailorDefence = 0.35;

        /// <summary>Archers behind the front shoot onto the enemy deck; a defender shoots from its own, a little better.</summary>
        public const double ArcherSupportAttack = 0.3, ArcherSupportDefence = 0.4;

        /// <summary>
        /// Men lost each second on deck per point of the enemy's strength, and the morale a crew loses for losing its
        /// whole complement (in proportion for fewer). Tuned so a full boarder takes a full ram in about 20 s.
        /// </summary>
        public const double DeckLossFraction = 0.15, DeckMoralePerCrew = 120;

        /// <summary>
        /// Lines cut per second for each defending swordsman beyond half the attacker's. A boarder's 40 swordsmen cut a
        /// ram's grapples before they hold (about 2.4 s); between equals the lines hold.
        /// </summary>
        public const double HookCutPerSwordsman = 0.012;

        /// <summary>Of the men lost in a fight, this share is wounded rather than killed.</summary>
        public const double WoundedShare = 0.4;

        /// <summary>A crew whose morale falls below this, or with no fighters left, strikes its colours.</summary>
        public const double SurrenderMorale = 20;

        // Port muster (E04 crew prices): a recruit, a healed man, or a man retrained for another trade.
        public const long SailorSilver = 50, SoldierSilver = 100, HealSilver = 25, RetrainSilver = 50;
        public const double RecruitSeconds = 1, RetrainSeconds = 2, HealPerSecond = 4;

        /// <summary>Renewing deposits: a point left alone this long grows back at this share of its greatest yield.</summary>
        public const double RegrowDelaySeconds = 60, RegrowShare = 0.15;

        /// <summary>An idle ship this close to its own shipyard port regroups there by itself.</summary>
        public const double MusterRange = 80;

        public const double RefundAfterStart = 0.8;
        public const double RepairFullFraction = 0.25, RepairHullPerSecond = 12;
        public const double ScrapFraction = 0.15, ScrapSeconds = 20, ScrapCombatLockSeconds = 10;
        public const double WreckCargoFraction = 0.5, WreckLifeSeconds = 300, WreckRange = 20;
    }

    /// <summary>Small deterministic generator with a saved state.</summary>
    public struct DeterministicRandom
    {
        public ulong State;

        public DeterministicRandom(ulong seed)
        {
            State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public double NextDouble()
        {
            State ^= State << 13;
            State ^= State >> 7;
            State ^= State << 17;
            return (State >> 11) * (1.0 / (1UL << 53));
        }
    }
}
