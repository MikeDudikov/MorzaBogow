using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Replaceable placeholder models for work points: a forest yard for wood and a mining quay for silver.
    /// Built from simple shapes. Piles shrink with the remaining stock; the model has no game logic.
    /// </summary>
    public sealed class ResourceSiteVisual : MonoBehaviour
    {
        readonly List<GameObject> pile = new List<GameObject>();
        long startStock;

        public int VisiblePileParts { get; private set; }
        public int TotalPileParts => pile.Count;

        public static ResourceSiteVisual Create(ResourceNode node, PlaceholderMaterials materials)
        {
            var root = new GameObject($"{node.Type} site {node.Id} [PLACEHOLDER]");
            var visual = root.AddComponent<ResourceSiteVisual>();
            visual.startStock = node.Stock;
            root.transform.position = new Vector3((float)node.X, 0f, (float)node.Y);

            // The ship works at the node point. The land sits just beyond it with its jetty toward the ship
            // and its front toward the default camera, so piles stay readable from every site.
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localPosition = new Vector3(0f, 0f, 24f);

            var random = new System.Random(node.Id * 7919);
            if (node.Type == ResourceType.Wood) visual.BuildForestYard(model, materials, random);
            else visual.BuildMiningQuay(model, materials, random);
            visual.UpdateStock(node.Stock);
            return visual;
        }

        void BuildForestYard(Transform model, PlaceholderMaterials materials, System.Random random)
        {
            Part(model, PrimitiveType.Sphere, materials?.Foliage, new Vector3(0f, 0.4f, 4f), new Vector3(34f, 3f, 24f), Vector3.zero);
            Part(model, PrimitiveType.Sphere, materials?.Rock, new Vector3(0f, 0.2f, 4f), new Vector3(38f, 1.6f, 28f), Vector3.zero);
            Jetty(model, materials);

            // Conifers on the far side of the islet.
            for (var i = 0; i < 9; i++)
            {
                var x = -13f + i * 3.2f + (float)random.NextDouble() * 1.5f;
                var z = 8f + (float)random.NextDouble() * 7f;
                var tall = 6f + (float)random.NextDouble() * 4f;
                Part(model, PrimitiveType.Cylinder, materials?.Timber, new Vector3(x, 2f + tall * 0.15f, z), new Vector3(0.5f, tall * 0.3f, 0.5f), Vector3.zero);
                Part(model, PrimitiveType.Capsule, materials?.Foliage, new Vector3(x, 2f + tall * 0.7f, z), new Vector3(2.8f, tall * 0.55f, 2.8f), Vector3.zero);
                Part(model, PrimitiveType.Capsule, materials?.Foliage, new Vector3(x, 2f + tall * 1.05f, z), new Vector3(1.8f, tall * 0.35f, 1.8f), Vector3.zero);
            }

            // Log stacks on the shore: these shrink with the stock.
            for (var stack = -1; stack <= 1; stack += 2)
            {
                for (var layer = 0; layer < 3; layer++)
                {
                    for (var log = 0; log < 4 - layer; log++)
                    {
                        var x = stack * 7f + (log - (3 - layer) * 0.5f) * 1.25f;
                        var position = new Vector3(x, 2.6f + layer * 1.05f, -1f);
                        pile.Add(Part(model, PrimitiveType.Cylinder, materials?.Timber, position, new Vector3(1.15f, 3.4f, 1.15f), new Vector3(90f, 0f, 0f)).gameObject);
                    }
                }
            }
        }

        void BuildMiningQuay(Transform model, PlaceholderMaterials materials, System.Random random)
        {
            Part(model, PrimitiveType.Sphere, materials?.Rock, new Vector3(0f, 0.6f, 5f), new Vector3(36f, 4f, 26f), Vector3.zero);
            for (var i = 0; i < 14; i++)
            {
                var size = 3.5f + (float)random.NextDouble() * 5f;
                var position = new Vector3(-16f + i * 2.5f + (float)random.NextDouble() * 1.5f, 2.2f + size * 0.25f, 11f + (float)random.NextDouble() * 6f);
                var rotation = new Vector3((float)random.NextDouble() * 70f, (float)random.NextDouble() * 180f, (float)random.NextDouble() * 70f);
                Part(model, PrimitiveType.Cube, materials?.Rock, position, new Vector3(size, size * 1.3f, size * 0.85f), rotation);
            }
            Jetty(model, materials);

            // Mine entrance: dark opening in a timber frame.
            Part(model, PrimitiveType.Cube, materials?.MineDark, new Vector3(0f, 4.2f, 7.4f), new Vector3(5f, 5f, 1f), Vector3.zero);
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(-2.9f, 4.2f, 6.8f), new Vector3(0.8f, 5.6f, 0.8f), Vector3.zero);
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(2.9f, 4.2f, 6.8f), new Vector3(0.8f, 5.6f, 0.8f), Vector3.zero);
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 7.2f, 6.8f), new Vector3(7f, 0.8f, 0.9f), Vector3.zero);

            // Silver ore glints on the rock.
            for (var i = 0; i < 12; i++)
            {
                var position = new Vector3(-14f + i * 2.5f, 4f + (float)random.NextDouble() * 5f, 8.5f + (float)random.NextDouble() * 2f);
                var rotation = new Vector3((float)random.NextDouble() * 90f, (float)random.NextDouble() * 90f, 0f);
                var size = 0.7f + (float)random.NextDouble() * 0.9f;
                Part(model, PrimitiveType.Cube, materials?.SilverOre, position, Vector3.one * size, rotation);
            }

            // Sealed chests on the quay: these shrink with the stock.
            for (var row = 0; row < 3; row++)
            {
                for (var i = 0; i < 5 - row; i++)
                {
                    var x = -8f + i * 2.5f + row * 1.25f;
                    var position = new Vector3(x, 3.2f + row * 1.45f, 0.6f);
                    var chest = Part(model, PrimitiveType.Cube, materials?.Timber, position, new Vector3(2.1f, 1.35f, 1.5f), Vector3.zero);
                    var band = Part(chest, PrimitiveType.Cube, materials?.SilverOre, Vector3.zero, new Vector3(0.22f, 1.04f, 1.04f), Vector3.zero);
                    band.localPosition = Vector3.zero;
                    pile.Add(chest.gameObject);
                }
            }
        }

        static void Jetty(Transform model, PlaceholderMaterials materials)
        {
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 1.4f, -9f), new Vector3(5f, 0.4f, 16f), Vector3.zero);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var post = 0; post < 3; post++)
                {
                    Part(model, PrimitiveType.Cylinder, materials?.Timber, new Vector3(side * 2.4f, 0.8f, -15f + post * 6f), new Vector3(0.45f, 1.5f, 0.45f), Vector3.zero);
                }
            }
        }

        /// <summary>Shows a share of the pile matching the remaining stock. An empty point shows none.</summary>
        public void UpdateStock(long stock)
        {
            var fraction = startStock > 0 ? (double)stock / startStock : 0.0;
            var shown = stock <= 0 ? 0 : System.Math.Max(1, (int)System.Math.Ceiling(fraction * pile.Count));
            if (shown == VisiblePileParts && shown != 0) return;
            VisiblePileParts = shown;
            for (var i = 0; i < pile.Count; i++)
            {
                var active = i < shown;
                if (pile[i].activeSelf != active) pile[i].SetActive(active);
            }
        }

        static Transform Part(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(rotation);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
    }
}
