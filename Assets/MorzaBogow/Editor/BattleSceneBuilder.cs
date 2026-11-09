using MorzaBogow.Presentation;
using MorzaBogow.Settings;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.Linq;

namespace MorzaBogow.Editor
{
    /// <summary>Creates the M1 battle scene and its placeholder materials, and builds the Windows player.</summary>
    public static class BattleSceneBuilder
    {
        const string ScenePath = "Assets/MorzaBogow/Scenes/M1_Battle.unity";
        const string MaterialFolder = "Assets/Art/Placeholders";
        const string SettingsFolder = "Assets/MorzaBogow/Settings";

        [MenuItem("Morza Bogów/M1/Zbuduj scenę bitwy")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Art", "Placeholders");
            AssetDatabase.DeleteAsset($"{MaterialFolder}/Placeholder_Node_Bronze.mat");
            AssetDatabase.DeleteAsset($"{MaterialFolder}/Placeholder_Node_Rations.mat");
            var materials = new PlaceholderMaterials
            {
                Wood = Material("Placeholder_Wood", new Color(0.36f, 0.24f, 0.14f), 0f, 0.35f),
                Bronze = Material("Placeholder_Bronze", new Color(0.55f, 0.36f, 0.16f), 1f, 0.6f),
                PlayerSail = Material("Placeholder_Sail_Player", new Color(0.12f, 0.22f, 0.6f), 0f, 0.2f),
                EnemySail = Material("Placeholder_Sail_Enemy", new Color(0.55f, 0.08f, 0.08f), 0f, 0.2f),
                Neutral = Material("Placeholder_Neutral", new Color(0.55f, 0.53f, 0.48f), 0f, 0.2f),
                WoodNode = Material("Placeholder_Node_Wood", new Color(0.45f, 0.3f, 0.12f), 0f, 0.3f),
                SilverNode = Material("Placeholder_Node_Silver", new Color(0.78f, 0.8f, 0.85f), 0.9f, 0.7f),
                Selection = Material("Placeholder_Selection", new Color(0.3f, 1f, 0.35f), 0f, 0.1f),
            };

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Strategic Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<HDAdditionalCameraData>();
            camera.farClipPlane = 6000f;
            camera.transform.SetPositionAndRotation(new Vector3(-500f, 130f, -100f), Quaternion.Euler(52f, 0f, 0f));

            var sun = new GameObject("Sun");
            sun.AddHDLight(LightType.Directional);
            sun.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            var light = sun.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 100000f;
            light.shadows = LightShadows.Soft;

            var profiles = GraphicsProfiles.All
                .Select(profile => AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{SettingsFolder}/Volume_{profile}.asset")).ToArray();
            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profiles[(int)GraphicsProfiles.Default];

            new GameObject("Ocean").AddComponent<WaterSurface>();

            var services = new GameObject("Services");
            services.AddComponent<GraphicsProfileController>().Configure(volume, profiles);
            services.AddComponent<DisplaySettingsController>();
            var coast = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Coast/Coast_Rocks_02.prefab");
            new GameObject("Battle").AddComponent<BattleController>().Configure(coast, materials, camera);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/MorzaBogow/Scenes/M0_Probe.unity", false),
                new EditorBuildSettingsScene("Assets/MorzaBogow/Scenes/Art_Integration_Coast.unity", false),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[M1] Battle scene built.");
        }

        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/M1/MorzaBogow.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[M1] Build {report.summary.result}, {report.summary.totalSize} bytes, {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings.");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
