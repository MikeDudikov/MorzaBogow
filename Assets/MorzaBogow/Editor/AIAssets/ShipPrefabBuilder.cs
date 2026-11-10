using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>Which way an imported model's bow or front points in its own space; Auto takes the longer horizontal axis.</summary>
    public enum ModelForward { PlusZ, MinusZ, PlusX, MinusX, Auto }

    /// <summary>
    /// Turns imported ship models (one per level of detail) into a prefab for a class: a logical root with ShipModel and
    /// its points, and a visual root "Visual" with the LODGroup over "Model", which holds the levels. Unless the model
    /// already comes in the project's frame, the bow is turned to +Z, the model scaled to the class's length (2 x its
    /// radius in the simulation), centred, and sunk so its keel lies at the class's draft below the waterline Y = 0.
    /// Material slots named TeamColor, Sail or Zagiel take the side's colour. No Rigidbody, collider or physics layer:
    /// the simulation moves ships, and a ship is picked by its position.
    /// </summary>
    public static class ShipPrefabBuilder
    {
        /// <summary>Deck height of the stands above the waterline, as the placeholder hull has them; adjust in the prefab.</summary>
        public const float StandHeight = 1.6f;

        public static readonly string[] TeamColourNames = { "teamcolor", "team_color", "teamcolour", "sail", "zagiel", "żagiel" };

        /// <summary>Builds the prefab's object in the scene, not yet saved. The models are copied; the originals are not changed.</summary>
        public static GameObject Assemble(IList<GameObject> lods, IList<float> screenHeights, ShipClass shipClass, ModelForward forward,
                                          bool normalised, IEnumerable<string> teamColour = null, string name = null)
        {
            var definition = ShipCatalog.Get(shipClass);
            var length = (float)definition.Radius * 2f;
            var beam = (float)definition.Beam;
            var draft = (float)definition.Draft;

            var root = new GameObject(name ?? shipClass.ToString());
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var holder = new GameObject("Model").transform;
            holder.SetParent(visual, false);
            var copies = lods.Select((lod, i) => Copy(lod, holder, $"LOD{i}")).ToList();

            if (!normalised)
            {
                holder.localRotation = Turn(forward == ModelForward.Auto ? AutoForward(holder, copies[0]) : forward);
                var bounds = BoundsIn(visual, copies[0]);
                holder.localScale = Vector3.one * (length / Mathf.Max(bounds.size.z, 0.001f));
                bounds = BoundsIn(visual, copies[0]);
                holder.localPosition -= new Vector3(bounds.center.x, bounds.min.y + draft, bounds.center.z);
            }
            LodGroupFor(visual.gameObject, copies, screenHeights);

            var ship = root.AddComponent<ShipModel>();
            var water = new GameObject("Water").transform;
            water.SetParent(root.transform, false);
            ship.Bow = Point(water, "Bow", new Vector3(0f, 0f, length * 0.5f));
            ship.Stern = Point(water, "Stern", new Vector3(0f, 0f, -length * 0.5f));
            ship.Port = Point(water, "Port", new Vector3(-beam * 0.5f, 0f, 0f));
            ship.Starboard = Point(water, "Starboard", new Vector3(beam * 0.5f, 0f, 0f));
            ship.Center = Point(water, "Center", Vector3.zero);

            // A stand and a missile exit for each post, spread along the deck as on the placeholder hull.
            var mounts = new GameObject("Mounts").transform;
            mounts.SetParent(root.transform, false);
            var muzzles = new GameObject("Muzzles").transform;
            muzzles.SetParent(root.transform, false);
            var posts = definition.Slots.Length;
            ship.Mounts = new Transform[posts];
            ship.Muzzles = new Transform[posts];
            for (var slot = 0; slot < posts; slot++)
            {
                var along = posts == 1 ? 0f : Mathf.Lerp(-0.3f, 0.3f, slot / (posts - 1f));
                var stand = new Vector3(0f, StandHeight, along * length);
                ship.Mounts[slot] = Point(mounts, $"Slot{slot}", stand);
                ship.Muzzles[slot] = Point(muzzles, $"Slot{slot}", stand + new Vector3(0f, 0.6f, 0.8f));
            }

            var tokens = (teamColour ?? TeamColourNames).Select(t => t.ToLowerInvariant()).ToArray();
            var renderers = new List<Renderer>();
            var slots = new List<int>();
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i] != null ? materials[i].name.ToLowerInvariant() : string.Empty;
                    if (!tokens.Any(material.Contains)) continue;
                    renderers.Add(renderer);
                    slots.Add(i);
                }
            }
            ship.TeamColourRenderers = renderers.ToArray();
            ship.TeamColourSlots = slots.ToArray();
            return root;
        }

        /// <summary>A copy of a model (a prefab instance when the model is an asset) under the parent.</summary>
        public static GameObject Copy(GameObject model, Transform parent, string name)
        {
            var copy = PrefabUtility.IsPartOfPrefabAsset(model) ? (GameObject)PrefabUtility.InstantiatePrefab(model) : Object.Instantiate(model);
            copy.name = name;
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            return copy;
        }

        /// <summary>One LOD per copy, with the given screen heights; the last one is where the asset is culled.</summary>
        public static LODGroup LodGroupFor(GameObject visual, IList<GameObject> copies, IList<float> screenHeights)
        {
            var group = visual.AddComponent<LODGroup>();
            var lods = new LOD[copies.Count];
            for (var i = 0; i < copies.Count; i++)
            {
                var height = screenHeights != null && screenHeights.Count > 0 ? screenHeights[Mathf.Min(i, screenHeights.Count - 1)] : 0.01f;
                lods[i] = new LOD(height, copies[i].GetComponentsInChildren<Renderer>(true));
            }
            group.SetLODs(lods);
            group.RecalculateBounds();
            return group;
        }

        /// <summary>The longer horizontal axis of the model in its own space taken as the bow's direction (sign unknown: +).</summary>
        public static ModelForward AutoForward(Transform holder, GameObject model)
        {
            var bounds = BoundsIn(holder, model);
            return bounds.size.x > bounds.size.z ? ModelForward.PlusX : ModelForward.PlusZ;
        }

        public static Quaternion Turn(ModelForward forward)
        {
            switch (forward)
            {
                case ModelForward.MinusZ: return Quaternion.Euler(0f, 180f, 0f);
                case ModelForward.PlusX: return Quaternion.Euler(0f, -90f, 0f);
                case ModelForward.MinusX: return Quaternion.Euler(0f, 90f, 0f);
                default: return Quaternion.identity;
            }
        }

        public static ModelForward Parse(string forward)
        {
            switch (forward)
            {
                case "-Z": return ModelForward.MinusZ;
                case "+X": return ModelForward.PlusX;
                case "-X": return ModelForward.MinusX;
                case "auto": return ModelForward.Auto;
                default: return ModelForward.PlusZ;
            }
        }

        public static Transform Point(Transform parent, string name, Vector3 at)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = at;
            return point;
        }

        /// <summary>The model's renderer bounds in the given space.</summary>
        public static Bounds BoundsIn(Transform space, GameObject model) => BoundsOf(space, model.GetComponentsInChildren<Renderer>(true));

        /// <summary>Bounds of renderers' meshes in the given space (mesh boxes transformed corner by corner).</summary>
        public static Bounds BoundsOf(Transform space, IEnumerable<Renderer> renderers)
        {
            var first = true;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.localBounds
                    : renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null ? filter.sharedMesh.bounds : (Bounds?)null;
                if (mesh == null) continue;
                var local = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (var corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    var point = local.MultiplyPoint3x4(mesh.Value.center + Vector3.Scale(mesh.Value.extents, offset));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }
}
