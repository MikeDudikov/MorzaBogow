"""Retexture request: the model's own UVs by default, Meshy's UV layout when the brief asks for it."""
import pathlib
import tempfile
import unittest

from asset_pipeline.providers.meshy import Meshy
from asset_pipeline.tests.support import Sandbox


class RetexturePayloadTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox(environ={"MESHY_API_KEY": "msy_test_only_not_a_real_key"})
        self.model = pathlib.Path(tempfile.mkdtemp()) / "ship.glb"
        self.model.write_bytes(b"glTF")

    def tearDown(self):
        self.box.close()

    def test_keeps_the_models_uvs_unless_told_otherwise(self):
        payload = Meshy(self.box.config).retexture_payload(self.model, "pitch-black hull")
        self.assertIs(payload["enable_original_uv"], True)

    def test_lets_meshy_lay_out_uvs_for_a_model_whose_uvs_overlap(self):
        # The game's generator gives UVs in metres that tile; Meshy then paints every part into the same texels.
        payload = Meshy(self.box.config).retexture_payload(self.model, "pitch-black hull", original_uv=False)
        self.assertIs(payload["enable_original_uv"], False)
        self.assertEqual(payload["target_formats"], ["glb"])


if __name__ == "__main__":
    unittest.main()
