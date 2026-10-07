"""Architect-reviewed form reset: one asymmetric eroded parent rock.
Only primary and secondary form. Deliberately no grain/noise displacement at this gate.
"""
import bpy, json, sys
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE));import build as B
sys.path.append(str(B.ROOT/'.agents/skills/scenario-blender-sculpting/scripts'))
import bx_sculpt as S

def layered_recess(x,y,z,broken=False):
    """Nine authored resistant edges on exposed front; ranges stop locally.
    These are medium form, not a periodic coordinate texture.
    """
    r=np.zeros(np.broadcast_shapes(x.shape,y.shape,z.shape),np.float32)
    beds=[(.14,.03,-2.9,.6,.050,.025),(.31,.055,-2.7,-.25,.075,.035),
          (.48,.14,-2.5,.15,.060,.021),(.65,.16,-2.4,.5,.080,.024),
          (.83,.18,-2.7,.8,.054,.020),(1.09,-.10,-2.7,-.15,.072,.034),
          (1.26,-.13,-2.5,-.4,.043,.021),(1.51,-.16,-2.4,-.5,.064,.028),
          (1.75,-.18,-2.5,-.85,.030,.018),(.25,-.09,.55,2.7,.041,.03),
          (.43,-.11,.75,2.8,.034,.02),(.61,-.12,.9,2.6,.025,.02)]
    for i,(h,slope,a,b,amp,width) in enumerate(beds):
        gate=B.smooth(a,a+.18,x)*(1-B.smooth(b-.15,b,x))
        q=z-h-slope*(x+1.2)-.025*y
        # Thicker on one side, pinched-out at the other; a sharp lip and broad worn recess.
        w=width*(1+.35*np.clip((x-a)/(b-a),0,1))
        lip=np.exp(-((q)/w)**2)
        if broken:
            w=w*(.64+.60*B.smooth(a,b,x))
            lip=B.smooth(-w*1.3,-w*.93,q)*(1-B.smooth(w*.24,w*.48,q))
            gap=[-1.76,-2.31,-1.46,-1.92,-2.15,-1.33,-1.93,-1.72,-2.12,1.65,2.02,1.44][i]
            half=.045+.014*(i%4)
            gate=gate*B.smooth(half,half+.022,np.abs(x-gap-.17*q))
            gate=gate*B.smooth(a,a+.035,x+.75*q)*(1-B.smooth(b-.03,b,x-.55*q))
        hollow=np.exp(-((q+.07)/(w*2.5))**2)
        r+=gate*amp*(lip-.63*hollow)
    return r

