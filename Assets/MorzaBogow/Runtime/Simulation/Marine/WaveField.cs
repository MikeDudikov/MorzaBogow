using System;

namespace MorzaBogow.Simulation
{
    /// <summary>Wind the wave model reads: gust-free mean at a point, the same with gusts, and the open-sea wind around the map.</summary>
    public interface IMarineWind
    {
        void MeanWind(double x, double y, out double ux, out double uy);
        void GustyWind(double x, double y, out double ux, out double uy);
        void SynopticWind(out double ux, out double uy);
    }

    /// <summary>The same wind everywhere, for tests and tools; GustX/GustY are added to the gusty wind only.</summary>
    public sealed class UniformWind : IMarineWind
    {
        public double X, Y, GustX, GustY;

        public UniformWind(double x, double y) { X = x; Y = y; }

        public void MeanWind(double x, double y, out double ux, out double uy) { ux = X; uy = Y; }
        public void GustyWind(double x, double y, out double ux, out double uy) { ux = X + GustX; uy = Y + GustY; }
        public void SynopticWind(out double ux, out double uy) { ux = X; uy = Y; }
    }

    /// <summary>
    /// Energy of the waves over the map, E(x, y, f, θ) in m² per bin, and how it travels, grows and dies:
    /// <list type="bullet">
    /// <item>transport at the group velocity of each bin, ∂E/∂t + ∇·(c_g·E) = S, first-order upwind on 50 m cells, with
    /// sub-steps whenever the Courant number would pass 0.9; land in a cell lets through only its transparency;</item>
    /// <item>wind input written from the JONSWAP fetch law in terms of the local wind-sea energy, so the steady state
    /// along a fetch reproduces E = 1.6·10⁻⁷·U²·X/g and a uniform sea grows with the duration law; the wind sea is reshaped
    /// to JONSWAP·TMA after each step (in place of the four-wave interactions); full development caps it;</item>
    /// <item>sinks: bottom friction (JONSWAP, C_f = 0.038 m²/s³), whitecapping of swell (WAM cycle 3), decay of an
    /// unforced short-wave tail at the rate that balanced its wind input (Toba 1973, Plant 1982), depth-induced
    /// breaking (Battjes and Janssen 1978);</item>
    /// <item>the sea beyond the map edge is one more spectrum, grown by the open-sea wind over the outer fetch and fed
    /// in through the edges facing it;</item>
    /// <item>foam coverage carried by the wind drift and the Stokes drift, fed by whitecaps (Monahan) and surf.</item>
    /// </list>
    /// A step takes four seconds of weather time (the longest waves take two sub-steps to keep the Courant number
    /// under 0.9) and is spread over the 120 ticks of those seconds: ticks 1–16 carry one direction each and sample the
    /// wind, ticks 17–116 apply the sources cell by cell, tick 117 the outer sea and the gust response of the short
    /// waves, tick 120 makes the new state current. The mean wind of half the cells is sampled each step, so each
    /// cell's every eight seconds (it is filtered over two minutes). Each part is a pure function of the saved state and
    /// the saved wind samples, so a save in the middle of a step is restored by doing the parts again.
    /// </summary>
    public sealed class WaveField
    {
        public const int TicksPerStep = 120;
        public const int PropagationTicks = WaveSpectrum.Directions;
        public const int SourceTicks = 100;
        public const double WindFilterSeconds = 120;
        public const double FoamSeconds = 15;
        public const double MaxCourant = 0.9;
        public const double MinimumWind = 1.0;
        public const int TailBins = 3;
        public const int FirstTailBin = WaveSpectrum.Frequencies - TailBins;
        public const int CoarseFactor = 4;

        /// <summary>How much faster than the energy grows the wind sea's spectral shape adjusts to the growth law.</summary>
        public const double ShapeRate = 10;

        // WAM cycle 3 whitecapping (Komen et al. 1984; WAMDI 1988), JONSWAP bottom friction, Toba and Plant tail decay.
        const double WhitecapCoefficient = 2.36e-5, PiersonMoskowitzSteepness = 3.02e-3;
        const double BottomFriction = 0.038;
        const double TobaConstant = 0.062, PlantGrowth = 0.04;

        const int NF = WaveSpectrum.Frequencies, ND = WaveSpectrum.Directions, NB = WaveSpectrum.Bins;

        public readonly WaveGrid Grid;
        public readonly MarineClimate Climate;
        public readonly int Cells, CoarseX, CoarseY, CoarseCells;

        // Saved state.
        public float[] Energy;
        public float[] WindX, WindY;
        public float[] Foam;
        public float[] TailFactor;
        public readonly double[] Outer = new double[NB];
        public int Steps, Slice;
        public readonly float[] SampleX, SampleY;
        public readonly float[] GustSample;
        public double SynopticX, SynopticY;

        // Not saved: the previous step (for smooth surfaces) and the step being built.
        public float[] Previous;
        float[] work;
        float[] windXNext, windYNext, foamNext, tailNext;
        readonly double[] outerNext = new double[NB];
        float[] peak, peakNext, breaking, breakingNext, surf, surfNext;
        readonly float[] binMax = new float[NB];
        readonly float[] binMaxNext = new float[NB];
        readonly float[] scratchA, scratchB;

