"""Run with Blender --background --python generate.py. Original lookdev support only."""
import bpy
import hashlib
import json
import math
import random
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
random.seed(7102026)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1


def material(name, rgb, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


materials = [material('Regolith', (.332, .256, .196), .94),
             material('Bedrock', (.451, .371, .281), .89),
             material('Clast', (.128, .117, .099), .96)]


def smoothstep(a, b, x):
    t = max(0, min(1, (x-a)/(b-a)))
    return t*t*(3-2*t)


def mound(x, z, cx, cz, wx, wz, h):
    return h*math.exp(-(((x-cx)/wx)**2 + ((z-cz)/wz)**2)*2)


def mesa(x,z,cx,cz,wx,wz,h):
    return (h+.6*math.sin(x*.071+z*.039))*math.exp(-(((x-cx)/wx)**4+((z-cz)/wz)**4)*2)


def height(x, z):
    r = math.hypot(x, z)
    small = .21*math.sin(x*.13+z*.055) + .17*math.sin(z*.17-x*.027)
    rolling = mound(x,z,-32,-34,35,26,2.1) + mound(x,z,42,-47,46,28,3.5)
    rolling += mound(x,z,-68,22,34,46,2.9) + mound(x,z,72,24,58,44,2.3)
    far = mesa(x,z,-70,-144,83,43,16) + mesa(x,z,98,-181,80,46,12)
    far += mesa(x,z,-166,-69,49,77,9) + mound(x,z,182,-87,54,86,8)
    far += mesa(x,z,-16,-235,143,61,13)
    return (small+rolling+far)*smoothstep(12,22,r)


def xyz(x, z, h):
    return (x, -z, h)


def mesh(name, verts, faces, mat, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.materials.append(mat)
    data.update()
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    for face in data.polygons:
        face.use_smooth = smooth
    return obj


# Continuous two-metre grid; the quiet centre has no platform or road boundary.
coords = list(range(-250, 251, 2))
verts = [xyz(x,z,height(x,z)) for z in coords for x in coords]
n = len(coords)
faces = []
for j in range(n-1):
    for i in range(n-1):
        a = j*n+i
        faces.append((a, a+n, a+n+1, a+1))
ground = mesh('Regolith_Continuous', verts, faces, materials[0], True)

rocks = {'Bedrock': ([], []), 'Clast': ([], [])}


def plate(role, x, z, length, width, thick, angle):
    """A buried, angular slab with a broken outline and a tilted cap."""
    vs, fs = rocks[role]
    start = len(vs)
    outline = [(-.46,-.25),(-.13,-.48),(.34,-.41),(.53,-.18),
               (.47,.29),(.11,.52),(-.39,.35),(-.55,.05)]
    if random.random()<.5:
        outline.pop(random.randrange(len(outline)))
    count = len(outline)
    rim = []
    for u,v in outline:
        u, v = (u+random.uniform(-.09,.09))*length,(v+random.uniform(-.09,.09))*width
        dx = math.cos(angle)*u-math.sin(angle)*v
        dz = math.sin(angle)*u+math.cos(angle)*v
        rim.append((dx,dz))
    base = min(height(x+dx,z+dz) for dx,dz in rim)-thick*.28
    tilt = random.uniform(-.3,.3)*thick
    for dx,dz in rim:
        vs.append(xyz(x+dx,z+dz,base))
    for dx,dz in rim:
        cap = thick+tilt*dx/max(length,.03)
        vs.append(xyz(x+dx,z+dz,base+cap))
    vs.append(xyz(x,z,base+thick))
    for i in range(count):
        j = (i+1)%count
        fs.append((start+count+i,start+count+j,start+j,start+i))
        fs.append((start+2*count,start+count+j,start+count+i))
    fs.append(tuple(start+i for i in range(count)))


# Two interrupted exposed-bedrock bands, with low crowns rather than hero stacks.
bands = [(-22,-24),(23,-30)]
for cx,cz in bands:
    for i in range(27):
        x = cx+random.uniform(-9,9)
        z = cz+random.uniform(-3.8,3.8)+.17*(x-cx)
        plate('Bedrock',x,z,random.uniform(1.1,3.7),random.uniform(.65,1.8),
              random.uniform(.12,.52),random.uniform(-.45,.4))

# Sparse flat cap fragments on selected northern ridges; no four-sided enclosure.
for cx,cz,spread in [(-72,-147,40),(96,-184,31),(-161,-69,23)]:
    for i in range(18):
        x,z = cx+random.uniform(-spread,spread),cz+random.uniform(-8,8)
        plate('Bedrock',x,z,random.uniform(3,9),random.uniform(1.8,4.5),
              random.uniform(.55,1.7),random.uniform(-.4,.35))

clast_count = 0
for cx,cz,total,sigma in [(6,0,320,4.0),(-22,-24,190,7),(23,-30,190,7),(0,0,150,42)]:
    for i in range(total):
        x,z = random.gauss(cx,sigma),random.gauss(cz,sigma*.65)
        if (abs(x-6)<2.8 and abs(z)<1.8) or any(math.hypot(x-a,z-b)<2.6 for a,b in [(-5,5),(0,-6),(-6,-2)]):
            continue
        size = random.uniform(.045,.14) if random.random()<.58 else random.uniform(.14,.40)
        if random.random()<.026:
            size = random.uniform(.40,.53)
        plate('Clast',x,z,size,size*random.uniform(.3,.72),size*random.uniform(.12,.32),random.uniform(0,math.tau))
        clast_count += 1

objects = [ground]
for role,(vs,fs) in rocks.items():
    objects.append(mesh(role+'_BrokenSlabs',vs,fs,materials[1 if role=='Bedrock' else 2]))

# Blender preview only; cameras and lights stay out of the GLB.
world = bpy.data.worlds.new('DustDay')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.43,.36,.29,1)
world.node_tree.nodes['Background'].inputs[1].default_value = .55
scene.world = world
bpy.ops.object.light_add(type='SUN', location=(10,-10,40))
sun = bpy.context.object
sun.name = 'PreviewSun'
sun.rotation_euler = (math.radians(32),math.radians(-28),math.radians(-35))
sun.data.energy = 2.2
sun.data.angle = math.radians(4)
for name,position,target in [('Normal',(25.12,28.8,26.3),(4,0,-2.5)),
                             ('Horizon',(20,3,22),(-25,2,-80)),
                             ('GroundNear',(12,4,10),(6,0,0))]:
    bpy.ops.object.camera_add(location=xyz(position[0],position[2],position[1]))
    camera = bpy.context.object
    camera.name = name
    aim = Vector(xyz(target[0],target[2],target[1]))-camera.location
    camera.rotation_euler = aim.to_track_quat('-Z','Y').to_euler()
    camera.data.lens = 38.6
scene.camera = bpy.data.objects['Normal']
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1200
scene.render.resolution_y = 750
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = ground
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'mars-ground-r1.blend'))
bpy.ops.export_scene.gltf(filepath=str(HERE/'mars-ground-r1.glb'),export_format='GLB',
                          use_selection=True,export_yup=True,export_materials='EXPORT',
                          export_cameras=False,export_extras=True)
