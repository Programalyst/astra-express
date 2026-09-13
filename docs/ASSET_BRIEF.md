# Astra Express Game Asset Brief

Version 0.5 · Grid exploration and extraction · Supplied packs selected

## Supplied assets

The user imported `Assets/Kenny/kenney_space-kit/` and `Assets/Kenny/kenney_city-kit-industrial_2.0/`. Both include CC0 license files. Preserve the licenses, model files, and Unity metadata.

Solar installations must use `Assets/Kenny/kenney_city-kit-industrial_2.0/Models/FBX format/solar-panel-landscape-group.fbx`. Its preview shows a grouped panel assembly; measure the imported bounds before choosing its grid footprint rather than assuming one mesh equals one tile.

The Space Kit contains `rover.fbx`, `monorail_trainFront.fbx`, `monorail_trainCargo.fbx`, straight/corner monorail pieces, crystal clusters, and generator props under `Models/FBX format/`. The runtime uses the rover, locomotive, generator, and rock models. The fuel milestone adds green-tinted rock deposits, matching extractor towers and cargo, and a generator-based plant with generated reactor/exchanger props. Crystal meshes, dedicated rail pieces, cargo wagons, and final plant artwork remain candidates for the art pass. Wrap supplied meshes in gameplay presentation objects rather than editing vendor FBX files.

## Direction and review

Create a readable low-poly space landscape viewed with an orthographic camera. The rover reveals deposits beneath soft fog, and the player fills explored terrain with panels, conduits, extractors, and railways. Discovery and a visibly operating network are the visual rewards.

Use a square grid throughout. Deposits occupy 1 by 1, 2 by 2, or 3 by 3 tiles, with larger, higher-yield sites farther from the colony. Fit the selected Kenney assets to this modular direction; their exact dimensions and ports still need to be checked in Unity.

All deposit tiers are infinite and retain their mineral appearance. Visual feedback communicates output rate, machine activity, and stored cargo; depleted-patch artwork and reserve meters are unnecessary.

The user and teammate refine art direction, may provide asset packs, and review lighting and shaders. The assistant integrates chosen assets and validates them in Unity and the browser. Existing compatible assets should be assessed before producing replacements.

This brief follows the revised [game design](GAME_DESIGN.md). The supplied packs were imported by the user. The assistant has now integrated their selected models, added shared URP presentation materials, and generated the prototype terrain, infrastructure, and placement geometry at runtime. Vendor FBX files remain unchanged. See [Playable handoff](PLAYABLE_HANDOFF.md) for the validated checkpoint.

## Palette and readability

| Role | Proposed colour | Additional cue |
| --- | --- | --- |
| Terrain | Muted violet, #6A567D | Broad ground and rock forms |
| Structures | Warm off-white, #E6E2D5 | Dark bases and distinct silhouettes |
| Ore | Orange, #F1A04B | Chunky deposit and cargo rock shapes |
| Power and solar | Cyan, #66C5D5 | Panel grid, lightning icon, lit conduits |
| Colony activity | Warm yellow, #FFD37A | Windows and delivery effect |
| Fog | Deep blue-violet, #24263D | Soft opaque boundary |

These are starting art directions, not an approved palette or verified accessible UI scheme. Use high-contrast panels for text and pair icons or labels with colour. Rails and conduits need different shapes even when they share a corridor.

## Core asset set

| Asset | Minimum set | Visual requirement |
| --- | --- | --- |
| Rover | One body with separate wheels | Clear front, movement, selection, and exploration character |
| Colony | One base with battery and depot details | Obvious starting point and readable connection ports |
| Solar installation | One reusable assembly | Distinct panel surface and connected state |
| Ore deposit | 1 by 1, 2 by 2, and 3 by 3 modular patch arrangements | Footprint and increasing scale readable; fog reveals only explored cells |
| Extractor | Shared machine parts with proposed foundations matching the three patch sizes | Distinct tier scale, edge ports, inventory, and working state without stretching the same mesh |
| Conduits | End, straight, corner, T, crossing | Continuous connections and distinct power overlay |
| Track | End, straight, corner, T, crossing | Consistent edge connections; room for conduit beside rail |
| Cargo train | One body and ore payload attachment | Loaded and empty states, visible capacity upgrades |
| Terrain | Ground and blocking rocks or craters | Buildable and blocked ground distinguishable after reveal |
| Fog | Exploration mask and soft edge material | No hidden-object or shadow leakage |
| UI icons | Ore, credits, power, rover, construction tools, upgrade | Readable at 32 pixels with text |
| Placement indicators | Footprint, ports, route preview, selection | Validity, price, and active layer obvious |
| Sound | Move, discovery, build, power connection, delivery, upgrade | Short cues comfortable under repetition |

