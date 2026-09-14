(() => {
  "use strict";
  const avatar = `<svg class="astrabot-avatar" viewBox="0 0 86 96" aria-hidden="true"><defs><linearGradient id="astrabot-shell" x2=".8" y2="1"><stop stop-color="#dcf7e8"/><stop offset="1" stop-color="#7ebab1"/></linearGradient><linearGradient id="astrabot-visor" x2="0" y2="1"><stop stop-color="#284e59"/><stop offset="1" stop-color="#102a38"/></linearGradient></defs><ellipse cx="43" cy="88" rx="23" ry="4" fill="#05212e" opacity=".45"/><g class="astrabot-float"><path d="M43 20V11" stroke="#9cd4c5" stroke-width="3"/><circle cx="43" cy="8" r="4" fill="#ffd798"/><path d="M26 67q17-9 34 0l-4 13H30Z" fill="#78bcb0"/><path d="m34 79 9 6 9-6" fill="#b8f5d9"/><rect x="8" y="39" width="10" height="23" rx="4" fill="#edbc7f"/><rect x="68" y="39" width="10" height="23" rx="4" fill="#edbc7f"/><rect x="14" y="21" width="58" height="52" rx="20" fill="url(#astrabot-shell)" stroke="#dcffed" stroke-width="1.3"/><path d="M24 30q19-8 39 0" fill="none" stroke="#f2fff8" stroke-width="2" opacity=".7"/><rect x="20" y="33" width="46" height="29" rx="12" fill="url(#astrabot-visor)"/><rect class="astrabot-eye" x="29" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><rect class="astrabot-eye" x="51" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><path d="M39 55q4 3 8 0" fill="none" stroke="#70baa9" stroke-width="1.6" stroke-linecap="round"/><circle cx="58" cy="67" r="2" fill="#244e50"/><path d="M29 66h13" stroke="#d9f8e9" stroke-width="2" stroke-linecap="round"/></g></svg>`;
  const root = document.createElement("aside");
  root.id = "coach"; root.hidden = true; root.dataset.open = "false";
  root.setAttribute("aria-label", "AstraBot, colony copilot");
  root.innerHTML = `<section id="coach-panel" role="region" aria-label="AstraBot's coaching" inert>
    <header class="coach-header"><div><span class="coach-name">AstraBot</span><span class="coach-role">COLONY COPILOT</span></div><button id="coach-close" aria-label="Dismiss AstraBot">×</button></header>
    <div class="coach-body"><div class="coach-source" id="coach-source">Next step</div><div id="coach-provenance" hidden></div><p id="coach-sight" hidden></p><h2 id="coach-title">A little help, when you need it</h2><p id="coach-primary"></p><details id="coach-details"><summary>Why / details</summary><p id="coach-copy"></p><ul id="coach-steps"></ul></details>
    </div><div class="coach-controls"><div class="coach-actions"><button id="coach-show">Show me</button></div>
    <button id="coach-proactive" class="coach-proactive" hidden></button><button id="coach-ablation" class="coach-ablation" hidden>Compare without image</button><p id="coach-comparison" hidden></p><button id="astrabot-task-open" class="coach-task">Give AstraBot a task</button>
    </div>
    <footer class="coach-footer"><span id="coach-status" role="status">Local suggestions · no passive AI calls</span></footer></section>
    <div class="coach-dock"><button id="coach-launcher" aria-label="Open AstraBot, your colony copilot" aria-expanded="false" aria-controls="coach-panel">${avatar}</button><div class="coach-teaser"><strong>ASTRABOT · LET’S BUILD</strong><span id="coach-teaser-text">What shall we do next?</span><div id="coach-thinking" role="status" hidden>Thinking <span class="astra-dots" aria-hidden="true"><i></i><i></i><i></i></span></div><div class="coach-dock-actions"><button id="coach-offer" hidden></button><button id="coach-task-quick">Do a task →</button><button id="coach-offer-dismiss" aria-label="Dismiss suggestion" hidden>×</button></div></div></div>`;
  document.body.append(root);
  const spot = document.createElement("div"); spot.id = "coach-spotlight"; spot.hidden = true;
  spot.innerHTML = '<svg class="coach-crosshair" viewBox="0 0 64 64" aria-hidden="true"><path class="coach-crosshair-shadow" d="M32 3v15M32 46v15M3 32h15M46 32h15"/><path class="coach-crosshair-arms" d="M32 3v15M32 46v15M3 32h15M46 32h15"/><circle cx="32" cy="32" r="6" fill="#061822"/><circle cx="32" cy="32" r="3" fill="#ffe4a3"/></svg><div class="coach-cue-hint"><span id="coach-cue-text" role="status"></span><button id="coach-cue-focus" hidden>Show target</button><button id="coach-cue-close" aria-label="Dismiss guidance">×</button></div>';
  document.body.append(spot);
  const evidenceBox = document.createElement("div"); evidenceBox.id = "coach-evidence-box"; evidenceBox.hidden = true;
  evidenceBox.innerHTML = '<span id="coach-evidence-label"></span>';
  document.body.append(evidenceBox);
  const el = id => document.getElementById(id);
  function buttonArt(id, caption, name) {
    const button = el(id), art = document.createElement("img"), label = document.createElement("span");
    art.src = `icons/${name}.png`; art.alt = ""; art.draggable = false; art.className = "coach-button-art";
    label.textContent = caption; button.replaceChildren(art, label);
  }
  buttonArt("coach-close", "", "close"); buttonArt("coach-show", "Show me", "focus");
  buttonArt("astrabot-task-open", "Give AstraBot a task", "play");
  const TASK_SUGGESTIONS = {
    "discover-ore": { label:"Send rover to survey", goal:"Send the rover on automatic exploration to uncover fog and discover a new Ore deposit. Stop safely once a new Ore deposit is revealed." },
    "expand-mines": { label:"Plan mining outposts", goal:"Build two more Ore extractors on revealed Ore deposits. Connect each to enough solar power and to the colony by rail so idle trains can begin service." }
  };
  const prefs = { get(k, d) { try { return localStorage.getItem(k) ?? d; } catch { return d; } }, set(k,v) { try { localStorage.setItem(k,v); } catch {} } };
  let enabled = prefs.get("astra.bot.enabled", "1") !== "0";
  const enableButton = document.createElement("button");
  enableButton.id = "astrabot-enable"; enableButton.type = "button";
  enableButton.setAttribute("role", "switch"); enableButton.setAttribute("aria-label", "Colony copilot");
  enableButton.innerHTML = '<span>Copilot</span><span class="astrabot-toggle-state"></span><span class="astrabot-toggle-track" aria-hidden="true"></span>';
  document.body.append(enableButton);
  let game, state, candidates = [], current, fingerprint = "", contextVersion = 0, open = false, requestNumber = 0;
  let configured = false, csrf = "", busy = false, lastRequest = 0, lastStateAt = 0, controller, pendingCapture;
  let lastVisionAt = 0, lastCaptureAt = 0, lastVisionSignature = "", highlight = null, highlightUntil = 0;
  let shownGuide = "", cueStage = "", dismissedCue = "", evidence = null;
  let events = [], previousState, question = "", lastDiagnosticRequest = null;
  // Passive model inference is disabled, including previously saved opt-ins.
  const live = false;
  let offer = null, dismissedOffer = "", offerSession = "";
  let tutorialMode = false, intro = true;
  const persistentAvatar = document.createElement("button");
  persistentAvatar.id = "astrabot-avatar-home";
  persistentAvatar.setAttribute("aria-label", "Talk to AstraBot");
  persistentAvatar.innerHTML = avatar;
  persistentAvatar.addEventListener("click", () => window.astraBotControl?.focusInput());
  for (const name of ["pointerdown","pointerup","mousedown","mouseup","keydown","keyup"]) persistentAvatar.addEventListener(name, event => event.stopPropagation());
  document.body.append(persistentAvatar);
  root.append(el("coach-panel"));
  root.querySelector(".coach-teaser strong").textContent = "AstraBot";
  el("coach-launcher").setAttribute("aria-label", "Toggle AstraBot local guidance");
  function updateEnabledControl() {
    enableButton.setAttribute("aria-checked", String(enabled));
    enableButton.querySelector(".astrabot-toggle-state").textContent = enabled ? "On" : "Off";
    enableButton.title = enabled ? "Turn the colony copilot off" : "Turn the colony copilot on";
    root.hidden = !intro || !enabled || !state || taskVisible();
    persistentAvatar.hidden = !enabled || !state;
  }
  function setEnabled(value) {
    enabled = !!value; prefs.set("astra.bot.enabled", enabled ? "1" : "0");
    if (!enabled) { setOpen(false); configured = false; csrf = ""; }
    updateEnabledControl();
    window.astraBotControl?.setEnabled(enabled);
    window.dispatchEvent(new CustomEvent("astra:enabled", {detail:enabled}));
    if (enabled) refreshConfig();
  }
  enableButton.addEventListener("click", event => { event.stopPropagation(); setEnabled(!enabled); });
  for (const name of ["pointerdown","pointerup","mousedown","mouseup","keydown","keyup","wheel"])
    enableButton.addEventListener(name, event => event.stopPropagation());
  updateEnabledControl();
  function send(method, value) { game?.SendMessage("Astra Express", method, value); }
  function blockInput() { send("CoachSetInputBlocked", !root.hidden && (root.matches(":hover") || root.contains(document.activeElement)) ? "1" : "0"); }
  root.addEventListener("pointerenter", blockInput); root.addEventListener("pointerleave", blockInput);
  root.addEventListener("focusin", blockInput); root.addEventListener("focusout", () => queueMicrotask(blockInput));
  // The DOM panel must not trigger Unity shortcuts, clicks, zoom, or camera pan underneath.
  for (const name of ["keydown","keyup","pointerdown","pointerup","mousedown","mouseup","wheel"]) root.addEventListener(name, e => {
    e.stopPropagation();
    if (name === "keydown" && e.key === "Escape") { e.preventDefault(); setOpen(false); }
  });
  function setOpen(value, keepHighlight = false) {
    if (value && !enabled) return;
    if (value && el("astrabot-settings") && !el("astrabot-settings").hidden) return;
    open = value; root.dataset.open = String(value); el("coach-panel").inert = !value;
    el("coach-launcher").setAttribute("aria-expanded", String(value));
    if (value) { if (current) render(current); }
    else { contextVersion++; controller?.abort(); rejectCapture(); question = ""; clearEvidence(); if (!keepHighlight) dismissHighlight(); prefs.set("astra.coach.dismissed", "1"); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2"); }
    blockInput();
  }
  el("coach-launcher").addEventListener("click", () => setOpen(!open));
  el("coach-close").addEventListener("click", () => setOpen(false));
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && open) { event.preventDefault(); event.stopImmediatePropagation(); setOpen(false); }
    else if (event.key === "Escape" && highlight) dismissHighlight();
  }, true);
  for (const name of ["pointerdown", "pointerup", "mousedown", "mouseup", "keydown", "keyup"])
    el("coach-cue-close").addEventListener(name, event => event.stopPropagation());
  el("coach-cue-close").addEventListener("click", event => { event.stopPropagation(); dismissHighlight(); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2"); });
  el("astrabot-task-open").addEventListener("click", () => {
    if (!enabled) return;
    if (!window.astraBotControl?.open) { status("Task controls are starting…"); return; }
    setOpen(false); window.astraBotControl.open();
  });
  el("coach-task-quick").textContent = "Ask AstraBot";
  el("coach-task-quick").addEventListener("click", () => { intro = false; setOpen(false); syncPresentation(); window.astraBotControl?.focusInput(); });
  const tutorialButton = document.createElement("button"); tutorialButton.id = "coach-tutorial"; tutorialButton.textContent = "Start tutorial";
  el("coach-task-quick").parentNode.insertBefore(tutorialButton, el("coach-task-quick"));
  tutorialButton.addEventListener("click", () => { tutorialMode = !tutorialMode; intro = false; dismissedCue = ""; clearHighlight(); setOpen(false, true); syncPresentation(); });
  window.addEventListener("astra:task-intent", () => { intro = false; tutorialMode = false; clearHighlight(); setOpen(false); });
  let offers = [], scannedAt = 0;
  const offerButtons = [el("coach-offer")];
  for (let i = 1; i < 3; i++) {
    const button = document.createElement("button"); button.hidden = true;
    el("coach-offer").parentNode.insertBefore(button, el("coach-task-quick")); offerButtons.push(button);
  }
  offerButtons.forEach((button,index) => button.addEventListener("click", () => {
    const latest = AstraCoachPolicy.suggestTasks(state).find(t => t.id === offers[index]?.id);
    if (!enabled || !latest || !window.astraBotControl?.suggest) return;
    setOpen(false); window.astraBotControl.suggest(latest.id);
  }));
  el("coach-offer-dismiss").addEventListener("click", () => { dismissedOffer = offer?.id || ""; updateOffer(); });
  function updateOffer() {
    if (offerSession !== state?.session) { offerSession = state?.session; dismissedOffer = ""; scannedAt = 0; }
    if (Date.now()-scannedAt >= 3000) { offers = AstraCoachPolicy.suggestTasks(state); scannedAt = Date.now(); }
    offer = offers.find(t => t.id !== dismissedOffer);
    const visible = !intro && !!offer && offer.id !== dismissedOffer && !open && !busy && !cueBlocked();
    tutorialButton.textContent = tutorialMode ? "End tutorial" : "Start tutorial";
    el("coach-offer-dismiss").hidden = !visible;
    offerButtons.forEach((button,index) => {
      const task = offers[index]; button.hidden = !visible || !task || task.id === dismissedOffer;
      button.textContent = task ? `${task.text} →` : "";
    });
    el("coach-teaser-text").textContent = intro ? "Take a tutorial, or tell me what you need." : visible ? "I can help with…" : "Here when you need me.";
    root.dataset.thinking = String(busy);
    el("coach-thinking").hidden = !busy;
  }
  el("coach-proactive").addEventListener("click", () => {
    const diagnostic = current?.visualDiagnosis;
    const suggestion = diagnostic?.repairAvailable ? {goal:diagnostic.goal,target:diagnostic.target} : TASK_SUGGESTIONS[current?.taskSuggestion];
    if (!suggestion || !window.astraBotControl?.open) return;
    setOpen(false); window.astraBotControl.open(suggestion.goal, suggestion.target);
  });
  el("coach-ablation").addEventListener("click", async () => {
    if (!lastDiagnosticRequest || busy || !csrf) return;
    el("coach-ablation").disabled = true; status("Repeating the same turn without its image…");
    try {
      const response = await (window.astraBotAPI?.request || fetch)("/api/coach", {method:"POST", headers:{"Content-Type":"application/json","X-Astra-Coach":csrf}, body:JSON.stringify({...lastDiagnosticRequest,imageRemoved:true})});
      const result = await response.json(); if (!response.ok) throw new Error(result.error || "Comparison unavailable");
      el("coach-comparison").textContent = `IMAGE REMOVED · ${result.observation} · No target or repair was resolved.`;
      el("coach-comparison").hidden = false; status("Image contribution compared");
    } catch (error) { status(String(error.message).slice(0,95)); }
    finally { el("coach-ablation").disabled = false; }
  });
  function status(text) { el("coach-status").textContent = text; updateOffer(); }
  function render(t, vision = false) {
    if (!t) return;
    const primary = t.primaryStep || t.cueLabel || t.steps?.[0] || t.title;
    const changedAction = current?.id !== t.id || el("coach-primary").textContent !== primary;
    current = t;
    el("coach-title").textContent = t.title;
    el("coach-title").hidden = (t.title || "").replace(/[.!]+$/, "") === primary.replace(/[.!]+$/, "");
    el("coach-primary").textContent = primary;
    el("coach-copy").textContent = t.body || "";
    const astraVision = vision && t.model === "gpt-6-astra" && t.planSource === "agents-api";
    el("coach-sight").textContent = t.observation || ""; el("coach-sight").hidden = !astraVision || !t.observation;
    const later = (t.steps || []).filter(text => text !== primary);
    el("coach-steps").replaceChildren(...later.map(text => { const li = document.createElement("li"); li.textContent = text; return li; }));
    el("coach-details").hidden = !t.body && !later.length;
    const grounding = t.grounding?.status;
    el("coach-source").textContent = astraVision ? `Astra vision · ${grounding === "matched" ? "grounded" : grounding === "missed" ? "box unverified" : "screen checked"}` : vision ? "Next step · model checked" : "Next step";
    const provenance = el("coach-provenance");
    provenance.textContent = astraVision ? `GPT-6 ASTRA · AGENTS API · IMAGE + GAME STATE · ${Math.round(t.durationMs || 0)} ms · frame ${((t.frameAgeMs || 0)/1000).toFixed(1)} s` : "";
    provenance.hidden = !astraVision;
    const suggestion = astraVision && (t.visualDiagnosis?.repairAvailable ? {label:t.visualDiagnosis.label} : TASK_SUGGESTIONS[t.taskSuggestion]);
    el("coach-proactive").textContent = suggestion?.label || ""; el("coach-proactive").hidden = !suggestion;
    const diagnosticReady = astraVision && t.visualDiagnosis?.status === "validated" && !!lastDiagnosticRequest;
    el("coach-ablation").hidden = !diagnosticReady;
    if (!diagnosticReady) { el("coach-comparison").hidden = true; el("coach-comparison").textContent = ""; }
    evidence = astraVision && t.visualEvidence?.visible ? { box:t.visualEvidence, grounding:t.grounding } : null;
    updateEvidenceBox();
    buttonArt("coach-show", "Show me", "focus");
    el("coach-show").hidden = !t.target && !t.uiTarget; el("coach-teaser-text").textContent = primary;
    if (changedAction) { el("coach-details").open = false; root.querySelector(".coach-body").scrollTop = 0; }
  }
  function taskVisible() { const task = el("astrabot-task"), reply = el("astrabot-reply"); return (!!task && !task.hidden) || (!!reply && !reply.hidden); }
  function cueBlocked() { return !enabled || document.hidden || (el("astrabot-settings") && !el("astrabot-settings").hidden) || taskVisible() || state?.pickingTile || state?.botBusy || window.astraBotControl?.active(); }
  function stageFor(cue) {
    return cue ? `${state?.session}:${state?.tool}:${state?.routeStart?.x},${state?.routeStart?.y}:${cue.id}:${cue.uiTarget || "world"}:${cue.target?.x},${cue.target?.y}` : "";
  }
  function clearHighlight() { spot.hidden = true; highlight = null; shownGuide = ""; cueStage = ""; }
  function clearEvidence() { evidenceBox.hidden = true; evidence = null; }
  function updateEvidenceBox() {
    if (!evidence || !enabled || !open || taskVisible()) { evidenceBox.hidden = true; return; }
    const canvas = el("unity-canvas")?.getBoundingClientRect(), b = evidence.box;
    if (!canvas?.width || !canvas?.height || ![b.xMin,b.yMin,b.xMax,b.yMax].every(Number.isFinite)) { evidenceBox.hidden = true; return; }
    evidenceBox.style.left = `${canvas.left + b.xMin/1000*canvas.width}px`;
    evidenceBox.style.top = `${canvas.top + b.yMin/1000*canvas.height}px`;
    evidenceBox.style.width = `${(b.xMax-b.xMin)/1000*canvas.width}px`;
    evidenceBox.style.height = `${(b.yMax-b.yMin)/1000*canvas.height}px`;
    const grounding = evidence.grounding?.status || "unavailable"; evidenceBox.dataset.grounding = grounding;
    el("coach-evidence-label").textContent = `ASTRA SAW · ${b.label}${grounding === "matched" ? " · GROUNDED" : " · CHECK"}`;
    evidenceBox.hidden = false;
  }
  function dismissHighlight() { tutorialMode = false; dismissedCue = cueStage || stageFor(candidates[0]); clearHighlight(); updateOffer(); }
  function syncPresentation() {
    if (taskVisible() && open) setOpen(false);
    root.hidden = !intro || !enabled || !state || taskVisible();
    persistentAvatar.hidden = !enabled || !state;
    updateSpotlight(); updateEvidenceBox(); updateOffer();
  }
  function validRect(a) { return a && a.visible !== false && [a.x,a.y,a.width,a.height].every(Number.isFinite) && a.width > 0 && a.height > 0; }
  function toRect(a, canvas) { return {left:canvas.left + a.x * canvas.width, top:canvas.top + a.y * canvas.height, width:a.width * canvas.width, height:a.height * canvas.height}; }
  function overlap(a, b, margin = 0) { return Math.max(0, Math.min(a.left+a.width,b.left+b.width+margin)-Math.max(a.left,b.left-margin)) * Math.max(0,Math.min(a.top+a.height,b.top+b.height+margin)-Math.max(a.top,b.top-margin)); }
  function visiblePanels(canvas) {
    const panels = (state?.uiPanels || []).filter(validRect).map(a => toRect(a,canvas));
    if (open) panels.push(el("coach-panel").getBoundingClientRect());
    if (!root.hidden) panels.push(el("coach-launcher").getBoundingClientRect());
    return panels.filter(p => p.width > 0 && p.height > 0);
  }
  function placeHint(x, y, targetRect, canvas, bottom, occluded) {
    const hint = spot.querySelector(".coach-cue-hint"), width = hint.offsetWidth, height = hint.offsetHeight;
    const minX = Math.max(8,canvas.left+8), maxX = Math.max(minX,Math.min(innerWidth,canvas.right)-width-8);
    const minY = Math.max(8,canvas.top+8), maxY = Math.max(minY,Math.min(innerHeight,bottom)-height-8);
    const panels = visiblePanels(canvas);
    const gap = 13;
    const choices = [
      {left:x-width/2, top:targetRect.top-height-gap},
      {left:targetRect.left+targetRect.width+gap, top:y-height/2},
      {left:targetRect.left-width-gap, top:y-height/2},
      {left:x-width/2, top:targetRect.top+targetRect.height+gap},
      // A control inside a sidebar, or beside the avatar, needs positions just
      // outside that panel as well as positions outside the control itself.
      ...panels.flatMap(panel => [
        {left:panel.left-width-gap,top:y-height/2},
        {left:panel.left+panel.width+gap,top:y-height/2},
        {left:x-width/2,top:panel.top-height-gap},
        {left:x-width/2,top:panel.top+panel.height+gap}
      ]),
      {left:minX,top:maxY}, {left:maxX,top:maxY}
    ].map((p,index) => {
      const r = {left:Math.max(minX,Math.min(maxX,p.left)),top:Math.max(minY,Math.min(maxY,p.top)),width,height};
      const targetPenalty = occluded ? 0 : overlap(r,targetRect,8)*100;
      const dx = Math.max(0,targetRect.left-r.left-r.width,r.left-targetRect.left-targetRect.width);
      const dy = Math.max(0,targetRect.top-r.top-r.height,r.top-targetRect.top-targetRect.height);
      return {r, score:targetPenalty+panels.reduce((sum,panel)=>sum+overlap(r,panel,5)*10,0)+Math.hypot(dx,dy)+index*.01};
    });
    choices.sort((a,b)=>a.score-b.score);
    hint.style.left = `${choices[0].r.left}px`; hint.style.top = `${choices[0].r.top}px`;
  }
  function updateSpotlight() {
    if (cueBlocked() || !state || Date.now()-lastStateAt > 3000) { clearHighlight(); return; }
    let cue = highlight && !highlight.automatic ? candidates.find(t => t.id === highlight.id) || candidates[0] : candidates[0];
    if (!cue) { clearHighlight(); return; }
    const stage = stageFor(cue);
    if (!highlight || highlight.automatic) {
      if (!tutorialMode || !cue.autoCue || dismissedCue === stage) { clearHighlight(); return; }
      highlight = {id:cue.id,automatic:true};
    } else if (Date.now() > highlightUntil && stage === cueStage) { dismissHighlight(); return; }
    const progressed = stage !== cueStage;
    if (progressed) { cueStage = stage; highlightUntil = Date.now()+30000; shownGuide = ""; }
    highlight.id = cue.id;
    const canvas = el("unity-canvas").getBoundingClientRect();
    if (!canvas.width || !canvas.height) { spot.hidden = true; return; }
    const scale = Math.min(canvas.width/1280,canvas.height/720);
    const toolbar = (state.uiAnchors || []).filter(a => validRect(a) && (a.id.startsWith("tool-") || a.id === "fleet"));
    const bottom = toolbar.length ? Math.min(...toolbar.map(a => canvas.top+a.y*canvas.height))-12 : canvas.bottom-100*scale;
    let x, y, targetRect, label = cue.cueLabel || cue.targetLabel || cue.primaryStep || cue.steps?.[0] || "Click here", occluded = false;
    const focusButton = el("coach-cue-focus"); focusButton.hidden = true;
    spot.dataset.kind = cue.uiTarget ? "control" : "world";
    if (cue.uiTarget) {
      const anchor = state.uiAnchors?.find(a => a.id === cue.uiTarget && validRect(a));
      if (!anchor) { clearHighlight(); return; }
      targetRect = toRect(anchor,canvas); x = targetRect.left+targetRect.width/2; y = targetRect.top+targetRect.height/2;

    } else {
      if (!cue.target || !Number.isFinite(cue.target.x) || !Number.isFinite(cue.target.y)) { clearHighlight(); return; }
      const points = [...(state.connectionTargets || []), state.frontier, state.solarSite, state.plantSite, state.rover, state.colonyPort,
        ...(state.buildings || []).flatMap(b => [b.origin,b.port]), ...(state.deposits || []).map(d => d.origin), cue.target];
      const point = points.find(p => p && p.x === cue.target.x && p.y === cue.target.y && Number.isFinite(p.screenX) && Number.isFinite(p.screenY));
      if (!point) { clearHighlight(); return; }
      x = canvas.left+point.screenX*canvas.width; y = canvas.top+point.screenY*canvas.height;
      const covered = visiblePanels(canvas).some(r => x>=r.left && x<=r.left+r.width && y>=r.top && y<=r.top+r.height);
      occluded = !point.visible || covered || x<canvas.left+16 || x>canvas.right-16 || y<canvas.top+16 || y>bottom-28;
      if (occluded) {
        // A hidden target gets an explicit recenter button, never an automatic camera jump.
        x = Math.max(canvas.left+36,Math.min(canvas.right-36,x)); y = Math.max(canvas.top+100*scale,Math.min(bottom-36,y));
        label = /Show target|Show me/i.test(label) ? "Next click is out of view" : `${label.replace(/^Click\s+/i, "").replace(/\.$/, "")} · outside view`; focusButton.hidden = false; spot.dataset.kind = "offscreen";
      }
      targetRect = {left:x-28,top:y-28,width:56,height:56};
    }
    spot.style.left = `${x-28}px`; spot.style.top = `${y-28}px`;
    el("coach-cue-text").textContent = label; spot.hidden = false;
    placeHint(x,y,targetRect,canvas,bottom,occluded);
  }
  function focusCurrentCue() {
    if (cueBlocked()) return;
    const cue = candidates.find(t => t.id === highlight?.id) || current;
    if (!cue?.target) return;
    if (cue.link && state.tool === cue.link.tool) {
      const guide = `${cue.link.tool},${cue.link.origin.x},${cue.link.origin.y}`;
      if (shownGuide !== guide) { shownGuide=guide; send("CoachGuideLink",guide); }
    }
    send("CoachFocus",`${cue.target.x},${cue.target.y}`);
    el("unity-canvas").focus(); send("CoachSetInputBlocked","2");
  }
  el("coach-cue-focus").addEventListener("click", event => { event.stopPropagation(); focusCurrentCue(); });
  for (const name of ["pointerdown","pointerup","mousedown","mouseup","keydown","keyup"])
    el("coach-cue-focus").addEventListener(name,event=>event.stopPropagation());
  el("coach-show").addEventListener("click", () => {
    if (!current?.target && !current?.uiTarget) return;
    dismissedCue = ""; highlight = {id:current.id,automatic:false}; highlightUntil = Date.now()+30000; cueStage = ""; shownGuide = "";
    setOpen(false,true); updateSpotlight();
    if (!current.uiTarget) focusCurrentCue();
  });
  function askQuestion(text) {
    question = text;
    status(busy ? "Question queued · finishing this frame…" : "Question queued…");
    maybeAsk(true);
  }
  async function refreshConfig() {
    if (!enabled) return;
    try {
      const config = window.astraBotAPI ? await window.astraBotAPI.config() : await (await fetch("/api/coach/config", {cache:"no-store"})).json(); if (!enabled) return; configured = config.configured; csrf = config.token;
      if (!live) status("Screen reading off");
      else if (!configured) status("Add a key in AstraBot settings ⚙");
      else if (!busy && !question) status(lastVisionAt ? "Watching while this panel is open" : "OpenAI ready · game screen only");
    } catch { configured = false; status("Game tips available · AI connection unavailable"); }
  }
  function rejectCapture() { if (pendingCapture) { clearTimeout(pendingCapture.timer); pendingCapture.reject(new Error("Capture cancelled")); pendingCapture = null; } }
  function capture() {
    return new Promise((resolve,reject) => {
      const id = String(++requestNumber);
      pendingCapture = { id, resolve, reject, timer:setTimeout(() => { pendingCapture = null; reject(new Error("Could not read this frame")); }, 7000) };
      send("CoachCapture", id);
    });
  }
  async function maybeAsk(force = false) {
    force = force || !!question;
    if (!enabled || !open || !live || (taskVisible() || state?.pickingTile || state?.botBusy || window.astraBotControl?.active()) || (!configured && !force) || !csrf || !state || !game || busy || document.hidden || Date.now()-lastStateAt > 3000) return;
    if (Date.now() - lastRequest < (force ? 4000 : 12000)) return;
    if (!force && fingerprint === lastVisionSignature && Date.now() - lastVisionAt < 45000) return;
    busy = true; lastRequest = Date.now(); const version = contextVersion, signature = fingerprint;
    const sentState = state, allowed = candidates, asked = question;
    controller = new AbortController(); const requestController = controller;
    const timeout = setTimeout(() => requestController.abort(), 50000);
    status(asked ? "Reading the screen to answer you…" : "Reading your game screen…");
    try {
      const frame = await capture();
      if (!enabled || !open || !live || version !== contextVersion) return;
      const requestBody = { image:frame.image, capturedAt:frame.capturedAt, state:sentState, candidates:allowed, events:events.slice(-8), question:asked, contextVersion:version };
      const response = await (window.astraBotAPI?.request || fetch)("/api/coach", { method:"POST", signal:requestController.signal,
        headers:{ "Content-Type":"application/json", "X-Astra-Coach":csrf },
        body:JSON.stringify(requestBody) });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error || "Vision temporarily unavailable");
      if (!enabled || !open || !live || document.hidden || version !== contextVersion || Date.now()-lastStateAt > 3000) return;
      if (question && question !== asked) return;
      const chosen = candidates.find(c => c.id === result.actionId);
      if (!chosen) return;
      // Model explains the screen; canonical game-validated steps remain exact.
      lastDiagnosticRequest = result.visualDiagnosis?.status === "validated" ? requestBody : null;
      render({ ...chosen, title:result.title, body:result.body, observation:result.observation, taskSuggestion:result.taskSuggestion,
        visualEvidence:result.visualEvidence, grounding:result.grounding, model:result.model, modelRoute:result.modelRoute,
        visualDiagnosis:result.visualDiagnosis, planSource:result.planSource, durationMs:result.durationMs, frameAgeMs:result.frameAgeMs }, true);
      if (question === asked) question = "";
      lastVisionAt = Date.now(); lastVisionSignature = signature;
      status("Just read your game screen");
    } catch(error) {
      if (version === contextVersion && open && live) {
        render(candidates[0]);
        if (asked && question === asked) question = "";
        status(error.name === "AbortError" ? "Vision timed out · game tip shown" : String(error.message).slice(0,95));
      }
    } finally {
      clearTimeout(timeout); busy = false; updateOffer(); if (controller === requestController) controller = null;
      if (open && live && version !== contextVersion) status(question ? "Colony changed · refreshing your answer…" : "Colony changed · current game tip shown");
      if (question) maybeAsk(true);
    }
  }
  document.addEventListener("astra:settings", () => { setOpen(false); });
  document.addEventListener("astra:credentials", () => {
    contextVersion++; controller?.abort(); rejectCapture(); configured = false; csrf = ""; lastVisionSignature = "";
    refreshConfig();
  });
  window.astraCoach = {
    enabled() { return enabled; },
    ready(instance) { game = instance; refreshConfig(); },
    receive(next) {
      const changedSession = state && next.session !== state.session;
      if (changedSession) { events = []; previousState = null; contextVersion++; controller?.abort(); lastVisionSignature = ""; lastVisionAt = 0; dismissedCue = ""; tutorialMode = false; intro = true; clearHighlight(); }
      if (previousState && (next.message !== previousState.message || next.tool !== previousState.tool || next.deliveries !== previousState.deliveries))
        events.push({ at:Math.round(next.elapsed), tool:next.tool, message:next.message, credits:next.credits, deliveries:next.deliveries });
      events = events.slice(-8); previousState = next; state = next; lastStateAt = Date.now();
      window.dispatchEvent(new CustomEvent("astra:state", {detail:next}));
      candidates = AstraCoachPolicy.advise(next); const key = AstraCoachPolicy.signature(next, candidates);
      if (key !== fingerprint) { fingerprint = key; contextVersion++; render(candidates[0]); }
      document.documentElement.style.setProperty("--game-scale", Math.min(innerWidth/1280, innerHeight/720));
      syncPresentation();
    },
    screenReady(id, jpeg) {
      if (!pendingCapture || pendingCapture.id !== id) return;
      const pending = pendingCapture; pendingCapture = null; clearTimeout(pending.timer);
      if (!jpeg) pending.reject(new Error("Could not read this frame"));
      else { lastCaptureAt = Date.now(); pending.resolve({image:"data:image/jpeg;base64," + jpeg, capturedAt:lastCaptureAt}); }
    },
    // Read-only diagnostics for local verification, without images, prompts, or credentials.
    diagnostics() { return { enabled, open, live, configured, busy, lastCaptureAt, lastVisionAt, contextVersion, stateConnected:!!state, tipId:current?.id, cueTarget:highlight ? cueStage : null, model:current?.model, modelRoute:current?.modelRoute, planSource:current?.planSource, grounding:current?.grounding?.status }; }
  };
  document.addEventListener("visibilitychange", () => { if (document.hidden) { contextVersion++; controller?.abort(); rejectCapture(); } });
  window.addEventListener("resize", () => { updateSpotlight(); updateEvidenceBox(); });
  setInterval(syncPresentation, 1000);
  setInterval(() => { if (!document.hidden) refreshConfig(); }, 15000);
})();
