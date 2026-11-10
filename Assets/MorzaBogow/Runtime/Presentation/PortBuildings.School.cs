using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The Sages' School as a structure of elements that can fail (docs/art/DESTRUCTION.md): built the moment it is first
    /// damaged, drawn as one mesh until an element fails. Columns of five drums and a capital standing on the stylobate,
    /// the entablature in spans from capital to capital, the hall's walls of sun-dried brick on a stone socle, the roof in
    /// bays resting on the entablature and the hall's walls, a pediment at each end. The settlement's hull decides how
    /// much of it is broken (DamagePlan); the shots that struck it decide where.
    /// </summary>
    public sealed partial class PortBuildings
    {
        /// <summary>The drums of a column's shaft.</summary>
        public const int SchoolDrums = 5;

        Piece schoolPiece;
        GreekMesh schoolCrepis;
        DestructionRecord record;
        bool continuous = true;
        readonly Dictionary<GreekMaterial, ParticleSystem> chips = new Dictionary<GreekMaterial, ParticleSystem>();
        Mesh chipMesh;

        /// <summary>
        /// The settlement's destruction record, shared with the battle's save (DestructionBook); without one given, a record
        /// of its own.
        /// </summary>
        public DestructionRecord Record
        {
            get => record ??= new DestructionRecord();
            set
            {
                record = value;
                foreach (var item in breakables) if (item.View != null) item.View.Record = Record;
            }
        }

        /// <summary>The view of a building's damage, its first piece's (the School's only one), once it has been damaged.</summary>
        public StructureDamageView DamageOf(PortBuilding kind) => breakables.Find(item => item.Kind == kind && item.View != null)?.View;

        /// <summary>The settlement went out of sight: what changes before it is seen again is shown at rest, not falling.</summary>
        public void Unseen() => continuous = false;

        /// <summary>
        /// What has fallen is dulled by its own dust: whitewash and painted stone greyed and darker, the paint's blue and red
        /// faded, tiles duller, the dark door leaf brown; brick and timber as they are. Copies made once (Tint), never per frame.
        /// </summary>
        static Material RubbleMaterialFor(GreekMaterial paint, PlaceholderMaterials materials)
        {
            var clean = MaterialFor(paint, materials);
            if (clean == null) return null;
            return paint switch
            {
                GreekMaterial.Paint => Tint(clean, "dusty", new Color(0.8f, 0.76f, 0.68f), 0.1f),
                GreekMaterial.Stone => Tint(clean, "dusty", new Color(0.86f, 0.82f, 0.75f), 0.1f),
                GreekMaterial.Azure => Tint(clean, "dusty", new Color(0.3f, 0.37f, 0.5f), 0.1f),
                GreekMaterial.Ochre => Tint(clean, "dusty", new Color(0.56f, 0.28f, 0.18f), 0.1f),
                GreekMaterial.Terracotta => Tint(clean, "dusty", new Color(0.58f, 0.38f, 0.27f), 0.1f),
                // A door leaf of dark-stained wood lying in the dust, not a black hole in the ground.
                GreekMaterial.Pitch => Tint(clean, "dusty", new Color(0.24f, 0.2f, 0.16f), 0.1f),
                _ => clean,
            };
        }

        /// <summary>Where a fallen piece of the School comes to rest: on the step it lands on, or on the hill beside it.</summary>
        float SchoolRestHeight(Vector3 point)
        {
            for (var i = 2; i >= 0; i--)
            {
                var inset = i * 0.2f;
                if (Mathf.Abs(point.x) <= SchoolWidth * 0.5f - inset && Mathf.Abs(point.z) <= SchoolDepth * 0.5f - inset) return SchoolSteps * (i + 1) / 3f;
            }
            var piece = schoolPiece.Root;
            var at = piece.localPosition + piece.localRotation * point;
            return Ground(toward * at.x, at.z) - piece.localPosition.y;
        }

        // ---------- Shots that strike ----------

        /// <summary>
        /// A shot struck the settlement (the game's projectile_hit_settlement at its point): noted in the record, so later
        /// failures and scars come where the shots fell, and, when seen, dust and what flies off the stuff it struck.
        /// True when it drew its own effect.
        /// </summary>
        public bool Struck(Settlement settlement, Vector3 world, Vector3? from, WeaponType weapon, long tick, bool seen)
        {
            var local = root.InverseTransformPoint(world);
            local.y = Ground(toward * local.x, local.z) + 1f;
            // The way the shot flew: from the ship that loosed it, or else in from the sea.
            var direction = from.HasValue ? root.InverseTransformDirection(world - from.Value) : new Vector3(-local.x, 0f, -local.z);
            var stage = DamagePlan.StageOf(DamagePlan.Damage(settlement.Hull, settlement.MaxHull), settlement.Destroyed, false);
            Record.Strike(tick, local, direction, Category(weapon), stage);
            if (!seen || !root.gameObject.activeInHierarchy) return false;
            var (material, height) = StruckAt(local);
            local.y = height;
            var strength = Mathf.Clamp(0.25f + (float)WeaponCatalog.Get(weapon).Hull / 120f, 0.25f, 1.2f);
            Burst(root, local, material, strength, DestructionHash.Mix((uint)seed, (int)(tick & 0x7fffffff)));
            return true;
        }

        static string Category(WeaponType weapon) => weapon switch
        {
            WeaponType.ArcherBank => "arrow",
            WeaponType.JavelinBank => "javelin",
            WeaponType.Scorpion or WeaponType.Gastraphetes or WeaponType.DeckBallista => "bolt",
            WeaponType.LeadBallista => "shot",
            WeaponType.StoneCatapult or WeaponType.HeavyCatapult or WeaponType.Onager => "stone",
            _ => "missile",
        };

        /// <summary>What stands where a shot fell (in the buildings' frame) and how high it struck: the ground if nothing.</summary>
        (BuildMaterial, float) StruckAt(Vector3 local)
        {
            var u = local.x * toward;
            foreach (var pair in structures)
            {
                var item = pair.Value;
                if (!item.Root.activeSelf) continue;
                foreach (var piece in item.Pieces)
                {
                    if (Mathf.Abs(u - piece.U) > piece.W * 0.5f + 0.6f || Mathf.Abs(local.z - piece.Z) > piece.D * 0.5f + 0.6f) continue;
                    var foot = piece.Root.localPosition.y;
                    var material = pair.Key switch
                    {
                        PortBuilding.SageSchool => BuildMaterial.StuccoedStone,
                        PortBuilding.Workshop or PortBuilding.Dock => BuildMaterial.Timber,
                        PortBuilding.Town when piece.Root.name == "Wall" => BuildMaterial.Stone,
                        PortBuilding.Store when piece.Root.name == "Store" => BuildMaterial.Stone,
                        _ => BuildMaterial.Mudbrick,
                    };
                    return (material, foot + piece.H * 0.55f);
                }
            }
            return (BuildMaterial.Stone, Ground(u, local.z) + 0.1f);
        }

        // ---------- Dust and what flies ----------

        /// <summary>
        /// Dust and fragments of the stuff at a point of a frame: limestone chips and whitewash flakes, brick crumbs, broken
        /// tiles, splinters. Particles only, in their own budget (maxParticles); none in a scene without the spray material.
        /// </summary>
        void Burst(Transform frame, Vector3 point, BuildMaterial material, float strength, uint hash)
        {
            var at = frame.TransformPoint(point);
            var random = new System.Random((int)(hash & 0x7fffffff));
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var dust = DustSystem();
            if (dust != null)
            {
                var puffs = 3 + Mathf.RoundToInt(8f * strength);
                for (var i = 0; i < puffs; i++)
                {
                    var offset = new Vector3(Next(-1f, 1f), Next(0f, 0.6f), Next(-1f, 1f)) * (0.4f + 0.6f * strength);
                    dust.Emit(new ParticleSystem.EmitParams
                    {
                        position = at + offset,
                        velocity = new Vector3(offset.x, 0f, offset.z) * Next(0.3f, 1.2f) + Vector3.up * Next(0.4f, 1.6f),
                        startSize = Next(1.1f, 3f) * (0.5f + 0.5f * strength),
                        startLifetime = Next(2f, 3.8f),
                        rotation = Next(0f, 360f),
                        applyShapeToPosition = false,
                    }, 1);
                }
            }
            if (material == BuildMaterial.Ceramic)
            {
                var tilesSystem = TileSystem();
                if (tilesSystem == null) return;
                var count = 4 + Mathf.RoundToInt(10f * strength);
                for (var i = 0; i < count; i++)
                    tilesSystem.Emit(new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(Next(-0.8f, 0.8f), Next(0f, 0.3f), Next(-0.8f, 0.8f)),
                        velocity = new Vector3(Next(-2f, 2f), Next(1f, 3.5f), Next(-2f, 2f)),
                        rotation3D = new Vector3(Next(0f, 360f), Next(0f, 360f), Next(0f, 360f)),
                        angularVelocity3D = new Vector3(Next(-200f, 200f), Next(-200f, 200f), Next(-200f, 200f)),
                        startSize = Next(0.5f, 1f),
                        startLifetime = Next(0.8f, 1.4f),
                        applyShapeToPosition = false,
                    }, 1);
                return;
            }
            var paints = material switch
            {
                BuildMaterial.Stone => new[] { GreekMaterial.Stone, GreekMaterial.Stone },
                BuildMaterial.StuccoedStone => new[] { GreekMaterial.Stone, GreekMaterial.Paint },
                BuildMaterial.Mudbrick => new[] { GreekMaterial.Mudbrick, GreekMaterial.Paint },
                _ => new[] { GreekMaterial.Wood, GreekMaterial.Wood },
            };
            var pieces = 5 + Mathf.RoundToInt(14f * strength);
            for (var i = 0; i < pieces; i++)
            {
                var system = Chips(paints[i % 2]);
                if (system == null) return;
                var size = material == BuildMaterial.Mudbrick ? Next(0.4f, 1.1f) : Next(0.3f, 0.9f);
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(Next(-0.4f, 0.4f), Next(0f, 0.4f), Next(-0.4f, 0.4f)),
                    velocity = new Vector3(Next(-2.2f, 2.2f), Next(1.2f, 4f), Next(-2.2f, 2.2f)) * (0.6f + 0.5f * strength),
                    rotation3D = new Vector3(Next(0f, 360f), Next(0f, 360f), Next(0f, 360f)),
                    angularVelocity3D = new Vector3(Next(-300f, 300f), Next(-300f, 300f), Next(-300f, 300f)),
                    startSize = size,
                    startLifetime = Next(0.9f, 1.6f),
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>Small rough fragments of one paint (a chip of limestone, a crumb of brick, a splinter), tumbling under gravity.</summary>
        ParticleSystem Chips(GreekMaterial paint)
        {
            if (chips.TryGetValue(paint, out var system) && system != null) return system;
            var material = MaterialFor(paint, materials);
            if (material == null) return null;
            system = Particles($"Fragments ({paint})", 360, 1f);
            if (chipMesh == null)
            {
                // An irregular wedge 12 by 8 by 5 cm.
                var b = Builder();
                var top = b.Vertex(new Vector3(0.01f, 0.03f, 0.005f), Vector2.zero);
                var bottom = b.Vertex(new Vector3(-0.01f, -0.025f, 0f), Vector2.one);
                var rim = new[]
                {
                    b.Vertex(new Vector3(0.06f, 0f, 0.01f), new Vector2(1f, 0f)), b.Vertex(new Vector3(0.01f, 0.004f, 0.04f), new Vector2(0f, 1f)),
                    b.Vertex(new Vector3(-0.055f, -0.003f, 0.012f), new Vector2(1f, 1f)), b.Vertex(new Vector3(-0.01f, 0.002f, -0.038f), new Vector2(0f, 0f)),
                };
                for (var k = 0; k < 4; k++)
                {
                    var a = rim[k];
                    var c = rim[(k + 1) % 4];
                    var mid = (b.Position(a) + b.Position(c)) * 0.5f;
                    b.Triangle(GreekMaterial.Stone, top, a, c, mid + Vector3.up * 0.02f);
                    b.Triangle(GreekMaterial.Stone, bottom, c, a, mid - Vector3.up * 0.02f);
                }
                chipMesh = b.Build(GreekMaterial.Stone).Mesh;
                owned.Add(chipMesh);
            }
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = chipMesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            var main = system.main;
            main.startRotation3D = true;
            chips[paint] = system;
            return system;
        }

        // ---------- The School's elements ----------

        /// <summary>
        /// The School taken apart as it was put together (AddSchool, the same measures): the crepis and the hall's stone
        /// socle as foundations; per column five drums and a capital, the drum that gives under a blow fixed per column;
        /// the architrave and the frieze with its cornice over each intercolumniation, resting on the two capitals; the
        /// hall's brick walls in stretches; the roof in four bays a side resting on the frieze and the hall's wall under
        /// them; a pediment at each end on the short sides' frieze.
        /// </summary>
        (StructureGraph, ElementShape[]) BuildSchoolModel()
        {
            var graph = new StructureGraph();
            var shapes = new List<ElementShape>();
            int Add(string id, ElementRole role, BuildMaterial material, DetachMethod detach, GreekMesh mesh, Vector3 outward, string joint, ElementShape shape, params int[] supports)
            {
                owned.Add(mesh.Mesh);
                if (shape.Stump != null) owned.Add(shape.Stump.Mesh);
                if (shape.Heap != null) owned.Add(shape.Heap.Mesh);
                shape.Whole = mesh;
                var box = mesh.Mesh.bounds;
                if (shape.Centre == Vector3.zero) shape.Centre = box.center;
                if (shape.Size == Vector3.zero) shape.Size = box.size;
                graph.Add(id, role, material, detach, box, outward, joint, supports);
                shapes.Add(shape);
                return graph.Count - 1;
            }

            const float steps = SchoolSteps, w = SchoolWidth, d = SchoolDepth;
            var crepis = Builder();
            for (var i = 0; i < 3; i++)
            {
                var inset = i * 0.2f;
                crepis.Box(GreekMaterial.Stone, new Vector3(0f, (steps * (i + 1) / 3f - Sink) * 0.5f, 0f), new Vector3(w * 0.5f - inset, (steps * (i + 1) / 3f + Sink) * 0.5f, d * 0.5f - inset), Quaternion.identity);
            }
            var ground = Add("crepis", ElementRole.Foundation, BuildMaterial.Stone, DetachMethod.Fixed, crepis.Build(GreekMaterial.Stone), Vector3.zero, "laid on the levelled rock",
                new ElementShape { Centre = new Vector3(0f, steps * 0.5f, 0f), Size = new Vector3(w, steps, d) });

            var floor = Vector3.up * steps;
            var hall = floor + new Vector3(HallOffset, 0f, 0f);
            var course = (SchoolColumnHeight + SchoolEpistyle + SchoolFrieze) / HallCourses;
            var socle = Builder();
            // The socle without the door: the door leaf down to the threshold is the door's own element, so it never stands alone.
            WallCourse(socle, GreekMaterial.Stone, hall, HallWidth, HallDepth, 0f, course, HallWall, HallDoor, HallDoorTop);
            var socleIndex = Add("hall.socle", ElementRole.Foundation, BuildMaterial.Stone, DetachMethod.Fixed, socle.Build(GreekMaterial.Stone), Vector3.zero, "orthostates on the stylobate",
                new ElementShape());

            // Columns: drums on drums, the capital on the top drum.
            var columns = SchoolColumns();
            var capitals = new int[columns.Count];
            var shaft = SchoolColumnHeight * 0.88f;
            for (var c = 0; c < columns.Count; c++)
            {
                var axis = columns[c];
                var outward = new Vector3(Mathf.Abs(axis.x) >= SchoolColumnX - 0.01f ? Mathf.Sign(axis.x) : 0f, 0f, Mathf.Abs(axis.z) >= SchoolColumnZ - 0.01f ? Mathf.Sign(axis.z) : 0f).normalized;
                var hash = DestructionHash.Mix((uint)seed, 500 + c);
                var u = DestructionHash.Unit(hash, 0);
                var weak = u < 0.15f ? 0 : u < 0.45f ? 1 : u < 0.75f ? 2 : 3;
                var below = ground;
                for (var j = 0; j < SchoolDrums; j++)
                {
                    var t0 = j / (float)SchoolDrums;
                    var t1 = (j + 1) / (float)SchoolDrums;
                    var b = Builder();
                    DoricDrum(b, floor + axis, SchoolColumnHeight, SchoolColumnRadius, 20, t0, t1);
                    var mid = (t0 + t1) * 0.5f;
                    var shape = new ElementShape
                    {
                        Centre = floor + axis + Vector3.up * shaft * mid,
                        Size = new Vector3(2f * SchoolColumnRadius, shaft * (t1 - t0), 2f * SchoolColumnRadius),
                        Radius = SchoolColumnRadius * (Mathf.Lerp(1f, 0.8f, mid) + 0.025f * Mathf.Sin(mid * Mathf.PI)),
                        Base = floor + axis + Vector3.up * shaft * t0,
                    };
                    below = Add($"col.{c:00}.drum.{j}", ElementRole.LoadBearing, BuildMaterial.StuccoedStone, DetachMethod.Topple, b.Build(GreekMaterial.Paint, GreekMaterial.Stone), outward,
                        j == 0 ? "bed joint on the stylobate, dowelled" : "bed joint with a wooden empolion", shape, below);
                    graph[below].Column = c;
                    graph[below].Primary = j == weak;
                }
                var capital = Builder();
                DoricCapital(capital, floor + axis, SchoolColumnHeight, SchoolColumnRadius);
                capitals[c] = Add($"col.{c:00}.cap", ElementRole.LoadBearing, BuildMaterial.StuccoedStone, DetachMethod.Topple, capital.Build(GreekMaterial.Paint, GreekMaterial.Stone), outward,
                    "bed joint on the top drum", new ElementShape
                    {
                        Centre = floor + axis + Vector3.up * (shaft + SchoolColumnHeight * 0.06f),
                        Size = new Vector3(2.76f * SchoolColumnRadius, SchoolColumnHeight * 0.12f, 2.76f * SchoolColumnRadius),
                        Radius = 1.38f * SchoolColumnRadius,
                        Base = floor + axis + Vector3.up * shaft,
                    }, below);
                graph[capitals[c]].Column = c;
            }

            // The entablature over each intercolumniation: the architrave on the two capitals, the frieze and cornice on it.
            var top = steps + SchoolColumnHeight;
            var ex = SchoolColumnX + 0.24f;
            var ez = SchoolColumnZ + 0.24f;
            var friezeAt = top + SchoolEpistyle;
            var triglyphs = SchoolTriglyphs(ex, ez);
            var spans = columns.Count;
            var friezes = new int[spans];
            var spanFrom = new float[spans];
            var spanTo = new float[spans];
            var spanSide = new Vector3[spans];
            for (var s = 0; s < spans; s++)
            {
                var a = columns[s];
                var c = columns[(s + 1) % spans];
                var alongZ = Mathf.Abs(a.x - c.x) < 0.01f;
                var side = alongZ ? new Vector3(Mathf.Sign(a.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(a.z));
                spanSide[s] = side;
                spanFrom[s] = alongZ ? Mathf.Min(a.z, c.z) : Mathf.Min(a.x, c.x);
                spanTo[s] = alongZ ? Mathf.Max(a.z, c.z) : Mathf.Max(a.x, c.x);
                var mine = new List<(Vector3 at, bool alongZ)>();
                foreach (var glyph in triglyphs)
                    if (NearestSpan(columns, glyph.at) == s) mine.Add(glyph);

                var architrave = Builder();
                RingSpan(architrave, GreekMaterial.Paint, ex, ez, 0.48f, top, SchoolEpistyle, a, c);
                RingSpan(architrave, GreekMaterial.Ochre, ex + 0.03f, ez + 0.03f, 0.06f, top + SchoolEpistyle - 0.06f, 0.06f, a, c);
                foreach (var (at, z) in mine)
                {
                    var out1 = z ? new Vector3(Mathf.Sign(at.x) * 0.02f, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(at.z) * 0.02f);
                    var half = z ? new Vector3(0.02f, 0.025f, 0.13f) : new Vector3(0.13f, 0.025f, 0.02f);
                    architrave.Box(GreekMaterial.Azure, at + out1 + Vector3.up * (top + SchoolEpistyle - 0.085f), half, Quaternion.identity);
                }
                var beam = Add($"span.{s:00}.architrave", ElementRole.Beam, BuildMaterial.StuccoedStone, DetachMethod.Drop, architrave.Build(GreekMaterial.Paint, GreekMaterial.Ochre, GreekMaterial.Azure), side,
                    "rests on two capitals, cramped to its neighbours", new ElementShape { Radius = 0.24f }, capitals[s], capitals[(s + 1) % spans]);

                var frieze = Builder();
                RingSpan(frieze, GreekMaterial.Paint, ex - 0.05f, ez - 0.05f, 0.4f, friezeAt, SchoolFrieze, a, c);
                foreach (var (at, z) in mine)
                {
                    var inward = z ? new Vector3(-Mathf.Sign(at.x) * 0.04f, 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(at.z) * 0.04f);
                    var half = z ? new Vector3(0.04f, SchoolFrieze * 0.5f, 0.13f) : new Vector3(0.13f, SchoolFrieze * 0.5f, 0.04f);
                    frieze.Box(GreekMaterial.Azure, at + inward + Vector3.up * (friezeAt + SchoolFrieze * 0.5f), half, Quaternion.identity);
                }
                RingSpan(frieze, GreekMaterial.Azure, ex + 0.15f, ez + 0.15f, 0.15f, friezeAt + SchoolFrieze - 0.03f, 0.03f, a, c);
                RingSpan(frieze, GreekMaterial.Paint, ex + 0.15f, ez + 0.15f, 0.4f, friezeAt + SchoolFrieze, SchoolCornice, a, c);
                friezes[s] = Add($"span.{s:00}.frieze", ElementRole.Beam, BuildMaterial.StuccoedStone, DetachMethod.Drop, frieze.Build(GreekMaterial.Paint, GreekMaterial.Azure), side,
                    "frieze and cornice blocks on the architrave", new ElementShape { Radius = 0.27f }, beam);
            }

            // The hall's walls of sun-dried brick on the socle, in stretches.
            var wallTop = SchoolColumnHeight + SchoolEpistyle + SchoolFrieze;
            var front = HallOffset + HallWidth * 0.5f - HallWall * 0.5f;
            var back = HallOffset - HallWidth * 0.5f + HallWall * 0.5f;
            var sideZ = HallDepth * 0.5f - HallWall * 0.5f;
            var walls = new List<(string id, Vector3 centre, bool alongZ, float from, float to, float y0, Vector3 outward, bool stump)>
            {
                ("wall.front.left", default, true, -HallDepth * 0.5f, -HallDoor, course, Vector3.right, true),
                ("wall.front.right", default, true, HallDoor, HallDepth * 0.5f, course, Vector3.right, true),
                ("wall.front.lintel", default, true, -HallDoor, HallDoor, 4f * course, Vector3.right, false),
                ("wall.back.0", default, true, -HallDepth * 0.5f, -HallDepth / 6f, course, Vector3.left, true),
                ("wall.back.1", default, true, -HallDepth / 6f, HallDepth / 6f, course, Vector3.left, true),
                ("wall.back.2", default, true, HallDepth / 6f, HallDepth * 0.5f, course, Vector3.left, true),
                ("wall.side.minus", default, false, HallOffset - (HallWidth * 0.5f - HallWall), HallOffset + (HallWidth * 0.5f - HallWall), course, Vector3.back, true),
                ("wall.side.plus", default, false, HallOffset - (HallWidth * 0.5f - HallWall), HallOffset + (HallWidth * 0.5f - HallWall), course, Vector3.forward, true),
            };
            var wallIndex = new Dictionary<string, int>();
            foreach (var (id, _, alongZ, from, to, y0, outward, stump) in walls)
            {
                var line = alongZ ? (outward.x > 0f ? front : back) : (outward.z > 0f ? sideZ : -sideZ);
                var length = to - from;
                var height = wallTop - y0;
                var middle = (from + to) * 0.5f;
                var centre = alongZ ? new Vector3(line, steps + y0 + height * 0.5f, middle) : new Vector3(middle, steps + y0 + height * 0.5f, line);
                var half = alongZ ? new Vector3(HallWall * 0.5f, height * 0.5f, length * 0.5f) : new Vector3(length * 0.5f, height * 0.5f, HallWall * 0.5f);
                var b = Builder();
                b.Box(GreekMaterial.Paint, centre, half, Quaternion.identity);
                var hash = DestructionHash.Mix((uint)seed, (int)DestructionHash.Of(id));
                var foot = new Vector3(centre.x, steps + y0, centre.z);
                var shape = new ElementShape { Centre = centre, Size = half * 2f, Base = foot };
                if (stump)
                {
                    var s = Builder();
                    var level = DestructionHash.Range(hash, 1, 0.2f, 0.75f);
                    JaggedWall(s, foot, alongZ, length, HallWall, level, hash);
                    shape.Stump = s.Build(GreekMaterial.Paint, GreekMaterial.Mudbrick);
                }
                var heap = Builder();
                var fallenShare = Mathf.Clamp01(1f - (stump ? 0.8f : 0f) / height);
                foreach (var sideSign in new[] { -1f, 1f })
                {
                    var across = alongZ ? new Vector3(sideSign * (HallWall * 0.5f + 0.3f), 0f, 0f) : new Vector3(0f, 0f, sideSign * (HallWall * 0.5f + 0.3f));
                    var mound = new Vector3(foot.x, steps, foot.z) + across;
                    var radii = alongZ ? new Vector2(0.45f, length * 0.45f) : new Vector2(length * 0.45f, 0.45f);
                    Heap(heap, GreekMaterial.Mudbrick, mound, radii.x, radii.y, 0.18f + 0.3f * fallenShare * (sideSign > 0f ? 1f : 0.7f), hash + (uint)(sideSign > 0f ? 1 : 2));
                }
                shape.Heap = heap.Build(GreekMaterial.Mudbrick);
                var supports = id == "wall.front.lintel" ? new[] { wallIndex["wall.front.left"], wallIndex["wall.front.right"] } : new[] { socleIndex };
                var index = Add(id, ElementRole.Wall, BuildMaterial.Mudbrick, DetachMethod.Crumble, b.Build(GreekMaterial.Paint), outward,
                    id == "wall.front.lintel" ? "brick over a timber lintel across the door" : "brick courses in mud mortar on the stone socle", shape, supports);
                graph[index].Primary = true;
                wallIndex[id] = index;
            }
            var door = Builder();
            door.Box(GreekMaterial.Pitch, hall + new Vector3(HallWidth * 0.5f - 0.25f, (HallDoorTop + 0.6f) * 0.5f, 0f), new Vector3(0.03f, (HallDoorTop + 0.6f) * 0.5f, HallDoor), Quaternion.identity);
            Add("hall.door", ElementRole.Decor, BuildMaterial.Timber, DetachMethod.Drop, door.Build(GreekMaterial.Pitch), Vector3.right, "hung in the door frame",
                new ElementShape { Radius = 0.04f }, wallIndex["wall.front.left"], wallIndex["wall.front.right"]);

            // The roof in bays, a side at a time, on the frieze under its eave and the hall's wall under its slope.
            var roofW = 2f * (ex + 0.15f);
            var roofD = 2f * (ez + 0.15f);
            var eaves = friezeAt + SchoolFrieze + SchoolCornice;
            var slope = SchoolPitch * Mathf.Deg2Rad;
            var rise = roofW * 0.5f * Mathf.Tan(slope);
            var run = roofW * 0.5f + SchoolOverhang;
            var thick = 0.1f / Mathf.Cos(slope);
            float TopAt(float x) => eaves + rise - Mathf.Abs(x) * Mathf.Tan(slope) + 0.12f + thick;
            float UnderAt(float x) => eaves + rise - Mathf.Abs(x) * Mathf.Tan(slope) + 0.12f - thick;
            const int bays = 4;
            var reach = roofD * 0.5f + SchoolOverhang;
            foreach (var side in new[] { 1f, -1f })
            {
                for (var k = 0; k < bays; k++)
                {
                    var z0 = -reach + k * 2f * reach / bays;
                    var z1 = -reach + (k + 1) * 2f * reach / bays;
                    var b = Builder();
                    RoofBay(b, roofW, roofD, eaves, SchoolOverhang, SchoolPitch, side, z0, z1, side > 0f, k == bays - 1);
                    var zc = (z0 + z1) * 0.5f;
                    var wallX = side > 0f ? HallOffset + HallWidth * 0.5f : HallOffset - HallWidth * 0.5f;
                    var shape = new ElementShape
                    {
                        EaveEnd = new Vector3(side * run, TopAt(run), zc),
                        RidgeEnd = new Vector3(0f, TopAt(0f), zc),
                        EaveHinge = new Vector3(side * (ex + 0.15f), UnderAt(ex + 0.15f), zc),
                        InnerHinge = new Vector3(wallX, UnderAt(wallX), zc),
                    };
                    // What is left of the bay: broken pan and cover tiles in a heap with rafters across it, in the hall when it
                    // dropped in, outside below its eave when it slid off.
                    var hash = DestructionHash.Mix((uint)seed, 900 + k + (side > 0f ? 0 : 10));
                    var inside = Builder();
                    var hallX = side > 0f ? HallOffset + HallWidth * 0.25f : HallOffset - HallWidth * 0.25f;
                    TileHeap(inside, new Vector3(hallX, steps, zc), 0.5f, (z1 - z0) * 0.42f, 0.42f, new Vector3(wallX, steps + 1.6f, zc), hash);
                    shape.Heap = inside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    var outside = Builder();
                    var outX = side * (ex + 0.75f);
                    var landing = SchoolRestHeight(new Vector3(outX, 0f, zc));
                    TileHeap(outside, new Vector3(outX, landing, zc), 0.7f, (z1 - z0) * 0.45f, 0.36f, new Vector3(side * (SchoolWidth * 0.5f - 0.3f), steps + 0.3f, zc), hash + 1u);
                    shape.HeapOut = outside.Build(GreekMaterial.Terracotta, GreekMaterial.Wood);
                    owned.Add(shape.HeapOut.Mesh);
                    var supports = new List<int>();
                    for (var s = 0; s < spans; s++)
                        if (spanSide[s].x == side && Overlap(spanFrom[s], spanTo[s], z0, z1, ez) > 0.1f) supports.Add(friezes[s]);
                    foreach (var (id, _, alongZ, from, to, _, outward, _) in walls)
                        if (alongZ && outward.x == side && id != "wall.front.lintel" && Overlap(from, to, z0, z1, HallDepth * 0.5f) > 0.1f) supports.Add(wallIndex[id]);
                    if (side > 0f && Overlap(-HallDoor, HallDoor, z0, z1, HallDepth * 0.5f) > 0.1f) supports.Add(wallIndex["wall.front.lintel"]);
                    var index = Add($"roof.{(side > 0f ? "front" : "back")}.{k}", ElementRole.Roof, BuildMaterial.Ceramic, DetachMethod.CaveIn,
                        b.Build(GreekMaterial.Terracotta, GreekMaterial.Wood), new Vector3(side, 0f, 0f), "pan and cover tiles on rafters over the frieze and the hall's wall", shape, supports.ToArray());
                    graph[index].Primary = true;
                }
            }

            // The pediments on the short sides' frieze.
            foreach (var end in new[] { 1f, -1f })
            {
                var b = Builder();
                Pediment(b, roofW, roofD, eaves, SchoolOverhang, SchoolPitch, end);
                var supports = new List<int>();
                for (var s = 0; s < spans; s++) if (spanSide[s].z == end) supports.Add(friezes[s]);
                Add($"pediment.{(end > 0f ? "plus" : "minus")}", ElementRole.Decor, BuildMaterial.StuccoedStone, DetachMethod.Drop, b.Build(GreekMaterial.Azure, GreekMaterial.Paint, GreekMaterial.Ochre),
                    new Vector3(0f, 0f, end), "tympanum slabs on the cornice, the raking sima pegged on", new ElementShape { Radius = 0.1f }, supports.ToArray());
            }
            return (graph, shapes.ToArray());
        }

        /// <summary>How far two stretches along a line overlap; the ends at the corners reach out to the given limit.</summary>
        static float Overlap(float a0, float a1, float b0, float b1, float limit)
        {
            if (Mathf.Abs(a0) >= SchoolColumnZ - 0.01f && a0 < 0f) a0 = -limit;
            if (Mathf.Abs(a1) >= SchoolColumnZ - 0.01f && a1 > 0f) a1 = limit;
            return Mathf.Min(a1, b1) - Mathf.Max(a0, b0);
        }

        /// <summary>The triglyphs round the School's frieze, as AddSchool lays them: on the long sides, then on the short.</summary>
        static List<(Vector3 at, bool alongZ)> SchoolTriglyphs(float ex, float ez)
        {
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
            return triglyphs;
        }

        /// <summary>The intercolumniation a point of the frieze lies over: the span whose two column axes it is nearest.</summary>
        static int NearestSpan(List<Vector3> columns, Vector3 at)
        {
            var best = 0;
            var near = float.MaxValue;
            var p = new Vector2(at.x, at.z);
            for (var s = 0; s < columns.Count; s++)
            {
                var a = new Vector2(columns[s].x, columns[s].z);
                var c = new Vector2(columns[(s + 1) % columns.Count].x, columns[(s + 1) % columns.Count].z);
                var ab = c - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                var d = Vector2.Distance(p, a + ab * t);
                if (d < near - 1e-4f) { near = d; best = s; }
            }
            return best;
        }

        /// <summary>
        /// The piece of one of Ring's beams (outside ex by ez, t deep, from y0 up height) over the intercolumniation between
        /// two column axes; at a corner column it reaches to the corner, as Ring's beams do.
        /// </summary>
        static void RingSpan(GreekShips.MeshBuilder b, GreekMaterial paint, float ex, float ez, float t, float y0, float height, Vector3 a, Vector3 c)
        {
            var y = y0 + height * 0.5f;
            if (Mathf.Abs(a.x - c.x) < 0.01f)
            {
                // A long side, along z: from corner to corner its beam runs the full ez.
                var lo = Mathf.Min(a.z, c.z);
                var hi = Mathf.Max(a.z, c.z);
                if (lo <= -SchoolColumnZ + 0.01f) lo = -ez;
                if (hi >= SchoolColumnZ - 0.01f) hi = ez;
                b.Box(paint, new Vector3(Mathf.Sign(a.x) * (ex - t * 0.5f), y, (lo + hi) * 0.5f), new Vector3(t * 0.5f, height * 0.5f, (hi - lo) * 0.5f), Quaternion.identity);
            }
            else
            {
                // A short side, along x, between the long sides' beams.
                var lo = Mathf.Max(Mathf.Min(a.x, c.x), -(ex - t));
                var hi = Mathf.Min(Mathf.Max(a.x, c.x), ex - t);
                if (hi - lo < 1e-3f) return;
                b.Box(paint, new Vector3((lo + hi) * 0.5f, y, Mathf.Sign(a.z) * (ez - t * 0.5f)), new Vector3((hi - lo) * 0.5f, height * 0.5f, t * 0.5f), Quaternion.identity);
            }
        }

        /// <summary>
        /// One drum of a Doric shaft, from t0 to t1 of the shaft's height: the same flutes, taper and entasis as DoricColumn,
        /// and its two beds of bare limestone, hidden while it stands under the next and bare once they come apart.
        /// </summary>
        static void DoricDrum(GreekShips.MeshBuilder b, Vector3 foot, float height, float radius, int flutes, float t0, float t1)
        {
            var shaft = height * 0.88f;
            var ring = flutes * 2;
            var heights = new[] { t0, t1 };
            var ids = new int[2, ring + 1];
            for (var r = 0; r < 2; r++)
            {
                var t = heights[r];
                var taper = Mathf.Lerp(1f, 0.8f, t) + 0.025f * Mathf.Sin(t * Mathf.PI);
                for (var k = 0; k <= ring; k++)
                {
                    var angle = k * Mathf.PI / flutes;
                    var reach = radius * taper * (k % 2 == 0 ? 1f : 0.92f);
                    ids[r, k] = b.Vertex(foot + new Vector3(Mathf.Cos(angle) * reach, t * shaft, Mathf.Sin(angle) * reach), new Vector2(angle * radius, t * shaft));
                }
                b.Seam(ids[r, 0], ids[r, ring]);
            }
            for (var k = 0; k < ring; k++)
            {
                var angle = (k + 0.5f) * Mathf.PI / flutes;
                b.Quad(GreekMaterial.Paint, ids[0, k], ids[0, k + 1], ids[1, k + 1], ids[1, k], new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
            for (var r = 0; r < 2; r++)
            {
                var t = heights[r];
                var taper = Mathf.Lerp(1f, 0.8f, t) + 0.025f * Mathf.Sin(t * Mathf.PI);
                var centre = foot + Vector3.up * (t * shaft);
                var middle = b.Vertex(centre, new Vector2(centre.x, centre.z));
                var rim = new int[ring];
                for (var k = 0; k < ring; k++)
                {
                    var angle = k * Mathf.PI / flutes;
                    var reach = radius * taper * (k % 2 == 0 ? 1f : 0.92f);
                    var p = centre + new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                    rim[k] = b.Vertex(p, new Vector2(p.x, p.z));
                }
                for (var k = 0; k < ring; k++) b.Triangle(GreekMaterial.Stone, middle, rim[k], rim[(k + 1) % ring], r == 0 ? Vector3.down : Vector3.up);
            }
        }

        /// <summary>A Doric capital alone, as DoricColumn tops its shaft: the echinus, the abacus, and its bed underneath.</summary>
        static void DoricCapital(GreekShips.MeshBuilder b, Vector3 foot, float height, float radius)
        {
            var top = foot + Vector3.up * (height * 0.88f);
            b.Lathe(GreekMaterial.Paint, new[] { (0f, radius * 0.8f), (height * 0.035f, radius * 1.05f), (height * 0.07f, radius * 1.3f), (height * 0.08f, radius * 1.32f) }, 16, top);
            var abacus = height * 0.04f;
            b.Box(GreekMaterial.Paint, top + Vector3.up * (height * 0.08f + abacus * 0.5f), new Vector3(radius * 1.38f, abacus * 0.5f, radius * 1.38f), Quaternion.identity);
            var middle = b.Vertex(top, new Vector2(top.x, top.z));
            const int sides = 16;
            var rim = new int[sides];
            for (var k = 0; k < sides; k++)
            {
                var angle = k * Mathf.PI * 2f / sides;
                var p = top + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.8f;
                rim[k] = b.Vertex(p, new Vector2(p.x, p.z));
            }
            for (var k = 0; k < sides; k++) b.Triangle(GreekMaterial.Stone, middle, rim[k], rim[(k + 1) % sides], Vector3.down);
        }

        /// <summary>
        /// One bay of TiledGable's roof (the same measures) on one side, from z0 to z1: its stretch of pan tiles, the cover
        /// tiles and antefixes whose rows fall in it, the rafters' ends; the ridge over it on the front side.
        /// </summary>
        static void RoofBay(GreekShips.MeshBuilder b, float w, float d, float h, float overhang, float pitch, float side, float z0, float z1, bool ridge, bool last)
        {
            var slope = pitch * Mathf.Deg2Rad;
            var rise = w * 0.5f * Mathf.Tan(slope);
            var run = w * 0.5f + overhang;
            var length = run / Mathf.Cos(slope);
            var turn = Quaternion.Euler(0f, 0f, -side * pitch);
            var centre = new Vector3(side * run * 0.5f, h + rise - run * 0.5f * Mathf.Tan(slope) + 0.12f, 0f);
            b.Box(GreekMaterial.Terracotta, centre + new Vector3(0f, 0f, (z0 + z1) * 0.5f), new Vector3(length * 0.5f, 0.1f, (z1 - z0) * 0.5f), turn);
            var eave = new Vector3(side * run, h + rise - run * Mathf.Tan(slope) + 0.12f, 0f);
            var rows = Mathf.Max(3, Mathf.RoundToInt((d + 2f * overhang) / 0.45f));
            bool Within(float z) => z >= z0 - 1e-4f && (z < z1 - 1e-4f || last && z <= z1 + 1e-4f);
            for (var i = 0; i <= rows; i++)
            {
                var z = -d * 0.5f - overhang + i * (d + 2f * overhang) / rows;
                if (!Within(z)) continue;
                b.Box(GreekMaterial.Terracotta, centre + turn * Vector3.up * 0.13f + new Vector3(0f, 0f, z), new Vector3(length * 0.5f, 0.05f, 0.07f), turn);
                b.Box(GreekMaterial.Terracotta, eave + new Vector3(0f, 0.17f, z), new Vector3(0.02f, 0.12f, 0.09f), Quaternion.identity);
            }
            if (ridge) b.Box(GreekMaterial.Terracotta, new Vector3(0f, h + rise + 0.27f, (z0 + z1) * 0.5f), new Vector3(0.16f, 0.09f, (z1 - z0) * 0.5f), Quaternion.identity);
            for (var z = -d * 0.5f + 0.3f; z < d * 0.5f; z += 0.9f)
                if (Within(z)) b.Box(GreekMaterial.Wood, new Vector3(side * (w * 0.5f + overhang * 0.5f), h + 0.05f, z), new Vector3(overhang * 0.5f, 0.05f, 0.05f), Quaternion.Euler(0f, 0f, -side * pitch));
        }

        /// <summary>A pediment at one end: the blue tympanum as a slab 15 cm thick, the red raking sima and acroteria of RakingSima.</summary>
        static void Pediment(GreekShips.MeshBuilder b, float w, float d, float h, float overhang, float pitch, float end)
        {
            var slope = pitch * Mathf.Deg2Rad;
            var rise = w * 0.5f * Mathf.Tan(slope);
            foreach (var (z, facing) in new[] { (end * d * 0.5f, end), (end * (d * 0.5f - 0.15f), -end) })
            {
                var a = b.Vertex(new Vector3(-w * 0.5f, h, z), new Vector2(-w * 0.5f, h));
                var c = b.Vertex(new Vector3(w * 0.5f, h, z), new Vector2(w * 0.5f, h));
                var top = b.Vertex(new Vector3(0f, h + rise, z), new Vector2(0f, h + rise));
                // Painted blue on its face only; the back toward the roof space is plain plaster.
                b.Triangle(facing == end ? GreekMaterial.Azure : GreekMaterial.Paint, a, c, top, new Vector3(0f, 0f, facing));
            }
            // The slab's underside and sloping tops.
            var inner = end * (d * 0.5f - 0.15f);
            var outer = end * d * 0.5f;
            void Face(Vector3 p0, Vector3 p1, Vector3 outward)
            {
                var i0 = b.Vertex(new Vector3(p0.x, p0.y, inner), new Vector2(p0.x, inner));
                var i1 = b.Vertex(new Vector3(p1.x, p1.y, inner), new Vector2(p1.x, inner));
                var o1 = b.Vertex(new Vector3(p1.x, p1.y, outer), new Vector2(p1.x, outer));
                var o0 = b.Vertex(new Vector3(p0.x, p0.y, outer), new Vector2(p0.x, outer));
                b.Quad(GreekMaterial.Paint, i0, i1, o1, o0, outward);
            }
            Face(new Vector3(-w * 0.5f, h, 0f), new Vector3(w * 0.5f, h, 0f), Vector3.down);
            Face(new Vector3(-w * 0.5f, h, 0f), new Vector3(0f, h + rise, 0f), new Vector3(-Mathf.Sin(slope), Mathf.Cos(slope), 0f));
            Face(new Vector3(0f, h + rise, 0f), new Vector3(w * 0.5f, h, 0f), new Vector3(Mathf.Sin(slope), Mathf.Cos(slope), 0f));

            var run = w * 0.5f + overhang;
            var zs = end * (d * 0.5f + overhang - 0.04f);
            foreach (var side in new[] { -1f, 1f })
            {
                var turn = Quaternion.Euler(0f, 0f, -side * pitch);
                var centre = new Vector3(side * run * 0.5f, h + rise - run * 0.5f * Mathf.Tan(slope) + 0.12f, zs);
                b.Box(GreekMaterial.Ochre, centre + turn * Vector3.up * 0.06f, new Vector3(run / Mathf.Cos(slope) * 0.5f, 0.16f, 0.04f), turn);
                b.Box(GreekMaterial.Ochre, new Vector3(side * run, h + rise - run * Mathf.Tan(slope) + 0.45f, zs), new Vector3(0.14f, 0.2f, 0.04f), Quaternion.identity);
            }
            b.Box(GreekMaterial.Ochre, new Vector3(0f, h + rise + 0.55f, zs), new Vector3(0.22f, 0.3f, 0.04f), Quaternion.identity);
        }

        /// <summary>
        /// What is left of a stretch of brick wall that slumped: plaster still on its faces up to a ragged top of bare
        /// brick, about the given height over its foot, the line of its top fixed by the hash.
        /// </summary>
        static void JaggedWall(GreekShips.MeshBuilder b, Vector3 foot, bool alongZ, float length, float thickness, float level, uint hash,
                               GreekMaterial faces = GreekMaterial.Paint, GreekMaterial top = GreekMaterial.Mudbrick)
        {
            var segments = Mathf.Max(4, Mathf.RoundToInt(length / 0.22f));
            var along = alongZ ? Vector3.forward : Vector3.right;
            var across = alongZ ? Vector3.right : Vector3.forward;
            var heights = new float[segments + 1];
            // Washed and broken unevenly: a slow swell along the stretch and small steps where bricks came out.
            var phase = DestructionHash.Range(hash, 39, 0f, Mathf.PI * 2f);
            for (var k = 0; k <= segments; k++)
            {
                var t = k / (float)segments;
                var swell = 0.65f + 0.35f * Mathf.Sin(phase + t * Mathf.PI * 1.6f);
                heights[k] = Mathf.Max(0.08f, level * swell + DestructionHash.Range(hash, 40 + k, -0.09f, 0.09f));
            }
            Vector3 At(int k, float side, float up) => foot + along * (-length * 0.5f + k * length / segments) + across * side * thickness * 0.5f + Vector3.up * up;
            for (var k = 0; k < segments; k++)
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    var a = b.Vertex(At(k, side, 0f), new Vector2(k * length / segments, 0f));
                    var c = b.Vertex(At(k + 1, side, 0f), new Vector2((k + 1) * length / segments, 0f));
                    var d = b.Vertex(At(k + 1, side, heights[k + 1]), new Vector2((k + 1) * length / segments, heights[k + 1]));
                    var e = b.Vertex(At(k, side, heights[k]), new Vector2(k * length / segments, heights[k]));
                    b.Quad(faces, a, c, d, e, across * side);
                }
                // The broken top: bare brick.
                var t0 = b.Vertex(At(k, -1f, heights[k]), new Vector2(k * 0.3f, 0f));
                var t1 = b.Vertex(At(k + 1, -1f, heights[k + 1]), new Vector2((k + 1) * 0.3f, 0f));
                var t2 = b.Vertex(At(k + 1, 1f, heights[k + 1]), new Vector2((k + 1) * 0.3f, thickness));
                var t3 = b.Vertex(At(k, 1f, heights[k]), new Vector2(k * 0.3f, thickness));
                b.Quad(top, t0, t1, t2, t3, Vector3.up);
            }
            foreach (var (k, facing) in new[] { (0, -1f), (segments, 1f) })
            {
                var a = b.Vertex(At(k, -1f, 0f), Vector2.zero);
                var c = b.Vertex(At(k, 1f, 0f), new Vector2(thickness, 0f));
                var d = b.Vertex(At(k, 1f, heights[k]), new Vector2(thickness, heights[k]));
                var e = b.Vertex(At(k, -1f, heights[k]), new Vector2(0f, heights[k]));
                b.Quad(top, a, c, d, e, along * facing);
            }
        }

        /// <summary>
        /// A fallen bay's tiles: a low heap of broken terracotta, loose pan tiles on and about it, and three rafters, broken,
        /// lying across it with their upper ends toward the given point (a wall top, the stylobate's edge).
        /// </summary>
        static void TileHeap(GreekShips.MeshBuilder b, Vector3 centre, float rx, float rz, float height, Vector3 toward, uint hash)
        {
            Heap(b, GreekMaterial.Terracotta, centre, rx, rz, height, hash);
            for (var k = 0; k < 9; k++)
            {
                var p = centre + new Vector3(DestructionHash.Range(hash, 200 + k, -1.4f, 1.4f) * rx, 0f, DestructionHash.Range(hash, 210 + k, -1.1f, 1.1f) * rz);
                p.y = centre.y + DestructionHash.Range(hash, 220 + k, 0.03f, 0.6f) * height;
                b.Box(GreekMaterial.Terracotta, p, new Vector3(0.2f, 0.02f, 0.14f),
                    Quaternion.Euler(DestructionHash.Range(hash, 230 + k, -35f, 35f), DestructionHash.Range(hash, 240 + k, 0f, 360f), DestructionHash.Range(hash, 250 + k, -35f, 35f)));
            }
            for (var k = 0; k < 3; k++)
            {
                var from = centre + new Vector3(DestructionHash.Range(hash, 260 + k, -0.4f, 0.4f) * rx, height * 0.6f, (k - 1) * rz * 0.6f);
                var to = new Vector3(Mathf.Lerp(from.x, toward.x, 0.8f), Mathf.Lerp(from.y, toward.y, DestructionHash.Range(hash, 270 + k, 0.08f, 0.3f)), from.z + DestructionHash.Range(hash, 280 + k, -0.4f, 0.4f));
                b.Tube(GreekMaterial.Wood, new List<Vector3> { from, to }, 0.055f, 0.05f, 5);
            }
        }

        /// <summary>A low heap (of brick earth, of rubble): rings narrowing up to a rounded top, pushed in and out by the hash.</summary>
        static void Heap(GreekShips.MeshBuilder b, GreekMaterial paint, Vector3 centre, float rx, float rz, float height, uint hash)
        {
            const int sides = 10;
            var levels = new[] { (0f, 1f), (0.5f, 0.72f), (0.85f, 0.35f) };
            var rings = new int[levels.Length, sides + 1];
            for (var i = 0; i < levels.Length; i++)
            {
                var (up, reach) = levels[i];
                for (var k = 0; k <= sides; k++)
                {
                    var angle = k * Mathf.PI * 2f / sides;
                    var bump = k == sides ? DestructionHash.Range(hash, i * 20, 0.82f, 1.15f) : DestructionHash.Range(hash, i * 20 + k, 0.82f, 1.15f);
                    var p = centre + new Vector3(Mathf.Cos(angle) * rx * reach * bump, up * height * (i == 0 ? 1f : bump) - (i == 0 ? 0.05f : 0f), Mathf.Sin(angle) * rz * reach * bump);
                    rings[i, k] = b.Vertex(p, new Vector2(p.x, p.z));
                }
            }
            for (var i = 0; i + 1 < levels.Length; i++)
                for (var k = 0; k < sides; k++)
                {
                    var angle = (k + 0.5f) * Mathf.PI * 2f / sides;
                    b.Quad(paint, rings[i, k], rings[i, k + 1], rings[i + 1, k + 1], rings[i + 1, k], new Vector3(Mathf.Cos(angle), 0.6f, Mathf.Sin(angle)));
                }
            var peak = b.Vertex(centre + Vector3.up * height, new Vector2(centre.x, centre.z));
            var last = levels.Length - 1;
            for (var k = 0; k < sides; k++) b.Triangle(paint, rings[last, k], rings[last, k + 1], peak, Vector3.up);
        }
    }
}
