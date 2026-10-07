"""Check the frozen art source and real viewport capture record with stdlib only."""
import hashlib
import json
import struct
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
evidence = root / 'docs/art/production/d1-a01-a03/evidence'
config = json.loads((root / 'art/d1/camera.json').read_text())
capture = json.loads((evidence / 'capture.json').read_text())
identity = json.loads((evidence / 'input-output.json').read_text())
assert capture['window_px'] == config['window_px'] == [1920, 1200]
assert capture['viewport_px'] == [1920, 1200]
assert capture['display'] == 'macOS' and capture['driver'] == 'metal'
expected = {'actual-main-v0', *config['cameras'], 'markers-normal', 'markers-overview-low', 'surface-style-0', 'surface-style-1', 'surface-style-2'}
assert set(capture['frames']) == expected
for name in expected:
    data = (evidence / (name + '.png')).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    assert struct.unpack('>II', data[16:24]) == (1920, 1200)
for mode, spec in config['cameras'].items():
    frame = capture['frames'][mode]
    assert frame['projection'] == config['projection']
    assert frame['keep_aspect'] == config['keep_aspect']
    assert abs(frame['near_m'] - config['near_m']) < 1e-5
    assert abs(frame['far_m'] - config['far_m']) < 1e-5
    assert abs(frame['fov'] - spec['fov']) < 1e-5
    assert all(abs(a-b) < 1e-4 for a,b in zip(frame['position'], spec['position']))
for mode in ('normal', 'overview'):
    models = capture['frames'][mode]['models']
    assert len(models) == 7 and all(m['scale'] == [1, 1, 1] for m in models)
    assert not any(m['ui_overlap'] for m in models), mode
    for model in models:
        x, y, w, h = model['projected_aabb_px']
        assert x >= 320 and y >= 56 and x + w <= 1920 and y + h <= 1088
normal = capture['frames']['normal']['models'][:3]
overview = capture['frames']['overview']['models'][:3]
assert all(80 <= m['projected_aabb_px'][2] <= 110 for m in normal)
assert all(35 <= m['projected_aabb_px'][2] <= 55 for m in overview)
low = capture['frames']['markers-overview-low']
assert low['msaa_3d_raw'] == 0 and low['scaling_3d_scale'] == .75
assert len(capture['marker_native_height_checks']) == 5
for marker in capture['marker_native_height_checks']:
    assert marker['vertex_height_error_m'] < 1e-5
    minimum, maximum = marker['triangle_center_gap_m']
    assert .015 <= minimum <= maximum <= .04, marker
for section in ('inputs', 'outputs'):
    for item in identity[section]:
        path = root / item['path']
        if section == 'inputs' and (item['path'].startswith('prototype/') or item['path'] == 'art/d1/verify.py'):
            # R1 captured the old Main; verify its recorded input, not today's player code.
            commit = identity['baseline_commit'] if item['path'].startswith('prototype/') else '81d786b2019b7c73da05d6b3cc2c0488b64e0cc1'
            data = subprocess.check_output(['git', 'show', f"{commit}:{item['path']}"], cwd=root)
        else:
            data = path.read_bytes()
        assert hashlib.sha256(data).hexdigest() == item['sha256'], item['path']
assert 'A03_MARKERS_PASS 138/138' in (evidence / 'marker-check.log').read_text()
log = (evidence / 'capture.log').read_text()
assert 'A01_A03_CAPTURE_OK' in log and 'ERROR:' not in log and 'FAIL' not in log
print('A01_A03_EVIDENCE_PASS: 11 native PNGs, source hashes, scale, UI clearance, native ground gaps, low-quality readback')
