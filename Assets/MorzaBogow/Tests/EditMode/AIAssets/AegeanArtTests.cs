using System.Linq;
using MorzaBogow.Editor;
using MorzaBogow.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The art set the battle draws from (AegeanArt in Resources): the pipeline's rocks and log stacks listed in path
    /// order, and models with levels of detail merged at their finest level only (the boulders were drawn four times).
    /// </summary>
    public sealed class AegeanArtTests
    {
        static int Triangles(Mesh mesh) => Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i) / 3);

        [Test]
        public void Art_ModelsWithLevelsOfDetailGiveTheirFinestLevelOnly()
        {
            var rock = AssetDatabase.LoadAssetAtPath<GameObject>(AegeanArtSet.Generated + "/rock_attica_limestone_a/rock_attica_limestone_a.prefab");
            Assert.That(rock, Is.Not.Null, "the pipeline's first Attic rock");
            var group = rock.GetComponentInChildren<LODGroup>();
            Assert.That(group, Is.Not.Null);
            var lods = group.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            var finest = lods[0].renderers.Sum(r => Triangles(r.GetComponent<MeshFilter>().sharedMesh));
            var all = rock.GetComponentsInChildren<MeshFilter>().Sum(f => Triangles(f.sharedMesh));
            var taken = AegeanArt.FinestMeshes(rock).ToList();
            Assert.That(taken.Sum(part => Triangles(part.mesh)), Is.EqualTo(finest), "the finest level, as close up");
            Assert.That(finest, Is.LessThan(all), "not every level merged together");
            Assert.That(taken.All(part => part.material != null), Is.True, "each part keeps its material");

            // A model without levels of detail is taken whole.
            var plain = new GameObject("Plain rock");
            try
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(plain.transform, false);
                cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                Assert.That(AegeanArt.FinestMeshes(plain).Count(), Is.EqualTo(1));
                var bounds = AegeanArt.FinestBounds(plain);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(bounds.size.x, Is.EqualTo(1f).Within(1e-5f));
            }
            finally
            {
                Object.DestroyImmediate(plain);
            }
        }

        [Test]
        public void Art_WoodsAreMergedAtTheTreesSecondLevelOfDetail()
        {
            var set = Resources.Load<AegeanArt>(AegeanArt.ResourcePath);
            var trees = set.Pines.Concat(set.Cypresses).ToList();
            Assert.That(trees.Count, Is.GreaterThanOrEqualTo(2), "a pine and a cypress for the woods");
            foreach (var tree in trees)
            {
                var lods = tree.GetComponentInChildren<LODGroup>().GetLODs();
                int Level(int level) => lods[level].renderers.Sum(r => Triangles(r.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(AegeanArt.LevelMeshes(tree, 1).Sum(part => Triangles(part.mesh)), Is.EqualTo(Level(1)), $"{tree.name}: the second level");
                Assert.That(Level(1), Is.LessThan(Level(0)), $"{tree.name}: lighter than the finest");
                Assert.That(AegeanArt.LevelMeshes(tree, 9).Sum(part => Triangles(part.mesh)), Is.EqualTo(Level(lods.Length - 1)), $"{tree.name}: past the last level, the last");
                Assert.That(AegeanArt.LevelMeshes(tree, 1).All(part => part.material != null), Is.True, $"{tree.name}: merged per material");
            }
        }

        [Test]
        public void Art_ModelsMergedInTheBattleHaveReadableMeshes()
        {
            // The battle merges rocks and trees with Mesh.CombineMeshes; a player drops meshes it may not read.
            var set = Resources.Load<AegeanArt>(AegeanArt.ResourcePath);
            var merged = set.Rocks.Concat(set.Pines).Concat(set.Cypresses).ToList();
            Assert.That(merged.Count, Is.GreaterThanOrEqualTo(5));
            foreach (var prefab in merged)
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(filter.sharedMesh.isReadable, Is.True, $"{prefab.name}/{filter.name}: Read/Write on, or the player drops it from the merged shore or wood");
        }

        [Test]
        public void Art_SetListsThePipelinesRocksAndLogsInPathOrder()
        {
            var set = Resources.Load<AegeanArt>(AegeanArt.ResourcePath);
            Assert.That(set, Is.Not.Null, "the art set lies in Resources, so builds carry it");
            Assert.That(AssetDatabase.GetAssetPath(set), Is.EqualTo(AegeanArtSet.AssetPath));
            var rocks = AegeanArtSet.Prefabs("rock_");
            var logs = AegeanArtSet.Prefabs("prop_timber_");
            Assert.That(rocks.Length, Is.GreaterThanOrEqualTo(2), "limestone and marble");
            Assert.That(logs.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(set.Rocks, Is.EqualTo(rocks), "the set is up to date with the pipeline's finished rocks (AegeanArtSet.Refresh)");
            Assert.That(set.TimberStacks, Is.EqualTo(logs));
            Assert.That(set.Pines, Is.EqualTo(AegeanArtSet.Prefabs("veg_pinus_")), "the timber yards' pines");
            Assert.That(set.Cypresses, Is.EqualTo(AegeanArtSet.Prefabs("veg_cupressus_")), "the timber yards' cypresses");
            Assert.That(set.Props, Is.EqualTo(AegeanArtSet.Prefabs("prop_").Where(p => !p.name.StartsWith("prop_timber_")).ToArray()), "the small props, logs apart");
            Assert.That(set.Prop("prop_basket_"), Is.Not.Null, "a basket of ore for Laurion");
            Assert.That(set.Prop("prop_rope_"), Is.Not.Null, "a coil of rope for the jetties");
            Assert.That(set.Prop("prop_nothing_"), Is.Null);
            var paths = rocks.Select(AssetDatabase.GetAssetPath).ToList();
            Assert.That(paths, Is.Ordered.Using((System.Collections.Generic.IComparer<string>)System.StringComparer.Ordinal), "in path order, so the same land gets the same rocks");
            foreach (var prefab in rocks.Concat(logs))
                Assert.That(prefab.GetComponentInChildren<LODGroup>(), Is.Not.Null, $"{prefab.name}: levels of detail from Blender");
        }
    }
}
