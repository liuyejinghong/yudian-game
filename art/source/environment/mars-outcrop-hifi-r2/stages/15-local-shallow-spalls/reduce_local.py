"""One conservative native planar-reduction trial; keep the dense source on failure."""
import bpy,bmesh,sys,json,math
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
STAGE=Path(__file__).resolve().parent;HERE=STAGE.parent.parent;sys.path.insert(0,str(HERE))
import sculpt_stage15 as S
import sculpt_stage15_surface as F
import pipeline as P

bpy.ops.wm.open_mainfile(filepath=str(S.SOURCE));old=bpy.data.objects['MarsOutcrop'];co,vi,nr,uv=S.original_arrays(old.data)
old_tri=co[vi.reshape(-1,3)].astype(float);old_uv=uv.reshape(-1,3,2).astype(float);old_nr=nr.reshape(-1,3,3).astype(float)
old.data.normals_split_custom_set(nr.tolist());_,_,control,_=S.original_arrays(old.data);control=control.reshape(-1,3,3).astype(float)
bpy.ops.wm.open_mainfile(filepath=str(STAGE/'source-candidate.blend'));low=bpy.data.objects['MarsOutcrop'];high=bpy.data.objects['HIGH_ParentSandstone'];me=low.data
before_co,_,_,_=S.original_arrays(me);before_bounds=np.array(low.bound_box);before_count=P.triangles(low)
bm=bmesh.new();bm.from_mesh(me);source=bm.faces.layers.int['stage15_original_face'];pos=bm.verts.layers.float_vector['stage15_original_position']
local=set()
for v in bm.verts:
    p=np.array(v[pos])
    if any(abs(S.local(p,r)[0])<r['half'][0]+.055 and abs(S.local(p,r)[1])<r['half'][1]+.055 and abs(S.local(p,r)[2])<.045 for r in S.REGIONS):local.add(v)
edges=[e for e in bm.edges if all(v in local for v in e.verts) and len(e.link_faces)==2 and e.link_faces[0][source]==e.link_faces[1][source]]
verts=[v for v in local if len({f[source] for f in v.link_faces})==1]
report={'input':P.identity(STAGE/'source-candidate.blend'),'angle_limit_degrees':.35,
    'scope':'Only within the two prepared charts and within a single original triangle; original face and UV boundaries protected',
    'candidate_edges':len(edges),'candidate_vertices':len(verts),'triangles_before':before_count,
    'acceptance_max_surface_deviation_m':.0005,'acceptance_p99_surface_deviation_m':.00015,
    'acceptance_native_roundtrip_corner_normal_delta':.0003}
bmesh.ops.dissolve_limit(bm,angle_limit=math.radians(.35),use_dissolve_boundaries=False,verts=verts,edges=edges,delimit={'UV','SHARP','MATERIAL'})
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3],quad_method='FIXED',ngon_method='EAR_CLIP')
try:report['micro_sliver_repairs']=F.retessellate_uv_slivers(bm)
except AssertionError as e:
    report['status']='REJECTED_KEEP_DENSE';report['reason']=str(e);P.write_json(STAGE/'local-reduction.json',report);print('RESULT_JSON='+json.dumps(report));raise SystemExit(0)
