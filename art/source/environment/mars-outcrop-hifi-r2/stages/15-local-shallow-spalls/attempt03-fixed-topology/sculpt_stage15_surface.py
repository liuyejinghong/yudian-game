"""Stage15 re-plan: continuous local erosion on the existing tessellation only."""
import bpy,bmesh,sys,json,importlib.util
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
import pipeline as P
import sculpt_stage15 as S
STAGE=S.STAGE;BACKUP=STAGE/'attempt02-angular-peel'
spec=importlib.util.spec_from_file_location('previous_peel',BACKUP/'sculpt_stage15.py')
OLD=importlib.util.module_from_spec(spec);spec.loader.exec_module(OLD)

def exposure_depth(co,r):
    """Non-periodic 2–8 cm connected relief with a broad vanishing envelope."""
    fu,fv,w=S.local(co,r);a,b=r['half'];active=(abs(fu)<a)&(abs(fv)<b)&(abs(w)<.038)
    u=fu[active];v=fv[active];seed=r['seed']
    # Broad fades carry less slope energy than the varying interior. There is
    # no polygon cut, constant-depth floor, isolated pit, or narrow edge ridge.
    env=(1-B.smooth(a-.125,a,abs(u)))*(1-B.smooth(b-.145,b,abs(v)))
    env*=1-B.smooth(.018,.038,abs(w[active]))
    uw=u+.014*B.noise(u,v,u*0,9.3,seed+1)
    vw=v+.011*B.noise(u,v,u*0,11.1,seed+2)
    p=.94*uw+.34*vw;q=-.34*uw+.94*vw
    primary=B.noise(p*.78,q*1.18,u*0,27.5,seed+3)
    broken=B.noise(p*.94+.07,q*1.08-.11,u*0+.31,44.0,seed+4)
    merged=B.noise(uw,vw,u*0+.17,15.7,seed+5)
    field=.0038+.0030*primary+.00145*broken+.00065*merged
    # The bounds are guardrails; normal samples lie within the varying field.
    field=np.clip(field,.0006,.0080)
    depth=np.zeros(len(co));envelope=np.zeros(len(co))
    depth[active]=field*env;envelope[active]=env
    return depth,envelope,{'type':'continuous connected shallow erosion',
        'nominal_scales_m':[.02,.08],'nominal_depth_m':[.002,.006],
        'maximum_depth_m':.008,'edge_fade_width_m':[.125,.145],
        'direction':'weak layer alignment; warped non-periodic fields; no contour cut'}

def mask_arrays(me):
    data={}
    for name in ('DustMask','FreshFractureMask','BaseColorSource','RoughnessSource'):
        a=np.empty(len(me.vertices)*4,np.float32);me.color_attributes[name].data.foreach_get('color',a);data[name]=a.reshape(-1,4)
    return data

def mix(d,f):
    rock=np.array([.220,.195,.171]);fracture=np.array([.172,.162,.148]);sand=np.array([.283,.236,.193])
    return (rock*(1-f[:,None])+fracture*f[:,None])*(1-d[:,None])+sand*d[:,None]

def restore_source_masks(me,pre):
    data=mask_arrays(me);old_depth=np.zeros(len(pre))
    for r in S.REGIONS:old_depth=np.maximum(old_depth,OLD.exposure_depth(pre,r)[0])
    reveal=B.smooth(.0006,.0045,old_depth)
    nd=data['DustMask'][:,0].astype(float);nf=data['FreshFractureMask'][:,0].astype(float)
    d=nd/(1-.65*reveal);f=(nf-.25*reveal)/(1-.25*reveal)
    data['BaseColorSource'][:,:3]*=mix(d,f)/mix(nd,nf)
    data['RoughnessSource'][:,:3]+=(.14*(d-nd)+.025*(nf-f))[:,None]
    data['DustMask'][:,:3]=d[:,None];data['FreshFractureMask'][:,:3]=f[:,None]
    for name,array in data.items():me.color_attributes[name].data.foreach_set('color',array.ravel())

def new_source_masks(me,depth):
    data=mask_arrays(me);reveal=B.smooth(.0006,.0045,depth)
    d=data['DustMask'][:,0].copy();f=data['FreshFractureMask'][:,0].copy()
    nd=d*(1-.65*reveal);nf=f+(1-f)*.25*reveal
    data['BaseColorSource'][:,:3]*=mix(nd,nf)/mix(d,f)
    data['RoughnessSource'][:,:3]+=(-.14*(d-nd)-.025*(nf-f))[:,None]
    data['DustMask'][:,:3]=nd[:,None];data['FreshFractureMask'][:,:3]=nf[:,None]
    for name,array in data.items():me.color_attributes[name].data.foreach_set('color',array.ravel())

