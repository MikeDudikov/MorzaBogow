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
        public void Coast_ShingleLiesOnTheGentleShoreFromUnderTheWaterToAboveTheSwash()
        {
            var (outline, lift) = Islet();
            var shingle = CoastFeatures.Make(outline, lift, 7).Shingle;
            Assert.That(shingle.Samples, Is.GreaterThan(outline.Length / 10), "A gentle shore has its pebble beaches...");
            Assert.That(shingle.Samples, Is.LessThan(outline.Length * 3 / 4), "...in stretches between bare shore.");

            var dry = shingle.Dry.vertices;
            var wet = shingle.Wet.vertices;
            Assert.That(wet.Min(p => p.y), Is.EqualTo(Shingle.Bottom + Shingle.Lift).Within(1e-4f), "It starts under the water...");
            Assert.That(dry.Max(p => p.y), Is.EqualTo(Shingle.Top + Shingle.Lift).Within(1e-4f), "...and reaches up past the swash.");
            Assert.That(wet.Max(p => p.y), Is.LessThanOrEqualTo(dry.Min(p => p.y) + 1e-4f), "Wet below, dry above.");
            var points = dry.Concat(wet).ToArray();
            Assert.That(points.All(p => lift[Sample(outline, p)] <= CoastFeatures.GentleLift), Is.True, "Never under the cliffs.");

            // Seen from the sea and from the strategic camera, as the shore it lies on.
            var normals = shingle.Dry.normals.Concat(shingle.Wet.normals).ToArray();
            var facing = Enumerable.Range(0, points.Length).Count(i =>
                normals[i].y > 0.05f && Vector3.Dot(new Vector3(normals[i].x, 0f, normals[i].z), new Vector3(points[i].x, 0f, points[i].z)) > 0f);
            Assert.That(facing, Is.GreaterThan(points.Length * 9 / 10), "The strip faces out and up.");

            Assert.That(CoastFeatures.Make(outline, lift, 7).Shingle.Dry.vertices, Is.EqualTo(dry), "Same islet, same beach.");
            var cliffs = Enumerable.Repeat(1.3f, outline.Length).ToArray();
            var none = CoastFeatures.Make(outline, cliffs, 7).Shingle;
            Assert.That(none.Samples, Is.Zero, "An islet of cliffs has no beach.");
            Assert.That(none.Dry.vertexCount + none.Wet.vertexCount, Is.Zero);
        }

        [Test]
        public void Coast_WhereShingleLiesItsOwnWetPartTakesTheSwash()
        {
            var (outline, lift) = Islet();
            var made = CoastFeatures.Make(outline, lift, 7);
            var vertices = made.Wet.vertices;
            var triangles = made.Wet.triangles;
            var underPebbles = 0;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                if (made.Shingle.Covers(Sample(outline, vertices[triangles[t]])) && made.Shingle.Covers(Sample(outline, vertices[triangles[t + 1]])) &&
                    made.Shingle.Covers(Sample(outline, vertices[triangles[t + 2]]))) underPebbles++;
            }
            Assert.That(underPebbles, Is.Zero, "The bare rock's wet band does not run under the pebbles.");
            Assert.That(triangles.Length, Is.GreaterThan(0), "Elsewhere it stays.");
        }

        [Test]
        public void Coast_CliffPlantsGrowOnTheFacesAboveTheNotchAndOnRockyShoresOnly()
        {
            var (outline, lift) = Islet();
            var plants = CoastFeatures.Make(outline, lift, 7).Plants;
            Assert.That(plants.Clumps.Count, Is.GreaterThan(10), "The cliffs carry their plants...");
            Assert.That(plants.Clumps.Count, Is.LessThanOrEqualTo(CliffPlants.MostClumps));
            Assert.That(plants.Clumps.Any(c => c.Kind == CliffPlants.Kind.Samphire) && plants.Clumps.Any(c => c.Kind == CliffPlants.Kind.SeaLavender), Is.True, "...samphire and sea lavender both.");
            foreach (var clump in plants.Clumps)
            {
                Assert.That(CliffPlants.Band(lift[clump.Sample], out var bottom, out var top, out var cliff), Is.True, "Never on a gentle shore or a beach.");
                Assert.That(clump.OnCliff, Is.EqualTo(cliff));
                Assert.That(clump.Position.y, Is.InRange(bottom - 1e-3f, top + 1e-3f), "In the spray zone: over the notch on a cliff, over the wet rock elsewhere.");
                Assert.That(bottom, Is.GreaterThan(cliff ? CoastFeatures.NotchTop : CoastFeatures.WetTop));
                // Rooted in the rock: as far from the islet's centre as the face itself at that height.
                var next = (clump.Sample + 1) % outline.Length;
                var a = CoastFeatures.SurfaceRadius(outline, lift, clump.Sample, clump.Position.y);
                var b = CoastFeatures.SurfaceRadius(outline, lift, next, clump.Position.y);
                var radius = new Vector2(clump.Position.x, clump.Position.z).magnitude;
                Assert.That(radius, Is.InRange(Mathf.Min(a, b) - 0.05f, Mathf.Max(a, b) + 0.05f), "On the face, not floating off it.");
                var (least, most) = clump.Kind == CliffPlants.Kind.Samphire ? (CliffPlants.SamphireMin, CliffPlants.SamphireMax) : (CliffPlants.LavenderMin, CliffPlants.LavenderMax);
                Assert.That(clump.Size, Is.InRange(least, most));
            }
            Assert.That(plants.Mesh.vertexCount, Is.EqualTo(plants.Clumps.Count * 12), "Three crossed cards each.");

            Assert.That(CoastFeatures.Make(outline, lift, 7).Plants.Clumps.Select(c => c.Position), Is.EqualTo(plants.Clumps.Select(c => c.Position)), "Same islet, same plants.");
            Assert.That(CoastFeatures.Make(outline, lift, 8).Plants.Clumps.Select(c => c.Position), Is.Not.EqualTo(plants.Clumps.Select(c => c.Position)), "Another islet, other plants.");
            var gentle = new float[outline.Length];
            Assert.That(CoastFeatures.Make(outline, gentle, 7).Plants.Clumps, Is.Empty, "A low islet of beaches has none.");
        }

        [Test]
        public void Coast_CliffPlantAtlasShowsGreySamphireAndVioletSeaLavender()
        {
            var atlas = CliffPlantAtlas.Bake(256);
            for (var cell = 0; cell < 4; cell++) Assert.That(atlas.Coverage(cell), Is.InRange(0.03f, 0.7f), $"Quarter {cell} holds a plant, with air around it.");
            var half = atlas.Size / 2;
            (float r, float g, float b, int violet) Mean(int cell)
            {
                var corner = TreeGenerator.AtlasCells[cell];
                int x0 = (int)(corner.x * atlas.Size), y0 = (int)(corner.y * atlas.Size), count = 0, violet = 0;
                float r = 0f, g = 0f, b = 0f;
                for (var y = 0; y < half; y++)
                {
                    for (var x = 0; x < half; x++)
                    {
                        var c = atlas.Colour[(y0 + y) * atlas.Size + x0 + x];
                        if (c.a <= 127) continue;
                        r += c.r; g += c.g; b += c.b; count++;
                        if (c.b > c.g + 12 && c.r > c.g) violet++;
                    }
                }
                return (r / count, g / count, b / count, violet);
            }
            var samphire = Mean(CliffPlants.SamphireSide);
            Assert.That(samphire.g, Is.GreaterThan(samphire.r).And.GreaterThan(samphire.b), "Samphire is grey-green.");
            Assert.That(Mean(CliffPlants.LavenderSpray).violet, Is.GreaterThan(20), "Sea lavender flowers violet.");
            Assert.That(Mean(CliffPlants.LavenderRosette).g, Is.GreaterThan(Mean(CliffPlants.LavenderRosette).b), "Its rosette is green.");
            Assert.That(CliffPlantAtlas.Bake(256).Colour, Is.EqualTo(atlas.Colour), "The same pixels every time.");
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
