using System;
using System.Collections.Generic;
using System.Linq;
using MorzaBogow.Presentation.Landscape;
using MorzaBogow.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MorzaBogow.Tests
{
    /// <summary>Trees of the environment: the generator, the wind response, the grove and the leaf texture.</summary>
    public sealed class TreeTests
    {
        static readonly TreeSpecies Olive = TreeSpecies.OliveCultivated;
        static readonly TreeSpecies Wild = TreeSpecies.OliveWild;
        const float Air = 1.165f;

        // ------------------------------------------------------------ generator

        [Test]
        public void Trees_SameIdGivesTheSameTreeAndAnotherIdAnother()
        {
            var a = TreeGenerator.Build(Olive, 42);
            var b = TreeGenerator.Build(Olive, 42);
            var c = TreeGenerator.Build(Olive, 43);
            Assert.That(b.Lods[0].Positions, Is.EqualTo(a.Lods[0].Positions), "The same id builds the same tree, vertex for vertex.");
            Assert.That(b.Bones.Count, Is.EqualTo(a.Bones.Count));
            var differs = c.Lods[0].Vertices != a.Lods[0].Vertices || Mathf.Abs(c.Height - a.Height) > 1e-4f || c.Lods[0].Positions[10] != a.Lods[0].Positions[10];
            Assert.That(differs, Is.True, "Another id builds another tree.");
        }

        [Test]
        public void Trees_StayWithinTheirSpecies()
        {
            foreach (var species in TreeSpecies.All)
            {
                for (uint id = 1; id <= 30; id++)
                {
                    var tree = TreeGenerator.Build(species, id);
                    Assert.That(tree.Height, Is.InRange(species.HeightMin, species.HeightMax), species.Id);
                    Assert.That(tree.Top, Is.LessThanOrEqualTo(species.HeightMax + 0.8f), $"{species.Id} {id}: nothing rises much above the species' height.");
                    Assert.That(tree.Lods[0].Positions.Min(p => p.y), Is.GreaterThan(-0.3f), "The stem is sunk at most a hand's breadth into the ground.");
                    var stems = tree.Bones.Count(bone => bone.Role == BoneRole.Trunk && bone.Oscillates);
                    Assert.That(stems, Is.InRange(species.StemsMin, species.StemsMax), species.Id);
                    var reach = tree.Lods[0].Positions.Max(p => new Vector2(p.x, p.z).magnitude);
                    Assert.That(reach, Is.LessThan(tree.CrownRadius * 1.25f + 0.6f), "The crown stays within its envelope.");
                    Assert.That(tree.Sprays.Count, Is.InRange(50, TreeGenerator.MostSprays), "An olive's crown is full of leaves, within the budget.");
                }
            }
        }

        [Test]
        public void Trees_LevelsShareOneSkeletonWithinTheirBudgets()
        {
            var report = new List<string>();
            foreach (var species in TreeSpecies.All)
            {
                for (uint id = 1; id <= 8; id++)
                {
                    var tree = TreeGenerator.Build(species, id);
                    var triangles = tree.Lods.Select(mesh => mesh.Triangles).ToArray();
                    report.Add($"{species.Id} {id}: H {tree.Height:0.0} m, bones {tree.Bones.Count}, sprays {tree.Sprays.Count}, triangles {string.Join("/", triangles)}");
                    Assert.That(triangles[0], Is.LessThanOrEqualTo(5000), "LOD0 budget");
                    Assert.That(triangles[1], Is.LessThanOrEqualTo(1500), "LOD1 budget");
                    Assert.That(triangles[2], Is.LessThanOrEqualTo(400), "LOD2 budget");
                    Assert.That(triangles[0], Is.GreaterThan(triangles[1]));
                    Assert.That(triangles[1], Is.GreaterThan(triangles[2]));
                    foreach (var mesh in tree.Lods)
                    {
                        Assert.That(mesh.Normals.Count == mesh.Vertices && mesh.Tangents.Count == mesh.Vertices && mesh.Uv.Count == mesh.Vertices && mesh.Weights.Count == mesh.Vertices, Is.True);
                        var weightsOk = mesh.Weights.All(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) < 1e-4f
                            && w.boneIndex0 >= 0 && w.boneIndex0 < tree.Bones.Count && w.boneIndex1 >= 0 && w.boneIndex1 < tree.Bones.Count);
                        Assert.That(weightsOk, Is.True, "Every vertex is held by bones of this tree, its weights adding up to one.");
                        Assert.That(mesh.Bark.Concat(mesh.Leaves).All(index => index >= 0 && index < mesh.Vertices), Is.True);
                    }
                }
            }
            TestContext.WriteLine(string.Join("\n", report));
        }

        [Test]
        public void Trees_TheFootStaysPutWhileTheCrownBends()
        {
            var tree = TreeGenerator.Build(Olive, 7);
            var dynamics = new TreeDynamics(tree);
            var wind = new Vector3(15f, 0f, 4f);
            dynamics.Settle(wind, Air);
            dynamics.Advance(0.5f, wind, Air);
            var skin = Skin(tree, dynamics);
            var mesh = tree.Lods[0];
            float foot = 0f, crown = 0f;
            var shift = Vector3.zero;
            var count = 0;
            for (var v = 0; v < mesh.Vertices; v++)
            {
                var rest = mesh.Positions[v];
                var moved = Skinned(mesh, v, skin) - rest;
                if (rest.y < 0.05f) foot = Mathf.Max(foot, moved.magnitude);
                if (rest.y > tree.Height * 0.75f)
                {
                    crown = Mathf.Max(crown, moved.magnitude);
                    shift += moved;
                    count++;
                }
            }
            Assert.That(foot, Is.LessThan(0.002f), "The foot of the stem does not move.");
            Assert.That(crown, Is.GreaterThan(0.03f), "A strong wind visibly moves the crown.");
            Assert.That(Vector3.Dot(shift / Mathf.Max(1, count), wind.normalized), Is.GreaterThan(0f), "The crown leans downwind.");
        }

        [Test]
        public void Trees_ChangingLevelKeepsTheBend()
        {
            var tree = TreeGenerator.Build(Olive, 11);
            var dynamics = new TreeDynamics(tree);
            for (var i = 0; i < 180; i++) dynamics.Advance(1f / 60f, new Vector3(6f + 9f * Mathf.Sin(i * 0.21f), 0f, 3f * Mathf.Cos(i * 0.13f)), Air);
            var skin = Skin(tree, dynamics);
            var near = tree.Lods[0];
            var nearBark = near.Bark.Distinct().ToArray();
            var worst = 0f;
            var compared = 0;
            for (var lod = 1; lod < TreeGenerator.LodCount; lod++)
            {
                var far = tree.Lods[lod];
                foreach (var v in far.Bark.Distinct())
                {
                    var rest = far.Positions[v];
                    var best = -1;
                    var bestDistance = float.MaxValue;
                    foreach (var u in nearBark)
                    {
                        var distance = (near.Positions[u] - rest).sqrMagnitude;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = u;
                        }
                    }
                    var gap = Mathf.Sqrt(bestDistance);
                    var difference = ((Skinned(far, v, skin) - rest) - (Skinned(near, best, skin) - near.Positions[best])).magnitude;
                    worst = Mathf.Max(worst, difference - 0.2f * gap);
                    compared++;
                }
            }
            Assert.That(compared, Is.GreaterThan(100));
            Assert.That(worst, Is.LessThan(0.005f), "Bark of every level moves with the same bones: no jump when the level changes.");
        }

        // ------------------------------------------------------------ wind response

        /// <summary>Sway (σ of the crown's sideways movement) of a tree in the field's gusty wind at one point.</summary>
        static float Sway(TreeModel tree, double speed, int seconds = 120)
        {
            var dynamics = new TreeDynamics(tree);
            var field = new WindField(Steady(speed), 7, null);
            var gusts = new LocalGusts(tree.Seed, 2f * tree.CrownRadius, 96f);
            double drift = 0;
            Vector3 Wind()
            {
                var sample = field.Sample(0, 0);
                var value = new Vector3((float)sample.MeanX, 0f, (float)sample.MeanY) + new Vector3((float)sample.GustX, 0f, (float)sample.GustY) * gusts.FieldShare;
                return value + gusts.At(drift, value, (float)(sample.Turbulence * sample.MeanSpeed));
            }
            dynamics.Settle(Wind(), Air);
            var values = new List<float>();
            for (var i = 0; i < 60 * seconds; i++)
            {
                if (i % 15 == 0) field.Advance(0.25);
                var wind = Wind();
                drift += wind.magnitude / 60.0;
                dynamics.Advance(1f / 60f, wind, Air);
                values.Add(dynamics.CrownSway);
            }
            return Deviation(values);
        }

        [Test]
        public void TreeWind_TheTwoOliveFormsDifferInFrequency()
        {
            float Frequency(TreeSpecies species) => Enumerable.Range(1, 9).Average(id => new TreeDynamics(TreeGenerator.Build(species, (uint)id)).Modes[0].Frequency);
            var olive = Frequency(Olive);
            var wild = Frequency(Wild);
            var oliveSway = Enumerable.Range(1, 4).Average(id => Sway(TreeGenerator.Build(Olive, (uint)id), 10));
            var wildSway = Enumerable.Range(1, 4).Average(id => Sway(TreeGenerator.Build(Wild, (uint)id), 10));
            // Reported, not asserted: in this model the two forms of the one species sway alike (a difference between
            // species is to be checked once a second species is in).
            TestContext.WriteLine($"first mode: cultivated {olive:0.00} Hz, wild {wild:0.00} Hz; crown sway (σ) in a gusty 10 m/s: {oliveSway * 1000f:0.00} / {wildSway * 1000f:0.00} mm");
            Assert.That(wild / olive, Is.GreaterThan(1.1f), "The low wild olive sways faster than the tall grove tree (pendulum, f ∝ H^−½).");
        }

        [Test]
        public void TreeWind_AStrongerGustyWindSwaysTheCrownMore()
        {
            var tree = TreeGenerator.Build(Olive, 4);
            var gentle = Sway(tree, 6, 90);
            var strong = Sway(tree, 12, 90);
            TestContext.WriteLine($"crown sway (σ): {gentle * 1000f:0.00} mm at 6 m/s, {strong * 1000f:0.00} mm at 12 m/s");
            Assert.That(gentle, Is.GreaterThan(0f));
            Assert.That(strong / gentle, Is.GreaterThan(1.5f), "Twice the wind, gusting in proportion, sways the crown well over half as much again.");
        }

        [Test]
        public void TreeWind_ModesStayWithinTheirRanges()
        {
            var lines = new List<string>();
            foreach (var species in TreeSpecies.All)
            {
                for (uint id = 1; id <= 5; id++)
                {
                    var dynamics = new TreeDynamics(TreeGenerator.Build(species, id));
                    var stems = dynamics.Modes.Where(mode => dynamics.Model.Bones[mode.Bone].Role == BoneRole.Trunk).Select(mode => mode.Frequency).ToArray();
                    var limbs = dynamics.Modes.Where(mode => dynamics.Model.Bones[mode.Bone].Role != BoneRole.Trunk).Select(mode => mode.Frequency).ToArray();
                    lines.Add($"{species.Id} {id}: stems {string.Join(", ", stems.Select(f => f.ToString("0.00")))} Hz; limbs and branches {limbs.Min():0.0}–{limbs.Max():0.0} Hz");
                    Assert.That(stems.All(f => f >= 0.5f && f <= 2.5f), Is.True, "Whole-tree sway of a small broadleaved tree, about 1–2 Hz.");
                    Assert.That(dynamics.Modes.All(mode => mode.Frequency <= TreeDynamics.HighestFrequency && mode.Stiffness > 0f && mode.Damping > 0f), Is.True);
                }
            }
            TestContext.WriteLine(string.Join("\n", lines));
        }

        [Test]
        public void TreeWind_SteadyBendFollowsUToTheTwoPlusV()
        {
            var tree = TreeGenerator.Build(Olive, 5);
            var dynamics = new TreeDynamics(tree);
            float Bend(float speed)
            {
                dynamics.Settle(new Vector3(speed, 0f, 0f), Air);
                return dynamics.Angle[0].magnitude;
            }
            var exponent = 2f + Olive.VogelExponent;
            Assert.That(Bend(14f) / Bend(8f), Is.EqualTo(Mathf.Pow(14f / 8f, exponent)).Within(0.01f), "Above the reconfiguration speed drag grows as U^(2+V).");
            Assert.That(Bend(2.5f) / Bend(2f), Is.EqualTo(1.25f * 1.25f).Within(0.01f), "Below it drag grows as U².");

            var expected = Bend(14f);
            dynamics.Settle(Vector3.zero, Air);
            for (var i = 0; i < 60 * 30; i++) dynamics.Advance(1f / 60f, new Vector3(14f, 0f, 0f), Air);
            Assert.That(dynamics.Angle[0].magnitude, Is.EqualTo(expected).Within(0.01f * expected), "A steady wind comes to rest at the static bend.");
        }

        [Test]
        public void TreeWind_AGustSwaysTheTreeWhileASteadyWindOnlyBendsIt()
        {
            var tree = TreeGenerator.Build(Olive, 9);
            var steady = new TreeDynamics(tree);
            var gusty = new TreeDynamics(tree);
            var mean = new Vector3(8f, 0f, 0f);
            steady.Settle(mean, Air);
            gusty.Settle(mean, Air);
            var still = new List<float>();
            var swaying = new List<float>();
            for (var i = 0; i < 60 * 20; i++)
            {
                var t = i / 60f;
                var gust = mean * (1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.4f * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * 1.3f * t + 1f));
                steady.Advance(1f / 60f, mean, Air);
                gusty.Advance(1f / 60f, gust, Air);
                still.Add(steady.Angle[0].magnitude);
                swaying.Add(gusty.Angle[0].magnitude);
            }
            var level = still.Average();
            Assert.That(Deviation(still), Is.LessThan(1e-6f), "A steady wind holds the tree bent without swaying.");
            Assert.That(Deviation(swaying), Is.GreaterThan(0.1f * level), "Gusts of the same mean wind sway it.");
            Assert.That(swaying.Max(), Is.GreaterThan(1.15f * level), "The gusts bend it further than the steady wind.");
        }

        [Test]
        public void TreeWind_AfterTheGustTheSwayDiesAway()
        {
            var tree = TreeGenerator.Build(Olive, 3);
            var dynamics = new TreeDynamics(tree);
            dynamics.Settle(new Vector3(12f, 0f, 0f), Air);
            var initial = dynamics.Angle[0].magnitude;
            var peaks = new float[6];
            for (var i = 0; i < 60 * 15; i++)
            {
                dynamics.Advance(1f / 60f, Vector3.zero, Air);
                var second = i / 60;
                if (second < peaks.Length) peaks[second] = Mathf.Max(peaks[second], dynamics.Angle[0].magnitude);
            }
            TestContext.WriteLine($"peaks per second: {string.Join(", ", peaks.Select(p => (p / initial).ToString("0.000")))}");
            Assert.That(peaks[5], Is.LessThan(0.35f * peaks[0]), "Within five seconds most of the swing is gone.");
            for (var s = 1; s < peaks.Length; s++) Assert.That(peaks[s], Is.LessThanOrEqualTo(peaks[s - 1] * 1.05f), "Each second swings less than the one before.");
            Assert.That(dynamics.Angle[0].magnitude, Is.LessThan(0.02f * initial), "After fifteen seconds the tree has come to rest.");
        }

        [Test]
        public void TreeWind_NearTreesSwayTogetherAndDownwindTreesLater()
        {
            const double speed = 8, distance = 200, dt = 0.25;
            var field = new WindField(Steady(speed), 11, null);
            var tree = TreeGenerator.Build(Olive, 21);
            var here = new TreeDynamics(tree);
            var beside = new TreeDynamics(tree);
            var downwind = new TreeDynamics(tree);
            Vector3 At(double x, double y)
            {
                var sample = field.Sample(x, y);
                return new Vector3((float)sample.X, 0f, (float)sample.Y);
            }
            here.Settle(At(0, 0), Air);
            beside.Settle(At(0, 5), Air);
            downwind.Settle(At(distance, 0), Air);
            var n = (int)(600 / dt);
            var a = new double[n];
            var b = new double[n];
            var c = new double[n];
            for (var i = 0; i < n; i++)
            {
                field.Advance(dt);
                here.Advance((float)dt, At(0, 0), Air);
                beside.Advance((float)dt, At(0, 5), Air);
                downwind.Advance((float)dt, At(distance, 0), Air);
                a[i] = here.Angle[0].magnitude;
                b[i] = beside.Angle[0].magnitude;
                c[i] = downwind.Angle[0].magnitude;
            }
            Assert.That(Correlation(a, b, 0), Is.GreaterThan(0.8), "Trees 5 m apart sway together: the gusts are larger than that.");
            var bestLag = 0;
            var best = double.MinValue;
            for (var lag = 0; lag <= (int)(60 / dt); lag++)
            {
                var r = Correlation(a, c, lag);
                if (r > best)
                {
                    best = r;
                    bestLag = lag;
                }
            }
            var expected = distance / speed;
            TestContext.WriteLine($"beside {Correlation(a, b, 0):0.000}; downwind best lag {bestLag * dt:0.0} s (expected {expected:0.0}), r {best:0.000}");
            Assert.That(bestLag * dt, Is.EqualTo(expected).Within(0.25 * expected), "A tree 200 m downwind sways with the same gust, distance / wind speed later.");
            Assert.That(best, Is.GreaterThan(0.6));
        }

        // ------------------------------------------------------------ grove

        sealed class TestWind : ITreeWind
        {
            public Vector3 Wind;
            public bool Present = true;

            public bool Sample(Vector3 point, out TreeWindSample sample)
            {
                sample = new TreeWindSample { Mean = Wind, Gust = Vector3.zero, Turbulence = 0f, AirDensity = Air, GustLength = 96f };
                return Present;
            }
        }

        static (GameObject parent, TreeGrove grove) Grove()
        {
            var parent = new GameObject("Grove test");
            var builder = new TreeGroveBuilder();
            builder.Add(Olive, 1, Vector3.zero, 0f);
            builder.Add(Wild, 2, new Vector3(6f, 0f, 1f), 90f);
            builder.Add(Olive, 3, new Vector3(-5f, 0.4f, 3f), 200f, new Vector3(0f, 0f, -1f), 1f);
            return (parent, builder.Build(parent.transform, "Grove", null, null));
        }

        static Quaternion[] Rotations(TreeGrove grove)
        {
            var result = new List<Quaternion>();
            for (var t = 0; t < grove.Count; t++) result.AddRange(grove.Bones(t).Select(bone => bone.localRotation));
            return result.ToArray();
        }

        [Test]
        public void Grove_WithoutAWindModelTheTreesStandStill()
        {
            var (parent, grove) = Grove();
            try
            {
                Assert.That(grove.Count, Is.EqualTo(3));
                Assert.That(grove.Renderers.Count, Is.EqualTo(TreeGenerator.LodCount));
                var bones = grove.Renderers[0].bones;
                Assert.That(grove.Renderers.All(renderer => renderer.bones.SequenceEqual(bones)), Is.True, "Every level is skinned to the same bones.");
                Assert.That(grove.Renderers.All(renderer => renderer.sharedMesh.bindposes.SequenceEqual(grove.Renderers[0].sharedMesh.bindposes)), Is.True);
                Assert.That(parent.GetComponentInChildren<LODGroup>().lodCount, Is.EqualTo(TreeGenerator.LodCount));

                grove.Wind = new TestWind { Present = false };
                var before = Rotations(grove);
                grove.Advance(2f, true);
                var after = Rotations(grove);
                Assert.That(before.Zip(after, Quaternion.Angle).Max(), Is.LessThan(1e-3f), "No wind model, no wind of the trees' own: they stand still.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Grove_WindBendsTheBonesAndAPauseFreezesThem()
        {
            var (parent, grove) = Grove();
            try
            {
                var rest = Rotations(grove);
                grove.Wind = new TestWind { Wind = new Vector3(12f, 0f, 3f) };
                for (var i = 0; i < 30; i++) grove.Advance(1f / 30f, true);
                var bent = Rotations(grove);
                Assert.That(rest.Zip(bent, Quaternion.Angle).Max(), Is.GreaterThan(0.5f), "The wind bends the shoots and limbs.");
                for (var i = 0; i < 10; i++) grove.Advance(0f, true);
                Assert.That(bent.Zip(Rotations(grove), Quaternion.Angle).Max(), Is.LessThan(1e-4f), "A paused game holds the trees as they were.");
                Assert.That(grove.WindAt(0).magnitude, Is.GreaterThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // ------------------------------------------------------------ leaves and sources

        [Test]
        public void Leaves_AtlasIsRepeatableWithSilverUndersides()
        {
            var a = LeafAtlas.Bake(Olive, 512);
            var b = LeafAtlas.Bake(Olive, 512);
            Assert.That(a.Colour.SequenceEqual(b.Colour) && a.Normal.SequenceEqual(b.Normal) && a.Mask.SequenceEqual(b.Mask), Is.True, "The same species bakes the same texture.");
            TestContext.WriteLine($"brightness top {a.Brightness(0):0.000} under {a.Brightness(1):0.000}; smoothness {a.Smoothness(0):0.00}/{a.Smoothness(1):0.00}; coverage {a.Coverage(0):0.00}/{a.Coverage(2):0.00}");
            Assert.That(a.Brightness(1), Is.GreaterThan(a.Brightness(0) * 1.4f), "The underside is silvery, far paler than the grey-green top.");
            Assert.That(a.Smoothness(0), Is.GreaterThan(a.Smoothness(1)), "The upper side is smoother than the scaly underside.");
            Assert.That(a.Coverage(0), Is.InRange(0.12f, 0.75f), "A spray covers part of its card; the rest is cut away.");
            Assert.That(a.Coverage(2), Is.GreaterThan(0.1f));
        }

        [Test]
        public void Trees_SpeciesCiteTheirSources()
        {
            foreach (var species in TreeSpecies.All)
            {
                Assert.That(species.Sources.Length, Is.GreaterThanOrEqualTo(8), species.Id);
                Assert.That(species.Sources.All(note => !string.IsNullOrWhiteSpace(note.Claim) && !string.IsNullOrWhiteSpace(note.Reference) && !string.IsNullOrWhiteSpace(note.Place)), Is.True);
                Assert.That(species.Sources.Any(note => note.Confidence == Confidence.Established), Is.True);
            }
            Assert.That(Olive.Sources.Any(note => note.Confidence == Confidence.GameChoice), Is.True, "The grove's height is the game's choice and says so.");
        }

        [Test]
        public void Manifest_IsMadeFromTheCodeAndListsNoLaterIntroductions()
        {
            var path = System.IO.Path.Combine(Application.dataPath, "..", "docs", "art", "EnvironmentAssetManifest.json");
            Assert.That(System.IO.File.Exists(path), Is.True);
            Assert.That(System.IO.File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(EnvironmentManifest.Json()), "docs/art/EnvironmentAssetManifest.json is written by EnvironmentArt from the code.");
            var excluded = EnvironmentManifest.Excluded.Select(item => item.name.Split(' ')[0]).ToArray();
            Assert.That(EnvironmentManifest.Entries.All(entry => excluded.All(name => !entry.Name.Contains(name))), Is.True, "No species brought to the Aegean after antiquity.");
            Assert.That(EnvironmentManifest.Entries.All(entry => entry.Sources.Length > 0), Is.True, "Every asset names its sources.");
            Assert.That(EnvironmentManifest.Entries.Where(entry => entry.Status == AssetStatus.Trial).All(entry => entry.Tests.Length > 0), Is.True, "A finished asset names its tests.");
            var ids = EnvironmentManifest.Entries.Select(entry => entry.Id).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            var own = typeof(TreeTests).GetMethods().Select(method => $"TreeTests.{method.Name}").ToArray();
            var named = EnvironmentManifest.Entries.SelectMany(entry => entry.Tests).Where(test => test.StartsWith("TreeTests.")).Distinct();
            Assert.That(named.All(test => own.Contains(test)), Is.True, "The tests the manifest names exist.");
        }

        [Test]
        public void Environment_ProfilesFollowTheirSources()
        {
            var profile = EnvironmentProfile.Default;
            Assert.That(profile.Era, Is.EqualTo(Era.PostPalatial), "The project's anchor: the 12th century BC.");
            Assert.That(profile.Region, Is.EqualTo(Region.SouthMainland));
            Assert.That(profile.IronTools, Is.False, "LH IIIC shipwrights and miners work with bronze.");
            Assert.That(new EnvironmentProfile { Era = Era.Classical }.IronTools, Is.True);
            var eras = (Era[])Enum.GetValues(typeof(Era));
            for (var i = 1; i < eras.Length; i++)
            {
                Assert.That(EnvironmentProfile.Years(eras[i]).from, Is.EqualTo(EnvironmentProfile.Years(eras[i - 1]).to), "The eras follow one another.");
                Assert.That(new EnvironmentProfile { Era = eras[i] }.CultivatedOliveShare, Is.GreaterThanOrEqualTo(new EnvironmentProfile { Era = eras[i - 1] }.CultivatedOliveShare));
            }
            Assert.That(new EnvironmentProfile { Region = Region.Crete }.BrutiaPine, Is.True);
            Assert.That(new EnvironmentProfile { Region = Region.SouthMainland }.BrutiaPine, Is.False, "Aleppo pine on the mainland.");
        }

        // ------------------------------------------------------------ helpers

        static Matrix4x4[] Skin(TreeModel model, TreeDynamics dynamics)
        {
            var bones = model.Bones;
            var posed = new Matrix4x4[bones.Count];
            var skin = new Matrix4x4[bones.Count];
            for (var i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone.Parent < 0)
                {
                    posed[i] = Matrix4x4.identity;
                    skin[i] = Matrix4x4.identity;
                    continue;
                }
                var parent = bones[bone.Parent];
                var inverse = Quaternion.Inverse(parent.Rest);
                posed[i] = posed[bone.Parent] * Matrix4x4.TRS(inverse * (bone.Origin - parent.Origin), inverse * bone.Rest * dynamics.Bend(i), Vector3.one);
                skin[i] = posed[i] * Matrix4x4.TRS(bone.Origin, bone.Rest, Vector3.one).inverse;
            }
            return skin;
        }

        static Vector3 Skinned(TreeMesh mesh, int vertex, Matrix4x4[] skin)
        {
            var weight = mesh.Weights[vertex];
            var position = mesh.Positions[vertex];
            return skin[weight.boneIndex0].MultiplyPoint3x4(position) * weight.weight0 + skin[weight.boneIndex1].MultiplyPoint3x4(position) * weight.weight1;
        }

        static float Deviation(List<float> values)
        {
            var mean = values.Average();
            return Mathf.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
        }

        static MarineClimate Steady(double wind, double fromBearing = 270)
        {
            var climate = MarineClimate.Aegean(WindRegime.EtesianModerate, AegeanRegion.Central);
            climate.MeanWind = wind;
            climate.FromBearingDegrees = fromBearing;
            climate.DirectionSigmaDegrees = 0;
            climate.SpeedSigma = 0;
            climate.DiurnalAmplitude = 0;
            climate.CellsPerHour = 0;
            return climate;
        }

        static double Correlation(double[] a, double[] b, int lag)
        {
            var n = a.Length - lag;
            double ma = 0, mb = 0;
            for (var i = 0; i < n; i++)
            {
                ma += a[i];
                mb += b[i + lag];
            }
            ma /= n;
            mb /= n;
            double sab = 0, saa = 0, sbb = 0;
            for (var i = 0; i < n; i++)
            {
                var da = a[i] - ma;
                var db = b[i + lag] - mb;
                sab += da * db;
                saa += da * da;
                sbb += db * db;
            }
            return sab / Math.Sqrt(saa * sbb);
        }
    }
}
