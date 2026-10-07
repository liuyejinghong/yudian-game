"""Independent 512 transfer trial. Run build, then --verify in a new native process."""
import bpy, sys, json, hashlib, math, shutil, datetime, struct
from pathlib import Path
import numpy as np
from mathutils import Vector
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'art/source/environment/mars-outcrop-hifi-r2'))
import build as B, pipeline as P
U,M=B.U,B.M
FORMAL=[ROOT/'art/source/environment'/asset/(asset+suffix) for asset in ('mars-outcrop-hifi-r2','mars-ground-patch-hifi-r2') for suffix in ('.blend','.glb')]
def identities():return {str(p.relative_to(ROOT)):P.identity(p) for p in FORMAL}
def savejson(name,data):P.write_json(HERE/name,data)
def geometry(o):
    co=np.array([list(o.matrix_world@v.co) for v in o.data.vertices]);uv=np.array([list(v.uv) for v in o.data.uv_layers.active.data]) if o.data.uv_layers.active else np.zeros((0,2))
    assert np.isfinite(co).all() and np.isfinite(uv).all()
    assert all(p.area>1e-12 for p in o.data.polygons)
    return {'vertices':len(co),'triangles':P.triangles(o),'min':co.min(0).tolist(),'max':co.max(0).tolist(),'dimensions':(co.max(0)-co.min(0)).tolist(),'uv_loops':len(uv),'uv_min':uv.min(0).tolist() if len(uv) else None,'uv_max':uv.max(0).tolist() if len(uv) else None}
def checkuv(o):
    q=U.uv_qa(o,tex_res=512,analysis_res=1024,gap_px=4)
    assert q['zero_area_faces']==q['flipped_faces']==q['overlapping_island_pairs']==0,q
    assert not q['self_overlapping_islands'] and q['faces_crossing_tiles']==0,q
    return q

