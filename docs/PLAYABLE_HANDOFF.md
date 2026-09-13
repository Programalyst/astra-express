# Astra Express playable checkpoint

13 September 2026 · Unity 6000.3.24f1 · Editor and local Chrome Web validation

This document records the first ore checkpoint. The subsequent [fuel-powered frontier milestone](FUEL_MILESTONE.md) adds Fluxite plants, a four-train fleet, new controls, and its own validation evidence; historical limitations below describe the first checkpoint only.

The later [two-elevation terrain milestone](TERRAIN_MILESTONE.md) adds the eastern plateau, ramp passes, height-aware A* routing, and slope-aware infrastructure without NavMesh, bridges, or tunnels.

## Result and timing

The first profitable mining railway loop works in both Unity Play Mode and the locally served Web build. The original approximately 09:35 Singapore target was not met: the first Web build failed at 08:34 because the semantic bridge's runtime assembly referenced an Editor-only API. The user fixed that dependency in its separate repository. The retry succeeded at 10:34, followed by interactive browser and Editor checks. Do not describe this checkpoint as delivered within the original 90-minute window.

The implemented scope is exploration, permanent fog reveal, a shared solar-charged battery, infinite tiered ore deposits, solar/extractor purchases, independent power and railway construction, one automated two-stop train, paid deliveries, and capacity/output upgrades. Fuel-fired power remains the next gameplay milestone.

## Launch

In Unity, open `Assets/Scenes/AstraExpress.unity`, enter Play Mode, and select the Game tab. The world is created at runtime; the empty-looking Edit Mode scene is intentional. If the colony is paused after changing focus, click Resume or press Space.

The browser build is in `Builds/Web/`. A server was started on this Mac at `http://127.0.0.1:8090/`. If it is no longer running, execute this from the repository root:

```sh
python3 -m http.server 8090 --bind 127.0.0.1 --directory Builds/Web
```

Open `http://127.0.0.1:8090/` in Chrome. Serve over HTTP; do not open index.html as a local file. This URL is local to this computer, not a public submission URL.

To rebuild, exit Play Mode, keep the Web platform active, save intended scene changes, and choose **Astra Express > Build Web**. The menu builds the existing scene, not the currently displayed play session. Do not rerun **Create playable scene**: that setup action deliberately refuses to overwrite the existing scene.

For automation, the Editor helper accepts `setup`, `switchweb`, or `buildweb` in `Temp/AstraExpress/request.txt`, consumed by an Editor update. After `switchweb`, wait for script compilation and reload before requesting `buildweb`. Status is written to `Temp/AstraExpress/status.json`. The semantic bridge's `refresh_assets` imports external script changes; it is not itself a build command.

## Controls and first route

| Action | Control |
| --- | --- |
| Explore or select a building | Tool 1 / Explore, then left click |
| Build extractor | Tool 2, then click a fully revealed ore patch |
| Build solar | Tool 3, then click a clear, revealed 2 by 2 footprint |
| Lay conduit or rail | Tool 4 or 5; click start tile, then end tile |
| Change an L-shaped route's bend | R, before confirming its endpoint |
| Cancel a preview | Right click or Escape |
| Pan / zoom | WASD or arrows, middle-button drag / scroll wheel |
| Centre colony / rover | C / V, or the sidebar buttons |
| Pause / resume | Space or the top-bar button |
| Train controls | Train / Upgrades on the toolbar |

From a fresh game:

1. Click ground east of the colony. Moving the rover near cell `(10, 6)` reveals the first deposit at `(11, 7)`.
2. Build its 150-credit extractor. It starts disconnected and produces nothing.
3. Lay conduit from the colony port `(5, 6)` to the extractor port `(11, 6)`. Six new tiles cost 12 credits. The extractor now produces 0.5 ore/second while consuming 1 power/second.
4. Lay rail between the same ports. Six new tiles cost 18 credits. The two networks can share this corridor but are electrically independent.
5. Select the extractor and click Dispatch Train. The free train loads stored ore, returns to the colony, sells each unit for 8 credits, and repeats.

Construction leaves 320 of the initial 500 credits. A full four-unit delivery adds 32. Production alone does not pay. A new connected solar array adds 2 power/second; an unwired array adds nothing. Mines automatically protect a 10-power rover reserve. The starter solar array cannot be removed, so zero battery is recoverable.

Two-stop service means one source mine and the colony. To change the source, use Park at Colony, let outstanding cargo arrive, then dispatch from another rail-connected extractor. Routes and buildings have no demolition action in this checkpoint; that restriction avoids accidental cargo loss.

