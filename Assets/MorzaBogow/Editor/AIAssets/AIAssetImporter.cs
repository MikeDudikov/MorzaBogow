using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace MorzaBogow.Editor.AIAssets
{
    /// <summary>
    /// Brings a pipeline result into the project: copies the model levels, textures and clips under
    /// Assets/MorzaBogow/Art/Generated/&lt;assetId&gt;/, sets the importers (metres, no cameras, lights or colliders,
    /// sRGB only for colour maps, normal maps as normal maps, the size budget), composes HDRP's mask map from separate
    /// metal, roughness and occlusion maps, makes one HDRP/Lit material per source material and remaps the models to
    /// them, and saves a prefab with a logical root (GeneratedAsset, ShipModel for ships) and a visual root (LODGroup).
    /// Writes nothing outside that folder.
    /// </summary>
    public static class AIAssetImporter
    {
        public const string Root = "Assets/MorzaBogow/Art/Generated";
        static readonly Regex Id = new Regex("^[a-z][a-z0-9_]{2,47}$");

        /// <summary>Refuses requests that would write outside the generated folder or miss the model.</summary>
        public static void Check(AIAssetRequest request)
        {
            if (request == null) throw new ArgumentException("Brak zlecenia importu.");
            if (string.IsNullOrEmpty(request.assetId) || !Id.IsMatch(request.assetId)) throw new ArgumentException($"Niepoprawny assetId '{request.assetId}'.");
            if (request.outputFolder != $"{Root}/{request.assetId}") throw new ArgumentException($"Folder wyjściowy musi być {Root}/{request.assetId}, nie '{request.outputFolder}'.");
            if (request.lods == null || request.lods.Length == 0) throw new ArgumentException("Zlecenie bez plików modelu.");
            foreach (var lod in request.lods)
                if (!File.Exists(lod.file)) throw new FileNotFoundException("Brak pliku modelu", lod.file);
            if (request.category == "ship" && !Enum.TryParse<ShipClass>(request.shipClass, out _)) throw new ArgumentException($"Nieznana klasa okrętu '{request.shipClass}'.");
        }

        public static string Import(AIAssetRequest request)
        {
            Check(request);
            var folder = request.outputFolder;
            var written = new List<string>();
            var models = request.lods.Select(lod => CopyIn(lod.file, $"{folder}/Source", written)).ToList();
            var textures = (request.textures ?? Array.Empty<TextureFile>()).Select(t => (t.role, path: CopyIn(t.file, $"{folder}/Textures", written))).ToList();
            var clips = (request.animations ?? Array.Empty<string>()).Select(file => CopyIn(file, $"{folder}/Animations", written)).ToList();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var model in models) ConfigureModel(model, request, models.Count == 1, false);
            foreach (var clip in clips) ConfigureModel(clip, request, false, true);
            foreach (var (role, path) in textures) ConfigureTexture(path, role, request.textureSize);
            var maps = new Dictionary<string, Texture2D>();
            foreach (var (role, path) in textures)
                if (!maps.ContainsKey(role)) maps[role] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var mask = MaskFor(maps, $"{folder}/Textures", request);
            var materials = BuildMaterials(models, request, maps, mask, $"{folder}/Materials");
            foreach (var model in models) Remap(model, materials);

            var prefabPath = $"{folder}/{request.assetId}.prefab";
            var root = Assemble(request, models, clips);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            written.AddRange(materials.Values.Select(AssetDatabase.GetAssetPath));
            if (mask != null) written.Add(AssetDatabase.GetAssetPath(mask));
            written.Add(ControllerPath(request));
            var removed = RemoveStale(folder, written);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AIAssets] {request.assetId}: prefab {prefabPath} ({models.Count} LOD, {materials.Count} materiałów, {textures.Count} tekstur"
                      + (removed.Count > 0 ? $"; usunięto nieaktualne: {string.Join(", ", removed)})" : ")"));
            return prefabPath;
        }

        static string FullPath(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        static string ControllerPath(AIAssetRequest request) => $"{request.outputFolder}/Animations/{request.assetId}.controller";

        /// <summary>Copies a file into an asset folder (with the MTL files an OBJ names), records what it wrote, and returns the file's asset path.</summary>
        static string CopyIn(string file, string folder, List<string> written)
        {
            Directory.CreateDirectory(FullPath(folder));
            var target = $"{folder}/{Path.GetFileName(file)}";
            File.Copy(file, FullPath(target), true);
            written.Add(target);
            if (file.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in File.ReadLines(file).Where(l => l.StartsWith("mtllib ")))
                {
                    var mtl = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, line.Substring(7).Trim());
                    if (!File.Exists(mtl)) continue;
                    var copy = $"{folder}/{Path.GetFileName(mtl)}";
                    File.Copy(mtl, FullPath(copy), true);
                    written.Add(copy);
                }
            }
            return target;
        }

        /// <summary>
        /// Deletes files an earlier import of this asset left in its own Source, Textures, Materials and Animations
        /// folders and this import no longer uses (a material the new model does not name, a replaced model file).
        /// The asset's folder and prefab stay, so references to the prefab keep its GUID.
        /// </summary>
        static List<string> RemoveStale(string folder, IEnumerable<string> keep)
        {
            var kept = new HashSet<string>(keep.Where(p => !string.IsNullOrEmpty(p)), StringComparer.OrdinalIgnoreCase);
            var removed = new List<string>();
            foreach (var sub in new[] { "Source", "Textures", "Materials", "Animations" })
            {
                var path = $"{folder}/{sub}";
                if (!AssetDatabase.IsValidFolder(path)) continue;
                foreach (var asset in AssetDatabase.FindAssets(string.Empty, new[] { path }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList())
                {
                    if (AssetDatabase.IsValidFolder(asset) || kept.Contains(asset)) continue;
                    if (AssetDatabase.DeleteAsset(asset)) removed.Add(asset.Substring(folder.Length + 1));
                }
            }
            return removed;
        }

        static void ConfigureModel(string path, AIAssetRequest request, bool single, bool clip)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            // Material remaps of an earlier import are dropped: the model names its materials again.
            foreach (var key in importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
                importer.RemoveRemap(key);
            importer.globalScale = 1f;
            importer.useFileScale = !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase);
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = clip ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            if (request.rigged)
            {
                importer.animationType = request.humanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
                importer.importAnimation = clip;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
            // Without a LOD chain from Blender, Unity's own mesh LODs lower the detail at a distance.
            importer.generateMeshLods = single && !request.rigged && !clip;
            importer.SaveAndReimport();
        }

        static void ConfigureTexture(string path, string role, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = role == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = role == "base_color" || role == "emission";
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>The given mask map, or one composed from metal, occlusion and roughness maps and saved beside them.</summary>
        static Texture2D MaskFor(Dictionary<string, Texture2D> maps, string folder, AIAssetRequest request)
        {
            if (maps.TryGetValue("mask", out var given)) return given;
            maps.TryGetValue("metallic", out var metal);
            maps.TryGetValue("occlusion", out var occlusion);
            maps.TryGetValue("roughness", out var roughness);
            if (metal == null && occlusion == null && roughness == null) return null;
            var mask = HdrpMaterialConverter.ComposeMask(metal, occlusion, roughness, null, 0.5f, $"{request.assetId}_MaskMap");
            var path = $"{folder}/{mask.name}.png";
            Directory.CreateDirectory(FullPath(folder));
            File.WriteAllBytes(FullPath(path), mask.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(path, "mask", request.textureSize);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Material names the freshly imported models use (embedded materials and renderer slots), in first-seen order.</summary>
        static List<string> SourceMaterialNames(IEnumerable<string> models)
        {
            var names = new List<string>();
            foreach (var path in models)
            {
                names.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name));
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    names.AddRange(renderer.sharedMaterials.Where(m => m != null).Select(m => m.name));
            }
            return names.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        }

        static Dictionary<string, Material> BuildMaterials(List<string> models, AIAssetRequest request, Dictionary<string, Texture2D> maps, Texture2D mask, string folder)
        {
            Directory.CreateDirectory(FullPath(folder));
            AssetDatabase.ImportAsset(folder);
            var names = SourceMaterialNames(models);
            if (names.Count == 0) names.Add(request.assetId);
            maps.TryGetValue("base_color", out var baseMap);
            maps.TryGetValue("normal", out var normalMap);
            maps.TryGetValue("emission", out var emissionMap);
            var materials = new Dictionary<string, Material>();
            foreach (var name in names)
            {
                var file = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
                var path = $"{folder}/{file}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("HDRP/Lit")) { name = file };
                    AssetDatabase.CreateAsset(material, path);
                }
                else material.shader = Shader.Find("HDRP/Lit");
                var flat = (request.materials ?? Array.Empty<FlatMaterial>()).FirstOrDefault(m => string.Equals(m.name, name, StringComparison.OrdinalIgnoreCase));
                material.SetColor("_BaseColor", baseMap != null ? request.MapTint : flat != null ? ColorOf(flat.baseColor) : new Color(0.7f, 0.7f, 0.7f));
                material.SetTexture("_BaseColorMap", baseMap);
                material.SetTexture("_NormalMap", normalMap);
                if (normalMap != null) material.SetFloat("_NormalScale", 1f);
                material.SetTexture("_MaskMap", mask);
                if (mask != null)
                {
                    material.SetFloat("_Metallic", 1f);
                    material.SetFloat("_SmoothnessRemapMin", 0f);
                    material.SetFloat("_SmoothnessRemapMax", 1f);
                }
                else
                {
                    material.SetFloat("_Metallic", flat != null ? flat.metallic : 0f);
                    material.SetFloat("_Smoothness", flat != null ? flat.smoothness : 0.5f);
                }
                if (emissionMap != null)
                {
                    material.SetTexture("_EmissiveColorMap", emissionMap);
                    material.SetColor("_EmissiveColor", Color.white);
                }
                HDShaderUtils.ResetMaterialKeywords(material);
                EditorUtility.SetDirty(material);
                materials[name] = material;
            }
            AssetDatabase.SaveAssets();
            return materials;
        }

        static Color ColorOf(float[] rgb) => rgb != null && rgb.Length >= 3 ? new Color(rgb[0], rgb[1], rgb[2], 1f) : Color.gray;

        static void Remap(string model, Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(model);
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }

        static GameObject Assemble(AIAssetRequest request, List<string> models, List<string> clips)
        {
            var lods = models.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToList();
            if (lods.Any(l => l == null)) throw new InvalidOperationException("Unity nie zaimportował modelu (" + string.Join(", ", models) + ").");
            var heights = request.lods.Select(l => l.screenRelativeHeight).ToList();
            var forward = ShipPrefabBuilder.Parse(request.forward);
            GameObject root;
            if (request.category == "ship")
            {
                var shipClass = (ShipClass)Enum.Parse(typeof(ShipClass), request.shipClass);
                root = ShipPrefabBuilder.Assemble(lods, heights, shipClass, forward, request.normalised, request.teamColour, request.assetId);
            }
            else root = AssembleGeneric(request, lods, heights, forward);

            var meta = root.AddComponent<GeneratedAsset>();
            meta.AssetId = request.assetId;
            meta.DisplayName = request.displayName ?? request.assetId;
            meta.Category = request.category;
            meta.ShipClass = request.shipClass ?? string.Empty;
            meta.JobId = request.jobId ?? string.Empty;
            meta.ProvenanceJson = string.IsNullOrEmpty(request.provenanceJson) ? "{}" : request.provenanceJson;
            meta.Visual = root.transform.Find("Visual");

            if (request.rigged && clips.Count > 0) AddAnimator(request, root, clips);
            return root;
        }

        /// <summary>Any asset other than a ship: turned to +Z, scaled to the brief's size, standing on Y = 0, centred.</summary>
        public static GameObject AssembleGeneric(AIAssetRequest request, IList<GameObject> lods, IList<float> heights, ModelForward forward)
        {
            var root = new GameObject(request.assetId);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var holder = new GameObject("Model").transform;
            holder.SetParent(visual, false);
            var copies = lods.Select((lod, i) => ShipPrefabBuilder.Copy(lod, holder, $"LOD{i}")).ToList();
            if (!request.normalised)
            {
                holder.localRotation = ShipPrefabBuilder.Turn(forward == ModelForward.Auto ? ShipPrefabBuilder.AutoForward(holder, copies[0]) : forward);
                var bounds = ShipPrefabBuilder.BoundsIn(visual, copies[0]);
                var size = request.dimensions ?? new Dimensions();
                var scale = size.height > 0 ? size.height / Mathf.Max(bounds.size.y, 0.001f)
                    : size.length > 0 ? size.length / Mathf.Max(bounds.size.z, 0.001f)
                    : size.width > 0 ? size.width / Mathf.Max(bounds.size.x, 0.001f) : 1f;
                holder.localScale = Vector3.one * scale;
                bounds = ShipPrefabBuilder.BoundsIn(visual, copies[0]);
                holder.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            ShipPrefabBuilder.LodGroupFor(visual.gameObject, copies, heights);
            return root;
        }

        static void AddAnimator(AIAssetRequest request, GameObject root, List<string> clips)
        {
            var path = ControllerPath(request);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var clip in clips.SelectMany(c => AssetDatabase.LoadAllAssetsAtPath(c).OfType<AnimationClip>()).Where(c => !c.name.StartsWith("__preview__")))
                if (controller.layers[0].stateMachine.states.All(s => s.state.motion != clip)) controller.AddMotion(clip);
            var model = root.transform.Find("Visual/Model/LOD0");
            if (model == null) return;
            var animator = model.GetComponent<Animator>() ?? model.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
        }
    }
}
