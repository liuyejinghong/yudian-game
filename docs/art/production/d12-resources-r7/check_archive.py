"""Verify published evidence and canonical asset identity, without a temporary fixture.

Run from any directory: python3 docs/art/production/d12-resources-r7/check_archive.py
This is an archive/geometry check, not a new render or visual acceptance.
"""
from pathlib import Path
import hashlib
import json
import math
import struct

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
IDS = {'iron_ore', 'copper_ore', 'iron', 'copper', 'parts', 'cable'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path):
    return json.loads(path.read_text())


def mesh_data(path, mesh_name=None, material_name=None):
    raw = path.read_bytes()
    assert struct.unpack_from('<III', raw) == (0x46546c67, 2, len(raw)), path
    length, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4e4f534a
    doc = json.loads(raw[20:20 + length])
    bin_start = 28 + length
    points, triangles = [], 0
    for mesh in doc['meshes']:
        if mesh_name and mesh['name'] != mesh_name:
            continue
        for primitive in mesh['primitives']:
            if material_name and doc['materials'][primitive['material']]['name'] != material_name:
                continue
            assert primitive.get('mode', 4) == 4
            acc = doc['accessors'][primitive['attributes']['POSITION']]
            assert acc['componentType'] == 5126 and acc['type'] == 'VEC3'
            view = doc['bufferViews'][acc['bufferView']]
            offset = bin_start + view.get('byteOffset', 0) + acc.get('byteOffset', 0)
            points.extend(struct.unpack_from('<3f', raw, offset + i * view.get('byteStride', 12))
                          for i in range(acc['count']))
            triangles += doc['accessors'][primitive['indices']]['count'] // 3
    assert points
    return doc, points, triangles


def bounds(points):
    return [[min(p[i] for p in points) for i in range(3)],
            [max(p[i] for p in points) for i in range(3)]]


def close(a, b):
    return max(abs(x - y) for row_a, row_b in zip(a, b) for x, y in zip(row_a, row_b)) < 1e-6


archive = load(HERE / 'archive-map.json')
by_archive = {row['archive']: row for row in archive['files']}
for row in archive['files']:
    path = ROOT / row['archive']
    assert sha(path) == row['archive_sha256'], path
    assert path.stat().st_size == row['archive_bytes'], path
    if path.suffix in {'.png', '.glb'}:
        assert row['original_sha256'] == row['archive_sha256']

capture = load(HERE / 'godot-r7/capture.json')
bindings = load(HERE / 'bindings.json')
identity = load(HERE / 'independent/identity.json')
assert identity['verdict'] == 'PASS_LIMITED_STATIC_GRAY_AND_KNOWN_MAPPING'
assert sha(HERE / 'godot-r7/capture.json') == identity['capture_sha256']
assert len(capture['cases']) == 57
assert set(capture['cases']) == {p.name for p in (HERE / 'godot-r7').glob('*.png')}
for name, case in capture['cases'].items():
    path = HERE / 'godot-r7' / name
    assert sha(path) == case['sha256'], name
    assert struct.unpack('>II', path.read_bytes()[16:24]) == (1920, 1200), name
    context = case['context']
    assert set(context.get('items', {})) <= IDS
    active = {id for id, count in context.get('items', {}).items() if count > 0}
    glyphs = set(case['identifier_ids'])
    assert glyphs == (active if name.startswith('cargo-') and ('normal' in name) else set()), name
    assert case['class_identifiers'] == len(glyphs), name
    if 'empty' in name:
        assert not active and context['goods_count'] == context['carrier_count'] == 0
    if name.startswith('cargo-') and active:
        assert context['goods_count'] == 4 and context['carrier_count'] == 1
        assert context['carrier_mount']['local_transform_identity']
        assert context['carrier_mount']['scale'] == 1 and context['scale'] == .65
        assert abs(context['carrier_mount']['deck_top_y'] - .72) < 1e-6
for source, relative in bindings['capture_sources_to_current_files'].items():
    assert sha(ROOT / relative) == capture['sources'][source], source
for relative, expected in bindings['additional_current_files'].items():
    assert sha(ROOT / relative) == expected, relative

points_by_id = {}
for id, expected in bindings['resource_glbs'].items():
    path = ROOT / bindings['capture_sources_to_current_files']['res://resources/' + id + '.glb']
    doc, points, triangles = mesh_data(path)
    points_by_id[id] = points
    assert sha(path) == expected['sha256']
    assert triangles == expected['triangles_independently_parsed']
    assert close(bounds(points), expected['bounds_declared'])
    for node in doc['nodes']:
        assert node.get('translation', [0, 0, 0]) == [0, 0, 0]
        assert node.get('rotation', [0, 0, 0, 1]) == [0, 0, 0, 1]
        assert node.get('scale', [1, 1, 1]) == [1, 1, 1]
        assert 'matrix' not in node
for name, case in capture['cases'].items():
    context = case['context']
    if not name.startswith('cargo-'):
        continue
    for placement in context.get('placement', []):
        angle = math.radians(placement['yaw'])
        x, y, z = placement['at']
        placed = [(x + .65 * (p[0] * math.cos(angle) + p[2] * math.sin(angle)),
                   y + .65 * p[1], z + .65 * (-p[0] * math.sin(angle) + p[2] * math.cos(angle)))
                  for p in points_by_id[placement['id']]]
        assert close(bounds(placed), placement['aabb']), name
        assert abs(min(p[1] for p in placed) - .72) < 1e-6

