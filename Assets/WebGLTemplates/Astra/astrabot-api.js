(() => {
  "use strict";
  const requiredModel = "gpt-6-astra";
  // A pasted key lives only in this closure. Never put it in storage, URLs,
  // diagnostics, the Unity state, or a model prompt.
  let tabKey = "", revision = 0, connecting;
  const changed = () => document.dispatchEvent(new CustomEvent("astra:credentials"));
  const cancelled = () => Object.assign(new Error("Connection cancelled."), {name:"AbortError"});
  async function config(options = {}) {
    const response = await fetch("/api/coach/config", {...options, cache:"no-store", credentials:"same-origin", redirect:"error"});
    if (!response.ok) throw new Error("AstraBot server unavailable. Start the game with Run Local.command.");
    const data = await response.json();
    if (typeof data.token !== "string" || data.engine !== "agents-api") throw new Error("This game needs the AstraBot Agents API server.");
    if (data.model !== requiredModel) throw new Error(`AstraBot server must use ${requiredModel}.`);
    return {...data, serverConfigured:!!data.configured, configured:!!tabKey || !!data.configured, tabKey:!!tabKey};
  }
  async function request(path, options = {}) {
    if (!["/api/coach", "/api/astrabot/plan", "/api/astrabot/chat"].includes(path) || options.method !== "POST") throw new Error("Unsupported AstraBot request.");
    const headers = {...options.headers};
    if (tabKey) headers["X-Astra-OpenAI-Key"] = tabKey;
    return fetch(path, {...options, headers, cache:"no-store", credentials:"same-origin", redirect:"error"});
  }
  async function connect(value) {
    const key = String(value).trim();
    if (!/^sk-[A-Za-z0-9_-]{16,508}$/.test(key)) throw new Error("Paste the API key only. It starts with sk-; leave out quotes and variable names.");
    connecting?.abort(); const controller = new AbortController(); connecting = controller;
    const version = ++revision, timer = setTimeout(() => controller.abort(), 15000);
    try {
      const current = await config({signal:controller.signal});
      if (!current.acceptsTabKey) throw new Error("Update the AstraBot server to enable pasted keys.");
      if (version !== revision || controller.signal.aborted) throw cancelled();
      const response = await fetch("/api/coach/key", {method:"POST", signal:controller.signal, credentials:"same-origin", redirect:"error", cache:"no-store",
        headers:{"Content-Type":"application/json", "X-Astra-Coach":current.token, "X-Astra-OpenAI-Key":key}, body:"{}"});
      if (version !== revision || controller.signal.aborted) throw cancelled();
      if (!response.ok) throw new Error(response.status === 401 ? "OpenAI rejected this key. Check it or create a new one." : response.status === 403 ? `This key needs access to ${requiredModel}. Check its project permissions.` : response.status === 429 ? "Too many attempts or an OpenAI limit. Try again shortly." : "Could not verify this key. Check your connection and try again.");
      const result = await response.json();
      if (version !== revision || controller.signal.aborted) throw cancelled();
      if (result.verified !== true) throw new Error("The server could not verify this key.");
      tabKey = key; changed();
    } finally { clearTimeout(timer); if (connecting === controller) connecting = null; }
  }
  function cancelConnection() { revision++; connecting?.abort(); connecting = null; }
  function forget() { cancelConnection(); tabKey = ""; changed(); }
  window.addEventListener("pagehide", forget);
  window.astraBotAPI = Object.freeze({config, request, connect, forget, cancelConnection, hasTabKey:() => !!tabKey});
})();
