using System.IO;
using MorzaBogow.Presentation;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using Map = MorzaBogow.Editor.ShipTextureBaker.Map;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// The port's masonry, baked in code (deterministic, nothing downloaded): regular limestone ashlar in level courses
    /// (Messene's walls, R-MESSENE), each block with a smooth margin round a pecked face (a dressing of the period, here
    /// an interpretation), and lime plaster for the paints to tint. Colour, tangent-space normal and HDRP mask maps; HDRP/Lit
    /// materials with the keywords the boulders and the olive bark already use, in Resources so every build carries them.
    /// Textures are in metres: the buildings' masonry UVs (MeshBuilder.Level) run level on upright faces.
    /// -executeMethod MorzaBogow.Editor.BuildingTextureBaker.RunBatch
    /// </summary>
    public static class BuildingTextureBaker
    {
        public const string Folder = "Assets/MorzaBogow/Art/Buildings/Resources/Buildings";
        public const int Size = 1024;

        /// <summary>The ashlar covers 2.4 m by 1.6 m: four courses of 0.4 m, blocks 1.2 m long, every other course half a block over.</summary>
        public const float AshlarWidth = 2.4f, AshlarHeight = 1.6f;

        /// <summary>The plaster covers 2 m by 2 m.</summary>
        public const float PlasterSpan = 2f;

        public static readonly Map[] Maps = { Map.Colour, Map.Normal, Map.Mask };

        public static string PathOf(string id, Map map) => $"{Folder}/{id}{(map == Map.Normal ? "_n" : map == Map.Mask ? "_mask" : "")}.png";

        [MenuItem("Morza Bogów/Budowle/Wypal tekstury murów")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            foreach (var id in new[] { "ashlar", "plaster" })
            {
                var surface = id == "ashlar" ? Ashlar(Size) : Plaster(Size);
                var (texelU, texelV) = id == "ashlar" ? (AshlarWidth / Size, AshlarHeight / Size) : (PlasterSpan / Size, PlasterSpan / Size);
                File.WriteAllBytes(PathOf(id, Map.Colour), Encode(surface.Colour, Size, false));
                File.WriteAllBytes(PathOf(id, Map.Normal), Encode(Normals(surface.Height, Size, texelU, texelV), Size, false));
                File.WriteAllBytes(PathOf(id, Map.Mask), Encode(Mask(surface), Size, true));
            }
            AssetDatabase.Refresh();
            foreach (var id in new[] { "ashlar", "plaster" })
            foreach (var map in Maps)
                ShipTextureBaker.Configure(PathOf(id, map), id, Size, map);
            MakeMaterial("Ashlar", "ashlar", new Vector2(1f / AshlarWidth, 1f / AshlarHeight), Color.white);
            MakeMaterial("Stucco", "plaster", new Vector2(1f / PlasterSpan, 1f / PlasterSpan), Color.white);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Buildings] ashlar and plaster textures, Ashlar.mat and Stucco.mat in {Folder}");
        }

        public static void RunBatch()
        {
            var code = 0;
            try { Run(); }
            catch (System.Exception error)
            {
                Debug.LogError($"[Buildings] {error}");
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void MakeMaterial(string name, string id, Vector2 tiling, Color tint)
        {
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PathOf(id, Map.Colour)));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PathOf(id, Map.Normal)));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PathOf(id, Map.Mask)));
            material.SetTextureScale("_BaseColorMap", tiling);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_NormalScale", 1f);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
        }

        public sealed class Surface
        {
            public readonly Color32[] Colour;
            public readonly float[] Height, Occlusion, Gloss;

            public Surface(int size)
            {
                Colour = new Color32[size * size];
                Height = new float[size * size];
                Occlusion = new float[size * size];
                Gloss = new float[size * size];
            }
        }

        /// <summary>
        /// Limestone ashlar: joints 8 mm wide and 6 mm deep with a bevel, a drafted margin 4 cm wide tooled smooth, the face
        /// within it pecked and standing a little proud; each block its own tone, the stone mottled at two scales.
        /// Heights in units of 6 mm.
        /// </summary>
        public static Surface Ashlar(int size)
        {
            var s = new Surface(size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                var u = (x + 0.5f) / size;
                var v = (y + 0.5f) / size;
                var course = Mathf.FloorToInt(v * 4f);
                var fv = v * 4f - course;
                var shifted = u + (course % 2) * 0.25f;
                shifted -= Mathf.Floor(shifted);
                var block = Mathf.FloorToInt(shifted * 2f);
                var fu = shifted * 2f - block;
                var dx = Mathf.Min(fu, 1f - fu) * AshlarWidth * 0.5f;
                var dy = Mathf.Min(fv, 1f - fv) * AshlarHeight * 0.25f;
                var d = Mathf.Min(dx, dy);
                var id = course * 2 + block;
                var tone = 0.9f + 0.16f * Hash(id, 7, 101);
                var warm = Hash(id, 3, 211) - 0.5f;
                var fine = Fbm(u, v, 12, 8, 4, 17);
                var peck = Fbm(u, v, 96, 64, 2, 29);
                float height, occlusion, gloss, shade;
                if (d < 0.004f) { height = -1f; occlusion = 0.45f; gloss = 0.08f; shade = 0.5f; }
                else if (d < 0.012f)
                {
                    var t = (d - 0.004f) / 0.008f;
                    height = -1f + t;
                    occlusion = 0.45f + 0.5f * t;
                    gloss = 0.15f;
                    shade = 0.72f + 0.25f * t;
                }
                else if (d < 0.04f) { height = 0.04f * (fine - 0.5f); occlusion = 1f; gloss = 0.3f; shade = 1f; }
                else
                {
                    height = 0.25f + 0.35f * (peck - 0.5f) + 0.15f * (fine - 0.5f);
                    occlusion = 0.86f + 0.12f * peck;
                    gloss = 0.16f;
                    shade = 0.93f + 0.1f * (peck - 0.5f);
                }
                // A few pits weathered out of the face.
                var cellX = x / 6;
                var cellY = y / 6;
                if (d > 0.04f && Hash(cellX, cellY, 313) > 0.985f)
                {
                    var cx = cellX * 6 + 3 - x;
                    var cy = cellY * 6 + 3 - y;
                    if (cx * cx + cy * cy < 6) { height -= 0.35f; shade *= 0.72f; occlusion *= 0.8f; }
                }
                var limestone = new Color(0.80f + 0.03f * warm, 0.76f + 0.01f * warm, 0.67f - 0.03f * warm);
                s.Colour[i] = Pixel(limestone * (tone * shade * (0.92f + 0.16f * fine)));
                s.Height[i] = height * 0.006f;
                s.Occlusion[i] = occlusion;
                s.Gloss[i] = gloss;
            }
            return s;
        }

        /// <summary>
        /// Lime plaster: near white, faintly clouded, with the long strokes of the trowel and a few grits; the paints tint
        /// it (whitewash, Egyptian blue, red ochre). Heights in units of 2 mm.
        /// </summary>
        public static Surface Plaster(int size)
        {
            var s = new Surface(size);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                var u = (x + 0.5f) / size;
                var v = (y + 0.5f) / size;
                var cloud = Fbm(u, v, 3, 3, 4, 41);
                var trowel = Fbm(u, v, 24, 5, 3, 43);
                var grit = Hash(x, y, 47) > 0.996f ? 1f : 0f;
                var shade = 0.95f + 0.09f * (cloud - 0.5f) + 0.05f * (trowel - 0.5f) - 0.12f * grit;
                s.Colour[i] = Pixel(new Color(0.95f, 0.94f, 0.91f) * shade);
                s.Height[i] = (0.6f * (cloud - 0.5f) + 0.3f * (trowel - 0.5f) + 0.4f * grit) * 0.002f;
                s.Occlusion[i] = 0.95f + 0.05f * cloud;
                s.Gloss[i] = 0.32f + 0.08f * (trowel - 0.5f);
            }
            return s;
        }

        /// <summary>Tangent-space normals from heights in metres (central differences, wrapping), texels texelU by texelV metres; +Y is +V.</summary>
        public static Color32[] Normals(float[] height, int size, float texelU, float texelV)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var down = (y - 1 + size) % size;
                var up = (y + 1) % size;
                for (var x = 0; x < size; x++)
                {
                    var left = (x - 1 + size) % size;
                    var right = (x + 1) % size;
                    var slopeU = (height[y * size + right] - height[y * size + left]) / (2f * texelU);
                    var slopeV = (height[up * size + x] - height[down * size + x]) / (2f * texelV);
                    var normal = new Vector3(-slopeU, -slopeV, 1f).normalized;
                    pixels[y * size + x] = new Color32(Byte(normal.x * 0.5f + 0.5f), Byte(normal.y * 0.5f + 0.5f), Byte(normal.z * 0.5f + 0.5f), 255);
                }
            }
            return pixels;
        }

        /// <summary>HDRP Lit packing: R metallic (none: stone and plaster), G occlusion, B detail mask (none), A smoothness.</summary>
        public static Color32[] Mask(Surface s)
        {
            var pixels = new Color32[s.Occlusion.Length];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, Byte(s.Occlusion[i]), 0, Byte(s.Gloss[i]));
            return pixels;
        }

        static byte[] Encode(Color32[] pixels, int size, bool alpha)
        {
            var texture = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static Color32 Pixel(Color c) => new Color32(Byte(c.r), Byte(c.g), Byte(c.b), 255);

        static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);

        /// <summary>An integer hash to 0..1.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        /// <summary>Value noise that repeats across the texture: cellsU by cellsV cells over 0..1, smoothly interpolated.</summary>
        static float Value(float u, float v, int cellsU, int cellsV, int seed)
        {
            var x = u * cellsU;
            var y = v * cellsV;
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = x - x0;
            var ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float At(int i, int j) => Hash(((i % cellsU) + cellsU) % cellsU, ((j % cellsV) + cellsV) % cellsV, seed);
            return Mathf.Lerp(Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx), Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx), ty);
        }

        /// <summary>Octaves of repeating value noise, each twice as fine and half as strong, to 0..1.</summary>
        static float Fbm(float u, float v, int cellsU, int cellsV, int octaves, int seed)
        {
            var sum = 0f;
            var weight = 0f;
            var amplitude = 1f;
            for (var o = 0; o < octaves; o++)
            {
                sum += amplitude * Value(u, v, cellsU << o, cellsV << o, seed + o * 31);
                weight += amplitude;
                amplitude *= 0.5f;
            }
            return sum / weight;
        }
    }
}
