#!/usr/bin/env python3
"""Static consumer checks for the tutorial-MVP far-side outcrop landmark (art/consumer side only).

Scope: file/GLB/scene identity and geometry math on repo bytes. This script does NOT run Godot,
does NOT import assets and does NOT render Main. Godot import, actual Main screen and whole-scene
frame budget remain NOT_RUN here and belong to the controller's real-Main acceptance.

Apron coverage is checked on the world quads DERIVED through the real parent x child transform
pipeline (Main.tscn instance transform x the script's packed world->local formula), not on the raw
constants, and a negative control must flag the naive local-placement variant.

Run: python3 -I tools/tutorial-mars-tests/verify_tutorial_mars_landmark.py
"""
from __future__ import annotations
import hashlib
import json
import math
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC_GLB = ROOT / "art/source/environment/mars-outcrop-hifi-r2/mars-outcrop-hifi-r2.glb"
COPY_GLB = ROOT / "prototype/assets/environment/tutorial-mars-r1/mars-outcrop-hifi-r2.glb"
MANIFEST = ROOT / "art/source/environment/mars-outcrop-hifi-r2/manifest.json"
FINAL_NATIVE = ROOT / "art/source/environment/mars-outcrop-hifi-r2/verification/final-native.json"
LOOKDEV_CAPTURE = ROOT / "docs/art/production/mars-hifi-r2/evidence/native-final/capture.json"
CAMERA_JSON = ROOT / "prototype/assets/d1-art/camera.json"
FIXTURE_JSON = ROOT / "prototype/fixtures/s_small.json"
MAIN_TSCN = ROOT / "prototype/scenes/Main.tscn"
LANDMARK_TSCN = ROOT / "prototype/assets/environment/tutorial-mars-r1/mars-outcrop-landmark.tscn"
LANDMARK_GD = ROOT / "prototype/assets/environment/tutorial-mars-r1/mars-outcrop-landmark.gd"
TERRAIN_VIEW_CS = ROOT / "prototype/scripts/TerrainWorld/TerrainRegionView.cs"

# Contract: the landmark footprint must stay outside the 60x60 authoritative terrain
# (x/z in [-30, 30]) with margin, per the tutorial-mvp-r1 contract ("例如 x/z 范围 > 31").
TERRAIN_HALF = 30.0
OUTSIDE_LIMIT = 31.0
MIN_CLEARANCE_M = 1.0

results: list[tuple[str, bool, str]] = []

def check(name: str, ok: bool, detail: str) -> bool:
    results.append((name, bool(ok), detail))
    return bool(ok)

def rel(p: Path) -> str:
    try:
        return str(p.relative_to(ROOT))
    except ValueError:
        return str(p)

def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()

# ---------- GLB parsing (glTF 2.0 binary) ----------

def parse_glb(path: Path) -> dict:
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    assert magic == b"glTF" and version == 2 and length == len(data), "not a valid GLB v2 container"
    off, jsonchunk, binchunk = 12, None, b""
    while off < len(data):
        clen, ctype = struct.unpack_from("<I4s", data, off)
        payload = data[off + 8:off + 8 + clen]
        if ctype == b"JSON":
            jsonchunk = payload
        elif ctype.startswith(b"BIN"):
            binchunk = payload
        off += 8 + clen
    return {"json": json.loads(jsonchunk), "bin": binchunk, "raw_json": jsonchunk}

def accessor_floats(j: dict, binb: bytes, acc_index: int) -> list[tuple]:
    acc = j["accessors"][acc_index]
    comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[acc["type"]]
    assert acc["componentType"] == 5126, "expected float accessor"
    bv = j["bufferViews"][acc["bufferView"]]
    base = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride") or 4 * comps
    return [struct.unpack_from("<" + "f" * comps, binb, base + i * stride) for i in range(acc["count"])]

