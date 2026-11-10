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
    /// <summary>
    /// The buildings' destruction in the battle (docs/art/DESTRUCTION.md): a port shelled by a ship of the other side keeps
    /// where the shots struck (the game's projectile_hit_settlement), shows the damage its hull gives, and looks the same
    /// after a quick save and load (DestructionBook beside the save). The battle's state is never changed by it.
    /// </summary>
    public sealed class DestructionPresentationTests
    {
        GameObject host;
        BattleController battle;
        string path;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Battle under test");
            battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            path = Path.Combine(Application.temporaryCachePath, "destruction-test.mbsave");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
            foreach (var file in new[] { path, ShipLookBook.PathBeside(path), DestructionBook.PathBeside(path), path + ".bak", ShipLookBook.PathBeside(path) + ".bak", DestructionBook.PathBeside(path) + ".bak" })
                if (File.Exists(file)) File.Delete(file);
        }

        Settlement Home => battle.World.SettlementOf(BattleController.PlayerFaction);

        /// <summary>Open sea within a ballista's reach of the home port, clear of land round it.</summary>
        Vector3 OpenWaterNearHome()
        {
            var home = Home;
            for (var radius = 70f; radius < 135f; radius += 8f)
            for (var angle = 0f; angle < 360f; angle += 15f)
            {
                var x = (float)home.X + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                var z = (float)home.Y + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                var open = true;
                for (var dx = -16f; dx <= 16f && open; dx += 8f)
                for (var dz = -16f; dz <= 16f && open; dz += 8f)
                    open = !battle.World.OnLand(x + dx, z + dz);
                if (open) return new Vector3(x, 0f, z);
            }
            Assert.Fail("No open water within reach of the home port.");
            return default;
        }

        [UnityTest]
        public IEnumerator Destruction_AShelledPortKeepsItsRuinsThroughASave()
        {
            yield return null;
            var home = Home;
            home.IsTown = home.HasSageSchool = true;
            battle.RunTicks(0);
            var buildings = battle.PortBuildings[home.Id];
            Assert.That(buildings.Shows(PortBuilding.SageSchool), Is.True);

            // A ship of the other side shells the home port until its shots strike.
            var at = OpenWaterNearHome();
            var shooter = battle.World.SpawnShip(1, ShipClass.ShipKiller, at.x, at.z);
            var hull = home.Hull;
            for (var i = 0; i < 90 && buildings.Record.Impacts.Count == 0; i++)
            {
                battle.World.Issue(new Command { Faction = 1, Kind = CommandKind.AttackSettlement, ShipIds = new[] { shooter.Id }, TargetId = home.Id });
                battle.RunTicks(20);
            }
            Assert.That(buildings.Record.Impacts.Count, Is.GreaterThan(0), "the shots that struck are noted where they fell");
            Assert.That(home.Hull, Is.LessThan(hull), "the game took them off the hull");
            var struck = buildings.Record.Events[0];
            Assert.That(struck.Category, Is.EqualTo("bolt"));
            Assert.That(new Vector2(struck.DirX, struck.DirZ).magnitude, Is.EqualTo(1f).Within(1e-3f), "with the way they flew");

            // Far gone (as more shots would take it), the School shows it: elements fail and come to rest.
            home.Hull = home.MaxHull * 0.3;
            battle.RunTicks(0);
            // The fall in game time, the game itself held.
            for (var i = 0; i < 20 && buildings.Collapsing; i++) buildings.Update(home, false, 0.25f);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Broken, Is.True);
            Assert.That(view.Stage, Is.EqualTo(DestructionStage.PartialCollapse).Or.EqualTo(DestructionStage.LocalFailure));
            var failed = view.Graph.Elements.Where(e => view.IsFailed(e.Index)).Select(e => e.Id).OrderBy(id => id).ToList();
            var own = buildings.Record.Failures.Select(f => f.Element).ToList();

            battle.QuickSave(path);
            Assert.That(File.Exists(DestructionBook.PathBeside(path)), Is.True, "the record is written beside the save");
            Assert.That(battle.QuickLoad(path), Is.True);
            yield return null;
            battle.RunTicks(0);
            var loaded = battle.PortBuildings[Home.Id];
            Assert.That(loaded, Is.Not.SameAs(buildings), "the load built the port anew");
            var again = loaded.DamageOf(PortBuilding.SageSchool);
            Assert.That(again, Is.Not.Null);
            Assert.That(again.Busy, Is.False, "nothing falls again after a load");
            Assert.That(loaded.Record.Failures.Select(f => f.Element), Is.EqualTo(own), "the same failures, in the same order");
            Assert.That(again.Graph.Elements.Where(e => again.IsFailed(e.Index)).Select(e => e.Id).OrderBy(id => id), Is.EqualTo(failed));
        }
    }
}
