using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The game's one reading of the keyboard and mouse, on the Input System package. Keys keep their KeyCode names, as
    /// KeyBindings saves and shows them; each maps to the Input System key at the same place on the keyboard (a US
    /// layout, which a Polish keyboard shares for every bound key). Mouse buttons 0–4 are left, right, middle, back and
    /// forward, as before. Without a keyboard or a mouse, as in batch mode, nothing is held or pressed and the pointer
    /// stays at the origin.
    /// </summary>
    public static class GameInput
    {
        /// <summary>The old "Mouse X" and "Mouse Y" axes moved by a tenth of the pointer's pixels (Input Manager sensitivity 0.1).</summary>
        public const float MouseAxisScale = 0.1f;

        static readonly Dictionary<KeyCode, Key> keys = BuildKeys();
        static readonly Dictionary<Key, KeyCode> codes = Invert(keys);

        /// <summary>The Input System key for a KeyCode; false for codes no keyboard key stands for.</summary>
        public static bool TryMap(KeyCode code, out Key key) => keys.TryGetValue(code, out key);

        /// <summary>The KeyCode a keyboard key is saved and shown as; None for keys the game does not bind.</summary>
        public static KeyCode CodeOf(Key key) => codes.TryGetValue(key, out var code) ? code : KeyCode.None;

        public static bool GetKey(KeyCode code) => Control(code)?.isPressed ?? false;
        public static bool GetKeyDown(KeyCode code) => Control(code)?.wasPressedThisFrame ?? false;
        public static bool GetKeyUp(KeyCode code) => Control(code)?.wasReleasedThisFrame ?? false;

        public static bool GetMouseButton(int button) => MouseButton(button)?.isPressed ?? false;
        public static bool GetMouseButtonDown(int button) => MouseButton(button)?.wasPressedThisFrame ?? false;
        public static bool GetMouseButtonUp(int button) => MouseButton(button)?.wasReleasedThisFrame ?? false;

        /// <summary>The pointer in screen pixels from the bottom left corner, as Input.mousePosition gave it.</summary>
        public static Vector3 MousePosition
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
            }
        }

        /// <summary>
        /// True once a mouse has reported where it is. Before its first event the pointer reads as the corner (0, 0), which
        /// must not count as a cursor resting at the screen's edge.
        /// </summary>
        public static bool PointerKnown => Mouse.current != null && Mouse.current.lastUpdateTime > 0;

        /// <summary>The wheel this frame; with the Input System's default uniform scrolling one notch is ±1, as before.</summary>
        public static Vector2 MouseScrollDelta => Mouse.current?.scroll.ReadValue() ?? Vector2.zero;

        /// <summary>The pointer's move this frame on the scale of the old "Mouse X" and "Mouse Y" axes.</summary>
        public static Vector2 MouseAxes => (Mouse.current?.delta.ReadValue() ?? Vector2.zero) * MouseAxisScale;

        /// <summary>
        /// True in the frame any keyboard key or mouse button goes down, also while another key is held (the Input
        /// System's "any key" only changes when the first key goes down).
        /// </summary>
        public static bool AnyKeyDown
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    foreach (var control in keyboard.allKeys)
                    {
                        if (control != null && control.wasPressedThisFrame) return true;
                    }
                }
                for (var button = 0; button <= 4; button++)
                {
                    if (GetMouseButtonDown(button)) return true;
                }
                return false;
            }
        }

        /// <summary>The first keyboard key pressed this frame, as the KeyCode the settings bind; false if none was.</summary>
        public static bool TryCapture(out KeyCode code)
        {
            code = KeyCode.None;
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame) continue;
                var mapped = CodeOf(control.keyCode);
                if (mapped == KeyCode.None) continue;
                code = mapped;
                return true;
            }
            return false;
        }

        static ButtonControl Control(KeyCode code)
        {
            if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6) return MouseButton(code - KeyCode.Mouse0);
            var keyboard = Keyboard.current;
            return keyboard != null && keys.TryGetValue(code, out var key) ? keyboard[key] : null;
        }

        static ButtonControl MouseButton(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return null;
            switch (button)
            {
                case 0: return mouse.leftButton;
                case 1: return mouse.rightButton;
                case 2: return mouse.middleButton;
                case 3: return mouse.backButton;
                case 4: return mouse.forwardButton;
                default: return null;
            }
        }

        static Dictionary<KeyCode, Key> BuildKeys()
        {
            var map = new Dictionary<KeyCode, Key>();
            for (var i = 0; i < 26; i++) map[KeyCode.A + i] = Key.A + i;
            for (var i = 0; i <= 9; i++) map[KeyCode.Alpha0 + i] = i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1);
            for (var i = 0; i <= 9; i++) map[KeyCode.Keypad0 + i] = Key.Numpad0 + i;
            // F1–F12 follow each other in both; F13 on comes after the OEM keys in the Input System's list.
            for (var i = 0; i < 12; i++) map[KeyCode.F1 + i] = Key.F1 + i;
            map[KeyCode.F13] = Key.F13;
            map[KeyCode.F14] = Key.F14;
            map[KeyCode.F15] = Key.F15;
            map[KeyCode.Space] = Key.Space;
            map[KeyCode.Return] = Key.Enter;
            map[KeyCode.Tab] = Key.Tab;
            map[KeyCode.Escape] = Key.Escape;
            map[KeyCode.Backspace] = Key.Backspace;
            map[KeyCode.Delete] = Key.Delete;
            map[KeyCode.Insert] = Key.Insert;
            map[KeyCode.Home] = Key.Home;
            map[KeyCode.End] = Key.End;
            map[KeyCode.PageUp] = Key.PageUp;
            map[KeyCode.PageDown] = Key.PageDown;
            map[KeyCode.UpArrow] = Key.UpArrow;
            map[KeyCode.DownArrow] = Key.DownArrow;
            map[KeyCode.LeftArrow] = Key.LeftArrow;
            map[KeyCode.RightArrow] = Key.RightArrow;
            map[KeyCode.Pause] = Key.Pause;
            map[KeyCode.Print] = Key.PrintScreen;
            map[KeyCode.ScrollLock] = Key.ScrollLock;
            map[KeyCode.Numlock] = Key.NumLock;
            map[KeyCode.CapsLock] = Key.CapsLock;
            map[KeyCode.Menu] = Key.ContextMenu;
            map[KeyCode.LeftShift] = Key.LeftShift;
            map[KeyCode.RightShift] = Key.RightShift;
            map[KeyCode.LeftControl] = Key.LeftCtrl;
            map[KeyCode.RightControl] = Key.RightCtrl;
            map[KeyCode.LeftAlt] = Key.LeftAlt;
            map[KeyCode.RightAlt] = Key.RightAlt;
            map[KeyCode.Comma] = Key.Comma;
            map[KeyCode.Period] = Key.Period;
            map[KeyCode.Slash] = Key.Slash;
            map[KeyCode.Backslash] = Key.Backslash;
            map[KeyCode.Semicolon] = Key.Semicolon;
            map[KeyCode.Quote] = Key.Quote;
            map[KeyCode.BackQuote] = Key.Backquote;
            map[KeyCode.LeftBracket] = Key.LeftBracket;
            map[KeyCode.RightBracket] = Key.RightBracket;
            map[KeyCode.Minus] = Key.Minus;
            map[KeyCode.Equals] = Key.Equals;
            map[KeyCode.KeypadPeriod] = Key.NumpadPeriod;
            map[KeyCode.KeypadDivide] = Key.NumpadDivide;
            map[KeyCode.KeypadMultiply] = Key.NumpadMultiply;
            map[KeyCode.KeypadMinus] = Key.NumpadMinus;
            map[KeyCode.KeypadPlus] = Key.NumpadPlus;
            map[KeyCode.KeypadEnter] = Key.NumpadEnter;
            map[KeyCode.KeypadEquals] = Key.NumpadEquals;
            return map;
        }

        static Dictionary<Key, KeyCode> Invert(Dictionary<KeyCode, Key> map)
        {
            var inverse = new Dictionary<Key, KeyCode>();
            foreach (var pair in map)
            {
                if (!inverse.ContainsKey(pair.Value)) inverse[pair.Value] = pair.Key;
            }
            return inverse;
        }
    }
}
