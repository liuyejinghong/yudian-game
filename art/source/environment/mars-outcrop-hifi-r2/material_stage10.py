"""Material-only revision on immutable stage09 geometry. No UV or final bake."""
import bpy,sys,json,hashlib
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P
source=HERE/'stages/09-exposed-bed-material/source-high.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));rock=bpy.data.objects['HIGH_ParentSandstone']
P.surface(rock);P.geologic_finish(rock)
stage=HERE/'stages/10-unequal-lamina-preview';stage.mkdir(parents=True,exist_ok=True)
views={'front':B.VIEWS['front'],'detail':((2.6,-6,2.8),(.15,-.45,.85),62),
       'rear-detail':((-4.1,5.4,2.6),(-.38,.69,1.03),56),'bed-close':((-1.7,-4.0,1.8),(-1.45,-1.02,1.08),72)}
B.renders(stage,prefix='source-pbr-',views=views)
bpy.ops.wm.save_as_mainfile(filepath=str(stage/'source-preview.blend'),compress=True,relative_remap=False)
P.write_json(stage/'stage.json',{'stage':'10-material-only','geometry_source':str(source.relative_to(HERE)),
 'geometry_source_sha256':P.identity(source)['sha256'],'triangles':P.triangles(rock),'source_shader':rock['fine_surface_source'],
 'status':'PENDING_OPEN_IMAGE_AND_VISUAL_REVIEW'})
