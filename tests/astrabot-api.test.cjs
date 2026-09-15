const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const {randomUUID} = require('node:crypto');
const project = process.env.ASTRA_TEST_PROJECT || path.resolve(__dirname, '..');
const KEY = 'sk-fake-key-for-transport-testing-only';
const model = 'gpt-6-astra';
const image = 'data:image/jpeg;base64,' + Buffer.concat([Buffer.from([255,216,255]), Buffer.alloc(120)]).toString('base64');
const result = (data, status=200) => ({ok:status >= 200 && status < 300,status,json:async()=>data});
const action = (type='wait', extra={}) => ({id:'step-1',type,x:null,y:null,targetX:null,targetY:null,trainIndex:null,seconds:type === 'wait' ? 2 : null,reason:'Check the colony.',...extra});
const plan = (actions=[action()]) => ({title:'Colony task',summary:'Review the next steps.',status:actions.length ? 'ready' : 'complete',actions,nextCheck:'Check the latest state.'});
const frame = extra => ({image,state:{session:'colony',credits:500,deposits:[],buildings:[],trainCount:1,idleTrains:1},candidates:[{id:'solar',title:'Solar',body:'Connect power',steps:['Choose conduit.']}],events:[],question:'',goal:'Help the colony',selectedTile:null,previousPlan:null,...extra});
function harness({configured=false,verify,respond,engine='agents-api',configuredModel=model}={}) {
  const calls=[],events=[],listeners={},window={addEventListener:(name,handler)=>{listeners[name]=handler;}};
  const context=vm.createContext({window,crypto:{randomUUID},document:{dispatchEvent:event=>events.push(event.type)},CustomEvent:class {constructor(type){this.type=type;}},AbortController,setTimeout,clearTimeout,
    localStorage:{setItem(){assert.fail('Keys must not be stored');}},sessionStorage:{setItem(){assert.fail('Keys must not be stored');}},
    fetch:async(url,options={})=>{calls.push({url,options});
      if(url==='/api/coach/config') return result({engine,token:'csrf',configured,acceptsTabKey:true,plannerAvailable:true,model:configuredModel});
      if(url==='/api/coach/key') return verify ? verify() : result({verified:true});
      return respond ? respond(url,options) : result({});
    }});
  for(const file of ['astrabot-contract.js','astrabot-planning.js','astrabot-api.js']) vm.runInContext(fs.readFileSync(path.join(project,'Assets/WebGLTemplates/Astra',file),'utf8'),context,{filename:file});
  return {api:window.astraBotAPI,planner:window.astraBotPlanning,calls,events,listeners,
    request:(data=frame(),planning=false,options={})=>window.astraBotAPI.request(planning?'/api/astrabot/plan':'/api/coach',{method:'POST',body:JSON.stringify(data),...options})};
}
test('configuration requires the hosted Agents API and Astra model',async()=>{
  const client=harness(); const config=await client.api.config();
  assert.equal(config.engine,'agents-api'); assert.equal(config.model,model); assert.equal(config.configured,false);
  await assert.rejects(harness({engine:'responses-api'}).api.config(),/Agents API server/);
  await assert.rejects(harness({configuredModel:'gpt-5.4-mini'}).api.config(),/gpt-6-astra/);
});
test('tab key travels only in allowed same-origin POST headers, never config or body',async()=>{
  const client=harness(); await client.api.connect(KEY);
  assert.equal(client.api.hasTabKey(),true); assert.equal((await client.api.config()).configured,true);
  await client.request(); await client.request(frame(),true);
  for(const call of client.calls){
    assert.equal(call.options.redirect,'error'); assert.equal(call.options.credentials,'same-origin');
    assert.ok(!call.url.includes(KEY)); assert.ok(!(call.options.body||'').includes(KEY));
    assert.equal(call.options.headers?.['X-Astra-OpenAI-Key'],call.url==='/api/coach/config'?undefined:KEY);
  }
  assert.ok(!JSON.stringify(await client.api.config()).includes(KEY));
});
test('forget and pagehide clear the override while leaving a server key usable',async()=>{
  const client=harness({configured:true}); await client.api.connect(KEY); client.api.forget();
  assert.equal(client.api.hasTabKey(),false); assert.equal((await client.api.config()).configured,true);
  await client.request(); assert.equal(client.calls.at(-1).options.headers['X-Astra-OpenAI-Key'],undefined);
  await client.api.connect(KEY); client.listeners.pagehide(); assert.equal(client.api.hasTabKey(),false);
  assert.equal(harness().api.hasTabKey(),false);
});
test('forget cancels pending validation and rejects a late success',async()=>{
  let finish; const wait=new Promise(resolve=>{finish=resolve;}); const client=harness({verify:()=>wait});
  const pending=client.api.connect(KEY); for(let i=0;i<8;i++) await Promise.resolve(); client.api.forget();
  finish(result({verified:true})); await assert.rejects(pending,/cancelled/); assert.equal(client.api.hasTabKey(),false);
  assert.equal(client.calls.at(-1).options.signal.aborted,true);
});
test('malformed keys and arbitrary request destinations never reach fetch',async()=>{
  const client=harness(); await assert.rejects(client.api.connect("OPENAI_API_KEY='value'"),/key only/);
  await assert.rejects(client.api.request('https://elsewhere.example',{method:'POST'}),/Unsupported/);
  await assert.rejects(client.api.request('/api/coach?key=anything',{method:'POST'}),/Unsupported/);
  await assert.rejects(client.api.request('/api/coach',{method:'GET'}),/Unsupported/);
  assert.equal(client.calls.length,0);
});
test('a rejected replacement keeps the existing key and never echoes upstream text',async()=>{
  let accepted=true; const client=harness({verify:()=>accepted?result({verified:true}):result({error:KEY},401)});
  await client.api.connect(KEY); accepted=false;
  await assert.rejects(client.api.connect('sk-another-fake-credential-for-tests'),error=>/rejected/.test(error.message)&&!error.message.includes(KEY));
  await client.request(); assert.equal(client.calls.at(-1).options.headers['X-Astra-OpenAI-Key'],KEY);
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
  for (const actions of [[action('shell')],[action(),action()],[action('explore',{x:32,y:0})],[action('explore',{x:0,y:32})],[action('select',{x:2,y:3,seconds:4})],[action('wait',{seconds:21})],[action('stop',{url:'https://example.com'})],[action('explore',{x:2,y:3}),action('stop',{id:'last'})]]) assert.throws(() => validate(actions));
});
test('expanded grid corner is accepted by the browser planner',() => {
  const checked=validate([action('explore',{x:31,y:31})]); assert.equal(checked.actions[0].x,31); assert.equal(checked.actions[0].y,31);
});
test('hidden ore, removed train purchases, and unconnected dispatch are rejected',() => {
  assert.throws(() => validate([action('build_extractor',{x:2,y:3})]),/revealed/);
  const data=frame(); data.state.credits=149; assert.throws(() => validate([action('buy_train')],data),/invalid/);
  data.state.trainCount=4; assert.throws(() => validate([action('buy_train')],data),/invalid/);
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

test('owned train dispatch cannot take another mine’s idle locomotive, and fifth train selection is valid',() => {
  const data=frame();
  data.state={...data.state,extractorOwnedTrains:true,trainCount:5,idleTrains:5,
    buildings:[{kind:'Extractor',origin:{x:2,y:3},connected:true,railConnected:true}],
    trains:[{owner:{x:8,y:8},phase:'Parked'}]};
  assert.throws(()=>validate([action('dispatch_train',{x:2,y:3})],data),/own parked train/);
  data.state.trains.push({owner:{x:2,y:3},phase:'Parked'});
  assert.equal(validate([action('dispatch_train',{x:2,y:3})],data).actions.length,1);
  assert.equal(validate([action('select',{trainIndex:4})],data).actions.length,1);
  assert.throws(()=>validate([action('select',{trainIndex:5})],data));
});
