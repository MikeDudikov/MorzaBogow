using System;
using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Every building of a port that breaks element by element (docs/art/DESTRUCTION.md): the School, the store and its
    /// houses, the workshop, the town's houses and its wall. Each piece has its own structure of elements, built the first
    /// time it is damaged. The settlement's hull gives the damage of the whole; the shots that struck it share it out:
    /// where they fell the buildings break more, elsewhere less, and with no shots all alike. A building's damage never
    /// goes back unless the hull rises (walls raised). The dock still falls as one piece into its seeded ruin.
    /// </summary>
    public sealed partial class PortBuildings
    {
        /// <summary>How strongly the shots draw the damage to the buildings they struck (0: all alike, 1: in proportion).</summary>
        public const float DamageDrawnToShots = 0.75f;

        sealed class Breakable
        {
            public PortBuilding Kind;
            public Piece Piece;
            public string Prefix;
            public Func<(StructureGraph, ElementShape[])> Model;
            public Func<Vector3, float> Rest;
            /// <summary>How far a point of the buildings' frame is from it, in metres (0 inside).</summary>
            public Func<Vector3, float> Distance;
            public StructureDamageView View;
            public bool Stood, Hidden;
            public float Level;
        }

        readonly List<Breakable> breakables = new List<Breakable>();
        double lastHull = double.NaN;
        bool wasRuined, firstDamage = true;

        /// <summary>How many buildings are taken apart into elements a frame while the port is in sight.</summary>
        public static int ModelsPerFrame = 1;

        /// <summary>All the views of a building's damage, one per piece (a house, the wall), made as they are first damaged.</summary>
        public List<StructureDamageView> DamagesOf(PortBuilding kind)
        {
            var list = new List<StructureDamageView>();
            foreach (var item in breakables) if (item.Kind == kind && item.View != null) list.Add(item.View);
            return list;
        }

        /// <summary>The building's pieces that break element by element (all but the dock's).</summary>
        public int BreakablePieces(PortBuilding kind) => breakables.FindAll(item => item.Kind == kind).Count;

        bool Breaks(PortBuilding kind) => breakables.Exists(item => item.Kind == kind);

        /// <summary>The record has failures or scars of a piece.</summary>
        bool Recorded(string prefix) =>
            Record.Failures.Exists(entry => entry.Element.StartsWith(prefix)) || Record.Scars.Exists(scar => scar.Element.StartsWith(prefix));

        /// <summary>Which building falls first when the port does: the one that shows most.</summary>
        static int FallOrder(PortBuilding kind) => kind switch
        {
            PortBuilding.SageSchool => 0,
            PortBuilding.Town => 1,
            PortBuilding.Workshop => 2,
            PortBuilding.Store => 3,
            _ => 4,
        };

        void AddBreakable(Structure item, Piece piece, string prefix, Func<(StructureGraph, ElementShape[])> model, Func<Vector3, float> rest,
                          Func<Vector3, float> distance) =>
            breakables.Add(new Breakable { Kind = item.Kind, Piece = piece, Prefix = prefix, Model = model, Rest = rest, Distance = distance });

        static bool Built(PortBuilding kind, Settlement settlement) => kind switch
        {
            PortBuilding.Store => true,
            PortBuilding.Workshop => settlement.HasWorkshop,
            PortBuilding.Town => settlement.IsTown,
            PortBuilding.SageSchool => settlement.HasSageSchool,
            PortBuilding.Dock => settlement.HasDock,
            _ => false,
        };

        // ---------- Following the hull ----------

        void UpdateDamage(Settlement settlement, bool ruined, float delta)
        {
            var hull = settlement.Hull;
            // Only a hull that rises (walls raised), or a port raised again where it fell, puts anything back.
            var mend = !double.IsNaN(lastHull) && hull > lastHull + 1e-6 || wasRuined && !ruined;
            lastHull = hull;
            wasRuined = ruined;
            var damage = DamagePlan.Damage(hull, settlement.MaxHull);
            var standing = new List<Breakable>();
            foreach (var item in breakables)
                if (!ruined && Built(item.Kind, settlement) && Rising != item.Kind) standing.Add(item);
            ShareOut(damage, standing, mend);
            // Taking every building apart at once costs some tens of milliseconds: in sight, from the first shot on, the
            // buildings' elements are made a few a frame (ModelsPerFrame), before they are needed; the first time the port
            // is shown, or after it was out of sight, all at once.
            var budget = continuous && !firstDamage ? ModelsPerFrame : int.MaxValue;
            firstDamage = false;
            // First what is damaged now, or by the record (a loaded game shows what had fallen, whatever its share is now);
            // then, from the first shot on, the rest ahead of need.
            foreach (var item in standing)
                if (item.View == null && budget > 0 && (item.Level >= DamagePlan.SurfaceFrom || Recorded(item.Prefix))) { Ensure(item); budget--; }
            if (damage > 0f)
                foreach (var item in standing)
                    if (item.View == null && budget > 0) { Ensure(item); budget--; }

            foreach (var item in breakables)
            {
                var stands = standing.Contains(item);
                if (stands && item.View != null)
                {
                    // Seen to fall only when it stood in sight a moment ago.
                    item.View.Apply(item.Level, false, continuous && item.Stood, mend);
                }
                item.Stood = stands;
                if (item.View == null) continue;
                item.View.Animate(delta);
                ShowBroken(item);
            }
            continuous = true;
            if (!Ruined) return;
            foreach (var pair in structures)
            {
                // A building that came down whole: only its ruin is left once its last piece lies still.
                var kind = pair.Key;
                var structure = pair.Value;
                if (!structure.Root.activeSelf || !Breaks(kind)) continue;
                var busy = false;
                var broken = false;
                foreach (var item in breakables)
                {
                    if (item.Kind != kind || item.View == null) continue;
                    busy |= item.View.Busy;
                    broken |= item.View.Broken;
                }
                if (!busy && broken) structure.Root.SetActive(false);
            }
        }

        /// <summary>
        /// Each standing building's share of the damage: the settlement's, drawn toward the buildings the shots struck by
        /// how many of them fell nearest each (DamageDrawnToShots), never lower than before unless the hull rose.
        /// </summary>
        void ShareOut(float damage, List<Breakable> standing, bool mend)
        {
            if (standing.Count == 0) return;
            var near = new float[standing.Count];
            var counted = 0;
            foreach (var impact in Record.Impacts)
            {
                var at = new Vector3(impact.X, impact.Y, impact.Z);
                var best = -1;
                var closest = 12f;
                for (var i = 0; i < standing.Count; i++)
                {
                    var d = standing[i].Distance(at);
                    if (d < closest) { closest = d; best = i; }
                }
                if (best < 0) continue;
                near[best]++;
                counted++;
            }
            const float spread = 0.3f;
            var n = standing.Count;
            for (var i = 0; i < n; i++)
            {
                var share = counted > 0 ? (near[i] / counted + spread / n) / (1f + spread) : 1f / n;
                var level = Mathf.Clamp01(damage * (1f + DamageDrawnToShots * (n * share - 1f)));
                standing[i].Level = mend ? level : Mathf.Max(standing[i].Level, level);
            }
        }

        void Ensure(Breakable item)
        {
            if (item.View != null) return;
            var (graph, shapes) = item.Model();
            var host = new StructureHost
            {
                RestHeight = item.Rest,
                Materials = slots =>
                {
                    var list = new Material[slots.Length];
                    for (var i = 0; i < list.Length; i++) list[i] = MaterialFor(slots[i], materials);
                    return list;
                },
                RubbleMaterials = slots =>
                {
                    var list = new Material[slots.Length];
                    for (var i = 0; i < list.Length; i++) list[i] = RubbleMaterialFor(slots[i], materials);
                    return list;
                },
                Owned = owned,
            };
            var view = new StructureDamageView(root, $"{item.Kind} {item.Piece.Root.name} (damage)", graph, shapes, host, item.Prefix,
                DestructionHash.Mix((uint)seed, (int)DestructionHash.Of(item.Prefix)));
            // In the piece's own frame: on its foot, its +X toward the harbour, as the piece stands.
            view.Frame.localPosition = item.Piece.Root.localPosition;
            view.Frame.localRotation = item.Piece.Root.localRotation;
            host.Burst = (point, material, strength, hash) => Burst(view.Frame, point, material, strength, hash);
            view.Record = Record;
            item.View = view;
        }

        /// <summary>The settlement fell: each piece of the building comes down whole, seen falling if it stood in sight.</summary>
        void RuinKind(Structure structure, bool animate)
        {
            var busy = false;
            foreach (var item in breakables)
            {
                if (item.Kind != structure.Kind) continue;
                Ensure(item);
                item.View.Apply(1f, true, animate && continuous, false);
                busy |= item.View.Busy;
                ShowBroken(item);
            }
            structure.Root.SetActive(animate && continuous && busy);
        }

        /// <summary>While a piece has failed elements, its one mesh, roof and base give way to its elements.</summary>
        void ShowBroken(Breakable item)
        {
            var piece = item.Piece;
            var broken = item.View != null && item.View.Broken;
            var body = piece.Body != null ? piece.Body.GetComponent<MeshRenderer>() : null;
            if (broken)
            {
                if (piece.Whole != null && piece.Whole.gameObject.activeSelf) piece.Whole.gameObject.SetActive(false);
                if (piece.Roof != null && piece.Roof.gameObject.activeSelf) piece.Roof.gameObject.SetActive(false);
                if (body != null && body.enabled) body.enabled = false;
                item.Hidden = true;
                return;
            }
            if (!item.Hidden) return;
            item.Hidden = false;
            if (body != null) body.enabled = true;
            if (!Ruined && Rising != item.Kind) Stage(piece, 1f);
        }

        /// <summary>How far a point of the buildings' frame lies from a piece's footprint (u toward the port, z along the shore).</summary>
        Func<Vector3, float> FootprintDistance(Piece piece) => point =>
        {
            var u = point.x * toward;
            var du = Mathf.Max(0f, Mathf.Abs(u - piece.U) - piece.W * 0.5f);
            var dz = Mathf.Max(0f, Mathf.Abs(point.z - piece.Z) - piece.D * 0.5f);
            return Mathf.Sqrt(du * du + dz * dz);
        };

        /// <summary>Where a fallen piece of a house or a shed comes to rest: on its floor inside, on the hill outside.</summary>
        Func<Vector3, float> PieceRest(Piece piece, float floor, float w, float d) => point =>
        {
            if (floor > 0f && Mathf.Abs(point.x) <= w * 0.5f && Mathf.Abs(point.z) <= d * 0.5f) return floor;
            var at = piece.Root.localPosition + piece.Root.localRotation * point;
            return Ground(toward * at.x, at.z) - piece.Root.localPosition.y;
        };

        // ---------- A house ----------

        /// <summary>
        /// A house or store of AddHouse taken apart: the stone socle (foundation); its walls in stretches, the front's beside
        /// and over the door, the ends with their gables, of sun-dried brick under plaster or of stone; the door leaf with its
        /// frame; the roof in bays a side resting on the long wall under its eave and the end wall it meets.
        /// </summary>
        (StructureGraph, ElementShape[]) BuildHouseModel(float w, float d, float h, GreekMaterial walls, Func<Vector3, float> rest)
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);

            const float socle = 0.7f, t = 0.3f, doorTop = 1.9f, overhang = 0.35f, pitch = RoofPitch;
            var stone = walls == GreekMaterial.Stone;
            var material = stone ? BuildMaterial.Stone : BuildMaterial.Mudbrick;
            var top = stone ? GreekMaterial.Stone : GreekMaterial.Mudbrick;
            var door = Mathf.Min(0.55f, d * 0.15f);
            var opening = door + 0.12f;

            var b = Builder();
            b.Box(GreekMaterial.Stone, new Vector3(0f, socle * 0.5f - 0.15f, 0f), new Vector3(w * 0.5f + 0.04f, socle * 0.5f + 0.15f, d * 0.5f + 0.04f), Quaternion.identity);
            var ground = Add("socle", ElementRole.Foundation, BuildMaterial.Stone, DetachMethod.Fixed, b.Build(GreekMaterial.Stone), Vector3.zero,
                "rubble socle set into the slope", new ElementShape());

            // The courses as AddHouse lays them: where the door's opening ends and the lintel's stretch begins.
            var course = (h - socle) / 6f;
            var lintelFrom = h;
            for (var k = 0; k < 6; k++)
                if (socle + k * course >= doorTop) { lintelFrom = socle + k * course; break; }
            var rise = w * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var stretches = new List<(string id, bool alongZ, float line, float from, float to, float y0, Vector3 outward, bool stump)>
            {
                ("wall.front.left", true, w * 0.5f - t * 0.5f, -d * 0.5f, -opening, socle, Vector3.right, true),
                ("wall.front.right", true, w * 0.5f - t * 0.5f, opening, d * 0.5f, socle, Vector3.right, true),
                ("wall.front.lintel", true, w * 0.5f - t * 0.5f, -opening, opening, lintelFrom, Vector3.right, false),
            };
            var backs = d > 4.5f ? 2 : 1;
            for (var k = 0; k < backs; k++)
                stretches.Add(($"wall.back.{k}", true, -w * 0.5f + t * 0.5f, -d * 0.5f + k * d / backs, -d * 0.5f + (k + 1) * d / backs, socle, Vector3.left, true));
            stretches.Add(("wall.end.minus", false, -(d * 0.5f - t * 0.5f), -(w * 0.5f - t), w * 0.5f - t, socle, Vector3.back, true));
            stretches.Add(("wall.end.plus", false, d * 0.5f - t * 0.5f, -(w * 0.5f - t), w * 0.5f - t, socle, Vector3.forward, true));
            var index = new Dictionary<string, int>();
            foreach (var (id, alongZ, line, from, to, y0, outward, stump) in stretches)
            {
                var length = to - from;
                var middle = (from + to) * 0.5f;
                var height = h - y0;
                var centre = alongZ ? new Vector3(line, y0 + height * 0.5f, middle) : new Vector3(middle, y0 + height * 0.5f, line);
                var half = alongZ ? new Vector3(t * 0.5f, height * 0.5f, length * 0.5f) : new Vector3(length * 0.5f, height * 0.5f, t * 0.5f);
                var wall = Builder();
                wall.Box(walls, centre, half, Quaternion.identity);
                if (id == "wall.front.left" || id == "wall.front.right")
                {
                    // Its small window, as AddHouse cuts it.
                    var z = Mathf.Sign(middle) * d * 0.3f;
                    wall.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f + 0.01f, h * 0.68f, z), new Vector3(0.03f, 0.22f, 0.18f), Quaternion.identity);
                }
                if (id == "wall.front.lintel")
                    wall.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.02f, 1.95f, 0f), new Vector3(0.08f, 0.07f, door + 0.2f), Quaternion.identity);
                if (!alongZ)
                {
                    // The gable over an end wall, a wall's thickness deep, the wall's own finish.
                    var z0 = line - Mathf.Sign(line) * t * 0.5f;
                    var z1 = line + Mathf.Sign(line) * t * 0.5f;
                    Gable3(wall, walls, w, h, rise, z0, z1);
                }
                var hash = DestructionHash.Mix((uint)seed, (int)DestructionHash.Of(id + "." + w + "." + d));
                var foot = new Vector3(centre.x, y0, centre.z);
                var shape = new ElementShape { Centre = centre, Size = half * 2f, Base = foot };
                if (stump)
                {
                    var s = Builder();
                    JaggedWall(s, foot, alongZ, length, t, DestructionHash.Range(hash, 1, 0.2f, 0.7f), hash, walls, top);
                    shape.Stump = s.Build(walls, top);
                }
                var heap = Builder();
                foreach (var sign in new[] { -1f, 1f })
                {
                    var across = alongZ ? new Vector3(sign * (t * 0.5f + 0.3f), 0f, 0f) : new Vector3(0f, 0f, sign * (t * 0.5f + 0.3f));
                    // On the floor inside, on the ground outside.
                    var mound = new Vector3(foot.x, 0f, foot.z) + across;
                    mound.y = rest(mound);
                    var radii = alongZ ? new Vector2(0.4f, length * 0.42f) : new Vector2(length * 0.42f, 0.4f);
                    Heap(heap, top, mound, radii.x, radii.y, 0.15f + 0.25f * (sign * Vector3.Dot(across.normalized, outward) > 0f ? 1f : 0.7f), hash + (uint)(sign > 0f ? 1 : 2));
                    if (stone) Blocks(heap, mound, radii, hash + 7u);
                }
                shape.Heap = heap.Build(top);
                var supports = id == "wall.front.lintel" ? new[] { index["wall.front.left"], index["wall.front.right"] } : new[] { ground };
                var made = Add(id, ElementRole.Wall, material, DetachMethod.Crumble, wall.Build(walls, GreekMaterial.Pitch, GreekMaterial.Wood), outward,
                    id == "wall.front.lintel" ? "over a timber lintel across the door" : stone ? "dressed courses on the socle" : "brick courses in mud mortar on the socle",
                    shape, supports);
                graph[made].Primary = true;
                index[id] = made;
            }

            var leaf = Builder();
            foreach (var side in new[] { -1f, 1f })
                leaf.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.05f, 1.0f, side * (door + 0.06f)), new Vector3(0.07f, 1.0f, 0.06f), Quaternion.identity);
            leaf.Box(GreekMaterial.Pitch, new Vector3(w * 0.5f + 0.03f, 0.95f, 0f), new Vector3(0.04f, 0.95f, door), Quaternion.identity);
            Add("door", ElementRole.Decor, BuildMaterial.Timber, DetachMethod.Drop, leaf.Build(GreekMaterial.Wood, GreekMaterial.Pitch), Vector3.right, "hung in its frame",
                new ElementShape { Radius = 0.06f }, index["wall.front.left"], index["wall.front.right"]);

            // The roof in bays: on the long wall under its eave and the end wall its bay meets.
            var bays = d > 4f ? 2 : 1;
            var reach = d * 0.5f + overhang;
            var run = w * 0.5f + overhang;
            var slope = pitch * Mathf.Deg2Rad;
            var thick = 0.1f / Mathf.Cos(slope);
            float TopAt(float x) => h + rise - Mathf.Abs(x) * Mathf.Tan(slope) + 0.12f + thick;
            foreach (var side in new[] { 1f, -1f })
            {
                for (var k = 0; k < bays; k++)
                {
                    var z0 = -reach + k * 2f * reach / bays;
                    var z1 = -reach + (k + 1) * 2f * reach / bays;
                    var zc = (z0 + z1) * 0.5f;
                    var bay = Builder();
                    RoofBay(bay, w, d, h, overhang, pitch, side, z0, z1, side > 0f, k == bays - 1);
                    var hash = DestructionHash.Mix((uint)seed, (int)DestructionHash.Of($"roof.{side}.{k}.{w}.{d}"));
                    var shape = new ElementShape
                    {
                        EaveEnd = new Vector3(side * run, TopAt(run), zc),
                        RidgeEnd = new Vector3(0f, TopAt(0f), zc),
                        EaveHinge = new Vector3(side * w * 0.5f, h, zc),
                        InnerHinge = new Vector3(0f, h + rise, zc),
                    };
                    var inside = Builder();
                    TileHeap(inside, new Vector3(side * w * 0.18f, socle, zc), Mathf.Max(0.3f, w * 0.2f), (z1 - z0) * 0.36f, 0.35f, new Vector3(side * w * 0.5f, h * 0.7f, zc), hash);
                    shape.Heap = inside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    var outside = Builder();
                    var outX = side * (w * 0.5f + 0.7f);
                    TileHeap(outside, new Vector3(outX, rest(new Vector3(outX, 0f, zc)), zc), 0.55f, (z1 - z0) * 0.4f, 0.3f, new Vector3(side * w * 0.5f, socle + 0.4f, zc), hash + 1u);
                    shape.HeapOut = outside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    var supports = new List<int>();
                    foreach (var (id, alongZ, _, from, to, _, outward, _) in stretches)
                    {
                        if (alongZ && outward.x == side && Mathf.Min(to, z1) - Mathf.Max(from, z0) > 0.1f) supports.Add(index[id]);
                        if (!alongZ && (outward.z > 0f ? z1 >= reach - 0.01f : z0 <= -reach + 0.01f)) supports.Add(index[id]);
                    }
                    var made = Add($"roof.{(side > 0f ? "front" : "back")}.{k}", ElementRole.Roof, BuildMaterial.Ceramic, DetachMethod.CaveIn,
                        bay.Build(GreekMaterial.Terracotta, GreekMaterial.Wood), new Vector3(side, 0f, 0f), "pan and cover tiles on rafters over the walls", shape, supports.ToArray());
                    graph[made].Primary = true;
                }
            }
            return (graph, shapes.ToArray());
        }

        // ---------- The workshop ----------

        /// <summary>
        /// The workshop's open shed of AddWorkshop taken apart: the trestles, the machine on them and the stacked timber stay
        /// on the ground; six posts set in it; the dry-stone back wall and the forge chimney; the roof in bays a side resting
        /// on the posts under its eave, and on the back wall on the uphill side.
        /// </summary>
        (StructureGraph, ElementShape[]) BuildShedModel(float w, float d, float h, Func<Vector3, float> rest)
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);

            var gear = Builder();
            foreach (var pz in new[] { -0.8f, 0.8f })
                gear.Box(GreekMaterial.Wood, new Vector3(0.6f, 0.45f, pz), new Vector3(1.2f, 0.12f, 0.12f), Quaternion.identity);
            gear.Box(GreekMaterial.Wood, new Vector3(0.6f, 0.62f, 0f), new Vector3(1.1f, 0.1f, 0.75f), Quaternion.identity);
            foreach (var pz in new[] { -0.45f, 0.45f })
                gear.Box(GreekMaterial.Wood, new Vector3(0.9f, 1.3f, pz), new Vector3(0.1f, 0.65f, 0.1f), Quaternion.identity);
            gear.Box(GreekMaterial.Wood, new Vector3(0.2f, 1.25f, 0f), new Vector3(1.0f, 0.08f, 0.08f), Quaternion.Euler(0f, 0f, 28f));
            gear.Box(GreekMaterial.Rope, new Vector3(0.9f, 1.1f, 0f), new Vector3(0.12f, 0.12f, 0.42f), Quaternion.identity);
            for (var i = 0; i < 4; i++)
                gear.Box(GreekMaterial.Wood, new Vector3(w * 0.5f + 0.7f, 0.18f + (i / 2) * 0.32f, d * 0.5f - 0.6f - (i % 2) * 0.36f), new Vector3(0.16f, 0.16f, 1.2f), Quaternion.Euler(0f, 90f, 0f));
            var ground = Add("gear", ElementRole.Foundation, BuildMaterial.Timber, DetachMethod.Fixed, gear.Build(GreekMaterial.Wood, GreekMaterial.Rope), Vector3.zero,
                "standing on the ground", new ElementShape());

            var posts = new List<(int index, float x, float z)>();
            var column = 0;
            foreach (var px in new[] { -w * 0.5f + 0.2f, w * 0.5f - 0.2f })
            foreach (var pz in new[] { -d * 0.5f + 0.2f, 0f, d * 0.5f - 0.2f })
            {
                var post = Builder();
                post.Box(GreekMaterial.Wood, new Vector3(px, h * 0.5f, pz), new Vector3(0.14f, h * 0.5f, 0.14f), Quaternion.identity);
                var shape = new ElementShape { Centre = new Vector3(px, h * 0.5f, pz), Size = new Vector3(0.28f, h, 0.28f), Radius = 0.14f, Base = new Vector3(px, 0f, pz) };
                var made = Add($"post.{column}", ElementRole.LoadBearing, BuildMaterial.Timber, DetachMethod.Topple, post.Build(GreekMaterial.Wood), new Vector3(Mathf.Sign(px), 0f, 0f),
                    "set in a post hole", shape, ground);
                graph[made].Column = 100 + column++;
                graph[made].Primary = true;
                posts.Add((made, px, pz));
            }

            // The back wall uphill and the chimney: dry stone, breaking into rubble and squared blocks.
            var wallX = -w * 0.5f + 0.2f;
            var back = Builder();
            back.Box(GreekMaterial.Stone, new Vector3(wallX, h * 0.5f, 0f), new Vector3(0.3f, h * 0.5f, d * 0.5f), Quaternion.identity);
            var backHash = DestructionHash.Mix((uint)seed, 610);
            var backShape = new ElementShape { Centre = new Vector3(wallX, h * 0.5f, 0f), Size = new Vector3(0.6f, h, d), Base = new Vector3(wallX, 0f, 0f) };
            var backStump = Builder();
            JaggedWall(backStump, backShape.Base, true, d, 0.6f, DestructionHash.Range(backHash, 1, 0.5f, 1.2f), backHash, GreekMaterial.Stone, GreekMaterial.Stone);
            backShape.Stump = backStump.Build(GreekMaterial.Stone);
            backShape.Heap = StoneHeaps(backShape.Base, true, d, 0.6f, rest, backHash);
            // It carries the uphill eave: when it goes, that side's roof slides off like the posts' side.
            var backIndex = Add("wall.back", ElementRole.LoadBearing, BuildMaterial.Stone, DetachMethod.Crumble, back.Build(GreekMaterial.Stone), Vector3.left,
                "dry stone on the hill", backShape, ground);
            graph[backIndex].Primary = true;

            var chimneyAt = new Vector3(-w * 0.5f + 0.7f, 0f, -d * 0.5f + 0.7f);
            var chimney = Builder();
            chimney.Box(GreekMaterial.Stone, chimneyAt + Vector3.up * ((h + 2.2f) * 0.5f), new Vector3(0.55f, (h + 2.2f) * 0.5f, 0.55f), Quaternion.identity);
            chimney.Box(GreekMaterial.Pitch, chimneyAt + Vector3.up * (h + 2.2f), new Vector3(0.35f, 0.02f, 0.35f), Quaternion.identity);
            var chimneyHash = DestructionHash.Mix((uint)seed, 620);
            var chimneyShape = new ElementShape { Centre = chimneyAt + Vector3.up * ((h + 2.2f) * 0.5f), Size = new Vector3(1.1f, h + 2.2f, 1.1f), Base = chimneyAt };
            var chimneyStump = Builder();
            JaggedWall(chimneyStump, chimneyAt, true, 1.1f, 1.1f, DestructionHash.Range(chimneyHash, 1, h * 0.35f, h * 0.7f), chimneyHash, GreekMaterial.Stone, GreekMaterial.Stone);
            chimneyShape.Stump = chimneyStump.Build(GreekMaterial.Stone);
            chimneyShape.Heap = StoneHeaps(chimneyAt, true, 1.1f, 1.1f, rest, chimneyHash);
            var chimneyIndex = Add("chimney", ElementRole.Wall, BuildMaterial.Stone, DetachMethod.Crumble, chimney.Build(GreekMaterial.Stone, GreekMaterial.Pitch), new Vector3(-1f, 0f, -1f).normalized,
                "dry stone, the forge in its foot", chimneyShape, ground);
            graph[chimneyIndex].Primary = true;

            const float overhang = 0.3f, pitch = RoofPitch;
            var rise = w * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var run = w * 0.5f + overhang;
            var slope = pitch * Mathf.Deg2Rad;
            var thick = 0.1f / Mathf.Cos(slope);
            float TopAt(float x) => h + rise - Mathf.Abs(x) * Mathf.Tan(slope) + 0.12f + thick;
            var reach = d * 0.5f + overhang;
            foreach (var side in new[] { 1f, -1f })
            for (var k = 0; k < 2; k++)
            {
                var z0 = -reach + k * reach;
                var z1 = z0 + reach;
                var zc = (z0 + z1) * 0.5f;
                var bay = Builder();
                RoofBay(bay, w, d, h, overhang, pitch, side, z0, z1, side > 0f, k == 1);
                var hash = DestructionHash.Mix((uint)seed, 640 + k + (side > 0f ? 0 : 10));
                var shape = new ElementShape
                {
                    EaveEnd = new Vector3(side * run, TopAt(run), zc), RidgeEnd = new Vector3(0f, TopAt(0f), zc),
                    EaveHinge = new Vector3(side * w * 0.5f, h, zc), InnerHinge = new Vector3(0f, h + rise, zc),
                };
                var inside = Builder();
                var inX = side * w * 0.22f;
                TileHeap(inside, new Vector3(inX, rest(new Vector3(inX, 0f, zc)), zc), w * 0.2f, reach * 0.4f, 0.3f, new Vector3(side * w * 0.5f, h * 0.6f, zc), hash);
                shape.Heap = inside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                var outside = Builder();
                var outX = side * (w * 0.5f + 0.8f);
                TileHeap(outside, new Vector3(outX, rest(new Vector3(outX, 0f, zc)), zc), 0.6f, reach * 0.4f, 0.3f, new Vector3(side * w * 0.5f, 0.6f, zc), hash + 1u);
                shape.HeapOut = outside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                var supports = new List<int>();
                foreach (var (index, x, z) in posts)
                    if (Mathf.Sign(x) == side && z >= z0 - 0.3f && z <= z1 + 0.3f) supports.Add(index);
                if (side < 0f) supports.Add(backIndex);
                var made = Add($"roof.{(side > 0f ? "front" : "back")}.{k}", ElementRole.Roof, BuildMaterial.Ceramic, DetachMethod.CaveIn,
                    bay.Build(GreekMaterial.Terracotta, GreekMaterial.Wood), new Vector3(side, 0f, 0f), "tiles on rafters over the posts", shape, supports.ToArray());
                graph[made].Primary = true;
            }
            return (graph, shapes.ToArray());
        }

        /// <summary>Rubble of a stone wall or stack slumped about its foot: a low heap each side and squared blocks among it.</summary>
        GreekMesh StoneHeaps(Vector3 foot, bool alongZ, float length, float thickness, Func<Vector3, float> rest, uint hash)
        {
            var heap = Builder();
            foreach (var sign in new[] { -1f, 1f })
            {
                var across = alongZ ? new Vector3(sign * (thickness * 0.5f + 0.35f), 0f, 0f) : new Vector3(0f, 0f, sign * (thickness * 0.5f + 0.35f));
                var mound = foot + across;
                mound.y = rest(mound);
                var radii = alongZ ? new Vector2(0.45f, length * 0.42f) : new Vector2(length * 0.42f, 0.45f);
                Heap(heap, GreekMaterial.Stone, mound, radii.x, radii.y, 0.25f, hash + (uint)(sign > 0f ? 1 : 2));
                Blocks(heap, mound, radii, hash + (uint)(sign > 0f ? 3 : 4));
            }
            return heap.Build(GreekMaterial.Stone);
        }

        // ---------- The town's wall ----------

        /// <summary>
        /// The town's curtain and towers of AddWalls taken apart, in the settlement's own frame: each 2.4 m stretch of the
        /// curtain with its merlons, each tower and its tiled cap. Stretches and towers stand on the rock; a cap on its tower.
        /// </summary>
        (StructureGraph, ElementShape[]) BuildWallModel()
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports) =>
                AddElement(graph, shapes, id, role, material, detach, mesh, outward, joint, shape, supports);
            const float back = -14.6f, front = 0.6f, side = 9.2f, gate = 3.6f;
            var middle = Local((back + front) * 0.5f, 0f, 0f);
            Func<Vector3, float> rest = point => Ground(point.x * toward, point.z);
            var ground = Add("rock", ElementRole.Foundation, BuildMaterial.Stone, DetachMethod.Fixed, null, Vector3.zero, "the levelled rock",
                new ElementShape { Centre = middle, Size = new Vector3(PlotNear - PlotFar, 1f, PlotHalfWidth * 2f) });

            void Stretch(string name, float u0, float z0, float u1, float z1)
            {
                var length = Mathf.Sqrt((u1 - u0) * (u1 - u0) + (z1 - z0) * (z1 - z0));
                var count = Mathf.Max(1, Mathf.CeilToInt(length / 2.4f));
                var along = Local(u1 - u0, z1 - z0, 0f).normalized;
                var alongZ = Mathf.Abs(along.z) > 0.5f;
                var turn = Quaternion.LookRotation(along);
                var piece = length / count;
                for (var i = 0; i < count; i++)
                {
                    var t = (i + 0.5f) / count;
                    var u = Mathf.Lerp(u0, u1, t);
                    var z = Mathf.Lerp(z0, z1, t);
                    var level = Ground(u, z);
                    var foot = Local(u, z, level);
                    var b = Builder();
                    b.Box(GreekMaterial.Stone, Local(u, z, level + (WallHeight - 0.3f) * 0.5f), new Vector3(0.45f, (WallHeight + 0.3f) * 0.5f, piece * 0.5f + 0.06f), turn);
                    foreach (var end in new[] { -0.25f, 0.25f })
                        b.Box(GreekMaterial.Stone, Local(u, z, level + WallHeight + 0.3f) + along * (end * piece), new Vector3(0.47f, 0.3f, 0.38f), turn);
                    var outward = Vector3.Cross(Vector3.up, along).normalized;
                    if (Vector3.Dot(outward, foot - middle) < 0f) outward = -outward;
                    var hash = DestructionHash.Mix((uint)seed, (int)DestructionHash.Of($"wall.{name}.{i}"));
                    var shape = new ElementShape { Centre = foot + Vector3.up * (WallHeight * 0.5f), Size = new Vector3(0.9f, WallHeight, piece), Base = foot };
                    var s = Builder();
                    JaggedWall(s, foot, alongZ, piece + 0.12f, 0.9f, DestructionHash.Range(hash, 1, 0.6f, 1.6f), hash, GreekMaterial.Stone, GreekMaterial.Stone);
                    shape.Stump = s.Build(GreekMaterial.Stone);
                    shape.Heap = StoneHeaps(foot, alongZ, piece, 0.9f, rest, hash);
                    var made = Add($"wall.{name}.{i}", ElementRole.Wall, BuildMaterial.Stone, DetachMethod.Crumble, b.Build(GreekMaterial.Stone), outward,
                        "regular ashlar laid dry (R-MESSENE)", shape, ground);
                    graph[made].Primary = true;
                }
            }
            Stretch("back", back, -side, back, side);
            Stretch("south", back, -side, front, -side);
            Stretch("north", back, side, front, side);
            Stretch("gate.south", front, -side, front, -gate);
            Stretch("gate.north", front, gate, front, side);

            var towers = new[] { (back, -side), (back, side), (front, -side), (front, side), (front, -gate), (front, gate) };
            for (var k = 0; k < towers.Length; k++)
            {
                var (u, z) = towers[k];
                var level = Ground(u, z) - 0.6f;
                var top = level + WallHeight + 1.4f;
                var foot = Local(u, z, level);
                var b = Builder();
                b.Box(GreekMaterial.Stone, Local(u, z, level + (WallHeight + 1.4f - 0.3f) * 0.5f), new Vector3(1.1f, (WallHeight + 1.4f + 0.3f) * 0.5f, 1.1f), Quaternion.identity);
                var slit = Local(u, z, level + WallHeight + 0.6f);
                foreach (var face in new[] { -1f, 1f })
                foreach (var across in new[] { -0.45f, 0.45f })
                {
                    b.Box(GreekMaterial.Pitch, slit + new Vector3(face * 1.11f, 0f, across), new Vector3(0.02f, 0.32f, 0.07f), Quaternion.identity);
                    b.Box(GreekMaterial.Pitch, slit + new Vector3(across, 0f, face * 1.11f), new Vector3(0.07f, 0.32f, 0.02f), Quaternion.identity);
                }
                var outward = (foot - middle);
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.right;
                var hash = DestructionHash.Mix((uint)seed, 700 + k);
                var shape = new ElementShape { Centre = foot + Vector3.up * ((WallHeight + 1.4f) * 0.5f), Size = new Vector3(2.2f, WallHeight + 1.4f, 2.2f), Base = foot };
                var s = Builder();
                JaggedWall(s, foot, true, 2.2f, 2.2f, DestructionHash.Range(hash, 1, 1.0f, 2.2f), hash, GreekMaterial.Stone, GreekMaterial.Stone);
                shape.Stump = s.Build(GreekMaterial.Stone);
                shape.Heap = StoneHeaps(foot, true, 2.2f, 2.2f, rest, hash);
                var tower = Add($"tower.{k}", ElementRole.LoadBearing, BuildMaterial.Stone, DetachMethod.Crumble, b.Build(GreekMaterial.Stone, GreekMaterial.Pitch), outward,
                    "ashlar on the rock, a storey over the wall-walk", shape, ground);
                graph[tower].Primary = true;

                var cap = Builder();
                TiledGable(cap, 2.2f, 2.2f, 0f, GreekMaterial.Stone, 0.18f);
                var capMesh = cap.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood);
                var roof = Builder();
                roof.Append(capMesh, Local(u, z, top), Quaternion.identity);
                Discard(capMesh.Mesh);
                var tiles = Builder();
                var landing = foot + outward * 2f;
                landing.y = rest(landing);
                TileHeap(tiles, landing, 0.6f, 0.6f, 0.25f, foot + Vector3.up * 0.5f, hash + 9u);
                var roofShape = new ElementShape { Radius = 0.35f, Heap = tiles.Build(GreekMaterial.Terracotta, GreekMaterial.Wood) };
                var made = Add($"tower.{k}.roof", ElementRole.Roof, BuildMaterial.Ceramic, DetachMethod.Drop, roof.Build(GreekMaterial.Terracotta, GreekMaterial.Stone, GreekMaterial.Wood), outward,
                    "tiled cap on the tower's beams", roofShape, tower);
                graph[made].Primary = true;
            }
            return (graph, shapes.ToArray());
        }

        /// <summary>How far a point of the buildings' frame lies from the town's wall: from the line of its curtain round the plot.</summary>
        float WallDistance(Vector3 point)
        {
            const float back = -14.6f, front = 0.6f, side = 9.2f;
            var u = point.x * toward;
            var z = point.z;
            var inside = u >= back && u <= front && z >= -side && z <= side;
            if (inside) return Mathf.Min(Mathf.Min(u - back, front - u), Mathf.Min(z + side, side - z));
            var du = Mathf.Max(0f, Mathf.Max(back - u, u - front));
            var dz = Mathf.Max(0f, Mathf.Max(-side - z, z - side));
            return Mathf.Sqrt(du * du + dz * dz);
        }

        /// <summary>A gable's triangle as a slab from z0 to z1 over a wall w wide at h, rise high at its peak.</summary>
        static void Gable3(GreekShips.MeshBuilder b, GreekMaterial paint, float w, float h, float rise, float z0, float z1)
        {
            foreach (var (z, facing) in new[] { (z0, Mathf.Sign(z0 - z1)), (z1, Mathf.Sign(z1 - z0)) })
            {
                var a = b.Vertex(new Vector3(-w * 0.5f, h, z), new Vector2(-w * 0.5f, h));
                var c = b.Vertex(new Vector3(w * 0.5f, h, z), new Vector2(w * 0.5f, h));
                var top = b.Vertex(new Vector3(0f, h + rise, z), new Vector2(0f, h + rise));
                b.Triangle(paint, a, c, top, new Vector3(0f, 0f, facing));
            }
        }

        /// <summary>A few squared blocks fallen about a heap of stone rubble.</summary>
        static void Blocks(GreekShips.MeshBuilder b, Vector3 centre, Vector2 radii, uint hash)
        {
            for (var k = 0; k < 4; k++)
            {
                var size = DestructionHash.Range(hash, 300 + k, 0.18f, 0.32f);
                var p = centre + new Vector3(DestructionHash.Range(hash, 310 + k, -1.3f, 1.3f) * radii.x, size * 0.5f, DestructionHash.Range(hash, 320 + k, -1.1f, 1.1f) * radii.y);
                b.Box(GreekMaterial.Stone, p, new Vector3(size * 1.4f, size, size), Quaternion.Euler(DestructionHash.Range(hash, 330 + k, -20f, 20f), DestructionHash.Range(hash, 340 + k, 0f, 360f), DestructionHash.Range(hash, 350 + k, -20f, 20f)));
            }
        }

        static int AddElement(StructureGraph graph, List<ElementShape> shapes, string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh,
                              Vector3 outward, string joint, ElementShape shape, int[] supports)
        {
            shape.Whole = mesh;
            var box = mesh != null ? mesh.Mesh.bounds : new Bounds(shape.Centre, shape.Size);
            if (shape.Centre == Vector3.zero) shape.Centre = box.center;
            if (shape.Size == Vector3.zero) shape.Size = box.size;
            graph.Add(id, role, material, detach, box, outward, joint, supports);
            shapes.Add(shape);
            return graph.Count - 1;
        }
    }
}
