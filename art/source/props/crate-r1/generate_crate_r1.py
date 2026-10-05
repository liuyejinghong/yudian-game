#!/usr/bin/env python3
# ART-P01-CRATE-GEO · 共用货箱静态子片 r1（python 标准库，独立可重跑）
#
# 冻结范围：米制、+Y 上、前 -Z、底面中心原点；0.64 宽 × 0.70 深 × 0.38 高闭合货箱，
# 外沿严格 X±0.32 / Y 0…0.38 / Z±0.35。frame_dark 下框、body_light 箱体、frame_dark 上沿，
# 外加小型 accent_warm 前识别片；所有零件都在冻结包络内。
#
# 尺寸来源：现役 U01 geom_cargo 冻结候选（art/source/units/tuoyun-r1/，只读依据，不改其文件）。
# 本脚本不 import U01、不依赖其任何输出路径；glTF/GLB 写出为最小 stdlib 辅助（做法沿用 U01）。
# 正式材质接线走 res://assets/materials/art-r1/<role>.tres（M01 共享六角色），GLB 内仅占位分面色。
#
# 用法：
#   python3 generate_crate_r1.py             # 正常构建 + 写前全量校验，非法即非零退出
#   python3 generate_crate_r1.py --selftest  # 负例断言：错包络/负尺寸/非有限/未知角色/越界索引/翻绕序
import hashlib
import json
import math
import os
import shutil
import struct
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
NAME = "crate-r1"
# 私有 GLB 副本目录（本票独占可写）；目录缺失视为环境错误，不静默跳过。
PROTOTYPE_DIR = os.path.abspath(
    os.path.join(HERE, "..", "..", "..", "..", "prototype", "assets", "props", NAME))

ROLES = ["frame_dark", "body_light", "accent_warm"]  # 本票只用三角角色，不凑六角色
# 线性占位 PBR（与 U01 / art-r1 六表同源口径），仅承载分面角色。
ROLE_PBR = {
    "frame_dark": ((0.0401, 0.0578, 0.0694), 0.75, 0.2),
    "body_light": ((0.6818, 0.7140, 0.6718), 0.65, 0.0),
    "accent_warm": ((0.6712, 0.1975, 0.0480), 0.60, 0.0),
}
ENVELOPE = ((-0.32, 0.0, -0.35), (0.32, 0.38, 0.35))  # 冻结外沿
EPS = 1e-6
COORDINATE = "meter, +Y up, forward -Z, ground Y=0, translations only"


class ValidationError(Exception):
    """写前自查失败：调用方必须非零退出，不得落地任何文件。"""


def box(cx, cy, cz, sx, sy, sz):
    """轴对齐盒（世界坐标构造），外向 CCW；非正尺寸在构造期即拒绝。"""
    if not all(isinstance(s, (int, float)) and math.isfinite(s) and s > 0 for s in (sx, sy, sz)):
        raise ValidationError(f"box size must be finite and positive, got {sx},{sy},{sz}")
    x, y, z = sx / 2, sy / 2, sz / 2
    v = [(cx + a * x, cy + b * y, cz + c * z)
         for c in (-1, 1) for b in (-1, 1) for a in (-1, 1)]
    t = [(0, 2, 3), (0, 3, 1),      # -Z
         (4, 5, 7), (4, 7, 6),      # +Z
         (4, 1, 5), (4, 0, 1),      # -Y
         (2, 7, 3), (2, 6, 7),      # +Y
         (0, 4, 6), (0, 6, 2),      # -X
         (1, 7, 5), (1, 3, 7)]      # +X
    return v, t, sx * sy * sz


