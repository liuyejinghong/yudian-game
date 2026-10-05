#!/usr/bin/env python3
# ART-F02-GEO · 加工设施 processor r1 静态几何生成器（原创，Python 标准库，无第三方依赖）
#
# 坐标：米制，+Y 上，前方 -Z，原点在地面中心（底座接地 Y=0）。
# 全部几何节点仅平移（局部 = 世界 − 父世界）；socket 节点按合同烘入真实旋转。
# 活动件（PressRam/FeedGate/StopGate/ServiceCover）为独立父节点，pivot 即铰轴/行程锚点；
# 动画属 STATE，本票只交付 idle 默认位姿并记录端点顶点 AABB。
#
# 输出（写入本目录）：
#   processor-r1.gltf + processor-r1.bin   可编辑分离源（.gltf 引用 .bin）
#   processor-r1.glb                       自包含 GLB（内嵌 JSON 的 buffer 无 uri）
#   geometry-facts.json                    生成期实测：AABB、端点 AABB、surface 表、区间检查
#
# 自检（失败退出非零，不写任何输出文件）：
#   - 每个基元闭合正符号体积（外向 CCW 绕向）；
#   - 冻结分区尺寸断言（first-assets-r1.md F02 表 + 派单票）；
#   - 关键静/端点件对的轴对齐区间分离检查（AABB 区间法，非 SAT；旋转件用其端点 AABB 保守判定）。

import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

ROLE_ORDER = ["frame_dark", "body_light", "accent_warm"]
ROLE_PLACEHOLDER = {  # 占位 PBR：线性近似值，仅承载分面角色；正式接线用 M01 共享 .tres
    "frame_dark":  ((0.0401, 0.0578, 0.0694), 0.75, 0.2),
    "body_light":  ((0.6818, 0.7140, 0.6718), 0.65, 0.0),
    "accent_warm": ((0.6712, 0.1975, 0.0480), 0.60, 0.0),
}

# ---------------------------------------------------------------- 基元（世界坐标）
REG = {}  # 名字 -> ((x0,y0,z0),(x1,y1,z2))，供区间检查


def _tri_volume(V, T):
    s = 0.0
    for a, b, c in T:
        A, B, C = V[a], V[b], V[c]
        s += (A[0] * (B[1] * C[2] - B[2] * C[1])
              - A[1] * (B[0] * C[2] - B[2] * C[0])
              + A[2] * (B[0] * C[1] - B[1] * C[0])) / 6.0
    return s


def box(cx, cy, cz, sx, sy, sz, name=None):
    """轴对齐盒，外向 CCW；构造即断言正体积。"""
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    v = [(cx + a * hx, cy + b * hy, cz + c * hz)
         for c in (-1, 1) for b in (-1, 1) for a in (-1, 1)]
    t = [(0, 2, 3), (0, 3, 1), (4, 5, 7), (4, 7, 6), (4, 1, 5), (4, 0, 1),
         (2, 7, 3), (2, 6, 7), (0, 4, 6), (0, 6, 2), (1, 7, 5), (1, 3, 7)]
    if _tri_volume(v, t) <= 1e-12:
        raise AssertionError("box 绕向/退化: %s" % name)
    if name:
        REG[name] = ((cx - hx, cy - hy, cz - hz), (cx + hx, cy + hy, cz + hz))
    return v, t


def rbox(cx, cy, cz, sx, sy, sz, deg_z, name=None):
    """绕自身中心 Z 轴旋转的盒（斜条），真旋转保绕向。"""
    bv, bt = box(cx, cy, cz, sx, sy, sz)
    a = math.radians(deg_z)
    ca, sa = math.cos(a), math.sin(a)
    v = [(cx + (x - cx) * ca - (y - cy) * sa, cy + (x - cx) * sa + (y - cy) * ca, z)
         for x, y, z in bv]
    if _tri_volume(v, bt) <= 1e-12:
        raise AssertionError("rbox 绕向/退化: %s" % name)
    if name:
        REG[name] = ((min(p[0] for p in v), min(p[1] for p in v), min(p[2] for p in v)),
                     (max(p[0] for p in v), max(p[1] for p in v), max(p[2] for p in v)))
    return v, bt


def merge(*geoms):
    V, T = [], []
    for v, t in geoms:
        b = len(V)
        V += list(v)
        T += [(a + b, bb + b, c + b) for a, bb, c in t]
    return V, T


