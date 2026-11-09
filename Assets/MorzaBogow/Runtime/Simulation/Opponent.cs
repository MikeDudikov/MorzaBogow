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
        const int CutterTarget = 6;
        static readonly ShipClass[] BuildCycle = { ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder };

        readonly int faction;
        readonly int minimumAttackGroup;
        int buildIndex;

        public Opponent(int faction, int minimumAttackGroup = 2)
        {
            this.faction = faction;
            this.minimumAttackGroup = minimumAttackGroup;
        }

        public int BuildIndex { get => buildIndex; set => buildIndex = value; }

        public void Think(World world)
        {
            if (world.Tick % StrategyPeriod == 0)
            {
                ManageEconomy(world);
                ManageProduction(world);
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
            if (settlement.PortQueue.Count == 0 && cutters < CutterTarget
                && settlement.Available(ResourceType.Wood) >= cutter.CostWood * Rules.Centi
                && settlement.Available(ResourceType.Silver) >= cutter.CostSilver * Rules.Centi)
            {
                world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
            }

            if (settlement.WarQueue.Count > 0) return;
            var next = ShipCatalog.Get(BuildCycle[buildIndex % BuildCycle.Length]);

            // E04: automatic military orders keep the reserve that protects rebuilding a cutter.
            if (settlement.Available(ResourceType.Wood) < next.CostWood * Rules.Centi + Rules.ReserveWood) return;
            if (settlement.Available(ResourceType.Silver) < next.CostSilver * Rules.Centi + Rules.ReserveSilver) return;
            world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = next.Class });
            buildIndex++;
        }

        void ManageFleet(World world)
        {
            var home = world.SettlementOf(faction);
            var enemyHome = world.SettlementOf(1 - faction);
            if (home == null || enemyHome == null) return;

            var warships = world.Ships.Where(ship => ship.Owner == faction && ship.Class != ShipClass.Cutter).OrderBy(ship => ship.Id).ToList();
            var visibleEnemies = world.Ships.Where(ship => ship.Owner != faction && world.CanSee(faction, ship)).OrderBy(ship => ship.Id).ToList();

            var active = new List<Ship>();
            foreach (var ship in warships)
            {
                if (ship.Hull < ship.Definition.Hull * 0.3)
                {
                    if (ship.Order != OrderKind.Repair) world.Issue(new Command { Faction = faction, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
                    continue;
                }
                if (ship.Order == OrderKind.Repair) continue;
                active.Add(ship);
            }

            var threatened = visibleEnemies.Any(enemy => World.Distance(enemy.X, enemy.Y, home.X, home.Y) < 250);
            if (active.Count < minimumAttackGroup && !threatened) return;

            foreach (var ship in active)
            {
                if ((ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board || ship.Order == OrderKind.AttackShip) && world.FindShip(ship.TargetId) != null) continue;

                var target = visibleEnemies.OrderBy(enemy => World.Distance(ship.X, ship.Y, enemy.X, enemy.Y)).ThenBy(enemy => enemy.Id).FirstOrDefault();
                if (target != null)
                {
                    var kind = ship.Class == ShipClass.Ram ? CommandKind.Ram
                        : ship.Class == ShipClass.Boarder && ship.Soldiers >= Rules.BoardMinSoldiers && target.Soldiers < ship.Soldiers ? CommandKind.Board
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
