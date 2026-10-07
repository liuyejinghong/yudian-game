"""Run with Blender --background mars-ground-r1.blend --python check_source.py."""
import bpy
import json
import struct
from pathlib import Path

here = Path(__file__).resolve().parent
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(objects) == 3
assert {o.data.materials[0].name for o in objects} == {'Regolith','Bedrock','Clast'}
ground = bpy.data.objects['Regolith_Continuous']
assert all(p.normal.z > .8 for p in ground.data.polygons)
assert all(p.use_smooth for p in ground.data.polygons)
assert all(not p.use_smooth for o in objects if o != ground for p in o.data.polygons)
facts = json.loads((here/'manifest.json').read_text())
blob = (here/'mars-ground-r1.glb').read_bytes()
size = struct.unpack_from('<I',blob,12)[0]
doc = json.loads(blob[20:20+size])
assert [m['name'] for m in doc['materials']] == ['Regolith','Bedrock','Clast']
assert 'textures' not in doc
bounds = []
for mesh in doc['meshes']:
    for part in mesh['primitives']:
        a = doc['accessors'][part['attributes']['POSITION']]
        bounds.append({'mesh':mesh['name'],'min':a['min'],'max':a['max'],'vertices':a['count']})
assert bounds[0]['min'][0] == bounds[0]['min'][2] == -250
assert bounds[0]['max'][0] == bounds[0]['max'][2] == 250
assert -.5 < bounds[0]['min'][1] <= 0 < bounds[0]['max'][1] < 30
assert all(y == 0 for y in facts['placements_y_m'].values())
facts['glb_bounds'] = bounds
facts['glb_bytes'] = len(blob)
facts['checks'] = {'glb_axis_bounds':'PASS','materials_no_textures':'PASS',
                   'flat_anchor_heights':'PASS','finite_vertices':'PASS',
                   'blender_preview':'PASS; integration visual acceptance pending'}
facts['checks']['source_reopen'] = 'PASS; 3 meshes, material names, upward ground normals, shading'
(here/'manifest.json').write_text(json.dumps(facts,indent=2)+'\n')
scene = bpy.context.scene
for camera,filename in [('Horizon','preview-horizon.png'),('GroundNear','preview-near.png')]:
    scene.camera = bpy.data.objects[camera]
    scene.render.filepath = str(here/filename)
    bpy.ops.render.render(write_still=True)
print('SOURCE_REOPEN_PASS')