## Implementation map

- `Assets/Scripts/AstraExpress/ColonySimulation.cs`: grid, deposits, construction validation, separate network graphs, power allocation, movement, extraction, train state, inventories, credits, and upgrades. It has no Unity dependency.
- `Assets/Scripts/AstraExpress/AstraGame.cs`: runtime world presentation, Kenney model adaptation, camera/input, placement preview, HUD, and focus-pause behaviour.
- `Assets/Editor/AstraExpressEditor.cs`: scene setup and repeatable Web build workflow.
- `Assets/WebGLTemplates/Astra/index.html`: full-window canvas and loading/error screen.
- `Assets/Scenes/AstraExpress.unity` and `Assets/GameGenerated/Surface.mat`: serialized scene/model references and shared URP material.

The scene references the requested industrial `solar-panel-landscape-group.fbx`, Space Kit rover, hangar, generator, rock, and monorail locomotive models. Imported FBX files were not edited. Rails, conduits, foundations, and tile terrain currently use generated geometry. Deposit tiers occupy 1, 4, or 9 cells and produce 0.5, 1, or 1.75 ore/second before upgrades.

## Validation evidence

| Check | Observed result |
| --- | --- |
| C# refresh and reload | Native bridge refresh compiled the new game scripts successfully; subsequent Editor hierarchy queries succeeded |
| Web export | Build succeeded at 10:34:10 SGT in 69 seconds; reported output 66,345,619 bytes |
| HTTP delivery | Web.wasm returned HTTP 200 with `Content-Type: application/wasm` |
| Chrome fresh-start loop | Mouse-built extractor, conduit and rail; dispatched train; observed 448 credits after 16 ore sold, exactly `320 + 16 * 8` |
| Chrome sustained play | Repeated deliveries continued; train capacity and extractor output upgrades purchased through the UI |
| Chrome solar expansion | New array initially left generation at 2/s; wiring its south port raised generation to 4/s and the battery recovered |
| Unity Editor fresh-start loop | Repeated the exploration/construction/dispatch sequence through the Game view; diagnostic snapshot showed 608 credits, 44 ore produced/accounted, 36 sold, 4 aboard, and 9 completed deliveries |
| Runtime error check | Editor console during the interactive loop contained bridge startup and delivery logs, with no gameplay error reported |
| Simulation checks | 39 checks passed against the actual ColonySimulation.cs using a temporary .NET 9 harness; no extra test framework or package added to the game |

Simulation coverage includes hidden and duplicate construction rejection without payment, actual rover exploration, all three extractor footprints, partial-patch concealment, disconnected mining, independent power and rail, free reuse of existing network tiles, cargo conservation throughout repeated delivery and upgrades, safe train parking, full-buffer production pause, a ten-minute infinite-resource run, whole-simulation pause, exhausted-rover recovery, additional solar connectivity, route rejection through buildings, fair multi-mine power allocation, and the protected rover reserve. The temporary harness is `/private/tmp/astra-validation/Validation.csproj` on this development Mac; it is not a committed test suite and is not a substitute for browser interaction.

## Known limits and next work

- This is an uncompressed development Web build, approximately 63.3 MiB by the build report. Public hosting, compression, cold-network startup, mobile/touch support, and other browsers remain unverified.
- Art is functional, not final: square fog transitions, simple infrastructure geometry, no audio, no custom atmosphere, and runtime-generated scene geometry. UI text can become small in a narrow Editor Game panel.
- Changing focus pauses gameplay and requires an explicit resume. There are no offline earnings or save/load; Restart and page reload start a fresh colony.
- Only one simultaneous train service exists. No signalling, collision scheduling, refunds, demolition, fuel plant, or fuel transport is implemented.
- Generation shown in a selected solar array's sidebar is its rated output; the top bar is the actual connected total. An unconnected array is labelled Wire South Port.
- The bridge inspector currently renders the diagnostics string as character codes. Its live string updates once per second, and delivery log lines may contain the previous sample. Use the game HUD or decode the current inspector snapshot for exact observations.
- The bridge dependency is still an absolute local package path. Another machine needs the matching checkout or a separately agreed portable package reference.

Actual Astra contributions: translated the game design into a pure C# simulation, integrated the selected asset models and URP presentation, built the Editor automation and Web template, validated model invariants, identified the bridge's player-build blocker, and exercised the full route through native browser/Editor UI tools. The user supplied the packs and fixed the bridge in its separate session. The assistant did not commit changes; the user independently committed the first slice as `e5bba75` during development.
