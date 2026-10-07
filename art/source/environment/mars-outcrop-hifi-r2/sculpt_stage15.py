"""Two local shallow spall exposures; retain the approved macro form and UV atlas."""
import bpy,bmesh,json,sys,math,hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
import pipeline as P

STAGE=HERE/'stages/15-local-shallow-spalls'
SOURCE=HERE/'mars-outcrop-hifi-r2.blend'
# Both centers are inside the real faces found from the fixed Godot camera rays.
REGIONS=[dict(name='right_cut_exposure',center=(1.406704,-.561279,.604110),
              normal=(.307580,-.605237,.734223),half=(.36,.39),seed=15031),
         dict(name='rear_old_exposure',center=(-1.05,1.50034,.94),
              normal=(-.194,1,.065),half=(.275,.43),seed=15079)]
for r in REGIONS:
    r['n']=np.array(r['normal'],float);r['n']/=np.linalg.norm(r['n'])
    r['u']=np.cross(r['n'],(0,0,1));r['u']/=np.linalg.norm(r['u'])
    # Give both charts an upward second coordinate; handedness is immaterial.
    r['v']=np.cross(r['u'],r['n']);r['v']/=np.linalg.norm(r['v'])
    r['o']=np.array(r['center'],float)

def local(co,r):
    d=co-r['o'];return d@r['u'],d@r['v'],d@r['n']

def polygon_distance(u,v,points):
    """Signed distance to an authored concave peel boundary, negative inside."""
    inside=np.zeros(len(u),bool);distance=np.full(len(u),np.inf)
    for (ax,ay),(bx,by) in zip(points,points[1:]+points[:1]):
        dx=bx-ax;dy=by-ay
        t=np.clip(((u-ax)*dx+(v-ay)*dy)/(dx*dx+dy*dy),0,1)
        distance=np.minimum(distance,np.hypot(u-ax-t*dx,v-ay-t*dy))
        crossing=((ay>v)!=(by>v)) & (u<(bx-ax)*(v-ay)/(by-ay+1e-30)+ax)
        inside^=crossing
    return np.where(inside,-distance,distance)


def exposure_depth(co,r):
    """One directional peel with a few unequal connected fracture steps.
    The intact face is retained around it. One end returns gradually to the
    old surface, so it is not a closed crater or an isolated scatter of pits.
    """
    full_u,full_v,w=local(co,r)
    a,b=r['half'];active=(np.abs(full_u)<a)&(np.abs(full_v)<b)&(np.abs(w)<.038)
    u=full_u[active];v=full_v[active]
    if r['name']=='right_cut_exposure':
        outline=[(-.30,-.14),(-.26,-.19),(-.20,-.18),(-.14,-.12),(-.09,-.13),
            (-.045,-.07),(0,-.04),(.09,-.04),(.14,0),(.20,.035),(.26,.115),
            (.21,.14),(.16,.125),(.115,.085),(.08,.10),(.02,.056),(-.015,.07),
            (-.075,.015),(-.14,.03),(-.205,-.035),(-.24,-.035)]
        deeper=[(-.205,-.113),(-.15,-.067),(-.12,-.075),(-.077,-.02),
            (-.018,-.021),(.042,.030),(.057,.070),(.018,.052),(-.033,.015),
            (-.087,.020),(-.151,-.026),(-.177,-.012),(-.232,-.082)]
        final_loss=[(-.081,-.032),(-.026,-.035),(.012,-.004),(.067,-.006),
            (.103,.019),(.091,.040),(.040,.018),(.010,.026),(-.043,-.002)]
        margin=.0036
        base=np.clip(.0047+.0030*u-.0018*v,.0033,.0059)
        open_end=1-B.smooth(.135,.263,u)
        depth=base+.00225*(1-B.smooth(-.0032,0,polygon_distance(u,v,deeper)))
        depth+=.00115*(1-B.smooth(-.0030,0,polygon_distance(u,v,final_loss)))
        # One unlost tongue is attached to the upper boundary, not a floating island.
        remnant=[(-.164,-.001),(-.117,-.003),(-.089,.019),(-.070,.014),
                 (-.091,.042),(-.147,.040),(-.182,.018)]
        depth*=1-.77*(1-B.smooth(-.0025,0,polygon_distance(u,v,remnant)))
    else:
        outline=[(-.10,-.33),(-.055,-.29),(-.08,-.23),(-.045,-.18),(-.06,-.105),
            (-.012,-.07),(-.035,0),(.02,.045),(.02,.13),(.07,.16),(.10,.23),
            (.085,.29),(.13,.31),(.18,.26),(.14,.20),(.15,.13),(.10,.09),
            (.11,.035),(.064,-.035),(.08,-.09),(.04,-.14),(.06,-.21),
            (.012,-.25),(.01,-.32),(-.045,-.38)]
        deeper=[(-.040,-.274),(-.008,-.222),(-.019,-.180),(.013,-.128),
            (.004,-.083),(.045,-.035),(.029,.014),(.073,.066),(.092,.099),
            (.078,.118),(.039,.082),(.017,.022),(-.014,-.007),(.001,-.055),
            (-.031,-.101),(-.020,-.146),(-.052,-.191)]
        final_loss=[(.028,.02),(.062,.031),(.081,.063),(.067,.093),
            (.083,.130),(.110,.143),(.130,.192),(.110,.213),(.071,.171),
            (.070,.141),(.047,.120),(.054,.076),(.027,.050)]
        margin=.0038
        base=np.clip(.0040+.0023*v-.0015*u,.0029,.0053)
        open_end=B.smooth(-.38,-.23,v)
        depth=base+.0025*(1-B.smooth(-.0033,0,polygon_distance(u,v,deeper)))
        depth+=.0015*(1-B.smooth(-.0034,0,polygon_distance(u,v,final_loss)))
        remnant=[(-.042,-.085),(-.004,-.060),(-.015,-.027),(.011,-.001),
                 (-.016,.015),(-.047,-.026),(-.061,-.068)]
        depth*=1-.68*(1-B.smooth(-.003,0,polygon_distance(u,v,remnant)))
    envelope=(1-B.smooth(-margin,0,polygon_distance(u,v,outline)))*open_end
    envelope*=1-B.smooth(.018,.038,np.abs(w[active]))
    full_depth=np.zeros(len(co));full_envelope=np.zeros(len(co))
    full_depth[active]=np.minimum(depth*envelope,.0090);full_envelope[active]=envelope
    definitions=[{'type':'single connected shallow peel','outline_m':outline,
        'edge_width_m':margin,'maximum_depth_m':.009,'open_end':True},
        {'type':'unequal connected 2–8 cm fracture-edge steps','regions':[deeper,final_loss]},
        {'type':'one attached remnant tongue','outline_m':remnant}]
    return full_depth,full_envelope,definitions

