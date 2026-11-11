using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The port's buildings of the 4th century BC (PortBuildings): masonry laid course by course while a building rises
    /// and one mesh once it stands, the Sages' School as a Doric peristyle on its stylobate, ashlar courses running level,
    /// the store's yard clear of the doors. Presentation only: a settlement read, nothing in it changed.
    /// </summary>
    public sealed class PortMasonryTests
    {
        GameObject land;

        [TearDown]
        public void Clear()
        {
            if (land == null) return;
            foreach (var owned in land.GetComponentsInChildren<OwnedMeshes>(true))
                if (owned.Meshes != null)
                    foreach (var mesh in owned.Meshes) if (mesh != null) Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(land);
        }

        PortBuildings Port(Settlement settlement)
        {
            land = new GameObject("Land under test");
            var buildings = new PortBuildings(land.transform, null, settlement, 1f, null, null);
            buildings.Update(settlement, false, 0f);
            return buildings;
        }

        static Settlement Town() => new Settlement { Id = 5, HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true };

        [Test]
        public void Port_BuildingsRiseCourseByCourseAndStandAsOneMesh()
        {
            var settlement = Town();
            settlement.HasSageSchool = false;
            settlement.ProjectKind = ProjectKind.SageSchool;
            settlement.Project = new BuildOrder { TotalTicks = 1000, DoneTicks = 300, Started = true };
            var buildings = Port(settlement);
            var crepis = buildings.Root.Find("SageSchool/School/Crepis");
            Assert.That(crepis, Is.Not.Null, "the School stands on its crepis");
            var whole = crepis.Find("Colonnade and hall");
            var courses = crepis.Cast<Transform>().Where(part => part != whole).ToList();
            int Laid() => courses.Count(part => part.gameObject.activeSelf);

            Assert.That(courses.Count, Is.EqualTo(12), "four sides of columns, six courses of the hall, architrave, frieze and cornice");
            var early = Laid();
            Assert.That(early, Is.InRange(1, 6), "at 30% the first columns stand");
            Assert.That(whole.gameObject.activeSelf, Is.False, "while it rises the courses show, not the whole");
            Assert.That(courses[0].name, Does.StartWith("Columns"), "the columns go up first");

            settlement.Project.DoneTicks = 700;
            buildings.Update(settlement, false, 0f);
            Assert.That(Laid(), Is.GreaterThan(early).And.LessThan(courses.Count), "at 70% more is laid, the cornice not yet");
            Assert.That(buildings.RoofOn, Is.False);

            settlement.HasSageSchool = true;
            settlement.Project = null;
            settlement.ProjectKind = ProjectKind.None;
            buildings.Update(settlement, false, 0f);
            Assert.That(Laid(), Is.Zero, "standing, the courses give way to one mesh");
            Assert.That(whole.gameObject.activeSelf, Is.True);
            var box = buildings.Footprint(PortBuilding.SageSchool);
            Assert.That(box.size.x, Is.EqualTo(5f).Within(1e-4f), "the School keeps its place on the land");
            Assert.That(box.size.z, Is.EqualTo(8.2f).Within(1e-4f));
        }

        [Test]
        public void Port_AFallenTownCrumblesIntoItsRuinsInGameTime()
        {
            var settlement = Town();
            var buildings = Port(settlement);
            var school = buildings.Root.Find("SageSchool");

            buildings.Update(settlement, true, 0f);
            Assert.That(buildings.Ruined, Is.True);
            Assert.That(buildings.Collapsing, Is.True, "what stood falls, it does not vanish");
            Assert.That(buildings.Shows(PortBuilding.SageSchool), Is.False);
            Assert.That(school.gameObject.activeSelf, Is.True, "still standing as it gives");
            Assert.That(buildings.ShowsRuin(PortBuilding.SageSchool), Is.False, "the ruin shows later, through the dust");
            var fall = buildings.DamageOf(PortBuilding.SageSchool);
            Assert.That(fall.Busy, Is.True, "the School comes down element by element (BuildingDestructionTests)");

            var settled = fall.SettledPieces;
            buildings.Update(settlement, true, 0f);
            Assert.That(fall.SettledPieces, Is.EqualTo(settled), "a pause holds the fall");

            buildings.Update(settlement, true, PortBuildings.CollapseSeconds * 0.5f);
            Assert.That(buildings.DamagesOf(PortBuilding.Dock).Any(view => view.Broken), Is.True, "the dock's shed and shear legs come down element by element too");
            Assert.That(buildings.ShowsRuin(PortBuilding.SageSchool), Is.True, "its first columns lie at rest: the ruin shows");

            for (var i = 0; i < 20 && buildings.Collapsing; i++) buildings.Update(settlement, true, PortBuildings.CollapseSeconds);
            Assert.That(buildings.Collapsing, Is.False);
            Assert.That(school.gameObject.activeSelf, Is.False, "fallen");
            Assert.That(buildings.ShowsRuin(PortBuilding.SageSchool) && buildings.ShowsRuin(PortBuilding.Store) && buildings.ShowsRuin(PortBuilding.Dock), Is.True,
                "what stood lies in ruins, the dock with the rest");

            buildings.Update(settlement, false, 0f);
            Assert.That(buildings.Shows(PortBuilding.SageSchool), Is.True, "raised again, it stands where it stood");
            Assert.That(buildings.ShowsRuin(PortBuilding.SageSchool), Is.False);
        }

        [Test]
        public void Port_SchoolIsADoricPeristyleOnItsStylobate()
        {
            var buildings = Port(Town());
            var colonnade = buildings.Root.Find("SageSchool/School/Crepis/Colonnade and hall").GetComponent<MeshFilter>().sharedMesh;
            var bounds = colonnade.bounds;
            // On the stylobate 1 m up, within the School's 5 by 8.2 m place: columns 3.3 m, architrave, frieze and cornice.
            Assert.That(bounds.min.y, Is.EqualTo(1f).Within(1e-3f), "standing on the stylobate");
            Assert.That(bounds.size.y, Is.EqualTo(3.3f + 0.42f + 0.48f + 0.2f).Within(1e-3f));
            Assert.That(bounds.extents.x, Is.LessThanOrEqualTo(2.5f));
            Assert.That(bounds.extents.z, Is.LessThanOrEqualTo(4.1f));
            Assert.That(colonnade.subMeshCount, Is.EqualTo(5), "stucco, stone, the door's dark, red ochre and Egyptian blue");

            // The corner column's foot: a fluted shaft 0.52 m across at (1.75, -3.35), without a base, inside the stylobate.
            var vertices = colonnade.vertices;
            var foot = vertices.Where(v => v.y < 1f + 1e-3f && Mathf.Abs(v.x - 1.75f) < 0.4f && Mathf.Abs(v.z + 3.35f) < 0.4f).ToList();
            Assert.That(foot.Count, Is.GreaterThanOrEqualTo(40), "twenty flutes: arrises and hollows round the foot");
            var reach = foot.Max(v => new Vector2(v.x - 1.75f, v.z + 3.35f).magnitude);
            Assert.That(reach, Is.EqualTo(0.26f).Within(0.005f));
            var shallowest = foot.Min(v => new Vector2(v.x - 1.75f, v.z + 3.35f).magnitude);
            Assert.That(shallowest / reach, Is.EqualTo(0.92f).Within(0.01f), "shallow flutes meeting at sharp arrises");
            Assert.That(3.3f / (2f * reach), Is.InRange(5.5f, 7f), "as slender as the Doric of the later 4th century");
            Assert.That(1.75f + reach, Is.LessThan(2.1f), "the foot within the stylobate's edge");
        }

        [Test]
        public void Port_AshlarCoursesRunLevelOnEveryUprightFace()
        {
            var buildings = Port(Town());
            foreach (var path in new[] { "Store/Store/Socle", "Town/Wall/Wall and towers", "SageSchool/School/Crepis" })
            {
                var mesh = buildings.Root.Find(path).GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var uvs = mesh.uv;
                var upright = 0;
                for (var i = 0; i < vertices.Length; i++)
                {
                    if (Mathf.Abs(normals[i].y) >= 0.5f) continue;
                    upright++;
                    Assert.That(uvs[i].y, Is.EqualTo(vertices[i].y).Within(1e-3f), $"{path}: v is the height on an upright face, so courses meet level");
                }
                Assert.That(upright, Is.GreaterThan(0), path);
            }
        }

        [Test]
        public void Port_StoreYardKeepsClearOfTheDoorsAndInsideTheWalls()
        {
            var buildings = Port(Town());
            var yard = buildings.Root.Find("Store/Store yard");
            Assert.That(yard, Is.Not.Null, "jars, amphorae and anchors before the store");
            var vertices = yard.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(vertices.Length, Is.GreaterThan(0));
            foreach (var v in vertices)
            {
                // The store's door (z 4.9 ± 0.67) and the house's (z -1.6 ± 0.67), out to the town wall's front.
                var beforeStore = v.x > -1.3f && v.x < 0.6f;
                Assert.That(beforeStore && v.z > 4.23f && v.z < 5.57f, Is.False, $"clear of the store's door: {v}");
                var beforeHouse = v.x > -1.6f && v.x < 0.6f;
                Assert.That(beforeHouse && v.z > -2.27f && v.z < -0.93f, Is.False, $"clear of the house's door: {v}");
                Assert.That(Mathf.Abs(v.z), Is.LessThan(8.75f), "inside the town wall");
                Assert.That(v.x, Is.LessThan(0.15f), "inside the town wall's front");
            }
            var store = buildings.Footprint(PortBuilding.Store);
            Assert.That(store.max.x, Is.EqualTo(-3.8f + 2.5f).Within(1e-4f), "the yard is no piece: the store's place on the land is its houses'");
        }
    }
}
