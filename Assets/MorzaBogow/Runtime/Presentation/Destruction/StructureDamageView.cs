using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>The shapes of one element in its structure's frame, and the measures its fall is worked out from.</summary>
    public sealed class ElementShape
    {
        /// <summary>The element as built.</summary>
        public GreekMesh Whole;
        /// <summary>What stays standing once it has failed (a wall's ragged stump), or null.</summary>
        public GreekMesh Stump;
        /// <summary>What it leaves where it stood or fell (a mound of brick earth, a heap of broken tiles), or null.</summary>
        public GreekMesh Heap;
        /// <summary>A bay of roof's heap when it slid off outward (its colonnade gone), else its heap is Heap, in the hall.</summary>
        public GreekMesh HeapOut;
        /// <summary>Its middle.</summary>
        public Vector3 Centre;
        /// <summary>Its size standing (a block: along x, up, along z in the frame).</summary>
        public Vector3 Size;
        /// <summary>A drum's or capital's radius, a block's half thickness when it lies on its face.</summary>
        public float Radius;
        /// <summary>A drum's foot on its column's axis; a wall's foot in its middle.</summary>
        public Vector3 Base;
        /// <summary>A bay of roof: points on the two lines it may swing about (both along z) and its two edges' middles.</summary>
        public Vector3 EaveHinge, InnerHinge, RidgeEnd, EaveEnd;
    }

    /// <summary>What a structure's view needs from the buildings it stands among.</summary>
    internal sealed class StructureHost
    {
        /// <summary>The height a fallen piece comes to rest at, at a point of the structure's frame.</summary>
        public Func<Vector3, float> RestHeight = _ => 0f;
        public Func<GreekMaterial[], Material[]> Materials = _ => null;
        /// <summary>Dust and what flies off the material, at a point of the frame, as strong as given.</summary>
        public Action<Vector3, BuildMaterial, float, uint> Burst;
        public List<Mesh> Owned;
    }

    /// <summary>Frees a structure's falling pieces from the budget when its view goes with its settlement.</summary>
    internal sealed class StructureDamageOwner : MonoBehaviour
    {
        public StructureDamageView View;

        void OnDestroy() => View?.Forget();
    }

    /// <summary>
    /// A structure shown at the damage the game's hull gives (DamagePlan), its elements failing where the shots struck:
    /// <list type="bullet">
    /// <item>scars on the faces nearest the shots (whitewash and plaster knocked off, broken tiles);</item>
    /// <item>elements failing in their own right nearest the shots, and with them whatever rested on them (StructureGraph);</item>
    /// <item>each fall worked out from the element and its supports: a column tipping over at its weak joint, its drums lying in a row;
    /// blocks tipping off their capitals; roof bays swinging down about what still holds them; brick walls slumping into mounds;</item>
    /// <item>in game time, deterministic, with no physics engine: nothing here can push a ship, block a way or harm anyone;</item>
    /// <item>a few falling pieces at a time within a budget, the rest coming to rest at once; what has come to rest is one mesh.</item>
    /// </list>
    /// The hull rising again (walls raised) puts back the last elements to fail first. The failures and scars are kept in
    /// the object's DestructionRecord, so a loaded game shows the same ones.
    /// </summary>
    public sealed class StructureDamageView
    {
        /// <summary>Most pieces in the air at once for one structure and for all of them; the rest come to rest at once.</summary>
        public static int MaxBodiesPerStructure = 56, MaxBodies = 112;

        /// <summary>Pieces at rest kept apart at most before they are merged into the rubble's one mesh.</summary>
        public static int MaxChunks = 12;
        static int bodiesInAir;
        public static int BodiesInAir => bodiesInAir;

        /// <summary>How long a structure's fall into ruin takes at most, in game seconds (PortBuildings.CollapseSeconds).</summary>
        public const float RuinSeconds = 2.6f;

        sealed class Body
        {
            public GameObject Object;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public readonly List<int> Elements = new List<int>();
            public DetachMethod Kind;
            public float Delay, Time, Duration, Split, Angle, ScaleTo;
            public Vector3 Pivot, Axis;
            public Quaternion RestRotation;
            public Vector3 RestPosition;
            public Mesh Mesh;
            public bool Started, Flying, Midway;
        }

        readonly StructureGraph graph;
        readonly ElementShape[] shapes;
        readonly StructureHost host;
        readonly string prefix;
        readonly uint seed;
        readonly Transform frame;
        DestructionRecord record;

        readonly bool[] failed;
        readonly bool[] settled;
        readonly int[] depth;
        readonly float[] fallDelay;
        readonly Vector3[] fall;
        readonly List<Body> bodies = new List<Body>();
        readonly List<Body> chunks = new List<Body>();
        readonly List<Body> pool = new List<Body>();
        int created;

        GameObject standing, scarObject, rubble;
        Mesh standingMesh, scarMesh, rubbleMesh;
        float damage = -1f;
        bool fallen, applied;

        public Transform Frame => frame;
        public StructureGraph Graph => graph;
        public bool Busy => bodies.Count > 0;
        /// <summary>Some element has failed: the structure is drawn in its elements, not as one mesh.</summary>
        public bool Broken { get; private set; }
        public int Scars { get; private set; }
        public int FailedCount { get; private set; }
        public int SettledPieces { get; private set; }
        public int PiecesMade => created;
        public int PiecesFalling => bodies.Count;
        public DestructionStage Stage { get; private set; }

        /// <param name="parent">The frame the record's points are in (a settlement's buildings); the view's own frame is a child of it.</param>
        internal StructureDamageView(Transform parent, string name, StructureGraph graph, ElementShape[] shapes, StructureHost host, string prefix, uint seed)
        {
            this.graph = graph;
            this.shapes = shapes;
            this.host = host;
            this.prefix = prefix;
            this.seed = seed;
            frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.gameObject.AddComponent<StructureDamageOwner>().View = this;
            failed = new bool[graph.Count];
            settled = new bool[graph.Count];
            depth = new int[graph.Count];
            fallDelay = new float[graph.Count];
            fall = new Vector3[graph.Count];
            standing = Part("Standing");
            scarObject = Part("Scars");
            rubble = Part("Rubble");
            frame.gameObject.SetActive(false);
        }

        GameObject Part(string name)
        {
            var item = new GameObject(name);
            item.transform.SetParent(frame, false);
            item.AddComponent<MeshFilter>();
            item.AddComponent<MeshRenderer>();
            return item;
        }

        public DestructionRecord Record
        {
            get => record;
            set { record = value; applied = false; }
        }

        public int IndexOf(string id) => graph.IndexOf(id.StartsWith(prefix) ? id.Substring(prefix.Length) : id);

        public bool IsFailed(int element) => failed[element];

        public bool IsFailed(string id)
        {
            var i = graph.IndexOf(id);
            return i >= 0 && failed[i];
        }

        /// <summary>The pose a failed element comes to rest in: x goes to rotation * x + position, in the frame.</summary>
        public (Quaternion rotation, Vector3 position) RestOf(int element) => Rest(element);

        // ---------- Following the hull ----------

        /// <summary>
        /// Shows the structure at the damage given (DamagePlan.Damage of the game's hull), fallen or not. Animate: the
        /// failures new since the last call are seen to fall; otherwise they lie where they would have come to rest.
        /// </summary>
        public void Apply(float damageNow, bool fallenNow, bool animate)
        {
            if (record == null) record = new DestructionRecord();
            if (applied && Mathf.Approximately(damageNow, damage) && fallenNow == fallen) return;
            var first = !applied;
            applied = true;
            damage = damageNow;
            fallen = fallenNow;

            var candidates = new List<int>();
            foreach (var element in graph.Elements) if (element.Primary) candidates.Add(element.Index);
            var wanted = DamagePlan.Failures(damage, fallen, candidates.Count);

            // The record's own failures, in order; ones it does not know (another layout) are dropped.
            var mine = new List<int>();
            for (var i = record.Failures.Count - 1; i >= 0; i--)
            {
                var entry = record.Failures[i];
                if (!entry.Element.StartsWith(prefix)) continue;
                var index = graph.IndexOf(entry.Element.Substring(prefix.Length));
                if (index < 0 || !graph[index].Primary) record.Failures.RemoveAt(i);
            }
            foreach (var entry in record.Failures)
                if (entry.Element.StartsWith(prefix)) mine.Add(graph.IndexOf(entry.Element.Substring(prefix.Length)));

            // A hull raised again puts back the last to fail first.
            while (mine.Count > wanted)
            {
                var last = mine[mine.Count - 1];
                mine.RemoveAt(mine.Count - 1);
                record.Failures.RemoveAll(entry => entry.Element == prefix + graph[last].Id);
            }

            var now = new bool[graph.Count];
            foreach (var index in mine) now[index] = true;
            graph.Cascade(now);
            // Shown for the first time (built at the first damage, or after a load): what the record says fell is at rest;
            // only failures chosen now may be seen to fall.
            var was = first ? (bool[])now.Clone() : (bool[])failed.Clone();
            // More damage: the elements nearest the shots go next.
            var batch = new List<int>();
            while (mine.Count < wanted)
            {
                var next = NextFailure(now);
                if (next < 0) break;
                var direction = FallDirection(next);
                record.Failures.Add(new DestructionFailure { Element = prefix + graph[next].Id, Event = LatestEvent(), FallX = direction.x, FallZ = direction.z });
                mine.Add(next);
                batch.Add(next);
                now[next] = true;
                graph.Cascade(now);
            }
            foreach (var entry in record.Failures)
            {
                if (!entry.Element.StartsWith(prefix)) continue;
                var index = graph.IndexOf(entry.Element.Substring(prefix.Length));
                if (index >= 0) fall[index] = new Vector3(entry.FallX, 0f, entry.FallZ);
            }
            Array.Copy(now, failed, now.Length);
            graph.Cascade(failed, depth);
            InheritFalls();

            // What failed only now falls before the eye, if the structure was in sight and stood before; the rest lies at rest.
            var restored = false;
            for (var i = 0; i < graph.Count; i++)
            {
                if (was[i] && !failed[i]) { restored = true; settled[i] = false; }
                if (first && was[i]) settled[i] = true;
            }
            if (restored) ReleaseAll();
            var fresh = new List<int>();
            for (var i = 0; i < graph.Count; i++) if (failed[i] && !was[i]) fresh.Add(i);
            if (!animate)
            {
                foreach (var i in fresh) settled[i] = true;
            }
            else Launch(fresh, batch);

            FailedCount = 0;
            for (var i = 0; i < graph.Count; i++) if (failed[i]) FailedCount++;
            Broken = FailedCount > 0;
            var scarred = UpdateScars();
            Stage = DamagePlan.StageOf(damage, fallen, bodies.Count == 0);
            // Most shots change the hull and nothing that shows: the meshes are rebuilt only when an element or a scar
            // comes or goes.
            if (first || restored || fresh.Count > 0 || scarred) Rebuild();
        }

        int LatestEvent() => record.Events.Count > 0 ? record.Events[record.Events.Count - 1].Id : 0;

        /// <summary>The standing primary element nearest the shots that struck (by its middle), a fixed jitter breaking ties.</summary>
        int NextFailure(bool[] now)
        {
            var best = -1;
            var bestScore = float.MaxValue;
            foreach (var element in graph.Elements)
            {
                if (!element.Primary || now[element.Index]) continue;
                var centre = element.Box.center;
                var near = 60f;
                foreach (var impact in record.Impacts)
                {
                    var at = FramePoint(impact);
                    near = Mathf.Min(near, Vector2.Distance(new Vector2(centre.x, centre.z), new Vector2(at.x, at.z)));
                }
                var score = near + 1.5f * DestructionHash.Unit(Seed(element.Index), 1);
                if (score < bestScore) { bestScore = score; best = element.Index; }
            }
            return best;
        }

        uint Seed(int element) => DestructionHash.Mix(seed ^ DestructionHash.Of(graph[element].Id), 0);

        Vector3 FramePoint(DestructionImpact impact) =>
            frame.parent != null ? frame.InverseTransformPoint(frame.parent.TransformPoint(new Vector3(impact.X, impact.Y, impact.Z))) : new Vector3(impact.X, impact.Y, impact.Z);

        Vector3 FrameDirection(DestructionImpact impact)
        {
            var direction = new Vector3(impact.DirX, 0f, impact.DirZ);
            if (frame.parent != null) direction = frame.InverseTransformDirection(frame.parent.TransformDirection(direction));
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
        }

        /// <summary>
        /// The way an element falls: out of the building (the hall stands in the way inward, the next columns sideways),
        /// turned up to 30 degrees the way the nearest shot leaned, and a little either way by its own seed.
        /// </summary>
        Vector3 FallDirection(int index)
        {
            var element = graph[index];
            var outward = element.Outward;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.right;
            DestructionImpact nearest = null;
            var near = float.MaxValue;
            foreach (var impact in record.Impacts)
            {
                var d = Vector3.Distance(FramePoint(impact), element.Box.center);
                if (d < near) { near = d; nearest = impact; }
            }
            var push = nearest != null ? FrameDirection(nearest) : Vector3.zero;
            var lean = 0f;
            if (push.sqrMagnitude > 1e-6f)
            {
                var along = Vector3.Cross(Vector3.up, outward);
                // The part of the push across the outward way turns the fall toward it.
                lean = Mathf.Clamp(Vector3.Dot(push, along) * 60f, -30f, 30f);
            }
            lean += DestructionHash.Range(Seed(index), 2, -10f, 10f);
            // A positive turn about up takes the outward way toward the along way (Cross(up, outward)).
            return (Quaternion.AngleAxis(lean, Vector3.up) * outward).normalized;
        }

        /// <summary>What fell with another takes its way: a column's drums go with it, a block tips the way its column went.</summary>
        void InheritFalls()
        {
            for (var round = 0; round < 6; round++)
            {
                foreach (var element in graph.Elements)
                {
                    if (!failed[element.Index] || fall[element.Index].sqrMagnitude > 1e-6f) continue;
                    foreach (var support in element.Supports)
                        if (failed[support] && fall[support].sqrMagnitude > 1e-6f)
                        {
                            var own = element.Outward;
                            own.y = 0f;
                            fall[element.Index] = element.Detach == DetachMethod.Topple || own.sqrMagnitude < 1e-6f
                                ? fall[support]
                                : (own.normalized * 0.7f + fall[support] * 0.3f).normalized;
                            break;
                        }
                }
            }
            for (var i = 0; i < graph.Count; i++)
                if (failed[i] && fall[i].sqrMagnitude < 1e-6f)
                {
                    var own = graph[i].Outward;
                    own.y = 0f;
                    fall[i] = own.sqrMagnitude > 1e-6f ? own.normalized : Vector3.right;
                }
        }

        // ---------- Falling ----------

        /// <summary>Sets the newly failed elements falling: a column's upper part as one piece, each block, bay and wall on its own.</summary>
        void Launch(List<int> fresh, List<int> batch)
        {
            // When each starts: the shots' own failures first (spread over the fall when the whole structure goes), what
            // rested on them a moment after what held them gave way.
            var spread = fallen ? Mathf.Min(0.7f, 0.08f * batch.Count) : 0.25f * Mathf.Max(0, batch.Count - 1);
            foreach (var i in fresh) fallDelay[i] = -1f;
            for (var k = 0; k < batch.Count; k++)
                fallDelay[batch[k]] = (batch.Count > 1 ? spread * k / (batch.Count - 1) : 0f) + 0.05f * DestructionHash.Unit(Seed(batch[k]), 3);
            var order = new List<int>(fresh);
            order.Sort((a, b) => depth[a] != depth[b] ? depth[a].CompareTo(depth[b]) : a.CompareTo(b));
            foreach (var i in order)
            {
                if (fallDelay[i] >= 0f) continue;
                var latest = 0f;
                foreach (var support in graph[i].Supports)
                {
                    if (!failed[support]) continue;
                    // What goes over as one piece with its support (a column's drums, a frieze on its architrave) starts with it.
                    var together = graph[i].Column >= 0 && graph[i].Column == graph[support].Column
                                   || graph[i].Detach == DetachMethod.Drop && graph[support].Detach == DetachMethod.Drop && graph[i].Supports.Length == 1;
                    latest = Mathf.Max(latest, Mathf.Max(0f, fallDelay[support]) + (together ? 0f : Lag(graph[support].Detach)));
                }
                fallDelay[i] = latest;
            }

            var grouped = new HashSet<int>();
            foreach (var i in order)
            {
                if (grouped.Contains(i)) continue;
                var element = graph[i];
                var carried = new List<int> { i };
                if (element.Detach == DetachMethod.Topple && element.Column >= 0)
                    // The drums above the joint that gave and the capital go over with it, as one.
                    foreach (var other in order)
                        if (other != i && graph[other].Column == element.Column && !grouped.Contains(other) && shapes[other].Base.y > shapes[i].Base.y) carried.Add(other);
                if (element.Detach == DetachMethod.Drop)
                    // A block resting on this one alone (the frieze on its architrave) goes down on it.
                    foreach (var other in order)
                        if (other != i && !grouped.Contains(other) && graph[other].Detach == DetachMethod.Drop && graph[other].Supports.Length == 1 && graph[other].Supports[0] == i) carried.Add(other);
                foreach (var c in carried) grouped.Add(c);
                if (bodies.Count >= MaxBodiesPerStructure || bodiesInAir >= MaxBodies)
                {
                    // Over the budget: it is at rest at once, in a puff of dust.
                    foreach (var c in carried) settled[c] = true;
                    Chunk(carried);
                    host.Burst?.Invoke(shapes[i].Centre, element.Material, 0.6f, Seed(i));
                    continue;
                }
                bodies.Add(Launched(carried));
            }
        }

        static float Lag(DetachMethod method) => method switch
        {
            DetachMethod.Topple => 0.3f,
            DetachMethod.Drop => 0.15f,
            DetachMethod.CaveIn => 0.1f,
            DetachMethod.Crumble => 0.4f,
            _ => 0f,
        };

        Body Launched(List<int> carried)
        {
            var body = Take();
            var first = carried[0];
            var element = graph[first];
            body.Elements.AddRange(carried);
            body.Kind = element.Detach;
            body.Delay = fallDelay[first];
            body.Time = 0f;
            body.Started = false;
            body.Flying = true;
            bodiesInAir++;
            var parts = new List<(GreekMesh, Matrix4x4)>();
            foreach (var c in carried) parts.Add((shapes[c].Whole, Matrix4x4.identity));
            Show(body, parts, element.Id);
            var shape = shapes[first];
            var direction = fall[first];
            switch (body.Kind)
            {
                case DetachMethod.Topple:
                {
                    var top = shape.Base.y;
                    foreach (var c in carried) top = Mathf.Max(top, shapes[c].Centre.y + shapes[c].Size.y * 0.5f);
                    body.Pivot = shape.Base + direction * shape.Radius;
                    body.Axis = Vector3.Cross(Vector3.up, direction).normalized;
                    body.Angle = 82f;
                    body.Duration = 0.5f + 0.22f * Mathf.Sqrt(Mathf.Max(0.3f, top - shape.Base.y));
                    break;
                }
                case DetachMethod.Drop:
                {
                    var (rotation, position) = Rest(first);
                    body.RestRotation = rotation;
                    body.RestPosition = position;
                    body.Pivot = shape.Centre + direction * (shape.Size.x * Mathf.Abs(direction.x) + shape.Size.z * Mathf.Abs(direction.z)) * 0.5f - Vector3.up * shape.Size.y * 0.5f;
                    body.Axis = Vector3.Cross(Vector3.up, direction).normalized;
                    body.Angle = 22f;
                    body.Split = 0.3f;
                    body.Duration = 0.75f;
                    break;
                }
                case DetachMethod.CaveIn:
                {
                    var (rotation, position) = Rest(first);
                    body.RestRotation = rotation;
                    body.RestPosition = position;
                    body.Duration = 0.7f;
                    break;
                }
                case DetachMethod.Crumble:
                    body.Pivot = shape.Base;
                    body.ScaleTo = shape.Stump != null && shape.Size.y > 0.01f ? Mathf.Clamp01(shape.Stump.Mesh.bounds.size.y / shape.Size.y) : 0.3f;
                    body.Duration = 1.2f;
                    break;
                default:
                    body.Duration = 0.01f;
                    break;
            }
            Pose(body, 0f);
            return body;
        }

        /// <summary>Moves the falling pieces on by game time (none in a pause); true once all of them are at rest.</summary>
        public bool Animate(float delta)
        {
            if (bodies.Count == 0) return true;
            for (var k = bodies.Count - 1; k >= 0; k--)
            {
                var body = bodies[k];
                body.Time += delta;
                var t = body.Time - body.Delay;
                if (t < 0f) continue;
                if (!body.Started)
                {
                    body.Started = true;
                    var shape = shapes[body.Elements[0]];
                    var material = graph[body.Elements[0]].Material;
                    host.Burst?.Invoke(body.Kind == DetachMethod.Topple ? shape.Base : shape.Centre, material, body.Kind == DetachMethod.CaveIn ? 0.9f : 0.45f, Seed(body.Elements[0]));
                }
                Pose(body, t);
                if (t < body.Duration) continue;
                Land(body);
                bodies.RemoveAt(k);
            }
            if (bodies.Count > 0)
            {
                if (chunks.Count >= MaxChunks) MergeChunks();
                return false;
            }
            // All at rest: the chunks go into one mesh of rubble, and the stumps stand.
            Stage = DamagePlan.StageOf(damage, fallen, true);
            Rebuild();
            return true;
        }

        void Pose(Body body, float t)
        {
            var transform = body.Object.transform;
            switch (body.Kind)
            {
                case DetachMethod.Topple:
                {
                    var u = Mathf.Clamp01(t / body.Duration);
                    var rotation = Quaternion.AngleAxis(body.Angle * u * u, body.Axis);
                    transform.localRotation = rotation;
                    transform.localPosition = body.Pivot - rotation * body.Pivot;
                    transform.localScale = Vector3.one;
                    break;
                }
                case DetachMethod.Drop:
                {
                    var tip = Quaternion.AngleAxis(body.Angle * Mathf.Clamp01(t / body.Split) * Mathf.Clamp01(t / body.Split), body.Axis);
                    var tipped = body.Pivot - tip * body.Pivot;
                    if (t <= body.Split)
                    {
                        transform.localRotation = tip;
                        transform.localPosition = tipped;
                    }
                    else
                    {
                        var u = Mathf.Clamp01((t - body.Split) / (body.Duration - body.Split));
                        Blend(transform, tip, tipped, body.RestRotation, body.RestPosition, u * u, shapes[body.Elements[0]].Centre);
                    }
                    transform.localScale = Vector3.one;
                    break;
                }
                case DetachMethod.CaveIn:
                {
                    var u = Mathf.Clamp01(t / body.Duration);
                    Blend(transform, Quaternion.identity, Vector3.zero, body.RestRotation, body.RestPosition, u * u, shapes[body.Elements[0]].Centre);
                    transform.localScale = Vector3.one;
                    break;
                }
                case DetachMethod.Crumble:
                {
                    var u = Mathf.Clamp01(t / body.Duration);
                    var s = Mathf.Lerp(1f, body.ScaleTo, Mathf.Pow(u, 1.5f));
                    transform.localRotation = Quaternion.identity;
                    transform.localScale = new Vector3(1f, s, 1f);
                    transform.localPosition = new Vector3(0f, body.Pivot.y * (1f - s), 0f);
                    if (u > 0.5f && !body.Midway)
                    {
                        // Halfway down, a second fall of crumbs and dust.
                        body.Midway = true;
                        host.Burst?.Invoke(shapes[body.Elements[0]].Centre, BuildMaterial.Mudbrick, 0.5f, Seed(body.Elements[0]) + 7u);
                    }
                    break;
                }
            }
        }

        /// <summary>Between two poses of a piece: its middle moves straight, it turns the short way.</summary>
        static void Blend(Transform transform, Quaternion r0, Vector3 p0, Quaternion r1, Vector3 p1, float u, Vector3 centre)
        {
            var rotation = Quaternion.Slerp(r0, r1, u);
            var middle = Vector3.Lerp(r0 * centre + p0, r1 * centre + p1, u);
            transform.localRotation = rotation;
            transform.localPosition = middle - rotation * centre;
        }

        /// <summary>A piece at rest: its elements lie in their rest poses as a chunk until the whole fall is over.</summary>
        void Land(Body body)
        {
            foreach (var i in body.Elements) settled[i] = true;
            var first = body.Elements[0];
            var shape = shapes[first];
            var material = graph[first].Material;
            if (body.Kind == DetachMethod.Topple)
            {
                // Dust and chips along the line the column fell on.
                for (var k = 1; k <= 3; k++)
                    host.Burst?.Invoke(shape.Base + fall[first] * k * 0.9f, material, 0.9f, Seed(first) + (uint)k);
            }
            else if (body.Kind == DetachMethod.CaveIn)
            {
                // The bay breaks up as it strikes: tiles fly, rafters crack, and the heap is what is left.
                var heap = (OuterGone(first) && shape.HeapOut != null ? shape.HeapOut : shape.Heap)?.Mesh;
                var at = heap != null ? heap.bounds.center : body.RestRotation * shape.Centre + body.RestPosition;
                host.Burst?.Invoke(at, BuildMaterial.Ceramic, 1.1f, Seed(first) + 11u);
                host.Burst?.Invoke(at, BuildMaterial.Timber, 0.5f, Seed(first) + 12u);
            }
            else host.Burst?.Invoke(body.Kind == DetachMethod.Crumble ? shape.Base : body.RestRotation * shape.Centre + body.RestPosition, material, 0.8f, Seed(first) + 11u);
            Chunk(body.Elements);
            Release(body);
        }

        void Chunk(List<int> elements)
        {
            var parts = new List<(GreekMesh, Matrix4x4)>();
            foreach (var i in elements) AddRest(parts, i);
            if (parts.Count == 0) return;
            var chunk = Take();
            Show(chunk, parts, "Chunk");
            chunk.Object.transform.localPosition = Vector3.zero;
            chunk.Object.transform.localRotation = Quaternion.identity;
            chunk.Object.transform.localScale = Vector3.one;
            chunks.Add(chunk);
            SettledPieces += elements.Count;
        }

        /// <summary>A bay of roof lost what held its eave (the colonnade), not only the wall under its slope.</summary>
        bool OuterGone(int i)
        {
            foreach (var support in graph[i].Supports)
                if (failed[support] && graph[support].Role != ElementRole.Wall) return true;
            return false;
        }

        void AddRest(List<(GreekMesh, Matrix4x4)> parts, int i)
        {
            var shape = shapes[i];
            if (graph[i].Detach == DetachMethod.Crumble)
            {
                if (shape.Stump != null) parts.Add((shape.Stump, Matrix4x4.identity));
            }
            else if (graph[i].Detach == DetachMethod.CaveIn)
            {
                // Broken tiles and rafters: outside below the eave when it slid off, else in the hall.
                var heap = OuterGone(i) && shape.HeapOut != null ? shape.HeapOut : shape.Heap;
                if (heap != null) parts.Add((heap, Matrix4x4.identity));
                return;
            }
            else if (shape.Whole != null)
            {
                var (rotation, position) = Rest(i);
                parts.Add((shape.Whole, Matrix4x4.TRS(position, rotation, Vector3.one)));
            }
            if (shape.Heap != null) parts.Add((shape.Heap, Matrix4x4.identity));
        }

        // ---------- Rest poses ----------

        /// <summary>
        /// Where a failed element comes to rest, worked out from its shape, the way it fell and the ground: the same element
        /// falling the same way always lies in the same place, seen falling or not.
        /// </summary>
        (Quaternion, Vector3) Rest(int i)
        {
            var element = graph[i];
            var shape = shapes[i];
            var hash = Seed(i);
            var direction = fall[i].sqrMagnitude > 1e-6f ? fall[i].normalized : Vector3.right;
            switch (element.Detach)
            {
                case DetachMethod.Topple:
                {
                    // The column's joint that gave: the lowest failed drum of its column.
                    var breakAt = shape.Base;
                    var breakRadius = shape.Radius;
                    var column = element.Column;
                    var turn = Quaternion.AngleAxis(DestructionHash.Range(DestructionHash.Mix(seed, column), 4, -9f, 9f), Vector3.up);
                    foreach (var other in graph.Elements)
                        if (other.Column == column && failed[other.Index] && shapes[other.Index].Base.y < breakAt.y)
                        {
                            breakAt = shapes[other.Index].Base;
                            breakRadius = shapes[other.Index].Radius;
                        }
                    var heading = (turn * direction).normalized;
                    var pivot = breakAt + direction * breakRadius;
                    var reach = shape.Centre.y - pivot.y;
                    var spread = 1.04f + 0.05f * DestructionHash.Unit(hash, 5);
                    var side = Vector3.Cross(Vector3.up, heading);
                    var centre = new Vector3(pivot.x, 0f, pivot.z) + heading * (reach * spread) + side * DestructionHash.Range(hash, 6, -0.12f, 0.12f);
                    centre.y = host.RestHeight(centre) + shape.Radius;
                    var lie = Quaternion.AngleAxis(DestructionHash.Range(hash, 7, 0f, 360f), heading) * Quaternion.FromToRotation(Vector3.up, heading);
                    return (lie, centre - lie * shape.Centre);
                }
                case DetachMethod.Drop:
                {
                    // Tipped over its outer edge onto its face, out from where it stood, turned a little.
                    var span = Mathf.Max(shape.Size.x * Mathf.Abs(direction.z) + shape.Size.z * Mathf.Abs(direction.x), 0.2f);
                    var height = shape.Centre.y - host.RestHeight(shape.Centre);
                    var centre = shape.Centre + direction * (0.5f + 0.35f * Mathf.Max(0f, height) + DestructionHash.Range(hash, 8, 0f, 0.6f));
                    centre += Vector3.Cross(Vector3.up, direction) * DestructionHash.Range(hash, 9, -0.25f, 0.25f) * span;
                    var tip = Quaternion.AngleAxis(DestructionHash.Range(hash, 10, 80f, 105f), Vector3.Cross(Vector3.up, direction).normalized);
                    var yaw = Quaternion.AngleAxis(DestructionHash.Range(hash, 11, -14f, 14f), Vector3.up);
                    var lie = yaw * tip;
                    centre.y = host.RestHeight(centre) + Mathf.Max(0.05f, shape.Radius);
                    return (lie, centre - lie * shape.Centre);
                }
                case DetachMethod.CaveIn:
                {
                    // Where the bay is when it breaks up: swung down off the hall's wall when the colonnade went, else
                    // dropped in about its eave. Its tiles then lie in a heap (AddRest), not as a slab.
                    if (OuterGone(i))
                    {
                        var angle = Mathf.Min(35f, SwingTo(shape.InnerHinge, shape.EaveEnd, out var sign));
                        var rotation = Quaternion.AngleAxis(sign * angle, Vector3.forward);
                        return (rotation, shape.InnerHinge - rotation * shape.InnerHinge);
                    }
                    var inward = Mathf.Min(25f, SwingTo(shape.EaveHinge, shape.RidgeEnd, out var turn));
                    var drop = Quaternion.AngleAxis(turn * inward, Vector3.forward);
                    return (drop, shape.EaveHinge - drop * shape.EaveHinge + Vector3.down * 0.8f);
                }
                default:
                    return (Quaternion.identity, Vector3.zero);
            }
        }

        /// <summary>The angle a bay must swing about a line along z through the hinge for its end to come down to rest.</summary>
        float SwingTo(Vector3 hinge, Vector3 end, out float sign)
        {
            var down = Quaternion.AngleAxis(5f, Vector3.forward) * (end - hinge);
            var turn = down.y < (end - hinge).y ? 1f : -1f;
            sign = turn;
            float Height(float a)
            {
                var p = hinge + Quaternion.AngleAxis(turn * a, Vector3.forward) * (end - hinge);
                return p.y - (host.RestHeight(p) + 0.12f);
            }
            if (Height(85f) > 0f) return 85f;
            float low = 0f, high = 85f;
            for (var k = 0; k < 24; k++)
            {
                var mid = (low + high) * 0.5f;
                if (Height(mid) > 0f) low = mid; else high = mid;
            }
            return high;
        }

        // ---------- Scars ----------

        /// <summary>Adds or takes away scars to the number the damage gives; true if the scars shown changed.</summary>
        bool UpdateScars()
        {
            var wanted = fallen ? DamagePlan.MaxScars : DamagePlan.Scars(damage);
            var mine = record.Scars.FindAll(scar => scar.Element.StartsWith(prefix));
            var changed = false;
            while (mine.Count > wanted)
            {
                record.Scars.Remove(mine[mine.Count - 1]);
                mine.RemoveAt(mine.Count - 1);
                changed = true;
            }
            while (mine.Count < wanted)
            {
                var scar = NewScar(mine.Count);
                if (scar == null) break;
                record.Scars.Add(scar);
                mine.Add(scar);
                changed = true;
            }
            var shown = 0;
            foreach (var scar in mine)
            {
                var index = graph.IndexOf(scar.Element.Substring(prefix.Length));
                if (index >= 0 && !failed[index]) shown++;
            }
            changed |= shown != Scars;
            Scars = shown;
            return changed;
        }

        /// <summary>A scar where a shot struck: on the face of the standing element nearest it, toward the shot.</summary>
        DestructionScar NewScar(int count)
        {
            var hash = DestructionHash.Mix(seed, 1000 + count);
            Vector3 at;
            if (record.Impacts.Count > 0) at = FramePoint(record.Impacts[record.Impacts.Count - 1 - count % record.Impacts.Count]);
            else
            {
                var angle = DestructionHash.Range(hash, 1, 0f, Mathf.PI * 2f);
                at = new Vector3(Mathf.Cos(angle) * 8f, 2f, Mathf.Sin(angle) * 8f);
            }
            at += new Vector3(DestructionHash.Range(hash, 2, -1.2f, 1.2f), DestructionHash.Range(hash, 3, -0.5f, 1.5f), DestructionHash.Range(hash, 4, -1.2f, 1.2f));
            var best = -1;
            var near = float.MaxValue;
            foreach (var element in graph.Elements)
            {
                if (failed[element.Index] || element.Role == ElementRole.Decor) continue;
                var d = element.Box.SqrDistance(at) + 0.3f * DestructionHash.Unit(hash, 10 + element.Index % 50);
                if (d < near) { near = d; best = element.Index; }
            }
            if (best < 0) return null;
            var box = graph[best].Box;
            var shape = shapes[best];
            Vector3 point, normal;
            if (graph[best].Detach == DetachMethod.Topple)
            {
                // Round: on the drum's side toward the shot.
                var toward = at - shape.Centre;
                toward.y = 0f;
                normal = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.right;
                point = new Vector3(shape.Centre.x, Mathf.Clamp(at.y, box.min.y + 0.1f, box.max.y - 0.1f), shape.Centre.z) + normal * shape.Radius;
            }
            else if (graph[best].Role == ElementRole.Roof)
            {
                // On the slope of tiles, between its eave and its ridge.
                var eave = shape.EaveEnd;
                var ridge = shape.RidgeEnd;
                var t = Mathf.Abs(ridge.x - eave.x) > 1e-3f ? Mathf.Clamp01((at.x - eave.x) / (ridge.x - eave.x)) : 0.5f;
                t = Mathf.Clamp(t, 0.15f, 0.85f);
                point = Vector3.Lerp(eave, ridge, t);
                point.z = Mathf.Clamp(at.z, box.min.z + 0.2f, box.max.z - 0.2f);
                var slope = ridge - eave;
                slope.z = 0f;
                normal = Vector3.Cross(Vector3.Cross(slope, Vector3.up), slope).normalized;
                if (normal.y < 0f) normal = -normal;
            }
            else
            {
                var inside = box.ClosestPoint(at);
                var offset = at - box.center;
                var e = box.extents;
                var ax = Mathf.Abs(offset.x) / Mathf.Max(e.x, 0.01f);
                var az = Mathf.Abs(offset.z) / Mathf.Max(e.z, 0.01f);
                normal = ax >= az ? new Vector3(Mathf.Sign(offset.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(offset.z));
                point = inside;
                if (ax >= az) point.x = box.center.x + normal.x * e.x; else point.z = box.center.z + normal.z * e.z;
                point.y = Mathf.Clamp(point.y, box.min.y + 0.1f, box.max.y - 0.1f);
            }
            return new DestructionScar
            {
                Element = prefix + graph[best].Id,
                X = point.x, Y = point.y, Z = point.z, NX = normal.x, NY = normal.y, NZ = normal.z,
                Size = DestructionHash.Range(hash, 5, 0.16f, 0.38f) * (graph[best].Detach == DetachMethod.Topple ? 0.6f : 1f), Seed = (int)hash,
            };
        }

        /// <summary>
        /// A scar's look by what it is on: limestone under knocked-off whitewash on a column or block, bright fresh stone on
        /// the steps, brick under plaster on a wall, a hole into the dark under broken tiles on a roof.
        /// </summary>
        void DrawScar(GreekShips.MeshBuilder b, DestructionScar scar, BuildMaterial material)
        {
            var paint = material switch
            {
                BuildMaterial.StuccoedStone => GreekMaterial.Stone,
                BuildMaterial.Stone => GreekMaterial.Paint,
                BuildMaterial.Mudbrick => GreekMaterial.Mudbrick,
                BuildMaterial.Ceramic => GreekMaterial.Pitch,
                _ => GreekMaterial.Wood,
            };
            var normal = new Vector3(scar.NX, scar.NY, scar.NZ).normalized;
            var centre = new Vector3(scar.X, scar.Y, scar.Z) + normal * 0.02f;
            var u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(normal, u);
            var hash = (uint)scar.Seed;
            if (material == BuildMaterial.Stone)
            {
                // Dressed stone does not peel: chips broken off its edges lie at the foot of the face it struck.
                var foot = new Vector3(scar.X, scar.Y, scar.Z) + normal * 0.12f;
                foot.y = scar.Y - (Mathf.Abs(normal.y) > 0.9f ? 0f : 0.25f);
                for (var k = 0; k < 5; k++)
                {
                    var size = scar.Size * DestructionHash.Range(hash, 70 + k, 0.18f, 0.4f);
                    var p = foot + (u * DestructionHash.Range(hash, 80 + k, -1f, 1f) + (Mathf.Abs(normal.y) > 0.9f ? v : normal) * DestructionHash.Range(hash, 90 + k, 0f, 1f)) * scar.Size;
                    p.y = host.RestHeight(p) + size * 0.4f;
                    b.Box(GreekMaterial.Stone, p, new Vector3(size, size * 0.6f, size * 0.8f), Quaternion.Euler(DestructionHash.Range(hash, 100 + k, -25f, 25f), DestructionHash.Range(hash, 110 + k, 0f, 360f), DestructionHash.Range(hash, 120 + k, -25f, 25f)));
                }
                return;
            }
            const int points = 7;
            var middle = b.Vertex(centre, Vector2.zero);
            var rim = new int[points];
            for (var k = 0; k < points; k++)
            {
                var angle = k * Mathf.PI * 2f / points + DestructionHash.Range(hash, 20 + k, -0.3f, 0.3f);
                var reach = scar.Size * DestructionHash.Range(hash, 30 + k, 0.45f, 1f);
                var p = centre + (u * Mathf.Cos(angle) * 1.3f + v * Mathf.Sin(angle)) * reach;
                rim[k] = b.Vertex(p, new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)));
            }
            for (var k = 0; k < points; k++) b.Triangle(paint, middle, rim[k], rim[(k + 1) % points], normal);
            if (material == BuildMaterial.Ceramic)
                // Broken tiles about the hole.
                for (var k = 0; k < 3; k++)
                {
                    var p = centre + (u * DestructionHash.Range(hash, 40 + k, -1f, 1f) + v * DestructionHash.Range(hash, 50 + k, -1f, 1f)) * scar.Size * 1.2f;
                    b.Box(GreekMaterial.Terracotta, p, new Vector3(0.11f, 0.02f, 0.08f), Quaternion.LookRotation(v, normal) * Quaternion.Euler(0f, DestructionHash.Range(hash, 60 + k, 0f, 180f), 0f));
                }
        }

        // ---------- Meshes ----------

        /// <summary>Rebuilds what stands, the scars and the rubble at rest; pieces still falling are drawn on their own.</summary>
        void Rebuild()
        {
            frame.gameObject.SetActive(Broken || Scars > 0);
            var falling = new HashSet<int>();
            foreach (var body in bodies) foreach (var i in body.Elements) falling.Add(i);
            var standingParts = new List<(GreekMesh, Matrix4x4)>();
            var rubbleParts = new List<(GreekMesh, Matrix4x4)>();
            if (Broken)
            {
                for (var i = 0; i < graph.Count; i++)
                {
                    if (!failed[i])
                    {
                        if (shapes[i].Whole != null) standingParts.Add((shapes[i].Whole, Matrix4x4.identity));
                        continue;
                    }
                    if (falling.Contains(i) || !settled[i]) continue;
                    // While pieces still fall, what is at rest stays in its chunks or the rubble merged so far.
                    if (bodies.Count > 0) continue;
                    if (graph[i].Detach == DetachMethod.Crumble)
                    {
                        if (shapes[i].Stump != null) standingParts.Add((shapes[i].Stump, Matrix4x4.identity));
                        if (shapes[i].Heap != null) rubbleParts.Add((shapes[i].Heap, Matrix4x4.identity));
                        continue;
                    }
                    AddRest(rubbleParts, i);
                }
            }
            if (bodies.Count == 0)
            {
                foreach (var chunk in chunks) Release(chunk);
                chunks.Clear();
                SettledPieces = 0;
                for (var i = 0; i < graph.Count; i++) if (failed[i] && settled[i]) SettledPieces++;
            }
            else
            {
                // While pieces still fall, what has come to rest stays in its chunks; the rubble mesh waits for the end.
                rubbleParts.Clear();
            }
            Fill(standing, ref standingMesh, standingParts);
            if (bodies.Count == 0) Fill(rubble, ref rubbleMesh, rubbleParts);

            var scarBuilder = new GreekShips.MeshBuilder(null);
            var any = false;
            foreach (var scar in record.Scars)
            {
                if (!scar.Element.StartsWith(prefix)) continue;
                var index = graph.IndexOf(scar.Element.Substring(prefix.Length));
                if (index < 0 || failed[index]) continue;
                DrawScar(scarBuilder, scar, graph[index].Material);
                any = true;
            }
            var scarParts = new List<(GreekMesh, Matrix4x4)>();
            GreekMesh built = null;
            if (any)
            {
                built = scarBuilder.Build(GreekMaterial.Stone, GreekMaterial.Paint, GreekMaterial.Mudbrick, GreekMaterial.Pitch, GreekMaterial.Terracotta, GreekMaterial.Wood);
                scarParts.Add((built, Matrix4x4.identity));
            }
            Fill(scarObject, ref scarMesh, scarParts);
            if (built != null) Destroy(built.Mesh);
        }

        /// <summary>
        /// While pieces still fall, those at rest go from their chunks into the rubble's one mesh (a stump with them for
        /// now: it stands where it is either way), so a long fall never piles up renderers.
        /// </summary>
        void MergeChunks()
        {
            var falling = new HashSet<int>();
            foreach (var body in bodies) foreach (var i in body.Elements) falling.Add(i);
            var parts = new List<(GreekMesh, Matrix4x4)>();
            var count = 0;
            for (var i = 0; i < graph.Count; i++)
            {
                if (!failed[i] || !settled[i] || falling.Contains(i)) continue;
                AddRest(parts, i);
                count++;
            }
            foreach (var chunk in chunks) Release(chunk);
            chunks.Clear();
            Fill(rubble, ref rubbleMesh, parts);
            SettledPieces = count;
        }

        void Fill(GameObject target, ref Mesh mesh, List<(GreekMesh, Matrix4x4)> parts)
        {
            if (mesh != null) { Free(mesh); mesh = null; }
            var filter = target.GetComponent<MeshFilter>();
            var renderer = target.GetComponent<MeshRenderer>();
            if (parts.Count == 0)
            {
                filter.sharedMesh = null;
                target.SetActive(false);
                return;
            }
            mesh = Combine(parts, out var slots);
            mesh.name = $"{frame.name} {target.name}";
            Own(mesh);
            filter.sharedMesh = mesh;
            var list = host.Materials(slots);
            if (list != null && Array.TrueForAll(list, m => m != null)) renderer.sharedMaterials = list;
            target.SetActive(true);
        }

        /// <summary>
        /// Meshes put together into one with a submesh per paint, keeping their normals and UVs: each paint's pieces merged
        /// first, then the paints side by side.
        /// </summary>
        internal static Mesh Combine(List<(GreekMesh mesh, Matrix4x4 at)> parts, out GreekMaterial[] slots)
        {
            var byPaint = new Dictionary<GreekMaterial, List<CombineInstance>>();
            var order = new List<GreekMaterial>();
            var vertices = 0;
            foreach (var (mesh, at) in parts)
            {
                if (mesh?.Mesh == null) continue;
                vertices += mesh.Mesh.vertexCount;
                for (var sub = 0; sub < mesh.Slots.Length && sub < mesh.Mesh.subMeshCount; sub++)
                {
                    var paint = mesh.Slots[sub];
                    if (!byPaint.TryGetValue(paint, out var list)) { byPaint[paint] = list = new List<CombineInstance>(); order.Add(paint); }
                    list.Add(new CombineInstance { mesh = mesh.Mesh, subMeshIndex = sub, transform = at });
                }
            }
            var wide = vertices > 60000;
            var merged = new List<CombineInstance>();
            var made = new List<Mesh>();
            foreach (var paint in order)
            {
                var part = new Mesh();
                if (wide) part.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                part.CombineMeshes(byPaint[paint].ToArray(), true, true);
                made.Add(part);
                merged.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
            }
            var result = new Mesh();
            if (wide) result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            result.CombineMeshes(merged.ToArray(), false, false);
            result.RecalculateBounds();
            foreach (var part in made) Destroy(part);
            slots = order.ToArray();
            return result;
        }

        void Show(Body body, List<(GreekMesh, Matrix4x4)> parts, string name)
        {
            if (body.Mesh != null) { Free(body.Mesh); body.Mesh = null; }
            body.Mesh = Combine(parts, out var slots);
            Own(body.Mesh);
            body.Filter.sharedMesh = body.Mesh;
            var list = host.Materials(slots);
            if (list != null && Array.TrueForAll(list, m => m != null)) body.Renderer.sharedMaterials = list;
            body.Object.name = name;
            body.Object.SetActive(true);
        }

        // ---------- Pool ----------

        Body Take()
        {
            Body body;
            if (pool.Count > 0)
            {
                body = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
            }
            else
            {
                var item = new GameObject("Piece");
                item.transform.SetParent(frame, false);
                body = new Body { Object = item, Filter = item.AddComponent<MeshFilter>(), Renderer = item.AddComponent<MeshRenderer>() };
                created++;
            }
            body.Elements.Clear();
            return body;
        }

        /// <summary>Back to the pool: hidden, its mesh freed, put back where it was made, carrying nothing.</summary>
        void Release(Body body)
        {
            if (body.Flying) { body.Flying = false; bodiesInAir = Mathf.Max(0, bodiesInAir - 1); }
            if (body.Mesh != null) { Free(body.Mesh); body.Mesh = null; }
            body.Filter.sharedMesh = null;
            body.Elements.Clear();
            body.Time = 0f;
            body.Started = false;
            body.Midway = false;
            var transform = body.Object.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            body.Object.name = "Piece";
            body.Object.SetActive(false);
            pool.Add(body);
        }

        /// <summary>Every piece back to the pool: the fall stops where it is and the rubble is drawn at rest.</summary>
        void ReleaseAll()
        {
            foreach (var body in bodies)
            {
                foreach (var i in body.Elements) settled[i] = true;
                Release(body);
            }
            bodies.Clear();
            foreach (var chunk in chunks) Release(chunk);
            chunks.Clear();
        }

        /// <summary>Puts the structure back whole: no failures, scars or pieces (the record is the caller's to clear).</summary>
        public void Clear()
        {
            ReleaseAll();
            Array.Clear(failed, 0, failed.Length);
            Array.Clear(settled, 0, settled.Length);
            Broken = false;
            Scars = 0;
            FailedCount = 0;
            damage = -1f;
            applied = false;
            Stage = DestructionStage.Intact;
            Fill(standing, ref standingMesh, new List<(GreekMesh, Matrix4x4)>());
            Fill(rubble, ref rubbleMesh, new List<(GreekMesh, Matrix4x4)>());
            Fill(scarObject, ref scarMesh, new List<(GreekMesh, Matrix4x4)>());
            frame.gameObject.SetActive(false);
        }

        /// <summary>The view goes with its settlement: its pieces in the air leave the budget.</summary>
        public void Forget()
        {
            foreach (var body in bodies)
                if (body.Flying) { body.Flying = false; bodiesInAir = Mathf.Max(0, bodiesInAir - 1); }
            bodies.Clear();
        }

        void Own(Mesh mesh) => host.Owned?.Add(mesh);

        void Free(Mesh mesh)
        {
            host.Owned?.Remove(mesh);
            Destroy(mesh);
        }

        static void Destroy(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
