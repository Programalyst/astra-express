const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {randomUUID} = require('node:crypto');
const project = process.env.ASTRA_TEST_PROJECT || path.resolve(__dirname, '..');
const KEY = 'sk-fake-key-for-transport-testing-only';
const model = 'gpt-5.4-mini';
const origin = 'https://api.openai.com/v1';
const image = 'data:image/jpeg;base64,' + Buffer.concat([Buffer.from([255,216,255]), Buffer.alloc(120)]).toString('base64');
const result = data => ({ok:true, status:200, json:async () => data});
const advice = () => ({actionId:'solar',title:'Connect solar',body:'Power needs a conduit.',observation:'An array stands by the colony.'});
const envelope = value => ({status:'completed',output:[{type:'message',role:'assistant',content:[{type:'output_text',text:JSON.stringify(value)}]}]});
const action = (type='wait', extra={}) => ({id:'step-1',type,x:null,y:null,targetX:null,targetY:null,trainIndex:null,seconds:type === 'wait' ? 2 : null,reason:'Check the colony.',...extra});
const plan = (actions=[action()]) => ({title:'Colony task',summary:'Review the next steps.',status:actions.length ? 'ready' : 'complete',actions,nextCheck:'Check the latest state.'});
const frame = extra => ({image,state:{session:'colony',credits:500,deposits:[],buildings:[],trainCount:1,idleTrains:1},candidates:[{id:'solar',title:'Solar',body:'Connect power',steps:['Choose conduit.']}],events:[],question:'',goal:'Help the colony',selectedTile:null,previousPlan:null,...extra});
const deferred = () => { let resolve; const promise = new Promise(done => { resolve = done; }); return {promise,resolve}; };
function harness({verify, infer}={}) {
  const calls=[], events=[], listeners={}, timers=new Map(); let timerId=0;
  const window={addEventListener:(name, handler) => { listeners[name]=handler; }};
  const context=vm.createContext({window,crypto:{randomUUID},document:{dispatchEvent:event => events.push(event.type)},CustomEvent:class {constructor(type){this.type=type;}},AbortController,
    setTimeout:(handler,ms) => { const id=++timerId; timers.set(id,{handler,ms}); return id; },clearTimeout:id => timers.delete(id),
    localStorage:{setItem(){assert.fail('Keys must not be stored');}},sessionStorage:{setItem(){assert.fail('Keys must not be stored');}},
    fetch:async(url,options) => { calls.push({url,options}); return url === origin + '/models/' + model ? (verify ? verify() : result({id:model})) : (infer ? infer() : result(envelope(advice()))); }});
  for (const file of ['astrabot-contract.js','astrabot-planning.js','astrabot-api.js']) vm.runInContext(fs.readFileSync(path.join(project,'Assets/WebGLTemplates/Astra',file),'utf8'),context,{filename:file});
  return {api:window.astraBotAPI,planner:window.astraBotPlanning,calls,events,listeners,
    request:(data=frame(),planning=false,options={}) => window.astraBotAPI.request(planning ? '/api/astrabot/plan' : '/api/coach',{method:'POST',body:JSON.stringify(data),...options}),
    fire:ms => { const timer=[...timers.values()].find(value => value.ms === ms); assert.ok(timer); timer.handler(); }};
}
test('configuration and missing keys need no backend or network',async () => {
  const client=harness(); assert.equal((await client.api.config()).configured,false);
  assert.equal((await client.request()).status,401); assert.equal(client.calls.length,0);
});
test('tab key goes only to OpenAI Authorization headers and Responses uses strict, stateless image input',async () => {
  const client=harness(); await client.api.connect(KEY);
  assert.equal((await client.api.config()).engine,'responses-api');
  const response=await client.request(); assert.deepEqual(JSON.parse(JSON.stringify(await response.json())),advice());
  for(const call of client.calls) {
    assert.ok(call.url.startsWith(origin + '/')); assert.equal(call.options.headers.Authorization,'Bearer ' + KEY);
    assert.equal(call.options.redirect,'error'); assert.equal(call.options.credentials,'omit'); assert.equal(call.options.referrerPolicy,'no-referrer');
    assert.ok(!call.url.includes(KEY)); assert.ok(!(call.options.body || '').includes(KEY));
    assert.equal(call.options.headers['X-Astra-OpenAI-Key'],undefined);
  }
  const body=JSON.parse(client.calls.at(-1).options.body);
  assert.equal(body.model,model); assert.equal(body.store,false); assert.equal(body.stream,false); assert.deepEqual(body.tools,[]);
  assert.equal(body.text.format.strict,true); assert.equal(body.text.format.type,'json_schema');
  assert.equal(body.input[0].content[1].image_url,image);
  assert.ok(!JSON.stringify(await client.api.config()).includes(KEY));
});
test('forget and pagehide erase credentials without a server fallback',async () => {
  const client=harness(); await client.api.connect(KEY); client.api.forget();
  assert.equal(client.api.hasTabKey(),false); assert.equal((await client.api.config()).configured,false);
  assert.equal((await client.request()).status,401);
  await client.api.connect(KEY); client.listeners.pagehide(); assert.equal(client.api.hasTabKey(),false);
  assert.equal(harness().api.hasTabKey(),false);
});
test('forget rejects late key validation even if fetch ignores abort',async () => {
  const wait=deferred(), client=harness({verify:() => wait.promise});
  const pending=client.api.connect(KEY); client.api.forget(); wait.resolve(result({id:model}));
  await assert.rejects(pending,/cancelled/); assert.equal(client.api.hasTabKey(),false); assert.equal(client.calls[0].options.signal.aborted,true);
});
test('a rejected replacement keeps the previous key and never echoes upstream content',async () => {
  let accepted=true; const client=harness({verify:() => accepted ? result({id:model}) : {ok:false,status:401,json:async () => ({error:KEY})}});
  await client.api.connect(KEY); accepted=false;
  await assert.rejects(client.api.connect('sk-another-fake-credential-for-tests'),error => /rejected/.test(error.message) && !error.message.includes(KEY));
  await client.request(); assert.equal(client.calls.at(-1).options.headers.Authorization,'Bearer ' + KEY);
});
test('malformed keys, unknown paths, methods, and invalid game frames are rejected before sending',async () => {
  const client=harness(); await assert.rejects(client.api.connect("OPENAI_API_KEY='value'"),/key only/);
  await assert.rejects(client.api.request('https://elsewhere.example',{method:'POST'}),/Unsupported/);
  await assert.rejects(client.api.request('/api/coach?key=anything',{method:'POST'}),/Unsupported/);
  await assert.rejects(client.api.request('/api/coach',{method:'GET'}),/Unsupported/);
  assert.equal(client.calls.length,0); await client.api.connect(KEY);
  await assert.rejects(client.request(frame({image:'https://example.com/screenshot'})),/JPEG/);
  await assert.rejects(client.request(frame({goal:'x'.repeat(601)}),true),/600/);
  assert.equal(client.calls.length,1);
});
for (const reason of ['forget','caller','timeout','replace']) test(`${reason} aborts inference and discards a late response`,async () => {
  const wait=deferred(), client=harness({infer:() => wait.promise}); await client.api.connect(KEY);
  const controller=new AbortController(), pending=client.request(frame(),false,{signal:controller.signal});
  if(reason === 'forget') client.api.forget();
  if(reason === 'caller') controller.abort();
  if(reason === 'timeout') client.fire(22000);
  if(reason === 'replace') await client.api.connect('sk-replacement-fake-credential-for-tests');
  wait.resolve(result(envelope(advice()))); await assert.rejects(pending,/cancelled/);
  assert.equal(client.calls.find(call => call.url.endsWith('/responses')).options.signal.aborted,true);
});
test('simultaneous requests use one inference slot without duplicate billing',async () => {
  const wait=deferred(), client=harness({infer:() => wait.promise}); await client.api.connect(KEY);
  const first=client.request(); const second=await client.request(frame(),true);
  assert.equal(second.status,429); assert.equal(client.calls.filter(call => call.url.endsWith('/responses')).length,1);
  wait.resolve(result(envelope(advice()))); await first;
});
test('tab rate limit caps inference requests without sending a 121st request',async () => {
  const client=harness(); await client.api.connect(KEY);
  for (let requestIndex=0; requestIndex<120; requestIndex++) assert.equal((await client.request()).status,200);
  const limited=await client.request(); assert.equal(limited.status,429);
  assert.match((await limited.json()).error,/120 requests/);
  assert.equal(client.calls.filter(call => call.url.endsWith('/responses')).length,120);
});
for (const status of [400,401,403,404,429,500]) test(`HTTP ${status} produces a safe error without exposing the upstream body`,async () => {
  const client=harness({infer:() => ({ok:false,status,json:async () => { assert.fail('Do not read upstream errors'); }})}); await client.api.connect(KEY);
  const response=await client.request(); assert.equal(response.status,status); assert.ok(!(await response.json()).error.includes(KEY));
});
for (const [name, value] of [
  ['incomplete',{...envelope(advice()),status:'incomplete'}],
  ['refusal',{status:'completed',output:[{type:'message',role:'assistant',content:[{type:'refusal',refusal:KEY}]}]}],
  ['malformed JSON',{status:'completed',output:[{type:'message',role:'assistant',content:[{type:'output_text',text:'not-json-' + KEY}]}]}],
  ['invented candidate',envelope({...advice(),actionId:'hidden-deposit'})],
  ['extra executable fields',envelope({...advice(),script:'steal-key'})]
]) test(`${name} cannot become usable advice`,async () => {
  const client=harness({infer:() => result(value)}); await client.api.connect(KEY);
  await assert.rejects(client.request(),error => !error.message.includes(KEY));
});
test('network failures have a sanitized browser/CORS explanation',async () => {
  const client=harness({verify:() => { throw new Error(KEY); }});
  await assert.rejects(client.api.connect(KEY),error => /browser/.test(error.message) && !error.message.includes(KEY));
});
test('plans are validated and returned for review, with a locally generated identifier',async () => {
  const client=harness({infer:() => result(envelope(plan()))}); await client.api.connect(KEY);
  const response=await client.request(frame(),true), value=await response.json();
  assert.equal(value.status,'ready'); assert.match(value.planId,/^[a-f0-9-]{36}$/);
  assert.equal(value.actions[0].type,'wait');
  const input=JSON.parse(client.calls.at(-1).options.body);
  assert.equal(input.text.format.name,'astra_plan'); assert.equal(input.max_output_tokens,2400);
});
test('progress distinguishes already known ore from discovery and never trusts supplied serverProgress',() => {
  const client=harness(), ore={origin:{x:2,y:3},resource:'Ore'};
  const initial=frame(); initial.state.deposits=[ore];
  assert.equal(client.planner.prepare(initial).serverProgress.newVisibleOreOrigins.length,0);
  client.planner.remember(initial,{planId:'first',...plan()});
  const next=frame({serverProgress:{newVisibleOreOrigins:[{x:27,y:21}]}});
  next.state.deposits=[ore,{origin:{x:4,y:5},resource:'Ore'},{origin:{x:6,y:7},resource:'Fluxite'}];
  const progress=client.planner.prepare(next).serverProgress;
  assert.equal(JSON.stringify(progress.newVisibleOreOrigins),'[{"x":4,"y":5}]'); assert.equal(progress.proposedBatches.length,1);
  client.planner.clear(); assert.equal(client.planner.prepare(next).serverProgress.newVisibleOreOrigins.length,0);
});
const validate = (actions, data=frame()) => harness().planner.parseResult(plan(actions),data,true);
test('plan schema rejects unknown commands, duplicate IDs, coordinates, duration, and extra fields',() => {
  for (const actions of [[action('shell')],[action(),action()],[action('explore',{x:28,y:0})],[action('select',{x:2,y:3,seconds:4})],[action('wait',{seconds:21})],[action('stop',{url:'https://example.com'})],[action('explore',{x:2,y:3}),action('stop',{id:'last'})]]) assert.throws(() => validate(actions));
});
test('hidden ore, overspending, fleet limits, and unconnected dispatch are rejected',() => {
  assert.throws(() => validate([action('build_extractor',{x:2,y:3})]),/revealed/);
  const data=frame(); data.state.credits=149;
  assert.throws(() => validate([action('buy_train')],data),/credits/);
  data.state.trainCount=4; assert.throws(() => validate([action('buy_train')],data),/Fleet/);
  data.state.buildings=[{kind:'Extractor',origin:{x:2,y:3},port:{x:2,y:2},connected:false,railConnected:true}];
  assert.throws(() => validate([action('dispatch_train',{x:2,y:3})],data),/Connect mine/);
});
test('known ore route can be built and dispatched in one bounded plan without mutating state',() => {
  const data=frame(); data.state.deposits=[{origin:{x:2,y:3},size:1,cost:100,buildable:true}];
  const actions=['build_extractor','connect_conduit','connect_rail','dispatch_train'].map((type,index) => action(type,{id:'step-' + index,x:2,y:3}));
  assert.equal(validate(actions,data).actions.length,4); assert.equal(data.state.credits,500); assert.equal(data.state.buildings.length,0);
});
test('solar wiring consumes credits once and construction cannot overlap deposits',() => {
  const data=frame(); data.state.credits=105; data.state.solarSite={x:4,y:4}; data.state.solarSitePowerRoute={possible:true,cost:6};
  const solar=action('build_solar',{x:4,y:4}); assert.throws(() => validate([solar],data),/credits/);
  data.state.credits=106; assert.equal(validate([solar,action('connect_conduit',{id:'wire',x:4,y:4})],data).actions.length,2);
  data.state.deposits=[{origin:{x:5,y:5},size:1}]; assert.throws(() => validate([solar],data),/resource/);
});
test('Fluxite dispatch requires a powered rail-linked plant and paired destination coordinates',() => {
  const data=frame(); data.state.buildings=[{kind:'Extractor',origin:{x:2,y:3},resource:'Fluxite',connected:true,railConnected:true},{kind:'PowerPlant',origin:{x:8,y:8},connected:true,railConnected:true}];
  const dispatch=action('dispatch_train',{x:2,y:3}); assert.throws(() => validate([dispatch],data),/Fluxite/);
  assert.throws(() => validate([{...dispatch,targetX:8}],data),/destination/);
  assert.equal(validate([{...dispatch,targetX:8,targetY:8}],data).actions.length,1);
});
