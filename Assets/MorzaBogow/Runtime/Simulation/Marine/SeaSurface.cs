using System;
using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Depth of the water on a 10 m grid from the map's shores and the Seabed profile (the same profile ships keep off
    /// and the presentation draws), 13 m away from land. Built from the islands, never saved.
    /// </summary>
    public sealed class Bathymetry
    {
        public const double Spacing = 10;
        readonly double originX, originY;
        readonly int nx, ny;
        readonly float[] depth;
        readonly double open;

        public Bathymetry(IReadOnlyList<Island> islands, double originX = -1300, double originY = -1300, double size = 2600)
        {
            this.originX = originX;
            this.originY = originY;
            nx = ny = (int)(size / Spacing) + 1;
            open = Seabed.Depth[Seabed.Depth.Length - 1];
            depth = new float[nx * ny];
            for (var n = 0; n < depth.Length; n++) depth[n] = (float)open;
            if (islands == null) return;
            var shelf = Seabed.Offset[Seabed.Offset.Length - 1];
            foreach (var island in islands)
            {
                var reach = (island.Outline == null ? island.Radius : LandShape.MaxRadius(island.Outline)) + shelf + Spacing;
                var i0 = Math.Max(0, (int)Math.Floor((island.X - reach - originX) / Spacing));
                var i1 = Math.Min(nx - 1, (int)Math.Ceiling((island.X + reach - originX) / Spacing));
                var j0 = Math.Max(0, (int)Math.Floor((island.Y - reach - originY) / Spacing));
                var j1 = Math.Min(ny - 1, (int)Math.Ceiling((island.Y + reach - originY) / Spacing));
                for (var j = j0; j <= j1; j++)
                    for (var i = i0; i <= i1; i++)
                    {
                        var d = LandShape.SignedDistance(island, originX + i * Spacing, originY + j * Spacing);
                        var here = d < 0 ? 0 : Seabed.DepthAt(d);
                        var index = j * nx + i;
                        if (here < depth[index]) depth[index] = (float)here;
                    }
            }
        }

        /// <summary>Depth at a point, metres (0 on land).</summary>
        public double At(double x, double y)
        {
            var fx = (x - originX) / Spacing;
            var fy = (y - originY) / Spacing;
            if (fx < 0 || fy < 0 || fx >= nx - 1 || fy >= ny - 1) return open;
            var i = (int)fx;
            var j = (int)fy;
            var wx = fx - i;
            var wy = fy - j;
            var n = j * nx + i;
            return (1 - wy) * ((1 - wx) * depth[n] + wx * depth[n + 1]) + wy * ((1 - wx) * depth[n + nx] + wx * depth[n + nx + 1]);
        }
    }

    /// <summary>The sea surface at a point: height, slope, orbital velocity and the local sea state.</summary>
    public struct WaterSample
    {
        public double Height, SlopeX, SlopeY;
        public double VelocityX, VelocityY, VelocityZ;
        public double SignificantHeight, Depth;

        /// <summary>Unit normal of the surface (simulation plane X, Y and up).</summary>
        public void Normal(out double nx, out double ny, out double nz)
        {
            var length = Math.Sqrt(SlopeX * SlopeX + SlopeY * SlopeY + 1);
            nx = -SlopeX / length;
            ny = -SlopeY / length;
            nz = 1 / length;
        }
    }

    /// <summary>
    /// Phase-resolved sea surface from the wave field: one linear (Airy) component per resolved frequency up to about
    /// 0.42 Hz (wavelengths from about 8 m) and direction, 144 in all, with a fixed frequency and direction offset inside
    /// its bin and a fixed phase, all from the seed: the same everywhere, so heights and phases have no seams. Each
    /// component's amplitude is √(2E) of its bin, interpolated between cell centres in space and between the last two
    /// steps in time; near a shore the height is capped at 0.55 of the local depth (depth-limited waves). Phases advance
    /// with simulation time, never with the weather's acceleration. Dispersion is taken at 13 m: phase speeds do not slow
    /// on the narrow shelves, whose effect the wave field carries in the energy. Shorter waves are drawn by HDRP only.
    /// </summary>
    public sealed class SeaSurface
    {
        public const int SurfaceFrequencies = 9;
        public const int Components = SurfaceFrequencies * WaveSpectrum.Directions;
        public const double ReferenceDepth = 13;
        public const double DepthLimitedRatio = 0.55;

        /// <summary>
        /// Along a hull, components weaker than 5% of the strongest one are left out (each carries under 0.25% of its
        /// energy): about a quarter of them remain, and the hull's motion keeps its character at a quarter of the cost.
        /// </summary>
        public const double LineFloor = 0.05;

        public readonly WaveField Waves;
        public readonly Bathymetry Depths;
        public readonly double[] WaveNumberX = new double[Components], WaveNumberY = new double[Components];
        public readonly double[] WaveNumber = new double[Components], Omega = new double[Components], Phase = new double[Components];

        readonly double[] amplitude = new double[Components];
        readonly double[] energy = new double[Components];

        public SeaSurface(WaveField waves, ulong seed, Bathymetry depths)
        {
            Waves = waves;
            Depths = depths;
            for (var i = 0; i < SurfaceFrequencies; i++)
                for (var j = 0; j < WaveSpectrum.Directions; j++)
                {
                    var c = i * WaveSpectrum.Directions + j;
                    var u = MarineHash.Unit(seed, MarineHash.WaveJitter, (ulong)(2 * c));
                    var v = MarineHash.Unit(seed, MarineHash.WaveJitter, (ulong)(2 * c + 1));
                    var f = WaveSpectrum.Frequency[i] * Math.Pow(WaveSpectrum.Ratio, u - 0.5);
                    var theta = WaveSpectrum.DirectionAngle[j] + WaveSpectrum.DirectionWidth * (v - 0.5);
                    var k = WaveSpectrum.WaveNumber(f, ReferenceDepth);
                    WaveNumber[c] = k;
                    WaveNumberX[c] = k * Math.Cos(theta);
                    WaveNumberY[c] = k * Math.Sin(theta);
                    Omega[c] = 2 * Math.PI * f;
                    Phase[c] = 2 * Math.PI * MarineHash.Unit(seed, MarineHash.WavePhases, (ulong)c);
                }
        }

        /// <summary>Weight of the current step against the previous one, at a fraction of the present tick.</summary>
        public double BlendWeight(double tickFraction = 0) => Math.Min(1, (Waves.Slice + tickFraction) / WaveField.TicksPerStep);

        /// <summary>
        /// Amplitude of each component at a point (m), capped where the water is shallow; returns the significant
        /// height of the components. Bilinear between cell centres, leaving out corners on land.
        /// </summary>
        public double Amplitudes(double x, double y, double blend, double[] into)
        {
            var grid = Waves.Grid;
            var fx = Math.Max(0, Math.Min(grid.NX - 1, (x - grid.OriginX) / grid.CellSize - 0.5));
            var fy = Math.Max(0, Math.Min(grid.NY - 1, (y - grid.OriginY) / grid.CellSize - 0.5));
            var i0 = (int)fx;
            var j0 = (int)fy;
            var i1 = Math.Min(grid.NX - 1, i0 + 1);
            var j1 = Math.Min(grid.NY - 1, j0 + 1);
            var wx = fx - i0;
            var wy = fy - j0;
            var c00 = j0 * grid.NX + i0;
            var c10 = j0 * grid.NX + i1;
            var c01 = j1 * grid.NX + i0;
            var c11 = j1 * grid.NX + i1;
            var w00 = grid.Land[c00] ? 0 : (1 - wx) * (1 - wy);
            var w10 = grid.Land[c10] ? 0 : wx * (1 - wy);
            var w01 = grid.Land[c01] ? 0 : (1 - wx) * wy;
            var w11 = grid.Land[c11] ? 0 : wx * wy;
            var sum = w00 + w10 + w01 + w11;
            if (sum <= 0)
            {
                Array.Clear(into, 0, Components);
                return 0;
            }
            w00 /= sum; w10 /= sum; w01 /= sum; w11 /= sum;
            double m0 = 0;
            var cells = Waves.Cells;
            var current = Waves.Energy;
            var previous = Waves.Previous;
            for (var c = 0; c < Components; c++)
            {
                var offset = c * cells;
                double e = 0;
                if (w00 > 0) e += w00 * (previous[offset + c00] + (current[offset + c00] - previous[offset + c00]) * blend);
                if (w10 > 0) e += w10 * (previous[offset + c10] + (current[offset + c10] - previous[offset + c10]) * blend);
                if (w01 > 0) e += w01 * (previous[offset + c01] + (current[offset + c01] - previous[offset + c01]) * blend);
                if (w11 > 0) e += w11 * (previous[offset + c11] + (current[offset + c11] - previous[offset + c11]) * blend);
                e = Math.Max(0, e);
                energy[c] = e;
                m0 += e;
            }
            var hs = 4 * Math.Sqrt(m0);
            var depth = Depths?.At(x, y) ?? ReferenceDepth;
            var cap = hs > 0 ? Math.Min(1, DepthLimitedRatio * depth / hs) : 1;
            for (var c = 0; c < Components; c++) into[c] = Math.Sqrt(2 * energy[c]) * cap;
            return hs * cap;
        }

        /// <summary>The surface at a point and time (seconds of simulation time).</summary>
        public WaterSample Sample(double x, double y, double time, double blend)
        {
            var sample = new WaterSample();
            sample.SignificantHeight = Amplitudes(x, y, blend, amplitude);
            sample.Depth = Depths?.At(x, y) ?? ReferenceDepth;
            for (var c = 0; c < Components; c++)
            {
                var a = amplitude[c];
                if (a <= 0) continue;
                var theta = WaveNumberX[c] * x + WaveNumberY[c] * y - Omega[c] * time + Phase[c];
                var cos = Math.Cos(theta);
                var sin = Math.Sin(theta);
                var k = WaveNumber[c];
                sample.Height += a * cos;
                sample.SlopeX -= a * WaveNumberX[c] * sin;
                sample.SlopeY -= a * WaveNumberY[c] * sin;
                // Airy at the surface: u = a·ω·coth(kh)·cos θ along k̂, w = a·ω·sin θ.
                var horizontal = a * Omega[c] / Math.Tanh(Math.Min(20, k * ReferenceDepth)) * cos / k;
                sample.VelocityX += horizontal * WaveNumberX[c];
                sample.VelocityY += horizontal * WaveNumberY[c];
                sample.VelocityZ += a * Omega[c] * sin;
            }
            return sample;
        }

        /// <summary>
        /// Effective elevation and its vertical velocity at <paramref name="count"/> stations along a line from (x0, y0)
        /// stepping (dx, dy), for a hull of the given draft: the pressure of each component felt over the draft
        /// (Smith effect, (1 − e^(−kT))/(kT)) and averaged over the station spacing (sinc). One amplitude set for the
        /// whole line (taken at its middle) and a complex recurrence along it: one sine and cosine per component.
        /// </summary>
        // Smith factors of the components for the drafts asked for lately: a hull keeps its draft until its load moves.
        readonly System.Collections.Generic.Dictionary<double, double[]> smithFactors = new System.Collections.Generic.Dictionary<double, double[]>();

        double[] SmithFactors(double draft)
        {
            if (smithFactors.TryGetValue(draft, out var factors)) return factors;
            if (smithFactors.Count >= 64) smithFactors.Clear();
            factors = new double[Components];
            for (var c = 0; c < Components; c++)
            {
                var k = WaveNumber[c];
                factors[c] = draft > 0 ? (1 - Math.Exp(-k * draft)) / (k * draft) : 1;
            }
            smithFactors[draft] = factors;
            return factors;
        }

        /// <summary>
        /// Both sides of a hull at once: SampleLine along two parallel lines from (portX, portY) and (starboardX, starboardY)
        /// with the same step, the amplitudes taken at the port line's middle. The same numbers as two SampleLine calls (the
        /// second with sameAmplitudes), for one sinc filter, one step rotation and one Smith factor per component.
        /// </summary>
        public void SampleHull(double portX, double portY, double starboardX, double starboardY, double dx, double dy, int count,
            double time, double blend, double draft, double[] portHeight, double[] portRate, double[] starboardHeight, double[] starboardRate)
        {
            Amplitudes(portX + dx * (count - 1) / 2, portY + dy * (count - 1) / 2, blend, amplitude);
            Array.Clear(portHeight, 0, count);
            Array.Clear(portRate, 0, count);
            Array.Clear(starboardHeight, 0, count);
            Array.Clear(starboardRate, 0, count);
            double largest = 0;
            for (var c = 0; c < Components; c++) largest = Math.Max(largest, amplitude[c]);
            if (largest <= 0) return;
            var floor = LineFloor * largest;
            var smith = draft > 0 ? SmithFactors(draft) : null;
            for (var c = 0; c < Components; c++)
            {
                var a = amplitude[c];
                if (a < floor) continue;
                var step = WaveNumberX[c] * dx + WaveNumberY[c] * dy;
                var filter = Math.Abs(step) > 1e-9 ? Math.Sin(step / 2) / (step / 2) : 1;
                var effective = a * (smith != null ? smith[c] : 1) * filter;
                var stepCos = Math.Cos(step);
                var stepSin = Math.Sin(step);
                var omega = Omega[c];
                // Phases written as SampleLine writes them, so the sums round alike.
                Accumulate(WaveNumberX[c] * portX + WaveNumberY[c] * portY - Omega[c] * time + Phase[c], effective, omega, stepCos, stepSin, count, portHeight, portRate);
                Accumulate(WaveNumberX[c] * starboardX + WaveNumberY[c] * starboardY - Omega[c] * time + Phase[c], effective, omega, stepCos, stepSin, count, starboardHeight, starboardRate);
            }
        }

        static void Accumulate(double theta, double effective, double omega, double stepCos, double stepSin, int count, double[] height, double[] verticalVelocity)
        {
            var cos = Math.Cos(theta);
            var sin = Math.Sin(theta);
            for (var n = 0; n < count; n++)
            {
                height[n] += effective * cos;
                verticalVelocity[n] += effective * omega * sin;
                var nextCos = cos * stepCos - sin * stepSin;
                sin = sin * stepCos + cos * stepSin;
                cos = nextCos;
            }
        }

        public void SampleLine(double x0, double y0, double dx, double dy, int count, double time, double blend, double draft,
            double[] height, double[] verticalVelocity, double floorShare = LineFloor, bool sameAmplitudes = false)
        {
            if (!sameAmplitudes) Amplitudes(x0 + dx * (count - 1) / 2, y0 + dy * (count - 1) / 2, blend, amplitude);
            Array.Clear(height, 0, count);
            Array.Clear(verticalVelocity, 0, count);
            double largest = 0;
            for (var c = 0; c < Components; c++) largest = Math.Max(largest, amplitude[c]);
            if (largest <= 0) return;
            var floor = floorShare * largest;
            for (var c = 0; c < Components; c++)
            {
                var a = amplitude[c];
                if (a < floor) continue;
                var k = WaveNumber[c];
                var smith = draft > 0 ? (1 - Math.Exp(-k * draft)) / (k * draft) : 1;
                var step = WaveNumberX[c] * dx + WaveNumberY[c] * dy;
                var filter = Math.Abs(step) > 1e-9 ? Math.Sin(step / 2) / (step / 2) : 1;
                var effective = a * smith * filter;
                var theta = WaveNumberX[c] * x0 + WaveNumberY[c] * y0 - Omega[c] * time + Phase[c];
                var cos = Math.Cos(theta);
                var sin = Math.Sin(theta);
                var stepCos = Math.Cos(step);
                var stepSin = Math.Sin(step);
                var omega = Omega[c];
                for (var n = 0; n < count; n++)
                {
                    height[n] += effective * cos;
                    verticalVelocity[n] += effective * omega * sin;
                    var nextCos = cos * stepCos - sin * stepSin;
                    sin = sin * stepCos + cos * stepSin;
                    cos = nextCos;
                }
            }
        }
    }
}
