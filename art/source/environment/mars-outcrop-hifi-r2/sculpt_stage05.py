"""Local 5–30 cm erosion, unequal remnant plates; no grain or surface material."""
import bpy,json,sys
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import sculpt_stage03 as P
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
sc.render.threads_mode='FIXED';sc.render.threads=6
coll=B.collection('SOURCE_HIGH_EDITABLE');rock=P.parent_volume(coll,voxel=.012,detail=True,weathered=True)
rock.data.materials.append(B.clay_material());bpy.context.view_layer.update()
stage=HERE/'stages/05-midscale-erosion';stage.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-high.blend'),compress=True)
views=dict(B.VIEWS);views['top']=((.5,-.5,10),(0,0,.8),45)
views['detail']=((2.6,-6,2.8),(.15,-.45,.85),62)
views['rear-detail']=((-4.1,5.4,2.6),(-.38,.69,1.03),56)
B.renders(stage,prefix='clay-',views=views)
result={'version':bpy.app.version_string,'stage':'05-midscale-erosion','voxel_m':.012,
        'triangles':sum(len(p.vertices)-2 for p in rock.data.polygons),'dimensions':list(rock.dimensions),
        'grain_or_noise':False,'visual_gate':'PENDING_OPEN_IMAGES_AND_INDEPENDENT_REVIEW'}
(stage/'stage.json').write_text(json.dumps(result,indent=2));print('RESULT_JSON='+json.dumps(result))
