"""Measured bake test; --finish builds maps only after inspected test evidence."""
import bpy,bmesh,json,sys,hashlib
import numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P
import complete_surface as C
import input_identity as I
def option(name,default=None):
    return sys.argv[sys.argv.index(name)+1] if name in sys.argv else default

TEST=Path(option('--work-dir',HERE/'stages/10-bake-test')).resolve();TEST.mkdir(parents=True,exist_ok=True)

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
    assert vertex_hash=='f94a58f29ba6d83f32261321e5b740db6f3fcd428a25f39611a4f8736b2b0d8c','Frozen low geometry changed; locate the repair regions again'
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
    high_source=Path(option('--high-source',HERE/'stages/09-exposed-bed-material/source-high.blend')).resolve()
    bpy.ops.wm.open_mainfile(filepath=str(high_source))
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
    report['approved_geometry_source']={'path':str(high_source.relative_to(B.ROOT)),**P.identity(high_source)}
    P.write_json(TEST/'pre-bake.json',report)
    assert validation['status']=='PASS'
    image=P.new_image('outcrop-normal-test',512,'normal');P.bake_pair(high,low,image,'normal',proj)
    report['normal']=P.normal_check(low,image);P.save_map(image,TEST/'normal-test.png')
    clay_comparison(high,low,image)
    report['uv_identity']=uv_identity(low)
    bpy.ops.wm.save_as_mainfile(filepath=str(TEST/'prepared.blend'),compress=True,relative_remap=False)
    report['prepared_file']=P.identity(TEST/'prepared.blend');P.write_json(TEST/'test.json',report)
    print('RESULT_JSON='+json.dumps(report),flush=True)

def finish(output_dir=None,prepare_only=False):
    assert output_dir,'Pass --output-dir to build into a separate directory'
    output=Path(output_dir).resolve()
    assert output!=HERE,'The approved delivery is frozen; use a separate --output-dir'
    output.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'))
    high=bpy.data.objects['HIGH_ParentSandstone'];low=bpy.data.objects['MarsOutcrop']
    baseline_report=json.loads((TEST/'test.json').read_text())
    assert baseline_report['uv_validation']['status']=='PASS' and baseline_report['uv_validation']['known_substantive_folds_resolved']
    assert baseline_report['prepared_file']==P.identity(TEST/'prepared.blend'),'Prepared bake input changed after validation'
    assert baseline_report['uv_identity']==uv_identity(low),'Validated baseline UV/geometry identity changed'
    P.geologic_finish(high)
    surface=C.prepare_complete_face(high,low)
    uv=P.U.uv_qa(low,tex_res=4096,analysis_res=2048,gap_px=16);validation=uv_gate(low,uv)
    measured=P.projection(high,low);old=baseline_report['projection']
    projection={**measured,'cage_extrusion':max(old['cage_extrusion'],measured['cage_extrusion']),
        'max_ray_distance':max(old['max_ray_distance'],measured['max_ray_distance'])}
    assert measured['miss_pct']==measured['uncovered_pct']==0
    report={'version':bpy.app.version_string,'stage':'COMPLETE_FACE_PREPARED_FROM_PUBLIC_BASELINE',
        'baseline':{'geometry_source':baseline_report['approved_geometry_source'],'uv_identity':baseline_report['uv_identity']},
        'high_triangles':P.triangles(high),'game_triangles':P.triangles(low),'uv':uv,'uv_validation':validation,'uv_identity':uv_identity(low),
        'projection':projection,'projection_measured':measured,'baseline_projection':old,'complete_surface':surface,
        'material_source':P.identity(HERE/'pipeline.py'),'surface_source':P.identity(HERE/'surface_stage17.py'),
        'surface_sampling':'Approved full actual cut face and rear wall, continuous 2–8 cm shallow relief and low-contrast intrinsic minerals, with the original local bed lenses. No photograph pixels, lighting or AO inputs.',
        'inputs':I.inputs(high,low)}
    P.portable_metadata();P.write_json(output/'prepared-inputs.json',report)
    if prepare_only:
        bpy.ops.wm.save_as_mainfile(filepath=str(output/'surface-prepared.blend'),compress=True,relative_remap=False)
        print('PREPARATION_JSON='+json.dumps({'status':'PREPARED','output':str(output.relative_to(B.ROOT)),'high_triangles':report['high_triangles'],'game_triangles':report['game_triangles'],'preservation':surface['preservation']}),flush=True)
        return report
    textures=output/'textures';textures.mkdir(exist_ok=True);maps={}
    linear=np.array([.220,.195,.171]);encoded=np.where(linear<=.0031308,12.92*linear,1.055*np.power(linear,1/2.4)-.055)
    fills={'normal':(.5,.5,1,1),'base_color':(*encoded,1),'roughness':(.85,.85,.85,1)}
    for role in ('normal','base_color','roughness'):
        image=P.new_image('outcrop-'+role,4096,role)
        image.pixels.foreach_set(np.tile(np.array(fills[role],np.float32),(4096*4096,1)).ravel());image.update()
        P.bake_pair(high,low,image,role,projection,clear=False);P.save_map(image,textures/(role+'.png'));maps[role]=image
    report['final_normal']=P.normal_check(low,maps['normal']);assert not report['final_normal']['problems']
    report['material']=P.material(low,maps);assert not report['material']
    report['texture_precision']='4096x4096 RGB PNG, 8 bits per channel';report['bake_padding_px']=8
    high.hide_render=True;high.hide_set(True);low.hide_render=False;low.hide_set(False)
    C.S.STAGE=output/'renders';C.S.STAGE.mkdir(exist_ok=True);C.S.fixed_renders('baked-')
    for o in (high,low):
        for attribute in list(o.data.attributes):
            if attribute.name.startswith('stage17_'):o.data.attributes.remove(attribute)
    for material in list(bpy.data.materials):
        if material.users==0:bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image not in maps.values() and image.users==0:bpy.data.images.remove(image)
    for role,image in maps.items():image.filepath='//textures/'+role+'.png'
    P.export([low],output/'mars-outcrop-hifi-r2.glb');bpy.context.scene.render.filepath='//renders/baked-rear.png'
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'mars-outcrop-hifi-r2.blend'),compress=True,relative_remap=False)
    report.update(stage='BUILT_FROM_PUBLIC_RECIPE_PENDING_OUTPUT_VERIFICATION',dimensions=list(low.dimensions),
        files={name:P.identity(output/name) for name in ('mars-outcrop-hifi-r2.blend','mars-outcrop-hifi-r2.glb')})
    assert all(f['bytes']<104857600 for f in report['files'].values())
    P.write_json(output/'manifest.json',report);print('RESULT_JSON='+json.dumps({k:report[k] for k in ('stage','high_triangles','game_triangles','files')}),flush=True)
    return report

if __name__=='__main__':
    if '--finish' in sys.argv:finish(option('--output-dir'),prepare_only='--prepare-only' in sys.argv)
    else:test()
