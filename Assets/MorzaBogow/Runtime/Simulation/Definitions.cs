using System;

namespace MorzaBogow.Simulation
{
    public enum ShipClass { Cutter, Ram, CrewDestroyer, Boarder }
    public enum ResourceType { Wood, Silver }

    /// <summary>Complete template values from ECONOMY_DESIGN.md E02 and E04. Radius, mass and turn rate are M1 tuning values.</summary>
    public sealed class ShipDefinition
    {
        public ShipClass Class;
        public double Hull, Speed, BuildSeconds, Radius, Mass, TurnRadiansPerSecond, Vision;
        public int MaxCrew, CostWood, CostSilver, Capacity, WoodWork, SilverWork, LightSlots;

        public bool IsEconomic => Capacity > 0;
        public bool CanHarvest => IsEconomic;
        public int BaseSailors => (int)Math.Ceiling(MaxCrew * 0.25);
        public int StandardSailors => IsEconomic ? MaxCrew : BaseSailors;
        public int StandardSoldiers => MaxCrew - StandardSailors;
    }

    public static class ShipCatalog
    {
        static readonly ShipDefinition[] Definitions =
        {
            new ShipDefinition { Class = ShipClass.Cutter, Hull = 180, Speed = 10, MaxCrew = 8, CostWood = 60, CostSilver = 20, BuildSeconds = 18, Capacity = 60, WoodWork = 6, SilverWork = 3, LightSlots = 0, Radius = 6, Mass = 180, TurnRadiansPerSecond = 0.75, Vision = 250 },
            new ShipDefinition { Class = ShipClass.Ram, Hull = 760, Speed = 8.2, MaxCrew = 36, CostWood = 240, CostSilver = 100, BuildSeconds = 35, LightSlots = 1, Radius = 10, Mass = 760, TurnRadiansPerSecond = 0.55, Vision = 200 },
            new ShipDefinition { Class = ShipClass.CrewDestroyer, Hull = 520, Speed = 6.8, MaxCrew = 52, CostWood = 180, CostSilver = 90, BuildSeconds = 30, LightSlots = 4, Radius = 11, Mass = 520, TurnRadiansPerSecond = 0.5, Vision = 200 },
            new ShipDefinition { Class = ShipClass.Boarder, Hull = 780, Speed = 5.2, MaxCrew = 90, CostWood = 200, CostSilver = 180, BuildSeconds = 40, LightSlots = 2, Radius = 13, Mass = 780, TurnRadiansPerSecond = 0.35, Vision = 200 },
        };

        public static ShipDefinition Get(ShipClass shipClass) => Definitions[(int)shipClass];
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

        /// <summary>A settlement's land lies on the side away from the map center.</summary>
        public static double SettlementLandX(Settlement settlement) =>
            settlement.X + (settlement.X < 0 ? -SettlementLandOffset : SettlementLandOffset);

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
