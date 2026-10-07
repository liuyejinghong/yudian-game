"""Continuous exposure response across the two real faces, without artificial ROIs."""
import bpy,bmesh,json,sys,math,hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
import pipeline as P
STAGE=HERE/'stages/17-continuous-face-response';SOURCE=HERE/'mars-outcrop-hifi-r2.blend'
REGIONS=[dict(id=1,name='right_actual_cut_face',center=(1.406704,-.561279,.604110),normal=(.307580,-.605237,.734223),seed=15031),
         dict(id=2,name='actual_rear_wall',center=(-1.05,1.50034,.94),normal=(-.194,1,.065),seed=15079)]
for r in REGIONS:
    r['o']=np.array(r['center']);r['n']=np.array(r['normal']);r['n']/=np.linalg.norm(r['n'])
    r['u']=np.cross(r['n'],(0,0,1));r['u']/=np.linalg.norm(r['u']);r['v']=np.cross(r['u'],r['n']);r['v']/=np.linalg.norm(r['v'])

def arrays(me):
    co=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('co',co)
    vi=np.empty(len(me.loops),np.int32);me.loops.foreach_get('vertex_index',vi)
    nr=np.empty(len(me.loops)*3,np.float32);me.corner_normals.foreach_get('vector',nr)
    uv=None
    if me.uv_layers:
        uv=np.empty(len(me.loops)*2,np.float32);me.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)
    return co.reshape(-1,3),vi,nr.reshape(-1,3),uv

def rear_geometry(co):
    x,y,z=co.T;back=1.55-.15*abs(x+.5)-.065*z+.10*np.exp(-(x+.8)**2)
    n=np.column_stack((.15*np.sign(x+.5)+.20*(x+.8)*np.exp(-(x+.8)**2),np.ones(len(x)),np.full(len(x),.065)))
    norm=np.linalg.norm(n,axis=1);return (y-back)/norm,n/norm[:,None]

def actual_face_labels(me):
    co,vi,_,_=arrays(me);sizes=np.empty(len(me.polygons),np.int32);me.polygons.foreach_get('loop_total',sizes);lf=np.repeat(np.arange(len(sizes)),sizes)
    centers=np.column_stack([np.bincount(lf,weights=co[vi,k],minlength=len(sizes))/sizes for k in range(3)])
    normal=np.empty(len(sizes)*3,np.float32);me.polygons.foreach_get('normal',normal);normal=normal.reshape(-1,3)
    labels=np.zeros(len(sizes),np.int32);r=REGIONS[0]
    plane=(centers-r['o'])@r['n'];right=(abs(plane)<.012)&(normal@r['n']>.965)&(centers[:,0]>.475)&(centers[:,1]<.06)
    distance,n=rear_geometry(centers);rear=(abs(distance)<.012)&(np.sum(normal*n,axis=1)>.965)&(normal[:,1]>.8)
    labels[right]=1;labels[rear]=2
    area=np.empty(len(sizes),float);me.polygons.foreach_get('area',area)
    return labels,[{'name':r['name'],'faces':int((labels==r['id']).sum()),'area_m2':float(area[labels==r['id']].sum()),
        'center_bounds_min':centers[labels==r['id']].min(0).tolist(),'center_bounds_max':centers[labels==r['id']].max(0).tolist()} for r in REGIONS]

def physical_boundaries(high):
    labels,report=actual_face_labels(high.data);bm=bmesh.new();bm.from_mesh(high.data);bm.faces.ensure_lookup_table();layer=bm.faces.layers.int.new('face_label')
    for f,k in zip(bm.faces,labels):f[layer]=int(k)
    trees={}
    for r in REGIONS:
        points=[]
        for e in bm.edges:
            kinds=[f[layer] for f in e.link_faces]
            if r['id'] not in kinds or (len(kinds)==2 and all(k==r['id'] for k in kinds)):continue
            a,b=(np.array(v.co) for v in e.verts);steps=max(1,int(np.ceil(np.linalg.norm(b-a)/.008)))
            points.extend(a+(b-a)*t/steps for t in range(steps+1))
        assert points,r['name'];tree=KDTree(len(points))
        for i,p in enumerate(points):tree.insert(Vector(p),i)
        tree.balance();trees[r['id']]=tree
    bm.free();return trees,report

