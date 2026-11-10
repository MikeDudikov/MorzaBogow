using System;
using System.Collections.Generic;
using MorzaBogow.Simulation;
using NUnit.Framework;

namespace MorzaBogow.Tests
{
    /// <summary>Wind over the map, the waves it raises and the forces of both on ships (docs/MORZE_I_WIATR.md).</summary>
    public sealed class MarineTests
    {
        // Helpers ------------------------------------------------------------------------------------------------

        /// <summary>A steady wind blowing toward +X (from the west): no slow variability, no diurnal cycle, no cells.</summary>
        static MarineClimate Steady(double wind, double fromBearing = 270)
        {
            var climate = MarineClimate.Aegean(WindRegime.EtesianModerate, AegeanRegion.Central);
            climate.MeanWind = wind;
            climate.FromBearingDegrees = fromBearing;
            climate.DirectionSigmaDegrees = 0;
            climate.SpeedSigma = 0;
            climate.DiurnalAmplitude = 0;
            climate.CellsPerHour = 0;
            return climate;
        }

        static double Longitudinal(WindSample sample)
        {
            var speed = sample.MeanSpeed;
            return (sample.GustX * sample.MeanX + sample.GustY * sample.MeanY) / speed;
        }

        static double Correlation(double[] a, double[] b, int lag)
        {
            var n = a.Length - lag;
            double ma = 0, mb = 0;
            for (var i = 0; i < n; i++) { ma += a[i]; mb += b[i + lag]; }
            ma /= n; mb /= n;
            double sab = 0, saa = 0, sbb = 0;
            for (var i = 0; i < n; i++)
            {
                var da = a[i] - ma;
                var db = b[i + lag] - mb;
                sab += da * db; saa += da * da; sbb += db * db;
            }
            return sab / Math.Sqrt(saa * sbb);
        }

        /// <summary>Bessel J0 (Abramowitz and Stegun 9.4.1 and 9.4.3, error below 1e-7).</summary>
        static double BesselJ0(double x)
        {
            x = Math.Abs(x);
            if (x <= 3)
            {
                var y = (x / 3) * (x / 3);
                return 1 + y * (-2.2499997 + y * (1.2656208 + y * (-0.3163866 + y * (0.0444479 + y * (-0.0039444 + y * 0.0002100)))));
            }
            var z = 3 / x;
            var f = 0.79788456 + z * (-0.00000077 + z * (-0.00552740 + z * (-0.00009512 + z * (0.00137237 + z * (-0.00072805 + z * 0.00014476)))));
            var t = x - 0.78539816 + z * (-0.04166397 + z * (-0.00003954 + z * (0.00262573 + z * (-0.00054125 + z * (-0.00029333 + z * 0.00013558)))));
            return f * Math.Cos(t) / Math.Sqrt(x);
        }

        // Wind ---------------------------------------------------------------------------------------------------

        [Test]
        public void Wind_GustPeaksReachDownwindPointLater()
        {
            const double wind = 8, distance = 200, dt = 0.25;
            var field = new WindField(Steady(wind), 11, null);
            var n = (int)(900 / dt);
            var upwind = new double[n];
            var downwind = new double[n];
            for (var i = 0; i < n; i++)
            {
                field.Advance(dt);
                upwind[i] = Longitudinal(field.Sample(0, 0));
                downwind[i] = Longitudinal(field.Sample(distance, 0));
            }
            int bestLag = 0;
            var best = double.MinValue;
            for (var lag = 0; lag <= (int)(60 / dt); lag++)
            {
                var c = Correlation(upwind, downwind, lag);
                if (c > best) { best = c; bestLag = lag; }
            }
            var expected = distance / wind;
            Assert.That(bestLag * dt, Is.EqualTo(expected).Within(0.2 * expected), "A gust reaches the point 200 m downwind after distance / wind speed.");
            Assert.That(best, Is.GreaterThan(0.7), "The gust pattern arrives nearly unchanged (frozen turbulence over 25 s).");
            Assert.That(Correlation(upwind, downwind, 0), Is.LessThan(best - 0.2), "At the same moment the two points differ.");
            Assert.That(Correlation(downwind, upwind, (int)(expected / dt)), Is.LessThan(0.3), "The downwind point does not lead.");
        }

        [Test]
        public void Wind_NeighboursFollowTheSpectrumCorrelation()
        {
            var climate = Steady(8);
            double[] separations = { 10, 50, 100, 200, 400, 800 };
            var length = climate.GustLength;
            var kMin = WindField.LowestWavenumber(length);
            var kMax = WindField.HighestWavenumber;

            // The spectrum the field claims: the band-limited Matérn (von Kármán along any line) correlation.
            var continuous = new double[separations.Length];
            const int steps = 4000;
            double norm = 0;
            for (var s = 0; s < steps; s++)
            {
                var k = kMin * Math.Pow(kMax / kMin, (s + 0.5) / steps);
                var dk = k * Math.Log(kMax / kMin) / steps;
                var e = WindField.RadialSpectrum(k, length) * dk;
                norm += e;
                for (var r = 0; r < separations.Length; r++) continuous[r] += e * BesselJ0(k * separations[r]);
            }
            for (var r = 0; r < separations.Length; r++) continuous[r] /= norm;
            var field0 = new WindField(climate, 3, null);
            Assert.That(norm, Is.EqualTo(field0.ResolvedVariance).Within(0.005), "Modes hold the band's variance.");
            Assert.That(field0.ResolvedVariance, Is.EqualTo(0.75).Within(0.03), "About three quarters of the turbulence lies between 20 m and ten integral lengths.");

            var empirical = new double[separations.Length];
            double variance = 0, cross = 0;
            foreach (var seed in new ulong[] { 3, 5, 7 })
            {
                var field = new WindField(climate, seed, null);
                double modes = 0;
                for (var m = 0; m < WindField.ModesPerComponent; m++) modes += field.ModeVariance(m);
                Assert.That(modes, Is.EqualTo(field.ResolvedVariance).Within(1e-9));
                var random = new DeterministicRandom(seed * 977 + 1);
                for (var p = 0; p < 3000; p++)
                {
                    var x = -5000 + 10000 * random.NextDouble();
                    var y = -5000 + 10000 * random.NextDouble();
                    var angle = 2 * Math.PI * random.NextDouble();
                    field.GustAt(x, y, out var u0, out var v0);
                    variance += u0 * u0;
                    cross += u0 * v0;
                    for (var r = 0; r < separations.Length; r++)
                    {
                        field.GustAt(x + separations[r] * Math.Cos(angle), y + separations[r] * Math.Sin(angle), out var u, out _);
                        empirical[r] += u0 * u;
                    }
                }
            }
            for (var r = 0; r < separations.Length; r++)
            {
                var rho = empirical[r] / variance;
                Assert.That(rho, Is.EqualTo(continuous[r]).Within(0.1), $"Correlation at {separations[r]} m follows the von Kármán spectrum ({continuous[r]:0.00}).");
            }
            Assert.That(empirical[0] / variance, Is.GreaterThan(0.85), "Points 10 m apart feel nearly the same gust (Kolmogorov: about 0.85–0.9).");
            Assert.That(empirical[0] / variance, Is.LessThan(0.999), "…but not exactly the same: the field is not uniform.");
            Assert.That(empirical[separations.Length - 1] / variance, Is.LessThan(0.3), "Gusts 800 m apart are nearly independent.");
            Assert.That(Math.Abs(cross / variance), Is.LessThan(0.1), "Longitudinal and lateral gusts are independent.");
        }

        [Test]
        public void Wind_IslandWakeShelterAndTurbulence()
        {
            var climate = Steady(8);
            var port = new Island { X = 0, Y = 0, Radius = 30, Kind = IslandKind.Settlement };
            var field = new WindField(climate, 1, new List<Island> { port });
            var open = field.Sample(0, 2500, false);
            double Ratio(double x, double y) => field.Sample(x, y, false).MeanSpeed / open.MeanSpeed;
            var wake = climate.WakeLengthPerHeight * IslandRelief.Height(port);

            Assert.That(Ratio(60, 0), Is.InRange(0.6, 0.75), "Two radii behind a 9 m cliff island the wind drops by about a third.");
            Assert.That(Ratio(30 + 3 * wake, 0), Is.GreaterThan(0.95), "Three wake lengths downwind it has recovered.");
            Assert.That(Ratio(-60, 0), Is.InRange(0.93, 1.0), "Upwind it is slowed only a little by blocking.");
            Assert.That(Ratio(0, 39), Is.GreaterThan(1.05), "It speeds up past the flanks.");
            Assert.That(field.Sample(60, 0, false).Turbulence / open.Turbulence, Is.InRange(1.5, 2.0), "The lee is more turbulent: intensity ×1.5–2.");

            var rock = new Island { X = 0, Y = 0, Radius = 16, Kind = IslandKind.Coast };
            var rockField = new WindField(climate, 1, new List<Island> { rock });
            Assert.That(rockField.Sample(32, 0, false).MeanSpeed / open.MeanSpeed, Is.GreaterThan(0.9), "A 1.4 m rock barely shelters the 10 m wind.");
        }

