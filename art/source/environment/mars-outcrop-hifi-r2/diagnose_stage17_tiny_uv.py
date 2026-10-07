import bpy,json,sys
from pathlib import Path
import numpy as np
h=Path(__file__).resolve().parent;sys.path.insert(0,str(h));import surface_stage17 as S
p=S.STAGE/'source-candidate.blend';bpy.ops.wm.open_mainfile(filepath=str(p));m=bpy.data.objects['MarsOutcrop'].data
co,vi,nr,uv=S.arrays(m);pre=np.empty(len(m.vertices)*3,np.float32);m.attributes['stage17_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
ids=np.empty(len(m.polygons),np.int32);m.attributes['stage17_original_face'].data.foreach_get('value',ids);ids-=1
region=np.empty(len(m.polygons),np.int32);m.attributes['stage17_face_region'].data.foreach_get('value',region)
t=uv.reshape(-1,3,2).astype(float);area=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0])*.5;sel=np.where(abs(area)<1e-14)[0];v=vi.reshape(-1,3)[sel];pos=co[v].astype(float);old=pre[v].astype(float)
lengths=np.linalg.norm(pos-np.roll(pos,1,axis=1),axis=2);old_lengths=np.linalg.norm(old-np.roll(old,1,axis=1),axis=2);uv_lengths=np.linalg.norm(t[sel]-np.roll(t[sel],1,axis=1),axis=2)
ga=np.linalg.norm(np.cross(pos[:,1]-pos[:,0],pos[:,2]-pos[:,0]),axis=1)*.5;pa=np.linalg.norm(np.cross(old[:,1]-old[:,0],old[:,2]-old[:,0]),axis=1)*.5
r={'count':len(sel),'exact_zero':int((area[sel]==0).sum()),'positive_tiny':int((area[sel]>0).sum()),'total_uv_area':float(area[sel].sum()),'total_geometry_area_m2':float(ga.sum()),'max_geometry_area_m2':float(ga.max()),'max_pre_geometry_area_m2':float(pa.max()),'min_edge_m_quantiles':np.quantile(lengths.min(1),[0,.1,.5,.9,1]).tolist(),'min_original_edge_m_quantiles':np.quantile(old_lengths.min(1),[0,.1,.5,.9,1]).tolist(),'min_uv_edge_quantiles':np.quantile(uv_lengths.min(1),[0,.1,.5,.9,1]).tolist(),'regions':{str(k):int((region[sel]==k).sum()) for k in np.unique(region[sel])},'unique_original_faces':len(np.unique(ids[sel])),'examples':[{'face':int(sel[i]),'original_face':int(ids[sel[i]]),'uv_area':float(area[sel[i]]),'geometry_area_m2':float(ga[i]),'pre_area_m2':float(pa[i]),'co':pos[i].tolist(),'uv':t[sel[i]].tolist()} for i in np.argsort(ga)[-8:]]}
S.P.write_json(S.STAGE/'tiny-uv-diagnostic.json',r);print('RESULT_JSON='+json.dumps(r),flush=True)
