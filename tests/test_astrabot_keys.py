"""Credential boundary checks; fake keys and mocked OpenAI responses only."""
import io
import json
import unittest
from urllib.error import HTTPError
from urllib.request import Request, urlopen
from unittest.mock import patch
import test_coach_server as fixtures
from test_coach_server import coach, payload, events, ANSWER, coaching_body

KEY = 'sk-' + 'fake-key-for-transport-tests-' * 3

class KeyHTTPTests(unittest.TestCase):
    setUp = fixtures.LocalHTTPTests.setUp
    tearDown = fixtures.LocalHTTPTests.tearDown

    def request(self, path, data=None, headers=None):
        req = Request(self.url + path, data=json.dumps(data).encode() if data is not None else None,
                      headers={'Content-Type':'application/json', **(headers or {})})
        try:
            with urlopen(req, timeout=3) as response: return response.status, response.read()
        except HTTPError as error: return error.code, error.read()

    def headers(self, key=KEY):
        return {'X-Astra-Coach':self.server.token, 'X-Astra-OpenAI-Key':key, 'Origin':self.url}

    def test_check_uses_non_generating_model_lookup_without_saving_or_echoing_key(self):
        before = (self.project / 'server/.env').read_bytes()
        replies = [io.BytesIO(b'{"id":"gpt-6-astra"}'), io.BytesIO(b'{"id":"gpt-5.4-mini"}')]
        with patch.object(coach, 'urlopen', side_effect=replies) as upstream:
            code, body = self.request('/api/coach/key', {}, self.headers())
        self.assertEqual((code, json.loads(body)), (200, {'verified':True}))
        requests = [call.args[0] for call in upstream.call_args_list]
        self.assertEqual([req.full_url for req in requests], ['https://api.openai.com/v1/models/gpt-6-astra', 'https://api.openai.com/v1/models/gpt-5.4-mini'])
        for req in requests:
            self.assertEqual(req.get_method(), 'GET')
            self.assertEqual(req.get_header('Authorization'), 'Bearer ' + KEY)
            self.assertIsNone(req.data)
        self.assertEqual((self.project / 'server/.env').read_bytes(), before)
        config = json.loads(self.request('/api/coach/config')[1])
        self.assertTrue(config['acceptsTabKey']); self.assertFalse(config['configured'])
        self.assertNotIn(KEY, json.dumps(config)); self.assertFalse((self.project / 'Logs').exists())

    def test_key_check_requires_same_origin_token_and_bounded_plain_key(self):
        with patch.object(coach, 'urlopen') as upstream:
            self.assertEqual(self.request('/api/coach/key', {}, {'X-Astra-OpenAI-Key':KEY})[0], 403)
            self.assertEqual(self.request('/api/coach/key', {}, {**self.headers(), 'Origin':'https://elsewhere.example'})[0], 403)
            for key in ['not-a-key', 'OPENAI_API_KEY=sk-test', 'sk-' + 'x'*520]:
                self.assertEqual(self.request('/api/coach/key', {}, self.headers(key))[0], 400)
            self.assertEqual(self.request('/api/coach/key', {'key':KEY}, self.headers())[0], 413)
            upstream.assert_not_called()

    def test_tab_request_uses_isolated_ephemeral_agents_and_leaves_default_registry(self):
        registry = self.project / 'Logs/coach-agent-sessions.json'; registry.parent.mkdir()
        registry.write_text('["sess_default"]'); original = registry.read_bytes()
        with patch.object(coach, 'urlopen', side_effect=[events(), io.BytesIO(b'{"deleted":true}')]) as upstream:
            code, body = self.request('/api/coach', payload(), self.headers())
            with self.server.vision_slot: pass
        self.assertEqual(code,200)
        self.assertEqual(coaching_body(body), {**ANSWER, 'grounding':{'status':'unavailable','method':'no-visible-box'},
                                               'planSource':'agents-api','model':'gpt-6-astra','modelRoute':'visual-coach'})
        self.assertEqual(len(upstream.call_args_list),2)
        calls = [c.args[0] for c in upstream.call_args_list]
        self.assertEqual(calls[0].full_url,'https://api.openai.com/v1/agents/sessions')
        self.assertEqual(calls[1].get_method(),'DELETE')
        for req in calls:
            self.assertEqual(req.get_header('Authorization'),'Bearer '+KEY)
            self.assertNotIn(KEY,req.full_url)
            self.assertNotIn(KEY,(req.data or b'').decode())
        self.assertEqual(registry.read_bytes(),original)
        self.assertFalse(self.server.agents.sessions)
        self.assertEqual(self.server.stats['sessionsDeleted'],1)

    def test_invalid_tab_key_never_falls_back_to_the_saved_server_key(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=server-test-key\n')
        with patch.object(coach, 'urlopen', side_effect=HTTPError('https://api.openai.com',401,KEY,None,io.BytesIO(KEY.encode()))) as upstream:
            code, body = self.request('/api/coach', payload(), self.headers())
            with self.server.vision_slot: pass
        self.assertEqual(code,502); self.assertNotIn(KEY.encode(),body)
        upstream.assert_called_once()
        self.assertEqual(upstream.call_args.args[0].get_header('Authorization'),'Bearer '+KEY)
        self.assertTrue(json.loads(self.request('/api/coach/config')[1])['configured'])

    def test_tab_planning_keeps_discovery_history_but_isolates_other_keys(self):
        from test_astrabot_planner import request_data, plan, action
        data = request_data(); data['goal'] = 'Discover new ore'
        first = plan([action('auto_explore')])
        done = plan([], 'complete')
        streams = [events(first), io.BytesIO(b'{"deleted":true}'), events(done), io.BytesIO(b'{"deleted":true}'), events(done), io.BytesIO(b'{"deleted":true}')]
        with patch.object(coach, 'urlopen', side_effect=streams) as upstream:
            self.assertEqual(self.request('/api/astrabot/plan', data, self.headers())[0], 200)
            with self.server.vision_slot: pass
            data['state']['deposits'].append({'origin':{'x':20,'y':6},'resource':'Ore'})
            self.server.last_request = -10
            self.assertEqual(self.request('/api/astrabot/plan', data, self.headers())[0], 200)
            with self.server.vision_slot: pass
            self.server.last_request = -10
            self.assertEqual(self.request('/api/astrabot/plan', data, self.headers('sk-other-fake-key-for-isolation'))[0], 200)
            with self.server.vision_slot: pass
        requests = [call.args[0] for call in upstream.call_args_list if call.args[0].get_method() == 'POST']
        contexts = [json.loads(json.loads(req.data)['input'][0]['content'][0]['text']) for req in requests]
        self.assertEqual(contexts[1]['serverProgress']['newVisibleOreOrigins'], [{'x':20,'y':6}])
        self.assertEqual(contexts[2]['serverProgress']['newVisibleOreOrigins'], [])
        self.assertEqual(len(contexts[1]['serverProgress']['proposedBatches']), 1)
        self.assertEqual(contexts[2]['serverProgress']['proposedBatches'], [])
        self.assertNotIn(KEY, json.dumps(list(self.server.planner_progress.goals.values())))

    def test_key_check_rejection_does_not_echo_openai_error_details(self):
        with patch.object(coach, 'urlopen', side_effect=HTTPError('https://api.openai.com',401,KEY,None,io.BytesIO(KEY.encode()))):
            code, body = self.request('/api/coach/key', {}, self.headers())
        self.assertEqual(code,401); self.assertNotIn(KEY.encode(),body)

    def test_followup_without_override_uses_only_the_original_server_key(self):
        (self.project / 'server/.env').write_text('OPENAI_API_KEY=server-test-key\n')
        with patch.object(coach, 'urlopen', side_effect=[io.BytesIO(b'{"id":"gpt-6-astra"}'), io.BytesIO(b'{"id":"gpt-5.4-mini"}')]):
            self.assertEqual(self.request('/api/coach/key',{},self.headers())[0],200)
        with patch.object(coach, 'urlopen', return_value=events()) as upstream:
            code, _ = self.request('/api/coach',payload(),{'X-Astra-Coach':self.server.token})
        self.assertEqual(code,200)
        self.assertEqual(upstream.call_args.args[0].get_header('Authorization'),'Bearer server-test-key')

if __name__ == '__main__': unittest.main()
