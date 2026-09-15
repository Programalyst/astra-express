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


def mine(resource='Ore', x=11, y=7, size=1, connected=True, rail_connected=True, served=False, **extra):
    return {'kind': 'Extractor', 'resource': resource, 'origin': {'x': x, 'y': y}, 'port': {'x': x, 'y': y - 1},
            'size': size, 'demand': size, 'connected': connected, 'railConnected': rail_connected, 'served': served,
            **extra}


def prepared_expansion(data):
    """Prepare the same immutable goal baseline the HTTP planner receives."""
    return planner.PlannerProgress().prepare(data)[1]


class PlanValidationTests(unittest.TestCase):
    def test_model_router_keeps_only_pure_rover_discovery_on_mini(self):
        mini = 'gpt-5.4-mini'; astra = 'gpt-6-astra'
        for goal in ['Discover the map with the rover', 'Uncover fog of war', 'Automatically discover new ore', 'Survey and reveal more of the map']:
            with self.subTest(goal=goal):
                self.assertEqual(planner.route_planner_model({'goal': goal}, astra, mini),
                                 {'model': mini, 'route': 'rover-exploration'})
        for goal in ['Explore, then build an extractor', 'Discover ore and connect a conduit', 'Connect the solar panel', 'Set up a paying ore route', 'Help me']:
            with self.subTest(goal=goal):
                self.assertEqual(planner.route_planner_model({'goal': goal}, astra, mini),
                                 {'model': astra, 'route': 'advanced-visual'})

    def test_payload_keeps_selected_tile_and_bounded_progress(self):
        data = request_data(); planner.validate_plan_payload(data)
        request = planner.build_planner_request(data, 'gpt-6-astra', 'Game facts.')
        content = request['input'][0]['content']
        self.assertEqual(content[1]['image_url'], data['image'])
        self.assertEqual(json.loads(content[0]['text'])['selectedTile'], data['selectedTile'])
        self.assertEqual(request['environment'], {'type': 'none'})
        self.assertEqual(request['agent']['tools'], [])
        self.assertEqual(request['agent']['model'], 'gpt-6-astra')
        self.assertEqual(request['agent']['reasoning'], {'effort': 'low'})
        for change in [{'goal': ''}, {'goal': 'g' * 601}, {'selectedTile': {'x': 32, 'y': 1}}, {'previousPlan': {'results': [{}] * 61}}]:
            with self.assertRaises(ValueError): planner.validate_plan_payload({**data, **change})

    def test_new_mine_ends_batch_then_fresh_state_can_connect_and_dispatch(self):
        data = request_data()
        result = planner.parse_plan(json.dumps(plan([action('build_extractor', 11, 7)])), data)
        self.assertEqual(result['actions'][0]['type'], 'build_extractor')
        self.assertTrue(result['planId'])
        bounded = planner.parse_plan(json.dumps(plan([action('build_extractor', 11, 7), action('connect_conduit', 11, 7)])), data)
        self.assertEqual([item['type'] for item in bounded['actions']], ['build_extractor'])
        self.assertIn('real port', bounded['summary'])

        built = mine(connected=False, rail_connected=False,
                     powerRoute={'possible': True, 'cost': 12}, railRoute={'possible': True, 'cost': 18})
        data['state'].update(buildings=[built], deposits=[], credits=500)
        followup = planner.parse_plan(json.dumps(plan([
            action('connect_conduit', 11, 7), action('connect_rail', 11, 6), action('dispatch_train', 11, 7)
        ])), data)
        self.assertEqual([item['type'] for item in followup['actions']], ['connect_conduit', 'connect_rail', 'dispatch_train'])

    def test_hidden_deposit_duplicate_build_and_unknown_connection_rejected(self):
        data = request_data()
        for actions in [[action('build_extractor', 20, 6)], [action('connect_conduit', 20, 6)],
                        [action('build_extractor', 11, 7), action('build_extractor', 11, 7, id='second')]]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan(actions)), data)

    def test_unknown_tools_arbitrary_code_and_extra_fields_rejected(self):
        for bad in [action('reset'), action('sell'), action('shell', command='rm -rf /'), action('explore', 7, 6, url='https://example.com')]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([bad])), request_data())

    def test_visual_diagnosis_repair_cannot_change_target_or_scope(self):
        data = request_data()
        data['goal'] = 'Select the extractor at (11, 7) and connect its south port to the colony shared power grid. Stop when simulation state confirms it is power-connected, or explain the blocker.'
        data['state'].update(buildings=[mine(connected=False, rail_connected=False,
                                             powerRoute={'possible':True,'cost':8})], deposits=[])
        accepted = planner.parse_plan(json.dumps(plan([action('select',11,7), action('connect_conduit',11,7)])), data)
        self.assertEqual([item['type'] for item in accepted['actions']], ['select','connect_conduit'])
        for actions in [[action('connect_conduit',12,7)], [action('connect_rail',11,7)], [action('build_solar',11,7)]]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan(actions)), data)

    def test_out_of_bounds_fractional_and_boolean_coordinates_rejected(self):
        for x, y in [(32, 5), (5, 32), (-1, 0), (1.5, 5), (True, 5)]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('explore', x, y)])), request_data())

    def test_expanded_grid_corner_is_valid(self):
        data = request_data()
        planner.validate_plan_payload({**data, 'selectedTile': {'x': 31, 'y': 31}})
        result = planner.parse_plan(json.dumps(plan([action('explore', 31, 31)])), data)
        self.assertEqual((result['actions'][0]['x'], result['actions'][0]['y']), (31, 31))

    def test_exploration_ends_batch_before_building_unseen_future_resources(self):
        bounded = planner.parse_plan(json.dumps(plan([action('explore', 10, 6), action('build_extractor', 11, 7)])), request_data())
        self.assertEqual([item['type'] for item in bounded['actions']], ['explore'])
        self.assertIn('fresh map', bounded['nextCheck'])
        self.assertEqual(planner.parse_plan(json.dumps(plan([action('explore', 10, 6)])), request_data())['status'], 'ready')

    def test_auto_exploration_is_coordinate_free_and_requires_a_fresh_batch(self):
        data = request_data()
        self.assertEqual(planner.parse_plan(json.dumps(plan([action('auto_explore')])), data)['actions'][0]['type'], 'auto_explore')
        for actions in [[action('auto_explore', 20, 6)], [action('auto_explore', seconds=60)],
                        [action('auto_explore', targetX=20, targetY=6)]]:
            with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan(actions)), data)
        bounded = planner.parse_plan(json.dumps(plan([action('auto_explore'), action('build_extractor', 11, 7)])), data)
        self.assertEqual([item['type'] for item in bounded['actions']], ['auto_explore'])
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

    def test_expansion_goal_parses_vague_additional_and_total_counts(self):
        data = request_data(); data['state']['buildings'] = [mine()]
        cases = [
            ('Build more Ore extractors and power them with solar', 'additional', 1, 2, True, False),
            ('Build two additional Ore extractors and connect them to solar power', 'additional', 2, 3, True, False),
            ("Build two additional Ore extractors with solar power. Do not add rails or trains.", 'additional', 2, 3, True, False),
            ('Expand to four working Ore train routes', 'total', 3, 4, False, True),
        ]
        for goal, mode, additional, target, solar, service in cases:
            with self.subTest(goal=goal):
                spec = planner.expansion_spec(goal, data['state'])
                self.assertEqual((spec['resource'], spec['mode']), ('Ore', mode))
                self.assertEqual(spec['requestedAdditionalExtractors'], additional)
                self.assertEqual(spec['targetExtractorCount'], target)
                self.assertEqual(spec['requiresSolarCapacity'], solar)
                self.assertEqual(spec['requiresRailService'], service)

    def test_expansion_progress_preserves_baseline_and_counts_verified_buildings(self):
        progress = planner.PlannerProgress(); data = request_data()
        data['goal'] = 'Build two additional Ore extractors and power them with solar'
        data['state'].update(buildings=[mine()], solarGeneration=2)
        key, first = progress.prepare(data)
        objective = first['serverProgress']['expansionObjective']
        self.assertEqual(objective['initialExtractorOrigins'], [{'x': 11, 'y': 7}])
        self.assertEqual(objective['newExtractorCount'], 0)
        self.assertEqual(objective['targetExtractorCount'], 3)

        # Proposed actions and disappearing deposit entries are not completion evidence.
        progress.remember(key, planner.parse_plan(json.dumps(plan([action('wait', seconds=1)])), first))
        data['state']['deposits'] = []
        data['state']['buildings'].append(mine(x=15, y=11, size=2))
        followup = progress.prepare(data)[1]['serverProgress']['expansionObjective']
        self.assertEqual(followup['initialExtractorOrigins'], objective['initialExtractorOrigins'])
        self.assertEqual(followup['newExtractorCount'], 1)
        self.assertEqual(followup['remainingExtractorCount'], 1)
        self.assertFalse(followup['goalSatisfied'])

    def test_expansion_completion_requires_count_power_capacity_and_service_in_fresh_state(self):
        progress = planner.PlannerProgress(); data = request_data()
        data['goal'] = 'Build two additional Ore extractors and power them with solar'
        data['state'].update(buildings=[mine()], solarGeneration=2)
        key, initial = progress.prepare(data)
        recovered = planner.parse_plan(json.dumps(plan([], 'complete')), initial)
        self.assertNotEqual(recovered['status'], 'complete')

        data['state']['buildings'] = [mine(), mine(x=15, y=11, size=1), mine(x=20, y=6, connected=False)]
        disconnected = progress.prepare(data)[1]
        recovered = planner.parse_plan(json.dumps(plan([], 'complete')), disconnected)
        self.assertNotEqual(recovered['status'], 'complete')

        data['state']['buildings'][-1]['connected'] = True
        data['state']['solarGeneration'] = 2
        undersupplied = progress.prepare(data)[1]
        self.assertEqual(undersupplied['serverProgress']['expansionObjective']['ratedExtractorDemand'], 3)
        recovered = planner.parse_plan(json.dumps(plan([], 'complete')), undersupplied)
        self.assertNotEqual(recovered['status'], 'complete')

        data['state']['solarGeneration'] = 4
        complete = progress.prepare(data)[1]
        accepted = planner.parse_plan(json.dumps(plan([], 'complete')), complete)
        self.assertTrue(accepted['goalProgress']['goalSatisfied'])
        self.assertEqual(accepted['goalProgress']['newExtractorCount'], 2)

        service_progress = planner.PlannerProgress(); service_data = request_data()
        service_data['goal'] = 'Build one additional working Ore extractor with rail service'
        service_data['state']['buildings'] = [mine(served=True)]
        service_progress.prepare(service_data)
        service_data['state']['buildings'].append(mine(x=15, y=11, served=False))
        unserved = service_progress.prepare(service_data)[1]
        with self.assertRaisesRegex(ValueError, 'services are not yet working'):
            planner.parse_plan(json.dumps(plan([], 'complete')), unserved)
        service_data['state']['buildings'][-1]['served'] = True
        served = service_progress.prepare(service_data)[1]
        self.assertTrue(planner.parse_plan(json.dumps(plan([], 'complete')), served)['goalProgress']['goalSatisfied'])

    def test_solar_capacity_uses_rated_building_demand_even_when_live_demand_is_zero(self):
        data = request_data(); data['goal'] = 'Build another Ore extractor powered by solar'
        data['state'].update(buildings=[mine(), mine(x=15, y=11, size=2)], solarGeneration=2, demand=0)
        objective = prepared_expansion(data)['serverProgress']['expansionObjective']
        self.assertEqual(objective['ratedExtractorDemand'], 3)
        self.assertEqual(objective['currentSolarShortfall'], 1)
        self.assertFalse(objective['goalSatisfied'])

    def test_solar_capacity_must_precede_next_extractor_and_its_connection(self):
        progress = planner.PlannerProgress(); data = request_data()
        data['goal'] = 'Build another Ore extractor and power it with solar'
        data['state'].update(buildings=[mine()], solarGeneration=2, solarSite={'x': 2, 'y': 11},
                             solarSitePowerRoute={'possible': True, 'cost': 10}, credits=500,
                             deposits=[{'origin': {'x': 15, 'y': 11}, 'cost': 250, 'size': 2, 'resource': 'Ore'}])
        _, initial = progress.prepare(data)
        with self.assertRaisesRegex(ValueError, 'Add solar capacity'):
            planner.validate_actions([action('build_extractor', 15, 11)], initial)
        accepted = planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11), action('build_extractor', 15, 11)])), initial)
        self.assertEqual([item['type'] for item in accepted['actions']], ['build_solar', 'build_extractor'])

        data['state']['buildings'].append(mine(x=15, y=11, size=2, connected=False,
                                                   powerRoute={'possible': True, 'cost': 14}))
        data['state']['deposits'] = []
        followup = progress.prepare(data)[1]
        with self.assertRaisesRegex(ValueError, 'Add solar capacity'):
            planner.validate_actions([action('connect_conduit', 15, 11)], followup)
        connected = planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11), action('connect_conduit', 15, 11)])), followup)
        self.assertEqual([item['type'] for item in connected['actions']], ['build_solar', 'connect_conduit'])

    def test_power_only_expansion_rejects_transport_but_explicit_service_goal_allows_it(self):
        data = request_data(); data['goal'] = 'Power another Ore extractor with solar'
        data['state'].update(buildings=[mine(rail_connected=False, railRoute={'possible': True, 'cost': 18})], deposits=[])
        power_only = prepared_expansion(data)
        for forbidden in [action('connect_rail', 11, 7), action('dispatch_train', 11, 7)]:
            with self.subTest(action=forbidden['type']), self.assertRaisesRegex(ValueError, 'Transport was not requested'):
                planner.validate_actions([forbidden], power_only)

        data['goal'] = 'Connect this Ore extractor by rail and dispatch a train'
        service_goal = prepared_expansion(data)
        accepted = planner.parse_plan(json.dumps(plan([action('connect_rail', 11, 7)])), service_goal)
        self.assertEqual(accepted['actions'][0]['type'], 'connect_rail')

    def test_guarded_power_expansion_recovers_bad_order_across_fresh_state_batches(self):
        progress = planner.PlannerProgress(); data = request_data()
        data['goal'] = 'Build two additional Ore extractors and connect them to solar power'
        data['state'].update(buildings=[], solarGeneration=2, credits=1000, frontier={'x': 10, 'y': 7},
                             solarSite={'x': 2, 'y': 11}, solarSitePowerRoute={'possible': True, 'cost': 10})
        key, current = progress.prepare(data)
        recovered = planner.parse_plan(json.dumps(plan([action('connect_conduit', 11, 7)])), current)
        self.assertEqual([a['type'] for a in recovered['actions']], ['build_extractor'])

        data['state']['buildings'] = [mine(connected=False, rail_connected=False,
                                                  powerRoute={'possible': True, 'cost': 12})]
        data['state']['deposits'] = []
        current = progress.prepare(data)[1]
        recovered = planner.parse_plan(json.dumps(plan([], 'complete')), current)
        self.assertEqual([a['type'] for a in recovered['actions']], ['connect_conduit'])

        data['state']['buildings'][0]['connected'] = True
        data['state']['deposits'] = [{'origin': {'x': 15, 'y': 11}, 'cost': 250, 'size': 2, 'resource': 'Ore'}]
        current = progress.prepare(data)[1]
        recovered = planner.parse_plan(json.dumps(plan([action('build_extractor', 15, 11)])), current)
        self.assertEqual([a['type'] for a in recovered['actions']], ['build_solar'])

        data['state']['buildings'].append({'kind': 'Solar', 'origin': {'x': 2, 'y': 11}, 'connected': True})
        data['state']['solarGeneration'] = 4
        current = progress.prepare(data)[1]
        recovered = planner.parse_plan(json.dumps(plan([], 'complete')), current)
        self.assertEqual([a['type'] for a in recovered['actions']], ['build_extractor'])

        data['state']['buildings'].append(mine(x=15, y=11, size=2, connected=False, rail_connected=False,
                                                powerRoute={'possible': True, 'cost': 14}))
        data['state']['deposits'] = []
        current = progress.prepare(data)[1]
        recovered = planner.parse_plan(json.dumps(plan([], 'complete')), current)
        self.assertEqual([a['type'] for a in recovered['actions']], ['connect_conduit'])

        data['state']['buildings'][-1]['connected'] = True
        current = progress.prepare(data)[1]
        complete = planner.parse_plan(json.dumps(plan([], 'blocked')), current)
        self.assertEqual(complete['status'], 'complete')
        self.assertTrue(complete['goalProgress']['goalSatisfied'])

    def test_solar_build_includes_known_wiring_and_reconnect_does_not_repurchase(self):
        missing_route = request_data(); missing_route['state'].update(solarSite={'x': 2, 'y': 11}, credits=200)
        with self.assertRaisesRegex(ValueError, 'Solar connection route required'):
            planner.parse_plan(json.dumps(plan([action('build_solar', 2, 11)])), missing_route)
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

    def test_separate_train_purchases_are_unsupported(self):
        data = request_data()
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('buy_train')])), data)
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
        data['state'].update(extractorOwnedTrains=True, trains=[{'owner': {'x': 11, 'y': 7}, 'phase': 'Parked'}])
        planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), data)
        data['state']['trains'] = [{'owner': {'x': 8, 'y': 13}, 'phase': 'Parked'}]
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), data)

    def test_fluxite_dispatch_has_explicit_power_plant_destination_not_colony(self):
        data = request_data(); data['state']['buildings'] = [mine('Fluxite'), {'kind': 'PowerPlant', 'origin': {'x': 3, 'y': 11}, 'connected': True, 'railConnected': True}]
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7)])), data)
        planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7, targetX=3, targetY=11)])), data)
        with self.assertRaises(ValueError): planner.parse_plan(json.dumps(plan([action('dispatch_train', 11, 7, targetX=5, targetY=7)])), data)

    def test_construction_uses_known_site_or_explicit_tile(self):
        data = request_data(); data['state'].update(solarSite={'x': 2, 'y': 11},
                                                   solarSitePowerRoute={'possible': True, 'cost': 12})
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
        self.assertEqual(json.loads(body)['routing'], {'roverExploration':'gpt-5.4-mini', 'advancedVisual':'gpt-6-astra'})

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
        self.assertEqual(upstream.call_args.args[2]['agent']['model'], 'gpt-6-astra')
        self.assertEqual(upstream.call_args.args[2]['metadata']['purpose'], 'astrabot-game-planning')
        self.assertEqual(result['model'], 'gpt-6-astra')
        self.assertEqual(result['modelRoute'], 'advanced-visual')
        self.assertEqual(self.server.stats['routedAstraPlans'], 1)
        self.assertEqual(self.server.stats['plansCompleted'], 1)
        self.assertEqual(self.request('/api/astrabot/plan', request_data(), {'X-Astra-Coach': self.server.token})[0], 429)

    def test_pure_rover_discovery_is_routed_to_mini_and_reported(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        data = request_data(); data['goal'] = 'Uncover fog of war with the rover and discover new ore'
        proposed = plan([action('auto_explore')])
        with patch.object(self.server.agents, 'request', return_value=base.events(answer=proposed)) as upstream:
            code, body = self.request('/api/astrabot/plan', data, {'X-Astra-Coach': self.server.token})
        self.assertEqual(code, 200)
        result = json.loads(body)
        self.assertEqual(upstream.call_args.args[2]['agent']['model'], 'gpt-5.4-mini')
        self.assertEqual(result['model'], 'gpt-5.4-mini')
        self.assertEqual(result['modelRoute'], 'rover-exploration')
        self.assertEqual(self.server.stats['routedExplorationPlans'], 1)
        self.assertEqual(self.server.stats['lastPlanTiming']['route'], 'rover-exploration')

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
