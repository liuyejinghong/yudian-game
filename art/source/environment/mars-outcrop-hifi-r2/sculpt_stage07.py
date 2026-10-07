"""Bed-controlled differential weathering of the approved continuous parent volume.
Centimetre/decimetre losses are local geometry. No micrograin or texture is used.
"""
import bpy,json,sys
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
sys.path.append(str(B.ROOT/'.agents/skills/scenario-blender-sculpting/scripts'))
import bx_sculpt as S

def beds(x,z):
    # Correlated erosion follows the bed package, with irregular finite exposed ends.
    n=B.noise(x,x*0,z,4.1,793)
    slow=B.noise(x,x*0,z,1.6,92)
    retreat=np.zeros(np.broadcast_shapes(x.shape,z.shape),np.float32)
    layers=[(.10,.04,-2.96,-.02,.056,.045),(.28,.10,-2.74,-.46,.082,.044),
            (.47,.16,-2.67,.13,.066,.036),(.65,.16,-2.53,-.17,.113,.066),
            (.86,.17,-2.62,-.71,.053,.030),(1.04,-.12,-2.73,-.18,.078,.046),
            (1.21,-.13,-2.41,-.49,.047,.025),(1.44,-.16,-2.42,-.64,.105,.061),
            (1.68,-.18,-2.25,-1.04,.042,.031),(.24,-.10,.66,2.75,.035,.035),
            (.45,-.12,1.05,2.29,.029,.025)]
    for i,(h,slope,a,b,depth,width) in enumerate(layers):
        q=z-h-slope*(x+1.2)+.018*n+.012*slow
        w=width*(1+.35*slow+.24*n)
        weak=B.smooth(-w,-w*.62,q)*(1-B.smooth(w*.37,w*.79,q))
        ends=B.smooth(a,a+.14,x+.13*n)*(1-B.smooth(b-.12,b,x+.19*slow))
        retreat=retreat+depth*weak*ends*(.84+.39*n+.18*slow)
    # Two whole hard remnants have detached between adjoining weak beds.
    # Boundary irregularity is correlated; the newly exposed break itself is a plane.
    q=z+.13*(x+1.2)
    peel=B.smooth(-1.68,-1.64,x+.055*n)*(1-B.smooth(-.88,-.84,x+.045*slow))
    peel=peel*B.smooth(1.055,1.08,q+.017*n)*(1-B.smooth(1.25,1.28,q+.022*n))
    retreat=retreat+peel*(.124+.035*(x+1.2)+.025*(z-1.1))
    q=z-.16*(x+1.2)
    peel=B.smooth(-2.20,-2.17,x+.04*n)*(1-B.smooth(-1.79,-1.75,x+.045*slow))
    peel=peel*B.smooth(.47,.49,q+.015*n)*(1-B.smooth(.66,.69,q+.019*n))
    retreat=retreat+peel*(.100-.035*(x+2.0)+.019*z)
    return retreat

