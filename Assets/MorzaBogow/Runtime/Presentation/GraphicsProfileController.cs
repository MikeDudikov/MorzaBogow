using System;
using MorzaBogow.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Selects a V10 visual profile. Presentation only: it never changes simulation or tactical information,
    /// and it never lowers quality automatically.
    /// </summary>
    public sealed class GraphicsProfileController : MonoBehaviour
    {
        const string ProfileKey = "graphics.profile";

        [SerializeField] Volume volume;
        [SerializeField] VolumeProfile[] volumeProfiles = Array.Empty<VolumeProfile>();

        bool panelVisible;

        public GraphicsProfile Current { get; private set; } = GraphicsProfiles.Default;
        public string Notice { get; private set; } = string.Empty;

        /// <summary>Off when the battle interface shows profiles and notices in its own settings window.</summary>
        public bool DrawImguiPanel { get; set; } = true;

        public void Configure(Volume targetVolume, VolumeProfile[] profiles)
        {
            volume = targetVolume;
            volumeProfiles = profiles;
        }

        void Awake()
        {
            var argument = ArgumentAfter("-morzaProfile");
            var saved = PlayerPrefs.GetString(ProfileKey, GraphicsProfiles.Default.ToString());
            Apply(GraphicsProfiles.ParseOrDefault(argument ?? saved), false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) panelVisible = !panelVisible;
        }

        public void Select(GraphicsProfile profile) => Apply(profile, true);

        void Apply(GraphicsProfile profile, bool save)
        {
            Current = profile;
            QualitySettings.SetQualityLevel((int)profile, true);

            var index = (int)profile;
            if (volume != null && index < volumeProfiles.Length) volume.sharedProfile = volumeProfiles[index];

            Notice = GraphicsProfiles.RequiresRayTracing(profile) && !SystemInfo.supportsRayTracing
                ? $"To urządzenie nie obsługuje ray tracingu wymaganego przez Ultra. Wybierz {GraphicsProfiles.DisplayName(GraphicsProfiles.SuggestionWithoutRayTracing)}."
                : string.Empty;

            if (!save) return;
            PlayerPrefs.SetString(ProfileKey, profile.ToString());
            PlayerPrefs.Save();
        }

        void OnGUI()
        {
            if (!DrawImguiPanel) return;
            if (!string.IsNullOrEmpty(Notice)) GUI.Label(new Rect(20, 50, 700, 30), Notice);
            if (!panelVisible) return;

            GUILayout.BeginArea(new Rect(340, 60, 220, 40 + GraphicsProfiles.All.Count * 30), GUI.skin.box);
            foreach (var profile in GraphicsProfiles.All)
            {
                var label = GraphicsProfiles.DisplayName(profile);
                if (GUILayout.Button(profile == Current ? $"[{label}]" : label)) Select(profile);
            }
            GUILayout.EndArea();
        }

        static string ArgumentAfter(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