bed = HERE / 'context/f02-rev3-processor.glb'
contact = load(HERE / 'technical/output-contact-check-r7.json')
assert contact['result'] == 'PASS' and sha(bed) == contact['bed_sha256']
_, bed_points, _ = mesh_data(bed, 'OutputRack', 'body_light')
assert len(bed_points) == 24
rectangles = []
for offset in range(0, 24, 8):
    corners = bed_points[offset:offset + 8]
    assert abs(max(p[1] for p in corners) - .48) < 1e-6
    rectangles.append((min(p[0] for p in corners), max(p[0] for p in corners),
                       min(p[2] for p in corners), max(p[2] for p in corners)))
for id, case in contact['cases'].items():
    assert case['sha256'] == bindings['resource_glbs'][id]['sha256']
    bottom = {p for p in points_by_id[id] if abs(p[1]) < 1e-7}
    for row in case['placements']:
        x, y, z = row['placement']
        assert y == .48 and len(bottom) == row['lowest_vertices_on_bed']
        assert all(any(lo_x - 1e-6 <= x + .65 * p[0] <= hi_x + 1e-6 and
                       lo_z - 1e-6 <= z + .65 * p[2] <= hi_z + 1e-6
                       for lo_x, hi_x, lo_z, hi_z in rectangles) for p in bottom)

proof = load(HERE / 'portable/native-proof.json')
bridge = bindings['capture_source_bridge']
r7 = proof['sources']['art/source/resources/d12-r1/r7-portable/resources-grey-r7.blend']
assert r7['original_sha256'] == bridge['original_r7_sha256']
assert r7['portable_sha256'] == bridge['portable_r7_sha256']
for source in proof['sources'].values():
    assert source['native_reopen_exact_semantic_snapshot']
    assert source['closure']['libraries'] == source['closure']['linked_ids'] == 0
    assert all(row['full_glb_visual_payload_exact'] for row in source['resources'].values())
receipt = load(HERE / 'portable/current-verifier-receipt.json')
stage = load(HERE / 'portable/current-verifier-stage.json')
assert receipt['status'] == 'PASS_CURRENT_VERIFIER_ACTUALLY_RUN'
assert receipt['verifier_sha256'] == stage['verifier_sha256']
assert not receipt['original_linked_blends_present'] and receipt['frozen_files_matching'] == 98
assert stage['inputs'] == receipt['input_sha256']
# Receipt SHA references are raw; path normalization changes the published JSON/log SHA.
for run in receipt['runs']:
    assert run['observed_exit_code'] == 0
    for path_field, sha_field in [('log', 'log_sha256'), ('output_receipt', 'output_receipt_sha256')]:
        assert by_archive[run[path_field]]['original_sha256'] == run[sha_field]
assert by_archive[receipt['stage']]['original_sha256'] == receipt['stage_sha256']
addon = load(ROOT / bridge['independent_portable_identity_file'])
assert addon['verdict'] == bridge['independent_portable_identity_appendix'] == 'PASS_SOURCE_IDENTITY_PORTABILITY_LIMITED'
assert len(addon['files']) == 351
for file_field, raw_field in [('independent_portable_identity_file', 'independent_portable_identity_raw_sha256'),
                             ('independent_portable_report_file', 'independent_portable_report_raw_sha256')]:
    assert by_archive[bridge[file_field]]['original_sha256'] == bridge[raw_field]
assert addon['report']['path'] == bridge['independent_portable_report_file']
assert addon['report']['sha256'] == bridge['independent_portable_report_raw_sha256']
for path, expected in [('art/source/resources/d12-r1/r7-portable/resources-grey-r7.blend', bridge['portable_r7_sha256']),
                       ('art/source/resources/d12-r1/resource-pair-grey-r5-portable.blend', bridge['portable_r5_sha256']),
                       ('art/source/resources/d12-r1/r7/resources-grey-r7.blend', bridge['original_r7_sha256']),
                       ('art/source/resources/d12-r1/resource-pair-grey-r5.blend', bridge['original_r5_sha256'])]:
    assert addon['files'][path]['sha256'] == expected
for path in ['portable/native-proof.json', 'portable/current-verifier-receipt.json', 'portable/current-verifier-stage.json']:
    relative = (HERE / path).relative_to(ROOT).as_posix()
    assert addon['files'][relative]['sha256'] == by_archive[relative]['original_sha256']
assert addon['current_verifier']['sha256'] == receipt['verifier_sha256']
assert addon['current_verifier']['stage_sha256'] == receipt['stage_sha256']
assert addon['new_image_visual_assessment'].startswith('NOT_ASSESSED')
assert addon['native_Blender_rebuild_reopen_render'].startswith('NOT_RUN')
print(json.dumps({'status': 'PASS_ARCHIVE_BINDINGS_NOT_NEW_VISUAL_SIGNOFF',
                  'original_pngs': 57, 'archived_raw_public_pairs': len(archive['files']),
                  'current_capture_inputs_checked': len(bindings['capture_sources_to_current_files']),
                  'historical_capture_inputs_not_replayed': len(bindings['historical_capture_sources_not_replayed']),
                  'resource_triangles': {id: row['triangles_independently_parsed'] for id, row in bindings['resource_glbs'].items()},
                  'output_static_contacts': 12, 'portable_independent_identity': addon['verdict']}, indent=2))
