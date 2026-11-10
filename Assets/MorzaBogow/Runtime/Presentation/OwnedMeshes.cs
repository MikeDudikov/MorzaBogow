using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>Frees meshes built for one view (a settlement's buildings, an overlay) when the view goes.</summary>
    public sealed class OwnedMeshes : MonoBehaviour
    {
        public List<Mesh> Meshes;

        void OnDestroy()
        {
            if (Meshes == null) return;
            foreach (var mesh in Meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
