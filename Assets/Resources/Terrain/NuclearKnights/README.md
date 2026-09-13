# Nuclear Knights desert surface

Copied at the project owner's request from the Unity 2022 project:
`nuclear-knights/Nuclear Knights/Assets/Shader/Desert/`.
The source project has not been modified.

## Dependency set

- `Grid Desert.mat`: active ground material.
- `Desert Floor.shadergraph`: original URP Lit graph, including its UV tiling,
  color blending, procedural noise, normal strength, and optional displacement.
- `desert_sand_texture_by_jb1992_d5cgqi1-375w-2x.jpg`: material's dune texture.
- `Textures/windswept-sand-smooth.jpg`: material/graph normal map.
- `Textures/wind-sand-height-map.jpg`: graph's default dune texture.

Source GUIDs are preserved and were checked for collisions before import. There
are no custom functions, subgraphs, or gameplay-script dependencies. Texture
import settings are copied from the source, including the normal-map importer.

## Astra adaptations

- Dune Height: 0 instead of 0.1. Shader-only vertex displacement would disagree
  with the immutable picking collider, rover height, structures, and fog edges.
  The windswept appearance comes from the texture and normals, not displaced cells.
- Tiling: (1, 1) instead of (50, 50). The source uses a scaled built-in Unity plane
  with 0-1 UVs. Astra generates continuous world-coordinate UVs, initially one
  repetition per 6 world units; applying 50 again would make the sand detail tiny.
- Original color, normal strength, and other graph/material settings are retained.

Tune `Grid Desert.mat` in the Inspector, or override Terrain Surface Material on
Astra Express. Terrain Texture Repeat controls the generated mesh's UV scale.
The graph's Tiling multiplies the dune color texture UVs; the normal map uses UV0
directly, as in the original graph. No separate downloaded UV-map asset is needed.

## Provenance

These files are user-supplied project assets; no CC0 license is assumed. No license
document was found alongside this dependency set. Confirm the original texture
licenses before publicly redistributing the source assets or publishing a build.
