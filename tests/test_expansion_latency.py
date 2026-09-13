import copy
import json
import time
import unittest
from unittest.mock import patch

import test_astrabot_planner as base

planner, coach = base.planner, base.coach


def expansion():
    data = base.request_data()
    data['goal'] = planner.EXPAND_MINES_GOAL
    data['state'].update(credits=1000, solarGeneration=2, paused=False,
                         solarSite={'x': 2, 'y': 11}, solarSitePowerRoute={'possible': True, 'cost': 10})
    return data


def acknowledge(data, plan):
    data['previousPlan'] = {'id': plan['planId'], 'actions': plan['actions'],
                            'results': [{'action': a, 'status': 'complete'} for a in plan['actions']]}


class ContinuationTests(unittest.TestCase):
    def setUp(self):
        self.data = expansion()
        self.progress = planner.PlannerProgress()
        self.key, context = self.progress.prepare(self.data)
        self.first = planner.parse_plan(json.dumps(base.plan([base.action('build_extractor', 11, 7)])), context)
        self.progress.remember(self.key, self.first)
        acknowledge(self.data, self.first)

    def built(self):
        self.data['state']['buildings'] = [base.mine(connected=False, powerRoute={'possible': True, 'cost': 12})]
        self.data['state']['deposits'] = []

    def next_plan(self):
        key, context = self.progress.prepare(self.data)
        return self.progress.continuation(key, context)

    def test_result_alone_does_not_prove_build_and_missing_failed_replayed_results_fall_back(self):
        self.assertIsNone(self.next_plan())
        self.built()
        valid = copy.deepcopy(self.data['previousPlan'])
        for change in [{'id': 'old'}, {'actions': []}, {'results': []},
                       {'results': [{'action': self.first['actions'][0], 'status': 'failed'}]},
                       {'results': [{'action': {**self.first['actions'][0], 'x': 9}, 'status': 'complete'}]}]:
            self.data['previousPlan'] = {**valid, **change}
            self.assertIsNone(self.next_plan())
        self.data['previousPlan'] = valid
        following = self.next_plan()
        self.assertEqual(following['actions'][0]['type'], 'connect_conduit')
        self.progress.remember(self.key, following)
        self.assertIsNone(self.next_plan())

    def test_two_new_mines_add_solar_and_only_complete_when_both_are_connected(self):
        self.built()
        expected = ['connect_conduit', 'build_solar', 'build_extractor', 'connect_conduit']
        for kind in expected:
            following = self.next_plan()
            self.assertEqual(following['actions'][0]['type'], kind)
            self.assertEqual(following['planSource'], 'game-state')
            self.assertFalse(following['goalProgress']['goalSatisfied'])
            self.progress.remember(self.key, following)
            acknowledge(self.data, following)
            if kind == 'connect_conduit':
                self.data['state']['buildings'][-1]['connected'] = True
                if len(self.data['state']['buildings']) == 1:
                    self.data['state']['deposits'] = [{'origin': {'x': 15, 'y': 11}, 'size': 2, 'cost': 250, 'resource': 'Ore'}]
            elif kind == 'build_solar':
                self.data['state']['buildings'].append({'kind': 'Solar', 'origin': {'x': 2, 'y': 11}, 'connected': True})
                self.data['state']['solarGeneration'] = 4
            elif kind == 'build_extractor':
                self.data['state']['buildings'].append(base.mine(x=15, y=11, size=2, connected=False,
                                                               powerRoute={'possible': True, 'cost': 14}))
                self.data['state']['deposits'] = []
        final = self.next_plan()
        self.assertEqual(final['status'], 'complete')
        self.assertTrue(final['goalProgress']['goalSatisfied'])

    def test_constraints_and_fluxite_are_never_replaced_by_local_recovery(self):
        for goal in ['Build another Ore extractor here and power it with solar',
                     'Build two additional Ore extractors with solar power, spend at most 200 credits',
                     'Build two additional Fluxite extractors with solar power',
                     'Do not build more extractors; connect the existing mines to solar',
                     'Build two additional Ore extractors with solar power. Do not explore']:
            data = expansion(); data['goal'] = goal
            key, context = planner.PlannerProgress().prepare(data)
            blocked = planner.parse_plan(json.dumps(base.plan([], 'blocked')), context)
            self.assertEqual(blocked['status'], 'blocked', goal)
            self.assertFalse(blocked['actions'])

    def test_inflight_goal_baseline_survives_other_busy_requests(self):
        for i in range(8):
            other = expansion(); other['goal'] = 'Different goal ' + str(i)
            self.progress.prepare(other, protected={self.key})
        self.built()
        self.assertEqual(self.next_plan()['actions'][0]['type'], 'connect_conduit')
        self.assertEqual(len(self.progress.goals), 4)


