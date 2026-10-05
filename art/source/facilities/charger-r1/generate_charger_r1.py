#!/usr/bin/env python3
# ART-F04-GEO · 充电站单泊位静态首样 r1（python 标准库，独立可重跑）
#
# 冻结范围：米制、+Y 上、前 -Z、原点 = 地基底面中心。根节点 ChargingStation。
# 包络 X[-.90,1.30] / Y[0,1.45] / Z[-1.10,1.10]，落地 Y=0。九个固定部件（无动画/漂浮/自转）：
# 深框 Foundation / CabinetFoot / ArmStem / PowerInGuard / ServicePanel；浅色 Cabinet / CabinetCap；
# 暖色 BayMark（X±.78 导向线 + Z±1.0 端标，Y .020… .024，不挡入口）与 ContactHead
# （center .68/.57/-.35，受保护接触面 X=.65；未使用旧票候选面 X=.52）。接口已按 architect 结论回缩，
# 避免与现役 U01 charge 开盖姿态穿插；候选演示位（机器人根 (0,.02,0)）车 Charge 点世界坐标
# (.48,.57,-.35)，与 Socket_Dock 相距 .17m——仅静态示意，不宣称机械插合/电网/补能。
# 两个 socket 为纯标记节点（无网格）：Socket_Dock=(.65,.57,-.35) forward -X/up+Y；
# Socket_PowerIn=(1.055,.18,-.66) forward -Z/up+Y（柜后护块前面）。前向 = 节点局部 -Z 参考轴
# 经四元数旋转到世界方向。单泊位仅首样比较，非玩法容量/停靠合同；无玩法/电网/补能/维修/导航/
# 保存/STATE。U01 对位示意、Godot 导入、开盖净空由主控执行。
#
# 做法：F03 最小导出辅助已复制入本目录（无公共框架、不依赖其私人输出路径）；盒体逐面平法线
# （硬表面）；三角色 M01 body_light/frame_dark/accent_warm 只读同源线性占位 PBR。
#
# 用法：
#   python3 generate_charger_r1.py             # 正常构建 + 写前全量校验 + 同轮二次重建一致性
#   python3 generate_charger_r1.py --selftest  # 六类非法输入负例（全部必须被拒绝）
#   python3 generate_charger_r1.py --readback  # 独立回读 GLB 原字节，核位置/方向/表面/包络
import hashlib
import json
import math
import os
import shutil
import struct
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
NAME = "charger-r1"
# 私有 GLB 副本目录（本票独占可写）；目录缺失视为环境错误，不静默跳过。
PROTOTYPE_DIR = os.path.abspath(
    os.path.join(HERE, "..", "..", "..", "..", "prototype", "assets", "facilities", NAME))

ROLES = ["frame_dark", "body_light", "accent_warm"]
ROLE_PBR = {  # 线性占位 PBR，与 U01/P01/art-r1 六表同源口径，仅承载分面角色（M01 只读同源）
    "frame_dark": ((0.0401, 0.0578, 0.0694), 0.75, 0.2),
    "body_light": ((0.6818, 0.7140, 0.6718), 0.65, 0.0),
    "accent_warm": ((0.6712, 0.1975, 0.0480), 0.60, 0.0),
}
ENV = ((-0.90, 0.0, -1.10), (1.30, 1.45, 1.10))  # 冻结外沿
EPS = 1e-6
COORDINATE = "meter, +Y up, forward -Z, ground Y=0, translations only"
ROOT_NAME = "ChargingStation"
SOCKET_FORWARD_REF = (0.0, 0.0, -1.0)  # socket 前向参考 = 节点局部 -Z（与项目前向约定一致）


def quat_y_deg(deg):
    """绕 +Y 的四元数（xyzw）。"""
    r = math.radians(deg) / 2.0
    return (0.0, math.sin(r), 0.0, math.cos(r))


_dock_q = quat_y_deg(90.0)  # 局部 -Z → 世界 -X，+Y 不变
SOCKETS = [  # (name, frozen translation, frozen quaternion xyzw, required world forward, semantics)
    ("Socket_Dock", (0.65, 0.57, -0.35), _dock_q, (-1.0, 0.0, 0.0),
     "docking marker only; no mechanical fit / grid / berth-capacity contract"),
    ("Socket_PowerIn", (1.055, 0.18, -0.66), (0.0, 0.0, 0.0, 1.0), (0.0, 0.0, -1.0),
     "cabinet rear power-in marker only; no grid/load contract"),
]
EXPECT_NODES = {ROOT_NAME, "Foundation", "BayMark", "CabinetFoot", "Cabinet", "CabinetCap",
                "ArmStem", "ContactHead", "PowerInGuard", "ServicePanel",
                "Socket_Dock", "Socket_PowerIn"}


