using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>
    /// The logical root of a prefab made by the asset pipeline (Tools/asset_pipeline): what the asset is, which job made
    /// it and from what, and where its visual part lives. Points and markers sit on this root; meshes, materials and the
    /// LODGroup sit under <see cref="Visual"/>, so a new model can replace the visual part without moving the points.
    /// Presentation may read it; the simulation never does.
    /// </summary>
    public sealed class GeneratedAsset : MonoBehaviour
    {
        public string AssetId = string.Empty;
        public string DisplayName = string.Empty;
        public string Category = string.Empty;
        public string ShipClass = string.Empty;
        public string JobId = string.Empty;

        /// <summary>Pipeline version, brief digest, providers and remote task ids, as JSON.</summary>
        [TextArea(2, 8)] public string ProvenanceJson = "{}";

        /// <summary>The visual root: LODGroup with the model's levels of detail.</summary>
        public Transform Visual;
    }
}
