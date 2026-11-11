using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Pictures of stage 3c part 2 appended to -morzaShowcase (docs/UZBROJENIE.md): the home port grown into a town with
    /// its workshop, school and dock while the sages research; a town rising among scaffolding; the ruins of a fallen
    /// bridgehead; a called charge of each side; the firing arcs of a ship destroyer and a bomber with a post short of
    /// its crew and a broken one; the new class icons in the order menu. Staged: the scenes set the settlement's
    /// buildings and the ships' state directly, as the other staged pictures do; nothing here is a game rule.
    /// </summary>
    public sealed partial class BattleController
    {
        Ship showcaseRammer, showcaseEnemyRammer;

        /// <summary>The camera on a settlement's land, from the harbour side, close enough for its buildings.</summary>
        void FramePort(Settlement port, float zoom)
        {
            var landX = (float)Rules.SettlementLandX(port);
            pivot = new Vector3((landX * 0.8f + (float)port.X * 0.2f), 0f, (float)port.Y);
            height = zoom; yaw = port.X > landX ? -60f : 120f; tilt = ObliqueCamera.DefaultTilt;
        }

        static void LogPort(string picture, PortBuildings buildings)
        {
            if (buildings == null) return;
            Debug.Log($"[Showcase] {picture}: store {buildings.Shows(PortBuilding.Store)}, workshop {buildings.Shows(PortBuilding.Workshop)}, town {buildings.Shows(PortBuilding.Town)}, " +
                      $"school {buildings.Shows(PortBuilding.SageSchool)}, dock {buildings.Shows(PortBuilding.Dock)}, rising {buildings.Rising?.ToString() ?? "-"} {buildings.RisingProgress:0.00}, " +
                      $"scaffold {buildings.Scaffolded}, fire {buildings.ResearchFire}, ruined {buildings.Ruined}");
        }

        IEnumerable<ShowcaseStep> PortShowcase(Settlement home, Vector3 area)
        {
            // The home port at its fullest: workshop, town wall and houses, the School with its fire, the dock's shear legs.
            yield return new ShowcaseStep { Shot = "port-miasto", Ticks = 30, Setup = () =>
            {
                hud?.ClosePopoverForTest();
                selected.Clear();
                home.HasWorkshop = home.IsTown = home.HasSageSchool = home.HasDock = true;
                home.Project = new BuildOrder { TotalTicks = 100000, DoneTicks = 1000 };
                home.ProjectKind = ProjectKind.HeavyArmsResearch;
                FramePort(home, 55f);
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () => LogPort("port-miasto", portBuildings.TryGetValue(home.Id, out var b) ? b : null) };

            // The dock up close: the ship shed of Zea's kind beside the war line's slipway and the jetty, the shear legs on its head.
            yield return new ShowcaseStep { Shot = "dok-hangar", Ticks = 30, Setup = () =>
            {
                var landX = (float)Rules.SettlementLandX(home);
                var toward = Mathf.Sign((float)home.X - landX);
                pivot = new Vector3(landX + toward * 17f, 0f, (float)home.Y + MorzaBogow.Presentation.PortBuildings.ShedZ * 0.6f);
                height = 36f; yaw = home.X > landX ? -35f : 145f; tilt = ObliqueCamera.DefaultTilt;
            } };

            // A bridgehead on the free shore nearest home: its field camp, with a cutter of ours in sight of it.
            yield return new ShowcaseStep { Shot = "przyczolek-oboz", Ticks = 30, Setup = () =>
            {
                var site = World.Sites.Where(World.IsSiteFree).OrderBy(item => World.Distance(item.X, item.Y, home.X, home.Y)).FirstOrDefault();
                if (site == null) return;
                var camp = new Settlement { Id = World.NextId++, Owner = PlayerFaction, X = site.X, Y = site.Y, IsOutpost = true, HasShipyard = true };
                camp.Hull = camp.MaxHull;
                World.Settlements.Add(camp);
                site.SettlementId = camp.Id;
                World.SpawnShip(PlayerFaction, ShipClass.Cutter, site.X + (site.X < 0 ? 30 : -30), site.Y - 25);
                FramePort(camp, 48f);
            } };

            // The town rising: walls and houses half up among scaffolding, the workshop already standing.
            yield return new ShowcaseStep { Shot = "port-budowa", Ticks = 30, Setup = () =>
            {
                home.IsTown = home.HasSageSchool = home.HasDock = false;
                home.Project = new BuildOrder { TotalTicks = 100000, DoneTicks = 52000 };
                home.ProjectKind = ProjectKind.Town;
                FramePort(home, 55f);
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () =>
            {
                LogPort("port-budowa", portBuildings.TryGetValue(home.Id, out var b) ? b : null);
                home.Project = null;
                home.ProjectKind = ProjectKind.None;
            } };

            // A bridgehead fallen on the free shore nearest home, with everything it had: the ruins and their land.
            Settlement fallen = null;
            yield return new ShowcaseStep { Shot = "ruiny", Ticks = 30, Setup = () =>
            {
                var site = World.Sites.Where(World.IsSiteFree).OrderBy(item => World.Distance(item.X, item.Y, home.X, home.Y)).FirstOrDefault();
                if (site == null) return;
                fallen = new Settlement
                {
                    Id = World.NextId++, Owner = PlayerFaction, X = site.X, Y = site.Y, IsOutpost = true, HasShipyard = true,
                    HasWorkshop = true, IsTown = true, HasSageSchool = true, HasDock = true, Hull = 0,
                };
                World.Settlements.Add(fallen);
                site.SettlementId = fallen.Id;
                // A cutter of ours lies off the shore, so the ruins are in sight, not under the fog.
                World.SpawnShip(PlayerFaction, ShipClass.Cutter, site.X + (site.X < 0 ? 30 : -30), site.Y - 25);
                FramePort(fallen, 55f);
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () => LogPort("ruiny", fallen != null && portBuildings.TryGetValue(fallen.Id, out var b) ? b : null) };

            // Two charges called at once: ours (the side's colour) on a fleeing crew destroyer, the enemy's (red) on our ship.
            yield return new ShowcaseStep { Shot = "taran-zapowiedz", Ticks = 2 * Rules.TickRate, Setup = () =>
            {
                selected.Clear();
                var prey = World.SpawnShip(1 - PlayerFaction, ShipClass.CrewDestroyer, area.x + 20, area.z + 40);
                showcaseRammer = World.SpawnShip(PlayerFaction, ShipClass.Ram, area.x - 18, area.z + 40);
                var ours = World.SpawnShip(PlayerFaction, ShipClass.CrewDestroyer, area.x + 20, area.z + 100);
                showcaseEnemyRammer = World.SpawnShip(1 - PlayerFaction, ShipClass.Ram, area.x - 18, area.z + 100);
                foreach (var ship in new[] { prey, showcaseRammer, ours, showcaseEnemyRammer }) ship.Heading = 0;
                prey.Hull = prey.Definition.Hull * 4;
                ours.Hull = ours.Definition.Hull * 4;
                World.Issue(new Command { Faction = 1 - PlayerFaction, Kind = CommandKind.Move, ShipIds = new[] { prey.Id }, X = area.x + 400, Y = area.z + 40 });
                World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.Move, ShipIds = new[] { ours.Id }, X = area.x + 400, Y = area.z + 100 });
                World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.Ram, ShipIds = new[] { showcaseRammer.Id }, TargetId = prey.Id });
                World.Issue(new Command { Faction = 1 - PlayerFaction, Kind = CommandKind.Ram, ShipIds = new[] { showcaseEnemyRammer.Id }, TargetId = ours.Id });
                pivot = area + new Vector3(15f, 0f, 70f); height = 75f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
            }, Follow = () => showcaseRammer };
            yield return new ShowcaseStep { Ticks = 1, Setup = () =>
            {
                if (ramTelegraph == null) return;
                Debug.Log($"[Showcase] taran-zapowiedz: {ramTelegraph.Shown} charges drawn, ours {ramTelegraph.FuseShownFor(showcaseRammer?.Id ?? -1):0.00}, " +
                          $"enemy's {ramTelegraph.FuseShownFor(showcaseEnemyRammer?.Id ?? -1):0.00}, calls {ramTelegraph.Calls}");
            } };

            // Firing arcs: a ship destroyer with one ballista broken, a bomber with one catapult short of its crew.
            yield return new ShowcaseStep { Shot = "stozki-ostrzalu", Ticks = 30, Setup = () =>
            {
                var killer = World.SpawnShip(PlayerFaction, ShipClass.ShipKiller, area.x - 60, area.z + 260);
                var bomber = World.SpawnShip(PlayerFaction, ShipClass.Bomber, area.x + 80, area.z + 260);
                killer.Heading = bomber.Heading = Mathf.PI * 0.5f;
                if (killer.ModuleHp.Length > 0) killer.ModuleHp[0] = 0;
                bomber.Swordsmen = WeaponCatalog.Get(WeaponType.HeavyCatapult).Crew;
                bomber.Javelins = bomber.Archers = 0;
                selected.Clear();
                selected.Add(killer.Id);
                selected.Add(bomber.Id);
                pivot = area + new Vector3(10f, 0f, 330f); height = 260f; yaw = ObliqueCamera.DefaultYaw; tilt = 60f;
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () =>
            {
                if (firingArcs == null) return;
                foreach (var id in selected)
                    Debug.Log($"[Showcase] stozki-ostrzalu: ship {id} posts {string.Join(", ", firingArcs.StatesFor(id).Select(state => state?.ToString() ?? "empty"))}");
            } };

            // The class icons in the war line's order menu.
            yield return new ShowcaseStep { Ticks = 15, Setup = () =>
            {
                selected.Clear();
                inspectedSettlementId = home.Id;
                FramePort(home, 90f);
            } };
            yield return new ShowcaseStep { Shot = "ikony-klas", Ticks = 1, Setup = () =>
            {
                var items = hud != null ? hud.OpenProductionForTest(true) : -1;
                Debug.Log($"[Showcase] order menu with {items} items");
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () => hud?.ClosePopoverForTest() };

            // E03 convoys (docs/BALANS_I_SKALA.md): the port card's menu with the priority port, the minimums and the idle merchants.
            Ship merchant = null;
            yield return new ShowcaseStep { Ticks = 15, Setup = () =>
            {
                selected.Clear();
                inspectedSettlementId = home.Id;
                merchant = World.SpawnShip(PlayerFaction, ShipClass.Merchant, area.x + 40, area.z + 60);
                FramePort(home, 90f);
            } };
            yield return new ShowcaseStep { Shot = "konwoje", Ticks = 1, Setup = () =>
            {
                var items = hud != null ? hud.OpenConvoysForTest() : -1;
                Debug.Log($"[Showcase] convoy menu with {items} items");
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () => hud?.ClosePopoverForTest() };

            // The merchant put on convoy duty: its card shows the switch on.
            yield return new ShowcaseStep { Shot = "handlowiec-konwoj", Ticks = 15, Setup = () =>
            {
                World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.Convoy, ShipIds = new[] { merchant.Id } });
                inspectedSettlementId = -1;
                selected.Clear();
                selected.Add(merchant.Id);
                pivot = new Vector3((float)merchant.X, 0f, (float)merchant.Y); height = 70f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
            } };
            yield return new ShowcaseStep { Ticks = 1, Setup = () => Debug.Log($"[Showcase] handlowiec-konwoj: {DescribeOrder(merchant)}") };

            // E06: a wreck holding goods and two ballistas that survived their draw, on the wreck card.
            yield return new ShowcaseStep { Shot = "wrak-machiny", Ticks = 15, Setup = () =>
            {
                selected.Clear();
                var wreck = new Wreck { Id = World.NextId++, X = merchant.X - 70, Y = merchant.Y + 10, Wood = 30 * Rules.Centi, ExpiresTick = World.Tick + 200 * Rules.TickRate };
                wreck.Items[(int)WeaponType.DeckBallista] = 2;
                World.Wrecks.Add(wreck);
                InspectedWreckId = wreck.Id;
                pivot = new Vector3((float)wreck.X, 0f, (float)wreck.Y); height = 60f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
            } };
        }
    }
}
