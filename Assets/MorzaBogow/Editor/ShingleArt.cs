using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Builds the pebble beach material of <see cref="Presentation.Landscape.Shingle"/> from the CC0 Poly Haven set
    /// dry_river_pebbles (Assets/Art/PolyHaven/ZRODLA_CC0.txt):
    /// <list type="bullet">
    /// <item>HDRP/Lit kept in a Resources folder as Environment/ShorePebbles, so a build keeps its shader variant;</item>
    /// <item>mapped triplanar in world space like the shore's other materials (the strip has no UVs), a tile
    /// <see cref="Tile"/> m wide;</item>
    /// <item>the river stones bleached toward the pale limestone and marble pebbles of an Aegean beach, in a copy
    /// (the CC0 source stays as it is);</item>
    /// <item>a mask map from the roughness and occlusion maps (R metallic, G occlusion, B detail, A smoothness).</item>
    /// </list>
    /// The wet strip below the swash is a darker, glossier copy made at run time (LandscapeArt.Wet).
    /// Batch: -executeMethod MorzaBogow.Editor.ShingleArt.Build -quit
    /// </summary>
    public static class ShingleArt
    {
        const string Source = "Assets/Art/PolyHaven/Textures";
        const string Output = "Assets/Art/Environment/Coast";
        const string Materials = "Assets/Art/Environment/Resources/Environment";
        const string Id = "dry_river_pebbles";

        /// <summary>
        /// Width of one tile of the texture on the beach. A tile shows a dozen or so large stones across, so the
        /// largest come to about 0.3 m and most to about 0.1 m: judged from the picture, not measured.
        /// </summary>
        public const float Tile = 2f;

        /// <summary>The texture size the material samples: a 2 m tile at 1K is 512 texels a metre, far above any view of the game.</summary>
        const int Size = 1024;

        [MenuItem("Morza Bogów/Środowisko/Zbuduj materiał plaży żwirowej")]
        public static void Build()
        {
            var diffuse = $"{Source}/{Id}_diff_2k.jpg";
            var normal = $"{Source}/{Id}_nor_gl_2k.jpg";
            var rough = $"{Source}/{Id}_rough_2k.jpg";
            var occlusion = $"{Source}/{Id}_ao_2k.jpg";
            if (!File.Exists(diffuse) || !File.Exists(normal) || !File.Exists(rough) || !File.Exists(occlusion))
            {
                Debug.LogError($"[Shingle] CC0 source {Id} missing in {Source}: no material built.");
                return;
            }
            Folder("Assets/Art", "Environment");
            Folder("Assets/Art/Environment", "Coast");
            Folder("Assets/Art/Environment", "Resources");
            Folder("Assets/Art/Environment/Resources", "Environment");

            // The sources are data for this builder; only the normal map is sampled as it is.
            Import(diffuse, TextureImporterType.Default, true, Size);
            Import(rough, TextureImporterType.Default, false, Size);
            Import(occlusion, TextureImporterType.Default, false, Size);
            Import(normal, TextureImporterType.NormalMap, false, Size);
            var colour = Bleached(diffuse, $"{Output}/{Id}_bleached_diff.jpg");
            var mask = Mask(rough, occlusion, $"{Output}/{Id}_mask.png");

            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/ShorePebbles.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, $"{Materials}/ShorePebbles.mat");
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colour));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mask));
            material.SetColor("_BaseColor", new Color(0.95f, 0.94f, 0.92f));
            material.SetFloat("_NormalScale", 1.2f);
            // Dry stones: matt, with only the smoothest faces catching the sun.
            material.SetFloat("_SmoothnessRemapMin", 0f);
            material.SetFloat("_SmoothnessRemapMax", 0.55f);
            // World-space triplanar mapping, as the shore's other materials: no UVs, no stretching on the slope.
            material.SetFloat("_UVBase", 5f);
            material.SetFloat("_TexWorldScale", 1f / Tile);
            material.SetFloat("_ObjectSpaceUVMapping", 0f);
            material.enableInstancing = true;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Shingle] built {AssetDatabase.GetAssetPath(material)}: colour {colour}, mask {mask}, tile {Tile} m");
        }

        /// <summary>
        /// A copy of the colour map at <see cref="Size"/>, each texel taken halfway to its own grey and lightened a
        /// little, keeping a trace of warmth: sun-bleached beach stones rather than river stones. Dark schist pebbles
        /// stay dark grey, as on the beaches of the Cyclades.
        /// </summary>
        static string Bleached(string sourcePath, string path)
        {
            var pixels = Halved(Pixels(sourcePath, out var width, out var height), ref width, ref height);
            for (var i = 0; i < pixels.Length; i++)
            {
                Color texel = pixels[i];
                var light = texel.r * 0.3f + texel.g * 0.59f + texel.b * 0.11f;
                var grey = new Color(light * 1.02f, light, light * 0.95f);
                var bleached = Color.Lerp(texel, grey, 0.55f) * 1.12f;
                pixels[i] = new Color(Mathf.Clamp01(bleached.r), Mathf.Clamp01(bleached.g), Mathf.Clamp01(bleached.b), 1f);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToJPG(92));
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Import(path, TextureImporterType.Default, true, Size);
            return path;
        }

        /// <summary>The HDRP mask at <see cref="Size"/>: occlusion in green, smoothness (one minus roughness) in alpha.</summary>
        static string Mask(string roughPath, string occlusionPath, string path)
        {
            var rough = Halved(Pixels(roughPath, out var width, out var height), ref width, ref height);
            var occlusion = Halved(Pixels(occlusionPath, out var occlusionWidth, out var occlusionHeight), ref occlusionWidth, ref occlusionHeight);
            var matching = occlusionWidth == width && occlusionHeight == height;
            var mask = new Color32[rough.Length];
            for (var i = 0; i < mask.Length; i++)
            {
                var ao = matching ? occlusion[i].r : (byte)255;
                mask[i] = new Color32(0, ao, 0, (byte)(255 - rough[i].r));
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(mask);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            Import(path, TextureImporterType.Default, false, Size);
            return path;
        }

        /// <summary>The bytes of an image file as they are stored (no import settings involved, the asset left untouched).</summary>
        static Color32[] Pixels(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.LoadImage(File.ReadAllBytes(path), false);
            width = texture.width;
            height = texture.height;
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        /// <summary>Box-averaged down to <see cref="Size"/> (halving while larger and even).</summary>
        static Color32[] Halved(Color32[] pixels, ref int width, ref int height)
        {
            while (width > Size && width % 2 == 0 && height % 2 == 0)
            {
                var half = new Color32[width / 2 * (height / 2)];
                for (var y = 0; y < height / 2; y++)
                {
                    for (var x = 0; x < width / 2; x++)
                    {
                        var a = pixels[2 * y * width + 2 * x];
                        var b = pixels[2 * y * width + 2 * x + 1];
                        var c = pixels[(2 * y + 1) * width + 2 * x];
                        var d = pixels[(2 * y + 1) * width + 2 * x + 1];
                        half[y * (width / 2) + x] = new Color32(
                            (byte)((a.r + b.r + c.r + d.r + 2) / 4), (byte)((a.g + b.g + c.g + d.g + 2) / 4),
                            (byte)((a.b + b.b + c.b + d.b + 2) / 4), (byte)((a.a + b.a + c.a + d.a + 2) / 4));
                    }
                }
                pixels = half;
                width /= 2;
                height /= 2;
            }
            return pixels;
        }

        static void Import(string path, TextureImporterType type, bool srgb, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.isReadable = false;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
