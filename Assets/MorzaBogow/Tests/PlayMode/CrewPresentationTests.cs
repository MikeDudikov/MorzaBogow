using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The crews on deck as the battle draws them. First the oars and helm they work: one stroke for the whole bank with
    /// the blade driving aft in the water, an oar without a rower drawn in on its own, the steering oars turning with the
    /// hull's turn.
    /// </summary>
    public sealed class CrewPresentationTests
    {
        [Test]
        public void Oars_AllPullTogetherAndDriveAftInTheWater()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                var parts = view.Parts;
                var half = parts.OarPins.Length;
                float? lastTip = null;
                var driving = 0;
                var wasOut = false;
                for (var frame = 0; frame < 240; frame++)
                {
                    view.Animate(0.8f, frame / 60f, 1f / 60f);
                    // Every oar of a side stands at the same angle: one stroke, not a ripple along the bank.
                    for (var i = 1; i < half; i++)
                    {
                        Assert.That(Quaternion.Angle(view.OarPivot(i).localRotation, view.OarPivot(0).localRotation), Is.LessThan(0.01f), "port oars together");
                        Assert.That(Quaternion.Angle(view.OarPivot(half + i).localRotation, view.OarPivot(half).localRotation), Is.LessThan(0.01f), "starboard oars together");
                    }
                    var pivot = view.OarPivot(half);
                    var tip = view.Model.InverseTransformPoint(pivot.TransformPoint(new Vector3(parts.OarLength[0], 0f, 0f)));
                    var bladeLow = tip.y < view.Model.InverseTransformPoint(pivot.position).y - parts.OarLength[0] * Mathf.Sin(parts.OarTilt[0] * Mathf.Deg2Rad) - 0.05f;
                    // Only once the oar is fully out, so its coming out from along the side is not taken for the stroke.
                    var isOut = view.OarStowed(half) < 1e-3f;
                    if (lastTip.HasValue && wasOut && isOut && RowingStroke.Driving(view.StrokePhase) && bladeLow)
                    {
                        Assert.That(tip.z, Is.LessThanOrEqualTo(lastTip.Value + 1e-4f), $"frame {frame}: the blade in the water moves aft");
                        driving++;
                    }
                    lastTip = tip.z;
                    wasOut = isOut;
                }
                Assert.That(driving, Is.GreaterThan(20), "Strokes were pulled.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Oars_AnOarWithoutARowerIsDrawnInOnItsOwn()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Cutter, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                view.SetOarCrewed(0, false);
                for (var frame = 0; frame < 120; frame++) view.Animate(0.7f, frame / 60f, 1f / 60f);
                Assert.That(view.OarStowed(0), Is.GreaterThan(0.95f), "The oar without its rower lies along the side.");
                Assert.That(view.OarStowed(1), Is.LessThan(0.05f), "The crewed oar beside it keeps rowing.");
                view.SetOarCrewed(0, true);
                for (var frame = 0; frame < 120; frame++) view.Animate(0.7f, frame / 60f, 1f / 60f);
                Assert.That(view.OarStowed(0), Is.LessThan(0.05f), "Manned again, it rows.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        // The battle's crews -------------------------------------------------------------------------------------------
        static (GameObject host, BattleController battle, CrewDirector crews) Battle()
        {
            var host = new GameObject("Battle with crews");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            return (host, battle, host.GetComponent<CrewDirector>());
        }

        static void Clean(GameObject host)
        {
            Object.DestroyImmediate(host);
            foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        }

        static Ship Spawn(BattleController battle, int owner, ShipClass shipClass, double dx, double dy)
        {
            var home = battle.World.SettlementOf(0);
            return battle.World.SpawnShip(owner, shipClass, home.X + dx, home.Y + dy);
        }

        static void Steps(BattleController battle, CrewDirector crews, int ticks)
        {
            for (var i = 0; i < ticks; i++) Tick(battle, crews);
        }

        /// <summary>
        /// One tick and its frame. The battle is paused so its own frames run no ticks between the test's, and a paused
        /// frame gives the views no game time; here each view gets the tick's game time, as a running game's frame gives it
        /// (BattleController.PresentFrame), so oars come out and the stroke runs.
        /// </summary>
        static void Tick(BattleController battle, CrewDirector crews)
        {
            battle.RunTicks(1);
            foreach (var ship in battle.World.Ships)
                if (battle.Views.TryGetValue(ship.Id, out var view) && view != null)
                    view.Animate((float)(ship.Speed / ship.Definition.Speed), Time.unscaledTime, (float)Rules.Dt);
            crews.Present((float)Rules.Dt, null);
        }

        [UnityTest]
        public IEnumerator Crew_A_EveryClassCarriesItsCrewRowingAndSteeringAtSea()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try
            {
                var ships = new List<Ship>();
                var classes = (ShipClass[])System.Enum.GetValues(typeof(ShipClass));
                for (var i = 0; i < classes.Length; i++)
                {
                    var ship = Spawn(battle, 0, classes[i], 260 + i * 70, 420);
                    battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = ship.X, Y = ship.Y + 600 });
                    ships.Add(ship);
                }
                Steps(battle, crews, 150);
                foreach (var ship in ships)
                {
                    Assert.That(crews.Crews.ContainsKey(ship.Id), $"{ship.Class}: a crew aboard");
                    var crew = crews.Crews[ship.Id];
                    var view = battle.Views[ship.Id];
                    Assert.That(crew.DrawnFigures, Is.GreaterThanOrEqualTo((int)(ship.Crew * 0.85f)), $"{ship.Class}: most of its {ship.Crew} men are drawn");
                    Assert.That(crew.Men.Any(m => m.Role == CrewRole.Helmsman && (m.Action == CrewAction.Steer || m.Action == CrewAction.SteerSeated)), $"{ship.Class}: the helmsman steers");
                    var rowing = crew.Men.Where(m => m.Action == CrewAction.Row || m.Action == CrewAction.RowStanding).ToList();
                    Assert.That(rowing.Count, Is.GreaterThan(0), $"{ship.Class}: men at the oars under way (order {ship.Order}, speed {ship.Speed:0.0} of {ship.Definition.Speed}, rowers {crew.Men.Count(m => m.Role == CrewRole.Rower)}, stowed {view.OarStowed(0):0.00}/{view.OarStowed(view.OarCount - 1):0.00}, actions {string.Join(",", crew.Men.Select(m => m.Action).Distinct())})");
                    foreach (var man in rowing) Assert.That(man.Phase, Is.EqualTo(view.StrokePhase), $"{ship.Class}: every rower keeps the oars' one stroke");
                    if (ship.Definition.IsEconomic)
                        Assert.That(crew.Men.Any(m => CrewRoles.Fights(m.Role)), Is.False, $"{ship.Class}: no fighting role on a working ship");
                }
                Assert.That(crews.FiguresDrawn, Is.GreaterThan(0));
                Assert.That(crews.Renderer.Instances, Is.GreaterThan(crews.FiguresDrawn * 8), "Each man drawn as his pieces.");
            }
            finally { Clean(host); }
        }

        [UnityTest]
        public IEnumerator Crew_B_MenStayAboardThroughAHardTurn()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try
            {
                var ship = Spawn(battle, 0, ShipClass.Ram, 300, 420);
                battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = ship.X, Y = ship.Y + 500 });
                Steps(battle, crews, 120);
                battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = ship.X + 400, Y = ship.Y - 50 });
                var view = battle.Views[ship.Id];
                var f = view.Parts.Form;
                for (var t = 0; t < 120; t++)
                {
                    Steps(battle, crews, 1);
                    foreach (var man in crews.Crews[ship.Id].Men)
                    {
                        if (man.Station < 0) continue; // a man without room is not drawn
                        var local = view.Model.InverseTransformPoint(crews.Crews[ship.Id].WorldPosition(man));
                        var s = Mathf.InverseLerp(GreekShips.Z(f, 0f), GreekShips.Z(f, 1f), local.z);
                        Assert.That(s, Is.InRange(0f, 1f), $"{man.Role} {man.Man}: within the hull's length");
                        Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(GreekShips.HalfWidth(f, s) + 0.05f), $"{man.Role} {man.Man}: within its beam");
                        Assert.That(local.y, Is.GreaterThan(GreekShips.DeckAt(f, s) - 0.25f), $"{man.Role} {man.Man}: on the deck, not in the hull");
                    }
                }
            }
            finally { Clean(host); }
        }

        [UnityTest]
        public IEnumerator Crew_CDE_ArchersLooseOnTheTickTheBankFiresTowardTheShotAndRowersRowOn()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try
            {
                var shooter = Spawn(battle, 0, ShipClass.CrewDestroyer, 300, 420);
                // The mark first out of reach, so the crews are aboard before the loaded banks see it.
                var target = Spawn(battle, 1, ShipClass.Cutter, 300, 900);
                target.Hull = 100000;
                battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { shooter.Id }, X = shooter.X + 300, Y = shooter.Y });
                Steps(battle, crews, 60);
                target.X = shooter.X;
                target.Y = shooter.Y + 70;
                var loosed = false;
                for (var t = 0; t < 240 && !loosed; t++)
                {
                    Tick(battle, crews);
                    var crew = crews.Crews[shooter.Id];
                    var shots = battle.World.Projectiles.Where(p => p.ShooterId == shooter.Id && p.LaunchTick == battle.World.Tick - 1 || p.ShooterId == shooter.Id && p.LaunchTick == battle.World.Tick).ToList();
                    if (shots.Count == 0) continue;
                    loosed = true;
                    var archers = crew.Men.Where(m => m.Role == CrewRole.BankArcher).ToList();
                    Assert.That(archers.Count, Is.EqualTo(16), "Four banks of four archers, as World.Manned mans them.");
                    Assert.That(archers.Any(m => m.Action == CrewAction.BowLoose), "Archers loose on the tick the bank fires.");
                    var shot = shots[0];
                    var direction = new Vector3((float)(shot.ToX - shot.FromX), 0f, (float)(shot.ToY - shot.FromY)).normalized;
                    foreach (var man in archers.Where(m => m.Action == CrewAction.BowLoose))
                    {
                        var view = battle.Views[shooter.Id];
                        var aim = view.Model.TransformDirection(Quaternion.Euler(0f, crew.Profile.Stations[man.Station].Facing, 0f) * man.Aim);
                        aim.y = 0f;
                        Assert.That(Vector3.Angle(aim, direction), Is.LessThan(15f), "The archer faces where his arrows go.");
                    }
                    Assert.That(crew.Men.Any(m => m.Action == CrewAction.Row), "The rowers keep rowing while the archers shoot.");
                    var start = battle.Effects.LaunchPoint?.Invoke(shot, 0);
                    Assert.That(start.HasValue, "The shot leaves from an archer's hand.");
                    Assert.That(start.Value.y, Is.GreaterThan(1.5f), "From a hand on deck, not the middle of the hull at the water.");
                }
                Assert.That(loosed, "The bank fired within eight seconds.");
            }
            finally { Clean(host); }
        }

        [UnityTest]
        public IEnumerator Crew_E_BallistaCrewsWorkTheReloadAndTheMachineFacesItsShot()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try { Assert.That(MachinePostsWork(battle, crews, ShipClass.ShipKiller, new Vector2(87f, 50f)), Is.GreaterThan(0), "A ballista fired twice at its mark."); }
            finally { Clean(host); }
        }

        [UnityTest]
        public IEnumerator Crew_E_CatapultCrewsWorkTheReloadAndTheMachineFacesItsShot()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try { Assert.That(MachinePostsWork(battle, crews, ShipClass.Bomber, new Vector2(130f, 40f)), Is.GreaterThan(0), "A catapult fired twice at its mark."); }
            finally { Clean(host); }
        }

        /// <summary>
        /// A machine ship against a mark off its bow, inside every machine's arc and reach. At a machine's second shot (the
        /// first may leave before it has turned to a mark it just saw): its whole crew is at the post, the winch men have
        /// heaved and the loader has set the shot during the reload, the machine faces where the shot goes, and the shot
        /// leaves from the machine above the deck.
        /// </summary>
        static int MachinePostsWork(BattleController battle, CrewDirector crews, ShipClass shipClass, Vector2 off)
        {
            var shooter = Spawn(battle, 0, shipClass, 300, 420);
            shooter.Heading = 0;
            // The mark first out of reach, so the crews are aboard and the machines loaded before they see it.
            var target = Spawn(battle, 1, ShipClass.Cutter, 300, 1000);
            target.Hull = 100000;
            Steps(battle, crews, 60);
            var seen = new HashSet<int>();
            var shotsBy = new Dictionary<int, int>();
            var cranked = new HashSet<int>();
            var loaded = new HashSet<int>();
            var checkedPosts = 0;
            for (var t = 0; t < 30 * 30 && checkedPosts == 0; t++)
            {
                target.X = shooter.X + off.x;
                target.Y = shooter.Y + off.y;
                target.Speed = 0;
                Tick(battle, crews);
                var crew = crews.Crews[shooter.Id];
                foreach (var man in crew.Men.Where(m => m.Role == CrewRole.MachineCrew))
                {
                    if (man.Action == CrewAction.Crank) cranked.Add(man.Slot);
                    if (man.Action == CrewAction.Load) loaded.Add(man.Slot);
                }
                var view = battle.Views[shooter.Id];
                foreach (var shot in battle.World.Projectiles.Where(p => p.ShooterId == shooter.Id && p.LaunchTick >= battle.World.Tick - 1).ToList())
                {
                    if (!seen.Add(shot.Id)) continue;
                    var start = battle.Effects.LaunchPoint?.Invoke(shot, 0);
                    Assert.That(start.HasValue, $"{shipClass}: the shot leaves from a machine");
                    // The machine that loosed it: the post nearest to where the shot left.
                    var slot = Enumerable.Range(0, shooter.Mounts.Length).Where(s => WeaponCatalog.Get(shooter.Mounts[s]).IsMachine)
                        .OrderBy(s => Vector3.Distance(view.Model.Find($"Mount {s}").position, start.Value)).First();
                    var mount = view.Model.Find($"Mount {slot}");
                    Assert.That(Vector3.Distance(start.Value, mount.position), Is.LessThan(4f), $"{shipClass} post {slot}: from the machine's own place");
                    Assert.That(start.Value.y, Is.GreaterThan(1f), $"{shipClass} post {slot}: above the deck, not from the waterline");
                    shotsBy[slot] = shotsBy.TryGetValue(slot, out var count) ? count + 1 : 1;
                    if (shotsBy[slot] < 2) continue;
                    var weapon = WeaponCatalog.Get(shooter.Mounts[slot]);
                    Assert.That(crew.Men.Count(m => m.Role == CrewRole.MachineCrew && m.Slot == slot), Is.EqualTo(weapon.Crew), $"{shipClass} post {slot}: its whole crew, as World.Manned mans it");
                    Assert.That(cranked.Contains(slot), $"{shipClass} post {slot}: the winch men heave through the reload");
                    Assert.That(loaded.Contains(slot), $"{shipClass} post {slot}: the loader sets the next shot");
                    var facing = mount.forward;
                    facing.y = 0f;
                    var direction = new Vector3((float)(shot.ToX - shot.FromX), 0f, (float)(shot.ToY - shot.FromY));
                    Assert.That(Vector3.Angle(facing, direction), Is.LessThan(15f), $"{shipClass} post {slot}: the machine faces where its shot goes");
                    checkedPosts++;
                }
            }
            return checkedPosts;
        }

        [UnityTest]
        public IEnumerator Crew_F_TheFrontFightsAtTheRailFacingTheOtherDeck()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try
            {
                var boarder = Spawn(battle, 0, ShipClass.Boarder, 300, 420);
                var target = Spawn(battle, 1, ShipClass.Ram, 300, 452);
                target.Speed = 0;
                battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
                var hooked = false;
                for (var t = 0; t < 900 && !hooked; t++)
                {
                    Steps(battle, crews, 1);
                    hooked = battle.World.Boardings.Any(link => link.Hooked && link.AttackerId == boarder.Id);
                }
                Assert.That(hooked, "The boarder grappled the ram.");
                Steps(battle, crews, 45);
                crews.UpdateHauls((float)Rules.Dt);
                var haul = crews.HaulOffset(boarder.Id);
                Assert.That(haul.magnitude, Is.GreaterThan(0.3f), "The grapples haul the boarder's hull alongside (the picture only).");
                Assert.That(haul.magnitude, Is.LessThan(25f), "No farther than the hulls lay apart when the grapples were thrown.");
                foreach (var ship in new[] { boarder, target })
                {
                    if (battle.World.FindShip(ship.Id) == null) continue;
                    var crew = crews.Crews[ship.Id];
                    var front = crew.Men.Where(m => m.RailStation >= 0).ToList();
                    Assert.That(front.Count, Is.GreaterThan(0), $"{ship.Class}: men at the rail");
                    var link = battle.World.Boardings.FirstOrDefault(l => l.AttackerId == boarder.Id);
                    if (link != null)
                    {
                        var limit = ship == boarder ? link.AttackerFront : link.TargetFront;
                        if (limit > 0) Assert.That(front.Count, Is.LessThanOrEqualTo(limit), $"{ship.Class}: no more men at the rail than the front holds");
                    }
                    var other = ship == boarder ? target : boarder;
                    var side = battle.Views[ship.Id].transform.InverseTransformPoint(new Vector3((float)other.X, 0f, (float)other.Y)).x >= 0f ? 1 : -1;
                    Assert.That(front.All(m => crew.Profile.Stations[m.RailStation].Side == side), $"{ship.Class}: at the rail facing the other ship");
                }
            }
            finally { Clean(host); }
        }

        [UnityTest]
        public IEnumerator Crew_G_TheCrewsChangeNothingInTheGame()
        {
            var (hostA, battleA, crewsA) = Battle();
            var (hostB, battleB, crewsB) = Battle();
            yield return null;
            try
            {
                Object.DestroyImmediate(crewsB);
                foreach (var battle in new[] { battleA, battleB })
                {
                    var ram = Spawn(battle, 0, ShipClass.Ram, 300, 420);
                    var enemy = Spawn(battle, 1, ShipClass.CrewDestroyer, 300, 520);
                    battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.Ram, ShipIds = new[] { ram.Id }, TargetId = enemy.Id });
                }
                for (var t = 0; t < 600; t++)
                {
                    battleA.RunTicks(1);
                    crewsA.Present((float)Rules.Dt, null);
                    battleB.RunTicks(1);
                }
                Assert.That(SaveGame.Hash(SaveGame.Serialize(battleA.World)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(battleB.World))), "The same game with and without the crews drawn.");
            }
            finally { Clean(hostA); Clean(hostB); }
        }

        [UnityTest]
        public IEnumerator Crew_H_ALoadedGameLoosesNoVolleyAndKeepsItsCrews()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            var path = System.IO.Path.Combine(Application.temporaryCachePath, "crew-load-test.mbsave");
            try
            {
                var shooter = Spawn(battle, 0, ShipClass.CrewDestroyer, 300, 420);
                var target = Spawn(battle, 1, ShipClass.Cutter, 300, 490);
                target.Hull = 100000;
                Steps(battle, crews, 75);
                battle.QuickSave(path);
                Assert.That(battle.QuickLoad(path), "Loaded.");
                crews.Present(0f, null);
                var loaded = battle.World.Ships.First(s => s.Class == ShipClass.CrewDestroyer && s.Owner == 0);
                Assert.That(crews.Crews.ContainsKey(loaded.Id), "The loaded ship has its crew at once.");
                var crew = crews.Crews[loaded.Id];
                Assert.That(crew.Men.Any(m => m.Action == CrewAction.BowLoose), Is.False, "No volley replayed from before the load.");
                Assert.That(crew.DrawnFigures, Is.GreaterThanOrEqualTo((int)(loaded.Crew * 0.85f)), "The crew by the loaded numbers.");
                Assert.That(battle.Effects.MissilesShown + battle.Effects.ActiveCount, Is.Zero, "No shots or effects carried over from before the load.");
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                Clean(host);
            }
        }

        [UnityTest]
        public IEnumerator Crew_I_EveryLevelOfDetailDrawsTheWholeCrew()
        {
            var (host, battle, crews) = Battle();
            var cameraObject = new GameObject("Crew test camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            yield return null;
            try
            {
                var ship = Spawn(battle, 0, ShipClass.Boarder, 300, 420);
                battle.RunTicks(2);
                var view = battle.Views[ship.Id];
                camera.transform.SetPositionAndRotation(view.transform.position + new Vector3(0f, 100f, -100f), Quaternion.Euler(45f, 0f, 0f));
                var drawn = new List<int>();
                foreach (var size in new[] { 12f, 78f, 240f })
                {
                    camera.orthographicSize = size;
                    for (var i = 0; i < 3; i++) crews.Present((float)Rules.Dt, camera);
                    drawn.Add(crews.FiguresDrawn);
                    Assert.That(crews.Renderer.Instances, Is.GreaterThan(0), $"LOD {crews.Lod}: drawn");
                }
                Assert.That(drawn.Distinct().Count(), Is.EqualTo(1), "The same men at every level of detail; none left out to save time.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Clean(host);
            }
        }

        [UnityTest]
        public IEnumerator Crew_J_FleetsOf100And200ShipsAreMeasured()
        {
            var (host, battle, crews) = Battle();
            yield return null;
            try
            {
                // The full measurement (100 and 200 ships) only with -morzaBenchmark on Unity's command line; the gate draws 20 once.
                var full = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-morzaBenchmark") >= 0;
                var batch = full ? 100 : 20;
                var frames = full ? 30 : 3;
                var classes = new[] { ShipClass.Ram, ShipClass.CrewDestroyer, ShipClass.Boarder, ShipClass.ShipKiller, ShipClass.Bomber, ShipClass.Merchant, ShipClass.Cutter };
                var report = new System.Text.StringBuilder();
                for (var total = 0; total < batch * (full ? 2 : 1);)
                {
                    for (var i = 0; i < batch; i++, total++)
                        Spawn(battle, total % 2, classes[total % classes.Length], 200 + total % 20 * 45, 300 + total / 20 * 60);
                    battle.RunTicks(2);
                    // The same men at the strategic camera's detail (LOD1) and from afar (LOD2), every one of them drawn.
                    foreach (var detail in new[] { 1, 2 })
                    {
                        crews.LodWithoutCamera = detail;
                        crews.Present((float)Rules.Dt, null);
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        double decide = 0, build = 0, pose = 0, list = 0, draw = 0;
                        for (var f = 0; f < frames; f++)
                        {
                            crews.Present((float)Rules.Dt, null);
                            var t = crews.Timings;
                            decide += t.Decide; build += t.Build; pose += t.Pose; list += t.List; draw += t.Draw;
                        }
                        var ms = watch.Elapsed.TotalMilliseconds / frames;
                        report.AppendLine($"{total} ships, LOD {crews.Lod}: {crews.FiguresDrawn} men, {crews.Renderer.Instances} pieces, {crews.Renderer.Draws} draws, {ms:0.00} ms a frame in the editor " +
                                          $"(decide {decide / frames:0.00}, build {build / frames:0.00}, pose {pose / frames:0.00}, list {list / frames:0.00}, place and submit {draw / frames:0.00})");
                    }
                    crews.LodWithoutCamera = 1;
                }
                Debug.Log("Crew fleet benchmark (editor, informational):\n" + report);
                Assert.That(crews.FiguresDrawn, Is.GreaterThan(0));
            }
            finally { Clean(host); }
        }

        [Test]
        public void Helm_SteeringOarsTurnWithTheHullAndCarryTheTillers()
        {
            var world = new World(1);
            var ship = world.SpawnShip(0, ShipClass.Ram, 0, 0);
            var view = ShipVisual.Create(ship, null);
            try
            {
                view.Animate(0.5f, 0f, 1f / 60f);
                var rest = view.Model.InverseTransformPoint(view.TillerGrip(1));
                for (var frame = 1; frame <= 90; frame++)
                {
                    view.transform.rotation = Quaternion.Euler(0f, frame * 0.4f, 0f);
                    view.Animate(0.5f, frame / 60f, 1f / 60f);
                }
                Assert.That(Mathf.Abs(view.SteeringAngle), Is.GreaterThan(5f), "A steady turn puts the helm over.");
                var turned = view.Model.InverseTransformPoint(view.TillerGrip(1));
                Assert.That(Vector3.Distance(turned, rest), Is.GreaterThan(0.05f), "The tiller moves with the oar.");
                for (var frame = 0; frame < 120; frame++) view.Animate(0.5f, 2f + frame / 60f, 1f / 60f);
                Assert.That(Mathf.Abs(view.SteeringAngle), Is.LessThan(1f), "Straight again, the helm comes amidships.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
