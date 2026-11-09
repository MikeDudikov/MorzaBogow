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
                Selection = RingMaterial("Placeholder_Selection", new Color(0.45f, 1f, 0.5f, 0.42f)),
                Foam = Material("Placeholder_Foam", new Color(0.9f, 0.95f, 0.97f), 0f, 0.3f),
                Arrow = Material("Placeholder_Arrow", new Color(0.12f, 0.1f, 0.08f), 0f, 0.2f),
                Flash = Material("Placeholder_Flash", new Color(1f, 0.6f, 0.2f), 0f, 0.1f),
                Rope = Material("Placeholder_Rope", new Color(0.72f, 0.6f, 0.38f), 0f, 0.1f),
                OrderLine = RingMaterial("Placeholder_OrderLine", new Color(0.45f, 1f, 0.5f, 0.42f)),
                WreckMarker = Material("Placeholder_Wreck", new Color(0.16f, 0.12f, 0.1f), 0f, 0.2f),
                TargetRing = RingMaterial("Placeholder_TargetRing", new Color(1f, 0.15f, 0.1f, 0.7f)),
                Timber = Material("Placeholder_Timber", new Color(0.42f, 0.27f, 0.13f), 0f, 0.25f),
                Foliage = Material("Placeholder_Foliage", new Color(0.13f, 0.3f, 0.12f), 0f, 0.15f),
                Rock = Material("Placeholder_Rock", new Color(0.2f, 0.19f, 0.2f), 0f, 0.15f),
                SilverOre = Material("Placeholder_SilverOre", new Color(0.85f, 0.87f, 0.92f), 1f, 0.85f),
                MineDark = Material("Placeholder_MineDark", new Color(0.02f, 0.02f, 0.02f), 0f, 0.05f),
                Fog = FogMaterial(),
                BowWave = WaterDecalMaterial("Placeholder_WaterBowWave", BowWaveType, deformation: true, foam: false),
                FoamDisk = WaterDecalMaterial("Placeholder_WaterFoamDisk", DiskType, deformation: false, foam: true),
            };

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Strategic Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<HDAdditionalCameraData>();
            camera.farClipPlane = 6000f;
            camera.orthographic = true;
            camera.transform.SetPositionAndRotation(new Vector3(-500f, 130f, -100f), Quaternion.Euler(52f, 0f, 0f));

            var sun = new GameObject("Sun");
            sun.AddHDLight(LightType.Directional);
            sun.transform.rotation = ObliqueCamera.SunRotation(ObliqueCamera.DefaultYaw, ObliqueCamera.DefaultTilt);
            var light = sun.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 100000f;
            light.shadows = LightShadows.Soft;

            var profiles = GraphicsProfiles.All
                .Select(profile => AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{SettingsFolder}/Volume_{profile}.asset")).ToArray();
            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profiles[(int)GraphicsProfiles.Default];

            var ocean = new GameObject("Ocean").AddComponent<WaterSurface>();
            ocean.scriptInteractions = true;

            // These fades follow camera distance, which means nothing in an orthographic view. Beyond every
            // camera position, they keep waves and gloss the same at each zoom level.
            ocean.ripplesFadeStart = 3000f;
            ocean.largeBand1FadeStart = 3000f;
            ocean.smoothnessFadeStart = 3000f;
            ocean.tessellationFactorFadeStart = 3000f;

            // From this view the sky reflects weakly, so the water's own color has to carry the sea and its waves.
            // Lower gloss turns sun glints into a soft shimmer instead of white flashes.
            ocean.scatteringColor = new Color(0.045f, 0.125f, 0.155f);
            ocean.ambientScattering = 0.25f;
            ocean.heightScattering = 0.5f;
            ocean.displacementScattering = 0.6f;
            ocean.startSmoothness = 0.8f;
            ocean.endSmoothness = 0.8f;

            // Hulls and shores change the surface: deformation for bow waves, foam where water breaks. The decal
            // region follows the view; 2048 texels over 1024 m keep a cutter's bow wave sharp.
            ocean.deformation = true;
            ocean.deformationRes = WaterSurface.WaterDecalRegionResolution.Resolution2048;
            ocean.foam = true;
            ocean.foamResolution = WaterSurface.WaterDecalRegionResolution.Resolution2048;
            ocean.decalRegionSize = new Vector2(1024f, 1024f);

            // Short-lived foam: water broken by a hull settles quickly instead of drawing a long strip behind it.
            ocean.foamPersistenceMultiplier = 0.3f;

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

        const string WaterDecalShaderPath = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/ShaderGraph/Water Decal Sample.shadergraph";
        const int DiskType = 0;
        const int BowWaveType = 2;

        /// <summary>
        /// A material for HDRP's built-in sample water decal, with the shapes HDRP ships: a disk and a bow wave.
        /// Amplitude and foam strength are set per decal at runtime, so all ships share one atlas slot.
        /// </summary>
        static Material WaterDecalMaterial(string name, int type, bool deformation, bool foam)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterDecalShaderPath);
            if (shader == null) throw new System.InvalidOperationException("HDRP water decal shader not found: " + WaterDecalShaderPath);

            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_TYPE", type);
            material.SetFloat("_AffectDeformation", deformation ? 1f : 0f);
            material.SetFloat("_AffectFoam", foam ? 1f : 0f);
            if (type == BowWaveType) material.SetFloat("_Elevation", 0.5f);
            // Water decal graphs are not HDRP surface shaders; HDRP's own migration validates them this way.
            MaterialEditor.ApplyMaterialPropertyDrawers(material);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Unlit and transparent, so a ring keeps its color and never hides what it marks.</summary>
        static Material RingMaterial(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("HDRP/Unlit");
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaCutoffEnable", 0f);
            material.SetColor("_UnlitColor", color);
            material.SetTexture("_UnlitColorMap", RingEdgeTexture());
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Alpha across the ring band: zero at both edges, full in the middle, a barely noticeable blur.</summary>
        static Texture2D RingEdgeTexture()
        {
            var path = $"{MaterialFolder}/Placeholder_RingEdge.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;

            const int Size = 32;
            texture = new Texture2D(4, Size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            for (var y = 0; y < Size; y++)
            {
                var across = y / (Size - 1f);
                var alpha = Mathf.SmoothStep(0f, 1f, across / 0.25f) * Mathf.SmoothStep(0f, 1f, (1f - across) / 0.25f);
                for (var x = 0; x < 4; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        static Material FogMaterial()
        {
            var path = $"{MaterialFolder}/Placeholder_Fog.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaCutoffEnable", 0f);
            material.SetColor("_UnlitColor", Color.white);
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
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