bm.normal_update();tree=BVHTree.FromBMesh(bm)
distances=np.array([tree.find_nearest(Vector(p))[3] for p in before_co])
report['surface_distance_max_m']=float(distances.max());report['surface_distance_p99_m']=float(np.quantile(distances,.99))
report['compared_dense_vertices']=len(before_co)
bm.to_mesh(me);bm.free();me.update()
co,vi,_,uv=S.original_arrays(me);pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage15_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
ids=np.empty(len(me.polygons),np.int32);me.attributes['stage15_original_face'].data.foreach_get('value',ids);ids-=1
assert all(len(p.vertices)==3 for p in me.polygons);lf=np.repeat(ids,3)
vn=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('normal',vn);vn=vn.reshape(-1,3)
normals=np.empty((len(vi),3),float);max_uv=0;control_refs=[];control_loop=[]
for start in range(0,len(vi),100000):
    end=min(start+100000,len(vi));f=lf[start:end];p=pre[vi[start:end]].astype(float)
    a,b,c=old_tri[f,0],old_tri[f,1],old_tri[f,2];e0=b-a;e1=c-a;d=p-a
    d00=(e0*e0).sum(1);d01=(e0*e1).sum(1);d11=(e1*e1).sum(1);d20=(d*e0).sum(1);d21=(d*e1).sum(1);den=d00*d11-d01*d01
    w1=(d11*d20-d01*d21)/den;w2=(d00*d21-d01*d20)/den;w=np.column_stack((1-w1-w2,w1,w2))
    dist=np.linalg.norm(old_tri[f]-p[:,None,:],axis=2);same=dist.min(1)<1e-7;w[same]=np.eye(3)[dist[same].argmin(1)]
    expected_uv=(old_uv[f]*w[:,:,None]).sum(1);max_uv=max(max_uv,float(abs(expected_uv-uv[start:end]).max()))
    normal=(old_nr[f]*w[:,:,None]).sum(1);changed=np.linalg.norm(co[vi[start:end]]-p,axis=1)>1e-7
    normal[changed]=vn[vi[start:end][changed]];normals[start:end]=normal
    keep=same&~changed;control_refs.extend(control[f[keep],dist[keep].argmin(1)].tolist());control_loop.extend((start+np.where(keep)[0]).tolist())
me.normals_split_custom_set(normals);_,_,nr,_=S.original_arrays(me)
report['original_corner_vs_native_roundtrip_max_delta']=float(np.linalg.norm(nr[np.array(control_loop)]-np.array(control_refs),axis=1).max())
report['max_uv_interpolation_error']=max_uv;uvtri=uv.reshape(-1,3,2).astype(float)
area=((uvtri[:,1,0]-uvtri[:,0,0])*(uvtri[:,2,1]-uvtri[:,0,1])-(uvtri[:,1,1]-uvtri[:,0,1])*(uvtri[:,2,0]-uvtri[:,0,0]))*.5
old_area=((old_uv[:,1,0]-old_uv[:,0,0])*(old_uv[:,2,1]-old_uv[:,0,1])-(old_uv[:,1,1]-old_uv[:,0,1])*(old_uv[:,2,0]-old_uv[:,0,0]))*.5
report['uv_negative_triangles']=int((area<-1e-12).sum());report['uv_zero_triangles']=int((abs(area)<1e-14).sum())
report['uv_area_partition_max_error']=float(abs(np.bincount(ids,weights=area,minlength=len(old_area))-old_area).max())
bpy.context.view_layer.update();report['bounds_max_delta_m']=float(abs(np.array(low.bound_box)-before_bounds).max());report['triangles_after']=P.triangles(low)
passed=(report['surface_distance_max_m']<=.0005 and report['surface_distance_p99_m']<=.00015 and report['original_corner_vs_native_roundtrip_max_delta']<=.0003 and max_uv<3e-5 and report['uv_negative_triangles']==0 and report['uv_zero_triangles']==0 and report['uv_area_partition_max_error']<1e-8 and report['bounds_max_delta_m']<1e-6)
report['status']='PASS_PENDING_ACTUAL_GRAY_REVIEW' if passed else 'REJECTED_KEEP_DENSE'
if passed:
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False);material=me.materials[0];me.materials[0]=S.B.clay_material()
    S.fixed_renders('reduced-gray-game-',(low,));me.materials[0]=material
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'source-candidate-reduced.blend'),compress=True,relative_remap=False)
    report['output']=P.identity(STAGE/'source-candidate-reduced.blend')
P.write_json(STAGE/'local-reduction.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
