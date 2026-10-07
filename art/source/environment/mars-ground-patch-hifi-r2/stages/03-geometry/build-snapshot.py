"""Companion sand / subcrop / angular clast patch, original geometry in metres."""
import bpy, bmesh, json, math, sys
import numpy as np
from pathlib import Path
from mathutils import Matrix, Vector
HERE=Path(__file__).resolve().parent
OUTCROP=HERE.parent/'mars-outcrop-hifi-r2'
sys.path.insert(0,str(OUTCROP))
import build as B

def reference_height(x,y):
    """r1 exact function at world (x+6,-y), written out to avoid importing its build."""
    x=np.asarray(x)+6; z=-np.asarray(y)
    def mound(cx,cz,wx,wz,h):return h*np.exp(-(((x-cx)/wx)**2+((z-cz)/wz)**2)*2)
    def mesa(cx,cz,wx,wz,h):return (h+.6*np.sin(x*.071+z*.039))*np.exp(-(((x-cx)/wx)**4+((z-cz)/wz)**4)*2)
    small=.21*np.sin(x*.13+z*.055)+.17*np.sin(z*.17-x*.027)
    rolling=mound(-32,-34,35,26,2.1)+mound(42,-47,46,28,3.5)+mound(-68,22,34,46,2.9)+mound(72,24,58,44,2.3)
    far=mesa(-70,-144,83,43,16)+mesa(98,-181,80,46,12)+mesa(-166,-69,49,77,9)+mound(182,-87,54,86,8)+mesa(-16,-235,143,61,13)
    return (small+rolling+far)*B.smooth(12,22,np.hypot(x,z))

def ground_height(x,y,fine=True):
    x,y=np.broadcast_arrays(x,y); edge=1-B.smooth(4.3,6,np.maximum(np.abs(x),np.abs(y)))
    r=np.sqrt((x/3.4)**2+(y/2.25)**2)
    apron=.15*np.exp(-((r-.86)/.47)**2)
    apron*=.78+.27*B.noise(x,y,x*0,.85,627)
    drift=.125*np.exp(-(((x+.85)/2.5)**2+((y+2.2)/1.15)**2))
    channels=-.03*np.exp(-((x+1.0+.22*y)/.42)**2)*np.exp(-((y+2.7)/1.1)**2)
    small=.012*B.noise(x,y,x*0,2.6,641)+.004*B.noise(x,y,x*0,12,872)
    if fine:
        # Subdued wind ripples in sheltered sand pockets, broken by coarser patches.
        gate=np.exp(-(((x-1.5)/1.7)**2+((y+2.6)/.8)**2))
        small+=gate*.003*np.sin((x+.26*y+.10*B.noise(x,y,x*0,1.5,44))*37)
    return reference_height(x,y)+edge*(apron+drift+channels+small)

def ground_mesh(coll,step=.04):
    v=np.linspace(-6,6,round(12/step)+1); X,Y=np.meshgrid(v,v)
    Z=ground_height(X,Y); N=len(v)
    ob=B.mesh('HIGH_SandApron',np.stack([X,Y,Z],axis=-1).reshape(-1,3),
        [(j*N+i,j*N+i+1,(j+1)*N+i+1,(j+1)*N+i) for j in range(N-1) for i in range(N-1)],coll)
    ob['boundary_contract']='x,y +/-6; height(x+6,-y) from r1 exact; open terrain sheet intended'
    return ob

def clast(name,width,depth,thick,seed,coll):
    rng=np.random.default_rng(seed)
    n=7 if seed%3 else 6
    ang=np.arange(n)*2*np.pi/n+rng.uniform(-.13,.13,n)
    rad=rng.uniform(.73,1.0,n)
    xy=np.stack([np.cos(ang)*rad*width/2,np.sin(ang)*rad*depth/2],axis=1)
    verts=[]
    for k in (0,1):
        for i,(x,y) in enumerate(xy):
            fac=1 if not k else rng.uniform(.73,.95)
            z=(0 if not k else thick)*max(.16,.66+.64*x/max(width,.1)-.76*y/max(depth,.1))
            z+=rng.uniform(-.1,.1)*thick
            verts.append((x*fac+(thick*.08 if k else 0),y*fac,z))
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    ob=B.mesh(name,verts,faces,coll)
    bm=bmesh.new()
    for co in verts:bm.verts.new(co)
    hull=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
    unused=list({g for g in hull['geom_interior']+hull['geom_unused'] if isinstance(g,bmesh.types.BMVert)})
    if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
    bm.to_mesh(ob.data);bm.free();ob.data.update()
    bpy.context.view_layer.objects.active=ob; ob.select_set(True)
    bev=ob.modifiers.new('fracture_edge_wear','BEVEL'); bev.width=min(.009,thick*.065); bev.segments=2
    bpy.ops.object.modifier_apply(modifier=bev.name)
    # Explicit face tessellation preserves fracture planes; no sphere smoothing.
    bm=bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=5,use_grid_fill=True)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.to_mesh(ob.data);bm.free();ob.data.update()
    co=np.array([v.co[:] for v in ob.data.vertices]); norm=np.array([v.normal[:] for v in ob.data.vertices])
    x,y,z=co.T
    d=(.006*B.noise(x,y,z,18,seed)+.002*B.noise(x,y,z,61,seed+1))*min(1,width/.4)
    if thick>.12:d+=.007*np.sin((z+x*.06)*110)*(1-np.abs(norm[:,2]))
    co+=norm*d[:,None];ob.data.vertices.foreach_set('co',co.astype(np.float32).ravel());ob.data.update()
    for p in ob.data.polygons:p.use_smooth=True
    ob.select_set(False);ob['source_method']='authored angular fracture planes, small bevel, tessellated erosion'
    return ob