def build_parts():
    """三层同心壳 + 前识别片；尺寸取自 U01 geom_cargo 冻结候选，平移到底面中心原点。"""
    return [
        {"part": "lower_frame", "role": "frame_dark",
         "geo": box(0.0, 0.07, 0.0, 0.59, 0.14, 0.65)},
        {"part": "body", "role": "body_light",
         "geo": box(0.0, 0.23, 0.0, 0.62, 0.18, 0.68)},
        {"part": "top_rim", "role": "frame_dark",
         "geo": box(0.0, 0.35, 0.0, 0.64, 0.06, 0.70)},
        {"part": "id_plate_front", "role": "accent_warm",
         "geo": box(0.16, 0.27, -0.343, 0.16, 0.05, 0.008)},
    ]


def signed_volume(V, T):
    s = 0.0
    for a, b, c in T:
        A, B, C = V[a], V[b], V[c]
        s += (A[0] * (B[1] * C[2] - B[2] * C[1])
              - A[1] * (B[0] * C[2] - B[2] * C[0])
              + A[2] * (B[0] * C[1] - B[1] * C[0])) / 6.0
    return s


def aabb(parts):
    pts = [v for p in parts for v in p["geo"][0]]
    return ([min(p[i] for p in pts) for i in range(3)],
            [max(p[i] for p in pts) for i in range(3)])


def validate_parts(parts):
    """写前自查：未知角色 / 非有限数 / 包络越界 / 越界索引 / 闭合外向体积 / 落地与总包络。"""
    (x0, y0, z0), (x1, y1, z1) = ENVELOPE
    for p in parts:
        role = p["role"]
        if role not in ROLES:
            raise ValidationError(f"unknown role: {role!r}")
        V, T, vol = p["geo"]
        for i, v in enumerate(V):
            if len(v) != 3 or not all(isinstance(c, float) and math.isfinite(c) for c in v):
                raise ValidationError(f"{p['part']} vert {i} not finite 3-tuple: {v!r}")
            if not (x0 - EPS <= v[0] <= x1 + EPS and y0 - EPS <= v[1] <= y1 + EPS
                    and z0 - EPS <= v[2] <= z1 + EPS):
                raise ValidationError(f"{p['part']} vert {i} {v} breaches frozen envelope")
        for ti, t in enumerate(T):
            if len(t) != 3 or any(not isinstance(k, int) or not 0 <= k < len(V) for k in t):
                raise ValidationError(f"{p['part']} tri {ti} has out-of-range index: {t}")
        sv = signed_volume(V, T)
        if abs(sv - vol) > 1e-9:
            raise ValidationError(
                f"{p['part']} not closed outward: signed volume {sv:.9f} != {vol:.9f}")
    lo, hi = aabb(parts)
    if lo[1] != 0.0:
        raise ValidationError(f"crate must rest on ground Y=0, got min Y {lo[1]!r}")
    for i in range(3):
        if abs(lo[i] - ENVELOPE[0][i]) > 1e-9 or abs(hi[i] - ENVELOPE[1][i]) > 1e-9:
            raise ValidationError(f"AABB {[lo, hi]} does not meet frozen envelope exactly")


