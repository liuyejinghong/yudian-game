"""Fresh-process source reopen or empty-scene GLB import plus shape review.

Usage after Blender --python: -- source|glb
"""
from pathlib import Path
import hashlib
import json
import math
import struct
import sys
import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[5]
SRC = ROOT / 'art/source/resources/d12-r1/r7-portable'
OUT = ROOT / 'art/exports/resources/d12-r1/r7'
AUTHOR = ROOT / 'docs/art/production/d12-r1/resources/portable-r7/output'
sys.path.append(str(ROOT / '.agents/skills/scenario-blender-hard-surface/scripts'))
import bx_hardsurface as HS
import bx_review
import bx_audit
sys.path.insert(0,str(SRC))
from make_portable import snapshot
from prove_portable import semantic, external_files


def read_glb(path):
    raw = path.read_bytes()
    magic, version, size = struct.unpack_from('<III', raw)
    assert (magic,version,size) == (0x46546c67,2,len(raw))
    jslen, jstype = struct.unpack_from('<II',raw,12)
    assert jstype == 0x4e4f534a
    doc = json.loads(raw[20:20+jslen])
    assert '/Users/' not in json.dumps(doc) and '/private/' not in json.dumps(doc)
    assert len(doc['meshes']) == 1 and not doc.get('animations')
    for node in doc['nodes']:
        assert node.get('translation',[0,0,0]) == [0,0,0]
        assert node.get('rotation',[0,0,0,1]) == [0,0,0,1]
        assert node.get('scale',[1,1,1]) == [1,1,1]
        assert 'matrix' not in node
    binstart = 20 + jslen + 8
    primitive = doc['meshes'][0]['primitives'][0]
    assert primitive.get('mode',4) == 4
    def vectors(key):
        acc = doc['accessors'][primitive['attributes'][key]]
        assert acc['componentType'] == 5126 and acc['type'] == 'VEC3'
        view = doc['bufferViews'][acc['bufferView']]
        start = binstart + view.get('byteOffset',0) + acc.get('byteOffset',0)
        return [struct.unpack_from('<3f', raw, start+i*view.get('byteStride',12)) for i in range(acc['count'])]
    pos = vectors('POSITION')
    normals = vectors('NORMAL')
    assert all(math.isfinite(x) for p in pos+normals for x in p)
    assert all(abs(sum(x*x for x in n)-1) < 1e-4 for n in normals)
    lo = [min(p[i] for p in pos) for i in range(3)]
    hi = [max(p[i] for p in pos) for i in range(3)]
    assert abs(lo[1]) < 1e-6
    assert all(hi[i]-lo[i] <= lim for i,lim in enumerate((.55,.4,.55)))
    return {'sha256':hashlib.sha256(raw).hexdigest(), 'bounds_y_up': [lo,hi],
            'triangles':doc['accessors'][primitive['indices']]['count']//3,
            'material':doc['materials'][0]['pbrMetallicRoughness'],
            'private_paths_in_json':False}


