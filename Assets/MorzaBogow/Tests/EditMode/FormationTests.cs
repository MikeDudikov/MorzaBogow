using System;
using System.Linq;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>A group sent to one point takes its own places there, in lines abreast or a wedge, never crowding or crossing.</summary>
    public sealed class FormationTests
    {
        /// <summary>A column of rams 40 m apart, north to south, centred on y = 0.</summary>
        static Ship[] Column(World world, int count, double x)
        {
            var ships = new Ship[count];
            for (var i = 0; i < count; i++) ships[i] = world.SpawnShip(0, ShipClass.Ram, x, (i - (count - 1) / 2.0) * 40);
            return ships;
        }

        /// <summary>True when the two ways cross each other (touching ends do not count).</summary>
        static bool Cross(Ship first, (double X, double Y) firstTarget, Ship second, (double X, double Y) secondTarget)
        {
            double Turn(double px, double py, double qx, double qy, double rx, double ry) => (qx - px) * (ry - py) - (qy - py) * (rx - px);
            var d1 = Turn(second.X, second.Y, secondTarget.X, secondTarget.Y, first.X, first.Y);
            var d2 = Turn(second.X, second.Y, secondTarget.X, secondTarget.Y, firstTarget.X, firstTarget.Y);
            var d3 = Turn(first.X, first.Y, firstTarget.X, firstTarget.Y, second.X, second.Y);
            var d4 = Turn(first.X, first.Y, firstTarget.X, firstTarget.Y, secondTarget.X, secondTarget.Y);
            return d1 * d2 < -1e-9 && d3 * d4 < -1e-9;
        }

        [Test]
        public void Formation_ALineStandsAbreastAcrossTheCourseWithRoomForTheOars()
        {
            var world = new World(1);
            var ships = Column(world, 4, 0);
            var targets = Formation.Targets(world, ships, 300, 0, FormationKind.Line);
            var spacing = Formation.Spacing(ships);
            Assert.That(spacing, Is.EqualTo(ships[0].Definition.Beam + 2 * Rules.OarReach(ships[0].Definition) + World.HullSpacing).Within(1e-9),
                "A hull, both banks of oars and the fender gap.");
            foreach (var (x, _) in targets) Assert.That(x, Is.EqualTo(300).Within(1e-9), "Abreast on the point, across the course.");
            var across = targets.Select(target => target.Y).OrderBy(y => y).ToArray();
            for (var i = 1; i < across.Length; i++) Assert.That(across[i] - across[i - 1], Is.EqualTo(spacing).Within(1e-9));
            Assert.That(across.Average(), Is.EqualTo(0).Within(1e-9), "Centred on the point.");
            for (var i = 1; i < ships.Length; i++) Assert.That(targets[i].Y, Is.GreaterThan(targets[i - 1].Y), "No ship crosses another's course.");
        }

        [Test]
        public void Formation_AWedgeLeadsFromThePointWithTwoArmsBehind()
        {
            var world = new World(1);
            var ships = new[]
            {
                world.SpawnShip(0, ShipClass.Ram, -200, -60), world.SpawnShip(0, ShipClass.Cutter, -200, -20),
                world.SpawnShip(0, ShipClass.Merchant, -200, 20), world.SpawnShip(0, ShipClass.Boarder, -200, 60),
                world.SpawnShip(0, ShipClass.Ram, -200, 100),
            };
            var targets = Formation.Targets(world, ships, 0, 0, FormationKind.Wedge);
            var spacing = Formation.Spacing(ships);
            Assert.That(spacing, Is.EqualTo(ships.Max(ship => ship.Definition.Beam + 2 * Rules.OarReach(ship.Definition)) + World.HullSpacing).Within(1e-9),
                "The widest hull with its oars sets the room.");

            // The course runs from the group's centre (-200, 20) to the point.
            var length = Math.Sqrt(200.0 * 200.0 + 20.0 * 20.0);
            var forwardX = 200.0 / length;
            var forwardY = -20.0 / length;
            var ahead = targets.Select(target => target.X * forwardX + target.Y * forwardY).ToArray();
            var side = targets.Select(target => -target.X * forwardY + target.Y * forwardX).ToArray();
            Assert.That(ahead.Count(value => Math.Abs(value) < 1e-9), Is.EqualTo(1), "One leader on the point.");
            Assert.That(ahead.All(value => value <= 1e-9), Is.True, "The others trail behind it.");
            Assert.That(side.Count(value => value > 1e-6), Is.EqualTo(2), "One arm to the left.");
            Assert.That(side.Count(value => value < -1e-6), Is.EqualTo(2), "One arm to the right.");
            for (var i = 0; i < targets.Length; i++)
            for (var j = i + 1; j < targets.Length; j++)
            {
                var gap = World.Distance(targets[i].X, targets[i].Y, targets[j].X, targets[j].Y);
                Assert.That(gap, Is.GreaterThanOrEqualTo(spacing - 1e-9), $"Places {i} and {j} leave room for oars.");
                Assert.That(Cross(ships[i], targets[i], ships[j], targets[j]), Is.False, $"Ships {i} and {j} do not cross.");
            }
        }

        [Test]
        public void Formation_ALargeGroupFormsLinesOfEightAndNoTwoCoursesCross()
        {
            var world = new World(1);
            // Strung out along the course, as a fleet on the move is; the first three are a known trap for matching by side.
            var positions = new[] { (-10.0, 40.0), (-300.0, 45.0), (-140.0, -85.0), (-60.0, 120.0), (-220.0, -30.0), (-90.0, 10.0),
                (-400.0, 90.0), (-35.0, -140.0), (-260.0, 150.0), (-180.0, 5.0), (-120.0, -200.0), (-330.0, -110.0) };
            var ships = positions.Select(position => world.SpawnShip(0, ShipClass.Ram, position.Item1, position.Item2)).ToArray();
            var centreY = positions.Average(position => position.Item2);
            var targets = Formation.Targets(world, ships, 400, centreY, FormationKind.Line);
            var depth = Formation.Depth(ships);
            var spacing = Formation.Spacing(ships);

            var front = targets.Count(target => Math.Abs(target.X - 400) < 1e-6);
            var second = targets.Count(target => Math.Abs(target.X - (400 - depth)) < 1e-6);
            Assert.That(front, Is.EqualTo(Formation.PerRank), "At most eight abreast.");
            Assert.That(second, Is.EqualTo(ships.Length - Formation.PerRank), "The rest a hull's length behind.");
            for (var i = 0; i < ships.Length; i++)
            for (var j = i + 1; j < ships.Length; j++)
            {
                Assert.That(Cross(ships[i], targets[i], ships[j], targets[j]), Is.False, $"Ships {i} and {j} do not cross.");
                Assert.That(World.Distance(targets[i].X, targets[i].Y, targets[j].X, targets[j].Y), Is.GreaterThanOrEqualTo(Math.Min(spacing, depth) - 1e-9));
            }
        }

        [Test]
        public void Formation_AClickNextToTheGroupKeepsTheLeadersHeading()
        {
            var world = new World(1);
            var ships = new[] { world.SpawnShip(0, ShipClass.Ram, -20, 0), world.SpawnShip(0, ShipClass.Ram, 0, 0), world.SpawnShip(0, ShipClass.Ram, 20, 0) };
            foreach (var ship in ships) ship.Heading = Math.PI / 2;
            var targets = Formation.Targets(world, ships, 3, 2, FormationKind.Line);
            foreach (var (_, y) in targets) Assert.That(y, Is.EqualTo(2).Within(1e-9), "Heading north, the line runs east to west through the point.");
            var xs = targets.Select(target => target.X).OrderBy(x => x).ToArray();
            Assert.That(xs[1] - xs[0], Is.EqualTo(Formation.Spacing(ships)).Within(1e-9));
            Assert.That(targets[0].X, Is.LessThan(targets[2].X), "The western ship keeps the western place.");
        }

        [Test]
        public void Formation_TheSameGroupGetsTheSamePlacesWhateverTheSelectionOrder()
        {
            var world = new World(1);
            var ships = Column(world, 6, 0);
            var once = Formation.Targets(world, ships, 120, 250, FormationKind.Wedge);
            var shuffled = new[] { ships[3], ships[0], ships[5], ships[1], ships[4], ships[2] };
            var again = Formation.Targets(world, shuffled, 120, 250, FormationKind.Wedge);
            for (var i = 0; i < shuffled.Length; i++)
                Assert.That(again[i], Is.EqualTo(once[Array.IndexOf(ships, shuffled[i])]), "Each ship keeps its place.");
            Assert.That(Formation.Targets(world, ships.Take(1).ToArray(), 120, 250, FormationKind.Line)[0], Is.EqualTo((120.0, 250.0)),
                "A single ship goes to the point itself.");
            Assert.That(Formation.Targets(world, Array.Empty<Ship>(), 0, 0, FormationKind.Line), Is.Empty);
        }

        [Test]
        public void Formation_PlacesStayInOpenWaterWithRoomEvenWhenThePointIsOnLand()
        {
            var world = MapFactory.CreateSkirmish(20261109);
            var island = world.Islands.OrderByDescending(item => item.Radius).First(item => world.OnLand(item.X, item.Y));
            var ships = Enumerable.Range(0, 7).Select(i => world.SpawnShip(0, ShipClass.Merchant, island.X + 300, island.Y + (i - 3) * 30)).ToArray();
            var room = Formation.Room(ships);
            Assert.That(room, Is.EqualTo(ships.Max(World.LandClearance) + 1).Within(1e-9), "The deepest hull's clearance from the shore, and a metre.");

            var moved = Formation.OffShore(world, island.X, island.Y, room);
            Assert.That(Formation.Clear(world, moved.X, moved.Y, room - 1e-6), Is.True, "A point on land moves out to open water.");

            var least = Math.Min(Formation.Spacing(ships), Formation.Depth(ships));
            foreach (var kind in new[] { FormationKind.Line, FormationKind.Wedge })
            {
                var targets = Formation.Targets(world, ships, island.X, island.Y, kind);
                Assert.That(targets.All(target => Formation.Clear(world, target.X, target.Y, room - 1e-6)), Is.True, $"{kind}: every place in open water with room.");
                for (var i = 0; i < targets.Length; i++)
                for (var j = i + 1; j < targets.Length; j++)
                    Assert.That(World.Distance(targets[i].X, targets[i].Y, targets[j].X, targets[j].Y), Is.GreaterThanOrEqualTo(least - 1e-6), $"{kind}: places {i} and {j} do not crowd.");
            }
        }
    }
}
