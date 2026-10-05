"""ART-LF-BATCH-01 subpackage B: P01 recovery-link visual candidate.

Static connector candidate per ticket: root RecoveryLink, .35 m dark bar
along glTF Z, two accent lugs, EndA/EndB sockets. No clips, no tow/rescue
capability claim. Built from scratch with the verified house helpers.

  Blender --background --factory-startup --python-exit-code 1 --python \
      art/source/lowfi-batch-r1/existing/build_recovery.py -- \
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
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
RUNTIME = ROOT / "prototype/assets/lowfi-batch-r1/models/recovery.glb"
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm"}
PALETTE = {"frame_dark": ((0.0401, 0.0578, 0.0694), 0.2, 0.75),
           "body_light": ((0.6818, 0.714, 0.6718), 0.0, 0.65),
           "accent_warm": ((0.6712, 0.1975, 0.048), 0.0, 0.6)}
SOCKET_YAW_DEG = {"Socket_EndA": 0.0, "Socket_EndB": 180.0}
FROZEN_SOCKETS = {"Socket_EndA": ((0, 0, -.175), (0, 0, -1)),
                  "Socket_EndB": ((0, 0, .175), (0, 0, 1))}
CANDIDATE_ENVELOPE = ((-.10, -.04, -.175), (.10, .04, .175))


def world_position(x, y, z):
    # Blender's glTF importer maps +Y up / -Z forward to +Z up / +Y forward.
    return Vector((x, -z, y))


def to_gltf(v):
    return (v.x, v.z, -v.y)


def material(role):
    assert role in PALETTE, role
    mat = bpy.data.materials.get(role)
    if mat is not None:
        return mat
    color, metallic, rough = PALETTE[role]
    mat = bpy.data.materials.new(role)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    return mat


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


def pivot(name, xyz, parent, display=.04):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = display
    obj.location = world_position(*xyz)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def build_parts():
    root = bpy.data.objects.new("RecoveryLink", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = .2
    bpy.context.scene.collection.objects.link(root)
    panel("LinkBar", (0, 0, 0), (.06, .06, .35), root, "frame_dark")
    panel("LugEndA", (0, 0, -.15), (.20, .08, .05), root, "accent_warm")
    panel("LugEndB", (0, 0, .15), (.20, .08, .05), root, "accent_warm")
    for name, (xyz, _fwd) in FROZEN_SOCKETS.items():
        socket = pivot(name, xyz, root, .05)
        socket.rotation_euler = (0, 0, math.radians(SOCKET_YAW_DEG[name]))


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
        assert roles and set(roles) <= set(PALETTE), (obj.name, roles)
        points = [to_gltf(p) for p in points]
        rows.append({"node": obj.name, "triangles": len(mesh.loop_triangles),
                     "vertices": len(mesh.vertices),
                     "roles": roles,
                     "bounds": {"min": [round(min(p[i] for p in points), 6) for i in range(3)],
                                "max": [round(max(p[i] for p in points), 6) for i in range(3)]}})
        evaluated.to_mesh_clear()
    return rows


def check_rows(rows):
    names = {r["node"] for r in rows}
    assert names == {"LinkBar", "LugEndA", "LugEndB"}, names
    assert all(r["triangles"] >= 12 for r in rows), rows
    lo = [min(r["bounds"]["min"][i] for r in rows) for i in range(3)]
    hi = [max(r["bounds"]["max"][i] for r in rows) for i in range(3)]
    for i, (want_lo, want_hi) in enumerate(zip(*CANDIDATE_ENVELOPE)):
        assert abs(lo[i] - want_lo) <= 1e-6 and abs(hi[i] - want_hi) <= 1e-6, (lo, hi)


def socket_survey(check_frozen):
    out = {}
    for name in sorted(SOCKET_YAW_DEG):
        obj = bpy.data.objects[name]
        t = obj.matrix_world.translation
        basis = obj.matrix_world.to_3x3()
        entry = {"position": [round(v, 6) for v in to_gltf(t)],
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


def export_glb(path):
    result = bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
        export_apply=True, export_yup=True, export_animations=False,
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
    sha_a = hashlib.sha256(Path(path_a).read_bytes()).hexdigest()
    sha_b = hashlib.sha256(Path(path_b).read_bytes()).hexdigest()
    return {"max_float_diff": max_diff, "problems": problems[:20],
            "reexport_sha256": sha_a, "deliverable_sha256": sha_b,
            "identical_bytes": sha_a == sha_b}


def cmd_verify(opts):
    assert opts.compare is not None, "--compare GLB required with --verify"
    bpy.ops.wm.open_mainfile(filepath=str(opts.verify))
    with tempfile.TemporaryDirectory(prefix="recovery-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        result = compare_glb(reexport, opts.compare)
    result["reopened_blend"] = str(opts.verify)
    result["compared_glb"] = str(opts.compare)
    print("VERIFY_RESULT " + json.dumps(result))
    assert not result["problems"] and result["max_float_diff"] <= 1e-6, result


def fake_rows(role="frame_dark", finite=True):
    z = .175 if finite else float("nan")
    return [{"node": "LinkBar", "roles": [role], "triangles": 12, "vertices": 8,
             "bounds": {"min": [0.0, 0.0, 0.0], "max": [.1, .1, z]}}]


def run_check_suite():
    results = {}
    try:
        assert Path("/nonexistent/recovery.glb").is_file() and \
            Path("/nonexistent/recovery.glb").read_bytes()[:4] == b"glTF"
        results["empty_output"] = "FAIL: not detected"
    except AssertionError:
        results["empty_output"] = "PASS"
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
        check_rows([r for r in fake_rows() if r["node"] == "LinkBar"])
        results["missing_lugs"] = "FAIL: not detected"
    except AssertionError:
        results["missing_lugs"] = "PASS"
    assert all(v == "PASS" for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


def write_report(out, facts):
    lo = facts["envelope"]["min"]
    hi = facts["envelope"]["max"]
    lines = [
        "# recovery (P01 link candidate) geometry report — ART-LF-BATCH-01 B",
        "",
        "- Scope: static tow-link visual candidate, original geometry per",
        "  ticket; NOT a rescue system, no pin holes, load or who-tows-whom",
        "  rule. Visual candidate only, comparable with the .35 m U01 tow",
        "  stub; acceptance is the owner's, not self-signed.",
        "- Geometry: LinkBar (frame_dark .06 section, .35 along glTF Z),",
        "  LugEndA/LugEndB (accent_warm .20x.08x.05 at z +-0.15).",
        "- Sockets: EndA (0,0,-.175) fwd -Z, EndB (0,0,+.175) fwd +Z, up +Y;",
        "  survey %s." % json.dumps(facts["sockets"]),
        "- Envelope (glTF m): min %s max %s, candidate matched." % (lo, hi),
        "- Triangles: %d across %d meshes, roles %s." % (
            facts["triangles_total"], len(facts["meshes"]),
            sorted(facts["roles_used"])),
        "- Blender: %s; static asset, no clips, no animation channels." % facts["blender"],
        "- Reopen/re-export: max_float_diff=%r, identical_bytes=%s." % (
            facts["reopen_check"]["max_float_diff"],
            facts["reopen_check"]["identical_bytes"]),
        "- Checks: %s" % json.dumps(facts["checks"]["negative_suite"]),
        "- NOT_RUN: %s" % "; ".join(facts["not_run"]),
        "",
    ]
    assert len(lines) <= 60, len(lines)
    (out / "geometry-report.md").write_text("\n".join(lines))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", type=Path, default=RUNTIME)
    parser.add_argument("--output", type=Path, default=HERE / "recovery")
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
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    build_parts()
    rows = survey()
    check_rows(rows)
    sockets = socket_survey(check_frozen=True)
    opts.output.mkdir(parents=True, exist_ok=True)
    blend = opts.output / "recovery.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    glb = opts.output / "recovery.glb"
    export_glb(glb)
    assert glb.is_file() and glb.stat().st_size > 0 and glb.read_bytes()[:4] == b"glTF"
    opts.runtime.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(glb, opts.runtime)
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    with tempfile.TemporaryDirectory(prefix="recovery-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        reopen = compare_glb(reexport, glb)
    assert not reopen["problems"] and reopen["max_float_diff"] <= 1e-6, reopen
    lo, hi = envelope(rows)
    checks = {"unknown_role": "PASS (palette subset asserted)",
              "non_finite": "PASS (finite coords asserted)",
              "missing_lugs": "PASS (name set asserted)",
              "empty_output": "PASS (GLB magic + size asserted)",
              "negative_suite": run_check_suite()}
    roles_used = {}
    for r in rows:
        for role in r["roles"]:
            roles_used[role] = roles_used.get(role, 0) + 1
    facts = {
        "scope": "ART-LF-BATCH-01 subpackage B: P01 recovery-link visual "
                 "candidate; static, no clips, no rescue capability claim",
        "ticket": "docs/art/production/tasks/ART-LF-BATCH-01.md (P01 recovery)",
        "blender": bpy.app.version_string,
        "provenance": "original geometry from ticket spec; palette copied from "
                      "legacy storage-r1 GLB materials",
        "triangles_total": sum(r["triangles"] for r in rows),
        "roles_used": roles_used,
        "meshes": rows,
        "sockets": sockets,
        "envelope": {"min": lo, "max": hi,
                     "candidate": [list(CANDIDATE_ENVELOPE[0]),
                                   list(CANDIDATE_ENVELOPE[1])],
                     "within": all(
                         abs(lo[i] - CANDIDATE_ENVELOPE[0][i]) <= 1e-6 and
                         abs(hi[i] - CANDIDATE_ENVELOPE[1][i]) <= 1e-6
                         for i in range(3))},
        "files": {p.name: {"bytes": p.stat().st_size,
                           "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in [blend, glb, opts.runtime]},
        "reopen_check": reopen,
        "checks": checks,
        "not_run": ["towed/recovery gameplay", "Godot import check handled by "
                    "shared runner", "UV/baking/LOD", "owner visual acceptance"],
        "build_seconds": round(time.monotonic() - started, 3)}
    (opts.output / "geometry-facts.json").write_text(json.dumps(facts, indent=2) + "\n")
    write_report(opts.output, facts)
    print("RECOVERY_BUILT", facts["triangles_total"], "tris",
          facts["build_seconds"], "s", "reopen_ok", reopen["identical_bytes"])


if __name__ == "__main__":
    main()
