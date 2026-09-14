# AstraBot, the colony copilot

## Action-first companion

Live activity retains only its three newest messages. A successful goal (including
an already-satisfied request) shows only “Goal complete.”, hides review controls,
and closes after 2.2 seconds. Failure/blocker cards remain available. A new task
invalidates the old completion timer. The persistent AstraBot avatar focuses the
message input and remains below the drone berth even after dismissing the welcome
menu; disabling Copilot hides both.

### Passive scanning disabled

The drone returns to a camera-relative berth beside the AstraBot UI when idle.
Task planning triggers a 1.5-second spin-up and four-second curved departure,
concurrent with inference; it waits in-world when a plan is ready for Start.
Conversation-only replies keep it at the berth. Cancel/completion returns it,
and Copilot Off hides it. The animation is presentation only and never grants
action approval or adds artificial API latency. Input hint: “Ask anything, just say the word.”

Passive model screen reads are disabled, including saved Live screen help opt-ins.
Check my colony and the Live screen help control have been removed. The remaining
three-second suggestion refresh reads local game state only and makes no model
requests. Model calls are limited to submitted conversation and requested task
planning/approved execution. Local guidance expands inside the same AstraBot card
as suggestions, rather than opening a separate colony-copilot panel.
The input starts at half desktop width, expands for drafts over 55 characters,
and shrinks again when shortened or sent. Small screens use available width.
This supersedes the historical automatic-screen-help descriptions below.

### Conversation in the fixed bar

Send (including Enter) lets the model route the message: questions, jokes and
explanations get replies; action requests automatically prepare a goal for Start
approval. Ambiguous requests get a clarifying question. Only a validated completed
model response can hand off to planning; partial or failed streams cannot. The
router uses structured reply/intent/goal output, not keyword matching.
Follow-ups such as “just do it” delegate the most recent actionable advice, with
its constraints carried into a self-contained planner goal. Thanks and unrelated
jokes do not imply game actions; genuinely unclear references get clarification.
Chat answers, jokes and follow-ups appear in a permanently open
reply card above the input, not Live activity. The card receives actual final-answer
text deltas from the hosted Agents event stream; it never displays reasoning or
commentary. A completed-turn event confirms the final reply. Stop reply, Escape,
tab hiding, credentials changes and Copilot Off cancel or discard pending replies.
Eight recent user/assistant messages are retained in tab memory for follow-ups and
cleared on colony/credential changes. Chat uses the configured fast model with
discovered game state, no screenshot, no browsing and no execution tools. It does
not claim model vision. API errors remain visible and never fall through to plans.
The same origin/token/key handling and inference limits protect the chat endpoint.

The fixed input above the bottom toolbar remains visible during coaching and
execution. Enter or Send handles both conversation and task requests, never
immediate execution. Start still approves the plan. While busy, players can draft the next message but
cannot submit overlapping work. Copilot Off disables the input without erasing it.
New sessions initially offer Start tutorial or Ask AstraBot.
The welcome card disappears for the rest of the colony session after either
choice; it is not replaced by a recurring suggestion menu. The fixed input stays
available with “ask anything, I'll do it with you just say the word”.
Automatic crosshairs are opt-in through Start tutorial; dismissing guidance ends guided mode so a new
step cannot reopen it. Show me remains an explicit, temporary highlight.
The service drone carries a small mint, camera-facing AstraBot face badge with
visor, eyes and antenna; it is depth-tested world geometry, not a HUD bubble.

During execution the player can pan, zoom, select, build and use the toolbar alongside AstraBot. Bot actions have independent targets and use the simulation's ordinary placement validation without changing the player's tool, route preview, selection or camera. Connections recheck current occupancy and credits at placement time. The rover remains shared: a manual move cancels the bot task, and AstraBot yields if a player movement order already exists. Pausing the colony stops the task until another review and Start.

