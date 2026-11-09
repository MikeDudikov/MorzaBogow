using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    public sealed class SimulationTests
    {
        static World Empty(ulong seed = 7)
        {
            var world = new World(seed);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            return world;
        }

        static ResourceNode AddNode(World world, ResourceType type, double x, double y, long units)
        {
            var node = new ResourceNode { Id = world.NextId++, Type = type, X = x, Y = y, Stock = units * Rules.Centi, KnownMask = 3 };
            world.Nodes.Add(node);
            return node;
        }

        static void Run(World world, double seconds)
        {
            var ticks = (int)(seconds * Rules.TickRate);
            for (var i = 0; i < ticks; i++) world.Step();
        }

        static void Harvest(World world, Ship ship, ResourceNode node) =>
            world.Issue(new Command { Faction = ship.Owner, Kind = CommandKind.Harvest, ShipIds = new[] { ship.Id }, TargetId = node.Id });

        static Ship Loaded(World world, int owner, double x, ResourceType type, long units)
        {
            var ship = world.SpawnShip(owner, ShipClass.Cutter, x, 0);
            ship.Order = OrderKind.Harvest;
            ship.Harvest = HarvestPhase.ToPort;
            ship.CargoType = type;
            ship.Cargo = units * Rules.Centi;
            return ship;
        }

        // ---------- Gathering (E02) ----------

        [Test]
        public void Harvest_WoodReachesStoreOnlyAfterRealDelivery()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var node = AddNode(world, ResourceType.Wood, -420, 0, 3000);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            var start = settlement.Wood;
            Harvest(world, ship, node);

            Run(world, 10);
            Assert.That(ship.Cargo, Is.GreaterThan(0));
            Assert.That(settlement.Wood, Is.EqualTo(start), "Cargo in a hold is not spendable.");

            Run(world, 40);
            Assert.That(settlement.Wood, Is.GreaterThan(start));
        }

        [Test]
        public void Income_MatchesTheModelCycleWithinTolerance()
        {
            // The model gives about 124 D/min at 70 m without manoeuvre detail. Real motion must stay in its range.
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var node = AddNode(world, ResourceType.Wood, -430, 0, 3000);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            Harvest(world, ship, node);
            Run(world, 60);
            var start = settlement.Wood;
            Run(world, 300);
            var perMinute = (settlement.Wood - start) / (double)Rules.Centi / 5.0;
            TestContext.WriteLine($"Delivered {perMinute:0.0} D/min");
            Assert.That(perMinute, Is.InRange(80.0, 124.2));
        }

        [Test]
        public void Node_HasTwoBerthsAndMakesOthersWaitFifo()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Wood, 0, 0, 3000);
            var ships = Enumerable.Range(0, 4).Select(i => world.SpawnShip(0, ShipClass.Cutter, i, 0)).ToArray();
            foreach (var ship in ships) Harvest(world, ship, node);
            Run(world, 1);
            Assert.That(node.Workers, Is.EqualTo(new[] { ships[0].Id, ships[1].Id }));
            Assert.That(node.Waiting, Is.EqualTo(new[] { ships[2].Id, ships[3].Id }));
            Assert.That(ships[2].Cargo + ships[3].Cargo, Is.EqualTo(0), "Waiting ships do not take work.");
        }

        [Test]
        public void Node_WoodLimitIsTwelvePerSecondTimesTheScenarioYield()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Wood, 0, 0, 3000);
            var ships = Enumerable.Range(0, 4).Select(i => world.SpawnShip(0, ShipClass.Cutter, i, 0)).ToArray();
            foreach (var ship in ships) Harvest(world, ship, node);
            Run(world, 1 + Rules.LandCrewSeconds);
            var before = node.Stock;
            Run(world, 4);
            var perSecond = (before - node.Stock) / (double)Rules.Centi / 4.0;
            Assert.That(perSecond, Is.EqualTo(Rules.WoodNodeCap * world.Profile.WoodYield).Within(0.1));
        }

        [Test]
        public void Node_SilverLimitIsSharedProportionally()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Silver, 0, 0, 1000);
            var ships = Enumerable.Range(0, 2).Select(i => world.SpawnShip(0, ShipClass.Cutter, i, 0)).ToArray();
            foreach (var ship in ships) Harvest(world, ship, node);
            Run(world, 1 + Rules.LandCrewSeconds);
            var before = node.Stock;
            Run(world, 5);
            Assert.That((before - node.Stock) / (double)Rules.Centi / 5.0, Is.EqualTo(Rules.SilverNodeCap * world.Profile.SilverYield).Within(0.1));
            Assert.That(ships[0].Cargo, Is.EqualTo(ships[1].Cargo).Within(5));
        }

        [Test]
        public void Node_LastUnitsGiveAPartialLoadAndNoSecondCopy()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Wood, 0, 0, 25);
            var first = world.SpawnShip(0, ShipClass.Cutter, 0, 0);
            var second = world.SpawnShip(0, ShipClass.Cutter, 1, 0);
            Harvest(world, first, node);
            Harvest(world, second, node);
            var total = node.Stock;
            // Landing, then 25 units at the scenario's yield shared by two hulls.
            Run(world, Rules.LandCrewSeconds + 25 / world.NodeCap(node) + 2);
            Assert.That(node.Stock, Is.EqualTo(0));
            Assert.That(first.Cargo + second.Cargo, Is.EqualTo(total));
            Assert.That(first.Harvest, Is.Not.EqualTo(HarvestPhase.Working));
        }

        [Test]
        public void Cutter_LandsItsCrewBeforeWorkAndWaitsForThemBeforeCastingOff()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Wood, -430, 0, 3000);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -440, 0);
            Harvest(world, ship, node);
            var phases = new List<(HarvestPhase phase, int tick, long cargo)>();
            for (var i = 0; i < 40 * Rules.TickRate; i++)
            {
                world.Step();
                if (phases.Count == 0 || phases[phases.Count - 1].phase != ship.Harvest) phases.Add((ship.Harvest, world.Tick, ship.Cargo));
                if (ship.Harvest == HarvestPhase.Landing) Assert.That(ship.Cargo, Is.EqualTo(0), "Nothing is gathered while the crew lands.");
                if (ship.Harvest == HarvestPhase.Embarking)
                {
                    Assert.That(Math.Abs(ship.Speed), Is.LessThan(0.5), "It waits for its crew instead of casting off.");
                    Assert.That(node.Workers, Does.Contain(ship.Id), "It keeps its berth until all are aboard.");
                }
            }

            var order = phases.Select(item => item.phase).ToList();
            var landing = order.IndexOf(HarvestPhase.Landing);
            Assert.That(order.Skip(landing).Take(4), Is.EqualTo(new[] { HarvestPhase.Landing, HarvestPhase.Working, HarvestPhase.Embarking, HarvestPhase.ToPort }));
            int TickOf(HarvestPhase phase) => phases.First(item => item.phase == phase).tick;
            Assert.That(TickOf(HarvestPhase.Working) - TickOf(HarvestPhase.Landing), Is.EqualTo((int)(Rules.LandCrewSeconds * Rules.TickRate)));
            Assert.That(TickOf(HarvestPhase.ToPort) - TickOf(HarvestPhase.Embarking), Is.EqualTo((int)(Rules.EmbarkSeconds * Rules.TickRate)));
            Assert.That(phases.First(item => item.phase == HarvestPhase.ToPort).cargo, Is.EqualTo(ship.Definition.Capacity * Rules.Centi), "It leaves with a full hold.");
        }

        [Test]
        public void Shipyard_LaunchesEachHullOffItsSlipwayWithoutOverlap()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            world.Islands.Add(new Island { X = Rules.SettlementLandX(home), Y = home.Y, Radius = Rules.SettlementLandRadius, Kind = IslandKind.Settlement });
            home.Wood = home.Silver = 100000 * Rules.Centi;
            for (var i = 0; i < 4; i++)
            {
                world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
                world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            }

            var launched = new List<(Ship ship, bool war)>();
            for (var i = 0; i < 200 * Rules.TickRate; i++)
            {
                world.Step();
                foreach (var built in world.Events.Where(item => item.Type == "ship_built"))
                {
                    var ship = world.FindShip(built.SubjectId);
                    launched.Add((ship, built.Amount == 1));
                    foreach (var other in world.Ships.Where(item => item != ship))
                    {
                        Assert.That(World.HullGap(ship, other), Is.GreaterThan(World.HullSpacing), $"{ship.Class} launched onto {other.Class}.");
                    }
                    var landSide = ship.Y - home.Y;
                    Assert.That(Math.Sign(landSide), Is.EqualTo(built.Amount == 1 ? -1 : 1), "Each line launches off its own side of the jetty.");
                    Assert.That(Math.Sin(ship.Heading) * Math.Sign(landSide), Is.GreaterThan(0.99), "Bow to the sea, away from the land.");
                }
                world.Events.Clear();
            }
            Assert.That(launched.Count(item => item.war), Is.EqualTo(4));
            Assert.That(launched.Count(item => !item.war), Is.EqualTo(4));
        }

        [Test]
        public void Nodes_BothResourcesAreFinite()
        {
            var world = Empty();
            var wood = AddNode(world, ResourceType.Wood, 0, 0, 100);
            var silver = AddNode(world, ResourceType.Silver, 50, 0, 100);
            Run(world, 300);
            Assert.That(wood.Stock, Is.EqualTo(100 * Rules.Centi));
            Assert.That(silver.Stock, Is.EqualTo(100 * Rules.Centi));
        }

        // ---------- Stores and port (E03) ----------

        [Test]
        public void Port_ServesThreeShipsAndMakesTheFourthWait()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ships = Enumerable.Range(0, 4).Select(i => Loaded(world, 0, settlement.X + 5 + i, ResourceType.Wood, 60)).ToArray();
            Run(world, 0.2);
            Assert.That(settlement.DockedShips.Count, Is.EqualTo(Rules.PortBerths));
            Assert.That(settlement.PortWaiting, Is.EqualTo(new[] { ships[3].Id }));
        }

        [Test]
        public void Port_PartialUnloadOfSixtyPerSecond()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ship = Loaded(world, 0, settlement.X, ResourceType.Wood, 60);
            var start = settlement.Wood;
            Run(world, 0.5);
            Assert.That(settlement.Wood - start, Is.GreaterThan(0));
            Assert.That(ship.Cargo, Is.GreaterThan(0));
            Assert.That(settlement.Wood - start + ship.Cargo, Is.EqualTo(60 * Rules.Centi));
        }

        [Test]
        public void Store_FullKeepsCargoOnTheShipAndFreesTheBerth()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.Wood = Rules.WarehouseWood;
            var ship = Loaded(world, 0, settlement.X, ResourceType.Wood, 50);
            Run(world, 1);
            Assert.That(ship.Cargo, Is.EqualTo(50 * Rules.Centi));
            Assert.That(settlement.Wood, Is.EqualTo(Rules.WarehouseWood));
            Assert.That(settlement.DockedShips, Is.Empty);
            Assert.That(world.Events.Count(item => item.Type == "store_full"), Is.EqualTo(1));
        }

        [Test]
        public void Store_SilverLimitIsThreeThousand()
        {
            Assert.That(new Settlement().Capacity(ResourceType.Silver), Is.EqualTo(3000 * Rules.Centi));
            Assert.That(new Settlement().Capacity(ResourceType.Wood), Is.EqualTo(6000 * Rules.Centi));
        }

        // ---------- Production (E04) ----------

        [TestCase(ShipClass.Cutter, 60, 20, 4.0, 48.0, 6.40, 12.0, 9.60)]
        [TestCase(ShipClass.Ram, 240, 100, 31.5, 192.0, 27.40, 48.0, 41.10)]
        [TestCase(ShipClass.CrewDestroyer, 180, 90, 45.5, 144.0, 17.80, 36.0, 26.70)]
        [TestCase(ShipClass.Boarder, 200, 180, 78.5, 160.0, 40.60, 40.0, 60.90)]
        public void Template_SplitMatchesTheCostTable(ShipClass shipClass, int wood, int silver, double crew, double conWood, double conSilver, double eqWood, double eqSilver)
        {
            var definition = ShipCatalog.Get(shipClass);
            Assert.That(definition.CostWood, Is.EqualTo(wood));
            Assert.That(definition.CostSilver, Is.EqualTo(silver));
            Assert.That(TemplatePricing.CrewSilver(definition) / 100.0, Is.EqualTo(crew).Within(0.001));
            Assert.That(TemplatePricing.ConstructionWood(definition) / 100.0, Is.EqualTo(conWood).Within(0.001));
            Assert.That(TemplatePricing.ConstructionSilver(definition) / 100.0, Is.EqualTo(conSilver).Within(0.001));
            Assert.That(TemplatePricing.EquipmentWood(definition) / 100.0, Is.EqualTo(eqWood).Within(0.001));
            Assert.That(TemplatePricing.EquipmentSilver(definition) / 100.0, Is.EqualTo(eqSilver).Within(0.001));
        }

        [Test]
        public void Queue_ChargesTemplateExactlyOnceWithProgress()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var wood = settlement.Wood;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Step();
            Assert.That(settlement.ReservedWood, Is.LessThanOrEqualTo(240 * Rules.Centi));
            Assert.That(settlement.Available(ResourceType.Wood), Is.EqualTo(wood - 240 * Rules.Centi), "Reserved in full at once.");
            Assert.That(settlement.Wood, Is.GreaterThan(wood - 240 * Rules.Centi), "Used up with progress.");
            Run(world, 34);
            Assert.That(world.Ships.Count(ship => ship.Class == ShipClass.Ram), Is.EqualTo(0));
            Run(world, 2);
            Assert.That(world.Ships.Count(ship => ship.Class == ShipClass.Ram), Is.EqualTo(1));
            Assert.That(settlement.Wood, Is.EqualTo(wood - 240 * Rules.Centi));
            Assert.That(settlement.ReservedWood, Is.EqualTo(0));
        }

        [Test]
        public void Queue_CancelQuoteIsExactlyWhatCancellingDoes()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            Run(world, 22);

            var waiting = world.QuoteCancel(settlement, false, 1);
            Assert.That(waiting.Started, Is.False);
            Assert.That(waiting.LostWood + waiting.LostSilver, Is.EqualTo(0), "Nothing is lost before work starts.");

            var quote = world.QuoteCancel(settlement, false, 0);
            Assert.That(quote.Valid && quote.Started, Is.True);
            Assert.That(quote.Progress, Is.GreaterThan(0.5), "The advanced case that needs a confirmation.");
            Assert.That(quote.LostWood, Is.GreaterThan(0));
            var wood = settlement.Wood;
            var silver = settlement.Silver;
            var reservedWood = settlement.ReservedWood;
            var reservedSilver = settlement.ReservedSilver;
            var quoteAgain = world.QuoteCancel(settlement, false, 0);
            Assert.That(quoteAgain.ReleasedWood, Is.EqualTo(quote.ReleasedWood), "A quote changes nothing.");

            world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 0, PortQueue = false, TargetId = settlement.Id });
            world.Step();
            // One more tick of work on the next order may follow the cancel; compare with what that tick used.
            var next = settlement.WarQueue[0];
            Assert.That(reservedWood - settlement.ReservedWood - next.UsedWood, Is.EqualTo(quote.ReleasedWood).Within(0));
            Assert.That(wood - settlement.Wood - next.UsedWood, Is.EqualTo(quote.LostWood));
            Assert.That(silver - settlement.Silver - next.UsedSilver, Is.EqualTo(quote.LostSilver));
            Assert.That(reservedSilver - settlement.ReservedSilver - next.UsedSilver, Is.EqualTo(quote.ReleasedSilver));
        }

        [Test]
        public void Queue_CompetingOrdersCannotReserveTheSameResource()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.Wood = 300 * Rules.Centi;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Step();
            Assert.That(settlement.WarQueue.Count, Is.EqualTo(1));
            Assert.That(settlement.Available(ResourceType.Wood), Is.EqualTo(60 * Rules.Centi));
        }

        [Test]
        public void Queue_WarLineAndPortQueueRunTogether()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
            Run(world, 1);
            Assert.That(settlement.WarQueue[0].DoneTicks, Is.GreaterThan(0));
            Assert.That(settlement.PortQueue[0].DoneTicks, Is.GreaterThan(0));
        }

        [Test]
        public void Queue_WarLineBuildsOneShipAtATime()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.CrewDestroyer });
            Run(world, 1);
            Assert.That(settlement.WarQueue[1].Started, Is.False);
        }

        [Test]
        public void Cancel_BeforeStartReleasesEverything()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var wood = settlement.Available(ResourceType.Wood);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 1 });
            world.Step();
            Assert.That(settlement.WarQueue.Count, Is.EqualTo(1));
            Assert.That(settlement.Available(ResourceType.Wood), Is.EqualTo(wood - 240 * Rules.Centi));
        }

        [Test]
        public void Cancel_AfterStartReturnsEightyPercentOfTheUnusedPart()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var wood = settlement.Wood;
            settlement.ReservedWood = 60 * Rules.Centi;
            settlement.Wood = wood - 40 * Rules.Centi;
            settlement.WarQueue.Add(new BuildOrder { Class = ShipClass.Ram, PaidWood = 100 * Rules.Centi, UsedWood = 40 * Rules.Centi, TotalTicks = 100, DoneTicks = 40, Started = true });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 0 });
            world.Step();
            Assert.That(settlement.ReservedWood, Is.EqualTo(0));
            Assert.That(settlement.Wood, Is.EqualTo(wood - 40 * Rules.Centi - 12 * Rules.Centi), "40 used, 60 unused, 48 back, 12 lost.");
        }

        [Test]
        public void Queue_RejectsOrderThatLocalStoreCannotPay()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.Silver = 10 * Rules.Centi;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
            world.Step();
            Assert.That(settlement.PortQueue, Is.Empty);
            Assert.That(settlement.ReservedWood, Is.EqualTo(0));
        }

        [Test]
        public void Crew_EconomicHasSailorsOnlyAndWarshipFullCrew()
        {
            var world = Empty();
            var cutter = world.SpawnShip(0, ShipClass.Cutter, 0, 0);
            var ram = world.SpawnShip(0, ShipClass.Ram, 0, 50);
            Assert.That(cutter.Sailors, Is.EqualTo(8));
            Assert.That(cutter.Soldiers, Is.EqualTo(0));
            Assert.That(ram.Sailors, Is.EqualTo(9));
            Assert.That(ram.Soldiers, Is.EqualTo(27));
        }

        // ---------- Repair, scrap, wreck (E06) ----------

        [Test]
        public void Repair_ChargesAQuarterOfConstructionForFullHull()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            ship.Hull = 380;
            var wood = settlement.Wood;
            var silver = settlement.Silver;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            Run(world, 40);
            Assert.That(ship.Hull, Is.EqualTo(760).Within(0.01));
            Assert.That((wood - settlement.Wood) / 100.0, Is.EqualTo(192 * 0.25 * 0.5).Within(0.05));
            Assert.That((silver - settlement.Silver) / 100.0, Is.EqualTo(27.4 * 0.25 * 0.5).Within(0.05));
        }

        [Test]
        public void Repair_RunsAtTwelveHullPerSecondOnOneStation()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var first = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            var second = world.SpawnShip(0, ShipClass.Ram, settlement.X, 5);
            first.Hull = second.Hull = 100;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { first.Id, second.Id } });
            Run(world, 10);
            Assert.That(first.Hull, Is.EqualTo(100 + Rules.RepairHullPerSecond * 10).Within(1));
            Assert.That(second.Hull, Is.EqualTo(100));
        }

        [Test]
        public void Repair_PausesWithoutResourcesAndDoesNotRestoreCrew()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            ship.Hull = 100;
            ship.Soldiers = 3;
            settlement.Wood = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            Run(world, 5);
            Assert.That(ship.Hull, Is.LessThan(110));
            Assert.That(settlement.Wood, Is.GreaterThanOrEqualTo(0));
            Assert.That(ship.Soldiers, Is.EqualTo(3));
        }

        [TestCase(0.25)]
        [TestCase(0.5)]
        [TestCase(0.75)]
        [TestCase(1.0)]
        public void Scrap_RepairingFirstNeverPaysBetter(double condition)
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            ship.Hull = ship.Definition.Hull * condition;
            var start = settlement.Wood;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Scrap, ShipIds = new[] { ship.Id } });
            Run(world, 21);
            Assert.That(world.FindShip(ship.Id), Is.Null);
            var refund = (settlement.Wood - start) / 100.0;
            Assert.That(refund, Is.EqualTo(192 * 0.15 * condition).Within(0.01));
            var viaRepair = 192 * 0.15 - 192 * 0.25 * (1 - condition);
            Assert.That(viaRepair, Is.LessThanOrEqualTo(refund + 0.01));
            Assert.That(world.Wrecks, Is.Empty);
        }

        [Test]
        public void Wreck_SunkTransportLeavesHalfItsCargo()
        {
            var world = Empty();
            var ship = world.SpawnShip(0, ShipClass.Cutter, 0, 0);
            ship.CargoType = ResourceType.Silver;
            ship.Cargo = 60 * Rules.Centi;
            ship.Hull = 0;
            var total = world.LedgerTotal(ResourceType.Silver);
            world.Step();
            Assert.That(world.Wrecks.Count, Is.EqualTo(1));
            Assert.That(world.Wrecks[0].Silver, Is.EqualTo(30 * Rules.Centi));
            Assert.That(world.LostSilver, Is.EqualTo(30 * Rules.Centi));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(total));
        }

        [Test]
        public void Wreck_EmptyHullPaysNothing()
        {
            var world = Empty();
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            ship.Hull = 0;
            var wood = world.SettlementOf(0).Wood;
            world.Step();
            Assert.That(world.Wrecks, Is.Empty);
            Assert.That(world.SettlementOf(0).Wood, Is.EqualTo(wood));
        }

        [Test]
        public void Wreck_CanBeSalvagedAndDeliveredByACutter()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            world.Wrecks.Add(new Wreck { Id = world.NextId++, X = -450, Y = 0, Wood = 40 * Rules.Centi, ExpiresTick = 100000 });
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            var start = settlement.Wood;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Salvage, ShipIds = new[] { cutter.Id }, TargetId = world.Wrecks[0].Id });
            Run(world, 40);
            Assert.That(world.Wrecks, Is.Empty);
            Assert.That(settlement.Wood - start, Is.EqualTo(40 * Rules.Centi));
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Wreck_ExpiresIntoTheLedgerAsLoss()
        {
            var world = Empty();
            world.Wrecks.Add(new Wreck { Id = world.NextId++, Wood = 40 * Rules.Centi, ExpiresTick = 5 });
            var total = world.LedgerTotal(ResourceType.Wood);
            Run(world, 1);
            Assert.That(world.Wrecks, Is.Empty);
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(total));
        }

        // ---------- Movement ----------

        [Test]
        public void Movement_HasNoLateralSlip()
        {
            var world = Empty();
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            ship.Heading = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 0, Y = 300 });
            for (var i = 0; i < 600; i++)
            {
                var x = ship.X;
                var y = ship.Y;
                world.Step();
                var step = World.Distance(x, y, ship.X, ship.Y);
                Assert.That(step, Is.LessThanOrEqualTo(ship.Definition.Speed * Rules.Dt + 1e-6));
                if (step > 1e-6)
                {
                    var direction = Math.Atan2(ship.Y - y, ship.X - x);
                    Assert.That(Math.Abs(World.NormalizeAngle(direction - ship.Heading)), Is.LessThan(1e-6));
                }
            }
        }

        [Test]
        public void Movement_ReachesTargetAroundIslandWithoutEnteringIt()
        {
            var world = Empty();
            var island = new Island { X = 0, Y = 0, Radius = 70 };
            world.Islands.Add(island);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -200, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 200, Y = 0 });
            for (var i = 0; i < 3000; i++)
            {
                world.Step();
                Assert.That(World.Distance(ship.X, ship.Y, 0, 0), Is.GreaterThanOrEqualTo(island.Radius + ship.Definition.Radius - 1e-6));
            }
            Assert.That(World.Distance(ship.X, ship.Y, 200, 0), Is.LessThan(ship.Definition.Radius + 8));
        }

        // ---------- Land under sites and settlements ----------

        [Test]
        public void Land_EverySiteAndSettlementHasLandAndStaysReachable()
        {
            var world = MapFactory.CreateSkirmish(1);
            Assert.That(world.Islands.Count(item => item.Kind == IslandKind.ResourceSite), Is.EqualTo(world.Nodes.Count));
            Assert.That(world.Islands.Count(item => item.Kind == IslandKind.Settlement), Is.EqualTo(world.Settlements.Count + world.Sites.Count));

            // A loaded cutter must fit at every berth, clear of land and of water too shallow for it.
            var probe = new World(1).SpawnShip(0, ShipClass.Cutter, 0, 0);
            probe.Cargo = probe.Definition.Capacity * Rules.Centi;
            var clearance = World.LandClearance(probe);
            foreach (var node in world.Nodes)
            {
                foreach (var island in world.Islands)
                {
                    Assert.That(LandShape.SignedDistance(island, node.X, node.Y), Is.GreaterThanOrEqualTo(clearance), $"Node {node.Id} vs {island.Kind}");
                }
            }
        }

        [Test]
        public void Land_IsTheExactWaterlineNotACircle()
        {
            var coast = new Island { X = 0, Y = 0, Outline = LandShape.Place(MapFactory.CoastOutline, 50, false), Kind = IslandKind.Coast };
            coast.Radius = LandShape.MaxRadius(coast.Outline);
            var narrow = MapFactory.CoastOutline[0] * 50;
            var wide = MapFactory.CoastOutline[40] * 50;
            // 1 m out along the ray; the neighbouring edges of the tip may lie a little closer than that.
            Assert.That(LandShape.SignedDistance(coast, narrow + 1, 0), Is.GreaterThan(0.2).And.LessThanOrEqualTo(1 + 1e-9), "Just off the narrow tip.");
            Assert.That(LandShape.SignedDistance(coast, narrow - 3, 0), Is.LessThan(0), "Inside the land.");
            var far = LandShape.Angle(40);
            Assert.That(LandShape.SignedDistance(coast, Math.Cos(far) * (wide - 2), Math.Sin(far) * (wide - 2)), Is.LessThan(0), "The wide lobe reaches past where the narrow side ends.");

            // A cutter can moor off the narrow tip, well inside the old circle of the land's largest radius.
            var world = Empty();
            world.Islands.Add(coast);
            var ship = world.SpawnShip(0, ShipClass.Cutter, 150, 0);
            var clearance = World.LandClearance(ship);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = narrow + clearance + 6, Y = 0 });
            for (var i = 0; i < 1500; i++)
            {
                world.Step();
                Assert.That(LandShape.SignedDistance(coast, ship.X, ship.Y), Is.GreaterThanOrEqualTo(clearance - 1e-6));
            }
            Assert.That(World.Distance(ship.X, ship.Y, 0, 0), Is.LessThan(coast.Radius), "Closer than the old circle allowed.");
        }

        [Test]
        public void Land_EveryIsletHasAnIrregularNaturalShoreThatKeepsItsQuays()
        {
            var world = MapFactory.CreateSkirmish(1);
            var again = MapFactory.CreateSkirmish(2);
            var islets = world.Islands.Where(island => island.Kind != IslandKind.Coast && island.Kind != IslandKind.Hinterland).ToList();
            Assert.That(islets.Count, Is.GreaterThan(10));
            for (var n = 0; n < islets.Count; n++)
            {
                var island = islets[n];
                Assert.That(island.Outline, Is.Not.Null, $"{island.Kind} at {island.X},{island.Y} has a real waterline.");
                Assert.That(again.Islands.Where(item => item.Kind != IslandKind.Coast && item.Kind != IslandKind.Hinterland).ElementAt(n).Outline, Is.EqualTo(island.Outline), "The same place always gets the same shore.");

                var plain = island.Kind == IslandKind.ResourceSite ? LandShape.Ellipse(Rules.SiteIsletHalfWidth, Rules.SiteIsletHalfDepth) : Enumerable.Repeat(Rules.SettlementLandRadius, LandShape.Samples).ToArray();
                var ratios = island.Outline.Select((radius, i) => radius / plain[i]).ToArray();
                var bends = 0;
                for (var i = 0; i < ratios.Length; i++)
                {
                    var before = ratios[(i + ratios.Length - 1) % ratios.Length] - ratios[i];
                    var after = ratios[(i + 1) % ratios.Length] - ratios[i];
                    if (before * after > 0) bends++;
                }
                Assert.That(ratios.Max() - ratios.Min(), Is.GreaterThan(0.1), "Capes and bays, not a plain ellipse or circle.");
                Assert.That(bends, Is.GreaterThanOrEqualTo(6), "A ragged shore with several capes and bays.");
            }

            // Toward the port and along the slipways the shore stays where jetty and ramps cross it.
            foreach (var settlement in world.Settlements)
            {
                var land = world.Islands.First(item => Math.Abs(item.X - Rules.SettlementLandX(settlement)) < 0.5 && Math.Abs(item.Y - settlement.Y) < 0.5);
                var toward = Math.Sign(settlement.X - land.X);
                Assert.That(LandShape.SignedDistance(land, land.X + toward * 21.5, land.Y), Is.GreaterThan(0), "The jetty end stands in water.");
                Assert.That(LandShape.SignedDistance(land, land.X + toward * 15, land.Y), Is.LessThan(0), "The jetty starts on land.");
                foreach (var side in new[] { -1, 1 })
                {
                    Assert.That(LandShape.SignedDistance(land, land.X + toward * Rules.SlipAlong, land.Y + side * 23), Is.GreaterThan(0), "The slipway runs into water.");
                    Assert.That(LandShape.SignedDistance(land, land.X + toward * Rules.SlipAlong, land.Y + side * 15), Is.LessThan(0), "The slipway starts on land.");
                }
            }

            // On a work point's islet, the trees or the mine stand on land and the jetty reaches the berth.
            foreach (var node in world.Nodes)
            {
                var land = world.Islands.First(item => Math.Abs(item.X - node.X) < 0.5 && Math.Abs(item.Y - (node.Y + Rules.SiteLandOffset)) < 0.5);
                foreach (var (x, z) in new[] { (-13.0, 3.0), (15.0, 3.0), (0.0, 9.0), (-10.0, -5.0), (10.0, -5.0), (0.0, -11.0) })
                    Assert.That(LandShape.SignedDistance(land, land.X + x, land.Y + z), Is.LessThan(0), $"Land under the yard at {x},{z}.");
                Assert.That(LandShape.SignedDistance(land, land.X, land.Y - 24), Is.GreaterThan(0), "The jetty end stands in water.");
            }
        }

        [Test]
        public void Land_DeeperHullsKeepFurtherFromTheShore()
        {
            var cutter = new World(1).SpawnShip(0, ShipClass.Cutter, 0, 0);
            var merchant = new World(1).SpawnShip(0, ShipClass.Merchant, 0, 0);
            Assert.That(Seabed.ShoalWidth(World.DraftOf(merchant)), Is.GreaterThan(Seabed.ShoalWidth(World.DraftOf(cutter))));

            var emptyDraft = World.DraftOf(merchant);
            merchant.HoldWood = 480 * Rules.Centi;
            Assert.That(World.DraftOf(merchant), Is.EqualTo(emptyDraft * (1 + Rules.DraftPerLoad)).Within(1e-9), "A full hold sits deeper.");
            Assert.That(Seabed.ShoalWidth(World.DraftOf(merchant)), Is.GreaterThan(Seabed.ShoalWidth(emptyDraft)));
            Assert.That(Seabed.DepthAt(Seabed.ShoalWidth(World.DraftOf(merchant))), Is.EqualTo(World.DraftOf(merchant) + Seabed.KeelClearance).Within(1e-9));

            foreach (var shipClass in new[] { ShipClass.Cutter, ShipClass.Boarder })
            {
                var world = Empty();
                var island = new Island { X = 0, Y = 0, Radius = 30 };
                world.Islands.Add(island);
                var ship = world.SpawnShip(0, shipClass, -150, 0);
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 0, Y = 0 });
                for (var i = 0; i < 1500; i++) world.Step();
                var keep = LandShape.SignedDistance(island, ship.X, ship.Y);
                Assert.That(keep, Is.GreaterThanOrEqualTo(World.LandClearance(ship) - 1e-6), $"{shipClass} stays out of its shallows.");
                Assert.That(keep, Is.LessThan(World.LandClearance(ship) + 8), $"{shipClass} still comes as close as it safely can.");
            }
        }

        [Test]
        public void Hulls_NeverOverlapAndPushEachOtherAside()
        {
            var world = Empty();
            var ships = new[]
            {
                world.SpawnShip(0, ShipClass.Cutter, -100, 0), world.SpawnShip(0, ShipClass.Merchant, -100, 30),
                world.SpawnShip(0, ShipClass.Ram, -100, -30), world.SpawnShip(1, ShipClass.Boarder, 100, 0),
            };
            foreach (var ship in ships) world.Issue(new Command { Faction = ship.Owner, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 0, Y = 0 });
            var worst = double.MaxValue;
            for (var i = 0; i < 1200; i++)
            {
                world.Step();
                for (var a = 0; a < world.Ships.Count; a++)
                {
                    for (var b = a + 1; b < world.Ships.Count; b++) worst = Math.Min(worst, World.HullGap(world.Ships[a], world.Ships[b]));
                }
            }
            Assert.That(worst, Is.GreaterThan(World.HullSpacing * 0.5), "Hulls keep water between them, so on screen none sits on another.");
            Assert.That(ships.Count(ship => World.Distance(ship.X, ship.Y, 0, 0) < 30), Is.GreaterThan(1), "They crowd round the point side by side.");

            // A light hull is shoved further than a heavy one.
            var pair = Empty();
            var light = pair.SpawnShip(0, ShipClass.Cutter, 0, 0);
            var heavy = pair.SpawnShip(0, ShipClass.Boarder, 0, 2);
            light.Heading = heavy.Heading = 0;
            pair.Step();
            Assert.That(Math.Abs(light.Y), Is.GreaterThan(Math.Abs(heavy.Y - 2)));
        }

        [Test]
        public void Land_ShipsNeverEnterItDuringAMatchAndStillDeliverGoods()
        {
            var world = MapFactory.CreateSkirmish(9);
            var first = new Opponent(0);
            var second = new Opponent(1);
            var startStock = world.Nodes.Sum(node => node.Stock);
            var worstGap = 0.0;
            for (var tick = 0; tick < 6000 && world.Outcome == OutcomeKind.InProgress; tick++)
            {
                first.Think(world);
                second.Think(world);
                world.Step();
                world.Events.Clear();
                foreach (var ship in world.Ships)
                {
                    var keep = World.LandClearance(ship);
                    foreach (var island in world.Islands)
                    {
                        var distance = LandShape.SignedDistance(island, ship.X, ship.Y);
                        if (distance < keep - 1e-6) Assert.Fail($"Tick {tick}: {ship.Class} {ship.Id} is {distance:0.00} m from {island.Kind} land, needs {keep:0.00}.");
                    }
                }
                for (var a = 0; a < world.Ships.Count; a++)
                {
                    for (var b = a + 1; b < world.Ships.Count; b++) worstGap = Math.Min(worstGap, World.HullGap(world.Ships[a], world.Ships[b]));
                }
            }
            TestContext.WriteLine($"Deepest hull overlap {worstGap:0.00} m");
            Assert.That(worstGap, Is.GreaterThan(-1.0), "Hulls never sail through each other.");
            Assert.That(world.Nodes.Sum(node => node.Stock), Is.LessThan(startStock), "Work points stay usable next to their land.");
        }

        [Test]
        public void Land_MoveOrderOntoLandStopsAtTheShore()
        {
            var world = Empty();
            var island = new Island { X = 0, Y = 0, Radius = 20, Kind = IslandKind.ResourceSite };
            world.Islands.Add(island);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -150, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 5, Y = 0 });
            world.Step();
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Move), "The command takes effect on the first step.");
            for (var i = 0; i < 3000 && ship.Order != OrderKind.Idle; i++) world.Step();
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle), "The ship must not circle the land forever.");
            Assert.That(World.Distance(ship.X, ship.Y, 0, 0), Is.LessThan(island.Radius + World.LandClearance(ship) * 2 + 8));
        }

        [Test]
        public void Land_OnlyACoastBlocksShotsAndKindIsSaved()
        {
            var world = Empty();
            world.Islands.Add(new Island { X = 0, Y = 0, Radius = 20, Kind = IslandKind.ResourceSite });
            Assert.That(world.HasIslandBetween(-50, 0, 50, 0), Is.False);
            world.Islands.Add(new Island { X = 0, Y = 100, Radius = 20, Kind = IslandKind.Coast });
            Assert.That(world.HasIslandBetween(-50, 100, 50, 100), Is.True);

            world.Islands[1].Outline = LandShape.Place(MapFactory.CoastOutline, 15, true);
            world.Islands[1].Yaw = 180;
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            Assert.That(loaded.Islands.Select(item => item.Kind), Is.EqualTo(new[] { IslandKind.ResourceSite, IslandKind.Coast }));
            Assert.That(loaded.Islands[1].Outline, Is.EqualTo(world.Islands[1].Outline), "The waterline is saved.");
            Assert.That(loaded.Islands[1].Yaw, Is.EqualTo(180));
            Assert.That(loaded.Islands[0].Outline, Is.Null);
        }

        // ---------- Ram ----------

        static (Ship ram, Ship target) RamSetup(World world, double targetHeading)
        {
            var ram = world.SpawnShip(0, ShipClass.Ram, -60, 0);
            ram.Heading = 0;
            var target = world.SpawnShip(1, ShipClass.Boarder, 0, 0);
            target.Heading = targetHeading;
            return (ram, target);
        }

        [Test]
        public void Ram_SideHitDoesTwiceTheBowDamage()
        {
            var world = Empty();
            var (ram, side) = RamSetup(world, Math.PI / 2);
            var sideDamage = World.RamDamage(ram, side, 8, 0);
            side.Heading = Math.PI;
            var bowDamage = World.RamDamage(ram, side, 8, 0);
            Assert.That(sideDamage, Is.EqualTo(bowDamage * 2).Within(1e-6));
        }

        [Test]
        public void Ram_DamageIsCappedAt400()
        {
            var world = Empty();
            var (ram, target) = RamSetup(world, Math.PI / 2);
            Assert.That(World.RamDamage(ram, target, 40, 0), Is.EqualTo(Rules.RamMaxDamage));
        }

        [Test]
        public void Ram_OneChargeResolvesOnceAndHurtsAttackerTwentyPercent()
        {
            var world = Empty();
            var (ram, target) = RamSetup(world, Math.PI / 2);
            ram.Speed = 8;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Ram, ShipIds = new[] { ram.Id }, TargetId = target.Id });
            var hits = 0;
            var dealt = 0.0;
            var ramLossAtImpact = 0.0;
            for (var i = 0; i < 300 && hits == 0; i++)
            {
                var ramBefore = ram.Hull;
                world.Events.Clear();
                world.Step();
                var hit = world.Events.FirstOrDefault(item => item.Type == "ram_hit");
                if (hit == null) continue;
                hits++;
                dealt = hit.Amount;
                var archery = world.Events.Any(item => item.Type == "volley" && item.OtherId == ram.Id);
                Assert.That(archery, Is.False, "This step must isolate the ram damage.");
                ramLossAtImpact = ramBefore - ram.Hull;
            }
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(dealt, Is.GreaterThan(0));
            Assert.That(ramLossAtImpact, Is.EqualTo(dealt * Rules.RamRecoilFraction).Within(0.01));

            world.Events.Clear();
            Run(world, 2);
            Assert.That(world.Events.Count(item => item.Type == "ram_hit"), Is.EqualTo(0), "Contact must not keep dealing damage.");
        }

        [Test]
        public void Ram_SlowContactDoesNoDamage()
        {
            var world = Empty();
            var ram = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var target = world.SpawnShip(1, ShipClass.Boarder, 20, 0);
            ram.Heading = 0;
            ram.Speed = 1;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Ram, ShipIds = new[] { ram.Id }, TargetId = target.Id });
            var hull = target.Hull;
            world.Step();
            Assert.That(world.Events.Any(item => item.Type == "ram_failed"), Is.True);
            Assert.That(target.Hull, Is.EqualTo(hull));
        }

        // ---------- Boarding ----------

        static (Ship boarder, Ship target) BoardSetup(World world, int defenders)
        {
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 26, 0);
            target.Soldiers = defenders;
            return (boarder, target);
        }

        [Test]
        public void Board_RequiresTenSoldiers()
        {
            var world = Empty();
            var (boarder, target) = BoardSetup(world, 5);
            boarder.Soldiers = 9;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            world.Step();
            Assert.That(boarder.Order, Is.Not.EqualTo(OrderKind.Board));
        }

        [Test]
        public void Board_DoesNotChangeOwnerImmediately_AndCapturesWithCargoAndPaidCost()
        {
            var world = Empty();
            var (boarder, target) = BoardSetup(world, 5);
            target.CargoType = ResourceType.Silver;
            target.Cargo = 77 * Rules.Centi;
            var paid = target.PaidConstructionWood;
            var silver = world.LedgerTotal(ResourceType.Silver);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            Run(world, 2);
            Assert.That(target.Owner, Is.EqualTo(1), "Hooks take 3 seconds.");
            Run(world, 30);
            Assert.That(target.Owner, Is.EqualTo(0));
            Assert.That(target.Cargo, Is.EqualTo(77 * Rules.Centi));
            Assert.That(target.PaidConstructionWood, Is.EqualTo(paid));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
            Assert.That(world.Boardings, Is.Empty);
        }

        [Test]
        public void Board_SecondAttackerCannotLinkToTheSameTarget()
        {
            var world = Empty();
            var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 0, 0);
            var first = world.SpawnShip(0, ShipClass.Boarder, -26, 0);
            var second = world.SpawnShip(0, ShipClass.Boarder, 26, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { first.Id, second.Id }, TargetId = target.Id });
            Run(world, 1);
            Assert.That(world.Boardings.Count, Is.EqualTo(1));
            Assert.That(world.Boardings[0].AttackerId, Is.EqualTo(first.Id));
        }

        [Test]
        public void Board_SunkTargetIsNotCapturedInTheSameStep()
        {
            var world = Empty();
            var (boarder, target) = BoardSetup(world, 0);
            world.Boardings.Add(new BoardingLink { AttackerId = boarder.Id, TargetId = target.Id, Hooked = true, ControlTicks = 1000 });
            target.Hull = 0;
            world.Step();
            Assert.That(world.FindShip(target.Id), Is.Null);
            Assert.That(world.Events.Any(item => item.Type == "ship_captured"), Is.False);
            Assert.That(world.Events.Any(item => item.Type == "ship_sunk"), Is.True);
        }

        [Test]
        public void Board_EscapingTargetCutsTheHooks()
        {
            var world = Empty();
            var (boarder, target) = BoardSetup(world, 20);
            world.Boardings.Add(new BoardingLink { AttackerId = boarder.Id, TargetId = target.Id, HookTicks = 90 });
            boarder.Order = OrderKind.Board;
            boarder.TargetId = target.Id;
            target.X = 300;
            world.Step();
            Assert.That(world.Boardings, Is.Empty);
            Assert.That(target.Owner, Is.EqualTo(1));
        }

        // ---------- Fog of war ----------

        [Test]
        public void Fog_HiddenShipCannotBeTargeted()
        {
            var world = Empty();
            var attacker = world.SpawnShip(0, ShipClass.CrewDestroyer, -480, 400);
            var hidden = world.SpawnShip(1, ShipClass.Cutter, 480, -400);
            Assert.That(world.CanSee(0, hidden), Is.False);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.AttackShip, ShipIds = new[] { attacker.Id }, TargetId = hidden.Id });
            world.Step();
            Assert.That(attacker.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Fog_CannotCommandEnemyShip()
        {
            var world = Empty();
            var enemy = world.SpawnShip(1, ShipClass.Cutter, 480, 0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { enemy.Id }, X = 0, Y = 0 });
            world.Step();
            Assert.That(enemy.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Opponent_DoesNotReactToHiddenShips()
        {
            var world = MapFactory.CreateSkirmish(11);
            var ai = new Opponent(1);
            var warship = world.SpawnShip(1, ShipClass.CrewDestroyer, 450, 450);
            var hidden = world.SpawnShip(0, ShipClass.Cutter, -500, -500);
            for (var i = 0; i < 90; i++) { ai.Think(world); world.Step(); }
            Assert.That(world.CanSee(1, hidden), Is.False);
            Assert.That(warship.TargetId, Is.Not.EqualTo(hidden.Id));
        }

        [Test]
        public void Fog_StartsUnexploredBeyondOwnSight()
        {
            var world = MapFactory.CreateSkirmish(1);
            var home = world.SettlementOf(0);
            var enemy = world.SettlementOf(1);
            int Cell(double value) => (int)((value - World.FogOrigin) / World.FogCellSize);
            Assert.That(world.GetFog(0, Cell(home.X), Cell(home.Y)), Is.EqualTo(FogState.Visible));
            Assert.That(world.GetFog(0, Cell(enemy.X), Cell(enemy.Y)), Is.EqualTo(FogState.Unexplored));
            Assert.That(world.GetFog(1, Cell(enemy.X), Cell(enemy.Y)), Is.EqualTo(FogState.Visible));
        }

        [Test]
        public void Fog_LeftAreaStaysExploredButNotVisible()
        {
            var world = Empty();
            var scout = world.SpawnShip(0, ShipClass.Cutter, 0, 500);
            world.RefreshExplored();
            int Cell(double value) => (int)((value - World.FogOrigin) / World.FogCellSize);
            Assert.That(world.GetFog(0, Cell(0), Cell(500)), Is.EqualTo(FogState.Visible));
            world.Ships.Remove(scout);
            world.RefreshExplored();
            Assert.That(world.GetFog(0, Cell(0), Cell(500)), Is.EqualTo(FogState.Explored));
            Assert.That(world.GetFog(1, Cell(0), Cell(500)), Is.EqualTo(FogState.Unexplored), "Exploration is per faction.");
        }

        [Test]
        public void Fog_ExplorationIsSaved()
        {
            var world = Empty();
            var scout = world.SpawnShip(0, ShipClass.Cutter, 0, 500);
            world.RefreshExplored();
            world.Ships.Remove(scout);
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            int Cell(double value) => (int)((value - World.FogOrigin) / World.FogCellSize);
            Assert.That(loaded.GetFog(0, Cell(0), Cell(500)), Is.EqualTo(FogState.Explored));
        }

        [Test]
        public void Fog_VisibleGridMatchesCellByCellQuery()
        {
            var world = MapFactory.CreateSkirmish(2);
            var grid = new bool[World.FogCells * World.FogCells];
            world.FillVisible(0, grid);
            for (var j = 0; j < World.FogCells; j++)
            {
                for (var i = 0; i < World.FogCells; i++)
                {
                    var expected = world.GetFog(0, i, j) == FogState.Visible;
                    if (grid[j * World.FogCells + i] != expected) Assert.Fail($"Cell {i},{j} differs");
                }
            }
            Assert.That(grid.Count(item => item), Is.GreaterThan(0));
        }

        // ---------- Combat and victory ----------

        [Test]
        public void Weapons_DoNotFireThroughAnIsland()
        {
            var world = Empty();
            world.Islands.Add(new Island { X = 0, Y = 0, Radius = 30 });
            world.SpawnShip(0, ShipClass.CrewDestroyer, -60, 0);
            var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 60, 0);
            var hull = target.Hull;
            Run(world, 10);
            Assert.That(target.Hull, Is.EqualTo(hull));
        }

        [Test]
        public void Weapons_DamageHullAndCrewInRange()
        {
            var world = Empty();
            world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
            var target = world.SpawnShip(1, ShipClass.Cutter, 60, 0);
            var hull = target.Hull;
            Run(world, 10);
            Assert.That(target.Hull, Is.LessThan(hull));
            Assert.That(target.Sailors, Is.LessThan(8));
        }

        [Test]
        public void Victory_DestroyedSettlementEndsTheMatch()
        {
            var world = Empty();
            world.SpawnShip(0, ShipClass.Cutter, -500, 0);
            world.SpawnShip(1, ShipClass.Cutter, 500, 0);
            world.SettlementOf(1).Hull = 0;
            world.Step();
            Assert.That(world.Outcome, Is.EqualTo(OutcomeKind.Won));
            Assert.That(world.Winner, Is.EqualTo(0));
        }

        [Test]
        public void Defeat_NoShipsQueueOrFundsLosesTheMatch()
        {
            var world = Empty();
            world.SpawnShip(1, ShipClass.Cutter, 500, 0);
            world.SettlementOf(0).Silver = 0;
            world.Step();
            Assert.That(world.Outcome, Is.EqualTo(OutcomeKind.Won));
            Assert.That(world.Winner, Is.EqualTo(1));
        }

        [Test]
        public void Defeat_NoShipsButFundsToRebuildContinues()
        {
            var world = Empty();
            world.SpawnShip(1, ShipClass.Cutter, 500, 0);
            world.Step();
            Assert.That(world.Outcome, Is.EqualTo(OutcomeKind.InProgress));
        }

        // ---------- Save, ledger, whole match ----------

        static (World world, Opponent first, Opponent second) Match(ulong seed)
        {
            var world = MapFactory.CreateSkirmish(seed);
            return (world, new Opponent(0), new Opponent(1));
        }

        static void Advance(World world, Opponent first, Opponent second, int ticks)
        {
            for (var i = 0; i < ticks && world.Outcome == OutcomeKind.InProgress; i++)
            {
                first.Think(world);
                second.Think(world);
                world.Step();
            }
        }

        [Test]
        public void Ledger_ConservesWoodAndSilverAcrossAMatch()
        {
            var (world, first, second) = Match(21);
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);
            for (var i = 0; i < 15000 && world.Outcome == OutcomeKind.InProgress; i++)
            {
                first.Think(world);
                second.Think(world);
                world.Step();
                world.Events.Clear();
                if (i % 30 != 0) continue;
                Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
                Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
                foreach (var settlement in world.Settlements)
                {
                    Assert.That(settlement.Wood, Is.GreaterThanOrEqualTo(0));
                    Assert.That(settlement.Silver, Is.GreaterThanOrEqualTo(0));
                    Assert.That(settlement.Available(ResourceType.Wood), Is.GreaterThanOrEqualTo(0));
                    Assert.That(settlement.Available(ResourceType.Silver), Is.GreaterThanOrEqualTo(0));
                    Assert.That(settlement.DockedShips.Count, Is.LessThanOrEqualTo(Rules.PortBerths));
                }
                foreach (var node in world.Nodes)
                {
                    Assert.That(node.Workers.Count, Is.LessThanOrEqualTo(Rules.WorkBerths));
                    Assert.That(node.Stock, Is.GreaterThanOrEqualTo(0));
                }
            }
        }

        [Test]
        public void Match_SameSeedGivesIdenticalState()
        {
            var (a, a0, a1) = Match(42);
            var (b, b0, b1) = Match(42);
            Advance(a, a0, a1, 12000);
            Advance(b, b0, b1, 12000);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(a)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(b))));
        }

        [Test]
        public void Save_RoundTripContinuesIdenticallyDuringTransfers()
        {
            var (world, first, second) = Match(5);
            Advance(world, first, second, 4000);
            Assert.That(world.Ships.Any(ship => ship.Cargo > 0), Is.True, "Saved while cargo is in transit.");
            var data = SaveGame.Serialize(world, second.BuildIndex);
            var firstBuild = first.BuildIndex;

            var loaded = SaveGame.Deserialize(data, out var build);
            Assert.That(SaveGame.Serialize(loaded, build), Is.EqualTo(data));
            Assert.That(loaded.LedgerTotal(ResourceType.Wood), Is.EqualTo(world.LedgerTotal(ResourceType.Wood)));

            var loadedFirst = new Opponent(0) { BuildIndex = firstBuild };
            var loadedSecond = new Opponent(1) { BuildIndex = build };
            Advance(world, first, second, 6000);
            Advance(loaded, loadedFirst, loadedSecond, 6000);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
        }

        [Test]
        public void Save_RejectsUnknownVersionAndForeignData()
        {
            var data = SaveGame.Serialize(MapFactory.CreateSkirmish(1));
            data[4] = 99;
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(data, out _));
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(new byte[32], out _));
        }

        [Test]
        public void Save_FileKeepsPreviousVersionAsBackup()
        {
            var directory = Path.Combine(Path.GetTempPath(), "morza-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "save.bin");
                SaveGame.WriteFile(path, new byte[] { 1 });
                SaveGame.WriteFile(path, new byte[] { 2 });
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 2 }));
                Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(new byte[] { 1 }));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void Match_StartMatchesTheDocumentedSetup()
        {
            var world = MapFactory.CreateSkirmish(1);
            foreach (var settlement in world.Settlements)
            {
                Assert.That(settlement.Wood, Is.EqualTo(360 * Rules.Centi));
                Assert.That(settlement.Silver, Is.EqualTo(160 * Rules.Centi));
                var cutters = world.Ships.Where(ship => ship.Owner == settlement.Owner).ToList();
                Assert.That(cutters.Count, Is.EqualTo(4));
                Assert.That(cutters.Count(ship => world.FindNode(ship.NodeId).Type == ResourceType.Wood), Is.EqualTo(3));
                Assert.That(cutters.Count(ship => world.FindNode(ship.NodeId).Type == ResourceType.Silver), Is.EqualTo(1));
                // E04's 70-110 m to the home resources, stretched with the scenario's map (MatchProfile.Spread).
                var spread = world.Profile.Spread;
                foreach (var node in world.Nodes.Where(item => item.KnownMask == 1 << settlement.Owner && World.Distance(item.X, item.Y, settlement.X, settlement.Y) < 150 * spread))
                {
                    Assert.That(World.Distance(node.X, node.Y, settlement.X, settlement.Y), Is.InRange(70.0 * spread, 110.0 * spread));
                }
            }
            // The pattern of E08 with the Standard scenario's deposits (docs/BALANS_I_SKALA.md).
            var profile = world.Profile;
            Assert.That(world.Length, Is.EqualTo(MatchLength.Standard));
            Assert.That(world.Nodes.Where(node => node.Type == ResourceType.Wood).Sum(node => node.Stock),
                Is.EqualTo((2 * profile.HomeWood + 4 * profile.PairWood + 2 * profile.CentreWood) * Rules.Centi), "Wood budget.");
            Assert.That(world.Nodes.Where(node => node.Type == ResourceType.Silver).Sum(node => node.Stock),
                Is.EqualTo((2 * profile.HomeSilver + 4 * profile.PairSilver + 2 * profile.CentreSilver) * Rules.Centi), "Silver budget.");
        }

        [Test]
        public void Scale_LongerScenariosSpreadWiderAndHoldMoreAndYieldSlower()
        {
            var worlds = new[] { MatchLength.Short, MatchLength.Standard, MatchLength.Long }.Select(length => MapFactory.CreateSkirmish(1, length)).ToArray();
            double Span(World world) => World.Distance(world.Settlements[0].X, 0, world.Settlements[1].X, 0);
            long Silver(World world) => world.Nodes.Where(node => node.Type == ResourceType.Silver).Sum(node => node.Stock);
            for (var i = 1; i < worlds.Length; i++)
            {
                Assert.That(Span(worlds[i]), Is.GreaterThan(Span(worlds[i - 1])), "A longer scenario's bases lie farther apart.");
                Assert.That(Silver(worlds[i]), Is.GreaterThan(Silver(worlds[i - 1])), "A longer scenario holds more silver.");
                Assert.That(worlds[i].Profile.SilverYield, Is.LessThanOrEqualTo(worlds[i - 1].Profile.SilverYield), "A longer scenario's mines yield no faster.");
                Assert.That(worlds[i].SettlementOf(0).MaxHull, Is.GreaterThan(worlds[i - 1].SettlementOf(0).MaxHull), "A longer scenario's home port holds out longer.");
            }
            foreach (var world in worlds)
            {
                Assert.That(world.Profile.TargetMinutes, Is.InRange(30, 90));
                Assert.That(world.Profile.TargetFleet, Is.InRange(30, 55));
                Assert.That(world.SettlementOf(0).Hull, Is.EqualTo(world.SettlementOf(0).MaxHull), "Ports start whole.");
            }
        }

        [Test]
        public void Scale_YieldSlowsWorkAndTheNodeCapAlike()
        {
            var world = MapFactory.CreateSkirmish(1, MatchLength.Long);
            var node = world.Nodes.First(item => item.Type == ResourceType.Silver);
            var cutter = world.Ships.First(ship => ship.Class == ShipClass.Cutter);
            Assert.That(world.WorkRate(node, cutter), Is.EqualTo(cutter.Definition.SilverWork * world.Profile.SilverYield).Within(1e-9));
            Assert.That(world.NodeCap(node), Is.EqualTo(Rules.SilverNodeCap * world.Profile.SilverYield).Within(1e-9));
        }

        [Test]
        public void Scale_HinterlandsJoinTheirIsletsAndLeaveEveryBerthOpen()
        {
            foreach (var length in new[] { MatchLength.Short, MatchLength.Standard, MatchLength.Long })
            {
                var world = MapFactory.CreateSkirmish(1, length);
                var hinterlands = world.Islands.Where(item => item.Kind == IslandKind.Hinterland).ToList();
                Assert.That(hinterlands.Count, Is.GreaterThanOrEqualTo(6), $"{length}: large islands behind ports and work points.");
                foreach (var land in hinterlands)
                {
                    Assert.That(land.Radius, Is.GreaterThan(Rules.SettlementLandRadius * 1.5), "Larger than the islet it backs.");
                    var islet = world.Islands.Where(item => item.Kind == IslandKind.Settlement || item.Kind == IslandKind.ResourceSite)
                        .OrderBy(item => World.Distance(item.X, item.Y, land.X, land.Y)).First();
                    Assert.That(LandShape.SignedDistance(land, islet.X, islet.Y), Is.LessThan(islet.Radius), "Joined to its islet, not a separate rock.");
                    foreach (var settlement in world.Settlements)
                        Assert.That(LandShape.SignedDistance(land, settlement.X, settlement.Y), Is.GreaterThanOrEqualTo(MapFactory.HinterlandBerthClearance - 1e-9));
                    foreach (var site in world.Sites)
                        Assert.That(LandShape.SignedDistance(land, site.X, site.Y), Is.GreaterThanOrEqualTo(MapFactory.HinterlandBerthClearance - 1e-9));
                    foreach (var node in world.Nodes)
                        Assert.That(LandShape.SignedDistance(land, node.X, node.Y), Is.GreaterThanOrEqualTo(MapFactory.HinterlandBerthClearance - 1e-9));
                }

                // The slipways still run from the port's islet into open water.
                foreach (var settlement in world.Settlements)
                {
                    var landX = Rules.SettlementLandX(settlement);
                    var toward = Math.Sign(settlement.X - landX);
                    foreach (var side in new[] { -1, 1 })
                        foreach (var island in world.Islands.Where(item => item.Kind != IslandKind.Settlement))
                            Assert.That(LandShape.SignedDistance(island, landX + toward * Rules.SlipAlong, settlement.Y + side * 30), Is.GreaterThan(0), $"{length}: slipway end clear of {island.Kind}.");
                }
            }
        }

        [Test]
        public void Match_FirstRamCanStartImmediatelyAndLeavesTheDocumentedRest()
        {
            var world = MapFactory.CreateSkirmish(1);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Step();
            var settlement = world.SettlementOf(0);
            Assert.That(settlement.Available(ResourceType.Wood), Is.EqualTo(120 * Rules.Centi));
            Assert.That(settlement.Available(ResourceType.Silver), Is.EqualTo(60 * Rules.Centi));
            Assert.That(settlement.WarQueue[0].Started, Is.True);
        }

        [Test]
        public void Match_OpponentsBuildFleetsAndFight()
        {
            var (world, first, second) = Match(9);
            var built = 0;
            var combat = 0;
            var firstCombatTick = -1;
            for (var i = 0; i < 30 * 60 * Rules.TickRate && world.Outcome == OutcomeKind.InProgress; i++)
            {
                first.Think(world);
                second.Think(world);
                world.Step();
                built += world.Events.Count(item => item.Type == "ship_built");
                var fights = world.Events.Count(item => item.Type == "volley" || item.Type == "ram_hit");
                if (fights > 0 && firstCombatTick < 0) firstCombatTick = world.Tick;
                combat += fights;
                world.Events.Clear();
                Assert.That(world.Ships.All(ship => !double.IsNaN(ship.X) && !double.IsNaN(ship.Y)), Is.True);
            }
            TestContext.WriteLine($"Tick {world.Tick} ({world.Tick / 30 / 60} min), outcome {world.Outcome}, winner {world.Winner}, built {built}, combat events {combat}, first combat {firstCombatTick / 30} s, ships {world.Ships.Count}");
            Assert.That(built, Is.GreaterThan(4));
            Assert.That(combat, Is.GreaterThan(0));
        }
    }
}
