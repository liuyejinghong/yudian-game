#!/usr/bin/env python3
"""原创太阳能首样。标准库生成可编辑glTF/bin和自包含GLB；米制+Y/-Z。

四块板采用需求r2直立双折；旋转轨在源内，不在包装中造替代模型。
所有box逐面独立顶点/法线，保留硬边。源默认completed，Straps由包装控制。
"""
import hashlib
import json
import math
from pathlib import Path
import struct

HERE = Path(__file__).resolve().parent
ROLES = ['body_light', 'frame_dark', 'accent_warm', 'solar_face']
RGB = ['D8DCD6', '38454B', 'D77B3D', '203B52']
nodes, meshes, accessors, views, surfaces = [], [], [], [], []
blob = bytearray()
by_name = {}


def quat(axis, degrees):
    a = math.radians(degrees) / 2
    q = [0., 0., 0., math.cos(a)]
    q[axis] = math.sin(a)
    return q


def node(name, parent=None, t=(0, 0, 0), rot=None):
    i = len(nodes)
    nodes.append({'name': name, 'translation': list(t)})
    by_name[name] = i
    if rot is not None:
        nodes[i]['rotation'] = rot
    if parent is not None:
        nodes[parent].setdefault('children', []).append(i)
    return i


def accessor(values, kind, target=None):
    width = {'SCALAR': 1, 'VEC3': 3, 'VEC4': 4}[kind]
    flat = values if width == 1 else [x for v in values for x in v]
    while len(blob) % 4:
        blob.append(0)
    data = struct.pack('<%df' % len(flat), *flat)
    view = {'buffer': 0, 'byteOffset': len(blob), 'byteLength': len(data)}
    if target is not None:
        view['target'] = target
    views.append(view)
    blob.extend(data)
    out = {'bufferView': len(views)-1, 'componentType': 5126,
           'count': len(values), 'type': kind}
    if width == 1:
        out.update(min=[min(values)], max=[max(values)])
    else:
        out.update(min=[min(v[i] for v in values) for i in range(width)],
                   max=[max(v[i] for v in values) for i in range(width)])
    accessors.append(out)
    return len(accessors)-1


