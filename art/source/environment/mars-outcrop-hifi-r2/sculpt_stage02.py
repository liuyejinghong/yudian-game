"""Second form study: implicit cut sandstone volumes, not an extruded ring shell."""
import bpy, json, sys, math
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build as B
sys.path.append(str(B.ROOT/'.agents/skills/scenario-blender-sculpting/scripts'))
import bx_sculpt as S

def bed_relief(q,x,y,seed):
    """Three finite geological packages, with inclined laminae and sparse hard beds."""
    result=np.zeros(np.broadcast_shapes(q.shape,x.shape,y.shape),np.float32)
    for j,(lo,hi,sx,sy) in enumerate(((-.2,.48,.02,.03),(.48,1.14,.24,-.035),(1.14,2.6,-.16,.075))):
        envelope=B.smooth(lo,lo+.055,q)*(1-B.smooth(hi-.055,hi,q))
        t=q-sx*x-sy*y
        rng=np.random.default_rng(184+j*39)
        beds=np.r_[-2,np.cumsum(rng.uniform(.027,.069,150))-2]
        idx=np.clip(np.searchsorted(beds,t)-1,0,len(beds)-2)
        phase=(t-beds[idx])/(beds[idx+1]-beds[idx])
        amps=rng.uniform(.004,.014,len(beds));amps[::6]*=2.8
        a=amps[idx]*(.68+.22*np.sin(x*4.7+idx*.69)+.10*np.sin(y*9.1+idx*1.1))
        peak=B.smooth(.04,.17,phase)*(1-B.smooth(.49,.94,phase))
        result+=envelope*a*(peak-.6)
    return result

