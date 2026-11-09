using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>Clearly marked placeholder materials supplied by the scene. Null values use the default material.</summary>
    [System.Serializable]
    public sealed class PlaceholderMaterials
    {
        public Material Wood;
        public Material Bronze;
        public Material PlayerSail;
        public Material EnemySail;
        public Material Neutral;
        public Material BronzeNode;
        public Material RationsNode;
        public Material Selection;
    }

    /// <summary>
    /// Replaceable visual model. The root follows simulation data; named mount points keep costs, damage
    /// and controls independent from whichever art later replaces this placeholder.
    /// </summary>
    public sealed class ShipVisual : MonoBehaviour
    {
        public int ShipId { get; private set; }
        public GameObject Selection { get; private set; }

        public static ShipVisual Create(Ship ship, PlaceholderMaterials materials)
        {
            var root = new GameObject($"Ship {ship.Id} {ship.Class} [PLACEHOLDER]");
            var visual = root.AddComponent<ShipVisual>();
            visual.ShipId = ship.Id;
            var definition = ship.Definition;
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            var length = (float)definition.Radius * 2f;
            var beam = Mathf.Max(2f, length * (ship.Class == ShipClass.Boarder ? 0.42f : 0.28f));
            var hull = Part(model, PrimitiveType.Capsule, "Hull", materials?.Wood);
            hull.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hull.localScale = new Vector3(beam, length * 0.5f, beam * 0.6f);
            hull.localPosition = new Vector3(0f, 0.6f, 0f);

            var mast = Part(model, PrimitiveType.Cylinder, "Mast", materials?.Wood);
            mast.localScale = new Vector3(0.5f, length * 0.28f, 0.5f);
            mast.localPosition = new Vector3(0f, length * 0.3f, 0f);

            visual.ApplyOwner(ship.Owner, materials);

            for (var side = -1; side <= 1; side += 2)
            {
                var oars = Part(model, PrimitiveType.Cube, side < 0 ? "Oars Left" : "Oars Right", materials?.Wood);
                oars.localScale = new Vector3(beam * 0.9f, 0.15f, length * 0.55f);
                oars.localPosition = new Vector3(side * beam * 0.7f, 0.4f, 0f);
            }

            if (ship.Class == ShipClass.Ram)
            {
                var ram = Part(model, PrimitiveType.Cube, "Ram", materials?.Bronze);
                ram.localScale = new Vector3(0.7f, 0.7f, length * 0.25f);
                ram.localPosition = new Vector3(0f, 0.3f, length * 0.58f);
            }

            for (var slot = 0; slot < definition.LightSlots; slot++)
            {
                var mount = new GameObject($"Mount {slot}").transform;
                mount.SetParent(model, false);
                var offset = definition.LightSlots == 1 ? 0f : Mathf.Lerp(-0.3f, 0.3f, slot / (definition.LightSlots - 1f));
                mount.localPosition = new Vector3(0f, 1.6f, offset * length);
                var weapon = Part(mount, PrimitiveType.Cube, "Weapon", materials?.Bronze);
                weapon.localScale = new Vector3(1.4f, 1.0f, 1.4f);
            }

            var selection = Part(root.transform, PrimitiveType.Cylinder, "Selection", materials?.Selection);
            selection.localScale = new Vector3(length * 1.3f, 0.05f, length * 1.3f);
            selection.localPosition = new Vector3(0f, 0.25f, 0f);
            visual.Selection = selection.gameObject;
            visual.Selection.SetActive(false);
            return visual;
        }

        /// <summary>Called when boarding changes the owner: only the sail changes.</summary>
        public void ApplyOwner(int owner, PlaceholderMaterials materials)
        {
            var existing = transform.Find("Model/Sail");
            if (existing != null) { existing.SetParent(null); Destroy(existing.gameObject); }
            var length = GetComponentInChildren<CapsuleCollider>() == null ? 0f : 0f;
            var model = transform.Find("Model");
            var hull = model.Find("Hull");
            var size = hull.localScale.y * 2f + length;
            var sail = Part(model, PrimitiveType.Cube, "Sail", owner == 0 ? materials?.PlayerSail : materials?.EnemySail);
            sail.localScale = new Vector3(size * 0.55f, size * 0.4f, 0.2f);
            sail.localPosition = new Vector3(0f, size * 0.4f, 0f);
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            DestroyCollider(part);
            part.transform.SetParent(parent, false);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static void DestroyCollider(GameObject part)
        {
            var collider = part.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        }
    }
}
