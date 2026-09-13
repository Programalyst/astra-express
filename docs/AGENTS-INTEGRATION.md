# Pip: hosted Agents API integration

Pip's server now uses the **hosted OpenAI Agents API**. The previous implementation made individual `POST /v1/responses` calls and did not use Agents. This is not a rename or an Agents SDK wrapper: the transport creates managed sessions at `/v1/agents/sessions`, submits later messages to each session's events endpoint, and consumes its event stream. It retains the configured `gpt-5.4-mini` model. No new Python dependencies are required.

The API distinctions and supported hosted session configuration are described in the [OpenAI Agents API overview](https://developers.openai.com/api/docs/guides/agents-api/overview) and [session creation reference](https://developers.openai.com/api/reference/python/resources/beta/subresources/agents/subresources/sessions/methods/create).

## Runtime contract

- The browser submits a current JPEG, visible game state, up to eight recent events, the player's question, and one to three validated action candidates. It continues to own the exact controls and steps.
- The server adds fixed coaching rules. Instructions explicitly distinguish extractor placement from production: the extractor also needs power, energy, storage, ore and an unpaused simulation. The latest frame supersedes earlier frames.
- Each game gets an inline agent configuration and a managed conversation. The agent has no tools, no subagents and `environment: {type: "none"}`. It can explain one candidate; it cannot spend credits or operate the game.
- Structured output contains only `actionId`, `title`, `body`, and `observation`. The fixed session schema cannot use a different action enum every frame, so the server rejects any ID absent from the **current request's** candidates. Extra fields, replacement steps, empty/oversized text and invalid JSON are rejected too.
- A final assistant item must belong to the same completed root turn. Idle status, an incomplete stream, commentary, or a failed turn never counts as success. For follow-up inputs, the server opens the event stream before submitting the message. The [events guide](https://developers.openai.com/api/docs/guides/agents-api/sessions/events) explains these lifecycle requirements.

## Boundaries and cleanup

The existing single-request concurrency slot, 4-second server minimum and 120-request hourly limit remain. The browser normally waits at least 12 seconds between frames. A turn has a 20-second deadline across connection, submission and reading; failure retires its session and attempts one deletion with a 3-second timeout. Other cleanup runs separately. No retry automatically generates a duplicate turn, and uncertain/failed turns are not reused.

At most four game conversations are kept. Each is retired after eight successful frames, ten minutes total, two minutes without a new successful frame, or a key/model change. A maintenance thread checks every 15 seconds. It deletes retired sessions; failed deletions stay queued and increment `cleanupFailures`. A persistent, private, ignored `Logs/coach-agent-sessions.json` stores **session IDs only**, allowing the next server process to retry cleanup after a crash. It contains no images, questions, state or API key. A clean SIGTERM/Control-C exit retires sessions too.

**Retention changed from the previous `store:false` Responses call.** The Agents API stores conversation state, including submitted game screenshots. It is not a Zero Data Retention integration. API deletion removes session availability while physical cleanup may continue asynchronously. See the [Agents API retention note](https://developers.openai.com/api/docs/guides/agents-api/overview) and [session deletion guide](https://developers.openai.com/api/docs/guides/agents-api/sessions/manage). Local cleanup is best effort during outages; an abrupt loss before the initial session ID arrives cannot be tracked locally. API-level retention controls still apply.

The server key stays in private, git-ignored `server/.env` and is never sent to the browser. Both secrets and cleanup records are outside the static web root. Raw upstream errors, screenshots and questions are not logged. The config endpoint reports `engine: "agents-api"` and aggregate counters without credentials or hosted session IDs. If the API is unavailable, the client receives an explicit failure and keeps its local game-state tip; there is no silent raw-Responses fallback.

## Verification

Run deterministic backend checks with `python3 -m unittest discover -s tests -p 'test_coach_server.py'`. These use real loopback HTTP with a mocked upstream. They cover the hosted endpoint/header contract, JPEG/state pairing, current action validation, repeated turns in one session, stream completion, deadlines, rotations, deletion retry after restart, local origin/token checks, private files and sanitized fallback.

Live integration verification and in-game playtest outcomes are recorded separately, so mocked transport checks are not represented as model or visual evidence. The current model's image and structured-output support is listed in the [GPT-5.4 Mini documentation](https://developers.openai.com/api/docs/models/gpt-5.4-mini).

On 2026-09-13, a real `gpt-5.4-mini` hosted session accepted two synthetic blank JPEG frames through this implementation. The first completed in 12.22 seconds; the follow-up reused the same session and completed in 14.66 seconds. Its allowed action changed from `explore` to `power`; the returned action changed accordingly, and the answer correctly stated that placement alone does not start production. This proves the real transport, image modality, structured reply and reused-session behavior, **not in-game visual comprehension**. The first cleanup request timed out; subsequent read-only inspection returned zero remaining sessions (`has_more: false`), confirming that the deletion had completed despite the client timeout. The separate browser playtest covers actual game frames.

All 15 deterministic backend tests passed, and a separate reviewer reran those tests and reviewed the API contract, timeout and retention behavior. The first live continuation attempt exposed a contract mismatch that mocks had missed: event streaming uses `?stream=true` with the SSE Accept header, and idempotency belongs in the `Idempotency-Key` header, not the JSON body. Both were corrected before the successful two-turn test.

The main playtest subsequently verified **five actual game-screen requests completed, zero failed**, with one hosted session created and four follow-up reuses. This is real browser/game evidence supplied by the main playtest agent, distinct from the synthetic-frame transport check.

That playtest also found a coaching candidate gap: a second powered, rail-connected mine had `served:false` and 24/24 storage, while the existing train served another mine. The old candidate set offered an unrelated capacity upgrade, so a valid structured reply still gave poor gameplay advice. In that pre-merge build, the gameplay policy supplied a mine-reassignment candidate, and the server instructions explicitly separate rail connectivity from service assignment: capacity upgrades cannot collect an unserved mine; park the single train at the colony and explicitly dispatch to the target. Full storage does not block dispatch. The next playtest checked that corrected advice and interaction in the original single-locomotive build.

## Upstream fleet and Fluxite merge

After merging the upstream fleet/power-plant changes, the coaching rules and local action policy were aligned with `ColonySimulation.cs`. The original ore exploration → extractor → conduit → rails → first paid delivery sequence remains. Revealed ore takes priority over Fluxite for the initial income route.

The fleet now supports four locomotives (150 credits each). An idle locomotive is dispatched immediately even if another train is busy; when none is idle, coaching offers buying within the fleet cap/budget or selecting a service in Fleet to park. Capacity upgrades apply to a particular locomotive and cannot substitute for an extra service. Advice uses the full fleet state, assignments and destinations rather than the first train's phase.

Fluxite extractors route fuel to a selected power plant, never to the ore buyer. Guidance checks extractor power, depot rails, plant power and destination rails before dispatch. Plants cost 250 credits, hold 48 Fluxite, convert one unit into 40 energy and supply up to 8 power per second as needed. The coach explains why full batteries pause burning and why full plants can hold fuel trains at unloading; it does not recommend selling fuel or increasing train capacity to resolve either condition. Only revealed resource coordinates enter guidance.

The updated policy tests cover the unchanged ore tutorial plus idle second trains, full fleets, both fuel rail legs, plant construction, full storage, fuel economics, and strategic signature changes. This merge validation is code/test evidence; root's rebuilt-game playtest verifies the merged runtime separately.
