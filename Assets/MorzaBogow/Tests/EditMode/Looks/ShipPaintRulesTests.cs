using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MorzaBogow.Editor;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Map = MorzaBogow.Editor.ShipTextureBaker.Map;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The ships' paint beyond its colour: normal and mask maps baked the same every time and imported as data, masks
    /// packed as HDRP Lit packs them, maps that tile, the renderer value's layout (wear now, wetness and soot kept for
    /// later), wear that follows the look alone, a paint graph that compiles without keywords for its maps and keeps the
    /// SRP Batcher, and palette materials made only from what a build carries.
    /// </summary>
    public sealed class ShipPaintRulesTests
    {
        static ShipClass[] Classes => (ShipClass[])Enum.GetValues(typeof(ShipClass));

        static PlaceholderMaterials Materials()
        {
            var lit = Shader.Find("HDRP/Lit");
            Material Paint(string name, Color colour)
            {
                var material = new Material(lit) { name = name };
                material.SetColor("_BaseColor", colour);
                return material;
            }
            return new PlaceholderMaterials
            {
                Wood = Paint("wood", new Color(0.36f, 0.24f, 0.14f)),
                Bronze = Paint("bronze", new Color(0.55f, 0.36f, 0.16f)),
                PlayerSail = Paint("player sail", new Color(0.12f, 0.22f, 0.6f)),
                EnemySail = Paint("enemy sail", new Color(0.55f, 0.08f, 0.08f)),
            };
        }

        [Test]
        public void Textures_PbrMapsAreBakedAndImportedAsData()
        {
            foreach (var (name, size) in ShipTextureBaker.Textures)
            foreach (var map in new[] { Map.Normal, Map.Mask })
            {
                var path = ShipTextureBaker.PathOf(name, map);
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(ShipTextureBaker.Encode(name, size, map), Is.EqualTo(File.ReadAllBytes(path)), $"{path}: the committed map is what the baker makes");
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(map == Map.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default), path);
                Assert.That(importer.sRGBTexture, Is.False, $"{path}: data, not colour");
                Assert.That(importer.convertToNormalmap, Is.False, $"{path}: baked, not made from greys");
                Assert.That(importer.alphaIsTransparency, Is.False, path);
                Assert.That(importer.alphaSource, Is.EqualTo(map == Map.Mask ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None), $"{path}: smoothness lives in the mask's alpha");
                Assert.That(importer.mipmapEnabled, Is.True, path);
                Assert.That(importer.wrapMode, Is.EqualTo(name.StartsWith("worn") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat), path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(size).And.LessThanOrEqualTo(512), $"{path}: as large as its colour, no larger");
                Assert.That(Resources.Load<Texture2D>("ShipLook/" + ShipTextureBaker.FileName(name, map)), Is.Not.Null, $"{path}: the game finds it");
            }
        }

        [Test]
        public void Textures_MasksAndNormalsFollowTheHdrpLitPackingAndTile()
        {
            foreach (var (name, size) in ShipTextureBaker.Textures)
            {
                var mask = ShipTextureBaker.MaskPixels(name, size);
                Assert.That(mask.All(p => p.r == 0), Is.True, $"{name}: R is metallic, and none of this is metal");
                Assert.That(mask.Average(p => p.g / 255f), Is.GreaterThan(0.75f), $"{name}: G is occlusion, deep only in grooves and gaps");
                Assert.That(mask.Min(p => p.g) / 255f, Is.GreaterThanOrEqualTo(0.3f), $"{name}: no black occlusion");
                Assert.That(mask.Max(p => p.b) - mask.Min(p => p.b), Is.GreaterThan(60), $"{name}: B says where wear shows, so it varies");
                Assert.That(mask.Min(p => p.a) / 255f, Is.GreaterThanOrEqualTo(0.3f - 0.5f / 255f), $"{name}: A scales the tone's smoothness, never below 0.3 (to 8-bit rounding)");
                Assert.That(mask.Average(p => p.a / 255f), Is.GreaterThan(0.6f), $"{name}: mostly the tone's own smoothness");

                var normals = ShipTextureBaker.NormalPixels(name, size);
                foreach (var pixel in normals)
                {
                    var n = new Vector3(pixel.r / 255f * 2f - 1f, pixel.g / 255f * 2f - 1f, pixel.b / 255f * 2f - 1f);
                    Assert.That(n.magnitude, Is.InRange(0.96f, 1.04f), $"{name}: unit normals");
                    Assert.That(n.z, Is.GreaterThan(0.35f), $"{name}: relief, never a wall facing sideways");
                }
                Assert.That(normals.Count(p => p.r != 128 || p.g != 128), Is.GreaterThan(normals.Length / 4), $"{name}: the normal map carries relief");

                // A texture that repeats has no seam of its own: the step from the last column (row) to the first is no
                // larger than the largest step between neighbouring columns (rows) inside it, such as a plank seam.
                if (name.StartsWith("worn")) continue;
                AssertTiles(name + " normal", normals, size, true, !name.StartsWith("canvas"));
                AssertTiles(name + " mask", mask, size, true, !name.StartsWith("canvas"));
            }
        }

        static void AssertTiles(string what, Color32[] pixels, int size, bool alongU, bool alongV)
        {
            float Step(int a, int b) => Mathf.Abs(pixels[a].r - pixels[b].r) + Mathf.Abs(pixels[a].g - pixels[b].g) + Mathf.Abs(pixels[a].b - pixels[b].b) + Mathf.Abs(pixels[a].a - pixels[b].a);
            // Mean step between one line of pixels and the next (the last line and the first across the tile's edge).
            float Between(int line, bool columns)
            {
                var next = (line + 1) % size;
                var sum = 0f;
                for (var i = 0; i < size; i++) sum += columns ? Step(i * size + line, i * size + next) : Step(line * size + i, next * size + i);
                return sum / size;
            }
            foreach (var (along, columns) in new[] { (alongU, true), (alongV, false) })
            {
                if (!along) continue;
                var largestInside = 0f;
                for (var line = 0; line < size - 1; line++) largestInside = Mathf.Max(largestInside, Between(line, columns));
                Assert.That(Between(size - 1, columns), Is.LessThanOrEqualTo(largestInside * 1.1f + 2f), $"{what}: tiles along {(columns ? "U" : "V")}");
            }
        }

        [Test]
        public void Surface_ValuePacksWearAndKeepsWetnessAndSootReserved()
        {
            Assert.That(ShipSurface.Pack(0), Is.EqualTo(0u));
            Assert.That(ShipSurface.Pack(15), Is.EqualTo(0xFu), "wear in bits 0-3");
            Assert.That(ShipSurface.Pack(0, 15), Is.EqualTo(0xF0u), "wetness in bits 4-7");
            Assert.That(ShipSurface.Pack(0, 0, 15), Is.EqualTo(0xF00u), "soot in bits 8-11");
            for (var wear = 0; wear <= ShipSurface.Levels; wear++)
            for (var wetness = 0; wetness <= ShipSurface.Levels; wetness++)
            for (var soot = 0; soot <= ShipSurface.Levels; soot += 5)
            {
                var value = ShipSurface.Pack(wear, wetness, soot);
                Assert.That(ShipSurface.Level(value, ShipSurface.WearShift), Is.EqualTo(wear));
                Assert.That(ShipSurface.Level(value, ShipSurface.WetnessShift), Is.EqualTo(wetness));
                Assert.That(ShipSurface.Level(value, ShipSurface.SootShift), Is.EqualTo(soot));
                Assert.That(value >> 12, Is.Zero, "bits 12-31 stay zero");
                Assert.That(ShipSurface.Unworn(value), Is.EqualTo(ShipSurface.Pack(0, wetness, soot)), "a fresh repair has no wear, and keeps the rest");
            }
            var clamped = ShipSurface.Pack(99, -3, 7);
            Assert.That(ShipSurface.Level(clamped, ShipSurface.WearShift), Is.EqualTo(15));
            Assert.That(ShipSurface.Level(clamped, ShipSurface.WetnessShift), Is.Zero);
            Assert.That(ShipSurface.Amount(ShipSurface.Pack(15), ShipSurface.WearShift), Is.EqualTo(1f), "15 reads as 1, as the paint reads it");
        }

        [Test]
        public void Surface_WearFollowsTheFinishFromTheLookAlone()
        {
            var ranges = new Dictionary<string, (int low, int high)> { ["fresh"] = (1, 4), ["seasoned"] = (5, 9), ["old"] = (10, 13) };
            var seen = ranges.Keys.ToDictionary(finish => finish, _ => new HashSet<int>());
            var state = UnityEngine.Random.state;
            for (var id = 0; id < 420; id++)
            {
                var shipClass = Classes[id % Classes.Length];
                var look = ShipLookComposer.Compose(id, shipClass, id % 2);
                var wear = ShipSurface.WearOf(look);
                var (low, high) = ranges[look.Finish];
                Assert.That(wear, Is.InRange(low, high), $"ship {id}: {look.Finish}");
                seen[look.Finish].Add(wear);
                Assert.That(ShipSurface.WearOf(ShipLook.FromJson(look.ToJson())), Is.EqualTo(wear), "a saved look keeps its wear");
                Assert.That(ShipSurface.Of(ShipLookComposer.Compose(id, shipClass, id % 2)), Is.EqualTo(ShipSurface.Of(look)), "the same id, the same value");
                Assert.That(ShipSurface.WearOf(ShipLookComposer.Plain(id, shipClass, id % 2)), Is.Zero, "the plain look is not worn");
            }
            foreach (var finish in ranges.Keys)
                Assert.That(seen[finish].Count, Is.GreaterThanOrEqualTo(3), $"{finish}: ships of one finish still differ");
            Assert.That(UnityEngine.Random.state, Is.EqualTo(state), "no Unity random numbers");
            Assert.That(ShipSurface.WearOf(null), Is.Zero);
        }

        [Test]
        public void Paint_TheGraphCompilesWithoutMapKeywordsAndKeepsTheSrpBatcher()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShipTextureBaker.PaintGraph);
            Assert.That(shader, Is.Not.Null, ShipTextureBaker.PaintGraph);
            Assert.That(shader.name, Is.EqualTo("MorzaBogow/ShipPaint"));
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(ShipTextureBaker.PaintMaterialPath));
            try
            {
                for (var pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                Assert.That(ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m => m.message), Is.Empty, "no compile errors");
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
                Assert.That(shader.isSupported, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
            foreach (var property in new[] { "_BaseColor", "_BaseColorMap", "_NormalMap", "_NormalScale", "_MaskMap", "_Metallic", "_Smoothness", ShipPaints.WearStrength })
                Assert.That(shader.FindPropertyIndex(property), Is.GreaterThanOrEqualTo(0), $"{property}: named as HDRP/Lit names it");
            var keywords = shader.keywordSpace.keywordNames;
            Assert.That(keywords, Has.None.EqualTo("_NORMALMAP").And.None.EqualTo("_MASKMAP").And.None.EqualTo("_NORMALMAP_TANGENT_SPACE"),
                "the maps are always sampled, so no keyword decides whether a build has them");

            // The SRP Batcher judges a shader once it has drawn with it: one frame of a quad in the paint, then the shader
            // inspector's own check (no public call exists, so through reflection) for the subshader HDRP uses.
            var probe = new Material(AssetDatabase.LoadAssetAtPath<Material>(ShipTextureBaker.PaintMaterialPath));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var camera = new GameObject("SRP Batcher probe").AddComponent<Camera>();
            var target = new RenderTexture(64, 64, 24);
            try
            {
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = probe;
                renderer.SetShaderUserValue(ShipSurface.Pack(9));
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2f), Quaternion.identity);
                camera.targetTexture = target;
                camera.Render();
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var active = typeof(ShaderUtil).GetMethod("GetShaderActiveSubshaderIndex", flags);
                var check = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode", flags);
                var reason = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason", flags);
                Assert.That(check, Is.Not.Null, "ShaderUtil.GetSRPBatcherCompatibilityCode");
                Assert.That(active, Is.Not.Null, "ShaderUtil.GetShaderActiveSubshaderIndex");
                var subShader = (int)active.Invoke(null, new object[] { shader });
                var code = (int)check.Invoke(null, new object[] { shader, subShader });
                var why = code == 0 || reason == null ? "" : reason.Invoke(null, new object[] { shader, subShader, code }) as string;
                Assert.That(code, Is.Zero, $"subshader {subShader} keeps the SRP Batcher: {why}");
            }
            finally
            {
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                UnityEngine.Object.DestroyImmediate(quad);
                UnityEngine.Object.DestroyImmediate(probe);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Paint_PaletteMaterialsComeFromThePaintTheBuildCarries()
        {
            var basis = Resources.Load<Material>("ShipLook/" + ShipPaints.PaintMaterial);
            Assert.That(basis, Is.Not.Null, "the paint material is in Resources, so every build carries it and its graph");
            Assert.That(AssetDatabase.GetAssetPath(basis), Is.EqualTo(ShipTextureBaker.PaintMaterialPath));
            Assert.That(basis.shader, Is.SameAs(AssetDatabase.LoadAssetAtPath<Shader>(ShipTextureBaker.PaintGraph)));
            Assert.That(ShipPaints.PaintBase, Is.SameAs(basis));
            var keywords = basis.shaderKeywords.OrderBy(k => k).ToArray();

            var materials = Materials();
            var made = new HashSet<Material>();
            for (var id = 0; id < 80; id++)
            {
                var look = ShipLookComposer.Compose(id, Classes[id % Classes.Length], id % 2);
                var paints = new ShipPaints(look, materials);
                foreach (GreekMaterial paint in Enum.GetValues(typeof(GreekMaterial))) made.Add(paints.Get(paint));
                foreach (var extra in new[] { paints.Sail(0), paints.Sail(1), paints.SailPatch(0), paints.SailPatch(1), paints.RepairPitch, paints.RepairWood, paints.Worn, paints.FreshWood })
                    made.Add(extra);
            }
            var palette = made.Where(m => m != null && m.name.Contains(" [")).ToList();
            Assert.That(palette.Count, Is.GreaterThan(20), "the looks of 80 ships use most of the palette");
            foreach (var material in palette)
            {
                Assert.That(material.shader, Is.SameAs(basis.shader), $"{material.name}: the ship paint");
                Assert.That(material.shaderKeywords.OrderBy(k => k), Is.EqualTo(keywords), $"{material.name}: the same variant as the paint material, which the build compiles");
                var colour = material.GetTexture("_BaseColorMap");
                foreach (var property in new[] { "_BaseColorMap", "_NormalMap", "_MaskMap" })
                {
                    var texture = material.GetTexture(property);
                    if (texture != null) Assert.That(AssetDatabase.GetAssetPath(texture), Does.StartWith(ShipTextureBaker.Folder + "/"), $"{material.name}: {property} from Resources/ShipLook");
                    else Assert.That(colour, Is.Null, $"{material.name}: a texture comes with its {property}");
                }
                Assert.That(material.GetFloat(ShipPaints.WearStrength), Is.EqualTo(colour != null ? 1f : ShipPaints.PlainWearStrength), material.name);
                Assert.That(material.GetFloat("_Metallic"), Is.Zero, $"{material.name}: paint is never metal");
            }
            Assert.That(palette.Any(m => m.GetTexture("_NormalMap") != null && m.name.Contains("grain")), Is.True, "timber carries its relief");
            Assert.That(palette.Any(m => m.GetTexture("_MaskMap") != null && m.name.StartsWith("player sail")), Is.True, "and the sail its cloth");
        }

        [Test]
        public void Ships_MeshesCarryTangentsForTheirNormalMaps()
        {
            foreach (var shipClass in Classes)
            {
                var parts = GreekShips.Parts(shipClass);
                foreach (var (part, mesh) in new[] { ("hull", parts.Hull), ("deck", parts.Deck), ("rig", parts.Rig), ("sail", parts.Sail) })
                {
                    var shared = mesh.Mesh;
                    var tangents = shared.tangents;
                    Assert.That(tangents.Length, Is.EqualTo(shared.vertexCount), $"{shipClass} {part}: a tangent per vertex");
                    var uvs = new List<Vector2>();
                    shared.GetUVs(0, uvs);
                    Assert.That(uvs.Count, Is.EqualTo(shared.vertexCount));
                    foreach (var tangent in tangents)
                    {
                        Assert.That(Mathf.Abs(tangent.w), Is.EqualTo(1f), $"{shipClass} {part}: handedness");
                        Assert.That(new Vector3(tangent.x, tangent.y, tangent.z).magnitude, Is.InRange(0.98f, 1.02f), $"{shipClass} {part}: unit tangents");
                    }
                }
            }
        }
    }
}
