"""512 base/rough response study. Frozen geometry, UV and 4K normal are retained."""
import bpy,sys,json,hashlib,struct,copy
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
import pipeline as P

STAGE=HERE/'stages/13-meso-response'
VIEWS={'front':B.VIEWS['front'],'detail':((2.6,-6,2.8),(.15,-.45,.85),62),
       'rear-detail':((-4.1,5.4,2.6),(-.38,.69,1.03),56),
       'bed-close':((-1.7,-4.0,1.8),(-1.45,-1.02,1.08),72)}

def meso_response(high):
    """Re-use accepted exposure fields; change only base color and roughness."""
    nt=high.data.materials[0].node_tree;p=nt.nodes['Principled BSDF']
    def math(op,*args,name=None):
        n=nt.nodes.new('ShaderNodeMath');n.operation=op
        if name:n.name=name
        for i,value in enumerate(args):
            if isinstance(value,(int,float)):n.inputs[i].default_value=value
            else:nt.links.new(value,n.inputs[i])
        return n.outputs[0]
    def mix(factor,a,b,name,mode='MIX'):
        n=nt.nodes.new('ShaderNodeMixRGB');n.name=name;n.blend_type=mode
        for i,value in enumerate((factor,a,b)):
            if isinstance(value,(int,float)):n.inputs[i].default_value=value
            elif isinstance(value,tuple):n.inputs[i].default_value=value
            else:nt.links.new(value,n.inputs[i])
        return n.outputs[0]
    dust=next(n.outputs['Color'] for n in nt.nodes if n.type=='VERTEX_COLOR' and n.layer_name=='DustMask')
    # This is the existing final fresh mask, including the existing rear fracture.
    fresh_factor=nt.nodes['FreshRearFaceTone'].inputs[0].links[0].from_node
    assert fresh_factor.operation=='MULTIPLY' and fresh_factor.inputs[1].default_value==np.float32(.28)
    fresh=fresh_factor.inputs[0].links[0].from_socket
    bare=math('SUBTRACT',1,dust);fresh_bare=math('MULTIPLY',bare,fresh)
    old_bare=math('MULTIPLY',bare,math('SUBTRACT',1,fresh))
    patch=nt.nodes['WeatheringPatches'].outputs['Fac']
    ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.name='MesoWeatheringContrast'
    ramp.color_ramp.interpolation='EASE'
    ramp.color_ramp.elements[0].position=.29;ramp.color_ramp.elements[1].position=.70
    nt.links.new(patch,ramp.inputs[0]);variation=ramp.outputs['Color']
    original_base=p.inputs['Base Color'].links[0].from_socket
    old_tone=math('MULTIPLY_ADD',variation,.40,.73)
    multiplier=math('ADD',1,math('MULTIPLY',old_bare,math('SUBTRACT',old_tone,1)))
    colored=mix(1,original_base,multiplier,'MesoOldBareTone','MULTIPLY')
    colored=mix(math('MULTIPLY',fresh_bare,.65),colored,(.205,.204,.197,1),'MesoFreshNeutralTone')
    colored=mix(math('MULTIPLY',dust,.22),colored,(.295,.255,.212,1),'MesoDustWarmTone')
    nt.links.new(colored,p.inputs['Base Color'])
    old_rough=math('MULTIPLY_ADD',variation,.14,.66)
    fresh_rough=math('MULTIPLY_ADD',variation,.05,.59)
    dust_rough=math('MULTIPLY_ADD',variation,.055,.91)
    rough=math('ADD',math('MULTIPLY',old_bare,old_rough),
        math('ADD',math('MULTIPLY',fresh_bare,fresh_rough),math('MULTIPLY',dust,dust_rough)))
    # Preserve a small part of accepted fine roughness; it adds no new grain normal.
    fine=nt.nodes['FineMineralGrains'].outputs['Fac']
    rough=math('ADD',rough,math('MULTIPLY_ADD',fine,.018,-.009),name='MesoExposureRoughness')
    nt.links.new(rough,p.inputs['Roughness'])
    return {'existing_masks':['DustMask','FreshFractureMask','RearPlane/RearOnly/RearRightFracture','WeatheringPatches'],
        'old_bare_color_multiplier':[.73,1.13],'old_patch_color_ramp':[.29,.70],
        'fresh_mix_linear_rgb':[.205,.204,.197],'fresh_mix_strength':.65,
        'dust_mix_linear_rgb':[.295,.255,.212],'dust_mix_strength':.22,
        'roughness_old_bare':[.66,.80],'roughness_fresh':[.59,.64],'roughness_dust':[.91,.965],
        'fine_roughness_amplitude':.009,'normal_changed':False,
        'scale':'Existing 3D weathering field: scale 3.7 per metre, mesoscopic coverage; no new periodic layer pattern'}

