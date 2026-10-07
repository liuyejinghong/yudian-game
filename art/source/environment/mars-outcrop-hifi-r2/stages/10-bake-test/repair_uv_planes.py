"""Conservative planar charts only for the remaining measured winding branch."""
import bpy,bmesh,sys,json,math,hashlib
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parents[2];sys.path.insert(0,str(HERE))
import pipeline as P
STAGE=HERE/'stages/10-bake-test'
bpy.ops.wm.open_mainfile(filepath=str(STAGE/'uv-branch-candidate.blend'))
low=bpy.data.objects['MarsOutcrop'];a=P.U._Arrays(low);labels,_=P.U._uv_islands(a)
faces=np.nonzero(labels==2255)[0].tolist();assert len(faces)==7228,len(faces)
outside=~np.isin(a.lf,faces);outside_uv=a.uv[outside].copy();geometry_hash=hashlib.sha256(a.co.tobytes()).hexdigest()
a.free();P.select(low)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='DESELECT')
bm=bmesh.from_edit_mesh(low.data);bm.faces.ensure_lookup_table()
for i in faces:bm.faces[i].select_set(True)
bmesh.update_edit_mesh(low.data)
bpy.ops.uv.smart_project(angle_limit=math.radians(40),island_margin=.006,area_weight=1.0,correct_aspect=True)
bpy.ops.object.mode_set(mode='OBJECT');a=P.U._Arrays(low)
outside_delta=float(np.max(np.abs(a.uv[outside]-outside_uv)));assert outside_delta==0,outside_delta;a.free()
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.005)
bpy.ops.object.mode_set(mode='OBJECT');P.U.seams_from_uv_islands(low)
qa=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
qa2048=P.U.uv_qa(low,tex_res=4096,analysis_res=2048,gap_px=16)
a=P.U._Arrays(low);labels,_=P.U._uv_islands(a);tris,tf=P.U._tris(a,a.uv);remaining=[]
assert hashlib.sha256(a.co.tobytes()).hexdigest()==geometry_hash
for k in sorted(set(qa['self_overlapping_islands']+qa2048['self_overlapping_islands'])):
    fm=labels==k
    remaining.append({'island':int(k),'faces':int(fm.sum()),
       'ratio_1024':P.U._self_overlap(a.uv,tris,tf,fm,res=1024),
       'ratio_2048':P.U._self_overlap(a.uv,tris,tf,fm,res=2048)})
a.free()
report={'method':'40 degree orientation charts restricted to measured winding branch','affected_faces':len(faces),
 'geometry_changed':False,'geometry_vertex_sha256':geometry_hash,'outside_uv_delta_before_pack':outside_delta,
 'qa':qa,'qa_2048':qa2048,'remaining':remaining}
P.write_json(STAGE/'uv-planar-candidate.json',report)
bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'uv-planar-candidate.blend'),compress=True)
print('PLANAR_UV_RESULT='+json.dumps({k:v for k,v in report.items() if k!='qa'}),flush=True)
