using UnityEngine;
using UnityEngine.UIElements;

namespace MorzaBogow.Presentation.Hud
{
    public enum HudIcon
    {
        None,
        // Resources and state
        Wood, Silver, Transit, Fleet, Port, Hull, Time, Pause, Play, Faster, Slower, Save, Load, Settings,
        Sailor, Soldier, Morale, Cargo, Berth, Alarm, Eye,
        // Ships and places
        Cutter, Ram, CrewDestroyer, Boarder, Merchant, Shipyard, Outpost, Settlement, Wreck,
        // Orders
        Move, Stop, Attack, RamOrder, Board, Volley, Harvest, Deliver, FoundOutpost, Escort, Repair, Scrap, Cancel,
        // Lines
        WarLine, PortLine,
    }

    /// <summary>
    /// Black-figure silhouettes drawn as vectors, so they stay sharp at any interface scale. Each icon is laid out
    /// on a unit square (y downward) and mapped into the given rectangle. All shapes are original.
    /// </summary>
    public static class HudIcons
    {
        sealed class Pen
        {
            public Painter2D P;
            public Rect R;
            public Vector2 V(float x, float y) => new Vector2(R.x + x * R.width, R.y + y * R.height);
            public float S(float size) => size * Mathf.Min(R.width, R.height);

            public void Poly(Color color, params float[] xy)
            {
                P.fillColor = color;
                P.BeginPath();
                P.MoveTo(V(xy[0], xy[1]));
                for (var i = 2; i < xy.Length; i += 2) P.LineTo(V(xy[i], xy[i + 1]));
                P.ClosePath();
                P.Fill();
            }

            public void Line(Color color, float width, params float[] xy)
            {
                P.strokeColor = color;
                P.lineWidth = S(width);
                P.lineCap = LineCap.Round;
                P.lineJoin = LineJoin.Round;
                P.BeginPath();
                P.MoveTo(V(xy[0], xy[1]));
                for (var i = 2; i < xy.Length; i += 2) P.LineTo(V(xy[i], xy[i + 1]));
                P.Stroke();
            }

            public void Circle(Color color, float x, float y, float radius)
            {
                P.fillColor = color;
                P.BeginPath();
                P.Arc(V(x, y), S(radius), Angle.Degrees(0f), Angle.Degrees(360f));
                P.ClosePath();
                P.Fill();
            }

            public void Ring(Color color, float x, float y, float radius, float width, float from = 0f, float to = 360f)
            {
                P.strokeColor = color;
                P.lineWidth = S(width);
                P.lineCap = LineCap.Round;
                P.BeginPath();
                P.Arc(V(x, y), S(radius), Angle.Degrees(from), Angle.Degrees(to));
                P.Stroke();
            }

            public void Curve(Color color, float width, float x0, float y0, float cx, float cy, float x1, float y1)
            {
                P.strokeColor = color;
                P.lineWidth = S(width);
                P.lineCap = LineCap.Round;
                P.BeginPath();
                P.MoveTo(V(x0, y0));
                P.QuadraticCurveTo(V(cx, cy), V(x1, y1));
                P.Stroke();
            }

            /// <summary>A galley hull seen from the side: a long low body with a raised stern post.</summary>
            public void Hull(Color color, float left, float right, float deck, float keel, float sternRise)
            {
                P.fillColor = color;
                P.BeginPath();
                P.MoveTo(V(left, deck - sternRise));
                P.QuadraticCurveTo(V(left + 0.06f, deck), V(left + 0.14f, deck));
                P.LineTo(V(right - 0.04f, deck));
                P.LineTo(V(right, deck + 0.02f));
                P.QuadraticCurveTo(V(right - 0.08f, keel), V(right - 0.2f, keel));
                P.LineTo(V(left + 0.2f, keel));
                P.QuadraticCurveTo(V(left + 0.05f, keel - 0.02f), V(left, deck - sternRise));
                P.ClosePath();
                P.Fill();
            }

            public void Waves(Color color, float y, float width)
            {
                for (var i = 0; i < 3; i++)
                {
                    var x = 0.12f + i * 0.27f;
                    Curve(color, width, x, y, x + 0.07f, y - 0.06f, x + 0.14f, y);
                    Curve(color, width, x + 0.14f, y, x + 0.2f, y + 0.05f, x + 0.27f, y);
                }
            }
        }

