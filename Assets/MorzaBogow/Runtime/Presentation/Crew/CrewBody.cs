using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>The bones of the crews' shared rig; each is one rigid segment of the body.</summary>
    public enum Bone { Pelvis, Spine, Chest, Neck, Head, UpperArmL, ForeArmL, HandL, UpperArmR, ForeArmR, HandR, ThighL, ShinL, FootL, ThighR, ShinR, FootR }

    /// <summary>
    /// The one rig every man aboard is drawn with: seventeen rigid segments jointed at the hips, spine, neck, shoulders,
    /// elbows, wrists, knees and ankles, in metres for a man of 1.66 m (the reference height; each man is scaled whole to
    /// his own height of 1.60 to 1.72 m, never to fit a place). Joint heights follow the standard proportions of the body
    /// (Drillis and Contini, after Winter): hips 0.53 H, knees 0.285 H, shoulders 0.818 H, ankles 0.039 H; upper arm
    /// 0.186 H, forearm 0.146 H, hand 0.108 H, shoulders 0.259 H wide. Bind pose: standing, facing +Z, arms down.
    /// </summary>
    public static class CrewBody
    {
        public const int Bones = 17;
        public const float ReferenceHeight = 1.66f;

        public static readonly int[] Parent = { -1, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 0, 11, 12, 0, 14, 15 };

        /// <summary>Each joint from its parent's joint in the bind pose; the pelvis from the floor between the feet.</summary>
        public static readonly Vector3[] Offset =
        {
            new Vector3(0f, 0.900f, 0f),        // pelvis (hips 0.88 = 0.53 H)
            new Vector3(0f, 0.075f, -0.01f),    // spine
            new Vector3(0f, 0.155f, 0.005f),    // chest
            new Vector3(0f, 0.245f, 0.005f),    // neck
            new Vector3(0f, 0.075f, 0.01f),     // head (the atlas, 1.45; crown at 1.66)
            new Vector3(-0.185f, 0.21f, -0.01f), // left shoulder (1.34 ≈ 0.818 H less the joint's depth)
            new Vector3(0f, -0.309f, 0f),       // left elbow
            new Vector3(0f, -0.242f, 0f),       // left wrist
            new Vector3(0.185f, 0.21f, -0.01f),
            new Vector3(0f, -0.309f, 0f),
            new Vector3(0f, -0.242f, 0f),
            new Vector3(-0.085f, -0.02f, 0f),   // left hip joint (0.88)
            new Vector3(0f, -0.407f, 0f),       // left knee (0.473)
            new Vector3(0f, -0.408f, 0f),       // left ankle (0.065)
            new Vector3(0.085f, -0.02f, 0f),
            new Vector3(0f, -0.407f, 0f),
            new Vector3(0f, -0.408f, 0f),
        };

        /// <summary>Lengths a two-bone chain reaches with, for IK: upper arm, forearm to the palm; thigh, shank.</summary>
        public const float UpperArm = 0.309f, ForeArm = 0.242f, Palm = 0.08f, Thigh = 0.407f, Shank = 0.408f, AnkleHeight = 0.065f;

        public static bool IsLeft(Bone bone) => bone == Bone.UpperArmL || bone == Bone.ForeArmL || bone == Bone.HandL || bone == Bone.ThighL || bone == Bone.ShinL || bone == Bone.FootL;

        /// <summary>The bind pose's joint positions from the floor, for a man of the reference height.</summary>
        public static Vector3[] BindPositions()
        {
            var positions = new Vector3[Bones];
            for (var i = 0; i < Bones; i++) positions[i] = (Parent[i] < 0 ? Vector3.zero : positions[Parent[i]]) + Offset[i];
            return positions;
        }
    }

    /// <summary>
    /// The palette of the crews' one material: flat cells of colour in an atlas, sRGB albedo without light baked in,
    /// with metallic and smoothness in the mask. Every part of a man takes its colours from cells by its UV, so men
    /// differ in skin, hair and dress without another material, and one instanced draw serves each part's mesh.
    /// </summary>
    public static class CrewPalette
    {
        public const int Cells = 8, CellPixels = 8, Size = Cells * CellPixels;

        /// <summary>The cells, row by row (8 to a row).</summary>
        public enum Cell
        {
            // Skin: light olive to dark, sunburnt (albedo of real skin, 0.25–0.6).
            Skin0, Skin1, Skin2, Skin3, Lips, Hair0, Hair1, Hair2,
            // Cloth: undyed linen, bleached linen, oatmeal wool, brown wool, ochre-dyed, worn linen, worn wool, bandage.
            Linen, Bleached, Wool, Brown, Ochre, LinenWorn, WoolWorn, Bandage,
            // The sides' dyes (ArtDirectionAegean #3): muted navy and dark red, and their worn shades; hide and paint of shields.
            Navy, Madder, NavyWorn, MadderWorn, Hide, HideDark, ShieldPaintNavy, ShieldPaintMadder,
            // Bronze, leather, wood, rope, wicker, iron-dark stone, bone white, pitch.
            Bronze, BronzeDark, Leather, LeatherLight, WoodShaft, Rope, Wicker, Stone,
        }

        public static Color Albedo(Cell cell) => cell switch
        {
            Cell.Skin0 => new Color(0.62f, 0.47f, 0.37f),
            Cell.Skin1 => new Color(0.55f, 0.40f, 0.30f),
            Cell.Skin2 => new Color(0.47f, 0.33f, 0.24f),
            Cell.Skin3 => new Color(0.39f, 0.26f, 0.18f),
            Cell.Lips => new Color(0.45f, 0.26f, 0.22f),
            Cell.Hair0 => new Color(0.05f, 0.04f, 0.035f),
            Cell.Hair1 => new Color(0.13f, 0.085f, 0.055f),
            Cell.Hair2 => new Color(0.36f, 0.34f, 0.31f),
            Cell.Linen => new Color(0.78f, 0.74f, 0.64f),
            Cell.Bleached => new Color(0.86f, 0.84f, 0.78f),
            Cell.Wool => new Color(0.63f, 0.57f, 0.46f),
            Cell.Brown => new Color(0.36f, 0.27f, 0.18f),
            Cell.Ochre => new Color(0.55f, 0.27f, 0.15f),
            Cell.LinenWorn => new Color(0.62f, 0.57f, 0.48f),
            Cell.WoolWorn => new Color(0.48f, 0.42f, 0.33f),
            Cell.Bandage => new Color(0.80f, 0.77f, 0.70f),
            Cell.Navy => new Color(0.13f, 0.18f, 0.30f),
            Cell.Madder => new Color(0.37f, 0.11f, 0.10f),
            Cell.NavyWorn => new Color(0.20f, 0.24f, 0.33f),
            Cell.MadderWorn => new Color(0.42f, 0.19f, 0.16f),
            Cell.Hide => new Color(0.52f, 0.40f, 0.28f),
            Cell.HideDark => new Color(0.30f, 0.22f, 0.15f),
            Cell.ShieldPaintNavy => new Color(0.16f, 0.22f, 0.38f),
            Cell.ShieldPaintMadder => new Color(0.48f, 0.13f, 0.11f),
            Cell.Bronze => new Color(0.70f, 0.48f, 0.26f),
            Cell.BronzeDark => new Color(0.42f, 0.33f, 0.22f),
            Cell.Leather => new Color(0.30f, 0.20f, 0.12f),
            Cell.LeatherLight => new Color(0.48f, 0.34f, 0.21f),
            Cell.WoodShaft => new Color(0.50f, 0.38f, 0.24f),
            Cell.Rope => new Color(0.60f, 0.51f, 0.36f),
            Cell.Wicker => new Color(0.64f, 0.52f, 0.33f),
            Cell.Stone => new Color(0.66f, 0.63f, 0.57f),
            _ => Color.magenta,
        };

        /// <summary>Metallic (bronze) and smoothness of a cell: oiled skin a little glossy, cloth matt, bronze polished.</summary>
        public static (float metallic, float smoothness) Surface(Cell cell) => cell switch
        {
            Cell.Bronze => (1f, 0.62f),
            Cell.BronzeDark => (0.85f, 0.42f),
            Cell.Skin0 or Cell.Skin1 or Cell.Skin2 or Cell.Skin3 or Cell.Lips => (0f, 0.38f),
            Cell.Hair0 or Cell.Hair1 or Cell.Hair2 => (0f, 0.32f),
            Cell.Leather or Cell.LeatherLight or Cell.Hide or Cell.HideDark => (0f, 0.3f),
            Cell.ShieldPaintNavy or Cell.ShieldPaintMadder => (0f, 0.28f),
            Cell.WoodShaft => (0f, 0.25f),
            _ => (0f, 0.12f),
        };

        /// <summary>The UV at the centre of a cell.</summary>
        public static Vector2 UV(Cell cell)
        {
            var index = (int)cell;
            return new Vector2((index % Cells + 0.5f) / Cells, 1f - (index / Cells + 0.5f) / Cells);
        }

        /// <summary>The albedo atlas (sRGB) and mask (linear: metallic, occlusion, detail, smoothness), point-sampled.</summary>
        public static (Texture2D albedo, Texture2D mask) Textures()
        {
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) { name = "Crew palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var mask = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true) { name = "Crew palette mask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var count = System.Enum.GetValues(typeof(Cell)).Length;
            for (var index = 0; index < Cells * Cells; index++)
            {
                var cell = (Cell)Mathf.Min(index, count - 1);
                var colour = index < count ? Albedo(cell) : Color.gray;
                var (metallic, smoothness) = index < count ? Surface(cell) : (0f, 0.2f);
                for (var y = 0; y < CellPixels; y++)
                for (var x = 0; x < CellPixels; x++)
                {
                    var px = index % Cells * CellPixels + x;
                    var py = Size - 1 - (index / Cells * CellPixels + y);
                    albedo.SetPixel(px, py, colour);
                    mask.SetPixel(px, py, new Color(metallic, 1f, 0f, smoothness));
                }
            }
            albedo.Apply(false, false);
            mask.Apply(false, false);
            return (albedo, mask);
        }
    }

    /// <summary>A man's look in numbers: what CrewLooks draws from his lasting identity, what CrewMeshes builds him from.</summary>
    public struct CrewLook
    {
        public float Height, Girth;
        public CrewPalette.Cell Skin, Hair, Cloth, Trim;
        public int Face, HairStyle, Beard;
        /// <summary>0 bare torso and kilt, 1 tunic, 2 corslet over a tunic (a leader).</summary>
        public int Dress;
        /// <summary>0 none, 1 hedgehog, 2 horned with a crest, 3 feathered, 4 leather cap.</summary>
        public int Helmet;
        public bool Greaves;
        /// <summary>0 none, 1 round with a cut-out, 2 round, 3 hourglass.</summary>
        public int Shield;
        public CrewPalette.Cell ShieldPaint;
    }

    /// <summary>
    /// The meshes of the crews, built in code from the rig's segments and cached: each segment of the body in its own
    /// joint's space (rounded ends that overlap the next segment, so no gap opens at a joint), in three levels of detail
    /// (LOD0 ≤ 800 triangles a man, LOD1 ≤ 250, LOD2 one coarse body), and the man's head pieces, arms and tools. Colours
    /// are cells of the palette by UV; the same shape in other colours is another mesh, built when first needed.
    /// </summary>
    public static class CrewMeshes
    {
        public enum Piece
        {
            Pelvis, Spine, Chest, Neck, Head, UpperArm, ForeArm, Hand, Thigh, Shin, Foot,
            // From afar (LOD2): the trunk, and each arm and leg in one along its upper bone.
            Trunk, ArmLong, LegLong,
            Hair, Beard, Helmet, Shield, Spear, Javelin, Bow, Quiver, Sword, Rope, Bailer, Pole, Stone, Bolt,
        }

        static readonly Dictionary<long, Mesh> cache = new Dictionary<long, Mesh>();

        /// <summary>Meshes built so far; tools and tests.</summary>
        public static int Built => cache.Count;

        /// <summary>A mesh by its piece, detail, shape variant, side (0 right, 1 left) and up to two colours.</summary>
        public static Mesh Get(Piece piece, int lod, int variant, int side, CrewPalette.Cell first, CrewPalette.Cell second)
        {
            Normalize(piece, variant, ref side, ref first, ref second);
            var key = (long)piece | (long)lod << 6 | (long)variant << 8 | (long)side << 14 | (long)first << 16 | (long)second << 24;
            if (cache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            mesh = Build(piece, lod, variant, side, first, second);
            mesh.name = $"Crew {piece} L{lod} v{variant}{(side == 1 ? " left" : "")} {first}/{second}";
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Only what changes a mesh goes into its key: only the hand is built mirrored; the pieces of one colour named here
        /// ignore their second colour, the hips their first, a shin without a greave its second; every other piece keeps
        /// both. Meshes that look the same are then one mesh, one bucket and one draw (CrewRenderer).
        /// </summary>
        static void Normalize(Piece piece, int variant, ref int side, ref CrewPalette.Cell first, ref CrewPalette.Cell second)
        {
            if (piece != Piece.Hand) side = 0;
            switch (piece)
            {
                case Piece.Pelvis:
                    first = second;
                    break;
                case Piece.Shin:
                    if (variant != 1) second = first;
                    break;
                case Piece.Spine:
                case Piece.Chest:
                case Piece.Neck:
                case Piece.Head:
                case Piece.ForeArm:
                case Piece.Hand:
                case Piece.Foot:
                case Piece.Hair:
                case Piece.Beard:
                case Piece.Bow:
                case Piece.Quiver:
                case Piece.Rope:
                case Piece.Bailer:
                case Piece.Pole:
                case Piece.Stone:
                case Piece.Bolt:
                case Piece.Trunk:
                case Piece.ArmLong:
                case Piece.LegLong:
                    second = first;
                    break;
            }
        }

        public static void Clear()
        {
            foreach (var mesh in cache.Values) if (mesh != null) Object.Destroy(mesh);
            cache.Clear();
        }

        // Builder ----------------------------------------------------------------------------------------------------
        sealed class Builder
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> UVs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            int Add(Vector3 p, Vector3 n, Vector2 uv)
            {
                Positions.Add(p);
                Normals.Add(n);
                UVs.Add(uv);
                return Positions.Count - 1;
            }

            /// <summary>
            /// A loft along an axis from a to b: elliptical rings of half width and half depth by t (0 at a, 1 at b), closed
            /// by rounded caps; colour by t. Rings face the given forward for their depth.
            /// </summary>
            public void Loft(Vector3 a, Vector3 b, System.Func<float, Vector2> size, int rings, int sides, System.Func<float, Vector2> colour, Vector3 forward, bool capStart = true, bool capEnd = true)
            {
                var axis = (b - a).normalized;
                var right = Vector3.Cross(axis, forward).normalized;
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                var depth = Vector3.Cross(right, axis).normalized;
                var start = Positions.Count;
                for (var r = 0; r <= rings; r++)
                {
                    var t = r / (float)rings;
                    var centre = Vector3.Lerp(a, b, t);
                    var half = size(t);
                    var uv = colour(t);
                    for (var s = 0; s < sides; s++)
                    {
                        var angle = (s + 0.5f) * Mathf.PI * 2f / sides;
                        var c = Mathf.Cos(angle);
                        var d = Mathf.Sin(angle);
                        var p = centre + right * (c * half.x) + depth * (d * half.y);
                        var n = (right * (c / Mathf.Max(0.001f, half.x)) + depth * (d / Mathf.Max(0.001f, half.y))).normalized;
                        Add(p, n, uv);
                    }
                }
                for (var r = 0; r < rings; r++)
                for (var s = 0; s < sides; s++)
                {
                    var i0 = start + r * sides + s;
                    var i1 = start + r * sides + (s + 1) % sides;
                    var i2 = start + (r + 1) * sides + (s + 1) % sides;
                    var i3 = start + (r + 1) * sides + s;
                    Quad(i0, i1, i2, i3);
                }
                if (capStart) Cap(start, sides, a - axis * size(0f).magnitude * 0.45f, -axis, colour(0f));
                if (capEnd) Cap(start + rings * sides, sides, b + axis * size(1f).magnitude * 0.45f, axis, colour(1f));
            }

            void Cap(int ring, int sides, Vector3 tip, Vector3 outward, Vector2 uv)
            {
                var centre = Add(tip, outward, uv);
                for (var s = 0; s < sides; s++)
                {
                    var a = ring + s;
                    var b = ring + (s + 1) % sides;
                    Triangle(centre, a, b, outward);
                }
            }

            void Quad(int a, int b, int c, int d)
            {
                var outward = (Normals[a] + Normals[c]) * 0.5f;
                Triangle(a, b, c, outward);
                Triangle(a, c, d, outward);
            }

            public void Triangle(int a, int b, int c, Vector3 outward)
            {
                var normal = Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]);
                if (Vector3.Dot(normal, outward) >= 0f) { Triangles.Add(a); Triangles.Add(b); Triangles.Add(c); }
                else { Triangles.Add(a); Triangles.Add(c); Triangles.Add(b); }
            }

            /// <summary>An ellipsoid of half sizes, with a deformation of its points (a face, a jaw), coloured by point.</summary>
            public void Blob(Vector3 centre, Vector3 half, int rings, int sides, System.Func<Vector3, Vector3> shape, System.Func<Vector3, Vector2> colour)
            {
                var start = Positions.Count;
                for (var r = 0; r <= rings; r++)
                {
                    var phi = Mathf.PI * r / rings;
                    for (var s = 0; s <= sides; s++)
                    {
                        var theta = Mathf.PI * 2f * s / sides;
                        var unit = new Vector3(Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Cos(theta));
                        var p = shape(Vector3.Scale(unit, half));
                        Add(centre + p, new Vector3(unit.x / half.x, unit.y / half.y, unit.z / half.z).normalized, colour(unit));
                    }
                }
                for (var r = 0; r < rings; r++)
                for (var s = 0; s < sides; s++)
                {
                    var i0 = start + r * (sides + 1) + s;
                    var i1 = i0 + 1;
                    var i2 = i0 + sides + 2;
                    var i3 = i0 + sides + 1;
                    var outward = Positions[i0] - centre + (Positions[i2] - centre);
                    if (r > 0) Triangle(i0, i1, i2, outward);
                    if (r < rings - 1) Triangle(i0, i2, i3, outward);
                }
            }

            /// <summary>A box, for flat tools and plates; colour by face.</summary>
            public void Box(Vector3 centre, Vector3 half, Quaternion rotation, Vector2 uv)
            {
                for (var face = 0; face < 6; face++)
                {
                    var n = face switch { 0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.up, 3 => Vector3.down, 4 => Vector3.forward, _ => Vector3.back };
                    var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    var v = Vector3.Cross(n, u);
                    var corners = new int[4];
                    for (var k = 0; k < 4; k++)
                    {
                        var su = k == 0 || k == 3 ? -1f : 1f;
                        var sv = k < 2 ? -1f : 1f;
                        var local = Vector3.Scale(n + u * su + v * sv, half);
                        corners[k] = Add(centre + rotation * local, rotation * n, uv);
                    }
                    var outward = rotation * n;
                    Triangle(corners[0], corners[1], corners[2], outward);
                    Triangle(corners[0], corners[2], corners[3], outward);
                }
            }

            public Mesh Build()
            {
                var mesh = new Mesh();
                mesh.SetVertices(Positions);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, UVs);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        static Vector2 C(CrewPalette.Cell cell) => CrewPalette.UV(cell);

        // Pieces -----------------------------------------------------------------------------------------------------
        /// <summary>Rings and sides of a limb's loft at each level of detail.</summary>
        static (int rings, int sides) Detail(int lod) => lod == 0 ? (4, 7) : lod == 1 ? (2, 4) : (1, 3);

        static Mesh Build(Piece piece, int lod, int variant, int side, CrewPalette.Cell first, CrewPalette.Cell second)
        {
            var b = new Builder();
            var (rings, sides) = Detail(lod);
            var mirror = side == 1 ? -1f : 1f;
            Vector2 One(float _) => C(first);
            switch (piece)
            {
                case Piece.Pelvis:
                    // Hips and seat; a kilt's waist in the second colour (first: skin under a kilt, cloth of a tunic's skirt).
                    b.Loft(new Vector3(0f, 0.13f, 0f), new Vector3(0f, -0.11f, 0f), t => new Vector2(Mathf.Lerp(0.14f, 0.165f, t), Mathf.Lerp(0.1f, 0.115f, t)), rings, sides + 1, _ => C(second), Vector3.forward);
                    break;
                case Piece.Spine:
                    b.Loft(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.19f, 0.005f), t => new Vector2(Mathf.Lerp(0.14f, 0.135f, t), Mathf.Lerp(0.1f, 0.095f, t)), rings, sides + 1, One, Vector3.forward);
                    break;
                case Piece.Chest:
                {
                    // Ribcage widening to the shoulders; variant 2 is a bronze corslet's bell.
                    var bell = variant == 2 ? 0.015f : 0f;
                    b.Loft(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.27f, -0.01f), t => new Vector2(Mathf.Lerp(0.14f, 0.19f, Mathf.Sin(t * 1.4f)) + bell, Mathf.Lerp(0.1f, 0.115f, Mathf.Sin(t * 2.2f)) + bell), rings + 1, sides + 1, One, Vector3.forward);
                    break;
                }
                case Piece.Neck:
                    b.Loft(new Vector3(0f, -0.03f, 0f), new Vector3(0f, 0.1f, 0.01f), _ => new Vector2(0.055f, 0.055f), 1, sides, One, Vector3.forward);
                    break;
                case Piece.Head:
                {
                    // A skull with jaw and nose by face variant; lips and brows are not seen at the camera's distance.
                    var jaw = variant == 0 ? 0.92f : variant == 1 ? 1.0f : 0.86f;
                    var nose = variant == 2 ? 0.026f : 0.018f;
                    b.Blob(new Vector3(0f, 0.105f, 0.012f), new Vector3(0.078f, 0.112f, 0.098f), lod == 0 ? 7 : 4, lod == 0 ? 10 : 6, p =>
                    {
                        if (p.y < -0.02f) p.x *= Mathf.Lerp(1f, jaw, Mathf.InverseLerp(-0.02f, -0.11f, p.y));
                        if (p.z > 0.06f && Mathf.Abs(p.x) < 0.03f && p.y > -0.05f && p.y < 0.02f) p.z += nose;
                        return p;
                    }, _ => C(first));
                    break;
                }
                case Piece.UpperArm:
                    b.Loft(new Vector3(0f, 0.035f, 0f), new Vector3(0f, -0.31f, 0f), t => new Vector2(Mathf.Lerp(0.052f, 0.04f, t), Mathf.Lerp(0.05f, 0.042f, t)), rings, sides, t => t < 0.33f ? C(second) : C(first), Vector3.forward);
                    break;
                case Piece.ForeArm:
                    b.Loft(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.245f, 0.01f), t => new Vector2(Mathf.Lerp(0.043f, 0.029f, t), Mathf.Lerp(0.037f, 0.022f, t)), rings, sides, One, Vector3.forward);
                    break;
                case Piece.Hand:
                    b.Loft(new Vector3(0f, 0.01f, 0.005f), new Vector3(0f, -0.17f, 0.03f), t => new Vector2(Mathf.Lerp(0.032f, 0.04f, Mathf.Sin(t * 2.5f)), 0.017f), lod == 0 ? 2 : 1, lod == 0 ? 6 : 4, One, Vector3.right * mirror);
                    break;
                case Piece.Thigh:
                    // A kilt's or tunic's hem over the top of the thigh moves with it when he sits.
                    b.Loft(new Vector3(0f, 0.05f, 0f), new Vector3(0f, -0.415f, 0.005f), t => new Vector2(Mathf.Lerp(0.082f, 0.05f, t), Mathf.Lerp(0.085f, 0.052f, t)), rings, sides, t => t < 0.3f ? C(second) : C(first), Vector3.forward);
                    break;
                case Piece.Shin:
                    // Variant 1: a bronze greave over the front of the shin.
                    b.Loft(new Vector3(0f, 0.03f, 0f), new Vector3(0f, -0.415f, 0f), t => new Vector2(Mathf.Lerp(0.05f, 0.03f, t) + 0.01f * Mathf.Sin(t * 3.4f), Mathf.Lerp(0.052f, 0.032f, t) + 0.012f * Mathf.Sin(t * 3.4f)), rings, sides,
                        t => variant == 1 && t > 0.08f && t < 0.85f ? C(second) : C(first), Vector3.forward);
                    break;
                case Piece.Foot:
                    b.Loft(new Vector3(0f, -0.035f, -0.055f), new Vector3(0f, -0.045f, 0.19f), t => new Vector2(Mathf.Lerp(0.035f, 0.045f, t), Mathf.Lerp(0.035f, 0.02f, t)), lod == 0 ? 2 : 1, lod == 0 ? 6 : 4, One, Vector3.up);
                    break;
                case Piece.Hair:
                {
                    // 0 cropped, 1 long to the shoulders, 2 tied back in a knot; a cap over the skull.
                    var back = variant == 1 ? 0.075f : variant == 2 ? 0.03f : 0f;
                    b.Blob(new Vector3(0f, 0.125f, 0.0f), new Vector3(0.085f, 0.105f, 0.104f), lod == 0 ? 6 : 3, lod == 0 ? 10 : 6, p =>
                    {
                        if (p.z > 0.04f && p.y < 0.06f) p.z -= 0.035f; // no hair over the face
                        if (p.z < 0f && p.y < 0.0f) p.y -= back;
                        return p;
                    }, _ => C(first));
                    if (variant == 2) b.Blob(new Vector3(0f, 0.15f, -0.1f), new Vector3(0.032f, 0.03f, 0.03f), 3, 5, p => p, _ => C(first));
                    break;
                }
                case Piece.Beard:
                {
                    // 1 short, 2 pointed (the Warrior Vase).
                    var length = variant == 2 ? 0.075f : 0.035f;
                    b.Blob(new Vector3(0f, 0.035f - length * 0.4f, 0.07f), new Vector3(0.055f, 0.03f + length * 0.5f, 0.035f), lod == 0 ? 4 : 3, lod == 0 ? 7 : 5,
                        p => { if (p.y < 0f) p.x *= Mathf.Lerp(1f, variant == 2 ? 0.3f : 0.8f, -p.y / (0.03f + length * 0.5f)); return p; }, _ => C(first));
                    break;
                }
                case Piece.Helmet:
                    BuildHelmet(b, lod, variant, first, second);
                    break;
                case Piece.Shield:
                    BuildShield(b, lod, variant, first, second);
                    break;
                case Piece.Spear:
                    b.Loft(new Vector3(0f, -1.0f, 0f), new Vector3(0f, 1.35f, 0f), _ => new Vector2(0.016f, 0.016f), 1, lod == 0 ? 5 : 3, One, Vector3.forward, false, false);
                    b.Loft(new Vector3(0f, 1.35f, 0f), new Vector3(0f, 1.62f, 0f), t => new Vector2(Mathf.Lerp(0.03f, 0.004f, t), 0.008f), 1, 3, _ => C(second), Vector3.forward, false, true);
                    break;
                case Piece.Javelin:
                    b.Loft(new Vector3(0f, -0.6f, 0f), new Vector3(0f, 0.85f, 0f), _ => new Vector2(0.012f, 0.012f), 1, 3, One, Vector3.forward, false, false);
                    b.Loft(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 1.0f, 0f), t => new Vector2(Mathf.Lerp(0.02f, 0.003f, t), 0.006f), 1, 3, _ => C(second), Vector3.forward, false, true);
                    break;
                case Piece.Bow:
                {
                    // A recurved bow held at its grip: limbs above and below the hand, strung (the string is too thin to draw).
                    var points = new List<Vector3>();
                    for (var i = -4; i <= 4; i++) points.Add(new Vector3(0f, i * 0.15f, -Mathf.Abs(i) * Mathf.Abs(i) * 0.006f + (Mathf.Abs(i) == 4 ? 0.035f : 0f)));
                    for (var i = 0; i + 1 < points.Count; i++)
                        b.Loft(points[i], points[i + 1], _ => new Vector2(0.014f, 0.01f), 1, 3, One, Vector3.right, i == 0, i + 2 == points.Count);
                    break;
                }
                case Piece.Quiver:
                    b.Loft(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 0.32f, 0f), _ => new Vector2(0.05f, 0.045f), 1, lod == 0 ? 6 : 4, One, Vector3.forward);
                    break;
                case Piece.Sword:
                    // A Naue II sword: the blade toward +Y from the fist, the hilt in it.
                    b.Loft(new Vector3(0f, 0.07f, 0f), new Vector3(0f, 0.69f, 0f), t => new Vector2(Mathf.Lerp(0.024f, 0.006f, t * t), 0.005f), 2, 4, One, Vector3.forward);
                    b.Box(new Vector3(0f, 0.0f, 0f), new Vector3(0.015f, 0.07f, 0.015f), Quaternion.identity, C(second));
                    break;
                case Piece.Rope:
                    b.Blob(Vector3.zero, new Vector3(0.11f, 0.035f, 0.11f), 3, 8, p => p, _ => C(first));
                    break;
                case Piece.Trunk:
                    // From afar: belly and chest as one, on the spine, up to the shoulders (the chest's joint is 0.155 above).
                    b.Loft(new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.43f, 0f), t => new Vector2(Mathf.Lerp(0.14f, 0.19f, Mathf.Sin(t * 1.4f)), Mathf.Lerp(0.1f, 0.112f, t)), rings + 1, sides + 1, One, Vector3.forward);
                    break;
                case Piece.ArmLong:
                    // From afar: the arm to the wrist in one, along the upper arm (0.309 and 0.242 m).
                    b.Loft(new Vector3(0f, 0.035f, 0f), new Vector3(0f, -0.555f, 0.01f), t => new Vector2(Mathf.Lerp(0.05f, 0.03f, t), Mathf.Lerp(0.048f, 0.025f, t)), rings, sides, One, Vector3.forward);
                    break;
                case Piece.LegLong:
                    // From afar: the leg to the ankle in one, along the thigh (0.407 and 0.408 m).
                    b.Loft(new Vector3(0f, 0.05f, 0f), new Vector3(0f, -0.82f, 0.005f), t => new Vector2(Mathf.Lerp(0.08f, 0.035f, t), Mathf.Lerp(0.083f, 0.037f, t)), rings, sides, One, Vector3.forward);
                    break;
                case Piece.Bailer:
                    b.Blob(new Vector3(0f, 0f, 0.06f), new Vector3(0.07f, 0.045f, 0.09f), 3, 6, p => p, _ => C(first));
                    b.Loft(Vector3.zero, new Vector3(0f, 0f, -0.18f), _ => new Vector2(0.012f, 0.012f), 1, 3, One, Vector3.up);
                    break;
                case Piece.Pole:
                    b.Loft(new Vector3(0f, -1.3f, 0f), new Vector3(0f, 1.3f, 0f), _ => new Vector2(0.02f, 0.02f), 1, 3, One, Vector3.forward);
                    break;
                case Piece.Stone:
                    b.Blob(Vector3.zero, new Vector3(0.12f, 0.1f, 0.11f), 3, 6, p => p, _ => C(first));
                    break;
                case Piece.Bolt:
                    b.Loft(new Vector3(0f, 0f, -0.45f), new Vector3(0f, 0f, 0.45f), _ => new Vector2(0.018f, 0.018f), 1, 3, One, Vector3.up);
                    break;
            }
            return b.Build();
        }

        /// <summary>
        /// Helmets of LH IIIC: 1 the "hedgehog" (a cap studded all over, Kynos and the Warrior Vase's back), 2 horned with a
        /// crest (the Warrior Vase's front), 3 a "feathered" crown (Bademgediği, Medinet Habu), 4 a plain leather cap.
        /// </summary>
        static void BuildHelmet(Builder b, int lod, int variant, CrewPalette.Cell first, CrewPalette.Cell second)
        {
            var rings = lod == 0 ? 5 : 3;
            var sides = lod == 0 ? 10 : 6;
            b.Blob(new Vector3(0f, 0.14f, 0.0f), new Vector3(0.092f, 0.088f, 0.108f), rings, sides, p => { if (p.y < -0.02f) p.y = -0.02f; return p; }, _ => C(first));
            switch (variant)
            {
                case 1:
                    if (lod > 0) break;
                    for (var k = 0; k < 14; k++)
                    {
                        var a = k * 2.399963f;
                        var y = 0.15f + 0.07f * Mathf.Cos(k * 0.7f);
                        b.Box(new Vector3(Mathf.Sin(a) * 0.085f, y, Mathf.Cos(a) * 0.1f), new Vector3(0.008f, 0.012f, 0.008f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), C(second));
                    }
                    break;
                case 2:
                    foreach (var s in new[] { -1f, 1f })
                        b.Loft(new Vector3(s * 0.07f, 0.19f, 0.01f), new Vector3(s * 0.15f, 0.29f, 0.04f), t => new Vector2(Mathf.Lerp(0.018f, 0.004f, t), Mathf.Lerp(0.018f, 0.004f, t)), 2, 4, _ => C(second), Vector3.forward);
                    b.Box(new Vector3(0f, 0.26f, -0.02f), new Vector3(0.012f, 0.035f, 0.08f), Quaternion.identity, C(second));
                    break;
                case 3:
                    for (var k = -4; k <= 4; k++)
                        b.Box(new Vector3(k * 0.022f, 0.25f, -0.01f), new Vector3(0.008f, 0.05f, 0.02f), Quaternion.Euler(0f, 0f, -k * 6f), C(second));
                    break;
            }
        }

        /// <summary>Shields, faced toward +Z in the forearm's hold: 1 round with a cut-out at its foot, 2 round, 3 hourglass.</summary>
        static void BuildShield(Builder b, int lod, int variant, CrewPalette.Cell first, CrewPalette.Cell second)
        {
            var sides = lod == 0 ? 14 : 8;
            var radius = variant == 3 ? 0.28f : 0.36f;
            var centre = new Vector3(0f, -0.05f, 0.09f);
            var rim = new List<Vector3>();
            for (var s = 0; s < sides; s++)
            {
                var a = s * Mathf.PI * 2f / sides;
                var x = Mathf.Sin(a) * radius;
                var y = Mathf.Cos(a) * radius * (variant == 3 ? 1.9f : 1f);
                if (variant == 3) x *= 0.6f + 0.4f * Mathf.Abs(Mathf.Cos(a)); // waisted
                if (variant == 1 && Mathf.Cos(a) < -0.85f) y *= 0.7f;         // the cut-out
                rim.Add(centre + new Vector3(x, y, 0f));
            }
            // A shallow dome of hide over wicker or wood, painted on the face, with a bronze boss on round ones.
            var hub = b.Positions.Count;
            b.Positions.Add(centre + new Vector3(0f, 0f, 0.05f));
            b.Normals.Add(Vector3.forward);
            b.UVs.Add(C(second));
            for (var s = 0; s < sides; s++)
            {
                b.Positions.Add(rim[s]);
                b.Normals.Add((Vector3.forward * 2f + (rim[s] - centre).normalized).normalized);
                b.UVs.Add(C(second));
            }
            for (var s = 0; s < sides; s++) b.Triangle(hub, hub + 1 + s, hub + 1 + (s + 1) % sides, Vector3.forward);
            var back = b.Positions.Count;
            b.Positions.Add(centre + new Vector3(0f, 0f, -0.01f));
            b.Normals.Add(Vector3.back);
            b.UVs.Add(C(first));
            for (var s = 0; s < sides; s++)
            {
                b.Positions.Add(rim[s] + new Vector3(0f, 0f, -0.015f));
                b.Normals.Add(Vector3.back);
                b.UVs.Add(C(first));
            }
            for (var s = 0; s < sides; s++) b.Triangle(back, back + 1 + s, back + 1 + (s + 1) % sides, Vector3.back);
            if (variant != 3 && lod == 0) b.Blob(centre + new Vector3(0f, 0f, 0.055f), new Vector3(0.05f, 0.05f, 0.025f), 3, 6, p => p, _ => C(CrewPalette.Cell.Bronze));
        }
    }
}
