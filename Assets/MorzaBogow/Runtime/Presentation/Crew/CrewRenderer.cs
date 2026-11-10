using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MorzaBogow.Presentation
{
    /// <summary>One piece of a man to draw: its mesh, the bone or socket it rides on, its place on that bone, its slot.</summary>
    public struct CrewPart
    {
        public int Figure, Bone;
        public float4x4 Local;
        /// <summary>The piece's index in the frame's instance buffer: its mesh's start plus its place among that mesh's pieces.</summary>
        public int Slot;
    }

    /// <summary>
    /// Draws the crews without a scene object per man: every piece of every man drawn this frame is one instance of its
    /// mesh, placed by a Burst job from its man's bones, and each mesh is one instanced draw of HDRP/Lit in batches of
    /// at most 511 (Graphics.RenderMeshInstanced), with the crews' one palette material, made with instancing enabled as
    /// an asset so its instancing variant is in the build (Resources/Crew/CrewBody). Shadows only up close (LOD0).
    /// </summary>
    public sealed class CrewRenderer : System.IDisposable
    {
        /// <summary>Bones of the rig and three sockets: the weapon in hand or slung, the shield, a second item.</summary>
        public const int Sockets = 3, Matrices = CrewBody.Bones + Sockets;
        public const int Weapon = CrewBody.Bones, ShieldSocket = CrewBody.Bones + 1, Extra = CrewBody.Bones + 2;
        const int Batch = 511;

        Material material;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Dictionary<Mesh, int> bucketOf = new Dictionary<Mesh, int>();
        readonly List<int> bucketCount = new List<int>();
        readonly List<CrewPart> pending = new List<CrewPart>();
        int[] bucketStart = System.Array.Empty<int>();
        NativeArray<CrewPart> parts;
        NativeArray<float4x4> instances;
        int partCount;
        Bounds bounds;

        public int Draws { get; private set; }
        public int Instances => partCount;
        public int MeshesInUse => meshes.Count;
        public Material Material => material;

        public CrewRenderer(Material crewMaterial = null)
        {
            material = crewMaterial != null ? crewMaterial : LoadMaterial();
        }

        /// <summary>
        /// The crews' material: the asset in Resources (instancing on, palette and mask), or in tests and tools without
        /// it a runtime copy of HDRP/Lit with the palette made in code.
        /// </summary>
        public static Material LoadMaterial()
        {
            var asset = Resources.Load<Material>("Crew/CrewBody");
            if (asset != null) return asset;
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null) return null;
            var (albedo, mask) = CrewPalette.Textures();
            var material = new Material(shader) { name = "Crew body (runtime)", enableInstancing = true };
            material.SetTexture("_BaseColorMap", albedo);
            material.SetTexture("_MaskMap", mask);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_MetallicRemapMax", 1f);
            material.SetFloat("_SmoothnessRemapMax", 1f);
            material.EnableKeyword("_MASKMAP");
            return material;
        }

        // Building the frame's list -------------------------------------------------------------------------------
        /// <summary>Starts a new list of pieces (call when the men drawn, their pieces or the level of detail change).</summary>
        public void BeginList()
        {
            pending.Clear();
            for (var i = 0; i < bucketCount.Count; i++) bucketCount[i] = 0;
        }

        public void Add(int figure, int bone, Mesh mesh, float4x4 local)
        {
            if (mesh == null) return;
            if (!bucketOf.TryGetValue(mesh, out var bucket))
            {
                bucket = meshes.Count;
                meshes.Add(mesh);
                bucketOf[mesh] = bucket;
                bucketCount.Add(0);
            }
            pending.Add(new CrewPart { Figure = figure, Bone = bone, Local = local, Slot = bucket });
            bucketCount[bucket]++;
        }

        // The quick list, for a frame whose men changed (a battle, every frame): no lookup per piece and no copy between.
        /// <summary>The bucket (one draw's mesh) of a mesh, made the first time it is asked for.</summary>
        public int Bucket(Mesh mesh)
        {
            if (bucketOf.TryGetValue(mesh, out var bucket)) return bucket;
            bucket = meshes.Count;
            meshes.Add(mesh);
            bucketOf[mesh] = bucket;
            bucketCount.Add(0);
            return bucket;
        }

        /// <summary>First pass: one more piece in this bucket (after BeginList).</summary>
        public void Count(int bucket) => bucketCount[bucket]++;

        /// <summary>Between the passes: where each bucket's pieces start, and room for all of them.</summary>
        public void Layout()
        {
            if (bucketStart.Length < meshes.Count) bucketStart = new int[meshes.Count + 16];
            if (cursor.Length < meshes.Count) cursor = new int[meshes.Count + 16];
            var start = 0;
            for (var b = 0; b < meshes.Count; b++)
            {
                bucketStart[b] = start;
                cursor[b] = start;
                start += bucketCount[b];
            }
            partCount = start;
            if (!parts.IsCreated || parts.Length < partCount)
            {
                if (parts.IsCreated) parts.Dispose();
                if (instances.IsCreated) instances.Dispose();
                var capacity = Mathf.Max(256, Mathf.NextPowerOfTwo(partCount));
                parts = new NativeArray<CrewPart>(capacity, Allocator.Persistent);
                instances = new NativeArray<float4x4>(capacity, Allocator.Persistent);
            }
        }

        /// <summary>Second pass: a piece straight into its slot (the same order and buckets as counted).</summary>
        public void Place(int figure, int bone, int bucket, float4x4 local)
        {
            var slot = cursor[bucket]++;
            parts[slot] = new CrewPart { Figure = figure, Bone = bone, Local = local, Slot = slot };
        }

        int[] cursor = System.Array.Empty<int>();

        /// <summary>Lays out the instance buffer: each mesh's pieces together, so one draw covers them.</summary>
        public void EndList()
        {
            if (bucketStart.Length < meshes.Count) bucketStart = new int[meshes.Count + 16];
            var start = 0;
            for (var b = 0; b < meshes.Count; b++)
            {
                bucketStart[b] = start;
                start += bucketCount[b];
            }
            partCount = pending.Count;
            if (!parts.IsCreated || parts.Length < partCount)
            {
                if (parts.IsCreated) parts.Dispose();
                if (instances.IsCreated) instances.Dispose();
                var capacity = Mathf.Max(256, Mathf.NextPowerOfTwo(partCount));
                parts = new NativeArray<CrewPart>(capacity, Allocator.Persistent);
                instances = new NativeArray<float4x4>(capacity, Allocator.Persistent);
            }
            var placed = new int[meshes.Count];
            for (var i = 0; i < pending.Count; i++)
            {
                var part = pending[i];
                var bucket = part.Slot;
                part.Slot = bucketStart[bucket] + placed[bucket]++;
                parts[i] = part;
            }
        }

        // Each frame --------------------------------------------------------------------------------------------------
        /// <summary>Places every piece from its man's bones (a Burst job), then draws each mesh's pieces at once.</summary>
        public void Draw(NativeArray<float4x4> bones, Camera camera, Bounds worldBounds, bool shadows, int layer)
        {
            Draws = 0;
            if (material == null || partCount == 0) return;
            new CrewPlaceJob { Parts = parts, Bones = bones, Instances = instances }.Schedule(partCount, 128).Complete();
            bounds = worldBounds;
            var rp = new RenderParams(material)
            {
                camera = camera,
                worldBounds = bounds,
                layer = layer,
                shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                motionVectorMode = MotionVectorGenerationMode.Camera,
                lightProbeUsage = LightProbeUsage.BlendProbes,
            };
            var matrices = instances.Reinterpret<Matrix4x4>();
            for (var b = 0; b < meshes.Count; b++)
            {
                var count = bucketCount[b];
                if (count == 0) continue;
                for (var offset = 0; offset < count; offset += Batch)
                {
                    Graphics.RenderMeshInstanced(rp, meshes[b], 0, matrices, Mathf.Min(Batch, count - offset), bucketStart[b] + offset);
                    Draws++;
                }
            }
        }

        public void Dispose()
        {
            if (parts.IsCreated) parts.Dispose();
            if (instances.IsCreated) instances.Dispose();
            partCount = 0;
        }
    }

    /// <summary>Each piece's world matrix: its man's bone or socket times its place on it.</summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct CrewPlaceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CrewPart> Parts;
        [ReadOnly] public NativeArray<float4x4> Bones;
        [NativeDisableParallelForRestriction] public NativeArray<float4x4> Instances;

        public void Execute(int index)
        {
            var part = Parts[index];
            Instances[part.Slot] = math.mul(Bones[part.Figure * CrewRenderer.Matrices + part.Bone], part.Local);
        }
    }
}
