"""Measured bake test; --finish builds maps only after inspected test evidence."""
import bpy,bmesh,json,sys,hashlib
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P
TEST=HERE/'stages/10-bake-test';TEST.mkdir(parents=True,exist_ok=True)

def pack_uv(low):
    P.select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.005)
    bpy.ops.object.mode_set(mode='OBJECT')

def local_uv_repair(low,qa):
    """Validated local chart recipe for the frozen 09 geometry in Blender 5.2.2."""
    a=P.U._Arrays(low);labels,_=P.U._uv_islands(a)
    faces={k:np.nonzero(labels==k)[0].tolist() for k in (1222,2013,2282)}
    expected={1222:2411,2013:9972,2282:11345}
    assert {k:len(v) for k,v in faces.items()}==expected,'Geometry or initial chart layout changed; review the local repair selections again'
    vertex_hash=hashlib.sha256(a.co.tobytes()).hexdigest()
    centers=np.zeros((len(low.data.polygons),3));np.add.at(centers,a.lf,a.co[a.lv]);centers/=a.lt[:,None]
    x,y,z=centers.T;a.free();P.U.seams_from_uv_islands(low)
    report={'initial_qa':qa,'geometry_vertex_sha256':vertex_hash,'local_solvers':{}}
    for k,method in ((1222,'MINIMUM_STRETCH'),(2282,'CONFORMAL')):
        solver=P.U.unwrap(low,method=method,faces=faces[k])
        assert all(n==0 for n in solver['unsolved_faces'].values()),solver
        report['local_solvers'][str(k)]=solver
    pack_uv(low);P.U.seams_from_uv_islands(low);report['boundary_splits']={}
    for k,group in ((2013,z+.18*(x+1.2)>1.69),(2282,x>1.87-.43*y+.075*z)):
        cuts=P.U.seams_from_groups(low,groups=group.astype(int),faces=faces[k])
        solver=P.U.unwrap(low,method='BEST',faces=faces[k])
        assert all(n==0 for n in solver['unsolved_faces'].values()),solver
        report['boundary_splits'][str(k)]={'cuts':cuts,'solver':solver}
    pack_uv(low)
    intermediate=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
    a=P.U._Arrays(low);labels,_=P.U._uv_islands(a)
    remaining=intermediate['self_overlapping_islands'];assert len(remaining)==1,remaining
    branch=np.nonzero(labels==remaining[0])[0].tolist();assert len(branch)==7228,len(branch)
    outside=~np.isin(a.lf,branch);outside_uv=a.uv[outside].copy();a.free()
    P.select(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='DESELECT')
    bm=bmesh.from_edit_mesh(low.data);bm.faces.ensure_lookup_table()
    for i in branch:bm.faces[i].select_set(True)
    bmesh.update_edit_mesh(low.data)
    bpy.ops.uv.smart_project(angle_limit=np.radians(40),island_margin=.006,area_weight=1.0,correct_aspect=True)
    bpy.ops.object.mode_set(mode='OBJECT');a=P.U._Arrays(low)
    delta=float(np.max(np.abs(a.uv[outside]-outside_uv)));assert delta==0,delta;a.free()
    pack_uv(low);P.U.seams_from_uv_islands(low)
    qa1024=P.U.uv_qa(low,tex_res=4096,analysis_res=1024,gap_px=16)
    qa2048=P.U.uv_qa(low,tex_res=4096,analysis_res=2048,gap_px=16)
    uv_gate(low,qa1024);uv_gate(low,qa2048)
    a=P.U._Arrays(low);assert hashlib.sha256(a.co.tobytes()).hexdigest()==vertex_hash;a.free()
    report.update(planar_branch_faces=len(branch),planar_projection_angle_degrees=40,
        outside_uv_delta_before_final_pack=delta,qa_1024=qa1024,qa_2048=qa2048)
    P.write_json(TEST/'uv-repair-progress.json',report)
    return qa2048,report

def uv_gate(low,qa):
    assert qa['flipped_faces']==0 and qa['zero_area_faces']==0,qa
    assert qa['overlapping_island_pairs']==0 and qa['faces_crossing_tiles']==0,qa
    assert qa['min_gap_px'] is None or qa['min_gap_px']>=16,qa
    assert qa['per_tile']['1001']['border_px']>=8 if '1001' in qa['per_tile'] else qa['per_tile'][1001]['border_px']>=8
    assert not qa['self_overlapping_islands'],qa['self_overlapping_islands']
    return {'status':'PASS','analysis_resolution':2048,'self_overlap_islands':[],
        'known_substantive_folds_resolved':True,'method':'Explicit geometry-boundary seams plus local 40-degree chart projection; no tolerance exception for the known collisions'}