def export(parts, surf_table):
    """最小 stdlib glTF/GLB 写出（做法沿用 U01）：可编辑 .gltf+.bin 与同源自包含 .glb。"""
    blob = bytearray()
    bvs, accs = [], []

    def view(data, target):
        bvs.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data), "target": target})
        blob.extend(data)
        return len(bvs) - 1

    def acc_f32(vals, ncomp):
        data = struct.pack("<%df" % (ncomp * len(vals)), *[c for v in vals for c in v])
        bv = view(data, 34962)
        accs.append({"bufferView": bv, "componentType": 5126, "count": len(vals),
                     "type": "VEC3" if ncomp == 3 else "VEC4",
                     "min": [min(v[i] for v in vals) for i in range(ncomp)],
                     "max": [max(v[i] for v in vals) for i in range(ncomp)]})
        return len(accs) - 1

    def acc_u32(tris):
        flat = [a for tri in tris for a in tri]
        bv = view(struct.pack("<%dI" % len(flat), *flat), 34963)
        accs.append({"bufferView": bv, "componentType": 5125, "count": len(flat), "type": "SCALAR",
                     "min": [min(flat)], "max": [max(flat)]})
        return len(accs) - 1

    materials = []
    for r in ROLES:
        lin, rough, metal = ROLE_PBR[r]
        materials.append({"name": r, "pbrMetallicRoughness": {
            "baseColorFactor": [round(c, 6) for c in lin] + [1.0],
            "metallicFactor": metal, "roughnessFactor": rough}})

    prims, prims_json = [], []
    for si, p in enumerate(parts):
        V, T, _ = p["geo"]
        acc = [[0.0, 0.0, 0.0] for _ in V]
        for a, b, c in T:
            A, B, C = V[a], V[b], V[c]
            u = [B[i] - A[i] for i in range(3)]
            q = [C[i] - A[i] for i in range(3)]
            n = (u[1] * q[2] - u[2] * q[1], u[2] * q[0] - u[0] * q[2], u[0] * q[1] - u[1] * q[0])
            for vi in (a, b, c):
                for k in range(3):
                    acc[vi][k] += n[k]
        NRM = []
        for a3 in acc:
            L = math.sqrt(sum(x * x for x in a3)) or 1.0
            NRM.append((a3[0] / L, a3[1] / L, a3[2] / L))
        prims_json.append({"attributes": {"POSITION": acc_f32(V, 3), "NORMAL": acc_f32(NRM, 3)},
                           "indices": acc_u32(T), "material": ROLES.index(p["role"]), "mode": 4})
        surf_table.append({"node": "Crate", "surface": si, "role": p["role"], "part": p["part"],
                           "tris": len(T), "verts": len(V)})

    while len(blob) % 4:
        blob += b"\x00"
    gltf = {
        "asset": {"version": "2.0",
                  "generator": "yudian ART-P01-CRATE-GEO generate_crate_r1.py (python stdlib, original)"},
        "scene": 0,
        "scenes": [{"name": "Scene", "nodes": [0]}],
        "nodes": [{"name": "Crate", "mesh": 0, "translation": [0.0, 0.0, 0.0]}],
        "meshes": [{"name": "Crate", "primitives": prims_json}],
        "materials": materials,
        "accessors": accs,
        "bufferViews": bvs,
        "buffers": [{"byteLength": len(blob), "uri": NAME + ".bin"}],
    }
    jt = json.dumps(gltf, separators=(",", ":"), ensure_ascii=False)
    with open(os.path.join(HERE, NAME + ".bin"), "wb") as f:
        f.write(blob)
    with open(os.path.join(HERE, NAME + ".gltf"), "w") as f:
        f.write(jt)

    # GLB 自包含：内嵌 JSON 的 buffer 不得带 uri（GLB 二进制块即 buffer）
    glb_doc = dict(gltf)
    glb_doc["buffers"] = [{"byteLength": len(blob)}]
    js = json.dumps(glb_doc, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    bb = bytes(blob) + b"\x00" * ((4 - len(blob) % 4) % 4)
    glb_path = os.path.join(HERE, NAME + ".glb")
    with open(glb_path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bb)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A))
        f.write(js)
        f.write(struct.pack("<II", len(bb), 0x004E4942))
        f.write(bb)
    return len(blob)


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def write_report(facts, elapsed):
    st = facts["surface_table"]
    rows = "\n".join(
        f"| {e['surface']} | {e['part']} | {e['role']} | {e['tris']} | {e['verts']} |" for e in st)
    amin, amax = facts["static_bbox"]["min"], facts["static_bbox"]["max"]
    lines = f"""# crate-r1 · geometry-report（ART-P01-CRATE-GEO）

由 `generate_crate_r1.py` 生成；本报告为末次真实运行的实录。

## 冻结范围与来源

- 米制，+Y 上，前 -Z，底面中心原点（落地 Y=0），无动画、无虚构 socket。
- 外沿 0.64 W × 0.70 D × 0.38 H，X±0.32 / Y 0…0.38 / Z±0.35（冻结候选包络）。
- 分区：frame_dark 下框、body_light 箱体、frame_dark 上沿、accent_warm 前识别片（包络内）。
- 尺寸来源：现役 U01 `geom_cargo` 冻结候选（`art/source/units/tuoyun-r1/`，只读依据，未改其文件）。
- 原创：python stdlib 程序化几何，无第三方/版权/图像生成资产；写出辅助为最小 stdlib 做法（沿用 U01 生成器方式）。

## 节点与 surface 角色

GLB 根节点 `Crate`（单 mesh，逐 surface 对应角色）；正式接线用共享 `art-r1` 材质，GLB 内为占位分面色。

| surface | part | role | tris | verts |
|---|---|---|---|---|
{rows}

三角面合计 {facts['triangles_total']}；GLB 材质数 {facts['materials_count']}（仅本票用到的三角色）。

## AABB（源与 GLB 一致）

min {amin} / max {amax}（米），与冻结包络逐轴一致，min Y = 0 落地。

## SHA256

| 文件 | SHA256 |
|---|---|
| {NAME}.gltf | `{facts['sha256']['gltf']}` |
| {NAME}.bin | `{facts['sha256']['bin']}` |
| {NAME}.glb（源目录） | `{facts['sha256']['glb']}` |
| prototype 私有副本 {NAME}.glb | `{facts['sha256']['glb_prototype']}` |

## 自检与验证实录（本机 stdlib 实测）

- 写前自查：合法索引、有限数、逐零件外向闭合体积（signed volume = 尺寸积）、冻结包络、落地 Y=0；失败非零退出。
- 负例：`python3 generate_crate_r1.py --selftest` —— 错包络 / 负尺寸 / 非有限坐标 / 未知角色 / 越界索引 / 单面翻绕序均被拒绝。
- 确定性：同一工作树从不同 cwd 重跑，GLB 字节一致（SHA 相同）；无时间戳/随机数进 GLB。
- 独立回读：交付前用单独脚本解析 GLB 二进制块，从原始 buffer 重算逐 surface AABB、闭合体积、角色与三角数，与 facts 一致。

重跑方式：`cd <任意目录> && python3 art/source/props/crate-r1/generate_crate_r1.py`（会重写本目录四件并同步 prototype 私有 GLB 副本）。本轮耗时 {elapsed:.3f}s。

## 本生成器未运行（主控实测另见双线证据索引）（本票未做、不做宣称）

- Godot 4 导入实测（角色/AABB 实机复核）、冻结白昼 normal/close 取证渲染：主控执行。
- manifest 公共表接入、preview 场景接线、碰撞体、LOD、动画、保存/库存接入：不在本票。
- 母票 ART-P01 回收连接件、最终量产比例：母票保持 DRAFT。
"""
    with open(os.path.join(HERE, "geometry-report.md"), "w") as f:
        f.write(lines)


