using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation.Hud;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>How a fitted post stands, in the ship card's words: ready, without its crew, or broken until a dock mends it.</summary>
    public enum MountState { Ready, Unmanned, Destroyed }

    /// <summary>
    /// The firing arcs of the player's selected warships, laid on the sea (docs/UZBROJENIE.md, 3c part 2): for each kind
    /// of weapon fitted, the sector it can reach, from its least to its greatest range (WeaponCatalog) and half its arc to
    /// each side of the bow, measured from the ship's centre exactly as World.InReach does. A faint fill with a firmer
    /// edge; inside the far edge one band per fitted post of that kind, side by side inward, coloured by its state: the
    /// side's colour when ready, amber and broken into dashes when it lacks its crew (World.Manned), red and dashed when
    /// its module is broken. Empty posts draw nothing. At most MaxShips ships at once. The mesh is in the ship's frame and
    /// is rebuilt only when a weapon or a post's state changes; each frame only follows the ship. Reads the simulation only.
    /// </summary>
    public sealed class FiringArcs
    {
        public const int MaxShips = 8;
        public const float Height = 1.2f;
        /// <summary>The edge line, each post's band and the gap between bands, metres; dashes and gaps of a broken band.</summary>
        public const float Edge = 1.2f, BandWidth = 2.6f, Gap = 1.4f, Dash = 7f;

        enum Paint { Fill, Edge, Ready, Unmanned, Destroyed }

        sealed class Arc
        {
            public GameObject Root;
            public Mesh Mesh;
            public long Key = long.MinValue;
            public MountState?[] States = System.Array.Empty<MountState?>();
        }

        readonly PlaceholderMaterials materials;
        readonly Transform holder;
        readonly Dictionary<int, Arc> arcs = new Dictionary<int, Arc>();
        readonly List<int> drop = new List<int>();
        Material[] palette;

        public FiringArcs(PlaceholderMaterials materials)
        {
            this.materials = materials;
            holder = new GameObject("Firing arcs").transform;
        }

        public Transform Root => holder;

        /// <summary>Ships whose arcs were drawn in the last frame.</summary>
        public int Shown { get; private set; }

        public bool ShowsFor(int shipId) => arcs.TryGetValue(shipId, out var arc) && arc.Root.activeSelf;

        /// <summary>Each post's state as drawn, by post; null for an empty post.</summary>
        public IReadOnlyList<MountState?> StatesFor(int shipId) => arcs.TryGetValue(shipId, out var arc) ? arc.States : System.Array.Empty<MountState?>();

        /// <summary>The drawn mesh in the ship's frame (+Z bow), for tests.</summary>
        public Mesh MeshFor(int shipId) => arcs.TryGetValue(shipId, out var arc) ? arc.Mesh : null;

        public static MountState? StateOf(Ship ship, int slot, bool[] manned)
        {
            if (slot >= ship.Mounts.Length || ship.Mounts[slot] == WeaponType.None) return null;
            if (slot < ship.ModuleHp.Length && ship.ModuleHp[slot] <= 0) return MountState.Destroyed;
            return manned[slot] ? MountState.Ready : MountState.Unmanned;
        }

        /// <summary>The sector a weapon reaches: least and greatest range and half its arc in radians (π for all round).</summary>
        public static (float inner, float outer, float half) Sector(WeaponDefinition weapon) =>
            ((float)weapon.MinRange, (float)weapon.MaxRange, weapon.ArcDegrees >= 360 ? Mathf.PI : (float)(weapon.ArcDegrees * Mathf.Deg2Rad * 0.5));

        /// <summary>A point at this distance and bearing off the bow (radians) lies in the drawn sector.</summary>
        public static bool Covers(WeaponDefinition weapon, float bearing, float distance)
        {
            var (inner, outer, half) = Sector(weapon);
            return distance >= inner && distance <= outer && Mathf.Abs(Mathf.DeltaAngle(0f, bearing * Mathf.Rad2Deg)) * Mathf.Deg2Rad <= half;
        }

        public void Update(World world, int faction, IReadOnlyCollection<int> selected, IReadOnlyDictionary<int, ShipVisual> views)
        {
            Shown = 0;
            foreach (var pair in arcs) pair.Value.Root.SetActive(false);
            foreach (var id in selected.OrderBy(item => item))
            {
                if (Shown >= MaxShips) break;
                var ship = world.FindShip(id);
                if (ship == null || ship.Owner != faction || !World.HasWeapons(ship)) continue;
                if (!views.TryGetValue(id, out var view) || view == null || !view.gameObject.activeInHierarchy) continue;
                var arc = ArcFor(id);
                var manned = World.Manned(ship);
                var key = 17L;
                for (var i = 0; i < ship.Mounts.Length; i++) key = key * 31 + (int)ship.Mounts[i] * 4 + (int)(StateOf(ship, i, manned) ?? (MountState)3);
                if (key != arc.Key)
                {
                    arc.Key = key;
                    arc.States = new MountState?[ship.Mounts.Length];
                    for (var i = 0; i < ship.Mounts.Length; i++) arc.States[i] = StateOf(ship, i, manned);
                    Build(arc.Mesh, ship, arc.States);
                }
                var at = view.transform.position;
                arc.Root.transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(0f, view.transform.eulerAngles.y, 0f));
                arc.Root.SetActive(true);
                Shown++;
            }

            drop.Clear();
            foreach (var id in arcs.Keys) if (world.FindShip(id) == null) drop.Add(id);
            foreach (var id in drop)
            {
                Object.Destroy(arcs[id].Root);
                Object.Destroy(arcs[id].Mesh);
                arcs.Remove(id);
            }
        }

        Arc ArcFor(int shipId)
        {
            if (arcs.TryGetValue(shipId, out var arc)) return arc;
            arc = new Arc { Root = new GameObject($"Firing arcs {shipId}"), Mesh = new Mesh { name = "Firing arcs" } };
            arc.Root.transform.SetParent(holder, false);
            arc.Root.AddComponent<MeshFilter>().sharedMesh = arc.Mesh;
            var renderer = arc.Root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var list = Palette();
            if (list != null) renderer.sharedMaterials = list;
            arcs[shipId] = arc;
            return arc;
        }

        Material[] Palette()
        {
            if (palette != null) return palette;
            var ring = materials?.Selection;
            if (ring == null) return null;
            palette = new[]
            {
                OverlayMeshes.Colour(ring, OverlayMeshes.WithAlpha(HudPalette.Ally, 0.1f)),
                OverlayMeshes.Colour(ring, OverlayMeshes.WithAlpha(HudPalette.Ally, 0.55f)),
                OverlayMeshes.Colour(ring, OverlayMeshes.WithAlpha(HudPalette.Ally, 0.9f)),
                OverlayMeshes.Colour(ring, OverlayMeshes.WithAlpha(HudPalette.Warning, 0.95f)),
                OverlayMeshes.Colour(ring, OverlayMeshes.WithAlpha(HudPalette.Critical, 0.95f)),
            };
            return palette;
        }

        /// <summary>
        /// The arcs of one ship in its own frame: per kind of weapon its sector filled faintly with an edge along every side,
        /// and inside the far edge one band per fitted post of that kind, solid when ready, dashed otherwise. Five
        /// submeshes in the order of Paint, always all present so the renderer's materials keep their places.
        /// </summary>
        public static void Build(Mesh mesh, Ship ship, IReadOnlyList<MountState?> states)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>[5];
            for (var i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();

            foreach (var group in Enumerable.Range(0, ship.Mounts.Length).Where(i => states[i] != null).GroupBy(i => ship.Mounts[i]))
            {
                var weapon = WeaponCatalog.Get(group.Key);
                var (inner, outer, half) = Sector(weapon);
                var full = half >= Mathf.PI - 1e-4f;
                Band(vertices, uvs, triangles[(int)Paint.Fill], inner, outer, -half, half, true);
                Band(vertices, uvs, triangles[(int)Paint.Edge], outer - Edge, outer, -half, half, false);
                Band(vertices, uvs, triangles[(int)Paint.Edge], inner, inner + Edge, -half, half, false);
                if (!full)
                {
                    foreach (var side in new[] { -1f, 1f }) Radial(vertices, uvs, triangles[(int)Paint.Edge], inner, outer, side * half);
                }
                var k = 0;
                foreach (var slot in group)
                {
                    var state = states[slot].Value;
                    var top = outer - Edge - Gap - k * (BandWidth + Gap);
                    var bottom = top - BandWidth;
                    k++;
                    if (bottom <= inner + Edge) break;
                    var paint = state == MountState.Ready ? Paint.Ready : state == MountState.Unmanned ? Paint.Unmanned : Paint.Destroyed;
                    if (state == MountState.Ready) Band(vertices, uvs, triangles[(int)paint], bottom, top, -half, half, false);
                    else
                    {
                        // Dashes of equal length along the band, centred on the bow.
                        var middle = (top + bottom) * 0.5f;
                        var step = Dash / middle;
                        for (var a = -half; a < half - 1e-4f; a += step * 2f)
                            Band(vertices, uvs, triangles[(int)paint], bottom, top, a, Mathf.Min(half, a + step), false);
                    }
                }
            }

            mesh.Clear();
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            var normals = new Vector3[vertices.Count];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.subMeshCount = triangles.Length;
            for (var i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
        }

        static Vector3 Point(float radius, float bearing) => new Vector3(Mathf.Sin(bearing) * radius, 0f, Mathf.Cos(bearing) * radius);

        /// <summary>A ring band between two radii over a span of bearings; V across the band (soft edges) or held in the middle (a fill).</summary>
        static void Band(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float inner, float outer, float from, float to, bool fill)
        {
            var steps = Mathf.Max(2, Mathf.CeilToInt((to - from) * Mathf.Rad2Deg / 3f));
            var start = vertices.Count;
            for (var i = 0; i <= steps; i++)
            {
                var bearing = Mathf.Lerp(from, to, i / (float)steps);
                vertices.Add(Point(inner, bearing));
                vertices.Add(Point(outer, bearing));
                var u = i / (float)steps;
                uvs.Add(new Vector2(u, fill ? 0.5f : 0f));
                uvs.Add(new Vector2(u, fill ? 0.5f : 1f));
            }
            for (var i = 0; i < steps; i++)
            {
                var a = start + i * 2;
                // Wound to face up (+Y) in the ship's frame.
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
            }
        }

        /// <summary>A straight edge from the least to the greatest range along one bearing.</summary>
        static void Radial(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float inner, float outer, float bearing)
        {
            var across = new Vector3(Mathf.Cos(bearing), 0f, -Mathf.Sin(bearing)) * Edge * 0.5f;
            var a = Point(inner, bearing);
            var b = Point(outer, bearing);
            var start = vertices.Count;
            vertices.Add(a - across); vertices.Add(a + across); vertices.Add(b - across); vertices.Add(b + across);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        public void Destroy()
        {
            foreach (var arc in arcs.Values) if (arc.Mesh != null) Object.Destroy(arc.Mesh);
            arcs.Clear();
            if (holder != null) Object.Destroy(holder.gameObject);
        }
    }
}