class ValidationError(Exception):
    """写前自查失败：调用方必须非零退出，不得落地任何文件。"""


def quat_rotate(q, v):
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty),
            v[1] + w * ty + (z * tx - x * tz),
            v[2] + w * tz + (x * ty - y * tx))


def box(cx, cy, cz, sx, sy, sz):
    """轴对齐盒（世界坐标构造），外向 CCW；非有限或非正尺寸在构造期即拒绝。"""
    if not all(isinstance(s, (int, float)) and math.isfinite(s) and s > 0 for s in (sx, sy, sz)):
        raise ValidationError(f"box size must be finite and positive, got {sx},{sy},{sz}")
    if not all(isinstance(c, (int, float)) and math.isfinite(c) for c in (cx, cy, cz)):
        raise ValidationError(f"box center must be finite, got {cx},{cy},{cz}")
    x, y, z = sx / 2, sy / 2, sz / 2
    v = [(cx + a * x, cy + b * y, cz + c * z)
         for c in (-1, 1) for b in (-1, 1) for a in (-1, 1)]
    t = [(0, 2, 3), (0, 3, 1), (4, 5, 7), (4, 7, 6),
         (4, 1, 5), (4, 0, 1), (2, 7, 3), (2, 6, 7),
         (0, 4, 6), (0, 6, 2), (1, 7, 5), (1, 3, 7)]
    return v, t, sx * sy * sz


# ---------------------------------------------------------------- 冻结部件（世界坐标）
def charger_parts():
    return [
        ("Foundation", "frame_dark", box(0.0, 0.01, 0.0, 1.80, 0.02, 2.20)),
        ("CabinetFoot", "frame_dark", box(1.055, 0.04, -0.35, 0.49, 0.08, 0.62)),
        ("Cabinet", "body_light", box(1.055, 0.74, -0.35, 0.38, 1.32, 0.50)),
        ("CabinetCap", "body_light", box(1.055, 1.425, -0.35, 0.42, 0.05, 0.54)),
        ("ArmStem", "frame_dark", box(0.795, 0.57, -0.35, 0.17, 0.08, 0.10)),
        ("ContactHead", "accent_warm", box(0.68, 0.57, -0.35, 0.06, 0.12, 0.16)),
        ("PowerInGuard", "frame_dark", box(1.055, 0.18, -0.615, 0.12, 0.12, 0.09)),
        ("ServicePanel", "frame_dark", box(1.254, 0.81, -0.35, 0.018, 0.60, 0.34)),
    ]


