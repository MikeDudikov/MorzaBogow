using System;
using System.Collections.Generic;
using UnityEngine;

namespace MorzaBogow.Presentation.Landscape
{
    /// <summary>
    /// A silver-bearing outcrop of the Laurion type as worked in the 12th century BC (E1). Report 4.1–4.13:
    /// <list type="bullet">
    /// <item>pale marble below, dark foliated schist above, and between them the contact where the ore lies: lead
    /// glance (galena, grey and metallic) and its white weathering crust (cerussite) in rust-coloured iron oxides
    /// (limonite, goethite) (Kepper 2004, p. 75);</item>
    /// <item>Bronze Age work is small and irregular: shallow pits and trenches along the contact, the ore broken and
    /// sorted by hand into a heap at its foot (Wood et al. 2021, interpretation for E1; the galleries and washeries
    /// of Laurion are 5th–4th century);</item>
    /// <item>the rock is never metallic or shining: metal shows only in the galena grains.</item>
    /// </list>
    /// The heap's size follows the deposit's stock as the game holds it; it never changes the stock. Presentation only.
    /// </summary>
    public static class OreOutcrop
    {
        public const float FaceHeight = 4.2f;

        public sealed class Result
        {
            public Mesh Marble, Contact, Schist, Galena, Cerussite, Workings, Heap;
            public int Grains, Pits;
            public float ContactLow, ContactHigh;
        }

