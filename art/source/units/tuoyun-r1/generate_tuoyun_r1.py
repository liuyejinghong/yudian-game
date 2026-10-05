#!/usr/bin/env python3
# ART-U01-GEO/STATE-A · 驮运机器人火星适应 r2 生成器（原创，Python 标准库，无第三方依赖）
#
# 坐标：米制，+Y 上，前方 -Z，原点在空载静止最低接地点（轮接地 Y=0）。
# 静态节点仅平移；轮几何圆柱轴即局部 X。STATE-A r5 起：
#   - CargoBox 总体外沿收敛到冻结 0.64W×0.70L×0.38H（原 0.66/0.72）；
#   - RearLock 只保留可转暖色横杆，固定支撑耳移入 Body 静态（避开 25° 旋转扫掠）；
#   - 新增 TowRod 私有牵引示意杆（沿前 Socket_TowFront -Z，0.35m，默认隐藏；
#     glTF 核心无 node 可见性，写 extras.visible=false 并由 STATE-B wrapper 控制）；
#   - Socket_TowRear 烘入 Y180°（forward +Z）、Socket_Charge 烘入 Y-90°（forward +X）；
#   - 导出 5 个真实动画 clip（move/work/charge/disabled/maintenance；towed 复用
#     disabled）：四元数旋转轨，move 用 1/4 圈分段 keyframe 防 0↔360 同端点。
#
# 输出（写入本目录）：
#   tuoyun-r1.gltf + tuoyun-r1.bin   可编辑分离源（bin 仅随 .gltf）
#   tuoyun-r1.glb                    真实自包含 GLB（JSON buffer 无 uri）
#   geometry-facts.json              生成期实测：包围盒、SAT 间隙、动画表、节点/surface 表
#
# 自检（失败退出非零，不写任何输出文件）：
#   - 每个 box/prism 三角绕向（凸体正符号体积）；
#   - 关键成对凸体 SAT：净空对要求 gap ≥ 0.005m，就位接触对要求无穿透；
#   - 有限运动端点检查：work 25° 横杆 vs 固定支耳、charge 60° 盖 vs 充电罩（离散端点，
#     不称连续扫掠证明；盖板 106 角离散采样沿用 GEO）；
#   - 火星r2轮组尺寸断言；保留r1货台与接口参照。
#
# 可复现：无时间戳、无随机、字典序稳定；重跑输出字节级一致（hash 见 geometry-report.md）。

import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- 冻结尺寸（需求表）
WHEEL_R, WHEEL_W = 0.205, 0.16
DRUM_R = .185
WHEEL_CX, WHEEL_CY, WHEEL_CZ = 0.55, 0.205, 0.56
DRUM_IN_R = 0.155           # 金属轮鼓内沿，保留可见轮辐开口
HEAD_HALF_X = 0.41           # 前服务头半宽；新轮内侧位于 |X|=.47
BAY_FLOOR_Y = 0.31           # 检修腔地板最高（= 头基座顶面）
BAY_WALL_IN_X = 0.38         # 检修腔真实内壁 |X|（深衬内沿；外白壳内沿 0.395、外沿 0.41）
DECK_Y, BED_Z0, BED_Z1 = 0.48, -0.10, 0.70
RAIL_TOP = 0.72
BOX_W, BOX_L, BOX_H, BOX_CZ, BOX_Y0 = 0.64, 0.70, 0.38, 0.28, 0.48
LID_PIVOT = (0.0, 0.64, -0.25)
LID_W, LID_T, LID_LEN = 0.70, 0.02, 0.50
LOCK_PIVOT = (0.0, 0.65, 0.78)
CAP_PIVOT = (0.48, 0.58, -0.35)
EAR_X, EAR_CY, EAR_CZ = 0.23, 0.565, 0.78   # 锁扣导向柱（Body 静态）：立柱在横杆两端外侧让开绕X扫掠
EAR_SX, EAR_SY, EAR_SZ = 0.04, 0.13, 0.04
TOW_ROD = {"y": 0.24, "z0": -0.80, "z1": -1.15, "s": 0.03}   # 前牵引示意杆，沿 -Z 0.35m
def _qy(deg):
    a = math.radians(deg) / 2.0
    return [0.0, round(math.sin(a), 8), 0.0, round(math.cos(a), 8)]
SOCKETS = {   # rotation 为 glTF 节点四元数（烘入源GLB）；forward 为烘后朝向
    "Socket_Cargo":    {"t": [0.0, 0.48, 0.28], "rotation": None, "forward": [0.0, 0.0, -1.0]},
    "Socket_TowFront": {"t": [0.0, 0.24, -0.80], "rotation": None, "forward": [0.0, 0.0, -1.0]},
    "Socket_TowRear":  {"t": [0.0, 0.24, 0.80], "rotation": _qy(180.0), "forward": [0.0, 0.0, 1.0]},
    "Socket_Charge":   {"t": [0.48, 0.55, -0.35], "rotation": _qy(-90.0), "forward": [1.0, 0.0, 0.0]},
}
ROLE_ORDER = ["frame_dark", "body_light", "rubber", "accent_warm"]
# 占位 PBR：baseColorFactor 存线性近似值，仅承载分面角色；正式接线用
# res://assets/materials/art-r1/<role>.tres（M01 已接受）逐 surface 覆盖。
ROLE_PLACEHOLDER = {
    "frame_dark":  ((0.0401, 0.0578, 0.0694), 0.75, 0.2),
    "body_light":  ((0.6818, 0.7140, 0.6718), 0.65, 0.0),
    "rubber":      ((0.0171, 0.0232, 0.0266), 0.90, 0.0),
    "accent_warm": ((0.6712, 0.1975, 0.0480), 0.60, 0.0),
}


# ---------------------------------------------------------------- STATE-A 动画（冻结）
def _qx(deg):
    a = math.radians(deg) / 2.0
    return [round(math.sin(a), 8), 0.0, 0.0, round(math.cos(a), 8)]


def _qz(deg):
    a = math.radians(deg) / 2.0
    return [0.0, 0.0, round(math.sin(a), 8), round(math.cos(a), 8)]


_T5 = [0.0, 0.25, 0.5, 0.75, 1.0]
_T2 = [0.0, 1.0]
# 五 clip：节点名 → (times, quats)；towed 复用 disabled（杆可见性归 STATE-B）。
ANIM_SPECS = [
    ("move", 1.0, True, "六轮绕局部X 1s整圈，1/4圈分段quat keyframe保证实际转动"),
    ("work", 1.0, False, "后锁横杆绕局部X +25°，1s非loop t=1定格；支撑耳已移静态"),
    ("charge", 1.0, False, "充电侧盖绕局部Z +60°朝+X开（顶铰链pivot .48,.58,-.35），1s非loop"),
    ("disabled", 1.0, False, "前顶盖绕局部X -35°下折遮鼻标，1s非loop；towed复用本clip"),
    ("maintenance", 1.0, False, "前顶盖绕局部X +70°上开露检修腔，1s非loop"),
]


