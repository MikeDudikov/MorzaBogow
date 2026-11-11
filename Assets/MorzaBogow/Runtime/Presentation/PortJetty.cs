using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// A timber jetty of a port or a free shore (docs/art/VisualReworkManifest.json, port.jetty: "timber jetty or stone
    /// quay"): a deck of planks laid across three stringers, carried on bents of tarred piles driven into the bed, each bent
    /// with its cap beam and a diagonal brace, a kerb along both edges and two bollards at the head. It keeps the old
    /// jetty's place exactly: from Start to Reach toward the port, 4 m wide, its deck's top at 1.375 m, where the dock
    /// gangs walk (BattleController.PortPlaces). One mesh, one renderer named "Jetty". Presentation only.
    /// </summary>
    public static class PortJetty
    {
        public const float Width = 4f, Top = 1.375f;
        const float Plank = 0.28f, Gap = 0.035f, Deck = 0.1f, Bed = -2.6f;

        /// <param name="parent">The land's root at its centre; the jetty runs along its x toward the port.</param>
        /// <param name="towardPort">+1 or -1: the side of the land facing its port.</param>
        /// <param name="start">Where the jetty leaves the beach, metres from the land's centre.</param>
        /// <param name="reach">Where it ends, beside a moored hull.</param>
        public static GameObject Build(Transform parent, float towardPort, float start, float reach, PlaceholderMaterials materials)
        {
            var b = new GreekShips.MeshBuilder(null);
            Vector3 At(float u, float y, float z) => new Vector3(towardPort * u, y, z);
            var length = reach - start;
            var middle = (start + reach) * 0.5f;

            // Planks across the stringers with narrow gaps between them, each a little off true.
            var count = Mathf.FloorToInt(length / (Plank + Gap));
            var used = count * (Plank + Gap) - Gap;
            for (var i = 0; i < count; i++)
            {
                var u = start + (length - used) * 0.5f + i * (Plank + Gap) + Plank * 0.5f;
                var tilt = Mathf.Sin(i * 12.9898f) * 0.6f;
                b.Box(GreekMaterial.Wood, At(u, Top - Deck * 0.5f, 0f), new Vector3(Plank * 0.5f, Deck * 0.5f, Width * 0.5f - 0.02f), Quaternion.Euler(0f, tilt, 0f));
            }
            // A kerb along both edges, over the plank ends.
            foreach (var side in new[] { -1f, 1f })
                b.Box(GreekMaterial.Wood, At(middle, Top + 0.06f, side * (Width * 0.5f - 0.07f)), new Vector3(length * 0.5f, 0.06f, 0.07f), Quaternion.identity);
            // Three stringers under the deck.
            foreach (var z in new[] { -1.55f, 0f, 1.55f })
                b.Box(GreekMaterial.Wood, At(middle, Top - Deck - 0.13f, z), new Vector3(length * 0.5f, 0.13f, 0.09f), Quaternion.identity);

            // Bents of piles about every 3 m: two tarred piles, a cap beam under the stringers, a brace across.
            var bents = Mathf.Max(2, Mathf.CeilToInt(length / 3.1f) + 1);
            for (var k = 0; k < bents; k++)
            {
                var u = Mathf.Lerp(start + 0.6f, reach - 0.35f, k / (bents - 1f));
                var cap = Top - Deck - 0.26f;
                foreach (var side in new[] { -1f, 1f })
                    b.Tube(GreekMaterial.Pitch, new List<Vector3> { At(u, Bed, side * 1.8f), At(u, cap, side * 1.8f) }, 0.17f, 0.15f, 7);
                b.Box(GreekMaterial.Wood, At(u, cap - 0.09f, 0f), new Vector3(0.11f, 0.1f, Width * 0.5f - 0.05f), Quaternion.identity);
                var from = At(u, -0.5f, (k % 2 == 0 ? -1f : 1f) * 1.8f);
                var to = At(u, cap - 0.25f, (k % 2 == 0 ? 1f : -1f) * 1.8f);
                b.Tube(GreekMaterial.Wood, new List<Vector3> { from, to }, 0.07f, 0.07f, 5);
            }
            // Two bollards at the head for the mooring lines.
            foreach (var side in new[] { -1f, 1f })
            {
                var foot = At(reach - 0.45f, Top, side * 1.65f);
                b.Lathe(GreekMaterial.Wood, new[] { (0f, 0.14f), (0.42f, 0.12f), (0.46f, 0.16f), (0.55f, 0.15f), (0.58f, 0f) }, 8, foot);
            }

            var mesh = b.Build(GreekMaterial.Wood, GreekMaterial.Pitch);
            mesh.Mesh.name = "Jetty";
            var materialList = new Material[mesh.Slots.Length];
            for (var i = 0; i < materialList.Length; i++) materialList[i] = PortBuildings.MaterialFor(mesh.Slots[i], materials);
            var jetty = GreekShips.Attach(parent, "Jetty", mesh, materialList);
            jetty.gameObject.AddComponent<OwnedMeshes>().Meshes = new List<Mesh> { mesh.Mesh };
            return jetty.gameObject;
        }
    }
}
