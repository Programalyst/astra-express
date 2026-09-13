(() => {
  "use strict";
  const contract = window.astraBotContract;
  const goals = new Map();
  const requireValid = (condition, message) => { if (!condition) throw new Error(message); };
  const point = value => value && Number.isInteger(value.x) && Number.isInteger(value.y) && value.x >= 0 && value.x < 28 && value.y >= 0 && value.y < 22 ? `${value.x},${value.y}` : null;
  const textWithin = (value, maximum, minimum = 1) => typeof value === "string" && value.length >= minimum && value.length <= maximum;
  function matches(value, schema) {
    if (schema.anyOf) return schema.anyOf.some(branch => matches(value, branch));
    const types = Array.isArray(schema.type) ? schema.type : [schema.type];
    const type = value === null ? "null" : Array.isArray(value) ? "array" : typeof value === "number" && Number.isInteger(value) ? "integer" : typeof value;
    if (!types.includes(type) || schema.enum && !schema.enum.includes(value)) return false;
    if (type === "string") return value.length >= (schema.minLength ?? 0) && value.length <= (schema.maxLength ?? Infinity);
    if (type === "integer") return value >= (schema.minimum ?? -Infinity) && value <= (schema.maximum ?? Infinity);
    if (type === "array") return value.length <= (schema.maxItems ?? Infinity) && value.every(item => matches(item, schema.items));
    if (type === "object") return schema.required.every(key => Object.hasOwn(value, key)) && Object.keys(value).every(key => Object.hasOwn(schema.properties, key) && matches(value[key], schema.properties[key]));
    return true;
  }
  function validateInput(data, planning) {
    requireValid(data && typeof data === "object" && !Array.isArray(data), "Invalid game request.");
    requireValid(typeof data.image === "string" && data.image.length <= 2700000 && /^data:image\/jpeg;base64,\/9j\/[A-Za-z0-9+/]*={0,2}$/.test(data.image) && data.image.length >= 150, "A current JPEG game frame is required.");
    requireValid(data.state && textWithin(data.state.session, 128) && JSON.stringify(data.state).length <= 180000, "Current game state is required.");
    if (planning) {
      requireValid(textWithin(data.goal?.trim(), 600), "Describe a goal in 600 characters or fewer.");
      requireValid(data.selectedTile == null || point(data.selectedTile), "Invalid selected game tile.");
      const previous = data.previousPlan;
      requireValid(previous == null || typeof previous === "object" && !Array.isArray(previous) && JSON.stringify(previous).length <= 60000 && ["actions", "results"].every(key => previous[key] === undefined || Array.isArray(previous[key]) && previous[key].length <= (key === "actions" ? 6 : 60)), "Previous progress is too large.");
      requireValid(data.progress == null || typeof data.progress === "object" && JSON.stringify(data.progress).length <= 12000, "Progress is too large.");
    } else {
      requireValid(Array.isArray(data.candidates) && data.candidates.length >= 1 && data.candidates.length <= 3, "Provide 1 to 3 validated actions.");
      for (const candidate of data.candidates) requireValid(candidate && textWithin(candidate.id, 100) && textWithin(candidate.title, 160) && textWithin(candidate.body, 1000) && Array.isArray(candidate.steps) && candidate.steps.length >= 1 && candidate.steps.length <= 5 && candidate.steps.every(step => textWithin(step, 1500, 0)), "Invalid coaching candidate.");
      requireValid(textWithin(data.question ?? "", 300, 0), "Keep questions under 300 characters.");
      requireValid(Array.isArray(data.events ?? []) && (data.events ?? []).length <= 8 && JSON.stringify(data.events ?? []).length <= 12000, "Too many recent actions.");
    }
  }
  function validateActions(actions, data) {
    const state = data.state;
    const buildings = new Map((state.buildings ?? []).filter(building => point(building.origin)).map(building => [point(building.origin), {...building}]));
    const deposits = new Map((state.deposits ?? []).filter(deposit => point(deposit.origin)).map(deposit => [point(deposit.origin), deposit]));
    let credit = state.credits;
    requireValid(Number.isFinite(credit), "Current credits required.");
    const trains = state.trains ?? [];
    let count = state.trainCount ?? (trains.length || 1);
    let idle = state.idleTrains ?? (trains.length ? trains.filter(train => train.phase === "Parked").length : Number((state.trainPhase ?? "Parked") === "Parked"));
    const ids = new Set();
    const chargeRoute = route => {
      if (!route) return;
      requireValid(route.possible && Number.isFinite(route.cost) && route.cost >= 0, "Known route is blocked or its cost is invalid.");
      credit -= route.cost;
    };
    const footprint = (origin, size) => {
      requireValid(Number.isInteger(size) && size >= 1 && size <= 3, "Invalid building footprint.");
      const cells = new Set();
      for (let offsetX = 0; offsetX < size; offsetX++) for (let offsetY = 0; offsetY < size; offsetY++) {
        const cell = point({x:origin.x + offsetX, y:origin.y + offsetY});
        requireValid(cell, "Building footprint is out of bounds."); cells.add(cell);
      }
      return cells;
    };
    for (const [index, action] of actions.entries()) {
      requireValid(!ids.has(action.id), "Plan action IDs must be unique."); ids.add(action.id);
      const kind = action.type, location = point(action);
      const destination = point({x:action.targetX, y:action.targetY});
      requireValid((action.targetX === null && action.targetY === null) || destination, "Invalid destination coordinates.");
      requireValid(action.trainIndex === null || action.trainIndex < count, "Invalid locomotive selection.");
      requireValid(!["stop", "explore", "auto_explore"].includes(kind) || index === actions.length - 1, "Stop or exploration must end a batch before replanning.");
      if (kind === "build_extractor") {
        const deposit = deposits.get(location);
        requireValid(deposit && !buildings.has(location) && deposit.buildable !== false, "Extractor must target a revealed unused deposit.");
        requireValid(Number.isInteger(deposit.cost) && deposit.cost >= 0, "Known extractor cost required.");
        credit -= deposit.cost;
        buildings.set(location, {kind:"Extractor", resource:deposit.resource ?? "Ore", size:deposit.size ?? 1, origin:{x:action.x, y:action.y}, port:{x:action.x, y:action.y - 1}, connected:false, railConnected:false, served:false});
      }
      if (["build_solar", "build_plant"].includes(kind)) {
        const solar = kind === "build_solar", existing = buildings.get(location);
        if (solar && existing?.kind === "Solar") {
          if (!existing.connected) { chargeRoute(existing.powerRoute); existing.connected = true; }
        } else {
          const site = solar ? state.solarSite : state.plantSite;
          requireValid(!existing && [point(site), point(data.selectedTile)].includes(location), "Construction needs a known site or selected tile.");
          requireValid(action.y > 0, "Building south port is out of bounds.");
          const cells = footprint(action, 2);
          for (const deposit of deposits.values()) requireValid(![...footprint(deposit.origin, deposit.size ?? 1)].some(cell => cells.has(cell)), "Keep known resource deposits free for extractors.");
          for (const building of buildings.values()) requireValid(![...footprint(building.origin, building.size ?? (building.kind === "Extractor" ? 1 : 2))].some(cell => cells.has(cell)), "Construction overlaps a known building.");
          credit -= solar ? 100 : (state.plantCost ?? 250);
          if (solar) chargeRoute(location === point(state.solarSite) ? state.solarSitePowerRoute : state.pickedSitePowerRoute);
          buildings.set(location, {kind:solar ? "Solar" : "PowerPlant", size:2, origin:{x:action.x, y:action.y}, port:{x:action.x, y:action.y - 1}, connected:solar, railConnected:false});
        }
      }
      if (["connect_conduit", "connect_rail", "dispatch_train", "pause_mine", "resume_mine"].includes(kind)) {
        const building = buildings.get(location) ?? [...buildings.values()].find(candidate => point(candidate.port) === location);
        requireValid(building, "Action must target an existing or earlier planned building.");
        if (kind.startsWith("connect_")) {
          const power = kind === "connect_conduit", field = power ? "connected" : "railConnected";
          if (!building[field]) chargeRoute(building[power ? "powerRoute" : "railRoute"]);
          building[field] = true;
        } else {
          requireValid(building.kind === "Extractor", "Action needs an extractor.");
          if (kind === "dispatch_train") {
            requireValid(!building.served && idle >= 1, "Dispatch needs an unserved mine and idle locomotive.");
            requireValid(building.connected && building.railConnected, "Connect mine power and depot rails before dispatch.");
            if (building.resource === "Fluxite") {
              const plant = buildings.get(destination);
              requireValid(plant?.kind === "PowerPlant" && plant.connected && plant.railConnected, "Fluxite needs a connected, rail-linked plant destination.");
            } else requireValid(destination === null, "Ore destination is the colony automatically.");
            idle--; building.served = true;
          }
        }
      }
      if (kind === "buy_train") {
        requireValid(count < Math.min(4, state.maxTrains ?? 4), "Fleet limit reached.");
        count++; idle++; credit -= state.trainCost ?? 150;
      }
      requireValid(Number.isFinite(credit) && credit >= 0, "Plan exceeds current credits; wait for income and replan.");
    }
  }
  function parseResult(value, data, planning) {
    requireValid(matches(value, planning ? contract.planSchema : contract.adviceSchema), "OpenAI returned an invalid game response. Try again.");
    if (!planning) {
      requireValid(data.candidates.some(candidate => candidate.id === value.actionId) && [value.title, value.body, value.observation].every(text => text.length > 0), "Advice did not match the current actions.");
      return value;
    }
    requireValid((value.status === "ready") === (value.actions.length > 0), "Only ready plans contain actions.");
    validateActions(value.actions, data);
    return {planId:crypto.randomUUID(), ...value};
  }
  function visibleOre(state) {
    return new Map([...(state.deposits ?? []).filter(deposit => (deposit.resource ?? "Ore") === "Ore"), ...(state.buildings ?? []).filter(building => building.kind === "Extractor" && (building.resource ?? "Ore") === "Ore")].filter(item => point(item.origin)).map(item => [point(item.origin), item.origin]));
  }
  function prepare(data) {
    const now = Date.now(), key = JSON.stringify([data.state.session, data.goal.trim()]);
    for (const [entry, record] of goals) if (now - record.updated > 600000) goals.delete(entry);
    const record = goals.get(key) ?? {initial:visibleOre(data.state), batches:[]};
    record.updated = now; goals.delete(key); goals.set(key, record);
    while (goals.size > 4) goals.delete(goals.keys().next().value);
    return {...data, serverProgress:{goal:data.goal.trim(), proposedBatches:record.batches, initialVisibleOreOrigins:[...record.initial.values()], newVisibleOreOrigins:[...visibleOre(data.state)].filter(([origin]) => !record.initial.has(origin)).map(([, origin]) => origin)}};
  }
  function remember(data, plan) {
    const record = goals.get(JSON.stringify([data.state.session, data.goal.trim()]));
    if (record) record.batches = [...record.batches, {planId:plan.planId, status:plan.status, summary:plan.summary, actions:plan.actions}].slice(-4);
  }
  window.astraBotPlanning = Object.freeze({validateInput, parseResult, prepare, remember, clear:() => goals.clear()});
})();
