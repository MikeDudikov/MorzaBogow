import json
import unittest

from asset_pipeline.core import handlers, service
from asset_pipeline.core.errors import NeedsReview
from asset_pipeline.core.worker import Worker
from asset_pipeline.providers.comfyui import ComfyUI, fill
from asset_pipeline.tests.support import MockComfy, Sandbox

WORKFLOW = {
    "3": {"class_type": "KSampler", "inputs": {"seed": "{{seed}}", "steps": 20}},
    "6": {"class_type": "CLIPTextEncode", "inputs": {"text": "{{prompt}}, high detail"}},
    "9": {"class_type": "SaveImage", "inputs": {"filename_prefix": "{{filename_prefix}}"}},
}


class ComfyTests(unittest.TestCase):
    def setUp(self):
        self.mock = MockComfy()
        self.box = Sandbox(environ={"COMFYUI_URL": self.mock.url})
        folder = self.box.dir / "ArtWorkspace" / "workflows"
        folder.mkdir(parents=True)
        (folder / "concept_api.json").write_text(json.dumps(WORKFLOW), encoding="utf-8")

    def tearDown(self):
        self.mock.close()
        self.box.close()

    def test_placeholders_keep_the_type_of_a_whole_value(self):
        filled = fill(WORKFLOW, {"seed": 7, "prompt": "a trireme", "filename_prefix": "x"})
        self.assertEqual(filled["3"]["inputs"]["seed"], 7)
        self.assertEqual(filled["6"]["inputs"]["text"], "a trireme, high detail")

    def test_a_concept_is_queued_once_under_a_known_id_and_downloaded(self):
        brief = {"id": "ship_concept", "category": "ship", "shipClass": "Ram", "description": "A lean warship with a painted eye.",
                 "concept": {"provider": "comfyui"}, "generation": {"provider": "meshy", "mode": "image"}}
        job = service.create_jobs(self.box.config, self.box.store, brief, approve_credits=0)[0]["id"]
        comfy = ComfyUI(self.box.config)

        def concept_only(ctx):
            handlers.concept(ctx)
            raise NeedsReview("koniec testu konceptu")

        Worker(self.box.config, self.box.store, providers={"comfyui": comfy}, handlers={"generating": concept_only},
               now=self.box.clock, sleep=self.box.clock.sleep).run(until_idle=True, handle_signals=False)
        state = self.box.store.get(job)
        self.assertEqual(state["state"], "needs_review", state["detail"])
        expected_id = comfy.prompt_id(job, "concept")
        self.assertEqual(list(self.mock.prompts), [expected_id])
        sent = self.mock.prompts[expected_id]["prompt"]
        self.assertIsInstance(sent["3"]["inputs"]["seed"], int)
        self.assertIn("Aegean", sent["6"]["inputs"]["text"], "The concept template of the art direction.")
        image = self.box.config.root / state["artifacts"]["concept"]["image"]
        self.assertTrue(image.read_bytes().startswith(b"\x89PNG"))

    def test_without_a_server_address_the_stage_needs_configuration(self):
        box = Sandbox(environ={})
        try:
            with self.assertRaises(Exception) as caught:
                ComfyUI(box.config).require()
            self.assertEqual(caught.exception.items, ["COMFYUI_URL"])
        finally:
            box.close()


if __name__ == "__main__":
    unittest.main()
