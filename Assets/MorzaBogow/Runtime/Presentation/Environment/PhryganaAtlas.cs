using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Bakes the phrygana's texture (Phrygana) in the trees' four quarters: thorny burnet, thyme and rockrose from the
    /// side, and in the fourth quarter the three seen from above, one to a corner. Colour with coverage in alpha, a
    /// tangent-space normal map and an HDRP mask map, drawn with LeafAtlas's canvas and CliffPlantAtlas's strokes.
    /// Drawn in the Aegean summer of the game's light, from the floras read (report 9.1–9.18):
    /// <list type="bullet">
    /// <item>thorny burnet (Sarcopoterium spinosum): a dense, much-branched, rounded tangle of spine-tipped twigs, its
    /// few leaves small; in summer dry and grey-brown, as if dead;</item>
    /// <item>thyme (Thymbra capitata): a small ball of upright twigs with tufts of narrow leaves, in flower in early
    /// summer with oblong heads of purple-pink flowers among greenish bracts;</item>
    /// <item>rockrose (Cistus creticus, C. salviifolius): a rounded bush of wavy, grey-green, hairy leaves 1.5–2.5 cm,
    /// past its spring flowers.</item>
    /// </list>
    /// Deterministic: the same size gives the same pixels.
    /// </summary>
    public static class PhryganaAtlas
    {
        static readonly Color TwigDark = new Color(0.42f, 0.37f, 0.31f);
        static readonly Color TwigPale = new Color(0.60f, 0.56f, 0.48f);
        static readonly Color Spine = new Color(0.70f, 0.66f, 0.56f);
        static readonly Color BurnetLeaf = new Color(0.42f, 0.45f, 0.33f);
        static readonly Color ThymeStem = new Color(0.50f, 0.45f, 0.38f);
        static readonly Color ThymeLeaf = new Color(0.45f, 0.51f, 0.38f);
        static readonly Color ThymeBract = new Color(0.55f, 0.60f, 0.42f);
        static readonly Color ThymeFlower = new Color(0.74f, 0.44f, 0.65f);
        static readonly Color RockroseStem = new Color(0.45f, 0.38f, 0.32f);
        static readonly Color RockroseLeafDark = new Color(0.47f, 0.53f, 0.42f);
        static readonly Color RockroseLeafPale = new Color(0.56f, 0.60f, 0.50f);

        /// <summary>The corner of each kind's top in the fourth quarter, and the side of that square (texture coordinates).</summary>
        public static Vector2 TopCorner(Phrygana.Kind kind) =>
            TreeGenerator.AtlasCells[Phrygana.CushionTop] + (kind == Phrygana.Kind.ThornyBurnet ? Vector2.zero : kind == Phrygana.Kind.Thyme ? new Vector2(0.25f, 0f) : new Vector2(0f, 0.25f));

        public const float TopExtent = 0.25f;

        /// <summary>A square of a canvas to draw in: canvas = origin + local · scale.</summary>
        readonly struct Frame
        {
            readonly Vector2 origin;
            readonly float scale;

            public Frame(Vector2 origin, float scale)
            {
                this.origin = origin;
                this.scale = scale;
            }

            public Vector2 At(Vector2 local) => origin + local * scale;

            public float Size(float local) => local * scale;
        }

        public static LeafAtlas.Result Bake(int size = 1024)
        {
            size = Mathf.Max(64, size - size % 2);
            var half = size / 2;
            var canvases = new[] { new LeafAtlas.Canvas(half), new LeafAtlas.Canvas(half), new LeafAtlas.Canvas(half), new LeafAtlas.Canvas(half) };
            var whole = new Frame(Vector2.zero, 1f);
            var burnet = new StableHash.Sequence(0xF4A0u);
            var thyme = new StableHash.Sequence(0xF4A1u);
            var rockrose = new StableHash.Sequence(0xF4A2u);
            var tops = new StableHash.Sequence(0xF4A3u);
            Burnet(canvases[Phrygana.BurnetSide], whole, ref burnet, false);
            Thyme(canvases[Phrygana.ThymeSide], whole, ref thyme, false);
            Rockrose(canvases[Phrygana.RockroseSide], whole, ref rockrose, false);
            var top = canvases[Phrygana.CushionTop];
            Burnet(top, new Frame(Vector2.zero, 0.5f), ref tops, true);
            Thyme(top, new Frame(new Vector2(0.5f, 0f), 0.5f), ref tops, true);
            Rockrose(top, new Frame(new Vector2(0f, 0.5f), 0.5f), ref tops, true);

            var result = new LeafAtlas.Result { Size = size, Colour = new Color32[size * size], Normal = new Color32[size * size], Mask = new Color32[size * size] };
            for (var cell = 0; cell < 4; cell++)
            {
                LeafAtlas.Dilate(canvases[cell]);
                var corner = TreeGenerator.AtlasCells[cell];
                LeafAtlas.Blit(canvases[cell], result, (int)(corner.x * size), (int)(corner.y * size));
            }
            return result;
        }

        /// <summary>A point inside the cushion's outline: a dome on the ground seen from the side, a disc seen from above.</summary>
        static Vector2 Inside(ref StableHash.Sequence random, bool fromAbove, float outer = 1f)
        {
            for (var tries = 0; tries < 16; tries++)
            {
                var p = new Vector2(random.Range(0.02f, 0.98f), random.Range(0.02f, 0.98f));
                if (fromAbove)
                {
                    if ((p - new Vector2(0.5f, 0.5f)).magnitude <= 0.42f * outer) return p;
                }
                else
                {
                    var x = (p.x - 0.5f) / 0.42f;
                    var y = p.y / 0.86f;
                    if (x * x + y * y <= outer * outer) return p;
                }
            }
            return fromAbove ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.3f);
        }

        /// <summary>How deep in the cushion a point lies (0 at its middle or foot, 1 at its skin): light falls on the skin.</summary>
        static float Skin(Vector2 p, bool fromAbove)
        {
            if (fromAbove) return Mathf.Clamp01((p - new Vector2(0.5f, 0.5f)).magnitude / 0.42f * 0.6f + 0.4f);
            var x = (p.x - 0.5f) / 0.42f;
            var y = p.y / 0.86f;
            return Mathf.Clamp01(Mathf.Sqrt(x * x + y * y) * 0.6f + 0.4f * y);
        }

        /// <summary>Thorny burnet: a tangle of short zig-zag twigs, each ending in a pale spine, a few small leaves.</summary>
        static void Burnet(LeafAtlas.Canvas canvas, Frame frame, ref StableHash.Sequence random, bool fromAbove)
        {
            for (var i = 0; i < 340; i++)
            {
                var p = Inside(ref random, fromAbove);
                var angle = random.Range(0f, 2f * Mathf.PI);
                var length = random.Range(0.03f, 0.07f);
                var first = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var second = LeafAtlas.Rotate(first, random.Range(-0.7f, 0.7f));
                var bend = p + first * (0.5f * length);
                var end = bend + second * (0.5f * length);
                var colour = Color.Lerp(TwigDark, TwigPale, random.Next());
                var light = 0.55f + 0.45f * Skin(p, fromAbove);
                CliffPlantAtlas.Capsule(canvas, frame.At(p), frame.At(bend), frame.Size(0.0045f), colour, 0.18f, light);
                CliffPlantAtlas.Capsule(canvas, frame.At(bend), frame.At(end), frame.Size(0.004f), colour, 0.18f, light);
                CliffPlantAtlas.Dot(canvas, frame.At(end), frame.Size(0.004f), Spine, 0.2f, light);
            }
            for (var i = 0; i < 70; i++)
            {
                var p = Inside(ref random, fromAbove);
                CliffPlantAtlas.Dot(canvas, frame.At(p), frame.Size(0.007f), BurnetLeaf * random.Range(0.9f, 1.1f), 0.2f, 0.6f + 0.4f * Skin(p, fromAbove));
            }
        }

        /// <summary>Thyme: twigs fanning up from the foot (or out from the middle), tufts of narrow leaves, flower heads at the tips.</summary>
        static void Thyme(LeafAtlas.Canvas canvas, Frame frame, ref StableHash.Sequence random, bool fromAbove)
        {
            var stems = random.Range(22, 30);
            for (var s = 0; s < stems; s++)
            {
                Vector2 foot, tip;
                if (fromAbove)
                {
                    var angle = (s + random.Range(0f, 0.8f)) / stems * 2f * Mathf.PI;
                    foot = new Vector2(0.5f, 0.5f);
                    tip = foot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * random.Range(0.25f, 0.44f);
                }
                else
                {
                    var lean = (s - 0.5f * (stems - 1)) / stems * 2.4f + random.Range(-0.15f, 0.15f);
                    foot = new Vector2(0.5f + random.Range(-0.05f, 0.05f), 0.02f);
                    tip = foot + new Vector2(Mathf.Sin(lean), Mathf.Cos(lean)) * random.Range(0.55f, 0.9f);
                    tip.x = Mathf.Clamp(tip.x, 0.05f, 0.95f);
                    tip.y = Mathf.Clamp(tip.y, 0.08f, 0.93f);
                }
                var path = LeafAtlas.Curve(foot, Vector2.Lerp(foot, tip, 0.5f) + new Vector2(random.Range(-0.04f, 0.04f), random.Range(-0.02f, 0.02f)), tip, 12);
                for (var i = 1; i < path.Length; i++)
                    CliffPlantAtlas.Capsule(canvas, frame.At(path[i - 1]), frame.At(path[i]), frame.Size(Mathf.Lerp(0.005f, 0.003f, i / (path.Length - 1f))), ThymeStem, 0.2f, 0.7f);
                // Tufts of narrow leaves along the twig, sparse in the dry season.
                for (var along = 0.15f; along < 0.95f; along += random.Range(0.08f, 0.14f))
                {
                    var at = path[Mathf.Clamp(Mathf.RoundToInt(along * (path.Length - 1)), 0, path.Length - 1)];
                    for (var k = 0; k < 3; k++)
                    {
                        var direction = new Vector2(random.Range(-1f, 1f), random.Range(-0.3f, 1f)).normalized;
                        CliffPlantAtlas.Capsule(canvas, frame.At(at), frame.At(at + direction * random.Range(0.015f, 0.025f)), frame.Size(0.004f), ThymeLeaf * random.Range(0.9f, 1.1f), 0.25f, 0.6f + 0.4f * along);
                    }
                }
                if (random.Next() > 0.7f) continue;
                // An oblong head: greenish bracts with purple-pink flowers among them.
                var heading = (tip - path[path.Length - 2]).normalized;
                var across = LeafAtlas.Rotate(heading, Mathf.PI * 0.5f);
                for (var f = 0; f < 9; f++)
                {
                    var at = tip + heading * (0.004f * f) + across * random.Range(-0.008f, 0.008f);
                    var flower = f % 3 != 0;
                    CliffPlantAtlas.Dot(canvas, frame.At(at), frame.Size(flower ? 0.0065f : 0.006f), flower ? ThymeFlower * random.Range(0.92f, 1.06f) : ThymeBract, 0.2f, 0.95f);
                }
            }
        }

        /// <summary>Rockrose: a few brown stems under a rounded mass of wavy, grey-green, hairy leaves.</summary>
        static void Rockrose(LeafAtlas.Canvas canvas, Frame frame, ref StableHash.Sequence random, bool fromAbove)
        {
            if (!fromAbove)
            {
                var stems = random.Range(10, 15);
                for (var s = 0; s < stems; s++)
                {
                    var lean = (s - 0.5f * (stems - 1)) / stems * 1.3f + random.Range(-0.1f, 0.1f);
                    var foot = new Vector2(0.5f + random.Range(-0.06f, 0.06f), 0.02f);
                    var tip = foot + new Vector2(Mathf.Sin(lean), Mathf.Cos(lean)) * random.Range(0.35f, 0.6f);
                    var path = LeafAtlas.Curve(foot, Vector2.Lerp(foot, tip, 0.5f), tip, 8);
                    for (var i = 1; i < path.Length; i++)
                        CliffPlantAtlas.Capsule(canvas, frame.At(path[i - 1]), frame.At(path[i]), frame.Size(Mathf.Lerp(0.007f, 0.004f, i / (path.Length - 1f))), RockroseStem, 0.15f, 0.6f);
                }
            }
            // Inner leaves first, the sunlit skin over them.
            for (var layer = 0; layer < 2; layer++)
            {
                for (var i = 0; i < 190; i++)
                {
                    var p = Inside(ref random, fromAbove, layer == 0 ? 0.75f : 1f);
                    var outward = fromAbove ? p - new Vector2(0.5f, 0.5f) : new Vector2(p.x - 0.5f, p.y);
                    var direction = LeafAtlas.Rotate(outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector2.up, random.Range(-0.9f, 0.9f));
                    var colour = Color.Lerp(RockroseLeafDark, RockroseLeafPale, random.Next()) * (layer == 0 ? 0.85f : 1f);
                    CliffPlantAtlas.Blade(canvas, frame.At(p), direction, frame.Size(random.Range(0.04f, 0.07f)), frame.Size(random.Range(0.025f, 0.04f)), colour, 0.18f);
                }
            }
        }
    }
}
