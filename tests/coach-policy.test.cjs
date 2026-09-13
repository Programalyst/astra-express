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
  assert.match(tip.id,/power-low/); assert.match(tip.body,/Pause this mine.*game running/); assert.doesNotMatch(tip.steps[0],/Space|Pause colony/);
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
  assert.match(t.body,/connected power before.*produce/);
  const running=advise(state({trainPhase:'ToMine',buildings:[mine({connected:true,railConnected:true,served:true})]}))[0];
  assert.match(running.body,/travelling to the mine/);
});
test('link guidance identifies click order, cost and the preview action',()=>{
  const t=advise(state({tool:'Conduit',buildings:[mine()]}))[0];
  assert.deepEqual(t.target,point(5,6));
  assert.deepEqual(t.link,{tool:'Conduit',origin:point(11,7)});
  assert.equal(t.steps.length,1);assert.match(t.primaryStep,/Click.*colony port/);
  assert.match(t.body,/12 credits/);assert.equal(t.autoCue,true);assert.doesNotMatch(JSON.stringify(t),/marker|turn here/i);
});
test('after selecting the start, coaching points to the destination',()=>{
  const t=advise(state({tool:'Conduit',routeStarted:true,routeStart:point(5,6),buildings:[mine()]}))[0];
  assert.match(t.primaryStep,/Click.*extractor port/);
  assert.deepEqual(t.target,point(11,6));
  assert.equal(t.steps.length,1);
});
test('partly completed routes continue from the remaining corner',()=>{
  const b=mine({powerRoute:{possible:true,cost:8,nextSegment:1,stops:[point(5,6),point(8,6),point(8,12)]}});
  const t=advise(state({tool:'Conduit',buildings:[b]}))[0];
  assert.deepEqual(t.target,point(8,6));assert.match(t.primaryStep,/Click.*route tile/);
  const next=advise(state({tool:'Conduit',routeStarted:true,routeStart:point(8,6),buildings:[b]}))[0];
  assert.deepEqual(next.target,point(8,12));assert.equal(next.steps.length,1);assert.doesNotMatch(JSON.stringify(next),/marker|turn here/i);
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
  assert.equal(tip.id,'buy-train-8-13'); assert.match(tip.primaryStep,/Open Fleet/); assert.match(tip.body,/150 credits/); assert.doesNotMatch(tip.steps.join(' '),/Park/);
  assert.equal(advise({...s,trainParkRequested:true})[0].id,'parking');
  assert.equal(advise({...s,trainPhase:'Parked',trainParkRequested:false})[0].id,'dispatch-8-13');
  assert.notEqual(advise({...s,selected:first.origin})[0].id,'switch-mine-8-13');
});
test('a revealed second deposit teaches construction before later fleet setup',()=>{
  const tips=advise(state({trainPhase:'Loading',deliveries:2,buildings:[mine({connected:true,railConnected:true,served:true})],deposits:[{origin:point(8,13),size:1,cost:150,buildable:true}]}));
  assert.equal(tips[0].id,'expand-mine-8-13'); assert.match(tips[0].body,/150 credits.*connected power/); assert.equal(tips[0].steps.length,1); assert.doesNotMatch(tips[0].body,/single train/);
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
  const t=advise(state({uiAnchors:cueAnchors,selected:b.origin,trainPhase:'ToMine',trains:[loco(0),loco(1,{phase:'Parked',source:null,destination:null})],buildings:[b]}))[0];
  assert.equal(t.id,'dispatch-11-7');assert.match(t.steps.join(' '),/Dispatch idle train/);assert.doesNotMatch(t.body+t.steps.join(' '),/park.*before|Buy train/);
});
test('full fleet explains selecting a service to park rather than buying fifth train',()=>{
  const b=mine({connected:true,railConnected:true,served:false});
  const s=state({selected:b.origin,buildings:[b],trains:[0,1,2,3].map(i=>loco(i)),trainCount:4,maxTrains:4,canBuyTrain:false});
  const t=advise(s)[0];assert.equal(t.id,'switch-mine-11-7');assert.match(t.primaryStep,/Open Fleet/);assert.match(t.body,/Park the selected service/);assert.doesNotMatch(t.steps.join(' '),/Buy train/);
});
test('dispatch and first delivery for Fluxite never claim fuel income',()=>{
  const b=fuelMine();const p=plant();
  const t=advise(state({uiAnchors:cueAnchors,selected:b.origin,buildings:[b,p],trains:[loco(0,{phase:'Parked'})]}))[0];
  assert.equal(t.id,'dispatch-13-3');assert.match(t.body,/power plant.*never sold/);assert.equal(t.uiTarget,'primary-action');assert.match(t.body,/selected power plant/);assert.doesNotMatch(t.steps.join(' '),/8 credits per/);
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
  assert.equal(t.id,'rail-3-11');assert.deepEqual(t.link,{tool:'Rail',origin:p.origin});assert.match(t.body,/Fluxite.*plant/);
});
test('unpowered plant is connected before the fuel train is dispatched',()=>{
  const b=fuelMine();const p=plant({connected:false});
  const t=advise(state({selected:b.origin,buildings:[b,p]}))[0];assert.equal(t.id,'conduit-3-11');assert.match(t.body,/plant.*delivered Fluxite/);
});
test('full plant explains stalled fuel unload without selling fuel or upgrading capacity',()=>{
  const ore=mine({connected:true,railConnected:true,served:true});const fuel=fuelMine({served:true});const p=plant({stock:48});
  const t=advise(state({deliveries:2,buildings:[ore,fuel,p],trains:[loco(0),loco(1,{resource:'Fluxite',phase:'Unloading',waitingForFuelSpace:true,destination:p.origin})]}))[0];
  assert.equal(t.id,'fuel-unloading-1');assert.match(t.body,/storage is full/);assert.match(t.body,/battery needs charging/);assert.doesNotMatch(t.steps.join(' '),/Capacity|sell/);
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
  const t=advise(s)[0];assert.equal(t.id,'plant-battery-full');assert.match(t.body,/fuel.*retained/i);
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
  assert.equal(ready.uiTarget,null);assert.deepEqual(ready.target,point(11,7));assert.match(ready.primaryStep,/Click.*resource patch/);assert.equal(ready.autoCue,true);
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
  assert.equal(port.uiTarget,null);assert.match(port.primaryStep,/Click.*colony port/);
  const destination=advise({...s,tool:'Conduit',routeStarted:true,routeStart:point(5,6)})[0];
  assert.deepEqual(destination.target,point(11,6));assert.match(destination.primaryStep,/Click.*extractor port/);assert.equal(destination.steps.length,1);
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

const smartState = extra => state({smartRouting:true,uiAnchors:cueAnchors,...extra});
const solar = extra => mine({kind:'Solar',origin:point(8,2),port:point(8,1),
  powerRoute:{possible:true,cost:16,stops:[point(5,6),point(8,6),point(8,1)]},...extra});
test('smart connection teaches the tool and two endpoints without corner clicks',()=>{
  const b=mine({powerRoute:{possible:true,cost:10,nextSegment:1,stops:[point(5,6),point(9,6),point(9,8),point(11,8),point(11,6)]}});
  const t=advise(smartState({buildings:[b]}))[0];
  assert.equal(t.uiTarget,'tool-conduit');assert.equal(t.target,null);
  assert.deepEqual(t.link,{tool:'Conduit',origin:b.origin});
  assert.match(t.primaryStep,/Choose Conduit/);assert.equal(t.steps.length,1);assert.equal(t.autoCue,true);
  assert.match(t.body,/10 credits/);
  assert.doesNotMatch(JSON.stringify(t),/marker|corner|segment/i);
  const ready=advise(smartState({buildings:[b],tool:'Conduit'}))[0];
  assert.equal(ready.uiTarget,null);assert.match(ready.steps[0],/Click.*colony port/);
  assert.match(ready.targetLabel,/Click.*colony port/);
});
test('smart solar start points back to colony even when starter solar is listed first',()=>{
  const b=solar();const starter=solar({origin:point(3,2),port:point(3,1),connected:true});
  for(const start of [b.port,b.origin]) {
    const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:start,selected:b.origin,
      buildings:[starter,b],connectionTargets:[starter.port,point(5,6)]}))[0];
    assert.deepEqual(t.target,point(5,6));assert.match(t.title,/colony port/);
    assert.match(t.steps[0],/Click the highlighted colony port/);
    assert.match(t.targetLabel,/colony port/);assert.equal(t.uiTarget,null);
    assert.doesNotMatch(JSON.stringify(t),/marker|corner|segment/i);
  }
});
test('smart colony start targets unfinished mine instead of connected starter or route corners',()=>{
  const b=mine({powerRoute:{possible:true,cost:8,nextSegment:1,stops:[point(5,6),point(8,6),point(8,8),point(11,8),point(11,6)]}});
  const starter=solar({connected:true});
  const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),
    buildings:[starter,b],connectionTargets:[starter.port,b.port]}))[0];
  assert.deepEqual(t.target,b.port);assert.match(t.steps[0],/Click.*extractor port/);
  assert.doesNotMatch(JSON.stringify(t),/marker|corner|segment/i);
});
test('smart colony start prioritizes the selected unfinished building among available ports',()=>{
  const b=mine(),p=solar();
  const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),selected:b.origin,
    buildings:[p,b],connectionTargets:[p.port,b.port]}))[0];
  assert.deepEqual(t.target,b.port);
});
test('smart colony start keeps solar connection guidance when it was the pending task',()=>{
  const b=mine(),p=solar();
  const s=smartState({tool:'Conduit',buildings:[b,p],connectionTargets:[b.port,p.port]});
  assert.equal(advise(s)[0].link.origin,p.origin);
  const t=advise({...s,routeStarted:true,routeStart:point(5,6)})[0];
  assert.deepEqual(t.target,p.port);assert.match(t.targetLabel,/solar array port/);
});
test('smart rails advance from tool cue to full destination and reverse back to depot',()=>{
  const b=mine({connected:true});
  const s=smartState({buildings:[b]});
  assert.equal(advise(s)[0].uiTarget,'tool-rail');
  for(const [start,target] of [[point(5,6),b.port],[b.port,point(5,6)]]) {
    const t=advise({...s,tool:'Rail',routeStarted:true,routeStart:start,connectionTargets:[target]})[0];
    assert.deepEqual(t.target,target);assert.match(t.body,/valid, affordable connection/);
    assert.doesNotMatch(JSON.stringify(t),/marker|corner|segment/i);
  }
});
test('smart invalid hover preserves the reason and only suggests previewing another valid port',()=>{
  const b=mine();
  const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),buildings:[b],
    connectionTargets:[b.port],placementReason:'Route cannot cross an occupied building.'}))[0];
  assert.match(t.body,/cannot cross an occupied building/);
  assert.deepEqual(t.target,b.port);assert.match(t.steps[0],/Move to.*port/);
  assert.match(t.steps[1],/Click a valid preview/);
  assert.doesNotMatch(t.body,/has an affordable route|ready to build/);
  assert.doesNotMatch(t.steps[0]+t.targetLabel,/Click.*to build/);
  assert.doesNotMatch(JSON.stringify(t),/marker|corner|segment/i);
});
test('smart unavailable mine is not replaced with an already-connected solar destination',()=>{
  const b=mine(),starter=solar({connected:true});
  const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),
    buildings:[starter,b],connectionTargets:[starter.port]}))[0];
  assert.equal(t.target,null);assert.equal(t.link,undefined);
  assert.match(t.body,/No suitable building port/);
  assert.match(t.steps.join(' '),/Click a valid preview/);
  assert.doesNotMatch(t.body,/has an affordable route/);
});
test('smart solar start does not invent an available colony route',()=>{
  const p=solar(),b=mine();
  for(const connectionTargets of [[],[b.port],undefined]) {
    const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:p.port,
      buildings:[b,p],connectionTargets}))[0];
    assert.equal(t.target,null);assert.doesNotMatch(t.body,/has an affordable route/);
    assert.match(t.steps.join(' '),/Click a valid preview/);
  }
});
test('smart blocked initial plans expose no connection action or ready claim',()=>{
  const t=advise(smartState({buildings:[mine({powerRoute:{possible:false,reason:'Route costs 30 credits; only 4 available.'}})]}))[0];
  assert.equal(t.link,undefined);assert.match(t.body,/only 4/);
  assert.doesNotMatch(t.body+t.steps.join(' '),/Click.*build the.*connection|has an affordable route/);
});
test('smart target availability invalidates advice but camera movement does not',()=>{
  const b=mine();const s=smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),buildings:[b],connectionTargets:[b.port]});
  const key=signature(s,advise(s));
  const missing={...s,connectionTargets:[]};
  assert.notEqual(signature(missing,advise(missing)),key);
  const camera={...s,connectionTargets:[{...b.port,screenX:.8,screenY:.2,visible:true}]};
  assert.equal(signature(camera,advise(camera)),key);
  const offscreen={...s,connectionTargets:[{...b.port,visible:false}]};
  assert.notEqual(signature(offscreen,advise(offscreen)),key);
  const legacy={...s,smartRouting:false};
  assert.notEqual(signature(legacy,advise(legacy)),key);
});


