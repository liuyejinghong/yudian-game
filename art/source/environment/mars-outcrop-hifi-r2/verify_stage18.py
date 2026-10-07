"""Read-only reopen of the separate stage18 source and independent GLB import."""
import bpy,json,sys,struct
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import pipeline as P
import build as B
STAGE=HERE/'stages/18-complete-face-2k';blend=STAGE/'candidate.blend';glb=STAGE/'candidate.glb'

def stats(objects):
    meshes=[o for o in objects if o.type=='MESH'];coords=[];tri=0;uv_missing=[]
    for o in meshes:
        coords.append(np.array([o.matrix_world@v.co for v in o.data.vertices]));tri+=P.triangles(o)
        if not o.data.uv_layers:uv_missing.append(o.name)
    co=np.concatenate(coords)
    return {'objects':len(meshes),'triangles':tri,'min':co.min(0).tolist(),'max':co.max(0).tolist(),
        'finite':bool(np.isfinite(co).all()),'uv_missing':uv_missing}

report=json.loads((STAGE/'stage.json').read_text());assert report['files']['candidate.blend']==P.identity(blend);assert report['files']['candidate.glb']==P.identity(glb)
bpy.ops.wm.open_mainfile(filepath=str(blend));source=stats(bpy.data.collections['GAME_EXPORT'].objects)
source['editable_high_triangles']=sum(P.triangles(o) for o in bpy.data.collections['SOURCE_HIGH_EDITABLE'].objects if o.type=='MESH')
assert source['triangles']==report['game_triangles'] and source['editable_high_triangles']==report['high_triangles']
assert source['finite'] and not source['uv_missing']
assert not [a.name for o in bpy.data.objects if o.type=='MESH' for a in o.data.attributes if a.name.startswith('stage17_')]
maps=[]
for im in bpy.data.images:
    if not im.name.startswith('stage18-'):continue
    assert im.packed_file and list(im.size)==[2048,2048]
    data=im.packed_file.data;assert data[:8]==b'\x89PNG\r\n\x1a\n' and data[24]==8
    maps.append({'name':im.name,'size':list(im.size),'packed':True,'colorspace':im.colorspace_settings.name,'png_bit_depth':data[24]})
assert len(maps)==3
assert glb.stat().st_size<100_000_000
payload=glb.read_bytes();magic,version,total=struct.unpack_from('<4sII',payload);assert magic==b'glTF' and version==2 and total==len(payload)
size,kind=struct.unpack_from('<I4s',payload,12);assert kind==b'JSON';doc=json.loads(payload[20:20+size]);assert '/Users/' not in json.dumps(doc)
bin_start=20+size+8;binary=payload[bin_start:]
assert len(doc['images'])==3
for im in doc['images']:
    assert 'uri' not in im and 'bufferView' in im;view=doc['bufferViews'][im['bufferView']];data=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
    assert data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack_from('>II',data,16)==(2048,2048) and data[24]==8
for mesh in doc['meshes']:
    for p in mesh['primitives']:assert all(name in p['attributes'] for name in ('POSITION','NORMAL','TEXCOORD_0','TANGENT'))
for mat in doc['materials']:
    pbr=mat['pbrMetallicRoughness'];assert 'normalTexture' in mat and 'baseColorTexture' in pbr and 'metallicRoughnessTexture' in pbr and pbr.get('metallicFactor',1)==0
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(glb));imported=stats(bpy.context.scene.objects)
assert imported['triangles']==source['triangles'] and imported['finite'] and not imported['uv_missing']
assert np.max(abs(np.array(imported['min'])-source['min']))<1e-4 and np.max(abs(np.array(imported['max'])-source['max']))<1e-4
assert all(P.identity(B.ROOT/path)==identity for path,identity in report['protected_files'].items())
proof={'status':'PASS','version':bpy.app.version_string,'source':{'path':str(blend.relative_to(B.ROOT)),**P.identity(blend)},
    'glb':{'path':str(glb.relative_to(B.ROOT)),**P.identity(glb)},'source_reopen':source,'source_maps':maps,
    'independent_glb_import':imported,'embedded_2k_rgb8_pngs':3,'pbr_and_tangents':True,
    'stage_attributes_removed':True,'source_under_100MiB':blend.stat().st_size<104857600,'formal_files_changed':False,'fixed_Godot_and_Sol':'PENDING_ROOT'}
P.write_json(STAGE/'native-import.json',proof);print('RESULT_JSON='+json.dumps(proof),flush=True)
