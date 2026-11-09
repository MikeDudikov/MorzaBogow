using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Turns water heights under a hull into heave, pitch and roll (C09, V06). Presentation only: the logical
    /// position, ranges and collisions never read these values, so waves cannot change a game result.
    /// </summary>
    public readonly struct HullPose
    {
        public const float MaxTiltDegrees = 12f;

        public readonly float Heave, Pitch, Roll;

        public HullPose(float heave, float pitch, float roll)
        {
            Heave = heave;
            Pitch = pitch;
            Roll = roll;
        }

        /// <summary>A higher bow gives negative pitch around X; a higher starboard gives positive roll around Z.</summary>
        public static HullPose FromHeights(float bow, float stern, float port, float starboard, float center, float length, float beam)
        {
            var heave = (bow + stern + port + starboard + center) / 5f;
            var pitch = -Mathf.Atan2(bow - stern, Mathf.Max(length, 0.1f)) * Mathf.Rad2Deg;
            var roll = Mathf.Atan2(starboard - port, Mathf.Max(beam, 0.1f)) * Mathf.Rad2Deg;
            return new HullPose(heave, Mathf.Clamp(pitch, -MaxTiltDegrees, MaxTiltDegrees), Mathf.Clamp(roll, -MaxTiltDegrees, MaxTiltDegrees));
        }
    }

    /// <summary>One degree of freedom with stiffness and damping: it lags behind, overshoots and settles.</summary>
    public struct BuoyancySpring
    {
        public float Value, Velocity;

        /// <summary>Semi-implicit steps of at most 20 ms keep it stable at any frame time.</summary>
        public void Step(float target, float period, float damping, float delta)
        {
            if (delta <= 0f) return;
            var omega = 2f * Mathf.PI / Mathf.Max(period, 0.05f);
            var steps = Mathf.Max(1, Mathf.CeilToInt(delta / 0.02f));
            var h = delta / steps;
            for (var i = 0; i < steps; i++)
            {
                var acceleration = omega * omega * (target - Value) - 2f * damping * omega * Velocity;
                Velocity += acceleration * h;
                Value += Velocity * h;
            }
        }
    }

    /// <summary>
    /// A floating hull is a mass on the water, not a point glued to it: it rises and rolls with a delay that grows
    /// with its size, overshoots a passing crest and settles. Natural periods follow length for heave and pitch
    /// and beam for roll, so a cutter bobs quickly and a merchant rolls slowly. Presentation only (C09, V06).
    /// </summary>
    public sealed class HullMotion
    {
        BuoyancySpring heave, pitch, roll;

        public HullMotion(float length, float beam)
        {
            HeavePeriod = 1.0f + 0.06f * length;
            PitchPeriod = 0.9f + 0.05f * length;
            RollPeriod = 1.4f + 0.25f * beam;
        }

        public float HeavePeriod { get; }
        public float PitchPeriod { get; }
        public float RollPeriod { get; }
        public float HeaveVelocity => heave.Velocity;
        public HullPose Pose => new HullPose(heave.Value, pitch.Value, roll.Value);

        public void Reset(HullPose pose)
        {
            heave = new BuoyancySpring { Value = pose.Heave };
            pitch = new BuoyancySpring { Value = pose.Pitch };
            roll = new BuoyancySpring { Value = pose.Roll };
        }

        public HullPose Step(HullPose water, float delta)
        {
            heave.Step(water.Heave, HeavePeriod, 0.55f, delta);
            pitch.Step(water.Pitch, PitchPeriod, 0.45f, delta);
            roll.Step(water.Roll, RollPeriod, 0.3f, delta);
            pitch.Value = Mathf.Clamp(pitch.Value, -HullPose.MaxTiltDegrees, HullPose.MaxTiltDegrees);
            roll.Value = Mathf.Clamp(roll.Value, -HullPose.MaxTiltDegrees, HullPose.MaxTiltDegrees);
            return Pose;
        }

        /// <summary>
        /// How far the water at the stem stands above the stem itself, in metres. Positive when the bow drives into
        /// a crest, which is where water breaks against the hull.
        /// </summary>
        public static float BowImmersion(HullPose hull, float bowWater, float length) =>
            bowWater - (hull.Heave + Mathf.Tan(-hull.Pitch * Mathf.Deg2Rad) * length * 0.5f);
    }

    /// <summary>Samples the real HDRP water surface, so ships move with the waves the player sees.</summary>
    public sealed class WaterSampler
    {
        readonly WaterSurface surface;

        public WaterSampler(WaterSurface surface)
        {
            this.surface = surface;
        }

        public long Samples { get; private set; }
        public long Failures { get; private set; }

        public bool TryHeight(Vector3 position, out float height)
        {
            var search = new WaterSearchParameters
            {
                targetPositionWS = new Unity.Mathematics.float3(position.x, 0f, position.z),
                startPositionWS = new Unity.Mathematics.float3(position.x, 0f, position.z),
                error = 0.05f,
                maxIterations = 4,
                // Bow waves and other local deformation count too, so a ship rides another's wake and its own bow wave.
                includeDeformation = true,
                excludeSimulation = false,
            };
            if (surface != null && surface.ProjectPointOnWaterSurface(search, out var result))
            {
                height = result.projectedPositionWS.y;
                Samples++;
                return true;
            }
            height = 0f;
            Failures++;
            return false;
        }
    }
}
