using System;
using System.Collections;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The game reads keys, buttons, the wheel and the pointer through the Input System. Virtual devices stand in for the
    /// keyboard and mouse, and input goes to the game whatever has the focus. The project runs on the Input System's
    /// default settings, kept in memory; the test changes two of them and puts them back, because the package destroys a
    /// default settings object it replaces.
    /// </summary>
    public sealed class GameInputTests
    {
        InputSettings.BackgroundBehavior savedBackground;
        InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        InputDevice[] realDevices = Array.Empty<InputDevice>();
        Keyboard keyboard;
        Mouse mouse;

        [SetUp]
        public void SetUp()
        {
            var settings = InputSystem.settings;
            savedBackground = settings.backgroundBehavior;
            savedEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // A real keyboard or mouse would take back ".current" with its first event; they rest during the test.
            realDevices = InputSystem.devices.Where(device => (device is Keyboard || device is Mouse) && device.enabled).ToArray();
            foreach (var device in realDevices) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>("Test keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Test mouse");
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
        }

        [TearDown]
        public void TearDown()
        {
            KeyBindings.Capturing = false;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            foreach (var device in realDevices)
            {
                if (!device.added) continue;
                InputSystem.EnableDevice(device);
                device.MakeCurrent();
            }
            var settings = InputSystem.settings;
            settings.backgroundBehavior = savedBackground;
            settings.editorInputBehaviorInPlayMode = savedEditorInput;
        }

        [Test]
        public void Input_KeyCodesMapToTheSameKeysAndBack()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                var code = KeyBindings.Default(action);
                Assert.That(GameInput.TryMap(code, out var key), Is.True, $"{action}: {code} has a key.");
                Assert.That(GameInput.CodeOf(key), Is.EqualTo(code), $"{action}: back to {code}.");
            }
            // The expected key of each code comes from the names alone, not from the map under test.
            var expected = Enumerable.Range(0, 26).Select(i => (KeyCode.A + i, ((char)('A' + i)).ToString()))
                .Concat(Enumerable.Range(0, 10).Select(i => (KeyCode.Alpha0 + i, $"Digit{i}")))
                .Concat(Enumerable.Range(0, 10).Select(i => (KeyCode.Keypad0 + i, $"Numpad{i}")))
                .Concat(Enumerable.Range(1, 15).Select(i => (KeyCode.F1 + (i - 1), $"F{i}")));
            foreach (var (code, name) in expected)
            {
                Assert.That(GameInput.TryMap(code, out var key), Is.True, code.ToString());
                Assert.That(key.ToString(), Is.EqualTo(name), $"{code} is the key named {name}.");
                Assert.That(GameInput.CodeOf(key), Is.EqualTo(code), code.ToString());
            }
            Assert.That(GameInput.TryMap(KeyCode.G, out var g) && g == Key.G, Is.True);
            Assert.That(GameInput.TryMap(KeyCode.Alpha1, out var one) && one == Key.Digit1, Is.True, "Digits along the top row.");
            Assert.That(GameInput.TryMap(KeyCode.Alpha0, out var zero) && zero == Key.Digit0, Is.True);
            Assert.That(GameInput.TryMap(KeyCode.F13, out var f13) && f13 == Key.F13, Is.True, "F13 comes after the OEM keys in the Input System.");
            Assert.That(GameInput.TryMap(KeyCode.Return, out var enter) && enter == Key.Enter, Is.True);
            Assert.That(GameInput.TryMap(KeyCode.LeftControl, out var control) && control == Key.LeftCtrl, Is.True);
            Assert.That(GameInput.TryMap(KeyCode.KeypadPlus, out var plus) && plus == Key.NumpadPlus, Is.True);
            Assert.That(GameInput.TryMap(KeyCode.Mouse0, out _), Is.False, "A mouse button is not a keyboard key.");
        }

        [Test]
        public void Input_KeysButtonsWheelAndPointerComeFromTheInputSystem()
        {
            var bound = KeyBindings.Key(GameAction.Formation);
            Assert.That(GameInput.TryMap(bound, out var key), Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            Assert.That(GameInput.GetKey(bound), Is.True);
            Assert.That(GameInput.GetKeyDown(bound), Is.True);
            Assert.That(GameInput.AnyKeyDown, Is.True);
            Assert.That(KeyBindings.Down(GameAction.Formation), Is.True, "An action fires from its bound key.");
            KeyBindings.Capturing = true;
            Assert.That(KeyBindings.Down(GameAction.Formation), Is.False, "Nothing fires while the settings wait for a key.");
            Assert.That(GameInput.TryCapture(out var captured), Is.True);
            Assert.That(captured, Is.EqualTo(bound), "The settings learn the key that was pressed.");
            KeyBindings.Capturing = false;

            InputSystem.Update();
            Assert.That(GameInput.GetKey(bound), Is.True, "Still held in the next frame.");
            Assert.That(GameInput.GetKeyDown(bound), Is.False, "Pressed only in its own frame.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(GameInput.GetKey(bound), Is.False);
            Assert.That(GameInput.GetKeyUp(bound), Is.True);

            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(320f, 240f), delta = new Vector2(30f, -10f), scroll = new Vector2(0f, 1f) }.WithButton(MouseButton.Right));
            InputSystem.Update();
            Assert.That(GameInput.MousePosition, Is.EqualTo(new Vector3(320f, 240f, 0f)), "Pixels from the bottom left, as before.");
            Assert.That(GameInput.GetMouseButtonDown(1), Is.True);
            Assert.That(GameInput.GetMouseButton(1), Is.True);
            Assert.That(GameInput.GetMouseButton(0), Is.False);
            Assert.That(GameInput.GetKey(KeyCode.Mouse1), Is.True, "A mouse button by its KeyCode.");
            Assert.That(GameInput.AnyKeyDown, Is.True, "A mouse button counts as a key going down.");
            Assert.That(GameInput.TryCapture(out _), Is.False, "But it cannot be bound.");
            Assert.That(GameInput.MouseScrollDelta, Is.EqualTo(mouse.scroll.ReadValue()), "The wheel as the Input System reads it.");
            Assert.That(GameInput.MouseScrollDelta.y, Is.GreaterThan(0f));
            Assert.That(mouse.delta.ReadValue().x, Is.Not.Zero, "The pointer's move arrived.");
            Assert.That(GameInput.MouseAxes.x, Is.EqualTo(mouse.delta.ReadValue().x * 0.1f).Within(1e-5f), "The old Mouse X axis: a tenth of the pixels.");
            Assert.That(GameInput.MouseAxes.y, Is.EqualTo(mouse.delta.ReadValue().y * 0.1f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator Input_TheFormationKeyTogglesAGroupsFormationThroughTheInterface()
        {
            const string preference = "orders.formation";
            var had = PlayerPrefs.HasKey(preference);
            var before = PlayerPrefs.GetString(preference, string.Empty);
            var host = new GameObject("Battle under test");
            var battle = host.AddComponent<BattleController>();
            battle.Paused = true;
            try
            {
                yield return null;
                var home = battle.World.SettlementOf(0);
                var first = battle.World.SpawnShip(0, ShipClass.Ram, home.X + 300, 200);
                var second = battle.World.SpawnShip(0, ShipClass.Ram, home.X + 330, 200);
                battle.RunTicks(1);
                battle.SelectForTest(first.Id);
                battle.SelectForTest(second.Id);
                battle.SetGroupFormation(FormationKind.Line, false);
                yield return null;

                Assert.That(GameInput.TryMap(KeyBindings.Key(GameAction.Formation), out var key), Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null;
                Assert.That(battle.GroupFormation, Is.EqualTo(FormationKind.Wedge), "The key, read through the Input System, switches the group to a wedge.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(battle.GroupFormation, Is.EqualTo(FormationKind.Wedge), "Releasing the key changes nothing.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
                // The test's switch is not the player's choice.
                if (had) PlayerPrefs.SetString(preference, before);
                else PlayerPrefs.DeleteKey(preference);
                PlayerPrefs.Save();
            }
        }
    }
}
