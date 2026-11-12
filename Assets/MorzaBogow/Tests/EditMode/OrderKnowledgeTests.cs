using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// J08/Q06: an order to work a wreck or a deposit is taken only for one the side may know of. A wreck it does not see
    /// now, and a deposit it has never seen, are refused exactly as one that is not there: the same answer and the same
    /// world, so neither the refusal nor a ship setting out tells of what lies under the fog. Each rule is checked on twin
    /// worlds that differ only by the hidden thing.
    /// </summary>
    public sealed class OrderKnowledgeTests
    {
        const double HiddenX = 350, HiddenY = 200;

        static World Empty()
        {
            var world = new World(7);
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 500, Y = 0, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            return world;
        }

        static Wreck AddWreck(World world, int id, double x, double y)
        {
            var wreck = new Wreck { Id = id, X = x, Y = y, Wood = 40 * Rules.Centi, ExpiresTick = 1000000 };
            wreck.Items[(int)WeaponType.Scorpion] = 1;
            world.Wrecks.Add(wreck);
            return wreck;
        }

        static ResourceNode AddNode(World world, int id, double x, double y, int knownMask)
        {
            var node = new ResourceNode { Id = id, Type = ResourceType.Wood, X = x, Y = y, Stock = 500 * Rules.Centi, KnownMask = knownMask };
            world.Nodes.Add(node);
            return node;
        }

        static (CommandOutcome, CommandReason) ResultOf(World world, int commandId, int shipId)
        {
            var result = world.CommandResults.Single(item => item.CommandId == commandId && item.ShipId == shipId);
            return (result.Outcome, result.Reason);
        }

        /// <summary>
        /// Twin worlds: the same two cutters of side 0, one under way and one at rest, and the same order naming the same
        /// number; in the first that number is a hidden thing (added by <paramref name="hide"/>), in the second nothing
        /// has it. Every step the results of the orders, the events and the whole saved state must be the same, the
        /// hidden thing itself apart (<paramref name="unhide"/> takes it out of a copy of the first before the states are compared).
        /// </summary>
        static void AssertRefusedLikeNothingThere(CommandKind kind, System.Action<World, int> hide, System.Action<World> unhide)
        {
            var worlds = new[] { Empty(), Empty() };
            var orders = new int[2];
            var sailing = new Ship[2];
            var resting = new Ship[2];
            var hiddenId = -1;
            for (var i = 0; i < 2; i++)
            {
                var world = worlds[i];
                sailing[i] = world.SpawnShip(0, ShipClass.Cutter, -440, 30);
                resting[i] = world.SpawnShip(0, ShipClass.Cutter, -440, -30);
                // The hidden thing takes the next number in the first world; the second world only uses the number up.
                hiddenId = world.NextId++;
                if (i == 0) hide(world, hiddenId);
                world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { sailing[i].Id }, X = -300, Y = 120 });
                world.Step();
                Assert.That(world.CanSee(0, HiddenX, HiddenY), Is.False, "The place lies outside what side 0 sees.");
                orders[i] = world.Issue(new Command { Faction = 0, Kind = kind, ShipIds = new[] { sailing[i].Id, resting[i].Id }, TargetId = hiddenId });
            }

            for (var tick = 0; tick < 20 * Rules.TickRate; tick++)
            {
                for (var i = 0; i < 2; i++) worlds[i].Step();
                if (tick == 0)
                {
                    for (var i = 0; i < 2; i++)
                    {
                        Assert.That(ResultOf(worlds[i], orders[i], sailing[i].Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoObservedTarget)), $"World {i}: the ship under way.");
                        Assert.That(ResultOf(worlds[i], orders[i], resting[i].Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoObservedTarget)), $"World {i}: the ship at rest.");
                        Assert.That(sailing[i].Order, Is.EqualTo(OrderKind.Move), $"World {i}: a refused order leaves the ship's own order.");
                        Assert.That(resting[i].Order, Is.EqualTo(OrderKind.Idle), $"World {i}: and a ship at rest stays at rest.");
                    }
                }
                Assert.That(worlds[0].CommandResults, Is.EqualTo(worlds[1].CommandResults), $"Tick {tick}: the same answers.");
                Assert.That(worlds[0].Events.Select(item => (item.Type, item.SubjectId, item.OtherId, item.X, item.Y, item.Amount)),
                    Is.EqualTo(worlds[1].Events.Select(item => (item.Type, item.SubjectId, item.OtherId, item.X, item.Y, item.Amount))), $"Tick {tick}: the same events.");
                var copy = SaveGame.Deserialize(SaveGame.Serialize(worlds[0]), out _);
                unhide(copy);
                Assert.That(SaveGame.Hash(SaveGame.Serialize(copy)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(worlds[1]))), $"Tick {tick}: the same world, the hidden thing apart.");
                for (var i = 0; i < 2; i++) worlds[i].Events.Clear();
            }
        }

        [Test]
        public void Salvage_AWreckTheSideDoesNotSeeIsRefusedLikeOneThatIsNotThere()
        {
            AssertRefusedLikeNothingThere(CommandKind.Salvage,
                (world, id) => AddWreck(world, id, HiddenX, HiddenY),
                world => Assert.That(world.Wrecks.RemoveAll(wreck => wreck.X == HiddenX && wreck.Y == HiddenY), Is.EqualTo(1), "The wreck lay there untouched."));
        }

        [Test]
        public void Harvest_ADepositTheSideHasNeverSeenIsRefusedLikeOneThatIsNotThere()
        {
            // The other side knows the deposit: knowledge is each side's own.
            AssertRefusedLikeNothingThere(CommandKind.Harvest,
                (world, id) => AddNode(world, id, HiddenX, HiddenY, 1 << 1),
                world => Assert.That(world.Nodes.RemoveAll(node => node.X == HiddenX && node.Y == HiddenY && node.KnownMask == 1 << 1 && node.Stock == 500 * Rules.Centi), Is.EqualTo(1), "The deposit lay there unworked and unknown to side 0."));
        }

        [Test]
        public void Salvage_AWreckInSightIsAcceptedAndOneOnlyTheOtherSideSeesIsItsOwnToTake()
        {
            var world = Empty();
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -440, 0);
            var theirs = world.SpawnShip(1, ShipClass.Cutter, 440, 0);
            var near = AddWreck(world, world.NextId++, -300, 60);
            var far = AddWreck(world, world.NextId++, HiddenX, HiddenY);
            Assert.That(world.CanSee(0, near.X, near.Y), Is.True, "Side 0 sees the near wreck: its port and its cutter are close.");
            Assert.That(world.CanSee(0, far.X, far.Y), Is.False);
            Assert.That(world.CanSee(1, far.X, far.Y), Is.True, "Side 1 sees the far one.");

            var mine = world.Issue(new Command { Faction = 0, Kind = CommandKind.Salvage, ShipIds = new[] { cutter.Id }, TargetId = near.Id });
            var other = world.Issue(new Command { Faction = 1, Kind = CommandKind.Salvage, ShipIds = new[] { theirs.Id }, TargetId = far.Id });
            world.Step();
            Assert.That(ResultOf(world, mine, cutter.Id), Is.EqualTo((CommandOutcome.Accepted, CommandReason.None)));
            Assert.That((cutter.Order, cutter.WreckId), Is.EqualTo((OrderKind.Salvage, near.Id)));
            Assert.That(ResultOf(world, other, theirs.Id), Is.EqualTo((CommandOutcome.Accepted, CommandReason.None)), "The same wreck that side 0 is refused.");
            Assert.That((theirs.Order, theirs.WreckId), Is.EqualTo((OrderKind.Salvage, far.Id)));

            var hidden = world.Issue(new Command { Faction = 0, Kind = CommandKind.Salvage, ShipIds = new[] { cutter.Id }, TargetId = far.Id });
            world.Step();
            Assert.That(ResultOf(world, hidden, cutter.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoObservedTarget)));
            Assert.That((cutter.Order, cutter.WreckId), Is.EqualTo((OrderKind.Salvage, near.Id)), "The refusal leaves the order it had.");
        }

        [Test]
        public void Harvest_AKnownDepositIsAcceptedEvenOutOfSightAndCapabilityIsAnsweredFirst()
        {
            var world = Empty();
            var cutter = world.SpawnShip(0, ShipClass.Cutter, -440, 0);
            var warship = world.SpawnShip(0, ShipClass.Ram, -440, 60);
            var known = AddNode(world, world.NextId++, HiddenX, HiddenY, 1 << 0);
            var unknown = AddNode(world, world.NextId++, HiddenX, -HiddenY, 1 << 1);
            Assert.That(world.CanSee(0, known.X, known.Y), Is.False, "Seen once, not now: a deposit stays where it was found.");

            var order = world.Issue(new Command { Faction = 0, Kind = CommandKind.Harvest, ShipIds = new[] { cutter.Id }, TargetId = known.Id });
            var incapable = world.Issue(new Command { Faction = 0, Kind = CommandKind.Harvest, ShipIds = new[] { warship.Id }, TargetId = unknown.Id });
            world.Step();
            Assert.That(ResultOf(world, order, cutter.Id), Is.EqualTo((CommandOutcome.Accepted, CommandReason.None)));
            Assert.That((cutter.Order, cutter.NodeId), Is.EqualTo((OrderKind.Harvest, known.Id)));
            Assert.That(ResultOf(world, incapable, warship.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NotCapable)), "What the ship cannot do is answered before anything about the target.");

            var hidden = world.Issue(new Command { Faction = 0, Kind = CommandKind.Harvest, ShipIds = new[] { cutter.Id }, TargetId = unknown.Id });
            world.Step();
            Assert.That(ResultOf(world, hidden, cutter.Id), Is.EqualTo((CommandOutcome.Rejected, CommandReason.NoObservedTarget)));
            Assert.That((cutter.Order, cutter.NodeId), Is.EqualTo((OrderKind.Harvest, known.Id)), "The refusal leaves the order it had.");
        }
    }
}
