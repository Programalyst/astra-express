(function (root) {
  "use strict";
  const coord = p => `(${p.x}, ${p.y})`;
  const same = (a, b) => a && b && a.x === b.x && a.y === b.y;
  const tip = (id, title, body, steps, target = null) => ({ id, title, body, steps, target });
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
    const destination = second === route.stops.length ? `${b.kind === "Solar" ? "solar array" : "extractor"}'s outlined port tile` : "highlighted corner tile";
    const steps = started
      ? [`Click marker ${second}, the ${destination}, to build this segment.`, "Right-click or Escape cancels the current placement without spending credits."]
      : [`Choose ${rail ? "5 · Rail" : "4 · Conduit"} on the bottom toolbar, or click Show connection.`,
         `Click marker ${first}, ${first === 1 ? "the small outlined colony port tile beside the train" : "the highlighted start tile"}.`,
         `Click marker ${second}, the ${destination}, to build this segment.`];
    if (second < route.stops.length) steps.push("At each corner, click it again to start the next segment, then follow the next numbered marker.");
    steps.push(rail ? "When the rails join both ports, select the extractor and Dispatch Train." : "Look for POWER CONNECTED and cyan socket lights to confirm the link.");
    return { ...tip(`${noun}-${b.origin.x}-${b.origin.y}`, started ? `Now click marker ${second}` : rail ? "Link the two ports with rails" : "Connect the highlighted ports",
      `${rail ? "Link the ports with tracks so the train can collect ore." : (b.kind === "Solar" ? "Connect this solar array’s outlined port tile to the colony." : "The extractor is placed. Connect its outlined port tile to the colony to power it.")} The translucent path is a suggestion. Remaining route: ${route.cost} credits.`,
      steps, route.stops[started ? segment + 1 : segment]), link:{tool:rail ? "Rail" : "Conduit", origin:b.origin} };
  }
  function advise(s) {
    if (!s) return [];
    if (s.paused) return [tip("resume", "Pick up where you left off", "Your colony is paused. Buildings and vehicles will continue when you resume.", ["Click the game, then press Space, or use Resume in the top bar."])];
    const mines = (s.buildings || []).filter(b => b.kind === "Extractor");
    const selected = mines.find(b => same(b.origin, s.selected));
    const ordered = selected ? [selected, ...mines.filter(b => b !== selected)] : mines;
    if (s.routeStarted && !s.placementReason) {
      const rail = s.tool === "Rail";
      const planned = [...ordered, ...(s.buildings || []).filter(b => b.kind === "Solar")].find(b => {
        const route = rail ? b.railRoute : b.powerRoute;
        return route?.possible && same(s.routeStart, route.stops[route.nextSegment || 0]);
      });
      if (planned) return [routeTip(s, planned, rail)];
    }
    if (s.routeStarted) return [tip(`route-preview-${s.tool}-${s.routeStart?.x}-${s.routeStart?.y}`, "Finish this segment",
      s.placementReason || `Your ${s.tool.toLowerCase()} starts at ${coord(s.routeStart)}.`,
      ["Move the pointer to the destination port or a clear corner.", "Press R to change the bend; click to confirm a valid preview.", "Right-click or Escape cancels without spending credits."], s.routeStart)];
    if (s.battery < 20 && s.demand > s.generation) {
      const active = ordered.find(b => b.connected && !b.paused);
      if (active) return [tip(`power-low-${active.origin.x}-${active.origin.y}`, "Let the battery recover",
        `Mining is using ${s.demand.toFixed(1)} power/s while solar supplies ${s.generation}/s.`,
        ["Press 1 and select the extractor.", "Click Pause Mine in its sidebar. Solar keeps charging while the game runs.", "Connect another solar array before resuming all mines."], active.origin)];
    }
    for (const b of (s.buildings || []).filter(b => b.kind === "Solar" && !b.connected)) return [routeTip(s, b, false)];
    for (const b of ordered) {
      if (!b.connected) return [routeTip(s, b, false)];
      if (b.paused && s.battery >= 35) return [tip(`resume-mine-${b.origin.x}-${b.origin.y}`, "This mine is ready to work", "Power is available, but this extractor is paused.", ["Press 1 and select this extractor.", "Click Resume Mine in the sidebar."], b.origin)];
      if (!b.railConnected) return [routeTip(s, b, true)];
      if (s.trainPhase === "Parked" && !b.paused) return [tip(`dispatch-${b.origin.x}-${b.origin.y}`, s.deliveries ? "Send the train to this mine" : "Send your first freight run", "The rails are connected. Your train can turn stored ore into credits.", ["Press 1 for Explore and select this extractor.", "Click Dispatch Train in the right sidebar.", "Deliveries sell automatically for 8 credits per ore."], b.origin)];
      if (b === selected && !b.served && !s.trainParkRequested && s.trainPhase !== "Parked") return [tip(`switch-mine-${b.origin.x}-${b.origin.y}`, "Move the train to this mine",
        "This mine has power and rails, but your one train serves another mine. Park it before assigning a new source; the previous mine will keep its ore in storage.",
        ["Click Park to switch mine in this extractor's sidebar.", "Wait for the train to finish its delivery and park at the colony.", "Select this extractor and click Dispatch Train."], b.origin)];
    }
    if (mines.length === 0) {
      const deposit = (s.deposits || []).find(d => d.buildable && s.credits >= d.cost);
      if (deposit) return [tip(`extractor-${deposit.origin.x}-${deposit.origin.y}`, "Turn this discovery into a mine",
        `This ${deposit.size} × ${deposit.size} ore patch is fully explored. An extractor costs ${deposit.cost} credits and needs a conduit connection before it can produce ore.`,
        ["Press 2 or choose Extractor on the toolbar.", `Click the ore patch at ${coord(deposit.origin)}.`, "Leave its south port clear for power and rail connections."], deposit.origin)];
      const blocked = (s.deposits || [])[0];
      if (blocked && s.credits < blocked.cost) return [tip("extractor-unaffordable", "Save enough for your first mine",
        `This extractor needs ${blocked.cost} credits; you have ${s.credits}. Only delivered ore earns money.`,
        ["Keep a working train running if you have a producing mine.", "If the starting budget was spent before a paying route could be built, use Restart. This build has no refunds."], blocked.origin)];
      if (blocked) return [tip(`mine-blocked-${blocked.origin.x}-${blocked.origin.y}`, "Clear the way for an extractor", blocked.reason,
        blocked.reason.includes("occupied") ? ["Press 1 and move the rover away from the ore patch.", "Try Extractor again when the footprint is clear."] : ["Check the placement message above the toolbar.", "Explore the patch and its south port; an extractor needs the full footprint."], blocked.origin)];
      if (s.roverMoving) return [tip("exploring", "Your rover is opening the frontier", "The rover reveals nearby ground as it travels. Let it reach the edge of the fog.", ["Watch for an orange ore patch to appear.", "Press V any time to centre the rover."], s.rover)];
      return [tip("explore", "Let's find your first ore patch", "I'm Pip, your colony copilot. Start with a short trip to the edge of the explored ground.",
        ["Press 1 for Explore.", "Click clear ground near the edge of the fog to move the rover.", "Orange ore appears when the rover gets close enough."], s.frontier)];
    }
    if (s.trainParkRequested) return [tip("parking", "Your cargo is coming home", "The train will sell what it carries, then park at the colony.", ["Wait for the train to park.", "To choose another source, select a rail-connected extractor and Dispatch Train."], s.colonyPort)];
    if (s.deliveries === 0 && s.trainPhase !== "Parked") return [tip("first-delivery", "Your railway is working", `The train is ${{ToMine:"travelling to the mine",Loading:"loading ore",ToColony:"returning to the colony"}[s.trainPhase] || "running"}. It earns credits when the cargo reaches the colony.`, ["Let the train complete its trip; it repeats automatically.", "Watch ore delivered and credits in the top bar."], s.colonyPort)];
    const options = [];
    if (s.generation <= s.demand && s.solarSite && s.credits >= 100) options.push(tip("expand-power", "Make room for more power", "Another connected solar array adds 2 power/s and gives your mines room to grow.", ["Press 3 for Solar and use this clear 2 × 2 footprint.", "Spend 100 credits to place it, then wire its south port with Conduit."], s.solarSite));
    if (s.capacityLevel < 3 && s.credits >= s.capacityLevel * 100) options.push(tip("upgrade-train", "Carry more on each trip", `Your train holds ${s.capacity} ore. Another 4 slots cost ${s.capacityLevel * 100} credits.`, ["Open Train on the bottom toolbar.", `Choose Capacity +4 / ${s.capacityLevel * 100} cr.`]));
    const nextDeposit = (s.deposits || []).find(d => d.buildable && s.credits >= d.cost);
    if (nextDeposit) options.unshift(tip(`expand-mine-${nextDeposit.origin.x}-${nextDeposit.origin.y}`, "You found another ore patch",
      `An extractor here costs ${nextDeposit.cost} credits. It will also need power and rails. Your single train must be parked before it can switch mines.`,
      ["Press 2 for Extractor.", `Click the revealed ore patch at ${coord(nextDeposit.origin)}.`, "Connect its power first, then its rails; keep the current freight run earning while you build."], nextDeposit.origin));
    options.push(tip("expand-frontier", s.deliveries > 0 ? "Your first route is paying off" : "Keep the colony moving",
      s.deliveries > 0 ? `${s.sold} ore delivered over ${s.deliveries} trips. The train keeps running while you explore.` : "Your colony is set up. Explore more ground to find the next opportunity.",
      ["Press 1 and send the rover to the edge of the fog.", "Larger deposits need more credits, space, power, and their own rail connection."], s.frontier));
    return options.slice(0, 3);
  }
  function signature(s, options) {
    // Deliveries must not erase an answer while the player reads it. Candidate changes
    // still invalidate newly affordable actions; purchases and link edits remain strategic.
    return JSON.stringify([s.session, s.paused, s.tool, s.routeStarted, s.routeStart?.x, s.routeStart?.y, s.selected?.x, s.selected?.y,
      s.trainPhase === "Parked", s.trainParkRequested, s.capacityLevel, s.generation,
      (s.buildings || []).map(b => [b.origin.x,b.origin.y,b.connected,b.railConnected,b.paused,b.level,b.served,
        ...[b.powerRoute,b.railRoute].map(r => r && [r.possible,r.cost,r.nextSegment,(r.stops || []).map(p => [p.x,p.y])])]),
      options.map(o => [o.id,o.steps,o.target?.x,o.target?.y]), s.placementReason]);
  }
  const api = { advise, signature };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.AstraCoachPolicy = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
