"""Split two measured chart collisions along their existing geological boundaries."""
import bpy,sys,json
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parents[2];sys.path.insert(0,str(HERE))
import pipeline as P
STAGE=HERE/'stages/10-bake-test'
bpy.ops.wm.open_mainfile(filepath=str(STAGE/'uv-local-candidate.blend'))
low=bpy.data.objects['MarsOutcrop'];a=P.U._Arrays(low);labels,_=P.U._uv_islands(a)
centers=np.zeros((len(low.data.polygons),3));np.add.at(centers,a.lf,a.co[a.lv]);centers/=a.lt[:,None]
x,y,z=centers.T;selections={k:np.nonzero(labels==k)[0].tolist() for k in (2013,2282)};a.free()
P.U.seams_from_uv_islands(low);solvers={}
for k,group in ((2013,z+.18*(x+1.2)>1.69),(2282,x>1.87-.43*y+.075*z)):
    cuts=P.U.seams_from_groups(low,groups=group.astype(int),faces=selections[k])
    solvers[k]={'cuts':cuts,'unwrap':P.U.unwrap(low,method='BEST',faces=selections[k])}
    assert all(n==0 for n in solvers[k]['unwrap']['unsolved_faces'].values()),solvers[k]
P.select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.005)
bpy.ops.object.mode_set(mode='OBJECT')
qa=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
a=P.U._Arrays(low);labels,_=P.U._uv_islands(a);tris,tf=P.U._tris(a,a.uv);remaining=[]
for k in qa['self_overlapping_islands']:
    fm=labels==k
    remaining.append({'island':int(k),'faces':int(fm.sum()),
       'ratio_1024':P.U._self_overlap(a.uv,tris,tf,fm,res=1024),
       'ratio_2048':P.U._self_overlap(a.uv,tris,tf,fm,res=2048)})
a.free()
P.write_json(STAGE/'uv-branch-candidate.json',{'solvers':solvers,'qa':qa,'remaining':remaining})
bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'uv-branch-candidate.blend'),compress=True)
print('BRANCH_UV_RESULT='+json.dumps({'solvers':solvers,'qa':{k:qa[k] for k in ('islands','flipped_faces','zero_area_faces','self_overlapping_islands','overlap_px_pct','td_px_per_m')},'remaining':remaining}),flush=True)
