"""Measured bake test; --finish builds maps only after inspected test evidence."""
import bpy,bmesh,json,sys
from pathlib import Path
HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build as B;import pipeline as P
TEST=HERE/'stages/10-bake-test';TEST.mkdir(parents=True,exist_ok=True)

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
    uv=P.unwrap(low);P.surface(high)
    proj=P.projection(high,low)
    report={'version':bpy.app.version_string,'stage':'10-real-selected-to-active-test','high_triangles':P.triangles(high),
            'game_triangles':P.triangles(low),'uv':uv,'projection':proj,'normal_source':'Independent approved high geometry only; no image-derived or shader normal in this geometry probe'}
    report['low_mesh_cleanup']={'merge_and_degenerate_edge_tolerance_m':.0002,'triangles_before':before,'triangles_after':P.triangles(low),'approved_high_unchanged':True}
    P.write_json(TEST/'pre-bake.json',report)
    assert uv['flipped_faces']==0 and uv['zero_area_faces']==0,uv
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

if '--finish' in sys.argv:finish()
else:test()
