(() => {
  "use strict";
  const EXPAND_MINES_GOAL = "Build two additional Ore extractors on revealed deposits and connect them to the colony's shared power grid. Add and connect enough solar arrays to cover their combined operating demand. Explore for Ore as needed. Do not add rails or trains. Stop only when both new extractors are powered, or explain the blocker.";
  const panel = document.createElement("section");
  panel.id = "astrabot-task"; panel.hidden = true; panel.setAttribute("aria-label", "AstraBot task plan");
  panel.innerHTML = `<header><div><strong>AstraBot</strong><small>PLAN & PLAY</small></div><div class="bot-window-controls"><button id="bot-minimize" aria-label="Minimize task panel">−</button><button id="bot-close" aria-label="Close task panel">×</button></div></header>
    <div id="bot-editor"><p class="bot-intro">What would you like me to do? Pick a suggestion or describe a goal.</p>
    <form id="bot-goal-form"><textarea id="bot-goal" maxlength="600" rows="3" aria-label="Goal for AstraBot" placeholder="Connect this mine to power and rails, then dispatch a train…"></textarea>
    <div class="bot-target-row"><button type="button" id="bot-pick">⌖ Pick a tile</button><span id="bot-tile">Whole colony</span><button type="button" id="bot-clear" aria-label="Clear selected tile" hidden>×</button></div>
    <div class="bot-presets"><button type="button" data-goal="Explore with the rover, find ore, build an extractor, connect power and rails, and dispatch a train. Complete the first ore delivery.">First ore route</button><button type="button" data-goal="Send the rover on automatic exploration to discover a new ore deposit. Use auto_explore and stop once a new ore deposit is fully revealed.">Discover ore</button><button type="button" data-goal="Connect the placed solar panel to the colony power grid. Use the selected tile if provided; otherwise choose the disconnected solar panel. Confirm it supplies power.">Connect solar</button><button type="button" id="bot-expand-mines" data-goal="${EXPAND_MINES_GOAL}">Expand mines + power</button></div>
    <div class="bot-presets"><button type="button" data-goal="Connect an existing Ore extractor to the colony by rail. Prefer the selected mine. Use an idle train if available; do not buy trains or reassign active or manually parked services.">Build the rails</button></div>
    <button id="bot-plan" type="submit">Create plan</button></form></div>
    <div id="bot-plan-body"><h3 id="bot-title">Your next colony project</h3><details id="bot-plan-details"><summary>Steps & details</summary><p id="bot-model" hidden></p><p id="bot-summary">Plans use your current game screen and discovered terrain.</p><ol id="bot-actions"></ol><p id="bot-check"></p></details></div>
    <div id="bot-thinking" role="status" hidden><span class="bot-thinking-face" aria-hidden="true">•ᴗ•</span><span>Thinking <span class="astra-dots" aria-hidden="true"><i></i><i></i><i></i></span></span></div><p id="bot-status" role="status">I’ll show you the plan before starting. Stop or Escape returns control.</p><p id="bot-progress" aria-label="Expansion progress" hidden></p>
    <details id="bot-activity"><summary id="bot-activity-toggle">Live activity · expand</summary><p class="bot-activity-note">Action explanations and game updates</p><ol id="bot-timeline" aria-label="AstraBot activity history"></ol></details>
    <footer><button id="bot-start" hidden>Start plan</button><button id="bot-stop" hidden>Stop</button><button id="bot-expand" hidden>Open task</button><button id="bot-edit" hidden>Edit goal</button></footer>
    <small class="bot-scope">Game controls only · 1 rover · 1 depot · 1 free train per extractor</small>`;
  document.body.append(panel);
  document.getElementById("bot-activity").open = false;
  document.getElementById("bot-activity").addEventListener("toggle", () => {
    document.getElementById("bot-activity-toggle").textContent = document.getElementById("bot-activity").open ? "Live activity · minimize" : "Live activity · expand";
  });
  const commandBar = document.createElement("form"); commandBar.id = "astrabot-commandbar";
  commandBar.setAttribute("aria-label", "Ask AstraBot to do a task");
  commandBar.innerHTML = `<label for="bot-command-input">AstraBot</label><input id="bot-command-input" maxlength="600" autocomplete="off" placeholder="Ask anything, just say the word." aria-label="Message AstraBot"><button id="bot-command-task" type="button" aria-label="Create action plan">Action</button><button id="bot-command-send" type="submit">Ask</button><span id="bot-drone-dock"></span>`;
  document.body.append(commandBar);
  const avatarHome = document.getElementById("astrabot-avatar-home");
  if (avatarHome) document.getElementById("bot-drone-dock").append(avatarHome);
  let companionDockRefreshTimer = 0;
  function sizeCommandBar() {
    const nextLong = String(el("bot-command-input").value.length > 55);
    const resizing = commandBar.dataset.long !== nextLong;
    commandBar.dataset.long = nextLong;
    clearTimeout(companionDockRefreshTimer);
    if (!resizing) updateCompanionDock();
    else companionDockRefreshTimer = setTimeout(updateCompanionDock, 220);
  }
  commandBar.addEventListener("transitionend", event => {
    if (event.propertyName === "width") updateCompanionDock();
  });
  document.getElementById("bot-command-input").addEventListener("input", sizeCommandBar);
  const replyCard = document.createElement("section"); replyCard.id = "astrabot-reply"; replyCard.hidden = true;
  replyCard.setAttribute("aria-label", "AstraBot conversation");
  replyCard.innerHTML = `<header><strong>AstraBot</strong><button id="bot-reply-close" aria-label="Dismiss reply">×</button></header><p id="bot-reply-question"></p><div id="bot-reply-text" role="status" aria-live="polite"></div><footer><span id="bot-reply-state"></span><button id="bot-reply-stop" hidden>Stop reply</button></footer>`;
  document.body.append(replyCard);
  const embodied = document.createElement("div"); embodied.id = "bot-embodied"; embodied.hidden = true;
  embodied.setAttribute("aria-hidden", "true");
  embodied.innerHTML = `<svg class="bot-fly-avatar" viewBox="0 0 86 96"><ellipse cx="43" cy="86" rx="22" ry="5" fill="#9bead5" opacity=".15"/><g class="astrabot-float"><path d="M43 22V10" stroke="#9bead5" stroke-width="3"/><circle cx="43" cy="8" r="4" fill="#ffe0a3"/><rect x="10" y="39" width="66" height="22" rx="8" fill="#edbc7f"/><rect x="16" y="22" width="54" height="50" rx="18" fill="#a7ddcd" stroke="#e5ffed" stroke-width="2"/><rect x="22" y="34" width="42" height="26" rx="10" fill="#102e3c"/><rect class="astrabot-eye" x="30" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><rect class="astrabot-eye" x="50" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><path d="M38 55q5 4 10 0M32 76l11 8 11-8" fill="none" stroke="#a7f9dc" stroke-width="3"/></g></svg><span id="bot-world-caption"></span><span class="bot-world-beam"></span>`;
  document.body.append(embodied);
  const el = id => document.getElementById(id);
  let game, state, stateAt = 0, plan = null, goal = "", results = [], session = "", running = false, planning = false;
  let controller, capturePending, actionPending, generation = 0, sequence = 0, batches = 0, startedAt = 0, pickPending = false;
  let checkpoint = null, enabled = true, editing = true, reviewReady = false, suggestedTile = null;
  let currentAction = null, activity = [], lastActivity = "";
  let quickTask = null;
  let companionMode = "", companionReturning = false;
  let chatBusy = false, chatController = null, chatVersion = 0, chatHistory = [];
  const send = (method, value) => game?.SendMessage("Astra Express", method, value);
  function updateCompanionDock() {
    const dock = el("bot-drone-dock"), canvas = el("unity-canvas");
    if (!game || !dock?.getBoundingClientRect || !canvas?.getBoundingClientRect) return;
    const d = dock.getBoundingClientRect(), c = canvas.getBoundingClientRect();
    if (!c.width || !c.height) return;
    const x = Math.round(Math.max(0,Math.min(1,(d.left+d.width/2-c.left)/c.width))*1000);
    const y = Math.round(Math.max(0,Math.min(1,1-(d.top+d.height/2-c.top)/c.height))*1000);
    send("CoachSetCompanionDock", `${x},${y}`);
  }
  function syncThinking() { el("bot-thinking").hidden = !planning; panel.dataset.thinking = String(planning); el("bot-plan").textContent = planning ? "Planning…" : "Create plan"; const commandBusy = !enabled || running || planning || chatBusy; el("bot-command-task").disabled = commandBusy; el("bot-command-send").disabled = commandBusy; el("bot-command-input").disabled = !enabled; el("bot-command-input").placeholder = !enabled ? "Turn Copilot on to ask AstraBot" : "Ask anything, just say the word."; }
  function recordActivity(text, kind = "update") {
    if (!text || text === lastActivity) return;
    lastActivity = text;
    activity.push({text:String(text).slice(0,300),kind}); activity = activity.slice(-3);
    el("bot-timeline").replaceChildren(...activity.slice().reverse().map(entry => {
      const li = document.createElement("li"); li.dataset.kind = entry.kind; li.textContent = entry.text; return li;
    }));
  }
  const message = value => { el("bot-status").textContent = value; recordActivity(value, planning ? "planning" : running ? "action" : "update"); syncThinking(); };
  function updateEmbodied() {
    const waitingInWorld = !!plan && plan.status !== "complete" && !editing;
    const mode = !enabled || document.hidden ? "off" : companionReturning ? "returning" : planning ? (running ? "thinking" : "launching") : running ? "working" : chatBusy ? "thinking" : reviewReady || waitingInWorld ? "ready" : "idle";
    if (game && companionMode !== mode) { companionMode = mode; send("CoachSetCompanionMode", mode); }
    // Current players render the companion inside Unity, with depth and lighting.
    // Keep the DOM fallback only for older players without this capability.
    if (state?.nativeCompanion) { embodied.hidden = true; return; }
    // Unity publishes only revealed targets. Screen coordinates are refreshed as
    // the player pans; the companion never moves the player's camera.
    if (!enabled || document.hidden || (!running && !planning) || !state || Date.now()-stateAt > 4000) { embodied.hidden = true; return; }
    const canvas = el("unity-canvas");
    if (!canvas?.getBoundingClientRect) return;
    const rect = canvas.getBoundingClientRect();
    const target = state.botBusy && state.botTarget ? state.botTarget : null;
    const visible = target?.visible && Number.isFinite(target.screenX) && Number.isFinite(target.screenY) && target.screenX >= 0 && target.screenX <= 1 && target.screenY >= 0 && target.screenY <= 1;
    const dock = panel.getBoundingClientRect();
    const x = visible ? rect.left + target.screenX * rect.width - 36 : dock.left + Math.min(dock.width, 300) - 65;
    const y = visible ? rect.top + target.screenY * rect.height - 112 : dock.top - 92;
    const placedX = Math.max(rect.left+8,Math.min(rect.right-235,x));
    const placedY = Math.max(rect.top+80,Math.min(rect.bottom-230,y));
    embodied.style.left = `${placedX}px`;
    embodied.style.top = `${placedY}px`;
    embodied.dataset.mode = planning ? "thinking" : "working";
    embodied.dataset.anchored = String(!!visible && placedX === x && placedY === y);
    el("bot-world-caption").textContent = planning ? "Planning next steps…" : visible || !target ? (state.botActionMessage || currentAction?.reason || "Following progress…") : "Working outside this view · activity below";
    embodied.hidden = false;
  }
  const number = (value, fallback = 0) => typeof value === "number" && Number.isFinite(value) && value >= 0 ? value : fallback;
  const pointKey = point => point && Number.isInteger(point.x) && Number.isInteger(point.y) ? `${point.x},${point.y}` : "";
  const displayNumber = value => Number.isInteger(value) ? String(value) : value.toFixed(1).replace(/\.0$/, "");
  function drawProgress() {
    const progress = plan?.goalProgress;
    const output = el("bot-progress");
    if (!progress) { output.textContent = ""; output.hidden = true; return; }
    const resource = progress.resource || "Ore";
    let current = number(progress.currentExtractorCount);
    let added = number(progress.newExtractorCount);
    let linked = number(progress.connectedTargetCount);
    let served = number(progress.servedTargetCount);
    let demand = number(progress.ratedExtractorDemand);
    let generation = number(progress.solarGeneration);
    if (Array.isArray(state?.buildings)) {
      const extractors = state.buildings.filter(building => building?.kind === "Extractor" && (building.resource || "Ore") === resource);
      const initial = new Set((progress.initialExtractorOrigins || []).map(pointKey).filter(Boolean));
      const relevant = progress.mode === "additional" ? extractors.filter(building => !initial.has(pointKey(building.origin))) : extractors;
      current = extractors.length; added = relevant.length;
      linked = relevant.filter(building => building.connected === true).length;
      served = relevant.filter(building => building.served === true).length;
      demand = state.buildings.filter(building => building?.kind === "Extractor").reduce((total, building) => total + number(building.demand, Math.max(1, number(building.size, 1))), 0);
      if (typeof state.solarGeneration === "number" && Number.isFinite(state.solarGeneration) && state.solarGeneration >= 0) generation = state.solarGeneration;
      else generation = state.buildings.filter(building => building?.kind === "Solar" && building.connected === true).reduce((total, building) => total + number(building.generation, 2), 0);
    }
    const target = progress.mode === "additional" ? number(progress.requestedAdditionalExtractors) : number(progress.targetExtractorCount);
    const built = progress.mode === "additional" ? added : current;
    output.textContent = `Mines ${built}/${target} · Linked ${linked}/${target} · Power ${displayNumber(generation)}/${displayNumber(demand)}`;
    output.dataset.state = built >= target && linked >= target && (!progress.requiresSolarCapacity || generation >= demand) && (!progress.requiresRailService || served >= target) ? "done" : "active";
    output.hidden = editing || (plan?.status === "complete" && !running && !planning);
  }
  function block() { send("CoachSetInputBlocked", (!panel.hidden && (panel.matches(":hover") || panel.contains(document.activeElement))) || (!replyCard.hidden && (replyCard.matches(":hover") || replyCard.contains(document.activeElement))) || commandBar.matches(":hover") || commandBar.contains(document.activeElement) ? "1" : "0"); }
  commandBar.addEventListener("pointerenter", block); commandBar.addEventListener("pointerleave", block);
  commandBar.addEventListener("focusin", block); commandBar.addEventListener("focusout", () => queueMicrotask(block));
  for (const name of ["keydown", "keyup", "pointerdown", "pointerup", "mousedown", "mouseup", "wheel"]) commandBar.addEventListener(name, e => e.stopPropagation());
  commandBar.onsubmit = async event => {
    event.preventDefault();
    const text = el("bot-command-input").value.trim();
    if (!text || !enabled || running || planning || chatBusy) return;
    window.dispatchEvent?.(new CustomEvent("astra:task-intent"));
    await converse(text);
  };
  el("bot-command-task").onclick = async () => {
    const text = el("bot-command-input").value.trim();
    if (!text) { el("bot-command-input").focus(); return; }
    if (!enabled || running || planning || chatBusy) return;
    window.dispatchEvent?.(new CustomEvent("astra:task-intent"));
    replyCard.hidden = true;
    el("bot-goal").value = text;
    el("bot-command-input").value = "";
    sizeCommandBar();
    await el("bot-goal-form").onsubmit({preventDefault(){}});
  };
  function stopChat() {
    chatVersion++; chatController?.abort(); chatController = null; chatBusy = false;
    el("bot-reply-stop").hidden = true; el("bot-reply-state").textContent = "Reply stopped · partial text may be incomplete";
    syncThinking(); updateEmbodied();
  }
  el("bot-reply-stop").onclick = stopChat;
  el("bot-reply-close").onclick = () => { if (chatBusy) stopChat(); replyCard.hidden = true; el("unity-canvas").focus(); send("CoachSetInputBlocked","2"); };
  replyCard.addEventListener("pointerenter",block); replyCard.addEventListener("pointerleave",block);
  replyCard.addEventListener("focusin",block); replyCard.addEventListener("focusout",()=>queueMicrotask(block));
  for (const name of ["keydown","keyup","pointerdown","pointerup","mousedown","mouseup","wheel"]) replyCard.addEventListener(name,e=>e.stopPropagation());
  async function converse(text) {
    panel.hidden = true; replyCard.hidden = false;
    el("bot-reply-question").textContent = text;
    el("bot-reply-text").textContent = ""; el("bot-reply-state").textContent = "Connecting…";
    companionReturning = false; chatBusy = true; const version = ++chatVersion;
    const active = new AbortController(); chatController = active;
    const timeout = setTimeout(() => active.abort(),65000);
    el("bot-reply-stop").hidden = false; syncThinking(); updateEmbodied();
    let answer = "", done = false, routedGoal = "";
    try {
      if (!state || Date.now()-stateAt > 4000) throw new Error("Wait for the game to reconnect.");
      const config = window.astraBotAPI ? await window.astraBotAPI.config({signal:active.signal}) : await (await fetch('/api/coach/config',{signal:active.signal})).json();
      if (!config.configured) throw new Error("Add an OpenAI key in AstraBot settings to chat.");
      let response;
      for (let waited = 0; ; ) {
        if (active.signal.aborted || version !== chatVersion) return;
        response = await (window.astraBotAPI?.request || fetch)('/api/astrabot/chat',{method:'POST',signal:active.signal,headers:{'Content-Type':'application/json','X-Astra-Coach':config.token},body:JSON.stringify({message:text,state,history:chatHistory})});
        if (response.ok) break;
        const error = await response.json();
        if (response.status !== 429 || !["AstraBot is already reading a screen","Wait a moment before asking again"].includes(error.error) || waited >= 24000) throw new Error(error.error || "Chat unavailable.");
        const delay = Math.min(4000,Math.max(250,Number(error.retryAfterMs)||4000)); waited += delay;
        el("bot-reply-state").textContent = "Finishing a background check…";
        await new Promise(resolve => {
          const finish = () => { clearTimeout(timer); active.signal.removeEventListener('abort',finish); resolve(); };
          const timer = setTimeout(finish,delay); active.signal.addEventListener('abort',finish,{once:true});
        });
      }
      const reader = response.body.getReader(), decoder = new TextDecoder(); let buffer = "";
      while (true) {
        const chunk = await reader.read();
        if (version !== chatVersion) { await reader.cancel(); return; }
        buffer += decoder.decode(chunk.value || new Uint8Array(),{stream:!chunk.done});
        if (buffer.length > 50000) throw new Error("Reply exceeded its limit.");
        const lines = buffer.split('\n'); buffer = lines.pop();
        for (const line of lines) {
          if (!line.trim()) continue;
          const event = JSON.parse(line);
          if (event.type === 'error') throw new Error(event.error);
          if (event.type === 'start') el("bot-reply-state").textContent = "AstraBot · preparing reply…";
          if (event.type === 'delta') { answer += event.text; el("bot-reply-state").textContent = "Replying…"; }
          if (event.type === 'done') {
            if (!['chat','task'].includes(event.intent) || typeof event.goal !== 'string' || (event.intent === 'task' ? !event.goal.trim() || event.goal.length > 600 : event.goal !== '')) throw new Error("Invalid reply route. Please try again.");
            answer = event.text; done = true; routedGoal = event.intent === 'task' ? event.goal : "";
            el("bot-reply-state").textContent = "AstraBot · reply complete";
          }
          if (answer.length > 12000) throw new Error("Reply exceeded its limit.");
          el("bot-reply-text").textContent = answer;
        }
        if (chunk.done) break;
      }
      if (!done) throw new Error("Reply interrupted. Please try again.");
      chatHistory = [...chatHistory,{role:'user',text},{role:'assistant',text:answer.slice(0,3000)}].slice(-8);
      if (el("bot-command-input").value.trim() === text) el("bot-command-input").value = "";
      sizeCommandBar();
    } catch (error) {
      routedGoal = "";
      if (version === chatVersion) { el("bot-reply-state").textContent = error.name === 'AbortError' ? "Reply timed out. Try again." : error.message; if (!answer) el("bot-reply-text").textContent = "I couldn’t finish that reply. Your game hasn’t been changed."; }
    } finally {
      clearTimeout(timeout);
      if (version === chatVersion) { chatBusy = false; chatController = null; companionReturning = true; el("bot-reply-stop").hidden = true; syncThinking(); updateEmbodied(); }
    }
    if (routedGoal && version === chatVersion && enabled && !running && !planning) {
      replyCard.hidden = true;
      el("bot-goal").value = routedGoal;
      await el("bot-goal-form").onsubmit({preventDefault(){}});
    }
  }
  panel.addEventListener("pointerenter", block); panel.addEventListener("pointerleave", block);
  panel.addEventListener("focusin", block); panel.addEventListener("focusout", () => queueMicrotask(block));
  for (const name of ["keydown", "keyup", "pointerdown", "pointerup", "mousedown", "mouseup", "wheel"]) panel.addEventListener(name, e => e.stopPropagation());
  function syncView() {
    syncThinking();
    updateEmbodied();
    const compact = panel.classList.contains("compact");
    const complete = plan?.status === "complete" && !running && !planning;
    el("bot-activity").hidden = complete;
    el("bot-editor").hidden = compact || running || !editing;
    el("bot-plan-body").hidden = compact || editing || !plan;
    el("bot-start").hidden = editing || running || planning || !reviewReady;
    el("bot-start").textContent = quickTask ? "Start now" : plan?.goalProgress ? "Start expansion" : "Start plan";
    el("bot-edit").hidden = compact || running || planning || !plan;
    el("bot-edit").textContent = editing ? "Back to plan" : "Edit goal";
    el("bot-expand").hidden = !compact || complete;
    if (complete) { el("bot-edit").hidden = true; el("bot-plan-body").hidden = true; }
    el("bot-expand").textContent = pickPending ? "Cancel picking" : running || planning ? "Open activity" : plan && !editing ? "Review plan" : "Open task";
    el("bot-expand").setAttribute("aria-expanded", String(!compact));
    el("bot-minimize").setAttribute("aria-expanded", String(!compact));
    drawProgress();
  }
  function show(compact = false) {
    panel.hidden = false; panel.classList.toggle("compact", compact); syncView(); block();
  }
  function minimize() {
    show(true); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2");
  }
  function finishGoal(token) {
    running = false; currentAction = null; reviewReady = false; companionReturning = true; embodied.hidden = true;
    el("bot-stop").hidden = true;
    activity = []; lastActivity = "";
    message("Goal complete."); show(true);
    const completedPlan = plan;
    setTimeout(() => {
      if (generation === token && plan === completedPlan && !running && !planning && !chatBusy) close();
    }, 2200);
  }
  function cancelPending() {
    controller?.abort(); controller = null;
    if (capturePending) { clearTimeout(capturePending.timer); capturePending.reject(new Error("Cancelled")); capturePending = null; }
    if (actionPending) { clearTimeout(actionPending.timer); actionPending.reject(new Error("Cancelled")); actionPending = null; }
  }
  function stop(reason = "Stopped. Completed work is kept.") {
    const hadControl = running || pickPending || !!state?.botBusy;
    const wasActive = running || planning;
    generation++; running = planning = false; currentAction = null; companionReturning = true; embodied.hidden = true; cancelPending();
    if (hadControl) send("CoachBotStop", "user");
    pickPending = false; send("CoachPickTile", "0"); reviewReady = false;
    el("bot-stop").hidden = true; el("bot-plan").disabled = false;
    editing = !plan; message(reason);
    if (wasActive && !panel.hidden) minimize(); else { syncView(); block(); }
  }
  function close() {
    if (running || planning) { minimize(); return; }
    if (plan) { companionReturning = true; updateEmbodied(); }
    pickPending = false; send("CoachPickTile", "0"); panel.hidden = true;
    el("unity-canvas").focus(); send("CoachSetInputBlocked", "2");
  }
  el("bot-close").onclick = close;
  el("bot-stop").onclick = () => stop();
  el("bot-minimize").onclick = minimize;
  el("bot-expand").onclick = () => {
    if (pickPending) { pickPending = false; send("CoachPickTile", "0"); message("Tile selection cancelled."); }
    show(false);
  };
  el("bot-edit").onclick = () => { editing = !editing; show(false); if (editing) el("bot-goal").focus(); };
  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && (running || planning || pickPending || !panel.hidden)) {
      e.preventDefault(); e.stopImmediatePropagation();
      if (pickPending) { pickPending = false; send("CoachPickTile", "0"); show(); message("Tile selection cancelled."); }
      else if (running || planning) stop(); else close();
    }
  }, true);
  el("bot-pick").onclick = () => {
    if (!game || running || planning) return;
    pickPending = true; send("CoachPickTile", "1");
    message("Click a tile in the game. Escape cancels picking."); minimize();
  };
  el("bot-clear").onclick = () => { suggestedTile = null; send("CoachPickTile", "clear"); plan = null; reviewReady = false; editing = true; syncView(); };
  const fillGoal = button => { el("bot-goal").value = button.dataset.goal; el("bot-goal").focus(); };
  for (const button of panel.querySelectorAll("[data-goal]")) button.onclick = () => fillGoal(button);
  // Direct binding keeps the primary expansion preset available to lightweight embedded clients too.
  if (el("bot-expand-mines")) el("bot-expand-mines").onclick = () => { el("bot-goal").value = EXPAND_MINES_GOAL; el("bot-goal").focus(); };
  function drawPlan() {
    if (!plan) return;
    editing = false; reviewReady = plan.status === "ready" && !!plan.actions.length;
    const routed = el("bot-model");
    const modelName = plan.model === "gpt-5.4-mini" ? "ASTRABOT" : plan.model === "gpt-6-astra" ? "GPT-6 ASTRA" : String(plan.model || "").toUpperCase();
    if (plan.modelRoute === "rover-exploration") routed.textContent = "ASTRABOT · ROVER EXPLORATION · AGENTS API";
    else if (plan.modelRoute === "advanced-visual") routed.textContent = `ROUTED · ${modelName || "GPT-6 ASTRA"} · VISUAL BUILD PLANNING`;
    else if (plan.modelRoute === "verified-game-state") routed.textContent = "ROUTED · VERIFIED GAME STATE";
    else routed.textContent = "";
    routed.hidden = !routed.textContent;
    el("bot-title").textContent = plan.title;
    el("bot-summary").textContent = plan.summary;
    el("bot-check").textContent = plan.nextCheck || "";
    el("bot-actions").replaceChildren(...plan.actions.map((a, index) => {
      const li = document.createElement("li"); li.id = `bot-step-${index}`; li.textContent = a.reason || a.type.replaceAll("_", " "); return li;
    }));
    syncView();
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
    updateEmbodied();
    controller = new AbortController(); const active = controller;
    const abortError = () => Object.assign(new Error("Planning cancelled."), {name:"AbortError"});
    const checkActive = () => {
      if (active.signal.aborted || token !== generation || !enabled) throw abortError();
    };
    const cancelCapture = () => {
      if (controller !== active || !capturePending) return;
      const pending = capturePending; capturePending = null; clearTimeout(pending.timer); pending.reject(abortError());
    };
    active.signal.addEventListener("abort", cancelCapture);
    // Includes capture and local screen-slot waits, rather than restarting a deadline per retry.
    const timeout = setTimeout(() => active.abort(), 65000);
    const waitForSlot = delay => new Promise((resolve, reject) => {
      if (active.signal.aborted) { reject(abortError()); return; }
      const aborted = () => { clearTimeout(timer); active.signal.removeEventListener("abort", aborted); reject(abortError()); };
      const timer = setTimeout(() => { active.signal.removeEventListener("abort", aborted); resolve(); }, delay);
      active.signal.addEventListener("abort", aborted, {once:true});
    });
    try {
      let config = null, slotWait = 0;
      const loadConfig = async () => {
        const current = window.astraBotAPI ? await window.astraBotAPI.config({signal:active.signal}) : await (await fetch("/api/coach/config", {cache:"no-store", signal:active.signal})).json();
        if (!current.configured) throw new Error("Add an OpenAI key in AstraBot settings (gear beside Copilot), then create a plan.");
        return current;
      };
      for (let attempt = 0; ; attempt++) {
        checkActive();
        message(attempt ? "Refreshing the game view and planning…" : running ? "Checking progress and planning the next steps…" : "Reading the game screen and planning…");
        if (!state || state.session !== session || Date.now() - stateAt > 4000) throw new Error("Game state is stale. Reconnect before planning.");
        // Local capture can run during configuration lookup; upload still requires both.
        // Reuse configuration only during this request's retries, and recapture every time.
        const [currentConfig, image] = await Promise.all([config || loadConfig(), capture()]);
        config = currentConfig;
        checkActive();
        if (!state || state.session !== session || Date.now() - stateAt > 4000) throw new Error("Game state is stale. Reconnect before planning.");
        const snapshot = state;
        const response = await (window.astraBotAPI?.request || fetch)("/api/astrabot/plan", { method:"POST", signal:active.signal,
          headers:{"Content-Type":"application/json", "X-Astra-Coach":config.token},
          body:JSON.stringify({ goal, state:snapshot, image, selectedTile:checkpoint?.selectedTile ?? snapshot.pickedTile ?? null,
            previousPlan:plan ? {id:plan.planId, actions:plan.actions, results:results.slice(-60)} : results.length ? {id:"resumed",actions:[],results:results.slice(-60)} : null }) });
        const data = await response.json();
        checkActive();
        const localBusy = response.status === 429 && ["AstraBot is already reading a screen", "Wait a moment before asking again"].includes(data.error);
        if (localBusy) {
          if (slotWait >= 24000) throw new Error("The screen reader is still busy. Wait a moment, then create the plan again.");
          const hint = data.retryAfterMs;
          const delay = Math.min(24000 - slotWait, typeof hint === "number" && Number.isFinite(hint) && hint > 0 ? Math.max(250, Math.min(4000, hint)) : 4000);
          slotWait += delay;
          message("Waiting for the current screen reading to finish… Stop cancels the wait.");
          await waitForSlot(delay);
          continue;
        }
        if (!response.ok) throw new Error(data.error || "Could not create a plan.");
        plan = data; batches++;
        // A freshly prepared plan has finished its launch/inference beat, so come
        // back to the UI dock while waiting for Start. Active chained plans remain
        // deployed because `running` stays true during their replanning gaps.
        if (!running) companionReturning = true;
        drawPlan(); saveCheckpoint();
        recordActivity(data.summary, "plan");
        message(data.status === "complete" ? "Goal complete." : data.status === "blocked" ? "AstraBot needs a change before continuing." : `${data.title}. Ready when you are.`);
        return data;
      }
    } finally {
      cancelCapture();
      active.abort();
      clearTimeout(timeout); active.signal.removeEventListener("abort", cancelCapture);
      if (controller === active) controller = null;
      if (token === generation) { planning = false; syncView(); el("bot-plan").disabled = false; if (!running) el("bot-stop").hidden = true; if (plan?.status === "complete") finishGoal(token); }
    }
  }
  function saveCheckpoint() {
    checkpoint = {session,goal,selectedTile:checkpoint?.selectedTile ?? state?.pickedTile ?? null,results:results.slice(-120),batches};
    // No images, credentials or automatic restart. This ledger accompanies later batches.
    try { sessionStorage.setItem("astra.bot.project", JSON.stringify(checkpoint)); } catch {}
  }
  el("bot-goal-form").onsubmit = async event => {
    event.preventDefault(); if (!enabled || running || planning || chatBusy) return;
    goal = el("bot-goal").value.trim(); if (!goal) { el("bot-goal").focus(); return; }
    companionReturning = false;
    generation++; const token = generation; results = []; plan = null; quickTask = null; batches = 0; reviewReady = false; drawProgress();
    activity = []; lastActivity = ""; el("bot-timeline").replaceChildren();
    checkpoint = {selectedTile:suggestedTile ?? state?.pickedTile ?? null};
    minimize();
    try { await requestPlan(token); }
    catch (error) { if (token === generation) { stop(error.name === "AbortError" ? "Planning timed out. Try again." : error.message); } }
  };
  function execute(action, token) {
    return new Promise((resolve, reject) => {
      if (token !== generation) return reject(new Error("Cancelled"));
      if (!state || state.session !== session || Date.now() - stateAt > 4000) return reject(new Error("Game state is stale. Reconnect before continuing."));
      const id = `bot-${Date.now()}-${++sequence}`;
      actionPending = {id,resolve,reject,timer:setTimeout(() => { actionPending = null; send("CoachBotStop", "timeout"); reject(new Error("Action timed out; control returned to you.")); }, 75000)};
      send("CoachBotCommand", JSON.stringify({...action,id,session,x:action.x ?? -1,y:action.y ?? -1,targetX:action.targetX ?? -1,targetY:action.targetY ?? -1,trainIndex:action.trainIndex ?? -1,seconds:action.seconds ?? 5}));
    });
  }
  el("bot-start").onclick = async () => {
    if (!plan || running || planning || chatBusy || plan.status !== "ready") return;
    if (quickTask) {
      const fresh = Date.now()-stateAt <= 4000 && window.AstraCoachPolicy?.suggestTasks(state).find(t => t.id === quickTask.id);
      if (!fresh || fresh.cost > quickTask.cost) { stop("Colony changed. Pick a fresh suggestion."); return; }
      plan.actions = [fresh.action];
    }
    companionReturning = false; running = true; startedAt = Date.now(); const token = ++generation;
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
          currentAction = action;
          message(action.reason || action.type.replaceAll("_", " "));
          const result = await execute(action, token);
          if (token !== generation) return;
          results.push({action, ...result}); results = results.slice(-120); saveCheckpoint();
          recordActivity(result.message || (result.status === "complete" ? "Action complete" : "Action stopped"), result.status);
          if (result.status === "cancelled") { stop(result.message || "Player took control. Task stopped."); return; }
          el(`bot-step-${i}`).dataset.state = result.status === "complete" ? "done" : "failed";
          if (result.status !== "complete") { failures++; batchFailed = true; break; }
          if (action.type === "stop") { stop("AstraBot stopped as planned."); return; }
        }
        if (!batchFailed) failures = 0;
        if (quickTask) {
          running = false; currentAction = null; embodied.hidden = true; reviewReady = false;
          plan.status = batchFailed ? "blocked" : "complete";
          el("bot-stop").hidden = true;
          if (batchFailed) { message("Couldn’t finish. Check activity or choose another task."); show(true); }
          else finishGoal(token);
          return;
        }
        if (failures >= 3) throw new Error("Repeated blocker. Review the action results before continuing.");
        const next = await requestPlan(token);
        if (!next || token !== generation) return;
        if (next.status !== "ready" || !next.actions.length) {
          running = false; currentAction = null; embodied.hidden = true; el("bot-stop").hidden = true; show(true); return;
        }
      }
    } catch (error) { if (token === generation) stop(error.name === "AbortError" ? "Planning timed out. Your completed work is kept." : error.message); }
  };
  document.addEventListener("visibilitychange", () => { if (document.hidden && (running || planning)) stop("Paused takeover because the game tab was hidden. Your completed work is kept."); });
  document.addEventListener("astra:settings", () => { stop("Settings opened. Completed work is kept."); close(); });
  document.addEventListener("astra:credentials", () => { stop("Connection changed. Review your plan before continuing."); });
  for (const event of ["astra:credentials","astra:settings"]) document.addEventListener(event,()=>{ if(chatBusy) stopChat(); chatHistory=[]; replyCard.hidden=true; });
  document.addEventListener("visibilitychange",()=>{ if(document.hidden && chatBusy) stopChat(); });
  document.addEventListener("keydown",e=>{ if(e.key==='Escape' && chatBusy) { e.preventDefault(); stopChat(); } },true);
  window.astraBotControl = {
    focusInput() { el("bot-command-input").focus(); block(); },
    ready(instance) { game = instance; enabled = window.astraCoach?.enabled?.() !== false; updateCompanionDock(); },
    setEnabled(value) { enabled = !!value; if (!enabled) { if(chatBusy) stopChat(); replyCard.hidden=true; stop("AstraBot is switched off."); close(); } },
    suggest(id) {
      if (!enabled || running || planning || chatBusy || !game || !state || Date.now()-stateAt > 4000) return;
      replyCard.hidden = true;
      const task = window.AstraCoachPolicy?.suggestTasks(state).find(t => t.id === id);
      if (!task) return;
      companionReturning = true; generation++; quickTask = task; goal = task.goal; results = []; batches = 0;
      activity = []; lastActivity = ""; el("bot-timeline").replaceChildren();
      checkpoint = {selectedTile:task.target || null};
      plan = {status:"ready",title:task.label,summary:task.text,modelRoute:"verified-game-state",actions:[task.action]};
      el("bot-plan-details").open = false;
      drawPlan(); message(`${task.text}${task.target ? ` · tile ${task.target.x}, ${task.target.y}` : " · stop at new ore"}. Ready when you are.`);
      show(true);
    },
    open(prefill, target) {
      if (!enabled) return;
      if (chatBusy) return;
      replyCard.hidden = true;
      if (quickTask && !running && !planning && !prefill) { quickTask = null; plan = null; reviewReady = false; editing = true; }
      if (typeof prefill === "string" && prefill.trim() && !running && !planning) {
        quickTask = null;
        suggestedTile = target && Number.isInteger(target.x) && Number.isInteger(target.y) ? {x:target.x,y:target.y} : null;
        if (suggestedTile) send("CoachPickTile", `set:${suggestedTile.x},${suggestedTile.y}`);
        else { send("CoachPickTile", "clear"); if (state) state = {...state,pickedTile:null}; }
        el("bot-goal").value = prefill.trim(); plan = null; reviewReady = false; editing = true;
        el("bot-tile").textContent = suggestedTile ? `Selected tile (${suggestedTile.x}, ${suggestedTile.y})` : "Whole colony";
        el("bot-clear").hidden = !suggestedTile;
        message(suggestedTile ? "The suggested tile is selected. Review the goal, then create a plan." : "Review the goal, then create a plan. I’ll wait for Start."); show(false); el("bot-goal").focus(); return;
      }
      show(running || planning || pickPending);
    },
    active() { return running || planning || chatBusy; },
    receive(next) {
      next = {...next, pickedTile:next.hasPickedTile ? next.pickedTile : null};
      commandBar.dataset.nativeCompanionMode = next.companionMode || "";
      commandBar.dataset.nativeCompanionPosition = `${Number(next.companionScreenX).toFixed(3)},${Number(next.companionScreenY).toFixed(3)}`;
      commandBar.dataset.nativeCompanionDock = `${Number(next.companionDockX).toFixed(3)},${Number(next.companionDockY).toFixed(3)}`;
      if (session && session !== next.session) { if(chatBusy) stopChat(); chatHistory=[]; replyCard.hidden=true; }
      if (session && session !== next.session) { stop("New colony. Create a new plan."); plan = null; results = []; checkpoint = null; companionMode = ""; }
      const playerPaused = state && !state.paused && next.paused;
      session = next.session; state = next; stateAt = Date.now();
      if (playerPaused && running) stop("Colony paused. AstraBot stopped; review before continuing.");
      if (running && next.botBusy && next.botActionMessage) {
        el("bot-status").textContent = next.botActionMessage;
        recordActivity(next.botActionMessage, "action");
      }
      updateEmbodied();
      if (suggestedTile && next.pickedTile && next.pickedTile.x === suggestedTile.x && next.pickedTile.y === suggestedTile.y) suggestedTile = null;
      el("bot-tile").textContent = next.pickedTile ? `Tile (${next.pickedTile.x}, ${next.pickedTile.y})` : suggestedTile ? `Astra-selected extractor (${suggestedTile.x}, ${suggestedTile.y})` : "Whole colony";
      el("bot-clear").hidden = !next.pickedTile;
      drawProgress();
      if (pickPending && !next.pickingTile && next.hasPickedTile && next.pickedTile) { pickPending = false; plan = null; reviewReady = false; editing = true; show(true); message("Tile selected. Open task to describe what to do here."); }
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
    diagnostics() { return {running,planning,batches,completed:results.filter(r => r.status === "complete").length,status:plan?.status,compact:panel.classList.contains("compact"),panelOpen:!panel.hidden,selectedTile:state?.pickedTile ?? null}; }
  };
  window.addEventListener?.("resize", () => { updateCompanionDock(); updateEmbodied(); });
})();
