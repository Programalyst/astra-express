(() => {
  "use strict";
  const api = window.astraBotAPI;
  const button = document.createElement("button"); button.id = "astrabot-settings-open"; button.type = "button";
  button.setAttribute("aria-label", "AstraBot settings"); button.setAttribute("aria-expanded", "false"); button.setAttribute("aria-controls", "astrabot-settings"); button.title = "AstraBot settings";
  button.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m9 3-.6 2.1-1.8 1L4.5 5.6l-3 5.2L3 12.3v2l-1.5 1.5 3 5.2 2.1-.5 1.8 1L9 23h6l.6-1.5 1.8-1 2.1.5 3-5.2-1.5-1.5v-2l1.5-1.5-3-5.2-2.1.5-1.8-1L15 3Z" transform="translate(1 0) scale(.9)"/><circle cx="12" cy="12" r="3.4"/></svg>';
  const panel = document.createElement("section"); panel.id = "astrabot-settings"; panel.hidden = true;
  panel.setAttribute("role", "dialog"); panel.setAttribute("aria-modal", "false"); panel.setAttribute("aria-labelledby", "astrabot-settings-title");
  panel.innerHTML = `<header><div><span class="settings-eyebrow">COLONY COPILOT</span><h2 id="astrabot-settings-title">Connect AstraBot</h2></div><button type="button" id="astrabot-settings-close" aria-label="Close AstraBot settings">×</button></header>
    <p id="astrabot-key-source" class="settings-source" role="status">Checking connection…</p>
    <ol class="settings-steps"><li><a href="https://platform.openai.com/api-keys" target="_blank" rel="noopener noreferrer">Create an OpenAI API key ↗</a><span>Add API credit to that project.</span></li><li>Paste the key below and connect.</li><li>Turn Copilot on and open AstraBot.</li></ol>
    <form id="astrabot-key-form" autocomplete="off"><label for="astrabot-key">OpenAI API key</label><input id="astrabot-key" type="password" placeholder="sk-…" maxlength="512" autocomplete="off" autocapitalize="off" spellcheck="false" aria-describedby="astrabot-key-privacy"><div class="settings-actions"><button id="astrabot-key-connect" type="submit">Connect for this tab</button><button id="astrabot-key-forget" type="button" hidden>Forget key</button></div></form>
    <p id="astrabot-key-status" role="status" aria-live="polite"></p>
    <p id="astrabot-key-privacy" class="settings-note">Tab only · reload or Forget key clears it. Sent through this game’s server to OpenAI; never saved to files or browser storage. Use a restricted, low-budget key, not a production key.</p>
    <p class="settings-note">Live help sends your game view and state to OpenAI. API usage is billed to the key owner, separately from ChatGPT.</p>`;
  document.body.append(button, panel);
  const el = id => document.getElementById(id);
  let active = 0;
  const send = value => window.unityInstance?.SendMessage("Astra Express", "CoachSetInputBlocked", value);
  function block() { send(!panel.hidden && (panel.matches(":hover") || panel.contains(document.activeElement)) ? "1" : "0"); }
  function message(text) { el("astrabot-key-status").textContent = text; }
  async function refresh() {
    try {
      const config = await api.config();
      const engine = config.engine === "agents-api" ? "Agents API" : config.engine;
      el("astrabot-key-source").textContent = config.tabKey ? `Connected · ${config.model} via ${engine} · tab key` : config.serverConfigured ? `Ready · ${config.model} via ${engine} · server key` : "Local game tips work without a key";
      el("astrabot-key-forget").hidden = !api.hasTabKey();
    } catch { el("astrabot-key-source").textContent = "Coach server unavailable · local game tips still work"; }
  }
  function show(value) {
    if (value) {
      document.dispatchEvent(new CustomEvent("astra:settings", {detail:true}));
      panel.hidden = false; button.setAttribute("aria-expanded", "true"); refresh(); el("astrabot-key").focus();
    } else {
      active++; api.cancelConnection(); el("astrabot-key").value = ""; panel.hidden = true; button.setAttribute("aria-expanded", "false");
      button.focus(); send("2");
    }
    block();
  }
  button.onclick = () => show(panel.hidden);
  el("astrabot-settings-close").onclick = () => show(false);
  panel.addEventListener("pointerenter", block); panel.addEventListener("pointerleave", block);
  panel.addEventListener("focusin", block); panel.addEventListener("focusout", () => queueMicrotask(block));
  for (const target of [panel, button]) for (const name of ["keydown", "keyup", "pointerdown", "pointerup", "mousedown", "mouseup", "wheel"]) target.addEventListener(name, event => {
    event.stopPropagation();
    if (name === "keydown" && event.key === "Escape") { event.preventDefault(); show(false); }
  });
  el("astrabot-key-form").onsubmit = async event => {
    event.preventDefault(); const version = ++active;
    let value = el("astrabot-key").value; el("astrabot-key").value = "";
    el("astrabot-key-connect").disabled = true; message("Checking access to both routed models · no generation…");
    try {
      const pending = api.connect(value); value = ""; await pending;
      if (version === active) message("Connected. Open AstraBot when you’re ready.");
    } catch (error) { if (version === active) message(error.name === "AbortError" ? "Connection cancelled or timed out. Try again." : error.message); }
    finally { value = ""; el("astrabot-key-connect").disabled = false; refresh(); }
  };
  el("astrabot-key-forget").onclick = () => {
    active++; api.forget(); el("astrabot-key").value = "";
    message("Tab key cleared. A configured server key can still be used; switch Copilot off to stop help."); refresh();
  };
  document.addEventListener("astra:credentials", refresh);
  window.addEventListener("pagehide", () => { el("astrabot-key").value = ""; });
})();
