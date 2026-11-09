using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Adapter between the pure simulation and Unity: fixed-step driver, interpolated views, camera,
    /// orders and a working M1 HUD. It only reads world state and sends commands.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        public const int PlayerFaction = 0;
        const int MaxStepsPerFrame = 8;
        static readonly float[] Speeds = { 0.5f, 1f, 1.5f, 2f };

        [SerializeField] GameObject coastPrefab;
        [SerializeField] PlaceholderMaterials materials = new PlaceholderMaterials();
        [SerializeField] Camera strategicCamera;
        [SerializeField] ulong seed = 20261109;

        readonly Dictionary<int, ShipVisual> views = new Dictionary<int, ShipVisual>();
        readonly Dictionary<int, int> viewOwners = new Dictionary<int, int>();
        readonly Dictionary<int, Vector3> previous = new Dictionary<int, Vector3>();
        readonly Dictionary<int, GameObject> nodeViews = new Dictionary<int, GameObject>();
        readonly Dictionary<int, GameObject> settlementViews = new Dictionary<int, GameObject>();
        readonly Dictionary<int, GameObject> freeShoreViews = new Dictionary<int, GameObject>();
        readonly HashSet<int> selected = new HashSet<int>();
        readonly Dictionary<int, int[]> groups = new Dictionary<int, int[]>();
        readonly List<GameObject> mapObjects = new List<GameObject>();
        readonly Dictionary<int, GameObject> wreckViews = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Transform> destinationMarkers = new Dictionary<int, Transform>();
        readonly Dictionary<int, ResourceSiteVisual> siteViews = new Dictionary<int, ResourceSiteVisual>();
        readonly HashSet<int> targetedShips = new HashSet<int>();
        WaterSampler water;
        int waterCursor;
        float maxRollSeen, maxHeaveSeen;
        BattleFog fog;
        float fogTimer;
        GameObject settlementTargetMarker;
        string smokeShotPrefix;
        int smokeShots;
        float smokeShotAt;
        readonly Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();
        readonly HashSet<int> sinkingShips = new HashSet<int>();
        BattleEffects effects;
        readonly HashSet<int> inspectedEnemies = new HashSet<int>();
        int inspectedSettlementId = -1;

        Opponent opponent;
        Opponent autoPlayer;
        double accumulator;
        int speedIndex = 1;
        bool paused;
        string notice = string.Empty;
        float noticeUntil;
        Vector3 pivot;
        float yaw = ObliqueCamera.DefaultYaw;
        float tilt = ObliqueCamera.DefaultTilt;
        Light sun;
        Transform decalAnchor;
        sealed class SurfPatch
        {
            public UnityEngine.Rendering.HighDefinition.WaterDecal Decal;
            public Island Island;
            public ParticleSystem Spray;
            public float Strength, Phase, Exposure, LastSet;
            public Vector3 Outward;
        }

        readonly List<SurfPatch> surf = new List<SurfPatch>();

        // Shore parties of ships, and dock gangs of ports, keyed by ship or settlement id.
        readonly Dictionary<int, ShipCrew> crews = new Dictionary<int, ShipCrew>();
        readonly Dictionary<int, ShipCrew> dockGangs = new Dictionary<int, ShipCrew>();
        readonly HashSet<int> docked = new HashSet<int>();
        Transform crewRoot;

        public IReadOnlyDictionary<int, ShipCrew> Crews => crews;
        public IReadOnlyDictionary<int, ShipCrew> DockGangs => dockGangs;

        /// <summary>
        /// Direction the large waves travel, from the water surface's own orientation. Shores facing into them take the
        /// full surf; sheltered sides get a gentle wash.
        /// </summary>
        Vector3 waveTravel = Vector3.right;

        float WaveExposure(Vector3 outward) => Mathf.Clamp01(Vector3.Dot(outward, -waveTravel));

        /// <summary>One emitter per rocky coast; it throws spray where a set of waves breaks on an exposed face.</summary>
        ParticleSystem ShorelineSpray(Transform parent)
        {
            var item = new GameObject("Breaking spray");
            item.transform.SetParent(parent, false);
            var system = item.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.maxParticles = 400;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (materials?.Spray != null) renderer.sharedMaterial = materials.Spray;
            system.Play();
            return system;
        }

        public int SurfCount => surf.Count;
        float height = 130f;
        bool dragging;
        Vector2 dragStart;
        string smokeReport;
        float smokeSeconds;
        float smokeElapsed;
        int smokeMaxVisibleEnemies;
        string coastScaleNote = "not used";
        float firstEnemySeenSeconds = -1f;
        int combatEvents;

        public World World { get; private set; }
        public IReadOnlyCollection<int> Selected => selected;
        public IReadOnlyDictionary<int, ShipVisual> Views => views;
        public IReadOnlyDictionary<int, GameObject> WreckViews => wreckViews;
        public BattleEffects Effects => effects;
        public BattleFog Fog => fog;
        public WaterSampler Water => water;

        /// <summary>Samples only some ships each frame; between samples the hull smoothly follows its last pose.</summary>
        const int WaterShipsPerFrame = 24;

        /// <summary>Bottom panels: a template list with a merchant and a shipyard button needs this much room.</summary>
        const float HudHeight = 190f;
        public IReadOnlyDictionary<int, ResourceSiteVisual> SiteViews => siteViews;
        public IReadOnlyDictionary<int, GameObject> SettlementViews => settlementViews;
        public IReadOnlyCollection<int> TargetedShips => targetedShips;
        public IReadOnlyCollection<int> InspectedEnemies => inspectedEnemies;
        public int InspectedSettlementId => inspectedSettlementId;

        /// <summary>A health bar appears only for what the player selected with the mouse and can still see.</summary>
        public bool ShowsHealthBar(Ship ship) =>
            World.CanSee(PlayerFaction, ship) && (selected.Contains(ship.Id) || inspectedEnemies.Contains(ship.Id));
        public bool Paused { get => paused; set => paused = value; }
        public float Speed => Speeds[speedIndex];

        public static string DefaultSavePath => Path.Combine(Application.persistentDataPath, "quicksave.mbsave");

        public void Configure(GameObject coast, PlaceholderMaterials placeholderMaterials, Camera mainCamera)
        {
            coastPrefab = coast;
            materials = placeholderMaterials;
            strategicCamera = mainCamera;
        }

        void Awake()
        {
            var surface = FindAnyObjectByType<UnityEngine.Rendering.HighDefinition.WaterSurface>();
            if (surface != null)
            {
                var orientation = surface.largeOrientationValue * Mathf.Deg2Rad;
                waveTravel = new Vector3(Mathf.Cos(orientation), 0f, Mathf.Sin(orientation));
            }
            StartMatch(MapFactory.CreateSkirmish(seed), 0);
            if (surface != null)
            {
                water = new WaterSampler(surface);

                // Bow waves and surf live in a square of water that follows the view, not the distant camera.
                decalAnchor = new GameObject("Water decal anchor").transform;
                surface.decalRegionAnchor = decalAnchor;
            }
            sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(light => light.type == LightType.Directional);
            ReadSmokeArguments();
        }

        void StartMatch(World world, int opponentBuildIndex)
        {
            ClearViews();
            if (effects == null) effects = new BattleEffects(materials);
            World = world;
            if (fog == null) fog = new BattleFog(materials?.Fog);
            opponent = new Opponent(1) { BuildIndex = opponentBuildIndex };
            BuildStaticViews();
            SyncViews();
            fog.Update(World, PlayerFaction, 0f, true);

            var home = World.SettlementOf(PlayerFaction);
            pivot = new Vector3((float)home.X + 60f, 0f, (float)home.Y);
            if (strategicCamera == null) strategicCamera = Camera.main;
        }

        void ReadSmokeArguments()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-morzaSmokeReport");
            if (index < 0 || index + 2 >= arguments.Length) return;
            smokeReport = arguments[index + 1];
            smokeSeconds = float.Parse(arguments[index + 2], System.Globalization.CultureInfo.InvariantCulture);
            var exposureIndex = Array.IndexOf(arguments, "-morzaExposure");
            if (exposureIndex >= 0 && exposureIndex + 1 < arguments.Length)
            {
                var value = float.Parse(arguments[exposureIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
                foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                {
                    if (volume.sharedProfile != null && volume.sharedProfile.TryGet<UnityEngine.Rendering.HighDefinition.Exposure>(out var exposure)) exposure.fixedExposure.Override(value);
                }
            }
            var shot = Array.IndexOf(arguments, "-morzaSmokeShots");
            if (shot >= 0 && shot + 1 < arguments.Length) smokeShotPrefix = arguments[shot + 1];
            autoPlayer = new Opponent(PlayerFaction);
            speedIndex = Speeds.Length - 1;
        }

        void Update()
        {
            HandleInput();
            if (!paused) Advance(Time.unscaledDeltaTime * Speed);
            PresentFrame();
            UpdateCamera();
            if (smokeReport != null) UpdateSmoke();
        }

        void Advance(double seconds)
        {
            accumulator += seconds;
            var steps = 0;
            while (accumulator >= Rules.Dt && steps < MaxStepsPerFrame)
            {
                StepOnce();
                accumulator -= Rules.Dt;
                steps++;
            }
            if (steps == MaxStepsPerFrame) accumulator = 0;
        }

        void StepOnce()
        {
            foreach (var ship in World.Ships) previous[ship.Id] = ToWorld(ship);
            autoPlayer?.Think(World);
            opponent.Think(World);
            foreach (var ship in World.Ships) lastPositions[ship.Id] = ToWorld(ship);
            World.Step();
            ProcessEvents();
            combatEvents += World.Events.Count(item => item.Type == "volley" || item.Type == "ram_hit" || item.Type == "hooks_thrown");
            World.Events.Clear();
            SyncViews();
        }

        /// <summary>Turns simulation events into effects, only where the player can see them.</summary>
        void ProcessEvents()
        {
            foreach (var item in World.Events)
            {
                var at = new Vector3((float)item.X, 0f, (float)item.Y);
                var seen = World.CanSee(PlayerFaction, item.X, item.Y);
                switch (item.Type)
                {
                    case "order_rejected":
                        if (World.FindShip(item.SubjectId)?.Owner == PlayerFaction) Notify("Rozkazu nie można wykonać: brak ładunku w porcie, nieodkryty lub zajęty brzeg.");
                        break;
                    case "store_full":
                        if (World.FindShip(item.SubjectId)?.Owner == PlayerFaction) Notify("Magazyn pełny: reszta ładunku została na statku.");
                        break;
                    case "outpost_built":
                        if (World.FindSettlement(item.OtherId)?.Owner == PlayerFaction) Notify("Przyczółek gotowy: ma własny magazyn, port i kolejkę portu.");
                        break;
                    case "ship_built":
                        if (seen || World.FindSettlement(item.OtherId)?.Owner == PlayerFaction) BeginLaunch(item.SubjectId, item.OtherId, item.Amount == 1);
                        break;
                    case "shipyard_built":
                        if (World.FindSettlement(item.SubjectId)?.Owner == PlayerFaction) Notify("Stocznia na przyczółku gotowa.");
                        break;
                    case "volley":
                        if (seen && lastPositions.TryGetValue(item.SubjectId, out var shooter)) effects.Arrows(shooter, at, (int)item.Amount);
                        break;
                    case "ram_hit":
                        if (seen) effects.Flash(at, 22f);
                        break;
                    case "ram_failed":
                        if (seen) effects.Flash(at, 8f);
                        break;
                    case "hooks_thrown":
                        if (seen && lastPositions.TryGetValue(item.SubjectId, out var boarder)) effects.Rope(boarder, at, 3f);
                        break;
                    case "ship_captured":
                        if (seen) effects.Flash(at, 16f);
                        break;
                    case "ship_sunk":
                        if (seen) { sinkingShips.Add(item.SubjectId); effects.Flash(at, 14f); }
                        break;
                }
            }
        }

        /// <summary>Advances whole simulation ticks; used by tests and tools.</summary>
        public void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) StepOnce();
            PresentFrame();
        }

        // ---------- Views ----------

        /// <summary>The map, shores, crews and yards are root objects of their own: they go with the battle.</summary>
        void OnDestroy()
        {
            ClearViews();
            if (crewRoot != null) Destroy(crewRoot.gameObject);
            crewRoot = null;
        }

        void ClearViews()
        {
            foreach (var view in views.Values) if (view != null) Destroy(view.gameObject);
            foreach (var item in mapObjects) if (item != null) Destroy(item);
            views.Clear();
            viewOwners.Clear();
            previous.Clear();
            nodeViews.Clear();
            settlementViews.Clear();
            freeShoreViews.Clear();
            mapObjects.Clear();
            selected.Clear();
            foreach (var view in wreckViews.Values) if (view != null) Destroy(view);
            wreckViews.Clear();
            foreach (var marker in destinationMarkers.Values) if (marker != null) Destroy(marker.gameObject);
            destinationMarkers.Clear();
            siteViews.Clear();
            targetedShips.Clear();
            if (settlementTargetMarker != null) Destroy(settlementTargetMarker);
            settlementTargetMarker = null;
            lastPositions.Clear();
            sinkingShips.Clear();
            inspectedEnemies.Clear();
            inspectedSettlementId = -1;
            foreach (var crew in crews.Values) crew.Destroy();
            foreach (var gang in dockGangs.Values) gang.Destroy();
            crews.Clear();
            dockGangs.Clear();
            docked.Clear();
            siteWorkplaces.Clear();
            portWorkplaces.Clear();
            yards.Clear();
            launches.Clear();
            recalled.Clear();
        }

        // ---------- Shipyards and launching ----------

        readonly Dictionary<int, ShipyardVisual> yards = new Dictionary<int, ShipyardVisual>();
        public IReadOnlyDictionary<int, ShipyardVisual> Yards => yards;

        /// <summary>A finished hull's run down its slipway into the sea, then afloat to where the game has it.</summary>
        sealed class Launch
        {
            public Vector3 RestPosition, SeaEnd;
            public Quaternion RestRotation;
            public float Elapsed;
            public bool Splashed;
        }

        const float SlideSeconds = 2.6f, FloatSeconds = 3.4f;
        readonly Dictionary<int, Launch> launches = new Dictionary<int, Launch>();
        public int LaunchesInProgress => launches.Count;
        public int LaunchesCompleted { get; private set; }

        void BeginLaunch(int shipId, int settlementId, bool war)
        {
            if (!yards.TryGetValue(settlementId, out var yard)) return;
            var slip = yard.Line(war);
            launches[shipId] = new Launch { RestPosition = slip.RestPosition, RestRotation = slip.RestRotation, SeaEnd = slip.SeaEnd };
        }

        /// <summary>
        /// Places a launching hull: it slides bow first down the ramp, gathering speed, meets the sea with a burst of
        /// spray, and then drifts out to its place in the game. False once it is done, when the game alone places it.
        /// </summary>
        bool PlaceLaunch(Ship ship, ShipVisual view, Vector3 gamePosition, Quaternion gameRotation, float delta)
        {
            if (!launches.TryGetValue(ship.Id, out var launch)) return false;
            launch.Elapsed += delta;
            if (launch.Elapsed < SlideSeconds)
            {
                var t = launch.Elapsed / SlideSeconds;
                var run = launch.SeaEnd - launch.RestPosition;
                var at = launch.RestPosition + run * (t * t);
                // The model floats its keel at the waterline; on the ramp it rides on the cradle instead.
                at.y += view.Draft;
                view.transform.SetPositionAndRotation(at, launch.RestRotation);
                return true;
            }

            if (!launch.Splashed)
            {
                launch.Splashed = true;
                view.transform.SetPositionAndRotation(new Vector3(launch.SeaEnd.x, 0f, launch.SeaEnd.z), Quaternion.Euler(0f, launch.RestRotation.eulerAngles.y, 0f));
                view.Splash(1f);
            }

            var drift = Mathf.Clamp01((launch.Elapsed - SlideSeconds) / FloatSeconds);
            var ease = drift * drift * (3f - 2f * drift);
            var from = new Vector3(launch.SeaEnd.x, 0f, launch.SeaEnd.z);
            view.transform.SetPositionAndRotation(Vector3.Lerp(from, gamePosition, ease),
                Quaternion.Slerp(Quaternion.Euler(0f, launch.RestRotation.eulerAngles.y, 0f), gameRotation, ease));
            if (drift < 1f) return true;
            launches.Remove(ship.Id);
            LaunchesCompleted++;
            return false;
        }

        /// <summary>Free water beside each side of a hull, to the nearest other hull, for its oars.</summary>
        void UpdateOarRoom(Ship ship, ShipVisual view)
        {
            float port = float.MaxValue, starboard = float.MaxValue;
            var cos = Math.Cos(ship.Heading);
            var sin = Math.Sin(ship.Heading);
            var reach = ship.Definition.Radius + Rules.OarReach(ship.Definition) + 2;
            foreach (var other in World.Ships)
            {
                if (other == ship) continue;
                var dx = other.X - ship.X;
                var dy = other.Y - ship.Y;
                if (dx * dx + dy * dy > Math.Pow(reach + other.Definition.Radius + Rules.OarReach(other.Definition), 2)) continue;
                // The other's own oars count too while it rows.
                var gap = (float)(World.HullGap(ship, other) - (Math.Abs(other.Speed) > 0.5 ? Rules.OarReach(other.Definition) : 0));
                // Left of the heading in the game's plane is the hull's port side.
                if (cos * dy - sin * dx > 0) port = Mathf.Min(port, gap);
                else starboard = Mathf.Min(starboard, gap);
            }

            // A jetty or quay alongside: a moored hull has its oars in anyway, as at rest.
            view.SetOarRoom(port, starboard);
        }

        // ---------- Shore parties ----------

        Workplace PortWorkplace(Settlement settlement, CrewLoad goods)
        {
            PortPlaces(settlement, out var jetty, out var landing, out var store);
            return new Workplace { JettyEnd = jetty, Landing = landing, Pile = store, Goods = goods };
        }

        static CrewLoad GoodsOf(ResourceType type) => type == ResourceType.Wood ? CrewLoad.Log : CrewLoad.Chest;

        readonly Dictionary<int, Workplace> siteWorkplaces = new Dictionary<int, Workplace>();
        readonly Dictionary<int, Workplace> portWorkplaces = new Dictionary<int, Workplace>();

        Workplace SiteWorkplace(ResourceNode node)
        {
            if (siteWorkplaces.TryGetValue(node.Id, out var place)) return place;
            if (!siteViews.TryGetValue(node.Id, out var site)) return null;
            place = new Workplace { JettyEnd = site.JettyEnd, Landing = site.Landing, Pile = site.StackSpot, Site = site, Goods = GoodsOf(node.Type) };
            siteWorkplaces[node.Id] = place;
            return place;
        }

        Workplace CachedPort(Settlement settlement, CrewLoad goods)
        {
            var key = settlement.Id * 4 + (int)goods;
            if (!portWorkplaces.TryGetValue(key, out var place)) portWorkplaces[key] = place = PortWorkplace(settlement, goods);
            return place;
        }

        /// <summary>What a ship's crew does ashore now, read from its order. Nothing here feeds back into the game.</summary>
        (CrewJob job, Workplace place) CrewJobOf(Ship ship)
        {
            if (ship.Order == OrderKind.Harvest && (ship.Harvest == HarvestPhase.Working || ship.Harvest == HarvestPhase.Landing))
            {
                var node = World.FindNode(ship.NodeId);
                if (node != null) return (node.Type == ResourceType.Wood ? CrewJob.Wood : CrewJob.Silver, SiteWorkplace(node));
            }
            if (ship.Order == OrderKind.Transfer || ship.Order == OrderKind.Expedition)
            {
                if (ship.Transport == TransportPhase.Loading)
                {
                    var source = World.FindSettlement(ship.SourceId);
                    if (source != null) return (CrewJob.Load, CachedPort(source, CrewLoad.Crate));
                }
                if (ship.Transport == TransportPhase.Unloading)
                {
                    var target = World.FindSettlement(ship.DestinationId);
                    if (target != null) return (CrewJob.Unload, CachedPort(target, CrewLoad.Crate));
                }
            }
            return (CrewJob.None, null);
        }

        /// <summary>Ships whose crew has been called back aboard before the ship casts off. Cleared when its job ends.</summary>
        readonly HashSet<int> recalled = new HashSet<int>();

        /// <summary>
        /// Game seconds until the ship's current shore job ends by the game's own rules: a full hold or an empty
        /// stock at a work point, or the last of the cargo handled in port. Read only from the game's state.
        /// </summary>
        double SecondsUntilDeparture(Ship ship)
        {
            var handling = Rules.HandlingPerSecond * Rules.Centi;
            if (ship.Order == OrderKind.Transfer || ship.Order == OrderKind.Expedition)
            {
                if (ship.Transport == TransportPhase.Loading) return (ship.WantWood + ship.WantSilver) / handling;
                if (ship.Transport == TransportPhase.Unloading) return (ship.HoldWood + ship.HoldSilver) / handling;
                return double.MaxValue;
            }
            if (ship.Order != OrderKind.Harvest) return double.MaxValue;
            if (ship.Harvest == HarvestPhase.Embarking) return ship.CrewTicks * Rules.Dt;
            if (ship.Harvest != HarvestPhase.Working && ship.Harvest != HarvestPhase.Landing) return double.MaxValue;
            // Landing still to finish before any work, and the embarking wait still to come after it.
            var fixedPart = Rules.EmbarkSeconds + (ship.Harvest == HarvestPhase.Landing ? ship.CrewTicks * Rules.Dt : 0);

            var node = World.FindNode(ship.NodeId);
            if (node == null) return 0;
            double Rate(Ship worker) => node.Type == ResourceType.Wood ? worker.Definition.WoodWork : worker.Definition.SilverWork;
            var total = 0.0;
            foreach (var id in node.Workers)
            {
                var worker = World.FindShip(id);
                if (worker != null && worker.Harvest == HarvestPhase.Working) total += Rate(worker);
            }
            var cap = node.Type == ResourceType.Wood ? Rules.WoodNodeCap : Rules.SilverNodeCap;
            var share = total > cap ? cap / total : 1.0;
            var own = Rate(ship) * share * Rules.Centi;
            if (own <= 0) return double.MaxValue;
            var room = ship.Definition.Capacity * Rules.Centi - ship.Cargo;
            var untilFull = room / own;
            var untilEmpty = total > 0 ? node.Stock / (total * share * Rules.Centi) : double.MaxValue;
            return fixedPart + Math.Min(untilFull, untilEmpty);
        }

        void UpdateCrews(float delta)
        {
            if (crewRoot == null) crewRoot = new GameObject("Shore parties").transform;
            foreach (var site in siteViews.Values) if (site != null) site.Tick(delta);

            foreach (var ship in World.Ships)
            {
                if (!views.TryGetValue(ship.Id, out var view)) continue;
                var (job, place) = CrewJobOf(ship);
                if (job == CrewJob.None) recalled.Remove(ship.Id);
                if (!crews.TryGetValue(ship.Id, out var crew))
                {
                    if (job == CrewJob.None || place == null || recalled.Contains(ship.Id)) continue;
                    crew = new ShipCrew(crewRoot, materials, ship.Owner, ship.Class == ShipClass.Merchant ? 6 : 4, ship.Id);
                    crews[ship.Id] = crew;
                }
                var deck = view.DeckToward(crew.JettyEnd);

                // The crew heads back aboard in time: when the hold will be full, the stock spent or the cargo moved
                // before they could walk back, they stop and board, so the hull never leaves anyone ashore.
                if (job != CrewJob.None && SecondsUntilDeparture(ship) <= crew.SecondsToBoard(deck) + 1.5f) recalled.Add(ship.Id);
                if (recalled.Contains(ship.Id)) job = CrewJob.None;

                crew.Assign(job, place);
                var visible = view.gameObject.activeSelf;
                crew.Update(deck, delta, visible);
                if (job == CrewJob.None && crew.AllAboard)
                {
                    crew.Destroy();
                    crews.Remove(ship.Id);
                }

                // A cutter moored in port hands its cargo over the gunwale; the port's dock gang carries it to the store.
                var inPort = ship.Order == OrderKind.Harvest && ship.Harvest == HarvestPhase.Docked && ship.Cargo > 0;
                if (inPort && docked.Add(ship.Id))
                {
                    var port = World.FindSettlement(ship.PortId);
                    if (port != null) Deliver(port, GoodsOf(ship.CargoType), (int)Mathf.Clamp(ship.Cargo / (20 * Rules.Centi), 1, 4));
                }
                else if (!inPort) docked.Remove(ship.Id);
            }

            foreach (var id in crews.Keys.Where(id => World.FindShip(id) == null).ToList())
            {
                crews[id].Destroy();
                crews.Remove(id);
            }

            foreach (var pair in dockGangs)
            {
                var settlement = World.FindSettlement(pair.Key);
                var visible = settlement != null && !settlement.Destroyed && (settlement.Owner == PlayerFaction || World.CanSee(PlayerFaction, settlement.X, settlement.Y));
                pair.Value.Update(pair.Value.JettyEnd, delta, visible);
            }
        }

        void Deliver(Settlement port, CrewLoad goods, int trips)
        {
            if (!dockGangs.TryGetValue(port.Id, out var gang))
            {
                gang = new ShipCrew(crewRoot, materials, port.Owner, 2, port.Id);
                dockGangs[port.Id] = gang;
            }
            var place = CachedPort(port, goods);
            if (gang.Job == CrewJob.Unload && gang.TripsLeft > 0) { gang.TripsLeft += trips; return; }
            gang.Assign(CrewJob.None, null);
            gang.TripsLeft = trips;
            gang.Assign(CrewJob.Unload, place);
        }

        void BuildStaticViews()
        {
            // Land under sites and settlements is drawn by their own models.
            foreach (var island in World.Islands.Where(item => item.Kind == IslandKind.Coast)) mapObjects.Add(CreateIsland(island));

            // Every shore follows the outline of its own land: the rocky coast's real waterline, an islet's ellipse.
            // Its seabed falls away along that outline, and surf breaks along it, not in a circle.
            surf.Clear();
            foreach (var island in World.Islands)
            {
                var outline = OutlineOf(island);
                var holder = new GameObject("Shore");
                holder.transform.position = new Vector3((float)island.X, 0f, (float)island.Y);

                var shelf = new GameObject("Underwater shelf");
                shelf.transform.SetParent(holder.transform, false);
                shelf.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Shelf(outline, island.X * 0.7 + island.Y * 1.3);
                var shelfRenderer = shelf.AddComponent<MeshRenderer>();
                shelfRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (materials?.Sand != null) shelfRenderer.sharedMaterial = materials.Sand;

                var coast = island.Kind == IslandKind.Coast;
                if (coast)
                {
                    // A sandy foreshore at the foot of the rock, so the land runs down into the water.
                    var beach = new GameObject("Beach");
                    beach.transform.SetParent(holder.transform, false);
                    beach.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Beach(outline);
                    var beachRenderer = beach.AddComponent<MeshRenderer>();
                    if (materials?.Sand != null) beachRenderer.sharedMaterial = materials.Sand;
                }

                // High rock throws breaking waves up as spray; low beaches only foam.
                var spray = coast ? ShorelineSpray(holder.transform) : null;
                foreach (var (point, along) in ShoreShape.SurfPoints(outline, ShoreShape.SurfSpacing))
                {
                    var decal = ShipVisual.WaterDecalAt(holder.transform, "Surf", materials?.FoamDisk, point, new Vector2(ShoreShape.SurfSize, ShoreShape.SurfSize));
                    decal.amplitude = 0f;

                    // Phase grows along the shore, so each set of waves runs along the coast instead of flashing at once.
                    surf.Add(new SurfPatch
                    {
                        Decal = decal, Island = island, Spray = spray,
                        Strength = coast ? 0.75f : 0.65f,
                        Phase = along * 0.08f + (float)(island.X * 0.013 + island.Y * 0.021),
                        Exposure = WaveExposure(point.normalized),
                        Outward = point.normalized,
                    });
                }
                holder.SetActive(island.Kind == IslandKind.Coast);
                mapObjects.Add(holder);
            }

            foreach (var settlement in World.Settlements)
            {
                var view = CreateSettlementView(settlement);
                settlementViews[settlement.Id] = view;
                mapObjects.Add(view);
            }

            foreach (var shore in World.Sites)
            {
                var view = CreateShore(shore.X, shore.Y, "Free shore [PLACEHOLDER]");
                view.SetActive(false);
                freeShoreViews[shore.Id] = view;
                mapObjects.Add(view);
            }

            foreach (var node in World.Nodes)
            {
                var site = ResourceSiteVisual.Create(node, materials);
                siteViews[node.Id] = site;
                nodeViews[node.Id] = site.gameObject;
                mapObjects.Add(site.gameObject);
            }
        }

        /// <summary>Replaceable placeholder: an islet with a jetty to the port, on the land ships cannot enter.</summary>
        GameObject CreateSettlementView(Settlement settlement)
        {
            var root = CreateShore(settlement.X, settlement.Y, $"Settlement {settlement.Owner} [PLACEHOLDER]");
            // The buildings keep to the landward half; the seaward half holds the jetty and the two slipways.
            var toward = Mathf.Sign((float)settlement.X - (float)Rules.SettlementLandX(settlement));
            var width = settlement.IsOutpost ? 9f : 11f;
            var depth = settlement.IsOutpost ? 12f : 16f;
            var tall = settlement.IsOutpost ? 4f : 6f;
            AddViewPart(root, PrimitiveType.Cube, "Buildings", materials?.Neutral, new Vector3(-toward * (width * 0.5f + 1.5f), 1.9f + tall * 0.5f, 0f), new Vector3(width, tall, depth));
            yards[settlement.Id] = new ShipyardVisual(root.transform, materials, settlement, toward);
            return root;
        }

        /// <summary>Points a crew walks between at a port, in world space: jetty end, foot of the jetty, store door.</summary>
        public static void PortPlaces(Settlement settlement, out Vector3 jettyEnd, out Vector3 landing, out Vector3 store)
        {
            var landX = (float)Rules.SettlementLandX(settlement);
            var toward = Mathf.Sign((float)settlement.X - landX);
            var y = (float)settlement.Y;
            jettyEnd = new Vector3(landX + toward * (JettyReach - 0.6f), 1.4f, y);
            landing = new Vector3(landX + toward * 15.5f, 1.4f, y);
            store = new Vector3(landX + toward * 12.5f, ShoreShape.MoundHeight(12.5f / (float)Rules.SettlementLandRadius) + 0.1f, y + 3f);
        }

        /// <summary>How far from the land's centre the port jetty reaches, close to a moored hull.</summary>
        const float JettyReach = 23.5f;

        /// <summary>The islet exactly matches the land's collision circle. A free shore shows only this.</summary>
        GameObject CreateShore(double portX, double portY, string name)
        {
            var landX = (float)Rules.LandX(portX);
            var root = new GameObject(name);
            root.transform.position = new Vector3(landX, 0f, (float)portY);

            // The same gentle mound with a beach as every islet, on the land's exact circle.
            var islet = new GameObject("Islet");
            islet.transform.SetParent(root.transform, false);
            islet.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Mound(ShoreShape.Ellipse((float)Rules.SettlementLandRadius, (float)Rules.SettlementLandRadius));
            islet.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials?.Sand, materials?.Foliage };

            // The jetty runs from the beach toward the port, ending beside a moored hull.
            var towardPort = Mathf.Sign((float)portX - landX);
            const float Start = 14f;
            AddViewPart(root, PrimitiveType.Cube, "Jetty", materials?.Timber, new Vector3(towardPort * (Start + JettyReach) * 0.5f, 1.2f, 0f), new Vector3(JettyReach - Start, 0.35f, 4f));
            for (var side = -1; side <= 1; side += 2)
            {
                for (var post = 0; post < 3; post++)
                {
                    var along = Mathf.Lerp(Start + 2f, JettyReach - 0.5f, post / 2f);
                    AddViewPart(root, PrimitiveType.Cylinder, "Jetty post", materials?.Timber, new Vector3(towardPort * along, 0f, side * 1.9f), new Vector3(0.4f, 1.6f, 0.4f));
                }
            }
            return root;
        }

        static void AddViewPart(GameObject root, PrimitiveType type, string name, Material material, Vector3 position, Vector3 scale)
        {
            var part = Primitive(type, name, material);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
        }

        /// <summary>The waterline of each land, as the radius around its centre at evenly spaced angles.</summary>
        float[] OutlineOf(Island island)
        {
            // The same waterline the simulation steers and collides against, so what looks like shore is shore.
            if (island.Outline == null) return ShoreShape.Ellipse((float)island.Radius, (float)island.Radius);
            return island.Outline.Select(radius => (float)radius).ToArray();
        }

        GameObject CreateIsland(Island island)
        {
            var center = new Vector3((float)island.X, 0f, (float)island.Y);
            if (coastPrefab == null)
            {
                var fallback = Primitive(PrimitiveType.Cylinder, "Island [PLACEHOLDER]", materials?.Neutral);
                fallback.transform.position = center;
                fallback.transform.localScale = new Vector3((float)island.Radius * 2f, 6f, (float)island.Radius * 2f);
                return fallback;
            }

            var root = new GameObject("Island");
            root.transform.position = center;
            var coast = Instantiate(coastPrefab, root.transform);
            coast.name = "Coast_Rocks_02";
            var bounds = WorldBounds(coast);

            // The land's radius reaches its farthest point; the model was measured against half its footprint.
            var footprintHalf = (float)(island.Radius / LandShape.MaxRadius(MapFactory.CoastOutline));
            var scale = footprintHalf * 2f / Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            coast.transform.localScale *= scale;
            bounds = WorldBounds(coast);
            coast.transform.position += center - new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.15f, bounds.center.z);
            coastScaleNote = $"unscaled footprint scale factor {scale:0.##}, placed size {bounds.size.x:0.#} x {bounds.size.y:0.#} x {bounds.size.z:0.#} m";

            foreach (var blocker in coast.GetComponentsInChildren<Collider>()) blocker.enabled = false;

            // Coasts on the second side are turned half a turn, like their waterline in the simulation.
            root.transform.rotation = Quaternion.Euler(0f, (float)island.Yaw, 0f);
            return root;
        }

        static Bounds WorldBounds(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(item.transform.position, Vector3.one);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static GameObject Primitive(PrimitiveType type, string name, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            Destroy(item.GetComponent<Collider>());
            if (material != null) item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        void SyncViews()
        {
            foreach (var ship in World.Ships)
            {
                if (!views.TryGetValue(ship.Id, out var view))
                {
                    view = ShipVisual.Create(ship, materials);
                    views[ship.Id] = view;
                    viewOwners[ship.Id] = ship.Owner;
                }
                else if (viewOwners[ship.Id] != ship.Owner)
                {
                    view.ApplyOwner(ship.Owner, materials);
                    viewOwners[ship.Id] = ship.Owner;
                    if (ship.Owner != PlayerFaction) selected.Remove(ship.Id);
                }
            }

            foreach (var id in views.Keys.Where(id => World.FindShip(id) == null).ToList())
            {
                if (sinkingShips.Remove(id)) effects.Sink(views[id].transform);
                else Destroy(views[id].gameObject);
                views.Remove(id);
                lastPositions.Remove(id);
                viewOwners.Remove(id);
                previous.Remove(id);
                selected.Remove(id);
            }
        }

        void SyncWrecks()
        {
            foreach (var wreck in World.Wrecks)
            {
                if (wreckViews.ContainsKey(wreck.Id)) continue;
                var view = Primitive(PrimitiveType.Cube, $"Wreck {wreck.Id} [PLACEHOLDER]", materials?.WreckMarker);
                view.transform.position = new Vector3((float)wreck.X, 0.4f, (float)wreck.Y);
                view.transform.localScale = new Vector3(9f, 0.8f, 4f);
                view.transform.rotation = Quaternion.Euler(0f, wreck.Id * 37f, 8f);
                wreckViews[wreck.Id] = view;
            }

            foreach (var id in wreckViews.Keys.Where(id => World.Wrecks.All(wreck => wreck.Id != id)).ToList())
            {
                Destroy(wreckViews[id]);
                wreckViews.Remove(id);
            }

            foreach (var wreck in World.Wrecks)
            {
                var view = wreckViews[wreck.Id];
                var shown = World.CanSee(PlayerFaction, wreck.X, wreck.Y);
                if (view.activeSelf != shown) view.SetActive(shown);
            }
        }

        /// <summary>Where a ship is heading, for the order line. Hidden enemy targets are never revealed.</summary>
        bool TryGetOrderTarget(Ship ship, out Vector3 target)
        {
            target = default;
            switch (ship.Order)
            {
                case OrderKind.Move:
                    target = new Vector3((float)ship.TargetX, 0f, (float)ship.TargetY);
                    return true;
                case OrderKind.AttackShip:
                case OrderKind.Ram:
                case OrderKind.Board:
                    var enemy = World.FindShip(ship.TargetId);
                    if (enemy == null || !World.CanSee(PlayerFaction, enemy)) return false;
                    target = ToWorld(enemy);
                    return true;
                case OrderKind.AttackSettlement:
                    var settlement = World.Settlements.FirstOrDefault(item => item.Id == ship.TargetId);
                    if (settlement == null) return false;
                    target = new Vector3((float)settlement.X, 0f, (float)settlement.Y);
                    return true;
                case OrderKind.Harvest:
                    if (ship.Harvest == HarvestPhase.ToPort || ship.Harvest == HarvestPhase.WaitingPort || ship.Harvest == HarvestPhase.Docked)
                    {
                        var home = World.ClosestPort(ship);
                        if (home == null) return false;
                        target = new Vector3((float)home.X, 0f, (float)home.Y);
                        return true;
                    }
                    var node = World.FindNode(ship.NodeId);
                    if (node == null) return false;
                    target = new Vector3((float)node.X, 0f, (float)node.Y);
                    return true;
                case OrderKind.Salvage:
                    var wreck = World.Wrecks.FirstOrDefault(item => item.Id == ship.WreckId);
                    if (wreck == null) return false;
                    target = new Vector3((float)wreck.X, 0f, (float)wreck.Y);
                    return true;
                case OrderKind.Repair:
                case OrderKind.Scrap:
                    var yard = World.ClosestYard(ship);
                    if (yard == null) return false;
                    target = new Vector3((float)yard.X, 0f, (float)yard.Y);
                    return true;
                case OrderKind.Transfer:
                case OrderKind.Expedition:
                    if (ship.Transport <= TransportPhase.Loading)
                    {
                        var source = World.FindSettlement(ship.SourceId);
                        if (source == null) return false;
                        target = new Vector3((float)source.X, 0f, (float)source.Y);
                        return true;
                    }
                    if (ship.Order == OrderKind.Expedition)
                    {
                        var shore = World.FindSite(ship.SiteId);
                        if (shore == null) return false;
                        target = new Vector3((float)shore.X, 0f, (float)shore.Y);
                        return true;
                    }
                    var destination = World.FindSettlement(ship.DestinationId);
                    if (destination == null) return false;
                    target = new Vector3((float)destination.X, 0f, (float)destination.Y);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A modest ring where selected ships are heading. Enemy targets get their own red marker instead.</summary>
        void UpdateDestinationMarkers()
        {
            foreach (var id in destinationMarkers.Keys.ToList())
            {
                var ship = World.FindShip(id);
                var hasMarker = ship != null && selected.Contains(id) && ship.Order != OrderKind.AttackShip && ship.Order != OrderKind.Ram
                                && ship.Order != OrderKind.Board && ship.Order != OrderKind.AttackSettlement && TryGetOrderTarget(ship, out _);
                destinationMarkers[id].gameObject.SetActive(hasMarker);
            }

            foreach (var id in selected)
            {
                var ship = World.FindShip(id);
                if (ship == null || ship.Order == OrderKind.AttackShip || ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board
                    || ship.Order == OrderKind.AttackSettlement || !TryGetOrderTarget(ship, out var target)) continue;
                if (!destinationMarkers.TryGetValue(id, out var marker))
                {
                    marker = RingMarker.Create("Destination", materials?.OrderLine, null, 4f);
                    destinationMarkers[id] = marker;
                }
                marker.gameObject.SetActive(true);
                marker.position = target + Vector3.up * 0.5f;
            }
        }

        /// <summary>Marks what the player's ships were ordered to attack, as long as the player can see it.</summary>
        void UpdateTargetMarkers()
        {
            targetedShips.Clear();
            Settlement targetedSettlement = null;
            foreach (var ship in World.Ships)
            {
                if (ship.Owner != PlayerFaction) continue;
                if (ship.Order == OrderKind.AttackShip || ship.Order == OrderKind.Ram || ship.Order == OrderKind.Board)
                {
                    var target = World.FindShip(ship.TargetId);
                    if (target != null && target.Owner != PlayerFaction && World.CanSee(PlayerFaction, target)) targetedShips.Add(target.Id);
                }
                else if (ship.Order == OrderKind.AttackSettlement)
                {
                    targetedSettlement = World.Settlements.FirstOrDefault(item => item.Id == ship.TargetId && !item.Destroyed);
                }
            }

            foreach (var pair in views) pair.Value.SetTargetMarker(targetedShips.Contains(pair.Key));

            if (targetedSettlement == null)
            {
                if (settlementTargetMarker != null) settlementTargetMarker.SetActive(false);
                return;
            }
            if (settlementTargetMarker == null)
            {
                settlementTargetMarker = RingMarker.Create("Settlement attack target", materials?.TargetRing, null, 24f).gameObject;
            }
            settlementTargetMarker.SetActive(true);
            var pulse = 26f + Mathf.Sin(Time.unscaledTime * 5f) * 1.5f;
            settlementTargetMarker.transform.position = new Vector3((float)Rules.SettlementLandX(targetedSettlement), 7f, (float)targetedSettlement.Y);
            settlementTargetMarker.transform.localScale = new Vector3(pulse, 1f, pulse);
        }

        void PresentFrame()
        {
            var blend = (float)(accumulator / Rules.Dt);
            var delta = Time.unscaledDeltaTime;
            effects.Update(paused ? 0f : delta * Speed);
            SyncWrecks();
            UpdateDestinationMarkers();
            UpdateTargetMarkers();
            fogTimer += delta;
            if (fogTimer >= 0.1f)
            {
                fog.Update(World, PlayerFaction, fogTimer);
                fogTimer = 0f;
            }
            inspectedEnemies.RemoveWhere(id =>
            {
                var inspected = World.FindShip(id);
                return inspected == null || inspected.Owner == PlayerFaction || !World.CanSee(PlayerFaction, inspected);
            });
            UpdateWaterPoses();
            UpdateCrews(paused ? 0f : delta * Speed);

            // Surf rises and falls as sets of waves reach each shore, never all shores at once.
            var now = Time.unscaledTime;
            foreach (var patch in surf)
            {
                if (patch.Decal == null) continue;

                // Surf would reveal small land under the fog, so it shows only where the player has explored.
                var holder = patch.Decal.transform.parent.gameObject;
                var shown = patch.Island.Kind == IslandKind.Coast || World.IsExplored(PlayerFaction, patch.Island.X, patch.Island.Y);
                if (holder.activeSelf != shown) holder.SetActive(shown);
                if (!shown) continue;

                // Exposed shores take the full sets; sheltered ones only wash. The water never fully calms at the line.
                var set = Mathf.Sin(now * 0.9f + patch.Phase);
                var wave = 0.5f + 0.5f * set;
                var reach = 0.25f + 0.75f * patch.Exposure;
                patch.Decal.surfaceFoamDimmer = patch.Strength * (0.3f + 0.7f * wave) * reach;
                patch.Decal.deepFoamDimmer = patch.Decal.surfaceFoamDimmer * 0.7f;

                // As a set crests on an exposed rock face, it bursts upward and falls back.
                if (patch.Spray != null && patch.Exposure > 0.35f && patch.LastSet < 0.85f && set >= 0.85f) BreakOnRock(patch);
                patch.LastSet = set;
            }

            foreach (var ship in World.Ships)
            {
                if (!views.TryGetValue(ship.Id, out var view)) continue;

                // Hidden ships are fully inactive, so they cast no shadow or reflection and reveal nothing.
                var visible = World.CanSee(PlayerFaction, ship);
                if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
                if (!visible) continue;

                var current = ToWorld(ship);
                var gamePosition = previous.TryGetValue(ship.Id, out var last) ? Vector3.Lerp(last, current, blend) : current;
                var gameRotation = Quaternion.Euler(0f, 90f - (float)(ship.Heading * Mathf.Rad2Deg), 0f);
                if (!PlaceLaunch(ship, view, gamePosition, gameRotation, paused ? 0f : delta * Speed)) view.transform.SetPositionAndRotation(gamePosition, gameRotation);
                UpdateOarRoom(ship, view);
                view.Selection.SetActive(selected.Contains(ship.Id) || inspectedEnemies.Contains(ship.Id));
                var capacity = ship.Definition.Capacity * Rules.Centi;
                view.SetLoad(capacity > 0 ? (float)(ship.Cargo + ship.HoldWood + ship.HoldSilver) / capacity : 0f);
                view.Animate((float)(ship.Speed / ship.Definition.Speed), Time.unscaledTime, paused ? 0f : delta * Speed, delta);
            }

            foreach (var node in World.Nodes)
            {
                if (nodeViews.TryGetValue(node.Id, out var view))
                {
                    var known = (node.KnownMask & (1 << PlayerFaction)) != 0;
                    if (view.activeSelf != known) view.SetActive(known);
                    if (known && siteViews.TryGetValue(node.Id, out var site)) site.UpdateStock(node.Stock);
                }
            }

            foreach (var settlement in World.Settlements)
            {
                if (!settlementViews.TryGetValue(settlement.Id, out var view))
                {
                    // A bridgehead rises during the match.
                    view = CreateSettlementView(settlement);
                    settlementViews[settlement.Id] = view;
                    mapObjects.Add(view);
                }
                var shown = !settlement.Destroyed && (settlement.Owner == PlayerFaction || World.CanSee(PlayerFaction, settlement.X, settlement.Y));
                if (view.activeSelf != shown) view.SetActive(shown);
                if (shown && yards.TryGetValue(settlement.Id, out var yard)) yard.Update(settlement, paused ? 0f : delta * Speed);
            }

            foreach (var shore in World.Sites)
            {
                if (!freeShoreViews.TryGetValue(shore.Id, out var view)) continue;
                var shown = World.IsSiteFree(shore) && World.IsExplored(PlayerFaction, shore.X, shore.Y);
                if (view.activeSelf != shown) view.SetActive(shown);
            }
        }

        void BreakOnRock(SurfPatch patch)
        {
            var at = patch.Decal.transform.position;
            var count = Mathf.RoundToInt(6f + 14f * patch.Exposure);
            for (var i = 0; i < count; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = at - patch.Outward * UnityEngine.Random.Range(0.5f, 2.5f) + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0.3f, UnityEngine.Random.Range(-2.5f, 2.5f)),
                    velocity = Vector3.up * UnityEngine.Random.Range(3f, 7.5f) * patch.Exposure + patch.Outward * UnityEngine.Random.Range(-0.5f, 1.2f)
                               + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)),
                    startSize = UnityEngine.Random.Range(0.9f, 2.2f),
                    startLifetime = UnityEngine.Random.Range(0.8f, 1.6f),
                    applyShapeToPosition = false,
                };
                patch.Spray.Emit(emit, 1);
            }
        }

        /// <summary>Reads the real surface under bow, stern, sides and center of visible ships.</summary>
        void UpdateWaterPoses()
        {
            if (water == null || World.Ships.Count == 0) return;
            var count = Mathf.Min(WaterShipsPerFrame, World.Ships.Count);
            for (var i = 0; i < count; i++)
            {
                waterCursor = (waterCursor + 1) % World.Ships.Count;
                var ship = World.Ships[waterCursor];
                if (!views.TryGetValue(ship.Id, out var view) || !World.CanSee(PlayerFaction, ship)) continue;

                var center = view.transform.position;
                var forward = view.transform.forward * (view.Length * 0.5f);
                var side = view.transform.right * (view.Beam * 0.5f);
                if (!water.TryHeight(center + forward, out var bow) || !water.TryHeight(center - forward, out var stern)
                    || !water.TryHeight(center - side, out var port) || !water.TryHeight(center + side, out var starboard)
                    || !water.TryHeight(center, out var middle)) continue;

                var pose = HullPose.FromHeights(bow, stern, port, starboard, middle, view.Length, view.Beam);
                view.SetWaterPose(pose, bow);
                maxRollSeen = Mathf.Max(maxRollSeen, Mathf.Abs(pose.Roll), Mathf.Abs(pose.Pitch));
                maxHeaveSeen = Mathf.Max(maxHeaveSeen, Mathf.Abs(pose.Heave));
            }
        }

        static Vector3 ToWorld(Ship ship) => new Vector3((float)ship.X, 0f, (float)ship.Y);

        // ---------- Camera (C10) ----------

        void UpdateCamera()
        {
            if (strategicCamera == null) return;
            var delta = Time.unscaledDeltaTime;
            var move = Vector3.zero;
            // Edge scrolling needs a focused game and a cursor inside the screen; the automatic test never scrolls.
            var mouse = Input.mousePosition;
            var edgeScroll = Application.isFocused && autoPlayer == null
                             && mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height;
            if (Input.GetKey(KeyCode.W) || (edgeScroll && mouse.y >= Screen.height - 4)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || (edgeScroll && mouse.y <= 4)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A) || (edgeScroll && mouse.x <= 4)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D) || (edgeScroll && mouse.x >= Screen.width - 4)) move += Vector3.right;
            if (Input.GetKey(KeyCode.Q)) yaw -= 70f * delta;
            if (Input.GetKey(KeyCode.E)) yaw += 70f * delta;
            if (Input.GetKey(KeyCode.PageUp)) tilt += 40f * delta;
            if (Input.GetKey(KeyCode.PageDown)) tilt -= 40f * delta;
            if (Input.GetMouseButton(2))
            {
                yaw += Input.GetAxisRaw("Mouse X") * 3f;
                tilt -= Input.GetAxisRaw("Mouse Y") * 2f;
            }
            if (Input.GetKeyDown(KeyCode.Home))
            {
                yaw = ObliqueCamera.DefaultYaw;
                tilt = ObliqueCamera.DefaultTilt;
            }
            tilt = ObliqueCamera.ClampTilt(tilt);
            height = Mathf.Clamp(height - Input.mouseScrollDelta.y * height * 0.1f, 20f, 300f);

            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var scroll = move.normalized;
            scroll.z *= ObliqueCamera.GroundDepthScale(tilt);
            pivot += rotation * scroll * (height * 0.8f * delta);
            pivot.x = Mathf.Clamp(pivot.x, -520f, 520f);
            pivot.z = Mathf.Clamp(pivot.z, -420f, 420f);

            ObliqueCamera.Apply(strategicCamera, pivot, yaw, tilt, height);
            if (sun != null) sun.transform.rotation = ObliqueCamera.SunRotation(yaw, tilt);
            if (decalAnchor != null) decalAnchor.position = pivot;
        }

        // ---------- Orders ----------

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) speedIndex = Mathf.Min(Speeds.Length - 1, speedIndex + 1);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) speedIndex = Mathf.Max(0, speedIndex - 1);
            if (Input.GetKeyDown(KeyCode.Escape)) ClearSelection();
            if (Input.GetKeyDown(KeyCode.F5)) QuickSave(DefaultSavePath);
            if (Input.GetKeyDown(KeyCode.F8)) QuickLoad(DefaultSavePath);
            HandleGroups();
            if (Input.GetKeyDown(KeyCode.R)) Send(CommandKind.Repair, selected.ToArray());

            if (PointerOverHud()) return;
            if (Input.GetMouseButtonDown(0)) { dragging = true; dragStart = Input.mousePosition; }
            if (Input.GetMouseButtonUp(0) && dragging) { dragging = false; FinishSelection(Input.mousePosition); }
            if (Input.GetMouseButtonDown(1) && TryGround(Input.mousePosition, out var point)) OrderAt(point);
        }

        void HandleGroups()
        {
            for (var key = 1; key <= 9; key++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + key)) continue;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) { groups[key] = selected.ToArray(); continue; }
                if (!groups.TryGetValue(key, out var ids)) continue;
                selected.Clear();
                foreach (var id in ids) { var ship = World.FindShip(id); if (ship != null && ship.Owner == PlayerFaction) selected.Add(id); }
            }
        }

        bool PointerOverHud() => Input.mousePosition.y < HudHeight;

        void FinishSelection(Vector2 end)
        {
            var additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!additive) ClearSelection();
            if (strategicCamera == null) return;

            if ((end - dragStart).magnitude < 6f)
            {
                if (TryGround(end, out var point))
                {
                    var ship = NearestShip(point, item => item.Owner == PlayerFaction);
                    if (ship != null) selected.Add(ship.Id);
                    Inspect(point);
                }
                return;
            }

            var box = Rect.MinMaxRect(Mathf.Min(dragStart.x, end.x), Mathf.Min(dragStart.y, end.y), Mathf.Max(dragStart.x, end.x), Mathf.Max(dragStart.y, end.y));
            foreach (var ship in World.Ships.Where(item => World.CanSee(PlayerFaction, item)))
            {
                var screen = strategicCamera.WorldToScreenPoint(ToWorld(ship));
                if (screen.z <= 0f || !box.Contains(screen)) continue;
                if (ship.Owner == PlayerFaction) selected.Add(ship.Id);
                else inspectedEnemies.Add(ship.Id);
            }
        }

        void ClearSelection()
        {
            if (smokeReport != null) Debug.Log($"[Smoke] selection cleared by input at {smokeElapsed:0.0} s");
            selected.Clear();
            inspectedEnemies.Clear();
            inspectedSettlementId = -1;
        }

        /// <summary>A left click on a visible enemy shows its state. Hidden ships can never be inspected.</summary>
        public void Inspect(Vector3 point)
        {
            var enemy = NearestShip(point, item => item.Owner != PlayerFaction);
            if (enemy != null) { inspectedEnemies.Add(enemy.Id); return; }
            foreach (var settlement in World.Settlements)
            {
                if (settlement.Destroyed || !IsOnSettlement(point, settlement)) continue;
                if (settlement.Owner == PlayerFaction || World.CanSee(PlayerFaction, settlement.X, settlement.Y)) inspectedSettlementId = settlement.Id;
            }
        }

        /// <summary>A click counts on the settlement's model as well as on its port.</summary>
        static bool IsOnSettlement(Vector3 point, Settlement settlement) =>
            World.Distance(point.x, point.z, settlement.X, settlement.Y) < 25
            || World.Distance(point.x, point.z, Rules.SettlementLandX(settlement), settlement.Y) < 25;

        Ship NearestShip(Vector3 point, Func<Ship, bool> filter)
        {
            Ship best = null;
            var bestDistance = float.MaxValue;
            foreach (var ship in World.Ships.Where(filter))
            {
                if (!World.CanSee(PlayerFaction, ship)) continue;
                var distance = Vector3.Distance(ToWorld(ship), point);
                if (distance > ship.Definition.Radius + 8f || distance >= bestDistance) continue;
                bestDistance = distance;
                best = ship;
            }
            return best;
        }

        bool TryGround(Vector2 screen, out Vector3 point)
        {
            point = default;
            if (strategicCamera == null) return false;
            var ray = strategicCamera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance)) return false;
            point = ray.GetPoint(distance);
            return true;
        }

        /// <summary>Context order at a world point, mirroring the right mouse button.</summary>
        public void OrderAt(Vector3 point)
        {
            var ids = selected.ToArray();
            if (ids.Length == 0) return;
            effects.Ping(point);

            var enemy = NearestShip(point, ship => ship.Owner != PlayerFaction);
            if (enemy != null) { OrderAttack(ids, enemy); return; }

            // Warships ordered onto an own merchant stay with it as its escort.
            var warships = ids.Where(id => World.FindShip(id)?.Definition.IsEconomic == false).ToArray();
            var merchants = ids.Where(id => World.FindShip(id)?.Class == ShipClass.Merchant).ToArray();
            var escorted = NearestShip(point, ship => ship.Owner == PlayerFaction && ship.Class == ShipClass.Merchant && !ids.Contains(ship.Id));
            if (escorted != null && warships.Length > 0)
            {
                Send(CommandKind.Escort, warships, target: escorted.Id);
                return;
            }

            var enemyPort = World.Settlements.FirstOrDefault(item => item.Owner != PlayerFaction && !item.Destroyed
                && World.CanSee(PlayerFaction, item.X, item.Y) && IsOnSettlement(point, item));
            if (enemyPort != null)
            {
                Send(CommandKind.AttackSettlement, ids, target: enemyPort.Id);
                return;
            }

            if (merchants.Length > 0)
            {
                var shore = World.Sites.FirstOrDefault(item => World.IsSiteFree(item) && World.IsExplored(PlayerFaction, item.X, item.Y)
                    && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, Rules.LandX(item.X), item.Y) < 25));
                if (shore != null)
                {
                    Send(CommandKind.FoundOutpost, merchants.Take(1).ToArray(), target: shore.Id);
                    Send(CommandKind.Move, ids.Except(merchants.Take(1)).ToArray(), point);
                    return;
                }

                var ownPort = World.PortsOf(PlayerFaction).FirstOrDefault(item => IsOnSettlement(point, item));
                if (ownPort != null)
                {
                    Send(CommandKind.Transfer, merchants, target: ownPort.Id);
                    Send(CommandKind.Move, ids.Except(merchants).ToArray(), point);
                    return;
                }
            }

            var wreck = World.Wrecks.FirstOrDefault(item => World.CanSee(PlayerFaction, item.X, item.Y) && World.Distance(point.x, point.z, item.X, item.Y) < 20);
            if (wreck != null)
            {
                var salvagers = ids.Where(id => World.FindShip(id)?.Definition.CanHarvest == true).ToArray();
                if (salvagers.Length > 0) { Send(CommandKind.Salvage, salvagers, target: wreck.Id); return; }
            }

            var node = World.Nodes.FirstOrDefault(item => (item.KnownMask & (1 << PlayerFaction)) != 0
                && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, item.X, item.Y + Rules.SiteLandOffset) < Rules.SiteLandRadius));
            if (node != null)
            {
                var cutters = ids.Where(id => World.FindShip(id)?.Definition.CanHarvest == true).ToArray();
                if (cutters.Length > 0)
                {
                    Send(CommandKind.Harvest, cutters, target: node.Id);
                    Send(CommandKind.Move, ids.Except(cutters).ToArray(), point);
                    return;
                }
            }
            Send(CommandKind.Move, ids, point);
        }

        void OrderAttack(int[] ids, Ship enemy)
        {
            foreach (var id in ids)
            {
                var ship = World.FindShip(id);
                if (ship == null) continue;
                var kind = ship.Class == ShipClass.Ram ? CommandKind.Ram
                    : ship.Class == ShipClass.Boarder && Input.GetKey(KeyCode.LeftAlt) == false ? CommandKind.Board
                    : ship.Definition.LightSlots > 0 ? CommandKind.AttackShip
                    : CommandKind.Move;
                if (kind == CommandKind.Move) Send(kind, new[] { id }, ToWorld(enemy));
                else Send(kind, new[] { id }, target: enemy.Id);
            }
        }

        void Send(CommandKind kind, int[] ids, Vector3 point = default, int target = -1)
        {
            if (ids.Length == 0) return;
            World.Issue(new Command { Faction = PlayerFaction, Kind = kind, ShipIds = ids, X = point.x, Y = point.z, TargetId = target });
        }

        public void SelectForTest(int id) => selected.Add(id);

        // ---------- Save ----------

        public void QuickSave(string path)
        {
            SaveGame.WriteFile(path, SaveGame.Serialize(World, opponent.BuildIndex));
            Notify("Zapisano grę.");
        }

        public bool QuickLoad(string path)
        {
            try
            {
                var world = SaveGame.Deserialize(File.ReadAllBytes(path), out var build);
                StartMatch(world, build);
                Notify("Wczytano grę.");
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is EndOfStreamException || exception is UnauthorizedAccessException)
            {
                Notify("Nie można wczytać zapisu: " + exception.Message);
                return false;
            }
        }

        void Notify(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 5f;
        }

        // ---------- Working M1 HUD ----------

        void OnGUI()
        {
            // E03: the bar shows one local store, never a remote total. Clicking an own port makes it active.
            var settlement = ActivePort;
            if (settlement != null)
            {
                var own = World.Ships.Where(ship => ship.Owner == PlayerFaction).ToList();
                var woodTransit = (own.Where(ship => ship.CargoType == ResourceType.Wood).Sum(ship => ship.Cargo) + own.Sum(ship => ship.HoldWood)) / Rules.Centi;
                var silverTransit = (own.Where(ship => ship.CargoType == ResourceType.Silver).Sum(ship => ship.Cargo) + own.Sum(ship => ship.HoldSilver)) / Rules.Centi;
                var portName = settlement.IsOutpost ? "Przyczółek" : "Osada";
                GUI.Box(new Rect(0, 0, Screen.width, 26), string.Empty);
                GUI.Label(new Rect(10, 3, Screen.width - 20, 24),
                    $"{portName}: drewno {settlement.Available(ResourceType.Wood) / Rules.Centi} (rez. {settlement.ReservedWood / Rules.Centi})   srebro {settlement.Available(ResourceType.Silver) / Rules.Centi} (rez. {settlement.ReservedSilver / Rules.Centi})   {Mathf.CeilToInt((float)settlement.Hull)} HP   " +
                    $"W drodze: {woodTransit} D, {silverTransit} S   Porty {World.PortsOf(PlayerFaction).Count()}   Flota {own.Count}/{Rules.FleetLimit}   " +
                    $"Prędkość {Speed:0.#}x{(paused ? "  PAUZA" : string.Empty)}   Czas {World.Tick / Rules.TickRate / 60}:{World.Tick / Rules.TickRate % 60:00}");

                GUILayout.BeginArea(new Rect(0, Screen.height - HudHeight, 380, HudHeight), GUI.skin.box);
                GUILayout.Label($"{portName}: cena kompletnego szablonu z lokalnego magazynu.");
                foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
                {
                    var definition = ShipCatalog.Get(shipClass);
                    if (!definition.IsEconomic && !settlement.HasShipyard) continue;
                    var label = $"{ClassName(shipClass)}: {definition.CostWood} D / {definition.CostSilver} S, {definition.BuildSeconds:0} s";
                    if (GUILayout.Button(label)) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.QueueShip, Class = shipClass, TargetId = settlement.Id });
                }
                if (settlement.ShipyardBuild != null)
                {
                    GUILayout.Label($"Stocznia: budowa {100 * settlement.ShipyardBuild.DoneTicks / settlement.ShipyardBuild.TotalTicks}%");
                }
                else if (!settlement.HasShipyard)
                {
                    if (GUILayout.Button($"Stocznia: {Rules.ShipyardWood / Rules.Centi} D / {Rules.ShipyardSilver / Rules.Centi} S, {Rules.ShipyardBuildSeconds:0} s"))
                    {
                        World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.BuildShipyard, TargetId = settlement.Id });
                    }
                }
                GUILayout.EndArea();

                if (settlement.WarQueue.Count + settlement.PortQueue.Count > 0)
                {
                    GUILayout.BeginArea(new Rect(385, Screen.height - HudHeight, 250, HudHeight), GUI.skin.box);
                    GUILayout.Label("Kolejki");
                    QueueButtons(settlement, settlement.WarQueue, false);
                    QueueButtons(settlement, settlement.PortQueue, true);
                    GUILayout.EndArea();
                }
            }

            if (Event.current.type == EventType.Repaint) DrawHealthBars();

            GUILayout.BeginArea(new Rect(640, Screen.height - HudHeight, Mathf.Max(320, Screen.width - 640), HudHeight), GUI.skin.box);
            if (selected.Count == 0 && inspectedEnemies.Count == 0) GUILayout.Label("LPM: zaznacz (także wroga, by zobaczyć jego stan; własny port staje się aktywny). PPM: rozkaz; handlowiec na wolny brzeg: przyczółek, na własny port: dostawa; okręt na handlowca: eskorta. Spacja: pauza. +/-: prędkość. F5/F8: zapis/wczytanie. R: remont. Alt+PPM: ostrzał abordażowcem.");
            foreach (var id in selected.Take(5))
            {
                var ship = World.FindShip(id);
                if (ship == null) continue;
                GUILayout.Label($"{ClassName(ship.Class)}: kadłub {Mathf.CeilToInt((float)ship.Hull)}/{ship.Definition.Hull}, marynarze {ship.Sailors}, żołnierze {ship.Soldiers}, morale {Mathf.RoundToInt((float)ship.Morale)}, ładunek {DescribeCargo(ship)}. {DescribeOrder(ship)}");
            }
            if (selected.Count > 5) GUILayout.Label($"oraz {selected.Count - 5} kolejnych");
            foreach (var id in inspectedEnemies.Take(3))
            {
                var enemy = World.FindShip(id);
                if (enemy == null) continue;
                GUILayout.Label($"WRÓG {ClassName(enemy.Class)}: kadłub {Mathf.CeilToInt((float)enemy.Hull)}/{enemy.Definition.Hull}, marynarze {enemy.Sailors}, żołnierze {enemy.Soldiers}, morale {Mathf.RoundToInt((float)enemy.Morale)}");
            }
            if (inspectedEnemies.Count > 3) GUILayout.Label($"oraz {inspectedEnemies.Count - 3} wrogów");
            GUILayout.EndArea();

            if (Time.unscaledTime < noticeUntil) GUI.Label(new Rect(10, 30, 700, 24), notice);
            if (dragging && (Input.mousePosition - (Vector3)dragStart).magnitude >= 6f)
            {
                var end = Input.mousePosition;
                GUI.Box(Rect.MinMaxRect(Mathf.Min(dragStart.x, end.x), Screen.height - Mathf.Max(dragStart.y, end.y), Mathf.Max(dragStart.x, end.x), Screen.height - Mathf.Min(dragStart.y, end.y)), string.Empty);
            }

            if (World.Outcome != OutcomeKind.InProgress)
            {
                var text = World.Outcome == OutcomeKind.Draw ? "REMIS" : World.Winner == PlayerFaction ? "ZWYCIĘSTWO" : "PORAŻKA";
                GUI.Box(new Rect(Screen.width / 2f - 150, Screen.height / 2f - 40, 300, 80), text);
                if (GUI.Button(new Rect(Screen.width / 2f - 80, Screen.height / 2f, 160, 28), "Nowa gra")) StartMatch(MapFactory.CreateSkirmish(seed + (ulong)Time.frameCount), 0);
            }
        }

        void DrawHealthBars()
        {
            if (strategicCamera == null) return;
            var previousColor = GUI.color;
            foreach (var ship in World.Ships)
            {
                if (!ShowsHealthBar(ship)) continue;
                var position = views.TryGetValue(ship.Id, out var view) ? view.transform.position : ToWorld(ship);
                var screen = strategicCamera.WorldToScreenPoint(position + Vector3.up * ((float)ship.Definition.Radius + 3f));
                if (screen.z <= 0f) continue;
                var width = Mathf.Clamp(18f + (float)ship.Definition.Radius * 240f / Mathf.Max(height, 20f), 18f, 90f);
                var own = ship.Owner == PlayerFaction;
                var crewMax = ship.Definition.MaxCrew;
                DrawBar(screen, width, 5f, (float)(ship.Hull / ship.Definition.Hull), own ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.15f), 0f);
                DrawBar(screen, width, 3f, (float)(ship.Sailors + ship.Soldiers) / crewMax, new Color(0.95f, 0.8f, 0.3f), 6f);
            }

            foreach (var settlement in World.Settlements)
            {
                if (settlement.Destroyed || settlement.Id != inspectedSettlementId) continue;
                if (settlement.Owner != PlayerFaction && !World.CanSee(PlayerFaction, settlement.X, settlement.Y)) continue;
                var screen = strategicCamera.WorldToScreenPoint(new Vector3((float)Rules.SettlementLandX(settlement), 12f, (float)settlement.Y));
                if (screen.z <= 0f) continue;
                DrawBar(screen, 110f, 7f, (float)(settlement.Hull / settlement.MaxHull), settlement.Owner == PlayerFaction ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.15f), 0f);
            }
            GUI.color = previousColor;
        }

        static void DrawBar(Vector3 screen, float width, float barHeight, float fraction, Color color, float offset)
        {
            var x = screen.x - width * 0.5f;
            var y = Screen.height - screen.y + offset;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, barHeight + 2f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(fraction), barHeight), Texture2D.whiteTexture);
        }

        /// <summary>Plain language order text for the selection panel.</summary>
        public string DescribeOrder(Ship ship)
        {
            string Enemy()
            {
                var target = World.FindShip(ship.TargetId);
                return target != null && World.CanSee(PlayerFaction, target) ? ClassName(target.Class) : "cel poza zasięgiem";
            }

            switch (ship.Order)
            {
                case OrderKind.Move: return "Rozkaz: płynie do wskazanego punktu.";
                case OrderKind.AttackShip: return $"Rozkaz: ostrzał, {Enemy()}.";
                case OrderKind.AttackSettlement: return "Rozkaz: ostrzał osady wroga.";
                case OrderKind.Ram:
                    return ship.RamState == RamPhase.Approach ? $"Rozkaz: szarża, {Enemy()}." : $"Rozkaz: taran, odskok i przygotowanie, {Enemy()}.";
                case OrderKind.Board: return $"Rozkaz: abordaż, {Enemy()}.";
                case OrderKind.Transfer:
                case OrderKind.Expedition:
                    var goal = ship.Order == OrderKind.Expedition ? "przyczółek" : "dostawa";
                    switch (ship.Transport)
                    {
                        case TransportPhase.ToSource: return $"Rozkaz: {goal}, płynie po ładunek.";
                        case TransportPhase.WaitSourceBerth: return $"Rozkaz: {goal}, czeka na miejsce w porcie.";
                        case TransportPhase.Loading: return $"Rozkaz: {goal}, załadunek.";
                        case TransportPhase.ToDestination: return ship.Order == OrderKind.Expedition ? "Rozkaz: przyczółek, płynie na brzeg." : "Rozkaz: dostawa, płynie do celu.";
                        case TransportPhase.WaitDestinationBerth: return "Rozkaz: dostawa, czeka na miejsce w porcie.";
                        case TransportPhase.Unloading: return "Rozkaz: dostawa, rozładunek.";
                        default: return $"Rozkaz: buduje przyczółek, {100 * ship.BuildTicks / (int)(Rules.OutpostBuildSeconds * Rules.TickRate)}%.";
                    }
                case OrderKind.Escort: return "Rozkaz: eskorta handlowca.";
                case OrderKind.Salvage: return "Rozkaz: zbiera wrak.";
                case OrderKind.Repair: return "Rozkaz: remont w stoczni.";
                case OrderKind.Scrap: return "Rozkaz: złomowanie.";
                case OrderKind.Harvest:
                    var resource = World.FindNode(ship.NodeId)?.Type == ResourceType.Silver ? "srebro" : "drewno";
                    switch (ship.Harvest)
                    {
                        case HarvestPhase.ToNode: return $"Rozkaz: płynie po {resource}.";
                        case HarvestPhase.WaitingBerth: return $"Rozkaz: {resource}, czeka na stanowisko.";
                        case HarvestPhase.Landing: return $"Rozkaz: {resource}, załoga schodzi na ląd.";
                        case HarvestPhase.Working: return $"Rozkaz: ładuje {resource}.";
                        case HarvestPhase.Embarking: return $"Rozkaz: {resource}, załoga wraca na pokład.";
                        case HarvestPhase.ToPort: return "Rozkaz: wiezie ładunek do portu.";
                        case HarvestPhase.WaitingPort: return "Rozkaz: czeka na miejsce w porcie.";
                        default: return "Rozkaz: rozładunek w porcie.";
                    }
                default: return "Brak rozkazu.";
            }
        }

        /// <summary>The own port clicked last, or the main settlement.</summary>
        public Settlement ActivePort
        {
            get
            {
                var inspected = World.FindSettlement(inspectedSettlementId);
                return inspected != null && inspected.Owner == PlayerFaction && !inspected.Destroyed ? inspected : World.SettlementOf(PlayerFaction);
            }
        }

        static string DescribeCargo(Ship ship)
        {
            if (ship.HoldWood + ship.HoldSilver > 0) return $"{ship.HoldWood / Rules.Centi} D i {ship.HoldSilver / Rules.Centi} S";
            return (ship.Cargo / Rules.Centi).ToString();
        }

        void QueueButtons(Settlement port, List<BuildOrder> queue, bool portQueue)
        {
            for (var i = 0; i < queue.Count; i++)
            {
                var order = queue[i];
                if (GUILayout.Button($"{ClassName(order.Class)} {100 * order.DoneTicks / order.TotalTicks}% (anuluj)"))
                    World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.CancelQueue, QueueIndex = i, PortQueue = portQueue, TargetId = port.Id });
            }
        }

        static string ClassName(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "Kuter";
                case ShipClass.Merchant: return "Handlowiec";
                case ShipClass.Ram: return "Taranowiec";
                case ShipClass.CrewDestroyer: return "Niszczyciel załóg";
                default: return "Abordażowiec";
            }
        }

        // ---------- Build smoke test ----------

        void UpdateSmoke()
        {
            smokeElapsed += Time.unscaledDeltaTime;
            if (firstEnemySeenSeconds < 0 && World.Ships.Any(ship => ship.Owner != PlayerFaction && World.CanSee(PlayerFaction, ship))) firstEnemySeenSeconds = World.Tick / (float)Rules.TickRate;
            smokeMaxVisibleEnemies = Mathf.Max(smokeMaxVisibleEnemies, World.Ships.Count(ship => ship.Owner != PlayerFaction && World.CanSee(PlayerFaction, ship)));
            UpdateSmokeShots();
            if (Mathf.FloorToInt(smokeElapsed / 30f) != Mathf.FloorToInt((smokeElapsed - Time.unscaledDeltaTime) / 30f))
                Debug.Log($"[Smoke] {smokeElapsed:0} s, tick {World.Tick}, ships {World.Ships.Count}, combat {combatEvents}, focus {Application.isFocused}");
            if (smokeElapsed < smokeSeconds && World.Outcome == OutcomeKind.InProgress) return;

            WriteSmokeReport(false);
            Application.Quit(0);
        }

        /// <summary>Test screenshots: a resource site up close, the whole map, then the fleet with bars and markers.</summary>
        void UpdateSmokeShots()
        {
            if (smokeShotPrefix == null || smokeShots >= 5) return;
            if (smokeShotAt > 0f)
            {
                if (smokeElapsed < smokeShotAt) return;
                smokeShots++;
                smokeShotAt = 0f;
                ScreenCapture.CaptureScreenshot($"{smokeShotPrefix}-{smokeShots}.png");
                Debug.Log($"[Smoke] shot {smokeShots}, selected {selected.Count}, player ships {World.Ships.Count(ship => ship.Owner == PlayerFaction)}");
                return;
            }
            if (smokeElapsed < smokeSeconds * (smokeShots + 1) / 6f) return;

            var home = World.SettlementOf(PlayerFaction);
            switch (smokeShots)
            {
                case 0:
                    var wood = World.Nodes.First(node => node.Type == ResourceType.Wood && (node.KnownMask & (1 << PlayerFaction)) != 0);
                    pivot = new Vector3((float)wood.X, 0f, (float)wood.Y + 22f);
                    height = 55f;
                    break;
                case 1:
                    // The fastest own ship up close, steep, so its bow wave and foam are visible. The pivot is set a
                    // little ahead, where the ship will be when the picture is taken.
                    var mover = World.Ships.Where(ship => ship.Owner == PlayerFaction).OrderByDescending(ship => ship.Speed).ThenBy(ship => ship.Id).First();
                    pivot = ToWorld(mover) + new Vector3(Mathf.Cos((float)mover.Heading), 0f, Mathf.Sin((float)mover.Heading)) * (float)(mover.Speed * 1.5 * Speed);
                    height = 40f;
                    yaw = -40f;
                    tilt = ObliqueCamera.MaxTilt;
                    break;
                case 2:
                    // A bridgehead when one exists, otherwise the home port, with docking ships next to the land.
                    var port = World.PortsOf(PlayerFaction).FirstOrDefault(item => item.IsOutpost) ?? home;
                    pivot = new Vector3((float)port.X, 0f, (float)port.Y);
                    height = 120f;
                    yaw = ObliqueCamera.DefaultYaw;
                    tilt = ObliqueCamera.DefaultTilt;
                    break;
                case 4:
                    // Late in the run: a bridgehead, made the active port so the panels show its local store.
                    var outpost = World.PortsOf(PlayerFaction).FirstOrDefault(item => item.IsOutpost) ?? home;
                    pivot = new Vector3((float)outpost.X, 0f, (float)outpost.Y);
                    height = 90f;
                    selected.Clear();
                    inspectedSettlementId = outpost.Id;
                    break;
                default:
                    var focus = World.Ships.FirstOrDefault(ship => ship.Owner == PlayerFaction && ship.Order == OrderKind.Ram)
                                ?? World.Ships.FirstOrDefault(ship => ship.Owner == PlayerFaction && ship.Definition.LightSlots > 0)
                                ?? World.Ships.First(ship => ship.Owner == PlayerFaction);
                    pivot = ToWorld(focus);
                    height = 90f;
                    selected.Clear();
                    foreach (var ship in World.Ships.Where(item => item.Owner == PlayerFaction)) selected.Add(ship.Id);
                    break;
            }
            smokeShotAt = smokeElapsed + 1.5f;
        }

        /// <summary>An outside close still leaves a report, clearly marked as not completed.</summary>
        void OnApplicationQuit()
        {
            if (smokeReport == null) return;
            Debug.Log($"[Smoke] Closed from outside after {smokeElapsed:0} s, focus {Application.isFocused}");
            WriteSmokeReport(true);
        }

        void WriteSmokeReport(bool interrupted)
        {
            var report = new StringBuilder("{\n");
            report.Append($"  \"interrupted\": {(interrupted ? "true" : "false")},\n  \"elapsedSeconds\": {Mathf.RoundToInt(smokeElapsed)},\n  \"fullScreenMode\": \"{Screen.fullScreenMode}\",\n");
            report.Append($"  \"tick\": {World.Tick},\n  \"outcome\": \"{World.Outcome}\",\n  \"winner\": {World.Winner},\n");
            report.Append($"  \"ships\": {World.Ships.Count},\n  \"views\": {views.Count},\n  \"maxVisibleEnemies\": {smokeMaxVisibleEnemies},\n");
            report.Append($"  \"coastPrefabLoaded\": {(coastPrefab != null ? "true" : "false")},\n  \"coast\": \"{coastScaleNote}\",\n");
            report.Append($"  \"profile\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\n  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            report.Append($"  \"wood\": [{World.Settlements[0].Wood / Rules.Centi}, {World.Settlements[1].Wood / Rules.Centi}],\n");
            report.Append($"  \"silver\": [{World.Settlements[0].Silver / Rules.Centi}, {World.Settlements[1].Silver / Rules.Centi}],\n");
            report.Append($"  \"firstEnemySeenSeconds\": {firstEnemySeenSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},\n");
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            report.Append($"  \"outposts\": {World.Settlements.Count(item => item.IsOutpost && !item.Destroyed)},\n  \"outpostShipyards\": {World.Settlements.Count(item => item.IsOutpost && !item.Destroyed && item.HasShipyard)},\n  \"merchants\": {World.Ships.Count(item => item.Class == ShipClass.Merchant)},\n");
            report.Append($"  \"waterSamples\": {(water != null ? water.Samples : 0)},\n  \"waterFailures\": {(water != null ? water.Failures : 0)},\n");
            report.Append($"  \"maxTiltDegrees\": {maxRollSeen.ToString("0.00", culture)},\n  \"maxHeaveMetres\": {maxHeaveSeen.ToString("0.00", culture)},\n");
            report.Append($"  \"combatEvents\": {combatEvents}\n}}\n");
            File.WriteAllText(smokeReport, report.ToString());
            smokeReport = null;
        }
    }
}
