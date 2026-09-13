# Two-elevation terrain milestone

13 September 2026. The colony remains in the lowlands. Columns 18–27 form an upper plateau 1.5 Unity units higher. Column 17 is an impassable hillside except for straight ramp passes at cells `(17, 5)` and `(17, 15)`. The existing eastern 2x2 and 3x3 ore deposits lie on the plateau. Coordinates are zero-based.

The northern 2x2 Fluxite patch at `(4, 17)` now sits on a separate plateau at the same upper elevation. Its flat area spans columns 0–8 and rows 16–21, including the extractor's south port at `(4, 16)`. Hillsides line row 15 and column 9, with a northbound ramp at `(4, 15)` and an impassable outer corner at `(9, 15)`. The lowland corridor between the plateaus remains open. The deposit's location, yield, and infinite supply are unchanged.

## Rules and controls

Cliff-art follow-up: blocked straight walls now use the owner's `terrain_sideCliff_double.prefab`, replacing `terrain_side.fbx`. Its two children are centered from their combined renderer bounds and fitted to one 2x2-unit tile with a 1.5-unit rise. No prefab anchor change is required. A temporary Unity probe verified all 34 straight walls' bounds, two readable child meshes and colliders per tile, and 10 samples across the two joins with the northern outer corner (maximum height difference 0.0005 units). The probe was removed. Ramps and simulation traversal rules are unchanged; this art update has not been deployed.

- Rover and train paths use A* over the existing four-neighbor grid. Heights do not introduce NavMesh or a third cell coordinate.
- Ramps connect only their low and high ends. Hillside cells, diagonal shortcuts, and turns across ramp sides are blocked.
- The rover and trains move at two tile-equivalents of surface distance per second. The rover pays two battery units per surface tile; traversing low cell 16 through a ramp to high cell 18 costs 4.5 power in either direction, before simultaneous generation.
- Rails and conduits follow the same valid terrain connections. An invalid corridor is rejected without spending credits. Use R to change the L-shaped route bend, or build multiple corridor segments through a revealed ramp pass.
- Buildings and their south ports must be flat and at the same elevation. Ore patch footprints remain level and all resource ports remain reachable.
- Terrain colliders determine mouse selection. Vehicles tilt on ramps; tracks, conduits, previews, foundations, ports, and labels follow elevation.
- Fog keeps its existing permanent radius-based reveal. Hidden terrain uses the fog material, including bedrock. Ramp labels appear only after exploration. Elevation-based line-of-sight is not implemented.
- No bridges, tunnels, terraforming, slope construction, or realistic railway gradient limits.

## Advisor integration

Read `game.Simulation.Terrain` from `AstraGame`. `Kind(cell)` returns `Flat`, `Ramp`, or `Hillside`; `Elevation(cell)` returns the flat/base level; `Walkable(cell)` and `CanTraverse(from, to)` describe valid movement. Use `HeightAt(column, row)` for actual continuous height in world units, including halfway up a ramp. `Cell.Y` and `RoverY` still mean the north/south grid coordinate, not vertical height. The existing simulation and resource APIs are preserved.

`Uphill(cell)` describes a slope's grid direction; ramps are no longer all east/west. `IsCorner(cell)` identifies the blocked outer-corner hillside. Continuous corner height is a simplified profile; picking uses the Kenney mesh, and no vehicles or buildings occupy that cell.

An advisor should explain blocked cliff-crossing corridors, direct players toward explored ramp passes, and avoid revealing hidden deposits or undiscovered passes from authoritative map data.

## Validation

Northern plateau follow-up: all 123 external simulation checks pass, including north/south ramp travel and energy costs, level extractor placement, northern power/rail routing, fuel delivery to a lowland plant, and fuel conservation. A temporary Unity probe passed 1,743 collider samples covering all walkable terrain, verified the corner mesh orientation and Web-readable import, and issued a northern uphill rover order. The raised Fluxite patch was also inspected in Play Mode at the updated 35° camera pitch. The probe was removed afterward. No Web rebuild or Sites deployment was performed for this follow-up.

Original eastern plateau validation:

- 102 external pure-C# simulation checks pass: the previous 66 economy/fuel/fleet checks plus 36 terrain checks.
- New coverage includes both ramp directions, side exits, nearest-pass routing, resource footprint levels and reachability, atomic construction rejection, power/rail cliff isolation, exact movement-energy accounting, depletion and recovery on a slope, and plateau delivery/parking cargo conservation.
- A temporary Unity Editor probe verified 1,848 collider heights against the simulation across every tile, including three positions along each ramp. The probe was removed after validation and does not ship.
- Runtime-created terrain MeshColliders require CPU-readable meshes in the Web player. The current terrain assets `terrain.fbx`, `terrain_ramp.fbx`, `terrain_sideCliff.fbx`, and `terrain_sideCorner.fbx` have Read/Write enabled; their importer metadata must travel with the source changes. The previously used `terrain_side.fbx` also retains its readable setting.
- Unity Play Mode demonstrated an upper-plateau extractor powered through a ramp, repeated paid train deliveries, and a mouse-issued downhill rover order.
- The corrected Web build succeeded and was exercised in local Chrome at `http://127.0.0.1:8093/`: a mouse-issued order moved the rover from the lowlands through the ramp to `(19, 6)`, displaying "Upper plateau" and revealing the 2x2 deposit. The earlier non-readable-mesh errors no longer appeared on startup.
- The terrain map is a fixed first layout, not a procedural terrain generator. Major route blocking by future construction is still a player concern; no demolition/refund system has been added.

The owner subsequently requested deployment: Sites version 4 published successfully at 14:11 Singapore time on 13 September 2026, including both plateaus and the camera upgrades. Further releases remain batched and require an explicit deployment request.