def original_arrays(me):
    co=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('co',co)
    ids=np.empty(len(me.loops),np.int32);me.loops.foreach_get('vertex_index',ids)
    normals=np.empty(len(me.corner_normals)*3,np.float32);me.corner_normals.foreach_get('vector',normals)
    uv=np.empty(len(me.uv_layers.active.data)*2,np.float32) if me.uv_layers else None
    if uv is not None:me.uv_layers.active.data.foreach_get('uv',uv)
    return co.reshape(-1,3),ids,normals.reshape(-1,3),None if uv is None else uv.reshape(-1,2)

def refine(o,is_game):
    """Bisect only the two interior charts, then subdivide with UV interpolation."""
    me=o.data;before_tri=P.triangles(o);old_co,old_ids,old_normals,old_uv=original_arrays(me)
    if is_game:
        assert all(len(p.vertices)==3 for p in me.polygons)
        old_tri=old_co[old_ids.reshape(-1,3)].copy()
        old_uv_tri=old_uv.reshape(-1,3,2).copy()
        old_normal_tri=old_normals.reshape(-1,3,3).copy()
    bm=bmesh.new();bm.from_mesh(me)
    face_source=bm.faces.layers.int.new('stage15_original_face')
    original_position=bm.verts.layers.float_vector.new('stage15_original_position')
    original_vertex=bm.verts.layers.int.new('stage15_original_vertex')
    for index,v in enumerate(bm.verts):v[original_position]=v.co;v[original_vertex]=index+1
    for index,f in enumerate(bm.faces):f[face_source]=index+1
    target=.0065 if is_game else .0045
    counts=[]
    for r in REGIONS:
        print('LOCAL_REFINE_BEGIN',o.name,r['name'],len(bm.faces),flush=True)
        half=np.array(r['half'])+.045
        def relevant(face,contained=False):
            co=np.array([v.co for v in face.verts]);u,v,w=local(co,r)
            if max(abs(w))>.040 or np.dot(face.normal,r['n'])<.92:return False
            if contained:return np.max(np.abs(u))<=half[0]+2e-6 and np.max(np.abs(v))<=half[1]+2e-6
            return np.min(u)<=half[0] and np.max(u)>=-half[0] and np.min(v)<=half[1] and np.max(v)>=-half[1]
        for axis,extent in ((r['u'],half[0]),(r['v'],half[1])):
            for sign in (-1,1):
                faces=[f for f in bm.faces if relevant(f)]
                geom=set(faces)
                for f in faces:geom.update(f.edges);geom.update(f.verts)
                bmesh.ops.bisect_plane(bm,geom=list(geom),dist=1e-7,
                    plane_co=Vector(r['o']+axis*extent*sign),plane_no=Vector(axis),
                    clear_inner=False,clear_outer=False,use_snap_center=False)
                bm.normal_update()
        faces=[f for f in bm.faces if relevant(f,True)]
        bmesh.ops.triangulate(bm,faces=faces);bm.normal_update()
        rounds=[]
        for step in range(12):
            faces=[f for f in bm.faces if relevant(f,True)]
            edges={e for f in faces for e in f.edges if e.calc_length()>target}
            if not edges:break
            rounds.append({'round':step,'split_edges':len(edges),'max_length_m':max(e.calc_length() for e in edges)})
            print('LOCAL_SUBDIVIDE',o.name,r['name'],step,len(edges),flush=True)
            result=bmesh.ops.subdivide_edges(bm,edges=list(edges),cuts=1,use_grid_fill=True,use_single_edge=True)
            modified={x for x in result['geom_inner']+result['geom_split'] if isinstance(x,bmesh.types.BMFace)}
            # Shared edge splits can add polygon corners just outside the rectangle.
            modified.update(f for e in edges if e.is_valid for f in e.link_faces)
            bmesh.ops.triangulate(bm,faces=[f for f in modified if f.is_valid and len(f.verts)>3])
            bm.normal_update()
        else:raise AssertionError('Local refinement did not converge')
        counts.append({'region':r['name'],'rounds':rounds,'final_local_faces':len(faces)})
        print('LOCAL_REFINE_END',o.name,r['name'],len(faces),flush=True)
    bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table()
    pre=np.array([v.co for v in bm.verts],np.float64);delta=np.zeros_like(pre);depth_max=np.zeros(len(pre))
    details=[]
    for r in REGIONS:
        print('LOCAL_DISPLACE',o.name,r['name'],len(pre),flush=True)
        depth,envelope,fragments=exposure_depth(pre,r)
        delta-=depth[:,None]*r['n'][None,:];depth_max=np.maximum(depth_max,depth)
        details.append({'region':r['name'],'changed_vertices':int((depth>1e-8).sum()),
            'max_depth_m':float(depth.max()),'fragment_definitions':fragments})
    assert np.linalg.norm(delta,axis=1).max()<=.0100001
    for v,new in zip(bm.verts,pre+delta):v.co=new
    bm.normal_update()
    if is_game:
        # Keep every untouched original corner normal. Interpolate the original
        # normal field on added flat corners; use real surface normals only locally.
        normals=[];face_orig=[];pre_corners=[];uv_corners=[];uv_layer=bm.loops.layers.uv.active
        for f in bm.faces:
            idx=f[face_source]-1;assert 0<=idx<len(old_tri)
            face_orig.append(idx)
            for loop in f.loops:
                p=np.array(loop.vert[original_position]);pre_corners.append(p)
                uv_corners.append(np.array(loop[uv_layer].uv))
                a,b,c=old_tri[idx];v0=b-a;v1=c-a;v2=p-a
                distances=np.linalg.norm(old_tri[idx]-p,axis=1)
                if distances.min()<1e-7:
                    weights=np.eye(3)[distances.argmin()]
                else:
                    d00=v0@v0;d01=v0@v1;d11=v1@v1;den=d00*d11-d01*d01
                    assert abs(den)>1e-26
                    w1=(d11*(v2@v0)-d01*(v2@v1))/den;w2=(d00*(v2@v1)-d01*(v2@v0))/den
                    weights=np.array([1-w1-w2,w1,w2])
                assert weights.min()>-.025 and weights.max()<1.025,(idx,weights)
                expected_uv=weights@old_uv_tri[idx]
                assert np.max(np.abs(expected_uv-np.array(loop[uv_layer].uv)))<3e-5
                old=weights@old_normal_tri[idx]
                # No broad normal recalculation: only vertices displaced by the new loss.
                displacement=np.linalg.norm(np.array(loop.vert.co)-p)
                if displacement>1e-7:old=np.array(loop.vert.normal)
                normals.append(old)
        bm.to_mesh(me);me.update();me.normals_split_custom_set(normals)
    else:
        bm.to_mesh(me);me.update()
    bm.free()
    # Color-mask changes only expose the actual freshly eroded surface.
    if not is_game:
        attrs=me.color_attributes
        dust=np.empty(len(me.vertices)*4,np.float32);fresh=np.empty_like(dust);base=np.empty_like(dust);rough=np.empty_like(dust)
        for name,array in [('DustMask',dust),('FreshFractureMask',fresh),('BaseColorSource',base),('RoughnessSource',rough)]:
            attrs[name].data.foreach_get('color',array)
        dust=dust.reshape(-1,4);fresh=fresh.reshape(-1,4);base=base.reshape(-1,4);rough=rough.reshape(-1,4)
        # BMesh preserves vertex order through to_mesh; verify the saved positions.
        now=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('co',now)
        assert np.max(np.abs(now.reshape(-1,3)-(pre+delta)))<2e-6
        reveal=B.smooth(.0006,.0045,depth_max)
        d=dust[:,0].copy();f=fresh[:,0].copy();nd=d*(1-.65*reveal);nf=f+(1-f)*.25*reveal
        rock=np.array([.220,.195,.171]);fracture=np.array([.172,.162,.148]);sand=np.array([.283,.236,.193])
        oldmix=(rock*(1-f[:,None])+fracture*f[:,None])*(1-d[:,None])+sand*d[:,None]
        newmix=(rock*(1-nf[:,None])+fracture*nf[:,None])*(1-nd[:,None])+sand*nd[:,None]
        base[:,:3]*=newmix/oldmix;dust[:,:3]=nd[:,None];fresh[:,:3]=nf[:,None]
        rough[:,:3]+=(-.14*(d-nd)-.025*(nf-f))[:,None]
        for name,array in [('DustMask',dust),('FreshFractureMask',fresh),('BaseColorSource',base),('RoughnessSource',rough)]:
            attrs[name].data.foreach_set('color',array.ravel())
    # Temporary original-face bookkeeping is retained only in this review stage.
    return {'before_triangles':before_tri,'after_triangles':P.triangles(o),
        'target_max_edge_m':target,'refinement':counts,'exposures':details,
        'max_vertex_loss_m':float(np.linalg.norm(delta,axis=1).max()),
        'outside_exposure_positions_unchanged':bool(np.all(delta[depth_max==0]==0)),
        'uv_method':'Existing atlas; local topology inserts interpolated coordinates without unwrap or repack' if is_game else None}

