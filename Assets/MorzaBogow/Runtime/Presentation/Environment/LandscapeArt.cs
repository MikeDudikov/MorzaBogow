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