        [Test]
        public void Wind_DragCoefficientFollowsCoare35()
        {
            double[] speeds = { 5, 8, 10, 12, 15 };
            double[] coare = { 0.92e-3, 1.14e-3, 1.32e-3, 1.52e-3, 1.83e-3 };
            for (var i = 0; i < speeds.Length; i++)
                Assert.That(WindField.DragCoefficient(speeds[i]), Is.EqualTo(coare[i]).Within(0.04 * coare[i]), $"Cd at {speeds[i]} m/s");
            WindField.Roughness(1.0, out var uStar, out var z0);
            Assert.That(double.IsNaN(uStar) || double.IsNaN(z0), Is.False, "Light wind stays on the smooth branch, no NaN.");
            Assert.That(z0, Is.GreaterThan(0));
        }

        [Test]
        public void Wind_EtesianPeaksInTheAfternoon()
        {
            var climate = Steady(8);
            climate.DiurnalAmplitude = 0.2;
            climate.StartHour = 14;
            var field = new WindField(climate, 2, null);
            Assert.That(field.SynopticSpeed, Is.EqualTo(9.6).Within(1e-9), "Afternoon maximum: Ū·1.2.");
            field.Advance(12 * 3600);
            Assert.That(field.SynopticSpeed, Is.EqualTo(6.4).Within(1e-9), "Night minimum twelve hours later: Ū·0.8.");
        }

        [Test]
        public void Wind_SameSeedGivesTheSameWindAndAnotherSeedDoesNot()
        {
            var climate = MarineClimate.Aegean(WindRegime.EtesianStrong, AegeanRegion.South);
            climate.CellsPerHour = 60;
            var a = new WindField(climate, 42, null);
            var b = new WindField(climate, 42, null);
            var c = new WindField(climate, 43, null);
            for (var i = 0; i < 1800; i++) { a.Advance(1.0 / 3); b.Advance(1.0 / 3); c.Advance(1.0 / 3); }
            var sa = a.Sample(120, -340);
            var sb = b.Sample(120, -340);
            var sc = c.Sample(120, -340);
            Assert.That(sb.X, Is.EqualTo(sa.X));
            Assert.That(sb.Y, Is.EqualTo(sa.Y));
            Assert.That(a.Cells.Count, Is.EqualTo(b.Cells.Count));
            Assert.That(Math.Abs(sc.X - sa.X) + Math.Abs(sc.Y - sa.Y), Is.GreaterThan(1e-6));
            Assert.That(climate.MeanWind, Is.LessThanOrEqualTo(climate.MaxMeanWind), "No storm in a match.");
        }

        // Waves --------------------------------------------------------------------------------------------------

        const int NF = WaveSpectrum.Frequencies, ND = WaveSpectrum.Directions, NB = WaveSpectrum.Bins;

        static double CellEnergy(WaveField field, int cell)
        {
            double e = 0;
            for (var b = 0; b < NB; b++) e += field.Energy[b * field.Cells + cell];
            return e;
        }

        /// <summary>Energy in a cell travelling toward +X (cos θ &gt; 0) or −X, in a range of frequency bins.</summary>
        static double Toward(WaveField field, int cell, int sign, int i0, int i1)
        {
            double e = 0;
            for (var i = i0; i <= i1; i++)
                for (var j = 0; j < ND; j++)
                    if (sign * WaveSpectrum.DirectionCos[j] > 1e-9) e += field.Energy[(i * ND + j) * field.Cells + cell];
            return e;
        }

        /// <summary>A channel along +X, uniform across (periodic in Y), with no sea coming in: the fetch starts at x = 0.</summary>
        static WaveField Channel(int cells, double size, double depth)
        {
            var grid = new WaveGrid(cells, 1, size, 0, 0, depth) { PeriodicY = true };
            return new WaveField(grid, Steady(10)) { EvolveOuterSea = false };
        }

        [Test]
        public void Waves_FetchGrowthFollowsJonswap()
        {
            const double wind = 10;
            var u = new UniformWind(wind, 0);
            // Game scale: 50 m cells over 3 km of fetch.
            var near = Channel(60, 50, WaveSpectrum.Deep);
            near.InitialiseCalm(u);
            for (var t = 0; t < 4500; t += 6) near.Step(6, u);
            for (var c = 5; c < 60; c += 6)
            {
                var x = (c + 0.5) * 50;
                var law = WaveSpectrum.FetchLimitedEnergy(x, wind);
                Assert.That(CellEnergy(near, c) / law, Is.InRange(0.85, 1.1), $"Energy at {x} m of fetch follows JONSWAP.");
                var peak = WaveSpectrum.PeakFrequency(MarineUnits.Gravity * x / (wind * wind), wind);
                Assert.That(near.PeakFrequency(c) / peak, Is.InRange(0.9, 1.1), $"Peak frequency at {x} m.");
            }
            // Open sea: 1 km cells over 80 km.
            var far = Channel(80, 1000, WaveSpectrum.Deep);
            far.InitialiseCalm(u);
            for (var t = 0; t < 40000; t += 60) far.Step(60, u);
            for (var c = 10; c < 80; c += 10)
            {
                var x = (c + 0.5) * 1000;
                Assert.That(CellEnergy(far, c) / WaveSpectrum.FetchLimitedEnergy(x, wind), Is.InRange(0.9, 1.1), $"Energy at {x / 1000} km of fetch.");
            }
            Assert.That(4 * Math.Sqrt(CellEnergy(far, 60)), Is.InRange(1.1, 1.4), "Hs at 60 km of a 10 m/s wind: about 1.3 m (Aegean summer 1–2 m).");
        }

        [Test]
        public void Waves_DurationGrowthWithin15PercentOfCem()
        {
            const double wind = 10;
            var grid = new WaveGrid(3, 3, 1000, 0, 0, WaveSpectrum.Deep) { PeriodicX = true, PeriodicY = true };
            var field = new WaveField(grid, Steady(wind)) { EvolveOuterSea = false };
            var u = new UniformWind(wind, 0);
            field.InitialiseCalm(u);
            var t = 0.0;
            foreach (var mark in new[] { 1800.0, 3600, 7200, 14400 })
            {
                while (t < mark) { field.Step(10, u); t += 10; }
                var chi = Math.Pow(MarineUnits.Gravity * t / (68.8 * wind), 1.5);
                var cem = WaveSpectrum.FetchLimitedEnergy(chi * wind * wind / MarineUnits.Gravity, wind);
                Assert.That(CellEnergy(field, 4) / cem, Is.InRange(0.85, 1.15), $"After {t / 60} min the sea has the energy of the CEM duration law.");
            }
        }

        [Test]
        public void Waves_ShortWavesAnswerAGustFasterThanLong()
        {
            var grid = new WaveGrid(8, 8, 50, 0, 0, 13);
            var field = new WaveField(grid, Steady(8));
            var u = new UniformWind(8, 0);
            field.Initialise(u);
            const int cell = 27;
            var before = new double[NF];
            for (var i = 0; i < NF; i++) before[i] = field.FrequencyEnergy(cell, i);
            var peak = 0;
            for (var i = 1; i < NF; i++) if (before[i] / WaveSpectrum.Width[i] > before[peak] / WaveSpectrum.Width[peak]) peak = i;

            // A gust of +50% for a minute: the gusty wind rises at once, the mean only through the two-minute filter.
            u.GustX = 4;
            for (var n = 0; n < 15; n++) field.Step(field.StepSeconds, u);
            var shortest = field.Tail(cell, 2) * field.FrequencyEnergy(cell, NF - 1) / before[NF - 1] - 1;
            var shorter = field.Tail(cell, 0) * field.FrequencyEnergy(cell, NF - 3) / before[NF - 3] - 1;
            var dominant = field.FrequencyEnergy(cell, peak) / before[peak] - 1;
            Assert.That(shortest, Is.GreaterThan(0.3), "Waves of about a metre grow by a third within the minute.");
            Assert.That(shorter, Is.LessThan(shortest), "Four-metre waves answer more slowly.");
            Assert.That(shorter, Is.GreaterThan(dominant));
            Assert.That(Math.Abs(dominant), Is.LessThan(0.02), "The dominant waves barely notice a one-minute gust (below 2%).");
        }