def raw_field(u,v,seed):
    # The accepted stage16 inner field, now continuous across each actual face.
    uw=u+.014*B.noise(u,v,u*0,9.3,seed+1);vw=v+.011*B.noise(u,v,u*0,11.1,seed+2)
    p=.94*uw+.34*vw;q=-.34*uw+.94*vw
    return np.clip(.0038+.0030*B.noise(p*.78,q*1.18,u*0,27.5,seed+3)
        +.00145*B.noise(p*.94+.07,q*1.08-.11,u*0+.31,44,seed+4)
        +.00065*B.noise(uw,vw,u*0+.17,15.7,seed+5),.0006,.008)

def face_response(co,r,tree):
    x,y,z=co.T;n=np.tile(r['n'],(len(co),1)) if r['id']==1 else rear_geometry(co)[1]
    uv=co-r['o'];u=uv@r['u'];v=uv@r['v'];seed=r['seed']
    dust=np.clip(.12+.48*B.smooth(.12,.92,n[:,2])+.25*(1-B.smooth(.05,.48,z))+.10*B.noise(x,y,z,1.2,383),0,.84)
    bare=1-dust;fresh=B.smooth(.56,.97,abs(n[:,0]))*bare*.55
    fresh=np.maximum(fresh,B.smooth(.55,.88,n[:,1])*B.smooth(.60,1.02,y)*B.smooth(-.55,.18,x)*bare)
    boundary=np.array([tree.find(Vector(p))[2] for p in co]);rim=B.smooth(.04,.12,boundary)
    exposure=bare*(1+.9*dust)*(1-.30*fresh)
    # Weak physical weathering differences remain connected everywhere on bare
    # rock. They never create another zero-coverage island or a color cloud.
    age=.65+.35*B.smooth(-.65,.65,B.noise(u,v,u*0+.23,3.1,seed+19))
    field=raw_field(u,v,seed);depth=field*rim*exposure*age
    old_tone=.13*B.noise(u,v,u*0+.21,18,seed+61)+.045*B.noise(u,v,u*0+.37,31,seed+62)
    fresh_tone=.08*B.noise(u,v,u*0+.47,31,seed+63)+.025*B.noise(u,v,u*0+.09,52,seed+64)
    tone=bare**1.1*rim*((1-fresh)*old_tone+fresh*fresh_tone+.015*(field-.0038)/.003)
    return depth,n,tone,{'actual_face_edge_fade_m':[.04,.12],'depth_quantiles_m':np.quantile(depth,[.1,.5,.9,1]).tolist(),
        'intrinsic_color_multiplier_quantiles':np.quantile(1+tone,[.01,.1,.5,.9,.99]).tolist(),
        'nonzero_depth_vertices':int((depth>1e-7).sum()),'quiet_edge_vertices':int((rim==0).sum())}

