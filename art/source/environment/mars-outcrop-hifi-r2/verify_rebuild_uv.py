"""Verify the handoff UV recipe against the delivered candidate, without replacing it."""
import bpy,bmesh,sys,json
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P;import finalize as F
bpy.ops.wm.open_mainfile(filepath=str(HERE/'stages/09-exposed-bed-material/source-high.blend'))
high=bpy.data.objects['HIGH_ParentSandstone'];game=B.collection('GAME_EXPORT')
low=P.clone_low(high,game,'MarsOutcrop',.24)
bm=bmesh.new();bm.from_mesh(low.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0002)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0002)
bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(low.data);bm.free();low.data.update()
for face in low.data.polygons:face.use_smooth=True
qa=P.unwrap(low);qa,repair=F.local_uv_repair(low,qa);F.uv_gate(low,qa)
actual=F.uv_identity(low);expected=json.loads((F.TEST/'test.json').read_text())['uv_identity']
assert actual==expected,{'expected':expected,'actual':actual}
P.write_json(HERE/'verification/rebuild-uv.json',{'status':'PASS','method':'Fresh clone/decimation and initial unwrap from frozen high, followed by the production local UV recipe','identity':actual})
print('REBUILD_UV_PASS='+json.dumps(actual),flush=True)
