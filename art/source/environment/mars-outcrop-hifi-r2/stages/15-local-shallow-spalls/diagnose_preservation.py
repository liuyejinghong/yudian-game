"""Read-only attribution of stage15 UV and custom-normal differences."""
import bpy,json,sys
import numpy as np
from pathlib import Path
stage=Path(__file__).resolve().parent;here=stage.parent.parent
def arrays(me):
    co=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('co',co)
    vi=np.empty(len(me.loops),np.int32);me.loops.foreach_get('vertex_index',vi)
    uv=np.empty(len(me.loops)*2,np.float32);me.uv_layers.active.data.foreach_get('uv',uv)
    nr=np.empty(len(me.loops)*3,np.float32);me.corner_normals.foreach_get('vector',nr)
    return co.reshape(-1,3),vi,uv.reshape(-1,2),nr.reshape(-1,3)
def signed_area(tri):
    a=tri[:,1]-tri[:,0];b=tri[:,2]-tri[:,0]
    return (a[:,0]*b[:,1]-a[:,1]*b[:,0])*.5
bpy.ops.wm.open_mainfile(filepath=str(here/'mars-outcrop-hifi-r2.blend'))
old=bpy.data.objects['MarsOutcrop'];co,vi,uv,nr=arrays(old.data)
tri=co[vi.reshape(-1,3)].astype(float);old_uv=uv.reshape(-1,3,2).astype(float);old_nr=nr.reshape(-1,3,3).astype(float)
report={'baseline_uv_negative':int((signed_area(old_uv)<-1e-12).sum())}
# In-memory roundtrip only; the original .blend is never saved.
old.data.normals_split_custom_set(nr.tolist());_,_,_,nr2=arrays(old.data)
report['baseline_normal_roundtrip_max_delta']=float(np.linalg.norm(nr2-nr,axis=1).max())
old_roundtrip=nr2.reshape(-1,3,3).astype(float)
bpy.ops.wm.open_mainfile(filepath=str(stage/'source-candidate.blend'))
o=bpy.data.objects['MarsOutcrop'];me=o.data;co,vi,uv,nr=arrays(me)
pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage15_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
ids=np.empty(len(me.polygons),np.int32);me.attributes['stage15_original_face'].data.foreach_get('value',ids);ids-=1
sizes=np.empty(len(me.polygons),np.int32);me.polygons.foreach_get('loop_total',sizes);lf=np.repeat(ids,sizes)
me.calc_loop_triangles();tl=np.array([t.loops for t in me.loop_triangles]);tf=np.array([t.polygon_index for t in me.loop_triangles])
area=signed_area(uv[tl].astype(float));negative=np.where(area<-1e-12)[0]
records=[]
for t in negative:
    p=co[vi[tl[t]]].astype(float);q=tri[ids[tf[t]]]
    cr=np.cross(p[1]-p[0],p[2]-p[0]);orig=np.cross(q[1]-q[0],q[2]-q[0])
    records.append({'triangle':int(t),'source_face':int(ids[tf[t]]),'uv_signed_area':float(area[t]),
        'physical_area_m2':float(np.linalg.norm(cr)*.5),'source_normal_dot':float(cr@orig/(np.linalg.norm(cr)*np.linalg.norm(orig))),
        'center_m':p.mean(axis=0).tolist(),'polygon_corners':int(sizes[tf[t]])})
report['negative_triangles']=records
# Compare only untouched corners that still occupy an original triangle vertex.
errors=[];control_errors=[];where=[];old_indices=[]
for start in range(0,len(vi),100000):
    end=min(len(vi),start+100000);f=lf[start:end];p=pre[vi[start:end]].astype(float)
    distance=np.linalg.norm(tri[f]-p[:,None,:],axis=2);match=distance.min(axis=1)<1e-7
    fixed=np.linalg.norm(co[vi[start:end]]-p,axis=1)<1e-7;keep=match&fixed
    expected=old_nr[f[keep],distance[keep].argmin(axis=1)]
    errors.extend(np.linalg.norm(expected-nr[start:end][keep],axis=1).tolist())
    control=old_roundtrip[f[keep],distance[keep].argmin(axis=1)]
    control_errors.extend(np.linalg.norm(control-nr[start:end][keep],axis=1).tolist())
    where.extend(p[keep].tolist());old_indices.extend(f[keep].tolist())
errors=np.asarray(errors);idx=int(errors.argmax())
report['untouched_original_vertex_corner_normal']={'count':len(errors),'max_delta':float(errors.max()),
    'p99_delta':float(np.quantile(errors,.99)),'max_center_m':where[idx],'source_face':old_indices[idx],
    'compared_to_native_roundtrip_max_delta':float(np.max(control_errors)),
    'compared_to_native_roundtrip_p99_delta':float(np.quantile(control_errors,.99))}
(stage/'preservation-diagnosis.json').write_text(json.dumps(report,indent=2)+'\n')
print('RESULT_JSON='+json.dumps(report),flush=True)