        // Physics by depth class.
        readonly int[] depthClass;
        readonly double[] classDepth;
        readonly double[] classK, classPhase, classGroup, classTma, classFriction;
        readonly double[] maxGroup = new double[NF];
        readonly double[] deepK = new double[NF], deepPhase = new double[NF], deepGroup = new double[NF];

        // Per-call buffers (the model is single-threaded).
        readonly double[] spectrum = new double[NB];
        readonly double[] shape = new double[NF];
        readonly double[] shares = new double[ND];
        readonly double[] windSea = new double[ND];
        readonly double[] cosDelta = new double[ND];
        readonly int[] starts = new int[ND];
        readonly double[] suffix = new double[NF], suffixF = new double[NF];
        readonly bool[] holds = new bool[ND];
        readonly double[] unforcedEnergy = new double[NF];
        readonly double[] fedFactor = new double[NF], freeFactor = new double[NF];
        readonly double[] frequencyEnergy = new double[NF];

        // Per bin: 2ωk·(cos θ, sin θ) for the Stokes drift, by depth class and for deep water; Toba's u* per unit energy.
        readonly double[] classStokes, deepStokes = new double[NB * 2];
        readonly double[] tobaLevel = new double[NF];

        public WaveField(WaveGrid grid, MarineClimate climate)
        {
            Grid = grid;
            Climate = climate;
            Cells = grid.Cells;
            CoarseX = (grid.NX + CoarseFactor - 1) / CoarseFactor;
            CoarseY = (grid.NY + CoarseFactor - 1) / CoarseFactor;
            CoarseCells = CoarseX * CoarseY;
            Energy = new float[NB * Cells];
            Previous = new float[NB * Cells];
            work = new float[NB * Cells];
            WindX = new float[Cells]; WindY = new float[Cells];
            windXNext = new float[Cells]; windYNext = new float[Cells];
            Foam = new float[Cells]; foamNext = new float[Cells];
            SampleX = new float[Cells]; SampleY = new float[Cells];
            GustSample = new float[CoarseCells];
            TailFactor = new float[CoarseCells * TailBins];
            tailNext = new float[CoarseCells * TailBins];
            for (var n = 0; n < TailFactor.Length; n++) TailFactor[n] = 1;
            peak = new float[Cells]; peakNext = new float[Cells];
            breaking = new float[Cells]; breakingNext = new float[Cells];
            surf = new float[Cells]; surfNext = new float[Cells];
            scratchA = new float[Cells]; scratchB = new float[Cells];

            depthClass = new int[Cells];
            var depths = new System.Collections.Generic.List<double>();
            for (var c = 0; c < Cells; c++)
            {
                var index = depths.IndexOf(grid.Depth[c]);
                if (index < 0) { index = depths.Count; depths.Add(grid.Depth[c]); }
                depthClass[c] = index;
            }
            classDepth = depths.ToArray();
            classK = new double[classDepth.Length * NF];
            classPhase = new double[classDepth.Length * NF];
            classGroup = new double[classDepth.Length * NF];
            classTma = new double[classDepth.Length * NF];
            classFriction = new double[classDepth.Length * NF];
            for (var d = 0; d < classDepth.Length; d++)
                for (var i = 0; i < NF; i++)
                {
                    var f = WaveSpectrum.Frequency[i];
                    var h = classDepth[d];
                    var k = WaveSpectrum.WaveNumber(f, h);
                    classK[d * NF + i] = k;
                    classPhase[d * NF + i] = 2 * Math.PI * f / k;
                    classGroup[d * NF + i] = WaveSpectrum.GroupSpeed(f, h);
                    classTma[d * NF + i] = WaveSpectrum.TmaFactor(f, h);
                    var sinh = h >= WaveSpectrum.Deep || k * h > 12 ? double.PositiveInfinity : Math.Sinh(k * h);
                    var omega = 2 * Math.PI * f;
                    classFriction[d * NF + i] = double.IsInfinity(sinh) ? 0 : BottomFriction * omega * omega / (MarineUnits.Gravity * MarineUnits.Gravity * sinh * sinh);
                    maxGroup[i] = Math.Max(maxGroup[i], classGroup[d * NF + i]);
                }
            for (var i = 0; i < NF; i++)
            {
                var f = WaveSpectrum.Frequency[i];
                deepK[i] = WaveSpectrum.WaveNumber(f, WaveSpectrum.Deep);
                deepPhase[i] = 2 * Math.PI * f / deepK[i];
                deepGroup[i] = WaveSpectrum.GroupSpeed(f, WaveSpectrum.Deep);
                // Equilibrium range E(f) = α_s·g·u*·(2π)⁻³·f⁻⁴ (Toba 1973) read backwards: u* from the bin's energy.
                tobaLevel[i] = Math.Pow(2 * Math.PI, 3) * f * f * f * f / (TobaConstant * MarineUnits.Gravity * WaveSpectrum.Width[i]);
            }
            classStokes = new double[classDepth.Length * NB * 2];
            for (var b = 0; b < NB; b++)
            {
                var i = b / ND;
                var j = b % ND;
                var omega = 2 * Math.PI * WaveSpectrum.Frequency[i];
                deepStokes[2 * b] = 2 * omega * deepK[i] * WaveSpectrum.DirectionCos[j];
                deepStokes[2 * b + 1] = 2 * omega * deepK[i] * WaveSpectrum.DirectionSin[j];
                for (var d = 0; d < classDepth.Length; d++)
                {
                    classStokes[(d * NB + b) * 2] = 2 * omega * classK[d * NF + i] * WaveSpectrum.DirectionCos[j];
                    classStokes[(d * NB + b) * 2 + 1] = 2 * omega * classK[d * NF + i] * WaveSpectrum.DirectionSin[j];
                }
            }
        }

