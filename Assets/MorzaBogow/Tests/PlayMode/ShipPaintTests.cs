using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The ship paint in play: every renderer of a ship carries the ship's own renderer value (its wear), new planks of a
    /// repair carry none, a new owner or a reused view gets it right, the hull's broken pieces keep it, and the paint
    /// really draws differently for a different value while two equal values draw the same.
    /// </summary>
    public sealed class ShipPaintTests
    {
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

        static ShipLook LookOf(Ship ship) => ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner);

        /// <summary>The ship's own parts: everything drawn under its model except the fight's fresh planks.</summary>
        static MeshRenderer[] Painted(ShipVisual view) =>
            view.Model.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name != "Fresh repair").ToArray();

        [Test]
        public void Paint_EveryRendererOfAShipCarriesItsWear()
        {
            var materials = Materials();
            var world = new World(1);
            var old = Enumerable.Range(0, 400).Select(_ => world.SpawnShip(0, ShipClass.Ram, 0, 0)).First(s => LookOf(s).Finish == "old");
            var fresh = Enumerable.Range(0, 400).Select(_ => world.SpawnShip(1, ShipClass.Ram, 0, 0)).First(s => LookOf(s).Finish == "fresh");
            var view = ShipVisual.Create(old, materials, LookOf(old));
            try
            {
                var worn = ShipSurface.Of(view.Look);
                Assert.That(ShipSurface.Level(worn, ShipSurface.WearShift), Is.InRange(10, 13), "an old hull is well worn");
                var painted = Painted(view);
                Assert.That(painted.Length, Is.GreaterThan(10));
                foreach (var renderer in painted) Assert.That(renderer.GetShaderUserValue(), Is.EqualTo(worn), $"{renderer.name}: the ship's own value");

                // The fight: breaches, arrows and tears carry it too; the dock's new planks are not weathered yet.
                for (var i = 0; i < 3; i++) view.Hit(view.transform.position + view.transform.right * 6f, 1.4f, false);
                view.StickArrows(view.transform.position - view.transform.right * 30f, 30);
                view.SetCondition(0.2f, new double[] { 10, 10, 10 });
                view.SetCondition(1f, new double[] { 10, 10, 10 });
                Assert.That(view.FreshRepairs, Is.GreaterThan(0));
                foreach (var renderer in Painted(view)) Assert.That(renderer.GetShaderUserValue(), Is.EqualTo(worn), $"{renderer.name}: after the fight");
                foreach (var planks in view.Model.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Fresh repair"))
                    Assert.That(planks.GetShaderUserValue(), Is.EqualTo(ShipSurface.Unworn(worn)), "new planks are not weathered");

                // A new owner changes the sail's dye, not what the paint shows of the ship.
                view.ApplyOwner(1, materials);
                for (var i = 0; i < 6; i++) view.Animate(0.3f, i * 0.5f, 0.5f);
                foreach (var renderer in Painted(view)) Assert.That(renderer.GetShaderUserValue(), Is.EqualTo(worn), $"{renderer.name}: after a new owner");

                // The hull's broken pieces keep the ship's paints and its value.
                var piece = view.AttachPiece(view.Model, "Broken piece", view.Parts.Sections[0]).GetComponent<MeshRenderer>();
                Assert.That(piece.sharedMaterials, Is.EqualTo(view.MaterialsFor(view.Parts.Sections[0])));
                Assert.That(piece.GetShaderUserValue(), Is.EqualTo(worn));

                // Used again for another ship: that ship's value everywhere, none of the old one's.
                view.ResetForReuse(fresh, LookOf(fresh));
                var unworn = ShipSurface.Of(view.Look);
                Assert.That(ShipSurface.Level(unworn, ShipSurface.WearShift), Is.InRange(1, 4), "a fresh hull is hardly worn");
                foreach (var renderer in Painted(view).Where(r => r.name != "Broken piece"))
                    Assert.That(renderer.GetShaderUserValue(), Is.EqualTo(unworn), $"{renderer.name}: the new ship's value");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        /// <summary>
        /// Four quads in one paint under the same light: two unworn draw the same, a fully worn one draws otherwise, and one
        /// with full wetness and soot but no wear draws as the unworn ones, since those fields are only reserved. Proof that
        /// the paint reads each renderer's own value through the SRP Batcher, not just that the value is set. The paint has
        /// no texture, so its white mask lets the wear act everywhere.
        /// </summary>
        [UnityTest]
        public IEnumerator Paint_TheRendererValueChangesThePicture()
        {
            var basis = ShipPaints.PaintBase;
            Assert.That(basis, Is.Not.Null, "the ship paint material");
            var paint = new Material(basis) { name = "Paint under test" };
            paint.SetColor("_BaseColor", new Color(0.62f, 0.46f, 0.28f));
            paint.SetFloat("_Smoothness", 0.4f);
            paint.SetFloat(ShipPaints.WearStrength, 1f);

            var root = new GameObject("Paint test");
            var target = new RenderTexture(320, 80, 24) { name = "Paint test target" };
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            UnityEngine.Rendering.VolumeProfile profile = null;
            try
            {
                var values = new[]
                {
                    ShipSurface.Pack(0), ShipSurface.Pack(0), ShipSurface.Pack(ShipSurface.Levels),
                    ShipSurface.Pack(0, ShipSurface.Levels, ShipSurface.Levels),
                };
                for (var i = 0; i < values.Length; i++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.transform.SetParent(root.transform, false);
                    quad.transform.localPosition = new Vector3(i - 1.5f, 0f, 0f);
                    quad.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var renderer = quad.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = paint;
                    renderer.SetShaderUserValue(values[i]);
                    Assert.That(renderer.GetShaderUserValue(), Is.EqualTo(values[i]));
                }
                var camera = new GameObject("Paint test camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.gameObject.AddComponent<HDAdditionalCameraData>();
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.transform.localPosition = new Vector3(0f, 0f, 5f);
                camera.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                camera.targetTexture = target;
                var sun = new GameObject("Paint test sun");
                sun.transform.SetParent(root.transform, false);
                sun.AddHDLight(LightType.Directional);
                sun.transform.localRotation = Quaternion.Euler(30f, 160f, 0f);
                var light = sun.GetComponent<Light>();
                light.lightUnit = UnityEngine.Rendering.LightUnit.Lux;
                light.intensity = 10000f;
                // A fixed exposure, so the picture does not wait for the eye to adapt.
                var volume = root.AddComponent<UnityEngine.Rendering.Volume>();
                volume.isGlobal = true;
                volume.priority = 1000f;
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                var exposure = profile.Add<Exposure>();
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(12f);
                volume.sharedProfile = profile;

                for (var frame = 0; frame < 12; frame++) yield return null;
                var active = RenderTexture.active;
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                picture.Apply();
                RenderTexture.active = active;

                // The camera's turn mirrors the row: quad i is drawn centred on column 280 - 80 i.
                Color Mean(int quad)
                {
                    var sum = Color.clear;
                    var count = 0;
                    var centre = 280 - 80 * quad;
                    for (var y = 20; y < 60; y++)
                    for (var x = centre - 20; x < centre + 20; x++)
                    {
                        sum += picture.GetPixel(x, y);
                        count++;
                    }
                    return sum / count;
                }
                float Difference(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                var unwornA = Mean(0);
                var unwornB = Mean(1);
                var worn = Mean(2);
                var reserved = Mean(3);
                var noise = Difference(unwornA, unwornB);
                var wear = Difference(unwornB, worn);
                var wetAndSooty = Difference(unwornB, reserved);
                Debug.Log($"[ShipPaintTest] unworn {unwornA} / {unwornB}, worn {worn}, wet and sooty {reserved}: noise {noise:0.0000}, wear {wear:0.0000}, reserved {wetAndSooty:0.0000}");
                Assert.That(unwornB.r + unwornB.g + unwornB.b, Is.GreaterThan(0.05f), "the paint is lit and drawn");
                Assert.That(wear, Is.GreaterThan(Mathf.Max(0.03f, noise * 4f)), "a fully worn renderer draws otherwise than an unworn one in the same paint");
                Assert.That(wetAndSooty, Is.LessThan(Mathf.Max(0.012f, noise * 2f)), "wetness and soot are reserved: they do not change the picture yet");
            }
            finally
            {
                Object.Destroy(root);
                target.Release();
                Object.Destroy(target);
                Object.Destroy(picture);
                Object.Destroy(paint);
                if (profile != null) Object.Destroy(profile);
            }
        }
    }
}
