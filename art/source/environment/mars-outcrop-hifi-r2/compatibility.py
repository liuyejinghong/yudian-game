"""Small native toolkit probe; no production scene or old source is opened."""
import bpy, json, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
for s in ('expert', 'uv-baking', 'texturing-shading'):
    sys.path.append(str(ROOT / '.agents/skills' / ('scenario-blender-' + s) / 'scripts'))
import bx_audit as A, bx_review as R, bx_uvbake as U, bx_materials as M
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
sc = bpy.context.scene
sc.render.threads_mode = 'FIXED'; sc.render.threads = 6
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 16
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12)
low = bpy.context.object; low.name = 'COMPAT_Low'
U.prepare_low(low, triangulate=True)
high = low.copy(); high.data = low.data.copy(); sc.collection.objects.link(high)
high.name = 'COMPAT_High'
md = high.modifiers.new('source_subdivision', 'SUBSURF'); md.levels = md.render_levels = 2
tex = bpy.data.textures.new('only_compatibility_relief', type='CLOUDS'); tex.noise_scale = .18
md = high.modifiers.new('source_relief', 'DISPLACE'); md.texture = tex; md.strength = .06
proj = U.measure_projection(low, [high], samples=1200)
mat = bpy.data.materials.new('COMPAT_Material'); mat.use_nodes = True
low.data.materials.append(mat)
img = bpy.data.images.new('compat_normal', 128, 128, alpha=False, float_buffer=True)
img.colorspace_settings.name = 'Non-Color'
node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = img
mat.node_tree.nodes.active = node; node.select = True
bpy.ops.object.select_all(action='DESELECT'); high.select_set(True); low.select_set(True)
bpy.context.view_layer.objects.active = low
r = bpy.ops.object.bake(type='NORMAL', use_selected_to_active=True,
    cage_extrusion=proj['cage_extrusion'], max_ray_distance=proj['max_ray_distance'],
    margin=2, margin_type='EXTEND', use_clear=True, target='IMAGE_TEXTURES', normal_space='TANGENT')
assert r == {'FINISHED'}
img.filepath_raw = str(HERE/'verification/compat-normal.png'); img.file_format='PNG'; img.save()
M.pbr_from_textures(mat, {'normal': img}, uv_map=low.data.uv_layers.active.name, ao_mode='none')
report = {'version': bpy.app.version_string, 'version_tuple': list(bpy.app.version),
          'device':'CPU', 'threads':6, 'projection':proj, 'bake':list(r),
          'low_audit':A.audit(low), 'material_audit':M.audit_material(mat, low),
          'skill_functions':['bx_audit.audit','bx_review.review','bx_uvbake.prepare_low',
                             'bx_uvbake.measure_projection','bx_materials.pbr_from_textures',
                             'bx_materials.audit_material']}
high.hide_render = True
report['review'] = R.review([low], str(HERE/'verification/compat-review'), views=('threequarter',), modes=('matcap',), res=160)
report['status'] = 'PASS'
report = json.loads(json.dumps(report).replace(str(HERE) + '/', ''))
(HERE/'verification/compatibility.json').write_text(json.dumps(report, indent=2))
print('RESULT_JSON='+json.dumps({'status':'PASS','version':bpy.app.version_string,'report':'verification/compatibility.json'}))
