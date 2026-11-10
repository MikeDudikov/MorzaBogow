using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MorzaBogow.Presentation
{
    /// <summary>The five points on the waterline where a hull reads the sea: the stem, the stern, both sides at the beam and the centre.</summary>
    public static class HullSamplePoints
    {
        public const int Count = 5;
        public const int Bow = 0, Stern = 1, Port = 2, Starboard = 3, Center = 4;

        /// <summary>
        /// Writes a hull's points at <paramref name="offset"/>: half the length ahead of and behind the centre along the
        /// heading, half the beam to either side, and the centre itself.
        /// </summary>
        public static void Write(Vector3 center, Vector3 forward, Vector3 right, float length, float beam, Vector3[] into, int offset)
        {
            var ahead = forward * (length * 0.5f);
            var side = right * (beam * 0.5f);
            into[offset + Bow] = center + ahead;
            into[offset + Stern] = center - ahead;
            into[offset + Port] = center - side;
            into[offset + Starboard] = center + side;
            into[offset + Center] = center;
        }
    }

    /// <summary>Where a batch of water heights comes from: HDRP's simulated sea in the game, a stand-in in tests.</summary>
    public interface IWaterHeights
    {
        /// <summary>
        /// Fills the height of the sea under each of the first <paramref name="count"/> points. False when the sea cannot
        /// be read now; the heights are then left as they were.
        /// </summary>
        bool TryHeights(Vector3[] points, int count, float[] heights);
    }

    /// <summary>
    /// Reads the sea under every visible hull in one batch each frame and turns the heights into hull poses (C09, V06).
    /// Its arrays are kept and only grow, so a frame allocates nothing. Presentation only: the logical position, ranges
    /// and collisions never read these values, so waves cannot change a game result.
    /// </summary>
    public sealed class WaterBatchSampler
    {
        readonly IWaterHeights source;
        Vector3[] points = new Vector3[HullSamplePoints.Count * 16];
        float[] heights = new float[HullSamplePoints.Count * 16];

        public WaterBatchSampler(IWaterHeights source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>Hulls added since the last <see cref="Begin"/>.</summary>
        public int HullCount { get; private set; }

        /// <summary>Points read from the sea so far.</summary>
        public long Samples { get; private set; }

        /// <summary>Points that could not be read because the sea had no data in that frame.</summary>
        public long Failures { get; private set; }

        /// <summary>Batches read so far: one per frame with a visible hull.</summary>
        public long Batches { get; private set; }

        /// <summary>The most hulls read in one batch.</summary>
        public int MostHulls { get; private set; }

        /// <summary>Starts a new batch; the hulls of the previous one are dropped.</summary>
        public void Begin() => HullCount = 0;

        /// <summary>Adds a hull to the batch and returns its index for <see cref="Pose"/> and <see cref="Height"/>.</summary>
        public int Add(Vector3 center, Vector3 forward, Vector3 right, float length, float beam)
        {
            var offset = HullCount * HullSamplePoints.Count;
            if (offset + HullSamplePoints.Count > points.Length)
            {
                Array.Resize(ref points, points.Length * 2);
                Array.Resize(ref heights, heights.Length * 2);
            }
            HullSamplePoints.Write(center, forward, right, length, beam, points, offset);
            return HullCount++;
        }

        /// <summary>Reads the sea under every point of the batch at once. False for an empty batch or an unreadable sea.</summary>
        public bool Sample()
        {
            var count = HullCount * HullSamplePoints.Count;
            if (count == 0) return false;
            if (!source.TryHeights(points, count, heights))
            {
                Failures += count;
                return false;
            }
            Samples += count;
            Batches++;
            MostHulls = Math.Max(MostHulls, HullCount);
            return true;
        }

        /// <summary>One of the points of a hull in the batch (HullSamplePoints.Bow … Center).</summary>
        public Vector3 Point(int hull, int point) => points[hull * HullSamplePoints.Count + point];

        /// <summary>The sea's height under one of the points of a hull, from the last batch read.</summary>
        public float Height(int hull, int point) => heights[hull * HullSamplePoints.Count + point];

        /// <summary>The heave, pitch and roll the sea gives a hull of the batch.</summary>
        public HullPose Pose(int hull, float length, float beam) => HullPose.FromHeights(
            Height(hull, HullSamplePoints.Bow), Height(hull, HullSamplePoints.Stern), Height(hull, HullSamplePoints.Port),
            Height(hull, HullSamplePoints.Starboard), Height(hull, HullSamplePoints.Center), length, beam);
    }

    /// <summary>
    /// HDRP's simulated sea, read for a whole batch of points in one Burst job (WaterSimulationSearchJob), the way
    /// HDRP's own Water samples do. The search starts from the target itself and includes deformation, so a hull rides
    /// its own bow wave and other ships' wakes. The native buffers are kept between frames, grow when a batch needs
    /// more, and are released by <see cref="Dispose"/>. The job is completed before it returns: HDRP updates the sea
    /// while it renders, so a search must never run into the next frame. There is no fallback to single queries:
    /// WaterSurface.ProjectPointOnWaterSurface reads the same search data, so it fails exactly when this does.
    /// </summary>
    public sealed class HdrpWaterHeights : IWaterHeights, IDisposable
    {
        public const float Error = 0.05f;
        public const int MaxIterations = 4;
        const int BatchSize = 16;

        readonly WaterSurface surface;
        NativeArray<float3> targets, projected, normals, candidates, directions;
        NativeArray<float> errors;
        NativeArray<int> steps;
        int capacity;
        bool disposed;

        public HdrpWaterHeights(WaterSurface surface)
        {
            this.surface = surface;
        }

        /// <summary>How many times the native buffers were made; it stays small because they are kept and only grow.</summary>
        public int BufferAllocations { get; private set; }

        /// <summary>False without a sea or once released: a released source never allocates its buffers again.</summary>
        public bool TryHeights(Vector3[] points, int count, float[] heights)
        {
            if (disposed || surface == null) return false;
            if (count <= 0) return true;
            var data = new WaterSimSearchData();
            if (!surface.FillWaterSearchData(ref data)) return false;

            Reserve(count);
            for (var i = 0; i < count; i++) targets[i] = new float3(points[i].x, 0f, points[i].z);
            var job = new WaterSimulationSearchJob
            {
                simSearchData = data,
                targetPositionWSBuffer = targets,
                startPositionWSBuffer = targets,
                error = Error,
                maxIterations = MaxIterations,
                includeDeformation = true,
                excludeSimulation = false,
                outputNormal = false,
                projectedPositionWSBuffer = projected,
                normalWSBuffer = normals,
                errorBuffer = errors,
                candidateLocationWSBuffer = candidates,
                directionBuffer = directions,
                stepCountBuffer = steps,
            };
            job.Schedule(count, BatchSize).Complete();
            for (var i = 0; i < count; i++) heights[i] = projected[i].y;
            return true;
        }

        void Reserve(int count)
        {
            if (count <= capacity) return;
            Release();
            capacity = Mathf.NextPowerOfTwo(Math.Max(count, 64));
            // Every buffer of the job has to exist when it is scheduled, the unused normals too.
            targets = new NativeArray<float3>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            projected = new NativeArray<float3>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            candidates = new NativeArray<float3>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            directions = new NativeArray<float3>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            errors = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            steps = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            BufferAllocations++;
        }

        void Release()
        {
            if (targets.IsCreated) targets.Dispose();
            if (projected.IsCreated) projected.Dispose();
            if (normals.IsCreated) normals.Dispose();
            if (candidates.IsCreated) candidates.Dispose();
            if (directions.IsCreated) directions.Dispose();
            if (errors.IsCreated) errors.Dispose();
            if (steps.IsCreated) steps.Dispose();
            capacity = 0;
        }

        /// <summary>Releases the native buffers; safe to call more than once.</summary>
        public void Dispose()
        {
            disposed = true;
            Release();
        }
    }
}
