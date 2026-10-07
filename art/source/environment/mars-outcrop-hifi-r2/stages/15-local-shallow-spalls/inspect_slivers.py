import bpy,bmesh,json
from pathlib import Path
p=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(p/'source-candidate.blend'))
bm=bmesh.new();bm.from_mesh(bpy.data.objects['MarsOutcrop'].data);uv=bm.loops.layers.uv.active;source=bm.faces.layers.int['stage15_original_face'];bm.faces.ensure_lookup_table();bm.verts.ensure_lookup_table()
def area(f):
 a,b,c=[l[uv].uv for l in f.loops];return ((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5
bad=[]
for f in bm.faces:
 if area(f)>=-1e-12:continue
 bad.append({'face':f.index,'source':f[source]-1,'area':area(f),'coords':[list(v.co) for v in f.verts],'uv':[list(l[uv].uv) for l in f.loops],
  'edges':[{'length':e.calc_length(),'neighbor_source':[g[source]-1 for g in e.link_faces if g!=f],'neighbor_uv_area':[area(g) for g in e.link_faces if g!=f]} for e in f.edges]})
(p/'sliver-locations.json').write_text(json.dumps(bad,indent=2)+'\n');print('RESULT_JSON='+json.dumps(bad))
