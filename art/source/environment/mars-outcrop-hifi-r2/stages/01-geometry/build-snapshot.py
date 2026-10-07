"""Original stratified volumes, explicit fractures and erosion; Blender 5.2.2.
Run --background --factory-startup --threads 6 --python-exit-code 1 --python build.py.
No photograph, external generator or r1 mesh is read by this build.
"""
import bpy, bmesh, math, json, sys
import numpy as np
from pathlib import Path
from mathutils import Vector
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
for skill in ('expert', 'uv-baking', 'texturing-shading'):
    sys.path.append(str(ROOT/'.agents/skills'/('scenario-blender-'+skill)/'scripts'))
import bx_audit as A, bx_review as R, bx_uvbake as U, bx_materials as M

def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)

def noise(x,y,z,scale=1.,seed=0):
    """Coherent value field, used for erosion amplitude, never as the primary form."""
    p=np.stack(np.broadcast_arrays(x*scale,y*scale,z*scale),axis=-1)
    base=np.floor(p).astype(np.int32); f=p-base; f=f*f*(3-2*f)
    out=np.zeros(p.shape[:-1])
    for dx in (0,1):
        for dy in (0,1):
            for dz in (0,1):
                q=base+np.array([dx,dy,dz])
                v=np.sin(q[...,0]*127.1+q[...,1]*311.7+q[...,2]*74.7+seed*19.3)*43758.5453
                v=(v-np.floor(v))*2-1
                out+=v*(f[...,0] if dx else 1-f[...,0])*(f[...,1] if dy else 1-f[...,1])*(f[...,2] if dz else 1-f[...,2])
    return out

def mesh(name,verts,faces,collection):
    me=bpy.data.meshes.new(name); me.from_pydata(np.asarray(verts).tolist(),[],faces); me.update()
    ob=bpy.data.objects.new(name,me); collection.objects.link(ob)
    bm=bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(me); bm.free()
    for p in me.polygons:p.use_smooth=True
    return ob

def collection(name):
    c=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c); return c