test('missing or hidden tool anchor gives a keyboard step without a premature world cue',()=>{
  for (const uiAnchors of [[],[{id:'tool-conduit',visible:false}]]) {
    const t=advise(smartState({uiAnchors,buildings:[mine()]}))[0];
    assert.equal(t.uiTarget,null);assert.equal(t.target,null);assert.equal(t.autoCue,false);
    assert.equal(t.primaryStep,'Press 4 for Conduit.');assert.equal(t.steps.length,1);
  }
});

test('valid offscreen endpoint requests Show target rather than an invisible world click',()=>{
  const b=mine(),out={...b.port,visible:false};
  const t=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),buildings:[b],connectionTargets:[out]}))[0];
  assert.deepEqual(t.target,out);assert.equal(t.autoCue,true);assert.equal(t.primaryStep,'Click Show target.');
  const visible=advise(smartState({tool:'Conduit',routeStarted:true,routeStart:point(5,6),buildings:[b],connectionTargets:[b.port]}))[0];
  assert.match(visible.primaryStep,/Click.*extractor port/);assert.equal(visible.steps.length,1);
});

test('placement failure preserves feedback and guides movement to a known footprint before clicking',()=>{
  const s=smartState({tool:'Extractor',placementReason:'Cannot build on occupied ground.',
    deposits:[{origin:point(11,7),resource:'Ore',size:1,cost:150,buildable:true}]});
  const t=advise(s)[0];assert.equal(t.body,s.placementReason);assert.deepEqual(t.target,point(11,7));
  assert.equal(t.autoCue,true);assert.match(t.primaryStep,/Move to/);assert.doesNotMatch(t.primaryStep,/Click/);
  const ready=advise({...s,placementReason:''})[0];assert.match(ready.primaryStep,/Click.*resource patch/);
});


