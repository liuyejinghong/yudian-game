"""Native saved-source reopen and independent empty-scene GLB import, both assets."""
import bpy,json,sys,hashlib,struct
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
asset=sys.argv[sys.argv.index('--asset')+1] if '--asset' in sys.argv else 'outcrop'
folder=HERE if asset=='outcrop' else HERE.parent/'mars-ground-patch-hifi-r2'
if '--asset-dir' in sys.argv:folder=Path(sys.argv[sys.argv.index('--asset-dir')+1]).resolve()
stem='mars-outcrop-hifi-r2' if asset=='outcrop' else 'mars-ground-patch-hifi-r2'
blend=folder/(stem+'.blend');glb=folder/(stem+'.glb')

def stats(objects):
    meshes=[o for o in objects if o.type=='MESH'];points=[];tri=0;uv_missing=[]
    for o in meshes:
        co=np.array([o.matrix_world@v.co for v in o.data.vertices]);points.append(co);tri+=sum(len(p.vertices)-2 for p in o.data.polygons)
        if not o.data.uv_layers:uv_missing.append(o.name)
    points=np.concatenate(points)
    return {'objects':len(meshes),'triangles':tri,'bounds_min':points.min(0).tolist(),'bounds_max':points.max(0).tolist(),
        'dimensions':(points.max(0)-points.min(0)).tolist(),'uv_missing':uv_missing,'finite':bool(np.isfinite(points).all())}

bpy.ops.wm.open_mainfile(filepath=str(blend));game=bpy.data.collections['GAME_EXPORT'];source=stats(game.objects)
source['high_editable_collection_present']='SOURCE_HIGH_EDITABLE' in bpy.data.collections
source['editable_high_triangles']=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.data.collections['SOURCE_HIGH_EDITABLE'].objects if o.type=='MESH')
source['textures']=[{'name':i.name,'width':i.size[0],'height':i.size[1],'packed':bool(i.packed_file),'colorspace':i.colorspace_settings.name}
    for i in bpy.data.images if i.name.startswith(('outcrop-normal','outcrop-base_color','outcrop-roughness','stage19-','ground-sand-','ground-clast-')) and 'test' not in i.name]
assert source['finite'] and not source['uv_missing'] and source['high_editable_collection_present']
assert source['textures'] and all(i['packed'] and i['width']==4096 and i['height']==4096 for i in source['textures'])
assert source['editable_high_triangles']>source['triangles']
for i in source['textures']:
    img=bpy.data.images[i['name']];p=Path(bpy.path.abspath(img.filepath));header=p.read_bytes()[:26]
    assert header[:8]==b'\x89PNG\r\n\x1a\n' and header[24]==8,(p,header)
    i['png_bit_depth']=header[24]
    assert img.filepath.startswith('//textures/') and img.packed_file
    assert hashlib.sha256(img.packed_file.data).hexdigest()==hashlib.sha256(p.read_bytes()).hexdigest()
    i['path']=str(p.relative_to(ROOT));i['sha256']=hashlib.sha256(p.read_bytes()).hexdigest()
assert blend.stat().st_size<104857600 and glb.stat().st_size<104857600
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(glb))
imported=stats(bpy.context.scene.objects)
assert imported['triangles']==source['triangles'],(source,imported)
assert np.max(np.abs(np.array(imported['bounds_min'])-source['bounds_min']))<1e-4
assert np.max(np.abs(np.array(imported['bounds_max'])-source['bounds_max']))<1e-4
assert imported['finite'] and not imported['uv_missing']
payload=glb.read_bytes();magic,version,total=struct.unpack_from('<4sII',payload);assert magic==b'glTF' and total==len(payload)
ln,kind=struct.unpack_from('<I4s',payload,12);document=json.loads(payload[20:20+ln])
assert '/Users/' not in json.dumps(document)
assert all('bufferView' in i and 'uri' not in i for i in document.get('images',[]))
materials=[]
for m in document.get('materials',[]):
    pbr=m.get('pbrMetallicRoughness',{})
    row={'name':m.get('name'),'base_color_texture':'baseColorTexture' in pbr,'normal_texture':'normalTexture' in m,
         'roughness_texture':'metallicRoughnessTexture' in pbr,'metallicFactor':pbr.get('metallicFactor',1)}
    assert row['base_color_texture'] and row['normal_texture'] and row['roughness_texture'] and row['metallicFactor']==0,row
    materials.append(row)
record={'status':'PASS','version':bpy.app.version_string,'source':str(blend.relative_to(ROOT)),'source_sha256':hashlib.sha256(blend.read_bytes()).hexdigest(),
        'glb':str(glb.relative_to(ROOT)),'glb_sha256':hashlib.sha256(payload).hexdigest(),'source_reopen':source,'independent_glb_import':imported,
        'embedded_images':len(document.get('images',[])),'materials':materials,'owner_final_visual':'NOT_RUN','Main_integration':'NOT_RUN','scene_performance':'NOT_RUN'}
record['file_bytes']={blend.name:blend.stat().st_size,glb.name:glb.stat().st_size}
if '--stdout-only' not in sys.argv:
    (folder/'verification').mkdir(exist_ok=True);(folder/'verification/final-native.json').write_text(json.dumps(record,indent=2))
print('RESULT_JSON='+json.dumps(record))
