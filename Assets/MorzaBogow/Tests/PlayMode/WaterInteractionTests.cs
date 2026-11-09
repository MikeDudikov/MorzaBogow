using System.Collections;
using System.Collections.Generic;
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
            foreach (ShipClass shipClass in System.Enum.GetValues(typeof(ShipClass)))
            {
                var definition = ShipCatalog.Get(shipClass);
                var empty = ShipVisual.DraftFor((float)definition.Draft, 0f);
                var full = ShipVisual.DraftFor((float)definition.Draft, 1f);
                Assert.That(empty, Is.GreaterThan(0f), "An empty hull still sits in the water.");
                Assert.That(full, Is.GreaterThanOrEqualTo(empty));
                Assert.That(full, Is.LessThan((float)definition.Beam * 0.6f), $"{shipClass}: even loaded, part of the side stays above water.");
                Assert.That(ShipVisual.DraftFor((float)definition.Draft, 5f), Is.EqualTo(full), "Load is clamped.");
            }
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
                Assert.That(decals.Select(item => item.name), Is.EquivalentTo(new[] { "Bow wave", "Hull foam", "Bow foam" }));
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

        [Test]
        public void Motion_LagsBehindACrestOvershootsAndSettles()
        {
            var motion = new HullMotion(20f, 6f);
            motion.Reset(new HullPose(0f, 0f, 0f));
            var crest = new HullPose(1f, -5f, 4f);

            var early = motion.Step(crest, 0.1f);
            Assert.That(early.Heave, Is.GreaterThan(0f).And.LessThan(0.5f), "Mass: it does not jump onto the crest.");

            var peak = 0f;
            for (var i = 0; i < 60; i++) peak = Mathf.Max(peak, motion.Step(crest, 0.05f).Heave);
            Assert.That(peak, Is.GreaterThan(1f), "Inertia carries it a little past the crest.");

            for (var i = 0; i < 600; i++) motion.Step(crest, 0.05f);
            Assert.That(motion.Pose.Heave, Is.EqualTo(1f).Within(0.01f));
            Assert.That(motion.Pose.Pitch, Is.EqualTo(-5f).Within(0.05f));
            Assert.That(motion.Pose.Roll, Is.EqualTo(4f).Within(0.05f));
        }

        [Test]
        public void Motion_BiggerHullsMoveMoreSlowly()
        {
            var cutter = new HullMotion(12f, 3.4f);
            var merchant = new HullMotion(24f, 10f);
            Assert.That(merchant.HeavePeriod, Is.GreaterThan(cutter.HeavePeriod));
            Assert.That(merchant.RollPeriod, Is.GreaterThan(cutter.RollPeriod));
        }

        [Test]
        public void Motion_TiltStaysWithinTheLimitEvenWhenOvershooting()
        {
            var motion = new HullMotion(12f, 3f);
            motion.Reset(new HullPose(0f, 0f, 0f));
            for (var i = 0; i < 200; i++)
            {
                var pose = motion.Step(new HullPose(0f, HullPose.MaxTiltDegrees, -HullPose.MaxTiltDegrees), 0.05f);
                Assert.That(Mathf.Abs(pose.Pitch), Is.LessThanOrEqualTo(HullPose.MaxTiltDegrees + 1e-4f));
                Assert.That(Mathf.Abs(pose.Roll), Is.LessThanOrEqualTo(HullPose.MaxTiltDegrees + 1e-4f));
            }
        }

        [Test]
        public void Breaking_ComesFromSpeedAndFromABowDrivingIntoACrest()
        {
            Assert.That(ShipVisual.BreakingFor(0f, 0f, 0f), Is.Zero, "At rest on calm water nothing breaks.");
            Assert.That(ShipVisual.BreakingFor(1f, 0f, 0f), Is.GreaterThan(ShipVisual.BreakingFor(0.3f, 0f, 0f)));
            Assert.That(ShipVisual.BreakingFor(0.5f, 0.6f, 0f), Is.GreaterThan(ShipVisual.BreakingFor(0.5f, 0f, 0f)), "A crest at the stem breaks harder.");
            Assert.That(ShipVisual.BreakingFor(0.5f, -0.6f, 0f), Is.EqualTo(ShipVisual.BreakingFor(0.5f, 0f, 0f)), "A bow lifted clear breaks nothing extra.");

            // Water 0.5 m over a level stem: positive immersion; a bow raised 2 m above it: negative.
            Assert.That(HullMotion.BowImmersion(new HullPose(0f, 0f, 0f), 0.5f, 20f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(HullMotion.BowImmersion(new HullPose(0f, -11.3f, 0f), 0f, 20f), Is.LessThan(-1.5f));
        }

        [Test]
        public void Shore_FollowsTheLandShapeAndFallsAway()
        {
            var ellipse = ShoreShape.Ellipse(19f, 14f);
            Assert.That(ellipse[0], Is.EqualTo(19f).Within(1e-3f));
            Assert.That(ellipse[ShoreShape.Samples / 4], Is.EqualTo(14f).Within(1e-3f));

            var mesh = ShoreShape.Shelf(ellipse, 3.0);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            Assert.That(normals.Average(normal => normal.y), Is.GreaterThan(0.5f), "The seabed faces up.");
            Assert.That(vertices.Max(vertex => vertex.y), Is.LessThan(0f), "Never above the water.");
            var deepest = vertices.OrderBy(vertex => vertex.y).First();
            Assert.That(new Vector2(deepest.x, deepest.z).magnitude, Is.GreaterThan(19f + 30f), "Deepest far from the land.");

            // The waterline ring follows the ellipse, not a circle.
            var ring = vertices.Skip(1 + ShoreShape.Samples).Take(ShoreShape.Samples).ToArray();
            Assert.That(new Vector2(ring[0].x, ring[0].z).magnitude, Is.EqualTo(19f).Within(1e-3f));
            Assert.That(new Vector2(ring[ShoreShape.Samples / 4].x, ring[ShoreShape.Samples / 4].z).magnitude, Is.EqualTo(14f).Within(1e-3f));

            var surf = ShoreShape.SurfPoints(ellipse, ShoreShape.SurfSpacing).ToList();
            Assert.That(surf.Count, Is.GreaterThan(8));
            foreach (var (point, _) in surf)
            {
                var angle = Mathf.Atan2(point.z, point.x);
                Assert.That(point.magnitude - ShoreShape.At(ellipse, angle), Is.InRange(0.5f, 3.5f), "Surf sits along the waterline.");
            }
        }

        [Test]
        public void Shore_OutlineFromPointsKeepsTheShape()
        {
            var points = new List<Vector2>();
            for (var i = 0; i < 2000; i++)
            {
                var angle = i * 0.0031f * Mathf.PI * 2f;
                var radius = 1f + 0.3f * Mathf.Cos(angle * 3f);
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * Random.Range(0.2f, 1f));
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            var outline = ShoreShape.FromPoints(points);
            Assert.That(outline[0], Is.EqualTo(1.3f).Within(0.08f), "A lobe stays a lobe.");
            Assert.That(outline[ShoreShape.Samples / 6], Is.EqualTo(0.7f).Within(0.08f), "A bay stays a bay.");
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

                Assert.That(battle.SurfCount, Is.GreaterThan(battle.World.Islands.Count * 6), "Surf runs along every shore.");
                var hiddenSurf = Object.FindObjectsByType<WaterDecal>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(decal => decal.name == "Surf")
                    .Where(decal =>
                    {
                        var centre = decal.transform.parent.position;
                        return !battle.World.IsExplored(0, centre.x, centre.z) && battle.World.Islands.Any(island => island.Kind != IslandKind.Coast
                            && Mathf.Approximately((float)island.X, centre.x) && Mathf.Approximately((float)island.Y, centre.z));
                    })
                    .ToList();
                Assert.That(hiddenSurf, Is.Not.Empty, "The map has unexplored islets at the start.");
                var shownUnderFog = hiddenSurf.Where(decal => decal.isActiveAndEnabled).Select(decal => decal.transform.parent.position.ToString()).Distinct();
                Assert.That(shownUnderFog, Is.Empty, "Surf shown under the fog at these shores.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}
