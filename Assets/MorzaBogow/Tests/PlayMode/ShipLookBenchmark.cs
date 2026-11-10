using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// What a fleet of one class costs under the strategic camera: renderers, materials, draw calls, set-pass calls,
    /// batches, main-thread time, garbage per frame and the time to create the ships. The numbers depend on the machine,
    /// so they go into the documentation, not into assertions: the full run (100 ships, 120 measured frames) happens
    /// only with -morzaBenchmark on Unity's command line, and writes its numbers to the temporary cache. Without it the
    /// test only builds and checks the small scene it would measure, so the gate stays fast.
    /// </summary>
    public sealed class ShipLookBenchmark
    {
        const int Ships = 100, Warmup = 30, Frames = 120;

        static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-morzaBenchmark") >= 0;

        static PlaceholderMaterials Materials()
        {
            var lit = Shader.Find("HDRP/Lit");
            Material Paint(string name, Color colour, float smoothness)
            {
                var material = new Material(lit) { name = name };
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_Smoothness", smoothness);
                return material;
            }
            return new PlaceholderMaterials
            {
                Wood = Paint("Placeholder_Wood", new Color(0.36f, 0.24f, 0.14f), 0.35f),
                Bronze = Paint("Placeholder_Bronze", new Color(0.55f, 0.36f, 0.16f), 0.6f),
                PlayerSail = Paint("Placeholder_Sail_Player", new Color(0.12f, 0.22f, 0.6f), 0.2f),
                EnemySail = Paint("Placeholder_Sail_Enemy", new Color(0.55f, 0.08f, 0.08f), 0.2f),
            };
        }

        sealed class Measured
        {
            public int Ships, Renderers, Blocks;
        }

        [UnityTest]
        public IEnumerator Benchmark_PlainLook()
        {
            var measured = new Measured();
            var run = Run("plain", measured);
            while (run.MoveNext()) yield return run.Current;
            Assert.That(measured.Renderers, Is.GreaterThan(measured.Ships), "Every ship draws its hull, deck, rig, sail and oars.");
        }

        /// <summary>Every ship in its own look from the shared palette: what the game does.</summary>
        [UnityTest]
        public IEnumerator Benchmark_LooksFromThePalette()
        {
            var measured = new Measured();
            var run = Run("palette", measured);
            while (run.MoveNext()) yield return run.Current;
            Assert.That(measured.Renderers, Is.GreaterThan(measured.Ships), "Every ship draws its hull, deck, rig, sail and oars.");
        }

        /// <summary>
        /// The same looks drawn the other way: the scene's base materials on every renderer and each look's tone, smoothness
        /// and texture in a MaterialPropertyBlock per submesh. The picture is the same; this measures what that costs the
        /// SRP Batcher, which renderers with property blocks leave.
        /// </summary>
        [UnityTest]
        public IEnumerator Benchmark_LooksFromPropertyBlocks()
        {
            var measured = new Measured();
            var run = Run("mpb", measured);
            while (run.MoveNext()) yield return run.Current;
            Assert.That(measured.Blocks, Is.GreaterThan(measured.Ships), "Every ship's look is carried by property blocks.");
        }

        /// <summary>Puts each palette material back to its scene material and carries its tone in a property block.</summary>
        static int ToPropertyBlocks(ShipVisual view, PlaceholderMaterials materials)
        {
            var bases = new Dictionary<string, Material>();
            foreach (GreekMaterial paint in Enum.GetValues(typeof(GreekMaterial)))
            {
                var material = GreekShips.MaterialFor(paint, materials);
                if (material != null) bases[material.name] = material;
            }
            bases[materials.PlayerSail.name] = materials.PlayerSail;
            bases[materials.EnemySail.name] = materials.EnemySail;
            var blocks = 0;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                var list = renderer.sharedMaterials;
                for (var i = 0; i < list.Length; i++)
                {
                    var variant = list[i];
                    var cut = variant != null ? variant.name.IndexOf(" [", StringComparison.Ordinal) : -1;
                    if (cut < 0 || !bases.TryGetValue(variant.name.Substring(0, cut), out var basis)) continue;
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", variant.GetColor("_BaseColor"));
                    block.SetFloat("_Smoothness", variant.GetFloat("_Smoothness"));
                    var map = variant.GetTexture("_BaseColorMap");
                    if (map != null)
                    {
                        block.SetTexture("_BaseColorMap", map);
                        block.SetVector("_BaseColorMap_ST", variant.GetVector("_BaseColorMap_ST"));
                    }
                    renderer.SetPropertyBlock(block, i);
                    list[i] = basis;
                    blocks++;
                }
                renderer.sharedMaterials = list;
            }
            return blocks;
        }

        IEnumerator Run(string mode, Measured measured)
        {
            var count = Requested ? Ships : 4;
            var materials = Materials();
            var world = new World(1);
            var views = new List<ShipVisual>();
            var root = new GameObject("Benchmark fleet");
            var camera = new GameObject("Benchmark camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<HDAdditionalCameraData>();
            var target = new RenderTexture(1920, 1080, 24) { name = "Benchmark target" };
            var sun = new GameObject("Benchmark sun");
            try
            {
                // A square of ships 30 m apart, headings varied, under the game's oblique camera at a middle zoom.
                var side = Mathf.CeilToInt(Mathf.Sqrt(count));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < count; i++)
                {
                    var x = (i % side - (side - 1) * 0.5f) * 30f;
                    var y = (i / side - (side - 1) * 0.5f) * 30f;
                    var ship = world.SpawnShip(i % 2, ShipClass.Ram, x, y);
                    var view = ShipVisual.Create(ship, materials, mode == "plain" ? null : ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner));
                    if (mode == "mpb") measured.Blocks += ToPropertyBlocks(view, materials);
                    view.transform.SetParent(root.transform, false);
                    view.transform.SetPositionAndRotation(new Vector3(x, 0f, y), Quaternion.Euler(0f, i * 37f % 360f, 0f));
                    views.Add(view);
                }
                watch.Stop();
                var createMs = watch.Elapsed.TotalMilliseconds;

                camera.orthographic = true;
                camera.orthographicSize = side * 15f;
                camera.farClipPlane = 6000f;
                camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
                camera.transform.position = -camera.transform.forward * 600f;
                camera.targetTexture = target;
                sun.AddHDLight(LightType.Directional);
                sun.transform.rotation = ObliqueCamera.SunRotation(ObliqueCamera.DefaultYaw, ObliqueCamera.DefaultTilt);

                var renderers = views.SelectMany(view => view.GetComponentsInChildren<Renderer>(true)).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
                var distinctMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
                Assert.That(views.Count, Is.EqualTo(count));
                measured.Ships = count;
                measured.Renderers = renderers.Count;
                if (!Requested) yield break;

                var counters = new (string key, ProfilerCategory category, string name)[]
                {
                    ("drawCalls", ProfilerCategory.Render, "Draw Calls Count"),
                    ("batches", ProfilerCategory.Render, "Batches Count"),
                    ("setPassCalls", ProfilerCategory.Render, "SetPass Calls Count"),
                    ("triangles", ProfilerCategory.Render, "Triangles Count"),
                    ("mainThreadNs", ProfilerCategory.Internal, "Main Thread"),
                    ("cpuMainFrameNs", ProfilerCategory.Internal, "CPU Main Thread Frame Time"),
                    ("cpuRenderFrameNs", ProfilerCategory.Internal, "CPU Render Thread Frame Time"),
                    ("gcAllocBytes", ProfilerCategory.Memory, "GC Allocated In Frame"),
                };
                var recorders = counters.Select(c => ProfilerRecorder.StartNew(c.category, c.name, 4)).ToArray();
                var sums = new double[counters.Length];
                var samples = new int[counters.Length];
                var animateMs = 0.0;
                try
                {
                    for (var frame = 0; frame < Warmup + Frames; frame++)
                    {
                        var step = System.Diagnostics.Stopwatch.StartNew();
                        foreach (var view in views) view.Animate(0.5f, Time.time, Time.deltaTime, Time.deltaTime);
                        step.Stop();
                        yield return null;
                        if (frame < Warmup) continue;
                        animateMs += step.Elapsed.TotalMilliseconds;
                        for (var c = 0; c < recorders.Length; c++)
                        {
                            if (!recorders[c].Valid) continue;
                            sums[c] += recorders[c].LastValue;
                            samples[c]++;
                        }
                    }
                }
                finally
                {
                    foreach (var recorder in recorders) recorder.Dispose();
                }

                var culture = CultureInfo.InvariantCulture;
                var json = new StringBuilder("{\n");
                json.Append($"  \"mode\": \"{mode}\",\n  \"ships\": {count},\n  \"class\": \"Ram\",\n");
                json.Append($"  \"device\": \"{SystemInfo.graphicsDeviceName}\",\n  \"quality\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\n");
                json.Append($"  \"srpBatcher\": {(GraphicsSettings.useScriptableRenderPipelineBatching ? "true" : "false")},\n  \"resolution\": \"1920x1080\",\n");
                json.Append($"  \"renderersPerShip\": {(renderers.Count / (double)count).ToString("0.00", culture)},\n  \"distinctMaterials\": {distinctMaterials},\n  \"propertyBlocks\": {measured.Blocks},\n");
                json.Append($"  \"createMsPerShip\": {(createMs / count).ToString("0.000", culture)},\n  \"animateMsPerFrame\": {(animateMs / Frames).ToString("0.000", culture)},\n");
                for (var c = 0; c < counters.Length; c++)
                {
                    var value = samples[c] > 0 ? (sums[c] / samples[c]).ToString("0.0", culture) : "null";
                    json.Append($"  \"{counters[c].key}\": {value},\n");
                }
                json.Append($"  \"frames\": {Frames}\n}}\n");
                var path = Path.Combine(Application.temporaryCachePath, $"ship-look-benchmark-{mode}.json");
                File.WriteAllText(path, json.ToString());
                Debug.Log($"[ShipLookBenchmark] {path}\n{json}");
                Assert.That(samples[0] + samples[4], Is.GreaterThan(0), "At least the draw calls or the main thread were measured.");
            }
            finally
            {
                foreach (var view in views) if (view != null) Object.Destroy(view.gameObject);
                Object.Destroy(root);
                Object.Destroy(camera.gameObject);
                Object.Destroy(sun);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
