using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// Materials of the environment built by the editor (MorzaBogow.Editor.EnvironmentArt) and kept in
    /// Assets/Art/Environment/Resources/Environment. Null until they are built; callers fall back to the scene's plain ones.
    /// Wet and beachrock variants are copies of the scene's own shore materials with other values only, so they use
    /// the shader variants the build already has.
    /// </summary>
    public static class LandscapeArt
    {
        static Material oliveBark, oliveLeaves;
        static readonly Dictionary<Material, Material> wet = new Dictionary<Material, Material>();
        static readonly Dictionary<Material, Material> cemented = new Dictionary<Material, Material>();

        public static Material OliveBark => oliveBark != null ? oliveBark : oliveBark = Resources.Load<Material>("Environment/OliveBark");

        public static Material OliveLeaves => oliveLeaves != null ? oliveLeaves : oliveLeaves = Resources.Load<Material>("Environment/OliveLeaves");

        /// <summary>Rock or shingle in the swash: darker and glossy with a film of water.</summary>
        public static Material Wet(Material dry)
        {
            if (dry == null) return null;
            if (wet.TryGetValue(dry, out var material) && material != null) return material;
            material = new Material(dry) { name = dry.name + " (wet)" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.GetColor("_BaseColor") * new Color(0.6f, 0.6f, 0.62f, 1f));
            if (material.HasProperty("_SmoothnessRemapMin")) material.SetFloat("_SmoothnessRemapMin", 0.55f);
            if (material.HasProperty("_SmoothnessRemapMax")) material.SetFloat("_SmoothnessRemapMax", 0.88f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.75f);
            wet[dry] = material;
            return material;
        }

        static readonly Dictionary<(Material, string), Material> tinted = new Dictionary<(Material, string), Material>();

        /// <summary>A copy of a rock material in another colour and finish (values only, the same shader variant).</summary>
        static Material Tinted(Material source, string tag, Color colour, float normal, float smoothMin, float smoothMax, bool metallic = false)
        {
            if (source == null) return null;
            if (tinted.TryGetValue((source, tag), out var material) && material != null) return material;
            material = new Material(source) { name = $"{source.name} ({tag})" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_NormalScale")) material.SetFloat("_NormalScale", normal);
            if (material.HasProperty("_SmoothnessRemapMin")) material.SetFloat("_SmoothnessRemapMin", smoothMin);
            if (material.HasProperty("_SmoothnessRemapMax")) material.SetFloat("_SmoothnessRemapMax", smoothMax);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.5f * (smoothMin + smoothMax));
            var metal = metallic ? 1f : 0f;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
            if (material.HasProperty("_MetallicRemapMin")) material.SetFloat("_MetallicRemapMin", metal);
            if (material.HasProperty("_MetallicRemapMax")) material.SetFloat("_MetallicRemapMax", metal);
            tinted[(source, tag)] = material;
            return material;
        }

        /// <summary>Dark, foliated schist over the marble (Laurion's upper rock, report 4.x).</summary>
        public static Material Schist(Material rock) => Tinted(rock, "schist", new Color(0.4f, 0.42f, 0.39f), 1.8f, 0.15f, 0.35f);

        /// <summary>Rust-coloured iron oxides of the ore-bearing contact (limonite, goethite; Kepper 2004, p. 75).</summary>
        public static Material Limonite(Material rock) => Tinted(rock, "limonite", new Color(0.72f, 0.48f, 0.3f), 1.4f, 0.1f, 0.3f);

        /// <summary>Galena, lead glance: grey and metallic, the only metal on the rock.</summary>
        public static Material Galena(Material rock) => Tinted(rock, "galena", new Color(0.46f, 0.47f, 0.5f), 0.4f, 0.6f, 0.8f, true);

        /// <summary>Cerussite, galena's white weathering crust: chalky, dull.</summary>
        public static Material Cerussite(Material rock) => Tinted(rock, "cerussite", new Color(0.92f, 0.91f, 0.87f), 0.6f, 0.15f, 0.3f);

        /// <summary>Beachrock: the shore's own sand and gravel cemented into a greyer, harder stone.</summary>
        public static Material Beachrock(Material sand)
        {
            if (sand == null) return null;
            if (cemented.TryGetValue(sand, out var material) && material != null) return material;
            material = new Material(sand) { name = sand.name + " (beachrock)" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.GetColor("_BaseColor") * new Color(0.9f, 0.9f, 0.88f, 1f));
            if (material.HasProperty("_NormalScale")) material.SetFloat("_NormalScale", 1.6f);
            cemented[sand] = material;
            return material;
        }
    }
}