        /// <summary>Weather seconds per step: one simulation second times the climate's explicit acceleration.</summary>
        public double StepSeconds => TicksPerStep * Rules.Dt * Climate.WeatherTimeScale;

        public double Depth(int cell) => Grid.Depth[cell];
        public double GroupSpeed(int cell, int frequency) => classGroup[depthClass[cell] * NF + frequency];
        public double WaveNumber(int cell, int frequency) => classK[depthClass[cell] * NF + frequency];

        // Start ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Starts from the open sea's fetch-limited state for the present wind, shoaled into each cell by keeping the
        /// energy flux, then lets it settle around the islands for a while with larger steps (stable: Courant ≤ 0.9).
        /// </summary>
        public void Initialise(IMarineWind wind, double settleSeconds = 300, double settleStep = 4)
        {
            Array.Clear(Energy, 0, Energy.Length);
            SampleAll(wind);
            SampleEveryCell(wind);
            OuterFetchLimited(SynopticX, SynopticY, Outer);
            for (var b = 0; b < NB; b++)
            {
                var i = b / ND;
                for (var c = 0; c < Cells; c++)
                    Energy[b * Cells + c] = Grid.Land[c] ? 0 : (float)(Outer[b] * deepGroup[i] / GroupSpeed(c, i));
            }
            Array.Copy(SampleX, WindX, Cells);
            Array.Copy(SampleY, WindY, Cells);
            for (var c = 0; c < Cells; c++)
                Foam[c] = Grid.Land[c] ? 0 : (float)WaveSpectrum.WhitecapCoverage(Math.Sqrt(WindX[c] * WindX[c] + WindY[c] * WindY[c]));
            for (var n = 0; n < TailFactor.Length; n++) TailFactor[n] = 1;
            Steps = 0;
            Slice = 0;
            RecomputeBinMax();
            var steps = (int)Math.Ceiling(settleSeconds / settleStep);
            for (var n = 0; n < steps; n++) Step(settleStep, wind);
            Steps = 0;
            Array.Copy(Energy, Previous, Energy.Length);
        }

        /// <summary>Starts from calm water with no sea beyond the edges (tests of growth and decay).</summary>
        public void InitialiseCalm(IMarineWind wind)
        {
            Array.Clear(Energy, 0, Energy.Length);
            Array.Clear(Previous, 0, Previous.Length);
            Array.Clear(Outer, 0, Outer.Length);
            SampleAll(wind);
            SampleEveryCell(wind);
            Array.Copy(SampleX, WindX, Cells);
            Array.Copy(SampleY, WindY, Cells);
            Array.Clear(Foam, 0, Foam.Length);
            for (var n = 0; n < TailFactor.Length; n++) TailFactor[n] = 1;
            Steps = 0;
            Slice = 0;
            RecomputeBinMax();
        }

        /// <summary>When false, the sea beyond the edges stays as it is (tests with a fixed or empty boundary).</summary>
        public bool EvolveOuterSea = true;

        // Stepping ---------------------------------------------------------------------------------------------------

        /// <summary>A whole step at once (tests, settling). The same parts in the same order as the ticks.</summary>
        public void Step(double dt, IMarineWind wind)
        {
            if (Slice != 0) throw new InvalidOperationException("A sliced step is in progress.");
            SampleAll(wind);
            for (var d = 0; d < ND; d++) PropagateDirection(d, dt);
            Sources(0, Cells, dt);
            Finish(dt);
            Swap();
        }

        /// <summary>One simulation tick: this tick's part of the current step.</summary>
        public void Tick(IMarineWind wind)
        {
            var t = Slice + 1;
            DoSlice(t, StepSeconds, wind);
            Slice = t;
            if (Slice == TicksPerStep) Swap();
        }

        /// <summary>After loading: redoes the parts of the current step already done before the save, from its samples.</summary>
        public void Restore()
        {
            Array.Copy(Energy, Previous, Energy.Length);
            RecomputeBinMax();
            Array.Clear(binMaxNext, 0, NB);
            for (var t = 1; t <= Slice; t++) DoSlice(t, StepSeconds, null);
        }

        void DoSlice(int t, double dt, IMarineWind wind)
        {
            if (t <= PropagationTicks)
            {
                if (wind != null) SampleSlice(t, wind);
                PropagateDirection(t - 1, dt);
            }
            else if (t <= PropagationTicks + SourceTicks)
            {
                var k = t - PropagationTicks - 1;
                Sources(k * Cells / SourceTicks, (k + 1) * Cells / SourceTicks, dt);
            }
            else if (t == PropagationTicks + SourceTicks + 1) Finish(dt);
        }

        void SampleAll(IMarineWind wind)
        {
            for (var t = 1; t <= PropagationTicks; t++) SampleSlice(t, wind);
        }

        /// <summary>Samples every cell's mean wind at once (start, tests).</summary>
        void SampleEveryCell(IMarineWind wind)
        {
            for (var c = 0; c < Cells; c++)
            {
                wind.MeanWind(Grid.CenterX(c), Grid.CenterY(c), out var ux, out var uy);
                SampleX[c] = (float)ux;
                SampleY[c] = (float)uy;
            }
        }

