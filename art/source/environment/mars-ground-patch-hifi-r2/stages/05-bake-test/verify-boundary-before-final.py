"""Read the actual supporting mesh edge, then compare native high and baked low borders."""
import bpy,json,bisect,hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
support=ROOT/'art/environment/mars-lookdev-r1/hifi/ground-with-patch-hole.blend'
bpy.ops.wm.open_mainfile(filepath=str(support))
ground=next(o for o in bpy.data.objects if o.type=='MESH' and 'regolith' in o.name.lower())
co=[tuple(ground.matrix_world@v.co) for v in ground.data.vertices];segments={}
for side,axis,value in [('left',0,0),('right',0,12),('front',1,-6),('back',1,6)]:
    other=1-axis
    segments[side]=sorted((c[other],c[2]) for c in co if abs(c[axis]-value)<1e-6 and (-6<=c[other]<=6 if axis==0 else 0<=c[other]<=12))
    assert len(segments[side])==7,(side,len(segments[side]))
checks=[]
for rel,name in [('stages/05-boundary-calibrated/ground-high.blend','HIGH_SandApron'),('stages/05-bake-test/prepared.blend','MarsSandApron')]:
    path=HERE/rel
    if not path.exists():continue
    bpy.ops.wm.open_mainfile(filepath=str(path));patch=bpy.data.objects[name];errors=[]
    for vert in patch.data.vertices:
        x,y,z=vert.co;x+=6
        side='left' if abs(x)<1e-6 else 'right' if abs(x-12)<1e-6 else 'front' if abs(y+6)<1e-6 else 'back' if abs(y-6)<1e-6 else None
        if side is None:continue
        rows=segments[side];t=y if side in ('left','right') else x
        i=min(max(1,bisect.bisect_left([a for a,b in rows],t)),len(rows)-1)
        a,za=rows[i-1];b,zb=rows[i];expected=za+(zb-za)*(t-a)/(b-a)
        errors.append((abs(z-expected),side,t,z-expected))
    maximum=max(errors)
    checks.append({'source':rel,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'boundary_vertices':len(errors),
        'max_separation_m':maximum[0],'worst_sample':maximum,'upward_face_min_z':min(p.normal.z for p in patch.data.polygons),
        'status':'PASS' if maximum[0]<1e-5 and min(p.normal.z for p in patch.data.polygons)>0 else 'FAIL'})
record={'support_source':str(support.relative_to(ROOT)),'support_sha256':hashlib.sha256(support.read_bytes()).hexdigest(),
        'method':'actual supporting mesh edge segments; native patch vertices at shared world transform','tolerance_m':1e-5,'checks':checks}
(HERE/'verification').mkdir(exist_ok=True);(HERE/'verification/boundary.json').write_text(json.dumps(record,indent=2))
print('RESULT_JSON='+json.dumps(record));assert all(c['status']=='PASS' for c in checks),record
