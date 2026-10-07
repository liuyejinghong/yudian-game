"""Strip local paths from delivery metadata and prove all visual data stays identical."""
import bpy,sys,json,hashlib,struct,runpy,os
from pathlib import Path
import numpy as np
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import pipeline as P

def digest(value):return hashlib.sha256(value).hexdigest()

def values(items,name,width,dtype):
    data=np.empty(len(items)*width,dtype=dtype);items.foreach_get(name,data);return data.tobytes()

def source_visual_identity():
    meshes={}
    for mesh in bpy.data.meshes:
        chunks=[values(mesh.vertices,'co',3,np.float32),values(mesh.loops,'vertex_index',1,np.int32),
            values(mesh.polygons,'loop_start',1,np.int32),values(mesh.polygons,'loop_total',1,np.int32),
            values(mesh.polygons,'material_index',1,np.int32),values(mesh.polygons,'use_smooth',1,np.bool_)]
        uv={layer.name:digest(values(layer.data,'uv',2,np.float32)) for layer in mesh.uv_layers}
        meshes[mesh.name]={'geometry':digest(b''.join(chunks)),'uv':uv,'materials':[m.name if m else None for m in mesh.materials]}
    objects={o.name:{'data':o.data.name if o.data else None,'matrix':[float(v) for row in o.matrix_world for v in row],
        'hide_render':o.hide_render,'hide_viewport':o.hide_viewport,
        'view_layer_hidden':{v.name:o.hide_get(view_layer=v) for v in bpy.context.scene.view_layers if o.name in v.objects},
        'modifiers':[{'name':m.name,'type':m.type,'show_viewport':m.show_viewport,'show_render':m.show_render} for m in o.modifiers]}
        for o in bpy.data.objects}
    collections={c.name:{'objects':sorted(o.name for o in c.objects),'children':sorted(x.name for x in c.children),
        'hide_render':c.hide_render,'hide_viewport':c.hide_viewport} for c in bpy.data.collections}
    materials={}
    for material in bpy.data.materials:
        if not material.use_nodes:continue
        nt=material.node_tree;nodes=[]
        for node in nt.nodes:
            inputs={}
            for socket in node.inputs:
                if hasattr(socket,'default_value'):
                    v=socket.default_value
                    if isinstance(v,(str,int,float,bool)):inputs[socket.identifier]=v
                    elif hasattr(v,'__iter__'):inputs[socket.identifier]=list(v)
            nodes.append({'name':node.name,'type':node.bl_idname,'inputs':inputs,
                'image':node.image.name if hasattr(node,'image') and node.image else None})
        links=sorted((l.from_node.name,l.from_socket.identifier,l.to_node.name,l.to_socket.identifier) for l in nt.links)
        materials[material.name]=digest(json.dumps({'nodes':nodes,'links':links},sort_keys=True).encode())
    images={i.name:digest(bytes(i.packed_file.data)) for i in bpy.data.images if i.packed_file}
    return {'meshes':meshes,'objects':objects,'collections':collections,'material_graphs':materials,'packed_images':images}

def read_glb(path):
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+n])
    pos=20+n;length,kind=struct.unpack_from('<I4s',blob,pos);assert kind==b'BIN\x00'
    return doc,blob[pos+8:pos+8+length]