def inside_polygon(x,y,poly):
    inside=False
    for i in range(len(poly)):
        a,b=poly[i],poly[i-1]
        if ((a[1]>y)!=(b[1]>y)) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside

def make_clasts(coll,clay):
    template_coll=B.collection('SOURCE_CLAST_PROTOTYPES')
    sizes=[(1.02,.66,.13),(.74,.48,.10),(.51,.78,.17),(.57,.43,.22),(.41,.35,.17),
           (.35,.51,.13),(.31,.16,.035),(.22,.18,.041),(.16,.25,.031),(.11,.09,.06),(.07,.09,.038),(.05,.042,.025)]
    templates=[clast('HIGH_Clast_%02d'%i,*s,771+i*67,template_coll) for i,s in enumerate(sizes)]
    for o in templates:o.data.materials.append(clay);o.hide_render=True;o.hide_set(True)
    rng=np.random.default_rng(937551); placed=[]; trials=0
    # Density decays from the eroded front and side slopes, with two fan concentrations.
    while len(placed)<310 and trials<16000:
        trials+=1;x,y=rng.uniform(-5.8,5.8,2)
        if any(inside_polygon(x,y,s[1]) for s in B.SPECS):continue
        r=math.sqrt((x/3.4)**2+(y/2.2)**2)
        fan=math.exp(-((r-1.13)/.46)**2)*(.52+.48*max(0,-y/3))
        fan+=.24*math.exp(-(((x+2.5)/1.2)**2+((y+3)/.9)**2))
        if rng.random()>fan*.84+.025:continue
        if any((x-a)**2+(y-b)**2<.005 for a,b in placed):continue
        if len(placed)<18 and r<1.32:idx=int(rng.integers(0,6));scale=rng.uniform(.7,1.0)
        elif r<1.55 and rng.random()<.26:idx=int(rng.integers(3,9));scale=rng.uniform(.6,1.0)
        else:idx=int(rng.integers(7,12));scale=rng.uniform(.55,1.2)
        src=templates[idx];ob=src.copy();ob.data=src.data.copy();ob.name='CLAST_%03d_source_%02d'%(len(placed),idx);coll.objects.link(ob)
        ob.hide_render=False;ob.hide_set(False)
        yaw=rng.uniform(-math.pi,math.pi)
        pitch=-(ground_height(x+.05,y)-ground_height(x-.05,y))/.1+rng.uniform(-.06,.06)
        roll=(ground_height(x,y+.05)-ground_height(x,y-.05))/.1+rng.uniform(-.06,.06)
        from mathutils import Euler
        rot=Euler((roll,pitch,yaw)).to_matrix();R=np.array(rot)
        co=np.array([v.co[:] for v in ob.data.vertices])@R.T*scale
        co[:,0]+=x;co[:,1]+=y
        # Bury every fragment's lowest point into its local sand height.
        g=ground_height(co[:,0],co[:,1]);offset=float(np.max(g-co[:,2]))-min(.018,.15*sizes[idx][2])
        co[:,2]+=offset
        above=co[:,2]-g
        assert float(np.min(above))<0 and float(np.max(above))>sizes[idx][2]*scale*.35, (idx,above.min(),above.max())
        ob.data.vertices.foreach_set('co',co.astype(np.float32).ravel());ob.data.update()
        ob['template']=idx;ob['placement_matrix']=list(np.r_[R.ravel(),scale,x,y,offset])
        placed.append((x,y))
    return templates

def build():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
    coll=B.collection('SOURCE_HIGH_EDITABLE');clay=B.clay_material()
    ground=ground_mesh(coll);ground.data.materials.append(clay)
    templates=make_clasts(coll,clay)
    # Larger bedrock tongues are mostly buried; they expose the parent's geological fabric.
    for i,(x,y,w,d,t,turn) in enumerate([(-2.55,-1.83,1.1,.82,.13,-.25),(.88,-2.30,1.3,.5,.095,.43),(2.92,.53,.72,1.04,.14,.62),(-1.66,1.88,.76,.55,.10,.3)]):
        ob=clast('HIGH_Subcrop_%02d'%i,w,d,t,1800+i*91,coll);ob.data.materials.append(clay)
        co=np.array([v.co[:] for v in ob.data.vertices]);R=np.array(Matrix.Rotation(turn,3,'Z'));co=co@R.T
        co[:,0]+=x;co[:,1]+=y;co[:,2]+=ground_height(co[:,0],co[:,1])-.06
        ob.data.vertices.foreach_set('co',co.astype(np.float32).ravel());ob.data.update()
    stage=HERE/'stages/03-geometry';stage.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'ground-high.blend'),compress=True)
    B.renders(stage,prefix='clay-',views={'front':((11,-13,8),(0,0,.05),48),'reverse':((-11,12,6),(0,0,.05),48),'detail':((-.2,-6,1.55),(-1.0,-2.15,.07),58)})
    edges=np.array([v.co[:] for v in ground.data.vertices]);mask=(np.abs(edges[:,0])>5.999)|(np.abs(edges[:,1])>5.999)
    err=float(np.max(np.abs(edges[mask,2]-reference_height(edges[mask,0],edges[mask,1]))))
    assert err<1e-6
    result={'version':bpy.app.version_string,'boundary_max_error_m':err,'boundary_contract':'r1 height(x+6,-y)',
            'source_triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in coll.objects if o.type=='MESH'),
            'high_objects':len(coll.objects),'visual':'PENDING_OPEN_IMAGE','status':'SHAPE_BUILT'}
    (stage/'stage.json').write_text(json.dumps(result,indent=2));print('RESULT_JSON='+json.dumps(result))

if __name__=='__main__':build()
