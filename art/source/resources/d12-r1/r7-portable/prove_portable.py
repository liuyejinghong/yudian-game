"""Native fresh reopen, editable GN closure, exact snapshots and full GLB payload."""
from pathlib import Path
import hashlib,json,struct,sys
import bpy
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).parent))
from make_portable import ROOT,SRC,AUTHOR,IDS,snapshot


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def semantic(value):
    # ID.session_uid is process-local and is not persisted model metadata.
    if isinstance(value,dict):
        if 'objects' in value and 'materials' in value:
            bound={name for obj in value['objects'].values() for field in ['base_mesh','evaluated_mesh'] for name in obj[field]['materials'] if name}
            value={**value,'materials':{k:v for k,v in value['materials'].items() if k in bound}}
        return {k:semantic(v) for k,v in value.items() if k!='session_uid'}
    if isinstance(value,list):return [semantic(v) for v in value]
    return value


def external_files():
    assert not bpy.data.libraries
    assert not [id for id in bpy.data.user_map() if id.library]
    images=[]
    for image in bpy.data.images:
        assert not image.filepath or image.packed_file,image.name
        images.append({'name':image.name,'source':image.source,'filepath':image.filepath,'packed':bool(image.packed_file)})
    assert not bpy.data.movieclips and not bpy.data.sounds
    assert all(font.filepath=='<builtin>' or font.packed_file for font in bpy.data.fonts)
    modifiers={}
    for name in IDS:
        if name not in bpy.data.objects:continue
        modifiers[name]=[{'name':m.name,'group':m.node_group.name,'local':m.node_group.library is None}
                         for m in bpy.data.objects[name].modifiers if m.type=='NODES']
        assert all(m['local'] for m in modifiers[name])
    return {'libraries':0,'linked_ids':0,'images':images,'editable_geometry_node_modifiers':modifiers,
            'render_output':bpy.context.scene.render.filepath}


def visual_payload(path):
    raw=path.read_bytes();length=struct.unpack_from('<I',raw,12)[0]
    doc=json.loads(raw[20:20+length]);start=28+length
    formats={5120:('b',1),5121:('B',1),5122:('h',2),5123:('H',2),5125:('I',4),5126:('f',4)}
    widths={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
    def values(index):
        accessor=doc['accessors'][index];view=doc['bufferViews'][accessor['bufferView']]
        fmt,size=formats[accessor['componentType']];width=widths[accessor['type']]
        offset=start+view.get('byteOffset',0)+accessor.get('byteOffset',0)
        result=[list(struct.unpack_from('<'+fmt*width,raw,offset+j*view.get('byteStride',size*width))) for j in range(accessor['count'])]
        return {'component_type':accessor['componentType'],'type':accessor['type'],
                'normalized':accessor.get('normalized',False),'values':result}
    primitives=[]
    for mesh in doc['meshes']:
        for p in mesh['primitives']:
            primitives.append({'attributes':{name:values(index) for name,index in p['attributes'].items()},
                    'indices':values(p['indices']),'mode':p.get('mode',4),
                    'material':doc['materials'][p['material']]})
    return {'primitives':primitives,'nodes':doc['nodes'],'scenes':doc['scenes'],
            'textures':doc.get('textures',[]),'images':doc.get('images',[])}


def export_selected(directory):
    directory.mkdir(parents=True)
    results={}
    for name in IDS:
        if name not in bpy.data.objects:continue
        for ob in bpy.context.scene.objects:ob.select_set(ob.name==name)
        bpy.context.view_layer.objects.active=bpy.data.objects[name]
        path=directory/(name+'.glb')
        bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
                export_yup=False,export_apply=True,export_extras=True,export_animations=False,
                export_cameras=False,export_lights=False)
        results[name]=visual_payload(path)
    return results


def main():
    temporary=Path(sys.argv[sys.argv.index('--')+1])
    assert not temporary.exists(),temporary
    report={'blender':bpy.app.version_string,'exact_float_arrays':True,'sources':{}}
    pairs=[(ROOT/'art/source/resources/d12-r1/resource-pair-grey-r5.blend',ROOT/'art/source/resources/d12-r1/resource-pair-grey-r5-portable.blend'),
           (ROOT/'art/source/resources/d12-r1/r7/resources-grey-r7.blend',SRC/'resources-grey-r7.blend')]
    for original,portable in pairs:
        bpy.ops.wm.open_mainfile(filepath=str(portable))
        after=snapshot();baseline=json.loads((AUTHOR/(portable.stem+'-before.json')).read_text())
        (AUTHOR/(portable.stem+'-fresh-reopen.json')).write_text(json.dumps(after,indent=2,sort_keys=True)+'\n')
        assert semantic(after)==semantic(baseline),'native reopen semantic snapshot differs'
        closure=external_files()
        portable_exports=export_selected(temporary/portable.stem/'portable')
        bpy.ops.wm.open_mainfile(filepath=str(original))
        assert semantic(snapshot())==semantic(baseline),'original source identity changed'
        original_exports=export_selected(temporary/portable.stem/'original')
        assert original_exports==portable_exports,'full GLB attributes/index/material/transforms/extras differ'
        resource_checks={}
        for name,payload in portable_exports.items():
            frozen=ROOT/'art/exports/resources/d12-r1/r7'/(name+'.glb')
            reference=visual_payload(frozen)
            assert payload==reference,'new export differs from frozen full visual payload '+name
            primitive=payload['primitives'][0]
            points=primitive['attributes']['POSITION']['values']
            resource_checks[name]={'frozen_sha256':sha(frozen),'full_glb_visual_payload_exact':True,
                'triangles':len(primitive['indices']['values'])//3,
                'bounds':[[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]]}
        report['sources'][str(portable.relative_to(ROOT))]={'portable_sha256':sha(portable),
            'original_sha256':sha(original),'native_reopen_exact_semantic_snapshot':True,
            'excluded_process_only_field':'ID.session_uid','unused_materials_not_bound_to_geometry':[name for name in baseline['materials'] if name not in after['materials']], 'closure':closure,'resources':resource_checks}
    (AUTHOR/'native-proof.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PORTABLE_NATIVE_PROOF_OK '+json.dumps(report))


if __name__=='__main__':main()
