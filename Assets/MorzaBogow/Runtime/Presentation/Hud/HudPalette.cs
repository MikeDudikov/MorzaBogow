using UnityEngine;

namespace MorzaBogow.Presentation.Hud
{
    /// <summary>
    /// Colours of the battle interface, from the design tokens: dark sea-glazed ceramic, patinated bronze, calm
    /// light lettering and sparing terracotta. Silver stays cool and apart from the bronze. Owners always differ
    /// by shape too (round shield for the player, pelta/diamond for the enemy), never by colour alone.
    /// </summary>
    public static class HudPalette
    {
        public static readonly Color Surface = Hex(0x101e23);
        public static readonly Color SurfaceDeep = Hex(0x0b1418);
        public static readonly Color SurfaceRaised = Hex(0x23363b);
        public static readonly Color Text = Hex(0xf3ead6);
        public static readonly Color Muted = Hex(0xc7c7b7);
        public static readonly Color Bronze = Hex(0xbea16c);
        public static readonly Color Terracotta = Hex(0x9d553d);
        public static readonly Color Silver = Hex(0xd9d8cf);
        public static readonly Color Ally = Hex(0x66c8d2);
        public static readonly Color Enemy = Hex(0xf1846c);
        public static readonly Color Healthy = Hex(0x9ab86a);
        public static readonly Color Warning = Hex(0xe4b969);
        public static readonly Color Critical = Hex(0xf1846c);
        public static readonly Color Timber = Hex(0xb98a55);

        // Chart colours: calm navy water, ochre land.
        public static readonly Color Sea = Hex(0x163940);
        public static readonly Color SeaDeep = Hex(0x0f2a31);
        public static readonly Color Land = Hex(0xc9a66b);
        public static readonly Color LandDark = Hex(0x8f7447);
        public static readonly Color Fog = new Color(0.04f, 0.07f, 0.08f, 0.92f);

        public static Color Owner(int owner, int player) => owner == player ? Ally : Enemy;

        /// <summary>Hull colour by remaining share: healthy, warning, critical.</summary>
        public static Color Hull(float fraction) => fraction > 0.6f ? Healthy : fraction > 0.3f ? Warning : Critical;

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
