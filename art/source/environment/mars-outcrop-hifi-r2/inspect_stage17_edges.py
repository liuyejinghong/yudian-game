import bpy,json
from pathlib import Path
h=Path(__file__).resolve().parent
for p in (h/'mars-outcrop-hifi-r2.blend',h/'stages/17-continuous-face-response/source-candidate.blend'):
 bpy.ops.wm.open_mainfile(filepath=str(p));m=bpy.data.objects['MarsOutcrop'].data
 print('MESH_EDGES='+json.dumps({'file':p.name,'edges':len(m.edges),'sharp':sum(e.use_edge_sharp for e in m.edges),'nonsmooth_faces':sum(not f.use_smooth for f in m.polygons),'sharp_attrs':[(a.name,a.data_type,a.domain) for a in m.attributes if 'sharp' in a.name]}),flush=True)
