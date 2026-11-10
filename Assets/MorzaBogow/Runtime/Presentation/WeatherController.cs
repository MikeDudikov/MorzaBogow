using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The state the weather gives HDRP's sea: the wind of the large waves and of the ripples, both in km/h (HDRP turns
    /// them into m/s itself), and the chaos of the large waves from 0 to 1.
    /// </summary>
    public readonly struct SeaState : IEquatable<SeaState>
    {
        public readonly float Wind, Chaos, Ripples;

        public SeaState(float wind, float chaos, float ripples)
        {
            Wind = wind;
            Chaos = chaos;
            Ripples = ripples;
        }

        /// <summary>The sea a water surface has now.</summary>
        public static SeaState Of(WaterSurface surface) => new SeaState(surface.largeWindSpeed, surface.largeChaos, surface.ripplesWindSpeed);

        /// <summary>Gives a water surface this sea.</summary>
        public void ApplyTo(WaterSurface surface)
        {
            surface.largeWindSpeed = Wind;
            surface.largeChaos = Chaos;
            surface.ripplesWindSpeed = Ripples;
        }

        /// <summary>Between two seas; exactly the first at 0 and the second at 1, so a finished change ends on its target.</summary>
        public static SeaState Lerp(SeaState a, SeaState b, float t) =>
            t <= 0f ? a : t >= 1f ? b
                : new SeaState(Mathf.Lerp(a.Wind, b.Wind, t), Mathf.Lerp(a.Chaos, b.Chaos, t), Mathf.Lerp(a.Ripples, b.Ripples, t));

        public bool Equals(SeaState other) => Wind == other.Wind && Chaos == other.Chaos && Ripples == other.Ripples;
        public override bool Equals(object obj) => obj is SeaState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Wind, Chaos, Ripples);
        public override string ToString() => FormattableString.Invariant($"wind {Wind:0.##} km/h, chaos {Chaos:0.###}, ripples {Ripples:0.##} km/h");
    }

    /// <summary>
    /// Weather as presentation only. One intensity, from 0 for the scene's calm to 1 for a storm, sets the wind and chaos
    /// of HDRP's sea, the fog and exposure of an overlay volume and the strength and colour of the sun; SetWeather moves
    /// it there smoothly over real time. The world never reads it: ranges, sight, movement and the fog of war stay as
    /// they are. The overlay has its own profile, made in memory for the battle, so the scene's volume and its profile
    /// assets are never changed, and switching the picture profile (which swaps the scene's profile) keeps working.
    /// In a match the weather stays calm; stage 5 decides when storms come.
    /// </summary>
    public sealed class WeatherController : MonoBehaviour
    {
        /// <summary>HDRP rebuilds the sea's spectrum when its wind changes, so the water takes new values at most four times a second.</summary>
        public const float SeaInterval = 0.25f;

        /// <summary>The scene's sea when there is no water surface to read it from: HDRP's defaults, which the battle scene keeps.</summary>
        public static readonly SeaState DefaultCalmSea = new SeaState(30f, 0.8f, 8f);

        /// <summary>A storm: twice the wind of the calm scene, choppy and fully ruffled.</summary>
        public static readonly SeaState StormSea = new SeaState(60f, 0.95f, 14f);

        /// <summary>The sun behind storm clouds gives about a third of its light, and cooler light.</summary>
        public const float StormSunFactor = 0.35f;
        public const float StormSunTemperature = 7000f;

        /// <summary>
        /// Exposure opens by this many EV in a storm, so the picture darkens by less than the sun dims and the fleet stays
        /// readable.
        /// </summary>
        public const float StormExposureShift = -0.75f;

        /// <summary>
        /// Storm haze: its mean free path in metres, the height of its layer and the tint it gives the sky colour. The
        /// strategic camera looks down through the whole layer, so the haze is about a third and ships stay visible.
        /// </summary>
        public const float StormFogDistance = 250f;
        public const float StormFogHeight = 90f;
        public static readonly Color StormFogTint = new Color(0.55f, 0.6f, 0.66f);

        // HDRP's own values for a scene volume that does not set them (Fog.cs).
        const float DefaultFogDistance = 400f;
        const float DefaultFogHeight = 50f;

        WaterSurface water;
        Light sun;
        Volume sceneVolume;
        VolumeProfile seenProfile;
        Volume overlay;
        VolumeProfile overlayProfile;
        Fog overlayFog;
        Exposure overlayExposure;

        SeaState calmSea;
        float calmSunIntensity, calmSunTemperature;
        bool sunUsesTemperature;
        float from, to, duration, elapsed;
        float sinceSea = SeaInterval;
        float applied = -1f;
        bool configured;

        /// <summary>The weather now: 0 calm, 1 storm.</summary>
        public float Intensity { get; private set; }

        /// <summary>Where the weather is heading.</summary>
        public float Target => to;

        /// <summary>The sea the scene had before any weather.</summary>
        public SeaState CalmSea => calmSea;

        /// <summary>The sea last given to the water; without water, the sea it would have been given.</summary>
        public SeaState Sea { get; private set; }

        /// <summary>How many times the sea was changed: at most four times a second.</summary>
        public int SeaChanges { get; private set; }

        /// <summary>The volume that lays the storm's fog and exposure over the scene's own.</summary>
        public Volume Overlay => overlay;

        /// <summary>The scene's own volume, whose current profile the storm's fog and exposure start from.</summary>
        public Volume SceneVolume => sceneVolume;

        /// <summary>
        /// Takes the calm from the scene's water, sun and volume and puts the overlay volume above that volume. Any of them
        /// may be missing; the weather then leaves that part alone. Called once, while the scene is still calm.
        /// </summary>
        public void Configure(WaterSurface surface, Light directional, Volume volume)
        {
            water = surface;
            sun = directional;
            sceneVolume = volume;
            calmSea = surface != null ? SeaState.Of(surface) : DefaultCalmSea;
            Sea = calmSea;
            if (sun != null)
            {
                calmSunIntensity = sun.intensity;
                calmSunTemperature = sun.colorTemperature;
                sunUsesTemperature = sun.useColorTemperature;
            }
            if (overlay == null) CreateOverlay();
            overlay.priority = (volume != null ? volume.priority : 0f) + 1f;
            from = to = Intensity = 0f;
            duration = elapsed = 0f;
            applied = -1f;
            configured = true;
            Apply();
        }

        /// <summary>
        /// The match's calm sea for HDRP's own bands (wind and ripples in km/h, chaos), from the simulation's climate; a
        /// storm still blends from it toward StormSea. This controller stays the only writer of these values.
        /// </summary>
        public void SetCalmSea(SeaState calm)
        {
            calmSea = calm;
            sinceSea = SeaInterval;
            Apply();
        }

        /// <summary>
        /// Moves the weather to <paramref name="intensity"/> (0 calm, 1 storm) over <paramref name="seconds"/> of real time.
        /// Asking again for the weather it already has or is heading to changes nothing, so a caller may repeat the call
        /// every frame without holding the change back.
        /// </summary>
        public void SetWeather(float intensity, float seconds)
        {
            var target = Mathf.Clamp01(intensity);
            if (target == to && (elapsed < duration || Intensity == to)) return;
            from = Intensity;
            to = target;
            duration = Mathf.Max(0f, seconds);
            elapsed = 0f;
            if (duration <= 0f) Intensity = to;
            Apply();
        }

        /// <summary>Moves the weather on by <paramref name="delta"/> seconds; Update calls it with real time.</summary>
        public void Advance(float delta)
        {
            if (!configured) return;
            sinceSea += delta;
            if (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + delta);
                Intensity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            }
            Apply();
        }

        void Update() => Advance(Time.unscaledDeltaTime);

        void Apply()
        {
            if (!configured) return;
            var intensity = Intensity;
            var sceneProfile = sceneVolume != null ? sceneVolume.sharedProfile : null;
            if (intensity != applied || sceneProfile != seenProfile)
            {
                seenProfile = sceneProfile;
                ApplyOverlay(intensity, sceneProfile);
                ApplySun(intensity);
                applied = intensity;
            }

            var sea = SeaState.Lerp(calmSea, StormSea, intensity);
            if (sea.Equals(Sea) || sinceSea < SeaInterval) return;
            Sea = sea;
            SeaChanges++;
            sinceSea = 0f;
            if (water != null) sea.ApplyTo(water);
        }

        void CreateOverlay()
        {
            var item = new GameObject("Weather volume");
            item.transform.SetParent(transform, false);
            overlay = item.AddComponent<Volume>();
            overlay.isGlobal = true;
            overlay.weight = 0f;
            // Made in memory for this battle and never saved.
            overlayProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            overlayProfile.name = "Weather (runtime)";
            overlayFog = overlayProfile.Add<Fog>();
            overlayFog.enabled.Override(true);
            overlayFog.meanFreePath.Override(DefaultFogDistance);
            overlayFog.maximumHeight.Override(DefaultFogHeight);
            overlayFog.tint.Override(Color.white);
            overlayExposure = overlayProfile.Add<Exposure>();
            overlay.sharedProfile = overlayProfile;
        }

        /// <summary>
        /// The overlay has no effect in calm weather; in any other it carries the storm's share of the fog and exposure,
        /// worked out from the scene's current profile. The fog's density, not its distance, follows the weather, so it
        /// thickens evenly instead of all at the end.
        /// </summary>
        void ApplyOverlay(float intensity, VolumeProfile sceneProfile)
        {
            if (overlay == null) return;
            overlay.weight = intensity > 0f ? 1f : 0f;
            if (intensity <= 0f) return;

            Fog sceneFog = null;
            Exposure sceneExposure = null;
            if (sceneProfile != null)
            {
                sceneProfile.TryGet(out sceneFog);
                sceneProfile.TryGet(out sceneExposure);
            }
            var fogOn = sceneFog != null && sceneFog.enabled.overrideState && sceneFog.enabled.value;
            var distance = sceneFog != null && sceneFog.meanFreePath.overrideState ? sceneFog.meanFreePath.value : DefaultFogDistance;
            var height = sceneFog != null && sceneFog.maximumHeight.overrideState ? sceneFog.maximumHeight.value : DefaultFogHeight;
            var tint = sceneFog != null && sceneFog.tint.overrideState ? sceneFog.tint.value : Color.white;
            var density = Mathf.Lerp(fogOn ? 1f / distance : 0f, 1f / StormFogDistance, intensity);
            overlayFog.meanFreePath.Override(1f / density);
            overlayFog.maximumHeight.Override(Mathf.Lerp(height, StormFogHeight, intensity));
            overlayFog.tint.Override(Color.Lerp(tint, StormFogTint, intensity));

            if (sceneExposure != null && sceneExposure.fixedExposure.overrideState)
                overlayExposure.fixedExposure.Override(sceneExposure.fixedExposure.value + StormExposureShift * intensity);
            else
                overlayExposure.fixedExposure.overrideState = false;
        }

        void ApplySun(float intensity)
        {
            if (sun == null) return;
            sun.intensity = Mathf.Lerp(calmSunIntensity, calmSunIntensity * StormSunFactor, intensity);
            if (sunUsesTemperature) sun.colorTemperature = Mathf.Lerp(calmSunTemperature, StormSunTemperature, intensity);
        }

        /// <summary>
        /// The calm comes back with the weather gone: the sea and the sun as the scene had them, and no overlay left
        /// behind, even when only this component is removed.
        /// </summary>
        void OnDestroy()
        {
            if (configured)
            {
                if (water != null && !Sea.Equals(calmSea)) calmSea.ApplyTo(water);
                if (sun != null)
                {
                    sun.intensity = calmSunIntensity;
                    if (sunUsesTemperature) sun.colorTemperature = calmSunTemperature;
                }
            }
            if (overlay != null) Destroy(overlay.gameObject);
            if (overlayProfile == null) return;
            foreach (var component in overlayProfile.components) if (component != null) Destroy(component);
            Destroy(overlayProfile);
        }
    }
}