def repair_slivers(bm):
    uv=bm.loops.layers.uv.active;source=bm.faces.layers.int['stage17_original_face'];region=bm.faces.layers.int['stage17_face_region']
    def area(points):
        a,b,c=points;return float((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))*.5
    def fa(f):return area([np.array(l[uv].uv,dtype=float) for l in f.loops])
    records=[]
    for f in [f for f in bm.faces if fa(f)<0]:
        if not f.is_valid:continue
        options=[]
        for edge in f.edges:
            neighbors=[g for g in edge.link_faces if g!=f and g[source]==f[source] and len(g.verts)==3]
            if len(neighbors)!=1:continue
            g=neighbors[0]
            if len(set(f.verts)|set(g.verts))!=4:continue
            values={l.vert:np.array(l[uv].uv,dtype=float) for h in (f,g) for l in h.loops}
            a=next(v for v in f.verts if v not in edge.verts);b=next(v for v in g.verts if v not in edge.verts)
            tris=[(a,edge.verts[0],b),(a,b,edge.verts[1])];tris=[t if area([values[v] for v in t])>0 else tuple(reversed(t)) for t in tris]
            areas=[area([values[v] for v in t]) for t in tris];previous=fa(f)+fa(g)
            if min(areas)>1e-14 and abs(sum(areas)-previous)<1e-12:options.append((min(areas),edge,g,tris,values,previous))
        assert options,'No same-face UV diagonal repair for '+str(f[source])
        _,edge,g,tris,values,previous=max(options,key=lambda a:a[0]);sid=f[source];rid=f[region];mat=f.material_index;smooth=f.smooth
        bm.faces.remove(f);bm.faces.remove(g)
        if edge.is_valid and not edge.link_faces:bm.edges.remove(edge)
        created=[]
        for tri in tris:
            nf=bm.faces.new(tri);nf[source]=sid;nf[region]=rid;nf.material_index=mat;nf.smooth=smooth
            for l in nf.loops:l[uv].uv=values[l.vert]
            created.append(nf)
        records.append({'source_face':sid-1,'area_error':abs(sum(fa(h) for h in created)-previous)})
    assert all(fa(f)>=0 for f in bm.faces);return records

