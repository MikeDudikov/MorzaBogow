using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Bakes a species' leaf texture in four quarters: a spray from above and the same spray from below, and a cluster
    /// of sprays from above and from below for the farther levels of detail. Colour with coverage in alpha, a
    /// tangent-space normal map and an HDRP mask map (R metallic, G occlusion, B detail mask, A smoothness).
    /// The leaves are drawn at the species' size (olive: 3–7 cm by 8–10 mm) in opposite pairs along the shoots, every
    /// other pair turned toward the viewer; the upper side grey-green and smooth, the underside silvery and matt (its
    /// peltate scales). Deterministic: the same species and size give the same pixels.
    /// </summary>
    public static class LeafAtlas
    {
        public sealed class Result
        {
            public int Size;
            public Color32[] Colour, Normal, Mask;

            /// <summary>Share of the quarter covered by leaves and shoots (alpha over one half).</summary>
            public float Coverage(int cell)
            {
                var (x0, y0, half) = Cell(cell);
                var covered = 0;
                for (var y = 0; y < half; y++)
                {
                    for (var x = 0; x < half; x++)
                    {
                        if (Colour[(y0 + y) * Size + x0 + x].a > 127) covered++;
                    }
                }
                return covered / (float)(half * half);
            }

            /// <summary>Mean luminance of the covered texels of a quarter (sRGB, 0–1).</summary>
            public float Brightness(int cell)
            {
                var (x0, y0, half) = Cell(cell);
                double sum = 0;
                var count = 0;
                for (var y = 0; y < half; y++)
                {
                    for (var x = 0; x < half; x++)
                    {
                        var c = Colour[(y0 + y) * Size + x0 + x];
                        if (c.a <= 127) continue;
                        sum += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0;
                        count++;
                    }
                }
                return count > 0 ? (float)(sum / count) : 0f;
            }

            /// <summary>Mean smoothness (mask alpha) of the covered texels of a quarter.</summary>
            public float Smoothness(int cell)
            {
                var (x0, y0, half) = Cell(cell);
                double sum = 0;
                var count = 0;
                for (var y = 0; y < half; y++)
                {
                    for (var x = 0; x < half; x++)
                    {
                        var i = (y0 + y) * Size + x0 + x;
                        if (Colour[i].a <= 127) continue;
                        sum += Mask[i].a / 255.0;
                        count++;
                    }
                }
                return count > 0 ? (float)(sum / count) : 0f;
            }

            (int x0, int y0, int half) Cell(int cell)
            {
                var half = Size / 2;
                var corner = TreeGenerator.AtlasCells[cell];
                return ((int)(corner.x * Size), (int)(corner.y * Size), half);
            }
        }

        sealed class Canvas
        {
            public readonly int Size;
            public readonly float[] R, G, B, A, Nx, Ny, Smooth, Occlusion;

            public Canvas(int size)
            {
                Size = size;
                var n = size * size;
                R = new float[n];
                G = new float[n];
                B = new float[n];
                A = new float[n];
                Nx = new float[n];
                Ny = new float[n];
                Smooth = new float[n];
                Occlusion = new float[n];
                for (var i = 0; i < n; i++) Occlusion[i] = 1f;
            }

            public void Put(int i, float alpha, Color colour, Vector2 normal, float smooth, float occlusion)
            {
                if (alpha <= 0f) return;
                var keep = 1f - alpha;
                R[i] = colour.r * alpha + R[i] * keep;
                G[i] = colour.g * alpha + G[i] * keep;
                B[i] = colour.b * alpha + B[i] * keep;
                Nx[i] = normal.x * alpha + Nx[i] * keep;
                Ny[i] = normal.y * alpha + Ny[i] * keep;
                Smooth[i] = smooth * alpha + Smooth[i] * keep;
                Occlusion[i] = occlusion * alpha + Occlusion[i] * keep;
                A[i] = alpha + A[i] * keep;
            }
        }

        /// <summary>A placement of a spray in a quarter: cell = origin + rotation·(p − (0.5, 0))·scale.</summary>
        struct Placement
        {
            public Vector2 Origin;
            public float Angle, Scale;

            public Vector2 Map(Vector2 p)
            {
                var q = (p - new Vector2(0.5f, 0f)) * Scale;
                var c = Mathf.Cos(Angle);
                var s = Mathf.Sin(Angle);
                return Origin + new Vector2(c * q.x - s * q.y, s * q.x + c * q.y);
            }

            public Vector2 Turn(Vector2 d)
            {
                var c = Mathf.Cos(Angle);
                var s = Mathf.Sin(Angle);
                return new Vector2(c * d.x - s * d.y, s * d.x + c * d.y);
            }
        }

        public static Result Bake(TreeSpecies species, int size = 1024)
        {
            size = Mathf.Max(64, size - size % 2);
            var half = size / 2;
            var seed = Fnv(species.Id);
            // The near card: six branchlets at the leaves' true size; the far card: ten smaller ones, denser.
            var spray = new Canvas(half);
            var sprayUnder = new Canvas(half);
            Cluster(species, StableHash.Mix(seed, 0xA1u), 6, 0.55f, 0.7f, spray, sprayUnder);
            var cluster = new Canvas(half);
            var clusterUnder = new Canvas(half);
            Cluster(species, StableHash.Mix(seed, 0xC1u), 10, 0.4f, 0.55f, cluster, clusterUnder);

            var result = new Result { Size = size, Colour = new Color32[size * size], Normal = new Color32[size * size], Mask = new Color32[size * size] };
            var canvases = new[] { spray, sprayUnder, cluster, clusterUnder };
            for (var cell = 0; cell < 4; cell++)
            {
                Dilate(canvases[cell]);
                var corner = TreeGenerator.AtlasCells[cell];
                Blit(canvases[cell], result, (int)(corner.x * size), (int)(corner.y * size));
            }
            return result;
        }

        /// <summary>Branchlets fanned out from the foot of a card, overlapping like the shoots of one bough.</summary>
        static void Cluster(TreeSpecies species, uint seed, int count, float smallest, float largest, Canvas top, Canvas under)
        {
            var random = new StableHash.Sequence(seed);
            for (var k = 0; k < count; k++)
            {
                var placement = new Placement
                {
                    Origin = new Vector2(0.14f + 0.72f * k / Mathf.Max(1, count - 1) + random.Range(-0.04f, 0.04f), random.Range(0.02f, 0.4f)),
                    Angle = (k - 0.5f * (count - 1)) * -0.14f + random.Range(-0.15f, 0.15f),
                    Scale = random.Range(smallest, largest),
                };
                Spray(species, StableHash.Mix(seed, (uint)k + 1u), placement, top, under);
            }
        }

        static uint Fnv(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }

        /// <summary>One spray: a main shoot with two or three side shoots, leaves in opposite pairs along all of them.</summary>
        static void Spray(TreeSpecies species, uint seed, Placement placement, Canvas top, Canvas under)
        {
            var random = new StableHash.Sequence(seed);
            // Lengths in the branchlet's own units, so after its placement's scale the leaves keep their true size on the card.
            var unit = 1f / Mathf.Max(species.SprayLength * placement.Scale, 0.02f);
            var shoots = new List<(Vector2[] path, float width)>();
            var main = Curve(new Vector2(0.5f, 0f), new Vector2(0.5f + random.Range(-0.08f, 0.08f), 0.5f), new Vector2(0.5f + random.Range(-0.1f, 0.1f), 0.95f), 24);
            shoots.Add((main, 0.011f));
            // A branchlet: the leading shoot with seven to nine side shoots, alternately left and right.
            var sides = random.Range(7, 10);
            for (var s = 0; s < sides; s++)
            {
                var at = Mathf.Lerp(0.1f, 0.78f, (s + random.Range(0.1f, 0.9f)) / sides);
                var index = Mathf.Clamp(Mathf.RoundToInt(at * (main.Length - 1)), 1, main.Length - 2);
                var direction = (main[index + 1] - main[index - 1]).normalized;
                var side = s % 2 == 0 ? 1f : -1f;
                var angle = side * random.Range(32f, 58f) * Mathf.Deg2Rad;
                var turned = Rotate(direction, angle);
                var length = random.Range(0.25f, 0.45f) * Mathf.Lerp(1f, 0.6f, at);
                var end = main[index] + turned * length;
                var bend = main[index] + turned * (0.5f * length) + Rotate(turned, side * Mathf.PI * 0.5f) * random.Range(-0.04f, 0.04f);
                shoots.Add((Curve(main[index], bend, end, 12), 0.006f));
            }

            // Shoots first, leaves over them.
            foreach (var (path, width) in shoots) Shoot(path, width, placement, top, under);
            var spacing = 0.018f * unit;
            foreach (var (path, width) in shoots)
            {
                var total = Length(path);
                var pairs = Mathf.Max(2, Mathf.FloorToInt(total / spacing));
                for (var p = 1; p <= pairs; p++)
                {
                    var along = Mathf.Min(total, p * spacing - random.Range(0f, 0.3f) * spacing);
                    var (point, direction) = At(path, along);
                    var youth = along / total;
                    // Every other pair stands across the view (decussate): drawn short, as if seen end-on.
                    var across = p % 2 == 0;
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var leafLength = random.Range(species.LeafLengthMin, species.LeafLengthMax) * unit * Mathf.Lerp(1f, 0.6f, youth * youth);
                        if (across) leafLength *= random.Range(0.55f, 0.75f);
                        var leafWidth = species.LeafWidth * unit * random.Range(0.9f, 1.12f);
                        var angle = side * random.Range(28f, 52f) * Mathf.Deg2Rad;
                        var leafDirection = Rotate(direction, angle);
                        var curl = side * random.Range(-0.6f, 1.2f);
                        var shade = random.Range(0.86f, 1.1f);
                        Leaf(species, point, leafDirection, leafLength, leafWidth, curl, shade, youth, random.Range(0f, 1f), placement, top, under);
                    }
                }
            }
        }

        static Vector2[] Curve(Vector2 a, Vector2 b, Vector2 c, int segments)
        {
            var points = new Vector2[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                points[i] = (1f - t) * (1f - t) * a + 2f * (1f - t) * t * b + t * t * c;
            }
            return points;
        }

        static float Length(Vector2[] path)
        {
            var total = 0f;
            for (var i = 1; i < path.Length; i++) total += (path[i] - path[i - 1]).magnitude;
            return total;
        }

        static (Vector2 point, Vector2 direction) At(Vector2[] path, float along)
        {
            for (var i = 1; i < path.Length; i++)
            {
                var step = (path[i] - path[i - 1]).magnitude;
                if (along <= step || i == path.Length - 1)
                {
                    var direction = (path[i] - path[i - 1]).normalized;
                    return (path[i - 1] + direction * Mathf.Min(along, step), direction);
                }
                along -= step;
            }
            return (path[path.Length - 1], Vector2.up);
        }

        static Vector2 Rotate(Vector2 v, float angle)
        {
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        static void Shoot(Vector2[] path, float width, Placement placement, Canvas top, Canvas under)
        {
            var size = top.Size;
            for (var i = 1; i < path.Length; i++)
            {
                var a = placement.Map(path[i - 1]) * size;
                var b = placement.Map(path[i]) * size;
                var t0 = (i - 1f) / (path.Length - 1);
                var half = 0.5f * width * placement.Scale * size * Mathf.Lerp(1f, 0.45f, t0);
                var young = new Color(0.50f, 0.54f, 0.38f);
                var old = new Color(0.46f, 0.43f, 0.36f);
                var colour = Color.Lerp(old, young, t0);
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
                        var distance = offset.magnitude;
                        var alpha = Mathf.Clamp01(half - distance + 0.5f);
                        if (alpha <= 0f) continue;
                        var side = half > 0f ? Mathf.Clamp(Vector2.Dot(offset, across) / half, -1f, 1f) : 0f;
                        var normal = across * (0.8f * side);
                        var i0 = y * size + x;
                        top.Put(i0, alpha, colour, normal, 0.25f, 0.7f);
                        under.Put(i0, alpha, colour * 0.92f, -normal, 0.22f, 0.7f);
                    }
                }
            }
        }

        static void Leaf(TreeSpecies species, Vector2 basePoint, Vector2 direction, float length, float width, float curl, float shade, float youth, float speck,
            Placement placement, Canvas top, Canvas under)
        {
            var size = top.Size;
            var origin = placement.Map(basePoint) * size;
            var d = placement.Turn(direction).normalized;
            var p = new Vector2(-d.y, d.x);
            var l = length * placement.Scale * size;
            var w = width * placement.Scale * size;
            if (l < 1.5f) return;
            var bendPerPixel = curl * 0.25f / Mathf.Max(l, 1f);
            var reach = l + w + 2f;
            var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(origin.x, origin.x + d.x * l) - reach * 0.5f - 2f));
            var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(origin.x, origin.x + d.x * l) + reach * 0.5f + 2f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(origin.y, origin.y + d.y * l) - reach * 0.5f - 2f));
            var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(origin.y, origin.y + d.y * l) + reach * 0.5f + 2f));

            // Upper side: the species' grey-green, older leaves near the base a little darker and yellower.
            var upper = species.LeafTop * shade;
            upper.r += 0.03f * (1f - youth);
            upper.g += 0.05f * youth;
            var lower = species.LeafUnder * Mathf.Lerp(0.95f, 1.04f, speck);
            var rib = new Color(0.62f, 0.58f, 0.45f);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f) - origin;
                    var along = Vector2.Dot(q, d);
                    if (along < 0f || along > l + 1f) continue;
                    var s = along / l;
                    var centre = bendPerPixel * along * along;
                    var t = Vector2.Dot(q, p) - centre;
                    // Lanceolate: a short stalk, widest a little below the middle, an acute tip.
                    var blade = s < 0.08f ? 0.12f : Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow((s - 0.08f) / 0.92f, 0.85f)), 0.9f);
                    var halfWidth = 0.5f * w * blade;
                    var alpha = Mathf.Clamp01(halfWidth - Mathf.Abs(t) + 0.5f) * Mathf.Clamp01(l - along + 0.5f);
                    if (alpha <= 0f) continue;
                    var across = halfWidth > 0.01f ? t / halfWidth : 0f;
                    // A shallow V along the midrib and a tip curling down: the normal leans toward the rib on top.
                    var tilt = -p * (0.28f * Mathf.Sign(t) * Mathf.Clamp01(Mathf.Abs(across) * 3f)) - d * (0.12f * (s - 0.5f));
                    var onRib = Mathf.Abs(t) < Mathf.Max(0.6f, 0.12f * halfWidth);
                    var edge = Mathf.Abs(across) > 0.85f;
                    var colourTop = onRib ? upper * 1.18f : edge ? upper * 0.9f : upper;
                    var colourUnder = onRib ? rib : lower;
                    var occlusion = 0.82f + 0.18f * s;
                    var i0 = y * size + x;
                    top.Put(i0, alpha, colourTop, tilt, 0.55f, occlusion);
                    under.Put(i0, alpha, colourUnder, -tilt, 0.3f, occlusion);
                }
            }
        }

        /// <summary>Spreads the leaves' colour into the empty texels around them, so the smaller mips do not fringe dark.</summary>
        static void Dilate(Canvas canvas)
        {
            var size = canvas.Size;
            var n = size * size;
            var filled = new bool[n];
            double r = 0, g = 0, b = 0;
            var covered = 0;
            for (var i = 0; i < n; i++)
            {
                if (canvas.A[i] <= 0.5f) continue;
                filled[i] = true;
                r += canvas.R[i] / canvas.A[i];
                g += canvas.G[i] / canvas.A[i];
                b += canvas.B[i] / canvas.A[i];
                covered++;
            }
            // Texels partly covered keep their own colour, unpremultiplied.
            for (var i = 0; i < n; i++)
            {
                if (canvas.A[i] <= 0.001f) continue;
                canvas.R[i] /= canvas.A[i];
                canvas.G[i] /= canvas.A[i];
                canvas.B[i] /= canvas.A[i];
                canvas.Smooth[i] /= canvas.A[i];
                canvas.Nx[i] /= canvas.A[i];
                canvas.Ny[i] /= canvas.A[i];
                canvas.Occlusion[i] = Mathf.Clamp01(canvas.Occlusion[i]);
                filled[i] = true;
            }
            var mean = covered > 0 ? new Color((float)(r / covered), (float)(g / covered), (float)(b / covered)) : Color.gray;
            var next = new bool[n];
            for (var pass = 0; pass < 12; pass++)
            {
                System.Array.Copy(filled, next, n);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var i = y * size + x;
                        if (filled[i]) continue;
                        float sr = 0, sg = 0, sb = 0, ss = 0;
                        var count = 0;
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            var yy = y + dy;
                            if (yy < 0 || yy >= size) continue;
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                var xx = x + dx;
                                if (xx < 0 || xx >= size) continue;
                                var j = yy * size + xx;
                                if (!filled[j]) continue;
                                sr += canvas.R[j];
                                sg += canvas.G[j];
                                sb += canvas.B[j];
                                ss += canvas.Smooth[j];
                                count++;
                            }
                        }
                        if (count == 0) continue;
                        canvas.R[i] = sr / count;
                        canvas.G[i] = sg / count;
                        canvas.B[i] = sb / count;
                        canvas.Smooth[i] = ss / count;
                        next[i] = true;
                    }
                }
                System.Array.Copy(next, filled, n);
            }
            for (var i = 0; i < n; i++)
            {
                if (filled[i]) continue;
                canvas.R[i] = mean.r;
                canvas.G[i] = mean.g;
                canvas.B[i] = mean.b;
                canvas.Smooth[i] = 0.4f;
            }
        }

        static void Blit(Canvas canvas, Result result, int x0, int y0)
        {
            var size = canvas.Size;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * size + x;
                    var o = (y0 + y) * result.Size + x0 + x;
                    result.Colour[o] = new Color32(Byte(canvas.R[i]), Byte(canvas.G[i]), Byte(canvas.B[i]), Byte(canvas.A[i]));
                    var nx = canvas.Nx[i];
                    var ny = canvas.Ny[i];
                    var nz = Mathf.Sqrt(Mathf.Max(0.05f, 1f - nx * nx - ny * ny));
                    var length = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    result.Normal[o] = new Color32(Byte(0.5f + 0.5f * nx / length), Byte(0.5f + 0.5f * ny / length), Byte(0.5f + 0.5f * nz / length), 255);
                    result.Mask[o] = new Color32(0, Byte(canvas.Occlusion[i]), 0, Byte(canvas.Smooth[i]));
                }
            }
        }

        static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }
}
