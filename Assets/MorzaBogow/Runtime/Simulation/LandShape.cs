using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// The exact waterline of land as a polygon: signed distance, inside tests and segment clearance. Positive
    /// distances are on the water. A circle (no outline) is handled exactly without a polygon.
    /// </summary>
    public static class LandShape
    {
        public const int Samples = 64;

        public static double Angle(int index) => index * 2 * Math.PI / Samples;

        public static double[] Ellipse(double halfX, double halfY)
        {
            var radii = new double[Samples];
            for (var i = 0; i < Samples; i++)
            {
                var c = Math.Cos(Angle(i)) / halfX;
                var s = Math.Sin(Angle(i)) / halfY;
                radii[i] = 1 / Math.Sqrt(c * c + s * s);
            }
            return radii;
        }

        /// <summary>Scales a unit outline and turns it by half a turn when asked (the map is point-symmetric).</summary>
        public static double[] Place(double[] unit, double scale, bool halfTurn)
        {
            var radii = new double[Samples];
            for (var i = 0; i < Samples; i++) radii[i] = unit[(i + (halfTurn ? Samples / 2 : 0)) % Samples] * scale;
            return radii;
        }

        public static double MaxRadius(double[] radii)
        {
            var max = 0.0;
            foreach (var value in radii) max = Math.Max(max, value);
            return max;
        }

        static void EnsurePolygon(Island island)
        {
            if (island.Outline == null || island.PolygonX != null) return;
            island.PolygonX = new double[Samples];
            island.PolygonY = new double[Samples];
            for (var i = 0; i < Samples; i++)
            {
                island.PolygonX[i] = island.X + Math.Cos(Angle(i)) * island.Outline[i];
                island.PolygonY[i] = island.Y + Math.Sin(Angle(i)) * island.Outline[i];
            }
        }

        public static int VertexCount(Island island) => island.Outline == null ? 0 : Samples;

        public static void Vertex(Island island, int index, out double x, out double y)
        {
            EnsurePolygon(island);
            x = island.PolygonX[index];
            y = island.PolygonY[index];
        }

        /// <summary>Distance from the waterline: positive on water, negative on land. Normal points out to the water.</summary>
        public static double SignedDistance(Island island, double x, double y, out double normalX, out double normalY)
        {
            var dx = x - island.X;
            var dy = y - island.Y;
            var fromCentre = Math.Sqrt(dx * dx + dy * dy);
            if (island.Outline == null)
            {
                normalX = fromCentre > 1e-9 ? dx / fromCentre : 1;
                normalY = fromCentre > 1e-9 ? dy / fromCentre : 0;
                return fromCentre - island.Radius;
            }

            EnsurePolygon(island);
            var best = double.MaxValue;
            var closestX = 0.0;
            var closestY = 0.0;
            var inside = false;
            for (int i = 0, j = Samples - 1; i < Samples; j = i++)
            {
                double ax = island.PolygonX[j], ay = island.PolygonY[j], bx = island.PolygonX[i], by = island.PolygonY[i];
                if ((ay > y) != (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) inside = !inside;
                ClosestOnSegment(ax, ay, bx, by, x, y, out var cx, out var cy);
                var d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 < best) { best = d2; closestX = cx; closestY = cy; }
            }

            var distance = Math.Sqrt(best);
            if (distance > 1e-9)
            {
                normalX = (x - closestX) / distance * (inside ? -1 : 1);
                normalY = (y - closestY) / distance * (inside ? -1 : 1);
            }
            else
            {
                normalX = fromCentre > 1e-9 ? dx / fromCentre : 1;
                normalY = fromCentre > 1e-9 ? dy / fromCentre : 0;
            }
            return inside ? -distance : distance;
        }

        public static double SignedDistance(Island island, double x, double y) => SignedDistance(island, x, y, out _, out _);

        /// <summary>Smallest distance between a segment and the land: zero when it touches or crosses it.</summary>
        public static double SegmentDistance(Island island, double ax, double ay, double bx, double by)
        {
            if (island.Outline == null)
            {
                return Math.Max(0, PointSegmentDistance(island.X, island.Y, ax, ay, bx, by) - island.Radius);
            }

            EnsurePolygon(island);
            if (SignedDistance(island, ax, ay) <= 0 || SignedDistance(island, bx, by) <= 0) return 0;
            var best = double.MaxValue;
            for (int i = 0, j = Samples - 1; i < Samples; j = i++)
            {
                double px = island.PolygonX[j], py = island.PolygonY[j], qx = island.PolygonX[i], qy = island.PolygonY[i];
                if (SegmentsCross(ax, ay, bx, by, px, py, qx, qy)) return 0;
                best = Math.Min(best, Math.Min(
                    Math.Min(PointSegmentDistance(ax, ay, px, py, qx, qy), PointSegmentDistance(bx, by, px, py, qx, qy)),
                    Math.Min(PointSegmentDistance(px, py, ax, ay, bx, by), PointSegmentDistance(qx, qy, ax, ay, bx, by))));
            }
            return best;
        }

        /// <summary>
        /// True when the segment touches the land or passes closer than <paramref name="limit"/> to it. Same answer as
        /// <c>SegmentDistance(...) &lt; limit</c> (or <c>&lt;= 0</c> for a zero limit), but edges far from the segment are
        /// skipped by their bounds, so the common far-from-land case is cheap.
        /// </summary>
        public static bool SegmentNear(Island island, double ax, double ay, double bx, double by, double limit)
        {
            if (island.Outline == null)
            {
                var gap = PointSegmentDistance(island.X, island.Y, ax, ay, bx, by) - island.Radius;
                return limit > 0 ? gap < limit : gap <= 0;
            }

            EnsurePolygon(island);
            var reach = Math.Max(0, limit);
            var minX = Math.Min(ax, bx) - reach;
            var maxX = Math.Max(ax, bx) + reach;
            var minY = Math.Min(ay, by) - reach;
            var maxY = Math.Max(ay, by) + reach;
            var limit2 = reach * reach;
            var insideA = false;
            for (int i = 0, j = Samples - 1; i < Samples; j = i++)
            {
                double px = island.PolygonX[j], py = island.PolygonY[j], qx = island.PolygonX[i], qy = island.PolygonY[i];
                if ((py > ay) != (qy > ay) && ax < (qx - px) * (ay - py) / (qy - py) + px) insideA = !insideA;
                if (Math.Max(px, qx) < minX || Math.Min(px, qx) > maxX || Math.Max(py, qy) < minY || Math.Min(py, qy) > maxY) continue;
                if (SegmentsCross(ax, ay, bx, by, px, py, qx, qy)) return true;
                if (limit <= 0) continue;
                if (PointSegmentDistance2(ax, ay, px, py, qx, qy) < limit2 || PointSegmentDistance2(bx, by, px, py, qx, qy) < limit2
                    || PointSegmentDistance2(px, py, ax, ay, bx, by) < limit2 || PointSegmentDistance2(qx, qy, ax, ay, bx, by) < limit2) return true;
            }
            // No edge is near or crossed: the segment lies wholly on land or wholly on water.
            return insideA;
        }

        static double PointSegmentDistance2(double x, double y, double ax, double ay, double bx, double by)
        {
            ClosestOnSegment(ax, ay, bx, by, x, y, out var cx, out var cy);
            return (x - cx) * (x - cx) + (y - cy) * (y - cy);
        }

        public static double PointSegmentDistance(double x, double y, double ax, double ay, double bx, double by)
        {
            ClosestOnSegment(ax, ay, bx, by, x, y, out var cx, out var cy);
            return Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }

        public static void ClosestOnSegment(double ax, double ay, double bx, double by, double x, double y, out double cx, out double cy)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var length2 = dx * dx + dy * dy;
            var t = length2 > 1e-12 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / length2)) : 0;
            cx = ax + t * dx;
            cy = ay + t * dy;
        }

        static bool SegmentsCross(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            var d1 = Cross(cx, cy, dx, dy, ax, ay);
            var d2 = Cross(cx, cy, dx, dy, bx, by);
            var d3 = Cross(ax, ay, bx, by, cx, cy);
            var d4 = Cross(ax, ay, bx, by, dx, dy);
            return ((d1 > 0) != (d2 > 0)) && ((d3 > 0) != (d4 > 0));
        }

        static double Cross(double ax, double ay, double bx, double by, double px, double py) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);

        /// <summary>Closest points of two segments, for hull-to-hull contact.</summary>
        public static double SegmentSegmentDistance(
            double p1x, double p1y, double q1x, double q1y, double p2x, double p2y, double q2x, double q2y,
            out double c1x, out double c1y, out double c2x, out double c2y)
        {
            double d1x = q1x - p1x, d1y = q1y - p1y, d2x = q2x - p2x, d2y = q2y - p2y, rx = p1x - p2x, ry = p1y - p2y;
            double a = d1x * d1x + d1y * d1y, e = d2x * d2x + d2y * d2y, f = d2x * rx + d2y * ry;
            double s, t;
            if (a <= 1e-12 && e <= 1e-12) { s = t = 0; }
            else if (a <= 1e-12) { s = 0; t = Math.Max(0, Math.Min(1, f / e)); }
            else
            {
                var c = d1x * rx + d1y * ry;
                if (e <= 1e-12) { t = 0; s = Math.Max(0, Math.Min(1, -c / a)); }
                else
                {
                    var b = d1x * d2x + d1y * d2y;
                    var denominator = a * e - b * b;
                    s = denominator > 1e-12 ? Math.Max(0, Math.Min(1, (b * f - c * e) / denominator)) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Math.Max(0, Math.Min(1, -c / a)); }
                    else if (t > 1) { t = 1; s = Math.Max(0, Math.Min(1, (b - c) / a)); }
                }
            }
            c1x = p1x + d1x * s; c1y = p1y + d1y * s;
            c2x = p2x + d2x * t; c2y = p2y + d2y * t;
            return Math.Sqrt((c1x - c2x) * (c1x - c2x) + (c1y - c2y) * (c1y - c2y));
        }
    }
}
