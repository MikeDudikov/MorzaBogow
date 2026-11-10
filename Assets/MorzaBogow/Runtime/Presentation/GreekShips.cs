using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The few paints of a ship. A mesh lists them per submesh; the renderer takes the matching materials. The last five
    /// dress the stays and the gear on deck (rope, linen, wicker, stone anchors, terracotta jars); the asset export folds
    /// them into the six paints of its budget.
    /// </summary>
    public enum GreekMaterial { Pitch, Ochre, Paint, Wood, Bronze, Sail, Rope, Linen, Wicker, Stone, Terracotta }

    /// <summary>A mesh and the paint of each of its submeshes, in submesh order.</summary>
    public sealed class GreekMesh
    {
        public Mesh Mesh;
        public GreekMaterial[] Slots;
    }

    /// <summary>
    /// The proportions of one class's ship after a type of the Aegean in the 12th century BC, the late Bronze Age of the
    /// Mycenaeans and the war at Troy: a rowing boat, a thirty-oared galley with a spur at the forefoot, a galley with
    /// raised platforms for archers, a fifty-oared galley with a latticed fighting rail, a round merchantman like the
    /// Uluburun ship, a galley with great bows on its bow castle and a heavy galley with castles fore and aft. Length,
    /// beam and draft come from the simulation's catalogue; everything else only shapes the picture.
    /// </summary>
    public sealed class GreekShipForm
    {
        public ShipClass Class;
        public string TypeName;
        public float Length, Beam, Draft;
        /// <summary>Width of the hull itself; oars reach out beyond it.</summary>
        public float HullBeam;
        /// <summary>Deck height above the empty waterline, amidships.</summary>
        public float Freeboard;
        /// <summary>Height of the bulwark above the deck (open boats and merchantmen sit deep inside their hull).</summary>
        public float Bulwark;
        public int Banks, OarsPerBank;
        /// <summary>Raised platforms (ikria) at bow and stern, their height above the gunwale; 0 for none.</summary>
        public float Platforms;
        /// <summary>Castles fore and aft (a heavy galley), much taller than platforms.</summary>
        public bool Castles;
        /// <summary>The open latticed rail of a fighting galley, its height above the gunwale; 0 for none.</summary>
        public float Lattice;
        /// <summary>A merchantman's wickerwork fence along the sides, its height; 0 for none.</summary>
        public float Fence;
        public bool CrowsNest, Cargo, BirdAtStern;
        /// <summary>A wooden spur at the forefoot (the class can ram); its length.</summary>
        public bool Ram;
        public float RamLength;
        public float SternRise, BowRise;
        /// <summary>Section shape: lower is rounder and fuller (a merchantman), higher is a lean galley's.</summary>
        public float Fullness;
        public float MastAt, MastHeight, SailWidth, SailHeight;
        /// <summary>Wood showing above the waterline instead of pitch (merchantmen).</summary>
        public bool WoodenTopsides;
    }

    /// <summary>Everything one class's ship is drawn from, in the ship's own space: metres, +Z bow, waterline Y = 0.</summary>
    public sealed class GreekShipParts
    {
        public GreekShipForm Form;
        public GreekMesh Hull, Deck, Ram, Rig, Steering, Sail;
        /// <summary>Hull and deck cut into stern, middle and bow pieces with ragged ends, for a ship that breaks up.</summary>
        public GreekMesh[] Sections;
        /// <summary>
        /// One oar mesh per bank, blade toward +X: the outboard part (OarLength) centred on the mesh's origin, as before,
        /// and the loom and handle reaching OarInboard further toward -X, inboard of the thole pin, where the rower holds it.
        /// </summary>
        public GreekMesh[] Oars;
        public float[] OarLength;
        /// <summary>Length of the oar inboard of its thole pin to the end of the handle, per bank (metres).</summary>
        public float[] OarInboard;
        /// <summary>Thole pins on the starboard side; port ones mirror X. Bank of each and its downward angle in degrees.</summary>
        public Vector3[] OarPins;
        public int[] OarBank;
        public float[] OarTilt;
        /// <summary>
        /// Where the rower of each starboard thole pin sits facing aft (his hips) or, at a merchantman's sweeps, stands (the
        /// deck between his feet); port ones mirror X. He holds the handle at mid-stroke in front of him.
        /// </summary>
        public Vector3[] RowerSeats;
        /// <summary>Rowers stand at their oars (a merchantman's few sweeps behind its fence) instead of sitting.</summary>
        public bool RowersStand;
        /// <summary>
        /// The steering oars on their own, port (0) then starboard (1): each mesh lies around its pivot, where the oar is
        /// lashed to the quarter; SteeringAxes is the shaft's direction from the tiller down to the blade, about which the
        /// helmsman turns the oar. SteeringMounts are the brackets that hold the pivots, which do not turn.
        /// </summary>
        public GreekMesh[] SteeringOars;
        public GreekMesh SteeringMounts;
        public Vector3[] SteeringPivots, SteeringAxes;
        /// <summary>The end of each tiller, port then starboard, where the helmsman's hands hold it with the oars at rest.</summary>
        public Vector3[] TillerGrips;
        /// <summary>
        /// Where each helmsman works, facing the bow: one between both tillers, or one at each oar on a broad heavy galley.
        /// Standing, the point is the deck or platform under his feet; seated (in a boat), the top of his seat.
        /// </summary>
        public Vector3[] HelmStands;
        public bool HelmSeated;
        public float DeckHeight;
        /// <summary>Top of the sail under the yard, and the sail's full size; the sail mesh is 1 x 1 hanging down from Y = 0.</summary>
        public Vector3 SailTop;
        public Vector2 SailSize;
        public Vector3 MastFoot;
        public Vector3[] Mounts;
        public Vector3 Stem;
    }

    /// <summary>
    /// Aegean ships of the 12th century BC for every class, built from simple geometry into a few meshes: a pitch-black
    /// hull ("black ships") with a red ochre wale and bow ("red-cheeked") and a white zigzag along it, a tall stem post
    /// crowned with a bird's head, a stern post curling up and inboard, a wooden spur at the forefoot of the galleys
    /// that ram, one bank of oars, a steering oar on each quarter, raised platforms fore and aft, a single mast with a
    /// crow's nest, a yard and a loose-footed brailed square sail, and what marks each type (latticed rail, archers'
    /// platforms, castles, a wickerwork fence with jars and oxhide ingots). Deterministic and cached; presentation only.
    /// The asset pipeline exports the same meshes, so its models match the game.
    /// </summary>
    public static class GreekShips
    {
        /// <summary>sRGB colours of the paints; the scene's materials and the fallbacks use the same values.</summary>
        public static class Palette
        {
            public static readonly Color Pitch = new Color(0.075f, 0.062f, 0.055f);
            public static readonly Color Ochre = new Color(0.64f, 0.23f, 0.12f);
            public static readonly Color Paint = new Color(0.93f, 0.90f, 0.82f);
            public static readonly Color Wood = new Color(0.56f, 0.41f, 0.25f);
            public static readonly Color Bronze = new Color(0.74f, 0.50f, 0.24f);
            public static readonly Color Linen = new Color(0.89f, 0.85f, 0.75f);
            public static readonly Color Rope = new Color(0.64f, 0.54f, 0.37f);
            public static readonly Color Wicker = new Color(0.68f, 0.55f, 0.34f);
            public static readonly Color Stone = new Color(0.74f, 0.71f, 0.64f);
            public static readonly Color Terracotta = new Color(0.64f, 0.36f, 0.23f);

            public static Color Of(GreekMaterial paint) => paint switch
            {
                GreekMaterial.Pitch => Pitch,
                GreekMaterial.Ochre => Ochre,
                GreekMaterial.Paint => Paint,
                GreekMaterial.Bronze => Bronze,
                GreekMaterial.Sail or GreekMaterial.Linen => Linen,
                GreekMaterial.Rope => Rope,
                GreekMaterial.Wicker => Wicker,
                GreekMaterial.Stone => Stone,
                GreekMaterial.Terracotta => Terracotta,
                _ => Wood,
            };
        }

        public const int Details = 3;
        static readonly Dictionary<(ShipClass, int), GreekShipParts> cache = new Dictionary<(ShipClass, int), GreekShipParts>();
        static readonly Dictionary<SlotSize, GreekMesh> weapons = new Dictionary<SlotSize, GreekMesh>();
        static readonly Dictionary<string, GreekMesh> props = new Dictionary<string, GreekMesh>();

        public static GreekShipForm Form(ShipClass shipClass)
        {
            var definition = ShipCatalog.Get(shipClass);
            var form = new GreekShipForm
            {
                Class = shipClass,
                Length = (float)definition.Radius * 2f,
                Beam = (float)definition.Beam,
                Draft = (float)definition.Draft,
                Ram = definition.CanRam,
            };
            var b = form.Beam;
            switch (shipClass)
            {
                case ShipClass.Cutter:
                    Set(form, "łódź", 0.95f, 0.75f + 0.12f * b, 0.32f, 4, 0.62f, 0.56f, 0.5f, 0.42f, 0.32f);
                    form.SternRise = 0.8f; form.BowRise = 0.4f;
                    break;
                case ShipClass.Ram:
                    Set(form, "triakontoros", 0.8f, 0.95f + 0.1f * b, 0.1f, 7, 0.74f, 0.5f, 0.5f, 0.48f, 0.3f);
                    form.Platforms = 0.35f; form.CrowsNest = true; form.SternRise = 1.2f; form.BowRise = 0.5f;
                    break;
                case ShipClass.CrewDestroyer:
                    Set(form, "galera łuczników", 0.82f, 1.0f + 0.1f * b, 0.1f, 7, 0.72f, 0.5f, 0.48f, 0.46f, 0.3f);
                    form.Platforms = 0.75f; form.CrowsNest = true; form.SternRise = 1.15f; form.BowRise = 0.45f;
                    break;
                case ShipClass.Boarder:
                    Set(form, "pentekontoros", 0.86f, 1.15f + 0.11f * b, 0.1f, 9, 0.76f, 0.48f, 0.42f, 0.42f, 0.28f);
                    form.Platforms = 0.5f; form.Lattice = 1.0f; form.CrowsNest = true; form.BirdAtStern = true; form.SternRise = 1.0f; form.BowRise = 0.4f;
                    break;
                case ShipClass.Merchant:
                    Set(form, "statek kupiecki", 0.94f, 1.3f + 0.13f * b, 0.4f, 2, 0.5f, 0.55f, 0.5f, 0.55f, 0.36f);
                    form.Fence = 0.9f; form.Cargo = true; form.SternRise = 1.2f; form.BowRise = 0.6f; form.WoodenTopsides = true;
                    break;
                case ShipClass.ShipKiller:
                    Set(form, "galera wielkich łuków", 0.82f, 1.05f + 0.1f * b, 0.1f, 7, 0.74f, 0.45f, 0.45f, 0.44f, 0.29f);
                    form.Platforms = 0.85f; form.CrowsNest = true; form.SternRise = 1.15f; form.BowRise = 0.45f;
                    break;
                default: // Bomber and any later heavy class
                    Set(form, "galera z kasztelami", 0.88f, 1.25f + 0.12f * b, 0.1f, 8, 0.8f, 0.5f, 0.4f, 0.4f, 0.26f);
                    form.Castles = true; form.Platforms = 1.8f; form.CrowsNest = true; form.BirdAtStern = true; form.SternRise = 0.9f; form.BowRise = 0.35f;
                    break;
            }
            form.Banks = 1;
            form.RamLength = form.Ram ? form.Length * (shipClass == ShipClass.Ram ? 0.09f : 0.06f) : 0f;
            return form;
        }

        static void Set(GreekShipForm form, string type, float hullBeam, float freeboard, float bulwark, int oars, float fullness,
                        float mastAt, float mastHeight, float sailWidth, float sailHeight)
        {
            form.TypeName = type;
            form.HullBeam = form.Beam * hullBeam;
            form.Freeboard = freeboard;
            form.Bulwark = bulwark;
            form.OarsPerBank = oars;
            form.Fullness = fullness;
            form.MastAt = mastAt;
            form.MastHeight = form.Length * mastHeight;
            form.SailWidth = form.Length * sailWidth;
            form.SailHeight = form.Length * sailHeight;
        }

        // Hull lines -----------------------------------------------------------------------------------------------
        /// <summary>Where the hull ends at the bow: before the spur's tip, or before the stem post and its bird's head, so the
        /// whole ship is exactly as long as the simulation's hull.</summary>
        static float BowEnd(GreekShipForm f) => f.Length * 0.5f - (f.Ram ? f.RamLength : StemReach(f));

        static float StemReach(GreekShipForm f) => 0.3f + (f.Class == ShipClass.Merchant ? 0f : (f.Length * 0.014f + 0.14f) * 1.8f);
        public static float Z(GreekShipForm f, float s) => Mathf.Lerp(-f.Length * 0.5f, BowEnd(f), s);

        public static float HalfWidth(GreekShipForm f, float s)
        {
            var shape = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(s)));
            var round = f.Fullness < 0.6f;
            var power = s < 0.5f ? (round ? 0.32f : 0.45f) : (round ? 0.42f : 0.62f);
            return f.HullBeam * 0.5f * Mathf.Max(0.04f, Mathf.Pow(shape, power));
        }

        public static float Depth(GreekShipForm f, float s)
        {
            var shape = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(s))), f.Fullness < 0.6f ? 0.4f : 0.55f);
            return f.Draft * ((f.Fullness < 0.6f ? 0.45f : 0.3f) + (f.Fullness < 0.6f ? 0.55f : 0.7f) * shape);
        }

        public static float Sheer(GreekShipForm f, float s)
        {
            var stern = Mathf.Clamp01((0.18f - s) / 0.18f);
            var bow = Mathf.Clamp01((s - 0.86f) / 0.14f);
            return f.Freeboard * (1f + 0.12f * (2f * s - 1f) * (2f * s - 1f) + f.SternRise * stern * stern + f.BowRise * bow * bow);
        }

        /// <summary>A point on the port side of the section at s: t = 0 at the gunwale, 1 at the keel.</summary>
        public static Vector3 Section(GreekShipForm f, float s, float t)
        {
            var phi = Mathf.PI * 0.5f * Mathf.Clamp01(t);
            var w = HalfWidth(f, s);
            var h = Sheer(f, s);
            var d = Depth(f, s);
            return new Vector3(-w * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(phi)), f.Fullness), h - (h + d) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phi)), 1.25f), Z(f, s));
        }

        public static float DeckAt(GreekShipForm f, float s) => Sheer(f, s) - f.Bulwark;

        // Parts ----------------------------------------------------------------------------------------------------
        public static GreekShipParts Parts(ShipClass shipClass, int detail = 0)
        {
            detail = Mathf.Clamp(detail, 0, Details - 1);
            if (cache.TryGetValue((shipClass, detail), out var parts) && parts.Hull.Mesh != null) return parts;
            parts = Build(Form(shipClass), detail);
            cache[(shipClass, detail)] = parts;
            return parts;
        }

        static GreekShipParts Build(GreekShipForm f, int detail)
        {
            var parts = new GreekShipParts { Form = f };
            var stations = detail == 0 ? 32 : detail == 1 ? 16 : 8;
            var ring = detail == 0 ? 6 : detail == 1 ? 4 : 2;
            var cuts = new[] { -f.Length * 0.5f + f.Length * 0.36f, -f.Length * 0.5f + f.Length * 0.68f };

            // The painted band along the wale is its own mesh (Band), so a workshop's pattern can differ on the same hull.
            var hull = new MeshBuilder(cuts);
            BuildShell(hull, f, stations, ring);
            BuildStern(hull, f, detail);
            BuildStem(hull, f, detail);

            var deck = new MeshBuilder(cuts);
            BuildDeck(deck, f, stations, detail);

            parts.Hull = hull.Build(GreekMaterial.Pitch, GreekMaterial.Ochre, GreekMaterial.Paint, GreekMaterial.Wood);
            parts.Deck = deck.Build(GreekMaterial.Wood, GreekMaterial.Ochre, GreekMaterial.Pitch, GreekMaterial.Bronze, GreekMaterial.Paint);
            parts.Sections = new GreekMesh[3];
            for (var i = 0; i < 3; i++)
            {
                var piece = new MeshBuilder(cuts);
                piece.Append(hull, i);
                piece.Append(deck, i);
                AddBreakCaps(piece, f, i, cuts);
                parts.Sections[i] = piece.Build(GreekMaterial.Pitch, GreekMaterial.Ochre, GreekMaterial.Paint, GreekMaterial.Wood, GreekMaterial.Bronze);
            }

            if (f.Ram)
            {
                var spur = new MeshBuilder(null);
                BuildSpur(spur, f, detail);
                parts.Ram = spur.Build(GreekMaterial.Pitch, GreekMaterial.Ochre);
            }

            var rig = new MeshBuilder(null);
            BuildRig(rig, f, detail, parts);
            parts.Rig = rig.Build(GreekMaterial.Wood, GreekMaterial.Ochre, GreekMaterial.Rope);

            BuildSteering(f, parts);

            var sail = new MeshBuilder(null);
            BuildSail(sail, f, detail);
            parts.Sail = sail.Build(GreekMaterial.Sail, GreekMaterial.Paint);

            BuildOars(f, parts, detail);
            parts.DeckHeight = DeckAt(f, 0.5f);
            parts.Mounts = MountPoints(f);
            parts.Stem = new Vector3(0f, 0.2f, f.Length * 0.5f);
            return parts;
        }

        static GreekMaterial ShellPaint(GreekShipForm f, float s, float y, bool wale)
        {
            if (wale) return GreekMaterial.Ochre;
            if (f.WoodenTopsides) return y > 0.25f ? GreekMaterial.Wood : GreekMaterial.Pitch;
            // "Red-cheeked": the bow above the water is painted with red ochre.
            return s > 0.84f && y > 0.1f ? GreekMaterial.Ochre : GreekMaterial.Pitch;
        }

        static void BuildShell(MeshBuilder b, GreekShipForm f, int stations, int ring)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var rows = new int[stations + 1, ring + 1];
                for (var i = 0; i <= stations; i++)
                {
                    var s = i / (float)stations;
                    var girth = 0f;
                    var previous = Section(f, s, 0f);
                    for (var k = 0; k <= ring; k++)
                    {
                        var p = Section(f, s, k / (float)ring);
                        girth += Vector3.Distance(p, previous);
                        previous = p;
                        var placed = p;
                        placed.x *= -side;
                        // Metres along the hull and down from the gunwale: the strakes run along u, as planks do.
                        rows[i, k] = b.Vertex(placed, new Vector2(p.z, girth));
                    }
                }
                for (var i = 0; i < stations; i++)
                {
                    var s = (i + 0.5f) / stations;
                    var axis = (Sheer(f, s) - Depth(f, s)) * 0.5f;
                    for (var k = 0; k < ring; k++)
                    {
                        var mid = (b.Position(rows[i, k]) + b.Position(rows[i + 1, k + 1])) * 0.5f;
                        var paint = ShellPaint(f, s, mid.y, k == 0);
                        b.Quad(paint, rows[i, k], rows[i + 1, k], rows[i + 1, k + 1], rows[i, k + 1], new Vector3(mid.x, mid.y - axis, 0f));
                    }
                }
            }
            // The ends are closed by fans from the section's middle.
            foreach (var (s, direction) in new[] { (0f, -1f), (1f, 1f) })
            {
                var centre = new Vector3(0f, (Sheer(f, s) - Depth(f, s)) * 0.5f, Z(f, s));
                var c = b.Vertex(centre, new Vector2(centre.x, centre.y));
                var ids = new List<int>();
                for (var k = 0; k <= ring; k++)
                {
                    var p = Section(f, s, k / (float)ring);
                    ids.Add(b.Vertex(p, new Vector2(p.x, p.y)));
                }
                for (var k = ring - 1; k >= 0; k--)
                {
                    var p = Section(f, s, k / (float)ring);
                    p.x = -p.x;
                    ids.Add(b.Vertex(p, new Vector2(p.x, p.y)));
                }
                for (var j = 0; j < ids.Count; j++) b.Triangle(GreekMaterial.Pitch, c, ids[j], ids[(j + 1) % ids.Count], new Vector3(0f, 0f, direction));
            }
        }

        /// <summary>The approved patterns painted along the red wale.</summary>
        public static readonly string[] BandPatterns = { "zigzag", "wave", "none" };

        static readonly Dictionary<(ShipClass, string, int), GreekMesh> bands = new Dictionary<(ShipClass, string, int), GreekMesh>();

        /// <summary>
        /// The white pattern painted on the red wale, a mesh of its own so one hull can carry its workshop's band: a zigzag
        /// or a wavy line (motifs of the pottery of the age, an interpretation for hulls), or none (null). The lowest detail
        /// has no band.
        /// </summary>
        public static GreekMesh Band(ShipClass shipClass, string pattern, int detail = 0)
        {
            detail = Mathf.Clamp(detail, 0, Details - 1);
            if (detail == 2 || string.IsNullOrEmpty(pattern) || pattern == "none") return null;
            if (bands.TryGetValue((shipClass, pattern, detail), out var made) && made.Mesh != null) return made;
            var f = Form(shipClass);
            var b = new MeshBuilder(null);
            const float top = 0.12f / 6f, bottom = 0.85f / 6f;
            for (var side = -1; side <= 1; side += 2)
            {
                Vector3 On(float s, float t)
                {
                    var p = Section(f, s, t);
                    p.x *= -side;
                    return p + new Vector3(side * 0.025f, 0f, 0f);
                }
                var outward = new Vector3(side, 0f, 0f);
                if (pattern == "wave")
                {
                    // A wavy line: a thin painted band swinging between the edges of the wale.
                    var steps = detail == 0 ? 96 : 48;
                    var mid = (top + bottom) * 0.5f;
                    var swing = (bottom - top) * 0.36f;
                    var half = (bottom - top) * 0.13f;
                    int upperBefore = -1, lowerBefore = -1;
                    for (var i = 0; i <= steps; i++)
                    {
                        var s = Mathf.Lerp(0.2f, 0.8f, i / (float)steps);
                        var centre = mid + swing * Mathf.Sin(i / (float)steps * 9f * Mathf.PI * 2f);
                        var upperAt = On(s, centre - half);
                        var lowerAt = On(s, centre + half);
                        var upper = b.Vertex(upperAt, new Vector2(upperAt.z, 0f));
                        var lower = b.Vertex(lowerAt, new Vector2(lowerAt.z, 0.1f));
                        if (i > 0) b.Quad(GreekMaterial.Paint, upperBefore, upper, lower, lowerBefore, outward);
                        upperBefore = upper;
                        lowerBefore = lower;
                    }
                    continue;
                }
                var teeth = detail == 0 ? 18 : 9;
                for (var i = 0; i < teeth; i++)
                {
                    var s0 = Mathf.Lerp(0.2f, 0.8f, i / (float)teeth);
                    var s1 = Mathf.Lerp(0.2f, 0.8f, (i + 1f) / teeth);
                    var low = i % 2 == 0;
                    var pa = On(s0, low ? top : bottom);
                    var pc = On(s1, low ? top : bottom);
                    var pd = On((s0 + s1) * 0.5f, low ? bottom : top);
                    b.Triangle(GreekMaterial.Paint, b.Vertex(pa, new Vector2(pa.z, 0f)), b.Vertex(pc, new Vector2(pc.z, 0f)), b.Vertex(pd, new Vector2(pd.z, 0.1f)), outward);
                }
            }
            made = b.Build(GreekMaterial.Paint);
            made.Mesh.name = $"Greek ship band ({shipClass}, {pattern})";
            bands[(shipClass, pattern, detail)] = made;
            return made;
        }

        /// <summary>The stern post curling up and inboard, as on the ships painted on Mycenaean kraters.</summary>
        static void BuildStern(MeshBuilder b, GreekShipForm f, int detail)
        {
            var top = new Vector3(0f, Sheer(f, 0f) - 0.15f, Z(f, 0f) + 0.1f);
            var radius = f.Length * 0.035f * Mathf.Lerp(0.8f, 1.2f, Mathf.InverseLerp(0.8f, 1.4f, f.SternRise));
            var steps = detail == 0 ? 10 : 5;
            var path = new List<Vector3>();
            for (var j = 0; j <= steps; j++)
            {
                var theta = Mathf.Deg2Rad * 170f * j / steps;
                var r = radius * (1f - 0.35f * j / steps);
                path.Add(top + new Vector3(0f, r * Mathf.Sin(theta) + f.Freeboard * 0.25f * j / steps, r * (1f - Mathf.Cos(theta))));
            }
            b.Ribbon(GreekMaterial.Pitch, path, 0.32f * Mathf.Max(0.6f, f.HullBeam / 6f), 0.16f, 0.45f);
            if (f.BirdAtStern) BirdHead(b, path[path.Count - 1] + Vector3.up * 0.05f, -1f, f.Length * 0.012f + 0.12f, detail);
        }

        /// <summary>The tall stem post crowned with a bird's head looking out ahead.</summary>
        static void BuildStem(MeshBuilder b, GreekShipForm f, int detail)
        {
            var bow = new Vector3(0f, Sheer(f, 1f) - 0.1f, Z(f, 1f));
            var rise = f.Freeboard * (f.Class == ShipClass.Merchant ? 0.55f : 0.95f);
            var path = new List<Vector3>();
            var steps = detail == 0 ? 6 : 3;
            for (var j = 0; j <= steps; j++)
            {
                var u = j / (float)steps;
                path.Add(bow + new Vector3(0f, rise * u, 0.15f + 0.12f * u * u));
            }
            b.Ribbon(GreekMaterial.Pitch, path, 0.26f, 0.18f, 0.75f);
            if (f.Class != ShipClass.Merchant) BirdHead(b, path[path.Count - 1] + Vector3.up * 0.05f, 1f, f.Length * 0.014f + 0.14f, detail);
        }

        /// <summary>A bird's head on a post: head, open beak, a small crest and a white eye with a dark pupil on each side.</summary>
        static void BirdHead(MeshBuilder b, Vector3 neck, float facing, float size, int detail)
        {
            var head = neck + new Vector3(0f, size * 1.3f, facing * size * 0.4f);
            b.Box(GreekMaterial.Pitch, neck + new Vector3(0f, size * 0.6f, facing * size * 0.15f), new Vector3(size * 0.3f, size * 0.7f, size * 0.3f), Quaternion.Euler(facing * 15f, 0f, 0f));
            b.Box(GreekMaterial.Pitch, head, new Vector3(size * 0.38f, size * 0.42f, size * 0.62f), Quaternion.identity);
            var beak = head + new Vector3(0f, -size * 0.08f, facing * size * 0.95f);
            b.Box(GreekMaterial.Ochre, beak, new Vector3(size * 0.16f, size * 0.12f, size * 0.42f), Quaternion.Euler(facing * 12f, 0f, 0f));
            if (detail < 2)
            {
                b.Box(GreekMaterial.Ochre, head + new Vector3(0f, size * 0.55f, -facing * size * 0.2f), new Vector3(size * 0.08f, size * 0.22f, size * 0.42f), Quaternion.Euler(-facing * 20f, 0f, 0f));
                foreach (var side in new[] { -1f, 1f })
                {
                    var eye = head + new Vector3(side * size * 0.39f, size * 0.12f, facing * size * 0.25f);
                    b.Ellipse(GreekMaterial.Paint, eye, Vector3.forward, Vector3.up, new Vector3(side, 0f, 0f), size * 0.16f, size * 0.12f, 8);
                    b.Ellipse(GreekMaterial.Pitch, eye + new Vector3(side * 0.01f, 0f, 0f), Vector3.forward, Vector3.up, new Vector3(side, 0f, 0f), size * 0.07f, size * 0.07f, 6);
                }
            }
        }

        static void BuildDeck(MeshBuilder b, GreekShipForm f, int stations, int detail)
        {
            var from = Mathf.Max(1, Mathf.RoundToInt(stations * 0.04f));
            var to = stations - from;
            // Deck planks run fore and aft: UV in metres along (z) and across (x); the bulwark's inside along (z) and up (y).
            var edges = new List<(int left, int right, int footLeft, int footRight, int wallLeft, int wallRight)>();
            for (var i = from; i <= to; i++)
            {
                var s = i / (float)stations;
                var w = HalfWidth(f, s) * 0.97f;
                var y = DeckAt(f, s);
                var gunwale = Sheer(f, s);
                var z = Z(f, s);
                edges.Add((b.Vertex(new Vector3(-w, y, z), new Vector2(z, -w)), b.Vertex(new Vector3(w, y, z), new Vector2(z, w)),
                           b.Vertex(new Vector3(-w, y, z), new Vector2(z, y)), b.Vertex(new Vector3(w, y, z), new Vector2(z, y)),
                           b.Vertex(new Vector3(-w, gunwale, z), new Vector2(z, gunwale)), b.Vertex(new Vector3(w, gunwale, z), new Vector2(z, gunwale))));
            }
            for (var i = 0; i + 1 < edges.Count; i++)
            {
                var (l0, r0, fl0, fr0, wl0, wr0) = edges[i];
                var (l1, r1, fl1, fr1, wl1, wr1) = edges[i + 1];
                b.Quad(GreekMaterial.Wood, l0, l1, r1, r0, Vector3.up);
                // The inside of the bulwark, so the hollow above the deck is never seen through.
                b.Quad(GreekMaterial.Wood, fl0, fl1, wl1, wl0, Vector3.right);
                b.Quad(GreekMaterial.Wood, fr0, fr1, wr1, wr0, Vector3.left);
            }

            if (f.Class == ShipClass.Cutter || f.Class == ShipClass.Merchant)
            {
                // Thwarts across an open boat, where its rowers sit facing aft and its helmsman aft of them; the beams of a
                // merchantman's hold.
                var thwarts = new List<float>();
                if (f.Class == ShipClass.Cutter)
                {
                    thwarts.Add(Helm(f).s);
                    for (var i = 0; i < f.OarsPerBank; i++)
                        if (detail < 2 || i == 1 || i == 2) thwarts.Add(OarStation(f, i, f.OarsPerBank) + SeatForward / Span(f));
                }
                else thwarts.AddRange(new[] { 0.3f, 0.72f });
                foreach (var s in thwarts)
                {
                    var w = HalfWidth(f, s) * 0.95f;
                    b.Box(GreekMaterial.Wood, new Vector3(0f, DeckAt(f, s) + f.Bulwark * 0.45f, Z(f, s)), new Vector3(w, 0.06f, 0.18f), Quaternion.identity);
                }
            }

            if (f.Platforms > 0f)
            {
                // Raised platforms at bow and stern where the fighting men stand.
                foreach (var (s0, s1, height) in new[] { (0.05f, 0.18f, f.Platforms * (f.Castles ? 1f : 0.85f)), (0.82f, 0.95f, f.Platforms) })
                {
                    var mid = (s0 + s1) * 0.5f;
                    var w = HalfWidth(f, mid) * 0.9f;
                    var y = Sheer(f, mid) + height;
                    var half = (Z(f, s1) - Z(f, s0)) * 0.5f;
                    var centre = new Vector3(0f, y - 0.1f, (Z(f, s0) + Z(f, s1)) * 0.5f);
                    b.Box(GreekMaterial.Wood, centre, new Vector3(w, 0.1f, half), Quaternion.identity);
                    var below = y - DeckAt(f, mid);
                    foreach (var x in new[] { -w * 0.85f, w * 0.85f })
                    foreach (var z in new[] { -half * 0.8f, half * 0.8f })
                        b.Box(GreekMaterial.Wood, new Vector3(x, DeckAt(f, mid) + below * 0.5f, centre.z + z), new Vector3(0.09f, below * 0.5f, 0.09f), Quaternion.identity);
                    if (f.Castles)
                    {
                        // A castle: screens of planks around the platform, painted at the top.
                        foreach (var sx in new[] { -1f, 1f })
                            b.Box(GreekMaterial.Wood, new Vector3(sx * w, y + 0.55f, centre.z), new Vector3(0.06f, 0.55f, half), Quaternion.identity);
                        foreach (var sz in new[] { -1f, 1f })
                            b.Box(GreekMaterial.Wood, new Vector3(0f, y + 0.55f, centre.z + sz * half), new Vector3(w, 0.55f, 0.06f), Quaternion.identity);
                        b.Box(GreekMaterial.Ochre, new Vector3(0f, y + 1.12f, centre.z + half), new Vector3(w, 0.07f, 0.08f), Quaternion.identity);
                        b.Box(GreekMaterial.Ochre, new Vector3(0f, y + 1.12f, centre.z - half), new Vector3(w, 0.07f, 0.08f), Quaternion.identity);
                    }
                    else Rail(b, f, s0, s1, height + 0.55f, 3);
                }
            }
            if (HasWalkway(f))
            {
                // The walkway over the rowers: planked along the middle on posts, so the rowers sit below it at the sides.
                var (w0, w1) = WalkwaySpan(f);
                var pieces = Mathf.Max(2, Mathf.RoundToInt((w1 - w0) * stations));
                for (var i = 0; i < pieces; i++)
                {
                    var sa = Mathf.Lerp(w0, w1, i / (float)pieces);
                    var sb = Mathf.Lerp(w0, w1, (i + 1f) / pieces);
                    var sm = (sa + sb) * 0.5f;
                    var half = WalkwayHalfWidth(f, sm);
                    var za = Z(f, sa);
                    var zb = Z(f, sb);
                    var top = WalkwayTop(f, sm);
                    b.Box(GreekMaterial.Wood, new Vector3(0f, top - 0.05f, (za + zb) * 0.5f), new Vector3(half, 0.05f, (zb - za) * 0.5f + 0.005f), Quaternion.identity);
                }
                var spacing = 2f / Span(f);
                for (var s = w0 + spacing * 0.25f; s <= w1; s += spacing)
                {
                    var half = WalkwayHalfWidth(f, s);
                    var floor = DeckAt(f, s);
                    var top = WalkwayTop(f, s) - 0.1f;
                    foreach (var side in new[] { -1f, 1f })
                        b.Box(GreekMaterial.Wood, new Vector3(side * (half - 0.06f), (floor + top) * 0.5f, Z(f, s)), new Vector3(0.05f, (top - floor) * 0.5f, 0.05f), Quaternion.identity);
                }
            }
            if (f.Lattice > 0f) Rail(b, f, 0.18f, 0.82f, f.Lattice, detail == 0 ? 18 : 9);
            if (f.Fence > 0f) Fence(b, f, detail);
            if (f.Cargo && detail < 2)
            {
                var jar = Jar(detail);
                var ingot = Prop("ingot");
                var rows = detail == 0 ? 3 : 2;
                var across = detail == 0 ? 3 : 2;
                for (var i = 0; i < rows; i++)
                for (var j = 0; j < across; j++)
                {
                    var s = Mathf.Lerp(0.28f, 0.42f, rows == 1 ? 0.5f : i / (rows - 1f));
                    var x = (j - (across - 1) * 0.5f) * HalfWidth(f, s) * 0.5f;
                    b.Append(jar, new Vector3(x, DeckAt(f, s), Z(f, s)), Quaternion.Euler(0f, i * 37f + j * 19f, (j - 1) * 4f));
                }
                // Copper oxhide ingots stacked crosswise forward of the mast.
                for (var layer = 0; layer < (detail == 0 ? 4 : 2); layer++)
                    b.Append(ingot, new Vector3(0f, DeckAt(f, 0.66f) + 0.03f + layer * 0.07f, Z(f, 0.66f)), Quaternion.Euler(0f, layer * 90f + 8f, 0f));
            }
        }

        /// <summary>An open rail of posts and a painted top rail along the sides between two stations, at a height above the gunwale.</summary>
        static void Rail(MeshBuilder b, GreekShipForm f, float s0, float s1, float height, int posts)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                for (var i = 0; i <= posts; i++)
                {
                    var s = Mathf.Lerp(s0, s1, i / (float)posts);
                    var x = HalfWidth(f, s) * 0.96f * side;
                    var y = Sheer(f, s);
                    b.Box(GreekMaterial.Wood, new Vector3(x, y + height * 0.5f, Z(f, s)), new Vector3(0.06f, height * 0.5f, 0.06f), Quaternion.identity);
                    if (i == posts) continue;
                    var sNext = Mathf.Lerp(s0, s1, (i + 1f) / posts);
                    var a = new Vector3(x, y + height, Z(f, s));
                    var c = new Vector3(HalfWidth(f, sNext) * 0.96f * side, Sheer(f, sNext) + height, Z(f, sNext));
                    var direction = c - a;
                    b.Box(GreekMaterial.Ochre, (a + c) * 0.5f, new Vector3(0.07f, 0.06f, direction.magnitude * 0.5f + 0.04f), Quaternion.LookRotation(direction.normalized));
                }
            }
        }

        /// <summary>A merchantman's wickerwork fence: close-set uprights between two rails along the whole side.</summary>
        static void Fence(MeshBuilder b, GreekShipForm f, int detail)
        {
            var uprights = detail == 0 ? 28 : 12;
            Rail(b, f, 0.12f, 0.9f, f.Fence, uprights);
            foreach (var side in new[] { -1f, 1f })
            {
                for (var i = 0; i < 6; i++)
                {
                    var s = Mathf.Lerp(0.12f, 0.9f, (i + 0.5f) / 6f);
                    var x = HalfWidth(f, s) * 0.97f * side;
                    var y = Sheer(f, s) + f.Fence * 0.45f;
                    b.Box(GreekMaterial.Ochre, new Vector3(x, y, Z(f, s)), new Vector3(0.03f, f.Fence * 0.32f, (Z(f, 0.9f) - Z(f, 0.12f)) / 12f), Quaternion.identity);
                }
            }
        }

        static readonly float[] CastleStations = { 0.115f, 0.885f };

        /// <summary>A wooden spur at the forefoot, sheathed in pitch, with a red band: the galleys' cutwater used to ram.</summary>
        static void BuildSpur(MeshBuilder b, GreekShipForm f, int detail)
        {
            var heavy = f.Class == ShipClass.Boarder || f.Class == ShipClass.Bomber ? 1.25f : 1f;
            var baseZ = BowEnd(f) - 0.3f;
            var tipZ = f.Length * 0.5f;
            var y = -0.15f * f.Draft;
            var halfW = 0.22f * heavy;
            var halfH = 0.32f * heavy;
            var body = new[]
            {
                new Vector3(-halfW, y - halfH, baseZ), new Vector3(halfW, y - halfH, baseZ), new Vector3(halfW, y + halfH, baseZ), new Vector3(-halfW, y + halfH, baseZ),
            };
            var tip = new Vector3(0f, y + halfH * 0.2f, tipZ);
            var ids = new int[4];
            for (var i = 0; i < 4; i++) ids[i] = b.Vertex(body[i], new Vector2(0.4f, 0.9f));
            var t = b.Vertex(tip, new Vector2(0.45f, 0.95f));
            for (var i = 0; i < 4; i++)
            {
                var mid = (body[i] + body[(i + 1) % 4]) * 0.5f;
                b.Triangle(GreekMaterial.Pitch, ids[i], ids[(i + 1) % 4], t, new Vector3(mid.x, mid.y - y, 0f));
            }
            if (detail < 2)
                b.Box(GreekMaterial.Ochre, new Vector3(0f, y + halfH + 0.02f, (baseZ + tipZ) * 0.5f - 0.2f), new Vector3(halfW * 0.6f, 0.03f, (tipZ - baseZ) * 0.3f), Quaternion.Euler(-4f, 0f, 0f));
        }

        static void BuildRig(MeshBuilder b, GreekShipForm f, int detail, GreekShipParts parts)
        {
            var z = Z(f, f.MastAt);
            var foot = new Vector3(0f, DeckAt(f, f.MastAt) - 0.1f, z);
            var top = foot + Vector3.up * f.MastHeight;
            var radius = 0.012f * f.Length + 0.08f;
            var sides = detail == 0 ? 8 : detail == 1 ? 6 : 4;
            b.Tube(GreekMaterial.Wood, new List<Vector3> { foot, top }, radius, radius * 0.6f, sides);
            var yardY = top.y - f.MastHeight * (f.CrowsNest ? 0.16f : 0.08f);
            var yardZ = z + radius * 1.5f;
            var half = f.SailWidth * 0.5f + 0.4f;
            // A single yard bent like a bow: the sail is loose-footed, brailed, with no boom below.
            b.Tube(GreekMaterial.Wood, new List<Vector3> { new Vector3(-half, yardY - 0.15f, yardZ), new Vector3(0f, yardY + 0.12f, yardZ), new Vector3(half, yardY - 0.15f, yardZ) },
                   radius * 0.55f, radius * 0.35f, sides);
            if (f.CrowsNest)
            {
                // The crow's nest at the masthead, from which a lookout watched and archers shot.
                var basket = new List<Vector3> { top + Vector3.down * 0.75f, top + Vector3.down * 0.15f };
                b.Tube(GreekMaterial.Wood, basket, radius * 3.4f, radius * 3.8f, sides);
                b.Tube(GreekMaterial.Ochre, new List<Vector3> { top + Vector3.down * 0.2f, top + Vector3.down * 0.1f }, radius * 3.9f, radius * 3.9f, sides);
            }
            if (detail < 2)
            {
                var stem = new Vector3(0f, Sheer(f, 1f), Z(f, 1f));
                var stern = new Vector3(0f, Sheer(f, 0.05f), Z(f, 0.05f));
                // Forestay and backstay: rope, not wood.
                b.Tube(GreekMaterial.Rope, new List<Vector3> { top, stem }, 0.035f, 0.035f, 3);
                b.Tube(GreekMaterial.Rope, new List<Vector3> { top, stern }, 0.035f, 0.035f, 3);
            }
            parts.MastFoot = foot;
            parts.SailTop = new Vector3(0f, yardY - 0.12f, yardZ + 0.15f);
            parts.SailSize = new Vector2(f.SailWidth, f.SailHeight);
        }

        // Rowing and steering -------------------------------------------------------------------------------------
        /// <summary>Metres from a rower's thole pin forward to his seat: he faces aft and holds the handle in front of him.</summary>
        public const float SeatForward = 0.48f;
        /// <summary>A rower's hands at mid-stroke above his seat, or above the deck where he stands.</summary>
        const float SeatedHands = 0.30f, StandingHands = 1.0f;
        /// <summary>A standing helmsman's hands on the tillers above the deck; a seated one's above his seat.</summary>
        const float HelmHands = 0.95f, SeatedHelmHands = 0.35f;
        /// <summary>The steering oar's loom above its pivot, up to the tiller.</summary>
        const float SteeringLoom = 0.6f;

        /// <summary>Metres along the hull for one unit of s (stern 0 to bow 1).</summary>
        static float Span(GreekShipForm f) => BowEnd(f) + f.Length * 0.5f;

        /// <summary>
        /// A galley's walkway: a narrow raised deck along the middle, over the rowers who sit at the sides below it, where
        /// the warriors stand, as the LH IIIC ship paintings put them above the rowers (Kynos, Bademgediği) and as the
        /// Sea Peoples' ships at Medinet Habu have a deck along their whole length but not their whole width (an
        /// interpretation; docs/art/HistoricalConstraints.md). Its top stands this far above the gunwale; the rowers keep
        /// this much of the beam at each side.
        /// </summary>
        public const float WalkwayRise = 0.35f, RowersRoom = 1.25f;

        public static bool HasWalkway(GreekShipForm f) => f.Platforms > 0f;

        /// <summary>Where the walkway runs (s): over the rowers' benches, from just aft of the first oar to just forward of the last.</summary>
        public static (float from, float to) WalkwaySpan(GreekShipForm f) =>
            (OarStation(f, 0, f.OarsPerBank) - 0.02f, OarStation(f, f.OarsPerBank - 1, f.OarsPerBank) + 0.02f);

        public static float WalkwayHalfWidth(GreekShipForm f, float s) => Mathf.Max(0.45f, HalfWidth(f, s) * 0.97f - RowersRoom);

        public static float WalkwayTop(GreekShipForm f, float s) => Sheer(f, s) + WalkwayRise;

        /// <summary>
        /// The floor under a point of the ship (s along, x across): a raised platform's top, the walkway's, or the deck
        /// (an open boat's bottom boards). Crews stand on it; gear and weapon posts stand on it.
        /// </summary>
        public static float FloorAt(GreekShipForm f, float s, float x)
        {
            if (f.Platforms > 0f && (s >= 0.05f && s <= 0.18f || s >= 0.82f && s <= 0.95f))
            {
                var stern = s < 0.5f;
                if (Mathf.Abs(x) <= HalfWidth(f, stern ? 0.115f : 0.885f) * 0.9f)
                    return Sheer(f, stern ? 0.115f : 0.885f) + f.Platforms * (stern && !f.Castles ? 0.85f : 1f);
            }
            if (HasWalkway(f))
            {
                var (from, to) = WalkwaySpan(f);
                if (s >= from && s <= to && Mathf.Abs(x) <= WalkwayHalfWidth(f, s)) return WalkwayTop(f, s);
            }
            return DeckAt(f, s);
        }

        /// <summary>The station (s) of thole pin i of n on one side.</summary>
        static float OarStation(GreekShipForm f, int i, int n) =>
            f.Class == ShipClass.Merchant ? Mathf.Lerp(0.66f, 0.74f, i) : Mathf.Lerp(0.26f, 0.74f, n == 1 ? 0.5f : i / (n - 1f));

        /// <summary>
        /// A thole pin's height: on the gunwale of a galley or merchantman, where the rowers sit low on deck with their bodies
        /// above the side, as LH IIIC paintings show them; in a notch of an open boat's top strake, above its thwarts.
        /// </summary>
        static float TholeHeight(GreekShipForm f, float s) => Sheer(f, s) + (f.Class == ShipClass.Cutter ? -0.15f : 0.05f);

        /// <summary>The top of an open boat's thwart at s.</summary>
        static float ThwartTop(GreekShipForm f, float s) => DeckAt(f, s) + f.Bulwark * 0.45f + 0.06f;

        /// <summary>
        /// Where a rower sits at s (the top of his seat): on a boat's thwart, on a low seat on a galley's deck; a merchantman's
        /// rowers stand behind its fence, so the deck under them.
        /// </summary>
        public static float RowerSeatHeight(GreekShipForm f, float s) =>
            f.Class == ShipClass.Cutter ? ThwartTop(f, s) : DeckAt(f, s) + (f.Class == ShipClass.Merchant ? 0f : 0.12f);

        /// <summary>
        /// Where each type's helmsman works (s), whether on the stern platform, seated (in a boat) and how many steer: two on
        /// the broad heavy galley, one at each oar; one between both tillers elsewhere. Where a machine stands on the stern
        /// platform the helmsman works on the deck just forward of it.
        /// </summary>
        static (float s, bool platform, bool seated, int men) Helm(GreekShipForm f) => f.Class switch
        {
            ShipClass.Cutter => (0.22f, false, true, 1),
            ShipClass.Merchant => (0.17f, false, false, 1),
            ShipClass.ShipKiller => (0.205f, false, false, 1),
            ShipClass.Bomber => (0.205f, false, false, 2),
            _ => (0.165f, true, false, 1),
        };

        /// <summary>
        /// A steering oar on each quarter, slanting aft into the water, lashed at a pivot that a bracket raises where the
        /// helmsman stands high; above the pivot its loom rises to a tiller that reaches his hands. Each oar is also built
        /// on its own around its pivot, so the view can turn it about its shaft as the helmsman steers.
        /// </summary>
        static void BuildSteering(GreekShipForm f, GreekShipParts parts)
        {
            // On the quarters, steep enough that the blades stay within the ship's length; the shafts end as deep as they
            // always reached, unless a blade would then reach below the keel.
            const float s = 0.15f;
            var (helmS, platform, seated, men) = Helm(f);
            var floor = platform ? Sheer(f, 0.115f) + f.Platforms * (f.Castles ? 1f : 0.85f) : seated ? DeckAt(f, helmS) + f.Bulwark * 0.45f + 0.06f : DeckAt(f, helmS);
            var hands = floor + (seated ? SeatedHelmHands : HelmHands);
            var helmZ = Z(f, helmS);
            var gunwale = Sheer(f, s);
            var bladeTip = gunwale - 0.1f - 0.788f * f.Length * 0.17f;

            var combined = new MeshBuilder(null);
            var mounts = new MeshBuilder(null);
            parts.SteeringOars = new GreekMesh[2];
            parts.SteeringPivots = new Vector3[2];
            parts.SteeringAxes = new Vector3[2];
            parts.TillerGrips = new Vector3[2];
            parts.HelmStands = men == 2
                ? new[] { new Vector3(-(HalfWidth(f, helmS) - 1.0f), floor, helmZ), new Vector3(HalfWidth(f, helmS) - 1.0f, floor, helmZ) }
                : new[] { new Vector3(0f, floor, helmZ) };
            parts.HelmSeated = seated;
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var down = Quaternion.Euler(-52f, 0f, side * 8f) * Vector3.back;
                var pivot = new Vector3(side * (HalfWidth(f, s) + 0.15f), hands - SteeringLoom * -down.y, Z(f, s));
                var length = (pivot.y - bladeTip) / -down.y;
                var top = pivot - down * SteeringLoom;
                // The tiller's end at the helmsman's hip, a little forward of him: beside him if he stands alone between
                // both tillers, in front of him if each oar has its own man.
                var grip = men == 2
                    ? new Vector3(parts.HelmStands[i].x + side * 0.15f, hands, helmZ + 0.3f)
                    : new Vector3(side * 0.3f, hands, helmZ + 0.15f);
                parts.SteeringPivots[i] = pivot;
                parts.SteeringAxes[i] = down;
                parts.TillerGrips[i] = grip;

                // The blade lies in the fore-and-aft plane of the shaft, so it steers when the shaft turns. Turned so, its broad
                // edge reaches below the shaft's end; where it would pass under the keel the shaft ends higher, along its own
                // line, as nothing of the ship reaches below its keel (the asset pipeline takes a model's lowest point for it).
                var blade = Quaternion.LookRotation(down, Vector3.up);
                var bladeHalf = new Vector3(0.05f, 0.42f, Mathf.Max(0.35f, length * 0.12f));
                var end = pivot + down * length;
                var bladeCentre = end - down * length * 0.12f;
                var under = -f.Draft - (bladeCentre.y - DepthBelowCentre(blade, bladeHalf));
                if (under > 0f)
                {
                    end -= down * (under / -down.y);
                    bladeCentre -= down * (under / -down.y);
                }

                var single = new MeshBuilder(null);
                foreach (var (b, offset) in new[] { (combined, Vector3.zero), (single, -pivot) })
                {
                    b.Tube(GreekMaterial.Wood, new List<Vector3> { top + offset, pivot + offset, end + offset }, 0.09f, 0.08f, 5);
                    b.Box(GreekMaterial.Wood, bladeCentre + offset, bladeHalf, blade);
                    b.Tube(GreekMaterial.Wood, new List<Vector3> { top + offset, grip + offset }, 0.045f, 0.035f, 5);
                }
                parts.SteeringOars[i] = single.Build(GreekMaterial.Wood);

                // Where the pivot stands above the gunwale, a post on the quarter carries it, braced in to the hull.
                if (pivot.y > gunwale + 0.1f)
                {
                    foreach (var b in new[] { combined, mounts })
                    {
                        var foot = new Vector3(pivot.x, gunwale - 0.05f, pivot.z);
                        b.Box(GreekMaterial.Wood, (foot + pivot) * 0.5f + new Vector3(0f, 0f, -0.12f), new Vector3(0.08f, (pivot.y - foot.y) * 0.5f + 0.06f, 0.08f), Quaternion.identity);
                        var inner = new Vector3(side * HalfWidth(f, s) * 0.8f, pivot.y - 0.08f, pivot.z - 0.12f);
                        var bar = new Vector3(pivot.x, pivot.y - 0.08f, pivot.z - 0.12f) - inner;
                        b.Box(GreekMaterial.Wood, inner + bar * 0.5f, new Vector3(0.06f, 0.06f, bar.magnitude * 0.5f), Quaternion.LookRotation(bar.normalized));
                    }
                }
            }
            parts.Steering = combined.Build(GreekMaterial.Wood);
            parts.SteeringMounts = mounts.Build(GreekMaterial.Wood);
        }

        /// <summary>How far a box turned by <paramref name="turn"/>, with these half extents, reaches below its centre.</summary>
        static float DepthBelowCentre(Quaternion turn, Vector3 half) =>
            Mathf.Abs((turn * Vector3.right).y) * half.x + Mathf.Abs((turn * Vector3.up).y) * half.y + Mathf.Abs((turn * Vector3.forward).y) * half.z;

        /// <summary>
        /// A point of the unit sail (1 x 1, hanging down from Y = 0, bellied toward +Z) at u across (0..1) and v down (0..1).
        /// The sail's transform scales it to the class's size and billow, so anything placed by this function stays on it.
        /// </summary>
        public static Vector3 SailPoint(float u, float v) =>
            new Vector3(u - 0.5f, -v - 0.08f * Mathf.Sin(Mathf.PI * u) * (1f - v), Mathf.Sin(Mathf.PI * u) * (0.3f + 0.7f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 1.1f))));

        /// <summary>A square sail 1 x 1 hanging from Y = 0, bellied toward +Z, with the pale lines of its brails. UV: metres across, 0..1 down.</summary>
        static void BuildSail(MeshBuilder b, GreekShipForm f, int detail)
        {
            var columns = detail == 0 ? 8 : detail == 1 ? 4 : 1;
            var rows = detail == 0 ? 5 : detail == 1 ? 3 : 1;
            Vector3 At(float u, float v) => SailPoint(u, v);
            foreach (var (face, shift) in new[] { (1f, 0f), (-1f, -0.015f) })
            {
                var grid = new int[rows + 1, columns + 1];
                for (var r = 0; r <= rows; r++)
                for (var c = 0; c <= columns; c++)
                {
                    var u = c / (float)columns;
                    var v = r / (float)rows;
                    // The cloths of the sail run up and down: across in metres, so their width is the same on every class.
                    grid[r, c] = b.Vertex(At(u, v) + new Vector3(0f, 0f, shift), new Vector2(u * f.SailWidth, v));
                }
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < columns; c++)
                    b.Quad(GreekMaterial.Sail, grid[r, c], grid[r, c + 1], grid[r + 1, c + 1], grid[r + 1, c], new Vector3(0f, 0f, face));
            }
            if (detail < 2)
            {
                // Brails: vertical lines of pale rope on the forward face, by which the sail was gathered up.
                var lines = detail == 0 ? 7 : 3;
                for (var i = 1; i <= lines; i++)
                {
                    var u = i / (lines + 1f);
                    var path = new List<Vector3>();
                    for (var r = 0; r <= rows; r++) path.Add(At(u, r / (float)rows) + new Vector3(0f, 0f, 0.012f));
                    b.Strip(GreekMaterial.Paint, path, 0.018f, Vector3.forward);
                }
            }
        }

        static void BuildOars(GreekShipForm f, GreekShipParts parts, int detail)
        {
            var reach = (float)Rules.OarReach(ShipCatalog.Get(f.Class));
            var pins = new List<Vector3>();
            var banks = new List<int>();
            var tilts = new List<float>();
            var oars = detail == 0 ? f.OarsPerBank : Mathf.Max(1, f.OarsPerBank / 2);
            for (var i = 0; i < oars; i++)
            {
                var s = OarStation(f, i, oars);
                pins.Add(new Vector3(HalfWidth(f, s) * 1.01f, TholeHeight(f, s), Z(f, s)));
                banks.Add(0);
            }
            var pinX = f.HullBeam * 0.5f;
            var pinY = TholeHeight(f, 0.5f);
            var outward = Mathf.Max(1.5f, f.Beam * 0.5f + reach * 0.92f - pinX);
            var tilt = Mathf.Atan2(pinY + 0.15f, outward) * Mathf.Rad2Deg;
            for (var i = 0; i < oars; i++) tilts.Add(tilt);
            var length = Mathf.Sqrt(outward * outward + (pinY + 0.15f) * (pinY + 0.15f));
            // Inboard of the thole the loom rises as steeply as the oar falls outboard, up to the rower's hands.
            var standing = f.Class == ShipClass.Merchant;
            var hands = RowerSeatHeight(f, 0.5f) + (standing ? StandingHands : SeatedHands);
            var inboard = Mathf.Clamp((hands - pinY) / Mathf.Sin(tilt * Mathf.Deg2Rad), 0.6f, 1.6f);
            parts.OarPins = pins.ToArray();
            parts.OarBank = banks.ToArray();
            parts.OarTilt = tilts.ToArray();
            parts.OarLength = new[] { length };
            parts.OarInboard = new[] { inboard };
            parts.RowersStand = standing;
            // Each rower faces aft just forward of his thole, the handle at mid-stroke in front of him at the loom's end.
            var across = inboard * Mathf.Cos(tilt * Mathf.Deg2Rad);
            parts.RowerSeats = new Vector3[pins.Count];
            for (var i = 0; i < pins.Count; i++)
            {
                var seatZ = pins[i].z + SeatForward;
                var seatS = Mathf.InverseLerp(Z(f, 0f), Z(f, 1f), seatZ);
                parts.RowerSeats[i] = new Vector3(pins[i].x - across - (standing ? 0.1f : 0.05f), RowerSeatHeight(f, seatS), seatZ);
            }
            var oar = new MeshBuilder(null);
            // One shaft from the end of the handle to the blade. The outboard part stays centred on the mesh's origin, as the
            // view places it, so the loom and handle reach toward -X past the thole pin.
            oar.Box(GreekMaterial.Wood, new Vector3(-inboard * 0.5f, 0f, 0f), new Vector3((length + inboard) * 0.5f, 0.045f, 0.045f), Quaternion.identity);
            oar.Box(GreekMaterial.Wood, new Vector3(length * 0.5f - 0.35f, 0f, 0f), new Vector3(0.35f, 0.16f, 0.025f), Quaternion.identity);
            parts.Oars = new[] { oar.Build(GreekMaterial.Wood) };
        }

        static Vector3[] MountPoints(GreekShipForm f)
        {
            var slots = ShipCatalog.Get(f.Class).Slots.Length;
            var mounts = new Vector3[slots];
            for (var i = 0; i < slots; i++)
            {
                float s;
                float y;
                if (f.Castles && slots == CastleStations.Length)
                {
                    // Heavy throwers stand on the castles, the forward one first.
                    s = CastleStations[slots - 1 - i];
                    y = Sheer(f, s) + f.Platforms + 0.02f;
                }
                else if (f.Class == ShipClass.ShipKiller && slots == 2)
                {
                    // The great bows stand on the bow and stern platforms.
                    s = i == 0 ? 0.885f : 0.115f;
                    y = Sheer(f, s) + f.Platforms * (i == 0 ? 1f : 0.85f) + 0.02f;
                }
                else
                {
                    var along = slots == 1 ? 0f : Mathf.Lerp(-0.3f, 0.3f, i / (slots - 1f));
                    s = (along * f.Length + f.Length * 0.5f) / Mathf.Max(0.1f, BowEnd(f) + f.Length * 0.5f);
                    // On the floor it stands over: a raised platform or the walkway rather than inside them, else the deck.
                    var floor = FloorAt(f, s, 0f);
                    y = floor > DeckAt(f, s) + 0.01f ? floor + 0.02f : DeckAt(f, s) + 0.05f;
                }
                mounts[i] = new Vector3(0f, y, Z(f, s));
            }
            return mounts;
        }

        /// <summary>Ragged dark faces where a hull broke, so the pieces of a broken ship are never hollow.</summary>
        static void AddBreakCaps(MeshBuilder b, GreekShipForm f, int section, float[] cuts)
        {
            for (var c = 0; c < cuts.Length; c++)
            {
                var facing = c == section - 1 ? -1f : c == section ? 1f : 0f;
                if (facing == 0f) continue;
                var s = Mathf.InverseLerp(-f.Length * 0.5f, BowEnd(f), cuts[c]);
                var rng = new System.Random(31 * (int)f.Class + c);
                var ring = new List<Vector3>();
                for (var k = 0; k <= 6; k++) ring.Add(Section(f, s, k / 6f));
                for (var k = 5; k >= 0; k--) { var p = Section(f, s, k / 6f); p.x = -p.x; ring.Add(p); }
                var centre = new Vector3(0f, (Sheer(f, s) - Depth(f, s)) * 0.5f, cuts[c]);
                var mid = b.Vertex(centre, new Vector2(0.3f, 0.8f), section);
                var ids = new List<int>();
                foreach (var p in ring) ids.Add(b.Vertex(p + new Vector3(0f, 0f, (float)(rng.NextDouble() - 0.5) * 0.8f), new Vector2(0.31f, 0.79f), section));
                for (var j = 0; j < ids.Count; j++) b.Triangle(GreekMaterial.Pitch, mid, ids[j], ids[(j + 1) % ids.Count], new Vector3(0f, 0f, facing), section);
            }
        }

        // Props ----------------------------------------------------------------------------------------------------
        /// <summary>
        /// A weapon post by its size: an archers' stand behind a wicker screen; a great bow on a stand; a frame that
        /// hurls stones. The game's machines are later than the age, so they are drawn as plainly as wood and rawhide allow.
        /// </summary>
        public static GreekMesh Weapon(SlotSize size)
        {
            if (weapons.TryGetValue(size, out var mesh) && mesh.Mesh != null) return mesh;
            var b = new MeshBuilder(null);
            switch (size)
            {
                case SlotSize.Light:
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 0.1f, 0f), new Vector3(0.8f, 0.1f, 0.65f), Quaternion.identity);
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 0.5f, 0.62f), new Vector3(0.8f, 0.32f, 0.04f), Quaternion.identity);
                    b.Box(GreekMaterial.Ochre, new Vector3(0f, 0.84f, 0.62f), new Vector3(0.82f, 0.04f, 0.06f), Quaternion.identity);
                    break;
                case SlotSize.Medium:
                {
                    // A great bow on a stand: a stock along the line of shot and a recurved bow across it.
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 0.5f, 0.14f), Quaternion.identity);
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 1.05f, 0.2f), new Vector3(0.12f, 0.1f, 1.0f), Quaternion.Euler(-6f, 0f, 0f));
                    var limbs = new List<Vector3>();
                    for (var i = -3; i <= 3; i++)
                    {
                        var x = i * 0.32f;
                        limbs.Add(new Vector3(x, 1.12f, 1.05f - 0.06f * i * i + (Mathf.Abs(i) == 3 ? 0.18f : 0f)));
                    }
                    b.Tube(GreekMaterial.Ochre, limbs, 0.07f, 0.07f, 4);
                    b.Strip(GreekMaterial.Paint, new List<Vector3> { limbs[0], new Vector3(0f, 1.12f, 0.45f), limbs[limbs.Count - 1] }, 0.03f, Vector3.up);
                    break;
                }
                default:
                {
                    // A throwing frame: two posts with a cross beam, a long arm swung up and a rawhide sling cup.
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 0.15f, 0f), new Vector3(1.0f, 0.15f, 1.2f), Quaternion.identity);
                    foreach (var side in new[] { -1f, 1f })
                        b.Box(GreekMaterial.Wood, new Vector3(side * 0.75f, 0.95f, -0.2f), new Vector3(0.12f, 0.8f, 0.16f), Quaternion.Euler(0f, 0f, side * 6f));
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 1.7f, -0.2f), new Vector3(0.9f, 0.1f, 0.14f), Quaternion.identity);
                    b.Box(GreekMaterial.Wood, new Vector3(0f, 1.1f, 0.5f), new Vector3(0.12f, 0.12f, 1.25f), Quaternion.Euler(-48f, 0f, 0f));
                    b.Box(GreekMaterial.Ochre, new Vector3(0f, 2.0f, 1.32f), new Vector3(0.25f, 0.08f, 0.25f), Quaternion.Euler(-48f, 0f, 0f));
                    b.Rock(0.22f, 1, new Vector3(0.5f, 0.42f, 0.6f));
                    b.Rock(0.2f, 1, new Vector3(-0.45f, 0.4f, 0.7f));
                    break;
                }
            }
            mesh = b.Build(GreekMaterial.Wood, GreekMaterial.Ochre, GreekMaterial.Paint, GreekMaterial.Pitch);
            weapons[size] = mesh;
            return mesh;
        }

        /// <summary>
        /// Small props built once: for effects and wrecks an arrow, javelin, bolt, stone, plank, jar, ingot, silver, mast piece
        /// and breach; and the gear a crew keeps on deck (names "gear.*"): a water jar in a rope ring, sacks of provisions,
        /// a wicker basket, a coil of rope, folded cloth and pierced stone anchors. Gear stands on its base at Y = 0, facing +Z.
        /// </summary>
        public static GreekMesh Prop(string name)
        {
            if (props.TryGetValue(name, out var mesh) && mesh.Mesh != null) return mesh;
            var b = new MeshBuilder(null);
            switch (name)
            {
                case "gear.water-jar":
                {
                    // A jar for water lashed upright in a ring of rope, with a coil under its pointed toe.
                    var jar = new[] { (0.02f, 0.01f), (0.06f, 0.07f), (0.19f, 0.13f), (0.33f, 0.15f), (0.43f, 0.13f), (0.48f, 0.05f), (0.54f, 0.045f), (0.57f, 0.055f) };
                    b.Lathe(GreekMaterial.Terracotta, jar, 10);
                    b.Tube(GreekMaterial.Rope, Ring(0.33f, 0.155f, 10), 0.018f, 0.018f, 4);
                    b.Tube(GreekMaterial.Rope, Ring(0.03f, 0.1f, 10), 0.03f, 0.03f, 4);
                    mesh = b.Build(GreekMaterial.Terracotta, GreekMaterial.Rope);
                    break;
                }
                case "gear.sacks":
                {
                    // Three sacks of grain or bread, tied at the neck, leaning on each other.
                    var at = new[] { new Vector3(-0.2f, 0.15f, 0.02f), new Vector3(0.14f, 0.16f, -0.06f), new Vector3(0.0f, 0.13f, 0.24f) };
                    for (var i = 0; i < at.Length; i++)
                    {
                        var turn = Quaternion.Euler(0f, i * 52f - 30f, i == 1 ? 9f : -6f);
                        b.Box(GreekMaterial.Linen, at[i], new Vector3(0.15f, 0.14f, 0.22f), turn);
                        b.Box(GreekMaterial.Linen, at[i] + turn * new Vector3(0f, 0.02f, 0.26f), new Vector3(0.07f, 0.07f, 0.05f), turn);
                        b.Box(GreekMaterial.Rope, at[i] + turn * new Vector3(0f, 0.02f, 0.225f), new Vector3(0.08f, 0.08f, 0.015f), turn);
                    }
                    mesh = b.Build(GreekMaterial.Linen, GreekMaterial.Rope);
                    break;
                }
                case "gear.basket":
                {
                    // A wicker basket, its load under a cloth.
                    b.Lathe(GreekMaterial.Wicker, new[] { (0f, 0.17f), (0.03f, 0.21f), (0.3f, 0.26f), (0.33f, 0.27f) }, 10);
                    b.Tube(GreekMaterial.Wicker, Ring(0.33f, 0.27f, 10), 0.025f, 0.025f, 4);
                    b.Ellipse(GreekMaterial.Linen, new Vector3(0f, 0.31f, 0f), Vector3.right, Vector3.forward, Vector3.up, 0.25f, 0.25f, 10);
                    b.Ellipse(GreekMaterial.Wicker, new Vector3(0f, 0.005f, 0f), Vector3.right, Vector3.forward, Vector3.down, 0.17f, 0.17f, 10);
                    mesh = b.Build(GreekMaterial.Wicker, GreekMaterial.Linen);
                    break;
                }
                case "gear.rope-coil":
                {
                    // A coil of rope flaked down flat on the deck, two layers.
                    var path = new List<Vector3>();
                    for (var i = 0; i <= 64; i++)
                    {
                        var turn = i / 64f * 3f * Mathf.PI * 2f;
                        var r = Mathf.Lerp(0.12f, 0.3f, i / 64f);
                        path.Add(new Vector3(Mathf.Cos(turn) * r, i < 40 ? 0.035f : 0.09f, Mathf.Sin(turn) * r));
                    }
                    b.Tube(GreekMaterial.Rope, path, 0.035f, 0.035f, 5);
                    mesh = b.Build(GreekMaterial.Rope);
                    break;
                }
                case "gear.cloth":
                {
                    // Folded cloth (an awning or a spare cloth for the sail), tied with a cord.
                    b.Box(GreekMaterial.Linen, new Vector3(0f, 0.06f, 0f), new Vector3(0.42f, 0.06f, 0.28f), Quaternion.identity);
                    b.Box(GreekMaterial.Linen, new Vector3(0.03f, 0.165f, -0.02f), new Vector3(0.36f, 0.045f, 0.25f), Quaternion.Euler(0f, 6f, 0f));
                    b.Box(GreekMaterial.Rope, new Vector3(0.02f, 0.11f, 0f), new Vector3(0.02f, 0.125f, 0.3f), Quaternion.identity);
                    mesh = b.Build(GreekMaterial.Linen, GreekMaterial.Rope);
                    break;
                }
                case "gear.anchor-stones":
                {
                    // Two pierced stone anchors, the Bronze Age kind: tapering slabs with a hole for the rope at the narrow end.
                    foreach (var (place, turn) in new[] { (new Vector3(-0.16f, 0.07f, 0f), 8f), (new Vector3(0.2f, 0.07f, 0.06f), -14f) })
                    {
                        var rotation = Quaternion.Euler(0f, turn, 0f);
                        b.Box(GreekMaterial.Stone, place, new Vector3(0.15f, 0.07f, 0.26f), rotation);
                        b.Box(GreekMaterial.Stone, place + rotation * new Vector3(0f, 0f, 0.3f), new Vector3(0.1f, 0.06f, 0.07f), rotation);
                        b.Ellipse(GreekMaterial.Pitch, place + rotation * new Vector3(0f, 0.072f, 0.29f), rotation * Vector3.right, rotation * Vector3.forward, Vector3.up, 0.035f, 0.035f, 8);
                    }
                    b.Tube(GreekMaterial.Rope, new List<Vector3> { new Vector3(-0.16f, 0.14f, 0.3f), new Vector3(0.0f, 0.2f, 0.42f), new Vector3(0.26f, 0.14f, 0.36f) }, 0.022f, 0.022f, 4);
                    mesh = b.Build(GreekMaterial.Stone, GreekMaterial.Rope, GreekMaterial.Pitch);
                    break;
                }
                case var unknown when unknown.StartsWith("gear."):
                    throw new System.ArgumentException($"No such gear: {unknown}");
                case "arrow":
                case "javelin":
                case "bolt":
                {
                    var length = name == "arrow" ? 1f : name == "javelin" ? 1.6f : 1.3f;
                    var thick = name == "bolt" ? 0.05f : 0.025f;
                    b.Box(GreekMaterial.Wood, Vector3.zero, new Vector3(thick, thick, length * 0.5f), Quaternion.identity);
                    b.Box(GreekMaterial.Bronze, new Vector3(0f, 0f, length * 0.5f + 0.06f), new Vector3(thick * 1.8f, thick * 1.8f, 0.08f), Quaternion.Euler(0f, 0f, 45f));
                    if (name != "javelin")
                    {
                        b.Box(GreekMaterial.Paint, new Vector3(0f, 0f, -length * 0.42f), new Vector3(thick * 4f, 0.004f, length * 0.07f), Quaternion.identity);
                        b.Box(GreekMaterial.Paint, new Vector3(0f, 0f, -length * 0.42f), new Vector3(0.004f, thick * 4f, length * 0.07f), Quaternion.identity);
                    }
                    mesh = b.Build(GreekMaterial.Wood, GreekMaterial.Bronze, GreekMaterial.Paint);
                    break;
                }
                case "stone":
                    b.Rock(0.5f, 2, Vector3.zero);
                    mesh = b.Build(GreekMaterial.Pitch);
                    break;
                case "plank":
                    b.Box(GreekMaterial.Wood, Vector3.zero, new Vector3(0.18f, 0.04f, 1.2f), Quaternion.identity);
                    b.Box(GreekMaterial.Pitch, new Vector3(0f, -0.05f, 0f), new Vector3(0.18f, 0.012f, 1.2f), Quaternion.identity);
                    mesh = b.Build(GreekMaterial.Wood, GreekMaterial.Pitch);
                    break;
                case "mast":
                    b.Tube(GreekMaterial.Wood, new List<Vector3> { new Vector3(0f, 0f, -2.5f), new Vector3(0f, 0f, 2.5f) }, 0.2f, 0.16f, 6);
                    mesh = b.Build(GreekMaterial.Wood);
                    break;
                case "splinter":
                    // One submesh only: particle systems draw single-material meshes.
                    b.Box(GreekMaterial.Wood, Vector3.zero, new Vector3(0.12f, 0.03f, 0.9f), Quaternion.Euler(0f, 0f, 8f));
                    mesh = b.Build(GreekMaterial.Wood);
                    break;
                case "jar":
                case "amphora":
                    mesh = Jar(0);
                    break;
                case "ingot":
                case "silver":
                {
                    // An oxhide ingot: a slab with its four corners drawn out like the legs of a stretched hide.
                    var paint = name == "ingot" ? GreekMaterial.Bronze : GreekMaterial.Paint;
                    b.Box(paint, Vector3.zero, new Vector3(0.3f, 0.03f, 0.2f), Quaternion.identity);
                    foreach (var sx in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                        b.Box(paint, new Vector3(sx * 0.3f, 0f, sz * 0.2f), new Vector3(0.09f, 0.03f, 0.07f), Quaternion.Euler(0f, sx * sz * 35f, 0f));
                    mesh = b.Build(paint);
                    break;
                }
                default: // "breach0".."breach3": a ragged black hole with splintered planks around it
                {
                    var seed = name.Length > 6 ? name[6] - '0' : 0;
                    var rng = new System.Random(101 + seed);
                    var centre = b.Vertex(new Vector3(0f, 0f, -0.04f), new Vector2(0.3f, 0.8f));
                    var rim = new List<int>();
                    const int points = 11;
                    for (var i = 0; i < points; i++)
                    {
                        var angle = i * Mathf.PI * 2f / points;
                        var r = 0.55f + (float)rng.NextDouble() * 0.5f;
                        rim.Add(b.Vertex(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.75f, 0f), new Vector2(0.31f, 0.81f)));
                    }
                    for (var i = 0; i < points; i++) b.Triangle(GreekMaterial.Pitch, centre, rim[i], rim[(i + 1) % points], Vector3.back);
                    for (var i = 0; i < 6; i++)
                    {
                        var angle = (float)rng.NextDouble() * 360f;
                        var direction = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
                        var at = direction * (0.7f + (float)rng.NextDouble() * 0.25f);
                        b.Box(GreekMaterial.Wood, at + Vector3.back * 0.06f, new Vector3(0.28f + (float)rng.NextDouble() * 0.2f, 0.05f, 0.03f),
                              Quaternion.Euler(-(10f + (float)rng.NextDouble() * 30f), 0f, angle));
                    }
                    mesh = b.Build(GreekMaterial.Pitch, GreekMaterial.Wood);
                    break;
                }
            }
            props[name] = mesh;
            return mesh;
        }

        // Marks of one ship's history -------------------------------------------------------------------------------------
        /// <summary>
        /// A point on the outside of the hull at s along it and height y above the empty waterline, on a side (-1 port,
        /// +1 starboard), with its outward normal. False above the gunwale or below the keel.
        /// </summary>
        public static bool SurfacePoint(GreekShipForm f, float s, float y, float side, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            s = Mathf.Clamp01(s);
            if (y > Sheer(f, s) || y < -Depth(f, s)) return false;
            // The section's height falls steadily from the gunwale (t = 0) to the keel (t = 1): bisect for the height.
            float low = 0f, high = 1f;
            for (var i = 0; i < 24; i++)
            {
                var mid = (low + high) * 0.5f;
                if (Section(f, s, mid).y > y) low = mid; else high = mid;
            }
            var t = (low + high) * 0.5f;
            var p = Section(f, s, t);
            var down = Section(f, s, Mathf.Min(1f, t + 0.01f)) - Section(f, s, Mathf.Max(0f, t - 0.01f));
            var ahead = Section(f, Mathf.Min(1f, s + 0.01f), t) - Section(f, Mathf.Max(0f, s - 0.01f), t);
            p.x *= -side;
            down.x *= -side;
            ahead.x *= -side;
            normal = Vector3.Cross(ahead, down).normalized;
            if (Vector3.Dot(normal, new Vector3(side, 0f, 0f)) < 0f) normal = -normal;
            point = p;
            return true;
        }

        /// <summary>
        /// A patch on the hull: centre at s along the hull and height Y, Length along the planks and Height up them, in
        /// metres, tilted by Angle degrees, on Side (-1 port, +1 starboard). A Ragged patch has an irregular edge (worn
        /// pitch); a plain one is a clean rectangle (planks put in).
        /// </summary>
        public struct HullPatch
        {
            public float S, Y, Length, Height, Angle;
            public int Side, Seed;
            public bool Ragged;
            public GreekMaterial Paint;
        }

        /// <summary>
        /// The patches of one ship as one small mesh lying 2 cm off its hull and following its curve, one submesh per paint.
        /// UV 0..1 over each patch, so a worn-pitch texture darkens toward the patch's edge. A patch that would leave the hull
        /// is left out. Null when there is nothing to draw.
        /// </summary>
        public static GreekMesh HullPatches(GreekShipForm f, IReadOnlyList<HullPatch> patches)
        {
            if (patches == null || patches.Count == 0) return null;
            var b = new MeshBuilder(null);
            var sPerMetre = 1f / Mathf.Max(1f, BowEnd(f) + f.Length * 0.5f);
            const int along = 6, up = 3;
            var points = new Vector3[along + 1, up + 1];
            var drawn = 0;
            foreach (var patch in patches)
            {
                var rng = new System.Random(patch.Seed);
                var slope = Mathf.Tan(patch.Angle * Mathf.Deg2Rad);
                var inside = true;
                for (var i = 0; i <= along && inside; i++)
                for (var j = 0; j <= up && inside; j++)
                {
                    var a = (i / (float)along - 0.5f) * patch.Length;
                    var h = (j / (float)up - 0.5f) * patch.Height;
                    if (patch.Ragged && (i == 0 || i == along || j == 0 || j == up))
                    {
                        // Worn pitch has no straight edge: pull the rim in by a random part of the way.
                        var pull = 1f - 0.3f * (float)rng.NextDouble();
                        a *= pull;
                        h *= pull;
                    }
                    inside = SurfacePoint(f, patch.S + a * sPerMetre, patch.Y + h + a * slope, patch.Side, out var point, out var normal);
                    points[i, j] = point + normal * 0.02f;
                }
                if (!inside) continue;
                var ids = new int[along + 1, up + 1];
                for (var i = 0; i <= along; i++)
                for (var j = 0; j <= up; j++)
                    ids[i, j] = b.Vertex(points[i, j], new Vector2(i / (float)along, j / (float)up));
                var outward = new Vector3(patch.Side, 0f, 0f);
                for (var i = 0; i < along; i++)
                for (var j = 0; j < up; j++)
                    b.Quad(patch.Paint, ids[i, j], ids[i + 1, j], ids[i + 1, j + 1], ids[i, j + 1], outward);
                drawn++;
            }
            if (drawn == 0) return null;
            var mesh = b.Build(GreekMaterial.Pitch, GreekMaterial.Wood);
            mesh.Mesh.name = "Ship patches";
            return mesh;
        }

        /// <summary>A patch sewn on the sail: centre (U across, V down, 0..1) and size as parts of the sail's width and height.</summary>
        public struct SailPatch
        {
            public float U, V, Width, Height;
        }

        /// <summary>
        /// The patches of one ship's sail as one small mesh in the unit sail's space, on both faces, following its belly, so
        /// they move, billow and come down with the sail. UV like the sail's: metres across, 0..1 down. Null when none.
        /// </summary>
        public static GreekMesh SailPatches(GreekShipForm f, IReadOnlyList<SailPatch> patches)
        {
            if (patches == null || patches.Count == 0) return null;
            var b = new MeshBuilder(null);
            const int cells = 3;
            foreach (var patch in patches)
            foreach (var (face, lift) in new[] { (1f, 0.03f), (-1f, -0.045f) })
            {
                var ids = new int[cells + 1, cells + 1];
                for (var i = 0; i <= cells; i++)
                for (var j = 0; j <= cells; j++)
                {
                    var u = Mathf.Clamp01(patch.U + (i / (float)cells - 0.5f) * patch.Width);
                    var v = Mathf.Clamp01(patch.V + (j / (float)cells - 0.5f) * patch.Height);
                    ids[i, j] = b.Vertex(SailPoint(u, v) + new Vector3(0f, 0f, lift), new Vector2(u * f.SailWidth, v));
                }
                for (var i = 0; i < cells; i++)
                for (var j = 0; j < cells; j++)
                    b.Quad(GreekMaterial.Sail, ids[i, j], ids[i + 1, j], ids[i + 1, j + 1], ids[i, j + 1], new Vector3(0f, 0f, face));
            }
            var mesh = b.Build(GreekMaterial.Sail);
            mesh.Mesh.name = "Sail patches";
            return mesh;
        }

        /// <summary>A closed circle of points at a height, for rings of rope or a basket's rim.</summary>
        static List<Vector3> Ring(float y, float radius, int points)
        {
            var path = new List<Vector3>();
            for (var i = 0; i <= points; i++)
            {
                var angle = i * Mathf.PI * 2f / points;
                path.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
            }
            return path;
        }

        /// <summary>A storage jar of the eastern trade (a "Canaanite jar"): pointed toe, high shoulders, short neck, two handles. Terracotta.</summary>
        static GreekMesh Jar(int detail)
        {
            var key = "jar" + detail;
            if (props.TryGetValue(key, out var made) && made.Mesh != null) return made;
            var b = new MeshBuilder(null);
            var profile = new[] { (0f, 0.02f), (0.1f, 0.12f), (0.35f, 0.25f), (0.62f, 0.28f), (0.8f, 0.24f), (0.9f, 0.1f), (1.02f, 0.08f), (1.07f, 0.1f) };
            b.Lathe(GreekMaterial.Terracotta, profile, detail == 0 ? 10 : 6);
            foreach (var side in new[] { -1f, 1f })
                b.Box(GreekMaterial.Terracotta, new Vector3(side * 0.24f, 0.74f, 0f), new Vector3(0.025f, 0.09f, 0.03f), Quaternion.Euler(0f, 0f, side * -25f));
            made = b.Build(GreekMaterial.Terracotta);
            props[key] = made;
            return made;
        }

        // Materials and renderers ----------------------------------------------------------------------------------
        static readonly Dictionary<(Material, GreekMaterial), Material> fallbacks = new Dictionary<(Material, GreekMaterial), Material>();

        /// <summary>The scene's material for a paint; without one (older scene, tests) a copy of the wood material in the palette's colour.</summary>
        public static Material MaterialFor(GreekMaterial paint, PlaceholderMaterials materials, Material sail = null)
        {
            if (paint == GreekMaterial.Sail) return sail;
            if (materials == null) return null;
            var given = paint switch
            {
                GreekMaterial.Pitch => materials.Pitch,
                GreekMaterial.Ochre => materials.Ochre,
                GreekMaterial.Paint => materials.Paint,
                GreekMaterial.Bronze => materials.Bronze,
                GreekMaterial.Wood => materials.Wood,
                _ => null,
            };
            if (given != null) return given;
            var source = materials.Wood;
            if (source == null) return null;
            if (fallbacks.TryGetValue((source, paint), out var copy) && copy != null) return copy;
            copy = new Material(source) { name = $"{source.name} ({paint})" };
            var colour = Palette.Of(paint);
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", colour);
            copy.color = colour;
            fallbacks[(source, paint)] = copy;
            return copy;
        }

        public static Material[] MaterialsFor(GreekMesh mesh, PlaceholderMaterials materials, Material sail = null)
        {
            var list = new Material[mesh.Slots.Length];
            for (var i = 0; i < list.Length; i++) list[i] = MaterialFor(mesh.Slots[i], materials, sail);
            return list;
        }

        /// <summary>A child object drawing the mesh with the matching materials; no collider.</summary>
        public static Transform Attach(Transform parent, string name, GreekMesh mesh, PlaceholderMaterials materials, Material sail = null) =>
            Attach(parent, name, mesh, MaterialsFor(mesh, materials, sail));

        /// <summary>A child object drawing the mesh with the given materials, one per submesh; no collider.</summary>
        public static Transform Attach(Transform parent, string name, GreekMesh mesh, Material[] list)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh.Mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            if (list != null && System.Array.TrueForAll(list, m => m != null)) renderer.sharedMaterials = list;
            return item.transform;
        }

        // Mesh building --------------------------------------------------------------------------------------------
        /// <summary>
        /// Vertices and triangles per paint. Each triangle is wound to face the given outward direction, so normals are
        /// right whatever order the corners come in. Triangles remember the hull section (by Z) they lie in.
        /// </summary>
        internal sealed class MeshBuilder
        {
            readonly float[] cuts;
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<(GreekMaterial paint, int a, int b, int c, int section)> triangles = new List<(GreekMaterial, int, int, int, int)>();
            /// <summary>Pairs of vertices at the same place where a round body's UV wraps; their normals are joined after the build.</summary>
            readonly List<(int a, int b)> seams = new List<(int, int)>();

            public MeshBuilder(float[] cuts) => this.cuts = cuts;

            public Vector3 Position(int index) => positions[index];

            public int Vertex(Vector3 position, Vector2 uv, int section = -1)
            {
                positions.Add(position);
                uvs.Add(uv);
                return positions.Count - 1;
            }

            int SectionOf(float z)
            {
                if (cuts == null) return 0;
                var section = 0;
                while (section < cuts.Length && z >= cuts[section]) section++;
                return section;
            }

            public void Triangle(GreekMaterial paint, int a, int b, int c, Vector3 outward, int section = -1)
            {
                var pa = positions[a];
                var normal = Vector3.Cross(positions[b] - pa, positions[c] - pa);
                if (normal.sqrMagnitude < 1e-12f) return;
                if (section < 0) section = SectionOf((pa.z + positions[b].z + positions[c].z) / 3f);
                triangles.Add(Vector3.Dot(normal, outward) >= 0f ? (paint, a, b, c, section) : (paint, a, c, b, section));
            }

            public void Quad(GreekMaterial paint, int a, int b, int c, int d, Vector3 outward)
            {
                Triangle(paint, a, b, c, outward);
                Triangle(paint, a, c, d, outward);
            }

            public void Box(GreekMaterial paint, Vector3 centre, Vector3 half, Quaternion rotation)
            {
                var corners = new Vector3[8];
                for (var i = 0; i < 8; i++)
                    corners[i] = centre + rotation * Vector3.Scale(half, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                int[][] faces = { new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 }, new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 } };
                foreach (var face in faces)
                {
                    var mid = (corners[face[0]] + corners[face[1]] + corners[face[2]] + corners[face[3]]) * 0.25f;
                    // Metres in the face's plane, u along its longer edge: the grain of a plank or a post runs along it.
                    var first = corners[face[1]] - corners[face[0]];
                    var second = corners[face[3]] - corners[face[0]];
                    var longer = first.sqrMagnitude >= second.sqrMagnitude;
                    var along = (longer ? first : second).normalized;
                    var across = (longer ? second : first).normalized;
                    var ids = new int[4];
                    for (var j = 0; j < 4; j++)
                    {
                        var p = corners[face[j]];
                        ids[j] = Vertex(p, new Vector2(Vector3.Dot(p, along), Vector3.Dot(p, across)));
                    }
                    Quad(paint, ids[0], ids[1], ids[2], ids[3], mid - centre);
                }
            }

            /// <summary>A tapering tube of n sides along a path.</summary>
            public void Tube(GreekMaterial paint, List<Vector3> path, float startRadius, float endRadius, int sides)
            {
                var rings = new List<int[]>();
                var along = 0f;
                for (var i = 0; i < path.Count; i++)
                {
                    if (i > 0) along += Vector3.Distance(path[i], path[i - 1]);
                    var tangent = (i + 1 < path.Count ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                    var side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                    var up = Vector3.Cross(side, tangent);
                    var r = Mathf.Lerp(startRadius, endRadius, path.Count == 1 ? 0f : i / (path.Count - 1f));
                    // One extra vertex closes the ring where the UV wraps; u runs along the tube in metres (the grain of a
                    // mast or an oar's loom, the lay of a rope), v around it.
                    var ring = new int[sides + 1];
                    for (var j = 0; j <= sides; j++)
                    {
                        var angle = j * Mathf.PI * 2f / sides;
                        ring[j] = Vertex(path[i] + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * r, new Vector2(along, angle * r));
                    }
                    seams.Add((ring[0], ring[sides]));
                    rings.Add(ring);
                }
                for (var i = 0; i + 1 < rings.Count; i++)
                for (var j = 0; j < sides; j++)
                {
                    var a = rings[i][j];
                    var b = rings[i][j + 1];
                    var c = rings[i + 1][j + 1];
                    var d = rings[i + 1][j];
                    var mid = (positions[a] + positions[c]) * 0.5f;
                    var axis = (path[i] + path[i + 1]) * 0.5f;
                    Quad(paint, a, b, c, d, mid - axis);
                }
            }

            /// <summary>A flat band of given width and thickness along a path (stern and stem posts), tapering to its end.</summary>
            public void Ribbon(GreekMaterial paint, List<Vector3> path, float width, float thickness, float endScale)
            {
                for (var i = 0; i + 1 < path.Count; i++)
                {
                    var a = path[i];
                    var b = path[i + 1];
                    var scale = Mathf.Lerp(1f, endScale, (i + 0.5f) / (path.Count - 1));
                    var direction = b - a;
                    var rotation = Quaternion.LookRotation(direction.normalized, Vector3.Cross(direction, Vector3.right).sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.right, direction).normalized : Vector3.up);
                    Box(paint, (a + b) * 0.5f, new Vector3(thickness * 0.5f, width * 0.5f * scale, direction.magnitude * 0.5f + 0.02f), rotation);
                }
            }

            /// <summary>A thin flat strip along a path, facing one way (rope lines on a sail, a bow string).</summary>
            public void Strip(GreekMaterial paint, List<Vector3> path, float width, Vector3 facing)
            {
                var along = 0f;
                for (var i = 0; i + 1 < path.Count; i++)
                {
                    var a = path[i];
                    var b = path[i + 1];
                    var length = Vector3.Distance(a, b);
                    var across = Vector3.Cross((b - a).normalized, facing).normalized * width * 0.5f;
                    var v0 = Vertex(a - across, new Vector2(along, 0f));
                    var v1 = Vertex(a + across, new Vector2(along, width));
                    var v2 = Vertex(b + across, new Vector2(along + length, width));
                    var v3 = Vertex(b - across, new Vector2(along + length, 0f));
                    along += length;
                    Quad(paint, v0, v1, v2, v3, facing);
                }
            }

            public void Ellipse(GreekMaterial paint, Vector3 centre, Vector3 along, Vector3 up, Vector3 normal, float halfLength, float halfHeight, int points)
            {
                var c = Vertex(centre, Vector2.zero);
                var rim = new int[points];
                for (var i = 0; i < points; i++)
                {
                    var angle = i * Mathf.PI * 2f / points;
                    var x = Mathf.Cos(angle) * halfLength;
                    var y = Mathf.Sin(angle) * halfHeight;
                    rim[i] = Vertex(centre + along * x + up * y, new Vector2(x, y));
                }
                for (var i = 0; i < points; i++) Triangle(paint, c, rim[i], rim[(i + 1) % points], normal);
            }

            /// <summary>A body of revolution around +Y from (height, radius) pairs. UV: metres around (at the widest girth) and up.</summary>
            public void Lathe(GreekMaterial paint, (float height, float radius)[] profile, int sides)
            {
                var widest = 0f;
                foreach (var (_, radius) in profile) widest = Mathf.Max(widest, radius);
                var rings = new int[profile.Length, sides + 1];
                for (var i = 0; i < profile.Length; i++)
                {
                    for (var j = 0; j <= sides; j++)
                    {
                        var angle = j * Mathf.PI * 2f / sides;
                        rings[i, j] = Vertex(new Vector3(Mathf.Cos(angle) * profile[i].radius, profile[i].height, Mathf.Sin(angle) * profile[i].radius), new Vector2(angle * widest, profile[i].height));
                    }
                    seams.Add((rings[i, 0], rings[i, sides]));
                }
                for (var i = 0; i + 1 < profile.Length; i++)
                for (var j = 0; j < sides; j++)
                {
                    var a = rings[i, j];
                    var b = rings[i, j + 1];
                    var c = rings[i + 1, j + 1];
                    var d = rings[i + 1, j];
                    var mid = (positions[a] + positions[c]) * 0.5f;
                    Quad(paint, a, b, c, d, new Vector3(mid.x, 0f, mid.z));
                }
            }

            /// <summary>A rough stone: a sphere whose points are pushed in and out by a fixed pattern.</summary>
            public void Rock(float radius, int rings, Vector3 centre)
            {
                var sides = rings * 4;
                var stacks = rings * 2 + 1;
                var grid = new int[stacks + 1, sides];
                for (var i = 0; i <= stacks; i++)
                for (var j = 0; j < sides; j++)
                {
                    var theta = Mathf.PI * i / stacks;
                    var phi = Mathf.PI * 2f * j / sides;
                    var bump = 1f + 0.12f * Mathf.Sin(3f * phi + i) * Mathf.Sin(2f * theta);
                    grid[i, j] = Vertex(centre + new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * radius * bump, new Vector2(j / (float)sides, i / (float)stacks));
                }
                for (var i = 0; i < stacks; i++)
                for (var j = 0; j < sides; j++)
                {
                    var a = grid[i, j];
                    var b = grid[i, (j + 1) % sides];
                    var c = grid[i + 1, (j + 1) % sides];
                    var d = grid[i + 1, j];
                    Quad(GreekMaterial.Pitch, a, b, c, d, (positions[a] + positions[c]) * 0.5f - centre);
                }
            }

            /// <summary>Copies another mesh's triangles, moved and turned.</summary>
            public void Append(GreekMesh mesh, Vector3 offset, Quaternion rotation)
            {
                var source = mesh.Mesh;
                var vertices = source.vertices;
                var sourceUvs = source.uv;
                var start = positions.Count;
                for (var i = 0; i < vertices.Length; i++) Vertex(offset + rotation * vertices[i], sourceUvs.Length > i ? sourceUvs[i] : Vector2.zero);
                for (var sub = 0; sub < source.subMeshCount; sub++)
                {
                    var list = source.GetTriangles(sub);
                    for (var i = 0; i < list.Length; i += 3)
                    {
                        var z = (positions[start + list[i]].z + positions[start + list[i + 1]].z + positions[start + list[i + 2]].z) / 3f;
                        triangles.Add((mesh.Slots[sub], start + list[i], start + list[i + 1], start + list[i + 2], SectionOf(z)));
                    }
                }
            }

            /// <summary>Copies one section of another builder (keeps its winding).</summary>
            public void Append(MeshBuilder other, int section)
            {
                var map = new Dictionary<int, int>();
                foreach (var (paint, a, b, c, s) in other.triangles)
                {
                    if (s != section) continue;
                    int Map(int index)
                    {
                        if (!map.TryGetValue(index, out var mapped))
                        {
                            mapped = Vertex(other.positions[index], other.uvs[index], section);
                            map[index] = mapped;
                        }
                        return mapped;
                    }
                    triangles.Add((paint, Map(a), Map(b), Map(c), section));
                }
            }

            /// <summary>A mesh with one submesh per paint, in the given order; paints with no triangles are left out.</summary>
            public GreekMesh Build(params GreekMaterial[] order)
            {
                var used = new List<GreekMaterial>();
                foreach (var paint in order) if (triangles.Exists(t => t.paint == paint)) used.Add(paint);
                foreach (var t in triangles) if (!used.Contains(t.paint)) used.Add(t.paint);
                var mesh = new Mesh { name = "Greek ship part" };
                if (positions.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = used.Count;
                for (var i = 0; i < used.Count; i++)
                {
                    var list = new List<int>();
                    foreach (var t in triangles)
                    {
                        if (t.paint != used[i]) continue;
                        list.Add(t.a);
                        list.Add(t.b);
                        list.Add(t.c);
                    }
                    mesh.SetTriangles(list, i, false);
                }
                mesh.RecalculateNormals();
                if (seams.Count > 0)
                {
                    // Round bodies stay smooth across the place where their UV wraps.
                    var normals = mesh.normals;
                    foreach (var (a, b) in seams)
                    {
                        var joined = (normals[a] + normals[b]).normalized;
                        normals[a] = joined;
                        normals[b] = joined;
                    }
                    mesh.normals = normals;
                }
                // Tangents follow the UVs (metres along planks, spars and cloths), so the ship paint's normal maps lie
                // along the grain and the weave.
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return new GreekMesh { Mesh = mesh, Slots = used.ToArray() };
            }
        }
    }
}
