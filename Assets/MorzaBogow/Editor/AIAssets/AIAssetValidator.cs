using System;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MorzaBogow.Editor.AIAssets
{
    /// <summary>
    /// Measures a generated prefab: levels of detail and their triangles, materials (HDRP only, no empty slots),
    /// textures and their imported sizes, UVs and normals, physics components, mirrored transforms, bounds in the
    /// prefab's space, texel density of LOD0, and for ships the class's points, weapon posts, bow direction and team
    /// colour. It reports; the pipeline applies the budget (Tools/asset_pipeline/validation/report.py).
    /// </summary>
    public static class AIAssetValidator
    {
        static readonly string[] MapProperties = { "_BaseColorMap", "_NormalMap", "_MaskMap", "_EmissiveColorMap" };

        /// <summary>Loads the prefab's contents, measures them and renders the preview.</summary>
        public static AIAssetReport ValidatePrefab(string prefabPath, AIAssetRequest request)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) throw new InvalidOperationException($"Brak prefabu {prefabPath}.");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            AIAssetReport report;
            try
            {
                report = Measure(root, request);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            report.prefab = prefabPath;
            var models = (request.lods ?? Array.Empty<LodFile>()).Length;
            report.meshLodsGenerated = models == 1 && !request.rigged;
            if (AIAssetPreview.Render(asset, request.previewPath, out var why)) report.preview = request.previewPath;
            else
            {
                report.preview = string.Empty;
                report.checks = report.checks.Append(new Check("preview", false, "warning", why)).ToArray();
            }
            return report;
        }

        public static AIAssetReport Measure(GameObject root, AIAssetRequest request)
        {
            var checks = new List<Check>();
            var report = new AIAssetReport
            {
                assetId = request.assetId,
                category = request.category,
                shipClass = request.shipClass ?? string.Empty,
                forwardChecked = request.forwardChecked,
            };
            var missing = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            checks.Add(new Check("scripts.missing", missing == 0, "error", $"{missing} brakujących skryptów"));

            var group = root.GetComponentInChildren<LODGroup>(true);
            var lods = group != null ? group.GetLODs() : Array.Empty<LOD>();
            checks.Add(new Check("lodgroup", group != null && lods.Length > 0 && lods.All(l => l.renderers.Any(r => r != null)), "error",
                group == null ? "brak LODGroup" : $"{lods.Length} poziomów"));
            report.lods = lods.Select((lod, i) => new LodInfo
            {
                level = i,
                triangles = Triangles(lod.renderers),
                renderers = lod.renderers.Count(r => r != null),
                screenRelativeHeight = lod.screenRelativeTransitionHeight,
            }).ToArray();
            if (group != null)
                checks.Add(new Check("lodgroup.visual", group.transform.parent == root.transform && group.name == "Visual", "error", "LODGroup ma być na korzeniu wizualnym 'Visual'"));

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            report.rendererCount = renderers.Length;
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
            report.materialCount = materials.Count;
            var other = materials.Where(m => !HdrpMaterialConverter.IsHdrp(m)).Select(m => $"{m.name} ({m.shader.name})").ToList();
            checks.Add(new Check("materials.hdrp", other.Count == 0, "error", string.Join(", ", other)));
            checks.Add(new Check("materials.slots", renderers.All(r => r.sharedMaterials.All(m => m != null)), "error", "pusty slot materiału"));
            report.materials = materials.Select(m => new MaterialInfo
            {
                name = m.name,
                shader = m.shader.name,
                baseMap = Has(m, "_BaseColorMap"),
                normalMap = Has(m, "_NormalMap"),
                maskMap = Has(m, "_MaskMap"),
            }).ToArray();
            var textures = materials.SelectMany(m => MapProperties.Where(m.HasProperty).Select(m.GetTexture)).Where(t => t != null).Distinct().ToList();
            report.textures = textures.Select(Describe).ToArray();
            foreach (var material in materials.Where(m => Has(m, "_NormalMap")))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture("_NormalMap"))) as TextureImporter;
                if (importer != null)
                    checks.Add(new Check($"textures.normal.{material.name}", importer.textureType == TextureImporterType.NormalMap, "error", "mapa normalnych nie jest typu Normal map"));
            }

            var meshes = renderers.Select(MeshOf).Where(m => m != null).Distinct().ToList();
            var textured = report.materials.Any(m => m.baseMap);
            checks.Add(new Check("mesh.uv", !textured || meshes.All(m => m.HasVertexAttribute(VertexAttribute.TexCoord0)), "error", "siatka bez UV przy materiale z teksturą"));
            checks.Add(new Check("mesh.normals", meshes.All(m => m.HasVertexAttribute(VertexAttribute.Normal)), "error", "siatka bez normalnych"));

            var physics = root.GetComponentsInChildren<Collider>(true).Length + root.GetComponentsInChildren<Rigidbody>(true).Length;
            checks.Add(new Check("physics", physics == 0, request.category == "ship" ? "error" : "warning", $"{physics} komponentów fizyki; ruch i trafienia liczy symulacja"));
            var mirrored = root.GetComponentsInChildren<Transform>(true).Where(t => t.localScale.x * t.localScale.y * t.localScale.z < 0f).Select(t => t.name).ToList();
            checks.Add(new Check("transform.mirrored", mirrored.Count == 0, "warning", string.Join(", ", mirrored)));

            var lod0 = lods.Length > 0 ? lods[0].renderers.Where(r => r != null).ToList() : renderers.ToList();
            var bounds = ShipPrefabBuilder.BoundsOf(root.transform, lod0);
            report.bounds = new BoundsInfo { min = Vec(bounds.min), max = Vec(bounds.max), size = Vec(bounds.size) };
            var baseMap = materials.Select(m => Has(m, "_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null).FirstOrDefault(t => t != null);
            report.texelDensity = baseMap != null ? TexelDensity(root.transform, lod0, Mathf.Max(baseMap.width, baseMap.height)) : 0f;

            if (request.category == "ship") ShipChecks(root, request, checks);
            report.checks = checks.ToArray();
            return report;
        }

        static void ShipChecks(GameObject root, AIAssetRequest request, List<Check> checks)
        {
            var ship = root.GetComponent<ShipModel>();
            checks.Add(new Check("ship.model", ship != null, "error", "brak ShipModel na korzeniu logicznym"));
            if (ship == null) return;
            var posts = Enum.TryParse<ShipClass>(request.shipClass, out var shipClass) ? ShipCatalog.Get(shipClass).Slots.Length : -1;
            checks.Add(new Check("ship.points", ship.Bow && ship.Stern && ship.Port && ship.Starboard && ship.Center, "error", "punkty próbkowania wody"));
            checks.Add(new Check("ship.mounts", ship.Mounts.Length == posts && ship.Muzzles.Length == posts && ship.Mounts.All(m => m) && ship.Muzzles.All(m => m),
                "error", $"{ship.Mounts.Length}/{posts} stanowisk broni"));
            checks.Add(new Check("ship.forward", ship.Bow && ship.Stern && ship.Bow.localPosition.z > ship.Stern.localPosition.z, "error", "dziób w +Z"));
            checks.Add(new Check("ship.teamColour", ship.TeamColourRenderers.Length > 0, "warning", "brak materiału w kolorze strony (żagiel)"));
        }

        static bool Has(Material material, string property) => material.HasProperty(property) && material.GetTexture(property) != null;

        static Mesh MeshOf(Renderer renderer) =>
            renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
            : renderer != null && renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;

        public static int Triangles(IEnumerable<Renderer> renderers)
        {
            var total = 0;
            foreach (var mesh in renderers.Where(r => r != null).Select(MeshOf).Where(m => m != null))
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                    if (mesh.GetTopology(sub) == MeshTopology.Triangles) total += (int)(mesh.GetIndexCount(sub) / 3);
            return total;
        }

        static TextureInfo Describe(Texture texture)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            return new TextureInfo
            {
                path = AssetDatabase.GetAssetPath(texture),
                maxSize = Mathf.Max(texture.width, texture.height),
                sRGB = importer == null || importer.sRGBTexture,
                type = importer != null ? importer.textureType.ToString() : "memory",
            };
        }

        static float[] Vec(Vector3 v) => new[] { (float)Math.Round(v.x, 4), (float)Math.Round(v.y, 4), (float)Math.Round(v.z, 4) };

        /// <summary>Texels per metre on LOD0: sqrt(UV area x texture size² / surface area).</summary>
        public static float TexelDensity(Transform space, IEnumerable<Renderer> renderers, int textureSize)
        {
            double world = 0, uv = 0;
            try
            {
                foreach (var renderer in renderers)
                {
                    var mesh = MeshOf(renderer);
                    if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) continue;
                    var matrix = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    var vertices = mesh.vertices;
                    var uvs = mesh.uv;
                    var triangles = mesh.triangles;
                    for (var i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        var a = matrix.MultiplyPoint3x4(vertices[triangles[i]]);
                        var b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                        var c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                        world += Vector3.Cross(b - a, c - a).magnitude * 0.5;
                        var p = uvs[triangles[i]];
                        var q = uvs[triangles[i + 1]];
                        var r = uvs[triangles[i + 2]];
                        uv += Mathf.Abs((q.x - p.x) * (r.y - p.y) - (r.x - p.x) * (q.y - p.y)) * 0.5;
                    }
                }
            }
            catch (UnityException)
            {
                return 0f; // mesh data not readable here
            }
            return world > 1e-6 ? (float)(Math.Sqrt(uv / world) * textureSize) : 0f;
        }
    }
}