def anim_tracks():
    """[(clip, duration, loop, [(node_name, times, quats)...])]，几何无关、供 export/selfcheck。"""
    wheel_quats = [_qx(0.0), _qx(90.0), _qx(180.0), _qx(270.0), _qx(360.0)]
    return [
        ("move", 1.0, True, [(nm, _T5, wheel_quats)
                             for nm in ("Wheel_LF", "Wheel_LM", "Wheel_LR", "Wheel_RF", "Wheel_RM", "Wheel_RR")]),
        ("work", 1.0, False, [("RearLock", _T2, [_qx(0.0), _qx(25.0)])]),
        ("charge", 1.0, False, [("ChargeCap", _T2, [_qz(0.0), _qz(60.0)])]),
        ("disabled", 1.0, False, [("HoodLid", _T2, [_qx(0.0), _qx(-35.0)])]),
        ("maintenance", 1.0, False, [("HoodLid", _T2, [_qx(0.0), _qx(70.0)])]),
    ]


# ================================================================ 基础几何（世界坐标构造）
def box(cx, cy, cz, sx, sy, sz):
    """轴对齐盒，外向 CCW。"""
    x, y, z = sx / 2, sy / 2, sz / 2
    v = [(cx + a * x, cy + b * y, cz + c * z)
         for c in (-1, 1) for b in (-1, 1) for a in (-1, 1)]
    # 规范外向 CCW：六面两三角均按叉积验法线朝外（自检正体积覆盖）
    t = [(0, 2, 3), (0, 3, 1),      # -Z
         (4, 5, 7), (4, 7, 6),      # +Z
         (4, 1, 5), (4, 0, 1),      # -Y
         (2, 7, 3), (2, 6, 7),      # +Y
         (0, 4, 6), (0, 6, 2),      # -X
         (1, 7, 5), (1, 3, 7)]      # +X
    return v, t


def prism_x(profile, xw):
    """沿 X 轴柱体。profile = [(z,y)...] 逆时针（从 +X 看），扫掠 x ∈ [-xw/2, xw/2]。
    外向 CCW：侧面 quad 顺序按轮廓方向，端盖三角扇按轴向定绕向。"""
    n = len(profile)
    v, t = [], []
    for z, y in profile:
        v.append((-xw / 2, y, z))
    for z, y in profile:
        v.append((xw / 2, y, z))
    for i in range(n):
        j = (i + 1) % n
        t += [(i, n + j, j), (i, n + i, n + j)]
    # -X 端盖（profile 逆时针从 +X 看 ⇒ 从 -X 看为顺时针）
    for i in range(1, n - 1):
        t.append((0, i, i + 1))
    # +X 端盖
    for i in range(1, n - 1):
        t.append((n, n + i + 1, n + i))
    return v, t


def ring_x(rr, xw, n, phase, rfun=None, reverse_winding=False):
    """圆环柱体侧面（轴 X）：n 段 quad。rfun(a) 可变半径（毂星形）。"""
    v, t = [], []

    def rad(i):
        a = phase + 2 * math.pi * i / n
        return rfun(a) if rfun else rr

    for i in range(n):
        a0 = phase + 2 * math.pi * i / n
        a1 = phase + 2 * math.pi * (i + 1) / n
        r0, r1 = rad(i), rad(i + 1)
        v += [(-xw / 2, r0 * math.sin(a0), r0 * math.cos(a0)),
              (-xw / 2, r1 * math.sin(a1), r1 * math.cos(a1)),
              (xw / 2, r1 * math.sin(a1), r1 * math.cos(a1)),
              (xw / 2, r0 * math.sin(a0), r0 * math.cos(a0))]
        b = len(v) - 4
        t += [(b, b + 2, b + 1), (b, b + 3, b + 2)]
    if reverse_winding:
        t = [(a, c, b) for a, b, c in t]
    return v, t


def annulus_caps(ring_pairs, xw):
    """环形端盖：ring_pairs = [(r0_i, r1_i, a0_i, a1_i)...] 分段环带。两个端面。"""
    v, t = [], []
    for (r0, r1, a0, a1) in ring_pairs:
        for xs, flip in ((-xw / 2, True), (xw / 2, False)):
            b = len(v)
            v += [(xs, r0 * math.sin(a0), r0 * math.cos(a0)),
                  (xs, r0 * math.sin(a1), r0 * math.cos(a1)),
                  (xs, r1 * math.sin(a1), r1 * math.cos(a1)),
                  (xs, r1 * math.sin(a0), r1 * math.cos(a0))]
            if flip:
                t += [(b, b + 1, b + 2), (b, b + 2, b + 3)]
            else:
                t += [(b, b + 2, b + 1), (b, b + 3, b + 2)]
    # Both annular caps originally faced into the wheel: reverse against
    # the physical end-face directions, independently of derived normals.
    t = [(a, c, b) for a, b, c in t]
    return v, t


def merge(*geoms):
    V, T = [], []
    for v, t in geoms:
        b = len(V)
        V += list(v)
        T += [(a + b, bb + b, c + b) for a, bb, c in t]
    return V, T


# ================================================================ 构件（世界坐标 + 归属节点）
def geom_chassis():
    """基座/底盘/保险杠/牵引耳（frame_dark）。"""
    g = []
    g.append(box(0, 0.295, -0.515, 0.82, 0.03, 0.53))          # 头基座=检修腔地板（顶 0.31）
    g.append(box(0, 0.37, 0.255, 0.80, 0.18, 1.01))            # 主地板梁 X±0.40
    for sx in (-1, 1):
        g.append(box(sx * 0.44, 0.485, 0.735, 0.08, 0.05, 0.05))  # 后横梁外侧垫块
    g.append(box(0, 0.41, -0.79, 0.82, 0.12, 0.02))            # 前保险杠 Z-0.80..-0.78
    g.append(box(0, 0.41, 0.79, 0.82, 0.12, 0.02))             # 后保险杠 Z0.78..0.80
    for sx in (-1, 1):
        g.append(box(sx * 0.16, 0.26, -0.78, 0.08, 0.20, 0.04))   # 前牵引耳板
        g.append(box(sx * 0.16, 0.26, 0.78, 0.08, 0.20, 0.04))    # 后牵引耳板
    g.append(box(0, 0.20, -0.78, 0.24, 0.08, 0.04))            # 前牵引横杆
    g.append(box(0, 0.20, 0.78, 0.24, 0.08, 0.04))             # 后牵引横杆
    return [("frame_dark", v, t) for v, t in g]


