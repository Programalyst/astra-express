# Grid Crystal Patch import

Visual prefab imported from the owner's Nuclear Knights project. Open `Prefabs/Environment/Grid Crystal Patch.prefab` for manual editing. It is not placed in the scene or connected to Astra gameplay.

Preserves the crystal/drill hierarchy, transforms, materials, textures, animations, and nested ring prefab. Reuses the existing Synty SciFi Worlds material by GUID. The source project is unchanged.

Compatibility changes in the copied prefab: removed the Nuclear Knights-only GridObstacle component and its fog-material dependencies, and removed one already-disabled MeshCollider whose legacy convex-mesh asset has a missing editor script. Cleared an unresolved legacy cubemap slot in Crystal.mat; its URP Lit shader does not use that slot. No gameplay scripts were imported.

Source GUIDs are preserved. Assets remain subject to their original licenses, including Synty and other third-party content; this import does not grant redistribution rights.

## User-authored ore visuals

`Prefabs/Environment/Orefield.prefab` replaces the Kenney rock used for ordinary ore deposits. The game preserves the prefab's materials and authored root scale of 1, with one centered, ground-aligned cluster per deposit cell and deterministic rotation. Orefield skips automatic bounds fitting, so its artwork can extend beyond its logical tile. Deposit footprints, per-cell fog reveal, yields, and resource visibility beneath extractors are unchanged. Fluxite continues to use `Single Crystal.prefab` with its existing automatic sizing.
