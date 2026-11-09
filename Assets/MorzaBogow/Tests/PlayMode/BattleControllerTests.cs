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
