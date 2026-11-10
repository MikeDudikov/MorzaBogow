using System.IO;
using MorzaBogow.Presentation;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Builds the crews' assets (docs/art/ZALOGI.md): the palette atlas and its mask as textures, the one HDRP/Lit
    /// material with instancing enabled in a Resources folder (so its instancing variant goes into the build: the project
    /// strips unused instancing variants), and a preview prefab of a man in each role for the editor. Batch:
    /// -executeMethod MorzaBogow.Editor.CrewAssetBuilder.Run
    /// </summary>
    public static class CrewAssetBuilder
    {
        public const string Folder = "Assets/MorzaBogow/Resources/Crew";
        public const string MaterialPath = Folder + "/CrewBody.mat";
        public const string PreviewFolder = "Assets/MorzaBogow/Art/Crew/Prefabs";

        [MenuItem("Morza Bogów/Załogi/Zbuduj materiał i podgląd ról")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var (albedo, mask) = CrewPalette.Textures();
            var albedoPath = Folder + "/CrewPalette.png";
            var maskPath = Folder + "/CrewPaletteMask.png";
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);
            Configure(albedoPath, true);
            Configure(maskPath, false);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit")) { name = "CrewBody" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find("HDRP/Lit");
            material.enableInstancing = true;
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_MetallicRemapMin", 0f);
            material.SetFloat("_MetallicRemapMax", 1f);
            material.SetFloat("_SmoothnessRemapMin", 0f);
            material.SetFloat("_SmoothnessRemapMax", 1f);
            material.SetFloat("_AORemapMin", 0f);
            material.SetFloat("_AORemapMax", 1f);
            // Small figures take no decals (an instanced draw would get them wrong anyway).
            material.SetFloat("_SupportDecals", 0f);
            material.EnableKeyword("_DISABLE_DECALS");
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            BuildPreviews(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"CrewAssetBuilder: {MaterialPath}, {albedoPath}, {maskPath}, previews in {PreviewFolder}");
        }

        /// <summary>Point-sampled cells without mipmaps (no bleeding between cells); the albedo sRGB, the mask linear data.</summary>
        static void Configure(string path, bool colour)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = colour;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = colour ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// A prefab of a man in each role, posed at rest, for looking at in the editor (Art/Crew/Prefabs). The game draws
        /// its crews instanced and never instantiates these.
        /// </summary>
        static void BuildPreviews(Material material)
        {
            Directory.CreateDirectory(PreviewFolder);
            var meshFolder = PreviewFolder + "/Meshes";
            Directory.CreateDirectory(meshFolder);
            foreach (var info in CrewRoles.All)
            {
                var root = new GameObject($"Crew {info.Role}");
                var merged = CrewAboard.PreviewMesh(info.Role);
                var meshPath = $"{meshFolder}/Crew {info.Role}.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(merged, meshPath);
                root.AddComponent<MeshFilter>().sharedMesh = merged;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root, $"{PreviewFolder}/Crew {info.Role}.prefab");
                Object.DestroyImmediate(root);
            }
        }
    }
}