test('first-route next-click cues advance through exploration, construction and dispatch',()=>{
  const s=smartState({});assert.equal(advise(s)[0].autoCue,true);assert.deepEqual(advise(s)[0].target,s.frontier);
  const deposit={origin:point(11,7),resource:'Ore',size:1,cost:150,buildable:true};
  const tool=advise({...s,deposits:[deposit]})[0];assert.equal(tool.uiTarget,'tool-extractor');assert.equal(tool.autoCue,true);
  const build=advise({...s,deposits:[deposit],tool:'Extractor'})[0];assert.deepEqual(build.target,deposit.origin);assert.equal(build.autoCue,true);
  const b=mine({connected:true,railConnected:true});
  const explore=advise({...s,buildings:[b],tool:'Rail'})[0];assert.equal(explore.uiTarget,'tool-explore');assert.equal(explore.autoCue,true);
  const select=advise({...s,buildings:[b]})[0];assert.deepEqual(select.target,b.origin);assert.equal(select.autoCue,true);
  const dispatch=advise({...s,buildings:[b],selected:b.origin})[0];assert.equal(dispatch.uiTarget,'primary-action');assert.equal(dispatch.autoCue,true);
  assert.equal(advise({...s,roverMoving:true})[0].autoCue,false);
  assert.equal(advise({...s,buildings:[mine({powerRoute:{possible:false,reason:'Not enough credits.'}})]})[0].autoCue,false);
});


