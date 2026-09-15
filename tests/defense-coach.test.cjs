const {test} = require('node:test');
const assert = require('node:assert/strict');
const {advise, signature} = require('../Assets/WebGLTemplates/Astra/coach-policy.js');
const origin = {x: 2, y: 7};
const state = extra => ({session:'defense', tool:'Explore', credits:500, battery:100, generation:2, demand:0, buildings:[], deposits:[], trains:[], ...extra});

test('disabled buildings receive repair guidance rather than mining advice', () => {
  const current = state({selected:origin, buildings:[{kind:'Extractor', origin, disabled:true, health:0, connected:true, railConnected:true, resource:'Ore'}]});
  const advice = advise(current)[0];
  assert.equal(advice.id, 'repair-building');
  assert.deepEqual(advice.target, origin);
  assert.match(advice.steps.join(' '), /Repair/);
  assert.match(advice.body, /no credits or power/);
});

test('raids without powered defenses give manual turret instructions', () => {
  const advice = advise(state({raidsStarted:true}))[0];
  assert.equal(advice.id, 'defend-base');
  assert.equal(advice.target, null);
  assert.match(advice.steps.join(' '), /7/);
});

test('turret placement preserves current placement errors', () => {
  const advice = advise(state({tool:'Turret', placementReason:'Footprint occupied.'}))[0];
  assert.equal(advice.id, 'place-turret');
  assert.match(advice.body, /Footprint occupied/);
  assert.equal(advice.target, null);
});

test('unpowered turret asks for conduit, not rail or fuel', () => {
  const current = state({selected:origin, buildings:[{kind:'Turret', origin, port:{x:2,y:6}, connected:false,
    powerRoute:{possible:true, cost:4, stops:[{x:5,y:6},{x:2,y:6}]}}]});
  const advice = advise(current)[0];
  assert.equal(advice.link.tool, 'Conduit');
  assert.match(advice.body, /turret/);
});

test('health-state transitions invalidate guidance without per-hit churn', () => {
  const current = state({buildings:[{kind:'Solar', origin, health:94, disabled:false}]});
  const initial = signature(current, []);
  current.buildings[0].health = 88;
  assert.equal(signature(current, []), initial);
  current.buildings[0].disabled = true;
  assert.notEqual(signature(current, []), initial);
});
