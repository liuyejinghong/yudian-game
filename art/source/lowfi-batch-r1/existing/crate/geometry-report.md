# crate (P01) geometry report — ART-LF-BATCH-01 subpackage B

- Scope: re-host of the read-only canonical crate-r1 GLB; no geometry,
  scale, material, animation or inventory change. Low-fi candidate,
  not an engineering or gameplay certification.
- Source: <project-root>/art-lowfi-batch-b-20261005/art/source/props/crate-r1/crate-r1.glb
- Source sha256: 07c1b3b65d929873...
- Deliverable GLB sha256: af183991e69c7a6d...
- Blender: 4.5.14 LTS, export via bpy glTF (GLB, apply modifiers, Y-up).
- Geometry: 1 mesh 'Crate', 48 tris, 32 verts, roles ['accent_warm', 'body_light', 'frame_dark'].
- Bounds (glTF m): min [-0.32, 0.0, -0.35] max [0.32, 0.38, 0.35]; footprint .64 x .38 x .70 kept,
  grounded at Y0.
- Reopen/re-export: {'max_float_diff': 0.0, 'problems': [], 'reexport_sha256': 'af183991e69c7a6d595518aba5bdddba847343cdb9002fa9127adcad8441c435', 'deliverable_sha256': 'af183991e69c7a6d595518aba5bdddba847343cdb9002fa9127adcad8441c435', 'identical_bytes': True}
- Roundtrip vs original hand-authored GLB: {'max_float_diff': 0.0, 'problems': []}
- GLB bytes vs re-export: identical_bytes=True (byte layout may differ
  from the original struct-packed file; geometry/indices/floats <=1e-6).
- Checks: {"missing_input": "PASS", "wrong_triangles": "PASS", "unknown_role": "PASS", "non_finite": "PASS"}
- NOT_RUN: Godot engine verification NOT_RUN by this subpackage (master native suite); gameplay/inventory integration; UV/baking/LOD; owner visual acceptance
