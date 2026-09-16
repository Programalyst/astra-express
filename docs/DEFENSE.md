# Alien raids and laser defense

Implemented gameplay, included in Sites version 13 on 15 September 2026. See [deployment record](DEPLOYMENT.md) for release scope and verification; the historical local playtest below predates publication.

## Trigger and approach

- The first produced unit from either 2x2 Ore extractor permanently awakens raids for that game. Exploration, placement, unpowered mines, paused mines, and 1x1 production do not trigger them.
- A visible warning starts a 20-second countdown. Waves then arrive every 45 simulation seconds from the northwest/top-left cell `(0, 31)`.
- Waves start with two aliens arriving 1.5 seconds apart, add one every two waves up to six, and never exceed 12 living aliens. Pausing mining does not reset the threat. Restart clears it.
- Aliens select the nearest reachable operational building by four-neighbor walking distance. They use ramps, avoid hillsides and building footprints, and re-evaluate routes when construction changes. They can travel through unrevealed terrain without revealing it to the player or AstraBot.
- Each alien has 40 HP, walks at one surface tile-equivalent/second, and deals 6 damage once per second from an adjacent reachable tile. Disabled buildings are skipped; vehicles and infrastructure are not attacked. Alien crowd collision is not simulated.

## Build defenses

Use **Turret** in the bottom toolbar after **Plant**, or press **7**. The compact **Frontier Defense** panel now only shows raid information. These refinements are local until the next deployment.

- Cost: 100 credits; footprint: 1x1 clear, explored, level tile with a clear south port.
- Connect the south port to colony conduits. No rail or owned train is needed.
- Turrets automatically target the nearest revealed alien within seven tiles, deal 15 damage every 0.75 seconds, and consume 2 battery power per shot. They use power before extractor allocation, including the mining reserve. Empty batteries, disconnection, or disablement stop firing.
- Terrain can block the line of fire; buildings are not projectile occluders in this version. The cyan range outline is a radius, not a guarantee of line of sight. Lasers are instant hits with brief visible beams.
- Requested art: `Assets/Kenny/kenney_space-kit/Models/FBX format/alien.fbx` and `turret_single.fbx`. These remain vendor assets and are referenced by the saved scene and scene-creation utility.
- Beam LineRenderers use `Assets/Materials/Red Laser.mat`, assigned through the AstraGame **Laser Material** field. Edit that asset to tune the beam without affecting cyan power conduits or range outlines. A generated cyan material is only a fallback for an unassigned field.

## Damage and recovery

Every building has 100 HP. At zero it stays in place but stops functioning. Extractors stop mining/loading; solar and plants stop generating; turrets stop shooting; a disabled colony stops accepting deliveries. Conduits and the shared battery remain intact, so disabling the colony does not cut the entire electrical network.

Stock, partial mining/burn progress, freight, upgrades, and train ownership are retained. Trains wait at disabled loading/unloading buildings with their cargo intact; repair does not restart manually parked services or unpause machines.

Select a damaged building and use **Repair** beneath its sidebar. Repair is free and needs no power, completes after 10 simulation seconds, and is interrupted by further damage. Disabled buildings are not attacked while waiting for repair. This provides recovery even at zero credits or battery; defense is still needed to prevent renewed attacks.

Game Pause freezes aliens, firing, waves, and repairs with the rest of the simulation. Focus loss retains the game's existing idle-simulation behavior. No offline catch-up, defeat screen, demolishing, turret upgrades, or automatic repairs are introduced.

## AstraBot and validation

Coaching receives building health/disablement/repair status, wave timing, and only revealed alien positions. It offers local defense/repair guidance. Turret construction and repairs are manual-only; the bounded planner cannot execute them. Existing approved conduit actions can power a selected turret.

`tests/DefenseChecks.cs` exercises the real simulation: both large-deposit triggers, countdown/pause, northwest spawns, bounded waves, nearest targets, terrain traversal, damage, disablement/repair, powered firing, fog/range/terrain restrictions, and cargo conservation. Legacy economy scenarios explicitly disable raids to isolate their original assertions; defense scenarios use the normal enabled simulation.

Run the full validation set in `AGENTS.md`. Automated checks are not visual evidence; inspect the freshly rebuilt player separately for model references, readability, aiming, and beam visibility.

For an isolated visual fixture, enter fresh Play Mode and choose **Astra Express / Defense playtest (fresh Play Mode only)**. It refuses an existing developed colony, reveals the map for inspection, builds a normally priced and wired large mine plus turret, then advances the real simulation until the first laser fires and pauses. Resume to watch combat; exit Play Mode to discard. This is an Editor-only revealed-map test, not evidence of ordinary-player discovery or model vision.

15 September local verification: Unity compiled cleanly and rebuilt the Web player. The browser showed the separate Frontier Defense control without changing the bottom toolbar. The Editor fixture reached wave 1 with two aliens, 56 credits, and 97.9 battery; a captured game-camera frame showed the assigned turret model firing a visible beam at an approaching alien. This verifies the first-shot presentation, not a full natural-play balance test. The fixture was discarded by exiting Play Mode. No Sites deployment or model-inference evaluation was performed.
