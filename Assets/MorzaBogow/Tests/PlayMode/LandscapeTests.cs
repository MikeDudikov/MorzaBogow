using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Presentation.Landscape;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MorzaBogow.Tests
{
    /// <summary>The environment in a running battle: olives as trees of their own, moved by the match's wind only.</summary>
    public sealed class LandscapeTests
    {
        GameObject host;
        BattleController battle;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Battle under test");
            battle = host.AddComponent<BattleController>();
            battle.Paused = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            foreach (var item in Object.FindObjectsByType<ShipVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        }

        static Quaternion[] Rotations(TreeGrove grove)
        {
            var result = new List<Quaternion>();
            for (var t = 0; t < grove.Count; t++) result.AddRange(grove.Bones(t).Select(bone => bone.localRotation));
            return result.ToArray();
        }

        [UnityTest]
        public IEnumerator Landscape_OlivesOfTheBattleAreTreesTheMatchWindBends()
        {
            yield return null;
            var groves = Object.FindObjectsByType<TreeGrove>(FindObjectsSortMode.None);
            Assert.That(groves.Length, Is.GreaterThan(0), "Port islets, free shores and the silver islet carry olives.");
            Assert.That(groves.Sum(grove => grove.Count), Is.GreaterThan(5));
            Assert.That(battle.World.Marine, Is.Not.Null, "A match has its wind.");
            Assert.That(Vegetation.Wind, Is.Not.Null);

            var grove = groves.OrderByDescending(item => item.Count).First();
            var paused = Rotations(grove);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(paused.Zip(Rotations(grove), Quaternion.Angle).Max(), Is.LessThan(1e-3f), "In a paused game the trees hold still.");

            for (var i = 0; i < 60; i++) grove.Advance(1f / 30f, true);
            Assert.That(grove.WindAt(0).magnitude, Is.GreaterThan(0.5f), "The match's wind reaches the trees.");
            Assert.That(paused.Zip(Rotations(grove), Quaternion.Angle).Max(), Is.GreaterThan(0.1f), "And bends them.");

            var leaves = grove.Renderers[0].sharedMaterials[1];
            Assert.That(leaves, Is.Not.Null);
            if (LandscapeArt.OliveLeaves != null) Assert.That(leaves, Is.SameAs(LandscapeArt.OliveLeaves), "The built translucent olive leaves, not the plain placeholder.");
            Assert.That(grove.GetComponent<LODGroup>().lodCount, Is.EqualTo(TreeGenerator.LodCount));
        }

        [UnityTest]
        public IEnumerator Landscape_TheSortedOreHeapFollowsTheStockAndNeverTouchesIt()
        {
            yield return null;
            var node = new ResourceNode { Id = 3, Type = ResourceType.Silver, X = 100, Y = -40, Stock = 1000, Capacity = 1000 };
            var site = ResourceSiteVisual.Create(node, null);
            try
            {
                var full = site.SortedOreScale;
                site.UpdateStock(400);
                var less = site.SortedOreScale;
                site.UpdateStock(0);
                Assert.That(full, Is.EqualTo(1f).Within(1e-4f), "A full deposit, a full heap.");
                Assert.That(less, Is.LessThan(full).And.GreaterThan(0.35f));
                Assert.That(site.SortedOreScale, Is.EqualTo(0.35f).Within(1e-4f));
                Assert.That(node.Stock, Is.EqualTo(1000), "The picture never changes the stock.");
                Assert.That(site.GetComponentsInChildren<MeshRenderer>().Any(item => item.name == "Galena"), Is.True, "The outcrop stands on the silver islet.");
            }
            finally
            {
                Object.Destroy(site.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Landscape_TreesReadTheWindAndLeaveTheMatchAsItWas()
        {
            yield return null;
            var reference = MapFactory.CreateSkirmish(20261109);
            reference.EnableMarineForMatch();
            var opponent = new Opponent(1);
            for (var i = 0; i < 300; i++)
            {
                opponent.Think(reference);
                reference.Step();
            }
            var groves = Object.FindObjectsByType<TreeGrove>(FindObjectsSortMode.None);
            for (var k = 0; k < 10; k++)
            {
                battle.RunTicks(30);
                foreach (var grove in groves) grove.Advance(1f, true);
            }
            yield return null;
            reference.Events.Clear();
            Assert.That(SaveGame.Hash(SaveGame.Serialize(battle.World)), Is.EqualTo(SaveGame.Hash(SaveGame.Serialize(reference))), "Sampling the wind for the trees changes nothing in the world.");
        }
    }
}
