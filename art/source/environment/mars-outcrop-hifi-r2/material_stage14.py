"""Authorized two-map 4K candidate; use frozen normal, UV and geometry bytes."""
import bpy,json,hashlib,sys
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import pipeline as P
import build as B
import material_stage13 as S

stage=HERE/'stages/14-meso-response-4k';textures=stage/'textures';textures.mkdir(exist_ok=True)
proof=json.loads((stage/'color-roundtrip.json').read_text());assert proof['status']=='PASS'
encoded=proof['required_encoded_pixel_input']
# Independently reopen the saved 8-bit probe, not its generated in-memory buffer.
probe=bpy.data.images.load(str(stage/'False-encoded_srgb.png'),check_existing=False)
assert max(abs(a-b) for a,b in zip(probe.pixels[:3],encoded))<.004
proof['saved_byte_png_reopened']=True
P.write_json(stage/'color-roundtrip.json',proof)
source=HERE/'mars-outcrop-hifi-r2.blend';glb=HERE/'mars-outcrop-hifi-r2.glb'
protected=[source,glb,*sorted((HERE/'textures').glob('*.png')),
    HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.blend',
    HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.glb']
identities={str(p.relative_to(B.ROOT)):P.identity(p) for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(source));high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
mesh_id=S.mesh_identity(low)
high_co=np.empty(len(high.data.vertices)*3,np.float32);high.data.vertices.foreach_get('co',high_co)
normal=next(n.image for n in low.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image and Path(n.image.filepath).name=='normal.png')
normal_hash=hashlib.sha256(normal.packed_file.data).hexdigest()
normal_socket=high.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Normal'].links[0].from_socket
params=S.meso_response(high)
assert high.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Normal'].links[0].from_socket==normal_socket
projection=json.loads((HERE/'stages/10-bake-test/test.json').read_text())['projection']
maps={'normal':normal};paths={}
for role in ('base_color','roughness'):
    im=P.new_image('meso4k-'+role,4096,role)
    fill=(*encoded,1) if role=='base_color' else (.78,.78,.78,1)
    im.pixels.foreach_set(np.tile(np.array(fill,np.float32),(4096*4096,1)).ravel());im.update()
    P.bake_pair(high,low,im,role,projection,clear=False)
    path=textures/(role+'.png');P.save_map(im,path);maps[role]=im;paths[role]=path
audit=P.material(low,maps);assert not audit
high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
B.renders(stage,prefix='candidate-',views={k:S.VIEWS[k] for k in ('detail','rear-detail')})
assert S.mesh_identity(low)==mesh_id
after=np.empty_like(high_co);high.data.vertices.foreach_get('co',after);assert np.array_equal(high_co,after)
assert hashlib.sha256(normal.packed_file.data).hexdigest()==normal_hash
for role in ('base_color','roughness'):maps[role].filepath='//textures/'+role+'.png'
normal.filepath='//../../textures/normal.png'
P.portable_metadata()
bpy.ops.wm.save_as_mainfile(filepath=str(stage/'candidate.blend'),compress=True,relative_remap=False)
glb_proof=S.candidate_glb(glb,stage/'candidate.glb',paths)
assert all(P.identity(B.ROOT/p)==old for p,old in identities.items())
report={'status':'4K_TWO_MAP_CANDIDATE_PENDING_FIXED_GODOT_REVIEW','formal_files_changed':False,
    'protected_files':identities,'parameters':params,'game_mesh_uv_sha256':mesh_id,
    'normal_png_sha256':normal_hash,'source_high_geometry_unchanged':True,'glb_proof':glb_proof,
    'material_audit':audit,'color_roundtrip':'color-roundtrip.json',
    'maps':{role:P.identity(path) for role,path in paths.items()},
    'candidate_glb':P.identity(stage/'candidate.glb'),
    'scope':'Two 4096 RGB8 EMIT maps only. Correctly encoded neutral background; resolution change does not fix color-space encoding. Original geometry, UV, normals, tangents and full-resolution normal PNG unchanged.'}
P.write_json(stage/'stage.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
