"""Blender: copy the r1 support terrain and open a real 12 m hole for r2."""
import bpy
import bmesh
import hashlib
import json
import math
import struct
from pathlib import Path

HERE = Path(__file__).resolve().parent
source = HERE / 'ground/mars-ground-r1.blend'
target = HERE / 'hifi'
target.mkdir(exist_ok=True)
assert not (target / 'ground-with-patch-hole.blend').exists(), 'preserve prior output before regenerating'
original = hashlib.sha256(source.read_bytes()).hexdigest()
patch = target / 'mars-ground-patch-hifi-r2.glb'
blob = patch.read_bytes()
length = struct.unpack_from('<I', blob, 12)[0]
gltf = json.loads(blob[20:20+length])
binary = blob[28+length:]
node = next(n for n in gltf['nodes'] if 'SandApron' in n.get('name', ''))
assert not any(k in node for k in ('matrix', 'translation', 'rotation', 'scale'))
primitive = gltf['meshes'][node['mesh']]['primitives'][0]

def vectors(attribute):
    accessor = gltf['accessors'][primitive['attributes'][attribute]]
    assert accessor['componentType'] == 5126 and accessor['type'] == 'VEC3'
    view = gltf['bufferViews'][accessor['bufferView']]
    offset = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
    return [struct.unpack_from('<3f', binary, offset + i*view.get('byteStride', 12))
            for i in range(accessor['count'])]

edge_normals = {}
for (x, y, z), (nx, ny, nz) in zip(vectors('POSITION'), vectors('NORMAL')):
    x += 6
    if abs(x) < 1e-5 or abs(x-12) < 1e-5 or abs(abs(z)-6) < 1e-5:
        edge_normals[round(x, 5), round(-z, 5)] = (nx, -nz, ny)
assert len(edge_normals) == 600
bpy.ops.wm.open_mainfile(filepath=str(source))
removed = {}
for obj in [o for o in bpy.context.scene.objects if o.type == 'MESH']:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if 'regolith' in obj.name.lower():
        faces = [f for f in bm.faces if 0 < f.calc_center_median().x < 12
                 and -6 < f.calc_center_median().y < 6]
        assert len(faces) == 36, 'hole must match the original 2 m grid'
        bmesh.ops.delete(bm, geom=faces, context='FACES_ONLY')
        boundary = [e for e in bm.edges if len(e.link_faces) == 1
                    and all(0 <= v.co.x <= 12 and -6 <= v.co.y <= 6 for v in e.verts)]
        assert len(boundary) == 24, 'four sides, six original 2 m edges per side'
        bmesh.ops.subdivide_edges(bm, edges=boundary, cuts=24, use_grid_fill=False)
        uv = bm.loops.layers.uv.verify()
        for face in bm.faces:
            for axis, offset in [(0, 0), (1, 6)]:
                values = [(v.co[axis] + offset) / 12 for v in face.verts]
                assert math.ceil(min(values) + 1e-6) >= max(values) - 1e-6, 'face crosses mirrored UV fold'
            for loop in face.loops:
                # glTF flips V; Godot receives the patch's world XZ mapping.
                # ponytail: mirrored 12 m lookdev tile; large terrain needs varied tiles.
                loop[uv].uv = tuple(1-abs(1-(t % 2)) for t in
                                   (loop.vert.co.x / 12, (loop.vert.co.y + 6) / 12))
        removed[obj.name] = 36
    else:
        pending = set(bm.verts)
        chosen = []
        while pending:
            seed = pending.pop()
            component = {seed}
            stack = [seed]
            while stack:
                v = stack.pop()
                for edge in v.link_edges:
                    other = edge.other_vert(v)
                    if other in pending:
                        pending.remove(other)
                        component.add(other)
                        stack.append(other)
            # Remove whole fragments intersecting the module; no cut fragments.
            xs = [v.co.x for v in component]
            ys = [v.co.y for v in component]
            if max(xs) > 0 and min(xs) < 12 and max(ys) > -6 and min(ys) < 6:
                chosen.extend(component)
        removed[obj.name] = len(chosen)
        bmesh.ops.delete(bm, geom=chosen, context='VERTS')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    if 'regolith' in obj.name.lower():
        normals = [tuple(v.normal) for v in obj.data.vertices]
        matched = 0
        for vertex in obj.data.vertices:
            key = (round(vertex.co.x, 5), round(vertex.co.y, 5))
            if key in edge_normals:
                normals[vertex.index] = edge_normals[key]
                matched += 1
        assert matched == 600
        obj.data.normals_split_custom_set_from_vertices(normals)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(target / 'ground-with-patch-hole.blend'), compress=True)
bpy.ops.export_scene.gltf(filepath=str(target / 'ground-with-patch-hole.glb'), export_format='GLB')
assert hashlib.sha256(source.read_bytes()).hexdigest() == original
record = {'blender': bpy.app.version_string, 'r1_source_unchanged': True,
          'r1_source_sha256': original, 'hole_world_xz_m': [0, 12, -6, 6],
          'patch_sha256': hashlib.sha256(blob).hexdigest(),
          'matching_edge_vertices': 600, 'edge_normals': 'copied from delivered patch',
          'support_uv': 'mirrored 12 m world UV, continuous at all four patch edges',
          'removed': removed, 'output_glb_sha256': hashlib.sha256((target / 'ground-with-patch-hole.glb').read_bytes()).hexdigest()}
(target / 'ground-preparation.json').write_text(json.dumps(record, indent=2)+'\n')
print('MARS_HIFI_GROUND_PREP_OK '+json.dumps(record))
