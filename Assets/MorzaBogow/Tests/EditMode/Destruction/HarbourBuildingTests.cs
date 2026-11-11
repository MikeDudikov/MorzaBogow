using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The port's works by the water and a bridgehead's camp (PortBuildings.Harbour, docs/art/DESTRUCTION.md): the dock's
    /// ship shed of Zea's kind beside the war line's slipway and the jetty, never over them, with its shear legs; a
    /// bridgehead's field camp; every building put up by assembly, nothing scaled; the workshop's forge; the dock and the
    /// camp breaking element by element like the rest; the same rubble on every system; the timber jetty in the old one's
    /// place. Presentation only: the settlements are read, never changed by the buildings.
    /// </summary>
    public sealed class HarbourBuildingTests
    {
        readonly List<GameObject> lands = new List<GameObject>();
        readonly List<PortBuildings> ports = new List<PortBuildings>();

        [SetUp]
        public void Remember() => PortBuildings.ModelsPerFrame = int.MaxValue;

        [TearDown]
        public void Clear()
        {
            foreach (var port in ports)
            foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
            foreach (var view in port.DamagesOf(kind)) view.Forget();
            ports.Clear();
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

        PortBuildings Port(Settlement settlement, float toward = 1f)
        {
            var land = new GameObject("Land under test " + settlement.Id);
            land.transform.position = new Vector3(settlement.Id * 100f, 0f, 0f);
            lands.Add(land);
            var buildings = new PortBuildings(land.transform, null, settlement, toward, null, null);
            buildings.Update(settlement, false, 0f);
            ports.Add(buildings);
            return buildings;
        }

        static Settlement Town(int id = 5) => new Settlement { Id = id, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };

        static void Settle(PortBuildings buildings, Settlement settlement, bool ruined = false)
        {
            for (var i = 0; i < 40 && buildings.Collapsing; i++) buildings.Update(settlement, ruined, 0.25f);
        }

        /// <summary>Every corner of every mesh under a part, in the land's frame as (u toward the port, height, z along the shore).</summary>
        static List<Vector3> Corners(Transform part, Transform land, float toward)
        {
            var list = new List<Vector3>();
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                for (var c = 0; c < 8; c++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var at = land.InverseTransformPoint(filter.transform.TransformPoint(local));
                    list.Add(new Vector3(at.x * toward, at.y, at.z));
                }
            }
            return list;
        }

        [Test]
        public void Dock_TheShipShedStandsBesideTheSlipwayAndTheJettyNeverOverThem()
        {
            foreach (var toward in new[] { 1f, -1f })
            {
                var settlement = Town(toward > 0f ? 3 : 4);
                var buildings = Port(settlement, toward);
                Assert.That(buildings.Shows(PortBuilding.Dock), Is.True);
                var shed = buildings.Root.Find("Dock/Ship shed");
                Assert.That(shed, Is.Not.Null, "the dock is a ship shed");
                Assert.That(buildings.Root.Find("Dock/Shear legs"), Is.Not.Null, "with its shear legs on the jetty");
                var corners = Corners(shed, buildings.Root.parent, toward);
                Assert.That(corners.Count, Is.GreaterThan(0));
                // A hull on the war line's slipway (u 6, up to 4.2 m either side of it with its scaffolding: u <= 10.8) and the
                // jetty with the porters on it (|z| < 2) stay clear; the roof stays low beside the ships.
                Assert.That(corners.Min(c => c.x), Is.GreaterThanOrEqualTo(11f), $"clear of a hull on the slipway ({toward})");
                Assert.That(corners.Max(c => c.z), Is.LessThan(-2.3f), $"off the jetty and the porters' path ({toward})");
                Assert.That(corners.Max(c => c.x), Is.LessThan(25f), "no further than the jetty's head toward the berth");
                Assert.That(corners.Max(c => c.y), Is.LessThan(6.5f), "a low roof");
                // Its ramp runs down into the sea, as Zea's did.
                Assert.That(corners.Min(c => c.y), Is.LessThan(-1f));
                var footprint = buildings.Footprint(PortBuilding.Dock);
                Assert.That(footprint.max.z, Is.LessThan(-0.9f));
                Assert.That(PortBuildings.Reserved(18f, PortBuildings.ShedZ), Is.True, "nothing else may stand in its place");
            }
            Assert.That(PortBuildings.Reserved(12.5f, 3f), Is.True, "the porters' pile by the store");
            Assert.That(PortBuildings.Reserved(1f, 0f), Is.True, "the town wall's front and gate");
            Assert.That(PortBuildings.Reserved(6f, 12f), Is.False);
            Assert.That(PortBuildings.Reserved(10f, 6f), Is.False);
        }

        [Test]
        public void Buildings_RiseByAssemblyAndNothingIsScaled()
        {
            foreach (var (kind, project) in new[] { (PortBuilding.Workshop, ProjectKind.Workshop), (PortBuilding.Town, ProjectKind.Town), (PortBuilding.SageSchool, ProjectKind.SageSchool), (PortBuilding.Dock, ProjectKind.Dock) })
            {
                var settlement = new Settlement { Id = 10 + (int)kind, HasWorkshop = kind != PortBuilding.Workshop, IsTown = kind != PortBuilding.Town };
                settlement.ProjectKind = project;
                settlement.Project = new BuildOrder { TotalTicks = 1000, DoneTicks = 300, Started = true };
                var buildings = Port(settlement);
                var structure = buildings.Root.Find(kind.ToString());
                int Shown() => structure.GetComponentsInChildren<MeshRenderer>().Length;
                var counts = new List<int>();
                foreach (var done in new[] { 300, 600, 900 })
                {
                    settlement.Project.DoneTicks = done;
                    buildings.Update(settlement, false, 0f);
                    Assert.That(buildings.Rising, Is.EqualTo(kind));
                    foreach (var part in structure.GetComponentsInChildren<Transform>(true))
                        Assert.That((part.localScale - Vector3.one).sqrMagnitude, Is.LessThan(1e-10f), $"{kind} at {done / 10}%: {part.name} is put up, not scaled");
                    counts.Add(Shown());
                }
                Assert.That(counts[1], Is.GreaterThan(counts[0]), $"{kind}: more of it stands at 60% than at 30%");
                Assert.That(counts[2], Is.GreaterThanOrEqualTo(counts[1]), $"{kind}: and more at 90%");
            }
        }

        [Test]
        public void Dock_BreaksElementByElementLikeTheRest()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            Assert.That(buildings.BreakablePieces(PortBuilding.Dock), Is.EqualTo(2), "the ship shed and the shear legs");
            settlement.Hull = settlement.MaxHull * 0.3;
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var views = buildings.DamagesOf(PortBuilding.Dock);
            Assert.That(views.Count, Is.EqualTo(2));
            foreach (var view in views)
            {
                Assert.That(view.Graph.Validate(), Is.Empty, $"{view.Frame.name}: everything rests on its foundation");
                Assert.That(view.Broken, Is.True, $"{view.Frame.name}: at 70% lost its elements fail");
            }
            var shed = views.Single(v => v.Frame.name.Contains("Ship shed")).Graph;
            Assert.That(shed.Elements.Count(e => e.Material == BuildMaterial.Stone && e.Detach == DetachMethod.Topple), Is.EqualTo(10), "ten limestone columns that go over");
            Assert.That(shed.Elements.Count(e => e.Role == ElementRole.Roof && e.Detach == DetachMethod.CaveIn), Is.EqualTo(8), "the roof in bays");
            Assert.That(shed.Elements.Single(e => e.Id == "wall.back").Detach, Is.EqualTo(DetachMethod.Crumble), "the back wall slumps into its stone");
            Assert.That(shed.Elements.Count(e => e.Role == ElementRole.Foundation), Is.EqualTo(1), "the ramp stays");
            var legs = views.Single(v => v.Frame.name.Contains("Shear legs")).Graph;
            Assert.That(legs.Elements.Count(e => e.Material == BuildMaterial.Timber && e.Detach == DetachMethod.Topple), Is.EqualTo(2), "two legs that go over");

            // Brought down whole, it lies in the ruin of its own pieces, as every building does.
            buildings.Update(settlement, true, 0f);
            Settle(buildings, settlement, true);
            Assert.That(buildings.ShowsRuin(PortBuilding.Dock), Is.True);
            Assert.That(buildings.Root.Find("Dock").gameObject.activeSelf, Is.False, "what stood is gone");
            foreach (var view in buildings.DamagesOf(PortBuilding.Dock))
                Assert.That(view.SettledPieces, Is.EqualTo(view.FailedCount), $"{view.Frame.name}: all at rest");
        }

        [Test]
        public void Camp_ABridgeheadIsAFieldCampThatBreaksToo()
        {
            var settlement = new Settlement { Id = 7, IsOutpost = true, HasShipyard = true };
            settlement.Hull = settlement.MaxHull;
            var buildings = Port(settlement);
            Assert.That(buildings.Shows(PortBuilding.Store), Is.True);
            Assert.That(buildings.Root.Find("Store/Palisade"), Is.Not.Null, "a palisade round the camp");
            Assert.That(buildings.Root.Find("Store").Cast<Transform>().Count(part => part.name == "Tent"), Is.EqualTo(2), "two tents");
            Assert.That(buildings.Root.Find("Store/Camp gear"), Is.Not.Null, "a boat on trestles, a hearth and supplies");
            Assert.That(buildings.Root.Find("Store/Store"), Is.Null, "no stone store at a bridgehead");
            var box = buildings.Footprint(PortBuilding.Store);
            Assert.That(box.min.x, Is.GreaterThanOrEqualTo(-12f));
            Assert.That(box.max.x, Is.LessThanOrEqualTo(0f), "on the landward half, the slipways and the jetty clear");
            Assert.That(Mathf.Max(-box.min.z, box.max.z), Is.LessThanOrEqualTo(7.5f));
            Assert.That(buildings.BreakablePieces(PortBuilding.Store), Is.EqualTo(3), "the palisade and the two tents");

            settlement.Hull = settlement.MaxHull * 0.3;
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var views = buildings.DamagesOf(PortBuilding.Store);
            Assert.That(views.Count, Is.EqualTo(3));
            foreach (var view in views)
            {
                Assert.That(view.Graph.Validate(), Is.Empty, $"{view.Frame.name}: everything rests on its foundation");
                Assert.That(view.Broken, Is.True, $"{view.Frame.name}: at 70% lost its elements fail");
            }
            var palisade = views.Single(v => v.Frame.name.Contains("Palisade")).Graph;
            Assert.That(palisade.Elements.Count(e => e.Material == BuildMaterial.Timber && e.Detach == DetachMethod.Topple), Is.GreaterThan(15), "the stakes go over stretch by stretch");
            var tent = views.First(v => v.Frame.name.Contains("Tent")).Graph;
            Assert.That(tent.Elements.Count(e => e.Role == ElementRole.Roof && e.Detach == DetachMethod.CaveIn), Is.EqualTo(2), "its cloth comes down a side at a time");

            buildings.Update(settlement, true, 0f);
            Settle(buildings, settlement, true);
            Assert.That(buildings.ShowsRuin(PortBuilding.Store), Is.True, "the camp lies flattened");
        }

        [Test]
        public void Workshop_IsAnErgasterionWithItsForge()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            var forge = buildings.Root.Find("Workshop/Shed/Floor/Forge and gear");
            Assert.That(forge, Is.Not.Null, "the forge and the gear go in last");
            var mesh = forge.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(5), "timber, stone, the bellows' leather and embers, iron, rope");
            // The hearth before the chimney's foot (-1.8, -1.3 in the shed's frame), its embers under the ash at 0.775 m.
            Assert.That(mesh.vertices.Any(v => Mathf.Abs(v.y - 0.775f) < 1e-3f && Mathf.Abs(v.x + 1f) <= 0.31f && Mathf.Abs(v.z + 1.3f) <= 0.31f), Is.True, "the hearth's embers");
            settlement.Hull = settlement.MaxHull * 0.3;
            buildings.Update(settlement, false, 0f);
            Settle(buildings, settlement);
            var shed = buildings.DamageOf(PortBuilding.Workshop).Graph;
            Assert.That(shed.Elements.Single(e => e.Id == "gear").Role, Is.EqualTo(ElementRole.Foundation), "the forge stays on the floor when the shed falls");
            Assert.That(shed.Elements.Count(e => e.Material == BuildMaterial.Timber && e.Detach == DetachMethod.Topple), Is.EqualTo(6), "six posts that go over");
        }

        [Test]
        public void Rubble_IsTheSameOnEverySystem()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            var shapes = new List<string>();
            try
            {
                foreach (var name in new[] { "pl-PL", "en-US" })
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
                    var settlement = Town(20);
                    var buildings = Port(settlement);
                    settlement.Hull = 0;
                    buildings.Update(settlement, true, 0f);
                    Settle(buildings, settlement, true);
                    var signature = new StringBuilder();
                    foreach (var kind in new[] { PortBuilding.Store, PortBuilding.Town, PortBuilding.Workshop })
                    foreach (var view in buildings.DamagesOf(kind))
                    foreach (var filter in view.Frame.GetComponentsInChildren<MeshFilter>(true).OrderBy(item => item.name))
                    {
                        if (filter.sharedMesh == null) continue;
                        var sum = Vector3.zero;
                        foreach (var v in filter.sharedMesh.vertices) sum += v;
                        signature.Append(filter.name).Append(':').Append(filter.sharedMesh.vertexCount).Append(':')
                            .Append(sum.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(sum.y.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
                    }
                    shapes.Add(signature.ToString());
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
            Assert.That(shapes[0].Length, Is.GreaterThan(0));
            Assert.That(shapes[1], Is.EqualTo(shapes[0]), "the houses' stumps and heaps come out the same with a decimal comma as with a point");
        }

        [Test]
        public void Jetty_TakesTheOldJettysPlaceAndKeepsThePortersPath()
        {
            var land = new GameObject("Shore under test");
            lands.Add(land);
            foreach (var toward in new[] { 1f, -1f })
            {
                var jetty = PortJetty.Build(land.transform, toward, 14f, 23.5f, null);
                Assert.That(jetty.name, Is.EqualTo("Jetty"));
                Assert.That(jetty.GetComponents<MeshRenderer>().Length, Is.EqualTo(1), "one mesh, one draw a material");
                var vertices = jetty.GetComponent<MeshFilter>().sharedMesh.vertices;
                var u = vertices.Select(v => v.x * toward).ToArray();
                Assert.That(u.Min(), Is.EqualTo(14f).Within(0.05f), "from the beach");
                Assert.That(u.Max(), Is.EqualTo(23.5f).Within(0.05f), "to beside a moored hull, short of the port point");
                Assert.That(vertices.Max(v => Mathf.Abs(v.z)), Is.LessThanOrEqualTo(PortJetty.Width * 0.5f + 1e-3f));
                // The deck where the porters walk lies where it lay (BattleController.PortPlaces walks it at 1.4 m), and nothing
                // stands on their path between the kerbs short of the bollards at the head.
                Assert.That(vertices.Any(v => Mathf.Abs(v.y - PortJetty.Top) < 1e-3f && v.x * toward > 15f && v.x * toward < 22.5f), Is.True, "the planks' top");
                var path = vertices.Where(v => Mathf.Abs(v.z) < 1.85f && v.x * toward > 14.2f && v.x * toward < 22.8f).ToList();
                Assert.That(path.Count, Is.GreaterThan(0));
                Assert.That(path.Max(v => v.y), Is.LessThanOrEqualTo(PortJetty.Top + 1e-3f), "nothing stands on the porters' path");
                Assert.That(vertices.Min(v => v.y), Is.LessThan(-2f), "its piles stand on the bed");
            }
        }
    }
}
