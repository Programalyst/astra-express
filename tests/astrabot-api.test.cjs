const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(process.env.ASTRA_TEST_PROJECT || path.resolve(__dirname,'..'), 'Assets/WebGLTemplates/Astra/astrabot-api.js'),'utf8');
const KEY = 'sk-fake-key-for-transport-testing-only';
const result = data => ({ok:true,json:async()=>data});
function harness({configured=false, verify}={}) {
  const calls=[], events=[], listeners={}, window={addEventListener:(n,fn)=>{listeners[n]=fn;}};
  vm.runInNewContext(source,{window,document:{dispatchEvent:e=>events.push(e.type)},CustomEvent:class {constructor(type){this.type=type;}},AbortController,setTimeout,clearTimeout,
    localStorage:{setItem(){assert.fail('Keys must not be stored');}},sessionStorage:{setItem(){assert.fail('Keys must not be stored');}},
    fetch:async(url,options)=>{calls.push({url,options});return url==='/api/coach/config'?result({engine:'agents-api',token:'csrf',configured,acceptsTabKey:true}):url==='/api/coach/key'?(verify?verify():result({verified:true})):result({});}});
  return {api:window.astraBotAPI,calls,events,listeners};
}
test('tab key travels only in allowed same-origin POST headers, never config or body',async()=>{
  const h=harness();await h.api.connect(KEY);
  assert.equal(h.api.hasTabKey(),true);assert.equal((await h.api.config()).configured,true);
  await h.api.request('/api/coach',{method:'POST',headers:{'X-Astra-Coach':'csrf'},body:'{"question":"help"}'});
  await h.api.request('/api/astrabot/plan',{method:'POST',body:'{}'});
  for(const call of h.calls) {
    assert.equal(call.options.redirect,'error');assert.equal(call.options.credentials,'same-origin');
    assert.ok(!call.url.includes(KEY));assert.ok(!(call.options.body||'').includes(KEY));
    assert.equal(call.options.headers?.['X-Astra-OpenAI-Key'], call.url==='/api/coach/config'?undefined:KEY);
  }
  assert.ok(!JSON.stringify(await h.api.config()).includes(KEY));
});
test('forget and reload clear the override, leaving configured server fallback usable',async()=>{
  const h=harness({configured:true});await h.api.connect(KEY);h.api.forget();
  assert.equal(h.api.hasTabKey(),false);assert.equal((await h.api.config()).configured,true);
  await h.api.request('/api/coach',{method:'POST'});assert.equal(h.calls.at(-1).options.headers['X-Astra-OpenAI-Key'],undefined);
  await h.api.connect(KEY);h.listeners.pagehide();assert.equal(h.api.hasTabKey(),false);
  assert.equal(harness().api.hasTabKey(),false);
});
test('forget cancels a pending validation and rejects its late successful reply',async()=>{
  let finish;const wait=new Promise(resolve=>{finish=resolve;});const h=harness({verify:()=>wait});
  const pending=h.api.connect(KEY);for(let i=0;i<8;i++)await Promise.resolve();h.api.forget();
  finish(result({verified:true}));await assert.rejects(pending,/cancelled/);assert.equal(h.api.hasTabKey(),false);
  assert.equal(h.calls.at(-1).options.signal.aborted,true);
});
test('malformed keys and arbitrary request destinations never reach fetch',async()=>{
  const h=harness();await assert.rejects(h.api.connect("OPENAI_API_KEY = 'value'"),/key only/);
  await assert.rejects(h.api.request('https://elsewhere.example',{method:'POST'}),/Unsupported/);
  await assert.rejects(h.api.request('/api/coach?key=anything',{method:'POST'}),/Unsupported/);
  assert.equal(h.calls.length,0);
});
test('a rejected replacement keeps the existing key and never echoes upstream response text',async()=>{
  let ok=true;const h=harness({verify:()=>ok?result({verified:true}):{ok:false,status:401,json:async()=>({error:KEY})}});
  await h.api.connect(KEY);ok=false;await assert.rejects(h.api.connect('sk-another-fake-credential-for-tests'),e=>!e.message.includes(KEY)&&/rejected/.test(e.message));
  await h.api.request('/api/coach',{method:'POST'});assert.equal(h.calls.at(-1).options.headers['X-Astra-OpenAI-Key'],KEY);
});
