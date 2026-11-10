using System.IO;
using System.Linq;
using MorzaBogow.Editor;
using MorzaBogow.Editor.AIAssets;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Role = MorzaBogow.Editor.HdrpMaterialConverter.MapRole;

namespace MorzaBogow.Tests
{
    /// <summary>The asset pipeline's editor side on objects in memory only: no asset is written, so the quality gate's snapshot stays whole.</summary>
    public sealed class AIAssetsTests
    {
        [TestCase("rock_07_diff_2k", Role.BaseColor, "rock_07", false)]
        [TestCase("rock_07_nor_gl_2k", Role.Normal, "rock_07", false)]
        [TestCase("rock_07_nor_dx_2k", Role.Normal, "rock_07", true)]
        [TestCase("rock_07_rough_2k", Role.Roughness, "rock_07", false)]
        [TestCase("Hull_BaseColor", Role.BaseColor, "hull", false)]
        [TestCase("Hull_Metallic", Role.Metallic, "hull", false)]
        [TestCase("hull-AO", Role.Occlusion, "hull", false)]
        [TestCase("hull_base_rough", Role.Roughness, "hull_base", false)]
        [TestCase("Sail_MaskMap", Role.Mask, "sail", false)]
        [TestCase("deck_height", Role.Height, "deck", false)]
        [TestCase("deck_planks", Role.None, "deck_planks", false)]
        public void Materials_TextureNamesGiveTheirRoleAndSet(string name, Role role, string stem, bool directX)
        {
            var read = HdrpMaterialConverter.Classify(name);
            Assert.That(read.Role, Is.EqualTo(role), name);
            Assert.That(read.Stem, Is.EqualTo(stem), name);
            Assert.That(read.DirectX, Is.EqualTo(directX), name);
        }

