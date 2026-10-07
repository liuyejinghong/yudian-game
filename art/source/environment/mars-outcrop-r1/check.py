"""One executable asset contract check, usable with Python or a reopened Blender.

python3 check.py
blender --background mars-outcrop-r1.blend --python-exit-code 1 --python check.py
The Blender invocation also exports a fresh GLB and compares decoded payloads.
"""
import hashlib
import json
import math
from pathlib import Path
import struct

try:
    import bpy
except ImportError:
    bpy=None

HERE=Path(__file__).resolve().parent
NAME='mars-outcrop-r1'
REPORT='check-report.json' if bpy is not None else 'glb-check-report.json'


def read_glb(path):
    raw=path.read_bytes()
    magic,version,total=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and total==len(raw),'Invalid GLB envelope'
    size,kind=struct.unpack_from('<I4s',raw,12)
    assert kind==b'JSON'
    doc=json.loads(raw[20:20+size])
    offset=20+size
    binary_size,kind=struct.unpack_from('<I4s',raw,offset)
    assert kind==b'BIN\0' and offset+8+binary_size==len(raw)
    return doc,raw[offset+8:]


def values(doc,binary,index):
    accessor=doc['accessors'][index];view=doc['bufferViews'][accessor['bufferView']]
    widths={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}
    codes={5126:'f',5125:'I',5123:'H',5121:'B'}
    fmt='<'+codes[accessor['componentType']]*widths[accessor['type']]
    width=struct.calcsize(fmt);stride=view.get('byteStride',width)
    offset=view.get('byteOffset',0)+accessor.get('byteOffset',0)
    return [struct.unpack_from(fmt,binary,offset+i*stride) for i in range(accessor['count'])]


def validate(path):
    doc,binary=read_glb(path)
    manifest=json.loads((HERE/'manifest.json').read_text())
    assert len(doc['meshes'])==1 and len(doc['materials'])==1
    assert not doc.get('cameras') and not doc.get('animations') and not doc.get('skins')
    assert 'KHR_lights_punctual' not in doc.get('extensions',{})
    for node in doc['nodes']:
        assert node.get('scale',[1,1,1])==[1,1,1]
        assert all(abs(v)<1e-7 for v in node.get('translation',[0,0,0]))
        assert node.get('rotation',[0,0,0,1])==[0,0,0,1]
        assert 'matrix' not in node
    points=[];triangle_count=0;normal_count=0
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            assert primitive.get('mode',4)==4
            attrs=primitive['attributes']
            assert {'POSITION','NORMAL','TEXCOORD_0','TANGENT'}<=attrs.keys()
            positions=values(doc,binary,attrs['POSITION']);points.extend(positions)
            assert all(math.isfinite(v) for p in positions for v in p)
            norms=values(doc,binary,attrs['NORMAL']);normal_count+=len(norms)
            assert all(abs(sum(v*v for v in p)-1)<.005 for p in norms),'Non-unit normals'
            tangents=values(doc,binary,attrs['TANGENT'])
            assert all(math.isfinite(v) for t in tangents for v in t),'Non-finite tangent'
            assert all(t[3] in (-1,1) and abs(sum(v*v for v in t[:3])-1)<.0003 for t in tangents),'Invalid tangent length or sign'
            assert all(abs(sum(n[i]*t[i] for i in range(3)))<.0003 for n,t in zip(norms,tangents)),'Tangent not orthogonal to normal'
            uv=values(doc,binary,attrs['TEXCOORD_0'])
            assert all(-.001<=v<=1.001 for p in uv for v in p),'UV outside atlas'
            indices=[i[0] for i in values(doc,binary,primitive['indices'])]
            assert len(indices)%3==0 and min(indices)>=0 and max(indices)<len(positions)
            triangle_count+=len(indices)//3
    assert triangle_count==manifest['triangles']
    assert 12000<=triangle_count<=65000,'Unexpected geometry budget'
    lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
    size=[hi[i]-lo[i] for i in range(3)]
    assert 5<=size[0]<=7 and 1.5<=size[1]<=2.4 and 3<=size[2]<=4.5,size
    assert -.20<=lo[1]<=-.08 and abs(lo[0]+hi[0])<.001 and abs(lo[2]+hi[2])<.001
    mb=manifest['bounds_blender'];expected_lo=[mb['min'][0],mb['min'][2],-mb['max'][1]]
    expected_hi=[mb['max'][0],mb['max'][2],-mb['min'][1]]
    assert max(abs(a-b) for a,b in zip(lo+hi,expected_lo+expected_hi))<.0001
    material=doc['materials'][0];pbr=material['pbrMetallicRoughness']
    assert pbr.get('metallicFactor',1)==0,'Natural rock became metal'
    assert 'baseColorTexture' in pbr and 'metallicRoughnessTexture' in pbr and 'normalTexture' in material
    image_info=[]
    for im in doc['images']:
        assert 'bufferView' in im and 'uri' not in im,'Texture not embedded'
        view=doc['bufferViews'][im['bufferView']]
        image=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        assert image[:8]==b'\x89PNG\r\n\x1a\n'
        w,h=struct.unpack_from('>II',image,16)
        assert (w,h)==(2048,2048)
        assert len(image)>20000,'Unexpectedly empty texture'
        image_info.append({'name':im.get('name'),'size':[w,h],'bytes':len(image)})
    assert len(image_info)==3
    return {'triangles':triangle_count,'export_vertices':len(points),'normals':normal_count,
            'bounds_gltf':{'min':lo,'max':hi,'size':size},'embedded_images':image_info,
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()},doc,binary


