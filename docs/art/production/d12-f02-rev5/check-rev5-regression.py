"""Recheck the accepted sample, then bind all twelve regression frames."""
from pathlib import Path
import hashlib
import json
import runpy

HERE = Path(__file__).resolve().parent
sample = runpy.run_path(str(HERE / 'check-rev5-sample.py'))
ROOT = sample['ROOT']
frames = list((HERE / 'godot-rev5-regression').glob('*/blind_0001.json'))
assert len(frames) == 12
for path in frames:
    record = json.loads(path.read_text())
    for name, facts in json.loads((path.parent / 'capture_hashes.json').read_text())['files'].items():
        raw = (path.parent / name).read_bytes()
        assert len(raw) == facts['bytes'] and hashlib.sha256(raw).hexdigest() == facts['sha256']
    camera, yaw, state, time = path.parent.name.removesuffix('-low').split('-')
    low = path.parent.name.endswith('-low')
    assert record['requested']['state'] == state and record['requested']['time_s'] == float(time)
    assert abs(record['roots']['entity_rotation_y_deg'] - int(yaw)) < 1e-4
    assert record['hashes']['glb']['actual_sha256'] == sample['expected']
    for facts in record['hashes']['source']['files']:
        assert facts['match'] and hashlib.sha256((ROOT / facts['path']).read_bytes()).hexdigest() == facts['actual_sha256']
    view = record['view']
    wanted = [21.12, 28.8, 28.8] if camera == 'normal' else [3., 3., 4.]
    assert max(abs(a - b) for a, b in zip(view['camera']['position'], wanted)) < 1e-4
    assert view['camera']['fov'] == 50
    assert view['viewport']['scaling_3d_scale'] == (.75 if low else 1)
    assert view['viewport']['msaa_3d_raw'] == (0 if low else 2)
print('PASS: 12 regression source/PNG/JSON/actual yaw/state/time/camera/quality records; aesthetic/Main not signed')
