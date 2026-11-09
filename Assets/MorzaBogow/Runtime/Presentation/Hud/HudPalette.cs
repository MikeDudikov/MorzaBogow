using UnityEngine;

namespace MorzaBogow.Presentation.Hud
{
    /// <summary>
    /// Colours of the battle interface: fired clay, black-figure glaze, bronze, marble and parchment, the Aegean
    /// blue of the player and the cinnabar of the enemy. Owners always also differ by shape (round hoplon shield
    /// for the player, crescent pelta for the enemy), so the colours are never the only cue.
    /// </summary>
    public static class HudPalette
    {
        public static readonly Color Terracotta = Hex(0xA4522E);
        public static readonly Color TerracottaDeep = Hex(0x7A3A20);
        public static readonly Color Clay = Hex(0x4A2416);
        public static readonly Color ClayNight = Hex(0x2A150D);
        public static readonly Color Glaze = Hex(0x1C1512);
        public static readonly Color Ochre = Hex(0xD9A441);
        public static readonly Color OchreLight = Hex(0xF0C978);
        public static readonly Color Bronze = Hex(0x8C6A3B);
        public static readonly Color BronzeLight = Hex(0xC79B5A);
        public static readonly Color Patina = Hex(0x4F8A7A);
        public static readonly Color Marble = Hex(0xEDE6D6);
        public static readonly Color Parchment = Hex(0xE6D3A8);
        public static readonly Color ParchmentDark = Hex(0xC9B07A);
        public static readonly Color Aegean = Hex(0x2F6DB5);
        public static readonly Color AegeanLight = Hex(0x6FA3E0);
        public static readonly Color Cinnabar = Hex(0xB8322A);
        public static readonly Color CinnabarLight = Hex(0xE0574C);
        public static readonly Color Olive = Hex(0x6E8B3D);
        public static readonly Color OliveLight = Hex(0x9DBB5C);
        public static readonly Color Silver = Hex(0xC9C3B4);
        public static readonly Color SilverDark = Hex(0x7D776B);
        public static readonly Color Timber = Hex(0x9A6B3C);
        public static readonly Color Sea = Hex(0x123A5C);
        public static readonly Color SeaDeep = Hex(0x0B2238);
        public static readonly Color Sand = Hex(0xCDB27A);
        public static readonly Color Fog = new Color(0.10f, 0.07f, 0.05f, 0.9f);

        public static Color Owner(int owner, int player) => owner == player ? Aegean : Cinnabar;
        public static Color OwnerLight(int owner, int player) => owner == player ? AegeanLight : CinnabarLight;

        /// <summary>Hull colour by remaining share: olive while sound, ochre when hurt, cinnabar when failing.</summary>
        public static Color Hull(float fraction) => fraction > 0.6f ? OliveLight : fraction > 0.3f ? Ochre : CinnabarLight;

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
