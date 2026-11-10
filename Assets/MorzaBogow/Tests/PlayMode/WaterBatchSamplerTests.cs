using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>The sea under every hull is read in one batch; the points and poses come out where and how the hull lies.</summary>
    public sealed class WaterBatchSamplerTests
    {
        /// <summary>A stand-in sea, a tilted plane. It counts the batches it is asked for and can refuse to be read.</summary>
        sealed class TiltedSea : IWaterHeights
        {
            public int Batches, LastCount;
            public bool Readable = true;

            public static float HeightAt(Vector3 point) => 0.3f + 0.1f * point.x - 0.05f * point.z;

            public bool TryHeights(Vector3[] points, int count, float[] heights)
            {
                Batches++;
                LastCount = count;
                if (!Readable) return false;
                for (var i = 0; i < count; i++) heights[i] = HeightAt(points[i]);
                return true;
            }
        }

        /// <summary>A stand-in sea standing level at one height; it keeps the points of its last batch.</summary>
        sealed class LevelSea : IWaterHeights
        {
            public const float Level = 0.7f;
            public int Batches;
            public Vector3[] LastPoints = new Vector3[0];

            public bool TryHeights(Vector3[] points, int count, float[] heights)
            {
                Batches++;
                LastPoints = points.Take(count).ToArray();
                for (var i = 0; i < count; i++) heights[i] = Level;
                return true;
            }
        }

        [Test]
        public void Water_FivePointsPerHullAlongItsHeadingAndBeam()
        {
            var turn = Quaternion.Euler(0f, 30f, 0f);
            var forward = turn * Vector3.forward;
            var right = turn * Vector3.right;
            var centre = new Vector3(10f, 0f, 5f);
            var points = new Vector3[HullSamplePoints.Count + 2];
            HullSamplePoints.Write(centre, forward, right, 20f, 6f, points, 2);
            Assert.That(Vector3.Distance(points[2 + HullSamplePoints.Bow], centre + forward * 10f), Is.LessThan(1e-4f), "The stem: half the length ahead.");
            Assert.That(Vector3.Distance(points[2 + HullSamplePoints.Stern], centre - forward * 10f), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(points[2 + HullSamplePoints.Port], centre - right * 3f), Is.LessThan(1e-4f), "Port is to the left, half the beam out.");
            Assert.That(Vector3.Distance(points[2 + HullSamplePoints.Starboard], centre + right * 3f), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(points[2 + HullSamplePoints.Center], centre), Is.LessThan(1e-4f));
            Assert.That(points[0], Is.EqualTo(Vector3.zero), "Points before the offset stay untouched.");
        }

        [Test]
        public void Water_EveryHullIsReadInOneBatchAndItsPoseFollowsTheSea()
        {
            var sea = new TiltedSea();
            var sampler = new WaterBatchSampler(sea);
            var hulls = new List<(Vector3 centre, Quaternion turn, float length, float beam)>();
            sampler.Begin();
            for (var i = 0; i < 40; i++)
            {
                var hull = (centre: new Vector3(i * 15f - 300f, 0f, i % 7 * 20f), turn: Quaternion.Euler(0f, i * 37f, 0f), length: 12f + i % 4 * 4f, beam: 3.4f + i % 3 * 2f);
                hulls.Add(hull);
                Assert.That(sampler.Add(hull.centre, hull.turn * Vector3.forward, hull.turn * Vector3.right, hull.length, hull.beam), Is.EqualTo(i));
            }
            Assert.That(sampler.Sample(), Is.True);
            Assert.That(sea.Batches, Is.EqualTo(1), "One batch for all hulls, in every frame.");
            Assert.That(sea.LastCount, Is.EqualTo(40 * HullSamplePoints.Count));
            Assert.That(sampler.Samples, Is.EqualTo(40 * HullSamplePoints.Count));
            Assert.That(sampler.MostHulls, Is.EqualTo(40));

            for (var i = 0; i < hulls.Count; i++)
            {
                var (centre, turn, length, beam) = hulls[i];
                var ahead = turn * Vector3.forward * (length * 0.5f);
                var side = turn * Vector3.right * (beam * 0.5f);
                var expected = HullPose.FromHeights(TiltedSea.HeightAt(centre + ahead), TiltedSea.HeightAt(centre - ahead),
                    TiltedSea.HeightAt(centre - side), TiltedSea.HeightAt(centre + side), TiltedSea.HeightAt(centre), length, beam);
                var pose = sampler.Pose(i, length, beam);
                Assert.That(pose.Heave, Is.EqualTo(expected.Heave).Within(1e-4f), $"hull {i}");
                Assert.That(pose.Pitch, Is.EqualTo(expected.Pitch).Within(1e-3f), $"hull {i}");
                Assert.That(pose.Roll, Is.EqualTo(expected.Roll).Within(1e-3f), $"hull {i}");
                Assert.That(sampler.Height(i, HullSamplePoints.Bow), Is.EqualTo(TiltedSea.HeightAt(centre + ahead)).Within(1e-4f));
            }

            // Heading east up the slope: the bow stands higher than the stern, and starboard (south) higher than port.
            sampler.Begin();
            var east = Quaternion.Euler(0f, 90f, 0f);
            sampler.Add(Vector3.zero, east * Vector3.forward, east * Vector3.right, 20f, 6f);
            Assert.That(sampler.Sample(), Is.True);
            var climbing = sampler.Pose(0, 20f, 6f);
            Assert.That(climbing.Pitch, Is.LessThan(0f), "A higher bow pitches the hull bow-up, negative about X.");
            Assert.That(climbing.Roll, Is.GreaterThan(0f), "A higher starboard rolls it, positive about Z.");
        }

        [Test]
        public void Water_AnUnreadableSeaIsCountedAndAnEmptyBatchAsksNothing()
        {
            var sea = new TiltedSea { Readable = false };
            var sampler = new WaterBatchSampler(sea);
            sampler.Begin();
            sampler.Add(Vector3.zero, Vector3.forward, Vector3.right, 20f, 6f);
            sampler.Add(Vector3.one, Vector3.forward, Vector3.right, 20f, 6f);
            Assert.That(sampler.Sample(), Is.False, "No sea data this frame: the hulls keep their last pose.");
            Assert.That(sampler.Failures, Is.EqualTo(2 * HullSamplePoints.Count));
            Assert.That(sampler.Samples, Is.Zero);

            sea.Readable = true;
            sampler.Begin();
            Assert.That(sampler.Sample(), Is.False, "Nothing to read without a hull.");
            Assert.That(sea.Batches, Is.EqualTo(1), "An empty batch does not ask the sea.");
        }

        [Test]
        public void Water_HdrpHeightsWithoutASeaReadNothingAndReleaseTwiceSafely()
        {
            var heights = new HdrpWaterHeights(null);
            var points = new Vector3[HullSamplePoints.Count];
            var read = new float[HullSamplePoints.Count];
            Assert.That(heights.TryHeights(points, points.Length, read), Is.False, "No sea, nothing to read.");
            Assert.That(heights.BufferAllocations, Is.Zero, "Nothing is allocated without a sea.");

            heights.Dispose();
            heights.Dispose();
            Assert.That(heights.TryHeights(points, points.Length, read), Is.False, "A released source reads nothing.");
            Assert.That(heights.BufferAllocations, Is.Zero, "And it never allocates again.");

            var sampler = new WaterBatchSampler(heights);
            sampler.Begin();
            sampler.Add(Vector3.zero, Vector3.forward, Vector3.right, 20f, 6f);
            Assert.That(sampler.Sample(), Is.False);
            Assert.That(sampler.Failures, Is.EqualTo(HullSamplePoints.Count));
        }

        [UnityTest]
        public IEnumerator Water_TheBattleReadsEveryVisibleHullAndNoHiddenOne()
        {
            var host = new GameObject("Battle under test");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            var sea = new LevelSea();
            try
            {
                // The first frame places the views; the stand-in sea is read from the next one.
                yield return null;
                battle.UseWaterForTest(sea);
                battle.RunTicks(1);

                var world = battle.World;
                var visible = world.Ships.Where(ship => battle.Views.ContainsKey(ship.Id) && world.CanSee(0, ship)).ToList();
                var hidden = world.Ships.Where(ship => battle.Views.ContainsKey(ship.Id) && !world.CanSee(0, ship)).ToList();
                Assert.That(visible, Is.Not.Empty);
                Assert.That(hidden, Is.Not.Empty, "The enemy fleet starts hidden.");
                Assert.That(sea.Batches, Is.EqualTo(1), "One batch a frame.");
                Assert.That(sea.LastPoints.Length, Is.EqualTo(visible.Count * HullSamplePoints.Count), "Every visible hull, at all five points.");
                Assert.That(battle.Water.MostHulls, Is.EqualTo(visible.Count));

                foreach (var ship in hidden)
                {
                    var at = new Vector3((float)ship.X, 0f, (float)ship.Y);
                    var reach = (float)ship.Definition.Radius * 2f + 1f;
                    Assert.That(sea.LastPoints.Any(point => Vector3.Distance(new Vector3(point.x, 0f, point.z), at) < reach), Is.False,
                        "A hidden ship's water is never read, so its waves cannot give it away.");
                    Assert.That(battle.Views[ship.Id].CurrentPose.Heave, Is.Zero);
                }
                foreach (var ship in visible)
                    Assert.That(battle.Views[ship.Id].CurrentPose.Heave, Is.EqualTo(LevelSea.Level).Within(1e-3f), "A visible hull rides the sea it read.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}
