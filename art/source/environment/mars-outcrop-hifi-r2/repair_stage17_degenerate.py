"""Only weld proven duplicate UV edges <= one observed float ULP.
The accepted shape and original candidate remain untouched on disk.
"""
import bpy,bmesh,sys,json
from pathlib import Path
import numpy as np
h=Path(__file__).resolve().parent;sys.path.insert(0,str(h));import surface_stage17 as S
stage=S.STAGE;src=stage/'source-candidate.blend';out=stage/'source-clean.blend';identity=S.P.identity(src);formal=S.P.identity(S.SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(S.SOURCE));orig=bpy.data.objects['MarsOutcrop'];oc,ov,on,ou=S.arrays(orig.data);original=(oc[ov.reshape(-1,3)].astype(float),ou.reshape(-1,3,2).astype(float),on.reshape(-1,3,3).astype(float),on.reshape(-1,3,3).astype(float))
bpy.ops.wm.open_mainfile(filepath=str(src));o=bpy.data.objects['MarsOutcrop'];m=o.data;before=S.P.triangles(o);bounds=np.array(o.bound_box)
bm=bmesh.new();bm.from_mesh(m);bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table();uv=bm.loops.layers.uv.active;pos=bm.verts.layers.float_vector['stage17_original_position'];rid=bm.faces.layers.int['stage17_face_region']
assert np.array_equal(np.array([v[pos] for v in list(bm.verts)[:len(oc)]],np.float32),oc),'Original vertex prefix not retained'
def area(f):
 a,b,c=[np.array(l[uv].uv,dtype=float) for l in f.loops];return float(np.cross(b-a,c-a))*.5
bad=[f for f in bm.faces if abs(area(f))<1e-14];assert len(bad)==1750 and all(area(f)==0 and f[rid]>0 for f in bad)
limit=5.960464477539063e-8;edges=set()
for f in bad:
 for e in f.edges:
  if e.calc_length()>limit:continue
  same=True
  for loop in e.link_loops:
   same &= (loop[uv].uv==loop.link_loop_next[uv].uv)
  if same:edges.add(e)
assert edges
parent={v:v for e in edges for v in e.verts}
def root(v):
 while parent[v]!=v:v=parent[v]
 return v
for e in edges:
 a,b=[root(v) for v in e.verts]
 if a==b:continue
 # Original vertices precede every inserted vertex; prefer them, then stable index.
 keep,drop=sorted((a,b),key=lambda v:(v.index>=len(oc),v.index));parent[drop]=keep
mapping={v:root(v) for v in parent if root(v)!=v};dist=[(v.co-k.co).length for v,k in mapping.items()]
assert max(dist)<=limit,('Duplicate component exceeds authorized distance',max(dist))
assert not any(v.index<len(oc) for v in mapping),'Would remove an original vertex'
records=[{'from':v.index,'to':k.index,'distance_m':(v.co-k.co).length} for v,k in mapping.items()]
bmesh.ops.weld_verts(bm,targetmap=mapping);assert all(len(f.verts)==3 for f in bm.faces)
areas=np.array([area(f) for f in bm.faces]);assert (areas>1e-14).all(),(int((areas<=0).sum()),int((areas<1e-14).sum()))
bm.normal_update();bm.to_mesh(m);bm.free();m.update();normals=S.set_low_normals(m,original)
bpy.context.view_layer.update();assert np.array_equal(np.array(o.bound_box),bounds)
assert not any(e.use_edge_sharp for e in m.edges)
report={'status':'LOCAL_DUPLICATE_EDGE_REPAIR_PENDING_INDEPENDENT_PRESERVATION','input':identity,'threshold_m':limit,'selected_duplicate_edges':len(edges),'merged_inserted_vertices':len(mapping),'removed_original_vertices':0,'max_merge_distance_m':max(dist),'triangles_before':before,'triangles_after':S.P.triangles(o),'removed_degenerate_triangles':before-S.P.triangles(o),'uv_zero_area_faces_after':int((abs(areas)<1e-14).sum()),'uv_negative_faces_after':int((areas<0).sum()),'bounds_max_delta':0,'raw_original_normal_diagnostic':{'reference':'Raw original corner vectors before native custom-normal encoding; not the same-state preservation gate','matched_corner_count':normals['matched_untouched_original_corner_count'],'max_delta':normals['native_roundtrip_corner_max_delta'],'p99_delta':normals['native_roundtrip_corner_p99_delta']},'merges':records}
bpy.ops.wm.save_as_mainfile(filepath=str(out),compress=True,relative_remap=False);report['output']=S.P.identity(out)
assert S.P.identity(src)==identity and S.P.identity(S.SOURCE)==formal
S.P.write_json(stage/'degenerate-repair.json',report);print('RESULT_JSON='+json.dumps({k:v for k,v in report.items() if k!='merges'}),flush=True)
