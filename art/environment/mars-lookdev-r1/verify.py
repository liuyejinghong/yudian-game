"""Check lookdev inputs and optional native capture; no game state is assessed."""
import hashlib
import json
import sys
import struct
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
DOCS = REPO / 'docs/art/production/mars-research-r1'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(capture=None):
    references = json.loads((DOCS / 'references/sources.json').read_text())
    assert len(references) == 4
    for row in references:
        path = DOCS / 'references' / row['file']
        assert row['sha256'] == sha(path), path
        assert row['bytes'] == path.stat().st_size
        assert row['credit'] and row['processing'] and row['root_pixel_viewed']
    anchors = json.loads((HERE / 'anchors/sources.json').read_text())
    assert len(anchors) == 3
    for row in anchors:
        assert sha(REPO / row['source']) == row['sha256'] == sha(HERE / 'anchors' / row['file'])
        assert row['scale'] == 1
    assert sha(HERE / 'hero/mars-outcrop-r1.glb') == sha(REPO / 'art/source/environment/mars-outcrop-r1/mars-outcrop-r1.glb')
    assert (HERE / 'ground/mars-ground-r1.blend').exists()
    assert (REPO / 'art/source/environment/mars-outcrop-r1/mars-outcrop-r1.blend').exists()
    if capture:
        folder = Path(capture)
        record = json.loads((folder / 'capture.json').read_text())
        assert record['source_unchanged'] and record['scope'].startswith('independent')
        assert len(record['assets']) == 5
        for row in record['assets']:
            assert row['sha256'] == sha(HERE / row['file'])
            assert row['scale'] == 1 and row['triangles'] > 0
            assert all(size > 0 for size in row['bounds_size_m'])
        for name, digest in record['sources'].items():
            assert digest == sha(HERE / name), name
        blob = (HERE / 'hero/mars-outcrop-r1.glb').read_bytes()
        length = struct.unpack_from('<I', blob, 12)[0]
        gltf = json.loads(blob[20:20+length])
        binary = blob[20+length+8:]
        assert len(gltf['images']) == 3
        for image in gltf['images']:
            view = gltf['bufferViews'][image['bufferView']]
            start = view.get('byteOffset', 0)
            packed = binary[start:start+view['byteLength']]
            assert packed == (HERE / 'hero' / ('mars-outcrop-r1_'+image['name']+'.png')).read_bytes()
        frames = record['frames']
        assert set(frames) == {'normal', 'near', 'reverse', 'horizon', 'normal-low'}
        assert all(abs(a-b) < 1e-4 for a,b in zip(frames['normal']['position_m'],[25.12,28.8,26.3]))
        assert frames['normal']['position_m'] == frames['normal-low']['position_m']
        assert frames['normal']['target_m'] == [4,0,-2.5]
        assert frames['normal']['fov'] == frames['normal-low']['fov'] == 50
        assert frames['normal']['render_scale'] == 1 and abs(frames['normal-low']['render_scale']-.65) < 1e-5
        assert frames['normal']['ssao'] and not frames['normal-low']['ssao']
        assert record['owner_visual_acceptance'] == record['scene_performance'] == 'NOT_RUN'
        for name, row in frames.items():
            assert row['mesh_lod_threshold'] == 0
            image = folder / (name + '.png')
            assert sha(image) == row['png_sha256']
            assert image.read_bytes()[:8] == b'\x89PNG\r\n\x1a\n'
    print('MARS_LOOKDEV_VERIFY_OK references=4 unchanged_anchors=3 hero_copy=true' + (' native_frames=5' if capture else ' capture=NOT_RUN'))


if __name__ == '__main__':
    assert len(sys.argv) <= 2, 'optional capture directory'
    check(sys.argv[1] if len(sys.argv) == 2 else None)