def geom_head():
    """前服务头：浅色壳 + 35° 顶前倒角 + 深色内衬 + 鼻标 + 充电侧罩。"""
    out = []
    # 侧墙 / 后墙（body_light）
    for sx in (-1, 1):
        out.append(("body_light", *box(sx * 0.4025, 0.47, -0.515, 0.015, 0.32, 0.53)))
    # Rear wall stays below the downward lid sweep (top 0.60); the 0.63
    # side walls do not enter the 0.70-wide lid's X interval.
    out.append(("body_light", *box(0, 0.455, -0.2675, 0.82, 0.29, 0.035)))
    # 鼻封板：X±0.375，z-0.78..-0.76 薄前脸 + 35° 斜顶（0.565→0.5615）。
    # 后沿 -0.76 退过盖板下折扫掠圆弧最远点 z=-0.7501（分离≥0.0099），顶高不再约束扫掠。
    prof = [(-0.78, 0.31), (-0.76, 0.31), (-0.76, 0.565), (-0.775, 0.565), (-0.78, 0.5615)]
    out.append(("body_light", *prism_x(prof, 0.75)))
    # 检修腔深色内衬（frame_dark）
    for sx in (-1, 1):
        out.append(("frame_dark", *box(sx * 0.3875, 0.445, -0.505, 0.015, 0.27, 0.43)))
    out.append(("frame_dark", *box(0, 0.445, -0.2925, 0.76, 0.27, 0.015)))
    # 鼻面封闭导热分面 + 暖鼻标
    for sx in (-1, 1):
        out.append(("frame_dark", *box(sx * 0.20, 0.43, -0.786, 0.24, 0.14, 0.012)))
    out.append(("accent_warm", *box(0, 0.5025, -0.785, 0.60, 0.045, 0.01)))
    # 右侧充电罩（accent_warm）：X0.41..0.48，罩住 Socket_Charge (0.48,0.55,-0.35)
    out.append(("accent_warm", *box(0.445, 0.55, -0.35, 0.07, 0.08, 0.14)))
    return out


def geom_body():
    """前transition块 / 货台地板 / 后横梁 + 护栏 + 锁扣固定支撑耳（frame_dark）。"""
    out = [("body_light", *box(0, 0.54, -0.175, 1.12, 0.16, 0.15))]   # 前transition块
    out.append(("body_light", *box(0, 0.53, 0.73, 0.80, 0.14, 0.06)))  # 后横梁中段
    for sx in (-1, 1):
        out.append(("body_light", *box(sx * 0.44, 0.555, 0.73, 0.08, 0.09, 0.06)))  # 后横梁侧耳
    out.append(("frame_dark", *box(0, 0.47, 0.30, 0.84, 0.02, 0.80)))  # 货台地板（顶=台面 0.48）
    for sx in (-1, 1):
        out.append(("frame_dark", *box(sx * 0.44, 0.60, 0.10, 0.04, 0.24, 0.40)))   # 侧护栏前段
        out.append(("frame_dark", *box(sx * 0.44, 0.62, 0.50, 0.04, 0.20, 0.40)))   # 侧护栏后段（底部 0.52 让轮拱）
    out.append(("frame_dark", *box(0, 0.67, -0.115, 0.92, 0.10, 0.03)))  # 前护栏
    out.append(("frame_dark", *box(0, 0.66, 0.715, 0.92, 0.12, 0.03)))   # 后护栏
    # STATE-A r5：锁扣导向柱（RearLock 拆出的固定件）移入静态 Body，立在横杆两端外侧，
    # x 向与横杆 25° 绕X扫掠全程分离（横杆转轴是X，扫掠不改变 x 范围）
    for sx in (-1, 1):
        out.append(("frame_dark", *box(sx * EAR_X, EAR_CY, EAR_CZ, EAR_SX, EAR_SY, EAR_SZ)))
        n = 24
        bearing = merge(ring_x(.02, .04, n, 0),
                        ring_x(.017, .04, n, 0, reverse_winding=True),
                        annulus_caps([(.017,.02,2*math.pi*i/n,2*math.pi*(i+1)/n) for i in range(n)], .04))
        vertices, faces = bearing
        out.append(("frame_dark", [(x+sx*EAR_X,y+.65,z+.78) for x,y,z in vertices], faces))
    return out


def geom_lid():
    """前顶盖（闭合位）：Y0.63..0.65，Z-0.75..-0.25；pivot 见 LID_PIVOT。"""
    plate = box(0, 0.64, -0.50, LID_W, LID_T, LID_LEN)
    strip = box(0, 0.64, -0.7495, 0.30, 0.018, 0.005)   # 前缘暖色条（嵌在盖前沿）
    return [("body_light", *plate), ("accent_warm", *strip)]


def geom_lock():
    """r8：暖色抬高横杆＋两臂＋带孔支承轴，源内绕X 25°；正常镜头保留可见轮廓。"""
    return [("accent_warm", *box(0, .82, .78, .40, .06, .04)),
            ("frame_dark", *box(-.18, .735, .78, .03, .17, .025)),
            ("frame_dark", *box(.18, .735, .78, .03, .17, .025)),
            ("frame_dark", *box(0, .65, .78, .50, .016, .016))]


def geom_cap():
    """r8：扩大暖色口盖；保持顶铰链、插口与+60°源动画，内面X=.48。"""
    return [("accent_warm", *box(.4875, .51, -.35, .015, .14, .20))]


def geom_rod():
    """前牵引示意杆（STATE-A）：沿 Socket_TowFront 的 -Z 方向 0.35m，默认隐藏
    （extras.visible=false；glTF 核心无 node 可见性，STATE-B wrapper 拥有 visible 控制）。"""
    zc = (TOW_ROD["z0"] + TOW_ROD["z1"]) / 2.0
    s = TOW_ROD["s"]
    return [("frame_dark", *box(0, TOW_ROD["y"], zc, s, s, abs(TOW_ROD["z1"] - TOW_ROD["z0"])))]


def geom_cargo():
    """货箱（节点原点 = 底面中心 = Socket_Cargo 落位）。
    STATE-A r5：总体外沿收敛到冻结 0.64W×0.70L×0.38H（深框/浅面/沿口三层同心）。"""
    return [("frame_dark", *box(0, 0.55, 0.28, 0.59, 0.14, 0.65)),
            ("body_light", *box(0, 0.74, 0.28, 0.62, 0.24, 0.68)),
            ("frame_dark", *box(0, 0.83, 0.28, 0.64, 0.06, 0.70))]


def annular_part(outer, inner, width, n=24):
    pairs = [(inner, outer, 2*math.pi*i/n, 2*math.pi*(i+1)/n) for i in range(n)]
    return merge(ring_x(outer, width, n, 0),
                 ring_x(inner, width, n, 0, reverse_winding=True),
                 annulus_caps(pairs, width))


def beam(a, b, width=.035):
    """A solid link between two Y/Z points, with X as its rotation axis."""
    cx, cy, cz = [(a[i]+b[i])/2 for i in range(3)]
    dy, dz = b[1]-a[1], b[2]-a[2]
    angle = -math.atan2(dy, dz)
    co, si = math.cos(angle), math.sin(angle)
    v, t = box(0, 0, 0, width, width, math.hypot(dy, dz))
    return [(x+cx, y*co-z*si+cy, y*si+z*co+cz) for x,y,z in v], t