# ---------------------------------------------------------------- 冻结尺寸/位置（需求表）
# socket forward 烘成节点旋转：局部 -Z 映射到 forward（glTF 四元数 x,y,z,w）
SOCKETS = {
    "Socket_Input":    {"t": [0.0, 0.48, -2.2], "forward": [0.0, 0.0, -1.0], "rot": None},
    "Socket_Output":   {"t": [0.0, 0.48, 2.2], "forward": [0.0, 0.0, 1.0],
                        "rot": [0.0, 1.0, 0.0, 0.0]},                                   # Y+180°
    "Socket_PowerIn":  {"t": [1.6, 0.35, 0.0], "forward": [1.0, 0.0, 0.0],
                        "rot": [0.0, -0.7071067811865476, 0.0, 0.7071067811865476]},    # Y-90°
    "Socket_Service":  {"t": [-1.3, 1.0, 0.4], "forward": [-1.0, 0.0, 0.0],
                        "rot": [0.0, 0.7071067811865476, 0.0, 0.7071067811865476]},    # Y+90°
}
BOX_W, BOX_L, BOX_H = 0.64, 0.70, 0.38      # 私有货箱外包络（冻结）
BOX_IN_CZ, BOX_OUT_CZ, BOX_Y0 = -1.55, 1.65, 0.48
RAM_PIVOT = (1.315, 1.90, 0.0)              # 压头中心；未来行程 Y-0.35
GATE_PIVOT = (0.0, 0.50, -0.84)             # 进料挡板底部铰轴（X 向）；未来 +25° 内摆
STOP_PIVOT = (0.0, 1.75, -2.29)             # 停机挡板中心；未来 Y-0.80 落下
COVER_PIVOT = (-1.17, 1.60, 0.90)           # 检修盖 +Z 立铰（Y 向）；未来 +70° 外开

# STATE-A 源动画：只动子件局部 TRS，节点/几何/角色与 GEO 完全一致；
# offline 仅是 disabled 的展示词，无第八态；move/charge/towed 为固定设施 N/A，不造 clip。
ANIM_SPECS = [
    {"name": "work-loop", "loop": True,
     "times": [0.0, 0.45, 0.5, 0.55, 1.0, 1.45, 1.5, 1.55, 2.0], "channels": [
        # 压头：默认位 Y1.90 ↔ 下行 0.35（Y1.55）。极值用 0.45/0.5/0.55 三键平台，
        # 避免 Godot 默认 30fps 重采样把 t=.5 落到 .5333 而吃掉满行程；2s 周期不变。
        {"node": "PressRam", "path": "translation",
         "values": [[1.315, 1.90, 0.0], [1.315, 1.55, 0.0], [1.315, 1.55, 0.0],
                    [1.315, 1.55, 0.0], [1.315, 1.90, 0.0], [1.315, 1.725, 0.0],
                    [1.315, 1.725, 0.0], [1.315, 1.725, 0.0], [1.315, 1.90, 0.0]]},
        # 进料挡板：局部 X +25° 内摆（GEO 冻结正向），同样平台化；t=.5 全摆与 t=1.5 半摆仍不同。
        {"node": "FeedGate", "path": "rotation", "axis": "x",
         "degs": [0.0, 25.0, 25.0, 25.0, 0.0, 12.5, 12.5, 12.5, 0.0]},
    ]},
    {"name": "disabled", "loop": False, "times": [0.0, 1.0], "channels": [
        # 停机大挡板落下 0.80：Y1.75 → 0.95
        {"node": "StopGate", "path": "translation",
         "values": [[0.0, 1.75, -2.29], [0.0, 0.95, -2.29]]},
    ]},
    {"name": "maintenance", "loop": False, "times": [0.0, 1.0], "channels": [
        # 检修盖绕局部 Y +70° 外开（GEO 冻结正向）
        {"node": "ServiceCover", "path": "rotation", "axis": "y", "degs": [0.0, 70.0]},
    ]},
]

# ---------------------------------------------------------------- 构件（世界坐标，(role, geom) 列表）
def g_base():
    b = [
        box(0, 0.08, 0, 3.6, 0.16, 4.8, "slab"),
        box(0, 0.18, 0.1, 2.5, 0.04, 2.1, "plinth"),
        box(0, 0.18, -1.6, 1.44, 0.04, 1.6, "strip_feed"),
        box(0, 0.18, 1.7, 1.0, 0.04, 1.4, "strip_out"),
    ]
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.append(box(sx * 1.66, 0.20, sz * 2.26, 0.20, 0.08, 0.20, "pad_corner"))
    for sx in (-1, 1):
        b.append(box(sx * 1.20, 0.22, -0.60, 0.10, 0.04, 0.10, "bolt_a"))
        b.append(box(sx * 1.20, 0.22, 0.80, 0.10, 0.04, 0.10, "bolt_b"))
    return [("frame_dark", *merge(*b))]


