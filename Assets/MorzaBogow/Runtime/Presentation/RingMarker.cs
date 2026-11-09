using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>Thin flat ring for selection and target markers, so a marker never hides what it marks.</summary>
    public static class RingMarker
    {
        const int Segments = 64;
        const float InnerRadius = 0.93f;
        static Mesh mesh;

        static Mesh Shared
        {
            get
            {
                if (mesh != null) return mesh;
                var vertices = new Vector3[(Segments + 1) * 2];
                var normals = new Vector3[vertices.Length];
                var coordinates = new Vector2[vertices.Length];
                var triangles = new int[Segments * 6];
                for (var i = 0; i <= Segments; i++)
                {
                    var angle = i * Mathf.PI * 2f / Segments;
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices[i * 2] = direction * InnerRadius;
                    vertices[i * 2 + 1] = direction;
                    normals[i * 2] = normals[i * 2 + 1] = Vector3.up;

                    // V runs across the band, so the material's alpha profile softens both edges.
                    var along = (float)i / Segments;
                    coordinates[i * 2] = new Vector2(along, 0f);
                    coordinates[i * 2 + 1] = new Vector2(along, 1f);
                    if (i == Segments) break;
                    var t = i * 6;
                    triangles[t] = i * 2;
                    triangles[t + 1] = i * 2 + 2;
                    triangles[t + 2] = i * 2 + 1;
                    triangles[t + 3] = i * 2 + 1;
                    triangles[t + 4] = i * 2 + 2;
                    triangles[t + 5] = i * 2 + 3;
                }
                mesh = new Mesh { name = "Ring marker", vertices = vertices, normals = normals, uv = coordinates, triangles = triangles };
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public static Transform Create(string name, Material material, Transform parent, float radius)
        {
            var ring = new GameObject(name);
            ring.AddComponent<MeshFilter>().sharedMesh = Shared;
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            ring.transform.SetParent(parent, false);
            ring.transform.localScale = new Vector3(radius, 1f, radius);
            return ring.transform;
        }
    }
}