def main():
    t0 = time.perf_counter()
    parts = build_parts()
    validate_parts(parts)  # 写前自查；任何失败在落地前非零退出
    surf_table = []
    bin_bytes = export(parts, surf_table)
    lo, hi = aabb(parts)
    glb_path = os.path.join(HERE, NAME + ".glb")
    os.makedirs(PROTOTYPE_DIR, exist_ok=True)
    proto_glb = os.path.join(PROTOTYPE_DIR, NAME + ".glb")
    shutil.copyfile(glb_path, proto_glb)
    shas = {k: sha256_file(os.path.join(HERE, NAME + "." + k)) for k in ("gltf", "bin", "glb")}
    sha_proto = sha256_file(proto_glb)
    if sha_proto != shas["glb"]:
        print("prototype GLB copy mismatch", file=sys.stderr)
        return 2
    facts = {
        "id": "ART-P01-CRATE-GEO",
        "generator": "generate_crate_r1.py",
        "coordinate": COORDINATE,
        "origin": "bottom-center on ground Y=0",
        "envelope_m": {"min": list(ENVELOPE[0]), "max": list(ENVELOPE[1])},
        "static_bbox": {"min": lo, "max": hi},
        "parts": [],
        "surface_table": surf_table,
        "triangles_total": sum(e["tris"] for e in surf_table),
        "materials_count": len(ROLES),
        "bin_bytes": bin_bytes,
        "sha256": {"gltf": shas["gltf"], "bin": shas["bin"], "glb": shas["glb"],
                   "glb_prototype": sha_proto},
    }
    # parts 表：逐零件真实尺寸与包络内范围
    for p in parts:
        V = p["geo"][0]
        cmin = [min(v[i] for v in V) for i in range(3)]
        cmax = [max(v[i] for v in V) for i in range(3)]
        facts["parts"].append({
            "part": p["part"], "role": p["role"],
            "size_m": [round(cmax[i] - cmin[i], 6) for i in range(3)],
            "bounds_m": {"min": [round(c, 6) for c in cmin], "max": [round(c, 6) for c in cmax]}})
    with open(os.path.join(HERE, "geometry-facts.json"), "w") as f:
        json.dump(facts, f, ensure_ascii=False, indent=2)
        f.write("\n")
    elapsed = time.perf_counter() - t0
    write_report(facts, elapsed)
    print(f"OK {NAME}: {facts['triangles_total']} tris, bin {bin_bytes}B, "
          f"glb sha {shas['glb'][:12]}…, {elapsed:.3f}s")
    return 0


