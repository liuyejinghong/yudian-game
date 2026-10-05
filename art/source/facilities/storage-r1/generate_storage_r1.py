#!/usr/bin/env python3
# ART-F03-GEO · 仓储低保真静态子片 r1（python 标准库，独立可重跑）
#
# 冻结范围：米制、+Y 上、前 -Z、原点 = 地基底面中心。外沿 X±2.25 / Y 0…2.20 / Z±1.60。
# 固定深色地基 4.5×0.12×3.2；两层托板（托顶 Y0.20 / Y1.10，厚 0.06）；四角柱中心
# X±2.17/Z±1.52、截面 0.12；顶侧梁最高 2.20；浅色后侧窄横服务板（center Y1.85）
# 与两侧窄遮护板（X±1.91，中部敞开）；前面 -Z 保持开放。
# RackPayloadDemo 父节点下 12 个 P01 规格视觉箱（X -1.35/0/1.35 × Z -0.70/0.60 ×
# 底 Y 0.20/1.10），每箱独立节点可整体隐藏。槽位仅为均匀比较，不是仓容量或库存。
#
# 箱体尺寸/角色复用现役 P01 crate-r1 冻结候选（只读依据，不改其文件）；本脚本不 import P01、
# 不依赖其输出路径。所有网格为轴对齐盒，导出逐面平法线（硬表面），无动画、无 socket、无游戏状态。
#
# 用法：
#   python3 generate_storage_r1.py             # 正常构建 + 写前全量校验，非法即非零退出
#   python3 generate_storage_r1.py --selftest  # 负例：越包络/非有限/未知角色/缺Payload/越界索引/翻绕序
import hashlib
import json
import math
import os
import shutil
import struct
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
NAME = "storage-r1"
# 私有 GLB 副本目录（本票独占可写）；目录缺失视为环境错误，不静默跳过。
PROTOTYPE_DIR = os.path.abspath(
    os.path.join(HERE, "..", "..", "..", "..", "prototype", "assets", "facilities", NAME))

ROLES = ["frame_dark", "body_light", "accent_warm"]
ROLE_PBR = {  # 线性占位 PBR，与 U01/P01/art-r1 六表同源口径，仅承载分面角色
    "frame_dark": ((0.0401, 0.0578, 0.0694), 0.75, 0.2),
    "body_light": ((0.6818, 0.7140, 0.6718), 0.65, 0.0),
    "accent_warm": ((0.6712, 0.1975, 0.0480), 0.60, 0.0),
}
RACK_ENV = ((-2.25, 0.0, -1.60), (2.25, 2.20, 1.60))   # 货架冻结外沿
CRATE_ENV = ((-0.32, 0.0, -0.35), (0.32, 0.38, 0.35))  # P01 货箱冻结外沿（箱局部坐标）
EPS = 1e-6
COORDINATE = "meter, +Y up, forward -Z, ground Y=0, translations only"
PAYLOAD_PARENT = "RackPayloadDemo"
CRATE_SLOTS = [  # (name, x, bottom_y, z)，冻结示意槽位
    ("Crate_B01", -1.35, 0.20, -0.70), ("Crate_B02", -1.35, 0.20, 0.60),
    ("Crate_B03", 0.00, 0.20, -0.70), ("Crate_B04", 0.00, 0.20, 0.60),
    ("Crate_B05", 1.35, 0.20, -0.70), ("Crate_B06", 1.35, 0.20, 0.60),
    ("Crate_T01", -1.35, 1.10, -0.70), ("Crate_T02", -1.35, 1.10, 0.60),
    ("Crate_T03", 0.00, 1.10, -0.70), ("Crate_T04", 0.00, 1.10, 0.60),
    ("Crate_T05", 1.35, 1.10, -0.70), ("Crate_T06", 1.35, 1.10, 0.60),
]


class ValidationError(Exception):
    """写前自查失败：调用方必须非零退出，不得落地任何文件。"""


