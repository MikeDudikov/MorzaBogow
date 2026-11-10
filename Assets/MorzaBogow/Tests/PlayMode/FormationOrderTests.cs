using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// A group sent to one point gets a place for each ship in the chosen formation; when only part of a mixed group can do
    /// the clicked work, the rest follows in formation; the switch shows for a group only.
    /// </summary>
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
        public IEnumerator Orders_TheRestOfAMixedGroupFollowsInFormationWhenPartEscortsOrSalvages()
        {
            yield return null;
            var world = battle.World;
            var spot = QuietWater(world);
            var merchant = world.SpawnShip(0, ShipClass.Merchant, spot.x, spot.z);
            var rams = new[] { world.SpawnShip(0, ShipClass.Ram, spot.x - 120f, spot.z - 20f), world.SpawnShip(0, ShipClass.Ram, spot.x - 120f, spot.z + 20f) };
            var cutters = new[] { world.SpawnShip(0, ShipClass.Cutter, spot.x - 150f, spot.z - 20f), world.SpawnShip(0, ShipClass.Cutter, spot.x - 150f, spot.z + 20f) };
            battle.RunTicks(1);
            foreach (var ship in rams.Concat(cutters)) battle.SelectForTest(ship.Id);
            battle.SetGroupFormation(FormationKind.Line, false);

            // A right click on an own merchant: the warships escort it, the cutters follow to it in formation.
            var atMerchant = new Vector3((float)merchant.X, 0f, (float)merchant.Y);
            battle.OrderAt(atMerchant);
            battle.RunTicks(1);
            Assert.That(rams.All(ship => ship.Order == OrderKind.Escort && ship.EscortId == merchant.Id), Is.True, "The warships escort the merchant.");
            Assert.That(cutters.All(ship => ship.Order == OrderKind.Move), Is.True, "The cutters are not left without an order.");
            Assert.That(Gap(cutters[0], cutters[1]), Is.GreaterThanOrEqualTo(Formation.Spacing(cutters) - 0.01), "Each cutter its own place in the line.");
            Assert.That(cutters.All(ship => World.Distance(ship.TargetX, ship.TargetY, atMerchant.x, atMerchant.z) <= Formation.Spacing(cutters)), Is.True, "They gather at the merchant.");

            // A right click on a wreck 40 m short of the merchant, beyond its click radius: the cutters salvage it,
            // the warships leave the escort and follow to it in formation.
            var wreck = new Wreck { Id = world.NextId++, X = spot.x - 40f, Y = spot.z, Wood = 100, ExpiresTick = 100000 };
            world.Wrecks.Add(wreck);
            battle.RunTicks(1);
            var atWreck = new Vector3((float)wreck.X, 0f, (float)wreck.Y);
            battle.OrderAt(atWreck);
            battle.RunTicks(1);
            Assert.That(cutters.All(ship => ship.Order == OrderKind.Salvage && ship.WreckId == wreck.Id), Is.True, "The cutters salvage the wreck.");
            Assert.That(rams.All(ship => ship.Order == OrderKind.Move), Is.True, "The warships are not left escorting.");
            Assert.That(Gap(rams[0], rams[1]), Is.GreaterThanOrEqualTo(Formation.Spacing(rams) - 0.01), "Each warship its own place in the line.");
            Assert.That(rams.All(ship => World.Distance(ship.TargetX, ship.TargetY, atWreck.x, atWreck.z) <= Formation.Spacing(rams)), Is.True, "They gather at the wreck.");
        }

        [UnityTest]
        public IEnumerator Orders_TheRestOfAnEscortGroupStopsBesideTheMerchantWithoutCircling()
        {
            yield return null;
            var world = battle.World;
            var spot = QuietWater(world);
            // The merchant lies across their course, the hardest way to come alongside: its hull spans the line's places.
            var merchant = world.SpawnShip(0, ShipClass.Merchant, spot.x, spot.z);
            merchant.Heading = System.Math.PI / 2;
            var ram = world.SpawnShip(0, ShipClass.Ram, spot.x - 120f, spot.z - 40f);
            var cutters = Enumerable.Range(0, 3).Select(i => world.SpawnShip(0, ShipClass.Cutter, spot.x - 130f, spot.z + (i - 1) * 25f)).ToArray();
            battle.RunTicks(1);
            battle.SelectForTest(ram.Id);
            foreach (var ship in cutters) battle.SelectForTest(ship.Id);
            battle.SetGroupFormation(FormationKind.Line, false);

            battle.OrderAt(new Vector3((float)merchant.X, 0f, (float)merchant.Y));
            battle.RunTicks(60 * Rules.TickRate);
            Assert.That(ram.Order, Is.EqualTo(OrderKind.Escort));
            Assert.That(cutters.Select(ship => ship.Order), Is.All.EqualTo(OrderKind.Idle), "Each cutter reached its place and stopped.");
            Assert.That(cutters.Select(ship => World.Distance(ship.X, ship.Y, merchant.X, merchant.Y)), Is.All.LessThan(60), "They wait beside the merchant.");
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
