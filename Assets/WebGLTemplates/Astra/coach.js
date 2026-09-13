(() => {
  "use strict";
  const avatar = `<svg class="pip-avatar" viewBox="0 0 86 96" aria-hidden="true"><defs><linearGradient id="pip-shell" x2=".8" y2="1"><stop stop-color="#dcf7e8"/><stop offset="1" stop-color="#7ebab1"/></linearGradient><linearGradient id="pip-visor" x2="0" y2="1"><stop stop-color="#284e59"/><stop offset="1" stop-color="#102a38"/></linearGradient></defs><ellipse cx="43" cy="88" rx="23" ry="4" fill="#05212e" opacity=".45"/><g class="pip-float"><path d="M43 20V11" stroke="#9cd4c5" stroke-width="3"/><circle cx="43" cy="8" r="4" fill="#ffd798"/><path d="M26 67q17-9 34 0l-4 13H30Z" fill="#78bcb0"/><path d="m34 79 9 6 9-6" fill="#b8f5d9"/><rect x="8" y="39" width="10" height="23" rx="4" fill="#edbc7f"/><rect x="68" y="39" width="10" height="23" rx="4" fill="#edbc7f"/><rect x="14" y="21" width="58" height="52" rx="20" fill="url(#pip-shell)" stroke="#dcffed" stroke-width="1.3"/><path d="M24 30q19-8 39 0" fill="none" stroke="#f2fff8" stroke-width="2" opacity=".7"/><rect x="20" y="33" width="46" height="29" rx="12" fill="url(#pip-visor)"/><rect class="pip-eye" x="29" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><rect class="pip-eye" x="51" y="42" width="6" height="10" rx="3" fill="#a7f9dc"/><path d="M39 55q4 3 8 0" fill="none" stroke="#70baa9" stroke-width="1.6" stroke-linecap="round"/><circle cx="58" cy="67" r="2" fill="#244e50"/><path d="M29 66h13" stroke="#d9f8e9" stroke-width="2" stroke-linecap="round"/></g></svg>`;
  const root = document.createElement("aside");
  root.id = "coach"; root.hidden = true; root.dataset.open = "false";
  root.setAttribute("aria-label", "Pip, colony copilot");
  root.innerHTML = `<section id="coach-panel" role="region" aria-label="Pip's coaching" inert>
    <header class="coach-header"><div><span class="coach-name">Pip</span><span class="coach-role">COLONY COPILOT</span></div><button id="coach-close" aria-label="Dismiss Pip">×</button></header>
    <div class="coach-body"><div class="coach-source" id="coach-source">Game-state tip</div><h2 id="coach-title">A little help, when you need it</h2><p id="coach-observation" hidden></p><p id="coach-copy"></p><ol id="coach-steps"></ol>
    </div><div class="coach-controls"><div class="coach-actions"><button id="coach-show">Show me</button><button id="coach-next">What next?</button></div>
    <form id="coach-form"><input id="coach-question" aria-label="Ask Pip a question" maxlength="300" placeholder="Ask about your colony…" autocomplete="off"><button id="coach-ask" type="submit">Ask</button></form></div>
    <footer class="coach-footer"><label><input type="checkbox" id="coach-live" checked> Live screen help</label><span id="coach-status" role="status">Connecting…</span></footer></section>
    <button id="coach-launcher" aria-label="Open Pip, your colony copilot" aria-expanded="false" aria-controls="coach-panel">${avatar}<span class="coach-teaser"><strong>PIP · YOUR COPILOT</strong><span id="coach-teaser-text">Need a hand?</span></span></button>`;
  document.body.append(root);
  const spot = document.createElement("div"); spot.id = "coach-spotlight"; spot.hidden = true; spot.innerHTML = "<span>Look here</span>"; document.body.append(spot);
  const el = id => document.getElementById(id);
  function buttonArt(id, caption, name) {
    const button = el(id), art = document.createElement("img"), label = document.createElement("span");
    art.src = `icons/${name}.png`; art.alt = ""; art.draggable = false; art.className = "coach-button-art";
    label.textContent = caption; button.replaceChildren(art, label);
  }
  buttonArt("coach-close", "", "close"); buttonArt("coach-show", "Show me", "focus");
  buttonArt("coach-next", "What next?", "ask"); buttonArt("coach-ask", "Ask", "ask");
  const prefs = { get(k, d) { try { return localStorage.getItem(k) ?? d; } catch { return d; } }, set(k,v) { try { localStorage.setItem(k,v); } catch {} } };
  let game, state, candidates = [], current, fingerprint = "", contextVersion = 0, open = false, requestNumber = 0;
  let configured = false, csrf = "", busy = false, lastRequest = 0, lastStateAt = 0, controller, pendingCapture;
  let lastVisionAt = 0, lastCaptureAt = 0, lastVisionSignature = "", highlight = null, highlightUntil = 0;
  let events = [], previousState, question = "", live = prefs.get("astra.coach.live", "1") === "1";
  el("coach-live").checked = live;
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
    open = value; root.dataset.open = String(value); el("coach-panel").inert = !value;
    el("coach-launcher").setAttribute("aria-expanded", String(value));
    if (value) { if (current) render(current); refreshConfig(); maybeAsk(true); }
    else { contextVersion++; controller?.abort(); rejectCapture(); question = ""; if (!keepHighlight) { spot.hidden = true; highlight = null; } prefs.set("astra.coach.dismissed", "1"); el("unity-canvas").focus(); send("CoachSetInputBlocked", "2"); }
    blockInput();
  }
  el("coach-launcher").addEventListener("click", () => setOpen(!open));
  el("coach-close").addEventListener("click", () => setOpen(false));
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && open) { event.preventDefault(); event.stopImmediatePropagation(); setOpen(false); }
  }, true);
  el("coach-live").addEventListener("change", () => {
    live = el("coach-live").checked; prefs.set("astra.coach.live", live ? "1" : "0"); contextVersion++;
    if (!live) { controller?.abort(); rejectCapture(); render(candidates[0]); status("Screen reading off"); }
    else { refreshConfig(); maybeAsk(true); }
  });
  function status(text) { el("coach-status").textContent = text; }
  function render(t, vision = false) {
    if (!t) return; const changedAction = current?.id !== t.id; current = t;
    el("coach-title").textContent = t.title; el("coach-copy").textContent = t.body;
    el("coach-observation").textContent = t.observation || ""; el("coach-observation").hidden = !t.observation;
    el("coach-steps").replaceChildren(...t.steps.map(text => { const li = document.createElement("li"); li.textContent = text; return li; }));
    el("coach-source").textContent = vision ? "Live screen + game state" : "Game-state tip";
    buttonArt("coach-show", t.link ? "Show connection" : "Show me", t.link ? (t.link.tool === "Rail" ? "rail" : "conduit") : "focus");
    el("coach-show").hidden = !t.target; el("coach-teaser-text").textContent = t.title;
    if (changedAction) root.querySelector(".coach-body").scrollTop = 0;
  }
  function updateSpotlight() {
    if (!highlight || Date.now() > highlightUntil) { spot.hidden = true; return; }
    const points = [state?.frontier, state?.solarSite, state?.rover, state?.colonyPort,
      ...(state?.buildings || []).flatMap(b => [b.origin,b.port]), ...(state?.deposits || []).map(d => d.origin)];
    const p = points.find(p => p && p.x === highlight.x && p.y === highlight.y);
    if (!p?.visible) { spot.hidden = true; return; }
    const rect = el("unity-canvas").getBoundingClientRect();
    spot.style.left = `${rect.left + p.screenX * rect.width}px`; spot.style.top = `${rect.top + p.screenY * rect.height}px`; spot.hidden = false;
  }
  el("coach-show").addEventListener("click", () => {
    if (current?.link) {
      send("CoachGuideLink", `${current.link.tool},${current.link.origin.x},${current.link.origin.y}`);
      setOpen(false);
      return;
    }
    if (!current?.target) return; highlight = current.target; highlightUntil = Date.now() + 7000;
    if (!highlight.visible) send("CoachFocus", `${highlight.x},${highlight.y}`);
    // Give the canvas its controls back immediately after showing a target.
    // Otherwise a pointer still over Pip can swallow the instructed number key.
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
    try {
      const response = await fetch("/api/coach/config", { cache:"no-store" });
      if (!response.ok) throw new Error();
      const config = await response.json(); configured = config.configured; csrf = config.token;
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
    if (!open || !live || (!configured && !force) || !csrf || !state || !game || busy || document.hidden || Date.now()-lastStateAt > 3000) return;
    if (Date.now() - lastRequest < (force ? 4000 : 12000)) return;
    if (!force && fingerprint === lastVisionSignature && Date.now() - lastVisionAt < 45000) return;
    busy = true; lastRequest = Date.now(); const version = contextVersion, signature = fingerprint;
    const sentState = state, allowed = candidates, asked = question;
    controller = new AbortController(); const requestController = controller;
    const timeout = setTimeout(() => requestController.abort(), 25000);
    status(asked ? "Reading the screen to answer you…" : "Reading your game screen…");
    try {
      const image = await capture();
      if (!open || !live || version !== contextVersion) return;
      const response = await fetch("/api/coach", { method:"POST", signal:requestController.signal,
        headers:{ "Content-Type":"application/json", "X-Astra-Coach":csrf },
        body:JSON.stringify({ image, state:sentState, candidates:allowed, events:events.slice(-8), question:asked, contextVersion:version }) });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error || "Vision temporarily unavailable");
      if (!open || !live || document.hidden || version !== contextVersion || Date.now()-lastStateAt > 3000) return;
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
    ready(instance) { game = instance; refreshConfig(); },
    receive(next) {
      const changedSession = state && next.session !== state.session;
      if (changedSession) { events = []; previousState = null; contextVersion++; controller?.abort(); lastVisionSignature = ""; lastVisionAt = 0; highlight = null; }
      if (previousState && (next.message !== previousState.message || next.tool !== previousState.tool || next.deliveries !== previousState.deliveries))
        events.push({ at:Math.round(next.elapsed), tool:next.tool, message:next.message, credits:next.credits, deliveries:next.deliveries });
      events = events.slice(-8); previousState = next; state = next; lastStateAt = Date.now();
      candidates = AstraCoachPolicy.advise(next); const key = AstraCoachPolicy.signature(next, candidates);
      if (key !== fingerprint) { fingerprint = key; contextVersion++; render(candidates[0]); }
      root.hidden = false; document.documentElement.style.setProperty("--game-scale", Math.min(innerWidth/1280, innerHeight/720));
      updateSpotlight(); maybeAsk();
    },
    screenReady(id, jpeg) {
      if (!pendingCapture || pendingCapture.id !== id) return;
      const pending = pendingCapture; pendingCapture = null; clearTimeout(pending.timer);
      if (!jpeg) pending.reject(new Error("Could not read this frame"));
      else { lastCaptureAt = Date.now(); pending.resolve("data:image/jpeg;base64," + jpeg); }
    },
    // Read-only diagnostics for local verification, without images, prompts, or credentials.
    diagnostics() { return { open, live, configured, busy, lastCaptureAt, lastVisionAt, contextVersion, stateConnected:!!state, tipId:current?.id }; }
  };
  document.addEventListener("visibilitychange", () => { if (document.hidden) { contextVersion++; controller?.abort(); rejectCapture(); } });
  setInterval(() => { if (open && !document.hidden) maybeAsk(); if (highlight) updateSpotlight(); }, 1000);
  setInterval(() => { if (!document.hidden) refreshConfig(); }, 15000);
})();
