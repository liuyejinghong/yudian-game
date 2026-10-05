# terrain-original geometry report — ART-LF-BATCH-01 subpackage B

- Scope: independent 9x9 m sculpting-shape REFERENCE terrain, 49x49
  vertex grid, boundary height 0, origin grounded. Shape reference
  only: not product-authoritative terrain, no ore emission, no
  inventory, no deformation system; formal terrain access BLOCKED.
- Formula: ticket-exact h0/hill/ore gaussians; variant = terrain-original.
- Mesh: TerrainSurface soil_mars, 2401 verts, 4608 tris; normals up;
  topology identical across the three variants.
- Ore rocks: 3 original low frame_dark blocks (OreRockA/B/C) seated
  on THIS variant's own surface (dug rocks sit in the pit).
- Envelope (glTF m): min [-4.5, 0.0, -4.5] max [4.5, 1.184066, 4.5].
- Blender: 4.5.14 LTS; static asset, no clips.
- Reopen/re-export: max_float_diff=0.0, identical_bytes=True.
- NOT_RUN: gameplay ore/inventory; deformation/terrain system; Godot engine verification NOT_RUN by this subpackage (master native suite); UV/baking/LOD; owner visual acceptance
