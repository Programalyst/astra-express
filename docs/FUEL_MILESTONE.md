# Fuel-powered frontier

13 September 2026 · extension of the first playable ore checkpoint

## Implemented

- Infinite green Fluxite deposits at `(13, 3)` (1x1) and `(4, 17)` (2x2), with ordinary extractor costs and output upgrades.
- Tool **6 / Plant** builds a 2x2 plant for 250 credits on clear, explored terrain. Its south port needs conduits for generation and rails for deliveries.
- Plants store 48 fuel and generate up to 8 power/second. Each Fluxite provides 40 power. Plants retain unused burn energy and stop consuming fuel when disconnected, paused, or the battery is satisfied.
- **Fleet** supports up to four locomotives; the first is free, additional trains cost 150 credits. Arrow buttons select a train for its individual capacity upgrades and park command.
- A Fluxite extractor's destination button cycles available plants. Dispatch assigns the first idle train; ore extractors always deliver to the colony.
- Ore pays 8 credits per unit delivered. Fuel never pays credits. Both inventories are independently conserved through production, loading, delivery, and combustion.
- Full plants make trains wait with cargo aboard. A park request never discards that cargo. Once unloading finishes, the train travels back to the colony before becoming available.
- HUD distinguishes solar, actual fuel output, and mining demand. Plant panels show stock, remaining burn energy, and connection/supply/pause state. Fuel deposits, extractor towers, and train payloads are green and labelled.
- Restart requires a second click; Escape cancels the pending confirmation. Delivery diagnostics use current counters rather than a stale one-second sample.

## Suggested second route

Keep the first ore service running to fund these purchases; the starter 500 credits do not buy the entire fuel chain immediately.

1. Explore southeast to reveal all of the small Fluxite deposit `(13, 3)` and its port `(13, 2)`.
2. Build its 150-credit extractor. Lay conduits and rails from the established ore port `(11, 6)` down to `(11, 2)`, then east to `(13, 2)`. Use **R** to select the vertical-first bend. Do not run the route through the extractor at `(13, 3)`.
3. Explore and build a 250-credit plant at `(8, 2)`; its footprint extends through `(9, 3)` and its port is `(8, 1)`. Keep the rover off that footprint while building.
4. Connect conduit and rail from colony port `(5, 6)` down to `(5, 1)`, then east to plant port `(8, 1)`, again vertical-first to avoid the plant footprint.
5. Buy a second locomotive in **Fleet**. Select the Fluxite extractor, verify its destination is the plant at `(8, 2)`, and dispatch.
6. Upgrade the ore extractor or expand to another mine to put the extra power to work. A full battery deliberately idles the reactor; the top bar reports actual generation, not an unconditional +8.

Rail and conduit networks can share tiles. The train reaches the fuel mine from the colony initially, then shuttles directly between the connected mine and plant. Trains may pass through one another; signalling and collision scheduling remain out of scope.

## Verification

66 checks pass in the existing temporary .NET harness at `/private/tmp/astra-validation/Validation.csproj`, linking the actual simulation source. This includes the original ore/rover/solar checks and 27 additional checks for fuel placement, fleet purchases, destination validation, simultaneous ore and fuel routes, separate resource accounting, zero-battery startup, fuel retention, full-plant backpressure, safe parking, per-train upgrades, fleet limits, and global pause. Fixture routes use unoccupied cells; the old solar fixture was moved one row to avoid the newly added fuel deposit.

The Unity bridge reports clean compilation. The fuel/fleet Web export succeeded at 11:13 Singapore time; a subsequent template-only export succeeded at 11:32, reporting 66,374,321 bytes. Chrome loaded the updated fuel/fleet interface and rover exploration revealed the first ore patch. A complete interactive fuel delivery and plant-generation playtest is still pending; the automated simulation checks cover that chain, but do not substitute for UI validation.

## Remaining scope

No save/load, offline earnings, demolition/refunds, train selling, multi-stop schedules, signals, audio, or final art pass. The plant uses the existing Kenney generator with generated reactor/exchanger props; vendor models and the requested solar asset are unchanged. Submission hosting must use OpenAI Sites. Direct-build compatibility and the suggested external-asset fallback remain unverified; nothing has been published. The local build is not a public submission URL.

For teammate setup, pull these source changes and open `Assets/Scenes/AstraExpress.unity` in Unity 6000.3.24f1. The optional, machine-specific semantic bridge package was removed during the coaching merge, so teammates no longer need that absolute local package path to open the project. The gameplay world is generated on entering Play Mode, so no new scene or vendor-asset changes are required for this milestone.
