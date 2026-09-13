# Astra Express Game Asset Brief

Version 0.5 · Grid exploration and extraction · Supplied packs selected

## Supplied assets

The user imported `Assets/Kenny/kenney_space-kit/` and `Assets/Kenny/kenney_city-kit-industrial_2.0/`. Both include CC0 license files. Preserve the licenses, model files, and Unity metadata.

Solar installations must use `Assets/Kenny/kenney_city-kit-industrial_2.0/Models/FBX format/solar-panel-landscape-group.fbx`. Its preview shows a grouped panel assembly; measure the imported bounds before choosing its grid footprint rather than assuming one mesh equals one tile.

The Space Kit contains `rover.fbx`, `monorail_trainFront.fbx`, `monorail_trainCargo.fbx`, straight/corner monorail pieces, crystal clusters, and generator props under `Models/FBX format/`. The runtime uses the locomotive, generator, and rock models; the original Kenney rover is retained in the pack but no longer selected. The fuel milestone adds green-tinted rock deposits, matching extractor towers and cargo, and a generator-based plant with generated reactor/exchanger props. Crystal meshes, dedicated rail pieces, cargo wagons, and final plant artwork remain candidates for the art pass. Wrap supplied meshes in gameplay presentation objects rather than editing vendor FBX files.

The rover now uses `Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_Apc_01.prefab`, copied with its FBX, URP material, and color/normal/emission textures from the owner's Asset Preview project. Its six-wheel APC silhouette is fitted to a 1.7-unit horizontal extent within the existing 2-unit cell. Source URP materials are preserved for this model. Imported physics colliders are removed; grid navigation, ramp alignment, rover power use, fog reveal, and camera follow remain unchanged. Texture imports are capped at 2K color and 1K normal/emission. Provenance and adaptations are documented beside the imported subset. These are licensed Synty assets, not part of the CC0 Kenney packs.

## Direction and review

Create a readable low-poly space landscape viewed with an orthographic camera. The rover reveals deposits beneath soft fog, and the player fills explored terrain with panels, conduits, extractors, and railways. Discovery and a visibly operating network are the visual rewards.

Use a square grid throughout. Deposits occupy 1 by 1, 2 by 2, or 3 by 3 tiles, with larger, higher-yield sites farther from the colony. Fit the selected Kenney assets to this modular direction; their exact dimensions and ports still need to be checked in Unity.

All deposit tiers are infinite and retain their mineral appearance. Visual feedback communicates output rate, machine activity, and stored cargo; depleted-patch artwork and reserve meters are unnecessary.

The user and teammate refine art direction, may provide asset packs, and review lighting and shaders. The assistant integrates chosen assets and validates them in Unity and the browser. Existing compatible assets should be assessed before producing replacements.

This brief follows the revised [game design](GAME_DESIGN.md). The supplied packs were imported by the user. The assistant has now integrated their selected models, added shared URP presentation materials, and generated the prototype terrain, infrastructure, and placement geometry at runtime. Vendor FBX files remain unchanged. See [Playable handoff](PLAYABLE_HANDOFF.md) for the validated checkpoint.

## Palette and readability

| Role | Proposed colour | Additional cue |
| --- | --- | --- |
| Terrain | Warm windswept sand | Continuous dune-textured ground, broad cliff forms |
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

Fluxite deposits use the owner's `Assets/Imported/NuclearKnights/Prefabs/Environment/Single Crystal.prefab`, assigned through `Fluxite Model` on Astra Express. Each deposit cell gets one crystal prefab, uniformly fitted within 1.55 units horizontally and 1.4 units vertically, grounded at its terrain elevation. Original prefab materials are preserved for manual color/emission edits; ordinary ore keeps its Kenney rocks. Fog visibility, extractor replacement, resource yields, and deposit footprints are unchanged.

Flat terrain now uses one generated `Continuous colony ground` mesh instead of individual `terrain.fbx` tiles. Its adjacent faces meet without inset square seams, and world-coordinate UVs continue across the lower floor and raised plateaus. The gameplay grid is unchanged: 2x2-unit cells, two elevations with a 1.5-unit rise, grid-snapped construction, and the existing rover pathfinding. This is not a single-height plane: plateau tops stay raised, with outer edge skirts, and the mesh leaves openings for existing ramps and cliffs.

