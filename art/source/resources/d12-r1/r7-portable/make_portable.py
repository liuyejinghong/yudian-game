"""Localize linked node assets, keeping the editable modifier stack intact."""
from pathlib import Path
import hashlib,json,sys
import bpy

ROOT=Path(__file__).resolve().parents[5]
SRC=ROOT/'art/source/resources/d12-r1/r7-portable'
AUTHOR=ROOT/'docs/art/production/d12-r1/resources/portable-r7'
IDS=['iron_ore','iron','copper_ore','copper','parts','cable']
sys.dont_write_bytecode=True


def encode(value):
    if value is None or isinstance(value,(str,int,float,bool)):return value
    if isinstance(value,bpy.types.ID):return {'id_type':value.bl_rna.identifier,'name':value.name}
    if hasattr(value,'to_dict'):return encode(value.to_dict())
    if hasattr(value,'items'):return {str(k):encode(v) for k,v in value.items()}
    return [encode(v) for v in value]


def custom(owner):
    # Bevel/normal modifiers expose no IDProperties; Nodes modifiers do.
    try:items=owner.items()
    except TypeError as exc:
        if "doesn't support IDProperties" not in str(exc):raise
        return {}
    return encode(dict(items))


def properties(owner,skip=()):
    out={}
    for prop in owner.bl_rna.properties:
        key=prop.identifier
        if key in set(skip)|{'rna_type','id_data'} or prop.type=='COLLECTION':continue
        if prop.type not in {'BOOLEAN','INT','FLOAT','STRING','ENUM','POINTER'}:continue
        value=getattr(owner,key)
        if prop.type=='POINTER' and value is not None and not isinstance(value,bpy.types.ID):continue
        out[key]=encode(value)
    return out


def node_tree(tree):
    if tree is None:return None
    result={'type':tree.bl_idname,'custom':encode(dict(tree.items())),
            'nodes':[],'links':[], 'interface':[]}
    for node in tree.nodes:
        item={'name':node.name,'type':node.bl_idname,'properties':properties(node),
              'inputs':[],'outputs':[]}
        for direction in ['inputs','outputs']:
            for socket in getattr(node,direction):
                item[direction].append({'id':socket.identifier,'type':socket.bl_idname,
                                        'properties':properties(socket,('node',))})
        result['nodes'].append(item)
    for link in tree.links:
        result['links'].append([link.from_node.name,link.from_socket.identifier,link.to_node.name,link.to_socket.identifier])
    for item in tree.interface.items_tree:
        result['interface'].append(properties(item,('parent',)))
    return result


def mesh_data(me):
    me.calc_loop_triangles()
    return {'positions':[list(v.co) for v in me.vertices],
            'vertex_normals':[list(v.normal) for v in me.vertices],
            'edges':[list(e.vertices) for e in me.edges],
            'loops':[loop.vertex_index for loop in me.loops],
            'corner_normals':[list(n.vector) for n in me.corner_normals],
            'polygons':[[list(p.vertices),p.material_index,p.use_smooth] for p in me.polygons],
            'triangles':[[list(t.vertices),list(t.loops),t.material_index,t.polygon_index] for t in me.loop_triangles],
            'materials':[mat.name if mat else None for mat in me.materials],
            'attributes':{a.name:{'type':a.data_type,'domain':a.domain,
                'values':[properties(d) for d in a.data]} for a in me.attributes}}


def snapshot():
    bpy.context.view_layer.update()
    result={'objects':{},'materials':{},'node_groups':{},
            'scene_units':properties(bpy.context.scene.unit_settings),
            'scene_custom':encode(dict(bpy.context.scene.items()))}
    deps=bpy.context.evaluated_depsgraph_get()
    for name in IDS:
        if name not in bpy.data.objects:continue
        ob=bpy.data.objects[name];evaluated=ob.evaluated_get(deps);me=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
        result['objects'][name]={'custom':encode(dict(ob.items())),
                'transform':encode(ob.matrix_world),'object_properties':properties(ob,('original','active_material','data','parent','users_collection')),
                'base_mesh':mesh_data(ob.data),'evaluated_mesh':mesh_data(me),
                'modifiers':[{'properties':properties(mod),'custom':custom(mod)} for mod in ob.modifiers]}
        evaluated.to_mesh_clear()
    for material in bpy.data.materials:
        result['materials'][material.name]={'properties':properties(material,('original','node_tree','library','library_weak_reference','override_library')),
                'custom':encode(dict(material.items())),'nodes':node_tree(material.node_tree)}
    for group in bpy.data.node_groups:
        result['node_groups'][group.name]=node_tree(group)
    return result


def localize_datablocks():
    changes=[]
    # Recursively loaded node groups may be indirect links; remap every live user.
    linked=[id for id in bpy.data.user_map() if id.library and not isinstance(id,bpy.types.Library)]
    for id in linked:
        old_name=id.name;local=id.make_local(clear_asset_data=False)
        if local!=id:id.user_remap(local)
        assert local.library is None,old_name
        changes.append({'type':local.bl_rna.identifier,'name':old_name,'local_name':local.name})
    remaining=[id for id in bpy.data.user_map() if id.library and not isinstance(id,bpy.types.Library)]
    assert not remaining,[id.name for id in remaining]
    while bpy.data.libraries:bpy.data.libraries.remove(bpy.data.libraries[0])
    assert not bpy.data.libraries
    for ob in bpy.data.objects:
        for modifier in ob.modifiers:
            if modifier.type=='NODES':assert modifier.node_group and modifier.node_group.library is None
    return changes


def main():
    report={'blender':bpy.app.version_string,'sources':{}}
    sources=[(ROOT/'art/source/resources/d12-r1/resource-pair-grey-r5.blend',ROOT/'art/source/resources/d12-r1/resource-pair-grey-r5-portable.blend'),
             (ROOT/'art/source/resources/d12-r1/r7/resources-grey-r7.blend',SRC/'resources-grey-r7.blend')]
    for original,destination in sources:
        assert not destination.exists(),destination
        original_sha=hashlib.sha256(original.read_bytes()).hexdigest()
        bpy.ops.wm.open_mainfile(filepath=str(original))
        libraries={lib.name:lib.filepath for lib in bpy.data.libraries}
        before=snapshot();changes=localize_datablocks();after=snapshot()
        (AUTHOR/(destination.stem+'-before.json')).write_text(json.dumps(before,sort_keys=True,indent=2)+'\n')
        (AUTHOR/(destination.stem+'-after.json')).write_text(json.dumps(after,sort_keys=True,indent=2)+'\n')
        assert before==after,'localization changed geometry/normals/indices/material/transform/metadata/node semantics'
        bpy.ops.wm.save_as_mainfile(filepath=str(destination))
        assert hashlib.sha256(original.read_bytes()).hexdigest()==original_sha
        report['sources'][str(destination.relative_to(ROOT))]={'original_sha256':original_sha,
            'portable_sha256':hashlib.sha256(destination.read_bytes()).hexdigest(),'libraries_before':libraries,
            'libraries_after':{},'localized':changes,'exact_snapshot_equal':True,
            'snapshot_sha256':hashlib.sha256(json.dumps(before,sort_keys=True).encode()).hexdigest()}
    (AUTHOR/'make-local.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PORTABLE_LOCALIZE_OK '+json.dumps(report))


if __name__=='__main__':main()
