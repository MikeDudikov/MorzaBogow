using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The waterline of a piece of land as radii around its centre, and the seabed and surf built from it, so the
    /// shallows follow the land's real shape. Presentation only: collisions still use the land's circle.
    /// </summary>
    public static class ShoreShape
    {
        public const int Samples = 64;
        // Patches overlap by more than half, so the surf reads as one band along the shore, not separate puffs.
        public const float SurfSpacing = 6f;
        public const float SurfSize = 15f;

        /// <summary>
        /// Distance from the waterline outward and the seabed depth there. The first ring sits under the land's
        /// edge, so no gap opens at the shore; the last lies where the water has absorbed the floor.
        /// </summary>
        static readonly float[] RingOffset = { -3f, 0f, 3f, 7f, 13f, 21f, 31f, 44f };
        static readonly float[] RingDepth = { -0.5f, -0.8f, -1.4f, -2.6f, -4.4f, -6.8f, -9.6f, -13f };

        public static float Angle(int index) => index * 2f * Mathf.PI / Samples;

        public static float[] Ellipse(float halfX, float halfZ)
        {
            var radii = new float[Samples];
            for (var i = 0; i < Samples; i++)
            {
                var angle = Angle(i);
                var c = Mathf.Cos(angle) / halfX;
                var s = Mathf.Sin(angle) / halfZ;
                radii[i] = 1f / Mathf.Sqrt(c * c + s * s);
            }
            return radii;
        }

        public static float[] Scaled(float[] radii, float scale)
        {
            var result = new float[radii.Length];
            for (var i = 0; i < radii.Length; i++) result[i] = radii[i] * scale;
            return result;
        }

        /// <summary>Radius at any angle, interpolated between samples.</summary>
        public static float At(float[] radii, float angle)
        {
            var position = Mathf.Repeat(angle, 2f * Mathf.PI) / (2f * Mathf.PI) * Samples;
            var index = Mathf.FloorToInt(position);
            return Mathf.Lerp(radii[index % Samples], radii[(index + 1) % Samples], position - index);
        }

        /// <summary>
        /// Builds a waterline from points of a model near the water, relative to its centre on the water plane:
        /// the furthest point in each direction, gaps filled from neighbours, then lightly smoothed.
        /// </summary>
        public static float[] FromPoints(IEnumerable<Vector2> points)
        {
            var radii = new float[Samples];
            foreach (var point in points)
            {
                var index = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(point.y, point.x), 2f * Mathf.PI) / (2f * Mathf.PI) * Samples) % Samples;
                radii[index] = Mathf.Max(radii[index], point.magnitude);
            }

            for (var pass = 0; pass < Samples; pass++)
            {
                var filled = true;
                for (var i = 0; i < Samples; i++)
                {
                    if (radii[i] > 0f) continue;
                    var left = radii[(i + Samples - 1) % Samples];
                    var right = radii[(i + 1) % Samples];
                    if (left > 0f || right > 0f) radii[i] = left > 0f && right > 0f ? (left + right) * 0.5f : Mathf.Max(left, right);
                    else filled = false;
                }
                if (filled) break;
            }

            var smooth = new float[Samples];
            for (var i = 0; i < Samples; i++)
            {
                smooth[i] = (radii[(i + Samples - 1) % Samples] + 2f * radii[i] + radii[(i + 1) % Samples]) * 0.25f;
            }
            return smooth;
        }

        /// <summary>
        /// A seabed that falls away from the waterline. Further out the slope wanders a little, as real shallows do,
        /// but close in it hugs the shore. Seeded, so the same land always gets the same seabed.
        /// </summary>
        public static Mesh Shelf(float[] outline, double seed)
        {
            var rings = RingOffset.Length;
            var vertices = new Vector3[1 + rings * Samples];
            var triangles = new List<int>();
            vertices[0] = new Vector3(0f, RingDepth[0], 0f);

            var phase = (float)(seed % 1000.0);
            for (var ring = 0; ring < rings; ring++)
            {
                for (var i = 0; i < Samples; i++)
                {
                    var angle = Angle(i);
                    var wander = Mathf.Sin(angle * 3f + phase) * 0.22f + Mathf.Sin(angle * 5f + phase * 1.7f) * 0.12f;
                    var offset = RingOffset[ring] * (RingOffset[ring] > 0f ? 1f + wander : 1f);
                    var radius = Mathf.Max(0.5f, outline[i] + offset);
                    vertices[1 + ring * Samples + i] = new Vector3(Mathf.Cos(angle) * radius, RingDepth[ring], Mathf.Sin(angle) * radius);
                }
            }

            // Triangles wind clockwise seen from above, so the floor faces up toward the camera.
            for (var i = 0; i < Samples; i++)
            {
                var next = (i + 1) % Samples;
                triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + i);
            }
            for (var ring = 0; ring < rings - 1; ring++)
            {
                for (var i = 0; i < Samples; i++)
                {
                    var next = (i + 1) % Samples;
                    var a = 1 + ring * Samples + i;
                    var b = 1 + ring * Samples + next;
                    var c = 1 + (ring + 1) * Samples + i;
                    var d = 1 + (ring + 1) * Samples + next;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            var mesh = new Mesh { name = "Underwater shelf", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Points just outside the waterline, about one spacing apart, with the distance along the shore.</summary>
        public static IEnumerable<(Vector3 point, float along)> SurfPoints(float[] outline, float spacing)
        {
            var perimeter = 0f;
            var previous = Vector3.zero;
            for (var i = 0; i <= Samples; i++)
            {
                var angle = Angle(i % Samples);
                var current = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * outline[i % Samples];
                if (i > 0) perimeter += Vector3.Distance(previous, current);
                previous = current;
            }

            var count = Mathf.Max(6, Mathf.RoundToInt(perimeter / spacing));
            var walked = 0f;
            var step = perimeter / count;
            var target = 0f;
            previous = new Vector3(outline[0], 0f, 0f);
            for (var i = 1; i <= Samples && target < perimeter - 0.01f; i++)
            {
                var angle = Angle(i % Samples);
                var current = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * outline[i % Samples];
                var length = Vector3.Distance(previous, current);
                while (target <= walked + length && target < perimeter - 0.01f)
                {
                    var point = Vector3.Lerp(previous, current, length > 0f ? (target - walked) / length : 0f);
                    yield return (point + point.normalized * 2f, target);
                    target += step;
                }
                walked += length;
                previous = current;
            }
        }
    }
}