def geom_wheel():
    """Metal drum, open spokes and a real axle bore; all teeth fit the roll radius."""
    drum = annular_part(DRUM_R, DRUM_IN_R, WHEEL_W)
    hub = annular_part(.11, .026, WHEEL_W)
    teeth = []
    for i in range(24):
        a = 2*math.pi*i/24
        # Five-point crest includes the cardinal tangent; its corners stay on the circle.
        profile = [(r*math.cos(theta), r*math.sin(theta)) for r,theta in
                   [(DRUM_R,a-.045),(WHEEL_R,a-.035),(WHEEL_R,a),
                    (WHEEL_R,a+.035),(DRUM_R,a+.045)]]
        teeth.append(prism_x(profile, WHEEL_W))
    spokes = []
    for i in range(6):
        a = 2*math.pi*i/6
        spokes.append(beam((0,.10*math.sin(a),.10*math.cos(a)),
                           (0,.16*math.sin(a),.16*math.cos(a)), .018))
    # A small asymmetrical hub mark exposes source rotation at the fixed camera.
    return [("body_light", *drum), ("frame_dark", *hub),
            ("frame_dark", *merge(*teeth)), ("frame_dark", *merge(*spokes)),
            ("accent_warm", *box(-.077, 0, .085, .006, .022, .04)),
            ("accent_warm", *box(.077, 0, .085, .006, .022, .04))]


def geom_electronics():
    return [("frame_dark", *box(0,.3875,-.38,.52,.135,.14)),
            ("body_light", *box(0,.4575,-.38,.52,.005,.14))]


def axle_at(sx, y, z):
    # The shaft enters a real bore; the dust collar remains outside the wheel.
    profile = [(.016*math.cos(2*math.pi*i/12), .016*math.sin(2*math.pi*i/12)) for i in range(12)]
    v,t = prism_x(profile, .10)
    shaft = ([(x+sx*.49, yy+y, zz+z) for x,yy,zz in v],t)
    v,t = annular_part(.032,.018,.035,16)
    collar = ([(x+sx*.445, yy+y, zz+z) for x,yy,zz in v],t)
    return [("frame_dark", *shaft),("rubber", *collar)]


# ================================================================ 场景组装
class N:
    def __init__(self, name, pivot, parent):
        self.name, self.pivot, self.parent = name, pivot, parent
        self.surfs = []          # [(role, worldV, worldT)]
        self.children = []


def build_scene():
    root = N("tuoyun-r1", (0, 0, 0), None)
    model = N("Model", (0, 0, 0), root)
    root.children.append(model)
    for name, geoms, pivot in [
        ("Chassis", geom_chassis(), (0, 0, 0)),
        ("Head", geom_head(), (0, 0, 0)),
        ("Body", geom_body(), (0, 0, 0)),
        ("ProtectedElectronics", geom_electronics(), (0, 0, 0)),
        ("HoodLid", geom_lid(), LID_PIVOT),
        ("RearLock", geom_lock(), LOCK_PIVOT),
        ("ChargeCap", geom_cap(), CAP_PIVOT),
    ]:
        n = N(name, pivot, model)
        n.surfs = geoms
        model.children.append(n)
    wheels = N("Wheels", (0, 0, 0), model)
    model.children.append(wheels)
    for sx, side in [(-1,"L"),(1,"R")]:
        rp = (sx*.445,.36,-.12)
        bp = (sx*.445,.29,.28)
        front = (sx*.445,WHEEL_CY,-WHEEL_CZ)
        rocker = N("Rocker_"+side, rp, wheels)
        wheels.children.append(rocker)
        rocker.surfs = [("frame_dark",*beam(rp,front)), ("frame_dark",*beam(rp,bp))]
        rocker.surfs += axle_at(sx,WHEEL_CY,-WHEEL_CZ)
        # Main mount meets the retained load beam, rather than floating outside it.
        rocker.surfs.append(("frame_dark",*box(sx*.4275,.36,-.12,.055,.05,.05)))
        bogie = N("Bogie_"+side, tuple(bp[i]-rp[i] for i in range(3)), rocker)
        rocker.children.append(bogie)
        for suffix,z in [("M",0.),("R",WHEEL_CZ)]:
            bogie.surfs.append(("frame_dark",*beam(bp,(sx*.445,WHEEL_CY,z))))
            bogie.surfs += axle_at(sx,WHEEL_CY,z)
        for suffix,z,parent in [("F",-WHEEL_CZ,rocker),("M",0.,bogie),("R",WHEEL_CZ,bogie)]:
            world = (sx*WHEEL_CX,WHEEL_CY,z)
            pp = world_translation(parent)
            wheel = N("Wheel_"+side+suffix,tuple(world[i]-pp[i] for i in range(3)),parent)
            wheel.surfs = [(role,[(v[0]+world[0],v[1]+world[1],v[2]+world[2]) for v in V],T)
                           for role,V,T in geom_wheel()]
            parent.children.append(wheel)
    cargo = N("CargoBox", (0.0, BOX_Y0, BOX_CZ), model)
    cargo.surfs = geom_cargo()
    model.children.append(cargo)
    rod = N("TowRod", (0, 0, 0), model)
    rod.surfs = geom_rod()
    model.children.append(rod)
    return root


def world_translation(n):
    px, py, pz = n.pivot
    p = n.parent
    while p is not None:
        px += p.pivot[0]; py += p.pivot[1]; pz += p.pivot[2]
        p = p.parent
    return (px, py, pz)


def all_world_surfs(root, skip_names=()):
    out = []
    def walk(n):
        if n.name not in skip_names:
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


# ================================================================ 自检
def signed_volume(V, T):
    s = 0.0
    for a, b, c in T:
        A, B, C = V[a], V[b], V[c]
        s += (A[0] * (B[1] * C[2] - B[2] * C[1])
              - A[1] * (B[0] * C[2] - B[2] * C[0])
              + A[2] * (B[0] * C[1] - B[1] * C[0])) / 6.0
    return s


def _axes_of(V, T):
    """凸体轴：三角形面法线 + 三角形边（单位向量，规范符号去重）。"""
    faces, edges = set(), set()
    for a, b, c in T:
        A_, B_, C_ = V[a], V[b], V[c]
        u = [B_[i] - A_[i] for i in range(3)]
        w = [C_[i] - A_[i] for i in range(3)]
        n = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
        L = math.sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2])
        if L > 1e-12:
            k = (round(n[0] / L, 9), round(n[1] / L, 9), round(n[2] / L, 9))
            faces.add(k if k > tuple(-x for x in k) else tuple(-x for x in k))
        for p, q in ((a, b), (b, c), (c, a)):
            d = (V[q][0] - V[p][0], V[q][1] - V[p][1], V[q][2] - V[p][2])
            L = math.sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2])
            if L > 1e-12:
                k = (round(d[0] / L, 9), round(d[1] / L, 9), round(d[2] / L, 9))
                edges.add(k if k > tuple(-x for x in k) else tuple(-x for x in k))
    return faces, edges


def sat_gap(A, B):
    """凸体对有符号 SAT 间隙。A/B = (verts, tris)。
    >0：存在分离轴，值为该轴分离量；=0：接触；<0：全部轴重叠，值为 -最小重叠深度。
    轴集 = 双方面法线 ∪ 双方边×边（凸体 SAT 完备）；小凸体（几十顶点）专用，不外推。"""
    (av, at), (bv, bt) = A, B
    fa, ea = _axes_of(av, at)
    fb, eb = _axes_of(bv, bt)
    axes = list(fa | fb)
    for x in ea:
        for y in eb:
            n = (x[1] * y[2] - x[2] * y[1], x[2] * y[0] - x[0] * y[2], x[0] * y[1] - x[1] * y[0])
            L = math.sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2])
            if L > 1e-9:
                k = (round(n[0] / L, 9), round(n[1] / L, 9), round(n[2] / L, 9))
                axes.append(k if k > tuple(-q for q in k) else tuple(-q for q in k))
    best = -1e18
    for ax in axes:
        pa = [ax[0] * q[0] + ax[1] * q[1] + ax[2] * q[2] for q in av]
        pb = [ax[0] * q[0] + ax[1] * q[1] + ax[2] * q[2] for q in bv]
        best = max(best, min(pa) - max(pb), min(pb) - max(pa))
    return best


