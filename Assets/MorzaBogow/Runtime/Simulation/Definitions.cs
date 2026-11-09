using System;

namespace MorzaBogow.Simulation
{
    public enum ShipClass { Cutter, Ram, CrewDestroyer, Boarder }
    public enum ResourceType { Bronze, Rations }

    /// <summary>Starting balance values from GAME_DESIGN.md. Radius, mass and turn rate are M1 tuning values.</summary>
    public sealed class ShipDefinition
    {
        public ShipClass Class;
        public double Hull, Speed, BuildSeconds, Radius, Mass, TurnRadiansPerSecond, Vision;
        public int MaxCrew, CostBronze, CostRations, Capacity, BronzeWork, RationsWork, LightSlots;
        public bool CanHarvest => Capacity > 0;
        public int BaseSailors => (int)Math.Ceiling(MaxCrew * 0.25);
    }

    public static class ShipCatalog
    {
        static readonly ShipDefinition[] Definitions =
        {
            new ShipDefinition { Class = ShipClass.Cutter, Hull = 180, Speed = 8.5, MaxCrew = 8, CostBronze = 200, CostRations = 0, BuildSeconds = 20, Capacity = 160, BronzeWork = 4, RationsWork = 3, LightSlots = 0, Radius = 6, Mass = 180, TurnRadiansPerSecond = 0.75, Vision = 250 },
            new ShipDefinition { Class = ShipClass.Ram, Hull = 760, Speed = 8.2, MaxCrew = 36, CostBronze = 800, CostRations = 300, BuildSeconds = 50, LightSlots = 1, Radius = 10, Mass = 760, TurnRadiansPerSecond = 0.55, Vision = 200 },
            new ShipDefinition { Class = ShipClass.CrewDestroyer, Hull = 520, Speed = 6.8, MaxCrew = 52, CostBronze = 600, CostRations = 200, BuildSeconds = 45, LightSlots = 4, Radius = 11, Mass = 520, TurnRadiansPerSecond = 0.5, Vision = 200 },
            new ShipDefinition { Class = ShipClass.Boarder, Hull = 780, Speed = 5.2, MaxCrew = 90, CostBronze = 900, CostRations = 200, BuildSeconds = 60, LightSlots = 2, Radius = 13, Mass = 780, TurnRadiansPerSecond = 0.35, Vision = 200 },
        };

        public static ShipDefinition Get(ShipClass shipClass) => Definitions[(int)shipClass];
    }

    public static class Rules
    {
        public const int TickRate = 30;
        public const double Dt = 1.0 / TickRate;
        public const long Centi = 100;

        public const int LightWeaponCost = 60;
        public const int SoldierRationCost = 3;
        public const int FleetLimit = 100;
        public const int StationCount = 2;
        public const double HandlingPerSecond = 40;
        public const double PortRange = 30;
        public const double NodeWorkRange = 30;
        public const int BronzeNodeCap = 12;
        public const int RationsNodeCap = 9;
        public const double ShellRegenMin = 1, ShellRegenSlope = 3;
        public const double ShellCloseFraction = 0.10, ShellReopenFraction = 0.30, ShellSlowFraction = 0.35, ShellMinFactor = 0.4;
        public const long SettlementCapacity = 5000 * Centi;
        public const double SettlementHull = 1800, SettlementVision = 300;

        public const double ArcherHullDamage = 2, ArcherCrewDamage = 4, ArcherRange = 100, ArcherMinRange = 15, ArcherReloadSeconds = 2, ArcherAccuracy = 0.75;
        public const double SoldierHitPoints = 10;

        public const double RamBase = 120, RamReferenceSpeed = 8, RamMaxDamage = 400, RamMinClosing = 3, RamRecoilFraction = 0.2, RamCooldownSeconds = 8, RamBackoffSeconds = 3;
        public const int BoardMinSoldiers = 10;
        public const double BoardGap = 4, BoardRelativeSpeed = 1.5, BoardHookSeconds = 3, BoardControlSeconds = 3, BoardLossFraction = 0.05;

        public const double RefundAfterStart = 0.8;
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
