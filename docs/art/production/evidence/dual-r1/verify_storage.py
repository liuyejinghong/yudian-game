"""Check imported world positions and capture provenance, independently of exporter math."""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parent

def run():
    facts = json.loads((ROOT / 'art/source/facilities/storage-r1/geometry-facts.json').read_text())
    audit = json.loads((HERE / 'storage-r3-audit.json').read_text())
    images = HERE / 'images-crate-storage-r3'
    captures = [r for r in json.loads((images / 'captures.json').read_text()) if r['sample'] == 'storage']
    digest = hashlib.sha256((ROOT / 'art/source/facilities/storage-r1/storage-r1.glb').read_bytes()).hexdigest()
    assert digest == facts['sha256']['glb'] == facts['sha256']['glb_prototype']
    assert hashlib.sha256((ROOT / 'prototype/assets/facilities/storage-r1/storage-r1.glb').read_bytes()).hexdigest() == digest
    assert audit['triangles'] == facts['triangles_total'] and len(audit['meshes']) == 16
    assert sum(len(m['surfaces']) for m in audit['meshes']) == 52 and not audit['sockets']
    close = lambda a, b: all(abs(x-y) <= 1e-6 for x,y in zip(a,b))
    assert close(audit['bounds_including_hidden']['min'], [-2.25,0,-1.6])
    assert close(audit['bounds_including_hidden']['max'], [2.25,2.2,1.6])
    crates = [m for m in audit['meshes'] if '/RackPayloadDemo/' in m['path']]
    assert len(crates) == 12
    centers = set()
    for slot in facts['payload_slots']:
        matches = [m for m in crates if m['path'].endswith('/'+slot['node'])]
        assert len(matches) == 1
        m = matches[0]; box = m['actual_vertices_bounds']
        x,y,z = slot['x'],slot['bottom_y'],slot['z']
        assert close(box['min'], [x-.32,y,z-.35]) and close(box['max'], [x+.32,y+.38,z+.35])
        assert [s['material'] for s in m['surfaces']] == ['frame_dark','body_light','frame_dark','accent_warm']
        centers.add(tuple(round((lo+hi)/2,5) for lo,hi in zip(box['min'],box['max'])))
    assert centers == {(x,y+.19,z) for x in [-1.35,0,1.35] for y in [.2,1.1] for z in [-.7,.6]}
    assert len(captures) == len(list(images.glob('storage_*.png'))) == 8
    assert {(r['camera'],r['yaw'],r['loaded_demo']) for r in captures} == {(c,y,l) for c in ['normal','close'] for y in [0,90] for l in [False,True]}
    hashes = {}
    for record in captures:
        assert record['source_sha256'] == digest and len(record['bindings']) == 52
        hashes[record['file']] = hashlib.sha256((images / record['file']).read_bytes()).hexdigest()
    crate_path = ROOT / 'art/source/props/crate-r1/crate-r1.glb'
    raw = crate_path.read_bytes(); length = struct.unpack_from('<I',raw,12)[0]
    doc = json.loads(raw[20:20+length]); blob = raw[28+length:]
    def top(primitive):
        a = doc['accessors'][primitive['attributes']['POSITION']]
        v = doc['bufferViews'][a['bufferView']]
        offset = v.get('byteOffset',0)+a.get('byteOffset',0)
        return max(struct.unpack_from('<fff',blob,offset+12*i)[1] for i in range(a['count']))
    parts = doc['meshes'][0]['primitives']
    assert abs(top(parts[1])-.32) < 1e-6 and abs(top(parts[2])-.38) < 1e-6, 'body top must stay below lid top'
    crate_sha = hashlib.sha256(raw).hexdigest()
    crate_records = [r for r in json.loads((images / 'captures.json').read_text()) if r['sample']=='crate']
    assert len(crate_records)==4 and all(r['source_sha256']==crate_sha for r in crate_records)
    crate_audit = json.loads((HERE / 'crate-r2-audit.json').read_text())
    assert crate_audit['triangles']==48 and close(crate_audit['bounds_including_hidden']['min'],[-.32,0,-.35]) and close(crate_audit['bounds_including_hidden']['max'],[.32,.38,.35])
    for record in crate_records:
        hashes[record['file']] = hashlib.sha256((images / record['file']).read_bytes()).hexdigest()
    result = {'source_sha256':digest,'triangles':audit['triangles'],'distinct_payload_world_positions':12,'surfaces':52,'images':8,'image_sha256':hashes,'visual_acceptance':'primary inspection recorded in README, owner NOT_RUN'}
    (HERE / 'storage-r3-verification.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS storage world positions, envelope, roles and 8 actual capture identities')

if __name__ == '__main__':
    run()
