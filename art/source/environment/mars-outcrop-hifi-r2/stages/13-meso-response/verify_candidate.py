import bpy,json,sys,hashlib
from pathlib import Path
p=Path(__file__).resolve().parent
sys.path.insert(0,str(p.parent.parent))
import build as B
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/'candidate.glb'))
objects=[o for o in bpy.data.objects if o.type=='MESH']
assert len(objects)==1
o=objects[0];tris=sum(len(v.vertices)-2 for v in o.data.polygons);assert tris==227160
images=[im for im in bpy.data.images if im.source!='GENERATED']
sizes=sorted([list(im.size) for im in images]);assert sizes==[[512,512],[512,512],[4096,4096]],sizes
report={'status':'PASS','independent_blender_import':True,'triangles':tris,'images':sizes,'candidate_glb_sha256':hashlib.sha256((p/'candidate.glb').read_bytes()).hexdigest()}
(p/'native-import.json').write_text(json.dumps(report,indent=2)+'\n');print('RESULT_JSON='+json.dumps(report))

bpy.context.scene.world=bpy.data.worlds.new('DiagnosticWorld');bpy.context.scene.world.use_nodes=True
B.renders(p,prefix='imported-',views={'bed-close':((-1.7,-4.0,1.8),(-1.45,-1.02,1.08),72)})
