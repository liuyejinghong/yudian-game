"""Check frozen terrain-look evidence and current deliverable identity."""
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / 'docs/art/production/terrain-look-r1/evidence'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    identity = json.loads((EVIDENCE / 'identity.json').read_text())
    for path, digest in identity['files_sha256'].items():
        assert sha(ROOT / path) == digest, path
    shader = ROOT / 'art/d1/surface.gdshader'
    assert shader.read_bytes() == (ROOT / 'prototype/assets/d1-art/surface.gdshader').read_bytes()
    assert 'VERTEX =' not in shader.read_text() and 'NORMAL =' not in shader.read_text()
    for name, expected_stage in [('natural', 0), ('working', 1), ('committed', 0)]:
        directory = EVIDENCE / name
        data = json.loads((directory / 'capture.json').read_text())
        assert data['driver'] == 'metal' and data['viewport_px'] == [1920, 1200]
        assert data['terrain_unchanged'] and data['mask_unchanged'] and data['mask_enabled']
        assert data['stage'] == expected_stage
        for view in ['normal', 'close', 'low']:
            before, after = (data['frames'][view + side] for side in ['-before', '-after'])
            assert {k: v for k, v in before.items() if k != 'shader_sha256'} == {
                k: v for k, v in after.items() if k != 'shader_sha256'}, (name, view)
            assert after['shader_sha256'] == sha(shader)
        for png in directory.glob('*.png'):
            raw = png.read_bytes()
            assert raw[:8] == b'\x89PNG\r\n\x1a\n'
            assert struct.unpack('>II', raw[16:24]) == (1920, 1200), png
        log = (directory / 'render.log').read_text()
        assert 'ERROR' not in log and 'TERRAIN_LOOK_CAPTURE_OK' in log
        assert identity['loaded_dll_sha256'] in log and identity['loaded_mvid'] in log
        if name != 'natural':
            assert 'PLAYER_LOAD_READY' in log
    for folder, filename, count in [('mask', 'mask-check.json', 14), ('slope', 'surface-check.json', 6)]:
        check = json.loads((EVIDENCE / folder / filename).read_text())
        assert check['checks'] == count and check['failures'] == 0 and check['driver'] == 'metal'
        assert 'ERROR' not in (EVIDENCE / folder / 'render.log').read_text()
    manifest = json.loads((ROOT / 'art/environment/terrain-look-r1/rocks/manifest.json').read_text())
    assert manifest['generator_sha256'] == sha(ROOT / 'art/environment/terrain-look-r1/rocks/generate_rocks.py')
    assert not any(manifest['semantics'].values())
    for key, asset in manifest['assets'].items():
        for filename, facts in asset['files'].items():
            assert facts['sha256'] == sha(ROOT / 'art/environment/terrain-look-r1/rocks' / filename)
        assert not asset['reopen_export_check']['problems'] and asset['reopen_export_check']['max_float_diff'] == 0
    for rock in json.loads((EVIDENCE / 'natural/capture.json').read_text())['rocks']:
        assert rock['sha256'] == manifest['assets'][rock['id']]['files'][rock['id'] + '.glb']['sha256']
        assert rock['base_samples'] > 0 and rock['base_gap_max_m'] <= 0
        assert rock['base_gap_min_m'] > -0.75
    assert 'ROCK_CHECK rock-cluster' in (EVIDENCE / 'blender-check.log').read_text()
    assert 'ROCK_CHECK rock-outcrop' in (EVIDENCE / 'blender-check.log').read_text()
    print('TERRAIN_LOOK_VERIFIED: 20 Main PNG, unchanged terrain/masks, 14 mask + 6 slope checks, 2 Blender sources')


if __name__ == '__main__':
    main()