def implicit_rock(name,polygon,height,seed,tilt,coll,voxel=.012):
    poly=np.asarray(polygon,float);center=poly.mean(axis=0);p=poly-center
    edges=np.roll(p,-1,axis=0)-p;normals=np.stack([edges[:,1],-edges[:,0]],axis=1)
    normals/=np.linalg.norm(normals,axis=1)[:,None]
    # Half-space planes lean differently. Basal volume is broader, upper mass is cut back.
    slopes=np.array([.08,.17,-.04,.20,.10,-.015,.22])[:len(p)]
    # Each geological unit has faces freshly exposed by joints, with negligible weathered ribs.
    exposures=np.array([.35,1.0,.12,.08,.68,.18,.9])[:len(p)]
    if seed in (227,562):exposures=np.roll(exposures,1)
    extent=np.max(np.abs(p),axis=0)+.24
    lo=(-extent[0],-extent[1],-.23);hi=(extent[0],extent[1],height+.45)
    clay=S.Clay(lo,hi,voxel=voxel)
    def field(x,y,z):
        wx=x+center[0];wy=y+center[1]
        q=z-.035*wx+.02*wy
        relief=bed_relief(q,wx,wy,seed)
        # Broadly worn scar patches determine where stratification survives.
        scars=np.exp(-(((x+.18)/.47)**4+((z-height*.49)/.31)**4))
        scars+=.7*np.exp(-(((x-.56)/.26)**4+((z-height*.77)/.25)**4))
        d=np.broadcast_to(-z-.16,np.broadcast_shapes(x.shape,y.shape,z.shape)).copy()
        for i,(nx,ny) in enumerate(normals):
            plane=nx*(x-p[i,0])+ny*(y-p[i,1])+slopes[i]*(z-.24)
            # Thick, localized erosion recesses interrupt thin cross beds.
            erosion=.042*np.sin(wx*4.9+z*7.3)*np.sin(wy*3.2-z*5.1)
            depth=exposures[i]*(relief*(1-.7*np.minimum(scars,1))-.070*scars+erosion*.40)
            # Two resistant bounding beds, finite in the tangent direction.
            tang=-ny*x+nx*y
            hard=.042*np.exp(-((q-.43)/.065)**2)*B.smooth(-.8,-.2,tang)*(1-B.smooth(.35,.85,tang))
            hard+=.057*np.exp(-((q-1.10)/.05)**2)*np.exp(-((tang+.24)/.51)**4)
            d=np.maximum(d,plane-depth-exposures[i]*hard)
        # Several authored roof/break planes intersect; top is a broken volume, not a lid.
        top=height+tilt[0]*x+tilt[1]*y
        top=np.minimum(top,height-.54*(x+.35*y-.14))
        top=np.minimum(top,height+.39*(.35*x+y+.49))
        top=np.minimum(top,height+.62*(-.75*x+y+.91))
        top+=.016*np.sin(wx*13.2+wy*6.1)*np.sin(wy*15.7-wx*3.1)
        d=np.maximum(d,z-top)
        return d.astype(np.float32)
    clay.add(S.Prim(field,lo,hi))
    # A through-going top joint opens into the outward wall; its depth is geometric.
    if seed in (113,227,346):
        def joint(x,y,z):
            return np.maximum.reduce(np.broadcast_arrays(
                np.abs(x-.21*y-(.02 if seed==113 else -.12))-(.025+.018*np.clip(-y,0,1)),
                z*0+y-.31,
                height-.46-.20*y-z))
        clay.sub(S.Prim(joint,lo,hi))
    # Angular collapse cavities open at a side, stopping before the back of the volume.
    if seed in (113,346,562):
        def notch(x,y,z):
            return np.maximum.reduce(np.broadcast_arrays(
                -x-.30, x-.80, y+.40+.12*x,
                z-(height*.56+.16*x), height*.32-.04*x-z))
        clay.sub(S.Prim(notch,lo,hi),blend=.012)
    elif seed==227:
        # Large oblique spall at the right shoulder, not another horizontal ledge.
        clay.intersect(S.sd_halfspace((.70,.0,height*.70),(1,-.24,.38)),blend=.005)
    elif seed==694:
        clay.intersect(S.sd_halfspace((.0,.22,height*.76),(-.27,1,.45)),blend=.005)
    ob=clay.to_object('HIGH_'+name,collection=coll,symmetric=False)
    # Fine chipping follows the already established volume. It cannot change major forms.
    co=np.empty(len(ob.data.vertices)*3,np.float32);ob.data.vertices.foreach_get('co',co);co=co.reshape(-1,3)
    nor=np.array([v.normal[:] for v in ob.data.vertices]);x,y,z=co.T
    small=.006*B.noise(x,y,z,24,seed)+.0025*B.noise(x,y,z,67,seed+7)
    co+=nor*small[:,None]
    co[:,0]+=center[0];co[:,1]+=center[1]
    ob.data.vertices.foreach_set('co',co.ravel());ob.data.update()
    ob['source_method']='bx_sculpt.Clay + Prim geological SDF, leaning fracture halfspaces, finite cross-bed packages, open joints and collapse cavities'
    ob['voxel_m']=voxel;ob['seed']=seed
    return ob

def build():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
    sc.render.threads_mode='FIXED';sc.render.threads=6
    coll=B.collection('SOURCE_HIGH_EDITABLE');mat=B.clay_material();objects=[]
    for name,poly,h,seed,tilt in B.SPECS:
        ob=implicit_rock(name,poly,h,seed,tilt,coll);ob.data.materials.append(mat);objects.append(ob)
        print('SCULPTED',ob.name,len(ob.data.vertices),len(ob.data.polygons),flush=True)
    bpy.context.view_layer.update()
    stage=HERE/'stages/02-geometry';stage.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-high.blend'),compress=True)
    B.R.review(objects,str(stage/'silhouette'),views=('front','back','threequarter'),modes=('silhouette',),res=500)
    B.renders(stage,prefix='clay-')
    result={'version':bpy.app.version_string,'stage':'02-volume-cuts','skill_functions':['bx_sculpt.Clay','bx_sculpt.Prim','bx_sculpt.sd_halfspace','bx_sculpt.Clay.to_object'],
            'high_triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
            'objects':[{'name':o.name,'vertices':len(o.data.vertices),'bounds':list(o.dimensions)} for o in objects],
            'visual_gate':'PENDING_OPEN_IMAGES_AND_ROOT'}
    (stage/'stage.json').write_text(json.dumps(result,indent=2));print('RESULT_JSON='+json.dumps(result))

if __name__=='__main__':build()
