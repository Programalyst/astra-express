const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const project = process.env.ASTRA_TEST_PROJECT || path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(project, 'Assets/WebGLTemplates/Astra/astrabot-control.js'), 'utf8');
const deferred = () => { let resolve, reject; const promise = new Promise((a,b) => { resolve=a; reject=b; }); return {promise,resolve,reject}; };
const response = data => ({ok:true,json:async()=>data});
const readyPlan = (actions = [{type:'select',x:11,y:7,reason:'Select mine'}]) => ({planId:'plan-1',status:'ready',title:'A colony task',summary:'Review before starting.',actions});
const completePlan = () => ({planId:'complete',status:'complete',title:'Done',summary:'Goal reached.',actions:[]});
const flush = async () => { for (let i=0;i<18;i++) await Promise.resolve(); };

function harness({autoCapture=true}={}) {
  const elements = new Map(), listeners = new Map(), timers = new Map(), calls = [], plans = [], configs = [];
  let now=1000, timerSequence=0;
  const document = {hidden:false,activeElement:null};
  class Element {
    constructor(tag) { this.tagName=tag; this.hidden=false; this.disabled=false; this.value=''; this.textContent=''; this.dataset={}; this.children=[]; this.listeners=new Map(); this.classes=new Set();
      this.classList={add:n=>this.classes.add(n),remove:n=>this.classes.delete(n),contains:n=>this.classes.has(n),toggle:(n,on)=>{ if(on ?? !this.classes.has(n))this.classes.add(n);else this.classes.delete(n); }};
    }
    set id(value) { this._id=value; elements.set(value,this); }
    get id() { return this._id; }
    set innerHTML(html) { for (const match of html.matchAll(/<([a-z][a-z0-9]*)\b([^>]*\bid="([^"]+)"[^>]*)>/g)) { const e=new Element(match[1]);e.id=match[3];e.hidden=/\bhidden\b/.test(match[2]);e.parent=this;this.children.push(e); } }
    setAttribute(name,value) { this[name]=value; }
    addEventListener(name,handler) { if(!this.listeners.has(name))this.listeners.set(name,[]);this.listeners.get(name).push(handler); }
    append(child) { child.parent=this;this.children.push(child); }
    replaceChildren(...children) { this.children=children;for(const child of children)child.parent=this; }
    querySelectorAll() { return []; }
    matches() { return false; }
    contains(child) { for(let e=child;e;e=e.parent)if(e===this)return true;return false; }
    focus() { document.activeElement=this; }
  }
  document.createElement=tag=>new Element(tag);
  document.getElementById=id=>elements.get(id);
  document.body=new Element('body');
  document.addEventListener=(name,handler)=>{if(!listeners.has(name))listeners.set(name,[]);listeners.get(name).push(handler);};
  const canvas=new Element('canvas');canvas.id='unity-canvas';
  const window={AstraCoachPolicy:require(path.join(project,'Assets/WebGLTemplates/Astra/coach-policy.js'))};
  const game={SendMessage(object,method,value) { calls.push({kind:'game',method,value}); if(method==='CoachCapture'&&autoCapture)queueMicrotask(()=>window.astraBotControl.screenReady(value,'jpeg')); }};
  const fetch=async(url,options={})=>{
    calls.push({kind:'fetch',url,options});
    if(url==='/api/coach/config') {
      const next=configs.length?configs.shift():{configured:true,token:'test-token'};
      return next?.promise ? next.promise : next?.json ? next : response(next);
    }
    const next=plans.length?plans.shift():completePlan();
    return next?.promise ? next.promise : next?.json ? next : response(next);
  };
  vm.runInNewContext(source,{document,window,fetch,AbortController,TextDecoder,queueMicrotask,console,
    Date:{now:()=>now},setTimeout:(fn,ms)=>{const id=++timerSequence;timers.set(id,{fn,ms});return id;},clearTimeout:id=>timers.delete(id),
    sessionStorage:{setItem(){}},
  },{filename:'astrabot-control.js'});
  const api=window.astraBotControl;api.ready(game);
  let state={session:'colony-1',credits:500,deliveries:0,pickedTile:null,pickingTile:false};
  const receive=extra=>{state={...state,...extra};api.receive(state);};receive({});
  const commands=()=>calls.filter(c=>c.kind==='game'&&c.method==='CoachBotCommand').map(c=>JSON.parse(c.value));
  const event=()=>({preventDefault(){},stopImmediatePropagation(){},stopPropagation(){}});
  const submit=(goal='Connect the mine')=>{elements.get('bot-goal').value=goal;return elements.get('bot-goal-form').onsubmit(event());};
  const acknowledge=(status='complete',id=commands().at(-1)?.id)=>receive({botActionId:id,botActionStatus:status,botActionMessage:status});
  return {api,e:id=>elements.get(id),calls,plans,configs,submit,commands,receive,acknowledge,
    start:()=>elements.get('bot-start').onclick(),stop:()=>elements.get('bot-stop').onclick(),
    advance:ms=>{now+=ms;},fireTimer:ms=>{const entry=[...timers].find(([,t])=>t.ms===ms);assert.ok(entry,`timer ${ms} exists`);timers.delete(entry[0]);entry[1].fn();},
    dispatch:(name,extra={})=>{for(const fn of listeners.get(name)||[])fn({...event(),...extra});},document};
}

test('live activity starts expanded and can be minimized and reopened',()=>{
  const h=harness(),activity=h.e('bot-activity');
  assert.equal(activity.open,true);
  activity.open=false;activity.listeners.get('toggle').forEach(fn=>fn());
  assert.equal(h.e('bot-activity-toggle').textContent,'Live activity · expand');
  activity.open=true;activity.listeners.get('toggle').forEach(fn=>fn());
  assert.equal(h.e('bot-activity-toggle').textContent,'Live activity · minimize');
});
test('command bar expands only for a long draft and shrinks after shortening',()=>{
  const h=harness(),input=h.e('bot-command-input');
  const change=()=>input.listeners.get('input').forEach(fn=>fn());
  input.value='Hello';change();assert.equal(h.e('astrabot-commandbar').dataset.long,'false');
  input.value='Please explore east and find more ore, then help me build the mining outpost.';change();assert.equal(h.e('astrabot-commandbar').dataset.long,'true');
  input.value='';change();assert.equal(h.e('astrabot-commandbar').dataset.long,'false');
});
test('passive model coaching stays disabled and retired controls are absent',()=>{
  const coach=fs.readFileSync(path.join(project,'Assets/WebGLTemplates/Astra/coach.js'),'utf8');
  assert.match(coach,/const live = false/);
  assert.doesNotMatch(coach,/id="coach-live"|id="coach-next"|Check my colony/);
  assert.match(coach,/if \(!enabled \|\| !open \|\| !live/);
  assert.doesNotMatch(coach,/setInterval\([^\n]*maybeAsk/);
  assert.equal((coach.match(/root.hidden = !intro \|\| !enabled/g)||[]).length,2);
});
test('Send streams a visible conversation reply, preserves follow-up history and never executes',async()=>{
  const h=harness(), last=deferred();let reads=0;
  h.plans.push({ok:true,json:async()=>({}),body:{getReader:()=>({read:()=> ++reads===1 ? Promise.resolve({value:new TextEncoder().encode('{"type":"delta","text":"Ore you"}\n'),done:false}) : last.promise,cancel:async()=>{}})}});
  h.e('bot-command-input').value='Tell me a joke';const work=h.e('astrabot-commandbar').onsubmit({preventDefault(){}});await flush();
  assert.equal(h.e('astrabot-reply').hidden,false);assert.equal(h.e('bot-reply-text').textContent,'Ore you');
  assert.equal(h.commands().length,0);assert.equal(h.e('bot-reply-stop').hidden,false);
  last.resolve({value:new TextEncoder().encode('{"type":"done","text":"Ore you kidding?","model":"test","intent":"chat","goal":""}\n'),done:true});await work;
  assert.equal(h.e('bot-reply-text').textContent,'Ore you kidding?');assert.equal(h.e('bot-command-input').value,'');
  assert.equal(h.e('bot-reply-state').textContent,'AstraBot · reply complete');
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
});
test('stopping a conversation discards late streamed text without any game commands',async()=>{
  const h=harness(), pending=deferred();
  h.plans.push({ok:true,json:async()=>({}),body:{getReader:()=>({read:()=>pending.promise,cancel:async()=>{}})}});
  h.e('bot-command-input').value='Hello';const work=h.e('astrabot-commandbar').onsubmit({preventDefault(){}});await flush();
  h.e('bot-reply-stop').onclick();pending.resolve({value:new TextEncoder().encode('{"type":"done","text":"Late answer"}\n'),done:true});await work;
  assert.equal(h.e('bot-reply-text').textContent,'');assert.match(h.e('bot-reply-state').textContent,/stopped/);
  assert.equal(h.commands().length,0);assert.equal(h.api.active(),false);
});
test('one Send routes a model-selected task to planning but still requires Start',async()=>{
  const h=harness();h.e('bot-command-input').value='Please connect it';
  h.plans.push({ok:true,json:async()=>({}),body:{getReader:()=>({read:async()=>({value:new TextEncoder().encode('{"type":"done","text":"I will prepare that connection.","intent":"task","goal":"Connect my mine","model":"test"}\n'),done:true})})}},readyPlan());
  await h.e('astrabot-commandbar').onsubmit({preventDefault(){}});
  assert.equal(h.e('bot-goal').value,'Connect my mine');assert.equal(h.commands().length,0);
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/chat').length,1);
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  assert.equal(h.e('bot-command-input').value,'');assert.equal(h.e('bot-command-send').disabled,false);
  const run=h.start();assert.equal(h.e('bot-command-send').disabled,true);
  assert.equal(h.e('bot-command-input').disabled,false);h.stop();await run;
  h.api.setEnabled(false);assert.equal(h.e('bot-command-input').disabled,true);
});
test('incomplete and invalid model routes never fall through to planning',async()=>{
  for (const result of [{type:'delta',text:'I will build rails'}, {type:'done',text:'Build rails',intent:'task',goal:''}, {type:'done',text:'Oops',intent:'execute',goal:'Build rails'}]) {
    const h=harness();h.e('bot-command-input').value='Build rails';
    h.plans.push({ok:true,json:async()=>({}),body:{getReader:()=>({read:async()=>({value:new TextEncoder().encode(JSON.stringify(result)+'\n'),done:true})})}});
    await h.e('astrabot-commandbar').onsubmit({preventDefault(){}});
    assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
    assert.equal(h.commands().length,0);
    assert.equal(h.e('astrabot-reply').hidden,false);
  }
});
test('Send waits for a busy screen reader and cancellation prevents retry',async()=>{
  const h=harness();h.e('bot-command-input').value='Hello';
  h.plans.push({ok:false,status:429,json:async()=>({error:'AstraBot is already reading a screen',retryAfterMs:4000})});
  const work=h.e('astrabot-commandbar').onsubmit({preventDefault(){}});await flush();
  assert.match(h.e('bot-reply-state').textContent,/background check/);
  h.e('bot-reply-stop').onclick();await work;
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/chat').length,1);
  assert.equal(h.api.active(),false);
});
test('just do it carries previous advice into the model and plans its resolved goal',async()=>{
  const h=harness();
  const streamed = decision => ({ok:true,json:async()=>({}),body:{getReader:()=>({read:async()=>({value:new TextEncoder().encode(JSON.stringify({type:'done',model:'test',...decision})+'\n'),done:true})})}});
  const advice='Explore east for Ore, then build and power an extractor. Do not buy a train.';
  h.plans.push(streamed({text:advice,intent:'chat',goal:''}));
  h.e('bot-command-input').value='How do I play? No train purchases.';
  await h.e('astrabot-commandbar').onsubmit({preventDefault(){}});
  const goal='Explore east for Ore, then build and power an extractor. Do not buy a train.';
  h.plans.push(streamed({text:'I will prepare those starter steps.',intent:'task',goal}),readyPlan());
  h.e('bot-command-input').value='just do it';
  await h.e('astrabot-commandbar').onsubmit({preventDefault(){}});
  const request=h.calls.filter(c=>c.url==='/api/astrabot/chat').at(-1);
  const body=JSON.parse(request.options.body);
  assert.deepEqual(body.history,[{role:'user',text:'How do I play? No train purchases.'},{role:'assistant',text:advice}]);
  assert.equal(body.message,'just do it');assert.equal(h.e('bot-goal').value,goal);
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  assert.equal(h.commands().length,0);
});
test('native companion replaces the DOM avatar and follows enable and task state',async()=>{
  const h=harness();h.receive({nativeCompanion:true,frontier:true,battery:90});
  assert.equal(h.e('bot-embodied').hidden,true);
  h.api.suggest('discover-ore');const run=h.start();
  assert.ok(h.calls.some(c=>c.method==='CoachSetCompanionMode'&&c.value==='working'));
  h.acknowledge();await run;
  assert.equal(h.calls.filter(c=>c.method==='CoachSetCompanionMode').at(-1).value,'idle');
  h.api.setEnabled(false);
  assert.equal(h.calls.filter(c=>c.method==='CoachSetCompanionMode').at(-1).value,'off');
});
test('suggested jobs prepare instantly, require Start, and finish without model calls',async()=>{
  const h=harness();h.receive({frontier:true,battery:90});h.api.suggest('discover-ore');
  assert.equal(h.commands().length,0);assert.equal(h.e('bot-start').hidden,false);
  assert.equal(h.e('bot-plan-details').open,false);
  const run=h.start();assert.equal(h.commands()[0].type,'auto_explore');
  h.acknowledge();await run;
  assert.equal(h.api.active(),false);assert.equal(h.calls.filter(c=>c.kind==='fetch').length,0);
  h.fireTimer(2200);assert.equal(h.e('astrabot-task').hidden,true);
});
test('suggested jobs reject stale, disabled, occupied-rover and changed-cost state',()=>{
  const h=harness();h.receive({frontier:true,battery:90});h.advance(4100);h.api.suggest('discover-ore');
  assert.equal(h.e('astrabot-task').hidden,true);
  h.receive({});h.api.suggest('discover-ore');h.receive({roverMoving:true});h.start();assert.equal(h.commands().length,0);
  h.receive({roverMoving:false});h.api.setEnabled(false);h.api.suggest('discover-ore');assert.equal(h.e('astrabot-task').hidden,true);
  const b={kind:'Solar',origin:{x:2,y:3},connected:false,powerRoute:{possible:true,cost:10}};
  const k=harness();k.receive({buildings:[b]});k.api.suggest('power-2-3');k.receive({buildings:[{...b,powerRoute:{possible:true,cost:20}}]});k.start();assert.equal(k.commands().length,0);
});
test('creating a plan captures a frame but never starts gameplay without Start',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();
  assert.equal(h.commands().length,0);assert.equal(h.e('bot-start').hidden,false);h.e('bot-expand').onclick();assert.equal(h.e('bot-start').hidden,false,h.e('bot-status').textContent);assert.equal(h.api.active(),false);
  const request=h.calls.find(c=>c.url==='/api/astrabot/plan');const body=JSON.parse(request.options.body);
  assert.equal(body.image,'data:image/jpeg;base64,jpeg');assert.equal(body.state.session,'colony-1');
});
test('thinking is visible while planning and clears on cancellation and success',async()=>{
  const h=harness(), pending=deferred();h.plans.push(pending);
  const work=h.submit('Find ore');await flush();
  assert.equal(h.e('bot-thinking').hidden,false);assert.equal(h.e('astrabot-task').dataset.thinking,'true');
  assert.ok(h.calls.some(c=>c.method==='CoachSetCompanionMode'&&c.value==='launching'));
  assert.equal(h.commands().length,0);
  h.stop();assert.equal(h.e('bot-thinking').hidden,true);
  pending.resolve(response(readyPlan()));await work;
  h.plans.push(readyPlan());await h.submit('Find ore');assert.equal(h.e('bot-thinking').hidden,true);
  assert.equal(h.calls.filter(c=>c.method==='CoachSetCompanionMode').at(-1).value,'ready');
});
test('a whole-colony offer clears an older selected tile',()=>{
  const h=harness();h.receive({hasPickedTile:true,pickedTile:{x:11,y:7}});
  h.api.open('Find more ore');
  assert.equal(h.e('bot-tile').textContent,'Whole colony');
  assert.ok(h.calls.some(c=>c.method==='CoachPickTile'&&c.value==='clear'));
  assert.doesNotMatch(h.e('bot-status').textContent,/repair|target is selected/);
});
test('manual rover cancellation stops the task without automatic replanning',async()=>{
  const h=harness();h.plans.push(readyPlan([{type:'auto_explore',reason:'Find ore'}]));await h.submit();
  const run=h.start();await flush();h.acknowledge('cancelled');await run;
  assert.equal(h.api.active(),false);assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  assert.equal(h.e('bot-embodied').hidden,true);
  assert.ok(h.e('bot-timeline').children.some(li=>li.dataset.kind==='cancelled'));
});

test('plan review shows which model AstraBot routed the task to',async()=>{
  const h=harness();
  h.plans.push({...readyPlan(),model:'gpt-5.4-mini',modelRoute:'rover-exploration'}); await h.submit('Discover ore with the rover'); h.e('bot-expand').onclick();
  assert.equal(h.e('bot-model').hidden,false); assert.equal(h.e('bot-model').textContent,'ASTRABOT · ROVER EXPLORATION · AGENTS API');
  h.plans.push({...readyPlan(),model:'gpt-6-astra',modelRoute:'advanced-visual'}); await h.submit('Connect the mine conduit'); h.e('bot-expand').onclick();
  assert.equal(h.e('bot-model').textContent,'ROUTED · GPT-6 ASTRA · VISUAL BUILD PLANNING');
});

test('configuration and local capture overlap while upload waits for configuration',async()=>{
  const h=harness(),config=deferred();h.configs.push(config);h.plans.push(readyPlan());
  const request=h.submit();await flush();
  assert.equal(h.calls.filter(c=>c.method==='CoachCapture').length,1);
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
  config.resolve(response({configured:true,token:'test-token'}));await request;
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  assert.equal(h.commands().length,0);
});

test('unconfigured server cancels concurrent capture without uploading its late frame',async()=>{
  const h=harness({autoCapture:false});h.configs.push({configured:false,token:'test-token'});
  const request=h.submit();await request;
  const frame=h.calls.find(c=>c.method==='CoachCapture');h.api.screenReady(frame.value,'late');await flush();
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
  assert.match(h.e('bot-status').textContent,/Add an OpenAI key/);
});

test('capture failure aborts a concurrent configuration lookup',async()=>{
  const h=harness({autoCapture:false}),config=deferred();h.configs.push(config);
  const request=h.submit();await flush();h.fireTimer(8000);await request;
  assert.equal(h.calls.find(c=>c.url==='/api/coach/config').options.signal.aborted,true);
  config.resolve(response({configured:true,token:'test-token'}));await flush();
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
});

test('powered mine expansion preset fills a bounded goal and shows verified progress before Start',async()=>{
  const h=harness();h.api.open();h.e('bot-expand-mines').onclick();
  const goal=h.e('bot-goal').value;
  assert.match(goal,/two additional Ore extractors/);assert.match(goal,/shared power grid/);assert.match(goal,/Do not add rails or trains/);
  assert.equal(h.calls.filter(c=>c.kind==='fetch').length,0);assert.equal(h.commands().length,0);
  h.receive({buildings:[],solarGeneration:2});
  h.plans.push({...readyPlan([{type:'auto_explore',x:null,y:null,reason:'Find another revealed Ore deposit.'}]),goalProgress:{
    resource:'Ore',mode:'additional',initialExtractorOrigins:[],requestedAdditionalExtractors:2,targetExtractorCount:2,
    currentExtractorCount:0,newExtractorCount:0,connectedTargetCount:0,ratedExtractorDemand:0,solarGeneration:2,requiresSolarCapacity:true
  }});
  await h.submit(goal);
  assert.equal(h.e('bot-progress').textContent,'Mines 0/2 · Linked 0/2 · Power 2/0');
  assert.equal(h.commands().length,0);h.e('bot-expand').onclick();
  assert.equal(h.e('bot-start').textContent,'Start expansion');assert.equal(h.e('bot-start').hidden,false);
  h.receive({buildings:[{kind:'Extractor',resource:'Ore',origin:{x:11,y:7},size:1,demand:1,connected:true}],solarGeneration:2});
  assert.equal(h.e('bot-progress').textContent,'Mines 1/2 · Linked 1/2 · Power 2/1');
});

test('a transport expansion is not marked done before train service exists',async()=>{
  const h=harness();const mine={kind:'Extractor',resource:'Ore',origin:{x:11,y:7},size:1,demand:1,connected:true,served:false};
  h.receive({buildings:[mine],solarGeneration:2});
  h.plans.push({...readyPlan(),goalProgress:{resource:'Ore',mode:'additional',initialExtractorOrigins:[],requestedAdditionalExtractors:1,targetExtractorCount:1,requiresSolarCapacity:true,requiresRailService:true}});
  await h.submit('Build one additional Ore extractor with power and train service');
  assert.equal(h.e('bot-progress').dataset.state,'active');
  h.receive({buildings:[{...mine,served:true}]});
  assert.equal(h.e('bot-progress').dataset.state,'done');
});

test('one Start builds and powers two mines through fresh-state continuation to completion',async()=>{
  const h=harness();
  const first={kind:'Extractor',resource:'Ore',origin:{x:11,y:7},size:1,demand:1,connected:false};
  const second={kind:'Extractor',resource:'Ore',origin:{x:15,y:11},size:2,demand:2,connected:false};
  const solar={kind:'Solar',origin:{x:8,y:9},connected:true,generation:2};
  const snapshots=[
    {buildings:[],solarGeneration:2,credits:500},
    {buildings:[first],solarGeneration:2,credits:400},
    {buildings:[{...first,connected:true}],solarGeneration:2,credits:390},
    {buildings:[{...first,connected:true},solar],solarGeneration:4,credits:285},
    {buildings:[{...first,connected:true},solar,second],solarGeneration:4,credits:105},
    {buildings:[{...first,connected:true},solar,{...second,connected:true}],solarGeneration:4,credits:85}
  ];
  const actions=[
    {type:'build_extractor',x:11,y:7},
    {type:'connect_conduit',x:11,y:7},
    {type:'build_solar',x:8,y:9},
    {type:'build_extractor',x:15,y:11},
    {type:'connect_conduit',x:15,y:11}
  ];
  const goalProgress={resource:'Ore',mode:'additional',initialExtractorOrigins:[],requestedAdditionalExtractors:2,targetExtractorCount:2,requiresSolarCapacity:true,requiresRailService:false};
  h.receive(snapshots[0]);
  h.plans.push(...actions.map((action,index)=>({...readyPlan([action]),planId:`expansion-${index}`,goalProgress})),{...completePlan(),goalProgress});
  await h.submit('Build two additional Ore extractors and connect them to solar power. Do not add rails or trains.');
  assert.equal(h.commands().length,0);
  h.e('bot-expand').onclick();
  const run=h.start();
  try {
    for(let index=0;index<actions.length;index++) {
      await flush();
      const commands=h.commands();
      assert.equal(commands.length,index+1);
      assert.equal(commands[index].type,actions[index].type);
      assert.equal(h.e('bot-stop').hidden,false);
      h.advance(1000);
      h.receive({...snapshots[index+1],botActionId:commands[index].id,botActionStatus:'complete',botActionMessage:'Completed'});
    }
    await run;
    const requests=h.calls.filter(c=>c.url==='/api/astrabot/plan').map(c=>JSON.parse(c.options.body));
    assert.equal(requests.length,6);
    assert.equal(h.calls.filter(c=>c.method==='CoachCapture').length,6);
    for(let index=0;index<requests.length;index++) {
      assert.deepEqual(requests[index].state.buildings,snapshots[index].buildings);
      assert.equal(requests[index].state.credits,snapshots[index].credits);
      assert.equal(requests[index].state.solarGeneration,snapshots[index].solarGeneration);
      assert.equal(requests[index].previousPlan?.results.length||0,index);
    }
    assert.equal(h.api.diagnostics().completed,5);
    assert.equal(h.api.diagnostics().batches,6);
    assert.equal(h.api.active(),false);
    assert.equal(h.e('bot-progress').textContent,'Mines 2/2 · Linked 2/2 · Power 4/3');
    assert.equal(h.e('bot-progress').dataset.state,'done');
    assert.equal(h.e('bot-status').textContent,'Goal complete.');
    assert.equal(h.calls.filter(c=>c.method==='CoachBotStop').length,0);
  } finally { if(h.api.active()) {h.stop();await run;} }
});

test('runner waits for its exact action acknowledgment and ignores duplicates',async()=>{
  const h=harness();h.plans.push(readyPlan([{type:'select',x:11,y:7},{type:'connect_rail',x:11,y:7}]));await h.submit();const run=h.start();await flush();
  assert.equal(h.commands().length,1);const first=h.commands()[0];h.acknowledge('complete','unrelated');await flush();assert.equal(h.commands().length,1);
  h.acknowledge('complete',first.id);await flush();assert.equal(h.commands().length,2);
  h.acknowledge('complete',first.id);await flush();assert.equal(h.commands().length,2);h.stop();await run;
});

test('Stop cancels pending screenshot and discards the late frame',async()=>{
  const h=harness({autoCapture:false});const request=h.submit();await flush();const frame=h.calls.find(c=>c.method==='CoachCapture');assert.ok(frame);
  h.stop();h.api.screenReady(frame.value,'late');await request;await flush();
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);assert.equal(h.api.active(),false);
});

test('Stop aborts a plan request and a late response cannot restore Start',async()=>{
  const h=harness(),late=deferred();h.plans.push(late);const request=h.submit();await flush();
  const call=h.calls.find(c=>c.url==='/api/astrabot/plan');h.stop();assert.equal(call.options.signal.aborted,true);
  late.resolve(response(readyPlan()));await request;assert.equal(h.e('bot-start').hidden,true);assert.equal(h.commands().length,0);
});

test('new colony cancels the running action and rejects its old acknowledgment',async()=>{
  const h=harness();h.plans.push(readyPlan([{type:'select',x:11,y:7},{type:'build_extractor',x:11,y:7}]));await h.submit();const run=h.start();await flush();const old=h.commands()[0];
  h.receive({session:'colony-2',botActionId:old.id,botActionStatus:'complete'});await run;
  assert.equal(h.commands().length,1);assert.equal(h.api.active(),false);assert.equal(h.e('bot-start').hidden,true);
});

test('action timeout returns control and never starts the next action',async()=>{
  const h=harness();h.plans.push(readyPlan([{type:'select',x:11,y:7},{type:'build_extractor',x:11,y:7}]));await h.submit();const run=h.start();await flush();h.fireTimer(75000);await run;
  assert.equal(h.commands().length,1);assert.equal(h.api.active(),false);assert.match(h.e('bot-status').textContent,/timed out/i);
  assert.ok(h.calls.some(c=>c.method==='CoachBotStop'&&c.value==='timeout'));
});

test('hiding the game tab stops takeover and ignores subsequent completion',async()=>{
  const h=harness();h.plans.push(readyPlan([{type:'select',x:11,y:7},{type:'build_extractor',x:11,y:7}]));await h.submit();const run=h.start();await flush();h.document.hidden=true;h.dispatch('visibilitychange');h.acknowledge();await run;
  assert.equal(h.commands().length,1);assert.equal(h.api.active(),false);assert.match(h.e('bot-status').textContent,/tab was hidden/i);
});

test('a stale game snapshot prevents Start from issuing a command',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();h.advance(5000);const run=h.start();await flush();
  try {assert.equal(h.commands().length,0);assert.equal(h.api.active(),false);} finally {h.stop();await run;}
});

test('snapshot becoming stale during capture prevents a paid planning request',async()=>{
  const h=harness({autoCapture:false});const request=h.submit();await flush();const frame=h.calls.find(c=>c.method==='CoachCapture');
  h.advance(5000);h.api.screenReady(frame.value,'jpeg');await request;await flush();
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);assert.equal(h.api.active(),false);
});

