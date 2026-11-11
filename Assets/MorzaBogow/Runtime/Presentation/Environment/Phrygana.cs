using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// The phrygana of an islet (report 2.15, 9.1–9.24): low, rounded, spiny cushions of thorny burnet (Sarcopoterium
    /// spinosum), thyme (Thymbra capitata) and rockrose (Cistus) in patches on the dry ground between the trees and the
    /// maquis, bare earth and rock between them. Most on the islets where people and goats live, where the maquis
    /// gives way to it; fewer in the clearings of the wooded ones. Each cushion is a few crossed cards over a baked
    /// atlas (PhryganaAtlas, built by the editor into Resources/Environment/Phrygana); one mesh per islet, no shadows,
    /// no colliders. Low and stiff, the cushions stand still in the wind (a simplification). Places come from stable
    /// hashes of the islet, never from the game's random numbers. Presentation only.
    /// </summary>
    public static class Phrygana
    {
        public enum Kind { ThornyBurnet, Thyme, Rockrose }

        /// <summary>At most this many cushions on a settled islet and on a wooded one.</summary>
        public const int MostSettled = 140, MostWooded = 70;

        /// <summary>Least distance between cushions, from a tree's trunk and from a bush of the maquis (m).</summary>
        public const float Spacing = 0.7f, ClearOfTrees = 1.6f, ClearOfShrubs = 1.4f;

        /// <summary>Lowest ground a cushion grows on (m): the dry slope, above the beach.</summary>
        public const float LowestGround = 0.45f;

        /// <summary>Patches: the islet is cut into cells this wide, and only cells over the share keep their cushions.</summary>
        public const float PatchCell = 7f, PatchShare = 0.35f;

        /// <summary>Atlas cells: each kind from the side, and the cushions' tops seen from above.</summary>
        public const int BurnetSide = 0, ThymeSide = 1, RockroseSide = 2, CushionTop = 3;

        public static float SizeMin(Kind kind) => kind == Kind.Rockrose ? 0.45f : kind == Kind.Thyme ? 0.25f : 0.4f;

        public static float SizeMax(Kind kind) => kind == Kind.Rockrose ? 0.95f : kind == Kind.Thyme ? 0.55f : 0.8f;

        /// <summary>Height as a share of the width: flat-topped spiny domes, the rockrose more upright.</summary>
        public static float Profile(Kind kind) => kind == Kind.Rockrose ? 0.9f : kind == Kind.Thyme ? 0.85f : 0.75f;

        public struct Cushion
        {
            public Kind Kind;
            public Vector3 Position;
            public float Size, Yaw;
        }

        public sealed class Result
        {
            public Mesh Mesh;
            public readonly List<Cushion> Cushions = new List<Cushion>();
        }

        static Material material;

        /// <summary>The built material, or null before the editor has made it (then nothing is drawn).</summary>
        public static Material Material => material != null ? material : material = Resources.Load<Material>("Environment/Phrygana");

        public static Result Make(float[] outline, float[] lift, uint seed, System.Func<float, float, bool> blocked, IReadOnlyList<Vector2> trees, IReadOnlyList<Vector2> shrubs, bool settled, float shelteredAngle = 0f, float minAway = 0f)
        {
            var result = new Result();
            var most = settled ? MostSettled : MostWooded;
            var random = new StableHash.Sequence(StableHash.Mix(seed, 0xF27Au));
            for (var attempt = 0; attempt < most * 10 && result.Cushions.Count < most; attempt++)
            {
                var angle = random.Range(0f, 2f * Mathf.PI);
                var fraction = random.Range(0.2f, 0.9f);
                var pick = random.Next();
                var size = random.Next();
                var yaw = random.Range(0f, 360f);
                // Not on the port's side, where the jetty, the porters and the slipways are (the olives keep off it too).
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, shelteredAngle * Mathf.Rad2Deg)) < minAway) continue;
                var radius = ShoreShape.At(outline, angle) * fraction;
                var spot = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                if (!InPatch(seed, spot)) continue;
                if (lift != null && ShoreShape.At(lift, angle) >= CoastFeatures.CliffLift && fraction > 0.86f) continue;
                if (blocked != null && blocked(spot.x, spot.y)) continue;
                var ground = ShoreShape.GroundAt(outline, lift, spot.x, spot.y);
                if (ground < LowestGround) continue;
                if (Near(trees, spot, ClearOfTrees) || Near(shrubs, spot, ClearOfShrubs)) continue;
                if (result.Cushions.Exists(other => (new Vector2(other.Position.x, other.Position.z) - spot).sqrMagnitude < Spacing * Spacing)) continue;
                // Burnet and rockrose make about two thirds of the cover (report 9.21), thyme the rest.
                var kind = pick < 0.45f ? Kind.ThornyBurnet : pick < 0.75f ? Kind.Rockrose : Kind.Thyme;
                result.Cushions.Add(new Cushion
                {
                    Kind = kind,
                    Position = new Vector3(spot.x, ground, spot.y),
                    Size = Mathf.Lerp(SizeMin(kind), SizeMax(kind), size),
                    Yaw = yaw,
                });
            }
            result.Mesh = Build(result.Cushions);
            return result;
        }

        /// <summary>Whether a place lies in one of the islet's patches of phrygana (a coarse, stable pattern of cells).</summary>
        public static bool InPatch(uint seed, Vector2 spot)
        {
            var x = (uint)Mathf.FloorToInt(spot.x / PatchCell + 1000f);
            var z = (uint)Mathf.FloorToInt(spot.y / PatchCell + 1000f);
            return StableHash.Unit(StableHash.Mix(seed, x, z)) >= PatchShare;
        }

        static bool Near(IReadOnlyList<Vector2> points, Vector2 spot, float distance)
        {
            if (points == null) return false;
            for (var i = 0; i < points.Count; i++) if ((points[i] - spot).sqrMagnitude < distance * distance) return true;
            return false;
        }

        /// <summary>Draws the cushions under an islet with the built material; nothing before the editor has built it.</summary>
        public static void Attach(Transform parent, Result result)
        {
            if (parent == null || result?.Mesh == null || result.Mesh.vertexCount == 0 || Material == null) return;
            var item = new GameObject("Phrygana");
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = result.Mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            item.AddComponent<OwnedMesh>().Mesh = result.Mesh;
        }

        static Mesh Build(List<Cushion> cushions)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var cushion in cushions)
            {
                var side = cushion.Kind == Kind.Rockrose ? RockroseSide : cushion.Kind == Kind.Thyme ? ThymeSide : BurnetSide;
                var height = cushion.Size * Profile(cushion.Kind);
                for (var k = 0; k < 2; k++)
                    CliffPlants.Upright(vertices, normals, uvs, triangles, cushion.Position, cushion.Yaw + 90f * k, cushion.Size, height, -0.04f, Vector3.up, side);
                CliffPlants.Flat(vertices, normals, uvs, triangles, cushion.Position + Vector3.up * (0.85f * height), cushion.Yaw, 0.9f * cushion.Size, PhryganaAtlas.TopCorner(cushion.Kind), PhryganaAtlas.TopExtent);
            }
            var mesh = new Mesh { name = "Phrygana", indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