        void SampleSlice(int t, IMarineWind wind)
        {
            var half = Steps & 1;
            for (var c = (t - 1) * Cells / PropagationTicks; c < t * Cells / PropagationTicks; c++)
            {
                if ((c & 1) != half) continue;
                wind.MeanWind(Grid.CenterX(c), Grid.CenterY(c), out var ux, out var uy);
                SampleX[c] = (float)ux;
                SampleY[c] = (float)uy;
            }
            for (var k = (t - 1) * CoarseCells / PropagationTicks; k < t * CoarseCells / PropagationTicks; k++)
            {
                CoarseCenter(k, out var x, out var y);
                wind.GustyWind(x, y, out var ux, out var uy);
                GustSample[k] = (float)Math.Sqrt(ux * ux + uy * uy);
            }
            if (t == 1) wind.SynopticWind(out SynopticX, out SynopticY);
        }

        void CoarseCenter(int k, out double x, out double y)
        {
            var i0 = k % CoarseX * CoarseFactor;
            var j0 = k / CoarseX * CoarseFactor;
            var i1 = Math.Min(Grid.NX, i0 + CoarseFactor);
            var j1 = Math.Min(Grid.NY, j0 + CoarseFactor);
            x = Grid.OriginX + 0.5 * (i0 + i1) * Grid.CellSize;
            y = Grid.OriginY + 0.5 * (j0 + j1) * Grid.CellSize;
        }

        void Swap()
        {
            var spare = Previous;
            Previous = Energy;
            Energy = work;
            work = spare;
            (WindX, windXNext) = (windXNext, WindX);
            (WindY, windYNext) = (windYNext, WindY);
            (Foam, foamNext) = (foamNext, Foam);
            (TailFactor, tailNext) = (tailNext, TailFactor);
            (peak, peakNext) = (peakNext, peak);
            (breaking, breakingNext) = (breakingNext, breaking);
            (surf, surfNext) = (surfNext, surf);
            Array.Copy(outerNext, Outer, NB);
            Array.Copy(binMaxNext, binMax, NB);
            Array.Clear(binMaxNext, 0, NB);
            Steps++;
            Slice = 0;
        }

        /// <summary>
        /// Call after writing Energy directly (tests, tools): the written state becomes both steps the surface blends,
        /// and the transport learns which bins are no longer empty.
        /// </summary>
        public void EnergyChanged()
        {
            Array.Copy(Energy, Previous, Energy.Length);
            RecomputeBinMax();
        }

        void RecomputeBinMax()
        {
            for (var b = 0; b < NB; b++)
            {
                float max = 0;
                var offset = b * Cells;
                for (var c = 0; c < Cells; c++) if (Energy[offset + c] > max) max = Energy[offset + c];
                binMax[b] = max;
            }
        }

        // Propagation ------------------------------------------------------------------------------------------------

        /// <summary>Largest Courant number of a step of dt seconds (before sub-steps).</summary>
        public double Courant(double dt)
        {
            double max = 0;
            for (var i = 0; i < NF; i++) max = Math.Max(max, maxGroup[i]);
            return dt * max * Math.Sqrt(2) / Grid.CellSize;
        }

        void PropagateDirection(int d, double dt)
        {
            for (var i = 0; i < NF; i++) PropagateBin(i * ND + d, dt);
        }

        void PropagateBin(int b, double dt)
        {
            var i = b / ND;
            var d = b % ND;
            var offset = b * Cells;
            if (binMax[b] <= 0 && Outer[b] <= 0)
            {
                Array.Clear(work, offset, Cells);
                return;
            }
            var ax = Math.Abs(WaveSpectrum.DirectionCos[d]);
            var ay = Math.Abs(WaveSpectrum.DirectionSin[d]);
            var courant = dt * maxGroup[i] * (ax + ay) / Grid.CellSize;
            var sub = Math.Max(1, (int)Math.Ceiling(courant / MaxCourant));
            var lambda = dt / sub / Grid.CellSize;
            var inflow = deepGroup[i] * Outer[b];
            if (sub == 1)
            {
                Advect(Energy, offset, work, offset, i, d, lambda, inflow);
                return;
            }
            Advect(Energy, offset, scratchA, 0, i, d, lambda, inflow);
            var source = scratchA;
            for (var s = 1; s < sub; s++)
            {
                var last = s == sub - 1;
                var target = last ? work : (source == scratchA ? scratchB : scratchA);
                Advect(source, 0, target, last ? offset : 0, i, d, lambda, inflow);
                source = target;
            }
        }

