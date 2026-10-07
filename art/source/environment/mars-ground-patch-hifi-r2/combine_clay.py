"""Combined scale/contact preview only; exports no asset."""
import bpy,sys
from pathlib import Path
HERE=Path(__file__).resolve().parent;OUT=HERE.parent/'mars-outcrop-hifi-r2'
sys.path.insert(0,str(OUT));import build as B
bpy.ops.wm.open_mainfile(filepath=str(HERE/'stages/04-broken-clasts/ground-high.blend'))
with bpy.data.libraries.load(str(OUT/'stages/05-midscale-erosion/source-high.blend'),link=False) as (src,dst):
    dst.objects=['HIGH_ParentSandstone']
for o in dst.objects:bpy.context.scene.collection.objects.link(o)
B.renders(HERE/'stages/04-broken-clasts',prefix='combined-',views={'front':((9,-12,5.1),(0,-.2,.55),52),'low':((3.8,-7.5,1.35),(0,-.6,.7),42),'reverse':((-7,10,3.6),(0,0,.7),49)})
