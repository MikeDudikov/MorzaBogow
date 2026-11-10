using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// What stands round a beach slipway of the 12th century BC (E1), beside the existing ramps (ShipyardVisual),
    /// never on them or on the buildings: a stack of logs and split planks, a work shelter roofed with reeds, a rack of
    /// the shipwright's tools, a pot of pitch on its hearthstones and flat stones to prop a hull. Report 5.x: hulls built
    /// shell first with mortise and tenon joints bored with bow drills (Uluburun); the tools of Homer, Od. 5 (axe, adze,
    /// auger); bronze in E1, iron from the Early Iron Age (EnvironmentProfile.IronTools). Sizes are a person's: the
    /// shelter clears a standing man. Presentation only; the shipwrights themselves are the crews' bodies.
    /// </summary>
    public static class ShipyardProps
    {
        public enum Kind { Logs, Planks, Shelter, ToolRack, PitchPot, Stones }

        public readonly struct Placed
        {
            public readonly Kind Kind;
            public readonly Vector3 Position;
            public readonly float Radius;

            public Placed(Kind kind, Vector3 position, float radius)
            {
                Kind = kind;
                Position = position;
                Radius = radius;
            }
        }

        /// <summary>Clear headroom under the shelter's eaves, metres: a standing man works under it.</summary>
        public const float ShelterEaves = 2.0f;

        /// <summary>
        /// Chooses spots for each prop among candidates round the slipways: on dry land, clear of the ramps, the
        /// buildings and each other. Deterministic from the seed.
        /// </summary>
        public static List<Placed> Plan(uint seed, float toward, Func<float, float, float> ground, Func<float, float, bool> clear)
        {
            var kinds = new[] { (Kind.Logs, 2.4f), (Kind.Planks, 1.6f), (Kind.Shelter, 2.4f), (Kind.ToolRack, 1.0f), (Kind.PitchPot, 1.0f), (Kind.Stones, 1.4f) };
            var placed = new List<Placed>();
            var random = new StableHash.Sequence(StableHash.Mix(seed, 0x5A1Fu));
            foreach (var (kind, radius) in kinds)
            {
                for (var attempt = 0; attempt < 60; attempt++)
                {
                    // Near the heads of the ramps, where timber and tools are at hand, on either line's side.
                    var side = random.Next() < 0.5f ? -1f : 1f;
                    var x = toward * random.Range(-2f, 14f);
                    var z = side * random.Range(2f, 16f);
                    var height = ground(x, z);
                    if (height < 0.7f || !clear(x, z)) continue;
                    var spot = new Vector3(x, height, z);
                    var free = true;
                    foreach (var other in placed) free &= (other.Position - spot).sqrMagnitude > (other.Radius + radius) * (other.Radius + radius);
                    if (!free) continue;
                    placed.Add(new Placed(kind, spot, radius));
                    break;
                }
            }
            return placed;
        }

        public static List<Placed> Build(Transform parent, uint seed, float toward, Func<float, float, float> ground, Func<float, float, bool> clear, bool ironTools,
            Material timber, Material thatch, Material stone, Material pitch)
        {
            var plan = Plan(seed, toward, ground, clear);
            var root = new GameObject("Shipyard gear").transform;
            root.SetParent(parent, false);
            var metal = ToolMetal(stone, ironTools);
            foreach (var item in plan)
            {
                var holder = new GameObject(item.Kind.ToString()).transform;
                holder.SetParent(root, false);
                holder.localPosition = item.Position;
                holder.localRotation = Quaternion.Euler(0f, StableHash.Unit(StableHash.Mix(seed, (uint)item.Kind, 3u)) * 360f, 0f);
                switch (item.Kind)
                {
                    case Kind.Logs:
                        // Trunks of pine and fir, barked, stacked three, two, one.
                        for (var row = 0; row < 3; row++)
                        for (var i = 0; i < 3 - row; i++)
                            Part(holder, PrimitiveType.Cylinder, timber, new Vector3((i - (2 - row) * 0.5f) * 0.42f, 0.2f + row * 0.36f, 0f), new Vector3(0.38f, 2.1f, 0.38f), new Vector3(90f, 0f, 0f));
                        break;
                    case Kind.Planks:
                        // Planks split and adzed, laid crosswise to season.
                        for (var layer = 0; layer < 4; layer++)
                            Part(holder, PrimitiveType.Cube, timber, new Vector3(0f, 0.12f + layer * 0.07f, 0f), new Vector3(0.32f, 0.05f, 3.2f), new Vector3(0f, layer % 2 == 0 ? 0f : 6f, 0f));
                        break;
                    case Kind.Shelter:
                        // Four posts and a pitched roof of reeds: shade for the work bench.
                        foreach (var corner in new[] { new Vector2(-1.4f, -1f), new Vector2(1.4f, -1f), new Vector2(-1.4f, 1f), new Vector2(1.4f, 1f) })
                            Part(holder, PrimitiveType.Cylinder, timber, new Vector3(corner.x, ShelterEaves * 0.5f, corner.y), new Vector3(0.14f, ShelterEaves * 0.5f, 0.14f), Vector3.zero);
                        Part(holder, PrimitiveType.Cube, thatch, new Vector3(0f, ShelterEaves + 0.35f, -0.62f), new Vector3(3.4f, 0.12f, 1.5f), new Vector3(-26f, 0f, 0f));
                        Part(holder, PrimitiveType.Cube, thatch, new Vector3(0f, ShelterEaves + 0.35f, 0.62f), new Vector3(3.4f, 0.12f, 1.5f), new Vector3(26f, 0f, 0f));
                        Part(holder, PrimitiveType.Cube, timber, new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.12f, 0.6f), Vector3.zero);
                        break;
                    case Kind.ToolRack:
                        // A rail on two posts with the tools hung on it: axe, adze, chisels, a bow drill.
                        Part(holder, PrimitiveType.Cylinder, timber, new Vector3(-0.7f, 0.55f, 0f), new Vector3(0.08f, 0.55f, 0.08f), Vector3.zero);
                        Part(holder, PrimitiveType.Cylinder, timber, new Vector3(0.7f, 0.55f, 0f), new Vector3(0.08f, 0.55f, 0.08f), Vector3.zero);
                        Part(holder, PrimitiveType.Cylinder, timber, new Vector3(0f, 1.05f, 0f), new Vector3(0.05f, 0.75f, 0.05f), new Vector3(0f, 0f, 90f));
                        for (var t = 0; t < 4; t++)
                        {
                            var x = -0.5f + t * 0.33f;
                            Part(holder, PrimitiveType.Cylinder, timber, new Vector3(x, 0.8f, 0.04f), new Vector3(0.03f, 0.25f, 0.03f), Vector3.zero);
                            Part(holder, PrimitiveType.Cube, metal, new Vector3(x + (t == 1 ? 0.06f : 0f), 0.58f, 0.04f), t == 1 ? new Vector3(0.14f, 0.05f, 0.04f) : new Vector3(0.04f, 0.12f, 0.1f), Vector3.zero);
                        }
                        break;
                    case Kind.PitchPot:
                        // Three hearthstones and a pot of pitch to seal the seams.
                        for (var s = 0; s < 3; s++)
                        {
                            var angle = s * 120f * Mathf.Deg2Rad;
                            Part(holder, PrimitiveType.Cube, stone, new Vector3(Mathf.Cos(angle) * 0.32f, 0.12f, Mathf.Sin(angle) * 0.32f), new Vector3(0.24f, 0.24f, 0.2f), new Vector3(0f, s * 37f, 8f));
                        }
                        Part(holder, PrimitiveType.Sphere, pitch, new Vector3(0f, 0.42f, 0f), new Vector3(0.56f, 0.4f, 0.56f), Vector3.zero);
                        break;
                    case Kind.Stones:
                        // Flat stones to bed the props under a hull.
                        for (var s = 0; s < 5; s++)
                            Part(holder, PrimitiveType.Cube, stone, new Vector3((s % 3 - 1) * 0.6f, 0.1f, (s / 3 - 0.5f) * 0.7f), new Vector3(0.5f, 0.2f, 0.4f), new Vector3(0f, s * 23f, 0f));
                        break;
                }
            }
            return plan;
        }

        /// <summary>Bronze in the 12th century, iron from the Early Iron Age on (a copy of a rock material, values only).</summary>
        public static Material ToolMetal(Material source, bool iron)
        {
            if (source == null) return null;
            var key = iron ? "iron" : "bronze";
            if (metals.TryGetValue((source, key), out var material) && material != null) return material;
            material = new Material(source) { name = $"{source.name} ({key})" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", iron ? new Color(0.36f, 0.35f, 0.34f) : new Color(0.62f, 0.42f, 0.22f));
            foreach (var name in new[] { "_Metallic", "_MetallicRemapMin", "_MetallicRemapMax" }) if (material.HasProperty(name)) material.SetFloat(name, 1f);
            if (material.HasProperty("_SmoothnessRemapMin")) material.SetFloat("_SmoothnessRemapMin", 0.45f);
            if (material.HasProperty("_SmoothnessRemapMax")) material.SetFloat("_SmoothnessRemapMax", 0.7f);
            metals[(source, key)] = material;
            return material;
        }

        static readonly Dictionary<(Material, string), Material> metals = new Dictionary<(Material, string), Material>();

        static void Part(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 euler)
        {
            var item = GameObject.CreatePrimitive(type);
            var collider = item.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(euler);
            item.transform.localScale = scale;
            if (material != null) item.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
