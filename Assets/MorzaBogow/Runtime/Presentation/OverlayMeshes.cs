using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// Flat overlays laid on the sea (firing arcs, a ram's charge lane): meshes in the XZ plane and unlit, transparent
    /// colours made as copies of the scene's ring material, so their band edges stay soft as the rings' do. Presentation only.
    /// </summary>
    public static class OverlayMeshes
    {
        static Mesh lane;
        static readonly Dictionary<(Material, Color), Material> colours = new Dictionary<(Material, Color), Material>();

        /// <summary>A strip 1 wide from Z = 0 to Z = 1, its edges soft across X (V runs across, as on a ring).</summary>
        public static Mesh Lane
        {
            get
            {
                if (lane != null) return lane;
                lane = new Mesh
                {
                    name = "Overlay lane",
                    vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f) },
                    normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                    uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f) },
                    triangles = new[] { 0, 2, 1, 1, 2, 3 },
                };
                lane.RecalculateBounds();
                return lane;
            }
        }

        /// <summary>The ring material in another colour (alpha included); null without a scene material.</summary>
        public static Material Colour(Material ring, Color colour)
        {
            if (ring == null) return null;
            if (colours.TryGetValue((ring, colour), out var made) && made != null) return made;
            made = new Material(ring) { name = $"{ring.name} ({ColorUtility.ToHtmlStringRGBA(colour)})" };
            if (made.HasProperty("_UnlitColor")) made.SetColor("_UnlitColor", colour);
            made.color = colour;
            colours[(ring, colour)] = made;
            return made;
        }

        public static Color WithAlpha(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);
    }
}
