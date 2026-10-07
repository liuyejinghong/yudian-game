import bpy, hashlib, json, math, struct
from pathlib import Path
source=Path(bpy.data.filepath).resolve().parent
import sys
args=sys.argv[sys.argv.index('--')+1:]
assert len(args)==1, 'new output directory required'
out=Path(args[0])
out.mkdir(exist_ok=False)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(objects)==3
assert {o.data.materials[0].name for o in objects}=={'Regolith','Bedrock','Clast'}
assert bpy.context.scene.unit_settings.scale_length==1
assert all(math.isfinite(v.co[k]) for o in objects for v in o.data.vertices for k in range(3))
assert all(tuple(o.scale)==(1,1,1) for o in objects)
ground=bpy.data.objects['Regolith_Continuous']
assert len(ground.data.vertices)==63001
assert all(p.normal.z>.8 and p.use_smooth for p in ground.data.polygons)
for x,z in [(6,0),(-5,5),(0,-6),(-6,-2)]:
    near=[v.co.z for v in ground.data.vertices if abs(v.co.x-x)<1.001 and abs(v.co.y+z)<1.001]
    assert near and max(abs(y) for y in near)<1e-7
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=ground
bpy.ops.export_scene.gltf(filepath=str(out/'reopened.glb'),export_format='GLB',use_selection=True,export_yup=True,export_materials='EXPORT',export_cameras=False,export_extras=True)
a=(source/'mars-ground-r1.glb').read_bytes();b=(out/'reopened.glb').read_bytes()
assert a==b,'Ground source/export bytes differ'
result={'source_reopen':'PASS','meshes':3,'finite_vertices':'PASS','anchor_ground_vertices':'PASS','unit_scale':'PASS','same_glb_bytes':True,'sha256':hashlib.sha256(b).hexdigest()}
(out/'check.json').write_text(json.dumps(result,indent=2)+'\n')
print('ROOT_GROUND_REOPEN_OK',json.dumps(result))
