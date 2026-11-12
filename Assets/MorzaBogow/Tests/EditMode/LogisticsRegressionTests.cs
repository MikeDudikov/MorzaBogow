using System;
using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>Recorded logistics failures and save boundaries: Q02/Q04, J01/J02/J07/J10.</summary>
    public sealed class LogisticsRegressionTests
    {
        static World Scenario(out Settlement source, out Settlement destination)
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0 });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            destination = new Settlement { Id = world.NextId++, Owner = 0, X = -150, Y = 0, IsOutpost = true, HasShipyard = true, HasWorkshop = true, Hull = Rules.OutpostHull };
            source = new Settlement { Id = world.NextId++, Owner = 0, X = -350, Y = 0, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull, Wood = 3000 * Rules.Centi, Silver = 1500 * Rules.Centi };
            world.Settlements.Add(destination);
            world.Settlements.Add(source);
            world.HeavyArmsResearched |= 1;
            for (var i = 0; i < world.Explored.Length; i++) world.Explored[i] = 3;
            return world;
        }

        static void Until(World world, Func<bool> condition, int seconds)
        {
            for (var tick = 0; tick < seconds * Rules.TickRate && !condition(); tick++) world.Step();
            Assert.That(condition(), Is.True, $"Condition not reached by tick {world.Tick} (seed 7).");
        }

        static void AssertBookings(World world)
        {
            foreach (var port in world.Settlements)
            {
                var ships = world.Ships.Where(ship => ship.SourceId == port.Id);
                Assert.That(port.ReservedWood, Is.EqualTo(ships.Sum(ship => ship.WantWood)), $"Wood at {port.Id}, tick {world.Tick}.");
                Assert.That(port.ReservedSilver, Is.EqualTo(ships.Sum(ship => ship.WantSilver)), $"Silver at {port.Id}, tick {world.Tick}.");
            }
        }

        /// <summary>
        /// Watches a convoy tick by tick for the rule of the convoys as they are (World.Convoy.cs) that nobody of a group
        /// sails while another of it is still to load. The plan does not state that rule itself; it asks that the working
        /// convoys be kept (docs/NOWY_PLAN_GRY.md §4 and §12). Two checks, each of its own kind of fault:
        /// the world's own answer (ConvoyStillLoading) for every loaded member must be what the members' public state
        /// says, a member of its group in a phase before its departure (World.BeforeDeparture, the one public rule the
        /// convoys themselves use; Convoy_EveryTransportPhaseIsClassedAsBeforeOrAfterDeparture pins that rule phase by
        /// phase); and no member is farther than DepartedMeters from the
        /// source before the last tick anybody of the group was still to load. The second sees a member sent on ahead only
        /// if it gets that far while the other still loads, so it needs a wait longer than the way out.
        /// </summary>
        sealed class ConvoyWatch
        {
            public const double DepartedMeters = 60;
            public readonly System.Collections.Generic.Dictionary<int, int> Departed = new System.Collections.Generic.Dictionary<int, int>();
            public int LastLoading = -1, WaitingTicks, Releases;
            readonly System.Collections.Generic.Dictionary<int, bool> waiting = new System.Collections.Generic.Dictionary<int, bool>();

            public void After(World world, Ship[] ships, Settlement source)
            {
                var group = ships.Where(ship => ship.Order == OrderKind.Convoy && ship.ConvoyGroup >= 0).ToList();
                if (group.Any(ship => World.BeforeDeparture(ship.Transport))) LastLoading = world.Tick;
                foreach (var ship in group)
                {
                    if (ship.Transport != TransportPhase.ToDestination) { waiting.Remove(ship.Id); continue; }
                    var expected = group.Any(other => other != ship && other.ConvoyGroup == ship.ConvoyGroup && World.BeforeDeparture(other.Transport));
                    Assert.That(world.ConvoyStillLoading(ship), Is.EqualTo(expected), $"Ship {ship.Id}, tick {world.Tick}: whether another of its group is still to load.");
                    if (expected) WaitingTicks++;
                    if (waiting.TryGetValue(ship.Id, out var before) && before && !expected) Releases++;
                    waiting[ship.Id] = expected;
                    if (!Departed.ContainsKey(ship.Id) && World.Distance(ship.X, ship.Y, source.X, source.Y) > DepartedMeters) Departed[ship.Id] = world.Tick;
                }
            }

            /// <summary>The checks above were really made: somebody did wait for its group, and was let go when the group had loaded.</summary>
            public void AssertWaitedAndSailedTogether()
            {
                Assert.That(WaitingTicks, Is.GreaterThan(0), "A loaded member waited for another of its group: otherwise nothing was checked.");
                Assert.That(Releases, Is.GreaterThan(0), "And was let go when nobody of its group was left to load.");
                Assert.That(Departed, Is.Not.Empty, "Somebody left the source's waters: otherwise the check of the departures below is empty.");
                Assert.That(Departed.Values.All(tick => tick > LastLoading), Is.True,
                    $"Nobody was more than {DepartedMeters} m from the source while another of the group was still to load (left at {string.Join(", ", Departed.Values)}, last loading at {LastLoading}).");
            }
        }

        /// <summary>
        /// The rule of "not yet sailed" phase by phase, written out here on its own: a carrier bound for its source,
        /// waiting for a berth there or loading has still to depart; every other phase has not. A phase added to the
        /// game must be classed here too: the test fails for a value this table does not name, so a new phase cannot
        /// fall silently on either side.
        /// </summary>
        [Test]
        public void Convoy_EveryTransportPhaseIsClassedAsBeforeOrAfterDeparture()
        {
            var table = new System.Collections.Generic.Dictionary<TransportPhase, bool>
            {
                { TransportPhase.ToSource, true },
                { TransportPhase.WaitSourceBerth, true },
                { TransportPhase.Loading, true },
                { TransportPhase.ToDestination, false },
                { TransportPhase.WaitDestinationBerth, false },
                { TransportPhase.Unloading, false },
                { TransportPhase.Building, false },
                { TransportPhase.Standby, false },
            };
            var phases = (TransportPhase[])Enum.GetValues(typeof(TransportPhase));
            Assert.That(phases, Has.Length.EqualTo(8), "The saved numbers 0-7; a ninth phase must be classed below before this count is raised.");
            foreach (var phase in phases)
            {
                Assert.That(table.ContainsKey(phase), Is.True, $"{phase} is not classed: say whether a carrier in it has still to leave its source.");
                Assert.That(World.BeforeDeparture(phase), Is.EqualTo(table[phase]), $"{phase}.");
            }
            Assert.That(table.Keys, Is.EquivalentTo(phases), "The table names nothing that is no phase.");
            // A number that is no phase (a damaged save) is no departure to wait for, and no exception in a step.
            Assert.That(World.BeforeDeparture((TransportPhase)(-1)), Is.False);
            Assert.That(World.BeforeDeparture((TransportPhase)phases.Length), Is.False);
        }

        [TestCase(-330, 1)]
        [TestCase(-330, -1)]
        [TestCase(-470, 1)]
        public void Convoy_AGroupStartingBesideItsSourceClearsTheApproachAndDelivers(int startX, int side)
        {
            // Exact recorded q02 layout, plus its mirror and the already-working far approach.
            // The 300 s bound predates the fix; widening it must not hide the blocked loading route.
            // Scope: open water, no land by the source or the destination. A port with its islet, as every port of the
            // generated map has, is not covered here: in the reproduction fxk the group jammed in 2 of 8 layouts there:
            // delivery only after 312 s and 594 s, a member making no way (under 5 m in 10 s) for 180 s and 430 s, its
            // longest full stop 6.5 s and 280 s (out/k1-fxd.txt); work for P03.
            var world = Scenario(out var source, out var destination);
            var ships = new[] { 40, 80, 120 }.Select(y => world.SpawnShip(0, ShipClass.Merchant, startX, side * y)).ToArray();
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = destination.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Convoy, ShipIds = ships.Select(ship => ship.Id).ToArray() });
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);
            var watch = new ConvoyWatch();
            for (var tick = 0; tick < 300 * Rules.TickRate; tick++)
            {
                world.Step();
                if (tick % Rules.TickRate == 0) AssertBookings(world);
                watch.After(world, ships, source);
                if (destination.Wood == 400 * Rules.Centi && destination.Silver == 260 * Rules.Centi) break;
            }
            // The delivery first: a jam must be reported as a jam, not as a convoy that did not sail together.
            Assert.That(destination.Wood, Is.EqualTo(400 * Rules.Centi), "The loaded member must not indefinitely block the one still approaching a free berth.");
            Assert.That(destination.Silver, Is.EqualTo(260 * Rules.Centi));
            Assert.That(source.Wood, Is.EqualTo(2600 * Rules.Centi));
            Assert.That(source.Silver, Is.EqualTo(1240 * Rules.Centi));
            Assert.That(ships.All(ship => ship.Order == OrderKind.Convoy), Is.True);
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
            AssertBookings(world);
            Assert.That(watch.Departed.Count, Is.EqualTo(2), "Two holds carry the 400 D and 260 S; the third member has nothing left to carry.");
            Assert.That(ships.Single(ship => !watch.Departed.ContainsKey(ship.Id)).Transport, Is.EqualTo(TransportPhase.Standby), "The third waits by the source.");
            watch.AssertWaitedAndSailedTogether();
        }

        [Test]
        public void Convoy_ALoadedMemberWaitsByTheSourceForOneStillFarAway()
        {
            // The member that books the full hold starts 350 m from the source; the other starts beside it, loads its 180
            // in a few seconds and then has most of a minute to wait. That wait is many times longer than the way out of
            // the source's waters, so a member sent on ahead is caught, whichever side of the source it lay on.
            // Open water only, as above.
            var world = Scenario(out var source, out var destination);
            // Which member books the full hold follows from the order in which the convoy's members choose; the check of
            // the bookings below is what this test stands on, whatever that order is.
            var near = world.SpawnShip(0, ShipClass.Merchant, source.X + 20, 80);
            var far = world.SpawnShip(0, ShipClass.Merchant, source.X - 350, 40);
            var ships = new[] { near, far };
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = destination.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Convoy, ShipIds = ships.Select(ship => ship.Id).ToArray() });
            Until(world, () => ships.All(ship => ship.WantWood + ship.WantSilver + ship.HoldWood + ship.HoldSilver > 0), 5);
            Assert.That((far.WantWood + far.WantSilver + far.HoldWood + far.HoldSilver, near.WantWood + near.WantSilver + near.HoldWood + near.HoldSilver),
                Is.EqualTo((480 * Rules.Centi, 180 * Rules.Centi)), "The far member carries the full hold of 480, the near one the remaining 180.");
            Assert.That((far.ConvoyGroup, far.SourceId), Is.EqualTo((near.ConvoyGroup, source.Id)), "One group, one source.");

            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);
            var watch = new ConvoyWatch();
            var nearWaiting = 0;
            for (var tick = 0; tick < 300 * Rules.TickRate; tick++)
            {
                world.Step();
                if (tick % Rules.TickRate == 0) AssertBookings(world);
                watch.After(world, ships, source);
                if (near.Transport == TransportPhase.ToDestination && World.BeforeDeparture(far.Transport))
                {
                    nearWaiting++;
                    Assert.That(World.Distance(near.X, near.Y, source.X, source.Y), Is.LessThanOrEqualTo(ConvoyWatch.DepartedMeters), $"Tick {world.Tick}: the loaded member stays by the source while the other is still to load.");
                }
                if (destination.Wood == 400 * Rules.Centi && destination.Silver == 260 * Rules.Centi) break;
            }
            Assert.That(destination.Wood, Is.EqualTo(400 * Rules.Centi), "Both holds are delivered.");
            Assert.That(destination.Silver, Is.EqualTo(260 * Rules.Centi));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
            AssertBookings(world);
            Assert.That(nearWaiting, Is.GreaterThan(30 * Rules.TickRate), "The near member waited more than half a minute, several times the way out of the source's waters.");
            Assert.That(watch.Departed.Count, Is.EqualTo(2));
            watch.AssertWaitedAndSailedTogether();
        }

        [Test]
        public void Convoy_ANewMemberCannotBookTheCapturedSourceOfAGroupNotYetUpdated()
        {
            var world = Scenario(out var source, out var destination);
            var newcomer = world.SpawnShip(0, ShipClass.Merchant, -470, 40);
            var member = world.SpawnShip(0, ShipClass.Merchant, -330, 80);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = destination.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Convoy, ShipIds = new[] { member.Id } });
            Until(world, () => member.Order == OrderKind.Convoy && member.Transport == TransportPhase.ToSource && member.WantWood + member.WantSilver > 0, 2);
            Assert.That((world.Tick + 1 + newcomer.Id) % Rules.TickRate, Is.Zero, "The lower-ID newcomer chooses before the existing member updates.");
            Assert.That(member.WantWood + member.WantSilver, Is.GreaterThan(0));
            source.Owner = 1;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Convoy, ShipIds = new[] { newcomer.Id } });
            world.Step();
            Assert.That(newcomer.WantWood + newcomer.WantSilver, Is.Zero, "No new reservation or reading of surplus in the captured port.");
            Assert.That(newcomer.SourceId, Is.EqualTo(-1));
            Assert.That(source.ReservedWood + source.ReservedSilver, Is.Zero);
            AssertBookings(world);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Transfer_LosingTheSourceWhileLoadingReleasesItsBookingsAndKeepsTheHold(bool reload)
        {
            var world = Scenario(out var source, out var destination);
            var ship = world.SpawnShip(0, ShipClass.Merchant, source.X, 20);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, SourceId = source.Id, TargetId = destination.Id, ShipIds = new[] { ship.Id }, Wood = 100 * Rules.Centi, Silver = 50 * Rules.Centi });
            Until(world, () => ship.Transport == TransportPhase.Loading && ship.HoldWood > 0 && ship.WantWood > 0, 30);
            if (reload)
            {
                world = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
                source = world.FindSettlement(source.Id);
                ship = world.FindShip(ship.Id);
            }
            var stock = (source.Wood, source.Silver);
            var hold = (ship.HoldWood, ship.HoldSilver);
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);
            source.Owner = 1; // Controlled loss of permission after a legally accepted transfer.
            world.Step();
            Assert.That((source.Wood, source.Silver), Is.EqualTo(stock), "No further taking from a port now owned by the other side.");
            Assert.That((ship.HoldWood, ship.HoldSilver), Is.EqualTo(hold));
            Assert.That(source.ReservedWood + source.ReservedSilver, Is.Zero);
            Assert.That(source.DockedShips.Contains(ship.Id), Is.False);
            Assert.That(source.PortWaiting.Contains(ship.Id), Is.False);
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
        }

        [TestCase(TransportPhase.Loading, false)]
        [TestCase(TransportPhase.Loading, true)]
        [TestCase(TransportPhase.ToDestination, false)]
        [TestCase(TransportPhase.ToDestination, true)]
        [TestCase(TransportPhase.Unloading, false)]
        [TestCase(TransportPhase.Unloading, true)]
        public void Transfer_LosingTheDestinationCannotDonateTheRemainingHold(TransportPhase phase, bool reload)
        {
            var world = Scenario(out var source, out var destination);
            var ship = world.SpawnShip(0, ShipClass.Merchant, source.X, 20);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, SourceId = source.Id, TargetId = destination.Id, ShipIds = new[] { ship.Id }, Wood = 100 * Rules.Centi, Silver = 50 * Rules.Centi });
            Until(world, () => ship.Transport == phase && ship.HoldWood + ship.HoldSilver > 0, 180);
            if (reload)
            {
                world = SaveGame.Deserialize(SaveGame.Serialize(world), out _);
                destination = world.FindSettlement(destination.Id);
                source = world.FindSettlement(source.Id);
                ship = world.FindShip(ship.Id);
            }
            var stock = (destination.Wood, destination.Silver);
            var hold = (ship.HoldWood, ship.HoldSilver);
            var wood = world.LedgerTotal(ResourceType.Wood);
            var silver = world.LedgerTotal(ResourceType.Silver);
            // The berth the ship holds when the permission is lost (J07): the source's while it loads, the destination's
            // while it unloads, none under way. Without this the checks below would hold of a ship that never had a berth.
            var berth = phase == TransportPhase.Loading ? source : phase == TransportPhase.Unloading ? destination : null;
            if (berth != null) Assert.That(berth.DockedShips.Contains(ship.Id), Is.True, $"{phase}: the ship lies at a berth of port {berth.Id} before the loss.");
            else Assert.That(source.DockedShips.Contains(ship.Id) || destination.DockedShips.Contains(ship.Id), Is.False, $"{phase}: under way, at no berth.");
            destination.Owner = 1;
            world.Step();
            Assert.That((destination.Wood, destination.Silver), Is.EqualTo(stock));
            Assert.That((ship.HoldWood, ship.HoldSilver), Is.EqualTo(hold), "Cargo stays aboard when permission to deliver is lost.");
            foreach (var port in new[] { source, destination })
            {
                Assert.That(port.DockedShips.Contains(ship.Id), Is.False, $"{phase}: the berth at port {port.Id} is free again.");
                Assert.That(port.PortWaiting.Contains(ship.Id), Is.False, $"{phase}: nor does the ship wait for one at port {port.Id}.");
            }
            Assert.That(ship.Order, Is.EqualTo(OrderKind.Idle));
            Assert.That(world.LedgerTotal(ResourceType.Wood), Is.EqualTo(wood));
            Assert.That(world.LedgerTotal(ResourceType.Silver), Is.EqualTo(silver));
            AssertBookings(world);
        }

        [Test]
        public void Save_WhileALoadedMemberMakesRoomForItsGroupContinuesIdentically()
        {
            // The manoeuvre that clears the approach keeps no state of its own: it is worked out each tick from where the
            // group lies. A match saved while a loaded member is under way to make room, the rest still to load, goes on
            // the same after loading, to the same delivery.
            var world = Scenario(out var source, out var destination);
            var ships = new[] { 40, 80, 120 }.Select(y => world.SpawnShip(0, ShipClass.Merchant, -330, y)).ToArray();
            world.Issue(new Command { Faction = 0, Kind = CommandKind.SetPriorityPort, TargetId = destination.Id });
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Convoy, ShipIds = ships.Select(ship => ship.Id).ToArray() });
            bool MakingRoom(World game) => game.Ships.Any(ship => ship.Order == OrderKind.Convoy && ship.Transport == TransportPhase.ToDestination
                && game.ConvoyStillLoading(ship) && ship.Speed > 0.5 && ship.HoldWood + ship.HoldSilver > 0);
            Until(world, () => MakingRoom(world), 120);
            Assert.That(destination.Wood + destination.Silver, Is.Zero, "Saved before anything is delivered.");

            var data = SaveGame.Serialize(world);
            var loaded = SaveGame.Deserialize(data, out _);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));
            Assert.That(MakingRoom(loaded), Is.True, "The loaded match is in the same manoeuvre.");
            for (var tick = 0; tick < 300 * Rules.TickRate; tick++)
            {
                world.Step();
                loaded.Step();
                if (tick % Rules.TickRate != 0) continue;
                AssertBookings(loaded);
                Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))), $"Tick {world.Tick}, seed 7.");
            }
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
            foreach (var game in new[] { world, loaded })
            {
                var port = game.FindSettlement(destination.Id);
                Assert.That((port.Wood, port.Silver), Is.EqualTo((400 * Rules.Centi, 260 * Rules.Centi)), "Both deliver the whole goal.");
                Assert.That(game.FindSettlement(source.Id).ReservedWood + game.FindSettlement(source.Id).ReservedSilver, Is.Zero);
            }
        }

        [Test]
        public void Save_InAPauseKeepsThePendingTransferAndItsNowInvalidDestinationWithoutExecutingIt()
        {
            var world = Scenario(out var source, out var destination);
            var ship = world.SpawnShip(0, ShipClass.Merchant, source.X, 20);
            var commandId = world.Issue(new Command { Faction = 0, Kind = CommandKind.Transfer, SourceId = source.Id, TargetId = destination.Id, ShipIds = new[] { ship.Id }, Wood = 100 * Rules.Centi });
            destination.Owner = 1;
            var tick = world.Tick;
            var data = SaveGame.Serialize(world);
            var loaded = SaveGame.Deserialize(data, out _);
            Assert.That(world.Tick, Is.EqualTo(tick));
            Assert.That(loaded.Tick, Is.EqualTo(tick));
            Assert.That(loaded.Pending.Single().Id, Is.EqualTo(commandId));
            Assert.That(loaded.FindSettlement(source.Id).ReservedWood, Is.Zero);
            Assert.That(loaded.FindShip(ship.Id).HoldWood, Is.Zero);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));
            foreach (var game in new[] { world, loaded })
            {
                game.Step();
                var result = game.CommandResults.Single(item => item.CommandId == commandId);
                Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.Rejected));
                Assert.That(result.Reason, Is.EqualTo(CommandReason.InvalidTarget));
                Assert.That(game.FindSettlement(source.Id).ReservedWood, Is.Zero);
                Assert.That(game.FindShip(ship.Id).Order, Is.EqualTo(OrderKind.Idle));
            }
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
        }

        [Test]
        public void Save_WhileALoadedMerchantTurnsAroundAnIslandKeepsItsRouteAndClearance()
        {
            var world = Scenario(out _, out _);
            var island = new Island { X = 0, Y = 0, Radius = 60 };
            world.Islands.Add(island);
            var ship = world.SpawnShip(0, ShipClass.Merchant, 0, 100);
            ship.HoldWood = ship.Definition.Capacity * Rules.Centi;
            var edge = island.Radius + World.LandClearance(ship) + 2;
            ship.Y = edge;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 0, Y = -edge });
            for (var tick = 0; tick < Rules.TickRate; tick++) world.Step();
            Assert.That(ship.HasWaypoint, Is.True, "Save an actual planned detour, not an idle or unobstructed route.");
            var data = SaveGame.Serialize(world);
            var loaded = SaveGame.Deserialize(data, out _);
            var other = loaded.FindShip(ship.Id);
            Assert.That(SaveGame.Serialize(loaded), Is.EqualTo(data));
            for (var tick = 0; tick < 180 * Rules.TickRate; tick++)
            {
                world.Step();
                loaded.Step();
                Assert.That(LandShape.SignedDistance(island, ship.X, ship.Y), Is.GreaterThanOrEqualTo(World.LandClearance(ship) - 1e-6), $"Tick {world.Tick}, seed 7.");
                Assert.That((other.X, other.Y, other.Heading, other.Speed), Is.EqualTo((ship.X, ship.Y, ship.Heading, ship.Speed)), $"Loaded continuation at tick {world.Tick}.");
            }
            Assert.That(World.Distance(ship.X, ship.Y, 0, -edge), Is.LessThan(ship.Definition.Radius + 8));
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(world))));
        }
    }
}