def fixed_renders(prefix,objects):
    sc,cam,review=B.setup_render();sc.render.resolution_x=1600;sc.render.resolution_y=1000
    cam.data.sensor_fit='VERTICAL';cam.data.sensor_height=24;cam.data.lens=12/math.tan(math.radians(25))
    views={'detail':((2.5,-3.7,2.4),(.4,0,1)),'rear':((-7,8,3.4),(0,0,.8))}
    for name,(eye,target) in views.items():
        cam.location=eye;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=str(STAGE/(prefix+name+'.png'));bpy.ops.render.render(write_still=True)
    for ob in list(review.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.collections.remove(review)

def run():
    STAGE.mkdir(parents=True,exist_ok=True);identity=P.identity(SOURCE)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    before_bounds={o.name:[list(o.bound_box[i]) for i in range(8)] for o in (high,low)}
    report={'source_file':str(SOURCE.relative_to(B.ROOT)),'source_identity':identity,'regions':[
        {k:v for k,v in r.items() if k in ('name','center','normal','half','seed')} for r in REGIONS]}
    report['high']=refine(high,False);report['game']=refine(low,True)
    bpy.context.view_layer.update()
    assert all(np.max(np.abs(np.array(o.bound_box)-before_bounds[o.name]))<1e-6 for o in (high,low))
    report['macro_bounds_unchanged']=True
    for o in (high,low):o.hide_set(False);o.hide_render=False
    high_material=high.data.materials[0];low_material=low.data.materials[0];clay=B.clay_material()
    high.data.materials[0]=clay;low.hide_render=True;fixed_renders('gray-high-',(high,))
    high.hide_render=True;low.hide_render=False;low.data.materials[0]=clay;fixed_renders('gray-game-',(low,))
    high.data.materials[0]=high_material;low.data.materials[0]=low_material;high.hide_render=False;low.hide_render=True
    fixed_renders('source-pbr-',(high,))
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    for im in bpy.data.images:
        if im.filepath.startswith('//textures/'):im.filepath='//../../textures/'+im.filepath.rsplit('/',1)[-1]
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'source-candidate.blend'),compress=True,relative_remap=False)
    report['status']='PENDING_ACTUAL_GRAY_AND_SOURCE_REVIEW_BEFORE_4K_BAKE'
    report['source_material']='Original stage11 shader; local Dust/Fresh attributes change only where material was physically removed. No stage14 cloud tone.'
    assert P.identity(SOURCE)==identity
    P.write_json(STAGE/'stage.json',report);print('RESULT_JSON='+json.dumps({k:v for k,v in report.items() if k not in ('high','game')}),flush=True)

if __name__=='__main__':run()
