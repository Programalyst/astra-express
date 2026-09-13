(() => {
  "use strict";
  const panel = document.createElement("section");
  panel.id = "astrabot-task"; panel.hidden = true; panel.setAttribute("aria-label", "AstraBot task plan");
  panel.innerHTML = `<header><div><strong>AstraBot</strong><small>PLAN & PLAY</small></div><button id="bot-close" aria-label="Close task panel">×</button></header>
    <div id="bot-editor"><p class="bot-intro">Choose a tile, describe your goal, then let AstraBot work through it.</p>
    <form id="bot-goal-form"><textarea id="bot-goal" maxlength="600" rows="3" aria-label="Goal for AstraBot" placeholder="Connect this mine to power and rails, then dispatch a train…"></textarea>
    <div class="bot-target-row"><button type="button" id="bot-pick">⌖ Pick a tile</button><span id="bot-tile">Whole colony</span><button type="button" id="bot-clear" aria-label="Clear selected tile" hidden>×</button></div>
    <div class="bot-presets"><button type="button" data-goal="Explore with the rover, find ore, build an extractor, connect power and rails, and dispatch a train. Complete the first ore delivery.">First ore route</button><button type="button" data-goal="Send the rover to discover another ore deposit. Stop once a new ore deposit is fully revealed.">Discover ore</button><button type="button" data-goal="Expand the base to four working ore train routes from the colony depot. Explore for ore, manage power and budget, and buy locomotives as needed. Keep existing routes earning.">Four rail routes</button></div>
    <button id="bot-plan" type="submit">Create plan</button></form></div>
    <div id="bot-plan-body"><h3 id="bot-title">Your next colony project</h3><p id="bot-summary">Plans use your current game screen and discovered terrain.</p><ol id="bot-actions"></ol><p id="bot-check"></p></div>
    <p id="bot-status" role="status">You stay in control. Stop or Escape ends the takeover.</p>
    <footer><button id="bot-start" hidden>Start plan</button><button id="bot-stop" hidden>Stop</button><button id="bot-expand" hidden>Show plan</button></footer>
    <small class="bot-scope">Game controls only · 1 rover · 1 depot · up to 4 trains</small>`;
  document.body.append(panel);
  const el = id => document.getElementById(id);
  let game, state, stateAt = 0, plan = null, goal = "", results = [], session = "", running = false, planning = false;
  let controller, capturePending, actionPending, generation = 0, sequence = 0, batches = 0, startedAt = 0, pickPending = false;
  let checkpoint = null, enabled = true;
  const send = (method, value) => game?.SendMessage("Astra Express", method, value);
  const message = value => { el("bot-status").textContent = value; };
  function block() { send("CoachSetInputBlocked", !panel.hidden && (panel.matches(":hover") || panel.contains(document.activeElement)) ? "1" : "0"); }
  panel.addEventListener("pointerenter", block); panel.addEventListener("pointerleave", block);
  panel.addEventListener("focusin", block); panel.addEventListener("focusout", () => queueMicrotask(block));
  for (const name of ["keydown", "keyup", "pointerdown", "pointerup", "mousedown", "mouseup", "wheel"]) panel.addEventListener(name, e => e.stopPropagation());
  function show(compact = false) {
    panel.hidden = false; panel.classList.toggle("compact", compact);
    el("bot-expand").hidden = !running; el("bot-expand").textContent = compact ? "Show plan" : "Minimize";
    block();
  }
  function cancelPending() {
    controller?.abort(); controller = null;
    if (capturePending) { clearTimeout(capturePending.timer); capturePending.reject(new Error("Cancelled")); capturePending = null; }
    if (actionPending) { clearTimeout(actionPending.timer); actionPending.reject(new Error("Cancelled")); actionPending = null; }
  }
  function stop(reason = "Stopped. Completed work is kept.") {
    const hadControl = running || pickPending || !!state?.botBusy;
    generation++; running = planning = false; cancelPending();
    if (hadControl) send("CoachBotStop", "user");
    panel.classList.remove("compact"); el("bot-stop").hidden = true; el("bot-expand").hidden = true;
    el("bot-editor").hidden = false; el("bot-plan").disabled = false; el("bot-start").hidden = true;
    message(reason); block();
  }
  function close() {
    if (running || planning) stop();
    pickPending = false; send("CoachPickTile", "0"); panel.hidden = true;
    el("unity-canvas").focus(); send("CoachSetInputBlocked", "2");
  }
  el("bot-close").onclick = close;
  el("bot-stop").onclick = () => stop();
  el("bot-expand").onclick = () => show(!panel.classList.contains("compact"));
  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && (running || planning || pickPending || !panel.hidden)) {
      e.preventDefault(); e.stopImmediatePropagation();
      if (pickPending) { pickPending = false; send("CoachPickTile", "0"); show(); message("Tile selection cancelled."); }
      else if (running || planning) stop(); else close();
    }
  }, true);
  el("bot-pick").onclick = () => {
    if (!game || running || planning) return;
    pickPending = true; panel.hidden = true; send("CoachPickTile", "1");
    el("unity-canvas").focus(); send("CoachSetInputBlocked", "2");
  };
  el("bot-clear").onclick = () => { send("CoachPickTile", "clear"); plan = null; el("bot-start").hidden = true; };
  for (const button of panel.querySelectorAll("[data-goal]")) button.onclick = () => { el("bot-goal").value = button.dataset.goal; el("bot-goal").focus(); };
  function drawPlan() {
    if (!plan) return;
    el("bot-title").textContent = plan.title;
    el("bot-summary").textContent = plan.summary;
    el("bot-check").textContent = plan.nextCheck || "";
    el("bot-actions").replaceChildren(...plan.actions.map((a, index) => {
      const li = document.createElement("li"); li.id = `bot-step-${index}`; li.textContent = a.reason || a.type.replaceAll("_", " "); return li;
    }));
    el("bot-start").hidden = running || plan.status !== "ready" || !plan.actions.length;
  }
  function capture() {
    return new Promise((resolve, reject) => {
      const id = `bot-frame-${++sequence}`;
      capturePending = { id, resolve, reject, timer:setTimeout(() => { capturePending = null; reject(new Error("Game screenshot timed out.")); }, 8000) };
      send("CoachCapture", id);
    });
  }
  async function requestPlan(token) {
    if (!enabled) throw new Error("AstraBot is switched off.");
    if (!state || !game || Date.now() - stateAt > 4000) throw new Error("Wait for the game to reconnect.");
    planning = true; el("bot-plan").disabled = true; el("bot-start").hidden = true; el("bot-stop").hidden = false;
    message(running ? "Checking progress and planning the next steps…" : "Reading the game screen and planning…");
    const configResponse = await fetch("/api/coach/config", { cache:"no-store" });
    if (!configResponse.ok) throw new Error("Coach server is unavailable.");
    const config = await configResponse.json();
    if (!config.configured) throw new Error("Set the server's OpenAI API key before creating a plan.");
    if (token !== generation) return null;
    const image = await capture();
    if (token !== generation) return null;
    if (!enabled || !state || state.session !== session || Date.now() - stateAt > 4000) throw new Error("Game state is stale. Reconnect before planning.");
    controller = new AbortController(); const active = controller;
    const timeout = setTimeout(() => active.abort(), 45000);
    try {
      const response = await fetch("/api/astrabot/plan", { method:"POST", signal:active.signal,
        headers:{"Content-Type":"application/json", "X-Astra-Coach":config.token},
        body:JSON.stringify({ goal, state, image, selectedTile:checkpoint?.selectedTile ?? state.pickedTile ?? null,
          previousPlan:plan ? {id:plan.planId, actions:plan.actions, results:results.slice(-60)} : results.length ? {id:"resumed",actions:[],results:results.slice(-60)} : null }) });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || "Could not create a plan.");
      if (token !== generation) return null;
      plan = data; batches++; drawPlan(); saveCheckpoint();
      message(data.status === "complete" ? "Goal complete." : data.status === "blocked" ? "AstraBot needs a change before continuing." : "Review the plan, then start when ready.");
      return data;
    } finally { clearTimeout(timeout); if (controller === active) controller = null; if (token === generation) { planning = false; el("bot-plan").disabled = false; if (!running) el("bot-stop").hidden = true; } }
  }
  function saveCheckpoint() {
    checkpoint = {session,goal,selectedTile:checkpoint?.selectedTile ?? state?.pickedTile ?? null,results:results.slice(-120),batches};
    // No images, credentials or automatic restart. This ledger accompanies later batches.
    try { sessionStorage.setItem("astra.bot.project", JSON.stringify(checkpoint)); } catch {}
  }
  el("bot-goal-form").onsubmit = async event => {
    event.preventDefault(); if (running || planning) return;
    goal = el("bot-goal").value.trim(); if (!goal) { el("bot-goal").focus(); return; }
    generation++; const token = generation; results = []; plan = null; batches = 0;
    checkpoint = {selectedTile:state?.pickedTile ?? null};
    try { await requestPlan(token); }
    catch (error) { if (token === generation) { stop(error.name === "AbortError" ? "Planning timed out. Try again." : error.message); } }
  };
  function execute(action, token) {
    return new Promise((resolve, reject) => {
      if (token !== generation) return reject(new Error("Cancelled"));
      if (!state || state.session !== session || Date.now() - stateAt > 4000) return reject(new Error("Game state is stale. Reconnect before continuing."));
      const id = `bot-${Date.now()}-${++sequence}`;
      actionPending = {id,resolve,reject,timer:setTimeout(() => { actionPending = null; send("CoachBotStop", "timeout"); reject(new Error("Action timed out; control returned to you.")); }, 75000)};
      send("CoachBotCommand", JSON.stringify({...action,id,session,x:action.x ?? -1,y:action.y ?? -1,targetX:action.targetX ?? -1,targetY:action.targetY ?? -1,seconds:action.seconds ?? 5}));
    });
  }
  el("bot-start").onclick = async () => {
    if (!plan || running || planning || plan.status !== "ready") return;
    running = true; startedAt = Date.now(); const token = ++generation;
    el("bot-editor").hidden = true; el("bot-start").hidden = true; el("bot-stop").hidden = false;
    show(true); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2");
    let failures = 0;
    try {
      while (running && token === generation) {
        if (batches >= 24 || Date.now() - startedAt > 20 * 60 * 1000) throw new Error("Checkpoint reached. Review progress and create the next plan to continue.");
        let batchFailed = false;
        for (let i = 0; i < plan.actions.length; i++) {
          if (!running || token !== generation) return;
          const action = plan.actions[i]; el(`bot-step-${i}`).dataset.state = "active";
          message(action.reason || action.type.replaceAll("_", " "));
          const result = await execute(action, token);
          if (token !== generation) return;
          results.push({action, ...result}); results = results.slice(-120); saveCheckpoint();
          el(`bot-step-${i}`).dataset.state = result.status === "complete" ? "done" : "failed";
          if (result.status !== "complete") { failures++; batchFailed = true; break; }
          if (action.type === "stop") { stop("AstraBot stopped as planned."); return; }
        }
        if (!batchFailed) failures = 0;
        if (failures >= 3) throw new Error("Repeated blocker. Review the action results before continuing.");
        const next = await requestPlan(token);
        if (!next || token !== generation) return;
        if (next.status !== "ready" || !next.actions.length) {
          running = false; el("bot-stop").hidden = true; el("bot-editor").hidden = false; show(false); el("bot-expand").hidden = true; return;
        }
      }
    } catch (error) { if (token === generation) stop(error.name === "AbortError" ? "Planning timed out. Your completed work is kept." : error.message); }
  };
  document.addEventListener("visibilitychange", () => { if (document.hidden && (running || planning)) stop("Paused takeover because the game tab was hidden. Your completed work is kept."); });
  window.astraBotControl = {
    ready(instance) { game = instance; enabled = window.astraCoach?.enabled?.() !== false; },
    setEnabled(value) { enabled = !!value; if (!enabled) { stop("AstraBot is switched off."); close(); } },
    open() { if (enabled) show(false); },
    active() { return running || planning; },
    receive(next) {
      next = {...next, pickedTile:next.hasPickedTile ? next.pickedTile : null};
      if (session && session !== next.session) { stop("New colony. Create a new plan."); plan = null; results = []; checkpoint = null; }
      session = next.session; state = next; stateAt = Date.now();
      el("bot-tile").textContent = next.pickedTile ? `Tile (${next.pickedTile.x}, ${next.pickedTile.y})` : "Whole colony";
      el("bot-clear").hidden = !next.pickedTile;
      if (pickPending && !next.pickingTile && next.hasPickedTile && next.pickedTile) { pickPending = false; plan = null; el("bot-start").hidden = true; show(); message("Tile selected. Describe what to do here."); }
      if (actionPending && next.botActionId === actionPending.id && ["complete","failed","cancelled"].includes(next.botActionStatus)) {
        const pending = actionPending; actionPending = null; clearTimeout(pending.timer);
        pending.resolve({status:next.botActionStatus,message:next.botActionMessage,credits:next.credits,deliveries:next.deliveries});
      }
    },
    screenReady(id, jpeg) {
      if (capturePending?.id !== id) return;
      const pending = capturePending; capturePending = null; clearTimeout(pending.timer);
      if (!jpeg) pending.reject(new Error("Could not capture the game.")); else pending.resolve("data:image/jpeg;base64," + jpeg);
    },
    diagnostics() { return {running,planning,batches,completed:results.filter(r => r.status === "complete").length,status:plan?.status,selectedTile:state?.pickedTile ?? null}; }
  };
})();