test('late cleanup from a cancelled request cannot unlock a newer request',async()=>{
  const h=harness(),old=deferred(),current=deferred();h.plans.push(old,current);const first=h.submit();await flush();h.stop();const second=h.submit();await flush();
  old.resolve(response(readyPlan()));await first;await flush();
  try {assert.equal(h.api.diagnostics().planning,true);assert.equal(h.e('bot-plan').disabled,true);} finally {h.stop();current.resolve(response(readyPlan()));await second;}
});

test('three blocked batches stop even when each starts with a successful selection',async()=>{
  const h=harness();const batch=()=>readyPlan([{type:'select',x:11,y:7},{type:'build_extractor',x:11,y:7}]);h.plans.push(batch(),batch(),batch(),batch());await h.submit();const run=h.start();
  try {
    for(let batchIndex=0;batchIndex<3;batchIndex++){await flush();assert.equal(h.commands().at(-1).type,'select');h.acknowledge();await flush();assert.equal(h.commands().at(-1).type,'build_extractor');h.acknowledge('failed');}
    await flush();assert.equal(h.api.active(),false);assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,3);
  } finally {h.stop();await run;}
});


test('switching AstraBot off hides its planner without cancelling manual game controls',async()=>{
  const h=harness();h.api.open();assert.equal(h.e('astrabot-task').hidden,false);
  h.api.setEnabled(false);h.api.open();assert.equal(h.e('astrabot-task').hidden,true);
  assert.equal(h.calls.filter(c=>c.method==='CoachBotStop').length,0);
  await h.submit();assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
  h.api.setEnabled(true);h.api.open();assert.equal(h.e('astrabot-task').hidden,false);
});

