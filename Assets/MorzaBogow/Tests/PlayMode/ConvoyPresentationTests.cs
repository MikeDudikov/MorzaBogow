using System.Collections;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// E03 convoys in the interface: the port card names the priority port, sets the port's minimum and sends the idle
    /// merchants on convoy duty; a merchant's card switches its convoy duty on and off. Each is an ordinary command.
    /// </summary>
    public sealed class ConvoyPresentationTests
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

        static IEnumerator WaitForInterface()
        {
            var waited = 0f;
            while (waited < 0.4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Hud_ConvoysAreSetFromThePortCardAndTheMerchantsCard()
        {
            yield return WaitForInterface();
            var hud = battle.Hud;
            var world = battle.World;
            var home = world.SettlementOf(BattleController.PlayerFaction);
            battle.SelectPort(home.Id);
            yield return WaitForInterface();

            // The priority port, four minimums, the idle merchants.
            Assert.That(hud.OpenConvoysForTest(), Is.EqualTo(2 + BattleController.PortMinimums.Length));
            Assert.That(hud.PopoverValuesForTest[0], Is.EqualTo("ten port"), "Home is the priority port by default.");
            Assert.That(hud.PopoverValuesForTest[2], Is.EqualTo("60 D · 20 S"), "A cutter's reserve, the default minimum.");
            hud.PickConvoyForTest(BattleController.PortMinimums.Length);
            battle.RunTicks(1);
            Assert.That(home.KeepWood, Is.EqualTo(500 * Rules.Centi), "The choice reaches the simulation as a command.");
            Assert.That(home.KeepSilver, Is.EqualTo(200 * Rules.Centi));

            var merchant = world.SpawnShip(BattleController.PlayerFaction, ShipClass.Merchant, home.X + 300, 200);
            battle.RunTicks(1);
            hud.OpenConvoysForTest();
            hud.PickConvoyForTest(1 + BattleController.PortMinimums.Length);
            battle.RunTicks(1);
            Assert.That(merchant.Order, Is.EqualTo(OrderKind.Convoy), "The idle merchant goes on convoy duty.");
            Assert.That(battle.DescribeOrder(merchant), Does.StartWith("Rozkaz: konwój"));

            battle.ClearSelectionKeepPort();
            battle.SelectForTest(merchant.Id);
            yield return WaitForInterface();
            Assert.That(hud.ConvoySwitchForTest, Is.EqualTo("Konwój: tak"));
            battle.ToggleConvoySelected();
            battle.RunTicks(1);
            Assert.That(merchant.Order, Is.Not.EqualTo(OrderKind.Convoy), "The same switch takes it off.");
            yield return WaitForInterface();
            Assert.That(hud.ConvoySwitchForTest, Is.EqualTo("Konwój"));
        }
    }
}
