using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>A group sent to one point gets a place for each ship in the chosen formation; the switch shows for a group only.</summary>
    public sealed class FormationOrderTests
    {
        GameObject host;
        BattleController battle;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Battle under test");
            battle = host.AddComponent<BattleController>();
            battle.Paused = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        }

        static IEnumerator WaitForInterface()
        {
            var waited = 0f;
            while (waited < 0.4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Open water far from land, ships, work points, shores and ports, so a right click there means only a move.</summary>
        static Vector3 QuietWater(World world)
        {
            for (var z = -320; z <= 320; z += 20)
            for (var x = -440; x <= 440; x += 20)
            {
                if (world.Ships.Any(ship => World.Distance(ship.X, ship.Y, x, z) < 220)) continue;
                if (world.Nodes.Any(node => World.Distance(node.X, node.Y, x, z) < 120)) continue;
                if (world.Sites.Any(site => World.Distance(site.X, site.Y, x, z) < 120)) continue;
                if (world.Settlements.Any(port => World.Distance(port.X, port.Y, x, z) < 160)) continue;
                var open = true;
                for (var dz = -80; dz <= 80 && open; dz += 10)
                for (var dx = -160; dx <= 80 && open; dx += 10)
                    open = !world.OnLand(x + dx, z + dz);
                if (open) return new Vector3(x, 0f, z);
            }
            Assert.Fail("No quiet open water on the map.");
            return default;
        }

        static double Gap(Ship a, Ship b) => World.Distance(a.TargetX, a.TargetY, b.TargetX, b.TargetY);

        [UnityTest]
        public IEnumerator Orders_AGroupMoveGivesEachShipItsOwnPlaceInTheChosenFormation()
        {
            yield return null;
            var world = battle.World;
            var spot = QuietWater(world);
            var group = Enumerable.Range(0, 4).Select(i => world.SpawnShip(0, ShipClass.Ram, spot.x - 120f, spot.z + (i - 2) * 30f)).ToArray();
            battle.RunTicks(1);
            foreach (var ship in group) battle.SelectForTest(ship.Id);
            var spacing = Formation.Spacing(group);

            battle.SetGroupFormation(FormationKind.Line, false);
            battle.OrderAt(spot);
            battle.RunTicks(1);
            Assert.That(group.All(ship => ship.Order == OrderKind.Move), Is.True, "Open water: a move, not an attack or an escort.");
            for (var i = 0; i < group.Length; i++)
            for (var j = i + 1; j < group.Length; j++)
                Assert.That(Gap(group[i], group[j]), Is.GreaterThanOrEqualTo(spacing - 0.01), "Each ship its own place, oars clear.");
            Assert.That(group.Count(ship => World.Distance(ship.TargetX, ship.TargetY, spot.x, spot.z) < 0.01), Is.Zero, "A line of four has no ship on the point itself.");

            battle.SetGroupFormation(FormationKind.Wedge, false);
            battle.OrderAt(spot);
            battle.RunTicks(1);
            Assert.That(group.Count(ship => World.Distance(ship.TargetX, ship.TargetY, spot.x, spot.z) < 0.01), Is.EqualTo(1), "A wedge has its leader on the point.");
            for (var i = 0; i < group.Length; i++)
            for (var j = i + 1; j < group.Length; j++)
                Assert.That(Gap(group[i], group[j]), Is.GreaterThanOrEqualTo(spacing - 0.01));

            battle.ClearSelectionKeepPort();
            battle.SelectForTest(group[0].Id);
            battle.OrderAt(spot);
            battle.RunTicks(1);
            Assert.That(World.Distance(group[0].TargetX, group[0].TargetY, spot.x, spot.z), Is.LessThan(0.01), "A single ship goes to the point itself.");
        }

        [UnityTest]
        public IEnumerator Hud_TheFormationSwitchShowsForAGroupAndNamesTheFormation()
        {
            yield return WaitForInterface();
            var world = battle.World;
            var spot = QuietWater(world);
            var ram = world.SpawnShip(0, ShipClass.Ram, spot.x, spot.z);
            var cutter = world.SpawnShip(0, ShipClass.Cutter, spot.x + 30f, spot.z);
            battle.RunTicks(1);
            battle.SetGroupFormation(FormationKind.Line, false);

            battle.SelectForTest(ram.Id);
            yield return WaitForInterface();
            Assert.That(battle.Hud.FormationSwitchForTest, Is.Null, "One ship goes to the point; there is no formation to choose.");

            battle.SelectForTest(cutter.Id);
            yield return WaitForInterface();
            Assert.That(battle.Hud.FormationSwitchForTest, Is.EqualTo("Szyk: linia"));

            battle.SetGroupFormation(FormationKind.Wedge, false);
            yield return WaitForInterface();
            Assert.That(battle.Hud.FormationSwitchForTest, Is.EqualTo("Szyk: klin"));
        }
    }
}
