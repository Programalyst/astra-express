# Astra Express

**Explore a Martian frontier. Ask AstraBot what it sees. Delegate the next colony project in plain language.**

![Astra Express splash art: a rover, glowing crystal deposits, and a freight train serving a Martian colony.](Assets/WebGLTemplates/Astra/loading-background.jpg)

[Play Astra Express](https://astra-express.leonard-lin-2003.chatgpt.site/) · [Game design](docs/GAME_DESIGN.md) · [Copilot guide](docs/COACH.md) · [Agent architecture](docs/AGENTS-INTEGRATION.md)

Astra Express is a Unity Web colony-and-logistics game inspired by **Transport Tycoon** and **Lucky Space**, built by two teammates collaborating with **Astra (GPT-6)**. Drive a rover into the fog of war, discover infinite mineral deposits, power extractors, and connect railway services to turn resources into an expanding colony.

Our central experiment is **an assistant that sees the player's game, explains the situation, and helps carry out a goal**. AstraBot combines screenshot observations with exact simulation facts, visible guidance, and player-approved automation. A compact on-screen avatar supports learning the mechanics and delegating familiar work, with an accessible Copilot toggle and Stop controls.

**The demo story:** natural-language intent → visual evidence → constrained plan → visible actions → verified game-state outcomes.

## Hackathon focus

| Category | What we want to demonstrate |
| --- | --- |
| **Best example of Visual Understanding** | Astra returns an image-derived bounding box and a short “I see…” observation, with local action candidates and screen geometry withheld during coaching inference. The UI exposes the visual evidence and its subsequent grounding check. |
| **Best use of Agents API** | The active local runtime uses hosted sessions for coaching and planning, routes advanced work to Astra, and executes player-approved action batches through a validated Unity adapter with fresh observations and outcome checks. |
| **A useful player experience** | Questions can lead to reviewable rover surveys or mining-outpost plans. Crosshairs explain the next click, while compact controls let the player dismiss help or stop automation. |

**Judging note:** the current local build uses hosted Agents API sessions through the same-origin Python server. Advisory coaching and advanced construction planning use `gpt-6-astra`; pure rover exploration and fog-reveal plans route to `gpt-5.4-mini`. The public Sites release may lag this source and still use the older direct, player-key Responses path. Its deployment record is identified separately rather than presented as Agents API evidence.

## Best example of Visual Understanding

### 1. Seeing the game while building it

Game development cannot be validated through source code alone. Does the cliff geometry fit? Is an ore label obscuring the deposit? Does an emissive material actually bloom? Does the Web build get past its splash screen?

Our development workflow combined Astra's visual inspection of the Unity Editor and rendered game with scene hierarchy inspection, console diagnostics, direct C# editing, and our **Unity Semantic Bridge**. Astra could refocus the Editor when background throttling interrupted the bridge, refresh assets and recompile, inspect the result, and iterate with our feedback.

That feedback loop supported work on terrain and raised plateaus, asset placement, camera framing, resource readability, lighting/post-processing, and browser startup issues. We supplied art direction, asset packs, custom prefabs, and manual adjustments; Astra helped implement and inspect the results.

```text
Human direction → inspect scene/screenshots → edit scripts or configuration
                → Unity refresh/recompile → inspect rendered result → refine
```

### 2. Seeing the game while helping the player

AstraBot's screen-help path captures the **actual rendered game and Unity HUD** as a JPEG. The local server receives that image alongside current game state, recent actions, selected tools, and local guidance candidates. It removes those candidates before sending the coaching perception input to Astra. Capture is limited to the game framebuffer; browser overlays and other applications are outside that image.

The inputs complement each other: the screenshot provides visual and spatial context, while structured state supplies exact facts such as battery charge, credits, stock levels, power connections, and train assignments. Hidden resource coordinates are excluded from the model context; the assistant must work with the player's discoveries rather than an omniscient map.

For Astra coaching, perception is deliberately separated from execution truth. Before inference the server removes the local next-action candidates, screen coordinates, UI rectangles, connection targets, route geometry, and precomputed world targets. `gpt-6-astra` must return one short **“I see…”** statement and a normalized image-derived bounding box, or explicitly report that it cannot see a reliable cue. Only after inference does the server attach the local authoritative next action and compare that box with its withheld target. The UI shows the model, Agents API backend, image-plus-state mode, frame age, latency, and whether the box passed that grounding check. Game state still guards every executable action.

The response connects an observation to a concrete next step. **Show me** and **Show connection** can highlight the relevant tile, port, or control instead of leaving the player to translate generic advice into a click. These guidance controls do not spend credits or move the rover.

Two overlays serve different purposes: the **Astra box** shows the model's visual observation, while the **local crosshair** marks the game-validated next click. A `GROUNDED` label means the returned box contains that withheld target point within a small tolerance. `CHECK` means it missed that target or could not be checked. This is a spatial consistency check, not independent proof that every object label or diagnosis is correct; Astra can correctly box a rover while the local next action points at a different frontier tile.

The visual evidence panel appears only after a successful `gpt-6-astra` Agents coaching response. It shows the model, backend, **image + game state** mode, measured request latency, and frame age at response. The coaching perception boundary does not remove state from normal construction planning: plans retain known sites and validated routes for reliable execution.

Screen-aware coaching is implemented, not just proposed. Earlier hosted-agent playtests recorded successful requests using actual game frames; the [integration record](docs/AGENTS-INTEGRATION.md#verification) separates these from synthetic-image transport checks and documents failures and corrections too. Local game-state hints remain available without an API key and are not presented as model vision.

## Best use of Agents API

### An adaptive tutorial, not a fixed checklist

Traditional tutorials require developers to anticipate a sequence of actions. Players explore in the wrong direction, build out of order, spend their starting money differently, or return to a colony they no longer remember how to operate.

AstraBot starts from **the colony that actually exists**. Its guidance accounts for discovered deposits, power, available money, mine storage, railway connections, and assigned services. Building an extractor, powering it, and getting its output delivered are separate problems—and advice needs to distinguish them.

Try questions such as:

- **“What should I do next?”** — get a contextual next step rather than restart a tutorial.
- **“Why isn't this mine producing?”** — ask about the current production bottleneck.
- **“Help me connect this extractor.”** — get guidance grounded in real building ports and routes.
- **“Explore the map and discover new ore.”** — move from advice to an explicitly approved exploration plan.

Game rules and action validation are still authored deliberately. The innovation is dynamically choosing and explaining relevant help instead of forcing every player through the same rigid sequence.

### Proactive help with a clear next step

Ask **“How can I find more ore?”** and AstraBot can explain the visible situation and offer **Send rover to survey**. The button fills a bounded exploration goal for review. Ask about expanding mining or building more bases and it can offer **Plan mining outposts**: powered extractors and rail services around the existing colony. The current game has one colony, one rover and one depot; extra colony bases are not a supported action.

Suggestions open the task composer without spending credits or starting a plan. The player can edit the goal, pick a tile to specify “here,” create a plan, and explicitly choose Start.

### Route each task to the appropriate model

| Work | Active path | Visible attribution |
| --- | --- | --- |
| Screen coaching and visual observations | `gpt-6-astra` through hosted Agents API | Model, backend, image/state mode, latency, frame age, and evidence box |
| Pure rover exploration, fog reveal, or ore discovery | `gpt-5.4-mini` through hosted Agents API | Model and rover-exploration route in plan review |
| Construction, conduits, power, rails, trains, or mixed goals | `gpt-6-astra` through hosted Agents API | Model and advanced-planning route in plan review |
| Eligible continuations of narrowly constrained mine-and-solar goals | Validated local game-state planner | Local source, without attribution to a new model call |

The server routes the bounded goal using deterministic intent rules. Mixed or ambiguous goals stay on Astra. This reserves Astra calls for richer work while avoiding unnecessary inference for verified continuations; routing itself is not an Astra inference.

### From “tell me” to “do this with me”

The planner turns a natural-language goal into a reviewable batch of up to six actions. Nothing executes until the player chooses **Start plan**.

```text
Player goal + screenshot + known game state
                    ↓
          Structured plan for review
                    ↓ Start plan
          Game validates and executes
                    ↓
     Results + fresh screenshot + progress
                    ↓
          Replan, finish, or explain a blocker
```

For autonomous exploration, the game-side survey action selects reachable, revealed frontiers and moves the rover to uncover more terrain. Each survey action is bounded to 55 seconds, preserves a battery reserve, and stops when it fully reveals new ore. A fresh observation is required before planning the next stage. It does not inspect hidden deposit coordinates to pick its route.

This changes the player's role: **drive every step yourself, ask for help, or delegate a goal and supervise**. The same interface can support learning and reduce repetitive micromanagement after the player understands the mechanics.

### Real integration, explicit boundaries

The active [Python transport](server/coach_server.py) uses hosted Agents sessions and event streams, with separate coaching and planning conversations. Server-key follow-up turns can reuse sessions; tab-key requests use isolated temporary sessions. Completion is checked against the completed root turn rather than treating partial commentary as a final answer. Session lifetimes, deadlines, retirement, and deletion retries are bounded. See the [implementation and recorded verification](docs/AGENTS-INTEGRATION.md).

The agent produces **structured plans**, not native hosted function-tool calls. This implementation has no hosted execution environment, native tools, or subagents. A narrow Unity adapter executes supported game actions and checks them against the live simulation.

- **Player consent:** ordinary coaching cannot initiate automation; plans have review, Start, and Stop controls.
- **Game authority:** credits, occupancy, visibility, power, routes, and fleet constraints are checked before actions execute. No free resources or hidden-map shortcuts.
- **Interruptibility:** Stop, Escape, and switching Copilot off cancel further automated actions.
- **Failure handling:** malformed or invalid outputs do not become executable plans; unavailable AI leaves clearly labelled local hints.

The current [browser transport](Assets/WebGLTemplates/Astra/astrabot-api.js) sends coaching and planning to the same-origin Python host, which owns hosted Agents sessions and the model router. See [the planner protocol](docs/ASTRABOT-PLANNER.md) for execution details and the separate public-build boundary.

## Try it as a judge

Use the [local build](#run-locally) to evaluate the current Agents routing and visual overlays. The [public game](https://astra-express.leonard-lin-2003.chatgpt.site/) is a separate release; check its [deployment record](docs/DEPLOYMENT.md) before attributing these features to it.

1. **Connect AI help.** Open the gear immediately left of **Copilot**, enter your own OpenAI API key, and choose **Connect for this tab**, or configure the private server key. Open AstraBot with **Live screen help** enabled.
2. **Discover ore.** Ask “How can I find more ore?” Inspect Astra's observation, image box and provenance. Choose **Send rover to survey**, create a plan, and confirm the `gpt-5.4-mini` exploration route before Start.
3. **Stage a visible connection problem.** Once the rover reveals Ore, place an extractor and leave its conduit disconnected. Ask “This extractor isn't producing. Inspect the screen and point out the visible problem.” Check whether Astra's box identifies useful evidence and whether the local grounding check agrees. An unverified box should remain labelled as such.
4. **Delegate the first paying route.** Open **Give AstraBot a task**, optionally select the extractor tile, and enter “Connect this extractor to power and rails so it produces and delivers ore.” Review the Astra plan and press Start. Watch the connections being built and an available idle locomotive auto-dispatch; check production and delivery outcomes.
5. **Expand and take back control.** Try **Expand mines + power** for two additional powered extractors with sufficient solar generation. Its progress reports built mines, connected targets and available power. Stop or Escape returns control, and Copilot Off disables further assistance.

The disconnected-extractor sequence is a recommended showcase scenario, not a claim of a completed visual-diagnosis benchmark. Coaching and task execution currently remain separate, explicit player flows.

## Evidence from the latest local update

The 13 September 2026 verification separated automated correctness checks from real model observations:

| Check | Observed result | What it establishes |
| --- | --- | --- |
| Backend and browser checks | 75 Python tests and 105 JavaScript tests passed | Routing, credential boundaries, structured output, withheld perception inputs, and review-before-execution behavior |
| Simulation and control checks | 186 scenarios, 46,582 invariants, 24 exploration assertions, and 37 bot-control checks passed | Game rules and bounded action execution under the tested cases |
| Rebuilt Unity Web player | Built successfully and opened locally | The updated browser/game integration loads |
| Real Astra coaching | Three completed hosted calls with no failures recorded at the checkpoint; displayed replies measured about 12–17 seconds | The active browser → local server → Astra Agents path accepted real game frames and returned visible evidence |
| Proactive task handoff | “Send rover to survey” opened a prefilled composer without initiating gameplay | The suggestion-to-review interaction works |

In the live checks, Astra boxed the rover or Explore control while the withheld next-click target was a frontier tile. Those boxes were visibly labelled **CHECK**, which correctly exposed the mismatch. The prompt was then refined to prefer a fog boundary for discovery questions and the player was rebuilt; that final refinement has not yet been confirmed by a subsequent successful live read. These observations support the integration and honest evidence display, not a claim of universal visual accuracy.

**Key and privacy note:** the current local runtime can use a private server key or a player key kept in tab memory and forwarded only to its loopback server for isolated hosted sessions. The older public demo sends its tab key directly to OpenAI. Neither path distributes a shared production secret. Use a restricted, low-budget key and revoke it after testing; AI requests send game screenshots and state to OpenAI and incur API usage. [Details and limits](docs/COACH.md).

The repository may be ahead of the public build; [deployment records](docs/DEPLOYMENT.md) identify the published version.

## The game underneath

- **32 × 32 frontier:** fog of war, two terrain elevations, traversable slopes, and grid-based rover pathfinding.
- **Explore to expand:** infinite deposits with higher-yield, larger patches farther from the colony.
- **Shared power economy:** solar generation charges a colony battery used by rover movement and working extractors.
- **Rail logistics:** every extractor includes a free dedicated train, with no four-train cap. Connect rails to start deliveries; click its extractor for capacity upgrades and service controls.
- **Fluxite fuel chain:** transport fictional space fuel to power plants for stronger generation.
- **Frontier defense:** 2x2 mining attracts northwest alien waves; automated laser turrets defend buildings, and free timed repairs restore disabled structures. [Rules and controls](docs/DEFENSE.md).
- **A readable miniature world:** windswept red terrain, a moving APC rover, crystal deposits, animated drills, and a research-pod colony.

## Run locally

Requires **Unity 6.3 LTS (`6000.3.24f1`)**, the **Web Build Support** module, and **Git LFS**. The project uses URP.

```sh
git lfs install
git lfs pull
```

The package manifest references the team's Unity Semantic Bridge through a sibling checkout:

```text
workspace/
├── astra-express/
└── unity-semantic-bridge/
    └── com.gamenami.unity-semantic-bridge/
```

Make that package available before opening the project. Open `Assets/Scenes/AstraExpress.unity` and enter Play mode for gameplay iteration. The browser copilot UI requires a Web build: choose **Astra Express → Build Web**, then run `bash "Run Local.command"` with Python 3 installed and visit `http://127.0.0.1:8090/`. This local host serves both the game and the hosted Agents API bridge.

## Explore the implementation

| Area | Source / evidence |
| --- | --- |
| Simulation and economy | [ColonySimulation.cs](Assets/Scripts/AstraExpress/ColonySimulation.cs) |
| Screenshot and game-state bridge | [AstraCoachBridge.cs](Assets/Scripts/AstraExpress/AstraCoachBridge.cs) |
| Validated game actions | [AstraBotControl.cs](Assets/Scripts/AstraExpress/AstraBotControl.cs) |
| Known-frontier exploration | [AstraBotExploration.cs](Assets/Scripts/AstraExpress/AstraBotExploration.cs) |
| Current browser API client | [astrabot-api.js](Assets/WebGLTemplates/Astra/astrabot-api.js) |
| Hosted Agents transport / planner | [coach_server.py](server/coach_server.py) · [astrabot_planner.py](server/astrabot_planner.py) |
| Integration and gameplay evidence | [Agents verification](docs/AGENTS-INTEGRATION.md#verification) · [Playtest notes](docs/PLAYTEST.md) · [Merge validation](docs/MERGE-VALIDATION.md) |
| Automated checks | [Tests](tests) — simulation, exploration, action validation, browser contracts, and mocked API transport |

Mocked transport tests establish protocol handling, not visual comprehension. Historical live tests are identified separately in the evidence documents; they are not a claim that every current browser configuration has been freshly live-tested.

## Credits

Built by a two-person team with Astra, combining human gameplay/art direction and custom Unity tooling with AI-assisted implementation, visual iteration, and debugging.

Visual assets include **Kenney Space Kit / Industrial City Kit**, **Synty Polygon Sci-Fi Worlds**, and selected assets and desert materials imported from the team's **Nuclear Knights** project. See the [import notes](Assets/Imported/NuclearKnights/README.md). Third-party assets remain subject to their respective licenses; the repository's [MIT license](LICENSE) does not override those licenses or grant redistribution rights to licensed asset packs.