test('a proactive coaching suggestion prefills a reviewable task without planning or starting it',()=>{
  const h=harness(); const suggestion='Send the rover to uncover fog and discover Ore.';
  h.api.open(suggestion);
  assert.equal(h.e('astrabot-task').hidden,false);
  assert.equal(h.e('bot-goal').value,suggestion);
  assert.equal(h.e('bot-editor').hidden,false);
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
  assert.equal(h.commands().length,0);
  assert.match(h.e('bot-status').textContent,/review/i);
});

test('a validated visual repair selects the exact extractor but still waits for plan and Start',async()=>{
  const h=harness(),goal='Select the extractor at (18, 9) and connect its south port to the colony shared power grid.';
  h.api.open(goal,{x:18,y:9});
  assert.deepEqual(h.calls.filter(c=>c.method==='CoachPickTile').at(-1),{kind:'game',method:'CoachPickTile',value:'set:18,9'});
  assert.equal(h.e('bot-tile').textContent,'Selected tile (18, 9)');
  assert.equal(h.commands().length,0);
  h.plans.push(readyPlan([{type:'connect_conduit',x:18,y:9,reason:'Close the validated power gap.'}]));
  await h.submit(goal);
  const request=JSON.parse(h.calls.find(c=>c.url==='/api/astrabot/plan').options.body);
  assert.deepEqual(request.selectedTile,{x:18,y:9});
  assert.equal(h.commands().length,0);
});

