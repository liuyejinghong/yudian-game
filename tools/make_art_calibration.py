#!/usr/bin/env python3
"""生成原创、不代表生产美术的米制 GLB 校准件及可编辑 glTF 源。"""
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[1]


def main():
    binary = bytearray()
    views, accessors, meshes = [], [], []

    def accessor(values, kind, component, fmt, bounds=None):
        while len(binary) % 4: binary.append(0)
        start = len(binary)
        flat = [v for row in values for v in row] if kind == 'VEC3' else values
        binary.extend(struct.pack('<' + fmt * len(flat), *flat))
        views.append({'buffer': 0, 'byteOffset': start, 'byteLength': len(binary) - start})
        item = {'bufferView': len(views)-1, 'componentType': component, 'count': len(values), 'type': kind}
        if bounds: item.update(min=bounds[0], max=bounds[1])
        accessors.append(item)
        return len(accessors)-1

    def box(name, minimum, maximum, material):
        x,y,z = minimum; X,Y,Z = maximum
        faces = [([(x,y,z),(X,y,z),(X,Y,z),(x,Y,z)],(0,0,-1)),
                 ([(X,y,Z),(x,y,Z),(x,Y,Z),(X,Y,Z)],(0,0,1)),
                 ([(x,y,Z),(x,y,z),(x,Y,z),(x,Y,Z)],(-1,0,0)),
                 ([(X,y,z),(X,y,Z),(X,Y,Z),(X,Y,z)],(1,0,0)),
                 ([(x,y,Z),(X,y,Z),(X,y,z),(x,y,z)],(0,-1,0)),
                 ([(x,Y,z),(X,Y,z),(X,Y,Z),(x,Y,Z)],(0,1,0))]
        vertices, normals, indices = [], [], []
        for points, normal in faces:
            start=len(vertices); vertices.extend(points); normals.extend([normal]*4)
            indices.extend([start,start+2,start+1,start,start+3,start+2])
        pos=accessor(vertices,'VEC3',5126,'f',(minimum,maximum))
        norm=accessor(normals,'VEC3',5126,'f')
        idx=accessor(indices,'SCALAR',5123,'H')
        meshes.append({'name':name,'primitives':[{'attributes':{'POSITION':pos,'NORMAL':norm},'indices':idx,'material':material}]})
        return len(meshes)-1

    body=box('Body',[-.45,0,-.625],[.45,.7,.625],0)
    nose=box('ForwardMarker',[-.12,.25,-1],[.12,.45,-.625],1)
    work=box('WorkPart',[-.2,0,-.2],[.2,.12,.2],1)
    nodes=[{'name':'CalibrationAsset','children':list(range(1,8))},
           {'name':'Body','mesh':body}, {'name':'ForwardMarker','mesh':nose},
           {'name':'WorkPart','mesh':work,'translation':[0,.75,0]},
           {'name':'Socket_Cargo','translation':[0,.7,.2]},
           {'name':'Socket_TowFront','translation':[0,.2,-.625]},
           {'name':'Socket_TowRear','translation':[0,.2,.625]},
           {'name':'Socket_Charge','translation':[.45,.25,0]}]
    doc={'asset':{'version':'2.0','generator':'Yudian ART-I00 calibration'},'scene':0,'scenes':[{'nodes':[0]}],
         'nodes':nodes,'meshes':meshes,'buffers':[{'byteLength':len(binary)}], 'bufferViews':views,'accessors':accessors,
         'materials':[{'name':'CalibrationShell','pbrMetallicRoughness':{'baseColorFactor':[.8,.82,.85,1],'metallicFactor':0,'roughnessFactor':1}},
                      {'name':'ForwardAndWorkMarker','pbrMetallicRoughness':{'baseColorFactor':[1,.55,.1,1],'metallicFactor':0,'roughnessFactor':1}}]}
    source=ROOT/'art/source/calibration'; source.mkdir(parents=True,exist_ok=True)
    editable=json.loads(json.dumps(doc)); editable['buffers'][0]['uri']='calibration.bin'
    (source/'calibration.gltf').write_text(json.dumps(editable,indent=2)+'\n'); (source/'calibration.bin').write_bytes(binary)
    js=json.dumps(doc,separators=(',',':')).encode(); js+=b' '*((-len(js))%4)
    data=bytes(binary)+b'\0'*((-len(binary))%4)
    glb=struct.pack('<4sII',b'glTF',2,12+8+len(js)+8+len(data))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(data),b'BIN\0')+data
    out=ROOT/'prototype/assets/calibration/art_i00.glb'; out.parent.mkdir(parents=True,exist_ok=True); out.write_bytes(glb)
    manifest={'id':'art-i00-calibration','revision':1,'purpose':'interface calibration only; not Tuoyun production art',
              'unit':'meter','up':'+Y','forward':'-Z','ground_origin':[0,0,0],'body_size_m':[.9,.7,1.25],
              'sockets':{n['name']:n['translation'] for n in nodes if n['name'].startswith('Socket_')},
              'animations':{'status':'N/A calibration; preview bridge supplies work motion only'},
              'source':'original procedural calibration, editable glTF + binary + generator; no third party content',
              'runtime_authority':'none; no collision/navigation/energy/cargo/inventory rules',
              'sha256':hashlib.sha256(glb).hexdigest(),'triangles':36,'materials':2,'textures':0}
    (ROOT/'art/manifests/art_i00.json').write_text(json.dumps(manifest,indent=2)+'\n')


if __name__=='__main__': main()
