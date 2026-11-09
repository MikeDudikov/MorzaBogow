using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Editor
{
    /// <summary>Imports Coast Rocks 02 from External into game-owned material, prefab and test scene.</summary>
    public static class CoastImporter
    {
        const string Source = "Assets/Art/External/Coast_Rocks_02";
        const string Output = "Assets/Art/Coast";
        const string Fbx = Source + "/coast_rocks_02_2k.fbx";
        const string Diffuse = Source + "/Textures/coast_rocks_02_diff_2k.exr";
        const string Normal = Source + "/Textures/coast_rocks_02_nor_gl_2k.exr";
        const string Roughness = Source + "/Textures/coast_rocks_02_rough_2k.exr";
        const string Occlusion = Source + "/Textures/coast_rocks_02_ao_2k.exr";
        const string MaskPath = Output + "/coast_rocks_02_mask.png";
        const string MaterialPath = Output + "/Coast_Rocks_02.mat";
        const string PrefabPath = Output + "/Coast_Rocks_02.prefab";
        const string ScenePath = "Assets/MorzaBogow/Scenes/Art_Integration_Coast.unity";

        [MenuItem("Morza Bogów/M1/Importuj wybrzeże")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/Art", "Coast");

            ConfigureTextures();
            ConfigureModel();
            BuildMaskMap();
            var material = BuildMaterial();
            var prefab = BuildPrefab(material);
            CreateScene(prefab);
            WriteReport(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[M1] Coast Rocks 02 imported.");
        }

        static void ConfigureTextures()
        {
            // EXR data is linear, so sRGB stays off for every map.
            Texture(Diffuse, TextureImporterType.Default, false, false);
            Texture(Normal, TextureImporterType.NormalMap, false, false);
            Texture(Roughness, TextureImporterType.Default, false, true);
            Texture(Occlusion, TextureImporterType.Default, false, true);
        }

        static void Texture(string path, TextureImporterType type, bool srgb, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.isReadable = readable;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void ConfigureModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.indexFormat = ModelImporterIndexFormat.UInt32;
            importer.generateMeshLods = true;
            importer.SaveAndReimport();
        }

        static void BuildMaskMap()
        {
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(Roughness);
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(Occlusion);
            var width = roughness.width;
            var height = roughness.height;
            var roughPixels = roughness.GetPixels();
            var occlusionPixels = occlusion.width == width && occlusion.height == height
                ? occlusion.GetPixels()
                : throw new IOException("Roughness and AO sizes differ.");

            // HDRP Lit mask: R metallic, G AO, B detail mask, A smoothness. Converted exactly once here.
            var mask = new Color32[roughPixels.Length];
            for (var i = 0; i < mask.Length; i++)
            {
                var ao = (byte)Mathf.RoundToInt(Mathf.Clamp01(occlusionPixels[i].r) * 255f);
                var smoothness = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - roughPixels[i].r) * 255f);
                mask[i] = new Color32(0, ao, 0, smoothness);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(mask);
            texture.Apply(false);
            File.WriteAllBytes(MaskPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(MaskPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(MaskPath);
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Restore(Roughness);
            Restore(Occlusion);
        }

        static void Restore(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material BuildMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Diffuse));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Normal));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath));
            material.SetFloat("_NormalMapSpace", 0f);
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject BuildPrefab(Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = new GameObject("Coast_Rocks_02");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Visual";

            var renderers = visual.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers) renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();

            // Simple blocker only. Navigation obstacles stay independent from visual LOD.
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var box = new GameObject("Blocker").AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false);
            box.center = bounds.center;
            box.size = bounds.size;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void CreateScene(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var coast = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            var bounds = new Bounds(coast.transform.position, Vector3.one);
            foreach (var renderer in coast.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(renderer.bounds);

            var camera = new GameObject("Strategic Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<HDAdditionalCameraData>();
            var distance = Mathf.Max(bounds.size.magnitude, 40f);
            camera.transform.position = bounds.center + new Vector3(0f, distance * 0.7f, -distance * 0.7f);
            camera.transform.LookAt(bounds.center);
            camera.farClipPlane = 6000f;

            var sun = new GameObject("Sun");
            sun.AddHDLight(LightType.Directional);
            sun.transform.rotation = Quaternion.Euler(35f, 30f, 0f);
            var light = sun.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 100000f;
            light.shadows = LightShadows.Soft;

            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/MorzaBogow/Settings/Volume_Ultra.asset");

            new GameObject("Ocean").AddComponent<WaterSurface>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void WriteReport(GameObject prefab)
        {
            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            var meshes = filters.Select(filter => filter.sharedMesh).Where(mesh => mesh != null).ToArray();
            var triangles = meshes.Sum(mesh => (long)mesh.GetIndexCount(0) / 3);
            var size = new Bounds();
            foreach (var mesh in meshes) size.Encapsulate(mesh.bounds);

            var report = new StringBuilder("{\n");
            report.Append($"  \"meshes\": {meshes.Length},\n");
            report.Append($"  \"submeshes\": {meshes.Sum(mesh => mesh.subMeshCount)},\n");
            report.Append($"  \"meshLodCounts\": \"{string.Join(",", meshes.Select(mesh => mesh.lodCount))}\",\n");
            report.Append($"  \"lod0Triangles\": {triangles},\n");
            report.Append($"  \"lastLodTriangles\": \"{string.Join(",", meshes.Select(mesh => mesh.GetIndexCount(0) == 0 ? 0 : mesh.GetLod(0, mesh.lodCount - 1).indexCount / 3))}\",\n");
            report.Append($"  \"meshBoundsMetres\": \"{size.size.x:0.##} x {size.size.y:0.##} x {size.size.z:0.##}\",\n");
            report.Append("  \"materials\": 1\n}\n");
            File.WriteAllText("artifacts-log/coast-import.json", report.ToString());
        }
    }
}
