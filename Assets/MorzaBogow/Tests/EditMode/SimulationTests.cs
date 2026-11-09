using System;
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
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Bronze = 2000 * Rules.Centi, Rations = 2000 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Bronze = 2000 * Rules.Centi, Rations = 2000 * Rules.Centi });
            return world;
        }

        static ResourceNode AddNode(World world, ResourceType type, double x, double y, long units)
        {
            var node = new ResourceNode { Id = world.NextId++, Type = type, X = x, Y = y, Stock = units * Rules.Centi, Capacity = units * Rules.Centi, KnownMask = 3 };
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

        // ---------- Economy ----------

        [Test]
        public void Harvest_BronzeReachesSettlementOnlyAfterRealDelivery()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var node = AddNode(world, ResourceType.Bronze, -400, 0, 3000);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            var start = settlement.Bronze;
            Harvest(world, ship, node);

            Run(world, 30);
            Assert.That(ship.Cargo, Is.GreaterThan(0));
            Assert.That(settlement.Bronze, Is.EqualTo(start), "Cargo in a hold is not spendable.");

            Run(world, 120);
            Assert.That(settlement.Bronze, Is.GreaterThan(start));
        }

        [Test]
        public void Harvest_ConservesBronzeAcrossNodeShipAndSettlement()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var node = AddNode(world, ResourceType.Bronze, -400, 0, 3000);
            var ship = world.SpawnShip(0, ShipClass.Cutter, -470, 0);
            var total = node.Stock + ship.Cargo + settlement.Bronze;
            Harvest(world, ship, node);
            for (var i = 0; i < 4000; i++)
            {
                world.Step();
                Assert.That(node.Stock + ship.Cargo + settlement.Bronze, Is.EqualTo(total));
            }
        }

        [Test]
        public void Node_SharesWorkCapBetweenFourCutters()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Bronze, 0, 0, 3000);
            var cutters = Enumerable.Range(0, 4).Select(i => world.SpawnShip(0, ShipClass.Cutter, i, 0)).ToArray();
            foreach (var ship in cutters) Harvest(world, ship, node);
            var before = node.Stock;
            Run(world, 10);
            var perSecond = (before - node.Stock) / (double)Rules.Centi / 10.0;
            Assert.That(perSecond, Is.EqualTo(Rules.BronzeNodeCap).Within(0.5));
        }

        [Test]
        public void ShellBed_ClosesBelowTenPercentAndReopensAtThirty()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Rations, 0, 0, 2000);
            node.Stock = 150 * Rules.Centi;
            world.Step();
            Assert.That(node.Closed, Is.True);
            node.Stock = 500 * Rules.Centi;
            world.Step();
            Assert.That(node.Closed, Is.True, "Hysteresis keeps the bed closed below 30%.");
            node.Stock = 600 * Rules.Centi;
            world.Step();
            Assert.That(node.Closed, Is.False);
        }

        [Test]
        public void ShellBed_RegeneratesEvenFromZero()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Rations, 0, 0, 2000);
            node.Stock = 0;
            Run(world, 10);
            Assert.That(node.Stock, Is.GreaterThan(0));
        }

        [Test]
        public void Bronze_DoesNotRegenerate()
        {
            var world = Empty();
            var node = AddNode(world, ResourceType.Bronze, 0, 0, 3000);
            node.Stock = 100 * Rules.Centi;
            Run(world, 60);
            Assert.That(node.Stock, Is.EqualTo(100 * Rules.Centi));
        }

        [Test]
        public void Port_ServesTwoShipsAndMakesTheThirdWait()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var ships = Enumerable.Range(0, 3).Select(i =>
            {
                var ship = world.SpawnShip(0, ShipClass.Cutter, settlement.X + 5 + i, 0);
                ship.Order = OrderKind.Harvest;
                ship.Harvest = HarvestPhase.ToPort;
                ship.CargoType = ResourceType.Bronze;
                ship.Cargo = 160 * Rules.Centi;
                return ship;
            }).ToArray();

            Run(world, 1);
            Assert.That(settlement.DockedShips.Count, Is.EqualTo(Rules.StationCount));
            Assert.That(ships.Count(ship => ship.Harvest == HarvestPhase.Docked), Is.EqualTo(2));
        }

        [Test]
        public void Settlement_FullStoreLeavesCargoOnTheShip()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.Bronze = Rules.SettlementCapacity;
            var ship = world.SpawnShip(0, ShipClass.Cutter, settlement.X, 0);
            ship.Order = OrderKind.Harvest;
            ship.Harvest = HarvestPhase.ToPort;
            ship.Cargo = 50 * Rules.Centi;
            Run(world, 5);
            Assert.That(ship.Cargo, Is.EqualTo(50 * Rules.Centi));
            Assert.That(settlement.Bronze, Is.EqualTo(Rules.SettlementCapacity));
        }

        // ---------- Production ----------

        [Test]
        public void Queue_ChargesTemplateOnceAndBuildsAfterItsTime()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var bronze = settlement.Bronze;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram });
            world.Step();
            Assert.That(bronze - settlement.Bronze, Is.EqualTo(860 * Rules.Centi), "800 hull + 60 light weapon.");
            Run(world, 49);
            Assert.That(world.Ships.Count(ship => ship.Class == ShipClass.Ram), Is.EqualTo(0));
            Run(world, 2);
            Assert.That(world.Ships.Count(ship => ship.Class == ShipClass.Ram), Is.EqualTo(1));
            Assert.That(bronze - settlement.Bronze, Is.EqualTo(860 * Rules.Centi));
        }

        [Test]
        public void Queue_RejectsOrderThatLocalStoreCannotPay()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            settlement.Bronze = 100 * Rules.Centi;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
            world.Step();
            Assert.That(settlement.Queue, Is.Empty);
            Assert.That(settlement.Bronze, Is.EqualTo(100 * Rules.Centi));
        }

        [Test]
        public void Cancel_BeforeStartRefundsEverything()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var bronze = settlement.Bronze;
            settlement.Queue.Add(new BuildOrder { Class = ShipClass.Cutter, PaidBronze = 100 * Rules.Centi, TotalTicks = 100 });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 0 });
            settlement.Queue[0].Started = false;
            world.Pending[0].QueueIndex = 0;
            ApplyPendingOnly(world);
            Assert.That(settlement.Bronze, Is.EqualTo(bronze + 100 * Rules.Centi));
        }

        [Test]
        public void Cancel_AfterStartMatchesTheDocumentedExample()
        {
            var world = Empty();
            var settlement = world.SettlementOf(0);
            var bronze = settlement.Bronze;
            settlement.Queue.Add(new BuildOrder { Class = ShipClass.Cutter, PaidBronze = 100 * Rules.Centi, TotalTicks = 100, DoneTicks = 40, Started = true });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.CancelQueue, QueueIndex = 0 });
            ApplyPendingOnly(world);
            Assert.That(settlement.Bronze, Is.EqualTo(bronze + 48 * Rules.Centi), "E07: 40% used, 60 reserved, 48 back, 12 lost.");
        }

        static void ApplyPendingOnly(World world)
        {
            var settlement = world.SettlementOf(0);
            var order = settlement.Queue[0];
            order.TotalTicks = Math.Max(order.TotalTicks, 100);
            // Step advances production after commands; commands run first, so the cancel sees this state.
            world.Step();
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
        public void Board_DoesNotChangeOwnerImmediately_AndCapturesWithCargo()
        {
            var world = Empty();
            var (boarder, target) = BoardSetup(world, 5);
            target.Cargo = 77 * Rules.Centi;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            Run(world, 2);
            Assert.That(target.Owner, Is.EqualTo(1), "Hooks take 3 seconds.");
            Run(world, 30);
            Assert.That(target.Owner, Is.EqualTo(0));
            Assert.That(target.Cargo, Is.EqualTo(77 * Rules.Centi));
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
            var warship = world.SpawnShip(1, ShipClass.CrewDestroyer, 500, 400);
            var hidden = world.SpawnShip(0, ShipClass.Cutter, -500, -400);
            for (var i = 0; i < 90; i++) { ai.Think(world); world.Step(); }
            Assert.That(world.CanSee(1, hidden), Is.False);
            Assert.That(warship.TargetId, Is.Not.EqualTo(hidden.Id));
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
            target.Sailors = 8;
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
            world.SettlementOf(0).Bronze = 0;
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

        // ---------- Save, determinism, whole match ----------

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
        public void Match_SameSeedGivesIdenticalState()
        {
            var (a, a0, a1) = Match(42);
            var (b, b0, b1) = Match(42);
            Advance(a, a0, a1, 12000);
            Advance(b, b0, b1, 12000);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(a)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(b))));
        }

        [Test]
        public void Save_RoundTripContinuesIdentically()
        {
            var (world, first, second) = Match(5);
            Advance(world, first, second, 9000);
            var data = SaveGame.Serialize(world, second.BuildIndex);
            var firstBuild = first.BuildIndex;

            var loaded = SaveGame.Deserialize(data, out var build);
            Assert.That(SaveGame.Serialize(loaded, build), Is.EqualTo(data));

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
                Assert.That(settlement.Bronze, Is.EqualTo(1400 * Rules.Centi));
                Assert.That(settlement.Rations, Is.EqualTo(600 * Rules.Centi));
                Assert.That(world.Ships.Count(ship => ship.Owner == settlement.Owner && ship.Class == ShipClass.Cutter), Is.EqualTo(2));
            }
            Assert.That(world.Nodes.Where(node => node.Type == ResourceType.Bronze).Sum(node => node.Capacity), Is.EqualTo(34000 * Rules.Centi), "E03: 34000 B on the reference map.");
        }

        [Test]
        public void Opponent_KeepsGatheringBothResources()
        {
            var (world, first, second) = Match(3);
            Advance(world, first, second, 10 * 60 * Rules.TickRate);
            var settlement = world.SettlementOf(1);
            var rationsStock = world.Nodes.Where(node => node.Type == ResourceType.Rations && (node.KnownMask & 2) != 0).Sum(node => node.Capacity - node.Stock);
            Assert.That(rationsStock + settlement.Rations, Is.GreaterThan(0));
            Assert.That(world.Ships.Any(ship => ship.Owner == 1 && ship.Order == OrderKind.Harvest && world.FindNode(ship.NodeId).Type == ResourceType.Rations)
                        || settlement.Rations * 2 >= settlement.Bronze, Is.True);
        }

        [Test]
        public void Match_OpponentsBuildFleetsFightAndFinishOrKeepPlaying()
        {
            var (world, first, second) = Match(9);
            var built = 0;
            var combat = 0;
            for (var i = 0; i < 30 * 60 * Rules.TickRate && world.Outcome == OutcomeKind.InProgress; i++)
            {
                first.Think(world);
                second.Think(world);
                world.Step();
                built += world.Events.Count(item => item.Type == "ship_built");
                combat += world.Events.Count(item => item.Type == "volley" || item.Type == "ram_hit");
                world.Events.Clear();
                Assert.That(world.Ships.All(ship => !double.IsNaN(ship.X) && !double.IsNaN(ship.Y)), Is.True);
            }
            TestContext.WriteLine($"Tick {world.Tick}, outcome {world.Outcome}, winner {world.Winner}, built {built}, combat events {combat}, ships {world.Ships.Count}");
            Assert.That(built, Is.GreaterThan(4));
            Assert.That(combat, Is.GreaterThan(0));
        }
    }
}
