using System;
using MorzaBogow.Simulation;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Draws the simulation's sea into the HDRP water. A compute shader sums the same 144 components the ships feel
    /// (SeaSurface: λ ≥ 8 m, amplitudes from the wave field, depth-limited near shores) into a height texture over the
    /// 1024 m water decal region around the view (512², 2 m texels), and one water decal in texture mode (the HDRP Water
    /// Decal Sample graph) adds it to the surface. HDRP's own large band is switched off so the waves are not drawn
    /// twice; its medium band, at a third, and its ripples stay as texture: decoration only, shorter than the physics.
    /// Phases run on simulation time, so the sea stands still in a pause and keeps pace with the game speed.
    /// </summary>
    public sealed class SeaSurfaceRenderer : IDisposable
    {
        public const int Resolution = 512;
        public const float Size = 1024f;
        public const float EdgeFade = 32f;
        public const float MediumBand = 0.33f;
        const int Components = SeaSurface.Components;
        const int DepthNodes = 261;
        const float DepthOrigin = -1300f, DepthSpacing = 10f;

        readonly ComputeShader shader;
        readonly int kernel;
        readonly RenderTexture heights;
        readonly ComputeBuffer componentBuffer;
        ComputeBuffer previousBuffer, currentBuffer, waterBuffer;
        readonly Texture2D depthTexture;
        readonly Material material;
        readonly GameObject decalObject;
        readonly WaterDecal decal;
        readonly float[] componentData = new float[Components * 4];
        readonly float[] largest = new float[Components];
        float[] previousData = Array.Empty<float>();
        float[] currentData = Array.Empty<float>();
        float[] waterData = Array.Empty<float>();
        int windowWidth;
        Bathymetry depthsUsed;

        // The window's energies go to the GPU once per wave step or when the window moves; the shader blends the steps.
        int uploadedSteps = -1, uploadedI0 = -1, uploadedJ0 = -1;
        WaveField uploadedField;

        /// <summary>Largest height the texture can hold (m): the decal's amplitude.</summary>
        public float Amplitude { get; private set; }

        /// <summary>Corner of the region drawn last (simulation X, Y).</summary>
        public Vector2 Corner { get; private set; }

        SeaSurfaceRenderer(ComputeShader shader, Material template)
        {
            this.shader = shader;
            kernel = shader.FindKernel("Heights");
            heights = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RHalf)
            {
                name = "Sea heights (simulation)",
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            heights.Create();
            componentBuffer = new ComputeBuffer(Components, 16);
            depthTexture = new Texture2D(DepthNodes, DepthNodes, TextureFormat.RFloat, false, true)
            {
                name = "Sea depths (simulation)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            material = new Material(template) { name = "Sea heights decal" };
            material.SetTexture("_Deformation_Texture", heights);
            material.SetFloat("_Remap_Min", 0f);
            material.SetFloat("_Remap_Max", 1f);
            decalObject = new GameObject("Sea surface (simulation)");
            decal = decalObject.AddComponent<WaterDecal>();
            decal.scaleMode = DecalScaleMode.ScaleInvariant;
            decal.regionSize = new Vector2(Size, Size);
            decal.resolution = new Vector2Int(Resolution, Resolution);
            decal.updateMode = CustomRenderTextureUpdateMode.Realtime;
            decal.material = material;
        }

        /// <summary>The renderer, or null where compute shaders or the decal material are missing (logic tests, old GPUs).</summary>
        public static SeaSurfaceRenderer TryCreate()
        {
            if (!SystemInfo.supportsComputeShaders) return null;
            var shader = Resources.Load<ComputeShader>("SeaSurfaceHeights");
            var template = Resources.Load<Material>("SeaHeightsDecal");
            if (shader == null || template == null) return null;
            return new SeaSurfaceRenderer(shader, template);
        }

        /// <summary>
        /// HDRP's spectrum for the match: wind and direction fixed (changing them would rebuild the spectrum). The weather
        /// controller keeps writing wind, chaos and ripples; the bands' weights follow DecalsSupported each frame.
        /// </summary>
        public static void ConfigureSurface(WaterSurface surface, MarineState marine)
        {
            if (surface == null || marine == null) return;
            marine.Wind.SynopticWind(out var ux, out var uy);
            surface.largeOrientationValue = Mathf.Atan2((float)uy, (float)ux) * Mathf.Rad2Deg;
            UpdateBands(surface);
        }

        /// <summary>
        /// Whether the profile draws water decals: every profile does, so the simulation's waves are drawn on all of them
        /// (WaterDetail keeps the decorative decals from High on). False only for an asset that turns them off.
        /// </summary>
        public static bool DecalsSupported =>
            GraphicsSettingsAsset != null && GraphicsSettingsAsset.currentPlatformRenderPipelineSettings.supportWaterDecals;

        static HDRenderPipelineAsset GraphicsSettingsAsset => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;

        /// <summary>
        /// With decals the large band is off (the decal draws the simulation's waves) and the medium band a third, as texture.
        /// Without them (an asset that turns them off) HDRP's own bands stand in, from the match's wind and direction: the sea
        /// looks as rough, but its crests are not the ones the hulls ride.
        /// </summary>
        public static void UpdateBands(WaterSurface surface)
        {
            if (surface == null) return;
            var decals = DecalsSupported;
            surface.largeBand0Multiplier = decals ? 0f : 1f;
            surface.largeBand1Multiplier = decals ? MediumBand : 1f;
        }

        /// <summary>The wind speed for HDRP's bands (km/h), from the match's mean wind.</summary>
        public static float WindKilometresPerHour(MarineState marine) => (float)(marine.Climate.MeanWind * 3.6);

        public void Render(MarineState marine, Vector3 anchor, double time, double blend)
        {
            if (marine == null) return;
            var drawn = DecalsSupported;
            if (decalObject.activeSelf != drawn) decalObject.SetActive(drawn);
            if (!drawn) return;
            var waves = marine.Waves;
            var grid = waves.Grid;
            var texel = Size / Resolution;
            var corner = new Vector2(Mathf.Round((anchor.x - Size / 2) / texel) * texel, Mathf.Round((anchor.z - Size / 2) / texel) * texel);
            Corner = corner;
            decalObject.transform.SetPositionAndRotation(new Vector3(corner.x + Size / 2, 0f, corner.y + Size / 2), Quaternion.identity);

            if (marine.Depths != depthsUsed) UploadDepths(marine.Depths);

            // The wave cells under the region and one more around it: a square window.
            var width = Math.Min(Math.Min(grid.NX, grid.NY), (int)Math.Ceiling(Size / grid.CellSize) + 2);
            var i0 = Math.Max(0, Math.Min(grid.NX - width, (int)Math.Floor((corner.x - grid.OriginX) / grid.CellSize - 0.5)));
            var j0 = Math.Max(0, Math.Min(grid.NY - width, (int)Math.Floor((corner.y - grid.OriginY) / grid.CellSize - 0.5)));
            if (width != windowWidth)
            {
                windowWidth = width;
                previousBuffer?.Release();
                currentBuffer?.Release();
                waterBuffer?.Release();
                previousBuffer = new ComputeBuffer(width * width * Components, 4);
                currentBuffer = new ComputeBuffer(width * width * Components, 4);
                waterBuffer = new ComputeBuffer(width * width, 4);
                previousData = new float[width * width * Components];
                currentData = new float[width * width * Components];
                waterData = new float[width * width];
                uploadedSteps = -1;
            }
            if (waves != uploadedField || waves.Steps != uploadedSteps || i0 != uploadedI0 || j0 != uploadedJ0)
            {
                uploadedField = waves;
                uploadedSteps = waves.Steps;
                uploadedI0 = i0;
                uploadedJ0 = j0;
                Array.Clear(largest, 0, Components);
                var cells = waves.Cells;
                for (var j = 0; j < width; j++)
                    for (var i = 0; i < width; i++)
                    {
                        var cell = (j0 + j) * grid.NX + i0 + i;
                        var w = j * width + i;
                        waterData[w] = grid.Land[cell] ? 0f : 1f;
                        for (var c = 0; c < Components; c++)
                        {
                            var index = c * cells + cell;
                            var before = waves.Previous[index];
                            var now = waves.Energy[index];
                            previousData[w * Components + c] = before;
                            currentData[w * Components + c] = now;
                            var top = Math.Max(before, now);
                            if (top > largest[c]) largest[c] = top;
                        }
                    }
                previousBuffer.SetData(previousData);
                currentBuffer.SetData(currentData);
                waterBuffer.SetData(waterData);
                double bound = 0;
                for (var c = 0; c < Components; c++) bound += Math.Sqrt(2 * Math.Max(0f, largest[c]));
                Amplitude = Mathf.Max(0.01f, (float)bound);
            }
            var surface = marine.Surface;
            for (var c = 0; c < Components; c++)
            {
                // Phase folded in double precision at the region's corner: θ = k·local + (k·C − ωt + φ).
                var phase = surface.WaveNumberX[c] * corner.x + surface.WaveNumberY[c] * corner.y - surface.Omega[c] * time + surface.Phase[c];
                phase -= Math.Floor(phase / (2 * Math.PI)) * 2 * Math.PI;
                componentData[4 * c] = (float)surface.WaveNumberX[c];
                componentData[4 * c + 1] = (float)surface.WaveNumberY[c];
                componentData[4 * c + 2] = (float)phase;
                componentData[4 * c + 3] = 0f;
            }
            decal.amplitude = Amplitude;

            componentBuffer.SetData(componentData);
            shader.SetTexture(kernel, "_Heights", heights);
            shader.SetBuffer(kernel, "_Components", componentBuffer);
            shader.SetBuffer(kernel, "_Previous", previousBuffer);
            shader.SetBuffer(kernel, "_Current", currentBuffer);
            shader.SetBuffer(kernel, "_Water", waterBuffer);
            shader.SetTexture(kernel, "_Depth", depthTexture);
            shader.SetVector("_Corner", new Vector4(corner.x, corner.y, texel, 1f / Amplitude));
            shader.SetVector("_Window", new Vector4((float)(grid.OriginX + (i0 + 0.5) * grid.CellSize), (float)(grid.OriginY + (j0 + 0.5) * grid.CellSize), (float)grid.CellSize, width));
            shader.SetVector("_DepthMap", new Vector4(DepthOrigin, DepthOrigin, 1f / (DepthSpacing * (DepthNodes - 1)), 0f));
            shader.SetVector("_Fade", new Vector4(Size, EdgeFade, (float)blend, 0f));
            shader.Dispatch(kernel, Resolution / 8, Resolution / 8, 1);
        }

        void UploadDepths(Bathymetry depths)
        {
            depthsUsed = depths;
            var values = new float[DepthNodes * DepthNodes];
            for (var j = 0; j < DepthNodes; j++)
                for (var i = 0; i < DepthNodes; i++)
                    values[j * DepthNodes + i] = (float)(depths?.At(DepthOrigin + i * DepthSpacing, DepthOrigin + j * DepthSpacing) ?? SeaSurface.ReferenceDepth);
            depthTexture.SetPixelData(values, 0);
            depthTexture.Apply(false, false);
        }

        public void Dispose()
        {
            componentBuffer?.Release();
            previousBuffer?.Release();
            currentBuffer?.Release();
            waterBuffer?.Release();
            if (heights != null) heights.Release();
            if (decalObject != null) UnityEngine.Object.Destroy(decalObject);
            if (material != null) UnityEngine.Object.Destroy(material);
            if (depthTexture != null) UnityEngine.Object.Destroy(depthTexture);
        }
    }
}
