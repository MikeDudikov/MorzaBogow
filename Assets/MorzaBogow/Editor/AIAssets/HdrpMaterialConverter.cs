using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Moves materials made for another pipeline (Standard, URP, or a shader that no longer exists) onto HDRP/Lit,
    /// keeping their colour, main texture, normal map, metal and smoothness, and finding the other maps of the same
    /// texture set by their names. Separate metal, occlusion and roughness or gloss maps are composed into HDRP's mask
    /// map (MaskMaps). One undo step for the whole selection; materials already on an HDRP shader are left alone.
    /// </summary>
    public static class HdrpMaterialConverter
    {
        /// <summary>What a texture is for, read from its name.</summary>
        public enum MapRole { None, BaseColor, Normal, Mask, Metallic, Occlusion, Roughness, Gloss, Height }

        /// <summary>A texture's role, the texture-set name it belongs to, and whether its normals are in DirectX form.</summary>
        public readonly struct MapName
        {
            public readonly MapRole Role;
            public readonly string Stem;
            public readonly bool DirectX;

            public MapName(MapRole role, string stem, bool directX)
            {
                Role = role;
                Stem = stem;
                DirectX = directX;
            }
        }

        // The last word of a name that names a role decides it, so "rock_base_rough" is roughness, not base colour.
        static readonly (MapRole role, string[] words)[] Roles =
        {
            (MapRole.Mask, new[] { "mask", "maskmap" }),
            (MapRole.Normal, new[] { "normal", "normals", "nor", "nrm", "norm", "normalmap", "normalgl", "normaldx" }),
            (MapRole.Roughness, new[] { "rough", "roughness" }),
            (MapRole.Gloss, new[] { "gloss", "glossiness", "smooth", "smoothness" }),
            (MapRole.Metallic, new[] { "metal", "metallic", "metalness" }),
            (MapRole.Occlusion, new[] { "ao", "occlusion", "ambientocclusion" }),
            (MapRole.Height, new[] { "height", "disp", "displacement", "bump" }),
            (MapRole.BaseColor, new[] { "diff", "diffuse", "albedo", "basecolor", "base", "col", "color", "colour" }),
        };

        static readonly HashSet<string> Conventions = new HashSet<string> { "gl", "dx", "ogl", "directx", "opengl" };

        /// <summary>Reads a texture's role and texture-set name: "rock_07_nor_gl_2k" is a normal map of the set "rock_07".</summary>
        public static MapName Classify(string name)
        {
            var words = Words(name);
            var directX = words.Contains("dx") || words.Contains("directx") || words.Contains("normaldx");
            // Sizes such as 2k and conventions such as gl are not part of the set's name.
            var meaningful = words.Where(word => !Conventions.Contains(word) && !IsSize(word)).ToList();
            for (var i = meaningful.Count - 1; i >= 0; i--)
            {
                foreach (var (role, list) in Roles)
                {
                    if (!list.Contains(meaningful[i])) continue;
                    return new MapName(role, string.Join("_", meaningful.Take(i)), directX);
                }
            }
            return new MapName(MapRole.None, string.Join("_", meaningful), directX);
        }

        /// <summary>The name of a texture set as a material or texture names it, in the same form Classify gives.</summary>
        public static string StemOf(string name) => string.Join("_", Words(name).Where(word => !Conventions.Contains(word) && !IsSize(word)));

        static List<string> Words(string name)
        {
            var spaced = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                // A capital after a lower-case letter starts a word: BaseColor → base color.
                if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1])) spaced.Append(' ');
                spaced.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }
            var words = spaced.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            // "base color" read as one word, as most texture sets write it.
            for (var i = 0; i + 1 < words.Count; i++)
            {
                if (words[i] == "base" && (words[i + 1] == "color" || words[i + 1] == "colour")) { words[i] = "basecolor"; words.RemoveAt(i + 1); }
            }
            return words;
        }

        static bool IsSize(string word) => word.Length >= 2 && word.EndsWith("k") && word.Take(word.Length - 1).All(char.IsDigit);

        /// <summary>What one conversion did, for the console.</summary>
        public sealed class Report
        {
            public int Converted, Skipped;
            public readonly List<string> Notes = new List<string>();

            public override string ToString() => $"[HDRP] przeniesiono {Converted}, pominięto {Skipped}" + (Notes.Count > 0 ? "\n" + string.Join("\n", Notes) : string.Empty);
        }

        [MenuItem("Morza Bogów/Grafika/Przenieś zaznaczone materiały do HDRP Lit")]
        static void ConvertSelection()
        {
            var report = ConvertAssets(SelectedMaterials());
            Debug.Log(report);
        }

        [MenuItem("Morza Bogów/Grafika/Przenieś zaznaczone materiały do HDRP Lit", true)]
        static bool CanConvertSelection() => Selection.objects.Any(item => item is Material || item is DefaultAsset);

        /// <summary>Materials selected directly, and every material inside a selected folder.</summary>
        static List<Material> SelectedMaterials()
        {
            var found = new List<Material>();
            foreach (var item in Selection.objects)
            {
                if (item is Material material) { found.Add(material); continue; }
                var path = AssetDatabase.GetAssetPath(item);
                if (!AssetDatabase.IsValidFolder(path)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { path }))
                {
                    var inside = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (inside != null) found.Add(inside);
                }
            }
            return found.Distinct().ToList();
        }

        /// <summary>
        /// Converts material assets: the maps of each come from its own folder and its main texture's folder, the mask
        /// map composed from separate maps is saved beside the material.
        /// </summary>
        public static Report ConvertAssets(IList<Material> materials)
        {
            var report = new Report();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            foreach (var material in materials)
            {
                var maps = FindMaps(material);
                Convert(material, maps, report, mask => SaveMask(material, mask));
            }
            Undo.SetCurrentGroupName("Przeniesienie materiałów do HDRP Lit");
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>True for a material that already uses a working HDRP or Shader Graph shader.</summary>
        public static bool IsHdrp(Material material)
        {
            var shader = material != null ? material.shader : null;
            if (shader == null || !shader.isSupported || shader.name == "Hidden/InternalErrorShader") return false;
            return shader.name.StartsWith("HDRP/") || shader.name.StartsWith("Shader Graphs/");
        }

        /// <summary>
        /// Moves one material to HDRP/Lit with the given maps. A mask map composed from separate maps goes through
        /// <paramref name="keepMask"/> (saved as an asset for material assets, kept in memory otherwise).
        /// </summary>
        public static void Convert(Material material, IReadOnlyDictionary<MapRole, Texture2D> maps, Report report, Func<Texture2D, Texture2D> keepMask = null)
        {
            if (material == null) return;
            if (IsHdrp(material)) { report.Skipped++; return; }
            Undo.RecordObject(material, "Przeniesienie materiału do HDRP Lit");

            // What the old shader knew, under Standard's and URP's names.
            var colour = Read(material, "_BaseColor", "_Color", Color.white);
            var main = ReadTexture(material, "_BaseMap", "_MainTex");
            var normal = ReadTexture(material, "_BumpMap", "_NormalMap");
            var normalScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1f;
            var metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            var smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
            var emission = Read(material, "_EmissionColor", "_EmissionColor", Color.black);
            var emissionMap = ReadTexture(material, "_EmissionMap", "_EmissionMap");

            Texture2D Map(MapRole role) => maps != null && maps.TryGetValue(role, out var texture) ? texture : null;
            var baseMap = Map(MapRole.BaseColor) ?? main as Texture2D;
            var normalMap = Map(MapRole.Normal) ?? normal as Texture2D;
            var mask = Map(MapRole.Mask);
            if (mask == null && (Map(MapRole.Metallic) != null || Map(MapRole.Occlusion) != null || Map(MapRole.Roughness) != null || Map(MapRole.Gloss) != null))
            {
                mask = ComposeMask(Map(MapRole.Metallic), Map(MapRole.Occlusion), Map(MapRole.Roughness), Map(MapRole.Gloss), smoothness, material.name);
                if (mask != null && keepMask != null) mask = keepMask(mask);
            }

            material.shader = Shader.Find("HDRP/Lit");
            material.SetColor("_BaseColor", colour);
            if (baseMap != null) material.SetTexture("_BaseColorMap", baseMap);
            else if (main != null) material.SetTexture("_BaseColorMap", main);
            if (normalMap != null)
            {
                material.SetTexture("_NormalMap", normalMap);
                material.SetFloat("_NormalScale", normalScale);
            }
            if (mask != null)
            {
                material.SetTexture("_MaskMap", mask);
                // With a mask the smoothness and metal come from it, over their full range.
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_SmoothnessRemapMin", 0f);
                material.SetFloat("_SmoothnessRemapMax", 1f);
            }
            else
            {
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smoothness);
            }
            if (emissionMap != null || emission.maxColorComponent > 0f)
            {
                material.SetColor("_EmissiveColor", emission);
                if (emissionMap != null) material.SetTexture("_EmissiveColorMap", emissionMap);
            }
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            report.Converted++;

            var directX = maps != null && maps.TryGetValue(MapRole.Normal, out var found) && found != null && Classify(found.name).DirectX;
            if (directX) report.Notes.Add($"{material.name}: mapa normalnych w konwencji DirectX ({normalMap.name}) — HDRP oczekuje OpenGL; odwróć kanał G w imporcie lub źródle.");
            if (Map(MapRole.Height) != null) report.Notes.Add($"{material.name}: mapa wysokości ({Map(MapRole.Height).name}) pominięta; HDRP/Lit używa jej tylko z teselacją lub paralaksą.");
        }

        static Color Read(Material material, string first, string second, Color fallback) =>
            material.HasProperty(first) ? material.GetColor(first) : material.HasProperty(second) ? material.GetColor(second) : fallback;

        static Texture ReadTexture(Material material, string first, string second) =>
            material.HasProperty(first) && material.GetTexture(first) != null ? material.GetTexture(first)
            : material.HasProperty(second) ? material.GetTexture(second) : null;

        /// <summary>
        /// The maps of a material's texture set: textures in the material's folder and its main texture's folder whose
        /// set name matches the material's or the main texture's; with a single set in those folders, that set.
        /// </summary>
        public static Dictionary<MapRole, Texture2D> FindMaps(Material material)
        {
            var folders = new List<string>();
            var materialPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(materialPath)) folders.Add(Path.GetDirectoryName(materialPath).Replace('\\', '/'));
            var main = ReadTexture(material, "_BaseMap", "_MainTex");
            var mainPath = main != null ? AssetDatabase.GetAssetPath(main) : null;
            if (!string.IsNullOrEmpty(mainPath)) folders.Add(Path.GetDirectoryName(mainPath).Replace('\\', '/'));
            folders = folders.Distinct().Where(AssetDatabase.IsValidFolder).ToList();
            if (folders.Count == 0) return new Dictionary<MapRole, Texture2D>();

            var textures = AssetDatabase.FindAssets("t:Texture2D", folders.ToArray())
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => folders.Contains(Path.GetDirectoryName(path).Replace('\\', '/')))
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(texture => texture != null)
                .ToList();
            return MapsOf(textures, StemOf(material.name), main != null ? Classify(main.name).Stem : null);
        }

        /// <summary>Picks one texture set out of candidates by name and gives its maps by role.</summary>
        public static Dictionary<MapRole, Texture2D> MapsOf(IEnumerable<Texture2D> textures, string materialStem, string mainStem)
        {
            var sets = textures
                .Select(texture => (texture, name: Classify(texture.name)))
                .Where(item => item.name.Role != MapRole.None)
                .GroupBy(item => item.name.Stem)
                .ToList();
            var chosen = sets.FirstOrDefault(set => set.Key == materialStem)
                         ?? sets.FirstOrDefault(set => mainStem != null && set.Key == mainStem)
                         ?? (sets.Count == 1 ? sets[0] : null);
            var maps = new Dictionary<MapRole, Texture2D>();
            if (chosen == null) return maps;
            foreach (var (texture, name) in chosen.OrderBy(item => item.texture.name, StringComparer.Ordinal))
                if (!maps.ContainsKey(name.Role)) maps[name.Role] = texture;
            return maps;
        }

        /// <summary>Composes HDRP's mask map at the largest given size; smaller maps are sampled to it.</summary>
        public static Texture2D ComposeMask(Texture2D metallic, Texture2D occlusion, Texture2D roughness, Texture2D gloss, float smoothness, string name)
        {
            var sources = new[] { metallic, occlusion, roughness, gloss }.Where(item => item != null).ToArray();
            if (sources.Length == 0) return null;
            var width = sources.Max(item => item.width);
            var height = sources.Max(item => item.height);
            Color32[] Pixels(Texture2D source) => source == null ? null : Readable(source, width, height);
            var pixels = MaskMaps.Compose(width * height, Pixels(metallic), Pixels(occlusion), Pixels(roughness), Pixels(gloss), smoothness);
            var mask = new Texture2D(width, height, TextureFormat.RGBA32, true, true) { name = $"{name}_mask_hdrp" };
            mask.SetPixels32(pixels);
            mask.Apply(true);
            return mask;
        }

        /// <summary>
        /// A texture's texels at the given size. A texture asset that is not readable is made readable for the read
        /// and put back exactly as it was imported.
        /// </summary>
        static Color32[] Readable(Texture2D texture, int width, int height)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            var wasReadable = importer == null || importer.isReadable;
            var compression = importer != null ? importer.textureCompression : TextureImporterCompression.Uncompressed;
            if (importer != null && (!wasReadable || compression != TextureImporterCompression.Uncompressed))
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            try
            {
                if (texture.width == width && texture.height == height) return texture.GetPixels32();
                var result = new Color32[width * height];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    result[y * width + x] = texture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                return result;
            }
            finally
            {
                if (importer != null && (!wasReadable || compression != TextureImporterCompression.Uncompressed))
                {
                    importer.isReadable = wasReadable;
                    importer.textureCompression = compression;
                    importer.SaveAndReimport();
                }
            }
        }

        /// <summary>Saves a composed mask beside a material asset as linear PNG and returns the imported texture.</summary>
        static Texture2D SaveMask(Material material, Texture2D mask)
        {
            var materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath)) return mask;
            var path = $"{Path.GetDirectoryName(materialPath).Replace('\\', '/')}/{mask.name}.png";
            File.WriteAllBytes(path, mask.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            UnityEngine.Object.DestroyImmediate(mask);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
