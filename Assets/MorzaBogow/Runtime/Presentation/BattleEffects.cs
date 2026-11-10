using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Combat effects, drawn only for events the player can see (the caller decides): arrows in flight, flashes, ropes
    /// and order pings (at most 160 at once, from one EffectPool per kind); the machines' bolts and stones in flight,
    /// read from the world's own record of them; splinters bursting from a struck hull and columns of water from a
    /// miss; ships going down, whole or broken in pieces; and wreckage that floats and drifts before it sinks.
    /// Randomness comes from the event's seed, never from the frame rate. Nothing here affects the simulation.
    /// </summary>
    public sealed class BattleEffects
    {
        const int MaxEffects = 160;
        public const int MaxDebris = 120, MaxMissiles = 64;

        enum Kind { Arrow, Flash, Rope, Ping, Bolt, Stone, Javelin }

        /// <summary>One shot in flight as drawn: a machine's bolt or stone, or the shafts of a bank's volley, one per man.</summary>
        sealed class Missile
        {
            public Kind Kind;
            public GameObject[] Items;
            public Vector3[] From;
            public Vector3[] Spread;
        }

        sealed class Effect
        {
            public Kind Kind;
            public GameObject Instance;
            public Transform Item;
            public Vector3 From, To;
            public float Age, Life, Size;
        }

        sealed class Debris
        {
            public Transform Item;
            public Vector3 Velocity;
            public float Spin, Age, Life, Phase;
        }

        readonly PlaceholderMaterials materials;
        readonly List<Effect> active = new List<Effect>();
        readonly Dictionary<Kind, EffectPool> pools = new Dictionary<Kind, EffectPool>();
        readonly List<ShipFate> sinking = new List<ShipFate>();
        readonly List<Debris> debris = new List<Debris>();
        readonly Dictionary<int, Missile> missiles = new Dictionary<int, Missile>();
        readonly HashSet<int> missilesSeen = new HashSet<int>();
        readonly List<int> missilesGone = new List<int>();
        readonly List<Transform> wrecks = new List<Transform>();
        Transform root;
        ParticleSystem splinters, water;
        float clock;

        public BattleEffects(PlaceholderMaterials materials)
        {
            this.materials = materials;
        }

        public int ActiveCount => active.Count;
        public int SinkingCount => sinking.Count;
        public int DebrisCount => debris.Count;
        public int MissilesShown => missiles.Count;
        public int SplinterParticles => splinters != null ? splinters.particleCount : 0;
        public int WaterParticles => water != null ? water.particleCount : 0;
        /// <summary>How many ships went down in pieces so far; tools and tests.</summary>
        public int BrokenShips { get; private set; }

        /// <summary>Effect objects made so far, of every kind; they are reused, so this stops growing in a long battle.</summary>
        public int CreatedObjects
        {
            get
            {
                var made = 0;
                foreach (var pool in pools.Values) made += pool.CreatedCount;
                return made;
            }
        }

        public void Arrows(Vector3 from, Vector3 to, int count)
        {
            for (var i = 0; i < Mathf.Min(count, 4); i++)
            {
                var spread = new Vector3(Mathf.Sin(i * 2.3f) * 2.5f, 0f, Mathf.Cos(i * 1.7f) * 2.5f);
                Add(Kind.Arrow, from + Vector3.up * 3f, to + spread + Vector3.up, 0.55f + i * 0.06f, 1f);
            }
        }

        public void Flash(Vector3 at, float size) => Add(Kind.Flash, at + Vector3.up * 2f, at, 0.6f, size);
        public void Rope(Vector3 from, Vector3 to, float life) => Add(Kind.Rope, from + Vector3.up * 2f, to + Vector3.up * 2f, life, 1f);
        /// <summary>A single ring where an order was given, gone after 0.6 s; no lasting order lines.</summary>
        public void Ping(Vector3 at) => Add(Kind.Ping, at + Vector3.up * 0.5f, at, 0.6f, 18f);

        /// <summary>Takes over a visual that only has to go down (anything without a ship's parts).</summary>
        public void Sink(Transform visual) => sinking.Add(new ShipFate(visual, null, false, 0));

        /// <summary>
        /// Takes over a sunk ship's visual; the ship no longer exists in the simulation. It settles and lists, the mast
        /// falls, oars and planks float off and the hull goes under; broken, it parts into stern, middle and bow, which
        /// tip and sink each their own way.
        /// </summary>
        public void Sink(ShipVisual view, bool breakUp)
        {
            if (view == null) return;
            var fate = new ShipFate(view.transform, view, breakUp, view.ShipId);
            sinking.Add(fate);
            if (breakUp) BrokenShips++;
        }

        void Add(Kind kind, Vector3 from, Vector3 to, float life, float size)
        {
            if (active.Count >= MaxEffects) return;
            var item = PoolOf(kind).Rent();
            if (item == null) return;
            active.Add(new Effect { Kind = kind, Instance = item, Item = item.transform, From = from, To = to, Life = life, Size = size });
        }

        EffectPool PoolOf(Kind kind)
        {
            // A bank's volley is several shafts in the air at once, one per man.
            var capacity = kind == Kind.Bolt || kind == Kind.Stone ? MaxMissiles : kind == Kind.Arrow || kind == Kind.Javelin ? MaxMissiles * 4 : MaxEffects;
            if (!pools.TryGetValue(kind, out var pool)) pools[kind] = pool = new EffectPool(() => Make(kind), capacity);
            return pool;
        }

        GameObject Make(Kind kind)
        {
            if (kind == Kind.Arrow || kind == Kind.Bolt || kind == Kind.Stone || kind == Kind.Javelin)
            {
                var mesh = GreekShips.Prop(kind == Kind.Arrow ? "arrow" : kind == Kind.Bolt ? "bolt" : kind == Kind.Javelin ? "javelin" : "stone");
                var item = MeshObject($"Effect {kind}", mesh);
                if (kind == Kind.Stone && materials?.Rock != null) item.GetComponent<MeshRenderer>().sharedMaterial = materials.Rock;
                return item;
            }
            var shape = kind == Kind.Flash ? PrimitiveType.Sphere : kind == Kind.Ping ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            var primitive = GameObject.CreatePrimitive(shape);
            primitive.name = $"Effect {kind}";
            Object.Destroy(primitive.GetComponent<Collider>());
            var material = kind == Kind.Rope ? materials?.Rope : kind == Kind.Ping ? materials?.Selection : materials?.Flash;
            if (material != null) primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        GameObject MeshObject(string name, GreekMesh mesh)
        {
            var item = new GameObject(name);
            item.AddComponent<MeshFilter>().sharedMesh = mesh.Mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var list = GreekShips.MaterialsFor(mesh, materials);
            if (System.Array.TrueForAll(list, m => m != null)) renderer.sharedMaterials = list;
            return item;
        }

        Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("Battle effects").transform;
                return root;
            }
        }

        // Missiles ---------------------------------------------------------------------------------------------------
        /// <summary>
        /// Where a shot leaves its ship, from the crew on deck: shaft i of a bank's volley from the hand of the man who
        /// loosed it, a machine's shot from its muzzle. Null keeps the start above the middle of the hull, as without a
        /// crew. Asked once per shaft when the shot is first drawn, so it keeps its start for the whole flight.
        /// </summary>
        public System.Func<Projectile, int, Vector3?> LaunchPoint;

        /// <summary>
        /// Every shot in flight, placed from the world's record of it (launch, arrival, from, to) over its real time of
        /// flight: a bolt flies straight and low; a bank's arrows or javelins, one per man of the volley, on a shallow
        /// arc; a stone or lead shot on a high one. Only shots the player can see are drawn.
        /// </summary>
        public void SyncMissiles(World world, int faction, float blend)
        {
            missilesSeen.Clear();
            foreach (var shot in world.Projectiles)
            {
                var flight = shot.ArriveTick - shot.LaunchTick;
                if (flight <= 0) continue;
                var weapon = WeaponCatalog.Get(shot.Weapon);
                var seconds = flight / (float)Rules.TickRate;
                var t = Mathf.Clamp01((world.Tick - shot.LaunchTick + Mathf.Clamp01(blend)) / flight);
                var to = new Vector3((float)shot.ToX, 0f, (float)shot.ToY);
                var position = Along(new Vector3((float)shot.FromX, 0f, (float)shot.FromY), to, t, weapon.Lobbed, seconds);
                if (!world.CanSee(faction, position.x, position.z)) continue;
                if (!missiles.TryGetValue(shot.Id, out var shown))
                {
                    if (missiles.Count >= MaxMissiles) continue;
                    shown = Launch(shot, weapon);
                    if (shown == null) continue;
                    missiles[shot.Id] = shown;
                }
                missilesSeen.Add(shot.Id);
                var thrown = shown.Kind == Kind.Arrow || shown.Kind == Kind.Javelin;
                for (var i = 0; i < shown.Items.Length; i++)
                {
                    var landing = to + shown.Spread[i];
                    var at = Flight(shown.From[i], landing, t, weapon.Lobbed, thrown, seconds);
                    var ahead = Flight(shown.From[i], landing, Mathf.Min(1f, t + 0.02f), weapon.Lobbed, thrown, seconds);
                    var item = shown.Items[i].transform;
                    item.position = at;
                    if ((ahead - at).sqrMagnitude > 1e-6f) item.rotation = Quaternion.LookRotation(ahead - at);
                    if (shown.Kind == Kind.Stone) item.rotation *= Quaternion.Euler(t * 720f, t * 360f, 0f);
                }
            }
            missilesGone.Clear();
            foreach (var id in missiles.Keys) if (!missilesSeen.Contains(id)) missilesGone.Add(id);
            foreach (var id in missilesGone)
            {
                var gone = missiles[id];
                missiles.Remove(id);
                foreach (var item in gone.Items) PoolOf(gone.Kind).Return(item);
            }
        }

        /// <summary>A shot first seen: a bank's volley is its men's shafts together, a machine throws one bolt or stone.</summary>
        Missile Launch(Projectile shot, WeaponDefinition weapon)
        {
            var kind = weapon.Lobbed ? Kind.Stone : shot.Weapon == WeaponType.ArcherBank ? Kind.Arrow : shot.Weapon == WeaponType.JavelinBank ? Kind.Javelin : Kind.Bolt;
            var count = weapon.IsMachine ? 1 : Mathf.Max(1, weapon.Crew);
            var missile = new Missile { Kind = kind, Items = new GameObject[count], From = new Vector3[count], Spread = new Vector3[count] };
            var above = new Vector3((float)shot.FromX, 3f, (float)shot.FromY);
            for (var i = 0; i < count; i++)
            {
                var item = PoolOf(kind).Rent();
                if (item == null)
                {
                    for (var j = 0; j < i; j++) PoolOf(kind).Return(missile.Items[j]);
                    return null;
                }
                // Drawn larger than life so a volley reads from the strategy camera.
                item.transform.localScale = Vector3.one * (kind == Kind.Arrow ? 2.4f : kind == Kind.Javelin ? 1.6f : kind == Kind.Bolt ? 2f : Mathf.Lerp(0.8f, 1.4f, (float)weapon.Hull / 160f));
                missile.Items[i] = item;
                missile.From[i] = LaunchPoint?.Invoke(shot, i) ?? above;
                // Men aiming at one ship do not all strike one spot: the shafts of a volley come down a little apart.
                var angle = shot.Id * 2.39996f + i * 1.7f;
                missile.Spread[i] = count == 1 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.6f + 0.45f * i);
            }
            return missile;
        }

        /// <summary>
        /// A shot's place from its start on deck to just above where it lands: a bolt straight; arrows and javelins rise as
        /// far above the line as a throw of that length does under gravity (g t² / 8); a stone or lead shot at least 6 m.
        /// </summary>
        static Vector3 Flight(Vector3 from, Vector3 landing, float t, bool lobbed, bool thrown, float seconds)
        {
            var end = landing + Vector3.up * (lobbed ? 2.5f : 1.5f);
            var line = Vector3.Lerp(from, end, t);
            if (!lobbed && !thrown) return line;
            var apex = 9.81f * seconds * seconds / 8f;
            if (lobbed) apex = Mathf.Max(6f, apex);
            return line + Vector3.up * (4f * apex * t * (1f - t));
        }

        /// <summary>A point of a shot's path: straight just above the deck for a bolt, a ballistic arc for a lobbed shot.</summary>
        public static Vector3 Along(Vector3 from, Vector3 to, float t, bool lobbed, float seconds)
        {
            var flat = Vector3.Lerp(from, to, t);
            if (!lobbed) return flat + Vector3.up * Mathf.Lerp(3f, 1.5f, t);
            // Apex of a throw lasting this long under gravity: g t² / 8.
            var apex = Mathf.Max(6f, 9.81f * seconds * seconds / 8f);
            return flat + Vector3.up * (2.5f + 4f * apex * t * (1f - t));
        }

        // Impacts ----------------------------------------------------------------------------------------------------
        /// <summary>Planks and splinters burst out of a struck hull; more for a heavier blow.</summary>
        public void Splinters(Vector3 at, float strength, int seed)
        {
            var system = Splinter();
            var random = new System.Random(seed);
            var count = Mathf.Clamp(Mathf.RoundToInt(10f + strength * 18f), 6, 60);
            for (var i = 0; i < count; i++)
            {
                var direction = new Vector3((float)random.NextDouble() - 0.5f, 0.4f + (float)random.NextDouble(), (float)random.NextDouble() - 0.5f).normalized;
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * (1f + (float)random.NextDouble()),
                    velocity = direction * Mathf.Lerp(4f, 11f, (float)random.NextDouble()) * Mathf.Clamp(0.6f + strength * 0.3f, 0.6f, 1.6f),
                    rotation3D = new Vector3((float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f),
                    angularVelocity3D = new Vector3(400f, 200f, 300f) * ((float)random.NextDouble() - 0.5f),
                    startSize = Mathf.Lerp(0.12f, 0.4f, (float)random.NextDouble()),
                    startLifetime = Mathf.Lerp(1.1f, 2.2f, (float)random.NextDouble()),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>A shot falling into the sea: a column of water and a ring of spray, taller for a heavier shot.</summary>
        public void WaterColumn(Vector3 at, float size, int seed)
        {
            var system = Water();
            var random = new System.Random(seed);
            var count = Mathf.Clamp(Mathf.RoundToInt(14f + size * 10f), 10, 50);
            at.y = 0.2f;
            for (var i = 0; i < count; i++)
            {
                var ring = i % 3 == 0;
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var out_ = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = at + out_ * (ring ? size * 0.6f : (float)random.NextDouble() * 0.6f),
                    velocity = ring ? out_ * Mathf.Lerp(2f, 4f, (float)random.NextDouble()) + Vector3.up * 2.5f
                                    : Vector3.up * Mathf.Lerp(7f, 12f, (float)random.NextDouble()) * Mathf.Clamp(size * 0.5f, 0.6f, 1.5f) + out_ * 0.8f,
                    startSize = Mathf.Lerp(0.7f, 1.6f, (float)random.NextDouble()) * Mathf.Clamp(size * 0.6f, 0.6f, 1.6f),
                    startLifetime = Mathf.Lerp(0.9f, 1.8f, (float)random.NextDouble()),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        ParticleSystem Splinter()
        {
            if (splinters != null) return splinters;
            splinters = Particles("Splinters", 400, 1f);
            var renderer = splinters.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GreekShips.Prop("splinter").Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            if (materials?.Wood != null) renderer.sharedMaterial = materials.Wood;
            var main = splinters.main;
            main.startRotation3D = true;
            return splinters;
        }

        ParticleSystem Water()
        {
            if (water != null) return water;
            water = Particles("Water columns", 600, 1.1f);
            var renderer = water.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (materials?.Spray != null) renderer.sharedMaterial = materials.Spray;
            var size = water.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.15f)));
            return water;
        }

        ParticleSystem Particles(string name, int max, float gravity)
        {
            var item = new GameObject(name);
            item.transform.SetParent(Root, false);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        // Debris and wrecks ------------------------------------------------------------------------------------------
        /// <summary>A piece of wreckage floating off: it drifts, turns, rocks on the water and after a while goes under.</summary>
        internal void Float(string prop, Vector3 at, Quaternion rotation, Vector3 velocity, int seed, float life = 30f)
        {
            while (debris.Count >= MaxDebris)
            {
                if (debris[0].Item != null) Object.Destroy(debris[0].Item.gameObject);
                debris.RemoveAt(0);
            }
            var item = MeshObject("Wreckage", GreekShips.Prop(prop)).transform;
            item.SetParent(Root, true);
            item.SetPositionAndRotation(at, rotation);
            var random = new System.Random(seed);
            debris.Add(new Debris { Item = item, Velocity = velocity, Spin = ((float)random.NextDouble() - 0.5f) * 40f, Life = life * Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble()), Phase = (float)random.NextDouble() * 6f });
        }

        internal void FloatOff(Transform item, Vector3 velocity, int seed)
        {
            if (item == null) return;
            while (debris.Count >= MaxDebris)
            {
                if (debris[0].Item != null) Object.Destroy(debris[0].Item.gameObject);
                debris.RemoveAt(0);
            }
            item.SetParent(Root, true);
            var random = new System.Random(seed);
            debris.Add(new Debris { Item = item, Velocity = velocity, Spin = ((float)random.NextDouble() - 0.5f) * 30f, Life = 18f, Phase = (float)random.NextDouble() * 6f });
        }

        /// <summary>
        /// What is left of a sunk ship while its cargo can still be salvaged: a broken stern piece awash, planks, a spar,
        /// and amphorae or crates bobbing among them. Owned by the caller, which removes it with the wreck.
        /// </summary>
        public GameObject CreateWreck(Vector3 at, int seed, bool amphorae)
        {
            var random = new System.Random(seed);
            var wreck = new GameObject($"Wreck {seed}");
            wreck.transform.position = new Vector3(at.x, 0f, at.z);
            wreck.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            var piece = GreekShips.Attach(wreck.transform, "Hull piece", GreekShips.Parts(ShipClass.Cutter).Sections[0], materials);
            piece.localPosition = new Vector3(0f, -0.9f, 2f);
            piece.localRotation = Quaternion.Euler(-12f, 0f, 24f);
            for (var i = 0; i < 6; i++)
            {
                var item = GreekShips.Attach(wreck.transform, "Plank", GreekShips.Prop("plank"), materials);
                item.localPosition = new Vector3(((float)random.NextDouble() - 0.5f) * 9f, 0.02f, ((float)random.NextDouble() - 0.5f) * 9f);
                item.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, ((float)random.NextDouble() - 0.5f) * 10f);
            }
            var spar = GreekShips.Attach(wreck.transform, "Spar", GreekShips.Prop("mast"), materials);
            spar.localPosition = new Vector3(-2.5f, 0.05f, -1f);
            spar.localRotation = Quaternion.Euler(0f, 70f, 0f);
            for (var i = 0; i < 4; i++)
            {
                // Jars of the eastern trade float half awash; silver ingots lie on the broken planking.
                var item = GreekShips.Attach(wreck.transform, amphorae ? "Jar" : "Timber", GreekShips.Prop(amphorae ? "jar" : "mast"), materials);
                item.localPosition = new Vector3(((float)random.NextDouble() - 0.5f) * 6f, amphorae ? -0.55f : 0.05f, ((float)random.NextDouble() - 0.5f) * 6f);
                item.localRotation = amphorae ? Quaternion.Euler(70f + (float)random.NextDouble() * 30f, (float)random.NextDouble() * 360f, 0f) : Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
                if (!amphorae) item.localScale = new Vector3(0.8f, 0.8f, 0.45f);
            }
            if (amphorae)
            {
                for (var i = 0; i < 3; i++)
                {
                    var ingot = GreekShips.Attach(piece, "Silver", GreekShips.Prop("silver"), materials);
                    ingot.localPosition = new Vector3(0f, 1.2f + i * 0.07f, -3f);
                    ingot.localRotation = Quaternion.Euler(0f, i * 90f + 10f, 0f);
                }
            }
            wrecks.Add(wreck.transform);
            return wreck;
        }

        /// <summary>Destroys every effect object, shown or waiting, every sinking hull and every piece of wreckage, when the battle goes.</summary>
        public void Clear()
        {
            foreach (var pool in pools.Values) pool.Clear();
            active.Clear();
            missiles.Clear();
            foreach (var fate in sinking) fate.Destroy();
            sinking.Clear();
            foreach (var piece in debris) if (piece.Item != null) Object.Destroy(piece.Item.gameObject);
            debris.Clear();
            wrecks.Clear();
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            splinters = null;
            water = null;
        }

        public void Update(float delta)
        {
            clock += delta;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var effect = active[i];
                effect.Age += delta;
                // An effect destroyed from outside, as with its scene, is only forgotten by its pool.
                if (effect.Age >= effect.Life || effect.Instance == null)
                {
                    pools[effect.Kind].Return(effect.Instance);
                    active.RemoveAt(i);
                    continue;
                }
                Present(effect, effect.Age / effect.Life);
            }

            for (var i = sinking.Count - 1; i >= 0; i--)
            {
                if (sinking[i].Step(delta, this)) continue;
                sinking[i].Destroy();
                sinking.RemoveAt(i);
            }

            for (var i = debris.Count - 1; i >= 0; i--)
            {
                var piece = debris[i];
                piece.Age += delta;
                if (piece.Item == null || piece.Age >= piece.Life + 4f)
                {
                    if (piece.Item != null) Object.Destroy(piece.Item.gameObject);
                    debris.RemoveAt(i);
                    continue;
                }
                piece.Velocity *= Mathf.Exp(-0.25f * delta);
                var position = piece.Item.position + piece.Velocity * delta;
                // Floats at the waterline, rocking; at the end of its life it fills and goes down.
                var under = Mathf.Clamp01((piece.Age - piece.Life) / 4f);
                position.y = Mathf.Sin(clock * 1.3f + piece.Phase) * 0.08f - 0.04f - under * 2.5f;
                piece.Item.position = position;
                piece.Item.rotation = Quaternion.Euler(Mathf.Sin(clock * 0.9f + piece.Phase) * 6f, piece.Spin * delta, Mathf.Cos(clock * 1.1f + piece.Phase) * 6f) * piece.Item.rotation;
            }

            wrecks.RemoveAll(item => item == null);
            foreach (var wreck in wrecks)
            {
                // Each wreck rocks on its own beat, from where it lies (not from an instance id, which changes between runs).
                var phase = wreck.position.x * 0.13f + wreck.position.z * 0.29f;
                var position = wreck.position;
                position.y = Mathf.Sin(clock * 0.8f + phase) * 0.12f;
                wreck.position = position;
            }
        }

        static void Present(Effect effect, float t)
        {
            switch (effect.Kind)
            {
                case Kind.Arrow:
                    var position = Vector3.Lerp(effect.From, effect.To, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 9f);
                    var next = Vector3.Lerp(effect.From, effect.To, Mathf.Min(1f, t + 0.05f)) + Vector3.up * (Mathf.Sin(Mathf.Min(1f, t + 0.05f) * Mathf.PI) * 9f);
                    effect.Item.position = position;
                    if ((next - position).sqrMagnitude > 1e-4f) effect.Item.rotation = Quaternion.LookRotation(next - position);
                    // Drawn larger than life so a volley reads from the strategy camera.
                    effect.Item.localScale = Vector3.one * 2.4f;
                    break;
                case Kind.Flash:
                    effect.Item.position = effect.From;
                    effect.Item.localScale = Vector3.one * (effect.Size * (0.3f + t * 1.2f) * (1f - t * 0.6f));
                    break;
                case Kind.Rope:
                    Stretch(effect.Item, effect.From, effect.To, 0.3f);
                    break;
                case Kind.Ping:
                    effect.Item.position = effect.From;
                    effect.Item.localScale = new Vector3(effect.Size * (1f - t * 0.75f), 0.05f, effect.Size * (1f - t * 0.75f));
                    break;
            }
        }

        public static void Stretch(Transform item, Vector3 from, Vector3 to, float width)
        {
            var offset = to - from;
            item.position = (from + to) * 0.5f;
            if (offset.sqrMagnitude > 1e-4f) item.rotation = Quaternion.LookRotation(offset);
            item.localScale = new Vector3(width, width, offset.magnitude);
        }

        /// <summary>
        /// A ship going down, from its own visual. Each step places every part from the time since it began, so a long
        /// frame or a big step lands exactly where many small ones would.
        /// </summary>
        sealed class ShipFate
        {
            const float BreakAt = 0.6f, MastFallsAt = 1.0f, FloatOffAt = 2.0f;
            readonly Transform root;
            readonly ShipVisual view;
            readonly bool breakUp;
            readonly int seed;
            readonly float life;
            readonly float headDown;
            readonly float listSide;
            readonly Vector3 startPosition;
            readonly Quaternion startRotation;
            Transform[] pieces = System.Array.Empty<Transform>();
            Vector3[] pieceDrift = System.Array.Empty<Vector3>();
            float[] piecePitch = System.Array.Empty<float>();
            float[] pieceSink = System.Array.Empty<float>();
            float age;
            bool broken, mastDown, floated;

            public ShipFate(Transform root, ShipVisual view, bool breakUp, int seed)
            {
                this.root = root;
                this.view = view;
                this.breakUp = breakUp && view != null && view.Parts != null;
                this.seed = seed;
                var random = new System.Random(seed * 37 + 11);
                headDown = random.NextDouble() < 0.5 ? -1f : 1f;
                listSide = random.NextDouble() < 0.5 ? -1f : 1f;
                life = view == null ? 4f : this.breakUp ? 10f : 9f;
                startPosition = root != null ? root.position : Vector3.zero;
                startRotation = root != null ? root.rotation : Quaternion.identity;
                if (view != null) view.enabled = false;
            }

            public bool Step(float delta, BattleEffects effects)
            {
                if (root == null) return false;
                age += delta;
                if (age >= life) return false;
                if (view == null)
                {
                    // A plain object: down and over, as any sunk thing.
                    root.position = startPosition + Vector3.down * (1.6f * age);
                    root.rotation = startRotation * Quaternion.Euler(9f * age, 0f, 5f * age);
                    return true;
                }
                var model = view.Model;
                var parts = view.Parts;
                var form = parts.Form;
                if (breakUp && !broken && age >= BreakAt) Break(effects, model, parts);
                if (!mastDown && age >= MastFallsAt) mastDown = true;
                if (!floated && age >= FloatOffAt) FloatOff(effects, model, form);

                // Settling: lower and listing at once, then the long slide under, head or stern first.
                var settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 1.5f));
                var under = Mathf.Clamp01((age - 2.5f) / (life - 3f));
                var depth = settle * form.Freeboard * 0.45f + under * under * (form.Freeboard + form.Draft + 6f);
                var roll = listSide * (settle * 12f + under * 22f);
                var pitch = headDown * (settle * 3f + under * 20f);
                model.localPosition = new Vector3(0f, -depth, 0f);
                model.localRotation = Quaternion.Euler(pitch, 0f, roll);

                if (mastDown && view.Rig != null)
                {
                    // The mast falls over the listing side, pivoting on its foot, the yard and sail with it.
                    var fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - MastFallsAt) / 1.6f));
                    var foot = parts.MastFoot;
                    var turn = Quaternion.Euler(0f, 0f, listSide * 78f * fall);
                    view.Rig.localRotation = turn;
                    view.Rig.localPosition = foot - turn * foot;
                    if (view.Sail != null)
                    {
                        view.Sail.localRotation = turn;
                        view.Sail.localPosition = foot + turn * (parts.SailTop - foot);
                        view.Sail.localScale = Vector3.Scale(view.Sail.localScale, new Vector3(1f, Mathf.Lerp(1f, 0.6f, fall), 1f));
                    }
                }

                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] == null) continue;
                    var t = age - BreakAt;
                    var apart = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 2.5f));
                    var down = Mathf.Clamp01((t - 0.5f) / (life - BreakAt - 1f));
                    pieces[i].localPosition = pieceDrift[i] * apart + Vector3.down * (down * down * pieceSink[i]);
                    pieces[i].localRotation = Quaternion.Euler(piecePitch[i] * apart, 0f, listSide * 8f * apart);
                }
                return true;
            }

            void Break(BattleEffects effects, Transform model, GreekShipParts parts)
            {
                broken = true;
                foreach (var name in new[] { "Hull", "Deck", "Ram", "Steering oars", "Band", "Look" })
                {
                    var part = model.Find(name);
                    if (part != null) part.gameObject.SetActive(false);
                }
                // Breaches, stuck arrows and mended planks were on the whole hull; they go with it.
                foreach (Transform child in model)
                    if (child.name == "Breach" || child.name == "Arrow" || child.name == "Fresh repair") child.gameObject.SetActive(false);
                pieces = new Transform[parts.Sections.Length];
                pieceDrift = new Vector3[pieces.Length];
                piecePitch = new float[pieces.Length];
                pieceSink = new float[pieces.Length];
                var random = new System.Random(seed * 59 + 3);
                var length = parts.Form.Length;
                for (var i = 0; i < pieces.Length; i++)
                {
                    // The pieces keep the ship's own pitch, ochre and timber.
                    pieces[i] = view.AttachPiece(model, $"Broken piece {i}", parts.Sections[i]);
                    var end = i - 1;
                    pieceDrift[i] = new Vector3(((float)random.NextDouble() - 0.5f) * 1.5f, 0f, end * length * 0.08f);
                    // The ends rear up as the middle goes down first.
                    piecePitch[i] = end * -24f;
                    pieceSink[i] = (end == 0 ? 1.4f : 1f) * (parts.Form.Freeboard + parts.Form.Draft + 6f);
                }
                if (parts.Ram != null)
                {
                    var ram = view.AttachPiece(pieces[pieces.Length - 1], "Ram", parts.Ram);
                    ram.localPosition = Vector3.zero;
                }
                var cuts = new[] { -length * 0.5f + length * 0.36f, -length * 0.5f + length * 0.68f };
                foreach (var z in cuts)
                {
                    var at = model.TransformPoint(new Vector3(0f, parts.Form.Freeboard * 0.5f, z));
                    effects.Splinters(at, 2.5f, seed * 13 + Mathf.RoundToInt(z * 10f));
                    effects.WaterColumn(at, 2.2f, seed * 17 + Mathf.RoundToInt(z * 10f));
                }
            }

            void FloatOff(BattleEffects effects, Transform model, GreekShipForm form)
            {
                floated = true;
                var random = new System.Random(seed * 71 + 5);
                var oars = new List<Transform>();
                foreach (Transform child in model) if (child.name == "Oar") oars.Add(child);
                for (var i = 0; i < oars.Count; i++)
                {
                    if (i % 3 != 0) { oars[i].gameObject.SetActive(false); continue; }
                    var outward = Vector3.ProjectOnPlane(oars[i].position - model.position, Vector3.up).normalized;
                    effects.FloatOff(oars[i], outward * Mathf.Lerp(0.6f, 1.6f, (float)random.NextDouble()), seed * 7 + i);
                }
                // Loose gear (jars, sacks, baskets, cloth) floats off with the oars; stone anchors and rope go down with her.
                var gear = 0;
                foreach (var item in new List<Transform>(view.FloatingGear))
                {
                    var outward = Vector3.ProjectOnPlane(item.position - model.position, Vector3.up).normalized;
                    effects.FloatOff(item, outward * Mathf.Lerp(0.4f, 1.2f, (float)random.NextDouble()), seed * 11 + gear++);
                }
                var planks = Mathf.Clamp(Mathf.RoundToInt(form.Length / 3f), 4, 9);
                for (var i = 0; i < planks; i++)
                {
                    var at = model.TransformPoint(new Vector3(((float)random.NextDouble() - 0.5f) * form.HullBeam, 0f, ((float)random.NextDouble() - 0.5f) * form.Length * 0.8f));
                    at.y = 0f;
                    var velocity = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 2.2f;
                    effects.Float("plank", at, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), velocity, seed * 101 + i);
                }
                if (form.Cargo)
                {
                    for (var i = 0; i < 6; i++)
                    {
                        var at = model.TransformPoint(new Vector3(((float)random.NextDouble() - 0.5f) * form.HullBeam * 0.6f, 0f, ((float)random.NextDouble() - 0.5f) * form.Length * 0.3f));
                        at.y = -0.5f;
                        effects.Float("amphora", at, Quaternion.Euler(80f, (float)random.NextDouble() * 360f, 0f), new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f), seed * 131 + i);
                    }
                }
                effects.WaterColumn(model.position, 1.4f, seed * 3 + 1);
            }

            public void Destroy()
            {
                if (root != null) Object.Destroy(root.gameObject);
            }
        }
    }
}
