using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Orthographic view from an angle: parallel lines stay parallel and a ship keeps its size anywhere on screen.
    /// The player changes zoom, rotation and tilt. Presentation only.
    /// </summary>
    public static class ObliqueCamera
    {
        public const float DefaultTilt = 45f;
        public const float DefaultYaw = 30f;
        public const float MinTilt = 25f;
        public const float MaxTilt = 80f;

        /// <summary>Half of the visible screen height in meters for each meter of the zoom range (C10: 20-300).</summary>
        const float SizePerZoomMeter = 0.6f;

        public const float SunHeight = 60f;
        public const float SunHeightAtMaxTilt = 45f;
        public const float SunYawOffset = 120f;

        public static float ClampTilt(float tilt) => Mathf.Clamp(tilt, MinTilt, MaxTilt);

        /// <summary>
        /// The sun turns with the view, so a rotation can never line the camera up with its mirror image on the
        /// water, which would turn the whole sea white. It also sinks as the view steepens, for the same reason.
        /// Shadows turn with the view too.
        /// </summary>
        public static Quaternion SunRotation(float yaw, float tilt)
        {
            var height = Mathf.Lerp(SunHeight, SunHeightAtMaxTilt, Mathf.InverseLerp(DefaultTilt, MaxTilt, tilt));
            return Quaternion.Euler(height, yaw + SunYawOffset, 0f);
        }

        /// <summary>Ground depth looks shorter on screen; scrolling by this factor keeps both screen axes equally fast.</summary>
        public static float GroundDepthScale(float tilt) => 1f / Mathf.Sin(ClampTilt(tilt) * Mathf.Deg2Rad);

        public static void Apply(Camera camera, Vector3 pivot, float yaw, float tilt, float zoom)
        {
            var size = zoom * SizePerZoomMeter;
            camera.orthographic = true;
            camera.orthographicSize = size;

            // Distance does not change the picture here; it only keeps the near plane above the water and models.
            var view = Quaternion.Euler(ClampTilt(tilt), yaw, 0f);
            camera.transform.SetPositionAndRotation(pivot - view * Vector3.forward * (150f + size * 2.5f), view);
        }
    }
}
