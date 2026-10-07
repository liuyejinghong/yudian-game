"""Blender: measure patch boundary vertices against the support mesh edges."""
import bisect
import hashlib
import json
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent


def load(path):
    if path.suffix == '.blend':
        bpy.ops.wm.open_mainfile(filepath=str(path))
    else:
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.gltf(filepath=str(path))


def edge_attributes(obj, offset_x=0):
    result = {}
    transform = obj.matrix_world.to_3x3().inverted().transposed()
    uv = obj.data.uv_layers.active.data
    for loop in obj.data.loops:
        x, y, z = obj.matrix_world @ obj.data.vertices[loop.vertex_index].co
        x += offset_x
        if not (0-1e-5 <= x <= 12+1e-5 and -6-1e-5 <= y <= 6+1e-5):
            continue
        if abs(x) < 1e-5 or abs(x-12) < 1e-5 or abs(abs(y)-6) < 1e-5:
            normal = (transform @ obj.data.corner_normals[loop.index].vector).normalized()
            result[round(x, 5), round(y, 5)] = (normal, uv[loop.index].uv.copy())
    assert len(result) == 600, len(result)
    return result


def check(patch):
    support = HERE / 'hifi/ground-with-patch-hole.glb'
    load(support)
    ground = next(o for o in bpy.context.scene.objects
                  if o.type == 'MESH' and 'regolith' in o.name.lower())
    points = [tuple(ground.matrix_world @ v.co) for v in ground.data.vertices]
    support_attributes = edge_attributes(ground)
    segments = {}
    for side, axis, value in [('left', 0, 0), ('right', 0, 12),
                              ('front', 1, -6), ('back', 1, 6)]:
        other = 1-axis
        low, high = (-6, 6) if axis == 0 else (0, 12)
        segments[side] = sorted({(p[other], p[2]) for p in points
                                if abs(p[axis]-value) < 1e-6
                                and low <= p[other] <= high})
        assert len(segments[side]) == 151, (side, len(segments[side]))
    load(patch)
    terrain = next(o for o in bpy.context.scene.objects
                   if o.type == 'MESH' and 'SandApron' in o.name)
    patch_attributes = edge_attributes(terrain, 6)
    assert support_attributes.keys() == patch_attributes.keys()
    min_normal_dot = min(n.dot(patch_attributes[k][0]) for k, (n, uv) in support_attributes.items())
    max_uv_distance = max((uv-patch_attributes[k][1]).length for k, (n, uv) in support_attributes.items())
    assert min_normal_dot > .99999 and max_uv_distance < 1e-5, (min_normal_dot, max_uv_distance)
    errors = []
    point_errors = []
    for vertex in terrain.data.vertices:
        x, y, z = terrain.matrix_world @ vertex.co
        x += 6  # Matches the module's world placement in lookdev.gd.
        side = ('left' if abs(x) < 1e-6 else 'right' if abs(x-12) < 1e-6
                else 'front' if abs(y+6) < 1e-6 else 'back' if abs(y-6) < 1e-6 else None)
        if side is None:
            continue
        rows = segments[side]
        t = y if side in ('left', 'right') else x
        i = min(max(1, bisect.bisect_left([a for a, b in rows], t)), len(rows)-1)
        a, za = rows[i-1]
        b, zb = rows[i]
        errors.append(abs(z-(za+(zb-za)*(t-a)/(b-a))))
        point_errors.append(min(((t-a)**2+(z-b)**2)**.5 for a, b in rows))
    assert errors and max(errors) <= 1e-5, ('boundary separation', max(errors, default=-1))
    assert max(point_errors) <= 1e-5, ('unmatched boundary vertex', max(point_errors))
    result = {'status': 'PASS', 'blender': bpy.app.version_string,
              'support_sha256': hashlib.sha256(support.read_bytes()).hexdigest(),
              'patch_sha256': hashlib.sha256(patch.read_bytes()).hexdigest(),
              'boundary_vertices': len(errors), 'max_separation_m': max(errors),
              'max_matching_vertex_distance_m': max(point_errors),
              'min_boundary_normal_dot': min_normal_dot, 'max_boundary_uv_distance': max_uv_distance}
    print('MARS_HIFI_JOIN_OK '+json.dumps(result))
    return result


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    assert len(args) <= 1, 'optional patch .blend or .glb path'
    check(Path(args[0]) if args else HERE / 'hifi/mars-ground-patch-hifi-r2.glb')
