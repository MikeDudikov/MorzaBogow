using System.Collections.Generic;
using System.Linq;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Rule-following opponent (C08): it only issues normal commands, pays normal costs and reads
    /// enemy ships through the same visibility check as the player. M1 scope: economy, production,
    /// attack groups and retreat; it knows the enemy start position as map knowledge.
    /// </summary>
    public sealed class Opponent
    {
        const int StrategyPeriod = 3 * Rules.TickRate;
        const int TacticsPeriod = Rules.TickRate;
        static readonly ShipClass[] BuildCycle = { ShipClass.CrewDestroyer, ShipClass.Ram, ShipClass.Boarder };

        readonly int faction;
        readonly int minimumAttackGroup;
        int buildIndex;

        public Opponent(int faction, int minimumAttackGroup = 3)
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
            var wanted = settlement.Rations * 2 < settlement.Bronze ? ResourceType.Rations : ResourceType.Bronze;
            var cutters = world.Ships.Where(ship => ship.Owner == faction && ship.Class == ShipClass.Cutter).OrderBy(ship => ship.Id).ToList();
            var assignedToWanted = cutters.Count(ship => ship.Order == OrderKind.Harvest && world.FindNode(ship.NodeId)?.Type == wanted);

            foreach (var cutter in cutters)
            {
                // Rebalance only ships that just unloaded, and keep at least half on the scarcer resource.
                var rebalance = cutter.Order == OrderKind.Harvest && cutter.Harvest == HarvestPhase.ToNode && cutter.Cargo == 0
                                && world.FindNode(cutter.NodeId)?.Type != wanted && assignedToWanted * 2 < cutters.Count;
                if (cutter.Order != OrderKind.Idle && !rebalance) continue;
                if (rebalance) assignedToWanted++;

                var node = world.Nodes
                    .Where(item => (item.KnownMask & (1 << faction)) != 0 && item.Stock > 0 && !item.Closed)
                    .OrderBy(item => item.Type == wanted ? 0 : 1)
                    .ThenBy(item => World.Distance(cutter.X, cutter.Y, item.X, item.Y))
                    .ThenBy(item => item.Id)
                    .FirstOrDefault();
                if (node != null) world.Issue(new Command { Faction = faction, Kind = CommandKind.Harvest, ShipIds = new[] { cutter.Id }, TargetId = node.Id });
            }
        }

        void ManageProduction(World world)
        {
            var settlement = world.SettlementOf(faction);
            if (settlement == null || settlement.Destroyed || settlement.Queue.Count >= 2) return;

            var cutters = world.Ships.Count(ship => ship.Owner == faction && ship.Class == ShipClass.Cutter);
            if (cutters < 4 && settlement.Bronze >= world.HullCostBronze(ShipClass.Cutter))
            {
                world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
                return;
            }

            var next = BuildCycle[buildIndex % BuildCycle.Length];
            if (settlement.Bronze < world.HullCostBronze(next) || settlement.Rations < world.HullCostRations(next)) return;
            world.Issue(new Command { Faction = faction, Kind = CommandKind.QueueShip, Class = next });
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
                    if (ship.Order != OrderKind.Move) world.Issue(new Command { Faction = faction, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = home.X + (faction == 0 ? 60 : -60), Y = home.Y });
                    continue;
                }
                active.Add(ship);
            }

            var threatened = visibleEnemies.Any(enemy => World.Distance(enemy.X, enemy.Y, home.X, home.Y) < 300);
            if (active.Count < minimumAttackGroup && !threatened) return;

            foreach (var ship in active)
            {
                if (ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board) { if (world.FindShip(ship.TargetId) != null) continue; }
                if (ship.Order == OrderKind.AttackShip && world.FindShip(ship.TargetId) != null) continue;

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
