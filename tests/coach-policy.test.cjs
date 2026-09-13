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
test('a selected second mine offers buying without stopping the current service',()=>{
  const second=mine({origin:point(8,13),port:point(8,12),connected:true,railConnected:true,served:false});
  const first=mine({connected:true,railConnected:true,served:true});
  const s=state({selected:second.origin,buildings:[first,second],trainPhase:'ToMine',deliveries:2});
  const tip=advise(s)[0];
  assert.equal(tip.id,'buy-train-8-13'); assert.match(tip.steps[1],/Buy train.*150/); assert.doesNotMatch(tip.steps.join(' '),/Park/);
  assert.equal(advise({...s,trainParkRequested:true})[0].id,'parking');
  assert.equal(advise({...s,trainPhase:'Parked',trainParkRequested:false})[0].id,'dispatch-8-13');
  assert.notEqual(advise({...s,selected:first.origin})[0].id,'switch-mine-8-13');
});
test('a revealed second deposit explains expanding the fleet',()=>{
  const tips=advise(state({trainPhase:'Loading',deliveries:2,buildings:[mine({connected:true,railConnected:true,served:true})],deposits:[{origin:point(8,13),size:1,cost:150,buildable:true}]}));
  assert.equal(tips[0].id,'expand-mine-8-13'); assert.match(tips[0].body,/idle locomotive.*up to 4/); assert.doesNotMatch(tips[0].body,/single train/);
});
test('route edits invalidate stale costs even when the endpoints stay the same',()=>{
  const s=state({buildings:[mine()]});
  const next=state({buildings:[mine({powerRoute:{possible:true,cost:6,stops:[point(5,6),point(11,6)]}})]});
  assert.notEqual(signature(s,advise(s)),signature(next,advise(next)));
});

