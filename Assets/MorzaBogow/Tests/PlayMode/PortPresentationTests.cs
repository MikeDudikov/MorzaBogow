using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// Stage 3c part 2 (docs/UZBROJENIE.md): the buildings on a port's land, their construction and ruins, a ram's called
    /// charge and the firing arcs of selected ships, and the new class icons. The views only read the simulation.
    /// </summary>
    public sealed class PortPresentationTests
    {
        GameObject host;
        BattleController battle;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Battle under test");
            battle = host.AddComponent<BattleController>();
            battle.Paused = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        }

        Settlement Home => battle.World.SettlementOf(BattleController.PlayerFaction);

        PortBuildings HomeBuildings => battle.PortBuildings[Home.Id];

        /// <summary>Open sea near the home port, clear of land for a box round it, where two ships meet in the player's sight.</summary>
        Vector3 OpenWater()
        {
            var home = Home;
            for (var radius = 120f; radius < 900f; radius += 40f)
            for (var angle = 0f; angle < 360f; angle += 20f)
            {
                var x = (float)home.X + Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                var z = (float)home.Y + Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                var open = true;
                for (var dx = -70f; dx <= 70f && open; dx += 10f)
                for (var dz = -40f; dz <= 40f && open; dz += 10f)
                    open = !battle.World.OnLand(x + dx, z + dz);
                if (open) return new Vector3(x, 0f, z);
            }
            Assert.Fail("No open water near the home port.");
            return default;
        }

        [UnityTest]
        public IEnumerator PortBuildings_FollowTheSettlementsBuildings()
        {
            yield return null;
            var home = Home;
            battle.RunTicks(0);
            var buildings = HomeBuildings;
            Assert.That(buildings.Shows(PortBuilding.Store), Is.True, "A port always has its store.");
            foreach (var kind in new[] { PortBuilding.Workshop, PortBuilding.Town, PortBuilding.SageSchool, PortBuilding.Dock })
                Assert.That(buildings.Shows(kind), Is.False, $"No {kind} before it is built.");

            home.HasWorkshop = true;
            battle.RunTicks(0);
            Assert.That(buildings.Shows(PortBuilding.Workshop), Is.True);
            Assert.That(buildings.Shows(PortBuilding.Town), Is.False);
            home.IsTown = home.HasSageSchool = home.HasDock = true;
            battle.RunTicks(0);
            foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
                Assert.That(buildings.Shows(kind), Is.True, $"{kind} stands once the settlement has it.");
            Assert.That(battle.SettlementViews[home.Id].transform.Find("Buildings"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PortBuildings_KeepClearOfTheJettySlipwaysAndStoreDoor()
        {
            yield return null;
            // Both home ports, the player's and the enemy's on the other side of the map, whether in sight or not.
            foreach (var settlement in battle.World.Settlements)
            {
                settlement.HasWorkshop = settlement.IsTown = settlement.HasSageSchool = settlement.HasDock = true;
                var toward = Mathf.Sign((float)settlement.X - (float)Rules.SettlementLandX(settlement));
                var land = new GameObject("Land under test");
                var buildings = new PortBuildings(land.transform, null, settlement, toward, null, null);
                buildings.Update(settlement, false, 0f);
                foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
                {
                    var box = buildings.Footprint(kind);
                    Assert.That(box.size.x, Is.GreaterThan(0f), $"{kind} has a place on the land.");
                    // Along u (toward the port) and z (along the shore).
                    var uMin = Mathf.Min(box.min.x * toward, box.max.x * toward);
                    var uMax = Mathf.Max(box.min.x * toward, box.max.x * toward);
                    bool Overlaps(float u0, float u1, float z0, float z1) => uMax > u0 && uMin < u1 && box.max.z > z0 && box.min.z < z1;
                    if (kind == PortBuilding.Dock)
                    {
                        Assert.That(box.max.z, Is.LessThan(-0.9f), "The shear legs stand beside the porters' path along the jetty's middle.");
                        Assert.That(uMax, Is.LessThan(25f), "They stand on the jetty and lean over the berth.");
                    }
                    else
                    {
                        Assert.That(uMax, Is.LessThanOrEqualTo(PortBuildings.PlotNear + 1.2f), $"{kind} keeps to the landward plot.");
                        Assert.That(uMin, Is.GreaterThanOrEqualTo(PortBuildings.PlotFar - 1.2f), $"{kind} keeps to the landward plot.");
                        Assert.That(Overlaps(14f, 23.5f, -2f, 2f), Is.False, $"{kind} is not on the jetty.");
                    }
                    var slip = (float)Rules.SlipAlong;
                    Assert.That(Overlaps(slip - 2.1f, slip + 2.1f, 1f, 27f) || Overlaps(slip - 2.1f, slip + 2.1f, -27f, -1f), Is.False, $"{kind} is clear of the slipways.");
                    Assert.That(Overlaps(12f, 13f, 2.5f, 3.5f), Is.False, $"{kind} is clear of the store door the gangs walk to.");
                }
                Object.Destroy(land);
            }
        }

        [UnityTest]
        public IEnumerator PortBuildings_AProjectRisesWithItsProgress()
        {
            yield return null;
            var home = Home;
            battle.RunTicks(0);
            var buildings = HomeBuildings;
            home.Project = new BuildOrder { TotalTicks = 1000, DoneTicks = 100 };
            home.ProjectKind = ProjectKind.Workshop;
            battle.RunTicks(0);
            Assert.That(buildings.Rising, Is.EqualTo(PortBuilding.Workshop));
            Assert.That(buildings.Shows(PortBuilding.Workshop), Is.False, "A rising building does not count as standing.");
            Assert.That(buildings.Scaffolded, Is.True);
            Assert.That(buildings.RoofOn, Is.False);
            Assert.That(buildings.WorkSpots.Count, Is.GreaterThan(0), "Builders would have places to work.");

            home.Project.DoneTicks = 900;
            battle.RunTicks(0);
            Assert.That(buildings.RisingProgress, Is.EqualTo(0.9f).Within(1e-3f));
            Assert.That(buildings.RoofOn, Is.True, "The roof goes on from 80%.");
            Assert.That(buildings.Scaffolded, Is.True);

            home.Project.DoneTicks = 990;
            battle.RunTicks(0);
            Assert.That(buildings.Scaffolded, Is.False, "The scaffolding is down at 97%.");

            home.Project = null;
            home.ProjectKind = ProjectKind.None;
            home.HasWorkshop = true;
            battle.RunTicks(0);
            Assert.That(buildings.Rising, Is.Null);
            Assert.That(buildings.Shows(PortBuilding.Workshop), Is.True);
            Assert.That(buildings.WorkSpots.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PortBuildings_ResearchLightsAFireAndRaisesNothing()
        {
            yield return null;
            var home = Home;
            home.HasWorkshop = home.IsTown = home.HasSageSchool = true;
            home.Project = new BuildOrder { TotalTicks = 1000, DoneTicks = 400 };
            home.ProjectKind = ProjectKind.HeavyArmsResearch;
            battle.RunTicks(0);
            var buildings = HomeBuildings;
            Assert.That(buildings.ResearchFire, Is.True, "A fire burns before the School while the sages research.");
            Assert.That(buildings.Rising, Is.Null, "Research raises no building.");
            Assert.That(buildings.Scaffolded, Is.False);

            home.Project = null;
            home.ProjectKind = ProjectKind.None;
            battle.RunTicks(0);
            Assert.That(buildings.ResearchFire, Is.False);
        }

        [UnityTest]
        public IEnumerator PortBuildings_AFallenSettlementLeavesRuinsUntilAnotherRises()
        {
            yield return null;
            var world = battle.World;
            var site = world.Sites.Where(world.IsSiteFree).OrderBy(item => World.Distance(item.X, item.Y, Home.X, Home.Y)).First();
            var fallen = new Settlement
            {
                Id = world.NextId++, Owner = BattleController.PlayerFaction, X = site.X, Y = site.Y, IsOutpost = true, HasShipyard = true,
                HasWorkshop = true, Hull = 0,
            };
            world.Settlements.Add(fallen);
            site.SettlementId = fallen.Id;
            battle.RunTicks(0);
            var view = battle.SettlementViews[fallen.Id];
            var buildings = battle.PortBuildings[fallen.Id];
            Assert.That(view.activeSelf, Is.True, "The fallen settlement's land stays.");
            Assert.That(buildings.Ruined, Is.True);
            Assert.That(buildings.ShowsRuin(PortBuilding.Store) && buildings.ShowsRuin(PortBuilding.Workshop), Is.True, "What stood lies in ruins.");
            Assert.That(buildings.Shows(PortBuilding.Store), Is.False);
            Assert.That(FreeShoreShownAt(view.transform.position), Is.False, "No second islet under the ruins.");

            // A new bridgehead on the same shore takes the place; the ruins go with the old view.
            var rebuilt = new Settlement { Id = world.NextId++, Owner = BattleController.PlayerFaction, X = site.X, Y = site.Y, IsOutpost = true, HasShipyard = false, Hull = Rules.OutpostHull };
            world.Settlements.Add(rebuilt);
            site.SettlementId = rebuilt.Id;
            battle.RunTicks(0);
            Assert.That(view.activeSelf, Is.False);
            Assert.That(battle.SettlementViews[rebuilt.Id].activeSelf, Is.True);
        }

        static bool FreeShoreShownAt(Vector3 at) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Any(item => item.name.StartsWith("Free shore") && Vector2.Distance(new Vector2(item.position.x, item.position.z), new Vector2(at.x, at.z)) < 1f);

        [UnityTest]
        public IEnumerator RamTelegraph_DrawnForBothSidesOnlyWhileTheChargeIsCalled()
        {
            yield return null;
            var world = battle.World;
            var sea = OpenWater();
            var rammer = world.SpawnShip(BattleController.PlayerFaction, ShipClass.Ram, sea.x - 20, sea.z);
            var prey = world.SpawnShip(1 - BattleController.PlayerFaction, ShipClass.CrewDestroyer, sea.x + 20, sea.z);
            battle.RunTicks(1);
            var view = battle.RamTelegraph;
            Assert.That(view, Is.Not.Null);

            rammer.Order = OrderKind.Ram;
            rammer.TargetId = prey.Id;
            rammer.RamState = RamPhase.Approach;
            battle.RunTicks(0);
            Assert.That(view.ShowsFor(rammer.Id), Is.False, "Nothing before the charge is called.");

            rammer.RamState = RamPhase.Telegraph;
            rammer.TelegraphTicks = RamTelegraphView.TelegraphTicksTotal;
            battle.RunTicks(0);
            Assert.That(view.ShowsFor(rammer.Id), Is.True);
            Assert.That(view.ShownAsOwn(rammer.Id), Is.True, "Our own charge in our colour.");
            Assert.That(view.FuseShownFor(rammer.Id), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(view.Calls, Is.EqualTo(1), "The call flashes once.");

            rammer.TelegraphTicks = RamTelegraphView.TelegraphTicksTotal / 2;
            battle.RunTicks(0);
            Assert.That(view.FuseShownFor(rammer.Id), Is.EqualTo(0.5f).Within(0.02f), "The fuse runs with the telegraph.");
            rammer.TelegraphTicks = 0;
            battle.RunTicks(0);
            Assert.That(view.FuseShownFor(rammer.Id), Is.EqualTo(1f), "Full when the blow may land.");
            Assert.That(view.Calls, Is.EqualTo(1));

            // The enemy's charge at our ship is drawn too, in the enemy's colour.
            prey.Order = OrderKind.Ram;
            prey.TargetId = rammer.Id;
            prey.RamState = RamPhase.Telegraph;
            prey.TelegraphTicks = RamTelegraphView.TelegraphTicksTotal;
            battle.RunTicks(0);
            Assert.That(view.ShowsFor(prey.Id), Is.True);
            Assert.That(view.ShownAsOwn(prey.Id), Is.False);

            rammer.RamState = RamPhase.Recoil;
            battle.RunTicks(0);
            Assert.That(view.ShowsFor(rammer.Id), Is.False, "Gone once the charge is resolved.");
        }

        [UnityTest]
        public IEnumerator RamTelegraph_AHiddenRammerDrawsNothing()
        {
            yield return null;
            var world = battle.World;
            var enemyHome = world.SettlementOf(1 - BattleController.PlayerFaction);
            var rammer = world.SpawnShip(1 - BattleController.PlayerFaction, ShipClass.Ram, enemyHome.X, enemyHome.Y + 60);
            var target = world.SpawnShip(1 - BattleController.PlayerFaction, ShipClass.Cutter, enemyHome.X, enemyHome.Y + 100);
            battle.RunTicks(1);
            Assert.That(world.CanSee(BattleController.PlayerFaction, rammer), Is.False, "Set up out of the player's sight.");
            rammer.Order = OrderKind.Ram;
            rammer.TargetId = target.Id;
            rammer.RamState = RamPhase.Telegraph;
            rammer.TelegraphTicks = 10;
            battle.RunTicks(0);
            Assert.That(battle.RamTelegraph.ShowsFor(rammer.Id), Is.False, "The fog of war hides the charge as it hides the ship.");
            Assert.That(battle.RamTelegraph.Calls, Is.Zero);
        }

        [Test]
        public void FiringArcs_CoverExactlyWhatTheGameReaches()
        {
            var ship = new Ship { X = 0, Y = 0, Heading = 0 };
            var checkedPoints = 0;
            foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
            {
                if (type == WeaponType.None) continue;
                var weapon = WeaponCatalog.Get(type);
                for (var bearing = -180f; bearing <= 180f; bearing += 7.3f)
                for (var distance = 0.6f; distance < weapon.MaxRange + 30; distance += 4.7f)
                {
                    var radians = bearing * Mathf.Deg2Rad;
                    var game = World.InReach(ship, weapon, Mathf.Cos(radians) * distance, Mathf.Sin(radians) * distance);
                    Assert.That(FiringArcs.Covers(weapon, radians, distance), Is.EqualTo(game), $"{type} at {bearing:0.0}° and {distance:0.0} m");
                    checkedPoints++;
                }
            }
            Assert.That(checkedPoints, Is.GreaterThan(1000));
        }

        [UnityTest]
        public IEnumerator FiringArcs_DrawEachPostAndMarkTheUnmannedAndBroken()
        {
            yield return null;
            var world = battle.World;
            var sea = OpenWater();
            var bomber = world.SpawnShip(BattleController.PlayerFaction, ShipClass.Bomber, sea.x, sea.z);
            var killer = world.SpawnShip(BattleController.PlayerFaction, ShipClass.ShipKiller, sea.x + 40, sea.z);
            battle.RunTicks(1);
            battle.SelectForTest(bomber.Id);
            battle.SelectForTest(killer.Id);
            battle.RunTicks(0);
            var arcs = battle.FiringArcs;
            Assert.That(arcs.ShowsFor(bomber.Id) && arcs.ShowsFor(killer.Id), Is.True);
            Assert.That(arcs.StatesFor(bomber.Id), Is.EqualTo(new MountState?[] { MountState.Ready, MountState.Ready }));

            // The mesh spans the catapults' sector: 60–260 m, 60° to each side of the bow.
            var mesh = arcs.MeshFor(bomber.Id);
            var radii = mesh.vertices.Select(v => new Vector2(v.x, v.z).magnitude).ToArray();
            var bearings = mesh.vertices.Select(v => Mathf.Abs(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg)).ToArray();
            var catapult = WeaponCatalog.Get(WeaponType.HeavyCatapult);
            Assert.That(radii.Max(), Is.EqualTo((float)catapult.MaxRange).Within(0.01f));
            Assert.That(radii.Min(), Is.EqualTo((float)catapult.MinRange).Within(0.01f));
            Assert.That(bearings.Max(), Is.EqualTo((float)catapult.ArcDegrees * 0.5f).Within(1f));

            // One ballista broken, the other short of its crew: both marked, as the ship card names them.
            killer.ModuleHp[0] = 0;
            killer.Swordsmen = WeaponCatalog.Get(WeaponType.DeckBallista).Crew;
            killer.Javelins = killer.Archers = 0;
            battle.RunTicks(0);
            Assert.That(arcs.StatesFor(killer.Id), Is.EqualTo(new MountState?[] { MountState.Destroyed, MountState.Unmanned }));
            Assert.That(arcs.MeshFor(killer.Id).GetTriangles(3).Length, Is.GreaterThan(0), "The unmanned post's band is drawn.");
            Assert.That(arcs.MeshFor(killer.Id).GetTriangles(4).Length, Is.GreaterThan(0), "The broken post's band is drawn.");
            Assert.That(arcs.MeshFor(killer.Id).GetTriangles(2).Length, Is.Zero, "No post is ready.");

            // An enemy ship's arcs are never drawn, nor any when nothing is selected.
            var enemy = world.SpawnShip(1 - BattleController.PlayerFaction, ShipClass.Bomber, sea.x + 80, sea.z);
            battle.RunTicks(1);
            battle.SelectForTest(enemy.Id);
            battle.RunTicks(0);
            Assert.That(arcs.ShowsFor(enemy.Id), Is.False);
            battle.ClearSelectionKeepPort();
            battle.RunTicks(0);
            Assert.That(arcs.Shown, Is.Zero);
        }

        [Test]
        public void ClassIcons_TheNewClassesHaveIconsOfTheirOwn()
        {
            var icons = System.Enum.GetValues(typeof(ShipClass)).Cast<ShipClass>().Select(BattleHud.ClassIcon).ToArray();
            Assert.That(icons.Distinct().Count(), Is.EqualTo(icons.Length), "Every class has its own icon.");
            Assert.That(BattleHud.ClassIcon(ShipClass.ShipKiller), Is.EqualTo("ship-killer"));
            Assert.That(BattleHud.ClassIcon(ShipClass.Bomber), Is.EqualTo("bomber"));
            foreach (var name in icons)
            foreach (var size in new[] { 64, 128 })
                Assert.That(Resources.Load<Texture2D>($"Hud/Icons/{name}-{size}"), Is.Not.Null, $"{name}-{size} is in the build.");
        }

        [UnityTest]
        public IEnumerator PortViews_LeaveTheSimulationUntouched()
        {
            yield return null;
            var world = battle.World;
            var home = Home;
            home.HasWorkshop = home.IsTown = true;
            home.Project = new BuildOrder { TotalTicks = 1000, DoneTicks = 500 };
            home.ProjectKind = ProjectKind.SageSchool;
            var sea = OpenWater();
            var rammer = world.SpawnShip(BattleController.PlayerFaction, ShipClass.Ram, sea.x - 20, sea.z);
            var prey = world.SpawnShip(1 - BattleController.PlayerFaction, ShipClass.CrewDestroyer, sea.x + 20, sea.z);
            battle.RunTicks(1);
            rammer.Order = OrderKind.Ram;
            rammer.TargetId = prey.Id;
            rammer.RamState = RamPhase.Telegraph;
            rammer.TelegraphTicks = 12;
            battle.SelectForTest(rammer.Id);
            var before = SaveGame.Serialize(world);
            for (var i = 0; i < 5; i++) battle.RunTicks(0);
            Assert.That(battle.RamTelegraph.ShowsFor(rammer.Id) && battle.FiringArcs.ShowsFor(rammer.Id), Is.True);
            Assert.That(battle.PortBuildings[home.Id].Rising, Is.EqualTo(PortBuilding.SageSchool));
            Assert.That(SaveGame.Serialize(world), Is.EqualTo(before), "Drawing the port, the charge and the arcs changes nothing in the game.");
        }
    }
}