def box(cx, cy, cz, sx, sy, sz):
    """轴对齐盒（世界坐标构造），外向 CCW；非正尺寸在构造期即拒绝。"""
    if not all(isinstance(s, (int, float)) and math.isfinite(s) and s > 0 for s in (sx, sy, sz)):
        raise ValidationError(f"box size must be finite and positive, got {sx},{sy},{sz}")
    x, y, z = sx / 2, sy / 2, sz / 2
    v = [(cx + a * x, cy + b * y, cz + c * z)
         for c in (-1, 1) for b in (-1, 1) for a in (-1, 1)]
    t = [(0, 2, 3), (0, 3, 1), (4, 5, 7), (4, 7, 6),
         (4, 1, 5), (4, 0, 1), (2, 7, 3), (2, 6, 7),
         (0, 4, 6), (0, 6, 2), (1, 7, 5), (1, 3, 7)]
    return v, t, sx * sy * sz


# ---------------------------------------------------------------- 冻结候选结构（世界坐标）
def rack_parts():
    parts = [
        ("foundation", "frame_dark", box(0.0, 0.06, 0.0, 4.50, 0.12, 3.20)),
        ("shelf_bottom", "frame_dark", box(0.0, 0.17, 0.0, 4.22, 0.06, 2.92)),
        ("shelf_top", "frame_dark", box(0.0, 1.07, 0.0, 4.22, 0.06, 2.92)),
    ]
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append((f"column_x{int(sx)}_z{int(sz)}", "frame_dark",
                          box(sx * 2.17, 1.16, sz * 1.52, 0.12, 2.08, 0.12)))
    parts += [
        ("beam_x_zm", "frame_dark", box(0.0, 2.14, -1.52, 4.46, 0.12, 0.12)),
        ("beam_x_zp", "frame_dark", box(0.0, 2.14, 1.52, 4.46, 0.12, 0.12)),
        ("beam_z_xm", "frame_dark", box(-2.17, 2.14, 0.0, 0.12, 0.12, 2.92)),
        ("beam_z_xp", "frame_dark", box(2.17, 2.14, 0.0, 0.12, 0.12, 2.92)),
        # 中部敞开，使斜俯视镜头能辨认空位和货箱。
        ("rear_panel", "body_light", box(0.0, 1.85, 1.54, 4.10, 0.25, 0.08)),
        ("canopy_left", "body_light", box(-1.91, 2.17, 0.0, 0.28, 0.06, 2.78)),
        ("canopy_right", "body_light", box(1.91, 2.17, 0.0, 0.28, 0.06, 2.78)),
        ("front_mark", "accent_warm", box(0.0, 0.1275, -1.52, 0.90, 0.015, 0.06)),
    ]
    return parts


def crate_parts():
    """P01 crate-r1 冻结候选（只读依据）：箱局部原点 = 底面中心。"""
    return [
        ("lower_frame", "frame_dark", box(0.0, 0.07, 0.0, 0.59, 0.14, 0.65)),
        ("body", "body_light", box(0.0, 0.23, 0.0, 0.62, 0.18, 0.68)),
        ("top_rim", "frame_dark", box(0.0, 0.35, 0.0, 0.64, 0.06, 0.70)),
        ("id_plate_front", "accent_warm", box(0.16, 0.27, -0.343, 0.16, 0.05, 0.008)),
    ]


def merge_role(parts):
    """同角色多盒合并为单 surface（索引重排）。"""
    V, T = [], []
    for _, _, (v, t, _) in parts:
        T += [(a + len(V), b + len(V), c + len(V)) for a, b, c in t]
        V += v
    return V, T


def signed_volume(V, T):
    s = 0.0
    for a, b, c in T:
        A, B, C = V[a], V[b], V[c]
        s += (A[0] * (B[1] * C[2] - B[2] * C[1])
              - A[1] * (B[0] * C[2] - B[2] * C[0])
              + A[2] * (B[0] * C[1] - B[1] * C[0])) / 6.0
    return s


def part_aabb(v):
    return ([min(p[i] for p in v) for i in range(3)], [max(p[i] for p in v) for i in range(3)])


