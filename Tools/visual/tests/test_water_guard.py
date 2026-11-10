import os
import sys
import unittest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
import water_guard as guard  # noqa: E402

SOURCE = '''
namespace Demo
{
    public sealed class Ship
    {
        public const float SurfSpacing = 6f;
        string label = "{ not a brace }";

        void KeepWaterOut(View view)
        {
            // a } in a comment and a "{" in a string must not end the method
            var note = "}";
            if (view != null) { view.Excluder = true; }
        }

        static float Draft(int a) => a * 0.5f;
        static float Draft(float a) => a * 0.25f;

        void Create()
        {
            var hull = Build();
            var wake = WaterDecalAt(hull);
            var paint = ShipSurface.Of(hull);
        }
    }
}
'''


class MemberExtractionTests(unittest.TestCase):
    def test_a_constant_is_its_own_line(self):
        self.assertEqual(guard.member_text(SOURCE, "SurfSpacing").strip(), "public const float SurfSpacing = 6f;")

    def test_braces_in_comments_and_strings_do_not_end_a_method(self):
        body = guard.member_text(SOURCE, "KeepWaterOut")
        self.assertTrue(body.rstrip().endswith("}"))
        self.assertIn("view.Excluder = true;", body)
        self.assertNotIn("static float Draft", body)

    def test_every_overload_is_fingerprinted(self):
        self.assertEqual(len(guard.member_spans(SOURCE, "Draft")), 2)

    def test_water_lines_skip_the_rest_of_a_shared_method(self):
        body = guard.member_text(SOURCE, "Create", guard.WATER_LINES)
        self.assertIn("WaterDecalAt", body)
        self.assertNotIn("ShipSurface", body)
        self.assertNotIn("Build()", body)

    def test_a_missing_member_is_none(self):
        self.assertIsNone(guard.member_text(SOURCE, "NoSuchThing"))

    def test_a_region_runs_from_its_start_to_its_end_marker(self):
        text = "a\nvar sun = new GameObject(\"Sun\");\nb\nocean.causticsIntensity = 0.8f;\nc"
        self.assertEqual(guard.region_text(text, r'new GameObject\("Sun"\)', r"ocean\.causticsIntensity").count("\n"), 2)

    def test_line_endings_do_not_change_a_fingerprint(self):
        self.assertEqual(guard.digest("a\nb".replace("\r\n", "\n")), guard.digest("a\r\nb".replace("\r\n", "\n")))

    def test_a_changed_water_line_changes_the_fingerprint(self):
        changed = SOURCE.replace("view.Excluder = true;", "view.Excluder = false;")
        self.assertNotEqual(guard.digest(guard.member_text(SOURCE, "KeepWaterOut")),
                            guard.digest(guard.member_text(changed, "KeepWaterOut")))
        unrelated = SOURCE.replace("var hull = Build();", "var hull = BuildFaster();")
        self.assertEqual(guard.digest(guard.member_text(SOURCE, "Create", guard.WATER_LINES)),
                         guard.digest(guard.member_text(unrelated, "Create", guard.WATER_LINES)))


if __name__ == "__main__":
    unittest.main()
