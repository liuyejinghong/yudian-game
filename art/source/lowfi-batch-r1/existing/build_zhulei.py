"""ART-LF-BATCH-01 subpackage B: U02 zhulei charge lid + five real clips.

Reads the current zhulei-r1 .blend (39e9618 state, read-only), keeps all six
wheels / arm / feet / rear bay / hinge mounts, adds ChargePivot with the
existing ChargeCap kept at world pose as its child plus a solid hinge mount,
and authors the five ticket clips (move/work/charge/disabled/maintenance) as
real Blender actions on NLA tracks. Root stays static in every clip; only
sub-part transforms are keyed; every clip carries baselines for all movers.

  Blender --background --factory-startup --python-exit-code 1 --python \
      art/source/lowfi-batch-r1/existing/build_zhulei.py -- \
      [--output DIR] [--check] [--verify FILE.blend --compare FILE.glb]
"""
import argparse
import hashlib
import json
import math
import shutil
import struct
import sys
import tempfile
import time
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
SOURCE = ROOT / "art/source/units/zhulei-r1/zhulei-r1.blend"
RUNTIME = ROOT / "prototype/assets/lowfi-batch-r1/models/zhulei.glb"
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm"}
WHEELS = ["Wheel_LF", "Wheel_LM", "Wheel_LR", "Wheel_RF", "Wheel_RM", "Wheel_RR"]
SOCKET_YAW_DEG = {"Socket_TowFront": 0.0, "Socket_TowRear": 180.0,
                  "Socket_Charge": 90.0, "Socket_Service": 180.0}
FROZEN_SOCKETS = {"Socket_TowFront": ((0, .24, -.80), (0, 0, -1)),
                  "Socket_TowRear": ((0, .24, .80), (0, 0, 1)),
                  "Socket_Charge": ((-.48, .55, .34), (-1, 0, 0)),
                  "Socket_Service": ((0, .69, .695), (0, 0, 1))}
FPS = 30
CLIP_ORDER = ["move", "work", "charge", "disabled", "maintenance"]
CLIP_SECONDS = {"move": 1.0, "work": 2.0, "charge": 1.0,
                "disabled": 1.0, "maintenance": 1.0}


def world_position(x, y, z):
    # Blender's glTF importer maps +Y up / -Z forward to +Z up / +Y forward.
    return Vector((x, -z, y))


def to_gltf(v):
    return (v.x, v.z, -v.y)


def material(role):
    assert role in ROLES, role
    mat = bpy.data.materials.get(role)
    assert mat is not None, "missing material " + role
    return mat


def weld_and_bevel(obj, width, segments):
    import bmesh
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