def node_matrix(nd: dict) -> list[float]:
    if "matrix" in nd:
        m = nd["matrix"]
        return [m[0], m[4], m[8], m[12], m[1], m[5], m[9], m[13], m[2], m[6], m[10], m[14], m[3], m[7], m[11], m[15]]
    t = nd.get("translation", [0, 0, 0]); r = nd.get("rotation", [0, 0, 0, 1]); s = nd.get("scale", [1, 1, 1])
    x, y, z, w = r
    rot = [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 2 * (x * y + z * w),
           1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 2 * (x * z - y * w), 2 * (y * z + x * w),
           1 - 2 * (x * x + y * y)]
    return [rot[0] * s[0], rot[1] * s[1], rot[2] * s[2], t[0],
            rot[3] * s[0], rot[4] * s[1], rot[5] * s[2], t[1],
            rot[6] * s[0], rot[7] * s[1], rot[8] * s[2], t[2],
            0.0, 0.0, 0.0, 1.0]

def mat_mul(a: list[float], b: list[float]) -> list[float]:
    return [sum(a[i * 4 + k] * b[k * 4 + j] for k in range(4)) for i in range(4) for j in range(4)]

def xform(m: list[float], p) -> tuple:
    x, y, z = p
    return (m[0] * x + m[1] * y + m[2] * z + m[3],
            m[4] * x + m[5] * y + m[6] * z + m[7],
            m[8] * x + m[9] * y + m[10] * z + m[11])

def glb_world_data(glb: dict) -> dict:
    j, binb = glb["json"], glb["bin"]
    world = {}
    def walk(i: int, parent: list[float]) -> None:
        m = mat_mul(parent, node_matrix(j["nodes"][i]))
        world[i] = m
        for c in j["nodes"][i].get("children", []):
            walk(c, m)
    ident = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    for root in j["scenes"][j.get("scene", 0)]["nodes"]:
        walk(root, ident)
    tris = 0
    lo = [1e18] * 3; hi = [-1e18] * 3
    verts = []
    for i, nd in enumerate(j["nodes"]):
        if nd.get("mesh") is None:
            continue
        m = world[i]
        for prim in j["meshes"][nd["mesh"]]["primitives"]:
            pos = j["accessors"][prim["attributes"]["POSITION"]]
            idx = j["accessors"][prim["indices"]] if "indices" in prim else None
            tris += (idx["count"] // 3) if idx else (pos["count"] // 3)
            for v in accessor_floats(j, binb, prim["attributes"]["POSITION"]):
                w = xform(m, v)
                verts.append(w)
                for k in range(3):
                    lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])
    return {"triangles": tris, "aabb_min": tuple(lo), "aabb_max": tuple(hi), "verts": verts}

# ---------- scene-text parsing (format=3 .tscn) ----------

def parse_transform3d(text: str, node_name: str) -> tuple[list[float], tuple[float, float, float]]:
    m = re.search(r'\[node name="' + re.escape(node_name) + r'"[^\]]*\]\n((?:[^\[]|\n)*)', text)
    assert m, f"node {node_name} not found"
    tm = re.search(r"transform = Transform3D\(([^)]*)\)", m.group(1))
    assert tm, f"node {node_name} has no transform"
    nums = [float(v) for v in tm.group(1).split(",")]
    assert len(nums) == 12, "Transform3D needs 12 floats"
    return [nums[0], nums[1], nums[2], nums[3], nums[4], nums[5], nums[6], nums[7], nums[8]], (nums[9], nums[10], nums[11])

def basis_origin_to_matrix(basis_flat: list[float], origin: tuple) -> list[float]:
    # basis_flat stores the three basis COLUMNS; build the row-major 4x4 affine matrix.
    c0 = basis_flat[0:3]; c1 = basis_flat[3:6]; c2 = basis_flat[6:9]
    return [c0[0], c1[0], c2[0], origin[0],
            c0[1], c1[1], c2[1], origin[1],
            c0[2], c1[2], c2[2], origin[2],
            0.0, 0.0, 0.0, 1.0]