def validate_parts(parts, env, label):
    """写前自查（逐零件）：未知角色 / 非有限 / 包络越界 / 越界索引 / 闭合外向体积。"""
    (x0, y0, z0), (x1, y1, z1) = env
    for p in parts:
        if p["role"] not in ROLES:
            raise ValidationError(f"unknown role: {p['role']!r}")
        V, T, vol = p["geo"]
        for i, v in enumerate(V):
            if len(v) != 3 or not all(isinstance(c, float) and math.isfinite(c) for c in v):
                raise ValidationError(f"{label} {p['part']} vert {i} not finite 3-tuple: {v!r}")
            if not (x0 - EPS <= v[0] <= x1 + EPS and y0 - EPS <= v[1] <= y1 + EPS
                    and z0 - EPS <= v[2] <= z1 + EPS):
                raise ValidationError(f"{label} {p['part']} vert {i} {v} breaches envelope")
        for ti, t in enumerate(T):
            if any(not isinstance(k, int) or not 0 <= k < len(V) for k in t):
                raise ValidationError(f"{label} {p['part']} tri {ti} out-of-range index: {t}")
        sv = signed_volume(V, T)
        if abs(sv - vol) > 1e-9:
            raise ValidationError(f"{label} {p['part']} not closed outward: {sv:.9f} != {vol:.9f}")


def validate_scene(scene):
    """结构自查：缺 Payload 父 / 槽位数量 / 每箱 surface 角色 / 空、有示意包络与落地。"""
    payload = [c for c in scene["children"] if c["name"] == PAYLOAD_PARENT]
    if len(payload) != 1:
        raise ValidationError(f"scene must have exactly one {PAYLOAD_PARENT} parent, got {len(payload)}")
    crates = payload[0]["children"]
    if len(crates) != len(CRATE_SLOTS):
        raise ValidationError(f"payload must hold {len(CRATE_SLOTS)} crates, got {len(crates)}")
    want = ["frame_dark", "body_light", "frame_dark", "accent_warm"]
    for c in crates:
        if [s[0] for s in c["surfaces"]] != want:
            raise ValidationError(f"{c['name']} surface roles != P01 crate layout {want}")
    lo, hi = scene_aabb(scene, with_payload=False)
    for i in range(3):
        if abs(lo[i] - RACK_ENV[0][i]) > 1e-9 or abs(hi[i] - RACK_ENV[1][i]) > 1e-9:
            raise ValidationError(f"static AABB {[lo, hi]} != frozen rack envelope")
    if lo[1] != 0.0:
        raise ValidationError(f"rack must rest on ground Y=0, got min Y {lo[1]!r}")
    llo, lhi = scene_aabb(scene, with_payload=True)
    for i in range(3):
        if llo[i] < lo[i] - 1e-9 or lhi[i] > hi[i] + 1e-9:
            raise ValidationError(f"loaded AABB {[llo, lhi]} exceeds static {[lo, hi]}")
    return (lo, hi), (llo, lhi)


def scene_aabb(node, with_payload, offset=(0.0, 0.0, 0.0)):
    """世界 AABB：节点几何按自身及祖先平移累计（箱体源为局部坐标，落位靠 translation）。"""
    if not with_payload and node["name"] == PAYLOAD_PARENT:
        return None
    w = tuple(offset[k] + node["translation"][k] for k in range(3))
    boxes = []
    if node.get("surfaces"):
        pts = [(v[0] + w[0], v[1] + w[1], v[2] + w[2]) for s in node["surfaces"] for v in s[1]]
        boxes.append(part_aabb(pts))
    for c in node.get("children", []):
        b = scene_aabb(c, with_payload, w)
        if b:
            boxes.append(b)
    if not boxes:
        return None
    return ([min(b[0][i] for b in boxes) for i in range(3)],
            [max(b[1][i] for b in boxes) for i in range(3)])


def build_scene(with_payload=True):
    rp = rack_parts()
    by_part = {p[0]: p for p in rp}
    # 结构节点：frame_dark 合并面（地基/托板/柱/梁）+ 后侧窄横服务板 + 两侧窄遮护 + 前标记
    scene = {"name": "Storage", "translation": (0.0, 0.0, 0.0), "children": [
        {"name": "Structure", "translation": (0.0, 0.0, 0.0), "children": [],
         "surfaces": [("frame_dark", *merge_role([p for p in rp if p[1] == "frame_dark"]))]},
    ]}
    singles = [
        ("RearPanel", "body_light", [by_part["rear_panel"]]),
        ("TopCanopy", "body_light", [by_part["canopy_left"], by_part["canopy_right"]]),
        ("FrontMark", "accent_warm", [by_part["front_mark"]]),
    ]
    for nm, role, parts in singles:
        scene["children"].append({"name": nm, "translation": (0.0, 0.0, 0.0), "children": [],
                                  "surfaces": [(role, *merge_role(parts))]})
    payload = {"name": PAYLOAD_PARENT, "translation": (0.0, 0.0, 0.0), "children": []}
    for nm, x, y, z in CRATE_SLOTS:
        payload["children"].append(
            {"name": nm, "translation": (x, y, z), "children": [],
             "surfaces": [(r, *g[:2]) for _, r, g in crate_parts()]})
    if with_payload:
        scene["children"].append(payload)
    return scene


