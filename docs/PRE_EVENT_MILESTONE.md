# Pre-event implementation milestone

13 September 2026 · 90-minute implementation window · 30-minute buffer protected

## Goal: first profitable mining railway

Deliver a small, playable vertical slice: explore with the rover, discover an infinite ore deposit, build and power an extractor, connect a railway, and watch a train deliver ore to the colony for credits. Use the supplied Kenney packs, including the explicitly selected solar-panel-landscape-group.fbx. Aim to have the scene playable in Unity and the same loop verified in a locally served Web build by approximately 09:35 Singapore time.

This is a pre-event checkpoint, not completion of the full game design. The latest user message authorizes implementation. Keep approximately 09:35–10:05 free as a buffer rather than silently extending feature work into it. If work is blocked, preserve the verified subset and report the blocker; an Editor-only result does not satisfy the browser acceptance criterion.

## Required scope

- One small authored square-grid map with initially hidden terrain and infinite 1 by 1, 2 by 2, and 3 by 3 ore deposits at increasing distances and production rates.
- Click-to-move rover, permanent exploration reveal, distance-based power consumption, and a shared regenerating battery. Use the proposed remote charging and protected starter-solar recovery rules for this checkpoint.
- One colony, one starting solar installation, additional purchasable solar installations, and extractor placement on fully revealed deposits.
- Independent cardinal conduit and rail connectivity. A corridor may contain both. Connected extractors consume power continuously while working and store output locally; full storage or insufficient power pauses production.
- One free train and one active two-stop service at a time. Visibly transfer stored ore from extractor to train to colony. Only unloading at the colony earns credits.
- Basic credit/battery/status UI, construction feedback, camera controls, cancellation of previews, and restart. Invalid construction must not charge credits.
- Supplied rover, solar, and train models where compatible; simple generated geometry for missing infrastructure. Preserve imported vendor assets and wrap them in gameplay prefabs or presentation objects.

The matching extractor footprints, provisional balance, permanent fog reveal, and remote charging remain reviewable implementation defaults, not newly confirmed user design requirements.

## Time allocation

| Elapsed | Checkpoint | Exit condition |
| --- | --- | --- |
| 0–15 minutes | Editor/build plumbing and asset integration | Refresh/recompile loop checked; attempt an early minimal Web export; identify build blockers before relying on Editor success |
| 15–35 minutes | Exploration and power | Rover reveals the grid and deposits, spends power for actual travel, and recovers through starter solar |
| 35–65 minutes | First paid delivery | Placement, power connectivity, extraction, rail route, train cargo, and one-time delivery payment work together |
| 65–90 minutes | Feature freeze, validation, handoff | Fix core-loop defects, export current Web build, test over local HTTP, record controls and known issues |

These are targets, not guaranteed timings. Build/import waits count against the window. If the first Web build runs long, continue independent source work while it runs, but do not overlap Editor operations or mutate the input of an active build. Reassess scope at each checkpoint. After minute 65, add no new features.

## Stretch and subsequent work

If the complete loop and browser path are stable before the freeze, add one train-capacity upgrade and one extractor-output upgrade. Do not sacrifice validation for them. These upgrades remain part of the full game even if absent from this checkpoint.

The fuel-to-power-plant chain is the next gameplay milestone, not cancelled scope. Use a resource identifier and typed inventories so the ore loop can later support fuel without conflating power generation and credit income. Working name: Fluxite, subject to review. Infinite fuel deposits are mined using the shared battery; trains deliver fuel to a connected plant, which consumes stored fuel over time to generate substantially more power than a solar installation. Solar bootstraps the chain and provides recovery when fuel runs out. Fuel delivery to the plant must not also award ore-sale credits.

Leave multiple simultaneous services, signalling, fuel plants, save/load, demolition/refunds, extensive onboarding, audio, custom shaders, and public deployment outside this 90-minute commitment. Preserve the full-game requirements in the game design. Restrict unsupported route/building removal rather than allowing cargo loss. Public hosting and credentials are still unverified; a local Web build is not a submission URL.

## Acceptance checks

1. From a fresh start, a player discovers a deposit and earns the first delivery without Editor intervention or debug currency.
2. Hidden deposits cannot be selected or exposed by placement previews; large patches cannot be partly occupied by an extractor.
3. A disconnected extractor produces nothing; a powered one stores ore without earning money until delivery.
4. Produced ore equals ore in extractor storage plus cargo aboard the train plus ore sold. Repeated unloading cannot pay twice.
5. Battery bounds, credit bounds, storage limits, invalid purchases, and zero-power recovery remain valid.
6. The same essential sequence runs in the locally served Web build, with usable controls and no blocking runtime errors.
7. Handoff records launch instructions, controls, validation actually performed, known issues, deferred work, and actual Astra contributions. Leave changes uncommitted unless asked.

## Operating requirements

Unity must remain open and the development Mac awake. Keeping Unity foregrounded is the current workaround for background Editor throttling; refresh_assets does not establish that throttling is fixed. A sleeping or disconnected development machine can interrupt local tools and validation. Use the semantic bridge serially and reacquire Unity instance IDs after reloads. Do not modify the sibling bridge during this milestone unless a specific blocker is coordinated with its other session.
