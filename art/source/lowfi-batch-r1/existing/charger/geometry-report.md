# charger (F04) geometry+clips report — ART-LF-BATCH-01 subpackage B

- Scope: legacy charger-r1 re-host; all legacy parts, Dock/PowerIn/
  socket and the .17 m ContactHead gap untouched (no insertion). New:
  StatusPaddle on CabinetCap (idle flat X-90; work X 0/+15/0/-15/0
  over 2 s per ticket update) on a solid axle+mount; ground StopGate
  (idle flat outward X-90, disabled upright X0) with axle + two
  foundation mounts; legacy ServicePanel re-hosted on a real top
  hinge with visible mount, maintenance glTF Z+70 toward +X.
- Source: <project-root>/art-lowfi-batch-b-20261005/art/source/facilities/charger-r1/charger-r1.glb sha 84c4849bda02625c...
- Blender: 4.5.14 LTS; fps 30; linear keys; NLA_TRACKS export, sampled.
  - work: 2.0s; envelope glTF min [-0.9, 0.0, -1.34] max [1.3, 1.71, 1.1]
  - disabled: 1.0s; envelope glTF min [-0.9, 0.0, -1.34] max [1.3, 1.49, 1.1]
  - maintenance: 1.0s; envelope glTF min [-0.9, 0.0, -1.34] max [1.820894, 1.49, 1.1]
- idle envelope glTF min [-0.9, 0.0, -1.34] max [1.3, 1.49, 1.1] (candidate x -0.9..1.85,
  y 0..1.74, z -1.40..1.10; asserted for idle and every clip).
- Sockets kept as imported (glTF): {"Socket_Dock": {"position": [0.65, 0.57, -0.35], "forward": [-1.0, 0.0, 0.0], "up": [0.0, 1.0, -0.0]}, "Socket_PowerIn": {"position": [1.055, 0.18, -0.66], "forward": [0.0, 0.0, -1.0], "up": [0.0, 1.0, -0.0]}}
- move/charge/towed N/A fixed power facility; robot charge state is
  expressed by the vehicle side, not this asset.
- Reopen/re-export vs deliverable: max_float_diff=0.0 identical=True
- Delivered-GLB sample check (bytes, ticket poses): {"work_paddle_0_0_deg_error": 0.0, "work_paddle_0_5_deg_error": 0.0, "work_paddle_1_0_deg_error": 0.0, "work_paddle_1_5_deg_error": 0.0, "work_paddle_2_0_deg_error": 0.0, "disabled_gate_upright_deg_error": 0.0, "maintenance_panel_deg_error": 0.0, "root_and_legacy_static": true, "animated_nodes_per_clip": {"maintenance": ["ServicePanelPivot"], "work": ["StatusPaddlePivot"], "disabled": ["StopGatePivot"]}}
- Clip envelopes measured by posing the rest model at key poses
  (measurement only; clip proof is the GLB sample check + Godot).
- NOT_RUN: charging gameplay/power transfer; Godot engine verification NOT_RUN by this subpackage (master native suite); LOD/UV/baking; owner visual acceptance