test('compact network sidebar hands a selected linked mine from Explore to visible Dispatch',()=>{
  const b=mine({connected:true,railConnected:true,served:false});
  const compactAnchors=cueAnchors.filter(a=>a.id!=='primary-action'&&a.id!=='mine-pause');
  for (const tool of ['Rail','Conduit']) {
    const s=smartState({tool,selected:b.origin,buildings:[b],uiAnchors:compactAnchors});
    const leaveTool=advise(s)[0];
    assert.equal(leaveTool.id,'dispatch-11-7');assert.equal(leaveTool.uiTarget,'tool-explore');
    assert.equal(leaveTool.primaryStep,'Choose Explore.');assert.equal(leaveTool.target,null);assert.equal(leaveTool.autoCue,true);
    const dispatch=advise({...s,tool:'Explore',uiAnchors:cueAnchors})[0];
    assert.equal(dispatch.uiTarget,'primary-action');assert.equal(dispatch.primaryStep,'Click Dispatch idle train.');
    assert.equal(dispatch.autoCue,true);assert.equal(dispatch.steps.length,1);
    // A later snapshot without the button selects the mine instead of cueing
    // an absent control or clicking while a network tool remains active.
    const select=advise({...s,tool:'Explore'})[0];
    assert.equal(select.uiTarget,null);assert.deepEqual(select.target,b.origin);assert.match(select.primaryStep,/Click.*building/);
  }
});


