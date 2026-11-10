using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation;
using MorzaBogow.Presentation.Landscape;
using NUnit.Framework;
using UnityEngine;

namespace MorzaBogow.Tests
{
    /// <summary>The maquis: kermes oaks and lentisks on the trees' generator, their leaves, their wind and where they grow.</summary>
    public sealed class ShrubTests
    {
        [Test]
        public void Shrubs_StayLowDenseToTheGroundAndWithinTheirBudgets()
        {
            var report = new List<string>();
            foreach (var species in TreeSpecies.Shrubs)
            {
                for (uint id = 1; id <= 20; id++)
                {
                    var bush = TreeGenerator.Build(species, id);
                    var triangles = bush.Lods.Select(mesh => mesh.Triangles).ToArray();
                    report.Add($"{species.Id} {id}: H {bush.Height:0.00} m, crown r {bush.CrownRadius:0.00} m, sprays {bush.Sprays.Count}, triangles {string.Join("/", triangles)}");
                    Assert.That(bush.Height, Is.InRange(species.HeightMin, species.HeightMax), species.Id);
                    Assert.That(bush.Top, Is.LessThanOrEqualTo(species.HeightMax + 0.5f), "A bush, not a tree.");
                    var stems = bush.Bones.Count(bone => bone.Role == BoneRole.Trunk && bone.Oscillates);
                    Assert.That(stems, Is.InRange(species.StemsMin, species.StemsMax), "Many stems from the ground.");
                    Assert.That(bush.Sprays.Count, Is.InRange(50, TreeGenerator.MostSprays), "Dense with leaves, within the budget.");
                    var leaves = bush.Lods[0].Leaves.Select(index => bush.Lods[0].Positions[index]).ToArray();
                    Assert.That(leaves.Min(p => p.y), Is.LessThan(0.3f * bush.Height), "The foliage comes down close to the ground: a dome, not a small tree.");
                    Assert.That(bush.CrownRadius * 2f, Is.GreaterThan(bush.Height * 0.9f), "As wide as it is tall, or wider.");
                    Assert.That(triangles[0], Is.LessThanOrEqualTo(5000), "LOD0 budget");
                    Assert.That(triangles[1], Is.LessThanOrEqualTo(1500), "LOD1 budget");
                    Assert.That(triangles[2], Is.LessThanOrEqualTo(400), "LOD2 budget");
                    Assert.That(triangles[0], Is.GreaterThan(triangles[1]));
                    Assert.That(triangles[1], Is.GreaterThan(triangles[2]));
                    // The farther levels' cards stay within the bush (an olive's grow to 2.5 m and more).
                    var far = bush.Lods[2].Leaves.Select(index => bush.Lods[2].Positions[index]).ToArray();
                    Assert.That(far.Max(p => new Vector2(p.x, p.z).magnitude), Is.LessThan(bush.CrownRadius * 1.6f + 0.3f), "Far cards keep to the bush.");
                }
            }
            TestContext.WriteLine(string.Join("\n", report));
            Assert.That(TreeGenerator.Build(TreeSpecies.KermesOak, 5).Lods[0].Positions, Is.EqualTo(TreeGenerator.Build(TreeSpecies.KermesOak, 5).Lods[0].Positions), "Same id, same bush.");
        }

        [Test]
        public void Shrubs_SwayFasterThanTheOlivesAsTheirModelSays()
        {
            float StemFrequency(TreeSpecies species, uint id)
            {
                var dynamics = new TreeDynamics(TreeGenerator.Build(species, id));
                var stems = dynamics.Modes.Where(mode => dynamics.Model.Bones[mode.Bone].Role == BoneRole.Trunk).ToArray();
                Assert.That(dynamics.Modes.All(mode => mode.Frequency <= TreeDynamics.HighestFrequency && mode.Stiffness > 0f && mode.Damping > 0f), Is.True);
                return stems.Average(mode => mode.Frequency);
            }
            var olives = Enumerable.Range(1, 5).Average(id => StemFrequency(TreeSpecies.OliveCultivated, (uint)id));
            foreach (var species in TreeSpecies.Shrubs)
            {
                for (uint id = 1; id <= 5; id++)
                {
                    var bush = TreeGenerator.Build(species, id);
                    var expected = species.FrequencyFor(bush.StemDiameter, bush.Height);
                    var frequency = StemFrequency(species, id);
                    Assert.That(frequency, Is.EqualTo(expected).Within(0.25f * expected), $"{species.Id} {id}: the pendulum model of its own size.");
                    Assert.That(frequency, Is.GreaterThan(olives), "A low bush sways faster than an olive.");
                }
            }
        }