test('switching AstraBot off cancels active planning and ignores a late reply',async()=>{
  const h=harness(),late=deferred();h.plans.push(late);const request=h.submit();await flush();
  const call=h.calls.find(c=>c.url==='/api/astrabot/plan');h.api.setEnabled(false);
  assert.equal(call.options.signal.aborted,true);assert.equal(h.api.active(),false);
  late.resolve(response(readyPlan()));await request;
  assert.equal(h.e('astrabot-task').hidden,true);assert.equal(h.e('bot-start').hidden,true);
  assert.equal(h.commands().length,0);
});

const limited = (error,retryAfterMs) => ({ok:false,status:429,json:async()=>({error,retryAfterMs})});
test('local busy retry captures a fresh screenshot and updated state before succeeding',async()=>{
  const h=harness({autoCapture:false});h.plans.push(limited('AstraBot is already reading a screen'),readyPlan());
  const request=h.submit();await flush();
  const first=h.calls.filter(c=>c.method==='CoachCapture').at(-1);h.api.screenReady(first.value,'first-frame');await flush();
  assert.match(h.e('bot-status').textContent,/Waiting.*screen reading/);
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  h.advance(4000);h.receive({credits:412});h.fireTimer(4000);await flush();
  const second=h.calls.filter(c=>c.method==='CoachCapture').at(-1);assert.notEqual(first.value,second.value);
  h.api.screenReady(second.value,'new-frame');await request;
  const bodies=h.calls.filter(c=>c.url==='/api/astrabot/plan').map(c=>JSON.parse(c.options.body));
  assert.equal(bodies.length,2);assert.equal(bodies[0].image,'data:image/jpeg;base64,first-frame');
  assert.equal(bodies[1].image,'data:image/jpeg;base64,new-frame');assert.equal(bodies[1].state.credits,412);
  assert.equal(h.calls.filter(c=>c.url==='/api/coach/config').length,1);
  assert.equal(h.e('bot-start').hidden,false);h.e('bot-expand').onclick();assert.equal(h.e('bot-start').hidden,false);assert.equal(h.api.diagnostics().batches,1);assert.equal(h.commands().length,0);
});
test('server retry hints shorten slot waits without shortening the total retry window',async()=>{
  const h=harness();for(let i=0;i<8;i++)h.plans.push(limited('AstraBot is already reading a screen',1000));h.plans.push(readyPlan());
  const request=h.submit();
  for(let i=0;i<8;i++){await flush();h.receive({credits:500-i});h.fireTimer(1000);}
  await request;
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,9);
  assert.equal(h.calls.filter(c=>c.method==='CoachCapture').length,9);
  assert.equal(h.calls.filter(c=>c.url==='/api/coach/config').length,1);
  assert.equal(h.api.diagnostics().batches,1);assert.equal(h.commands().length,0);
});
test('server retry hints are bounded to avoid rapid loops or excessive waits',async()=>{
  for(const [hint,delay] of [[1,250],[999999,4000],['1000',4000],[0,4000]]) {
    const h=harness();h.plans.push(limited('AstraBot is already reading a screen',hint),readyPlan());
    const request=h.submit();await flush();h.fireTimer(delay);await request;
    assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,2);
  }
});
test('Stop cancels a queued local-busy retry without sending another request',async()=>{
  const h=harness();h.plans.push(limited('Wait a moment before asking again'),readyPlan());
  const request=h.submit();await flush();h.stop();await request;
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  assert.equal(h.calls.filter(c=>c.method==='CoachCapture').length,1);
  assert.equal(h.api.active(),false);assert.equal(h.e('bot-start').hidden,true);
});
test('Off cancels a queued retry and no later frame restarts planning',async()=>{
  const h=harness();h.plans.push(limited('AstraBot is already reading a screen'));
  const request=h.submit();await flush();h.api.setEnabled(false);await request;h.receive({credits:550});await flush();
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
  assert.equal(h.api.active(),false);assert.equal(h.e('astrabot-task').hidden,true);
});
test('OpenAI quotas and hourly limits are never treated as retryable screen contention',async()=>{
  for (const error of ['OpenAI rate limit or credit limit reached','Hourly vision limit reached · game tips available']) {
    const h=harness();h.plans.push(limited(error));await h.submit();
    assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,1);
    assert.equal(h.api.active(),false);assert.equal(h.e('bot-status').textContent,error);
  }
});
test('repeated screen contention has a finite retry count',async()=>{
  const h=harness();for(let i=0;i<7;i++)h.plans.push(limited('AstraBot is already reading a screen'));
  const request=h.submit();
  for(let i=0;i<6;i++){await flush();h.fireTimer(4000);}
  await request;assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,7);
  assert.equal(h.api.active(),false);assert.match(h.e('bot-status').textContent,/still busy/);
});
test('the overall planning deadline cancels an in-flight screenshot',async()=>{
  const h=harness({autoCapture:false});const request=h.submit();await flush();h.fireTimer(65000);await request;
  assert.equal(h.calls.filter(c=>c.url==='/api/astrabot/plan').length,0);
  assert.equal(h.api.active(),false);assert.match(h.e('bot-status').textContent,/timed out/);
});