        /// <param name="width">Length of the outcrop along the face, metres.</param>
        /// <param name="faceZ">Where the face stands (model space), the rock rising behind it.</param>
        /// <param name="ground">Ground height at a point of the model (x, z).</param>
        public static Result Make(uint seed, float width, float faceZ, Func<float, float, float> ground)
        {
            var result = new Result();
            var marble = new Builder();
            var contact = new Builder();
            var schist = new Builder();
            var galena = new Builder();
            var cerussite = new Builder();
            var workings = new Builder();
            var heap = new Builder();
            var random = new StableHash.Sequence(StableHash.Mix(seed, 0x0FEu));
            var phase = random.Range(0f, 10f);

            // The face: rows up the rock, leaning back as it rises, its relief from smooth noise.
            const float step = 0.5f, rise = 0.35f;
            var columns = Mathf.Max(2, Mathf.CeilToInt(width / step));
            var rows = Mathf.CeilToInt(FaceHeight / rise);
            float Relief(float x, float y) => 0.35f * Mathf.Sin(x * 0.9f + phase) * Mathf.Sin(y * 1.7f + 0.6f * phase) + 0.18f * Mathf.Sin(x * 2.3f - y * 1.1f + 2f * phase);
            // The contact dips gently along the face, as the marble's top does at Laurion.
            float ContactAt(float x) => 1.6f + 0.45f * Mathf.Sin(x * 0.21f + phase) + 0.15f * Mathf.Sin(x * 0.7f);
            // The outcrop is the slope's own skin: rows climb the hillside from faceZ, a hand's breadth over the ground,
            // with the rock's relief; y counts metres up the outcrop from its foot.
            Vector3 FacePoint(float x, float y)
            {
                var z = faceZ + y * 0.85f;
                return new Vector3(x, ground(x, z) + 0.12f + 0.35f * Mathf.Max(0f, Relief(x, y) + 0.25f), z);
            }
            Vector3 FaceNormal(float x, float y)
            {
                const float e = 0.05f;
                var across = FacePoint(x + e, y) - FacePoint(x - e, y);
                var up = FacePoint(x, y + e) - FacePoint(x, y - e);
                return Vector3.Cross(up, across).normalized;
            }
            result.ContactLow = float.MaxValue;
            result.ContactHigh = float.MinValue;
            for (var c = 0; c < columns; c++)
            {
                var x0 = -0.5f * width + c * width / columns;
                var x1 = x0 + width / columns;
                var contactHere = ContactAt(0.5f * (x0 + x1));
                result.ContactLow = Mathf.Min(result.ContactLow, contactHere - 0.35f);
                result.ContactHigh = Mathf.Max(result.ContactHigh, contactHere + 0.35f);
                for (var r = 0; r < rows; r++)
                {
                    var y0 = r * rise;
                    var y1 = y0 + rise;
                    var middle = 0.5f * (y0 + y1);
                    var target = middle < contactHere - 0.35f ? marble : middle > contactHere + 0.35f ? schist : contact;
                    // Normals from the face's own shape: smooth rock, continuous across the bands.
                    target.Quad(FacePoint(x0, y0), FacePoint(x1, y0), FacePoint(x1, y1), FacePoint(x0, y1),
                        FaceNormal(x0, y0), FaceNormal(x1, y0), FaceNormal(x1, y1), FaceNormal(x0, y1));
                }
            }

            // Galena in the contact: small cubes of lead glance, clustered, half in the rock.
            var clusters = Mathf.RoundToInt(width / 3f);
            for (var k = 0; k < clusters; k++)
            {
                var x = random.Range(-0.45f, 0.45f) * width;
                var y = ContactAt(x) + random.Range(-0.25f, 0.25f);
                var at = FacePoint(x, y);
                var grains = random.Range(4, 9);
                for (var g = 0; g < grains; g++)
                {
                    var size = random.Range(0.06f, 0.18f);
                    var spot = at + new Vector3(random.Range(-0.35f, 0.35f), random.Range(-0.2f, 0.2f), -random.Range(0f, 0.08f));
                    galena.Box(spot, Quaternion.Euler(random.Range(0f, 90f), random.Range(0f, 90f), random.Range(0f, 90f)), Vector3.one * size);
                    result.Grains++;
                }
                // Its white weathering crust round it.
                for (var w = 0; w < 3; w++)
                {
                    var spot = at + new Vector3(random.Range(-0.6f, 0.6f), random.Range(-0.35f, 0.35f), -0.04f);
                    cerussite.Patch(spot, random.Range(0.18f, 0.45f), random.Range(0f, 360f));
                }
            }

            // Shallow pits along the contact: a Bronze Age miner's work, irregular and small.
            var pits = Mathf.Max(2, Mathf.RoundToInt(width / 5f));
            for (var p = 0; p < pits; p++)
            {
                var x = (-0.42f + 0.84f * (p + random.Range(0.2f, 0.8f)) / pits) * width;
                var at = FacePoint(x, ContactAt(x)) + new Vector3(0f, 0f, -0.03f);
                workings.Patch(at, random.Range(0.8f, 1.5f), random.Range(0f, 360f), 0.55f);
                result.Pits++;
                // Spoil spilled below each pit.
                for (var s = 0; s < 6; s++)
                {
                    var spot = new Vector3(x + random.Range(-1f, 1f), 0f, faceZ - random.Range(0.3f, 1.6f));
                    spot.y = ground(spot.x, spot.z) + 0.05f;
                    heap.Box(spot, Quaternion.Euler(random.Range(0f, 360f), random.Range(0f, 360f), 0f), Vector3.one * random.Range(0.12f, 0.3f));
                }
            }

            result.Marble = marble.Mesh("Outcrop marble");
            result.Contact = contact.Mesh("Outcrop contact");
            result.Schist = schist.Mesh("Outcrop schist");
            result.Galena = galena.Mesh("Galena");
            result.Cerussite = cerussite.Mesh("Cerussite");
            result.Workings = workings.Mesh("Workings");
            result.Heap = heap.Mesh("Spoil");
            return result;
        }

