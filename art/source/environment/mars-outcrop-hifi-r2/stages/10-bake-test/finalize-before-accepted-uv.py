"""Measured bake test; --finish builds maps only after inspected test evidence."""
import bpy,bmesh,json,sys
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P
TEST=HERE/'stages/10-bake-test';TEST.mkdir(parents=True,exist_ok=True)

def local_uv_repair(low,qa):
    """Repair measured folds only; preserve the approved geometry and other charts."""
    a=P.U._Arrays(low);labels,_=P.U._uv_islands(a);t,tf=P.U._tris(a,a.uv)
    folds=[];review=[]
    for k in qa['self_overlapping_islands']:
        fm=labels==k;overlap=P.U._self_overlap(a.uv,t,tf,fm,res=1024)
        review.append({'island':k,'faces':int(fm.sum()),'self_overlap_island_normalized':overlap})
        if overlap>.005:folds.extend(np.nonzero(fm)[0].tolist())
    a.free();P.U.seams_from_uv_islands(low)
    report={'before':review,'repaired_faces':len(folds),'solver':None}
    if folds:report['solver']=P.U.unwrap(low,method='BEST',faces=folds)
    P.write_json(TEST/'uv-repair-progress.json',report)
    if report['solver']:
        assert not report['solver']['folding_islands'],report
        assert all(not faces for faces in report['solver']['unsolved_faces'].values()),report
    P.select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.005)
    bpy.ops.object.mode_set(mode='OBJECT')
    qa=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
    report['after']=qa
    P.write_json(TEST/'uv-repair-progress.json',report)
    return qa,report

def uv_gate(low,qa):
    assert qa['flipped_faces']==0 and qa['zero_area_faces']==0,qa
    assert qa['overlapping_island_pairs']==0 and qa['faces_crossing_tiles']==0,qa
    assert qa['min_gap_px'] is None or qa['min_gap_px']>=16,qa
    assert qa['per_tile']['1001']['border_px']>=8 if '1001' in qa['per_tile'] else qa['per_tile'][1001]['border_px']>=8
    a=P.U._Arrays(low);labels,_=P.U._uv_islands(a);t,tf=P.U._tris(a,a.uv);remaining=[]
    for k in qa['self_overlapping_islands']:
        ratio=P.U._self_overlap(a.uv,t,tf,labels==k,res=1024)
        remaining.append({'island':k,'self_overlap_island_normalized':ratio})
        assert ratio<=.0001,remaining
    a.free();return {'status':'PASS','analysis_resolution':1024,'tiny_raster_residuals':remaining,
        'maximum_remaining_island_overlap_fraction':.0001,'known_substantive_folds_resolved':True}

def clay_comparison(high,low,image):
    original_high=high.data.materials[0];original_low=low.data.materials[0]
    clay=B.clay_material();high.data.materials[0]=clay;low.data.materials[0]=clay
    views={'detail':((2.6,-6,2.8),(.15,-.45,.85),62)}
    high.hide_render=False;low.hide_render=True;B.renders(TEST,prefix='high-',views=views)
    high.hide_render=True;low.hide_render=False;B.renders(TEST,prefix='bare-low-',views=views)
    mapped=bpy.data.materials.new('REVIEW_MappedClay');mapped.use_nodes=True
    P.M.pbr_from_textures(mapped,{'normal':image},uv_map=low.data.uv_layers.active.name,ao_mode='none')
    bs=mapped.node_tree.nodes.get('Principled BSDF') or next(n for n in mapped.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(.32,.32,.32,1);bs.inputs['Roughness'].default_value=.87
    low.data.materials[0]=mapped;B.renders(TEST,prefix='mapped-low-',views=views)
    high.data.materials[0]=original_high;low.data.materials[0]=original_low

def test():
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'stages/09-exposed-bed-material/source-high.blend'))
    high=bpy.data.objects['HIGH_ParentSandstone'];game=B.collection('GAME_EXPORT')
    low=P.clone_low(high,game,'MarsOutcrop',.24)
    before=P.triangles(low)
    bm=bmesh.new();bm.from_mesh(low.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0002)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0002)
    bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(low.data);bm.free();low.data.update()
    for face in low.data.polygons:face.use_smooth=True
    uv=P.unwrap(low);uv,uv_repair=local_uv_repair(low,uv);validation=uv_gate(low,uv);P.surface(high)
    proj=P.projection(high,low)
    report={'version':bpy.app.version_string,'stage':'10-real-selected-to-active-test','high_triangles':P.triangles(high),
            'game_triangles':P.triangles(low),'uv':uv,'projection':proj,'normal_source':'Independent approved high geometry only; no image-derived or shader normal in this geometry probe'}
    report['low_mesh_cleanup']={'merge_and_degenerate_edge_tolerance_m':.0002,'triangles_before':before,'triangles_after':P.triangles(low),'approved_high_unchanged':True}
    report['uv_repair']=uv_repair;report['uv_validation']=validation
    report['approved_geometry_source']={'path':'stages/09-exposed-bed-material/source-high.blend',**P.identity(HERE/'stages/09-exposed-bed-material/source-high.blend')}
    P.write_json(TEST/'pre-bake.json',report)
    assert validation['status']=='PASS'
    image=P.new_image('outcrop-normal-test',512,'normal');P.bake_pair(high,low,image,'normal',proj)
    report['normal']=P.normal_check(low,image);P.save_map(image,TEST/'normal-test.png')
    clay_comparison(high,low,image)
    P.write_json(TEST/'test.json',report)
    bpy.ops.wm.save_as_mainfile(filepath=str(TEST/'prepared.blend'),compress=True)
    print('RESULT_JSON='+json.dumps(report),flush=True)