test('composer minimizes and reopens without losing its draft or blocking the game',()=>{
  const h=harness();h.api.open();h.e('bot-goal').value='Find new ore near the rover';
  h.e('bot-minimize').onclick();
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-editor').hidden,true);
  assert.equal(h.e('astrabot-task').hidden,false);assert.equal(h.e('bot-expand').hidden,false);
  assert.ok(h.calls.some(c=>c.method==='CoachSetInputBlocked'&&c.value==='2'));
  h.e('bot-expand').onclick();assert.equal(h.e('bot-editor').hidden,false);
  assert.equal(h.e('bot-goal').value,'Find new ore near the rover');
});

test('ready plan offers Start in its dock and survives collapse or close',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-start').hidden,false);
  h.e('bot-expand').onclick();assert.equal(h.e('bot-start').hidden,false);
  assert.equal(h.e('bot-editor').hidden,true);assert.equal(h.e('bot-plan-body').hidden,false);
  h.e('bot-minimize').onclick();h.e('bot-expand').onclick();
  assert.equal(h.e('bot-title').textContent,'A colony task');assert.equal(h.e('bot-start').hidden,false);
  h.e('bot-close').onclick();h.api.open();assert.equal(h.e('bot-start').hidden,false);
  h.e('bot-edit').onclick();assert.equal(h.e('bot-goal').value,'Connect the mine');assert.equal(h.e('bot-start').hidden,true);
  assert.equal(h.e('bot-edit').textContent,'Back to plan');h.e('bot-edit').onclick();assert.equal(h.e('bot-start').hidden,false);
});