def rock_volume(name,polygon,height,seed,coll,spacing=.022,vertical=.010,tilt=(.08,-.04),weather=None):
    """Closed sandstone volume. Bedding physically retracts each exposed face.
    Fresh joint faces receive little differential erosion; exposed faces retain ridges.
    The canopy intersects bedding, so these are cut strata rather than stacked plates.
    """
    rng=np.random.default_rng(seed)
    poly=np.asarray(polygon,float); center=poly.mean(axis=0)
    if np.cross(poly[1]-poly[0],poly[2]-poly[1])<0:poly=poly[::-1]
    edge=np.roll(poly,-1,axis=0)-poly; length=np.linalg.norm(edge,axis=1)
    ns=np.stack([edge[:,1],-edge[:,0]],axis=1)/length[:,None]
    if weather is None:weather=np.clip(.45-.5*ns[:,1]+rng.uniform(-.18,.18,len(poly)),.12,1)
    boundary=[]; normals=[]; exposed=[]; arcs=[]; along=0
    for i in range(len(poly)):
        steps=max(3,int(math.ceil(length[i]/spacing)))
        for j in range(steps):
            t=j/steps
            boundary.append(poly[i]+t*edge[i]); arcs.append(along+t*length[i])
            n=ns[i] if j else (ns[i]+ns[i-1])/max(np.linalg.norm(ns[i]+ns[i-1]),1e-8)
            normals.append(n); exposed.append(weather[i])
        along+=length[i]
    p=np.array(boundary); n=np.array(normals); exposed=np.array(exposed); N=len(p)
    x,y=p.T
    def canopy(x,y):
        rx,ry=x-center[0],y-center[1]
        # Author-defined intersecting erosion planes preserve angular, varied summits.
        t=height+tilt[0]*rx+tilt[1]*ry
        t-=.18*np.maximum(0,rx+.38*ry-.15)
        t-=.10*np.maximum(0,-.5*rx-ry-.30)
        t+=.047*noise(x,y,x*0+seed,3.1,seed)
        t+=.012*noise(x,y,x*0+seed,14,seed+9)
        # Two partially eroded joint traces, real relief in the canopy.
        crack=rx+.40*ry-.21-.028*noise(x,y,rx*0,5,seed+3)
        t-=.025*np.exp(-(crack/.026)**2)*smooth(-.8,.0,ry)
        return t
    top=canopy(x,y)
    nz=max(28,int(math.ceil((max(top)+.16)/vertical)))
    frac=np.linspace(0,1,nz+1)[:,None]
    zz=-.16+frac*(top[None,:]+.16)
    xx=np.broadcast_to(x,zz.shape); yy=np.broadcast_to(y,zz.shape)
    q=zz-.055*xx+.025*yy
    # Irregular bed succession shared geologically, separate broken joint faces.
    brng=np.random.default_rng(7351)
    beds=np.r_[-1,np.cumsum(brng.uniform(.037,.118,70))-1]
    amp=brng.uniform(.028,.078,len(beds))
    idx=np.clip(np.searchsorted(beds,q)-1,0,len(beds)-2)
    phase=(q-beds[idx])/(beds[idx+1]-beds[idx])
    hard=smooth(.05,.16,phase)*(1-smooth(.46,.94,phase))
    lam=amp[idx]*(hard-.54)
    lam*=.68+.42*noise(xx,yy,idx*.041,3.6,91)
    # Cross beds occur within bounded packages and are truncated by their margins.
    cross=np.zeros_like(q)
    for lo,hi,slope in ((.29,.79,.21),(1.06,1.60,-.17)):
        gate=smooth(lo,lo+.04,q)*(1-smooth(hi-.035,hi,q))
        cq=q-slope*xx-.06*yy+.010*noise(xx,yy,q,8,seed)
        wave=(cq*23.7+noise(xx,yy,q*0,2,4)*.25)%1
        cross+=gate*.027*(smooth(.06,.18,wave)*(1-smooth(.4,.94,wave))-.48)
    erosion=.046*noise(xx,yy,zz,3.9,seed)+.021*noise(xx,yy,zz,11.5,seed+6)
    # Face spalls interrupt the succession; not periodic all-around shelves.
    recess=np.zeros_like(q)
    for cx,cz,wx,wz,depth in ((center[0]-.30,.52,.32,.16,.10),(center[0]+.30,1.18,.38,.22,.13),(center[0]-.02,1.67,.23,.14,.075)):
        recess+=depth*np.exp(-(((xx-cx)/wx)**4+((zz-cz)/wz)**4))*smooth(-center[1]+.2,-center[1]+.8,-yy)
    macro=.025*noise(xx,yy,zz,1.6,seed+21)
    micro=.0058*noise(xx,yy,zz,36,seed+71)+.0024*noise(xx,yy,zz,83,seed+55)
    displacement=exposed[None,:]*(lam+cross+erosion-recess)+macro+micro
    # Feet are tucked into regolith; no severed film-thin bottom layer.
    displacement*=smooth(-.16,-.03,zz)
    vx=xx+displacement*n[None,:,0]; vy=yy+displacement*n[None,:,1]
    vv=np.stack([vx,vy,zz],axis=-1).reshape(-1,3)
    faces=[]
    for j in range(nz):
        for i in range(N):faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
    verts=list(vv); topids=list(range(nz*N,(nz+1)*N))
    # Concentric cap grid follows the irregular footprint and intersects physical beds.
    nr=max(10,int(max(np.linalg.norm(p-center,axis=1))/spacing))
    outer=np.stack([vx[-1],vy[-1]],axis=1)
    prev=topids
    for ring in range(1,nr):
        t=1-ring/nr; xy=center[None,:]+t*(outer-center[None,:])
        capz=canopy(xy[:,0],xy[:,1])
        ids=list(range(len(verts),len(verts)+N)); verts.extend(np.column_stack([xy,capz]))
        for i in range(N):faces.append((prev[i],prev[(i+1)%N],ids[(i+1)%N],ids[i]))
        prev=ids
    ci=len(verts); verts.append((*center,float(canopy(*center))))
    for i in range(N):faces.append((prev[i],prev[(i+1)%N],ci))
    bi=len(verts); verts.append((*center,-.16))
    for i in range(N):faces.append((i,bi,(i+1)%N))
    ob=mesh(name,verts,faces,coll)
    ob['source_method']='authored joint volume; differential bed erosion; bounded cross beds; spall recesses; canopy plane cuts'
    ob['seed']=seed; ob['bedding_unit']='Stimson-inspired original sandstone, not a sampled site'
    return ob

