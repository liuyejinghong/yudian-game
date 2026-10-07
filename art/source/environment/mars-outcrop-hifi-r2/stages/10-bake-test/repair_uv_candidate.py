"""Local chart repair and exact collision locations; leaves the bake input untouched."""
import bpy,sys,json
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parents[2];sys.path.insert(0,str(HERE))
import pipeline as P
STAGE=HERE/'stages/10-bake-test'
bpy.ops.wm.open_mainfile(filepath=str(STAGE/'prepared.blend'))
low=bpy.data.objects['MarsOutcrop'];a=P.U._Arrays(low);labels,_=P.U._uv_islands(a)
face_sets={k:np.nonzero(labels==k)[0].tolist() for k in (1222,2282)};a.free()
P.U.seams_from_uv_islands(low);solvers={}
for k,method in ((1222,'MINIMUM_STRETCH'),(2282,'CONFORMAL')):
    solvers[k]=P.U.unwrap(low,method=method,faces=face_sets[k])
    assert all(n==0 for n in solvers[k]['unsolved_faces'].values()),solvers[k]
P.select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.005)
bpy.ops.object.mode_set(mode='OBJECT')
qa=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
a=P.U._Arrays(low);labels,_=P.U._uv_islands(a);tris,tf=P.U._tris(a,a.uv);locations=[]
for k in qa['self_overlapping_islands']:
    fm=labels==k;sel=fm[tf];tri=a.uv[tris[sel]];face_ids=tf[sel]
    origin=tri.reshape(-1,2).min(0);span=(tri.reshape(-1,2).max(0)-origin).max()
    samples={}
    for res in (1024,2048):
        count,lab,pairs=P.U._raster((tri-origin)/span*.999,face_ids,res)
        collided=sorted(set(f for pair in pairs for f in pair));rows=[]
        for i,j in sorted(pairs):
            ci=a.co[a.lv[np.nonzero(a.lf==i)[0]]];cj=a.co[a.lv[np.nonzero(a.lf==j)[0]]]
            rows.append({'faces':[i,j],'centers':[ci.mean(0).tolist(),cj.mean(0).tolist()],
                'center_distance_m':float(np.linalg.norm(ci.mean(0)-cj.mean(0)))})
        samples[res]={'overlap_pixels':int((count>1).sum()),'covered_pixels':int((count>0).sum()),
            'ratio':float((count>1).sum()/max((count>0).sum(),1)),'collision_faces':collided,'pairs':rows}
    locations.append({'island':int(k),'faces':int(fm.sum()),'samples':samples})
a.free()
P.write_json(STAGE/'uv-local-candidate.json',{'solvers':solvers,'qa':qa,'locations':locations})
bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'uv-local-candidate.blend'),compress=True)
print('LOCAL_REPAIR_LOCATIONS='+json.dumps(locations),flush=True)
