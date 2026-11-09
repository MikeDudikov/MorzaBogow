using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Records real device and HDRP feature availability (V00) with informational frame data (V10).
    /// Active only when the player starts with -morzaProbeReport path.
    /// </summary>
    public sealed class M0Probe : MonoBehaviour
    {
        const int WarmupFrames = 180;
        const int MeasuredFrames = 600;

        void Start()
        {
            var path = ArgumentAfter("-morzaProbeReport");
            if (string.IsNullOrEmpty(path))
            {
                enabled = false;
                return;
            }

            StartCoroutine(Run(path));
        }

        IEnumerator Run(string path)
        {
            for (var i = 0; i < WarmupFrames; i++) yield return null;

            var samples = new List<float>(MeasuredFrames);
            for (var i = 0; i < MeasuredFrames; i++)
            {
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
            }

            File.WriteAllText(path, BuildReport(samples), Encoding.UTF8);
            Application.Quit(0);
        }

        static string BuildReport(List<float> frameMilliseconds)
        {
            frameMilliseconds.Sort();
            var pipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            var settings = asset != null ? asset.currentPlatformRenderPipelineSettings : default;

            var builder = new StringBuilder("{\n");
            void Text(string key, string value) => builder.Append($"  \"{key}\": \"{Escape(value)}\",\n");
            void Raw(string key, string value) => builder.Append($"  \"{key}\": {value},\n");
            void Flag(string key, bool value) => Raw(key, value ? "true" : "false");

            Text("profile", QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Text("unity", Application.unityVersion);
            Text("gpu", SystemInfo.graphicsDeviceName);
            Text("graphicsApi", SystemInfo.graphicsDeviceType.ToString());
            Text("driver", SystemInfo.graphicsDeviceVersion);
            Raw("vramMb", SystemInfo.graphicsMemorySize.ToString());
            Text("cpu", SystemInfo.processorType);
            Raw("ramMb", SystemInfo.systemMemorySize.ToString());
            Raw("screenWidth", Screen.width.ToString());
            Raw("screenHeight", Screen.height.ToString());
            Text("fullScreenMode", Screen.fullScreenMode.ToString());
            Text("reportedModes", string.Join(" ", Screen.resolutions.Select(m => $"{m.width}x{m.height}").Distinct()));
            Flag("hdrpActive", pipeline != null);
            Flag("deviceSupportsRayTracing", SystemInfo.supportsRayTracing);
            Flag("deviceSupportsRayTracingShaders", SystemInfo.supportsRayTracingShaders);
            Flag("assetRayTracing", asset != null && settings.supportRayTracing);
            Flag("pipelineRayTracingActive", pipeline != null && pipeline.rayTracingSupported);
            Flag("assetWater", asset != null && settings.supportWater);
            Flag("assetVolumetrics", asset != null && settings.supportVolumetrics);
            Flag("assetSsr", asset != null && settings.supportSSR);
            Flag("assetSsgi", asset != null && settings.supportSSGI);
            Flag("assetSubsurfaceScattering", asset != null && settings.supportSubsurfaceScattering);
            Raw("frameCount", frameMilliseconds.Count.ToString());
            Raw("frameMsP50", Percentile(frameMilliseconds, 0.50f));
            Raw("frameMsP95", Percentile(frameMilliseconds, 0.95f));
            Raw("frameMsP99", Percentile(frameMilliseconds, 0.99f));
            Text("scope", "M0 test scene with water, sky and the selected profile; no ships; informational only");
            builder.Append($"  \"utc\": \"{DateTime.UtcNow:o}\"\n}}\n");
            return builder.ToString();
        }

        static string Percentile(List<float> sorted, float fraction)
        {
            var index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * fraction) - 1, 0, sorted.Count - 1);
            return sorted[index].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string ArgumentAfter(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