        void Advect(float[] src, int so, float[] dst, int dOffset, int i, int d, double lambda, double inflowFlux)
        {
            var nx = Grid.NX;
            var ny = Grid.NY;
            var cx = WaveSpectrum.DirectionCos[d];
            var cy = WaveSpectrum.DirectionSin[d];
            var ax = Math.Abs(cx);
            var ay = Math.Abs(cy);
            var sx = cx > 0 ? -1 : 1;
            var sy = cy > 0 ? -1 : 1;
            var along = ax + ay;
            var transparency = Grid.Transparency;
            var obstacle = Grid.ObstacleSize;
            var toff = d * Cells;
            var periodicX = Grid.PeriodicX;
            var periodicY = Grid.PeriodicY;
            for (var y = 0; y < ny; y++)
            {
                var row = y * nx;
                var uy = y + sy;
                if (periodicY) uy = (uy + ny) % ny;
                var yInside = uy >= 0 && uy < ny;
                for (var x = 0; x < nx; x++)
                {
                    var c = row + x;
                    var cls = depthClass[c] * NF + i;
                    var e = src[so + c];
                    double incoming = 0;
                    if (ax > 0)
                    {
                        var ux = x + sx;
                        if (periodicX) ux = (ux + nx) % nx;
                        incoming += ax * (ux >= 0 && ux < nx ? classGroup[depthClass[row + ux] * NF + i] * src[so + row + ux] : inflowFlux);
                    }
                    if (ay > 0)
                        incoming += ay * (yInside ? classGroup[depthClass[uy * nx + x] * NF + i] * src[so + uy * nx + x] : inflowFlux);
                    var pass = 1.0;
                    var opaque = 1 - transparency[toff + c];
                    if (opaque > 0)
                    {
                        var size = obstacle[c];
                        var bend = size <= 0 ? 1 : Math.Min(1, size * classK[cls] / Math.PI);
                        pass = 1 - opaque * bend;
                    }
                    var value = e + lambda * (pass * incoming - along * classGroup[cls] * e);
                    dst[dOffset + c] = value < 1e-14 ? 0f : (float)value;
                }
            }
        }

        // Sources ----------------------------------------------------------------------------------------------------

        void Sources(int c0, int c1, double dt)
        {
            var keep = 1 - Math.Exp(-dt / WindFilterSeconds);
            for (var c = c0; c < c1; c++)
            {
                var wx = WindX[c] + (SampleX[c] - WindX[c]) * keep;
                var wy = WindY[c] + (SampleY[c] - WindY[c]) * keep;
                windXNext[c] = (float)wx;
                windYNext[c] = (float)wy;
                if (Grid.Land[c])
                {
                    for (var b = 0; b < NB; b++) work[b * Cells + c] = 0;
                    foamNext[c] = 0;
                    peakNext[c] = 0;
                    breakingNext[c] = 0;
                    surfNext[c] = 0;
                    continue;
                }
                for (var b = 0; b < NB; b++) spectrum[b] = work[b * Cells + c];
                var physics = depthClass[c] * NF;
                ApplySources(spectrum, physics, classDepth[depthClass[c]], wx, wy, dt, double.MaxValue, 0, out var fp, out var probability, out var depthBreaking, out var stokesX, out var stokesY);
                for (var b = 0; b < NB; b++)
                {
                    var value = spectrum[b] < 1e-14 ? 0f : (float)spectrum[b];
                    work[b * Cells + c] = value;
                    if (value > binMaxNext[b]) binMaxNext[b] = value;
                }
                peakNext[c] = (float)fp;
                breakingNext[c] = (float)probability;
                surfNext[c] = (float)depthBreaking;
                foamNext[c] = (float)FoamAt(c, wx, wy, stokesX, stokesY, depthBreaking, dt);
            }
        }

