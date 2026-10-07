"""Three real 4K selected-to-active maps for the frozen complete-face candidate."""
import bpy,json,sys,hashlib,struct,zlib
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B
import pipeline as P
import surface_stage17 as S
INPUT=HERE/'stages/17-continuous-face-response/source-clean.blend'
STAGE=HERE/'stages/19-complete-face-4k';TEXTURES=STAGE/'textures'

def mesh_hash(o):
    me=o.data;digest=hashlib.sha256()
    for data,key,width,kind in ((me.vertices,'co',3,np.float32),(me.loops,'vertex_index',1,np.int32),(me.corner_normals,'vector',3,np.float32),(me.uv_layers.active.data,'uv',2,np.float32)):
        a=np.empty(len(data)*width,kind);data.foreach_get(key,a);digest.update(a.tobytes())
    return digest.hexdigest()

def first_pixel(path):
    data=path.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n';offset=8;parts=[];head=None
    while offset<len(data):
        size=struct.unpack_from('>I',data,offset)[0];kind=data[offset+4:offset+8];payload=data[offset+8:offset+8+size];offset+=12+size
        if kind==b'IHDR':head=struct.unpack('>IIBBBBB',payload)
        if kind==b'IDAT':parts.append(payload)
    assert head[:4]==(4096,4096,8,2),head
    raw=zlib.decompress(b''.join(parts));return list(raw[1:4])

def run():
    TEXTURES.mkdir(parents=True,exist_ok=True)
    protected=[HERE/'mars-outcrop-hifi-r2.blend',HERE/'mars-outcrop-hifi-r2.glb',*sorted((HERE/'textures').glob('*.png')),
        HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.blend',HERE.parent/'mars-ground-patch-hifi-r2/mars-ground-patch-hifi-r2.glb']
    identities={str(p.relative_to(B.ROOT)):P.identity(p) for p in protected}
    preservation=json.loads((INPUT.parent/'clean-preservation.json').read_text())
    assert preservation['status']=='PASS' and preservation['candidate']==P.identity(INPUT)
    bpy.ops.wm.open_mainfile(filepath=str(INPUT));high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    report={'status':'MEASURING_BEFORE_4K_BAKE','input':{'path':str(INPUT.relative_to(B.ROOT)),**P.identity(INPUT)},
        'accepted_source_preview':'Root and independent Sol accepted stage18 2K continuity and backlit intrinsic detail, explicitly authorizing this frozen 4K candidate; final Godot/Sol verdict remains pending',
        'high_triangles':P.triangles(high),'game_triangles':P.triangles(low),'preservation':preservation,
        'degenerate_repair':json.loads((INPUT.parent/'degenerate-repair.json').read_text()),'protected_files':identities}
    report['uv']=P.U.uv_qa(low,tex_res=4096,analysis_res=2048,gap_px=16)
    qa=report['uv'];assert qa['flipped_faces']==qa['zero_area_faces']==qa['overlapping_island_pairs']==qa['faces_crossing_tiles']==0,qa
    assert not qa['self_overlapping_islands'],qa['self_overlapping_islands']
    measured=P.projection(high,low);old=json.loads((HERE/'stages/10-bake-test/test.json').read_text())['projection']
    projection={**measured,'cage_extrusion':max(old['cage_extrusion'],measured['cage_extrusion']),
        'max_ray_distance':max(old['max_ray_distance'],measured['max_ray_distance'])}
    assert measured['miss_pct']==0 and measured['uncovered_pct']==0,measured
    report['projection']={'measured':measured,'baseline':old,'used':projection,
        'method':'Measured changed pair; do not reduce the already validated baseline ray envelope on unchanged thin beds'}
    previous=json.loads((HERE/'stages/18-complete-face-2k/stage.json').read_text())
    assert previous['input']['sha256']==P.identity(INPUT)['sha256']
    report['game_geometry_uv_normal_sha256']=mesh_hash(low)
    assert report['game_geometry_uv_normal_sha256']==previous['game_geometry_uv_normal_sha256']
    report['frozen_stage18_input']=previous['input']
    report['bake_padding_px']=8
    assert qa['min_gap_px']>=38
    P.write_json(STAGE/'pre-bake.json',report);print('PRE_BAKE_READY='+json.dumps({'projection':report['projection'],'uv':{k:qa[k] for k in ('flipped_faces','zero_area_faces','overlapping_island_pairs','self_overlapping_islands','td_px_per_m','min_gap_px')}}),flush=True)
    linear=np.array([.220,.195,.171]);encoded=np.where(linear<=.0031308,12.92*linear,1.055*np.power(linear,1/2.4)-.055)
    fills={'normal':(.5,.5,1,1),'base_color':(*encoded,1),'roughness':(.85,.85,.85,1)};maps={}
    for role in ('normal','base_color','roughness'):
        im=P.new_image('stage19-'+role,4096,role);im.pixels.foreach_set(np.tile(np.array(fills[role],np.float32),(4096*4096,1)).ravel());im.update()
        P.bake_pair(high,low,im,role,projection,clear=False);path=TEXTURES/(role+'.png');P.save_map(im,path);maps[role]=im
        pixel=first_pixel(path);expected=np.rint(np.array(fills[role][:3])*255).astype(int)
        assert max(abs(np.array(pixel)-expected))<=1,(role,pixel,expected)
        report.setdefault('maps',{})[role]={'path':str(path.relative_to(B.ROOT)),**P.identity(path),'size':[4096,4096],'png_bit_depth':8,'neutral_border_pixel':pixel}
        P.write_json(STAGE/'bake-progress.json',report)
    report['normal_sanity']=P.normal_check(low,maps['normal']);assert not report['normal_sanity']['problems'],report['normal_sanity']
    report['material_audit']=P.material(low,maps);assert not report['material_audit'],report['material_audit']
    assert mesh_hash(low)==report['game_geometry_uv_normal_sha256']
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    S.STAGE=STAGE;S.fixed_renders('baked-')
    stripped=[]
    for ob in (high,low):
        for attr in list(ob.data.attributes):
            if attr.name.startswith('stage17_'):stripped.append(ob.name+':'+attr.name);ob.data.attributes.remove(attr)
    report['removed_stage_attributes']=stripped
    # Legacy packed maps and review materials are unnecessary duplicates.
    for mat in list(bpy.data.materials):
        if mat.users==0:bpy.data.materials.remove(mat)
    for im in list(bpy.data.images):
        if im not in maps.values() and im.users==0:bpy.data.images.remove(im)
    for role,im in maps.items():im.filepath='//textures/'+role+'.png'
    P.portable_metadata();P.export([low],STAGE/'candidate.glb')
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGE/'candidate.blend'),compress=True,relative_remap=False)
    report['files']={name:P.identity(STAGE/name) for name in ('candidate.blend','candidate.glb')}
    assert all(P.identity(B.ROOT/p)==old for p,old in identities.items())
    report['source_and_glb_under_100MiB']=all(f['bytes']<104857600 for f in report['files'].values())
    assert report['source_and_glb_under_100MiB'],report['files']
    report.update(status='4K_CANDIDATE_PENDING_NATIVE_REOPEN_AND_FIXED_GODOT_REVIEW',formal_files_changed=False,
        source_material='Accepted stage17 complete-face shallow relief and intrinsic mineral variation over the stage11 graph; no lighting or AO is baked',
        runtime_lighting_changed=False)
    P.write_json(STAGE/'stage.json',report);print('RESULT_JSON='+json.dumps({k:report[k] for k in ('status','files','normal_sanity','high_triangles','game_triangles')}),flush=True)
if __name__=='__main__':run()
