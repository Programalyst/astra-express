import importlib.util
from pathlib import Path
import unittest


PROJECT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location('expansion_scope_planner', PROJECT / 'server/astrabot_planner.py')
planner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(planner)


class ExpansionScopeTests(unittest.TestCase):
    def setUp(self):
        self.state = {'buildings': []}

    def test_shipped_and_short_power_only_presets_remain_guarded(self):
        for goal in [
            planner.EXPAND_MINES_GOAL,
            'Build two additional Ore extractors with solar power. Do not add rails or trains.',
        ]:
            with self.subTest(goal=goal):
                result = planner.expansion_spec(goal, self.state)
                self.assertIsNotNone(result)
                self.assertEqual(result['resource'], 'Ore')
                self.assertEqual(result['requestedAdditionalExtractors'], 2)
                self.assertTrue(result['requiresSolarCapacity'])
                self.assertFalse(result['requiresRailService'])
                self.assertTrue(result['simpleSolarExpansion'])

    def test_supported_explicit_and_vague_counts_are_preserved(self):
        cases = [
            ('Build more Ore extractors and power them with solar', 1),
            ('Build five additional Ore extractors powered by solar', 5),
            ('Build 12 additional Ore extractors powered by solar', 12),
        ]
        for goal, count in cases:
            with self.subTest(goal=goal):
                result = planner.expansion_spec(goal, self.state)
                self.assertEqual(result['requestedAdditionalExtractors'], count)

    def test_precise_count_survives_constraints_without_enabling_local_fallback(self):
        for goal, count in [
            ('Build two additional Ore extractors within a 500 credit budget and power them with solar', 2),
            ('Build another Ore extractor here and power it with solar', 1),
        ]:
            with self.subTest(goal=goal):
                result = planner.expansion_spec(goal, self.state)
                self.assertIsNotNone(result)
                self.assertEqual(result['requestedAdditionalExtractors'], count)
                self.assertFalse(result['simpleSolarExpansion'])

    def test_negated_construction_is_not_turned_into_an_expansion(self):
        for goal in [
            'Do not build more extractors; connect the existing mines to solar',
            "Don't add any new Ore mines. Power the existing mine with solar.",
            'Continue without building another extractor and connect the current mine to power',
            'No additional extractors; use solar for the mines already built',
        ]:
            with self.subTest(goal=goal):
                self.assertIsNone(planner.expansion_spec(goal, self.state))

    def test_mixed_resource_goals_are_left_to_the_hosted_planner(self):
        for goal in [
            'Build two Ore extractors, not Fluxite, powered by solar',
            'Build one Ore mine and one Fluxite mine with solar power',
            'Add more mines for Ore before expanding the Fluxite network',
        ]:
            with self.subTest(goal=goal):
                self.assertIsNone(planner.expansion_spec(goal, self.state))

    def test_ranges_and_unsupported_quantity_words_do_not_default_to_one(self):
        goals = [
            'Build zero additional Ore extractors powered by solar',
            'Build eleven additional Ore extractors powered by solar',
            'Build twenty one additional Ore extractors powered by solar',
            'Build twenty-one additional Ore extractors powered by solar',
            'Build a dozen Ore extractors powered by solar',
            'Build several Ore mines powered by solar',
            'Build two or three additional Ore extractors powered by solar',
            'Build 2-4 Ore extractors powered by solar',
            'Build 1.5 Ore extractors powered by solar',
            'Build -2 Ore extractors powered by solar',
            'Build between two and four working Ore mines with solar',
            'Build at least two additional Ore extractors powered by solar',
            'Build two or more Ore extractors powered by solar',
        ]
        for goal in goals:
            with self.subTest(goal=goal):
                self.assertIsNone(planner.expansion_spec(goal, self.state))

    def test_unrepresented_negative_power_or_transport_scope_is_conservative(self):
        for goal in [
            'Build two additional Ore mines without solar power',
            'Build two powered Ore mines with rails but do not dispatch trains',
            'Build two additional Ore extractors with solar but no rail route',
        ]:
            with self.subTest(goal=goal):
                self.assertIsNone(planner.expansion_spec(goal, self.state))


if __name__ == '__main__':
    unittest.main()
