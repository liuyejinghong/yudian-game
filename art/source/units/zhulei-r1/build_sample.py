"""ART-U02-GEO zhulei-r1 frozen GEO first sample builder.

Run with the repo Blender, background + factory startup (--python-exit-code 1
makes script failures set a nonzero exit code):
  tools-bin/Blender-4.5.14.app/Contents/MacOS/Blender --background \
      --factory-startup --python-exit-code 1 \
      --python art/source/units/zhulei-r1/build_sample.py -- \
      [--output DIR] [--check] [--verify FILE.blend --compare FILE.glb]

The active U01 source GLB is read-only. Wheels/Rocker/Bogie subtrees and their
ancestor empties are kept verbatim; old body meshes, old interfaces and the
original animations are removed. Geometry, poses, sockets and the envelope are
frozen by docs/art/production/tasks/ART-U02-GEO.md; this script only
materializes them. Static snapshots only: no animation data is written.
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
SOURCE = ROOT / "art/source/units/tuoyun-r1/tuoyun-r1.glb"
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm"}
KEEP_NODES = {"Wheels", "Rocker_L", "Rocker_R", "Bogie_L", "Bogie_R",
              "Wheel_LF", "Wheel_LM", "Wheel_LR",
              "Wheel_RF", "Wheel_RM", "Wheel_RR"}
DROP_NODES = {"Chassis", "Head", "Body", "ProtectedElectronics", "HoodLid",
              "RearLock", "ChargeCap", "CargoBox", "TowRod",
              "Socket_Cargo", "Socket_TowFront", "Socket_TowRear",
              "Socket_Charge"}
NEW_NODE_PROBES = ("FrameBed", "BodyShell", "HoodLidPanel", "ShoulderAxle",
                   "UpperArm", "LowerArm", "WorkPad", "SupportBeamL",
                   "SupportSleeveL_Cap", "SupportSlideL_Rod", "ChargeHousing",
                   "TowFrontEar", "TowRearMount")
SOCKET_YAW_DEG = {"Socket_TowFront": 0.0, "Socket_TowRear": 180.0,
                  "Socket_Charge": 90.0, "Socket_Service": 180.0,
                  "Socket_Work": 0.0}
FROZEN_SOCKETS = {"Socket_TowFront": ((0, .24, -.80), (0, 0, -1)),
                  "Socket_TowRear": ((0, .24, .80), (0, 0, 1)),
                  "Socket_Charge": ((-.48, .55, .34), (-1, 0, 0)),
                  "Socket_Service": ((0, .69, .695), (0, 0, 1))}
POSES = {"idle": dict(shoulder=55.0, elbow=-155.0, lid=0.0, foot_y=0.30),
         "work": dict(shoulder=-20.0, elbow=5.0, lid=0.0, foot_y=0.03),
         "maintenance": dict(shoulder=55.0, elbow=-155.0, lid=70.0, foot_y=0.30)}
ENVELOPE = {"idle": ((-.90, 0.0, -.84), (.90, 1.14, .84)),
            "work": ((-.90, 0.0, -1.60), (.90, 1.52, .84)),
            "maintenance": ((-.90, 0.0, -1.60), (.90, 1.52, .84))}


def world_position(x, y, z):
    # Blender's glTF importer maps +Y up / -Z forward to +Z up / +Y forward.
    return Vector((x, -z, y))


def to_gltf(v):
    return (v.x, v.z, -v.y)


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
    assert role in ROLES, role
    mat = bpy.data.materials.get(role)
    assert mat is not None, "missing material " + role
    return mat


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


def pivot(name, xyz, parent, display=.04):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = display
    obj.location = world_position(*xyz)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def import_source(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    result = bpy.ops.import_scene.gltf(filepath=str(path))
    assert result == {"FINISHED"}, result
    for obj in bpy.data.objects:
        obj.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bpy.context.scene.frame_set(0)


def strip_imported():
    objects = bpy.data.objects
    missing = sorted(n for n in KEEP_NODES | {"tuoyun-r1", "Model"}
                     if objects.get(n) is None)
    assert not missing, "missing wheel/ancestor nodes: " + ", ".join(missing)
    dropped = []
    for name in sorted(DROP_NODES):
        obj = objects.get(name)
        assert obj is not None, "expected imported node " + name
        bpy.data.objects.remove(obj, do_unlink=True)
        dropped.append(name)
    bpy.data.orphans_purge(do_recursive=True)
    assert not any("Cargo" in o.name for o in bpy.data.objects), "Cargo left"
    objects["tuoyun-r1"].name = "Zhulei"
    return dropped


def build_parts():
    model = bpy.data.objects["Model"]
    panel("FrameBed", (0, .37, .10), (.90, .14, 1.25), model, "frame_dark")
    panel("BodyShell", (0, .48, .10), (.90, .08, 1.22), model, "body_light")
    panel("RearFloor", (0, .57, .34), (.74, .10, .70), model, "frame_dark")
    panel("RearWallL", (-.34, .69, .34), (.06, .14, .70), model, "body_light")
    panel("RearWallR", (.34, .69, .34), (.06, .14, .70), model, "body_light")
    panel("RearWallFront", (0, .69, .02), (.62, .14, .06), model, "body_light")
    panel("RearWallBack", (0, .69, .66), (.62, .14, .06), model, "body_light")
    panel("ProtectedModule", (0, .68, .34), (.54, .12, .46), model, "frame_dark")
    panel("ShoulderMount", (0, .56, -.43), (.38, .08, .22), model, "frame_dark")
    hood = pivot("HoodLid", (0, .80, .68), model)
    panel("HoodLidPanel", (0, .80, .33), (.74, .03, .70), hood, "body_light")
    panel("HoodHingeMount", (0, .78, .68), (.28, .04, .06), model, "frame_dark")
    axle("HoodHingeAxle", (0, .80, .68), .022, .36, model)
    shoulder = pivot("Shoulder", (0, .62, -.45), model)
    axle("ShoulderAxle", (0, .62, -.45), .09, .34, shoulder)
    panel("UpperArm", (-.09, .62, -.70), (.10, .10, .50), shoulder, "body_light")
    elbow = pivot("Elbow", (0, .62, -.95), shoulder)
    axle("ElbowAxle", (0, .62, -.95), .075, .36, elbow)
    panel("LowerArm", (.09, .62, -1.15), (.10, .10, .40), elbow, "body_light")
    wrist = pivot("Wrist", (0, .62, -1.35), elbow)
    axle("WristAxle", (0, .62, -1.35), .06, .34, wrist)
    panel("WorkPad", (0, .62, -1.43), (.34, .10, .16), wrist, "frame_dark")
    panel("WorkPadMark", (0, .672, -1.43), (.26, .004, .10), wrist, "accent_warm")
    for side, tag in ((-1., "L"), (1., "R")):
        panel("SupportBeam" + tag, (side * .585, .48, -.40), (.31, .10, .10),
              model, "frame_dark")
        sleeve = pivot("SupportSleeve" + tag, (side * .78, .57, -.40), model)
        for off, end in ((.034, "Pos"), (-.034, "Neg")):
            panel("SupportSleeve" + tag + "_WallX" + end,
                  (side * .78 + off, .57, -.40), (.012, .42, .08), sleeve,
                  "body_light")
            panel("SupportSleeve" + tag + "_WallZ" + end,
                  (side * .78, .57, -.40 + off), (.056, .42, .012), sleeve,
                  "body_light")
        panel("SupportSleeve" + tag + "_Cap", (side * .78, .795, -.40),
              (.08, .03, .08), sleeve, "frame_dark")
        slide = pivot("SupportSlide" + tag, (side * .78, .30, -.40), model)
        panel("SupportSlide" + tag + "_Rod", (side * .78, .53, -.40),
              (.045, .40, .045), slide, "frame_dark")
        panel("SupportSlide" + tag + "_Pad", (side * .78, .30, -.40),
              (.18, .06, .22), slide, "accent_warm")
    panel("ChargeHousing", (-.455, .55, .34), (.05, .16, .16), model, "frame_dark")
    panel("ChargeCap", (-.484, .55, .34), (.008, .12, .12), model, "accent_warm")
    panel("TowFrontMount", (0, .28, -.605), (.18, .12, .23), model, "frame_dark")
    panel("TowFrontEar", (0, .24, -.76), (.20, .06, .08), model, "accent_warm")
    panel("TowRearMount", (0, .28, .715), (.18, .12, .17), model, "frame_dark")
    for name, (xyz, _fwd) in FROZEN_SOCKETS.items():
        socket = pivot(name, xyz, model, .05)
        socket.rotation_euler = (0, 0, math.radians(SOCKET_YAW_DEG[name]))
    pivot("Socket_Work", (0, .62, -1.51), wrist, .05)


def set_pose(pose):
    bpy.data.objects["Shoulder"].rotation_euler = (math.radians(pose["shoulder"]), 0, 0)
    bpy.data.objects["Elbow"].rotation_euler = (math.radians(pose["elbow"]), 0, 0)
    bpy.data.objects["HoodLid"].rotation_euler = (math.radians(pose["lid"]), 0, 0)
    for tag in "LR":
        # Blender +Z carries glTF +Y: slide the foot pivots along world Y.
        bpy.data.objects["SupportSlide" + tag].location.z = pose["foot_y"]


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
        assert points and all(math.isfinite(c) for v in points for c in v), obj.name
        roles = sorted({m.name for m in mesh.materials if m})
        assert roles and set(roles) <= ROLES, (obj.name, roles)
        points = [to_gltf(p) for p in points]
        rows.append({"node": obj.name, "triangles": len(mesh.loop_triangles),
                     "roles": roles,
                     "bounds": {"min": [round(min(p[i] for p in points), 6) for i in range(3)],
                                "max": [round(max(p[i] for p in points), 6) for i in range(3)]}})
        evaluated.to_mesh_clear()
    wheels = [r for r in rows if r["node"].startswith("Wheel_")]
    assert len(wheels) == 6, [r["node"] for r in wheels]
    for name in NEW_NODE_PROBES:
        assert any(r["node"] == name for r in rows), "missing " + name
    return rows


def socket_survey(check_frozen):
    out = {}
    for name in sorted(SOCKET_YAW_DEG):
        obj = bpy.data.objects[name]
        assert obj is not None, name
        t = obj.matrix_world.translation
        basis = obj.matrix_world.to_3x3()
        entry = {"position": [round(v, 6) for v in to_gltf(t)],
                 # glTF -Z forward / +Y up map to Blender +Y / +Z.
                 "forward": [round(v, 6) for v in to_gltf(basis @ Vector((0, 1, 0)))],
                 "up": [round(v, 6) for v in to_gltf(basis @ Vector((0, 0, 1)))]}
        if check_frozen and name in FROZEN_SOCKETS:
            xyz, fwd = FROZEN_SOCKETS[name]
            for got, want in zip(entry["position"] + entry["forward"],
                                 list(xyz) + list(fwd)):
                assert abs(got - want) <= 1e-6, (name, got, want)
            assert entry["up"] == [0, 1, 0], (name, entry["up"])
        out[name] = entry
    return out


def envelope(rows):
    lo = [min(r["bounds"]["min"][i] for r in rows) for i in range(3)]
    hi = [max(r["bounds"]["max"][i] for r in rows) for i in range(3)]
    return lo, hi


def hierarchy_snapshot():
    def kids(name):
        return sorted(c.name for c in bpy.data.objects[name].children)
    return {"Zhulei": kids("Zhulei"), "Model": kids("Model")}


def export_glb(path):
    result = bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
        export_apply=True, export_yup=True, export_animations=False,
        export_materials="EXPORT", export_normals=True, export_texcoords=True,
        export_cameras=False, export_lights=False, export_extras=True)
    assert result == {"FINISHED"}, result


def check_source_exists(path):
    if not path.is_file():
        raise FileNotFoundError("source GLB missing: " + str(path))


def check_rows(rows):
    wheels = [r for r in rows if r["node"].startswith("Wheel_")]
    assert len(wheels) == 6, "expected six Wheel_ nodes, got %d" % len(wheels)
    for r in rows:
        assert r["roles"] and set(r["roles"]) <= ROLES, (r["node"], r["roles"])
        for key in ("min", "max"):
            assert all(math.isfinite(c) for c in r["bounds"][key]), (r["node"], key)


def check_outputs(paths):
    for path in paths:
        assert path.is_file() and path.stat().st_size > 0, path
        assert path.read_bytes()[:4] == b"glTF", path


def fake_rows(role="frame_dark", finite=True):
    y_max = .1 if finite else float("nan")
    def row(name):
        return {"node": name, "roles": [role], "triangles": 2,
                "bounds": {"min": [0.0, 0.0, 0.0], "max": [.1, y_max, .1]}}
    return ([row("Wheel_" + t) for t in ("LF", "LM", "LR", "RF", "RM", "RR")]
            + [row("FrameBed")])


def run_check_suite():
    results = {}
    try:
        check_source_exists(Path("/nonexistent/zhulei-missing.glb"))
        results["missing_input"] = "FAIL: not detected"
    except FileNotFoundError:
        results["missing_input"] = "PASS"
    try:
        check_rows(fake_rows()[:1])
        results["missing_wheel_nodes"] = "FAIL: not detected"
    except AssertionError:
        results["missing_wheel_nodes"] = "PASS"
    try:
        check_rows(fake_rows(role="cargo_plastic"))
        results["unknown_role"] = "FAIL: not detected"
    except AssertionError:
        results["unknown_role"] = "PASS"
    try:
        check_rows(fake_rows(finite=False))
        results["non_finite"] = "FAIL: not detected"
    except AssertionError:
        results["non_finite"] = "PASS"
    try:
        check_outputs([Path("/nonexistent/zhulei-empty.glb")])
        results["empty_output"] = "FAIL: not detected"
    except AssertionError:
        results["empty_output"] = "PASS"
    assert all(v == "PASS" for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


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
    for key in ("scene", "scenes", "nodes", "meshes", "materials",
                "accessors", "bufferViews", "buffers"):
        assert (key in ja) == (key in jb), key
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
    assert ja["asset"]["version"] == jb["asset"]["version"], "asset version"
    assert len(ja["meshes"]) == len(jb["meshes"]), "mesh count"
    for i, (ma, mb) in enumerate(zip(ja["meshes"], jb["meshes"])):
        assert ma.get("name") == mb.get("name"), "mesh%d name" % i
        assert len(ma["primitives"]) == len(mb["primitives"]), "mesh%d prims" % i
        for pa, pb in zip(ma["primitives"], mb["primitives"]):
            assert pa.get("attributes") == pb.get("attributes"), "mesh%d attrs" % i
            assert pa.get("indices") == pb.get("indices"), "mesh%d indices acc" % i
            assert pa.get("material") == pb.get("material"), "mesh%d material" % i
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
        for key in ("min", "max"):
            if key in aa or key in ab:
                assert key in aa and key in ab, "acc%d %s" % (i, key)
                for j, (x, y) in enumerate(zip(aa[key], ab[key])):
                    diff = abs(x - y)
                    max_diff = max(max_diff, diff)
                    if diff > tol:
                        problems.append("acc%d %s[%d] %r vs %r" % (i, key, j, x, y))
    assert [m.get("name") for m in ja["materials"]] == \
           [m.get("name") for m in jb["materials"]], "material names"
    assert ja["buffers"][0]["byteLength"] == jb["buffers"][0]["byteLength"], "buffer size"
    sha_a = hashlib.sha256(Path(path_a).read_bytes()).hexdigest()
    sha_b = hashlib.sha256(Path(path_b).read_bytes()).hexdigest()
    return {"max_float_diff": max_diff, "problems": problems[:20],
            "reexport_sha256": sha_a, "deliverable_sha256": sha_b,
            "identical_bytes": sha_a == sha_b}


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


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=SOURCE)
    parser.add_argument("--output", type=Path, default=HERE)
    parser.add_argument("--check", action="store_true",
                        help="run the negative check suite only")
    parser.add_argument("--verify", type=Path,
                        help="reopen this .blend and re-export for comparison")
    parser.add_argument("--compare", type=Path,
                        help="deliverable idle GLB to compare against")
    opts = parser.parse_args(args)
    assert bpy.app.version == (4, 5, 14), bpy.app.version_string
    if opts.verify is not None:
        cmd_verify(opts)
        return
    if opts.check:
        run_check_suite()
        return
    started = time.monotonic()
    check_source_exists(opts.source)
    import_source(opts.source)
    dropped = strip_imported()
    build_parts()
    set_pose(POSES["idle"])
    rows = survey()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    opts.output.mkdir(parents=True, exist_ok=True)
    blend = opts.output / "zhulei-r1.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    pose_facts = {}
    glb_paths = {}
    for name in ("idle", "work", "maintenance"):
        if name != "idle":
            set_pose(POSES[name])
        path = opts.output / ("zhulei-r1.glb" if name == "idle"
                              else "zhulei-" + name + ".glb")
        export_glb(path)
        glb_paths[name] = path
        rows_p = survey()
        lo, hi = envelope(rows_p)
        cand = ENVELOPE[name]
        pose_facts[name] = {
            "params": POSES[name],
            "envelope": {"min": lo, "max": hi,
                         "candidate_min": list(cand[0]),
                         "candidate_max": list(cand[1]),
                         "within": all(cand[0][i] - 1e-9 <= lo[i]
                                       and hi[i] <= cand[1][i] + 1e-9
                                       for i in range(3))},
            "sockets": socket_survey(check_frozen=(name == "idle")),
            "triangles": sum(r["triangles"] for r in rows_p)}
        if name != "idle":
            set_pose(POSES["idle"])
    check_outputs(list(glb_paths.values()))
    assert blend.is_file() and blend.stat().st_size > 0, blend
    check_rows(rows)
    checks = {"missing_input": "PASS (build aborts; negative in --check suite)",
              "missing_wheel_nodes": "PASS (six Wheel_ asserted in survey)",
              "unknown_role": "PASS (roles subset asserted in survey)",
              "non_finite": "PASS (finite coords asserted in survey)",
              "empty_output": "PASS (GLB magic + size asserted)",
              "negative_suite": run_check_suite()}
    roles_used = {}
    for r in rows:
        for role in r["roles"]:
            roles_used[role] = roles_used.get(role, 0) + 1
    facts = {
        "scope": "ART-U02-GEO frozen zhulei-r1 GEO first sample; static "
                 "snapshots only, no animation, no seven-state run",
        "ticket": "docs/art/production/tasks/ART-U02-GEO.md",
        "revision": 1,
        "blender": bpy.app.version_string,
        "source": {"path": str(opts.source), "sha256":
                   hashlib.sha256(opts.source.read_bytes()).hexdigest(),
                   "provenance": "active U01 tuoyun-r1 GLB, read-only; "
                                 "wheels/Rocker/Bogie subtree kept verbatim"},
        "dropped_nodes": dropped,
        "hierarchy": hierarchy_snapshot(),
        "triangles_total": sum(r["triangles"] for r in rows),
        "roles_used": roles_used,
        "meshes": rows,
        "poses": pose_facts,
        "files": {p.name: {"bytes": p.stat().st_size,
                           "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in [blend] + list(glb_paths.values())},
        "checks": checks,
        "not_run": ["seven-state animation/reset", "construction/repair/power "
                    "gameplay", "Godot import and socket/pose verification "
                    "(master)", "resampled demo clearances", "LOD/UV/baking",
                    "full-scene performance", "owner visual acceptance"],
        "build_seconds": round(time.monotonic() - started, 3)}
    (opts.output / "geometry-facts.json").write_text(
        json.dumps(facts, indent=2) + "\n")
    print("ZHULEI_GEO_BUILT", facts["triangles_total"], "triangles",
          facts["build_seconds"], "seconds")


if __name__ == "__main__":
    main()