def mesh_audit(ob, canonical_y_up):
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    me.calc_loop_triangles()
    points = [ob.matrix_world @ v.co for v in me.vertices]
    if not canonical_y_up:
        points = [Vector((p.x,p.z,-p.y)) for p in points]
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    bottom_area = 0
    bottom_centroid = Vector((0,0,0))
    for tri in me.loop_triangles:
        a,b,c = [points[i] for i in tri.vertices]
        if all(abs(p.y-lo[1]) < 1e-7 for p in (a,b,c)):
            area = (b-a).cross(c-a).length / 2
            bottom_area += area
            bottom_centroid += (a+b+c) / 3 * area
    if bottom_area > 0:
        bottom_centroid /= bottom_area
    else:
        bottom_centroid=Vector(((lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2))
    bm = bmesh.new()
    bm.from_mesh(me)
    # glTF splits vertices at normal seams; weld only this audit copy.
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bm.normal_update()
    report = {'bounds_y_up':[lo,hi], 'triangles':len(me.loop_triangles),
              'non_manifold_edges':sum(not e.is_manifold for e in bm.edges),
              'zero_area_faces':sum(f.calc_area() < 1e-12 for f in bm.faces),
              'bottom_contact_area_centroid_y_up':list(bottom_centroid) if bottom_area else None,
              'bottom_bbox_center_y_up':[(lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2],
              'signed_volume_m3':bm.calc_volume(signed=True)}
    bm.free()
    ev.to_mesh_clear()
    assert report['non_manifold_edges'] == 0 and report['zero_area_faces'] == 0, report
    assert report['signed_volume_m3'] > 0, report
    assert abs(lo[1]) < 1e-6, report
    if ob.name in ('iron_ore','iron'):
        assert max(abs(bottom_centroid[i]) for i in (0,2)) < .005,report
    else:
        assert max(abs(report['bottom_bbox_center_y_up'][i]) for i in (0,2))<1e-6,report
    return report


def diagnose_clamp():
    from mathutils.kdtree import KDTree
    bpy.ops.wm.open_mainfile(filepath=str(SRC / 'resources-grey-r7.blend'))
    assert len(bpy.data.libraries)==0
    reports={}
    rot=Matrix.Rotation(math.pi/2,4,'X')
    for name in ('copper_ore','copper','parts','cable'):
        ob=bpy.data.objects[name]
        bevel=next((mod for mod in ob.modifiers if mod.type=='BEVEL'),None)
        report={'base_min_edge_m':min((ob.data.vertices[e.vertices[0]].co-ob.data.vertices[e.vertices[1]].co).length
                                      for e in ob.data.edges), 'bevel_applicable':bevel is not None}
        report['self_intersecting_face_pairs']=bx_audit.audit(ob,evaluated=True)['self_intersections']
        assert report['self_intersecting_face_pairs']==0,report
        if bevel:
            def coordinates():
                ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
                me=ev.to_mesh()
                points=[vertex.co.copy() for vertex in me.vertices]
                ev.to_mesh_clear()
                return points
            clamped=coordinates()
            bevel.use_clamp_overlap=False
            unclamped=coordinates()
            bevel.use_clamp_overlap=True
            kd=KDTree(len(unclamped))
            for i,point in enumerate(unclamped):kd.insert(point,i)
            kd.balance()
            report.update(bevel_width_m=bevel.width,
                          same_index_max_displacement_m=max((a-b).length for a,b in zip(clamped,unclamped)),
                          nearest_vertex_max_displacement_m=max(kd.find(point)[2] for point in clamped))
        if name=='cable':
            cap_start=len(ob.data.polygons)-2
            centers=[sum((ob.data.vertices[i+j].co for j in range(8)),Vector())/8
                     for i in range(0,len(ob.data.vertices),8)]
            side_errors=[]
            cap_errors=[]
            for poly in ob.data.polygons:
                for loop in poly.loop_indices:
                    vertex=ob.data.loops[loop].vertex_index
                    expected=poly.normal if poly.index>=cap_start else (ob.data.vertices[vertex].co-centers[vertex//8]).normalized()
                    actual=ob.data.corner_normals[loop].vector.normalized()
                    error=math.degrees(math.acos(max(-1,min(1,actual.dot(expected)))))
                    (cap_errors if poly.index>=cap_start else side_errors).append(error)
            report.update(radial_side_normal_max_error_deg=max(side_errors),
                          planar_terminal_normal_max_error_deg=max(cap_errors))
            assert max(side_errors+cap_errors)<.1,report
        reports[name]=report
        ob.matrix_world=rot@ob.matrix_world
        bpy.context.view_layer.update()
        if name=='copper_ore':
            for i,point in enumerate(((.2149,.1301,.1395),(-.1241,.12,.1897))):
                HS.closeup(ob,rot@Vector(point),.03,str(AUTHOR/('copper-ore-hard-edge-'+str(i)+'.png')),
                           direction=(.6,1,.75),mode='hsgrey',res=640)
        if name=='parts':
            HS.closeup(ob,rot@Vector((.20,.17,.12)),.065,str(AUTHOR/'parts-corner-reflection.png'),
                       direction=(.6,1,.8),mode='reflect',res=640)
        if name=='cable':
            HS.closeup(ob,rot@Vector((.17,.026,.02)),.07,str(AUTHOR/'cable-terminal-near.png'),
                       direction=(.5,1,.7),mode='hsgrey',res=640)
    (AUTHOR/'geometry-diagnostic.json').write_text(json.dumps(reports,indent=2)+'\n')
    print('RESULT '+json.dumps(reports))


def main():
    mode = sys.argv[sys.argv.index('--')+1]
    if mode == 'diagnostics':
        diagnose_clamp()
        return
    assert mode in ('source','glb')
    report = {'mode':mode, 'blender':bpy.app.version_string, 'resources':{}}
    raw = {name:read_glb(OUT / (name+'.glb')) for name in ('iron_ore','iron','copper_ore','copper','parts','cable')}
    assert all(facts['material']==raw['iron']['material'] for facts in raw.values())
    for name in ('iron_ore','iron'):
        assert (OUT/(name+'.glb')).read_bytes()==(ROOT/'art/exports/resources/d12-r1/r5'/(name+'.glb')).read_bytes()
    if mode == 'source':
        bpy.ops.wm.open_mainfile(filepath=str(SRC / 'resources-grey-r7.blend'))
        assert len(bpy.data.libraries)==0
        frozen_snapshot=ROOT/'docs/art/production/d12-r1/resources/portable-r7/resources-grey-r7-before.json'
        assert semantic(snapshot())==semantic(json.loads(frozen_snapshot.read_text())), 'portable source differs from frozen semantic snapshot'
        report['frozen_snapshot_sha256']=hashlib.sha256(frozen_snapshot.read_bytes()).hexdigest()
        report['frozen_source_semantics_exact']=True
        report['portable_closure']=external_files()
        assert len(bpy.context.scene.objects) == 6
        objects = [bpy.data.objects[name] for name in raw]
    else:
        for ob in list(bpy.data.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        objects = []
        for name in raw:
            previous = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=str(OUT / (name+'.glb')))
            imported = [o for o in bpy.data.objects if o not in previous]
            assert len(imported) == 1 and imported[0].type == 'MESH'
            objects.append(imported[0])
    bpy.context.view_layer.update()
    for name,ob in zip(raw,objects):
        audit = mesh_audit(ob, mode == 'source')
        assert audit['triangles'] == raw[name]['triangles']
        assert max(abs(audit['bounds_y_up'][j][i]-raw[name]['bounds_y_up'][j][i])
                   for j in range(2) for i in range(3)) < 1e-6
        report['resources'][name] = {**raw[name], 'reopen_audit':audit}
    placements = [(-.19,.72,.10),(.19,.72,.10),(-.19,.72,.50),(.19,.72,.50)]
    scale=.65
    for name,facts in raw.items():
        lo,hi = facts['bounds_y_up']
        bounds = [[min(t[i]+lo[i]*scale for t in placements) for i in range(3)],
                  [max(t[i]+hi[i]*scale for t in placements) for i in range(3)]]
        assert all(bounds[0][i] >= limit for i,limit in enumerate((-.42,.72,-.1)))
        assert all(bounds[1][i] <= limit for i,limit in enumerate((.42,1.35,.7)))
        report['resources'][name]['four_visual_aggregates_bounds_y_up'] = bounds
    (AUTHOR / (mode+'-readback.json')).write_text(json.dumps(report,indent=2)+'\n')
    scene = bpy.context.scene
    scene.render.threads_mode='FIXED'
    scene.render.threads=4
    # Review only: conventional Blender Z-up; source geometry/transforms stay saved unchanged.
    if mode == 'source':
        rot = Matrix.Rotation(math.pi/2,4,'X')
        for ob in objects:
            ob.matrix_world = rot @ ob.matrix_world
    # Imported front -Z is now +Y in Blender; all labelled views use that same front.
    bx_review.VIEW_DIRS.update(front=Vector((0,1,0)), back=Vector((0,-1,0)),
                              threequarter=Vector((.7,1,.75)).normalized(),
                              low=Vector((.8,1,-.25)).normalized())
    sheets=[]
    for name,ob in zip(raw,objects):
        sheet=HS.review([ob],str(AUTHOR/(mode+'-'+name)),
                        views=('front','threequarter','back','low'),
                        modes=('silhouette','hsgrey'),res=480)
        sheets.append(str(Path(sheet).relative_to(ROOT)))
        HS.closeup(ob,(0,0,.12),.28,str(AUTHOR/(mode+'-'+name+'-near.png')),
                   direction=(.7,1,.7),mode='hsgrey',res=640)
    print('RESULT ' + json.dumps({'mode':mode,'sheets':sheets,
                                  'triangles':{k:v['triangles'] for k,v in raw.items()}}))


if __name__ == '__main__':
    main()