        [Test]
        public void Waves_GustChangesPeakEnergyBelow2Percent()
        {
            var grid = new WaveGrid(8, 8, 50, 0, 0, 13);
            var field = new WaveField(grid, Steady(8));
            var u = new UniformWind(8, 0);
            field.Initialise(u);
            const int cell = 27;
            var peak = 0;
            for (var i = 1; i < NF; i++)
                if (field.FrequencyEnergy(cell, i) / WaveSpectrum.Width[i] > field.FrequencyEnergy(cell, peak) / WaveSpectrum.Width[peak]) peak = i;
            var before = field.FrequencyEnergy(cell, peak);
            // A gust strong enough to show in the mean wind for 30 s, then calm again for a minute.
            u.X = 12; u.GustX = 0;
            for (var n = 0; n < 8; n++) field.Step(field.StepSeconds, u);
            u.X = 8;
            for (var n = 0; n < 15; n++) field.Step(field.StepSeconds, u);
            Assert.That(Math.Abs(field.FrequencyEnergy(cell, peak) / before - 1), Is.LessThan(0.02));
        }

        [Test]
        public void Waves_AfterTheWindStopsEnergyTravelsAndDecays()
        {
            var field = Channel(40, 50, WaveSpectrum.Deep);
            var u = new UniformWind(10, 0);
            field.InitialiseCalm(u);
            for (var t = 0; t < 1800; t += 4) field.Step(4, u);
            double Centroid()
            {
                double e = 0, ex = 0;
                for (var c = 0; c < 40; c++) { var v = CellEnergy(field, c); e += v; ex += v * (c + 0.5) * 50; }
                return ex / e;
            }
            var total0 = field.TotalEnergy();
            var centroid0 = Centroid();
            var upwind0 = CellEnergy(field, 2);
            u.X = 0;
            var total = total0;
            for (var n = 0; n < 75; n++)
            {
                field.Step(4, u);
                total = field.TotalEnergy();
                // The waves feel the wind averaged over two minutes, so for a while it still feeds them a little.
                Assert.That(total, Is.LessThan(1.02 * total0), "No new sea without wind.");
                if (n == 14) Assert.That(total / total0, Is.GreaterThan(0.7), "A minute after the wind stops most of the sea is still there.");
            }
            Assert.That(total / total0, Is.LessThan(0.9), "Five minutes later it has clearly decreased.");
            var late = total;
            for (var n = 0; n < 25; n++) field.Step(4, u);
            Assert.That(field.TotalEnergy(), Is.LessThan(late), "…and keeps decreasing.");
            Assert.That(Centroid(), Is.GreaterThan(centroid0 + 50), "The energy keeps travelling downwind.");
            Assert.That(CellEnergy(field, 2), Is.LessThan(0.2 * upwind0), "The upwind end calms first: nothing comes to replace what leaves.");
        }

        [Test]
        public void Waves_WindReversalLeavesTheOldSeaRunning()
        {
            var grid = new WaveGrid(8, 8, 50, 0, 0, 13);
            var field = new WaveField(grid, Steady(10));
            var u = new UniformWind(10, 0);
            field.Initialise(u);
            const int cell = 27;
            var oldLong = Toward(field, cell, 1, 0, 6);
            u.X = -10;
            for (var n = 0; n < 75; n++) field.Step(field.StepSeconds, u);
            Assert.That(Toward(field, cell, 1, 0, 6) / oldLong, Is.GreaterThan(0.8), "Five minutes later the old long waves still run with the old wind.");
            Assert.That(Toward(field, cell, -1, 9, NF - 1), Is.GreaterThan(1e-6), "A new sea grows against them.");
            Assert.That(Toward(field, cell, -1, 0, 6), Is.LessThan(0.1 * oldLong), "The new sea is still young: few long waves yet.");
            // Short waves turn with the wind on the time scale of their growth (Snyder: minutes for metre waves).
            for (var n = 0; n < 225; n++) field.Step(field.StepSeconds, u);
            Assert.That(Toward(field, cell, -1, NF - 1, NF - 1), Is.GreaterThan(Toward(field, cell, 1, NF - 1, NF - 1)), "After twenty minutes the shortest waves run with the new wind.");
            Assert.That(Toward(field, cell, 1, 0, 6) / oldLong, Is.GreaterThan(0.5), "…while the old long waves still run.");
        }

        [Test]
        public void Waves_ShortFetchBehindAnIslandStaysSmaller()
        {
            var grid = new WaveGrid(30, 30, 50, 0, 0, 13);
            for (var y = 13; y <= 16; y++)
                for (var x = 8; x <= 9; x++) grid.SetObstacle(y * 30 + x, 0, 200);
            var field = new WaveField(grid, Steady(10));
            var u = new UniformWind(10, 0);
            field.Initialise(u, 900, 4);
            double Hs(int x, int y) => field.SignificantHeight(y * 30 + x);
            var lee = Hs(12, 14);
            var open = Hs(12, 4);
            Assert.That(lee / open, Is.LessThan(0.7), "Right behind the island the sea is smaller.");
            Assert.That(Hs(28, 14) / open, Is.GreaterThan(lee / open), "Further on the shadow fills in.");
            Assert.That(field.PeakFrequency(14 * 30 + 12), Is.GreaterThan(field.PeakFrequency(4 * 30 + 12)), "In the lee the waves are younger and shorter.");
        }

        [Test]
        public void Waves_IslandShadowMatchesTransparency()
        {
            // A wall of cells letting through half the flux, crossed by a swell of 10 s along +X.
            var grid = new WaveGrid(20, 1, 50, 0, 0, WaveSpectrum.Deep) { PeriodicY = true };
            grid.SetObstacle(10, 0.5, 1000);
            var field = new WaveField(grid, Steady(10)) { EvolveOuterSea = false };
            var calm = new UniformWind(0, 0);
            field.InitialiseCalm(calm);
            const int bin = 1 * ND;
            field.Outer[bin] = 0.01;
            for (var n = 0; n < 200; n++) field.Step(4, calm);
            var before = field.Energy[bin * grid.Cells + 8];
            var after = field.Energy[bin * grid.Cells + 12];
            Assert.That(after / before, Is.EqualTo(0.5).Within(0.03), "Behind the wall half the energy flux remains.");

            // A small rock (20 m) hardly shades 160 m waves: they bend around it.
            var rockGrid = new WaveGrid(20, 1, 50, 0, 0, WaveSpectrum.Deep) { PeriodicY = true };
            rockGrid.SetObstacle(10, 0.5, 20);
            var rock = new WaveField(rockGrid, Steady(10)) { EvolveOuterSea = false };
            rock.InitialiseCalm(calm);
            rock.Outer[bin] = 0.01;
            for (var n = 0; n < 200; n++) rock.Step(4, calm);
            Assert.That(rock.Energy[bin * rockGrid.Cells + 12] / rock.Energy[bin * rockGrid.Cells + 8], Is.GreaterThan(0.75));
        }

        /// <summary>A beach: 80 cells of 25 m, 13 m deep at x = 0 shoaling to 1 m at 2 km, with a 10 s swell coming in.</summary>
        static WaveField Beach(bool slope)
        {
            var grid = new WaveGrid(80, 1, 25, 0, 0, 13) { PeriodicY = true };
            for (var c = 0; c < 80; c++) grid.Depth[c] = slope ? 13 - 12 * (c + 0.5) / 80 : 13;
            var field = new WaveField(grid, Steady(10)) { EvolveOuterSea = false };
            field.InitialiseCalm(new UniformWind(0, 0));
            field.Outer[1 * ND] = 0.01; // Hs 0.4 m in deep water, 0.0985 Hz, toward +X
            return field;
        }

