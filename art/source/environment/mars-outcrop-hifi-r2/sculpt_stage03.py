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

def facet(u,v):
    """Broad angular weathering loss: an open retreat, not a bounded cavity cutter."""
    return np.maximum(0,1-np.abs(u)-np.abs(v))

def weathered_beds(x,y,z):
    r=np.zeros(np.broadcast_shapes(x.shape,y.shape,z.shape),np.float32)
    # Entire remnants have different thickness, reach and loss histories.
    beds=[(.19,.035,-2.91,-.12,.050,.026,0),
          (.39,.13,-2.69,.03,.083,.051,1),
          (.63,.18,-2.47,.22,.033,.017,2),
          (.90,.17,-2.53,-.63,.072,.030,0),
          (1.19,-.12,-2.41,-.24,.111,.061,3),
          (1.53,-.16,-2.27,-.55,.049,.022,1),
          (1.79,-.18,-2.38,-1.21,.036,.019,0),
          (.30,-.095,.87,2.71,.050,.033,2),
          (.54,-.14,1.05,2.49,.031,.017,0)]
    for i,(h,slope,a,b,amp,w,loss) in enumerate(beds):
        bend=.018*np.interp(x,[a,a+(b-a)*.31,a+(b-a)*.74,b],[0,-1,.7,-.3])
        q=z-h-slope*(x+1.2)-.025*y-bend
        width=w*(.48+.77*B.smooth(a,b,x))
        lip=B.smooth(-width,-width*.67,q)*(1-B.smooth(width*.33,width*.53,q))
        gate=B.smooth(a,a+.05,x+.45*q)*(1-B.smooth(b-.10,b,x-.8*q))
        if loss==1:
            # A whole weak end has fallen, leaving a shorter overhanging plate.
            gate=gate*B.smooth(a+.36,a+.45,x+.40*q)
        elif loss==2:
            gate=gate*(1-B.smooth(a+(b-a)*.54,a+(b-a)*.64,x-.45*q))
        elif loss==3:
            gate=gate*(.38+.62*B.smooth(a+.48,a+.59,x))
        trough=np.exp(-((q+width*1.9)/(width*1.3))**2)
        r=r+gate*amp*(lip-.42*trough)
    return r

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

def parent_volume(coll,voxel=.02,detail=False,weathered=False):
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
        if weathered:
            # Back-wall spall fans grow into the upper edge and cut the long rim.
            back=back-.23*facet((x+.63)/1.22,(z-1.35-.20*x)/1.20)
            back=back-.16*facet((x-1.24)/.88,(z-.58+.27*x)/.94)
            back=back-.095*B.smooth(.56,1.32,z)*np.exp(-((x+.90)/.48)**4)
            back=back+.044*np.exp(-((z-.69+.18*x)/.035)**2)*(B.smooth(-1.77,-1.63,x)*(1-B.smooth(-.41,-.25,x)))
            h=h-.13*facet((x+.57)/.81,(y-1.0)/.58)-.085*facet((x+1.31)/.64,(y+.71)/.45)
            # Two unequal open plate losses on the eastern tongue and its front edge.
            front=front+.20*facet((x-1.40)/.76,(z-.39+.30*(x-1.4))/.48)
            front=front+.11*facet((x-2.45)/.57,(z-.27)/.38)
            h=h-.14*facet((x-1.77)/.78,(y+.88)/.62)-.10*facet((x-2.63)/.57,(y-.07)/.67)
            front=front+.10*facet((x+1.25)/.72,(z-1.29)/.55)
        if detail:
            slip=1-B.smooth(-.022,.025,x-(-1.96+.29*y+.055*z))
            h=h-.075*slip
            front=front+.027*slip
            relief=weathered_beds(x,y,z+.075*slip) if weathered else layered_recess(x,y,z+.075*slip,broken=True)
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
        if weathered:
            # Nonparallel retreat surfaces vary both along depth and height.
            center=center+.055*np.interp(z,[0,.4,.91,1.37,2.3],[.1,-.7,.5,-.25,.4])
            wl=width+.19*facet((y+.43)/.81,(z-.79-.28*y)/.63)
            wl=wl+.13*facet((y+.06)/.45,(z-1.43+.31*y)/.37)
            wr=width+.17*facet((y+.61)/.84,(z-.40+.25*y)/.54)
            wr=wr+.20*facet((y+.19)/.61,(z-1.06-.28*y)/.58)
            rear=.28-.10*z+.10*x+.20*facet((x-.25)/.53,(z-.77)/.57)
            floor=-.10+.18*np.maximum(y+1.35,0)
            return np.maximum.reduce(np.broadcast_arrays(center-x-wl,x-center-wr,y-rear,floor-z))
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
        if weathered:
            plane=plane+.15*facet((x-.92)/.66,(y+.74)/.62)-.075*facet((x-1.61)/.71,(y+.24)/.61)
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
    if detail and not weathered:
        # Each small open spall intersects an existing exposed rim; never a framed hole.
        chips=[((-.08,-.36,1.51),(.15,.22,.12),(1,.35,-.5)),
               ((.78,-.14,1.18),(.20,.22,.14),(-.8,.55,.4))]
        for center,half,normal in chips:
            n=np.asarray(normal)/np.linalg.norm(normal)
            def spall(x,y,z,center=center,half=half,n=n):
                dx=x-center[0];dy=y-center[1];dz=z-center[2]
                return np.maximum.reduce(np.broadcast_arrays(np.abs(dx+.4*dz)-half[0],np.abs(dy-.25*dx)-half[1],np.abs(dz+.3*dx+.14*dy)-half[2],dx*n[0]+dy*n[1]+dz*n[2]-.025))
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