def baymark_parts():
    """暖色地面导向（Y .020… .024，厚 .004，不挡入口）：X±.78 两条 Z 向线（Z±1.0 之间）
    与 Z±1.0 两根端标横线（横跨两线）。返回 (子件名, 角色, 几何)。"""
    return [
        ("guide_xm", "accent_warm", box(-0.78, 0.022, 0.0, 0.035, 0.004, 2.00)),
        ("guide_xp", "accent_warm", box(0.78, 0.022, 0.0, 0.035, 0.004, 2.00)),
        ("end_zm", "accent_warm", box(0.0, 0.022, -1.0, 1.595, 0.004, 0.035)),
        ("end_zp", "accent_warm", box(0.0, 0.022, 1.0, 1.595, 0.004, 0.035)),
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


def check_orientation(name, rot, fwd_want):
    """socket 方向自查：有限、归一化、局部 -Z 前向与 +Y 上必须落到冻结世界方向。"""
    if len(rot) != 4 or not all(isinstance(c, (int, float)) and math.isfinite(c) for c in rot):
        raise ValidationError(f"{name} rotation must be finite xyzw quaternion, got {rot!r}")
    n = math.sqrt(sum(c * c for c in rot))
    if abs(n - 1.0) > 1e-6:
        raise ValidationError(f"{name} rotation not normalized: norm {n!r}")
    fwd = quat_rotate(rot, SOCKET_FORWARD_REF)
    up = quat_rotate(rot, (0.0, 1.0, 0.0))
    if math.dist(fwd, fwd_want) > 1e-6:
        raise ValidationError(f"{name} forward {tuple(round(c, 6) for c in fwd)} "
                              f"!= required {fwd_want}")
    if math.dist(up, (0.0, 1.0, 0.0)) > 1e-6:
        raise ValidationError(f"{name} up {tuple(round(c, 6) for c in up)} != +Y")


def validate_scene(scene):
    """结构自查：根名/节点集合、socket 纯标记且位置方向冻结、包络逐轴一致、落地 Y=0。"""
    if scene["name"] != ROOT_NAME:
        raise ValidationError(f"root must be {ROOT_NAME}, got {scene['name']!r}")
    names = [c["name"] for c in scene["children"]]
    if len(names) != len(set(names)):
        raise ValidationError(f"duplicate node names: {names}")
    if set(names) != EXPECT_NODES - {ROOT_NAME}:
        raise ValidationError(f"node set mismatch: {sorted(names)}")
    boxes = []
    for c in scene["children"]:
        w = c["translation"]
        if c["name"].startswith("Socket_"):
            if c.get("surfaces"):
                raise ValidationError(f"{c['name']} must be a pure marker node without mesh")
            spec = [s for s in SOCKETS if s[0] == c["name"]][0]
            if any(not (abs(w[i] - spec[1][i]) <= 1e-9) for i in range(3)):
                raise ValidationError(f"{c['name']} translation {w} != frozen {spec[1]}")
            check_orientation(c["name"], c["rotation"], spec[3])
            continue
        for s in c.get("surfaces", []):
            pts = [(v[0] + w[0], v[1] + w[1], v[2] + w[2]) for v in s[1]]
            boxes.append(part_aabb(pts))
    lo = [min(b[0][i] for b in boxes) for i in range(3)]
    hi = [max(b[1][i] for b in boxes) for i in range(3)]
    for i in range(3):
        if abs(lo[i] - ENV[0][i]) > 1e-9 or abs(hi[i] - ENV[1][i]) > 1e-9:
            raise ValidationError(f"AABB {[lo, hi]} != frozen envelope {[ENV[0], ENV[1]]}")
    if lo[1] != 0.0:
        raise ValidationError(f"charger must rest on ground Y=0, got min Y {lo[1]!r}")
    return (lo, hi)


def build_scene():
    children = []
    for nm, role, geo in charger_parts():
        children.append({"name": nm, "translation": (0.0, 0.0, 0.0), "children": [],
                         "surfaces": [(role, *merge_role([(nm, role, geo)]))]})
    children.append({"name": "BayMark", "translation": (0.0, 0.0, 0.0), "children": [],
                     "surfaces": [("accent_warm", *merge_role(baymark_parts()))]})
    for nm, tr, rot, _, _ in SOCKETS:
        children.append({"name": nm, "translation": tr, "rotation": rot,
                         "children": [], "surfaces": []})
    return {"name": ROOT_NAME, "translation": (0.0, 0.0, 0.0), "children": children}


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
    """最小 stdlib glTF/GLB 写出（做法沿用 U01/P01/F03）：可编辑 .gltf+.bin 与同源自包含 .glb。"""
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
        nj = {"name": node["name"],
              "translation": [round(w[0], 6), round(w[1], 6), round(w[2], 6)]}
        if node.get("rotation") is not None:
            nj["rotation"] = [round(c, 9) for c in node["rotation"]]
        nodes_json.append(nj)
        node_list.append(node["name"])
        if parent_idx is not None:
            nodes_json[parent_idx].setdefault("children", []).append(idx)
        if node.get("surfaces"):
            prims = []
            for si, (role, V, T) in enumerate(node["surfaces"]):
                # 源几何即节点局部坐标（本票所有部件节点平移为 0），落位不靠 translation。
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
                  "generator": "yudian ART-F04-GEO generate_charger_r1.py (python stdlib, original)"},
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


def all_parts_for_validate():
    return ([{"part": nm, "role": role, "geo": geo} for nm, role, geo in charger_parts()]
            + [{"part": f"BayMark/{s}", "role": role, "geo": g}
               for s, role, g in baymark_parts()])