def retessellate_uv_slivers(bm):
    """Flip only a micro-sliver's internal diagonal within its original face.

    Native ear clipping sometimes connects the endpoints of a nearly collinear
    cut edge, producing one tiny negative-UV triangle. Joining that triangle to
    its sibling and choosing the other diagonal uses the inserted edge vertex
    correctly. Vertex positions, UV coordinates, and source-face area stay fixed.
    """
    uv=bm.loops.layers.uv.active;source=bm.faces.layers.int['stage15_original_face']
    def area(points):
        a,b,c=points;return float((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))*.5
    def face_area(f):return area([np.array(l[uv].uv,dtype=float) for l in f.loops])
    bad=[f for f in bm.faces if face_area(f)<-1e-12];records=[]
    for f in bad:
        if not f.is_valid:continue
        options=[]
        for edge in f.edges:
            neighbors=[g for g in edge.link_faces if g!=f and len(g.verts)==3 and g[source]==f[source]]
            if len(neighbors)!=1:continue
            g=neighbors[0];verts=set(f.verts)|set(g.verts)
            if len(verts)!=4:continue
            values={l.vert:np.array(l[uv].uv,dtype=float) for h in (f,g) for l in h.loops}
            a=next(v for v in f.verts if v not in edge.verts);b=next(v for v in g.verts if v not in edge.verts)
            tris=[(a,edge.verts[0],b),(a,b,edge.verts[1])]
            tris=[t if area([values[v] for v in t])>0 else tuple(reversed(t)) for t in tris]
            areas=[area([values[v] for v in t]) for t in tris]
            old_sum=face_area(f)+face_area(g)
            if min(areas)>1e-12 and abs(sum(areas)-old_sum)<1e-12:
                options.append((min(areas),edge,g,tris,values,old_sum))
        assert options,'No in-source-face diagonal repair for '+str(f[source])
        _,edge,g,tris,values,old_sum=max(options,key=lambda x:x[0])
        orig=f[source];material=f.material_index;smooth=f.smooth
        bm.faces.remove(f);bm.faces.remove(g)
        if edge.is_valid and not edge.link_faces:bm.edges.remove(edge)
        created=[]
        for tri in tris:
            nf=bm.faces.new(tri);nf[source]=orig;nf.material_index=material;nf.smooth=smooth
            for loop in nf.loops:loop[uv].uv=values[loop.vert]
            created.append(nf)
        records.append({'original_face':orig-1,'area_error':abs(sum(face_area(h) for h in created)-old_sum)})
    assert all(face_area(f)>=-1e-12 for f in bm.faces)
    return records

def reuse(o,is_game,original):
    me=o.data;before=P.triangles(o)
    pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage15_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
    if not is_game:restore_source_masks(me,pre)
    me.vertices.foreach_set('co',pre.ravel());me.update()
    bm=bmesh.new();bm.from_mesh(me);pos=bm.verts.layers.float_vector['stage15_original_position']
    face_source=bm.faces.layers.int['stage15_original_face'];bm.normal_update()
    # The six negative-UV micro-slivers are cut degeneracies, not relief. Clean
    # only the two prepared charts, in their original undisplaced coordinates.
    local_verts=set()
    for v in bm.verts:
        p=np.array(v[pos]);
        if any(abs(S.local(p,r)[0])<r['half'][0]+.055 and abs(S.local(p,r)[1])<r['half'][1]+.055 and abs(S.local(p,r)[2])<.045 for r in S.REGIONS):local_verts.add(v)
    local_edges=[e for e in bm.edges if all(v in local_verts for v in e.verts)]
    bmesh.ops.dissolve_degenerate(bm,dist=.00001,edges=local_edges)
    # The low mesh was all triangles before this operation; all its ngons are
    # inserted shared-edge corners. Native deterministic tessellation freezes it.
    if is_game:ngons=[f for f in bm.faces if len(f.verts)>3]
    else:ngons=[f for f in bm.faces if len(f.verts)>3 and all(v in local_verts for v in f.verts)]
    bmesh.ops.triangulate(bm,faces=ngons,quad_method='FIXED',ngon_method='EAR_CLIP')
    repaired=retessellate_uv_slivers(bm) if is_game else []
    bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table()
    pre=np.array([v[pos] for v in bm.verts],float);delta=np.zeros_like(pre);maxdepth=np.zeros(len(pre));details=[]
    for r in S.REGIONS:
        depth,envelope,definition=exposure_depth(pre,r);delta-=depth[:,None]*r['n'];maxdepth=np.maximum(maxdepth,depth)
        details.append({'name':r['name'],'definition':definition,'changed_vertices':int((depth>1e-8).sum()),'max_depth_m':float(depth.max())})
    for v,p in zip(bm.verts,pre+delta):v.co=p
    bm.normal_update()
    if is_game:
        old_tri,old_uv,old_nr=original;normals=[];uvlayer=bm.loops.layers.uv.active;max_uv=0
        for f in bm.faces:
            i=f[face_source]-1;a,b,c=old_tri[i];e0=b-a;e1=c-a;d00=e0@e0;d01=e0@e1;d11=e1@e1;den=d00*d11-d01*d01
            for loop in f.loops:
                p=np.array(loop.vert[pos]);dist=np.linalg.norm(old_tri[i]-p,axis=1)
                if dist.min()<1e-7:w=np.eye(3)[dist.argmin()]
                else:
                    dv=p-a;d20=dv@e0;d21=dv@e1;w1=(d11*d20-d01*d21)/den;w2=(d00*d21-d01*d20)/den;w=np.array([1-w1-w2,w1,w2])
                max_uv=max(max_uv,float(np.abs(w@old_uv[i]-np.array(loop[uvlayer].uv)).max()))
                value=w@old_nr[i]
                if np.linalg.norm(np.array(loop.vert.co)-p)>1e-7:value=np.array(loop.vert.normal)
                normals.append(value)
        assert max_uv<3e-5,max_uv
        bm.to_mesh(me);me.update();me.normals_split_custom_set(normals)
    else:bm.to_mesh(me);me.update()
    bm.free()
    if not is_game:new_source_masks(me,maxdepth)
    return {'triangles_before':before,'triangles_after':P.triangles(o),'new_subdivision':False,
        'local_degenerate_tolerance_m':.00001,'micro_sliver_diagonal_repairs':repaired,
        'exposures':details,'max_depth_m':float(maxdepth.max())}

