using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    public enum GameAction
    {
        // Orders (fixed order on the command grid)
        Move, Stop, Attack, Ram, Board, Volley, Harvest, Deliver, Outpost, Escort, Repair, Scrap,
        // The formation a group takes when it moves (a switch beside the orders)
        Formation,
        // Game and interface
        Pause, FocusAlert, CollapseHud, Cancel, Save, Load, CancelOrder, Faster, Slower,
        // Camera
        CameraLeft, CameraRight, CameraUp, CameraDown, RotateLeft, RotateRight, TiltUp, TiltDown, CameraReset,
    }

    /// <summary>
    /// The one place keys are bound. Defaults follow the interface contract (orders M S A R B V H D F E P X, G for a
    /// group's formation, Pause, Space to the alarm, Tab to fold the panels); the camera moves on the arrows so no letter
    /// is bound twice. The player may rebind any action; conflicts are reported, never silently resolved. Saved in
    /// PlayerPrefs by name, as KeyCode, and read through GameInput on the Input System. While a key is being captured,
    /// no action fires.
    /// </summary>
    public static class KeyBindings
    {
        const string Prefix = "keys.";

        static readonly Dictionary<GameAction, KeyCode> defaults = new Dictionary<GameAction, KeyCode>
        {
            { GameAction.Move, KeyCode.M }, { GameAction.Stop, KeyCode.S }, { GameAction.Attack, KeyCode.A },
            { GameAction.Ram, KeyCode.R }, { GameAction.Board, KeyCode.B }, { GameAction.Volley, KeyCode.V },
            { GameAction.Harvest, KeyCode.H }, { GameAction.Deliver, KeyCode.D }, { GameAction.Outpost, KeyCode.F },
            { GameAction.Escort, KeyCode.E }, { GameAction.Repair, KeyCode.P }, { GameAction.Scrap, KeyCode.X },
            { GameAction.Formation, KeyCode.G },
            { GameAction.Pause, KeyCode.Pause }, { GameAction.FocusAlert, KeyCode.Space }, { GameAction.CollapseHud, KeyCode.Tab },
            { GameAction.Cancel, KeyCode.Escape }, { GameAction.Save, KeyCode.F5 }, { GameAction.Load, KeyCode.F8 },
            { GameAction.CancelOrder, KeyCode.Delete }, { GameAction.Faster, KeyCode.Equals }, { GameAction.Slower, KeyCode.Minus },
            { GameAction.CameraLeft, KeyCode.LeftArrow }, { GameAction.CameraRight, KeyCode.RightArrow },
            { GameAction.CameraUp, KeyCode.UpArrow }, { GameAction.CameraDown, KeyCode.DownArrow },
            { GameAction.RotateLeft, KeyCode.Comma }, { GameAction.RotateRight, KeyCode.Period },
            { GameAction.TiltUp, KeyCode.PageUp }, { GameAction.TiltDown, KeyCode.PageDown }, { GameAction.CameraReset, KeyCode.Home },
        };

        static Dictionary<GameAction, KeyCode> current;

        /// <summary>True while the settings wait for a new key: nothing in the game reacts to keys then.</summary>
        public static bool Capturing { get; set; }

        static Dictionary<GameAction, KeyCode> Current
        {
            get
            {
                if (current != null) return current;
                current = new Dictionary<GameAction, KeyCode>(defaults);
                foreach (var pair in defaults)
                {
                    var saved = PlayerPrefs.GetString(Prefix + pair.Key, string.Empty);
                    if (saved.Length == 0 || !Enum.TryParse(saved, out KeyCode key)) continue;
                    // A key the Input System cannot read would silently stop the action: the default comes back instead.
                    if (GameInput.TryMap(key, out _)) current[pair.Key] = key;
                    else Debug.LogWarning($"[Klawisze] {Name(pair.Key)}: zapisany klawisz {saved} jest nieobsługiwany; przywrócono {Label(pair.Value)}.");
                }
                return current;
            }
        }

        public static KeyCode Key(GameAction action) => Current[action];
        public static KeyCode Default(GameAction action) => defaults[action];

        public static bool Down(GameAction action) => !Capturing && GameInput.GetKeyDown(Current[action]);
        public static bool Held(GameAction action) => !Capturing && GameInput.GetKey(Current[action]);

        /// <summary>Binds a key; returns the other actions that now share it, for the settings to show.</summary>
        public static List<GameAction> Bind(GameAction action, KeyCode key)
        {
            Current[action] = key;
            PlayerPrefs.SetString(Prefix + action, key.ToString());
            PlayerPrefs.Save();
            return ConflictsOf(action);
        }

        public static void ResetAll()
        {
            foreach (var pair in defaults) PlayerPrefs.DeleteKey(Prefix + pair.Key);
            PlayerPrefs.Save();
            current = null;
        }

        public static List<GameAction> ConflictsOf(GameAction action)
        {
            var key = Current[action];
            var conflicts = new List<GameAction>();
            foreach (var pair in Current)
            {
                if (pair.Key != action && pair.Value == key) conflicts.Add(pair.Key);
            }
            return conflicts;
        }

        /// <summary>Every pair of actions bound to the same key.</summary>
        public static List<(GameAction, GameAction)> AllConflicts()
        {
            var list = new List<(GameAction, GameAction)>();
            var actions = (GameAction[])Enum.GetValues(typeof(GameAction));
            for (var i = 0; i < actions.Length; i++)
            for (var j = i + 1; j < actions.Length; j++)
            {
                if (Current[actions[i]] == Current[actions[j]]) list.Add((actions[i], actions[j]));
            }
            return list;
        }

        /// <summary>Short label of a key as printed on a button.</summary>
        public static string Label(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space: return "Spacja";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Delete: return "Del";
                case KeyCode.Pause: return "Pause";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Equals: return "+";
                case KeyCode.Minus: return "−";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.PageUp: return "PgUp";
                case KeyCode.PageDown: return "PgDn";
                default: return key.ToString().Replace("Alpha", string.Empty).Replace("Keypad", "Num ");
            }
        }

        public static string Name(GameAction action)
        {
            switch (action)
            {
                case GameAction.Move: return "Ruch";
                case GameAction.Stop: return "Stop";
                case GameAction.Attack: return "Atak";
                case GameAction.Ram: return "Taran";
                case GameAction.Board: return "Abordaż";
                case GameAction.Volley: return "Ostrzał";
                case GameAction.Harvest: return "Zbiór";
                case GameAction.Deliver: return "Dostawa";
                case GameAction.Outpost: return "Przyczółek";
                case GameAction.Escort: return "Eskorta";
                case GameAction.Repair: return "Remont";
                case GameAction.Scrap: return "Złomowanie";
                case GameAction.Formation: return "Szyk grupy";
                case GameAction.Pause: return "Pauza";
                case GameAction.FocusAlert: return "Do alarmu";
                case GameAction.CollapseHud: return "Zwiń panele";
                case GameAction.Cancel: return "Anuluj / wróć";
                case GameAction.Save: return "Szybki zapis";
                case GameAction.Load: return "Szybkie wczytanie";
                case GameAction.CancelOrder: return "Anuluj zamówienie";
                case GameAction.Faster: return "Szybciej";
                case GameAction.Slower: return "Wolniej";
                case GameAction.CameraLeft: return "Kamera w lewo";
                case GameAction.CameraRight: return "Kamera w prawo";
                case GameAction.CameraUp: return "Kamera w górę";
                case GameAction.CameraDown: return "Kamera w dół";
                case GameAction.RotateLeft: return "Obrót w lewo";
                case GameAction.RotateRight: return "Obrót w prawo";
                case GameAction.TiltUp: return "Pochylenie +";
                case GameAction.TiltDown: return "Pochylenie −";
                default: return "Kamera: domyślny widok";
            }
        }
    }
}
