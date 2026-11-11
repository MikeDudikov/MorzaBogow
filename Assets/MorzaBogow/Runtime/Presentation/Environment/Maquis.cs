using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// The maquis of an islet: kermes oaks and lentisks (report 2.14, 8.1–8.14) on the same generator, bones and match
    /// wind as the olives. Lentisks keep to the band above the shore, kermes oaks to the slopes inland. On the islets of
    /// ports and free shores, where people and their goats live, the bushes are fewer (8.6: lower and sparser by the
    /// folds); on the wooded hinterland islets they are the undergrowth of the pines. Never on the beach, on a cliff's
    /// rim, on a building's ground or at a tree's foot. Places come from stable hashes of the grove, never from the
    /// game's random numbers, and leave the trees' own draws as they were. Presentation only.
    /// </summary>
    public static class Maquis
    {
        /// <summary>At most this many bushes on a settled islet and on a wooded one.</summary>
        public const int MostSettled = 10, MostWooded = 22;

        /// <summary>Least distance between bushes, and from a bush to a tree's trunk (m).</summary>
        public const float Spacing = 2.2f, ClearOfTrees = 2f;

        /// <summary>Lowest ground a bush grows on (m): above the beach and the swash.</summary>
        public const float LowestGround = 0.35f;

        /// <summary>Share of the shore's radius: lentisks between these, kermes oaks further in.</summary>
        public const float ShoreBandInner = 0.7f, ShoreBandOuter = 0.88f, InlandInner = 0.3f;

        /// <summary>Where the bushes of one islet stand: species, stable id and place in the islet's space.</summary>
        public static List<(TreeSpecies species, uint id, Vector3 position, float yaw)> Places(float[] outline, float[] lift, uint seed, System.Func<float, float, bool> blocked, IReadOnlyList<Vector2> trees, bool settled, float shelteredAngle = 0f, float minAway = 0f)
        {
            var places = new List<(TreeSpecies species, uint id, Vector3 position, float yaw)>();
            var most = settled ? MostSettled : MostWooded;
            var random = new StableHash.Sequence(StableHash.Mix(seed, 0x3A9u));
            for (var attempt = 0; attempt < most * 8 && places.Count < most; attempt++)
            {
                var angle = random.Range(0f, 2f * Mathf.PI);
                var near = random.Next() < (settled ? 0.65f : 0.35f);
                var fraction = near ? random.Range(ShoreBandInner, ShoreBandOuter) : random.Range(InlandInner, ShoreBandInner);
                var yaw = random.Range(0f, 360f);
                var mixed = random.Next();
                // Not on the port's side, where the jetty, the porters and the slipways are (the olives keep off it too).
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, shelteredAngle * Mathf.Rad2Deg)) < minAway) continue;
                var radius = ShoreShape.At(outline, angle) * fraction;
                var spot = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                // Not on a cliff's rim: the face drops away from fraction 0.93.
                if (lift != null && ShoreShape.At(lift, angle) >= CoastFeatures.CliffLift && fraction > 0.86f) continue;
                if (blocked != null && blocked(spot.x, spot.y)) continue;
                var ground = ShoreShape.GroundAt(outline, lift, spot.x, spot.y);
                if (ground < LowestGround) continue;
                if (trees != null && Near(trees, spot, ClearOfTrees)) continue;
                if (places.Exists(other => (new Vector2(other.position.x, other.position.z) - spot).sqrMagnitude < Spacing * Spacing)) continue;
                // Lentisks by the shore, kermes oaks inland, with a quarter mixed into either band.
                var lentisk = near ? mixed < 0.75f : mixed < 0.25f;
                var id = StableHash.Mix(seed, 0x3A9u, (uint)places.Count);
                places.Add((lentisk ? TreeSpecies.Lentisk : TreeSpecies.KermesOak, id, new Vector3(spot.x, ground - 0.08f, spot.y), yaw));
            }
            return places;
        }

        static bool Near(IReadOnlyList<Vector2> points, Vector2 spot, float distance)
        {
            for (var i = 0; i < points.Count; i++) if ((points[i] - spot).sqrMagnitude < distance * distance) return true;
            return false;
        }

        /// <summary>
        /// Plants the maquis of an islet under its parent, one grove per species (each has its own leaf atlas). Nothing
        /// while the old plain olives are drawn (the A/B measurement compares without it), or before the editor has
        /// built the leaf materials. Returns how many bushes it planted.
        /// </summary>
        public static int Plant(Transform parent, float[] outline, float[] lift, uint seed, System.Func<float, float, bool> blocked, IReadOnlyList<Vector2> trees, bool settled, float shelteredAngle = 0f, float minAway = 0f)
        {
            if (parent == null || Vegetation.PlainOlives || LandscapeArt.OliveBark == null) return 0;
            var leeward = parent.InverseTransformDirection(Vegetation.Leeward);
            var places = Places(outline, lift, seed, blocked, trees, settled, shelteredAngle, minAway);
            // The phrygana in patches between the trees and the bushes (most where people and their goats live).
            var bushes = places.ConvertAll(place => new Vector2(place.position.x, place.position.z));
            Phrygana.Attach(parent, Phrygana.Make(outline, lift, seed, blocked, trees, bushes, settled, shelteredAngle, minAway));
            var planted = 0;
            foreach (var species in TreeSpecies.Shrubs)
            {
                var leaves = LandscapeArt.LeavesFor(species);
                if (leaves == null) continue;
                var builder = new TreeGroveBuilder();
                foreach (var place in places)
                {
                    if (place.species != species) continue;
                    var angle = Mathf.Atan2(place.position.z, place.position.x);
                    var exposure = Mathf.Clamp01(1f - (ShoreShape.At(outline, angle) - new Vector2(place.position.x, place.position.z).magnitude) / 25f);
                    builder.Add(species, place.id, place.position, place.yaw, leeward, exposure);
                }
                if (builder.Count == 0) continue;
                builder.Build(parent, $"Maquis ({species.Name})", LandscapeArt.OliveBark, leaves);
                planted += builder.Count;
            }
            return planted;
        }
    }
}