def parent_volume(coll,voxel=.014):
    lo=(-3.35,-2.4,-.26);hi=(3.35,2.15,2.42);c=S.Clay(lo,hi,voxel=voxel)
    def body(x,y,z):
        h=np.interp(x,[-3.2,-2.8,-2.20,-1.42,-.66,.05,.78,1.48,2.22,3.18],
            [.58,1.05,1.88,2.12,1.78,1.57,1.49,1.09,.72,.28])
        h=h+.055*y-.18*np.maximum(-y-.35,0)-.14*np.maximum(y-.80,0)
        left=-3.02+.30*np.abs(y+.1)+.11*np.maximum(z-.45,0)
        right=3.13-.32*np.abs(y-.08)-.12*np.maximum(z-.3,0)
        front=-1.80+.12*x+.25*np.maximum(z-.17,0)+.34*B.smooth(.28,.63,z)
        front=front-.21*np.exp(-((x+2.22)/.43)**2)*(1-B.smooth(.3,1.3,z))
        front=front+.13*np.exp(-((x+1.3)/.7)**2)*B.smooth(.5,1.4,z)
        back=1.55-.15*np.abs(x+.5)-.065*z+.10*np.exp(-((x+.8)/1.0)**2)
        slip=1-B.smooth(-.022,.025,x-(-1.96+.29*y+.055*z))
        h=h-.06*slip;front=front+.027*slip
        loss=beds(x,z+.06*slip)
        front=front+loss
        left=left+loss*.35*(1-B.smooth(.1,.75,y))
        d=np.maximum.reduce(np.broadcast_arrays(-z-.16,z-h,left-x,x-right,front-y,y-back))
        chip=B.noise(x,y+.19*z,x*0,8.1,218)
        # Existing front-left corner is worn across the ends, not etched on its face.
        lipcorner=(1-B.smooth(front+.06,front+.23,y))*B.smooth(h-.14,h-.03,z)
        erosion=lipcorner*(.009+.012*B.smooth(-.4,.7,chip))
        return (d+erosion).astype(np.float32)
    c.add(S.Prim(body,lo,hi))
    def bay(x,y,z):
        center=.15+.13*z+.08*y
        width=.18+.20*(1-B.smooth(0,2,z))+.28*np.maximum(-y,0)
        rear=.20+.24*(1-B.smooth(0,1.8,z));floor=-.07+.14*np.maximum(y+1.34,0)
        d=np.maximum.reduce(np.broadcast_arrays(np.abs(x-center)-width,y-rear,floor-z))
        n=B.noise(x,y,z,3.8,190)
        chip=B.noise(x,y,z,9.2,191)
        # Keep a small irregular outline; directional spalls below own the deep losses.
        rim=B.smooth(.98,1.40,z)*(B.smooth(-.7,-.4,y))
        loss=.018*rim*B.smooth(-.3,.6,n)
        seam=np.exp(-((z-.74-.14*y+.025*n)/.065)**2)
        loss=loss+.040*seam*B.smooth(-1.20,-.82,y)*(1-B.smooth(-.1,.23,y))*(.85+.35*n)
        return d-loss
    c.sub(S.Prim(bay,lo,hi),blend=.018)
    def wedge(x,y,z):
        plane=.31*(x-.80)-.61*(y+.45)+.74*(z-.95)
        return np.maximum.reduce(np.broadcast_arrays(.48-x,-plane,y-.05))
    c.sub(S.Prim(wedge,lo,hi),blend=.012)
    # These are OPEN spalls across existing edges, with one quiet sloping break each.
    # Noise perturbs only their end boundary, never the planar fracture surface.
    def rear_west_spall(x,y,z):
        n=B.noise(x,x*0,z,7.0,337)
        plane=.15*(x+1.0)-.55*(y-1.20)+.82*(z-1.60)
        return np.maximum.reduce(np.broadcast_arrays(-plane,1.09+.048*n-y,-1.51+.065*n-x,x+.55+.060*n))
    def rear_east_spall(x,y,z):
        n=B.noise(x,x*0,z,8.6,410)
        plane=-.22*(x-.75)-.64*(y-1.05)+.73*(z-1.21)
        return np.maximum.reduce(np.broadcast_arrays(-plane,.94+.040*n-y,.30+.055*n-x,x-1.19+.054*n))
    def bay_west_spall(x,y,z):
        n=B.noise(x,y,z,7.2,62)
        wall=-.18+.16*(z-1.2)+.09*y
        underside=1.08+.14*y-.20*x
        return np.maximum.reduce(np.broadcast_arrays(wall-x,x-.42,underside-z,-.56+.052*n-y,y-.31+.058*n))
    def bay_east_spall(x,y,z):
        n=B.noise(x,y,z,8.0,126)
        wall=.99-.26*(z-.9)+.10*y
        underside=.93-.21*y+.12*x
        return np.maximum.reduce(np.broadcast_arrays(x-wall,.42-x,underside-z,-1.01+.048*n-y,y+.14+.044*n))
    for f in (rear_west_spall,rear_east_spall,bay_west_spall,bay_east_spall):c.sub(S.Prim(f,lo,hi),blend=.006)
    def west(x,y,z):
        n=B.noise(x*0,y,z,4.1,522)
        at=-1.96+.29*y+.055*z+.020*n
        width=.018+.022*B.smooth(.6,2.1,z)+.016*np.maximum(-y,0)
        width=width+.052*B.smooth(-.23,.61,n)*B.smooth(1.24,1.72,z)
        return np.maximum.reduce(np.broadcast_arrays(np.abs(x-at)-width,.34-.17*y-z))
    def east(x,y,z):
        n=B.noise(x*0,y,z,3.8,348)
        at=1.87-.43*y+.075*z+.024*n
        width=.025+.013*np.maximum(-y,0)+.035*B.smooth(-.22,.65,n)*B.smooth(.25,.54,z)
        return np.maximum.reduce(np.broadcast_arrays(np.abs(x-at)-width,.14+.12*y-z))
    c.sub(S.Prim(west,lo,hi));c.sub(S.Prim(east,lo,hi))
    rock=c.to_object('HIGH_ParentSandstone',collection=coll,symmetric=False)
    rock['source_method']='Approved asymmetric parent; finite bed-controlled retreat; two detached hard remnants; planar open spalls with irregular end boundaries at bay and rear top'
    rock['voxel_m']=voxel;rock['micrograin_present']=False
    return rock

def build():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
    sc.render.threads_mode='FIXED';sc.render.threads=6
    coll=B.collection('SOURCE_HIGH_EDITABLE');rock=parent_volume(coll);rock.data.materials.append(B.clay_material())
    bpy.context.view_layer.update();stage=HERE/'stages/07-directional-spalls';stage.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-high.blend'),compress=True)
    views=dict(B.VIEWS);views['top']=((.5,-.5,10),(0,0,.8),45)
    views['detail']=((2.6,-6,2.8),(.15,-.45,.85),62);views['rear-detail']=((-4.1,5.4,2.6),(-.38,.69,1.03),56)
    B.renders(stage,prefix='clay-',views=views)
    result={'version':bpy.app.version_string,'stage':'07-directional-spalls','voxel_m':.014,
        'triangles':sum(len(p.vertices)-2 for p in rock.data.polygons),'dimensions':list(rock.dimensions),
        'micrograin':False,'midscale_erosion':'local 2–20 cm bed/rim losses','visual_gate':'PENDING_OPEN_IMAGES_AND_INDEPENDENT_REVIEW'}
    (stage/'stage.json').write_text(json.dumps(result,indent=2));print('RESULT_JSON='+json.dumps(result))

if __name__=='__main__':build()