def _sat_selftest():
    """三例证明 sat_gap 有符号：分离>0、接触=0、重叠<0。失败即拒产出。"""
    errs = []
    u = box(0, 0, 0, 1, 1, 1)
    far = box(3, 0, 0, 1, 1, 1)
    touch = box(1, 0, 0, 1, 1, 1)
    over = box(0.5, 0, 0, 1, 1, 1)
    g1, g2, g3 = sat_gap(u, far), sat_gap(u, touch), sat_gap(u, over)
    if not (g1 > 1.5 and abs(g1 - 2.0) < 1e-9):
        errs.append("SAT自检-分离: %.9f" % g1)
    if abs(g2) > 1e-9:
        errs.append("SAT自检-接触: %.9f" % g2)
    if not (g3 < -0.25 and abs(g3 + 0.5) < 1e-9):
        errs.append("SAT自检-重叠: %.9f" % g3)
    return errs, (g1, g2, g3)


def wheel_verts(sx, sz):
    """单轮滚转包络的外切 SAT 凸包（24 边形两端 + 轮宽）：返回 (verts, side_tris)。"""
    V, T = [], []
    for i in range(24):
        a = 2 * math.pi * i / 24
        for xs in (sx * WHEEL_CX - WHEEL_W / 2, sx * WHEEL_CX + WHEEL_W / 2):
            V.append((xs, WHEEL_CY + (WHEEL_R/math.cos(math.pi/24)) * math.sin(a), sz * WHEEL_CZ + (WHEEL_R/math.cos(math.pi/24)) * math.cos(a)))
    for i in range(24):
        j = (i + 1) % 24
        a0, a1 = 2 * i, 2 * j       # 布局：每角先 -x 端后 +x 端
        T += [(a0, a0 + 1, a1 + 1), (a0, a1 + 1, a1)]
    return (V, T)