def set_low_normals(me,original):
    old_tri,old_uv,old_nr,control=original;co,vi,_,uv=arrays(me)
    pre=np.empty(len(me.vertices)*3,np.float32);me.attributes['stage17_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
    ids=np.empty(len(me.polygons),np.int32);me.attributes['stage17_original_face'].data.foreach_get('value',ids);ids-=1;lf=np.repeat(ids,3)
    vn=np.empty(len(me.vertices)*3,np.float32);me.vertices.foreach_get('normal',vn);vn=vn.reshape(-1,3)
    normals=np.empty((len(vi),3),float);maxuv=0;ctrl=[];idx=[]
    for start in range(0,len(vi),100000):
        end=min(start+100000,len(vi));f=lf[start:end];p=pre[vi[start:end]].astype(float);a,b,c=old_tri[f,0],old_tri[f,1],old_tri[f,2]
        e0=b-a;e1=c-a;d=p-a;d00=(e0*e0).sum(1);d01=(e0*e1).sum(1);d11=(e1*e1).sum(1);d20=(d*e0).sum(1);d21=(d*e1).sum(1);den=d00*d11-d01*d01
        w1=(d11*d20-d01*d21)/den;w2=(d00*d21-d01*d20)/den;w=np.column_stack((1-w1-w2,w1,w2))
        dist=np.linalg.norm(old_tri[f]-p[:,None,:],axis=2);same=dist.min(1)<1e-7;w[same]=np.eye(3)[dist[same].argmin(1)]
        maxuv=max(maxuv,float(abs((old_uv[f]*w[:,:,None]).sum(1)-uv[start:end]).max()))
        n=(old_nr[f]*w[:,:,None]).sum(1);changed=np.linalg.norm(co[vi[start:end]]-p,axis=1)>1e-7;n[changed]=vn[vi[start:end][changed]];normals[start:end]=n
        keep=same&~changed;ctrl.extend(control[f[keep],dist[keep].argmin(1)].tolist());idx.extend((start+np.where(keep)[0]).tolist())
    assert maxuv<3e-5,maxuv;me.normals_split_custom_set(normals);_,_,nr,_=arrays(me)
    delta=np.linalg.norm(nr[np.array(idx)]-np.array(ctrl),axis=1)
    return {'max_uv_interpolation_error':maxuv,'matched_untouched_original_corner_count':len(idx),
        'native_roundtrip_corner_max_delta':float(delta.max()),'native_roundtrip_corner_p99_delta':float(np.quantile(delta,.99)),
        'native_roundtrip_threshold':.0003,'native_roundtrip_status':'PASS' if delta.max()<=.0003 else 'REWORK'}

def update_source(high,depth,tone):
    me=high.data;data={}
    for name in ('DustMask','FreshFractureMask','BaseColorSource','RoughnessSource'):
        a=np.empty(len(me.vertices)*4,np.float32);me.color_attributes[name].data.foreach_get('color',a);data[name]=a.reshape(-1,4)
    d=data['DustMask'][:,0].copy();f=data['FreshFractureMask'][:,0].copy();reveal=B.smooth(.0007,.004,depth);nd=d*(1-.35*reveal);nf=f+(1-f)*.12*reveal
    def mix(d,f):return (np.array([.220,.195,.171])*(1-f[:,None])+np.array([.172,.162,.148])*f[:,None])*(1-d[:,None])+np.array([.283,.236,.193])*d[:,None]
    data['BaseColorSource'][:,:3]*=mix(nd,nf)/mix(d,f);data['DustMask'][:,:3]=nd[:,None];data['FreshFractureMask'][:,:3]=nf[:,None]
    data['RoughnessSource'][:,:3]+=(-.14*(d-nd)-.025*(nf-f))[:,None]
    for name,a in data.items():me.color_attributes[name].data.foreach_set('color',a.ravel())
    P.M.write_vertex_values(high,'FaceIntrinsicReflectance',1+tone)
    nt=me.materials[0].node_tree;p=nt.nodes['Principled BSDF'];previous=p.inputs['Base Color'].links[0].from_socket
    attr=nt.nodes.new('ShaderNodeVertexColor');attr.layer_name='FaceIntrinsicReflectance';attr.name='LowContrastActualFaceMinerals'
    mult=nt.nodes.new('ShaderNodeMixRGB');mult.name='ActualFaceIntrinsicVariation';mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1
    nt.links.new(previous,mult.inputs[1]);nt.links.new(attr.outputs['Color'],mult.inputs[2]);nt.links.new(mult.outputs[0],p.inputs['Base Color'])

def refine_face(o,is_game,trees,original):
    me=o.data;before=P.triangles(o);labels,area_report=actual_face_labels(me)
    bm=bmesh.new();bm.from_mesh(me);source=bm.faces.layers.int.new('stage17_original_face');region=bm.faces.layers.int.new('stage17_face_region');position=bm.verts.layers.float_vector.new('stage17_original_position')
    for v in bm.verts:v[position]=v.co
    for i,(f,k) in enumerate(zip(bm.faces,labels)):f[source]=i+1;f[region]=int(k)
    target=.020 if is_game else .010;rounds=[]
    for step in range(12):
        edges={e for f in bm.faces if f[region]>0 for e in f.edges if e.calc_length()>target}
        if not edges:break
        rounds.append({'round':step,'edges':len(edges),'max_length_m':max(e.calc_length() for e in edges)})
        print('REFINE',o.name,step,len(edges),flush=True)
        bmesh.ops.subdivide_edges(bm,edges=list(edges),cuts=1,use_grid_fill=True,use_single_edge=True)
        if is_game:bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3],quad_method='FIXED',ngon_method='EAR_CLIP')
        bm.normal_update()
    else:raise AssertionError('Face refinement failed to converge')
    repaired=repair_slivers(bm) if is_game else []
    bm.verts.ensure_lookup_table();pre=np.array([v[position] for v in bm.verts],float);vr=np.zeros(len(pre),np.int8)
    for f in bm.faces:
        if f[region]>0:
            for v in f.verts:vr[v.index]=f[region]
    delta=np.zeros_like(pre);depthall=np.zeros(len(pre));tone=np.zeros(len(pre));responses=[]
    for r in REGIONS:
        chosen=vr==r['id'];depth,n,color,record=face_response(pre[chosen],r,trees[r['id']]);delta[chosen]=-depth[:,None]*n;depthall[chosen]=depth;tone[chosen]=color
        responses.append({'name':r['name'],**record})
    for v,p in zip(bm.verts,pre+delta):v.co=p
    bm.normal_update();bm.to_mesh(me);bm.free();me.update()
    normal_report=set_low_normals(me,original) if is_game else None
    if not is_game:update_source(o,depthall,tone)
    return {'triangles_before':before,'triangles_after':P.triangles(o),'target_edge_m':target,'selected_actual_faces':area_report,
        'refinement':rounds,'uv_diagonal_repairs':repaired,'response':responses,'normal_control':normal_report,
        'max_vertex_displacement_m':float(np.linalg.norm(delta,axis=1).max())}

