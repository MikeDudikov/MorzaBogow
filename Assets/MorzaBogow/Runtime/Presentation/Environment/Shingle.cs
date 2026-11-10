using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Pebble beaches on the gentle shores of an islet: a strip of rounded shingle from just under the water up past the
    /// swash, wet and dark below the reach of the waves, pale and dry above (report 3.x). Stretches of it alternate with
    /// bare shore along a slow noise; beachrock slabs (CoastFeatures) rest on it where both occur. Material: the CC0
    /// Poly Haven set dry_river_pebbles, built by the editor (ShingleArt) into Resources/Environment/ShorePebbles, its wet
    /// part a darker, glossier copy. No colliders: presentation only.
    /// </summary>
    public static class Shingle
    {
        public const float Bottom = -0.35f, WetTop = 0.22f, Top = 0.7f;

        /// <summary>Height of the strip over the islet's own surface, so the two never fight for the same depth.</summary>
        public const float Lift = 0.05f;

        public sealed class Result
        {
            public Mesh Wet, Dry;
            public bool[] Covered;
            public int Samples;

            /// <summary>Whether shingle lies along the shore at this outline sample (the wet rock band leaves it then).</summary>
            public bool Covers(int sample) => Covered != null && sample >= 0 && sample < Covered.Length && Covered[sample];
        }

        static Material pebbles;

        /// <summary>The built shingle material, or null before the editor has made it.</summary>
        public static Material Pebbles => pebbles != null ? pebbles : pebbles = Resources.Load<Material>("Environment/ShorePebbles");

        public static Result Make(float[] outline, float[] lift, uint seed)
        {
            var samples = outline.Length;
            var result = new Result { Covered = new bool[samples] };
            var phase = StableHash.Unit(StableHash.Mix(seed, 0x5B1u)) * 10f;
            for (var i = 0; i < samples; i++)
            {
                if (lift != null && lift[i] > CoastFeatures.GentleLift) continue;
                var stretch = 0.5f + 0.4f * Mathf.Sin(i * 0.31f + phase) + 0.15f * Mathf.Sin(i * 1.1f + 1.7f * phase);
                result.Covered[i] = stretch > 0.35f;
            }
            result.Wet = Strip(outline, lift, result.Covered, new[] { Bottom, -0.08f, WetTop }, "Shingle (wet)");
            result.Dry = Strip(outline, lift, result.Covered, new[] { WetTop, 0.45f, Top }, "Shingle");
            foreach (var covered in result.Covered) if (covered) result.Samples++;
            return result;
        }

        /// <summary>Rows at these heights along every covered stretch, hugging the islet's surface, facing out and up.</summary>
        static Mesh Strip(float[] outline, float[] lift, bool[] covered, float[] heights, string name)
        {
            var samples = outline.Length;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var rows = heights.Length;
            for (var i = 0; i < samples; i++)
            {
                var next = (i + 1) % samples;
                if (!covered[i] || !covered[next]) continue;
                var start = vertices.Count;
                foreach (var sample in new[] { i, next })
                {
                    var angle = ShoreShape.Angle(sample);
                    foreach (var h in heights)
                    {
                        var radius = CoastFeatures.SurfaceRadius(outline, lift, sample, h);
                        vertices.Add(new Vector3(Mathf.Cos(angle) * radius, h + Lift, Mathf.Sin(angle) * radius));
                    }
                }
                for (var k = 0; k + 1 < rows; k++)
                {
                    int a = start + k, b = start + rows + k;
                    // As the wet band: outward and up.
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