def g_chamber():
    dark = [
        box(0, 0.25, 0.1, 2.3, 0.10, 1.8, "ch_floor"),            # Y0.20..0.30
        box(1.125, 0.70, 0.1, 0.05, 0.80, 1.7, "ch_wall_px_lo"),  # X1.10..1.15 Y0.30..1.10
        box(-1.125, 0.70, 0.1, 0.05, 0.80, 1.7, "ch_wall_nx_lo"),
        box(0, 0.39, -0.775, 2.2, 0.18, 0.05, "ch_front_low"),    # Y0.30..0.48
        box(-0.85, 0.79, -0.775, 0.50, 0.62, 0.05, "ch_front_l"), # Y0.48..1.10
        box(0.85, 0.79, -0.775, 0.50, 0.62, 0.05, "ch_front_r"),
        box(0, 1.06, -0.775, 1.20, 0.08, 0.05, "ch_front_top"),   # 口上沿 Y1.02..1.10
        box(0, 0.70, 0.975, 2.2, 0.80, 0.05, "ch_rear_lo"),
        box(0, 0.46, -0.60, 1.20, 0.04, 0.30, "throat_floor"),    # 床面连续 Y0.44..0.48
        box(0, 1.04, -0.60, 1.20, 0.04, 0.30, "throat_ceil"),
        box(-0.58, 0.75, -0.60, 0.04, 0.54, 0.30, "throat_side_l"),
        box(0.58, 0.75, -0.60, 0.04, 0.54, 0.30, "throat_side_r"),
        box(0, 0.75, -0.465, 1.12, 0.54, 0.03, "throat_back"),
        box(-0.90, 1.27, 0.35, 0.40, 0.04, 1.00, "cav_floor"),    # 检修深腔内衬
        box(-0.90, 1.93, 0.35, 0.40, 0.04, 1.00, "cav_ceil"),
        box(-0.72, 1.60, 0.35, 0.04, 0.70, 1.00, "cav_back"),
        box(-0.90, 1.60, -0.13, 0.40, 0.70, 0.04, "cav_side_f"),
        box(-0.90, 1.60, 0.83, 0.40, 0.70, 0.04, "cav_side_r"),
    ]
    light = [
        box(1.125, 1.73, 0.1, 0.05, 1.26, 1.7, "ch_wall_px_hi"),  # Y1.10..2.36
        box(-1.125, 1.175, 0.1, 0.05, 0.15, 1.7, "ch_wall_nx_below"),
        box(-1.125, 2.155, 0.1, 0.05, 0.41, 1.7, "ch_wall_nx_above"),
        box(-1.125, 1.73, -0.45, 0.05, 1.26, 0.60, "ch_wall_nx_front"),
        box(-1.125, 1.73, 0.90, 0.05, 1.26, 0.10, "ch_wall_nx_rear"),
        box(0, 1.73, -0.775, 2.20, 1.26, 0.05, "ch_front_hi"),
        box(0, 1.73, 0.975, 2.20, 1.26, 0.05, "ch_rear_hi"),
        box(-1.10, 2.38, 0.1, 0.10, 0.04, 1.80, "roof_l"),        # Y2.36..2.40
        box(1.10, 2.38, 0.1, 0.10, 0.04, 1.80, "roof_r"),
        box(0, 2.38, -0.75, 2.10, 0.04, 0.10, "roof_f"),
        box(0, 2.38, 0.95, 2.10, 0.04, 0.10, "roof_b"),
        box(-0.93, 1.61, 0.35, 0.22, 0.38, 0.54, "cav_unit"),     # 腔内机组
    ]
    roof_panel = [box(0, 2.38, 0.1, 2.10, 0.04, 1.60, "roof_panel")]
    stripe = [box(0, 1.19, 1.006, 2.00, 0.10, 0.012, "rear_stripe")]
    return [("frame_dark", *merge(*dark)),
            ("body_light", *merge(*light)),
            ("frame_dark", *merge(*roof_panel)),
            ("accent_warm", *merge(*stripe))]


def g_press_guide():
    g = [box(1.185, 1.70, -0.48, 0.07, 1.30, 0.08, "rail_l"),
         box(1.185, 1.70, 0.48, 0.07, 1.30, 0.08, "rail_r"),
         box(1.185, 2.375, 0, 0.07, 0.05, 0.88, "press_beam")]
    return [("frame_dark", *merge(*g))]


def g_press_ram():
    return [("frame_dark", *box(1.275, 1.90, 0, 0.09, 0.60, 0.80, "ram_body")),
            ("body_light", *box(1.36, 1.90, 0, 0.08, 0.60, 0.80, "ram_face")),
            ("accent_warm", *box(1.315, 1.64, 0, 0.17, 0.08, 0.80, "ram_edge"))]


def g_feed_bed():
    core = [box(0, 0.30, -1.60, 1.20, 0.20, 1.60, "feed_core")]  # X±0.60 Z-2.40..-0.80
    lands = []
    for i, (cx, w) in enumerate(((-0.475, 0.25), (-0.15, 0.20), (0.15, 0.20), (0.475, 0.25))):
        lands.append(box(cx, 0.45, -1.60, w, 0.06, 1.52, "feed_land_%d" % i))  # 顶 Y0.48
    chans = []
    for i, cx in enumerate((-0.30, 0.0, 0.30)):
        chans.append(box(cx, 0.41, -1.60, 0.10, 0.02, 1.52, "feed_chan_%d" % i))  # 槽底顶 Y0.42
    return [("frame_dark", *merge(*core, *chans)),
            ("body_light", *merge(*lands))]


def g_feed_gate():
    # 板宽 1.10 < 喉道内净宽 1.12（侧壁内沿 ±0.56）：内摆 25° 板缘不入侧壁
    return [("body_light", *box(0, 0.74, -0.84, 1.10, 0.48, 0.04, "fg_panel")),
            ("frame_dark", *merge(
                box(-0.51, 0.74, -0.84, 0.07, 0.48, 0.05, "fg_rib_l"),
                box(0.51, 0.74, -0.84, 0.07, 0.48, 0.05, "fg_rib_r"),
                box(-0.48, 0.52, -0.85, 0.10, 0.04, 0.08, "fg_lug_l"),
                box(0.48, 0.52, -0.85, 0.10, 0.04, 0.08, "fg_lug_r")))]


