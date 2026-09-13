# Astra Express contributor guide

Read `README.md`, `docs/GAME_DESIGN.md`, and the relevant feature document before changing gameplay or AstraBot. Treat committed code and a freshly rebuilt game as the source of truth; historical playtest notes can describe older behavior.

## Current product state

- The game is a Unity 6.3 LTS (`6000.3.24f1`) WebGL colony and rail simulation.
- Completing a valid rail route automatically assigns the first idle locomotive. Buying a locomotive automatically assigns it to the first ready waiting route. A manually parked service stays stopped until the player dispatches it again.
- AstraBot supports local contextual hints, screenshot-aware coaching, and player-approved bounded plans. The game simulation validates every action and remains authoritative for credits, visibility, occupancy, routes, power, and fleet state.
- The current local browser build uses the same-origin Python hosted Agents API bridge. Advisory coaching and advanced construction plans use `gpt-6-astra`; pure rover/fog/ore-discovery plans route to `gpt-5.4-mini`. The public Sites release may still be the older direct Responses build. Keep deployed and local claims separate.
- The public Sites release is static. Never place a shared API key in source, generated Web files, commits, or deployment assets. `server/.env` is private and ignored.

## Product constraints

- Keep AstraBot optional, compact, dismissible, and easy to stop. Plans require explicit Start and must expose Stop throughout execution.
- Prefer visible crosshairs and highlights on the next actionable tile, port, or control. Keep advice short and grounded in what the player can currently see.
- Do not modify the bottom toolbar unless the user explicitly requests it.
- Never reveal hidden deposit coordinates to the model or use them to steer automated exploration.
- Do not claim visual understanding from mocked transport tests. Separate screenshot evidence, game-state validation, and local deterministic guidance in product text and test reports.
- For Astra visual evidence, withhold local next-action candidates, screen coordinates, UI rectangles, connection targets, route solutions, and precomputed world targets until after inference. Attach the local action afterwards, validate the returned image box against its withheld geometry, and label missed or unavailable grounding honestly. Show this evidence UI only for a successful `gpt-6-astra` Agents response.
- Preserve manual player intent: automation must not buy resources without an approved plan, steal active trains, restart manually parked services, or continue after Stop, Escape, tab hiding, or disabling AstraBot.

## Development workflow

- The package manifest expects the Unity Semantic Bridge at `../unity-semantic-bridge/com.gamenami.unity-semantic-bridge/`.
- After changing C# gameplay, WebGL templates, or generated browser contracts, rebuild the Web player before visual verification:

  ```sh
  /Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit -projectPath "$PWD" -buildTarget WebGL \
    -executeMethod AstraExpressEditor.BuildWeb
  ```

- Run the local game with `bash "Run Local.command"`, then open `http://127.0.0.1:8090/`.
- If Python prompts or schemas change, regenerate and verify the browser contract:

  ```sh
  python3 scripts/export-astrabot-contract.py > Assets/WebGLTemplates/Astra/astrabot-contract.js
  python3 scripts/export-astrabot-contract.py --check
  ```

## Required validation

Run checks appropriate to the changed area. Before handing off a broad gameplay or AstraBot change, run the complete set:

```sh
python3 -m unittest discover -s tests -p 'test_*.py'
node --test tests/*.test.cjs
python3 tests/run-simulation-checks.py
python3 tests/run-exploration-checks.py
python3 tests/run-bot-control-checks.py
git diff --check
```

Python HTTP tests bind temporary loopback ports. If a sandbox blocks local sockets, rerun them with loopback permission rather than treating `PermissionError` as a product failure.

Visual changes require direct inspection of a freshly rebuilt game at the target viewport. Check that the player can identify the next action, overlays do not cover the play area, trains move without collisions or abrupt turns, connected networks visibly reach building ports, and fog/resource effects remain readable.