class ContinuationHTTPTests(unittest.TestCase):
    setUp = base.PlannerHTTPTests.setUp
    tearDown = base.PlannerHTTPTests.tearDown
    request = base.PlannerHTTPTests.request

    def test_fresh_continuation_skips_upstream_slot_and_cooldown_but_preserves_auth(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        data = expansion()
        headers = {'X-Astra-Coach': self.server.token}
        with patch.object(self.server.agents, 'request', return_value=base.base.events(answer=base.plan([base.action('build_extractor', 11, 7)]))) as upstream:
            code, body = self.request('/api/astrabot/plan', data, headers)
        self.assertEqual(code, 200)
        self.assertEqual(upstream.call_count, 1)
        acknowledge(data, json.loads(body))
        data['state']['buildings'] = [base.mine(connected=False, powerRoute={'possible': True, 'cost': 12})]
        data['state']['deposits'] = []
        self.server.last_request = time.monotonic()
        self.server.vision_slot.acquire()
        try:
            with patch.object(self.server.agents, 'advise', side_effect=AssertionError('Unexpected model call')):
                self.assertEqual(self.request('/api/astrabot/plan', data)[0], 403)
                code, body = self.request('/api/astrabot/plan', data, headers)
                self.assertEqual(code, 200)
                self.assertEqual(json.loads(body)['planSource'], 'game-state')
                self.assertEqual(json.loads(body)['actions'][0]['type'], 'connect_conduit')
                # Replaying a consumed previous plan cannot take the local path.
                code, body = self.request('/api/astrabot/plan', data, headers)
                self.assertEqual(code, 429)
                self.assertEqual(json.loads(body)['retryAfterMs'], 1000)
        finally:
            self.server.vision_slot.release()
        self.assertEqual(self.server.stats['localPlansCompleted'], 1)
        self.assertEqual(self.server.stats['upstreamPlansCompleted'], 1)
        self.assertEqual(len(self.server.requests), 1)
        self.assertEqual(self.server.stats['lastPlanTiming']['source'], 'game-state')

    def test_changed_credential_does_not_reuse_local_goal(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        data = expansion(); headers = {'X-Astra-Coach': self.server.token}
        with patch.object(self.server.agents, 'request', return_value=base.base.events(answer=base.plan([base.action('build_extractor', 11, 7)]))):
            code, body = self.request('/api/astrabot/plan', data, headers)
        self.assertEqual(code, 200)
        acknowledge(data, json.loads(body))
        data['state']['buildings'] = [base.mine(connected=False, powerRoute={'possible': True, 'cost': 12})]
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=different-private-value\n')
        self.assertEqual(self.request('/api/astrabot/plan', data, headers)[0], 429)
        self.assertEqual(self.server.stats['localPlansCompleted'], 0)

    def test_malformed_state_returns_json_error(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        data = expansion(); data['state']['buildings'] = None
        code, body = self.request('/api/astrabot/plan', data, {'X-Astra-Coach': self.server.token})
        self.assertEqual(code, 400)
        self.assertIn('error', json.loads(body))


if __name__ == '__main__':
    unittest.main()