def finish():
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'))
    high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    report=json.loads((TEST/'test.json').read_text());proj=report['projection']
    assert report['uv_validation']['status']=='PASS' and report['uv_validation']['known_substantive_folds_resolved']
    textures=HERE/'textures';textures.mkdir(exist_ok=True);maps={}
    P.geologic_finish(high)
    report['surface_sampling']='Vertex dust/fracture masks control separate 3D material fields: exposed-bed 18mm lamination and 16mm/5mm granular spacing. Final normal combines the independent high geometry and this explicitly separate material relief after the geometry-only 512 probe.'
    for role in ('normal','base_color','roughness'):
        image=P.new_image('outcrop-'+role,4096,role);P.bake_pair(high,low,image,role,proj)
        P.save_map(image,textures/(role+'.png'));maps[role]=image
    report['final_normal']=P.normal_check(low,maps['normal']);report['material']=P.material(low,maps)
    report['texture_precision']='4096x4096 RGB PNG, 8 bits per channel'
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    B.renders(HERE/'renders',views=B.VIEWS)
    P.export([low],HERE/'mars-outcrop-hifi-r2.glb')
    for im in maps.values():im.filepath='//textures/'+im.filepath.rsplit('/',1)[-1]
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'mars-outcrop-hifi-r2.blend'),compress=True,relative_remap=False)
    report.update(stage='FINAL_CANDIDATE_PENDING_NATIVE_REOPEN_AND_GLB_IMPORT',dimensions=list(low.dimensions),
        files={name:P.identity(HERE/name) for name in ['mars-outcrop-hifi-r2.blend','mars-outcrop-hifi-r2.glb']})
    P.write_json(HERE/'manifest.json',report);print('RESULT_JSON='+json.dumps(report),flush=True)

def repair_existing_uv():
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'))
    low=bpy.data.objects['MarsOutcrop'];high=bpy.data.objects['HIGH_ParentSandstone']
    report=json.loads((TEST/'test.json').read_text())
    qa=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
    qa,repair=local_uv_repair(low,qa);report['uv']=qa;report['uv_repair']=repair;report['uv_validation']=uv_gate(low,qa)
    report['approved_geometry_source']={'path':'stages/09-exposed-bed-material/source-high.blend',**P.identity(HERE/'stages/09-exposed-bed-material/source-high.blend')}
    image=P.new_image('outcrop-normal-test-repaired',512,'normal');P.bake_pair(high,low,image,'normal',report['projection'])
    report['normal']=P.normal_check(low,image);P.save_map(image,TEST/'normal-test.png');clay_comparison(high,low,image)
    P.write_json(TEST/'test.json',report);bpy.ops.wm.save_as_mainfile(filepath=str(TEST/'prepared.blend'),compress=True)
    print('RESULT_JSON='+json.dumps({'uv':report['uv'],'normal':report['normal'],'status':'UV_REPAIRED_512_REBAKED'}))

if __name__=='__main__':
    if '--finish' in sys.argv:finish()
    elif '--repair-uv' in sys.argv:repair_existing_uv()
    else:test()