        [Test]
        public void Waves_ShallowWaterChangesSpeedAndHeight()
        {
            var calm = new UniformWind(0, 0);
            var beach = Beach(true);
            var flat = Beach(false);
            const int target = 72; // 1.8 km, about 2.2 m deep on the beach
            var beachSeries = new double[400];
            var flatSeries = new double[400];
            for (var n = 0; n < 400; n++)
            {
                beach.Step(2, calm);
                flat.Step(2, calm);
                beachSeries[n] = beach.Energy[ND * beach.Cells + target];
                flatSeries[n] = flat.Energy[ND * flat.Cells + target];
            }
            // Arrival: half of each place's final energy (the front, whatever the shoaling).
            int Arrival(double[] series) { var n = 0; while (series[n] < 0.5 * series[series.Length - 1]) n++; return n + 1; }
            var f = WaveSpectrum.Frequency[1];
            double slow = 0;
            for (var c = 0; c <= target; c++) slow += 25 / WaveSpectrum.GroupSpeed(f, beach.Depth(c));
            var expectedDelay = slow / ((target + 1) * 25 / WaveSpectrum.GroupSpeed(f, 13));
            var delay = Arrival(beachSeries) / (double)Arrival(flatSeries);
            Assert.That(delay, Is.GreaterThan(1.15), "Waves slow down as the water gets shallow.");
            Assert.That(delay, Is.EqualTo(expectedDelay).Within(0.15 * expectedDelay), "…by the group speed of finite depth.");

            // Shoaling: the energy flux is kept, so the height grows as the group speed falls (before breaking).
            var deepCell = 2;
            var shallowCell = 64; // about 3.3 m
            var expected = Math.Sqrt(WaveSpectrum.GroupSpeed(f, beach.Depth(deepCell)) / WaveSpectrum.GroupSpeed(f, beach.Depth(shallowCell)));
            var ratio = beach.SignificantHeight(shallowCell) / beach.SignificantHeight(deepCell);
            Assert.That(ratio, Is.GreaterThan(1.2), "Waves rise over the shoal.");
            Assert.That(ratio, Is.EqualTo(expected).Within(0.1 * expected), "…as much as the group speed falls.");
            double highest = 0;
            for (var c = 0; c < 80; c++) highest = Math.Max(highest, beach.SignificantHeight(c));
            Assert.That(beach.SignificantHeight(79), Is.LessThan(0.8 * highest), "In the last metre of water they have broken.");
        }

        [Test]
        public void Waves_SteepnessAndDepthRaiseTheRightBreaking()
        {
            var calm = new UniformWind(0, 0);
            var beach = Beach(true);
            for (var n = 0; n < 400; n++) beach.Step(2, calm);
            Assert.That(beach.SurfBreaking(10), Is.EqualTo(0), "No depth breaking in 11 m of water.");
            var previous = 0.0;
            for (var c = 60; c < 80; c++)
            {
                Assert.That(beach.SurfBreaking(c), Is.GreaterThanOrEqualTo(previous - 1e-9), "More waves break as the depth falls.");
                previous = beach.SurfBreaking(c);
            }
            Assert.That(previous, Is.GreaterThan(0.05));

            // Deep water: a young, steep sea breaks more often than an older one (Banner et al. 2000).
            var u = new UniformWind(10, 0);
            var channel = Channel(60, 50, WaveSpectrum.Deep);
            channel.InitialiseCalm(u);
            for (var t = 0; t < 4500; t += 6) channel.Step(6, u);
            Assert.That(channel.BreakingProbability(5), Is.GreaterThan(channel.BreakingProbability(55)), "Short fetch, steeper waves, more breaking.");
            Assert.That(channel.BreakingProbability(5), Is.GreaterThan(0));
            Assert.That(channel.SurfBreaking(30), Is.EqualTo(0), "Deep water: no depth breaking.");
        }

        [Test]
        public void Waves_CflIsKeptBySubsteps()
        {
            var calm = new UniformWind(0, 0);
            WaveField Swell()
            {
                var grid = new WaveGrid(40, 1, 50, 0, 0, 13) { PeriodicY = true };
                var field = new WaveField(grid, Steady(10)) { EvolveOuterSea = false };
                field.InitialiseCalm(calm);
                field.Outer[0] = 0.02; // 0.08 Hz toward +X: the fastest bin
                return field;
            }
            var coarse = Swell();
            var fine = Swell();
            Assert.That(coarse.Courant(12), Is.GreaterThan(1), "A 12 s step would break the Courant limit without sub-steps.");
            for (var n = 0; n < 12; n++) coarse.Step(12, calm);
            for (var n = 0; n < 36; n++) fine.Step(4, calm);
            // Coming in from deep water the swell shoals a little into 13 m: the energy flux is kept.
            var shoaled = 0.02 * WaveSpectrum.GroupSpeed(WaveSpectrum.Frequency[0], WaveSpectrum.Deep) / WaveSpectrum.GroupSpeed(WaveSpectrum.Frequency[0], 13);
            for (var c = 0; c < 40; c++)
            {
                var a = coarse.Energy[c];
                var b = fine.Energy[c];
                Assert.That(double.IsNaN(a) || a < 0, Is.False);
                Assert.That(a, Is.LessThanOrEqualTo(shoaled * 1.001), "No overshoot.");
                Assert.That(Math.Abs(a - b), Is.LessThan(0.002), "Sub-steps give the same front as small steps.");
            }
        }

        [Test]
        public void Waves_TransportConservesEnergy()
        {
            var grid = new WaveGrid(30, 30, 50, 0, 0, WaveSpectrum.Deep) { PeriodicX = true, PeriodicY = true };
            var field = new WaveField(grid, Steady(10)) { EvolveOuterSea = false };
            var calm = new UniformWind(0, 0);
            field.InitialiseCalm(calm);
            // A gentle swell blob of 8 s waves in three directions.
            foreach (var j in new[] { 0, 3, 6 })
                for (var y = 12; y < 18; y++)
                    for (var x = 12; x < 18; x++)
                        field.Energy[(2 * ND + j) * grid.Cells + y * 30 + x] = 0.004f;
            field.EnergyChanged();
            field.Step(4, calm);
            var start = field.TotalEnergy();
            for (var n = 0; n < 100; n++) field.Step(4, calm);
            Assert.That(field.TotalEnergy() / start, Is.EqualTo(1).Within(1e-3), "Transport moves energy without creating or losing it.");
        }

        [Test]
        public void Waves_SlicedStepEqualsWholeStepAndRestoresMidStep()
        {
            WaveField Make()
            {
                var islands = new List<Island> { new Island { X = 180, Y = 210, Radius = 40, Kind = IslandKind.Settlement } };
                var grid = WaveGrid.FromIslands(islands, 10, 10, 50, 0, 0, 13);
                return new WaveField(grid, Steady(9));
            }
            var u = new UniformWind(6, 6);
            var whole = Make();
            var sliced = Make();
            whole.Initialise(u, 40, 4);
            sliced.Initialise(u, 40, 4);
            for (var n = 0; n < 3; n++) whole.Step(whole.StepSeconds, u);
            for (var n = 0; n < 3 * WaveField.TicksPerStep; n++) sliced.Tick(u);
            Assert.That(sliced.Steps, Is.EqualTo(whole.Steps));
            for (var k = 0; k < whole.Energy.Length; k++) Assert.That(sliced.Energy[k], Is.EqualTo(whole.Energy[k]), "Ticks and whole steps give the same numbers.");

            // Saved halfway through a step: the parts already done are done again from the saved samples.
            for (var n = 0; n < 50; n++) sliced.Tick(u);
            var copy = Make();
            copy.Initialise(u, 0, 4);
            Array.Copy(sliced.Energy, copy.Energy, sliced.Energy.Length);
            Array.Copy(sliced.WindX, copy.WindX, sliced.WindX.Length);
            Array.Copy(sliced.WindY, copy.WindY, sliced.WindY.Length);
            Array.Copy(sliced.Foam, copy.Foam, sliced.Foam.Length);
            Array.Copy(sliced.TailFactor, copy.TailFactor, sliced.TailFactor.Length);
            Array.Copy(sliced.Outer, copy.Outer, sliced.Outer.Length);
            Array.Copy(sliced.SampleX, copy.SampleX, sliced.SampleX.Length);
            Array.Copy(sliced.SampleY, copy.SampleY, sliced.SampleY.Length);
            Array.Copy(sliced.GustSample, copy.GustSample, sliced.GustSample.Length);
            copy.SynopticX = sliced.SynopticX;
            copy.SynopticY = sliced.SynopticY;
            copy.Steps = sliced.Steps;
            copy.Slice = sliced.Slice;
            copy.Restore();
            for (var n = 0; n < 2 * WaveField.TicksPerStep; n++) { sliced.Tick(u); copy.Tick(u); }
            for (var k = 0; k < sliced.Energy.Length; k++) Assert.That(copy.Energy[k], Is.EqualTo(sliced.Energy[k]), "A restored step continues identically.");
            for (var k = 0; k < sliced.Foam.Length; k++) Assert.That(copy.Foam[k], Is.EqualTo(sliced.Foam[k]));
        }

        // Sea surface -------------------------------------------------------------------------------------------------

