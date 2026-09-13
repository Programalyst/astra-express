import base64
import copy
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

spec = importlib.util.spec_from_file_location('coach_server', Path(__file__).resolve().parents[1] / 'server/coach_server.py')
coach = importlib.util.module_from_spec(spec)
spec.loader.exec_module(coach)
MODEL_ANSWER = {'body': 'Reach the fog.', 'observation': "I can't clearly see a reliable target.",
                'taskSuggestion': 'discover-ore',
                'visualEvidence': {'visible': False, 'label': '', 'xMin': 0, 'yMin': 0, 'xMax': 0, 'yMax': 0}}
ANSWER = {'actionId': 'explore', 'title': 'Explore', **MODEL_ANSWER}
CONFIG = {'key': 'private-test-value', 'model': 'gpt-6-astra'}


def payload():
    # Header fixture for transport validation, not a visual or real-model test image.
    return {'image': 'data:image/jpeg;base64,' + base64.b64encode(b'\xff\xd8\xff' + b'0' * 200).decode(),
            'state': {'session': 'one', 'credits': 500},
            'candidates': [{'id': 'explore', 'title': 'Explore', 'body': 'Find ore.', 'steps': ['Press 1.']}],
            'events': [], 'question': 'What next?', 'capturedAt': time.time() * 1000}


def coaching_body(body):
    result = json.loads(body)
    duration = result.pop('durationMs'); age = result.pop('frameAgeMs')
    assert isinstance(duration, (int, float)) and duration >= 0
    assert isinstance(age, (int, float)) and age >= 0
    return result


def events(answer=MODEL_ANSWER, create=True, terminal='agent.session.turn.completed', phase='final_answer', turn='turn_1'):
    result = []
    if create:
        result.append({'type': 'agent.session.created', 'session': {'id': 'sess_test'}})
    result.append({'type': 'agent.session.idle'})
    result.append({'type': 'agent.session.turn.item.done', 'item': {'type': 'message', 'role': 'assistant', 'phase': phase,
                   'status': 'completed', 'turn_id': turn, 'content': [{'type': 'output_text', 'text': json.dumps(answer)}]}})
    if terminal:
        result.append({'type': terminal, 'turn_id': turn, 'turn': {'id': turn, 'subagent_id': None}})
    return io.BytesIO(b''.join(b'data: ' + json.dumps(e).encode() + b'\n\n' for e in result))


