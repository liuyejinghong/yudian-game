"""Ground high/low per-source baking; 512 probe precedes inspected 4K finish."""
import bpy,json,sys,importlib.util
import numpy as np
from mathutils import Matrix
from pathlib import Path
HERE=Path(__file__).resolve().parent;OUT=HERE.parent/'mars-outcrop-hifi-r2'
sys.path.insert(0,str(OUT));import build as B;import pipeline as P
spec=importlib.util.spec_from_file_location('ground_builder',HERE/'build.py');G=importlib.util.module_from_spec(spec);spec.loader.exec_module(G)
TEST=HERE/'stages/05-bake-test';TEST.mkdir(parents=True,exist_ok=True)

def placement(o):
    p=np.array(o['placement_matrix']);mat=np.eye(4);mat[:3,:3]=p[:9].reshape(3,3)*p[9];mat[:3,3]=p[10:13]
    return Matrix(mat.tolist())

def visibility(objects):
    chosen=set(objects)
    for o in bpy.context.scene.objects:
        if o.type=='MESH':o.hide_render=o not in chosen

def comparison(highs,lows,normal_maps):
    orig={o:o.data.materials[0] for o in highs+lows};clay=B.clay_material()
    for o in highs+lows:o.data.materials[0]=clay
    view={'detail':((-.2,-6,1.55),(-1,-2.15,.07),58)}
    visibility(highs);B.renders(TEST,prefix='high-',views=view)
    visibility(lows);B.renders(TEST,prefix='bare-low-',views=view)
    mats={}
    for kind,image in normal_maps.items():
        mat=bpy.data.materials.new('REVIEW_'+kind);mat.use_nodes=True
        P.M.pbr_from_textures(mat,{'normal':image},uv_map='UVMap',ao_mode='none')
        bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bs.inputs['Base Color'].default_value=(.32,.32,.32,1);bs.inputs['Roughness'].default_value=.87;mats[kind]=mat
    for o in lows:o.data.materials[0]=mats['sand' if o.name=='MarsSandApron' else 'clast']
    B.renders(TEST,prefix='mapped-low-',views=view)
    for o,mat in orig.items():o.data.materials[0]=mat

def test():
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'stages/05-boundary-calibrated/ground-high.blend'))
    highcoll=bpy.data.collections['SOURCE_HIGH_EDITABLE'];game=B.collection('GAME_EXPORT');proto=B.collection('GAME_PROTOTYPES')
    highterrain=bpy.data.objects['HIGH_SandApron'];lowterrain=G.ground_mesh(game,step=.08);lowterrain.name='MarsSandApron'
    P.select(lowterrain);P.U.prepare_low(lowterrain,triangulate=True)
    for md in list(lowterrain.modifiers):bpy.ops.object.modifier_apply(modifier=md.name)
    P.terrain_uv(lowterrain);P.surface(highterrain,kind='sand')
    pairs=[(highterrain,lowterrain,'sand')];ltemplates={};htemplates={}
    for i in range(12):
        high=bpy.data.objects['HIGH_Clast_%02d'%i];P.surface(high)
        low=P.clone_low(high,proto,'LOW_Clast_%02d'%i,.20);P.unwrap(low,cell=i)
        pairs.append((high,low,'clast'));ltemplates[i]=low;htemplates[i]=high
    for i in range(4):
        high=bpy.data.objects['HIGH_Subcrop_%02d'%i];P.surface(high)
        low=P.clone_low(high,game,'MarsSubcrop_%02d'%i,.25);P.unwrap(low,cell=12+i)
        pairs.append((high,low,'clast'))
    for high in list(highcoll.objects):
        if high.name.startswith('CLAST_'):
            i=int(high['template']);mat=placement(high)
            # Shared editable prototypes reproduce the recorded placed high geometry.
            high.data=htemplates[i].data;high.matrix_world=mat
            low=bpy.data.objects.new(high.name.replace('CLAST_','MarsClast_'),ltemplates[i].data);game.objects.link(low);low.matrix_world=mat
    report={'version':bpy.app.version_string,'stage':'05-per-source-selected-to-active-test','pairs':[],
        'atlas_contract':'12 unique fragment sources and 4 subcrops occupy separate atlas cells; placed copies intentionally reuse source UVs',
        'game_triangles':sum(P.triangles(o) for o in game.objects),'source_triangles_instances_counted':sum(P.triangles(o) for o in highcoll.objects)}
    images={k:P.new_image('ground-'+k+'-normal-test',512,'normal') for k in ('sand','clast')};first={'sand':True,'clast':True}
    for high,low,kind in pairs:
        proj=P.projection(high,low);im=images[kind]
        P.bake_pair(high,low,im,'normal',proj,clear=first[kind]);first[kind]=False
        result={'high':high.name,'low':low.name,'kind':kind,'projection':proj,'high_triangles':P.triangles(high),'low_triangles':P.triangles(low),
            'uv':P.U.uv_qa(low,tex_res=4096,analysis_res=512,gap_px=12),'normal':P.normal_check(low,im)}
        report['pairs'].append(result);P.write_json(TEST/'test-progress.json',report)
    for kind,im in images.items():P.save_map(im,TEST/(kind+'-normal-test.png'))
    # Normals are per-source and source material slots stay separate until final wiring.
    for low in proto.objects:low.hide_render=True;low.hide_set(True)
    comparison(list(highcoll.objects),list(game.objects),images)
    visibility(list(game.objects));P.write_json(TEST/'test.json',report)
    bpy.ops.wm.save_as_mainfile(filepath=str(TEST/'prepared.blend'),compress=True)
    print('RESULT_JSON='+json.dumps({'stage':report['stage'],'pairs':len(pairs),'game_triangles':report['game_triangles']}),flush=True)