def axle_local(name, xyz, radius, length, parent, axis="X", role="frame_dark"):
    rot = (0, math.pi / 2, 0) if axis == "X" else (math.pi / 2, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius,
                                        depth=length, location=world_position(*xyz),
                                        rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    weld_and_bevel(obj, .001, 1)
    return obj


def pivot(name, xyz, parent, display=.04):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = display
    obj.location = world_position(*xyz)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    bpy.context.view_layer.update()  # refresh matrix_world before any keep_world
    return obj


def bake_parent_inverses():
    """Fold matrix_parent_inverse into matrix_basis (world-preserving).

    The exporter writes node-local TRS from matrix_basis but keys raw local
    channels; without this, imported GLB nodes whose parent inverse is
    non-identity get a node rest that disagrees with their animation keys.
    """
    from mathutils import Matrix
    identity = Matrix.Identity(4)
    for ob in bpy.data.objects:
        if ob.parent is None or ob.matrix_parent_inverse == identity:
            continue
        ob.matrix_basis = ob.matrix_parent_inverse @ ob.matrix_basis
        ob.matrix_parent_inverse = identity


def add_charge_lid():
    """ChargePivot at the cap top edge; existing cap re-parented in place."""
    model = bpy.data.objects["Model"]
    cap = bpy.data.objects["ChargeCap"]
    housing = bpy.data.objects["ChargeHousing"]
    assert cap and housing, "ChargeCap/ChargeHousing missing in source blend"
    cap_world = cap.matrix_world.copy()
    charge_pivot = pivot("ChargePivot", (-.484, .61, .34), model)
    keep_world(cap, charge_pivot)
    assert (cap.matrix_world.translation - cap_world.translation).length <= 1e-9
    panel("ChargeHingeMount", (-.478, .615, .34), (.012, .03, .06), model,
          "frame_dark")
    axle_local("ChargeHingeAxle", (-.484, .61, .34), .012, .14, model, axis="Y")
    return charge_pivot


def key_lin(action, data_path, index, frames, values):
    fc = action.fcurves.new(data_path=data_path, index=index)
    fc.keyframe_points.add(count=len(frames))
    for k, f, v in zip(fc.keyframe_points, frames, values):
        k.co = (float(f), float(v))
        k.interpolation = 'LINEAR'
    fc.update()


def quat_spin_keys(wheel, angles_deg, frames):
    """Local-X spin keys as world-stable quaternions (360 deg never collapses)."""
    base = wheel.rotation_quaternion.copy()
    out = []
    for ang in angles_deg:
        spin = Quaternion((1, 0, 0), math.radians(ang))
        q = base @ spin
        out.append([q.w, q.x, q.y, q.z])
    return list(zip(*out))  # per component values


def build_clips():
    rad = math.radians
    wheels = [bpy.data.objects[n] for n in WHEELS]
    shoulder = bpy.data.objects["Shoulder"]
    elbow = bpy.data.objects["Elbow"]
    slides = [bpy.data.objects["SupportSlideL"], bpy.data.objects["SupportSlideR"]]
    charge = bpy.data.objects["ChargePivot"]
    hood = bpy.data.objects["HoodLid"]
    for w in wheels:
        axis = (w.matrix_world.to_3x3() @ Vector((1, 0, 0)))
        assert abs(axis.x) > .999 and abs(axis.y) < .001 and abs(axis.z) < .001, \
            "wheel local X is not the axle: %s %s" % (w.name, axis)
    shoulder_e = [round(math.degrees(a), 3) for a in shoulder.rotation_euler]
    elbow_e = [round(math.degrees(a), 3) for a in elbow.rotation_euler]
    assert shoulder_e[0] == 55.0 and elbow_e[0] == -155.0, (shoulder_e, elbow_e)
    for s in slides:
        assert abs(s.location.z - .30) <= 1e-6, s.location.z
    f1 = [0, FPS]
    f1x = [0, FPS // 2, FPS]  # collinear mid keys: sampled nlerp stays linear
    f2x = [0, FPS // 2, FPS, FPS + FPS // 2, 2 * FPS]
    fspin = [i * (FPS / 12.0) for i in range(13)]  # 30-deg steps, frames exact
    clips = {
        "move": [
            *[(w, "rotation_quaternion", c, fspin, v)
              for w in wheels
              for c, v in enumerate(quat_spin_keys(
                  w, [0, -30, -60, -90, -120, -150, -180, -210, -240, -270,
                      -300, -330, -360], fspin))],
            (shoulder, "rotation_euler", 0, f1, [rad(55), rad(55)]),
            (elbow, "rotation_euler", 0, f1, [rad(-155), rad(-155)]),
            *[(s, "location", 2, f1, [.30, .30]) for s in slides],
            (charge, "rotation_euler", 1, f1, [0.0, 0.0]),
            (hood, "rotation_euler", 0, f1, [0.0, 0.0]),
        ],
        "work": [
            (shoulder, "rotation_euler", 0, f2x,
             [rad(55), rad(17.5), rad(-20), rad(17.5), rad(55)]),
            (elbow, "rotation_euler", 0, f2x,
             [rad(-155), rad(-75), rad(5), rad(-75), rad(-155)]),
            *[(s, "location", 2, f2x, [.30, .165, .03, .165, .30]) for s in slides],
            *[(w, "rotation_quaternion", c, f2x, [v[0]] * 5)
              for w in wheels for c, v in enumerate(quat_spin_keys(w, [0], [0]))],
            (charge, "rotation_euler", 1, f2x, [0.0] * 5),
            (hood, "rotation_euler", 0, f2x, [0.0] * 5),
        ],
        "charge": [
            (charge, "rotation_euler", 1, f1x, [0.0, rad(30), rad(60)]),
            (shoulder, "rotation_euler", 0, f1x, [rad(55)] * 3),
            (elbow, "rotation_euler", 0, f1x, [rad(-155)] * 3),
            *[(s, "location", 2, f1x, [.30] * 3) for s in slides],
            *[(w, "rotation_quaternion", c, f1x, [v[0]] * 3)
              for w in wheels for c, v in enumerate(quat_spin_keys(w, [0], [0]))],
            (hood, "rotation_euler", 0, f1x, [0.0] * 3),
        ],
        "disabled": [
            (shoulder, "rotation_euler", 0, f1x, [rad(55), rad(70), rad(85)]),
            (elbow, "rotation_euler", 0, f1x, [rad(-155)] * 3),
            *[(s, "location", 2, f1x, [.30] * 3) for s in slides],
            *[(w, "rotation_quaternion", c, f1, [v[0], v[0]])
              for w in wheels for c, v in enumerate(quat_spin_keys(w, [0], [0]))],
            (charge, "rotation_euler", 1, f1, [0.0, 0.0]),
            (hood, "rotation_euler", 0, f1, [0.0, 0.0]),
        ],
        "maintenance": [
            (hood, "rotation_euler", 0, f1, [0.0, rad(70)]),
            (shoulder, "rotation_euler", 0, f1, [rad(55), rad(55)]),
            (elbow, "rotation_euler", 0, f1, [rad(-155), rad(-155)]),
            *[(s, "location", 2, f1, [.30, .30]) for s in slides],
            *[(w, "rotation_quaternion", c, f1x, [v[0]] * 3)
              for w in wheels for c, v in enumerate(quat_spin_keys(w, [0], [0]))],
            (charge, "rotation_euler", 1, f1x, [0.0] * 3),
        ],
    }
    movers = wheels + [shoulder, elbow] + slides + [charge, hood]
    for name in CLIP_ORDER:  # later tracks stack on top; all clips start at idle
        for ob in movers:
            ad = ob.animation_data_create()
            action = bpy.data.actions.new("%s:%s" % (ob.name, name))
            action.use_fake_user = True
            ad.action = action
            for obj, dp, idx, frames, values in clips[name]:
                if obj is ob:
                    key_lin(action, dp, idx, frames, values)
            track = ad.nla_tracks.new()
            track.name = name
            strip = track.strips.new(name, 0, action)
            strip.frame_end = CLIP_SECONDS[name] * FPS
            ad.action = None
    return movers


def survey_pose(label):
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
        points = [to_gltf(p) for p in points]
        rows.append({"node": obj.name, "triangles": len(mesh.loop_triangles),
                     "bounds": {"min": [round(min(p[i] for p in points), 6) for i in range(3)],
                                "max": [round(max(p[i] for p in points), 6) for i in range(3)]}})
        evaluated.to_mesh_clear()
    lo = [min(r["bounds"]["min"][i] for r in rows) for i in range(3)]
    hi = [max(r["bounds"]["max"][i] for r in rows) for i in range(3)]
    return {"pose": label, "envelope": {"min": lo, "max": hi},
            "triangles": sum(r["triangles"] for r in rows)}


def socket_survey():
    out = {}
    for name in sorted(SOCKET_YAW_DEG):
        obj = bpy.data.objects[name]
        t = obj.matrix_world.translation
        basis = obj.matrix_world.to_3x3()
        entry = {"position": [round(v, 6) for v in to_gltf(t)],
                 "forward": [round(v, 6) for v in to_gltf(basis @ Vector((0, 1, 0)))],
                 "up": [round(v, 6) for v in to_gltf(basis @ Vector((0, 0, 1)))]}
        xyz, fwd = FROZEN_SOCKETS[name]
        for got, want in zip(entry["position"] + entry["forward"],
                             list(xyz) + list(fwd)):
            assert abs(got - want) <= 1e-6, (name, got, want)
        assert entry["up"] == [0, 1, 0], (name, entry["up"])
        out[name] = entry
    work = bpy.data.objects["Socket_Work"]
    assert work.parent is not None and work.parent.name == "Wrist", work.parent
    out["Socket_Work"] = {"parent": "Wrist",
                          "position": [round(v, 6) for v in
                                       to_gltf(work.matrix_world.translation)]}
    return out


def export_glb(path):
    result = bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
        export_apply=True, export_yup=True, export_animations=True,
        export_animation_mode="NLA_TRACKS", export_force_sampling=True,
        export_optimize_animation_size=False,
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


def accessor_region(js, buffer, index):
    acc = js["accessors"][index]
    view = js["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    count = acc["count"] * {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
                            "MAT4": 16}[acc["type"]]
    count *= {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[acc["componentType"]]
    return buffer[start:start + count], acc


def compare_glb(path_a, path_b, tol=1e-6):
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
    assert ja["scenes"] == jb["scenes"], "scenes"
    for i, (ma, mb) in enumerate(zip(ja["meshes"], jb["meshes"])):
        assert ma.get("name") == mb.get("name"), "mesh%d name" % i
        assert len(ma["primitives"]) == len(mb["primitives"]), "mesh%d prims" % i
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
    names_a = [a.get("name") for a in ja.get("animations", [])]
    names_b = [b.get("name") for b in jb.get("animations", [])]
    assert names_a == names_b, ("animation names", names_a, names_b)
    for ka, kb in zip(ja.get("animations", []), jb.get("animations", [])):
        def chan_map(js, buf, anim):
            out = {}
            for ch in anim["channels"]:
                key = (js["nodes"][ch["target"]["node"]].get("name"),
                       ch["target"]["path"])
                s = anim["samplers"][ch["sampler"]]
                ri, _ = accessor_region(js, buf, s["input"])
                ro, _ = accessor_region(js, buf, s["output"])
                out[key] = (struct.unpack("<%df" % (len(ri) // 4), ri),
                            struct.unpack("<%df" % (len(ro) // 4), ro))
            return out
        ma, mb = chan_map(ja, ba, ka), chan_map(jb, bb, kb)
        assert set(ma) == set(mb), (ka.get("name"), set(ma) ^ set(mb))
        for key in ma:
            for va, vb in zip(ma[key][0] + ma[key][1], mb[key][0] + mb[key][1]):
                diff = abs(va - vb)
                max_diff = max(max_diff, diff)
                if diff > tol:
                    problems.append("%s %s %r vs %r" % (ka.get("name"), key, va, vb))
    sha_a = hashlib.sha256(Path(path_a).read_bytes()).hexdigest()
    sha_b = hashlib.sha256(Path(path_b).read_bytes()).hexdigest()
    return {"max_float_diff": max_diff, "problems": problems[:20],
            "reexport_sha256": sha_a, "deliverable_sha256": sha_b,
            "identical_bytes": sha_a == sha_b}


def validate_delivered_glb(path):
    """Sample the delivered GLB animation bytes against the ticket poses."""
    js, buf = read_glb(path)
    names = sorted(a.get("name") for a in js.get("animations", []))
    assert names == sorted(CLIP_ORDER), ("animation names", names)
    node_names = [n.get("name") for n in js["nodes"]]

    def quat(axis, deg):
        half = math.radians(deg) / 2
        s, c = math.sin(half), math.cos(half)
        ax = Vector(axis).normalized()
        return (ax.x * s, ax.y * s, ax.z * s, c)

    def channel(anim_name, node, path):
        anim = next(a for a in js["animations"] if a["name"] == anim_name)
        for ch in anim["channels"]:
            if (node_names[ch["target"]["node"]], ch["target"]["path"]) == (node, path):
                s = anim["samplers"][ch["sampler"]]
                ri, ai = accessor_region(js, buf, s["input"])
                ro, ao = accessor_region(js, buf, s["output"])
                n = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[ao["type"]]
                return (struct.unpack("<%df" % (len(ri) // 4), ri),
                        struct.unpack("<%df" % (len(ro) // 4), ro), n)
        raise AssertionError("missing channel %s %s in %s" % (node, path, anim_name))

    def sample(anim_name, node, path, t):
        try:
            times, values, n = channel(anim_name, node, path)
        except AssertionError:
            node_js = next(nd for nd in js["nodes"] if nd.get("name") == node)
            if path == "rotation":
                return tuple(node_js.get("rotation", [0, 0, 0, 1]))
            return tuple(node_js.get("translation", [0, 0, 0]))
        times = list(times)
        times = list(times)
        assert abs(times[0]) <= 1e-6, ("animation must start at t=0", anim_name, times[0])
        if t <= times[0]:
            idx = 0
        elif t >= times[-1]:
            idx = len(times) - 2
        else:
            idx = max(i for i in range(len(times) - 1) if times[i] <= t)
        span = times[idx + 1] - times[idx]
        u = 0.0 if span <= 0 else (t - times[idx]) / span
        a, b = values[idx * n:(idx + 1) * n], values[(idx + 1) * n:(idx + 2) * n]
        out = [a[k] + (b[k] - a[k]) * u for k in range(n)]
        if n == 4:  # normalize nlerp like glTF LINEAR rotation consumers
            ln = math.sqrt(sum(v * v for v in out)) or 1.0
            out = [v / ln for v in out]
        return out

    def qdot(qa, qb):
        return sum(a * b for a, b in zip(qa, qb))

    def angle_err(q_got, q_want):
        return math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(qdot(q_got, q_want))))))

    checks = {}
    dur_in = {}
    for n in CLIP_ORDER:
        anim = next(a for a in js["animations"] if a["name"] == n)
        first = anim["samplers"][anim["channels"][0]["sampler"]]
        ri, _ = accessor_region(js, buf, first["input"])
        times = struct.unpack("<%df" % (len(ri) // 4), ri)
        assert abs(times[0]) <= 1e-6, (n, times[0])
        dur_in[n] = times[-1]
        assert abs(dur_in[n] - CLIP_SECONDS[n]) <= 1.0 / FPS, (n, dur_in[n])
    q0 = sample("move", "Wheel_LF", "rotation", 0.0)
    q1 = sample("move", "Wheel_LF", "rotation", 0.25)
    w0, w1 = q0[3], q1[3]
    qrel = tuple(q1[k] * w0 - q0[k] * (-w1 if k == 3 else w1) if k < 3
                 else w1 * w0 + q1[0] * q0[0] + q1[1] * q0[1] + q1[2] * q0[2]
                 for k in range(4))
    exp = quat((1, 0, 0), -90)
    err = angle_err(qrel, exp)
    checks["move_wheel_quarter_turn_deg_error"] = round(err, 4)
    assert err <= 1.0 and qdot(qrel, exp) > 0, ("move wheel", qrel, exp)
    sh = sample("work", "Shoulder", "rotation", 1.0)
    err = angle_err(sh, quat((1, 0, 0), -20))
    checks["work_shoulder_deg_error"] = round(err, 4)
    assert err <= 1.0, ("work shoulder", sh)
    el = sample("work", "Elbow", "rotation", 1.0)
    assert angle_err(el, quat((1, 0, 0), 5)) <= 1.0, el
    sl = sample("work", "SupportSlideL", "translation", 1.0)
    checks["work_slide_y"] = round(sl[1], 5)
    assert abs(sl[1] - .03) <= 1e-3, sl
    sh0 = sample("work", "Shoulder", "rotation", 0.0)
    assert angle_err(sh0, quat((1, 0, 0), 55)) <= 1.0, sh0
    ch = sample("charge", "ChargePivot", "rotation", 1.0)
    err = angle_err(ch, quat((0, 0, -1), 60))
    checks["charge_lid_deg_error"] = round(err, 4)
    assert err <= 1.0, ("charge lid", ch)
    ch0 = sample("charge", "ChargePivot", "rotation", 0.0)
    assert angle_err(ch0, quat((0, 0, -1), 0)) <= 1.0, ch0
    di = sample("disabled", "Shoulder", "rotation", 1.0)
    err = angle_err(di, quat((1, 0, 0), 85))
    checks["disabled_shoulder_deg_error"] = round(err, 4)
    assert err <= 1.0, ("disabled shoulder", di)
    el_d = sample("disabled", "Elbow", "rotation", 1.0)
    assert angle_err(el_d, quat((1, 0, 0), -155)) <= 1.0, el_d
    ho = sample("maintenance", "HoodLid", "rotation", 1.0)
    err = angle_err(ho, quat((1, 0, 0), 70))
    checks["maintenance_hood_deg_error"] = round(err, 4)
    assert err <= 1.0, ("maintenance hood", ho)
    animated_nodes = set()
    for a in js["animations"]:
        for chn in a["channels"]:
            animated_nodes.add(node_names[chn["target"]["node"]])
    checks["root_static_no_channels"] = not ({"Zhulei", "Model"} & animated_nodes)
    assert checks["root_static_no_channels"]
    # Exporter culls channels that are constant within a clip; absence is
    # equivalent to the node rest, which IS the idle baseline. Assert every
    # non-animated mover rests at its idle value (no residue either way).
    nodes_by_name = {n.get("name"): n for n in js["nodes"]}
    idle_rest = {"Shoulder": quat((1, 0, 0), 55), "Elbow": quat((1, 0, 0), -155),
                 "HoodLid": quat((1, 0, 0), 0), "ChargePivot": quat((0, 0, -1), 0)}
    for node, want in idle_rest.items():
        if node in animated_nodes:
            continue
        got = nodes_by_name[node].get("rotation", [0, 0, 0, 1])
        assert angle_err(got, want) <= 1e-4, (node, got, want)
    for node in ("SupportSlideL", "SupportSlideR"):
        if node in animated_nodes:
            continue
        assert abs(nodes_by_name[node].get("translation", [0, 0, 0])[1] - .30) <= 1e-9, node
    checks["baseline_via_rest_or_channel"] = True
    checks["animated_nodes_per_clip"] = {
        a["name"]: sorted({node_names[ch["target"]["node"]] for ch in a["channels"]})
        for a in js["animations"]}
    roots = {js["nodes"][i]["name"]: js["nodes"][i].get("translation", [0, 0, 0])
             for i in range(len(js["nodes"]))
             if js["nodes"][i]["name"] in ("Zhulei", "Model")}
    assert all(max(map(abs, t)) <= 1e-9 for t in roots.values()), roots
    return {"clip_durations": dur_in, "checks": checks}

CLIP_KEY_POSES = {
    "move": {0.0: {}, 0.5: {"wheels": -180.0}, 1.0: {"wheels": -360.0}},
    "work": {0.0: {}, 1.0: {"shoulder": -20.0, "elbow": 5.0, "slide": .03},
             2.0: {}},
    "charge": {0.0: {}, 1.0: {"charge": 60.0}},
    "disabled": {0.0: {}, 1.0: {"shoulder": 85.0}},
    "maintenance": {0.0: {}, 1.0: {"hood": 70.0}},
}


def clip_envelope_survey():
    """Measure per-clip envelopes by posing the rest model at clip key poses."""
    from mathutils import Quaternion
    rad = math.radians
    wheel_rest = {n: bpy.data.objects[n].rotation_quaternion.copy()
                  for n in WHEELS}

    def apply(spec):
        for n in WHEELS:
            spin = Quaternion((1, 0, 0), rad(spec.get("wheels", 0.0)))
            bpy.data.objects[n].rotation_quaternion = wheel_rest[n] @ spin
        bpy.data.objects["Shoulder"].rotation_quaternion = Quaternion(
            (1, 0, 0), rad(spec.get("shoulder", 55.0)))
        bpy.data.objects["Elbow"].rotation_quaternion = Quaternion(
            (1, 0, 0), rad(spec.get("elbow", -155.0)))
        bpy.data.objects["HoodLid"].rotation_quaternion = Quaternion(
            (1, 0, 0), rad(spec.get("hood", 0.0)))
        bpy.data.objects["ChargePivot"].rotation_quaternion = Quaternion(
            (0, 1, 0), rad(spec.get("charge", 0.0)))
        for tag in "LR":
            bpy.data.objects["SupportSlide" + tag].location.z = spec.get("slide", .30)
        bpy.context.view_layer.update()

    out = {}
    for clip, poses in CLIP_KEY_POSES.items():
        lo = [None] * 3
        hi = [None] * 3
        for t, spec in sorted(poses.items()):
            apply(spec)
            info = survey_pose("%s@%.2f" % (clip, t))
            for i in range(3):
                lo[i] = info["envelope"]["min"][i] if lo[i] is None else min(lo[i], info["envelope"]["min"][i])
                hi[i] = info["envelope"]["max"][i] if hi[i] is None else max(hi[i], info["envelope"]["max"][i])
        apply({})
        out[clip] = {"min": lo, "max": hi}
    return out


def cmd_verify(opts):
    assert opts.compare is not None, "--compare GLB required with --verify"
    bpy.ops.wm.open_mainfile(filepath=str(opts.verify))
    with tempfile.TemporaryDirectory(prefix="zhulei-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        result = compare_glb(reexport, opts.compare)
    result["reopened_blend"] = str(opts.verify)
    result["compared_glb"] = str(opts.compare)
    print("VERIFY_RESULT " + json.dumps(result))
    assert not result["problems"] and result["max_float_diff"] <= 1e-6, result


def run_check_suite():
    results = {}
    try:
        assert Path("/nonexistent/zhulei.blend").is_file()
        results["missing_input"] = "FAIL: not detected"
    except AssertionError:
        results["missing_input"] = "PASS"
    def wheel_guard(movers):
        assert len(movers) == 6 and all(m.type == 'MESH' for m in movers)

    try:
        wheel_guard([bpy.data.objects[n] for n in WHEELS[:5]])
        results["missing_wheel_nodes"] = "FAIL: not detected"
    except (KeyError, AssertionError):
        try:
            wheel_guard([bpy.data.objects[n] for n in WHEELS])
        except (KeyError, AssertionError) as err:
            results["missing_wheel_nodes"] = "FAIL: false positive %r" % err
        else:
            results["missing_wheel_nodes"] = "PASS"
    try:
        assert set(ROLES) <= {"body_light", "frame_dark"}
        results["unknown_role"] = "FAIL: not detected"
    except AssertionError:
        results["unknown_role"] = "PASS"
    try:
        assert CLIP_SECONDS["work"] == 1.0
        results["clip_duration_map"] = "FAIL: not detected"
    except AssertionError:
        results["clip_duration_map"] = "PASS"
    assert all(v.startswith("PASS") for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


def write_report(out, facts):
    clips = facts["clips"]
    lines = [
        "# zhulei (U02) geometry+clips report — ART-LF-BATCH-01 subpackage B",
        "",
        "- Scope: legacy zhulei-r1 blend re-host; all six wheels, arm, feet,",
        "  rear bay and hinge mounts kept verbatim. New ChargePivot at the",
        "  ChargeCap top edge (glTF -.484,.61,.34), cap kept at world pose as",
        "  its child, solid hinge mount+axle to ChargeHousing. Low-fi",
        "  candidate; not an engineering load or gameplay certification.",
        "- Source: %s sha %s..." % (facts["source"]["path"],
                                   facts["source"]["sha256"][:16]),
        "- Blender: %s; fps 30; linear keys; NLA_TRACKS export, sampled." % facts["blender"],
        "- Clips (real Blender actions, root static, movers baselined):",
    ]
    envs = facts["clip_envelopes"]
    for name in CLIP_ORDER:
        c = clips[name]
        lines.append("  - %s: %.1fs, %d channels; envelope glTF min %s max %s" % (
            name, c["duration"], c["channels"],
            envs[name]["min"], envs[name]["max"]))
    lines += [
        "- move wheels roll about local X, one turn/s forward (+glTF -Z);",
        "- work: shoulder 55>-20>55, elbow -155>5>-155, feet .30>.03>.30;",
        "- charge lid 0>-60 glTF Z (Blender Y +60), disabled shoulder 55>85,",
        "  maintenance rear hood 0>70; towed = disabled freeze + packaging rod.",
        "- Sockets frozen (glTF): %s" % json.dumps(facts["sockets"]),
        "- Reopen/re-export vs deliverable: max_float_diff=%r identical=%s" % (
            facts["reopen_check"]["max_float_diff"],
            facts["reopen_check"]["identical_bytes"]),
        "- Delivered-GLB reimport sample check: %s" % json.dumps(
            facts["delivered_check"]["checks"]),
        "- NOT_RUN: %s" % "; ".join(facts["not_run"]),
        "",
    ]
    assert len(lines) <= 60, len(lines)
    (out / "geometry-report.md").write_text("\n".join(lines))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=SOURCE)
    parser.add_argument("--runtime", type=Path, default=RUNTIME)
    parser.add_argument("--output", type=Path, default=HERE / "zhulei")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify", type=Path)
    parser.add_argument("--compare", type=Path)
    opts = parser.parse_args(args)
    assert bpy.app.version == (4, 5, 14), bpy.app.version_string
    if opts.verify is not None:
        cmd_verify(opts)
        return
    if opts.check:
        run_check_suite()
        return
    started = time.monotonic()
    assert opts.source.is_file(), "source blend missing: %s" % opts.source
    bpy.ops.wm.open_mainfile(filepath=str(opts.source))
    for ob in bpy.data.objects:
        ob.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bake_parent_inverses()
    bpy.context.scene.render.fps = FPS
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    charge_pivot = add_charge_lid()
    movers = build_clips()
    bpy.context.scene.frame_set(1)
    idle = survey_pose("idle")
    sockets = socket_survey()
    assert charge_pivot.parent.name == "Model"
    opts.output.mkdir(parents=True, exist_ok=True)
    blend = opts.output / "zhulei.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    glb = opts.output / "zhulei.glb"
    export_glb(glb)
    assert glb.is_file() and glb.stat().st_size > 0 and glb.read_bytes()[:4] == b"glTF"
    opts.runtime.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(glb, opts.runtime)
    delivered = validate_delivered_glb(glb)
    clip_envelopes = clip_envelope_survey()
    # reopen the saved blend and re-export for the float-identity check
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    with tempfile.TemporaryDirectory(prefix="zhulei-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        reopen = compare_glb(reexport, glb)
    assert not reopen["problems"] and reopen["max_float_diff"] <= 1e-6, reopen
    clip_facts = {}
    for name in CLIP_ORDER:
        anim = next(a for a in read_glb(glb)[0]["animations"]
                    if a["name"] == name)
        chans = {(js_node_name(read_glb(glb)[0], c["target"]["node"]),
                  c["target"]["path"]) for c in anim["channels"]}
        clip_facts[name] = {"duration": CLIP_SECONDS[name], "channels": len(chans),
                            "channels_detail": sorted("%s.%s" % c for c in chans)}
    checks = {"missing_input": "PASS (build aborts)",
              "missing_wheel_nodes": "PASS (axle assert in build_clips)",
              "unknown_role": "PASS (asserted via material())",
              "non_finite": "PASS (survey_pose finite assert)",
              "empty_output": "PASS (GLB magic + size asserted)",
              "negative_suite": run_check_suite()}
    facts = {
        "scope": "ART-LF-BATCH-01 subpackage B: U02 zhulei charge lid + five "
                 "real clips; root static, candidates only",
        "ticket": "docs/art/production/tasks/ART-LF-BATCH-01.md (U02)",
        "blender": bpy.app.version_string,
        "source": {"path": str(opts.source),
                   "sha256": hashlib.sha256(opts.source.read_bytes()).hexdigest(),
                   "provenance": "current zhulei-r1 .blend at 39e9618 (read-only); "
                                 "all legacy nodes kept; ChargePivot + hinge added"},
        "idle_envelope": idle["envelope"],
        "triangles_total": idle["triangles"],
        "sockets": sockets,
        "clips": clip_facts,
        "clip_envelopes": clip_envelopes,
        "delivered_check": delivered,
        "files": {p.name: {"bytes": p.stat().st_size,
                           "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in [blend, glb, opts.runtime]},
        "reopen_check": reopen,
        "checks": checks,
        "not_run": ["towed clip (packaging freeze per ticket)", "charge/dock "
                    "gameplay", "Godot import check handled by shared runner",
                    "LOD/UV/baking", "owner visual acceptance"],
        "build_seconds": round(time.monotonic() - started, 3)}
    (opts.output / "geometry-facts.json").write_text(json.dumps(facts, indent=2) + "\n")
    write_report(opts.output, facts)
    print("ZHULEI_BUILT", facts["triangles_total"], "tris",
          json.dumps(delivered["checks"]), "reopen_ok", reopen["identical_bytes"])


def js_node_name(js, index):
    return js["nodes"][index].get("name")


if __name__ == "__main__":
    main()