def affine_inverse(m: list[float]) -> list[float]:
    a, b, c = m[0], m[1], m[2]
    d, e, f = m[4], m[5], m[6]
    g, h, i = m[8], m[9], m[10]
    det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g)
    assert abs(det) > 1e-12, "singular parent transform"
    inv = [(e * i - f * h) / det, (c * h - b * i) / det, (b * f - c * e) / det,
           (f * g - d * i) / det, (a * i - c * g) / det, (c * d - a * f) / det,
           (d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det]
    t = (m[3], m[7], m[11])
    it = (inv[0] * t[0] + inv[1] * t[1] + inv[2] * t[2],
          inv[3] * t[0] + inv[4] * t[1] + inv[5] * t[2],
          inv[6] * t[0] + inv[7] * t[1] + inv[8] * t[2])
    return [inv[0], inv[1], inv[2], -it[0],
            inv[3], inv[4], inv[5], -it[1],
            inv[6], inv[7], inv[8], -it[2],
            0.0, 0.0, 0.0, 1.0]

def landmark_world_point(basis_flat: list[float], origin: tuple, v) -> tuple:
    c = [(basis_flat[0], basis_flat[1], basis_flat[2]), (basis_flat[3], basis_flat[4], basis_flat[5]),
         (basis_flat[6], basis_flat[7], basis_flat[8])]
    return (origin[0] + c[0][0] * v[0] + c[1][0] * v[1] + c[2][0] * v[2],
            origin[1] + c[0][1] * v[0] + c[1][1] * v[1] + c[2][1] * v[2],
            origin[2] + c[0][2] * v[0] + c[1][2] * v[1] + c[2][2] * v[2])

# ---------- camera projection (mirrors Main.cs + camera.json) ----------

def sub(a, b): return tuple(a[i] - b[i] for i in range(3))
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def norm(a):
    l = math.sqrt(sum(c * c for c in a)); return tuple(c / l for c in a)
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])