def main():
    verification=HERE/'verification';verification.mkdir(exist_ok=True)
    report=verification/REPORT
    report.write_text(json.dumps({'pass':False,'status':'running','sha256':hashlib.sha256((HERE/f'{NAME}.glb').read_bytes()).hexdigest()},indent=2)+'\n')
    result,doc,binary=validate(HERE/f'{NAME}.glb')
    if bpy is None:
        result['blend_reopen']='Not run by this Python invocation; run through Blender.'
    else:
        import bmesh
        import numpy as np
        assert Path(bpy.data.filepath).resolve()==HERE/f'{NAME}.blend','Open saved asset first'
        objects=[o for o in bpy.data.objects if o.type=='MESH']
        assert len(objects)==1 and len(bpy.data.objects)==1,'Asset source contains lookdev props'
        obj=objects[0]
        assert tuple(obj.location)==(0,0,0) and tuple(obj.scale)==(1,1,1)
        assert len(obj.vertex_groups)==20
        bm=bmesh.new();bm.from_mesh(obj.data)
        assert not any(not e.is_manifold for e in bm.edges),'Open/non-manifold mesh edge'
        components=0;seen=set()
        for vertex in bm.verts:
            if vertex in seen:continue
            components+=1;todo=[vertex];seen.add(vertex)
            while todo:
                current=todo.pop()
                for edge in current.link_edges:
                    other=edge.other_vert(current)
                    if other not in seen:seen.add(other);todo.append(other)
        bm.free();assert components==20,components
        packed=[im for im in bpy.data.images if im.name.startswith(NAME)]
        assert len(packed)==3 and all(im.packed_file and tuple(im.size)==(2048,2048) and im.filepath.startswith('//textures/') for im in packed)
        pixels=np.empty(2048*2048*4,dtype=np.float32)
        for im in packed:
            im.pixels.foreach_get(pixels)
            sampled=pixels.reshape(-1,4)[::193,:3]
            assert np.ptp(sampled)>.02,im.name
        del pixels
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        out=verification/'reopened.glb'
        bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',use_selection=True,
            export_yup=True,export_apply=True,export_extras=True,export_texcoords=True,
            export_normals=True,export_tangents=True,export_materials='EXPORT',
            export_cameras=False,export_lights=False,export_image_format='AUTO')
        reopened,new_doc,new_binary=validate(out)
        assert new_doc==doc,'Saved/reopened source exports different JSON'
        tangent_index=doc['meshes'][0]['primitives'][0]['attributes']['TANGENT']
        tangent_view=doc['accessors'][tangent_index]['bufferView']
        for i,view in enumerate(doc['bufferViews']):
            if i==tangent_view:continue
            start=view.get('byteOffset',0);end=start+view['byteLength']
            assert binary[start:end]==new_binary[start:end],'Changed non-tangent bytes in buffer view '+str(i)
        before=values(doc,binary,tangent_index);after=values(new_doc,new_binary,tangent_index)
        assert all(a[3]==b[3] for a,b in zip(before,after)),'Tangent handedness changed'
        deltas=[abs(a[i]-b[i]) for a,b in zip(before,after) for i in range(3)]
        # Blender's exporter rounds tangent xyz to four decimal places. A few
        # values straddle a rounding boundary between independent processes.
        maximum=max(deltas);changed=sum(d>0 for d in deltas)
        assert maximum<=.00011,'Tangent change exceeds observed exporter rounding'
        result['blend_reopen']={'pass':True,'loose_components':components,'manifold':True,
                                'packed_original_textures':len(packed),'exact_binary':new_binary==binary,
                                'same_json_and_non_tangent_bytes':True,
                                'tangent_xyz_max_delta':maximum,'tangent_xyz_changed_components':changed,
                                'tangent_delta_limit':.00011,'same_tangent_handedness':True,
                                'reexport_sha256':reopened['sha256']}
        # Independent importer check: packed textures and orientation survive
        # GLB -> Blender as well as saved Blender -> GLB. Never save this scene.
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.gltf(filepath=str(out))
        imported=[o for o in bpy.data.objects if o.type=='MESH']
        assert len(imported)==1
        obj=imported[0];points=[obj.matrix_world@v.co for v in obj.data.vertices]
        lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
        expected=json.loads((HERE/'manifest.json').read_text())['bounds_blender']
        assert max(abs(a-b) for a,b in zip(lo+hi,expected['min']+expected['max']))<.0001
        assert sum(len(p.vertices)-2 for p in obj.data.polygons)==result['triangles']
        material=obj.data.materials[0]
        textures=[node.image for node in material.node_tree.nodes if node.type=='TEX_IMAGE']
        assert len(textures)==3 and all(tuple(im.size)==(2048,2048) for im in textures)
        shader=next(node for node in material.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
        assert all(shader.inputs[name].is_linked for name in ['Base Color','Roughness','Normal'])
        result['independent_glb_import']={'pass':True,'triangles':result['triangles'],'pbr_images':len(textures),'bounds_match':True}
    result['pass']=True
    report.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__=='__main__':
    try:main()
    except Exception as error:
        failure={'pass':False,'error':type(error).__name__+': '+str(error),
                 'sha256':hashlib.sha256((HERE/f'{NAME}.glb').read_bytes()).hexdigest()}
        (HERE/'verification'/REPORT).write_text(json.dumps(failure,indent=2)+'\n')
        raise
