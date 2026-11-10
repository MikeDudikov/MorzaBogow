namespace MorzaBogow.Presentation
{
    /// <summary>
    /// What a ship's paint shows of the ship itself, carried per renderer by Renderer.SetShaderUserValue and read by the
    /// ship paint graph (Art/Ships/Shaders/ShipPaint.hlsl). Four bits a field, 0..15 read as 0..1:
    /// <list type="bullet">
    /// <item>bits 0-3: wear, from the look (its finish and seed);</item>
    /// <item>bits 4-7: wetness and bits 8-11: soot, reserved for VISUAL_TECH_SPEC V03 — their place is kept and the
    /// paint decodes them, but shows them only once the game has rain, spray or fire;</item>
    /// <item>bits 12-31: zero.</item>
    /// </list>
    /// HDRP keeps the value in the per-draw buffer, so ships that differ still share their palette materials and the SRP
    /// Batcher. Nothing here is saved: the same look always gives the same value, so the save format does not change.
    /// </summary>
    public static class ShipSurface
    {
        public const int Levels = 15, WearShift = 0, WetnessShift = 4, SootShift = 8;
        const uint Field = 15u;

        /// <summary>The renderer value of the given levels (each clamped to 0..15); every other bit is zero.</summary>
        public static uint Pack(int wear, int wetness = 0, int soot = 0) =>
            Clamp(wear) << WearShift | Clamp(wetness) << WetnessShift | Clamp(soot) << SootShift;

        /// <summary>One field's level, 0..15.</summary>
        public static int Level(uint value, int shift) => (int)((value >> shift) & Field);

        /// <summary>One field as the paint reads it, 0..1.</summary>
        public static float Amount(uint value, int shift) => Level(value, shift) / (float)Levels;

        /// <summary>The same value with no wear: new planks of a repair in this match.</summary>
        public static uint Unworn(uint value) => value & ~(Field << WearShift);

        /// <summary>A ship's renderer value from its look: its wear; no wetness or soot yet.</summary>
        public static uint Of(ShipLook look) => Pack(WearOf(look));

        /// <summary>
        /// How weathered the look is, 0..15: fresh 1-4, seasoned 5-9, old 10-13 (an old hull still looks seaworthy), and 0
        /// for the plain look. Within its finish's range from the look's own seed, never the game's or Unity's random
        /// numbers, so a saved look keeps its wear.
        /// </summary>
        public static int WearOf(ShipLook look)
        {
            if (look == null || look.IsPlain) return 0;
            var (low, high) = look.Finish switch
            {
                "fresh" => (1, 4),
                "seasoned" => (5, 9),
                "old" => (10, 13),
                _ => (0, 0),
            };
            if (high == 0) return 0;
            return low + (int)(Mix(look.Seed) % (uint)(high - low + 1));
        }

        static uint Clamp(int level) => (uint)(level < 0 ? 0 : level > Levels ? Levels : level);

        /// <summary>A finalizer of the seed, offset so the wear is not the same draw as anything else made from it.</summary>
        static uint Mix(uint seed)
        {
            unchecked
            {
                var h = seed ^ 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
