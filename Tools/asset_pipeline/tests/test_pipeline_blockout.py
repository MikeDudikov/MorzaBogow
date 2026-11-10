"""The local route end to end up to Unity: blockout -> geometry -> flat textures -> skipped rig and
animation -> import request -> validation verdict. Unity itself is replaced by a stand-in that
records the request and returns a measured report (the real editor run is the E2E in the docs)."""
import json
import unittest

from asset_pipeline.core import service
from asset_pipeline.core.worker import Worker
from asset_pipeline.providers import registry
from asset_pipeline.tests.support import Sandbox

BRIEF = {"id": "ship_ram_flow", "category": "ship", "shipClass": "Ram", "description": "A lean warship with a bronze ram.",
         "generation": {"provider": "blockout"}}


class UnityStandIn:
    name = "unity"
    paid = False

    def __init__(self, triangles_lod0=None):
        self.requests = []
        self.triangles_lod0 = triangles_lod0

    def request(self, ctx):
        from asset_pipeline.unity import Unity
        return Unity(ctx.config).request(ctx)

    def export_blockout(self, ctx, ship_class, out, asset_id):
        from asset_pipeline.tests.support import write_ship_export
        self.exports = getattr(self, "exports", 0) + 1
        return write_ship_export(out, asset_id, ship_class)

    def run(self, ctx, method, request_path):
        request = json.loads(request_path.read_text(encoding="utf-8"))
        self.requests.append((method, request))
        lods = [{"level": i, "triangles": t} for i, t in enumerate(ctx.artifacts["geometry"]["triangles"])]
        if self.triangles_lod0:
            lods[0]["triangles"] = self.triangles_lod0
        report = {"checks": [{"id": "prefab.loads", "ok": True}], "lods": lods, "materialCount": 4, "rendererCount": 3, "textures": [],
                  "bounds": {"min": [-8.4, -1.2, -10.0], "size": [16.8, 12.7, 20.0]}, "forwardChecked": True, "preview": None}
        (ctx.dir / "validation.json").write_text(json.dumps(report), encoding="utf-8")
        return {"prefab": request["outputFolder"] + "/ship_ram_flow.prefab", "report": f"ArtWorkspace/jobs/{ctx.job['id']}/validation.json",
                "preview": None, "log": None, "stale": False}


class BlockoutPipelineTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        self.providers = registry(self.box.config)

    def tearDown(self):
        self.box.close()

    def run_job(self, unity):
        self.providers["unity"] = unity
        job = service.create_jobs(self.box.config, self.box.store, BRIEF)[0]["id"]
        Worker(self.box.config, self.box.store, providers=self.providers, now=self.box.clock, sleep=self.box.clock.sleep).run(
            until_idle=True, handle_signals=False)
        return self.box.store.get(job)

    def test_a_blockout_ship_reaches_ready_with_a_complete_unity_request(self):
        unity = UnityStandIn()
        job = self.run_job(unity)
        self.assertEqual(job["state"], "ready", job["detail"])
        method, request = unity.requests[0]
        self.assertEqual(method, "Import")
        self.assertEqual(request["outputFolder"], "Assets/MorzaBogow/Art/Generated/ship_ram_flow")
        self.assertEqual(len(request["lods"]), 3)
        self.assertEqual([l["screenRelativeHeight"] for l in request["lods"]], [0.25, 0.08, 0.002],
                         "The last level stays until 0.2% of the screen: ships remain visible from the highest camera.")
        self.assertTrue(all(l["file"].lower().endswith(".obj") for l in request["lods"]))
        self.assertEqual(request["dimensions"]["length"], 20.0)
        self.assertTrue(request["normalised"] and request["forwardChecked"])
        self.assertIn("sail", {m["name"] for m in request["materials"]})
        self.assertEqual(request["budget"]["triangles"], 8000)
        provenance = json.loads(request["provenanceJson"])
        self.assertEqual((provenance["generation"], provenance["geometry"]), ("blockout", "blockout"))
        self.assertTrue(job["artifacts"]["validation"]["passed"])
        stages = [e["state"] for e in self.box.store.events(job["id"], 100)]
        for stage in ("generating", "preparing_geometry", "texturing", "rigging", "animating", "importing", "validating", "ready"):
            self.assertIn(stage, stages)

    def test_a_failed_validation_without_blender_stops_for_review(self):
        job = self.run_job(UnityStandIn(triangles_lod0=9000))
        self.assertEqual((job["state"], job["blocked_stage"]), ("needs_review", "validating"))
        self.assertIn("budżet", job["detail"])


if __name__ == "__main__":
    unittest.main()