test('tile picking and selection keep a compact dock rather than reopening the composer',()=>{
  const h=harness();h.api.open();h.e('bot-goal').value='Wire this solar panel';h.e('bot-pick').onclick();
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-editor').hidden,true);
  assert.equal(h.e('bot-expand').textContent,'Cancel picking');
  h.receive({hasPickedTile:true,pickedTile:{x:3,y:10},pickingTile:false});
  assert.equal(h.api.diagnostics().compact,true);assert.match(h.e('bot-status').textContent,/Tile selected/);
  h.e('bot-expand').onclick();assert.equal(h.e('bot-editor').hidden,false);assert.equal(h.e('bot-goal').value,'Wire this solar panel');
});

test('running and completed plans stay compact with Stop available throughout execution',async()=>{
  const h=harness();h.plans.push(readyPlan(),completePlan());await h.submit();const run=h.start();await flush();
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-stop').hidden,false);
  h.e('bot-expand').onclick();assert.equal(h.e('bot-editor').hidden,true);assert.equal(h.e('bot-stop').hidden,false);
  h.e('bot-close').onclick();assert.equal(h.api.active(),true);assert.equal(h.api.diagnostics().compact,true);
  assert.equal(h.calls.filter(c=>c.method==='CoachBotStop').length,0);
  h.acknowledge();await run;
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-stop').hidden,true);
  assert.equal(h.e('bot-plan-body').hidden,true);assert.equal(h.e('bot-expand').hidden,true);
  assert.equal(h.e('bot-status').textContent,'Goal complete.');
  assert.equal(h.e('bot-activity').hidden,true);
  h.fireTimer(2200);assert.equal(h.e('astrabot-task').hidden,true);
});

