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
    return next?.promise ? next.promise : response(next);
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
  assert.equal(h.commands().length,0);assert.equal(h.e('bot-start').hidden,false,h.e('bot-status').textContent);assert.equal(h.api.active(),false);
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