def g_stop_gate():
    panel = box(0, 1.75, -2.29, 1.16, 0.90, 0.04, "sg_panel")  # 收起 Y1.30..2.20
    slats = []
    for fi, face_z in enumerate((-2.322, -2.258)):
        for ci, cx in enumerate((-0.34, 0.0, 0.34)):
            slats.append(rbox(cx, 1.75, face_z, 0.40, 0.05, 0.024, 45.0, "sg_slat_%d_%d" % (fi, ci)))
    return [("frame_dark", *panel),
            ("accent_warm", *merge(*slats))]


def g_gantry():
    return [("frame_dark", *merge(
        box(-0.64, 1.22, -2.28, 0.08, 2.04, 0.16, "post_l"),
        box(0.64, 1.22, -2.28, 0.08, 2.04, 0.16, "post_r"),
        box(0, 2.32, -2.28, 1.36, 0.16, 0.16, "gantry_beam")))]


def g_out_rack():
    dark = [box(0, 0.31, 1.04, 0.90, 0.22, 0.08, "out_skirt")]
    for sx in (-1, 1):
        for cz in (1.12, 2.28):
            dark.append(box(sx * 0.40, 0.31, cz, 0.08, 0.22, 0.08, "out_leg"))
    lands = []
    for i, (cx, w) in enumerate(((-0.355, 0.19), (0.0, 0.32), (0.355, 0.19))):
        lands.append(box(cx, 0.46, 1.70, w, 0.04, 1.40, "out_land_%d" % i))  # 顶 Y0.48
    chans = [box(-0.21, 0.43, 1.70, 0.10, 0.02, 1.40, "out_chan_0"),
             box(0.21, 0.43, 1.70, 0.10, 0.02, 1.40, "out_chan_1")]     # 通到外沿 Z2.40
    return [("frame_dark", *merge(*dark, *chans)),
            ("body_light", *merge(*lands))]


def g_service_port():
    return [("frame_dark", *box(-1.215, 1.0, 0.40, 0.13, 0.28, 0.30, "sp_body")),
            ("accent_warm", *box(-1.29, 1.0, 0.40, 0.02, 0.20, 0.22, "sp_face"))]


def g_power_port():
    return [("frame_dark", *box(1.365, 0.35, 0, 0.43, 0.16, 0.20, "pp_conduit")),
            ("accent_warm", *box(1.59, 0.35, 0, 0.02, 0.24, 0.28, "pp_flange"))]


def g_service_cover():
    return [("body_light", *box(-1.17, 1.60, 0.35, 0.04, 0.80, 1.10, "cv_panel")),
            ("accent_warm", *box(-1.20, 1.60, 0.02, 0.02, 0.72, 0.06, "cv_handle"))]


def g_crate(cz, tag):
    return [("frame_dark", *merge(
                box(0, 0.53, cz, 0.56, 0.10, 0.62, "crate_base_" + tag),
                box(0, 0.83, cz, BOX_W, 0.06, BOX_L, "crate_rim_" + tag))),
            ("body_light", *box(0, 0.69, cz, 0.60, 0.22, 0.66, "crate_body_" + tag))]


def g_dust_collar():
    return [("frame_dark", *box(0, 1.14, -.91, 1.32, .06, .22)),
            ("body_light", *box(-.64, .75, -.91, .05, .72, .22)),
            ("body_light", *box(.64, .75, -.91, .05, .72, .22))]


def g_inner_cover():
    # The inner cover stays closed when the outer service cover opens.
    return [("frame_dark", *box(-1.04875, 1.61, .35, .0175, .38, .54)),
            ("body_light", *box(-1.07, 1.61, .35, .025, .38, .54))]


def g_heat_panel():
    # Solid mounts conduct toward the exposed panel; this is not an air vent.
    return [("frame_dark", *box(1.16125, 1.70, .70, .0225, .36, .16)),
            ("body_light", *box(1.185, 1.70, .70, .025, .55, .22))]


# ---------------------------------------------------------------- 场景组装
class N:
    def __init__(self, name, pivot, parent):
        self.name, self.pivot, self.parent = name, pivot, parent
        self.surfs = []   # [(role, worldV, worldT)]，同 node 同 role 已合并
        self.children = []


def build_scene():
    root = N("processor-r1", (0, 0, 0), None)
    model = N("Model", (0, 0, 0), root)
    root.children.append(model)
    parts = [
        ("Base", g_base(), (0, 0, 0)),
        ("Chamber", g_chamber(), (0, 0, 0)),
        ("PressGuide", g_press_guide(), (0, 0, 0)),
        ("PressRam", g_press_ram(), RAM_PIVOT),
        ("FeedBed", g_feed_bed(), (0, 0, 0)),
        ("FeedGate", g_feed_gate(), GATE_PIVOT),
        ("Gantry", g_gantry(), (0, 0, 0)),
        ("StopGate", g_stop_gate(), STOP_PIVOT),
        ("OutputRack", g_out_rack(), (0, 0, 0)),
        ("ServicePort", g_service_port(), (0, 0, 0)),
        ("PowerPort", g_power_port(), (0, 0, 0)),
        ("DustCollar", g_dust_collar(), (0, 0, 0)),
        ("ElectronicsInnerCover", g_inner_cover(), (0, 0, 0)),
        ("HeatPanel", g_heat_panel(), (0, 0, 0)),
        ("ServiceCover", g_service_cover(), COVER_PIVOT),
        ("InputCrate", g_crate(BOX_IN_CZ, "in"), (0, BOX_Y0, BOX_IN_CZ)),
        ("OutputCrate", g_crate(BOX_OUT_CZ, "out"), (0, BOX_Y0, BOX_OUT_CZ)),
    ]
    for name, geoms, pivot in parts:
        n = N(name, pivot, model)
        agg = {}
        for role, V, T in geoms:
            if role in agg:
                agg[role] = merge(agg[role], (V, T))
            else:
                agg[role] = (V, T)
        n.surfs = [(r, agg[r][0], agg[r][1]) for r in ROLE_ORDER if r in agg]
        model.children.append(n)
    return root