test('manual construction tools outrank a selected earning mine capacity upgrade',()=>{
  const b=mine({connected:true,railConnected:true,served:true,stock:24,storage:24});
  const s=smartState({selected:b.origin,buildings:[b],deliveries:3,trainPhase:'ToMine',
    generation:2,demand:1,solarSite:point(3,10),plantSite:point(6,12),credits:500});
  assert.equal(advise(s)[0].id,'upgrade-train');
  const solarTip=advise({...s,tool:'Solar'})[0];assert.equal(solarTip.id,'expand-power');
  assert.deepEqual(solarTip.target,s.solarSite);assert.equal(solarTip.uiTarget,null);assert.equal(solarTip.autoCue,true);
  const plantTip=advise({...s,tool:'PowerPlant'})[0];assert.equal(plantTip.id,'build-plant');assert.deepEqual(plantTip.target,s.plantSite);
  const deposit={origin:point(8,13),resource:'Ore',cost:150,size:1,buildable:true};
  const extractorTip=advise({...s,tool:'Extractor',deposits:[deposit]})[0];assert.equal(extractorTip.id,'extractor-8-13');assert.deepEqual(extractorTip.target,deposit.origin);
});

test('manual construction respects actual budget, safe sites and battery safety',()=>{
  const b=mine({connected:true,railConnected:true,served:true,stock:24,storage:24});
  const s=smartState({selected:b.origin,buildings:[b],deliveries:3,trainPhase:'ToMine',tool:'Solar',solarSite:point(3,10)});
  for (const change of [{credits:99},{solarSite:null,placementReason:'No clear footprint.'}]) {
    const t=advise({...s,...change})[0];assert.equal(t.id,'placement-blocked-Solar');assert.equal(t.target,null);assert.equal(t.autoCue,false);
    assert.notEqual(t.uiTarget,'fleet');
  }
  const noOre=advise({...s,tool:'Extractor',deposits:[]})[0];assert.equal(noOre.id,'placement-blocked-Extractor');assert.equal(noOre.target,null);
  const low=advise({...s,battery:12,generation:2,demand:3,buildings:[{...b,stock:0}]})[0];assert.match(low.id,/power-low/);
  const badHover=advise({...s,placementReason:'Footprint is occupied.'})[0];assert.equal(badHover.body,'Footprint is occupied.');assert.match(badHover.primaryStep,/Move to/);
});
