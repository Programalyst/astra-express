const {test} = require('node:test');
const assert = require('node:assert/strict');
const {advise,signature} = require('../Assets/WebGLTemplates/Astra/coach-policy.js');
const point = (x,y) => ({x,y,screenX:.5,screenY:.5,visible:true});
function state(extra={}) { return {session:'run-1',credits:500,battery:100,generation:2,demand:0,paused:false,
  tool:'Explore',message:'',selected:null,routeStarted:false,placementReason:'',
  rover:point(7,6),frontier:point(10,6),roverMoving:false,buildings:[],deposits:[],
  colonyPort:point(5,6),trainPhase:'Parked',capacity:4,capacityLevel:1,cargo:0,sold:0,deliveries:0,...extra}; }
function mine(extra={}) {return {kind:'Extractor',origin:point(11,7),port:point(11,6),connected:false,paused:false,
  railConnected:false,stock:0,storage:24,level:1,demand:1,rate:.5,
  powerRoute:{possible:true,cost:12,stops:[point(5,6),point(11,6)]},
  railRoute:{possible:true,cost:18,stops:[point(5,6),point(11,6)]},...extra};}
test('fresh game teaches exploration without undiscovered ore locations',()=>{
  const tip=advise(state())[0]; assert.equal(tip.id,'explore'); assert.deepEqual(tip.target,point(10,6)); assert.doesNotMatch(JSON.stringify(tip),/11, 7/);
});
test('discovered affordable ore gives exact extractor instructions',()=>{
  const tip=advise(state({deposits:[{origin:point(11,7),size:1,cost:150,buildable:true}]}))[0];
  assert.equal(tip.id,'extractor-11-7'); assert.match(tip.body,/150/); assert.match(tip.steps[0],/Press 2/);
});
test('insufficient credits do not suggest a build even with inconsistent buildable flag',()=>{
  const tip=advise(state({credits:20,deposits:[{origin:point(11,7),size:1,cost:150,buildable:true,reason:'Need 150 credits.'}]}))[0];
  assert.notEqual(tip.id,'extractor-11-7');
});
test('teaching follows power, rail, dispatch, delivery as actions change state',()=>{
  assert.equal(advise(state({buildings:[mine()]}))[0].id,'conduit-11-7');
  assert.equal(advise(state({buildings:[mine({connected:true})]}))[0].id,'rail-11-7');
  assert.equal(advise(state({buildings:[mine({connected:true,railConnected:true})]}))[0].id,'dispatch-11-7');
  assert.equal(advise(state({buildings:[mine({connected:true,railConnected:true,served:true})],trainPhase:'ToMine'}))[0].id,'first-delivery');
});
test('blocked routes never claim the route is buildable',()=>{
  const tip=advise(state({buildings:[mine({powerRoute:{possible:false,reason:'Route costs 30 credits; only 4 available.'}})]}))[0];
  assert.match(tip.id,/blocked/); assert.match(tip.body,/only 4/); assert.doesNotMatch(tip.steps.join(' '),/Click to build/);
});
test('route preview and placement feedback outrank the next mission',()=>{
  const tip=advise(state({tool:'Rail',routeStarted:true,routeStart:point(5,6),placementReason:'Route must stay on explored, unoccupied ground.'}))[0];
  assert.match(tip.body,/unoccupied/); assert.match(tip.steps.join(' '),/Escape/);
});
test('power shortage teaches pausing a mine instead of pausing the whole game',()=>{
  const tip=advise(state({battery:12,demand:3,buildings:[mine({connected:true})]}))[0];
  assert.match(tip.id,/power-low/); assert.match(tip.steps[1],/Pause Mine/); assert.match(tip.steps[1],/game runs/);
});
test('disconnected solar is connected before buying more',()=>{
  const solar=mine({kind:'Solar',origin:point(8,2),port:point(8,1)});
  assert.equal(advise(state({buildings:[solar]}))[0].id,'conduit-8-2');
});
test('paused colony only gives resume guidance',()=>{
  const tips=advise(state({paused:true,buildings:[mine()]}));assert.equal(tips.length,1);assert.equal(tips[0].id,'resume');
});
test('upgrade suggestions obey affordability and max level',()=>{
  const s=state({buildings:[mine({connected:true,railConnected:true,served:true})],trainPhase:'Loading',deliveries:2,credits:99});
  assert.equal(advise(s).some(t=>t.id==='upgrade-train'),false);
  assert.equal(advise({...s,credits:100}).some(t=>t.id==='upgrade-train'),true);
  assert.equal(advise({...s,credits:1000,capacityLevel:3}).some(t=>t.id==='upgrade-train'),false);
});
test('context signatures reject restart, purchases, tool changes and new construction',()=>{
  const s=state(),key=signature(s,advise(s));
  for(const next of [{...s,session:'run-2'},{...s,capacityLevel:2},{...s,tool:'Extractor'},{...s,buildings:[mine()]}]) assert.notEqual(signature(next,advise(next)),key);
  const animation={...s,battery:99,elapsed:1,rover:point(8,6)};assert.equal(signature(animation,advise(animation)),key);
});
test('income keeps a relevant live explanation while affordability changes invalidate it',()=>{
  const s=state({buildings:[mine({connected:true,railConnected:true,served:true})],trainPhase:'ToMine',deliveries:2,credits:352});
  const income={...s,credits:384,deliveries:3,sold:12};
  assert.equal(signature(s,advise(s)),signature(income,advise(income)));
  const poor={...s,credits:99};
  assert.notEqual(signature(poor,advise(poor)),signature({...poor,credits:100},advise({...poor,credits:100})));
});
test('placement guidance explicitly requires power and train phase uses readable language',()=>{
  const t=advise(state({deposits:[{origin:point(11,7),size:1,cost:150,buildable:true}]}))[0];
  assert.match(t.body,/conduit connection before.*produce/);
  const running=advise(state({trainPhase:'ToMine',buildings:[mine({connected:true,railConnected:true,served:true})]}))[0];
  assert.match(running.body,/travelling to the mine/);
});
test('link guidance identifies click order, cost and the preview action',()=>{
  const t=advise(state({buildings:[mine()]}))[0];
  assert.deepEqual(t.target,point(5,6));
  assert.deepEqual(t.link,{tool:'Conduit',origin:point(11,7)});
  assert.match(t.steps[1],/marker 1.*colony port tile/);
  assert.match(t.steps[2],/marker 2.*extractor/);
  assert.match(t.body,/translucent.*12 credits/);
});
test('after selecting the start, coaching points to the destination',()=>{
  const t=advise(state({tool:'Conduit',routeStarted:true,routeStart:point(5,6),buildings:[mine()]}))[0];
  assert.equal(t.title,'Now click marker 2');
  assert.deepEqual(t.target,point(11,6));
  assert.match(t.steps[0],/marker 2/);
});
test('partly completed routes continue from the remaining corner',()=>{
  const b=mine({powerRoute:{possible:true,cost:8,nextSegment:1,stops:[point(5,6),point(8,6),point(8,12)]}});
  const t=advise(state({buildings:[b]}))[0];
  assert.deepEqual(t.target,point(8,6));
  assert.match(t.steps[1],/marker 2/);
  assert.match(t.steps[2],/marker 3/);
});
test('blocked plans expose no guided build action',()=>{
  const t=advise(state({buildings:[mine({powerRoute:{possible:false,reason:'Route must stay on explored ground.'}})]}))[0];
  assert.equal(t.link,undefined);
  assert.doesNotMatch(t.body,/translucent/);
});
test('a selected second mine explains parking before reassignment',()=>{
  const second=mine({origin:point(8,13),port:point(8,12),connected:true,railConnected:true,served:false});
  const first=mine({connected:true,railConnected:true,served:true});
  const s=state({selected:second.origin,buildings:[first,second],trainPhase:'ToMine',deliveries:2});
  const tip=advise(s)[0];
  assert.equal(tip.id,'switch-mine-8-13'); assert.match(tip.steps[0],/Park to switch mine/);
  assert.equal(advise({...s,trainParkRequested:true})[0].id,'parking');
  assert.equal(advise({...s,trainPhase:'Parked',trainParkRequested:false})[0].id,'dispatch-8-13');
  assert.notEqual(advise({...s,selected:first.origin})[0].id,'switch-mine-8-13');
});
test('a revealed second deposit is acknowledged without claiming a second train',()=>{
  const tips=advise(state({trainPhase:'Loading',deliveries:2,buildings:[mine({connected:true,railConnected:true,served:true})],deposits:[{origin:point(8,13),size:1,cost:150,buildable:true}]}));
  assert.equal(tips[0].id,'expand-mine-8-13'); assert.match(tips[0].body,/single train.*parked/);
});
test('route edits invalidate stale costs even when the endpoints stay the same',()=>{
  const s=state({buildings:[mine()]});
  const next=state({buildings:[mine({powerRoute:{possible:true,cost:6,stops:[point(5,6),point(11,6)]}})]});
  assert.notEqual(signature(s,advise(s)),signature(next,advise(next)));
});
