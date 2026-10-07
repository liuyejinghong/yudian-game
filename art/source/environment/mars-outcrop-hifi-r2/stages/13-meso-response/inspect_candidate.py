import bpy,json,hashlib
import numpy as np
from pathlib import Path
p=Path(__file__).resolve().parent
def snap(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));o=bpy.data.objects['MarsOutcrop'];nt=o.data.materials[0].node_tree
 graph=[]
 for n in nt.nodes:
  graph.append({'name':n.name,'type':n.type,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None,'color_space':n.image.colorspace_settings.name if n.type=='TEX_IMAGE' and n.image else None,'uv_map':getattr(n,'uv_map',None),'defaults':{s.name:list(s.default_value) if hasattr(s.default_value,'__len__') else s.default_value for s in n.inputs if hasattr(s,'default_value') and not s.is_linked and not isinstance(s.default_value,str)}})
 smooth=np.empty(len(o.data.polygons),np.int8);o.data.polygons.foreach_get('use_smooth',smooth)
 normal=np.empty(len(o.data.corner_normals)*3,np.float32);o.data.corner_normals.foreach_get('vector',normal)
 maps={}
 for n in nt.nodes:
  if n.type!='TEX_IMAGE' or not n.image:continue
  im=n.image;a=np.empty(len(im.pixels),np.float32);im.pixels.foreach_get(a);a=a.reshape(-1,4)[:,:3]
  maps[im.name]={'size':list(im.size),'min':a.min(axis=0).tolist(),'max':a.max(axis=0).tolist(),'black_pixels':int((a.max(axis=1)<.001).sum()),'nan':int(np.isnan(a).sum())}
 return {'graph':graph,'links':[(l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name) for l in nt.links], 'smooth_hash':hashlib.sha256(smooth.tobytes()).hexdigest(),'corner_normals_hash':hashlib.sha256(normal.tobytes()).hexdigest(),'maps':maps}
result={'formal':snap(p.parent.parent/'mars-outcrop-hifi-r2.blend'),'candidate':snap(p/'candidate.blend')}
(p/'candidate-readonly-inspection.json').write_text(json.dumps(result,indent=2)+'\n')
print('RESULT_JSON='+json.dumps({k:{a:v for a,v in r.items() if a in ('smooth_hash','corner_normals_hash','maps')} for k,r in result.items()}))