def finish():
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'));report=json.loads((TEST/'test.json').read_text())
    game=bpy.data.collections['GAME_EXPORT'];maps={k:{} for k in ('sand','clast')};textures=HERE/'textures';textures.mkdir(exist_ok=True)
    for pair in report['pairs']:P.microfinish(bpy.data.objects[pair['high']],'sand' if pair['kind']=='sand' else 'rock')
    report['surface_sampling']='Base color and dust/parent masks are vertex-interpolated source fields; final normal also contains explicitly separate metric shader grain, after the geometry-only 512 probe.'
    for role in ('normal','base_color','roughness'):
        first={'sand':True,'clast':True}
        for kind in maps:maps[kind][role]=P.new_image('ground-'+kind+'-'+role,4096,role)
        for pair in report['pairs']:
            high=bpy.data.objects[pair['high']];low=bpy.data.objects[pair['low']];kind=pair['kind']
            P.bake_pair(high,low,maps[kind][role],role,pair['projection'],clear=first[kind]);first[kind]=False
        for kind in maps:P.save_map(maps[kind][role],textures/(kind+'-'+role+'.png'))
    final_mats={}
    for kind in maps:
        mat=bpy.data.materials.new('Mars_'+kind+'_PBR');mat.use_nodes=True
        P.M.pbr_from_textures(mat,maps[kind],uv_map='UVMap',ao_mode='none');final_mats[kind]=mat
    for ob in game.objects:
        mat=final_mats['sand' if ob.name=='MarsSandApron' else 'clast']
        ob.data.materials.clear();ob.data.materials.append(mat)
    report['final_normal']={}
    for pair in report['pairs']:
        low=bpy.data.objects[pair['low']];report['final_normal'][low.name]=P.normal_check(low,maps[pair['kind']]['normal'])
    visibility(list(game.objects))
    B.renders(HERE/'renders',views={'front':((11,-13,8),(0,0,.05),48),'reverse':((-11,12,6),(0,0,.05),48),'detail':((-.2,-6,1.55),(-1,-2.15,.07),58)})
    P.export(list(game.objects),HERE/'mars-ground-patch-hifi-r2.glb')
    for kind in maps:
        for im in maps[kind].values():im.filepath='//textures/'+im.filepath.rsplit('/',1)[-1]
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'mars-ground-patch-hifi-r2.blend'),compress=True)
    report.update(stage='FINAL_CANDIDATE_PENDING_NATIVE_REOPEN_AND_GLB_IMPORT',files={name:P.identity(HERE/name) for name in ['mars-ground-patch-hifi-r2.blend','mars-ground-patch-hifi-r2.glb']})
    P.write_json(HERE/'manifest.json',report);print('RESULT_JSON='+json.dumps({'stage':report['stage'],'game_triangles':report['game_triangles']}),flush=True)

if '--review-only' in sys.argv:
    bpy.ops.wm.open_mainfile(filepath=str(TEST/'prepared.blend'))
    images={k:bpy.data.images['ground-'+k+'-normal-test'] for k in ('sand','clast')}
    for kind,im in images.items():P.save_map(im,TEST/(kind+'-normal-test.png'))
    comparison(list(bpy.data.collections['SOURCE_HIGH_EDITABLE'].objects),list(bpy.data.collections['GAME_EXPORT'].objects),images)
    record={}
    for name in ('LOW_Clast_06','MarsSubcrop_01','MarsSubcrop_03'):
        record[name]=P.U.map_problem_review(bpy.data.objects[name],str(TEST/'clast-normal-test.png'),str(TEST/('problems-'+name)),kind='NORMAL',views=('threequarter','low'),res=640)
    P.write_json(TEST/'problem-review.json',json.loads(json.dumps(record).replace(str(HERE)+'/','')))
elif '--finish' in sys.argv:finish()
else:test()
