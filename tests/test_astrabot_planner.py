import copy
import io
import json
import unittest
from unittest.mock import patch
import test_coach_server as base

coach = base.coach
planner = coach.planner


def request_data():
    data = base.payload()
    data.pop('candidates'); data.pop('question'); data.pop('events')
    data.update(goal='Set up a paying ore route', selectedTile={'x': 11, 'y': 7}, previousPlan=None)
    data['state'].update(credits=500, trainCount=1, idleTrains=1, maxTrains=4, trainCost=150,
                         colonyPort={'x': 5, 'y': 6}, buildings=[],
                         deposits=[{'origin': {'x': 11, 'y': 7}, 'cost': 150, 'size': 1, 'resource': 'Ore'}])
    return data


def action(kind, x=None, y=None, **kwargs):
    return {'id': kind, 'type': kind, 'x': x, 'y': y, 'targetX': None, 'targetY': None,
            'trainIndex': None, 'seconds': None, 'reason': 'Move toward the requested goal.', **kwargs}


def plan(actions, status='ready'):
    return {'title': 'Next game actions', 'summary': 'Build a paying ore route in small verified steps.',
            'status': status, 'actions': actions, 'nextCheck': 'Check connections and the assigned train in the next frame.'}


def mine(resource='Ore'):
    return {'kind': 'Extractor', 'resource': resource, 'origin': {'x': 11, 'y': 7}, 'port': {'x': 11, 'y': 6},
            'connected': True, 'railConnected': True, 'served': False}


