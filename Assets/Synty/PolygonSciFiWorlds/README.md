# Astra visual asset subset

Imported at the owner's request from:
`Asset Preview/Assets/Synty/PolygonSciFiWorlds`.
The source project is unchanged. The rover's visual dependencies are:

- `Prefabs/Props/Vehicles/SM_Veh_Apc_01.prefab`
- `Models/SM_Veh_Apc_01.fbx`
- `Materials/Alts/PolygonScifiWorlds_Mat_01_A.mat`
- `Textures/Alts/PolygonScifiWorlds_Texture_01_A.png`
- `Textures/Emissive/Emissive_01.png`
- `Textures/Misc/PolygonScifiWorlds_Texture_A_01_Normal_8k.png`

Source GUIDs are preserved. The copied prefab has its 11 mesh/sphere colliders
removed: movement and picking belong to Astra's existing grid, not vehicle
physics. This also removes an otherwise unused dependency on a legacy convex
collision asset with a missing editor-tool script. Meshes, wheel/door hierarchy,
UVs, and the source URP material are retained. No source FBX/PNG files are edited.

Texture import limits are 2048 for the color atlas and 1024 for the normal and
emission maps, including platform settings. The renderer preserves the source
URP material, normal map, emission map, and smoothness instead of converting it
to the simplified Kenney material. The rover is uniformly fitted within a
1.7-unit horizontal extent and 1.1-unit height and grounded automatically. Its
heading uses the prefab's +Z forward axis. Wheels are static visual meshes; no
wheel-physics simulation is added.

The colony also uses `Prefabs/Buildings/SM_Bld_Pod_Research_05.prefab` and its
matching model, Mat_02_A color atlas/material, normal/emission maps, and Glass_01
material. Existing Mat_01_A color texture is reused by the glass material. The
copied prefab omits 18 mesh colliders and their legacy collision-only assets;
Astra uses grid picking rather than these physics meshes. Its hierarchy and
source materials are preserved, including transparent glass and emission.
Color texture import limits are 2048, normal/emission maps 1024. The colony
retains its 2×2 footprint and south port, fitting within 3.6 units horizontally
and 2.6 units vertically on the existing foundation.

These are licensed Synty assets, not CC0 assets. Keep them subject to the owner's
applicable asset license; copying them here does not grant redistribution rights.
