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

        static AegeanArt loaded;
        static bool looked;

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
        public static System.Collections.Generic.IEnumerable<(Mesh mesh, Material material, Matrix4x4 local)> FinestMeshes(GameObject prefab)
        {
            var group = prefab.GetComponentInChildren<LODGroup>();
            System.Collections.Generic.HashSet<Renderer> finest = null;
            if (group != null && group.lodCount > 0)
            {
                finest = new System.Collections.Generic.HashSet<Renderer>();
                foreach (var renderer in group.GetLODs()[0].renderers) if (renderer != null) finest.Add(renderer);
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