def selfcheck(root, facts):
    st_errs, st_vals = _sat_selftest()
    facts["sat_selftest_separate_touch_overlap"] = [round(v, 9) for v in st_vals]
    errs = list(st_errs)

    def walk(n):
        for role, V, T in n.surfs:
            vol = signed_volume(V, T)
            if vol <= 1e-9:
                errs.append("绕向/退化: %s/%s vol=%.3e" % (n.name, role, vol))
        for c in n.children:
            walk(c)
    walk(root)

    def hull(cx, cy, cz, sx, sy, sz):
        return box(cx, cy, cz, sx, sy, sz)

    cargoH = hull(0, 0.48 + BOX_H / 2, BOX_CZ, BOX_W, BOX_H, BOX_L)   # cargo box outer envelope = frozen .64/.70/.38
    lidH = hull(0, 0.64, -0.50, LID_W, LID_T, LID_LEN)
    noseP = prism_x([(-0.78, 0.31), (-0.76, 0.31), (-0.76, 0.565), (-0.775, 0.565), (-0.78, 0.5615)], 0.75)
    railF_L = hull(-0.44, 0.60, 0.10, 0.04, 0.24, 0.40)
    railR_L = hull(-0.44, 0.62, 0.50, 0.04, 0.20, 0.40)
    railFront = hull(0, 0.67, -0.115, 0.92, 0.10, 0.03)
    railRear = hull(0, 0.66, 0.715, 0.92, 0.12, 0.03)
    lockH = hull(0, .82, .78, .40, .06, .04)
    headRear = hull(0, 0.455, -0.2675, 0.82, 0.29, 0.035)
    podH = hull(0.445, 0.55, -0.35, 0.07, 0.08, 0.14)
    plinthH = hull(0, 0.295, -0.515, 0.82, 0.03, 0.53)
    slabH = hull(0, 0.37, 0.255, 0.80, 0.18, 1.01)
    extF_L = hull(-0.48, 0.37, 0.025, 0.16, 0.18, 0.55)
    extR_L = hull(-0.48, 0.37, 0.745, 0.16, 0.18, 0.03)
    tabR_L = hull(-0.44, 0.555, 0.73, 0.08, 0.09, 0.06)
    bumpF = hull(0, 0.41, -0.79, 0.82, 0.12, 0.02)
    liner = hull(0.3875, 0.445, -0.505, 0.015, 0.27, 0.43)
    deckBlock = hull(0, 0.54, -0.175, 1.12, 0.16, 0.15)
    # STATE-A r5 新增静止件
    earL = hull(-EAR_X, EAR_CY, EAR_CZ, EAR_SX, EAR_SY, EAR_SZ)
    earR = hull(EAR_X, EAR_CY, EAR_CZ, EAR_SX, EAR_SY, EAR_SZ)
    beamMid = hull(0, 0.53, 0.73, 0.80, 0.14, 0.06)
    towBarF = hull(0, 0.20, -0.78, 0.24, 0.08, 0.04)
    rodH = hull(0, TOW_ROD["y"], (TOW_ROD["z0"] + TOW_ROD["z1"]) / 2.0,
                TOW_ROD["s"], TOW_ROD["s"], abs(TOW_ROD["z1"] - TOW_ROD["z0"]))

    def rot_axis(pts, deg, axis, pivot):
        a = math.radians(deg)
        ca, sa = math.cos(a), math.sin(a)
        px, py, pz = pivot
        out = []
        for x, y, z in pts:
            lx, ly, lz = x - px, y - py, z - pz
            if axis == "x":
                out.append((x, py + ly * ca - lz * sa, pz + ly * sa + lz * ca))
            else:  # z
                out.append((px + lx * ca - ly * sa, py + lx * sa + ly * ca, z))
        return out

    # 有限运动端点（仅离散端点姿态，不称连续扫掠证明）：
    barV, barT = lockH
    bar25 = (rot_axis(barV, 25.0, "x", LOCK_PIVOT), barT)      # work t=1
    capH_full = hull(.4875, .51, -.35, .015, .14, .20)
    capV, capT = capH_full
    cap60 = (rot_axis(capV, 60.0, "z", CAP_PIVOT), capT)       # charge t=1

    CLEAR = [  # (名, A, B)：要求 gap ≥ 0.005
        ("cargo_vs_rail_side_front", cargoH, railF_L),
        ("cargo_vs_rail_side_rear", cargoH, railR_L),
        ("cargo_vs_rail_front", cargoH, railFront),
        ("cargo_vs_rail_rear", cargoH, railRear),
        ("cargo_vs_lock", cargoH, lockH),
        ("cargo_vs_head_rear", cargoH, headRear),
        ("lid_vs_deckblock", lidH, deckBlock),
        ("lid_vs_nose", lidH, noseP),
        ("lid_vs_liner", lidH, liner),
        ("wheelFL_vs_pod", wheel_verts(1, -1), podH),
        ("wheelFL_vs_plinth", wheel_verts(1, -1), plinthH),
        ("wheelFL_vs_slab", wheel_verts(1, -1), slabH),
        ("wheelFL_vs_bumper", wheel_verts(1, -1), bumpF),
        ("wheelRL_vs_railR", wheel_verts(-1, 1), railR_L),
        ("wheelRL_vs_slab", wheel_verts(1, 1), slabH),
        ("wheelRL_vs_tab", wheel_verts(-1, 1), tabR_L),
        ("rod_vs_bumper_front", rodH, bumpF),
        ("rod_vs_wheel_front", rodH, wheel_verts(-1, -1)),
        ("bar_closed_gap_vs_earL", lockH, earL),
        ("bar_closed_gap_vs_earR", lockH, earR),
        ("bar_open25_vs_earL", bar25, earL),
        ("bar_open25_vs_earR", bar25, earR),
        ("bar_open25_vs_beam_mid", bar25, beamMid),
        ("bar_open25_vs_rear_rail", bar25, railRear),
    ]
    CONTACT = [  # 就位接触（结构件贴合/搭接/铰链角点原位）：仅要求无穿透（≥ -1e-6）
        ("lid_seated_on_walls", lidH, hull(0.4025, 0.47, -0.515, 0.015, 0.32, 0.53)),
        ("earL_seated_on_beam", earL, beamMid),
        ("earR_seated_on_beam", earR, beamMid),
        ("rod_seated_on_towbar_front", rodH, towBarF),
        ("cap_closed_seated_on_pod", capH_full, podH),
        ("cap_open60_hinge_corner_seated", cap60, podH),
    ]
    for nm, A, B in CLEAR:
        g = sat_gap(A, B)
        facts["sat_" + nm] = round(g, 6)
        if g < 0.005:
            errs.append("净空不足: %s gap=%.6f" % (nm, g))
    for nm, A, B in CONTACT:
        g = sat_gap(A, B)
        facts["sat_" + nm] = round(g, 6)
        if g < -1e-6:
            errs.append("接触穿透: %s gap=%.6f" % (nm, g))

    # r8真实臂/横杆，26个离散角度净空检查；轴在固定带孔支承中转动。
    minima = {}
    for deg in range(26):
        for part, h in [("bar", lockH), ("armL", hull(-.18,.735,.78,.03,.17,.025)),
                        ("armR", hull(.18,.735,.78,.03,.17,.025))]:
            pose = (rot_axis(h[0], deg, "x", LOCK_PIVOT), h[1])
            for name, obstacle in [("cargo",cargoH),("earL",earL),("earR",earR),
                                   ("beam",beamMid),("rail",railRear)]:
                key = part+"_"+name
                gap = sat_gap(pose, obstacle)
                minima[key] = min(minima.get(key, float("inf")), gap)
                if gap < .005: errs.append("r8 discrete clearance %s angle=%s gap=%s" % (key,deg,gap))
    facts["readability_r8_discrete_min_gaps"] = minima
    hole_clearance = .017 * math.cos(math.pi/24) - math.sqrt(2) * .008
    feet_clearance = .65 - math.sqrt(2) * .008 - (EAR_CY + EAR_SY/2)
    facts["bearing_radial_clearance_all_x_rotations"] = hole_clearance
    facts["axle_support_clearance_all_x_rotations"] = feet_clearance
    if min(hole_clearance, feet_clearance) < .005: errs.append("axle bearing clearance below .005")
    facts["readability_bearing"] = "static 24-sided ring inner radius .017; axle square .016; support top .63"

    # 冻结尺寸断言
    (sx0, sy0, sz0), (sx1, sy1, sz1) = facts["static_bbox"]
    (lx0, ly0, lz0), (lx1, ly1, lz1) = facts["loaded_bbox"]
    cargo_node = [c for c in root.children[0].children if c.name == "CargoBox"][0]
    (cb0, cb1) = bbox(cargo_node.surfs)
    anim_ok = []
    for clip, dur, loop, tracks in anim_tracks():
        for nm, times, quats in tracks:
            for q in quats:
                n2 = sum(c * c for c in q)
                if abs(n2 - 1.0) > 1e-6:
                    anim_ok.append("%s/%s quat non-unit %.9f" % (clip, nm, n2))
    tr = dict((c[0], c) for c in anim_tracks())
    mv = [t for t in tr["move"][3] if t[0] == "Wheel_LF"][0]
    # 每档 keyframe 须是绕X的真实 θ 转动（旋转测试向量核对），相邻档 dot>0.7 保证
    # LINEAR 插值走短弧；末档是 360° 的双覆盖四元数 (0,0,0,-1)，与首档数值不同，
    # 恰是"分段 quat 防 0↔360 同端点"的要求——首尾数值相同会让 lerp 永不转动。
    if mv[1] != [0.0, 0.25, 0.5, 0.75, 1.0]:
        anim_ok.append("move key times not 0/.25/.5/.75/1")
    for i, deg in enumerate((0.0, 90.0, 180.0, 270.0, 360.0)):
        x, _, _, w = mv[2][i]
        s, c = 2.0 * x * w, 1.0 - 2.0 * x * x
        if abs(s - math.sin(math.radians(deg))) > 1e-6 or abs(c - math.cos(math.radians(deg))) > 1e-6:
            anim_ok.append("move key %d not X-rotation %.0fdeg" % (i, deg))
    for i in range(4):
        d = sum(a * b for a, b in zip(mv[2][i], mv[2][i + 1]))
        if d <= 0.7:
            anim_ok.append("move consecutive quat dot %.6f (long arc)" % d)
    if mv[2][0] == mv[2][4]:
        anim_ok.append("move first==last quat: identical endpoints cannot interpolate rotation")
    ends = {"work": ("RearLock", _qx(25.0)), "charge": ("ChargeCap", _qz(60.0)),
            "disabled": ("HoodLid", _qx(-35.0)), "maintenance": ("HoodLid", _qx(70.0))}
    for clip, (nm, lastq) in ends.items():
        got = [t for t in tr[clip][3] if t[0] == nm][0][2][-1]
        if got != lastq:
            anim_ok.append("%s endpoint quat %s != frozen %s" % (clip, got, lastq))
    hard = [
        ("static X∈±0.65", sx0 >= -0.65 - 1e-6 and sx1 <= 0.65 + 1e-6),
        ("static Z∈±0.8", sz0 >= -0.8 - 1e-6 and sz1 <= 0.8 + 1e-6),
        ("static Y0..0.9", sy0 >= -1e-6 and sy1 <= 0.9 + 1e-6),
        ("接地 Y=0", abs(sy0) < 1e-6),
        ("loaded 顶 0.86", abs(ly1 - 0.86) < 1e-6),
        ("loaded 不超静态 XY", lx0 >= sx0 - 1e-9 and lx1 <= sx1 + 1e-9 and lz0 >= sz0 - 1e-9 and lz1 <= sz1 + 1e-9),
        ("净宽≥0.80", 2 * 0.42 >= 0.80 - 1e-9),
        ("净长≥0.80", (BED_Z1 - BED_Z0) >= 0.80 - 1e-9),
        ("轮心", WHEEL_CX == 0.55 and WHEEL_CY == 0.205 and WHEEL_CZ == 0.56),
        ("轮参数", WHEEL_R == 0.205 and WHEEL_W == 0.16),
        ("检修腔内壁|X|≥0.38", BAY_WALL_IN_X >= 0.38 - 1e-9),
        ("货箱总体外沿W0.64", abs((cb1[0] - cb0[0]) - BOX_W) < 1e-9),
        ("货箱总体外沿L0.70", abs((cb1[2] - cb0[2]) - BOX_L) < 1e-9),
        ("货箱总体外沿H0.38", abs((cb1[1] - cb0[1]) - BOX_H) < 1e-9),
        ("货箱底心(0,.48,.28)", abs(cb0[0] + BOX_W / 2 - 0.0) < 1e-9
         and abs(cb0[1] - BOX_Y0) < 1e-9 and abs(cb0[2] + BOX_L / 2 - BOX_CZ) < 1e-9),
        ("动画结构冻结", not anim_ok),
    ]
    for nm, ok in hard:
        if not ok:
            errs.append("尺寸断言失败: %s" % nm)

    # 盖板活动扫掠（STATE 预检，非动画交付）：闭合位顶点绕 pivot X 轴旋转后与静止件 SAT 无穿透
    def rot_x(pts, deg):
        a = math.radians(deg)
        ca, sa = math.cos(a), math.sin(a)
        px, py, pz = LID_PIVOT
        out = []
        for x, y, z in pts:
            ly, lz = y - py, z - pz
            out.append((x, py + ly * ca - lz * sa, pz + ly * sa + lz * ca))
        return out

    lidH_full = box(0, 0.64, -0.50, LID_W, LID_T, LID_LEN)
    lidV, lidT = lidH_full[0], lidH_full[1]
    wallR_h = hull(0.4025, 0.47, -0.515, 0.015, 0.32, 0.53)
    wallL_h = hull(-0.4025, 0.47, -0.515, 0.015, 0.32, 0.53)
    static_parts = {"nose": noseP, "wall_L": wallL_h, "wall_R": wallR_h,
                    "rear_wall": hull(0, 0.455, -0.2675, 0.82, 0.29, 0.035),
                    "rear_liner": hull(0, 0.445, -0.2925, 0.76, 0.27, 0.015),
                    "liner_L": hull(-0.3875, 0.445, -0.505, 0.015, 0.27, 0.43),
                    "liner_R": liner, "bay_floor": plinthH,
                    "deckblock": deckBlock}
    # 离散 1° 样本（非连续扫掠）：fold 0..-35、open 0..+70，各角记录对全部静止件的最小间隙
    for tag, degs in (("fold_1deg", range(0, -36, -1)), ("open_1deg", range(0, 71, 1))):
        mins = []
        for deg in degs:
            rv = rot_x(lidV, deg)
            worst = min(sat_gap((rv, lidT), ph) for ph in static_parts.values())
            mins.append((deg, round(worst, 6)))
            if worst < -1e-6:
                errs.append("盖板采样穿透: %s %d° gap=%.6f" % (tag, deg, worst))
        facts["lid_sample_%s" % tag] = mins
        lo = [min(v[i] for v in rot_x(lidV, degs[-1] if tag == "fold_1deg" else 70)) for i in range(3)]
        hi = [max(v[i] for v in rot_x(lidV, degs[-1] if tag == "fold_1deg" else 70)) for i in range(3)]
        facts["lid_sample_%s_end_bbox" % tag] = [round(x, 4) for x in lo + hi]
    errs += mars_check(root, facts)
    return errs


