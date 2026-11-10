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
        /// <summary>The dusty grey-green of olive leaves, paler than the pines' dark resinous green.</summary>
        public Material OliveLeaves;
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

        /// <summary>
        /// Surfaces of the Aegean shore (CC0 textures, world-mapped): pale limestone where land meets the sea, a
        /// sandy floor strewn with stones under the shallows, and dry grass over rock on the land. Null falls back
        /// to the plain placeholders.
        /// </summary>
        public Material Limestone;
        public Material Seabed;
        public Material Meadow;

        /// <summary>Boulders (CC0 models) scattered along the waterline and over the shallows.</summary>
        public GameObject[] Boulders = System.Array.Empty<GameObject>();

        /// <summary>Level ground high on the rocky coast model where trees can stand, in the model's own space.</summary>
        public Vector3[] CoastTreeSpots = System.Array.Empty<Vector3>();

        public Material ShoreRock => Limestone != null ? Limestone : Sand;
        public Material Floor => Seabed != null ? Seabed : Sand;
        public Material Ground(Material fallback) => Meadow != null ? Meadow : fallback;
        public Material OliveFoliage => OliveLeaves != null ? OliveLeaves : Foliage;

        /// <summary>
        /// Paints of a Greek ship (GreekShips.Palette): pitch for the hull, red ochre for the wale and bow cheeks, white
        /// for the eyes and brails. Null uses a copy of Wood in the palette's colour.
        /// </summary>
        public Material Pitch;
        public Material Ochre;
        public Material Paint;
    }

    /// <summary>
    /// A ship as the battle shows it: the ancient Greek type of its class (GreekShips) with its hull, ram, rig, banks
    /// of oars and weapon posts, the sail in its side's colour, and the marks of the fight on it (breaches, arrows,
    /// broken posts, a list, a torn sail). The root follows simulation data; named mount points keep costs, damage
    /// and controls independent from the art. All animation here is presentation only and never feeds back into the
    /// simulation.
    /// </summary>
    public sealed class ShipVisual : MonoBehaviour
    {
        public const int MaxBreaches = 8, MaxStuckArrows = 12;

        readonly List<(Transform pivot, int side, float offset, float tilt)> oars = new List<(Transform, int, float, float)>();
        readonly List<Transform> breaches = new List<Transform>();
        readonly List<Transform> stuck = new List<Transform>();
        Transform model;
        Transform sail;
        Transform rig;
        Transform ram;
        Transform[] weapons = System.Array.Empty<Transform>();
        Vector3[] weaponRest = System.Array.Empty<Vector3>();
        float[] recoil = System.Array.Empty<float>();
        bool[] broken = System.Array.Empty<bool>();
        bool ramBent;
        Transform targetRing;
        GreekShipParts parts;
        PlaceholderMaterials materials;
        Material sailMaterial, pendingSail;
        float sailDrop;
        bool sailLowering;
        readonly List<Transform> sailHoles = new List<Transform>();
        float hullFraction = 1f;
        float listSide = 1f;
        int marks;
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
        /// <summary>From the keel to the deck amidships.</summary>
        public float HullHeight => parts != null ? parts.DeckHeight + emptyDraft : beam * 0.6f;
        public float Draft => DraftFor(emptyDraft, smoothedLoad);
        float emptyDraft;
        public float BowWaveHeight => bowWave != null ? bowWave.amplitude : 0f;
        public float HullFoam => hullFoam != null ? hullFoam.surfaceFoamDimmer : 0f;

        public GreekShipParts Parts => parts;
        public Transform Model => model;
        public Transform Rig => rig;
        public Transform Sail => sail;
        public int OarCount => oars.Count;
        public int BreachCount => breaches.Count;
        public int StuckArrows => stuck.Count;
        public float HullFraction => hullFraction;
        public Material SailMaterial => sailMaterial;
        public bool SailChanging => sailLowering || pendingSail != null;
        public bool IsPostBroken(int slot) => slot >= 0 && slot < broken.Length && broken[slot];
        public bool RamBent => ramBent;

        /// <summary>The simulation's draft: the empty draft, deeper as the hold fills. Shallows are judged by the same value.</summary>
        public static float DraftFor(float emptyDraft, float loadFraction) => emptyDraft * (1f + (float)Rules.DraftPerLoad * Mathf.Clamp01(loadFraction));

        /// <summary>A hull pushes up water in front of it; a longer, faster hull a higher wave. None at rest.</summary>
        public static float BowWaveFor(float length, float speedFraction)
        {
            var fraction = Mathf.Clamp01(Mathf.Abs(speedFraction));
            return (0.5f + length * 0.05f) * fraction * fraction;
        }

        /// <summary>Foam where the hull breaks the surface: a thin waterline at rest, much more under way.</summary>
        public static float HullFoamFor(float speedFraction) => 0.25f + 0.75f * Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(speedFraction)));

        public int ShipId { get; private set; }
        public ShipClass Class { get; private set; }
        public GameObject Selection { get; private set; }
        public bool IsMarkedAsTarget => targetRing != null && targetRing.gameObject.activeSelf;

        public static ShipVisual Create(Ship ship, PlaceholderMaterials materials)
        {
            var parts = GreekShips.Parts(ship.Class);
            var root = new GameObject($"Ship {ship.Id} {ship.Class} ({parts.Form.TypeName})");
            var visual = root.AddComponent<ShipVisual>();
            visual.ShipId = ship.Id;
            visual.Class = ship.Class;
            visual.seed = ship.Id * 1.37f;
            visual.parts = parts;
            visual.materials = materials;
            var definition = ship.Definition;
            visual.model = new GameObject("Model").transform;
            visual.model.SetParent(root.transform, false);

            var length = (float)definition.Radius * 2f;
            visual.size = length;
            // The simulation's beam: hulls touch on screen exactly where they collide in the game.
            var beam = (float)definition.Beam;
            visual.beam = beam;
            visual.emptyDraft = (float)definition.Draft;

            GreekShips.Attach(visual.model, "Hull", parts.Hull, materials);
            GreekShips.Attach(visual.model, "Deck", parts.Deck, materials);
            GreekShips.Attach(visual.model, "Steering oars", parts.Steering, materials);
            if (parts.Ram != null) visual.ram = GreekShips.Attach(visual.model, "Ram", parts.Ram, materials);
            visual.rig = GreekShips.Attach(visual.model, "Rig", parts.Rig, materials);
            visual.SetOwner(ship.Owner, false);
            visual.oarReach = (float)Rules.OarReach(definition);

            // Banks of oars on both sides; each oar turns on its own thole pin, and all banks pull together.
            for (var side = -1; side <= 1; side += 2)
            {
                var place = new Dictionary<int, int>();
                for (var i = 0; i < parts.OarPins.Length; i++)
                {
                    var bank = parts.OarBank[i];
                    place.TryGetValue(bank, out var index);
                    place[bank] = index + 1;
                    var pin = parts.OarPins[i];
                    var pivot = new GameObject("Oar").transform;
                    pivot.SetParent(visual.model, false);
                    pivot.localPosition = new Vector3(side * pin.x, pin.y, pin.z);
                    var blade = GreekShips.Attach(pivot, "Blade", parts.Oars[bank], materials);
                    blade.localPosition = new Vector3(side * parts.OarLength[bank] * 0.5f, 0f, 0f);
                    blade.localRotation = side < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                    visual.oars.Add((pivot, side, index * 0.35f, parts.OarTilt[i]));
                }
            }

            var posts = definition.Slots.Length;
            visual.weapons = new Transform[posts];
            visual.weaponRest = new Vector3[posts];
            visual.recoil = new float[posts];
            visual.broken = new bool[posts];
            for (var slot = 0; slot < posts; slot++)
            {
                var mount = new GameObject($"Mount {slot}").transform;
                mount.SetParent(visual.model, false);
                mount.localPosition = parts.Mounts[slot];
                var weapon = GreekShips.Attach(mount, "Weapon", GreekShips.Weapon(definition.Slots[slot]), materials);
                visual.weapons[slot] = weapon;
                visual.weaponRest[slot] = weapon.localPosition;
            }

            var ringHeight = parts.DeckHeight + 0.2f;
            visual.targetRing = RingMarker.Create("Attack target", materials?.TargetRing, root.transform, length);
            visual.targetRing.localPosition = new Vector3(0f, ringHeight, 0f);
            visual.targetRing.gameObject.SetActive(false);

            var selection = RingMarker.Create("Selection", materials?.Selection, root.transform, length * 0.58f);
            selection.localPosition = new Vector3(0f, ringHeight, 0f);
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

        Vector3 DeckCentre => model.position + model.up * (parts != null ? parts.DeckHeight : 0.6f + HullHeight * 0.5f);
        float RailHalf => parts != null ? parts.Form.HullBeam * 0.5f : beam * 0.5f;

        /// <summary>
        /// Where a crew member steps on or off: the deck at the gunwale on the side facing a point ashore, following
        /// the hull as it rides the waves.
        /// </summary>
        public Vector3 DeckToward(Vector3 target)
        {
            var centre = DeckCentre;
            var away = target - centre;
            var side = Vector3.Dot(away, transform.right) >= 0f ? 1f : -1f;
            var along = Mathf.Clamp(Vector3.Dot(away, transform.forward), -size * 0.3f, size * 0.3f);
            return centre + transform.right * side * RailHalf * 0.8f + transform.forward * along;
        }

        /// <summary>
        /// The deck at the gunwale nearest a point: on the side facing it, as far along the hull as the point lies
        /// (up to the bow and stern quarters). Grapple lines and the boarding fight stand along it.
        /// </summary>
        public Vector3 RailNear(Vector3 point)
        {
            var centre = DeckCentre;
            var away = point - centre;
            var side = Vector3.Dot(away, transform.right) >= 0f ? 1f : -1f;
            var along = Mathf.Clamp(Vector3.Dot(away, transform.forward), -size * 0.42f, size * 0.42f);
            return centre + transform.right * side * RailHalf * 0.76f + transform.forward * along;
        }

        /// <summary>Fraction of the hold in use, 0 to 1. A loaded transport sits lower in the water.</summary>
        public void SetLoad(float fraction) => load = Mathf.Clamp01(fraction);

        void ApplyDraft()
        {
            // The hull is modelled on its empty waterline; a fuller hold sinks it by the extra draft.
            baseHeight = -(Draft - emptyDraft);
        }

        float baseHeight;

        /// <summary>
        /// Called when boarding changes the owner: the sail comes down, takes the new colours and goes up again.
        /// </summary>
        public void ApplyOwner(int owner, PlaceholderMaterials materials)
        {
            if (materials != null) this.materials = materials;
            SetOwner(owner, sail != null);
        }

        void SetOwner(int owner, bool lowerFirst)
        {
            var colour = owner == 0 ? materials?.PlayerSail : materials?.EnemySail;
            if (sail == null)
            {
                sail = GreekShips.Attach(model, "Sail", parts.Sail, materials, colour);
                sail.localPosition = parts.SailTop;
                sail.localScale = new Vector3(parts.SailSize.x, parts.SailSize.y, 0.2f);
                sailMaterial = colour;
                return;
            }
            if (!lowerFirst)
            {
                SetSailMaterial(colour);
                return;
            }
            pendingSail = colour ?? sailMaterial;
            sailLowering = true;
        }

        void SetSailMaterial(Material colour)
        {
            sailMaterial = colour;
            var renderer = sail.GetComponent<MeshRenderer>();
            var list = renderer.sharedMaterials;
            for (var i = 0; i < parts.Sail.Slots.Length && i < list.Length; i++)
                if (parts.Sail.Slots[i] == GreekMaterial.Sail) list[i] = colour;
            if (System.Array.TrueForAll(list, m => m != null)) renderer.sharedMaterials = list;
        }

        /// <summary>Latest pose from the real water surface. Smoothing keeps small waves from shaking the hull.</summary>
        public void SetWaterPose(HullPose value, float bowWaterHeight)
        {
            if (!hasWater) motion.Reset(value);
            pose = value;
            bowWater = bowWaterHeight;
            hasWater = true;
        }

        float oarReach;
        float roomPort = float.MaxValue, roomStarboard = float.MaxValue;
        readonly float[] shipped = { 1f, 1f };

        public float OarReach => oarReach;

        /// <summary>0 rowing, 1 drawn in along the side. Side -1 is port (left), +1 starboard.</summary>
        public float OarsShipped(int side) => shipped[side < 0 ? 0 : 1];

        /// <summary>Free water beside each side, up to the nearest other hull, from the simulation's positions.</summary>
        public void SetOarRoom(float port, float starboard)
        {
            roomPort = port;
            roomStarboard = starboard;
        }

        /// <summary>A hull meeting the sea at speed: a burst of spray along its length and foam around it.</summary>
        public void Splash(float strength)
        {
            if (spray == null) return;
            var count = Mathf.RoundToInt(40f * strength);
            var random = new System.Random(ShipId * 7919 + marks++);
            for (var i = 0; i < count; i++)
            {
                var along = ((float)random.NextDouble() - 0.5f) * size;
                var flank = random.NextDouble() < 0.5 ? -1f : 1f;
                var at = transform.position + transform.forward * along + transform.right * flank * beam * Mathf.Lerp(0.4f, 0.8f, (float)random.NextDouble());
                spray.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = Vector3.up * Mathf.Lerp(3f, 8f, (float)random.NextDouble()) * strength + transform.right * flank * Mathf.Lerp(1f, 4f, (float)random.NextDouble()),
                    startSize = Mathf.Lerp(0.8f, 2.4f, (float)random.NextDouble()),
                    startLifetime = Mathf.Lerp(0.8f, 1.8f, (float)random.NextDouble()),
                    applyShapeToPosition = false,
                }, 1);
            }
            if (hullFoam != null) hullFoam.surfaceFoamDimmer = 1f;
        }

        /// <summary>Red marker under an enemy ship the player ordered to attack.</summary>
        public void SetTargetMarker(bool marked)
        {
            if (targetRing.gameObject.activeSelf != marked) targetRing.gameObject.SetActive(marked);
        }

        // Damage -----------------------------------------------------------------------------------------------------
        /// <summary>
        /// A blow on the side facing a point: a ragged breach with splintered planks, larger for a heavier blow, at the
        /// waterline for a ram. The oldest breach goes when there are too many.
        /// </summary>
        public void Hit(Vector3 worldPoint, float size, bool waterline)
        {
            if (parts == null) return;
            var random = new System.Random(ShipId * 104729 + marks++);
            var local = model.InverseTransformPoint(worldPoint);
            var side = Mathf.Abs(local.x) > 0.3f ? Mathf.Sign(local.x) : (random.NextDouble() < 0.5 ? -1f : 1f);
            var form = parts.Form;
            var z = Mathf.Clamp(local.z, -form.Length * 0.4f, form.Length * 0.4f);
            var s = Mathf.InverseLerp(GreekShips.Z(form, 0f), GreekShips.Z(form, 1f), z);
            var top = GreekShips.Sheer(form, s);
            var height = waterline ? 0.2f : Mathf.Lerp(0.35f, top * 0.8f, (float)random.NextDouble());
            var (point, normal) = SurfaceAt(form, s, height, side);
            var breach = GreekShips.Attach(model, "Breach", GreekShips.Prop($"breach{random.Next(4)}"), materials);
            breach.localPosition = point + normal * 0.04f;
            breach.localRotation = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
            breach.localScale = Vector3.one * Mathf.Clamp(size, 0.4f, 2.6f);
            breaches.Add(breach);
            if (breaches.Count > MaxBreaches) Remove(breaches, 0);
            listSide = Mathf.Sign(SideBalance());
        }

        static (Vector3 point, Vector3 normal) SurfaceAt(GreekShipForm form, float s, float height, float side)
        {
            var t = 0.5f;
            for (var probe = 0f; probe <= 1f; probe += 0.02f)
            {
                if (GreekShips.Section(form, s, probe).y > height) continue;
                t = probe;
                break;
            }
            var p = GreekShips.Section(form, s, t);
            var below = GreekShips.Section(form, s, Mathf.Min(1f, t + 0.04f));
            var ahead = GreekShips.Section(form, Mathf.Min(1f, s + 0.02f), t);
            p.x *= -side;
            below.x *= -side;
            ahead.x *= -side;
            var normal = Vector3.Cross(ahead - p, below - p).normalized;
            if (Vector3.Dot(normal, new Vector3(side, 0f, 0f)) < 0f) normal = -normal;
            return (p, normal);
        }

        float SideBalance()
        {
            var sum = 0f;
            foreach (var breach in breaches) sum += breach.localPosition.x;
            return Mathf.Abs(sum) < 0.01f ? 1f : sum;
        }

        /// <summary>Arrows of a volley left standing in the side and deck that faced the archers.</summary>
        public void StickArrows(Vector3 from, int count)
        {
            if (parts == null) return;
            var random = new System.Random(ShipId * 7907 + marks++);
            var local = model.InverseTransformPoint(from);
            var side = Mathf.Abs(local.x) > 0.3f ? Mathf.Sign(local.x) : 1f;
            var form = parts.Form;
            for (var i = 0; i < Mathf.Clamp(count / 3, 1, 4); i++)
            {
                var s = Mathf.Lerp(0.2f, 0.85f, (float)random.NextDouble());
                var onDeck = random.NextDouble() < 0.5;
                Vector3 point, normal;
                if (onDeck)
                {
                    point = new Vector3(side * GreekShips.HalfWidth(form, s) * (float)random.NextDouble() * 0.8f, GreekShips.DeckAt(form, s), GreekShips.Z(form, s));
                    normal = Vector3.up;
                }
                else (point, normal) = SurfaceAt(form, s, Mathf.Lerp(0.4f, GreekShips.Sheer(form, s) * 0.9f, (float)random.NextDouble()), side);
                var arrow = GreekShips.Attach(model, "Arrow", GreekShips.Prop("arrow"), materials);
                var slant = Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 40f, ((float)random.NextDouble() - 0.5f) * 40f, 0f);
                arrow.localRotation = Quaternion.LookRotation(-(slant * normal), Vector3.up);
                arrow.localPosition = point + normal * 0.32f;
                arrow.localScale = Vector3.one * 0.9f;
                stuck.Add(arrow);
                if (stuck.Count > MaxStuckArrows) Remove(stuck, 0);
            }
        }

        /// <summary>
        /// The ship's state from the simulation: what is left of the hull sets the list, how low it lies and whether the
        /// sail is torn; a lost weapon post leans broken, a lost ram is bent. Repairs take breaches away, oldest first.
        /// </summary>
        public void SetCondition(float fraction, double[] moduleHp)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction > hullFraction + 0.001f)
            {
                var allowed = Mathf.CeilToInt((1f - fraction) * 10f);
                while (breaches.Count > allowed) Remove(breaches, 0);
                if (fraction > 0.99f) while (stuck.Count > 0) Remove(stuck, 0);
            }
            hullFraction = fraction;
            UpdateSailHoles();
            if (moduleHp == null) return;
            for (var slot = 0; slot < weapons.Length && slot < moduleHp.Length; slot++)
            {
                var gone = moduleHp[slot] <= 0;
                if (gone == broken[slot]) continue;
                broken[slot] = gone;
                weapons[slot].localRotation = gone ? Quaternion.Euler(-28f, 15f, 22f) : Quaternion.identity;
                weaponRest[slot] = gone ? new Vector3(0.2f, -0.15f, 0f) : Vector3.zero;
            }
            if (ram != null && moduleHp.Length > weapons.Length)
            {
                var bent = moduleHp[weapons.Length] <= 0;
                if (bent != ramBent)
                {
                    ramBent = bent;
                    var tip = new Vector3(0f, 0f, size * 0.5f);
                    ram.localRotation = bent ? Quaternion.Euler(7f, 9f, 4f) : Quaternion.identity;
                    ram.localPosition = bent ? tip - ram.localRotation * tip : Vector3.zero;
                }
            }
        }

        void UpdateSailHoles()
        {
            var wanted = hullFraction < 0.25f ? 3 : hullFraction < 0.5f ? 2 : 0;
            while (sailHoles.Count > wanted) Remove(sailHoles, sailHoles.Count - 1);
            while (sailHoles.Count < wanted && sail != null)
            {
                var hole = GreekShips.Attach(sail, "Tear", GreekShips.Prop("breach" + (sailHoles.Count + 1)), materials);
                var u = new[] { -0.22f, 0.18f, 0.02f }[sailHoles.Count];
                var v = new[] { -0.55f, -0.3f, -0.75f }[sailHoles.Count];
                hole.localPosition = new Vector3(u, v, 0.85f);
                hole.localRotation = Quaternion.Euler(0f, 180f, sailHoles.Count * 70f);
                hole.localScale = new Vector3(0.09f, 0.11f, 0.2f);
                sailHoles.Add(hole);
            }
        }

        /// <summary>A machine on its post fired: its arms kick back and settle.</summary>
        public void Recoil(int slot)
        {
            if (slot >= 0 && slot < recoil.Length && !broken[slot]) recoil[slot] = 1f;
        }

        static void Remove(List<Transform> list, int index)
        {
            var item = list[index];
            list.RemoveAt(index);
            if (item == null) return;
            if (Application.isPlaying) Destroy(item.gameObject); else DestroyImmediate(item.gameObject);
        }

        // Animation --------------------------------------------------------------------------------------------------
        /// <summary>Oars, swell and sail follow current speed. Time here is real time, not simulation time.</summary>
        /// <param name="delta">Game time, for rowing and the hold.</param>
        /// <param name="waterDelta">Real time: the water keeps moving at real speed even when the game is paused or sped up.</param>
        public void Animate(float speedFraction, float time, float delta, float waterDelta = -1f)
        {
            if (waterDelta < 0f) waterDelta = delta;
            var fraction = Mathf.Clamp01(Mathf.Abs(speedFraction));
            rowing += delta * (1.2f + fraction * 3.2f);

            // Oars come in along the side where another hull lies within their reach, and both sides at rest:
            // shipped, they lie along the gunwale pointing aft, so no oar ever strikes another hull or oar.
            for (var s = 0; s < 2; s++)
            {
                var room = s == 0 ? roomPort : roomStarboard;
                var target = fraction < 0.08f || room < oarReach + 0.8f ? 1f : 0f;
                shipped[s] = Mathf.MoveTowards(shipped[s], target, delta * 1.6f + (target > shipped[s] && room < oarReach * 0.6f ? 1f : 0f));
            }
            foreach (var (pivot, side, offset, tilt) in oars)
            {
                var stroke = Mathf.Sin(rowing + offset);
                var lift = Mathf.Cos(rowing + offset);
                var strength = Mathf.Lerp(0.12f, 1f, fraction);
                var rowed = Quaternion.Euler(0f, side * stroke * 24f * strength, side * (-tilt + lift * 9f * strength));
                var stowed = Quaternion.Euler(0f, side * 84f, side * 6f);
                pivot.localRotation = Quaternion.Slerp(rowed, stowed, OarsShipped(side));
            }

            // Loading and unloading change the draft over seconds, not in one jump.
            smoothedLoad = Mathf.MoveTowards(smoothedLoad, load, delta * 0.25f);
            ApplyDraft();
            // A holed hull lies lower and lists toward its breaches.
            var damage = 1f - hullFraction;
            var sink = damage * damage * emptyDraft * 0.35f;
            var list = damage * 7f * listSide;
            if (hasWater)
            {
                // The hull carries its own inertia: it follows the water with a lag, overshoots and settles.
                smoothed = motion.Step(pose, waterDelta);
                immersion = HullMotion.BowImmersion(smoothed, bowWater, size);

                // The bow lifts slightly with speed, on top of what the water does.
                model.localPosition = new Vector3(0f, baseHeight - sink + smoothed.Heave, 0f);
                model.localRotation = Quaternion.Euler(smoothed.Pitch - fraction * 1.5f, 0f, smoothed.Roll - list);
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
                // Without a water surface, as in logic tests, a small sway keeps the hull alive.
                var wave = time * 0.9f + seed;
                model.localPosition = new Vector3(0f, baseHeight - sink + Mathf.Sin(wave) * 0.22f, 0f);
                model.localRotation = Quaternion.Euler(Mathf.Sin(wave * 0.8f) * 1.6f, 0f, Mathf.Cos(wave * 0.6f) * 2.2f - list);
            }

            AnimateSail(fraction, time, delta);
            for (var slot = 0; slot < weapons.Length; slot++)
            {
                if (recoil[slot] <= 0f && !broken[slot]) continue;
                recoil[slot] = Mathf.MoveTowards(recoil[slot], 0f, delta * 2.5f);
                var kick = Mathf.Sin(recoil[slot] * Mathf.PI) * 0.35f;
                weapons[slot].localPosition = weaponRest[slot] + Vector3.back * kick;
            }

            if (targetRing.gameObject.activeSelf)
            {
                var pulse = size * (0.95f + Mathf.Sin(time * 5f) * 0.07f);
                targetRing.localScale = new Vector3(pulse, 1f, pulse);
            }
        }

        void AnimateSail(float fraction, float time, float delta)
        {
            if (sail == null) return;
            // A captured ship lowers its sail, bends on the new colours and hoists it again.
            if (sailLowering)
            {
                sailDrop = Mathf.MoveTowards(sailDrop, 1f, delta * 1.5f);
                if (sailDrop >= 1f)
                {
                    SetSailMaterial(pendingSail);
                    pendingSail = null;
                    sailLowering = false;
                }
            }
            else sailDrop = Mathf.MoveTowards(sailDrop, 0f, delta * 1.2f);
            var billow = (0.2f + fraction * 1.1f + Mathf.Sin(time * 1.7f + seed) * 0.12f) * 0.55f;
            var hoisted = Mathf.Lerp(1f, 0.12f, sailDrop);
            sail.localScale = new Vector3(parts.SailSize.x, parts.SailSize.y * hoisted, billow * hoisted);
            sail.localPosition = parts.SailTop;
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
            var random = new System.Random(ShipId * 31 + Mathf.FloorToInt(rowing * 10f));
            while (sprayBudget >= 1f)
            {
                sprayBudget -= 1f;
                var side = random.NextDouble() < 0.5 ? -1f : 1f;
                float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
                var emit = new ParticleSystem.EmitParams
                {
                    position = stem + right * side * beam * R(0.1f, 0.45f),
                    velocity = right * side * R(1.2f, 3.5f) * (0.6f + breaking)
                               + Vector3.up * R(1.5f, 4.5f) * (0.5f + breaking)
                               + forward * fraction * R(0.5f, 2.5f),
                    startSize = R(0.5f, 1.3f) * Mathf.Clamp(size / 20f, 0.6f, 1.4f),
                    startLifetime = R(0.5f, 1.1f),
                    applyShapeToPosition = false,
                };
                spray.Emit(emit, 1);
            }
        }
    }
}
