using System;
using System.Collections.Generic;

namespace MorzaBogow.Simulation
{
    /// <summary>Wind at one point: U10 with and without gusts, its turbulence, sheltering and the surface stress it gives.</summary>
    public struct WindSample
    {
        /// <summary>U10 including gusts, m/s, simulation plane.</summary>
        public double X, Y;

        /// <summary>U10 without gusts (synoptic × mesoscale cells × islands): what drives the waves.</summary>
        public double MeanX, MeanY;

        public double GustX, GustY;

        /// <summary>Local turbulence intensity σ_u / U.</summary>
        public double Turbulence;

        /// <summary>1 in open water, lower in an island's lee (1 − wake deficit).</summary>
        public double Shelter;

        /// <summary>Relative strength of the mesoscale cells here (0 none, +0.15 a stronger area).</summary>
        public double Cell;

        /// <summary>Friction velocity and roughness length of the mean wind (COARE 3.5), m/s and m.</summary>
        public double UStar, Z0;

        public double Speed => Math.Sqrt(X * X + Y * Y);
        public double MeanSpeed => Math.Sqrt(MeanX * MeanX + MeanY * MeanY);

        /// <summary>Direction the air moves toward, radians from +X toward +Y.</summary>
        public double Direction => Math.Atan2(Y, X);
        public double MeanDirection => Math.Atan2(MeanY, MeanX);

        /// <summary>Wind at height z above the water from the logarithmic profile of the mean wind (neutral layer, z ≈ 2–50 m).</summary>
        public double SpeedAt(double z) => Speed * Math.Log(Math.Max(z, 0.5) / Z0) / Math.Log(MarineUnits.ReferenceHeight / Z0);
    }

    /// <summary>A mesoscale area of stronger or weaker wind carried by the wind, with a smooth sin² life.</summary>
    public sealed class WindCell
    {
        public double SpawnX, SpawnY, BornDriftX, BornDriftY;
        public double Radius, Amplitude, Born, Life;
    }

    /// <summary>
    /// Wind over the map, U(x, y, t) at 10 m, from four scales:
    /// <list type="bullet">
    /// <item>synoptic: the climate's mean wind and bearing with slow Ornstein–Uhlenbeck variability and the Etesian
    /// diurnal cycle;</item>
    /// <item>mesoscale cells of stronger and weaker wind that drift with the wind and live a few minutes;</item>
    /// <item>gusts: a kinematic simulation (Fung et al. 1992) of a von Kármán spectrum, carried by the mean wind
    /// (frozen turbulence) and decorrelating with each mode's own frequency; scales from 20 m to 10 integral lengths;</item>
    /// <item>islands: a wake with a deficit decaying over WakeLengthPerHeight island heights, blocking upwind, jets at the
    /// flanks, a potential-flow turn of the direction close to the shore and more turbulence in the lee.</item>
    /// </list>
    /// Coriolis is left out: at a 3 km map and minutes the Rossby number is about 37. Everything is deterministic from
    /// the seed and the saved state; the gust field is a function of position, time and the saved advection offset.
    /// </summary>
    public sealed class WindField : IMarineWind
    {
        public const int ModesPerComponent = 64;
        const double ShortestGust = 20;

        public readonly MarineClimate Climate;
        public readonly ulong Seed;

        // Saved state.
        public double Time;
        public double DirectionOffset;
        public double SpeedFactor = 1;
        public double DriftX, DriftY;
        public readonly List<WindCell> Cells = new List<WindCell>();
        public DeterministicRandom Random;

        // Derived from the seed and the climate (not saved).
        readonly double[] modeKx, modeKy, modePhase, modeOmega, modeAmplitude;
        readonly double[] lateralKx, lateralKy, lateralPhase, lateralOmega, lateralAmplitude;
        IReadOnlyList<Island> islands;
        int islandCount = -1;
        double[] islandX, islandY, islandRadius, islandHeight;

        public WindField(MarineClimate climate, ulong seed, IReadOnlyList<Island> islandList)
        {
            Climate = climate;
            Seed = seed;
            Random = new DeterministicRandom(MarineHash.Mix(seed, MarineHash.Process, 1));
            islands = islandList ?? Array.Empty<Island>();
            BuildModes(MarineHash.GustModes, out modeKx, out modeKy, out modePhase, out modeOmega, out modeAmplitude);
            BuildModes(MarineHash.GustLateral, out lateralKx, out lateralKy, out lateralPhase, out lateralOmega, out lateralAmplitude);
        }

        /// <summary>
        /// Von Kármán constant of the longitudinal spectrum: Φ_u(k₁) = (2σ²L/π)/(1 + (1.339·L·k₁)²)^(5/6).
        /// </summary>
        public const double VonKarmanScale = 1.339;

