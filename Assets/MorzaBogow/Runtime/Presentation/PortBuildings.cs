using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>What a port shows on its land (docs/UZBROJENIE.md, 3c part 2).</summary>
    public enum PortBuilding { Store, Workshop, Town, SageSchool, Dock }

    /// <summary>Where a man building or mending would stand at a port, and the point he faces (for CrewAshore's ShoreMen later).</summary>
    public struct PortWorkSpot
    {
        public Vector3 Position;
        public Vector3 Facing;
    }

    /// <summary>
    /// The buildings on a settlement's land, read from the simulation each frame and never feeding back:
    /// <list type="bullet">
    /// <item>always a stone store with a house or two (a bridgehead: a smaller store and a hut);</item>
    /// <item>the workshop (HasWorkshop): an open shed on posts by the war line, a forge chimney, timber and a machine frame;</item>
    /// <item>the town (IsTown): houses on terraces up the hill and a wall with towers round the land, its gate toward the harbour;</item>
    /// <item>the Sages' School (HasSageSchool): a whitewashed colonnade under a pediment on the highest terrace;</item>
    /// <item>the dock (HasDock): shear legs at the end of the jetty, where ships lie for repair, and a windlass.</item>
    /// </list>
    /// A project rising (Settlement.Project) shows its building at the stage reached, by the slipway's thresholds: footing
    /// and stakes, walls growing among scaffolding, the roof from 80%, the scaffolding down at 97%. Research raises nothing:
    /// a fire burns on a tripod before the School. A destroyed settlement keeps its land and shows the ruins of what stood:
    /// broken walls, charred beams, rubble, the shear legs fallen.
    ///
    /// Everything stands on the land's landward half (x toward the port between -14.5 and 1 m, |z| below 9.5 m, the plot the
    /// trees keep clear of), stepping up the hill in terraces, except the dock's gear by the jetty, off its middle where the
    /// port's men walk. The slipways, the jetty and the store door the dock gangs use stay clear.
    /// </summary>
    public sealed class PortBuildings
    {
        /// <summary>The plot on the land's landward half, as distance toward the port (u) and across (z).</summary>
        public const float PlotNear = 1f, PlotFar = -14.5f, PlotHalfWidth = 9.5f;

        /// <summary>The shares of a project at which its footing gives way to walls, its roof goes on and its scaffolding comes down.</summary>
        public const float WallsFrom = 0.15f, RoofFrom = 0.8f, ScaffoldUntil = 0.97f;

        /// <summary>One shape put up as a unit: its walls, its roof, the scaffolding and footing while it rises.</summary>
        sealed class Piece
        {
            public Transform Root, Body, Roof, Scaffold, Footing;
            /// <summary>The piece's footprint on the land: centre (u, z) and size (along u, along z).</summary>
            public float U, Z, W, D, H;
        }

        sealed class Structure
        {
            public PortBuilding Kind;
            public GameObject Root;
            public readonly List<Piece> Pieces = new List<Piece>();
            public GameObject Ruin;
        }

        readonly Transform root;
        readonly PlaceholderMaterials materials;
        readonly float toward;
        readonly float[] outline, lift;
        readonly bool outpost;
        readonly int seed;
        readonly Dictionary<PortBuilding, Structure> structures = new Dictionary<PortBuilding, Structure>();
        readonly List<Mesh> owned = new List<Mesh>();
        readonly List<PortWorkSpot> workSpots = new List<PortWorkSpot>();
        Transform fire, flame;
        long state = long.MinValue;
        float flicker;

        public bool Ruined { get; private set; }
        public PortBuilding? Rising { get; private set; }
        public float RisingProgress { get; private set; }
        public bool Scaffolded { get; private set; }
        public bool RoofOn { get; private set; }
        public bool ResearchFire => fire != null && fire.gameObject.activeSelf;

        /// <summary>Where builders would work on the rising building, or menders at the dock; empty when nothing is done.</summary>
        public IReadOnlyList<PortWorkSpot> WorkSpots => workSpots;

        /// <param name="settlementRoot">The settlement's root at the land's centre.</param>
        /// <param name="toward">+1 or -1: the side of the land facing its port.</param>
        /// <param name="outline">The land's waterline and its cliffs, as the islet is drawn (ShoreShape).</param>
        public PortBuildings(Transform settlementRoot, PlaceholderMaterials materials, Settlement settlement, float toward, float[] outline, float[] lift)
        {
            this.materials = materials;
            this.toward = toward;
            this.outline = outline;
            this.lift = lift;
            outpost = settlement.IsOutpost;
            seed = settlement.Id * 7919 + 17;
            root = new GameObject("Buildings").transform;
            root.SetParent(settlementRoot, false);
            root.gameObject.AddComponent<OwnedMeshes>().Meshes = owned;
            Get(PortBuilding.Store).Root.SetActive(true);
        }

        public Transform Root => root;

        /// <summary>The building stands whole (not rising, not in ruins).</summary>
        public bool Shows(PortBuilding kind) => !Ruined && structures.TryGetValue(kind, out var item) && item.Root.activeSelf && Rising != kind;

        public bool ShowsRuin(PortBuilding kind) => structures.TryGetValue(kind, out var item) && item.Ruin != null && item.Ruin.activeSelf;

        /// <summary>
        /// Where a building stands on the land: the box round its pieces in the settlement root's space (x across the
        /// land toward the port, z along the shore). Empty for a building never raised here.
        /// </summary>
        public Bounds Footprint(PortBuilding kind)
        {
            if (!structures.TryGetValue(kind, out var item) || item.Pieces.Count == 0) return default;
            var box = new Bounds();
            var first = true;
            foreach (var piece in item.Pieces)
            {
                var part = new Bounds(new Vector3(toward * piece.U, piece.Root.localPosition.y + piece.H * 0.5f, piece.Z), new Vector3(piece.W, piece.H, piece.D));
                if (first) box = part; else box.Encapsulate(part);
                first = false;
            }
            return box;
        }

        /// <summary>Reads the settlement and shows what stands, rises or lies in ruins; changes the scene only when that changes.</summary>
        public void Update(Settlement settlement, bool ruined, float delta)
        {
            var project = settlement.Project != null ? settlement.ProjectKind : ProjectKind.None;
            var progress = project != ProjectKind.None && settlement.Project.TotalTicks > 0 ? Mathf.Clamp01((float)settlement.Project.DoneTicks / settlement.Project.TotalTicks) : 0f;
            var key = (settlement.HasWorkshop ? 1L : 0) | (settlement.IsTown ? 2L : 0) | (settlement.HasSageSchool ? 4L : 0) | (settlement.HasDock ? 8L : 0)
                      | (ruined ? 16L : 0) | ((long)project << 5) | ((long)Mathf.RoundToInt(progress * 1000f) << 9);
            if (key != state)
            {
                state = key;
                Apply(settlement, ruined, project, progress);
            }

            // The research fire flickers in game time.
            if (flame != null && flame.gameObject.activeInHierarchy)
            {
                flicker += delta;
                var s = 1f + 0.12f * Mathf.Sin(flicker * 11f) + 0.07f * Mathf.Sin(flicker * 17.3f + 1.3f);
                flame.localScale = new Vector3(1f, s, 1f);
            }
        }

        void Apply(Settlement settlement, bool ruined, ProjectKind project, float progress)
        {
            var stood = new Dictionary<PortBuilding, bool>
            {
                [PortBuilding.Store] = true,
                [PortBuilding.Workshop] = settlement.HasWorkshop,
                [PortBuilding.Town] = settlement.IsTown,
                [PortBuilding.SageSchool] = settlement.HasSageSchool,
                [PortBuilding.Dock] = settlement.HasDock,
            };
            PortBuilding? rising = project switch
            {
                ProjectKind.Workshop => PortBuilding.Workshop,
                ProjectKind.Town => PortBuilding.Town,
                ProjectKind.SageSchool => PortBuilding.SageSchool,
                ProjectKind.Dock => PortBuilding.Dock,
                _ => null,
            };
            if (ruined) rising = null;
            Ruined = ruined;
            Rising = rising;
            RisingProgress = rising != null ? progress : 0f;
            Scaffolded = false;
            RoofOn = false;
            workSpots.Clear();

            foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
            {
                var whole = stood[kind];
                var building = rising == kind;
                if (!whole && !building && !structures.ContainsKey(kind)) continue;
                var item = Get(kind);
                if (ruined)
                {
                    item.Root.SetActive(false);
                    if (whole && item.Ruin == null) item.Ruin = BuildRuin(item);
                    if (item.Ruin != null) item.Ruin.SetActive(whole);
                    continue;
                }
                if (item.Ruin != null) item.Ruin.SetActive(false);
                item.Root.SetActive(whole || building);
                if (whole) foreach (var piece in item.Pieces) Stage(piece, 1f);
                else if (building)
                {
                    foreach (var piece in item.Pieces) Stage(piece, progress);
                    Scaffolded = progress < ScaffoldUntil && item.Kind != PortBuilding.Dock;
                    RoofOn = progress >= RoofFrom;
                    AddWorkSpots(item);
                }
            }

            // Research raises nothing; a fire burns before the School while the sages work.
            var researching = !ruined && project == ProjectKind.HeavyArmsResearch && settlement.HasSageSchool;
            if (researching && fire == null) BuildFire();
            if (fire != null) fire.gameObject.SetActive(researching);
            if (!ruined && settlement.HasDock && settlement.RepairShipId >= 0) AddDockSpots();
        }

        /// <summary>A piece at a share of its building: footing first, then the walls up to full height, the roof at the end.</summary>
        static void Stage(Piece piece, float progress)
        {
            var whole = progress >= 1f;
            var walls = whole ? 1f : Mathf.Clamp01((progress - WallsFrom) / (RoofFrom - WallsFrom));
            if (piece.Body != null)
            {
                piece.Body.gameObject.SetActive(whole || progress >= WallsFrom);
                piece.Body.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 1f, walls), 1f);
            }
            if (piece.Roof != null) piece.Roof.gameObject.SetActive(whole || progress >= RoofFrom);
            if (piece.Scaffold != null) piece.Scaffold.gameObject.SetActive(!whole && progress < ScaffoldUntil);
            if (piece.Footing != null) piece.Footing.gameObject.SetActive(!whole);
        }

        void AddWorkSpots(Structure item)
        {
            foreach (var piece in item.Pieces)
            {
                if (workSpots.Count >= 6) return;
                var centre = Local(piece.U, piece.Z, piece.Root.localPosition.y);
                foreach (var side in new[] { -0.3f, 0.3f })
                {
                    var at = Local(piece.U + piece.W * 0.5f + 1.2f, piece.Z + side * piece.D, 0f);
                    at.y = Ground(piece.U + piece.W * 0.5f + 1.2f, piece.Z + side * piece.D);
                    workSpots.Add(new PortWorkSpot { Position = root.TransformPoint(at), Facing = root.TransformPoint(centre) });
                }
            }
        }

        void AddDockSpots()
        {
            var at = Local(19.5f, -2.6f, JettyTop);
            workSpots.Add(new PortWorkSpot { Position = root.TransformPoint(at), Facing = root.TransformPoint(Local(23.5f, -1.8f, JettyTop)) });
        }

        Structure Get(PortBuilding kind)
        {
            if (structures.TryGetValue(kind, out var item)) return item;
            item = new Structure { Kind = kind, Root = new GameObject(kind.ToString()) };
            item.Root.transform.SetParent(root, false);
            switch (kind)
            {
                case PortBuilding.Store:
                    if (outpost)
                    {
                        AddHouse(item, "Store", -3.8f, 4.9f, 4.4f, 6f, 2.9f, GreekMaterial.Stone);
                        AddHouse(item, "Hut", -3.6f, -1.6f, 3.2f, 3.6f, 2.4f, GreekMaterial.Paint);
                    }
                    else
                    {
                        AddHouse(item, "Store", -3.8f, 4.9f, 5f, 7f, 3.2f, GreekMaterial.Stone);
                        AddHouse(item, "House", -3.6f, -1.6f, 4f, 4.4f, 2.8f, GreekMaterial.Paint);
                    }
                    break;
                case PortBuilding.Workshop:
                    AddWorkshop(item);
                    break;
                case PortBuilding.Town:
                    AddHouse(item, "House", -9.6f, 6.6f, 3.6f, 3.2f, 2.8f, GreekMaterial.Paint);
                    AddHouse(item, "House", -12.6f, -6.9f, 2.8f, 3f, 2.6f, GreekMaterial.Paint);
                    AddHouse(item, "House", -7.9f, 1.6f, 2.2f, 3.2f, 2.6f, GreekMaterial.Paint);
                    AddHouse(item, "House", -7.9f, -2.6f, 2.2f, 3.2f, 2.6f, GreekMaterial.Stone);
                    AddWalls(item);
                    break;
                case PortBuilding.SageSchool:
                    AddSchool(item);
                    break;
                case PortBuilding.Dock:
                    AddDock(item);
                    break;
            }
            item.Root.SetActive(false);
            structures[kind] = item;
            return item;
        }

        // ---------- Placement ----------

        /// <summary>A point of the land in the settlement root's space from (u toward the port, z along the shore).</summary>
        Vector3 Local(float u, float z, float y) => new Vector3(toward * u, y, z);

        float Ground(float u, float z) => outline != null ? ShoreShape.GroundAt(outline, lift, toward * u, z) : 2f;

        /// <summary>The lowest ground under a footprint: a building is set into the slope with its foot there.</summary>
        float Foot(float u, float z, float w, float d)
        {
            var foot = Ground(u, z);
            for (var corner = 0; corner < 4; corner++)
                foot = Mathf.Min(foot, Ground(u + ((corner & 1) == 0 ? -0.5f : 0.5f) * w, z + ((corner & 2) == 0 ? -0.5f : 0.5f) * d));
            return foot;
        }

        /// <summary>A piece's own frame: on its foot, its +X toward the harbour.</summary>
        Piece NewPiece(Structure item, string name, float u, float z, float w, float d, float h)
        {
            var piece = new Piece { U = u, Z = z, W = w, D = d, H = h };
            piece.Root = new GameObject(name).transform;
            piece.Root.SetParent(item.Root.transform, false);
            piece.Root.localPosition = Local(u, z, Foot(u, z, w, d));
            piece.Root.localRotation = toward > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            item.Pieces.Add(piece);
            return piece;
        }

        Transform Attach(Transform parent, string name, GreekMesh mesh)
        {
            if (mesh == null) return null;
            owned.Add(mesh.Mesh);
            var part = GreekShips.Attach(parent, name, mesh, MaterialsFor(mesh));
            return part;
        }

        // ---------- Buildings ----------

        /// <summary>A house or store: walls (sunk into the slope behind), a door toward the harbour, a low tiled gable roof.</summary>
        void AddHouse(Structure item, string name, float u, float z, float w, float d, float h, GreekMaterial walls)
        {
            var piece = NewPiece(item, name, u, z, w, d, h + w * 0.5f * Mathf.Tan(RoofPitch * Mathf.Deg2Rad));
            var body = new GreekShips.MeshBuilder(null);
            body.Box(walls, new Vector3(0f, (h - Sink) * 0.5f, 0f), new Vector3(w * 0.5f, (h + Sink) * 0.5f, d * 0.5f), Quaternion.identity);
            body.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f + 0.03f, 0.95f, 0f), new Vector3(0.05f, 0.95f, Mathf.Min(0.55f, d * 0.15f)), Quaternion.identity);
            foreach (var side in new[] { -1f, 1f })
                body.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f + 0.03f, h * 0.68f, side * d * 0.3f), new Vector3(0.05f, 0.28f, 0.22f), Quaternion.identity);
            piece.Body = Attach(piece.Root, "Walls", body.Build(walls, GreekMaterial.Pitch));

            var roof = new GreekShips.MeshBuilder(null);
            Gable(roof, w, d, h, GreekMaterial.Terracotta, walls, 0.35f);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, walls));
            AddScaffold(piece, w, d, h);
        }

        const float RoofPitch = 22f, Sink = 4f;

        /// <summary>A tiled roof with its ridge along z over walls w by d, h high, and the gable ends filled in the wall's finish.</summary>
        static void Gable(GreekShips.MeshBuilder b, float w, float d, float h, GreekMaterial tiles, GreekMaterial ends, float overhang)
        {
            var slope = RoofPitch * Mathf.Deg2Rad;
            var rise = w * 0.5f * Mathf.Tan(slope);
            var run = w * 0.5f + overhang;
            foreach (var side in new[] { -1f, 1f })
            {
                var centre = new Vector3(side * run * 0.5f, h + rise - run * 0.5f * Mathf.Tan(slope) + 0.12f, 0f);
                b.Box(tiles, centre, new Vector3(run / Mathf.Cos(slope) * 0.5f, 0.1f, d * 0.5f + overhang), Quaternion.Euler(0f, 0f, -side * RoofPitch));
            }
            foreach (var end in new[] { -1f, 1f })
            {
                var z = end * d * 0.5f;
                var a = b.Vertex(new Vector3(-w * 0.5f, h, z), new Vector2(-w * 0.5f, h));
                var c = b.Vertex(new Vector3(w * 0.5f, h, z), new Vector2(w * 0.5f, h));
                var top = b.Vertex(new Vector3(0f, h + rise, z), new Vector2(0f, h + rise));
                b.Triangle(ends, a, c, top, new Vector3(0f, 0f, end));
            }
        }

        /// <summary>Poles at the corners and the middle of each side with two rings of planks, and a stone footing with stakes.</summary>
        void AddScaffold(Piece piece, float w, float d, float h)
        {
            var poles = new GreekShips.MeshBuilder(null);
            var tall = h + 1.6f;
            var x = w * 0.5f + 0.45f;
            var zz = d * 0.5f + 0.45f;
            foreach (var px in new[] { -x, 0f, x })
            foreach (var pz in new[] { -zz, 0f, zz })
            {
                if (px == 0f && pz == 0f) continue;
                poles.Box(GreekMaterial.Wood, new Vector3(px, tall * 0.5f - 0.3f, pz), new Vector3(0.09f, tall * 0.5f, 0.09f), Quaternion.identity);
            }
            foreach (var level in new[] { h * 0.4f, h * 0.85f })
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    poles.Box(GreekMaterial.Wood, new Vector3(side * x, level, 0f), new Vector3(0.05f, 0.06f, zz), Quaternion.identity);
                    poles.Box(GreekMaterial.Wood, new Vector3(0f, level, side * zz), new Vector3(x, 0.06f, 0.05f), Quaternion.identity);
                }
            }
            piece.Scaffold = Attach(piece.Root, "Scaffolding", poles.Build(GreekMaterial.Wood));

            var footing = new GreekShips.MeshBuilder(null);
            footing.Box(GreekMaterial.Stone, new Vector3(0f, -0.2f, 0f), new Vector3(w * 0.5f + 0.15f, 0.45f, d * 0.5f + 0.15f), Quaternion.identity);
            foreach (var px in new[] { -x, x })
            foreach (var pz in new[] { -zz, zz })
                footing.Box(GreekMaterial.Wood, new Vector3(px, 0.4f, pz), new Vector3(0.06f, 0.45f, 0.06f), Quaternion.identity);
            piece.Footing = Attach(piece.Root, "Footing", footing.Build(GreekMaterial.Stone, GreekMaterial.Wood));
        }

        /// <summary>
        /// The workshop: an open shed on posts with a stone back wall, a forge chimney at one end, timber stacked by it and
        /// the frame of a catapult on the trestles: the machines it makes are built here.
        /// </summary>
        void AddWorkshop(Structure item)
        {
            const float w = 5.6f, d = 4f, h = 3.3f;
            var piece = NewPiece(item, "Shed", -6f, -6.6f, w, d, h + 1.6f);
            var body = new GreekShips.MeshBuilder(null);
            foreach (var px in new[] { -w * 0.5f + 0.2f, w * 0.5f - 0.2f })
            foreach (var pz in new[] { -d * 0.5f + 0.2f, 0f, d * 0.5f - 0.2f })
                body.Box(GreekMaterial.Wood, new Vector3(px, (h - Sink) * 0.5f, pz), new Vector3(0.14f, (h + Sink) * 0.5f, 0.14f), Quaternion.identity);
            // The back wall uphill and the forge chimney.
            body.Box(GreekMaterial.Stone, new Vector3(-w * 0.5f + 0.2f, (h - Sink) * 0.5f, 0f), new Vector3(0.3f, (h + Sink) * 0.5f, d * 0.5f), Quaternion.identity);
            body.Box(GreekMaterial.Stone, new Vector3(-w * 0.5f + 0.7f, (h + 2.2f - Sink) * 0.5f, -d * 0.5f + 0.7f), new Vector3(0.55f, (h + 2.2f + Sink) * 0.5f, 0.55f), Quaternion.identity);
            body.Box(GreekMaterial.Pitch, new Vector3(-w * 0.5f + 0.7f, h + 2.2f, -d * 0.5f + 0.7f), new Vector3(0.35f, 0.02f, 0.35f), Quaternion.identity);
            // Trestles and a catapult frame on them: base beams, two uprights, the arm cocked back.
            foreach (var pz in new[] { -0.8f, 0.8f })
                body.Box(GreekMaterial.Wood, new Vector3(0.6f, 0.45f, pz), new Vector3(1.2f, 0.12f, 0.12f), Quaternion.identity);
            body.Box(GreekMaterial.Wood, new Vector3(0.6f, 0.62f, 0f), new Vector3(1.1f, 0.1f, 0.75f), Quaternion.identity);
            foreach (var pz in new[] { -0.45f, 0.45f })
                body.Box(GreekMaterial.Wood, new Vector3(0.9f, 1.3f, pz), new Vector3(0.1f, 0.65f, 0.1f), Quaternion.identity);
            body.Box(GreekMaterial.Wood, new Vector3(0.2f, 1.25f, 0f), new Vector3(1.0f, 0.08f, 0.08f), Quaternion.Euler(0f, 0f, 28f));
            body.Box(GreekMaterial.Rope, new Vector3(0.9f, 1.1f, 0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity);
            // Timber stacked outside the downhill end.
            for (var i = 0; i < 4; i++)
                body.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.7f, 0.18f + (i / 2) * 0.32f, d * 0.5f - 0.6f - (i % 2) * 0.36f), new Vector3(0.16f, 0.16f, 1.2f), Quaternion.Euler(0f, 90f, 0f));
            piece.Body = Attach(piece.Root, "Shed", body.Build(GreekMaterial.Wood, GreekMaterial.Stone, GreekMaterial.Rope, GreekMaterial.Pitch));

            var roof = new GreekShips.MeshBuilder(null);
            Gable(roof, w, d, h, GreekMaterial.Terracotta, GreekMaterial.Wood, 0.3f);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Wood));
            AddScaffold(piece, w, d, h);
        }

        /// <summary>
        /// The Sages' School on the highest terrace: a three-stepped base, a whitewashed hall inside a ring of columns, a
        /// tiled roof with pediments at both ends; its front toward the harbour.
        /// </summary>
        void AddSchool(Structure item)
        {
            const float w = 5f, d = 8.2f, h = 4.6f, steps = 1.0f;
            var piece = NewPiece(item, "School", -11.6f, -0.5f, w, d, h + steps + 1.2f);
            var body = new GreekShips.MeshBuilder(null);
            for (var i = 0; i < 3; i++)
            {
                var inset = i * 0.25f;
                body.Box(GreekMaterial.Stone, new Vector3(0f, (steps * (i + 1) / 3f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f - inset, (steps * (i + 1) / 3f + Sink) * 0.5f, d * 0.5f - inset), Quaternion.identity);
            }
            body.Box(GreekMaterial.Paint, new Vector3(-0.3f, steps + h * 0.5f, 0f), new Vector3(w * 0.5f - 1.2f, h * 0.5f, d * 0.5f - 1.3f), Quaternion.identity);
            body.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f - 1.17f, steps + 1.05f, 0f), new Vector3(0.05f, 1.05f, 0.6f), Quaternion.identity);
            var columns = new List<Vector3>();
            for (var i = 0; i < 6; i++) foreach (var side in new[] { -1f, 1f }) columns.Add(new Vector3(-w * 0.5f + 0.75f + i * (w - 1.5f) / 5f, 0f, side * (d * 0.5f - 0.75f)));
            for (var i = 1; i < 5; i++) foreach (var side in new[] { -1f, 1f }) columns.Add(new Vector3(side * (w * 0.5f - 0.75f), 0f, -d * 0.5f + 0.75f + i * (d - 1.5f) / 5f));
            foreach (var at in columns)
            {
                body.Tube(GreekMaterial.Paint, new List<Vector3> { at + Vector3.up * steps, at + Vector3.up * (steps + h) }, 0.2f, 0.16f, 8);
                body.Box(GreekMaterial.Paint, at + Vector3.up * (steps + h + 0.05f), new Vector3(0.26f, 0.06f, 0.26f), Quaternion.identity);
            }
            // The architrave over the columns.
            body.Box(GreekMaterial.Paint, new Vector3(0f, steps + h + 0.3f, 0f), new Vector3(w * 0.5f - 0.5f, 0.22f, d * 0.5f - 0.5f), Quaternion.identity);
            piece.Body = Attach(piece.Root, "Colonnade", body.Build(GreekMaterial.Stone, GreekMaterial.Paint, GreekMaterial.Pitch));

            var roof = new GreekShips.MeshBuilder(null);
            Gable(roof, w - 0.8f, d - 0.8f, steps + h + 0.52f, GreekMaterial.Terracotta, GreekMaterial.Paint, 0.35f);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Paint));
            AddScaffold(piece, w, d, h + steps);
        }

        /// <summary>
        /// The town's wall round the plot: stretches stepping with the ground, towers at the corners and a pair at the gate
        /// toward the harbour. Built in the settlement's own space, each stretch on the ground under it.
        /// </summary>
        void AddWalls(Structure item)
        {
            var piece = new Piece { U = (PlotNear + PlotFar) * 0.5f, Z = 0f, W = PlotNear - PlotFar + 1f, D = PlotHalfWidth * 2f, H = WallHeight + 1.4f };
            piece.Root = new GameObject("Wall").transform;
            piece.Root.SetParent(item.Root.transform, false);
            item.Pieces.Add(piece);
            const float back = -14.6f, front = 0.6f, side = 9.2f, gate = 3.6f;
            var body = new GreekShips.MeshBuilder(null);
            void Stretch(float u0, float z0, float u1, float z1)
            {
                var length = Mathf.Sqrt((u1 - u0) * (u1 - u0) + (z1 - z0) * (z1 - z0));
                var count = Mathf.Max(1, Mathf.CeilToInt(length / 2.4f));
                for (var i = 0; i < count; i++)
                {
                    var t = (i + 0.5f) / count;
                    var u = Mathf.Lerp(u0, u1, t);
                    var z = Mathf.Lerp(z0, z1, t);
                    var ground = Ground(u, z);
                    var centre = Local(u, z, ground + (WallHeight - Sink) * 0.5f);
                    var along = Local(u1 - u0, z1 - z0, 0f).normalized;
                    body.Box(GreekMaterial.Stone, centre, new Vector3(0.45f, (WallHeight + Sink) * 0.5f, length / count * 0.5f + 0.06f), Quaternion.LookRotation(along));
                }
            }
            void Tower(float u, float z)
            {
                var ground = Ground(u, z) - 0.6f;
                body.Box(GreekMaterial.Stone, Local(u, z, ground + (WallHeight + 1.4f - Sink) * 0.5f), new Vector3(1.1f, (WallHeight + 1.4f + Sink) * 0.5f, 1.1f), Quaternion.identity);
                body.Box(GreekMaterial.Pitch, Local(u, z, ground + WallHeight + 1.42f), new Vector3(0.7f, 0.02f, 0.7f), Quaternion.identity);
            }
            Stretch(back, -side, back, side);
            Stretch(back, -side, front, -side);
            Stretch(back, side, front, side);
            Stretch(front, -side, front, -gate);
            Stretch(front, gate, front, side);
            foreach (var (u, z) in new[] { (back, -side), (back, side), (front, -side), (front, side), (front, -gate), (front, gate) }) Tower(u, z);
            piece.Body = Attach(piece.Root, "Wall and towers", body.Build(GreekMaterial.Stone, GreekMaterial.Pitch));
            // A wall rises from its footing like any building: the stretches grow from the ground they stand on.
            var footing = new GreekShips.MeshBuilder(null);
            foreach (var (u0, z0, u1, z1) in new[] { (back, -side, back, side), (back, -side, front, -side), (back, side, front, side) })
            {
                var steps = 8;
                for (var i = 0; i < steps; i++)
                {
                    var t = (i + 0.5f) / steps;
                    var u = Mathf.Lerp(u0, u1, t);
                    var z = Mathf.Lerp(z0, z1, t);
                    footing.Box(GreekMaterial.Stone, Local(u, z, Ground(u, z) - 0.1f), new Vector3(0.55f, 0.3f, 0.55f), Quaternion.identity);
                }
            }
            piece.Footing = Attach(piece.Root, "Wall footing", footing.Build(GreekMaterial.Stone));
        }

        const float WallHeight = 3.2f;

        /// <summary>Height of the jetty's deck (BattleController.CreateShore: 1.2 m centre, 0.35 m thick).</summary>
        const float JettyTop = 1.375f;

        /// <summary>
        /// The dock: shear legs standing on the jetty's end beside its middle and leaning out over the berth where ships lie
        /// for repair, with a block and fall hauled by a windlass.
        /// </summary>
        void AddDock(Structure item)
        {
            var piece = new Piece { U = 20.7f, Z = -1.8f, W = 8.4f, D = 1.6f, H = 9.5f };
            piece.Root = new GameObject("Shear legs").transform;
            piece.Root.SetParent(item.Root.transform, false);
            item.Pieces.Add(piece);

            var crane = new GreekShips.MeshBuilder(null);
            var apex = Local(24.2f, -1.8f, 9.4f);
            foreach (var u in new[] { 20.4f, 22.4f })
                crane.Tube(GreekMaterial.Wood, new List<Vector3> { Local(u, -1.75f, JettyTop), apex }, 0.2f, 0.15f, 6);
            crane.Box(GreekMaterial.Wood, Local(21.4f, -1.75f, JettyTop + 0.1f), new Vector3(1.4f, 0.1f, 0.18f), Quaternion.identity);
            // The backstay to a bollard, the fall from the apex to its block.
            var bollard = Local(17.2f, -1.75f, JettyTop);
            crane.Tube(GreekMaterial.Rope, new List<Vector3> { apex, bollard + Vector3.up * 0.5f }, 0.045f, 0.045f, 4);
            crane.Box(GreekMaterial.Wood, bollard + Vector3.up * 0.3f, new Vector3(0.2f, 0.35f, 0.2f), Quaternion.identity);
            crane.Tube(GreekMaterial.Rope, new List<Vector3> { apex, apex + Vector3.down * 4.6f }, 0.04f, 0.04f, 4);
            crane.Box(GreekMaterial.Wood, apex + Vector3.down * 4.8f, new Vector3(0.18f, 0.25f, 0.14f), Quaternion.identity);
            // A windlass beside the bollard hauls the fall. The slipways' pitch pot and timber are ShipyardProps'.
            var drum = Local(18.6f, -1.75f, JettyTop + 0.55f);
            crane.Tube(GreekMaterial.Wood, new List<Vector3> { drum + Vector3.forward * 0.55f, drum - Vector3.forward * 0.55f }, 0.22f, 0.22f, 8);
            foreach (var end in new[] { -0.6f, 0.6f })
                crane.Box(GreekMaterial.Wood, drum + new Vector3(0f, -0.3f, end), new Vector3(0.08f, 0.32f, 0.06f), Quaternion.identity);
            piece.Body = Attach(piece.Root, "Shear legs", crane.Build(GreekMaterial.Wood, GreekMaterial.Rope));
        }

        /// <summary>A body of revolution at a point (the builder's lathe works about the origin).</summary>
        static void AppendLathe(GreekShips.MeshBuilder b, GreekMaterial paint, Vector3 at, (float, float)[] profile, int sides)
        {
            var lathe = new GreekShips.MeshBuilder(null);
            lathe.Lathe(paint, profile, sides);
            var mesh = lathe.Build(paint);
            b.Append(mesh, at, Quaternion.identity);
            Object.Destroy(mesh.Mesh);
        }

        /// <summary>A bronze tripod with a fire bowl before the School's front, lit while the sages research.</summary>
        void BuildFire()
        {
            var school = Get(PortBuilding.SageSchool);
            var front = school.Pieces[0];
            fire = new GameObject("Research fire").transform;
            fire.SetParent(school.Root.transform, false);
            var u = front.U + front.W * 0.5f + 1.3f;
            fire.localPosition = Local(u, front.Z, Ground(u, front.Z));
            var stand = new GreekShips.MeshBuilder(null);
            foreach (var angle in new[] { 0f, 120f, 240f })
            {
                var foot = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.45f, 0f, 0f);
                stand.Tube(GreekMaterial.Bronze, new List<Vector3> { foot, new Vector3(0f, 1.1f, 0f) + foot * 0.35f }, 0.05f, 0.04f, 5);
            }
            AppendLathe(stand, GreekMaterial.Bronze, new Vector3(0f, 1.05f, 0f), new[] { (0f, 0.08f), (0.12f, 0.36f), (0.26f, 0.42f) }, 10);
            Attach(fire, "Tripod", stand.Build(GreekMaterial.Bronze));
            var light = new GreekShips.MeshBuilder(null);
            light.Lathe(GreekMaterial.Ochre, new[] { (0f, 0.34f), (0.35f, 0.26f), (0.8f, 0.1f), (1.05f, 0.01f) }, 8);
            var mesh = light.Build(GreekMaterial.Ochre);
            owned.Add(mesh.Mesh);
            flame = new GameObject("Flame").transform;
            flame.SetParent(fire, false);
            flame.localPosition = new Vector3(0f, 1.25f, 0f);
            flame.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.Mesh;
            var renderer = flame.gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var glow = Flame(materials);
            if (glow != null) renderer.sharedMaterial = glow;
        }

        // ---------- Ruins ----------

        /// <summary>
        /// What is left of a building after the settlement fell, seeded by the settlement so it is always the same:
        /// walls broken off at a quarter to half their height, no roof, charred beams and rubble about, columns as stumps
        /// with drums fallen, the shear legs lying across the jetty.
        /// </summary>
        GameObject BuildRuin(Structure item)
        {
            var random = new System.Random(seed + (int)item.Kind * 101);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var ruin = new GameObject($"{item.Kind} (ruin)");
            ruin.transform.SetParent(root, false);
            foreach (var piece in item.Pieces)
            {
                var b = new GreekShips.MeshBuilder(null);
                var holder = new GameObject(piece.Root.name + " (ruin)").transform;
                holder.SetParent(ruin.transform, false);
                holder.localPosition = piece.Root.localPosition;
                holder.localRotation = piece.Root.localRotation;
                if (item.Kind == PortBuilding.Dock)
                {
                    // The legs fallen along the jetty and into the water.
                    holder.localPosition = Vector3.zero;
                    holder.localRotation = Quaternion.identity;
                    foreach (var offset in new[] { -0.5f, 0.6f })
                        b.Tube(GreekMaterial.Pitch, new List<Vector3> { Local(19.6f + offset, -1.6f + offset, JettyTop + 0.15f), Local(27f, -4.5f + offset, -0.2f) }, 0.19f, 0.14f, 6);
                }
                else if (item.Kind == PortBuilding.Town && piece.Root.name == "Wall")
                {
                    holder.localPosition = Vector3.zero;
                    holder.localRotation = Quaternion.identity;
                    const float back = -14.6f, front = 0.6f, side = 9.2f;
                    foreach (var (u0, z0, u1, z1) in new[] { (back, -side, back, side), (back, -side, front, -side), (back, side, front, side) })
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            var t = (i + 0.5f) / 8f;
                            var u = Mathf.Lerp(u0, u1, t);
                            var z = Mathf.Lerp(z0, z1, t);
                            var tall = WallHeight * Next(0.2f, 0.6f);
                            var along = Local(u1 - u0, z1 - z0, 0f).normalized;
                            var length = Mathf.Sqrt((u1 - u0) * (u1 - u0) + (z1 - z0) * (z1 - z0)) / 8f;
                            if (random.NextDouble() < 0.2) tall = 0.3f;
                            b.Box(GreekMaterial.Stone, Local(u, z, Ground(u, z) + (tall - Sink) * 0.5f), new Vector3(0.45f, (tall + Sink) * 0.5f, length * 0.5f), Quaternion.LookRotation(along));
                        }
                    }
                }
                else
                {
                    var w = piece.W;
                    var d = piece.D;
                    var h = item.Kind == PortBuilding.SageSchool ? 3.6f : Mathf.Max(2.4f, piece.H * 0.75f);
                    var wall = item.Kind == PortBuilding.Store && piece.Root.name == "Store" ? GreekMaterial.Stone : GreekMaterial.Paint;
                    if (item.Kind == PortBuilding.SageSchool)
                    {
                        b.Box(GreekMaterial.Stone, new Vector3(0f, (0.6f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f, (0.6f + Sink) * 0.5f, d * 0.5f), Quaternion.identity);
                        for (var i = 0; i < 6; i++)
                        foreach (var sideZ in new[] { -1f, 1f })
                        {
                            var at = new Vector3(-w * 0.5f + 0.75f + i * (w - 1.5f) / 5f, 0.6f, sideZ * (d * 0.5f - 0.75f));
                            b.Tube(GreekMaterial.Paint, new List<Vector3> { at, at + Vector3.up * Next(0.4f, 1.8f) }, 0.2f, 0.18f, 8);
                        }
                        for (var i = 0; i < 3; i++)
                        {
                            var at = new Vector3(Next(-w, w) * 0.6f, 0.85f, Next(-d, d) * 0.5f);
                            var dir = Quaternion.Euler(0f, Next(0f, 360f), 0f) * Vector3.forward;
                            b.Tube(GreekMaterial.Paint, new List<Vector3> { at, at + dir * 1.4f }, 0.2f, 0.2f, 8);
                        }
                    }
                    else if (item.Kind == PortBuilding.Workshop)
                    {
                        foreach (var px in new[] { -w * 0.5f + 0.2f, w * 0.5f - 0.2f })
                        foreach (var pz in new[] { -d * 0.5f + 0.2f, d * 0.5f - 0.2f })
                        {
                            var tall = Next(0.4f, 1.6f);
                            b.Box(GreekMaterial.Pitch, new Vector3(px, (tall - Sink) * 0.5f, pz), new Vector3(0.14f, (tall + Sink) * 0.5f, 0.14f), Quaternion.identity);
                        }
                        b.Box(GreekMaterial.Stone, new Vector3(-w * 0.5f + 0.7f, (2.4f - Sink) * 0.5f, -d * 0.5f + 0.7f), new Vector3(0.55f, (2.4f + Sink) * 0.5f, 0.55f), Quaternion.identity);
                        b.Box(GreekMaterial.Stone, new Vector3(-w * 0.5f + 0.2f, (1.1f - Sink) * 0.5f, 0f), new Vector3(0.3f, (1.1f + Sink) * 0.5f, d * 0.5f), Quaternion.identity);
                    }
                    else
                    {
                        // Four walls, each broken into stretches of uneven height.
                        foreach (var (cx, cz, hx, hz) in new[] { (w * 0.5f - 0.2f, 0f, 0.2f, d * 0.5f), (-w * 0.5f + 0.2f, 0f, 0.2f, d * 0.5f), (0f, d * 0.5f - 0.2f, w * 0.5f, 0.2f), (0f, -d * 0.5f + 0.2f, w * 0.5f, 0.2f) })
                        {
                            const int stretches = 3;
                            for (var i = 0; i < stretches; i++)
                            {
                                var t = (i + 0.5f) / stretches - 0.5f;
                                var tall = h * Next(0.25f, 0.5f);
                                var at = new Vector3(cx + (hz > hx ? 0f : t * 2f * hx), (tall - Sink) * 0.5f, cz + (hz > hx ? t * 2f * hz : 0f));
                                var half = hz > hx ? new Vector3(hx, (tall + Sink) * 0.5f, hz / stretches) : new Vector3(hx / stretches, (tall + Sink) * 0.5f, hz);
                                b.Box(wall, at, half, Quaternion.identity);
                            }
                        }
                    }
                    // Charred beams fallen across, and rubble.
                    for (var i = 0; i < 3; i++)
                        b.Box(GreekMaterial.Pitch, new Vector3(Next(-w, w) * 0.3f, Next(0.3f, 1.1f), Next(-d, d) * 0.3f), new Vector3(0.12f, 0.12f, Mathf.Max(w, d) * 0.45f),
                            Quaternion.Euler(Next(-25f, 25f), Next(0f, 180f), Next(-10f, 10f)));
                    for (var i = 0; i < 7; i++)
                    {
                        var size = Next(0.25f, 0.6f);
                        b.Box(GreekMaterial.Stone, new Vector3(Next(-w, w) * 0.6f, size * 0.4f, Next(-d, d) * 0.6f), new Vector3(size, size * 0.6f, size * 0.8f), Quaternion.Euler(Next(-20f, 20f), Next(0f, 360f), Next(-20f, 20f)));
                    }
                }
                Attach(holder, "Ruin", b.Build(GreekMaterial.Stone, GreekMaterial.Paint, GreekMaterial.Pitch));
            }
            ruin.SetActive(false);
            return ruin;
        }

        // ---------- Materials ----------

        static readonly Dictionary<(Material, string), Material> tints = new Dictionary<(Material, string), Material>();

        /// <summary>
        /// The land's own limestone for stone, whitewash and charred timber as copies of the scene's materials with another
        /// colour (as LandscapeArt.Wet), so they use shader variants the build already has; the rest as the ships paint them.
        /// </summary>
        Material[] MaterialsFor(GreekMesh mesh)
        {
            var list = new Material[mesh.Slots.Length];
            for (var i = 0; i < list.Length; i++) list[i] = MaterialFor(mesh.Slots[i], materials);
            return list;
        }

        public static Material MaterialFor(GreekMaterial paint, PlaceholderMaterials materials)
        {
            if (materials == null) return null;
            var stone = materials.Limestone != null ? materials.Limestone : materials.Neutral;
            switch (paint)
            {
                case GreekMaterial.Stone: return stone;
                case GreekMaterial.Paint: return Tint(stone, "whitewash", new Color(1f, 0.98f, 0.94f), 0.3f);
                case GreekMaterial.Pitch: return Tint(materials.Timber, "charred", new Color(0.07f, 0.06f, 0.055f), 0.1f);
                case GreekMaterial.Wood: return materials.Timber != null ? materials.Timber : GreekShips.MaterialFor(paint, materials);
                default: return GreekShips.MaterialFor(paint, materials);
            }
        }

        static Material Tint(Material source, string name, Color colour, float smoothness)
        {
            if (source == null) return null;
            if (tints.TryGetValue((source, name), out var made) && made != null) return made;
            made = new Material(source) { name = $"{source.name} ({name})" };
            if (made.HasProperty("_BaseColor")) made.SetColor("_BaseColor", colour);
            if (made.HasProperty("_Smoothness")) made.SetFloat("_Smoothness", smoothness);
            tints[(source, name)] = made;
            return made;
        }

        /// <summary>The fire: the scene's flash colour, glowing (emission on a copy of the lit material).</summary>
        static Material Flame(PlaceholderMaterials materials)
        {
            var source = materials?.Flash;
            if (source == null) return null;
            if (tints.TryGetValue((source, "flame"), out var made) && made != null) return made;
            made = new Material(source) { name = $"{source.name} (flame)" };
            var colour = new Color(1f, 0.55f, 0.15f);
            if (made.HasProperty("_BaseColor")) made.SetColor("_BaseColor", colour);
            if (made.HasProperty("_EmissiveColor")) made.SetColor("_EmissiveColor", colour * 4000f);
            tints[(source, "flame")] = made;
            return made;
        }
    }
}