        /// <summary>
        /// Sources and sinks of one spectrum over dt seconds. Phase speed falls with frequency at any depth, so the bins
        /// the wind feeds in a direction are those from one frequency up (c &lt; 1.2·U·cos Δθ): one threshold per
        /// direction. Three passes: the wind sea per direction and the swell per frequency; the wind sea grown by the
        /// fetch law and reshaped; every sink as one factor per frequency, depth breaking folded in.
        /// physics: offset of the depth class in the per-frequency tables (−1: deep water); cap: largest wind-sea energy
        /// (the outer sea's fetch); outerFetch &gt; 0: swell leaves the outer region at its group speed.
        /// </summary>
        void ApplySources(double[] s, int physics, double depth, double ux, double uy, double dt, double cap, double outerFetch,
            out double fp, out double probability, out double depthBreaking, out double stokesX, out double stokesY)
        {
            var g = MarineUnits.Gravity;
            var deep = physics < 0;
            var phases = deep ? deepPhase : classPhase;
            var offset = deep ? 0 : physics;
            var wind = Math.Sqrt(ux * ux + uy * uy);
            var blowing = wind >= MinimumWind;
            double cw = 1, sw = 0;
            if (blowing) { cw = ux / wind; sw = uy / wind; }
            for (var i = 0; i < NF; i++) unforcedEnergy[i] = 0;

            // Pass 1: the wind sea in each direction, the swell by frequency.
            double total = 0;
            for (var j = 0; j < ND; j++)
            {
                var start = NF;
                if (blowing)
                {
                    var limit = WaveSpectrum.WindSeaLimit * wind * (WaveSpectrum.DirectionCos[j] * cw + WaveSpectrum.DirectionSin[j] * sw);
                    if (limit > 0)
                    {
                        start = 0;
                        while (start < NF && phases[offset + start] >= limit) start++;
                    }
                }
                starts[j] = start;
                for (var i = 0; i < start; i++) unforcedEnergy[i] += s[i * ND + j];
                double fed = 0;
                for (var i = start; i < NF; i++) fed += s[i * ND + j];
                windSea[j] = fed;
                total += fed;
            }
            double swell = 0, swellK = 0, swellF = 0;
            for (var i = 0; i < NF; i++)
            {
                var e = unforcedEnergy[i];
                swell += e;
                swellK += e * (deep ? deepK[i] : classK[physics + i]);
                swellF += e * WaveSpectrum.Frequency[i];
            }

            // Pass 2: the wind sea grows by the fetch law and takes the JONSWAP·TMA shape in each direction it fills.
            fp = 0;
            double windSeaF = 0;
            if (blowing)
            {
                var chi = WaveSpectrum.EquivalentFetch(total, wind);
                fp = WaveSpectrum.PeakFrequency(chi, wind);
                WaveSpectrum.BinShape(fp, WaveSpectrum.PeakEnhancement(chi), shape);
                double norm = 0;
                for (var i = 0; i < NF; i++)
                {
                    if (!deep) shape[i] *= classTma[physics + i];
                    norm += shape[i];
                }
                double tail = 0, tailF = 0;
                for (var i = NF - 1; i >= 0; i--)
                {
                    if (norm > 0) shape[i] /= norm;
                    tail += shape[i];
                    tailF += shape[i] * WaveSpectrum.Frequency[i];
                    suffix[i] = tail;
                    suffixF[i] = tailF;
                }
                WaveSpectrum.DirectionShares(Math.Atan2(uy, ux), shares);
                double holding = 0;
                for (var j = 0; j < ND; j++) if (starts[j] < NF) holding += shares[j];
                // d(κ·c_g(f_p)·E)/dx = S with E = 1.6·10⁻⁷·U²X/g and c_g ∝ X^0.33 gives S = (4/3)·κ·c_g(f_p)·1.6·10⁻⁷·U²/g.
                var input = 4.0 / 3.0 * WaveSpectrum.TransportFactor * GroupAt(physics, fp) * WaveSpectrum.FetchEnergy * wind * wind / g * Climate.GrowthCalibration;
                var limitEnergy = Math.Min(cap, WaveSpectrum.FullyDevelopedEnergy(wind) * Climate.GrowthCalibration);
                var added = Math.Max(0, Math.Min(input * dt, limitEnergy - total));
                // New energy takes the shape at once; the energy already there relaxes toward it at ShapeRate times the
                // relative growth rate (the four-wave transfer that shapes a wind sea is driven by the same input): a
                // young sea follows the growth law cell by cell, an old one does not reshape under a passing gust.
                var relax = total > 0 ? 1 - Math.Exp(-ShapeRate * input * dt / total) : 1;
                double grown = 0;
                for (var j = 0; j < ND; j++)
                {
                    var start = starts[j];
                    if (start >= NF) continue;
                    var share = holding > 0 ? added * shares[j] / holding : 0;
                    var within = suffix[start];
                    if (within <= 0)
                    {
                        for (var i = start; i < NF; i++) windSeaF += s[i * ND + j] * WaveSpectrum.Frequency[i];
                        grown += windSea[j];
                        continue;
                    }
                    var target = windSea[j] / within;
                    var fresh = share / within;
                    for (var i = start; i < NF; i++)
                    {
                        var b = i * ND + j;
                        var v = s[b] + (shape[i] * target - s[b]) * relax + shape[i] * fresh;
                        s[b] = v;
                        windSeaF += v * WaveSpectrum.Frequency[i];
                    }
                    grown += windSea[j] + share;
                }
                total = grown;
            }

            // Sinks as one factor per frequency: bottom friction on all bins; whitecapping of swell (WAM cycle 3), decay
            // of unforced short waves and swell leaving the outer region on the bins the wind does not feed; depth breaking.
            double whitecap = 0, meanK = 1;
            if (swell > 0)
            {
                meanK = swellK / swell;
                var steepness = swell * meanK * meanK / PiersonMoskowitzSteepness;
                whitecap = WhitecapCoefficient * 2 * Math.PI * (swellF / swell) * steepness * steepness;
            }
            var m0 = total + swell;
            double breakingFactor = 1;
            depthBreaking = 0;
            if (m0 > 0 && depth < WaveSpectrum.Deep)
            {
                var hrms = Math.Sqrt(8 * m0);
                if (hrms > 0.06 * depth)
                {
                    var mean = (windSeaF + swellF) / m0;
                    var k = WaveSpectrum.WaveNumber(mean, depth);
                    var hmax = WaveSpectrum.BreakerIndex(k * depth) * depth;
                    depthBreaking = WaveSpectrum.BreakingFraction(hrms, hmax);
                    if (depthBreaking > 0) breakingFactor = 1 / (1 + 0.25 * mean * depthBreaking * hmax * hmax / m0 * dt);
                }
            }
            for (var i = 0; i < NF; i++)
            {
                var friction = deep ? 0 : classFriction[physics + i];
                var k = deep ? deepK[i] : classK[physics + i];
                double tail = 0;
                if (i >= FirstTailBin && unforcedEnergy[i] > 0)
                {
                    var ustar = unforcedEnergy[i] * tobaLevel[i];
                    var phase = phases[offset + i];
                    tail = PlantGrowth * (ustar / phase) * (ustar / phase) * 2 * Math.PI * WaveSpectrum.Frequency[i];
                }
                var leaving = outerFetch > 0 ? deepGroup[i] / outerFetch : 0;
                fedFactor[i] = breakingFactor / (1 + friction * dt);
                freeFactor[i] = breakingFactor / (1 + (friction + whitecap * k / meanK + tail + leaving) * dt);
                frequencyEnergy[i] = 0;
            }

            // Pass 3: apply, with the energy per frequency and the Stokes drift (deep-water form).
            stokesX = 0;
            stokesY = 0;
            var stokes = deep ? deepStokes : classStokes;
            var stokesOffset = deep ? 0 : physics / NF * NB * 2;
            for (var j = 0; j < ND; j++)
            {
                var start = starts[j];
                double sx = 0, sy = 0;
                for (var i = 0; i < NF; i++)
                {
                    var b = i * ND + j;
                    var v = s[b] * (i < start ? freeFactor[i] : fedFactor[i]);
                    s[b] = v;
                    frequencyEnergy[i] += v;
                    sx += v * stokes[stokesOffset + 2 * b];
                    sy += v * stokes[stokesOffset + 2 * b + 1];
                }
                stokesX += sx;
                stokesY += sy;
            }

            // The dominant waves and their breaking probability (Banner et al. 2000).
            var peakBin = 0;
            double peakDensity = 0;
            for (var i = 0; i < NF; i++)
            {
                var density = frequencyEnergy[i] / WaveSpectrum.Width[i];
                if (density > peakDensity) { peakDensity = density; peakBin = i; }
            }
            var dominant = WaveSpectrum.Frequency[peakBin];
            if (fp <= 0) fp = peakDensity > 0 ? dominant : 0;
            double band = 0;
            for (var i = 0; i < NF; i++)
            {
                var f = WaveSpectrum.Frequency[i];
                if (f >= 0.7 * dominant && f <= 1.3 * dominant) band += frequencyEnergy[i];
            }
            var kp = deep ? deepK[peakBin] : classK[physics + peakBin];
            probability = WaveSpectrum.BreakingProbability(2 * Math.Sqrt(band) * kp);
        }

