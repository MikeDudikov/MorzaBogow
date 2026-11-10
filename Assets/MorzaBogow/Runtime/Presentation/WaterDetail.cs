using System.Collections.Generic;
using MorzaBogow.Settings;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The water's decorative decals (bow waves, foam at hulls and bows, surf) from the High profile on, as the profile table
    /// has it (VISUAL_TECH_SPEC V10: Low "simple detail", Medium "limited detail and foam", High "full wake"). The
    /// simulation's sea is a decal too (SeaSurfaceRenderer), drawn on every profile, so the hulls always ride the waves the
    /// player sees. Every profile's HDRP asset supports water decals; this keeps the detail where the table puts it. It only
    /// turns the decals' components on and off: their owners still set their shape, strength and visibility as before.
    /// </summary>
    public static class WaterDetail
    {
        static readonly List<WaterDecal> decals = new List<WaterDecal>();
        static int appliedLevel = int.MinValue;

        /// <summary>Whether a quality level (a GraphicsProfile) draws the decorative decals.</summary>
        public static bool ShownAt(int qualityLevel) => qualityLevel >= (int)GraphicsProfile.High;

        public static bool Shown => ShownAt(QualitySettings.GetQualityLevel());

        /// <summary>Decorative decals followed, live ones and those destroyed since the last tidy.</summary>
        public static int Tracked => decals.Count;

        /// <summary>A new decorative decal: on or off for the current profile, and followed through profile changes.</summary>
        public static void Track(WaterDecal decal)
        {
            if (decal == null) return;
            if (decals.Count >= 512 && decals.Count % 512 == 0) decals.RemoveAll(item => item == null);
            decals.Add(decal);
            decal.enabled = Shown;
        }

        /// <summary>Once a frame: after a change of profile, every followed decal is turned on or off to match it.</summary>
        public static void Refresh()
        {
            var level = QualitySettings.GetQualityLevel();
            if (level == appliedLevel) return;
            appliedLevel = level;
            Apply(ShownAt(level));
        }

        /// <summary>Turns every followed decal on or off (Refresh does it for the profile; tests call it directly).</summary>
        public static void Apply(bool shown)
        {
            decals.RemoveAll(item => item == null);
            foreach (var decal in decals) decal.enabled = shown;
        }
    }
}
