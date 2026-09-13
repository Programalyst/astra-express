(() => {
  "use strict";
  const avatar = `<svg class="astrabot-avatar" viewBox="0 0 86 96" aria-hidden="true"><defs><linearGradient id="astrabot-shell" x2=".8" y2="1"><stop stop-color="#dcf7e8"/><stop offset="1" stop-color="#7ebab1"/></linearGradient><linearGradient id="astrabot-visor" x2="0" y2="1"><stop stop-color="#284e59"/><stop offset="1" stop-color="#102a38"/></linearGradient></defs><ellipse cx="43" cy="88" rx="23" ry="4" fill="#05212e" opacity=".45"/><g class="astrabot-float"><path d="M43 20V11" stroke="#9cd4c5" stroke-width="3"/><circle cx="43" cy="8" r="4" fill="#ffd798"/><path d="M26 67q17-9 34 0l-4 13H30Z" fill="#78bcb0"/><path d="m34 79 9 6 9-6" fill="#b8f5d9"/><rect x="8" y="39" width="10" height="23" rx="4" fill="#edbc7f"/><rect x="68" y="39" width="10" height="23" rx="4" fill="#edbc7f"/><rect x="14" y="21" width="58" height="52" rx="20" fill="url(#astrabot-shell)" stroke="#dcffed" stroke-width="1.3"/><path d="M24 30q19-8 39 0" fill="none" stroke="#f2fff8" stroke-width="2" opacity=".7"/><rect x="20" y="33" width="46" height="29" rx="12" fill="url(#astrabot-visor)"/><rect class="astrabot-eye" x="29" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><rect class="astrabot-eye" x="51" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><path d="M39 55q4 3 8 0" fill="none" stroke="#70baa9" stroke-width="1.6" stroke-linecap="round"/><circle cx="58" cy="67" r="2" fill="#244e50"/><path d="M29 66h13" stroke="#d9f8e9" stroke-width="2" stroke-linecap="round"/></g></svg>`;
  const root = document.createElement("aside");
  root.id = "coach"; root.hidden = true; root.dataset.open = "false";
  root.setAttribute("aria-label", "AstraBot, colony copilot");
  root.innerHTML = `<section id="coach-panel" role="region" aria-label="AstraBot's coaching" inert>
    <header class="coach-header"><div><span class="coach-name">AstraBot</span><span class="coach-role">COLONY COPILOT</span></div><button id="coach-close" aria-label="Dismiss AstraBot">×</button></header>
    <div class="coach-body"><div class="coach-source" id="coach-source">Game-state tip</div><h2 id="coach-title">A little help, when you need it</h2><p id="coach-observation" hidden></p><p id="coach-copy"></p><ol id="coach-steps"></ol>
    </div><div class="coach-controls"><div class="coach-actions"><button id="coach-show">Show me</button><button id="coach-next">What next?</button></div>
    <button id="astrabot-task-open" class="coach-task">Give AstraBot a task</button>
    <form id="coach-form"><input id="coach-question" aria-label="Ask AstraBot a question" maxlength="300" placeholder="Ask about your colony…" autocomplete="off"><button id="coach-ask" type="submit">Ask</button></form></div>
    <footer class="coach-footer"><label><input type="checkbox" id="coach-live" checked> Live screen help</label><span id="coach-status" role="status">Connecting…</span></footer></section>
    <button id="coach-launcher" aria-label="Open AstraBot, your colony copilot" aria-expanded="false" aria-controls="coach-panel">${avatar}<span class="coach-teaser"><strong>ASTRABOT · YOUR COPILOT</strong><span id="coach-teaser-text">Need a hand?</span></span></button>`;
  document.body.append(root);
  const spot = document.createElement("div"); spot.id = "coach-spotlight"; spot.hidden = true;
  spot.innerHTML = '<div class="coach-cue-frame" aria-hidden="true"></div><div class="coach-cue-hint"><span id="coach-cue-text" role="status"></span><button id="coach-cue-close" aria-label="Dismiss guidance">×</button></div>';
  document.body.append(spot);
  const el = id => document.getElementById(id);
  function buttonArt(id, caption, name) {
    const button = el(id), art = document.createElement("img"), label = document.createElement("span");
    art.src = `icons/${name}.png`; art.alt = ""; art.draggable = false; art.className = "coach-button-art";
    label.textContent = caption; button.replaceChildren(art, label);
  }
  buttonArt("coach-close", "", "close"); buttonArt("coach-show", "Show me", "focus");
  buttonArt("coach-next", "What next?", "ask"); buttonArt("coach-ask", "Ask", "ask");
  buttonArt("astrabot-task-open", "Give AstraBot a task", "play");
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
  let shownGuide = "", cueStage = "";
  let events = [], previousState, question = "", live = prefs.get("astra.coach.live", "1") === "1";
  el("coach-live").checked = live;
  function updateEnabledControl() {
    enableButton.setAttribute("aria-checked", String(enabled));
    enableButton.querySelector(".astrabot-toggle-state").textContent = enabled ? "On" : "Off";
    enableButton.title = enabled ? "Turn the colony copilot off" : "Turn the colony copilot on";
    root.hidden = !enabled || !state;
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
  function blockInput() { send("CoachSetInputBlocked", open && (root.matches(":hover") || root.contains(document.activeElement)) ? "1" : "0"); }
  root.addEventListener("pointerenter", blockInput); root.addEventListener("pointerleave", blockInput);
  root.addEventListener("focusin", blockInput); root.addEventListener("focusout", () => queueMicrotask(blockInput));
  // The DOM panel must not trigger Unity shortcuts, clicks, zoom, or camera pan underneath.
  for (const name of ["keydown","keyup","pointerdown","pointerup","mousedown","mouseup","wheel"]) root.addEventListener(name, e => {
    e.stopPropagation();
    if (name === "keydown" && e.key === "Escape") { e.preventDefault(); setOpen(false); }
  });
  function setOpen(value, keepHighlight = false) {
    if (value && !enabled) return;
    open = value; root.dataset.open = String(value); el("coach-panel").inert = !value;
    el("coach-launcher").setAttribute("aria-expanded", String(value));
    if (value) { if (current) render(current); csrf = ""; refreshConfig().then(() => maybeAsk(true)); }
    else { contextVersion++; controller?.abort(); rejectCapture(); question = ""; if (!keepHighlight) clearHighlight(); prefs.set("astra.coach.dismissed", "1"); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2"); }
    blockInput();
  }
  el("coach-launcher").addEventListener("click", () => setOpen(!open));
  el("coach-close").addEventListener("click", () => setOpen(false));
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && open) { event.preventDefault(); event.stopImmediatePropagation(); setOpen(false); }
    else if (event.key === "Escape" && highlight) clearHighlight();
  }, true);
  el("coach-cue-close").addEventListener("pointerdown", event => event.stopPropagation());
  el("coach-cue-close").addEventListener("click", event => { event.stopPropagation(); clearHighlight(); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2"); });
  el("astrabot-task-open").addEventListener("click", () => {
    if (!enabled) return;
    if (!window.astraBotControl?.open) { status("Task controls are starting…"); return; }
    setOpen(false); window.astraBotControl.open();
  });
  el("coach-live").addEventListener("change", () => {
    live = el("coach-live").checked; prefs.set("astra.coach.live", live ? "1" : "0"); contextVersion++;
    if (!live) { controller?.abort(); rejectCapture(); render(candidates[0]); status("Screen reading off"); }
    else { csrf = ""; refreshConfig().then(() => maybeAsk(true)); }
  });
  function status(text) { el("coach-status").textContent = text; }
  function render(t, vision = false) {
    if (!t) return; const changedAction = current?.id !== t.id; current = t;
    el("coach-title").textContent = t.title; el("coach-copy").textContent = t.body;
    el("coach-observation").textContent = t.observation || ""; el("coach-observation").hidden = !t.observation;
    el("coach-steps").replaceChildren(...t.steps.map(text => { const li = document.createElement("li"); li.textContent = text; return li; }));
    el("coach-source").textContent = vision ? "Live screen + game state" : "Game-state tip";
    buttonArt("coach-show", t.uiTarget ? "Show next control" : t.link ? "Show connection" : "Show me", t.link ? (t.link.tool === "Rail" ? "rail" : "conduit") : "focus");
    el("coach-show").hidden = !t.target && !t.uiTarget; el("coach-teaser-text").textContent = t.title;
    if (changedAction) root.querySelector(".coach-body").scrollTop = 0;
  }
  function clearHighlight() { spot.hidden = true; highlight = null; shownGuide = ""; cueStage = ""; }
  function updateSpotlight() {
    if (!enabled || !highlight || Date.now() > highlightUntil || window.astraBotControl?.active()) { clearHighlight(); return; }
    const cue = candidates.find(t => t.id === highlight.id) || candidates[0];
    if (!cue) { clearHighlight(); return; }
    const stage = `${cue.id}:${cue.uiTarget || "world"}:${cue.target?.x},${cue.target?.y}`;
    const progressed = stage !== cueStage;
    if (progressed) { cueStage = stage; highlightUntil = Date.now() + 30000; }
    highlight = {id:cue.id};
    const rect = el("unity-canvas").getBoundingClientRect();
    if (!rect.width || !rect.height) { spot.hidden = true; return; }
    const anchor = state?.uiAnchors?.find(a => a.id === cue.uiTarget && a.visible !== false);
    let left, top, width, height, label;
    if (cue.uiTarget) {
      if (!anchor || ![anchor.x,anchor.y,anchor.width,anchor.height].every(Number.isFinite) || anchor.width <= 0 || anchor.height <= 0) { spot.hidden = true; return; }
      left = rect.left + anchor.x * rect.width - 4; top = rect.top + anchor.y * rect.height - 4;
      width = anchor.width * rect.width + 8; height = anchor.height * rect.height + 8;
      label = cue.cueLabel || anchor.label; spot.dataset.kind = "control";
    } else {
      if (!cue.target) { clearHighlight(); return; }
      if (cue.link && state.tool === cue.link.tool) {
        const guide = `${cue.link.tool},${cue.link.origin.x},${cue.link.origin.y}`;
        if (shownGuide !== guide) { shownGuide = guide; send("CoachGuideLink", guide); }
      }
      const points = [state?.frontier, state?.solarSite, state?.rover, state?.colonyPort,
        state?.plantSite, cue.target,
        ...(state?.buildings || []).flatMap(b => [b.origin,b.port]), ...(state?.deposits || []).map(d => d.origin)];
      const p = points.find(p => p && p.x === cue.target.x && p.y === cue.target.y);
      if (!p?.visible || ![p.screenX,p.screenY].every(Number.isFinite)) {
        if (progressed && !cue.link) send("CoachFocus", `${cue.target.x},${cue.target.y}`);
        spot.hidden = true; return;
      }
      width = height = 48; left = rect.left + p.screenX * rect.width - 24; top = rect.top + p.screenY * rect.height - 24;
      label = cue.targetLabel; spot.dataset.kind = "world";
    }
    spot.style.left = `${left}px`; spot.style.top = `${top}px`; spot.style.width = `${width}px`; spot.style.height = `${height}px`;
    el("coach-cue-text").textContent = label || "Look here"; spot.hidden = false;
    const hint = spot.querySelector(".coach-cue-hint");
    const hintWidth = hint.offsetWidth, hintHeight = hint.offsetHeight;
    hint.style.left = `${Math.max(8, Math.min(innerWidth - hintWidth - 8, left + width / 2 - hintWidth / 2))}px`;
    const below = top + height + 10;
    hint.style.top = `${Math.max(8, below + hintHeight < innerHeight - 12 && (spot.dataset.kind === "world" || top < hintHeight + 18) ? below : top - hintHeight - 10)}px`;
  }
  el("coach-show").addEventListener("click", () => {
    if (!current?.target && !current?.uiTarget) return;
    highlight = {id:current.id}; highlightUntil = Date.now() + 30000; cueStage = ""; shownGuide = "";
    if (!current.uiTarget && current.target && !current.target.visible) send("CoachFocus", `${current.target.x},${current.target.y}`);
    // Give the canvas its controls back immediately after showing a target.
    // Otherwise a pointer still over AstraBot can swallow the instructed number key.
    setOpen(false, true); updateSpotlight();
  });
  function askQuestion(text) {
    question = text;
    status(busy ? "Question queued · finishing this frame…" : "Question queued…");
    maybeAsk(true);
  }
  el("coach-next").addEventListener("click", () => askQuestion("What should I do next, and how?"));
  el("coach-form").addEventListener("submit", e => { e.preventDefault(); const input = el("coach-question"); if (!input.value.trim()) return; askQuestion(input.value.trim()); input.value = ""; });
  async function refreshConfig() {
    if (!enabled) return;
    try {
      const response = await fetch("/api/coach/config", { cache:"no-store" });
      if (!response.ok) throw new Error();
      const config = await response.json(); if (!enabled) return; configured = config.configured; csrf = config.token;
      if (!live) status("Screen reading off");
      else if (!configured) status("Vision waiting for server key");
      else if (!busy && !question) status(lastVisionAt ? "Watching while this panel is open" : "OpenAI ready · game screen only");
    } catch { configured = false; status("Game tips available · server offline"); }
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
    if (!enabled || !open || !live || window.astraBotControl?.active() || (!configured && !force) || !csrf || !state || !game || busy || document.hidden || Date.now()-lastStateAt > 3000) return;
    if (Date.now() - lastRequest < (force ? 4000 : 12000)) return;
    if (!force && fingerprint === lastVisionSignature && Date.now() - lastVisionAt < 45000) return;
    busy = true; lastRequest = Date.now(); const version = contextVersion, signature = fingerprint;
    const sentState = state, allowed = candidates, asked = question;
    controller = new AbortController(); const requestController = controller;
    const timeout = setTimeout(() => requestController.abort(), 25000);
    status(asked ? "Reading the screen to answer you…" : "Reading your game screen…");
    try {
      const image = await capture();
      if (!enabled || !open || !live || version !== contextVersion) return;
      const response = await fetch("/api/coach", { method:"POST", signal:requestController.signal,
        headers:{ "Content-Type":"application/json", "X-Astra-Coach":csrf },
        body:JSON.stringify({ image, state:sentState, candidates:allowed, events:events.slice(-8), question:asked, contextVersion:version }) });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error || "Vision temporarily unavailable");
      if (!enabled || !open || !live || document.hidden || version !== contextVersion || Date.now()-lastStateAt > 3000) return;
      if (question && question !== asked) return;
      const chosen = candidates.find(c => c.id === result.actionId);
      if (!chosen) return;
      // Model explains the screen; canonical game-validated steps remain exact.
      render({ ...chosen, title:result.title, body:result.body, observation:result.observation }, true);
      if (question === asked) question = "";
      lastVisionAt = Date.now(); lastVisionSignature = signature;
      status("Just read your game screen");
    } catch(error) {
      if (version === contextVersion && open && live) {
        render(candidates[0]);
        if (asked && question === asked) { if (!el("coach-question").value) el("coach-question").value = asked; question = ""; }
        status(error.name === "AbortError" ? "Vision timed out · game tip shown" : String(error.message).slice(0,95));
      }
    } finally {
      clearTimeout(timeout); busy = false; if (controller === requestController) controller = null;
      if (open && live && version !== contextVersion) status(question ? "Colony changed · refreshing your answer…" : "Colony changed · current game tip shown");
      if (question) maybeAsk(true);
    }
  }
  window.astraCoach = {
    enabled() { return enabled; },
    ready(instance) { game = instance; refreshConfig(); },
    receive(next) {
      const changedSession = state && next.session !== state.session;
      if (changedSession) { events = []; previousState = null; contextVersion++; controller?.abort(); lastVisionSignature = ""; lastVisionAt = 0; clearHighlight(); }
      if (previousState && (next.message !== previousState.message || next.tool !== previousState.tool || next.deliveries !== previousState.deliveries))
        events.push({ at:Math.round(next.elapsed), tool:next.tool, message:next.message, credits:next.credits, deliveries:next.deliveries });
      events = events.slice(-8); previousState = next; state = next; lastStateAt = Date.now();
      window.dispatchEvent(new CustomEvent("astra:state", {detail:next}));
      candidates = AstraCoachPolicy.advise(next); const key = AstraCoachPolicy.signature(next, candidates);
      if (key !== fingerprint) { fingerprint = key; contextVersion++; render(candidates[0]); }
      root.hidden = !enabled; document.documentElement.style.setProperty("--game-scale", Math.min(innerWidth/1280, innerHeight/720));
      updateSpotlight(); maybeAsk();
    },
    screenReady(id, jpeg) {
      if (!pendingCapture || pendingCapture.id !== id) return;
      const pending = pendingCapture; pendingCapture = null; clearTimeout(pending.timer);
      if (!jpeg) pending.reject(new Error("Could not read this frame"));
      else { lastCaptureAt = Date.now(); pending.resolve("data:image/jpeg;base64," + jpeg); }
    },
    // Read-only diagnostics for local verification, without images, prompts, or credentials.
    diagnostics() { return { enabled, open, live, configured, busy, lastCaptureAt, lastVisionAt, contextVersion, stateConnected:!!state, tipId:current?.id, cueTarget:highlight ? cueStage : null }; }
  };
  document.addEventListener("visibilitychange", () => { if (document.hidden) { contextVersion++; controller?.abort(); rejectCapture(); } });
  window.addEventListener("resize", updateSpotlight);
  setInterval(() => { if (open && !document.hidden) maybeAsk(); if (highlight) updateSpotlight(); }, 1000);
  setInterval(() => { if (!document.hidden) refreshConfig(); }, 15000);
})();