        /// <summary>Shortest and longest resolved gust wavenumbers for an integral length scale, rad/m.</summary>
        public static double LowestWavenumber(double integralLength) => 2 * Math.PI / (10 * integralLength);
        public static double HighestWavenumber => 2 * Math.PI / ShortestGust;

        /// <summary>
        /// Variance of a unit-variance gust field held by wavenumbers below k (two-dimensional isotropic Matérn field,
        /// ν = 1/3, ℓ = 1.339·L): 1 − (1 + (kℓ)²)^(−1/3). Along any line this field has exactly the von Kármán
        /// longitudinal spectrum with integral length L, which is why it is used for the horizontal gust pattern.
        /// </summary>
        public static double VarianceBelow(double k, double integralLength)
        {
            var kl = k * VonKarmanScale * integralLength;
            return 1 - Math.Pow(1 + kl * kl, -1.0 / 3.0);
        }

        /// <summary>Radial energy density of the unit-variance field, E(k) = (2ℓ²/3)·k·(1 + (kℓ)²)^(−4/3).</summary>
        public static double RadialSpectrum(double k, double integralLength)
        {
            var l = VonKarmanScale * integralLength;
            return 2 * l * l / 3 * k * Math.Pow(1 + k * l * k * l, -4.0 / 3.0);
        }

        /// <summary>Fraction of the gust variance the modes resolve: scales from 20 m to ten integral lengths.</summary>
        public double ResolvedVariance => VarianceBelow(HighestWavenumber, Climate.GustLength) - VarianceBelow(LowestWavenumber(Climate.GustLength), Climate.GustLength);

        /// <summary>Wavenumber magnitude and squared amplitude of each longitudinal mode, for tests of the spectrum.</summary>
        public double ModeWavenumber(int m) => Math.Sqrt(modeKx[m] * modeKx[m] + modeKy[m] * modeKy[m]);
        public double ModeVariance(int m) => modeAmplitude[m] * modeAmplitude[m] / 2;

        void BuildModes(ulong stream, out double[] kx, out double[] ky, out double[] phase, out double[] omega, out double[] amplitude)
        {
            var n = ModesPerComponent;
            kx = new double[n]; ky = new double[n]; phase = new double[n]; omega = new double[n]; amplitude = new double[n];
            var length = Climate.GustLength;
            var kMin = LowestWavenumber(length);
            var kMax = HighestWavenumber;
            var ratio = Math.Pow(kMax / kMin, 1.0 / n);
            // Eddy turnover with the match's nominal σ (fixed per match, so the field stays a function of time).
            var nominal = Climate.TurbulenceIntensity(Climate.MeanWind) * Math.Max(Climate.MeanWind, 1.0);
            for (var m = 0; m < n; m++)
            {
                var low = kMin * Math.Pow(ratio, m);
                var high = low * ratio;
                var k = low * Math.Pow(ratio, MarineHash.Unit(Seed, stream, (ulong)(m * 4)));
                var angle = 2 * Math.PI * MarineHash.Unit(Seed, stream, (ulong)(m * 4 + 1));
                kx[m] = k * Math.Cos(angle);
                ky[m] = k * Math.Sin(angle);
                phase[m] = 2 * Math.PI * MarineHash.Unit(Seed, stream, (ulong)(m * 4 + 2));
                // The band's share of the unit variance, exact for the Matérn spectrum: no renormalisation, so the
                // field holds what the spectrum puts between 20 m and ten integral lengths (about 75%).
                var energy = VarianceBelow(high, length) - VarianceBelow(low, length);
                amplitude[m] = Math.Sqrt(2 * energy);
                // Kinematic simulation (Fung et al. 1992): ω = λ·√(k³·E(k)), λ = 0.5.
                omega[m] = 0.5 * Math.Sqrt(k * k * k * RadialSpectrum(k, length) * nominal * nominal);
            }
        }

        // Synoptic wind -----------------------------------------------------------------------------------------

        public double Hour => Climate.StartHour + Time / 3600.0;

        public double SynopticSpeed
        {
            get
            {
                var diurnal = 1 + Climate.DiurnalAmplitude * Math.Cos(2 * Math.PI * (Hour - Climate.DiurnalPeakHour) / 24.0);
                return Math.Max(0, Math.Min(Climate.MaxMeanWind, Climate.MeanWind * diurnal * SpeedFactor));
            }
        }

        public double SynopticAngle => Climate.TowardAngle + DirectionOffset;

