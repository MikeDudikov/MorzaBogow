using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    public enum OrderKind { Idle, Move, Harvest, AttackShip, AttackSettlement, Ram, Board, Salvage, Repair, Scrap, Transfer, Expedition, Escort }
    public enum TransportPhase { ToSource, WaitSourceBerth, Loading, ToDestination, WaitDestinationBerth, Unloading, Building }
    public enum HarvestPhase { ToNode, WaitingBerth, Working, ToPort, WaitingPort, Docked }
    public enum RamPhase { Approach, Recoil, Cooldown }

    public enum IslandKind { Coast, ResourceSite, Settlement }

    /// <summary>Land ships cannot enter. Only a coast also stops shots; a quay or a settlement is too low for that.</summary>
    public sealed class Island
    {
        public double X, Y, Radius;
        public IslandKind Kind;

        public bool BlocksShots => Kind == IslandKind.Coast;
    }

    /// <summary>A finite coastal work point with two berths and a FIFO queue (E02).</summary>
    public sealed class ResourceNode
    {
        public int Id;
        public ResourceType Type;
        public double X, Y;
        public long Stock;
        public int KnownMask;
        public readonly List<int> Workers = new List<int>();
        public readonly List<int> Waiting = new List<int>();
    }

    /// <summary>A free shore where a bridgehead can rise (E05). The point lies on water, its land is beside it.</summary>
    public sealed class BuildSite
    {
        public int Id;
        public double X, Y;
        public int SettlementId = -1;
        public int BuilderShipId = -1;
    }

    public sealed class BuildOrder
    {
        public ShipClass Class;
        public long PaidWood, PaidSilver, UsedWood, UsedSilver;
        public int TotalTicks, DoneTicks;
        public bool Started;
    }

    public sealed class Settlement
    {
        public int Id, Owner;
        public double X, Y, Hull = Rules.SettlementHull;
        public long Wood, Silver, ReservedWood, ReservedSilver;
        public int RepairShipId = -1;
        public readonly List<BuildOrder> WarQueue = new List<BuildOrder>();
        public readonly List<BuildOrder> PortQueue = new List<BuildOrder>();
        public readonly List<int> DockedShips = new List<int>();
        public readonly List<int> PortWaiting = new List<int>();

        /// <summary>A bridgehead has its own store, port and port queue, but no war line until it gets a shipyard.</summary>
        public bool IsOutpost;
        public bool HasShipyard = true;
        public BuildOrder ShipyardBuild;

        public double MaxHull => IsOutpost ? Rules.OutpostHull : Rules.SettlementHull;
        public bool Destroyed => Hull <= 0;

        public long Stock(ResourceType type) => type == ResourceType.Wood ? Wood : Silver;
        public long Reserved(ResourceType type) => type == ResourceType.Wood ? ReservedWood : ReservedSilver;
        public long Available(ResourceType type) => Stock(type) - Reserved(type);
        public long Capacity(ResourceType type) => type == ResourceType.Wood ? Rules.WarehouseWood : Rules.WarehouseSilver;

        public void Add(ResourceType type, long amount)
        {
            if (type == ResourceType.Wood) Wood += amount; else Silver += amount;
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

        public long PaidConstructionWood, PaidConstructionSilver;

        public OrderKind Order;
        public double TargetX, TargetY;
        public int TargetId = -1;

        public int NodeId = -1;
        public int WreckId = -1;
        public HarvestPhase Harvest;
        public ResourceType CargoType;
        public long Cargo;
        public double WorkRemainder, UnloadRemainder;
        public int PortId = -1;

        // The next point of a route around land, and the target it was planned for.
        public bool HasWaypoint;
        public double WaypointX, WaypointY, RouteX, RouteY;
        public int ReplanTick;

        // A merchant's hold carries both resources at once (E02, E03).
        public long HoldWood, HoldSilver;
        public long WantWood, WantSilver;
        public TransportPhase Transport;
        public int SourceId = -1, DestinationId = -1, SiteId = -1, EscortId = -1;
        public int BuildTicks;
        public long BuildUsedWood, BuildUsedSilver;

        public double RepairWoodRemainder, RepairSilverRemainder;
        public int ScrapTicks;
        public bool Scrapped;

        public RamPhase RamState;
        public int RamTimer;
        public int LastRamTick = -100000;

        public ShipDefinition Definition => ShipCatalog.Get(Class);
    }

    /// <summary>Half of a sunk transport's real cargo, collectable until it expires (E06).</summary>
    public sealed class Wreck
    {
        public int Id;
        public double X, Y;
        public long Wood, Silver;
        public int ExpiresTick;
    }

    public sealed class BoardingLink
    {
        public int AttackerId, TargetId;
        public int HookTicks, CycleTicks, ControlTicks;
        public bool Hooked;
    }

    public enum CommandKind { Move, Harvest, AttackShip, AttackSettlement, Ram, Board, Stop, QueueShip, CancelQueue, Salvage, Repair, Scrap, Transfer, FoundOutpost, Escort, BuildShipyard }

    public sealed class Command
    {
        public int Faction;
        public CommandKind Kind;
        public int[] ShipIds = System.Array.Empty<int>();
        public double X, Y;
        public int TargetId = -1;
        public ShipClass Class;
        public int QueueIndex;
        public bool PortQueue;

        /// <summary>Transfer: the source port and amounts in 0.01 units. Zero amounts let the order pick a load.</summary>
        public int SourceId = -1;
        public long Wood, Silver;
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
