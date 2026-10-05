# recovery (P01 link candidate) geometry report — ART-LF-BATCH-01 B

- Scope: static tow-link visual candidate, original geometry per
  ticket; NOT a rescue system, no pin holes, load or who-tows-whom
  rule. Visual candidate only, comparable with the .35 m U01 tow
  stub; acceptance is the owner's, not self-signed.
- Geometry: LinkBar (frame_dark .06 section, .35 along glTF Z),
  LugEndA/LugEndB (accent_warm .20x.08x.05 at z +-0.15).
- Sockets: EndA (0,0,-.175) fwd -Z, EndB (0,0,+.175) fwd +Z, up +Y;
  survey {"Socket_EndA": {"position": [0.0, 0.0, -0.175], "forward": [0.0, 0.0, -1.0], "up": [0.0, 1.0, -0.0]}, "Socket_EndB": {"position": [0.0, 0.0, 0.175], "forward": [0.0, 0.0, 1.0], "up": [0.0, 1.0, -0.0]}}.
- Envelope (glTF m): min [-0.1, -0.04, -0.175] max [0.1, 0.04, 0.175], candidate matched.
- Triangles: 132 across 3 meshes, roles ['accent_warm', 'frame_dark'].
- Blender: 4.5.14 LTS; static asset, no clips, no animation channels.
- Reopen/re-export: max_float_diff=0.0, identical_bytes=True.
- Checks: {"empty_output": "PASS", "unknown_role": "PASS", "non_finite": "PASS", "missing_lugs": "PASS"}
- NOT_RUN: towed/recovery gameplay; Godot engine verification NOT_RUN by this subpackage (master native suite); UV/baking/LOD; owner visual acceptance
