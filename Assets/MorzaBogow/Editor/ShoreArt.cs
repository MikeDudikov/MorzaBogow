using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Prepares the Aegean shore from CC0 Poly Haven sources (see Assets/Art/PolyHaven/ZRODLA_CC0.txt): world-mapped
    /// limestone, seabed and meadow materials, and boulder prefabs with mesh LODs. Mask maps are made once from
    /// the roughness maps (HDRP mask: R metallic, G AO, B detail, A smoothness).
    /// </summary>
    public static class ShoreArt
    {
        const string Source = "Assets/Art/PolyHaven";
        const string Output = "Assets/Art/Shore";

        public sealed class Result
        {
            public Material Limestone, Seabed, Meadow;
            public GameObject[] Boulders;
        }

        public static Result Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Source)) return null;
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/Art", "Shore");

            var result = new Result
            {
                // Pale limestone in tiles of 9 m, a bright sandy floor in 11 m, dry grass over rock in 14 m.
                Limestone = Surface("Shore_Limestone", "marble_cliff_03", new Color(0.86f, 0.85f, 0.82f), 9f),
                Seabed = Surface("Shore_Seabed", "coast_sand_rocks_02", new Color(1f, 0.98f, 0.92f), 11f),
                Meadow = Surface("Shore_Meadow", "aerial_grass_rock", new Color(0.7f, 0.8f, 0.52f), 14f),
            };
            // Pale grey stones only; the red namaqualand boulder does not belong on Aegean limestone.
            result.Boulders = new[] { "rock_07", "boulder_01" }.Select(Boulder).Where(prefab => prefab != null).ToArray();
            AssetDatabase.SaveAssets();
            return result;
        }

        static Material Surface(string name, string id, Color tint, float tile)
        {
            var diffuse = $"{Source}/Textures/{id}_diff_2k.jpg";
            var normal = $"{Source}/Textures/{id}_nor_gl_2k.jpg";
            var rough = $"{Source}/Textures/{id}_rough_2k.jpg";
            if (!File.Exists(diffuse)) return null;
            Texture(diffuse, TextureImporterType.Default, true, false);
            Texture(normal, TextureImporterType.NormalMap, false, false);
            var mask = Mask(rough, $"{Output}/{id}_mask.png");

            var material = MaterialAt($"{Output}/{name}.mat");
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetTexture("_MaskMap", mask);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_NormalScale", 1.2f);

            // World-space triplanar mapping: the procedural shore meshes have no UVs, and cliffs stay unstretched.
            material.SetFloat("_UVBase", 5f);
            material.SetFloat("_TexWorldScale", 1f / tile);
            material.SetFloat("_ObjectSpaceUVMapping", 0f);
            material.enableInstancing = true;
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Boulder(string id)
        {
            var folder = $"{Source}/Models/{id}";
            var fbx = $"{folder}/{id}_1k.fbx";
            if (!File.Exists(fbx)) return null;

            var model = (ModelImporter)AssetImporter.GetAtPath(fbx);
            model.importCameras = false;
            model.importLights = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importAnimation = false;
            model.addCollider = false;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.indexFormat = ModelImporterIndexFormat.UInt32;
            model.generateMeshLods = true;
            model.SaveAndReimport();

            var diffuse = $"{folder}/textures/{id}_diff_1k.jpg";
            var normal = $"{folder}/textures/{id}_nor_gl_1k.exr";
            var rough = $"{folder}/textures/{id}_rough_1k.exr";
            Texture(diffuse, TextureImporterType.Default, true, false);
            Texture(normal, TextureImporterType.NormalMap, false, false);
            var material = MaterialAt($"{Output}/Boulder_{id}.mat");
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetTexture("_MaskMap", Mask(rough, $"{Output}/{id}_mask.png"));
            // Warmed and lightened toward the pale Aegean limestone.
            material.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.88f));
            material.enableInstancing = true;
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject($"Boulder_{id}");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.name = "Visual";
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();

            // Seen from the strategic camera a boulder covers a few dozen pixels: a few thousand triangles of the
            // model's own simplified levels are plenty, and hundreds of them stay cheap.
            var index = 0;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                filter.sharedMesh = Simplified(filter.sharedMesh, 3000, $"{Output}/Boulder_{id}_{index++}.asset");
            }

            var meshes = visual.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh).Where(mesh => mesh != null).ToArray();
            var triangles = meshes.Sum(mesh => (long)mesh.GetIndexCount(0) / 3);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Output}/Boulder_{id}.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[Shore] {id}: {triangles} triangles at LOD0, LODs {string.Join(",", meshes.Select(mesh => mesh.lodCount))}");
            return prefab;
        }

        /// <summary>
        /// A standalone mesh made of the first simplified level of detail that fits the triangle budget, taken from
        /// the levels the importer generated. Vertex data is copied as it is; only the triangle list changes.
        /// </summary>
        static Mesh Simplified(Mesh mesh, int budget, string path)
        {
            var level = 0;
            for (; level < mesh.lodCount - 1; level++)
            {
                if (mesh.GetLod(0, level).indexCount / 3 <= budget) break;
            }
            var range = mesh.GetLod(0, level);
            int[] indices;
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var all = data[0].GetIndexData<uint>();
                indices = new int[range.indexCount];
                var start = (int)range.indexStart;
                for (var i = 0; i < indices.Length; i++) indices[i] = (int)all[start + i];
            }

            var simple = new Mesh { name = mesh.name + "_simple", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            simple.vertices = mesh.vertices;
            simple.normals = mesh.normals;
            simple.tangents = mesh.tangents;
            simple.uv = mesh.uv;
            simple.SetTriangles(indices, 0);
            simple.RecalculateBounds();
            simple.Optimize();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(simple, path);
            return simple;
        }

        static Material MaterialAt(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Texture(string path, TextureImporterType type, bool srgb, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            if (importer.textureType == type && importer.sRGBTexture == srgb && importer.isReadable == readable && importer.maxTextureSize == 2048) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.isReadable = readable;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.textureCompression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>A mask map from a roughness map, made once and kept as an asset.</summary>
        static Texture2D Mask(string roughPath, string maskPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (existing != null) return existing;
            Texture(roughPath, TextureImporterType.Default, false, true);
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            var pixels = rough.GetPixels();
            var mask = new Color32[pixels.Length];
            for (var i = 0; i < mask.Length; i++)
            {
                var smoothness = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - pixels[i].r) * 255f);
                mask[i] = new Color32(0, 255, 0, smoothness);
            }
            var texture = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(mask);
            texture.Apply(false);
            File.WriteAllBytes(maskPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(maskPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            var roughImporter = (TextureImporter)AssetImporter.GetAtPath(roughPath);
            roughImporter.isReadable = false;
            roughImporter.textureCompression = TextureImporterCompression.CompressedHQ;
            roughImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
        }
    }
}