        /// <summary>Advance by dt seconds of weather time: slow variability (exact OU steps), advection offset, cells.</summary>
        public void Advance(double dt)
        {
            if (dt <= 0) return;
            Time += dt;
            var sigmaDirection = Climate.DirectionSigmaDegrees * Math.PI / 180;
            var decay = Math.Exp(-dt / Climate.DirectionTau);
            DirectionOffset = DirectionOffset * decay + sigmaDirection * Math.Sqrt(1 - decay * decay) * MarineHash.Normal(ref Random);
            var speedDecay = Math.Exp(-dt / Climate.SpeedTau);
            SpeedFactor = 1 + (SpeedFactor - 1) * speedDecay + Climate.SpeedSigma * Math.Sqrt(1 - speedDecay * speedDecay) * MarineHash.Normal(ref Random);
            var speed = SynopticSpeed;
            // Below about 3 m/s Taylor's hypothesis fails: the gusts are not carried, only evolve.
            if (speed >= 3)
            {
                DriftX += Math.Cos(SynopticAngle) * speed * dt;
                DriftY += Math.Sin(SynopticAngle) * speed * dt;
            }
            for (var i = Cells.Count - 1; i >= 0; i--)
                if (Time - Cells[i].Born > Cells[i].Life) Cells.RemoveAt(i);
            if (Random.NextDouble() < Climate.CellsPerHour / 3600.0 * dt)
            {
                var half = 1300 + Climate.CellRadiusMax;
                Cells.Add(new WindCell
                {
                    SpawnX = -half + 2 * half * Random.NextDouble(),
                    SpawnY = -half + 2 * half * Random.NextDouble(),
                    BornDriftX = DriftX,
                    BornDriftY = DriftY,
                    Radius = Climate.CellRadiusMin + (Climate.CellRadiusMax - Climate.CellRadiusMin) * Random.NextDouble(),
                    Amplitude = Climate.CellAmplitude * (Random.NextDouble() < 0.5 ? -1 : 1) * (0.5 + 0.5 * Random.NextDouble()),
                    Born = Time,
                    Life = Climate.CellLifeMin + (Climate.CellLifeMax - Climate.CellLifeMin) * Random.NextDouble(),
                });
            }
        }

        // Spatial structure ------------------------------------------------------------------------------------

        void CacheIslands()
        {
            if (islands.Count == islandCount) return;
            islandCount = islands.Count;
            islandX = new double[islandCount]; islandY = new double[islandCount]; islandRadius = new double[islandCount]; islandHeight = new double[islandCount];
            for (var i = 0; i < islandCount; i++)
            {
                var island = islands[i];
                islandX[i] = island.X;
                islandY[i] = island.Y;
                double mean = island.Radius;
                if (island.Outline != null && island.Outline.Length > 0)
                {
                    mean = 0;
                    for (var j = 0; j < island.Outline.Length; j++) mean += island.Outline[j];
                    mean /= island.Outline.Length;
                }
                islandRadius[i] = Math.Max(1, mean);
                islandHeight[i] = IslandRelief.Height(island);
            }
            BuildBuckets();
        }

        // Islands whose influence reaches each 100 m square, so a point checks only those.
        const double BucketSize = 100;
        double bucketX0, bucketY0;
        int bucketsX, bucketsY;
        int[][] buckets;

        double Reach(int i)
        {
            var r = islandRadius[i];
            return r + Math.Max(3 * Climate.WakeLengthPerHeight * islandHeight[i], 4 * r);
        }

        void BuildBuckets()
        {
            buckets = null;
            if (islandCount == 0) return;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (var i = 0; i < islandCount; i++)
            {
                var reach = Reach(i);
                x0 = Math.Min(x0, islandX[i] - reach); x1 = Math.Max(x1, islandX[i] + reach);
                y0 = Math.Min(y0, islandY[i] - reach); y1 = Math.Max(y1, islandY[i] + reach);
            }
            bucketX0 = x0;
            bucketY0 = y0;
            bucketsX = (int)Math.Ceiling((x1 - x0) / BucketSize) + 1;
            bucketsY = (int)Math.Ceiling((y1 - y0) / BucketSize) + 1;
            var lists = new List<int>[bucketsX * bucketsY];
            for (var i = 0; i < islandCount; i++)
            {
                var reach = Reach(i);
                var i0 = (int)((islandX[i] - reach - x0) / BucketSize);
                var i1 = (int)((islandX[i] + reach - x0) / BucketSize);
                var j0 = (int)((islandY[i] - reach - y0) / BucketSize);
                var j1 = (int)((islandY[i] + reach - y0) / BucketSize);
                for (var j = j0; j <= j1; j++)
                    for (var k = i0; k <= i1; k++)
                    {
                        // Keep the island only where its reach circle can touch the square.
                        var cx = Math.Max(x0 + k * BucketSize, Math.Min(islandX[i], x0 + (k + 1) * BucketSize));
                        var cy = Math.Max(y0 + j * BucketSize, Math.Min(islandY[i], y0 + (j + 1) * BucketSize));
                        if ((cx - islandX[i]) * (cx - islandX[i]) + (cy - islandY[i]) * (cy - islandY[i]) > reach * reach) continue;
                        var index = j * bucketsX + k;
                        (lists[index] ??= new List<int>()).Add(i);
                    }
            }
            buckets = new int[lists.Length][];
            for (var n = 0; n < lists.Length; n++) buckets[n] = lists[n]?.ToArray() ?? Array.Empty<int>();
        }