        /// <summary>A sea with a shadow behind a block of land, so its energy varies from cell to cell.</summary>
        static WaveField ShadowedSea(out SeaSurface surface)
        {
            var grid = new WaveGrid(14, 14, 50, 0, 0, 13);
            for (var y = 6; y <= 7; y++) grid.SetObstacle(y * 14 + 4, 0, 100);
            var field = new WaveField(grid, Steady(10));
            field.Initialise(new UniformWind(10, 0), 400, 4);
            surface = new SeaSurface(field, 77, null);
            return field;
        }

        [Test]
        public void Sea_NoSeamsInHeightOrPhase()
        {
            ShadowedSea(out var surface);
            const double step = 0.25, y = 337, time = 123.4;
            double largestJump = 0, largestSlope = 0, largestHsJump = 0;
            var previous = surface.Sample(260, y, time, 0.5);
            for (var x = 260 + step; x <= 640; x += step)
            {
                var sample = surface.Sample(x, y, time, 0.5);
                largestJump = Math.Max(largestJump, Math.Abs(sample.Height - previous.Height));
                largestSlope = Math.Max(largestSlope, Math.Abs(sample.SlopeX));
                largestHsJump = Math.Max(largestHsJump, Math.Abs(sample.SignificantHeight - previous.SignificantHeight));
                previous = sample;
            }
            Assert.That(largestSlope, Is.GreaterThan(0.01), "The line crosses real waves.");
            Assert.That(largestJump, Is.LessThan(1.5 * largestSlope * step + 1e-6), "Across seven cell borders the height never jumps.");
            Assert.That(largestHsJump, Is.LessThan(0.01), "The sea state changes smoothly from cell to cell.");

            // A single component: its crests are the same everywhere, cell borders or not.
            var grid = new WaveGrid(6, 6, 50, 0, 0, 13) { PeriodicX = true, PeriodicY = true };
            var field = new WaveField(grid, Steady(10)) { EvolveOuterSea = false };
            field.InitialiseCalm(new UniformWind(0, 0));
            var bin = 4 * ND + 2;
            for (var c = 0; c < grid.Cells; c++) field.Energy[bin * grid.Cells + c] = 0.02f;
            field.EnergyChanged();
            var single = new SeaSurface(field, 5, null);
            var a = Math.Sqrt(2 * 0.02);
            for (var x = 10.0; x < 290; x += 7.3)
            {
                var expected = a * Math.Cos(single.WaveNumberX[bin] * x + single.WaveNumberY[bin] * 40 - single.Omega[bin] * time + single.Phase[bin]);
                Assert.That(single.Sample(x, 40, time, 0).Height, Is.EqualTo(expected).Within(1e-6));
            }
        }

        [Test]
        public void Sea_HeightContinuousAcrossWaveSteps()
        {
            var field = ShadowedSea(out var surface);
            var wind = new UniformWind(12, 3);
            double largestJump = 0, largestRate = 0;
            var tick = 0;
            var previous = surface.Sample(500, 300, 0, surface.BlendWeight());
            for (var n = 0; n < 3 * WaveField.TicksPerStep; n++)
            {
                field.Tick(wind);
                tick++;
                var sample = surface.Sample(500, 300, tick * Rules.Dt, surface.BlendWeight());
                largestJump = Math.Max(largestJump, Math.Abs(sample.Height - previous.Height));
                largestRate = Math.Max(largestRate, Math.Abs(sample.VelocityZ));
                previous = sample;
            }
            Assert.That(field.Steps, Is.EqualTo(3), "Three wave steps were crossed.");
            Assert.That(largestJump, Is.LessThan(1.5 * largestRate * Rules.Dt + 1e-6), "No step in the height when a wave step ends.");
        }

        [Test]
        public void Sea_CrestSpeedIsOmegaOverK()
        {
            ShadowedSea(out var surface);
            for (var c = 0; c < SeaSurface.Components; c++)
            {
                var k = surface.WaveNumber[c];
                var omega = surface.Omega[c];
                Assert.That(omega * omega, Is.EqualTo(MarineUnits.Gravity * k * Math.Tanh(k * SeaSurface.ReferenceDepth)).Within(1e-9 * omega * omega), "Linear dispersion at 13 m.");
                // A crest moves along k at ω/k: the phase at (x + c·t·k̂, t) equals the phase at (x, 0).
                var speed = omega / k;
                var moved = surface.WaveNumberX[c] / k * speed * 10 * surface.WaveNumberX[c] + surface.WaveNumberY[c] / k * speed * 10 * surface.WaveNumberY[c] - omega * 10;
                Assert.That(moved, Is.EqualTo(0).Within(1e-9));
            }
            Assert.That(surface.WaveNumber[0], Is.LessThan(surface.WaveNumber[SeaSurface.Components - 1]), "Lower frequencies, longer waves.");
            Assert.That(2 * Math.PI / surface.WaveNumber[SeaSurface.Components - 1], Is.GreaterThan(7), "The shortest physical waves are about 8 m long.");
        }

        // Ships in the water ----------------------------------------------------------------------------------------------

        static readonly ShipClass[] Classes = (ShipClass[])Enum.GetValues(typeof(ShipClass));

        /// <summary>A uniform sea of one component: frequency bin and direction bin, given energy.</summary>
        static SeaSurface OneWave(int frequency, int direction, double energy, out int component)
        {
            var grid = new WaveGrid(4, 4, 100, -200, -200, 13) { PeriodicX = true, PeriodicY = true };
            var field = new WaveField(grid, Steady(0)) { EvolveOuterSea = false };
            field.InitialiseCalm(new UniformWind(0, 0));
            component = frequency * ND + direction;
            for (var c = 0; c < grid.Cells; c++) field.Energy[component * grid.Cells + c] = (float)energy;
            field.EnergyChanged();
            return new SeaSurface(field, 3, null);
        }

        /// <summary>Largest heave, roll and pitch over a run after a settling time, seconds of simulation.</summary>
        static (double heave, double roll, double pitch) Response(HullForm hull, SeaSurface surface, double heading, double seconds = 90, double settle = 30)
        {
            var motion = Seakeeping.AtRest();
            var model = new Seakeeping();
            double heave = 0, roll = 0, pitch = 0;
            var ticks = (int)(seconds * Rules.TickRate);
            for (var n = 0; n < ticks; n++)
            {
                model.Step(hull, ref motion, surface, 0, 0, heading, n * Rules.Dt, 0, Rules.Dt, 0, 0, false);
                if (n < settle * Rules.TickRate) continue;
                heave = Math.Max(heave, Math.Abs(motion.Heave));
                roll = Math.Max(roll, Math.Abs(motion.Roll));
                pitch = Math.Max(pitch, Math.Abs(motion.Pitch));
            }
            return (heave, roll, pitch);
        }

        [Test]
        public void Seakeeping_EveryClassFloatsAtItsCatalogDraft()
        {
            foreach (var shipClass in Classes)
            {
                var definition = ShipCatalog.Get(shipClass);
                var hull = HullForm.For(shipClass, 0, MarineClimate.Default);
                Assert.That(hull.Draft, Is.EqualTo(definition.Draft).Within(1e-9), $"{shipClass}: draft from the catalogue.");
                Assert.That(hull.Metacentric, Is.GreaterThan(0), $"{shipClass}: stable.");
                Assert.That(hull.Mass / 1000, Is.InRange(2, 400), $"{shipClass}: a displacement of a few to a few hundred tonnes.");
                double buoyancy = 0;
                for (var i = 0; i < HullForm.Stations; i++) buoyancy += hull.SectionArea(i, hull.Draft) * hull.StationLength;
                Assert.That(buoyancy * hull.WaterDensity * MarineUnits.Gravity, Is.EqualTo(hull.Weight).Within(1e-6 * hull.Weight), $"{shipClass}: buoyancy at the draft carries the weight.");
                // Dropped 20 cm too deep and heeled 5°, it comes back to its draft and upright.
                var motion = new ShipMotion { Heave = -0.2, Roll = 0.087 };
                var model = new Seakeeping();
                for (var n = 0; n < 90 * Rules.TickRate; n++) model.Step(hull, ref motion, null, 0, 0, 0, n * Rules.Dt, 0, Rules.Dt, 0, 0, false);
                Assert.That(motion.Heave, Is.EqualTo(0).Within(0.01), $"{shipClass}: floats at its draft.");
                Assert.That(motion.Roll, Is.EqualTo(0).Within(0.005), $"{shipClass}: upright (roll is lightly damped: 0.3° after 90 s).");
            }
            var loaded = HullForm.For(ShipClass.Merchant, 1, MarineClimate.Default);
            var empty = HullForm.For(ShipClass.Merchant, 0, MarineClimate.Default);
            Assert.That(loaded.Draft, Is.EqualTo(empty.Draft * (1 + Rules.DraftPerLoad)).Within(1e-9), "A full hold sits deeper, as navigation assumes.");
            Assert.That(loaded.Mass, Is.GreaterThan(empty.Mass), "…because it carries the cargo's weight.");
        }

