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

        /// <param name="landOutline">The islet's waterline from the game; null uses the plain ellipse.</param>
        public static ResourceSiteVisual Create(ResourceNode node, PlaceholderMaterials materials, double[] landOutline = null)
        {
            var root = new GameObject($"{node.Type} site {node.Id} [PLACEHOLDER]");
            var visual = root.AddComponent<ResourceSiteVisual>();
            visual.startStock = node.Stock;
            root.transform.position = new Vector3((float)node.X, 0f, (float)node.Y);

            // The ship works at the node point. The land sits just beyond it with its jetty toward the ship
            // and its front toward the default camera, so piles stay readable from every site.
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localPosition = new Vector3(0f, 0f, (float)Rules.SiteLandOffset - IsletZ);
            visual.modelRoot = model;
            visual.Kind = node.Type;
            visual.outline = System.Array.ConvertAll(landOutline ?? LandShape.Ellipse(Rules.SiteIsletHalfWidth, Rules.SiteIsletHalfDepth), value => (float)value);
            CurrentMaterials = materials;

            var random = new System.Random(node.Id * 7919);
            if (node.Type == ResourceType.Wood) visual.BuildForestYard(model, materials, random);
            else visual.BuildMiningQuay(model, materials, random);
            visual.UpdateStock(node.Stock);
            return visual;
        }

        /// <summary>Islet waterline in model space; the land's centre sits at model z = IsletZ.</summary>
        const float IsletZ = 4f;
        float[] outline;

        /// <summary>Ground under a model point, following the islet's gentle mound.</summary>
        float Ground(float x, float z) => ShoreShape.GroundAt(outline, x, z - IsletZ);

        Transform modelRoot;
        readonly List<Transform> trees = new List<Transform>();
        readonly List<float> treeFall = new List<float>();
        readonly List<float> treeRegrow = new List<float>();
        readonly List<Vector3> workSpots = new List<Vector3>();
        Vector3 landing, jettyEnd, stackSpot;

        public ResourceType Kind { get; private set; }

        /// <summary>End of the jetty over the berth, in world space: where crews step from the hull.</summary>
        public Vector3 JettyEnd => modelRoot.TransformPoint(jettyEnd);

        /// <summary>Where the jetty meets the land.</summary>
        public Vector3 Landing => modelRoot.TransformPoint(landing);

        /// <summary>Where harvested goods are carried to before loading: the log stacks or the chest quay.</summary>
        public Vector3 StackSpot => modelRoot.TransformPoint(stackSpot);

        public int WorkSpotCount => workSpots.Count;

        /// <summary>A tree to fell or a rock face to work, in world space, standing beside it.</summary>
        public Vector3 WorkSpot(int index) => modelRoot.TransformPoint(workSpots[(index % workSpots.Count + workSpots.Count) % workSpots.Count]);

        /// <summary>Starts felling the tree at a work spot; it leans, falls and regrows later. Wood only.</summary>
        public void Fell(int index, float progress)
        {
            if (trees.Count == 0) return;
            var tree = (index % trees.Count + trees.Count) % trees.Count;
            treeFall[tree] = Mathf.Max(treeFall[tree], Mathf.Clamp01(progress));
            if (treeFall[tree] >= 1f && treeRegrow[tree] <= 0f) treeRegrow[tree] = 25f;
        }

        public bool IsFelled(int index) => trees.Count > 0 && treeFall[(index % trees.Count + trees.Count) % trees.Count] >= 1f;

        /// <summary>Felled trees lie a while, then a young tree grows back. Presentation only, in game time.</summary>
        public void Tick(float delta)
        {
            for (var i = 0; i < trees.Count; i++)
            {
                if (treeRegrow[i] > 0f)
                {
                    treeRegrow[i] -= delta;
                    if (treeRegrow[i] <= 0f) treeFall[i] = 0f;
                }
                var fall = treeFall[i];

                // Leans slowly while cut, then drops quickly; lying trees sink out of sight before regrowing.
                var angle = fall < 0.8f ? fall / 0.8f * 12f : Mathf.Lerp(12f, 88f, (fall - 0.8f) / 0.2f);
                trees[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
                var gone = treeRegrow[i] > 0f && treeRegrow[i] < 8f;
                trees[i].localScale = gone ? Vector3.one * Mathf.Max(0.05f, 1f - (8f - treeRegrow[i]) / 8f) : Vector3.one;
            }
        }

        void BuildIslet(Transform model, Material land)
        {
            var islet = new GameObject("Islet");
            islet.transform.SetParent(model, false);
            islet.transform.localPosition = new Vector3(0f, 0f, IsletZ);
            islet.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Mound(outline);
            var renderer = islet.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CurrentMaterials?.ShoreRock, land };
        }

        static PlaceholderMaterials CurrentMaterials;

        void BuildForestYard(Transform model, PlaceholderMaterials materials, System.Random random)
        {
            BuildIslet(model, materials?.Ground(materials.Foliage));
            Jetty(model, materials);
            stackSpot = new Vector3(0f, Ground(0f, -1f), -3f);

            // Conifers on the far side of the islet, each a group that can be felled at its foot.
            for (var i = 0; i < 9; i++)
            {
                var x = -13f + i * 3.2f + (float)random.NextDouble() * 1.5f;
                var z = 7f + (float)random.NextDouble() * 6f;
                var tall = 6f + (float)random.NextDouble() * 4f;
                var tree = new GameObject("Tree").transform;
                tree.SetParent(model, false);
                tree.localPosition = new Vector3(x, Ground(x, z) - 0.1f, z);
                Part(tree, PrimitiveType.Cylinder, materials?.Timber, new Vector3(0f, tall * 0.15f, 0f), new Vector3(0.5f, tall * 0.3f, 0.5f), Vector3.zero);
                Part(tree, PrimitiveType.Capsule, materials?.Foliage, new Vector3(0f, tall * 0.7f, 0f), new Vector3(2.8f, tall * 0.55f, 2.8f), Vector3.zero);
                Part(tree, PrimitiveType.Capsule, materials?.Foliage, new Vector3(0f, tall * 1.05f, 0f), new Vector3(1.8f, tall * 0.35f, 1.8f), Vector3.zero);
                trees.Add(tree);
                treeFall.Add(0f);
                treeRegrow.Add(0f);
                workSpots.Add(new Vector3(x, Ground(x, z - 1.6f), z - 1.6f));
            }

            // Log stacks on the shore: these shrink with the stock.
            for (var stack = -1; stack <= 1; stack += 2)
            {
                for (var layer = 0; layer < 3; layer++)
                {
                    for (var log = 0; log < 4 - layer; log++)
                    {
                        var x = stack * 7f + (log - (3 - layer) * 0.5f) * 1.25f;
                        var position = new Vector3(x, Ground(x, -1f) + 0.55f + layer * 1.05f, -1f);
                        pile.Add(Part(model, PrimitiveType.Cylinder, materials?.Timber, position, new Vector3(1.15f, 3.4f, 1.15f), new Vector3(90f, 0f, 0f)).gameObject);
                    }
                }
            }
        }

        void BuildMiningQuay(Transform model, PlaceholderMaterials materials, System.Random random)
        {
            BuildIslet(model, materials?.Rock);
            for (var i = 0; i < 14; i++)
            {
                var size = 3.5f + (float)random.NextDouble() * 5f;
                var x = -14f + i * 2.1f + (float)random.NextDouble() * 1.5f;
                var z = 9f + (float)random.NextDouble() * 4f;
                var position = new Vector3(x, Ground(x, z) + size * 0.3f, z);
                var rotation = new Vector3((float)random.NextDouble() * 70f, (float)random.NextDouble() * 180f, (float)random.NextDouble() * 70f);
                Part(model, PrimitiveType.Cube, materials?.Rock, position, new Vector3(size, size * 1.3f, size * 0.85f), rotation);
            }
            Jetty(model, materials);
            stackSpot = new Vector3(0f, Ground(0f, 0.6f), -1.5f);

            // Mine entrance: dark opening in a timber frame, set into the rock.
            var foot = Ground(0f, 7.4f);
            Part(model, PrimitiveType.Cube, materials?.MineDark, new Vector3(0f, foot + 2.4f, 7.4f), new Vector3(5f, 5f, 1f), Vector3.zero);
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(-2.9f, foot + 2.4f, 6.8f), new Vector3(0.8f, 5.6f, 0.8f), Vector3.zero);
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(2.9f, foot + 2.4f, 6.8f), new Vector3(0.8f, 5.6f, 0.8f), Vector3.zero);
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, foot + 5.4f, 6.8f), new Vector3(7f, 0.8f, 0.9f), Vector3.zero);

            // Miners work the mine mouth and the ore-bearing faces either side of it.
            foreach (var x in new[] { 0f, -6f, 6f, -10f, 10f })
            {
                workSpots.Add(new Vector3(x, Ground(x, 5.6f), 5.6f));
            }

            // Silver ore glints on the rock.
            for (var i = 0; i < 12; i++)
            {
                var x = -14f + i * 2.5f;
                var z = 8.5f + (float)random.NextDouble() * 2f;
                var position = new Vector3(x, Ground(x, z) + 2f + (float)random.NextDouble() * 4f, z);
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
                    var position = new Vector3(x, Ground(x, 0.6f) + 0.7f + row * 1.45f, 0.6f);
                    var chest = Part(model, PrimitiveType.Cube, materials?.Timber, position, new Vector3(2.1f, 1.35f, 1.5f), Vector3.zero);
                    var band = Part(chest, PrimitiveType.Cube, materials?.SilverOre, Vector3.zero, new Vector3(0.22f, 1.04f, 1.04f), Vector3.zero);
                    band.localPosition = Vector3.zero;
                    pile.Add(chest.gameObject);
                }
            }
        }

        /// <summary>The jetty runs from the beach out to the berth, so a moored hull lies alongside its end.</summary>
        void Jetty(Transform model, PlaceholderMaterials materials)
        {
            const float Shore = -7f;
            var end = -(float)Rules.SiteLandOffset + 8f;
            var length = Shore - end;
            Part(model, PrimitiveType.Cube, materials?.Timber, new Vector3(0f, 1.2f, (Shore + end) * 0.5f), new Vector3(4f, 0.35f, length), Vector3.zero);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var post = 0; post < 4; post++)
                {
                    var z = Mathf.Lerp(end + 0.5f, Shore - 1f, post / 3f);
                    Part(model, PrimitiveType.Cylinder, materials?.Timber, new Vector3(side * 1.9f, 0f, z), new Vector3(0.4f, 1.6f, 0.4f), Vector3.zero);
                }
            }
            jettyEnd = new Vector3(0f, 1.4f, end + 0.6f);
            landing = new Vector3(0f, Mathf.Max(1.4f, Ground(0f, Shore + 1f)), Shore + 1.5f);
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
