# repair low-fi build report (ART-LF-BATCH-01 subpackage A)

Blender 4.5.14 LTS, meters, +Y up / -Z forward, root at rest, Y0 ground.
Triangles 1348 across 27 meshes; roles {"accent_warm": 5, "body_light": 6, "frame_dark": 16}.
Socket_Bay pos [0.0, 0.04, -0.0] fwd [0.0, 0.0, -1.0] up [0.0, 1.0, -0.0]
Socket_PowerIn pos [-1.6, 0.18, 1.255] fwd [0.0, 0.0, 1.0] up [0.0, 1.0, -0.0]
Socket_Service pos [-1.385, 0.9, 0.9] fwd [1.0, 0.0, 0.0] up [0.0, 1.0, -0.0]
clip disabled 1.000s nodes 3 keys [2, 2, 2]
clip maintenance 1.000s nodes 3 keys [2, 2, 2]
clip work 2.000s nodes 3 keys [2, 2, 3, 2]
envelope idle min [-1.85, 0.0, -1.86] max [1.7, 2.31, 1.6] within True; hold [-1.85, 0.0, -1.86]..[1.7, 2.31, 1.6] within True
envelope work min [-1.85, 0.0, -1.86] max [1.7, 2.31, 1.6] within True; hold [-1.85, 0.0, -1.86]..[1.7, 2.31, 1.6] within True
envelope disabled min [-1.85, 0.0, -1.86] max [1.7, 2.31, 1.6] within True; hold [-1.85, 0.0, -1.6]..[1.7, 2.31, 1.6] within True
envelope maintenance min [-1.85, 0.0, -1.86] max [1.7, 2.31, 1.6] within True; hold [-1.85, 0.0, -1.86]..[1.7, 2.31, 1.6] within True
Checks: {"envelope_within": "PASS", "clip_names": "PASS", "root_not_animated": "PASS", "finite_coords": "PASS", "roles_known": "PASS", "glb_magic": "PASS", "frame0_idle": "PASS"}
NOT_RUN: owner visual acceptance; master preview packaging/manifest; gameplay/performance/navigation; Godot runtime import (separate step)
Sources: original geometry; helpers from zhulei-r1 build_sample.py (accepted).
Low-fi candidate only: no aesthetic/performance/gameplay sign-off.
