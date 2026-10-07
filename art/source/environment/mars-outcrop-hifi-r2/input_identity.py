"""Geometry, material-input and packed-image identities for the frozen delivery."""
import hashlib,json
import numpy as np

def array(data,key,width,dtype=np.float32):
    a=np.empty(len(data)*width,dtype);data.foreach_get(key,a);return a.reshape(len(data),width)

def digest(a):return hashlib.sha256(np.ascontiguousarray(a).tobytes()).hexdigest()

def mesh(o):
    m=o.data;co=array(m.vertices,'co',3);vi=array(m.loops,'vertex_index',1,np.int32)
    record={'vertices':len(m.vertices),'polygons':len(m.polygons),'triangles':sum(len(p.vertices)-2 for p in m.polygons),
        'matrix_world':[list(row) for row in o.matrix_world],
        'position_sha256':digest(co),'corner_vertices_sha256':digest(vi),
        'polygon_sizes_sha256':digest(array(m.polygons,'loop_total',1,np.int32)),
        'corner_normal_sha256':digest(array(m.corner_normals,'vector',3)),
        'smooth_sha256':digest(array(m.polygons,'use_smooth',1,bool)),
        'sharp_edge_sha256':digest(array(m.edges,'use_edge_sharp',1,bool)),
        'bounds_min':co.min(0).tolist(),'bounds_max':co.max(0).tolist(),
        'uv':{l.name:digest(array(l.data,'uv',2)) for l in m.uv_layers},
        'active_uv':m.uv_layers.active.name if m.uv_layers.active else None,
        'render_uv':[l.name for l in m.uv_layers if l.active_render],
        'color_attributes':{a.name:{'domain':a.domain,'data_type':a.data_type,'sha256':digest(array(a.data,'color',4))} for a in m.color_attributes}}
    return record

def value(v):
    if isinstance(v,(str,int,float,bool)) or v is None:return v
    try:return list(v)
    except TypeError:return str(v)

def material(mat):
    tree=mat.node_tree;outputs=[n for n in tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output]
    selected=set(outputs);queue=list(outputs)
    while queue:
        node=queue.pop()
        for socket in node.inputs:
            for link in socket.links:
                if link.from_node not in selected:selected.add(link.from_node);queue.append(link.from_node)
    fields=('operation','blend_type','use_clamp','clamp_factor','clamp_result','data_type','layer_name','attribute_name','attribute_type','noise_dimensions','noise_type','normalize','invert','distribution','subsurface_method')
    nodes=[]
    for n in sorted(selected,key=lambda n:n.name):
        record={'name':n.name,'type':n.bl_idname,'parameters':{k:value(getattr(n,k)) for k in fields if hasattr(n,k)},
            'inputs':[{'identifier':s.identifier,'value':value(s.default_value)} for s in n.inputs if hasattr(s,'default_value')]}
        if hasattr(n,'color_ramp'):
            ramp=n.color_ramp;record['ramp']={'interpolation':ramp.interpolation,'color_mode':ramp.color_mode,'hue_interpolation':ramp.hue_interpolation,
                'elements':[{'position':e.position,'color':list(e.color)} for e in ramp.elements]}
        if n.type=='TEX_IMAGE':record['image']={'name':n.image.name,'colorspace':n.image.colorspace_settings.name} if n.image else None
        nodes.append(record)
    links=sorted((l.from_node.name,l.from_socket.identifier,l.to_node.name,l.to_socket.identifier) for l in tree.links if l.from_node in selected and l.to_node in selected)
    graph={'nodes':nodes,'links':links}
    return {'sha256':hashlib.sha256(json.dumps(graph,sort_keys=True).encode()).hexdigest(),'graph':graph}

def inputs(high,low):
    return {'high':mesh(high),'game':mesh(low),'high_material':material(high.data.materials[0])}
