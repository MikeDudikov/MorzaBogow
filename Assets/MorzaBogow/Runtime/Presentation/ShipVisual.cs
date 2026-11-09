using System.Collections.Generic;
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
        public Material WoodNode;
        public Material SilverNode;
        public Material Selection;
        public Material Foam;
        public Material Arrow;
        public Material Flash;
        public Material Rope;
        public Material OrderLine;
        public Material WreckMarker;
    }

    /// <summary>
    /// Replaceable visual model. The root follows simulation data; named mount points keep costs, damage
    /// and controls independent from whichever art later replaces this placeholder. All animation here
    /// is presentation only and never feeds back into the simulation.
    /// </summary>
    public sealed class ShipVisual : MonoBehaviour
    {
        readonly List<(Transform pivot, int side, float offset)> oars = new List<(Transform, int, float)>();
        Transform model;
        Transform sail;
        Transform wake;
        float size;
        float rowing;
        float seed;

        public int ShipId { get; private set; }
        public GameObject Selection { get; private set; }

        public static ShipVisual Create(Ship ship, PlaceholderMaterials materials)
        {
            var root = new GameObject($"Ship {ship.Id} {ship.Class} [PLACEHOLDER]");
            var visual = root.AddComponent<ShipVisual>();
            visual.ShipId = ship.Id;
            visual.seed = ship.Id * 1.37f;
            var definition = ship.Definition;
            visual.model = new GameObject("Model").transform;
            visual.model.SetParent(root.transform, false);

            var length = (float)definition.Radius * 2f;
            visual.size = length;
            var beam = Mathf.Max(2f, length * (ship.Class == ShipClass.Boarder ? 0.42f : 0.28f));
            var hull = Part(visual.model, PrimitiveType.Capsule, "Hull", materials?.Wood);
            hull.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hull.localScale = new Vector3(beam, length * 0.5f, beam * 0.6f);
            hull.localPosition = new Vector3(0f, 0.6f, 0f);

            var mast = Part(visual.model, PrimitiveType.Cylinder, "Mast", materials?.Wood);
            mast.localScale = new Vector3(0.5f, length * 0.28f, 0.5f);
            mast.localPosition = new Vector3(0f, length * 0.3f, 0f);

            visual.ApplyOwner(ship.Owner, materials);

            var oarCount = ship.Class == ShipClass.Cutter ? 3 : 6;
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < oarCount; i++)
                {
                    var along = oarCount == 1 ? 0f : Mathf.Lerp(-0.32f, 0.32f, i / (oarCount - 1f));
                    var pivot = new GameObject("Oar").transform;
                    pivot.SetParent(visual.model, false);
                    pivot.localPosition = new Vector3(side * beam * 0.45f, 0.9f, along * length);
                    var blade = Part(pivot, PrimitiveType.Cube, "Blade", materials?.Wood);
                    var reach = Mathf.Max(2.5f, length * 0.28f);
                    blade.localScale = new Vector3(reach, 0.14f, 0.3f);
                    blade.localPosition = new Vector3(side * reach * 0.5f, 0f, 0f);
                    visual.oars.Add((pivot, side, i * 0.35f));
                }
            }

            if (ship.Class == ShipClass.Ram)
            {
                var ram = Part(visual.model, PrimitiveType.Cube, "Ram", materials?.Bronze);
                ram.localScale = new Vector3(0.7f, 0.7f, length * 0.25f);
                ram.localPosition = new Vector3(0f, 0.3f, length * 0.58f);
            }

            for (var slot = 0; slot < definition.LightSlots; slot++)
            {
                var mount = new GameObject($"Mount {slot}").transform;
                mount.SetParent(visual.model, false);
                var offset = definition.LightSlots == 1 ? 0f : Mathf.Lerp(-0.3f, 0.3f, slot / (definition.LightSlots - 1f));
                mount.localPosition = new Vector3(0f, 1.6f, offset * length);
                var weapon = Part(mount, PrimitiveType.Cube, "Weapon", materials?.Bronze);
                weapon.localScale = new Vector3(1.4f, 1.0f, 1.4f);
            }

            visual.wake = Part(root.transform, PrimitiveType.Cube, "Wake", materials?.Foam);
            visual.wake.gameObject.SetActive(false);

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
            if (sail != null) { sail.SetParent(null); Destroy(sail.gameObject); }
            sail = Part(model, PrimitiveType.Cube, "Sail", owner == 0 ? materials?.PlayerSail : materials?.EnemySail);
            sail.localScale = new Vector3(size * 0.55f, size * 0.4f, 0.2f);
            sail.localPosition = new Vector3(0f, size * 0.4f, 0f);
        }

        /// <summary>Oars, swell, sail and wake follow current speed. Time here is real time, not simulation time.</summary>
        public void Animate(float speedFraction, float time, float delta)
        {
            var fraction = Mathf.Clamp01(Mathf.Abs(speedFraction));
            rowing += delta * (1.2f + fraction * 3.2f);

            foreach (var (pivot, side, offset) in oars)
            {
                var stroke = Mathf.Sin(rowing + offset);
                var lift = Mathf.Cos(rowing + offset);
                var strength = Mathf.Lerp(0.12f, 1f, fraction);
                pivot.localRotation = Quaternion.Euler(0f, side * stroke * 24f * strength, side * (-14f + lift * 9f * strength));
            }

            var wave = time * 0.9f + seed;
            model.localPosition = new Vector3(0f, 0.35f + Mathf.Sin(wave) * 0.22f, 0f);
            model.localRotation = Quaternion.Euler(Mathf.Sin(wave * 0.8f) * 1.6f, 0f, Mathf.Cos(wave * 0.6f) * 2.2f);

            if (sail != null)
            {
                var billow = 0.2f + fraction * 1.1f + Mathf.Sin(time * 1.7f + seed) * 0.12f;
                sail.localScale = new Vector3(size * 0.55f, size * 0.4f, billow);
                sail.localPosition = new Vector3(0f, size * 0.4f, billow * 0.4f);
            }

            var showWake = fraction > 0.08f;
            if (wake.gameObject.activeSelf != showWake) wake.gameObject.SetActive(showWake);
            if (!showWake) return;
            var length = size * (0.7f + fraction * 1.6f);
            wake.localScale = new Vector3(size * (0.18f + fraction * 0.22f), 0.04f, length);
            wake.localPosition = new Vector3(0f, 0.45f, -size * 0.45f - length * 0.5f);
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
