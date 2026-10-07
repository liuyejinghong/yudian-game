"""Original static terrain dressing. Run with Blender --background --python-exit-code 1
--python art/environment/terrain-look-r1/rocks/generate_rocks.py [-- --check].
--check reopens the delivered sources and compares fresh exports without replacing files.
"""
import argparse
import hashlib
import importlib.util
import json
import math
import random
import sys
import tempfile
from pathlib import Path

import bpy
import bmesh

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
HELPER = ROOT / "art/source/lowfi-batch-r1/existing/build_terrain.py"
spec = importlib.util.spec_from_file_location("terrain_export", HELPER)
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
COLORS = [(0.19, 0.105, 0.075), (0.235, 0.16, 0.12),
          (0.275, 0.225, 0.18), (0.31, 0.265, 0.22)]
SIZES = {"rock-cluster": (3.0, 0.88, 2.5), "rock-outcrop": (4.0, 1.45, 2.0)}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stone(name, center, size, yaw, seed, layered, root, mats):
    rng = random.Random(seed)
    n = 9
    outline = [rng.uniform(0.82, 1.08) for _ in range(n)]
    rings = [(0.0, 0.83), (0.21, 1.0), (0.57, 0.92), (0.84, 0.72), (1.0, 0.46)]
    verts = []
    for level, (height, radius) in enumerate(rings):
        for i in range(n):
            angle = i * math.tau / n + yaw
            r = radius * outline[i]
            x = math.cos(angle) * size[0] * r / 2 + height * size[0] * 0.09
            y = math.sin(angle) * size[1] * r / 2 - height * size[1] * 0.07
            z = height * size[2]
            if level not in (0, len(rings) - 1):
                z += rng.uniform(-0.025, 0.025) * size[2]
            if level == len(rings) - 1:
                z += rng.uniform(-0.09, 0.04) * size[2]
            verts.append((center[0] + x, center[1] + y, z))
    faces = [tuple(reversed(range(n)))]
    roles = [0]
    for level in range(len(rings) - 1):
        for i in range(n):
            j = (i + 1) % n
            faces.append((level * n + i, level * n + j,
                          (level + 1) * n + j, (level + 1) * n + i))
            roles.append(min(level, 3) if layered else rng.choice([1, 1, 2, 2, 3]))
    faces.append(tuple((len(rings) - 1) * n + i for i in range(n)))
    roles.append(3)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for mat in mats:
        mesh.materials.append(mat)
    for face, role in zip(mesh.polygons, roles):
        face.material_index = role
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = root


def build(key):
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.preferences.filepaths.save_version = 0
    root = bpy.data.objects.new(key, None)
    bpy.context.scene.collection.objects.link(root)
    mats = []
    for i, color in enumerate(COLORS):
        mat = bpy.data.materials.new("rock-earth-%d" % i)
        mat.diffuse_color = (*color, 1)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*color, 1)
        bsdf.inputs["Roughness"].default_value = 0.95
        mats.append(mat)
    if key == "rock-cluster":
        pieces = [((-0.65, 0.2), (1.7, 1.5, 0.88), 0.3),
                  ((0.65, 0.05), (1.4, 1.3, 0.68), -0.2),
                  ((-0.2, -0.85), (1.25, 1.0, 0.54), 0.7),
                  ((0.72, 0.8), (0.95, 0.85, 0.43), 0.4),
                  ((-1.03, -0.68), (0.72, 0.75, 0.32), -0.4)]
    else:
        pieces = [((-0.72, 0.15), (2.6, 1.6, 1.45), 0.08),
                  ((1.0, -0.1), (1.8, 1.5, 0.97), -0.25),
                  ((-0.35, -0.68), (1.65, 0.8, 0.48), -0.15)]
    for i, (center, size, yaw) in enumerate(pieces):
        stone("Stone_%02d" % (i + 1), center, size, yaw,
              7121 + i, key == "rock-outcrop", root, mats)
    objects = [o for o in bpy.data.objects if o.type == "MESH"]
    points = [v.co for o in objects for v in o.data.vertices]
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    target = (SIZES[key][0], SIZES[key][2], SIZES[key][1])
    for obj in objects:
        for v in obj.data.vertices:
            for i in range(3):
                v.co[i] = (v.co[i] - (lo[i] if i == 2 else (lo[i] + hi[i]) / 2)) * target[i] / (hi[i] - lo[i])
    return facts(key)


