using System;

namespace MorzaBogow.Simulation
{
    /// <summary>
    /// Physical units of the marine model in one place: metres, seconds, metres per second, kilograms, newtons,
    /// kilograms per cubic metre. The world is laid out in metres (GAME_DESIGN: one world unit is one metre), so the
    /// conversion is the identity, but every marine formula goes through it so a change of scale changes one number.
    /// </summary>
    public static class MarineUnits
    {
        /// <summary>Metres in one world unit.</summary>
        public const double MetresPerUnit = 1.0;

        /// <summary>Normal gravity near 38° N (the Aegean), m/s².</summary>
        public const double Gravity = 9.80;

        public const double VonKarman = 0.40;

        /// <summary>Kinematic viscosity of air at about 26 °C, m²/s (COARE roughness).</summary>
        public const double AirViscosity = 1.6e-5;

        /// <summary>Kinematic viscosity of sea water at 25 °C, m²/s.</summary>
        public const double WaterViscosity = 0.94e-6;

        /// <summary>Reference height of the wind that drives the waves (U10), metres.</summary>
        public const double ReferenceHeight = 10.0;

        public static double Metres(double units) => units * MetresPerUnit;

        public static double Units(double metres) => metres / MetresPerUnit;

        /// <summary>Simulation time of a tick, seconds.</summary>
        public static double Seconds(int tick) => tick * Rules.Dt;
    }

    /// <summary>
    /// Deterministic numbers for the marine model. Per-index values (gust modes, wave phases) come from a hash of the
    /// marine seed, so they need no saved state; processes that evolve (background wind, wind cells) draw from a
    /// saved DeterministicRandom seeded from the same seed. Nothing here depends on frames or on UnityEngine.Random.
    /// </summary>
    public static class MarineHash
    {
        public static ulong SplitMix64(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        public static ulong Mix(ulong seed, ulong stream, ulong index) => SplitMix64(SplitMix64(seed ^ (stream * 0xD1B54A32D192ED03UL)) ^ index);

        /// <summary>A number in [0, 1) for (seed, stream, index).</summary>
        public static double Unit(ulong seed, ulong stream, ulong index) => (Mix(seed, stream, index) >> 11) * (1.0 / (1UL << 53));

        /// <summary>A standard normal number for (seed, stream, index), Box–Muller from two hashed uniforms.</summary>
        public static double Normal(ulong seed, ulong stream, ulong index)
        {
            var u1 = Math.Max(1e-12, Unit(seed, stream, index * 2));
            var u2 = Unit(seed, stream, index * 2 + 1);
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        public static double Normal(ref DeterministicRandom random)
        {
            var u1 = Math.Max(1e-12, random.NextDouble());
            var u2 = random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        // Streams, so the same seed gives unrelated sequences for each use.
        public const ulong GustModes = 1, GustLateral = 2, WavePhases = 3, WaveJitter = 4, Process = 5;
    }
}
