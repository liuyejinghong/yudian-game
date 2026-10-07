import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(p.parent.parent/'mars-outcrop-hifi-r2.blend'))
o=bpy.data.objects['MarsOutcrop'];tree=BVHTree.FromObject(o,bpy.context.evaluated_depsgraph_get())
views={'detail':((2.5,-3.7,2.4),(.4,0,1),[(x,y) for x in (800,975,1150) for y in (500,700,900)]),'reverse':((-7,8,3.4),(0,0,.8),[(x,y) for x in (660,800,950) for y in (430,540,650)])}
result={}
for name,(eye,target,pixels) in views.items():
 eye=Vector(eye);fwd=(Vector(target)-eye).normalized();right=fwd.cross(Vector((0,0,1))).normalized();up=right.cross(fwd).normalized();rows=[]
 for x,y in pixels:
  ray=(fwd+right*((x-800)/500*math.tan(math.radians(25)))+up*((500-y)/500*math.tan(math.radians(25)))).normalized()
  pos,normal,index,distance=tree.ray_cast(eye,ray,50)
  rows.append({'pixel':[x,y],'position':list(pos) if pos else None,'normal':list(normal) if normal else None,'face':index,'distance':distance})
 result[name]=rows
result['mesh']={ob.name:{'vertices':len(ob.data.vertices),'faces':len(ob.data.polygons),'has_custom_normals':ob.data.has_custom_normals,'color_attributes':[a.name for a in ob.data.color_attributes]} for ob in bpy.data.objects if ob.type=='MESH'}
(p/'region-locations.json').write_text(json.dumps(result,indent=2)+'\n');print('RESULT_JSON='+json.dumps(result))
