using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The one stroke of a ship's oars and of the men who pull them. Phase 0 is the catch: the blade, forward, enters the
    /// water. Through the drive (the first Drive part of the cycle) the blade sweeps aft in the water while the rower leans
    /// back and pulls; at the finish it comes out, and through the recovery it returns forward above the water while he
    /// leans forward again. Sweep runs from -1 (blade forward) to +1 (blade aft); lift is below zero in the water and above
    /// it in the air. The oars (ShipVisual) and the rowers (crew) read the same phase, so blade and body move together.
    /// Rates are strokes a minute, after the sea trials of the reconstructed trireme Olympias (an analogy for a later,
    /// larger ship; docs/art/ZALOGI.md): about 20 to 30 cruising, near 40 at a sprint.
    /// </summary>
    public static class RowingStroke
    {
        /// <summary>Share of the cycle the blade pulls through the water.</summary>
        public const float Drive = 0.42f;

        /// <summary>How far the oar swings fore and aft about its thole (each way), and how far the blade rises and dips.</summary>
        public const float SweepDegrees = 24f, LiftDegrees = 9f;

        /// <summary>Strokes a minute: cruising from slow to full speed, turning, a sprint before a ram, backing water.</summary>
        public const float CruiseSlow = 18f, CruiseFast = 28f, Turning = 16f, Sprint = 40f, Astern = 20f;

        /// <summary>Where blade and loom are at a phase of the stroke (0 to 1, the catch at 0).</summary>
        public static void Sample(float phase, out float sweep, out float lift)
        {
            phase = Mathf.Repeat(phase, 1f);
            if (phase < Drive)
            {
                var u = phase / Drive;
                sweep = -Mathf.Cos(Mathf.PI * u);
                // In at the catch, buried through the drive, out at the finish.
                lift = Mathf.Lerp(0.25f, -1f, Smooth(0f, 0.14f, u));
                lift = Mathf.Lerp(lift, 0.35f, Smooth(0.88f, 1f, u));
            }
            else
            {
                var v = (phase - Drive) / (1f - Drive);
                sweep = Mathf.Cos(Mathf.PI * v);
                // Feathered high on the way back, lowered again to meet the water.
                lift = Mathf.Lerp(0.35f, 1f, Smooth(0f, 0.3f, v));
                lift = Mathf.Lerp(lift, 0.25f, Smooth(0.7f, 1f, v));
            }
        }

        /// <summary>True while the blade pulls through the water.</summary>
        public static bool Driving(float phase) => Mathf.Repeat(phase, 1f) < Drive;

        /// <summary>
        /// Strokes a minute for a ship's state: none at rest, steady at sea by its speed, slower in a hard turn, a sprint in a
        /// ram's charge, and backing water when it pulls astern.
        /// </summary>
        public static float RateFor(float speedFraction, bool turningHard, bool charging)
        {
            var speed = Mathf.Abs(speedFraction);
            if (speed < 0.08f) return 0f;
            if (speedFraction < -0.02f) return Astern;
            if (charging) return Sprint;
            var cruise = Mathf.Lerp(CruiseSlow, CruiseFast, Mathf.Clamp01(speed));
            return turningHard ? Mathf.Min(cruise, Turning + 6f * speed) : cruise;
        }

        static float Smooth(float a, float b, float x)
        {
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
