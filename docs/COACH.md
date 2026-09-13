# AstraBot, the colony copilot

The **Copilot On/Off** switch beside Pause turns the copilot on or off. Off hides its avatar, panel and highlights, cancels pending coaching, and stops new screen-reading requests. The choice is saved in this browser. Turning it on restores the avatar without opening the dialog.

Run `Run Local.command`, open http://127.0.0.1:8090/, and click AstraBot in the bottom-left corner. The panel slides out without pausing the colony. Close it with ×, Escape, or the avatar. It never opens itself. “Show me” highlights the suggested tile and centres the camera if necessary; it does not construct anything or move a vehicle.

AstraBot now has two separate flows. **Show me / Show connection** provides guidance and framing only; the player performs those actions. **Plan → Start plan** is opt-in control: describe a goal, optionally pick a tile for “here,” review the visible action batch, and start it. The game-scoped adapter then performs bounded actions with visible targets and checks their results. Stop or Escape stops further automated actions; ordinary coaching never starts a plan on its own. Long goals continue through small batches with fresh screenshots and progress, within one fixed colony, one rover and at most four train services. See [AstraBot planner](ASTRABOT-PLANNER.md) for the protocol and limits.

AstraBot follows the current game state, recent player actions, selected tool, placement feedback, discoveries, connections, power, money, and train progress. Advice uses Unity's construction and path validation. Hidden ore locations are omitted from the model context. Game-state tips work immediately without an API key.

For actual screen understanding, set `OPENAI_API_KEY` in `server/.env` (see `.env.example`). The server reads changes without restart. `ASTRA_COACH_MODEL` defaults to `gpt-5.4-mini`; choose another model supported by the hosted Agents API if required. The key stays on the server; only `Builds/Web` is served. Do not add the key to Unity, JavaScript, or the Web build.

With the panel open and Live screen help enabled, Unity captures its current rendered frame at the end of a frame. It includes the game and HUD, excludes the DOM coaching overlay, and does not capture the desktop or other apps. The frame is resized to at most 1280 × 960 and encoded as JPEG. The local server sends that image, current state, up to eight recent action events, and an optional player question to the hosted OpenAI Agents API. Managed sessions retain submitted conversation state, including screenshots, until retired and deleted; this is not a Zero Data Retention integration. The local server does not save screenshots or questions. Its ignored cleanup registry stores session IDs only. See [Agents integration](AGENTS-INTEGRATION.md) for session limits, deletion retries, and API retention details.

Automatic reads are at least 12 seconds apart, and unchanged contexts are checked at most every 45 seconds. Manual requests are at least four seconds apart. A local limit caps requests at 120 per hour. Automatic coaching reads stop when the coaching panel closes, live help is disabled, or the tab is hidden. An explicitly started plan has its own screenshot/replan lifecycle and Stop control. One request runs at a time. A request already submitted may finish and incur usage even if dismissed; its result will not be shown.

Responses are rejected when strategic context changes, the game restarts, the panel closes, or the tab is hidden. AstraBot uses the model's visible observation and explanation, alongside the selected action's exact game-validated steps. The label says “Live screen + game state” only after a successful image request. Missing credentials, timeouts, malformed replies, and API failures leave labelled game-state tips available.

## Development

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

Checks: `node --test tests/coach-policy.test.cjs` and `python3 -m unittest discover -s tests -p 'test_*.py'`. Browser interaction and a successful real OpenAI response are separate validation steps; mocked server tests do not prove live model access.

API references: [image input](https://developers.openai.com/api/docs/guides/images-vision), [structured output](https://developers.openai.com/api/docs/guides/structured-outputs).

## Initial validation on 13 September 2026

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
