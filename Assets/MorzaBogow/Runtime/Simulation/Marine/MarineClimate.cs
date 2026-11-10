using System;

namespace MorzaBogow.Simulation
{
    /// <summary>Weather of a match. Storm is for the showcase and stage 5 only: a match stays at Beaufort 7 or below.</summary>
    public enum WindRegime { Calm, Breeze, EtesianModerate, EtesianStrong, Sirocco, StormShowcase }

    /// <summary>Part of the Aegean the map stands for; the Etesian blows from NE/NNE in the north, N in the centre, NNW–NW in the south.</summary>
    public enum AegeanRegion { North, Central, South }

    /// <summary>
    /// Parameters of the marine model for one match, with their sources (docs/MORZE_I_WIATR.md). Map north is +Y in
    /// the simulation (+Z in Unity); bearings are clockwise from north, as in meteorology ("from" directions).
    /// Values marked [W] in the documentation are inferences from the sources, not measurements.
    /// </summary>
    public sealed class MarineClimate
    {
        public string Name = "Etezja umiarkowana";
        public WindRegime Regime = WindRegime.EtesianModerate;

        // Water and air, summer surface of the Aegean: S = 39, T = 25 °C (EOS-80) [W]; 26 °C, 1010 hPa [W].
        public double WaterDensity = 1026.3;
        public double AirDensity = 1.165;

        // Synoptic wind at 10 m: daily mean, the bearing it blows from, slow variability (Ornstein–Uhlenbeck).
        public double MeanWind = 8.0;
        public double FromBearingDegrees;
        public double DirectionSigmaDegrees = 12;
        public double DirectionTau = 3600;
        public double SpeedSigma = 0.10;
        public double SpeedTau = 1800;

        /// <summary>No storm in a match: the mean wind stays at or below Beaufort 7.</summary>
        public double MaxMeanWind = 15;

        // Diurnal cycle of the Etesian: U = Ū·[1 + A·cos(2π(t − peak)/24 h)] (Prezerakos 2022, Koletsis 2009) [W].
        public double DiurnalAmplitude = 0.2;
        public double DiurnalPeakHour = 14;
        public double StartHour = 11;

        // Turbulence at 10 m over the sea: I = base·(1 + slope·U)·stability (ISO 19901-1; stable summer layer ×0.8) [W].
        public double TurbulenceBase = 0.06;
        public double TurbulenceSlope = 0.043;
        public double Stability = 0.8;

        /// <summary>Integral length scale of the longitudinal gusts at 10 m over sea (EN 1991-1-4), metres.</summary>
        public double GustLength = 96;

        /// <summary>Lateral to longitudinal gust standard deviation.</summary>
        public double LateralRatio = 0.75;

        // Mesoscale areas of stronger and weaker wind, carried by the wind; finite life with a smooth envelope.
        public double CellsPerHour = 6;
        public double CellRadiusMin = 250, CellRadiusMax = 700;
        public double CellAmplitude = 0.15;
        public double CellLifeMin = 300, CellLifeMax = 1200;

        // Island wakes: length in island heights, deficit at the island, speed-up at the flanks, extra turbulence (Koletsis 2009/2010, Smith 1997) [W].
        // WakeTurbulence 1 gives turbulence intensity ×1.5–2 close behind a high island (gust factors 1.5–1.6 in the lee, Koletsis 2009).
        public double WakeLengthPerHeight = 20;
        public double WakeDeficit = 0.4;
        public double FlankJet = 0.15;
        public double WakeTurbulence = 1.0;

        // Waves: fetch of the open sea beyond the map edge (Poulos 2025: 42–295 km in the central Aegean) [W], growth-law calibration.
        public double OuterFetch = 60000;
        public double GrowthCalibration = 1.0;

        /// <summary>Explicit acceleration of weather development (never a hidden change of units). Wave phases ignore it.</summary>
        public double WeatherTimeScale = 1.0;

        /// <summary>Wind drift of the surface water as a fraction of U10 (NOAA: 3%, range 1–6%); movement switch only.</summary>
        public double DriftFraction = 0.03;

        /// <summary>Wind in the simulation plane: the direction the air moves toward, radians from +X toward +Y.</summary>
        public double TowardAngle => Math.PI / 2 - (FromBearingDegrees + 180) * Math.PI / 180;

        public double TurbulenceIntensity(double speed) => TurbulenceBase * (1 + TurbulenceSlope * speed) * Stability;

        public MarineClimate Copy() => (MarineClimate)MemberwiseClone();

        // Saved field by field: a match keeps the climate it started with.
        double[] Numbers() => new[]
        {
            WaterDensity, AirDensity, MeanWind, FromBearingDegrees, DirectionSigmaDegrees, DirectionTau, SpeedSigma, SpeedTau, MaxMeanWind,
            DiurnalAmplitude, DiurnalPeakHour, StartHour, TurbulenceBase, TurbulenceSlope, Stability, GustLength, LateralRatio,
            CellsPerHour, CellRadiusMin, CellRadiusMax, CellAmplitude, CellLifeMin, CellLifeMax,
            WakeLengthPerHeight, WakeDeficit, FlankJet, WakeTurbulence, OuterFetch, GrowthCalibration, WeatherTimeScale, DriftFraction,
        };

        internal void Write(System.IO.BinaryWriter writer)
        {
            writer.Write(Name ?? string.Empty);
            writer.Write((int)Regime);
            var numbers = Numbers();
            writer.Write(numbers.Length);
            foreach (var value in numbers) writer.Write(value);
        }