        /// <summary>Group speed at any frequency, interpolated in ln f between the bins (above them: deep water).</summary>
        double GroupAt(int physics, double f)
        {
            var g = MarineUnits.Gravity;
            var top = WaveSpectrum.Frequency[WaveSpectrum.Tail];
            if (f >= top) return g / (4 * Math.PI * f);
            if (f <= WaveSpectrum.Frequency[0])
                return physics < 0 ? g / (4 * Math.PI * f) : WaveSpectrum.GroupSpeed(f, classDepth[physics / NF]);
            int i0;
            double w;
            var last = WaveSpectrum.Resolved - 1;
            if (f >= WaveSpectrum.Frequency[last])
            {
                i0 = last;
                w = Math.Log(f / WaveSpectrum.Frequency[last]) / Math.Log(top / WaveSpectrum.Frequency[last]);
            }
            else
            {
                var t = Math.Log(f / WaveSpectrum.Frequency[0]) / Math.Log(WaveSpectrum.Ratio);
                i0 = Math.Min(last - 1, (int)t);
                w = t - i0;
            }
            var a = physics < 0 ? deepGroup[i0] : classGroup[physics + i0];
            var b = physics < 0 ? deepGroup[i0 + 1] : classGroup[physics + i0 + 1];
            return a + (b - a) * w;
        }

        /// <summary>Foam coverage: carried by the drift, fed by whitecaps (Monahan) and surf, decaying in FoamSeconds.</summary>
        double FoamAt(int c, double wx, double wy, double stokesX, double stokesY, double depthBreaking, double dt)
        {
            var nx = Grid.NX;
            var x = c % nx;
            var y = c / nx;
            var vx = Climate.DriftFraction * wx + stokesX;
            var vy = Climate.DriftFraction * wy + stokesY;
            var f = (double)Foam[c];
            var ux = x + (vx > 0 ? -1 : 1);
            var uy = y + (vy > 0 ? -1 : 1);
            if (Grid.PeriodicX) ux = (ux + nx) % nx;
            if (Grid.PeriodicY) uy = (uy + Grid.NY) % Grid.NY;
            var fx = ux >= 0 && ux < nx && !Grid.Land[y * nx + ux] ? Foam[y * nx + ux] : f;
            var fy = uy >= 0 && uy < Grid.NY && !Grid.Land[uy * nx + x] ? Foam[uy * nx + x] : f;
            var lambda = dt / Grid.CellSize;
            var carried = f - lambda * (Math.Abs(vx) * (f - fx) + Math.Abs(vy) * (f - fy));
            var source = (WaveSpectrum.WhitecapCoverage(Math.Sqrt(wx * wx + wy * wy)) + depthBreaking) / FoamSeconds;
            return Math.Max(0, Math.Min(1, (carried + source * dt) / (1 + dt / FoamSeconds)));
        }

        // End of a step --------------------------------------------------------------------------------------------------