A small native 3D service drone uses the existing CC0 Kenney Space Kit `craft_miner.fbx`. It hovers beside the colony when idle, flies to revealed task targets, and uses a thin work beam on arrival. It shares scene lighting and depth, has no colliders, never moves the camera, and disappears when Copilot is off. Construction allows a bounded 1.4–5 second arrival beat based on travel distance. The build processor assigns the model in the build copy without overwriting the source scene. Current players suppress the old floating SVG, speech bubble and large task-target brackets; the DOM fallback remains for older players. **Live activity · expand** shows a bounded history of plan summaries, game-reported stages and confirmed results. **Open activity** expands the full Plan & Play panel during execution. The feed contains action explanations and status updates, not private chain-of-thought or token streaming from the model.

Concurrent-control validation (14 September): 111 JavaScript tests, 79 Python tests, 41 bot-control assertions, 24 exploration assertions and the simulation suite passed. The control tests cover preserving the player's tool, selection and route preview through construction/Stop, plus manual rover priority. In a separate live Web tab, a survey completed with the embodied target and activity feed visible. A second, 20-second observation action remained running while the player selected Extractor; Stop stayed accessible with history expanded. These browser checks do not establish every possible simultaneous construction interaction.

The persistent **Do a task →** button beside the robot opens the goal composer directly. The former question input has been removed; **Check my colony** in the coaching panel still requests a screen read. The task composer includes **Find/Discover ore**, **Build the rails**, and the existing construction presets.

The companion offers a task from current discovered game facts: connect an unpowered building, add rails to a powered Ore mine, develop a buildable deposit, or survey for more Ore. Connection offers require a possible, affordable route. Offers stay quiet during tile picking, route placement, paused gameplay and active automation, and each can be dismissed. Clicking an offer prefills its goal and exact building/deposit target; it does not create or start a plan. These local suggestions do not trigger background model requests.

Actual pending screen reads animate the existing SVG robot with a thinking sway and moving eyes. Planning and screen-slot waits show a small robot face and bouncing ellipsis, including in the minimized task dock. Completion, cancellation and errors clear the thinking state. Reduced-motion preferences disable these animations.

The **Copilot On/Off** switch beside Pause turns the copilot on or off. Off hides its avatar, panel and highlights, cancels pending coaching, and stops new screen-reading requests. The choice is saved in this browser. Turning it on restores the avatar without opening the dialog.

Run `Run Local.command`, open http://127.0.0.1:8090/, and click AstraBot in the bottom-left corner. The panel slides out without pausing the colony. Close it with ×, Escape, or the avatar. It never opens itself. “Show me” highlights the suggested tile and centres the camera if necessary; it does not construct anything or move a vehicle.

AstraBot now has two separate flows. **Show me / Show connection** provides guidance and framing only; the player performs those actions. **Plan → Start plan** is opt-in control: describe a goal, optionally pick a tile for “here,” review the visible action batch, and start it. The game-scoped adapter then performs bounded actions with visible targets and checks their results. Stop or Escape stops further automated actions; ordinary coaching never starts a plan on its own. Long goals continue through small batches with fresh screenshots and progress after one explicit Start, within one fixed colony, one rover and at most four train services. See [AstraBot planner](ASTRABOT-PLANNER.md) for the protocol and limits.

The **Expand mines + power** preset fills a goal for two additional Ore extractors. AstraBot explores when more revealed Ore is needed, builds each extractor, connects it to the colony's shared conduit grid, and adds enough connected solar capacity for the mines' rated demand. Solar arrays and extractors both join that shared grid rather than connecting directly to each other. A new mine build closes its batch so AstraBot can inspect the real port and route before connecting it in the next fresh-state batch. The progress line reports the verified mine count, powered target count and generation versus rated demand. Completion requires all three checks to pass. The preset does not add rails or dispatch trains unless the edited goal asks for transport or delivery.

