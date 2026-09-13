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
  const elements = new Map(), listeners = new Map(), timers = new Map(), calls = [], plans = [];
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
  const window={};
  const game={SendMessage(object,method,value) { calls.push({kind:'game',method,value}); if(method==='CoachCapture'&&autoCapture)queueMicrotask(()=>window.astraBotControl.screenReady(value,'jpeg')); }};
  const fetch=async(url,options={})=>{
    calls.push({kind:'fetch',url,options});
    if(url==='/api/coach/config')return response({configured:true,token:'test-token'});
    const next=plans.length?plans.shift():completePlan();
    return next?.promise ? next.promise : next?.json ? next : response(next);
  };
  vm.runInNewContext(source,{document,window,fetch,AbortController,queueMicrotask,console,
    Date:{now:()=>now},setTimeout:(fn,ms)=>{const id=++timerSequence;timers.set(id,{fn,ms});return id;},clearTimeout:id=>timers.delete(id),
    sessionStorage:{setItem(){}},
  },{filename:'astrabot-control.js'});
  const api=window.astraBotControl;api.ready(game);
  let state={session:'colony-1',credits:500,deliveries:0,pickedTile:null,pickingTile:false};
  const receive=extra=>{state={...state,...extra};api.receive(state);};receive({});
  const commands=()=>calls.filter(c=>c.kind==='game'&&c.method==='CoachBotCommand').map(c=>JSON.parse(c.value));
  const event=()=>({preventDefault(){},stopImmediatePropagation(){},stopPropagation(){}});
  const submit=()=>{elements.get('bot-goal').value='Connect the mine';return elements.get('bot-goal-form').onsubmit(event());};
  const acknowledge=(status='complete',id=commands().at(-1)?.id)=>receive({botActionId:id,botActionStatus:status,botActionMessage:status});
  return {api,e:id=>elements.get(id),calls,plans,submit,commands,receive,acknowledge,
    start:()=>elements.get('bot-start').onclick(),stop:()=>elements.get('bot-stop').onclick(),
    advance:ms=>{now+=ms;},fireTimer:ms=>{const entry=[...timers].find(([,t])=>t.ms===ms);assert.ok(entry,`timer ${ms} exists`);timers.delete(entry[0]);entry[1].fn();},
    dispatch:(name,extra={})=>{for(const fn of listeners.get(name)||[])fn({...event(),...extra});},document};
}

test('creating a plan captures a frame but never starts gameplay without Start',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();
  assert.equal(h.commands().length,0);assert.equal(h.e('bot-start').hidden,true);h.e('bot-expand').onclick();assert.equal(h.e('bot-start').hidden,false,h.e('bot-status').textContent);assert.equal(h.api.active(),false);
  const request=h.calls.find(c=>c.url==='/api/astrabot/plan');const body=JSON.parse(request.options.body);
  assert.equal(body.image,'data:image/jpeg;base64,jpeg');assert.equal(body.state.session,'colony-1');
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

test('switching AstraBot off cancels active planning and ignores a late reply',async()=>{
  const h=harness(),late=deferred();h.plans.push(late);const request=h.submit();await flush();
  const call=h.calls.find(c=>c.url==='/api/astrabot/plan');h.api.setEnabled(false);
  assert.equal(call.options.signal.aborted,true);assert.equal(h.api.active(),false);
  late.resolve(response(readyPlan()));await request;
  assert.equal(h.e('astrabot-task').hidden,true);assert.equal(h.e('bot-start').hidden,true);
  assert.equal(h.commands().length,0);
});

const limited = error => ({ok:false,status:429,json:async()=>({error})});
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
  assert.equal(h.e('bot-start').hidden,true);h.e('bot-expand').onclick();assert.equal(h.e('bot-start').hidden,false);assert.equal(h.api.diagnostics().batches,1);assert.equal(h.commands().length,0);
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

test('ready plan remains docked until explicit review and survives collapse or close',async()=>{
  const h=harness();h.plans.push(readyPlan());await h.submit();
  assert.equal(h.api.diagnostics().compact,true);assert.equal(h.e('bot-start').hidden,true);
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
  assert.equal(h.e('bot-plan-body').hidden,true);assert.equal(h.e('bot-expand').hidden,false);
  assert.equal(h.e('bot-status').textContent,'Goal complete.');
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
