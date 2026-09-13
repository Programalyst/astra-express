# Astra Express

**Explore a Martian frontier. Build a railway economy. Teach your colony what to do next—in plain language.**

![Astra Express splash art: a rover, glowing crystal deposits, and a freight train serving a Martian colony.](Assets/WebGLTemplates/Astra/loading-background.jpg)

[Play Astra Express](https://astra-express.leonard-lin-2003.chatgpt.site/) · [Game design](docs/GAME_DESIGN.md) · [Copilot guide](docs/COACH.md) · [Agent architecture](docs/AGENTS-INTEGRATION.md)

Astra Express is a Unity Web colony-and-logistics game inspired by **Transport Tycoon** and **Lucky Space**, built by two teammates collaborating with **Astra (GPT-6)**. Drive a rover into the fog of war, discover infinite mineral deposits, power extractors, and connect railway services to turn resources into an expanding colony.

Our central experiment is **a tutorial that understands your colony, rather than a script that assumes your next click**. AstraBot can explain what to do next, show you where to do it, or carry out a player-approved plan. It turns assistance into a new way to play—not just a chat window beside the game.

## Hackathon focus

| Category | What we want to demonstrate |
| --- | --- |
| **Best example of Visual Understanding** | Visual feedback helped Astra build and debug the game; the in-game assistant receives actual game screenshots alongside current, player-visible state. |
| **Best use of Agents API** | A stateful hosted-agent implementation for contextual coaching and bounded planning, with a game-side execution loop that lets players delegate goals such as exploration. |

**Judging note:** the repository includes the hosted Agents API implementation and recorded integration/playtest evidence. To ship without hosting a key-bearing backend, the current public Web client instead calls the **Responses API directly with a player-provided key**. It demonstrates the coaching and planning experience, but does **not** create hosted Agents sessions. These are distinct implementations, not interchangeable API names. The in-game model is currently `gpt-5.4-mini`; Astra/GPT-6 is our development collaborator.

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

AstraBot's screen-help path captures the **actual rendered game and HUD** as a JPEG. It pairs that image with current game state, recent actions, selected tools, and validated guidance candidates. It captures the game—not the player's desktop or other applications.

The inputs complement each other: the screenshot provides visual and spatial context, while structured state supplies exact facts such as battery charge, credits, stock levels, power connections, and train assignments. Hidden resource coordinates are excluded from the model context; the assistant must work with the player's discoveries rather than an omniscient map.

The response connects an observation to a concrete next step. **Show me** and **Show connection** can highlight the relevant tile, port, or control instead of leaving the player to translate generic advice into a click. These guidance controls do not spend credits or move the rover.

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

The retained [Python transport](server/coach_server.py) uses hosted Agents sessions and event streams, with separate coaching and planning conversations. Follow-up turns reuse sessions; completion is checked against the completed root turn rather than treating partial commentary as a final answer. Session lifetimes, deadlines, retirement, and deletion retries are bounded. See the [implementation and recorded verification](docs/AGENTS-INTEGRATION.md) and [official Agents API overview](https://developers.openai.com/api/docs/guides/agents-api/overview).

The agent produces **structured plans**, not native hosted function-tool calls. This implementation has no hosted execution environment, native tools, or subagents. A narrow Unity adapter executes supported game actions and checks them against the live simulation.

- **Player consent:** ordinary coaching cannot initiate automation; plans have review, Start, and Stop controls.
- **Game authority:** credits, occupancy, visibility, power, routes, and fleet constraints are checked before actions execute. No free resources or hidden-map shortcuts.
- **Interruptibility:** Stop, Escape, and switching Copilot off cancel further automated actions.
- **Failure handling:** malformed or invalid outputs do not become executable plans; unavailable AI leaves clearly labelled local hints.

The current [browser transport](Assets/WebGLTemplates/Astra/astrabot-api.js) preserves this plan/replan experience using stateless Responses requests and bounded local progress history. Running the retained Python server alone does not switch that Web client back to Agents sessions. See [the planner protocol](docs/ASTRABOT-PLANNER.md) for the transport distinction and execution details.

## Try it as a judge

1. **[Open the game](https://astra-express.leonard-lin-2003.chatgpt.site/).** Allow the initial Unity download to finish. Basic gameplay and local tutorial hints require no API key.
2. **Explore and build.** Click to move the rover, uncover ore, place an extractor, and connect power and rails. Notice how the relevant advice changes with the colony.
3. **Connect AI help.** Open the gear immediately left of **Copilot**, enter your own OpenAI API key, and choose **Connect for this tab**. Open AstraBot and enable **Live screen help** for screenshot-based advice.
4. **Ask about the current situation.** Try “What should I do next?” and use **Show me** to locate the suggested action.
5. **Delegate exploration.** Enter “Explore the map and discover new ore,” request a plan, review it, and select **Start plan**. Watch the rover reveal terrain, then use **Stop** to take back control.

**Key and privacy note:** the public demo keeps the key in tab memory and sends it directly to OpenAI, not to a game backend. This is a hackathon BYOK design, not a secure way to distribute a shared production secret. Use a restricted, low-budget key and revoke it after testing; browser scripts/extensions may access credentials. Reload or **Forget key** clears it. AI requests send game screenshots and state to OpenAI and incur API usage. [Details and limits](docs/COACH.md).

The repository may be ahead of the public build; [deployment records](docs/DEPLOYMENT.md) identify the published version.

## The game underneath

- **32 × 32 frontier:** fog of war, two terrain elevations, traversable slopes, and grid-based rover pathfinding.
- **Explore to expand:** infinite deposits with higher-yield, larger patches farther from the colony.
- **Shared power economy:** solar generation charges a colony battery used by rover movement and working extractors.
- **Rail logistics:** deliver ore to earn credits; expand and upgrade mining and train services.
- **Fluxite fuel chain:** transport fictional space fuel to power plants for stronger generation.
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

Make that package available before opening the project. Open `Assets/Scenes/AstraExpress.unity` and enter Play mode for gameplay iteration. The browser copilot UI requires a Web build: choose **Astra Express → Build Web**, then run `bash "Run Local.command"` with Python 3 installed and visit `http://127.0.0.1:8090/`. The current Web client still uses its direct, tab-key API flow when served locally.

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