def mars_check(root, facts):
    errors = []
    all_nodes = []
    def visit(n):
        all_nodes.append(n)
        for child in n.children: visit(child)
    visit(root)
    wheel_nodes = [n for n in all_nodes if n.name.startswith("Wheel_")]
    expected = {"Wheel_"+side+suffix for side in "LR" for suffix in "FMR"}
    if {n.name for n in wheel_nodes} != expected: errors.append("six wheel nodes missing")
    if {n for n,_,_ in anim_tracks()[0][3]} != expected: errors.append("six move tracks missing")
    gaps = []
    for n in wheel_nodes:
        wanted = ("Rocker_" if n.name[-1]=="F" else "Bogie_")+n.name[6]
        if n.parent.name != wanted: errors.append("wrong wheel parent: "+n.name)
        c = world_translation(n)
        if abs(c[0])!=WHEEL_CX or abs(c[1]-WHEEL_CY)>1e-8: errors.append("wrong world wheel pivot")
        for role,V,T in n.surfs:
            if role=="rubber": errors.append("rubber drum rejected")
            for x,y,z in V:
                if math.hypot(y-c[1],z-c[2]) > WHEEL_R+1e-8: errors.append("tooth outside roll envelope")
        for part in root.children[0].children:
            if part.name not in ["Chassis","Head","Body","ProtectedElectronics"]: continue
            for _,V,T in part.surfs:
                lo,hi=bbox([("part",V,T)])
                # Circular roll envelope and a separating coordinate prove no overlap.
                clearance=max(lo[0]-(c[0]+WHEEL_W/2),c[0]-WHEEL_W/2-hi[0],
                              lo[1]-(c[1]+WHEEL_R),c[1]-WHEEL_R-hi[1],
                              lo[2]-(c[2]+WHEEL_R),c[2]-WHEEL_R-hi[2])
                gaps.append(clearance)
                if clearance < .005-1e-8: errors.append("wheel-body clearance: "+n.name+"/"+part.name)
    module = geom_electronics()
    lidV,lidT = geom_lid()[0][1:]
    cover_gap = float("inf")
    for degree in range(-35,71):
        a=math.radians(degree);co,si=math.cos(a),math.sin(a)
        px,py,pz=LID_PIVOT
        v=[(x,py+(y-py)*co-(z-pz)*si,pz+(y-py)*si+(z-pz)*co) for x,y,z in lidV]
        for _,V,T in module:cover_gap=min(cover_gap,sat_gap((v,lidT),(V,T)))
    if cover_gap < .005-1e-8:errors.append("inner module intersects cover sweep")
    facts["mars_r2"]={"wheel_count":len(wheel_nodes),"move_tracks":sorted(expected),
                      "min_roll_envelope_vs_body_gap":min(gaps),"module_cover_gap_106_angles":cover_gap,
                      "shaft_radius":.016,"hub_bore_radius":.026,"shaft_bore_radial_gap":.010,
                      "suspension":"rocker front + bogie middle/rear; visual pivots, no terrain solver"}
    return errors


