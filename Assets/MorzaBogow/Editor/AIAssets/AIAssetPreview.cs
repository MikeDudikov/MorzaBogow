using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MorzaBogow.Editor.AIAssets
{
    /// <summary>
    /// A 512 px PNG of a prefab from a high oblique angle, like the strategy camera, rendered with the editor's preview
    /// scene through the active render pipeline. Needs a graphics device: batch mode without -nographics.
    /// </summary>
    public static class AIAssetPreview
    {
        /// <summary>Objects named so in a group are drawn round the subject but not framed (RenderGroup).</summary>
        public const string Backdrop = "Backdrop";

        public static bool Render(GameObject prefab, string pngPath, out string why) => Render(prefab, pngPath, false, out why, -140f);

        /// <summary>Renders an object that lives in the open scene (not a prefab asset); it is moved into the preview scene and back.</summary>
        public static bool RenderObject(GameObject item, string pngPath, float yaw, out string why) => Render(item, pngPath, true, out why, yaw);

        /// <summary>
        /// A group of objects in one picture under the same light: orthographic from the strategy camera's height and angle
        /// (levels of detail chosen as the game would choose them), or in perspective for a close look (everything at full
        /// detail). The frame is fitted to the group.
        /// </summary>
        public static bool RenderGroup(GameObject group, string pngPath, float pitch, float yaw, bool orthographic, int width, int height, out string why)
        {
            why = string.Empty;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { why = "brak urządzenia graficznego (-nographics)"; return false; }
            PreviewRenderUtility preview = null;
            Texture2D image = null;
            try
            {
                preview = new PreviewRenderUtility();
                preview.AddSingleGO(group);
                // A backdrop (ground round the subject) is drawn but left out of the frame.
                var renderers = group.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.StartsWith(Backdrop)).ToArray();
                if (!orthographic)
                    foreach (var lods in group.GetComponentsInChildren<LODGroup>(true)) lods.ForceLOD(0);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
                var camera = preview.camera;
                var direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                var distance = orthographic ? radius * 4f : radius / Mathf.Sin(15f * Mathf.Deg2Rad) * 0.42f;
                camera.orthographic = orthographic;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.62f;
                camera.fieldOfView = 30f;
                camera.transform.position = bounds.center - direction * distance;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = distance + radius * 3f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.36f, 0.48f, 1f);
                // A bright afternoon: the sun over the camera's shoulder and a strong sky fill, the same for every ship.
                if (preview.lights.Length > 0)
                {
                    preview.lights[0].intensity = 2.2f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(50f, yaw + 25f, 0f);
                }
                if (preview.lights.Length > 1) preview.lights[1].intensity = 0.9f;
                preview.ambientColor = new Color(0.5f, 0.52f, 0.56f);
                preview.BeginStaticPreview(new Rect(0, 0, width, height));
                preview.Render(true);
                image = preview.EndStaticPreview();
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath) ?? ".");
                File.WriteAllBytes(pngPath, image.EncodeToPNG());
                return true;
            }
            catch (Exception error)
            {
                why = $"{error.GetType().Name}: {error.Message}";
                return false;
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                preview?.Cleanup();
            }
        }

        static bool Render(GameObject source, string pngPath, bool existing, out string why, float yaw)
        {
            why = string.Empty;
            if (string.IsNullOrEmpty(pngPath)) { why = "brak ścieżki podglądu"; return false; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { why = "brak urządzenia graficznego (-nographics)"; return false; }
            PreviewRenderUtility preview = null;
            Texture2D image = null;
            try
            {
                preview = new PreviewRenderUtility();
                GameObject instance;
                if (existing)
                {
                    preview.AddSingleGO(source);
                    instance = source;
                }
                else instance = preview.InstantiatePrefabInScene(source);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var lodGroup = instance.GetComponentInChildren<LODGroup>(true);
                if (lodGroup != null) lodGroup.ForceLOD(0);
                var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one);
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
                var camera = preview.camera;
                camera.fieldOfView = 30f;
                var direction = Quaternion.Euler(36f, yaw, 0f) * Vector3.forward;
                var distance = radius / Mathf.Sin(15f * Mathf.Deg2Rad) * 0.72f;
                camera.transform.position = bounds.center - direction * distance;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = Mathf.Max(0.05f, distance - radius * 2f);
                camera.farClipPlane = distance + radius * 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.52f, 0.64f, 0.72f, 1f);
                if (preview.lights.Length > 0)
                {
                    // Key light from behind the camera's shoulder, so the faces the camera sees are lit.
                    preview.lights[0].intensity = 1.3f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(50f, yaw + 25f, 0f);
                }
                if (preview.lights.Length > 1) preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.32f, 0.34f, 0.38f);
                preview.BeginStaticPreview(new Rect(0, 0, 512, 512));
                preview.Render(true);
                image = preview.EndStaticPreview();
                var pixels = image.GetPixels32();
                var light = pixels.Average(p => (p.r + p.g + p.b) / 3.0);
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath) ?? ".");
                File.WriteAllBytes(pngPath, image.EncodeToPNG());
                if (light < 2.0) { why = "podgląd jest czarny"; return false; }
                return true;
            }
            catch (Exception error)
            {
                why = $"{error.GetType().Name}: {error.Message}";
                return false;
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                preview?.Cleanup();
            }
        }
    }
}
