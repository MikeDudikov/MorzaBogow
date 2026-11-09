using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>E03 and E05: merchant, transport between local stores, bridgehead, local yard and escort.</summary>
    public sealed class ExpansionTests
    {
        static World Empty()
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            for (var i = 0; i < world.Explored.Length; i++) world.Explored[i] = 3;
            return world;
        }

        static Settlement AddOutpost(World world, double x)
        {
            var outpost = new Settlement { Id = world.NextId++, Owner = 0, X = x, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull };
            world.Settlements.Add(outpost);
            return outpost;
        }

        static BuildSite AddSite(World world, double x)
        {
            var site = new BuildSite { Id = world.NextId++, X = x, Y = 0 };
            world.Sites.Add(site);
            return site;
        }

        static void Run(World world, double seconds)
        {
            for (var i = 0; i < (int)(seconds * Rules.TickRate); i++) world.Step();
        }

        static void Order(World world, CommandKind kind, Ship ship, int target, int source = -1, long wood = 0, long silver = 0)
        {
            world.Issue(new Command { Faction = ship.Owner, Kind = kind, ShipIds = new[] { ship.Id }, TargetId = target, SourceId = source, Wood = wood, Silver = silver });
        }

        // ---------- Merchant ----------

        [Test]
        public void Merchant_MatchesTheTemplateAndCannotGather()
        {
            var merchant = ShipCatalog.Get(ShipClass.Merchant);
            Assert.That(new[] { merchant.CostWood, merchant.CostSilver, merchant.Capacity }, Is.EqualTo(new[] { 140, 60, 480 }));
            Assert.That(merchant.BuildSeconds, Is.EqualTo(28));
            Assert.That(merchant.Speed, Is.EqualTo(7));
            Assert.That(merchant.IsEconomic, Is.True, "It uses the port queue.");
            Assert.That(merchant.CanHarvest, Is.False);

            var world = Empty();
            var node = new ResourceNode { Id = world.NextId++, Type = ResourceType.Wood, X = -450, Y = 0, Stock = 100 * Rules.Centi, KnownMask = 3 };
            world.Nodes.Add(node);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(world, CommandKind.Harvest, ship, node.Id);
            world.Step();
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
        }

        // ---------- Transfer ----------

        [Test]
        public void Transfer_MovesRealGoodsBetweenStoresAndKeepsTheLedger()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -200);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            var silverTotal = world.LedgerTotal(ResourceType.Silver);

            Order(world, CommandKind.Transfer, ship, outpost.Id, home.Id, 300 * Rules.Centi, 100 * Rules.Centi);
            world.Step();
            Assert.That(home.ReservedWood, Is.EqualTo(300 * Rules.Centi), "The source reserves before the ship arrives.");
            Assert.That(home.Wood, Is.EqualTo(2000 * Rules.Centi), "A reservation is part of the real stock, not a new copy.");

            var sawHold = false;
            for (var i = 0; i < 200 * Rules.TickRate && ship.Order != OrderKind.Idle; i++)
            {
                world.Step();
                sawHold |= ship.HoldWood + ship.HoldSilver > 0;
                Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
                Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silverTotal));
            }

            Assert.That(sawHold, Is.True);
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
            Assert.That(outpost.Wood, Is.EqualTo(300 * Rules.Centi));
            Assert.That(outpost.Silver, Is.EqualTo(100 * Rules.Centi));
            Assert.That(home.Wood, Is.EqualTo(1700 * Rules.Centi));
            Assert.That(home.Silver, Is.EqualTo(1400 * Rules.Centi));
            Assert.That(home.ReservedWood + home.ReservedSilver, Is.Zero);
            Assert.That(ship.HoldWood + ship.HoldSilver, Is.Zero);
        }

        [Test]
        public void Transfer_AutomaticLoadFitsTheHoldAndKeepsTheReserve()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            home.Wood = 100 * Rules.Centi;
            home.Silver = 30 * Rules.Centi;
            var outpost = AddOutpost(world, -200);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(world, CommandKind.Transfer, ship, outpost.Id);
            world.Step();
            Assert.That(ship.WantWood, Is.EqualTo(40 * Rules.Centi));
            Assert.That(ship.WantSilver, Is.EqualTo(10 * Rules.Centi));

            var rich = Empty();
            var richOutpost = AddOutpost(rich, -200);
            var other = rich.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(rich, CommandKind.Transfer, other, richOutpost.Id);
            rich.Step();
            Assert.That(other.WantWood + other.WantSilver, Is.EqualTo(480 * Rules.Centi));
        }

        [Test]
        public void Transfer_StopReleasesTheReservationAndLeavesLoadedGoodsOnBoard()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -200);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -500, 0);
            Order(world, CommandKind.Transfer, ship, outpost.Id, home.Id, 300 * Rules.Centi, 100 * Rules.Centi);
            for (var i = 0; i < 60 * Rules.TickRate && ship.HoldWood < 50 * Rules.Centi; i++) world.Step();
            Assert.That(ship.HoldWood, Is.GreaterThanOrEqualTo(50 * Rules.Centi));

            world.Issue(new Command { Faction = 0, Kind = CommandKind.Stop, ShipIds = new[] { ship.Id } });
            world.Step();
            Assert.That(home.ReservedWood + home.ReservedSilver, Is.Zero);
            Assert.That(ship.HoldWood, Is.GreaterThan(0), "Stopping does not refund goods already loaded.");
            Assert.That(home.Wood + ship.HoldWood, Is.EqualTo(2000 * Rules.Centi));
        }

        [Test]
        public void Transfer_FullStoreKeepsTheRestOnTheShip()
        {
            var world = Empty();
            var outpost = AddOutpost(world, -200);
            outpost.Wood = Rules.WarehouseWood - 10 * Rules.Centi;
            outpost.Silver = Rules.WarehouseSilver;
            var ship = world.SpawnShip(0, ShipClass.Merchant, -200, 0);
            ship.HoldWood = 100 * Rules.Centi;
            ship.HoldSilver = 20 * Rules.Centi;
            Order(world, CommandKind.Transfer, ship, outpost.Id);
            Run(world, 30);
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
            Assert.That(outpost.Wood, Is.EqualTo(Rules.WarehouseWood));
            Assert.That(ship.HoldWood, Is.EqualTo(90 * Rules.Centi));
            Assert.That(ship.HoldSilver, Is.EqualTo(20 * Rules.Centi));
            Assert.That(outpost.DockedShips, Is.Empty, "The berth is freed.");
        }

        // ---------- Bridgehead ----------

        [Test]
        public void Expedition_CarriesTheCostAndRaisesABridgeheadWithoutAShipyard()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var site = AddSite(world, -350);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            var silverTotal = world.LedgerTotal(ResourceType.Silver);

            Order(world, CommandKind.FoundOutpost, ship, site.Id);
            world.Step();
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Expedition));
            Assert.That(home.ReservedWood, Is.EqualTo(Rules.OutpostWood));

            for (var i = 0; i < 300 * Rules.TickRate && ship.Order == OrderKind.Expedition; i++)
            {
                world.Step();
                Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
                Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silverTotal));
            }

            var outpost = world.Settlements.Single(item => item.IsOutpost);
            Assert.That(outpost.Owner, Is.EqualTo(0));
            Assert.That(outpost.HasShipyard, Is.False);
            Assert.That(outpost.Wood + outpost.Silver, Is.Zero, "No free stock.");
            Assert.That(site.SettlementId, Is.EqualTo(outpost.Id));
            Assert.That(world.IsSiteFree(site), Is.False);
            Assert.That(home.Wood, Is.EqualTo(2000 * Rules.Centi - Rules.OutpostWood));
            Assert.That(home.Silver, Is.EqualTo(1500 * Rules.Centi - Rules.OutpostSilver));
            Assert.That(world.SpentWood, Is.EqualTo(Rules.OutpostWood));
            Assert.That(world.SpentSilver, Is.EqualTo(Rules.OutpostSilver));
            Assert.That(ship.HoldWood + ship.HoldSilver, Is.Zero);
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Expedition_TakesBuildTimeAfterArrival()
        {
            var world = Empty();
            var site = AddSite(world, -300);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -300, 0);
            ship.HoldWood = Rules.OutpostWood;
            ship.HoldSilver = Rules.OutpostSilver;
            Order(world, CommandKind.FoundOutpost, ship, site.Id);
            Run(world, Rules.OutpostBuildSeconds - 1);
            Assert.That(world.Settlements.Any(item => item.IsOutpost), Is.False);
            Run(world, 2);
            Assert.That(world.Settlements.Count(item => item.IsOutpost), Is.EqualTo(1));
        }

        [Test]
        public void Expedition_IsRejectedForUnknownOccupiedOrUnaffordableShores()
        {
            var world = Empty();
            var hidden = AddSite(world, -300);
            for (var i = 0; i < world.Explored.Length; i++) world.Explored[i] = 0;
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(world, CommandKind.FoundOutpost, ship, hidden.Id);
            world.Step();
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle), "An undiscovered shore cannot be chosen.");

            var poor = Empty();
            poor.SettlementOf(0).Wood = 100 * Rules.Centi;
            var site = AddSite(poor, -300);
            var poorShip = poor.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(poor, CommandKind.FoundOutpost, poorShip, site.Id);
            poor.Step();
            Assert.That(poorShip.Order, Is.EqualTo(OrderKind.Idle), "The full cost must be available.");

            var taken = Empty();
            var occupied = AddSite(taken, -300);
            occupied.SettlementId = AddOutpost(taken, -300).Id;
            var other = taken.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(taken, CommandKind.FoundOutpost, other, occupied.Id);
            taken.Step();
            Assert.That(other.Order, Is.EqualTo(OrderKind.Idle));

            var contested = Empty();
            var shore = AddSite(contested, -300);
            var first = contested.SpawnShip(0, ShipClass.Merchant, -480, 0);
            var second = contested.SpawnShip(0, ShipClass.Merchant, -470, 0);
            Order(contested, CommandKind.FoundOutpost, first, shore.Id);
            Order(contested, CommandKind.FoundOutpost, second, shore.Id);
            contested.Step();
            Assert.That(first.Order, Is.EqualTo(OrderKind.Expedition));
            Assert.That(second.Order, Is.EqualTo(OrderKind.Idle), "One expedition per shore.");
        }

        [Test]
        public void Expedition_SunkLeavesHalfTheCargoAsAWreckAndFreesTheShore()
        {
            var world = Empty();
            var site = AddSite(world, -300);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -700, 400);
            ship.HoldWood = Rules.OutpostWood;
            ship.HoldSilver = Rules.OutpostSilver;
            Order(world, CommandKind.FoundOutpost, ship, site.Id);
            world.Step();
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            var silverTotal = world.LedgerTotal(ResourceType.Silver);

            ship.Hull = 0;
            world.Step();

            var wreck = world.Wrecks.Single();
            Assert.That(wreck.Wood, Is.EqualTo(Rules.OutpostWood / 2));
            Assert.That(wreck.Silver, Is.EqualTo(Rules.OutpostSilver / 2));
            Assert.That(site.BuilderShipId, Is.EqualTo(-1));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silverTotal));
        }

        [Test]
        public void Expedition_SunkWhileLoadingReleasesTheReservation()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var site = AddSite(world, -300);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            Order(world, CommandKind.FoundOutpost, ship, site.Id);
            world.Step();
            Assert.That(home.ReservedWood, Is.GreaterThan(0));
            ship.Hull = 0;
            world.Step();
            Assert.That(home.ReservedWood + home.ReservedSilver, Is.Zero);
        }

        // ---------- Local stores and yards ----------

        [Test]
        public void Cutter_DeliversToItsNearestPort()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -100);
            var node = new ResourceNode { Id = world.NextId++, Type = ResourceType.Wood, X = -60, Y = 0, Stock = 1000 * Rules.Centi, KnownMask = 3 };
            world.Nodes.Add(node);
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -70, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Harvest, ShipIds = new[] { cutter.Id }, TargetId = node.Id });
            Run(world, 60);
            Assert.That(outpost.Wood, Is.GreaterThan(0));
            Assert.That(home.Wood, Is.EqualTo(2000 * Rules.Centi), "The sum is not a remote wallet.");
        }

        [Test]
        public void Outpost_PaysFromItsOwnStoreAndNeedsAShipyardForWarships()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = AddOutpost(world, -200);

            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter, TargetId = outpost.Id });
            world.Step();
            Assert.That(outpost.PortQueue, Is.Empty, "An empty local store cannot pay, whatever the main store holds.");

            outpost.Wood = 1000 * Rules.Centi;
            outpost.Silver = 500 * Rules.Centi;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter, TargetId = outpost.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = outpost.Id });
            world.Step();
            Assert.That(outpost.PortQueue.Count, Is.EqualTo(1));
            Assert.That(outpost.WarQueue, Is.Empty, "No war line without a shipyard.");
            Assert.That(home.ReservedWood, Is.Zero);

            world.Issue(new Command { Faction = 0, Kind = CommandKind.BuildShipyard, TargetId = outpost.Id });
            world.Step();
            Assert.That(outpost.ShipyardBuild, Is.Not.Null);
            Run(world, Rules.ShipyardBuildSeconds + 1);
            Assert.That(outpost.HasShipyard, Is.True);
            Assert.That(outpost.ShipyardBuild, Is.Null);

            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = outpost.Id });
            world.Step();
            Assert.That(outpost.WarQueue.Count, Is.EqualTo(1));
        }

        [Test]
        public void Shipyard_CostsExactlyItsPriceInTheLedger()
        {
            var world = Empty();
            var outpost = AddOutpost(world, -200);
            outpost.Wood = 300 * Rules.Centi;
            outpost.Silver = 150 * Rules.Centi;
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.BuildShipyard, TargetId = outpost.Id });
            Run(world, Rules.ShipyardBuildSeconds + 1);
            Assert.That(outpost.Wood, Is.EqualTo(100 * Rules.Centi));
            Assert.That(outpost.Silver, Is.EqualTo(50 * Rules.Centi));
            Assert.That(outpost.ReservedWood + outpost.ReservedSilver, Is.Zero);
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(woodTotal));
        }

        [Test]
        public void Repair_UsesTheNearestShipyardOnly()
        {
            var world = Empty();
            var outpost = AddOutpost(world, -200);
            outpost.Wood = 1000 * Rules.Centi;
            outpost.Silver = 500 * Rules.Centi;
            var ram = world.SpawnShip(0, ShipClass.Ram, -200, 40);
            var start = world.ClosestYard(ram);
            Assert.That(start, Is.EqualTo(world.SettlementOf(0)), "A bridgehead without a yard repairs nothing.");
            outpost.HasShipyard = true;
            Assert.That(world.ClosestYard(ram), Is.EqualTo(outpost));
        }

        [Test]
        public void Outpost_LossDoesNotEndTheMatchAndFreesTheShore()
        {
            var world = Empty();
            var site = AddSite(world, -300);
            var outpost = AddOutpost(world, -300);
            site.SettlementId = outpost.Id;
            world.SpawnShip(0, ShipClass.Cutter, -480, 0);
            world.SpawnShip(1, ShipClass.Cutter, 480, 0);

            outpost.Hull = 0;
            world.Step();
            Assert.That(world.Outcome, Is.EqualTo(OutcomeKind.InProgress));
            Assert.That(world.IsSiteFree(site), Is.True);
            Assert.That(world.SettlementOf(0).IsOutpost, Is.False);

            world.SettlementOf(0).Hull = 0;
            world.Step();
            Assert.That(world.Outcome, Is.EqualTo(OutcomeKind.Won));
            Assert.That(world.Winner, Is.EqualTo(1));
        }

        // ---------- Escort ----------

        [Test]
        public void Escort_EngagesInsideTheZoneAndReturnsInsteadOfChasing()
        {
            var world = Empty();
            var merchant = world.SpawnShip(0, ShipClass.Merchant, 0, 0);
            var escort = world.SpawnShip(0, ShipClass.CrewDestroyer, -20, 0);
            var enemy = world.SpawnShip(1, ShipClass.Cutter, 95, 0);
            enemy.Hull = 100000;
            Order(world, CommandKind.Escort, escort, merchant.Id);
            var startGap = World.Distance(escort.X, escort.Y, enemy.X, enemy.Y);
            Run(world, 10);
            Assert.That(escort.Order, Is.EqualTo(OrderKind.Escort));
            Assert.That(World.Distance(escort.X, escort.Y, enemy.X, enemy.Y), Is.LessThan(startGap), "A threat inside the zone is engaged.");

            enemy.X = 170;
            for (var i = 0; i < 40 * Rules.TickRate; i++)
            {
                world.Step();
                enemy.X = 170;
                enemy.Speed = 0;
            }
            Assert.That(World.Distance(escort.X, escort.Y, merchant.X, merchant.Y), Is.LessThanOrEqualTo(Rules.EscortStation + 12), "It returns instead of chasing.");
        }

        [Test]
        public void Escort_FollowsATransportAndStopsWhenItIsGone()
        {
            var world = Empty();
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -300, 0);
            var escort = world.SpawnShip(0, ShipClass.Ram, -320, 0);
            Order(world, CommandKind.Escort, escort, merchant.Id);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { merchant.Id }, X = 0, Y = 0 });
            Run(world, 25);
            Assert.That(merchant.X, Is.GreaterThan(-200), "The transport has really moved.");
            Assert.That(World.Distance(escort.X, escort.Y, merchant.X, merchant.Y), Is.LessThan(Rules.EscortStation + 20));

            merchant.Hull = 0;
            world.Step();
            world.Step();
            Assert.That(escort.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Escort_IsRefusedForEconomicShipsAndEnemyTargets()
        {
            var world = Empty();
            var merchant = world.SpawnShip(0, ShipClass.Merchant, 0, 0);
            var cutter = world.SpawnShip(0, ShipClass.Cutter, 20, 0);
            var ram = world.SpawnShip(0, ShipClass.Ram, 40, 0);
            var enemy = world.SpawnShip(1, ShipClass.Merchant, 60, 0);
            Order(world, CommandKind.Escort, cutter, merchant.Id);
            Order(world, CommandKind.Escort, ram, enemy.Id);
            world.Step();
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Idle));
            Assert.That(ram.Order, Is.EqualTo(OrderKind.Idle));
        }

        // ---------- Map, save and the opponent ----------

        [Test]
        public void Map_FreeShoresAreMirroredAndTheirPortsReachable()
        {
            var world = MapFactory.CreateSkirmish(1);
            Assert.That(world.Sites.Count, Is.EqualTo(8));
            foreach (var site in world.Sites)
            {
                Assert.That(world.Sites.Any(other => other.X == -site.X && other.Y == site.Y), Is.True, "Mirrored for the other side.");
                Assert.That(world.Sites.Any(other => other.X == site.X && other.Y == -site.Y), Is.True);
                foreach (var island in world.Islands)
                {
                    var reach = island.Radius + ShipCatalog.Get(ShipClass.Merchant).Radius - Rules.PortRange;
                    Assert.That(World.Distance(site.X, site.Y, island.X, island.Y), Is.GreaterThanOrEqualTo(reach), $"Site {site.Id} vs {island.Kind} land.");
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Map_AMerchantFromEitherBaseCanRaiseABridgeheadOnEveryShore(int owner)
        {
            for (var index = 0; index < 8; index++)
            {
                var world = MapFactory.CreateSkirmish(1);
                world.Ships.Clear();
                for (var i = 0; i < world.Explored.Length; i++) world.Explored[i] = 3;
                var home = world.SettlementOf(owner);
                var side = home.X < 0 ? 1 : -1;
                var ship = world.SpawnShip(owner, ShipClass.Merchant, home.X + side * 45, home.Y);
                var site = world.Sites[index];
                Order(world, CommandKind.FoundOutpost, ship, site.Id);
                world.Step();
                for (var i = 0; i < 400 * Rules.TickRate && ship.Order == OrderKind.Expedition; i++) world.Step();
                Assert.That(world.IsSiteFree(site), Is.False, $"Owner {owner} shore {index} at {site.X},{site.Y}: ship at {ship.X:0},{ship.Y:0}, {ship.Transport}.");
            }
        }

        [TestCase(ShipClass.Ram)]
        [TestCase(ShipClass.Boarder)]
        public void Map_ADamagedWarshipReachesAYardBesideItsLand(ShipClass shipClass)
        {
            var world = MapFactory.CreateSkirmish(1);
            world.Ships.Clear();
            var home = world.SettlementOf(0);
            var ship = world.SpawnShip(0, shipClass, 0, 110);
            ship.Hull = ship.Definition.Hull / 2;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            for (var i = 0; i < 300 * Rules.TickRate && home.RepairShipId != ship.Id; i++) world.Step();
            Assert.That(home.RepairShipId, Is.EqualTo(ship.Id), $"Ship at {ship.X:0},{ship.Y:0}.");
        }

        [Test]
        public void Map_ACutterReachesEveryWorkPointAndABerth()
        {
            var nodes = MapFactory.CreateSkirmish(1).Nodes.Count;
            for (var index = 0; index < nodes; index++)
            {
                var world = MapFactory.CreateSkirmish(1);
                world.Ships.Clear();
                var home = world.SettlementOf(0);
                var node = world.Nodes[index];
                node.KnownMask = 3;
                var ship = world.SpawnShip(0, ShipClass.Cutter, home.X + 45, home.Y);
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Harvest, ShipIds = new[] { ship.Id }, TargetId = node.Id });
                var worked = false;
                for (var i = 0; i < 300 * Rules.TickRate && !worked; i++)
                {
                    world.Step();
                    worked = ship.Harvest == HarvestPhase.Working;
                }
                Assert.That(worked, Is.True, $"Node {index} at {node.X},{node.Y}: ship at {ship.X:0},{ship.Y:0}.");
            }
        }

        [Test]
        public void Save_RestoresAnExpeditionMidWayAndContinuesIdentically()
        {
            var world = Empty();
            var site = AddSite(world, -350);
            var ship = world.SpawnShip(0, ShipClass.Merchant, -480, 0);
            var outpost = AddOutpost(world, -100);
            outpost.Wood = 500 * Rules.Centi;
            outpost.Silver = 300 * Rules.Centi;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.BuildShipyard, TargetId = outpost.Id });
            var escort = world.SpawnShip(0, ShipClass.Ram, -470, 30);
            Order(world, CommandKind.Escort, escort, ship.Id);
            Order(world, CommandKind.FoundOutpost, ship, site.Id);
            Run(world, 40);
            Assert.That(ship.HoldWood + ship.HoldSilver, Is.GreaterThan(0), "Saved with goods on board.");

            var data = SaveGame.Serialize(world);
            var loaded = SaveGame.Deserialize(data, out _);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));

            Run(world, 60);
            Run(loaded, 60);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(SaveGame.Serialize(world)));
            Assert.That(world.Settlements.Count(item => item.IsOutpost), Is.EqualTo(2), "The saved expedition finished.");
        }

        [Test]
        public void Opponent_ExpandsWithRealCostsAndKeepsTheLedger()
        {
            var world = MapFactory.CreateSkirmish(9);
            var first = new Opponent(0);
            var second = new Opponent(1);
            var woodTotal = world.LedgerTotal(ResourceType.Wood);
            var silverTotal = world.LedgerTotal(ResourceType.Silver);
            var merchants = 0;
            for (var tick = 0; tick < 12000 && world.Outcome == OutcomeKind.InProgress; tick++)
            {
                first.Think(world);
                second.Think(world);
                world.Step();
                world.Events.Clear();
                merchants = System.Math.Max(merchants, world.Ships.Count(ship => ship.Class == ShipClass.Merchant));
                if (world.LedgerTotal(ResourceType.Wood) != woodTotal || world.LedgerTotal(ResourceType.Silver) != silverTotal) Assert.Fail($"Ledger changed at tick {tick}.");
                foreach (var port in world.Settlements)
                {
                    if (port.Wood < 0 || port.Silver < 0 || port.Available(ResourceType.Wood) < 0 || port.Available(ResourceType.Silver) < 0) Assert.Fail($"Negative store at tick {tick}.");
                }
            }
            TestContext.WriteLine($"Merchants {merchants}, outposts {world.Settlements.Count(item => item.IsOutpost)}, tick {world.Tick}, outcome {world.Outcome}");
            Assert.That(merchants, Is.GreaterThan(0), "The opponent builds a merchant.");
            Assert.That(world.Settlements.Count(item => item.IsOutpost), Is.GreaterThan(0), "The opponent founds a bridgehead.");
        }
    }
}
