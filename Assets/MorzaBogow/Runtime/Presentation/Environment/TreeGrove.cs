using System;
using System.Collections.Generic;
using MorzaBogow.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>Wind at a tree: the field's mean wind and gusts at the crown's height, its turbulence and the air's density.</summary>
    public struct TreeWindSample
    {
        public Vector3 Mean, Gust;
        public float Turbulence, AirDensity, GustLength;
    }

    /// <summary>Where the trees read their wind from. False means no wind model: the trees then stand still.</summary>
    public interface ITreeWind
    {
        bool Sample(Vector3 point, out TreeWindSample sample);
    }

    /// <summary>
    /// The match's wind (World.Marine.Wind) for the trees, read only: U10 brought to the crown's height above the sea
    /// by the field's own logarithmic profile (COARE roughness over water; the islets are too small for an internal
    /// boundary layer over land to grow much at a crown's height, an approximation noted in the documentation).
    /// </summary>
    public sealed class MarineTreeWind : ITreeWind
    {
        readonly Func<World> world;

        public MarineTreeWind(Func<World> world) => this.world = world;

        public bool Sample(Vector3 point, out TreeWindSample sample)
        {
            sample = default;
            var marine = world?.Invoke()?.Marine;
            if (marine == null) return false;
            var field = marine.Wind;
            var wind = field.Sample(point.x, point.z);
            var z0 = Math.Max(wind.Z0, 1e-6);
            var profile = (float)(Math.Log(Math.Max(point.y, 0.5) / z0) / Math.Log(MarineUnits.ReferenceHeight / z0));
            sample.Mean = new Vector3((float)wind.MeanX, 0f, (float)wind.MeanY) * profile;
            sample.Gust = new Vector3((float)wind.GustX, 0f, (float)wind.GustY) * profile;
            sample.Turbulence = (float)wind.Turbulence;
            sample.AirDensity = (float)field.Climate.AirDensity;
            sample.GustLength = (float)field.Climate.GustLength;
            return true;
        }
    }

    /// <summary>What every grove of a match reads: the wind and the game's clock (zero in a pause). Set by the battle.</summary>
    public static class Vegetation
    {
        public static ITreeWind Wind;
        public static Func<float> Clock;

        /// <summary>The old one-mesh olives instead of these trees (-morzaPlainOlives), for A/B measurements in one build.</summary>
        public static bool PlainOlives;

        /// <summary>
        /// Where the prevailing wind blows toward, world space: the region's Etesian, which trains the crowns of
        /// exposed trees over years (a Sirocco match does not reshape them). Zero when no climate is known.
        /// </summary>
        public static Vector3 Leeward;

        /// <summary>The prevailing (Etesian) wind of a climate, as a world direction it blows toward; zero without one.</summary>
        public static Vector3 LeewardOf(MarineClimate climate)
        {
            if (climate == null) return Vector3.zero;
            var bearing = climate.Regime == WindRegime.Sirocco ? 0.0 : climate.FromBearingDegrees;
            var toward = Math.PI / 2 - (bearing + 180.0) * Math.PI / 180.0;
            return new Vector3((float)Math.Cos(toward), 0f, (float)Math.Sin(toward));
        }
    }

    /// <summary>
    /// Gusts smaller than the wind field resolves (it stops at 20 m), down to the crown's own size, from the same von
    /// Kármán spectrum: frozen turbulence carried past the tree in a few modes with phases from the tree's id. The
    /// field holds all of σ_u² in its band, so its gusts are scaled to their true share and these add the rest:
    /// the variance at the tree stays that of the spectrum between ten integral lengths and the crown.
    /// </summary>
    public sealed class LocalGusts
    {
        const int Modes = 6;
        readonly double[] wavenumber = new double[Modes], slant = new double[Modes], phase = new double[Modes];
        readonly double[] lateralSlant = new double[Modes], lateralPhase = new double[Modes], amplitude = new double[Modes];
        readonly int count;

        /// <summary>The factor for the field's own gusts: the square root of the spectrum's share in its band.</summary>
        public readonly float FieldShare;

        public LocalGusts(uint seed, float crownDiameter, float gustLength)
        {
            gustLength = Mathf.Max(gustLength, 1f);
            var lowest = WindField.LowestWavenumber(gustLength);
            var field = WindField.HighestWavenumber;
            var crown = 2.0 * Math.PI / Math.Max(crownDiameter, 1.5);
            FieldShare = (float)Math.Sqrt(Math.Max(0.0, WindField.VarianceBelow(field, gustLength) - WindField.VarianceBelow(lowest, gustLength)));
            if (crown <= field) return;
            count = Modes;
            for (var m = 0; m < Modes; m++)
            {
                var k0 = field * Math.Pow(crown / field, (double)m / Modes);
                var k1 = field * Math.Pow(crown / field, (double)(m + 1) / Modes);
                var variance = WindField.VarianceBelow(k1, gustLength) - WindField.VarianceBelow(k0, gustLength);
                amplitude[m] = Math.Sqrt(2.0 * Math.Max(0.0, variance));
                wavenumber[m] = Math.Sqrt(k0 * k1);
                // A two-dimensional mode at an angle to the wind passes the tree at U·k·cos θ.
                slant[m] = Math.Cos((StableHash.Unit(StableHash.Mix(seed, (uint)m, 11u)) - 0.5) * Math.PI);
                phase[m] = StableHash.Unit(StableHash.Mix(seed, (uint)m, 12u)) * 2.0 * Math.PI;
                lateralSlant[m] = Math.Cos((StableHash.Unit(StableHash.Mix(seed, (uint)m, 13u)) - 0.5) * Math.PI);
                lateralPhase[m] = StableHash.Unit(StableHash.Mix(seed, (uint)m, 14u)) * 2.0 * Math.PI;
            }
        }

        /// <summary>The gust at the tree after this much air has passed (metres), for a mean wind and its σ_u.</summary>
        public Vector3 At(double drift, Vector3 mean, float sigma)
        {
            var speed = mean.magnitude;
            if (count == 0 || sigma <= 0f || speed < 1e-3f) return Vector3.zero;
            var along = mean / speed;
            var across = new Vector3(-along.z, 0f, along.x);
            double u = 0, v = 0;
            for (var m = 0; m < count; m++)
            {
                u += amplitude[m] * Math.Cos(wavenumber[m] * slant[m] * drift + phase[m]);
                v += amplitude[m] * Math.Cos(wavenumber[m] * lateralSlant[m] * drift + lateralPhase[m]);
            }
            return along * (float)(sigma * u) + across * (float)(sigma * 0.75 * v);
        }
    }

    /// <summary>
    /// A grove drawn as one skinned mesh per level of detail (bark and leaves), every tree over its own bones and all
    /// levels over the same bones. Each frame it advances every tree's <see cref="TreeDynamics"/> by the game's clock
    /// (nothing in a pause) in the match's wind and bends the bones; out of sight the trees keep moving but no bone
    /// is written. Presentation only.
    /// </summary>
    public sealed class TreeGrove : MonoBehaviour
    {
        public const float SampleInterval = 0.25f;

        /// <summary>Most game time one frame advances the trees: after a hitch they lag a little rather than spend steps catching up.</summary>
        public const float LongestFrame = 0.05f;

        /// <summary>
        /// LODGroup.size set to the trees' scale, not the grove's, so the levels change at a fixed number of pixels
        /// per metre: 12 and 4 at 1080 lines (zoom 75 and 225 of the oblique camera), culled below 0.2.
        /// </summary>
        public const float LodSize = 10f;
        public static readonly float[] LodHeights = { 0.111f, 0.037f, 0.002f };

        public ITreeWind Wind;
        public Func<float> Clock;

        internal sealed class Tree
        {
            public TreeModel Model;
            public TreeDynamics Dynamics;
            public Transform[] Bones;
            public Quaternion[] RestLocal;
            public Quaternion ToTree;
            public Vector3 Crown;
            public LocalGusts Gusts;
            public Vector3 From, To;
            public float SampledAt, NextSample, Turbulence, AirDensity = 1.2f;
            public double Drift;
            public bool HasWind;
        }

        Tree[] trees = Array.Empty<Tree>();
        SkinnedMeshRenderer[] renderers = Array.Empty<SkinnedMeshRenderer>();
        Mesh[] meshes = Array.Empty<Mesh>();
        float clock;

        public int Count => trees.Length;
        public IReadOnlyList<SkinnedMeshRenderer> Renderers => renderers;
        public IReadOnlyList<Mesh> Meshes => meshes;
        public TreeModel Model(int tree) => trees[tree].Model;
        public TreeDynamics Dynamics(int tree) => trees[tree].Dynamics;
        public IReadOnlyList<Transform> Bones(int tree) => trees[tree].Bones;
        public Vector3 WindAt(int tree) => trees[tree].Dynamics.Wind;

        static readonly List<TreeGrove> groves = new List<TreeGrove>();
        static double accumulated, average;
        static int stamp = -1;

        /// <summary>Trees in all groves of the scene and the bark and leaf triangles of each level of detail.</summary>
        public static int TreesActive
        {
            get
            {
                var count = 0;
                foreach (var grove in groves) count += grove.Count;
                return count;
            }
        }

        public static long Triangles(int lod)
        {
            long count = 0;
            foreach (var grove in groves)
            {
                if (lod < grove.meshes.Length && grove.meshes[lod] != null) count += (grove.meshes[lod].GetIndexCount(0) + grove.meshes[lod].GetIndexCount(1)) / 3;
            }
            return count;
        }

        /// <summary>Main-thread time of the trees' wind and bones over the last frames, milliseconds (moving average).</summary>
        public static double FrameMilliseconds => average;

        /// <summary>The same, averaged over every frame since the trees first moved (smoke reports).</summary>
        public static double MeanMilliseconds => frames > 0 ? total / frames : 0.0;

        static double total;
        static long frames;

        void OnEnable() => groves.Add(this);

        void OnDisable() => groves.Remove(this);

        void OnDestroy()
        {
            foreach (var mesh in meshes)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
        }

        void Update()
        {
            var source = Clock ?? Vegetation.Clock;
            Advance(source != null ? source() : Time.deltaTime);
        }

        /// <summary>
        /// Advances every tree by dt seconds of game time and bends its bones, while the grove is in the camera's view
        /// (or when asked to). Out of view it rests: nothing is computed, and when it comes back each tree starts from the
        /// static bend of the wind then blowing, as a tree out of sight would be found.
        /// </summary>
        public void Advance(float dt, bool pose = false)
        {
            if (dt <= 0f || trees.Length == 0) return;
            if (!pose && !InView)
            {
                resting = true;
                return;
            }
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            dt = Mathf.Min(dt, LongestFrame);
            clock += dt;
            if (resting)
            {
                resting = false;
                foreach (var tree in trees)
                {
                    tree.HasWind = false;
                    tree.NextSample = clock;
                }
            }
            var wind = Wind ?? Vegetation.Wind;
            // Under 12 pixels a metre (LOD1) a shoot's flutter is under a pixel and its bones keep still; under 4 (LOD2)
            // no branch shows either, and only the stems and main limbs move.
            var detail = pose ? 0 : Detail;
            var far = detail >= 2;
            foreach (var tree in trees)
            {
                if (wind != null && clock >= tree.NextSample) Sample(tree, wind);
                var current = Vector3.zero;
                if (tree.HasWind)
                {
                    var field = Vector3.Lerp(tree.From, tree.To, Mathf.Clamp01((clock - tree.SampledAt) / SampleInterval));
                    var speed = field.magnitude;
                    tree.Drift += speed * dt;
                    current = field + tree.Gusts.At(tree.Drift, field, tree.Turbulence * speed);
                }
                tree.Dynamics.Advance(dt, tree.ToTree * current, tree.AirDensity, far);
            }
            Pose(detail);

            var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (Time.frameCount != stamp)
            {
                if (stamp >= 0)
                {
                    total += accumulated;
                    frames++;
                }
                average = stamp < 0 ? accumulated : average * 0.95 + accumulated * 0.05;
                accumulated = 0;
                stamp = Time.frameCount;
            }
            accumulated += elapsed;
        }

        bool resting;
        static readonly Plane[] frustum = new Plane[6];
        static int frustumFrame = -1;
        static bool frustumValid;

        /// <summary>
        /// Whether the grove is inside the main camera's view this frame (with a margin for its swaying crowns). Not
        /// Renderer.isVisible, which also counts the sun's shadow passes over half the map.
        /// </summary>
        bool InView
        {
            get
            {
                if (Time.frameCount != frustumFrame)
                {
                    frustumFrame = Time.frameCount;
                    var camera = Camera.main;
                    frustumValid = camera != null;
                    if (frustumValid) GeometryUtility.CalculateFrustumPlanes(camera, frustum);
                }
                if (!frustumValid || renderers.Length == 0 || renderers[0] == null) return false;
                var bounds = renderers[0].bounds;
                bounds.Expand(4f);
                return GeometryUtility.TestPlanesAABB(frustum, bounds);
            }
        }

        void Sample(Tree tree, ITreeWind wind)
        {
            tree.NextSample = clock + SampleInterval;
            if (!wind.Sample(tree.Crown, out var sample))
            {
                tree.HasWind = false;
                return;
            }
            if (tree.Gusts == null) tree.Gusts = new LocalGusts(tree.Model.Seed, 2f * tree.Model.CrownRadius, sample.GustLength);
            var value = sample.Mean + sample.Gust * tree.Gusts.FieldShare;
            if (!tree.HasWind)
            {
                // The first wind a tree meets: it starts bent as a steady wind would hold it, not swinging from rest.
                tree.From = value;
                tree.Dynamics.Settle(tree.ToTree * value, sample.AirDensity);
            }
            else tree.From = Vector3.Lerp(tree.From, tree.To, Mathf.Clamp01((clock - tree.SampledAt) / SampleInterval));
            tree.To = value;
            tree.SampledAt = clock;
            tree.Turbulence = sample.Turbulence;
            tree.AirDensity = sample.AirDensity;
            tree.HasWind = true;
        }

        /// <summary>Writes every bone's rest pose times its bending; from LOD1 not the shoots', from LOD2 not the branches' either.</summary>
        public void Pose(int detail = 0)
        {
            foreach (var tree in trees)
            {
                var bones = tree.Bones;
                var model = tree.Model.Bones;
                for (var i = 1; i < bones.Length; i++)
                {
                    var role = model[i].Role;
                    if (detail >= 1 && role == BoneRole.Twig || detail >= 2 && role == BoneRole.Branch) continue;
                    bones[i].localRotation = tree.RestLocal[i] * tree.Dynamics.Bend(i);
                }
            }
        }

        /// <summary>
        /// The level of detail the main camera draws the trees at: from its pixels per metre against the same 12 and 4
        /// as the LODGroup (scaled by the quality's LOD bias). Orthographic only, like the game's camera; 0 otherwise.
        /// </summary>
        static int Detail
        {
            get
            {
                var camera = Camera.main;
                if (camera == null || !camera.orthographic || camera.orthographicSize <= 0f) return 0;
                var pixels = camera.pixelHeight / (2f * camera.orthographicSize) * QualitySettings.lodBias;
                return pixels >= 12f ? 0 : pixels >= 4f ? 1 : 2;
            }
        }

        internal void Initialise(Tree[] grown, SkinnedMeshRenderer[] drawn, Mesh[] built)
        {
            trees = grown;
            renderers = drawn;
            meshes = built;
            // Staggered sampling: an eighth of the trees each frame of the interval, never all at once.
            for (var i = 0; i < trees.Length; i++) trees[i].NextSample = SampleInterval * (i % 8) / 8f;
        }
    }

    /// <summary>
    /// Gathers trees for one grove and builds it: bones per tree, one skinned mesh per level of detail with bark
    /// (submesh 0) and leaves (submesh 1), and a LODGroup. Draws stay those of one mesh per level, however many trees.
    /// </summary>
    public sealed class TreeGroveBuilder
    {
        readonly List<(TreeModel model, Vector3 position, float yaw)> plants = new List<(TreeModel model, Vector3 position, float yaw)>();

        public int Count => plants.Count;

        /// <summary>Adds a tree at a point of the grove (its parent's space), turned about +Y by yaw degrees.</summary>
        /// <param name="leeward">Where the prevailing wind blows toward, in the same space; zero for a sheltered tree.</param>
        /// <param name="exposure">0 sheltered to 1 out on the shore: how much the wind has trained the crown.</param>
        public TreeModel Add(TreeSpecies species, uint id, Vector3 position, float yaw, Vector3 leeward = default, float exposure = 0f)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * leeward;
            var model = TreeGenerator.Build(species, id, local, exposure);
            plants.Add((model, position, yaw));
            return model;
        }

        public TreeGrove Build(Transform parent, string name, Material bark, Material leaves)
        {
            if (plants.Count == 0) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var grove = root.AddComponent<TreeGrove>();

            var allBones = new List<Transform>();
            var offsets = new int[plants.Count];
            var trees = new TreeGrove.Tree[plants.Count];
            for (var t = 0; t < plants.Count; t++)
            {
                var (model, position, yaw) = plants[t];
                offsets[t] = allBones.Count;
                var bones = new Transform[model.Bones.Count];
                var rest = new Quaternion[model.Bones.Count];
                foreach (var bone in model.Bones)
                {
                    var item = new GameObject(bone.Parent < 0 ? $"Tree {t}" : $"{bone.Role} {bone.Index}").transform;
                    if (bone.Parent < 0)
                    {
                        item.SetParent(root.transform, false);
                        item.localPosition = position;
                        item.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                    else
                    {
                        var p = model.Bones[bone.Parent];
                        var inverse = Quaternion.Inverse(p.Rest);
                        item.SetParent(bones[bone.Parent], false);
                        item.localPosition = inverse * (bone.Origin - p.Origin);
                        item.localRotation = inverse * bone.Rest;
                    }
                    bones[bone.Index] = item;
                    rest[bone.Index] = item.localRotation;
                    allBones.Add(item);
                }
                trees[t] = new TreeGrove.Tree
                {
                    Model = model,
                    Dynamics = new TreeDynamics(model),
                    Bones = bones,
                    RestLocal = rest,
                    ToTree = Quaternion.Inverse(bones[0].rotation),
                    Crown = bones[0].TransformPoint(model.CrownCentre),
                };
            }

            var boneArray = allBones.ToArray();
            var bindposes = new Matrix4x4[boneArray.Length];
            var space = root.transform.localToWorldMatrix;
            for (var i = 0; i < boneArray.Length; i++) bindposes[i] = boneArray[i].worldToLocalMatrix * space;

            var renderers = new SkinnedMeshRenderer[TreeGenerator.LodCount];
            var meshes = new Mesh[TreeGenerator.LodCount];
            for (var lod = 0; lod < TreeGenerator.LodCount; lod++)
            {
                var mesh = Combine(lod, offsets, bindposes, name);
                var holder = new GameObject($"LOD{lod}");
                holder.transform.SetParent(root.transform, false);
                var renderer = holder.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.bones = boneArray;
                renderer.rootBone = root.transform;
                var bounds = mesh.bounds;
                bounds.Expand(2f);
                renderer.localBounds = bounds;
                renderer.updateWhenOffscreen = false;
                renderer.skinnedMotionVectors = true;
                renderer.quality = SkinQuality.Bone2;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.sharedMaterials = new[] { bark, leaves };
                renderers[lod] = renderer;
                meshes[lod] = mesh;
            }

            var group = root.AddComponent<LODGroup>();
            var levels = new LOD[TreeGenerator.LodCount];
            for (var i = 0; i < levels.Length; i++) levels[i] = new LOD(TreeGrove.LodHeights[i], new Renderer[] { renderers[i] });
            group.SetLODs(levels);
            group.localReferencePoint = meshes[0].bounds.center;
            group.size = TreeGrove.LodSize;
            group.fadeMode = LODFadeMode.None;

            grove.Initialise(trees, renderers, meshes);
            return grove;
        }

        Mesh Combine(int lod, int[] offsets, Matrix4x4[] bindposes, string name)
        {
            int vertices = 0, bark = 0, leaves = 0;
            foreach (var plant in plants)
            {
                var m = plant.model.Lods[lod];
                vertices += m.Vertices;
                bark += m.Bark.Count;
                leaves += m.Leaves.Count;
            }
            var positions = new Vector3[vertices];
            var normals = new Vector3[vertices];
            var tangents = new Vector4[vertices];
            var uv = new Vector2[vertices];
            var weights = new BoneWeight[vertices];
            var barkIndices = new int[bark];
            var leafIndices = new int[leaves];
            int v = 0, b = 0, l = 0;
            for (var t = 0; t < plants.Count; t++)
            {
                var (model, position, yaw) = plants[t];
                var m = model.Lods[lod];
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                var start = v;
                for (var i = 0; i < m.Vertices; i++, v++)
                {
                    positions[v] = position + rotation * m.Positions[i];
                    normals[v] = rotation * m.Normals[i];
                    var tangent = m.Tangents[i];
                    var direction = rotation * new Vector3(tangent.x, tangent.y, tangent.z);
                    tangents[v] = new Vector4(direction.x, direction.y, direction.z, tangent.w);
                    uv[v] = m.Uv[i];
                    var weight = m.Weights[i];
                    weight.boneIndex0 += offsets[t];
                    weight.boneIndex1 += offsets[t];
                    weight.boneIndex2 += offsets[t];
                    weight.boneIndex3 += offsets[t];
                    weights[v] = weight;
                }
                foreach (var index in m.Bark) barkIndices[b++] = index + start;
                foreach (var index in m.Leaves) leafIndices[l++] = index + start;
            }
            var mesh = new Mesh { name = $"{name} LOD{lod}", indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(barkIndices, 0, false);
            mesh.SetTriangles(leafIndices, 1, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
