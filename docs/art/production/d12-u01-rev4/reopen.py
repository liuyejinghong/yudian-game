"""Blender --background --factory-startup --python-exit-code 1 --python <this file>."""
from pathlib import Path
import bpy,json
r=Path(__file__).resolve().parents[4]
out={"blender":bpy.app.version_string,"checks":{}}
for ext in ['gltf','glb']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(r/'art/source/units/tuoyun-r1'/('tuoyun-r1.'+ext)))
    meshes=[o for o in bpy.data.objects if o.type=='MESH']
    count=0
    for o in meshes:
        o.data.calc_loop_triangles();count+=len(o.data.loop_triangles)
    out['checks'][ext]={'triangles':count,'mesh_nodes':len(meshes),'clips':len(bpy.data.actions)}
    assert count==7312 and len(bpy.data.actions)==5
assert out['checks']['gltf']==out['checks']['glb']
print(json.dumps(out))
(r/'docs/art/production/d12-u01-rev4/source-reopen.json').write_text(json.dumps(out,indent=2)+'\n')
