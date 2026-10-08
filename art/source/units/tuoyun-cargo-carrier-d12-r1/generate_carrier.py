"""Static cargo attachment: source origin is the CargoSocket mount anchor."""
from pathlib import Path
import bpy, bmesh, hashlib, json, math, struct
from mathutils import Vector, Matrix
S=Path(__file__).resolve().parent;W=S.parents[3]
E=W/'art/exports/units/tuoyun-cargo-carrier-d12-r1'
A=W/'docs/art/production/d12-r1/u01-carrier/author'
U=W/'art/source/units/tuoyun-r1'
NAME='tuoyun-cargo-carrier-d12-r1'
BEVEL=.003

def bounds(points):
 return [[round(min(v[i] for v in points),6) for i in range(3)],[round(max(v[i] for v in points),6) for i in range(3)]]

def read_values(d,b,index):
 a=d['accessors'][index];v=d['bufferViews'][a['bufferView']]
 fmt={5126:'f',5125:'I',5123:'H',5121:'B'}[a['componentType']]
 n={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']];size=struct.calcsize('<'+fmt*n)
 start=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',size)
 return [struct.unpack_from('<'+fmt*n,b,start+i*stride) for i in range(a['count'])]

# Read actual U01 data; do not regenerate or modify U01.
ud=json.loads((U/'tuoyun-r1.gltf').read_text());ub=(U/'tuoyun-r1.bin').read_bytes()
socket=next(n for n in ud['nodes'] if n['name']=='Socket_Cargo')
assert socket['translation']==[0,.48,.28] and socket.get('rotation',[0,0,0,1])==[0,0,0,1]
body=next(m for m in ud['meshes'] if m['name']=='Body')
body_bounds=[bounds(read_values(ud,ub,p['attributes']['POSITION'])) for p in body['primitives']]
assert [[-.42,.46,-.10],[.42,.48,.70]] in body_bounds
inputs={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in U.iterdir() if p.name in ['tuoyun-r1.gltf','tuoyun-r1.bin','tuoyun-r1.glb','generate_tuoyun_r1.py']}
assert bpy.app.version[:3]==(5,2,2)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
root=bpy.data.objects.new(NAME,None);scene.collection.objects.link(root)
mat=bpy.data.materials.new('frame_dark')
bsdf=mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value=(.0401,.0578,.0694,1)
bsdf.inputs['Roughness'].default_value=.75;bsdf.inputs['Metallic'].default_value=.2
mat.diffuse_color=(.0401,.0578,.0694,1)
parts=[('Deck',(0,.225,.02),(.79,.03,.79))]
parts += [('Leg_'+side+end,(x,.105,z+.02),(.05,.21,.05)) for side,x in [('L',-.28),('R',.28)] for end,z in [('F',-.28),('R',.28)]]
source_audit={}
for name,center,size in parts:
 bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
 cx,cy,cz=center;sx,sy,sz=size
 for v in bm.verts:v.co=Vector((v.co.x*sx+cx,v.co.y*sz-cz,v.co.z*sy+cy))
 bmesh.ops.bevel(bm,geom=list(bm.edges),offset=BEVEL,segments=1,profile=.5,affect='EDGES',clamp_overlap=True)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 assert all(e.is_manifold for e in bm.edges),name
 assert bm.calc_volume(signed=True)>0,name
 mesh=bpy.data.meshes.new(name);bm.to_mesh(mesh);bm.free()
 assert not mesh.validate(),name
 for p in mesh.polygons:p.use_smooth=False
 mesh.materials.append(mat)
 ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.parent=root
 source_audit[name]={'bounds':bounds([(v.co.x,v.co.z,-v.co.y) for v in mesh.vertices]),'non_manifold':0,'bevel_m':BEVEL,'bevel_segments':1}
 # A 3 mm planar chamfer must remain on every top face after clamping.
 top=max(v.co.z for v in mesh.vertices)
 top_points=[(v.co.x,-v.co.y) for v in mesh.vertices if abs(v.co.z-top)<1e-6]
 assert abs(max(v[0] for v in top_points)-(cx+sx/2-BEVEL))<1e-6,name
 assert abs(min(v[0] for v in top_points)-(cx-sx/2+BEVEL))<1e-6,name
 assert abs(max(v[1] for v in top_points)-(cz+sz/2-BEVEL))<1e-6,name
 assert abs(min(v[1] for v in top_points)-(cz-sz/2+BEVEL))<1e-6,name
assert source_audit['Deck']['bounds']==[[-.395,.21,-.375],[.395,.24,.415]]
for name in source_audit:
 if name.startswith('Leg'):assert source_audit[name]['bounds'][0][1]==0 and source_audit[name]['bounds'][1][1]==.21
# Save only the asset, then reopen before exporting it.
bpy.ops.wm.save_as_mainfile(filepath=str(S/(NAME+'.blend')))
bpy.ops.wm.open_mainfile(filepath=str(S/(NAME+'.blend')))
assert set(o.name for o in bpy.context.scene.objects)=={NAME,*source_audit}
for o in bpy.context.scene.objects:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(E/(NAME+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_cameras=False,export_lights=False,export_yup=True)

# Independently decode exported geometry, normals, welded topology and contact planes.
glb=(E/(NAME+'.glb')).read_bytes();magic,version,total=struct.unpack_from('<III',glb)
assert (magic,version,total)==(0x46546C67,2,len(glb))
jlen,jtype=struct.unpack_from('<II',glb,12);blen,btype=struct.unpack_from('<II',glb,20+jlen)
assert (jtype,btype)==(0x4E4F534A,0x004E4942)
d=json.loads(glb[20:20+jlen]);b=glb[28+jlen:28+jlen+blen]
assert len(d['nodes'])==6 and len(d['meshes'])==5 and not d.get('animations')
assert [m['name'] for m in d['materials']]==['frame_dark'] and 'uri' not in d['buffers'][0]
export_audit={};all_points=[];triangle_total=0
for mesh in d['meshes']:
 assert len(mesh['primitives'])==1
 p=mesh['primitives'][0];v=read_values(d,b,p['attributes']['POSITION']);n=read_values(d,b,p['attributes']['NORMAL']);indices=[i[0] for i in read_values(d,b,p['indices'])]
 assert bounds(v)==source_audit[mesh['name']]['bounds']
 all_points+=v;edges={};volume=0;max_normal_error=0
 for k in range(0,len(indices),3):
  ids=indices[k:k+3];x,y,z=[Vector(v[i]) for i in ids];cross=(y-x).cross(z-x);assert cross.length>1e-9
  normal=cross.normalized();volume+=x.dot(y.cross(z))/6
  for i in ids:
   assert Vector(n[i]).length>.99999
   error=(Vector(n[i])-normal).length;max_normal_error=max(max_normal_error,error)
   assert error<.0001,(mesh['name'],error)
  keys=[tuple(round(c,7) for c in v[i]) for i in ids]
  for j in range(3):
   edge=tuple(sorted((keys[j],keys[(j+1)%3])));edges[edge]=edges.get(edge,0)+1
 assert all(count==2 for count in edges.values()),mesh['name']
 assert volume>0
 triangle_total+=len(indices)//3
 export_audit[mesh['name']]={'non_manifold_welded_edges':0,'positive_volume_m3':volume,'max_face_normal_error':max_normal_error}
assert bounds(all_points)==[[-.395,0,-.375],[.395,.24,.415]]
mounted=[(x,y+.48,z+.28) for x,y,z in all_points]
assert bounds(mounted)==[[-.395,.48,-.095],[.395,.72,.695]]
for name,data in source_audit.items():
 if name.startswith('Leg'):
  low,high=data['bounds']
  assert low[0]>=-.42 and high[0]<=.42 and low[2]+.28>=-.1 and high[2]+.28<=.7
  assert low[1]+.48==.48 and high[1]==source_audit['Deck']['bounds'][0][1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(E/(NAME+'.glb')))
readback=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(readback)==5
bpy.context.view_layer.update()
assert bounds([(p.x,p.z,-p.y) for o in readback for v in o.data.vertices for p in [o.matrix_world@v.co]])==[[-.395,0,-.375],[.395,.24,.415]]
# Author shape inspection only; no yaw matrix or graphical sign-off.
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH'
scene.world=bpy.data.worlds.new('AuthorWorld');scene.world.color=(.30,.34,.37)
scene.display.shading.background_type='WORLD'
camdata=bpy.data.cameras.new('AuthorCamera');camdata.type='ORTHO';camdata.ortho_scale=1.3
cam=bpy.data.objects.new('AuthorCamera',camdata);scene.collection.objects.link(cam);scene.camera=cam
cam.location=(1.2,-1.4,.6);cam.rotation_euler=(Vector((0,-.02,.12))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(A/'carrier-author-shape.png');bpy.ops.render.render(write_still=True)

# Real 1:1 parenting to the imported socket; temporary U01 objects are never saved.
carrier=bpy.data.objects[NAME]
bpy.ops.import_scene.gltf(filepath=str(U/'tuoyun-r1.glb'))
base_socket=bpy.data.objects['Socket_Cargo'];carrier.parent=base_socket;carrier.matrix_basis=Matrix.Identity(4)
for name in ['CargoBox','TowRod']:bpy.data.objects[name].hide_render=True
bpy.context.view_layer.update()
assert bounds([(p.x,p.z,-p.y) for o in readback for v in o.data.vertices for p in [o.matrix_world@v.co]])==[[-.395,.48,-.095],[.395,.72,.695]]
camdata.type='PERSP';camdata.sensor_fit='VERTICAL';camdata.sensor_height=24;camdata.lens=24/(2*math.tan(math.radians(50)/2))
cam.location=(3,-4,3);cam.rotation_euler=(Vector((0,0,.4))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(A/'carrier-author-mounted.png');bpy.ops.render.render(write_still=True)
result={'technical':'PASS','blender':bpy.app.version_string,'origin':'CargoSocket mount anchor at local(0,0,0); Y0 bottom contact plane; geometry center localZ .02','material_role':'frame_dark','nodes':6,'meshes':5,'triangles':triangle_total,'local_bounds':[[-.395,0,-.375],[.395,.24,.415]],'mounted_bounds':[[-.395,.48,-.095],[.395,.72,.695]],'deck_size':[.79,.03,.79],'leg_size':[.05,.21,.05],'deck_top_local_y':.24,'deck_top_mounted_y':.72,'rail_clearance_m':{'side':.025,'front_rear':.005},'source_audit':source_audit,'independent_export_audit':export_audit,'source_reopen':'PASS','independent_glb_readback':'PASS','actual_socket_parenting_scale1':'PASS','u01_inputs_sha256':inputs,'u01_changed':False,'visual':'NOT_SIGNED; author Workbench inspection only; actual Godot resource/empty captures and independent review owned by master','transport_all_animations_main_performance':'NOT_RUN'}
(A/'carrier-check.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
files=[S/(NAME+'.blend'),S/'generate_carrier.py',E/(NAME+'.glb')]
(A/'delivery-hashes.json').write_text(json.dumps({str(p.relative_to(W)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},indent=2)+'\n')
print('RESULT '+json.dumps(result,ensure_ascii=False))
