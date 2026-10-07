import bpy,sys,json
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parents[2];sys.path.insert(0,str(HERE))
import pipeline as P
stage=HERE/'stages/10-bake-test';bpy.ops.wm.open_mainfile(filepath=str(stage/'prepared.blend'))
low=bpy.data.objects['MarsOutcrop'];qa=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=12)
a=P.U._Arrays(low);labels,_=P.U._uv_islands(a);tris,tf=P.U._tris(a,a.uv);rows=[]
for k in qa['self_overlapping_islands']:
    mask=labels==k;f=np.nonzero(mask)[0];verts=np.unique(a.lv[np.isin(a.lf,f)]);co=a.co[verts]
    rows.append({'island':k,'faces':len(f),'bounds_min':co.min(0).tolist(),'bounds_max':co.max(0).tolist(),
        'self_overlap_island_normalized':P.U._self_overlap(a.uv,tris,tf,mask,res=1024)})
a.free();P.write_json(stage/'uv-overlap-location.json',{'qa_1024':qa,'locations':rows})
print('UV_LOCATIONS='+json.dumps(rows))
