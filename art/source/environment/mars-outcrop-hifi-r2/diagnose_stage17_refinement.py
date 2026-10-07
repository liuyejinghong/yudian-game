"""Single read-only refinement comparison; no asset save or decimation."""
import bpy,bmesh,inspect,sys,json
from pathlib import Path
import numpy as np
here=Path(__file__).resolve().parent;sys.path.insert(0,str(here))
import surface_stage17 as S
bpy.ops.wm.open_mainfile(filepath=str(S.SOURCE));high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
co,vi,nr,uv=S.arrays(low.data);old=(co[vi.reshape(-1,3)].astype(float),uv.reshape(-1,3,2).astype(float),nr.reshape(-1,3,3).astype(float))
low.data.normals_split_custom_set(nr.tolist());_,_,control,_=S.arrays(low.data);original=(*old,control.reshape(-1,3,3).astype(float))
trees,faces=S.physical_boundaries(high)
src=inspect.getsource(S.refine_face).replace("        if is_game:bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3],quad_method='FIXED',ngon_method='EAR_CLIP')\n",'')
src=src.replace("    repaired=repair_slivers(bm) if is_game else []","    if is_game:bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3],quad_method='FIXED',ngon_method='EAR_CLIP')\n    repaired=repair_slivers(bm) if is_game else []")
exec(src,S.__dict__);report=S.refine_face(low,True,trees,original)
S.P.write_json(S.STAGE/'triangulate-once-diagnostic.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
