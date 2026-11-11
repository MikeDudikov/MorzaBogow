using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Plants of the sea cliffs (report 2.17): rock samphire (Crithmum maritimum) and sea lavender (Limonium) in
    /// clumps on the limestone faces above the notch, where the spray reaches, and on the rocky shores between beach
    /// and cliff. Each clump is a few crossed cards over a baked atlas (CliffPlantAtlas, built by the editor into
    /// Resources/Environment/CliffPlants); kind, size and turn come from stable hashes of the islet and the place on its
    /// shore. One mesh per islet, no shadows and no colliders. Small, stiff and fleshy, they stand still in the wind
    /// (a simplification). Presentation only.
    /// </summary>
    public static class CliffPlants
    {
        public enum Kind { Samphire, SeaLavender }

        /// <summary>The band on a cliff face: from just over the notch's lip up to where the face meets the slope (m).</summary>
        public const float FaceBottom = CoastFeatures.NotchTop + 0.2f, FaceTop = 5f;

        /// <summary>The band on a rocky shore, over the wet rock (m).</summary>
        public const float RockBottom = CoastFeatures.WetTop + 0.2f, RockTop = 1.6f;

        /// <summary>
        /// Width of a clump (m): samphire's cushions 15–50 cm high and wider than half a metre (report 8.15, 8.17), sea
        /// lavender 10–35 cm (8.20–8.21).
        /// </summary>
        public const float SamphireMin = 0.25f, SamphireMax = 0.55f, LavenderMin = 0.15f, LavenderMax = 0.35f;

        /// <summary>At most this many clumps on one islet.</summary>
        public const int MostClumps = 240;

        /// <summary>Places tried per outline sample, and the share of them that carry a clump on a cliff and on rock.</summary>
        const int Slots = 5;
        const float CliffChance = 0.5f, RockChance = 0.35f;

        /// <summary>Atlas cells (2 by 2): samphire from the side and from above, sea lavender's flower spray and rosette.</summary>
        public const int SamphireSide = 0, LavenderSpray = 1, LavenderRosette = 2, SamphireTop = 3;

        public struct Clump
        {
            public Kind Kind;

            /// <summary>Where it grows out of the rock, in the islet's space.</summary>
            public Vector3 Position;

            /// <summary>Level direction away from the islet's centre.</summary>
            public Vector3 Outward;

            public float Size, Yaw;

            /// <summary>The outline sample it grows by and how far toward the next one (0 to 1).</summary>
            public int Sample;
            public float Along;
            public bool OnCliff;
        }

        public sealed class Result
        {
            public Mesh Mesh;
            public readonly List<Clump> Clumps = new List<Clump>();
        }

        static Material material;

        /// <summary>The built material, or null before the editor has made it (then nothing is drawn).</summary>
        public static Material Material => material != null ? material : material = Resources.Load<Material>("Environment/CliffPlants");

        /// <summary>Whether a shore direction with this much cliff carries cliff plants, and the band of heights they take there.</summary>
        public static bool Band(float lift, out float bottom, out float top, out bool cliff)
        {
            cliff = lift >= CoastFeatures.CliffLift;
            var rocky = !cliff && lift > CoastFeatures.GentleLift;
            bottom = cliff ? FaceBottom : RockBottom;
            // On a cliff, up to the top of the steep face (fraction 0.97 of the radius), never over FaceTop.
            top = cliff ? Mathf.Min(FaceTop, ShoreShape.MoundHeight(0.97f, lift)) : Mathf.Min(RockTop, ShoreShape.MoundHeight(0.93f, lift));
            return (cliff || rocky) && top > bottom + 0.2f;
        }

        public static Result Make(float[] outline, float[] lift, uint seed)
        {
            var result = new Result();
            var samples = outline.Length;
            for (var i = 0; i < samples && result.Clumps.Count < MostClumps; i++)
            {
                if (!Band(lift != null ? lift[i] : 0f, out var bottom, out var top, out var cliff)) continue;
                var next = (i + 1) % samples;
                var nextFits = Band(lift != null ? lift[next] : 0f, out var nextBottom, out var nextTop, out _);
                for (var slot = 0; slot < Slots && result.Clumps.Count < MostClumps; slot++)
                {
                    var random = new StableHash.Sequence(StableHash.Mix(seed, (uint)i, 0xC11F0u + (uint)slot));
                    if (random.Next() > (cliff ? CliffChance : RockChance)) continue;
                    var height = random.Range(bottom, top);
                    // Between this sample and the next where both carry plants at this height, else right at this one.
                    var along = nextFits && height >= nextBottom && height <= nextTop ? random.Next() : 0f;
                    var position = Vector3.Lerp(Point(outline, lift, i, height), Point(outline, lift, next, height), along);
                    var lavender = cliff ? random.Next() < 0.3f : random.Next() < 0.6f;
                    result.Clumps.Add(new Clump
                    {
                        Kind = lavender ? Kind.SeaLavender : Kind.Samphire,
                        Position = position,
                        Outward = new Vector3(position.x, 0f, position.z).normalized,
                        Size = lavender ? random.Range(LavenderMin, LavenderMax) : random.Range(SamphireMin, SamphireMax),
                        Yaw = random.Range(0f, 180f),
                        Sample = i,
                        Along = along,
                        OnCliff = cliff,
                    });
                }
            }
            result.Mesh = Build(result.Clumps);
            return result;
        }

        /// <summary>Draws the clumps on an islet with the built material; nothing before the editor has built it.</summary>
        public static void Attach(Transform islet, Result result)
        {
            if (result?.Mesh == null || result.Mesh.vertexCount == 0 || Material == null) return;
            var item = new GameObject("Cliff plants");
            item.transform.SetParent(islet, false);
            item.AddComponent<MeshFilter>().sharedMesh = result.Mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            item.AddComponent<OwnedMesh>().Mesh = result.Mesh;
        }

        static Vector3 Point(float[] outline, float[] lift, int sample, float height)
        {
            var angle = ShoreShape.Angle(sample);
            var radius = CoastFeatures.SurfaceRadius(outline, lift, sample, height);
            return new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        }

        static Mesh Build(List<Clump> clumps)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var clump in clumps)
            {
                // Lit as foliage facing out and up, whichever side of a card is seen.
                var normal = (clump.Outward * 0.5f + Vector3.up).normalized;
                // The clump stands out from its crack, its foot in the rock.
                var centre = clump.Position + clump.Outward * (clump.Size * 0.3f);
                var s = clump.Size;
                if (clump.Kind == Kind.Samphire)
                {
                    for (var k = 0; k < 2; k++) Upright(vertices, normals, uvs, triangles, centre, clump.Yaw + 90f * k, s, 0.85f * s, -0.08f * s, normal, SamphireSide);
                    Flat(vertices, normals, uvs, triangles, centre + Vector3.up * (0.5f * s), clump.Yaw, 0.9f * s, SamphireTop);
                }
                else
                {
                    Flat(vertices, normals, uvs, triangles, centre + Vector3.up * 0.03f, clump.Yaw, 0.7f * s, LavenderRosette);
                    for (var k = 0; k < 2; k++) Upright(vertices, normals, uvs, triangles, centre, clump.Yaw + 45f + 90f * k, s, 1.1f * s, 0f, normal, LavenderSpray);
                }
            }
            var mesh = new Mesh { name = "Cliff plants", indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A point of an atlas cell, in the quarters the trees' leaf atlases use (TreeGenerator.AtlasCells).</summary>
        static Vector2 Cell(int cell, float u, float v) => TreeGenerator.AtlasCells[cell] + new Vector2(u, v) * 0.5f;

        internal static void Upright(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 centre, float yaw, float width, float height, float bottom, Vector3 normal, int cell)
        {
            var across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (0.5f * width);
            var start = vertices.Count;
            vertices.Add(centre - across + Vector3.up * bottom);
            vertices.Add(centre + across + Vector3.up * bottom);
            vertices.Add(centre + across + Vector3.up * (bottom + height));
            vertices.Add(centre - across + Vector3.up * (bottom + height));
            for (var k = 0; k < 4; k++) normals.Add(normal);
            uvs.Add(Cell(cell, 0f, 0f)); uvs.Add(Cell(cell, 1f, 0f)); uvs.Add(Cell(cell, 1f, 1f)); uvs.Add(Cell(cell, 0f, 1f));
            Quad(triangles, start);
        }

        internal static void Flat(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 centre, float yaw, float width, int cell) =>
            Flat(vertices, normals, uvs, triangles, centre, yaw, width, TreeGenerator.AtlasCells[cell], 0.5f);

        /// <summary>A level card over any square of the atlas: its lower-left corner and side, in texture coordinates.</summary>
        internal static void Flat(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 centre, float yaw, float width, Vector2 corner, float extent)
        {
            var turn = Quaternion.Euler(0f, yaw, 0f);
            var right = turn * Vector3.right * (0.5f * width);
            var forward = turn * Vector3.forward * (0.5f * width);
            var start = vertices.Count;
            vertices.Add(centre - right - forward);
            vertices.Add(centre + right - forward);
            vertices.Add(centre + right + forward);
            vertices.Add(centre - right + forward);
            for (var k = 0; k < 4; k++) normals.Add(Vector3.up);
            uvs.Add(corner + new Vector2(0f, 0f) * extent); uvs.Add(corner + new Vector2(1f, 0f) * extent);
            uvs.Add(corner + new Vector2(1f, 1f) * extent); uvs.Add(corner + new Vector2(0f, 1f) * extent);
            Quad(triangles, start);
        }

        /// <summary>Both faces of a card: the material is single-sided, as the olive's leaves.</summary>
        static void Quad(List<int> triangles, int start)
        {
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
    }
}