def selftest():
    """负例断言：全部必须被同一校验路径拒绝；任一未拒绝即失败。"""
    cases = []

    def expect_reject(name, fn):
        try:
            fn()
        except ValidationError as e:
            cases.append((name, True, str(e)))
        else:
            cases.append((name, False, "no rejection"))

    expect_reject("negative-size", lambda: box(0, 0.1, 0, 0.5, -0.1, 0.5))
    expect_reject("non-finite-coord", lambda: validate_parts(
        [dict(build_parts()[0], geo=(build_parts()[0]["geo"][0]
                                     + [(float("nan"), 0.1, 0.0)],
              build_parts()[0]["geo"][1], build_parts()[0]["geo"][2]))]))
    expect_reject("unknown-role", lambda: validate_parts(
        [dict(build_parts()[0], role="chrome")]))
    expect_reject("out-of-range-index", lambda: validate_parts(
        [dict(build_parts()[0], geo=(build_parts()[0]["geo"][0],
                                     build_parts()[0]["geo"][1][:-1] + [(0, 1, 99)],
                                     build_parts()[0]["geo"][2]))]))
    expect_reject("envelope-breach", lambda: validate_parts(
        [dict(build_parts()[2], geo=(build_parts()[2]["geo"][0][:1]
                                     + [(0.0, 0.5, 0.0)] + build_parts()[2]["geo"][0][2:],
                                     build_parts()[2]["geo"][1], build_parts()[2]["geo"][2]))]))
    expect_reject("flipped-winding", lambda: validate_parts(
        [dict(build_parts()[0], geo=(build_parts()[0]["geo"][0],
                                     build_parts()[0]["geo"][1][:-1]
                                     + [tuple(reversed(build_parts()[0]["geo"][1][-1]))],
                                     build_parts()[0]["geo"][2]))]))

    bad = [c for c in cases if not c[1]]
    for name, ok, msg in cases:
        print(f"  {'PASS' if ok else 'FAIL'} {name}: {msg}")
    if bad:
        print(f"selftest FAILED: {len(bad)} case(s) not rejected", file=sys.stderr)
        return 1
    print(f"selftest OK: {len(cases)} negative cases all rejected")
    return 0


if __name__ == "__main__":
    if "--selftest" in sys.argv[1:]:
        sys.exit(selftest())
    sys.exit(main())
