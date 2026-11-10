using MorzaBogow.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MorzaBogow.Tests
{
    /// <summary>
    /// The pose job composes a man's frame from his ship's model and his place in it, and takes his hands' marks in the
    /// world: the same bones as a frame and marks given whole. From afar a man is drawn in fewer pieces, and meshes no
    /// one can tell apart are one mesh.
    /// </summary>
    public sealed class CrewPoseTests
    {
        static FigureInput Rower(float4x4 frame) => new FigureInput
        {
            Frame = frame, Height = 1.66f, Girth = 1f, Action = (int)CrewAction.Row, Phase = 0.3f, Strength = 0.8f, Seated = true,
        };

        static float4x4[] Pose(FigureInput figure, params float4x4[] models)
        {
            var figures = new NativeArray<FigureInput>(1, Allocator.TempJob);
            var bones = new NativeArray<float4x4>(CrewRenderer.Matrices, Allocator.TempJob);
            var shipModels = new NativeArray<float4x4>(math.max(1, models.Length), Allocator.TempJob);
            try
            {
                figures[0] = figure;
                for (var i = 0; i < models.Length; i++) shipModels[i] = models[i];
                // As CrewDirector does: frames from the ships' models first, then the poses.
                var framing = new CrewFrameJob { Figures = figures, Models = shipModels }.Schedule(1, 1);
                new CrewPoseJob { Figures = figures, Bones = bones }.Schedule(1, 1, framing).Complete();
                return bones.ToArray();
            }
            finally
            {
                figures.Dispose();
                bones.Dispose();
                shipModels.Dispose();
            }
        }

        static void AssertSame(float4x4[] a, float4x4[] b, string what)
        {
            for (var i = 0; i < a.Length; i++)
            for (var c = 0; c < 4; c++)
                Assert.That(math.length(a[i][c] - b[i][c]), Is.LessThan(1e-4f), $"{what}: matrix {i}, column {c}");
        }

        [Test]
        public void Pose_TheJobComposesTheFrameFromTheShipAndThePlace()
        {
            var model = float4x4.TRS(new float3(120.5f, 0.3f, -40.2f), quaternion.Euler(0.05f, 1.1f, -0.03f), new float3(1f));
            var place = new float3(1.2f, 1.6f, -3.4f);
            const float facing = 180f;
            var given = Rower(math.mul(model, float4x4.TRS(place, quaternion.RotateY(math.radians(facing)), new float3(1f))));
            var composed = Rower(float4x4.identity);
            composed.ModelSlot = 1;
            composed.Position = place;
            composed.Facing = facing;
            AssertSame(Pose(given, model), Pose(composed, model), "the frame composed in the job");
        }

        [Test]
        public void Pose_HandsMarkedInTheWorldReachAsMarkedInTheStation()
        {
            var frame = float4x4.TRS(new float3(10f, 1.5f, 3f), quaternion.RotateY(0.7f), new float3(1f));
            var left = new float3(-0.15f, 0.35f, 0.45f);
            var right = new float3(0.12f, 0.33f, 0.47f);
            var station = Rower(frame);
            station.Hands = 3;
            station.HandLeft = left;
            station.HandRight = right;
            var world = Rower(frame);
            world.Hands = 3 | 4;
            world.HandLeft = math.transform(frame, left);
            world.HandRight = math.transform(frame, right);
            AssertSame(Pose(station), Pose(world), "hands marked in the world");
        }

        [Test]
        public void Pieces_FromAfarAManIsEightPiecesOnHisOwnBones()
        {
            foreach (CrewRole role in System.Enum.GetValues(typeof(CrewRole)))
            foreach (CrewTrade trade in System.Enum.GetValues(typeof(CrewTrade)))
            {
                var look = CrewLooks.For(7, 1000 + (int)trade, role, 0, trade != CrewTrade.Sailor);
                var far = CrewAboard.Pieces(look, role, trade, 2);
                var near = CrewAboard.Pieces(look, role, trade, 1);
                Assert.That(far.Count, Is.LessThanOrEqualTo(8), $"{role} ({trade}): pieces from afar");
                Assert.That(far.Count, Is.LessThan(near.Count), $"{role} ({trade}): fewer than at the strategic camera's detail");
                foreach (var piece in far)
                {
                    Assert.That(piece.Bone, Is.InRange(0, CrewRenderer.Matrices - 1), $"{role}: a bone or socket of the rig");
                    Assert.That(piece.Mesh, Is.Not.Null, $"{role}: a mesh for every piece");
                }
            }
        }

        [Test]
        public void Pieces_MeshesNoOneCanTellApartAreOneMesh()
        {
            var skin = CrewPalette.Cell.Skin1;
            // Only the hand is built mirrored: a left arm or leg is the right one's mesh.
            Assert.That(CrewMeshes.Get(CrewMeshes.Piece.UpperArm, 1, 0, 1, skin, CrewPalette.Cell.Navy), Is.SameAs(CrewMeshes.Get(CrewMeshes.Piece.UpperArm, 1, 0, 0, skin, CrewPalette.Cell.Navy)));
            Assert.That(CrewMeshes.Get(CrewMeshes.Piece.Thigh, 0, 0, 1, skin, CrewPalette.Cell.Linen), Is.SameAs(CrewMeshes.Get(CrewMeshes.Piece.Thigh, 0, 0, 0, skin, CrewPalette.Cell.Linen)));
            Assert.That(CrewMeshes.Get(CrewMeshes.Piece.Hand, 0, 0, 1, skin, skin), Is.Not.SameAs(CrewMeshes.Get(CrewMeshes.Piece.Hand, 0, 0, 0, skin, skin)), "the hand is built mirrored");
            // A piece of one colour does not depend on the second colour it does not use; the hips use only their second.
            Assert.That(CrewMeshes.Get(CrewMeshes.Piece.Spine, 0, 0, 0, CrewPalette.Cell.Navy, CrewPalette.Cell.Linen), Is.SameAs(CrewMeshes.Get(CrewMeshes.Piece.Spine, 0, 0, 0, CrewPalette.Cell.Navy, CrewPalette.Cell.Wool)));
            Assert.That(CrewMeshes.Get(CrewMeshes.Piece.Pelvis, 0, 0, 0, CrewPalette.Cell.Skin0, CrewPalette.Cell.Navy), Is.SameAs(CrewMeshes.Get(CrewMeshes.Piece.Pelvis, 0, 0, 0, CrewPalette.Cell.Skin3, CrewPalette.Cell.Navy)));
            // Two-coloured pieces keep both colours.
            Assert.That(CrewMeshes.Get(CrewMeshes.Piece.Thigh, 0, 0, 0, skin, CrewPalette.Cell.Navy), Is.Not.SameAs(CrewMeshes.Get(CrewMeshes.Piece.Thigh, 0, 0, 0, skin, CrewPalette.Cell.Madder)));
        }
    }
}