def world_translation(n):
    px, py, pz = n.pivot
    p = n.parent
    while p is not None:
        px += p.pivot[0]; py += p.pivot[1]; pz += p.pivot[2]
        p = p.parent
    return (px, py, pz)


def node_bbox(n):
    xs, ys, zs = [], [], []
    for role, V, T in n.surfs:
        xs += [v[0] for v in V]; ys += [v[1] for v in V]; zs += [v[2] for v in V]
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


def all_surfs(root):
    out = []
    def walk(n):
        out.extend(n.surfs)
        for c in n.children:
            walk(c)
    walk(root)
    return out


def bbox(surfs):
    xs, ys, zs = [], [], []
    for role, V, T in surfs:
        xs += [v[0] for v in V]; ys += [v[1] for v in V]; zs += [v[2] for v in V]
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


# ---------------------------------------------------------------- 端点姿态（顶点 AABB 实测，动画属 STATE 未交）
def _rot_x_about(pts, pivot, deg):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    px, py, pz = pivot
    return [(x, py + (y - py) * ca - (z - pz) * sa, pz + (y - py) * sa + (z - pz) * ca)
            for x, y, z in pts]


def _rot_y_about(pts, pivot, deg):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    px, py, pz = pivot
    return [(px + (x - px) * ca + (z - pz) * sa, y, pz - (x - px) * sa + (z - pz) * ca)
            for x, y, z in pts]


def endpoint_bboxes(model):
    get = lambda nm: [c for c in model.children if c.name == nm][0]
    out = {}
    ram = get("PressRam")
    v = [p for role, V, T in ram.surfs for p in V]
    out["press_ram_down_0.35"] = _bbox_of_pts([(x, y - 0.35, z) for x, y, z in v])
    gate = get("FeedGate")
    v = [p for role, V, T in gate.surfs for p in V]
    out["feed_gate_open_in_25deg"] = _bbox_of_pts(_rot_x_about(v, GATE_PIVOT, 25.0))
    sg = get("StopGate")
    v = [p for role, V, T in sg.surfs for p in V]
    out["stop_gate_fallen_0.80"] = _bbox_of_pts([(x, y - 0.80, z) for x, y, z in v])
    cv = get("ServiceCover")
    v = [p for role, V, T in cv.surfs for p in V]
    out["service_cover_open_70deg"] = _bbox_of_pts(_rot_y_about(v, COVER_PIVOT, 70.0))
    return out


def _bbox_of_pts(pts):
    return ([round(min(p[i] for p in pts), 6) for i in range(3)],
            [round(max(p[i] for p in pts), 6) for i in range(3)])


# ---------------------------------------------------------------- 自检：区间分离（AABB 区间法，非 SAT）
def _gap(A, B):
    """A/B=((x0,y0,z0),(x1,y1,z1))。返回三轴中最大分离量：>0 至少一轴分离（AABB 充分），<0 全轴重叠。"""
    return max(max(A[0][i] - B[1][i], B[0][i] - A[1][i]) for i in range(3))


