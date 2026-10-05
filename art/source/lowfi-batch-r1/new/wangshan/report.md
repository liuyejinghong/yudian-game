# wangshan low-fi build report (ART-LF-BATCH-01 subpackage A)

Blender 4.5.14 LTS, meters, +Y up / -Z forward, root at rest, Y0 ground.
Triangles 7544 across 32 meshes; roles {"body_light": 10, "frame_dark": 26, "rubber": 4, "accent_warm": 8}.
Socket_Charge pos [0.48, 0.55, -0.32] fwd [1.0, 0.0, 0.0] up [0.0, 1.0, -0.0]
Socket_Sensor pos [0.0, 1.62, -0.325] fwd [0.0, 0.0, -1.0] up [0.0, 1.0, -0.0]
Socket_Service pos [0.0, 0.75, 0.65] fwd [0.0, 0.0, 1.0] up [0.0, 1.0, -0.0]
Socket_TowFront pos [0.0, 0.24, -0.8] fwd [0.0, 0.0, -1.0] up [0.0, 1.0, -0.0]
Socket_TowRear pos [0.0, 0.24, 0.8] fwd [0.0, 0.0, 1.0] up [0.0, 1.0, -0.0]
clip charge 1.000s nodes 10 keys [2, 2, 2, 2, 2, 2, 2, 2, 2, 2]
clip disabled 1.000s nodes 10 keys [2, 2, 2, 2, 2, 2, 2, 2, 2, 2]
clip maintenance 1.000s nodes 10 keys [2, 2, 2, 2, 2, 2, 2, 2, 2, 2]
clip move 1.000s nodes 10 keys [2, 2, 2, 2, 5, 5, 5, 5, 5, 5]
clip work 2.000s nodes 10 keys [2, 3, 2, 2, 2, 2, 2, 2, 2, 2]
envelope idle min [-0.63, 0.0, -0.8] max [0.63, 1.73, 0.8] within True; hold [-0.63, 0.0, -0.8]..[0.63, 1.73, 0.8] within True
envelope move min [-0.63, 0.0, -0.8] max [0.63, 1.73, 0.8] within True; hold [-0.63, 0.0, -0.8]..[0.63, 1.73, 0.8] within True
envelope work min [-0.63, 0.0, -0.8] max [0.63, 1.73, 0.8] within True; hold [-0.63, 0.0, -0.8]..[0.63, 1.73, 0.8] within True
envelope charge min [-0.63, 0.0, -0.8] max [0.63, 1.73, 0.8] within True; hold [-0.63, 0.0, -0.8]..[0.63, 1.73, 0.8] within True
envelope disabled min [-0.63, 0.0, -0.8] max [0.63, 1.749, 0.8] within True; hold [-0.63, 0.0, -0.8]..[0.63, 1.749, 0.8] within True
envelope maintenance min [-0.63, 0.0, -0.8] max [0.63, 1.73, 0.8] within True; hold [-0.63, 0.0, -0.8]..[0.63, 1.73, 0.8] within True
Checks: {"envelope_within": "PASS", "clip_names": "PASS", "root_not_animated": "PASS", "finite_coords": "PASS", "roles_known": "PASS", "glb_magic": "PASS", "frame0_idle": "PASS"}
NOT_RUN: owner visual acceptance; master preview packaging/manifest; gameplay/performance/navigation; Godot runtime import (separate step)
Sources: U01 tuoyun-r1.glb chassis subtree (read-only); helpers from zhulei-r1 build_sample.py (accepted).
Low-fi candidate only: no aesthetic/performance/gameplay sign-off.
