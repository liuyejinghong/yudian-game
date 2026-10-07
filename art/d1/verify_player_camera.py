"""Verify the recorded actual-player camera comparison and its source identities."""
import hashlib
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[2]
folder = root / 'docs/art/production/d1-a01-a03/evidence/player-camera'
record = json.loads((folder / 'player-camera.json').read_text())
config = json.loads((root / 'art/d1/camera-player.json').read_text())
identity = json.loads((folder / 'input-output.json').read_text())
assert record['driver'] == 'metal' and record['window_px'] == [1920, 1200]
assert record['hud_rects_px'] == [[16, 16, 298, 193], [745, 1125, 430, 55]]
chosen = record['frames']['normal-distance-1.6']
assert len(chosen['objects']) == 10
assert sum(o['id'].startswith('Facility_') for o in chosen['objects']) == 6
assert sum(o['id'].startswith('Robot_Zhulei_') for o in chosen['objects']) == 4
assert not any(o['clipped'] or o['hud_overlap'] for o in chosen['objects'])
assert sum(o['clipped'] for o in record['frames']['actual-normal']['objects']) == 4
camera = config['cameras']['normal']
assert all(abs(a-b) < 1e-4 for a,b in zip(chosen['position'],camera['position']))
assert chosen['fov'] == camera['fov'] == 50
assert abs(math.dist(chosen['position'],camera['target']) - config['distance_m']) < 1e-4
for o in chosen['objects']:
    if o['id'].startswith('Robot_Zhulei_'):
        assert 50 <= o['projected_bounds_px'][2] <= 70
for group in ('inputs', 'outputs'):
    for item in identity[group]:
        assert hashlib.sha256((root / item['path']).read_bytes()).hexdigest() == item['sha256'], item['path']
print('A01_PLAYER_CAMERA_EVIDENCE_PASS: 6 facilities + 4 Zhulei, full HUD, no clipping, recorded source/output identities')
