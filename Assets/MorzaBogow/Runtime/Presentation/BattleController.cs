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
        readonly HashSet<int> selected = new HashSet<int>();
        readonly Dictionary<int, int[]> groups = new Dictionary<int, int[]>();
        readonly List<GameObject> mapObjects = new List<GameObject>();

        Opponent opponent;
        Opponent autoPlayer;
        double accumulator;
        int speedIndex = 1;
        bool paused;
        string notice = string.Empty;
        float noticeUntil;
        Vector3 pivot;
        float yaw;
        float height = 130f;
        bool dragging;
        Vector2 dragStart;
        string smokeReport;
        float smokeSeconds;
        float smokeElapsed;
        int smokeMaxVisibleEnemies;
        string coastScaleNote = "not used";

        public World World { get; private set; }
        public IReadOnlyCollection<int> Selected => selected;
        public IReadOnlyDictionary<int, ShipVisual> Views => views;
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
            StartMatch(MapFactory.CreateSkirmish(seed), 0);
            ReadSmokeArguments();
        }

        void StartMatch(World world, int opponentBuildIndex)
        {
            ClearViews();
            World = world;
            opponent = new Opponent(1) { BuildIndex = opponentBuildIndex };
            BuildStaticViews();
            SyncViews();

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
            World.Step();
            World.Events.Clear();
            SyncViews();
        }

        /// <summary>Advances whole simulation ticks; used by tests and tools.</summary>
        public void RunTicks(int ticks)
        {
            for (var i = 0; i < ticks; i++) StepOnce();
            PresentFrame();
        }

        // ---------- Views ----------

        void ClearViews()
        {
            foreach (var view in views.Values) if (view != null) Destroy(view.gameObject);
            foreach (var item in mapObjects) if (item != null) Destroy(item);
            views.Clear();
            viewOwners.Clear();
            previous.Clear();
            nodeViews.Clear();
            settlementViews.Clear();
            mapObjects.Clear();
            selected.Clear();
        }

        void BuildStaticViews()
        {
            foreach (var island in World.Islands) mapObjects.Add(CreateIsland(island));

            foreach (var settlement in World.Settlements)
            {
                var view = Primitive(PrimitiveType.Cube, $"Settlement {settlement.Owner} [PLACEHOLDER]", materials?.Neutral);
                view.transform.position = new Vector3((float)settlement.X, 3f, (float)settlement.Y);
                view.transform.localScale = new Vector3(28f, 6f, 28f);
                settlementViews[settlement.Id] = view;
                mapObjects.Add(view);
            }

            foreach (var node in World.Nodes)
            {
                var material = node.Type == ResourceType.Bronze ? materials?.BronzeNode : materials?.RationsNode;
                var view = Primitive(PrimitiveType.Cylinder, $"{node.Type} node {node.Id} [PLACEHOLDER]", material);
                view.transform.position = new Vector3((float)node.X, 0.3f, (float)node.Y);
                view.transform.localScale = new Vector3(40f, 0.15f, 40f);
                nodeViews[node.Id] = view;
                mapObjects.Add(view);
            }
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
            var scale = (float)island.Radius * 2f / Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            coast.transform.localScale *= scale;
            bounds = WorldBounds(coast);
            coast.transform.position += center - new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.15f, bounds.center.z);
            coastScaleNote = $"unscaled footprint scale factor {scale:0.##}, placed size {bounds.size.x:0.#} x {bounds.size.y:0.#} x {bounds.size.z:0.#} m";

            foreach (var blocker in coast.GetComponentsInChildren<Collider>()) blocker.enabled = false;
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
                Destroy(views[id].gameObject);
                views.Remove(id);
                viewOwners.Remove(id);
                previous.Remove(id);
                selected.Remove(id);
            }
        }

        void PresentFrame()
        {
            var blend = (float)(accumulator / Rules.Dt);
            foreach (var ship in World.Ships)
            {
                if (!views.TryGetValue(ship.Id, out var view)) continue;

                // Hidden ships are fully inactive, so they cast no shadow or reflection and reveal nothing.
                var visible = World.CanSee(PlayerFaction, ship);
                if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
                if (!visible) continue;

                var current = ToWorld(ship);
                view.transform.position = previous.TryGetValue(ship.Id, out var last) ? Vector3.Lerp(last, current, blend) : current;
                view.transform.rotation = Quaternion.Euler(0f, 90f - (float)(ship.Heading * Mathf.Rad2Deg), 0f);
                view.Selection.SetActive(selected.Contains(ship.Id));
            }

            foreach (var node in World.Nodes)
            {
                if (nodeViews.TryGetValue(node.Id, out var view))
                {
                    var known = (node.KnownMask & (1 << PlayerFaction)) != 0;
                    if (view.activeSelf != known) view.SetActive(known);
                }
            }

            foreach (var settlement in World.Settlements)
            {
                if (!settlementViews.TryGetValue(settlement.Id, out var view)) continue;
                var shown = !settlement.Destroyed && (settlement.Owner == PlayerFaction || World.CanSee(PlayerFaction, settlement.X, settlement.Y));
                if (view.activeSelf != shown) view.SetActive(shown);
            }
        }

        static Vector3 ToWorld(Ship ship) => new Vector3((float)ship.X, 0f, (float)ship.Y);

        // ---------- Camera (C10) ----------

        void UpdateCamera()
        {
            if (strategicCamera == null) return;
            var delta = Time.unscaledDeltaTime;
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.mousePosition.y >= Screen.height - 4) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || Input.mousePosition.y <= 4) move += Vector3.back;
            if (Input.GetKey(KeyCode.A) || Input.mousePosition.x <= 4) move += Vector3.left;
            if (Input.GetKey(KeyCode.D) || Input.mousePosition.x >= Screen.width - 4) move += Vector3.right;
            if (Input.GetKey(KeyCode.Q)) yaw -= 70f * delta;
            if (Input.GetKey(KeyCode.E)) yaw += 70f * delta;
            height = Mathf.Clamp(height - Input.mouseScrollDelta.y * height * 0.1f, 20f, 300f);

            var rotation = Quaternion.Euler(0f, yaw, 0f);
            pivot += rotation * move.normalized * (height * 0.8f * delta);
            pivot.x = Mathf.Clamp(pivot.x, -750f, 750f);
            pivot.z = Mathf.Clamp(pivot.z, -750f, 750f);

            var view = rotation * Quaternion.Euler(52f, 0f, 0f);
            strategicCamera.transform.SetPositionAndRotation(pivot - view * Vector3.forward * (height / Mathf.Sin(52f * Mathf.Deg2Rad)), view);
        }

        // ---------- Orders ----------

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) speedIndex = Mathf.Min(Speeds.Length - 1, speedIndex + 1);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) speedIndex = Mathf.Max(0, speedIndex - 1);
            if (Input.GetKeyDown(KeyCode.Escape)) selected.Clear();
            if (Input.GetKeyDown(KeyCode.F5)) QuickSave(DefaultSavePath);
            if (Input.GetKeyDown(KeyCode.F8)) QuickLoad(DefaultSavePath);
            HandleGroups();

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

        bool PointerOverHud() => Input.mousePosition.y < 150f;

        void FinishSelection(Vector2 end)
        {
            var additive = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!additive) selected.Clear();
            if (strategicCamera == null) return;

            if ((end - dragStart).magnitude < 6f)
            {
                if (TryGround(end, out var point))
                {
                    var ship = NearestShip(point, item => item.Owner == PlayerFaction);
                    if (ship != null) selected.Add(ship.Id);
                }
                return;
            }

            var box = Rect.MinMaxRect(Mathf.Min(dragStart.x, end.x), Mathf.Min(dragStart.y, end.y), Mathf.Max(dragStart.x, end.x), Mathf.Max(dragStart.y, end.y));
            foreach (var ship in World.Ships.Where(item => item.Owner == PlayerFaction))
            {
                var screen = strategicCamera.WorldToScreenPoint(ToWorld(ship));
                if (screen.z > 0f && box.Contains(screen)) selected.Add(ship.Id);
            }
        }

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

            var enemy = NearestShip(point, ship => ship.Owner != PlayerFaction);
            if (enemy != null) { OrderAttack(ids, enemy); return; }

            var enemyHome = World.SettlementOf(1 - PlayerFaction);
            if (enemyHome != null && World.CanSee(PlayerFaction, enemyHome.X, enemyHome.Y) && World.Distance(point.x, point.z, enemyHome.X, enemyHome.Y) < 25)
            {
                Send(CommandKind.AttackSettlement, ids, target: enemyHome.Id);
                return;
            }

            var node = World.Nodes.FirstOrDefault(item => (item.KnownMask & (1 << PlayerFaction)) != 0 && World.Distance(point.x, point.z, item.X, item.Y) < 25);
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
            var settlement = World.SettlementOf(PlayerFaction);
            if (settlement != null)
            {
                var transit = World.Ships.Where(ship => ship.Owner == PlayerFaction && ship.Cargo > 0).ToList();
                var bronzeTransit = transit.Where(ship => ship.CargoType == ResourceType.Bronze).Sum(ship => ship.Cargo) / Rules.Centi;
                var rationsTransit = transit.Where(ship => ship.CargoType == ResourceType.Rations).Sum(ship => ship.Cargo) / Rules.Centi;
                var fleet = World.Ships.Count(ship => ship.Owner == PlayerFaction);
                GUI.Box(new Rect(0, 0, Screen.width, 26), string.Empty);
                GUI.Label(new Rect(10, 3, Screen.width - 20, 24),
                    $"Brąz {settlement.Bronze / Rules.Centi} (w drodze {bronzeTransit})   Racje {settlement.Rations / Rules.Centi} (w drodze {rationsTransit})   Flota {fleet}/{Rules.FleetLimit}   Osada {Mathf.CeilToInt((float)settlement.Hull)} HP   " +
                    $"Prędkość {Speed:0.#}x{(paused ? "  PAUZA" : string.Empty)}   Czas {World.Tick / Rules.TickRate / 60}:{World.Tick / Rules.TickRate % 60:00}");

                GUILayout.BeginArea(new Rect(0, Screen.height - 150, 380, 150), GUI.skin.box);
                GUILayout.Label("Stocznia (cena z bronią i pełną załogą)");
                foreach (ShipClass shipClass in Enum.GetValues(typeof(ShipClass)))
                {
                    var label = $"{ClassName(shipClass)}: {World.HullCostBronze(shipClass) / Rules.Centi} B / {World.HullCostRations(shipClass) / Rules.Centi} R";
                    if (GUILayout.Button(label)) World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.QueueShip, Class = shipClass });
                }
                GUILayout.EndArea();

                if (settlement.Queue.Count > 0)
                {
                    GUILayout.BeginArea(new Rect(385, Screen.height - 150, 250, 150), GUI.skin.box);
                    GUILayout.Label("Kolejka");
                    for (var i = 0; i < settlement.Queue.Count; i++)
                    {
                        var order = settlement.Queue[i];
                        if (GUILayout.Button($"{ClassName(order.Class)} {100 * order.DoneTicks / order.TotalTicks}% (anuluj)"))
                            World.Issue(new Command { Faction = PlayerFaction, Kind = CommandKind.CancelQueue, QueueIndex = i });
                    }
                    GUILayout.EndArea();
                }
            }

            GUILayout.BeginArea(new Rect(640, Screen.height - 150, Mathf.Max(320, Screen.width - 640), 150), GUI.skin.box);
            if (selected.Count == 0) GUILayout.Label("LPM: zaznacz. PPM: rozkaz. Spacja: pauza. +/-: prędkość. F5/F8: zapis/wczytanie. Alt+PPM: ostrzał abordażowcem.");
            foreach (var id in selected.Take(5))
            {
                var ship = World.FindShip(id);
                if (ship == null) continue;
                GUILayout.Label($"{ClassName(ship.Class)}: kadłub {Mathf.CeilToInt((float)ship.Hull)}/{ship.Definition.Hull}, marynarze {ship.Sailors}, żołnierze {ship.Soldiers}, morale {Mathf.RoundToInt((float)ship.Morale)}, ładunek {ship.Cargo / Rules.Centi}, rozkaz {ship.Order}");
            }
            if (selected.Count > 5) GUILayout.Label($"oraz {selected.Count - 5} kolejnych");
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

        static string ClassName(ShipClass shipClass)
        {
            switch (shipClass)
            {
                case ShipClass.Cutter: return "Kuter";
                case ShipClass.Ram: return "Taranowiec";
                case ShipClass.CrewDestroyer: return "Niszczyciel załóg";
                default: return "Abordażowiec";
            }
        }

        // ---------- Build smoke test ----------

        void UpdateSmoke()
        {
            smokeElapsed += Time.unscaledDeltaTime;
            smokeMaxVisibleEnemies = Mathf.Max(smokeMaxVisibleEnemies, World.Ships.Count(ship => ship.Owner != PlayerFaction && World.CanSee(PlayerFaction, ship)));
            if (smokeElapsed < smokeSeconds && World.Outcome == OutcomeKind.InProgress) return;

            var report = new StringBuilder("{\n");
            report.Append($"  \"tick\": {World.Tick},\n  \"outcome\": \"{World.Outcome}\",\n  \"winner\": {World.Winner},\n");
            report.Append($"  \"ships\": {World.Ships.Count},\n  \"views\": {views.Count},\n  \"maxVisibleEnemies\": {smokeMaxVisibleEnemies},\n");
            report.Append($"  \"coastPrefabLoaded\": {(coastPrefab != null ? "true" : "false")},\n  \"coast\": \"{coastScaleNote}\",\n");
            report.Append($"  \"profile\": \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\n  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            report.Append($"  \"bronze\": [{World.Settlements[0].Bronze / Rules.Centi}, {World.Settlements[1].Bronze / Rules.Centi}],\n");
            report.Append($"  \"rations\": [{World.Settlements[0].Rations / Rules.Centi}, {World.Settlements[1].Rations / Rules.Centi}]\n}}\n");
            File.WriteAllText(smokeReport, report.ToString());
            smokeReport = null;
            Application.Quit(0);
        }
    }
}
