# zhulei (U02) geometry+clips report — ART-LF-BATCH-01 subpackage B

- Scope: legacy zhulei-r1 blend re-host; all six wheels, arm, feet,
  rear bay and hinge mounts kept verbatim. New ChargePivot at the
  ChargeCap top edge (glTF -.484,.61,.34), cap kept at world pose as
  its child, solid hinge mount+axle to ChargeHousing. Low-fi
  candidate; not an engineering load or gameplay certification.
- Source: <project-root>/art-lowfi-batch-b-20261005/art/source/units/zhulei-r1/zhulei-r1.blend sha 6d985fe2db16e318...
- Blender: 4.5.14 LTS; fps 30; linear keys; NLA_TRACKS export, sampled.
- Clips (real Blender actions, root static, movers baselined):
  - move: 1.0s, 6 channels; envelope glTF min [-0.87, 2.4e-05, -0.810649] max [0.87, 1.103437, 0.8]
  - work: 2.0s, 4 channels; envelope glTF min [-0.87, 0.0, -0.810649] max [0.87, 1.103437, 0.8]
  - charge: 1.0s, 1 channels; envelope glTF min [-0.87, 2.4e-05, -0.810649] max [0.87, 1.103437, 0.8]
  - disabled: 1.0s, 1 channels; envelope glTF min [-0.87, 2.4e-05, -0.810649] max [0.87, 1.103437, 0.8]
  - maintenance: 1.0s, 1 channels; envelope glTF min [-0.87, 2.4e-05, -0.810649] max [0.87, 1.103437, 0.8]
- move wheels roll about local X, one turn/s forward (+glTF -Z);
- work: shoulder 55>-20>55, elbow -155>5>-155, feet .30>.03>.30;
- charge lid 0>-60 glTF Z (Blender Y +60), disabled shoulder 55>85,
  maintenance rear hood 0>70; towed = disabled freeze + packaging rod.
- Sockets frozen (glTF): {"Socket_Charge": {"position": [-0.48, 0.55, 0.34], "forward": [-1.0, 0.0, 0.0], "up": [0.0, 1.0, -0.0]}, "Socket_Service": {"position": [0.0, 0.69, 0.695], "forward": [0.0, 0.0, 1.0], "up": [0.0, 1.0, -0.0]}, "Socket_TowFront": {"position": [0.0, 0.24, -0.8], "forward": [0.0, 0.0, -1.0], "up": [0.0, 1.0, -0.0]}, "Socket_TowRear": {"position": [0.0, 0.24, 0.8], "forward": [0.0, 0.0, 1.0], "up": [0.0, 1.0, -0.0]}, "Socket_Work": {"parent": "Wrist", "position": [0.0, 0.478084, -0.639545]}}
- Reopen/re-export vs deliverable: max_float_diff=0.0 identical=True
- Delivered-GLB reimport sample check: {"move_wheel_quarter_turn_deg_error": 0.0, "work_shoulder_deg_error": 0.0, "work_slide_y": 0.03, "charge_lid_deg_error": 0.0, "disabled_shoulder_deg_error": 0.0, "maintenance_hood_deg_error": 0.0, "root_static_no_channels": true, "baseline_via_rest_or_channel": true, "animated_nodes_per_clip": {"charge": ["ChargePivot"], "maintenance": ["HoodLid"], "work": ["Elbow", "Shoulder", "SupportSlideL", "SupportSlideR"], "disabled": ["Shoulder"], "move": ["Wheel_LF", "Wheel_LM", "Wheel_LR", "Wheel_RF", "Wheel_RM", "Wheel_RR"]}}
- NOT_RUN: towed clip (packaging freeze per ticket); charge/dock gameplay; Godot engine verification NOT_RUN by this subpackage (master native suite); LOD/UV/baking; owner visual acceptance
