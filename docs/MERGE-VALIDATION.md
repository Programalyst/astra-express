# Fuel/fleet and Pip merge validation

13 September 2026. Local coaching/UI work was committed as `93a6d59`, then merged with `origin/main` at `a4e2045` (Fluxite power plants, multiple trains, and Kenney terrain).

## Integration

The `AstraGame.cs` conflict was resolved by retaining both the terrain/fleet/fuel mechanics and the custom interface, coach input isolation, connected socket visuals, and numbered route guides. Fleet purchases and upgrades operate on the selected locomotive; the extractor panel uses idle trains before offering fleet management. Fluxite destinations, plant power, and both rail prerequisites are represented in the UI and coach state. The restart confirmation and Escape cancellation from upstream remain.

Pip's policy and server instructions now distinguish ore sales from fuel delivery, explain full plants and idle generation, and support up to four trains. The first ore tutorial remains available. Plant routes use the shared colony rail network. A clipped plant heading found in the browser was reduced to fit the panel.

## Reproducible checks

From the project root:

```sh
node --test tests/coach-policy.test.cjs
python3 -m unittest discover -s tests -p test_coach_server.py
python3 tests/run-simulation-checks.py
```

The simulation runner uses Unity 6000.3.24f1's bundled Mono on macOS; pass `--unity /path/to/Unity.app` for another installation. It builds in a temporary directory and links the actual simulation source. Its fixture reveals the map directly, so it validates mechanics rather than exploration or UI interactions.

- 31 policy tests and 15 server tests passed.
- Simulation harness passed 41 scenario assertions plus 37,792 repeated invariant assertions across 9,448 quarter-second steps. These are not 37,792 separate scenarios. Coverage includes simultaneous ore/fuel service, separate conservation and income, full-plant backpressure, cargo-safe parking, selected-train upgrades, redispatch, duplicate-service rejection, and fleet limits.
- Final WebGL build succeeded: 67,425,086 bytes, 23-second build step. Log: `Logs/merge-final-web-build.log` (ignored).
- Source whitespace/conflict checks and staged credential checks passed.

## Browser observations

Actual browser controls verified a second locomotive purchase (150 credits), its capacity upgrade from 4 to 8 (100 credits), and the first locomotive retaining capacity 4. Plant construction, readable final heading, connection preview, restart confirmation, Escape cancellation, and reset were checked.

The original ore path was played from a fresh colony. Extractor placement cost 150; guided power and rail routes cost 12 and 18. The first marker spent nothing, the second completed each connection, the ready dispatch action started service, and eight deliveries sold 32 ore for 256 credits (balance 320 to 576). The interface showed the assigned train running. These browser actions were agent-led with knowledge of the game.

One real game-frame request completed through the hosted Agents API on the merged server. Pip identified the visible colony/extractor ports and explained power before railway/dispatch. Health counters reported one completed frame, zero failed frames, and one session deleted; they also reported two cleanup failures, so this is not evidence of failure-free cleanup. Retention and retry limits remain documented in `AGENTS-INTEGRATION.md`.

A complete interactive Fluxite delivery chain was not replayed during this merge check; fuel delivery, generation, and parking were exercised by the simulation harness and coaching cases. This distinction matters when evaluating visual guidance for that chain.

Private `.env`, runtime sessions, logs, builds, and OS files remain excluded from Git. The 1,354 existing assets covered by LFS attributes but committed as raw blobs were checked byte-for-byte against HEAD and left unchanged; no bulk asset normalization was included.
