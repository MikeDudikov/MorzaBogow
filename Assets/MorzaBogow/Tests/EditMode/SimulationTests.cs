using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void Repair_AtADockChargesAQuarterOfThePriceInSilverOnly()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.HasDock = true;
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            ship.Hull = 380;
            // The whole price, not its construction part (user decision of 2026-11-11): 240 D + 100 S for a ram.
            Assert.That(World.FullPrice(ship), Is.EqualTo((ship.Definition.CostWood + ship.Definition.CostSilver) * Rules.Centi));
            var price = World.FullPrice(ship) / 100.0;
            var wood = settlement.Wood;
            var silver = settlement.Silver;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            Run(world, 40);
            Assert.That(ship.Hull, Is.EqualTo(760).Within(0.01));
            Assert.That(settlement.Wood, Is.EqualTo(wood), "Repairs take no wood.");
            Assert.That((silver - settlement.Silver) / 100.0, Is.EqualTo(price * Rules.RepairFullFraction * 0.5).Within(0.05), "A quarter of the whole price in silver for a full repair; half the hull here.");
        }

        [Test]
        public void Repair_TheWholePriceCountsTheItemsFittedNow()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.HasDock = true;
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            var armed = World.FullPrice(ship);
            // The archer bank taken off and out of the plan: the construction, the crew and the ram stay in the price.
            var bank = ship.ItemWood[0] + ship.ItemSilver[0];
            ship.Mounts[0] = ship.Loadout[0] = WeaponType.None;
            ship.ItemWood[0] = ship.ItemSilver[0] = 0;
            Assert.That(bank, Is.GreaterThan(0));
            Assert.That(World.FullPrice(ship), Is.EqualTo(armed - bank));
            Assert.That(World.FullPrice(ship), Is.GreaterThan(ship.PaidConstructionWood + ship.PaidConstructionSilver), "More than the construction part.");

            ship.Hull = 380;
            var silver = settlement.Silver;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            Run(world, 40);
            Assert.That(ship.Hull, Is.EqualTo(760).Within(0.01));
            Assert.That((silver - settlement.Silver) / 100.0, Is.EqualTo((armed - bank) / 100.0 * Rules.RepairFullFraction * 0.5).Within(0.05));
        }

        [Test]
        public void Repair_WithoutADockTheHullWaitsButTheCrewIsMustered()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            ship.Hull = 400;
            ship.Swordsmen -= 4;
            var wood = settlement.Wood;
            var restored = false;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            for (var i = 0; i < 40 * Rules.TickRate; i++)
            {
                world.Step();
                restored |= world.Events.Any(item => item.Type == "ship_restored" && item.SubjectId == ship.Id);
            }
            Assert.That(ship.Swordsmen, Is.EqualTo(ship.Plan.Swordsmen), "Any shipyard musters the crew.");
            Assert.That(ship.Hull, Is.EqualTo(400), "Only a dock mends the hull.");
            Assert.That(settlement.Wood, Is.EqualTo(wood));
            Assert.That(restored, Is.True);
            Assert.That(ship.Order, Is.Not.EqualTo(OrderKind.Repair));
        }

        [Test]
        public void Repair_OrderForAHullAloneNeedsADock()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X + 100, 0);
            ship.Hull = 400;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            world.Step();
            Assert.That(ship.Order, Is.Not.EqualTo(OrderKind.Repair), "Nothing a port without a dock could do for it.");
            settlement.HasDock = true;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            world.Step();
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Repair), "With a dock on its side it sails to be mended.");
        }

        [Test]
        public void Repair_RunsAtTwelveHullPerSecondOnOneStation()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.HasDock = true;
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
            settlement.HasDock = true;
            var ship = world.SpawnShip(0, ShipClass.Ram, settlement.X, 0);
            ship.Hull = 100;
            ship.Swordsmen = 3;
            ship.Javelins = 0;
            ship.Archers = 0;
            settlement.Silver = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            Run(world, 5);
            Assert.That(ship.Hull, Is.LessThan(110), "No silver, no repair.");
            Assert.That(settlement.Silver, Is.GreaterThanOrEqualTo(0));
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
            // Scrapping returns a share of the construction paid; a repair costs a share of the whole price.
            var price = (ship.PaidConstructionWood + ship.PaidConstructionSilver) / 100.0;
            var whole = World.FullPrice(ship) / 100.0;
            var startWood = settlement.Wood;
            var startSilver = settlement.Silver;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Scrap, ShipIds = new[] { ship.Id } });
            Run(world, 21);
            Assert.That(world.FindShip(ship.Id), Is.Null);
            // Repairs cost silver only and scrapping returns both stores, so the comparison counts wood and silver together.
            var refund = (settlement.Wood - startWood + settlement.Silver - startSilver) / 100.0;
            Assert.That(refund, Is.EqualTo(price * Rules.ScrapFraction * condition).Within(0.02));
            var viaRepair = price * Rules.ScrapFraction - whole * Rules.RepairFullFraction * (1 - condition);
            Assert.That(viaRepair, Is.LessThanOrEqualTo(refund + 0.02), "Repairing first never pays better.");
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
            // The target's posts stay unmanned, so no shot lands on the ram in the step of the impact.
            target.Javelins = 0;
            target.Archers = 0;
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
                var archery = world.Events.Any(item => item.Type == "projectile_hit" && item.SubjectId == ram.Id);
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

        /// <summary>Keel to keel when two hulls lie side by side at the spacing hulls keep: where a boarding begins (C11).</summary>
        static double Alongside(ShipClass a, ShipClass b) => (ShipCatalog.Get(a).Beam + ShipCatalog.Get(b).Beam) / 2 + World.HullSpacing;

        static (Ship boarder, Ship target) BoardSetup(World world, int defenders)
        {
            // Side by side, on opposite courses: boarding is from the side, never bow or stern on.
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 0, Alongside(ShipClass.Boarder, ShipClass.CrewDestroyer));
            target.Swordsmen = defenders;
            target.Archers = 0;
            return (boarder, target);
        }

        [Test]
        public void Board_RequiresTenSoldiers()
        {
            var world = Empty();
            var (boarder, target) = BoardSetup(world, 5);
            boarder.Swordsmen = 9;
            boarder.Javelins = 0;
            boarder.Archers = 0;
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
            var first = world.SpawnShip(0, ShipClass.Boarder, 0, -Alongside(ShipClass.Boarder, ShipClass.CrewDestroyer));
            var second = world.SpawnShip(0, ShipClass.Boarder, 0, Alongside(ShipClass.Boarder, ShipClass.CrewDestroyer));
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

        // ---------- Boarding geometry (A02, C11, Q04): from the side, at real relative speed ----------

        static bool HooksWithin(World world, double seconds)
        {
            for (var i = 0; i < seconds * Rules.TickRate; i++)
            {
                world.Step();
                var thrown = world.Events.Any(item => item.Type == "hooks_thrown");
                world.Events.Clear();
                if (thrown) return true;
            }
            return false;
        }

        static Ship UnderWay(World world, int owner, ShipClass shipClass, double x, double y, double heading, double speed)
        {
            var ship = world.SpawnShip(owner, shipClass, x, y);
            ship.Heading = heading;
            ship.Speed = speed;
            ship.Order = OrderKind.Move;
            ship.TargetX = x + Math.Cos(heading) * 5000;
            ship.TargetY = y + Math.Sin(heading) * 5000;
            return ship;
        }

        [Test]
        public void Board_HullsPassingOnOppositeCoursesOrCrossingThrowNoHooks()
        {
            // Side by side at 5.2 m/s each on opposite courses the hulls pass at 10.4 m/s; crossing at right angles at
            // 7.35 m/s. A scalar difference of speeds made both 0 and threw the hooks (A02).
            var world = Empty();
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            boarder.Heading = 0;
            boarder.Speed = 5.2;
            var passing = UnderWay(world, 1, ShipClass.CrewDestroyer, 10, Alongside(ShipClass.Boarder, ShipClass.CrewDestroyer) + 1.5, Math.PI, 5.2);
            Assert.That(World.HullGap(boarder, passing), Is.LessThanOrEqualTo(Rules.BoardGap), "Within the hooks' reach as they pass.");
            Assert.That(world.RelativeSpeed(boarder, passing), Is.EqualTo(10.4).Within(1e-6));
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = passing.Id });
            Assert.That(HooksWithin(world, 6), Is.False, "No hooks between hulls passing each other.");

            var crossing = Empty();
            var crosser = crossing.SpawnShip(0, ShipClass.Boarder, 0, 0);
            crosser.Heading = 0;
            crosser.Speed = 5.2;
            var crossed = UnderWay(crossing, 1, ShipClass.CrewDestroyer, 20, -15, Math.PI / 2, 5.2);
            Assert.That(crossing.RelativeSpeed(crosser, crossed), Is.EqualTo(Math.Sqrt(2) * 5.2).Within(1e-6));
            crossing.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { crosser.Id }, TargetId = crossed.Id });
            Assert.That(HooksWithin(crossing, 6), Is.False, "Nor across a bow at right angles.");
        }

        [Test]
        public void Board_TheHooksReachOnlyAcrossTheWaterBetweenTheHulls()
        {
            // Measured between the hulls' outlines, as contacts are (World.HullGap), not between circles round them.
            bool HooksAt(double water)
            {
                var world = Empty();
                var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
                var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 0, Alongside(ShipClass.Boarder, ShipClass.CrewDestroyer) - World.HullSpacing + water);
                boarder.Heading = target.Heading = 0;
                Assert.That(World.HullGap(boarder, target), Is.EqualTo(water).Within(1e-6));
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                return HooksWithin(world, 0.1);
            }
            Assert.That(HooksAt(3), Is.True, "Three metres of water: the hooks reach.");
            Assert.That(HooksAt(11), Is.False, "Eleven metres: they do not, though circles round the hulls would overlap.");
        }

        [Test]
        public void Board_ComesAlongsideAStillTargetFromAnyApproachAndHooksFromTheSide()
        {
            (bool thrown, double seconds, double gap, bool alongside, double relative) Approach(double x, double y, double heading, double targetX = 0, double targetHeading = 0)
            {
                var world = Empty();
                var target = world.SpawnShip(1, ShipClass.CrewDestroyer, targetX, 0);
                target.Heading = targetHeading;
                target.Swordsmen = target.Javelins = target.Archers = 0;
                var boarder = world.SpawnShip(0, ShipClass.Boarder, x, y);
                boarder.Heading = heading;
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                for (var i = 0; i < 60 * Rules.TickRate; i++)
                {
                    world.Step();
                    var thrown = world.Events.Any(item => item.Type == "hooks_thrown");
                    world.Events.Clear();
                    if (thrown) return (true, i / (double)Rules.TickRate, World.HullGap(boarder, target), World.BoardAlongside(boarder, target), world.RelativeSpeed(boarder, target));
                }
                return (false, 60, 0, false, 0);
            }

            var approaches = new[]
            {
                ("ahead, on its bow line", Approach(80, 0, Math.PI)),
                ("astern", Approach(-80, 0, 0)),
                ("abeam, bow on", Approach(0, 70, -Math.PI / 2)),
                ("from 45 degrees", Approach(60, 60, -3 * Math.PI / 4)),
                ("bow to bow, 2 m apart", Approach(0, 0, 0, 26, Math.PI)),
            };
            foreach (var (name, result) in approaches)
            {
                Assert.That(result.thrown, Is.True, $"{name}: alongside and hooked within a minute.");
                // Measured after the step that threw them, so within a tick's motion of the rule.
                Assert.That(result.gap, Is.LessThanOrEqualTo(Rules.BoardGap + 0.5), $"{name}: across little water.");
                Assert.That(result.alongside, Is.True, $"{name}: side to side, not bow or stern on.");
                Assert.That(result.relative, Is.LessThanOrEqualTo(Rules.BoardRelativeSpeed), $"{name}: at the target's pace.");
            }
        }

        [Test]
        public void Board_ASlowerShipUnderWayIsCaughtAndAFasterOneIsNot()
        {
            var world = Empty();
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 60);
            boarder.Heading = 0;
            var bomber = UnderWay(world, 1, ShipClass.Bomber, 0, 0, 0, 4.3);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = bomber.Id });
            Assert.That(HooksWithin(world, 120), Is.True, "A bomber at 4.3 m/s is overtaken by a boarder at 5.2 and grappled alongside.");
            Assert.That(World.BoardAlongside(boarder, bomber), Is.True);

            var chase = Empty();
            var chaser = chase.SpawnShip(0, ShipClass.Boarder, -40, 15);
            chaser.Heading = 0;
            chaser.Speed = 5;
            var ram = UnderWay(chase, 1, ShipClass.Ram, 0, 0, 0, 8.2);
            chase.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { chaser.Id }, TargetId = ram.Id });
            Assert.That(HooksWithin(chase, 60), Is.False, "A ram rowing away at 8.2 m/s is never grappled.");
        }

        [Test]
        public void Board_ATargetPullingAwayWhileTheHooksBiteCutsThem()
        {
            var world = Empty();
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            var target = world.SpawnShip(1, ShipClass.CrewDestroyer, 0, Alongside(ShipClass.Boarder, ShipClass.CrewDestroyer));
            boarder.Heading = target.Heading = 0;
            target.Swordsmen = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            Assert.That(HooksWithin(world, 0.1), Is.True);
            // The target makes off at once, faster than the boarder can follow.
            target.Order = OrderKind.Move;
            target.TargetX = 5000;
            target.Speed = 4;
            var cut = false;
            for (var i = 0; i < Rules.BoardHookSeconds * Rules.TickRate && !cut; i++)
            {
                world.Step();
                cut = world.Events.Any(item => item.Type == "hooks_cut" && item.Amount == 0);
                world.Events.Clear();
            }
            Assert.That(cut, Is.True, "Pulling away parts the lines before they hold, with no swordsman to cut them.");
            Assert.That(world.Boardings, Is.Empty);
        }

        [Test]
        public void Board_LashedHullsComeToRestWhicheverComesFirstInTheList()
        {
            // The target first in the list, under way on its own orders: lashed, it rows nowhere, and the pair stops.
            var world = Empty();
            var target = UnderWay(world, 1, ShipClass.Bomber, 0, Alongside(ShipClass.Boarder, ShipClass.Bomber), 0, 3);
            // The bomber keeps its whole crew: the pair loses its way in 4 to 5 s, and with three fighters aboard it was
            // taken in 4 s, before it could be seen to lie still.
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            boarder.Heading = 0;
            boarder.Speed = 3;
            Assert.That(target.Id, Is.LessThan(boarder.Id));
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            var hooked = false;
            for (var i = 0; i < 20 * Rules.TickRate && !hooked; i++) { world.Step(); hooked = world.Boardings.Any(link => link.Hooked); }
            Assert.That(hooked, Is.True, "Grappled alongside under way, and the hooks held.");
            // The target must come to rest while it is still the enemy's: once taken it would lie still as any ship without
            // orders does, and the test would pass whatever the lashed hull did.
            Assert.That(target.Speed, Is.GreaterThan(1.0), "The target is still under way when the hooks hold.");
            var restedAsEnemy = false;
            for (var i = 0; i < 8 * Rules.TickRate && target.Owner == 1 && !restedAsEnemy; i++)
            {
                world.Step();
                restedAsEnemy = target.Owner == 1 && target.Speed == 0;
            }
            Assert.That(restedAsEnemy, Is.True, "The lashed target lies still before the fight on deck is decided.");
            Assert.That(boarder.Speed, Is.EqualTo(0).Within(1e-9), "And the boarder with it.");
        }

        [Test]
        public void Board_WithItsSideOfTheTargetTakenTheBoarderGoesRoundToTheOtherSide()
        {
            // J01: another hull lies where the boarder would lie beside its target. It used to shove that hull aside for
            // minutes before it could throw its hooks; it goes round and boards from the free side.
            var world = Empty();
            var target = world.SpawnShip(1, ShipClass.Merchant, 0, 0);
            var neighbour = world.SpawnShip(1, ShipClass.Merchant, 0, 12.1);
            target.Heading = neighbour.Heading = 0;
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 120);
            boarder.Heading = -Math.PI / 2;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            Assert.That(HooksWithin(world, 60), Is.True, "Hooks within a minute, not after minutes of shoving.");
            Assert.That(world.Boardings.Single().TargetId, Is.EqualTo(target.Id));
            Assert.That(boarder.Y, Is.LessThan(target.Y), "From the side the neighbour does not lie on.");
            Assert.That(World.BoardAlongside(boarder, target), Is.True, "And from the side, as ever.");
            Assert.That(World.Distance(neighbour.X, neighbour.Y, 0, 12.1), Is.LessThan(1.0), "The neighbour was not pushed out of its place.");
        }

        [Test]
        public void Board_WithBothSidesOfTheTargetTakenTheBoarderWaitsCloseByAndBoardsWhenOneIsFree()
        {
            // J01: with a hull on either side there is no place alongside. The boarder waits near the target, lying still
            // with its order standing, instead of manoeuvring without end; when a side comes free it boards.
            var world = Empty();
            var target = world.SpawnShip(1, ShipClass.Merchant, 0, 0);
            var near = world.SpawnShip(1, ShipClass.Merchant, 0, 12.1);
            var far = world.SpawnShip(1, ShipClass.Merchant, 0, -12.1);
            target.Heading = near.Heading = far.Heading = 0;
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 120);
            boarder.Heading = -Math.PI / 2;
            // No javelins: the wait is not cut short by the target sinking under them.
            boarder.Swordsmen += boarder.Javelins;
            boarder.Javelins = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            Assert.That(HooksWithin(world, 60), Is.False, "No place alongside: no hooks.");
            var (x, y, heading) = (boarder.X, boarder.Y, boarder.Heading);
            Assert.That(HooksWithin(world, 60), Is.False);
            Assert.That((boarder.Order, boarder.TargetId), Is.EqualTo((OrderKind.Board, target.Id)), "The order stands.");
            Assert.That(World.Distance(boarder.X, boarder.Y, x, y), Is.LessThan(0.5), "It lies still while it waits.");
            Assert.That(Math.Abs(boarder.Heading - heading), Is.LessThan(0.01), "And does not turn to and fro.");
            Assert.That(World.Distance(boarder.X, boarder.Y, target.X, target.Y), Is.LessThan(60.0), "Close by the target.");
            Assert.That((World.Distance(near.X, near.Y, 0, 12.1), World.Distance(far.X, far.Y, 0, -12.1)), Is.EqualTo((0.0, 0.0)), "Nobody was pushed.");

            near.Y = 300;
            Assert.That(HooksWithin(world, 90), Is.True, "A side comes free: the boarder goes alongside and throws its hooks.");
            Assert.That(world.Boardings.Single().TargetId, Is.EqualTo(target.Id));
        }

        [Test]
        public void Board_SaveDuringTheApproachContinuesIdentically()
        {
            // The approach alongside keeps nothing of its own between ticks (the side, the lane and the course are read
            // from where the hulls lie), so a match saved while a boarder is still coming up goes on the same after
            // loading: off the lane, turning onto it, and with the side taken and the boarder on its way round. Each phase
            // is checked at the moment of saving, so a change of the approach's timing fails here instead of quietly
            // saving in another phase.
            // The lane runs along the target's course through the station off its side; World.BoardLaneWidth either way.
            const double laneWidth = 2.5, linedUp = 0.35;
            var station = Alongside(ShipClass.Boarder, ShipClass.Merchant);
            void SavedAfter(double seconds, bool sideTaken, string phase, Action<Ship, Ship> atSave, Action<Ship, Ship> atEnd = null)
            {
                var world = Empty();
                var target = world.SpawnShip(1, ShipClass.Merchant, 0, 0);
                target.Heading = 0;
                if (sideTaken) world.SpawnShip(1, ShipClass.Merchant, 0, 12.1).Heading = 0;
                var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 120);
                boarder.Heading = -Math.PI / 2;
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                Run(world, seconds);
                Assert.That(world.Boardings, Is.Empty, $"{phase}: saved before any hooks.");
                Assert.That(boarder.Order, Is.EqualTo(OrderKind.Board));
                atSave(boarder, target);
                var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
                var hooks = 0;
                for (var i = 0; i < 30 * Rules.TickRate; i++)
                {
                    world.Step();
                    loaded.Step();
                    hooks += world.Events.Count(item => item.Type == "hooks_thrown");
                    world.Events.Clear();
                    loaded.Events.Clear();
                }
                Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))), phase);
                Assert.That(hooks, Is.EqualTo(1), $"{phase}: the approach ended in hooks within those 30 s, so the whole of it was compared.");
                atEnd?.Invoke(boarder, target);
            }
            SavedAfter(14, false, "Off the lane, under way to its start", (boarder, target) =>
            {
                Assert.That(Math.Abs(boarder.Y - station), Is.GreaterThan(laneWidth), "Still off the lane.");
                Assert.That(boarder.Speed, Is.GreaterThan(1.0), "And under way to its start.");
            });
            SavedAfter(31, false, "On the lane, turning to its course", (boarder, target) =>
            {
                Assert.That(Math.Abs(boarder.Y - station), Is.LessThanOrEqualTo(laneWidth), "On the lane.");
                Assert.That(Math.Abs(Math.Sin(boarder.Heading - target.Heading)), Is.GreaterThan(Math.Sin(linedUp)), "Not yet lined up with it.");
                Assert.That(boarder.Speed, Is.LessThan(0.1), "Turning where it lies.");
            });
            SavedAfter(20, true, "Its side taken, on the way round the target", (boarder, target) =>
            {
                Assert.That(boarder.Y, Is.GreaterThan(target.Y + station), "Still on the side the neighbour lies on.");
            }, (boarder, target) =>
            {
                Assert.That(boarder.Y, Is.LessThan(target.Y), "It went round: the hooks came from the other side.");
            });
        }

        [Test]
        public void Board_AHullTheSideDoesNotSeeDoesNotSteerItsBoarder()
        {
            // Q06, J08: the side of the target a boarder makes for was chosen by every hull beside it, seen or not, so its
            // course told of a ship its side did not see (the opponent's boarders of the player's hidden ships too). Two
            // matches that differ only in a hull nobody of the side sees must steer the boarder alike, until it is seen.
            (World world, Ship boarder, Ship target, Ship neighbour) Story(bool withNeighbour)
            {
                var world = Empty();
                var target = world.SpawnShip(1, ShipClass.Merchant, 0, 0);
                target.Heading = 0;
                // A cutter sees 250 m: the target at 245 m, not a hull 12.1 m beyond it.
                world.SpawnShip(0, ShipClass.Cutter, 0, -245);
                var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 600);
                boarder.Heading = -Math.PI / 2;
                Ship neighbour = null;
                if (withNeighbour)
                {
                    neighbour = world.SpawnShip(1, ShipClass.Merchant, 0, 12.1);
                    neighbour.Heading = 0;
                }
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                return (world, boarder, target, neighbour);
            }
            var (with, boarder, target, neighbour) = Story(true);
            var (without, twin, twinTarget, _) = Story(false);
            Assert.That(boarder.Id, Is.EqualTo(twin.Id));
            Assert.That(with.CanSee(0, target), Is.True, "The side sees the target.");
            Assert.That(with.CanSee(0, neighbour), Is.False, "And not the hull beside it.");

            var seenAt = -1;
            for (var tick = 0; tick < 120 * Rules.TickRate && seenAt < 0; tick++)
            {
                if (with.CanSee(0, neighbour)) { seenAt = tick; break; }
                with.Step();
                without.Step();
                Assert.That((boarder.X, boarder.Y, boarder.Heading, boarder.Speed, boarder.Order), Is.EqualTo((twin.X, twin.Y, twin.Heading, twin.Speed, twin.Order)),
                    $"Tick {tick}: the unseen hull already steers the boarder.");
            }
            Assert.That(seenAt, Is.GreaterThan(0), "The boarder comes near enough to see the hull.");
            Assert.That(boarder.Order, Is.EqualTo(OrderKind.Board));

            // Seen, the hull counts: the boarder goes round to the free side, as the rule says; without it, it does not.
            for (var i = 0; i < 90 * Rules.TickRate && (with.Boardings.Count == 0 || without.Boardings.Count == 0); i++)
            {
                if (with.Boardings.Count == 0) with.Step();
                if (without.Boardings.Count == 0) without.Step();
            }
            Assert.That((with.Boardings.Count, without.Boardings.Count), Is.EqualTo((1, 1)), "Both come alongside and throw their hooks.");
            Assert.That(boarder.Y, Is.LessThan(target.Y), "Round the hull it now sees, from the far side.");
            Assert.That(twin.Y, Is.GreaterThan(twinTarget.Y), "With no hull there, from the side it came.");
        }

        [Test]
        public void Board_SaveWhileTheHooksBiteContinuesIdentically()
        {
            var (world, attacker, target) = Grappled(ShipClass.Boarder, ShipClass.CrewDestroyer);
            Run(world, 1);
            Assert.That(world.Boardings.Any(link => !link.Hooked), Is.True, "Saved while the hooks bite.");
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            for (var i = 0; i < 10 * Rules.TickRate; i++) { world.Step(); loaded.Step(); }
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
            Assert.That(loaded.FindShip(target.Id).Owner, Is.EqualTo(target.Owner));
        }

        // ---------- Crew by trade (docs/ZALOGA_I_ABORDAZ.md) ----------

        static readonly ShipClass[] AllClasses = { ShipClass.Cutter, ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.Merchant };

        [Test]
        public void Crew_EveryPlanFillsTheHullAndTheDefaultKeepsThePrice()
        {
            foreach (var shipClass in AllClasses)
            {
                var definition = ShipCatalog.Get(shipClass);
                foreach (CrewPreset preset in Enum.GetValues(typeof(CrewPreset)))
                {
                    if (preset == CrewPreset.Custom) continue;
                    var plan = definition.PlanFor(preset);
                    Assert.That(plan.Total, Is.EqualTo(definition.MaxCrew), $"{shipClass} {preset} fills the hull.");
                    Assert.That(plan.Sailors, Is.GreaterThanOrEqualTo(definition.BaseSailors), $"{shipClass} {preset} keeps its rowers.");
                }
                // E04 prices sailors at 0.5 S and fighters at 1 S: the default crew is the one the price was set for.
                var standard = definition.PlanFor(definition.DefaultPreset);
                Assert.That(standard.Sailors, Is.EqualTo(definition.StandardSailors));
                Assert.That(standard.Swordsmen + standard.Javelins + standard.Archers, Is.EqualTo(definition.StandardSoldiers));
                var ship = Empty().SpawnShip(0, shipClass, 0, 0);
                Assert.That((ship.Sailors, ship.Swordsmen, ship.Javelins, ship.Archers), Is.EqualTo((standard.Sailors, standard.Swordsmen, standard.Javelins, standard.Archers)));
                // Every post of the default loadout has its crew.
                Assert.That(World.Manned(ship).Where((manned, i) => ship.Mounts[i] != WeaponType.None).All(manned => manned), Is.True, $"{shipClass} mans its posts.");
            }
            Assert.That(ShipCatalog.Get(ShipClass.CrewDestroyer).PlanFor(CrewPreset.Volley).Archers, Is.GreaterThan(ShipCatalog.Get(ShipClass.CrewDestroyer).PlanFor(CrewPreset.Volley).Swordsmen), "A crew destroyer is a ship of archers.");
            Assert.That(ShipCatalog.Get(ShipClass.Boarder).PlanFor(CrewPreset.Boarding).Swordsmen, Is.GreaterThan(ShipCatalog.Get(ShipClass.Boarder).PlanFor(CrewPreset.Boarding).Archers), "A boarder is a ship of swordsmen.");
        }

        [Test]
        public void Weapons_FourArcherBanksHurtCrewsAsTheCrewDestroyerDidBefore()
        {
            // Stage 2: 29 archers x 0.75 x 0.55 men-HP a volley; now four banks of 3.5 at a reference hit rate of 0.85.
            var bank = WeaponCatalog.Get(WeaponType.ArcherBank);
            Assert.That(4 * bank.Men * 0.85, Is.EqualTo(29 * 0.75 * 0.55).Within(0.5), "Crew damage per volley.");
            Assert.That(ShipCatalog.Get(ShipClass.CrewDestroyer).DefaultLoadout.Count(item => item == WeaponType.ArcherBank), Is.EqualTo(4));
        }

        [Test]
        public void Crew_FewerArchersShootLessAndNoneDoNotShoot()
        {
            int Lost(int archers)
            {
                var world = Empty(11);
                var shooter = world.SpawnShip(0, ShipClass.CrewDestroyer, 0, 0);
                shooter.Archers = archers;
                var target = world.SpawnShip(1, ShipClass.Boarder, 60, 0);
                target.Hull = 1e6;
                var crew = target.Crew;
                world.Issue(new Command { Faction = 0, Kind = CommandKind.AttackShip, ShipIds = new[] { shooter.Id }, TargetId = target.Id });
                Run(world, 30);
                return crew - target.Crew;
            }
            Assert.That(Lost(29), Is.GreaterThan(Lost(6)), "Four banks against one.");
            Assert.That(Lost(3), Is.Zero, "Three archers cannot man a bank of four.");
        }

        [Test]
        public void Crew_OnlySailorsRowAndSpareOarsAddSpeed()
        {
            var world = Empty();
            var ram = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            Assert.That(World.CrewSpeedFactor(ram), Is.EqualTo(1.0));
            ram.Swordsmen += 20;
            ram.Sailors = ram.Definition.BaseSailors / 2;
            Assert.That(World.CrewSpeedFactor(ram), Is.LessThan(0.6), "Fighters do not row.");
            var seamen = ram.Definition.PlanFor(CrewPreset.Seamen);
            ram.Sailors = seamen.Sailors;
            Assert.That(World.CrewSpeedFactor(ram), Is.EqualTo(1 + Rules.SeamenSpeedBonus).Within(1e-9));
        }

        // ---------- The fight on deck ----------

        [Test]
        public void Board_TheFrontLimitsWhoFightsAndArchersDefendTheirOwnDeck()
        {
            var world = Empty();
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, 0);
            var narrow = World.DeckStrength(boarder, 4, true);
            var wide = World.DeckStrength(boarder, 20, true);
            Assert.That(narrow.swordsmen, Is.EqualTo(4));
            Assert.That(wide.strength, Is.GreaterThan(narrow.strength), "A wider front brings more men to the rail.");

            var swords = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            swords.Swordsmen = 20; swords.Archers = 0;
            var bows = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            bows.Swordsmen = 0; bows.Archers = 20;
            Assert.That(World.DeckStrength(swords, 10, true).strength, Is.GreaterThan(World.DeckStrength(bows, 10, true).strength), "Swordsmen carry an assault.");
            Assert.That(World.DeckStrength(bows, 10, false).strength, Is.GreaterThan(World.DeckStrength(bows, 10, true).strength), "Archers fight better on their own deck.");
        }

        static (World world, Ship attacker, Ship target) Grappled(ShipClass attackerClass, ShipClass targetClass, ulong seed = 7)
        {
            var world = Empty(seed);
            var attacker = world.SpawnShip(0, attackerClass, 0, 100);
            var target = world.SpawnShip(1, targetClass, 0, 100 + Alongside(attackerClass, targetClass));
            attacker.Heading = target.Heading = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { attacker.Id }, TargetId = target.Id });
            return (world, attacker, target);
        }

        [Test]
        public void Board_TheFallenAreTwoInFiveWoundedAndTheRestDead()
        {
            var (world, attacker, target) = Grappled(ShipClass.Boarder, ShipClass.Ram);
            // A wounded man stays aboard, so able plus wounded falls only with the dead (and the prize crew sent across).
            var killed = 0;
            for (var i = 0; i < 60 * Rules.TickRate && target.Owner == 1; i++)
            {
                var aboard = attacker.Crew + attacker.Wounded;
                world.Step();
                if (target.Owner == 1) killed += aboard - (attacker.Crew + attacker.Wounded);
            }
            Assert.That(target.Owner, Is.EqualTo(0), "The boarder takes the ram.");
            var fallen = killed + attacker.Wounded;
            Assert.That(attacker.Wounded, Is.GreaterThan(0));
            Assert.That(attacker.Wounded, Is.EqualTo(fallen * Rules.WoundedShare).Within(1.0));
        }

        [Test]
        public void Board_ABrokenCrewSurrendersAndTheShipIsTakenWhole()
        {
            var (world, attacker, target) = Grappled(ShipClass.Boarder, ShipClass.CrewDestroyer);
            target.CargoType = ResourceType.Wood;
            target.Cargo = 40 * Rules.Centi;
            var paid = target.PaidConstructionSilver;
            var sailors = target.Sailors;
            var surrendered = false;
            for (var i = 0; i < 60 * Rules.TickRate && target.Owner == 1; i++)
            {
                if (world.Boardings.Any(link => link.Hooked)) target.Morale = Math.Min(target.Morale, Rules.SurrenderMorale - 1);
                world.Step();
                surrendered |= world.Events.Any(item => item.Type == "ship_surrendered");
            }
            Assert.That(surrendered, Is.True, "Fighters still aboard, but the crew is broken.");
            Assert.That(target.Owner, Is.EqualTo(0));
            Assert.That(target.Cargo, Is.EqualTo(40 * Rules.Centi), "Taken with its cargo.");
            Assert.That(target.PaidConstructionSilver, Is.EqualTo(paid), "And its fittings and cost.");
            Assert.That(target.Sailors, Is.GreaterThanOrEqualTo(Math.Min(sailors, target.Definition.BaseSailors)), "Its sailors row on for the new owner.");
            Assert.That(target.Wounded, Is.Zero, "The enemy's wounded are lost.");
            Assert.That(target.Plan.Total, Is.EqualTo(target.Definition.MaxCrew));
        }

        [Test]
        public void Board_DefendingSwordsmenCutTheLinesOfAWeakerBoarder()
        {
            var (world, attacker, target) = Grappled(ShipClass.Ram, ShipClass.Boarder);
            var cut = false;
            for (var i = 0; i < 10 * Rules.TickRate && !cut; i++)
            {
                world.Step();
                cut = world.Events.Any(item => item.Type == "hooks_cut" && item.Amount == 1);
            }
            Assert.That(cut, Is.True, "The boarder's swordsmen hack through a ram's grapples.");
            Assert.That(target.Owner, Is.EqualTo(1));
            Assert.That(World.BoardingOdds(target, attacker), Is.GreaterThan(1), "The other way round the odds favour the boarder.");
        }

        [Test]
        public void Board_ALostFightCostsTheWholeFightingCrewButNotTheSailors()
        {
            // A few swordsmen against a deck of archers and javelin men (no swordsmen to cut the lines): the hooks hold
            // and the attack is beaten on the enemy deck.
            var (world, attacker, target) = Grappled(ShipClass.Ram, ShipClass.CrewDestroyer);
            attacker.Swordsmen = 12; attacker.Javelins = 0; attacker.Archers = 0;
            attacker.Wounded = 2;
            target.Swordsmen = 0; target.Javelins = 10; target.Archers = 40;
            var sailors = attacker.Sailors;
            var repelled = false;
            var lost = 0;
            for (var i = 0; i < 60 * Rules.TickRate && !repelled; i++)
            {
                world.Step();
                var item = world.Events.FirstOrDefault(e => e.Type == "boarding_repelled");
                if (item == null) continue;
                repelled = true;
                lost = (int)item.Amount;
            }
            Assert.That(repelled, Is.True, "The attack is beaten on the enemy deck.");
            Assert.That(attacker.Soldiers, Is.Zero, "No fighter comes back.");
            Assert.That(lost, Is.GreaterThan(0), "The wounded of this attack are left on the enemy deck.");
            Assert.That(attacker.Wounded, Is.EqualTo(2), "Those wounded before the attack stay aboard.");
            Assert.That(attacker.Sailors, Is.EqualTo(sailors), "The sailors stay at the oars to bring the hull home.");
            Assert.That(target.Owner, Is.EqualTo(1));
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { attacker.Id }, TargetId = target.Id });
            world.Step();
            Assert.That(attacker.Order, Is.Not.EqualTo(OrderKind.Board), "Without fighters it cannot board again until a port trains a crew.");
        }

        [Test]
        public void Board_AShipWithoutFightersCannotResist()
        {
            var (world, attacker, target) = Grappled(ShipClass.Boarder, ShipClass.Merchant);
            // Swordsmen only, so no javelin or arrow flies on the way in: what is measured is the deck alone.
            attacker.Javelins = 0;
            attacker.Archers = 0;
            Assert.That(target.Soldiers, Is.Zero, "A merchant carries sailors only.");
            var sailors = target.Sailors;
            int? aboard = null;
            var ticks = 0;
            for (; ticks < 30 * Rules.TickRate && target.Owner == 1; ticks++)
            {
                world.Step();
                if (target.Owner != 1) break;
                if (world.Boardings.Any(link => link.Hooked)) aboard ??= attacker.Soldiers + attacker.Wounded;
                if (aboard.HasValue) Assert.That(attacker.Soldiers + attacker.Wounded, Is.EqualTo(aboard.Value), "Sailors do not fight at the rail: the attacker loses no one.");
                Assert.That(target.Sailors, Is.EqualTo(sailors), "Nor does anyone fall on the defenceless deck.");
            }
            Assert.That(target.Owner, Is.EqualTo(0), "Taken once the deck is held.");
            Assert.That(ticks, Is.LessThanOrEqualTo((int)((Rules.BoardHookSeconds + Rules.BoardControlSeconds + 2) * Rules.TickRate)), "Hooks, then the deck held, and nothing more.");
            Assert.That(target.Sailors, Is.GreaterThanOrEqualTo(Math.Min(sailors, target.Definition.BaseSailors)), "Its sailors row on for the new owner.");
        }

        [Test]
        public void Board_SaveDuringTheFightContinuesIdentically()
        {
            var (world, attacker, target) = Grappled(ShipClass.Boarder, ShipClass.CrewDestroyer);
            for (var i = 0; i < 6 * Rules.TickRate; i++) world.Step();
            Assert.That(world.Boardings.Any(link => link.Hooked), Is.True);
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            for (var i = 0; i < 8 * Rules.TickRate; i++) { world.Step(); loaded.Step(); }
            foreach (var ship in world.Ships)
            {
                var copy = loaded.FindShip(ship.Id);
                Assert.That((copy.Owner, copy.Sailors, copy.Swordsmen, copy.Archers, copy.Wounded, copy.Morale), Is.EqualTo((ship.Owner, ship.Sailors, ship.Swordsmen, ship.Archers, ship.Wounded, ship.Morale)));
                Assert.That(copy.Plan.Total, Is.EqualTo(ship.Plan.Total));
            }
        }

        // ---------- Regrouping in port ----------

        [Test]
        public void Port_AnIdleShipRegroupsByItselfForTheQuotedSilver()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, port.X + 30, 0);
            ship.Swordsmen -= 6;
            ship.Archers -= 2;
            ship.Wounded = 3;
            var quote = World.MusterCost(ship);
            Assert.That(quote, Is.EqualTo(3 * Rules.HealSilver + 5 * Rules.SoldierSilver), "Three healed, five recruited.");
            var silver = port.Silver;
            var restored = false;
            for (var i = 0; i < 40 * Rules.TickRate && !restored; i++)
            {
                world.Step();
                restored = world.Events.Any(item => item.Type == "ship_restored" && item.SubjectId == ship.Id);
            }
            Assert.That(restored, Is.True, "Went to the dock without an order.");
            Assert.That((ship.Sailors, ship.Swordsmen, ship.Archers, ship.Wounded), Is.EqualTo((ship.Plan.Sailors, ship.Plan.Swordsmen, ship.Plan.Archers, 0)));
            Assert.That(silver - port.Silver, Is.EqualTo(quote));
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Port_WithoutSilverTheMusterWaitsAndChargesNothing()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            port.Silver = Rules.SoldierSilver - 1;
            var ship = world.SpawnShip(0, ShipClass.Ram, port.X + 30, 0);
            ship.Swordsmen -= 4;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { ship.Id } });
            var silver = port.Silver;
            Run(world, 10);
            Assert.That(ship.Swordsmen, Is.EqualTo(ship.Plan.Swordsmen - 4));
            Assert.That(port.Silver, Is.EqualTo(silver));
        }

        [Test]
        public void Port_ANewPlanChangesTheCrewOnlyAtPort()
        {
            var world = Empty();
            var port = world.SettlementOf(0);
            var ship = world.SpawnShip(0, ShipClass.Ram, port.X + 400, 0);
            var before = (ship.Sailors, ship.Swordsmen, ship.Archers);
            var beforeJavelins = ship.Javelins;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetCrewPlan, ShipIds = new[] { ship.Id }, Preset = CrewPreset.Boarding });
            Run(world, 5);
            Assert.That(ship.Plan.Preset, Is.EqualTo(CrewPreset.Boarding));
            Assert.That((ship.Sailors, ship.Swordsmen, ship.Archers), Is.EqualTo(before), "Nobody retrains at sea.");

            ship.X = port.X + 30;
            var silver = port.Silver;
            Run(world, 60);
            Assert.That((ship.Sailors, ship.Swordsmen, ship.Archers), Is.EqualTo((ship.Plan.Sailors, ship.Plan.Swordsmen, ship.Plan.Archers)));
            Assert.That(ship.Javelins, Is.EqualTo(ship.Plan.Javelins));
            // Every man the new plan wants in another trade is one retraining.
            var moved = Math.Max(0, ship.Plan.Swordsmen - before.Swordsmen) + Math.Max(0, ship.Plan.Javelins - beforeJavelins) + Math.Max(0, ship.Plan.Archers - before.Archers);
            Assert.That(moved, Is.GreaterThan(0));
            Assert.That(silver - port.Silver, Is.EqualTo(moved * Rules.RetrainSilver), "Archers retrained for the boarding crew.");
        }

        [Test]
        public void Opponent_RegroupsWhenHalfOfAllItsFightersAreGoneJavelinMenIncluded()
        {
            // The opponent weighs every trade of fighter alike, in the plan as aboard (A04): a boarder's javelin men count
            // toward the half it must keep, and a crew of javelin men alone regroups too.
            bool Regroups(int swordsmen, int javelins, int archers, CrewPlan? plan = null, long silver = 1500 * Rules.Centi)
            {
                var world = Empty();
                world.SettlementOf(0).Silver = silver;
                var ship = world.SpawnShip(0, ShipClass.Boarder, -400, 0);
                if (plan.HasValue) ship.Plan = plan.Value;
                ship.Swordsmen = swordsmen;
                ship.Javelins = javelins;
                ship.Archers = archers;
                new Opponent(0).Think(world);
                return world.Pending.Any(command => command.Kind == CommandKind.Repair && command.ShipIds.Contains(ship.Id));
            }

            var definition = ShipCatalog.Get(ShipClass.Boarder);
            var planned = definition.PlanFor(definition.DefaultPreset);
            Assert.That(planned.Javelins, Is.GreaterThan(0), "The boarder's own plan carries javelin men.");
            var belowHalf = (planned.Soldiers - 1) / 2;
            Assert.That(belowHalf * 2, Is.GreaterThanOrEqualTo(planned.Swordsmen + planned.Archers), "Without the javelin men this crew would not count as spent.");
            Assert.That(Regroups(belowHalf, 0, 0), Is.True, "Fewer than half of all its fighters left: home to regroup.");
            Assert.That(Regroups(belowHalf + 1, 0, 0), Is.False, "Half or more still aboard: it fights on.");

            var javelinMen = new CrewPlan { Sailors = planned.Sailors, Javelins = 40, Preset = CrewPreset.Custom };
            Assert.That(Regroups(0, 19, 0, javelinMen), Is.True, "A crew of javelin men alone regroups as any other.");
            Assert.That(Regroups(0, 20, 0, javelinMen), Is.False);

            Assert.That(Regroups(belowHalf, 0, 0, silver: 0), Is.False, "Without the silver for the muster it stays with the fleet.");
        }

        [Test]
        public void Opponent_BoardsOnlyWhatItsBoarderCanCatch()
        {
            // Hooks hold only between hulls keeping pace (A02): the opponent boards a ship lying still, and shoots at one
            // making off faster than its boarder can row, from what it sees of that ship.
            CommandKind OrderAgainst(double speed)
            {
                var world = Empty();
                var boarder = world.SpawnShip(0, ShipClass.Boarder, -300, 0);
                var target = world.SpawnShip(1, ShipClass.Merchant, -240, 0);
                target.Speed = speed;
                new Opponent(0, 1).Think(world);
                return world.Pending.Last(command => command.ShipIds.Contains(boarder.Id)).Kind;
            }
            Assert.That(OrderAgainst(0), Is.EqualTo(CommandKind.Board), "A ship lying still is boarded.");
            Assert.That(OrderAgainst(6.5), Is.EqualTo(CommandKind.AttackShip), "One faster than the boarder can row is shot at instead.");
        }

        [Test]
        public void Opponent_ABoarderShootingAtAShipMakingOffBoardsItOnceItLiesStill()
        {
            // The choice between hooks and javelins follows what the opponent sees, both ways (A02): left at its javelins,
            // a boarder sank every ship that was under way when first seen and lay still afterwards, and took none.
            var world = Empty();
            var boarder = world.SpawnShip(0, ShipClass.Boarder, -300, 0);
            var target = UnderWay(world, 1, ShipClass.Merchant, -240, 30, 0, 6.5);
            var ai = new Opponent(0, 1);
            void Play(double seconds)
            {
                for (var i = 0; i < seconds * Rules.TickRate; i++) { ai.Think(world); world.Step(); world.Events.Clear(); }
            }

            Play(2);
            Assert.That(boarder.Order, Is.EqualTo(OrderKind.AttackShip), "Under way, faster than the boarder rows: shot at.");
            Assert.That(boarder.TargetId, Is.EqualTo(target.Id));

            world.Issue(new Command { Faction = 1, Kind = CommandKind.Stop, ShipIds = new[] { target.Id } });
            Play(6);
            Assert.That(target.Speed, Is.EqualTo(0).Within(1e-9), "The target lies still.");
            Assert.That(boarder.Order, Is.EqualTo(OrderKind.Board), "Seen lying still, it is boarded.");
            Assert.That(boarder.TargetId, Is.EqualTo(target.Id));

            var hooks = false;
            for (var i = 0; i < 60 * Rules.TickRate && !hooks; i++)
            {
                ai.Think(world);
                world.Step();
                world.Events.Clear();
                hooks =world.Boardings.Any(link => link.AttackerId == boarder.Id && link.TargetId == target.Id);
            }
            Assert.That(hooks, Is.True, "And the boarder comes alongside and throws its hooks.");
            Assert.That(world.FindShip(target.Id), Is.Not.Null, "The ship it could take is not sunk first.");
        }

        [Test]
        public void Opponent_TheSpeedOfATargetItDoesNotSeeChangesNothing()
        {
            // Q06: whether a boarding goes on was decided by the target's speed even when the side no longer saw it. Two
            // matches that differ only in what a hidden ship does must get the same orders.
            string Orders(double hiddenSpeed)
            {
                var world = Empty();
                var boarder = world.SpawnShip(0, ShipClass.Boarder, -300, 0);
                var target = world.SpawnShip(1, ShipClass.Merchant, 0, 300);
                target.Speed = hiddenSpeed;
                boarder.Order = OrderKind.Board;
                boarder.TargetId = target.Id;
                Assert.That(world.CanSee(0, target), Is.False, "The target is out of sight.");
                new Opponent(0, 1).Think(world);
                return string.Join(";", world.Pending.Select(command => $"{command.Kind} {string.Join(",", command.ShipIds)} > {command.TargetId} ({command.X:F1},{command.Y:F1})"));
            }
            Assert.That(Orders(6.5), Is.EqualTo(Orders(0)), "Lying still or making off unseen, the same orders.");
        }

        [Test]
        public void Opponent_GivesNoOrderToShootToAShipWithNoPostThatCanFire()
        {
            // J02: the rule refuses an order to shoot to a ship with no post that can fire; the opponent does not give
            // one, so it gets no refusal to repeat every second. A manned ship beside it is sent as before.
            // No silver at home: the ship is not called back to muster a crew, it stays with the fleet as it is.
            var world = Empty();
            world.SettlementOf(0).Silver = 0;
            var unmanned = world.SpawnShip(0, ShipClass.CrewDestroyer, -300, 0);
            unmanned.Archers = 0;
            var manned = world.SpawnShip(0, ShipClass.CrewDestroyer, -300, 40);
            var enemy = world.SpawnShip(1, ShipClass.Merchant, -180, 0);
            enemy.Hull = 1e6;
            var ai = new Opponent(0, 1);
            var refusals = 0;
            for (var i = 0; i < 5 * Rules.TickRate; i++)
            {
                ai.Think(world);
                Assert.That(world.Pending.Any(command => command.Kind == CommandKind.AttackShip && command.ShipIds.Contains(unmanned.Id)), Is.False, "No order to shoot for the ship that cannot.");
                world.Step();
                refusals += world.CommandResults.Count(item => item.Faction == 0 && item.Outcome == CommandOutcome.Rejected);
                world.Events.Clear();
            }
            Assert.That(refusals, Is.Zero, "And so no refused order.");
            Assert.That((manned.Order, manned.TargetId), Is.EqualTo((OrderKind.AttackShip, enemy.Id)), "The manned ship attacks.");
            Assert.That(unmanned.Order, Is.Not.EqualTo(OrderKind.AttackShip));

            var port = Empty();
            port.SettlementOf(0).Silver = 0;
            var atPort = port.SpawnShip(0, ShipClass.CrewDestroyer, 420, 0);
            atPort.Archers = 0;
            new Opponent(0, 1).Think(port);
            Assert.That(port.Pending.Any(command => command.Kind == CommandKind.AttackSettlement), Is.False, "Nor an order to shoot at the port it sees.");
        }

        [Test]
        public void Opponent_TheRegroupThresholdIsHalfOfEveryPlansFighters()
        {
            foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
            {
                if (ShipCatalog.Get(shipClass).IsEconomic) continue;
                foreach (CrewPreset preset in Enum.GetValues(typeof(CrewPreset)))
                {
                    if (preset == CrewPreset.Custom) continue;
                    var plan = ShipCatalog.Get(shipClass).PlanFor(preset);
                    if (plan.Soldiers == 0) continue;
                    bool Regroups(int fighters)
                    {
                        var world = Empty();
                        var ship = world.SpawnShip(0, shipClass, -400, 0);
                        ship.Plan = plan;
                        ship.Swordsmen = fighters; ship.Javelins = 0; ship.Archers = 0;
                        new Opponent(0).Think(world);
                        return world.Pending.Any(command => command.Kind == CommandKind.Repair && command.ShipIds.Contains(ship.Id));
                    }
                    var below = (plan.Soldiers - 1) / 2;
                    Assert.That(Regroups(below), Is.True, $"{shipClass} {preset}: {below} of {plan.Soldiers} fighters left.");
                    Assert.That(Regroups(below + 1), Is.False, $"{shipClass} {preset}: {below + 1} of {plan.Soldiers} fighters left.");
                }
            }
        }

        [Test]
        public void Opponent_RegroupsOnlyWhereTheMusterCanBePaidAndNeverOutOfABoarding()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            // A bridgehead with a shipyard lies nearer the spent ship than home, and has no silver for its muster.
            var bridgehead = new Settlement { Id = world.NextId++, Owner = 0, X = -300, Y = 0, IsOutpost = true, Hull = Rules.OutpostHull };
            world.Settlements.Add(bridgehead);
            var ship = world.SpawnShip(0, ShipClass.Boarder, -320, 0);
            ship.Swordsmen = 10; ship.Javelins = 0; ship.Archers = 0;
            Assert.That(world.RepairYard(ship), Is.SameAs(bridgehead));
            Assert.That(home.Available(ResourceType.Silver), Is.GreaterThan(World.MusterCost(ship) + Rules.ReserveSilver), "Home could pay it.");
            new Opponent(0).Think(world);
            Assert.That(world.Pending.Any(command => command.Kind == CommandKind.Repair), Is.False, "The yard it would sail to cannot: it stays with the fleet.");
            bridgehead.Silver = 1500 * Rules.Centi;
            new Opponent(0).Think(world);
            Assert.That(world.Pending.Count(command => command.Kind == CommandKind.Repair && command.ShipIds.Contains(ship.Id)), Is.EqualTo(1), "Now the yard can pay.");

            // Locked in a boarding it fights it out, however many it has lost: regrouping would cast off the hooks.
            var other = Empty();
            var boarder = other.SpawnShip(0, ShipClass.Boarder, -400, 0);
            var enemy = other.SpawnShip(1, ShipClass.Ram, -380, 0);
            boarder.Swordsmen = 10; boarder.Javelins = 0; boarder.Archers = 0;
            other.Boardings.Add(new BoardingLink { AttackerId = boarder.Id, TargetId = enemy.Id, Hooked = true });
            new Opponent(0).Think(other);
            Assert.That(other.Pending.Any(command => command.Kind == CommandKind.Repair && command.ShipIds.Contains(boarder.Id)), Is.False);
        }

        // ---------- Order results (J02, J08) ----------

        static (CommandOutcome, CommandReason) ResultOf(World world, int commandId, int shipId = -1)
        {
            var result = world.CommandResults.Single(item => item.CommandId == commandId && item.ShipId == shipId);
            return (result.Outcome, result.Reason);
        }

        [Test]
        public void Order_EveryShipGetsAResultAndARefusalSaysWhyAndChangesNothing()
        {
            var world = Empty();
            var loaded = world.SpawnShip(0, ShipClass.Cutter, -420, 0);
            loaded.CargoType = ResourceType.Wood;
            loaded.Cargo = 30 * Rules.Centi;
            loaded.Order = OrderKind.Move;
            loaded.TargetX = -300;
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -420, 60);
            merchant.HoldSilver = 40 * Rules.Centi;
            var empty = world.SpawnShip(0, ShipClass.Cutter, -420, -60);
            var boarder = world.SpawnShip(0, ShipClass.Boarder, -380, 0);
            boarder.Swordsmen = 5; boarder.Javelins = 0; boarder.Archers = 0;
            var whole = world.SpawnShip(0, ShipClass.Ram, -380, 80);
            var holed = world.SpawnShip(0, ShipClass.Ram, -380, -80);
            holed.Hull -= 100;
            var enemy = world.SpawnShip(1, ShipClass.Ram, -330, 0);
            Assert.That(world.CanSee(0, enemy), Is.True);
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);

            var scrap = world.Issue(new Command { Faction = 0, Kind = CommandKind.Scrap, ShipIds = new[] { loaded.Id, merchant.Id, empty.Id } });
            var board = world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { empty.Id, boarder.Id }, TargetId = enemy.Id });
            var foreign = world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { enemy.Id, 9999 }, X = 0, Y = 0 });
            var repair = world.Issue(new Command { Faction = 0, Kind = CommandKind.Repair, ShipIds = new[] { whole.Id, holed.Id } });
            world.Step();

            Assert.That(ResultOf(world, scrap, loaded.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.CarriesCargo)), "A01: a cutter with cargo is not scrapped.");
            Assert.That(ResultOf(world, scrap, merchant.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.CarriesCargo)), "Nor a merchant with goods in its hold.");
            Assert.That(ResultOf(world, scrap, empty.Id), Is.EqualTo((CommandOutcome.Accepted, CommandReason.None)), "An empty hull in the same order is.");
            Assert.That(loaded.Order, Is.EqualTo(OrderKind.Move), "A refused order leaves the ship's own order.");
            Assert.That((loaded.Cargo, merchant.HoldSilver), Is.EqualTo((30 * Rules.Centi, 40 * Rules.Centi)));
            Assert.That(ResultOf(world, board, empty.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NotCapable)));
            Assert.That(ResultOf(world, board, boarder.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoCrew)));
            Assert.That(ResultOf(world, foreign, enemy.Id), Is.EqualTo(ResultOf(world, foreign, 9999)), "An enemy ship and no ship at all get the same answer.");
            Assert.That(ResultOf(world, foreign, 9999), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoUnit)));
            Assert.That(ResultOf(world, repair, whole.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NothingToDo)));
            Assert.That(ResultOf(world, repair, holed.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoDock)), "Only a dock mends a hull, and the side has none.");
            Assert.That(world.CommandResults.Count, Is.EqualTo(9), "One result for every ship of every order.");
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));

            world.Step();
            Assert.That(world.CommandResults, Is.Empty, "Results belong to the step that applied the orders.");
        }

        [Test]
        public void Order_ARefusalUnderFogIsTheSameForAHiddenAMissingAndAFallenTarget()
        {
            var world = Empty();
            var archer = world.SpawnShip(0, ShipClass.CrewDestroyer, -480, 400);
            var hidden = world.SpawnShip(1, ShipClass.Cutter, 480, -400);
            var enemyPort = world.SettlementOf(1);
            Assert.That(world.CanSee(0, hidden), Is.False);
            Assert.That(world.CanSee(0, enemyPort.X, enemyPort.Y), Is.False);
            Command Attack(CommandKind kind, int target) => new Command { Faction = 0, Kind = kind, ShipIds = new[] { archer.Id }, TargetId = target };

            var atHidden = world.Issue(Attack(CommandKind.AttackShip, hidden.Id));
            var atMissing = world.Issue(Attack(CommandKind.AttackShip, 9999));
            var atStandingPort = world.Issue(Attack(CommandKind.AttackSettlement, enemyPort.Id));
            var atMissingPort = world.Issue(Attack(CommandKind.AttackSettlement, 9998));
            world.Step();
            var answer = (CommandOutcome.Rejected, CommandReason.NoObservedTarget);
            Assert.That(ResultOf(world, atHidden, archer.Id), Is.EqualTo(answer));
            Assert.That(ResultOf(world, atMissing, archer.Id), Is.EqualTo(answer), "A ship that is not there is refused as a hidden one.");
            Assert.That(ResultOf(world, atStandingPort, archer.Id), Is.EqualTo(answer));
            Assert.That(ResultOf(world, atMissingPort, archer.Id), Is.EqualTo(answer));

            enemyPort.Hull = 0;
            var atFallenPort = world.Issue(Attack(CommandKind.AttackSettlement, enemyPort.Id));
            world.Step();
            Assert.That(ResultOf(world, atFallenPort, archer.Id), Is.EqualTo(answer), "A hidden port's fall is not told by the refusal.");
            Assert.That(archer.Order, Is.EqualTo(OrderKind.Idle));
        }

        [Test]
        public void Order_PortOrdersReportWhyTheyAreRefusedAndARefusalCostsNothing()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var enemyPort = world.SettlementOf(1);
            // A hull on each side keeps the match going from step to step.
            world.SpawnShip(0, ShipClass.Cutter, -420, 0);
            world.SpawnShip(1, ShipClass.Cutter, 420, 0);
            home.HasShipyard = false;
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);
            Assert.That(home.HasWorkshop, Is.False, "A bare port: no shipyard, no workshop.");

            var warship = world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = home.Id });
            var abroad = world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter, TargetId = enemyPort.Id });
            var noProject = world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelProject, TargetId = home.Id });
            var noWorkshop = world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueWeapon, Weapon = WeaponType.Scorpion, TargetId = home.Id });
            var badIndex = world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 3, TargetId = home.Id });
            world.Step();
            Assert.That(ResultOf(world, warship), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NotCapable)), "A warship needs a shipyard.");
            Assert.That(ResultOf(world, abroad), Is.EqualTo((CommandOutcome.Rejected, CommandReason.InvalidTarget)), "Not the side's port.");
            Assert.That(ResultOf(world, noProject), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NothingToDo)));
            Assert.That(ResultOf(world, noWorkshop), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NotCapable)));
            Assert.That(ResultOf(world, badIndex), Is.EqualTo((CommandOutcome.Rejected, CommandReason.InvalidOrder)));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
            Assert.That(home.ReservedWood + home.ReservedSilver, Is.Zero, "Nothing reserved for refused orders.");

            var stock = (home.Wood, home.Silver);
            home.Wood = 10 * Rules.Centi;
            var poor = world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter, TargetId = home.Id });
            world.Step();
            Assert.That(ResultOf(world, poor), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoFunds)));
            Assert.That(home.PortQueue, Is.Empty);

            (home.Wood, home.Silver) = stock;
            var taken = world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter, TargetId = home.Id });
            world.Step();
            Assert.That(ResultOf(world, taken), Is.EqualTo((CommandOutcome.Accepted, CommandReason.None)));
            Assert.That(home.PortQueue.Count, Is.EqualTo(1));
        }

        [Test]
        public void Order_ATransferOfANegativeLoadIsRefusedAndBooksNothing()
        {
            var world = Empty();
            var home = world.SettlementOf(0);
            var outpost = new Settlement { Id = world.NextId++, Owner = 0, X = -200, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull };
            world.Settlements.Add(outpost);
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -420, 0);
            var available = home.Available(ResourceType.Silver);
            var id = world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, ShipIds = new[] { merchant.Id }, TargetId = outpost.Id, SourceId = home.Id, Wood = 100 * Rules.Centi, Silver = -50 * Rules.Centi });
            world.Step();
            Assert.That(ResultOf(world, id, merchant.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.InvalidOrder)));
            Assert.That(home.ReservedWood + home.ReservedSilver, Is.Zero, "No reservation, least of all a negative one.");
            Assert.That(home.Available(ResourceType.Silver), Is.EqualTo(available));
            Assert.That(merchant.Order, Is.Not.EqualTo(OrderKind.Transfer));
        }

        [Test]
        public void Order_AScrapRefusedForItsCargoIsRefusedAlikeAfterASaveInAPause()
        {
            var world = Empty();
            var merchant = world.SpawnShip(0, ShipClass.Merchant, -420, 0);
            merchant.HoldWood = 50 * Rules.Centi;
            var id = world.Issue(new Command { Faction = 0, Kind = CommandKind.Scrap, ShipIds = new[] { merchant.Id } });
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            world.Step();
            loaded.Step();
            var refused = (CommandOutcome.Rejected, CommandReason.CarriesCargo);
            Assert.That(ResultOf(world, id, merchant.Id), Is.EqualTo(refused));
            Assert.That(ResultOf(loaded, id, merchant.Id), Is.EqualTo(refused), "The saved order meets the same rule after loading.");
            Assert.That(loaded.FindShip(merchant.Id).HoldWood, Is.EqualTo(50 * Rules.Centi));
            Assert.That(loaded.FindShip(merchant.Id).Order, Is.Not.EqualTo(OrderKind.Scrap));
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
            // A file of the format before pending orders were saved is refused, not read as if it had none.
            var old = SaveGame.Serialize(MapFactory.CreateSkirmish(1));
            old[4] = 18;
            var refused = Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(old, out _));
            Assert.That(refused.Message, Does.Contain("18"));
            // Nor one of the format before the three owner slots (D3): refused by its number, with no migration.
            Assert.That(SaveGame.Version, Is.EqualTo(20));
            var before = SaveGame.Serialize(MapFactory.CreateSkirmish(1));
            before[4] = 19;
            refused = Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(before, out _));
            Assert.That(refused.Message, Does.Contain("19").And.Contain("20"), "The refusal names the file's version and the one this build reads.");
        }

        [Test]
        public void Save_InAPauseKeepsThePendingOrdersAndContinuesIdentically()
        {
            // Orders given in a pause wait in Pending; saving takes no step (J02/J10). The loaded game applies them at the
            // same step as the continued one, so both go on identically, the AI's later orders included.
            var (world, first, second) = Match(5);
            Advance(world, first, second, 1500);
            var home = world.SettlementOf(0);
            var own = world.Ships.Where(ship => ship.Owner == 0).OrderBy(ship => ship.Id).ToList();
            Assert.That(own.Count, Is.GreaterThanOrEqualTo(2));
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { own[0].Id }, X = home.X + 120, Y = home.Y + 80 });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Stop, ShipIds = new[] { own[1].Id } });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = home.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPortMinimum, TargetId = home.Id, Wood = 100 * Rules.Centi, Silver = 50 * Rules.Centi });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = home.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetCrewPlan, ShipIds = new[] { own[1].Id }, Preset = CrewPreset.Custom, Plan = new CrewPlan { Sailors = 4, Swordsmen = 2, Javelins = 1, Archers = 1 } });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SaveBlueprint, Class = ShipClass.Ram, Name = "Wzór", Loadout = new[] { WeaponType.ArcherBank, WeaponType.Scorpion } });
            var tick = world.Tick;
            var pending = world.Pending.Select(command => (command.Id, command.Kind)).ToList();

            var data = SaveGame.Serialize(world, second.BuildIndex);
            Assert.That(world.Tick, Is.EqualTo(tick), "Saving takes no step.");
            Assert.That(world.Pending.Select(command => (command.Id, command.Kind)), Is.EqualTo(pending), "The orders still wait.");
            var loaded = SaveGame.Deserialize(data, out var build);
            Assert.That(loaded.Pending.Select(command => (command.Id, command.Kind)), Is.EqualTo(pending), "Loaded in the order given, with their numbers.");
            Assert.That(SaveGame.Serialize(loaded, build), Is.EqualTo(data), "Every field of every order comes back.");

            var loadedFirst = new Opponent(0) { BuildIndex = first.BuildIndex };
            var loadedSecond = new Opponent(1) { BuildIndex = build };
            Advance(world, first, second, 3000);
            Advance(loaded, loadedFirst, loadedSecond, 3000);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
            Assert.That(loaded.LedgerTotal(ResourceType.Silver), Is.EqualTo(world.LedgerTotal(ResourceType.Silver)));
            Assert.That(loaded.SettlementOf(0).KeepWood, Is.EqualTo(100 * Rules.Centi), "The pending minimum took effect after loading.");
        }

        [Test]
        public void Save_CancellingAShipOrderAndAProjectAfterLoadingSettlesAsWithoutTheSave()
        {
            // Q02: a ship order and a project under way go through a save; cancelled afterwards, they give back exactly
            // what the continued game gives back, and nothing is made or lost on the way.
            var world = Empty();
            world.SpawnShip(0, ShipClass.Cutter, -420, 0);
            world.SpawnShip(1, ShipClass.Cutter, 420, 0);
            var home = world.SettlementOf(0);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = home.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.BuildProject, Project = ProjectKind.Workshop, TargetId = home.Id });
            Run(world, 6);
            Assert.That(home.WarQueue.Count, Is.EqualTo(1));
            Assert.That(home.Project, Is.Not.Null);
            Assert.That(home.WarQueue[0].Started && home.Project.Started, Is.True, "Both under way: a cancel now keeps back a share.");
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);

            var loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
            foreach (var game in new[] { world, loaded })
            {
                var port = game.SettlementOf(0);
                var cancelShip = game.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 0, PortQueue = false, TargetId = port.Id });
                var cancelProject = game.Issue(new Command { Faction = 0, Kind = CommandKind.CancelProject, TargetId = port.Id });
                game.Step();
                Assert.That(game.CommandResults.Where(item => item.CommandId == cancelShip || item.CommandId == cancelProject).All(item => item.Outcome == CommandOutcome.Accepted), Is.True);
                Assert.That(port.WarQueue, Is.Empty);
                Assert.That(port.Project, Is.Null);
                Assert.That(port.ReservedWood + port.ReservedSilver, Is.Zero, "Every reservation of the cancelled work is settled.");
            }
            var after = loaded.SettlementOf(0);
            Assert.That((after.Wood, after.Silver, loaded.LostWood, loaded.LostSilver), Is.EqualTo((home.Wood, home.Silver, world.LostWood, world.LostSilver)), "The same refund and the same loss as without the save.");
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood), "The ledger holds through the cancel.");
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
            Assert.That(loaded.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(loaded.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
        }

        [Test]
        public void Save_KeepsTheOrderOfConflictingPendingOrdersAndTheirNumbers()
        {
            Ship AfterLoad(CommandKind firstKind, CommandKind secondKind, out World loaded, out int lastId)
            {
                var world = Empty();
                var ship = world.SpawnShip(0, ShipClass.Cutter, -400, 0);
                Command Order(CommandKind kind) => new Command { Faction = 0, Kind = kind, ShipIds = new[] { ship.Id }, X = -300, Y = 120 };
                var firstId = world.Issue(Order(firstKind));
                lastId = world.Issue(Order(secondKind));
                Assert.That(lastId, Is.EqualTo(firstId + 1), "Each order gets the next number.");
                loaded = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
                loaded.Step();
                return loaded.FindShip(ship.Id);
            }

            Assert.That(AfterLoad(CommandKind.Move, CommandKind.Stop, out var a, out var aLast).Order, Is.EqualTo(OrderKind.Idle), "Move then Stop: the ship stops.");
            Assert.That(AfterLoad(CommandKind.Stop, CommandKind.Move, out _, out _).Order, Is.EqualTo(OrderKind.Move), "Stop then Move: the ship sails.");
            Assert.That(a.Issue(new Command { Faction = 0, Kind = CommandKind.Stop }), Is.EqualTo(aLast + 1), "Numbering goes on after loading.");
        }

        [Test]
        public void Save_EveryFieldOfAPendingOrderComesBack()
        {
            // Every public field of Command, set apart from its default, goes through the save. A field added to Command
            // and left out of SaveGame on both sides makes this fail.
            // Fields of one type each get a value of their own, so two of them written or read in each other's place fail
            // too. A bool has only two values: the fields alternate, and a second pass turns them all over, so each is
            // once apart from its default.
            var fields = typeof(Command).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var flip in new[] { false, true })
            {
                var given = new Dictionary<Type, int>();
                object Sample(Type type)
                {
                    given.TryGetValue(type, out var nth);
                    given[type] = nth + 1;
                    if (type == typeof(int)) return 17 + nth * 7;
                    if (type == typeof(long)) return 123456789L + nth * 1000;
                    if (type == typeof(double)) return -42.625 - nth * 3;
                    if (type == typeof(bool)) return (nth % 2 == 0) != flip;
                    if (type == typeof(string)) return "Wzór próbny";
                    if (type == typeof(int[])) return new[] { 3, 5, 8 };
                    if (type == typeof(WeaponType[])) return new[] { WeaponType.Scorpion, WeaponType.None, WeaponType.ArcherBank };
                    if (type == typeof(CrewPlan)) return new CrewPlan { Sailors = 3, Swordsmen = 4, Javelins = 5, Archers = 6, Preset = CrewPreset.Volley };
                    if (type.IsEnum) { var values = Enum.GetValues(type); return values.GetValue(values.Length - 1); }
                    Assert.Fail($"A Command field of type {type.Name} has no sample here: add one, and its place in SaveGame.");
                    return null;
                }

                var world = Empty();
                var command = new Command();
                foreach (var field in fields) if (field.Name != nameof(Command.Id)) field.SetValue(command, Sample(field.FieldType));
                // The side is one of the three owner slots, or the save is refused (format 20): the third, apart from the default.
                command.Faction = Owners.Independent;
                foreach (var type in given.Where(item => item.Value > 1 && item.Key != typeof(bool)).Select(item => item.Key))
                {
                    var values = fields.Where(field => field.FieldType == type && field.Name != nameof(Command.Id)).Select(field => field.GetValue(command)).ToList();
                    Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count), $"Every {type.Name} field has a value of its own.");
                }
                world.Issue(command);
                var copy = SaveGame.Deserialize(SaveGame.Serialize(world), out _).Pending.Single();
                foreach (var field in fields) Assert.That(field.GetValue(copy), Is.EqualTo(field.GetValue(command)), field.Name);
            }
            Assert.That(fields.Length, Is.GreaterThanOrEqualTo(21), "Every field of the order is compared.");
        }

        [Test]
        public void Save_ADamagedPendingOrderIsRefusedBeforeTheMatchIsReplaced()
        {
            // J10: a damaged order is caught when the file is read, so the running match stays; it never reaches a step.
            byte[] SavedWith(Action<World> damage)
            {
                var world = Empty();
                world.SpawnShip(0, ShipClass.Cutter, -420, 0);
                damage(world);
                return SaveGame.Serialize(world);
            }

            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(SavedWith(w => w.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = (ShipClass)99 })), out _), "A class beyond the catalogue.");
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(SavedWith(w => w.Issue(new Command { Faction = 0, Kind = CommandKind.QueueWeapon, Weapon = (WeaponType)42 })), out _), "A weapon beyond the catalogue.");
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(SavedWith(w => w.Issue(new Command { Faction = 0, Kind = CommandKind.SetLoadout, Loadout = new[] { WeaponType.ArcherBank, (WeaponType)77 } })), out _), "A planned post beyond the catalogue.");
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(SavedWith(w => w.Issue(new Command { Faction = 0, Kind = CommandKind.Move, X = double.NaN })), out _), "A position that is no number.");
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(SavedWith(w => w.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, Silver = -5000 })), out _), "A negative load.");
            Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(SavedWith(w => w.NextCommandId = 0), out _), "A broken order count.");
            var sound = SaveGame.Deserialize(SavedWith(w => w.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter })), out _);
            Assert.That(sound.Pending.Count, Is.EqualTo(1), "A sound order of the same kind still loads.");
        }

        // ---------- Format 20: the three owner slots ----------

        /// <summary>Where the save keeps the match's result and the side relations (format 20), checked against a save before use.</summary>
        const int OutcomeAt = 28, WinnerAt = 32, ExploredAt = 104;
        static readonly int RelationsAt = ExploredAt + World.FogCells * World.FogCells + 4 + 4 + 4 * Owners.SlotCount;

        [Test]
        public void Save_KeepsTheSideRelationsAndAThirdSidesPriorityPort()
        {
            for (var pairs = 0; pairs < 8; pairs++)
            {
                var world = Empty();
                world.Relations = new SideRelations(pairs);
                var data = SaveGame.Serialize(world);
                Assert.That(BitConverter.ToInt32(data, RelationsAt), Is.EqualTo(pairs), "The relations are saved as the bits of the three pairs.");
                var loaded = SaveGame.Deserialize(data, out _);
                Assert.That(loaded.Relations, Is.EqualTo(world.Relations), $"Hostile pairs {pairs}");
                Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));
            }
            Assert.That(SaveGame.Deserialize(SaveGame.Serialize(Empty()), out _).Relations, Is.EqualTo(SideRelations.AllHostile), "A match that says nothing is at war all round.");

            // A match of three sides, the human at peace with the third: after loading it goes on exactly as without the save.
            World Match()
            {
                var world = Empty();
                world.Relations = SideRelations.AllHostile.With(0, 2, false);
                var third = new Settlement { Id = world.NextId++, Owner = 2, X = 0, Y = 400, Wood = 500 * Rules.Centi, Silver = 500 * Rules.Centi };
                var thirdOutpost = new Settlement { Id = world.NextId++, Owner = 2, X = 0, Y = 150, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull };
                world.Settlements.Add(third);
                world.Settlements.Add(thirdOutpost);
                world.SpawnShip(0, ShipClass.CrewDestroyer, -40, 60);
                world.SpawnShip(1, ShipClass.CrewDestroyer, 60, 60);
                world.SpawnShip(2, ShipClass.CrewDestroyer, 10, 110);
                world.Issue(new Command { Faction = 2, Kind = CommandKind.SetPriorityPort, TargetId = thirdOutpost.Id });
                return world;
            }
            var straight = Match();
            var saved = Match();
            for (var i = 0; i < 5 * Rules.TickRate; i++) { straight.Step(); saved.Step(); }
            var middle = SaveGame.Serialize(saved);
            saved = SaveGame.Deserialize(middle, out _);
            Assert.That(saved.Relations, Is.EqualTo(SideRelations.AllHostile.With(0, 2, false)));
            Assert.That(saved.PriorityPort(2)?.IsOutpost, Is.True, "The third side's named port comes back.");
            Assert.That(saved.PriorityPorts, Is.EqualTo(straight.PriorityPorts));
            for (var i = 0; i < 40 * Rules.TickRate; i++) { straight.Step(); saved.Step(); }
            Assert.That(straight.Ships.Min(ship => ship.Hull), Is.LessThan(straight.Ships.Max(ship => ship.Definition.Hull)), "Shots were exchanged meanwhile.");
            Assert.That(SaveGame.Hash(SaveGame.Serialize(saved)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(straight))));
        }

        [Test]
        public void Save_RefusesOwnersMasksRelationsAndResultsOutsideTheThreeSlots()
        {
            // J10: what names a slot that is not is caught when the file is read, before the running match is replaced.
            World Sound()
            {
                var world = Empty();
                world.SpawnShip(0, ShipClass.Cutter, -420, 0);
                world.SpawnShip(2, ShipClass.Ram, 0, 300);
                world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 2, X = 0, Y = 400 });
                AddNode(world, ResourceType.Wood, -300, 100, 500).KnownMask = 0b111;
                world.Blueprints.Add(new Blueprint { Id = world.NextBlueprintId++, Owner = 2, Name = "Wzor", Class = ShipClass.Ram, Loadout = new[] { WeaponType.None, WeaponType.None } });
                world.Projectiles.Add(new Projectile { Id = world.NextProjectileId++, OwnerId = 2, ShooterId = -1, Weapon = WeaponType.ArcherBank, ArriveTick = 900 });
                world.HeavyArmsResearched = 0b111;
                world.Explored[5] = 0b111;
                world.Issue(new Command { Faction = 2, Kind = CommandKind.Stop, ShipIds = new int[0] });
                return world;
            }
            byte[] SavedWith(Action<World> damage)
            {
                var world = Sound();
                damage(world);
                return SaveGame.Serialize(world);
            }
            byte[] Patched(int at, int value)
            {
                var data = SaveGame.Serialize(Sound());
                BitConverter.GetBytes(value).CopyTo(data, at);
                return data;
            }
            void Refused(string what, byte[] data) => Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(data, out _), what);

            var sound = SaveGame.Deserialize(SaveGame.Serialize(Sound()), out _);
            Assert.That(sound.Ships.Select(ship => ship.Owner), Is.EqualTo(new[] { 0, 2 }), "All three slots, their masks and orders load.");
            Assert.That((sound.HeavyArmsResearched, sound.Explored[5], sound.Pending.Single().Faction), Is.EqualTo((7, (byte)7, 2)));

            foreach (var owner in new[] { -1, 3, 99 })
            {
                Refused($"A ship of slot {owner}", SavedWith(w => w.Ships[0].Owner = owner));
                Refused($"A port of slot {owner}", SavedWith(w => w.Settlements[0].Owner = owner));
                Refused($"A blueprint of slot {owner}", SavedWith(w => w.Blueprints[0].Owner = owner));
                Refused($"A shot of slot {owner}", SavedWith(w => w.Projectiles[0].OwnerId = owner));
            }
            Refused("A deposit known to a fourth slot", SavedWith(w => w.Nodes[0].KnownMask = 0b1000));
            Refused("Research of a fourth slot", SavedWith(w => w.HeavyArmsResearched = 0b1001));
            Refused("Fog explored by a fourth slot", SavedWith(w => w.Explored[World.FogCells * World.FogCells - 1] = 0b1000));

            // A pending order of such a slot is never written (see the test below): the file is damaged by hand, at the
            // one place where saves of two legal sides differ.
            var bySecond = SavedWith(w => w.Pending[0].Faction = 1);
            var byThird = SaveGame.Serialize(Sound());
            var factionAt = Enumerable.Range(0, byThird.Length).Where(i => bySecond[i] != byThird[i]).ToList();
            Assert.That(factionAt.Count, Is.EqualTo(1), "Only the order's side differs.");
            Assert.That(BitConverter.ToInt32(byThird, factionAt[0]), Is.EqualTo(2));
            foreach (var owner in new[] { -1, 3, 99 }) Refused($"A pending order of slot {owner}", Patched(factionAt[0], owner));
            Assert.That(SaveGame.Deserialize(Patched(factionAt[0], 1), out _).Pending.Single().Faction, Is.EqualTo(1), "A sound side in the same place loads.");

            Assert.That(BitConverter.ToInt32(SaveGame.Serialize(Sound()), RelationsAt), Is.EqualTo(SideRelations.AllHostile.HostilePairs), "The patch below lands on the relations.");
            Refused("A fourth pair at war", Patched(RelationsAt, 0b1000));
            Refused("Relations that are no bits", Patched(RelationsAt, -1));
            Assert.That(SaveGame.Deserialize(Patched(RelationsAt, 0b010), out _).Relations, Is.EqualTo(new SideRelations(0b010)), "A sound value in the same place loads.");
        }

        [Test]
        public void Save_APendingOrderOfASlotThatIsNotIsNotWritten()
        {
            // Taking an order costs nothing and checks nothing, so one of a slot that is not can wait in a pause. Reading
            // would refuse it: the save is not made at all, rather than made unreadable. Once the step has answered the
            // order, the match saves and loads as ever.
            foreach (var faction in new[] { -1, 3 })
            {
                var world = Empty();
                var cutter = world.SpawnShip(0, ShipClass.Cutter, -420, 0);
                var sound = SaveGame.Serialize(world);
                world.Issue(new Command { Faction = faction, Kind = CommandKind.Stop, ShipIds = new[] { cutter.Id } });
                var refused = Assert.Throws<ArgumentOutOfRangeException>(() => SaveGame.Serialize(world), $"A pending order of slot {faction}");
                Assert.That(refused.Message, Does.Contain(faction.ToString()));
                Assert.That(SaveGame.Deserialize(sound, out _).Ships.Count, Is.EqualTo(1), "What was saved before is as good as it was.");
                world.Step();
                Assert.That(world.CommandResults.Single().Reason, Is.EqualTo(CommandReason.NoUnit), "The step answers the order as before.");
                Assert.That(SaveGame.Deserialize(SaveGame.Serialize(world), out _).Tick, Is.EqualTo(world.Tick), "And the match saves again.");
            }
        }

        [Test]
        public void Save_AWonMatchNamesAMainSideAndADrawNobody()
        {
            // D7: -1 says nobody has won; a save where the result and the winner disagree is refused.
            byte[] Result(OutcomeKind outcome, int winner)
            {
                var world = Empty();
                world.Outcome = outcome;
                world.Winner = winner;
                var data = SaveGame.Serialize(world);
                Assert.That((BitConverter.ToInt32(data, OutcomeAt), BitConverter.ToInt32(data, WinnerAt)), Is.EqualTo(((int)outcome, winner)));
                return data;
            }
            foreach (var (outcome, winner) in new[] { (OutcomeKind.InProgress, -1), (OutcomeKind.Draw, -1), (OutcomeKind.Won, 0), (OutcomeKind.Won, 1) })
            {
                var loaded = SaveGame.Deserialize(Result(outcome, winner), out _);
                Assert.That((loaded.Outcome, loaded.Winner), Is.EqualTo((outcome, winner)));
            }
            foreach (var (outcome, winner) in new[]
                     {
                         (OutcomeKind.Won, -1), (OutcomeKind.Won, 2), (OutcomeKind.Won, 3),
                         (OutcomeKind.InProgress, 0), (OutcomeKind.InProgress, 2), (OutcomeKind.InProgress, -2),
                         (OutcomeKind.Draw, 0), (OutcomeKind.Draw, 1), ((OutcomeKind)7, -1), ((OutcomeKind)(-1), -1),
                     })
                Assert.Throws<InvalidDataException>(() => SaveGame.Deserialize(Result(outcome, winner), out _), $"{outcome} with winner {winner}");

            // What the game itself writes when a match ends is sound: a win, and a draw with the third side still afloat.
            var won = Empty();
            won.SpawnShip(2, ShipClass.Ram, 0, 300);
            won.SettlementOf(1).Hull = 0;
            won.Step();
            var afterWin = SaveGame.Deserialize(SaveGame.Serialize(won), out _);
            Assert.That((afterWin.Outcome, afterWin.Winner), Is.EqualTo((OutcomeKind.Won, Owners.Human)));
            var drawn = Empty();
            drawn.SpawnShip(2, ShipClass.Ram, 0, 300);
            drawn.SettlementOf(0).Hull = 0;
            drawn.SettlementOf(1).Hull = 0;
            drawn.Step();
            var afterDraw = SaveGame.Deserialize(SaveGame.Serialize(drawn), out _);
            Assert.That((afterDraw.Outcome, afterDraw.Winner), Is.EqualTo((OutcomeKind.Draw, -1)));
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
        public void Deposits_RenewWhenLeftAloneSoTheyNeverEndTheGame()
        {
            var world = MapFactory.CreateSkirmish(1);
            // A contested central point no one works at the start.
            var node = world.Nodes.First(item => item.Type == ResourceType.Silver && item.KnownMask == 0);
            node.Stock = node.Capacity / 2;
            node.LastWorkedTick = world.Tick;
            var half = node.Stock;
            var ledger = world.LedgerTotal(ResourceType.Silver);
            Run(world, Rules.RegrowDelaySeconds - 5);
            Assert.That(node.Stock, Is.EqualTo(half), "Nothing grows back while the point was worked recently.");
            Run(world, 65);
            Assert.That(node.Stock, Is.GreaterThan(half), "Left alone, it grows back.");
            Assert.That(node.Stock - half, Is.EqualTo(world.NodeCap(node) * Rules.RegrowShare * 60 * Rules.Centi).Within(Rules.Centi), "At its regrowth rate.");
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(ledger), "The ledger counts the growth.");

            node.Stock = node.Capacity - 1;
            Run(world, 10);
            Assert.That(node.Stock, Is.EqualTo(node.Capacity), "Never beyond its starting stock.");
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
        public void Map_LandIsMostlyWorkPointsAndEveryBerthStaysOpen()
        {
            // Area inside a waterline given as radii at evenly spaced angles.
            double Area(Island island)
            {
                if (island.Outline == null) return Math.PI * island.Radius * island.Radius;
                var step = 2 * Math.PI / island.Outline.Length;
                var sum = 0.0;
                for (var i = 0; i < island.Outline.Length; i++) sum += 0.5 * island.Outline[i] * island.Outline[(i + 1) % island.Outline.Length] * Math.Sin(step);
                return sum;
            }

            foreach (var length in new[] { MatchLength.Short, MatchLength.Standard, MatchLength.Long })
            {
                var world = MapFactory.CreateSkirmish(1, length);
                Assert.That(world.Islands.Any(item => item.Kind == IslandKind.Hinterland), Is.False, $"{length}: no land that is only scenery.");
                var rocks = world.Islands.Where(item => item.Kind == IslandKind.Coast).ToList();
                Assert.That(rocks.Count, Is.EqualTo(5), $"{length}: five small rocks.");

                // Most land is forests to fell and silver to mine; the rocks are a small share of it.
                var workLand = world.Islands.Where(item => item.Kind == IslandKind.ResourceSite).Sum(Area);
                var otherLand = world.Islands.Where(item => item.Kind != IslandKind.ResourceSite).Sum(Area);
                Assert.That(rocks.Sum(Area), Is.LessThanOrEqualTo(workLand * 0.25), $"{length}: rocks against work points.");
                Assert.That(otherLand, Is.LessThanOrEqualTo(workLand * 1.5), $"{length}: ports, free shores and rocks against work points.");

                foreach (var rock in rocks)
                {
                    Assert.That(rock.Radius, Is.LessThanOrEqualTo(24), "A small rock.");
                    Assert.That(MapFactory.KeepsApproaches(world, rock), Is.True, $"{length}: the rock at {rock.X:0},{rock.Y:0} leaves every approach and a full channel or a firm join.");
                    Assert.That(rocks.Any(other => Math.Abs(other.X + rock.X) < 1e-6 && Math.Abs(other.Y + rock.Y) < 1e-6), Is.True, "Point-symmetric for both players.");
                    foreach (var settlement in world.Settlements)
                        Assert.That(LandShape.SignedDistance(rock, settlement.X, settlement.Y), Is.GreaterThanOrEqualTo(MapFactory.BerthClearance - 1e-9));
                    foreach (var site in world.Sites)
                        Assert.That(LandShape.SignedDistance(rock, site.X, site.Y), Is.GreaterThanOrEqualTo(MapFactory.BerthClearance - 1e-9));
                    foreach (var node in world.Nodes)
                        Assert.That(LandShape.SignedDistance(rock, node.X, node.Y), Is.GreaterThanOrEqualTo(MapFactory.BerthClearance - 1e-9));
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
