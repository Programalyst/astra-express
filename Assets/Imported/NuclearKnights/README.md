# Grid Crystal Patch import

Visual prefab imported from the owner's Nuclear Knights project. Open `Prefabs/Environment/Grid Crystal Patch.prefab` for manual editing. It is not placed in the scene or connected to Astra gameplay.

Preserves the crystal/drill hierarchy, transforms, materials, textures, animations, and nested ring prefab. Reuses the existing Synty SciFi Worlds material by GUID. The source project is unchanged.

Compatibility changes in the copied prefab: removed the Nuclear Knights-only GridObstacle component and its fog-material dependencies, and removed one already-disabled MeshCollider whose legacy convex-mesh asset has a missing editor script. Cleared an unresolved legacy cubemap slot in Crystal.mat; its URP Lit shader does not use that slot. No gameplay scripts were imported.

Source GUIDs are preserved. Assets remain subject to their original licenses, including Synty and other third-party content; this import does not grant redistribution rights.
