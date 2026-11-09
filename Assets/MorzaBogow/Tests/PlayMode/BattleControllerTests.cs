using System.Collections;
using System.IO;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    public sealed class BattleControllerTests
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

        [UnityTest]
        public IEnumerator Settlement_ModelStandsOnItsLandAndKeepsThePortClear()
        {
            yield return null;
            foreach (var settlement in battle.World.Settlements)
            {
                var view = battle.SettlementViews[settlement.Id];
                Assert.That(view.transform.position.x, Is.EqualTo((float)Rules.SettlementLandX(settlement)).Within(1e-3f));

                var islet = view.transform.Find("Islet").GetComponent<Renderer>().bounds;
                // The beach runs out under the water just past the waterline; at the water it matches the land circle.
                Assert.That(islet.extents.x, Is.EqualTo((float)Rules.SettlementLandRadius * 1.1f).Within(0.05f), "The islet matches the collision circle.");

                // The nearest point a cutter's hull can reach is the land edge plus nothing more than the jetty tip.
                var jetty = view.transform.Find("Jetty").GetComponent<Renderer>().bounds;
                var portSide = Mathf.Max(Mathf.Abs(jetty.min.x - (float)settlement.X), Mathf.Abs(jetty.max.x - (float)settlement.X));
                var jettyGap = Mathf.Min(Mathf.Abs(jetty.min.x - (float)settlement.X), Mathf.Abs(jetty.max.x - (float)settlement.X));
                Assert.That(jettyGap, Is.GreaterThan(0f), "The jetty does not reach the port point.");
                Assert.That(portSide, Is.LessThanOrEqualTo((float)Rules.SettlementLandOffset));
            }
        }

        [UnityTest]
        public IEnumerator Views_MatchSimulationShips()
        {
            yield return null;
            Assert.That(battle.Views.Count, Is.EqualTo(battle.World.Ships.Count));
            var extra = battle.World.SpawnShip(0, ShipClass.Ram, -400, 0);
            battle.RunTicks(1);
            Assert.That(battle.Views.ContainsKey(extra.Id), Is.True);
            extra.Hull = 0;
            battle.RunTicks(1);
            Assert.That(battle.Views.ContainsKey(extra.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator Fog_HiddenEnemyHasNoActiveRenderers()
        {
            yield return null;
            var enemy = battle.World.Ships.First(ship => ship.Owner == 1);
            Assert.That(battle.World.CanSee(0, enemy), Is.False);
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].gameObject.activeSelf, Is.False);
            var renderers = battle.Views[enemy.Id].GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Assert.That(renderers.Count(renderer => renderer.gameObject.activeInHierarchy), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Fog_EnemyBecomesVisibleWhenObserved()
        {
            yield return null;
            var enemy = battle.World.Ships.First(ship => ship.Owner == 1);
            battle.World.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator Visual_DoesNotChangeSimulationResult()
        {
            yield return null;
            var reference = MapFactory.CreateSkirmish(20261109);
            var opponent = new Opponent(1);
            for (var i = 0; i < 600; i++) { opponent.Think(reference); reference.Step(); }
            battle.RunTicks(600);
            yield return null;
            reference.Events.Clear();
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(reference))));
        }

        [UnityTest]
        public IEnumerator Order_RightClickOnNodeSendsCutterToHarvest()
        {
            yield return null;
            var cutter = battle.World.Ships.First(ship => ship.Owner == 0 && ship.Class == ShipClass.Cutter);
            var node = battle.World.Nodes.First(item => (item.KnownMask & 1) != 0);
            battle.SelectForTest(cutter.Id);
            battle.OrderAt(new Vector3((float)node.X, 0f, (float)node.Y));
            battle.RunTicks(1);
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Harvest));
            Assert.That(cutter.NodeId, Is.EqualTo(node.Id));
        }

        [UnityTest]
        public IEnumerator Save_LoadRestoresStateAndViews()
        {
            yield return null;
            var path = Path.Combine(Application.temporaryCachePath, "battle-test.mbsave");
            battle.RunTicks(300);
            battle.QuickSave(path);
            var saved = SaveGame.Hash(SaveGame.Serialize(battle.World));
            battle.RunTicks(300);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.Not.EqualTo(saved));

            Assert.That(battle.QuickLoad(path), Is.True);
            yield return null;
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.EqualTo(saved));
            Assert.That(battle.Views.Count, Is.EqualTo(battle.World.Ships.Count));
            File.Delete(path);
        }

        Ship HiddenEnemy() => battle.World.Ships.First(ship => ship.Owner == 1);

        [UnityTest]
        public IEnumerator Inspect_ShowsVisibleEnemyAndNeverAHiddenOne()
        {
            yield return null;
            var enemy = HiddenEnemy();
            var point = new Vector3((float)enemy.X, 0f, (float)enemy.Y);
            battle.Inspect(point);
            Assert.That(battle.InspectedEnemies, Is.Empty, "A hidden ship cannot be inspected.");

            var observer = battle.World.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);
            battle.Inspect(new Vector3((float)enemy.X, 0f, (float)enemy.Y));
            Assert.That(battle.InspectedEnemies, Does.Contain(enemy.Id));

            observer.Hull = 0;
            battle.RunTicks(1);
            Assert.That(battle.InspectedEnemies, Is.Empty, "Inspection ends when the ship leaves view.");
        }

        [UnityTest]
        public IEnumerator Target_EnemyIsMarkedAfterAttackOrderAndNotBefore()
        {
            yield return null;
            var world = battle.World;
            var enemy = HiddenEnemy();
            var attacker = world.SpawnShip(0, ShipClass.CrewDestroyer, enemy.X - 60, enemy.Y);
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].IsMarkedAsTarget, Is.False);

            world.Issue(new Command { Faction = 0, Kind = CommandKind.AttackShip, ShipIds = new[] { attacker.Id }, TargetId = enemy.Id });
            battle.RunTicks(1);
            Assert.That(battle.TargetedShips, Does.Contain(enemy.Id));
            Assert.That(battle.Views[enemy.Id].IsMarkedAsTarget, Is.True);

            world.Issue(new Command { Faction = 0, Kind = CommandKind.Stop, ShipIds = new[] { attacker.Id } });
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].IsMarkedAsTarget, Is.False);
        }

        [Test]
        public void Waves_HigherBowLiftsTheBowAndTiltIsLimited()
        {
            var calm = HullPose.FromHeights(1f, 1f, 1f, 1f, 1f, 20f, 6f);
            Assert.That(calm.Heave, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(calm.Pitch, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(calm.Roll, Is.EqualTo(0f).Within(1e-4f));

            var bowUp = HullPose.FromHeights(1f, -1f, 0f, 0f, 0f, 20f, 6f);
            Assert.That(bowUp.Pitch, Is.LessThan(0f), "Negative rotation around X raises the bow.");

            var starboardUp = HullPose.FromHeights(0f, 0f, -0.5f, 0.5f, 0f, 20f, 6f);
            Assert.That(starboardUp.Roll, Is.GreaterThan(0f));

            var steep = HullPose.FromHeights(50f, -50f, -50f, 50f, 0f, 10f, 4f);
            Assert.That(Mathf.Abs(steep.Pitch), Is.EqualTo(HullPose.MaxTiltDegrees).Within(1e-4f));
            Assert.That(Mathf.Abs(steep.Roll), Is.EqualTo(HullPose.MaxTiltDegrees).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Visual_ShipHasNoWakeStrip()
        {
            yield return null;
            var view = battle.Views.Values.First();
            Assert.That(view.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Wake" || item.name == "Order line"), Is.False);
        }

        [UnityTest]
        public IEnumerator Fog_LayerFollowsTheThreeStates()
        {
            yield return null;
            var world = battle.World;
            battle.Fog.Update(world, 0, 0f, true);
            var home = world.SettlementOf(0);
            int Cell(double value) => (int)((value - World.FogOrigin) / World.FogCellSize);

            Assert.That(battle.Fog.AlphaAt(Cell(home.X), Cell(home.Y)), Is.EqualTo(0), "Own base is visible.");
            Assert.That(battle.Fog.AlphaAt(Cell(375), Cell(0)), Is.EqualTo(BattleFog.UnexploredAlpha), "Enemy base starts unexplored.");

            var scout = world.SpawnShip(0, ShipClass.Cutter, 0, 650);
            world.RefreshExplored();
            battle.Fog.Update(world, 0, 0f, true);
            Assert.That(battle.Fog.AlphaAt(Cell(0), Cell(650)), Is.EqualTo(0));

            world.Ships.Remove(scout);
            battle.Fog.Update(world, 0, 0f, true);
            Assert.That(battle.Fog.AlphaAt(Cell(0), Cell(650)), Is.EqualTo(BattleFog.ExploredAlpha), "Explored but no longer observed.");
        }

        [UnityTest]
        public IEnumerator Site_PileShrinksWithStockAndEmptiesAtZero()
        {
            yield return null;
            var node = battle.World.Nodes.First(item => item.Type == ResourceType.Wood && (item.KnownMask & 1) != 0);
            var site = battle.SiteViews[node.Id];
            Assert.That(site.VisiblePileParts, Is.EqualTo(site.TotalPileParts));
            Assert.That(site.TotalPileParts, Is.GreaterThan(4));
            var full = node.Stock;
            site.UpdateStock(full / 2);
            Assert.That(site.VisiblePileParts, Is.LessThan(site.TotalPileParts).And.GreaterThan(0));
            site.UpdateStock(1);
            Assert.That(site.VisiblePileParts, Is.EqualTo(1));
            site.UpdateStock(0);
            Assert.That(site.VisiblePileParts, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HealthBar_AppearsOnlyAfterMouseSelection()
        {
            yield return null;
            var world = battle.World;
            var own = world.Ships.First(ship => ship.Owner == 0);
            var enemy = HiddenEnemy();
            world.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);

            Assert.That(world.CanSee(0, enemy), Is.True);
            Assert.That(battle.ShowsHealthBar(own), Is.False, "An unselected own ship has no bar.");
            Assert.That(battle.ShowsHealthBar(enemy), Is.False, "An unselected visible enemy has no bar.");

            battle.SelectForTest(own.Id);
            Assert.That(battle.ShowsHealthBar(own), Is.True);

            battle.Inspect(new Vector3((float)enemy.X, 0f, (float)enemy.Y));
            Assert.That(battle.ShowsHealthBar(enemy), Is.True);
            Assert.That(battle.ShowsHealthBar(world.Ships.First(ship => ship.Owner == 0 && ship.Id != own.Id)), Is.False);
        }

        [UnityTest]
        public IEnumerator HealthBar_SeveralEnemiesCanBeSelectedAtOnce()
        {
            yield return null;
            var world = battle.World;
            var enemies = world.Ships.Where(ship => ship.Owner == 1).Take(2).ToArray();
            foreach (var enemy in enemies) world.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);
            foreach (var enemy in enemies) battle.Inspect(new Vector3((float)enemy.X, 0f, (float)enemy.Y));
            Assert.That(enemies.All(battle.ShowsHealthBar), Is.True);
            Assert.That(battle.InspectedEnemies.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator HealthBar_SettlementShowsOnlyAfterClick()
        {
            yield return null;
            var home = battle.World.SettlementOf(0);
            Assert.That(battle.InspectedSettlementId, Is.EqualTo(-1));
            battle.Inspect(new Vector3((float)home.X, 0f, (float)home.Y));
            Assert.That(battle.InspectedSettlementId, Is.EqualTo(home.Id));
            var enemyHome = battle.World.SettlementOf(1);
            battle.Inspect(new Vector3((float)enemyHome.X, 0f, (float)enemyHome.Y));
            Assert.That(battle.InspectedSettlementId, Is.EqualTo(home.Id), "A hidden enemy settlement cannot be selected.");
        }

        [UnityTest]
        public IEnumerator Effects_HiddenVolleyCreatesNothingAndVisibleVolleyDoes()
        {
            yield return null;
            var world = battle.World;
            world.SpawnShip(1, ShipClass.CrewDestroyer, 900, 900);
            world.SpawnShip(0, ShipClass.CrewDestroyer, 900, -900).Soldiers = 0;
            var hiddenTarget = world.SpawnShip(0, ShipClass.Cutter, 950, 900);
            hiddenTarget.Hull = 100000;
            battle.Effects.Update(10f);
            var before = battle.Effects.ActiveCount;
            battle.RunTicks(120);
            Assert.That(world.CanSee(0, 950, 900), Is.True, "This cutter sees its own attacker, so effects are allowed.");
            Assert.That(battle.Effects.ActiveCount, Is.GreaterThan(before));

            var clean = new GameObject("Second battle").AddComponent<BattleController>();
            clean.Paused = true;
            yield return null;
            var enemyA = clean.World.SpawnShip(1, ShipClass.CrewDestroyer, 900, 900);
            var enemyB = clean.World.SpawnShip(0, ShipClass.Cutter, 950, 900);
            enemyB.Owner = 1;
            enemyA.Owner = 1;
            clean.World.Issue(new Command { Faction = 1, Kind = CommandKind.Stop, ShipIds = new[] { enemyA.Id } });
            clean.RunTicks(120);
            Assert.That(clean.World.CanSee(0, 900, 900), Is.False);
            Assert.That(clean.Effects.ActiveCount, Is.EqualTo(0));
            Object.DestroyImmediate(clean.gameObject);
        }

        [UnityTest]
        public IEnumerator Sink_VisibleShipLeavesAnAnimatedVisualWithoutSimulationEntry()
        {
            yield return null;
            var ship = battle.World.SpawnShip(0, ShipClass.Ram, -300, 100);
            battle.RunTicks(1);
            ship.Hull = 0;
            battle.RunTicks(1);
            Assert.That(battle.World.FindShip(ship.Id), Is.Null);
            Assert.That(battle.Views.ContainsKey(ship.Id), Is.False);
            Assert.That(battle.Effects.SinkingCount, Is.EqualTo(1));
            battle.Effects.Update(5f);
            Assert.That(battle.Effects.SinkingCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Wreck_ViewExistsOnlyWhileTheWreckDoesAndHidesInFog()
        {
            yield return null;
            var world = battle.World;
            var near = new Wreck { Id = world.NextId++, X = -300, Y = 0, Wood = 100, ExpiresTick = 100000 };
            var far = new Wreck { Id = world.NextId++, X = 900, Y = 900, Wood = 100, ExpiresTick = 100000 };
            world.Wrecks.Add(near);
            world.Wrecks.Add(far);
            battle.RunTicks(1);
            Assert.That(battle.WreckViews[near.Id].activeSelf, Is.True);
            Assert.That(battle.WreckViews[far.Id].activeSelf, Is.False);
            world.Wrecks.Remove(near);
            battle.RunTicks(1);
            Assert.That(battle.WreckViews.ContainsKey(near.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator Order_DescriptionIsPlainAndHidesUnseenTargets()
        {
            yield return null;
            var world = battle.World;
            var ram = world.SpawnShip(0, ShipClass.Ram, -300, 0);
            Assert.That(battle.DescribeOrder(ram), Is.EqualTo("Brak rozkazu."));
            var enemy = HiddenEnemy();
            ram.Order = OrderKind.Ram;
            ram.TargetId = enemy.Id;
            Assert.That(battle.DescribeOrder(ram), Does.Contain("cel poza zasięgiem"));
            Assert.That(battle.DescribeOrder(ram), Does.Not.Contain("Kuter"));
        }

        [UnityTest]
        public IEnumerator Load_DamagedFileKeepsTheCurrentGame()
        {
            yield return null;
            var path = Path.Combine(Application.temporaryCachePath, "battle-damaged.mbsave");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            var world = battle.World;
            Assert.That(battle.QuickLoad(path), Is.False);
            Assert.That(battle.World, Is.SameAs(world));
            File.Delete(path);
        }
    }
}