class PlanValidationTests(unittest.TestCase):
    def test_payload_keeps_selected_tile_and_bounded_progress(self):
        data = request_data(); planner.validate_plan_payload(data)
        request = planner.build_planner_request(data, 'gpt-5.4-mini', 'Game facts.')
        content = request['input'][0]['content']
        self.assertEqual(content[1]['image_url'], data['image'])
        self.assertEqual(json.loads(content[0]['text'])['selectedTile'], data['selectedTile'])
        self.assertEqual(request['environment'], {'type': 'none'})
        self.assertEqual(request['agent']['tools'], [])
        self.assertEqual(request['agent']['model'], 'gpt-5.4-mini')
        for change in [{'goal': ''}, {'goal': 'g' * 601}, {'selectedTile': {'x': 32, 'y': 1}}, {'previousPlan': {'results': [{}] * 61}}]:
            with self.assertRaises(ValueError): planner.validate_plan_payload({**data, **change})

    def test_current_revealed_mine_can_be_built_connected_and_dispatched(self):
        result = planner.parse_plan(json.dumps(plan([action('build_extractor', 11, 7), action('connect_conduit', 11, 7),
                                                     action('connect_rail', 11, 6), action('dispatch_train', 11, 7)])), request_data())
        self.assertEqual(len(result['actions']), 4)
        self.assertTrue(result['planId'])

    def test_hidden_deposit_duplicate_build_and_unknown_connection_rejected(self):
        data = request_data()
        for actions in [[action('build_extractor', 20, 6)], [action('connect_conduit', 20, 6)],
                        [action('build_extractor', 11, 7), action('build_extractor', 11, 7, id='second')]]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan(actions)), data)

    def test_unknown_tools_arbitrary_code_and_extra_fields_rejected(self):
        for bad in [action('reset'), action('sell'), action('shell', command='rm -rf /'), action('explore', 7, 6, url='https://example.com')]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([bad])), request_data())

    def test_out_of_bounds_fractional_and_boolean_coordinates_rejected(self):
        for x, y in [(32, 5), (5, 32), (-1, 0), (1.5, 5), (True, 5)]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('explore', x, y)])), request_data())

    def test_expanded_grid_corner_is_valid(self):
        data = request_data()
        planner.validate_plan_payload({**data, 'selectedTile': {'x': 31, 'y': 31}})
        result = planner.parse_plan(json.dumps(plan([action('explore', 31, 31)])), data)
        self.assertEqual((result['actions'][0]['x'], result['actions'][0]['y']), (31, 31))

    def test_exploration_ends_batch_before_building_unseen_future_resources(self):
        with self.assertRaises(ValueError):
            planner.parse_plan(json.dumps(plan([action('explore', 10, 6), action('build_extractor', 11, 7)])), request_data())
        self.assertEqual(planner.parse_plan(json.dumps(plan([action('explore', 10, 6)])), request_data())['status'], 'ready')

    def test_auto_exploration_is_coordinate_free_and_requires_a_fresh_batch(self):
        data = request_data()
        self.assertEqual(planner.parse_plan(json.dumps(plan([action('auto_explore')])), data)['actions'][0]['type'], 'auto_explore')
        for actions in [[action('auto_explore', 20, 6)], [action('auto_explore', seconds=60)],
                        [action('auto_explore', targetX=20, targetY=6)],
                        [action('auto_explore'), action('build_extractor', 11, 7)]]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan(actions)), data)
        planner.parse_plan(json.dumps(plan([action('resume'), action('auto_explore')])), data)

    def test_auto_exploration_schema_forbids_hidden_coordinate_or_duration_targets(self):
        variants = planner.plan_schema()['properties']['actions']['items']['anyOf']
        variant = next(v for v in variants if v['properties']['type']['enum'] == ['auto_explore'])
        for field in ['x', 'y', 'targetX', 'targetY', 'trainIndex', 'seconds']:
            self.assertEqual(variant['properties'][field], {'type': 'null'})

    def test_new_ore_progress_excludes_preexisting_deposits_and_fluxite(self):
        progress = planner.PlannerProgress(); data = request_data(); data['goal'] = 'Automatically discover new ores'
        first = progress.prepare(data)[1]['serverProgress']
        self.assertEqual(first['initialVisibleOreOrigins'], [{'x': 11, 'y': 7}])
        self.assertEqual(first['newVisibleOreOrigins'], [])
        data['state']['deposits'].append({'origin': {'x': 13, 'y': 3}, 'resource': 'Fluxite'})
        self.assertEqual(progress.prepare(data)[1]['serverProgress']['newVisibleOreOrigins'], [])
        data['state']['deposits'].append({'origin': {'x': 20, 'y': 6}, 'resource': 'Ore'})
        self.assertEqual(progress.prepare(data)[1]['serverProgress']['newVisibleOreOrigins'], [{'x': 20, 'y': 6}])
        data['state']['buildings'] = [mine()]; data['state']['deposits'] = data['state']['deposits'][1:]
        self.assertEqual(progress.prepare(data)[1]['serverProgress']['initialVisibleOreOrigins'], first['initialVisibleOreOrigins'])

    def test_solar_build_includes_known_wiring_and_reconnect_does_not_repurchase(self):
        data = request_data(); data['state'].update(solarSite={'x': 2, 'y': 11}, credits=100,
            solarSitePowerRoute={'possible': True, 'cost': 12})
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11)])), data)
        data['state']['credits'] = 112
        planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11), action('connect_conduit', 2, 11)])), data)
        data['state']['buildings'] = [{'kind': 'Solar', 'origin': {'x': 2, 'y': 11}, 'connected': False,
                                     'powerRoute': {'possible': True, 'cost': 12}}]
        data['state']['credits'] = 12
        planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11)])), data)
        data['state']['buildings'][0]['powerRoute']['possible'] = False
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11)])), data)

    def test_wait_and_terminal_status_have_no_extra_actions(self):
        for seconds in [0, 21, None, True]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('wait', seconds=seconds)])), request_data())
        planner.parse_plan(json.dumps(plan([action('wait', seconds=5)])), request_data())
        for status in ['complete', 'blocked']:
            planner.parse_plan(json.dumps(plan([], status)), request_data())
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('resume')], status)), request_data())
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([])), request_data())

    def test_maximum_batch_and_duplicate_ids_rejected(self):
        for actions in [[action('wait', seconds=1, id=str(i)) for i in range(7)], [action('resume'), action('resume')]]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan(actions)), request_data())

    def test_buying_obeys_fleet_and_current_budget_without_future_income(self):
        data = request_data()
        planner.parse_plan(json.dumps(plan([action('buy_train')])), data)
        for change in [{'trainCount': 4}, {'credits': 149}]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('buy_train')])), {**data, 'state': {**data['state'], **change}})
        with self.assertRaises(ValueError):
            planner.parse_plan(json.dumps(plan([action('wait', seconds=20), action('buy_train')])), {**data, 'state': {**data['state'], 'credits': 149}})

    def test_dispatch_requires_idle_train_and_ready_connections(self):
        data = request_data(); data['state']['buildings'] = [mine()]
        planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), data)
        for change in [{'served': True}, {'connected': False}, {'railConnected': False}]:
            with self.assertRaises(ValueError):
                planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), {**data, 'state': {**data['state'], 'buildings': [{**mine(), **change}]}})
        data['state']['idleTrains'] = 0
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), data)
        planner.parse_plan(json.dumps(plan([action('buy_train'), action('dispatch_train', 11, 7)])), data)

    def test_fluxite_dispatch_has_explicit_power_plant_destination_not_colony(self):
        data = request_data(); data['state']['buildings'] = [mine('Fluxite'), {'kind': 'PowerPlant', 'origin': {'x': 3, 'y': 11}, 'connected': True, 'railConnected': True}]
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), data)
        planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7, targetX=3, targetY=11)])), data)
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7, targetX=5, targetY=7)])), data)

    def test_construction_uses_known_site_or_explicit_tile(self):
        data = request_data(); data['state']['solarSite'] = {'x': 2, 'y': 11}
        planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11)])), data)
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('build_solar', 20, 19)])), data)
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('build_plant', 11, 7)])), data)
        data['selectedTile'] = {'x': 3, 'y': 11}
        planner.parse_plan(json.dumps(plan([action('build_plant', 3, 11)])), data)

    def test_known_route_costs_are_budgeted_and_blocked_paths_rejected(self):
        data = request_data(); b = mine(); b.update(connected=False, powerRoute={'possible': True, 'cost': 15})
        data['state'].update(buildings=[b], credits=14)
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('connect_conduit', 11, 6)])), data)
        b['powerRoute'] = {'possible': False, 'cost': 0}
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('connect_conduit', 11, 6)])), data)

    def test_progress_is_bounded_and_never_records_proposals_as_completed_actions(self):
        progress = planner.PlannerProgress(); data = request_data()
        key, prepared = progress.prepare(data)
        self.assertEqual(prepared['serverProgress']['proposedBatches'], [])
        for _ in range(8): progress.remember(key, planner.parse_plan(json.dumps(plan([action('wait', seconds=5)])), data))
        self.assertEqual(len(progress.prepare(data)[1]['serverProgress']['proposedBatches']), 4)
        for i in range(8): progress.prepare({**data, 'goal': str(i)})
        self.assertEqual(len(progress.goals), 4)