def facts(key):
    rows = []
    points = []
    for obj in bpy.data.objects:
        assert obj.type in {"EMPTY", "MESH"}, obj.type
        assert obj.animation_data is None and not obj.modifiers, obj.name
        if obj.type != "MESH":
            assert tuple(obj.location) == (0, 0, 0)
            continue
        assert obj.parent and obj.parent.name == key
        obj.data.calc_loop_triangles()
        rows.append({"name": obj.name, "vertices": len(obj.data.vertices),
                     "faces": len(obj.data.polygons), "triangles": len(obj.data.loop_triangles)})
        points.extend(helper.to_gltf(obj.matrix_world @ v.co) for v in obj.data.vertices)
        assert all(not p.use_smooth for p in obj.data.polygons)
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    dimensions = [hi[i] - lo[i] for i in range(3)]
    assert all(math.isfinite(c) for p in points for c in p)
    assert abs(lo[1]) < 1e-6
    assert all(abs(a - b) < 1e-5 for a, b in zip(dimensions, SIZES[key]))
    return {"dimensions_xyz_m": [round(v, 6) for v in dimensions],
            "bounds_gltf_m": {"min": lo, "max": hi}, "meshes": rows,
            "triangles": sum(row["triangles"] for row in rows)}


def verify(key):
    bpy.ops.wm.open_mainfile(filepath=str(HERE / (key + ".blend")))
    result = facts(key)
    with tempfile.TemporaryDirectory(prefix="rocks-check-") as tmp:
        fresh = Path(tmp) / "fresh.glb"
        helper.export_glb(fresh)
        check = helper.compare_glb(fresh, HERE / (key + ".glb"))
        assert not check["problems"] and check["max_float_diff"] <= 1e-6, check
        js, _ = helper.read_glb(fresh)
        assert not js.get("animations")
    result["reopen_export_check"] = check
    return result


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    opts = parser.parse_args(args)
    assert bpy.app.version == (4, 5, 14), bpy.app.version_string
    if opts.check:
        for key in SIZES:
            print("ROCK_CHECK", key, json.dumps(verify(key)))
        return
    assert not any((HERE / (key + ext)).exists() for key in SIZES for ext in (".blend", ".glb")), "Back up existing deliverables before rebuilding"
    manifest = {"source_version": "terrain-look-r1-rocks-v1", "baseline": "718905d",
                "blender": bpy.app.version_string, "units": "meters",
                "axes": "+Y up / -Z forward in glTF; Blender +Z up",
                "provenance": "Original deterministic polygon-ring geometry; no external assets, textures, or AI-generated meshes",
                "semantics": {"animation": False, "collision": False,
                              "resource_facts": False, "real_obstacle": False},
                "formal_integration": "NOT_RUN", "not_run": ["Godot import", "RTS visual review", "slope placement"],
                "generator_sha256": sha(Path(__file__)),
                "export_helper": {"path": str(HELPER.relative_to(ROOT)), "sha256": sha(HELPER)},
                "check_command": "Blender --background --factory-startup --python-exit-code 1 --python art/environment/terrain-look-r1/rocks/generate_rocks.py -- --check",
                "assets": {}}
    for key in SIZES:
        build(key)
        blend = HERE / (key + ".blend")
        glb = HERE / (key + ".glb")
        bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        helper.export_glb(glb)
        row = verify(key)
        row["files"] = {p.name: {"bytes": p.stat().st_size, "sha256": sha(p)} for p in (blend, glb)}
        manifest["assets"][key] = row
        print("ROCK_BUILT", key, row["triangles"], "triangles; reopen PASS")
    (HERE / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