        static Texture2D Plain(string name, byte value)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = name };
            texture.SetPixels32(Enumerable.Repeat(new Color32(value, value, value, 255), 16).ToArray());
            texture.Apply(false);
            return texture;
        }

        [Test]
        public void Materials_MaskMapTakesMetalOcclusionAndInvertedRoughness()
        {
            var metal = Enumerable.Repeat(new Color32(30, 0, 0, 255), 2).ToArray();
            var rough = Enumerable.Repeat(new Color32(64, 0, 0, 255), 2).ToArray();
            var mask = MaskMaps.Compose(2, metal, null, rough, null);
            Assert.That((mask[0].r, mask[0].g, mask[0].b, mask[0].a), Is.EqualTo(((byte)30, (byte)255, (byte)0, (byte)191)));
            var plain = MaskMaps.Compose(1, null, null, null, null, 0.25f);
            Assert.That((plain[0].r, plain[0].g, plain[0].a), Is.EqualTo(((byte)0, (byte)255, (byte)64)));
        }

        [Test]
        public void Materials_AnOldMaterialMovesToHdrpLitWithItsSetsMapsAndUndoes()
        {
            var textures = new[] { Plain("crate_diff", 180), Plain("crate_nor_gl", 128), Plain("crate_rough", 64), Plain("crate_ao", 200), Plain("barrel_diff", 90) };
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Texture");
            var material = new Material(shader) { name = "crate" };
            Texture mask = null;
            try
            {
                var maps = HdrpMaterialConverter.MapsOf(textures, HdrpMaterialConverter.StemOf(material.name), null);
                Assert.That(maps.Keys, Is.EquivalentTo(new[] { Role.BaseColor, Role.Normal, Role.Roughness, Role.Occlusion }), "Only the crate's set, not the barrel's.");

                Undo.IncrementCurrentGroup();
                var report = new HdrpMaterialConverter.Report();
                HdrpMaterialConverter.Convert(material, maps, report);
                Assert.That(report.Converted, Is.EqualTo(1));
                Assert.That(material.shader.name, Is.EqualTo("HDRP/Lit"));
                Assert.That(material.GetTexture("_BaseColorMap"), Is.SameAs(textures[0]));
                Assert.That(material.GetTexture("_NormalMap"), Is.SameAs(textures[1]));
                mask = material.GetTexture("_MaskMap");
                Assert.That(mask, Is.Not.Null, "Roughness and occlusion become HDRP's mask map.");
                var texel = ((Texture2D)mask).GetPixels32()[0];
                Assert.That((texel.r, texel.g, texel.b, texel.a), Is.EqualTo(((byte)0, (byte)200, (byte)0, (byte)(255 - 64))), "No metal, occlusion as given, smoothness = 1 - roughness.");

                var again = new HdrpMaterialConverter.Report();
                HdrpMaterialConverter.Convert(material, maps, again);
                Assert.That(again.Skipped, Is.EqualTo(1), "A material already on HDRP is left alone.");

                Undo.PerformUndo();
                Assert.That(material.shader, Is.SameAs(shader), "One undo puts the material back.");
            }
            finally
            {
                foreach (var texture in textures) Object.DestroyImmediate(texture);
                if (mask != null) Object.DestroyImmediate(mask);
                Object.DestroyImmediate(material);
            }
        }

        static GameObject Box(string name, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static AIAssetRequest ShipRequest(ShipClass shipClass) => new AIAssetRequest
        {
            assetId = "ship_test",
            category = "ship",
            shipClass = shipClass.ToString(),
            outputFolder = $"{AIAssetImporter.Root}/ship_test",
            forwardChecked = true,
        };

        [Test]
        public void Ships_ModelsBecomeAClassPrefabWithLodsPointsAndNoPhysics()
        {
            // Long boxes with the bow along +X and a sail material that takes the side's colour; LOD1 is a smaller copy.
            var sail = new Material(Shader.Find("HDRP/Lit")) { name = "Sail" };
            var lod0 = Box("Hull LOD0", new Vector3(10f, 2f, 3f), sail);
            var lod1 = Box("Hull LOD1", new Vector3(10f, 2f, 3f), sail);
            GameObject root = null;
            try
            {
                root = ShipPrefabBuilder.Assemble(new[] { lod0, lod1 }, new[] { 0.25f, 0.002f }, ShipClass.Ram, ModelForward.PlusX, false);
                var definition = ShipCatalog.Get(ShipClass.Ram);
                var length = (float)definition.Radius * 2f;
                var visual = root.transform.Find("Visual");
                var group = visual.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null, "The LODGroup sits on the visual root.");
                Assert.That(group.GetLODs().Select(l => l.screenRelativeTransitionHeight), Is.EqualTo(new[] { 0.25f, 0.002f }));
                var bounds = ShipPrefabBuilder.BoundsIn(root.transform, visual.Find("Model/LOD0").gameObject);
                Assert.That(bounds.size.z, Is.EqualTo(length).Within(0.01f), "Scaled to the class's length, bow along +Z.");
                Assert.That(bounds.size.x, Is.EqualTo(length * 0.3f).Within(0.01f), "Its own proportions kept.");
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.min.y, Is.EqualTo(-(float)definition.Draft).Within(0.01f), "The keel at the class's draft under the waterline.");

                var ship = root.GetComponent<ShipModel>();
                Assert.That(ship, Is.Not.Null, "Points live on the logical root.");
                Assert.That(ship.Bow.localPosition.z, Is.EqualTo(length * 0.5f).Within(1e-4f));
                Assert.That(ship.Stern.localPosition.z, Is.EqualTo(-length * 0.5f).Within(1e-4f));
                Assert.That(ship.Mounts.Length, Is.EqualTo(definition.Slots.Length));
                Assert.That(ship.Muzzles.Length, Is.EqualTo(definition.Slots.Length));
                Assert.That(ship.TeamColourRenderers.Length, Is.EqualTo(2), "The sail takes the side's colour on every LOD.");
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty, "The simulation moves ships.");
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "A ship is picked by its position.");
                Assert.That(lod0.transform.localScale, Is.EqualTo(new Vector3(10f, 2f, 3f)), "The source model is not changed.");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(lod0);
                Object.DestroyImmediate(lod1);
                Object.DestroyImmediate(sail);
            }
        }

        [Test]
        public void AIAssets_ValidatorMeasuresLodsMaterialsBoundsAndFlagsPhysics()
        {
            var lit = new Material(Shader.Find("HDRP/Lit")) { name = "Sail" };
            var old = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color")) { name = "Old" };
            var lod0 = Box("A", new Vector3(3f, 2f, 20f), lit);
            var lod1 = Box("B", new Vector3(3f, 2f, 20f), lit);
            GameObject root = null;
            try
            {
                root = ShipPrefabBuilder.Assemble(new[] { lod0, lod1 }, new[] { 0.25f, 0.002f }, ShipClass.Ram, ModelForward.PlusZ, false);
                var request = ShipRequest(ShipClass.Ram);
                var report = AIAssetValidator.Measure(root, request);
                Assert.That(report.checks.Where(c => !c.ok).Select(c => c.id), Is.Empty, string.Join("; ", report.checks.Where(c => !c.ok).Select(c => $"{c.id}: {c.message}")));
                Assert.That(report.lods.Select(l => l.triangles), Is.EqualTo(new[] { 12, 12 }), "A cube is 12 triangles on each level.");
                Assert.That(report.materialCount, Is.EqualTo(1));
                Assert.That(report.rendererCount, Is.EqualTo(2));
                Assert.That(report.bounds.size[2], Is.EqualTo(20f).Within(0.01f));
                Assert.That(report.bounds.min[1], Is.EqualTo(-1.2f).Within(0.01f));

                root.transform.Find("Visual/Model/LOD1").gameObject.AddComponent<BoxCollider>();
                root.transform.Find("Visual/Model/LOD0").GetComponent<Renderer>().sharedMaterial = old;
                var failed = AIAssetValidator.Measure(root, request).checks.Where(c => !c.ok).Select(c => c.id).ToList();
                Assert.That(failed, Does.Contain("physics"));
                Assert.That(failed, Does.Contain("materials.hdrp"));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(lod0);
                Object.DestroyImmediate(lod1);
                Object.DestroyImmediate(lit);
                Object.DestroyImmediate(old);
            }
        }

        [Test]
        public void AIAssets_GenericAssetStandsOnTheGroundAtTheBriefsHeight()
        {
            var material = new Material(Shader.Find("HDRP/Lit"));
            var model = Box("Rock", new Vector3(4f, 2f, 3f), material);
            GameObject root = null;
            try
            {
                var request = new AIAssetRequest { assetId = "rock_test", category = "rock", dimensions = new Dimensions { height = 6f } };
                root = AIAssetImporter.AssembleGeneric(request, new[] { model }, new[] { 0.002f }, ModelForward.PlusZ);
                var bounds = ShipPrefabBuilder.BoundsIn(root.transform, root.transform.Find("Visual/Model/LOD0").gameObject);
                Assert.That(bounds.size.y, Is.EqualTo(6f).Within(0.01f));
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void AIAssets_RequestFromThePipelineParses()
        {
            const string json = "{\"schema\":\"morza-bogow/unity-import@1\",\"assetId\":\"ship_ram_x\",\"category\":\"ship\",\"shipClass\":\"Ram\"," +
                                "\"outputFolder\":\"Assets/MorzaBogow/Art/Generated/ship_ram_x\",\"lods\":[{\"file\":\"C:\\\\a\\\\x_LOD0.obj\",\"screenRelativeHeight\":0.25}]," +
                                "\"materials\":[{\"name\":\"sail\",\"baseColor\":[0.9,0.8,0.7],\"metallic\":0.0,\"smoothness\":0.1}],\"normalised\":true," +
                                "\"dimensions\":{\"length\":20.0,\"width\":5.6,\"height\":0.0,\"draft\":1.2},\"budget\":{\"triangles\":8000,\"textureSize\":1024," +
                                "\"materials\":4,\"renderers\":40,\"texelDensity\":64},\"teamColour\":[\"sail\"],\"textureSize\":1024}";
            var request = AIAssetRequest.Parse(json);
            Assert.That(request.lods[0].file, Is.EqualTo("C:\\a\\x_LOD0.obj"));
            Assert.That(request.lods[0].screenRelativeHeight, Is.EqualTo(0.25f));
            Assert.That(request.materials[0].baseColor, Is.EqualTo(new[] { 0.9f, 0.8f, 0.7f }));
            Assert.That(request.dimensions.draft, Is.EqualTo(1.2f));
            Assert.That(request.budget.triangles, Is.EqualTo(8000));
            Assert.That(request.normalised, Is.True);
        }

        [Test]
        public void AIAssets_ImportRefusesFoldersOutsideGenerated()
        {
            var request = ShipRequest(ShipClass.Ram);
            request.outputFolder = "Assets/MorzaBogow/Scenes";
            request.lods = new[] { new LodFile { file = "missing.obj", screenRelativeHeight = 0.01f } };
            Assert.Throws<System.ArgumentException>(() => AIAssetImporter.Check(request));
            request.outputFolder = $"{AIAssetImporter.Root}/ship_test";
            Assert.Throws<FileNotFoundException>(() => AIAssetImporter.Check(request));
        }

        [Test]
        public void AIAssets_DimensionsFileMatchesTheShipCatalogue()
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, AIAssetBatch.DimensionsFile);
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(AIAssetBatch.DimensionsJson()),
                "Regenerate with Morza Bogów > Assety AI > Eksportuj wymiary okrętów after changing the ship catalogue.");
        }
    }
}