const loco=(index,extra={})=>({index,phase:'ToMine',resource:'Ore',capacity:4,capacityLevel:1,parkRequested:false,source:point(11,7),destination:point(5,7),...extra});
const plant=(extra={})=>mine({kind:'PowerPlant',resource:'Fluxite',origin:point(3,11),port:point(3,10),connected:true,railConnected:true,stock:0,storage:48,burnEnergy:0,...extra});
const fuelMine=(extra={})=>mine({resource:'Fluxite',origin:point(13,3),port:point(13,2),connected:true,railConnected:true,served:false,destination:point(3,11),destinationRailConnected:true,...extra});
test('an idle second locomotive dispatches even while selected locomotive is busy',()=>{
  const b=mine({connected:true,railConnected:true,served:false,stock:24});
  const t=advise(state({selected:b.origin,trainPhase:'ToMine',trains:[loco(0),loco(1,{phase:'Parked',source:null,destination:null})],buildings:[b]}))[0];
  assert.equal(t.id,'dispatch-11-7');assert.match(t.steps.join(' '),/Dispatch idle train/);assert.doesNotMatch(t.body+t.steps.join(' '),/park.*before|Buy train/);
});
test('full fleet explains selecting a service to park rather than buying fifth train',()=>{
  const b=mine({connected:true,railConnected:true,served:false});
  const s=state({selected:b.origin,buildings:[b],trains:[0,1,2,3].map(i=>loco(i)),trainCount:4,maxTrains:4,canBuyTrain:false});
  const t=advise(s)[0];assert.equal(t.id,'switch-mine-11-7');assert.match(t.steps.join(' '),/Fleet.*Park at colony/);assert.doesNotMatch(t.steps.join(' '),/Buy train/);
});
test('dispatch and first delivery for Fluxite never claim fuel income',()=>{
  const b=fuelMine();const p=plant();
  const t=advise(state({selected:b.origin,buildings:[b,p],trains:[loco(0,{phase:'Parked'})]}))[0];
  assert.equal(t.id,'dispatch-13-3');assert.match(t.body,/power plant.*never sold/);assert.match(t.steps.join(' '),/plant destination/);assert.doesNotMatch(t.steps.join(' '),/8 credits per/);
  const running=advise(state({buildings:[b,p],trains:[loco(0,{resource:'Fluxite',source:b.origin,destination:p.origin})]}));
  assert.equal(running.some(t=>t.id==='first-delivery'),false);
});
test('Fluxite requires a plant and checks both depot and destination rail links',()=>{
  const b=fuelMine();
  const s=state({selected:b.origin,buildings:[b],plantSite:point(3,11),plantCost:250});
  assert.equal(advise(s)[0].id,'build-plant');
  assert.equal(advise({...s,credits:249,plantSite:null})[0].id,'plant-needed');
  assert.equal(advise({...s,buildings:[{...b,railConnected:false}]})[0].id,'rail-13-3');
  const p=plant({railConnected:false,railRoute:{possible:true,cost:12,stops:[point(5,6),point(3,10)]}});
  const t=advise({...s,buildings:[{...b,destinationRailConnected:false},p]})[0];
  assert.equal(t.id,'rail-3-11');assert.deepEqual(t.link,{tool:'Rail',origin:p.origin});assert.match(t.body,/plant.*Fluxite/);
});
test('unpowered plant is connected before the fuel train is dispatched',()=>{
  const b=fuelMine();const p=plant({connected:false});
  const t=advise(state({selected:b.origin,buildings:[b,p]}))[0];assert.equal(t.id,'conduit-3-11');assert.match(t.body,/plant.*delivered Fluxite/);
});
test('full plant explains stalled fuel unload without selling fuel or upgrading capacity',()=>{
  const ore=mine({connected:true,railConnected:true,served:true});const fuel=fuelMine({served:true});const p=plant({stock:48});
  const t=advise(state({deliveries:2,buildings:[ore,fuel,p],trains:[loco(0),loco(1,{resource:'Fluxite',phase:'Unloading',waitingForFuelSpace:true,destination:p.origin})]}))[0];
  assert.equal(t.id,'fuel-unloading-1');assert.match(t.body,/storage is full/);assert.match(t.steps.join(' '),/battery needs charging/);assert.doesNotMatch(t.steps.join(' '),/Capacity|sell/);
});
test('first profitable tutorial chooses ore when Fluxite was also discovered',()=>{
  const t=advise(state({deposits:[{resource:'Fluxite',origin:point(13,3),cost:150,size:1,buildable:true},{resource:'Ore',origin:point(11,7),cost:150,size:1,buildable:true}]}))[0];
  assert.equal(t.id,'extractor-11-7');
  assert.equal(advise(state({deposits:[{resource:'Fluxite',origin:point(13,3),cost:150,size:1,buildable:true}]}))[0].id,'explore');
});
test('Fluxite discoveries after ore tutorial explain fuel destination and no sale',()=>{
  const t=advise(state({deliveries:2,trains:[loco(0)],buildings:[mine({connected:true,railConnected:true,served:true})],deposits:[{resource:'Fluxite',origin:point(13,3),cost:150,size:1,buildable:true}]}))[0];
  assert.equal(t.id,'expand-mine-13-3');assert.match(t.body,/power plant.*never sold/);
});
test('fleet assignment and destination changes invalidate advice while fuel generation animation does not',()=>{
  const b=fuelMine({served:true});const p=plant({stock:10});
  const s=state({deliveries:2,solarGeneration:2,fuelGeneration:5,generation:7,buildings:[mine({connected:true,railConnected:true,served:true}),b,p],trains:[loco(0),loco(1,{resource:'Fluxite',destination:p.origin})]});
  const key=signature(s,advise(s));
  assert.equal(signature({...s,fuelGeneration:3,generation:5},advise({...s,fuelGeneration:3,generation:5})),key);
  for(const next of [{...s,idleTrains:1},{...s,trains:[loco(0),loco(1,{phase:'Parked'})]},{...s,fuelDestination:point(6,12)}])assert.notEqual(signature(next,advise(next)),key);
});

