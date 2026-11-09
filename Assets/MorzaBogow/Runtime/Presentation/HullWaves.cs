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
                includeDeformation = false,
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
