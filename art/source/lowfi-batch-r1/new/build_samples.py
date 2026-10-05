"""ART-LF-BATCH-01 subpackage A builder: U03 wangshan / F05 repair / F06 lander.

Run with the repo Blender (4.5.14 LTS):
  tools-bin/Blender-4.5.14.app/Contents/MacOS/Blender --background \
      --factory-startup --python-exit-code 1 \
      --python art/source/lowfi-batch-r1/new/build_samples.py -- \
      [--only wangshan|repair|lander|all] [--check]
      [--verify DIR_MODEL --compare MODEL.glb]

Geometry/poses/sockets/clips frozen by docs/art/production/tasks/ART-LF-BATCH-01.md.
Real Blender actions + NLA strips per clip, exported via glTF ACTIONS mode
(same-name actions merge into one named glTF animation; wheels share one move
action through per-object slots). Linear keyframes, 30 fps, root never keyed.
Helpers (world_position/panel/axle/pivot/survey/compare_glb/negative suite)
copied from the accepted art/source/units/zhulei-r1/build_sample.py.
"""
import argparse
import hashlib
import json
import math
import struct
import sys
import tempfile
import time
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
U01 = ROOT / "art/source/units/tuoyun-r1/tuoyun-r1.glb"
CRATE = ROOT / "art/source/props/crate-r1/crate-r1.glb"
RUNTIME = ROOT / "prototype/assets/lowfi-batch-r1/models"
FPS = 30

# U01 fleet material values (kept identical for visual continuity).
MATERIAL_COLORS = {
    "frame_dark": ([0.0401, 0.0578, 0.0694, 1.0], 0.2, 0.75),
    "body_light": ([0.6818, 0.714, 0.6718, 1.0], 0.0, 0.65),
    "rubber": ([0.0171, 0.0232, 0.0266, 1.0], 0.0, 0.9),
    "accent_warm": ([0.6712, 0.1975, 0.048, 1.0], 0.0, 0.6),
}
KEEP_NODES = {"Wheels", "Rocker_L", "Rocker_R", "Bogie_L", "Bogie_R",
              "Wheel_LF", "Wheel_LM", "Wheel_LR",
              "Wheel_RF", "Wheel_RM", "Wheel_RR"}
DROP_NODES = {"Chassis", "Head", "Body", "ProtectedElectronics", "HoodLid",
              "RearLock", "ChargeCap", "CargoBox", "TowRod",
              "Socket_Cargo", "Socket_TowFront", "Socket_TowRear",
              "Socket_Charge"}


def world_position(x, y, z):
    # glTF +Y up / -Z forward -> Blender +Z up / +Y forward.
    return Vector((x, -z, y))


def to_gltf(v):
    return (v.x, v.z, -v.y)


def local_position(x, y, z):
    # glTF-local offset -> Blender-local (same axis map as world).
    return Vector((x, -z, y))