        static readonly Pen pen = new Pen();

        public static void Draw(Painter2D painter, HudIcon icon, Rect rect, Color ink)
        {
            if (icon == HudIcon.None) return;
            pen.P = painter;
            pen.R = rect;
            var p = pen;
            var light = HudPalette.WithAlpha(ink, 0.55f);
            switch (icon)
            {
                case HudIcon.Wood:
                    // Two stacked logs with an axe bitten into the top one.
                    p.Line(ink, 0.17f, 0.18f, 0.72f, 0.72f, 0.72f);
                    p.Line(ink, 0.17f, 0.26f, 0.5f, 0.8f, 0.5f);
                    p.Circle(light, 0.18f, 0.72f, 0.05f);
                    p.Circle(light, 0.26f, 0.5f, 0.05f);
                    p.Line(ink, 0.06f, 0.62f, 0.12f, 0.5f, 0.42f);
                    p.Poly(ink, 0.56f, 0.06f, 0.82f, 0.14f, 0.76f, 0.3f, 0.6f, 0.22f);
                    break;

                case HudIcon.Silver:
                    // A cast bar and a stamped coin.
                    p.Poly(ink, 0.08f, 0.72f, 0.22f, 0.5f, 0.68f, 0.5f, 0.6f, 0.72f);
                    p.Poly(light, 0.22f, 0.5f, 0.32f, 0.38f, 0.76f, 0.38f, 0.68f, 0.5f);
                    p.Circle(ink, 0.74f, 0.3f, 0.2f);
                    p.Ring(light, 0.74f, 0.3f, 0.13f, 0.035f);
                    p.Circle(light, 0.74f, 0.3f, 0.05f);
                    break;

                case HudIcon.Transit:
                case HudIcon.Cargo:
                case HudIcon.Deliver:
                    // An amphora: narrow neck, two handles, round belly, pointed foot.
                    p.P.fillColor = ink;
                    p.P.BeginPath();
                    p.P.MoveTo(p.V(0.42f, 0.08f));
                    p.P.LineTo(p.V(0.58f, 0.08f));
                    p.P.LineTo(p.V(0.56f, 0.24f));
                    p.P.QuadraticCurveTo(p.V(0.8f, 0.34f), p.V(0.74f, 0.58f));
                    p.P.QuadraticCurveTo(p.V(0.66f, 0.82f), p.V(0.5f, 0.94f));
                    p.P.QuadraticCurveTo(p.V(0.34f, 0.82f), p.V(0.26f, 0.58f));
                    p.P.QuadraticCurveTo(p.V(0.2f, 0.34f), p.V(0.44f, 0.24f));
                    p.P.ClosePath();
                    p.P.Fill();
                    p.Curve(ink, 0.06f, 0.44f, 0.14f, 0.24f, 0.16f, 0.32f, 0.34f);
                    p.Curve(ink, 0.06f, 0.56f, 0.14f, 0.76f, 0.16f, 0.68f, 0.34f);
                    p.Line(light, 0.04f, 0.34f, 0.5f, 0.66f, 0.5f);
                    if (icon == HudIcon.Transit) p.Waves(ink, 0.98f, 0.05f);
                    if (icon == HudIcon.Deliver) p.Poly(HudPalette.WithAlpha(ink, 0.9f), 0.78f, 0.66f, 0.98f, 0.8f, 0.78f, 0.94f);
                    break;

                case HudIcon.Fleet:
                    // Three prows side by side.
                    for (var i = 0; i < 3; i++)
                    {
                        var x = 0.08f + i * 0.28f;
                        p.Poly(i == 1 ? ink : light, x, 0.82f, x + 0.24f, 0.82f, x + 0.3f, 0.3f, x + 0.18f, 0.4f, x + 0.04f, 0.58f);
                    }
                    break;

                case HudIcon.Port:
                case HudIcon.Berth:
                    // A pier on posts with a bollard.
                    p.Poly(ink, 0.08f, 0.44f, 0.92f, 0.44f, 0.92f, 0.54f, 0.08f, 0.54f);
                    for (var i = 0; i < 4; i++) p.Line(ink, 0.07f, 0.16f + i * 0.22f, 0.54f, 0.16f + i * 0.22f, 0.84f);
                    p.Poly(ink, 0.7f, 0.44f, 0.7f, 0.28f, 0.8f, 0.28f, 0.8f, 0.44f);
                    p.Waves(light, 0.74f, 0.05f);
                    break;

                case HudIcon.Hull:
                    p.Hull(ink, 0.06f, 0.94f, 0.42f, 0.74f, 0.22f);
                    p.Line(light, 0.04f, 0.2f, 0.5f, 0.85f, 0.5f);
                    p.Waves(ink, 0.88f, 0.05f);
                    break;

                case HudIcon.Time:
                    // An hourglass in a frame.
                    p.Line(ink, 0.08f, 0.2f, 0.08f, 0.8f, 0.08f);
                    p.Line(ink, 0.08f, 0.2f, 0.92f, 0.8f, 0.92f);
                    p.Poly(ink, 0.28f, 0.14f, 0.72f, 0.14f, 0.54f, 0.5f, 0.72f, 0.86f, 0.28f, 0.86f, 0.46f, 0.5f);
                    p.Poly(light, 0.34f, 0.84f, 0.66f, 0.84f, 0.5f, 0.64f);
                    break;

                case HudIcon.Pause:
                    p.Poly(ink, 0.24f, 0.16f, 0.42f, 0.16f, 0.42f, 0.84f, 0.24f, 0.84f);
                    p.Poly(ink, 0.58f, 0.16f, 0.76f, 0.16f, 0.76f, 0.84f, 0.58f, 0.84f);
                    break;

                case HudIcon.Play:
                    p.Poly(ink, 0.26f, 0.14f, 0.84f, 0.5f, 0.26f, 0.86f);
                    break;

                case HudIcon.Faster:
                    p.Poly(ink, 0.12f, 0.2f, 0.5f, 0.5f, 0.12f, 0.8f);
                    p.Poly(ink, 0.5f, 0.2f, 0.88f, 0.5f, 0.5f, 0.8f);
                    break;

                case HudIcon.Slower:
                    p.Poly(ink, 0.88f, 0.2f, 0.5f, 0.5f, 0.88f, 0.8f);
                    p.Poly(ink, 0.5f, 0.2f, 0.12f, 0.5f, 0.5f, 0.8f);
                    break;

                case HudIcon.Save:
                    // A seal pressed into wax.
                    p.Circle(ink, 0.5f, 0.56f, 0.34f);
                    p.Ring(light, 0.5f, 0.56f, 0.22f, 0.05f);
                    p.Poly(ink, 0.4f, 0.06f, 0.6f, 0.06f, 0.58f, 0.26f, 0.42f, 0.26f);
                    break;

                case HudIcon.Load:
                    // An unrolled scroll.
                    p.Poly(ink, 0.18f, 0.2f, 0.82f, 0.2f, 0.82f, 0.8f, 0.18f, 0.8f);
                    p.Circle(ink, 0.18f, 0.5f, 0.12f);
                    p.Circle(ink, 0.82f, 0.5f, 0.12f);
                    for (var i = 0; i < 3; i++) p.Line(light, 0.04f, 0.3f, 0.36f + i * 0.14f, 0.7f, 0.36f + i * 0.14f);
                    break;

                case HudIcon.Settings:
                    // A ship's wheel of eight spokes.
                    p.Ring(ink, 0.5f, 0.5f, 0.26f, 0.09f);
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = i * Mathf.PI / 4f;
                        p.Line(ink, 0.07f, 0.5f + Mathf.Cos(angle) * 0.1f, 0.5f + Mathf.Sin(angle) * 0.1f, 0.5f + Mathf.Cos(angle) * 0.44f, 0.5f + Mathf.Sin(angle) * 0.44f);
                    }
                    p.Circle(ink, 0.5f, 0.5f, 0.1f);
                    break;

                case HudIcon.Sailor:
                    // An oar with its blade.
                    p.Line(ink, 0.08f, 0.16f, 0.12f, 0.7f, 0.66f);
                    p.Poly(ink, 0.62f, 0.6f, 0.92f, 0.82f, 0.84f, 0.94f, 0.56f, 0.7f);
                    p.Circle(ink, 0.18f, 0.14f, 0.07f);
                    break;

                case HudIcon.Soldier:
                    // A crested helmet with cheek guards.
                    p.P.fillColor = ink;
                    p.P.BeginPath();
                    p.P.MoveTo(p.V(0.22f, 0.86f));
                    p.P.LineTo(p.V(0.22f, 0.5f));
                    p.P.QuadraticCurveTo(p.V(0.24f, 0.24f), p.V(0.5f, 0.22f));
                    p.P.QuadraticCurveTo(p.V(0.78f, 0.24f), p.V(0.78f, 0.5f));
                    p.P.LineTo(p.V(0.78f, 0.86f));
                    p.P.LineTo(p.V(0.62f, 0.86f));
                    p.P.LineTo(p.V(0.6f, 0.58f));
                    p.P.LineTo(p.V(0.4f, 0.58f));
                    p.P.LineTo(p.V(0.38f, 0.86f));
                    p.P.ClosePath();
                    p.P.Fill();
                    p.Curve(ink, 0.11f, 0.16f, 0.3f, 0.5f, -0.02f, 0.86f, 0.3f);
                    break;

                case HudIcon.Morale:
                    // A laurel wreath, open at the top.
                    p.Ring(ink, 0.5f, 0.54f, 0.32f, 0.05f, 120f, 420f);
                    for (var i = 0; i < 7; i++)
                    {
                        foreach (var side in new[] { -1f, 1f })
                        {
                            var angle = (90f + side * (40f + i * 20f)) * Mathf.Deg2Rad;
                            var cx = 0.5f + Mathf.Cos(angle) * 0.32f;
                            var cy = 0.54f + Mathf.Sin(angle) * 0.32f;
                            var tangent = angle + side * Mathf.PI * 0.35f;
                            p.Poly(ink, cx, cy, cx + Mathf.Cos(tangent) * 0.14f + 0.03f * side, cy + Mathf.Sin(tangent) * 0.14f,
                                cx + Mathf.Cos(tangent - 0.6f) * 0.08f, cy + Mathf.Sin(tangent - 0.6f) * 0.08f);
                        }
                    }
                    break;

                case HudIcon.Alarm:
                    // A curved horn.
                    p.P.fillColor = ink;
                    p.P.BeginPath();
                    p.P.MoveTo(p.V(0.12f, 0.62f));
                    p.P.QuadraticCurveTo(p.V(0.5f, 0.58f), p.V(0.72f, 0.2f));
                    p.P.LineTo(p.V(0.92f, 0.3f));
                    p.P.QuadraticCurveTo(p.V(0.74f, 0.82f), p.V(0.14f, 0.76f));
                    p.P.ClosePath();
                    p.P.Fill();
                    p.Line(light, 0.04f, 0.08f, 0.69f, 0.16f, 0.69f);
                    break;

                case HudIcon.Eye:
                    p.P.fillColor = ink;
                    p.P.BeginPath();
                    p.P.MoveTo(p.V(0.06f, 0.5f));
                    p.P.QuadraticCurveTo(p.V(0.5f, 0.08f), p.V(0.94f, 0.5f));
                    p.P.QuadraticCurveTo(p.V(0.5f, 0.92f), p.V(0.06f, 0.5f));
                    p.P.ClosePath();
                    p.P.Fill();
                    p.Circle(light, 0.5f, 0.5f, 0.15f);
                    p.Circle(ink, 0.5f, 0.5f, 0.07f);
                    break;

                case HudIcon.Cutter:
                    p.Hull(ink, 0.18f, 0.84f, 0.6f, 0.78f, 0.1f);
                    p.Line(ink, 0.04f, 0.5f, 0.6f, 0.5f, 0.2f);
                    p.Poly(light, 0.5f, 0.22f, 0.74f, 0.3f, 0.5f, 0.52f);
                    p.Line(ink, 0.04f, 0.3f, 0.62f, 0.22f, 0.86f);
                    p.Line(ink, 0.04f, 0.66f, 0.62f, 0.74f, 0.86f);
                    break;

                case HudIcon.Ram:
                    p.Hull(ink, 0.04f, 0.86f, 0.5f, 0.7f, 0.2f);
                    p.Poly(ink, 0.82f, 0.62f, 0.98f, 0.66f, 0.82f, 0.71f);
                    p.Line(ink, 0.04f, 0.46f, 0.5f, 0.46f, 0.12f);
                    p.Poly(light, 0.24f, 0.14f, 0.68f, 0.14f, 0.64f, 0.44f, 0.28f, 0.44f);
                    for (var i = 0; i < 5; i++) p.Line(ink, 0.03f, 0.26f + i * 0.12f, 0.64f, 0.18f + i * 0.12f, 0.88f);
                    break;

                case HudIcon.CrewDestroyer:
                    p.Hull(ink, 0.04f, 0.92f, 0.52f, 0.72f, 0.2f);
                    p.Line(ink, 0.04f, 0.46f, 0.52f, 0.46f, 0.14f);
                    p.Poly(light, 0.26f, 0.16f, 0.66f, 0.16f, 0.62f, 0.44f, 0.3f, 0.44f);
                    // A drawn bow on the foredeck.
                    p.Curve(ink, 0.04f, 0.72f, 0.24f, 0.9f, 0.36f, 0.72f, 0.5f);
                    p.Line(ink, 0.02f, 0.72f, 0.24f, 0.72f, 0.5f);
                    break;

                case HudIcon.Boarder:
                    p.Hull(ink, 0.02f, 0.94f, 0.48f, 0.76f, 0.18f);
                    p.Line(ink, 0.05f, 0.44f, 0.48f, 0.44f, 0.08f);
                    p.Poly(light, 0.2f, 0.1f, 0.68f, 0.1f, 0.64f, 0.4f, 0.24f, 0.4f);
                    // A grappling hook hanging at the bow.
                    p.Line(ink, 0.03f, 0.84f, 0.48f, 0.9f, 0.24f);
                    p.Curve(ink, 0.04f, 0.84f, 0.2f, 0.9f, 0.32f, 0.96f, 0.2f);
                    for (var i = 0; i < 4; i++) p.Circle(light, 0.24f + i * 0.14f, 0.58f, 0.045f);
                    break;

                case HudIcon.Merchant:
                    // A round-bellied hull with a broad sail.
                    p.P.fillColor = ink;
                    p.P.BeginPath();
                    p.P.MoveTo(p.V(0.08f, 0.56f));
                    p.P.LineTo(p.V(0.92f, 0.56f));
                    p.P.QuadraticCurveTo(p.V(0.86f, 0.86f), p.V(0.5f, 0.86f));
                    p.P.QuadraticCurveTo(p.V(0.14f, 0.86f), p.V(0.08f, 0.56f));
                    p.P.ClosePath();
                    p.P.Fill();
                    p.Line(ink, 0.04f, 0.5f, 0.56f, 0.5f, 0.08f);
                    p.Poly(light, 0.2f, 0.14f, 0.8f, 0.14f, 0.76f, 0.5f, 0.24f, 0.5f);
                    p.Line(ink, 0.03f, 0.2f, 0.14f, 0.8f, 0.14f);
                    break;

                case HudIcon.Shipyard:
                    // A keel on its stocks with a carpenter's hammer.
                    p.Line(ink, 0.07f, 0.1f, 0.78f, 0.9f, 0.78f);
                    for (var i = 0; i < 5; i++)
                    {
                        var x = 0.2f + i * 0.15f;
                        p.Curve(ink, 0.05f, x - 0.08f, 0.36f, x - 0.06f, 0.74f, x, 0.76f);
                        p.Curve(ink, 0.05f, x + 0.08f, 0.36f, x + 0.06f, 0.74f, x, 0.76f);
                    }
                    p.Line(ink, 0.05f, 0.7f, 0.08f, 0.9f, 0.3f);
                    p.Poly(ink, 0.6f, 0.04f, 0.78f, 0.02f, 0.82f, 0.14f, 0.64f, 0.16f);
                    break;

                case HudIcon.Outpost:
                case HudIcon.FoundOutpost:
                    // A Doric column on its base.
                    p.Poly(ink, 0.2f, 0.12f, 0.8f, 0.12f, 0.74f, 0.2f, 0.26f, 0.2f);
                    p.Poly(ink, 0.32f, 0.2f, 0.68f, 0.2f, 0.66f, 0.78f, 0.34f, 0.78f);
                    for (var i = 0; i < 3; i++) p.Line(light, 0.025f, 0.42f + i * 0.08f, 0.24f, 0.42f + i * 0.08f, 0.74f);
                    p.Poly(ink, 0.18f, 0.78f, 0.82f, 0.78f, 0.86f, 0.9f, 0.14f, 0.9f);
                    if (icon == HudIcon.FoundOutpost) p.Waves(light, 0.97f, 0.04f);
                    break;

                case HudIcon.Settlement:
                    // A temple front: pediment, four columns, steps.
                    p.Poly(ink, 0.08f, 0.32f, 0.5f, 0.1f, 0.92f, 0.32f);
                    for (var i = 0; i < 4; i++) p.Poly(ink, 0.16f + i * 0.2f, 0.36f, 0.24f + i * 0.2f, 0.36f, 0.24f + i * 0.2f, 0.78f, 0.16f + i * 0.2f, 0.78f);
                    p.Poly(ink, 0.06f, 0.8f, 0.94f, 0.8f, 0.94f, 0.9f, 0.06f, 0.9f);
                    break;

                case HudIcon.Wreck:
                    p.Poly(ink, 0.1f, 0.5f, 0.44f, 0.56f, 0.4f, 0.74f, 0.16f, 0.7f);
                    p.Poly(ink, 0.54f, 0.42f, 0.9f, 0.6f, 0.82f, 0.76f, 0.5f, 0.62f);
                    p.Line(ink, 0.04f, 0.48f, 0.58f, 0.6f, 0.18f);
                    p.Waves(light, 0.86f, 0.05f);
                    break;

                case HudIcon.Move:
                    // A bow-pointing arrow over the waves.
                    p.Poly(ink, 0.12f, 0.42f, 0.56f, 0.42f, 0.56f, 0.24f, 0.9f, 0.5f, 0.56f, 0.76f, 0.56f, 0.58f, 0.12f, 0.58f);
                    p.Waves(light, 0.9f, 0.05f);
                    break;

                case HudIcon.Stop:
                    // An open palm raised: halt.
                    p.Poly(ink, 0.28f, 0.5f, 0.72f, 0.5f, 0.7f, 0.9f, 0.32f, 0.9f);
                    for (var i = 0; i < 4; i++) p.Line(ink, 0.09f, 0.33f + i * 0.11f, 0.52f, 0.33f + i * 0.11f, 0.18f + (i == 0 || i == 3 ? 0.08f : 0f));
                    p.Line(ink, 0.09f, 0.28f, 0.6f, 0.16f, 0.46f);
                    break;

                case HudIcon.Attack:
                    // A spearhead.
                    p.Poly(ink, 0.5f, 0.04f, 0.68f, 0.34f, 0.56f, 0.5f, 0.44f, 0.5f, 0.32f, 0.34f);
                    p.Line(ink, 0.07f, 0.5f, 0.5f, 0.5f, 0.96f);
                    p.Line(light, 0.03f, 0.5f, 0.12f, 0.5f, 0.44f);
                    break;

                case HudIcon.RamOrder:
                    // A bronze ram head with three fins.
                    p.Poly(ink, 0.08f, 0.36f, 0.6f, 0.36f, 0.92f, 0.5f, 0.6f, 0.64f, 0.08f, 0.64f);
                    for (var i = 0; i < 3; i++) p.Line(light, 0.04f, 0.4f + i * 0.14f, 0.4f, 0.48f + i * 0.14f, 0.6f);
                    p.Waves(ink, 0.88f, 0.05f);
                    break;

                case HudIcon.Board:
                    // A grappling hook with its rope.
                    p.Line(ink, 0.07f, 0.5f, 0.1f, 0.5f, 0.62f);
                    p.Curve(ink, 0.07f, 0.5f, 0.62f, 0.22f, 0.66f, 0.24f, 0.4f);
                    p.Curve(ink, 0.07f, 0.5f, 0.62f, 0.78f, 0.66f, 0.76f, 0.4f);
                    p.Line(ink, 0.07f, 0.5f, 0.62f, 0.5f, 0.86f);
                    p.Curve(light, 0.04f, 0.5f, 0.1f, 0.3f, 0.02f, 0.12f, 0.2f);
                    break;

                case HudIcon.Volley:
                    // A strung bow with a nocked arrow.
                    p.Curve(ink, 0.07f, 0.3f, 0.08f, 0.82f, 0.5f, 0.3f, 0.92f);
                    p.Line(ink, 0.025f, 0.3f, 0.08f, 0.3f, 0.92f);
                    p.Line(ink, 0.04f, 0.14f, 0.5f, 0.86f, 0.5f);
                    p.Poly(ink, 0.84f, 0.42f, 0.98f, 0.5f, 0.84f, 0.58f);
                    break;

                case HudIcon.Harvest:
                    // An axe and a pick crossed.
                    p.Line(ink, 0.07f, 0.18f, 0.88f, 0.78f, 0.18f);
                    p.Poly(ink, 0.62f, 0.08f, 0.9f, 0.16f, 0.86f, 0.4f, 0.7f, 0.3f);
                    p.Line(ink, 0.07f, 0.82f, 0.88f, 0.22f, 0.18f);
                    p.Curve(light, 0.07f, 0.06f, 0.3f, 0.2f, 0.06f, 0.42f, 0.12f);
                    break;

                case HudIcon.Escort:
                    // A round hoplon with a boss and a rim.
                    p.Circle(ink, 0.5f, 0.5f, 0.42f);
                    p.Ring(light, 0.5f, 0.5f, 0.32f, 0.05f);
                    p.Circle(light, 0.5f, 0.5f, 0.1f);
                    break;

                case HudIcon.Repair:
                    // A carpenter's mallet over a plank.
                    p.Poly(ink, 0.1f, 0.74f, 0.9f, 0.74f, 0.9f, 0.86f, 0.1f, 0.86f);
                    p.Line(ink, 0.07f, 0.28f, 0.64f, 0.66f, 0.24f);
                    p.Poly(ink, 0.54f, 0.1f, 0.76f, 0.04f, 0.9f, 0.26f, 0.68f, 0.34f);
                    break;

                case HudIcon.Scrap:
                    // A hull broken in two.
                    p.Poly(ink, 0.06f, 0.42f, 0.44f, 0.42f, 0.36f, 0.62f, 0.44f, 0.76f, 0.22f, 0.76f);
                    p.Poly(ink, 0.56f, 0.42f, 0.94f, 0.42f, 0.8f, 0.76f, 0.56f, 0.76f, 0.64f, 0.62f);
                    p.Line(light, 0.04f, 0.5f, 0.12f, 0.5f, 0.3f);
                    break;

                case HudIcon.Cancel:
                    p.Line(ink, 0.12f, 0.22f, 0.22f, 0.78f, 0.78f);
                    p.Line(ink, 0.12f, 0.78f, 0.22f, 0.22f, 0.78f);
                    break;

                case HudIcon.WarLine:
                    // A trident.
                    p.Line(ink, 0.07f, 0.5f, 0.2f, 0.5f, 0.94f);
                    p.Curve(ink, 0.07f, 0.2f, 0.12f, 0.2f, 0.4f, 0.5f, 0.42f);
                    p.Curve(ink, 0.07f, 0.8f, 0.12f, 0.8f, 0.4f, 0.5f, 0.42f);
                    p.Poly(ink, 0.12f, 0.16f, 0.2f, 0.02f, 0.28f, 0.16f);
                    p.Poly(ink, 0.42f, 0.16f, 0.5f, 0.02f, 0.58f, 0.16f);
                    p.Poly(ink, 0.72f, 0.16f, 0.8f, 0.02f, 0.88f, 0.16f);
                    break;

                case HudIcon.PortLine:
                    // An anchor.
                    p.Ring(ink, 0.5f, 0.14f, 0.08f, 0.05f);
                    p.Line(ink, 0.07f, 0.5f, 0.22f, 0.5f, 0.88f);
                    p.Line(ink, 0.06f, 0.3f, 0.32f, 0.7f, 0.32f);
                    p.Curve(ink, 0.07f, 0.12f, 0.6f, 0.2f, 0.92f, 0.5f, 0.88f);
                    p.Curve(ink, 0.07f, 0.88f, 0.6f, 0.8f, 0.92f, 0.5f, 0.88f);
                    p.Poly(ink, 0.06f, 0.6f, 0.2f, 0.52f, 0.18f, 0.68f);
                    p.Poly(ink, 0.94f, 0.6f, 0.8f, 0.52f, 0.82f, 0.68f);
                    break;
            }
        }

        public static HudIcon ForClass(Simulation.ShipClass shipClass)
        {
            switch (shipClass)
            {
                case Simulation.ShipClass.Cutter: return HudIcon.Cutter;
                case Simulation.ShipClass.Ram: return HudIcon.Ram;
                case Simulation.ShipClass.CrewDestroyer: return HudIcon.CrewDestroyer;
                case Simulation.ShipClass.Merchant: return HudIcon.Merchant;
                default: return HudIcon.Boarder;
            }
        }
    }
}