Add decorative props, dust, power-flow effects, upgrade attachments, and ambient music after the core set works. The current scope does not require refinery, food, alloy, greenhouse, gate, humanoid, or combat assets. Simple transform animation is sufficient for wheels and drills.

## Candidate sources

Inspect [Kenney Space Kit](https://kenney.nl/assets/space-kit) for compatible exterior structures and props. Its listing contains 150 files under CC0; verify the actual subset before assuming it supplies the rover, train, or conduit system.

[Kenney Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) lists 70 files under CC0. Audition a small subset for exploration and machinery. [Kenney Space Station Kit](https://kenney.nl/assets/space-station-kit), with 90 files under CC0, is an optional supplement; its interior emphasis may be less useful here.

These listings were checked on 12 September 2026. Preserve licenses and record filename, creator, URL, license, and modifications. Record original and generated assets separately. Evaluate packs supplied by the user and teammate for style, pipeline compatibility, scale, shader support, and browser cost.

Blender can supply missing vehicle or infrastructure meshes. Image generation is optional for a mood reference or illustration; exact track geometry and the fog mask are better authored directly.

## Import conventions

The playable floor uses the Space Kit's `terrain.fbx`, retaining its rust-colored rock material through URP conversion. Each flat mesh fits one 2-unit cell with a narrow grid seam. Fog replaces every terrain material slot until that cell is explored. Ramps and raised terrain are not used: the current vehicles, rails, and building placement share a flat ground plane.

- One gameplay tile is 2 Unity units square. Use a consistent metre-based scale.
- Ground lies on XZ with Y up. Vehicles face positive local Z and use ground-centred pivots; wheels pivot around their axles.
- Agree small grid footprints and explicit edge ports for power and rail. Port alignment must survive rotation.
- Deposit footprints cover exactly 1, 4, or 9 logical cells at the common tile scale. Build larger patches from reusable ore clusters and foundations rather than enlarging the grid units.
- Proposed extractor variants share core machine parts and add foundations or drill assemblies for larger footprints. Output upgrades use separate attachments so machine level and deposit size remain distinguishable.
- Keep placement geometry and decorative overhangs within their declared footprint, and leave room outside edge ports for connections. Do not expose a hidden patch's full outline before exploration reveals it.
- Rails and conduits use separate meshes and slight lateral separation when sharing a tile.
- Export static meshes as FBX with deliberate axis conversion. Keep source Blender files separate.
- Apply transforms, aim for root scale 1, 1, 1, and avoid negative scales or embedded lights and cameras.
- Share a small material palette, usually one material per prop and no more than two per major building.
- Begin with 300 to 1,500 triangles per small prop or vehicle and below 3,000 per main building. These are budgets, not platform limits.
- Prefer flat colours or small atlases until an asset pack or approved direction warrants more.
- Use simple selection colliders and logical tile blocking rather than detailed moving mesh collision.
- Keep imported meshes separate from gameplay prefabs and commit Unity metadata with imported assets.

## Review checkpoints

First review the rover, terrain scale, camera, and fog reveal, including a partly revealed 3 by 3 patch. Next review all three deposit sizes, the proposed extractor footprints, colony ports, panels, and the power overlay. Then review track and conduit sharing a corridor, a loaded train, and the delivery effect.

Evaluate lighting and shaders on the actual Web build. Check that fog conceals shadows and effects, all statuses remain readable, and the chosen treatment maintains the target frame rate. Produce further variants only after these pieces work together.