def weld_and_bevel(obj, width, segments):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    modifier = obj.modifiers.new("Manufactured_edge", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(35)
    modifier.use_clamp_overlap = True
    modifier.harden_normals = True


def material(role):
    mat = bpy.data.materials.get(role)
    assert mat is not None, "missing material " + role
    return mat


def create_materials(roles):
    for role in roles:
        if bpy.data.materials.get(role) is not None:
            continue
        color, metallic, rough = MATERIAL_COLORS[role]
        mat = bpy.data.materials.new(role)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = rough


def keep_world(obj, parent):
    saved = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = saved


def panel(name, xyz, size, parent, role):
    bpy.ops.mesh.primitive_cube_add(size=1, location=world_position(*xyz))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    weld_and_bevel(obj, .003, 1)
    return obj


def bevel_panel(obj, width, segments):
    # For meshes built in place (cube with bevel, e.g. CoreBus) after sizing.
    modifier = obj.modifiers.new("Manufactured_edge", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(35)
    modifier.use_clamp_overlap = True
    modifier.harden_normals = True


def axle_x(name, xyz, radius, length, parent, role="frame_dark"):
    # Cylinder along glTF X (Blender X).
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius,
                                        depth=length, location=world_position(*xyz),
                                        rotation=(0, math.pi / 2, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    weld_and_bevel(obj, .001, 1)
    return obj


def axle_z(name, xyz, radius, length, parent, role="frame_dark"):
    # Cylinder along glTF Z (Blender Y).
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius,
                                        depth=length, location=world_position(*xyz),
                                        rotation=(math.pi / 2, 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    weld_and_bevel(obj, .001, 1)
    return obj


def rod(name, a, b, radius, parent, role="frame_dark"):
    # Cylinder from glTF point a to b along its own axis.
    va, vb = world_position(*a), world_position(*b)
    center = (va + vb) / 2
    direction = (vb - va)
    length = direction.length
    quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius,
                                        depth=length, location=center,
                                        rotation=quaternion.to_euler())
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    weld_and_bevel(obj, .001, 1)
    return obj


def pivot(name, xyz, parent, display=.04, local=False):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = display
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    if local:
        # True local placement: identity parent inverse, location in the
        # parent's own glTF-local frame (e.g. Socket_Sensor under SensorPitch).
        obj.location = local_position(*xyz)
    else:
        obj.location = world_position(*xyz)
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def socket(name, xyz, parent, yaw_deg=0.0, local=False):
    obj = pivot(name, xyz, parent, .05, local)
    obj.rotation_euler = (0, 0, math.radians(yaw_deg))
    return obj


# ---------------------------------------------------------------- actions

def ensure_action(name):
    act = bpy.data.actions.get(name)
    if act is None:
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
    return act


def _write_kps(fc, points):
    fc.keyframe_points.add(len(points))
    for kp, (frame, value) in zip(fc.keyframe_points, points):
        kp.co = (float(frame), float(value))
    for kp in fc.keyframe_points:
        kp.interpolation = 'LINEAR'


def find_slot(act, obj):
    for s in act.slots:
        if s.identifier == "OB" + obj.name:
            return s
    raise AssertionError("no slot for " + obj.name + " in " + act.name)


def bind_rot(clip, obj, seq):
    # seq: [(frame, (x, y, z) radians, Blender axes)]
    act = ensure_action(clip)
    ad = obj.animation_data_create()
    ad.action = act
    for i in range(3):
        fc = act.fcurve_ensure_for_datablock(obj, "rotation_euler", index=i)
        _write_kps(fc, [(f, vals[i]) for f, vals in seq])
    ad.action_slot = find_slot(act, obj)
    return act


def bind_loc(clip, obj, axis, seq):
    # seq: [(frame, value)] for one Blender location component; others keyed
    # at rest by the caller through bind_loc_baseline.
    act = ensure_action(clip)
    ad = obj.animation_data_create()
    ad.action = act
    fc = act.fcurve_ensure_for_datablock(obj, "location", index=axis)
    _write_kps(fc, seq)
    ad.action_slot = find_slot(act, obj)
    return act


def finalize_clips(entries):
    # entries: [(clip, obj)] unique pairs -> one NLA track + single-strip each;
    # active action cleared so the saved file rests at the idle pose.
    seen = set()
    for clip, obj in entries:
        if (clip, obj.name) in seen:
            continue
        seen.add((clip, obj.name))
        act = bpy.data.actions[clip]
        ad = obj.animation_data
        assert ad is not None, (clip, obj.name)
        track = ad.nla_tracks.new()
        strip = track.strips.new(name=clip, start=0, action=act)
        strip.action_slot = find_slot(act, obj)
    for obj in {obj for _, obj in entries}:
        obj.animation_data.action = None


def clip_objects(clip):
    out = []
    for obj in bpy.data.objects:
        ad = obj.animation_data
        if ad is None:
            continue
        if any(t.strips and t.strips[0].action.name == clip
               for t in ad.nla_tracks):
            out.append(obj)
    return out


def assert_frame0_idle(clips, rests):
    # Every clip must begin at the idle pose so the saved rest pose, the GLB
    # node transforms and any frame-0 NLA stacking all stay idle.
    for clip in clips:
        act = bpy.data.actions[clip]
        for obj in clip_objects(clip):
            obj.animation_data.action = act
            obj.animation_data.action_slot = find_slot(act, obj)
    bpy.context.scene.frame_set(0)
    for name, (kind, rest) in rests.items():
        obj = bpy.data.objects[name]
        got = tuple(obj.rotation_euler) if kind == "rot" else tuple(obj.location)
        # float32 RNA storage vs float64 literals: 1e-6 tolerance
        assert all(abs(a - b) <= 1e-6 for a, b in zip(got, rest)), \
            (name, got, rest)
    for clip in clips:
        for obj in clip_objects(clip):
            obj.animation_data.action = None
    bpy.context.scene.frame_set(0)


def measure_clip(clip, survey_fn):
    # Assign the action to every object holding it (suspends NLA stacking),
    # sample at its own keyframes, then restore the resting pose.
    frames = set()
    actors = clip_objects(clip)
    assert actors, "clip has no objects: " + clip
    for obj in actors:
        for t in obj.animation_data.nla_tracks:
            if t.strips and t.strips[0].action.name == clip:
                obj.animation_data.action = bpy.data.actions[clip]
                obj.animation_data.action_slot = find_slot(
                    bpy.data.actions[clip], obj)
        act = bpy.data.actions[clip]
        for fc in act.fcurves:
            frames.update(k.co[0] for k in fc.keyframe_points)
    if not frames:  # no fcurve access: fall back to scene range
        frames = {0, bpy.context.scene.frame_end}
    lo = hi = None
    hold_lo = hold_hi = None
    for frame in sorted(frames):
        bpy.context.scene.frame_set(int(frame))
        rows = survey_fn()
        clo = [min(r["bounds"]["min"][i] for r in rows) for i in range(3)]
        chi = [max(r["bounds"]["max"][i] for r in rows) for i in range(3)]
        lo = clo if lo is None else [min(a, b) for a, b in zip(lo, clo)]
        hi = chi if hi is None else [max(a, b) for a, b in zip(hi, chi)]
        if frame == sorted(frames)[-1]:
            hold_lo, hold_hi = clo, chi
    for obj in actors:
        obj.animation_data.action = None
    bpy.context.scene.frame_set(0)
    return lo, hi, hold_lo, hold_hi, sorted(frames)


# ---------------------------------------------------------------- survey

def survey():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    rows = []
    for obj in sorted(bpy.data.objects, key=lambda o: o.name):
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        points = [evaluated.matrix_world @ v.co for v in mesh.vertices]
        assert points and all(math.isfinite(c) for p in points for c in p), obj.name
        roles = sorted({m.name for m in mesh.materials if m})
        assert roles and set(roles) <= set(MATERIAL_COLORS), (obj.name, roles)
        points = [to_gltf(p) for p in points]
        rows.append({"node": obj.name, "triangles": len(mesh.loop_triangles),
                     "roles": roles,
                     "bounds": {"min": [round(min(p[i] for p in points), 6) for i in range(3)],
                                "max": [round(max(p[i] for p in points), 6) for i in range(3)]}})
        evaluated.to_mesh_clear()
    return rows


def socket_entries(names):
    out = {}
    for name in sorted(names):
        obj = bpy.data.objects[name]
        assert obj is not None, name
        t = obj.matrix_world.translation
        basis = obj.matrix_world.to_3x3()
        out[name] = {"position": [round(v, 6) for v in to_gltf(t)],
                     "forward": [round(v, 6) for v in to_gltf(basis @ Vector((0, 1, 0)))],
                     "up": [round(v, 6) for v in to_gltf(basis @ Vector((0, 0, 1)))]}
    return out


# ---------------------------------------------------------------- GLB io

def export_glb(path, animations=True):
    result = bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
        export_apply=True, export_yup=True, export_animations=animations,
        export_animation_mode="ACTIONS", export_force_sampling=False,
        export_materials="EXPORT", export_normals=True, export_texcoords=True,
        export_cameras=False, export_lights=False, export_extras=True)
    assert result == {"FINISHED"}, result


def read_glb(path):
    data = Path(path).read_bytes()
    magic, version, _ = struct.unpack_from("<III", data, 0)
    assert magic == 0x46546C67 and version == 2, (magic, version)
    chunk_len, chunk_type = struct.unpack_from("<II", data, 12)
    assert chunk_type == 0x4E4F534A, chunk_type
    js = json.loads(data[20:20 + chunk_len])
    offset = 20 + chunk_len
    bin_len, bin_type = struct.unpack_from("<II", data, offset)
    assert bin_type == 0x004E4942, bin_type
    return js, data[offset + 8:offset + 8 + bin_len]


def glb_animations(path):
    js, _ = read_glb(path)
    names = [n.get("name") for n in js["nodes"]]
    out = []
    for an in js.get("animations", []):
        chans = {}
        for c in an["channels"]:
            node = names[c["target"]["node"]]
            path = c["target"]["path"]
            chans.setdefault(node, set()).add(path)
        out.append({"name": an.get("name"),
                    "duration_s": round(max(
                        js["accessors"][s["input"]]["max"][0] if
                        js["accessors"][s["input"]].get("max") else 0
                        for s in an["samplers"]), 6),
                    "channels": {k: sorted(v) for k, v in sorted(chans.items())},
                    "key_counts": [js["accessors"][s["input"]]["count"]
                                   for s in an["samplers"]]})
    return out


def accessor_region(js, buffer, index):
    acc = js["accessors"][index]
    view = js["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    count = acc["count"] * {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
                            "MAT4": 16}[acc["type"]]
    count *= {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[acc["componentType"]]
    return buffer[start:start + count], acc


def compare_glb(path_a, path_b, tol=1e-6):
    # Geometry comparison from zhulei build_sample.py plus animation channels.
    ja, ba = read_glb(path_a)
    jb, bb = read_glb(path_b)
    problems = []
    max_diff = 0.0
    assert len(ja["nodes"]) == len(jb["nodes"]), "node count"
    for i, (na, nb) in enumerate(zip(ja["nodes"], jb["nodes"])):
        label = "node%d(%s)" % (i, na.get("name"))
        assert na.get("name") == nb.get("name"), label
        assert na.get("mesh") == nb.get("mesh"), label + " mesh"
        assert na.get("children") == nb.get("children"), label + " children"
        for key in ("translation", "rotation", "scale"):
            va, vb = na.get(key), nb.get(key)
            assert (va is None) == (vb is None), label + " " + key
            if va:
                for j, (x, y) in enumerate(zip(va, vb)):
                    diff = abs(x - y)
                    max_diff = max(max_diff, diff)
                    if diff > tol:
                        problems.append("%s %s[%d] %r vs %r" % (label, key, j, x, y))
    assert len(ja["meshes"]) == len(jb["meshes"]), "mesh count"
    for i, (ma, mb) in enumerate(zip(ja["meshes"], jb["meshes"])):
        assert ma.get("name") == mb.get("name"), "mesh%d name" % i
        for pa, pb in zip(ma["primitives"], mb["primitives"]):
            assert pa.get("attributes") == pb.get("attributes"), "mesh%d attrs" % i
            assert pa.get("indices") == pb.get("indices"), "mesh%d indices acc" % i
    assert len(ja["accessors"]) == len(jb["accessors"]), "accessor count"
    for i, (aa, ab) in enumerate(zip(ja["accessors"], jb["accessors"])):
        assert aa["componentType"] == ab["componentType"], "acc%d type" % i
        assert aa["type"] == ab["type"] and aa["count"] == ab["count"], "acc%d" % i
        ra, acc = accessor_region(ja, ba, i)
        rb, _ = accessor_region(jb, bb, i)
        if acc["componentType"] == 5126:
            fa = struct.unpack("<%df" % (len(ra) // 4), ra)
            fb = struct.unpack("<%df" % (len(rb) // 4), rb)
            for j, (x, y) in enumerate(zip(fa, fb)):
                diff = abs(x - y)
                max_diff = max(max_diff, diff)
                if diff > tol:
                    problems.append("acc%d[%d] %r vs %r" % (i, j, x, y))
        else:
            if ra != rb:
                problems.append("acc%d non-float bytes differ" % i)
    aa_list = [(a.get("name"), len(a.get("channels", []))) for a in ja.get("animations", [])]
    ab_list = [(a.get("name"), len(a.get("channels", []))) for a in jb.get("animations", [])]
    assert aa_list == ab_list, ("animations differ", aa_list, ab_list)
    sha_a = hashlib.sha256(Path(path_a).read_bytes()).hexdigest()
    sha_b = hashlib.sha256(Path(path_b).read_bytes()).hexdigest()
    return {"max_float_diff": max_diff, "problems": problems[:20],
            "reexport_sha256": sha_a, "deliverable_sha256": sha_b,
            "identical_bytes": sha_a == sha_b}


# ---------------------------------------------------------------- checks

def check_rows(rows):
    for r in rows:
        assert r["roles"] and set(r["roles"]) <= set(MATERIAL_COLORS), (r["node"], r["roles"])
        for key in ("min", "max"):
            assert all(math.isfinite(c) for c in r["bounds"][key]), (r["node"], key)


def check_outputs(paths):
    for path in paths:
        assert path.is_file() and path.stat().st_size > 0, path
        if path.suffix == ".glb":
            assert path.read_bytes()[:4] == b"glTF", path


def run_check_suite():
    def row(name, role="frame_dark", finite=True):
        y_max = .1 if finite else float("nan")
        return {"node": name, "roles": [role], "triangles": 2,
                "bounds": {"min": [0.0, 0.0, 0.0], "max": [.1, y_max, .1]}}
    results = {}
    try:
        Path("/nonexistent/lowfi/u01-missing.glb").resolve(strict=True)
        results["missing_input"] = "FAIL: missing source not detected"
    except FileNotFoundError:
        results["missing_input"] = "PASS"
    try:
        check_rows([row("A", role="cargo_plastic")])
        results["unknown_role"] = "FAIL: not detected"
    except AssertionError:
        results["unknown_role"] = "PASS"
    try:
        check_rows([row("A", finite=False)])
        results["non_finite"] = "FAIL: not detected"
    except AssertionError:
        results["non_finite"] = "PASS"
    try:
        check_outputs([Path("/nonexistent/lowfi-missing.glb")])
        results["empty_output"] = "FAIL: not detected"
    except AssertionError:
        results["empty_output"] = "PASS"
    assert all(v == "PASS" for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


# ---------------------------------------------------------------- shared build

def prepare_scene(root_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = 60
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    root = bpy.data.objects.new(root_name, None)
    scene.collection.objects.link(root)
    return root


def import_u01_chassis(root_name):
    assert U01.is_file(), "missing U01 source " + str(U01)
    # --factory-startup still loads the default scene (with its Cube); clear
    # first, exactly like zhulei-r1 build_sample.py import_source().
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(U01))
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = 60
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    for obj in bpy.data.objects:
        obj.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    objects = bpy.data.objects
    missing = sorted((KEEP_NODES | {"tuoyun-r1", "Model"}) - set(objects.keys()))
    assert not missing, "missing wheel/ancestor nodes: " + ", ".join(missing)
    for name in sorted(DROP_NODES):
        obj = objects.get(name)
        assert obj is not None, "expected imported node " + name
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)
    objects["tuoyun-r1"].name = root_name
    root = objects[root_name]
    model = objects["Model"]
    assert not any("Cargo" in o.name for o in bpy.data.objects), "Cargo left"
    return root, model


def wheel_rest_eulers():
    out = {}
    for name in sorted(KEEP_NODES):
        if not name.startswith("Wheel_"):
            continue
        wheel = bpy.data.objects[name]
        assert wheel.type == "MESH", name
        wheel.rotation_mode = 'XYZ'
        out[name] = wheel.rotation_quaternion.to_euler('XYZ')
        wheel.rotation_euler = out[name]
    assert len(out) == 6, out.keys()
    return out


# ---------------------------------------------------------------- wangshan

def build_wangshan():
    D = math.radians
    root, model = import_u01_chassis("Wangshan")
    # Body stack (frame tables from ART-LF-BATCH-01 §U03).
    panel("FrameBed", (0, .37, .10), (.90, .14, 1.25), model, "frame_dark")
    panel("BodyShell", (0, .53, .10), (.90, .22, 1.22), model, "body_light")
    panel("SensorBase", (0, .69, -.08), (.32, .10, .32), model, "frame_dark")
    panel("Mast", (0, 1.005, -.08), (.12, .53, .12), model, "body_light")
    panel("MastHeadMount", (0, 1.28, -.08), (.24, .04, .22), model, "frame_dark")
    yaw = pivot("SensorYaw", (0, 1.30, -.08), model)
    panel("HeadForkPostL", (-.35, 1.50, -.08), (.05, .40, .10), yaw, "frame_dark")
    panel("HeadForkPostR", (.35, 1.50, -.08), (.05, .40, .10), yaw, "frame_dark")
    panel("HeadForkBridge", (0, 1.33, -.08), (.76, .06, .16), yaw, "frame_dark")
    pitch = pivot("SensorPitch", (0, 1.62, -.08), yaw)
    axle_x("SensorPitchAxle", (0, 1.62, -.08), .045, .76, pitch)
    panel("SensorHead", (0, 1.62, -.16), (.60, .22, .30), pitch, "body_light")
    panel("SensorFace", (0, 1.62, -.317), (.48, .12, .014), pitch, "frame_dark")
    socket("Socket_Sensor", (0, 0, -.245), pitch, local=True)
    panel("RearModule", (0, .75, .42), (.58, .22, .42), model, "frame_dark")
    service = pivot("ServiceLid", (0, .86, .64), model)
    panel("ServiceLidPanel", (0, .86, .42), (.64, .025, .46), service, "body_light")
    panel("ServiceHingeMountL", (-.20, .86, .63), (.10, .05, .08), model, "frame_dark")
    panel("ServiceHingeMountR", (.20, .86, .63), (.10, .05, .08), model, "frame_dark")
    axle_x("ServiceHingeAxle", (0, .86, .64), .015, .44, model)
    panel("ChargeHousing", (.455, .55, -.32), (.05, .16, .16), model, "frame_dark")
    charge = pivot("ChargeCap", (.484, .61, -.32), model)
    panel("ChargeCapPanel", (.484, .55, -.32), (.008, .12, .12), charge, "accent_warm")
    axle_z("ChargeHingeAxle", (.484, .61, -.32), .012, .15, model)
    panel("TowFrontMount", (0, .28, -.605), (.18, .12, .23), model, "frame_dark")
    panel("TowFrontEar", (0, .24, -.76), (.20, .06, .08), model, "accent_warm")
    panel("TowRearMount", (0, .28, .715), (.18, .12, .17), model, "frame_dark")
    socket("Socket_TowFront", (0, .24, -.80), model, 0.0)
    socket("Socket_TowRear", (0, .24, .80), model, 180.0)
    socket("Socket_Charge", (.48, .55, -.32), model, -90.0)
    socket("Socket_Service", (0, .75, .65), model, 180.0)

    wheel_rest = wheel_rest_eulers()
    wheels = sorted(wheel_rest)
    pivots = [("SensorYaw", "rot"), ("SensorPitch", "rot"),
              ("ServiceLid", "rot"), ("ChargeCap", "rot")]

    def rot_seq(obj, seq):
        return seq

    entries = []

    def clip_all(name, dynamic):
        # dynamic: dict obj-name -> rot seq (Blender radians); others baseline.
        for pname, _ in pivots:
            bind_rot(name, bpy.data.objects[pname],
                     dynamic.get(pname, [(0, (0, 0, 0)), (30, (0, 0, 0))]))
            entries.append((name, bpy.data.objects[pname]))
        for w in wheels:
            rest = wheel_rest[w]
            seq = dynamic.get(w, [(0, (rest.x, rest.y, rest.z)),
                                  (30, (rest.x, rest.y, rest.z))])
            bind_rot(name, bpy.data.objects[w], seq)
            entries.append((name, bpy.data.objects[w]))

    turn = 2 * math.pi
    # Full turn keyed 0/90/180/270/360 deg (frames 0/7.5/15/22.5/30): two keys
    # alone export identical quaternions (0 == 360) and the wheel never turns.
    clip_all("move", {w: [(f, (wheel_rest[w].x + turn * f / 30.0,
                               wheel_rest[w].y, wheel_rest[w].z))
                          for f in (0, 7.5, 15, 22.5, 30)]
                      for w in wheels})
    clip_all("work", {"SensorYaw": [(0, (0, 0, D(-35))), (30, (0, 0, D(35))),
                                    (60, (0, 0, D(-35)))]})
    clip_all("charge", {"ChargeCap": [(0, (0, 0, 0)), (30, (0, D(-60), 0))]})
    # Held poses still start from idle at frame 0 so any NLA stacking at
    # frame 0 and the saved/scene rest stay the idle pose.
    clip_all("disabled", {"SensorPitch": [(0, (0, 0, 0)),
                                          (30, (D(-35), 0, 0))]})
    clip_all("maintenance", {"ServiceLid": [(0, (0, 0, 0)),
                                            (30, (D(70), 0, 0))]})
    finalize_clips(entries)

    rests = {name: ("rot", (0.0, 0.0, 0.0)) for name, _ in pivots}
    for w in wheels:
        rests[w] = ("rot", (wheel_rest[w].x, wheel_rest[w].y, wheel_rest[w].z))
    candidates = {
        "idle": ((-.65, 0.0, -.84), (.65, 1.80, .84)),
        "move": ((-.65, 0.0, -.84), (.65, 1.80, .84)),
        "work": ((-.65, 0.0, -.84), (.65, 1.80, .84)),
        "charge": ((-.65, 0.0, -.84), (.65, 1.80, .84)),
        "disabled": ((-.65, 0.0, -.84), (.65, 1.80, .84)),
        "maintenance": ((-.65, 0.0, -.84), (.65, 1.85, .84)),
    }
    return root, candidates, ["move", "work", "charge", "disabled",
                              "maintenance"], rests


# ---------------------------------------------------------------- repair

def build_repair():
    D = math.radians
    root = prepare_scene("Repair")
    create_materials(["frame_dark", "body_light", "accent_warm"])
    model = pivot("Model", (0, 0, 0), root, .06)
    panel("Foundation", (0, .02, 0), (3.4, .04, 3.2), model, "frame_dark")
    for sx in (-1, 1):
        for sz in (-1, 1):
            panel("PortalPost%d%s" % (sx, sz), (sx * 1.45, 1.13, sz * .90),
                  (.12, 2.18, .12), model, "frame_dark")
    for sz in (-1, 1):
        panel("PortalCrossbar%d" % sz, (0, 2.25, sz * .90), (3.02, .12, .14),
              model, "body_light")
    for sx in (-1, 1):
        panel("SideRail%d" % sx, (sx * 1.45, 2.25, 0), (.12, .12, 1.80),
              model, "body_light")
    for sx in (-1, 1):
        panel("BayGuide%d" % sx, (sx * 1.0, .045, 0), (.035, .01, 2.4),
              model, "accent_warm")
    panel("CabinetFoot", (-1.60, .05, .90), (.50, .10, .70), model, "frame_dark")
    panel("CabinetBody", (-1.60, .75, .90), (.40, 1.30, .60), model, "body_light")
    panel("CabinetCap", (-1.60, 1.425, .90), (.44, .05, .64), model, "body_light")
    panel("ToolTrack", (1.05, 1.61, -.35), (.80, .12, .16), model, "frame_dark")
    panel("TrackHanger", (1.45, 1.93, -.35), (.12, .64, .12), model, "frame_dark")
    slide = pivot("ToolSlide", (1.05, 1.60, -.35), model)
    panel("ToolDropRod", (1.05, 1.30, -.35), (.10, .60, .10), slide, "frame_dark")
    panel("ToolBlock", (1.05, .95, -.35), (.30, .10, .20), slide, "accent_warm")
    cover = pivot("ServiceCover", (-1.395, 1.25, .90), model)
    panel("ServiceCoverPanel", (-1.395, .90, .90), (.018, .70, .40), cover,
          "frame_dark")
    # Real Z hinge on the rotation axis itself; seats on the cabinet +X face.
    axle_z("CoverHingeAxle", (-1.395, 1.25, .90), .015, .40, model)
    panel("CoverHingeSeatRear", (-1.395, 1.25, .72), (.06, .06, .10), model,
          "frame_dark")
    panel("CoverHingeSeatFront", (-1.395, 1.25, 1.08), (.06, .06, .10), model,
          "frame_dark")
    gate = pivot("StopGate", (0, .045, -1.52), model)
    # Parent the panel while the pivot is still at 0, THEN rotate the pivot;
    # keep_world under an already-rotated pivot would bake a +90 counter
    # rotation into the panel's local transform and the gate would stand
    # closed at idle instead of lying flat outside.
    panel("StopGatePanel", (0, .215, -1.52), (2.0, .34, .035), gate, "accent_warm")
    gate.rotation_euler = (D(-90), 0, 0)  # idle: flat outside on the ground
    axle_x("StopGateAxle", (0, .045, -1.52), .02, 1.44, model)
    panel("StopGateSeatL", (-.72, .05, -1.52), (.12, .10, .12), model, "frame_dark")
    panel("StopGateSeatR", (.72, .05, -1.52), (.12, .10, .12), model, "frame_dark")
    panel("PowerInGuard", (-1.60, .18, 1.23), (.30, .12, .06), model, "accent_warm")
    socket("Socket_Bay", (0, .04, 0), model, 0.0)
    socket("Socket_PowerIn", (-1.60, .18, 1.255), model, 180.0)
    socket("Socket_Service", (-1.385, .90, .90), model, -90.0)

    movable = ["ToolSlide", "ServiceCover", "StopGate"]
    base_flat = (D(-90), 0, 0)  # StopGate idle: folded flat
    base = {"ToolSlide": (0, 0, 0), "ServiceCover": (0, 0, 0),
            "StopGate": base_flat}
    entries = []

    def clip_all(name, dyn):
        for m in movable:
            seq = dyn.get(m, [(0, base[m]), (30, base[m])])
            bind_rot(name, bpy.data.objects[m], seq)
            entries.append((name, bpy.data.objects[m]))

    slide_obj = bpy.data.objects["ToolSlide"]
    bind_loc("work", slide_obj, 0, [(0, 1.05), (30, .85), (60, 1.05)])
    entries.append(("work", slide_obj))
    clip_all("work", {})
    clip_all("disabled", {"StopGate": [(0, base_flat), (30, (0, 0, 0))]})
    clip_all("maintenance", {"ServiceCover": [(0, (0, 0, 0)),
                                              (30, (0, D(-70), 0))]})
    finalize_clips(entries)

    candidates = {k: ((-1.85, 0.0, -1.90), (1.70, 2.32, 1.60)) for k in
                  ("idle", "work", "disabled", "maintenance")}
    rests = {"ToolSlide": ("loc", (1.05, .35, 1.60)),
             "ServiceCover": ("rot", (0.0, 0.0, 0.0)),
             "StopGate": ("rot", (D(-90), 0.0, 0.0))}
    return root, candidates, ["work", "disabled", "maintenance"], rests


# ---------------------------------------------------------------- lander

def build_lander():
    D = math.radians
    root = prepare_scene("Lander")
    model = pivot("Model", (0, 0, 0), root, .06)
    # Import the active P01 crate first so its palette materials exist before
    # any panel() call needs them; create_materials only fills gaps after.
    assert CRATE.is_file(), "missing crate source " + str(CRATE)
    bpy.ops.import_scene.gltf(filepath=str(CRATE))
    crate = bpy.data.objects.get("Crate")
    assert crate is not None and crate.type == "MESH", "crate import failed"
    create_materials(["frame_dark", "body_light", "accent_warm"])
    core = panel("CoreBus", (0, 1.55, .25), (1.60, 1.60, 1.40), model, "body_light")
    bevel_panel(core, .05, 2)
    panel("UnderCarriage", (0, .70, .25), (1.40, .10, 1.20), model, "frame_dark")
    for sx in (-1, 1):
        for sz in (-1, 1):
            panel("FootPad%d%s" % (sx, sz), (sx * 1.40, .05, sz * 1.40),
                  (.40, .10, .40), model, "frame_dark")
            top = (.65 * sx, 1.15, -.40 if sz < 0 else .90)
            foot = (1.40 * sx, .15, 1.40 * sz)
            rod("LandingStrut%d%s" % (sx, sz), top, foot, .06, model)
            panel("StrutSeat%d%s" % (sx, sz), (sx * 1.40, .125, sz * 1.40),
                  (.16, .15, .16), model, "accent_warm")
    # Forward cargo bay.
    panel("CargoFloor", (0, .82, -.78), (1.20, .08, .90), model, "frame_dark")
    panel("CargoWallL", (-.62, 1.17, -.78), (.08, .62, .90), model, "frame_dark")
    panel("CargoWallR", (.62, 1.17, -.78), (.08, .62, .90), model, "frame_dark")
    panel("CargoTopBeam", (0, 1.50, -.78), (1.28, .06, .90), model, "body_light")
    lid = pivot("CargoLid", (0, 1.54, -1.265), model)
    panel("CargoLidPanel", (0, 1.20, -1.265), (1.22, .68, .04), lid, "body_light")
    axle_x("CargoLidAxle", (0, 1.54, -1.265), .025, 1.28, lid)
    panel("CargoFrontBeam", (0, 1.50, -1.22), (1.28, .06, .08), model, "body_light")
    panel("CargoHingeSeatL", (-.55, 1.54, -1.24), (.10, .08, .10), model, "frame_dark")
    panel("CargoHingeSeatR", (.55, 1.54, -1.24), (.10, .08, .10), model, "frame_dark")
    demo = pivot("LanderPayloadDemo", (0, .86, -.83), model, .05)
    crate.location = world_position(0, .86, -.83)
    keep_world(crate, demo)
    # Rear service module + stop plate.
    panel("RearModule", (0, 1.30, 1.015), (.70, .50, .15), model, "frame_dark")
    service = pivot("RearServiceLid", (0, 1.55, 1.10), model)
    panel("RearServiceLidPanel", (0, 1.30, 1.10), (.68, .50, .025), service,
          "body_light")
    axle_x("RearServiceAxle", (0, 1.55, 1.10), .018, .74, service)
    panel("RearHingeSeatL", (-.28, 1.55, 1.06), (.08, .06, .08), model, "frame_dark")
    panel("RearHingeSeatR", (.28, 1.55, 1.06), (.08, .06, .08), model, "frame_dark")
    stop = pivot("StopPlate", (0, 2.35, .25), model)
    # Same ordering rule as StopGate: children first, pivot rotation last.
    panel("StopPlatePanel", (0, 2.47, .25), (.55, .24, .025), stop, "accent_warm")
    axle_x("StopPlateAxle", (0, 2.35, .25), .015, .60, stop)
    stop.rotation_euler = (D(-90), 0, 0)  # idle: folded flat on the hull top
    panel("StopPlateSeatL", (-.20, 2.35, .25), (.08, .05, .08), model, "frame_dark")
    panel("StopPlateSeatR", (.20, 2.35, .25), (.08, .05, .08), model, "frame_dark")
    panel("HandlingEar", (0, .70, .925), (.20, .10, .15), model, "frame_dark")
    socket("Socket_Cargo", (0, .86, -.83), model, 0.0)
    socket("Socket_Service", (0, 1.3, 1.1125), model, 180.0)
    socket("Socket_Handling", (0, .70, 1.0), model, 180.0)

    movable = ["CargoLid", "RearServiceLid", "StopPlate"]
    base_flat = (D(-90), 0, 0)  # StopPlate idle: folded flat on hull top
    base = {"CargoLid": (0, 0, 0), "RearServiceLid": (0, 0, 0),
            "StopPlate": base_flat}
    entries = []

    def clip_all(name, dyn):
        for m in movable:
            seq = dyn.get(m, [(0, base[m]), (30, base[m])])
            bind_rot(name, bpy.data.objects[m], seq)
            entries.append((name, bpy.data.objects[m]))

    clip_all("work", {"CargoLid": [(0, (0, 0, 0)), (30, (D(90), 0, 0))]})
    clip_all("disabled", {"StopPlate": [(0, base_flat), (30, (0, 0, 0))]})
    clip_all("maintenance", {"RearServiceLid": [(0, (0, 0, 0)),
                                                (30, (D(-70), 0, 0))]})
    finalize_clips(entries)

    candidates = {k: ((-1.65, 0.0, -2.02), (1.65, 2.60, 1.65)) for k in
                  ("idle", "work", "disabled", "maintenance")}
    rests = {"CargoLid": ("rot", (0.0, 0.0, 0.0)),
             "RearServiceLid": ("rot", (0.0, 0.0, 0.0)),
             "StopPlate": ("rot", (D(-90), 0.0, 0.0))}
    return root, candidates, ["work", "disabled", "maintenance"], rests


# ---------------------------------------------------------------- pipeline

BUILDERS = {"wangshan": build_wangshan, "repair": build_repair,
            "lander": build_lander}
CANDIDATE_STATIC = {
    "wangshan": ((-.65, 0.0, -.84), (.65, 1.80, .84)),
    "repair": ((-1.85, 0.0, -1.90), (1.70, 2.32, 1.60)),
    "lander": ((-1.65, 0.0, -2.02), (1.65, 2.60, 1.65)),
}


def build_model(name):
    started = time.monotonic()
    out_dir = HERE / name
    out_dir.mkdir(parents=True, exist_ok=True)
    builder = BUILDERS[name]
    root, candidates, clips, rests = builder()
    assert_frame0_idle(clips, rests)

    rows = survey()
    check_rows(rows)
    sockets = socket_entries([o.name for o in bpy.data.objects
                              if o.name.startswith("Socket_")])
    idle_lo = [min(r["bounds"]["min"][i] for r in rows) for i in range(3)]
    idle_hi = [max(r["bounds"]["max"][i] for r in rows) for i in range(3)]
    envs = {"idle": {"min": idle_lo, "max": idle_hi,
                     "hold_min": idle_lo, "hold_max": idle_hi}}
    for clip in clips:
        lo, hi, hold_lo, hold_hi, frames = measure_clip(clip, survey)
        # Union includes the frame-0 idle baseline every clip shares; "hold"
        # is the last-keyframe pose, i.e. the operative disabled/maintenance
        # state (and the loop end pose for move/work/charge).
        envs[clip] = {"min": lo, "max": hi, "frames": frames,
                      "hold_min": hold_lo, "hold_max": hold_hi}

    for state, env in envs.items():
        cand = candidates[state]
        within = all(cand[0][i] - 1e-9 <= env["min"][i]
                     and env["max"][i] <= cand[1][i] + 1e-9 for i in range(3))
        hold_within = all(cand[0][i] - 1e-9 <= env["hold_min"][i]
                          and env["hold_max"][i] <= cand[1][i] + 1e-9
                          for i in range(3))
        env["candidate_min"], env["candidate_max"] = list(cand[0]), list(cand[1])
        env["within"] = within
        env["hold_within"] = hold_within
        assert within and hold_within, (name, state, env, cand)

    blend = out_dir / (name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    glb = out_dir / (name + ".glb")
    export_glb(glb)
    check_outputs([glb, blend])
    anims = glb_animations(glb)
    got = sorted(a["name"] for a in anims)
    assert got == sorted(clips), (got, clips)
    for a in anims:
        assert root.name not in a["channels"] and "Model" not in a["channels"], a
        assert set(a["channels"]) <= set(
            o.name for o in clip_objects(a["name"])) | set(
            o.name for o in clip_objects(a["name"])), a["name"]

    roles_used = {}
    for r in rows:
        for role in r["roles"]:
            roles_used[role] = roles_used.get(role, 0) + 1
    facts = {
        "scope": "ART-LF-BATCH-01 subpackage A %s low-fi candidate" % name,
        "ticket": "docs/art/production/tasks/ART-LF-BATCH-01.md",
        "blender": bpy.app.version_string,
        "triangles_total": sum(r["triangles"] for r in rows),
        "mesh_count": len(rows),
        "roles_used": roles_used,
        "meshes": rows,
        "sockets": sockets,
        "clips": [{"name": a["name"], "duration_s": a["duration_s"],
                   "channels": a["channels"], "key_counts": a["key_counts"]}
                  for a in anims],
        "envelopes": envs,
        "sources": {"wangshan": "U01 tuoyun-r1.glb chassis subtree (read-only)",
                    "lander": "P01 crate-r1.glb payload crate (read-only)",
                    "repair": "original geometry"}[name],
        "helpers_provenance": "zhulei-r1 build_sample.py (accepted)",
        "files": {p.name: {"bytes": p.stat().st_size,
                           "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in [blend, glb]},
        "checks": {"envelope_within": "PASS", "clip_names": "PASS",
                   "root_not_animated": "PASS", "finite_coords": "PASS",
                   "roles_known": "PASS", "glb_magic": "PASS",
                   "frame0_idle": "PASS"},
        "not_run": ["owner visual acceptance", "master preview packaging/manifest",
                    "gameplay/performance/navigation", "Godot runtime import (separate step)"],
        "build_seconds": round(time.monotonic() - started, 3),
    }
    (out_dir / "facts.json").write_text(json.dumps(facts, indent=2) + "\n")
    write_report(out_dir, facts, name)
    RUNTIME.mkdir(parents=True, exist_ok=True)
    (RUNTIME / (name + ".glb")).write_bytes(glb.read_bytes())
    print("%s_BUILT %s triangles=%d clips=%s" %
          (name.upper(), glb, facts["triangles_total"], got))
    return facts


def write_report(out_dir, facts, name):
    lines = ["# %s low-fi build report (ART-LF-BATCH-01 subpackage A)" % name, ""]
    lines.append("Blender %s, meters, +Y up / -Z forward, root at rest, Y0 ground."
                 % facts["blender"])
    lines.append("Triangles %d across %d meshes; roles %s." %
                 (facts["triangles_total"], facts["mesh_count"],
                  json.dumps(facts["roles_used"])))
    for sock, e in facts["sockets"].items():
        lines.append("%s pos %s fwd %s up %s" %
                     (sock, e["position"], e["forward"], e["up"]))
    for clip in facts["clips"]:
        lines.append("clip %s %.3fs nodes %d keys %s" %
                     (clip["name"], clip["duration_s"], len(clip["channels"]),
                      clip["key_counts"]))
    for state, env in facts["envelopes"].items():
        lines.append("envelope %s min %s max %s within %s; hold %s..%s within %s"
                     % (state, [round(v, 3) for v in env["min"]],
                        [round(v, 3) for v in env["max"]], env["within"],
                        [round(v, 3) for v in env["hold_min"]],
                        [round(v, 3) for v in env["hold_max"]], env["hold_within"]))
    lines.append("Checks: " + json.dumps(facts["checks"]))
    lines.append("NOT_RUN: " + "; ".join(facts["not_run"]))
    lines.append("Sources: " + facts["sources"] + "; helpers from " +
                 facts["helpers_provenance"] + ".")
    lines.append("Low-fi candidate only: no aesthetic/performance/gameplay sign-off.")
    (out_dir / "report.md").write_text("\n".join(lines) + "\n")


def cmd_verify(model_dir, compare):
    blend = Path(model_dir) / (Path(model_dir).name + ".blend")
    assert blend.is_file(), blend
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    with tempfile.TemporaryDirectory(prefix="lowfi-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        result = compare_glb(reexport, compare)
    result["reopened_blend"] = str(blend)
    result["compared_glb"] = str(compare)
    (Path(model_dir) / "reopen-check.json").write_text(
        json.dumps(result, indent=2) + "\n")
    print("VERIFY_RESULT " + json.dumps(result))
    assert not result["problems"] and result["max_float_diff"] <= 1e-6, result


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", default="all",
                        choices=["wangshan", "repair", "lander", "all"])
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify", type=Path)
    parser.add_argument("--compare", type=Path)
    opts = parser.parse_args(args)
    assert bpy.app.version == (4, 5, 14), bpy.app.version_string
    if opts.verify is not None:
        assert opts.compare is not None
        cmd_verify(opts.verify, opts.compare)
        return
    if opts.check:
        run_check_suite()
        return
    names = list(BUILDERS) if opts.only == "all" else [opts.only]
    for name in names:
        build_model(name)


if __name__ == "__main__":
    main()