test('selected plants explain idle generation at full battery and resuming a paused plant',()=>{
  const p=plant({stock:20});const s=state({selected:p.origin,buildings:[p],battery:100});
  const t=advise(s)[0];assert.equal(t.id,'plant-battery-full');assert.match(t.body,/fuel.*retained/);
  assert.equal(advise({...s,buildings:[{...p,paused:true}]})[0].id,'resume-plant');
});
test('selected empty plant asks for discovered Fluxite without inventing a hidden fuel coordinate',()=>{
  const p=plant();const s=state({selected:p.origin,buildings:[mine({connected:true,railConnected:true,served:true}),p],battery:80,deliveries:2});
  assert.equal(advise(s)[0].id,'find-fuel');
  const t=advise({...s,deposits:[{resource:'Fluxite',origin:point(13,3),cost:150,size:1,buildable:true}]} )[0];
  assert.equal(t.id,'fuel-extractor-13-3');assert.match(t.body,/never sold/);
});

const cueAnchors = ['tool-explore','tool-extractor','tool-conduit','tool-rail','tool-solar','tool-plant','fleet','pause','primary-action','mine-pause','buy-train','train-capacity','train-next'].map(id=>({id,visible:true}));
test('construction cues progress from the real toolbar to the revealed world patch',()=>{
  const s=state({uiAnchors:cueAnchors,deposits:[{origin:point(11,7),size:1,cost:150,buildable:true}]});
  assert.equal(advise(s)[0].uiTarget,'tool-extractor');
  const ready=advise({...s,tool:'Extractor'})[0];
  assert.equal(ready.uiTarget,null);assert.deepEqual(ready.target,point(11,7));assert.match(ready.targetLabel,/place the extractor/);
});
test('dispatch cues require selecting the intended mine before pointing at its sidebar action',()=>{
  const b=mine({connected:true,railConnected:true});
  const s=state({uiAnchors:cueAnchors,buildings:[b],tool:'Rail'});
  assert.equal(advise(s)[0].uiTarget,'tool-explore');
  assert.equal(advise({...s,tool:'Explore',selected:point(3,11)})[0].uiTarget,null);
  const ready=advise({...s,tool:'Explore',selected:b.origin})[0];
  assert.equal(ready.uiTarget,'primary-action');assert.match(ready.cueLabel,/Dispatch idle train/);
  assert.doesNotMatch(ready.steps.join(' '),/Press 1/);
});
test('connection cues advance from tool to port to destination without repeating completed steps',()=>{
  const s=state({uiAnchors:cueAnchors,buildings:[mine()]});
  assert.equal(advise(s)[0].uiTarget,'tool-conduit');
  const port=advise({...s,tool:'Conduit'})[0];
  assert.equal(port.uiTarget,null);assert.match(port.steps[0],/Click marker 1/);
  const destination=advise({...s,tool:'Conduit',routeStarted:true,routeStart:point(5,6)})[0];
  assert.deepEqual(destination.target,point(11,6));assert.match(destination.targetLabel,/marker 2/);
});
test('Fleet cues advance to Buy train and never point at absent controls',()=>{
  const b=mine({connected:true,railConnected:true});
  const s=state({uiAnchors:cueAnchors,buildings:[b],selected:b.origin,trainPhase:'ToMine'});
  assert.equal(advise(s)[0].uiTarget,'fleet');
  assert.equal(advise({...s,trainSelected:true})[0].uiTarget,'buy-train');
  assert.equal(advise({...s,trainSelected:true,uiAnchors:[]})[0].uiTarget,null);
});
test('paused colony has an actionable Resume cue without a world target',()=>{
  const t=advise(state({paused:true,uiAnchors:cueAnchors}))[0];
  assert.equal(t.uiTarget,'pause');assert.equal(t.target,null);
});
test('capacity cues select the intended train before highlighting an upgrade',()=>{
  const s=state({uiAnchors:cueAnchors,deliveries:2,trainSelected:true,selectedTrainIndex:0,
    buildings:[mine({connected:true,railConnected:true,served:true})],
    trains:[loco(0,{capacityLevel:3}),loco(1,{capacityLevel:1})]});
  assert.equal(advise(s).find(t=>t.id==='upgrade-train').uiTarget,'train-next');
  assert.equal(advise({...s,selectedTrainIndex:1}).find(t=>t.id==='upgrade-train').uiTarget,'train-capacity');
});