def selfcheck(root, facts):
    errs = []
    model = root.children[0]
    nodes = {c.name: c for c in model.children}

    # ---- 冻结尺寸断言（实测）
    (sx0, sy0, sz0), (sx1, sy1, sz1) = bbox(all_surfs(root))
    facts["static_bbox"] = [[round(sx0, 6), round(sy0, 6), round(sz0, 6)],
                            [round(sx1, 6), round(sy1, 6), round(sz1, 6)]]
    ch = bbox([s for s in nodes["Chamber"].surfs if s[0] != "accent_warm"])  # 结构面；后饰条凸 0.012 另记
    fb = node_bbox(nodes["FeedBed"])
    rk = node_bbox(nodes["OutputRack"])
    ram = node_bbox(nodes["PressRam"])
    ci = node_bbox(nodes["InputCrate"])
    co = node_bbox(nodes["OutputCrate"])
    hard = [
        ("静态包络 ≤3.6×4.8×2.4", sx0 >= -1.8 - 1e-9 and sx1 <= 1.8 + 1e-9
         and sz0 >= -2.4 - 1e-9 and sz1 <= 2.4 + 1e-9 and sy0 >= -1e-9 and sy1 <= 2.4 + 1e-9),
        ("接地 Y=0", abs(sy0) < 1e-9),
        ("主腔 X±1.15 Z-0.8..1.0 顶2.4", abs(ch[0][0] + 1.15) < 1e-9 and abs(ch[1][0] - 1.15) < 1e-9
         and abs(ch[0][2] + 0.80) < 1e-9 and abs(ch[1][2] - 1.00) < 1e-9 and abs(ch[1][1] - 2.40) < 1e-9),
        ("进料床 X±0.60 床面0.48", abs(fb[0][0] + 0.60) < 1e-9 and abs(fb[1][0] - 0.60) < 1e-9
         and abs(fb[1][1] - 0.48) < 1e-9 and abs(fb[0][2] + 2.40) < 1e-9 and abs(fb[1][2] + 0.80) < 1e-9),
        ("出料架 X±0.45 床面0.48", abs(rk[0][0] + 0.45) < 1e-9 and abs(rk[1][0] - 0.45) < 1e-9
         and abs(rk[1][1] - 0.48) < 1e-9 and abs(rk[0][2] - 1.00) < 1e-9 and abs(rk[1][2] - 2.40) < 1e-9),
        ("压头默认上位 Y1.60..2.20", abs(ram[0][1] - 1.60) < 1e-9 and abs(ram[1][1] - 2.20) < 1e-9),
        ("货箱外包络 0.64×0.70×0.38 底Y0.48",
         all(abs(b[1][0] - b[0][0] - BOX_W) < 1e-9 and abs(b[1][2] - b[0][2] - BOX_L) < 1e-9
             and abs(b[1][1] - b[0][1] - BOX_H) < 1e-9 and abs(b[0][1] - BOX_Y0) < 1e-9
             for b in (ci, co))),
        ("socket 位置冻结",
         SOCKETS["Socket_Input"]["t"] == [0.0, 0.48, -2.2] and
         SOCKETS["Socket_Output"]["t"] == [0.0, 0.48, 2.2] and
         SOCKETS["Socket_PowerIn"]["t"] == [1.6, 0.35, 0.0] and
         SOCKETS["Socket_Service"]["t"] == [-1.3, 1.0, 0.4]),
    ]
    for nm, ok in hard:
        if not ok:
            errs.append("尺寸断言失败: %s" % nm)

    # ---- 关键静/端点件对：AABB 区间分离（非 SAT；旋转件用端点顶点 AABB 保守判定）
    ep = endpoint_bboxes(model)
    facts["endpoint_bboxes"] = ep
    epb = {k: (tuple(v[0]), tuple(v[1])) for k, v in ep.items()}
    def R(prefix):  # 全实例展开（land/槽/斜条/箱件按编号注册）
        return [REG[k] for k in sorted(REG) if k.startswith(prefix)]

    crate_in = R("crate_base_in") + R("crate_rim_in") + R("crate_body_in")
    crate_out = R("crate_base_out") + R("crate_rim_out") + R("crate_body_out")
    CLEAR = [  # (名, A 列表, B 列表, 最小间隙)；对全部实例组合取最小
        ("ram_vs_rails", [REG["ram_body"]], [REG["rail_l"], REG["rail_r"]], 0.005),
        ("ram_vs_wall_px", [REG["ram_body"]], [REG["ch_wall_px_hi"], REG["ch_wall_px_lo"]], 0.005),
        ("ram_end_vs_rails", [epb["press_ram_down_0.35"]], [REG["rail_l"], REG["rail_r"]], 0.005),
        ("fg_panel_vs_front_wall", [REG["fg_panel"]], [REG["ch_front_top"], REG["ch_front_low"],
                                                       REG["ch_front_l"], REG["ch_front_r"]], 0.005),
        ("fg_lugs_vs_feed_lands", [REG["fg_lug_l"], REG["fg_lug_r"]], R("feed_land_"), 0.005),
        ("sg_panel_vs_posts", [REG["sg_panel"]], [REG["post_l"], REG["post_r"]], 0.005),
        ("sg_panel_vs_beam", [REG["sg_panel"]], [REG["gantry_beam"]], 0.005),
        ("sg_slats_vs_posts", R("sg_slat_"), [REG["post_l"], REG["post_r"]], 0.005),
        ("cover_vs_service_port", [REG["cv_panel"]], [REG["sp_body"], REG["sp_face"]], 0.005),
        ("handle_vs_wall_nx", [REG["cv_handle"]], [REG["ch_wall_nx_front"], REG["ch_wall_nx_below"]], 0.005),
        ("crate_in_vs_feed_chans", crate_in, R("feed_chan_"), 0.005),
        ("crate_out_vs_out_chans", crate_out, R("out_chan_"), 0.005),
        ("crate_in_vs_stop_gate_fallen", crate_in, [epb["stop_gate_fallen_0.80"]], 0.005),
        ("stop_gate_fallen_vs_feed_lands", [epb["stop_gate_fallen_0.80"]], R("feed_land_"), 0.005),
        ("stop_gate_fallen_vs_feed_gate", [epb["stop_gate_fallen_0.80"]], [REG["fg_panel"], REG["fg_rib_l"]], 0.005),
        ("gate_open_vs_throat", [epb["feed_gate_open_in_25deg"]], [REG["throat_ceil"], REG["throat_back"],
                                                                   REG["throat_side_l"], REG["throat_side_r"]], 0.005),
        ("gate_open_vs_crate_in", [epb["feed_gate_open_in_25deg"]], crate_in, 0.005),
        ("cover_open_vs_slab", [epb["service_cover_open_70deg"]], [REG["slab"]], 0.005),
        ("cover_open_vs_posts", [epb["service_cover_open_70deg"]], [REG["post_r"], REG["post_l"]], 0.005),
        ("posts_vs_feed_core", [REG["post_l"], REG["post_r"]], [REG["feed_core"]], 0.0),
        ("power_conduit_vs_wall", [REG["pp_conduit"]], [REG["ch_wall_px_lo"]], 0.0),
        ("service_body_vs_wall", [REG["sp_body"]], [REG["ch_wall_nx_lo"]], 0.0),
        ("feed_core_vs_chamber", [REG["feed_core"]], [REG["ch_floor"]], 0.0),
        ("out_skirt_vs_rear_wall", [REG["out_skirt"]], [REG["ch_rear_lo"]], 0.0),
        ("crate_in_base_vs_feed_lands", R("crate_base_in"), R("feed_land_"), 0.0),
        ("crate_out_base_vs_out_lands", R("crate_base_out"), R("out_land_"), 0.0),
    ]
    for nm, As, Bs, req in CLEAR:
        g = min(_gap(A, B) for A in As for B in Bs)
        g = round(g, 6)
        facts["interval_" + nm] = g
        if g < req - 1e-9:
            errs.append("区间分离不足: %s gap=%.6f (req %.3f)" % (nm, g, req))
    return errs