        [Test]
        public void Seakeeping_NaturalPeriodsWithin5PercentOfAnalytic()
        {
            foreach (var shipClass in new[] { ShipClass.Cutter, ShipClass.Ram, ShipClass.Merchant, ShipClass.Bomber })
            {
                var hull = HullForm.For(shipClass, 0, MarineClimate.Default);
                double Period(ShipMotion start, Func<ShipMotion, double> read)
                {
                    var motion = start;
                    var model = new Seakeeping();
                    var crossings = new List<double>();
                    var previous = read(motion);
                    for (var n = 1; n < 120 * Rules.TickRate && crossings.Count < 3; n++)
                    {
                        model.Step(hull, ref motion, null, 0, 0, 0, n * Rules.Dt, 0, Rules.Dt, 0, 0, false);
                        var value = read(motion);
                        if (previous < 0 && value >= 0) crossings.Add(n * Rules.Dt - Rules.Dt * value / (value - previous));
                        previous = value;
                    }
                    return crossings.Count < 2 ? double.NaN : crossings[1] - crossings[0];
                }
                // Damped periods: longer than the natural ones by 1/√(1 − ζ²), 3% at ζ = 0.25.
                Assert.That(Period(new ShipMotion { Heave = 0.02 }, m => m.Heave), Is.EqualTo(hull.HeavePeriod * 1.033).Within(0.05 * hull.HeavePeriod), $"{shipClass}: heave period");
                Assert.That(Period(new ShipMotion { Pitch = 0.004 }, m => m.Pitch), Is.EqualTo(hull.PitchPeriod * 1.033).Within(0.05 * hull.PitchPeriod), $"{shipClass}: pitch period");
                Assert.That(Period(new ShipMotion { Roll = 0.02 }, m => m.Roll), Is.EqualTo(hull.RollPeriod).Within(0.05 * hull.RollPeriod), $"{shipClass}: roll period");
            }
            var cutter = HullForm.For(ShipClass.Cutter, 0, MarineClimate.Default);
            Assert.That(cutter.HeavePeriod, Is.InRange(2.0, 3.2), "With added mass the 12 m cutter heaves at about 2.6 s, not 1.3 s.");
        }

        [Test]
        public void Seakeeping_ResponseLimitsAtLongAndShortWaves()
        {
            var hull = HullForm.For(ShipClass.Ram, 0, MarineClimate.Default);
            // Waves of 160–240 m: the hull rides them.
            var swell = OneWave(0, 0, 0.02, out var component);
            var amplitude = Math.Sqrt(2 * 0.02);
            var longWaves = Response(hull, swell, Math.PI);
            Assert.That(2 * Math.PI / swell.WaveNumber[component], Is.GreaterThan(5 * hull.Length));
            Assert.That(longWaves.heave / amplitude, Is.InRange(0.85, 1.15), "Long waves: the ship rises and falls with them.");
            // Waves of about 9 m on a 17.6 m waterline: they cancel along the hull.
            var chop = OneWave(8, 0, 0.02, out component);
            var shortWaves = Response(hull, chop, Math.PI);
            Assert.That(2 * Math.PI / chop.WaveNumber[component], Is.LessThan(0.7 * hull.Length));
            Assert.That(shortWaves.heave / amplitude, Is.LessThan(0.3), "Short waves: the hull barely heaves.");
            Assert.That(shortWaves.pitch, Is.LessThan(longWaves.pitch + 0.02));
        }

        [Test]
        public void Seakeeping_ResponseDependsOnWavelengthAndHeading()
        {
            var hull = HullForm.For(ShipClass.Ram, 0, MarineClimate.Default);
            // Waves of about 45 m travelling toward +X.
            var sea = OneWave(4, 0, 0.03, out _);
            var head = Response(hull, sea, Math.PI);
            var beam = Response(hull, sea, Math.PI / 2);
            Assert.That(beam.roll, Is.GreaterThan(3 * head.roll), "Beam seas roll the ship, head seas hardly.");
            Assert.That(head.pitch, Is.GreaterThan(2 * beam.pitch), "Head seas pitch it, beam seas hardly.");
            Assert.That(beam.roll * 180 / Math.PI, Is.LessThan(25), "No capsizing roll in a moderate sea.");
            // Pitch depends on the wavelength against the hull: 45 m waves pitch more than 9 m chop of the same energy.
            var chop = Response(hull, OneWave(8, 0, 0.03, out _), Math.PI);
            Assert.That(head.pitch, Is.GreaterThan(chop.pitch));
        }

        [Test]
        public void Seakeeping_MeanRollEqualsStaticHeel()
        {
            var climate = Steady(10);
            foreach (var shipClass in new[] { ShipClass.Ram, ShipClass.Merchant })
            {
                var hull = HullForm.For(shipClass, 0, climate);
                var wind = new WindField(climate, 4, null).Sample(0, 0, false);
                // Wind toward +X on the beam of a ship heading +Y: toward her starboard side, which goes down (roll &gt; 0: port up).
                var forces = SailForces.Compute(hull, Math.PI / 2, 0, 0, wind, climate.AirDensity);
                var expected = Seakeeping.StaticHeel(hull, forces.HeelMoment);
                var motion = Seakeeping.AtRest();
                var model = new Seakeeping();
                double mean = 0;
                var ticks = 120 * Rules.TickRate;
                for (var n = 0; n < ticks; n++)
                {
                    model.Step(hull, ref motion, null, 0, 0, Math.PI / 2, n * Rules.Dt, 0, Rules.Dt, forces.HeelMoment, forces.TrimMoment, false);
                    if (n >= ticks - 20 * Rules.TickRate) mean += motion.Roll / (20 * Rules.TickRate);
                }
                Assert.That(mean * 180 / Math.PI, Is.EqualTo(expected * 180 / Math.PI).Within(1), $"{shipClass}: mean heel is the static heel.");
                Assert.That(Math.Abs(expected) * 180 / Math.PI, Is.InRange(0.5, 15), $"{shipClass}: a beam wind of 10 m/s heels a few degrees.");
                Assert.That(expected, Is.GreaterThan(0), "Heeled away from the wind: the lee side down.");
            }
        }

        [Test]
        public void Planar_SailHelpsDownwindAndSlowsUpwind()
        {
            var climate = Steady(10);
            var hull = HullForm.For(ShipClass.Ram, 0, climate);
            var wind = new WindField(climate, 4, null).Sample(0, 0, false); // toward +X
            var running = SailForces.Compute(hull, 0, 0, 0, wind, climate.AirDensity);
            var reaching = SailForces.Compute(hull, Math.PI / 2, 0, 0, wind, climate.AirDensity);
            var heading = SailForces.Compute(hull, Math.PI, 0, 0, wind, climate.AirDensity);
            Assert.That(running.SailSet && running.Drive > 0, "Wind from astern drives her.");
            Assert.That(reaching.SailSet && reaching.Drive > 0, "A beam wind drives her too, with the yard braced.");
            Assert.That(Math.Abs(reaching.YardAngle), Is.GreaterThan(Math.PI / 6), "…braced round toward the wind.");
            Assert.That(running.Drive, Is.GreaterThan(reaching.Drive), "A square sail drives best before the wind.");
            Assert.That(heading.SailSet, Is.False, "Into the wind the sail is furled.");
            Assert.That(heading.Drive, Is.LessThan(0), "…and the wind on the hull holds her back.");
            Assert.That(Math.Abs(reaching.Side), Is.GreaterThan(Math.Abs(running.Side)), "A beam wind pushes her sideways.");
        }

