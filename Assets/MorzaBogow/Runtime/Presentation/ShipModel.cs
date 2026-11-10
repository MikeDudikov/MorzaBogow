using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Marks a ship model laid out by the editor's ship prefab builder: the bow at +Z, the waterline of the empty hull at
    /// Y = 0, the class's length from the simulation's catalogue. It names where the water is sampled, where each weapon
    /// post stands and where its missiles leave, and which material slots take the side's colour. It sits on the
    /// prefab's logical root; the meshes are under the visual root. The simulation never reads it.
    /// </summary>
    public sealed class ShipModel : MonoBehaviour
    {
        /// <summary>Points on the waterline where the sea under the hull is sampled.</summary>
        public Transform Bow, Stern, Port, Starboard, Center;

        /// <summary>Where each weapon post of the class stands, in post order.</summary>
        public Transform[] Mounts = System.Array.Empty<Transform>();

        /// <summary>Where each post's missiles leave the ship, in post order.</summary>
        public Transform[] Muzzles = System.Array.Empty<Transform>();

        /// <summary>Renderers and the material slot on each that take the side's colour (sails, banners), on every LOD.</summary>
        public Renderer[] TeamColourRenderers = System.Array.Empty<Renderer>();
        public int[] TeamColourSlots = System.Array.Empty<int>();

        /// <summary>Puts the side's colour on every marked material slot.</summary>
        public void ApplyTeamColour(Material colour)
        {
            if (colour == null) return;
            for (var i = 0; i < TeamColourRenderers.Length && i < TeamColourSlots.Length; i++)
            {
                var renderer = TeamColourRenderers[i];
                if (renderer == null) continue;
                var slots = renderer.sharedMaterials;
                if (TeamColourSlots[i] < 0 || TeamColourSlots[i] >= slots.Length) continue;
                slots[TeamColourSlots[i]] = colour;
                renderer.sharedMaterials = slots;
            }
        }
    }
}