# ---------------------------------------------------------------- glTF 导出
def export(root, facts):
    blob = bytearray()
    bvs, accs = [], []

    def view(data, target):
        bv = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}
        if target is not None:  # 动画 times 等非顶点数据不带 target
            bv["target"] = target
        bvs.append(bv)
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
        data = struct.pack("<%dI" % len(flat), *flat)
        bv = view(data, 34963)
        accs.append({"bufferView": bv, "componentType": 5125, "count": len(flat), "type": "SCALAR",
                     "min": [min(flat)], "max": [max(flat)]})
        return len(accs) - 1

    def acc_scalar(vals):
        data = struct.pack("<%df" % len(vals), *vals)
        bv = view(data, None)
        accs.append({"bufferView": bv, "componentType": 5126, "count": len(vals), "type": "SCALAR",
                     "min": [min(vals)], "max": [max(vals)]})
        return len(accs) - 1

    def acc_vec4(vals):
        data = struct.pack("<%df" % (4 * len(vals)), *[c for v in vals for c in v])
        bv = view(data, None)
        accs.append({"bufferView": bv, "componentType": 5126, "count": len(vals), "type": "VEC4",
                     "min": [min(v[i] for v in vals) for i in range(4)],
                     "max": [max(v[i] for v in vals) for i in range(4)]})
        return len(accs) - 1

    materials = []
    for r in ROLE_ORDER:
        lin, rough, metal = ROLE_PLACEHOLDER[r]
        materials.append({"name": r, "pbrMetallicRoughness": {
            "baseColorFactor": [round(lin[0], 6), round(lin[1], 6), round(lin[2], 6), 1.0],
            "metallicFactor": metal, "roughnessFactor": rough}})

    nodes_json, meshes, surf_table = [], [], []

    def emit(n, parent_idx, parent_world):
        w = world_translation(n)
        idx = len(nodes_json)
        node_idx[n.name] = idx
        nodes_json.append({"name": n.name,
                           "translation": [round(w[0] - parent_world[0], 6),
                                           round(w[1] - parent_world[1], 6),
                                           round(w[2] - parent_world[2], 6)]})
        if parent_idx is None:
            scene_roots.append(idx)
        else:
            nodes_json[parent_idx].setdefault("children", []).append(idx)
        if n.surfs:
            prims = []
            si = 0
            for role, V, T in n.surfs:
                LV = [(v[0] - w[0], v[1] - w[1], v[2] - w[2]) for v in V]
                acc = [[0.0, 0.0, 0.0] for _ in LV]
                for a, b, c in T:
                    A, B, C = LV[a], LV[b], LV[c]
                    u = [B[i] - A[i] for i in range(3)]
                    q = [C[i] - A[i] for i in range(3)]
                    nx = u[1] * q[2] - u[2] * q[1]
                    ny = u[2] * q[0] - u[0] * q[2]
                    nz = u[0] * q[1] - u[1] * q[0]
                    for vi in (a, b, c):
                        acc[vi][0] += nx; acc[vi][1] += ny; acc[vi][2] += nz
                NRM = []
                for a3 in acc:
                    L = math.sqrt(sum(x * x for x in a3)) or 1.0
                    NRM.append((a3[0] / L, a3[1] / L, a3[2] / L))
                pv, pn, pi = acc_f32(LV), acc_f32(NRM), acc_u32(T)
                prims.append({"attributes": {"POSITION": pv, "NORMAL": pn},
                              "indices": pi, "material": ROLE_ORDER.index(role), "mode": 4})
                surf_table.append({"node": n.name, "surface": si, "role": role,
                                   "tris": len(T), "verts": len(LV)})
                si += 1
            meshes.append({"name": n.name, "primitives": prims})
            nodes_json[idx]["mesh"] = len(meshes) - 1
        for c in n.children:
            emit(c, idx, w)

    scene_roots = []
    node_idx = {}
    emit(root, None, (0.0, 0.0, 0.0))
    for nm, spec in SOCKETS.items():
        idx = len(nodes_json)
        node = {"name": nm, "translation": spec["t"],
                "extras": {"kind": "socket", "forward": spec["forward"], "up": [0, 1, 0]}}
        if spec["rot"] is not None:
            node["rotation"] = spec["rot"]
        nodes_json.append(node)
        nodes_json[1].setdefault("children", []).append(idx)

    # ---- STATE-A 源 clips：附加于同一 gltf/glb 文档，几何与节点零改动
    animations = []
    anim_facts = []
    for spec in ANIM_SPECS:
        t_acc = acc_scalar(spec["times"])
        samplers, channels = [], []
        for ch in spec["channels"]:
            if ch["path"] == "rotation":
                quats = []
                for d in ch["degs"]:
                    h = math.radians(d) / 2.0
                    s, c = math.sin(h), math.cos(h)
                    quats.append((s, 0.0, 0.0, c) if ch["axis"] == "x" else (0.0, s, 0.0, c))
                o_acc = acc_vec4(quats)
            else:
                o_acc = acc_f32(ch["values"])
            samplers.append({"input": t_acc, "output": o_acc, "interpolation": "LINEAR"})
            channels.append({"sampler": len(samplers) - 1,
                             "target": {"node": node_idx[ch["node"]], "path": ch["path"]}})
        animations.append({"name": spec["name"], "channels": channels, "samplers": samplers,
                           "extras": {"loop": spec["loop"], "duration_s": spec["times"][-1]}})
        anim_facts.append({"name": spec["name"], "loop": spec["loop"],
                           "duration_s": spec["times"][-1], "times": spec["times"],
                           "channels": [{"node": c["node"], "path": c["path"]} for c in spec["channels"]]})

    while len(blob) % 4:
        blob += b"\x00"
    gltf = {
        "asset": {"version": "2.0", "generator": "yudian ART-F02-GEO generate_processor_r1.py (python stdlib, original)"},
        "scene": 0,
        "scenes": [{"name": "Scene", "nodes": scene_roots}],
        "nodes": nodes_json,
        "meshes": meshes,
        "animations": animations,
        "materials": materials,
        "accessors": accs,
        "bufferViews": bvs,
        "buffers": [{"byteLength": len(blob), "uri": "processor-r1.bin"}],
    }
    jt = json.dumps(gltf, separators=(",", ":"), ensure_ascii=False)
    with open(os.path.join(HERE, "processor-r1.bin"), "wb") as f:
        f.write(blob)
    with open(os.path.join(HERE, "processor-r1.gltf"), "w") as f:
        f.write(jt)

    glb_doc = dict(gltf)
    glb_doc["buffers"] = [{"byteLength": len(blob)}]  # GLB 自包含：buffer 无 uri
    js = json.dumps(glb_doc, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    bb = bytes(blob) + b"\x00" * ((4 - len(blob) % 4) % 4)
    with open(os.path.join(HERE, "processor-r1.glb"), "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bb)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A))
        f.write(js)
        f.write(struct.pack("<II", len(bb), 0x004E4942))
        f.write(bb)

    facts["surface_table"] = surf_table
    facts["animations"] = anim_facts
    facts["bin_bytes"] = len(blob)
    return len(blob)


# ---------------------------------------------------------------- main
def main():
    root = build_scene()
    model = root.children[0]
    facts = {
        "generator": "generate_processor_r1.py",
        "origin": "original python-stdlib procedural geometry for yudian ART-F02-GEO",
        "coordinate": "meter, +Y up, forward -Z, ground Y=0, mesh nodes translation-only (sockets carry baked rotation)",
        "sockets": {k: {"position": v["t"], "forward": v["forward"],
                        "rotation_xyzw": v["rot"] or [0, 0, 0, 1]} for k, v in SOCKETS.items()},
        "motion_pivots": {"PressRam": list(RAM_PIVOT), "FeedGate": list(GATE_PIVOT),
                          "StopGate": list(STOP_PIVOT), "ServiceCover": list(COVER_PIVOT)},
        "part_bboxes": {c.name: [list(node_bbox(c)[0]), list(node_bbox(c)[1])] for c in model.children},
        "idle_state": "press ram up, feed gate closed, stop gate retracted, service cover closed",
    }
    errs = selfcheck(root, facts)
    if errs:
        print("SELF-CHECK FAILED:")
        for e in errs:
            print("  -", e)
        sys.exit(1)
    export(root, facts)
    facts["triangles_total"] = sum(s["tris"] for s in facts["surface_table"])
    facts["materials_count"] = len(ROLE_ORDER)
    facts["textures_count"] = 0
    with open(os.path.join(HERE, "geometry-facts.json"), "w") as f:
        json.dump(facts, f, indent=1, ensure_ascii=False)
    print("OK static=%s tris=%d bin=%dB" % (
        facts["static_bbox"], facts["triangles_total"], facts["bin_bytes"]))


if __name__ == "__main__":
    main()
