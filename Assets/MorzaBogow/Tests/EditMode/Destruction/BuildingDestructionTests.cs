using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The Sages' School breaking as the settlement's hull falls (docs/art/DESTRUCTION.md), the vertical slice's building:
    /// where the shots struck (A), by material (B), along what holds what (C), more with more damage only (E), by the hull
    /// alone (F), put back when the hull rises (G), the same after a load (H), pieces from a pool (I), within budgets (L)
    /// and one budget for many ports at once (M). Presentation only: the settlement is read, never changed.
    /// </summary>
    public sealed class BuildingDestructionTests
    {
        readonly List<GameObject> lands = new List<GameObject>();
        readonly List<PortBuildings> ports = new List<PortBuildings>();
        int maxBodies, maxPerStructure;

        [SetUp]
        public void Remember()
        {
            maxBodies = StructureDamageView.MaxBodies;
            maxPerStructure = StructureDamageView.MaxBodiesPerStructure;
            // The tests look at every building after one update; in the game they are taken apart a few a frame.
            PortBuildings.ModelsPerFrame = int.MaxValue;
        }

        [TearDown]
        public void Clear()
        {
            // In edit mode no OnDestroy runs: every view's pieces in the air leave the budget here.
            foreach (var port in ports)
            foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
            foreach (var view in port.DamagesOf(kind)) view.Forget();
            ports.Clear();
            StructureDamageView.MaxBodies = maxBodies;
            StructureDamageView.MaxBodiesPerStructure = maxPerStructure;
            PortBuildings.ModelsPerFrame = 1;
            foreach (var land in lands)
            {
                if (land == null) continue;
                foreach (var owned in land.GetComponentsInChildren<OwnedMeshes>(true))
                    if (owned.Meshes != null)
                        foreach (var mesh in owned.Meshes.ToList()) if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(land);
            }
            lands.Clear();
        }

        PortBuildings Port(Settlement settlement, DestructionRecord record = null)
        {
            var land = new GameObject("Land under test " + settlement.Id);
            land.transform.position = new Vector3(settlement.Id * 100f, 0f, 0f);
            lands.Add(land);
            var buildings = new PortBuildings(land.transform, null, settlement, 1f, null, null);
            if (record != null) buildings.Record = record;
            buildings.Update(settlement, false, 0f);
            ports.Add(buildings);
            return buildings;
        }

        static Settlement Town(int id = 5) => new Settlement { Id = id, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };

        static void Damage(Settlement settlement, float share) => settlement.Hull = settlement.MaxHull * (1f - share);

        /// <summary>Lets every piece in the air come to rest, in game time.</summary>
        static void Settle(PortBuildings buildings, Settlement settlement, bool ruined = false)
        {
            for (var i = 0; i < 40 && buildings.Collapsing; i++) buildings.Update(settlement, ruined, 0.25f);
        }

        /// <summary>A point by the School's harbour face (in the world), along it by z.</summary>
        static Vector3 FrontOfSchool(PortBuildings buildings, float z)
        {
            var box = buildings.Footprint(PortBuilding.SageSchool);
            return buildings.Root.TransformPoint(new Vector3(box.max.x - 0.6f, 2f, box.center.z + z));
        }

        static void ShellFromTheHarbour(PortBuildings buildings, Settlement settlement, int shots, WeaponType weapon = WeaponType.HeavyCatapult)
        {
            for (var i = 0; i < shots; i++)
            {
                var at = FrontOfSchool(buildings, -2f + 4f * i / Mathf.Max(1, shots - 1));
                buildings.Struck(settlement, at, at + new Vector3(80f, 0f, 3f), weapon, 100 + i, false);
            }
        }

        /// <summary>The elements that failed in their own right (the record's), not those that fell with them.</summary>
        static List<StructureElement> OwnFailures(PortBuildings buildings)
        {
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            return buildings.Record.Failures.Where(f => f.Element.StartsWith("school/"))
                .Select(f => view.Graph[view.Graph.IndexOf(f.Element.Substring("school/".Length))]).ToList();
        }

        /// <summary>How many elements failed in their own right in the pieces whose record prefix starts so (one building's).</summary>
        static int OwnFailuresOf(PortBuildings buildings, string prefix) => buildings.Record.Failures.Count(f => f.Element.StartsWith(prefix));

        static HashSet<string> FailedIds(StructureDamageView view) =>
            new HashSet<string>(view.Graph.Elements.Where(element => view.IsFailed(element.Index)).Select(element => element.Id));

        [Test]
        public void Destruction_A_ShotsBreakTheSideTheyStruck()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            ShellFromTheHarbour(buildings, settlement, 6);
            Damage(settlement, 0.55f);
            buildings.Update(settlement, false, 0f);

            var view = buildings.DamageOf(PortBuilding.SageSchool);
            Assert.That(view, Is.Not.Null, "damaged, the School is drawn in its elements");
            var failed = OwnFailures(buildings);
            Assert.That(failed.Count, Is.GreaterThanOrEqualTo(2), "the shots fell on the School: its elements fail");
            foreach (var element in failed)
                Assert.That(element.Box.center.x, Is.GreaterThan(0f), $"{element.Id} failed on the side the shots struck, the harbour's");
            Assert.That(OwnFailuresOf(buildings, "Store."), Is.LessThan(failed.Count), "the store, which no shot struck, breaks less");
            Assert.That(view.Graph.Elements.Count(e => view.IsFailed(e.Index)), Is.GreaterThan(failed.Count), "and what rested on them came down too");
            // The record keeps the shots: where, from which way, with what.
            var events = buildings.Record.Events;
            Assert.That(events.Count, Is.EqualTo(6));
            Assert.That(events.Select(e => e.Id), Is.Ordered.And.Unique);
            Assert.That(events.All(e => e.Category == "stone" && e.Tick >= 100 && e.Seed != 0), Is.True, "each event: what struck, when, its seed");
            Assert.That(events[0].DirX, Is.LessThan(-0.9f), "the shots flew in from the harbour");
        }

        [Test]
        public void Destruction_B_EachMaterialBreaksItsOwnWay()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            Damage(settlement, 0.2f);
            buildings.Update(settlement, false, 0f);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            var graph = view.Graph;
            StructureElement Get(string id) => graph[graph.IndexOf(id)];
            Assert.That(Get("crepis").Detach, Is.EqualTo(DetachMethod.Fixed));
            Assert.That(Get("crepis").Material, Is.EqualTo(BuildMaterial.Stone));
            Assert.That(Get("col.00.drum.0").Material, Is.EqualTo(BuildMaterial.StuccoedStone));
            Assert.That(Get("col.00.drum.0").Detach, Is.EqualTo(DetachMethod.Topple));
            Assert.That(Get("span.00.architrave").Detach, Is.EqualTo(DetachMethod.Drop));
            Assert.That(Get("wall.back.1").Material, Is.EqualTo(BuildMaterial.Mudbrick));
            Assert.That(Get("wall.back.1").Detach, Is.EqualTo(DetachMethod.Crumble));
            Assert.That(Get("roof.front.0").Material, Is.EqualTo(BuildMaterial.Ceramic));
            Assert.That(Get("roof.front.0").Detach, Is.EqualTo(DetachMethod.CaveIn));
            Assert.That(Get("hall.door").Material, Is.EqualTo(BuildMaterial.Timber));
            Assert.That(graph.Elements.Count(e => e.Column == 7), Is.EqualTo(PortBuildings.SchoolDrums + 1), "five drums and a capital a column");

            // Brought down whole: each element lies the way its material and its fall make it lie.
            buildings.Update(settlement, true, 0f);
            Settle(buildings, settlement, true);
            foreach (var element in graph.Elements)
            {
                if (!view.IsFailed(element.Index)) continue;
                var (rotation, position) = view.RestOf(element.Index);
                switch (element.Detach)
                {
                    case DetachMethod.Topple:
                    {
                        Assert.That(Mathf.Abs((rotation * Vector3.up).y), Is.LessThan(0.2f), $"{element.Id}: a drum or capital lies on its side");
                        var lying = rotation * element.Box.center + position;
                        Assert.That(lying.y, Is.LessThan(element.Box.center.y), $"{element.Id}: lies lower than it stood");
                        // Out of the building: never into the hall, where its walls stand in the way.
                        var stood = new Vector2(element.Box.center.x, element.Box.center.z);
                        var lies = new Vector2(lying.x, lying.z);
                        Assert.That(Vector2.Dot(lies - stood, new Vector2(element.Outward.x, element.Outward.z)), Is.GreaterThan(0f), $"{element.Id}: fell outward");
                        break;
                    }
                    case DetachMethod.Drop:
                        Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.InRange(60f, 120f), $"{element.Id}: a block tipped onto its face");
                        break;
                    case DetachMethod.CaveIn:
                        Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.GreaterThan(8f), $"{element.Id}: a bay of roof swung down");
                        break;
                    case DetachMethod.Crumble:
                        Assert.That(position, Is.EqualTo(Vector3.zero), $"{element.Id}: brick slumps where it stood");
                        break;
                }
            }
            Assert.That(view.IsFailed("crepis"), Is.False, "the steps stay");
            Assert.That(view.IsFailed("hall.socle"), Is.False);
        }

        [Test]
        public void Destruction_C_WhatRestsOnAFailedColumnFallsWithIt()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            Damage(settlement, 0.1f);
            buildings.Update(settlement, false, 0f);
            var graph = buildings.DamageOf(PortBuilding.SageSchool).Graph;
            Assert.That(graph.Validate(), Is.Empty, "every element rests at last on the steps");
            Assert.That(graph.Elements.Count(e => e.Primary), Is.EqualTo(32), "a weak joint per column, eight walls, eight bays of roof");

            var weak = graph.Elements.Single(e => e.Column == 3 && e.Primary);
            var failed = new bool[graph.Count];
            failed[weak.Index] = true;
            graph.Cascade(failed);
            bool Failed(string id) => failed[graph.IndexOf(id)];
            Assert.That(Failed("col.03.cap"), Is.True, "the capital goes over with its column");
            foreach (var below in graph.Elements.Where(e => e.Column == 3 && e.Index < weak.Index)) Assert.That(failed[below.Index], Is.False, $"{below.Id}: below the joint it stands");
            foreach (var span in new[] { "span.02", "span.03" })
            {
                Assert.That(Failed(span + ".architrave"), Is.True, $"{span}: its architrave lost a capital");
                Assert.That(Failed(span + ".frieze"), Is.True, $"{span}: the frieze on it");
            }
            Assert.That(Failed("span.01.architrave") || Failed("span.04.architrave"), Is.False, "the neighbours' spans keep both capitals");
            Assert.That(Failed("roof.front.1") && Failed("roof.front.2"), Is.True, "the roof over the fallen spans comes down");
            Assert.That(graph.Elements.Where(e => e.Id.StartsWith("roof.back")).Any(e => failed[e.Index]), Is.False, "the far side's roof stands");
            Assert.That(Failed("col.02.cap") || Failed("col.04.cap") || Failed("crepis"), Is.False);
        }

        [Test]
        public void Destruction_E_MoreDamageOnlyAddsToWhatFell()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            ShellFromTheHarbour(buildings, settlement, 4);
            var before = new HashSet<string>();
            var stage = DestructionStage.Intact;
            foreach (var share in new[] { 0.1f, 0.25f, 0.35f, 0.5f, 0.65f, 0.8f, 0.95f })
            {
                ShellFromTheHarbour(buildings, settlement, 2, WeaponType.StoneCatapult);
                Damage(settlement, share);
                buildings.Update(settlement, false, 0f);
                Settle(buildings, settlement);
                var view = buildings.DamageOf(PortBuilding.SageSchool);
                var now = FailedIds(view);
                Assert.That(before.IsSubsetOf(now), Is.True, $"at {share:0.00} what fell before still lies fallen");
                Assert.That(view.Stage, Is.GreaterThanOrEqualTo(stage), $"at {share:0.00} the stage never goes back");
                before = now;
                stage = view.Stage;
            }
            Assert.That(stage, Is.EqualTo(DestructionStage.PartialCollapse));
            Assert.That(settlement.Destroyed, Is.False, "the view only reads the hull");
        }

        [Test]
        public void Destruction_F_TheHullAloneDecidesTheStageAndHowMuchFails()
        {
            Assert.That(DamagePlan.StageOf(0.05f, false, false), Is.EqualTo(DestructionStage.Intact));
            Assert.That(DamagePlan.StageOf(0.1f, false, false), Is.EqualTo(DestructionStage.SurfaceDamage));
            Assert.That(DamagePlan.StageOf(0.4f, false, false), Is.EqualTo(DestructionStage.LocalFailure));
            Assert.That(DamagePlan.StageOf(0.7f, false, false), Is.EqualTo(DestructionStage.PartialCollapse));
            Assert.That(DamagePlan.StageOf(1f, true, false), Is.EqualTo(DestructionStage.WreckOrRuin));
            Assert.That(DamagePlan.StageOf(1f, true, true), Is.EqualTo(DestructionStage.Settled));
            Assert.That(DamagePlan.Failures(0.2f, false, 32), Is.Zero);
            Assert.That(DamagePlan.Failures(0.3f, false, 32), Is.EqualTo(1));
            Assert.That(DamagePlan.Failures(0.99f, false, 32), Is.EqualTo(9));
            Assert.That(DamagePlan.Failures(0.5f, true, 32), Is.EqualTo(32));

            // Many small shots or a few big ones: the same hull shows the same stage and the same number of failures.
            var arrows = Town(5);
            var stones = Town(6);
            var a = Port(arrows);
            var b = Port(stones);
            ShellFromTheHarbour(a, arrows, 40, WeaponType.ArcherBank);
            ShellFromTheHarbour(b, stones, 2, WeaponType.HeavyCatapult);
            Damage(arrows, 0.62f);
            Damage(stones, 0.62f);
            var hull = arrows.Hull;
            a.Update(arrows, false, 0f);
            b.Update(stones, false, 0f);
            var va = a.DamageOf(PortBuilding.SageSchool);
            var vb = b.DamageOf(PortBuilding.SageSchool);
            Assert.That(va.Stage, Is.EqualTo(vb.Stage));
            Assert.That(OwnFailures(a).Count, Is.EqualTo(OwnFailures(b).Count), "the same hull and the same places struck");
            Assert.That(arrows.Hull, Is.EqualTo(hull), "showing the damage takes nothing off the hull");
        }

        [Test]
        public void Destruction_G_RaisedWallsPutBackTheLastToFallFirst()
        {
            // No shots noted: every building takes the settlement's own share, so the School's failures follow the hull.
            var settlement = Town();
            var buildings = Port(settlement);
            Damage(settlement, 0.35f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            var first = buildings.Record.Failures.Select(f => f.Element).ToList();
            Assert.That(OwnFailures(buildings).Count, Is.EqualTo(DamagePlan.Failures(0.35f, false, 32)), "one of its own at 35% lost");

            Damage(settlement, 0.85f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            Assert.That(buildings.Record.Failures.Count, Is.GreaterThan(4));
            Assert.That(buildings.Record.Failures[0].Element, Is.EqualTo(first[0]), "the first to fall stays first");

            // The walls raised: the hull rises (World.Arsenal), and the last to fall stand again first.
            Damage(settlement, 0.35f);
            buildings.Update(settlement, false, 0f);
            Assert.That(buildings.Record.Failures.Select(f => f.Element), Is.EqualTo(first), "back to the first failure alone");
            Assert.That(view.Busy, Is.False, "nothing is left falling");

            Damage(settlement, 0f);
            buildings.Update(settlement, false, 0f);
            Assert.That(view.Broken, Is.False);
            Assert.That(view.Scars, Is.Zero, "a sound building has no scars");
            var whole = buildings.Root.Find("SageSchool/School/Crepis/Colonnade and hall");
            Assert.That(whole.gameObject.activeSelf, Is.True, "whole again, it is one mesh");
            Assert.That(buildings.Root.Find("SageSchool/School/Crepis").GetComponent<MeshRenderer>().enabled, Is.True);
        }

        [Test]
        public void Destruction_H_ALoadedGameShowsTheSameFailuresWhereTheyLay()
        {
            var book = new DestructionBook();
            var settlement = Town();
            var buildings = Port(settlement, book.For(settlement.Id));
            ShellFromTheHarbour(buildings, settlement, 6);
            Damage(settlement, 0.75f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            var failed = FailedIds(view);
            var poses = view.Graph.Elements.Where(e => view.IsFailed(e.Index)).ToDictionary(e => e.Id, e => view.RestOf(e.Index));
            var scars = view.Scars;

            const ulong hash = 0x1234abcdUL;
            var json = book.ToJson(hash, new[] { settlement.Id });
            Assert.That(DestructionBook.FromJson(json, 0x999UL), Is.Null, "a record belongs to its own save only");
            var loaded = DestructionBook.FromJson(json, hash);
            Assert.That(loaded, Is.Not.Null);

            var again = new Settlement { Id = settlement.Id, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true, Hull = settlement.Hull };
            var reloaded = Port(again, loaded.For(again.Id));
            var view2 = reloaded.DamageOf(PortBuilding.SageSchool);
            Assert.That(view2, Is.Not.Null);
            Assert.That(view2.Busy, Is.False, "after a load nothing falls again");
            Assert.That(FailedIds(view2), Is.EquivalentTo(failed), "the same elements lie fallen");
            foreach (var pair in poses)
            {
                var (rotation, position) = view2.RestOf(view2.Graph.IndexOf(pair.Key));
                Assert.That(Vector3.Distance(position, pair.Value.position), Is.LessThan(1e-3f), $"{pair.Key} lies where it lay");
                Assert.That(Quaternion.Angle(rotation, pair.Value.rotation), Is.LessThan(0.05f));
            }
            Assert.That(view2.Scars, Is.EqualTo(scars));

            // Without the record the stage and the number of failures are the same; which elements may differ.
            var fresh = new Settlement { Id = 6, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true, Hull = settlement.Hull };
            var plainPort = Port(fresh);
            Assert.That(plainPort.DamageOf(PortBuilding.SageSchool).Stage, Is.EqualTo(view.Stage));
            Assert.That(OwnFailures(plainPort).Count, Is.EqualTo(DamagePlan.Failures(0.75f, false, 32)), "with no shots noted, the hull's own share");
        }

        [Test]
        public void Destruction_I_FallingPiecesGoBackToThePoolAndComeBackClean()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            var inAir = StructureDamageView.BodiesInAir;
            buildings.Update(settlement, true, 0f);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            Assert.That(view.Busy, Is.True, "a School in sight is seen to fall");
            Assert.That(StructureDamageView.BodiesInAir, Is.GreaterThan(inAir));
            Settle(buildings, settlement, true);
            var made = view.PiecesMade;
            Assert.That(view.Busy, Is.False);
            Assert.That(StructureDamageView.BodiesInAir, Is.EqualTo(inAir), "at rest, the pieces leave the budget");
            foreach (Transform piece in view.Frame)
            {
                if (piece.name != "Piece") continue;
                Assert.That(piece.gameObject.activeSelf, Is.False, "a pooled piece is hidden");
                Assert.That(piece.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(piece.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(piece.GetComponent<MeshFilter>().sharedMesh, Is.Null, "and carries nothing");
            }

            // Raised again and brought down again: the same pieces serve.
            buildings.Update(settlement, false, 0f);
            Assert.That(view.Broken, Is.False);
            buildings.Update(settlement, true, 0f);
            Assert.That(view.Busy, Is.True);
            Settle(buildings, settlement, true);
            Assert.That(view.PiecesMade, Is.EqualTo(made), "no new pieces for a second fall");
        }

        [Test]
        public void Destruction_L_AFallStaysWithinItsBudgets()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            buildings.Update(settlement, true, 0f);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            var frame = view.Frame;
            int Drawn() => frame.GetComponentsInChildren<MeshRenderer>(false).Length;
            var most = 0;
            for (var t = 0f; t < PortBuildings.CollapseSeconds + 0.5f; t += 0.1f)
            {
                Assert.That(view.PiecesFalling, Is.LessThanOrEqualTo(StructureDamageView.MaxBodiesPerStructure));
                Assert.That(StructureDamageView.BodiesInAir, Is.LessThanOrEqualTo(StructureDamageView.MaxBodies));
                most = Mathf.Max(most, Drawn());
                buildings.Update(settlement, true, 0.1f);
            }
            Assert.That(view.Busy, Is.False, $"the fall is over within {PortBuildings.CollapseSeconds} s of game time");
            Assert.That(most, Is.LessThanOrEqualTo(StructureDamageView.MaxBodiesPerStructure + StructureDamageView.MaxChunks + 3));
            Assert.That(Drawn(), Is.LessThanOrEqualTo(3), "at rest: what stands, the scars and the rubble, one mesh each");
            var triangles = frame.GetComponentsInChildren<MeshFilter>(false).Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3);
            Assert.That(triangles, Is.LessThan(60000), "the ruin's triangles");
            Assert.That(view.Stage, Is.EqualTo(DestructionStage.Settled));
            Assert.That(view.SettledPieces, Is.EqualTo(view.FailedCount));
        }

        [Test]
        public void Destruction_M_ManyPortsFallingAtOnceShareOneBudget()
        {
            StructureDamageView.MaxBodies = 30;
            var towns = Enumerable.Range(5, 4).Select(id => Town(id)).ToList();
            var all = towns.Select(town => Port(town)).ToList();
            for (var i = 0; i < all.Count; i++) all[i].Update(towns[i], true, 0f);
            Assert.That(StructureDamageView.BodiesInAir, Is.LessThanOrEqualTo(30), "one budget for all the pieces in the air");
            for (var t = 0f; t < PortBuildings.CollapseSeconds + 0.5f; t += 0.25f)
                for (var i = 0; i < all.Count; i++) all[i].Update(towns[i], true, 0.25f);
            foreach (var port in all)
            {
                var view = port.DamageOf(PortBuilding.SageSchool);
                Assert.That(view.Busy, Is.False);
                Assert.That(view.SettledPieces, Is.EqualTo(view.FailedCount), "over the budget pieces lie at rest at once, none lost");
                Assert.That(port.ShowsRuin(PortBuilding.SageSchool), Is.True);
            }
            Assert.That(StructureDamageView.BodiesInAir, Is.Zero);
        }

        [Test]
        public void Destruction_OutOfSightItIsFoundAtRest()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            buildings.Unseen();
            Damage(settlement, 0.7f);
            buildings.Update(settlement, false, 0f);
            var view = buildings.DamageOf(PortBuilding.SageSchool);
            Assert.That(view.Broken, Is.True);
            Assert.That(view.Busy, Is.False, "what fell out of sight is not seen falling later");
            Assert.That(buildings.Collapsing, Is.False);
        }

        [Test]
        public void Destruction_N_HousesTheWorkshopAndTheWallBreakToo()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            Assert.That(buildings.BreakablePieces(PortBuilding.Store), Is.EqualTo(2), "the store and its house");
            Assert.That(buildings.BreakablePieces(PortBuilding.Town), Is.EqualTo(5), "four houses and the wall");
            Assert.That(buildings.BreakablePieces(PortBuilding.Workshop), Is.EqualTo(1));
            Assert.That(buildings.BreakablePieces(PortBuilding.Dock), Is.EqualTo(2), "the dock's ship shed and its shear legs (HarbourBuildingTests)");
            Damage(settlement, 0.7f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            foreach (var kind in new[] { PortBuilding.Store, PortBuilding.Town, PortBuilding.Workshop, PortBuilding.SageSchool, PortBuilding.Dock })
            {
                var views = buildings.DamagesOf(kind);
                Assert.That(views.Count, Is.EqualTo(buildings.BreakablePieces(kind)), $"{kind}: every piece shows the damage");
                foreach (var view in views)
                {
                    Assert.That(view.Graph.Validate(), Is.Empty, $"{view.Frame.name}: everything rests on its foundation");
                    Assert.That(view.Broken, Is.True, $"{view.Frame.name}: at 70% lost its elements fail");
                }
            }
            // Each by its own material and construction.
            var store = buildings.DamagesOf(PortBuilding.Store)[0].Graph;
            Assert.That(store.Elements.Where(e => e.Role == ElementRole.Wall).All(e => e.Material == BuildMaterial.Stone), Is.True, "the store is of stone");
            var house = buildings.DamagesOf(PortBuilding.Store)[1].Graph;
            Assert.That(house.Elements.Where(e => e.Role == ElementRole.Wall).All(e => e.Material == BuildMaterial.Mudbrick && e.Detach == DetachMethod.Crumble), Is.True,
                "the house is of brick, which slumps");
            Assert.That(house.Elements.Count(e => e.Role == ElementRole.Roof), Is.GreaterThanOrEqualTo(2));
            var shed = buildings.DamagesOf(PortBuilding.Workshop)[0].Graph;
            Assert.That(shed.Elements.Count(e => e.Material == BuildMaterial.Timber && e.Detach == DetachMethod.Topple), Is.EqualTo(6), "six posts that go over");
            var wall = buildings.DamagesOf(PortBuilding.Town).Single(v => v.Frame.name.Contains("Wall")).Graph;
            Assert.That(wall.Elements.Count(e => e.Id.StartsWith("tower.") && !e.Id.EndsWith(".roof")), Is.EqualTo(6));
            Assert.That(wall.Elements.Count(e => e.Id.StartsWith("wall.")), Is.GreaterThan(15), "the curtain in stretches");
            Assert.That(wall.Elements.Where(e => e.Id.EndsWith(".roof")).All(e => e.Supports.Length == 1), Is.True, "a tower's cap rests on its tower");

            // Brought down whole, every building lies in the ruin of its own pieces.
            buildings.Update(settlement, true, 0f);
            Settle(buildings, settlement, true);
            foreach (var kind in new[] { PortBuilding.Store, PortBuilding.Town, PortBuilding.Workshop, PortBuilding.SageSchool, PortBuilding.Dock })
            {
                Assert.That(buildings.ShowsRuin(kind), Is.True, $"{kind} lies in its ruin");
                Assert.That(buildings.Root.Find(kind.ToString()).gameObject.activeSelf, Is.False, $"{kind}: what stood is gone");
                foreach (var view in buildings.DamagesOf(kind))
                    Assert.That(view.SettledPieces, Is.EqualTo(view.FailedCount), $"{view.Frame.name}: all at rest");
            }
        }

        [Test]
        public void Destruction_O_TheShotsDrawTheDamageToWhatTheyStruck()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            ShellFromTheHarbour(buildings, settlement, 8);
            Damage(settlement, 0.3f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var school = OwnFailures(buildings).Count;
            Assert.That(school, Is.GreaterThan(DamagePlan.Failures(0.3f, false, 32)), "more than its share where the shots fell");
            foreach (var prefix in new[] { "Store.", "Workshop.", "Town." })
                Assert.That(OwnFailuresOf(buildings, prefix), Is.LessThan(school), $"{prefix} breaks less than the School the shots struck");
        }

        [Test]
        public void Destruction_Q_InSightTheBuildingsAreTakenApartAFewAFrame()
        {
            PortBuildings.ModelsPerFrame = 1;
            var settlement = Town();
            var buildings = Port(settlement);
            ShellFromTheHarbour(buildings, settlement, 6);
            Damage(settlement, 0.05f);
            int Made() => System.Enum.GetValues(typeof(PortBuilding)).Cast<PortBuilding>().Sum(kind => buildings.DamagesOf(kind).Count);
            buildings.Update(settlement, false, 0f);
            Assert.That(Made(), Is.EqualTo(1), "one building a frame, ahead of need");
            Assert.That(buildings.DamageOf(PortBuilding.SageSchool), Is.Not.Null, "the one the shots struck, which shows damage first, comes first");
            for (var i = 0; i < 12; i++) buildings.Update(settlement, false, 0f);
            Assert.That(Made(), Is.EqualTo(11), "within a few frames every building is ready to break, the dock's shed and shear legs too");
            // A port first seen (or seen again) is taken apart at once.
            var other = Town(6);
            var seen = Port(other);
            Damage(other, 0.5f);
            seen.Unseen();
            seen.Update(other, false, 0f);
            Assert.That(System.Enum.GetValues(typeof(PortBuilding)).Cast<PortBuilding>().Sum(kind => seen.DamagesOf(kind).Count), Is.EqualTo(11));
        }

        [Test]
        public void Destruction_P_ShotsElsewhereNeverPutBackWhatFell()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            ShellFromTheHarbour(buildings, settlement, 8);
            Damage(settlement, 0.3f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var fallen = FailedIds(buildings.DamageOf(PortBuilding.SageSchool));
            Assert.That(fallen, Is.Not.Empty);
            // Now the shots fall on the store: its share grows; the School's would shrink, but what fell stays down.
            var store = buildings.Footprint(PortBuilding.Store);
            for (var i = 0; i < 30; i++)
            {
                var at = buildings.Root.TransformPoint(new Vector3(store.center.x, 2f, store.center.z + (i % 5 - 2) * 0.8f));
                buildings.Struck(settlement, at, at + new Vector3(80f, 0f, 0f), WeaponType.StoneCatapult, 300 + i, false);
            }
            Damage(settlement, 0.32f);
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            Assert.That(fallen.IsSubsetOf(FailedIds(buildings.DamageOf(PortBuilding.SageSchool))), Is.True, "only a rising hull puts anything back");
        }
    }
}
