"""Read-only renders of delivered PBR objects; exact material views and joint contact."""
import bpy,sys
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P
source=HERE/'mars-outcrop-hifi-r2.blend';ground=HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.blend'
hero_id=P.identity(source);ground_id=P.identity(ground)
bpy.ops.wm.open_mainfile(filepath=str(source))
views={'front':B.VIEWS['front'],'detail':((2.6,-6,2.8),(.15,-.45,.85),62),
       'rear-detail':((-4.1,5.4,2.6),(-.38,.69,1.03),56),'bed-close':((-1.7,-4.0,1.8),(-1.45,-1.02,1.08),72)}
B.renders(HERE/'renders',prefix='baked-',views=views)
bpy.ops.wm.open_mainfile(filepath=str(ground))
with bpy.data.libraries.load(str(source),link=False) as (src,dst):dst.objects=['MarsOutcrop']
hero=dst.objects[0];bpy.context.scene.collection.objects.link(hero);hero.hide_render=False;hero.hide_set(False)
B.renders(HERE/'renders',prefix='combined-',views={'front':((9,-12,5.1),(0,-.2,.55),52),
    'low':((3.8,-7.5,1.35),(0,-.6,.7),42),'reverse':((-7,10,3.6),(0,0,.7),49)})
assert P.identity(source)==hero_id and P.identity(ground)==ground_id
P.write_json(HERE/'verification/final-render-inputs.json',{'outcrop':hero_id,'ground':ground_id,
 'source_files_changed':False,'method':'Delivered GAME_EXPORT PBR objects; same four material preview cameras, plus three combined contact cameras; no runtime material override'})