def fixed_renders(prefix):
    sc,cam,review=B.setup_render();sc.render.resolution_x=1600;sc.render.resolution_y=1000;cam.data.sensor_fit='VERTICAL';cam.data.sensor_height=24;cam.data.lens=12/math.tan(math.radians(25))
    for name,(eye,target) in {'detail':((2.5,-3.7,2.4),(.4,0,1)),'rear':((-7,8,3.4),(0,0,.8))}.items():
        cam.location=eye;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();sc.render.filepath=str(STAGE/(prefix+name+'.png'));bpy.ops.render.render(write_still=True)
    for o in list(review.objects):bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.collections.remove(review)

def run():
    STAGE.mkdir(exist_ok=True,parents=True);identity=P.identity(SOURCE);bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop'];bounds={o.name:np.array(o.bound_box) for o in (high,low)}
    assert P.triangles(high)==946516 and P.triangles(low)==227160,'Historical preview requires the original baseline; use finalize.py for the current public build'
    co,vi,nr,uv=arrays(low.data);old_nr=nr.reshape(-1,3,3).astype(float);old_tri=co[vi.reshape(-1,3)].astype(float);old_uv=uv.reshape(-1,3,2).astype(float)
    # The control uses exactly this native version and the same original corners.
    low.data.normals_split_custom_set(nr.tolist());_,_,control,_=arrays(low.data);original=(old_tri,old_uv,old_nr,control.reshape(-1,3,3).astype(float))
    trees,faces=physical_boundaries(high);report={'status':'BUILDING_COMPLETE_ACTUAL_FACE_PREVIEWS','source_identity':identity,'physical_high_face_selection':faces,
        'selection':'Analytic actual stage09 wedge and curved rear wall plus matching real face normals; no u/v rectangle or artificial ROI',
        'intrinsic_color':'World-position mineral and erosion reflectance at roughly2–8cm; old faces stronger/coarser, fresh faces weaker/finer, dust quiet; no lighting or AO inputs'}
    P.write_json(STAGE/'progress.json',report)
    report['high']=refine_face(high,False,trees,original);print('HIGH_DONE',flush=True)
    report['game']=refine_face(low,True,trees,original);print('GAME_DONE',flush=True)
    bpy.context.view_layer.update();report['bounds_delta']={o.name:float(abs(np.array(o.bound_box)-bounds[o.name]).max()) for o in (high,low)}
    assert max(report['bounds_delta'].values())<1e-6,report['bounds_delta']
    for o in (high,low):o.hide_set(False)
    hm=high.data.materials[0];lm=low.data.materials[0];clay=B.clay_material();high.data.materials[0]=clay;high.hide_render=False;low.hide_render=True;fixed_renders('gray-high-')
    high.hide_render=True;low.hide_render=False;low.data.materials[0]=clay;fixed_renders('gray-game-')
    high.data.materials[0]=hm;low.data.materials[0]=lm;high.hide_render=False;low.hide_render=True;fixed_renders('source-pbr-')
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    for im in bpy.data.images:
        if im.filepath.startswith('//textures/'):im.filepath='//../../textures/'+im.filepath.rsplit('/',1)[-1]
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'source-candidate.blend'),compress=True,relative_remap=False)
    assert P.identity(SOURCE)==identity
    report['status']='PENDING_COMPLETE_FACE_GRAY_AND_SOURCE_PBR_REVIEW_NO_BAKE';report['candidate']=P.identity(STAGE/'source-candidate.blend')
    P.write_json(STAGE/'stage.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
if __name__=='__main__':run()