        [Test]
        public void Sails_TheCrewShortensSailToKeepTheHeelWithinTheGuide()
        {
            foreach (var speed in new[] { 4.0, 10.0, 15.0, 20.0 })
            {
                var climate = Steady(speed);
                var wind = new WindField(climate, 4, null).Sample(0, 0, false); // toward +X
                foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
                {
                    var hull = HullForm.For(shipClass, 0, climate);
                    var limit = SailForces.HeelLimit(hull);
                    var gunwale = Math.Atan(hull.Freeboard / (hull.Beam / 2));
                    Assert.That(limit, Is.EqualTo(Math.Min(16 * Math.PI / 180, 0.8 * gunwale)).Within(1e-12), "IMO's guide: 16° or 80% of the gunwale's angle.");
                    // Beam, broad reach, before the wind.
                    foreach (var heading in new[] { Math.PI / 2, Math.PI / 4, 0.0 })
                    {
                        var forces = SailForces.Compute(hull, heading, 0, 0, wind, climate.AirDensity);
                        if (!forces.SailSet) continue;
                        var heel = Math.Abs(Seakeeping.StaticHeel(hull, forces.HeelMoment));
                        Assert.That(heel, Is.LessThanOrEqualTo(limit + 1e-9), $"{shipClass} at {speed} m/s: her sail never heels her past the guide.");
                        Assert.That(forces.SailShare, Is.InRange(0.0, 1.0));
                        if (speed <= 4) Assert.That(forces.SailShare, Is.EqualTo(1.0), $"{shipClass}: all sail set in a light wind.");
                    }
                }
            }

            // A cutter in a strong beam wind sails on with her sail shortened; the full sail would heel her past the guide.
            var strong = Steady(15);
            var cutter = HullForm.For(ShipClass.Cutter, 0, strong);
            var sample = new WindField(strong, 4, null).Sample(0, 0, false);
            var shortened = SailForces.Compute(cutter, Math.PI / 2, 0, 0, sample, strong.AirDensity);
            Assert.That(shortened.SailSet, "She keeps some sail.");
            Assert.That(shortened.SailShare, Is.InRange(0.05, 0.99), "…brailed up, not all of it.");
            Assert.That(Math.Abs(Seakeeping.StaticHeel(cutter, shortened.HeelMoment)), Is.EqualTo(SailForces.HeelLimit(cutter)).Within(1e-6), "She heels to the guide and no further.");
        }

        [Test]
        public void Sea_HullSamplingEqualsTwoLineSamples()
        {
            var world = MarineMatch(3, false, MarineClimate.Aegean(WindRegime.EtesianStrong, AegeanRegion.Central));
            var surface = world.Marine.Surface;
            const int n = HullForm.Stations;
            double[] Fresh() => new double[n];
            foreach (var draft in new[] { 0.0, 0.7, 2.2 })
                foreach (var heading in new[] { 0.0, 1.1, 2.9 })
                    foreach (var time in new[] { 0.0, 37.5 })
                    {
                        double ex = Math.Cos(heading), ey = Math.Sin(heading), x = -300 + 50 * heading, y = -100 + 30 * draft;
                        double px = x - ey * 0.9, py = y + ex * 0.9, sx = x + ey * 0.9, sy = y - ex * 0.9;
                        double[] ph = Fresh(), pr = Fresh(), sh = Fresh(), sr = Fresh();
                        surface.SampleLine(px, py, ex * 1.4, ey * 1.4, n, time, 0.3, draft, ph, pr);
                        surface.SampleLine(sx, sy, ex * 1.4, ey * 1.4, n, time, 0.3, draft, sh, sr, SeaSurface.LineFloor, true);
                        double[] ph2 = Fresh(), pr2 = Fresh(), sh2 = Fresh(), sr2 = Fresh();
                        surface.SampleHull(px, py, sx, sy, ex * 1.4, ey * 1.4, n, time, 0.3, draft, ph2, pr2, sh2, sr2);
                        Assert.That(ph2, Is.EqualTo(ph), "Port heights, bit for bit.");
                        Assert.That(pr2, Is.EqualTo(pr), "Port rates, bit for bit.");
                        Assert.That(sh2, Is.EqualTo(sh), "Starboard heights, bit for bit.");
                        Assert.That(sr2, Is.EqualTo(sr), "Starboard rates, bit for bit.");
                    }
            Assert.That(surface.Amplitudes(-300, -100, 0.3, new double[SeaSurface.Components]), Is.GreaterThan(0.3), "Compared in a real sea.");
        }

        [Test]
        public void Seakeeping_ThreeTickStepsStayCloseToEveryTickSteps()
        {
            // The far view's approximation: a hull stepped every third tick over three ticks against the same hull stepped
            // every tick, under way in a strong Etesian sea.
            var world = MarineMatch(3, false, MarineClimate.Aegean(WindRegime.EtesianStrong, AegeanRegion.Central));
            var surface = world.Marine.Surface;
            foreach (var shipClass in new[] { ShipClass.Cutter, ShipClass.Ram })
                foreach (var headingDeg in new[] { 0.0, 90.0 })
                {
                    var hull = HullForm.For(shipClass, 0, world.Marine.Climate);
                    var heading = headingDeg * Math.PI / 180;
                    Seakeeping fine = new Seakeeping(), coarse = new Seakeeping();
                    ShipMotion a = default, b = default;
                    fine.Settle(hull, ref a, surface, -300, -100, heading, 0, 0.5);
                    coarse.Settle(hull, ref b, surface, -300, -100, heading, 0, 0.5);
                    double diff = 0, motion = 0, rollDiff = 0, roll = 0;
                    for (var t = 1; t <= 60 * Rules.TickRate; t++)
                    {
                        var x = -300 + 4 * Math.Cos(heading) * t * Rules.Dt;
                        var y = -100 + 4 * Math.Sin(heading) * t * Rules.Dt;
                        fine.Step(hull, ref a, surface, x, y, heading, t * Rules.Dt, 0.5, Rules.Dt, 0, 0, true);
                        if (t % SeakeepingStride != 0) continue;
                        coarse.Step(hull, ref b, surface, x, y, heading, t * Rules.Dt, 0.5, SeakeepingStride * Rules.Dt, 0, 0, true);
                        if (t < 10 * Rules.TickRate) continue;
                        diff += (a.Heave - b.Heave) * (a.Heave - b.Heave);
                        motion += a.Heave * a.Heave;
                        rollDiff += (a.Roll - b.Roll) * (a.Roll - b.Roll);
                        roll += a.Roll * a.Roll;
                    }
                    var samples = (60 - 10) * Rules.TickRate / SeakeepingStride;
                    Assert.That(Math.Sqrt(motion / samples), Is.GreaterThan(0.1), $"{shipClass}: a real sea heaves her.");
                    Assert.That(Math.Sqrt(diff / motion), Is.LessThan(0.2), $"{shipClass} at {headingDeg}°: heave within a fifth of the motion.");
                    Assert.That(Math.Sqrt(rollDiff / roll), Is.LessThan(0.2), $"{shipClass} at {headingDeg}°: roll within a fifth of the motion.");
                }
        }

        // SeakeepingDriver.FarStride (presentation): the test assembly sees only the simulation.
        const int SeakeepingStride = 3;

        // The match: one clock, one owner of the ships, saved --------------------------------------------------------

        static World MarineMatch(ulong seed, bool movement, MarineClimate climate = null)
        {
            var world = MapFactory.CreateSkirmish(seed, MatchLength.Short);
            world.EnableMarine(climate ?? MarineClimate.Aegean(WindRegime.EtesianModerate, AegeanRegion.Central), seed, movement, 40);
            return world;
        }

        /// <summary>Every 5 s each ship is sent 80 m in a direction that changes with time and its id.</summary>
        static void Sail(World world, int ticks)
        {
            for (var n = 0; n < ticks; n++)
            {
                if (world.Tick % 150 == 0)
                    foreach (var ship in world.Ships)
                        world.Issue(new Command { Faction = ship.Owner, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = ship.X + 80 * Math.Cos(world.Tick / 150.0 + ship.Id), Y = ship.Y + 80 * Math.Sin(world.Tick / 150.0 + ship.Id) });
                world.Step();
            }
        }

