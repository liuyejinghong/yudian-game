"""Four new grey resource units; retain the accepted r5 pair without re-export."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import sys
import bpy
import bmesh
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[5]
SRC=ROOT/'art/source/resources/d12-r1/r7-portable'
OUT=ROOT/'art/exports/resources/d12-r1/r7'
AUTHOR=ROOT/'docs/art/production/d12-r1/resources/portable-r7/output'
sys.path.append(str(ROOT/'.agents/skills/scenario-blender-hard-surface/scripts'))
import bx_hardsurface as HS
import bx_audit
sys.path.insert(0,str(SRC))
from make_portable import localize_datablocks


def mesh(name,verts,faces):
    me=bpy.data.meshes.new(name)
    me.from_pydata(verts,[],faces)
    bm=bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.normal_update()
    for edge in bm.edges:
        edge.smooth=edge.calc_face_angle(0)<math.radians(35)
    bm.to_mesh(me)
    bm.free()
    assert not me.validate()
    me.update()
    ob=bpy.data.objects.new(name,me)
    bpy.context.scene.collection.objects.link(ob)
    ob['resource_id']=name
    ob['discrete_units']=1
    ob['candidate_revision']='d12-r1-grey-r7'
    for poly in me.polygons:
        poly.use_smooth=True
    return ob


def ore():
    # A low broken slab with a high right shoulder and an oblique main fracture.
    points=[(-.17,0,-.11),(.15,0,-.12),(.19,0,.05),(.045,0,.15),(-.17,0,.09),
            (-.23,.09,-.04),(-.13,.12,.19),(.21,.13,.14),(.12,.22,.06),
            (-.15,.07,-.19),(.22,.07,-.19),(.16,.20,-.02),(-.105,.15,-.075)]
    bm=bmesh.new()
    for point in points:
        bm.verts.new(point)
    hull=bmesh.ops.convex_hull(bm,input=list(bm.verts))
    unused=[v for v in hull['geom_unused'] if isinstance(v,bmesh.types.BMVert)]
    if unused:
        bmesh.ops.delete(bm,geom=unused,context='VERTS')
    bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges))
    bm.verts.index_update()
    verts=[tuple(v.co) for v in bm.verts]
    faces=[[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    ob=mesh('copper_ore',verts,faces)
    stack=HS.hard_surface_stack(ob,micro_width=.0007,micro_segments=3,angle=35,
                               smooth_by_angle=True)
    stack['micro'].limit_method='WEIGHT'
    edges=sorted((e for e in ob.data.edges
                  if min(ob.data.vertices[i].co.y for i in e.vertices)>.08),
                 key=lambda e:(ob.data.vertices[e.vertices[0]].co-ob.data.vertices[e.vertices[1]].co).length,
                 reverse=True)[:2]
    HS.set_edge_attr(ob,[e.index for e in edges],1)
    return ob


def copper():
    # Circular-section stock; two integral end chamfers, not a rectangular ingot.
    n=16
    profile=[(-.22,.064),(-.214,.070),(.214,.070),(.22,.064)]
    verts=[(x,.07+radius*math.cos(2*math.pi*j/n),radius*math.sin(2*math.pi*j/n))
           for x,radius in profile for j in range(n)]
    faces=[]
    for ring in range(len(profile)-1):
        for j in range(n):
            faces.append((ring*n+j,ring*n+(j+1)%n,(ring+1)*n+(j+1)%n,(ring+1)*n+j))
    faces += [tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+j for j in range(n))]
    ob=mesh('copper',verts,faces)
    # The profile already contains chamfers; the round wall keeps its smooth radial normals.
    with bpy.context.temp_override(object=ob,active_object=ob,selected_objects=[ob],selected_editable_objects=[ob]):
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35),keep_sharp_edges=False)
    return ob


def parts():
    # One standard U-section support. The open channel is structural, not a decorative cut.
    profile=[(-.13,0),(.13,0),(.13,.18),(.10,.18),(.10,.04),(-.10,.04),(-.10,.18),(-.13,.18)]
    verts=[(x,y,z) for x in (-.21,.21) for z,y in profile]
    n=len(profile)
    faces=[tuple(range(n-1,-1,-1)),tuple(n+i for i in range(n))]
    for j in range(n):
        faces.append((j,(j+1)%n,n+(j+1)%n,n+j))
    ob=mesh('parts',verts,faces)
    HS.hard_surface_stack(ob,micro_width=.0012,micro_segments=3,angle=35,
                          smooth_by_angle=True)
    return ob


def cable():
    # A continuous 1.5-turn cable with two short outward leads and chamfered terminals.
    count=28
    centers=[]
    for i in range(count+1):
        angle=3*math.pi*i/count
        radius=.153+.025*max(0,1-i/3)+.025*max(0,1-(count-i)/3)
        centers.append(Vector((radius*math.cos(angle),.021+.078*i/count,radius*math.sin(angle))))
    side=8
    verts=[]
    radials=[]
    for i,point in enumerate(centers):
        tangent=(centers[min(i+1,count)]-centers[max(i-1,0)]).normalized()
        horizontal=Vector((tangent.z,0,-tangent.x)).normalized()
        up=tangent.cross(horizontal).normalized()
        tube_radius=.027 if i in (0,count) else .024 if i in (1,count-1) else .020
        for j in range(side):
            angle=2*math.pi*j/side
            radial=(math.cos(angle)*up+math.sin(angle)*horizontal).normalized()
            verts.append(tuple(point+tube_radius*radial))
            radials.append(radial)
    faces=[]
    for i in range(count):
        for j in range(side):
            a,b,c,d=(i*side+j,i*side+(j+1)%side,(i+1)*side+(j+1)%side,(i+1)*side+j)
            faces.extend(((a,b,c),(a,c,d)))
    faces += [tuple(range(side-1,-1,-1)),tuple(count*side+j for j in range(side))]
    ob=mesh('cable',verts,faces)
    # Tube loops use radial normals; cap loops use their own planar normals.
    normals=[]
    for poly in ob.data.polygons:
        for loop in poly.loop_indices:
            vertex=ob.data.loops[loop].vertex_index
            normals.append(tuple(poly.normal if poly.index>=2*count*side else radials[vertex]))
    ob.data.normals_split_custom_set(normals)
    ob['normal_contract']='radial tube sides; separate planar terminal caps'
    return ob


def center_bottom(ob):
    ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me=ev.to_mesh()
    lo=[min(v.co[i] for v in me.vertices) for i in range(3)]
    hi=[max(v.co[i] for v in me.vertices) for i in range(3)]
    shift=Vector(((lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2))
    ev.to_mesh_clear()
    for vertex in ob.data.vertices:
        vertex.co-=shift
    ob.data.update()
    bpy.context.view_layer.update()


def main():
    assert bpy.app.version[:2]==(5,2)
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/source/resources/d12-r1/resource-pair-grey-r5-portable.blend'))
    scene=bpy.context.scene
    scene.render.threads_mode='FIXED'
    scene.render.threads=4
    material=bpy.data.objects['iron'].data.materials[0]
    old=[bpy.data.objects['iron_ore'],bpy.data.objects['iron']]
    new=[ore(),copper(),parts(),cable()]
    reports={}
    for ob in new:
        ob.data.materials.clear()
        ob.data.materials.append(material)
        center_bottom(ob)
        report=HS.shading_audit(ob)
        report['verdict']=HS.verdict(report)
        report['base_min_edge_m']=min((ob.data.vertices[e.vertices[0]].co-ob.data.vertices[e.vertices[1]].co).length
                                      for e in ob.data.edges)
        report['self_intersecting_face_pairs']=bx_audit.audit(ob,evaluated=True)['self_intersections']
        assert report['eval_non_manifold_edges']==0 and report['nonplanar_base_faces']==0,report
        assert report['self_intersecting_face_pairs']==0,report
        assert report['eval_tris']<=512,report
        reports[ob.name]=report
        for other in old+new:
            other.select_set(other==ob)
        bpy.context.view_layer.objects.active=ob
        bpy.ops.export_scene.gltf(filepath=str(OUT/(ob.name+'.glb')),export_format='GLB',
                                  use_selection=True,export_yup=False,export_apply=True,
                                  export_extras=True,export_animations=False,export_cameras=False,
                                  export_lights=False)
    for ob in old:
        shutil.copy2(ROOT/'art/exports/resources/d12-r1/r5'/(ob.name+'.glb'),OUT/(ob.name+'.glb'))
    (AUTHOR/'build-audit.json').write_text(json.dumps(reports,indent=2)+'\n')
    localize_datablocks()
    assert len(bpy.data.libraries)==0
    bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'resources-grey-r7.blend'))
    files=[SRC/'resources-grey-r7.blend']+list(OUT.glob('*.glb'))
    (AUTHOR/'delivery-sha256.json').write_text(json.dumps({str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest()
                                                        for f in files},indent=2)+'\n')
    print('RESULT '+json.dumps({'objects':list(reports),'verdicts':{k:v['verdict'] for k,v in reports.items()}}))


if __name__=='__main__':
    main()