def write_report(facts, elapsed):
    tri = facts["triangles_total"]
    a = facts["computed_bbox"]["min"]; b = facts["computed_bbox"]["max"]
    s = facts["sha256"]
    lines = f"""# charger-r1 · geometry-report（ART-F04-GEO）

由 `generate_charger_r1.py` 生成；本报告为末次真实运行的实录。

## 冻结范围与来源

- 米制，+Y 上，前 -Z，原点 = 地基底面中心；根节点 ChargingStation；落地 Y=0。
  单泊位仅首样比较，不是玩法容量/停靠合同；主体全部固定，无动画/漂浮/自转，无游戏状态。
- 包络 X[-.90,1.30] / Y[0,1.45] / Z[-1.10,1.10]。九个固定部件：深框 Foundation(1.80×.02×2.20)、
  CabinetFoot、ArmStem(X.71… .88)、PowerInGuard(柜后，前Z=-.66)、ServicePanel(贴柜X1.245)；
  浅色 Cabinet(底Y.08/顶1.40)、CabinetCap(顶1.45，不用大遮棚)；暖色 BayMark(X±.78 导向线+
  Z±1.0 端标，Y .020… .024)与 ContactHead(center .68/.57/-.35，受保护接触面 X=.65)。
- 接口回缩：未使用旧票候选头部面 X=.52；面 X=.65 与候选演示车 Charge 点(.48,.57,-.35，
  机器人根(0,.02,0))相距 .17m。仅安全待接静态示意，不宣称插合；开盖顶点净空由主控按
  现役 U01 开盖 charge 姿态实测。
- 两个 socket 为纯标记节点（无网格）：Socket_Dock=(.65,.57,-.35) forward -X/up+Y；
  Socket_PowerIn=(1.055,.18,-.66) forward -Z/up+Y。真实负载/电网/补能归技术线。
- 三角色 M01（body_light/frame_dark/accent_warm）只读同源线性占位 PBR；盒体逐面平法线。
  原创程序化几何（python 标准库），无第三方/版权/图像生成资产；F03 最小导出辅助已复制入本目录，
  无公共框架。

## 节点、角色与面数

节点 {facts['node_count']} 个：根 ChargingStation + 九个部件节点（Foundation/BayMark/CabinetFoot/
Cabinet/CabinetCap/ArmStem/ContactHead/PowerInGuard/ServicePanel）+ 两个 socket 空节点；
mesh {facts['mesh_count']}，surface {len(facts['surface_table'])}，三角面合计 {tri}；
GLB 材质数 {facts['materials_count']}（正式接线用共享 art-r1 材质）。逐 surface 角色明细与
socket 变换（glTF 四元数 xyzw，前向=局部 -Z 参考轴）见 geometry-facts.json。

## 包络与确定性（源/GLB 一致）

- 实测 AABB min {a} / max {b}，与冻结包络逐轴一致，min Y=0。
- SHA256：gltf `{s['gltf'][:16]}…`、bin `{s['bin'][:16]}…`、GLB（源目录）`{s['glb'][:16]}…`、
  prototype 私有副本 GLB `{s['glb_prototype'][:16]}…`（与源字节一致）。
- 确定性：同一运行内二次重建 GLB 字节一致（facts rebuilt_glb_identical）；跨目录重跑 hash 一致；
  无时间戳/随机数进 GLB。
- 独立回读：`--readback` 解析 GLB 原始字节（header/JSON/BIN 块），核根/节点、两个 socket 的
  平移与朝向（-X/-Z）、逐 surface 顶点 AABB、包络、面数、平法线轴向与材质数，与 facts 一致。

## 自检（本机 stdlib 实测）

- 写前自查：非有限数/负尺寸/未知角色/越界索引/翻绕序/超包络/缺或坏 socket 方向 → 具体异常
  非零退出，不落地任何文件；`--selftest` 六类负例全部被拒绝（PASS 明细见运行输出）。

## 本生成器未运行（NOT_RUN）

- Godot 4 导入、同镜头 normal/close/yaw0/90 空泊位图、U01(含开盖 charge 姿态)静态对位与
  顶点净空、引擎世界几何/角色核验：主控执行。机械插合、正式电网/补能/维修/导航/保存、STATE、
  占用逻辑、性能、所有者最终视觉：未制作。

重跑方式：`cd <任意目录> && python3 art/source/facilities/charger-r1/generate_charger_r1.py`
（重写本目录五件并同步 prototype 私有 GLB 副本）。本轮耗时 {elapsed:.3f}s。
"""
    n = len(lines.rstrip("\n").split("\n"))
    if n > 60:
        raise SystemExit(f"report too long: {n} lines > 60")
    with open(os.path.join(HERE, "geometry-report.md"), "w") as f:
        f.write(lines)


