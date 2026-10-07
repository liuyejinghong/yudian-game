"""Independent preservation proof in the candidate's actual encoding state.
The old-topology and zero-displacement diagnostics remain historical evidence.
No asset is changed or saved by this verifier.
"""
import bpy,json,sys
import numpy as np
from pathlib import Path
here=Path(__file__).resolve().parent;sys.path.insert(0,str(here))
import surface_stage17 as S
stage=S.STAGE;source_identity=S.P.identity(S.SOURCE);candidate_identity=S.P.identity(stage/'source-candidate.blend')
bpy.ops.wm.open_mainfile(filepath=str(S.SOURCE));original=bpy.data.objects['MarsOutcrop'];m=original.data
oc,ov,on,ou=S.arrays(m);tri=oc[ov.reshape(-1,3)].astype(float);uv0=ou.reshape(-1,3,2).astype(float);nr0=on.reshape(-1,3,3).astype(float)
regions,_=S.actual_face_labels(m);bounds=np.array(original.bound_box);old_sharp=np.array([e.use_edge_sharp for e in m.edges]);old_smooth=np.array([f.use_smooth for f in m.polygons])
assert not old_sharp.any() and old_smooth.all(),'Initial hard-edge/smooth state differs from known input'
bpy.ops.wm.open_mainfile(filepath=str(stage/'source-candidate.blend'));ob=bpy.data.objects['MarsOutcrop'];me=ob.data
co,vi,nr,uv=S.arrays(me);pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage17_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
ids=np.empty(len(me.polygons),np.int32);me.attributes['stage17_original_face'].data.foreach_get('value',ids);ids-=1
assert min(ids)>=0 and max(ids)<len(tri)
sizes=np.array([len(f.vertices) for f in me.polygons]);assert (sizes==3).all();lf=np.repeat(ids,3);region=regions[ids];lr=np.repeat(region,3)
stored_region=np.empty(len(region),np.int32);me.attributes['stage17_face_region'].data.foreach_get('value',stored_region);assert np.array_equal(stored_region,region)
changed=np.linalg.norm(co.astype(float)-pre,axis=1)>1e-7
# A loop on an unselected face can still share a fan with the permitted face.
vertex_allowed=np.zeros(len(co),bool);vertex_allowed[vi[lr>0]]=True
face_deformed=changed[vi.reshape(-1,3)].any(1)
vertex_fan_deformed=np.zeros(len(co),bool);vertex_fan_deformed[vi[np.repeat(face_deformed,3)]]=True
strict_outside=(lr==0)&~vertex_allowed[vi]&~vertex_fan_deformed[vi]
fan_margin=(lr==0)&~strict_outside
vertex_normal=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('normal',vertex_normal);vertex_normal=vertex_normal.reshape(-1,3)
target=np.empty((len(vi),3),float);match=np.zeros(len(vi),bool);nearest=np.empty(len(vi),np.int8);uv_error=0.;bary_error=0.;original_target_error=0.
for start in range(0,len(vi),100000):
 end=min(start+100000,len(vi));f=lf[start:end];p=pre[vi[start:end]].astype(float);a,b,c=tri[f,0],tri[f,1],tri[f,2]
 e0=b-a;e1=c-a;d=p-a;cross=np.cross(e0,e1);norm=(cross*cross).sum(1)
 assert (norm>0).all()
 # Cross-product barycentrics are independent of the author's dot-product solve.
 w1=(np.cross(d,e1)*cross).sum(1)/norm;w2=(np.cross(e0,d)*cross).sum(1)/norm;w=np.column_stack((1-w1-w2,w1,w2))
 dist=np.linalg.norm(tri[f]-p[:,None,:],axis=2);which=dist.argmin(1);same=dist.min(1)<1e-7;w[same]=np.eye(3)[which[same]]
 match[start:end]=same;nearest[start:end]=which
 expected=(nr0[f]*w[:,:,None]).sum(1)
 original_target_error=max(original_target_error,float(np.max(abs(expected[same]-nr0[f[same],which[same]]),initial=0)))
 bary_error=max(bary_error,float(np.max(abs((tri[f]*w[:,:,None]).sum(1)-p))))
 uv_error=max(uv_error,float(np.max(abs((uv0[f]*w[:,:,None]).sum(1)-uv[start:end]))))
 moved=changed[vi[start:end]];expected[moved]=vertex_normal[vi[start:end][moved]];target[start:end]=expected
