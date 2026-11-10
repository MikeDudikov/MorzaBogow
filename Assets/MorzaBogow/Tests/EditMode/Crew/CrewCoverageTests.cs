using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The crews follow the game's data: the matrix of classes and the manifest of roles are what the code draws; no class
    /// gets a weapon, an attack or a fighting role its data lacks; the posts are manned exactly as the simulation mans them;
    /// never more men of a trade than the ship has; every station holds a man of real size on its floor.
    /// </summary>
    public sealed class CrewCoverageTests
    {
        static ShipClass[] Classes => (ShipClass[])Enum.GetValues(typeof(ShipClass));
        static string Docs(string name) => Path.Combine(Application.dataPath, "..", "docs", "art", name);

        static readonly CrewRole[] FightingRoles = { CrewRole.BankArcher, CrewRole.BankJavelin, CrewRole.MachineCrew, CrewRole.Marine, CrewRole.Commander, CrewRole.WarriorRower };

        [Test]
        public void Coverage_TheMatrixFileIsTheGameData()
        {
            var file = File.ReadAllText(Docs("ShipCrewCoverage.json")).Replace("\r\n", "\n");
            Assert.That(file, Is.EqualTo(CrewCoverage.Json()), "docs/art/ShipCrewCoverage.json is made by CrewExport.Run from the code.");
            var roles = File.ReadAllText(Docs("CrewRoles.json")).Replace("\r\n", "\n");
            Assert.That(roles, Is.EqualTo(CrewRoles.Json()), "docs/art/CrewRoles.json is made by CrewExport.Run from CrewRoles.");
        }

        [Test]
        public void Coverage_NoClassGetsAWeaponAttackOrFightingRoleBeyondItsData()
        {
            var matrix = CrewCoverage.Build();
            Assert.That(matrix.klasy.Length, Is.EqualTo(Classes.Length));
            foreach (var entry in matrix.klasy)
            {
                var shipClass = (ShipClass)Enum.Parse(typeof(ShipClass), entry.klasa);
                var definition = ShipCatalog.Get(shipClass);
                Assert.That(entry.uzbrojenie.Length, Is.EqualTo(definition.Slots.Length), $"{shipClass}: one entry per post");
                foreach (var weapon in entry.uzbrojenie)
                    foreach (var name in weapon.mozliwe)
                        Assert.That(WeaponCatalog.Fits((WeaponType)Enum.Parse(typeof(WeaponType), name), definition.Slots[weapon.stanowisko]), $"{shipClass}: {name} fits post {weapon.stanowisko}");
                Assert.That(entry.taran, Is.EqualTo(definition.CanRam), $"{shipClass}: ram only where the data allows it");
                var mounted = CrewCoverage.Default(shipClass).Mounts;
                int Of(CrewCoverage.Count[] counts, CrewRole role) => counts.FirstOrDefault(c => c.nazwa == role.ToString())?.liczba ?? 0;
                foreach (var counts in new[] { entry.role, entry.roleWGotowosci })
                {
                    Assert.That(Of(counts, CrewRole.BankArcher), Is.EqualTo(4 * mounted.Count(m => m == WeaponType.ArcherBank)), $"{shipClass}: archers only at archer banks");
                    Assert.That(Of(counts, CrewRole.BankJavelin), Is.EqualTo(4 * mounted.Count(m => m == WeaponType.JavelinBank)), $"{shipClass}: javelin men only at javelin banks");
                    Assert.That(Of(counts, CrewRole.MachineCrew), Is.EqualTo(mounted.Where(m => m != WeaponType.None && WeaponCatalog.Get(m).IsMachine).Sum(m => WeaponCatalog.Get(m).Crew)),
                        $"{shipClass}: machine crews only at machines");
                    if (definition.IsEconomic)
                        foreach (var role in FightingRoles) Assert.That(Of(counts, role), Is.Zero, $"{shipClass}: a working ship has no {role}");
                }
                if (!definition.CanRam) Assert.That(entry.atak.Any(line => line.Contains("taran")), Is.False, $"{shipClass}: no ram in its attack");
                Assert.That(entry.postacieRejs + entry.bezMiejsca, Is.EqualTo(definition.MaxCrew), $"{shipClass}: every man of the full crew is either drawn or counted as without room");
            }
        }

        [Test]
        public void Manning_PostsAsTheSimulationMansThemAndNeverMoreMenThanAboard()
        {
            var random = new System.Random(7);
            foreach (var shipClass in Classes)
            {
                var definition = ShipCatalog.Get(shipClass);
                var profile = CrewLayoutProfile.For(shipClass);
                foreach (CrewPreset preset in Enum.GetValues(typeof(CrewPreset)))
                {
                    if (preset == CrewPreset.Custom) continue;
                    for (var trial = 0; trial < 12; trial++)
                    {
                        var ship = CrewCoverage.Default(shipClass);
                        var plan = definition.PlanFor(preset);
                        // Losses, wounded and refits of the machine posts, as a fight and a port leave a ship.
                        ship.Sailors = Math.Max(0, plan.Sailors - random.Next(0, 6) * (trial % 3));
                        ship.Swordsmen = Math.Max(0, plan.Swordsmen - random.Next(0, 8) * (trial % 2));
                        ship.Javelins = Math.Max(0, plan.Javelins - random.Next(0, 5));
                        ship.Archers = Math.Max(0, plan.Archers - random.Next(0, 9) * (trial % 4 == 0 ? 1 : 0));
                        ship.Wounded = random.Next(0, 9);
                        for (var slot = 0; slot < ship.Mounts.Length; slot++)
                        {
                            var allowed = CrewCoverage.Allowed(definition, slot);
                            if (allowed.Length > 0 && definition.Slots[slot] != SlotSize.Light && trial % 2 == 1) ship.Mounts[slot] = allowed[random.Next(allowed.Length)];
                            if (trial == 5) ship.ModuleHp[slot] = 0;
                        }
                        var roster = new CrewRoster();
                        roster.Reset(ship);
                        var manned = World.Manned(ship);
                        foreach (var ready in new[] { false, true })
                        {
                            var places = CrewManning.Assign(profile, roster, ship.Mounts, manned, ready);
                            var label = $"{shipClass} {preset} trial {trial} ready {ready}";
                            foreach (CrewTrade trade in Enum.GetValues(typeof(CrewTrade)))
                                Assert.That(places.Count(p => p.Trade == trade), Is.LessThanOrEqualTo(CrewRoles.Count(ship, trade)), $"{label}: no more {trade} than aboard");
                            Assert.That(places.Select(p => p.Man).Distinct().Count(), Is.EqualTo(places.Count), $"{label}: each man once");
                            var used = places.Where(p => p.Station >= 0).Select(p => p.Station).ToList();
                            Assert.That(used.Distinct().Count(), Is.EqualTo(used.Count), $"{label}: one man per station");
                            foreach (var place in places.Where(p => p.Station >= 0))
                            {
                                Assert.That(profile.Stations[place.Station].Holds(place.Role), $"{label}: {place.Role} allowed at {profile.Stations[place.Station].Id}");
                                Assert.That(CrewRoles.Of(place.Role).Trades, Does.Contain(place.Trade), $"{label}: {place.Role} filled by its trade, not {place.Trade}");
                            }
                            // The simulation's own allocation, trade by trade.
                            var archers = ship.Archers;
                            var javelins = ship.Javelins;
                            var swordsmen = ship.Swordsmen;
                            for (var slot = 0; slot < ship.Mounts.Length; slot++)
                            {
                                var crew = places.Where(p => p.Slot == slot).ToList();
                                if (!manned[slot] || ship.Mounts[slot] == WeaponType.None) { Assert.That(crew, Is.Empty, $"{label}: post {slot} unmanned in the game"); continue; }
                                var weapon = WeaponCatalog.Get(ship.Mounts[slot]);
                                Assert.That(crew.Count, Is.EqualTo(weapon.Crew), $"{label}: post {slot} has its {weapon.Crew} men");
                                if (weapon.CrewKind == WeaponCrew.Archers) { Assert.That(crew.All(p => p.Trade == CrewTrade.Archer), $"{label}: bank of archers"); archers -= weapon.Crew; }
                                else if (weapon.CrewKind == WeaponCrew.Javelins) { Assert.That(crew.All(p => p.Trade == CrewTrade.Javelin), $"{label}: bank of javelin men"); javelins -= weapon.Crew; }
                            }
                            for (var slot = 0; slot < ship.Mounts.Length; slot++)
                            {
                                if (!manned[slot] || ship.Mounts[slot] == WeaponType.None || !WeaponCatalog.Get(ship.Mounts[slot]).IsMachine) continue;
                                var need = WeaponCatalog.Get(ship.Mounts[slot]).Crew;
                                var crew = places.Where(p => p.Slot == slot).ToList();
                                var sword = Math.Min(swordsmen, need); swordsmen -= sword; need -= sword;
                                var jav = Math.Min(javelins, need); javelins -= jav; need -= jav;
                                archers -= need;
                                Assert.That(crew.Count(p => p.Trade == CrewTrade.Swordsman), Is.EqualTo(sword), $"{label}: machine {slot} takes swordsmen first");
                                Assert.That(crew.Count(p => p.Trade == CrewTrade.Javelin), Is.EqualTo(jav), $"{label}: then javelin men");
                                Assert.That(crew.Count(p => p.Trade == CrewTrade.Archer), Is.EqualTo(need), $"{label}: then archers");
                            }
                            Assert.That(places.Count(p => p.Role == CrewRole.Rower) % 2, Is.Zero, $"{label}: rowers pull in pairs");
                            if (ready && places.Any(p => p.Role == CrewRole.WarriorRower))
                            {
                                // Ready to fight, a warrior stays at an oar only when every place to stand or sit is taken.
                                var free = profile.Stations.Select((s, i) => (s, i)).Count(x => (x.s.Kind == StationKind.Stand || x.s.Kind == StationKind.Seat) && !used.Contains(x.i));
                                Assert.That(free, Is.Zero, $"{label}: warriors leave the oars while there is room to stand");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void Layout_EveryStationHoldsAManOfRealSizeOnItsFloor()
        {
            foreach (var shipClass in Classes)
            {
                var profile = CrewLayoutProfile.For(shipClass);
                var parts = profile.Parts;
                var f = parts.Form;
                var definition = ShipCatalog.Get(shipClass);
                Assert.That(profile.Count(StationKind.Bench), Is.EqualTo(parts.OarPins.Length * 2), $"{shipClass}: a bench at every oar");
                Assert.That(profile.Count(StationKind.Helm), Is.EqualTo(parts.HelmStands.Length), $"{shipClass}: the helm");
                for (var slot = 0; slot < definition.Slots.Length; slot++)
                {
                    var most = definition.Slots[slot] == SlotSize.Light ? 4 : definition.Slots[slot] == SlotSize.Medium ? 6 : 8;
                    Assert.That(profile.Stations.Count(s => s.Slot == slot), Is.EqualTo(most), $"{shipClass}: places for the largest crew of post {slot}");
                }
                foreach (var station in profile.Stations)
                {
                    var label = $"{shipClass} {station.Id}";
                    if (station.Kind == StationKind.Bench || station.Kind == StationKind.Helm) continue; // measured in CrewGeometryTests
                    var at = station.Slot >= 0 ? profile.PostPoint(station, 0f) : station.Position;
                    var feet = at;
                    if (station.Posture == CrewPosture.Sit) feet.y -= 0.06f;
                    Assert.That(CrewLayoutProfile.OnFloor(f, feet, station.Kind == StationKind.Rail ? 0.2f : 0.18f), $"{label}: on its deck or platform, inside the hull");
                    var floor = CrewLayoutProfile.FloorAt(f, feet, out _);
                    if (station.Slot < 0 || station.Position.y <= 0f)
                        Assert.That(feet.y, Is.EqualTo(floor).Within(0.07f), $"{label}: standing on the floor, never above or in it");
                }
                // Standing and seated places do not overlap, except the standing places the same men take when they rise.
                var stations = profile.Stations.Where(s => s.Slot < 0 && s.Kind != StationKind.Bench && s.Kind != StationKind.Helm && s.Kind != StationKind.Rail).ToList();
                for (var i = 0; i < stations.Count; i++)
                for (var j = i + 1; j < stations.Count; j++)
                {
                    var a = stations[i];
                    var b = stations[j];
                    if ((a.Kind == StationKind.Seat) != (b.Kind == StationKind.Seat) && (a.Kind == StationKind.Stand || b.Kind == StationKind.Stand)) continue;
                    var gap = new Vector2(a.Position.x - b.Position.x, a.Position.z - b.Position.z).magnitude;
                    if (Mathf.Abs(a.Position.y - b.Position.y) > 1.2f) continue;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(CrewLayoutProfile.BodyHalfWidth * 2f - 0.02f), $"{shipClass}: {a.Id} and {b.Id} each have room");
                }
            }
        }

        [Test]
        public void Layout_EveryClassHasItsWorkingCrewAndRoomForItsFullComplement()
        {
            foreach (var shipClass in Classes)
            {
                var profile = CrewLayoutProfile.For(shipClass);
                var definition = ShipCatalog.Get(shipClass);
                if (shipClass == ShipClass.Cutter) Assert.That(profile.Count(StationKind.Bow), Is.EqualTo(1), "The boat's bow man.");
                else Assert.That(profile.Count(StationKind.Command), Is.EqualTo(1), $"{shipClass}: a leader's or skipper's place");
                var ship = CrewCoverage.Default(shipClass);
                var roster = new CrewRoster();
                roster.Reset(ship);
                var places = CrewManning.Assign(profile, roster, ship.Mounts, World.Manned(ship), false);
                var drawn = places.Count(p => p.Station >= 0);
                Assert.That(drawn, Is.GreaterThanOrEqualTo((int)(definition.MaxCrew * 0.85f)), $"{shipClass}: at least 85% of the full crew is drawn ({drawn} of {definition.MaxCrew})");
                Assert.That(places.Count(p => p.Role == CrewRole.Helmsman && p.Station >= 0), Is.EqualTo(profile.Count(StationKind.Helm)), $"{shipClass}: the helm is manned");
                Assert.That(places.Count(p => (p.Role == CrewRole.Rower || p.Role == CrewRole.WarriorRower) && p.Station >= 0), Is.GreaterThan(0), $"{shipClass}: rowers at sea");
            }
        }
    }
}
