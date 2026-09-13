# AstraBot game planner

`POST /api/astrabot/plan` uses the genuine hosted OpenAI Agents API to turn a natural-language goal, current game screenshot and current state into a visible batch of game actions. It uses the configured model and server-side key. It cannot execute actions itself: the browser's game-scoped adapter owns start/stop, visible execution and simulation acknowledgments. This is not OpenAI's native computer-use tool and provides no desktop, browser navigation, shell or arbitrary-code control.

The hosted API also supports [application function tools](https://developers.openai.com/api/docs/guides/agents-api/tools/functions), using `requires_action` and `tool_result` events. This implementation deliberately uses a structured plan/replan contract instead of claiming that a JSON plan is a native tool call. It runs with [no hosted execution environment](https://developers.openai.com/api/docs/guides/agents-api/architecture), no tools and no subagents.

## Protocol

Read `GET /api/astrabot/config` (an alias of `/api/coach/config`) and send its local token in `X-Astra-Coach`, with the same local-origin policy as AstraBot. Configuration adds `plannerAvailable: true`; it never exposes the OpenAI key.

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

A goal retains the last four **proposed** batches in RAM, keyed by game session and goal, capped at four goals and ten minutes. Current progress/results and a new screenshot are sent on every replan. Hosted planner sessions are separate from advisory coaching sessions and use the same eight-turn/ten-minute/two-minute-idle retirement rules and ID-only cleanup registry. No goal, image, or progress is written to local disk by the backend. Hosted session retention and asynchronous physical deletion remain as documented in `AGENTS-INTEGRATION.md`.

The model is instructed to complete the first paying ore route before optional expansion, preserve current service income, use idle locomotives before asking to park, and distinguish one colony depot with up to four mining services from unsupported additional depots. Repeated failures should change the plan or expose a blocker. The browser bounds the actual execution/replan loop and honors stop independently of model output.

## Checks

Run `python3 -m unittest discover -s tests -p 'test_*.py'`. The combined backend suite exercises structured planning validation, progress bounds, selected tiles, resource/rail/fleet/budget checks, transport isolation, local security and unchanged coaching behavior using mocked upstream responses and real loopback HTTP.

One real hosted planner smoke request reached a final model reply, but its ambiguous explore action included dispatch-only target fields and was rejected before execution. Its session was deleted. The output schema was then tightened per action type; the final schema's live acceptance and actual in-game execution are checked by the main browser playtest, not claimed by the mocked tests.


## Automatic exploration and connected solar

For “automatically explore and discover new ores,” the hosted agent can return `auto_explore` with all coordinate, locomotive and duration fields null. On **Start plan**, the game surveys reachable revealed ground, selects a fog boundary with useful visibility gain, and issues adjacent revealed rover steps. It does not examine hidden resource positions or hidden ramps when choosing destinations. Freshly fully revealed Ore ends the action; Fluxite is reported separately. Each batch lasts at most 55 real seconds and keeps 10 battery in reserve. Pause, Stop, Escape and the copilot off control halt rover movement through the existing cancellation adapter. A new screenshot is required before extraction or another survey batch. This remains the game-scoped adapter, not a native desktop computer-use tool.

`build_solar` now means place the array, then visibly connect its actual south port. Budget 100 credits plus the known `solarSitePowerRoute.cost` or `pickedSitePowerRoute.cost`; `possible:false` rejects the combined action. Reconnecting an existing array charges only its known missing conduit cost. The live simulation rechecks route/cost before laying it. If construction succeeds but a changed route or budget prevents wiring, the array is retained and the action fails with an explicit connection message. Operational power is reported only once `Connected` is true; a later `connect_conduit` is idempotent.

`python3 tests/run-exploration-checks.py` exercises the actual survey coroutine and simulation using only a clock/UI shim: known-only routes, independence from hidden deposit coordinates, real Ore discovery, Ore/Fluxite separation, known-deposit exclusion, pause/cancel/battery handling and a stalled-motion absolute deadline. It does not replace the browser’s real hosted-plan and Start/Stop playtest.
