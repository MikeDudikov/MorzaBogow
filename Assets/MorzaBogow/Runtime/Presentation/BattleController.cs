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
    public sealed partial class BattleController : MonoBehaviour
    {
        public const int PlayerFaction = 0;
        const int MaxStepsPerFrame = 8;
        static readonly float[] Speeds = { 0.5f, 1f, 1.5f, 2f };

        [SerializeField] GameObject coastPrefab;
        [SerializeField] PlaceholderMaterials materials = new PlaceholderMaterials();
        [SerializeField] Camera strategicCamera;
        [SerializeField] ulong seed = 20261109;

        readonly Dictionary<int, ShipVisual> views = new Dictionary<int, ShipVisual>();
        /// <summary>The lasting looks of this match's ships by id; -morzaPlainShips draws every ship in its class's plain look, to measure the difference.</summary>
        ShipLookBook looks = new ShipLookBook();
        readonly bool plainShips = Array.IndexOf(Environment.GetCommandLineArgs(), "-morzaPlainShips") >= 0;
        readonly Dictionary<int, int> viewOwners = new Dictionary<int, int>();
        readonly Dictionary<int, ShipClass> viewClasses = new Dictionary<int, ShipClass>();
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
        WaterBatchSampler water;
        HdrpWaterHeights waterHeights;
        readonly List<ShipVisual> waterHulls = new List<ShipVisual>();
        WeatherController weather;

        // The simulation's wind and sea on screen (docs/MORZE_I_WIATR.md): hull motion and the waves' heights.
        SeakeepingDriver seakeeping;
        SeaSurfaceRenderer seaRenderer;
        UnityEngine.Rendering.HighDefinition.WaterSurface waterSurface;
        readonly List<Ship> seenShips = new List<Ship>();
        double smokeSeakeepingMs, smokeSeaMs;
        int smokeSeakeepingSteps, smokeSeaFrames;
        double seaCompareN, seaCompareX, seaCompareY, seaCompareXY, seaCompareXX, seaCompareYY;
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
        /// <summary>The latest herald's text, for tools and tests.</summary>
        public string LastNotice { get; private set; } = string.Empty;
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
        int smokeFrames;
        float smokeFrameTime, smokeWorstFrame;

        public World World { get; private set; }

        /// <summary>
        /// After every simulation tick, with that tick's events still in World.Events (they are cleared right after):
        /// presentation that must see each event once, such as the crews on deck, listens here.
        /// </summary>
        public event System.Action<World> Ticked;
        public IReadOnlyCollection<int> Selected => selected;
        public IReadOnlyDictionary<int, ShipVisual> Views => views;
        public IReadOnlyDictionary<int, GameObject> WreckViews => wreckViews;
        public BattleEffects Effects => effects;
        public BattleFog Fog => fog;
        /// <summary>Reads the sea under every visible hull once a frame; null without HDRP water in the scene.</summary>
        public WaterBatchSampler Water => water;

        /// <summary>Presentation-only weather; calm in a match.</summary>
        public WeatherController Weather => weather;

        public IReadOnlyDictionary<int, ResourceSiteVisual> SiteViews => siteViews;
        public IReadOnlyDictionary<int, GameObject> SettlementViews => settlementViews;
        public IReadOnlyCollection<int> TargetedShips => targetedShips;
        public IReadOnlyCollection<int> InspectedEnemies => inspectedEnemies;
        public int InspectedSettlementId => inspectedSettlementId;

        /// <summary>
        /// A health bar appears only for what the player selected with the mouse, and over an enemy the player's
        /// ships are attacking, while the player can see it.
        /// </summary>
        public bool ShowsHealthBar(Ship ship) =>
            World.CanSee(PlayerFaction, ship) && (selected.Contains(ship.Id) || inspectedEnemies.Contains(ship.Id) || targetedShips.Contains(ship.Id));
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
            // The crews on deck read this battle after every tick and draw after the hulls (docs/art/ZALOGI.md).
            crewDirector = GetComponent<CrewDirector>();
            if (crewDirector == null) crewDirector = gameObject.AddComponent<CrewDirector>();
            groupFormation = Enum.TryParse(PlayerPrefs.GetString(FormationKey, nameof(FormationKind.Line)), out FormationKind savedFormation)
                && Enum.IsDefined(typeof(FormationKind), savedFormation) ? savedFormation : FormationKind.Line;
            var surface = FindAnyObjectByType<UnityEngine.Rendering.HighDefinition.WaterSurface>();
            waterSurface = surface;
            if (surface != null)
            {
                var orientation = surface.largeOrientationValue * Mathf.Deg2Rad;
                waveTravel = new Vector3(Mathf.Cos(orientation), 0f, Mathf.Sin(orientation));
            }
            StartMatch(MapFactory.CreateSkirmish(seed, LengthFromArguments(Environment.GetCommandLineArgs())), 0);
            if (surface != null)
            {
                waterHeights = new HdrpWaterHeights(surface);
                water = new WaterBatchSampler(waterHeights);

                // Bow waves and surf live in a square of water that follows the view, not the distant camera.
                decalAnchor = new GameObject("Water decal anchor").transform;
                surface.decalRegionAnchor = decalAnchor;
            }
            sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(light => light.type == LightType.Directional);

            // Calm in a match; only the showcase calls a storm until scenarios decide the weather (stage 5). The scene's
            // volume is never another battle's weather overlay.
            var sceneVolume = FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)
                .Where(item => item.isGlobal && item.GetComponentInParent<WeatherController>(true) == null)
                .OrderByDescending(item => item.priority).FirstOrDefault();
            weather = gameObject.AddComponent<WeatherController>();
            weather.Configure(surface, sun, sceneVolume);
            if (surface != null) seaRenderer = SeaSurfaceRenderer.TryCreate();
            ApplyMarineSea();
            ReadSmokeArguments();
            hud = BattleHud.Create(this);
        }

        void StartMatch(World world, int opponentBuildIndex, ShipLookBook savedLooks = null)
        {
            ClearViews();
            looks = savedLooks ?? new ShipLookBook();
            // A loaded match starts with no shots, wreckage or sinking hulls of the match it replaces.
            if (effects == null) effects = new BattleEffects(materials);
            else effects.Clear();
            World = world;
            // The match's wind and sea; a loaded match brings its own from the save.
            // -morzaNoSea runs the match without them, for A/B measurements in one build.
            if (World.Marine == null && Array.IndexOf(System.Environment.GetCommandLineArgs(), "-morzaNoSea") < 0) World.EnableMarineForMatch();
            seakeeping = new SeakeepingDriver();
            if (World.Marine != null)
            {
                // Surf faces the match's waves: they run with its wind.
                World.Marine.Wind.SynopticWind(out var windX, out var windY);
                if (windX * windX + windY * windY > 1e-6) waveTravel = new Vector3((float)windX, 0f, (float)windY).normalized;
            }
            // Trees sway in the match's wind on the game's clock (still in a pause); exposed crowns lean from the Etesian.
            Landscape.Vegetation.Wind = new Landscape.MarineTreeWind(() => World);
            Landscape.Vegetation.Clock = () => paused ? 0f : Time.unscaledDeltaTime * Speed;
            Landscape.Vegetation.Leeward = Landscape.Vegetation.LeewardOf(World.Marine?.Wind.Climate);
            if (fog == null) fog = new BattleFog(materials?.Fog);
            opponent = new Opponent(1) { BuildIndex = opponentBuildIndex };
            BuildStaticViews();
            SyncViews();
            fog.Update(World, PlayerFaction, 0f, true);

            var home = World.SettlementOf(PlayerFaction);
            pivot = new Vector3((float)home.X + 60f, 0f, (float)home.Y);
            if (strategicCamera == null) strategicCamera = Camera.main;
            PendingOrder = HudOrder.None;
            lastAlarm.Clear();
            // A new or loaded match starts calm; the weather is not part of a save.
            weather?.SetWeather(0f, 0f);
            ApplyMarineSea();
            if (hud != null) hud.OnMatchStarted();
        }

        /// <summary>HDRP's own bands for the match: direction and wind from the climate, the large band left to the decal.</summary>
        void ApplyMarineSea()
        {
            if (World?.Marine == null || waterSurface == null || weather == null || seaRenderer == null) return;
            SeaSurfaceRenderer.ConfigureSurface(waterSurface, World.Marine);
            var wind = SeaSurfaceRenderer.WindKilometresPerHour(World.Marine);
            weather.SetCalmSea(new SeaState(wind, weather.CalmSea.Chaos, Mathf.Clamp(wind * 0.4f, 2f, 15f)));
        }

        /// <summary>Hull motion of the ships the player can see, once per simulation tick.</summary>
        void StepSeakeeping()
        {
            if (World.Marine == null || seakeeping == null) return;
            seenShips.Clear();
            // Ships in the camera's view with a margin, so one coming in has settled on the water before it shows; without a
            // camera (tests, tools) those within 700 m of the view's centre, all that the widest zoom can show.
            var camera = strategicCamera != null && strategicCamera.isActiveAndEnabled ? strategicCamera : null;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, viewPlanes);
            foreach (var ship in World.Ships)
            {
                if (!views.TryGetValue(ship.Id, out var shown) || !World.CanSee(PlayerFaction, ship)) continue;
                var centre = ToWorld(ship);
                if (camera == null ? (centre - pivot).sqrMagnitude >= 700f * 700f
                    : !GeometryUtility.TestPlanesAABB(viewPlanes, new Bounds(centre, new Vector3(shown.Length + 2f * ViewMargin, 40f, shown.Length + 2f * ViewMargin)))) continue;
                seenShips.Add(ship);
            }
            // Far views step each hull every few ticks (an explicit approximation, SeakeepingDriver), close views every tick.
            var stride = camera != null && camera.orthographic && camera.orthographicSize > 0f
                ? SeakeepingDriver.StrideFor(camera.pixelHeight / (2f * camera.orthographicSize)) : 1;
            seakeeping.Step(World, seenShips, stride);
            if (smokeReport == null || smokeElapsed <= 5f) return;
            smokeSeakeepingMs += seakeeping.Milliseconds;
            smokeSeakeepingSteps++;
            smokeSeakeepingStepped += seakeeping.Stepped;
            smokeSeakeepingFollowed += seakeeping.Ships;
        }

        /// <summary>Metres around the view within which hulls are already moving with the sea.</summary>
        const float ViewMargin = 60f;
        readonly Plane[] viewPlanes = new Plane[6];
        double smokeSeakeepingStepped, smokeSeakeepingFollowed;

        /// <summary>
        /// Smoke runs: HDRP's water height (read back from the GPU, a few frames late) at eight points 250 m round the view,
        /// away from hulls and their bow waves, against the simulation's sea there. A correlation near one shows the decal
        /// draws the simulation's waves, the right way round.
        /// </summary>
        void CompareRenderedSea(float fraction)
        {
            if (smokeElapsed <= 5f) return;
            var marine = World.Marine;
            water.Begin();
            seaProbes.Clear();
            for (var k = 0; k < 8; k++)
            {
                var angle = k * Mathf.PI / 4f;
                var probe = pivot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 250f;
                if (marine.Depths.At(probe.x, probe.z) < 5) continue;
                var nearHull = false;
                foreach (var ship in World.Ships)
                    if ((ToWorld(ship) - probe).sqrMagnitude < 60f * 60f) { nearHull = true; break; }
                if (nearHull) continue;
                water.Add(probe, Vector3.forward, Vector3.right, 0f, 0f);
                seaProbes.Add(probe);
            }
            if (seaProbes.Count == 0 || !water.Sample())
            {
                CompareHullWaterlines();
                return;
            }
            var time = marine.SurfaceTime(fraction);
            var blend = marine.Surface.BlendWeight(fraction);
            for (var i = 0; i < seaProbes.Count; i++)
            {
                double rendered = water.Height(i, HullSamplePoints.Center);
                var simulated = marine.Surface.Sample(seaProbes[i].x, seaProbes[i].z, time, blend).Height;
                seaCompareN++;
                seaCompareX += simulated; seaCompareY += rendered;
                seaCompareXY += simulated * rendered; seaCompareXX += simulated * simulated; seaCompareYY += rendered * rendered;
            }
            CompareHullWaterlines();
        }

        readonly List<Vector3> seaProbes = new List<Vector3>();
        readonly List<ShipVisual> hullProbes = new List<ShipVisual>();
        readonly List<Ship> hullProbeShips = new List<Ship>();
        double hullWaterN, hullWaterSum, hullWaterSumSq, hullWaterMax, hullDeckAwash;

        const string DryHullName = "Dry hull (water excluder)";
        static readonly Dictionary<long, Mesh> dryHullMeshes = new Dictionary<long, Mesh>();

        /// <summary>
        /// HDRP draws its water plane through an open hull wherever a crest stands above the deck inside it. A water
        /// excluder shaped like the hull (a pointed prism inside the waterline, from the keel to above the gunwale)
        /// rides with the hull model and keeps the water out of it: drawing only.
        /// </summary>
        void KeepWaterOut(ShipVisual view)
        {
            var deck = view.Parts != null ? view.Parts.DeckHeight : 0.8f;
            var key = ((long)Mathf.RoundToInt(view.Length * 10f) << 40) | ((long)Mathf.RoundToInt(view.Beam * 10f) << 20) | (long)Mathf.RoundToInt((deck + view.Draft) * 10f);
            if (!dryHullMeshes.TryGetValue(key, out var mesh))
            {
                mesh = DryHullMesh(view.Length, view.Beam, view.Draft, deck);
                dryHullMeshes[key] = mesh;
            }
            var item = new GameObject(DryHullName);
            item.transform.SetParent(view.Model, false);
            item.AddComponent<UnityEngine.Rendering.HighDefinition.WaterExcluder>().SetExclusionMesh(mesh);
        }

        static Mesh DryHullMesh(float length, float beam, float draft, float deck)
        {
            const int stations = 12;
            var half = 0.46f * length;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var bottom = -draft - 0.05f;
            var top = deck + 0.6f;
            for (var s = 0; s <= stations; s++)
            {
                var z = -half + 2f * half * s / stations;
                var width = 0.42f * beam * (1f - Mathf.Pow(Mathf.Abs(z / half), 2.5f));
                vertices.Add(new Vector3(-width, top, z));
                vertices.Add(new Vector3(width, top, z));
                vertices.Add(new Vector3(-width, bottom, z));
                vertices.Add(new Vector3(width, bottom, z));
            }
            for (var s = 0; s < stations; s++)
            {
                int a = s * 4, b = a + 4;
                // Top and bottom caps, both sides.
                triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                triangles.AddRange(new[] { a + 2, a + 3, b + 2, a + 3, b + 3, b + 2 });
                // Port and starboard walls.
                triangles.AddRange(new[] { a, a + 2, b, b, a + 2, b + 2 });
                triangles.AddRange(new[] { a + 1, b + 1, a + 3, b + 1, b + 3, a + 3 });
            }
            var mesh = new Mesh { name = "Dry hull" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Smoke runs: HDRP's water under the stern, the sides and the middle of each hull near the view against the hull's
        /// shown waterline there. Positive: water standing above the waterline; above the deck it would flood the hull.
        /// </summary>
        void CompareHullWaterlines()
        {
            water.Begin();
            hullProbes.Clear();
            hullProbeShips.Clear();
            foreach (var ship in seenShips)
            {
                if (hullProbes.Count >= 12 || !views.TryGetValue(ship.Id, out var shown) || shown.Model == null) continue;
                if ((ToWorld(ship) - pivot).sqrMagnitude > 400f * 400f) continue;
                var root = shown.transform;
                water.Add(root.position, root.forward, root.right, shown.Length, shown.Beam);
                hullProbes.Add(shown);
                hullProbeShips.Add(ship);
            }
            if (hullProbes.Count == 0 || !water.Sample()) return;
            for (var i = 0; i < hullProbes.Count; i++)
            {
                var shown = hullProbes[i];
                var empty = (float)ShipCatalog.Get(hullProbeShips[i].Class).Draft;
                var waterline = shown.Draft - empty;
                var deck = shown.Parts != null ? shown.Parts.DeckHeight : 0.8f;
                foreach (var (point, local) in new[]
                {
                    (HullSamplePoints.Center, new Vector3(0f, waterline, 0f)),
                    (HullSamplePoints.Stern, new Vector3(0f, waterline, -shown.Length / 2f)),
                    (HullSamplePoints.Port, new Vector3(-shown.Beam / 2f, waterline, 0f)),
                    (HullSamplePoints.Starboard, new Vector3(shown.Beam / 2f, waterline, 0f)),
                })
                {
                    double above = water.Height(i, point) - shown.Model.TransformPoint(local).y;
                    hullWaterN++;
                    hullWaterSum += above;
                    hullWaterSumSq += above * above;
                    hullWaterMax = Math.Max(hullWaterMax, above);
                    if (above > deck) hullDeckAwash++;
                }
            }
        }

        double SeaCorrelation()
        {
            var n = seaCompareN;
            if (n < 3) return 0;
            var cov = n * seaCompareXY - seaCompareX * seaCompareY;
            var vx = n * seaCompareXX - seaCompareX * seaCompareX;
            var vy = n * seaCompareYY - seaCompareY * seaCompareY;
            return vx > 0 && vy > 0 ? cov / Math.Sqrt(vx * vy) : 0;
        }

        /// <summary>The simulation's waves drawn into the water around the view, on simulation time (still in a pause).</summary>
        void RenderSea(float fraction)
        {
            if (waterSurface != null) waterSurface.timeMultiplier = paused ? 0.001f : Speed;
            if (seaRenderer == null || World.Marine == null) return;
            SeaSurfaceRenderer.UpdateBands(waterSurface);
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            seaRenderer.Render(World.Marine, pivot, World.Marine.SurfaceTime(fraction), World.Marine.Surface.BlendWeight(fraction));
            if (smokeReport == null || smokeElapsed <= 5f) return;
            smokeSeaMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            smokeSeaFrames++;
        }

        // ---------- Showcase: staged pictures of the newest features (-morzaShowcase <prefix>) ----------

        string showcasePrefix;
        int showcaseStep, showcasePhase, showcaseSince;
        float showcaseClock, showcaseMark;
        Ship showcaseBoarder, showcaseCatapult;
        Ship[] showcaseGroup = Array.Empty<Ship>();
        Vector3 showcaseSpot;

        /// <summary>One staged picture: set the scene, let game time and the camera settle, then the shot (if any).</summary>
        sealed class ShowcaseStep
        {
            public Action Setup;
            public string Shot;
            public int Ticks;

            /// <summary>A ship the camera keeps centred while the scene plays.</summary>
            public Func<Ship> Follow;
        }

        ShowcaseStep[] showcaseSteps;

        /// <summary>
        /// A short scripted run for screenshots, not a test: the map with its hinterlands, the home port among its trees,
        /// a boarding fought on the rails, ranged fire, the weapon choice on a ship card and the port's building menu.
        /// Each scene waits for game time to pass and for the camera to rest at least two seconds before its picture,
        /// and nothing moves in the frame a picture is taken. The ships are added to the running game in open water
        /// south of the islands; the game quits afterwards.
        /// </summary>
        void UpdateShowcase()
        {
            showcaseClock += Time.unscaledDeltaTime;
            if (showcaseSteps == null) showcaseSteps = crewShowcase ? BuildCrewShowcase() : BuildShowcase();
            if (showcaseStep >= showcaseSteps.Length)
            {
                if (showcaseClock - showcaseMark > 1.5f) Application.Quit(0);
                return;
            }
            var step = showcaseSteps[showcaseStep];
            switch (showcasePhase)
            {
                case 0:
                    // The first scene waits for the game to finish loading and run steadily.
                    if (showcaseStep == 0 && (showcaseClock < 4f || World.Tick < 30)) return;
                    step.Setup?.Invoke();
                    showcaseSince = World.Tick;
                    showcaseMark = showcaseClock;
                    showcasePhase = 1;
                    return;
                case 1:
                    if (step.Follow != null && step.Follow() is Ship followed && World.FindShip(followed.Id) != null)
                    {
                        // A boarding is framed on the point where the hulls meet, not on the boarder alone.
                        var link = BoardingOf(followed.Id);
                        var other = link != null ? World.FindShip(link.AttackerId == followed.Id ? link.TargetId : link.AttackerId) : null;
                        pivot = other != null ? (ToWorld(followed) + ToWorld(other)) * 0.5f : ToWorld(followed);
                    }
                    if (World.Tick - showcaseSince < step.Ticks || showcaseClock - showcaseMark < 2.5f) return;
                    if (step.Shot != null)
                    {
                        Debug.Log($"[Showcase] before {step.Shot}: popover {(hud?.PopoverBoundsForTest?.ToString() ?? "none")}");
                        ScreenCapture.CaptureScreenshot($"{showcasePrefix}-{showcaseStep + 1:00}-{step.Shot}.png");
                        Debug.Log($"[Showcase] {step.Shot} at {showcaseClock:0.0} s, tick {World.Tick}");
                    }
                    showcaseMark = showcaseClock;
                    showcasePhase = 2;
                    return;
                default:
                    // The next scene changes nothing until the picture has been written.
                    if (showcaseClock - showcaseMark < 1f) return;
                    showcaseStep++;
                    showcasePhase = 0;
                    return;
            }
        }

        /// <summary>
        /// The south-most open water inside the camera's reach where both staged fights fit: every point of a grid
        /// spanning 220 by 180 m around the scene is sea.
        /// </summary>
        Vector3 ShowcaseWater(float spread)
        {
            for (var z = -400f * spread; z <= 300f * spread; z += 20f)
            for (var x = -350f * spread; x <= 250f * spread; x += 20f)
            {
                var open = true;
                for (var dz = -30f; dz <= 150f && open; dz += 20f)
                for (var dx = -60f; dx <= 160f && open; dx += 20f)
                    open = !World.OnLand(x + dx, z + dz);
                if (open) return new Vector3(x, 0f, z);
            }
            return new Vector3(0f, 0f, -400f * spread);
        }

        /// <summary>The camera at its default angle over the work point of this kind nearest a port: berth and islet.</summary>
        void FrameWorkPoint(Settlement port, ResourceType type)
        {
            var node = World.Nodes.Where(item => item.Type == type).OrderBy(item => World.Distance(item.X, item.Y, port.X, port.Y)).FirstOrDefault();
            if (node == null) return;
            pivot = new Vector3((float)node.X, 0f, (float)(node.Y + Rules.SiteLandOffset * 0.45));
            height = 55f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
        }

        ShowcaseStep[] BuildShowcase()
        {
            var home = World.SettlementOf(PlayerFaction);
            var spread = (float)World.Profile.Spread;
            var area = ShowcaseWater(spread);
            return new[]
            {
                new ShowcaseStep { Shot = "mapa", Ticks = 30, Setup = () =>
                {
                    pivot = Vector3.zero; height = 300f * Mathf.Max(1f, spread * 0.85f); yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
                } },
                new ShowcaseStep { Shot = "port-i-drzewa", Ticks = 30, Setup = () =>
                {
                    var landX = (float)Rules.SettlementLandX(home);
                    pivot = new Vector3((landX + (float)home.X) * 0.5f, 0f, (float)home.Y); height = 75f; yaw = -35f; tilt = ObliqueCamera.DefaultTilt;
                } },
                // The home forest and silver mine from the default camera, with the cutters at their jetties.
                new ShowcaseStep { Shot = "las-i-kuter", Ticks = 30, Setup = () => FrameWorkPoint(home, ResourceType.Wood) },
                new ShowcaseStep { Shot = "kopalnia", Ticks = 30, Setup = () => FrameWorkPoint(home, ResourceType.Silver) },
                // A ram sails past the far side of the middle rock: the rock must not hide it.
                new ShowcaseStep { Shot = "skala-i-okret", Ticks = 4 * Rules.TickRate, Setup = () =>
                {
                    var passing = World.SpawnShip(PlayerFaction, ShipClass.Ram, -45, 38);
                    passing.Heading = 0;
                    World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.Move, ShipIds = new[] { passing.Id }, X = 80, Y = 38 });
                    pivot = new Vector3(0f, 0f, 12f); height = 70f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
                } },
                // A boarder lies alongside an enemy ram, side by side; the hooks hold after 3 s and the decks fight.
                new ShowcaseStep { Shot = "abordaz", Ticks = 6 * Rules.TickRate, Follow = () => showcaseBoarder, Setup = () =>
                {
                    showcaseBoarder = World.SpawnShip(PlayerFaction, ShipClass.Boarder, area.x - 20, area.z);
                    var prey = World.SpawnShip(1 - PlayerFaction, ShipClass.Ram, area.x - 20, area.z + 13);
                    showcaseBoarder.Heading = prey.Heading = 0;
                    World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.Board, ShipIds = new[] { showcaseBoarder.Id }, TargetId = prey.Id });
                    pivot = area + new Vector3(-20f, 0f, 6f); height = 40f; yaw = -30f; tilt = ObliqueCamera.DefaultTilt;
                } },
                // Archers and a stone catapult against an enemy crew destroyer that shoots back.
                new ShowcaseStep { Shot = "ostrzal", Ticks = 4 * Rules.TickRate, Setup = () =>
                {
                    World.SpawnShip(PlayerFaction, ShipClass.CrewDestroyer, area.x + 60, area.z + 80);
                    showcaseCatapult = World.SpawnShip(PlayerFaction, ShipClass.Ram, area.x - 30, area.z + 100);
                    showcaseCatapult.Mounts[1] = WeaponType.StoneCatapult;
                    showcaseCatapult.Loadout[1] = WeaponType.StoneCatapult;
                    showcaseCatapult.Heading = 0;
                    var enemy = World.SpawnShip(1 - PlayerFaction, ShipClass.CrewDestroyer, area.x + 130, area.z + 120);
                    enemy.Hull = enemy.Definition.Hull * 3;
                    pivot = area + new Vector3(50f, 0f, 100f); height = 80f; yaw = -20f; tilt = ObliqueCamera.DefaultTilt;
                } },
                new ShowcaseStep { Ticks = 15, Setup = () =>
                {
                    selected.Clear();
                    if (showcaseCatapult != null && World.FindShip(showcaseCatapult.Id) != null) { selected.Add(showcaseCatapult.Id); pivot = ToWorld(showcaseCatapult); height = 55f; }
                } },
                new ShowcaseStep { Shot = "karta-okretu-bron", Ticks = 1, Setup = () =>
                {
                    var options = hud != null ? hud.OpenWeaponsForTest() : -1;
                    Debug.Log($"[Showcase] weapon choice opened with {options} options, selected {string.Join(",", selected)}");
                } },
                new ShowcaseStep { Ticks = 15, Setup = () =>
                {
                    hud?.ClosePopoverForTest();
                    selected.Clear();
                    inspectedSettlementId = home.Id;
                    pivot = new Vector3((float)home.X, 0f, (float)home.Y); height = 90f; yaw = ObliqueCamera.DefaultYaw;
                } },
                new ShowcaseStep { Shot = "karta-portu-rozbudowa", Ticks = 1, Setup = () => hud?.OpenBuildForTest() },
                // Weather is presentation only: a storm rolls over the home waters and passes; the battle goes on unchanged.
                new ShowcaseStep { Shot = "sztorm", Ticks = 12 * Rules.TickRate, Setup = () =>
                {
                    hud?.ClosePopoverForTest();
                    selected.Clear();
                    pivot = new Vector3((float)home.X + 45f, 0f, (float)home.Y - 10f); height = 70f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
                    weather?.SetWeather(1f, 6f);
                } },
                new ShowcaseStep { Shot = "po-sztormie", Ticks = 10 * Rules.TickRate, Setup = () =>
                {
                    LogWeather("storm");
                    weather?.SetWeather(0f, 6f);
                } },
                new ShowcaseStep { Ticks = 1, Setup = () => LogWeather("calm again") },
                // A group of rams sent to one point takes its own places: first a line abreast, then a wedge further on.
                new ShowcaseStep { Shot = "szyk-linia", Ticks = 14 * Rules.TickRate, Setup = () =>
                {
                    showcaseSpot = FormationWater(spread);
                    showcaseGroup = Enumerable.Range(0, 5)
                        .Select(i => World.SpawnShip(PlayerFaction, ShipClass.Ram, showcaseSpot.x - 70f, showcaseSpot.z + (i - 2) * 28f)).ToArray();
                    foreach (var ship in showcaseGroup) ship.Heading = 0;
                    selected.Clear();
                    foreach (var ship in showcaseGroup) selected.Add(ship.Id);
                    SetGroupFormation(FormationKind.Line, false);
                    SendMove(showcaseGroup.Select(ship => ship.Id).ToArray(), showcaseSpot);
                    pivot = showcaseSpot + new Vector3(-15f, 0f, 0f); height = 85f; yaw = ObliqueCamera.DefaultYaw; tilt = ObliqueCamera.DefaultTilt;
                } },
                new ShowcaseStep { Shot = "szyk-klin", Ticks = 14 * Rules.TickRate, Setup = () =>
                {
                    SetGroupFormation(FormationKind.Wedge, false);
                    var ahead = showcaseSpot + new Vector3(60f, 0f, 0f);
                    var group = showcaseGroup.Where(ship => World.FindShip(ship.Id) != null).Select(ship => ship.Id).ToArray();
                    SendMove(group, ahead);
                    pivot = ahead + new Vector3(-25f, 0f, 0f);
                    Debug.Log($"[Showcase] formation: {group.Length} rams, line then wedge");
                } },
            };
        }

        /// <summary>Open water away from every ship where a group and its formation fit: a box of 260 by 180 m, all sea.</summary>
        Vector3 FormationWater(float spread)
        {
            for (var z = -380f * spread; z <= 380f * spread; z += 20f)
            for (var x = -480f * spread; x <= 480f * spread; x += 20f)
            {
                if (World.Ships.Any(ship => World.Distance(ship.X, ship.Y, x, z) < 160)) continue;
                var open = true;
                for (var dz = -90f; dz <= 90f && open; dz += 15f)
                for (var dx = -160f; dx <= 100f && open; dx += 15f)
                    open = !World.OnLand(x + dx, z + dz);
                if (open) return new Vector3(x, 0f, z);
            }
            return ShowcaseWater(spread);
        }

        void LogWeather(string moment)
        {
            if (weather == null) return;
            Debug.Log($"[Showcase] weather {moment}: intensity {weather.Intensity:0.00}, {weather.Sea}, sea changes {weather.SeaChanges}, sun {(sun != null ? sun.intensity : 0f):0} lx");
        }

        void ReadSmokeArguments()
        {
            var arguments = Environment.GetCommandLineArgs();
            var showcase = Array.IndexOf(arguments, "-morzaShowcase");
            if (showcase >= 0 && showcase + 1 < arguments.Length) showcasePrefix = arguments[showcase + 1];
            ReadCrewShowcase(arguments);
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
            if (smokeReport == null)
            {
                HandleInput();
                if (!paused) Advance(Time.unscaledDeltaTime * Speed);
                PresentFrame();
                UpdateCamera();
                if (showcasePrefix != null) UpdateShowcase();
                return;
            }

            // The smoke run also times each part of the frame, and the CPU and GPU behind it.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            HandleInput();
            if (!paused) Advance(Time.unscaledDeltaTime * Speed);
            var simulated = watch.Elapsed.TotalMilliseconds;
            PresentFrame();
            var presented = watch.Elapsed.TotalMilliseconds;
            UpdateCamera();
            if (smokeElapsed > 5f)
            {
                smokeSimMs += simulated;
                smokePresentMs += presented - simulated;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, frameTiming) > 0)
                {
                    smokeTimed++;
                    smokeCpuMs += frameTiming[0].cpuMainThreadFrameTime;
                    smokeRenderMs += frameTiming[0].cpuRenderThreadFrameTime;
                    smokeGpuMs += frameTiming[0].gpuFrameTime;
                }
            }
            UpdateSmoke();
        }

        readonly FrameTiming[] frameTiming = new FrameTiming[1];
        double smokeSimMs, smokePresentMs, smokeCpuMs, smokeRenderMs, smokeGpuMs, smokeWaterMs;
        int smokeTimed, smokeWaterFrames;

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
            StepSeakeeping();
            ProcessEvents();
            Ticked?.Invoke(World);
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
                        if (World.FindShip(item.SubjectId)?.Owner == PlayerFaction) Announce("Rozkazu nie można wykonać: brak ładunku w porcie, nieodkryty lub zajęty brzeg.", HeraldLevel.Important, at);
                        break;
                    case "store_full":
                        if (World.FindShip(item.SubjectId)?.Owner == PlayerFaction) Announce("Magazyn pełny: reszta ładunku została na statku.", HeraldLevel.Important, at);
                        break;
                    case "outpost_built":
                        if (World.FindSettlement(item.OtherId)?.Owner == PlayerFaction) Announce("Przyczółek gotowy: ma własny magazyn, port i kolejkę portu.", HeraldLevel.Important, at);
                        break;
                    case "ship_built":
                        if (seen || World.FindSettlement(item.OtherId)?.Owner == PlayerFaction) BeginLaunch(item.SubjectId, item.OtherId, item.Amount == 1);
                        var launched = World.FindShip(item.SubjectId);
                        if (launched != null && launched.Owner == PlayerFaction) Announce($"Zwodowano: {ClassName(launched.Class)}.", HeraldLevel.Important, at, HeraldTopic.Launch, launched.Id, $"launch:{item.OtherId}");
                        break;
                    case "shipyard_built":
                        if (World.FindSettlement(item.SubjectId)?.Owner == PlayerFaction) Announce("Stocznia na przyczółku gotowa.", HeraldLevel.Important, at);
                        break;
                    case "volley":
                        // The shafts fly from the hands of the bank's men over their real flight (BattleEffects.SyncMissiles)
                        // and stay in the hull only where they strike it (ShotStruck).
                        WarnIfOwnUnderAttack(item.OtherId, at);
                        break;
                    case "ram_hit":
                        if (seen) { effects.Flash(at, 12f); RamBlow(item.SubjectId, item.OtherId, at, item.Amount); }
                        WarnIfOwnUnderAttack(item.OtherId, at);
                        break;
                    case "weapon_fired":
                        if (seen && views.TryGetValue(item.SubjectId, out var firing)) firing.Recoil((int)(item.Amount % 100));
                        break;
                    case "projectile_hit":
                        if (seen) ShotStruck(item.SubjectId, at, (WeaponType)(int)item.Amount, item.OtherId);
                        break;
                    case "projectile_miss":
                        if (seen) effects.WaterColumn(at, MissileSize((WeaponType)(int)item.Amount), item.SubjectId);
                        break;
                    case "projectile_land":
                    case "projectile_hit_settlement":
                        if (seen) effects.Splinters(at, MissileSize((WeaponType)(int)item.Amount) * 0.6f, item.SubjectId);
                        break;
                    case "ram_failed":
                        if (seen) effects.Flash(at, 8f);
                        break;
                    case "hooks_thrown":
                        // The lines themselves are drawn for the whole boarding (GrappleLines in UpdateDeckFights).
                        if (World.FindShip(item.OtherId)?.Owner == PlayerFaction) Announce("Abordaż! Wróg zarzuca haki – szermierze mogą przeciąć liny.", HeraldLevel.Critical, at, HeraldTopic.Alarm, item.OtherId, $"board:{item.OtherId}");
                        break;
                    case "hooks_cut":
                        if (World.FindShip(item.SubjectId)?.Owner == PlayerFaction) Announce(item.Amount == 1 ? "Obrońcy przecięli liny abordażowe." : "Haki zerwane – cel się wyrwał.", HeraldLevel.Important, at);
                        else if (World.FindShip(item.OtherId)?.Owner == PlayerFaction && item.Amount == 1) Announce("Szermierze przecięli liny – abordaż odparty.", HeraldLevel.Important, at);
                        break;
                    case "boarding_repelled":
                        if (World.FindShip(item.SubjectId)?.Owner == PlayerFaction) Announce("Abordaż odparty – cała załoga bojowa została na wrogim pokładzie. Okręt jest bezbronny, dopóki port nie przeszkoli nowej.", HeraldLevel.Important, at);
                        else if (World.FindShip(item.OtherId)?.Owner == PlayerFaction) Announce("Odparliśmy abordaż.", HeraldLevel.Important, at);
                        break;
                    case "ship_surrendered":
                        if (seen) Announce(World.FindShip(item.SubjectId)?.Owner == PlayerFaction ? "Załoga złamana – okręt się poddaje." : "Wroga załoga złożyła broń.", HeraldLevel.Important, at);
                        break;
                    case "project_done":
                        if (World.FindSettlement(item.SubjectId)?.Owner == PlayerFaction) Announce($"Ukończono: {ProjectName((ProjectKind)(int)item.Amount)}.", HeraldLevel.Important, at);
                        break;
                    case "weapon_made":
                        if (World.FindSettlement(item.SubjectId)?.Owner == PlayerFaction) Announce($"Warsztat: gotowa {WeaponCatalog.Get((WeaponType)(int)item.Amount).Name.ToLowerInvariant()}.", HeraldLevel.Info, at, HeraldTopic.General, item.SubjectId, $"workshop:{item.SubjectId}");
                        break;
                    case "ship_restored":
                        var restored = World.FindShip(item.SubjectId);
                        if (restored != null && restored.Owner == PlayerFaction) Announce($"Przegrupowano: {ClassName(restored.Class)} – kadłub i załoga gotowe.", HeraldLevel.Info, at, HeraldTopic.General, restored.Id, $"restored:{item.OtherId}");
                        break;
                    case "ship_captured":
                        if (seen) effects.Flash(at, 16f);
                        if (viewOwners.TryGetValue(item.SubjectId, out var formerOwner))
                        {
                            var takenName = viewClasses.TryGetValue(item.SubjectId, out var takenClass) ? ClassName(takenClass) : "okręt";
                            if (formerOwner == PlayerFaction) Announce($"Wróg zdobył abordażem: {takenName}.", HeraldLevel.Critical, at);
                            else if (seen) Announce($"Zdobyto abordażem: {takenName}.", HeraldLevel.Important, at);
                        }
                        break;
                    case "ship_sunk":
                        if (seen) { sinkingShips.Add(item.SubjectId); effects.Flash(at, 14f); }
                        if (viewOwners.TryGetValue(item.SubjectId, out var sunkOwner))
                        {
                            var sunkName = viewClasses.TryGetValue(item.SubjectId, out var sunkClass) ? ClassName(sunkClass) : "okręt";
                            if (sunkOwner == PlayerFaction) Announce($"Utracono: {sunkName}.", HeraldLevel.Critical, at);
                            else if (seen) Announce($"Zatopiono wrogi okręt: {sunkName}.", HeraldLevel.Important, at);
                        }
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
            // The sea's native search buffers are released first, so nothing that fails below can leak them.
            waterHeights?.Dispose();
            waterHeights = null;
            water = null;
            seaRenderer?.Dispose();
            seaRenderer = null;
            ClearViews();
            if (crewRoot != null) Destroy(crewRoot.gameObject);
            crewRoot = null;
            effects?.Clear();
        }

        /// <summary>Reads the sea from a stand-in instead of HDRP's water, for tests of the hull poses.</summary>
        public void UseWaterForTest(IWaterHeights heights) => water = new WaterBatchSampler(heights);

        void ClearViews()
        {
            foreach (var view in views.Values) if (view != null) Destroy(view.gameObject);
            foreach (var item in mapObjects) if (item != null) Destroy(item);
            views.Clear();
            viewOwners.Clear();
            viewClasses.Clear();
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
            heavyBlows.Clear();
            inspectedEnemies.Clear();
            inspectedSettlementId = -1;
            foreach (var crew in crews.Values) crew.Destroy();
            foreach (var gang in dockGangs.Values) gang.Destroy();
            foreach (var fight in deckFights.Values) fight.Destroy();
            deckFights.Clear();
            foreach (var lines in grapples.Values) lines.Destroy();
            grapples.Clear();
            crews.Clear();
            dockGangs.Clear();
            docked.Clear();
            siteWorkplaces.Clear();
            portWorkplaces.Clear();
            yards.Clear();
            launches.Clear();
            recalled.Clear();
            foreach (var mesh in generatedMeshes) if (mesh != null) Destroy(mesh);
            generatedMeshes.Clear();
            BouldersPlaced = 0;
            TreesPlanted = 0;
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
            double Rate(Ship worker) => World.WorkRate(node, worker);
            var total = 0.0;
            foreach (var id in node.Workers)
            {
                var worker = World.FindShip(id);
                if (worker != null && worker.Harvest == HarvestPhase.Working) total += Rate(worker);
            }
            var cap = World.NodeCap(node);
            var share = total > cap ? cap / total : 1.0;
            var own = Rate(ship) * share * Rules.Centi;
            if (own <= 0) return double.MaxValue;
            var room = ship.Definition.Capacity * Rules.Centi - ship.Cargo;
            var untilFull = room / own;
            var untilEmpty = total > 0 ? node.Stock / (total * share * Rules.Centi) : double.MaxValue;
            return fixedPart + Math.Min(untilFull, untilEmpty);
        }

        readonly Dictionary<int, DeckFight> deckFights = new Dictionary<int, DeckFight>();

        /// <summary>
        /// Figures now fighting on the rails of every boarding the player can see; zero when none. With the crews aboard
        /// (CrewDirector) these are the real crews' fronts, so no second set of men is drawn.
        /// </summary>
        public int DeckFightersShown => crewDirector != null ? crewDirector.RailFighters : deckFights.Values.Sum(fight => fight.Shown);

        CrewDirector crewDirector;

        readonly Dictionary<int, GrappleLines> grapples = new Dictionary<int, GrappleLines>();

        /// <summary>Grapple lines now standing between the hulls of every boarding the player can see; zero when none.</summary>
        public int GrappleLinesShown => grapples.Values.Sum(lines => lines.Shown);

        /// <summary>
        /// Men fight across the rails only while the grapples hold (the melee); before that the lines fly across
        /// (BattleEffects.Rope). Each boarding gets its figures from the decks of both hulls as they ride the waves.
        /// </summary>
        void UpdateDeckFights(float delta)
        {
            foreach (var link in World.Boardings)
            {
                if (!views.TryGetValue(link.AttackerId, out var attackerView) || !views.TryGetValue(link.TargetId, out var targetView)) continue;
                var attacker = World.FindShip(link.AttackerId);
                var target = World.FindShip(link.TargetId);
                if (attacker == null || target == null) continue;
                var visible = attackerView.gameObject.activeSelf && targetView.gameObject.activeSelf;
                // The lines stand from the throw until the link ends, slack while the hooks are fought over.
                if (!grapples.TryGetValue(link.AttackerId, out var lines)) grapples[link.AttackerId] = lines = new GrappleLines(crewRoot, materials);
                lines.Update(attackerView, targetView, GrappleLines.Standing(link.Hooked, link.CutProgress), link.Hooked, delta, visible);
                // The crews aboard fight at their own rails (CrewDirector); the stand-in figures are only for a battle without them.
                if (!link.Hooked || crewDirector != null) continue;
                if (!deckFights.TryGetValue(link.AttackerId, out var fight))
                {
                    fight = new DeckFight(crewRoot, materials, attacker.Owner, target.Owner);
                    var seed = link.AttackerId * 7 + link.TargetId;
                    fight.Splash = at => effects.WaterColumn(at, 0.8f, seed++);
                    deckFights[link.AttackerId] = fight;
                }
                fight.Update(attackerView, targetView, link.AttackerFront, link.TargetFront, delta, visible);
            }
            foreach (var id in deckFights.Keys.Where(id => !World.Boardings.Any(link => link.Hooked && link.AttackerId == id)).ToList())
            {
                deckFights[id].Destroy();
                deckFights.Remove(id);
            }
            foreach (var id in grapples.Keys.Where(id => !World.Boardings.Any(link => link.AttackerId == id)).ToList())
            {
                grapples[id].Destroy();
                grapples.Remove(id);
            }
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

            UpdateDeckFights(delta);

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
            foreach (var island in World.Islands.Where(item => item.Kind == IslandKind.Hinterland)) mapObjects.Add(CreateHinterland(island));

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
                if (materials?.Floor != null) shelfRenderer.sharedMaterial = materials.Floor;

                var coast = island.Kind == IslandKind.Coast;
                if (coast)
                {
                    // A rocky foreshore at the foot of the cliffs, so the land runs on down under the water.
                    var beach = new GameObject("Beach");
                    beach.transform.SetParent(holder.transform, false);
                    beach.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Beach(outline);
                    var beachRenderer = beach.AddComponent<MeshRenderer>();
                    if (materials?.ShoreRock != null) beachRenderer.sharedMaterial = materials.ShoreRock;
                }
                ScatterBoulders(holder.transform, outline, island.X * 1.31 + island.Y * 0.77, coast);

                // High rock throws breaking waves up as spray; low beaches only foam.
                var high = coast || island.Kind == IslandKind.Hinterland;
                var spray = high ? ShorelineSpray(holder.transform) : null;
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
                holder.SetActive(high);
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
                var site = ResourceSiteVisual.Create(node, materials, LandAt(node.X, node.Y + Rules.SiteLandOffset)?.Outline);
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
            // Built into the slope behind the harbour: its foot at the lowest ground under it, its back set into the hill.
            var shape = ShoreOf(settlement.X, settlement.Y);
            var centreX = -toward * (width * 0.5f + 1.5f);
            var foot = float.MaxValue;
            for (var corner = 0; corner < 4; corner++)
            {
                var x = centreX + ((corner & 1) == 0 ? -0.5f : 0.5f) * width;
                var z = ((corner & 2) == 0 ? -0.5f : 0.5f) * depth;
                foot = Mathf.Min(foot, ShoreShape.GroundAt(shape.outline, shape.lift, x, z));
            }
            AddViewPart(root, PrimitiveType.Cube, "Buildings", materials?.Neutral, new Vector3(centreX, foot + tall * 0.5f - 0.3f, 0f), new Vector3(width, tall, depth));
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

            // The game's own irregular waterline of this land, so the shore drawn is the shore ships steer by:
            // a low landing toward the port, limestone cliffs dropping into the sea on the open side.
            var islet = new GameObject("Islet");
            islet.transform.SetParent(root.transform, false);
            var shape = ShoreOf(portX, portY);
            islet.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Mound(shape.outline, shape.lift);
            islet.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials?.ShoreRock, materials?.Ground(materials.Foliage) };
            // The sea's work on its shore: a notch under the cliffs, the wet swash, beachrock on the gentle side.
            Landscape.CoastFeatures.Build(islet.transform, shape.outline, shape.lift, (uint)Mathf.RoundToInt((float)(portX * 31 + portY * 17)), materials?.ShoreRock,
                Landscape.LandscapeArt.Wet(materials?.ShoreRock), Landscape.LandscapeArt.Beachrock(materials?.Floor));
            var (planted, pines) = PlantGrove(root.transform, shape.outline, shape.lift, portX * 3.1 + portY * 1.7, Mathf.Atan2(0f, (float)portX - landX), 115f, 0.24f, 0.82f, (x, z) =>
                // Keep clear of where a settlement's buildings stand on the plateau.
                Mathf.Abs(z) < 9.5f && x * Mathf.Sign((float)portX - landX) < 1f && x * Mathf.Sign((float)portX - landX) > -14.5f,
                // A few low olives and a cypress or two: a port's islet is not a forest, and nothing tall stands by the berth.
                6, 5f, true);
            MostShoreTrees = Mathf.Max(MostShoreTrees, planted);
            ShorePinesPlanted += pines;

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


        /// <summary>
        /// Rocks fallen from the shore: some half out of the water at the waterline, some lying on the shallow floor,
        /// seen dark through the clear water as on any rocky Aegean coast. Seeded, so the same land always gets the
        /// same rocks. Presentation only; ships steer by the land's outline and shallows, not by these.
        /// </summary>
        public int BouldersPlaced { get; private set; }

        /// <summary>A boulder model taken apart once: its meshes, their materials and placement, and its width.</summary>
        sealed class BoulderModel
        {
            public (Mesh mesh, Material material, Matrix4x4 local)[] Parts;
            public float Width;
        }

        readonly Dictionary<GameObject, BoulderModel> boulderModels = new Dictionary<GameObject, BoulderModel>();
        readonly List<Mesh> generatedMeshes = new List<Mesh>();
        readonly Dictionary<(Material, bool), List<CombineInstance>> boulderBatch = new Dictionary<(Material, bool), List<CombineInstance>>();

        BoulderModel ModelOf(GameObject prefab)
        {
            if (boulderModels.TryGetValue(prefab, out var model)) return model;
            var parts = new List<(Mesh, Material, Matrix4x4)>();
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null) continue;
                var local = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                parts.Add((filter.sharedMesh, renderer.sharedMaterial, local));
                var meshBounds = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = local.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            model = new BoulderModel { Parts = parts.ToArray(), Width = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z)) };
            boulderModels[prefab] = model;
            return model;
        }

        /// <summary>
        /// Rocks of one shore are merged into a few meshes (one per material, with and without shadows), so hundreds
        /// of boulders cost a handful of draws instead of thousands.
        /// </summary>
        void ScatterBoulders(Transform parent, float[] outline, double seed, bool coast)
        {
            var prefabs = materials?.Boulders;
            if (prefabs == null || prefabs.Length == 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "-morzaNoBoulders") >= 0) return;
            var random = new System.Random((int)(seed * 1000.0) ^ 0x5BD1E995);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
            foreach (var list in boulderBatch.Values) list.Clear();

            foreach (var (point, _) in ShoreShape.SurfPoints(outline, coast ? 8f : 9f))
            {
                var outward = point.normalized;
                var side = new Vector3(-outward.z, 0f, outward.x);
                var shore = point - outward * 2f;
                var count = random.Next(1, coast ? 3 : 2) + (random.NextDouble() < 0.3 ? 1 : 0);
                for (var k = 0; k < count; k++)
                {
                    // From under the cliff foot out over the shallows; the further out, the deeper it lies.
                    var out_ = Next(-1.5f, coast ? 9f : 7f);
                    var size = Mathf.Lerp(1.1f, coast ? 4.2f : 2.8f, Mathf.Pow((float)random.NextDouble(), 2f)) * (out_ > 4f ? 1.3f : 1f);
                    var depth = out_ < 1.5f ? Next(-0.35f, 0.05f) * size : -Mathf.Lerp(0.4f, 2.4f, Mathf.InverseLerp(1.5f, 9f, out_)) - size * 0.25f;
                    var model = ModelOf(prefabs[random.Next(prefabs.Length)]);
                    var at = shore + outward * out_ + side * Next(-2.5f, 2.5f);
                    var placement = Matrix4x4.TRS(new Vector3(at.x, depth, at.z), Quaternion.Euler(Next(-8f, 8f), Next(0f, 360f), Next(-8f, 8f)), Vector3.one * (size / model.Width));
                    var shadow = out_ < 2.5f;
                    foreach (var (mesh, material, local) in model.Parts)
                    {
                        if (!boulderBatch.TryGetValue((material, shadow), out var list)) boulderBatch[(material, shadow)] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = mesh, transform = placement * local });
                    }
                    BouldersPlaced++;
                }
            }

            foreach (var pair in boulderBatch)
            {
                if (pair.Value.Count == 0) continue;
                var combined = new Mesh { name = "Boulders", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(pair.Value.ToArray(), true, true);
                generatedMeshes.Add(combined);
                var rocks = new GameObject("Boulders");
                rocks.transform.SetParent(parent, false);
                rocks.AddComponent<MeshFilter>().sharedMesh = combined;
                var renderer = rocks.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key.Item1;
                renderer.shadowCastingMode = pair.Key.Item2 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static void AddViewPart(GameObject root, PrimitiveType type, string name, Material material, Vector3 position, Vector3 scale)
        {
            var part = Primitive(type, name, material);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
        }

        public int TreesPlanted { get; private set; }

        /// <summary>Trees planted on rocky coasts. The small rocks of the map carry none, so no tree hides a hull.</summary>
        public int CoastTreesPlanted { get; private set; }

        /// <summary>Most trees on any one port or free-shore islet, and stone pines among them (only olives and cypresses).</summary>
        public int MostShoreTrees { get; private set; }
        public int ShorePinesPlanted { get; private set; }

        /// <summary>
        /// A grove on an islet's plateau, on the open side away from the port, clear of whatever stands there: stone
        /// pines with a few cypresses, or, on the islets of ports and free shores, low olives with at most two
        /// cypresses, so the trees stay below the masts moored beside them. Seeded, so the same land always gets the
        /// same grove. Returns how many trees it planted and how many of them are stone pines.
        /// </summary>
        (int trees, int pines) PlantGrove(Transform parent, float[] outline, float[] lift, double seed, float shelteredAngle, float minAway, float nearFraction, float farFraction, Func<float, float, bool> blocked, int maxTrees = 14, float spacing = 6f, bool olives = false)
        {
            var random = new System.Random((int)(seed * 100.0) ^ 0x2F6A1D3);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var dark = new TreeModels.Grove();
            // Olives are trees of their own (Presentation.Landscape): bones the match's wind bends, a form from a stable id.
            var olive = new Landscape.TreeGroveBuilder();
            var groveId = (uint)((int)(seed * 100.0) ^ 0x2F6A1D3);
            var leeward = parent != null ? parent.InverseTransformDirection(Landscape.Vegetation.Leeward) : Vector3.zero;
            var profile = Landscape.EnvironmentProfile.Default;
            var planted = new List<Vector2>();
            var cypresses = 0;
            var pines = 0;
            for (var attempt = 0; attempt < maxTrees * 6 && planted.Count < maxTrees; attempt++)
            {
                var angle = Next(0f, Mathf.PI * 2f);
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, shelteredAngle * Mathf.Rad2Deg)) < minAway) continue;
                var radius = ShoreShape.At(outline, angle) * Next(nearFraction, farFraction);
                var spot = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                if (blocked != null && blocked(spot.x, spot.y)) continue;
                if (planted.Exists(other => (other - spot).sqrMagnitude < spacing * spacing)) continue;
                planted.Add(spot);
                var kind = olives
                    ? cypresses < 2 && random.NextDouble() < 0.2 ? TreeKind.Cypress : TreeKind.Olive
                    : random.NextDouble() < 0.3 ? TreeKind.Cypress : TreeKind.StonePine;
                if (kind == TreeKind.Cypress) cypresses++;
                if (kind == TreeKind.StonePine) pines++;
                var ground = ShoreShape.GroundAt(outline, lift, spot.x, spot.y) - 0.15f;
                var scale = kind == TreeKind.StonePine ? Next(0.5f, 0.75f) : kind == TreeKind.Olive ? Next(0.5f, 0.65f) : olives ? Next(0.5f, 0.62f) : Next(0.55f, 0.8f);
                var variant = random.Next(4);
                var yaw = Next(0f, 360f);
                if (kind == TreeKind.Olive)
                {
                    var id = Landscape.StableHash.Mix(groveId, (uint)planted.Count);
                    var species = Landscape.StableHash.Unit(Landscape.StableHash.Mix(id, 5u)) < profile.CultivatedOliveShare ? Landscape.TreeSpecies.OliveCultivated : Landscape.TreeSpecies.OliveWild;
                    // Trees near the waterline stand in the wind; those inland are sheltered.
                    var exposure = Mathf.Clamp01(1f - (ShoreShape.At(outline, angle) - radius) / 25f);
                    olive.Add(species, id, new Vector3(spot.x, ground, spot.y), yaw, leeward, exposure);
                }
                else dark.Add(kind, variant, new Vector3(spot.x, ground, spot.y), yaw, scale);
            }
            AddGrove(parent, dark, materials?.Foliage);
            if (olive.Count > 0)
            {
                olive.Build(parent, "Olives", Landscape.LandscapeArt.OliveBark ?? materials?.Timber, Landscape.LandscapeArt.OliveLeaves ?? materials?.OliveFoliage);
                TreesPlanted += olive.Count;
            }
            return (planted.Count, pines);
        }

        void AddGrove(Transform parent, TreeModels.Grove grove, Material leaves)
        {
            var mesh = grove.Build("Grove");
            if (mesh == null) return;
            generatedMeshes.Add(mesh);
            var trees = new GameObject("Grove");
            trees.transform.SetParent(parent, false);
            trees.AddComponent<MeshFilter>().sharedMesh = mesh;
            trees.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials?.Timber, leaves };
            TreesPlanted += grove.Count;
        }

        /// <summary>
        /// The large island behind a port or a work point: a limestone plateau on the game's own waterline, falling in
        /// cliffs to the open sea and gently toward the islet it joins, wooded with stone pines and cypresses.
        /// Always drawn, like the rocky coasts: it is the lie of the land, not something scouted.
        /// </summary>
        GameObject CreateHinterland(Island island)
        {
            var root = new GameObject("Hinterland");
            root.transform.position = new Vector3((float)island.X, 0f, (float)island.Y);
            var outline = OutlineOf(island);
            var joined = World.Islands.Where(item => item.Kind == IslandKind.Settlement || item.Kind == IslandKind.ResourceSite)
                .OrderBy(item => World.Distance(item.X, item.Y, island.X, island.Y)).FirstOrDefault();
            var toward = joined != null ? Mathf.Atan2((float)(joined.Y - island.Y), (float)(joined.X - island.X)) : 0f;
            var lift = ShoreShape.Cliffs(toward, 60f, 110f, (float)(island.X * 0.29 + island.Y * 0.17));
            var land = new GameObject("Plateau");
            land.transform.SetParent(root.transform, false);
            land.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Mound(outline, lift);
            land.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials?.ShoreRock, materials?.Ground(materials.Foliage) };
            Landscape.CoastFeatures.Build(land.transform, outline, lift, (uint)Mathf.RoundToInt((float)(island.X * 29 + island.Y * 13)), materials?.ShoreRock,
                Landscape.LandscapeArt.Wet(materials?.ShoreRock), Landscape.LandscapeArt.Beachrock(materials?.Floor));
            // Woods thicken with the island's size, within a budget a whole archipelago can afford to draw.
            var trees = Mathf.Clamp(Mathf.RoundToInt((float)island.Radius * 0.28f), 10, 32);
            PlantGrove(root.transform, outline, lift, island.X * 2.3 + island.Y * 1.1, toward, 25f, 0.1f, 0.8f, null, trees, 9f);
            return root;
        }

        /// <summary>A port's islet as drawn: the game's waterline and the cliffs on its open side, away from the port.</summary>
        (float[] outline, float[] lift) ShoreOf(double portX, double portY)
        {
            var landX = Rules.LandX(portX);
            var land = LandAt(landX, portY);
            var outline = land != null ? OutlineOf(land) : ShoreShape.Ellipse((float)Rules.SettlementLandRadius, (float)Rules.SettlementLandRadius);
            var toward = portX >= landX ? 0f : Mathf.PI;
            return (outline, ShoreShape.Cliffs(toward, 95f, 150f, (float)(landX * 0.37 + portY * 0.11)));
        }

        /// <summary>The land whose centre lies at this point, if any.</summary>
        Island LandAt(double x, double y) => World.Islands.FirstOrDefault(island => World.Distance(island.X, island.Y, x, y) < 0.5);

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

            // A small rock wears the pale limestone of the islets' cliffs (world-mapped, so the model needs no UVs):
            // all the rock of the map reads as one Aegean stone.
            if (island.Radius < WoodedCoastRadius && materials?.Limestone != null)
                foreach (var renderer in coast.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(materials.Limestone, renderer.sharedMaterials.Length).ToArray();

            // Coasts on the second side are turned half a turn, like their waterline in the simulation.
            root.transform.rotation = Quaternion.Euler(0f, (float)island.Yaw, 0f);

            // Stone pines and cypresses over the high level ground of a large rocky coast, as on the wooded Aegean
            // headlands. A small rock stays bare: trees at their real size would tower over it and over passing hulls.
            var spots = materials?.CoastTreeSpots;
            if (spots != null && spots.Length > 0 && island.Radius >= WoodedCoastRadius)
            {
                var grove = new TreeModels.Grove();
                var random = new System.Random((int)(island.X * 13 + island.Y * 7));
                foreach (var spot in spots)
                {
                    var local = root.transform.InverseTransformPoint(coast.transform.TransformPoint(spot));
                    var cypress = random.NextDouble() < 0.22;
                    grove.Add(cypress ? TreeKind.Cypress : TreeKind.StonePine, random.Next(4), local - new Vector3(0f, 0.25f, 0f), (float)random.NextDouble() * 360f,
                        cypress ? 0.6f + (float)random.NextDouble() * 0.3f : 0.65f + (float)random.NextDouble() * 0.35f);
                }
                AddGrove(root.transform, grove, materials?.Foliage);
                CoastTreesPlanted += grove.Count;
            }
            return root;
        }

        /// <summary>The smallest rocky coast that carries a wood (metres from its centre to its farthest shore).</summary>
        public const double WoodedCoastRadius = 40;

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
                    view = ShipVisual.Create(ship, materials, plainShips ? null : looks.For(ship));
                    views[ship.Id] = view;
                    viewOwners[ship.Id] = ship.Owner;
                    viewClasses[ship.Id] = ship.Class;
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
                if (sinkingShips.Remove(id)) effects.Sink(views[id], BreaksUp(id, views[id]));
                else Destroy(views[id].gameObject);
                views.Remove(id);
                lastPositions.Remove(id);
                viewOwners.Remove(id);
                viewClasses.Remove(id);
                previous.Remove(id);
                selected.Remove(id);
            }
        }

        void SyncWrecks()
        {
            foreach (var wreck in World.Wrecks)
            {
                if (wreckViews.ContainsKey(wreck.Id)) continue;
                // Floating wreckage over the salvageable cargo: amphorae for silver, crates and timber for wood.
                wreckViews[wreck.Id] = effects.CreateWreck(new Vector3((float)wreck.X, 0f, (float)wreck.Y), wreck.Id, wreck.Silver > wreck.Wood);
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

        /// <summary>Ships whose last blow was a ram or a heavy shot: if that blow sinks them, they break apart. Presentation only.</summary>
        readonly HashSet<int> heavyBlows = new HashSet<int>();

        static float MissileSize(WeaponType weapon) => 0.6f + (float)WeaponCatalog.Get(weapon).Hull / 80f;

        /// <summary>A ram strikes: a breach at the waterline where the bow met the side, splinters and a burst of water.</summary>
        void RamBlow(int attackerId, int targetId, Vector3 at, double damage)
        {
            if (!views.TryGetValue(targetId, out var target)) return;
            var contact = at;
            if (lastPositions.TryGetValue(attackerId, out var ram))
            {
                var toward = Vector3.ProjectOnPlane(ram - at, Vector3.up);
                if (toward.sqrMagnitude > 1e-4f) contact = at + toward.normalized * target.Beam * 0.5f;
            }
            target.Hit(contact, Mathf.Lerp(1.4f, 2.6f, Mathf.Clamp01((float)damage / 300f)), true);
            effects.Splinters(contact, 2f + (float)damage / 150f, attackerId * 31 + targetId);
            effects.WaterColumn(contact, 1.6f, attackerId * 37 + targetId);
            heavyBlows.Add(targetId);
        }

        /// <summary>A machine's shot strikes a ship: a breach where it hit and splinters; a heavy shot may break the ship if it sinks.</summary>
        void ShotStruck(int shipId, Vector3 at, WeaponType weapon, int shooterId = -1)
        {
            // A hull drawn hauled alongside in a boarding takes its blows where it is drawn.
            if (crewDirector != null) at += crewDirector.HaulOffset(shipId);
            var size = MissileSize(weapon);
            effects.Splinters(at, size, shipId * 53 + (int)weapon);
            if (!views.TryGetValue(shipId, out var view)) return;
            view.Hit(at, size, false);
            // A volley's arrows stay in the hull they struck, pointing back the way they came.
            if (weapon == WeaponType.ArcherBank && lastPositions.TryGetValue(shooterId, out var from)) view.StickArrows(from, WeaponCatalog.Get(weapon).Crew);
            if (WeaponCatalog.Get(weapon).Hull >= 100) heavyBlows.Add(shipId);
            else heavyBlows.Remove(shipId);
        }

        bool BreaksUp(int shipId, ShipVisual view) => heavyBlows.Remove(shipId) || (view != null && view.BreachCount >= 5);

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
            effects.SyncMissiles(World, PlayerFaction, blend);
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
            RenderSea(blend);
            UpdateCrews(paused ? 0f : delta * Speed);
            if (crewDirector != null) crewDirector.UpdateHauls(paused ? 0f : delta * Speed);

            // Surf rises and falls as sets of waves reach each shore, never all shores at once.
            var now = Time.unscaledTime;
            foreach (var patch in surf)
            {
                if (patch.Decal == null) continue;

                // Surf would reveal small land under the fog, so it shows only where the player has explored.
                var holder = patch.Decal.transform.parent.gameObject;
                var shown = patch.Island.Kind == IslandKind.Coast || patch.Island.Kind == IslandKind.Hinterland || World.IsExplored(PlayerFaction, patch.Island.X, patch.Island.Y);
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
                // A boarder is drawn hauled alongside its target by the grapples (the picture only, CrewDirector.HaulOffset).
                if (crewDirector != null) gamePosition += crewDirector.HaulOffset(ship.Id);
                if (!PlaceLaunch(ship, view, gamePosition, gameRotation, paused ? 0f : delta * Speed)) view.transform.SetPositionAndRotation(gamePosition, gameRotation);
                UpdateOarRoom(ship, view);
                view.Selection.SetActive(selected.Contains(ship.Id) || inspectedEnemies.Contains(ship.Id));
                var capacity = ship.Definition.Capacity * Rules.Centi;
                view.SetLoad(capacity > 0 ? (float)(ship.Cargo + ship.HoldWood + ship.HoldSilver) / capacity : 0f);
                view.SetCondition((float)(ship.Hull / ship.Definition.Hull), ship.ModuleHp);
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

        /// <summary>
        /// Reads the real surface under bow, stern, sides and centre of every visible ship in one batch a frame
        /// (WaterBatchSampler). A hidden ship is not read; without sea data in a frame the hulls keep their last pose.
        /// </summary>
        void UpdateWaterPoses()
        {
            if (World.Marine != null && seakeeping != null && seaRenderer != null)
            {
                // The hulls' own dynamics in the simulation's sea, between the last two ticks.
                var fraction = (float)(accumulator / Rules.Dt);
                foreach (var ship in World.Ships)
                {
                    if (!views.TryGetValue(ship.Id, out var shown)) continue;
                    if (!seakeeping.TryPose(ship.Id, World.Tick, fraction, out var physical, out var bow, out var heaveRate)) continue;
                    shown.SetPhysicalPose(physical, bow, heaveRate);
                    if (shown.Model != null && shown.Model.Find(DryHullName) == null) KeepWaterOut(shown);
                    maxRollSeen = Mathf.Max(maxRollSeen, Mathf.Abs(physical.Roll), Mathf.Abs(physical.Pitch));
                    maxHeaveSeen = Mathf.Max(maxHeaveSeen, Mathf.Abs(physical.Heave));
                }
                // In a smoke run HDRP's heights under the hulls are still read: they must match the simulation's sea.
                if (smokeReport != null && water != null) CompareRenderedSea(fraction);
                return;
            }
            if (water == null || World.Ships.Count == 0) return;
            var started = smokeReport != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            water.Begin();
            waterHulls.Clear();
            foreach (var ship in World.Ships)
            {
                if (!views.TryGetValue(ship.Id, out var view) || !World.CanSee(PlayerFaction, ship)) continue;
                var hull = view.transform;
                water.Add(hull.position, hull.forward, hull.right, view.Length, view.Beam);
                waterHulls.Add(view);
            }

            if (water.Sample())
            {
                for (var i = 0; i < waterHulls.Count; i++)
                {
                    var view = waterHulls[i];
                    var pose = water.Pose(i, view.Length, view.Beam);
                    view.SetWaterPose(pose, water.Height(i, HullSamplePoints.Bow));
                    maxRollSeen = Mathf.Max(maxRollSeen, Mathf.Abs(pose.Roll), Mathf.Abs(pose.Pitch));
                    maxHeaveSeen = Mathf.Max(maxHeaveSeen, Mathf.Abs(pose.Heave));
                }
            }

            if (smokeReport == null || smokeElapsed <= 5f) return;
            smokeWaterMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            smokeWaterFrames++;
        }

        static Vector3 ToWorld(Ship ship) => new Vector3((float)ship.X, 0f, (float)ship.Y);

        // ---------- Camera (C10) ----------

        void UpdateCamera()
        {
            if (strategicCamera == null) return;
            var delta = Time.unscaledDeltaTime;
            var move = Vector3.zero;
            // Edge scrolling needs a focused game and a cursor inside the screen; the automatic test never scrolls.
            var mouse = GameInput.MousePosition;
            // A dialog holds the input: the view stays put, the world keeps running.
            var free = !InputHeldByInterface;
            var edgeScroll = free && Application.isFocused && autoPlayer == null && showcasePrefix == null && GameInput.PointerKnown
                             && mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height;
            if (free && KeyBindings.Held(GameAction.CameraUp) || (edgeScroll && mouse.y >= Screen.height - 4)) move += Vector3.forward;
            if (free && KeyBindings.Held(GameAction.CameraDown) || (edgeScroll && mouse.y <= 4)) move += Vector3.back;
            if (free && KeyBindings.Held(GameAction.CameraLeft) || (edgeScroll && mouse.x <= 4)) move += Vector3.left;
            if (free && KeyBindings.Held(GameAction.CameraRight) || (edgeScroll && mouse.x >= Screen.width - 4)) move += Vector3.right;
            if (free && KeyBindings.Held(GameAction.RotateLeft)) yaw -= 70f * delta;
            if (free && KeyBindings.Held(GameAction.RotateRight)) yaw += 70f * delta;
            if (free && KeyBindings.Held(GameAction.TiltUp)) tilt += 40f * delta;
            if (free && KeyBindings.Held(GameAction.TiltDown)) tilt -= 40f * delta;
            if (free && GameInput.GetMouseButton(2))
            {
                var turn = GameInput.MouseAxes;
                yaw += turn.x * 3f;
                tilt -= turn.y * 2f;
            }
            if (free && KeyBindings.Down(GameAction.CameraReset))
            {
                yaw = ObliqueCamera.DefaultYaw;
                tilt = ObliqueCamera.DefaultTilt;
            }
            tilt = ObliqueCamera.ClampTilt(tilt);
            // A larger scenario's map is spread wider; the camera may rise and roam in proportion.
            var spread = (float)World.Profile.Spread;
            if (free && !PointerOverHud()) height = Mathf.Clamp(height - GameInput.MouseScrollDelta.y * height * 0.1f, 20f, 300f * Mathf.Max(1f, spread * 0.85f));

            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var scroll = move.normalized;
            scroll.z *= ObliqueCamera.GroundDepthScale(tilt);
            pivot += rotation * scroll * (height * 0.8f * delta);
            pivot.x = Mathf.Clamp(pivot.x, -520f * spread, 520f * spread);
            pivot.z = Mathf.Clamp(pivot.z, -420f * spread, 420f * spread);

            ObliqueCamera.Apply(strategicCamera, pivot, yaw, tilt, height);
            if (sun != null) sun.transform.rotation = ObliqueCamera.SunRotation(yaw, tilt);
            if (decalAnchor != null) decalAnchor.position = pivot;
        }

        // ---------- Orders ----------

        /// <summary>True while a dialog holds the input: the world and the camera ignore keys and clicks.</summary>
        bool InputHeldByInterface => hud != null && hud.HoldsInput;

        /// <summary>
        /// The single place input is read each frame. Keys come from KeyBindings; the interface's own keys (orders,
        /// Tab, alarm, dialogs) are handled by it from here, so no key is checked in two places.
        /// </summary>
        void HandleInput()
        {
            if (hud != null) hud.HandleKeys();
            if (InputHeldByInterface || KeyBindings.Capturing) { dragging = false; return; }
            if (KeyBindings.Down(GameAction.Pause)) paused = !paused;
            if (KeyBindings.Down(GameAction.Faster) || GameInput.GetKeyDown(KeyCode.KeypadPlus)) SpeedUp();
            if (KeyBindings.Down(GameAction.Slower) || GameInput.GetKeyDown(KeyCode.KeypadMinus)) SpeedDown();
            if (KeyBindings.Down(GameAction.Save)) QuickSave(DefaultSavePath);
            if (KeyBindings.Down(GameAction.Load)) QuickLoad(DefaultSavePath);
            HandleGroups();

            // A targeted order waits for its target: left click places it, right click or Escape cancels it.
            if (PendingOrder != HudOrder.None)
            {
                if (selected.Count == 0 || KeyBindings.Down(GameAction.Cancel) || (GameInput.GetMouseButtonDown(1) && !PointerOverHud())) { PendingOrder = HudOrder.None; return; }
                if (GameInput.GetMouseButtonDown(0) && !PointerOverHud() && TryGround(GameInput.MousePosition, out var target))
                {
                    var order = PendingOrder;
                    var keep = GameInput.GetKey(KeyCode.LeftShift) || GameInput.GetKey(KeyCode.RightShift);
                    if (ExecuteOrder(order, target) && !keep) PendingOrder = HudOrder.None;
                }
                return;
            }
            if (KeyBindings.Down(GameAction.Cancel) && !(hud != null && hud.ConsumedCancel)) ClearSelection();

            if (PointerOverHud()) { if (GameInput.GetMouseButtonUp(0)) dragging = false; return; }
            if (GameInput.GetMouseButtonDown(0)) { dragging = true; dragStart = GameInput.MousePosition; }
            if (GameInput.GetMouseButtonUp(0) && dragging) { dragging = false; FinishSelection(GameInput.MousePosition); }
            if (GameInput.GetMouseButtonDown(1) && TryGround(GameInput.MousePosition, out var point)) OrderAt(point);
        }

        void HandleGroups()
        {
            for (var key = 1; key <= 9; key++)
            {
                if (!GameInput.GetKeyDown(KeyCode.Alpha0 + key)) continue;
                if (GameInput.GetKey(KeyCode.LeftControl) || GameInput.GetKey(KeyCode.RightControl)) { groups[key] = selected.ToArray(); continue; }
                if (!groups.TryGetValue(key, out var ids)) continue;
                selected.Clear();
                foreach (var id in ids) { var ship = World.FindShip(id); if (ship != null && ship.Owner == PlayerFaction) selected.Add(id); }
            }
        }

        bool PointerOverHud() => hud != null && hud.IsPointerOverInterface(GameInput.MousePosition);

        void FinishSelection(Vector2 end)
        {
            var additive = GameInput.GetKey(KeyCode.LeftShift) || GameInput.GetKey(KeyCode.RightShift);
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
            InspectedWreckId = -1;
        }

        /// <summary>Clears the selection without touching the active port; the interface's × button.</summary>
        public void ClearSelectionKeepPort()
        {
            selected.Clear();
            inspectedEnemies.Clear();
            InspectedWreckId = -1;
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
            // A wreck in sight can be inspected for what it still holds.
            var wreck = World.Wrecks.FirstOrDefault(item => World.CanSee(PlayerFaction, item.X, item.Y) && World.Distance(point.x, point.z, item.X, item.Y) < 15);
            if (wreck != null && selected.Count == 0) InspectedWreckId = wreck.Id;
        }

        /// <summary>
        /// What a right click would do here with the current selection, as a cursor name from the interface
        /// package; null for the plain cursor. Only a preview: the order itself is checked again when given.
        /// </summary>
        public string ContextCursor(Vector2 screen)
        {
            if (selected.Count == 0 || !TryGround(screen, out var point)) return null;
            var ships = selected.Select(World.FindShip).Where(ship => ship != null && ship.Owner == PlayerFaction).ToList();
            if (ships.Count == 0) return null;
            var enemy = NearestShip(point, ship => ship.Owner != PlayerFaction);
            if (enemy != null)
            {
                if (ships.All(ship => ship.Class == ShipClass.Ram)) return "ram";
                return ships.Any(ship => World.HasWeapons(ship) || ship.Definition.CanRam) ? "attack" : "blocked";
            }
            var warships = ships.Any(ship => !ship.Definition.IsEconomic);
            var ids = selected.ToArray();
            if (warships && NearestShip(point, ship => ship.Owner == PlayerFaction && ship.Class == ShipClass.Merchant && !ids.Contains(ship.Id)) != null) return "escort";
            if (World.Settlements.Any(item => item.Owner != PlayerFaction && !item.Destroyed && World.CanSee(PlayerFaction, item.X, item.Y) && IsOnSettlement(point, item)))
                return ships.Any(ship => World.HasWeapons(ship)) ? "attack" : "blocked";
            if (ships.Any(ship => ship.Class == ShipClass.Merchant))
            {
                if (World.Sites.Any(item => World.IsSiteFree(item) && World.IsExplored(PlayerFaction, item.X, item.Y)
                    && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, Rules.LandX(item.X), item.Y) < 25))) return "outpost";
                if (World.PortsOf(PlayerFaction).Any(item => IsOnSettlement(point, item))) return "deliver";
            }
            if (ships.Any(ship => ship.Definition.CanHarvest))
            {
                var node = World.Nodes.FirstOrDefault(item => (item.KnownMask & (1 << PlayerFaction)) != 0
                    && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, item.X, item.Y + Rules.SiteLandOffset) < Rules.SiteLandRadius));
                if (node != null) return node.Type == ResourceType.Wood ? "wood" : "silver";
            }
            foreach (var island in World.Islands)
            {
                if (World.Distance(point.x, point.z, island.X, island.Y) > island.Radius) continue;
                if (LandShape.SignedDistance(island, point.x, point.z) < 0) return "blocked";
            }
            return "move";
        }

        /// <summary>The wreck clicked last while it is in sight, or -1.</summary>
        public int InspectedWreckId { get; private set; } = -1;

        /// <summary>How a port is named on the interface: the home port, numbered bridgeheads, the enemy's ports.</summary>
        public string PortName(Settlement settlement)
        {
            if (settlement == null) return string.Empty;
            if (settlement.Owner != PlayerFaction) return settlement.IsOutpost ? "Przyczółek wroga" : "Port wroga";
            if (!settlement.IsOutpost) return "Port macierzysty";
            var number = 1 + World.Settlements.Count(item => item.Owner == PlayerFaction && item.IsOutpost && item.Id < settlement.Id);
            return $"Przyczółek {number}";
        }

        /// <summary>Saved ship groups: number and the ships still in them, for the group chips.</summary>
        public IEnumerable<(int number, int[] ids)> Groups => groups.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Where(id => World.FindShip(id)?.Owner == PlayerFaction).ToArray()));

        public void SelectGroup(int number)
        {
            if (!groups.TryGetValue(number, out var ids)) return;
            selected.Clear();
            inspectedEnemies.Clear();
            foreach (var id in ids) if (World.FindShip(id)?.Owner == PlayerFaction) selected.Add(id);
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

            // Warships ordered onto an own merchant stay with it as its escort. The working ships of a mixed group sail
            // in formation to where the merchant is (a plain move: they do not keep after it), as the rest of a group
            // does whenever only part of it can do the clicked work.
            var warships = ids.Where(id => World.FindShip(id)?.Definition.IsEconomic == false).ToArray();
            var merchants = ids.Where(id => World.FindShip(id)?.Class == ShipClass.Merchant).ToArray();
            var escorted = NearestShip(point, ship => ship.Owner == PlayerFaction && ship.Class == ShipClass.Merchant && !ids.Contains(ship.Id));
            if (escorted != null && warships.Length > 0)
            {
                Send(CommandKind.Escort, warships, target: escorted.Id);
                SendMove(ids.Except(warships).ToArray(), point);
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
                    SendMove(ids.Except(merchants.Take(1)).ToArray(), point);
                    return;
                }

                var ownPort = World.PortsOf(PlayerFaction).FirstOrDefault(item => IsOnSettlement(point, item));
                if (ownPort != null)
                {
                    Send(CommandKind.Transfer, merchants, target: ownPort.Id);
                    SendMove(ids.Except(merchants).ToArray(), point);
                    return;
                }
            }

            var wreck = World.Wrecks.FirstOrDefault(item => World.CanSee(PlayerFaction, item.X, item.Y) && World.Distance(point.x, point.z, item.X, item.Y) < 20);
            if (wreck != null)
            {
                var salvagers = ids.Where(id => World.FindShip(id)?.Definition.CanHarvest == true).ToArray();
                if (salvagers.Length > 0)
                {
                    Send(CommandKind.Salvage, salvagers, target: wreck.Id);
                    SendMove(ids.Except(salvagers).ToArray(), point);
                    return;
                }
            }

            var node = World.Nodes.FirstOrDefault(item => (item.KnownMask & (1 << PlayerFaction)) != 0
                && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, item.X, item.Y + Rules.SiteLandOffset) < Rules.SiteLandRadius));
            if (node != null)
            {
                var cutters = ids.Where(id => World.FindShip(id)?.Definition.CanHarvest == true).ToArray();
                if (cutters.Length > 0)
                {
                    Send(CommandKind.Harvest, cutters, target: node.Id);
                    SendMove(ids.Except(cutters).ToArray(), point);
                    return;
                }
            }
            SendMove(ids, point);
        }

        void OrderAttack(int[] ids, Ship enemy)
        {
            foreach (var id in ids)
            {
                var ship = World.FindShip(id);
                if (ship == null) continue;
                var kind = ship.Class == ShipClass.Ram ? CommandKind.Ram
                    : ship.Class == ShipClass.Boarder && GameInput.GetKey(KeyCode.LeftAlt) == false ? CommandKind.Board
                    : World.HasWeapons(ship) ? CommandKind.AttackShip
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

        /// <summary>
        /// A move order: one ship goes to the point; a group takes its own places there in the chosen formation
        /// (Formation), each ship with an ordinary move command of its own, so the ships no longer crowd onto one point.
        /// </summary>
        void SendMove(int[] ids, Vector3 point)
        {
            if (ids.Length <= 1) { Send(CommandKind.Move, ids, point); return; }
            var ships = ids.Select(World.FindShip).Where(ship => ship != null).ToArray();
            var places = Formation.Targets(World, ships, point.x, point.z, groupFormation);
            for (var i = 0; i < ships.Length; i++)
                World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.Move, ShipIds = new[] { ships[i].Id }, X = places[i].X, Y = places[i].Y });
        }

        public void SelectForTest(int id) => selected.Add(id);

        // ---------- Save ----------

        public void QuickSave(string path)
        {
            var data = SaveGame.Serialize(World, opponent.BuildIndex);
            SaveGame.WriteFile(path, data);
            // The ships' looks go beside the save, tied to it by its hash; the save itself stays the simulation's alone.
            looks.WriteBeside(path, data, World.Ships.Select(ship => ship.Id));
            Notify("Zapisano grę.");
        }

        public bool QuickLoad(string path)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                var world = SaveGame.Deserialize(data, out var build);
                StartMatch(world, build, ShipLookBook.ReadBeside(path, data));
                Notify("Wczytano grę.");
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is EndOfStreamException || exception is UnauthorizedAccessException)
            {
                Notify("Nie można wczytać zapisu: " + exception.Message);
                return false;
            }
        }

        void Notify(string text) => Announce(text, HeraldLevel.Info, null);

        // ---------- Interface ----------

        /// <summary>The battle interface reads this controller and issues orders through it; it never changes the game itself.</summary>
        BattleHud hud;
        public BattleHud Hud => hud;
        public Camera StrategicCamera => strategicCamera;

        /// <summary>A herald's message for the interface: text, weight and the place it concerns.</summary>
        public event Action<HeraldMessage> Herald;
        readonly Dictionary<int, float> lastAlarm = new Dictionary<int, float>();

        void Announce(string text, HeraldLevel level, Vector3? at, HeraldTopic topic = HeraldTopic.General, int entityId = -1, string groupKey = null)
        {
            LastNotice = text;
            Herald?.Invoke(new HeraldMessage(text, level, at, topic, entityId, groupKey));
        }

        /// <summary>
        /// The port a position belongs to, as a grouping key for the herald: the nearest own port within 200 m, else
        /// a 200 m square of open sea, so fights far apart keep their own alarms.
        /// </summary>
        string NearPort(Vector3 at)
        {
            Settlement best = null;
            var bestDistance = double.MaxValue;
            foreach (var port in World.PortsOf(PlayerFaction))
            {
                var distance = World.Distance(at.x, at.z, port.X, port.Y);
                if (distance < bestDistance) { bestDistance = distance; best = port; }
            }
            return best != null && bestDistance < 200 ? $"port{best.Id}" : $"sea{Mathf.FloorToInt(at.x / 200f)}_{Mathf.FloorToInt(at.z / 200f)}";
        }

        /// <summary>An own ship or port under fire raises an alarm, at most once in a while for each of them.</summary>
        void WarnIfOwnUnderAttack(int targetId, Vector3 at)
        {
            var ship = World.FindShip(targetId);
            var settlement = ship == null ? World.FindSettlement(targetId) : null;
            if (ship?.Owner != PlayerFaction && settlement?.Owner != PlayerFaction) return;
            if (lastAlarm.TryGetValue(targetId, out var last) && Time.unscaledTime - last < 15f) return;
            lastAlarm[targetId] = Time.unscaledTime;
            var text = ship != null ? $"Atak na {ClassName(ship.Class).ToLowerInvariant()}!" : settlement.IsOutpost ? "Przyczółek pod ostrzałem!" : "Osada pod ostrzałem!";
            // Escorted transports raise one alarm per convoy; the rest per port waters.
            var convoy = ship != null && ship.Order == OrderKind.Escort ? ship.EscortId : ship != null && ship.Class == ShipClass.Merchant ? ship.Id : -1;
            Announce(text, HeraldLevel.Critical, at, HeraldTopic.Alarm, targetId, convoy >= 0 ? $"alarm:convoy{convoy}" : $"alarm:{NearPort(at)}");
        }

        /// <summary>A targeted order chosen on the order panel, waiting for a left click on the map.</summary>
        public HudOrder PendingOrder { get; set; }

        const string FormationKey = "orders.formation";
        FormationKind groupFormation;

        /// <summary>The formation a group takes when it is sent to one point; the player's choice, kept between games.</summary>
        public FormationKind GroupFormation => groupFormation;

        /// <summary>Sets the group formation; <paramref name="remember"/> keeps it for the next game.</summary>
        public void SetGroupFormation(FormationKind kind, bool remember = true)
        {
            groupFormation = kind;
            if (!remember) return;
            PlayerPrefs.SetString(FormationKey, kind.ToString());
            PlayerPrefs.Save();
        }

        public void ToggleGroupFormation() => SetGroupFormation(groupFormation == FormationKind.Line ? FormationKind.Wedge : FormationKind.Line);

        public bool IsDragging => dragging && (GameInput.MousePosition - (Vector3)dragStart).magnitude >= 6f;

        /// <summary>The selection box in screen pixels, origin at the bottom left.</summary>
        public Rect DragScreenRect
        {
            get
            {
                var end = (Vector2)GameInput.MousePosition;
                return Rect.MinMaxRect(Mathf.Min(dragStart.x, end.x), Mathf.Min(dragStart.y, end.y), Mathf.Max(dragStart.x, end.x), Mathf.Max(dragStart.y, end.y));
            }
        }

        public void FocusCamera(Vector3 point)
        {
            pivot = new Vector3(Mathf.Clamp(point.x, -520f, 520f), 0f, Mathf.Clamp(point.z, -420f, 420f));
        }

        public void TogglePause() => paused = !paused;
        public void SpeedUp() => speedIndex = Mathf.Min(Speeds.Length - 1, speedIndex + 1);
        public void SpeedDown() => speedIndex = Mathf.Max(0, speedIndex - 1);
        public bool CanSpeedUp => speedIndex < Speeds.Length - 1;
        public bool CanSpeedDown => speedIndex > 0;

        public void SelectOnly(int id)
        {
            selected.Clear();
            inspectedEnemies.Clear();
            if (World.FindShip(id)?.Owner == PlayerFaction) selected.Add(id);
        }

        public void Deselect(int id) => selected.Remove(id);

        public void SelectPort(int settlementId)
        {
            var settlement = World.FindSettlement(settlementId);
            if (settlement != null && settlement.Owner == PlayerFaction && !settlement.Destroyed) inspectedSettlementId = settlementId;
        }

        public void QueueShip(ShipClass shipClass, bool unarmed = false)
        {
            var port = ActivePort;
            if (port == null) return;
            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.QueueShip, Class = shipClass, TargetId = port.Id, Unarmed = unarmed });
        }

        /// <summary>
        /// Whether new warships are ordered with their class's default weapons (docs/UZBROJENIE.md); without them a hull
        /// is cheaper and gets its weapons later from the plan and the workshop. Remembered between games.
        /// </summary>
        public bool DefaultArming
        {
            get => PlayerPrefs.GetInt("MorzaBogow.DefaultArming", 1) != 0;
            set => PlayerPrefs.SetInt("MorzaBogow.DefaultArming", value ? 1 : 0);
        }

        public void CancelQueue(bool warLine, int index)
        {
            var port = ActivePort;
            if (port == null) return;
            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.CancelQueue, QueueIndex = index, PortQueue = !warLine, TargetId = port.Id });
        }

        public void BuildShipyard()
        {
            var port = ActivePort;
            if (port == null) return;
            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.BuildShipyard, TargetId = port.Id });
        }

        // ---------- Port buildings, workshop, fitting and blueprints (docs/UZBROJENIE.md) ----------

        public void BuildProject(ProjectKind kind)
        {
            var port = ActivePort;
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.BuildProject, Project = kind, TargetId = port.Id });
        }

        public void CancelProject()
        {
            var port = ActivePort;
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.CancelProject, TargetId = port.Id });
        }

        public void QueueWeapon(WeaponType weapon)
        {
            var port = ActivePort;
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.QueueWeapon, Weapon = weapon, TargetId = port.Id });
        }

        public void CancelWeapon(int index)
        {
            var port = ActivePort;
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.CancelWeapon, QueueIndex = index, TargetId = port.Id });
        }

        /// <summary>Plans one post's weapon; the ship is refitted when it next regroups in port.</summary>
        public void SetWeapon(int shipId, int slot, WeaponType weapon)
        {
            var ship = World.FindShip(shipId);
            if (ship == null || slot < 0 || slot >= ship.Loadout.Length) return;
            var loadout = (WeaponType[])ship.Loadout.Clone();
            loadout[slot] = weapon;
            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.SetLoadout, ShipIds = new[] { shipId }, Loadout = loadout });
        }

        /// <summary>Saves the ship's class, planned weapons and crew as a blueprint under the given name.</summary>
        public void SaveBlueprint(int shipId, string name)
        {
            var ship = World.FindShip(shipId);
            if (ship == null) return;
            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.SaveBlueprint, Class = ship.Class, Loadout = (WeaponType[])ship.Loadout.Clone(), Preset = ship.Plan.Preset, Name = name });
        }

        public void QueueBlueprint(int blueprintId)
        {
            var port = ActivePort;
            if (port != null) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.QueueShip, BlueprintId = blueprintId, TargetId = port.Id });
        }

        public static string ProjectName(ProjectKind kind) =>
            kind == ProjectKind.Workshop ? "Warsztat (arsenał)" : kind == ProjectKind.Town ? "Awans na Miasto"
            : kind == ProjectKind.SageSchool ? "Szkoła Mędrców" : kind == ProjectKind.HeavyArmsResearch ? "Badanie: broń ciężka"
            : kind == ProjectKind.Dock ? "Dok (ulepszenie stoczni)" : "—";

        /// <summary>Sets the crew a ship's port will train it to; the people change only when it regroups in port.</summary>
        public void SetCrewPlan(int shipId, CrewPreset preset) =>
            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.SetCrewPlan, ShipIds = new[] { shipId }, Preset = preset });

        /// <summary>The boarding this ship is part of, attacking or defending, if any.</summary>
        public BoardingLink BoardingOf(int shipId) => World.Boardings.FirstOrDefault(link => link.AttackerId == shipId || link.TargetId == shipId);

        public static string PresetName(CrewPreset preset) =>
            preset == CrewPreset.Boarding ? "Abordaż" : preset == CrewPreset.Volley ? "Ostrzał" : preset == CrewPreset.Seamen ? "Żegluga" : preset == CrewPreset.Custom ? "Własny" : "Wyważony";

        public void StopSelected() => Send(CommandKind.Stop, selected.ToArray());
        public void RepairSelected() => Send(CommandKind.Repair, selected.ToArray());
        public void ScrapSelected() => Send(CommandKind.Scrap, selected.ToArray());

        /// <summary>A new skirmish of the given length, or of the current one's length.</summary>
        public void NewGame(MatchLength? length = null)
        {
            StartMatch(MapFactory.CreateSkirmish(seed + (ulong)Time.frameCount, length ?? World.Length), 0);
        }

        /// <summary>-morzaLength Short|Standard|Long picks the scenario length at start; Standard otherwise.</summary>
        public static MatchLength LengthFromArguments(string[] arguments)
        {
            var index = Array.IndexOf(arguments, "-morzaLength");
            if (index >= 0 && index + 1 < arguments.Length && Enum.TryParse<MatchLength>(arguments[index + 1], true, out var length)) return length;
            return MatchLength.Standard;
        }

        /// <summary>
        /// Carries out a targeted order at a point on the map, the same way the context order does but for one
        /// chosen kind. False when nothing suitable lies there or no selected ship can do it.
        /// </summary>
        public bool ExecuteOrder(HudOrder order, Vector3 point)
        {
            var ships = selected.Select(World.FindShip).Where(ship => ship != null && ship.Owner == PlayerFaction).ToList();
            if (ships.Count == 0) return false;
            int[] Ids(Func<Ship, bool> able) => ships.Where(able).Select(ship => ship.Id).ToArray();
            var enemy = NearestShip(point, ship => ship.Owner != PlayerFaction);
            var done = false;

            switch (order)
            {
                case HudOrder.Move:
                    SendMove(Ids(_ => true), point);
                    done = true;
                    break;
                case HudOrder.Attack:
                {
                    var archers = Ids(ship => World.HasWeapons(ship));
                    if (enemy != null && archers.Length > 0) { Send(CommandKind.AttackShip, archers, target: enemy.Id); done = true; break; }
                    var port = World.Settlements.FirstOrDefault(item => item.Owner != PlayerFaction && !item.Destroyed && World.CanSee(PlayerFaction, item.X, item.Y) && IsOnSettlement(point, item));
                    if (port != null && archers.Length > 0) { Send(CommandKind.AttackSettlement, archers, target: port.Id); done = true; }
                    break;
                }
                case HudOrder.Ram:
                {
                    var rams = Ids(ship => ship.Definition.CanRam);
                    if (enemy != null && rams.Length > 0) { Send(CommandKind.Ram, rams, target: enemy.Id); done = true; }
                    break;
                }
                case HudOrder.Board:
                {
                    var boarders = Ids(ship => ship.Class == ShipClass.Boarder);
                    if (enemy != null && boarders.Length > 0) { Send(CommandKind.Board, boarders, target: enemy.Id); done = true; }
                    break;
                }
                case HudOrder.Volley:
                {
                    var boarders = Ids(ship => ship.Class == ShipClass.Boarder && World.HasWeapons(ship));
                    if (enemy != null && boarders.Length > 0) { Send(CommandKind.AttackShip, boarders, target: enemy.Id); done = true; }
                    break;
                }
                case HudOrder.Harvest:
                {
                    var cutters = Ids(ship => ship.Definition.CanHarvest);
                    if (cutters.Length == 0) break;
                    var wreck = World.Wrecks.FirstOrDefault(item => World.CanSee(PlayerFaction, item.X, item.Y) && World.Distance(point.x, point.z, item.X, item.Y) < 20);
                    if (wreck != null) { Send(CommandKind.Salvage, cutters, target: wreck.Id); done = true; break; }
                    var node = World.Nodes.FirstOrDefault(item => (item.KnownMask & (1 << PlayerFaction)) != 0
                        && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, item.X, item.Y + Rules.SiteLandOffset) < Rules.SiteLandRadius));
                    if (node != null) { Send(CommandKind.Harvest, cutters, target: node.Id); done = true; }
                    break;
                }
                case HudOrder.Deliver:
                {
                    var merchants = Ids(ship => ship.Class == ShipClass.Merchant);
                    var port = World.PortsOf(PlayerFaction).FirstOrDefault(item => IsOnSettlement(point, item));
                    if (port != null && merchants.Length > 0) { Send(CommandKind.Transfer, merchants, target: port.Id); done = true; }
                    break;
                }
                case HudOrder.Outpost:
                {
                    var merchant = Ids(ship => ship.Class == ShipClass.Merchant).Take(1).ToArray();
                    var shore = World.Sites.FirstOrDefault(item => World.IsSiteFree(item) && World.IsExplored(PlayerFaction, item.X, item.Y)
                        && (World.Distance(point.x, point.z, item.X, item.Y) < 25 || World.Distance(point.x, point.z, Rules.LandX(item.X), item.Y) < 25));
                    if (shore != null && merchant.Length > 0) { Send(CommandKind.FoundOutpost, merchant, target: shore.Id); done = true; }
                    break;
                }
                case HudOrder.Escort:
                {
                    var warships = Ids(ship => !ship.Definition.IsEconomic);
                    var ids = selected.ToArray();
                    var escorted = NearestShip(point, ship => ship.Owner == PlayerFaction && ship.Class == ShipClass.Merchant && !ids.Contains(ship.Id));
                    if (escorted != null && warships.Length > 0) { Send(CommandKind.Escort, warships, target: escorted.Id); done = true; }
                    break;
                }
            }

            if (done) effects.Ping(point);
            else Announce("Tu nie ma właściwego celu dla tego rozkazu.", HeraldLevel.Info, point);
            return done;
        }

        /// <summary>Seconds left to finish a build order at the game's pace.</summary>
        public static float SecondsLeft(BuildOrder order) => order == null ? 0f : (order.TotalTicks - order.DoneTicks) / (float)Rules.TickRate;

        /// <summary>The stage a hull on the slipway has reached, as named on the interface.</summary>
        public static string StageName(float progress)
        {
            if (progress < 0.1f) return "stępka";
            if (progress < 0.35f) return "wręgi";
            if (progress < 0.7f) return "poszycie";
            if (progress < 0.85f) return "pokład";
            if (progress < 0.95f) return "maszt";
            return "wiosła i żagiel";
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
                case OrderKind.Repair: return World.RepairYard(ship)?.HasDock == true ? "Rozkaz: remont w doku." : "Rozkaz: przegrupowanie w porcie.";
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

        public static string DescribeCargo(Ship ship)
        {
            if (ship.HoldWood + ship.HoldSilver > 0) return $"{ship.HoldWood / Rules.Centi} D i {ship.HoldSilver / Rules.Centi} S";
            return (ship.Cargo / Rules.Centi).ToString();
        }

        public static string ClassName(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "Kuter";
                case ShipClass.Merchant: return "Handlowiec";
                case ShipClass.Ram: return "Taranowiec";
                case ShipClass.CrewDestroyer: return "Niszczyciel załóg";
                case ShipClass.ShipKiller: return "Niszczyciel okrętów";
                case ShipClass.Bomber: return "Bombowiec";
                default: return "Abordażowiec";
            }
        }

        // ---------- Build smoke test ----------

        void UpdateSmoke()
        {
            smokeElapsed += Time.unscaledDeltaTime;
            if (smokeElapsed > 5f) { smokeFrames++; smokeFrameTime += Time.unscaledDeltaTime; smokeWorstFrame = Mathf.Max(smokeWorstFrame, Time.unscaledDeltaTime); }
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
                                ?? World.Ships.FirstOrDefault(ship => ship.Owner == PlayerFaction && World.HasWeapons(ship))
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
            report.Append($"  \"shipLooks\": \"{(plainShips ? "plain" : ShipLookLibrary.Aegean)}\",\n  \"lookRenderers\": {views.Values.Sum(view => view != null ? view.LookRenderers.Count : 0)},\n  \"lookMaterials\": {ShipPaints.PaletteSize},\n");
            report.Append($"  \"coastPrefabLoaded\": {(coastPrefab != null ? "true" : "false")},\n  \"coast\": \"{coastScaleNote}\",\n");
            report.Append($"  \"profile\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\n  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            report.Append($"  \"wood\": [{World.Settlements[0].Wood / Rules.Centi}, {World.Settlements[1].Wood / Rules.Centi}],\n");
            report.Append($"  \"silver\": [{World.Settlements[0].Silver / Rules.Centi}, {World.Settlements[1].Silver / Rules.Centi}],\n");
            report.Append($"  \"firstEnemySeenSeconds\": {firstEnemySeenSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},\n");
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var shipPaint = ShipPaints.PaintBase;
            report.Append($"  \"lookShader\": \"{(shipPaint != null ? shipPaint.shader.name : "none")}\",\n  \"lookShaderSupported\": {(shipPaint != null && shipPaint.shader.isSupported ? "true" : "false")},\n");
            report.Append($"  \"outposts\": {World.Settlements.Count(item => item.IsOutpost && !item.Destroyed)},\n  \"outpostShipyards\": {World.Settlements.Count(item => item.IsOutpost && !item.Destroyed && item.HasShipyard)},\n  \"merchants\": {World.Ships.Count(item => item.Class == ShipClass.Merchant)},\n");
            report.Append($"  \"waterSamples\": {(water != null ? water.Samples : 0)},\n  \"waterFailures\": {(water != null ? water.Failures : 0)},\n");
            report.Append($"  \"waterMs\": {Ms(smokeWaterMs, smokeWaterFrames)},\n  \"waterMostHulls\": {(water != null ? water.MostHulls : 0)},\n");
            report.Append($"  \"waterBufferAllocations\": {(waterHeights != null ? waterHeights.BufferAllocations : 0)},\n");
            report.Append($"  \"maxTiltDegrees\": {maxRollSeen.ToString("0.00", culture)},\n  \"maxHeaveMetres\": {maxHeaveSeen.ToString("0.00", culture)},\n");
            if (World.Marine != null)
            {
                // Wind and sea at the view, cost of the hull motion (per tick) and of drawing the waves (per frame, CPU).
                var marine = World.Marine;
                var cell = marine.Waves.Grid.CellAt(pivot.x, pivot.z);
                var wind = marine.Wind.Sample(pivot.x, pivot.z, false);
                report.Append($"  \"marineClimate\": \"{marine.Climate.Name}\",\n  \"marineWindMs\": {wind.MeanSpeed.ToString("0.00", culture)},\n");
                report.Append($"  \"marineHs\": {(cell >= 0 ? marine.Waves.SignificantHeight(cell) : 0).ToString("0.00", culture)},\n  \"marineAmplitude\": {(seaRenderer != null ? seaRenderer.Amplitude : 0f).ToString("0.00", culture)},\n");
                report.Append($"  \"marineTickMs\": {Ms(marine.TickMilliseconds, marine.TimedTicks)},\n");
                report.Append($"  \"seakeepingMs\": {Ms(smokeSeakeepingMs, smokeSeakeepingSteps)},\n  \"seakeepingShips\": {seakeeping?.Ships ?? 0},\n  \"seaRenderCpuMs\": {Ms(smokeSeaMs, smokeSeaFrames)},\n");
                // Share of the followed hulls stepped in an average tick: 1 close up, 1/FarStride far away.
                report.Append($"  \"seakeepingStepShare\": {(smokeSeakeepingFollowed > 0 ? smokeSeakeepingStepped / smokeSeakeepingFollowed : 0).ToString("0.000", culture)},\n");
                report.Append($"  \"seaCorrelation\": {SeaCorrelation().ToString("0.000", culture)},\n  \"seaComparisons\": {seaCompareN.ToString("0", culture)},\n");
                // Rendered against simulated wave heights (1: the decal draws them at their true size).
                var spreadSim = seaCompareN > 1 ? Math.Sqrt(Math.Max(0, seaCompareXX / seaCompareN - Math.Pow(seaCompareX / seaCompareN, 2))) : 0;
                var spreadRendered = seaCompareN > 1 ? Math.Sqrt(Math.Max(0, seaCompareYY / seaCompareN - Math.Pow(seaCompareY / seaCompareN, 2))) : 0;
                report.Append($"  \"seaHeightRatio\": {(spreadSim > 0 ? spreadRendered / spreadSim : 0).ToString("0.000", culture)},\n");
                // Water against each hull's shown waterline (m): mean, RMS, highest, share above the deck.
                var meanAbove = hullWaterN > 0 ? hullWaterSum / hullWaterN : 0;
                report.Append($"  \"hullWaterMean\": {meanAbove.ToString("0.000", culture)},\n  \"hullWaterRms\": {(hullWaterN > 0 ? Math.Sqrt(hullWaterSumSq / hullWaterN) : 0).ToString("0.000", culture)},\n");
                report.Append($"  \"hullWaterMax\": {hullWaterMax.ToString("0.000", culture)},\n  \"hullDeckAwashShare\": {(hullWaterN > 0 ? hullDeckAwash / hullWaterN : 0).ToString("0.0000", culture)},\n  \"hullWaterSamples\": {hullWaterN.ToString("0", culture)},\n");
            }
            report.Append($"  \"combatEvents\": {combatEvents},\n");
            report.Append($"  \"boulders\": {BouldersPlaced},\n");
            report.Append($"  \"averageFrameMs\": {(smokeFrames > 0 ? smokeFrameTime / smokeFrames * 1000f : 0f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},\n");
            string Ms(double total, int count) => (count > 0 ? total / count : 0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            report.Append($"  \"worstFrameMs\": {(smokeWorstFrame * 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},\n");
            report.Append($"  \"simulationMs\": {Ms(smokeSimMs, smokeFrames)},\n");
            report.Append($"  \"presentationMs\": {Ms(smokePresentMs, smokeFrames)},\n");
            report.Append($"  \"interfaceMs\": {(hud != null ? hud.AverageUpdateMs : 0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},\n");
            report.Append($"  \"cpuMainMs\": {Ms(smokeCpuMs, smokeTimed)},\n");
            report.Append($"  \"cpuRenderMs\": {Ms(smokeRenderMs, smokeTimed)},\n");
            report.Append($"  \"gpuMs\": {Ms(smokeGpuMs, smokeTimed)},\n");
            report.Append($"  \"vSyncCount\": {QualitySettings.vSyncCount},\n");
            report.Append($"  \"qualityLevel\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\"\n}}\n");
            File.WriteAllText(smokeReport, report.ToString());
            smokeReport = null;
        }
    }
}
