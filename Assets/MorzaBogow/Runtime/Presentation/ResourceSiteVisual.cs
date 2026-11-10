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
            // A low landing at the jetty toward the berth; cliffs rise on the far side, where the trees or the mine stand.
            visual.lift = ShoreShape.Cliffs(-Mathf.PI / 2f, 70f, 125f, node.Id * 0.73f);
            CurrentMaterials = materials;

            var random = new System.Random(node.Id * 7919);
            if (node.Type == ResourceType.Wood) visual.BuildForestYard(model, materials, random);
            else visual.BuildMiningQuay(model, materials, random);
            visual.UpdateStock(node.Stock);
            return visual;
        }

        /// <summary>Islet waterline in model space; the land's centre sits at model z = IsletZ.</summary>
        const float IsletZ = 4f;
        float[] outline, lift;

        /// <summary>Ground under a model point, following the islet's mound and cliffs.</summary>
        float Ground(float x, float z) => ShoreShape.GroundAt(outline, lift, x, z - IsletZ);

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
            islet.AddComponent<MeshFilter>().sharedMesh = ShoreShape.Mound(outline, lift);
            var renderer = islet.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { CurrentMaterials?.ShoreRock, land };
            Landscape.CoastFeatures.Build(islet.transform, outline, lift, (uint)Mathf.RoundToInt(model.position.x * 23f + model.position.z * 11f), CurrentMaterials?.ShoreRock,
                Landscape.LandscapeArt.Wet(CurrentMaterials?.ShoreRock), Landscape.LandscapeArt.Beachrock(CurrentMaterials?.Floor));
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
                var tree = new GameObject("Tree").transform;
                tree.SetParent(model, false);
                tree.localPosition = new Vector3(x, Ground(x, z) - 0.1f, z);

                // Cypresses and pines; each leans and falls as one when felled. The pipeline's Aleppo pines and cypresses
                // where the art set has them (AegeanArt), as tall as the plain ones; the plain meshes otherwise.
                var cypress = i % 3 != 1;
                var shape = new GameObject("Shape").transform;
                shape.SetParent(tree, false);
                shape.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                var scale = TreeModels.LifeSize * 0.85f * (cypress ? 0.75f + (float)random.NextDouble() * 0.2f : 0.55f + (float)random.NextDouble() * 0.15f);
                var drawn = YardTree(cypress, i);
                if (drawn != null)
                {
                    var bounds = AegeanArt.FinestBounds(drawn);
                    var height = (cypress ? 11f : 8.75f) * scale;
                    var item = Object.Instantiate(drawn, shape, false);
                    item.name = cypress ? "Cypress" : "Pine";
                    item.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, bounds.size.y));
                    item.transform.localPosition = Vector3.down * (bounds.min.y * item.transform.localScale.y);
                }
                else
                {
                    shape.localScale = Vector3.one * scale;
                    shape.gameObject.AddComponent<MeshFilter>().sharedMesh = TreeModels.Get(cypress ? TreeKind.Cypress : TreeKind.StonePine, i);
                    shape.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials?.Timber, materials?.Foliage };
                }
                trees.Add(tree);
                treeFall.Add(0f);
                treeRegrow.Add(0f);
                workSpots.Add(new Vector3(x, Ground(x, z - 1.6f), z - 1.6f));
            }

            // Log stacks on the shore: these shrink with the stock. The pipeline's fir logs where the art set has them
            // (AegeanArt): three stacks side by side and one on top in each pile, the top one going first.
            var art = AegeanArt.Current;
            var logs = art != null && art.TimberStacks != null && art.TimberStacks.Length > 0 ? art.TimberStacks[0] : null;
            if (logs != null)
            {
                for (var stack = -1; stack <= 1; stack += 2)
                {
                    foreach (var (across, layer) in new[] { (-1, 0), (0, 0), (1, 0), (0, 1) })
                        pile.Add(LogStack(model, logs, stack * 7f, across, layer, random));
                }
                return;
            }
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
            // Bare pale limestone, as on the Aegean mining islets; the dark ore-bearing blocks stand out on it.
            BuildIslet(model, materials?.ShoreRock ?? materials?.Rock);
            // The ore-bearing rock behind the mine: the pipeline's rocks darkened to schist where the art set has them
            // (AegeanArt), plain dark cubes otherwise; the same draws either way, so all that follows is placed alike.
            var art = AegeanArt.Current;
            var rocks = art != null && art.Rocks != null && art.Rocks.Length > 0 ? art.Rocks : null;
            for (var i = 0; i < 14; i++)
            {
                var size = 3.5f + (float)random.NextDouble() * 5f;
                var x = -14f + i * 2.1f + (float)random.NextDouble() * 1.5f;
                var z = 9f + (float)random.NextDouble() * 4f;
                var position = new Vector3(x, Ground(x, z) + size * 0.3f, z);
                var rotation = new Vector3((float)random.NextDouble() * 70f, (float)random.NextDouble() * 180f, (float)random.NextDouble() * 70f);
                if (rocks != null) SchistRock(model, rocks[i % rocks.Length], new Vector3(x, Ground(x, z), z), size, rotation.y * 2f, rotation.x * 0.15f);
                else Part(model, PrimitiveType.Cube, materials?.Rock, position, new Vector3(size, size * 1.3f, size * 0.85f), rotation);
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

            // The ore-bearing outcrop either side of the mine: marble under schist, galena and its white crust in the
            // rusty contact between them, worked in shallow pits (Laurion type, 12th century BC; Presentation.Landscape).
            var rock = CurrentMaterials?.ShoreRock;
            var outcropSeed = (uint)Mathf.RoundToInt(model.position.x * 17f + model.position.z * 5f) ^ 0x0AEu;
            // Two stretches either side of the mine mouth, just behind where the miners stand (z 5.6), in front of the rock.
            foreach (var centre in new[] { -9.3f, 9.3f })
            {
                var outcrop = new GameObject("Outcrop").transform;
                outcrop.SetParent(model, false);
                outcrop.localPosition = new Vector3(centre, 0f, 0f);
                var shift = centre;
                Landscape.OreOutcrop.Build(outcrop, outcropSeed + (uint)(centre > 0f ? 1 : 0), 11.5f, 6.4f, (x, z) => Ground(x + shift, z), rock, Landscape.LandscapeArt.Limonite(rock),
                    Landscape.LandscapeArt.Schist(rock), Landscape.LandscapeArt.Galena(rock), Landscape.LandscapeArt.Cerussite(rock), materials?.MineDark);
            }
            // The ore broken and sorted by hand into a heap by the mine: it shrinks with the deposit's stock.
            sortedOre = Landscape.OreOutcrop.Attach(model, Landscape.OreOutcrop.SortedHeap(outcropSeed, 60), Landscape.LandscapeArt.Limonite(rock));
            if (sortedOre != null) sortedOre.localPosition = new Vector3(3.2f, Ground(3.2f, 3.6f) - 0.05f, 3.6f);

            // Wild olives of the maquis cling to the flanks of the rock, clear of the mine and the quay: dry hillside,
            // not wood. Each is a tree of its own whose bones the match's wind bends (Presentation.Landscape).
            var grove = new Landscape.TreeGroveBuilder();
            var leeward = model.InverseTransformDirection(Landscape.Vegetation.Leeward);
            var siteId = (uint)Mathf.RoundToInt(model.position.x * 13f + model.position.z * 7f) ^ 0x51A7u;
            for (var i = 0; i < 7; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var x = side * (12.5f + (float)random.NextDouble() * 4.5f);
                var z = -2f + (float)random.NextDouble() * 9f;
                if (ShoreShape.GroundAt(outline, lift, x, z - IsletZ) < 0.4f) continue;
                random.NextDouble();
                var yaw = (float)random.NextDouble() * 360f;
                grove.Add(Landscape.TreeSpecies.OliveWild, Landscape.StableHash.Mix(siteId, (uint)i), new Vector3(x, Ground(x, z) - 0.15f, z), yaw, leeward, 1f);
            }
            if (!Landscape.Vegetation.PlainOlives) grove.Build(model, "Olives", Landscape.LandscapeArt.OliveBark ?? materials?.Timber, Landscape.LandscapeArt.OliveLeaves ?? materials?.OliveFoliage);
            else PlainOlives(model, materials);
            BuildWashery(model, materials);
            BuildChests(model, materials);
        }

        /// <summary>
        /// The ore workshop of the 4th century BC beside the quay (R-LAURION-WORKSHOPS): ground ore washed with recycled
        /// water on a flat-washing platform of hydraulic plaster below its stand tank, the water running off along a
        /// channel into settling tanks at both ends; a large open cistern up the slope, and stone mortars for crushing by
        /// the heap. A picture only, built once: the deposit's stock shows in the heap and the chests.
        /// </summary>
        void BuildWashery(Transform model, PlaceholderMaterials materials)
        {
            var b = new GreekShips.MeshBuilder(null) { Level = true };
            const float cx = 7.4f, cz = 0.4f, w = 6f, d = 3.6f;
            var g = Ground(cx, cz);
            // The platform on its footing, and the washing table sloping gently toward the channel in front.
            b.Box(GreekMaterial.Stone, new Vector3(cx, g - 0.6f, cz), new Vector3(w * 0.5f, 0.75f, d * 0.5f), Quaternion.identity);
            b.Box(GreekMaterial.Paint, new Vector3(cx, g + 0.2f, cz + 0.1f), new Vector3(w * 0.5f - 0.3f, 0.06f, d * 0.5f - 0.65f), Quaternion.Euler(-3f, 0f, 0f));
            // The stand tank along the back, its water, and the holes from which it runs onto the table.
            var tank = cz + d * 0.5f - 0.35f;
            b.Box(GreekMaterial.Stone, new Vector3(cx, g + 0.6f, tank + 0.27f), new Vector3(w * 0.5f, 0.5f, 0.08f), Quaternion.identity);
            b.Box(GreekMaterial.Paint, new Vector3(cx, g + 0.5f, tank - 0.27f), new Vector3(w * 0.5f, 0.4f, 0.08f), Quaternion.identity);
            foreach (var side in new[] { -1f, 1f })
                b.Box(GreekMaterial.Stone, new Vector3(cx + side * (w * 0.5f - 0.08f), g + 0.6f, tank), new Vector3(0.08f, 0.5f, 0.35f), Quaternion.identity);
            b.Box(GreekMaterial.Azure, new Vector3(cx, g + 0.86f, tank), new Vector3(w * 0.5f - 0.16f, 0.01f, 0.19f), Quaternion.identity);
            for (var i = 0; i < 5; i++)
                b.Box(GreekMaterial.Pitch, new Vector3(cx - w * 0.4f + i * w * 0.2f, g + 0.24f, tank - 0.36f), new Vector3(0.06f, 0.05f, 0.02f), Quaternion.identity);
            // The channel along the front, and a settling tank at either end of it.
            var front = cz - d * 0.5f + 0.25f;
            b.Box(GreekMaterial.Paint, new Vector3(cx, g + 0.12f, front - 0.17f), new Vector3(w * 0.5f, 0.12f, 0.06f), Quaternion.identity);
            b.Box(GreekMaterial.Azure, new Vector3(cx, g + 0.09f, front), new Vector3(w * 0.5f - 0.1f, 0.01f, 0.1f), Quaternion.identity);
            foreach (var side in new[] { -1f, 1f })
            {
                var bx = cx + side * (w * 0.5f + 0.55f);
                var basin = new Vector3(bx, Ground(bx, front + 0.3f), front + 0.3f);
                foreach (var end in new[] { -1f, 1f })
                {
                    b.Box(GreekMaterial.Stone, basin + new Vector3(0f, 0.2f, end * 0.45f), new Vector3(0.5f, 0.3f, 0.06f), Quaternion.identity);
                    b.Box(GreekMaterial.Stone, basin + new Vector3(end * 0.45f, 0.2f, 0f), new Vector3(0.06f, 0.3f, 0.4f), Quaternion.identity);
                }
                b.Box(GreekMaterial.Azure, basin + new Vector3(0f, 0.38f, 0f), new Vector3(0.4f, 0.01f, 0.4f), Quaternion.identity);
            }
            // The open cistern up the slope: a round wall, and its water near the brim.
            var cistern = new Vector3(10.2f, Ground(10.2f, 4.6f), 4.6f);
            b.Lathe(GreekMaterial.Stone, new[] { (-0.6f, 1.9f), (0.5f, 1.9f), (0.56f, 1.72f) }, 24, cistern);
            b.Lathe(GreekMaterial.Azure, new[] { (0.42f, 1.72f), (0.44f, 0f) }, 24, cistern);
            // Stone mortars for crushing, by the heap of sorted ore.
            foreach (var (x, z) in new[] { (5.3f, 4.3f), (6.2f, 3.4f) })
                b.Lathe(GreekMaterial.Stone, new[] { (-0.2f, 0.42f), (0.45f, 0.4f), (0.5f, 0.3f), (0.53f, 0f) }, 12, new Vector3(x, Ground(x, z), z));

            var built = b.Build(GreekMaterial.Stone, GreekMaterial.Paint, GreekMaterial.Azure, GreekMaterial.Pitch);
            var list = new Material[built.Slots.Length];
            for (var i = 0; i < list.Length; i++)
                list[i] = built.Slots[i] == GreekMaterial.Azure ? StandingWater(materials) : PortBuildings.MaterialFor(built.Slots[i], materials);
            var washery = GreekShips.Attach(model, "Washery", built, list);
            washery.gameObject.AddComponent<OwnedMeshes>().Meshes = new List<Mesh> { built.Mesh };
        }

        static Material standingWater;

        /// <summary>Still water in the tanks and the cistern: dark and glossy, a copy of the scene's neutral material.</summary>
        static Material StandingWater(PlaceholderMaterials materials)
        {
            if (standingWater != null) return standingWater;
            var source = materials?.Neutral;
            if (source == null) return null;
            standingWater = new Material(source) { name = $"{source.name} (standing water)" };
            if (standingWater.HasProperty("_BaseColor")) standingWater.SetColor("_BaseColor", new Color(0.13f, 0.2f, 0.22f));
            if (standingWater.HasProperty("_Smoothness")) standingWater.SetFloat("_Smoothness", 0.92f);
            return standingWater;
        }

        /// <summary>
        /// A block of the ore-bearing rock from the pipeline's rocks (AegeanArt), darkened to the schist of Laurion and
        /// set into the slope, in place of a plain cube of the same size and turn.
        /// </summary>
        static void SchistRock(Transform model, GameObject prefab, Vector3 foot, float size, float yaw, float tilt)
        {
            var bounds = AegeanArt.FinestBounds(prefab);
            var width = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
            var scale = size * 1.25f / width;
            var item = Object.Instantiate(prefab, model, false);
            item.name = "Ore rock";
            item.transform.localScale = Vector3.one * scale;
            item.transform.localRotation = Quaternion.Euler(tilt, yaw, tilt * 0.5f);
            item.transform.localPosition = foot - Vector3.up * (size * 0.15f + bounds.min.y * scale);
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
            {
                var shared = renderer.sharedMaterials;
                for (var i = 0; i < shared.Length; i++) shared[i] = Landscape.LandscapeArt.Schist(shared[i]);
                renderer.sharedMaterials = shared;
            }
        }

        /// <summary>-morzaPlainOlives: the old one-mesh olives on the same flanks, to measure what the new trees cost.</summary>
        void PlainOlives(Transform model, PlaceholderMaterials materials)
        {
            var plain = new TreeModels.Grove();
            for (var i = 0; i < 7; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var x = side * 14f;
                var z = 2f + i;
                if (ShoreShape.GroundAt(outline, lift, x, z - IsletZ) < 0.4f) continue;
                plain.Add(TreeKind.Olive, i, new Vector3(x, Ground(x, z) - 0.15f, z), i * 51f, 0.5f);
            }
            var mesh = plain.Build("Grove");
            if (mesh == null) return;
            var holder = new GameObject("Grove");
            holder.transform.SetParent(model, false);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            holder.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials?.Timber, materials?.OliveFoliage };
        }

        void BuildChests(Transform model, PlaceholderMaterials materials)
        {
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
        Transform sortedOre;

        /// <summary>Size of the hand-sorted ore heap by the mine (0.35 nearly worked out to 1 full); 0 where there is none.</summary>
        public float SortedOreScale => sortedOre != null ? sortedOre.localScale.x : 0f;

        public void UpdateStock(long stock)
        {
            var fraction = startStock > 0 ? (double)stock / startStock : 0.0;
            if (sortedOre != null) sortedOre.localScale = Vector3.one * (stock <= 0 ? 0.35f : Mathf.Lerp(0.35f, 1f, (float)System.Math.Min(1.0, fraction)));
            var shown = stock <= 0 ? 0 : System.Math.Max(1, (int)System.Math.Ceiling(fraction * pile.Count));
            if (shown == VisiblePileParts && shown != 0) return;
            VisiblePileParts = shown;
            for (var i = 0; i < pile.Count; i++)
            {
                var active = i < shown;
                if (pile[i].activeSelf != active) pile[i].SetActive(active);
            }
        }

        /// <summary>The art set's tree for a yard: its cypresses or pines in turn by number; none without them.</summary>
        static GameObject YardTree(bool cypress, int number)
        {
            var art = AegeanArt.Current;
            var kinds = art == null ? null : cypress ? art.Cypresses : art.Pines;
            return kinds != null && kinds.Length > 0 ? kinds[number % kinds.Length] : null;
        }

        /// <summary>
        /// One stack of the pipeline's fir logs in a pile at x along the shore: its logs along z and 6.8 m long like the
        /// plain logs, side by side across (−1, 0, 1) and lying on the stacks below (layer), each turned a little.
        /// </summary>
        GameObject LogStack(Transform model, GameObject prefab, float x, int across, int layer, System.Random random)
        {
            var bounds = AegeanArt.FinestBounds(prefab);
            var alongX = bounds.size.x > bounds.size.z;
            var length = Mathf.Max(0.01f, alongX ? bounds.size.x : bounds.size.z);
            var width = alongX ? bounds.size.z : bounds.size.x;
            var scale = 6.8f / length;
            var item = Object.Instantiate(prefab, model, false);
            item.name = "Log stack";
            var px = x + across * width * scale * 0.92f;
            var turn = Quaternion.Euler(0f, (alongX ? 90f : 0f) + ((float)random.NextDouble() - 0.5f) * 8f, 0f);
            var lift = layer * bounds.size.y * scale * 0.7f;
            item.transform.localRotation = turn;
            item.transform.localScale = Vector3.one * scale;
            item.transform.localPosition = new Vector3(px, Ground(px, -1f) - bounds.min.y * scale + lift, -1f) - turn * (new Vector3(bounds.center.x, 0f, bounds.center.z) * scale);
            return item;
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