        [Test]
        public void Marine_SameSeedSameSeaAndSaveContinuesIdentically()
        {
            var a = MarineMatch(31, true);
            var b = MarineMatch(31, true);
            Sail(a, 437);
            Sail(b, 437);
            var saved = SaveGame.Serialize(a);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(b)), Is.EqualTo(SaveGame.Hash(saved)), "Same seed: the same wind, sea and ships.");
            Assert.That(a.Marine.Waves.Slice, Is.Not.EqualTo(0), "Saved in the middle of a wave step.");
            var loaded = SaveGame.Deserialize(saved, out _);
            Assert.That(loaded.Marine, Is.Not.Null);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(saved)), "Loading changes nothing.");
            Sail(a, 400);
            Sail(loaded, 400);
            Assert.That(SaveGame.Hash(SaveGame.Serialize(loaded)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(a))), "A loaded match continues identically.");
            for (var k = 0; k < a.Marine.Waves.Energy.Length; k++)
                if (a.Marine.Waves.Energy[k] != loaded.Marine.Waves.Energy[k]) Assert.Fail("The sea differs after loading.");
            var other = MarineMatch(32, true);
            Sail(other, 437);
            Assert.That(other.Marine.Wind.Sample(0, 0).X, Is.Not.EqualTo(b.Marine.Wind.Sample(0, 0).X), "Another seed, another wind.");
        }

        [Test]
        public void Planar_SwitchOffLeavesMovementUnchanged()
        {
            var plain = MapFactory.CreateSkirmish(9, MatchLength.Short);
            var marine = MapFactory.CreateSkirmish(9, MatchLength.Short);
            marine.EnableMarine(MarineClimate.Aegean(WindRegime.EtesianStrong, AegeanRegion.South), 9, false, 40);
            Sail(plain, 600);
            Sail(marine, 600);
            Assert.That(marine.Ships.Count, Is.EqualTo(plain.Ships.Count));
            for (var i = 0; i < plain.Ships.Count; i++)
            {
                Assert.That(marine.Ships[i].X, Is.EqualTo(plain.Ships[i].X), "Without the switch the wind moves no ship.");
                Assert.That(marine.Ships[i].Y, Is.EqualTo(plain.Ships[i].Y));
                Assert.That(marine.Ships[i].Heading, Is.EqualTo(plain.Ships[i].Heading));
                Assert.That(marine.Ships[i].Speed, Is.EqualTo(plain.Ships[i].Speed));
            }
            Assert.That(marine.Random.State, Is.EqualTo(plain.Random.State), "The sea draws nothing from the game's random numbers.");
        }

        /// <summary>A ship alone on open water sent 700 m along a heading, with the wind moving ships.</summary>
        static Ship Underway(ShipClass shipClass, double heading, MarineClimate climate, out World world)
        {
            world = new World(5);
            // Ports with stores, so neither side has lost: a lone ship on open water.
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 0, X = -1200, Y = -1200, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            world.Settlements.Add(new Settlement { Id = world.NextId++, Owner = 1, X = 1200, Y = 1200, Wood = 2000 * Rules.Centi, Silver = 1500 * Rules.Centi });
            var ship = world.SpawnShip(0, shipClass, 0, 0);
            ship.Heading = heading;
            world.EnableMarine(climate, 5, true, 40);
            world.Issue(new Command { Faction = 0, Kind = CommandKind.Move, ShipIds = new[] { ship.Id }, X = 700 * Math.Cos(heading), Y = 700 * Math.Sin(heading) });
            for (var n = 0; n < 15 * Rules.TickRate; n++) world.Step();
            return ship;
        }

        [Test]
        public void Planar_WindChangesSpeedAndDriftWhenSwitchedOn()
        {
            var climate = Steady(12); // toward +X: Etesian at its strongest, no storm
            foreach (var shipClass in new[] { ShipClass.Ram, ShipClass.Bomber })
            {
                var downwind = Underway(shipClass, 0, climate, out _);
                var upwind = Underway(shipClass, Math.PI, climate, out _);
                var baseSpeed = ShipCatalog.Get(shipClass).Speed * World.CrewSpeedFactor(downwind);
                Assert.That(downwind.WindSpeedFactor, Is.GreaterThan(1.005), $"{shipClass}: the sail helps before the wind.");
                Assert.That(upwind.WindSpeedFactor, Is.LessThan(0.95), $"{shipClass}: the wind on the hull slows her into it.");
                Assert.That(downwind.WindSpeedFactor, Is.LessThanOrEqualTo(1.25), "C09: at most a quarter faster.");
                Assert.That(upwind.WindSpeedFactor, Is.GreaterThanOrEqualTo(0.75), "C09: at most a quarter slower.");
                Assert.That(downwind.Speed, Is.EqualTo(baseSpeed * downwind.WindSpeedFactor).Within(0.02 * baseSpeed), $"{shipClass}: the speed she settles at.");
                Assert.That(upwind.Speed, Is.LessThan(downwind.Speed));
            }
            var slow = Underway(ShipClass.Bomber, 0, climate, out _);
            var fast = Underway(ShipClass.Ram, 0, climate, out _);
            Assert.That(slow.WindSpeedFactor, Is.GreaterThan(fast.WindSpeedFactor), "The sail matters more to a slow hull.");

            var beam = Underway(ShipClass.Ram, Math.PI / 2, climate, out _);
            Assert.That(beam.DriftX, Is.GreaterThan(0), "A beam wind sets her to leeward.");
            Assert.That(beam.X, Is.GreaterThan(1), "…so she ends up off her course, downwind.");
            Assert.That(climate.DriftFraction * 12, Is.LessThanOrEqualTo(MarineState.CurrentSpeedShare * ShipCatalog.Get(ShipClass.Ram).Speed), "C09: the current is at most a fifth of the base speed.");
        }

        [Test]
        public void Planar_MeanAddedResistanceInWaves()
        {
            Underway(ShipClass.Ram, 0, Steady(12), out var world); // waves running toward +X
            var hull = HullForm.For(ShipClass.Ram, 0, world.Marine.Climate);
            var headSeas = world.Marine.AddedResistance(hull, 0, 0, Math.PI);
            var following = world.Marine.AddedResistance(hull, 0, 0, 0);
            var beamSeas = world.Marine.AddedResistance(hull, 0, 0, Math.PI / 2);
            Assert.That(headSeas, Is.GreaterThan(0), "Waves met head on hold the ship back (STAWAVE-1).");
            Assert.That(following, Is.EqualTo(0), "Waves from astern do not.");
            Assert.That(beamSeas, Is.LessThan(0.2 * headSeas), "Waves from the beam hardly.");
            var cell = world.Marine.Waves.Grid.CellAt(0, 0);
            var height = world.Marine.Waves.SignificantHeight(cell);
            var upper = world.Marine.Climate.WaterDensity * MarineUnits.Gravity * height * height * hull.Beam * Math.Sqrt(hull.Beam / (0.25 * hull.Length)) / 16;
            Assert.That(headSeas, Is.LessThanOrEqualTo(upper), "No more than the formula gives for the whole sea coming from ahead.");
        }

        [Test]
        public void Marine_MatchWeatherFollowsTheAegeanSummerAndIsNeverAStorm()
        {
            var counts = new Dictionary<WindRegime, int>();
            for (ulong seed = 1; seed <= 4000; seed++)
            {
                var climate = MarineClimate.ForMatch(seed * 7919);
                counts[climate.Regime] = (counts.TryGetValue(climate.Regime, out var n) ? n : 0) + 1;
                Assert.That(climate.Regime, Is.Not.EqualTo(WindRegime.StormShowcase), "No storm in a match.");
                Assert.That(climate.MeanWind, Is.LessThanOrEqualTo(climate.MaxMeanWind));
            }
            int Share(WindRegime regime) => counts.TryGetValue(regime, out var n) ? n : 0;
            Assert.That(Share(WindRegime.EtesianModerate) + Share(WindRegime.EtesianStrong), Is.InRange(2600, 3000), "About 70% Etesian days (35–65% of July–August, more in the afternoon hours played).");
            Assert.That(Share(WindRegime.Calm), Is.InRange(300, 500), "About one match in ten calm.");
            var world = MapFactory.CreateSkirmish(77, MatchLength.Short);
            var state = world.Random.State;
            world.EnableMarineForMatch();
            Assert.That(world.Random.State, Is.EqualTo(state), "Choosing the weather draws nothing from the game's random numbers.");
            Assert.That(world.Marine.Climate.Name, Is.EqualTo(MarineClimate.ForMatch(state).Name));
        }

        [Test]
        public void Marine_WavePhasesUseSimulationTimeAtAnyWeatherScale()
        {
            var slow = MarineClimate.Aegean(WindRegime.EtesianModerate, AegeanRegion.Central);
            var fast = slow.Copy();
            fast.WeatherTimeScale = 3;
            var a = new World(1);
            var b = new World(1);
            a.EnableMarine(slow, 1, false, 0);
            b.EnableMarine(fast, 1, false, 0);
            for (var n = 0; n < 300; n++) { a.Step(); b.Step(); }
            Assert.That(b.Marine.SurfaceTime(), Is.EqualTo(a.Marine.SurfaceTime()), "Crests move at ω/k whatever the weather's acceleration.");
            Assert.That(b.Marine.Wind.Time, Is.EqualTo(3 * a.Marine.Wind.Time).Within(1e-9), "The weather itself runs three times faster.");
            Assert.That(b.Marine.Waves.Steps, Is.EqualTo(a.Marine.Waves.Steps), "Wave steps stay on the simulation clock…");
            Assert.That(b.Marine.Waves.StepSeconds, Is.EqualTo(3 * a.Marine.Waves.StepSeconds).Within(1e-9), "…each one covering three times the weather time.");
        }
    }
}