        /// <summary>The hand-sorted heap of ore at the foot of the workings: broken lumps, rusty and grey.</summary>
        public static Mesh SortedHeap(uint seed, int lumps)
        {
            var heap = new Builder();
            var random = new StableHash.Sequence(StableHash.Mix(seed, 0x4EAu));
            for (var i = 0; i < lumps; i++)
            {
                // A low cone: lumps settle toward the middle and the top.
                var radius = Mathf.Sqrt(random.Next()) * 1.3f;
                var angle = random.Range(0f, 2f * Mathf.PI);
                var height = (1.3f - radius) * 0.55f * random.Range(0.6f, 1f);
                var spot = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                heap.Box(spot, Quaternion.Euler(random.Range(0f, 360f), random.Range(0f, 360f), random.Range(0f, 360f)), Vector3.one * random.Range(0.12f, 0.28f));
            }
            return heap.Mesh("Sorted ore");
        }

        /// <summary>Adds the outcrop's parts under a parent with their materials; the meshes go with the objects.</summary>
        public static Result Build(Transform parent, uint seed, float width, float faceZ, Func<float, float, float> ground, Material marble, Material contact, Material schist, Material galena, Material cerussite, Material workings)
        {
            var result = Make(seed, width, faceZ, ground);
            Attach(parent, result.Marble, marble);
            Attach(parent, result.Contact, contact);
            Attach(parent, result.Schist, schist);
            Attach(parent, result.Galena, galena);
            Attach(parent, result.Cerussite, cerussite);
            Attach(parent, result.Workings, workings);
            Attach(parent, result.Heap, contact);
            return result;
        }

        public static Transform Attach(Transform parent, Mesh mesh, Material material)
        {
            if (mesh == null || mesh.vertexCount == 0) return null;
            var item = new GameObject(mesh.name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            item.AddComponent<OwnedMesh>().Mesh = mesh;
            return item.transform;
        }

        sealed class Builder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> triangles = new List<int>();

            /// <summary>A quad seen from the front (−Z side for a face leaning back from the viewer), with given normals.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
            {
                // Pieces with their own faces keep normals computed later; pad so the lists stay aligned.
                while (normals.Count < vertices.Count) normals.Add(Vector3.zero);
                var start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                normals.Add(na);
                normals.Add(nb);
                normals.Add(nc);
                normals.Add(nd);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }

            /// <summary>An irregular flat patch in the plane of the leaning face, facing its front, with a jagged rim.</summary>
            public void Patch(Vector3 centre, float radius, float turn, float squash = 1f)
            {
                const int sides = 9;
                var right = Vector3.right;
                var up = new Vector3(0f, 1f, 0.42f).normalized;
                var front = Vector3.Cross(up, right).normalized;
                centre += front * 0.06f;
                var start = vertices.Count;
                vertices.Add(centre);
                for (var i = 0; i < sides; i++)
                {
                    var angle = (turn + i * 360f / sides) * Mathf.Deg2Rad;
                    var r = radius * (0.7f + 0.3f * Mathf.Abs(Mathf.Sin(i * 2.7f + turn)));
                    vertices.Add(centre + right * (Mathf.Cos(angle) * r) + up * (Mathf.Sin(angle) * r * squash));
                }
                for (var i = 0; i < sides; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + 1 + (i + 1) % sides);
                    triangles.Add(start + 1 + i);
                }
            }

            /// <summary>A box with its own faces (flat shading).</summary>
            public void Box(Vector3 centre, Quaternion turn, Vector3 size)
            {
                var h = size * 0.5f;
                var corners = new Vector3[8];
                for (var c = 0; c < 8; c++) corners[c] = centre + turn * new Vector3((c & 1) == 0 ? -h.x : h.x, (c & 2) == 0 ? -h.y : h.y, (c & 4) == 0 ? -h.z : h.z);
                int[][] faces = { new[] { 2, 6, 7, 3 }, new[] { 0, 1, 5, 4 }, new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 } };
                foreach (var face in faces)
                {
                    var start = vertices.Count;
                    foreach (var index in face) vertices.Add(corners[index]);
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                if (normals.Count == vertices.Count && normals.Count > 0) mesh.SetNormals(normals);
                else mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