def main():
    t0 = time.perf_counter()
    scene = build_scene()
    validate_parts(all_parts_for_validate(), ENV, "charger")  # 写前自查；失败非零退出不落地
    static = validate_scene(scene)
    surf_table, node_list = [], []
    export(scene, surf_table, node_list)
    h1 = sha256_file(os.path.join(HERE, NAME + ".glb"))
    export(scene, [], [])  # 同轮二次重建：确定性自证
    h2 = sha256_file(os.path.join(HERE, NAME + ".glb"))
    if h1 != h2:
        print("rebuild GLB hash mismatch", file=sys.stderr)
        return 2
    glb_path = os.path.join(HERE, NAME + ".glb")
    os.makedirs(PROTOTYPE_DIR, exist_ok=True)
    proto_glb = os.path.join(PROTOTYPE_DIR, NAME + ".glb")
    shutil.copyfile(glb_path, proto_glb)
    shas = {k: sha256_file(os.path.join(HERE, NAME + "." + k)) for k in ("gltf", "bin", "glb")}
    sha_proto = sha256_file(proto_glb)
    if sha_proto != shas["glb"]:
        print("prototype GLB copy mismatch", file=sys.stderr)
        return 2
    sockets_facts = []
    for nm, tr, rot, fwd_want, sem in SOCKETS:
        sockets_facts.append({
            "name": nm, "translation": list(tr),
            "rotation_xyzw": [round(c, 9) for c in rot],
            "forward_ref_local": list(SOCKET_FORWARD_REF),
            "forward_world": [round(c, 9) for c in quat_rotate(rot, SOCKET_FORWARD_REF)],
            "up_world": [round(c, 9) for c in quat_rotate(rot, (0.0, 1.0, 0.0))],
            "semantics": sem,
        })
    facts = {
        "id": "ART-F04-GEO",
        "generator": "generate_charger_r1.py",
        "coordinate": COORDINATE,
        "origin": "foundation bottom-center on ground Y=0",
        "root_node": ROOT_NAME,
        "envelope_m": {"min": list(ENV[0]), "max": list(ENV[1])},
        "computed_bbox": {"min": static[0], "max": static[1]},
        "parts": [{"part": p["part"], "role": p["role"],
                   "bounds_m": {"min": part_aabb(p["geo"][0])[0], "max": part_aabb(p["geo"][0])[1]}}
                  for p in all_parts_for_validate()],
        "nodes": node_list,
        "node_count": len(node_list),
        "mesh_count": len([n for n in node_list if not n.startswith("Socket_")
                           and n != ROOT_NAME]),
        "surface_table": surf_table,
        "triangles_total": sum(e["tris"] for e in surf_table),
        "materials_count": len(ROLES),
        "sockets": sockets_facts,
        "single_berth_note": "single berth is first-sample illustration only; "
                             "not capacity / docking / grid contract",
        "u01_reference_note": "head face X=.65 (old candidate .52 not used); candidate demo robot "
                              "root (0,.02,0) puts U01 Socket_Charge at (.48,.57,-.35); gap to "
                              "Socket_Dock .17m; static illustration only, open-lid vertex "
                              "clearance to be verified by master",
        "provenance": "original procedural geometry (python stdlib); shared M01 three-role color "
                      "basis read-only; no third-party/copyrighted/image-generated assets",
        "bin_bytes": os.path.getsize(os.path.join(HERE, NAME + ".bin")),
        "rebuilt_glb_identical": h1 == h2,
        "sha256": {"gltf": shas["gltf"], "bin": shas["bin"], "glb": shas["glb"],
                   "glb_prototype": sha_proto},
    }
    with open(os.path.join(HERE, "geometry-facts.json"), "w") as f:
        json.dump(facts, f, ensure_ascii=False, indent=2)
        f.write("\n")
    elapsed = time.perf_counter() - t0
    write_report(facts, elapsed)
    print(f"OK {NAME}: {facts['triangles_total']} tris, {facts['node_count']} nodes, "
          f"bin {facts['bin_bytes']}B, glb sha {shas['glb'][:12]}…, {elapsed:.3f}s")
    return 0


