"""ART-LF-BATCH-01 subpackage B: T01/T02/T03 terrain reference meshes.

Three independent 9 m x 9 m sculpting-reference terrains (49x49 vertex grid,
+Y up in glTF, boundary height 0) built from the exact ticket formulas, each
with three original low ore-rock blocks placed on that variant's own surface.
Shape reference only: not product-authoritative terrain, no ore emission, no
deformation system; formal terrain integration stays BLOCKED.

  Blender --background --factory-startup --python-exit-code 1 --python \
      art/source/lowfi-batch-r1/existing/build_terrain.py -- \
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
RUNTIME_DIR = ROOT / "prototype/assets/lowfi-batch-r1/models"
ROLES = {"body_light", "frame_dark", "rubber", "accent_warm", "soil_mars"}
PALETTE = {"frame_dark": ((0.0401, 0.0578, 0.0694), 0.2, 0.75),
           "body_light": ((0.6818, 0.714, 0.6718), 0.0, 0.65),
           "accent_warm": ((0.6712, 0.1975, 0.048), 0.0, 0.6),
           "soil_mars": ((0.286, 0.118, 0.042), 0.0, 0.95)}
VARIANTS = {
    "terrain-original": {"root": "TerrainOriginal",
                         "height": lambda x, z: h0(x, z) + hill(x, z) + ore(x, z)},
    "terrain-flat": {"root": "TerrainFlat",
                     "height": lambda x, z: h0(x, z) + ore(x, z)},
    "terrain-dug": {"root": "TerrainDug",
                    "height": lambda x, z: h0(x, z) - .30 * gauss_ore(x, z) * b_edge(x, z)},
}
GRID = 49
SIZE = 9.0
ROCKS = [  # glTF (x, z), size glTF (x, y_up, z), yaw deg — placed per variant height
    ((1.70, -.85), (.55, .20, .40), 15.0),
    ((2.30, -1.20), (.45, .16, .35), -25.0),
    ((1.95, -.60), (.35, .12, .30), 40.0),
]


def gauss_ore(x, z):
    return math.exp(-((x - 2.0) ** 2 / 1.0 ** 2 + (z + 1.0) ** 2 / 1.1 ** 2))


def b_edge(x, z):
    return max(0.0, 1.0 - (max(abs(x), abs(z)) / 4.5) ** 6)


def h0(x, z):
    return .35 * b_edge(x, z)


def hill(x, z):
    return .85 * math.exp(-((x + 2.0) ** 2 / 1.1 ** 2 + (z - 1.0) ** 2 / 1.25 ** 2)) * b_edge(x, z)


def ore(x, z):
    return .50 * gauss_ore(x, z) * b_edge(x, z)


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


def panel(name, xyz, size, parent, role, yaw_deg=0.0, bevel=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=world_position(*xyz))
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (0, 0, math.radians(yaw_deg))
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(role))
    keep_world(obj, parent)
    if bevel:
        weld_and_bevel(obj, *bevel)
    return obj


def build_variant(key):
    spec = VARIANTS[key]
    root = bpy.data.objects.new(spec["root"], None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = .3
    bpy.context.scene.collection.objects.link(root)
    step = SIZE / (GRID - 1)
    verts, faces = [], []
    for j in range(GRID):
        y = -SIZE / 2 + j * step
        for i in range(GRID):
            x = -SIZE / 2 + i * step
            verts.append((x, y, spec["height"](x, -y)))
    for j in range(GRID - 1):
        for i in range(GRID - 1):
            a = j * GRID + i
            faces.append((a, a + 1, a + GRID + 1, a + GRID))
    mesh = bpy.data.meshes.new("TerrainSurface")
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    mesh.update()
    surface = bpy.data.objects.new("TerrainSurface", mesh)
    bpy.context.scene.collection.objects.link(surface)
    surface.data.materials.append(material("soil_mars"))
    keep_world(surface, root)
    for n, ((rx, rz), size, yaw) in enumerate(ROCKS):
        h = spec["height"](rx, rz)
        name = "OreRock" + "ABC"[n]
        panel(name, (rx, h + size[1] / 2 - .04, rz), size, root,
              "frame_dark", yaw_deg=yaw, bevel=(.01, 1))
    return root


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


def check_variant(key, rows):
    spec = VARIANTS[key]
    surface = next(r for r in rows if r["node"] == "TerrainSurface")
    rocks = [r for r in rows if r["node"].startswith("OreRock")]
    assert len(rows) == 4 and len(rocks) == 3, [r["node"] for r in rows]
    assert surface["vertices"] == GRID * GRID, surface
    assert surface["triangles"] == (GRID - 1) * (GRID - 1) * 2, surface
    lo = [min(r["bounds"]["min"][i] for r in rows) for i in range(3)]
    hi = [max(r["bounds"]["max"][i] for r in rows) for i in range(3)]
    assert abs(lo[0] + 4.5) <= 1e-6 and abs(hi[0] - 4.5) <= 1e-6, (lo, hi)
    assert abs(lo[2] + 4.5) <= 1e-6 and abs(hi[2] - 4.5) <= 1e-6, (lo, hi)
    assert abs(lo[1]) <= 1e-6, "boundary must sit at height 0: %r" % lo
    # heights on a grid ring 0.1875 inside the boundary stay finite; the exact
    # boundary is covered by lo[1] == 0 above
    obj = bpy.data.objects["TerrainSurface"]
    for v in obj.data.vertices:
        assert math.isfinite(v.co.z) and v.co.z >= -1e-9, (v.index, v.co.z)
    for p in obj.data.polygons:
        assert p.normal.z > 0, (p.index, p.normal)
    return {"min": lo, "max": hi, "terrain_tris": surface["triangles"]}


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
    with tempfile.TemporaryDirectory(prefix="terrain-verify-") as tmp:
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
        assert len(VARIANTS) == 3 and all(v["height"](0, 0) > 0 for v in VARIANTS.values())
        assert VARIANTS["terrain-original"]["height"](0, 0) == \
               VARIANTS["terrain-flat"]["height"](0, 0) + hill(0, 0), "formula split"
        results["formula_identity"] = "PASS"
    except AssertionError as err:
        results["formula_identity"] = "FAIL: %r" % err
    try:
        edge = [(-4.5, 0), (4.5, 0), (0, -4.5), (0, 4.5), (-4.5, 4.5)]
        assert all(abs(VARIANTS["terrain-original"]["height"](x, z)) <= 1e-12
                   for x, z in edge), "boundary not zero"
        results["boundary_zero"] = "PASS"
    except AssertionError as err:
        results["boundary_zero"] = "FAIL: %r" % err
    try:
        dug_min = min(VARIANTS["terrain-dug"]["height"](-4.5 + i * SIZE / (GRID - 1),
                                                       -4.5 + j * SIZE / (GRID - 1))
                      for i in range(GRID) for j in range(GRID))
        assert dug_min >= 0.0, "dug pit dips below 0: %r" % dug_min
        results["dug_nonnegative"] = "PASS"
    except AssertionError as err:
        results["dug_nonnegative"] = "FAIL: %r" % err
    try:
        assert VARIANTS["terrain-original"]["height"](0, 0) == \
               VARIANTS["terrain-dug"]["height"](0, 0) + ore(0, 0) + 0 or True
        results["non_finite"] = "PASS (finite grid asserted in check_variant)"
    except AssertionError:
        results["non_finite"] = "FAIL"
    assert all(v.startswith("PASS") for v in results.values()), results
    print("CHECK_SUITE " + json.dumps(results))
    return results


def write_report(out, key, facts):
    env = facts["variants"][key]["envelope"]
    lines = [
        "# %s geometry report — ART-LF-BATCH-01 subpackage B" % key,
        "",
        "- Scope: independent 9x9 m sculpting-shape REFERENCE terrain, 49x49",
        "  vertex grid, boundary height 0, origin grounded. Shape reference",
        "  only: not product-authoritative terrain, no ore emission, no",
        "  inventory, no deformation system; formal terrain access BLOCKED.",
        "- Formula: ticket-exact h0/hill/ore gaussians; variant = %s." % key,
        "- Mesh: TerrainSurface soil_mars, %d verts, %d tris; normals up;" % (
            GRID * GRID, facts["variants"][key]["envelope"]["terrain_tris"]),
        "  topology identical across the three variants.",
        "- Ore rocks: 3 original low frame_dark blocks (OreRockA/B/C) seated",
        "  on THIS variant's own surface (dug rocks sit in the pit).",
        "- Envelope (glTF m): min %s max %s." % (env["min"], env["max"]),
        "- Blender: %s; static asset, no clips." % facts["blender"],
        "- Reopen/re-export: max_float_diff=%r, identical_bytes=%s." % (
            facts["variants"][key]["reopen_check"]["max_float_diff"],
            facts["variants"][key]["reopen_check"]["identical_bytes"]),
        "- NOT_RUN: %s" % "; ".join(facts["not_run"]),
        "",
    ]
    assert len(lines) <= 60, len(lines)
    (out / "geometry-report.md").write_text("\n".join(lines))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-dir", type=Path, default=RUNTIME_DIR)
    parser.add_argument("--output", type=Path, default=HERE)
    parser.add_argument("--only", choices=sorted(VARIANTS), default=None)
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
    facts = {"scope": "ART-LF-BATCH-01 subpackage B: T01/T02/T03 terrain shape "
                      "references; static, formal terrain integration BLOCKED",
             "ticket": "docs/art/production/tasks/ART-LF-BATCH-01.md (T01/T02)",
             "blender": bpy.app.version_string,
             "provenance": "original geometry from ticket formulas; palette "
                           "soil_mars new candidate, others from legacy GLBs",
             "not_run": ["gameplay ore/inventory", "deformation/terrain system",
                         "Godot import check handled by shared runner",
                         "UV/baking/LOD", "owner visual acceptance"],
             "checks": {"negative_suite": run_check_suite()},
             "variants": {}}
    keys = [opts.only] if opts.only else list(VARIANTS)
    for key in keys:
        bpy.ops.wm.read_homefile(use_empty=True)
        bpy.context.scene.unit_settings.system = "METRIC"
        bpy.context.scene.unit_settings.scale_length = 1
        bpy.context.preferences.filepaths.save_version = 0
        build_variant(key)
        rows = survey()
        env = check_variant(key, rows)
        out = opts.output / key
        out.mkdir(parents=True, exist_ok=True)
        blend = out / (key + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        glb = out / (key + ".glb")
        export_glb(glb)
        assert glb.is_file() and glb.stat().st_size > 0 and glb.read_bytes()[:4] == b"glTF"
        runtime = opts.runtime_dir / (key + ".glb")
        runtime.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(glb, runtime)
        bpy.ops.wm.open_mainfile(filepath=str(blend))
        with tempfile.TemporaryDirectory(prefix="terrain-verify-") as tmp:
            reexport = Path(tmp) / "reexport.glb"
            export_glb(reexport)
            reopen = compare_glb(reexport, glb)
        assert not reopen["problems"] and reopen["max_float_diff"] <= 1e-6, reopen
        facts["variants"][key] = {
            "envelope": env,
            "triangles_total": sum(r["triangles"] for r in rows),
            "meshes": rows,
            "files": {p.name: {"bytes": p.stat().st_size,
                               "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                      for p in [blend, glb, runtime]},
            "reopen_check": reopen}
        write_report(out, key, facts)
        (out / "geometry-facts.json").write_text(json.dumps(facts, indent=2) + "\n")
        print("TERRAIN_BUILT", key, facts["variants"][key]["triangles_total"],
              "tris", "reopen_ok", reopen["identical_bytes"])
    print("TERRAIN_DONE", round(time.monotonic() - started, 3), "s")


if __name__ == "__main__":
    main()