The default `Assets/Resources/Terrain/NuclearKnights/Grid Desert.mat` and its `Desert Floor.shadergraph` come from the owner's Nuclear Knights Unity 2022 project, with the referenced sand color, normal, and graph-default textures. The source project is unchanged. The graph retains its color blending and windswept normals; Dune Height is disabled to keep rendering aligned with navigation, fog, and colliders. Material Tiling is reduced from 50 to 1 because Astra's UVs already repeat in world coordinates. Adjust the material directly or override `Terrain Surface Material` on Astra Express. `Terrain Texture Repeat` sets world units per UV repetition (default 6); UVs are generated by the mesh, not downloaded separately. The graph applies its Tiling only to the dune color texture, as in the source. Check original texture licenses before public redistribution; provenance and adaptations are recorded in the imported folder's README.

The Space Kit's `terrain_ramp.fbx`, `terrain_sideCorner.fbx`, and user-authored `terrain_sideCliff_double.prefab` retain their existing materials and fitting. The eastern plateau has east-facing uphill passes. The northern Fluxite plateau has a north-facing uphill pass, a west-facing eastern hillside, and an outer corner joining its edges. The original `TerrainModel` scene reference is retained for compatibility but no longer instanced. Vendor assets are unchanged.

The ground uses separate revealed/fog submeshes, updating only when exploration changes. Unexplored cells remain opaque; the animated fog veil and hidden deposits are unchanged. An independent immutable picking mesh maps raycast triangles back to logical cells, including after reveal updates. Cliff/ramp colliders remain separate and their FBX meshes must stay CPU-readable for Web picking. `Astra Express > Validate continuous ground in Play Mode` checks all flat cells, elevation, picking, UV continuity, cliff/ramp openings, and fog isolation. The grid is 32×32; existing colony, deposit, and ramp coordinates are preserved, with the plateaus extending to the expanded map edges.

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

## Post-processing

The colony scene uses a global `Colony Post Processing` Volume with `Assets/Settings/AstraPostProcessing.asset`. Edit its overrides in the Inspector to tune the look; the setup command reuses an existing profile without resetting artist edits. Main Camera has URP post-processing enabled, FXAA for softer jagged edges, and dithering for fog gradients. Existing sun intensity, ambient lighting, pipeline HDR support, and render scale are unchanged.

The saved profile uses Neutral tonemapping, zero exposure offset, contrast +6, saturation +2, and a light 0.10 vignette (smoothness 0.65). Bloom uses threshold 1.0, artist-tuned intensity 1.4, scatter 0.45, quarter-resolution sampling, three iterations, high-quality filtering, and no lens dirt. A newly created profile starts with intensity 0.18 and high-quality filtering disabled; the setup command preserves the saved profile's tuning. Depth-of-field, motion blur, chromatic aberration, and film grain are not added. Browser/device performance should be checked after tuning.

`Astra Express > Apply lightweight post-processing` connects the profile and camera in the saved colony scene; `Astra Express > Validate post-processing` checks the connection. New scenes generated by the setup command include the same profile. Both utilities are Editor-only; the saved Volume/profile/camera settings ship in the player. Native Play Mode visual comparison and console checks passed. Unity Web build succeeded on 13 September 2026 at 16:38 Singapore (69,888,744 bytes, 9 seconds). Browser/device performance still needs playtesting; deployment remains a separate explicit step.

## Review checkpoints

First review the rover, terrain scale, camera, and fog reveal, including a partly revealed 3 by 3 patch. Next review all three deposit sizes, the proposed extractor footprints, colony ports, panels, and the power overlay. Then review track and conduit sharing a corridor, a loaded train, and the delivery effect.

Evaluate lighting and shaders on the actual Web build. Check that fog conceals shadows and effects, all statuses remain readable, and the chosen treatment maintains the target frame rate. Produce further variants only after these pieces work together.