def flat_split(V, T):
    """逐三角复制顶点 + 精确面法线：轴对齐盒得到硬表面平法线。"""
    fv, ft = [], []
    for a, b, c in T:
        A, B, C = V[a], V[b], V[c]
        u = [B[i] - A[i] for i in range(3)]
        q = [C[i] - A[i] for i in range(3)]
        n = (u[1] * q[2] - u[2] * q[1], u[2] * q[0] - u[0] * q[2], u[0] * q[1] - u[1] * q[0])
        L = math.sqrt(sum(x * x for x in n)) or 1.0
        n = (n[0] / L, n[1] / L, n[2] / L)
        ft.append((len(fv), len(fv) + 1, len(fv) + 2))
        fv += [(A, n), (B, n), (C, n)]
    return fv, ft


def export(scene, surf_table, node_list):
    """最小 stdlib glTF/GLB 写出（做法沿用 U01/P01）：可编辑 .gltf+.bin 与同源自包含 .glb。"""
    blob = bytearray()
    bvs, accs = [], []

    def view(data, target):
        bvs.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data), "target": target})
        blob.extend(data)
        return len(bvs) - 1

    def acc_f32(vals):
        data = struct.pack("<%df" % (3 * len(vals)), *[c for v in vals for c in v])
        bv = view(data, 34962)
        accs.append({"bufferView": bv, "componentType": 5126, "count": len(vals), "type": "VEC3",
                     "min": [min(v[i] for v in vals) for i in range(3)],
                     "max": [max(v[i] for v in vals) for i in range(3)]})
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

    nodes_json, meshes = [], []

    def emit(node, parent_idx):
        w = node["translation"]
        idx = len(nodes_json)
        nodes_json.append({"name": node["name"],
                           "translation": [round(w[0], 6), round(w[1], 6), round(w[2], 6)]})
        node_list.append(node["name"])
        if parent_idx is None:
            pass
        else:
            nodes_json[parent_idx].setdefault("children", []).append(idx)
        if node.get("surfaces"):
            prims = []
            for si, (role, V, T) in enumerate(node["surfaces"]):
                # 源几何即节点局部坐标（结构节点平移为 0，箱体局部 = 底面中心原点），
                # 落位由 glTF translation 承载；不得再减平移（否则双偏移）。
                fv, ft = flat_split(V, T)
                POS = [p for p, _ in fv]
                NRM = [n for _, n in fv]
                prims.append({"attributes": {"POSITION": acc_f32(POS), "NORMAL": acc_f32(NRM)},
                              "indices": acc_u32(ft), "material": ROLES.index(role), "mode": 4})
                surf_table.append({"node": node["name"], "surface": si, "role": role,
                                   "tris": len(T), "verts": len(POS)})
            meshes.append({"name": node["name"], "primitives": prims})
            nodes_json[idx]["mesh"] = len(meshes) - 1
        for c in node.get("children", []):
            emit(c, idx)
        return idx

    root_idx = emit(scene, None)
    while len(blob) % 4:
        blob += b"\x00"
    gltf = {
        "asset": {"version": "2.0",
                  "generator": "yudian ART-F03-GEO generate_storage_r1.py (python stdlib, original)"},
        "scene": 0,
        "scenes": [{"name": "Scene", "nodes": [root_idx]}],
        "nodes": nodes_json,
        "meshes": meshes,
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
    with open(os.path.join(HERE, NAME + ".glb"), "wb") as f:
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
    tri = facts["triangles_total"]
    a = facts["static_bbox"]["min"]; b = facts["static_bbox"]["max"]
    la = facts["loaded_bbox"]["min"]; lb = facts["loaded_bbox"]["max"]
    lines = f"""# storage-r1 · geometry-report（ART-F03-GEO）

由 `generate_storage_r1.py` 生成；本报告为末次真实运行的实录。

## 冻结范围与来源

- 米制，+Y 上，前 -Z，原点 = 地基底面中心，落地 Y=0；无动画、无 socket、无游戏状态。
- 外沿 X±2.25 / Y 0…2.20 / Z±1.60：地基 4.5×0.12×3.2（frame_dark），托板顶 Y0.20/Y1.10 厚 0.06，
  四角柱中心 X±2.17/Z±1.52 截面 0.12，顶侧梁最高 2.20；浅色后侧窄横服务板（center Y1.85）与
  两侧窄遮护板（X±1.91，中部敞开）（body_light）；前面 -Z 开放；accent_warm 前取放标记仅为几何服务方向。
- 修订 r1.1（主控 Godot 实拍 REWORK 后冻结）：原整面后服务板+满顶遮护在 yaw0 固定机位遮挡全部示意货物，
  空/有图几乎一致；改为后侧窄横服务板与两侧窄遮护。地基、柱/梁/托板、12 箱位、单位/包络/角色不变。
- 箱示意：{PAYLOAD_PARENT} 父下 12 个 P01 规格视觉箱（X -1.35/0/1.35 × Z -0.70/0.60 × 底 Y0.20/1.10），
  每箱独立节点可整体隐藏；形体最高 Y1.48。槽位非仓容量/库存。
- 箱体尺寸/角色复用现役 P01 crate-r1 冻结候选（只读依据，未改其文件）；导出辅助为最小 stdlib
  做法（沿用 U01/P01 方式）。原创程序化几何，无第三方/版权/图像生成资产。
- 所有网格为轴对齐盒，逐面平法线（硬表面）。

## 节点与 surface 角色

根 Storage → Structure（frame_dark 合并 11 盒）、RearPanel（body_light 窄横板）、TopCanopy
（body_light，两块窄遮护合并单 surface）、FrontMark（accent_warm）、{PAYLOAD_PARENT}（空父，
12 箱各 4 surface：frame_dark/body_light/frame_dark/accent_warm）。
节点 {facts['node_count']} 个，mesh {facts['mesh_count']} 个，surface {len(facts['surface_table'])} 个，
三角面合计 {tri}；GLB 材质数 {facts['materials_count']}（三角色，正式接线用共享 art-r1 材质）。
逐 surface 明细见 geometry-facts.json surface_table；12 箱槽位见 payload_slots。

## 包络（源与 GLB 一致）

- 空架 static AABB：min {a} / max {b}，与冻结外沿逐轴一致，min Y=0。
- 含示意 loaded AABB：min {la} / max {lb}（payload 全部位于结构包络内，两者重合）。

## SHA256

| 文件 | SHA256 |
|---|---|
| {NAME}.gltf | `{facts['sha256']['gltf']}` |
| {NAME}.bin | `{facts['sha256']['bin']}` |
| {NAME}.glb（源目录） | `{facts['sha256']['glb']}` |
| prototype 私有副本 {NAME}.glb | `{facts['sha256']['glb_prototype']}` |

## 自检与验证实录（本机 stdlib 实测）

- 写前自查：逐零件合法索引、有限数、外向闭合体积、冻结包络（货架/箱各自口径）、落地 Y0；
  结构自查：{PAYLOAD_PARENT} 唯一、12 箱、逐箱 P01 surface 角色、空/有示意包络关系。失败非零退出。
- 负例：`python3 generate_storage_r1.py --selftest` —— 越包络 / 非有限 / 未知角色 / 缺 Payload 父 /
  越界索引 / 单面翻绕序均被拒绝。
- 确定性：从不同 cwd 重跑 GLB 字节一致（SHA 相同）；无时间戳/随机数进 GLB。
- 独立回读：单独脚本解析 GLB 二进制块，重算逐 surface AABB、闭合体积、角色、三角数与
  逐顶点法线轴向（硬表面），与 facts 一致。

重跑方式：`cd <任意目录> && python3 art/source/facilities/storage-r1/generate_storage_r1.py`
（重写本目录四件并同步 prototype 私有 GLB 副本）。本轮耗时 {elapsed:.3f}s。

## 本生成器未运行（主控实测另见双线证据索引）

- Godot 4 导入实测、冻结白昼 normal/close 空/有对照取证：主控执行。
- STATE/preview 接线、仓容量/库存/存档/正式场景接入、动画、运行状态、正式 socket：未制作。
"""
    with open(os.path.join(HERE, "geometry-report.md"), "w") as f:
        f.write(lines)


def main():
    t0 = time.perf_counter()
    scene = build_scene()
    validate_parts([{"part": p[0], "role": p[1], "geo": p[2]} for p in rack_parts()],
                   RACK_ENV, "rack")
    for c in [c for cn in scene["children"] if cn["name"] == PAYLOAD_PARENT for c in cn["children"]]:
        validate_parts([{"part": p[0], "role": r, "geo": g} for p, r, g in crate_parts()],
                       CRATE_ENV, c["name"])
    static, loaded = validate_scene(scene)  # 写前自查；任何失败在落地前非零退出
    surf_table, node_list = [], []
    bin_bytes = export(scene, surf_table, node_list)
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
        "id": "ART-F03-GEO",
        "generator": "generate_storage_r1.py",
        "coordinate": COORDINATE,
        "origin": "foundation bottom-center on ground Y=0",
        "envelope_m": {"min": list(RACK_ENV[0]), "max": list(RACK_ENV[1])},
        "static_bbox": {"min": static[0], "max": static[1]},
        "loaded_bbox": {"min": loaded[0], "max": loaded[1]},
        "payload_parent": PAYLOAD_PARENT,
        "payload_slots": [{"node": n, "x": x, "bottom_y": y, "z": z} for n, x, y, z in CRATE_SLOTS],
        "rack_parts": [{"part": p[0], "role": p[1],
                        "bounds_m": {"min": part_aabb(p[2][0])[0], "max": part_aabb(p[2][0])[1]}}
                       for p in rack_parts()],
        "nodes": node_list,
        "node_count": len(node_list),
        "mesh_count": len([n for n in node_list if n not in ("Storage", PAYLOAD_PARENT)]),
        "surface_table": surf_table,
        "triangles_total": sum(e["tris"] for e in surf_table),
        "materials_count": len(ROLES),
        "bin_bytes": bin_bytes,
        "sha256": {"gltf": shas["gltf"], "bin": shas["bin"], "glb": shas["glb"],
                   "glb_prototype": sha_proto},
    }
    with open(os.path.join(HERE, "geometry-facts.json"), "w") as f:
        json.dump(facts, f, ensure_ascii=False, indent=2)
        f.write("\n")
    elapsed = time.perf_counter() - t0
    write_report(facts, elapsed)
    print(f"OK {NAME}: {facts['triangles_total']} tris, {facts['node_count']} nodes, "
          f"bin {bin_bytes}B, glb sha {shas['glb'][:12]}…, {elapsed:.3f}s")
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

    rp = rack_parts()
    expect_reject("envelope-breach", lambda: validate_parts(
        [{"part": rp[0][0], "role": rp[0][1],
          "geo": box(0.0, 0.06, 0.0, 4.60, 0.12, 3.20)}], RACK_ENV, "rack"))
    expect_reject("non-finite-coord", lambda: validate_parts(
        [{"part": rp[0][0], "role": rp[0][1],
          "geo": (rp[0][2][0] + [(float("nan"), 0.06, 0.0)], rp[0][2][1], rp[0][2][2])}],
        RACK_ENV, "rack"))
    expect_reject("unknown-role", lambda: validate_parts(
        [{"part": rp[0][0], "role": "chrome", "geo": rp[0][2]}], RACK_ENV, "rack"))
    expect_reject("missing-payload-parent", lambda: validate_scene(build_scene(with_payload=False)))
    expect_reject("out-of-range-index", lambda: validate_parts(
        [{"part": rp[0][0], "role": rp[0][1],
          "geo": (rp[0][2][0], rp[0][2][1][:-1] + [(0, 1, 99)], rp[0][2][2])}], RACK_ENV, "rack"))
    expect_reject("flipped-winding", lambda: validate_parts(
        [{"part": rp[0][0], "role": rp[0][1],
          "geo": (rp[0][2][0], rp[0][2][1][:-1] + [tuple(reversed(rp[0][2][1][-1]))],
                  rp[0][2][2])}], RACK_ENV, "rack"))

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
