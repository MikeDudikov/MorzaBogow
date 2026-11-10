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
            if (falling.Count > 0) Fall(delta);

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
            // Only what an earlier state showed standing can fall; a settlement first seen fallen lies in ruins at once.
            var stoodBefore = applied && !Ruined;
            applied = true;
            // A port raised again stands at once, whatever was still falling.
            if (!ruined) StopFalling();
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
                    if (whole && item.Ruin == null) item.Ruin = BuildRuin(item);
                    // What stood in sight falls into its ruin; what never stood here, or fell before, lies in it at once.
                    if (whole && stoodBefore && item.Root.activeSelf && StartFalling(item)) continue;
                    item.Root.SetActive(false);
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
                // A building of courses is put together, one course or column after another; others still grow as a whole.
                if (piece.Courses.Count == 0) piece.Body.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 1f, walls), 1f);
            }
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
                    AddStoreYard(item, outpost);
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
        /// The workshop: an open shed on posts with a stone back wall, a forge chimney at one end, timber stacked by it and
        /// the frame of a catapult on the trestles: the machines it makes are built here.
        /// </summary>
        void AddWorkshop(Structure item)
        {
            const float w = 5.6f, d = 4f, h = 3.3f;
            var piece = NewPiece(item, "Shed", -6f, -6.6f, w, d, h + 1.6f);
            var body = Builder();
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

            var roof = Builder();
            TiledGable(roof, w, d, h, GreekMaterial.Wood, 0.3f);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Wood));
            AddScaffold(piece, w, d, h);
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
        }

        /// <summary>The School's three steps (1 m in all), its columns' measures and axes: 0.35 m in from the stylobate's edge.</summary>
        const float SchoolSteps = 1f, SchoolColumnHeight = 3.3f, SchoolColumnRadius = 0.26f, SchoolColumnX = 1.75f, SchoolColumnZ = 3.35f;

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
            const float w = 5f, d = 8.2f, h = 4.6f, steps = SchoolSteps;
            var piece = NewPiece(item, "School", -11.6f, -0.5f, w, d, h + steps + 1.2f);
            var body = Builder();
            for (var i = 0; i < 3; i++)
            {
                var inset = i * 0.2f;
                body.Box(GreekMaterial.Stone, new Vector3(0f, (steps * (i + 1) / 3f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f - inset, (steps * (i + 1) / 3f + Sink) * 0.5f, d * 0.5f - inset), Quaternion.identity);
            }
            piece.Body = Attach(piece.Root, "Crepis", body.Build(GreekMaterial.Stone));
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
            const float epistyle = 0.42f, frieze = 0.48f, cornice = 0.2f, hallW = 2.1f, hallD = 5.2f, door = 0.5f, doorTop = 2.2f;
            var hall = floor + new Vector3(-0.15f, 0f, 0f);
            const int courses = 6;
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
            const float pitch = 15f, overhang = 0.25f;
            var eaves = friezeAt + frieze + cornice;
            var roof = Builder();
            TiledGable(roof, 2f * (ex + 0.15f), 2f * (ez + 0.15f), eaves, GreekMaterial.Azure, overhang, pitch);
            RakingSima(roof, 2f * (ex + 0.15f), 2f * (ez + 0.15f), eaves, overhang, pitch);
            piece.Roof = Attach(piece.Root, "Roof", roof.Build(GreekMaterial.Terracotta, GreekMaterial.Azure, GreekMaterial.Ochre, GreekMaterial.Wood));
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
            var body = Builder();
            var roofs = Builder();
            // A curtain of regular ashlar (Messene's walls, R-MESSENE); its crenellated parapet is an interpretation.
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
                    var turn = Quaternion.LookRotation(along);
                    body.Box(GreekMaterial.Stone, centre, new Vector3(0.45f, (WallHeight + Sink) * 0.5f, length / count * 0.5f + 0.06f), turn);
                    foreach (var end in new[] { -0.25f, 0.25f })
                        body.Box(GreekMaterial.Stone, Local(u, z, ground + WallHeight + 0.3f) + along * (end * length / count), new Vector3(0.47f, 0.3f, 0.38f), turn);
                }
            }
            // Square towers rising a storey over the wall-walk, loopholes in the upper room, a gabled roof (R-MESSENE;
            // its tiles are an interpretation).
            void Tower(float u, float z)
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
                var cap = Builder();
                TiledGable(cap, 2.2f, 2.2f, 0f, GreekMaterial.Stone, 0.18f);
                var mesh = cap.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood);
                roofs.Append(mesh, Local(u, z, top), Quaternion.identity);
                Discard(mesh.Mesh);
            }
            Stretch(back, -side, back, side);
            Stretch(back, -side, front, -side);
            Stretch(back, side, front, side);
            Stretch(front, -side, front, -gate);
            Stretch(front, gate, front, side);
            foreach (var (u, z) in new[] { (back, -side), (back, side), (front, -side), (front, side), (front, -gate), (front, gate) }) Tower(u, z);
            piece.Body = Attach(piece.Root, "Wall and towers", body.Build(GreekMaterial.Stone, GreekMaterial.Pitch));
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

            var crane = Builder();
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
                var b = Builder();
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
                        // The steps stand; of the peristyle stumps are left, some columns gone, drums fallen about.
                        for (var i = 0; i < 3; i++)
                        {
                            var inset = i * 0.2f;
                            b.Box(GreekMaterial.Stone, new Vector3(0f, (SchoolSteps * (i + 1) / 3f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f - inset, (SchoolSteps * (i + 1) / 3f + Sink) * 0.5f, d * 0.5f - inset), Quaternion.identity);
                        }
                        const float r = SchoolColumnRadius;
                        foreach (var at in SchoolColumns())
                        {
                            if (random.NextDouble() < 0.25) continue;
                            // A stump broken off, its top a low rough cone.
                            var tall = Next(0.4f, 1.8f);
                            b.Lathe(GreekMaterial.Paint, new[] { (0f, r), (tall, r * 0.97f), (tall + Next(0.03f, 0.12f), 0f) }, 10, at + Vector3.up * SchoolSteps);
                        }
                        for (var i = 0; i < 4; i++)
                        {
                            // Drums fallen on the stylobate, whole bodies with their faces.
                            var length = Next(0.6f, 1.4f);
                            var drum = Builder();
                            drum.Lathe(GreekMaterial.Paint, new[] { (-0.02f, 0f), (0f, r), (length, r), (length + 0.02f, 0f) }, 10);
                            var mesh = drum.Build(GreekMaterial.Paint);
                            var at = new Vector3(Next(-w, w) * 0.6f, SchoolSteps + r, Next(-d, d) * 0.5f);
                            b.Append(mesh, at, Quaternion.Euler(0f, Next(0f, 360f), 0f) * Quaternion.Euler(90f, 0f, 0f));
                            Discard(mesh.Mesh);
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

        // ---------- Falling into ruin ----------

        /// <summary>How long a building takes to fall into its ruin, in game seconds (a pause holds it).</summary>
        public const float CollapseSeconds = 2.6f;

        /// <summary>A building falling: where its pieces stood, the way each leans, the time since it gave.</summary>
        sealed class Falling
        {
            public Structure Item;
            public Vector3[] Position;
            public Quaternion[] Rotation;
            public Vector3[] Lean;
            public float Time;
            public bool Struck;
        }

        readonly List<Falling> falling = new List<Falling>();
        ParticleSystem dust, tiles;
        Mesh tileMesh;
        bool applied;

        /// <summary>Some building of the settlement is still falling into its ruin.</summary>
        public bool Collapsing => falling.Count > 0;

        /// <summary>Starts a standing building's fall: dust bursts from it as it gives; its ruin shows later, through the dust.</summary>
        bool StartFalling(Structure item)
        {
            if (falling.Exists(fall => fall.Item == item)) return true;
            var random = new System.Random(seed + (int)item.Kind * 53);
            var count = item.Pieces.Count;
            var started = new Falling { Item = item, Position = new Vector3[count], Rotation = new Quaternion[count], Lean = new Vector3[count] };
            for (var i = 0; i < count; i++)
            {
                started.Position[i] = item.Pieces[i].Root.localPosition;
                started.Rotation[i] = item.Pieces[i].Root.localRotation;
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                started.Lean[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            if (item.Ruin != null) item.Ruin.SetActive(false);
            falling.Add(started);
            Dust(item, 0.8f, random);
            return true;
        }

        /// <summary>
        /// The walls give at their foot and sink into their own rubble, leaning as they go; the roof falls in first and
        /// faster. When it strikes, the ruin shows through a second cloud of dust and the roof's tiles fly.
        /// </summary>
        void Fall(float delta)
        {
            for (var k = falling.Count - 1; k >= 0; k--)
            {
                var fall = falling[k];
                fall.Time += delta;
                var p = Mathf.Clamp01(fall.Time / CollapseSeconds);
                for (var i = 0; i < fall.Item.Pieces.Count; i++)
                {
                    var piece = fall.Item.Pieces[i];
                    piece.Root.localPosition = fall.Position[i] + Vector3.down * (p * p * piece.H * 0.9f);
                    piece.Root.localRotation = Quaternion.AngleAxis(p * 12f, fall.Lean[i]) * fall.Rotation[i];
                    if (piece.Roof == null) continue;
                    // It tips about its own middle, not the building's foot, and drops in between the walls.
                    var q = Mathf.Clamp01(p * 1.7f);
                    var tip = Quaternion.AngleAxis(q * 18f, fall.Lean[i]);
                    var middle = Vector3.up * (piece.H * 0.85f);
                    piece.Roof.localRotation = tip;
                    piece.Roof.localPosition = middle - tip * middle + Vector3.down * (q * q * piece.H * 0.55f);
                }
                if (!fall.Struck && p >= 0.45f)
                {
                    fall.Struck = true;
                    if (fall.Item.Ruin != null) fall.Item.Ruin.SetActive(true);
                    var random = new System.Random(seed + (int)fall.Item.Kind * 59);
                    Dust(fall.Item, 1.2f, random);
                    Tiles(fall.Item, random);
                }
                if (p < 1f) continue;
                Restore(fall);
                fall.Item.Root.SetActive(false);
                falling.RemoveAt(k);
            }
        }

        /// <summary>Puts the pieces back where they stood, hidden, for the day the building is raised again.</summary>
        static void Restore(Falling fall)
        {
            for (var i = 0; i < fall.Item.Pieces.Count; i++)
            {
                var piece = fall.Item.Pieces[i];
                piece.Root.localPosition = fall.Position[i];
                piece.Root.localRotation = fall.Rotation[i];
                if (piece.Roof == null) continue;
                piece.Roof.localPosition = Vector3.zero;
                piece.Roof.localRotation = Quaternion.identity;
            }
        }

        void StopFalling()
        {
            foreach (var fall in falling) Restore(fall);
            falling.Clear();
        }

        /// <summary>Clouds of limestone and plaster dust rising off a falling building's pieces.</summary>
        void Dust(Structure item, float strength, System.Random random)
        {
            var system = DustSystem();
            if (system == null) return;
            foreach (var piece in item.Pieces)
            {
                var count = Mathf.Clamp(Mathf.RoundToInt(piece.W * piece.D * 0.5f * strength), 8, 60);
                for (var i = 0; i < count; i++)
                {
                    var local = new Vector3(((float)random.NextDouble() - 0.5f) * piece.W, (float)random.NextDouble() * piece.H * 0.6f, ((float)random.NextDouble() - 0.5f) * piece.D);
                    var at = piece.Root.TransformPoint(local);
                    var outward = at - piece.Root.position;
                    outward.y = 0f;
                    system.Emit(new ParticleSystem.EmitParams
                    {
                        position = at,
                        velocity = outward.normalized * Mathf.Lerp(0.4f, 1.6f, (float)random.NextDouble()) * strength + Vector3.up * Mathf.Lerp(0.6f, 2.2f, (float)random.NextDouble()),
                        startSize = Mathf.Lerp(1.6f, 4.2f, (float)random.NextDouble()) * strength,
                        startLifetime = Mathf.Lerp(2.6f, 4.6f, (float)random.NextDouble()),
                        rotation = (float)random.NextDouble() * 360f,
                        applyShapeToPosition = false,
                    }, 1);
                }
            }
        }

        /// <summary>The roof's tiles thrown off as it strikes: they tumble out and down.</summary>
        void Tiles(Structure item, System.Random random)
        {
            var system = TileSystem();
            if (system == null) return;
            foreach (var piece in item.Pieces)
            {
                if (piece.Roof == null) continue;
                var count = Mathf.Clamp(Mathf.RoundToInt(piece.W * piece.D * 0.6f), 6, 40);
                for (var i = 0; i < count; i++)
                {
                    var local = new Vector3(((float)random.NextDouble() - 0.5f) * piece.W, piece.H * 0.7f, ((float)random.NextDouble() - 0.5f) * piece.D);
                    var at = piece.Root.TransformPoint(local);
                    var outward = at - piece.Root.position;
                    outward.y = 0f;
                    system.Emit(new ParticleSystem.EmitParams
                    {
                        position = at,
                        velocity = outward.normalized * Mathf.Lerp(1f, 3.5f, (float)random.NextDouble()) + Vector3.up * Mathf.Lerp(0.5f, 3f, (float)random.NextDouble()),
                        rotation3D = new Vector3((float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f),
                        angularVelocity3D = new Vector3(300f, 200f, 260f) * ((float)random.NextDouble() - 0.5f),
                        startSize = 1f,
                        startLifetime = Mathf.Lerp(0.8f, 1.3f, (float)random.NextDouble()),
                        applyShapeToPosition = false,
                    }, 1);
                }
            }
        }

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
