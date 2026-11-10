using System.IO;
using System.Linq;
using MorzaBogow.Presentation.Landscape;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// Builds the environment's assets from code and CC0 sources (Assets/Art/PolyHaven/ZRODLA_CC0.txt):
    /// <list type="bullet">
    /// <item>the olive's leaf texture, baked by <see cref="LeafAtlas"/> (colour and coverage, normals, HDRP mask);</item>
    /// <item>its bark from the Poly Haven set bark_willow_02 (grey, furrowed bark close to an old olive's), with a mask
    /// map made from its roughness and occlusion (R metallic, G occlusion, B detail, A smoothness);</item>
    /// <item>HDRP/Lit materials kept in a Resources folder, so a build keeps the shader variants they use:
    /// opaque bark on the generator's metre UVs, and translucent leaves with alpha clipping and HDRP's foliage
    /// diffusion profile (already in the default volume profile's diffusion profile list).</item>
    /// </list>
    /// Batch: -executeMethod MorzaBogow.Editor.EnvironmentArt.BuildTrialSet -quit
    /// </summary>
    public static class EnvironmentArt
    {
        const string Source = "Assets/Art/PolyHaven/Textures";
        const string Output = "Assets/Art/Environment/Vegetation";
        const string Materials = "Assets/Art/Environment/Resources/Environment";
        const string Foliage = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/FoliageDiffusionProfile.asset";

        [MenuItem("Morza Bogów/Środowisko/Zbuduj zestaw próbny")]
        public static void BuildTrialSet()
        {
            Folders();
            var bark = Bark("OliveBark", "bark_willow_02", new Color(0.82f, 0.82f, 0.8f));
            var leaves = Leaves("OliveLeaves", TreeSpecies.OliveCultivated);
            // The maquis shrubs, each with its own leaf atlas; their bark is the olive's (barely seen in a dense bush).
            Leaves("KermesLeaves", TreeSpecies.KermesOak);
            Leaves("LentiskLeaves", TreeSpecies.Lentisk);
            // Rock samphire and sea lavender on the cliffs (CliffPlants), on the same translucent foliage material.
            var cliffPlants = FoliageMaterial("CliffPlants", CliffPlantAtlas.Bake(1024));
            AssetDatabase.SaveAssets();
            ExportManifest();
            Debug.Log($"[Environment] trial set: bark {(bark != null ? AssetDatabase.GetAssetPath(bark) : "MISSING SOURCE")}, leaves {(leaves != null ? AssetDatabase.GetAssetPath(leaves) : "FAILED")}, cliff plants {AssetDatabase.GetAssetPath(cliffPlants)}");
        }

        /// <summary>
        /// Pictures of the trial olives for a look at the result, written to artifacts-log (never into Assets): a close
        /// perspective at full detail and the strategy camera's orthographic view, still and in a 12 m/s wind.
        /// Batch mode with a graphics device (without -nographics): -executeMethod MorzaBogow.Editor.EnvironmentArt.PreviewTrial
        /// </summary>
        public static void PreviewTrial()
        {
            var folder = Path.Combine(Application.dataPath, "..", "artifacts-log");
            Shot(Scene(0, false), Path.Combine(folder, "env-olive-close.png"), 18f, 30f, false);
            Shot(Scene(0, true), Path.Combine(folder, "env-olive-close-wind.png"), 18f, 30f, false);
            Shot(Scene(1, false), Path.Combine(folder, "env-olive-lod1.png"), 45f, 30f, true);
            Shot(Scene(2, false), Path.Combine(folder, "env-olive-lod2.png"), 45f, 30f, true);
        }

        /// <summary>
        /// The maquis shrubs, three kermes oaks and three lentisks, close up still and in a 12 m/s wind and from the
        /// strategic camera at the farther levels. Batch mode with a graphics device: -executeMethod MorzaBogow.Editor.EnvironmentArt.PreviewShrubs
        /// </summary>
        public static void PreviewShrubs()
        {
            var folder = Path.Combine(Application.dataPath, "..", "artifacts-log");
            Shot(ShrubScene(0, false), Path.Combine(folder, "env-shrubs-close.png"), 18f, 30f, false);
            Shot(ShrubScene(0, true), Path.Combine(folder, "env-shrubs-close-wind.png"), 18f, 30f, false);
            Shot(ShrubScene(1, false), Path.Combine(folder, "env-shrubs-lod1.png"), 45f, 30f, true);
            Shot(ShrubScene(2, false), Path.Combine(folder, "env-shrubs-lod2.png"), 45f, 30f, true);
        }

        static GameObject ShrubScene(int lod, bool wind)
        {
            var root = new GameObject("Shrub preview");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            for (var kind = 0; kind < 2; kind++)
            {
                var species = kind == 0 ? TreeSpecies.KermesOak : TreeSpecies.Lentisk;
                var builder = new TreeGroveBuilder();
                for (var i = 0; i < 3; i++) builder.Add(species, (uint)(21 + 10 * kind + i), new Vector3(-3f + 3f * i, 0f, kind == 0 ? 2f : -2f), 70f * i);
                var grove = builder.Build(root.transform, species.Name, LandscapeArt.OliveBark, LandscapeArt.LeavesFor(species));
                if (wind)
                {
                    grove.Wind = new SteadyWind(new Vector3(12f, 0f, 0f));
                    for (var i = 0; i < 90; i++) grove.Advance(1f / 30f, true);
                }
                var source = grove.Renderers[lod];
                var baked = new Mesh { indexFormat = source.sharedMesh.indexFormat };
                source.BakeMesh(baked);
                var drawn = new GameObject($"{species.Name} LOD{lod} baked");
                drawn.transform.SetParent(root.transform, false);
                drawn.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                drawn.AddComponent<MeshFilter>().sharedMesh = baked;
                drawn.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                Object.DestroyImmediate(grove.gameObject);
            }
            return root;
        }

        /// <summary>An islet with the game's shore materials, its notch, wet band and beachrock, over a flat blue sea.</summary>
        public static void PreviewCoast()
        {
            var folder = Path.Combine(Application.dataPath, "..", "artifacts-log");
            Shot(Islet(), Path.Combine(folder, "env-coast-cliff.png"), 14f, 180f, false);
            Shot(Islet(), Path.Combine(folder, "env-coast-gentle.png"), 22f, 0f, false);
            Shot(Islet(), Path.Combine(folder, "env-coast-strategic.png"), 45f, 30f, true);
        }

        /// <summary>
        /// A close look at the cliff plants: a few metres of the preview islet's cliff face cut out around its densest
        /// clumps (the whole islet would leave them a few pixels), seen from the sea and from above.
        /// Batch mode with a graphics device: -executeMethod MorzaBogow.Editor.EnvironmentArt.PreviewCliffPlants
        /// </summary>
        public static void PreviewCliffPlants()
        {
            var folder = Path.Combine(Application.dataPath, "..", "artifacts-log");
            Shot(CliffCut(), Path.Combine(folder, "env-cliff-plants-face.png"), 8f, 0f, false);
            Shot(CliffCut(), Path.Combine(folder, "env-cliff-plants-above.png"), 40f, 0f, false);
        }

        static GameObject CliffCut()
        {
            var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Limestone.mat");
            var meadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Meadow.mat");
            var outline = MorzaBogow.Presentation.ShoreShape.Ellipse(40f, 30f);
            var lift = MorzaBogow.Presentation.ShoreShape.Cliffs(0f, 60f, 110f, 1.3f);
            var made = CoastFeatures.Make(outline, lift, 7);
            // The cliff clump with the most others within 3 m: the window is 3 m around it.
            var clumps = made.Plants.Clumps.FindAll(clump => clump.OnCliff);
            var centre = clumps.Count == 0 ? Vector3.zero : clumps.OrderByDescending(c => clumps.Count(o => (o.Position - c.Position).sqrMagnitude < 9f)).First().Position;
            var box = new Bounds(centre, new Vector3(3f, 3f, 3f));
            var root = new GameObject("Cliff plants preview");
            // Turned so the face looks along +z, toward a camera at yaw 0.
            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            face.transform.rotation = Quaternion.FromToRotation(new Vector3(centre.x, 0f, centre.z).normalized, Vector3.back);
            Part(face.transform, "Islet", Crop(MorzaBogow.Presentation.ShoreShape.Mound(outline, lift), box), rock, meadow);
            Part(face.transform, "Notch", Crop(made.Notch, box), rock);
            Part(face.transform, "Cliff plants", Crop(made.Plants.Mesh, box), CliffPlants.Material);
            return root;
        }

        static void Part(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            if (mesh == null || mesh.vertexCount == 0) return;
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        /// <summary>The triangles of a mesh whose middle lies in a box, as a mesh of their own (every submesh kept).</summary>
        static Mesh Crop(Mesh mesh, Bounds box)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var keptVertices = new System.Collections.Generic.List<Vector3>();
            var keptNormals = new System.Collections.Generic.List<Vector3>();
            var keptUvs = new System.Collections.Generic.List<Vector2>();
            var remap = new System.Collections.Generic.Dictionary<int, int>();
            var submeshes = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
            int Keep(int index)
            {
                if (remap.TryGetValue(index, out var kept)) return kept;
                kept = keptVertices.Count;
                keptVertices.Add(vertices[index]);
                if (normals.Length == vertices.Length) keptNormals.Add(normals[index]);
                if (uvs.Length == vertices.Length) keptUvs.Add(uvs[index]);
                remap[index] = kept;
                return kept;
            }
            for (var s = 0; s < mesh.subMeshCount; s++)
            {
                var triangles = mesh.GetTriangles(s);
                var kept = new System.Collections.Generic.List<int>();
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    if (!box.Contains((vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f)) continue;
                    kept.Add(Keep(triangles[t])); kept.Add(Keep(triangles[t + 1])); kept.Add(Keep(triangles[t + 2]));
                }
                submeshes.Add(kept);
            }
            var cut = new Mesh { name = mesh.name + " (cut)", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            cut.SetVertices(keptVertices);
            if (keptNormals.Count == keptVertices.Count) cut.SetNormals(keptNormals);
            if (keptUvs.Count == keptVertices.Count) cut.SetUVs(0, keptUvs);
            cut.subMeshCount = submeshes.Count;
            for (var s = 0; s < submeshes.Count; s++) cut.SetTriangles(submeshes[s], s);
            if (keptNormals.Count != keptVertices.Count) cut.RecalculateNormals();
            cut.RecalculateBounds();
            return cut;
        }

        static GameObject Islet()
        {
            var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Limestone.mat");
            var sand = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Seabed.mat");
            var meadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shore/Shore_Meadow.mat");
            var root = new GameObject("Coast preview");
            var outline = MorzaBogow.Presentation.ShoreShape.Ellipse(40f, 30f);
            var lift = MorzaBogow.Presentation.ShoreShape.Cliffs(0f, 60f, 110f, 1.3f);
            var islet = new GameObject("Islet");
            islet.transform.SetParent(root.transform, false);
            islet.AddComponent<MeshFilter>().sharedMesh = MorzaBogow.Presentation.ShoreShape.Mound(outline, lift);
            islet.AddComponent<MeshRenderer>().sharedMaterials = new[] { rock, meadow };
            CoastFeatures.Build(islet.transform, outline, lift, 7, rock, LandscapeArt.Wet(rock), LandscapeArt.Beachrock(sand));
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.transform.SetParent(root.transform, false);
            sea.transform.localScale = new Vector3(12f, 1f, 12f);
            var water = new Material(Shader.Find("HDRP/Lit"));
            water.SetColor("_BaseColor", new Color(0.08f, 0.32f, 0.38f, 1f));
            water.SetFloat("_Smoothness", 0.9f);
            sea.GetComponent<MeshRenderer>().sharedMaterial = water;
            return root;
        }

        /// <summary>
        /// Three olives on a plain ground, one level of detail baked into plain meshes: the preview scene does not skin,
        /// so the bones' pose (still, or after three seconds of a 12 m/s wind) is baked as the game would draw it.
        /// </summary>
        static GameObject Scene(int lod, bool wind)
        {
            var root = new GameObject("Olive preview");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            var builder = new TreeGroveBuilder();
            builder.Add(TreeSpecies.OliveCultivated, 11, new Vector3(-4f, 0f, 0f), 20f);
            builder.Add(TreeSpecies.OliveCultivated, 12, new Vector3(4.5f, 0f, 2f), 140f);
            builder.Add(TreeSpecies.OliveWild, 13, new Vector3(0f, 0f, -6f), 260f);
            var grove = builder.Build(root.transform, "Olives", LandscapeArt.OliveBark, LandscapeArt.OliveLeaves);
            if (wind)
            {
                grove.Wind = new SteadyWind(new Vector3(12f, 0f, 0f));
                for (var i = 0; i < 90; i++) grove.Advance(1f / 30f, true);
            }
            var source = grove.Renderers[lod];
            var baked = new Mesh { indexFormat = source.sharedMesh.indexFormat };
            source.BakeMesh(baked);
            var drawn = new GameObject($"LOD{lod} baked");
            drawn.transform.SetParent(root.transform, false);
            drawn.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            drawn.AddComponent<MeshFilter>().sharedMesh = baked;
            drawn.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            Object.DestroyImmediate(grove.gameObject);
            return root;
        }

        static void Shot(GameObject root, string path, float pitch, float yaw, bool orthographic)
        {
            var done = MorzaBogow.Editor.AIAssets.AIAssetPreview.RenderGroup(root, path, pitch, yaw, orthographic, 1024, 768, out var why);
            Debug.Log($"[Environment] preview {Path.GetFileName(path)}: {(done ? "ok" : why)}");
            if (root != null) Object.DestroyImmediate(root);
        }

        sealed class SteadyWind : ITreeWind
        {
            readonly Vector3 wind;

            public SteadyWind(Vector3 wind) => this.wind = wind;

            public bool Sample(Vector3 point, out TreeWindSample sample)
            {
                sample = new TreeWindSample { Mean = wind, Gust = Vector3.zero, Turbulence = 0.1f, AirDensity = 1.165f, GustLength = 96f };
                return true;
            }
        }

        public static string ManifestPath => Path.Combine(Application.dataPath, "..", "docs", "art", "EnvironmentAssetManifest.json");

        /// <summary>Writes docs/art/EnvironmentAssetManifest.json from the code; a test keeps the file equal to it.</summary>
        [MenuItem("Morza Bogów/Środowisko/Zapisz manifest")]
        public static void ExportManifest()
        {
            File.WriteAllText(ManifestPath, EnvironmentManifest.Json(), new System.Text.UTF8Encoding(false));
            Debug.Log($"[Environment] manifest: {EnvironmentManifest.Entries.Count} assets written to {ManifestPath}");
        }

        static void Folders()
        {
            Folder("Assets/Art", "Environment");
            Folder("Assets/Art/Environment", "Vegetation");
            Folder("Assets/Art/Environment", "Resources");
            Folder("Assets/Art/Environment/Resources", "Environment");
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        static Material Bark(string name, string id, Color tint)
        {
            var diffuse = $"{Source}/{id}_diff_2k.jpg";
            var normal = $"{Source}/{id}_nor_gl_2k.jpg";
            var rough = $"{Source}/{id}_rough_2k.jpg";
            var occlusion = $"{Source}/{id}_ao_2k.jpg";
            if (!File.Exists(diffuse) || !File.Exists(normal) || !File.Exists(rough) || !File.Exists(occlusion)) return null;
            // Bark covers a few dozen pixels from the strategic camera: 1K is plenty and keeps memory low.
            Import(diffuse, TextureImporterType.Default, true, false, 1024);
            Import(normal, TextureImporterType.NormalMap, false, false, 1024);
            var mask = Mask(rough, occlusion, $"{Output}/{id}_mask.png");

            var material = MaterialAt($"{Materials}/{name}.mat");
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetTexture("_MaskMap", mask);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_NormalScale", 1.3f);
            material.SetFloat("_UVBase", 0f);
            material.enableInstancing = false;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Leaves(string name, TreeSpecies species) => FoliageMaterial(name, LeafAtlas.Bake(species, 1024));

        /// <summary>A translucent, alpha-clipped HDRP/Lit material over a baked atlas (colour, normal and mask PNGs).</summary>
        static Material FoliageMaterial(string name, LeafAtlas.Result atlas)
        {
            var colour = Png($"{Output}/{name}_colour.png", atlas.Colour, atlas.Size, false);
            var normal = Png($"{Output}/{name}_normal.png", atlas.Normal, atlas.Size, true);
            var mask = Png($"{Output}/{name}_mask.png", atlas.Mask, atlas.Size, true);

            var importer = (TextureImporter)AssetImporter.GetAtPath(colour);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            // Alpha-tested leaves thin out in small mips; preserving coverage keeps the crowns full from afar.
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            Import(normal, TextureImporterType.NormalMap, false, false, 1024, TextureWrapMode.Clamp);
            Import(mask, TextureImporterType.Default, false, false, 1024, TextureWrapMode.Clamp);

            var material = MaterialAt($"{Materials}/{name}.mat");
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colour));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mask));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_NormalScale", 1f);
            material.SetFloat("_UVBase", 0f);
            HDMaterial.SetAlphaClipping(material, true);
            material.SetFloat("_AlphaCutoff", 0.5f);
            HDMaterial.SetMaterialType(material, MaterialId.LitTranslucent);
            var profile = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(Foliage);
            if (profile != null) HDMaterial.SetDiffusionProfile(material, profile);
            else Debug.LogWarning("[Environment] HDRP foliage diffusion profile not found: leaves stay without transmission colour.");
            // A thin leaf: the light coming through it is the foliage profile's.
            material.SetFloat("_Thickness", 0.25f);
            material.SetFloat("_DoubleSidedEnable", 0f);
            material.enableInstancing = false;
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static string Png(string path, Color32[] pixels, int size, bool linear)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        static Material MaterialAt(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Import(string path, TextureImporterType type, bool srgb, bool readable, int maxSize, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.isReadable = readable;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.anisoLevel = 4;
            importer.textureCompression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>An HDRP mask map from a roughness and an occlusion map, made once and kept as an asset.</summary>
        static Texture2D Mask(string roughPath, string occlusionPath, string maskPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (existing != null) return existing;
            Import(roughPath, TextureImporterType.Default, false, true, 2048);
            Import(occlusionPath, TextureImporterType.Default, false, true, 2048);
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath);
            var r = rough.GetPixels();
            var o = occlusion.width == rough.width && occlusion.height == rough.height ? occlusion.GetPixels() : null;
            var mask = new Color32[r.Length];
            for (var i = 0; i < mask.Length; i++)
            {
                var smoothness = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - r[i].r) * 255f);
                var ao = o != null ? (byte)Mathf.RoundToInt(Mathf.Clamp01(o[i].r) * 255f) : (byte)255;
                mask[i] = new Color32(0, ao, 0, smoothness);
            }
            // Kept at 1K, the size the material samples, so the asset stays small.
            var width = rough.width;
            var height = rough.height;
            while (width > 1024 && width % 2 == 0 && height % 2 == 0)
            {
                var half = new Color32[width / 2 * (height / 2)];
                for (var y = 0; y < height / 2; y++)
                {
                    for (var x = 0; x < width / 2; x++)
                    {
                        var a = mask[2 * y * width + 2 * x];
                        var b = mask[2 * y * width + 2 * x + 1];
                        var c = mask[(2 * y + 1) * width + 2 * x];
                        var d = mask[(2 * y + 1) * width + 2 * x + 1];
                        half[y * (width / 2) + x] = new Color32(0, (byte)((a.g + b.g + c.g + d.g + 2) / 4), 0, (byte)((a.a + b.a + c.a + d.a + 2) / 4));
                    }
                }
                mask = half;
                width /= 2;
                height /= 2;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(mask);
            texture.Apply(false);
            File.WriteAllBytes(maskPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(maskPath);
            Import(maskPath, TextureImporterType.Default, false, false, 1024);
            Import(roughPath, TextureImporterType.Default, false, false, 1024);
            Import(occlusionPath, TextureImporterType.Default, false, false, 1024);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
        }
    }
}
