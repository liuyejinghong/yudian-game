"""Bind the seven static sample frames to their source, pose and camera."""
from pathlib import Path
import hashlib
import json
import struct

ROOT = Path(__file__).resolve().parents[4]
directory = ROOT / 'docs/art/production/d12-f02-rev5/godot-rev5-sample'
expected = 'b439ab57f4c23fc9868fcb8c0d313ebbdb2956942f1ff3eac7f74080a5bd51e2'

def glb(path):
    raw = path.read_bytes()
    length = struct.unpack_from('<I', raw, 12)[0]
    return json.loads(raw[20:20 + length]), raw[28 + length:]

def accessor(doc, raw, index):
    item = doc['accessors'][index]
    view = doc['bufferViews'][item['bufferView']]
    size = {5126:4, 5125:4, 5123:2}[item['componentType']] * {'SCALAR':1, 'VEC3':3, 'VEC4':4}[item['type']]
    start = view.get('byteOffset', 0) + item.get('byteOffset', 0)
    stride = view.get('byteStride', size)
    return (item['componentType'], item['type'], item['count'],
            b''.join(raw[start + i * stride:start + i * stride + size] for i in range(item['count'])))

assert hashlib.sha256((ROOT / 'art/source/facilities/processor-d12-rev5-sample/processor-d12-rev5-sample.glb').read_bytes()).hexdigest() == expected
before, old_raw = glb(ROOT / 'docs/art/production/d12-f02-rev5/before-rev4/processor-r1.glb')
after, new_raw = glb(ROOT / 'art/source/facilities/processor-d12-rev5-sample/processor-d12-rev5-sample.glb')
assert before['nodes'] == after['nodes'] and before['materials'] == after['materials']
assert len(before['meshes']) == len(after['meshes']) == 17
for old, new in zip(before['meshes'], after['meshes']):
    assert old['name'] == new['name']
    if old['name'] in ['PressRam', 'PressGuide']:
        continue
    assert len(old['primitives']) == len(new['primitives'])
    for a, b in zip(old['primitives'], new['primitives']):
        assert a['material'] == b['material'] and a['attributes'].keys() == b['attributes'].keys()
        for key in a['attributes']:
            assert accessor(before, old_raw, a['attributes'][key]) == accessor(after, new_raw, b['attributes'][key])
        assert accessor(before, old_raw, a['indices']) == accessor(after, new_raw, b['indices'])
for old, new in zip(before['animations'], after['animations']):
    assert old['name'] == new['name'] and old['channels'] == new['channels']
    assert len(old['samplers']) == len(new['samplers'])
    for a, b in zip(old['samplers'], new['samplers']):
        assert a.get('interpolation') == b.get('interpolation')
        for key in ['input', 'output']:
            assert accessor(before, old_raw, a[key]) == accessor(after, new_raw, b[key])
assert len(before['animations']) == len(after['animations']) == 3
frames = list(directory.glob('*/blind_0001.json'))
assert len(frames) == 7
for path in frames:
    record = json.loads(path.read_text())
    hashes = json.loads((path.parent / 'capture_hashes.json').read_text())
    for name, facts in hashes['files'].items():
        raw = (path.parent / name).read_bytes()
        assert len(raw) == facts['bytes'] and hashlib.sha256(raw).hexdigest() == facts['sha256']
    assert record['hashes']['glb']['actual_sha256'] == expected
    assert abs(record['roots']['entity_rotation_y_deg'] - 90) < 1e-4
    assert record['requested']['state'] == 'work' and record['requested']['time_s'] in [0, .5, 1.5]
    assert record['applied']['mode'] == 'animated'
    for facts in record['hashes']['source']['files']:
        assert facts['match']
        assert hashlib.sha256((ROOT / facts['path']).read_bytes()).hexdigest() == facts['actual_sha256']
print('PASS: nodes/materials/15 meshes/all animation keys unchanged; 7 source-bound frames, yaw90/work; aesthetic/Main not signed')