The current Web transport sends each coaching request and hosted plan/replan to the Python game server. The server uses OpenAI hosted Agents API sessions. Pure rover exploration, map/fog reveal and ore-discovery goals use `gpt-5.4-mini`; building, connection, power, rail, train and ambiguous goals use `gpt-6-astra`. Verified game-state continuations can complete narrowly constrained Ore-and-solar goals without another model call. See [AstraBot planner](ASTRABOT-PLANNER.md) for the contract and acknowledgment checks. A new screenshot is captured for every planning batch.

AstraBot follows the current game state, recent player actions, selected tool, placement feedback, discoveries, connections, power, money, and train progress. Advice uses Unity's construction and path validation. Hidden ore locations are omitted from the model context. Game-state tips work immediately without an API key.

When the player asks how to find more Ore, Astra coaching can offer **Send rover to survey**. Requests to expand mining or build more bases offer **Plan mining outposts**, translated into supported powered extractors and train services around the game's one fixed colony. These buttons only prefill a task; the player reviews it, creates the plan, and explicitly starts execution.

For screen understanding, run `Run Local.command`, click the **gear immediately left of Copilot**, paste an OpenAI API key (the `sk-…` value only), and click **Connect for this tab**. The browser sends the key only to the same-origin game server in `X-Astra-OpenAI-Key`. The server checks access to both routed models without generation, then uses that key for an isolated hosted agent session and deletes the session after the request. The key stays in tab memory and is never written to files, browser storage, game state, URLs, or model prompts. Reload, closing the tab, or **Forget key** clears it. A private `OPENAI_API_KEY` in the ignored `server/.env` is the server-key fallback. Opening settings stops active takeover, and changing credentials invalidates pending plans.

This is a local bring-your-own-key demo mode, not a way to distribute a shared production key. Use a restricted, low-budget project key and revoke it after the demo. API usage is billed separately from ChatGPT. The loopback server enforces same-origin requests and a per-process token; upstream requests use a fixed OpenAI HTTPS origin. Static hosting cannot run this Python backend or safely contain a shared key, so a public deployment needs a separately hosted server or platform proxy.

With the panel open and Live screen help enabled, Unity captures its current rendered frame at the end of a frame. It includes the game and HUD, excludes the DOM coaching overlay, and does not capture the desktop or other apps. The frame is resized to at most 1280 × 960 and encoded as JPEG. The browser sends that image, current state, up to eight recent action events, and an optional player question to the local server. Advisory coaching uses `gpt-6-astra`; task planning uses the model router described above. Both run with low reasoning, structured output, no tools, no subagents and no execution environment. Hosted sessions store submitted context according to OpenAI's Agents API retention behavior; retirement and deletion are described in [Agents integration](AGENTS-INTEGRATION.md).

For visual proof, the server removes local next-action candidates, screen coordinates, UI rectangles, connection targets, route geometry, and precomputed world targets from the perception input. Astra returns either a short “I see…” sentence with a normalized screenshot bounding box or an explicit no-reliable-cue result. The server then attaches the local action and checks the box against its withheld target geometry. The overlay shows `GROUNDED` only for a match and `CHECK` when the target cannot be validated. It also exposes model, Agents API backend, image-plus-state mode, measured latency, and frame age. This evidence UI appears only for `gpt-6-astra` advisory responses through Agents API; local tips and `gpt-5.4-mini` exploration planning do not imitate it.

Automatic reads are at least 12 seconds apart, and unchanged contexts are checked at most every 45 seconds. Manual requests are at least four seconds apart. A tab-memory limit caps inference attempts at 120 per hour; reload resets this guardrail, so it is not an account spending cap. Automatic coaching reads stop when the coaching panel closes, live help is disabled, or the tab is hidden. An explicitly started plan has its own screenshot/replan lifecycle and Stop control. One request runs at a time. A request already submitted may finish and incur usage even if dismissed; its result will not be shown.

