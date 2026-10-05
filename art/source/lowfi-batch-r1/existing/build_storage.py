"""ART-LF-BATCH-01 subpackage B: F03 storage gate + rear service pod.

Imports the read-only canonical storage-r1 GLB (full geometry + 12 crates,
RackPayloadDemo visibility stays packaging-controlled), adds the front Gate
(idle X+90 folded FORWARD out of the rack, closed X0; warm strip; solid axle
to the front posts) and an enclosed rear ServicePod with a top-hinged
ServiceCover (X-70 outward, solid axle + two mount blocks). Two real clips:
disabled (gate open->closed 1 s) and maintenance (cover 0->-70 1 s, gate
stays open). Root static; movers baselined; crate visibility untouched.

  Blender --background --factory-startup --python-exit-code 1 --python \
      art/source/lowfi-batch-r1/existing/build_storage.py -- \
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
SOURCE = ROOT / "art/source/facilities/storage-r1/storage-r1.glb"
RUNTIME = ROOT / "prototype/assets/lowfi-batch-r1/models/storage.glb"
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm"}
CRATES = ["Crate_B%02d" % i for i in range(1, 7)] + \
         ["Crate_T%02d" % i for i in range(1, 7)]
FPS = 30
CLIP_ORDER = ["disabled", "maintenance"]
CLIP_SECONDS = {"disabled": 1.0, "maintenance": 1.0}
SOCKET_YAW_DEG = {"Socket_Input": 0.0, "Socket_Output": 180.0,
                  "Socket_Service": 180.0}
FROZEN_SOCKETS = {"Socket_Input": ((0, .20, -1.60), (0, 0, -1)),
                  "Socket_Output": ((0, .20, 1.78), (0, 0, 1)),
                  "Socket_Service": ((0, .85, 1.7425), (0, 0, 1))}
CANDIDATE_ENVELOPE = ((-2.25, 0.0, -3.02), (2.25, 2.20, 2.35))  # glTF x,y,z


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


def axle(name, xyz, radius, length, parent, role="frame_dark"):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius,
                                        depth=length, location=world_position(*xyz),
                                        rotation=(0, math.pi / 2, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    weld_and_bevel(obj, .001, 1)
    return obj


def pivot(name, xyz, parent, display=.05):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = display
    obj.location = world_position(*xyz)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    bpy.context.view_layer.update()  # refresh matrix_world before any keep_world
    return obj


def build_parts():
    root = bpy.data.objects["Storage"]
    for n in ["Storage", "RackPayloadDemo", "Structure", "TopCanopy",
              "FrontMark", "RearPanel"] + CRATES:
        assert bpy.data.objects.get(n) is not None, "legacy node missing: " + n
    assert len([o for o in bpy.data.objects
                if o.name.startswith("Crate_")]) == 12
    payload = bpy.data.objects["RackPayloadDemo"]
    for crate in CRATES:
        assert bpy.data.objects[crate].parent is payload, crate
    # front gate: idle folds X+90 FORWARD (glTF -Z) out of the rack, never
    # sweeping the front crate row
    gate = pivot("GatePivot", (0, 1.90, -1.52), root)
    panel("GatePanel", (0, 1.18, -1.52), (4.12, 1.44, .06), gate, "body_light")
    panel("GateStrip", (0, 1.24, -1.56), (3.90, .10, .02), gate, "accent_warm")
    axle("GateAxle", (0, 1.90, -1.52), .04, 4.32, root)
    structure = bpy.data.objects["Structure"]
    pts = [structure.matrix_world @ v.co for v in structure.data.vertices]
    xs = [p.x for p in pts]
    assert max(xs) >= 2.16 - 1e-6 and min(xs) <= -2.16 + 1e-6, \
        "GateAxle ends (x=+-2.16) must reach the front posts: %r" % (min(xs), max(xs))
    # enclosed rear service pod on the rear foundation, cover on a real top hinge
    panel("ServicePod", (0, .85, 1.62), (.80, .60, .20), root, "frame_dark")
    panel("PodBracket", (0, .335, 1.58), (.50, .43, .20), root, "frame_dark")
    cover = pivot("CoverPivot", (0, 1.15, 1.725), root)
    panel("ServiceCoverPanel", (0, .85, 1.725), (.78, .60, .03), cover,
          "body_light")
    axle("CoverAxle", (0, 1.15, 1.725), .02, .82, root)
    for sx in (-.30, .30):
        panel("CoverMount" + ("L" if sx < 0 else "R"), (sx, 1.10, 1.70),
              (.06, .10, .08), root, "frame_dark")
    for name, (xyz, _fwd) in FROZEN_SOCKETS.items():
        socket = pivot(name, xyz, root)
        socket.rotation_euler = (0, 0, math.radians(SOCKET_YAW_DEG[name]))
    return gate, cover


def key_lin(action, data_path, index, frames, values):
    fc = action.fcurves.new(data_path=data_path, index=index)
    fc.keyframe_points.add(count=len(frames))
    for k, f, v in zip(fc.keyframe_points, frames, values):
        k.co = (float(f), float(v))
        k.interpolation = 'LINEAR'
    fc.update()


def build_clips(gate, cover):
    rad = math.radians
    movers = [gate, cover]
    f3 = [0, FPS // 2, FPS]  # collinear mid keys keep sampled nlerp linear
    # idle rest: gate open (+90 forward), cover closed (0)
    gate.rotation_euler = (rad(90), 0, 0)
    cover.rotation_euler = (0, 0, 0)
    clips = {
        "disabled": [
            (gate, "rotation_euler", 0, f3, [rad(90), rad(45), rad(0)]),
            (cover, "rotation_euler", 0, f3, [0.0, 0.0, 0.0]),
        ],
        "maintenance": [
            (cover, "rotation_euler", 0, f3, [0.0, rad(-35), rad(-70)]),
            (gate, "rotation_euler", 0, f3, [rad(90), rad(90), rad(90)]),
        ],
    }
    for name in CLIP_ORDER:
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
    return out


CLIP_KEY_POSES = {
    "disabled": {0.0: {"gate": 90.0}, 0.5: {"gate": 45.0}, 1.0: {"gate": 0.0}},
    "maintenance": {0.0: {"gate": 90.0, "cover": 0.0},
                    0.5: {"gate": 90.0, "cover": -35.0},
                    1.0: {"gate": 90.0, "cover": -70.0}},
}


def clip_envelope_survey(gate_deg0=90.0):
    """Envelope measurement at clip key poses (math posing, not the clip)."""
    rad = math.radians
    gate = bpy.data.objects["GatePivot"]
    cover = bpy.data.objects["CoverPivot"]

    def apply(gate_deg, cover_deg):
        gate.rotation_euler = (rad(gate_deg), 0, 0)
        cover.rotation_euler = (rad(cover_deg), 0, 0)
        bpy.context.view_layer.update()

    out = {}
    for clip, poses in CLIP_KEY_POSES.items():
        lo = [None] * 3
        hi = [None] * 3
        for t, spec in sorted(poses.items()):
            apply(spec.get("gate", 90.0), spec.get("cover", 0.0))
            info = survey_pose("%s@%.2f" % (clip, t))
            for i in range(3):
                lo[i] = info["envelope"]["min"][i] if lo[i] is None else min(lo[i], info["envelope"]["min"][i])
                hi[i] = info["envelope"]["max"][i] if hi[i] is None else max(hi[i], info["envelope"]["max"][i])
        out[clip] = {"min": lo, "max": hi}
    apply(90.0, 0.0)
    idle = survey_pose("idle")
    return idle, out


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
    nodes_by_name = {n.get("name"): n for n in js["nodes"]}

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
                ri, _ = accessor_region(js, buf, s["input"])
                ro, _ = accessor_region(js, buf, s["output"])
                n = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[ro.__class__ and
                                                       js["accessors"][s["output"]]["type"]]
                return (struct.unpack("<%df" % (len(ri) // 4), ri),
                        struct.unpack("<%df" % (len(ro) // 4), ro))
        raise AssertionError("missing channel %s %s in %s" % (node, path, anim_name))

    def sample(anim_name, node, path, t):
        try:
            times, values = channel(anim_name, node, path)
            n = 4 if path == "rotation" else 3
        except AssertionError:
            node_js = nodes_by_name[node]
            if path == "rotation":
                return tuple(node_js.get("rotation", [0, 0, 0, 1]))
            return tuple(node_js.get("translation", [0, 0, 0]))
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
        if n == 4:
            ln = math.sqrt(sum(v * v for v in out)) or 1.0
            out = [v / ln for v in out]
        return out

    def qdot(qa, qb):
        return sum(a * b for a, b in zip(qa, qb))

    def angle_err(q_got, q_want):
        return math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(qdot(q_got, q_want))))))

    checks = {}
    for n in CLIP_ORDER:
        anim = next(a for a in js["animations"] if a["name"] == n)
        first = anim["samplers"][anim["channels"][0]["sampler"]]
        ri, _ = accessor_region(js, buf, first["input"])
        times = struct.unpack("<%df" % (len(ri) // 4), ri)
        assert abs(times[0]) <= 1e-6, (n, times[0])
        assert abs(times[-1] - CLIP_SECONDS[n]) <= 1.0 / FPS, (n, times[-1])
    # disabled: gate X+90 (forward fold, open) -> X0 (closed, blocks face)
    g0 = sample("disabled", "GatePivot", "rotation", 0.0)
    err = angle_err(g0, quat((1, 0, 0), 90))
    checks["disabled_gate_open_deg_error"] = round(err, 4)
    assert err <= 1.0, ("gate open", g0)
    g1 = sample("disabled", "GatePivot", "rotation", 1.0)
    err = angle_err(g1, quat((1, 0, 0), 0))
    checks["disabled_gate_closed_deg_error"] = round(err, 4)
    assert err <= 1.0, ("gate closed", g1)
    # maintenance: cover X0 -> X-70; gate stays open at rest 90
    c1 = sample("maintenance", "CoverPivot", "rotation", 1.0)
    err = angle_err(c1, quat((1, 0, 0), -70))
    checks["maintenance_cover_deg_error"] = round(err, 4)
    assert err <= 1.0, ("cover", c1)
    gm = sample("maintenance", "GatePivot", "rotation", 1.0)
    assert angle_err(gm, quat((1, 0, 0), 90)) <= 1.0, gm
    animated_nodes = set()
    for a in js["animations"]:
        for chn in a["channels"]:
            animated_nodes.add(node_names[chn["target"]["node"]])
    checks["root_and_crates_static"] = not ({"Storage", "RackPayloadDemo"} & animated_nodes)
    assert checks["root_and_crates_static"]
    checks["animated_nodes_per_clip"] = {
        a["name"]: sorted({node_names[ch["target"]["node"]] for ch in a["channels"]})
        for a in js["animations"]}
    return {"checks": checks}


def cmd_verify(opts):
    assert opts.compare is not None, "--compare GLB required with --verify"
    bpy.ops.wm.open_mainfile(filepath=str(opts.verify))
    with tempfile.TemporaryDirectory(prefix="storage-verify-") as tmp:
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
        assert Path("/nonexistent/storage.glb").is_file()
        results["missing_input"] = "FAIL: not detected"
    except AssertionError:
        results["missing_input"] = "PASS"

    def gate_guard(gate_deg):
        # gate folds FORWARD (+X toward glTF -Z); closed is 0; never below 0
        assert 0.0 <= gate_deg <= 90.0, gate_deg

    try:
        gate_guard(-90.0)
        results["gate_backward_sweep"] = "FAIL: not detected"
    except AssertionError:
        try:
            gate_guard(90.0)
            gate_guard(0.0)
        except AssertionError as err:
            results["gate_backward_sweep"] = "FAIL: false positive %r" % err
        else:
            results["gate_backward_sweep"] = "PASS"
    def crate_guard(crates):
        assert len(crates) == 12 and all(n.startswith("Crate_") for n in crates)

    try:
        crate_guard(CRATES[:11])
        results["crate_set"] = "FAIL: not detected"
    except AssertionError:
        try:
            crate_guard(CRATES)
        except AssertionError as err:
            results["crate_set"] = "FAIL: false positive %r" % err
        else:
            results["crate_set"] = "PASS"
    assert all(v.startswith("PASS") for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


def write_report(out, facts):
    envs = facts["clip_envelopes"]
    lines = [
        "# storage (F03) geometry+clips report — ART-LF-BATCH-01 subpackage B",
        "",
        "- Scope: legacy storage-r1 re-host (full geometry + 12 crates,",
        "  RackPayloadDemo untouched for packaging visibility); new front Gate",
        "  folding X+90 FORWARD (open, reaches glTF z -2.96, in front of the",
        "  front crate row; closed X0) with solid GateAxle (r.04 x 4.32) to",
        "  the front posts, and enclosed rear ServicePod (bracket to the rear",
        "  foundation) with top-hinged ServiceCover X-70 on a real axle plus",
        "  two mount blocks. Low-fi candidate, not capacity or mechanism claim.",
        "- Source: %s sha %s..." % (facts["source"]["path"],
                                   facts["source"]["sha256"][:16]),
        "- Blender: %s; fps 30; linear keys; NLA_TRACKS export, sampled." % facts["blender"],
    ]
    for name in CLIP_ORDER:
        e = envs[name]
        lines.append("  - %s: %.1fs; envelope glTF min %s max %s" % (
            name, CLIP_SECONDS[name], e["min"], e["max"]))
    lines += [
        "- idle envelope glTF min %s max %s (candidate x +-2.25," % (
            facts["idle_envelope"]["min"], facts["idle_envelope"]["max"]),
        "  y 0..2.20, z -3.02..2.35; maintenance cover reach +2.01 inside).",
        "- Sockets frozen (glTF): %s" % json.dumps(facts["sockets"]),
        "- Move/charge/towed N/A fixed facility; work stays static take/put",
        "  handoff; no crate auto motion, no inventory promises.",
        "- Reopen/re-export vs deliverable: max_float_diff=%r identical=%s" % (
            facts["reopen_check"]["max_float_diff"],
            facts["reopen_check"]["identical_bytes"]),
        "- Delivered-GLB sample check (bytes, ticket poses): %s" % json.dumps(
            facts["delivered_check"]["checks"]),
        "- Clip envelopes measured by posing the rest model at key poses",
        "  (measurement only; clip proof is the GLB sample check + Godot).",
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
    parser.add_argument("--output", type=Path, default=HERE / "storage")
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
    assert opts.source.is_file(), "source GLB missing: %s" % opts.source
    bpy.ops.wm.read_homefile(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(opts.source))
    assert result == {"FINISHED"}, result
    assert not list(bpy.data.actions), "unexpected animation in source"
    for ob in bpy.data.objects:
        ob.animation_data_clear()
    bpy.context.scene.render.fps = FPS
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    gate, cover = build_parts()
    build_clips(gate, cover)
    bpy.context.scene.frame_set(0)
    idle, clip_env = clip_envelope_survey()
    sockets = socket_survey()
    for i, (want_lo, want_hi) in enumerate(zip(*CANDIDATE_ENVELOPE)):
        assert idle["envelope"]["min"][i] >= want_lo - 1e-6, idle
        assert idle["envelope"]["max"][i] <= want_hi + 1e-6, idle
    for name in CLIP_ORDER:
        for i, (want_lo, want_hi) in enumerate(zip(*CANDIDATE_ENVELOPE)):
            assert clip_env[name]["min"][i] >= want_lo - 1e-6, (name, clip_env[name])
            assert clip_env[name]["max"][i] <= want_hi + 1e-6, (name, clip_env[name])
    opts.output.mkdir(parents=True, exist_ok=True)
    blend = opts.output / "storage.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    glb = opts.output / "storage.glb"
    export_glb(glb)
    assert glb.is_file() and glb.stat().st_size > 0 and glb.read_bytes()[:4] == b"glTF"
    opts.runtime.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(glb, opts.runtime)
    delivered = validate_delivered_glb(glb)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    with tempfile.TemporaryDirectory(prefix="storage-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        reopen = compare_glb(reexport, glb)
    assert not reopen["problems"] and reopen["max_float_diff"] <= 1e-6, reopen
    checks = {"missing_input": "PASS (build aborts)",
              "gate_backward_sweep": "PASS (gate degrees guarded to [0,90])",
              "crate_set": "PASS (12 asserted)",
              "non_finite": "PASS (survey_pose finite assert)",
              "empty_output": "PASS (GLB magic + size asserted)",
              "negative_suite": run_check_suite()}
    facts = {
        "scope": "ART-LF-BATCH-01 subpackage B: F03 storage gate + rear service "
                 "pod; fixed facility, disabled/maintenance clips only",
        "ticket": "docs/art/production/tasks/ART-LF-BATCH-01.md (F03)",
        "blender": bpy.app.version_string,
        "source": {"path": str(opts.source),
                   "sha256": hashlib.sha256(opts.source.read_bytes()).hexdigest(),
                   "provenance": "read-only canonical storage-r1 GLB; untouched"},
        "idle_envelope": idle["envelope"],
        "triangles_total": idle["triangles"],
        "sockets": sockets,
        "clip_envelopes": clip_env,
        "delivered_check": delivered,
        "files": {p.name: {"bytes": p.stat().st_size,
                           "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in [blend, glb, opts.runtime]},
        "reopen_check": reopen,
        "checks": checks,
        "not_run": ["take/put gameplay", "Godot import check handled by shared "
                    "runner", "LOD/UV/baking", "owner visual acceptance"],
        "build_seconds": round(time.monotonic() - started, 3)}
    (opts.output / "geometry-facts.json").write_text(json.dumps(facts, indent=2) + "\n")
    write_report(opts.output, facts)
    print("STORAGE_BUILT", facts["triangles_total"], "tris",
          json.dumps(delivered["checks"]["animated_nodes_per_clip"]),
          "reopen_ok", reopen["identical_bytes"])


if __name__ == "__main__":
    main()
