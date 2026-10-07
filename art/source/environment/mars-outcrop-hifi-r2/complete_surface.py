"""Accepted complete-face surface, applied once to the public stage09/10 baseline.
This module has no file input, scene-loading side effects, or historical-cache gate.
"""
import bpy,bmesh
import numpy as np
import surface_stage17 as S

DUPLICATE_DISTANCE=5.960464477539063e-8

def baseline(low):
    me=low.data;co,vi,nr,uv=S.arrays(me)
    assert len(me.polygons)==227160 and all(len(f.vertices)==3 for f in me.polygons),'Expected the unchanged stage10 UV baseline; refusing repeated surface displacement'
    assert not any(a.name.startswith('stage17_') for a in me.attributes)
    assert not any(e.use_edge_sharp for e in me.edges) and all(f.use_smooth for f in me.polygons)
    labels,_=S.actual_face_labels(me)
    me.normals_split_custom_set(nr.tolist());_,_,control,_=S.arrays(me)
    return {'co':co,'tri':co[vi.reshape(-1,3)].astype(float),'uv':uv.reshape(-1,3,2).astype(float),
        'normal':nr.reshape(-1,3,3).astype(float),'control':control.reshape(-1,3,3).astype(float),
        'labels':labels,'bounds':np.array(low.bound_box)}

def normal_input(base):return base['tri'],base['uv'],base['normal'],base['control']

def remove_duplicate_edges(low,base):
    """Weld only the proven zero-UV duplicate edges; retain every original vertex."""
    me=low.data;before=S.P.triangles(low);bm=bmesh.new();bm.from_mesh(me);bm.verts.ensure_lookup_table()
    uv=bm.loops.layers.uv.active;pos=bm.verts.layers.float_vector['stage17_original_position'];region=bm.faces.layers.int['stage17_face_region']
    assert np.array_equal(np.array([v[pos] for v in list(bm.verts)[:len(base['co'])]],np.float32),base['co'])
    def area(f):
        a,b,c=[np.array(l[uv].uv,dtype=float) for l in f.loops];d=b-a;e=c-a;return float(d[0]*e[1]-d[1]*e[0])*.5
    bad=[f for f in bm.faces if abs(area(f))<1e-14]
    assert all(area(f)==0 and f[region]>0 for f in bad),'Unexpected UV degeneracy outside the accepted duplicate-edge case'
    edges=set()
    for f in bad:
        for e in f.edges:
            if e.calc_length()<=DUPLICATE_DISTANCE and all(l[uv].uv==l.link_loop_next[uv].uv for l in e.link_loops):edges.add(e)
    parent={v:v for e in edges for v in e.verts}
    def root(v):
        while parent[v]!=v:v=parent[v]
        return v
    for e in edges:
        a,b=[root(v) for v in e.verts]
        if a==b:continue
        keep,drop=sorted((a,b),key=lambda v:(v.index>=len(base['co']),v.index));parent[drop]=keep
    mapping={v:root(v) for v in parent if root(v)!=v}
    max_distance=max(((v.co-k.co).length for v,k in mapping.items()),default=0)
    assert max_distance<=DUPLICATE_DISTANCE and not any(v.index<len(base['co']) for v in mapping)
    if mapping:bmesh.ops.weld_verts(bm,targetmap=mapping)
    assert all(len(f.verts)==3 for f in bm.faces)
    areas=np.array([area(f) for f in bm.faces]);assert (areas>1e-14).all()
    bm.normal_update();bm.to_mesh(me);bm.free();me.update();S.set_low_normals(me,normal_input(base))
    return {'selected_duplicate_edges':len(edges),'merged_inserted_vertices':len(mapping),'removed_original_vertices':0,
        'max_merge_distance_m':max_distance,'threshold_m':DUPLICATE_DISTANCE,'zero_uv_faces_before':len(bad),
        'triangles_before':before,'triangles_after':S.P.triangles(low),'removed_degenerate_triangles':before-S.P.triangles(low)}