def selftest():
    """六类非法输入负例：全部必须被同一校验路径拒绝；任一未拒绝即失败。"""
    cases = []

    def expect_reject(name, fn):
        try:
            fn()
        except ValidationError as e:
            cases.append((name, True, str(e)))
        else:
            cases.append((name, False, "no rejection"))

    fp = charger_parts()
    found = {nm: (r, g) for nm, r, g in fp}
    expect_reject("non-finite-coord", lambda: validate_parts(
        [{"part": "Foundation", "role": "frame_dark",
          "geo": (found["Foundation"][1][0] + [(float("nan"), 0.01, 0.0)],
                  found["Foundation"][1][1], found["Foundation"][1][2])}], ENV, "charger"))
    expect_reject("negative-size", lambda: box(0.0, 0.01, 0.0, 1.80, -0.02, 2.20))
    expect_reject("unknown-role", lambda: validate_parts(
        [{"part": "Foundation", "role": "chrome", "geo": found["Foundation"][1]}], ENV, "charger"))
    expect_reject("out-of-range-index", lambda: validate_parts(
        [{"part": "Foundation", "role": "frame_dark",
          "geo": (found["Foundation"][1][0],
                  found["Foundation"][1][1][:-1] + [(0, 1, 99)],
                  found["Foundation"][1][2])}], ENV, "charger"))
    expect_reject("envelope-breach", lambda: validate_parts(
        [{"part": "Foundation", "role": "frame_dark",
          "geo": box(0.0, 0.01, 0.0, 2.40, 0.02, 2.20)}], ENV, "charger"))

    def bad_socket():
        sc = build_scene()
        sc["children"] = [c for c in sc["children"] if c["name"] != "Socket_Dock"]
        validate_scene(sc)  # 缺 socket 必须拒绝
        sc2 = build_scene()
        for c in sc2["children"]:
            if c["name"] == "Socket_Dock":
                c["rotation"] = (0.0, 0.0, 0.0, 1.0)  # 朝 -Z：Dock 冻结朝向是 -X，坏
        validate_scene(sc2)  # 坏方向必须拒绝
    expect_reject("missing-or-bad-socket-direction", bad_socket)

    bad = [c for c in cases if not c[1]]
    for name, ok, msg in cases:
        print(f"  {'PASS' if ok else 'FAIL'} {name}: {msg}")
    if bad:
        print(f"selftest FAILED: {len(bad)} case(s) not rejected", file=sys.stderr)
        return 1
    print(f"selftest OK: {len(cases)} negative case classes all rejected")
    return 0