        [Test]
        public void Shrubs_AtlasesDrawTheirOwnLeaves()
        {
            var olive = LeafAtlas.Bake(TreeSpecies.OliveCultivated, 256);
            var oak = LeafAtlas.Bake(TreeSpecies.KermesOak, 256);
            var lentisk = LeafAtlas.Bake(TreeSpecies.Lentisk, 256);
            foreach (var (name, atlas) in new[] { ("kermes", oak), ("lentisk", lentisk) })
            {
                Assert.That(atlas.Coverage(0), Is.InRange(0.15f, 0.9f), $"{name}: a spray of leaves with air between them.");
                Assert.That(atlas.Brightness(0), Is.LessThan(olive.Brightness(0)), "Dark green, not the olive's grey-green.");

            }
            Assert.That(oak.Smoothness(0), Is.GreaterThan(olive.Smoothness(0)), "The kermes oak's leaves shine...");
            Assert.That(oak.Smoothness(1), Is.GreaterThan(olive.Smoothness(1)), "...underneath too (report 8.3), not with the olive's matt scales.");
            foreach (var atlas in new[] { olive, oak, lentisk })
                Assert.That(atlas.Colour.Count(c => c.r == 0 && c.g == 0 && c.b == 0), Is.Zero, "No black, empty texels: past a leaf's tip the blade once came out NaN.");
            Assert.That(oak.Colour, Is.Not.EqualTo(lentisk.Colour), "Spiny ovals and pinnate leaves draw differently.");
            Assert.That(LeafAtlas.Bake(TreeSpecies.Lentisk, 256).Colour, Is.EqualTo(lentisk.Colour), "The same pixels every time.");
        }

        [Test]
        public void Maquis_KeepsOffBeachesCliffRimsTreesAndBuildings()
        {
            var outline = ShoreShape.Ellipse(60f, 45f);
            var lift = ShoreShape.Cliffs(0f, 60f, 110f, 1.3f);
            var trees = new List<Vector2> { new Vector2(10f, 5f), new Vector2(-12f, 8f), new Vector2(3f, -20f) };
            bool Blocked(float x, float z) => x > -8f && x < 8f && z > -8f && z < 8f;
            foreach (var settled in new[] { true, false })
            {
                var places = Maquis.Places(outline, lift, 4242u, Blocked, trees, settled);
                Assert.That(places.Count, Is.InRange(1, settled ? Maquis.MostSettled : Maquis.MostWooded));
                foreach (var place in places)
                {
                    var spot = new Vector2(place.position.x, place.position.z);
                    Assert.That(ShoreShape.GroundAt(outline, lift, spot.x, spot.y), Is.GreaterThanOrEqualTo(Maquis.LowestGround), "Never on the beach or in the swash.");
                    Assert.That(Blocked(spot.x, spot.y), Is.False, "Never on a building's ground.");
                    Assert.That(trees.All(tree => (tree - spot).magnitude >= Maquis.ClearOfTrees), Is.True, "Never at a tree's foot.");
                    Assert.That(places.Count(other => (new Vector2(other.position.x, other.position.z) - spot).magnitude < Maquis.Spacing), Is.EqualTo(1), "Apart from each other.");
                    var angle = Mathf.Atan2(spot.y, spot.x);
                    var fraction = spot.magnitude / ShoreShape.At(outline, angle);
                    Assert.That(ShoreShape.At(lift, angle) < CoastFeatures.CliffLift || fraction <= 0.86f, Is.True, "Never on a cliff's rim.");
                    Assert.That(TreeSpecies.Shrubs.Contains(place.species), Is.True);
                }
                Assert.That(Maquis.Places(outline, lift, 4242u, Blocked, trees, settled).Select(p => p.position), Is.EqualTo(places.Select(p => p.position)), "The same islet, the same bushes.");
            }
            var wooded = Maquis.Places(outline, lift, 4242u, Blocked, trees, false);
            var lentisksNearShore = wooded.Count(p => p.species == TreeSpecies.Lentisk && new Vector2(p.position.x, p.position.z).magnitude / ShoreShape.At(outline, Mathf.Atan2(p.position.z, p.position.x)) >= Maquis.ShoreBandInner);
            var lentisks = wooded.Count(p => p.species == TreeSpecies.Lentisk);
            Assert.That(lentisksNearShore, Is.GreaterThanOrEqualTo(lentisks / 2), "Lentisks keep mostly to the band above the shore.");
            Assert.That(Maquis.Places(outline, lift, 4242u, Blocked, trees, true).Count, Is.LessThanOrEqualTo(wooded.Count), "Fewer bushes where people live.");
        }
    }
}
