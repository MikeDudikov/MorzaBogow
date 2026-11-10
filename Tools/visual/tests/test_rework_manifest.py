import os
import sys
import unittest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
import rework_manifest as manifest  # noqa: E402


class ReworkManifestTests(unittest.TestCase):
    def test_every_asset_class_role_and_building_has_an_entry_and_the_file_is_current(self):
        self.assertEqual(manifest.problems(), [])

    def test_meshy_spending_is_the_sum_of_the_ledger(self):
        book = manifest.ledger()
        self.assertEqual(manifest.manifest()["meshy"]["spentCredits"], sum(t["credits"] for t in book["tasks"]))

    def test_the_water_is_outside_the_refresh(self):
        water = [e for e in manifest.ENTRIES if e["assetId"] == "water.system"]
        self.assertEqual(len(water), 1)
        self.assertEqual(water[0]["scope"], ["exclude"])


if __name__ == "__main__":
    unittest.main()