SPECS=[
 ('WestCrown',[(-2.75,-.42),(-2.38,-1.08),(-1.49,-1.13),(-.64,-.80),(-.49,.53),(-1.00,1.12),(-2.38,.95)],1.99,113,(.06,.035)),
 ('CentralJoint',[(-.51,-.77),(.20,-.94),(1.17,-.51),(1.33,.94),(.45,1.54),(-.35,1.15)],1.81,227,(-.03,.05)),
 ('EastWedge',[(1.23,-.57),(1.54,-1.05),(2.52,-.98),(2.97,-.11),(2.60,.74),(1.42,1.05)],1.39,346,(-.19,.08)),
 ('FrontTalus',[(-2.40,-1.01),(-2.55,-1.39),(-1.94,-1.81),(-.84,-1.61),(-.56,-1.00),(-1.25,-.90)],.79,471,(.14,.21)),
 ('ForeButtress',[(-.61,-1.04),(-.28,-1.85),(.67,-1.92),(1.44,-1.17),(1.20,-.68),(.23,-.69)],1.03,562,(.08,.16)),
 ('RearHeel',[(-.48,1.10),(.48,1.52),(.14,1.92),(-.87,1.80),(-1.55,1.12)],.81,694,(-.13,-.09)),
]

def clay_material():
    m=bpy.data.materials.new('REVIEW_Neutral_Clay'); p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(.32,.32,.32,1); p.inputs['Roughness'].default_value=.87
    m.diffuse_color=(.32,.32,.32,1); return m

def setup_render():
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=32
    sc.cycles.use_denoising=True; sc.render.threads_mode='FIXED'; sc.render.threads=6
    sc.render.resolution_x=1500; sc.render.resolution_y=1100; sc.render.resolution_percentage=100
    sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Medium High Contrast'; sc.view_settings.exposure=0
    sc.world.color=(.2,.2,.2); sc.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.52,.58,.66,1)
    sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.55
    c=collection('REVIEW_ONLY_NOT_EXPORTED')
    sun=bpy.data.lights.new('ReviewSun','SUN'); sun.energy=2.8; sun.angle=.07
    ob=bpy.data.objects.new('ReviewSun',sun); c.objects.link(ob); ob.rotation_euler=(math.radians(30),math.radians(-28),math.radians(-38))
    cam=bpy.data.objects.new('ReviewCamera',bpy.data.cameras.new('ReviewCamera')); c.objects.link(cam); sc.camera=cam
    ground=mesh('ReviewGround',[(-200,-200,-.11),(200,-200,-.11),(200,200,-.11),(-200,200,-.11)],[(0,1,2,3)],c)
    ground.data.materials.append(clay_material())
    return sc,cam,c

VIEWS={'front':((7.0,-10,4.8),(0,-.15,.78),56), 'reverse':((-6.5,8,4.2),(0,.1,.75),56),
       'detail':((2.0,-5.3,2.45),(-.2,-.5,1.05),63)}
def renders(out, prefix='', views=VIEWS):
    sc,cam,c=setup_render(); out=Path(out); out.mkdir(parents=True,exist_ok=True)
    for name,(eye,target,lens) in views.items():
        cam.location=eye; cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.lens=lens
        sc.render.filepath=str(out/(prefix+name+'.png')); bpy.ops.render.render(write_still=True)
    for ob in list(c.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.collections.remove(c)

def build():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    sc=bpy.context.scene; sc.unit_settings.system='METRIC'; sc.unit_settings.scale_length=1
    high=collection('SOURCE_HIGH_EDITABLE'); clay=clay_material(); objects=[]
    for name,poly,h,seed,tilt in SPECS:
        ob=rock_volume('HIGH_'+name,poly,h,seed,high,tilt=tilt); ob.data.materials.append(clay); objects.append(ob)
        print('BUILT',ob.name,len(ob.data.vertices),len(ob.data.polygons),flush=True)
    bpy.context.view_layer.update()
    stage=HERE/'stages/01-geometry'; stage.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-high.blend'),compress=True)
    R.review(objects,str(stage/'silhouette'),views=('front','back','threequarter'),modes=('silhouette',),res=500)
    renders(stage,prefix='clay-')
    report={'blender':bpy.app.version_string,'stage':'geometry-before-material','objects':[
        {'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'dimensions':list(o.dimensions)} for o in objects],
        'sources_read':['stimson-close.jpg','murray-buttes-shape.jpg','color-processing.jpg'],
        'visual_gate':'PENDING_ROOT_AND_OPEN_IMAGE','old_generator_used':False}
    (stage/'stage.json').write_text(json.dumps(report,indent=2))
    print('RESULT_JSON='+json.dumps(report),flush=True)

if __name__=='__main__':build()
