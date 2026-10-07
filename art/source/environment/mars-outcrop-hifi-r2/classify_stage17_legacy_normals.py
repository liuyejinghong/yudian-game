"""Read-only, same native version and identical candidate topology control."""
import bpy,sys,json
from pathlib import Path
import numpy as np
here=Path(__file__).resolve().parent;sys.path.insert(0,str(here))
import surface_stage17 as S
stage=S.STAGE
bpy.ops.wm.open_mainfile(filepath=str(S.SOURCE));o=bpy.data.objects['MarsOutcrop']
co,vi,nr,uv=S.arrays(o.data);tri=co[vi.reshape(-1,3)].astype(float);old_uv=uv.reshape(-1,3,2).astype(float);old_nr=nr.reshape(-1,3,3).astype(float)
o.data.normals_split_custom_set(nr.tolist());_,_,nr2,_=S.arrays(o.data);original=(tri,old_uv,old_nr,nr2.reshape(-1,3,3).astype(float))
identity=S.P.identity(stage/'source-candidate.blend')
bpy.ops.wm.open_mainfile(filepath=str(stage/'source-candidate.blend'));me=bpy.data.objects['MarsOutcrop'].data
co,vi,nr,uv=S.arrays(me);pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage17_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
ids=np.empty(len(me.polygons),np.int32);me.attributes['stage17_original_face'].data.foreach_get('value',ids);ids-=1;lf=np.repeat(ids,3)
keep=np.zeros(len(vi),bool)
for start in range(0,len(vi),100000):
 end=min(start+100000,len(vi));p=pre[vi[start:end]].astype(float);f=lf[start:end]
 d=np.linalg.norm(tri[f]-p[:,None,:],axis=2)
 keep[start:end]=(d.min(1)<1e-7)&(np.linalg.norm(co[vi[start:end]]-p,axis=1)<1e-7)
# Restore vertex coordinates only. Faces, loop order, UV, smoothing and sharp flags remain identical.
me.vertices.foreach_set('co',pre.ravel());me.update();report=S.set_low_normals(me,original)
_,_,control,_=S.arrays(me);delta=np.linalg.norm(nr.astype(float)-control.astype(float),axis=1)
sel=np.where(keep)[0];worst=sel[np.argsort(delta[sel])[-12:][::-1]]
report={'candidate':identity,'unmoved_same_topology_vs_original_native_roundtrip':report,
 'candidate_vs_unmoved_same_topology_max_delta':float(delta[keep].max()),'candidate_vs_unmoved_same_topology_p99_delta':float(np.quantile(delta[keep],.99)),
 'over_threshold':int((delta[keep]>.0003).sum()),'matched_corner_count':int(keep.sum()),
 'worst':[{'loop':int(i),'vertex':int(vi[i]),'original_face':int(lf[i]),'co':pre[vi[i]].tolist(),'delta':float(delta[i])} for i in worst]}
assert S.P.identity(stage/'source-candidate.blend')==identity
rid=np.empty(len(me.polygons),np.int32);me.attributes['stage17_face_region'].data.foreach_get('value',rid);lr=np.repeat(rid,3)
allowed=np.zeros(len(co),bool);allowed[vi[lr>0]]=True
moved=np.linalg.norm(co.astype(float)-pre,axis=1)>1e-7;affected=np.zeros(len(co),bool);affected[vi[np.repeat(moved[vi.reshape(-1,3)].any(1),3)]]=True
bad=np.where(keep & (delta>.0003))[0]
report['all_over_threshold_corners']=[{'loop':int(i),'vertex':int(vi[i]),'original_face':int(lf[i]),'original_face_region':int(lr[i]),'co':pre[vi[i]].tolist(),'delta':float(delta[i]),'vertex_touches_permitted_face':bool(allowed[vi[i]]),'fan_touches_displaced_face':bool(affected[vi[i]]),'strict_outside':bool(lr[i]==0 and not allowed[vi[i]] and not affected[vi[i]])} for i in bad]
report['over_threshold_original_face_regions']={str(k):int((lr[bad]==k).sum()) for k in np.unique(lr[bad])}
report['strict_outside_over_threshold_count']=sum(r['strict_outside'] for r in report['all_over_threshold_corners'])
S.P.write_json(stage/'legacy-35-classification.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