def box(parent, name, role, center, size):
    i = node(name, parent, center)
    h = [s/2 for s in size]
    positions, normals = [], []
    for axis in range(3):
        uv = [a for a in range(3) if a != axis]
        for side in [-1, 1]:
            v = []
            for u, w in [(-1, -1), (1, -1), (1, 1), (-1, 1)]:
                p = [0., 0., 0.]
                p[axis], p[uv[0]], p[uv[1]] = side*h[axis], u*h[uv[0]], w*h[uv[1]]
                v.append(p)
            a = [v[1][k]-v[0][k] for k in range(3)]
            b = [v[2][k]-v[0][k] for k in range(3)]
            cross = [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
            if cross[axis]*side < 0:
                v.reverse()
            n = [0., 0., 0.]
            n[axis] = float(side)
            for k in [0, 1, 2, 0, 2, 3]:
                positions.append(v[k]); normals.append(n)
    prim = {'attributes': {'POSITION': accessor(positions, 'VEC3', 34962),
                           'NORMAL': accessor(normals, 'VEC3', 34962)},
            'material': ROLES.index(role), 'mode': 4}
    meshes.append({'name': name, 'primitives': [prim]})
    nodes[i]['mesh'] = len(meshes)-1
    surfaces.append({'node': name, 'surface': 0, 'role': role, 'triangles': 12})
    return i


root = node('solar-r1')
model = node('Model', root)
base = node('Base', model)
box(base, 'SpineFrame', 'frame_dark', (0, .51, 0), (.8, .50, 2.8))
box(base, 'SpineShell', 'body_light', (0, .78, 0), (.76, .04, 2.72))
box(base, 'FrontNose', 'accent_warm', (0, .40, -1.42), (.58, .16, .08))
for sign in [-1, 1]:
    for z in [-1.1, 1.1]:
        foot = node(('L' if sign < 0 else 'R')+('F' if z < 0 else 'B')+'Foot', model,
                    (sign*.58, 0, z))
        box(foot, 'Pad'+str(foot), 'frame_dark', (0, .04, 0), (.44, .08, .32))
        box(foot, 'Arm'+str(foot), 'body_light', (-sign*.14, .19, 0), (.32, .14, .16))
straps = node('Straps', model)
for z in [-.9, .9]:
    for x in [-.585, .585]:
        box(straps, 'StrapSide'+str(len(nodes)), 'accent_warm', (x, 1.35, z), (.03, 1.08, .10))
    for y in [.82, 1.88]:
        box(straps, 'StrapCross'+str(len(nodes)), 'accent_warm', (0, y, z), (1.14, .02, .10))


def panel(parent, prefix, sign):
    box(parent, prefix+'Back', 'frame_dark', (sign*.5, -.025, 0), (1., .03, 2.8))
    box(parent, prefix+'Face', 'solar_face', (sign*.5, .013, 0), (.92, .046, 2.70))
    for x in [.02, .98]:
        box(parent, prefix+'Edge'+str(x), 'body_light', (sign*x, .007, 0), (.04, .066, 2.8))
    for z in [-1.38, 1.38]:
        box(parent, prefix+'End'+str(z), 'body_light', (sign*.5, .007, z), (.92, .066, .04))
    # Coarse grid in real geometry; readable at normal distance without textures.
    for z in [-.7, 0, .7]:
        box(parent, prefix+'Grid'+str(z), 'body_light', (sign*.5, .037, z), (.92, .006, .012))


for prefix, sign in [('Left', -1), ('Right', 1)]:
    inner = node(prefix+'Inner', model, (sign*.4, .85, 0), quat(2, sign*12))
    panel(inner, prefix+'Inner', sign)
    outer = node(prefix+'Outer', inner, (sign*1, -.10, 0))
    panel(outer, prefix+'Outer', sign)
for x in [-.31, .31]:
    box(base, 'ServiceSide'+str(x), 'frame_dark', (x, .58, 1.43), (.02, .44, .06))
for y in [.37, .79]:
    box(base, 'ServiceEdge'+str(y), 'frame_dark', (0, y, 1.43), (.60, .02, .06))
cover = node('ServiceCover', model, (0, .8, 1.48))
box(cover, 'CoverPanel', 'body_light', (0, -.18, 0), (.58, .36, .025))
box(cover, 'CoverHandle', 'accent_warm', (0, -.28, .025), (.34, .045, .025))
indicator = node('WorkIndicator', model, (0, 1.05, 1.54))
box(indicator, 'IndicatorBlade', 'accent_warm', (0, 0, 0), (.50, .30, .025))
# Visible fixed supports: the indicator and stop flap are mechanisms, not floating marks.
box(base, 'IndicatorStand', 'frame_dark', (.36, .73, 1.58), (.035, .70, .035))
box(base, 'IndicatorArm', 'frame_dark', (.18, 1.05, 1.58), (.36, .035, .035))
box(base, 'IndicatorAxle', 'frame_dark', (0, 1.05, 1.56), (.04, .04, .04))
box(base, 'StandFoot', 'frame_dark', (.36, .38, 1.50), (.06, .06, .20))
box(base, 'StopAxle', 'frame_dark', (.36, .57, 1.45), (.04, .04, .14))
stop = node('StopFlap', model, (.36, .57, 1.51))
box(stop, 'StopBlade', 'accent_warm', (0, .16, 0), (.10, .32, .025))
box(base, 'PowerDuct', 'frame_dark', (0, .30, 1.46), (.30, .20, .08))
for name, t in [('Socket_PowerOut', (0, .30, 1.45)), ('Socket_Service', (0, .65, 1.35))]:
    node(name, model, t, quat(1, 180))

# Fixed protection sits below the folding panels and behind the service cover.
box(base, 'ElectronicsMount', 'frame_dark', (0, .58, 1.4075), (.24, .16, .015))
box(base, 'ProtectedElectronics', 'frame_dark', (0, .58, 1.435), (.42, .22, .04))
box(base, 'ElectronicsInnerCover', 'body_light', (0, .58, 1.4575), (.46, .26, .005))
for sign in [-1, 1]:
    box(base, 'FixedCableGuard'+('L' if sign < 0 else 'R'), 'frame_dark',
        (sign*.415, .68, 1.06), (.03, .08, .65))

animations = []


def animation(name, tracks):
    samplers, channels = [], []
    for target, axis, times, angles in tracks:
        samplers.append({'input': accessor(times, 'SCALAR'),
                         'output': accessor([quat(axis, d) for d in angles], 'VEC4'),
                         'interpolation': 'LINEAR'})
        channels.append({'sampler': len(samplers)-1,
                         'target': {'node': by_name[target], 'path': 'rotation'}})
    animations.append({'name': name, 'samplers': samplers, 'channels': channels})


animation('deploying', [(prefix+part, 2, [0., 1., 2., 3., 4.], [sign*a for a in angles])
    for prefix, sign in [('Left', -1), ('Right', 1)]
    for part, angles in [('Inner', [90, 90, 90, 51, 12]), ('Outer', [180, 270, 360, 360, 360])]])
animation('work-loop', [('WorkIndicator', 2, [0., .25, .5, .75, 1.], [0, 20, 0, -20, 0])])
animation('disabled', [('StopFlap', 2, [0., 1.], [0, -35])])
animation('maintenance', [('ServiceCover', 0, [0., 1.], [0, -110])])

# Baked foot retraction: packed seeks the final source pose; installed uses baseline.
samplers, channels = [], []
for sign in [-1, 1]:
    for z in [-1.1, 1.1]:
        name = ('L' if sign < 0 else 'R')+('F' if z < 0 else 'B')+'Foot'
        samplers.append({'input': accessor([0., 1.], 'SCALAR'),
                         'output': accessor([(sign*.58, 0, z), (sign*.38, 0, z)], 'VEC3'),
                         'interpolation': 'LINEAR'})
        channels.append({'sampler': len(samplers)-1, 'target': {'node': by_name[name], 'path': 'translation'}})
animations.append({'name': 'packed', 'samplers': samplers, 'channels': channels})

materials = []
for r, color in zip(ROLES, RGB):
    # Placeholder only. Public preview assigns exact M01 resources by surface.
    rgb = [int(color[i:i+2], 16)/255 for i in [0, 2, 4]]
    materials.append({'name': r, 'pbrMetallicRoughness': {'baseColorFactor': rgb+[1.],
                     'metallicFactor': 0., 'roughnessFactor': .65}})
while len(blob) % 4:
    blob.append(0)
doc = {'asset': {'version': '2.0', 'generator': 'Yudian original solar-r1 Python stdlib'},
       'scene': 0, 'scenes': [{'nodes': [root]}], 'nodes': nodes, 'meshes': meshes,
       'materials': materials, 'accessors': accessors, 'bufferViews': views,
       'animations': animations, 'buffers': [{'byteLength': len(blob), 'uri': 'solar-r1.bin'}]}
HERE.mkdir(parents=True, exist_ok=True)
(HERE/'solar-r1.bin').write_bytes(blob)
(HERE/'solar-r1.gltf').write_text(json.dumps(doc, separators=(',', ':')), encoding='utf-8')
doc['buffers'] = [{'byteLength': len(blob)}]
j = json.dumps(doc, separators=(',', ':')).encode()
j += b' '*((-len(j)) % 4)
glb = struct.pack('<III', 0x46546C67, 2, 28+len(j)+len(blob))
glb += struct.pack('<II', len(j), 0x4E4F534A)+j+struct.pack('<II', len(blob), 0x004E4942)+blob
(HERE/'solar-r1.glb').write_bytes(glb)
repo = HERE.parents[3]
out = repo/'prototype/assets/facilities/solar-r1/solar-r1.glb'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_bytes(glb)
(HERE/'geometry-facts.json').write_text(json.dumps({'unit': 'meter', 'up': '+Y', 'forward': '-Z',
    'original': True, 'surfaces': surfaces, 'triangles': 12*len(meshes),
    'glb_sha256': hashlib.sha256(glb).hexdigest(),
    'source_clips': ['deploying', 'work-loop', 'disabled', 'maintenance', 'packed'],
    'visibility_requires_wrapper': ['Straps'], 'import_visual': 'NOT_RUN'}, indent=2)+'\n')
print('SOLAR_SOURCE_WRITTEN', len(meshes), 'meshes', hashlib.sha256(glb).hexdigest())
