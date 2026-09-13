(function (root) {
  "use strict";
  const coord = p => `(${p.x}, ${p.y})`;
  const same = (a, b) => a && b && a.x === b.x && a.y === b.y;
  const tip = (id, title, body, steps, target = null) => ({ id, title, body, steps, target });
  const isFuel = b => b.resource === "Fluxite";
  const resourceName = b => isFuel(b) ? "Fluxite" : "ore";
  function fleetState(s) {
    const trains = s.trains?.length ? s.trains : [{index:0,phase:s.trainPhase || "Parked",resource:"Ore",parkRequested:!!s.trainParkRequested,capacity:s.capacity,capacityLevel:s.capacityLevel}];
    const count = s.trainCount ?? trains.length, max = s.maxTrains ?? 4, cost = s.trainCost ?? 150;
    return {trains, count, max, cost, idle:s.idleTrains ?? trains.filter(t => t.phase === "Parked").length,
      canBuy:s.canBuyTrain ?? (count < max && s.credits >= cost)};
  }
  function assignTip(s, b, fleet) {
    const name = resourceName(b);
    if (fleet.idle > 0) return tip(`dispatch-${b.origin.x}-${b.origin.y}`, isFuel(b) ? "Send Fluxite to the power plant" : s.deliveries ? "Send a train to this mine" : "Send your first freight run",
      isFuel(b) ? "An idle locomotive can deliver this Fluxite to its selected power plant. Fuel generates power; it is never sold for credits." : "The rails are connected and an idle locomotive is available. It can carry stored ore to the colony for credits.",
      ["Press 1 for Explore and select this extractor.", ...(isFuel(b) ? ["Check the plant destination in its sidebar."] : []), "Click Dispatch idle train in the right sidebar.", isFuel(b) ? "Watch the plant's fuel storage; connected, unpaused plants burn fuel as the battery needs energy." : "Deliveries sell automatically for 8 credits per ore."], b.origin);
    const parking = fleet.trains.find(t => t.parkRequested);
    if (parking) return tip("parking", "A locomotive is returning to the depot",
      parking.resource === "Fluxite" ? "This train must finish its fuel delivery before parking. A full plant must consume fuel to make room." : "The train finishes its cargo delivery, then returns to the colony depot.",
      ["Wait until a locomotive is parked and idle.", "Select this extractor and click Dispatch idle train to assign the idle locomotive."], s.colonyPort);
    if (fleet.canBuy) return tip(`buy-train-${b.origin.x}-${b.origin.y}`, "Add a locomotive for this mine",
      `Every locomotive is assigned. Another costs ${fleet.cost} credits; the fleet allows ${fleet.max}. Existing services can keep running.`,
      ["Open Fleet on the bottom toolbar.", `Click Buy train / ${fleet.cost} cr.`, "Select this extractor and click Dispatch idle train; check the plant destination for Fluxite."], b.origin);
    return tip(`switch-mine-${b.origin.x}-${b.origin.y}`, "Free a locomotive for this mine",
      fleet.count >= fleet.max ? `All ${fleet.max} locomotives are assigned. Park a service before moving it to this mine; a capacity upgrade does not add a locomotive.` : `No locomotive is idle. Another costs ${fleet.cost} credits; keep ore deliveries earning or park an existing service.`,
      ["Open Fleet and select the locomotive you want to reassign.", "Click Park at colony, then wait for it to finish delivering and return to the colony.", "Select this extractor and click Dispatch idle train; full mine storage does not prevent dispatch."], b.origin);
  }
  function routeTip(s, b, rail) {
    const route = rail ? b.railRoute : b.powerRoute;
    const noun = rail ? "rail" : "conduit";
    if (!route || !route.possible) return tip(`${noun}-blocked-${b.origin.x}-${b.origin.y}`, `Check this ${noun} route`,
      route?.reason || "The two ports need an explored, clear route.",
      route?.reason?.includes("credits")
        ? ["Keep any working train service running to earn the missing credits.", "If no paying route can be completed, Restart gives you a fresh colony. There are no refunds in this build."]
        : ["Press 1 for Explore and uncover the ground between the ports.", "Keep buildings off the route. Existing network tiles can be reused for free."], b.port);
    const segment = route.nextSegment || 0;
    const started = s.routeStarted && same(s.routeStart, route.stops[segment]);
    const first = segment + 1, second = first + 1;
    const destination = second === route.stops.length ? `${b.kind === "Solar" ? "solar array" : b.kind === "PowerPlant" ? "power plant" : "extractor"}'s outlined port tile` : "highlighted corner tile";
    const steps = started
      ? [`Click marker ${second}, the ${destination}, to build this segment.`, "Right-click or Escape cancels the current placement without spending credits."]
      : [`Choose ${rail ? "5 · Rail" : "4 · Conduit"} on the bottom toolbar, or click Show connection.`,
         `Click marker ${first}, ${first === 1 ? "the small outlined colony port tile beside the train" : "the highlighted start tile"}.`,
         `Click marker ${second}, the ${destination}, to build this segment.`];
    if (second < route.stops.length) steps.push("At each corner, click it again to start the next segment, then follow the next numbered marker.");
    steps.push(rail ? b.kind === "PowerPlant" ? "Select the Fluxite extractor, choose this plant as its destination, then Dispatch an idle locomotive." : isFuel(b) ? "Fluxite also needs rails to a selected power plant; it is not sold at the colony." : "When the rails join both ports, select the extractor and Dispatch an idle locomotive." : "Look for POWER CONNECTED and cyan socket lights to confirm the link.");
    return { ...tip(`${noun}-${b.origin.x}-${b.origin.y}`, started ? `Now click marker ${second}` : rail ? "Link the two ports with rails" : "Connect the highlighted ports",
      `${rail ? b.kind === "PowerPlant" ? "Connect this plant to the rail network so Fluxite can reach it." : isFuel(b) ? "Connect this Fluxite extractor to the colony depot rails, then its plant destination." : "Link the ports with tracks so the train can collect ore." : b.kind === "Solar" ? "Connect this solar array’s outlined port tile to the colony." : b.kind === "PowerPlant" ? "Connect this plant to the colony power grid; it also needs delivered Fluxite to generate power." : "The extractor is placed. Connect its outlined port tile to the colony to power it."} The translucent path is a suggestion. Remaining route: ${route.cost} credits.`,
      steps, route.stops[started ? segment + 1 : segment]), link:{tool:rail ? "Rail" : "Conduit", origin:b.origin} };
  }
  function advise(s) {
    if (!s) return [];
    if (s.paused) return [tip("resume", "Pick up where you left off", "Your colony is paused. Buildings and vehicles will continue when you resume.", ["Click the game, then press Space, or use Resume in the top bar."])];
    const mines = (s.buildings || []).filter(b => b.kind === "Extractor");
    const oreMines = mines.filter(b => !isFuel(b));
    const plants = (s.buildings || []).filter(b => b.kind === "PowerPlant");
    const fleet = fleetState(s);
    const selected = mines.find(b => same(b.origin, s.selected));
    const ordered = selected ? [selected, ...mines.filter(b => b !== selected)] : mines;
    if (s.routeStarted && !s.placementReason) {
      const rail = s.tool === "Rail";
      const planned = [...ordered, ...(s.buildings || []).filter(b => b.kind === "Solar" || b.kind === "PowerPlant")].find(b => {
        const route = rail ? b.railRoute : b.powerRoute;
        return route?.possible && same(s.routeStart, route.stops[route.nextSegment || 0]);
      });
      if (planned) return [routeTip(s, planned, rail)];
    }
    if (s.routeStarted) return [tip(`route-preview-${s.tool}-${s.routeStart?.x}-${s.routeStart?.y}`, "Finish this segment",
      s.placementReason || `Your ${s.tool.toLowerCase()} starts at ${coord(s.routeStart)}.`,
      ["Move the pointer to the destination port or a clear corner.", "Press R to change the bend; click to confirm a valid preview.", "Right-click or Escape cancels without spending credits."], s.routeStart)];
    if (s.battery < 20 && s.demand > s.generation) {
      const active = ordered.find(b => b.connected && !b.paused && b.stock < b.storage);
      if (active) return [tip(`power-low-${active.origin.x}-${active.origin.y}`, "Let the battery recover",
        `Mining is using ${s.demand.toFixed(1)} power/s while the grid currently supplies ${s.generation.toFixed(1)}/s.`,
        ["Press 1 and select the extractor.", "Click Pause Mine in its sidebar. Connected solar keeps charging while the game runs.", "Add connected solar, or supply a connected power plant with Fluxite, before resuming all mines."], active.origin)];
    }
    for (const b of (s.buildings || []).filter(b => (b.kind === "Solar" || b.kind === "PowerPlant") && !b.connected)) return [routeTip(s, b, false)];
    const selectedPlant = plants.find(p => same(p.origin, s.selected));
    if (selectedPlant) {
      if (!selectedPlant.railConnected) return [routeTip(s, selectedPlant, true)];
      if (selectedPlant.paused) return [tip("resume-plant", "Let this plant generate power", "This plant is paused. It can burn delivered Fluxite when the shared battery needs energy.", ["Click Resume plant in its sidebar.", "Keep a Fluxite service delivering fuel to this plant."], selectedPlant.origin)];
      if ((selectedPlant.stock > 0 || selectedPlant.burnEnergy > 0) && s.battery >= 99) return [tip("plant-battery-full", "The plant is saving its fuel", "The battery is charged, so this plant has no need to burn more Fluxite. Stored fuel and unspent burn energy are retained.", ["Keep the plant connected and unpaused.", "It generates automatically as mining or rover movement uses battery energy."], selectedPlant.origin)];
      const source = ordered.find(b => isFuel(b) && same(b.destination, selectedPlant.origin));
      if (source) { ordered.splice(ordered.indexOf(source), 1); ordered.unshift(source); }
      if (!source && oreMines.length > 0 && selectedPlant.stock === 0 && !(selectedPlant.burnEnergy > 0)) {
        const deposit = (s.deposits || []).find(d => isFuel(d) && d.buildable && s.credits >= d.cost);
        if (deposit) return [tip(`fuel-extractor-${deposit.origin.x}-${deposit.origin.y}`, "Mine fuel for this plant", `This revealed Fluxite patch needs a ${deposit.cost}-credit extractor. Fluxite fuels the plant; it is never sold.`, ["Press 2 for Extractor.", `Build on the Fluxite patch at ${coord(deposit.origin)}.`, "Connect power, depot rails and this plant's rails, then choose this plant and Dispatch idle train."], deposit.origin)];
        return [tip("find-fuel", "This plant needs Fluxite", "A power plant cannot generate on its own. A Fluxite extractor and a train service must supply its fuel.", ["Use the rover to reveal a green Fluxite patch.", "Keep ore services earning while you fund a fuel extractor and its rail connections."], s.frontier)];
      }
    }
    for (const b of ordered) {
      if (!b.connected) return [routeTip(s, b, false)];
      if (b.paused && s.battery >= 35) return [tip(`resume-mine-${b.origin.x}-${b.origin.y}`, "This mine is ready to work", "Power is available, but this extractor is paused.", ["Press 1 and select this extractor.", "Click Resume Mine in the sidebar."], b.origin)];
      if (!b.railConnected) return [routeTip(s, b, true)];
      if (isFuel(b) && !b.served) {
        const destination = b.destination || s.fuelDestination;
        const plant = plants.find(p => same(p.origin, destination));
        if (!plant) {
          if (plants.length) return [tip(`choose-plant-${b.origin.x}-${b.origin.y}`, "Choose a fuel destination", "Fluxite must go to a power plant. Delivering it never earns credits.",
            ["Press 1 and select this Fluxite extractor.", "Use the plant destination button in its sidebar to choose a power plant.", "Connect rails to that plant before dispatching."], b.origin)];
          if (s.plantSite && s.credits >= (s.plantCost ?? 250)) return [tip("build-plant", "Give Fluxite a destination", `A 2 × 2 power plant costs ${s.plantCost ?? 250} credits. Connect its power and rails, then deliver Fluxite; fuel is never sold.`,
            ["Press 6 or choose Plant on the toolbar.", `Place it on the clear footprint at ${coord(s.plantSite)}.`, "Wire its south port and link its rails before assigning this Fluxite extractor."], s.plantSite)];
          return [tip("plant-needed", "Fluxite needs a power plant", `A plant costs ${s.plantCost ?? 250} credits and needs a clear explored 2 × 2 footprint. Keep an ore service earning credits; Fluxite is fuel, not income.`,
            ["Explore space for a plant and keep its south port clear.", "Use ore deliveries to fund the plant, rails and an available locomotive."], s.frontier)];
        }
        if (!plant.connected) return [routeTip(s, plant, false)];
        if (!plant.railConnected || !b.destinationRailConnected) return [routeTip(s, {...plant, railRoute:b.destinationRailRoute || plant.railRoute}, true)];
      }
      if (!b.served && (!b.paused || b.stock > 0) && (fleet.idle > 0 || b === selected || !selected)) return [assignTip(s, b, fleet)];
    }
    if (oreMines.length === 0) {
      const deposit = (s.deposits || []).find(d => !isFuel(d) && d.buildable && s.credits >= d.cost);
      if (deposit) return [tip(`extractor-${deposit.origin.x}-${deposit.origin.y}`, "Turn this discovery into a mine",
        `This ${deposit.size} × ${deposit.size} ore patch is fully explored. An extractor costs ${deposit.cost} credits and needs a conduit connection before it can produce ore.`,
        ["Press 2 or choose Extractor on the toolbar.", `Click the ore patch at ${coord(deposit.origin)}.`, "Leave its south port clear for power and rail connections."], deposit.origin)];
      const blocked = (s.deposits || []).find(d => !isFuel(d));
      if (blocked && s.credits < blocked.cost) return [tip("extractor-unaffordable", "Save enough for your first mine",
        `This extractor needs ${blocked.cost} credits; you have ${s.credits}. Only delivered ore earns money.`,
        ["Keep a working train running if you have a producing mine.", "If the starting budget was spent before a paying route could be built, use Restart. This build has no refunds."], blocked.origin)];
      if (blocked) return [tip(`mine-blocked-${blocked.origin.x}-${blocked.origin.y}`, "Clear the way for an extractor", blocked.reason,
        blocked.reason.includes("occupied") ? ["Press 1 and move the rover away from the ore patch.", "Try Extractor again when the footprint is clear."] : ["Check the placement message above the toolbar.", "Explore the patch and its south port; an extractor needs the full footprint."], blocked.origin)];
      if (s.roverMoving) return [tip("exploring", "Your rover is opening the frontier", "The rover reveals nearby ground as it travels. Let it reach the edge of the fog.", ["Watch for an orange ore patch to appear.", "Press V any time to centre the rover."], s.rover)];
      return [tip("explore", "Let's find your first ore patch", "I'm Pip, your colony copilot. Start with a short trip to the edge of the explored ground.",
        ["Press 1 for Explore.", "Click clear ground near the edge of the fog to move the rover.", "Orange ore appears when the rover gets close enough."], s.frontier)];
    }
    const waiting = fleet.trains.find(t => t.waitingForFuelSpace);
    if (waiting) {
      const plant = plants.find(p => same(p.origin, waiting.destination));
      return [tip(`fuel-unloading-${waiting.index}`, "Make room for the arriving fuel", "The fuel train is waiting because its plant storage is full. Extra cargo capacity will not clear this stop.",
        ["Select the destination power plant and check that it is connected and unpaused.", "Let mining use battery energy; the plant burns fuel only when the battery needs charging.", "The train unloads the remaining Fluxite as storage space becomes available."], plant?.origin || waiting.destination)];
    }
    const parking = fleet.trains.find(t => t.parkRequested);
    if (parking) return [tip("parking", "A locomotive is returning to the depot", parking.resource === "Fluxite" ? "Fuel goes to its plant before the train returns to the colony. A full plant can delay unloading." : "The train finishes its ore delivery, then parks at the colony.", ["Wait for the locomotive to park.", "Select an unassigned, rail-connected extractor and Dispatch the idle locomotive."], s.colonyPort)];
    const earningTrain = fleet.trains.find(t => t.resource !== "Fluxite" && t.phase !== "Parked");
    if (s.deliveries === 0 && earningTrain) return [tip("first-delivery", "Your railway is working", `The ore train is ${{ToMine:"travelling to the mine",Loading:"loading ore",ToColony:"returning to the colony",Unloading:"unloading ore",ReturningToDepot:"returning to the depot"}[earningTrain.phase] || "running"}. It earns credits when cargo unloads at the colony.`, ["Let the train complete its trip; the service repeats automatically.", "Watch ore delivered and credits in the top bar."], s.colonyPort)];
    const options = [];
    if (s.generation <= s.demand && s.solarSite && s.credits >= 100) options.push(tip("expand-power", "Make room for more power", "Another connected solar array adds 2 power/s and gives your mines room to grow.", ["Press 3 for Solar and use this clear 2 × 2 footprint.", "Spend 100 credits to place it, then wire its south port with Conduit."], s.solarSite));
    const upgrade = fleet.trains.find(t => t.index === s.selectedTrainIndex && t.capacityLevel < 3) || fleet.trains.find(t => t.capacityLevel < 3 && t.resource !== "Fluxite");
    if (upgrade && s.credits >= upgrade.capacityLevel * 100) options.push(tip("upgrade-train", "Carry more on each trip", `Locomotive ${upgrade.index + 1} holds ${upgrade.capacity} cargo. Another 4 slots cost ${upgrade.capacityLevel * 100} credits; this upgrades that train only.`, ["Open Fleet on the bottom toolbar.", `Select locomotive ${upgrade.index + 1}, then choose Capacity +4 / ${upgrade.capacityLevel * 100} cr.`]));
    const nextDeposit = (s.deposits || []).find(d => !isFuel(d) && d.buildable && s.credits >= d.cost) || (s.deposits || []).find(d => d.buildable && s.credits >= d.cost);
    if (nextDeposit) options.unshift(tip(`expand-mine-${nextDeposit.origin.x}-${nextDeposit.origin.y}`, isFuel(nextDeposit) ? "You found Fluxite fuel" : "You found another ore patch",
      isFuel(nextDeposit) ? `An extractor costs ${nextDeposit.cost} credits. Fluxite must be hauled to a connected power plant; it generates power and is never sold.` : `An extractor here costs ${nextDeposit.cost} credits. It needs power, rails and an idle locomotive. Buy another in Fleet when needed, up to ${fleet.max} total.`,
      ["Press 2 for Extractor.", `Click the revealed ${resourceName(nextDeposit)} patch at ${coord(nextDeposit.origin)}.`, isFuel(nextDeposit) ? "Connect power, rails to the colony depot and a plant, then assign an idle locomotive to that plant." : "Connect power first, then rails; keep the current freight services earning while you build."], nextDeposit.origin));
    options.push(tip("expand-frontier", s.deliveries > 0 ? "Your first route is paying off" : "Keep the colony moving",
      s.deliveries > 0 ? `${s.sold} ore delivered over ${s.deliveries} trips. The train keeps running while you explore.` : "Your colony is set up. Explore more ground to find the next opportunity.",
      ["Press 1 and send the rover to the edge of the fog.", "Larger deposits need more credits, space, power, and their own rail connection."], s.frontier));
    return options.slice(0, 3);
  }
  function signature(s, options) {
    // Deliveries must not erase an answer while the player reads it. Candidate changes
    // still invalidate newly affordable actions; purchases and link edits remain strategic.
    return JSON.stringify([s.session, s.paused, s.tool, s.routeStarted, s.routeStart?.x, s.routeStart?.y, s.selected?.x, s.selected?.y,
      s.trainSelected, s.selectedTrainIndex, s.trainPhase === "Parked", s.trainParkRequested, s.capacityLevel, s.solarGeneration ?? s.generation,
      s.idleTrains, s.trainCount, s.canBuyTrain, s.fuelDestination?.x, s.fuelDestination?.y,
      (s.trains || []).map(t => [t.index,t.phase === "Parked",t.parkRequested,t.resource,t.capacityLevel,t.source?.x,t.source?.y,t.destination?.x,t.destination?.y,t.waitingForFuelSpace]),
      (s.buildings || []).map(b => [b.origin.x,b.origin.y,b.connected,b.railConnected,b.paused,b.level,b.served,b.resource,b.destination?.x,b.destination?.y,b.destinationRailConnected,
        b.kind === "PowerPlant" && [b.stock === 0,b.stock >= b.storage],
        ...[b.powerRoute,b.railRoute,b.destinationRailRoute].map(r => r && [r.possible,r.cost,r.nextSegment,(r.stops || []).map(p => [p.x,p.y])])]),
      options.map(o => [o.id,o.steps,o.target?.x,o.target?.y]), s.placementReason]);
  }
  const api = { advise, signature };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.AstraCoachPolicy = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
