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
    /// The buildings on a settlement's land, of the 4th century BC, read from the simulation each frame and never feeding back:
    /// <list type="bullet">
    /// <item>always: at a home port a stone store and a plastered house with the yard before them; at a bridgehead a field
    /// camp of a palisade, tents and a boat drawn up (PortBuildings.Harbour);</item>
    /// <item>the workshop (HasWorkshop): an ergasterion, an open shed on posts by the war line with its forge, bellows, anvil
    /// and a machine's frame on trestles;</item>
    /// <item>the town (IsTown): houses on terraces up the hill and an ashlar wall with towers round the land, its gate toward
    /// the harbour;</item>
    /// <item>the Sages' School (HasSageSchool): a hall in a Doric peristyle on the highest terrace;</item>
    /// <item>the dock (HasDock): a ship shed of Zea's kind beside the war line's slipway and the jetty, and shear legs on the
    /// jetty's end over the berth where ships lie for repair (PortBuildings.Harbour).</item>
    /// </list>
    /// A project rising (Settlement.Project) shows its building at the stage reached, by the slipway's thresholds: footing
    /// and stakes, then courses, columns and stretches put up one after another among scaffolding (nothing scaled), the
    /// roof from 80%, the scaffolding down at 97%. Research raises nothing: a fire burns on a tripod before the School. Every
    /// building breaks element by element as the settlement's hull goes, and a destroyed settlement keeps its land with the
    /// ruins of what stood (PortBuildings.Breakables, docs/art/DESTRUCTION.md).
    ///
    /// The buildings stand on the land's landward half (x toward the port between -14.5 and 1 m, |z| below 9.5 m, the plot the
    /// trees keep clear of), stepping up the hill in terraces; the dock's works stand by the water, off the jetty's middle where
    /// the port's men walk. The slipways, the jetty and the store door the dock gangs use stay clear.
    /// </summary>
    public sealed partial class PortBuildings
    {
        /// <summary>The plot on the land's landward half, as distance toward the port (u) and across (z).</summary>
        public const float PlotNear = 1f, PlotFar = -14.5f, PlotHalfWidth = 9.5f;

        /// <summary>The shares of a project at which its footing gives way to walls, its roof goes on and its scaffolding comes down.</summary>
        public const float WallsFrom = 0.15f, RoofFrom = 0.8f, ScaffoldUntil = 0.97f;

        /// <summary>One shape put up as a unit: its walls, its roof, the scaffolding and footing while it rises.</summary>
        sealed class Piece
        {
            public Transform Root, Body, Roof, Scaffold, Footing;
            /// <summary>What rises course by course (wall courses, columns), shown in order as the work goes on; none: Body grows.</summary>
            public readonly List<Transform> Courses = new List<Transform>();
            /// <summary>All the courses in one mesh, shown instead of them once the building stands: one renderer, not a dozen.</summary>
            public Transform Whole;
            /// <summary>The piece's footprint on the land: centre (u, z) and size (along u, along z).</summary>
            public float U, Z, W, D, H;
        }

        sealed class Structure
        {
            public PortBuilding Kind;
            public GameObject Root;
            public readonly List<Piece> Pieces = new List<Piece>();
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

        public bool ShowsRuin(PortBuilding kind)
        {
            // Every building lies in the ruin of its own fallen pieces, shown once the first of them is at rest.
            if (!Ruined) return false;
            foreach (var view in DamagesOf(kind))
                if (view.Broken && view.Frame.gameObject.activeSelf && view.SettledPieces > 0) return true;
            return false;
        }

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
            // Every building shows the hull the settlement has left (PortBuildings.Breakables).
            UpdateDamage(settlement, ruined, delta);

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
            // Only what an earlier state showed standing, in sight a moment ago, can fall; a settlement first seen fallen
            // lies in ruins at once.
            var stoodBefore = applied && !Ruined && continuous;
            applied = true;
            Ruined = ruined;
            Rising = rising;
            RisingProgress = rising != null ? progress : 0f;
            Scaffolded = false;
            RoofOn = false;
            workSpots.Clear();

            var falls = new List<(Structure item, bool animate)>();
            foreach (PortBuilding kind in System.Enum.GetValues(typeof(PortBuilding)))
            {
                var whole = stood[kind];
                var building = rising == kind;
                if (!whole && !building && !structures.ContainsKey(kind)) continue;
                var item = Get(kind);
                if (ruined)
                {
                    // Every building comes down element by element into the ruin of its own fallen pieces
                    // (PortBuildings.Breakables): seen falling if it stood in sight a moment ago, else lying at rest at once.
                    // What was still rising is gone with the settlement.
                    if (whole) falls.Add((item, stoodBefore && item.Root.activeSelf));
                    else item.Root.SetActive(false);
                    continue;
                }
                item.Root.SetActive(whole || building);
                if (whole) foreach (var piece in item.Pieces) Stage(piece, 1f);
                else if (building)
                {
                    foreach (var piece in item.Pieces) Stage(piece, progress);
                    Scaffolded = progress < ScaffoldUntil;
                    RoofOn = progress >= RoofFrom;
                    AddWorkSpots(item);
                }
            }
            // The pieces in the air are few (StructureDamageView budgets): the School, the port's showpiece, takes them first,
            // then the town, the workshop and the store; what is over the budget lies at rest at once, in a puff of dust.
            falls.Sort((a, b) => FallOrder(a.item.Kind).CompareTo(FallOrder(b.item.Kind)));
            foreach (var (item, animate) in falls) RuinKind(item, animate);

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
            // A building is put together, one course, column or stretch after another; nothing is scaled up from the ground.
            if (piece.Body != null) piece.Body.gameObject.SetActive(whole || progress >= WallsFrom);
            var laid = whole && piece.Whole == null ? piece.Courses.Count : !whole && progress >= WallsFrom ? Mathf.CeilToInt(walls * piece.Courses.Count) : 0;
            for (var i = 0; i < piece.Courses.Count; i++) piece.Courses[i].gameObject.SetActive(i < laid);
            if (piece.Whole != null) piece.Whole.gameObject.SetActive(whole);
            if (piece.Roof != null) piece.Roof.gameObject.SetActive(whole || progress >= RoofFrom);
            if (piece.Scaffold != null) piece.Scaffold.gameObject.SetActive(!whole && progress < ScaffoldUntil);
            if (piece.Footing != null) piece.Footing.gameObject.SetActive(!whole);
        }

        void AddWorkSpots(Structure item)
        {
            foreach (var piece in item.Pieces)
            {
                if (workSpots.Count >= 6) return;
                // The shear legs are raised from the jetty by the dock's own men (AddDockSpots).
                if (piece.Root.name == "Shear legs") continue;
                var centre = Local(piece.U, piece.Z, piece.Root.localPosition.y);
                // The ship shed is worked on from the shore at its head and along its landward side, never from the sea.
                var spots = piece.Root.name == "Ship shed"
                    ? new[] { (ShedFrom - 1.2f, ShedZ - 1.5f), (ShedFrom - 1.2f, ShedZ + 1.5f), (ShedFrom + 2.5f, ShedZ - ShedWidth * 0.5f - 1.2f) }
                    : new[] { (piece.U + piece.W * 0.5f + 1.2f, piece.Z - 0.3f * piece.D), (piece.U + piece.W * 0.5f + 1.2f, piece.Z + 0.3f * piece.D) };
                foreach (var (u, z) in spots)
                {
                    var at = Local(u, z, Ground(u, z));
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
                    // A bridgehead is a field camp (PortBuildings.Harbour); a home port has its store and house.
                    if (outpost) AddCamp(item);
                    else
                    {
                        AddHouse(item, "Store", -3.8f, 4.9f, 5f, 7f, 3.2f, GreekMaterial.Stone);
                        AddHouse(item, "House", -3.6f, -1.6f, 4f, 4.4f, 2.8f, GreekMaterial.Paint);
                        AddStoreYard(item, false);
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
                    AddShipShed(item);
                    AddShearLegs(item);
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

        /// <summary>A mesh builder for masonry: courses of the stone and plaster textures run level on every upright face.</summary>
        static GreekShips.MeshBuilder Builder() => new GreekShips.MeshBuilder(null) { Level = true };

        /// <summary>A mesh only built to be copied into another: freed at once, in the editor's previews as in play.</summary>
        static void Discard(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }

        Transform Attach(Transform parent, string name, GreekMesh mesh)
        {
            if (mesh == null) return null;
            owned.Add(mesh.Mesh);
            var part = GreekShips.Attach(parent, name, mesh, MaterialsFor(mesh));
            return part;
        }

        // ---------- Buildings ----------

        /// <summary>
        /// A house or store of the 4th century BC (the houses of Olynthus, R-OLYNTHUS): a socle of rubble stone sunk into the
        /// slope, walls above it plastered over sun-dried brick (or of stone for a store), a timber door frame toward the
        /// harbour and small windows, a tiled gable roof with its antefixes. The walls rise in courses while it is built.
        /// </summary>
        void AddHouse(Structure item, string name, float u, float z, float w, float d, float h, GreekMaterial walls)
        {
            var piece = NewPiece(item, name, u, z, w, d, h + w * 0.5f * Mathf.Tan(RoofPitch * Mathf.Deg2Rad));
            const float socle = 0.7f;
            var body = Builder();
            body.Box(GreekMaterial.Stone, new Vector3(0f, (socle - Sink) * 0.5f, 0f), new Vector3(w * 0.5f + 0.04f, (socle + Sink) * 0.5f, d * 0.5f + 0.04f), Quaternion.identity);
            // The door frame is set first and the walls rise round it.
            var door = Mathf.Min(0.55f, d * 0.15f);
            foreach (var side in new[] { -1f, 1f })
                body.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.05f, 1.0f, side * (door + 0.06f)), new Vector3(0.07f, 1.0f, 0.06f), Quaternion.identity);
            body.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f + 0.03f, 0.95f, 0f), new Vector3(0.04f, 0.95f, door), Quaternion.identity);
            piece.Body = Attach(piece.Root, "Socle", body.Build(GreekMaterial.Stone, GreekMaterial.Wood, GreekMaterial.Pitch));

            const int courses = 6;
            var course = (h - socle) / courses;
            var all = Builder();
            for (var k = 0; k < courses; k++)
            {
                var y0 = socle + k * course;
                var c = Builder();
                foreach (var target in new[] { c, all })
                {
                    WallCourse(target, walls, Vector3.zero, w, d, y0, course, 0.3f, door + 0.12f, 1.9f);
                    // A small window in the course that holds it, a timber lintel over the door's top.
                    if (y0 <= h * 0.68f && h * 0.68f < y0 + course)
                        foreach (var side in new[] { -1f, 1f })
                            target.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f + 0.01f, h * 0.68f, side * d * 0.3f), new Vector3(0.03f, 0.22f, 0.18f), Quaternion.identity);
                    if (y0 <= 1.95f && 1.95f < y0 + course)
                        target.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.02f, 1.95f, 0f), new Vector3(0.08f, 0.07f, door + 0.2f), Quaternion.identity);
                }
                piece.Courses.Add(Attach(piece.Body, $"Course {k}", c.Build(walls, GreekMaterial.Pitch, GreekMaterial.Wood)));
            }
            piece.Whole = Attach(piece.Body, "Walls", all.Build(walls, GreekMaterial.Pitch, GreekMaterial.Wood));

            var roof = Builder();
            TiledGable(roof, w, d, h, walls, 0.35f);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, walls, GreekMaterial.Wood));
            AddScaffold(piece, w, d, h);
            // Its walls, door and roof bays break as the settlement's hull goes (PortBuildings.Breakables).
            var rest = PieceRest(piece, 0.7f, w, d);
            AddBreakable(item, piece, $"{item.Kind}.{name}.{item.Pieces.Count - 1}/", () => BuildHouseModel(w, d, h, walls, rest), rest, FootprintDistance(piece));
        }

        /// <summary>
        /// One course of four walls t thick round a plan w by d about the centre, from y0 up height, with the door's
        /// opening (half its width given) left in the wall toward the harbour (+x) in the courses that start below doorTop.
        /// </summary>
        static void WallCourse(GreekShips.MeshBuilder c, GreekMaterial walls, Vector3 centre, float w, float d, float y0, float height, float t, float opening, float doorTop)
        {
            var y = y0 + height * 0.5f;
            var half = height * 0.5f;
            if (y0 < doorTop)
                foreach (var side in new[] { -1f, 1f })
                {
                    var span = (d * 0.5f - opening) * 0.5f;
                    c.Box(walls, centre + new Vector3(w * 0.5f - t * 0.5f, y, side * (opening + span)), new Vector3(t * 0.5f, half, span), Quaternion.identity);
                }
            else c.Box(walls, centre + new Vector3(w * 0.5f - t * 0.5f, y, 0f), new Vector3(t * 0.5f, half, d * 0.5f), Quaternion.identity);
            c.Box(walls, centre + new Vector3(-w * 0.5f + t * 0.5f, y, 0f), new Vector3(t * 0.5f, half, d * 0.5f), Quaternion.identity);
            foreach (var side in new[] { -1f, 1f })
                c.Box(walls, centre + new Vector3(0f, y, side * (d * 0.5f - t * 0.5f)), new Vector3(w * 0.5f - t, half, t * 0.5f), Quaternion.identity);
        }

        /// <summary>
        /// A tiled roof of the 4th century BC over walls w by d, h high, ridge along z: rafters under flat pan tiles, ridged
        /// cover tiles over their joints, a row of ridge tiles, painted antefixes along both eaves, and the gable ends filled
        /// in the wall's finish (blue on a pediment). Tiles lying flat are found in the houses of Olynthus (R-OLYNTHUS).
        /// </summary>
        static void TiledGable(GreekShips.MeshBuilder b, float w, float d, float h, GreekMaterial ends, float overhang, float pitch = RoofPitch)
        {
            Gable(b, w, d, h, GreekMaterial.Terracotta, ends, overhang, pitch);
            var slope = pitch * Mathf.Deg2Rad;
            var rise = w * 0.5f * Mathf.Tan(slope);
            var run = w * 0.5f + overhang;
            var length = run / Mathf.Cos(slope);
            var rows = Mathf.Max(3, Mathf.RoundToInt((d + 2f * overhang) / 0.45f));
            foreach (var side in new[] { -1f, 1f })
            {
                var turn = Quaternion.Euler(0f, 0f, -side * pitch);
                var centre = new Vector3(side * run * 0.5f, h + rise - run * 0.5f * Mathf.Tan(slope) + 0.12f, 0f);
                var eave = new Vector3(side * run, h + rise - run * Mathf.Tan(slope) + 0.12f, 0f);
                for (var i = 0; i <= rows; i++)
                {
                    var z = -d * 0.5f - overhang + i * (d + 2f * overhang) / rows;
                    // The cover tile over the joint, its ridge standing proud of the pans.
                    b.Box(GreekMaterial.Terracotta, centre + turn * Vector3.up * 0.13f + new Vector3(0f, 0f, z), new Vector3(length * 0.5f, 0.05f, 0.07f), turn);
                    // Its antefix upright at the eave: a plate with a painted palmette (the plate's colour).
                    b.Box(GreekMaterial.Terracotta, eave + new Vector3(0f, 0.17f, z), new Vector3(0.02f, 0.12f, 0.09f), Quaternion.identity);
                }
            }
            b.Box(GreekMaterial.Terracotta, new Vector3(0f, h + rise + 0.27f, 0f), new Vector3(0.16f, 0.09f, d * 0.5f + overhang), Quaternion.identity);
            // The rafters' ends under the eaves.
            foreach (var side in new[] { -1f, 1f })
                for (var z = -d * 0.5f + 0.3f; z < d * 0.5f; z += 0.9f)
                    b.Box(GreekMaterial.Wood, new Vector3(side * (w * 0.5f + overhang * 0.5f), h + 0.05f, z), new Vector3(overhang * 0.5f, 0.05f, 0.05f), Quaternion.Euler(0f, 0f, -side * pitch));
        }

        /// <summary>
        /// A Doric column standing on the stylobate without a base: a shaft of twenty shallow flutes tapering with a slight
        /// entasis, a cushion-shaped echinus and a plain square abacus, of stuccoed limestone (the canonical form, built here
        /// because Meshy's columns came out with bases and volutes, docs/ASSET_PIPELINE.md).
        /// </summary>
        static void DoricColumn(GreekShips.MeshBuilder b, Vector3 foot, float height, float radius, int flutes)
        {
            var shaft = height * 0.88f;
            var heights = new[] { 0f, 0.35f, 0.7f, 1f };
            var ring = flutes * 2;
            var ids = new int[heights.Length, ring + 1];
            for (var r = 0; r < heights.Length; r++)
            {
                var t = heights[r];
                // Tapering to 0.8 of the foot, swelling a little (entasis) about the middle.
                var taper = Mathf.Lerp(1f, 0.8f, t) + 0.025f * Mathf.Sin(t * Mathf.PI);
                for (var k = 0; k <= ring; k++)
                {
                    var a = k * Mathf.PI / flutes;
                    // The sharp arrises and the hollows of the flutes between them.
                    var reach = radius * taper * (k % 2 == 0 ? 1f : 0.92f);
                    ids[r, k] = b.Vertex(foot + new Vector3(Mathf.Cos(a) * reach, t * shaft, Mathf.Sin(a) * reach), new Vector2(a * radius, t * shaft));
                }
                b.Seam(ids[r, 0], ids[r, ring]);
            }
            for (var r = 0; r + 1 < heights.Length; r++)
                for (var k = 0; k < ring; k++)
                {
                    var a = (k + 0.5f) * Mathf.PI / flutes;
                    b.Quad(GreekMaterial.Paint, ids[r, k], ids[r, k + 1], ids[r + 1, k + 1], ids[r + 1, k], new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                }
            var top = foot + Vector3.up * shaft;
            // The echinus flaring straight and steep (the later form: an interpretation), the square abacus over it.
            b.Lathe(GreekMaterial.Paint, new[] { (0f, radius * 0.8f), (height * 0.035f, radius * 1.05f), (height * 0.07f, radius * 1.3f), (height * 0.08f, radius * 1.32f) }, 16, top);
            var abacus = height * 0.04f;
            b.Box(GreekMaterial.Paint, top + Vector3.up * (height * 0.08f + abacus * 0.5f), new Vector3(radius * 1.38f, abacus * 0.5f, radius * 1.38f), Quaternion.identity);
        }

        /// <summary>
        /// The raking sima along the slopes at both ends of a pediment roof built by Gable with the same measures, and the
        /// acroteria at its peak and corners: painted terracotta, red as such simas were.
        /// </summary>
        static void RakingSima(GreekShips.MeshBuilder b, float w, float d, float h, float overhang, float pitch)
        {
            var slope = pitch * Mathf.Deg2Rad;
            var rise = w * 0.5f * Mathf.Tan(slope);
            var run = w * 0.5f + overhang;
            foreach (var end in new[] { -1f, 1f })
            {
                var z = end * (d * 0.5f + overhang - 0.04f);
                foreach (var side in new[] { -1f, 1f })
                {
                    var turn = Quaternion.Euler(0f, 0f, -side * pitch);
                    var centre = new Vector3(side * run * 0.5f, h + rise - run * 0.5f * Mathf.Tan(slope) + 0.12f, z);
                    b.Box(GreekMaterial.Ochre, centre + turn * Vector3.up * 0.06f, new Vector3(run / Mathf.Cos(slope) * 0.5f, 0.16f, 0.04f), turn);
                    b.Box(GreekMaterial.Ochre, new Vector3(side * run, h + rise - run * Mathf.Tan(slope) + 0.45f, z), new Vector3(0.14f, 0.2f, 0.04f), Quaternion.identity);
                }
                b.Box(GreekMaterial.Ochre, new Vector3(0f, h + rise + 0.55f, z), new Vector3(0.22f, 0.3f, 0.04f), Quaternion.identity);
            }
        }

        /// <summary>A rectangular ring of four beams about the vertical axis: outside ex by ez half-extents, t deep, from y0 up height.</summary>
        static void Ring(GreekShips.MeshBuilder b, GreekMaterial paint, float ex, float ez, float t, float y0, float height)
        {
            var y = y0 + height * 0.5f;
            foreach (var side in new[] { -1f, 1f })
            {
                b.Box(paint, new Vector3(side * (ex - t * 0.5f), y, 0f), new Vector3(t * 0.5f, height * 0.5f, ez), Quaternion.identity);
                b.Box(paint, new Vector3(0f, y, side * (ez - t * 0.5f)), new Vector3(ex - t, height * 0.5f, t * 0.5f), Quaternion.identity);
            }
        }

        const float RoofPitch = 22f, Sink = 4f;

        /// <summary>A tiled roof with its ridge along z over walls w by d, h high, and the gable ends filled in the wall's finish.</summary>
        static void Gable(GreekShips.MeshBuilder b, float w, float d, float h, GreekMaterial tiles, GreekMaterial ends, float overhang, float pitch = RoofPitch)
        {
            var slope = pitch * Mathf.Deg2Rad;
            var rise = w * 0.5f * Mathf.Tan(slope);
            var run = w * 0.5f + overhang;
            foreach (var side in new[] { -1f, 1f })
            {
                var centre = new Vector3(side * run * 0.5f, h + rise - run * 0.5f * Mathf.Tan(slope) + 0.12f, 0f);
                b.Box(tiles, centre, new Vector3(run / Mathf.Cos(slope) * 0.5f, 0.1f, d * 0.5f + overhang), Quaternion.Euler(0f, 0f, -side * pitch));
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
            var poles = Builder();
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

            var footing = Builder();
            footing.Box(GreekMaterial.Stone, new Vector3(0f, -0.2f, 0f), new Vector3(w * 0.5f + 0.15f, 0.45f, d * 0.5f + 0.15f), Quaternion.identity);
            foreach (var px in new[] { -x, x })
            foreach (var pz in new[] { -zz, zz })
                footing.Box(GreekMaterial.Wood, new Vector3(px, 0.4f, pz), new Vector3(0.06f, 0.45f, 0.06f), Quaternion.identity);
            piece.Footing = Attach(piece.Root, "Footing", footing.Build(GreekMaterial.Stone, GreekMaterial.Wood));
        }

        /// <summary>
        /// The workshop, an ergasterion of the 4th century BC: an open shed on posts with a stone back wall uphill, the forge
        /// in its corner (a hearth of stone under the chimney, a pair of leather bellows feeding it, an anvil and a quenching
        /// trough), the frame of a catapult on trestles (the machines it makes are built here) and timber stacked outside.
        /// It is put up in order: the floor and footings, the back wall and the chimney, the posts, then the gear goes in.
        /// </summary>
        void AddWorkshop(Structure item)
        {
            const float w = WorkshopWidth, d = WorkshopDepth, h = WorkshopHeight;
            var piece = NewPiece(item, "Shed", -6f, -6.6f, w, d, h + 1.6f);
            var floor = Builder();
            floor.Box(GreekMaterial.Mudbrick, new Vector3(0f, (0.05f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f, (0.05f + Sink) * 0.5f, d * 0.5f), Quaternion.identity);
            foreach (var (px, pz) in WorkshopPosts())
                floor.Box(GreekMaterial.Stone, new Vector3(px, 0.08f, pz), new Vector3(0.24f, 0.1f, 0.24f), Quaternion.identity);
            piece.Body = Attach(piece.Root, "Floor", floor.Build(GreekMaterial.Mudbrick, GreekMaterial.Stone));

            var all = Builder();
            var chimneyAt = new Vector3(-w * 0.5f + 0.7f, 0f, -d * 0.5f + 0.7f);
            var lower = Builder();
            var upper = Builder();
            foreach (var (target, from, to) in new[] { (lower, -Sink, h * 0.5f), (upper, h * 0.5f, h), (all, -Sink, h) })
                target.Box(GreekMaterial.Stone, new Vector3(-w * 0.5f + 0.2f, (from + to) * 0.5f, 0f), new Vector3(0.3f, (to - from) * 0.5f, d * 0.5f), Quaternion.identity);
            foreach (var (target, from, to) in new[] { (lower, -Sink, h * 0.5f), (upper, h * 0.5f, h + 2.2f), (all, -Sink, h + 2.2f) })
                target.Box(GreekMaterial.Stone, chimneyAt + Vector3.up * ((from + to) * 0.5f), new Vector3(0.55f, (to - from) * 0.5f, 0.55f), Quaternion.identity);
            foreach (var target in new[] { upper, all })
                target.Box(GreekMaterial.Pitch, chimneyAt + Vector3.up * (h + 2.2f), new Vector3(0.35f, 0.02f, 0.35f), Quaternion.identity);
            piece.Courses.Add(Attach(piece.Body, "Back wall and chimney, lower", lower.Build(GreekMaterial.Stone)));
            piece.Courses.Add(Attach(piece.Body, "Back wall and chimney, upper", upper.Build(GreekMaterial.Stone, GreekMaterial.Pitch)));
            var posts = Builder();
            foreach (var target in new[] { posts, all })
                foreach (var (px, pz) in WorkshopPosts())
                    target.Box(GreekMaterial.Wood, new Vector3(px, (h - Sink) * 0.5f, pz), new Vector3(0.14f, (h + Sink) * 0.5f, 0.14f), Quaternion.identity);
            piece.Courses.Add(Attach(piece.Body, "Posts", posts.Build(GreekMaterial.Wood)));
            var gear = Builder();
            foreach (var target in new[] { gear, all }) WorkshopGear(target);
            piece.Courses.Add(Attach(piece.Body, "Forge and gear", gear.Build(GreekMaterial.Wood, GreekMaterial.Stone, GreekMaterial.Pitch, GreekMaterial.Lead, GreekMaterial.Rope)));
            piece.Whole = Attach(piece.Body, "Shed", all.Build(GreekMaterial.Wood, GreekMaterial.Stone, GreekMaterial.Rope, GreekMaterial.Pitch, GreekMaterial.Lead));

            var roof = Builder();
            TiledGable(roof, w, d, h, GreekMaterial.Wood, 0.3f);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Wood));
            AddScaffold(piece, w, d, h);
            var rest = PieceRest(piece, 0f, w, d);
            AddBreakable(item, piece, "Workshop.Shed/", () => BuildShedModel(w, d, h, rest), rest, FootprintDistance(piece));
        }

        /// <summary>The workshop's shed: across the land, along the shore, up to the eaves.</summary>
        const float WorkshopWidth = 5.6f, WorkshopDepth = 4f, WorkshopHeight = 3.3f;

        /// <summary>The shed's six posts: three a side, the uphill row in the back wall.</summary>
        static IEnumerable<(float x, float z)> WorkshopPosts()
        {
            foreach (var px in new[] { -WorkshopWidth * 0.5f + 0.2f, WorkshopWidth * 0.5f - 0.2f })
            foreach (var pz in new[] { -WorkshopDepth * 0.5f + 0.2f, 0f, WorkshopDepth * 0.5f - 0.2f })
                yield return (px, pz);
        }

        /// <summary>
        /// What stands on the workshop's floor: the forge under the chimney (a stone hearth with its embers, two leather
        /// bags of bellows feeding it, an anvil of stone with its iron face, a quenching trough, the smith's tongs and
        /// hammers), the trestles with a catapult's frame on them, the arm cocked back, and timber stacked outside.
        /// </summary>
        static void WorkshopGear(GreekShips.MeshBuilder b)
        {
            const float w = WorkshopWidth, d = WorkshopDepth;
            // The hearth before the chimney's foot, waist-high, its embers dark under the ash.
            var hearth = new Vector3(-w * 0.5f + 1.8f, 0f, -d * 0.5f + 0.7f);
            b.Box(GreekMaterial.Stone, hearth + Vector3.up * 0.375f, new Vector3(0.45f, 0.375f, 0.45f), Quaternion.identity);
            b.Box(GreekMaterial.Pitch, hearth + Vector3.up * 0.76f, new Vector3(0.3f, 0.015f, 0.3f), Quaternion.identity);
            // Two bellows of leather lying side by side, their nozzles at the hearth; a handle on each.
            var bag = Builder();
            bag.Lathe(GreekMaterial.Pitch, new[] { (0f, 0.05f), (0.08f, 0.2f), (0.35f, 0.24f), (0.6f, 0.15f), (0.72f, 0.04f) }, 10);
            var bagMesh = bag.Build(GreekMaterial.Pitch);
            foreach (var side in new[] { -0.25f, 0.25f })
            {
                var at = hearth + new Vector3(side, 0.3f, 0.95f);
                b.Append(bagMesh, at, Quaternion.Euler(-90f, 0f, 0f));
                b.Box(GreekMaterial.Wood, at + new Vector3(0f, 0.26f, -0.25f), new Vector3(0.03f, 0.03f, 0.22f), Quaternion.identity);
            }
            Discard(bagMesh.Mesh);
            // The anvil: a block of stone with an iron face; the trough of water to quench in.
            var anvil = new Vector3(-0.35f, 0f, -d * 0.5f + 0.55f);
            b.Box(GreekMaterial.Stone, anvil + Vector3.up * 0.3f, new Vector3(0.2f, 0.3f, 0.2f), Quaternion.identity);
            b.Box(GreekMaterial.Lead, anvil + Vector3.up * 0.65f, new Vector3(0.22f, 0.05f, 0.12f), Quaternion.identity);
            b.Box(GreekMaterial.Lead, anvil + new Vector3(0.3f, 0.06f, 0.1f), new Vector3(0.12f, 0.02f, 0.02f), Quaternion.Euler(0f, 25f, 0f));
            b.Box(GreekMaterial.Lead, anvil + new Vector3(0.28f, 0.06f, -0.12f), new Vector3(0.09f, 0.03f, 0.03f), Quaternion.Euler(0f, -10f, 0f));
            var trough = new Vector3(-w * 0.5f + 1.2f, 0f, d * 0.5f - 0.65f);
            b.Box(GreekMaterial.Stone, trough + Vector3.up * 0.25f, new Vector3(0.5f, 0.25f, 0.25f), Quaternion.identity);
            b.Box(GreekMaterial.Pitch, trough + Vector3.up * 0.505f, new Vector3(0.42f, 0.01f, 0.17f), Quaternion.identity);
            // Trestles and a catapult frame on them: base beams, two uprights, the arm cocked back.
            foreach (var pz in new[] { -0.8f, 0.8f })
                b.Box(GreekMaterial.Wood, new Vector3(0.6f, 0.45f, pz), new Vector3(1.2f, 0.12f, 0.12f), Quaternion.identity);
            b.Box(GreekMaterial.Wood, new Vector3(0.6f, 0.62f, 0f), new Vector3(1.1f, 0.1f, 0.75f), Quaternion.identity);
            foreach (var pz in new[] { -0.45f, 0.45f })
                b.Box(GreekMaterial.Wood, new Vector3(0.9f, 1.3f, pz), new Vector3(0.1f, 0.65f, 0.1f), Quaternion.identity);
            b.Box(GreekMaterial.Wood, new Vector3(0.2f, 1.25f, 0f), new Vector3(1.0f, 0.08f, 0.08f), Quaternion.Euler(0f, 0f, 28f));
            b.Box(GreekMaterial.Rope, new Vector3(0.9f, 1.1f, 0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity);
            // Timber stacked outside the downhill end.
            for (var i = 0; i < 4; i++)
                b.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.7f, 0.18f + (i / 2) * 0.32f, d * 0.5f - 0.6f - (i % 2) * 0.36f), new Vector3(0.16f, 0.16f, 1.2f), Quaternion.Euler(0f, 90f, 0f));
        }

        /// <summary>A storage jar 1.4 m tall, from its foot up (height, radius): egg-shaped, two raised rope bands, a rolled rim.</summary>
        static readonly (float, float)[] Pithos =
        {
            (0f, 0.14f), (0.12f, 0.26f), (0.35f, 0.42f), (0.55f, 0.5f), (0.57f, 0.53f), (0.61f, 0.53f), (0.63f, 0.5f), (0.8f, 0.52f),
            (0.95f, 0.5f), (0.97f, 0.53f), (1.01f, 0.53f), (1.03f, 0.49f), (1.15f, 0.4f), (1.25f, 0.3f), (1.3f, 0.26f), (1.33f, 0.31f),
            (1.39f, 0.31f), (1.41f, 0.26f),
        };

        /// <summary>A transport amphora 0.8 m tall on its knobbed toe: ovoid body, sloping shoulder, tall neck, rim.</summary>
        static readonly (float, float)[] Amphora =
        {
            (0f, 0.035f), (0.07f, 0.04f), (0.1f, 0.03f), (0.16f, 0.06f), (0.3f, 0.15f), (0.45f, 0.18f), (0.52f, 0.17f), (0.58f, 0.12f),
            (0.62f, 0.065f), (0.76f, 0.055f), (0.78f, 0.07f), (0.81f, 0.065f),
        };

        /// <summary>
        /// Before the store: big storage jars (pithoi) sunk to their shoulders, transport amphorae leaning on its wall, and
        /// two anchors of the 4th century, one-armed and of wood with a lead-filled stock (R-ANCHORS): the port's trade
        /// at a glance. One mesh beside the store, not a piece, so the store's place on the land (Footprint) is as it
        /// was; the yard keeps north and south of the store's door.
        /// </summary>
        void AddStoreYard(Structure item, bool small)
        {
            var b = Builder();
            var front = -3.8f + (small ? 2.2f : 2.5f);
            // Three jars between the door and the town wall's inner face (z 8.75).
            for (var i = 0; i < (small ? 2 : 3); i++)
            {
                var u = front + 0.6f;
                var z = 6.3f + i * 0.9f;
                var foot = Local(u, z, Ground(u, z) - 0.5f);
                b.Lathe(GreekMaterial.Terracotta, Pithos, 16, foot);
                // Sealed with a lid, or open and dark inside.
                var lid = new[] { (1.3f, 0.27f), (1.37f, 0f) };
                b.Lathe(i % 2 == 0 ? GreekMaterial.Pitch : GreekMaterial.Terracotta, lid, 12, foot);
            }

            var jar = Builder();
            jar.Lathe(GreekMaterial.Terracotta, Amphora, 12);
            foreach (var side in new[] { -1f, 1f })
                jar.Tube(GreekMaterial.Terracotta, new List<Vector3> { new Vector3(side * 0.165f, 0.55f, 0f), new Vector3(side * 0.13f, 0.78f, 0f), new Vector3(side * 0.06f, 0.74f, 0f) }, 0.022f, 0.018f, 5);
            var amphora = jar.Build(GreekMaterial.Terracotta);
            for (var i = 0; i < (small ? 3 : 5); i++)
            {
                var u = front + 0.17f;
                var z = 2.1f + i * 0.42f;
                b.Append(amphora, Local(u, z, Ground(u, z)), Quaternion.Euler(0f, 0f, toward * 11f) * Quaternion.Euler(0f, 37f * i, 0f));
            }
            Discard(amphora.Mesh);

            // Anchors leaning on the house's wall either side of its door: one-armed, of oak, the arm's tip shod with
            // iron, the wooden stock filled with lead (Ma'agan Mikhael c. 400 BC, Kyrenia c. 300 BC: R-ANCHORS).
            var anchor = Builder();
            anchor.Box(GreekMaterial.Wood, new Vector3(0f, 1f, 0f), new Vector3(0.06f, 1f, 0.06f), Quaternion.identity);
            anchor.Box(GreekMaterial.Wood, new Vector3(0.22f, 0.22f, 0f), new Vector3(0.28f, 0.05f, 0.05f), Quaternion.Euler(0f, 0f, 40f));
            anchor.Box(GreekMaterial.Pitch, new Vector3(0.42f, 0.39f, 0f), new Vector3(0.06f, 0.055f, 0.055f), Quaternion.Euler(0f, 0f, 40f));
            anchor.Box(GreekMaterial.Wood, new Vector3(0f, 1.82f, 0f), new Vector3(0.08f, 0.07f, 0.55f), Quaternion.identity);
            foreach (var end in new[] { -0.3f, 0.3f })
                anchor.Box(GreekMaterial.Lead, new Vector3(0f, 1.82f, end), new Vector3(0.085f, 0.05f, 0.12f), Quaternion.identity);
            var made = anchor.Build(GreekMaterial.Wood, GreekMaterial.Pitch, GreekMaterial.Lead);
            var house = -3.6f + (small ? 1.6f : 2f);
            foreach (var z in new[] { -3.2f, 0f })
            {
                var u = house + 0.55f;
                b.Append(made, Local(u, z, Ground(u, z)), Quaternion.Euler(0f, 0f, toward * 15f));
            }
            Discard(made.Mesh);
            Attach(item.Root.transform, "Store yard", b.Build(GreekMaterial.Terracotta, GreekMaterial.Pitch, GreekMaterial.Wood, GreekMaterial.Lead));
            // A coil of rope in the street between the house and the amphorae: the pipeline's model (AegeanArt), if any.
            var art = AegeanArt.Current;
            var coil = art != null ? art.Prop("prop_rope_") : null;
            if (coil != null)
                AegeanArt.Place(coil, item.Root.transform, Local(front + 0.55f, 1.3f, Ground(front + 0.55f, 1.3f)), 0.9f, Quaternion.Euler(0f, 15f, 0f) * Quaternion.Euler(90f, 0f, 0f), "Rope coil");
        }

        /// <summary>The School's three steps (1 m in all), its columns' measures and axes: 0.35 m in from the stylobate's edge.</summary>
        const float SchoolSteps = 1f, SchoolColumnHeight = 3.3f, SchoolColumnRadius = 0.26f, SchoolColumnX = 1.75f, SchoolColumnZ = 3.35f;

        /// <summary>The School's plan (the crepis) and its hall's height; the entablature's courses; the hall inside the colonnade.</summary>
        const float SchoolWidth = 5f, SchoolDepth = 8.2f, SchoolHallHeight = 4.6f;
        const float SchoolEpistyle = 0.42f, SchoolFrieze = 0.48f, SchoolCornice = 0.2f, SchoolPitch = 15f, SchoolOverhang = 0.25f;
        const float HallWidth = 2.1f, HallDepth = 5.2f, HallDoor = 0.5f, HallDoorTop = 2.2f, HallOffset = -0.15f, HallWall = 0.3f;
        const int HallCourses = 6;

        /// <summary>
        /// The School's peristyle of 4 by 6 columns: the long side toward the harbour first, then round. Columns of 6.3
        /// lower diameters: slender, as the Doric column grew century by century, the later 4th century's among the
        /// slenderest (R-DORIC-PROPORTIONS).
        /// </summary>
        static List<Vector3> SchoolColumns()
        {
            var list = new List<Vector3>();
            for (var i = 0; i < 6; i++) list.Add(new Vector3(SchoolColumnX, 0f, -SchoolColumnZ + i * 2f * SchoolColumnZ / 5f));
            for (var i = 1; i < 3; i++) list.Add(new Vector3(SchoolColumnX - i * 2f * SchoolColumnX / 3f, 0f, SchoolColumnZ));
            for (var i = 5; i >= 0; i--) list.Add(new Vector3(-SchoolColumnX, 0f, -SchoolColumnZ + i * 2f * SchoolColumnZ / 5f));
            for (var i = 2; i >= 1; i--) list.Add(new Vector3(SchoolColumnX - i * 2f * SchoolColumnX / 3f, 0f, -SchoolColumnZ));
            return list;
        }

        /// <summary>
        /// The Sages' School as a hall of learning of the 4th century BC, the Academy's and the Lyceum's time: a
        /// three-stepped base, a stuccoed hall inside a Doric peristyle (the game's choice), an entablature painted as
        /// Doric ones were (bluish triglyphs as on the Vergina facades, R-VERGINA-PELLA; the red taenia and sima and the
        /// blue tympana an interpretation), a tiled roof; its long side toward the harbour. While it is built the columns
        /// go up a side at a time, the hall in courses inside them, then the architrave and the frieze are laid on.
        /// </summary>
        void AddSchool(Structure item)
        {
            const float w = SchoolWidth, d = SchoolDepth, h = SchoolHallHeight, steps = SchoolSteps;
            var piece = NewPiece(item, "School", -11.6f, -0.5f, w, d, h + steps + 1.2f);
            schoolPiece = piece;
            var body = Builder();
            for (var i = 0; i < 3; i++)
            {
                var inset = i * 0.2f;
                body.Box(GreekMaterial.Stone, new Vector3(0f, (steps * (i + 1) / 3f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f - inset, (steps * (i + 1) / 3f + Sink) * 0.5f, d * 0.5f - inset), Quaternion.identity);
            }
            schoolCrepis = body.Build(GreekMaterial.Stone);
            piece.Body = Attach(piece.Root, "Crepis", schoolCrepis);
            var floor = Vector3.up * steps;

            // Each stage of the work is laid twice: as its own course, shown while the School rises, and into the whole.
            var all = Builder();
            var columns = SchoolColumns();
            foreach (var (first, count) in new[] { (0, 6), (6, 2), (8, 6), (14, 2) })
            {
                var c = Builder();
                foreach (var target in new[] { c, all })
                    for (var i = first; i < first + count; i++) DoricColumn(target, floor + columns[i], SchoolColumnHeight, SchoolColumnRadius, 20);
                piece.Courses.Add(Attach(piece.Body, $"Columns {first}", c.Build(GreekMaterial.Paint)));
            }

            // The hall: stone orthostates, then stuccoed courses up under the roof; a tall door toward the harbour.
            const float epistyle = SchoolEpistyle, frieze = SchoolFrieze, cornice = SchoolCornice, hallW = HallWidth, hallD = HallDepth, door = HallDoor, doorTop = HallDoorTop;
            var hall = floor + new Vector3(HallOffset, 0f, 0f);
            const int courses = HallCourses;
            var course = (SchoolColumnHeight + epistyle + frieze) / courses;
            for (var k = 0; k < courses; k++)
            {
                var y0 = k * course;
                var c = Builder();
                foreach (var target in new[] { c, all })
                {
                    WallCourse(target, k == 0 ? GreekMaterial.Stone : GreekMaterial.Paint, hall, hallW, hallD, y0, course, 0.3f, door, doorTop);
                    if (y0 < doorTop)
                        target.Box(GreekMaterial.Pitch, hall + new Vector3(hallW * 0.5f - 0.25f, y0 + course * 0.5f, 0f), new Vector3(0.03f, course * 0.5f, door), Quaternion.identity);
                }
                piece.Courses.Add(Attach(piece.Body, $"Hall course {k}", c.Build(GreekMaterial.Stone, GreekMaterial.Paint, GreekMaterial.Pitch)));
            }

            // The architrave on the capitals, its red taenia and the blue regulae under each triglyph.
            var top = steps + SchoolColumnHeight;
            var ex = SchoolColumnX + 0.24f;
            var ez = SchoolColumnZ + 0.24f;
            var triglyphs = new List<(Vector3 at, bool alongZ)>();
            foreach (var side in new[] { -1f, 1f })
            {
                for (var i = 0; i <= 10; i++)
                {
                    var z = i == 0 ? -(ez - 0.13f) : i == 10 ? ez - 0.13f : -SchoolColumnZ + i * SchoolColumnZ / 5f;
                    triglyphs.Add((new Vector3(side * ex, 0f, z), true));
                }
                for (var i = 0; i <= 6; i++)
                {
                    var x = i == 0 ? -(ex - 0.13f) : i == 6 ? ex - 0.13f : -SchoolColumnX + i * SchoolColumnX / 3f;
                    triglyphs.Add((new Vector3(x, 0f, side * ez), false));
                }
            }
            var architrave = Builder();
            foreach (var target in new[] { architrave, all })
            {
                Ring(target, GreekMaterial.Paint, ex, ez, 0.48f, top, epistyle);
                Ring(target, GreekMaterial.Ochre, ex + 0.03f, ez + 0.03f, 0.06f, top + epistyle - 0.06f, 0.06f);
                foreach (var (at, alongZ) in triglyphs)
                {
                    var out1 = alongZ ? new Vector3(Mathf.Sign(at.x) * 0.02f, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(at.z) * 0.02f);
                    var half = alongZ ? new Vector3(0.02f, 0.025f, 0.13f) : new Vector3(0.13f, 0.025f, 0.02f);
                    target.Box(GreekMaterial.Azure, at + out1 + Vector3.up * (top + epistyle - 0.085f), half, Quaternion.identity);
                }
            }
            piece.Courses.Add(Attach(piece.Body, "Architrave", architrave.Build(GreekMaterial.Paint, GreekMaterial.Ochre, GreekMaterial.Azure)));

            // The frieze of blue triglyphs standing proud of white metopes, the cornice over it with its blue mutules.
            var entablature = Builder();
            var friezeAt = top + epistyle;
            foreach (var target in new[] { entablature, all })
            {
                Ring(target, GreekMaterial.Paint, ex - 0.05f, ez - 0.05f, 0.4f, friezeAt, frieze);
                foreach (var (at, alongZ) in triglyphs)
                {
                    var back = alongZ ? new Vector3(-Mathf.Sign(at.x) * 0.04f, 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(at.z) * 0.04f);
                    var half = alongZ ? new Vector3(0.04f, frieze * 0.5f, 0.13f) : new Vector3(0.13f, frieze * 0.5f, 0.04f);
                    target.Box(GreekMaterial.Azure, at + back + Vector3.up * (friezeAt + frieze * 0.5f), half, Quaternion.identity);
                }
                Ring(target, GreekMaterial.Azure, ex + 0.15f, ez + 0.15f, 0.15f, friezeAt + frieze - 0.03f, 0.03f);
                Ring(target, GreekMaterial.Paint, ex + 0.15f, ez + 0.15f, 0.4f, friezeAt + frieze, cornice);
            }
            piece.Courses.Add(Attach(piece.Body, "Frieze and cornice", entablature.Build(GreekMaterial.Paint, GreekMaterial.Azure)));
            piece.Whole = Attach(piece.Body, "Colonnade and hall", all.Build(GreekMaterial.Paint, GreekMaterial.Stone, GreekMaterial.Pitch, GreekMaterial.Ochre, GreekMaterial.Azure));

            // The roof at a temple's low pitch, blue tympana in the pediments, the red raking sima and acroteria.
            const float pitch = SchoolPitch, overhang = SchoolOverhang;
            var eaves = friezeAt + frieze + cornice;
            var roof = Builder();
            TiledGable(roof, 2f * (ex + 0.15f), 2f * (ez + 0.15f), eaves, GreekMaterial.Azure, overhang, pitch);
            RakingSima(roof, 2f * (ex + 0.15f), 2f * (ez + 0.15f), eaves, overhang, pitch);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Azure, GreekMaterial.Ochre, GreekMaterial.Wood));
            AddScaffold(piece, w, d, h + steps);
            AddBreakable(item, piece, "school/", BuildSchoolModel, SchoolRestHeight, FootprintDistance(piece));
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
            var all = Builder();
            var roofs = Builder();
            // A curtain of regular ashlar (Messene's walls, R-MESSENE); its crenellated parapet is an interpretation.
            void Stretch(GreekShips.MeshBuilder body, float u0, float z0, float u1, float z1)
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
                    var turn = Quaternion.LookRotation(along);
                    body.Box(GreekMaterial.Stone, centre, new Vector3(0.45f, (WallHeight + Sink) * 0.5f, length / count * 0.5f + 0.06f), turn);
                    foreach (var end in new[] { -0.25f, 0.25f })
                        body.Box(GreekMaterial.Stone, Local(u, z, ground + WallHeight + 0.3f) + along * (end * length / count), new Vector3(0.47f, 0.3f, 0.38f), turn);
                }
            }
            // Square towers rising a storey over the wall-walk, loopholes in the upper room, a gabled roof (R-MESSENE;
            // its tiles are an interpretation).
            void Tower(GreekShips.MeshBuilder body, float u, float z, bool cap)
            {
                var ground = Ground(u, z) - 0.6f;
                var top = ground + WallHeight + 1.4f;
                body.Box(GreekMaterial.Stone, Local(u, z, ground + (WallHeight + 1.4f - Sink) * 0.5f), new Vector3(1.1f, (WallHeight + 1.4f + Sink) * 0.5f, 1.1f), Quaternion.identity);
                var slit = Local(u, z, ground + WallHeight + 0.6f);
                foreach (var face in new[] { -1f, 1f })
                foreach (var across in new[] { -0.45f, 0.45f })
                {
                    body.Box(GreekMaterial.Pitch, slit + new Vector3(face * 1.11f, 0f, across), new Vector3(0.02f, 0.32f, 0.07f), Quaternion.identity);
                    body.Box(GreekMaterial.Pitch, slit + new Vector3(across, 0f, face * 1.11f), new Vector3(0.07f, 0.32f, 0.02f), Quaternion.identity);
                }
                if (!cap) return;
                var tiles = Builder();
                TiledGable(tiles, 2.2f, 2.2f, 0f, GreekMaterial.Stone, 0.18f);
                var mesh = tiles.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood);
                roofs.Append(mesh, Local(u, z, top), Quaternion.identity);
                Discard(mesh.Mesh);
            }
            // Put up stretch by stretch, the towers last; one mesh once it stands.
            piece.Body = new GameObject("Wall courses").transform;
            piece.Body.SetParent(piece.Root, false);
            var lines = new[] { (back, -side, back, side), (back, -side, front, -side), (back, side, front, side), (front, -side, front, -gate), (front, gate, front, side) };
            for (var i = 0; i < lines.Length; i++)
            {
                var (u0, z0, u1, z1) = lines[i];
                var c = Builder();
                Stretch(c, u0, z0, u1, z1);
                Stretch(all, u0, z0, u1, z1);
                piece.Courses.Add(Attach(piece.Body, $"Curtain {i}", c.Build(GreekMaterial.Stone)));
            }
            var towers = Builder();
            foreach (var (u, z) in new[] { (back, -side), (back, side), (front, -side), (front, side), (front, -gate), (front, gate) })
            {
                Tower(towers, u, z, true);
                Tower(all, u, z, false);
            }
            piece.Courses.Add(Attach(piece.Body, "Towers", towers.Build(GreekMaterial.Stone, GreekMaterial.Pitch)));
            piece.Whole = Attach(piece.Root, "Wall and towers", all.Build(GreekMaterial.Stone, GreekMaterial.Pitch));
            piece.Roof = Attach(piece.Root, "Tower roofs", roofs.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood));
            // A wall rises from its footing like any building: the stretches grow from the ground they stand on.
            var footing = Builder();
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
            AddBreakable(item, piece, "Town.Wall/", BuildWallModel, point => Ground(point.x * toward, point.z), WallDistance);
        }

        const float WallHeight = 3.2f;

        /// <summary>Height of the jetty's deck (BattleController.CreateShore: 1.2 m centre, 0.35 m thick).</summary>
        const float JettyTop = 1.375f;

        /// <summary>A body of revolution at a point (the builder's lathe works about the origin).</summary>
        static void AppendLathe(GreekShips.MeshBuilder b, GreekMaterial paint, Vector3 at, (float, float)[] profile, int sides) => b.Lathe(paint, profile, sides, at);

        /// <summary>A bronze tripod with a fire bowl before the School's front, lit while the sages research.</summary>
        void BuildFire()
        {
            var school = Get(PortBuilding.SageSchool);
            var front = school.Pieces[0];
            fire = new GameObject("Research fire").transform;
            fire.SetParent(school.Root.transform, false);
            var u = front.U + front.W * 0.5f + 1.3f;
            fire.localPosition = Local(u, front.Z, Ground(u, front.Z));
            var stand = Builder();
            foreach (var angle in new[] { 0f, 120f, 240f })
            {
                var foot = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.45f, 0f, 0f);
                stand.Tube(GreekMaterial.Bronze, new List<Vector3> { foot, new Vector3(0f, 1.1f, 0f) + foot * 0.35f }, 0.05f, 0.04f, 5);
            }
            AppendLathe(stand, GreekMaterial.Bronze, new Vector3(0f, 1.05f, 0f), new[] { (0f, 0.08f), (0.12f, 0.36f), (0.26f, 0.42f) }, 10);
            Attach(fire, "Tripod", stand.Build(GreekMaterial.Bronze));
            var light = Builder();
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

        // ---------- Falling into ruin ----------

        /// <summary>How long a building's pieces take to come down, in game seconds (a pause holds them); the tools wait so long.</summary>
        public const float CollapseSeconds = 2.6f;

        ParticleSystem dust, tiles;
        Mesh tileMesh;
        bool applied;

        /// <summary>Some building of the settlement is still coming down: pieces of it are in the air.</summary>
        public bool Collapsing => breakables.Exists(item => item.View != null && item.View.Busy);

        /// <summary>
        /// Soft billboards in the colour of limestone dust: a copy of the scene's spray material (the same shader
        /// variant, its own colour; the water's material itself is untouched). None without it.
        /// </summary>
        ParticleSystem DustSystem()
        {
            if (dust != null) return dust;
            var source = materials?.Spray;
            if (source == null) return null;
            dust = Particles("Collapse dust", 900, -0.03f);
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = DustMaterial(source);
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.35f, 1f), new Keyframe(0.85f, 1.25f), new Keyframe(1f, 0f)));
            var limit = dust.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1.2f;
            limit.dampen = 0.08f;
            return dust;
        }

        /// <summary>Roof tiles as small flat terracotta slabs, tumbling under gravity.</summary>
        ParticleSystem TileSystem()
        {
            if (tiles != null) return tiles;
            var material = MaterialFor(GreekMaterial.Terracotta, materials);
            if (material == null) return null;
            tiles = Particles("Falling tiles", 300, 1f);
            var builder = Builder();
            builder.Box(GreekMaterial.Terracotta, Vector3.zero, new Vector3(0.22f, 0.02f, 0.16f), Quaternion.identity);
            tileMesh = builder.Build(GreekMaterial.Terracotta).Mesh;
            owned.Add(tileMesh);
            var renderer = tiles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = tileMesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            var main = tiles.main;
            main.startRotation3D = true;
            return tiles;
        }

        ParticleSystem Particles(string name, int max, float gravity)
        {
            var item = new GameObject(name);
            item.transform.SetParent(root, false);
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
            item.GetComponent<ParticleSystemRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        static Material DustMaterial(Material source)
        {
            if (tints.TryGetValue((source, "dust"), out var made) && made != null) return made;
            made = new Material(source) { name = $"{source.name} (collapse dust)" };
            var colour = new Color(0.74f, 0.68f, 0.58f, 0.42f);
            foreach (var property in new[] { "_UnlitColor", "_BaseColor", "_Color" })
                if (made.HasProperty(property)) made.SetColor(property, colour);
            tints[(source, "dust")] = made;
            return made;
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

        /// <summary>Dressed limestone and lime plaster for buildings, baked by the editor's BuildingTextureBaker into Resources.</summary>
        public const string AshlarMaterial = "Buildings/Ashlar", StuccoMaterial = "Buildings/Stucco";

        public static Material MaterialFor(GreekMaterial paint, PlaceholderMaterials materials)
        {
            if (materials == null) return null;
            var stone = materials.Limestone != null ? materials.Limestone : materials.Neutral;
            // Where the baked masonry is missing: the land's own limestone, and plain paint for plaster.
            var ashlar = Resources.Load<Material>(AshlarMaterial);
            var plaster = Resources.Load<Material>(StuccoMaterial);
            var masonry = ashlar != null ? ashlar : stone;
            var painted = plaster != null ? plaster : materials.Neutral != null ? materials.Neutral : stone;
            switch (paint)
            {
                case GreekMaterial.Stone: return masonry;
                case GreekMaterial.Paint: return Tint(painted, "whitewash", new Color(1f, 0.98f, 0.94f), 0.3f);
                case GreekMaterial.Pitch: return Tint(materials.Timber, "charred", new Color(0.07f, 0.06f, 0.055f), 0.1f);
                case GreekMaterial.Wood: return materials.Timber != null ? materials.Timber : GreekShips.MaterialFor(paint, materials);
                // The painters' colours on plaster and terracotta: Egyptian blue and red ochre.
                case GreekMaterial.Azure: return Tint(painted, "egyptian blue", GreekShips.Palette.Azure, 0.25f);
                case GreekMaterial.Ochre: return Tint(painted, "red ochre", GreekShips.Palette.Ochre, 0.25f);
                case GreekMaterial.Lead: return Tint(materials.Neutral != null ? materials.Neutral : stone, "lead", GreekShips.Palette.Lead, 0.4f);
                // Sun-dried brick where the plaster has come off: the plaster's grain in the brick's colour.
                case GreekMaterial.Mudbrick: return Tint(painted, "mudbrick", GreekShips.Palette.Mudbrick, 0.12f);
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