def mesh_identity(o):
    me=o.data;h=hashlib.sha256()
    for coll,attr,n,dtype in ((me.vertices,'co',3,np.float32),(me.loops,'vertex_index',1,np.int32),
            (me.uv_layers.active.data,'uv',2,np.float32)):
        a=np.empty(len(coll)*n,dtype);coll.foreach_get(attr,a);h.update(a.tobytes())
    return h.hexdigest()

def candidate_glb(source,destination,maps):
    """Replace only two image payloads in the existing GLB; all accessor bytes stay exact."""
    raw=source.read_bytes();magic,version,total=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and total==len(raw)
    length,kind=struct.unpack_from('<I4s',raw,12);assert kind==b'JSON'
    doc=json.loads(raw[20:20+length]);original=copy.deepcopy(doc)
    bin_start=20+length;bin_length,kind=struct.unpack_from('<I4s',raw,bin_start);assert kind==b'BIN\0'
    payload=raw[bin_start+8:bin_start+8+bin_length]
    mat=doc['materials'][0];pbr=mat['pbrMetallicRoughness'];replacements={}
    for role,texture in [('base_color',pbr['baseColorTexture']),('roughness',pbr['metallicRoughnessTexture'])]:
        image=doc['images'][doc['textures'][texture['index']]['source']]
        replacements[image['bufferView']]=maps[role].read_bytes()
    normal_view=doc['images'][doc['textures'][mat['normalTexture']['index']]['source']]['bufferView']
    rebuilt=bytearray();unchanged=[]
    for index,view in enumerate(doc['bufferViews']):
        old=payload[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        data=replacements.get(index,old)
        while len(rebuilt)%4:rebuilt.append(0)
        view['byteOffset']=len(rebuilt);view['byteLength']=len(data);rebuilt.extend(data)
        if index not in replacements:
            assert data==old;unchanged.append(index)
    assert normal_view in unchanged
    doc['buffers'][0]['byteLength']=len(rebuilt)
    for role in ('base_color','roughness'):
        mat['extras']['bx_texture_set'][role]=str(maps[role].relative_to(B.ROOT))
    j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
    rebuilt+=b'\0'*((-len(rebuilt))%4)
    destination.write_bytes(struct.pack('<4sII',b'glTF',2,12+8+len(j)+8+len(rebuilt))+
        struct.pack('<I4s',len(j),b'JSON')+j+struct.pack('<I4s',len(rebuilt),b'BIN\0')+rebuilt)
    # Scene, geometry and material parameters do not change, only map payload/metadata and offsets.
    a=copy.deepcopy(doc);b=copy.deepcopy(original)
    for d in (a,b):
        for v in d['bufferViews']:v.pop('byteOffset',None);v.pop('byteLength',None)
        d['buffers'][0].pop('byteLength');d['materials'][0].pop('extras',None)
    assert a==b
    return {'unchanged_buffer_views':unchanged,'replaced_image_views':sorted(replacements),
        'normal_png_sha256':hashlib.sha256(payload[original['bufferViews'][normal_view]['byteOffset']:
            original['bufferViews'][normal_view]['byteOffset']+original['bufferViews'][normal_view]['byteLength']]).hexdigest(),
        'geometry_uv_normals_tangents_indices_and_normal_png_exact':True}

def run():
    STAGE.mkdir(parents=True,exist_ok=True);textures=STAGE/'textures';textures.mkdir(exist_ok=True)
    source=HERE/'mars-outcrop-hifi-r2.blend';glb=HERE/'mars-outcrop-hifi-r2.glb'
    protected=[source,glb,*sorted((HERE/'textures').glob('*.png')),
        HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.blend',
        HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.glb']
    identities={str(p.relative_to(B.ROOT)):P.identity(p) for p in protected}
    bpy.ops.wm.open_mainfile(filepath=str(source));high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    identity=mesh_identity(low);high_geometry=np.empty(len(high.data.vertices)*3,np.float32);high.data.vertices.foreach_get('co',high_geometry)
    normal=next(n.image for n in low.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image and Path(n.image.filepath).name=='normal.png')
    normal_hash=hashlib.sha256(normal.packed_file.data).hexdigest()
    normal_socket=high.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Normal'].links[0].from_socket
    params=meso_response(high)
    assert high.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Normal'].links[0].from_socket==normal_socket
    projection=json.loads((HERE/'stages/10-bake-test/test.json').read_text())['projection']
    maps={'normal':normal};map_paths={}
    for role in ('base_color','roughness'):
        im=P.new_image('meso-response-'+role,512,role)
        # At 512, subpixel UV charts cannot provide covered texels everywhere.
        # Seed only this disposable probe with a neutral surface, not black sky.
        fill=(.22,.195,.171,1) if role=='base_color' else (.78,.78,.78,1)
        im.pixels.foreach_set(np.tile(np.array(fill,np.float32),(512*512,1)).ravel());im.update()
        P.bake_pair(high,low,im,role,projection,clear=False)
        path=textures/(role+'.png');P.save_map(im,path);maps[role]=im;map_paths[role]=path
    audit=P.material(low,maps);assert not audit['problems'] if isinstance(audit,dict) else not audit
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    B.renders(STAGE,prefix='candidate-',views=VIEWS)
    assert mesh_identity(low)==identity
    high_after=np.empty_like(high_geometry);high.data.vertices.foreach_get('co',high_after);assert np.array_equal(high_geometry,high_after)
    assert hashlib.sha256(normal.packed_file.data).hexdigest()==normal_hash
    for role in ('base_color','roughness'):maps[role].filepath='//textures/'+role+'.png'
    normal.filepath='//../../textures/normal.png'
    P.portable_metadata()
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'candidate.blend'),compress=True,relative_remap=False)
    glb_proof=candidate_glb(glb,STAGE/'candidate.glb',map_paths)
    assert all(P.identity(B.ROOT/p)==old for p,old in identities.items())
    report={'status':'512_CANDIDATE_PENDING_FIXED_GODOT_REVIEW','formal_files_changed':False,
        'protected_files':identities,'parameters':params,'game_mesh_uv_sha256':identity,
        'normal_packed_png_sha256':normal_hash,'source_high_geometry_unchanged':True,
        'glb_proof':glb_proof,'material_audit':audit,
        'maps':{role:P.identity(path) for role,path in map_paths.items()},
        'candidate_glb':P.identity(STAGE/'candidate.glb'),
        'probe_resolution_limit':'512 charts have subpixel regions; unbaked background initialized to neutral base (.22,.195,.171) / roughness .78 to avoid black seams. Final resolution unchanged and no final maps replaced.',
        'scope':'Two 512 color/roughness maps only; original full-resolution normal unchanged; no final 4K bake'}
    P.write_json(STAGE/'stage.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)

if __name__=='__main__':run()
