"""Check lookdev inputs and optional native capture; no game state is assessed."""
import hashlib
import json
import struct
import argparse
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
DOCS = REPO / 'docs/art/production/mars-research-r1'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(capture=None, outcrop_candidate=None):
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
        hifi = record.get('candidate') == 'hifi-r2'
        assert not outcrop_candidate or hifi, 'candidate override requires a hifi capture'
        assert record['source_unchanged'] and record['scope'].startswith('independent')
        assert len(record['assets']) == (6 if hifi else 5)
        if hifi:
            for name in ('mars-outcrop-hifi-r2', 'mars-ground-patch-hifi-r2'):
                source = REPO / 'art/source/environment' / name / (name + '.glb')
                assert source.with_suffix('.blend').exists()
                expected = Path(outcrop_candidate) if outcrop_candidate and name == 'mars-outcrop-hifi-r2' else source
                assert sha(expected) == sha(HERE / 'hifi' / (name + '.glb'))
        for row in record['assets']:
            assert row['sha256'] == sha(HERE / row['file'])
            assert row['scale'] == 1 and row['triangles'] > 0
            assert all(size > 0 for size in row['bounds_size_m'])
        for name, digest in record['sources'].items():
            assert digest == sha(HERE / name), name
        assets = [HERE / 'hifi' / (name+'.glb') for name in ('mars-outcrop-hifi-r2', 'mars-ground-patch-hifi-r2')] if hifi else [HERE / 'hero/mars-outcrop-r1.glb']
        for asset in assets:
            blob = asset.read_bytes()
            length = struct.unpack_from('<I', blob, 12)[0]
            gltf = json.loads(blob[20:20+length])
            binary = blob[20+length+8:]
            assert gltf.get('images')
            for texture in gltf['images']:
                view = gltf['bufferViews'][texture['bufferView']]
                start = view.get('byteOffset', 0)
                packed = binary[start:start+view['byteLength']]
                extracted = asset.parent / (asset.stem+'_'+texture['name']+'.png')
                assert packed == extracted.read_bytes(), extracted
        frames = record['frames']
        expected = {'normal', 'near', 'reverse', 'horizon', 'normal-low'}
        if hifi:
            expected |= {'detail', 'ground'}
        assert set(frames) == expected
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
    print('MARS_LOOKDEV_VERIFY_OK references=4 unchanged_anchors=3 hero_copy=true' + (f' native_frames={len(frames)} outcrop_candidate={bool(outcrop_candidate)}' if capture else ' capture=NOT_RUN'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', nargs='?')
    parser.add_argument('--outcrop-candidate', help='explicit preview GLB; formal-source check remains the default')
    args = parser.parse_args()
    assert not args.outcrop_candidate or args.capture, 'candidate override requires a capture'
    check(args.capture, args.outcrop_candidate)
