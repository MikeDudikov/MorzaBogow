using UnityEngine;

namespace MorzaBogow.Editor
{
    /// <summary>
    /// HDRP's mask map from separate maps: R metallic, G ambient occlusion, B detail mask (unused, 0), A smoothness.
    /// A missing map takes its neutral value: no metal, full light, the given smoothness. Shared by the shore art and
    /// the material converter, so every mask in the project is composed the same way.
    /// </summary>
    public static class MaskMaps
    {
        /// <summary>
        /// One mask texel per input texel. Roughness is inverted into smoothness; a gloss map is taken as smoothness
        /// as it is. Each given map holds the same number of texels and is read from its red channel.
        /// </summary>
        public static Color32[] Compose(int texels, Color32[] metallic, Color32[] occlusion, Color32[] roughness, Color32[] gloss, float smoothness = 0.5f)
        {
            var mask = new Color32[texels];
            var plain = (byte)Mathf.RoundToInt(Mathf.Clamp01(smoothness) * 255f);
            for (var i = 0; i < texels; i++)
            {
                var r = metallic != null ? metallic[i].r : (byte)0;
                var g = occlusion != null ? occlusion[i].r : (byte)255;
                var a = roughness != null ? (byte)(255 - roughness[i].r) : gloss != null ? gloss[i].r : plain;
                mask[i] = new Color32(r, g, 0, a);
            }
            return mask;
        }
    }
}
