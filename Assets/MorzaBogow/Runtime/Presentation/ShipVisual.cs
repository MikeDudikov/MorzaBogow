using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>Clearly marked placeholder materials supplied by the scene. Null values use the default material.</summary>
    [System.Serializable]
    public sealed class PlaceholderMaterials
    {
        public Material Wood;
        public Material Bronze;
        public Material PlayerSail;
        public Material EnemySail;
        public Material Neutral;
        public Material WoodNode;
        public Material SilverNode;
        public Material Selection;
        public Material Foam;
        public Material Arrow;
        public Material Flash;
        public Material Rope;
        public Material OrderLine;
        public Material WreckMarker;
        public Material TargetRing;
        public Material Fog;
        public Material Timber;
        public Material Foliage;
        public Material Rock;
        public Material SilverOre;
        public Material MineDark;

        /// <summary>HDRP water decals: a bow wave that lifts the surface, and a disk of foam used at hulls and shores.</summary>
        public Material BowWave;
        public Material FoamDisk;

        /// <summary>Sea floor around land, seen through the water as shallows.</summary>
        public Material Sand;

        /// <summary>Soft white droplets thrown up at the stem.</summary>
        public Material Spray;
    }

    /// <summary>
    /// Replaceable visual model. The root follows simulation data; named mount points keep costs, damage
    /// and controls independent from whichever art later replaces this placeholder. All animation here
    /// is presentation only and never feeds back into the simulation.
    /// </summary>
    public sealed class ShipVisual : MonoBehaviour
    {
        readonly List<(Transform pivot, int side, float offset)> oars = new List<(Transform, int, float)>();
        Transform model;
        Transform sail;
        Transform targetRing;
        float size;
        float rowing;
        float seed;
        float beam;
        HullPose pose;
        HullPose smoothed;
        bool hasWater;
        HullMotion motion;
        float bowWater;
        float immersion;
        float sprayBudget;
        UnityEngine.Rendering.HighDefinition.WaterDecal bowFoam;
        ParticleSystem spray;

        /// <summary>Metres of water standing above the stem; positive while the bow breaks into a crest.</summary>
        public float BowImmersion => immersion;
        public int SprayParticles => spray != null ? spray.particleCount : 0;
        public float BowFoam => bowFoam != null ? bowFoam.surfaceFoamDimmer : 0f;
        float load;
        float smoothedLoad;
        UnityEngine.Rendering.HighDefinition.WaterDecal bowWave;
        UnityEngine.Rendering.HighDefinition.WaterDecal hullFoam;

        public float Length => size;
        public float Beam => beam;
        public HullPose CurrentPose => smoothed;
        public float HullHeight => beam * 0.6f;
        public float Draft => DraftFor(HullHeight, smoothedLoad);
        public float BowWaveHeight => bowWave != null ? bowWave.amplitude : 0f;
        public float HullFoam => hullFoam != null ? hullFoam.surfaceFoamDimmer : 0f;

        /// <summary>Depth of the hull below the waterline: a third of its height empty, more as the hold fills.</summary>
        public static float DraftFor(float hullHeight, float loadFraction) => hullHeight * (0.34f + 0.22f * Mathf.Clamp01(loadFraction));

        /// <summary>A hull pushes up water in front of it; a longer, faster hull a higher wave. None at rest.</summary>
        public static float BowWaveFor(float length, float speedFraction)
        {
            var fraction = Mathf.Clamp01(Mathf.Abs(speedFraction));
            return (0.5f + length * 0.05f) * fraction * fraction;
        }

        /// <summary>Foam where the hull breaks the surface: a thin waterline at rest, much more under way.</summary>
        public static float HullFoamFor(float speedFraction) => 0.25f + 0.75f * Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(speedFraction)));

        public int ShipId { get; private set; }
        public GameObject Selection { get; private set; }
        public bool IsMarkedAsTarget => targetRing != null && targetRing.gameObject.activeSelf;

        public static ShipVisual Create(Ship ship, PlaceholderMaterials materials)
        {
            var root = new GameObject($"Ship {ship.Id} {ship.Class} [PLACEHOLDER]");
            var visual = root.AddComponent<ShipVisual>();
            visual.ShipId = ship.Id;
            visual.seed = ship.Id * 1.37f;
            var definition = ship.Definition;
            visual.model = new GameObject("Model").transform;
            visual.model.SetParent(root.transform, false);

            var length = (float)definition.Radius * 2f;
            visual.size = length;
            var beam = Mathf.Max(2f, length * (ship.Class == ShipClass.Boarder || ship.Class == ShipClass.Merchant ? 0.42f : 0.28f));
            visual.beam = beam;
            var hull = Part(visual.model, PrimitiveType.Capsule, "Hull", materials?.Wood);
            hull.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hull.localScale = new Vector3(beam, length * 0.5f, beam * 0.6f);
            hull.localPosition = new Vector3(0f, 0.6f, 0f);

            var mast = Part(visual.model, PrimitiveType.Cylinder, "Mast", materials?.Wood);
            mast.localScale = new Vector3(0.5f, length * 0.28f, 0.5f);
            mast.localPosition = new Vector3(0f, length * 0.3f, 0f);

            visual.ApplyOwner(ship.Owner, materials);

            var oarCount = ship.Class == ShipClass.Cutter ? 3 : 6;
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < oarCount; i++)
                {
                    var along = oarCount == 1 ? 0f : Mathf.Lerp(-0.32f, 0.32f, i / (oarCount - 1f));
                    var pivot = new GameObject("Oar").transform;
                    pivot.SetParent(visual.model, false);
                    pivot.localPosition = new Vector3(side * beam * 0.45f, 0.9f, along * length);
                    var blade = Part(pivot, PrimitiveType.Cube, "Blade", materials?.Wood);
                    var reach = Mathf.Max(2.5f, length * 0.28f);
                    blade.localScale = new Vector3(reach, 0.14f, 0.3f);
                    blade.localPosition = new Vector3(side * reach * 0.5f, 0f, 0f);
                    visual.oars.Add((pivot, side, i * 0.35f));
                }
            }

            if (ship.Class == ShipClass.Ram)
            {
                var ram = Part(visual.model, PrimitiveType.Cube, "Ram", materials?.Bronze);
                ram.localScale = new Vector3(0.7f, 0.7f, length * 0.25f);
                ram.localPosition = new Vector3(0f, 0.3f, length * 0.58f);
            }

            for (var slot = 0; slot < definition.LightSlots; slot++)
            {
                var mount = new GameObject($"Mount {slot}").transform;
                mount.SetParent(visual.model, false);
                var offset = definition.LightSlots == 1 ? 0f : Mathf.Lerp(-0.3f, 0.3f, slot / (definition.LightSlots - 1f));
                mount.localPosition = new Vector3(0f, 1.6f, offset * length);
                var weapon = Part(mount, PrimitiveType.Cube, "Weapon", materials?.Bronze);
                weapon.localScale = new Vector3(1.4f, 1.0f, 1.4f);
            }

            visual.targetRing = RingMarker.Create("Attack target", materials?.TargetRing, root.transform, length);
            visual.targetRing.localPosition = new Vector3(0f, 1.4f, 0f);
            visual.targetRing.gameObject.SetActive(false);

            var selection = RingMarker.Create("Selection", materials?.Selection, root.transform, length * 0.58f);
            selection.localPosition = new Vector3(0f, 1.4f, 0f);
            visual.Selection = selection.gameObject;
            visual.Selection.SetActive(false);

            // Children of the root, which only turns with the heading: water decals must stay flat. A hidden ship's
            // root is inactive, so its decals switch off too and fog never shows where it sails.
            // HDRP's bow wave peaks at one end of its region and fades along it; turned so the peak is at the stem
            // and the falling crests run back along both sides of the hull.
            visual.bowWave = WaterDecalAt(root.transform, "Bow wave", materials?.BowWave, new Vector3(0f, 0f, -length * 0.05f), new Vector2(beam * 3f, length * 1.1f));
            visual.bowWave.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visual.hullFoam = WaterDecalAt(root.transform, "Hull foam", materials?.FoamDisk, Vector3.zero, new Vector2(beam * 1.9f, length * 1.15f));
            visual.bowFoam = WaterDecalAt(root.transform, "Bow foam", materials?.FoamDisk, new Vector3(0f, 0f, length * 0.48f), new Vector2(beam * 2f, beam * 2f));
            visual.bowWave.amplitude = 0f;
            visual.hullFoam.amplitude = 0f;
            visual.hullFoam.surfaceFoamDimmer = 0f;
            visual.hullFoam.deepFoamDimmer = 0f;
            visual.bowFoam.amplitude = 0f;
            visual.bowFoam.surfaceFoamDimmer = 0f;
            visual.bowFoam.deepFoamDimmer = 0f;
            visual.motion = new HullMotion(length, beam);
            visual.spray = CreateSpray(root.transform, materials?.Spray, length);
            visual.ApplyDraft();
            return visual;
        }

        /// <summary>Water thrown up where the stem cuts a crest. World space, so spray stays where it was thrown.</summary>
        static ParticleSystem CreateSpray(Transform parent, Material material, float length)
        {
            var item = new GameObject("Bow spray");
            item.transform.SetParent(parent, false);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;

            // Spray thins out as it falls back: the only way an unlit particle fades without vertex colour.
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.1f)));

            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            system.Play();
            return system;
        }

        internal static UnityEngine.Rendering.HighDefinition.WaterDecal WaterDecalAt(Transform parent, string name, Material material, Vector3 position, Vector2 region)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            var decal = item.AddComponent<UnityEngine.Rendering.HighDefinition.WaterDecal>();
            decal.scaleMode = UnityEngine.Rendering.HighDefinition.DecalScaleMode.ScaleInvariant;
            decal.regionSize = region;
            decal.resolution = new Vector2Int(64, 64);
            decal.updateMode = CustomRenderTextureUpdateMode.OnLoad;
            decal.material = material;
            return decal;
        }

        /// <summary>Fraction of the hold in use, 0 to 1. A loaded transport sits lower in the water.</summary>
        public void SetLoad(float fraction) => load = Mathf.Clamp01(fraction);

        void ApplyDraft()
        {
            // The hull is centred 0.6 m above the model origin; this lowers it so its keel is at the draft depth.
            baseHeight = -Draft - 0.6f + HullHeight * 0.5f;
        }

        float baseHeight;

        /// <summary>Called when boarding changes the owner: only the sail changes.</summary>
        public void ApplyOwner(int owner, PlaceholderMaterials materials)
        {
            if (sail != null) { sail.SetParent(null); Destroy(sail.gameObject); }
            sail = Part(model, PrimitiveType.Cube, "Sail", owner == 0 ? materials?.PlayerSail : materials?.EnemySail);
            sail.localScale = new Vector3(size * 0.55f, size * 0.4f, 0.2f);
            sail.localPosition = new Vector3(0f, size * 0.4f, 0f);
        }

        /// <summary>Latest pose from the real water surface. Smoothing keeps small waves from shaking the hull.</summary>
        public void SetWaterPose(HullPose value, float bowWaterHeight)
        {
            if (!hasWater) motion.Reset(value);
            pose = value;
            bowWater = bowWaterHeight;
            hasWater = true;
        }

        /// <summary>Red marker under an enemy ship the player ordered to attack.</summary>
        public void SetTargetMarker(bool marked)
        {
            if (targetRing.gameObject.activeSelf != marked) targetRing.gameObject.SetActive(marked);
        }

        /// <summary>Oars, swell and sail follow current speed. Time here is real time, not simulation time.</summary>
        /// <param name="delta">Game time, for rowing and the hold.</param>
        /// <param name="waterDelta">Real time: the water keeps moving at real speed even when the game is paused or sped up.</param>
        public void Animate(float speedFraction, float time, float delta, float waterDelta = -1f)
        {
            if (waterDelta < 0f) waterDelta = delta;
            var fraction = Mathf.Clamp01(Mathf.Abs(speedFraction));
            rowing += delta * (1.2f + fraction * 3.2f);

            foreach (var (pivot, side, offset) in oars)
            {
                var stroke = Mathf.Sin(rowing + offset);
                var lift = Mathf.Cos(rowing + offset);
                var strength = Mathf.Lerp(0.12f, 1f, fraction);
                pivot.localRotation = Quaternion.Euler(0f, side * stroke * 24f * strength, side * (-14f + lift * 9f * strength));
            }

            // Loading and unloading change the draft over seconds, not in one jump.
            smoothedLoad = Mathf.MoveTowards(smoothedLoad, load, delta * 0.25f);
            ApplyDraft();
            if (hasWater)
            {
                // The hull carries its own inertia: it follows the water with a lag, overshoots and settles.
                smoothed = motion.Step(pose, waterDelta);
                immersion = HullMotion.BowImmersion(smoothed, bowWater, size);

                // The bow lifts slightly with speed, on top of what the water does.
                model.localPosition = new Vector3(0f, baseHeight + smoothed.Heave, 0f);
                model.localRotation = Quaternion.Euler(smoothed.Pitch - fraction * 1.5f, 0f, smoothed.Roll);
            }
            else immersion = 0f;

            // Water breaks where the stem meets it: harder under way, and in bursts when the bow digs into a crest.
            var breaking = BreakingFor(fraction, immersion, motion.HeaveVelocity);
            bowWave.amplitude = BowWaveFor(size, fraction) * (1f + 0.6f * Mathf.Clamp01(immersion));
            hullFoam.surfaceFoamDimmer = Mathf.Clamp01(HullFoamFor(fraction) + Mathf.Abs(motion.HeaveVelocity) * 0.15f);
            hullFoam.deepFoamDimmer = hullFoam.surfaceFoamDimmer * 0.8f;
            bowFoam.surfaceFoamDimmer = breaking;
            bowFoam.deepFoamDimmer = breaking * 0.9f;
            EmitSpray(breaking, fraction, waterDelta);

            if (!hasWater)
            {
                // Without a water surface, as in logic tests, a small sway keeps the placeholder alive.
                var wave = time * 0.9f + seed;
                model.localPosition = new Vector3(0f, baseHeight + Mathf.Sin(wave) * 0.22f, 0f);
                model.localRotation = Quaternion.Euler(Mathf.Sin(wave * 0.8f) * 1.6f, 0f, Mathf.Cos(wave * 0.6f) * 2.2f);
            }

            if (sail != null)
            {
                var billow = 0.2f + fraction * 1.1f + Mathf.Sin(time * 1.7f + seed) * 0.12f;
                sail.localScale = new Vector3(size * 0.55f, size * 0.4f, billow);
                sail.localPosition = new Vector3(0f, size * 0.4f, billow * 0.4f);
            }

            if (targetRing.gameObject.activeSelf)
            {
                var pulse = size * (0.95f + Mathf.Sin(time * 5f) * 0.07f);
                targetRing.localScale = new Vector3(pulse, 1f, pulse);
            }
        }

        /// <summary>
        /// 0 to 1: how hard water breaks at the stem. Speed alone gives a steady curl; a bow driving into a crest or
        /// dropping onto the water adds bursts. A ship at rest on calm water breaks nothing.
        /// </summary>
        public static float BreakingFor(float speedFraction, float immersion, float heaveVelocity)
        {
            var speed = Mathf.Clamp01(Mathf.Abs(speedFraction));
            var crest = Mathf.Clamp01(immersion * 1.5f);
            var drop = Mathf.Clamp01(-heaveVelocity * 0.5f);
            return Mathf.Clamp01(speed * 0.45f + crest * (0.25f + speed) + drop * 0.3f);
        }

        void EmitSpray(float breaking, float fraction, float delta)
        {
            if (spray == null || delta <= 0f || breaking < 0.15f) { sprayBudget = 0f; return; }
            sprayBudget += (breaking - 0.15f) * 70f * delta;
            var stem = model.TransformPoint(new Vector3(0f, 0.3f, size * 0.5f));
            var forward = transform.forward;
            var right = transform.right;
            while (sprayBudget >= 1f)
            {
                sprayBudget -= 1f;
                var side = Random.value < 0.5f ? -1f : 1f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = stem + right * side * beam * Random.Range(0.1f, 0.45f),
                    velocity = right * side * Random.Range(1.2f, 3.5f) * (0.6f + breaking)
                               + Vector3.up * Random.Range(1.5f, 4.5f) * (0.5f + breaking)
                               + forward * fraction * Random.Range(0.5f, 2.5f),
                    startSize = Random.Range(0.5f, 1.3f) * Mathf.Clamp(size / 20f, 0.6f, 1.4f),
                    startLifetime = Random.Range(0.5f, 1.1f),
                    applyShapeToPosition = false,
                };
                spray.Emit(emit, 1);
            }
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            DestroyCollider(part);
            part.transform.SetParent(parent, false);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static void DestroyCollider(GameObject part)
        {
            var collider = part.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        }
    }
}
