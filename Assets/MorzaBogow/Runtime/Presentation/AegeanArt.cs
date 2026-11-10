using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Finished models of the asset pipeline (Art/Generated, Meshy -> Blender -> Unity) that the battle draws, listed in
    /// one asset in Resources so that every build carries them and the battle scene need not be rebuilt: Attic rocks
    /// among the shore's boulders, fir log stacks at the timber yards. The editor fills it (AegeanArtSet.Refresh); when
    /// it is missing or empty, the shores and yards are drawn as before. Presentation only.
    /// </summary>
    public sealed class AegeanArt : ScriptableObject
    {
        public const string ResourcePath = "AegeanArt";

        /// <summary>Rocks of the region (limestone, marble), each a prefab with its levels of detail.</summary>
        public GameObject[] Rocks = System.Array.Empty<GameObject>();

        /// <summary>Stacks of logs for the timber yards.</summary>
        public GameObject[] TimberStacks = System.Array.Empty<GameObject>();

        /// <summary>Aleppo pines and columnar cypresses for the timber yards, felled as the yard's trees are.</summary>
        public GameObject[] Pines = System.Array.Empty<GameObject>();
        public GameObject[] Cypresses = System.Array.Empty<GameObject>();

        /// <summary>Small props of the ports and sites (baskets of ore, coils of rope, amphorae), found by the start of their name.</summary>
        public GameObject[] Props = System.Array.Empty<GameObject>();

        /// <summary>The first prop whose name starts so; null when the set has none.</summary>
        public GameObject Prop(string prefix)
        {
            if (Props == null) return null;
            foreach (var prop in Props)
                if (prop != null && prop.name.StartsWith(prefix, System.StringComparison.Ordinal)) return prop;
            return null;
        }

        /// <summary>
        /// An instance of a prefab under parent: its finest level's footprint scaled to the given width, turned about the
        /// vertical and stood with its lowest point on foot (in the parent's space).
        /// </summary>
        public static GameObject Place(GameObject prefab, Transform parent, Vector3 foot, float width, float yaw, string name) =>
            Place(prefab, parent, foot, width, Quaternion.Euler(0f, yaw, 0f), name);

        /// <summary>As above, turned any way (a coil of rope laid flat): the turned model's footprint and lowest point count.</summary>
        public static GameObject Place(GameObject prefab, Transform parent, Vector3 foot, float width, Quaternion turn, string name)
        {
            var bounds = FinestBounds(prefab);
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = turn * (bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
            var scale = width / Mathf.Max(0.01f, Mathf.Max(max.x - min.x, max.z - min.z));
            var item = Instantiate(prefab, parent, false);
            item.name = name;
            item.transform.localRotation = turn;
            item.transform.localScale = Vector3.one * scale;
            item.transform.localPosition = foot - new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f) * scale;
            return item;
        }

        static AegeanArt loaded;
        static bool looked;

        /// <summary>-morzaPlainTrees: the plain pine and cypress meshes instead of the set's, to measure what its trees cost.</summary>
        public static readonly bool PlainTrees = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-morzaPlainTrees") >= 0;

        /// <summary>The set in Resources, loaded once; null when there is none.</summary>
        public static AegeanArt Current
        {
            get
            {
                if (!looked || loaded == null)
                {
                    loaded = Resources.Load<AegeanArt>(ResourcePath);
                    looked = true;
                }
                return loaded;
            }
        }

        /// <summary>
        /// The bounds of a prefab's finest level of detail in its own space: what is drawn close up, not every level
        /// at once.
        /// </summary>
        public static Bounds FinestBounds(GameObject prefab)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var (mesh, _, local) in FinestMeshes(prefab))
            {
                var box = mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = local.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        /// <summary>
        /// A prefab's meshes with their material and placement in its space, taking only its finest level of detail when
        /// it has a LODGroup (merging every level draws the model once per level).
        /// </summary>
        public static System.Collections.Generic.IEnumerable<(Mesh mesh, Material material, Matrix4x4 local)> FinestMeshes(GameObject prefab) =>
            LevelMeshes(prefab, 0);

        /// <summary>
        /// As FinestMeshes, at the given level of detail (the last one if the prefab has fewer): a wood merged into one
        /// mesh is drawn at its second level, as seen from the strategy camera.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<(Mesh mesh, Material material, Matrix4x4 local)> LevelMeshes(GameObject prefab, int level)
        {
            var group = prefab.GetComponentInChildren<LODGroup>();
            System.Collections.Generic.HashSet<Renderer> finest = null;
            if (group != null && group.lodCount > 0)
            {
                var lods = group.GetLODs();
                finest = new System.Collections.Generic.HashSet<Renderer>();
                foreach (var renderer in lods[Mathf.Clamp(level, 0, lods.Length - 1)].renderers) if (renderer != null) finest.Add(renderer);
            }
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null) continue;
                if (finest != null && !finest.Contains(renderer)) continue;
                yield return (filter.sharedMesh, renderer.sharedMaterial, prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix);
            }
        }
    }
}