test('activity retains only the three latest messages',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();
  const run=h.start();await flush();
  for(let i=0;i<6;i++) h.receive({botBusy:true,botActionMessage:`Update ${i}`});
  assert.deepEqual(h.e('bot-timeline').children.map(item=>item.textContent),['Update 5','Update 4','Update 3']);
  h.stop();await run;
});
test('an already-complete goal closes without offering review or execution',async()=>{
  const h=harness();h.plans.push(completePlan());await h.submit();
  assert.equal(h.e('bot-status').textContent,'Goal complete.');
  assert.equal(h.e('bot-expand').hidden,true);assert.equal(h.e('bot-start').hidden,true);
  h.fireTimer(2200);assert.equal(h.e('astrabot-task').hidden,true);assert.equal(h.commands().length,0);
});
test('an old completion timer cannot close a newer task',async()=>{
  const h=harness();h.plans.push(completePlan());await h.submit();
  h.plans.push(readyPlan());await h.submit('A different task');
  h.fireTimer(2200);assert.equal(h.e('astrabot-task').hidden,false);
  assert.equal(h.e('bot-start').hidden,false);
});


test('an execution failure stays docked and preserves the plan for review',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();const run=h.start();await flush();
  h.fireTimer(75000);await run;
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-expand').hidden,false);
  assert.equal(h.e('bot-editor').hidden,true);assert.match(h.e('bot-status').textContent,/timed out/);
  h.e('bot-expand').onclick();assert.equal(h.e('bot-title').textContent,'A colony task');assert.equal(h.e('bot-start').hidden,true);
});


test('opening key settings stops takeover and closes the plan before text entry',async()=>{
  const h=harness();h.plans.push(readyPlan([{type:'select',x:11,y:7}]));await h.submit();const run=h.start();await flush();
  h.dispatch('astra:settings');await run;
  assert.equal(h.api.active(),false);assert.equal(h.e('astrabot-task').hidden,true);
  assert.ok(h.calls.some(c=>c.method==='CoachBotStop'));
});
test('changing the credential rejects a late plan and requires a new review',async()=>{
  const h=harness(),late=deferred();h.plans.push(late);const pending=h.submit();await flush();
  h.dispatch('astra:credentials');late.resolve(response(readyPlan()));await pending;
  assert.equal(h.api.active(),false);assert.equal(h.e('bot-start').hidden,true);assert.equal(h.commands().length,0);
});
