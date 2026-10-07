"""Make only source output metadata portable; prove every visible input unchanged."""
import bpy,sys,json,hashlib
from pathlib import Path
h=Path(__file__).resolve().parent;sys.path.insert(0,str(h));import pipeline as P;import input_identity as I
source=h/'mars-outcrop-hifi-r2.blend';glb=h/'mars-outcrop-hifi-r2.glb';before_file=P.identity(source);glb_identity=P.identity(glb)
textures={p.name:P.identity(p) for p in (h/'textures').glob('*.png')}
def snapshot():
 high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
 return {'inputs':I.inputs(high,low),'game_material':I.material(low.data.materials[0]),'images':{i.name:{'filepath':i.filepath,'size':list(i.size),'colorspace':i.colorspace_settings.name,'packed_sha256':hashlib.sha256(i.packed_file.data).hexdigest()} for i in bpy.data.images if i.packed_file}}
bpy.ops.wm.open_mainfile(filepath=str(source));before=snapshot();changes=P.portable_metadata()
for scene in bpy.data.scenes:
 old=scene.render.filepath
 if old.startswith('/') and not old.startswith('//'):
  scene.render.filepath='//renders/'+Path(old).name;changes.append({'owner':'scene:'+scene.name,'property':'render.filepath','old_was_absolute':True,'value':scene.render.filepath})
if changes:bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True,relative_remap=False)
bpy.ops.wm.open_mainfile(filepath=str(source));after=snapshot();assert before==after,'Visible or editable asset input changed during metadata save'
assert all(i['filepath'].startswith('//textures/') for i in after['images'].values())
assert P.identity(glb)==glb_identity and all(P.identity(h/'textures'/name)==v for name,v in textures.items())
assert source.stat().st_size<104857600
P.write_json(h/'verification/delivery-inputs.json',{'status':'PASS','source':P.identity(source),'glb':glb_identity,**after['inputs']})
P.write_json(h/'verification/source-portability.json',{'status':'PASS','source_before':before_file,'source_after':P.identity(source),'metadata_changes':changes,'all_visible_mesh_uv_normals_colors_material_graphs_and_packed_images_identical':True,'glb_and_png_bytes_unchanged':True,'source_images':after['images'],'under_100MiB':True})
proof=json.loads((h/'verification/formal-promotion.json').read_text());proof['new_formal_files'][source.name]=P.identity(source);proof['source_resaved']=bool(changes);proof['source_metadata_proof']='verification/source-portability.json';P.write_json(h/'verification/formal-promotion.json',proof)
print('RESULT_JSON='+json.dumps({'source':P.identity(source),'metadata_changes':changes,'visible_inputs_identical':True}),flush=True)