facts = {'status':'lookdev candidate; owner visual acceptance pending','units':'meters',
         'source_up':'+Z','export_up':'+Y','export_forward':'-Z','seed':7102026,
         'materials':[m.name for m in materials], 'terrain_grid_m':2,
         'terrain_bounds_xz_m':[-250,250], 'central_flat_radius_m':12,
         'placements_y_m':{'hero [6,0]':height(6,0),'carrier [-5,5]':height(-5,5),
                            'solar [0,-6]':height(0,-6),'processor [-6,-2]':height(-6,-2)},
         'clast_instances':clast_count,'bedrock_slabs':108,
         'objects':[{ 'name':o.name,'vertices':len(o.data.vertices),
                     'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)} for o in objects],
         'references':['PIA21042 shape only; processing unspecified','PIA16800 middle natural-color column'],
         'original_geometry':True,'photo_textures':False,'collision_or_simulation':False,
         'glb_sha256':hashlib.sha256((HERE/'mars-ground-r1.glb').read_bytes()).hexdigest()}
assert clast_count < 1500
assert all(h == 0 for h in facts['placements_y_m'].values())
assert all(math.isfinite(v.co[k]) for o in objects for v in o.data.vertices for k in range(3))
(HERE/'manifest.json').write_text(json.dumps(facts,indent=2)+'\n')
scene.render.filepath = str(HERE/'preview-normal.png')
bpy.ops.render.render(write_still=True)
print('GROUND_GENERATED',json.dumps(facts))