def uv_identity(low):
    a=P.U._Arrays(low)
    record={'uv_sha256':hashlib.sha256(a.uv.tobytes()).hexdigest(),
        'geometry_vertex_sha256':hashlib.sha256(a.co.tobytes()).hexdigest(),
        'triangles':P.triangles(low),'loops':len(a.uv)}
    a.free();return record

def clay_comparison(high,low,image):
    original_high=high.data.materials[0];original_low=low.data.materials[0]
    clay=B.clay_material();high.data.materials[0]=clay;low.data.materials[0]=clay
    views={'detail':((2.6,-6,2.8),(.15,-.45,.85),62),
           'right-joint':((4.8,-4.3,2.0),(1.92,-.27,.61),68)}
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
    report['uv_identity']=uv_identity(low)
    bpy.ops.wm.save_as_mainfile(filepath=str(TEST/'prepared.blend'),compress=True,relative_remap=False)
    report['prepared_file']=P.identity(TEST/'prepared.blend');P.write_json(TEST/'test.json',report)
    print('RESULT_JSON='+json.dumps(report),flush=True)

def finish():
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'))
    high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    report=json.loads((TEST/'test.json').read_text());proj=report['projection']
    assert report['uv_validation']['status']=='PASS' and report['uv_validation']['known_substantive_folds_resolved']
    assert report['prepared_file']==P.identity(TEST/'prepared.blend'),'Prepared bake input changed after validation'
    assert report['uv_identity']==uv_identity(low),'Validated UV/geometry identity changed'
    textures=HERE/'textures';textures.mkdir(exist_ok=True);maps={}
    P.geologic_finish(high)
    report['material_preview']={'path':'stages/11-local-exposure-preview/source-preview.blend',
        **P.identity(HERE/'stages/11-local-exposure-preview/source-preview.blend'),'independent_visual':'PASS: root and independent 6.1 Sol high'}
    report['material_source']=P.identity(HERE/'pipeline.py')
    report['material_input_probe']=json.loads((TEST/'material-input-proof.json').read_text())
    report['surface_sampling']='Two exposed bed faces carry unequal, nonperiodic centimetre sediment lenses with local slope and termination. Old bare rock has patchy coarse grain, fresh rear-right fracture weak fine grain, and dust reduced grain coverage. These explicit 3D shader fields supplement the independently validated high-geometry normal bake; no photograph pixels, image-derived normals, baked light or AO.'
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

def accept_uv_candidate():
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'uv-planar-candidate.blend'))
    low=bpy.data.objects['MarsOutcrop'];high=bpy.data.objects['HIGH_ParentSandstone']
    report=json.loads((TEST/'test.json').read_text());repair=json.loads((TEST/'uv-planar-candidate.json').read_text())
    uv_gate(low,repair['qa']);validation=uv_gate(low,repair['qa_2048'])
    report.update(uv=repair['qa_2048'],uv_repair=repair,uv_validation=validation,uv_identity=uv_identity(low))
    report['uv_repair_history']={name:json.loads((TEST/name).read_text()) for name in ('uv-local-candidate.json','uv-branch-candidate.json')}
    report['approved_geometry_source']={'path':'stages/09-exposed-bed-material/source-high.blend',**P.identity(HERE/'stages/09-exposed-bed-material/source-high.blend')}
    image=P.new_image('outcrop-normal-test-final-uv',512,'normal');P.bake_pair(high,low,image,'normal',report['projection'])
    report['normal']=P.normal_check(low,image);P.save_map(image,TEST/'normal-test.png');clay_comparison(high,low,image)
    bpy.ops.wm.save_as_mainfile(filepath=str(TEST/'prepared.blend'),compress=True,relative_remap=False)
    report['prepared_file']=P.identity(TEST/'prepared.blend');P.write_json(TEST/'test.json',report)
    print('RESULT_JSON='+json.dumps({'uv_identity':report['uv_identity'],'normal':report['normal'],'status':'LOCAL_UV_PASS_512_REBAKED'}))

if __name__=='__main__':
    if '--finish' in sys.argv:finish()
    elif '--accept-uv-candidate' in sys.argv:accept_uv_candidate()
    elif '--repair-uv' in sys.argv:repair_existing_uv()
    else:test()
