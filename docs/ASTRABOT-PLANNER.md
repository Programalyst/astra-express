# AstraBot game planner

## Current Web transport

The current Web template sends `POST /api/astrabot/plan` to the same-origin Python server. Pure rover exploration, fog-reveal and ore-discovery goals route to `gpt-5.4-mini`; goals involving buildings, extractors, solar, power, conduits, rail, trains or other ambiguous work route to `gpt-6-astra`. Both models run through hosted OpenAI Agents API sessions. A narrowly recognized Ore-and-solar goal can continue with a server-derived game-state step after a matching hosted batch has been completed and verified; the conditions are described below. Neither path executes actions itself: the browser's game-scoped adapter owns start/stop, visible execution and simulation acknowledgments. This is not OpenAI's native computer-use tool and provides no desktop, browser navigation, shell or arbitrary-code control.

`astrabot-planning.js` still provides defense-in-depth validation of the bounded action schema and economic constraints. Review/Start/Stop and Unity's runtime checks remain authoritative. The generated browser contract matches the Python rules and schemas.

The hosted API also supports [application function tools](https://developers.openai.com/api/docs/guides/agents-api/tools/functions), using `requires_action` and `tool_result` events. This implementation deliberately uses a structured plan/replan contract instead of claiming that a JSON plan is a native tool call. It runs with [no hosted execution environment](https://developers.openai.com/api/docs/guides/agents-api/architecture), no tools and no subagents.

## Protocol

Read `GET /api/astrabot/config` (an alias of `/api/coach/config`) and send its local token in `X-Astra-Coach`, with the same local-origin policy as AstraBot. Configuration adds `plannerAvailable: true` and a `routing` map; it never exposes the OpenAI key. Its process-local diagnostics include per-route counts and `stats.lastPlanTiming`, whose `source`, `model` and `route` identify the latest successful plan. `durationMs` covers server handling only. These counters and timings are diagnostics, not an end-to-end latency benchmark.

Request fields:

- `goal`: 1–600 characters.
- `image`: current JPEG data URL, using the same size/format checks as coaching.
- `state`: current bounded game state, including its game session identifier.
- `selectedTile`: `{x,y}` or null; the explicit reference for “here.”
- `previousPlan`: null or an object up to 60KB, with at most six actions and 60 execution-result records. Result statuses such as `complete`, `failed` and `cancelled` are reported progress, not instructions.
- `progress`: optional additional bounded progress object/array up to 12KB.

Response fields:

```json
{
  "planId": "server-generated identifier",
  "planSource": "agents-api",
  "model": "gpt-6-astra",
  "modelRoute": "advanced-visual",
  "title": "Next game actions",
  "summary": "What this batch will achieve",
  "status": "ready",
  "actions": [{
    "id": "step-1", "type": "explore",
    "x": 10, "y": 6,
    "targetX": null, "targetY": null,
    "trainIndex": null, "seconds": null,
    "reason": "Reveal the ground beside the selected tile."
  }],
  "nextCheck": "Replan after the rover reaches its target."
}
```

`ready` requires 1–6 actions. `complete` and `blocked` contain no actions. Completion is a model judgment about the current state, not proof that proposed steps ran. The executor must inspect acknowledgments and fresh state. A server error contains no executable plan and starts no actions.

`planSource` distinguishes a hosted `agents-api` result from a verified local `game-state` continuation. `model` and `modelRoute` are server-owned evidence of the selected route; hosted routes are `rover-exploration` or `advanced-visual`, while a safe deterministic continuation reports `verified-game-state`. For a recognized extractor-expansion goal, the response also includes `goalProgress`. This is derived from the latest game state and reports the fixed starting count, requested new or total count, connected target mines, rated extractor demand, connected solar generation and whether the goal is verified complete.

Every action has the same fields; unused fields are null. The output schema uses separate action branches so irrelevant destination fields are forbidden:

| Type | Target |
|---|---|
| `explore`, `select`, `build_extractor`, `build_solar`, `build_plant` | `x,y`: target tile/origin. `select` may instead use `trainIndex`. |
| `connect_conduit`, `connect_rail` | `x,y`: building origin or south port. The game computes the route from the colony depot. `targetX/Y` are null. |
| `dispatch_train` | `x,y`: extractor. `targetX/Y`: explicit plant origin for Fluxite, null for ore. |
| `pause_mine`, `resume_mine` | `x,y`: existing extractor. |
| `auto_explore` | No target or duration. Survey reachable revealed frontiers for at most 55 seconds, stopping on new Ore. |
| `buy_train`, `resume`, `stop` | No target. |
| `wait` | No target; integer `seconds` from 1–20. |

Both exploration actions and stop end their batch. Newly discovered resources require a fresh frame before planning their extraction. Unsupported actions such as reset, refund, building sale, extra colony depots and arbitrary code are rejected.

## Validation and progress

The server validates coordinates, exact action fields, duplicate IDs, current revealed extractor targets, known sites, known building/deposit overlap, known route failures/costs, available locomotives, the four-train cap, Fluxite destinations and current credits. Earlier proposed building/link/train actions are considered when checking a later action in the same batch. Future income is never counted as current budget. Route costs for newly proposed buildings and the full terrain/occupancy simulation are checked again by the game adapter immediately before execution; this backend is not a second copy of the simulation.

A goal also retains the Ore origins visible when it began (including existing ore extractors). `serverProgress.initialVisibleOreOrigins` and `newVisibleOreOrigins` distinguish earlier discoveries from newly revealed Ore; Fluxite never counts as Ore. A completed survey batch may have found no Ore, so its action acknowledgment alone does not prove the discovery goal completed.

A goal retains the last four **proposed** batches in RAM, keyed by game session and goal, capped at four goals and ten minutes. Current progress/results and a new screenshot are submitted to the local endpoint on every replan. An `agents-api` plan forwards the current planning context to OpenAI; a `game-state` continuation makes no OpenAI API request. Hosted planner sessions are separate from advisory coaching sessions and use the same eight-turn/ten-minute/two-minute-idle retirement rules and ID-only cleanup registry. No goal, image, or progress is written to local disk by the backend. Hosted session retention and asynchronous physical deletion remain as documented in `AGENTS-INTEGRATION.md`.

The model is instructed to complete the first paying ore route before optional expansion, preserve current service income, use idle locomotives before asking to park, and distinguish one colony depot with up to four mining services from unsupported additional depots. Repeated failures should change the plan or expose a blocker. The browser bounds the actual execution/replan loop and honors stop independently of model output.

Extractor-expansion goals keep an immutable starting count so “two additional extractors” cannot be satisfied by mines that already existed. A vague request for “more” means one additional Ore extractor; an explicit additional or total count takes precedence. Completion requires the requested count in fresh state, every target extractor joined to power, and connected solar generation at least equal to the stable rated demand when the goal asks for solar or power. A temporarily idle mine still contributes its rated demand.

Solar arrays and extractors join the colony's shared conduit grid; there is no direct solar-to-extractor wire. AstraBot adds and connects enough solar capacity before powering another mine. A new extractor or power plant ends its current batch so the next batch can use the building's real port, route and cost from a fresh state. After the player presses **Start expansion** once, the browser continues these bounded plan, execute and replan batches until the verified goal completes, a blocker is reported, or the player presses Stop or Escape. A power-only expansion does not add rails, trains, a fuel plant or train service unless the goal explicitly asks for transport, delivery, a route or income.

Local continuation is limited to the exact **Expand mines + power** preset and fully matched simple Ore-and-solar wording without extra constraints or references such as “here” or “this tile.” An incidental tile or building selection made by the game does not disable a whole-colony continuation. The previous plan ID and action list must match the latest server plan, every corresponding result must be `complete`, and fresh state must prove each effect. Only then may the server choose the next safe step and return `planSource: "game-state"` without entering the shared upstream slot, four-second upstream cooldown, or OpenAI API call. The browser still captures and submits a new screenshot with the fresh state before every batch.

Failed, missing, replayed or mismatched results return control to the hosted Agents API. So do hard route or budget blockers, a survey that would repeat without finding a usable Ore site, goals that refer to a selected tile, other constrained goals, Fluxite goals, and any goal outside the exact simple contract. This keeps strategic changes and ambiguous recovery with the agent. The local branch follows [OpenAI's latency guidance](https://developers.openai.com/api/docs/guides/latency-optimization) to avoid an LLM request when the next result is highly constrained; no end-to-end speedup has been claimed or measured.

The browser starts configuration lookup and Unity frame capture together, but waits for both before upload. When the server reports an active reader it supplies a one-second retry hint; when the four-second upstream cooldown remains, it supplies the remaining delay. The browser follows the bounded hint, captures a new frame for each retry, and lets Stop cancel the wait.

## Checks

Run `python3 -m unittest discover -s tests -p 'test_*.py'`. The combined backend suite exercises structured planning validation, progress bounds, selected tiles, resource/rail/fleet/budget checks, transport isolation, local security and unchanged coaching behavior using mocked upstream responses and real loopback HTTP.

`tests/test_expansion_latency.py` checks that only an exact acknowledged and state-verified continuation bypasses the upstream slot and cooldown, and that changed credentials, constraints, selected targets, Fluxite, failures, blockers and stalled surveys return to the Agents API. `tests/astrabot-control.test.cjs` covers concurrent configuration/frame preparation, server retry hints, and one Start driving the simulated two-mine sequence through five actions, six fresh planning frames and final 2/2 linked, 4/3 power verification.

The latest live browser run completed the exact preset after the first Agents API request timed out and a manual retry succeeded. One Start built and connected two new Ore extractors plus one additional solar array, added no rails or trains, and ended with `Goal complete`, `Mines 2/2`, `Linked 2/2`, and `Power 4/2`. The server then reported ten completed plans: seven `game-state` and three `agents-api`. Its latest local `lastPlanTiming.durationMs` was 1.37 ms, covering server receipt through response, including body parsing and key/config lookup but excluding browser capture, game animation and client network time. This run began before the final conservative scope and in-flight guards were applied; it exercised the same exact-preset path, while those final guards are covered by the deterministic tests.


## Automatic exploration, connected solar and mine expansion

For “automatically explore and discover new ores,” the request routes to `gpt-5.4-mini` and the hosted agent can return `auto_explore` with all coordinate, locomotive and duration fields null. On **Start plan**, the game surveys reachable revealed ground, selects a fog boundary with useful visibility gain, and issues adjacent revealed rover steps. It does not examine hidden resource positions or hidden ramps when choosing destinations. Freshly fully revealed Ore ends the action; Fluxite is reported separately. Each batch lasts at most 55 real seconds and keeps 10 battery in reserve. Pause, Stop, Escape and the copilot off control halt rover movement through the existing cancellation adapter. A new screenshot is required before extraction or another survey batch. Adding extractor, power, conduit, rail or transport work to the same goal routes the full task to `gpt-6-astra`. This remains the game-scoped adapter, not a native desktop computer-use tool.

`build_solar` now means place the array, then visibly connect its actual south port. Budget 100 credits plus the known `solarSitePowerRoute.cost` or `pickedSitePowerRoute.cost`; `possible:false` rejects the combined action. Reconnecting an existing array charges only its known missing conduit cost. The live simulation rechecks route/cost before laying it. If construction succeeds but a changed route or budget prevents wiring, the array is retained and the action fails with an explicit connection message. Operational power is reported only once `Connected` is true; a later `connect_conduit` is idempotent.

The **Expand mines + power** preset asks for two additional Ore extractors, explores for Ore only when necessary, joins each mine and any new solar arrays to the shared grid, and stops only after the fresh-state count, connections and solar capacity pass. It deliberately leaves rail and train construction out of scope. The player can edit the filled goal before creating the plan.

`python3 tests/run-exploration-checks.py` exercises the actual survey coroutine and simulation using only a clock/UI shim: known-only routes, independence from hidden deposit coordinates, real Ore discovery, Ore/Fluxite separation, known-deposit exclusion, pause/cancel/battery handling and a stalled-motion absolute deadline. It does not replace the browser’s real hosted-plan and Start/Stop playtest.
