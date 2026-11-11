using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Bakes the cliff plants' texture (CliffPlants) in the four quarters the trees' leaf atlases use: rock samphire
    /// from the side and from above, sea lavender's flower sprays and its rosette. Colour with coverage in alpha, a
    /// tangent-space normal map and an HDRP mask map (R metallic, G occlusion, B detail mask, A smoothness), drawn with
    /// LeafAtlas's canvas. From the floras read (report 8.15–8.23):
    /// <list type="bullet">
    /// <item>samphire: a much-branched, fleshy, glaucous grey-green cushion 15–50 cm high, leaf segments 1–2 cm, narrow
    /// and thick; umbels of 8–36 rays with small whitish to yellow-green flowers, in flower June to September;</item>
    /// <item>sea lavender of the Aegean cliffs (Limonium rhodense, L. virgatum): a rosette of narrow fleshy leaves
    /// 2–8.5 cm long, stems 10–50 cm branched nearly from the base, spikes of small pale violet to red-violet
    /// flowers, July to September.</item>
    /// </list>
    /// Drawn in flower: the game's light is the Aegean summer. Deterministic: the same size gives the same pixels.
    /// </summary>
    public static class CliffPlantAtlas
    {
        // Colours (sRGB) chosen to the floras' words: glaucous grey-green, whitish to yellow-green, pale violet.
        static readonly Color SamphireLeaf = new Color(0.47f, 0.56f, 0.47f);
        static readonly Color SamphireStem = new Color(0.52f, 0.55f, 0.42f);
        static readonly Color SamphireFlower = new Color(0.82f, 0.82f, 0.56f);
        static readonly Color LavenderLeaf = new Color(0.31f, 0.40f, 0.27f);
        static readonly Color LavenderStem = new Color(0.46f, 0.50f, 0.40f);
        static readonly Color LavenderCalyx = new Color(0.86f, 0.85f, 0.88f);
        static readonly Color LavenderFlower = new Color(0.66f, 0.55f, 0.79f);

        public static LeafAtlas.Result Bake(int size = 1024)
        {
            size = Mathf.Max(64, size - size % 2);
            var half = size / 2;
            var canvases = new[] { new LeafAtlas.Canvas(half), new LeafAtlas.Canvas(half), new LeafAtlas.Canvas(half), new LeafAtlas.Canvas(half) };
            var samphireSide = new StableHash.Sequence(0xC1F0u);
            var lavenderSpray = new StableHash.Sequence(0xC1F1u);
            var lavenderRosette = new StableHash.Sequence(0xC1F2u);
            var samphireTop = new StableHash.Sequence(0xC1F3u);
            SamphireSide(canvases[CliffPlants.SamphireSide], ref samphireSide);
            LavenderSpray(canvases[CliffPlants.LavenderSpray], ref lavenderSpray);
            LavenderRosette(canvases[CliffPlants.LavenderRosette], ref lavenderRosette);
            SamphireTop(canvases[CliffPlants.SamphireTop], ref samphireTop);

            var result = new LeafAtlas.Result { Size = size, Colour = new Color32[size * size], Normal = new Color32[size * size], Mask = new Color32[size * size] };
            for (var cell = 0; cell < 4; cell++)
            {
                LeafAtlas.Dilate(canvases[cell]);
                var corner = TreeGenerator.AtlasCells[cell];
                LeafAtlas.Blit(canvases[cell], result, (int)(corner.x * size), (int)(corner.y * size));
            }
            return result;
        }

        /// <summary>
        /// Samphire seen from the side: zig-zag stems fanning up and out from the crack (the card is the clump's width,
        /// about 40 cm, so 1 cm is 0.025 of it), fleshy segments in twos and threes along them, umbels on top.
        /// </summary>
        static void SamphireSide(LeafAtlas.Canvas canvas, ref StableHash.Sequence random)
        {
            var stems = random.Range(17, 23);
            for (var s = 0; s < stems; s++)
            {
                var foot = new Vector2(0.5f + random.Range(-0.08f, 0.08f), 0.02f);
                var lean = (s - 0.5f * (stems - 1)) / stems * 2.2f + random.Range(-0.2f, 0.2f);
                var length = random.Range(0.45f, 0.82f);
                var tip = foot + new Vector2(Mathf.Sin(lean), Mathf.Cos(lean)) * length;
                tip.x = Mathf.Clamp(tip.x, 0.06f, 0.94f);
                tip.y = Mathf.Clamp(tip.y, 0.1f, 0.95f);
                var bend = Vector2.Lerp(foot, tip, 0.5f) + new Vector2(random.Range(-0.06f, 0.06f), random.Range(-0.03f, 0.03f));
                var path = LeafAtlas.Curve(foot, bend, tip, 16);
                Path(canvas, path, 0.006f, 0.0035f, SamphireStem, 0.25f);
                Segments(canvas, path, ref random, SamphireLeaf, 0.028f, 0.05f, 0.0055f, 0.45f);
                if (random.Next() < 0.6f) Umbel(canvas, tip, path[path.Length - 1] - path[path.Length - 2], ref random, SamphireFlower, 0.04f, 0.075f);
            }
        }

        /// <summary>Samphire from above: the same stems radiating from the middle of the cushion.</summary>
        static void SamphireTop(LeafAtlas.Canvas canvas, ref StableHash.Sequence random)
        {
            var centre = new Vector2(0.5f, 0.5f);
            var stems = random.Range(17, 22);
            for (var s = 0; s < stems; s++)
            {
                var angle = (s + random.Range(0f, 0.8f)) / stems * 2f * Mathf.PI;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var start = centre + direction * random.Range(0f, 0.06f);
                var tip = centre + direction * random.Range(0.28f, 0.44f);
                var bend = Vector2.Lerp(start, tip, 0.5f) + LeafAtlas.Rotate(direction, Mathf.PI * 0.5f) * random.Range(-0.05f, 0.05f);
                var path = LeafAtlas.Curve(start, bend, tip, 14);
                Path(canvas, path, 0.006f, 0.0035f, SamphireStem, 0.25f);
                Segments(canvas, path, ref random, SamphireLeaf, 0.028f, 0.05f, 0.0055f, 0.45f);
                if (random.Next() < 0.45f) Umbel(canvas, tip, direction, ref random, SamphireFlower, 0.035f, 0.06f);
            }
        }

        /// <summary>Sea lavender's flower stems: forked from near the base, spikes of small flowers at the ends.</summary>
        static void LavenderSpray(LeafAtlas.Canvas canvas, ref StableHash.Sequence random)
        {
            var stems = random.Range(5, 8);
            for (var s = 0; s < stems; s++)
            {
                var foot = new Vector2(0.5f + random.Range(-0.05f, 0.05f), 0.02f);
                var angle = (s - 0.5f * (stems - 1)) * 0.28f + random.Range(-0.1f, 0.1f);
                Fork(canvas, foot, angle, random.Range(0.22f, 0.3f), 3, ref random);
            }
        }

        static void Fork(LeafAtlas.Canvas canvas, Vector2 from, float angle, float length, int depth, ref StableHash.Sequence random)
        {
            var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            var to = from + direction * length;
            to.x = Mathf.Clamp(to.x, 0.04f, 0.96f);
            to.y = Mathf.Min(to.y, 0.94f);
            Path(canvas, LeafAtlas.Curve(from, Vector2.Lerp(from, to, 0.5f) + new Vector2(random.Range(-0.02f, 0.02f), 0f), to, 8), 0.004f, 0.003f, LavenderStem, 0.2f);
            if (depth == 0)
            {
                Spike(canvas, to, direction, ref random);
                return;
            }
            var spread = random.Range(0.35f, 0.6f);
            Fork(canvas, to, angle - spread, length * random.Range(0.6f, 0.8f), depth - 1, ref random);
            Fork(canvas, to, angle + spread, length * random.Range(0.6f, 0.8f), depth - 1, ref random);
        }

        /// <summary>A spike 15–30 mm: small flowers in two rows, a pale calyx with the violet corolla over it.</summary>
        static void Spike(LeafAtlas.Canvas canvas, Vector2 start, Vector2 direction, ref StableHash.Sequence random)
        {
            var flowers = random.Range(7, 11);
            var across = LeafAtlas.Rotate(direction, Mathf.PI * 0.5f);
            for (var f = 0; f < flowers; f++)
            {
                var along = start + direction * (0.012f * f);
                var side = f % 2 == 0 ? 1f : -1f;
                var at = along + across * (0.006f * side);
                Dot(canvas, at, 0.008f, LavenderCalyx, 0.15f, 0.9f);
                Dot(canvas, at + direction * 0.003f, 0.0065f, LavenderFlower * random.Range(0.9f, 1.08f), 0.15f, 0.95f);
            }
        }

        /// <summary>Sea lavender's rosette from above: narrow, fleshy, dark green leaves radiating flat on the rock.</summary>
        static void LavenderRosette(LeafAtlas.Canvas canvas, ref StableHash.Sequence random)
        {
            var centre = new Vector2(0.5f, 0.5f);
            var leaves = random.Range(9, 14);
            for (var k = 0; k < leaves; k++)
            {
                var angle = (k + random.Range(0f, 0.7f)) / leaves * 2f * Mathf.PI;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // The card is about 0.7 of the clump (10–25 cm), so a 3–6 cm leaf is a quarter to nearly half of it.
                Blade(canvas, centre + direction * 0.02f, direction, random.Range(0.24f, 0.44f), random.Range(0.045f, 0.07f), LavenderLeaf * random.Range(0.9f, 1.1f), 0.4f);
            }
            // The bases of the flower stems in the middle.
            for (var k = 0; k < 5; k++) Dot(canvas, centre + new Vector2(random.Range(-0.03f, 0.03f), random.Range(-0.03f, 0.03f)), 0.008f, LavenderStem, 0.2f, 0.7f);
        }

        /// <summary>The fleshy segments of samphire's divided leaves: short, thick, round-ended, in twos and threes.</summary>
        static void Segments(LeafAtlas.Canvas canvas, Vector2[] path, ref StableHash.Sequence random, Color colour, float shortest, float longest, float halfWidth, float smooth)
        {
            var total = 0f;
            for (var i = 1; i < path.Length; i++) total += (path[i] - path[i - 1]).magnitude;
            var step = 0.045f;
            for (var along = 0.08f; along < total - 0.03f; along += step * random.Range(0.8f, 1.2f))
            {
                var (point, direction) = LeafAtlas.At(path, along);
                var count = random.Next() < 0.5f ? 2 : 3;
                for (var k = 0; k < count; k++)
                {
                    var angle = (k - 0.5f * (count - 1)) * random.Range(0.5f, 0.8f) + random.Range(-0.15f, 0.15f);
                    var end = point + LeafAtlas.Rotate(direction, angle) * random.Range(shortest, longest);
                    Capsule(canvas, point, end, halfWidth, colour * random.Range(0.9f, 1.08f), smooth, 0.75f + 0.25f * Mathf.Clamp01(point.y));
                }
            }
        }

        /// <summary>A compound umbel: rays from one point, a small head of flowers at the end of each.</summary>
        static void Umbel(LeafAtlas.Canvas canvas, Vector2 at, Vector2 direction, ref StableHash.Sequence random, Color colour, float shortest, float longest)
        {
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.up;
            var rays = random.Range(8, 15);
            for (var r = 0; r < rays; r++)
            {
                var angle = (r - 0.5f * (rays - 1)) / rays * 1.6f;
                var end = at + LeafAtlas.Rotate(direction, angle) * random.Range(shortest, longest);
                Capsule(canvas, at, end, 0.0018f, SamphireStem, 0.2f, 0.85f);
                for (var f = 0; f < 3; f++) Dot(canvas, end + new Vector2(random.Range(-0.006f, 0.006f), random.Range(-0.006f, 0.006f)), 0.0045f, colour * random.Range(0.92f, 1.06f), 0.2f, 0.95f);
            }
        }

        internal static void Path(LeafAtlas.Canvas canvas, Vector2[] path, float foot, float tip, Color colour, float smooth)
        {
            for (var i = 1; i < path.Length; i++)
            {
                var t = (i - 1f) / (path.Length - 1);
                Capsule(canvas, path[i - 1], path[i], Mathf.Lerp(foot, tip, t), colour, smooth, 0.7f + 0.3f * t);
            }
        }

        /// <summary>A round-ended stroke, rounded across (its normal leans to either side).</summary>
        internal static void Capsule(LeafAtlas.Canvas canvas, Vector2 a, Vector2 b, float halfWidth, Color colour, float smooth, float occlusion)
        {
            var size = canvas.Size;
            a *= size;
            b *= size;
            var half = Mathf.Max(0.6f, halfWidth * size);
            var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - 1f));
            var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - 1f));
            var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + 1f));
            var ab = b - a;
            var lengthSquared = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            var across = new Vector2(-ab.y, ab.x).normalized;
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                    var offset = p - (a + ab * t);
                    var alpha = Mathf.Clamp01(half - offset.magnitude + 0.5f);
                    if (alpha <= 0f) continue;
                    var side = Mathf.Clamp(Vector2.Dot(offset, across) / half, -1f, 1f);
                    canvas.Put(y * size + x, alpha, colour, across * (0.75f * side), smooth, occlusion);
                }
            }
        }

        /// <summary>A small round flower or bud, domed.</summary>
        internal static void Dot(LeafAtlas.Canvas canvas, Vector2 centre, float radius, Color colour, float smooth, float occlusion)
        {
            var size = canvas.Size;
            var c = centre * size;
            var r = Mathf.Max(0.7f, radius * size);
            var minX = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 1f));
            var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + r + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 1f));
            var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + r + 1f));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var offset = new Vector2(x + 0.5f, y + 0.5f) - c;
                    var alpha = Mathf.Clamp01(r - offset.magnitude + 0.5f);
                    if (alpha <= 0f) continue;
                    canvas.Put(y * size + x, alpha, colour, offset / r * 0.7f, smooth, occlusion);
                }
            }
        }

        /// <summary>A narrow leaf widest toward its rounded tip (spatulate), with a pale midrib.</summary>
        internal static void Blade(LeafAtlas.Canvas canvas, Vector2 basePoint, Vector2 direction, float length, float width, Color colour, float smooth)
        {
            var size = canvas.Size;
            var origin = basePoint * size;
            var d = direction.normalized;
            var p = new Vector2(-d.y, d.x);
            var l = length * size;
            var w = width * size;
            var reach = l + w;
            var minX = Mathf.Max(0, Mathf.FloorToInt(origin.x - reach - 1f));
            var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(origin.x + reach + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(origin.y - reach - 1f));
            var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(origin.y + reach + 1f));
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f) - origin;
                    var along = Vector2.Dot(q, d);
                    if (along < 0f || along > l + 1f) continue;
                    var s = along / l;
                    var t = Vector2.Dot(q, p);
                    var shape = Mathf.Sqrt(Mathf.Clamp01(Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(s), 0.6f))));
                    var halfWidth = 0.5f * w * Mathf.Max(0.15f, shape);
                    var alpha = Mathf.Clamp01(halfWidth - Mathf.Abs(t) + 0.5f) * Mathf.Clamp01(l - along + 0.5f);
                    if (alpha <= 0f) continue;
                    var across = halfWidth > 0.01f ? t / halfWidth : 0f;
                    var onRib = Mathf.Abs(t) < Mathf.Max(0.6f, 0.1f * halfWidth);
                    canvas.Put(y * size + x, alpha, onRib ? colour * 1.2f : colour, p * (0.5f * across), smooth, 0.75f + 0.25f * s);
                }
            }
        }
    }
}
