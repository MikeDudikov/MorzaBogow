import unittest

from asset_pipeline.core import brief as briefs
from asset_pipeline.core import handlers
from asset_pipeline.core.brief import BriefError
from asset_pipeline.tests.support import Sandbox


class BriefTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        self.config = self.box.config

    def tearDown(self):
        self.box.close()

    def ship(self, **extra):
        return {"id": "ship_ram_test", "category": "ship", "shipClass": "Ram", "description": "A lean warship with a bronze beak.", **extra}

    def test_ship_size_comes_from_the_simulation_catalogue(self):
        brief = briefs.normalise(self.ship(), self.config)
        self.assertEqual(brief["dimensions"], {"length": 20.0, "width": 5.6, "draft": 1.2})
        self.assertEqual(brief["generation"]["provider"], "meshy")
        self.assertEqual(brief["texturing"]["provider"], "generation")
        self.assertEqual(brief["rigging"]["provider"], "none")
        self.assertEqual(brief["lods"], 3)
        self.assertEqual(brief["budget"]["triangles"], 8000)
        self.assertIn("sail", brief["teamColour"])

    def test_every_problem_is_listed_at_once(self):
        with self.assertRaises(BriefError) as caught:
            briefs.normalise({"id": "X", "category": "ship", "shipClass": "Galleon", "description": "short"}, self.config)
        text = " ".join(caught.exception.problems)
        for field in ("id:", "shipClass:", "description:"):
            self.assertIn(field, text)

    def test_texture_prompt_always_ends_with_the_house_weathering(self):
        weathering = self.config.data["templates"]["weathering"]
        self.assertIn("photorealistic weathering", weathering)
        plain = briefs.texture_prompt(briefs.normalise(self.ship(), self.config), self.config)
        self.assertTrue(plain.endswith(weathering), plain)
        self.assertIn(self.config.data["templates"]["texture"].rstrip("."), plain)
        # A long description gives way; the weathering stays whole within the limit.
        long = briefs.texture_prompt(briefs.normalise(self.ship(description="A hull of pine planks. " * 60), self.config), self.config)
        self.assertLessEqual(len(long), briefs.PROMPT_LIMIT)
        self.assertTrue(long.endswith(weathering), long[-200:])
        # A brief's own prompt keeps its words and gets the weathering once.
        own = briefs.texture_prompt(briefs.normalise(self.ship(texturing={"prompt": "Silver fir bark, pale cut ends"}), self.config), self.config)
        self.assertTrue(own.startswith("Silver fir bark, pale cut ends"))
        self.assertEqual(own.count(weathering), 1)
        again = briefs.texture_prompt(briefs.normalise(self.ship(texturing={"prompt": f"Pine bark, {weathering}"}), self.config), self.config)
        self.assertEqual(again.count(weathering), 1, "not twice when the brief already asks for it")

    def test_tint_darkens_a_colour_map_with_three_numbers_from_0_to_1(self):
        brief = briefs.normalise(self.ship(texturing={"tint": [0.6, 0.58, 0.55]}), self.config)
        self.assertEqual(brief["texturing"]["tint"], [0.6, 0.58, 0.55])
        for wrong in ([1.2, 0.5, 0.5], [0.5, 0.5], "dark"):
            with self.assertRaises(BriefError) as caught:
                briefs.normalise(self.ship(texturing={"tint": wrong}), self.config)
            self.assertIn("texturing.tint", " ".join(caught.exception.problems))

    def test_a_ship_keeps_the_size_of_its_class(self):
        with self.assertRaises(BriefError) as caught:
            briefs.normalise(self.ship(dimensions={"length": 30}), self.config)
        self.assertIn("katalogu symulacji", " ".join(caught.exception.problems))

    def test_budget_overrides_may_only_lower_the_category_budget(self):
        with self.assertRaises(BriefError):
            briefs.normalise(self.ship(budget={"triangles": 9000}), self.config)
        self.assertEqual(briefs.normalise(self.ship(budget={"triangles": 5000}), self.config)["budget"]["triangles"], 5000)

    def test_prompt_fits_the_limit_and_names_the_size(self):
        brief = briefs.normalise(self.ship(description="x" * 700), self.config)
        prompt = briefs.prompt(brief, self.config)
        self.assertLessEqual(len(prompt), briefs.PROMPT_LIMIT)
        self.assertIn("20 m long", prompt)
        self.assertIn("bronze ram", prompt)

    def test_credit_estimate_follows_the_plan(self):
        text = briefs.normalise(self.ship(), self.config)
        blockout = briefs.normalise(self.ship(generation={"provider": "blockout"}), self.config)
        retexture = briefs.normalise(self.ship(generation={"provider": "blockout"}, texturing={"provider": "meshy_retexture"}), self.config)
        self.assertEqual(handlers.estimate_credits(text, self.config), 30)
        self.assertEqual(handlers.estimate_credits(blockout, self.config), 0)
        self.assertEqual(handlers.estimate_credits(retexture, self.config), 10)

    def test_rigs_only_for_characters_and_blockouts_only_for_ships(self):
        with self.assertRaises(BriefError):
            briefs.normalise(self.ship(rigging={"provider": "meshy"}), self.config)
        with self.assertRaises(BriefError):
            briefs.normalise({"id": "rock_one", "category": "rock", "description": "A white limestone rock.", "generation": {"provider": "blockout"}}, self.config)

    def test_every_template_needs_only_a_real_description(self):
        for category in ("ship", "rock", "character"):
            brief = briefs.template(category, "Cutter")
            brief["description"] = "A real description of the asset."
            briefs.normalise(brief, self.config)


if __name__ == "__main__":
    unittest.main()