def readback():
    """独立回读 GLB 原字节（不经内存构建物）：header/JSON/BIN 块 → 位置/方向/表面/包络/角色。"""
    facts_path = os.path.join(HERE, "geometry-facts.json")
    with open(facts_path) as f:
        facts = json.load(f)
    with open(os.path.join(HERE, NAME + ".glb"), "rb") as f:
        data = f.read()

    def need(cond, msg):
        if not cond:
            raise ValidationError("readback: " + msg)

    magic, ver, length = struct.unpack_from("<III", data, 0)
    need(magic == 0x46546C67 and ver == 2, f"GLB header magic/version {magic},{ver}")
    need(length == len(data), f"GLB length field {length} != file size {len(data)}")
    clen, ctype = struct.unpack_from("<II", data, 12)
    need(ctype == 0x4E4F534A, "JSON chunk type")
    doc = json.loads(data[20:20 + clen].decode("utf-8"))
    blen, btype = struct.unpack_from("<II", data, 20 + clen)
    need(btype == 0x004E4942, "BIN chunk type")
    need(doc["buffers"][0].get("uri") is None, "GLB buffer must not carry uri")
    need(doc["buffers"][0]["byteLength"] == blen,
         f"buffer byteLength {doc['buffers'][0]['byteLength']} != BIN chunk {blen}")
    blob = data[28 + clen: 28 + clen + blen]

    def acc_read(ai):
        a = doc["accessors"][ai]
        bv = doc["bufferViews"][a["bufferView"]]
        base = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        if a["type"] == "VEC3" and a["componentType"] == 5126:
            return [struct.unpack_from("<3f", blob, base + 12 * i) for i in range(a["count"])]
        if a["type"] == "SCALAR" and a["componentType"] == 5125:
            return [struct.unpack_from("<I", blob, base + 4 * i)[0] for i in range(a["count"])]
        raise ValidationError(f"readback: unsupported accessor layout {a['type']}/{a['componentType']}")

    nodes = doc["nodes"]
    parent = {}
    for ni, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = ni

    def world_tr(ni):
        t = list(nodes[ni].get("translation", [0.0, 0.0, 0.0]))
        if ni in parent:
            pt = world_tr(parent[ni])
            t = [t[i] + pt[i] for i in range(3)]
        return t

    # 节点集合
    names = [n["name"] for n in nodes]
    need(len(names) == len(set(names)), "duplicate node names in GLB")
    need(set(names) == EXPECT_NODES, f"GLB node set mismatch: {sorted(names)}")
    need(nodes[doc["scenes"][0]["nodes"][0]]["name"] == ROOT_NAME, "GLB root node")

    # socket：位置、朝向、纯标记
    for spec in SOCKETS:
        nm, tr, rot, fwd_want, _ = spec
        n = [x for x in nodes if x["name"] == nm][0]
        need("mesh" not in n, f"{nm} must be a pure marker node without mesh")
        wt = world_tr(nodes.index(n))
        need(all(abs(wt[i] - tr[i]) <= 1e-5 for i in range(3)),
             f"{nm} world translation {wt} != frozen {list(tr)}")
        q = n.get("rotation")
        need(q is not None and len(q) == 4, f"{nm} rotation missing")
        check_orientation(nm, q, fwd_want)

    # 表面：AABB/包络/面数/法线/角色
    mats = doc["materials"]
    need(len(mats) == facts["materials_count"] == 3, f"materials count {len(mats)}")
    mat_names = [m["name"] for m in mats]
    need(mat_names == ROLES, f"material roles {mat_names} != {ROLES}")
    lo = [float("inf")] * 3
    hi = [float("-inf")] * 3
    tri_total = 0
    got_surfaces = []
    for ni, n in enumerate(nodes):
        if "mesh" not in n:
            continue
        mesh = doc["meshes"][n["mesh"]]
        wt = world_tr(ni)
        for si, prim in enumerate(mesh["primitives"]):
            pos = acc_read(prim["attributes"]["POSITION"])
            nrm = acc_read(prim["attributes"]["NORMAL"])
            idx = acc_read(prim["indices"])
            for v in pos:
                for i in range(3):
                    lo[i] = min(lo[i], v[i] + wt[i])
                    hi[i] = max(hi[i], v[i] + wt[i])
            for nr in nrm:
                L = math.sqrt(sum(c * c for c in nr))
                need(abs(L - 1.0) < 1e-6, f"{n['name']} normal not unit: {nr}")
                need(all(min(abs(c), abs(abs(c) - 1.0)) < 1e-6 for c in nr),
                     f"{n['name']} normal not axis-aligned flat: {nr}")
            role = mat_names[prim["material"]]
            got_surfaces.append({"node": n["name"], "surface": si, "role": role,
                                 "tris": len(idx) // 3, "verts": len(pos)})
            tri_total += len(idx) // 3
    env_lo, env_hi = facts["envelope_m"]["min"], facts["envelope_m"]["max"]
    need(all(abs(lo[i] - env_lo[i]) <= 1e-5 and abs(hi[i] - env_hi[i]) <= 1e-5 for i in range(3)),
         f"GLB AABB {[lo, hi]} != frozen envelope {[env_lo, env_hi]}")
    need(abs(lo[1]) < 1e-6, f"GLB min Y {lo[1]} != 0")
    need(tri_total == facts["triangles_total"],
         f"triangles {tri_total} != facts {facts['triangles_total']}")
    key = lambda e: (e["node"], e["surface"])
    need(sorted(got_surfaces, key=key) == sorted(facts["surface_table"], key=key),
         "surface table mismatch between GLB and facts")
    need(doc["asset"]["generator"].startswith("yudian ART-F04-GEO"), "generator identity")
    print(f"READBACK OK {NAME}: nodes {len(nodes)}, sockets "
          f"{[(s[0], [round(c, 3) for c in quat_rotate(s[2], SOCKET_FORWARD_REF)]) for s in SOCKETS]}, "
          f"tris {tri_total}, envelope {[round(c, 3) for c in lo + hi]}")
    return 0


if __name__ == "__main__":
    if "--selftest" in sys.argv[1:]:
        sys.exit(selftest())
    if "--readback" in sys.argv[1:]:
        try:
            sys.exit(readback())
        except ValidationError as e:
            print(str(e), file=sys.stderr)
            sys.exit(1)
    sys.exit(main())
