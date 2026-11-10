using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MorzaBogow.Presentation;
using MorzaBogow.Simulation;
using UnityEngine;

namespace MorzaBogow.Editor.AIAssets
{
    /// <summary>
    /// Writes a class's ship from the game's own generator (GreekShips) as OBJ with an MTL of flat palette colours, one file
    /// per level of detail: hull, deck, spur, rig, steering oars, the sail (material "sail", the side's colour) and the
    /// oars in a rowing pose, with the weapon stands on their posts. Unity's left-handed frame is mirrored to OBJ's
    /// right-handed one (X negated, faces reversed), so importing the file back gives the same ship.
    /// </summary>
    public static class ShipExport
    {
        static string N(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

        public static int WriteObj(ShipClass shipClass, int detail, string path, string mtlName)
        {
            var parts = GreekShips.Parts(shipClass, detail);
            var placed = new List<(GreekMesh mesh, Matrix4x4 matrix)>
            {
                (parts.Hull, Matrix4x4.identity),
                (parts.Deck, Matrix4x4.identity),
                (parts.Rig, Matrix4x4.identity),
                (parts.Steering, Matrix4x4.identity),
                (parts.Sail, Matrix4x4.TRS(parts.SailTop, Quaternion.identity, new Vector3(parts.SailSize.x, parts.SailSize.y, 0.3f))),
            };
            if (parts.Ram != null) placed.Add((parts.Ram, Matrix4x4.identity));
            // The class's ship as the harbour yard paints it: the white zigzag on the wale (a separate mesh in the game).
            var band = GreekShips.Band(shipClass, "zigzag", detail);
            if (band != null) placed.Add((band, Matrix4x4.identity));
            for (var side = -1; side <= 1; side += 2)
            for (var i = 0; i < parts.OarPins.Length; i++)
            {
                var pin = parts.OarPins[i];
                var bank = parts.OarBank[i];
                var pivot = Matrix4x4.TRS(new Vector3(side * pin.x, pin.y, pin.z), Quaternion.Euler(0f, 0f, side * -parts.OarTilt[i]), Vector3.one);
                var blade = Matrix4x4.TRS(new Vector3(side * parts.OarLength[bank] * 0.5f, 0f, 0f), side < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity, Vector3.one);
                placed.Add((parts.Oars[bank], pivot * blade));
            }
            var slots = ShipCatalog.Get(shipClass).Slots;
            for (var slot = 0; slot < slots.Length; slot++) placed.Add((GreekShips.Weapon(slots[slot]), Matrix4x4.Translate(parts.Mounts[slot])));

            var text = new StringBuilder();
            text.Append("# Morza Bogow: ").Append(shipClass).Append(" (").Append(parts.Form.TypeName).Append("), detail ").Append(detail).Append('\n');
            text.Append("mtllib ").Append(mtlName).Append('\n');
            text.Append("o ").Append(Path.GetFileNameWithoutExtension(path)).Append('\n');
            var faces = new StringBuilder();
            var offset = 0;
            var triangles = 0;
            foreach (var (mesh, matrix) in placed)
            {
                var source = mesh.Mesh;
                var vertices = source.vertices;
                var normals = source.normals;
                var uvs = source.uv;
                var normalMatrix = matrix.inverse.transpose;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var p = matrix.MultiplyPoint3x4(vertices[i]);
                    var n = normalMatrix.MultiplyVector(normals.Length > i ? normals[i] : Vector3.up).normalized;
                    var uv = uvs.Length > i ? uvs[i] : Vector2.zero;
                    text.Append("v ").Append(N(-p.x)).Append(' ').Append(N(p.y)).Append(' ').Append(N(p.z)).Append('\n');
                    text.Append("vt ").Append(N(uv.x)).Append(' ').Append(N(uv.y)).Append('\n');
                    text.Append("vn ").Append(N(-n.x)).Append(' ').Append(N(n.y)).Append(' ').Append(N(n.z)).Append('\n');
                }
                for (var sub = 0; sub < source.subMeshCount; sub++)
                {
                    faces.Append("usemtl ").Append(PaintName(mesh.Slots[sub])).Append('\n');
                    var list = source.GetTriangles(sub);
                    for (var t = 0; t < list.Length; t += 3)
                    {
                        // Mirrored: the winding is reversed so faces still point outward.
                        int a = list[t] + offset + 1, b = list[t + 2] + offset + 1, c = list[t + 1] + offset + 1;
                        faces.Append("f ").Append(a).Append('/').Append(a).Append('/').Append(a).Append(' ')
                             .Append(b).Append('/').Append(b).Append('/').Append(b).Append(' ')
                             .Append(c).Append('/').Append(c).Append('/').Append(c).Append('\n');
                        triangles++;
                    }
                }
                offset += vertices.Length;
            }
            File.WriteAllText(path, text.Append(faces).ToString());
            return triangles;
        }

        /// <summary>The export's paint for a generator paint: rope and wicker are wood, linen is paint, stone pitch, terracotta ochre.</summary>
        public static string PaintName(GreekMaterial paint) => paint switch
        {
            GreekMaterial.Rope or GreekMaterial.Wicker => "wood",
            GreekMaterial.Linen => "paint",
            GreekMaterial.Stone => "pitch",
            GreekMaterial.Terracotta => "ochre",
            _ => paint.ToString().ToLowerInvariant(),
        };

        /// <summary>The flat paints as OBJ materials (sRGB Kd), and the same values for the pipeline's Unity import.</summary>
        public static (string name, Color colour, float metallic, float smoothness)[] Paints() => new[]
        {
            ("pitch", GreekShips.Palette.Pitch, 0f, 0.35f),
            ("ochre", GreekShips.Palette.Ochre, 0f, 0.25f),
            ("paint", GreekShips.Palette.Paint, 0f, 0.3f),
            ("wood", GreekShips.Palette.Wood, 0f, 0.3f),
            ("bronze", GreekShips.Palette.Bronze, 0.85f, 0.55f),
            ("sail", GreekShips.Palette.Linen, 0f, 0.15f),
        };

        public static void WriteMtl(string path)
        {
            var text = new StringBuilder("# Morza Bogow paints (sRGB)\n");
            foreach (var (name, colour, metallic, smoothness) in Paints())
                text.Append("newmtl ").Append(name).Append("\nKd ").Append(N(colour.r)).Append(' ').Append(N(colour.g)).Append(' ').Append(N(colour.b))
                    .Append("\nNs ").Append(N(smoothness * 100f)).Append("\nd 1\nillum 2\n\n");
            File.WriteAllText(path, text.ToString());
        }

        /// <summary>The export's manifest for the pipeline: files, triangles per level and the paints.</summary>
        public static string Manifest(string id, ShipClass shipClass, IList<string> lods, IList<int> triangles)
        {
            var paints = string.Join(", ", Paints().Select(p =>
                $"{{\"name\": \"{p.name}\", \"baseColor\": [{N(p.colour.r)}, {N(p.colour.g)}, {N(p.colour.b)}], \"metallic\": {N(p.metallic)}, \"smoothness\": {N(p.smoothness)}}}"));
            var files = string.Join(", ", lods.Select(f => $"\"{f}\""));
            return $"{{\"id\": \"{id}\", \"shipClass\": \"{shipClass}\", \"type\": \"{GreekShips.Form(shipClass).TypeName}\", \"lods\": [{files}], " +
                   $"\"triangles\": [{string.Join(", ", triangles)}], \"materials\": [{paints}]}}";
        }
    }
}