Responses are rejected when strategic context changes, the game restarts, the panel closes, or the tab is hidden. AstraBot uses the model's visible observation and explanation, alongside the selected action's exact game-validated steps. Missing credentials, timeouts, malformed replies, and API failures leave labelled game-state tips available.

## Development

The Web template loads `astrabot-contract.js`, `astrabot-planning.js`, then `astrabot-api.js` before the UI. The contract exports the existing Python prompts and JSON schemas, while the browser validates output fields, known targets, budgets, fleet limits, wiring, and Fluxite destinations before returning a plan for review. Unity still checks each action against the current simulation before executing it.

After changing Python prompts/schemas, regenerate with `python3 scripts/export-astrabot-contract.py > Assets/WebGLTemplates/Astra/astrabot-contract.js`; verify synchronization with `python3 scripts/export-astrabot-contract.py --check`. Run `node --test tests/astrabot-api.test.cjs tests/astrabot-control.test.cjs tests/coach-policy.test.cjs` and `python3 -m unittest discover -s tests -p 'test_*.py'`. Serve a fresh Web export with `Run Local.command`; the active integration requires the Python Agents server. It uses the documented [Agents API sessions](https://developers.openai.com/api/docs/guides/agents-api/overview) with [GPT-6 Astra](https://developers.openai.com/api/docs/models/gpt-6-astra).

Link coaching includes a native Unity route preview (`AstraLinkGuide.cs`). Selecting an unpowered building shows translucent tiles, a dashed path, numbered ports and corners, and the validated remaining cost. The prompt advances after the start click; completion removes the preview and shows a short confirmation. `Hide X` or Escape dismisses it; selecting Conduit or Rail can show it again. Rail previews appear for a selected powered extractor or power plant when Rail is selected. Plant and mine ports can both connect through the colony rail network.

AstraBot's `Show connection` button chooses the relevant construction tool, fits both ports and route corners in the camera, and collapses the dialog. It never lays tiles or spends credits. Every segment still requires the player's start and end clicks. Route validation is shared with the existing coach planner, including occupied or unexplored tiles and affordability; blocked plans do not show a buildable ghost.

- `Assets/Scripts/AstraExpress/AstraCoachBridge.cs`: state snapshots, camera-only focus, input isolation, real framebuffer capture.
- `Assets/Plugins/WebGL/AstraCoach.jslib`: WebGL-to-browser bridge.
- `Assets/WebGLTemplates/Astra/coach-policy.js`: local contextual coaching and stale-context signatures.
- `Assets/WebGLTemplates/Astra/coach.js`, `coach.css`: AstraBot avatar, sliding dialog, screenshot requests and reply lifecycle.
- `Assets/Scripts/AstraExpress/AstraBotControl.cs` and `astrabot-control.js`: bounded in-game execution, visible targets and acknowledgments.
- `server/astrabot_planner.py`: strict action plans, goal/progress bounds and plan validation.
- `server/coach_server.py`: loopback-only static host, coaching and planning endpoints; Python 3.9+ standard library, no packages needed.

Rebuild Unity with `-batchmode -quit -projectPath <project> -buildTarget WebGL -executeMethod AstraExpressEditor.BuildWeb`, or use Astra Express > Build Web with the Web target selected. Reload the browser after rebuilding. This version of the avatar and live coach runs in the Web player; native Unity Editor Play Mode retains the existing mission guidance.

Checks: `node --test tests/coach-policy.test.cjs tests/astrabot-control.test.cjs` and `python3 -m unittest discover -s tests -p 'test_*.py'`. The simulated runner covers one Start through the full two-extractor, shared-grid and solar-capacity sequence. The latest live run also completed the exact preset under one Start: two new extractors and one new solar array were connected, no rails or trains were added, and the UI ended at `Mines 2/2 · Linked 2/2 · Power 4/2`. An initial API timeout required a manual retry; the final server snapshot showed seven local and three hosted completed plans. The run began before the final conservative scope and in-flight guards, whose behavior is covered by the deterministic tests.

API references: [image input](https://developers.openai.com/api/docs/guides/images-vision), [structured output](https://developers.openai.com/api/docs/guides/structured-outputs), [latency optimization](https://developers.openai.com/api/docs/guides/latency-optimization).

## Initial validation on 13 September 2026

Direct-browser update validation (16:08 Singapore): 112 JavaScript tests passed, browser/Python prompt-schema synchronization passed, and the Unity Web build succeeded (69,888,004 bytes). Chrome loaded the game and new settings from a static-only server at `http://127.0.0.1:8094/`; submitting a deliberately fake key displayed OpenAI's authentication rejection, not a backend or CORS error. The local server received no `/api/` requests. OpenAI preflights for the Sites origin permitted Authorization and JSON requests. No real key was available for a successful inference test; model quality and a successful paid request remain to be verified by the owner. This update is local only, not published on Sites yet.

These initial results predate the Agents migration and fuel/fleet merge. Current evidence is recorded in [PLAYTEST.md](PLAYTEST.md) and [MERGE-VALIDATION.md](MERGE-VALIDATION.md).

- Unity 6000.3.24f1 Web build succeeded after the final input fix (66,693,647 bytes; 21-second build step).
- 11 coaching-policy checks and 8 server checks passed. The server checks mock OpenAI while exercising real local HTTP, origin/token checks, credential isolation, malformed replies, image transport, and rate limiting.
- Separately, actual OpenAI requests with real game frames succeeded. Pip described visible rover/fog and extractor/connection cues, answered a typed controls question, and explained the live disconnected-mine state.
- Browser interaction verified collapsed startup, slide-out panel, Escape dismissal, typing without triggering game hotkeys, hotkeys immediately after dismissal, the world highlight, and changing advice after exploration and extractor construction. The panel remained collapsed through those gameplay changes.
- The game frame reached the local server as a real JPEG (one observed frame was 61,080 bytes). No image data or API key is returned in the health response.
- This is an initial browser coaching implementation, not a full evaluation of model accuracy or long-session performance. A pre-existing development-player warning about its frame scheduling was observed; no runtime crash occurred in these checks.

### Link-guidance update

- Final Web build passed (66,731,563 bytes; 23-second build step), with 15 policy checks passing.
- Browser verification covered translucent route tiles, dashed lines, numbered clickable ports, Show connection camera framing and panel collapse, Hide and Escape, and restoring the hint. The first marker kept credits at 350 and advanced the instruction; the second spent the displayed 12 credits, connected the mine, cleared the ghost, and showed POWER CONNECTED. A rail preview then displayed its separate 18-credit cost.
- Hint dismissal and marker buttons suppress the corresponding world input frame, preventing a click from also moving the rover or starting a second route.
- Web builds now use content hashes in asset filenames. This fixes an observed startup failure caused by reusing cached data with a rebuilt WASM binary; startup at the original local URL passed after this change.
# Cooperative quick jobs

The dock scans discovered game state every three seconds and offers up to three
affordable jobs: connect power, build rails, place an Ore extractor, or discover
Ore. These local suggestions prepare a single-action confirmation immediately;
they do not call the model or start automatically. Start now is explicit approval.
The offer is revalidated at Start, including freshness, rover ownership and cost.
Unity remains authoritative and revalidates placement and routes before spending.
Placing an extractor is intentionally only placement; subsequent scans offer its
missing connections. Rail completion uses the simulation's existing idle-train
assignment, never purchases a train. Each quick job stops on its first result,
with no model replan, and a successful compact card closes after 2.2 seconds.

Custom goals retain screenshot-aware planning. Their Start button is available
in the compact dock; full steps and provenance are collapsed under Steps & details.
Live activity remains expandable. Construction uses a 1.4-second arrival beat,
1.2-second placement beat and 2-second connection preview; player input outside
the panel remains available. Proactive scans do not send periodic model requests.