assert original_target_error==0 and uv_error<3e-5 and bary_error<3e-5
outside_original=strict_outside&match
outside_original_target_error=float(np.max(abs(target[outside_original]-nr0[lf[outside_original],nearest[outside_original]]),initial=0))
outside_coordinate_error=float(np.linalg.norm(co[vi[strict_outside]].astype(float)-pre[vi[strict_outside]],axis=1).max(initial=0))
assert outside_original_target_error==0 and outside_coordinate_error==0
sharp=np.array([e.use_edge_sharp for e in me.edges]);smooth=np.array([f.use_smooth for f in me.polygons])
assert not sharp.any() and smooth.all(),'Hard-edge or smooth state changed'
# The independent encoding uses the candidate coordinates and topology, with
# exactly the original initial hard-edge state (all smooth, zero hard edges).
control=me.copy();control.edges.foreach_set('use_edge_sharp',np.zeros(len(control.edges),bool));control.polygons.foreach_set('use_smooth',np.ones(len(control.polygons),bool));control.update()
control.normals_split_custom_set(target)
_,_,encoded,_=S.arrays(control);delta=np.linalg.norm(nr.astype(float)-encoded.astype(float),axis=1)
assert not any(e.use_edge_sharp for e in control.edges)
area=lambda t:np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0])*.5
uv_area=area(uv.reshape(-1,3,2).astype(float));original_area=area(uv0);partition=np.bincount(ids,weights=uv_area,minlength=len(original_area))
legacy=json.loads((stage/'same-topology-normal-control.json').read_text());legacy_worst=[]
for entry in legacy['worst']:
 i=entry['loop'];legacy_worst.append({**entry,'original_face_region':int(lr[i]),'vertex_touches_permitted_face':bool(vertex_allowed[vi[i]]),'fan_touches_displaced_face':bool(vertex_fan_deformed[vi[i]]),'strict_outside':bool(strict_outside[i]),'same_state_control_delta':float(delta[i])})
# Recreate the full 35-corner classification once, using the saved same-topology
# experiment's definition; this is diagnostic only and does not replace this gate.
report={'status':'PASS','source':source_identity,'candidate':candidate_identity,'triangles':len(ids),
 'scope_counts':{'permitted_face_corners':int((lr>0).sum()),'outside_faces_on_permitted_or_deformed_fans':int(fan_margin.sum()),'strict_outside_corners':int(strict_outside.sum()),'strict_outside_original_corners':int(outside_original.sum())},
 'input_target_truth':{'strict_outside_original_corner_max_delta':outside_original_target_error,'new_corner_rule':'Original-face barycentric interpolation; independently solved by cross products','max_barycentric_position_error':bary_error,'strict_outside_position_delta_m':outside_coordinate_error,'original_sharp_edges':int(old_sharp.sum()),'candidate_sharp_edges':int(sharp.sum()),'original_and_candidate_all_faces_smooth':bool(old_smooth.all() and smooth.all())},
 'same_state_encoding_control':{'native':'5.2.2 LTS','same_coordinates_topology_initial_hard_edges':True,'threshold':.0003,'all_corner_max_delta':float(delta.max()),'strict_outside_corner_max_delta':float(delta[strict_outside].max()),'strict_outside_p99_delta':float(np.quantile(delta[strict_outside],.99)),'over_threshold_all':int((delta>.0003).sum()),'over_threshold_strict_outside':int((delta[strict_outside]>.0003).sum())},
 'uv':{'max_interpolation_error':uv_error,'max_original_face_area_partition_error':float(abs(partition-original_area).max()),'flipped_faces':int((uv_area<0).sum()),'zero_area_faces':int((abs(uv_area)<1e-14).sum()),'non_triangular_polygons':int((sizes!=3).sum())},
 'bounds_max_delta':float(abs(np.array(ob.bound_box)-bounds).max()),
 'legacy_diagnostics_retained':{'old_topology_max_delta':.0006016109904149607,'zero_displacement_same_topology_max_delta':legacy['candidate_vs_unmoved_same_topology_max_delta'],'zero_displacement_over_threshold_count':legacy['over_threshold'],'zero_displacement_worst_positions_classified':legacy_worst,'meaning':'Both earlier deltas remain recorded; a zero-displacement control also changes the geometric fan basis, so it is not the same encoding state.'}}
if delta[strict_outside].max()>.0003 or delta.max()>.0003 or (uv_area<0).any() or (abs(uv_area)<1e-14).any() or abs(partition-original_area).max()>1e-8 or report['bounds_max_delta']!=0:report['status']='REWORK'
assert S.P.identity(S.SOURCE)==source_identity and S.P.identity(stage/'source-candidate.blend')==candidate_identity
S.P.write_json(stage/'same-state-preservation.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
assert report['status']=='PASS'
