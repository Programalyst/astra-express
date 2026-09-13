(() => {
  "use strict";
  const model = "gpt-5.4-mini", endpoint = "https://api.openai.com/v1";
  const contract = window.astraBotContract, planner = window.astraBotPlanning;
  const requestTimes = [];
  let tabKey = "", revision = 0, connecting, inference;
  const changed = () => document.dispatchEvent(new CustomEvent("astra:credentials"));
  const cancelled = () => Object.assign(new Error("Connection cancelled."), {name:"AbortError"});
  const reply = (data, status = 200) => ({ok:status >= 200 && status < 300, status, json:async () => data});
  const failure = status => status === 401 ? "OpenAI rejected this key. Check it or create a new one." : status === 403 ? "This key needs access to the model. Check its project permissions." : status === 429 ? "OpenAI rate or credit limit reached. Check your API budget or try later." : status === 404 ? "This key cannot access the configured model." : status === 400 ? "OpenAI rejected the game request. Check model compatibility." : "OpenAI could not finish the request. Try again shortly.";
  async function config(options = {}) {
    if (options.signal?.aborted) throw cancelled();
    return {configured:!!tabKey, tabKey:!!tabKey, serverConfigured:false, acceptsTabKey:true, plannerAvailable:true, model, engine:"responses-api", token:"direct-browser"};
  }
  async function callOpenAI(path, key, options) {
    try {
      return await fetch(endpoint + path, {...options, headers:{"Authorization":"Bearer " + key, ...(options.body ? {"Content-Type":"application/json"} : {})}, cache:"no-store", credentials:"omit", redirect:"error", referrerPolicy:"no-referrer"});
    } catch {
      if (options.signal.aborted) throw cancelled();
      throw new Error("Cannot reach OpenAI from this browser. Check your connection or browser network restrictions.");
    }
  }
  async function request(path, options = {}) {
    if (!["/api/coach", "/api/astrabot/plan"].includes(path) || options.method !== "POST") throw new Error("Unsupported AstraBot request.");
    if (options.signal?.aborted) throw cancelled();
    if (!tabKey) return reply({error:"Add an OpenAI key in AstraBot settings first."}, 401);
    if (inference) return reply({error:"AstraBot is already reading a screen"}, 429);
    if (typeof options.body !== "string" || options.body.length > 3100000) throw new Error("Invalid game request.");
    let data;
    try { data = JSON.parse(options.body); } catch { throw new Error("Invalid game request."); }
    const planning = path === "/api/astrabot/plan";
    planner.validateInput(data, planning);
    const now = Date.now();
    while (requestTimes.length && now - requestTimes[0] >= 3600000) requestTimes.shift();
    if (requestTimes.length >= 120) return reply({error:"Screen-reading limit reached: 120 requests per hour in this tab."}, 429);
    requestTimes.push(now);
    if (planning) data = planner.prepare(data);
    const fields = planning ? ["goal", "selectedTile", "state", "previousPlan", "progress", "serverProgress"] : ["state", "candidates", "events", "question"];
    const context = Object.fromEntries(fields.filter(field => data[field] !== undefined).map(field => [field, data[field]]));
    const active = new AbortController(), version = revision;
    inference = active;
    const abort = () => active.abort();
    options.signal?.addEventListener("abort", abort, {once:true});
    const timer = setTimeout(abort, planning ? 55000 : 22000);
    const checkActive = () => { if (active.signal.aborted || version !== revision) throw cancelled(); };
    try {
      const response = await callOpenAI("/responses", tabKey, {method:"POST", signal:active.signal, body:JSON.stringify({
        model, store:false, stream:false, tools:[], reasoning:{effort:"none"}, max_output_tokens:planning ? 2400 : 450,
        instructions:planning ? contract.plannerRules + "\nGAME RULES:\n" + contract.rules : contract.rules,
        input:[{role:"user", content:[{type:"input_text", text:JSON.stringify(context)}, {type:"input_image", image_url:data.image, detail:"low"}]}],
        text:{format:{type:"json_schema", name:planning ? "astra_plan" : "astra_advice", strict:true, schema:planning ? contract.planSchema : contract.adviceSchema}}
      })});
      checkActive();
      if (!response.ok) return reply({error:failure(response.status)}, response.status);
      let envelope;
      try { envelope = await response.json(); } catch { throw new Error("OpenAI returned an unreadable response. Try again."); }
      checkActive();
      if (envelope.status !== "completed" || !Array.isArray(envelope.output)) throw new Error("OpenAI did not complete this answer. Try again.");
      const messages = envelope.output.filter(item => item.type === "message" && item.role === "assistant");
      if (messages.some(item => item.content?.some(part => part.type === "refusal"))) throw new Error("OpenAI declined this request. Try a different game goal.");
      const output = messages.flatMap(item => item.content ?? []).filter(part => part.type === "output_text").map(part => part.text).join("");
      if (!output || output.length > 16000) throw new Error("OpenAI returned an invalid game response. Try again.");
      let value;
      try { value = JSON.parse(output); } catch { throw new Error("OpenAI returned invalid game JSON. Try again."); }
      const result = planner.parseResult(value, data, planning);
      if (planning) planner.remember(data, result);
      return reply(result);
    } finally {
      clearTimeout(timer); options.signal?.removeEventListener("abort", abort);
      if (inference === active) inference = null;
    }
  }
  async function connect(value) {
    const key = String(value).trim();
    if (!/^sk-[A-Za-z0-9_-]{16,508}$/.test(key)) throw new Error("Paste the API key only. It starts with sk-; leave out quotes and variable names.");
    connecting?.abort(); const controller = new AbortController(); connecting = controller;
    const timer = setTimeout(() => controller.abort(), 15000);
    try {
      const response = await callOpenAI("/models/" + model, key, {method:"GET", signal:controller.signal});
      if (controller.signal.aborted || connecting !== controller) throw cancelled();
      if (!response.ok) throw new Error(failure(response.status));
      let result;
      try { result = await response.json(); } catch { throw new Error("OpenAI could not verify model access."); }
      if (controller.signal.aborted || connecting !== controller) throw cancelled();
      if (result.id !== model) throw new Error("OpenAI could not verify model access.");
      revision++; inference?.abort(); tabKey = key; planner.clear(); changed();
    } finally { clearTimeout(timer); if (connecting === controller) connecting = null; }
  }
  function cancelConnection() { connecting?.abort(); connecting = null; }
  function forget() { cancelConnection(); revision++; inference?.abort(); tabKey = ""; planner.clear(); changed(); }
  window.addEventListener("pagehide", forget);
  window.astraBotAPI = Object.freeze({config, request, connect, forget, cancelConnection, hasTabKey:() => !!tabKey});
})();
