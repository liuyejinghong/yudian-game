#!/usr/bin/env python3
# ART-F02-GEO 独立核验：直接解析成品 processor-r1.gltf/.bin/.glb 与 prototype 副本，
# 不复用生成器内部状态。检查：GLB 自包含（buffer 无 uri）、结构与 bin 一致、
# 每个 primitive 绕向（闭合正符号体积，外向 CCW）、socket 旋转把局部 -Z 映射到合同 forward、
# 两份 GLB 字节相同。只读，不写不删。动画/SAT/导入 NOT_RUN。

import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROTO = os.path.join(HERE, "..", "..", "..", "..", "prototype", "assets", "facilities", "processor-r1", "processor-r1.glb")

fails = []


def chk(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


def quat_apply(q, v):
    x, y, z, w = q
    # glTF 四元数 (x,y,z,w) 旋转向量
    return [
        (1 - 2 * (y * y + z * z)) * v[0] + 2 * (x * y - w * z) * v[1] + 2 * (x * z + w * y) * v[2],
        2 * (x * y + w * z) * v[0] + (1 - 2 * (x * x + z * z)) * v[1] + 2 * (y * z - w * x) * v[2],
        2 * (x * z - w * y) * v[0] + 2 * (y * z + w * x) * v[1] + (1 - 2 * (x * x + y * y)) * v[2],
    ]


def vol_of(V, T):
    s = 0.0
    for a, b, c in T:
        A, B, C = V[a], V[b], V[c]
        s += (A[0] * (B[1] * C[2] - B[2] * C[1])
              - A[1] * (B[0] * C[2] - B[2] * C[0])
              + A[2] * (B[0] * C[1] - B[1] * C[0])) / 6.0
    return s


def decode(doc, blob, mesh_idx):
    """返回 [(prim, positions, indices)]。"""
    m = doc["meshes"][mesh_idx]
    out = []
    for p in m["primitives"]:
        pa = doc["accessors"][p["attributes"]["POSITION"]]
        ia = doc["accessors"][p["indices"]]
        pv = doc["bufferViews"][pa["bufferView"]]
        iv = doc["bufferViews"][ia["bufferView"]]
        pos = struct.unpack_from("<%df" % (3 * pa["count"]), blob, pv["byteOffset"] + pa.get("byteOffset", 0))
        idx = struct.unpack_from("<%dI" % ia["count"], blob, iv["byteOffset"] + ia.get("byteOffset", 0))
        V = [tuple(pos[3 * i:3 * i + 3]) for i in range(pa["count"])]
        T = [tuple(idx[3 * i:3 * i + 3]) for i in range(ia["count"] // 3)]
        out.append((p, V, T))
    return out


def main():
    with open(os.path.join(HERE, "processor-r1.gltf")) as f:
        doc = json.load(f)
    with open(os.path.join(HERE, "processor-r1.bin"), "rb") as f:
        bin_data = f.read()

    # 1) .gltf 引用 .bin 且长度一致
    chk(doc["buffers"] == [{"byteLength": len(bin_data), "uri": "processor-r1.bin"}],
        "gltf buffers 声明 = bin 实长 %d" % len(bin_data))

    # 2) 每个 primitive：绕向正体积（独立于生成器的 face-winding 检查）+ accessor min/max 一致
    nprim, ntri, bad_winding, bad_minmax = 0, 0, 0, 0
    for mi in range(len(doc["meshes"])):
        for p, V, T in decode(doc, bin_data, mi):
            nprim += 1
            ntri += len(T)
            if vol_of(V, T) <= 1e-9:
                bad_winding += 1
            pa = doc["accessors"][p["attributes"]["POSITION"]]
            for i in range(3):
                if abs(min(v[i] for v in V) - pa["min"][i]) > 1e-6 or abs(max(v[i] for v in V) - pa["max"][i]) > 1e-6:
                    bad_minmax += 1
    chk(bad_winding == 0, "face-winding: %d/%d primitive 闭合正体积" % (nprim - bad_winding, nprim))
    chk(bad_minmax == 0, "accessor min/max 与顶点一致")
    chk(ntri == 1140, "三角面总数 1140（实测 %d）" % ntri)

    # 3) 材质角色名
    names = [m["name"] for m in doc["materials"]]
    chk(names == ["frame_dark", "body_light", "accent_warm"], "材质角色 = %s" % names)

    # 4) socket：位置精确、旋转把局部 -Z 映射到 extras.forward（误差 <1e-6）
    for nd in doc["nodes"]:
        if nd.get("extras", {}).get("kind") == "socket":
            q = nd.get("rotation", [0, 0, 0, 1])
            fwd = quat_apply(q, [0, 0, -1])
            want = nd["extras"]["forward"]
            ok = all(abs(fwd[i] - want[i]) < 1e-6 for i in range(3))
            chk(ok, "%s 旋转->局部-Z = %s (want %s)" % (nd["name"], [round(x, 6) for x in fwd], want))

    # 5) GLB：chunk 结构、内嵌 JSON 的 buffer 无 uri（自包含）、BIN 块 = .bin 字节
    with open(os.path.join(HERE, "processor-r1.glb"), "rb") as f:
        glb = f.read()
    magic, ver, total = struct.unpack_from("<III", glb, 0)
    jlen, jtype = struct.unpack_from("<II", glb, 12)
    blen, btype = struct.unpack_from("<II", glb, 20 + jlen)
    chk(magic == 0x46546C67 and ver == 2 and total == len(glb), "GLB 头部长度自洽 (%dB)" % len(glb))
    chk(jtype == 0x4E4F534A and btype == 0x004E4942, "GLB chunk 类型 JSON/BIN")
    gdoc = json.loads(glb[20:20 + jlen])
    chk("uri" not in gdoc["buffers"][0], "GLB buffer 无 uri（自包含）")
    gbin = glb[28 + jlen:28 + jlen + blen]
    chk(gbin[:len(bin_data)] == bin_data and blen >= len(bin_data), "GLB BIN 块与 .bin 字节一致")

    # 6) 源/资产两份 GLB 相同
    with open(PROTO, "rb") as f:
        proto_glb = f.read()
    chk(proto_glb == glb, "prototype 副本与源 GLB 字节相同 (%dB)" % len(proto_glb))

    # 7) 结构：根/Model/活动件节点存在
    node_names = [n["name"] for n in doc["nodes"]]
    need = ["processor-r1", "Model", "Base", "Chamber", "PressGuide", "PressRam", "FeedBed",
            "FeedGate", "Gantry", "StopGate", "OutputRack", "ServicePort", "PowerPort",
            "ServiceCover", "InputCrate", "OutputCrate",
            "Socket_Input", "Socket_Output", "Socket_PowerIn", "Socket_Service"]
    miss = [n for n in need if n not in node_names]
    chk(not miss, "节点齐全（缺：%s）" % (miss or "无"))

    # 8) STATE-A 源动画：3 clips、LINEAR、times 无 target、关键帧与 t=.5/1.5 位姿差
    anims = doc.get("animations", [])
    anames = [a["name"] for a in anims]
    chk(anames == ["work-loop", "disabled", "maintenance"], "动画 clip = %s" % anames)

    def anim_data(a):
        out = {}
        for c in a["channels"]:
            nname = doc["nodes"][c["target"]["node"]]["name"]
            s = a["samplers"][c["sampler"]]
            ia = doc["accessors"][s["input"]]
            oa = doc["accessors"][s["output"]]
            iv = doc["bufferViews"][ia["bufferView"]]
            ov = doc["bufferViews"][oa["bufferView"]]
            chk("target" not in iv, "%s/%s times bufferView 无 target" % (a["name"], nname))
            chk(s["interpolation"] == "LINEAR", "%s/%s LINEAR" % (a["name"], nname))
            t = struct.unpack_from("<%df" % ia["count"], bin_data, iv["byteOffset"])
            comp = {"VEC3": 3, "VEC4": 4}[oa["type"]]
            v = struct.unpack_from("<%df" % (comp * oa["count"]), bin_data, ov["byteOffset"])
            out[(nname, c["target"]["path"])] = (t, v, comp)
        return out

    wl = anim_data(anims[0])
    chk(set(wl) == {("PressRam", "translation"), ("FeedGate", "rotation")}, "work-loop 通道正确")
    WL_T = (0.0, 0.45, 0.5, 0.55, 1.0, 1.45, 1.5, 1.55, 2.0)
    t, v, _ = wl[("PressRam", "translation")]
    chk(len(t) == 9 and len(v) == 27 and all(abs(t[i] - WL_T[i]) < 1e-6 for i in range(9)),
        "work-loop 压头 9 键×VEC3（极值平台，times=%.3g..%.3g）" % (t[1], t[-2]))
    # 平台键：t∈[.45,.55] 全压 Y1.55，t∈[1.45,1.55] 半压 Y1.725，t=1/2 回 Y1.90
    chk(all(abs(v[3 * i + 1] - e) < 1e-6 for i, e in
            ((1, 1.55), (2, 1.55), (3, 1.55), (4, 1.90), (5, 1.725), (6, 1.725), (7, 1.725), (8, 1.90))),
        "压头平台值 [.45-.55]=1.55 / [1.45-1.55]=1.725 / t=1,2=1.90")
    t, v, _ = wl[("FeedGate", "rotation")]
    q = [tuple(v[4 * i:4 * i + 4]) for i in range(9)]
    unit = all(abs(qi[0] ** 2 + qi[1] ** 2 + qi[2] ** 2 + qi[3] ** 2 - 1.0) < 1e-6 for qi in q)
    chk(unit and abs(q[0][3] - 1.0) < 1e-6 and q[2] != q[6]
        and abs(q[2][0] - math.sin(math.radians(12.5))) < 1e-6
        and abs(q[6][0] - math.sin(math.radians(6.25))) < 1e-6,
        "挡板四元数单位化、t=0 identity、t=.5 平台 25°、t=1.5 平台 12.5°（位姿不同）")
    dm = anim_data(anims[1])
    t, v, _ = dm[("StopGate", "translation")]
    chk(tuple(t) == (0.0, 1.0) and abs(v[4] - 0.95) < 1e-6 and abs(v[1] - 1.75) < 1e-6,
        "disabled 停机挡板 Y1.75→0.95（-0.80）")
    mt = anim_data(anims[2])
    t, v, _ = mt[("ServiceCover", "rotation")]
    q1 = tuple(v[4:8])
    exp = (0.0, math.sin(math.radians(35.0)), 0.0, math.cos(math.radians(35.0)))
    chk(all(abs(q1[i] - exp[i]) < 1e-6 for i in range(4)), "maintenance 检修盖 rotY 70°")

    print("VERIFY_RESULT " + ("PASS" if not fails else "FAIL(%d)" % len(fails)))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