        internal static MarineClimate Read(System.IO.BinaryReader reader)
        {
            var climate = new MarineClimate { Name = reader.ReadString(), Regime = (WindRegime)reader.ReadInt32() };
            var count = reader.ReadInt32();
            if (count != climate.Numbers().Length) throw new System.IO.InvalidDataException("Damaged climate data.");
            var n = new double[count];
            for (var i = 0; i < count; i++) n[i] = reader.ReadDouble();
            var k = 0;
            climate.WaterDensity = n[k++]; climate.AirDensity = n[k++]; climate.MeanWind = n[k++]; climate.FromBearingDegrees = n[k++];
            climate.DirectionSigmaDegrees = n[k++]; climate.DirectionTau = n[k++]; climate.SpeedSigma = n[k++]; climate.SpeedTau = n[k++];
            climate.MaxMeanWind = n[k++]; climate.DiurnalAmplitude = n[k++]; climate.DiurnalPeakHour = n[k++]; climate.StartHour = n[k++];
            climate.TurbulenceBase = n[k++]; climate.TurbulenceSlope = n[k++]; climate.Stability = n[k++]; climate.GustLength = n[k++];
            climate.LateralRatio = n[k++]; climate.CellsPerHour = n[k++]; climate.CellRadiusMin = n[k++]; climate.CellRadiusMax = n[k++];
            climate.CellAmplitude = n[k++]; climate.CellLifeMin = n[k++]; climate.CellLifeMax = n[k++]; climate.WakeLengthPerHeight = n[k++];
            climate.WakeDeficit = n[k++]; climate.FlankJet = n[k++]; climate.WakeTurbulence = n[k++]; climate.OuterFetch = n[k++];
            climate.GrowthCalibration = n[k++]; climate.WeatherTimeScale = n[k++]; climate.DriftFraction = n[k++];
            return climate;
        }

        public static MarineClimate Default => Aegean(WindRegime.EtesianModerate, AegeanRegion.Central);

        /// <summary>
        /// Summer weather of the central Aegean for a match, drawn from its seed without touching the game's random
        /// numbers: calm 10%, breeze 20%, moderate Etesian 50%, strong Etesian 20% (Etesian days are 35–65% of July and
        /// August: Gandham 2025, Koletsis 2009). Never a storm in a match.
        /// </summary>
        public static MarineClimate ForMatch(ulong seed)
        {
            var draw = (MarineHash.SplitMix64(seed) >> 11) * (1.0 / (1UL << 53));
            var regime = draw < 0.1 ? WindRegime.Calm : draw < 0.3 ? WindRegime.Breeze : draw < 0.8 ? WindRegime.EtesianModerate : WindRegime.EtesianStrong;
            return Aegean(regime, AegeanRegion.Central);
        }

        /// <summary>
        /// Summer Aegean weather. Etesian means: 7–8 m/s in the central and south Aegean, 4.5–6.7 in the north
        /// (Soukissian 2008, Badger 2015); strong episodes 11–15 m/s with gusts to 20 (Koletsis 2009/2010).
        /// </summary>
        public static MarineClimate Aegean(WindRegime regime, AegeanRegion region)
        {
            var etesianFrom = region == AegeanRegion.North ? 25.0 : region == AegeanRegion.South ? 335.0 : 0.0;
            var climate = new MarineClimate { Regime = regime, FromBearingDegrees = etesianFrom };
            switch (regime)
            {
                case WindRegime.Calm:
                    climate.Name = "Cisza";
                    climate.MeanWind = 1.5;
                    climate.DirectionSigmaDegrees = 60;
                    climate.DiurnalAmplitude = 0;
                    climate.CellsPerHour = 2;
                    break;
                case WindRegime.Breeze:
                    climate.Name = "Bryza";
                    climate.MeanWind = 4.5;
                    climate.DirectionSigmaDegrees = 30;
                    climate.DiurnalAmplitude = 0.3;
                    break;
                case WindRegime.EtesianModerate:
                    climate.Name = "Etezja umiarkowana";
                    climate.MeanWind = region == AegeanRegion.North ? 6.0 : 8.0;
                    break;
                case WindRegime.EtesianStrong:
                    climate.Name = "Etezja silna";
                    climate.MeanWind = region == AegeanRegion.North ? 10.5 : 12.5;
                    climate.DirectionSigmaDegrees = 8;
                    climate.CellAmplitude = 0.12;
                    break;
                case WindRegime.Sirocco:
                    climate.Name = "Sirocco";
                    climate.MeanWind = 7.0;
                    climate.FromBearingDegrees = 150;
                    climate.DirectionSigmaDegrees = 20;
                    climate.DiurnalAmplitude = 0.1;
                    break;
                case WindRegime.StormShowcase:
                    climate.Name = "Sztorm (tylko pokaz)";
                    climate.MeanWind = 20.0;
                    climate.MaxMeanWind = 24;
                    climate.DiurnalAmplitude = 0;
                    climate.CellAmplitude = 0.2;
                    break;
            }
            return climate;
        }
    }

    /// <summary>
    /// Height of the land above the water by island kind, as the presentation builds it (ShoreShape: mound crown
    /// 2.1 m, port cliffs to 9.1 m; a coast rock is the Coast_Rocks_02 model at about 1.4 m above the water).
    /// Map data for the island wakes, not a measurement.
    /// </summary>
    public static class IslandRelief
    {
        public static double Height(Island island)
        {
            switch (island.Kind)
            {
                case IslandKind.Settlement: return 9.1;
                case IslandKind.Hinterland: return 9.1;
                case IslandKind.ResourceSite: return 2.1;
                default: return 1.4;
            }
        }
    }
}
