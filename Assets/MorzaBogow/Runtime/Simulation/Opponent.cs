using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Rule-following opponent (C08, E09): it only issues normal commands, pays normal costs, waits in the
    /// same queues and reads enemy ships through the same visibility check as the player. M1 scope: economy,
    /// production, attack groups and retreat. Known map knowledge: the enemy start and work point positions.
    /// </summary>
    public sealed class Opponent
    {
        const int StrategyPeriod = 3 * Rules.TickRate;
        const int TacticsPeriod = Rules.TickRate;

        const int CuttersPerOutpost = 2;
        const int MaxOutposts = 2;
        static readonly ShipClass[] BuildCycle = { ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder };

        readonly int faction;
        readonly int? fixedAttackGroup;
        int minimumAttackGroup = 2;
        int CutterTarget = 6;
        int buildIndex;

        /// <summary>Without a fixed group size the opponent waits for the attack group its scenario's profile names.</summary>
        public Opponent(int faction, int? minimumAttackGroup = null)
        {
            this.faction = faction;
            fixedAttackGroup = minimumAttackGroup;
        }

        public int BuildIndex { get => buildIndex; set => buildIndex = value; }

        /// <summary>Keeps every warship at home unless the base is threatened. Balance probes measure the economy's ceiling with it.</summary>
        public bool Passive { get; set; }

        public void Think(World world)
        {
            minimumAttackGroup = fixedAttackGroup ?? world.Profile.AttackGroup;
            CutterTarget = world.Profile.CutterTarget;
            if (world.Tick % StrategyPeriod == 0)
            {
                ManageEconomy(world);
                ManageProduction(world);
                ManageExpansion(world);
            }
            if (world.Tick % TacticsPeriod == 0) ManageFleet(world);
        }

        void ManageEconomy(World world)
        {
            var settlement = world.SettlementOf(faction);
            if (settlement == null) return;

            // Silver is the scarcer resource by design, so it is requested below half of the wood stock.
            var wanted = settlement.Silver * 2 < settlement.Wood ? ResourceType.Silver : ResourceType.Wood;
            var cutters = world.Ships.Where(ship => ship.Owner == faction && ship.Class == ShipClass.Cutter).OrderBy(ship => ship.Id).ToList();
            var onWanted = cutters.Count(ship => ship.Order == OrderKind.Harvest && world.FindNode(ship.NodeId)?.Type == wanted);

            foreach (var cutter in cutters)
            {
                // Rebalance only ships that just unloaded, keeping at most half on one side of the shortage.
                var rebalance = cutter.Order == OrderKind.Harvest && cutter.Harvest == HarvestPhase.ToNode && cutter.Cargo == 0
                                && world.FindNode(cutter.NodeId)?.Type != wanted && onWanted * 2 < cutters.Count;
                if (cutter.Order != OrderKind.Idle && !rebalance) continue;

                var node = FindNode(world, cutter, wanted);
                if (node != null)
                {
                    if (rebalance) onWanted++;
                    world.Issue(new Command { Faction = faction, Kind = CommandKind.Harvest, ShipIds = new[] { cutter.Id }, TargetId = node.Id });
                }
                else if (cutter.Order == OrderKind.Idle) Explore(world, cutter);
            }
        }

        ResourceNode FindNode(World world, Ship cutter, ResourceType wanted) =>
            world.Nodes
                .Where(item => (item.KnownMask & (1 << faction)) != 0 && item.Stock > 0)
                .OrderBy(item => item.Type == wanted ? 0 : 1)
                .ThenBy(item => World.Distance(cutter.X, cutter.Y, item.X, item.Y))
                .ThenBy(item => item.Id)
                .FirstOrDefault();

        void Explore(World world, Ship cutter)
        {
            var target = world.Nodes
                .Where(item => (item.KnownMask & (1 << faction)) == 0)
                .OrderBy(item => World.Distance(cutter.X, cutter.Y, item.X, item.Y))
                .ThenBy(item => item.Id)
                .FirstOrDefault();
            if (target != null) world.Issue(new Command { Faction = faction, Kind = CommandKind.Move, ShipIds = new[] { cutter.Id }, X = target.X, Y = target.Y });
        }

        void ManageProduction(World world)
        {
            var settlement = world.SettlementOf(faction);
            if (settlement == null || settlement.Destroyed) return;

            var cutters = world.Ships.Count(ship => ship.Owner == faction && ship.Class == ShipClass.Cutter);
            var cutter = ShipCatalog.Get(ShipClass.Cutter);
            var outposts = world.PortsOf(faction).Count(port => port.IsOutpost);
            if (settlement.PortQueue.Count == 0 && cutters < CutterTarget + outposts * CuttersPerOutpost
                && settlement.Available(ResourceType.Wood) >= cutter.CostWood * Rules.Centi
                && settlement.Available(ResourceType.Silver) >= cutter.CostSilver * Rules.Centi)
            {
                world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
            }

            foreach (var port in world.PortsOf(faction).Where(item => item.HasShipyard && item.WarQueue.Count == 0).OrderBy(item => item.Id).ToList())
            {
                var next = ShipCatalog.Get(BuildCycle[buildIndex % BuildCycle.Length]);

                // A waiting merchant's cargo is paid for first; otherwise warships would spend every saving.
                var saved = port == settlement ? Saving(world, settlement) : (wood: 0L, silver: 0L);
                var savedWood = saved.wood;
                var savedSilver = saved.silver;

                // E04: automatic military orders keep the reserve that protects rebuilding a cutter. Each port pays locally.
                if (port.Available(ResourceType.Wood) < next.CostWood * Rules.Centi + Rules.ReserveWood + savedWood) continue;
                if (port.Available(ResourceType.Silver) < next.CostSilver * Rules.Centi + Rules.ReserveSilver + savedSilver) continue;
                world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = next.Class, TargetId = port.Id });
                buildIndex++;
            }
        }

        /// <summary>What to keep for an idle merchant's next cargo, unless the fleet is too small or the base is under threat.</summary>
        (long wood, long silver) Saving(World world, Settlement home)
        {
            if (!world.Ships.Any(ship => ship.Owner == faction && ship.Class == ShipClass.Merchant && ship.Order == OrderKind.Idle)) return (0, 0);
            if (world.Ships.Count(ship => ship.Owner == faction && !ship.Definition.IsEconomic) < minimumAttackGroup) return (0, 0);
            if (world.Ships.Any(ship => ship.Owner != faction && world.CanSee(faction, ship) && World.Distance(ship.X, ship.Y, home.X, home.Y) < 250)) return (0, 0);

            var outposts = world.PortsOf(faction).Where(port => port.IsOutpost).OrderBy(port => port.Id).ToList();
            foreach (var outpost in outposts)
            {
                var need = SupplyNeed(world, outpost);
                if (need.wood + need.silver > 0) return need;
            }
            return outposts.Count < MaxOutposts ? (Rules.OutpostWood, Rules.OutpostSilver) : (0, 0);
        }

        /// <summary>What a bridgehead still lacks for its shipyard, counting goods already on their way to it.</summary>
        (long wood, long silver) SupplyNeed(World world, Settlement outpost)
        {
            if (outpost.HasShipyard || outpost.ShipyardBuild != null) return (0, 0);
            var inboundWood = 0L;
            var inboundSilver = 0L;
            foreach (var ship in world.Ships)
            {
                if (ship.Owner != faction || ship.Order != OrderKind.Transfer || ship.DestinationId != outpost.Id) continue;
                inboundWood += ship.HoldWood + ship.WantWood;
                inboundSilver += ship.HoldSilver + ship.WantSilver;
            }
            return (System.Math.Max(0, Rules.ShipyardWood - outpost.Available(ResourceType.Wood) - inboundWood),
                System.Math.Max(0, Rules.ShipyardSilver - outpost.Available(ResourceType.Silver) - inboundSilver));
        }

        /// <summary>E05 with normal costs: one merchant after the first warships, then a bridgehead on a discovered free shore.</summary>
        void ManageExpansion(World world)
        {
            var home = world.SettlementOf(faction);
            if (home == null || home.Destroyed) return;

            var outposts = world.PortsOf(faction).Where(port => port.IsOutpost).OrderBy(port => port.Id).ToList();
            foreach (var outpost in outposts)
            {
                if (!outpost.HasShipyard && outpost.ShipyardBuild == null
                    && outpost.Available(ResourceType.Wood) >= Rules.ShipyardWood && outpost.Available(ResourceType.Silver) >= Rules.ShipyardSilver)
                {
                    world.Issue(new Command { Faction = faction, Kind = CommandKind.BuildShipyard, TargetId = outpost.Id });
                }
            }

            // A bridgehead that lacks goods for its shipyard comes before founding another one.
            var needy = outposts.Where(outpost => { var need = SupplyNeed(world, outpost); return need.wood + need.silver > 0; }).ToList();
            if (outposts.Count >= MaxOutposts && needy.Count == 0) return;

            var merchants = world.Ships.Where(ship => ship.Owner == faction && ship.Class == ShipClass.Merchant).OrderBy(ship => ship.Id).ToList();
            var warships = world.Ships.Count(ship => ship.Owner == faction && !ship.Definition.IsEconomic);
            var merchant = ShipCatalog.Get(ShipClass.Merchant);
            if (merchants.Count == 0 && warships >= minimumAttackGroup && home.PortQueue.Count == 0
                && home.Available(ResourceType.Wood) >= merchant.CostWood * Rules.Centi + Rules.ReserveWood
                && home.Available(ResourceType.Silver) >= merchant.CostSilver * Rules.Centi + Rules.ReserveSilver)
            {
                world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = ShipClass.Merchant });
                return;
            }

            foreach (var ship in merchants.Where(item => item.Order == OrderKind.Idle))
            {
                if (needy.Count > 0)
                {
                    var outpost = needy[0];
                    needy.RemoveAt(0);
                    var need = SupplyNeed(world, outpost);
                    if (ship.HoldWood + ship.HoldSilver > 0)
                    {
                        world.Issue(new Command { Faction = faction, Kind = CommandKind.Transfer, ShipIds = new[] { ship.Id }, TargetId = outpost.Id });
                    }
                    else if (home.Available(ResourceType.Wood) >= need.wood + Rules.ReserveWood && home.Available(ResourceType.Silver) >= need.silver + Rules.ReserveSilver)
                    {
                        world.Issue(new Command { Faction = faction, Kind = CommandKind.Transfer, ShipIds = new[] { ship.Id }, TargetId = outpost.Id, SourceId = home.Id, Wood = need.wood, Silver = need.silver });
                    }
                    continue;
                }
                if (outposts.Count >= MaxOutposts) continue;

                var free = world.Sites.Where(site => world.IsSiteFree(site) && site.BuilderShipId < 0)
                    .OrderBy(site => World.Distance(ship.X, ship.Y, site.X, site.Y)).ThenBy(site => site.Id).ToList();
                var known = free.FirstOrDefault(site => world.IsExplored(faction, site.X, site.Y));
                if (known != null)
                {
                    if (home.Available(ResourceType.Wood) < Rules.OutpostWood + Rules.ReserveWood) continue;
                    if (home.Available(ResourceType.Silver) < Rules.OutpostSilver + Rules.ReserveSilver) continue;
                    world.Issue(new Command { Faction = faction, Kind = CommandKind.FoundOutpost, ShipIds = new[] { ship.Id }, TargetId = known.Id, SourceId = home.Id });
                }
                else if (free.Count > 0)
                {
                    world.Issue(new Command { Faction = faction, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = free[0].X, Y = free[0].Y });
                }
            }
        }

        void ManageFleet(World world)
        {
            var home = world.SettlementOf(faction);
            var enemyHome = world.SettlementOf(1 - faction);
            if (home == null || enemyHome == null) return;

            var warships = world.Ships.Where(ship => ship.Owner == faction && !ship.Definition.IsEconomic).OrderBy(ship => ship.Id).ToList();
            var visibleEnemies = world.Ships.Where(ship => ship.Owner != faction && world.CanSee(faction, ship)).OrderBy(ship => ship.Id).ToList();

            var active = new List<Ship>();
            foreach (var ship in warships)
            {
                // A badly holed hull, or a crew with half its fighters gone, goes home to regroup.
                // Only when the home port can pay the muster now: a ship waiting in the dock for silver is lost to the fleet.
                var crewSpent = ship.Plan.Swordsmen + ship.Plan.Archers > 0 && ship.Soldiers * 2 < ship.Plan.Swordsmen + ship.Plan.Archers
                                && home.Available(ResourceType.Silver) >= World.MusterCost(ship) + Rules.ReserveSilver;
                if (ship.Hull < ship.Definition.Hull * 0.3 || crewSpent)
                {
                    if (ship.Order != OrderKind.Repair) world.Issue(new Command { Faction = faction, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
                    continue;
                }
                if (ship.Order == OrderKind.Repair) continue;
                active.Add(ship);
            }

            var threatened = visibleEnemies.Any(enemy => World.Distance(enemy.X, enemy.Y, home.X, home.Y) < 250);
            if ((Passive || active.Count < minimumAttackGroup) && !threatened) return;

            foreach (var ship in active)
            {
                if ((ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board || ship.Order == OrderKind.AttackShip) && world.FindShip(ship.TargetId) != null) continue;

                var target = visibleEnemies.OrderBy(enemy => World.Distance(ship.X, ship.Y, enemy.X, enemy.Y)).ThenBy(enemy => enemy.Id).FirstOrDefault();
                if (target != null)
                {
                    var kind = ship.Class == ShipClass.Ram ? CommandKind.Ram
                        : ship.Class == ShipClass.Boarder && ship.Soldiers >= Rules.BoardMinSoldiers && World.BoardingOdds(ship, target) > 1.2 ? CommandKind.Board
                        : CommandKind.AttackShip;
                    world.Issue(new Command { Faction = faction, Kind = kind, ShipIds = new[] { ship.Id }, TargetId = target.Id });
                }
                else if (world.CanSee(faction, enemyHome.X, enemyHome.Y) && !enemyHome.Destroyed)
                {
                    if (ship.Order != OrderKind.AttackSettlement) world.Issue(new Command { Faction = faction, Kind = CommandKind.AttackSettlement, ShipIds = new[] { ship.Id }, TargetId = enemyHome.Id });
                }
                else if (ship.Order != OrderKind.Move)
                {
                    world.Issue(new Command { Faction = faction, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = enemyHome.X, Y = enemyHome.Y + (ship.Id % 5 - 2) * 25 });
                }
            }
        }
    }
}
