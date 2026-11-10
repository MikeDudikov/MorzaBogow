using System.IO;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Bakes the shared textures of the ships' looks from code, the same bytes every time (no downloads, no Random): four
    /// wood grains, four sail cloths, two of worn pitch, rope and wicker. They are base colour only and near white, so
    /// each material's tone gives the hue. Planks run along U as the generator's UVs follow the planks, a sail's cloths run
    /// down V, and every texture tiles without a seam. Run from the menu or with
    /// -executeMethod MorzaBogow.Editor.ShipTextureBaker.Run; the runtime loads them from Resources/ShipLook.
    /// </summary>
    public static class ShipTextureBaker
    {
        public const string Folder = "Assets/MorzaBogow/Art/Ships/Resources/ShipLook";

        public static readonly (string name, int size)[] Textures =
        {
            ("grain_fine", 512), ("grain_straight", 512), ("grain_knotty", 512), ("grain_weathered", 512),
            ("canvas_narrow", 512), ("canvas_wide", 512), ("canvas_band", 512), ("canvas_grimy", 512),
            ("worn_a", 256), ("worn_b", 256), ("rope", 256), ("wicker", 256),
        };

        [MenuItem("Morza Bogów/Okręty/Wypal tekstury wariantów")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            foreach (var (name, size) in Textures) File.WriteAllBytes($"{Folder}/{name}.png", Encode(name, size));
            AssetDatabase.Refresh();
            foreach (var (name, size) in Textures) Configure($"{Folder}/{name}.png", name, size);
            Debug.Log($"[ShipTextureBaker] {Textures.Length} textures in {Folder}");
        }

        /// <summary>Colour textures with mipmaps and anisotropic filtering, compressed at high quality; worn pitch is clamped (one patch, 0..1), the rest repeats.</summary>
        public static void Configure(string path, string name, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = name.StartsWith("worn") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        public static byte[] Encode(string name, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(Pixels(name, size));
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static Color32[] Pixels(string name, int size) => name switch
        {
            "grain_fine" => Grain(size, 11, 0.24f, 0.10f, 0, 0.02f, 0.04f),
            "grain_straight" => Grain(size, 23, 0.2f, 0.16f, 1, 0.03f, 0.06f),
            "grain_knotty" => Grain(size, 37, 0.18f, 0.2f, 6, 0.04f, 0.08f),
            "grain_weathered" => Grain(size, 53, 0.22f, 0.27f, 3, 0.05f, 0.15f),
            "canvas_narrow" => Canvas(size, 61, 8, false, 0.12f),
            "canvas_wide" => Canvas(size, 67, 5, false, 0.12f),
            "canvas_band" => Canvas(size, 71, 6, true, 0.14f),
            "canvas_grimy" => Canvas(size, 79, 6, false, 0.38f),
            "worn_a" => Worn(size, 83),
            "worn_b" => Worn(size, 89),
            "rope" => Rope(size),
            "wicker" => Wicker(size),
            _ => throw new System.ArgumentException($"No such ship texture: {name}"),
        };

        // Wood -------------------------------------------------------------------------------------------------------------
        /// <summary>
        /// Four metres of planking, planks along U: each plank its own shade, long wavy grain lines, butt joints where planks
        /// end, dark seams between them, knots, the shallow scallops of the adze across the grain and lighter worn edges.
        /// </summary>
        static Color32[] Grain(int size, int seed, float plankWidth, float contrast, int knots, float toolMarks, float wear)
        {
            const float tile = 4f;
            var planks = Mathf.Max(4, Mathf.RoundToInt(tile / plankWidth));
            var pixels = new Color32[size * size];
            var knotAt = new Vector2[knots];
            for (var k = 0; k < knots; k++) knotAt[k] = new Vector2(Hash01(seed, k, 1) * tile, (Mathf.Floor(Hash01(seed, k, 2) * planks) + 0.5f) * tile / planks);
            // Every pattern repeats a whole number of times across the tile, so the planking tiles without a seam.
            var scallops = Mathf.RoundToInt(tile / 0.06f);
            for (var y = 0; y < size; y++)
            {
                var vn = (y + 0.5f) / size;
                var v = vn * tile;
                var plank = Mathf.Min(planks - 1, (int)(vn * planks));
                var inPlank = vn * planks - plank;
                var shade = 0.86f + (Hash01(seed, plank, 3) - 0.5f) * 0.12f;
                // Each plank ends somewhere in the tile and the next one butts against it.
                var butt = Hash01(seed, plank, 4) * tile;
                for (var x = 0; x < size; x++)
                {
                    var un = (x + 0.5f) / size;
                    var u = un * tile;
                    // Wavy grain lines a few centimetres apart, bent by the plank's figure, and fine fibres along them.
                    var fibre = Noise(un * 3f, vn * 36f + plank * 3.1f, 3f, 36f, seed);
                    var lines = Mathf.Sin((vn * 64f + fibre * 4f + Noise(un * 8f, vn * 8f, 8f, 8f, seed + 1) * 2.5f) * Mathf.PI);
                    var streaks = Noise(un * 24f, vn * 160f, 24f, 160f, seed + 3) - 0.5f;
                    var value = shade + lines * contrast * 0.35f + (fibre - 0.5f) * contrast * 0.6f + streaks * contrast * 0.5f;
                    foreach (var knot in knotAt)
                    {
                        var d = new Vector2(Wrap(u - knot.x, tile) * 2.2f, Wrap(v - knot.y, tile) * 6f).magnitude;
                        if (d < 0.35f) value -= (0.35f - d) * 0.9f + Mathf.Sin(d * 60f) * 0.04f;
                    }
                    value += Mathf.Sin(un * scallops * Mathf.PI * 2f + Hash01(seed, plank, 5) * 6f) * toolMarks * (0.5f + 0.5f * Noise(un * 4f, vn * 16f, 4f, 16f, seed + 2));
                    var edge = Mathf.Min(inPlank, 1f - inPlank);
                    value += Mathf.Clamp01(1f - edge * 8f) * wear;
                    if (edge < 0.035f) value *= 0.55f;
                    if (Mathf.Abs(Wrap(u - butt, tile)) < 0.012f) value *= 0.6f;
                    pixels[y * size + x] = Grey(value);
                }
            }
            return pixels;
        }

        // Cloth ------------------------------------------------------------------------------------------------------------
        /// <summary>
        /// A sail's cloth across four metres (U) and its whole height (V, from the yard down): cloths sewn side by side, each
        /// a little different, their seams, a fine weave, an optional strip sewn on below the yard, and grime toward the foot.
        /// </summary>
        static Color32[] Canvas(int size, int seed, int cloths, bool band, float grime)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var cloth = Mathf.Min(cloths - 1, (int)(u * cloths));
                    var inCloth = u * cloths - cloth;
                    var value = 0.9f + (Hash01(seed, cloth, 1) - 0.5f) * 0.08f;
                    value += (Noise(u * 160f, v * 120f, 160f, 120f, seed) - 0.5f) * 0.05f;
                    value += (Noise(u * 12f, v * 6f, 12f, 6f, seed + 1) - 0.5f) * 0.04f;
                    if (Mathf.Min(inCloth, 1f - inCloth) < 0.02f * cloths / 6f) value *= 0.78f;
                    if (band && v < 0.07f) value = value * 0.94f + (v > 0.064f ? -0.12f : 0f);
                    var foot = Mathf.Clamp01((v - 0.72f) / 0.28f);
                    value -= foot * foot * grime * (0.6f + 0.8f * Noise(u * 8f, v * 5f, 8f, 5f, seed + 2));
                    var stain = Noise(u * 3f, v * 3f, 3f, 3f, seed + 3);
                    if (stain > 0.72f) value -= (stain - 0.72f) * grime * 1.2f;
                    pixels[y * size + x] = Grey(value);
                }
            }
            return pixels;
        }

        // Worn pitch -------------------------------------------------------------------------------------------------------
        /// <summary>
        /// One patch (U along it, V up): at its edge the colour of pitch over the wood tone (about 0.14), inside the wood
        /// showing through in ragged islands with its grain, as pitch wears off where hulls are beached and rubbed.
        /// </summary>
        static Color32[] Worn(int size, int seed)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 2f;
                    var ragged = Noise(u * 6f, v * 6f, 6f, 6f, seed) * 0.6f + Noise(u * 17f, v * 17f, 17f, 17f, seed + 1) * 0.4f;
                    var bare = Mathf.Clamp01((edge * 1.6f + ragged - 0.95f) * 4f);
                    var grain = 0.86f + Mathf.Sin((v * 14f + Noise(u * 2f, v * 5f, 2f, 5f, seed + 2) * 3f) * Mathf.PI) * 0.035f
                                + (Noise(u * 12f, v * 48f, 12f, 48f, seed + 3) - 0.5f) * 0.06f;
                    pixels[y * size + x] = Grey(Mathf.Lerp(0.14f, grain, bare));
                }
            }
            return pixels;
        }

        // Rope and wicker --------------------------------------------------------------------------------------------------
        /// <summary>A quarter metre of three-strand rope: U along it, V around it; strands laid at a slant, fibres along them.</summary>
        static Color32[] Rope(int size)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size;
                var v = (y + 0.5f) / size;
                var lay = Mathf.Repeat((u * 6f + v * 3f), 1f);
                var value = 0.72f + Mathf.Sin(lay * Mathf.PI) * 0.22f + (Noise(u * 64f, v * 16f, 64f, 16f, 97) - 0.5f) * 0.1f;
                pixels[y * size + x] = Grey(value);
            }
            return pixels;
        }

        /// <summary>A quarter metre of basketwork: weavers passing over and under the stakes, each shaded round.</summary>
        static Color32[] Wicker(int size)
        {
            const int cells = 8;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size * cells;
                var v = (y + 0.5f) / size * cells;
                int cu = (int)u, cv = (int)v;
                var over = (cu + cv) % 2 == 0;
                var across = over ? v - cv : u - cu;
                var value = 0.62f + Mathf.Sin(across * Mathf.PI) * 0.3f + (Noise(u * 6f, v * 6f, cells * 6f, cells * 6f, 101) - 0.5f) * 0.08f;
                pixels[y * size + x] = Grey(value);
            }
            return pixels;
        }

        // Deterministic noise --------------------------------------------------------------------------------------------
        static Color32 Grey(float value)
        {
            var b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
            return new Color32(b, b, b, 255);
        }

        static float Wrap(float d, float period) => d - Mathf.Round(d / period) * period;

        /// <summary>A number 0..1 from three integers: the same on every machine and run.</summary>
        static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                var h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Smooth value noise on a lattice that wraps with the given period, so the texture tiles.</summary>
        static float Noise(float x, float y, float periodX, float periodY, int seed)
        {
            int px = Mathf.Max(1, Mathf.RoundToInt(periodX)), py = Mathf.Max(1, Mathf.RoundToInt(periodY));
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float At(int i, int j) => Hash01(seed, ((i % px) + px) % px, (((j % py) + py) % py) * 7919);
            var a = Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), fx);
            var b = Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }
    }
}