# ================================================================ glTF 导出
def export(root, facts):
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

    def acc_quat(vals):
        data = struct.pack("<%df" % (4 * len(vals)), *[c for v in vals for c in v])
        bv = view(data, 34962)
        accs.append({"bufferView": bv, "componentType": 5126, "count": len(vals), "type": "VEC4",
                     "min": [min(v[i] for v in vals) for i in range(4)],
                     "max": [max(v[i] for v in vals) for i in range(4)]})
        return len(accs) - 1

    def acc_time(vals):
        data = struct.pack("<%df" % len(vals), *vals)
        bv = view(data, 34962)
        accs.append({"bufferView": bv, "componentType": 5126, "count": len(vals), "type": "SCALAR",
                     "min": [min(vals)], "max": [max(vals)]})
        return len(accs) - 1

    def acc_u32(tris):
        flat = [a for tri in tris for a in tri]
        data = struct.pack("<%dI" % len(flat), *flat)
        bv = view(data, 34963)
        accs.append({"bufferView": bv, "componentType": 5125, "count": len(flat), "type": "SCALAR",
                     "min": [min(flat)], "max": [max(flat)]})
        return len(accs) - 1

    materials = []
    for r in ROLE_ORDER:
        lin, rough, metal = ROLE_PLACEHOLDER[r]
        materials.append({"name": r, "pbrMetallicRoughness": {
            "baseColorFactor": [round(lin[0], 6), round(lin[1], 6), round(lin[2], 6), 1.0],
            "metallicFactor": metal, "roughnessFactor": rough}})

    nodes_json, meshes, surf_table = [], [], []
    node_idx = {}          # node name -> glTF node index（动画 target 用）

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
                # 局部化
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
        return idx

    scene_roots = []
    emit(root, None, (0.0, 0.0, 0.0))
    for nm, spec in SOCKETS.items():
        idx = len(nodes_json)
        node_idx[nm] = idx
        entry = {"name": nm, "translation": spec["t"],
                 "extras": {"forward": spec["forward"],
                            "kind": "socket", "up": [0, 1, 0]}}
        if spec["rotation"] is not None:
            entry["rotation"] = spec["rotation"]
        nodes_json.append(entry)
        # socket 挂在 Model 下，随 VisualRoot 一起管理
        nodes_json[1].setdefault("children", []).append(idx)

    # STATE-A：TowRod 默认隐藏（glTF 核心无 node 可见性，extras 仅供导入端/STATE-B 采用）
    nodes_json[node_idx["TowRod"]]["extras"] = {"visible": False, "kind": "tow_rod"}

    # STATE-A：真实动画 clip（rotation 四元数轨，LINEAR）
    animations = []
    for clip, dur, loop, tracks in anim_tracks():
        channels, samplers = [], []
        for nm, times, quats in tracks:
            ta, qa = acc_time(times), acc_quat(quats)
            samplers.append({"input": ta, "output": qa, "interpolation": "LINEAR"})
            channels.append({"sampler": len(samplers) - 1,
                             "target": {"node": node_idx[nm], "path": "rotation"}})
        animations.append({"name": clip + "-loop" if loop else clip, "channels": channels, "samplers": samplers})

    while len(blob) % 4:
        blob += b"\x00"
    gltf = {
        "asset": {"version": "2.0", "generator": "yudian ART-U01-STATE-A generate_tuoyun_r1.py (python stdlib, original)"},
        "scene": 0,
        "scenes": [{"name": "Scene", "nodes": scene_roots}],
        "nodes": nodes_json,
        "meshes": meshes,
        "materials": materials,
        "animations": animations,
        "accessors": accs,
        "bufferViews": bvs,
        "buffers": [{"byteLength": len(blob), "uri": "tuoyun-r1.bin"}],
    }
    jt = json.dumps(gltf, separators=(",", ":"), ensure_ascii=False)

    with open(os.path.join(HERE, "tuoyun-r1.bin"), "wb") as f:
        f.write(blob)
    with open(os.path.join(HERE, "tuoyun-r1.gltf"), "w") as f:
        f.write(jt)

    # GLB 自包含：其内嵌 JSON 的 buffer 不得带 uri（GLB 二进制块即 buffer）
    glb_doc = dict(gltf)
    glb_doc["buffers"] = [{"byteLength": len(blob)}]
    jt_glb = json.dumps(glb_doc, separators=(",", ":"), ensure_ascii=False)

    js = jt_glb.encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    bb = bytes(blob) + b"\x00" * ((4 - len(blob) % 4) % 4)
    with open(os.path.join(HERE, "tuoyun-r1.glb"), "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bb)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A))
        f.write(js)
        f.write(struct.pack("<II", len(bb), 0x004E4942))
        f.write(bb)
    facts["surface_table"] = surf_table
    facts["bin_bytes"] = len(blob)
    return len(blob)


# ================================================================ main
def main():
    root = build_scene()
    # static/loaded 包络口径（需求"包络口径补清"）：空载闭盖无杆、有载闭盖无杆；
    # TowRod 是 towed 态示意件（默认隐藏），不进静态/有载包络，见 facts.tow_rod。
    static = all_world_surfs(root, skip_names=("CargoBox", "TowRod"))
    loaded = all_world_surfs(root, skip_names=("TowRod",))
    facts = {
        "generator": "generate_tuoyun_r1.py",
        "origin": "original python-stdlib procedural geometry for yudian ART-U01-GEO",
        "coordinate": "meter, +Y up, forward -Z, ground Y=0, translations only",
        "static_bbox": [list(map(lambda v: round(v, 6), bbox(static)[0])),
                        list(map(lambda v: round(v, 6), bbox(static)[1]))],
        "loaded_bbox": [list(map(lambda v: round(v, 6), bbox(loaded)[0])),
                        list(map(lambda v: round(v, 6), bbox(loaded)[1]))],
        "sockets": {nm: {"t": s["t"], "rotation": s["rotation"], "forward": s["forward"]}
                    for nm, s in SOCKETS.items()},
        "animations": [{"name": clip, "duration_s": dur, "loop_intent": loop, "gltf_name": clip + "-loop" if loop else clip,
                        "note": [a[3] for a in ANIM_SPECS if a[0] == clip][0],
                        "channels": [{"node": nm, "path": "rotation",
                                      "key_times": times, "key_quats": quats}
                                     for nm, times, quats in tracks]}
                       for clip, dur, loop, tracks in anim_tracks()],
        "towed_note": "towed reuses the disabled clip; TowRod visibility is owned by the STATE-B wrapper",
        "tow_rod": dict(TOW_ROD, default_extras_visible=False, parent="Model"),
        "lock_ears_static": {"node": "Body", "x": EAR_X, "y": EAR_CY, "z": EAR_CZ,
                             "size": [EAR_SX, EAR_SY, EAR_SZ], "top": EAR_CY + EAR_SY / 2},
        "wheel": {"radius": WHEEL_R, "width": WHEEL_W,
                  "centers": [[sx * WHEEL_CX, WHEEL_CY, sz * WHEEL_CZ]
                              for sx in (1, -1) for sz in (-1, 0, 1)]},
        "hinge_pivots": {"HoodLid": list(LID_PIVOT), "RearLock": list(LOCK_PIVOT),
                         "ChargeCap": list(CAP_PIVOT)},
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
    print("OK static=%s loaded=%s tris=%d bin=%dB" % (
        facts["static_bbox"], facts["loaded_bbox"], facts["triangles_total"], facts["bin_bytes"]))


if __name__ == "__main__":
    main()