def glb_visual_identity(path):
    doc,buffer=read_glb(path);images=[];accessors=[]
    for image in doc.get('images',[]):
        view=doc['bufferViews'][image['bufferView']];offset=view.get('byteOffset',0)
        images.append({'name':image.get('name'),'sha256':digest(buffer[offset:offset+view['byteLength']])})
    sizes={5120:1,5121:1,5122:2,5123:2,5125:4,5126:4};widths={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
    for a in doc.get('accessors',[]):
        assert 'sparse' not in a
        v=doc['bufferViews'][a['bufferView']];size=sizes[a['componentType']]*widths[a['type']]
        stride=v.get('byteStride',size);offset=v.get('byteOffset',0)+a.get('byteOffset',0)
        payload=b''.join(buffer[offset+i*stride:offset+i*stride+size] for i in range(a['count']))
        accessors.append({'type':a['type'],'componentType':a['componentType'],'count':a['count'],'sha256':digest(payload)})
    return {'images':images,'accessors':accessors},doc

def assert_no_private_path(value):
    if isinstance(value,str):assert '/Users/' not in value,'Local user path remains'
    elif hasattr(value,'keys'):
        for v in value.values():assert_no_private_path(v)
    elif isinstance(value,(tuple,list)):
        for v in value:assert_no_private_path(v)

def without_extras(value):
    if isinstance(value,dict):return {k:without_extras(v) for k,v in value.items() if k!='extras'}
    if isinstance(value,list):return [without_extras(v) for v in value]
    return value

def keep_visual_buffer(path,original_doc,original_buffer):
    """A metadata edit must not adopt the exporter's newly calculated tangents."""
    document,_=read_glb(path)
    assert without_extras(document)==without_extras(original_doc),'Non-metadata GLB structure changed'
    payload=json.dumps(document,separators=(',',':'),ensure_ascii=False).encode()
    payload+=b' '*((-len(payload))%4)
    total=12+8+len(payload)+8+len(original_buffer)
    path.write_bytes(struct.pack('<4sII',b'glTF',2,total)+struct.pack('<I4s',len(payload),b'JSON')+payload+
        struct.pack('<I4s',len(original_buffer),b'BIN\x00')+original_buffer)

for asset,folder in [('outcrop',HERE),('ground',HERE.parent/'mars-ground-patch-hifi-r2')]:
    stem=folder.name;blend=folder/(stem+'.blend');glb=folder/(stem+'.glb')
    backup=folder/'stages/metadata-before-portable-paths'
    assert (backup/blend.name).exists() and (backup/glb.name).exists(),'Back up the frozen delivery first'
    old_blend=P.identity(backup/blend.name);old_glb=P.identity(backup/glb.name)
    before_glb,original_doc=glb_visual_identity(backup/glb.name);_,original_buffer=read_glb(backup/glb.name)
    bpy.ops.wm.open_mainfile(filepath=str(backup/blend.name));before=source_visual_identity()
    retained=[]
    clay=bpy.data.materials.get('REVIEW_Neutral_Clay')
    if clay and clay.users==0 and not clay.use_fake_user:
        clay.use_fake_user=True;retained.append({'material':clay.name,'fake_user_before':False,'fake_user_after':True})
    changed=P.portable_metadata();image_paths=[]
    for image in bpy.data.images:
        if image.filepath.startswith(str(P.B.ROOT)+'/'):
            image.filepath='//'+os.path.relpath(image.filepath,folder);image_paths.append(image.name)
    for blocks in (bpy.data.objects,bpy.data.meshes,bpy.data.materials):
        for block in blocks:assert_no_private_path(dict(block.items()))
    assert before==source_visual_identity(),'Sanitizing metadata changed source visual data'
    P.export(list(bpy.data.collections['GAME_EXPORT'].objects),glb)
    keep_visual_buffer(glb,original_doc,original_buffer)
    after_glb,document=glb_visual_identity(glb)
    assert before_glb==after_glb,'GLB geometry/UV/normal/tangent/index or embedded image bytes changed'
    assert_no_private_path(document)
    for name,state in before['objects'].items():
        obj=bpy.data.objects[name];obj.hide_render=state['hide_render'];obj.hide_viewport=state['hide_viewport']
        for layer,hidden in state['view_layer_hidden'].items():obj.hide_set(hidden,view_layer=bpy.context.scene.view_layers[layer])
    assert before==source_visual_identity(),'Export changed source visual data before saving'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True,relative_remap=False)
    bpy.ops.wm.open_mainfile(filepath=str(blend));after=source_visual_identity()
    assert before==after,'Saved source visual data changed'
    record={'status':'PASS','operation':'Custom-property and image-reference paths only; no geometry, UV, material graph or image content edit',
        'changes':changed,'image_references_made_relative':image_paths,
        'ui_retention_flags':retained,
        'before_files':{blend.name:old_blend,glb.name:old_glb},
        'after_files':{blend.name:P.identity(blend),glb.name:P.identity(glb)},
        'source_visual_identity_unchanged':True,'glb_visual_identity_unchanged':True,
        'glb_binary_payload_unchanged':True,'binary_preservation':'Native re-exported JSON, non-extras structure matched exactly; original complete visual BIN retained to avoid tangent recalculation differences',
        'source_visual_identity':after,'glb_visual_identity':after_glb,'glb_user_paths_remaining':0}
    P.write_json(folder/'verification/metadata-portability.json',record)
    manifest=json.loads((folder/'manifest.json').read_text());manifest['files']=record['after_files']
    manifest['metadata_portability']='verification/metadata-portability.json'
    P.write_json(folder/'manifest.json',manifest)
    sys.argv=['verify_final.py','--asset',asset]
    runpy.run_path(str(HERE/'verify_final.py'),run_name='__main__')
    print('PORTABLE_METADATA_PASS='+json.dumps({'asset':asset,'changes':len(changed),'files':record['after_files'],
        'embedded_images_unchanged':len(after_glb['images']),'mesh_accessors_unchanged':len(after_glb['accessors'])}),flush=True)
