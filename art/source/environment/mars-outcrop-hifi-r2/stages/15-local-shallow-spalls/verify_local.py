"""Read-only local topology/UV/normal preservation proof for the stage15 study."""
import bpy,json,sys
import numpy as np
from pathlib import Path
stage=Path(__file__).resolve().parent;here=stage.parent.parent;sys.path.insert(0,str(here))
import pipeline as P

def arrays(me):
    co=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('co',co)
    vi=np.empty(len(me.loops),np.int32);me.loops.foreach_get('vertex_index',vi)
    uv=np.empty(len(me.loops)*2,np.float32);me.uv_layers.active.data.foreach_get('uv',uv)
    nr=np.empty(len(me.loops)*3,np.float32);me.corner_normals.foreach_get('vector',nr)
    return co.reshape(-1,3),vi,uv.reshape(-1,2),nr.reshape(-1,3)

bpy.ops.wm.open_mainfile(filepath=str(here/'mars-outcrop-hifi-r2.blend'))
old=bpy.data.objects['MarsOutcrop'];co,vi,uv,nr=arrays(old.data)
old_tri=co[vi.reshape(-1,3)].astype(float);old_uv=uv.reshape(-1,3,2).astype(float);old_nr=nr.reshape(-1,3,3).astype(float)
old_bounds=np.array(old.bound_box);old_area=np.cross(old_uv[:,1]-old_uv[:,0],old_uv[:,2]-old_uv[:,0])*.5
bpy.ops.wm.open_mainfile(filepath=str(stage/'source-candidate.blend'))
o=bpy.data.objects['MarsOutcrop'];me=o.data;co,vi,uv,nr=arrays(me)
pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage15_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
face_ids=np.empty(len(me.polygons),np.int32);me.attributes['stage15_original_face'].data.foreach_get('value',face_ids);face_ids-=1
assert face_ids.min()>=0 and face_ids.max()<len(old_tri)
sizes=np.empty(len(me.polygons),np.int32);me.polygons.foreach_get('loop_total',sizes)
loop_faces=np.repeat(face_ids,sizes);report={};max_uv=0;outside_deltas=[]
for start in range(0,len(vi),100000):
    end=min(start+100000,len(vi));f=loop_faces[start:end];p=pre[vi[start:end]].astype(float)
    a,b,c=old_tri[f,0],old_tri[f,1],old_tri[f,2];v0=b-a;v1=c-a;v2=p-a
    d00=np.sum(v0*v0,axis=1);d01=np.sum(v0*v1,axis=1);d11=np.sum(v1*v1,axis=1)
    d20=np.sum(v2*v0,axis=1);d21=np.sum(v2*v1,axis=1);den=d00*d11-d01*d01
    safe=np.where(np.abs(den)>1e-26,den,1)
    w1=(d11*d20-d01*d21)/safe;w2=(d00*d21-d01*d20)/safe;weights=np.column_stack((1-w1-w2,w1,w2))
    distances=np.linalg.norm(old_tri[f]-p[:,None,:],axis=2);same=distances.min(axis=1)<1e-7
    weights[same]=np.eye(3)[distances[same].argmin(axis=1)]
    expected_uv=np.sum(old_uv[f]*weights[:,:,None],axis=1)
    max_uv=max(max_uv,float(np.abs(expected_uv-uv[start:end]).max()))
    expected=np.sum(old_nr[f]*weights[:,:,None],axis=1);expected/=np.linalg.norm(expected,axis=1)[:,None]
    untouched=np.linalg.norm(co[vi[start:end]]-p,axis=1)<1e-7
    outside_deltas.extend(np.linalg.norm(expected[untouched]-nr[start:end][untouched],axis=1).tolist())
me.calc_loop_triangles();tri_loops=np.array([t.loops for t in me.loop_triangles]);tri_faces=np.array([t.polygon_index for t in me.loop_triangles])
uv_tri=uv[tri_loops].astype(float);area=np.cross(uv_tri[:,1]-uv_tri[:,0],uv_tri[:,2]-uv_tri[:,0])*.5
recovered=np.bincount(face_ids[tri_faces],weights=area,minlength=len(old_area))
report={'max_corner_uv_interpolation_error':max_uv,
    'max_uv_area_partition_error':float(np.max(np.abs(recovered-old_area))),
    'outside_exposure_corner_normal_max_delta':float(np.max(outside_deltas)),
    'outside_exposure_corner_normal_p99_delta':float(np.quantile(outside_deltas,.99)),
    'uv_negative_area_triangles':int((area<-1e-12).sum()),
    'uv_effectively_zero_area_triangles':int((np.abs(area)<1e-14).sum()),
    'non_triangular_polygons':int((sizes!=3).sum()),'triangles':len(tri_loops),
    'max_vertex_displacement_m':float(np.linalg.norm(co-pre,axis=1).max()),
    'macro_bounds_max_delta':float(np.max(np.abs(np.array(o.bound_box)-old_bounds)))}
report['status']='PASS' if max_uv<3e-5 and report['outside_exposure_corner_normal_max_delta']<.0003 and report['macro_bounds_max_delta']<1e-6 and report['uv_negative_area_triangles']==0 else 'REWORK'
(stage/'local-preservation.json').write_text(json.dumps(report,indent=2)+'\n');print('RESULT_JSON='+json.dumps(report))