class ProtocolTests(unittest.TestCase):
    def test_diagnostic_categories_never_include_unrecognized_exception_arguments(self):
        cases = [(coach.AgentTurnError('No completed final answer for this turn'), 'missing_final_answer'),
                 (coach.AgentTurnError('Stream ended before a completed answer'), 'stream_incomplete'),
                 (json.JSONDecodeError('private model text', 'private', 0), 'invalid_json_response'),
                 (KeyError('private field'), 'missing_response_field'),
                 (ValueError('private response'), 'plan_validation')]
        for error, category in cases:
            detail = coach.planner_error_details(error)
            self.assertEqual(detail['category'], category)
            self.assertNotIn('private', json.dumps(detail))

    def test_client_disconnect_is_safely_ignored_by_json_writer(self):
        from unittest.mock import Mock
        handler = object.__new__(coach.CoachHandler)
        handler.send_response = Mock(); handler.send_header = Mock(); handler.end_headers = Mock()
        handler.wfile = Mock(); handler.wfile.write.side_effect = BrokenPipeError()
        handler.json_response(502, {'error': 'No new actions started'})

    def test_agents_request_has_current_image_and_no_tools_or_sandbox(self):
        data = payload()
        data['state'].update({'uiAnchors':[{'id':'fleet','x':.1,'y':.2,'width':.1,'height':.1}],
                              'frontier':{'x':8,'y':9,'screenX':.4,'screenY':.5},
                              'railRoute':[{'x':1,'y':2}]})
        data['candidates'][0].update({'uiTarget':'fleet','target':{'x':8,'y':9,'screenX':.4,'screenY':.5}})
        coach.validate_payload(data)
        request = coach.build_request(data, CONFIG['model'])
        self.assertEqual(request['environment'], {'type': 'none'})
        self.assertEqual(request['agent']['model'], CONFIG['model'])
        self.assertEqual(request['agent']['tools'], [])
        self.assertFalse(request['agent']['multi_agent']['enabled'])
        self.assertEqual(request['agent']['reasoning'], {'effort': 'low'})
        self.assertEqual(request['input'][0]['content'][1], {'type': 'input_image', 'image_url': data['image']})
        self.assertEqual(request['agent']['text']['format']['type'], 'json_schema')
        for field in ['store', 'max_output_tokens', 'instructions']:
            self.assertNotIn(field, request)
        self.assertNotIn('Authorization', json.dumps(request))
        visible_context = request['input'][0]['content'][0]['text']
        for withheld in ['screenX', 'screenY', 'uiAnchors', 'frontier', 'railRoute', 'uiTarget', 'target', 'actionChoices', 'Press 1']:
            self.assertNotIn(withheld, visible_context)

    def test_invalid_frame_context_and_session_rejected(self):
        for bad in ['https://example.com/image.jpg', 'data:image/jpeg;base64,!!!!', 'data:image/png;base64,aGVsbG8=']:
            with self.assertRaises(ValueError): coach.validate_payload({**payload(), 'image': bad})
        for bad in ['', 'x' * 129, 4]:
            with self.assertRaises(ValueError): coach.validate_payload({**payload(), 'state': {'session': bad}})
        with self.assertRaises(ValueError): coach.validate_payload({**payload(), 'question': 'a' * 301})

    def test_stale_actions_or_replacement_steps_are_rejected(self):
        for bad in [{**MODEL_ANSWER, 'actionId': 'old-action'}, {**MODEL_ANSWER, 'steps': ['Build instantly']}, {**MODEL_ANSWER, 'body': None}]:
            with self.assertRaises(ValueError): coach.parse_advice(json.dumps(bad), payload())
        self.assertEqual(coach.parse_advice(json.dumps(MODEL_ANSWER), payload()), ANSWER)

    def test_visual_box_requires_visible_evidence_and_i_see(self):
        valid = {**MODEL_ANSWER, 'observation':'I see the rover at the fog edge.',
                 'visualEvidence':{'visible':True,'label':'rover','xMin':350,'yMin':420,'xMax':460,'yMax':540}}
        self.assertEqual(coach.parse_advice(json.dumps(valid), payload()), {'actionId':'explore','title':'Explore',**valid})
        for bad in [{**valid, 'observation':'The rover is visible.'},
                    {**valid, 'visualEvidence':{**valid['visualEvidence'],'xMax':351}},
                    {**MODEL_ANSWER, 'observation':'I see nothing reliable.'}]:
            with self.assertRaises(ValueError): coach.parse_advice(json.dumps(bad), payload())

    def test_visual_grounding_is_checked_after_inference(self):
        data = payload(); data['candidates'][0]['target'] = {'x':8,'y':9}
        data['state']['frontier'] = {'x':8,'y':9,'screenX':.4,'screenY':.5}
        result = {**ANSWER, 'observation':'I see the rover at the fog edge.',
                  'visualEvidence':{'visible':True,'label':'rover','xMin':350,'yMin':450,'xMax':450,'yMax':550}}
        self.assertEqual(coach.ground_visual_evidence(result, data)['status'], 'matched')
        result['visualEvidence'].update({'xMin':700,'xMax':800})
        self.assertEqual(coach.ground_visual_evidence(result, data)['status'], 'missed')



class ManagedAgentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.project = Path(self.temp.name)
        self.stats = {'sessionsCreated': 0, 'sessionsReused': 0, 'sessionsDeleted': 0, 'cleanupFailures': 0}
        self.agent = coach.ManagedCoach(self.project, self.stats)

    def tearDown(self): self.temp.cleanup()

    def test_initial_and_followup_use_same_session_and_subscribe_before_input(self):
        with patch.object(self.agent, 'request', side_effect=[events(), events(create=False, turn='turn_2'), io.BytesIO(b'')]) as request:
            self.assertEqual(self.agent.advise(payload(), CONFIG), ANSWER)
            self.assertEqual(self.agent.advise(payload(), CONFIG), ANSWER)
        calls = request.call_args_list
        self.assertEqual([c.args[1] for c in calls], ['/sessions', '/sessions/sess_test/events?stream=true', '/sessions/sess_test/events'])
        self.assertEqual(len(calls[1].args), 2)  # Subscribe GET before event POST.
        posted = calls[2].args[2]
        self.assertEqual(posted['events'][0]['type'], 'agent.session.input.message')
        self.assertIn('idempotency_key', calls[2].kwargs)
        self.assertNotIn('idempotency_key', posted)
        self.assertEqual(self.stats['sessionsCreated'], 1)
        self.assertEqual(self.stats['sessionsReused'], 1)
        self.assertEqual(self.agent.sessions['one']['turns'], 2)
        registry = self.agent.registry.read_text()
        self.assertEqual(json.loads(registry), ['sess_test'])
        self.assertNotIn('image', registry)
        self.assertNotIn(CONFIG['key'], registry)

    def test_idle_eof_commentary_failure_or_wrong_turn_never_count_as_answer(self):
        for stream in [events(terminal=None), events(terminal='agent.session.turn.failed'), events(phase='commentary'),
                       io.BytesIO(b'data: {"type":"agent.session.idle"}\n\n')]:
            with self.assertRaises(ValueError):
                self.agent.read_events(stream, payload(), lambda sid: None, time.monotonic() + 1)
        wrong_turn = events().getvalue().replace(b'"turn_id": "turn_1", "turn"', b'"turn_id": "turn_wrong", "turn"')
        with self.assertRaises(ValueError):
            self.agent.read_events(io.BytesIO(wrong_turn), payload(), lambda sid: None, time.monotonic() + 1)
        with self.assertRaises(ValueError):
            self.agent.read_events(events(), payload(), lambda sid: None, time.monotonic() - 1)

    def test_turn_limit_rotates_without_duplicate_saved_agents(self):
        with patch.object(self.agent, 'request', return_value=events()):
            self.agent.advise(payload(), CONFIG)
        self.agent.sessions['one']['turns'] = coach.SESSION_TURNS - 1
        with patch.object(self.agent, 'request', side_effect=[events(create=False), io.BytesIO()]):
            self.agent.advise(payload(), CONFIG)
        self.assertNotIn('one', self.agent.sessions)
        self.assertIn('sess_test', self.agent.pending_delete)

    def test_model_or_key_changes_do_not_reuse_old_conversation(self):
        for change in [{'model': 'test-model'}, {'key': 'different-test-key'}]:
            self.agent.sessions.clear()
            with patch.object(self.agent, 'request', return_value=events()): self.agent.advise(payload(), CONFIG)
            with patch.object(self.agent, 'request', return_value=events()) as request:
                self.agent.advise(payload(), {**CONFIG, **change})
            self.assertEqual(request.call_args.args[1], '/sessions')

    def test_timeout_retires_and_deletes_active_session(self):
        with patch.object(self.agent, 'request', side_effect=[events(terminal=None), io.BytesIO(b'{"deleted":true}')]) as request:
            with self.assertRaises(ValueError): self.agent.advise(payload(), CONFIG)
        self.assertEqual(request.call_args.kwargs['method'], 'DELETE')
        self.assertEqual(self.stats['sessionsDeleted'], 1)
        self.assertFalse(self.agent.sessions)

    def test_idle_cleanup_and_restart_preserve_failed_deletion_for_retry(self):
        with patch.object(self.agent, 'request', return_value=events()): self.agent.advise(payload(), CONFIG)
        self.agent.sessions['one']['last'] -= coach.SESSION_IDLE + 1
        with patch.object(self.agent, 'request', side_effect=OSError('unavailable')): self.agent.cleanup(CONFIG)
        self.assertIn('sess_test', self.agent.pending_delete)
        self.assertEqual(self.stats['cleanupFailures'], 1)
        after_restart = coach.ManagedCoach(self.project, self.stats)
        self.assertFalse(after_restart.sessions)
        self.assertIn('sess_test', after_restart.pending_delete)
        with patch.object(after_restart, 'request', return_value=io.BytesIO(b'{"deleted":true}')): after_restart.cleanup(CONFIG)
        self.assertEqual(json.loads(after_restart.registry.read_text()), [])

    def test_documented_transport_endpoint_and_beta_header(self):
        with patch.object(coach, 'urlopen', return_value=io.BytesIO(b'{}')) as upstream:
            with self.agent.request(CONFIG, '/sessions', coach.build_request(payload(), CONFIG['model'])): pass
        request = upstream.call_args.args[0]
        self.assertEqual(request.full_url, 'https://api.openai.com/v1/agents/sessions')
        self.assertEqual(request.get_header('Openai-beta'), 'agents=v1')


class LocalHTTPTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.project = Path(self.temp.name)
        (self.project / 'Builds/Web').mkdir(parents=True); (self.project / 'server').mkdir()
        (self.project / 'Builds/Web/index.html').write_text('Astra Express')
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=\n')
        self.server = coach.CoachServer(('127.0.0.1', 0), self.project)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True); self.thread.start()
        self.url = 'http://127.0.0.1:' + str(self.server.server_port)
        self.env = patch.dict(coach.os.environ, {}, clear=True); self.env.start()

    def tearDown(self):
        self.server.shutdown(); self.server.server_close(); self.thread.join(); self.env.stop(); self.temp.cleanup()

    def request(self, path, data=None, headers=None):
        request = Request(self.url + path, data=json.dumps(data).encode() if data else None,
                          headers={'Content-Type': 'application/json', **(headers or {})})
        try:
            with urlopen(request, timeout=3) as response: return response.status, response.read()
        except HTTPError as error: return error.code, error.read()

    def test_static_host_does_not_serve_secrets_registry_or_traversal(self):
        self.assertEqual(self.request('/')[0], 200)
        for path in ['/server/.env', '/../../server/.env', '/%2e%2e/%2e%2e/server/.env', '/Logs/coach-agent-sessions.json', '/Build/']:
            self.assertEqual(self.request(path)[0], 404)

    def test_cross_origin_and_missing_token_blocked(self):
        self.assertEqual(self.request('/api/coach', payload())[0], 403)
        self.assertEqual(self.request('/api/coach', payload(), {'X-Astra-Coach': self.server.token, 'Origin': 'https://evil.example'})[0], 403)

    def test_missing_key_hot_reload_and_honest_engine_never_expose_key(self):
        _, body = self.request('/api/coach/config'); self.assertFalse(json.loads(body)['configured'])
        self.assertEqual(json.loads(body)['engine'], 'agents-api')
        code, _ = self.request('/api/coach', payload(), {'X-Astra-Coach': self.server.token}); self.assertEqual(code, 503)
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        _, body = self.request('/api/coach/config'); self.assertTrue(json.loads(body)['configured']); self.assertNotIn(b'private-test-value', body)

    def test_request_reaches_agents_and_returns_validated_advice(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        with patch.object(self.server.agents, 'request', return_value=events()) as upstream:
            code, body = self.request('/api/coach', payload(), {'X-Astra-Coach': self.server.token})
            self.assertEqual(code, 200)
            self.assertEqual(coaching_body(body), {**ANSWER, 'grounding':{'status':'unavailable','method':'no-visible-box'},
                                                   'planSource':'agents-api','model':'gpt-6-astra','modelRoute':'visual-coach'})
            self.assertEqual(upstream.call_args.args[1], '/sessions')
        self.assertEqual(self.server.stats['completed'], 1)
        code, _ = self.request('/api/coach', payload(), {'X-Astra-Coach': self.server.token}); self.assertEqual(code, 429)

    def test_upstream_failure_is_sanitized_local_fallback(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=private-test-value\n')
        with patch.object(self.server.agents, 'advise', side_effect=ValueError('secret upstream payload')):
            code, body = self.request('/api/coach', payload(), {'X-Astra-Coach': self.server.token})
        self.assertEqual(code, 502)
        self.assertNotIn(b'secret upstream payload', body)
        self.assertIn(b'game tip shown', body)
        self.assertEqual(self.server.stats['failed'], 1)


if __name__ == '__main__': unittest.main()
