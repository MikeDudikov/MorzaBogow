using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Presentation.Landscape;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>The shore of an islet: notch under the cliffs, wet swash and beachrock on the gentle side.</summary>
    public sealed class CoastTests
    {
        static (float[] outline, float[] lift) Islet() => (ShoreShape.Ellipse(60f, 45f), ShoreShape.Cliffs(0f, 60f, 110f, 1.3f));

        static int Sample(float[] outline, Vector3 point)
        {
            var angle = Mathf.Atan2(point.z, point.x);
            if (angle < 0f) angle += 2f * Mathf.PI;
            return Mathf.RoundToInt(angle / (2f * Mathf.PI) * outline.Length) % outline.Length;
        }

        [Test]
        public void Coast_TheNotchCutsOnlyTheCliffsAtTheGamesSeaLevel()
        {
            var (outline, lift) = Islet();
            var notch = CoastFeatures.Make(outline, lift, 7).Notch;
            var vertices = notch.vertices;
            Assert.That(vertices.Length, Is.GreaterThan(0), "An islet with cliffs has a notch.");
            Assert.That(vertices.Min(p => p.y), Is.EqualTo(CoastFeatures.NotchRoof).Within(0.01f), "Its roof sits just above the water the sea cut it at.");
            Assert.That(vertices.Max(p => p.y), Is.LessThanOrEqualTo(CoastFeatures.NotchTop + 0.01f));
            Assert.That(vertices.All(p => lift[Sample(outline, p)] >= CoastFeatures.CliffLift - 0.1f), Is.True, "No notch on a gentle shore.");
            var standing = vertices.Count(p =>
            {
                var sample = Sample(outline, p);
                return new Vector2(p.x, p.z).magnitude > CoastFeatures.SurfaceRadius(outline, lift, sample, p.y) + 0.2f;
            });
            Assert.That(standing, Is.GreaterThan(vertices.Length / 4), "The lip stands out over the recess cut into the face.");
        }

        [Test]
        public void Coast_BeachrockLiesAwashOnTheGentleShoreAndDipsSeaward()
        {
            var (outline, lift) = Islet();
            var made = CoastFeatures.Make(outline, lift, 7);
            Assert.That(made.Slabs, Is.GreaterThan(3), "A gentle shore carries a few rows of slabs.");
            var vertices = made.Beachrock.vertices;
            Assert.That(vertices.Max(p => p.y), Is.LessThan(0.6f), "Beachrock forms in the swash: it barely stands out of the water.");
            Assert.That(vertices.Min(p => p.y), Is.GreaterThan(-1.3f));
            Assert.That(vertices.All(p => lift[Sample(outline, p)] <= CoastFeatures.GentleLift + 0.15f), Is.True, "None under the cliffs.");
            var normals = made.Beachrock.normals;
            var tops = Enumerable.Range(0, vertices.Length).Where(i => normals[i].y > 0.9f).ToArray();
            var seaward = tops.Count(i => Vector3.Dot(new Vector3(normals[i].x, 0f, normals[i].z), new Vector3(vertices[i].x, 0f, vertices[i].z).normalized) > 0f);
            Assert.That(seaward, Is.GreaterThan(tops.Length * 3 / 4), "The slabs dip toward the sea.");
        }

        [Test]
        public void Coast_TheWetBandHugsTheWaterline()
        {
            var (outline, lift) = Islet();
            var wet = CoastFeatures.Make(outline, lift, 7).Wet.vertices;
            Assert.That(wet.Min(p => p.y), Is.LessThan(0f), "It runs from under the water...");
            Assert.That(wet.Max(p => p.y), Is.LessThanOrEqualTo(CoastFeatures.NotchRoof + 0.05f), "...to the swash or the notch's roof, no higher.");
        }

        [Test]
        public void Coast_SameSeedSameShore()
        {
            var (outline, lift) = Islet();
            var a = CoastFeatures.Make(outline, lift, 7);
            var b = CoastFeatures.Make(outline, lift, 7);
            Assert.That(b.Notch.vertices, Is.EqualTo(a.Notch.vertices));
            Assert.That(b.Beachrock.vertices, Is.EqualTo(a.Beachrock.vertices));
            Assert.That(CoastFeatures.Make(outline, lift, 8).Beachrock.vertexCount != a.Beachrock.vertexCount || CoastFeatures.Make(outline, lift, 8).Slabs != a.Slabs ||
                CoastFeatures.Make(outline, lift, 8).Beachrock.vertices[0] != a.Beachrock.vertices[0], Is.True, "Another islet, other slabs.");
        }
    }
}
