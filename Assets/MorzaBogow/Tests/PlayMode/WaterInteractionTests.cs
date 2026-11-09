using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>Hulls sit in the water, push up a bow wave and break it into foam; shores have surf. Presentation only.</summary>
    public sealed class WaterInteractionTests
    {
        [Test]
        public void Draft_GrowsWithTheLoadAndStaysWithinTheHull()
        {
            var empty = ShipVisual.DraftFor(4f, 0f);
            var full = ShipVisual.DraftFor(4f, 1f);
            Assert.That(empty, Is.GreaterThan(0f), "An empty hull still sits in the water.");
            Assert.That(full, Is.GreaterThan(empty));
            Assert.That(full, Is.LessThan(4f * 0.6f), "Even loaded, most of the side stays above water.");
            Assert.That(ShipVisual.DraftFor(4f, 5f), Is.EqualTo(full), "Load is clamped.");
        }

        [Test]
        public void BowWave_IsZeroAtRestAndGrowsWithSpeedAndLength()
        {
            Assert.That(ShipVisual.BowWaveFor(20f, 0f), Is.Zero);
            Assert.That(ShipVisual.BowWaveFor(20f, 1f), Is.GreaterThan(ShipVisual.BowWaveFor(20f, 0.5f)));
            Assert.That(ShipVisual.BowWaveFor(26f, 1f), Is.GreaterThan(ShipVisual.BowWaveFor(12f, 1f)));
            Assert.That(ShipVisual.HullFoamFor(0f), Is.GreaterThan(0f).And.LessThan(0.5f), "A thin waterline even at rest.");
            Assert.That(ShipVisual.HullFoamFor(1f), Is.GreaterThan(ShipVisual.HullFoamFor(0.3f)));
        }

        [Test]
        public void Ship_HasFlatWaterDecalsThatFollowItsSpeed()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Merchant, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                var decals = view.GetComponentsInChildren<WaterDecal>(true);
                Assert.That(decals.Select(item => item.name), Is.EquivalentTo(new[] { "Bow wave", "Hull foam" }));
                foreach (var decal in decals) Assert.That(decal.transform.parent, Is.EqualTo(view.transform), "On the root, so waves never tilt the decal.");

                view.Animate(0f, 0f, 0.1f);
                Assert.That(view.BowWaveHeight, Is.Zero);
                var restingFoam = view.HullFoam;
                view.Animate(1f, 1f, 0.1f);
                Assert.That(view.BowWaveHeight, Is.GreaterThan(0f));
                Assert.That(view.HullFoam, Is.GreaterThan(restingFoam));

                var emptyDraft = view.Draft;
                view.SetLoad(1f);
                for (var i = 0; i < 100; i++) view.Animate(0f, i * 0.1f, 0.1f);
                Assert.That(view.Draft, Is.GreaterThan(emptyDraft), "A loaded merchant sits lower.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Fog_HiddenEnemyMakesNoWavesAndUnexploredLandNoSurf()
        {
            var host = new GameObject("Battle under test");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            try
            {
                yield return null;
                var enemy = battle.World.Ships.First(ship => ship.Owner == 1);
                battle.RunTicks(1);
                Assert.That(battle.World.CanSee(0, enemy), Is.False);
                var decals = battle.Views[enemy.Id].GetComponentsInChildren<WaterDecal>(true);
                Assert.That(decals, Is.Not.Empty);
                Assert.That(decals.All(decal => !decal.isActiveAndEnabled), Is.True, "Its bow wave would show where it sails.");

                Assert.That(battle.SurfCount, Is.EqualTo(battle.World.Islands.Count));
                var hiddenSurf = Object.FindObjectsByType<WaterDecal>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(decal => decal.name == "Surf")
                    .Where(decal => !battle.World.IsExplored(0, decal.transform.position.x, decal.transform.position.z))
                    .Where(decal => battle.World.Islands.Any(island => island.Kind != IslandKind.Coast
                        && Mathf.Approximately((float)island.X, decal.transform.position.x) && Mathf.Approximately((float)island.Y, decal.transform.position.z)))
                    .ToList();
                Assert.That(hiddenSurf, Is.Not.Empty, "The map has unexplored islets at the start.");
                Assert.That(hiddenSurf.All(decal => !decal.isActiveAndEnabled), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}
