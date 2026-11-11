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

        [Test]
        public void Phrygana_GrowsInPatchesOnDryGroundClearOfTreesBushesAndBuildings()
        {
            var outline = ShoreShape.Ellipse(60f, 45f);
            var lift = ShoreShape.Cliffs(0f, 60f, 110f, 1.3f);
            var trees = new List<Vector2> { new Vector2(10f, 5f), new Vector2(-12f, 8f) };
            var bushes = new List<Vector2> { new Vector2(20f, -10f), new Vector2(-25f, -5f) };
            bool Blocked(float x, float z) => x > -8f && x < 8f && z > -8f && z < 8f;
            var settled = Phrygana.Make(outline, lift, 4242u, Blocked, trees, bushes, true);
            var wooded = Phrygana.Make(outline, lift, 4242u, Blocked, trees, bushes, false);
            Assert.That(settled.Cushions.Count, Is.InRange(40, Phrygana.MostSettled), "Where people and goats live the phrygana spreads...");
            Assert.That(wooded.Cushions.Count, Is.InRange(1, Phrygana.MostWooded), "...and keeps to the clearings of a wood.");
            Assert.That(settled.Cushions.Count, Is.GreaterThan(wooded.Cushions.Count));
            foreach (var kind in new[] { Phrygana.Kind.ThornyBurnet, Phrygana.Kind.Thyme, Phrygana.Kind.Rockrose })
                Assert.That(settled.Cushions.Count(c => c.Kind == kind), Is.GreaterThan(0), $"{kind} grows in it.");
            Assert.That(settled.Cushions.Count(c => c.Kind != Phrygana.Kind.Thyme), Is.GreaterThan(settled.Cushions.Count / 2), "Burnet and rockrose make most of it (report 9.21).");
            foreach (var cushion in settled.Cushions)
            {
                var spot = new Vector2(cushion.Position.x, cushion.Position.z);
                Assert.That(cushion.Position.y, Is.EqualTo(ShoreShape.GroundAt(outline, lift, spot.x, spot.y)).Within(1e-4f), "Standing on the ground.");
                Assert.That(cushion.Position.y, Is.GreaterThanOrEqualTo(Phrygana.LowestGround), "Never on the beach.");
                Assert.That(Blocked(spot.x, spot.y), Is.False, "Never on a building's ground.");
                Assert.That(Phrygana.InPatch(4242u, spot), Is.True, "In its patches, bare ground between them.");
                Assert.That(trees.All(t => (t - spot).magnitude >= Phrygana.ClearOfTrees) && bushes.All(b => (b - spot).magnitude >= Phrygana.ClearOfShrubs), Is.True, "Clear of the trees and the maquis.");
                Assert.That(settled.Cushions.Count(o => (new Vector2(o.Position.x, o.Position.z) - spot).magnitude < Phrygana.Spacing), Is.EqualTo(1));
                Assert.That(cushion.Size, Is.InRange(Phrygana.SizeMin(cushion.Kind), Phrygana.SizeMax(cushion.Kind)));
                Assert.That(cushion.Size * Phrygana.Profile(cushion.Kind), Is.LessThanOrEqualTo(cushion.Kind == Phrygana.Kind.Rockrose ? 1f : 0.6f), "Low: burnet and thyme under 60 cm, rockrose under a metre (report 9.1, 9.9, 9.13).");
            }
            Assert.That(settled.Mesh.vertexCount, Is.EqualTo(settled.Cushions.Count * 12), "Three cards a cushion.");
            Assert.That(Phrygana.Make(outline, lift, 4242u, Blocked, trees, bushes, true).Cushions.Select(c => c.Position), Is.EqualTo(settled.Cushions.Select(c => c.Position)), "The same islet, the same phrygana.");
        }

        [Test]
        public void Maquis_AndPhryganaKeepOffThePortSideAsTheOlivesDo()
        {
            var outline = ShoreShape.Ellipse(30f, 24f);
            var lift = ShoreShape.Cliffs(0f, 60f, 110f, 1.3f);
            // The port lies toward +x (angle 0): jetty, porters and slipways; the olives keep 115° away from it.
            const float port = 0f, away = 115f;
            float Bearing(Vector3 p) => Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg, port * Mathf.Rad2Deg));
            var bushes = Maquis.Places(outline, lift, 99u, null, null, true, port, away);
            var cushions = Phrygana.Make(outline, lift, 99u, null, null, null, true, port, away).Cushions;
            Assert.That(bushes.Count + cushions.Count, Is.GreaterThan(0), "The far side still has its scrub.");
            Assert.That(bushes.All(b => Bearing(b.position) >= away), Is.True, "No bush by the jetty or on the porters' way.");
            Assert.That(cushions.All(c => Bearing(c.Position) >= away), Is.True, "No phrygana there either.");
            var free = Phrygana.Make(outline, lift, 99u, null, null, null, true).Cushions;
            Assert.That(free.Any(c => Bearing(c.Position) < away), Is.True, "Without a port, the whole islet may carry it.");
        }

        [Test]
        public void Phrygana_AtlasShowsGreyBrownBurnetPinkThymeAndGreyGreenRockrose()
        {
            var atlas = PhryganaAtlas.Bake(256);
            var half = atlas.Size / 2;
            for (var cell = 0; cell < 4; cell++) Assert.That(atlas.Coverage(cell), Is.InRange(0.15f, 0.9f), $"Quarter {cell} holds its cushions with air around them.");
            (float r, float g, float b, int pink) Mean(int cell)
            {
                var corner = TreeGenerator.AtlasCells[cell];
                int x0 = (int)(corner.x * atlas.Size), y0 = (int)(corner.y * atlas.Size), count = 0, pink = 0;
                float r = 0f, g = 0f, b = 0f;
                for (var y = 0; y < half; y++)
                {
                    for (var x = 0; x < half; x++)
                    {
                        var c = atlas.Colour[(y0 + y) * atlas.Size + x0 + x];
                        if (c.a <= 127) continue;
                        r += c.r; g += c.g; b += c.b; count++;
                        if (c.r > c.g + 30 && c.b > c.g + 10) pink++;
                    }
                }
                return (r / count, g / count, b / count, pink);
            }
            var burnet = Mean(Phrygana.BurnetSide);
            Assert.That(burnet.r, Is.GreaterThan(burnet.b).And.GreaterThanOrEqualTo(burnet.g - 6f), "Burnet in summer: grey-brown, dry (report 9.6).");
            Assert.That(Mean(Phrygana.ThymeSide).pink, Is.GreaterThan(20), "Thyme in flower, purple-pink (report 9.10).");
            var rockrose = Mean(Phrygana.RockroseSide);
            Assert.That(rockrose.g, Is.GreaterThan(rockrose.r).And.GreaterThan(rockrose.b), "Rockrose: grey-green leaves.");
            Assert.That(Mean(Phrygana.RockroseSide).pink, Is.Zero, "Its pink flowers are over by summer (report 9.13).");
            Assert.That(atlas.Colour.Count(c => c.r == 0 && c.g == 0 && c.b == 0), Is.Zero, "No empty black texels.");
            Assert.That(PhryganaAtlas.Bake(256).Colour, Is.EqualTo(atlas.Colour), "The same pixels every time.");
        }
    }
}
