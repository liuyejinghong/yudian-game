"""Bed-controlled differential weathering of the approved continuous parent volume.
Centimetre/decimetre losses are local geometry. No micrograin or texture is used.
"""
import bpy,json,sys,shutil
from datetime import datetime,timezone
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
sys.path.append(str(B.ROOT/'.agents/skills/scenario-blender-sculpting/scripts'))
import bx_sculpt as S

def beds(x,y,z):
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
    # Whole plate losses follow unequal oblique fractures through depth. Neither
    # footprint is an x/z rectangle extruded along y; upper/lower edges diverge.
    deep=y+1.12
    q=z+.13*(x+1.2)+.065*deep
    left=-1.70+.38*deep+.16*(z-1.1)
    right=-.79-.47*deep+.09*(z-1.1)
    peel=B.smooth(left,left+.028,x)*(1-B.smooth(right-.026,right,x))
    lower=1.045+.04*(x+1.2)-.04*deep
    upper=1.29-.065*(x+1.2)+.025*deep
    peel=peel*B.smooth(lower,lower+.014,q)*(1-B.smooth(upper-.015,upper,q))
    retreat=retreat+peel*(.135+.041*(x+1.2)-.055*deep)
    q=z-.16*(x+1.2)-.085*deep
    left=-2.23-.23*deep+.15*z
    right=-1.70-.52*deep-.13*z
    peel=B.smooth(left,left+.018,x)*(1-B.smooth(right-.031,right,x))
    lower=.465-.10*(x+2.0)+.016*deep
    upper=.695+.07*(x+2.0)-.052*deep
    peel=peel*B.smooth(lower,lower+.016,q)*(1-B.smooth(upper-.012,upper,q))
    retreat=retreat+peel*(.118-.056*(x+2.0)-.028*deep)
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
        loss=beds(x,y,z+.06*slip)
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
        return d
    c.sub(S.Prim(bay,lo,hi),blend=.005)
    def wedge(x,y,z):
        plane=.31*(x-.80)-.61*(y+.45)+.74*(z-.95)
        return np.maximum.reduce(np.broadcast_arrays(.48-x,-plane,y-.05))
    c.sub(S.Prim(wedge,lo,hi),blend=.012)
    # Replacements for stage07 boxed cutters: authored unequal open-edge depth
    # profiles meet the uncut wall at both ends. No independent straight side slot.
    def back_at(x,z):
        return 1.55-.15*np.abs(x+.5)-.065*z+.10*np.exp(-((x+.8)/1.0)**2)
    def rear_west_spall(x,y,z):
        depth=np.interp(x,[-2.10,-1.75,-1.46,-1.12,-.71,-.38],[0,.04,.24,.31,.09,0])
        fracture=1.57-.24*(x+1.1)-.72*(y-1.0)
        return np.maximum(back_at(x,z)-depth-y,fracture-z)
    def rear_east_spall(x,y,z):
        depth=np.interp(x,[.12,.31,.54,.83,1.20,1.62],[0,.11,.27,.15,.07,0])
        fracture=1.15-.20*(x-.75)-.57*(y-.93)
        return np.maximum(back_at(x,z)-depth-y,fracture-z)
    def bay_west_spall(x,y,z):
        center=.15+.13*z+.08*y
        width=.18+.20*(1-B.smooth(0,2,z))+.28*np.maximum(-y,0)
        depth=np.interp(y,[-1.10,-.62,-.32,.03,.27,.47],[0,.065,.22,.29,.17,0])
        wall=center-width-depth
        fracture=1.16+.26*y-.58*(x+.08)
        return np.maximum.reduce(np.broadcast_arrays(wall-x,x-center,fracture-z))
    def bay_east_spall(x,y,z):
        center=.15+.13*z+.08*y
        width=.18+.20*(1-B.smooth(0,2,z))+.28*np.maximum(-y,0)
        depth=np.interp(y,[-1.40,-1.13,-.84,-.39,-.09,.16],[0,.045,.21,.14,.07,0])
        wall=center+width+depth
        fracture=.75-.22*y+.65*(x-.67)
        return np.maximum.reduce(np.broadcast_arrays(x-wall,center-x,fracture-z))
    # The two failed rear-top cuts are withdrawn. The intact back face and the
    # existing variable-width joint remain; no replacement slot is invented.
    for f in (bay_west_spall,bay_east_spall):c.sub(S.Prim(f,lo,hi),blend=.004)
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
    rock['source_method']='Stage08 passed parent/bay/oblique plate losses retained; both failed rear-top cuts withdrawn to preserve the intact back face and existing joints'
    rock['voxel_m']=voxel;rock['micrograin_present']=False
    return rock

def build():
    stage=Path(sys.argv[sys.argv.index('--output-dir')+1]).resolve() if '--output-dir' in sys.argv else HERE/'stages/09-exposed-bed-material'
    if stage.exists() and any(stage.iterdir()):
        stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
        shutil.copytree(stage,HERE/'stages/rebuild-backups'/('09-'+stamp))
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
    sc.render.threads_mode='FIXED';sc.render.threads=6
    coll=B.collection('SOURCE_HIGH_EDITABLE');rock=parent_volume(coll);rock.data.materials.append(B.clay_material())
    bpy.context.view_layer.update();stage.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-high.blend'),compress=True)
    views=dict(B.VIEWS);views['top']=((.5,-.5,10),(0,0,.8),45)
    views['detail']=((2.6,-6,2.8),(.15,-.45,.85),62);views['rear-detail']=((-4.1,5.4,2.6),(-.38,.69,1.03),56)
    B.renders(stage,prefix='clay-',views=views)
    # Independent high-mesh preview; UV and final baking still await visual gate.
    import pipeline as P
    P.surface(rock);P.geologic_finish(rock)
    B.renders(stage,prefix='source-pbr-',views={k:views[k] for k in ('front','detail','rear-detail')})
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-preview.blend'),compress=True)
    result={'version':bpy.app.version_string,'stage':'09-exposed-bed-material','voxel_m':.014,
        'triangles':sum(len(p.vertices)-2 for p in rock.data.polygons),'dimensions':list(rock.dimensions),
        'micrograin':False,'midscale_erosion':'local 2–20 cm bed/rim losses','visual_gate':'PENDING_OPEN_IMAGES_AND_INDEPENDENT_REVIEW'}
    (stage/'stage.json').write_text(json.dumps(result,indent=2));print('RESULT_JSON='+json.dumps(result))

if __name__=='__main__':build()