def build():
    assert bpy.app.version[:2]==(5,2)
    before=identities();savejson('formal-before.json',before)
    # A rerun preserves previous real evidence before replacing owned output.
    if (HERE/'trial.blend').exists():
        backup=HERE/'backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True)
        for path in list(HERE.glob('*.blend'))+list(HERE.glob('*.glb'))+list((HERE/'textures').glob('*.png'))+list((HERE/'renders').glob('*.png'))+list(HERE.glob('*.json')):shutil.copy2(path,backup/path.name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1
    sc.render.engine='CYCLES';sc.cycles.device='CPU';sc.render.threads_mode='FIXED';sc.render.threads=4
    hc=B.collection('SOURCE_HIGH_EDITABLE');lc=B.collection('GAME_EXPORT')
    high=B.rock_volume('HIGH_IndependentSandstone', [(-.38,-.24),(.11,-.34),(.43,-.13),(.31,.23),(-.15,.30),(-.43,.10)], .33,1941,hc,spacing=.022,vertical=.012,tilt=(.16,-.11),weather=np.array([.75,.30,.12,.18,.6,.9]))
    P.surface(high)
    low=P.clone_low(high,lc,'GAME_IndependentSandstone',.35)
    P.select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(30),island_margin=.01,area_weight=1,correct_aspect=True)
    bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.008)
    bpy.ops.object.mode_set(mode='OBJECT');q=checkuv(low)
    projection=U.measure_projection(low,[high],samples=3500,search=.08,quantile=1)
    assert projection['miss_pct']<2 and projection['wrong_part_pct']<1,projection
    maps={};bakes=[]
    for role in ('normal','base_color','roughness'):
        im=P.new_image('Trial_'+role,512,role)
        linear=np.array([.22,.195,.171]);encoded=np.where(linear<=.0031308,12.92*linear,1.055*linear**(1/2.4)-.055)
        fill=(*encoded,1) if role=='base_color' else ((.5,.5,1,1) if role=='normal' else (.86,.86,.86,1))
        im.pixels.foreach_set(np.tile(np.array(fill,np.float32),512*512))
        P.target(low,im);P.select(high,low)
        sc.cycles.samples=16
        if role!='normal':P.emit_source(high,'BaseColorSource' if role=='base_color' else 'RoughnessSource')
        result=bpy.ops.object.bake(type='NORMAL' if role=='normal' else 'EMIT',use_selected_to_active=True,cage_extrusion=projection['cage_extrusion'],max_ray_distance=projection['max_ray_distance'],margin=1,margin_type='EXTEND',use_clear=False,target='IMAGE_TEXTURES',normal_space='TANGENT')
        P.restore_source(high);assert result=={'FINISHED'}
        P.save_map(im,HERE/'textures'/(role+'.png'));maps[role]=im
        bakes.append({'role':role,'result':sorted(result),'selected_to_active':True,'threads':sc.render.threads,'samples':sc.cycles.samples})
    sanity=P.normal_check(low,maps['normal'])
    audit=P.material(low,maps)
    P.export([low],HERE/'trial.glb')
    for im in maps.values():im.filepath='//textures/'+im.name.removeprefix('Trial_')+'.png'
    high.hide_set(True);high.hide_render=True;low.hide_render=False
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'trial.blend'),compress=True,relative_remap=False)
    # Same camera/light for high and mapped low; source mesh remains untouched.
    world=bpy.data.worlds.new('TrialWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.48,.54,.62,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45;sc.world=world
    sun=bpy.data.objects.new('TrialSun',bpy.data.lights.new('TrialSun','SUN'));sc.collection.objects.link(sun);sun.data.energy=2.7;sun.data.angle=.12;sun.rotation_euler=(.5,-.45,-.6)
    camera=bpy.data.objects.new('TrialCamera',bpy.data.cameras.new('TrialCamera'));sc.collection.objects.link(camera);camera.location=(1.15,-1.55,.98);camera.rotation_euler=(Vector((0,0,.09))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=53;sc.camera=camera
    sc.render.resolution_x=960;sc.render.resolution_y=720;sc.render.resolution_percentage=100;sc.cycles.samples=32;sc.cycles.use_denoising=True;sc.view_settings.view_transform='AgX'
    for name,obj in [('high',high),('mapped-low',low)]:
        high.hide_render=obj!=high;low.hide_render=obj!=low;obj.hide_set(False)
        sc.render.filepath=str(HERE/'renders'/(name+'.png'));bpy.ops.render.render(write_still=True)
    report={'status':'BUILD_PASS_VERIFY_PENDING','blender':bpy.app.version_string,'high':geometry(high),'low':geometry(low),'uv':q,'projection':projection,'normal_sanity':sanity,'material_audit':audit,'bakes':bakes,'background_fill':{'base_color_linear':[.22,.195,.171],'base_color_encoded':encoded.tolist(),'normal':[.5,.5,1],'roughness':.86},'formal_unchanged':before==identities(),'skills_functions':['bx_uvbake.prepare_low','bx_uvbake.uv_qa','bx_uvbake.measure_projection','bx_uvbake.bake_sanity','bx_materials.write_vertex_values','bx_materials.pbr_from_textures','bx_materials.audit_material'],'helpers':['build.rock_volume','pipeline.clone_low','pipeline.surface','pipeline.emit_source/restore_source','pipeline.new_image/target/save_map/material/export'],'limits':'512 original small fragment transfer trial; no formal 4K or Godot/visual acceptance claimed'}
    assert report['formal_unchanged'];savejson('build.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)

def verify():
    expected=json.loads((HERE/'build.json').read_text())
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'trial.blend'))
    assert bpy.context.scene.unit_settings.system=='METRIC' and bpy.context.scene.unit_settings.scale_length==1
    high=bpy.data.collections['SOURCE_HIGH_EDITABLE'].objects[0];low=bpy.data.collections['GAME_EXPORT'].objects[0]
    assert geometry(high)['triangles']==expected['high']['triangles'];assert geometry(low)['triangles']==expected['low']['triangles']
    source_images=[{'name':im.name,'size':list(im.size),'packed':bool(im.packed_file),'path':im.filepath,'space':im.colorspace_settings.name} for im in bpy.data.images if im.type=='IMAGE']
    assert len(source_images)==3 and all(im['packed'] and im['size']==[512,512] and im['path'].startswith('//textures/') for im in source_images),source_images
    source_uv=checkuv(low);source_audit=M.audit_material(low.data.materials[0],low);assert not source_audit,source_audit
    backgrounds={}
    for role in ('base_color','normal','roughness'):
        im=bpy.data.images.load(str(HERE/'textures'/(role+'.png')),check_existing=False);im.colorspace_settings.name='sRGB' if role=='base_color' else 'Non-Color'
        backgrounds[role]=list(im.pixels[:4]);bpy.data.images.remove(im)
    assert np.allclose(backgrounds['base_color'][:3],[.22,.195,.171],atol=.002),backgrounds
    assert np.allclose(backgrounds['normal'][:3],[.5,.5,1],atol=.001),backgrounds
    assert np.allclose(backgrounds['roughness'][:3],[.86]*3,atol=.001),backgrounds
    bpy.ops.wm.read_factory_settings(use_empty=True);assert len(bpy.data.objects)==0
    bpy.ops.import_scene.gltf(filepath=str(HERE/'trial.glb'))
    objs=[o for o in bpy.data.objects if o.type=='MESH'];assert len(objs)==1
    o=objs[0];g=geometry(o);assert g['triangles']==expected['low']['triangles']
    assert np.allclose(g['dimensions'],expected['low']['dimensions'],atol=1e-6)
    q=checkuv(o);mat=o.data.materials[0];p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    assert p.inputs['Metallic'].default_value==0
    assert all(p.inputs[s].is_linked for s in ('Base Color','Roughness','Normal'))
    imported=[{'name':im.name,'size':list(im.size),'packed':bool(im.packed_file),'space':im.colorspace_settings.name} for im in bpy.data.images if im.type=='IMAGE']
    assert len(imported)>=3 and all(im['packed'] and im['size']==[512,512] for im in imported),imported
    blob=(HERE/'trial.glb').read_bytes();length=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+length]);assert all('bufferView' in im and 'uri' not in im for im in doc['images'])
    assert not any('occlusionTexture' in m for m in doc['materials'])
    binstart=20+length+8;pngs=[]
    for im in doc['images']:
        view=doc['bufferViews'][im['bufferView']];data=blob[binstart+view.get('byteOffset',0):binstart+view.get('byteOffset',0)+view['byteLength']]
        assert data[:8]==b'\x89PNG\r\n\x1a\n'
        w,h,depth,color=struct.unpack_from('>IIBB',data,16);assert (w,h)==(512,512)
        pngs.append({'name':im.get('name'),'width':w,'height':h,'bit_depth':depth,'color_type':color,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
    gm=doc['materials'][0];assert gm['pbrMetallicRoughness'].get('metallicFactor',1)==0
    assert 'baseColorTexture' in gm['pbrMetallicRoughness'] and 'metallicRoughnessTexture' in gm['pbrMetallicRoughness'] and 'normalTexture' in gm
    formal=json.loads((HERE/'formal-before.json').read_text());assert formal==identities()
    report={'status':'PASS','blender':bpy.app.version_string,'native_source_reopen':True,'source_images':source_images,'source_uv':source_uv,'source_material_audit':source_audit,'empty_scene_glb_import':True,'glb_geometry':g,'glb_uv':q,'glb_images':imported,'embedded_images':len(doc['images']),'embedded_pngs':pngs,'background_png_roundtrip':backgrounds,'pbr_connections':['Base Color','Roughness','Normal'],'metallic':0,'no_occlusion_texture':True,'formal_hashes_unchanged':True,'formal':formal,'outputs':{str(p.relative_to(HERE)):P.identity(p) for p in [HERE/'trial.blend',HERE/'trial.glb',HERE/'trial.py',*sorted((HERE/'textures').glob('*.png')),*sorted((HERE/'renders').glob('*.png'))]}}
    savejson('verification.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)
if __name__=='__main__':
    verify() if '--verify' in sys.argv else build()
