using System;

namespace MorzaBogow.Simulation
{
    public enum ShipClass { Cutter, Ram, CrewDestroyer, Boarder, Merchant }
    public enum ResourceType { Wood, Silver }

    /// <summary>Complete template values from ECONOMY_DESIGN.md E02 and E04. Radius, mass and turn rate are M1 tuning values.</summary>
    public sealed class ShipDefinition
    {
        public ShipClass Class;
        public double Hull, Speed, BuildSeconds, Radius, Mass, TurnRadiansPerSecond, Vision;

        /// <summary>Width of the hull and its depth below the waterline when empty, in metres.</summary>
        public double Beam, Draft;
        public int MaxCrew, CostWood, CostSilver, Capacity, WoodWork, SilverWork, LightSlots;

        public bool IsEconomic => Capacity > 0;
        public bool CanHarvest => WoodWork > 0 || SilverWork > 0;
        public int BaseSailors => (int)Math.Ceiling(MaxCrew * 0.25);
        public int StandardSailors => IsEconomic ? MaxCrew : BaseSailors;
        public int StandardSoldiers => MaxCrew - StandardSailors;
    }

    public static class ShipCatalog
    {
        static readonly ShipDefinition[] Definitions =
        {
            new ShipDefinition { Class = ShipClass.Cutter, Hull = 180, Speed = 10, MaxCrew = 8, CostWood = 60, CostSilver = 20, BuildSeconds = 18, Capacity = 60, WoodWork = 6, SilverWork = 3, LightSlots = 0, Radius = 6, Mass = 180, TurnRadiansPerSecond = 0.75, Vision = 250, Beam = 3.4, Draft = 0.7 },
            new ShipDefinition { Class = ShipClass.Ram, Hull = 760, Speed = 8.2, MaxCrew = 36, CostWood = 240, CostSilver = 100, BuildSeconds = 35, LightSlots = 1, Radius = 10, Mass = 760, TurnRadiansPerSecond = 0.55, Vision = 200, Beam = 5.6, Draft = 1.2 },
            new ShipDefinition { Class = ShipClass.CrewDestroyer, Hull = 520, Speed = 6.8, MaxCrew = 52, CostWood = 180, CostSilver = 90, BuildSeconds = 30, LightSlots = 4, Radius = 11, Mass = 520, TurnRadiansPerSecond = 0.5, Vision = 200, Beam = 6.2, Draft = 1.3 },
            new ShipDefinition { Class = ShipClass.Boarder, Hull = 780, Speed = 5.2, MaxCrew = 90, CostWood = 200, CostSilver = 180, BuildSeconds = 40, LightSlots = 2, Radius = 13, Mass = 780, TurnRadiansPerSecond = 0.35, Vision = 200, Beam = 10.9, Draft = 2.2 },
            new ShipDefinition { Class = ShipClass.Merchant, Hull = 420, Speed = 7, MaxCrew = 12, CostWood = 140, CostSilver = 60, BuildSeconds = 28, Capacity = 480, LightSlots = 0, Radius = 12, Mass = 420, TurnRadiansPerSecond = 0.4, Vision = 200, Beam = 10.1, Draft = 2.0 },
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

        public const double ArcherHullDamage = 2, ArcherCrewDamage = 4, ArcherRange = 100, ArcherMinRange = 15, ArcherReloadSeconds = 2, ArcherAccuracy = 0.75;
        public const double SoldierHitPoints = 10;

        public const double RamBase = 120, RamReferenceSpeed = 8, RamMaxDamage = 400, RamMinClosing = 3, RamRecoilFraction = 0.2, RamCooldownSeconds = 8, RamBackoffSeconds = 3;
        public const int BoardMinSoldiers = 10;
        public const double BoardGap = 4, BoardRelativeSpeed = 1.5, BoardHookSeconds = 3, BoardControlSeconds = 3, BoardLossFraction = 0.05;

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
