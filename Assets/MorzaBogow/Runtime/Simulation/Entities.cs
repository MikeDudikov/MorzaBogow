using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    public enum OrderKind { Idle, Move, Harvest, AttackShip, AttackSettlement, Ram, Board, Salvage, Repair, Scrap, Transfer, Expedition, Escort }
    public enum TransportPhase { ToSource, WaitSourceBerth, Loading, ToDestination, WaitDestinationBerth, Unloading, Building }
    /// <summary>Landing: the crew goes ashore before work starts. Embarking: the crew is back aboard before the cutter casts off.</summary>
    public enum HarvestPhase { ToNode, WaitingBerth, Working, ToPort, WaitingPort, Docked, Landing, Embarking }
    /// <summary>Approach, then the called charge (Telegraph, at least 1.2 s before contact), the strike, recoil and cooldown.</summary>
    public enum RamPhase { Approach, Recoil, Cooldown, Telegraph }

    /// <summary>Hinterland: the large island behind a port or a work point's islet, joined to it, as the land a town or a forest stands on.</summary>
    public enum IslandKind { Coast, ResourceSite, Settlement, Hinterland }

    /// <summary>Land ships cannot enter. Only a coast or a hinterland's high ground also stops shots; a quay or a settlement is too low for that.</summary>
    public sealed class Island
    {
        /// <summary>Radius is the largest distance from the centre to the waterline.</summary>
        public double X, Y, Radius;
        public IslandKind Kind;

        /// <summary>
        /// The waterline as distances from the centre at LandShape.Samples evenly spaced angles, counted from +X
        /// toward +Y. Null means a circle of Radius. The model of the land is turned by Yaw degrees to match.
        /// </summary>
        public double[] Outline;
        public double Yaw;

        // Polygon derived from the outline; not saved.
        internal double[] PolygonX, PolygonY;

        public bool BlocksShots => Kind == IslandKind.Coast || Kind == IslandKind.Hinterland;
    }

    /// <summary>A finite coastal work point with two berths and a FIFO queue (E02).</summary>
    public sealed class ResourceNode
    {
        public int Id;
        public ResourceType Type;
        public double X, Y;
        public long Stock;
        public int KnownMask;

        /// <summary>
        /// Deposits renew (user decision of 2026-11-10: they do not decide the end of a game): left alone for a while, a
        /// point grows back toward the stock it started with (Capacity; zero means it does not renew).
        /// </summary>
        public long Capacity;
        public int LastWorkedTick = -100000;
        public double RegrowRemainder;

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

    /// <summary>
    /// The settlement of cancelling a build order: reservation released, and what is lost if work had begun.
    /// Amounts in 0.01 units. Returned (released minus lost) becomes free stock again.
    /// </summary>
    public struct CancelQuote
    {
        public bool Valid, Started;
        public double Progress;
        public long ReleasedWood, ReleasedSilver, LostWood, LostSilver;
        public long ReturnedWood => ReleasedWood - LostWood;
        public long ReturnedSilver => ReleasedSilver - LostSilver;
    }

    public sealed class BuildOrder
    {
        public ShipClass Class;
        public long PaidWood, PaidSilver, UsedWood, UsedSilver;
        public int TotalTicks, DoneTicks;
        public bool Started;

        /// <summary>A workshop order makes this weapon; a ship order may follow a fleet blueprint.</summary>
        public WeaponType Weapon;
        public int BlueprintId = -1;
    }

    /// <summary>What a port builds in its one construction slot (docs/UZBROJENIE.md): buildings and the research.</summary>
    public enum ProjectKind { None, Workshop, Town, SageSchool, HeavyArmsResearch }

    /// <summary>A fleet blueprint: a hull, the weapons for its posts and its crew, built with one order.</summary>
    public sealed class Blueprint
    {
        public int Id, Owner;
        public string Name;
        public ShipClass Class;
        public WeaponType[] Loadout = System.Array.Empty<WeaponType>();
        public CrewPreset Preset;
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

        /// <summary>
        /// Buildings beyond the shipyard (docs/UZBROJENIE.md): the workshop (arsenal) makes weapons into the port's
        /// arsenal; a home port grows into a town, which may raise the Sages' School. One project at a time.
        /// </summary>
        public bool HasWorkshop, IsTown, HasSageSchool;
        public ProjectKind ProjectKind;
        public BuildOrder Project;
        public readonly List<BuildOrder> WorkshopQueue = new List<BuildOrder>();

        /// <summary>Weapons lying in the port, by type, ready to be fitted.</summary>
        public int[] Arsenal = new int[System.Enum.GetValues(typeof(WeaponType)).Length];

        /// <summary>How strong the walls are against the base value: a longer scenario's home port is built to hold out longer (MatchProfile.HomeWalls).</summary>
        public double Walls = 1;

        public double MaxHull => (IsOutpost ? Rules.OutpostHull : Rules.SettlementHull) * Walls;
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
        /// <summary>
        /// The crew by trade (docs/ZALOGA_I_ABORDAZ.md): sailors steer, row and gather; swordsmen fight hand to hand when
        /// boarding or repelling boarders; archers shoot at the crews of nearby ships and defend their own deck. The
        /// wounded fight and row no more until a port replaces them. Plan is the crew the ship's port brings it back to.
        /// </summary>
        public int Sailors, Swordsmen, Javelins, Archers, Wounded;
        public int Soldiers => Swordsmen + Javelins + Archers;
        public int Crew => Sailors + Swordsmen + Javelins + Archers;

        /// <summary>
        /// The weapon in each post (ShipDefinition.Slots order), how far its cycle has come (1 = ready), its module's hit
        /// points and what the fitted item is worth (docs/UZBROJENIE.md). Captured with the ship.
        /// </summary>
        public WeaponType[] Mounts = System.Array.Empty<WeaponType>();

        /// <summary>The weapons the ship should carry: its port refits it toward this, as the crew toward its plan.</summary>
        public WeaponType[] Loadout = System.Array.Empty<WeaponType>();
        public double[] Progress = System.Array.Empty<double>();
        public double[] ModuleHp = System.Array.Empty<double>();
        public long[] ItemWood = System.Array.Empty<long>(), ItemSilver = System.Array.Empty<long>();

        /// <summary>The charge's telegraph ticks left, and the ram's own worth for repairs (the last item value).</summary>
        public int TelegraphTicks;
        public CrewPlan Plan;
        public double CrewDamage, Morale = 100;

        /// <summary>The share of a wounded man carried between casualties, so two of every five fallen are wounded.</summary>
        public double WoundRemainder;

        /// <summary>Ticks of work left on the man the port is healing, recruiting or retraining now.</summary>
        public int MusterTicks;
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

        /// <summary>Ticks left while the shore party lands or comes back aboard at a work point.</summary>
        public int CrewTicks;
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

    /// <summary>The crew a ship should carry: its port recruits, heals and retrains toward it.</summary>
    public struct CrewPlan
    {
        public int Sailors, Swordsmen, Javelins, Archers;
        public CrewPreset Preset;
        public int Total => Sailors + Swordsmen + Javelins + Archers;
    }

    /// <summary>A shot in the air: it lands where it was aimed (scatter already drawn) when its flight is done.</summary>
    public sealed class Projectile
    {
        public int Id, OwnerId, ShooterId, TargetShipId = -1, TargetSettlementId = -1;
        public WeaponType Weapon;
        public double FromX, FromY, ToX, ToY;
        public int LaunchTick, ArriveTick;
    }

    /// <summary>Ready-made crews a port can train a ship's people to; Custom is a plan given in numbers.</summary>
    public enum CrewPreset { Balanced, Boarding, Volley, Seamen, Custom }

    /// <summary>Grapples hold for a few seconds before men can cross; then the decks fight a round each second.</summary>
    public enum BoardingPhase { Hooks, Melee }

    public sealed class BoardingLink
    {
        public int AttackerId, TargetId;
        public int HookTicks, CycleTicks, ControlTicks;
        public bool Hooked;

        /// <summary>How far the defender's swordsmen have hacked through the grapple lines (1 cuts them).</summary>
        public double CutProgress;

        /// <summary>Casualties owed to each side between rounds, so the fight loses no men to rounding.</summary>
        public double AttackerLossRemainder, TargetLossRemainder;

        /// <summary>
        /// The attacker's wounded when the hooks went over. A boarding lost on the enemy deck leaves the wounded of this
        /// attack there too (docs/UZGODNIENIE_SPECYFIKACJI.md); those already aboard before it stay.
        /// </summary>
        public int AttackerWoundedAtStart;

        /// <summary>Strength each side brought to the last round, for the interface. Not saved; derived next round.</summary>
        public double AttackerStrength, TargetStrength;
        public int AttackerFront, TargetFront;

        public BoardingPhase Phase => Hooked ? BoardingPhase.Melee : BoardingPhase.Hooks;
    }

    public enum CommandKind { Move, Harvest, AttackShip, AttackSettlement, Ram, Board, Stop, QueueShip, CancelQueue, Salvage, Repair, Scrap, Transfer, FoundOutpost, Escort, BuildShipyard, SetCrewPlan, BuildProject, CancelProject, QueueWeapon, CancelWeapon, SetLoadout, SaveBlueprint }

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

        /// <summary>SetCrewPlan: a preset, or with Custom the plan's numbers.</summary>
        public CrewPreset Preset;
        public CrewPlan Plan;

        /// <summary>BuildProject: what to build; QueueWeapon: the weapon; SetLoadout / SaveBlueprint: the weapons by post.</summary>
        public ProjectKind Project;
        public WeaponType Weapon;
        public WeaponType[] Loadout;

        /// <summary>QueueShip after a blueprint; SaveBlueprint: its name.</summary>
        public int BlueprintId = -1;
        public string Name;
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