def parent_volume(coll,voxel=.02,detail=False):
    lo=(-3.35,-2.4,-.26);hi=(3.35,2.15,2.42)
    c=S.Clay(lo,hi,voxel=voxel)
    def body(x,y,z):
        # A single crest at west, a broad saddle, then a long low eastern tongue.
        h=np.interp(x,[-3.2,-2.8,-2.20,-1.42,-.66,.05,.78,1.48,2.22,3.18],
                       [.58,1.05,1.88,2.12,1.78,1.57,1.49,1.09,.72,.28])
        h=h+.055*y-.18*np.maximum(-y-.35,0)-.14*np.maximum(y-.80,0)
        left=-3.02+.30*np.abs(y+.1)+.11*np.maximum(z-.45,0)
        right=3.13-.32*np.abs(y-.08)-.12*np.maximum(z-.3,0)
        # Sloped apron merging into the main cliff, different sections along x.
        front=-1.80+.12*x+.25*np.maximum(z-.17,0)+.34*B.smooth(.28,.63,z)
        front-=.21*np.exp(-((x+2.22)/.43)**2)*(1-B.smooth(.3,1.3,z))
        front+=.13*np.exp(-((x+1.3)/.7)**2)*B.smooth(.5,1.4,z)
        back=1.55-.15*np.abs(x+.5)-.065*z+.10*np.exp(-((x+.8)/1.0)**2)
        if detail:
            slip=1-B.smooth(-.022,.025,x-(-1.96+.29*y+.055*z))
            h=h-.075*slip
            front=front+.027*slip
            relief=layered_recess(x,y,z+.075*slip,broken=True)
        else:
            relief=layered_recess(x,y,z)
        front=front-relief
        # A limited weathered left exposure; the intact rear carries no wrap-around bands.
        left=left-relief*.43*(1-B.smooth(.3,1.0,y))
        d=np.maximum.reduce(np.broadcast_arrays(-z-.16,z-h,left-x,x-right,front-y,y-back))
        return d.astype(np.float32)
    c.add(S.Prim(body,lo,hi))
    # The erosion bay is open to the sky AND the toe, widening toward the front and bottom.
    def bay(x,y,z):
        center=.15+.13*z+.08*y
        width=.18+.20*(1-B.smooth(0,2,z))+.28*np.maximum(-y,0)
        rear=.20+.24*(1-B.smooth(0,1.8,z))
        if detail:
            center=center+np.interp(z,[-.2,.24,.64,.98,1.34,1.68,2.3],[.02,.02,-.05,.035,-.03,.065,.065])
            wl=width+np.interp(z,[-.2,.3,.46,.68,.83,1.12,1.30,1.64,2.3],[.03,.03,.12,.12,.015,.015,.075,.075,.11])
            wr=width+np.interp(z,[-.2,.24,.37,.65,.78,1.02,1.19,1.5,2.3],[.06,.06,.015,.015,.13,.13,.025,.025,.075])
            rear=rear+np.interp(z,[-.2,.38,.6,.93,1.13,1.58,2.3],[.02,.02,-.10,-.10,.065,.065,.10])+.08*x
            floor=-.10+.18*np.maximum(y+1.35,0)
            return np.maximum.reduce(np.broadcast_arrays(center-x-wl,x-center-wr,y-rear,floor-z))
        return np.maximum.reduce(np.broadcast_arrays(np.abs(x-center)-width,y-rear,-z-.04))
    c.sub(S.Prim(bay,lo,hi),blend=.025)
    # One oblique collapse plane beside the bay; an exposed wedge, never a framed window.
    def collapsed_wedge(x,y,z):
        plane=.31*(x-.80)-.61*(y+.45)+.74*(z-.95)
        return np.maximum.reduce(np.broadcast_arrays(.48-x,-plane,y-.05))
    c.sub(S.Prim(collapsed_wedge,lo,hi),blend=.012)
    # Two offset non-identical joints. Neither bisects every visible crown.
    def west_joint(x,y,z):
        at=-1.96+.29*y+.055*z
        width=.018+.022*B.smooth(.6,2.1,z)+.016*np.maximum(-y,0)
        if detail:
            at=at+np.interp(z,[0,.6,1,1.3,1.6,2.3],[0,0,.027,-.027,.041,.07])+.035*np.clip(y+.25,-.5,.65)
            width=width+.074*np.exp(-(((y+.62)/.23)**4+((z-1.35)/.22)**4))+.045*np.exp(-(((y-.65)/.27)**4+((z-1.68)/.31)**4))
            width=width*B.smooth(.34-.17*y,.58-.17*y,z)
        return np.maximum.reduce(np.broadcast_arrays(np.abs(x-at)-width,.34-.17*y-z))
    c.sub(S.Prim(west_joint,lo,hi))
    def east_joint(x,y,z):
        at=1.87-.43*y+.075*z
        width=.025+.013*np.maximum(-y,0)
        if detail:
            at=at+np.interp(y,[-2,-1.1,-.72,-.1,.5,1.6],[.02,.02,-.045,-.045,.035,.035])
            width=width+.05*np.exp(-(((y+.72)/.30)**4+((z-.42)/.26)**4))
            width=width*B.smooth(.14+.12*y,.36+.12*y,z)
        return np.maximum.reduce(np.broadcast_arrays(np.abs(x-at)-width,.14+.12*y-z))
    c.sub(S.Prim(east_joint,lo,hi))
    if detail:
        # Each small open spall intersects an existing exposed rim; never a framed hole.
        chips=[((-.08,-.36,1.51),(.15,.22,.12),(1,.35,-.5)),
               ((.78,-.14,1.18),(.20,.22,.14),(-.8,.55,.4)),
               ((.71,-.58,.87),(.19,.24,.095),(.6,-.9,.5)),
               ((-1.85,.43,2.035),(.16,.24,.15),(.8,-.4,.8)),
               ((-2.33,-.60,1.56),(.16,.19,.10),(.2,.7,-.6))]
        for center,half,normal in chips:
            n=np.asarray(normal)/np.linalg.norm(normal)
            def spall(x,y,z,center=center,half=half,n=n):
                dx=x-center[0];dy=y-center[1];dz=z-center[2]
                return np.maximum.reduce(np.broadcast_arrays(np.abs(dx)-half[0],np.abs(dy)-half[1],np.abs(dz)-half[2],dx*n[0]+dy*n[1]+dz*n[2]-.025))
            c.sub(S.Prim(spall,lo,hi),blend=.005)
    rock=c.to_object('HIGH_ParentSandstone',collection=coll,symmetric=False)
    rock['source_method']='One continuous asymmetric parent volume; open erosion bay; one oblique collapse wedge; two offset joints; finite authored cross-bed edges'
    rock['voxel_m']=voxel;rock['grain_detail_present']=False
    return rock

def build():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
    sc.render.threads_mode='FIXED';sc.render.threads=6
    coll=B.collection('SOURCE_HIGH_EDITABLE');rock=parent_volume(coll);rock.data.materials.append(B.clay_material())
    bpy.context.view_layer.update();stage=HERE/'stages/03-parent-form';stage.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-mid.blend'),compress=True)
    B.R.review([rock],str(stage/'silhouette'),views=('front','back','top','threequarter'),modes=('silhouette',),res=500)
    views=dict(B.VIEWS);views['top']=((.5,-.5,10),(0,0,.8),45)
    views['detail']=((2.6,-6.0,2.8),(.15,-.45,.85),62)
    B.renders(stage,prefix='clay-',views=views)
    result={'version':bpy.app.version_string,'stage':'03-parent-form','voxel_m':.02,
            'triangles':sum(len(p.vertices)-2 for p in rock.data.polygons),'dimensions':list(rock.dimensions),
            'grain_or_noise':False,'visual_gate':'PENDING_OPEN_IMAGES_AND_INDEPENDENT_REVIEW'}
    (stage/'stage.json').write_text(json.dumps(result,indent=2));print('RESULT_JSON='+json.dumps(result))

if __name__=='__main__':build()