class PlannerHTTPTests(unittest.TestCase):
    setUp = base.LocalHTTPTests.setUp
    tearDown = base.LocalHTTPTests.tearDown
    request = base.LocalHTTPTests.request

    def test_planner_uses_same_authentication_and_origin_boundary(self):
        self.assertEqual(self.request('/api/astrabot/plan', request_data())[0], 403)
        self.assertEqual(self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token, 'Origin': 'https://evil.example'})[0], 403)
        _, body = self.request('/api/astrabot/config')
        self.assertTrue(json.loads(body)['plannerAvailable'])
        self.assertEqual(json.loads(body)['engine'], 'agents-api')

    def test_plan_uses_real_hosted_transport_and_returns_visible_validated_actions(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        proposed = plan([action('explore', 10, 6)])
        with patch.object(self.server.agents, 'request', return_value=base.events(answer=proposed)) as upstream:
            code, body = self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token})
        self.assertEqual(code, 200)
        result = json.loads(body)
        self.assertEqual(result['actions'], proposed['actions'])
        self.assertTrue(result['planId'])
        self.assertEqual(upstream.call_args.args[1], '/sessions')
        self.assertEqual(upstream.call_args.args[2]['metadata']['purpose'], 'astrabot-game-planning')
        self.assertEqual(self.server.stats['plansCompleted'], 1)
        self.assertEqual(self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token})[0], 429)

    def test_planner_validation_failure_starts_no_actions_and_hides_upstream_text(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        with patch.object(self.server.agents, 'advise', side_effect=ValueError('secret private details')):
            code, body = self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token})
        self.assertEqual(code, 502)
        self.assertIn(b'No new actions started', body)
        self.assertIn(b'completed work is kept', body)
        self.assertNotIn(b'private details', body)
        self.assertEqual(json.loads(body)['diagnostic']['category'], 'plan_validation')

    def test_fixed_validator_reason_is_visible_without_echoing_arbitrary_exception_text(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        with patch.object(self.server.agents, 'advise', side_effect=ValueError('Invalid plan explanation')), patch('builtins.print') as logged:
            code, body = self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token})
        result = json.loads(body)
        self.assertEqual(code, 502)
        self.assertEqual(result['diagnostic']['reason'], 'Invalid plan explanation')
        self.assertEqual(self.server.stats['lastPlannerError']['category'], 'plan_validation')
        self.assertNotIn('private-test-value', str(logged.call_args_list))
        self.assertTrue(self.server.vision_slot.acquire(blocking=False))
        self.server.vision_slot.release()

    def test_timeout_is_distinguished_from_rejected_plan_and_preserves_prior_work_wording(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        with patch.object(self.server.agents, 'advise', side_effect=TimeoutError('private upstream detail')), patch('builtins.print') as logged:
            code, body = self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token})
        self.assertEqual(code, 502)
        self.assertEqual(json.loads(body)['diagnostic']['category'], 'upstream_timeout')
        self.assertIn(b'No new actions started', body)
        self.assertNotIn(b'private upstream detail', body)
        self.assertNotIn('private upstream detail', str(logged.call_args_list))

    def test_discovery_followup_accepts_completed_goal_in_reused_hosted_session(self):
        import io
        data = request_data(); data['goal'] = 'Automatically discover new ore'; data['state']['deposits'] = []
        key, context = self.server.planner_progress.prepare(data)
        proposed = plan([action('auto_explore')])
        with patch.object(self.server.agents, 'request', return_value=base.events(answer=proposed)):
            first = self.server.agents.advise(context, base.CONFIG, planner_key=key)
        self.server.planner_progress.remember(key, first)
        followup = request_data(); followup['goal'] = data['goal']
        followup['previousPlan'] = {'id': first['planId'], 'actions': first['actions'], 'results': [{'status': 'complete'}]}
        next_key, next_context = self.server.planner_progress.prepare(followup)
        self.assertEqual(next_key, key)
        self.assertEqual(next_context['serverProgress']['newVisibleOreOrigins'], [{'x': 11, 'y': 7}])
        with patch.object(self.server.agents, 'request', side_effect=[base.events(answer=plan([], 'complete'), create=False, turn='turn_2'), io.BytesIO()]):
            result = self.server.agents.advise(next_context, base.CONFIG, planner_key=key)
        self.assertEqual(result['status'], 'complete')
        self.assertEqual(result['actions'], [])
        self.assertEqual(self.server.stats['sessionsReused'], 1)

    def test_coach_and_planner_use_distinct_hosted_conversations(self):
        data = request_data()
        with patch.object(self.server.agents, 'request', return_value=base.events()): self.server.agents.advise(base.payload(), base.CONFIG)
        with patch.object(self.server.agents, 'request', return_value=base.events(answer=plan([action('explore', 10, 6)]))) as upstream:
            self.server.agents.advise(data, base.CONFIG, planner_key='goal-one')
        self.assertEqual(upstream.call_args.args[1], '/sessions')
        self.assertIn('one', self.server.agents.sessions)
        self.assertIn('astrabot:goal-one', self.server.agents.sessions)


if __name__ == '__main__': unittest.main()
