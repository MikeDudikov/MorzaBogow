import unittest

from asset_pipeline.core import brief as briefs
from asset_pipeline.validation.report import evaluate
from asset_pipeline.tests.support import Sandbox


def unity_report(**extra):
    report = {
        "checks": [{"id": "prefab.loads", "ok": True}],
        "lods": [{"level": 0, "triangles": 840}, {"level": 1, "triangles": 300}, {"level": 2, "triangles": 115}],
        "materialCount": 4, "rendererCount": 3, "textures": [],
        "bounds": {"min": [-8.4, -1.2, -10.0], "size": [16.8, 12.7, 20.0]},
        "forwardChecked": True, "preview": "ArtWorkspace/jobs/x/preview.png", "texelDensity": 70,
    }
    report.update(extra)
    return report


class ReportTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        self.brief = briefs.normalise({"id": "ship_ram_check", "category": "ship", "shipClass": "Ram", "description": "A lean warship.",
                                       "generation": {"provider": "blockout"}}, self.box.config)

    def tearDown(self):
        self.box.close()

    def test_a_ship_within_budget_and_class_size_passes(self):
        verdict = evaluate(unity_report(), self.brief, self.box.config)
        self.assertEqual((verdict["passed"], verdict["failures"]), (True, []))

    def test_too_many_triangles_asks_for_a_geometry_repair(self):
        verdict = evaluate(unity_report(lods=[{"level": 0, "triangles": 9000}, {"level": 1, "triangles": 3000}]), self.brief, self.box.config)
        self.assertFalse(verdict["passed"])
        self.assertTrue(verdict["repair"])

    def test_wrong_length_or_draft_fails_without_repair(self):
        verdict = evaluate(unity_report(bounds={"min": [-3, -0.4, -9], "size": [6, 8, 18.0]}), self.brief, self.box.config)
        self.assertFalse(verdict["passed"])
        self.assertFalse(verdict["repair"])
        text = " ".join(verdict["failures"])
        self.assertIn("długość", text)
        self.assertIn("stępka", text)

    def test_unity_check_failures_and_warnings_are_kept_apart(self):
        checks = [{"id": "materials.hdrp", "ok": False, "message": "Standard"}, {"id": "colliders", "ok": False, "severity": "warning", "message": "1"}]
        verdict = evaluate(unity_report(checks=checks, preview=None), self.brief, self.box.config)
        self.assertIn("materials.hdrp: Standard", verdict["failures"])
        self.assertIn("colliders: 1", verdict["warnings"])
        self.assertTrue(any("podglądu" in w for w in verdict["warnings"]))


if __name__ == "__main__":
    unittest.main()
