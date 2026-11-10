using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Spectral bins of the wave model and the laws it uses, each with its source:
    /// <list type="bullet">
    /// <item>linear dispersion ω² = g·k·tanh(k·h) (physical law);</item>
    /// <item>fetch- and duration-limited growth of the wind sea, JONSWAP (Hasselmann et al. 1973): g²E/U⁴ = 1.6·10⁻⁷·χ,
    /// f_p·U/g = 3.5·χ^−0.33, χ = gX/U²; full development at f_p·U/g = 0.13 (Pierson–Moskowitz 1964);</item>
    /// <item>spectral shape JONSWAP with γ = 7·χ^−0.142 limited to 1.5–3.5 (Mazzaretto et al. 2022 for the trend), finite depth by
    /// the TMA factor (Bouws et al. 1985);</item>
    /// <item>directional spreading cos^2s(Δθ/2), s = 10 (Goda 2000, wind waves; Mitsuyasu et al. 1975);</item>
    /// <item>deep-water breaking probability of the dominant waves (Banner, Babanin and Young 2000);</item>
    /// <item>depth-induced breaking (Battjes and Janssen 1978) with the breaker index of Ruessink et al. 2003;</item>
    /// <item>whitecap coverage by wind speed (Monahan and O'Muircheartaigh 1980).</item>
    /// </list>
    /// Twelve resolved frequencies from 0.08 to 0.79 Hz (bins 23% wide) and one lumped tail bin to 2.5 Hz, sixteen
    /// directions. Directions are where the waves travel toward, radians from +X toward +Y.
    /// </summary>
    public static class WaveSpectrum
    {
        public const int Resolved = 12;
        public const int Frequencies = Resolved + 1;
        public const int Tail = Resolved;
        public const int Directions = 16;
        public const int Bins = Frequencies * Directions;
        public const double LowestFrequency = 0.08, HighestResolved = 0.79, TailTop = 2.5;

        /// <summary>A depth at or beyond which no bin feels the bottom.</summary>
        public const double Deep = 1e4;

        // Growth laws.
        public const double FetchEnergy = 1.6e-7;
        public const double PeakCoefficient = 3.5, PeakExponent = -0.33;
        public const double FullyDevelopedPeak = 0.13;

        /// <summary>Components slower than 1.2·U·cos(Δθ) take energy from the wind (c_p = 1.2·U is full development).</summary>
        public const double WindSeaLimit = 1.2;

        /// <summary>Lower bound of the dimensionless fetch, so growth starts from calm water.</summary>
        public const double MinimumFetch = 10;

        /// <summary>
        /// κ: mean along-wind speed of the wind-sea energy over c_g(f_p). Growing a uniform sea with
        /// S = (4/3)·κ·c_g(f_p)·1.6·10⁻⁷·U²/g gives gt/U = (49.2/κ)·χ^0.67; the SPM/CEM duration law gt/U = 68.8·χ^(2/3),
        /// derived from the same fetch law, implies κ = 0.715. (The model's own spectrum, cos^20(Δθ/2) spread and JONSWAP
        /// shape, moves its energy at 0.76–0.84·c_g(f_p), so steady fetch growth lies a few per cent under the law.)
        /// </summary>
        public const double TransportFactor = 49.2 / 68.8;

        public const double SpreadingPower = 10;

        public static readonly double Ratio = Math.Pow(HighestResolved / LowestFrequency, 1.0 / (Resolved - 1));
        public static readonly double[] Frequency = new double[Frequencies];
        public static readonly double[] Low = new double[Frequencies];
        public static readonly double[] High = new double[Frequencies];
        public static readonly double[] Width = new double[Frequencies];
        public static readonly double DirectionWidth = 2 * Math.PI / Directions;
        public static readonly double[] DirectionAngle = new double[Directions];
        public static readonly double[] DirectionCos = new double[Directions];
        public static readonly double[] DirectionSin = new double[Directions];

        /// <summary>χ at full development: f_p from the fetch law equals 0.13·g/U.</summary>
        public static readonly double FullyDevelopedFetch = Math.Pow(PeakCoefficient / FullyDevelopedPeak, -1 / PeakExponent);

        const int TableFp = 360;
        const double TableFpMin = 0.03, TableFpMax = 5.0;
        static readonly double[] Gammas = { 1.5, 2.0, 2.5, 3.0, 3.5 };
        static readonly double tableLogMin = Math.Log(TableFpMin);
        static readonly double tableLogStep = Math.Log(TableFpMax / TableFpMin) / (TableFp - 1);
        static readonly double[] shapeTable = new double[TableFp * 5 * Frequencies];

        const int SpreadSamples = 4096;
        static readonly double[] spreadCumulative = new double[SpreadSamples + 1];

        static WaveSpectrum()
        {
            var half = Math.Sqrt(Ratio);
            for (var i = 0; i < Resolved; i++)
            {
                Frequency[i] = LowestFrequency * Math.Pow(Ratio, i);
                Low[i] = Frequency[i] / half;
                High[i] = Frequency[i] * half;
            }
            Low[Tail] = High[Resolved - 1];
            High[Tail] = TailTop;
            // Tail bin: the frequency with the bin's mean period under an f⁻⁵ tail, ⟨1/f⟩⁻¹.
            double a = Low[Tail], b = High[Tail];
            Frequency[Tail] = 1.25 * (Math.Pow(a, -4) - Math.Pow(b, -4)) / (Math.Pow(a, -5) - Math.Pow(b, -5));
            for (var i = 0; i < Frequencies; i++) Width[i] = High[i] - Low[i];
            for (var j = 0; j < Directions; j++)
            {
                DirectionAngle[j] = j * DirectionWidth;
                DirectionCos[j] = Math.Abs(Math.Cos(DirectionAngle[j])) < 1e-12 ? 0 : Math.Cos(DirectionAngle[j]);
                DirectionSin[j] = Math.Abs(Math.Sin(DirectionAngle[j])) < 1e-12 ? 0 : Math.Sin(DirectionAngle[j]);
            }

            for (var p = 0; p < TableFp; p++)
            {
                var fp = Math.Exp(tableLogMin + p * tableLogStep);
                for (var g = 0; g < Gammas.Length; g++)
                    for (var i = 0; i < Frequencies; i++)
                        shapeTable[(p * Gammas.Length + g) * Frequencies + i] = IntegrateJonswap(Low[i], High[i], fp, Gammas[g], i == Tail ? 48 : 16);
            }

            // cos^2s(δ/2) cumulated over δ from −π to π, normalised.
            double total = 0;
            spreadCumulative[0] = 0;
            for (var n = 0; n < SpreadSamples; n++)
            {
                var d0 = -Math.PI + 2 * Math.PI * n / SpreadSamples;
                var d1 = -Math.PI + 2 * Math.PI * (n + 1) / SpreadSamples;
                var mid = 0.5 * (d0 + d1);
                total += (d1 - d0) * (Spread(d0) + 4 * Spread(mid) + Spread(d1)) / 6;
                spreadCumulative[n + 1] = total;
            }
            for (var n = 0; n <= SpreadSamples; n++) spreadCumulative[n] /= total;
        }

        static double Spread(double delta) => Math.Pow(Math.Cos(delta / 2), 2 * SpreadingPower);

        /// <summary>JONSWAP shape f⁻⁵·exp(−5/4·(fp/f)⁴)·γ^r, scaled by fp⁴ so values are of order one.</summary>
        public static double Jonswap(double f, double fp, double gamma)
        {
            if (f <= 0) return 0;
            var ratio = fp / f;
            var r4 = ratio * ratio * ratio * ratio;
            var sigma = f <= fp ? 0.07 : 0.09;
            var d = (f - fp) / (sigma * fp);
            var peak = Math.Exp(-0.5 * d * d) * Math.Log(gamma);
            return Math.Pow(fp, 4) / Math.Pow(f, 5) * Math.Exp(-1.25 * r4 + peak);
        }

        static double IntegrateJonswap(double low, double high, double fp, double gamma, int intervals)
        {
            // Simpson in ln f: ∫ S df = ∫ S·f d(ln f).
            var a = Math.Log(low);
            var h = (Math.Log(high) - a) / intervals;
            double sum = 0;
            for (var n = 0; n <= intervals; n++)
            {
                var f = Math.Exp(a + n * h);
                var weight = n == 0 || n == intervals ? 1 : (n % 2 == 1 ? 4 : 2);
                sum += weight * Jonswap(f, fp, gamma) * f;
            }
            return sum * h / 3;
        }

        /// <summary>Energy of the JONSWAP shape in each frequency bin (unnormalised), from a table in fp and γ.</summary>
        public static void BinShape(double fp, double gamma, double[] into)
        {
            var t = (Math.Log(Math.Max(TableFpMin, Math.Min(TableFpMax, fp))) - tableLogMin) / tableLogStep;
            var p0 = Math.Min(TableFp - 2, Math.Max(0, (int)t));
            var wp = Math.Max(0, Math.Min(1, t - p0));
            var gt = (Math.Max(1.5, Math.Min(3.5, gamma)) - 1.5) / 0.5;
            var g0 = Math.Min(Gammas.Length - 2, Math.Max(0, (int)gt));
            var wg = Math.Max(0, Math.Min(1, gt - g0));
            for (var i = 0; i < Frequencies; i++)
            {
                var v00 = shapeTable[(p0 * Gammas.Length + g0) * Frequencies + i];
                var v01 = shapeTable[(p0 * Gammas.Length + g0 + 1) * Frequencies + i];
                var v10 = shapeTable[((p0 + 1) * Gammas.Length + g0) * Frequencies + i];
                var v11 = shapeTable[((p0 + 1) * Gammas.Length + g0 + 1) * Frequencies + i];
                into[i] = (1 - wp) * ((1 - wg) * v00 + wg * v01) + wp * ((1 - wg) * v10 + wg * v11);
            }
        }

        /// <summary>Share of cos^2s(Δθ/2) spreading about the wind (toward angle) that falls in each direction bin; sums to one.</summary>
        public static void DirectionShares(double windAngle, double[] into)
        {
            for (var j = 0; j < Directions; j++)
            {
                var lo = Wrap(DirectionAngle[j] - windAngle - DirectionWidth / 2);
                var hi = lo + DirectionWidth;
                into[j] = hi <= Math.PI ? Cumulative(hi) - Cumulative(lo) : (1 - Cumulative(lo)) + Cumulative(hi - 2 * Math.PI);
            }
        }

        static double Cumulative(double delta)
        {
            var t = (delta + Math.PI) / (2 * Math.PI) * SpreadSamples;
            var n = Math.Max(0, Math.Min(SpreadSamples - 1, (int)t));
            var w = Math.Max(0, Math.Min(1, t - n));
            return spreadCumulative[n] + (spreadCumulative[n + 1] - spreadCumulative[n]) * w;
        }

        /// <summary>Angle in [−π, π) (for angles within a few turns; no floating-point remainder).</summary>
        public static double Wrap(double angle)
        {
            while (angle >= Math.PI) angle -= 2 * Math.PI;
            while (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        // Dispersion ----------------------------------------------------------------------------------------------

        /// <summary>Wavenumber from ω² = g·k·tanh(k·h): Guo (2002) start and three Newton steps.</summary>
        public static double WaveNumber(double frequency, double depth)
        {
            var g = MarineUnits.Gravity;
            var omega = 2 * Math.PI * frequency;
            var deep = omega * omega / g;
            if (depth >= Deep || deep * depth > 12) return deep;
            var x = omega * Math.Sqrt(depth / g);
            var kh = x * x * Math.Pow(1 - Math.Exp(-Math.Pow(x, 2.4908)), -1 / 2.4908);
            var k = kh / depth;
            for (var n = 0; n < 3; n++)
            {
                var t = Math.Tanh(k * depth);
                var residual = g * k * t - omega * omega;
                var slope = g * t + g * k * depth * (1 - t * t);
                k -= residual / slope;
            }
            return k;
        }

        public static double PhaseSpeed(double frequency, double depth) => 2 * Math.PI * frequency / WaveNumber(frequency, depth);

        public static double GroupSpeed(double frequency, double depth)
        {
            var k = WaveNumber(frequency, depth);
            var kh = depth >= Deep ? 100 : k * depth;
            var n = kh > 12 ? 0.5 : 0.5 * (1 + 2 * kh / Math.Sinh(2 * kh));
            return 2 * Math.PI * frequency / k * n;
        }

        /// <summary>Kitaigorodskii depth factor of the TMA spectrum (Bouws et al. 1985).</summary>
        public static double TmaFactor(double frequency, double depth)
        {
            if (depth >= Deep) return 1;
            var wh = 2 * Math.PI * frequency * Math.Sqrt(depth / MarineUnits.Gravity);
            if (wh <= 1) return 0.5 * wh * wh;
            if (wh <= 2) return 1 - 0.5 * (2 - wh) * (2 - wh);
            return 1;
        }

        // Growth laws -------------------------------------------------------------------------------------------

        /// <summary>Dimensionless fetch χ = gX/U² equivalent to a wind-sea energy: g²E/U⁴ = 1.6·10⁻⁷·χ.</summary>
        public static double EquivalentFetch(double energy, double wind)
        {
            var u2 = wind * wind;
            return Math.Max(MinimumFetch, MarineUnits.Gravity * MarineUnits.Gravity * energy / (FetchEnergy * u2 * u2));
        }

        public static double FetchLimitedEnergy(double fetch, double wind) => FetchEnergy * wind * wind * fetch / MarineUnits.Gravity;

        public static double FullyDevelopedEnergy(double wind)
        {
            var u2 = wind * wind;
            return FetchEnergy * FullyDevelopedFetch * u2 * u2 / (MarineUnits.Gravity * MarineUnits.Gravity);
        }

        public static double PeakFrequency(double chi, double wind) =>
            Math.Max(PeakCoefficient * Math.Pow(chi, PeakExponent), FullyDevelopedPeak) * MarineUnits.Gravity / Math.Max(wind, 0.1);

        public static double PeakEnhancement(double chi) => Math.Max(1.5, Math.Min(3.5, 7.0 * Math.Pow(chi, -0.142)));

        // Breaking and foam ----------------------------------------------------------------------------------------

        /// <summary>Breaking probability of the dominant waves by their significant steepness ε = H_p·k_p/2 (Banner et al. 2000).</summary>
        public static double BreakingProbability(double steepness) =>
            steepness <= 0.055 ? 0 : Math.Min(1, 22 * Math.Pow(steepness - 0.055, 2.01));

        /// <summary>Whitecap coverage, fraction of the surface (Monahan and O'Muircheartaigh 1980).</summary>
        public static double WhitecapCoverage(double wind) => wind <= 0 ? 0 : Math.Min(1, 3.84e-6 * Math.Pow(wind, 3.41));

        /// <summary>Breaker index γ = 0.29 + 0.76·k·h (Ruessink et al. 2003).</summary>
        public static double BreakerIndex(double kh) => 0.29 + 0.76 * kh;

        /// <summary>Largest wave the depth allows (Battjes and Janssen 1978, with the Miche steepness limit).</summary>
        public static double MaximumHeight(double k, double depth)
        {
            if (depth >= Deep) return 0.88 / k;
            var gamma = BreakerIndex(k * depth);
            return 0.88 / k * Math.Tanh(gamma * k * depth / 0.88);
        }

        /// <summary>Fraction of breaking waves Q_b from (1 − Q_b)/ln Q_b = −(H_rms/H_max)² (Battjes and Janssen 1978).</summary>
        public static double BreakingFraction(double hrms, double hmax)
        {
            if (hmax <= 0) return 1;
            var b = hrms / hmax;
            var x = b * b;
            if (x >= 1) return 1;
            if (x < 0.05) return 0;
            double lo = 1e-12, hi = 1 - 1e-12;
            for (var n = 0; n < 60; n++)
            {
                var mid = 0.5 * (lo + hi);
                if (1 - mid + x * Math.Log(mid) < 0) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }
    }
}
