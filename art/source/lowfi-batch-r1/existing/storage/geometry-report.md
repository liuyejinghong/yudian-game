# storage (F03) geometry+clips report — ART-LF-BATCH-01 subpackage B

- Scope: legacy storage-r1 re-host (full geometry + 12 crates,
  RackPayloadDemo untouched for packaging visibility); new front Gate
  folding X+90 FORWARD (open, reaches glTF z -2.96, in front of the
  front crate row; closed X0) with solid GateAxle (r.04 x 4.32) to
  the front posts, and enclosed rear ServicePod (bracket to the rear
  foundation) with top-hinged ServiceCover X-70 on a real axle plus
  two mount blocks. Low-fi candidate, not capacity or mechanism claim.
- Source: <project-root>/art-lowfi-batch-b-20261005/art/source/facilities/storage-r1/storage-r1.glb sha dcaac7f78d9d11f2...
- Blender: 4.5.14 LTS; fps 30; linear keys; NLA_TRACKS export, sampled.
  - disabled: 1.0s; envelope glTF min [-2.25, 0.0, -2.96] max [2.25, 2.2, 1.745]
  - maintenance: 1.0s; envelope glTF min [-2.25, 0.0, -2.96] max [2.25, 2.2, 2.29292]
- idle envelope glTF min [-2.25, 0.0, -2.96] max [2.25, 2.2, 1.745] (candidate x +-2.25,
  y 0..2.20, z -3.02..2.35; maintenance cover reach +2.01 inside).
- Sockets frozen (glTF): {"Socket_Input": {"position": [0.0, 0.2, -1.6], "forward": [0.0, 0.0, -1.0], "up": [0.0, 1.0, -0.0]}, "Socket_Output": {"position": [0.0, 0.2, 1.78], "forward": [0.0, 0.0, 1.0], "up": [0.0, 1.0, -0.0]}, "Socket_Service": {"position": [0.0, 0.85, 1.7425], "forward": [0.0, 0.0, 1.0], "up": [0.0, 1.0, -0.0]}}
- Move/charge/towed N/A fixed facility; work stays static take/put
  handoff; no crate auto motion, no inventory promises.
- Reopen/re-export vs deliverable: max_float_diff=1.1920928955078125e-07 identical=False
- Delivered-GLB sample check (bytes, ticket poses): {"disabled_gate_open_deg_error": 0.0, "disabled_gate_closed_deg_error": 0.0, "maintenance_cover_deg_error": 0.0, "root_and_crates_static": true, "animated_nodes_per_clip": {"maintenance": ["CoverPivot"], "disabled": ["GatePivot"]}}
- Clip envelopes measured by posing the rest model at key poses
  (measurement only; clip proof is the GLB sample check + Godot).
- NOT_RUN: take/put gameplay; Godot engine verification NOT_RUN by this subpackage (master native suite); LOD/UV/baking; owner visual acceptance
