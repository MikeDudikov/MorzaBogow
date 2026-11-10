using System.Collections.Generic;
using MorzaBogow.Simulation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The crews aboard every ship in sight (docs/art/ZALOGI.md). It only reads the game: after each simulation tick it
    /// takes that tick's events once each (BattleController.Ticked) and the ships' numbers; each frame, after the hulls
    /// have their final pose, it decides what every man does from his ship's state, poses all of them in one Burst job and
    /// draws them instanced. Nothing here deals damage, fires a shot or moves a ship; shots it draws start where the man
    /// who loosed them stands (BattleEffects.LaunchPoint). Time is the game's: paused, the men stand still while the sea
    /// keeps moving the hulls they stand on.
    /// </summary>
    public sealed class CrewDirector : MonoBehaviour
    {
        /// <summary>Pixels per metre at which the men get their full detail, and below which their coarse one (with 10% hysteresis).</summary>
        public const float FullDetail = 15f, CoarseDetail = 5f;
        const float LooseSeconds = 0.45f, DrawSeconds = 0.6f, WindupSeconds = 0.5f, JoltSeconds = 0.8f, FallSeconds = 1.1f, BodySeconds = 6f;
        const int MaxBodies = 4;

        BattleController battle;
        World world;
        int watermark;
        float clock;
        int lod = 1;
        readonly Dictionary<int, CrewAboard> crews = new Dictionary<int, CrewAboard>();
        readonly List<int> order = new List<int>();
        readonly List<Figure> figures = new List<Figure>();
        readonly Dictionary<int, (int ship, int slot)> launches = new Dictionary<int, (int, int)>();
        readonly Dictionary<long, int> figureOf = new Dictionary<long, int>();
        CrewRenderer renderer;
        NativeArray<FigureInput> inputs;
        NativeArray<float4x4> bones;
        // Each drawn ship's model this frame, in the order its men were emitted (FigureInput.ModelSlot counts from 1).
        NativeArray<float4x4> models;
        readonly List<float4x4> shipModels = new List<float4x4>();
        long signature = -1;
        Bounds drawn;

        /// <summary>Men drawn in the last frame, ships whose crews were drawn, the level of detail; tests and the smoke report.</summary>
        public int FiguresDrawn { get; private set; }
        public int ShipsDrawn { get; private set; }

        /// <summary>Men at work ashore drawn in the last frame: shore parties, dock gangs, shipwrights (CrewAshore); also in FiguresDrawn.</summary>
        public int AshoreDrawn { get; private set; }
        public int Lod => lod;
        public CrewRenderer Renderer => renderer;
        public double PoseMilliseconds { get; private set; }

        public IReadOnlyDictionary<int, CrewAboard> Crews => crews;

        /// <summary>
        /// Men at the rails of every grappled boarding the player can see: each side's front as the simulation fights it,
        /// never more than its fighters (BattleController.DeckFightersShown). Read from the game, so it holds between frames.
        /// </summary>
        public int RailFighters
        {
            get
            {
                if (battle == null || battle.World == null) return 0;
                var total = 0;
                foreach (var link in battle.World.Boardings)
                {
                    if (!link.Hooked || !battle.Views.TryGetValue(link.AttackerId, out var a) || !battle.Views.TryGetValue(link.TargetId, out var t)) continue;
                    if (a == null || t == null || !a.gameObject.activeSelf || !t.gameObject.activeSelf) continue;
                    var attacker = battle.World.FindShip(link.AttackerId);
                    var target = battle.World.FindShip(link.TargetId);
                    if (attacker == null || target == null) continue;
                    total += Front(link.AttackerFront, attacker, World.BoardingFront(attacker, target));
                    total += Front(link.TargetFront, target, World.BoardingFront(attacker, target));
                }
                return total;
            }
        }

        /// <summary>One boarding's haul: how the attacker's picture is brought alongside its target and let go again.</summary>
        sealed class Haul
        {
            public int TargetId;
            /// <summary>The target's side the attacker lies against: +1 starboard, -1 port; fixed for the whole boarding.</summary>
            public int Side;
            public int ThrowTick;
            public float Duration, HeadingFrom;
            public Vector3 Offset;
            public float Yaw, Weight;
            public bool Active;
            public float Since, EndWeight, EndYaw;
        }

        readonly Dictionary<int, Haul> hauls = new Dictionary<int, Haul>();
        readonly Dictionary<int, float> lastHeading = new Dictionary<int, float>();

        /// <summary>Seconds over which a hull drifts back to its place in the game when its boarding ends.</summary>
        public const float ReleaseSeconds = 4f;
        /// <summary>How fast the grapples haul a hull alongside (m/s): a far throw takes longer, never under the hooks' 3 s nor over 7 s.</summary>
        const float HaulSpeed = 4f, HaulLongest = 7f, Gap = 0.2f;

        /// <summary>
        /// How far the picture of a ship is drawn from its place in the game while it is hauled alongside the ship it boards
        /// (the user's decisions of 2026-11-10: hauled by the grapples, and broadside to broadside, never bow first): the
        /// attacker's hull comes up beside the target's on the side nearer to it when the hooks flew, parallel to it and
        /// amidships to amidships, a hand's breadth between the rails; it lies there through the fight and drifts back over
        /// 4 s when the boarding ends. Only the picture moves: the game's positions, the fog and every rule are untouched.
        /// </summary>
        public Vector3 HaulOffset(int shipId) => hauls.TryGetValue(shipId, out var haul) ? haul.Offset * haul.Weight : Vector3.zero;

        /// <summary>How far the picture of a hauled ship is turned from its heading in the game (degrees about the up axis).</summary>
        public float HaulYaw(int shipId) => hauls.TryGetValue(shipId, out var haul) ? (haul.Active ? haul.Yaw : haul.EndYaw * (haul.EndWeight > 1e-4f ? haul.Weight / haul.EndWeight : 0f)) : 0f;

        /// <summary>How far a boarding's attacker has been hauled alongside (0 to 1): 1 with the rails touching.</summary>
        public float HaulWeight(int attackerId) => hauls.TryGetValue(attackerId, out var haul) ? haul.Weight : 0f;

        /// <summary>
        /// The side of a ship that lies against another in a boarding (its oars come in there): -1 port, +1 starboard, 0 none.
        /// </summary>
        public int ContactSide(int shipId)
        {
            foreach (var pair in hauls)
            {
                if (pair.Value.Weight < 0.3f) continue;
                if (pair.Key == shipId) return -pair.Value.Side;
                if (pair.Value.TargetId == shipId) return pair.Value.Side;
            }
            return 0;
        }

        /// <summary>The weight of a released haul after <paramref name="since"/> seconds: back to its place linearly in 4 s.</summary>
        public static float ReleaseWeight(float endWeight, float since) => endWeight * Mathf.Max(0f, 1f - since / ReleaseSeconds);

        /// <summary>Each boarding's haul for this frame (BattleController places the attacker's hull with HaulOffset and HaulYaw).</summary>
        public void UpdateHauls(float delta)
        {
            if (battle == null || battle.World == null) return;
            Refresh(battle.World);
            // Changes are made after the walk over the table: Mono's dictionary forbids even replacing a value meanwhile.
            var ended = new List<int>();
            foreach (var pair in hauls)
            {
                var haul = pair.Value;
                if (haul.Active) continue;
                haul.Since += delta;
                haul.Weight = ReleaseWeight(haul.EndWeight, haul.Since);
                if (haul.Weight <= 0f || battle.World.FindShip(pair.Key) == null) ended.Add(pair.Key);
            }
            foreach (var id in ended) hauls.Remove(id);
            foreach (var ship in battle.World.Ships) lastHeading[ship.Id] = HeadingDegrees(ship);
        }

        /// <summary>
        /// The hauls of the boardings in the game now, from the game's ticks alone (so the same at any frame rate and however
        /// often it is called): a boarding seen for the first time takes its side and its haul's length; one gone is let go.
        /// </summary>
        void Refresh(World world)
        {
            var hookTicks = (int)(Rules.BoardHookSeconds * Rules.TickRate);
            foreach (var pair in hauls) pair.Value.Active = pair.Value.Active && world.Boardings.Exists(link => link.AttackerId == pair.Key);
            foreach (var link in world.Boardings)
            {
                var attacker = world.FindShip(link.AttackerId);
                var target = world.FindShip(link.TargetId);
                if (attacker == null || target == null) continue;
                if (!hauls.TryGetValue(link.AttackerId, out var haul) || !haul.Active || haul.TargetId != link.TargetId)
                {
                    haul = new Haul
                    {
                        Active = true, TargetId = link.TargetId, Side = SideOf(attacker, target),
                        ThrowTick = world.Tick - (link.Hooked ? hookTicks : hookTicks - link.HookTicks),
                        HeadingFrom = lastHeading.TryGetValue(attacker.Id, out var before) ? before : HeadingDegrees(attacker),
                    };
                    var reach = Vector3.Distance(Alongside(attacker, target, haul.Side), SimPosition(attacker));
                    haul.Duration = Mathf.Clamp(reach / HaulSpeed, (float)Rules.BoardHookSeconds, HaulLongest);
                    hauls[link.AttackerId] = haul;
                }
                var elapsed = (world.Tick - haul.ThrowTick) * (float)Rules.Dt;
                haul.Weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / haul.Duration));
                haul.Offset = Alongside(attacker, target, haul.Side) - SimPosition(attacker);
                // The picture turns from the heading the attacker had when the hooks flew to the target's (the game snaps it).
                var picture = Mathf.LerpAngle(haul.HeadingFrom, HeadingDegrees(target), haul.Weight);
                haul.Yaw = Mathf.DeltaAngle(picture, HeadingDegrees(attacker));
            }
            foreach (var pair in hauls)
            {
                var haul = pair.Value;
                if (haul.Active || haul.EndWeight > 0f || haul.Since > 0f) continue;
                haul.EndWeight = haul.Weight;
                haul.EndYaw = haul.Yaw;
            }
        }

        /// <summary>Where the attacker's centre lies alongside the target (the target as drawn, should it be hauled itself).</summary>
        Vector3 Alongside(Ship attacker, Ship target, int side)
        {
            var targetAt = SimPosition(target) + HaulOffset(target.Id);
            var turn = Quaternion.Euler(0f, 90f - HeadingDegrees(target), 0f);
            var apart = GreekShips.HalfWidth(GreekShips.Form(target.Class), 0.5f) + GreekShips.HalfWidth(GreekShips.Form(attacker.Class), 0.5f) + Gap;
            return targetAt + turn * new Vector3(side * apart, 0f, 0f);
        }

        /// <summary>The target's side nearer to the attacker; in line (a chase) the lower id decides, the same each time.</summary>
        static int SideOf(Ship attacker, Ship target)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(0f, 90f - HeadingDegrees(target), 0f)) * (SimPosition(attacker) - SimPosition(target));
            if (Mathf.Abs(local.x) > 0.5f) return local.x > 0f ? 1 : -1;
            return attacker.Id < target.Id ? 1 : -1;
        }

        static Vector3 SimPosition(Ship ship) => new Vector3((float)ship.X, 0f, (float)ship.Y);

        static float HeadingDegrees(Ship ship) => (float)(ship.Heading * Mathf.Rad2Deg);

        /// <summary>A side's front: the last round's, or before the first round the width of the decks; at most its fighters.</summary>
        public static int Front(int lastRound, Ship own, int width) => Mathf.Min(lastRound > 0 ? lastRound : width, own.Soldiers);

        void Awake()
        {
            battle = GetComponent<BattleController>();
            renderer = new CrewRenderer();
            // -morzaNoCrew: the same build without the crews drawn, for measuring what they cost (A/B).
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-morzaNoCrew") >= 0) enabled = false;
        }

        // What the crews cost in a run, written to the log when the battle ends (smoke runs read it from Player.log).
        double costMs;
        long costFigures, costDraws;
        int costFrames;

        void OnApplicationQuit()
        {
            if (costFrames == 0) return;
            Debug.Log($"Crew cost: {costFrames} frames, {costFigures / (double)costFrames:0} men drawn, {costDraws / (double)costFrames:0} draws, " +
                      $"{costMs / costFrames:0.000} ms a frame to decide, pose, place and submit (main thread)");
        }

        void OnEnable()
        {
            if (battle == null) battle = GetComponent<BattleController>();
            if (battle != null) battle.Ticked += OnTicked;
        }

        void OnDisable()
        {
            if (battle != null) battle.Ticked -= OnTicked;
            if (battle != null && battle.Effects != null && battle.Effects.LaunchPoint == (System.Func<Projectile, int, Vector3?>)LaunchPoint) battle.Effects.LaunchPoint = null;
        }

        void OnDestroy()
        {
            renderer?.Dispose();
            if (inputs.IsCreated) inputs.Dispose();
            if (bones.IsCreated) bones.Dispose();
            if (models.IsCreated) models.Dispose();
        }

        /// <summary>A new match or a loaded one: nothing of the last is kept; the next events are those after the load.</summary>
        void Reset(World next)
        {
            world = next;
            watermark = next != null ? next.NextEventId - 1 : 0;
            crews.Clear();
            launches.Clear();
            figureOf.Clear();
            // A loaded game starts its boardings' hauls afresh from its own ticks (HaulOffset).
            hauls.Clear();
            lastHeading.Clear();
            signature = -1;
        }

        // Every tick ------------------------------------------------------------------------------------------------
        /// <summary>Each event once, by its id; the crews' numbers after the tick; which machine each new shot left from.</summary>
        void OnTicked(World current)
        {
            if (current != world) Reset(current);
            if (battle != null && battle.Effects != null) battle.Effects.LaunchPoint = LaunchPoint;
            var hitShips = new Dictionary<int, Vector3>();
            // A ship taken or a boarding beaten off changes its crew by the rules of capture, not by blows: no one falls.
            var settled = new HashSet<int>();
            foreach (var item in current.Events)
            {
                if (item.EventId <= watermark) continue;
                watermark = item.EventId;
                var at = new Vector3((float)item.X, 0f, (float)item.Y);
                switch (item.Type)
                {
                    case "weapon_fired":
                        if (crews.TryGetValue(item.SubjectId, out var shooter)) shooter.Fired((int)(item.Amount % 100), clock, at, item.Tick);
                        MapLaunches(current, item.SubjectId, (int)(item.Amount % 100), (WeaponType)(int)(item.Amount / 100), item.Tick);
                        break;
                    case "ram_hit":
                        if (crews.TryGetValue(item.SubjectId, out var rammer)) rammer.Jolt(clock, Mathf.Clamp01((float)item.Amount / 300f + 0.4f), null);
                        if (crews.TryGetValue(item.OtherId, out var rammed)) rammed.Jolt(clock, Mathf.Clamp01((float)item.Amount / 250f + 0.5f), null);
                        break;
                    case "ram_failed":
                        if (crews.TryGetValue(item.SubjectId, out var missed)) missed.Jolt(clock, 0.35f, null);
                        break;
                    case "projectile_hit":
                        hitShips[item.SubjectId] = at;
                        if (crews.TryGetValue(item.SubjectId, out var struck)) struck.Jolt(clock, 0.6f, at);
                        break;
                    case "ship_surrendered":
                        if (crews.TryGetValue(item.SubjectId, out var yielding)) yielding.Surrender(clock);
                        break;
                    case "ship_captured":
                    case "boarding_repelled":
                        settled.Add(item.SubjectId);
                        settled.Add(item.OtherId);
                        break;
                }
            }
            // Losses: a man falls only when his trade shrinks in a tick the ship was struck or fought a boarding round.
            foreach (var ship in current.Ships)
            {
                if (!crews.TryGetValue(ship.Id, out var crew)) continue;
                var fought = false;
                foreach (var link in current.Boardings)
                    if (link.Hooked && link.CycleTicks == 0 && (link.AttackerId == ship.Id || link.TargetId == ship.Id)) fought = true;
                var struckAt = hitShips.TryGetValue(ship.Id, out var point) ? point : (Vector3?)null;
                crew.Count(ship, clock, (struckAt.HasValue || fought) && !settled.Contains(ship.Id), struckAt);
            }
        }

        /// <summary>The shots a weapon loosed this tick, in the order the simulation made them, tied to its post.</summary>
        void MapLaunches(World current, int shipId, int slot, WeaponType weapon, int tick)
        {
            foreach (var shot in current.Projectiles)
            {
                if (shot.LaunchTick != tick || shot.ShooterId != shipId || shot.Weapon != weapon || launches.ContainsKey(shot.Id)) continue;
                launches[shot.Id] = (shipId, slot);
                break;
            }
            if (launches.Count > 4096)
            {
                var stale = new List<int>();
                foreach (var id in launches.Keys) if (current.Projectiles.TrueForAll(p => p.Id != id)) stale.Add(id);
                foreach (var id in stale) launches.Remove(id);
            }
        }

        /// <summary>Where shaft i of a shot leaves its ship: the hand of the man of its post who loosed it, or the machine's muzzle.</summary>
        Vector3? LaunchPoint(Projectile shot, int shaft)
        {
            if (!launches.TryGetValue(shot.Id, out var source) || !crews.TryGetValue(source.ship, out var crew) || crew.View == null) return null;
            return crew.LaunchPoint(source.slot, shaft, bones, figureOf);
        }

        // Every frame -----------------------------------------------------------------------------------------------
        void LateUpdate()
        {
            if (battle == null || battle.World == null) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            Present(battle.Paused ? 0f : Time.unscaledDeltaTime * battle.Speed, Camera.main);
            if (Time.realtimeSinceStartup < 5f) return;
            costMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            costFigures += FiguresDrawn;
            costDraws += renderer.Draws;
            costFrames++;
        }

        /// <summary>
        /// The shown men of one shore party, dock gang or slipway into the frame's figures, when the camera can see where
        /// they stand: each as the man he is (CrewAshore.Identify), counted in the frame's signature and bounds.
        /// </summary>
        void EmitAshore(IReadOnlyList<ShoreMan> men, int place, CrewAshore.Group group, int owner, Camera camera, ref long sign, ref bool any)
        {
            if (!CrewAshore.BoxOf(men, out var box)) return;
            if (camera != null && !GeometryUtility.TestPlanesAABB(frustum, box)) return;
            CrewAboard aboard = null;
            if (group == CrewAshore.Group.Party) crews.TryGetValue(place, out aboard);
            for (var i = 0; i < men.Count; i++)
            {
                var man = men[i];
                if (!man.Shown) continue;
                CrewAshore.Identify(man, place, group, owner, aboard);
                figures.Add(CrewAshore.Build(man, place, group, lod));
                sign = sign * 31 + CrewAshore.Signature(man);
                AshoreDrawn++;
            }
            if (!any) drawn = box; else drawn.Encapsulate(box);
            any = true;
        }

        /// <summary>Decides, poses and draws every crew in sight; public so tests can run a frame after RunTicks.</summary>
        public void Present(float delta, Camera camera)
        {
            if (battle.World != world) Reset(battle.World);
            clock += delta;
            ChooseLod(camera);
            if (world != null) Refresh(world);
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            figures.Clear();
            shipModels.Clear();
            order.Clear();
            foreach (var pair in battle.Views) order.Add(pair.Key);
            order.Sort();
            long sign = lod;
            drawn = new Bounds();
            var any = false;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            long deciding = 0, building = 0;
            foreach (var id in order)
            {
                var view = battle.Views[id];
                var ship = world.FindShip(id);
                if (view == null || ship == null) continue;
                if (!crews.TryGetValue(id, out var crew) || crew.View != view || crew.ShipId != view.ShipId)
                {
                    crew = new CrewAboard(ship, view, world);
                    crews[id] = crew;
                }
                // Every crew keeps its stations and its ship's oars; only the crews in sight decide each man's doing.
                var shown = view.gameObject.activeInHierarchy && (camera == null || Visible(frustum, view));
                // The men of its shore party who are off the ship are drawn ashore (CrewAshore), not aboard.
                crew.PartyAshore = battle.Crews.TryGetValue(id, out var party) ? party.AshoreMask : 0;
                // A grappled boarding: the other ship's view (its deck to cross to, or the boarders to face) and the haul.
                crew.BoardingView = null;
                crew.BoardingContact = 0f;
                crew.BoardingAttacker = false;
                foreach (var link in world.Boardings)
                {
                    if (!link.Hooked || link.AttackerId != id && link.TargetId != id) continue;
                    if (!battle.Views.TryGetValue(link.AttackerId == id ? link.TargetId : link.AttackerId, out var otherView) || otherView == null) continue;
                    crew.BoardingView = otherView;
                    crew.BoardingContact = HaulWeight(link.AttackerId);
                    crew.BoardingAttacker = link.AttackerId == id;
                }
                var before = System.Diagnostics.Stopwatch.GetTimestamp();
                crew.Update(ship, view, world, clock, delta, shown);
                var decided = System.Diagnostics.Stopwatch.GetTimestamp();
                deciding += decided - before;
                if (!shown) continue;
                shipModels.Add(view.Model.localToWorldMatrix);
                crew.Emit(figures, lod, clock, shipModels.Count);
                building += System.Diagnostics.Stopwatch.GetTimestamp() - decided;
                sign = sign * 31 + id * 7 + crew.Version;
                sign = sign * 31 + crew.PartyAshore;
                var box = new Bounds(view.transform.position, new Vector3(view.Length + 6f, 12f, view.Length + 6f));
                if (!any) drawn = box; else drawn.Encapsulate(box);
                any = true;
            }
            // A ship gone down: its hull is the effects' now (BattleEffects.Sink); its men go over the side as it settles,
            // then are gone. Crews of ships that left the game otherwise (scrapped, out of sight for good) go at once.
            gone.Clear();
            foreach (var pair in crews)
            {
                if (battle.Views.ContainsKey(pair.Key)) continue;
                var crew = pair.Value;
                if (crew.View == null || world.FindShip(pair.Key) != null || crew.View.enabled) { gone.Add(pair.Key); continue; }
                if (!crew.Abandon(clock)) { gone.Add(pair.Key); continue; }
                if (!crew.View.gameObject.activeInHierarchy) continue;
                shipModels.Add(crew.View.Model.localToWorldMatrix);
                crew.Emit(figures, lod, clock, shipModels.Count);
                sign = sign * 31 + pair.Key * 7 + crew.Version;
            }
            foreach (var id in gone) crews.Remove(id);
            // The men at work ashore: ships' shore parties, ports' dock gangs and the shipwrights at the slipways, on the
            // same bodies and in the same draws (CrewAshore); only those the player can see are shown.
            AshoreDrawn = 0;
            foreach (var pair in battle.Crews) EmitAshore(pair.Value.Men, pair.Key, CrewAshore.Group.Party, pair.Value.Owner, camera, ref sign, ref any);
            foreach (var pair in battle.DockGangs) EmitAshore(pair.Value.Men, pair.Key, CrewAshore.Group.Dock, pair.Value.Owner, camera, ref sign, ref any);
            foreach (var pair in battle.Yards)
            {
                var yard = pair.Value;
                if (yard == null) continue;
                if (yard.War.InSight) EmitAshore(yard.War.Shipwrights, pair.Key, CrewAshore.Group.Yard, yard.War.Owner, camera, ref sign, ref any);
                if (yard.Port.InSight) EmitAshore(yard.Port.Shipwrights, pair.Key, CrewAshore.Group.Yard, yard.Port.Owner, camera, ref sign, ref any);
            }
            FiguresDrawn = figures.Count;
            ShipsDrawn = 0;
            foreach (var id in order) if (crews.TryGetValue(id, out var c) && c.DrawnFigures > 0) ShipsDrawn++;
            if (figures.Count == 0) { PoseMilliseconds = 0; return; }

            if (!inputs.IsCreated || inputs.Length < figures.Count)
            {
                if (inputs.IsCreated) inputs.Dispose();
                if (bones.IsCreated) bones.Dispose();
                var capacity = Mathf.NextPowerOfTwo(figures.Count + 64);
                inputs = new NativeArray<FigureInput>(capacity, Allocator.Persistent);
                bones = new NativeArray<float4x4>(capacity * CrewRenderer.Matrices, Allocator.Persistent);
                signature = -1;
            }
            if (!models.IsCreated || models.Length < shipModels.Count)
            {
                if (models.IsCreated) models.Dispose();
                models = new NativeArray<float4x4>(Mathf.NextPowerOfTwo(shipModels.Count + 16), Allocator.Persistent);
            }
            var posing = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < shipModels.Count; i++) models[i] = shipModels[i];
            for (var i = 0; i < figures.Count; i++) inputs[i] = figures[i].Input;
            // Frames from the ships' models first, then the poses: both on worker threads.
            var framing = new CrewFrameJob { Figures = inputs, Models = models }.Schedule(figures.Count, 64);
            new CrewPoseJob { Figures = inputs, Bones = bones }.Schedule(figures.Count, 16, framing).Complete();
            var listing = System.Diagnostics.Stopwatch.GetTimestamp();

            sign = sign * 31 + figures.Count;
            if (sign != signature)
            {
                signature = sign;
                figureOf.Clear();
                // Two passes over the pieces: count each mesh's, then each straight into its slot. A man's pieces keep
                // their buckets with them (BucketsOf), so no piece is looked up by its mesh.
                renderer.BeginList();
                for (var i = 0; i < figures.Count; i++)
                {
                    var buckets = BucketsOf(figures[i].Parts);
                    for (var p = 0; p < buckets.Length; p++) renderer.Count(buckets[p]);
                }
                renderer.Layout();
                for (var i = 0; i < figures.Count; i++)
                {
                    var figure = figures[i];
                    figureOf[Key(figure.Ship, figure.Man)] = i;
                    var parts = figure.Parts;
                    var buckets = BucketsOf(parts);
                    for (var p = 0; p < buckets.Length; p++) renderer.Place(i, parts[p].Bone, buckets[p], parts[p].Local);
                }
            }
            var drawing = System.Diagnostics.Stopwatch.GetTimestamp();
            PoseMilliseconds = (drawing - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            renderer.Draw(bones, camera, drawn, lod == 0, 0);
            Timings = (Ms(deciding), Ms(building), Ms(listing - posing), Ms(drawing - listing), Ms(System.Diagnostics.Stopwatch.GetTimestamp() - drawing));
            // Averaged over about a second of frames, for the logs of staged pictures and smoke runs.
            const double k = 0.05;
            var a = Averages;
            var t = Timings;
            Averages = (a.Decide + (t.Decide - a.Decide) * k, a.Build + (t.Build - a.Build) * k, a.Pose + (t.Pose - a.Pose) * k,
                        a.List + (t.List - a.List) * k, a.Draw + (t.Draw - a.Draw) * k);
            AverageFigures += (figures.Count - AverageFigures) * k;
            AverageDraws += (renderer.Draws - AverageDraws) * k;
        }

        /// <summary>
        /// Where the last frame's time went (ms): deciding each man's doing (every crew's upkeep included), building the
        /// figures, the pose job, laying out the pieces when they changed, placing and submitting the draws.
        /// </summary>
        public (double Decide, double Build, double Pose, double List, double Draw) Timings { get; private set; }

        /// <summary>The stages' times, the men drawn and the draws, each averaged over about a second of frames.</summary>
        public (double Decide, double Build, double Pose, double List, double Draw) Averages { get; private set; }
        public double AverageFigures { get; private set; }
        public double AverageDraws { get; private set; }

        static double Ms(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        // Each man's list of pieces (one per man and level of detail) with its meshes' buckets, worked out once.
        readonly Dictionary<List<(int Bone, Mesh Mesh, float4x4 Local)>, int[]> bucketsOf = new Dictionary<List<(int Bone, Mesh Mesh, float4x4 Local)>, int[]>();

        int[] BucketsOf(List<(int Bone, Mesh Mesh, float4x4 Local)> parts)
        {
            if (bucketsOf.TryGetValue(parts, out var buckets)) return buckets;
            // Lists of men long gone are let go now and then; the buckets themselves stay as long as the renderer.
            if (bucketsOf.Count > 20000) bucketsOf.Clear();
            buckets = new int[parts.Count];
            for (var p = 0; p < parts.Count; p++) buckets[p] = renderer.Bucket(parts[p].Mesh);
            bucketsOf[parts] = buckets;
            return buckets;
        }

        public static long Key(int ship, int man) => (long)ship << 20 | (uint)man;

        readonly Plane[] frustum = new Plane[6];
        readonly List<int> gone = new List<int>();

        static bool Visible(Plane[] planes, ShipVisual view) =>
            GeometryUtility.TestPlanesAABB(planes, new Bounds(view.transform.position, new Vector3(view.Length + 4f, 14f, view.Length + 4f)));

        /// <summary>The level of detail by the camera's pixels per metre at 1080 lines; one for the whole frame (orthographic).</summary>
        /// <summary>The level of detail without a camera (tests and tools): 1 by default, as at the default strategic camera.</summary>
        public int LodWithoutCamera = 1;

        void ChooseLod(Camera camera)
        {
            if (camera == null)
            {
                if (lod == LodWithoutCamera) return;
                lod = LodWithoutCamera;
                foreach (var crew in crews.Values) crew.Dirty();
                return;
            }
            var pixels = PixelsPerMetre(camera);
            var next = lod;
            if (pixels >= FullDetail * (lod == 0 ? 0.85f : 1.15f)) next = 0;
            else if (pixels >= CoarseDetail * (lod == 2 ? 1.15f : 0.85f)) next = 1;
            else next = 2;
            if (next != lod)
            {
                lod = next;
                foreach (var crew in crews.Values) crew.Dirty();
            }
        }

        public static float PixelsPerMetre(Camera camera) =>
            camera.orthographic ? Screen.height / Mathf.Max(0.01f, 2f * camera.orthographicSize) : Screen.height / Mathf.Max(0.01f, 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 150f);
    }

    /// <summary>One man to draw this frame: his pose's input and the pieces he is drawn with.</summary>
    public struct Figure
    {
        public int Ship, Man;
        public FigureInput Input;
        public List<(int Bone, Mesh Mesh, float4x4 Local)> Parts;
    }
}