        void Finish(double dt)
        {
            var ratioAir = Climate.AirDensity / Climate.WaterDensity;
            for (var k = 0; k < CoarseCells; k++)
            {
                var i0 = k % CoarseX * CoarseFactor;
                var j0 = k / CoarseX * CoarseFactor;
                double mean = 0, fp = 0;
                var count = 0;
                for (var j = j0; j < Math.Min(Grid.NY, j0 + CoarseFactor); j++)
                    for (var i = i0; i < Math.Min(Grid.NX, i0 + CoarseFactor); i++)
                    {
                        var c = j * Grid.NX + i;
                        if (Grid.Land[c]) continue;
                        mean += Math.Sqrt(windXNext[c] * windXNext[c] + windYNext[c] * windYNext[c]);
                        fp += peakNext[c];
                        count++;
                    }
                if (count > 0) { mean /= count; fp /= count; }
                WindField.Roughness(mean, out var meanStar, out _);
                WindField.Roughness(GustSample[k], out var gustStar, out _);
                var ratio = mean >= MinimumWind ? Math.Max(0.25, Math.Min(2.5, gustStar / meanStar)) : 1;
                var ustar = Math.Max(gustStar, meanStar);
                for (var t = 0; t < TailBins; t++)
                {
                    var i = FirstTailBin + t;
                    var f = WaveSpectrum.Frequency[i];
                    var weight = fp > 0 ? SmoothStep(1.5, 3.0, f / fp) : 1;
                    var target = 1 + weight * (ratio - 1);
                    var beta = 0.25 * ratioAir * Math.Max(28 * ustar / deepPhase[i] - 1, 1) * 2 * Math.PI * f;
                    var n = k * TailBins + t;
                    tailNext[n] = (float)(target + (TailFactor[n] - target) * Math.Exp(-beta * dt));
                }
            }

            Array.Copy(Outer, outerNext, NB);
            if (EvolveOuterSea)
            {
                var wind = Math.Sqrt(SynopticX * SynopticX + SynopticY * SynopticY);
                var cap = WaveSpectrum.FetchLimitedEnergy(Climate.OuterFetch, wind) * Climate.GrowthCalibration;
                ApplySources(outerNext, -1, WaveSpectrum.Deep, SynopticX, SynopticY, dt, cap, Climate.OuterFetch, out _, out _, out _, out _, out _);
            }
        }

        static double SmoothStep(double edge0, double edge1, double x)
        {
            var t = Math.Max(0, Math.Min(1, (x - edge0) / (edge1 - edge0)));
            return t * t * (3 - 2 * t);
        }

        /// <summary>The open sea's wind sea for a wind blowing over the outer fetch: the JONSWAP fetch law and shape.</summary>
        void OuterFetchLimited(double ux, double uy, double[] into)
        {
            Array.Clear(into, 0, NB);
            var wind = Math.Sqrt(ux * ux + uy * uy);
            if (wind < MinimumWind) return;
            var energy = Math.Min(WaveSpectrum.FetchLimitedEnergy(Climate.OuterFetch, wind), WaveSpectrum.FullyDevelopedEnergy(wind)) * Climate.GrowthCalibration;
            var chi = WaveSpectrum.EquivalentFetch(energy, wind);
            var fp = WaveSpectrum.PeakFrequency(chi, wind);
            WaveSpectrum.BinShape(fp, WaveSpectrum.PeakEnhancement(chi), shape);
            WaveSpectrum.DirectionShares(Math.Atan2(uy, ux), shares);
            double holding = 0;
            for (var j = 0; j < ND; j++)
            {
                cosDelta[j] = WaveSpectrum.DirectionCos[j] * ux / wind + WaveSpectrum.DirectionSin[j] * uy / wind;
                holds[j] = false;
                for (var i = 0; i < NF; i++) if (deepPhase[i] < WaveSpectrum.WindSeaLimit * wind * cosDelta[j]) holds[j] = true;
                if (holds[j]) holding += shares[j];
            }
            for (var j = 0; j < ND; j++)
            {
                if (!holds[j]) continue;
                double within = 0;
                for (var i = 0; i < NF; i++) if (deepPhase[i] < WaveSpectrum.WindSeaLimit * wind * cosDelta[j]) within += shape[i];
                if (within <= 0) continue;
                for (var i = 0; i < NF; i++)
                    if (deepPhase[i] < WaveSpectrum.WindSeaLimit * wind * cosDelta[j])
                        into[i * ND + j] = energy * shares[j] / holding * shape[i] / within;
            }
        }

        // Reading ------------------------------------------------------------------------------------------------------

        /// <summary>Energy of a bin in a cell between the last two steps (w = 0: previous, 1: current).</summary>
        public double Blend(int bin, int cell, double w)
        {
            var index = bin * Cells + cell;
            return Previous[index] + (Energy[index] - Previous[index]) * w;
        }

        public double SignificantHeight(int cell)
        {
            double m0 = 0;
            for (var b = 0; b < NB; b++) m0 += Energy[b * Cells + cell];
            return 4 * Math.Sqrt(m0);
        }

        /// <summary>Energy travelling within ±90° of a direction (radians), in a cell.</summary>
        public double EnergyToward(int cell, double angle)
        {
            double e = 0;
            for (var b = 0; b < NB; b++)
            {
                var j = b % ND;
                if (Math.Cos(WaveSpectrum.DirectionAngle[j] - angle) > 1e-9) e += Energy[b * Cells + cell];
            }
            return e;
        }

        public double FrequencyEnergy(int cell, int frequency)
        {
            double e = 0;
            for (var j = 0; j < ND; j++) e += Energy[(frequency * ND + j) * Cells + cell];
            return e;
        }

        public double TotalEnergy()
        {
            double e = 0;
            for (var n = 0; n < Energy.Length; n++) e += Energy[n];
            return e;
        }

        public double PeakFrequency(int cell) => peak[cell];
        public double BreakingProbability(int cell) => breaking[cell];
        public double SurfBreaking(int cell) => surf[cell];

        /// <summary>Gust multiplier of the short-wave energy of a tail bin (0–2) at a cell.</summary>
        public double Tail(int cell, int tailBin)
        {
            var k = cell / Grid.NX / CoarseFactor * CoarseX + cell % Grid.NX / CoarseFactor;
            return TailFactor[k * TailBins + tailBin];
        }
    }
}
