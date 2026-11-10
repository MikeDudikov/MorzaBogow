using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    public enum TreeKind { StonePine, Cypress, Olive }

    /// <summary>
    /// Mediterranean trees built from simple shapes into one mesh each, with two parts: bark (submesh 0) and
    /// foliage (submesh 1). The stone pine has a leaning trunk and a flat umbrella crown of several flattened
    /// masses; the cypress is a tall dark spire; the olive is low, with a twisted trunk and a broad, open crown.
    /// Variants differ in proportions, so groves do not repeat. Presentation only.
    /// </summary>
    public static class TreeModels
    {
        const int Variants = 4;

        /// <summary>
        /// Groves are placed in model units where a stone pine of scale 1 is 7.5-10 m. Every grove is multiplied by this
        /// on top, so trees stand at their real size beside 12-26 m hulls: a stone pine 11-18 m, a cypress 14-22 m.
        /// </summary>
        public const float LifeSize = 2.2f;
        static readonly Dictionary<(TreeKind, int), Mesh> cache = new Dictionary<(TreeKind, int), Mesh>();
        static Mesh sphere, cylinder, capsule;

        public static Mesh Get(TreeKind kind, int variant)
        {
            variant = ((variant % Variants) + Variants) % Variants;
            if (cache.TryGetValue((kind, variant), out var mesh) && mesh != null) return mesh;
            mesh = kind == TreeKind.StonePine ? StonePine(variant) : kind == TreeKind.Cypress ? Cypress(variant) : Olive(variant);
            cache[(kind, variant)] = mesh;
            return mesh;
        }

        static void LoadPrimitives()
        {
            if (sphere != null) return;
            sphere = Primitive(PrimitiveType.Sphere);
            cylinder = Primitive(PrimitiveType.Cylinder);
            capsule = Primitive(PrimitiveType.Capsule);
        }

        static Mesh Primitive(PrimitiveType type)
        {
            var item = GameObject.CreatePrimitive(type);
            var mesh = item.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
            return mesh;
        }

        static Mesh StonePine(int variant)
        {
            LoadPrimitives();
            var random = new System.Random(1301 + variant * 7);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var bark = new List<CombineInstance>();
            var leaves = new List<CombineInstance>();

            // A trunk leaning a little, then forking into two or three limbs under the crown.
            var height = R(7.5f, 10f);
            var lean = R(4f, 12f);
            var trunkTop = Quaternion.Euler(lean, 0f, R(-4f, 4f)) * new Vector3(0f, height * 0.72f, 0f);
            bark.Add(Piece(cylinder, trunkTop * 0.5f, Quaternion.FromToRotation(Vector3.up, trunkTop), new Vector3(0.55f, trunkTop.magnitude * 0.5f, 0.55f)));
            var limbs = 2 + variant % 2;
            for (var i = 0; i < limbs; i++)
            {
                var direction = Quaternion.Euler(0f, i * 360f / limbs + R(-20f, 20f), 0f) * new Vector3(1f, 1.1f, 0f).normalized;
                var limb = direction * R(1.6f, 2.4f);
                bark.Add(Piece(cylinder, trunkTop + limb * 0.5f, Quaternion.FromToRotation(Vector3.up, limb), new Vector3(0.3f, limb.magnitude * 0.5f, 0.3f)));
            }

            // The umbrella: one broad flat mass over the top and a ring of smaller ones at its rim.
            var crown = trunkTop + new Vector3(0f, R(1.4f, 2f), 0f);
            var width = R(6f, 8f);
            leaves.Add(Piece(sphere, crown, Quaternion.identity, new Vector3(width, R(1.7f, 2.2f), width * R(0.85f, 1f))));
            var masses = 5 + variant;
            for (var i = 0; i < masses; i++)
            {
                var angle = i * Mathf.PI * 2f / masses + R(-0.3f, 0.3f);
                var at = crown + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width * R(0.28f, 0.38f) + new Vector3(0f, R(-0.6f, 0.3f), 0f);
                var size = width * R(0.38f, 0.5f);
                leaves.Add(Piece(sphere, at, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(size, size * R(0.32f, 0.42f), size * R(0.8f, 1f))));
            }
            return Build($"Stone pine {variant}", bark, leaves);
        }

        static Mesh Cypress(int variant)
        {
            LoadPrimitives();
            var random = new System.Random(7919 + variant * 13);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var bark = new List<CombineInstance>();
            var leaves = new List<CombineInstance>();
            var height = R(9f, 13f);
            bark.Add(Piece(cylinder, new Vector3(0f, 0.8f, 0f), Quaternion.identity, new Vector3(0.4f, 0.8f, 0.4f)));
            // A narrow spire of stacked, overlapping masses, widest a third of the way up.
            leaves.Add(Piece(capsule, new Vector3(0f, height * 0.5f + 0.6f, 0f), Quaternion.identity, new Vector3(R(1.8f, 2.3f), height * 0.5f, R(1.8f, 2.3f))));
            leaves.Add(Piece(capsule, new Vector3(R(-0.2f, 0.2f), height * 0.38f + 0.6f, R(-0.2f, 0.2f)), Quaternion.identity, new Vector3(R(2.2f, 2.6f), height * 0.3f, R(2.2f, 2.6f))));
            leaves.Add(Piece(capsule, new Vector3(0f, height * 0.82f + 0.6f, 0f), Quaternion.identity, new Vector3(R(1.1f, 1.4f), height * 0.22f, R(1.1f, 1.4f))));
            return Build($"Cypress {variant}", bark, leaves);
        }

        /// <summary>
        /// The olive: a short gnarled trunk that twists as it rises and splits into two or three limbs, each under a
        /// rounded mass of leaves, with a broader mass over the middle. About 4.5 model units high, so at a grove scale
        /// of 0.6 it stands some 6 m: low beside a stone pine, and no wall in front of a moored hull.
        /// </summary>
        static Mesh Olive(int variant)
        {
            LoadPrimitives();
            var random = new System.Random(4271 + variant * 11);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var bark = new List<CombineInstance>();
            var leaves = new List<CombineInstance>();

            // Two trunk segments leaning different ways: the twist of an old olive.
            var lower = Quaternion.Euler(R(8f, 16f), R(0f, 360f), 0f) * new Vector3(0f, R(1.1f, 1.4f), 0f);
            bark.Add(Piece(cylinder, lower * 0.5f, Quaternion.FromToRotation(Vector3.up, lower), new Vector3(0.75f, lower.magnitude * 0.5f, 0.65f)));
            var upper = Quaternion.Euler(R(10f, 20f), R(0f, 360f), 0f) * new Vector3(0f, R(0.8f, 1.1f), 0f);
            bark.Add(Piece(cylinder, lower + upper * 0.5f, Quaternion.FromToRotation(Vector3.up, upper), new Vector3(0.5f, upper.magnitude * 0.5f, 0.45f)));
            var fork = lower + upper;

            var limbs = 2 + variant % 2;
            var middle = Vector3.zero;
            for (var i = 0; i < limbs; i++)
            {
                var direction = Quaternion.Euler(0f, i * 360f / limbs + R(-25f, 25f), 0f) * new Vector3(1f, 0.9f, 0f).normalized;
                var limb = direction * R(1.1f, 1.6f);
                bark.Add(Piece(cylinder, fork + limb * 0.5f, Quaternion.FromToRotation(Vector3.up, limb), new Vector3(0.28f, limb.magnitude * 0.5f, 0.28f)));
                var size = R(2.4f, 3.1f);
                leaves.Add(Piece(sphere, fork + limb + new Vector3(0f, 0.5f, 0f), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(size, size * R(0.6f, 0.75f), size * R(0.8f, 1f))));
                middle += fork + limb;
            }
            middle /= limbs;
            var width = R(3f, 3.6f);
            leaves.Add(Piece(sphere, middle + new Vector3(0f, R(0.7f, 1f), 0f), Quaternion.identity, new Vector3(width, width * 0.55f, width * R(0.85f, 1f))));
            return Build($"Olive {variant}", bark, leaves);
        }

        static void Discard(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
        }

        static CombineInstance Piece(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale) =>
            new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) };

        static Mesh Build(string name, List<CombineInstance> bark, List<CombineInstance> leaves)
        {
            var barkMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            barkMesh.CombineMeshes(bark.ToArray(), true, true);
            var leafMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            leafMesh.CombineMeshes(leaves.ToArray(), true, true);
            var tree = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            tree.CombineMeshes(new[]
            {
                new CombineInstance { mesh = barkMesh, transform = Matrix4x4.identity },
                new CombineInstance { mesh = leafMesh, transform = Matrix4x4.identity },
            }, false, true);
            tree.RecalculateBounds();
            Discard(barkMesh);
            Discard(leafMesh);
            return tree;
        }

        /// <summary>
        /// Gathers trees into one mesh per grove with the same two parts, so a wooded island is a single draw for
        /// bark and one for foliage.
        /// </summary>
        public sealed class Grove
        {
            readonly List<CombineInstance> bark = new List<CombineInstance>(), leaves = new List<CombineInstance>();
            public int Count { get; private set; }

            public void Add(TreeKind kind, int variant, Vector3 position, float yaw, float scale)
            {
                var mesh = Get(kind, variant);
                var placement = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * (scale * LifeSize));
                bark.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = placement });
                leaves.Add(new CombineInstance { mesh = mesh, subMeshIndex = 1, transform = placement });
                Count++;
            }

            public Mesh Build(string name) => Count == 0 ? null : TreeModels.Build(name, bark, leaves);
        }
    }
}
