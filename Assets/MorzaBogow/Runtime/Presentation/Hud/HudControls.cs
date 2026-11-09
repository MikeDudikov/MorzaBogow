using UnityEngine;
using UnityEngine.UIElements;

namespace MorzaBogow.Presentation.Hud
{
    /// <summary>Shared drawing helpers: gradient quads and soft glows, written straight into the panel's mesh.</summary>
    public static class HudDraw
    {
        public static void Gradient(MeshGenerationContext context, Rect rect, Color top, Color bottom)
        {
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = bottom });
            mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = bottom });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
        }

        public static void Disc(Painter2D painter, Vector2 centre, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
            painter.Fill();
        }

        public static void Ring(Painter2D painter, Vector2 centre, float radius, float width, Color color, float from = 0f, float to = 360f)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.Arc(centre, radius, Angle.Degrees(from), Angle.Degrees(to));
            painter.Stroke();
        }
    }

    /// <summary>Procedural surface textures, made once: fired-clay grain for panels and veined marble for cards.</summary>
    public static class HudTextures
    {
        static Texture2D grain, marble;

        public static Texture2D Grain => grain != null ? grain : grain = Make(128, (x, y) =>
        {
            var n = Mathf.PerlinNoise(x * 0.21f, y * 0.21f) * 0.6f + Mathf.PerlinNoise(x * 0.9f + 31f, y * 0.9f + 7f) * 0.4f;
            var speck = Hash(x, y) > 0.985f ? 0.35f : 0f;
            var dark = Mathf.Clamp01((n - 0.45f) * 0.5f) + speck;
            var lightSpot = Mathf.Clamp01((0.4f - n) * 0.35f);
            return lightSpot > dark ? new Color(1f, 0.92f, 0.8f, lightSpot) : new Color(0.08f, 0.03f, 0.01f, dark);
        });

        public static Texture2D Marble => marble != null ? marble : marble = Make(256, (x, y) =>
        {
            var turbulence = 0f;
            var scale = 1f;
            for (var octave = 0; octave < 4; octave++)
            {
                turbulence += Mathf.Abs(Mathf.PerlinNoise(x * 0.02f * scale + 13f, y * 0.02f * scale + 5f) - 0.5f) / scale;
                scale *= 2f;
            }
            var vein = Mathf.Pow(Mathf.Abs(Mathf.Sin((x * 0.03f + y * 0.012f + turbulence * 6f))), 0.25f);
            var shade = Mathf.Lerp(0.12f, 0f, vein);
            return new Color(0.25f, 0.18f, 0.1f, shade);
        });

        static float Hash(int x, int y)
        {
            var h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }

        static Texture2D Make(int size, System.Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "HUD surface" };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++) pixels[y * size + x] = pixel(x, y);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }

    /// <summary>A vector icon filling its box.</summary>
    public sealed class IconElement : VisualElement
    {
        HudIcon icon;
        Color ink = HudPalette.Glaze;

        public IconElement(HudIcon icon, Color ink)
        {
            this.icon = icon;
            this.ink = ink;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += context => HudIcons.Draw(context.painter2D, this.icon, contentRect, this.ink);
        }

        public void Set(HudIcon value, Color color)
        {
            if (value == icon && color == ink) return;
            icon = value;
            ink = color;
            MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// A coin-like medallion: a bronze rim, a clay or parchment field and a black-figure icon. Optional: a ring in
    /// the owner's colour, a progress ring with marks for the stages of a hull on the slipway, and a dimmed state.
    /// </summary>
    public sealed class Medallion : VisualElement
    {
        HudIcon icon;
        Color field = HudPalette.Parchment, rim = HudPalette.Bronze, ink = HudPalette.Glaze;
        Color? ownerRing;
        float progress = -1f;
        bool dimmed, hovered, active;

        public static readonly float[] StageMarks = { 0.1f, 0.35f, 0.7f, 0.85f, 0.95f };

        public Medallion(HudIcon icon)
        {
            this.icon = icon;
            AddToClassList("medallion");
            generateVisualContent += Draw;
            RegisterCallback<PointerEnterEvent>(_ => { hovered = true; MarkDirtyRepaint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; MarkDirtyRepaint(); });
        }

        public void Set(HudIcon value, Color fieldColor, Color? owner = null, float progressValue = -1f, bool isDimmed = false, bool isActive = false)
        {
            if (value == icon && fieldColor == field && owner == ownerRing && Mathf.Approximately(progressValue, progress) && isDimmed == dimmed && isActive == active) return;
            icon = value;
            field = fieldColor;
            ownerRing = owner;
            progress = progressValue;
            dimmed = isDimmed;
            active = isActive;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius < 2f) return;
            var centre = rect.center;
            var painter = context.painter2D;
            var ringWidth = Mathf.Max(2f, radius * 0.12f);

            // Glow when the pointer is over it or it is the chosen one.
            if (hovered || active) HudDraw.Disc(painter, centre, radius, HudPalette.WithAlpha(HudPalette.OchreLight, hovered ? 0.35f : 0.22f));
            HudDraw.Disc(painter, centre, radius - 1f, HudPalette.Glaze);
            HudDraw.Disc(painter, centre, radius - 2f, dimmed ? HudPalette.SilverDark : rim);
            HudDraw.Ring(painter, centre, radius - 2f - ringWidth * 0.5f, 1.2f, HudPalette.WithAlpha(HudPalette.OchreLight, dimmed ? 0.2f : 0.7f), 200f, 340f);
            HudDraw.Disc(painter, centre, radius - 2f - ringWidth, dimmed ? Color.Lerp(field, HudPalette.SilverDark, 0.6f) : field);
            if (ownerRing.HasValue) HudDraw.Ring(painter, centre, radius - 2f - ringWidth * 0.5f, ringWidth * 0.75f, ownerRing.Value);

            if (progress >= 0f)
            {
                HudDraw.Ring(painter, centre, radius - 2f - ringWidth * 0.5f, ringWidth * 0.9f, HudPalette.WithAlpha(HudPalette.Glaze, 0.65f));
                if (progress > 0f) HudDraw.Ring(painter, centre, radius - 2f - ringWidth * 0.5f, ringWidth * 0.9f, HudPalette.OchreLight, -90f, -90f + 360f * Mathf.Clamp01(progress));
                foreach (var mark in StageMarks)
                {
                    var angle = (-90f + 360f * mark) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    painter.strokeColor = HudPalette.Glaze;
                    painter.lineWidth = 1.5f;
                    painter.BeginPath();
                    painter.MoveTo(centre + direction * (radius - 2f - ringWidth * 1.05f));
                    painter.LineTo(centre + direction * (radius - 1.5f));
                    painter.Stroke();
                }
            }

            var inner = radius - 2f - ringWidth * 1.6f;
            var iconRect = new Rect(centre.x - inner * 0.82f, centre.y - inner * 0.82f, inner * 1.64f, inner * 1.64f);
            HudIcons.Draw(painter, icon, iconRect, dimmed ? HudPalette.WithAlpha(HudPalette.Glaze, 0.45f) : ink);
        }
    }

    public enum OrnamentKind { Meander, Wave, EggAndDart }

    /// <summary>A painted band of Greek ornament repeated along its width.</summary>
    public sealed class OrnamentBand : VisualElement
    {
        readonly OrnamentKind kind;
        readonly Color color, ground;

        public OrnamentBand(OrnamentKind kind, Color color, Color ground)
        {
            this.kind = kind;
            this.color = color;
            this.ground = ground;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 4f || rect.height < 3f) return;
            if (ground.a > 0f) HudDraw.Gradient(context, rect, ground, ground);
            var painter = context.painter2D;
            var h = rect.height;
            painter.strokeColor = color;
            painter.fillColor = color;
            painter.lineJoin = LineJoin.Miter;
            painter.lineCap = LineCap.Butt;

            switch (kind)
            {
                case OrnamentKind.Meander:
                {
                    // A running key: each unit is a square spiral hooked to the next along a base line.
                    var unit = h * 1.25f;
                    var line = Mathf.Max(1f, h * 0.13f);
                    painter.lineWidth = line;
                    var top = rect.yMin + line * 0.5f;
                    var bottom = rect.yMax - line * 0.5f;
                    var span = bottom - top;
                    for (var x = rect.xMin; x < rect.xMax; x += unit)
                    {
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(x, bottom));
                        painter.LineTo(new Vector2(x, top));
                        painter.LineTo(new Vector2(x + unit * 0.8f, top));
                        painter.LineTo(new Vector2(x + unit * 0.8f, top + span * 0.72f));
                        painter.LineTo(new Vector2(x + unit * 0.28f, top + span * 0.72f));
                        painter.LineTo(new Vector2(x + unit * 0.28f, top + span * 0.36f));
                        painter.LineTo(new Vector2(x + unit * 0.54f, top + span * 0.36f));
                        painter.Stroke();
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(x, bottom));
                        painter.LineTo(new Vector2(Mathf.Min(rect.xMax, x + unit), bottom));
                        painter.Stroke();
                    }
                    break;
                }
                case OrnamentKind.Wave:
                {
                    // Running waves: a breaking crest rolled into a spiral, unit after unit.
                    var unit = h * 1.6f;
                    painter.lineWidth = Mathf.Max(1f, h * 0.12f);
                    for (var x = rect.xMin; x < rect.xMax; x += unit)
                    {
                        var baseY = rect.yMax - h * 0.15f;
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(x, baseY));
                        painter.QuadraticCurveTo(new Vector2(x + unit * 0.45f, baseY), new Vector2(x + unit * 0.55f, rect.yMin + h * 0.35f));
                        painter.QuadraticCurveTo(new Vector2(x + unit * 0.62f, rect.yMin), new Vector2(x + unit * 0.78f, rect.yMin + h * 0.3f));
                        painter.QuadraticCurveTo(new Vector2(x + unit * 0.84f, rect.yMin + h * 0.55f), new Vector2(x + unit * 0.66f, rect.yMin + h * 0.5f));
                        painter.Stroke();
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(x + unit * 0.6f, baseY));
                        painter.LineTo(new Vector2(Mathf.Min(rect.xMax, x + unit), baseY));
                        painter.Stroke();
                    }
                    break;
                }
                case OrnamentKind.EggAndDart:
                {
                    var unit = h * 1.3f;
                    painter.lineWidth = Mathf.Max(1f, h * 0.1f);
                    for (var x = rect.xMin; x + unit * 0.5f < rect.xMax; x += unit)
                    {
                        var centre = new Vector2(x + unit * 0.35f, rect.center.y);
                        painter.BeginPath();
                        painter.Arc(centre, h * 0.36f, Angle.Degrees(0f), Angle.Degrees(360f));
                        painter.Stroke();
                        HudDraw.Disc(painter, centre, h * 0.18f, color);
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(x + unit * 0.82f, rect.yMin + h * 0.1f));
                        painter.LineTo(new Vector2(x + unit * 0.9f, rect.yMin + h * 0.5f));
                        painter.LineTo(new Vector2(x + unit * 0.82f, rect.yMax - h * 0.05f));
                        painter.LineTo(new Vector2(x + unit * 0.74f, rect.yMin + h * 0.5f));
                        painter.ClosePath();
                        painter.Fill();
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The frame of a panel: a soft shadow, a bronze double rule with an inner light line, and palmettes in the
    /// corners. Drawn over the panel's own fill, ignoring the pointer.
    /// </summary>
    public sealed class PanelFrame : VisualElement
    {
        readonly Color accent;

        public PanelFrame(Color accent)
        {
            this.accent = accent;
            pickingMode = PickingMode.Ignore;
            AddToClassList("panel-frame");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 20f || rect.height < 20f) return;
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Miter;

            // A darker vignette at the bottom of the panel gives it depth.
            HudDraw.Gradient(context, new Rect(rect.xMin, rect.yMax - rect.height * 0.35f, rect.width, rect.height * 0.35f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.22f));
            HudDraw.Gradient(context, new Rect(rect.xMin, rect.yMin, rect.width, Mathf.Min(18f, rect.height * 0.2f)), new Color(1f, 0.9f, 0.7f, 0.12f), new Color(1f, 0.9f, 0.7f, 0f));

            Frame(painter, rect, 1.5f, HudPalette.Glaze, 3f);
            Frame(painter, rect, 4f, HudPalette.Bronze, 2f);
            Frame(painter, rect, 6.5f, HudPalette.WithAlpha(accent, 0.75f), 1f);

            foreach (var corner in new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax) })
            {
                var inward = new Vector2(corner.x < rect.center.x ? 1f : -1f, corner.y < rect.center.y ? 1f : -1f);
                Palmette(painter, corner + inward * 4f, inward, 15f);
            }
        }

        static void Frame(Painter2D painter, Rect rect, float inset, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + inset, rect.yMin + inset));
            painter.LineTo(new Vector2(rect.xMax - inset, rect.yMin + inset));
            painter.LineTo(new Vector2(rect.xMax - inset, rect.yMax - inset));
            painter.LineTo(new Vector2(rect.xMin + inset, rect.yMax - inset));
            painter.ClosePath();
            painter.Stroke();
        }

        /// <summary>A fan of five petals springing from the corner.</summary>
        static void Palmette(Painter2D painter, Vector2 origin, Vector2 inward, float size)
        {
            var baseAngle = Mathf.Atan2(inward.y, inward.x);
            for (var i = -2; i <= 2; i++)
            {
                var angle = baseAngle + i * 0.32f;
                var tip = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * (i == 0 ? 1f : 0.82f);
                var side = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * size * 0.13f;
                var middle = origin + (tip - origin) * 0.55f;
                painter.fillColor = i == 0 ? HudPalette.OchreLight : HudPalette.Ochre;
                painter.BeginPath();
                painter.MoveTo(origin);
                painter.QuadraticCurveTo(middle + side, tip);
                painter.QuadraticCurveTo(middle - side, origin);
                painter.ClosePath();
                painter.Fill();
            }
            HudDraw.Disc(painter, origin, size * 0.16f, HudPalette.Glaze);
        }
    }

    /// <summary>A meter drawn as one shape: a dark groove, the filled share and fine segment marks.</summary>
    public sealed class MeterBar : VisualElement
    {
        float fraction;
        Color fill;
        readonly int segments;

        public MeterBar(Color fill, int segments = 0)
        {
            this.fill = fill;
            this.segments = segments;
            pickingMode = PickingMode.Ignore;
            AddToClassList("meter");
            generateVisualContent += Draw;
        }

        public void Set(float value, Color color)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - fraction) < 0.002f && color == fill) return;
            fraction = value;
            fill = color;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 2f) return;
            HudDraw.Gradient(context, rect, new Color(0.04f, 0.02f, 0.01f, 0.85f), new Color(0.12f, 0.06f, 0.03f, 0.85f));
            if (fraction > 0f)
            {
                var filled = new Rect(rect.xMin + 1f, rect.yMin + 1f, (rect.width - 2f) * fraction, rect.height - 2f);
                HudDraw.Gradient(context, filled, Color.Lerp(fill, Color.white, 0.25f), Color.Lerp(fill, Color.black, 0.2f));
            }
            if (segments <= 1) return;
            var painter = context.painter2D;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.45f);
            painter.lineWidth = 1f;
            for (var i = 1; i < segments; i++)
            {
                var x = rect.xMin + rect.width * i / segments;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.yMin + 1f));
                painter.LineTo(new Vector2(x, rect.yMax - 1f));
                painter.Stroke();
            }
        }
    }
}
