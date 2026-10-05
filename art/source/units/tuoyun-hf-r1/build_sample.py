"""U01 Blender geometry trial. Run with Blender --background --factory-startup.

Source U01 is read-only. This is an idle geometry trial, not the complete
high-fidelity model; animation conversion, UV/baking and optimization are pending.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import time

import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm"}
BEVEL_TARGETS = {"Body", "Head", "HoodLid", "CargoBox", "RearLock",
                 "ChargeCap", "ProtectedElectronics"}


def weld_and_bevel(obj, width, segments):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    modifier = obj.modifiers.new("Manufactured_edge_trial", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(35)
    modifier.use_clamp_overlap = True
    modifier.harden_normals = True


def world_position(x, y, z):
    # Blender's glTF importer maps +Y up / -Z forward to +Z up / +Y forward.
    return Vector((x, -z, y))


def panel(name, xyz, size, parent, role, radius=0.002):
    bpy.ops.mesh.primitive_cube_add(size=1, location=world_position(*xyz))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(bpy.data.materials[role])
    saved = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = saved
    weld_and_bevel(obj, radius, 3)
    return obj


def fastener(name, xyz, parent, radius=0.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=0.004,
                                        location=world_position(*xyz))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(bpy.data.materials["frame_dark"])
    saved = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = saved
    weld_and_bevel(obj, 0.0006, 2)



def formed_cover(parent):
    # Sloped shoulders make the cover's thickness readable without fine surface noise.
    outline = [(-.30,-.735),(.30,-.735),(.34,-.695),(.34,-.305),
               (.30,-.265),(-.30,-.265),(-.34,-.305),(-.34,-.695)]
    vertices = [world_position(x,.662,z) for x,z in outline]
    vertices += [world_position(x*.9265,.705,-.5+(z+.5)*.8936) for x,z in outline]
    faces = [tuple(reversed(range(8))),tuple(range(8,16))]
    faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh = bpy.data.meshes.new("Formed_service_cover")
    inverse = parent.matrix_world.inverted()
    mesh.from_pydata([inverse@v for v in vertices],[],faces)
    mesh.update()
    obj = bpy.data.objects.new("Formed_service_cover",mesh)
    bpy.context.collection.objects.link(obj);obj.parent=parent
    mesh.materials.append(bpy.data.materials["body_light"])
    weld_and_bevel(obj,.005,3)


def hub_cover(name, xyz, parent, radius, depth, role):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=depth,
        location=world_position(*xyz),rotation=(0,math.pi/2,0))
    obj=bpy.context.object;obj.name=name
    obj.data.materials.append(bpy.data.materials[role])
    saved=obj.matrix_world.copy();obj.parent=parent;obj.matrix_world=saved
    weld_and_bevel(obj,.0015,2)

def geometry_report():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    rows = []
    for obj in sorted(bpy.data.objects, key=lambda o: o.name):
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        points = [evaluated.matrix_world @ v.co for v in mesh.vertices]
        assert points and all(math.isfinite(c) for v in points for c in v), obj.name
        roles = sorted({m.name for m in mesh.materials if m})
        assert roles and set(roles) <= ROLES, (obj.name, roles)
        # Serialize bounds in exported glTF coordinates, not author Z-up.
        points = [(p.x, p.z, -p.y) for p in points]
        rows.append({"node": obj.name, "triangles": len(mesh.loop_triangles),
                     "roles": roles,
                     "bounds": {"min": [min(p[i] for p in points) for i in range(3)],
                                "max": [max(p[i] for p in points) for i in range(3)]}})
        evaluated.to_mesh_clear()
    assert len([r for r in rows if r["node"].startswith("Wheel_")]) == 6
    assert sum(r["triangles"] for r in rows) > 7312
    return rows


def export_glb(path):
    result = bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
        export_apply=True, export_yup=True, export_animations=False,
        export_materials="EXPORT", export_normals=True, export_texcoords=True,
        export_cameras=False, export_lights=False, export_extras=True)
    assert result == {"FINISHED"}, result


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path,
        default=ROOT / "prototype/assets/units/tuoyun-r1/tuoyun-r1.glb")
    parser.add_argument("--output", type=Path, default=HERE)
    opts = parser.parse_args(args)
    if not opts.source.is_file():
        raise FileNotFoundError("U01 source GLB missing: " + str(opts.source))
    assert bpy.app.version == (4, 5, 14), bpy.app.version_string
    started = time.monotonic()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(opts.source))
    bpy.context.scene.frame_set(0)
    for obj in list(bpy.data.objects):
        obj.animation_data_clear()
        if obj.name == "TowRod":
            bpy.data.objects.remove(obj, do_unlink=True)
    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.name in BEVEL_TARGETS:
            # Wheel bevels add many faces with little normal-camera benefit.
            weld_and_bevel(obj, .004, 3)
    for name in ["Body", "Head", "HoodLid", "ProtectedElectronics", "CargoBox"]:
        assert bpy.data.objects.get(name) is not None, "Missing " + name
    body = bpy.data.objects["Body"]
    hood = bpy.data.objects["HoodLid"]
    panel("Lid_gasket", (0,.656,-.50), (.65,.012,.45), hood,"frame_dark",.002)
    formed_cover(hood)
    panel("Lid_inset_plate", (0, .708, -.49), (.40, .008, .22), hood,
          "body_light", .003)
    for i, (x, z) in enumerate([(-.175, -.58), (.175, -.58), (-.175, -.40), (.175, -.40)]):
        fastener("Lid_fastener_" + str(i), (x, .714, z), hood)
    for side in [-1, 1]:
        panel("Lid_tab_mount_" + str(side), (side*.045, .713, -.69),
              (.014, .020, .024), hood, "frame_dark", .001)
        panel("Lid_shoulder_mark_"+str(side), (side*.285,.708,-.50),
              (.045,.006,.32), hood,"accent_warm",.002)
        panel("Deck_side_shell_"+str(side), (side*.505,.54,.28),
              (.09,.12,.70), body,"body_light",.015)
        panel("Deck_edge_strip_" + str(side), (side*.505, .605, .28),
              (.05, .01, .65), body, "accent_warm", .003)
        for suffix,z in [("F",-.56),("M",0),("R",.56)]:
            wheel=bpy.data.objects["Wheel_"+("L" if side<0 else "R")+suffix]
            name=("L" if side<0 else "R")+suffix
            hub_cover("Hub_cover_"+name,(side*.636,.205,z),wheel,.063,.012,"body_light")
            hub_cover("Hub_lock_"+name,(side*.644,.205,z),wheel,.022,.004,"frame_dark")
    panel("Lid_lift_tab", (0, .730, -.69), (.12, .014, .024), hood,
          "accent_warm", .002)
    rows = geometry_report()
    opts.output.mkdir(parents=True, exist_ok=True)
    blend = opts.output / "tuoyun-hf-r1.blend"
    glb = opts.output / "tuoyun-hf-r1.glb"
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    export_glb(glb)
    facts = {"scope": "Blender idle geometry trial, not completed high-fidelity U01",
        "revision": 3, "blender": bpy.app.version_string,
        "bevel_targets": sorted(BEVEL_TARGETS),
        "source_sha256": hashlib.sha256(opts.source.read_bytes()).hexdigest(),
        "glb_sha256": hashlib.sha256(glb.read_bytes()).hexdigest(),
        "triangles": sum(r["triangles"] for r in rows), "meshes": rows,
        "build_seconds": round(time.monotonic()-started, 3),
        "animations_exported": False, "textures_created": False,
        "not_run": ["complete motion clearance", "UV production", "baking", "LOD",
                    "full-scene performance", "owner high-fidelity acceptance"]}
    (opts.output / "geometry-facts.json").write_text(json.dumps(facts, indent=2)+"\n")
    print("HF_BOOT_BUILT", facts["triangles"], "triangles", facts["build_seconds"], "seconds")


if __name__ == "__main__":
    main()
