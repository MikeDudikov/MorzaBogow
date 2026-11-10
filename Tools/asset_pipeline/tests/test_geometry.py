import json
import struct
import unittest

from asset_pipeline.providers.blockout import obj_to_glb, read_obj
from asset_pipeline.validation import geometry
from asset_pipeline.tests.support import Sandbox, write_ship_export


class GeometryTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()

    def tearDown(self):
        self.box.close()

    def test_an_exported_ship_inspects_as_a_simpler_lod_chain(self):
        manifest = write_ship_export(self.box.dir / "export", "ship_ram_test")
        reports = [geometry.inspect(self.box.dir / "export" / name) for name in manifest["lods"]]
        self.assertEqual(geometry.lod_problems(reports, 8000), [])
        self.assertEqual([r["triangles"] for r in reports], manifest["triangles"])
        self.assertEqual(reports[0]["materials"], ["pitch", "sail"])
        self.assertAlmostEqual(reports[0]["bounds"]["size"][2], 20.0)

    def test_obj_converts_to_a_well_formed_glb_with_the_same_triangles_and_paints(self):
        manifest = write_ship_export(self.box.dir / "export", "ship_ram_test")
        obj = self.box.dir / "export" / manifest["lods"][0]
        glb = self.box.dir / "export" / "ship.glb"
        triangles = obj_to_glb(obj, glb, {m["name"]: m for m in manifest["materials"]})
        self.assertEqual(triangles, manifest["triangles"][0])
        data = glb.read_bytes()
        magic, version, length = struct.unpack_from("<4sII", data, 0)
        self.assertEqual((magic, version, length), (b"glTF", 2, len(data)))
        json_length, json_type = struct.unpack_from("<I4s", data, 12)
        self.assertEqual(json_type, b"JSON")
        gltf = json.loads(data[20:20 + json_length])
        self.assertEqual([m["name"] for m in gltf["materials"]], ["pitch", "sail"])
        self.assertLess(gltf["materials"][0]["pbrMetallicRoughness"]["baseColorFactor"][0], 0.075, "sRGB colours become linear factors.")
        inspected = geometry.inspect(glb)
        self.assertEqual(inspected["triangles"], triangles)
        self.assertTrue(inspected["has_uv"] and inspected["has_normals"])
        positions, uvs, normals, groups = read_obj(obj)
        self.assertEqual(sum(len(f) for f in groups.values()), triangles)

    def test_a_model_without_uvs_is_reported(self):
        path = self.box.dir / "plain.obj"
        path.write_text("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n", encoding="utf-8")
        report = geometry.inspect(path)
        self.assertFalse(report["has_uv"])
        self.assertIn("LOD0 bez UV", geometry.lod_problems([report], 100))


if __name__ == "__main__":
    unittest.main()