def preservation(low,base):
    """Independent cross-product interpolation and same-state native encoding.
    Original-face labels and affected fans define the permitted edit boundary.
    """
    me=low.data;co,vi,nr,uv=S.arrays(me);pre=np.empty(len(me.vertices)*3,np.float32)
    me.attributes['stage17_original_position'].data.foreach_get('vector',pre);pre=pre.reshape(-1,3)
    ids=np.empty(len(me.polygons),np.int32);me.attributes['stage17_original_face'].data.foreach_get('value',ids);ids-=1
    assert ids.min()>=0 and ids.max()<len(base['tri']) and all(len(f.vertices)==3 for f in me.polygons)
    lf=np.repeat(ids,3);lr=np.repeat(base['labels'][ids],3);moved=np.linalg.norm(co.astype(float)-pre,axis=1)>1e-7
    allowed=np.zeros(len(co),bool);allowed[vi[lr>0]]=True
    fan=np.zeros(len(co),bool);fan[vi[np.repeat(moved[vi.reshape(-1,3)].any(1),3)]]=True
    outside=(lr==0)&~allowed[vi]&~fan[vi];margin=(lr==0)&~outside
    vn=np.empty(len(co)*3,np.float32);me.vertices.foreach_get('normal',vn);vn=vn.reshape(-1,3)
    target=np.empty((len(vi),3),float);uv_error=0.;position_error=0.;target_error=0.
    for start in range(0,len(vi),100000):
        end=min(start+100000,len(vi));f=lf[start:end];p=pre[vi[start:end]].astype(float)
        a,b,c=base['tri'][f,0],base['tri'][f,1],base['tri'][f,2];e0=b-a;e1=c-a;d=p-a;n=np.cross(e0,e1);den=(n*n).sum(1);assert (den>0).all()
        w1=(np.cross(d,e1)*n).sum(1)/den;w2=(np.cross(e0,d)*n).sum(1)/den;w=np.column_stack((1-w1-w2,w1,w2))
        distances=np.linalg.norm(base['tri'][f]-p[:,None,:],axis=2);which=distances.argmin(1);same=distances.min(1)<1e-7;w[same]=np.eye(3)[which[same]]
        expected=(base['normal'][f]*w[:,:,None]).sum(1)
        target_error=max(target_error,float(np.max(abs(expected[same]-base['normal'][f[same],which[same]]),initial=0)))
        uv_error=max(uv_error,float(abs((base['uv'][f]*w[:,:,None]).sum(1)-uv[start:end]).max()))
        position_error=max(position_error,float(abs((base['tri'][f]*w[:,:,None]).sum(1)-p).max()))
        changed=moved[vi[start:end]];expected[changed]=vn[vi[start:end][changed]];target[start:end]=expected
    outside_position=float(np.linalg.norm(co[vi[outside]].astype(float)-pre[vi[outside]],axis=1).max(initial=0))
    assert target_error==0 and outside_position==0 and uv_error<3e-5 and position_error<3e-5
    assert not any(e.use_edge_sharp for e in me.edges) and all(f.use_smooth for f in me.polygons)
    control=me.copy();control.edges.foreach_set('use_edge_sharp',np.zeros(len(control.edges),bool));control.polygons.foreach_set('use_smooth',np.ones(len(control.polygons),bool));control.update();control.normals_split_custom_set(target)
    _,_,encoded,_=S.arrays(control);delta=np.linalg.norm(nr.astype(float)-encoded.astype(float),axis=1);bpy.data.meshes.remove(control)
    def area(t):
        a=t[:,1]-t[:,0];b=t[:,2]-t[:,0];return (a[:,0]*b[:,1]-a[:,1]*b[:,0])*.5
    areas=area(uv.reshape(-1,3,2).astype(float));partition=np.bincount(ids,weights=areas,minlength=len(base['tri']))
    partition_error=float(abs(partition-area(base['uv'])).max());bpy.context.view_layer.update();bounds_error=float(abs(np.array(low.bound_box)-base['bounds']).max())
    report={'status':'PASS','uv_interpolation_max_delta':uv_error,'uv_original_face_area_partition_max_delta':partition_error,
        'uv_negative_faces':int((areas<0).sum()),'uv_zero_area_faces':int((abs(areas)<1e-14).sum()),'non_triangular_faces':0,
        'original_corner_target_max_delta':target_error,'strict_outside_position_max_delta_m':outside_position,'bounds_max_delta':bounds_error,
        'original_and_current_hard_edges':0,'all_faces_smooth':True,'normal_threshold':.0003,
        'strict_outside_corners':int(outside.sum()),'strict_outside_same_state_normal_max_delta':float(delta[outside].max()),
        'outside_face_shared_fan_normal_max_delta':float(delta[margin].max(initial=0)),
        'permitted_face_normal_encoding_diagnostic_max_delta':float(delta[lr>0].max()),
        'control':'Independent target interpolation encoded using the candidate coordinates, topology and original initial hard-edge state; no historical diagnostic is a precondition'}
    assert partition_error<1e-8 and bounds_error==0 and report['uv_negative_faces']==report['uv_zero_area_faces']==0 and delta[outside].max()<=.0003,report
    return report

def prepare_complete_face(high,low):
    assert S.P.triangles(high)==946516,'Expected the original stage09 high mesh'
    base=baseline(low);trees,selection=S.physical_boundaries(high)
    report={'selection':selection,'high':S.refine_face(high,False,trees,normal_input(base)),
        'game':S.refine_face(low,True,trees,normal_input(base))}
    report['duplicate_cleanup']=remove_duplicate_edges(low,base)
    report['preservation']=preservation(low,base)
    return report
