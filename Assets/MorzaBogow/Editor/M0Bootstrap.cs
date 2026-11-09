using System.IO;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Editor
{
    /// <summary>Batch entry points for the M0 HDRP profiles (V10) and Windows build.</summary>
    public static class M0Bootstrap
    {
        const string SettingsFolder = "Assets/MorzaBogow/Settings";
        const string ScenesFolder = "Assets/MorzaBogow/Scenes";
        const string ScenePath = ScenesFolder + "/M0_Probe.unity";

        [MenuItem("Morza Bogów/M0/Skonfiguruj projekt")]
        public static void Run()
        {
            EnsureFolder("Assets/MorzaBogow", "Settings");
            EnsureFolder("Assets/MorzaBogow", "Scenes");
            ConfigurePlayer();
            ConfigureQualityLevels();

            var assets = GraphicsProfiles.All.Select(EnsurePipelineAsset).ToArray();
            AssignPipelines(assets);
            var profiles = GraphicsProfiles.All.Select(EnsureVolumeProfile).ToArray();
            CreateProbeScene(profiles);
            AssetDatabase.SaveAssets();
            Debug.Log($"[M0] Configured {assets.Length} HDRP profiles: {string.Join(", ", QualitySettings.names)}.");
        }

        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/M0/MorzaBogow.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[M0] Build {report.summary.result}, {report.summary.totalSize} bytes, " +
                      $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings.");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static HDRenderPipelineAsset EnsurePipelineAsset(GraphicsProfile profile)
        {
            var path = $"{SettingsFolder}/HDRP_{profile}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var atLeastMedium = profile >= GraphicsProfile.Medium;
            var atLeastHigh = profile >= GraphicsProfile.High;
            var atLeastExtraHigh = profile >= GraphicsProfile.ExtraHigh;
            var ultra = profile == GraphicsProfile.Ultra;

            var settings = asset.currentPlatformRenderPipelineSettings;
            settings.supportRayTracing = ultra;
            if (ultra) settings.supportedRayTracingMode = RenderPipelineSettings.SupportedRayTracingMode.Both;
            settings.supportSSR = atLeastHigh;
            settings.supportSSGI = atLeastExtraHigh;
            settings.supportVolumetrics = atLeastMedium;
            settings.supportVolumetricClouds = atLeastExtraHigh;
            settings.supportSubsurfaceScattering = atLeastHigh;
            settings.supportWater = true;
            settings.supportWaterDecals = atLeastHigh;

            // Every visible ship has a bow wave and two foam patches, and surf runs along every shore: 400 ships need room.
            settings.maximumWaterDecalCount = 2048;
            settings.supportTransparentBackface = atLeastHigh;
            settings.supportTransparentDepthPrepass = atLeastHigh;
            settings.supportTransparentDepthPostpass = atLeastHigh;
            settings.colorBufferFormat = atLeastHigh
                ? RenderPipelineSettings.ColorBufferFormat.R16G16B16A16
                : RenderPipelineSettings.ColorBufferFormat.R11G11B10;
            asset.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "MikeDudikov";
            PlayerSettings.productName = "Morza Bogów";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        /// <summary>Unity has no public API for adding quality levels, so the settings asset is edited directly.</summary>
        static void ConfigureQualityLevels()
        {
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            levels.arraySize = GraphicsProfiles.All.Count;
            for (var i = 0; i < GraphicsProfiles.All.Count; i++)
            {
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue =
                    GraphicsProfiles.DisplayName(GraphicsProfiles.All[i]);
            }

            var ultraIndex = (int)GraphicsProfiles.Default;
            quality.FindProperty("m_CurrentQuality").intValue = ultraIndex;
            var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (var i = 0; i < defaults.arraySize; i++)
            {
                defaults.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = ultraIndex;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignPipelines(HDRenderPipelineAsset[] assets)
        {
            GraphicsSettings.defaultRenderPipeline = assets[(int)GraphicsProfiles.Default];
            for (var level = 0; level < assets.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = assets[level];
            }
            QualitySettings.SetQualityLevel((int)GraphicsProfiles.Default, false);
        }

        static VolumeProfile EnsureVolumeProfile(GraphicsProfile tier)
        {
            var path = $"{SettingsFolder}/Volume_{tier}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var environment = profile.Add<VisualEnvironment>(true);
            environment.skyType.Override((int)SkyType.PhysicallyBased);
            profile.Add<PhysicallyBasedSky>(true);

            // V05: fixed exposure, so dark fog on screen never brightens the visible part of the battle.
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(12.25f);

            // Compresses highlights, so sunlit sand and light models keep their shape at this exposure.
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            // Neutral tonemapping washes colors out; this brings the water back to its earlier blue-green.
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(35f);

            var shadows = profile.Add<HDShadowSettings>(true);
            shadows.cascadeShadowSplitCount.Override(tier >= GraphicsProfile.ExtraHigh ? 4 : tier == GraphicsProfile.High ? 3 : 2);

            var fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);

            // An orthographic camera distance is arbitrary, so thick distance fog would change the picture with zoom.
            fog.meanFreePath.Override(6000f);
            fog.enableVolumetricFog.Override(tier >= GraphicsProfile.Medium);

            var indirect = profile.Add<GlobalIllumination>(true);
            indirect.enable.Override(tier >= GraphicsProfile.ExtraHigh);

            // Ray traced indirect light leaves speckles under the orthographic game camera, so Ultra uses SSGI too.
            indirect.tracing.Override(RayCastingMode.RayMarching);

            var reflections = profile.Add<ScreenSpaceReflection>(true);
            reflections.enabled.Override(tier >= GraphicsProfile.High);
            reflections.tracing.Override(tier == GraphicsProfile.Ultra ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);

            foreach (var component in profile.components.Where(item => !AssetDatabase.Contains(item)))
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void CreateProbeScene(VolumeProfile[] profiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.gameObject.tag = "MainCamera";
            camera.gameObject.AddComponent<HDAdditionalCameraData>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 30f, -60f), Quaternion.Euler(20f, 0f, 0f));
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
            volume.sharedProfile = profiles[(int)GraphicsProfiles.Default];

            new GameObject("Ocean").AddComponent<WaterSurface>();

            var metal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            metal.name = "Reflective test sphere";
            metal.transform.position = new Vector3(0f, 12f, 0f);
            metal.transform.localScale = Vector3.one * 16f;

            var services = new GameObject("Services");
            services.AddComponent<GraphicsProfileController>().Configure(volume, profiles);
            services.AddComponent<DisplaySettingsController>();
            services.AddComponent<M0Probe>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
