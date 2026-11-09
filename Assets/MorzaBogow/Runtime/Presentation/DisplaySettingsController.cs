using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Settings;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Applies V13 resolution choices. This is presentation only and never touches simulation state.
    /// The IMGUI panel is a working M0 surface; the final menu replaces it.
    /// </summary>
    public sealed class DisplaySettingsController : MonoBehaviour
    {
        const string WidthKey = "display.width";
        const string HeightKey = "display.height";
        const string FullscreenKey = "display.fullscreen";

        readonly ResolutionChangeSession session = new ResolutionChangeSession();
        bool previousFullscreen;
        bool panelVisible;

        public IReadOnlyList<DisplayModeSize> Available { get; private set; } = new List<DisplayModeSize>();
        public ResolutionChangeSession Session => session;
        public string Notice { get; private set; } = string.Empty;

        /// <summary>Off when the battle interface shows resolutions, the confirmation and notices itself.</summary>
        public bool DrawImguiPanel { get; set; } = true;

        void Awake()
        {
            RefreshAvailable();
            ApplyStartupMode();
        }

        void Update()
        {
            if (session.Tick(Time.unscaledDeltaTime))
            {
                ApplyToScreen(session.Previous, previousFullscreen);
                Notice = "Przywrócono poprzednią rozdzielczość.";
            }

            if (Input.GetKeyDown(KeyCode.F10)) panelVisible = !panelVisible;
        }

        public void RefreshAvailable()
        {
            Available = ResolutionCatalog.Build(
                Screen.resolutions.Select(mode => new DisplayModeSize(mode.width, mode.height)));
        }

        public void Request(DisplayModeSize mode, bool fullscreen)
        {
            if (!Available.Contains(mode) || session.State == ResolutionChangeState.AwaitingConfirmation) return;
            previousFullscreen = Screen.fullScreen;
            session.Begin(new DisplayModeSize(Screen.width, Screen.height), mode);
            ApplyToScreen(mode, fullscreen);
            Notice = string.Empty;
        }

        public void Confirm()
        {
            if (session.State != ResolutionChangeState.AwaitingConfirmation) return;
            session.Confirm();
            Save(session.Requested, Screen.fullScreen);
        }

        void ApplyStartupMode()
        {
            var hasSavedChoice = PlayerPrefs.HasKey(WidthKey);
            bool? savedFullscreen = hasSavedChoice ? PlayerPrefs.GetInt(FullscreenKey, 1) == 1 : (bool?)null;

            // Unity remembers the last window mode by itself, so the wanted mode is always set explicitly.
            var fullscreen = DisplayStartup.WantsFullscreen(System.Environment.GetCommandLineArgs(), savedFullscreen);

            if (Available.Count == 0)
            {
                Notice = "Monitor nie zgłasza trybu z zakresu 1920×1080–2560×1440.";
                Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                return;
            }

            if (!hasSavedChoice)
            {
                ApplyToScreen(ResolutionCatalog.PickDefault(Available).Value, fullscreen);
                return;
            }

            var saved = new DisplayModeSize(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey));
            var chosen = ResolutionCatalog.PickFallback(saved, Available).Value;
            if (!chosen.Equals(saved)) Notice = $"Tryb {saved} jest niedostępny. Wybrano {chosen}.";
            ApplyToScreen(chosen, fullscreen);
        }

        static void ApplyToScreen(DisplayModeSize mode, bool fullscreen)
        {
            Screen.SetResolution(mode.Width, mode.Height,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        static void Save(DisplayModeSize mode, bool fullscreen)
        {
            PlayerPrefs.SetInt(WidthKey, mode.Width);
            PlayerPrefs.SetInt(HeightKey, mode.Height);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        void OnGUI()
        {
            if (!DrawImguiPanel) return;
            if (session.State == ResolutionChangeState.AwaitingConfirmation)
            {
                GUILayout.BeginArea(new Rect(20, 20, 420, 110), GUI.skin.box);
                GUILayout.Label($"Zachować {session.Requested}? Powrót za {Mathf.CeilToInt(session.RemainingSeconds)} s.");
                if (GUILayout.Button("Zachowaj")) Confirm();
                if (GUILayout.Button("Przywróć"))
                {
                    session.Revert();
                    ApplyToScreen(session.Previous, previousFullscreen);
                }
                GUILayout.EndArea();
                return;
            }

            if (!string.IsNullOrEmpty(Notice)) GUI.Label(new Rect(20, 20, 600, 30), Notice);
            if (!panelVisible) return;

            GUILayout.BeginArea(new Rect(20, 60, 300, 40 + Available.Count * 30), GUI.skin.box);
            foreach (var mode in Available)
            {
                if (GUILayout.Button(mode.ToString())) Request(mode, Screen.fullScreen);
            }
            GUILayout.EndArea();
        }
    }
}
