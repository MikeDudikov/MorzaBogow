using System.Collections;
using System.IO;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    public sealed class BattleControllerTests
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

        [UnityTest]
        public IEnumerator Settlement_ModelStandsOnItsLandAndKeepsThePortClear()
        {
            yield return null;
            foreach (var settlement in battle.World.Settlements)
            {
                var view = battle.SettlementViews[settlement.Id];
                Assert.That(view.transform.position.x, Is.EqualTo((float)Rules.SettlementLandX(settlement)).Within(1e-3f));

                var islet = view.transform.Find("Islet").GetComponent<Renderer>().bounds;
                // The beach runs out under the water just past the waterline; at the water it matches the land circle.
                var land = battle.World.Islands.First(item => item.Kind == IslandKind.Settlement && Mathf.Abs((float)(item.X - Rules.SettlementLandX(settlement))) < 0.5f && Mathf.Abs((float)(item.Y - settlement.Y)) < 0.5f);
                Assert.That(land.Outline, Is.Not.Null, "Settlement land has an irregular waterline.");
                // The mound's outer ring runs at 1.1 of the waterline; half its width is half the span across.
                var xs = Enumerable.Range(0, LandShape.Samples).Select(i => Mathf.Cos((float)LandShape.Angle(i)) * (float)land.Outline[i] * 1.1f).ToArray();
                Assert.That(islet.extents.x, Is.EqualTo((xs.Max() - xs.Min()) * 0.5f).Within(0.3f), "The islet matches the game's waterline.");

                // The nearest point a cutter's hull can reach is the land edge plus nothing more than the jetty tip.
                var jetty = view.transform.Find("Jetty").GetComponent<Renderer>().bounds;
                var portSide = Mathf.Max(Mathf.Abs(jetty.min.x - (float)settlement.X), Mathf.Abs(jetty.max.x - (float)settlement.X));
                var jettyGap = Mathf.Min(Mathf.Abs(jetty.min.x - (float)settlement.X), Mathf.Abs(jetty.max.x - (float)settlement.X));
                Assert.That(jettyGap, Is.GreaterThan(0f), "The jetty does not reach the port point.");
                Assert.That(portSide, Is.LessThanOrEqualTo((float)Rules.SettlementLandOffset));
            }
        }

        [Test]
        public void Shipyard_ShowsEachBuildStageInTurn()
        {
            var parent = new GameObject("Yard under test");
            try
            {
                var slip = new Slipway(parent.transform, null, 0, 1f, -1f);
                slip.Show(ShipClass.Ram, 0.05f, 0.1f);
                Assert.That(slip.FramesShown, Is.EqualTo(0), "First only the keel.");
                Assert.That(slip.Scaffolded, Is.True);
                Assert.That(slip.ShipwrightsAtWork, Is.GreaterThan(0));

                slip.Show(ShipClass.Ram, 0.25f, 0.1f);
                var some = slip.FramesShown;
                Assert.That(some, Is.GreaterThan(0), "Frames rise.");
                Assert.That(slip.PlankedFraction, Is.EqualTo(0f));

                slip.Show(ShipClass.Ram, 0.5f, 0.1f);
                Assert.That(slip.FramesShown, Is.GreaterThan(some), "All frames stand before planking is far along.");
                Assert.That(slip.PlankedFraction, Is.InRange(0.1f, 0.9f), "Planking is half way up.");
                Assert.That(slip.Decked, Is.False);

                slip.Show(ShipClass.Ram, 0.8f, 0.1f);
                Assert.That(slip.PlankedFraction, Is.EqualTo(1f));
                Assert.That(slip.Decked, Is.True);
                Assert.That(slip.Rigged, Is.False);

                slip.Show(ShipClass.Ram, 0.99f, 0.1f);
                Assert.That(slip.Rigged, Is.True, "Oars and sail at the end.");
                Assert.That(slip.Scaffolded, Is.False, "Scaffolding down before launching.");
                Assert.That(slip.ShipwrightsAtWork, Is.EqualTo(0));

                slip.Show(null, 0f, 0.1f);
                Assert.That(slip.Building, Is.Null, "An idle line shows an empty ramp.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [UnityTest]
        public IEnumerator Launch_HullSlidesDownItsSlipwayThenTakesItsPlaceInTheGame()
        {
            yield return null;
            var home = battle.World.SettlementOf(0);
            home.Wood = home.Silver = 5000 * Rules.Centi;
            var before = battle.World.Ships.Select(ship => ship.Id).ToList();
            battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Cutter });
            Ship built = null;
            for (var i = 0; i < 40 * Rules.TickRate && built == null; i++)
            {
                battle.RunTicks(1);
                built = battle.World.Ships.FirstOrDefault(ship => ship.Class == ShipClass.Cutter && !before.Contains(ship.Id) && ship.Owner == 0);
            }
            Assert.That(built, Is.Not.Null);
            Assert.That(battle.LaunchesInProgress, Is.EqualTo(1));
            Assert.That(battle.LastNotice, Does.StartWith("Zwodowano"), "The herald announces the launch.");

            yield return null;
            var view = battle.Views[built.Id];
            var slip = battle.Yards[home.Id].Port;
            var onRamp = new Vector2(view.transform.position.x - slip.RestPosition.x, view.transform.position.z - slip.RestPosition.z).magnitude;
            Assert.That(onRamp, Is.LessThan(1.5f), "It starts on the cradle of its slipway, not out on the water.");

            battle.Paused = false;
            var waited = 0f;
            while (battle.LaunchesCompleted == 0 && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            battle.Paused = true;
            yield return null;
            Assert.That(battle.LaunchesCompleted, Is.EqualTo(1), "The launch ends.");
            var placed = new Vector2(view.transform.position.x - (float)built.X, view.transform.position.z - (float)built.Y).magnitude;
            Assert.That(placed, Is.LessThan(2f), "Afloat, it is where the game has it.");
        }

        IEnumerator WaitForInterface()
        {
            var waited = 0f;
            while (waited < 0.4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Hud_ShowsTheActivePortsStoreAndSwitchesOrdersWithSelection()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            Assert.That(hud, Is.Not.Null);
            var port = battle.ActivePort;
            Assert.That(hud.WoodText, Is.EqualTo((port.Available(ResourceType.Wood) / Rules.Centi).ToString()), "The bar shows the active port's free wood.");
            Assert.That(hud.VisibleCommands, Is.EqualTo(0), "Without a selection no order is offered.");

            // The bars are placed through the battle camera, which the test scene does not have by itself.
            var view = new GameObject("Test camera").AddComponent<Camera>();
            try
            {
                battle.Configure(null, null, view);
                var own = battle.World.Ships.First(ship => ship.Owner == 0 && ship.Class == ShipClass.Cutter);
                battle.SelectForTest(own.Id);
                yield return WaitForInterface();
                Assert.That(hud.VisibleCommands, Is.EqualTo(5), "A cutter: move, stop, harvest, repair, scrap – the rest are hidden.");
                Assert.That(hud.VisibleHealthBars, Is.GreaterThanOrEqualTo(1), "The selected ship shows its bar.");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Hud_ShipCardShowsTheCrewByTradeAndTheCrewChoiceSetsThePlan()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            var world = battle.World;
            var home = world.SettlementOf(0);
            var ram = world.SpawnShip(0, ShipClass.Ram, home.X + 300, 200);
            ram.Swordsmen -= 3;
            ram.Wounded = 2;
            battle.RunTicks(1);
            battle.SelectForTest(ram.Id);
            yield return WaitForInterface();
            Assert.That(hud.CrewText, Is.EqualTo($"{ram.Sailors}|{ram.Swordsmen}/{ram.Plan.Swordsmen}|{ram.Javelins}|{ram.Archers}|2"), "Sailors, swordsmen against the plan, javelin men, archers, wounded.");
            Assert.That(hud.CrewPlanOffered, Is.True, "An own warship offers the crew choice.");

            Assert.That(hud.OpenCrewPlanForTest(), Is.EqualTo(4), "Balanced, boarding, volley, seamen.");
            hud.PickCrewPlanForTest(1);
            battle.RunTicks(1);
            Assert.That(ram.Plan.Preset, Is.EqualTo(CrewPreset.Boarding), "The choice reaches the simulation as a command.");
            Assert.That(ram.Swordsmen, Is.EqualTo(ram.Definition.PlanFor(CrewPreset.Balanced).Swordsmen - 3), "The people change only in port.");

            // An enemy in sight of the ram (a hidden one cannot be selected at all).
            var enemy = world.SpawnShip(1, ShipClass.Boarder, ram.X + 150, ram.Y);
            battle.RunTicks(1);
            battle.ClearSelectionKeepPort();
            battle.SelectForTest(enemy.Id);
            yield return WaitForInterface();
            Assert.That(hud.CrewPlanOffered, Is.False, "No crew choice for an enemy.");
        }

        [UnityTest]
        public IEnumerator Hud_WeaponChoiceListsWhatFitsEachPostAndSetsThePlan()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            var world = battle.World;
            var ram = world.SpawnShip(0, ShipClass.Ram, world.SettlementOf(0).X + 300, 200);
            battle.RunTicks(1);
            battle.SelectForTest(ram.Id);
            yield return WaitForInterface();
            // Light post: empty, archers, javelin men; medium post: empty and the four medium machines.
            Assert.That(hud.OpenWeaponsForTest(), Is.EqualTo(3 + 5));
            battle.SetWeapon(ram.Id, 1, WeaponType.Scorpion);
            battle.RunTicks(1);
            Assert.That(ram.Loadout[1], Is.EqualTo(WeaponType.Scorpion), "The plan changes at once.");
            Assert.That(ram.Mounts[1], Is.EqualTo(WeaponType.None), "The weapon itself waits for a port.");
        }

        [UnityTest]
        public IEnumerator Boarding_FightersStandOnTheRailsOnlyWhileTheDecksFight()
        {
            yield return null;
            var world = battle.World;
            var boarder = world.SpawnShip(0, ShipClass.Boarder, 0, -950);
            var target = world.SpawnShip(1, ShipClass.Merchant, boarder.Definition.Radius + ShipCatalog.Get(ShipClass.Merchant).Radius + 2, -950);
            boarder.Heading = target.Heading = 0;
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Board, ShipIds = new[] { boarder.Id }, TargetId = target.Id });
            battle.RunTicks(30);
            Assert.That(world.Boardings.Any(link => !link.Hooked), Is.True, "Grapples thrown.");
            Assert.That(battle.DeckFightersShown, Is.Zero, "No one crosses while the hooks are still biting.");

            var fought = 0;
            for (var i = 0; i < 20 && target.Owner == 1; i++)
            {
                battle.RunTicks(15);
                fought = Mathf.Max(fought, battle.DeckFightersShown);
            }
            Assert.That(fought, Is.GreaterThan(0).And.LessThanOrEqualTo(2 * DeckFight.MaxPerSide), "Men fight on the rails during the melee.");
            battle.RunTicks(150);
            Assert.That(target.Owner, Is.EqualTo(0), "The merchant is taken.");
            Assert.That(battle.DeckFightersShown, Is.Zero, "The figures go when the fight ends.");
        }

        [Test]
        public void Keys_DefaultBindingsFollowTheContractWithoutConflicts()
        {
            KeyBindings.ResetAll();
            Assert.That(KeyBindings.AllConflicts(), Is.Empty);
            Assert.That(KeyBindings.Key(GameAction.Move), Is.EqualTo(KeyCode.M));
            Assert.That(KeyBindings.Key(GameAction.Stop), Is.EqualTo(KeyCode.S));
            Assert.That(KeyBindings.Key(GameAction.Repair), Is.EqualTo(KeyCode.P));
            Assert.That(KeyBindings.Key(GameAction.Pause), Is.EqualTo(KeyCode.Pause));
            Assert.That(KeyBindings.Key(GameAction.FocusAlert), Is.EqualTo(KeyCode.Space));
            var conflicts = KeyBindings.Bind(GameAction.Move, KeyCode.S);
            Assert.That(conflicts, Does.Contain(GameAction.Stop), "A clash is reported, not hidden.");
            KeyBindings.ResetAll();
        }

        [UnityTest]
        public IEnumerator Hud_HeraldShowsAtMostFourAndTabFoldsThePanels()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            var raise = typeof(BattleController).GetMethod("Announce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (var i = 0; i < 7; i++)
                raise.Invoke(battle, new object[] { $"Wiadomość {i}", HeraldLevel.Info, null, HeraldTopic.General, -1, $"test{i}" });
            Assert.That(hud.VisibleHeralds, Is.LessThanOrEqualTo(4));

            var before = hud.VisibleHeralds;
            raise.Invoke(battle, new object[] { "Atak!", HeraldLevel.Critical, Vector3.zero, HeraldTopic.Alarm, -1, "alarm:x" });
            raise.Invoke(battle, new object[] { "Atak!", HeraldLevel.Critical, Vector3.zero, HeraldTopic.Alarm, -1, "alarm:x" });
            Assert.That(hud.VisibleHeralds, Is.LessThanOrEqualTo(4), "Repeated alarms of one group join one entry.");

            Assert.That(hud.Collapsed, Is.False);
            var collapse = typeof(BattleHud).GetMethod("ToggleCollapse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            collapse.Invoke(hud, null);
            Assert.That(hud.Collapsed, Is.True);
            Assert.That(hud.IsPointerOverInterface(new Vector2(Screen.width * 0.5f, Screen.height * 0.04f)), Is.False, "Folded panels free the sea.");
            collapse.Invoke(hud, null);
            Assert.That(hud.Collapsed, Is.False);
        }

        [UnityTest]
        public IEnumerator Hud_CancellingAnAdvancedOrderAsksWithTheSimulationsSettlement()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            var port = battle.ActivePort;
            port.Wood = port.Silver = 3000 * Rules.Centi;
            battle.World.Issue(new Command { Faction = 0, Kind = CommandKind.QueueShip, Class = ShipClass.Ram, TargetId = port.Id });
            battle.RunTicks(22 * Rules.TickRate);
            var queued = port.WarQueue.Count;
            Assert.That(queued, Is.GreaterThan(0));

            hud.RequestCancel(true, 0);
            Assert.That(hud.ModalOpen, Is.True, "Past half way a confirmation comes first.");
            Assert.That(hud.HoldsInput, Is.True, "The dialog holds the input.");
            battle.RunTicks(1);
            Assert.That(port.WarQueue.Count, Is.EqualTo(queued), "Nothing is cancelled before confirming.");

            var confirm = typeof(BattleHud).GetMethod("ConfirmModal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            confirm.Invoke(hud, null);
            battle.RunTicks(1);
            Assert.That(hud.ModalOpen, Is.False);
            Assert.That(port.WarQueue.Count, Is.EqualTo(queued - 1));
        }

        [UnityTest]
        public IEnumerator Hud_PanelsTakeThePointerAndTheSeaDoesNot()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            Assert.That(hud.IsPointerOverInterface(new Vector2(Screen.width * 0.5f, Screen.height * 0.04f)), Is.True, "The bottom dock takes clicks.");
            Assert.That(hud.IsPointerOverInterface(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f)), Is.False, "The open sea does not.");
            Assert.That(hud.IsPointerOverInterface(new Vector2(Screen.width * 0.3f, Screen.height * 0.985f)), Is.True, "The top bar takes clicks.");
        }

        [UnityTest]
        public IEnumerator Oars_AreDrawnInOnTheSideWhereAnotherHullLies()
        {
            yield return null;
            // Open water between the home waters and the western rocky coast.
            var a = battle.World.SpawnShip(0, ShipClass.Ram, -260, 150);
            var b = battle.World.SpawnShip(0, ShipClass.Ram, -260, 150 + 5.6 + World.HullSpacing + 1);
            a.Heading = b.Heading = 0;
            a.Speed = b.Speed = a.Definition.Speed;
            battle.RunTicks(1);
            a.X = b.X = -260;
            a.Y = 150;
            b.Y = 150 + 5.6 + World.HullSpacing + 1;
            a.Speed = b.Speed = a.Definition.Speed;
            battle.Paused = false;
            var watched = 0f;
            while (watched < 2.5f)
            {
                a.Speed = b.Speed = a.Definition.Speed;
                watched += Time.unscaledDeltaTime;
                yield return null;
            }
            battle.Paused = true;

            // b lies on a's left (port) side in the game's plane; a's oars there are in, its other side rows.
            var view = battle.Views[a.Id];
            Assert.That(view.OarsShipped(-1), Is.GreaterThan(0.9f), "Oars toward the other hull are drawn in.");
            Assert.That(view.OarsShipped(1), Is.LessThan(0.1f), "The free side keeps rowing.");
        }

        [UnityTest]
        public IEnumerator Views_MatchSimulationShips()
        {
            yield return null;
            Assert.That(battle.Views.Count, Is.EqualTo(battle.World.Ships.Count));
            var extra = battle.World.SpawnShip(0, ShipClass.Ram, -400, 0);
            battle.RunTicks(1);
            Assert.That(battle.Views.ContainsKey(extra.Id), Is.True);
            extra.Hull = 0;
            battle.RunTicks(1);
            Assert.That(battle.Views.ContainsKey(extra.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator Fog_HiddenEnemyHasNoActiveRenderers()
        {
            yield return null;
            var enemy = battle.World.Ships.First(ship => ship.Owner == 1);
            Assert.That(battle.World.CanSee(0, enemy), Is.False);
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].gameObject.activeSelf, Is.False);
            var renderers = battle.Views[enemy.Id].GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Assert.That(renderers.Count(renderer => renderer.gameObject.activeInHierarchy), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Fog_EnemyBecomesVisibleWhenObserved()
        {
            yield return null;
            var enemy = battle.World.Ships.First(ship => ship.Owner == 1);
            battle.World.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator Visual_DoesNotChangeSimulationResult()
        {
            yield return null;
            var reference = MapFactory.CreateSkirmish(20261109);
            var opponent = new Opponent(1);
            for (var i = 0; i < 600; i++) { opponent.Think(reference); reference.Step(); }
            battle.RunTicks(600);
            yield return null;
            reference.Events.Clear();
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(reference))));
        }

        [UnityTest]
        public IEnumerator Order_RightClickOnNodeSendsCutterToHarvest()
        {
            yield return null;
            var cutter = battle.World.Ships.First(ship => ship.Owner == 0 && ship.Class == ShipClass.Cutter);
            var node = battle.World.Nodes.First(item => (item.KnownMask & 1) != 0);
            battle.SelectForTest(cutter.Id);
            battle.OrderAt(new Vector3((float)node.X, 0f, (float)node.Y));
            battle.RunTicks(1);
            Assert.That(cutter.Order, Is.EqualTo(OrderKind.Harvest));
            Assert.That(cutter.NodeId, Is.EqualTo(node.Id));
        }

        [UnityTest]
        public IEnumerator Save_LoadRestoresStateAndViews()
        {
            yield return null;
            var path = Path.Combine(Application.temporaryCachePath, "battle-test.mbsave");
            battle.RunTicks(300);
            battle.QuickSave(path);
            var saved = SaveGame.Hash(SaveGame.Serialize(battle.World));
            battle.RunTicks(300);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.Not.EqualTo(saved));

            Assert.That(battle.QuickLoad(path), Is.True);
            yield return null;
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.EqualTo(saved));
            Assert.That(battle.Views.Count, Is.EqualTo(battle.World.Ships.Count));
            File.Delete(path);
        }

        Ship HiddenEnemy() => battle.World.Ships.First(ship => ship.Owner == 1);

        [UnityTest]
        public IEnumerator Inspect_ShowsVisibleEnemyAndNeverAHiddenOne()
        {
            yield return null;
            var enemy = HiddenEnemy();
            var point = new Vector3((float)enemy.X, 0f, (float)enemy.Y);
            battle.Inspect(point);
            Assert.That(battle.InspectedEnemies, Is.Empty, "A hidden ship cannot be inspected.");

            var observer = battle.World.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);
            battle.Inspect(new Vector3((float)enemy.X, 0f, (float)enemy.Y));
            Assert.That(battle.InspectedEnemies, Does.Contain(enemy.Id));

            observer.Hull = 0;
            battle.RunTicks(1);
            Assert.That(battle.InspectedEnemies, Is.Empty, "Inspection ends when the ship leaves view.");
        }

        [UnityTest]
        public IEnumerator Target_EnemyIsMarkedAfterAttackOrderAndNotBefore()
        {
            yield return null;
            var world = battle.World;
            var enemy = HiddenEnemy();
            var attacker = world.SpawnShip(0, ShipClass.CrewDestroyer, enemy.X - 60, enemy.Y);
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].IsMarkedAsTarget, Is.False);

            Assert.That(battle.ShowsHealthBar(enemy), Is.False, "No bar over an enemy nobody attacks or inspects.");

            world.Issue(new Command { Faction = 0, Kind = CommandKind.AttackShip, ShipIds = new[] { attacker.Id }, TargetId = enemy.Id });
            battle.RunTicks(1);
            Assert.That(battle.TargetedShips, Does.Contain(enemy.Id));
            Assert.That(battle.Views[enemy.Id].IsMarkedAsTarget, Is.True);
            Assert.That(battle.ShowsHealthBar(enemy), Is.True, "The enemy under attack shows its hull.");

            world.Issue(new Command { Faction = 0, Kind = CommandKind.Stop, ShipIds = new[] { attacker.Id } });
            battle.RunTicks(1);
            Assert.That(battle.Views[enemy.Id].IsMarkedAsTarget, Is.False);
            Assert.That(battle.ShowsHealthBar(enemy), Is.False, "The bar goes when the attack ends.");
        }

        [Test]
        public void Waves_HigherBowLiftsTheBowAndTiltIsLimited()
        {
            var calm = HullPose.FromHeights(1f, 1f, 1f, 1f, 1f, 20f, 6f);
            Assert.That(calm.Heave, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(calm.Pitch, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(calm.Roll, Is.EqualTo(0f).Within(1e-4f));

            var bowUp = HullPose.FromHeights(1f, -1f, 0f, 0f, 0f, 20f, 6f);
            Assert.That(bowUp.Pitch, Is.LessThan(0f), "Negative rotation around X raises the bow.");

            var starboardUp = HullPose.FromHeights(0f, 0f, -0.5f, 0.5f, 0f, 20f, 6f);
            Assert.That(starboardUp.Roll, Is.GreaterThan(0f));

            var steep = HullPose.FromHeights(50f, -50f, -50f, 50f, 0f, 10f, 4f);
            Assert.That(Mathf.Abs(steep.Pitch), Is.EqualTo(HullPose.MaxTiltDegrees).Within(1e-4f));
            Assert.That(Mathf.Abs(steep.Roll), Is.EqualTo(HullPose.MaxTiltDegrees).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Visual_ShipHasNoWakeStrip()
        {
            yield return null;
            var view = battle.Views.Values.First();
            Assert.That(view.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Wake" || item.name == "Order line"), Is.False);
        }

        [UnityTest]
        public IEnumerator Fog_LayerFollowsTheThreeStates()
        {
            yield return null;
            var world = battle.World;
            battle.Fog.Update(world, 0, 0f, true);
            var home = world.SettlementOf(0);
            int Cell(double value) => (int)((value - World.FogOrigin) / World.FogCellSize);

            Assert.That(battle.Fog.AlphaAt(Cell(home.X), Cell(home.Y)), Is.EqualTo(0), "Own base is visible.");
            Assert.That(battle.Fog.AlphaAt(Cell(375), Cell(0)), Is.EqualTo(BattleFog.UnexploredAlpha), "Enemy base starts unexplored.");

            var scout = world.SpawnShip(0, ShipClass.Cutter, 0, 650);
            world.RefreshExplored();
            battle.Fog.Update(world, 0, 0f, true);
            Assert.That(battle.Fog.AlphaAt(Cell(0), Cell(650)), Is.EqualTo(0));

            world.Ships.Remove(scout);
            battle.Fog.Update(world, 0, 0f, true);
            Assert.That(battle.Fog.AlphaAt(Cell(0), Cell(650)), Is.EqualTo(BattleFog.ExploredAlpha), "Explored but no longer observed.");
        }

        [UnityTest]
        public IEnumerator Site_PileShrinksWithStockAndEmptiesAtZero()
        {
            yield return null;
            var node = battle.World.Nodes.First(item => item.Type == ResourceType.Wood && (item.KnownMask & 1) != 0);
            var site = battle.SiteViews[node.Id];
            Assert.That(site.VisiblePileParts, Is.EqualTo(site.TotalPileParts));
            Assert.That(site.TotalPileParts, Is.GreaterThan(4));
            var full = node.Stock;
            site.UpdateStock(full / 2);
            Assert.That(site.VisiblePileParts, Is.LessThan(site.TotalPileParts).And.GreaterThan(0));
            site.UpdateStock(1);
            Assert.That(site.VisiblePileParts, Is.EqualTo(1));
            site.UpdateStock(0);
            Assert.That(site.VisiblePileParts, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HealthBar_AppearsOnlyAfterMouseSelection()
        {
            yield return null;
            var world = battle.World;
            var own = world.Ships.First(ship => ship.Owner == 0);
            var enemy = HiddenEnemy();
            world.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);

            Assert.That(world.CanSee(0, enemy), Is.True);
            Assert.That(battle.ShowsHealthBar(own), Is.False, "An unselected own ship has no bar.");
            Assert.That(battle.ShowsHealthBar(enemy), Is.False, "An unselected visible enemy has no bar.");

            battle.SelectForTest(own.Id);
            Assert.That(battle.ShowsHealthBar(own), Is.True);

            battle.Inspect(new Vector3((float)enemy.X, 0f, (float)enemy.Y));
            Assert.That(battle.ShowsHealthBar(enemy), Is.True);
            Assert.That(battle.ShowsHealthBar(world.Ships.First(ship => ship.Owner == 0 && ship.Id != own.Id)), Is.False);
        }

        [UnityTest]
        public IEnumerator HealthBar_SeveralEnemiesCanBeSelectedAtOnce()
        {
            yield return null;
            var world = battle.World;
            var enemies = world.Ships.Where(ship => ship.Owner == 1).Take(2).ToArray();
            foreach (var enemy in enemies) world.SpawnShip(0, ShipClass.Cutter, enemy.X - 50, enemy.Y);
            battle.RunTicks(1);
            foreach (var enemy in enemies) battle.Inspect(new Vector3((float)enemy.X, 0f, (float)enemy.Y));
            Assert.That(enemies.All(battle.ShowsHealthBar), Is.True);
            Assert.That(battle.InspectedEnemies.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator HealthBar_SettlementShowsOnlyAfterClick()
        {
            yield return null;
            var home = battle.World.SettlementOf(0);
            Assert.That(battle.InspectedSettlementId, Is.EqualTo(-1));
            battle.Inspect(new Vector3((float)home.X, 0f, (float)home.Y));
            Assert.That(battle.InspectedSettlementId, Is.EqualTo(home.Id));
            var enemyHome = battle.World.SettlementOf(1);
            battle.Inspect(new Vector3((float)enemyHome.X, 0f, (float)enemyHome.Y));
            Assert.That(battle.InspectedSettlementId, Is.EqualTo(home.Id), "A hidden enemy settlement cannot be selected.");
        }

        [UnityTest]
        public IEnumerator Effects_HiddenVolleyCreatesNothingAndVisibleVolleyDoes()
        {
            yield return null;
            var world = battle.World;
            world.SpawnShip(1, ShipClass.CrewDestroyer, 900, 900);
            var unarmed = world.SpawnShip(0, ShipClass.CrewDestroyer, 900, -900);
            unarmed.Swordsmen = 0;
            unarmed.Archers = 0;
            var hiddenTarget = world.SpawnShip(0, ShipClass.Cutter, 950, 900);
            hiddenTarget.Hull = 100000;
            battle.Effects.Update(10f);
            var before = battle.Effects.ActiveCount;
            battle.RunTicks(120);
            Assert.That(world.CanSee(0, 950, 900), Is.True, "This cutter sees its own attacker, so effects are allowed.");
            Assert.That(battle.Effects.ActiveCount, Is.GreaterThan(before));

            var clean = new GameObject("Second battle").AddComponent<BattleController>();
            clean.Paused = true;
            yield return null;
            var enemyA = clean.World.SpawnShip(1, ShipClass.CrewDestroyer, 900, 900);
            var enemyB = clean.World.SpawnShip(0, ShipClass.Cutter, 950, 900);
            enemyB.Owner = 1;
            enemyA.Owner = 1;
            clean.World.Issue(new Command { Faction = 1, Kind = CommandKind.Stop, ShipIds = new[] { enemyA.Id } });
            clean.RunTicks(120);
            Assert.That(clean.World.CanSee(0, 900, 900), Is.False);
            Assert.That(clean.Effects.ActiveCount, Is.EqualTo(0));
            Object.DestroyImmediate(clean.gameObject);
        }

        [UnityTest]
        public IEnumerator Sink_VisibleShipLeavesAnAnimatedVisualWithoutSimulationEntry()
        {
            yield return null;
            var ship = battle.World.SpawnShip(0, ShipClass.Ram, -300, 100);
            battle.RunTicks(1);
            ship.Hull = 0;
            battle.RunTicks(1);
            Assert.That(battle.World.FindShip(ship.Id), Is.Null);
            Assert.That(battle.Views.ContainsKey(ship.Id), Is.False);
            Assert.That(battle.Effects.SinkingCount, Is.EqualTo(1));
            battle.Effects.Update(5f);
            Assert.That(battle.Effects.SinkingCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Wreck_ViewExistsOnlyWhileTheWreckDoesAndHidesInFog()
        {
            yield return null;
            var world = battle.World;
            var near = new Wreck { Id = world.NextId++, X = -300, Y = 0, Wood = 100, ExpiresTick = 100000 };
            var far = new Wreck { Id = world.NextId++, X = 900, Y = 900, Wood = 100, ExpiresTick = 100000 };
            world.Wrecks.Add(near);
            world.Wrecks.Add(far);
            battle.RunTicks(1);
            Assert.That(battle.WreckViews[near.Id].activeSelf, Is.True);
            Assert.That(battle.WreckViews[far.Id].activeSelf, Is.False);
            world.Wrecks.Remove(near);
            battle.RunTicks(1);
            Assert.That(battle.WreckViews.ContainsKey(near.Id), Is.False);
        }

        [UnityTest]
        public IEnumerator Order_DescriptionIsPlainAndHidesUnseenTargets()
        {
            yield return null;
            var world = battle.World;
            var ram = world.SpawnShip(0, ShipClass.Ram, -300, 0);
            Assert.That(battle.DescribeOrder(ram), Is.EqualTo("Brak rozkazu."));
            var enemy = HiddenEnemy();
            ram.Order = OrderKind.Ram;
            ram.TargetId = enemy.Id;
            Assert.That(battle.DescribeOrder(ram), Does.Contain("cel poza zasięgiem"));
            Assert.That(battle.DescribeOrder(ram), Does.Not.Contain("Kuter"));
        }

        [UnityTest]
        public IEnumerator Load_DamagedFileKeepsTheCurrentGame()
        {
            yield return null;
            var path = Path.Combine(Application.temporaryCachePath, "battle-damaged.mbsave");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            var world = battle.World;
            Assert.That(battle.QuickLoad(path), Is.False);
            Assert.That(battle.World, Is.SameAs(world));
            File.Delete(path);
        }
    }
}
