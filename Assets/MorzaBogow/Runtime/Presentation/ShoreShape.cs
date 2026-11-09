using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The waterline of a piece of land as radii around its centre (the simulation's own outline), and the seabed,
    /// beach, islet mound and surf built from it, so what the player sees is the shore ships steer by.
    /// </summary>
    public static class ShoreShape
    {
        public const int Samples = Simulation.LandShape.Samples;
        // Patches overlap by more than half, so the surf reads as one band along the shore, not separate puffs.
        public const float SurfSpacing = 6f;
        public const float SurfSize = 15f;

        /// <summary>The simulation's seabed profile: what is too shallow for a hull looks shallow.</summary>
        static readonly float[] RingOffset = System.Array.ConvertAll(Simulation.Seabed.Offset, value => (float)value);
        static readonly float[] RingDepth = System.Array.ConvertAll(Simulation.Seabed.Depth, value => -(float)value);

        /// <summary>
        /// An islet rises gently: a wet beach from below the water to the dry sand, then grass or rock to a low
        /// crown. Fractions of the waterline radius, inner first; the land material starts at ring LandRings.
        /// </summary>
        static readonly float[] MoundFraction = { 0.18f, 0.4f, 0.62f, 0.78f, 0.86f, 0.93f, 0.97f, 1.0f, 1.1f };
        static readonly float[] MoundHeightAt = { 2.1f, 1.9f, 1.4f, 0.75f, 0.5f, 0.25f, 0.1f, -0.05f, -0.7f };

        /// <summary>
        /// Where the shore is a cliff: a limestone plateau that ends in a near-vertical face dropping straight into
        /// deeper water, as on the rocky Aegean islets. Blended with the gentle profile by each direction's lift.
        /// </summary>
        static readonly float[] CliffHeightAt = { 5.6f, 5.5f, 5.2f, 4.8f, 4.4f, 3.7f, 2.2f, -0.3f, -2.6f };
        const int LandRings = 3;
        const float CrownHeight = 2.15f, CliffCrown = 5.7f;

        /// <summary>
        /// How much of a cliff each direction has: none in the sheltered sector where jetties and slipways meet the
        /// water, rising to full on the open side, with an uneven crest so the cliffs are not a ring.
        /// </summary>
        public static float[] Cliffs(float shelteredAngle, float clearDegrees, float fullDegrees, float seed)
        {
            var lift = new float[Samples];
            for (var i = 0; i < Samples; i++)
            {
                var angle = Angle(i);
                var away = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, shelteredAngle * Mathf.Rad2Deg));
                var open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clearDegrees, fullDegrees, away));
                var crest = 0.72f + 0.28f * Mathf.Sin(angle * 3f + seed) * Mathf.Sin(angle * 5f + seed * 1.7f);
                lift[i] = Mathf.Clamp01(open * crest);
            }
            return lift;
        }

        static float Lifted(float gentle, float cliff, float lift) => Mathf.Lerp(gentle, cliff, lift);

        /// <summary>Ground height at a fraction of the waterline radius, with this much cliff (0 none, 1 full).</summary>
        public static float MoundHeight(float fraction, float lift)
        {
            if (lift <= 0f) return MoundHeight(fraction);
            if (fraction <= MoundFraction[0]) return Mathf.Lerp(Lifted(CrownHeight, CliffCrown, lift), Lifted(MoundHeightAt[0], CliffHeightAt[0], lift), fraction / MoundFraction[0]);
            for (var i = 1; i < MoundFraction.Length; i++)
            {
                if (fraction > MoundFraction[i]) continue;
                var t = (fraction - MoundFraction[i - 1]) / (MoundFraction[i] - MoundFraction[i - 1]);
                return Mathf.Lerp(Lifted(MoundHeightAt[i - 1], CliffHeightAt[i - 1], lift), Lifted(MoundHeightAt[i], CliffHeightAt[i], lift), t);
            }
            return Lifted(MoundHeightAt[MoundHeightAt.Length - 1], CliffHeightAt[CliffHeightAt.Length - 1], lift);
        }

        /// <summary>Ground height of an islet with cliffs at a point relative to its centre.</summary>
        public static float GroundAt(float[] outline, float[] lift, float x, float z)
        {
            if (lift == null) return GroundAt(outline, x, z);
            var angle = Mathf.Atan2(z, x);
            var reach = At(outline, angle);
            return MoundHeight(Mathf.Sqrt(x * x + z * z) / Mathf.Max(reach, 0.1f), At(lift, angle));
        }

        /// <summary>A beach skirt around a rocky coast: it rises inland under the rock and runs out under the water.</summary>
        static readonly float[] BeachOffset = { -10f, -5f, -1.5f, 0f, 2.5f };
        static readonly float[] BeachHeight = { 0.6f, 0.35f, 0.08f, -0.1f, -0.7f };

        /// <summary>Ground height of an islet at a fraction of the waterline radius from its centre.</summary>
        public static float MoundHeight(float fraction)
        {
            if (fraction <= MoundFraction[0]) return Mathf.Lerp(CrownHeight, MoundHeightAt[0], fraction / MoundFraction[0]);
            for (var i = 1; i < MoundFraction.Length; i++)
            {
                if (fraction > MoundFraction[i]) continue;
                return Mathf.Lerp(MoundHeightAt[i - 1], MoundHeightAt[i], (fraction - MoundFraction[i - 1]) / (MoundFraction[i] - MoundFraction[i - 1]));
            }
            return MoundHeightAt[MoundHeightAt.Length - 1];
        }

        /// <summary>Ground height of an islet at a point relative to its centre.</summary>
        public static float GroundAt(float[] outline, float x, float z)
        {
            var reach = At(outline, Mathf.Atan2(z, x));
            return MoundHeight(Mathf.Sqrt(x * x + z * z) / Mathf.Max(reach, 0.1f));
        }

        /// <summary>The islet itself: submesh 0 is the rocky shore and cliffs, submesh 1 the land on top.</summary>
        public static Mesh Mound(float[] outline, float[] lift = null)
        {
            var rings = MoundFraction.Length;
            var vertices = new Vector3[1 + rings * Samples];
            var crownLift = 0f;
            if (lift != null) { foreach (var value in lift) crownLift += value; crownLift /= Samples; }
            vertices[0] = new Vector3(0f, Lifted(CrownHeight, CliffCrown, crownLift), 0f);
            for (var ring = 0; ring < rings; ring++)
            {
                for (var i = 0; i < Samples; i++)
                {
                    var angle = Angle(i);
                    var radius = outline[i] * MoundFraction[ring];
                    var height = lift == null ? MoundHeightAt[ring] : Lifted(MoundHeightAt[ring], CliffHeightAt[ring], lift[i]);
                    vertices[1 + ring * Samples + i] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                }
            }

            var land = new List<int>();
            var beach = new List<int>();
            AddCap(land);
            for (var ring = 0; ring < rings - 1; ring++) AddRing(ring < LandRings ? land : beach, ring);

            var mesh = new Mesh { name = "Islet", vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(beach, 0);
            mesh.SetTriangles(land, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A sandy foreshore under a rocky coast, so the rock does not end in a sheer edge at the water.</summary>
        public static Mesh Beach(float[] outline)
        {
            var rings = BeachOffset.Length;
            var vertices = new Vector3[1 + rings * Samples];
            vertices[0] = new Vector3(0f, BeachHeight[0] - 0.5f, 0f);
            for (var ring = 0; ring < rings; ring++)
            {
                for (var i = 0; i < Samples; i++)
                {
                    var angle = Angle(i);
                    var radius = Mathf.Max(0.5f, outline[i] + BeachOffset[ring]);
                    vertices[1 + ring * Samples + i] = new Vector3(Mathf.Cos(angle) * radius, BeachHeight[ring], Mathf.Sin(angle) * radius);
                }
            }
            var triangles = new List<int>();
            for (var ring = 0; ring < rings - 1; ring++) AddRing(triangles, ring);
            var mesh = new Mesh { name = "Beach", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Triangles wind clockwise seen from above, so surfaces face up toward the camera.
        static void AddCap(List<int> triangles)
        {
            for (var i = 0; i < Samples; i++)
            {
                triangles.Add(0); triangles.Add(1 + (i + 1) % Samples); triangles.Add(1 + i);
            }
        }

        static void AddRing(List<int> triangles, int ring)
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

            // Gaps are bridged by a straight line between the known directions on both sides.
            for (var i = 0; i < Samples; i++)
            {
                if (radii[i] > 0f) continue;
                var left = i;
                while (radii[(left + Samples - 1) % Samples] <= 0f && left - i > -Samples) left--;
                left = (left + Samples - 1) % Samples;
                var right = i;
                while (radii[(right + 1) % Samples] <= 0f && right - i < Samples) right++;
                right = (right + 1) % Samples;
                if (radii[left] <= 0f || radii[right] <= 0f) continue;
                var span = (right - left + Samples) % Samples;
                var step = (i - left + Samples) % Samples;
                radii[i] = Mathf.Lerp(radii[left], radii[right], span > 0 ? (float)step / span : 0f);
            }

            // A notch one or two directions wide comes from missing vertices, not from a real bay: bridge it.
            var raw = (float[])radii.Clone();
            for (var i = 0; i < Samples; i++)
            {
                var left = Mathf.Max(raw[(i + Samples - 2) % Samples], raw[(i + Samples - 1) % Samples]);
                var right = Mathf.Max(raw[(i + 1) % Samples], raw[(i + 2) % Samples]);
                if (raw[i] < 0.75f * Mathf.Min(left, right)) radii[i] = (left + right) * 0.5f;
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
