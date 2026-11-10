using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// Ships' looks in the battle: kept through a save, nothing left behind when a view is used again, a new owner changes
    /// only the colours, damage and repairs never draw the look anew, no stat, point or collider moves, the essential look
    /// holds at a distance, and the look stays within its measured budget.
    /// </summary>
    public sealed class ShipVariantTests
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

        /// <summary>A Ram whose look has gear, sail patches and marks, so every piece of the look is there to check.</summary>
        static Ship FullyDressed(World world, int owner)
        {
            for (var i = 0; i < 400; i++)
            {
                var ship = world.SpawnShip(owner, ShipClass.Ram, 0, 0);
                var look = LookOf(ship);
                if (look.Gear.Count >= 2 && look.Patches.Count > 0 && look.Marks.Count >= 2) return ship;
            }
            Assert.Fail("No fully dressed Ram among 400 ids.");
            return null;
        }

        static IEnumerable<string> Pieces(ShipVisual view) =>
            view.GetComponentsInChildren<Renderer>(true).Select(r => $"{r.name}:{string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "-"))}");

        [UnityTest]
        public IEnumerator Variant_SaveAndLoadKeepEveryShipsLook()
        {
            var host = new GameObject("Battle under test");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            var path = Path.Combine(Application.temporaryCachePath, "ship-variant-test.mbsave");
            try
            {
                yield return null;
                var looks = battle.World.Ships.ToDictionary(ship => ship.Id, ship => battle.Views[ship.Id].Look.ToJson());
                Assert.That(looks.Values.All(json => json.Contains(ShipLookLibrary.Aegean)), Is.True, "The battle composes every ship's look.");
                // A ship taken by the other side keeps its builder's workshop; composed anew it would get the new side's.
                var taken = battle.World.Ships.First(ship => battle.Views[ship.Id].Look.Family != "workshop.modest");
                taken.Owner = 1 - taken.Owner;
                battle.QuickSave(path);
                Assert.That(File.Exists(ShipLookBook.PathBeside(path)), Is.True, "The looks are written beside the save.");
                Assert.That(battle.QuickLoad(path), Is.True);
                yield return null;
                foreach (var ship in battle.World.Ships)
                    Assert.That(battle.Views[ship.Id].Look.ToJson(), Is.EqualTo(looks[ship.Id]), $"ship {ship.Id} keeps its look");
                Assert.That(ShipLookComposer.Compose(taken.Id, taken.Class, taken.Owner).Family, Is.Not.EqualTo(battle.Views[taken.Id].Look.Family),
                    "Without the saved looks the taken ship would have changed its workshop.");

                // Looks written for another save are not taken for this one.
                File.WriteAllText(ShipLookBook.PathBeside(path), File.ReadAllText(ShipLookBook.PathBeside(path)).Replace("\"SaveHash\": \"", "\"SaveHash\": \"0"));
                Assert.That(ShipLookBook.ReadBeside(path, File.ReadAllBytes(path)), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
                foreach (var file in new[] { path, ShipLookBook.PathBeside(path), path + ".bak", ShipLookBook.PathBeside(path) + ".bak" })
                    if (File.Exists(file)) File.Delete(file);
            }
        }

        [Test]
        public void Variant_ReapplyingLeavesNothingOfThePreviousShip()
        {
            var materials = Materials();
            var world = new World(1);
            var first = FullyDressed(world, 0);
            var second = FullyDressed(world, 1);
            var view = ShipVisual.Create(first, materials, LookOf(first));
            var fresh = ShipVisual.Create(second, materials, LookOf(second));
            try
            {
                // The first ship has been through a fight and is changing sides.
                for (var i = 0; i < 5; i++) view.Hit(view.transform.position + view.transform.right * 6f, 1.5f, i == 0);
                view.StickArrows(view.transform.position - view.transform.right * 30f, 30);
                view.SetCondition(0.2f, new double[] { 0, 10, 0 });
                view.SetCondition(0.5f, new double[] { 0, 10, 0 });
                view.ApplyOwner(1, materials);
                view.SetTargetMarker(true);
                Assert.That(view.BreachCount + view.StuckArrows + view.FreshRepairs, Is.GreaterThan(0));

                view.ResetForReuse(second, LookOf(second));
                Assert.That(view.ShipId, Is.EqualTo(second.Id));
                Assert.That(view.BreachCount, Is.Zero, "no breaches of the previous ship");
                Assert.That(view.StuckArrows, Is.Zero, "no arrows");
                Assert.That(view.FreshRepairs, Is.Zero, "no repairs");
                Assert.That(view.IsPostBroken(0) || view.RamBent || view.SailChanging || view.IsMarkedAsTarget, Is.False, "no broken post, bent spur, half-changed sail or marker");
                Assert.That(view.Look.SameAs(LookOf(second)), Is.True);
                Assert.That(view.SailMaterial, Is.SameAs(fresh.SailMaterial), "the new owner's colours in the new ship's cloth");
                Assert.That(Pieces(view).OrderBy(p => p), Is.EqualTo(Pieces(fresh).OrderBy(p => p)), "exactly what a new view of that ship draws, no more");
                Assert.That(view.OwnMeshes, Is.EqualTo(fresh.OwnMeshes));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(fresh.gameObject);
            }
        }

        [Test]
        public void Variant_ANewOwnerChangesOnlyTheColours()
        {
            var materials = Materials();
            var ship = FullyDressed(new World(1), 0);
            var view = ShipVisual.Create(ship, materials, LookOf(ship));
            try
            {
                var look = view.Look.ToJson();
                var sail = view.Sail.GetComponent<Renderer>();
                var patches = view.Sail.Find("Patches").GetComponent<MeshFilter>().sharedMesh.vertices;
                var others = Pieces(view).Where(p => !p.StartsWith("Sail:") && !p.StartsWith("Patches:")).ToList();
                var cloth = view.SailMaterial.name;
                view.ApplyOwner(1, materials);
                for (var i = 0; i < 6; i++) view.Animate(0.3f, i * 0.5f, 0.5f);
                Assert.That(view.SailMaterial.name, Is.Not.EqualTo(cloth), "the sail takes the new side's dye");
                Assert.That(view.SailMaterial.name, Does.StartWith("enemy sail").And.EndWith(cloth.Substring(cloth.IndexOf('['))), "in the same cloth");
                Assert.That(view.Sail.Find("Patches").GetComponent<Renderer>().sharedMaterial.name, Does.StartWith("enemy sail"), "the patches too");
                Assert.That(view.Sail.Find("Patches").GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(patches), "the patches stay where they were sewn");
                Assert.That(Pieces(view).Where(p => !p.StartsWith("Sail:") && !p.StartsWith("Patches:")), Is.EqualTo(others), "timber, paint, marks and gear unchanged");
                Assert.That(view.Look.ToJson(), Is.EqualTo(look), "the look itself is the ship's, not its owner's");
                Assert.That(sail.sharedMaterials.Length, Is.EqualTo(view.Parts.Sail.Slots.Length));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Variant_RepairAndDamageNeverRerollTheLook()
        {
            var materials = Materials();
            var world = new World(1);
            var ship = Enumerable.Range(0, 400).Select(i => world.SpawnShip(0, ShipClass.Ram, 0, 0))
                .First(s => LookOf(s).Gear.Any(g => ShipLookLibrary.ItemOf(g.Item).LostBelow > 0f));
            var view = ShipVisual.Create(ship, materials, LookOf(ship));
            try
            {
                var look = view.Look.ToJson();
                var dressed = Pieces(view).Where(p => !p.StartsWith("Breach") && !p.StartsWith("Arrow") && !p.StartsWith("Tear")).OrderBy(p => p).ToList();
                var gear = view.GearShown;
                for (var i = 0; i < 4; i++) view.Hit(view.transform.position + view.transform.right * (i % 2 == 0 ? 6f : -6f), 1.4f, false);
                view.SetCondition(0.2f, new double[] { 0, 10, 0 });
                Assert.That(view.GearShown, Is.LessThan(gear), "loose gear is washed over the side of a badly holed ship");
                Assert.That(view.Look.ToJson(), Is.EqualTo(look), "damage does not draw the look anew");

                view.SetCondition(1f, new double[] { 10, 10, 10 });
                Assert.That(view.BreachCount, Is.Zero);
                Assert.That(view.FreshRepairs, Is.InRange(1, ShipVisual.MaxFreshRepairs), "the dock leaves new planks where the breaches were");
                Assert.That(view.GearShown, Is.EqualTo(gear), "and the gear is back aboard");
                Assert.That(view.Look.ToJson(), Is.EqualTo(look), "nor does the repair");
                var after = Pieces(view).Where(p => !p.StartsWith("Fresh repair")).OrderBy(p => p).ToList();
                Assert.That(after, Is.EqualTo(dressed), "the same paints, cloth, marks and gear after the repair");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Variant_ChangesNoStatsPointsOrColliders()
        {
            var materials = Materials();
            var world = MapFactory.CreateSkirmish(5);
            var saved = SaveGame.Hash(SaveGame.Serialize(world));
            foreach (var shipClass in (ShipClass[])System.Enum.GetValues(typeof(ShipClass)))
            {
                var ship = world.SpawnShip(0, shipClass, 0, 0);
                var plain = ShipVisual.Create(ship, materials);
                var dressed = ShipVisual.Create(ship, materials, LookOf(ship));
                try
                {
                    foreach (var view in new[] { plain, dressed }) view.transform.SetPositionAndRotation(new Vector3(50f, 0f, 20f), Quaternion.Euler(0f, 30f, 0f));
                    Assert.That(dressed.Length, Is.EqualTo(plain.Length));
                    Assert.That(dressed.Beam, Is.EqualTo(plain.Beam));
                    Assert.That(dressed.OarReach, Is.EqualTo(plain.OarReach));
                    Assert.That(dressed.HullHeight, Is.EqualTo(plain.HullHeight));
                    Assert.That(dressed.Draft, Is.EqualTo(plain.Draft));
                    Assert.That(dressed.OarCount, Is.EqualTo(plain.OarCount));
                    foreach (var target in new[] { new Vector3(90f, 0f, 20f), new Vector3(10f, 0f, 35f), new Vector3(50f, 0f, -30f) })
                    {
                        Assert.That(dressed.RailNear(target), Is.EqualTo(plain.RailNear(target)), $"{shipClass}: where boarders stand");
                        Assert.That(dressed.DeckToward(target), Is.EqualTo(plain.DeckToward(target)), $"{shipClass}: where crews step off");
                    }
                    Vector3[] Points(ShipVisual view, string name) => view.Model.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith(name)).Select(t => t.localPosition).ToArray();
                    Assert.That(Points(dressed, "Mount "), Is.EqualTo(Points(plain, "Mount ")), $"{shipClass}: weapon posts");
                    Assert.That(Points(dressed, "Oar"), Is.EqualTo(Points(plain, "Oar")), $"{shipClass}: thole pins");
                    Assert.That(dressed.GetComponentsInChildren<Collider>(true), Is.Empty, $"{shipClass}: no colliders");
                    Assert.That(dressed.Model.Find("Hull").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(plain.Model.Find("Hull").GetComponent<MeshFilter>().sharedMesh), $"{shipClass}: the same hull, shared, never scaled");
                    Assert.That(dressed.Model.localScale, Is.EqualTo(Vector3.one));
                }
                finally
                {
                    Object.DestroyImmediate(plain.gameObject);
                    Object.DestroyImmediate(dressed.gameObject);
                }
            }
            var check = MapFactory.CreateSkirmish(5);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(check)), Is.EqualTo(saved), "the same map from the same seed");
            var before = SaveGame.Hash(SaveGame.Serialize(check));
            foreach (var ship in check.Ships) Object.DestroyImmediate(ShipVisual.Create(ship, materials, LookOf(ship)).gameObject);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(check)), Is.EqualTo(before), "Looks never touch the simulation.");
        }

        [Test]
        public void Variant_FarAwayKeepsTheEssentialLook()
        {
            var materials = Materials();
            var ship = FullyDressed(new World(1), 1);
            var view = ShipVisual.Create(ship, materials, LookOf(ship));
            var plain = ShipVisual.Create(ship, materials);
            try
            {
                var group = view.Model.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null);
                var lods = group.GetLODs();
                Assert.That(lods.Length, Is.EqualTo(2));
                var near = lods[0].renderers;
                var far = lods[1].renderers;
                var details = view.LookRenderers.Where(r => r.name != "Band").ToArray();
                Assert.That(near, Is.EquivalentTo(details), "up close every detail of the look");
                Assert.That(far, Is.SubsetOf(near));
                Assert.That(far.Select(r => r.name), Does.Contain("Patches"), "from afar the patches still show");
                Assert.That(near.Select(r => r.name), Has.None.EqualTo("Band").And.None.EqualTo("Hull").And.None.EqualTo("Sail"),
                    "the band and the type's parts are never culled: they carry the look at every distance");
                // At any distance the hull, deck, sail and band are the ship's own paints, not the plain ones.
                foreach (var part in new[] { "Hull", "Deck", "Rig" })
                    Assert.That(view.Model.Find(part).GetComponent<Renderer>().sharedMaterials.Select(m => m.name),
                        Is.Not.EqualTo(plain.Model.Find(part).GetComponent<Renderer>().sharedMaterials.Select(m => m.name)), part);
                Assert.That(view.SailMaterial, Is.Not.SameAs(plain.SailMaterial));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(plain.gameObject);
            }
        }

        [Test]
        public void Variant_StaysInItsMeasuredBudget()
        {
            var materials = Materials();
            var world = new World(1);
            var ships = Enumerable.Range(0, 40).Select(i => world.SpawnShip(i % 2, (ShipClass)(i % 7), i * 40f, 0f)).ToArray();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var views = ships.Select(ship => ShipVisual.Create(ship, materials, LookOf(ship))).ToArray();
            var dressedMs = watch.Elapsed.TotalMilliseconds / ships.Length;
            watch.Restart();
            var plain = ships.Select(ship => ShipVisual.Create(ship, materials)).ToArray();
            var plainMs = watch.Elapsed.TotalMilliseconds / ships.Length;
            try
            {
                foreach (var view in views)
                {
                    Assert.That(view.LookRenderers.Count, Is.LessThanOrEqualTo(7), $"{view.Class}: renderers of the look");
                    Assert.That(view.OwnMeshes, Is.LessThanOrEqualTo(3), $"{view.Class}: meshes made for this ship alone");
                    foreach (var renderer in view.LookRenderers)
                        Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.LessThan(4000), $"{view.Class}: {renderer.name}");
                }
                var distinct = views.SelectMany(v => v.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
                Assert.That(distinct, Is.LessThanOrEqualTo(48), "a bounded palette for 40 ships of every class, no material per ship");

                // After it is made, the look costs nothing per frame: animating and checking the condition allocates no more
                // than for ships without a look.
                long Allocated(ShipVisual[] fleet)
                {
                    foreach (var view in fleet) { view.Animate(0f, 0f, 0.02f); view.SetCondition(1f, null); }
                    var start = System.GC.GetAllocatedBytesForCurrentThread();
                    for (var frame = 0; frame < 30; frame++)
                    foreach (var view in fleet)
                    {
                        view.Animate(0f, frame * 0.02f, 0.02f);
                        view.SetCondition(1f, null);
                    }
                    return System.GC.GetAllocatedBytesForCurrentThread() - start;
                }
                var dressedBytes = Allocated(views);
                var plainBytes = Allocated(plain);
                Assert.That(dressedBytes, Is.LessThanOrEqualTo(plainBytes), "no allocation of the look's own per frame");
                Debug.Log($"[ShipLookBudget] create {dressedMs:0.000} ms/ship with look, {plainMs:0.000} plain; {distinct} materials for 40 ships; " +
                          $"look renderers {views.Average(v => v.LookRenderers.Count):0.00}/ship; own meshes {views.Average(v => v.OwnMeshes):0.00}/ship; " +
                          $"30 frames allocate {dressedBytes} B with looks, {plainBytes} B plain");
                Assert.That(dressedMs, Is.LessThan(25.0), "creating a ship with its look stays well under a frame");
            }
            finally
            {
                foreach (var view in views.Concat(plain)) Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
