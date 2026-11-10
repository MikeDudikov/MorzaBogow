using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MorzaBogow.Editor;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The rules of the ships' looks: a lasting identity from the id alone, whatever the order and without the game's
    /// random numbers; a saved description that a reordered library still draws the same; gear clear of crews, passages,
    /// oars and posts; fleets that differ ship to ship and still hold together; limits that never pass for damage; and the
    /// shared textures as the baker makes them.
    /// </summary>
    public sealed class ShipVariantRulesTests
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

        /// <summary>What a view draws: every renderer with its materials and where it stands.</summary>
        static string Drawn(ShipVisual view) => string.Join("\n", view.GetComponentsInChildren<Renderer>(true).Select(r =>
            $"{r.name}@{r.transform.localPosition.ToString("F3")}:{string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "-"))}"));

        [Test]
        public void Looks_SameIdGivesTheSameLook()
        {
            foreach (var shipClass in Classes)
            for (var id = 1; id <= 30; id++)
            {
                var first = ShipLookComposer.Compose(id, shipClass, id % 2);
                Assert.That(ShipLookComposer.Compose(id, shipClass, id % 2).ToJson(), Is.EqualTo(first.ToJson()), $"{shipClass} {id}");
                Assert.That(ShipLook.FromJson(first.ToJson()).ToJson(), Is.EqualTo(first.ToJson()), "The description survives its own JSON.");
            }
            // FNV-1a of "aegean-12c/1|Ram|7", folded to 32 bits: if this moves, ships without a saved description move too.
            Assert.That(ShipLookComposer.Compose(7, ShipClass.Ram, 0).Seed, Is.EqualTo(1265233437u));
        }

        [Test]
        public void Looks_CreationOrderDoesNotChangeAnyShipsLook()
        {
            var ids = Enumerable.Range(1, 40).ToArray();
            var inOrder = ids.ToDictionary(id => id, id => ShipLookComposer.Compose(id, ShipClass.Ram, id % 2).ToJson());
            foreach (var id in ids.OrderBy(id => id * 7919 % 41))
                Assert.That(ShipLookComposer.Compose(id, ShipClass.Ram, id % 2).ToJson(), Is.EqualTo(inOrder[id]), $"ship {id}");

            // Views made in another order draw every ship the same.
            var world = new World(1);
            var ships = ids.Take(12).Select(id => world.SpawnShip(id % 2, ShipClass.Ram, id * 40, 0)).ToArray();
            var materials = Materials();
            var first = new Dictionary<int, string>();
            var second = new Dictionary<int, string>();
            foreach (var (order, drawn) in new[] { (ships, first), (ships.Reverse().ToArray(), second) })
            foreach (var ship in order)
            {
                var view = ShipVisual.Create(ship, materials, ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner));
                drawn[ship.Id] = Drawn(view);
                Object.DestroyImmediate(view.gameObject);
            }
            Assert.That(second, Is.EquivalentTo(first));
        }

        [Test]
        public void Looks_UseNeitherTheGamesNorUnitysRandom()
        {
            var world = MapFactory.CreateSkirmish(3);
            var state = world.Random.State;
            var saved = SaveGame.Hash(SaveGame.Serialize(world));
            UnityEngine.Random.InitState(12345);
            var unity = UnityEngine.Random.state;
            var materials = Materials();
            foreach (var ship in world.Ships)
            {
                var view = ShipVisual.Create(ship, materials, ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner));
                view.Hit(view.transform.position + view.transform.right * 5f, 1f, false);
                view.SetCondition(0.5f, ship.ModuleHp);
                view.SetCondition(1f, ship.ModuleHp);
                Object.DestroyImmediate(view.gameObject);
            }
            Assert.That(world.Ships.Count, Is.GreaterThan(0));
            Assert.That(world.Random.State, Is.EqualTo(state), "The game's random numbers are untouched.");
            Assert.That(SaveGame.Hash(SaveGame.Serialize(world)), Is.EqualTo(saved), "The world itself is untouched.");
            Assert.That(UnityEngine.Random.state, Is.EqualTo(unity), "Unity's random numbers are untouched.");
        }

        [Test]
        public void Looks_SavedDescriptionSurvivesALibraryInAnotherOrder()
        {
            var world = new World(1);
            var ship = Enumerable.Range(0, 20).Select(i => world.SpawnShip(1, ShipClass.Merchant, 0, 0))
                .First(s => ShipLookComposer.Compose(s.Id, s.Class, s.Owner).Gear.Count >= 2);
            var saved = ShipLookComposer.Compose(ship.Id, ship.Class, ship.Owner).ToJson();
            var materials = Materials();
            string Draw()
            {
                var view = ShipVisual.Create(ship, materials, ShipLook.FromJson(saved));
                try { return Drawn(view); }
                finally { Object.DestroyImmediate(view.gameObject); }
            }
            var before = Draw();
            Array.Reverse(ShipLookLibrary.Items);
            Array.Reverse(ShipLookLibrary.Tones);
            Array.Reverse(ShipLookLibrary.Families);
            Array.Reverse(ShipLookLibrary.Finishes);
            try
            {
                Assert.That(Draw(), Is.EqualTo(before), "Ids, not places in the library, draw a saved ship.");
            }
            finally
            {
                Array.Reverse(ShipLookLibrary.Items);
                Array.Reverse(ShipLookLibrary.Tones);
                Array.Reverse(ShipLookLibrary.Families);
                Array.Reverse(ShipLookLibrary.Finishes);
            }
        }

        static float DistanceXZ(Bounds box, Vector3 point)
        {
            var dx = Mathf.Max(box.min.x - point.x, 0f, point.x - box.max.x);
            var dz = Mathf.Max(box.min.z - point.z, 0f, point.z - box.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        [Test]
        public void Looks_GearKeepsClearOfCrewStationsPassagesOarsAndPosts()
        {
            var placed = 0;
            var classesWithGear = 0;
            var refused = new List<string>();
            foreach (var shipClass in Classes)
            {
                var ship = new World(1).SpawnShip(0, shipClass, 0, 0);
                var form = GreekShips.Form(shipClass);
                var before = placed;
                foreach (var place in ShipLookLibrary.PlacesOf(shipClass))
                foreach (var item in ShipLookLibrary.Items)
                {
                    if (!item.Places.Contains(place.Kind) || (shipClass == ShipClass.Cutter && !item.OnCutter)) continue;
                    var why = ShipLookLibrary.Blocked(shipClass, item, place);
                    if (why != null)
                    {
                        refused.Add($"{shipClass} {item.Id} at {place.Id}: {why}");
                        continue;
                    }
                    foreach (var yaw in new[] { 0f, 45f, 90f, 200f })
                    {
                        var look = ShipLookComposer.Plain(ship.Id, shipClass, 0);
                        look.Gear.Add(new LookGear { Item = item.Id, Anchor = place.Id, Yaw = yaw });
                        var view = ShipVisual.Create(ship, null, look);
                        try
                        {
                            var what = $"{shipClass} {item.Id} at {place.Id}, turned {yaw}";
                            var box = view.Model.Find("Look").GetComponentsInChildren<Renderer>().Single(r => r.name == "Gear " + item.Id).bounds;
                            // Weapon posts, with a working room for their crews.
                            foreach (var mount in view.Model.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Mount ")))
                            foreach (var weapon in mount.GetComponentsInChildren<Renderer>())
                            {
                                var room = weapon.bounds;
                                room.Expand(new Vector3(1.2f, 0.6f, 1.2f));
                                Assert.That(room.Intersects(box), Is.False, $"{what}: a weapon post's crew");
                            }
                            // Where crews stand at the rail to board, fight and work.
                            foreach (var side in new[] { -1f, 1f })
                            for (var along = -0.45f; along <= 0.45f; along += 0.05f)
                            {
                                var outside = new Vector3(side * 40f, 0f, along * view.Length);
                                Assert.That(DistanceXZ(box, view.RailNear(outside)), Is.GreaterThan(0.25f), $"{what}: the boarding line");
                                Assert.That(DistanceXZ(box, view.DeckToward(outside)), Is.GreaterThan(0.25f), $"{what}: the crews' step ashore");
                            }
                            // The rowers' benches between the first and the last thole pin, and the oars' sweep outboard.
                            var pins = view.Model.GetComponentsInChildren<Transform>().Where(t => t.name == "Oar").Select(t => t.position).ToArray();
                            Assert.That(box.max.z < pins.Min(p => p.z) - 0.3f || box.min.z > pins.Max(p => p.z) + 0.3f, Is.True, $"{what}: the rowers");
                            Assert.That(Mathf.Max(-box.min.x, box.max.x), Is.LessThan(pins.Min(p => Mathf.Abs(p.x))), $"{what}: inboard of the oars");
                            // The mast and the steering oars.
                            Assert.That(DistanceXZ(box, view.Model.TransformPoint(view.Parts.MastFoot)), Is.GreaterThan(0.3f), $"{what}: the mast");
                            foreach (var side in new[] { -1f, 1f })
                            {
                                var pin = new Vector3(side * (GreekShips.HalfWidth(form, 0.15f) + 0.15f), 0f, GreekShips.Z(form, 0.15f));
                                Assert.That(DistanceXZ(box, pin), Is.GreaterThan(0.5f), $"{what}: the steering oars");
                            }
                            // On board and low.
                            var s = Mathf.InverseLerp(GreekShips.Z(form, 0f), GreekShips.Z(form, 1f), box.center.z);
                            Assert.That(Mathf.Max(-box.min.x, box.max.x), Is.LessThan(GreekShips.HalfWidth(form, s)), $"{what}: on board");
                            Assert.That(box.size.y, Is.LessThan(1f), $"{what}: low");
                            placed++;
                        }
                        finally
                        {
                            Object.DestroyImmediate(view.gameObject);
                        }
                    }
                }
                if (placed > before) classesWithGear++;
            }
            Assert.That(classesWithGear, Is.GreaterThanOrEqualTo(5), "Gear has room on most classes. Refused:\n" + string.Join("\n", refused));
            var paints = ShipLookLibrary.Items.SelectMany(i => GreekShips.Prop(i.Mesh).Slots).Distinct().ToArray();
            Assert.That(paints, Has.None.EqualTo(GreekMaterial.Bronze), "No gear has a bronze point or blade: nothing on deck passes for a weapon.");
        }

        static string Visible(ShipLook look) =>
            $"{look.Family}|{look.Band}|{look.Pitch}|{look.Ochre}|{look.White}|{look.Wood}|{look.Grain}|{look.Canvas}|{look.Patches.Count}|{look.Marks.Count}|" +
            string.Join(",", look.Gear.Select(g => g.Item + "@" + g.Anchor));

        [Test]
        public void Looks_AFleetDiffersShipToShipYetKeepsItsWorkshopAndColours()
        {
            foreach (var side in new[] { 0, 1 })
            {
                var fleet = Enumerable.Range(1, 24).Select(i => ShipLookComposer.Compose(side * 100 + i, ShipClass.Ram, side)).ToList();
                Assert.That(fleet.Select(Visible).Distinct().Count(), Is.GreaterThanOrEqualTo(20), $"side {side}: hardly two of 24 ships alike");
                var own = ShipLookLibrary.WorkshopsOf(side)[0].family;
                Assert.That(fleet.Count(l => l.Family == own), Is.InRange(12, 23), $"side {side}: most of a fleet from its side's workshop");
                Assert.That(fleet.All(l => l.Family == own || l.Family == "workshop.modest"), Is.True, "only the side's workshops");
                foreach (var look in fleet)
                {
                    var family = ShipLookLibrary.FamilyOf(look.Family);
                    Assert.That(family.Pitch[look.Finish], Does.Contain(look.Pitch), "pitch within the workshop's range");
                    Assert.That(family.Ochre[look.Finish], Does.Contain(look.Ochre), "ochre within the workshop's range");
                    Assert.That(family.Bands.Select(b => b.band), Does.Contain(look.Band));
                }
            }
            // The two sides stay apart on every cloth: the cloth shades the dye, it never shifts its hue.
            foreach (var cloth in ShipLookLibrary.Tones.Where(t => t.Paint == GreekMaterial.Sail))
            {
                Assert.That(cloth.Colour.r, Is.EqualTo(cloth.Colour.g).And.EqualTo(cloth.Colour.b), cloth.Id);
                Assert.That(cloth.Colour.r, Is.InRange(0.8f, 1.15f), $"{cloth.Id}: a shade, not another colour");
            }
        }

        [Test]
        public void Looks_StayWithinTheirLimitsAndNeverLookLikeDamage()
        {
            foreach (var shipClass in Classes)
            for (var id = 1; id <= 60; id++)
            {
                var look = ShipLookComposer.Compose(id, shipClass, id % 2);
                var form = GreekShips.Form(shipClass);
                Assert.That(look.Marks.Count, Is.LessThanOrEqualTo(ShipLookLibrary.MaxMarks));
                Assert.That(look.Patches.Count, Is.LessThanOrEqualTo(ShipLookLibrary.MaxPatches));
                Assert.That(look.Gear.Count, Is.LessThanOrEqualTo(ShipLookLibrary.GearCount(shipClass).max));
                Assert.That(look.Gear.Select(g => g.Item).Distinct().Count(), Is.EqualTo(look.Gear.Count), "each piece of gear once");
                Assert.That(GreekShips.BandPatterns, Does.Contain(look.Band));
                Assert.That(ShipLookLibrary.Grains, Does.Contain(look.Grain));
                foreach (var tone in new[] { look.Pitch, look.Ochre, look.White, look.Wood, look.Rope, look.Linen, look.Canvas, look.PatchCanvas })
                    Assert.That(ShipLookLibrary.ToneOf(tone), Is.Not.Null, $"{shipClass} {id}: {tone}");
                foreach (var mark in look.Marks) Assert.That(ShipLookComposer.Fits(form, mark), Is.True, $"{shipClass} {id}: a mark off the hull");
                foreach (var gear in look.Gear)
                    Assert.That(ShipLookLibrary.Blocked(shipClass, ShipLookLibrary.ItemOf(gear.Item), ShipLookLibrary.PlaceOf(shipClass, gear.Anchor)), Is.Null);
            }
            // A ship's history is not damage: an old hull at full strength shows no breach, arrow or tear, only its old repairs.
            var world = new World(1);
            var old = Enumerable.Range(0, 200).Select(i => world.SpawnShip(0, ShipClass.Ram, 0, 0)).First(s => ShipLookComposer.Compose(s.Id, s.Class, 0).Finish == "old");
            var view = ShipVisual.Create(old, null, ShipLookComposer.Compose(old.Id, old.Class, 0));
            try
            {
                Assert.That(view.BreachCount, Is.Zero);
                Assert.That(view.StuckArrows, Is.Zero);
                Assert.That(view.Look.Marks.Count, Is.GreaterThanOrEqualTo(3));
                Assert.That(view.LookRenderers.Select(r => r.name), Has.None.EqualTo("Breach").And.None.EqualTo("Arrow").And.None.EqualTo("Tear"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Textures_BakerIsDeterministicAndImportSettingsFitTheirUse()
        {
            foreach (var (name, size) in ShipTextureBaker.Textures)
            {
                var path = $"{ShipTextureBaker.Folder}/{name}.png";
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(ShipTextureBaker.Encode(name, size), Is.EqualTo(File.ReadAllBytes(path)), $"{name}: the committed texture is what the baker makes");
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.True, name);
                Assert.That(importer.mipmapEnabled, Is.True, name);
                Assert.That(importer.wrapMode, Is.EqualTo(name.StartsWith("worn") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat), name);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(512), $"{name}: no large texture for a ship's details");
                Assert.That(Resources.Load<Texture2D>("ShipLook/" + name), Is.Not.Null, $"{name}: the game finds it");
            }
        }
    }
}
