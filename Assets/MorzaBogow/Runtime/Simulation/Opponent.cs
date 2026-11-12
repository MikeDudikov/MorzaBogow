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
        static readonly ShipClass[] BuildCycle = { ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.ShipKiller };

        /// <summary>Every this many warships one is a bomber, once the port may build it (docs/UZBROJENIE.md).</summary>
        const int BomberEvery = 5;

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
                ManageBuildings(world);
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
            var salvaging = ManageSalvage(world, cutters);

            foreach (var cutter in cutters)
            {
                if (salvaging.Contains(cutter.Id)) continue;
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

        /// <summary>A wreck this close to one of the opponent's ports is worth a cutter's trip, with no enemy seen this close to it.</summary>
        const double SalvageReach = 400, SalvageThreat = 200;

        /// <summary>
        /// E06, E09: a wreck the opponent sees near one of its ports, with no enemy seen beside it, gets the nearest free
        /// cutter: idle, or harvesting and just unloaded. One cutter a wreck; the others keep harvesting. Returns the
        /// cutters sent, so the harvest orders of this pass leave them alone.
        /// </summary>
        HashSet<int> ManageSalvage(World world, List<Ship> cutters)
        {
            var sent = new HashSet<int>();
            var ports = world.PortsOf(faction).ToList();
            var enemies = world.Ships.Where(ship => ship.Owner != faction && world.CanSee(faction, ship)).ToList();
            foreach (var wreck in world.Wrecks.OrderBy(item => item.Id))
            {
                if (wreck.Empty || !world.CanSee(faction, wreck.X, wreck.Y)) continue;
                if (!ports.Any(port => World.Distance(port.X, port.Y, wreck.X, wreck.Y) <= SalvageReach)) continue;
                if (enemies.Any(enemy => World.Distance(enemy.X, enemy.Y, wreck.X, wreck.Y) <= SalvageThreat)) continue;
                if (cutters.Any(ship => ship.WreckId == wreck.Id)) continue;

                var free = cutters
                    .Where(ship => !sent.Contains(ship.Id) && (ship.Order == OrderKind.Idle
                        || (ship.Order == OrderKind.Harvest && ship.Harvest == HarvestPhase.ToNode && ship.Cargo == 0 && ship.WreckId < 0)))
                    .OrderBy(ship => World.Distance(ship.X, ship.Y, wreck.X, wreck.Y)).ThenBy(ship => ship.Id)
                    .FirstOrDefault();
                if (free == null) break;
                world.Issue(new Command { Faction = faction, Kind = CommandKind.Salvage, ShipIds = new[] { free.Id }, TargetId = wreck.Id });
                sent.Add(free.Id);
            }
            return sent;
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

        /// <summary>
        /// From about the eighth minute, one building at a time at the home port (NextBuilding): the workshop, the town and
        /// its dock (without a dock no hull is mended, docs/UZGODNIENIE_SPECYFIKACJI.md), the Sages' School and the heavy-arms
        /// research for the bomber (docs/UZBROJENIE.md). Paid like the player's, with 150 S left in store; the warship line
        /// saves its price meanwhile (BuildingSaving).
        /// </summary>
        void ManageBuildings(World world)
        {
            var home = world.SettlementOf(faction);
            var next = NextBuilding(world, home);
            if (next == ProjectKind.None) return;
            var (_, silver, _) = World.ProjectCost(next);
            if (home.Available(ResourceType.Silver) < silver + 150 * Rules.Centi) return;
            if (world.ProjectProblem(home, next) != null) return;
            world.Issue(new Command { Faction = faction, Kind = CommandKind.BuildProject, Project = next, TargetId = home.Id });
        }

        const int BuildingsFromTick = 8 * 60 * Rules.TickRate;

        /// <summary>
        /// The building due next at home: the workshop, the town, the dock, the Sages' School, then the heavy-arms research
        /// that opens the bomber; none before the eighth minute or while one rises.
        /// </summary>
        ProjectKind NextBuilding(World world, Settlement home)
        {
            if (home == null || home.Destroyed || home.Project != null || world.Tick < BuildingsFromTick) return ProjectKind.None;
            return !home.HasWorkshop ? ProjectKind.Workshop : !home.IsTown ? ProjectKind.Town : !home.HasDock ? ProjectKind.Dock
                : !home.HasSageSchool ? ProjectKind.SageSchool : !world.HasHeavyArms(faction) ? ProjectKind.HeavyArmsResearch : ProjectKind.None;
        }

        /// <summary>The next warship of the cycle; every BomberEvery-th a bomber where the port may build one.</summary>
        ShipClass NextWarship(World world, Settlement port) =>
            buildIndex % BomberEvery == BomberEvery - 1 && world.ShipProblem(port, ShipClass.Bomber) == null
                ? ShipClass.Bomber
                : BuildCycle[buildIndex % BuildCycle.Length];

        /// <summary>
        /// The price of the building due at home, kept from the warship line so the line does not reserve every silver
        /// piece first (the dock alone mends hulls). Not while the base is threatened: warships come first then.
        /// </summary>
        (long wood, long silver) BuildingSaving(World world, Settlement home)
        {
            var next = NextBuilding(world, home);
            if (next == ProjectKind.None) return (0, 0);
            if (world.Ships.Any(ship => ship.Owner != faction && world.CanSee(faction, ship) && World.Distance(ship.X, ship.Y, home.X, home.Y) < 250)) return (0, 0);
            var (wood, silver, _) = World.ProjectCost(next);
            return (wood, silver);
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
                var next = ShipCatalog.Get(NextWarship(world, port));

                // A waiting merchant's cargo is paid for first, and so is the building due at home; otherwise warships
                // would spend every saving.
                var saved = port == settlement ? Saving(world, settlement) : (wood: 0L, silver: 0L);
                var building = port == settlement ? BuildingSaving(world, settlement) : (wood: 0L, silver: 0L);
                var savedWood = saved.wood + building.wood;
                var savedSilver = saved.silver + building.silver;

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
                // A badly holed hull goes to the dock when the side has one (only a dock mends); a crew with half its
                // fighters gone (swordsmen, javelin men and archers alike, as aboard so in the plan) goes to regroup,
                // only when the yard it would sail to can pay the muster now: a ship waiting in port for silver is lost
                // to the fleet. A ship locked in a boarding fights it out: regrouping would cast off the hooks.
                var grappled = world.Boardings.Any(link => link.AttackerId == ship.Id || link.TargetId == ship.Id);
                var holed = ship.Hull < ship.Definition.Hull * 0.3 && world.HasDock(faction);
                var yard = world.RepairYard(ship);
                var crewSpent = ship.Plan.Soldiers > 0 && ship.Soldiers * 2 < ship.Plan.Soldiers
                                && yard != null && yard.Available(ResourceType.Silver) >= World.MusterCost(ship) + Rules.ReserveSilver;
                if (!grappled && (holed || crewSpent))
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
