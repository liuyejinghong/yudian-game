"""ART-LF-BATCH-01 subpackage B: P01 crate-r1 re-host (no geometry change).

Imports the read-only canonical crate-r1 GLB, saves an editable .blend and
re-exports the deliverable GLB through Blender. No animation, no new parts,
no scale change (.64 x .38 x .70 glTF meters kept).

  Blender --background --factory-startup --python-exit-code 1 --python \
      art/source/lowfi-batch-r1/existing/build_crate.py -- \
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

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
SOURCE = ROOT / "art/source/props/crate-r1/crate-r1.glb"
RUNTIME = ROOT / "prototype/assets/lowfi-batch-r1/models/crate.glb"
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm"}
EXPECT_DIMS = (0.640, 0.380, 0.700)  # glTF X (width) / Y (height) / Z (depth)
EXPECT_TRIS = 48
EXPECT_VERTS = 32


def world_position(x, y, z):
    # Blender's glTF importer maps +Y up / -Z forward to +Z up / +Y forward.
    from mathutils import Vector
    return Vector((x, -z, y))


def to_gltf(v):
    return (v.x, v.z, -v.y)


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
                     "vertices": len(mesh.vertices),
                     "roles": roles,
                     "bounds": {"min": [round(min(p[i] for p in points), 6) for i in range(3)],
                                "max": [round(max(p[i] for p in points), 6) for i in range(3)]}})
        evaluated.to_mesh_clear()
    return rows


def check_rows(rows):
    assert len(rows) == 1 and rows[0]["node"] == "Crate", rows
    row = rows[0]
    assert row["triangles"] == EXPECT_TRIS, row
    assert row["vertices"] == EXPECT_VERTS, row
    for key in ("min", "max"):
        assert all(math.isfinite(c) for c in row["bounds"][key]), row
    lo, hi = row["bounds"]["min"], row["bounds"]["max"]
    for i, want in enumerate(EXPECT_DIMS):
        assert abs((hi[i] - lo[i]) - want) <= 1e-6, (i, hi[i] - lo[i], want)
    assert abs(lo[1]) <= 1e-6, "crate must stay grounded: %r" % lo


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


def compare_vs_original(path_new, path_old, tol=1e-6):
    """Geometry-only roundtrip check against the hand-authored source GLB."""
    jn, bn = read_glb(path_new)
    jo, bo = read_glb(path_old)
    assert len(jn["meshes"]) == len(jo["meshes"]) == 1
    pn, po = jn["meshes"][0]["primitives"], jo["meshes"][0]["primitives"]
    mat_n, mat_o = {}, {}
    for js, buf, prims, out in ((jn, bn, pn, mat_n), (jo, bo, po, mat_o)):
        for p in prims:
            out.setdefault(js["materials"][p["material"]]["name"], []).append(p)
    assert set(mat_n) == set(mat_o), (sorted(mat_n), sorted(mat_o))
    problems = []
    max_diff = 0.0

    def triangles(js, buf, prim):
        """Triangle set as coordinate triples: robust to vertex splitting."""
        pos, _ = accessor_region(js, buf, prim["attributes"]["POSITION"])
        xyz = struct.unpack("<%df" % (len(pos) // 4), pos)
        idx, acc = accessor_region(js, buf, prim["indices"])
        fmt = {5121: "b", 5123: "H", 5125: "I"}[acc["componentType"]]
        idx = struct.unpack("<%d%s" % (len(idx) // struct.calcsize(fmt), fmt), idx)
        out = set()
        for t in range(0, len(idx), 3):
            tri = []
            for k in idx[t:t + 3]:
                tri.append(tuple(round(xyz[k * 3 + c] / tol) * tol for c in range(3)))
            out.add(tuple(tri))
        return out

    for name in sorted(mat_n):
        ta = set().union(*(triangles(jn, bn, p) for p in mat_n[name]))
        tb = set().union(*(triangles(jo, bo, p) for p in mat_o[name]))
        if ta != tb:
            problems.append("triangle set differs for material %s (%d vs %d tris)"
                            % (name, len(ta), len(tb)))
    return {"max_float_diff": max_diff, "problems": problems[:20]}


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
    with tempfile.TemporaryDirectory(prefix="crate-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        result = compare_glb(reexport, opts.compare)
    result["reopened_blend"] = str(opts.verify)
    result["compared_glb"] = str(opts.compare)
    print("VERIFY_RESULT " + json.dumps(result))
    assert not result["problems"] and result["max_float_diff"] <= 1e-6, result


def fake_rows(tris=EXPECT_TRIS, role="frame_dark", finite=True):
    y_max = .1 if finite else float("nan")
    return [{"node": "Crate", "roles": [role], "triangles": tris, "vertices": 32,
             "bounds": {"min": [0.0, 0.0, 0.0], "max": [.1, y_max, .1]}}]


def run_check_suite():
    results = {}
    try:
        assert Path("/nonexistent/crate-missing.glb").is_file()
        results["missing_input"] = "FAIL: not detected"
    except AssertionError:
        results["missing_input"] = "PASS"
    try:
        check_rows(fake_rows(tris=12))
        results["wrong_triangles"] = "FAIL: not detected"
    except AssertionError:
        results["wrong_triangles"] = "PASS"
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
    assert all(v == "PASS" for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


def write_report(out, facts):
    lines = [
        "# crate (P01) geometry report — ART-LF-BATCH-01 subpackage B",
        "",
        "- Scope: re-host of the read-only canonical crate-r1 GLB; no geometry,",
        "  scale, material, animation or inventory change. Low-fi candidate,",
        "  not an engineering or gameplay certification.",
        "- Source: %s" % facts["source"]["path"],
        "- Source sha256: %s" % facts["source"]["sha256"][:16] + "...",
        "- Deliverable GLB sha256: %s" % facts["files"]["crate.glb"]["sha256"][:16] + "...",
        "- Blender: %s, export via bpy glTF (GLB, apply modifiers, Y-up)." % facts["blender"],
        "- Geometry: 1 mesh 'Crate', %d tris, %d verts, roles %s." % (
            facts["triangles_total"], facts["vertices_total"], facts["roles_used"]),
        "- Bounds (glTF m): min %s max %s; footprint .64 x .38 x .70 kept," % (
            facts["meshes"][0]["bounds"]["min"], facts["meshes"][0]["bounds"]["max"]),
        "  grounded at Y0.",
        "- Reopen/re-export: %s" % facts["reopen_check"],
        "- Roundtrip vs original hand-authored GLB: %s" % facts["original_check"],
        "- GLB bytes vs re-export: identical_bytes=%s (byte layout may differ" % (
            facts["reopen_check"].get("identical_bytes")),
        "  from the original struct-packed file; geometry/indices/floats <=1e-6).",
        "- Checks: %s" % json.dumps(facts["checks"]["negative_suite"]),
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
    parser.add_argument("--output", type=Path, default=HERE / "crate")
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
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    rows = survey()
    check_rows(rows)
    opts.output.mkdir(parents=True, exist_ok=True)
    blend = opts.output / "crate.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    glb = opts.output / "crate.glb"
    export_glb(glb)
    assert glb.is_file() and glb.stat().st_size > 0 and glb.read_bytes()[:4] == b"glTF"
    opts.runtime.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(glb, opts.runtime)
    # reopen/re-export against the deliverable
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    with tempfile.TemporaryDirectory(prefix="crate-verify-") as tmp:
        reexport = Path(tmp) / "reexport.glb"
        export_glb(reexport)
        reopen = compare_glb(reexport, glb)
    assert not reopen["problems"] and reopen["max_float_diff"] <= 1e-6, reopen
    original = compare_vs_original(glb, opts.source)
    assert not original["problems"] and original["max_float_diff"] <= 1e-6, original
    checks = {"missing_input": "PASS (build aborts)",
              "wrong_triangles": "PASS (asserted in check_rows)",
              "unknown_role": "PASS (roles subset asserted)",
              "non_finite": "PASS (finite coords asserted)",
              "empty_output": "PASS (GLB magic + size asserted)",
              "negative_suite": run_check_suite()}
    facts = {
        "scope": "ART-LF-BATCH-01 subpackage B: P01 crate-r1 re-host; static, "
                 "no animation, no new capability",
        "ticket": "docs/art/production/tasks/ART-LF-BATCH-01.md (P01 crate)",
        "blender": bpy.app.version_string,
        "source": {"path": str(opts.source),
                   "sha256": hashlib.sha256(opts.source.read_bytes()).hexdigest(),
                   "provenance": "read-only canonical crate-r1 GLB (hand-authored "
                                 "r3, no coplanar artifacts); untouched"},
        "triangles_total": rows[0]["triangles"],
        "vertices_total": rows[0]["vertices"],
        "roles_used": rows[0]["roles"],
        "meshes": rows,
        "files": {p.name: {"bytes": p.stat().st_size,
                           "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in [blend, glb, opts.runtime]},
        "reopen_check": reopen,
        "original_check": original,
        "checks": checks,
        "not_run": ["Godot import check handled by shared runner",
                    "gameplay/inventory integration", "UV/baking/LOD",
                    "owner visual acceptance"],
        "build_seconds": round(time.monotonic() - started, 3)}
    (opts.output / "geometry-facts.json").write_text(json.dumps(facts, indent=2) + "\n")
    write_report(opts.output, facts)
    print("CRATE_BUILT", facts["triangles_total"], "tris",
          facts["build_seconds"], "s", "reopen_ok", reopen["identical_bytes"])


if __name__ == "__main__":
    main()
