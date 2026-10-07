"""Shared native Blender helpers for the two r2 assets. No photograph textures."""
import bpy,bmesh,math,json,hashlib
import numpy as np
from pathlib import Path
import build as B
U,M=B.U,B.M

def select(*objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.hide_render=False;o.select_set(True)
    bpy.context.view_layer.objects.active=objects[-1]

def triangles(o):return sum(len(p.vertices)-2 for p in o.data.polygons)

def clone_low(high,coll,name,ratio):
    low=high.copy();low.data=high.data.copy();low.name=name;coll.objects.link(low)
    low.data.materials.clear();select(low)
    mod=low.modifiers.new('static_silhouette_preserving_reduction','DECIMATE');mod.ratio=ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    U.prepare_low(low,triangulate=True)
    for mod in list(low.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
    return low

def unwrap(low,cell=None):
    select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.006,area_weight=1.0,correct_aspect=True)
    bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.023 if cell is not None else .005)
    bpy.ops.object.mode_set(mode='OBJECT')
    if cell is not None:
        # Each source owns one 1/4 atlas cell; instances reuse that source intentionally.
        uv=low.data.uv_layers.active.data;a=np.empty(len(uv)*2,np.float32);uv.foreach_get('uv',a)
        a=a.reshape(-1,2)*.236+np.array([cell%4,cell//4])*.25+.007
        uv.foreach_set('uv',a.astype(np.float32).ravel())
    return U.uv_qa(low,tex_res=4096,analysis_res=512,gap_px=12)

def terrain_uv(o):
    layer=o.data.uv_layers.new(name='UVMap') if not o.data.uv_layers else o.data.uv_layers.active
    co=np.empty(len(o.data.vertices)*3,np.float32);o.data.vertices.foreach_get('co',co);co=co.reshape(-1,3)
    vi=np.empty(len(o.data.loops),np.int32);o.data.loops.foreach_get('vertex_index',vi)
    layer.data.foreach_set('uv',((co[vi,:2]+6)/12).astype(np.float32).ravel())

def surface(high,kind='rock',grain=False):
    """Independent dust, fractured-parent and roughness fields, without light or AO."""
    me=high.data;n=len(me.vertices);co=np.empty(n*3,np.float32);nr=np.empty(n*3,np.float32)
    me.vertices.foreach_get('co',co);me.vertices.foreach_get('normal',nr);co=co.reshape(-1,3);nr=nr.reshape(-1,3)
    dust=np.empty(n,np.float32);fresh=np.empty(n,np.float32);colors=np.empty((n,4),np.float32);rough=np.empty(n,np.float32)
    for start in range(0,n,65536):
        sl=slice(start,min(start+65536,n));x,y,z=co[sl].T;normal=nr[sl]
        broad=B.noise(x,y,z,1.2,383);small=B.noise(x,y,z,27,122)
        if kind=='sand':
            d=np.ones(len(x))*.88;f=np.zeros(len(x))
            rgb=np.array([.27,.211,.162])[None,:]*(1+.075*broad[:,None]+.018*small[:,None])
            rr=.94+.018*small
        else:
            d=np.clip(.12+.48*B.smooth(.12,.92,normal[:,2])+.25*(1-B.smooth(.05,.48,z))+.10*broad,0,.84)
            f=B.smooth(.56,.97,np.abs(normal[:,0]))*(1-d)*.55
            rock=np.array([.220,.195,.171])[None,:];fracture=np.array([.172,.162,.148])[None,:]
            rgb=rock*(1-f[:,None])+fracture*f[:,None]
            rgb=rgb*(1-d[:,None])+np.array([.283,.236,.193])[None,:]*d[:,None]
            rgb=rgb*(1+.075*broad[:,None]+.022*small[:,None])
            rr=np.clip(.79+.14*d+.025*small-.025*f,.71,.96)
        dust[sl]=d;fresh[sl]=f;colors[sl,:3]=rgb;colors[sl,3]=1;rough[sl]=rr
        if grain:
            # 1.5 mm differential loss supplements, never constructs, the approved form.
            loss=.0015*B.noise(x,y,z,34,631)+.0007*B.noise(x,y,z,71,923)
            co[sl]+=normal*loss[:,None]
    if grain:me.vertices.foreach_set('co',co.ravel());me.update()
    M.write_vertex_values(high,'DustMask',dust);M.write_vertex_values(high,'FreshFractureMask',fresh)
    M.write_vertex_values(high,'RoughnessSource',rough)
    attr=me.color_attributes.get('BaseColorSource') or me.color_attributes.new(name='BaseColorSource',type='FLOAT_COLOR',domain='POINT')
    attr.data.foreach_set('color',colors.ravel())
    mat=bpy.data.materials.new('SOURCE_'+kind+'_'+high.name);mat.use_nodes=True;nt=mat.node_tree
    p=nt.nodes.get('Principled BSDF');out=nt.nodes.get('Material Output')
    for attr_name,socket in [('BaseColorSource','Base Color'),('RoughnessSource','Roughness')]:
        node=nt.nodes.new('ShaderNodeVertexColor');node.layer_name=attr_name;nt.links.new(node.outputs['Color'],p.inputs[socket])
    high.data.materials.clear();high.data.materials.append(mat)
    return mat

def emit_source(high,attribute):
    nt=high.data.materials[0].node_tree;out=nt.nodes.get('Material Output')
    node=nt.nodes.get('BAKE_EMIT') or nt.nodes.new('ShaderNodeEmission');node.name='BAKE_EMIT'
    socket=nt.nodes.get('Principled BSDF').inputs['Base Color' if attribute=='BaseColorSource' else 'Roughness']
    assert socket.is_linked
    nt.links.new(socket.links[0].from_socket,node.inputs['Color']);nt.links.new(node.outputs['Emission'],out.inputs['Surface'])

def restore_source(high):
    nt=high.data.materials[0].node_tree;nt.links.new(nt.nodes.get('Principled BSDF').outputs['BSDF'],nt.nodes.get('Material Output').inputs['Surface'])

def microfinish(high,kind='rock'):
    """Small metric shader grain supplements the separately proven geometric normal bake.
    Colour/dust remain vertex fields; this does not mislabel their sampling as millimetres.
    """
    nt=high.data.materials[0].node_tree;p=nt.nodes.get('Principled BSDF')
    position=nt.nodes.new('ShaderNodeNewGeometry')
    noise=nt.nodes.new('ShaderNodeTexNoise');noise.name='MetricFineSurface'
    noise.inputs['Scale'].default_value=175 if kind=='sand' else 110
    noise.inputs['Detail'].default_value=2;noise.inputs['Roughness'].default_value=.61
    nt.links.new(position.outputs['Position'],noise.inputs['Vector'])
    bump=nt.nodes.new('ShaderNodeBump');bump.name='FineSurfaceOnly'
    bump.inputs['Distance'].default_value=.00035 if kind=='sand' else .00065
    bump.inputs['Strength'].default_value=.5
    nt.links.new(noise.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    original=p.inputs['Roughness'].links[0].from_socket
    delta=nt.nodes.new('ShaderNodeMath');delta.operation='MULTIPLY_ADD';delta.inputs[1].default_value=.06;delta.inputs[2].default_value=-.03
    nt.links.new(noise.outputs['Fac'],delta.inputs[0])
    add=nt.nodes.new('ShaderNodeMath');add.operation='ADD';add.use_clamp=True
    nt.links.new(original,add.inputs[0]);nt.links.new(delta.outputs[0],add.inputs[1]);nt.links.new(add.outputs[0],p.inputs['Roughness'])
    high['fine_surface_source']='Metric 3D shader field for sub-mm height / several-mm spacing; supplemental to independent high geometry'

def geologic_finish(high):
    """Centimetre lamination only on old exposed beds; dust/fresh breaks suppress it.
    Patch variation and grain remain three-dimensional authored fields, without AO.
    """
    nt=high.data.materials[0].node_tree;p=nt.nodes.get('Principled BSDF')
    def math_node(op,a,b=None,c=None,name=None):
        nd=nt.nodes.new('ShaderNodeMath');nd.operation=op
        if name:nd.name=name
        for i,v in enumerate((a,b,c)):
            if v is None:continue
            if isinstance(v,(int,float)):nd.inputs[i].default_value=v
            else:nt.links.new(v,nd.inputs[i])
        return nd.outputs[0]
    geo=nt.nodes.new('ShaderNodeNewGeometry');xyz=nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Position'],xyz.inputs[0]);x,y,z=[xyz.outputs[k] for k in ('X','Y','Z')]
    nxyz=nt.nodes.new('ShaderNodeSeparateXYZ');nt.links.new(geo.outputs['Normal'],nxyz.inputs[0])
    attrs={}
    for name in ('DustMask','FreshFractureMask'):
        nd=nt.nodes.new('ShaderNodeVertexColor');nd.layer_name=name;attrs[name]=nd.outputs['Color']
    bare=math_node('SUBTRACT',1,attrs['DustMask']);old=math_node('SUBTRACT',1,attrs['FreshFractureMask'])
    exposure=math_node('MULTIPLY',bare,old)
    front=math_node('MAXIMUM',math_node('MULTIPLY',nxyz.outputs['Y'],-1),0)
    region=math_node('MULTIPLY',math_node('LESS_THAN',x,.45),math_node('LESS_THAN',y,-.32))
    lamina_mask=math_node('MULTIPLY',math_node('MULTIPLY',exposure,front),region,name='OldBedExposure')
    def noise(name,scale,detail):
        nd=nt.nodes.new('ShaderNodeTexNoise');nd.name=name;nd.inputs['Scale'].default_value=scale
        nd.inputs['Detail'].default_value=detail;nd.inputs['Roughness'].default_value=.67
        nt.links.new(geo.outputs['Position'],nd.inputs['Vector']);return nd.outputs['Fac']
    patch=noise('WeatheringPatches',3.7,2);coarse=noise('ExposedSandstoneGrains',62,2.5);fine=noise('FineMineralGrains',190,2)
    tilted=math_node('ADD',z,math_node('MULTIPLY',x,.13))
    warped=math_node('ADD',tilted,math_node('MULTIPLY',patch,.023))
    band=math_node('MULTIPLY_ADD',math_node('SINE',math_node('MULTIPLY',warped,345)),.5,.5)
    band=math_node('POWER',band,2.6)
    tone=math_node('MULTIPLY_ADD',patch,.20,.88)
    tone=math_node('ADD',tone,math_node('MULTIPLY',math_node('MULTIPLY_ADD',band,.23,-.07),lamina_mask))
    tone=math_node('ADD',tone,math_node('MULTIPLY',math_node('MULTIPLY_ADD',coarse,.12,-.06),bare))
    base=p.inputs['Base Color'].links[0].from_socket
    mult=nt.nodes.new('ShaderNodeMixRGB');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1
    nt.links.new(base,mult.inputs[1]);nt.links.new(tone,mult.inputs[2]);nt.links.new(mult.outputs[0],p.inputs['Base Color'])
    original=p.inputs['Roughness'].links[0].from_socket
    rough_delta=math_node('ADD',math_node('MULTIPLY_ADD',patch,.08,-.04),math_node('MULTIPLY_ADD',fine,.045,-.0225))
    nt.links.new(math_node('ADD',original,rough_delta),p.inputs['Roughness'])
    # Weathered bare stone has greater relief; fine dust and fresh fracture are quieter.
    strength=math_node('MULTIPLY_ADD',exposure,.76,.15)
    height=math_node('ADD',math_node('MULTIPLY',coarse,.76),math_node('MULTIPLY',fine,.24))
    height=math_node('ADD',height,math_node('MULTIPLY',math_node('MULTIPLY',band,lamina_mask),.17))
    bump=nt.nodes.new('ShaderNodeBump');bump.name='ExposureControlledGrainAndLamina';bump.inputs['Distance'].default_value=.0022
    nt.links.new(strength,bump.inputs['Strength']);nt.links.new(height,bump.inputs['Height']);nt.links.new(bump.outputs['Normal'],p.inputs['Normal'])
    high['fine_surface_source']='Old front bed mask: warped 18mm lamination; exposed/fresh/dust masks control 16mm/5mm granular fields and roughness; no AO or image-derived normals'

def new_image(name,res,role):
    # Geometry probes keep float precision. Final RGB PNGs use standard 8-bit channels
    # at full 4K; 16-bit grain maps previously bloated the ground source to 216 MB.
    im=bpy.data.images.new(name,width=res,height=res,alpha=False,float_buffer=res<2048)
    im.colorspace_settings.name='sRGB' if role=='base_color' else 'Non-Color'
    return im

def target(low,image):
    if not low.data.materials:
        m=bpy.data.materials.new('GAME_'+low.name);m.use_nodes=True;low.data.materials.append(m)
    mat=low.data.materials[0];nt=mat.node_tree
    for nd in nt.nodes:nd.select=False
    nd=nt.nodes.get('BAKE_TARGET') or nt.nodes.new('ShaderNodeTexImage');nd.name='BAKE_TARGET';nd.image=image;nd.select=True;nt.nodes.active=nd

def bake_pair(high,low,image,role,projection,clear=True):
    sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.cycles.samples=1
    sc.render.threads_mode='FIXED';sc.render.threads=6
    for o in sc.objects:
        if o.type=='MESH':o.hide_render=True
    high.hide_render=False;low.hide_render=False
    target(low,image);select(high,low)
    if role!='normal':emit_source(high,'BaseColorSource' if role=='base_color' else 'RoughnessSource')
    result=bpy.ops.object.bake(type='NORMAL' if role=='normal' else 'EMIT',use_selected_to_active=True,
        cage_extrusion=projection['cage_extrusion'],max_ray_distance=projection['max_ray_distance'],
        margin=8 if image.size[0]>=2048 else 1,margin_type='EXTEND',use_clear=clear,target='IMAGE_TEXTURES',normal_space='TANGENT')
    restore_source(high);assert result=={'FINISHED'};print('BAKE_OK',low.name,role,image.size[:],flush=True)

def save_map(image,path):
    image.filepath_raw=str(path);image.file_format='PNG';image.save();image.pack()

def projection(high,low):
    bpy.context.view_layer.update()
    return U.measure_projection(low,[high],samples=3500,search=.06,quantile=.999)

def material(low,maps):
    mat=low.data.materials[0];M.pbr_from_textures(mat,maps,uv_map=low.data.uv_layers.active.name,ao_mode='none')
    return M.audit_material(mat,low)

def normal_check(low,image):
    return U.bake_sanity(image,'NORMAL',U.island_mask(low,image.size[0]))

def export(objects,path):
    select(*objects)
    for o in bpy.context.scene.objects:
        if o.type=='MESH':o.hide_render=o not in objects
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_apply=True,export_yup=True,export_texcoords=True,export_normals=True,export_tangents=True,
        export_materials='EXPORT',export_cameras=False,export_lights=False,export_extras=True)

def identity(path):return {'sha256':hashlib.sha256(Path(path).read_bytes()).hexdigest(),'bytes':Path(path).stat().st_size}

def write_json(path,data):Path(path).write_text(json.dumps(data,indent=2,ensure_ascii=False))
