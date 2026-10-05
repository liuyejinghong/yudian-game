# lander low-fi build report (ART-LF-BATCH-01 subpackage A)

Blender 4.5.14 LTS, meters, +Y up / -Z forward, root at rest, Y0 ground.
Triangles 2444 across 34 meshes; roles {"frame_dark": 24, "body_light": 6, "accent_warm": 6}.
Socket_Cargo pos [0.0, 0.86, -0.83] fwd [0.0, 0.0, -1.0] up [0.0, 1.0, -0.0]
Socket_Handling pos [0.0, 0.7, 1.0] fwd [0.0, 0.0, 1.0] up [0.0, 1.0, -0.0]
Socket_Service pos [0.0, 1.3, 1.1125] fwd [0.0, 0.0, 1.0] up [0.0, 1.0, -0.0]
clip disabled 1.000s nodes 3 keys [2, 2, 2]
clip maintenance 1.000s nodes 3 keys [2, 2, 2]
clip work 1.000s nodes 3 keys [2, 2, 2]
envelope idle min [-1.6, 0.0, -1.6] max [1.6, 2.375, 1.6] within True; hold [-1.6, 0.0, -1.6]..[1.6, 2.375, 1.6] within True
envelope work min [-1.6, 0.0, -1.945] max [1.6, 2.375, 1.6] within True; hold [-1.6, 0.0, -1.945]..[1.6, 2.375, 1.6] within True
envelope disabled min [-1.6, 0.0, -1.6] max [1.6, 2.59, 1.6] within True; hold [-1.6, 0.0, -1.6]..[1.6, 2.59, 1.6] within True
envelope maintenance min [-1.6, 0.0, -1.6] max [1.6, 2.375, 1.6] within True; hold [-1.6, 0.0, -1.6]..[1.6, 2.375, 1.6] within True
Checks: {"envelope_within": "PASS", "clip_names": "PASS", "root_not_animated": "PASS", "finite_coords": "PASS", "roles_known": "PASS", "glb_magic": "PASS", "frame0_idle": "PASS"}
NOT_RUN: owner visual acceptance; master preview packaging/manifest; gameplay/performance/navigation; Godot runtime import (separate step)
Sources: P01 crate-r1.glb payload crate (read-only); helpers from zhulei-r1 build_sample.py (accepted).
Low-fi candidate only: no aesthetic/performance/gameplay sign-off.
