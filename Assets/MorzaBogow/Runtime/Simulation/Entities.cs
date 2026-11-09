using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    public enum OrderKind { Idle, Move, Harvest, AttackShip, AttackSettlement, Ram, Board }
    public enum HarvestPhase { ToNode, Working, ToPort, Docked }
    public enum RamPhase { Approach, Recoil, Cooldown }

    public sealed class Island
    {
        public double X, Y, Radius;
    }

    public sealed class ResourceNode
    {
        public int Id;
        public ResourceType Type;
        public double X, Y;
        public long Stock, Capacity;
        public bool Closed;
        public int KnownMask;
        public double RegenRemainder;
    }

    public sealed class BuildOrder
    {
        public ShipClass Class;
        public long PaidBronze, PaidRations;
        public int TotalTicks, DoneTicks;
        public bool Started;
    }

    public sealed class Settlement
    {
        public int Id, Owner;
        public double X, Y, Hull = Rules.SettlementHull;
        public long Bronze, Rations;
        public readonly List<BuildOrder> Queue = new List<BuildOrder>();
        public readonly List<int> DockedShips = new List<int>();
        public bool Destroyed => Hull <= 0;

        public long Stock(ResourceType type) => type == ResourceType.Bronze ? Bronze : Rations;

        public void Add(ResourceType type, long amount)
        {
            if (type == ResourceType.Bronze) Bronze += amount; else Rations += amount;
        }
    }

    public sealed class Ship
    {
        public int Id, Owner;
        public ShipClass Class;
        public double X, Y, Heading, Speed, Hull;
        public int Sailors, Soldiers;
        public double CrewDamage, Morale = 100;
        public double Reload;
        public int LastDamagedTick = -100000;

        public OrderKind Order;
        public double TargetX, TargetY;
        public int TargetId = -1;

        public int NodeId = -1;
        public HarvestPhase Harvest;
        public ResourceType CargoType;
        public long Cargo;
        public double WorkRemainder, UnloadRemainder;

        public RamPhase RamState;
        public int RamTimer;
        public int LastRamTick = -100000;

        public ShipDefinition Definition => ShipCatalog.Get(Class);
    }

    public sealed class BoardingLink
    {
        public int AttackerId, TargetId;
        public int HookTicks, CycleTicks, ControlTicks;
        public bool Hooked;
    }

    public enum CommandKind { Move, Harvest, AttackShip, AttackSettlement, Ram, Board, Stop, QueueShip, CancelQueue }

    public sealed class Command
    {
        public int Faction;
        public CommandKind Kind;
        public int[] ShipIds = System.Array.Empty<int>();
        public double X, Y;
        public int TargetId = -1;
        public ShipClass Class;
        public int QueueIndex;
    }

    public enum OutcomeKind { InProgress, Won, Draw }

    /// <summary>Presentation events; identifiers prevent duplicate effects.</summary>
    public sealed class SimEvent
    {
        public int EventId, Tick, SubjectId, OtherId;
        public string Type;
        public double X, Y, Amount;
    }
}
