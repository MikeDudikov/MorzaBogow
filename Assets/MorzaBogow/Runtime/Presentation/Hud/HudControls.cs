using UnityEngine;
using UnityEngine.UIElements;

namespace MorzaBogow.Presentation.Hud
{
    /// <summary>Shared drawing helpers: gradient quads and discs, written straight into the panel's mesh.</summary>
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

    /// <summary>
    /// A meter drawn as one shape: a dark groove, the filled share, segment marks and, when asked, diagonal hatching
    /// so the state reads without colour. Redrawn only when its value or colour changes.
    /// </summary>
    public sealed class MeterBar : VisualElement
    {
        float fraction;
        Color fill;
        readonly int segments;
        bool hatched;

        public MeterBar(Color fill, int segments = 0)
        {
            this.fill = fill;
            this.segments = segments;
            pickingMode = PickingMode.Ignore;
            AddToClassList("meter");
            generateVisualContent += Draw;
        }

        public float Value => fraction;

        public void Set(float value, Color color, bool hatch = false)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - fraction) < 0.002f && color == fill && hatch == hatched) return;
            fraction = value;
            fill = color;
            hatched = hatch;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 2f) return;
            HudDraw.Gradient(context, rect, new Color(0.02f, 0.04f, 0.05f, 0.9f), new Color(0.05f, 0.08f, 0.09f, 0.9f));
            if (fraction > 0f)
            {
                var filled = new Rect(rect.xMin + 1f, rect.yMin + 1f, (rect.width - 2f) * fraction, rect.height - 2f);
                HudDraw.Gradient(context, filled, Color.Lerp(fill, Color.white, 0.2f), Color.Lerp(fill, Color.black, 0.15f));
                if (hatched)
                {
                    var painter = context.painter2D;
                    painter.strokeColor = new Color(0f, 0f, 0f, 0.45f);
                    painter.lineWidth = 1.5f;
                    for (var x = filled.xMin - filled.height; x < filled.xMax; x += 5f)
                    {
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(Mathf.Max(filled.xMin, x), filled.yMax - Mathf.Max(0f, filled.xMin - x)));
                        painter.LineTo(new Vector2(Mathf.Min(filled.xMax, x + filled.height), filled.yMin + Mathf.Max(0f, x + filled.height - filled.xMax)));
                        painter.Stroke();
                    }
                }
            }
            if (segments <= 1) return;
            var marks = context.painter2D;
            marks.strokeColor = new Color(0f, 0f, 0f, 0.5f);
            marks.lineWidth = 1f;
            for (var i = 1; i < segments; i++)
            {
                var x = rect.xMin + rect.width * i / segments;
                marks.BeginPath();
                marks.MoveTo(new Vector2(x, rect.yMin + 1f));
                marks.LineTo(new Vector2(x, rect.yMax - 1f));
                marks.Stroke();
            }
        }
    }
}