def field_statistics():
    result=[]
    for r in S.REGIONS:
        a,b=r['half'];u,v=np.meshgrid(np.arange(-a,a,.001),np.arange(-b,b,.001));co=r['o']+u.ravel()[:,None]*r['u']+v.ravel()[:,None]*r['v']
        d,e,_=exposure_depth(co,r);grid=d.reshape(u.shape);gy,gx=np.gradient(grid,.001,.001);slope=np.hypot(gx,gy).ravel();inside=e>.8
        energy=slope[inside]**2;angle=np.degrees(np.arctan(slope[inside]));order=np.sort(energy)
        result.append({'region':r['name'],'sampling_step_m':.001,'interior_definition':'envelope > 0.8',
            'interior_depth_quantiles_m':np.quantile(d[inside],[0,.1,.5,.9,1]).tolist(),
            'interior_slope_degrees_quantiles':np.quantile(angle,[.1,.5,.9,.99]).tolist(),
            'interior_slope_below_1_degree_fraction':float(np.mean(angle<1)),
            'highest_10pct_interior_area_slope_energy_fraction':float(order[int(.9*len(order)):].sum()/order.sum())})
    return result

def run():
    identities={str(p.relative_to(B.ROOT)):P.identity(p) for p in (S.SOURCE,HERE/'mars-outcrop-hifi-r2.glb')}
    bpy.ops.wm.open_mainfile(filepath=str(S.SOURCE));low=bpy.data.objects['MarsOutcrop'];co,vi,nr,uv=S.original_arrays(low.data)
    original=(co[vi.reshape(-1,3)].astype(float),uv.reshape(-1,3,2).astype(float),nr.reshape(-1,3,3).astype(float))
    bounds={o.name:np.array(o.bound_box) for o in (bpy.data.objects['HIGH_ParentSandstone'],low)}
    bpy.ops.wm.open_mainfile(filepath=str(BACKUP/'source-candidate.blend'));high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    report={'method':'architect continuous shallow-surface re-plan','field_statistics':field_statistics()}
    print('FIELD_STATS='+json.dumps(report),flush=True)
    report['high']=reuse(high,False,original);print('HIGH_DONE',flush=True)
    report['game']=reuse(low,True,original);print('GAME_DONE',flush=True)
    bpy.context.view_layer.update();assert all(np.max(abs(np.array(o.bound_box)-bounds[o.name]))<1e-6 for o in (high,low))
    for o in (high,low):o.hide_set(False);o.hide_render=False
    highmat=high.data.materials[0];lowmat=low.data.materials[0];clay=B.clay_material()
    high.data.materials[0]=clay;low.hide_render=True;S.fixed_renders('gray-high-',(high,))
    high.hide_render=True;low.hide_render=False;low.data.materials[0]=clay;S.fixed_renders('gray-game-',(low,))
    high.data.materials[0]=highmat;low.data.materials[0]=lowmat;high.hide_render=False;low.hide_render=True;S.fixed_renders('source-pbr-',(high,))
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    # The source's packed legacy images keep working when the candidate reopens.
    for im in bpy.data.images:
        if im.filepath.endswith(('/normal.png','/basecolor.png','/roughness.png')):im.filepath='//../../textures/'+im.filepath.rsplit('/',1)[-1]
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'source-candidate.blend'),compress=True,relative_remap=False)
    for name,identity in identities.items():assert P.identity(B.ROOT/name)==identity
    report['status']='PENDING_ACTUAL_GRAY_AND_SOURCE_REVIEW_BEFORE_4K_BAKE';report['formal_identities']=identities
    P.write_json(STAGE/'stage.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
if __name__=='__main__':run()
