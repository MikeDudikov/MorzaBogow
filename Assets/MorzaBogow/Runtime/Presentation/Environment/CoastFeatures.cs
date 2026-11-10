using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// The shoreline of an islet as the sea has worked it, over the islet's own mound (ShoreShape):
    /// <list type="bullet">
    /// <item>a tidal notch where the limestone falls in a cliff: the sea cuts it at its own level, deepest on smooth
    /// vertical limestone (report 3.x, Evelpidou et al. 2014); drawn as the overhanging lip above the recess, at the
    /// game's sea level, which is the ancient one;</item>
    /// <item>a wet band along the waterline, darker and glossier;</item>
    /// <item>beachrock on the gentle shores: beach sand and gravel cemented in the swash zone, broken into slabs in
    /// rows along the shore, dipping gently seaward (report 3.2, Desruelles et al. 2009).</item>
    /// </list>
    /// Presentation only, no colliders: the waterline the ships steer by is the simulation's.
    /// </summary>
    public static class CoastFeatures
    {
        /// <summary>Notch: roof and lip above the game's sea level (m) and how far the lip stands out (m).</summary>
        public const float NotchRoof = 0.55f, NotchTop = 1.7f, NotchDepthMin = 0.5f, NotchDepthMax = 1.1f;

        /// <summary>The wet band runs from under the water to this height (m).</summary>
        public const float WetTop = 0.45f;

        /// <summary>Least cliff (ShoreShape lift) that carries a notch, and most that still has a gentle shore.</summary>
        public const float CliffLift = 0.55f, GentleLift = 0.25f;

        public sealed class Result
        {
            public Mesh Notch, Wet, Beachrock;

            /// <summary>Pebble beaches along the gentle shores (Shingle): where they lie, the wet rock band gives way.</summary>
            public Shingle.Result Shingle;

            /// <summary>Rock samphire and sea lavender on the cliff faces and rocky shores (CliffPlants).</summary>
            public CliffPlants.Result Plants;
            public int Slabs;
        }

        public static Result Build(Transform islet, float[] outline, float[] lift, uint seed, Material rock, Material wetRock, Material beachrock, Material pebbles = null)
        {
            var result = Make(outline, lift, seed);
            Attach(islet, "Notch", result.Notch, rock);
            Attach(islet, "Wet band", result.Wet, wetRock != null ? wetRock : rock);
            Attach(islet, "Beachrock", result.Beachrock, beachrock != null ? beachrock : rock);
            // Shingle: the built CC0 pebbles, or the beachrock's sandy stone until they are built.
            var shingle = pebbles != null ? pebbles : Landscape.Shingle.Pebbles != null ? Landscape.Shingle.Pebbles : beachrock != null ? beachrock : rock;
            Attach(islet, "Shingle", result.Shingle.Dry, shingle);
            Attach(islet, "Shingle (wet)", result.Shingle.Wet, LandscapeArt.Wet(shingle));
            CliffPlants.Attach(islet, result.Plants);
            return result;
        }

        static void Attach(Transform parent, string name, Mesh mesh, Material material)
        {
            if (mesh == null || mesh.vertexCount == 0) return;
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            item.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        /// <summary>The meshes alone, in the islet's space (tests and previews).</summary>
        public static Result Make(float[] outline, float[] lift, uint seed)
        {
            var shingle = Landscape.Shingle.Make(outline, lift, seed);
            var result = new Result { Notch = Notch(outline, lift, seed), Wet = Wet(outline, lift, shingle), Shingle = shingle, Plants = CliffPlants.Make(outline, lift, seed) };
            result.Beachrock = Beachrock(outline, lift, seed, out result.Slabs);
            return result;
        }

        /// <summary>Radius of the islet's surface at a height, in one direction: where the mound crosses that height.</summary>
        public static float SurfaceRadius(float[] outline, float[] lift, int sample, float height)
        {
            float lo = 0.5f, hi = 1.1f;
            var l = lift != null ? lift[sample] : 0f;
            for (var i = 0; i < 30; i++)
            {
                var mid = 0.5f * (lo + hi);
                if (ShoreShape.MoundHeight(mid, l) > height) lo = mid; else hi = mid;
            }
            return outline[sample] * 0.5f * (lo + hi);
        }

        static Vector3 Point(int sample, float radius, float height)
        {
            var angle = ShoreShape.Angle(sample);
            return new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        }

        static float Cliff(float[] lift, int sample) => lift == null ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CliffLift, 0.8f, lift[sample]));

        static Mesh Notch(float[] outline, float[] lift, uint seed)
        {
            var samples = outline.Length;
            // The lip's cross-section: roof inner, roof outer, front, top front, top inner (radial share of depth, height).
            var profile = new[] { new Vector2(-0.05f, NotchRoof), new Vector2(0.95f, NotchRoof + 0.08f), new Vector2(1f, 0.5f * (NotchRoof + NotchTop)), new Vector2(0.55f, NotchTop - 0.2f), new Vector2(-0.05f, NotchTop) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var rows = new List<int>();
            for (var i = 0; i < samples; i++)
            {
                var strength = Cliff(lift, i);
                if (strength <= 0f)
                {
                    rows.Add(-1);
                    continue;
                }
                var depth = strength * Mathf.Lerp(NotchDepthMin, NotchDepthMax, StableHash.Unit(StableHash.Mix(seed, (uint)i, 0x40u)));
                rows.Add(vertices.Count);
                foreach (var p in profile) vertices.Add(Point(i, SurfaceRadius(outline, lift, i, p.y) + p.x * depth, p.y));
            }
            for (var i = 0; i < samples; i++)
            {
                var a = rows[i];
                var b = rows[(i + 1) % samples];
                if (a < 0 || b < 0) continue;
                for (var k = 0; k + 1 < profile.Length; k++)
                {
                    // Facing seaward and down under the roof, up on the top.
                    triangles.Add(a + k); triangles.Add(a + k + 1); triangles.Add(b + k);
                    triangles.Add(b + k); triangles.Add(a + k + 1); triangles.Add(b + k + 1);
                }
            }
            return Finish("Notch", vertices, triangles);
        }

        static Mesh Wet(float[] outline, float[] lift, Shingle.Result shingle)
        {
            var samples = outline.Length;
            var heights = new[] { -0.45f, -0.1f, 0.2f, WetTop };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < samples; i++)
            {
                var notch = Cliff(lift, i) > 0f;
                foreach (var h in heights)
                {
                    // Under a notch the wet rock reaches up to its roof; elsewhere to the swash's height.
                    var height = notch && h >= WetTop ? NotchRoof : h;
                    vertices.Add(Point(i, SurfaceRadius(outline, lift, i, height) + 0.03f, height + 0.03f));
                }
            }
            var row = heights.Length;
            for (var i = 0; i < samples; i++)
            {
                // Where shingle lies, its own wet part takes the swash.
                if (shingle != null && shingle.Covers(i) && shingle.Covers((i + 1) % samples)) continue;
                var a = i * row;
                var b = (i + 1) % samples * row;
                for (var k = 0; k + 1 < row; k++)
                {
                    triangles.Add(a + k); triangles.Add(a + k + 1); triangles.Add(b + k);
                    triangles.Add(b + k); triangles.Add(a + k + 1); triangles.Add(b + k + 1);
                }
            }
            return Finish("Wet band", vertices, triangles);
        }

        static Mesh Beachrock(float[] outline, float[] lift, uint seed, out int slabs)
        {
            slabs = 0;
            var samples = outline.Length;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // The band runs along stretches of a gentle shore (a slow noise along it), as a pavement of slabs broken by
            // narrow joints: one to three rows, the landward just out of the water, the seaward awash or under it.
            var phase = StableHash.Unit(StableHash.Mix(seed, 0xB1u)) * 10f;
            for (var i = 0; i < samples; i++)
            {
                if (lift != null && lift[i] > GentleLift) continue;
                var band = 0.5f + 0.35f * Mathf.Sin(i * 0.45f + phase) + 0.15f * Mathf.Sin(i * 1.3f + 2.1f * phase);
                if (band < 0.55f) continue;
                var random = new StableHash.Sequence(StableHash.Mix(seed, (uint)i, 0xB3u));
                var rows = band > 0.85f ? 3 : band > 0.7f ? 2 : 1;
                var a0 = ShoreShape.Angle(i);
                var a1 = ShoreShape.Angle(i + 1);
                for (var r = 0; r < rows; r++)
                {
                    var height = 0.12f - 0.26f * r;
                    var start = Point(i, SurfaceRadius(outline, lift, i, height), height);
                    var end = Point((i + 1) % samples, SurfaceRadius(outline, lift, (i + 1) % samples, height), height);
                    var run = end - start;
                    var pieces = Mathf.Max(1, Mathf.RoundToInt(run.magnitude / random.Range(1.4f, 2.2f)));
                    var step = run / pieces;
                    for (var p = 0; p < pieces; p++)
                    {
                        var angle = Mathf.Lerp(a0, a1, (p + 0.5f) / pieces);
                        var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        var centre = start + step * (p + 0.5f) + outward * (0.6f * r) + Vector3.up * random.Range(-0.04f, 0.04f);
                        var length = step.magnitude - random.Range(0.08f, 0.2f);
                        var width = random.Range(1.0f, 1.4f);
                        var thick = random.Range(0.22f, 0.32f);
                        var dip = random.Range(5f, 9f);
                        var turn = Quaternion.AngleAxis(random.Range(-3f, 3f), Vector3.up) * Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(dip, 0f, 0f);
                        Slab(vertices, triangles, centre, turn, new Vector3(length, thick, width));
                        slabs++;
                    }
                }
            }
            return Finish("Beachrock", vertices, triangles);
        }

        /// <summary>A slab: a box with its own faces (flat shading), top bevelled a little toward the sea.</summary>
        static void Slab(List<Vector3> vertices, List<int> triangles, Vector3 centre, Quaternion turn, Vector3 size)
        {
            var h = size * 0.5f;
            var corners = new Vector3[8];
            for (var c = 0; c < 8; c++)
            {
                var x = (c & 1) == 0 ? -h.x : h.x;
                var y = (c & 2) == 0 ? -h.y : h.y;
                var z = (c & 4) == 0 ? -h.z : h.z;
                // The top's seaward edge worn down by the swash.
                if (y > 0f && z > 0f) y -= 0.35f * size.y;
                corners[c] = centre + turn * new Vector3(x, y, z);
            }
            int[][] faces =
            {
                new[] { 2, 6, 7, 3 }, new[] { 0, 1, 5, 4 }, new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            foreach (var face in faces)
            {
                var start = vertices.Count;
                foreach (var index in face) vertices.Add(corners[index]);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }

        static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Destroys a mesh made at run time together with the object that draws it.</summary>
    public sealed class OwnedMesh : MonoBehaviour
    {
        public Mesh Mesh;

        void OnDestroy()
        {
            if (Mesh == null) return;
            if (Application.isPlaying) Destroy(Mesh); else DestroyImmediate(Mesh);
        }
    }
}