def main() -> int:
    # -- A. source identity --
    manifest = json.loads(MANIFEST.read_text())
    man_files = manifest["files"]["mars-outcrop-hifi-r2.glb"]
    src_sha = sha256_file(SRC_GLB)
    check("source GLB sha256 == manifest", src_sha == man_files["sha256"],
          f"{src_sha} vs manifest {man_files['sha256']}")
    check("source GLB bytes == manifest", SRC_GLB.stat().st_size == man_files["bytes"],
          f"{SRC_GLB.stat().st_size} vs manifest {man_files['bytes']}")

    # -- B. copy identity --
    copy_sha = sha256_file(COPY_GLB)
    check("copy GLB sha256 == source (unchanged)", copy_sha == src_sha, f"{copy_sha}")
    check("copy GLB bytes == source", COPY_GLB.stat().st_size == SRC_GLB.stat().st_size,
          f"{COPY_GLB.stat().st_size} bytes at {rel(COPY_GLB)}")

    # -- C/D. GLB structure identity --
    glb = parse_glb(COPY_GLB)
    j = glb["json"]
    wd = glb_world_data(glb)
    final_native = json.loads(FINAL_NATIVE.read_text())
    check("triangles == manifest.game_triangles", wd["triangles"] == manifest["game_triangles"],
          f"parsed {wd['triangles']} vs manifest {manifest['game_triangles']}")
    check("triangles == final-native receipt", wd["triangles"] == final_native["independent_glb_import"]["triangles"],
          f"parsed {wd['triangles']} vs receipt {final_native['independent_glb_import']['triangles']}")
    check("embedded images == 3 (receipt)", len(j.get("images", [])) == final_native["embedded_images"],
          f"{len(j.get('images', []))} images")
    imgs_ok = True; img_detail = []
    for im in j.get("images", []):
        bv = j["bufferViews"][im["bufferView"]]
        raw = glb["bin"][bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
        w, h = struct.unpack_from(">II", raw, 16) if raw[:8] == b"\x89PNG\r\n\x1a\n" else (0, 0)
        img_detail.append(f"{im.get('name')}:{w}x{h}")
        imgs_ok = imgs_ok and w == 4096 and h == 4096
    check("3 textures 4096x4096", imgs_ok and len(j.get("images", [])) == 3, ", ".join(img_detail))
    mats = j.get("materials", [])
    check("material GAME_MarsOutcrop metallic=0",
          len(mats) == 1 and mats[0].get("name") == final_native["materials"][0]["name"]
          and mats[0]["pbrMetallicRoughness"].get("metallicFactor") == final_native["materials"][0]["metallicFactor"],
          f"materials={[m.get('name') for m in mats]}")
    check("GLB fully embedded (no external uri)",
          all("uri" not in b for b in j.get("buffers", [])) and all("uri" not in im for im in j.get("images", [])),
          "buffers/images carry no uri")
    check("no absolute /Users/ path in GLB json", b"/Users/" not in glb["raw_json"],
          "glTF extras/metadata contain no local absolute path")

    # -- E. AABB vs manifest dims and engine-measured lookdev receipt --
    size = tuple(wd["aabb_max"][k] - wd["aabb_min"][k] for k in range(3))
    dims_ok = all(abs(sorted(size)[k] - sorted(manifest["dimensions"])[k]) < 1e-3 for k in range(3))
    check("AABB size == manifest dimensions (axis set)", dims_ok,
          f"parsed size {['%.4f' % v for v in size]} vs manifest {manifest['dimensions']}")
    cap = json.loads(LOOKDEV_CAPTURE.read_text())
    cap_asset = next(a for a in cap["assets"] if a["file"].endswith("mars-outcrop-hifi-r2.glb"))
    eng_min = cap_asset["bounds_min_m"]; eng_size = cap_asset["bounds_size_m"]
    check("Godot-space AABB == lookdev engine-measured bounds",
          all(abs(wd["aabb_min"][k] - eng_min[k]) < 1e-3 for k in range(3))
          and all(abs(size[k] - eng_size[k]) < 1e-3 for k in range(3)),
          f"parsed min {['%.4f' % v for v in wd['aabb_min']]} size {['%.4f' % v for v in size]} vs engine "
          f"min {['%.4f' % v for v in eng_min]} size {['%.4f' % v for v in eng_size]}")

    # -- F. Main.tscn wiring --
    main_text = MAIN_TSCN.read_text()
    check("Main.tscn references landmark scene",
          'path="res://assets/environment/tutorial-mars-r1/mars-outcrop-landmark.tscn"' in main_text,
          "ext_resource PackedScene present")
    check("Main.tscn keeps 1 main script + 1 landmark scene (load_steps=3)",
          main_text.startswith("[gd_scene load_steps=3 format=3]"), "header load_steps=3")
    check("Main.tscn child instance exists once",
          len(re.findall(r'\[node name="TutorialMarsOutcrop" parent="\." instance=ExtResource\("2_mars_landmark"\)\]', main_text)) == 1,
          "single fixed scene child")
    basis_flat, origin = parse_transform3d(main_text, "TutorialMarsOutcrop")

    # -- G/H. landmark scene + script contracts --
    lm_text = LANDMARK_TSCN.read_text()
    check("landmark scene has script, NOT the GLB ext_resource",
          "mars-outcrop-landmark.gd" in lm_text and "mars-outcrop-hifi-r2.glb" not in lm_text,
          "scene load does not pull the 53MB GLB; guarded runtime load only")
    check("landmark scene has no static meshes (apron is runtime-only, benchmark-safe)",
          "MeshInstance3D" not in lm_text, "no MeshInstance3D node lines")
    gd = LANDMARK_GD.read_text()
    gd_code = "\n".join(line.split("##")[0].split("#")[0] for line in gd.splitlines())  # strip comments
    guard_ok = all(t in gd for t in [
        'OS.get_environment("YUDIAN_BENCHMARK") == "1"', "--benchmark", "--yudian-benchmark",
        "--duration", "get_cmdline_user_args", "get_cmdline_args"])
    check("script mirrors BenchmarkOptions guard", guard_ok, "env + user/engine flag detection")
    check("script uses no test-flag shortcuts",
          not re.search(r"YUDIAN_[A-Z_]*SELF_TEST|YUDIAN_PLAYER_GUI_TEST|YUDIAN_CAPTURE", gd),
          "no self-test/capture env vars read")
    check("script calls no C#/Main members",
          not re.search(r"get_node\(|\bMain\b|PlayerContracts|ReadPlayerState", gd_code),
          "self-contained Node3D + OS only (comments excluded)")
    check("script has no per-frame logic", "func _process" not in gd and "func _physics_process" not in gd,
          "cosmetic idle only")
    check("script instantiates once", gd.count("instantiate(") == 1, "single instantiate call")
    check("script adds no physics", not re.search(r"CollisionShape3D|StaticBody3D|Area3D|PhysicsBody", gd),
          "no body/shape nodes in consumer code")
    check("apron uses no new shader", "ShaderMaterial" not in gd and ".gdshader" not in gd,
          "plain StandardMaterial3D only")

    # GDScript property names (v1 regression: C# names AlbedoColor/Roughness/CullMode do not exist in GDScript)
    snake_ok = all(t in gd_code for t in ("albedo_color", "roughness", "cull_mode"))
    cs_tokens = re.findall(r"AlbedoColor|\bRoughness\s*=|\bCullMode\s*=", gd_code)
    check("apron material uses GDScript property names", snake_ok and not cs_tokens,
          f"albedo_color/roughness/cull_mode present; C#-style tokens={cs_tokens or 'none'}")

    # apron material must match the existing TerrainRegionView surface (cross-checked live)
    cs = TERRAIN_VIEW_CS.read_text()
    cs_color = re.search(r"AlbedoColor = new Color\(([^)]*)\)", cs)
    cs_rough = re.search(r"Roughness = ([\d.]+)f", cs)
    cs_cull = re.search(r"CullMode = BaseMaterial3D\.CullModeEnum\.(\w+)", cs)
    gd_color = re.search(r"albedo_color = Color\(([^)]*)\)", gd_code)
    gd_rough = re.search(r"roughness = ([\d.]+)", gd_code)
    gd_cull = re.search(r"cull_mode = BaseMaterial3D\.(\w+)", gd_code)
    if cs_color and cs_rough and cs_cull and gd_color and gd_rough and gd_cull:
        cs_vals = [float(x.strip().rstrip("f")) for x in cs_color.group(1).split(",")]
        gd_vals = [float(x) for x in gd_color.group(1).split(",")]
        check("apron material matches TerrainRegionView surface",
              cs_vals == gd_vals and float(cs_rough.group(1)) == float(gd_rough.group(1))
              and cs_cull.group(1).upper() == "BACK" and gd_cull.group(1) == "CULL_BACK",
              f"albedo {gd_vals} roughness {gd_rough.group(1)} cull back == existing {cs_vals}/{cs_rough.group(1)}")
    else:
        check("apron material matches TerrainRegionView surface", False, "pattern not found")

    # ---- apron placement: world targets converted through the actual parent x child pipeline ----
    quads = [[float(x) for x in q.split(",")] for q in re.findall(r"Vector4\(([^)]*)\)", gd)]
    check("apron quad constants present", len(quads) == 3, f"{len(quads)} quads")
    pin_ok = ("var to_local := global_transform.affine_inverse()" in gd_code
              and "apron.transform = to_local * Transform3D(Basis.IDENTITY, center_world)" in gd_code
              and "apron.position =" not in gd_code)
    check("apron placement pins world->local through parent inverse", pin_ok,
          "global_transform.affine_inverse() x world Transform3D; no raw local position assignment")

    parent = basis_origin_to_matrix(basis_flat, origin)
    parent_inv = affine_inverse(parent)
    world_quads = []
    sim_ok = True; sim_detail = []
    for x0, z0, x1, z1 in quads:
        center_world = ((x0 + x1) * 0.5, 0.0, (z0 + z1) * 0.5)
        target = basis_origin_to_matrix([1, 0, 0, 0, 1, 0, 0, 0, 1], center_world)  # Transform3D(Basis.IDENTITY, center)
        local = mat_mul(parent_inv, target)          # what the script stores on the child
        final = mat_mul(parent, local)               # parent x child at runtime
        fx, fy, fz = final[3], final[7], final[11]
        identity_expect = {0: 1.0, 1: 0.0, 2: 0.0, 4: 0.0, 5: 1.0, 6: 0.0, 8: 0.0, 9: 0.0, 10: 1.0}
        basis_ok = all(abs(final[k] - identity_expect[k]) < 1e-9 for k in identity_expect)
        center_ok = all(abs((fx, fy, fz)[k] - center_world[k]) < 1e-9 for k in range(3))
        sim_ok = sim_ok and basis_ok and center_ok
        sim_detail.append(f"q[{x0:.0f},{x1:.0f}]x[{z0:.0f},{z1:.0f}]->center({fx:.2f},{fz:.2f})")
        world_quads.append((fx - (x1 - x0) / 2, fz - (z1 - z0) / 2, fx + (x1 - x0) / 2, fz + (z1 - z0) / 2))
    check("apron world quads via parent x child == declared world ranges", sim_ok, "; ".join(sim_detail))

    def rect_square_overlap(r):
        x0, z0, x1, z1 = r
        ox = min(x1, TERRAIN_HALF) - max(x0, -TERRAIN_HALF)
        oz = min(z1, TERRAIN_HALF) - max(z0, -TERRAIN_HALF)
        return ox > 1e-9 and oz > 1e-9

    outside_ok = True; detail = []
    for r in world_quads:
        overlap = rect_square_overlap(r)
        outside_ok = outside_ok and not overlap
        detail.append(f"x[{r[0]:.0f},{r[2]:.0f}] z[{r[1]:.0f},{r[3]:.0f}] overlap={'YES' if overlap else 'no'}")
    check("apron world quads never cover the playable square (no area overlap)", outside_ok, "; ".join(detail))

    # negative control: the v1 naive local placement must be caught by the world-quad checks above
    naive_world = []
    for x0, z0, x1, z1 in quads:
        center = ((x0 + x1) * 0.5, 0.0, (z0 + z1) * 0.5)
        wc = xform(parent, center)  # naive: constants written straight into local position
        naive_world.append((wc[0] - (x1 - x0) / 2, wc[2] - (z1 - z0) / 2, wc[0] + (x1 - x0) / 2, wc[2] + (z1 - z0) / 2))
    naive_overlaps = any(rect_square_overlap(r) for r in naive_world)
    naive_east = naive_world[2]
    check("negative control: naive local placement overlaps playable square", naive_overlaps,
          f"east quad would be x[{naive_east[0]:.1f},{naive_east[2]:.1f}] z[{naive_east[1]:.1f},{naive_east[3]:.1f}]")

    # -- I. placement contract (outside authoritative terrain, grounded) --
    corners = [(wd["aabb_min"][0] if i & 1 else wd["aabb_max"][0],
                wd["aabb_min"][1] if i & 2 else wd["aabb_max"][1],
                wd["aabb_min"][2] if i & 4 else wd["aabb_max"][2]) for i in range(8)]
    world_corners = [landmark_world_point(basis_flat, origin, c) for c in corners]
    xs = [p[0] for p in world_corners]; ys = [p[1] for p in world_corners]; zs = [p[2] for p in world_corners]
    rect = (min(xs), max(xs), min(zs), max(zs))
    rock_rect = rect
    gap_x = max(0.0, -TERRAIN_HALF - rect[1], rect[0] - TERRAIN_HALF)
    gap_z = max(0.0, -TERRAIN_HALF - rect[3], rect[2] - TERRAIN_HALF)
    clearance = math.hypot(gap_x, gap_z)
    check("footprint outside 60x60 authoritative terrain", gap_x > 0 or gap_z > 0,
          f"rect x[{rect[0]:.2f},{rect[1]:.2f}] z[{rect[2]:.2f},{rect[3]:.2f}]")
    check(f"terrain clearance >= {MIN_CLEARANCE_M} m", clearance >= MIN_CLEARANCE_M,
          f"{clearance:.2f} m (gaps x={gap_x:.2f} z={gap_z:.2f})")
    per_corner = min(max(abs(p[0]), abs(p[2])) for p in world_corners)
    check(f"every footprint corner beyond +/-{OUTSIDE_LIMIT}", per_corner > OUTSIDE_LIMIT,
          f"min over corners of max(|x|,|z|) = {per_corner:.2f}")
    check("feet at/below ground plane (no hover/lift)", -0.60 <= min(ys) <= 0.0,
          f"world AABB y [{min(ys):.3f}, {max(ys):.3f}] (base cut embeds 0.16 m below apron plane)")
    fixture = json.loads(FIXTURE_JSON.read_text())
    for site_name, site in (("mound", fixture["terrain"]["mound"]), ("mineral_pit", fixture["terrain"]["mineral_pit"])):
        px_, pz_, r = site["position"][0], site["position"][1], site["radius"]
        d = math.hypot(max(rect[0] - px_, px_ - rect[1], 0.0), max(rect[2] - pz_, pz_ - rect[3], 0.0)) - r
        check(f"clear of {site_name} (r={r})", d > 0.5, f"{d:.2f} m beyond site radius")
    rock_in_apron = all(any(x0 <= x <= x1 and z0 <= z <= z1 for x0, z0, x1, z1 in world_quads)
                        for x in (rect[0], rect[1]) for z in (rect[2], rect[3]))
    check("rock footprint stands on the visual apron (world quads)", rock_in_apron,
          "all footprint corners inside derived apron world quads")
    naive_misses_rock = not all(any(x0 <= x <= x1 and z0 <= z <= z1 for x0, z0, x1, z1 in naive_world)
                                for x in (rect[0], rect[1]) for z in (rect[2], rect[3]))
    check("negative control: naive local placement misses the rock footprint", naive_misses_rock,
          "v1 variant would leave the rock without apron under its feet")

    # -- J. normal-camera visibility (Main.cs player camera: camera.json normal *1.6 from target) --
    cam = json.loads(CAMERA_JSON.read_text())
    normal = cam["cameras"]["normal"]
    target = tuple(normal["target"]); art_pos = tuple(normal["position"])
    pos = tuple(target[i] + (art_pos[i] - target[i]) * 1.6 for i in range(3))
    fov_y = float(normal["fov"])
    W = fixture["target_resolution"]["width"]; H = fixture["target_resolution"]["height"]
    tan_half = math.tan(math.radians(fov_y / 2)); aspect = W / H
    z_axis = norm(sub(pos, target)); x_axis = norm(cross((0, 1, 0), z_axis)); y_axis = cross(z_axis, x_axis)

    def project(p):
        d = sub(p, pos)
        xc, yc, zc = dot(d, x_axis), dot(d, y_axis), dot(d, z_axis)
        depth = -zc
        return ((xc / depth) / (tan_half * aspect), (yc / depth) / tan_half, depth,
                (xc / depth) / (tan_half * aspect) * 0.5 * W + W / 2, H / 2 - (yc / depth) / tan_half * 0.5 * H)

    def ground_hit(px, py):
        ndc_x = (px / W) * 2 - 1; ndc_y = 1 - (py / H) * 2
        d = (x_axis[0] * ndc_x * tan_half * aspect + y_axis[0] * ndc_y * tan_half - z_axis[0],
             x_axis[1] * ndc_x * tan_half * aspect + y_axis[1] * ndc_y * tan_half - z_axis[1],
             x_axis[2] * ndc_x * tan_half * aspect + y_axis[2] * ndc_y * tan_half - z_axis[2])
        if d[1] >= -1e-9:
            return None
        t = -pos[1] / d[1]
        return (pos[0] + t * d[0], pos[2] + t * d[2])

    proj = [project(p) for p in world_corners]
    ndc_x = [p[0] for p in proj]; ndc_y = [p[1] for p in proj]
    pxs = [p[3] for p in proj]; pys = [p[4] for p in proj]; depth = min(p[2] for p in proj)
    check("within normal camera frustum", max(map(abs, ndc_x)) <= 0.95 and max(map(abs, ndc_y)) <= 0.95,
          f"ndc x[{min(ndc_x):+.3f},{max(ndc_x):+.3f}] y[{min(ndc_y):+.3f},{max(ndc_y):+.3f}]")
    check("inside 200 m far plane", depth < 200.0, f"nearest corner depth {depth:.1f} m")
    ui = cam["ui_reservation_px"]
    check("clear of UI reservations (left/top)",
          min(pxs) >= ui["left"] and min(pys) >= ui["top"],
          f"screen box x[{min(pxs):.0f},{max(pxs):.0f}] y[{min(pys):.0f},{max(pys):.0f}] of {W}x{H}, "
          f"UI left {ui['left']} top {ui['top']}")

    # -- K. apron coverage on the DERIVED world quads --
    def covered(x, z):
        eps = 0.05
        for x0, z0, x1, z1 in world_quads:
            if x0 - eps <= x <= x1 + eps and z0 - eps <= z <= z1 + eps:
                return True
        return False

    uncovered = 0; sampled = 0; outside_hits = 0
    for py in range(0, H, 4):
        for px in range(0, W, 4):
            hit = ground_hit(px, py)
            sampled += 1
            if hit is None:
                uncovered += 1
                continue
            x, z = hit
            if abs(x) <= TERRAIN_HALF + 1e-6 and abs(z) <= TERRAIN_HALF + 1e-6:
                continue
            outside_hits += 1
            if not covered(x, z):
                uncovered += 1
    check("every normal-frame sightline beyond terrain lands on terrain or apron", uncovered == 0,
          f"{sampled} rays sampled, {outside_hits} beyond-terrain, {uncovered} uncovered")

    seam_bad = []
    for qi, (x0, z0, x1, z1) in enumerate(world_quads):
        for cx, cz in ((x0, z0), (x1, z0), (x1, z1), (x0, z1)):
            p = project((cx, 0.0, cz))
            inside_frame = 0 <= p[3] <= W and 0 <= p[4] <= H
            collinear = (abs(abs(cx) - TERRAIN_HALF) < 1e-6 and abs(cz) <= TERRAIN_HALF + 1e-6) or \
                        (abs(abs(cz) - TERRAIN_HALF) < 1e-6 and abs(cx) <= TERRAIN_HALF + 1e-6)
            if inside_frame and not collinear:
                seam_bad.append(f"q{qi}({cx:.0f},{cz:.0f}) at px={p[3]:.0f},py={p[4]:.0f}")
    check("apron boundary edges stay off-frame or collinear with terrain edge", not seam_bad,
          "; ".join(seam_bad) if seam_bad else "all apron corners off-frame or on the terrain edge line")

    base_band = [v for v in wd["verts"] if v[1] <= wd["aabb_min"][1] + 0.02]
    bottom: dict[int, float] = {}
    for vv in base_band:
        p = project(landmark_world_point(basis_flat, origin, vv))
        col = int(p[3] // 8.0)
        bottom[col] = max(bottom.get(col, -1e9), p[4])
    under_bad = []
    for col, b in sorted(bottom.items()):
        for dy in (4, 10, 20):
            py = min(H - 1, b + dy)
            px = col * 8 + 4
            hit = ground_hit(px, py)
            if hit is None or not (abs(hit[0]) <= TERRAIN_HALF + 1e-6 and abs(hit[1]) <= TERRAIN_HALF + 1e-6 or covered(hit[0], hit[1])):
                under_bad.append(f"col{col} dy{dy} hit={hit}")
    check("pixels below rock base show ground (no floating cutoff)", not under_bad,
          "; ".join(under_bad) if under_bad else f"{len(bottom)} base columns x 3 offsets all grounded")

    # ---------- summary ----------
    failed = [r for r in results if not r[1]]
    print(f"tutorial-mars-r1 landmark static verification — {len(results) - len(failed)}/{len(results)} PASS")
    for name, ok, detail in results:
        print(f"  {'PASS' if ok else 'FAIL'}  {name}: {detail}")
    print()
    print("artifacts:")
    for p in (COPY_GLB, LANDMARK_TSCN, LANDMARK_GD, MAIN_TSCN):
        print(f"  {rel(p)}")
    print("NOT_RUN here: Godot import of the new GLB, actual Main screen, whole-scene frame budget.")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