        int[] IslandsNear(double x, double y)
        {
            if (buckets == null) return Array.Empty<int>();
            var k = (int)Math.Floor((x - bucketX0) / BucketSize);
            var j = (int)Math.Floor((y - bucketY0) / BucketSize);
            return k < 0 || j < 0 || k >= bucketsX || j >= bucketsY ? Array.Empty<int>() : buckets[j * bucketsX + k];
        }

        static double SmoothStep(double edge0, double edge1, double x)
        {
            var t = Math.Max(0, Math.Min(1, (x - edge0) / (edge1 - edge0)));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Mean (gust-free) wind at a point, plus the local turbulence multiplier, shelter and cell factor.</summary>
        public void MeanAt(double x, double y, out double meanX, out double meanY, out double turbulence, out double shelter, out double cell)
        {
            CacheIslands();
            var angle = SynopticAngle;
            var ex = Math.Cos(angle);
            var ey = Math.Sin(angle);

            cell = 0;
            for (var i = 0; i < Cells.Count; i++)
            {
                var c = Cells[i];
                var cx = c.SpawnX + (DriftX - c.BornDriftX);
                var cy = c.SpawnY + (DriftY - c.BornDriftY);
                var age = (Time - c.Born) / c.Life;
                var envelope = Math.Sin(Math.PI * Math.Max(0, Math.Min(1, age)));
                var d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                cell += c.Amplitude * envelope * envelope * Math.Exp(-d2 / (2 * c.Radius * c.Radius));
            }

            double keep = 1, jets = 1, extraTurbulence = 0, turn = 0;
            foreach (var i in IslandsNear(x, y))
            {
                var r = islandRadius[i];
                var h = islandHeight[i];
                var wake = Climate.WakeLengthPerHeight * h;
                var reach = r + Math.Max(3 * wake, 4 * r);
                var dx = x - islandX[i];
                var dy = y - islandY[i];
                var d2 = dx * dx + dy * dy;
                if (d2 > reach * reach) continue;
                var s = dx * ex + dy * ey;
                var n = -dx * ey + dy * ex;
                var across = Math.Abs(n);
                // Obstacles lower than the 10 m reference height shelter U10 in proportion [W].
                var strength = Math.Min(1, h / MarineUnits.ReferenceHeight);
                var deficit0 = Climate.WakeDeficit * strength;
                var downwind = SmoothStep(-r, r, s);
                // Each term is left out where its Gaussian is below e⁻⁹ (10⁻⁴).
                double lee = 0, blocking = 0;
                var width = r + 0.1 * Math.Max(0, s);
                if (s > -r && across < 3 * width)
                {
                    var q = n / width;
                    lee = deficit0 * Math.Exp(-Math.Max(0, s - r) / Math.Max(wake, 1) - q * q) * downwind;
                }
                if (s < r && across < 3 * r)
                {
                    var q = n / r;
                    blocking = 0.15 * deficit0 * Math.Exp(-Math.Max(0, -s - r) / (2 * r) - q * q) * (1 - downwind);
                }
                keep *= 1 - Math.Min(0.95, lee + blocking);
                if (Math.Abs(s) < 6 * r && across < 3 * r)
                {
                    var side = (across - 1.3 * r) / (0.5 * r);
                    var q = s / (2 * r);
                    jets *= 1 + Climate.FlankJet * strength * Math.Exp(-side * side - q * q);
                }
                extraTurbulence += Climate.WakeTurbulence * (lee / Math.Max(Climate.WakeDeficit, 1e-9));
                // Potential flow around a circle turns the wind near the shore; separation halves it [W].
                var distance = Math.Max(Math.Sqrt(d2), r);
                if (distance < 3 * r)
                {
                    var phi = Math.Atan2(n, s);
                    var q = r * r / (distance * distance);
                    var flowS = 1 - q * Math.Cos(2 * phi);
                    var flowN = -q * Math.Sin(2 * phi);
                    turn += 0.5 * strength * Math.Atan2(flowN, flowS) * Math.Exp(-Math.Pow(distance / (2.5 * r), 2));
                }
            }
            turn = Math.Max(-Math.PI / 4, Math.Min(Math.PI / 4, turn));
            var speed = SynopticSpeed * Math.Max(0.2, 1 + cell) * keep * jets;
            var direction = angle + turn;
            meanX = speed * Math.Cos(direction);
            meanY = speed * Math.Sin(direction);
            turbulence = 1 + Math.Min(Climate.WakeTurbulence, extraTurbulence);
            shelter = keep;
        }

        /// <summary>Unit-variance longitudinal and lateral gust fields at a point (frozen turbulence carried by the wind).</summary>
        public void GustAt(double x, double y, out double longitudinal, out double lateral)
        {
            var px = x - DriftX;
            var py = y - DriftY;
            double u = 0, v = 0;
            for (var m = 0; m < ModesPerComponent; m++)
            {
                u += modeAmplitude[m] * Math.Cos(modeKx[m] * px + modeKy[m] * py + modePhase[m] - modeOmega[m] * Time);
                v += lateralAmplitude[m] * Math.Cos(lateralKx[m] * px + lateralKy[m] * py + lateralPhase[m] - lateralOmega[m] * Time);
            }
            longitudinal = u;
            lateral = v;
        }

        public WindSample Sample(double x, double y, bool gusts = true)
        {
            MeanAt(x, y, out var meanX, out var meanY, out var turbulenceFactor, out var shelter, out var cell);
            var meanSpeed = Math.Sqrt(meanX * meanX + meanY * meanY);
            var sample = new WindSample { MeanX = meanX, MeanY = meanY, Shelter = shelter, Cell = cell };
            var intensity = Climate.TurbulenceIntensity(meanSpeed) * turbulenceFactor;
            sample.Turbulence = intensity;
            if (gusts && meanSpeed > 1e-6)
            {
                GustAt(x, y, out var u, out var v);
                var sigma = intensity * meanSpeed;
                var lateralRatio = meanSpeed < 3 ? 1.0 : Climate.LateralRatio;
                var ex = meanX / meanSpeed;
                var ey = meanY / meanSpeed;
                sample.GustX = sigma * (u * ex - lateralRatio * v * ey);
                sample.GustY = sigma * (u * ey + lateralRatio * v * ex);
            }
            sample.X = meanX + sample.GustX;
            sample.Y = meanY + sample.GustY;
            Roughness(meanSpeed, out sample.UStar, out sample.Z0);
            return sample;
        }

        public void MeanWind(double x, double y, out double ux, out double uy) => MeanAt(x, y, out ux, out uy, out _, out _, out _);

        public void GustyWind(double x, double y, out double ux, out double uy)
        {
            var sample = Sample(x, y);
            ux = sample.X;
            uy = sample.Y;
        }

        public void SynopticWind(out double ux, out double uy)
        {
            var speed = SynopticSpeed;
            ux = speed * Math.Cos(SynopticAngle);
            uy = speed * Math.Sin(SynopticAngle);
        }

        /// <summary>
        /// COARE 3.5 neutral roughness (Edson et al. 2013): Charnock α = 0.0017·U10 − 0.005 (fixed above 19 m/s),
        /// z0 = 0.11·ν_air/u* + max(α, 0)·u*²/g, u* = κ·U10/ln(10/z0). The max keeps light winds (α below zero under
        /// 2.94 m/s) on the smooth-flow branch instead of a negative roughness. Fixed iterations, so it is a pure function.
        /// </summary>
        public static void Roughness(double u10, out double uStar, out double z0)
        {
            var u = Math.Max(u10, 0.1);
            uStar = 0.036 * u;
            z0 = 1e-4;
            var charnock = Math.Max(0, 0.0017 * Math.Min(u, 19) - 0.005);
            for (var i = 0; i < 6; i++)
            {
                z0 = 0.11 * MarineUnits.AirViscosity / uStar + charnock * uStar * uStar / MarineUnits.Gravity;
                uStar = MarineUnits.VonKarman * u / Math.Log(MarineUnits.ReferenceHeight / z0);
            }
        }

        /// <summary>Neutral 10 m drag coefficient (u*/U10)².</summary>
        public static double DragCoefficient(double u10)
        {
            Roughness(u10, out var uStar, out _);
            var u = Math.Max(u10, 0.1);
            return uStar * uStar / (u * u);
        }
    }
}
