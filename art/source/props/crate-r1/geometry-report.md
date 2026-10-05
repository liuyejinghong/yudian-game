# crate-r1 · geometry-report（ART-P01-CRATE-GEO）

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
| 0 | lower_frame | frame_dark | 12 | 8 |
| 1 | body | body_light | 12 | 8 |
| 2 | top_rim | frame_dark | 12 | 8 |
| 3 | id_plate_front | accent_warm | 12 | 8 |

三角面合计 48；GLB 材质数 3（仅本票用到的三角色）。

## AABB（源与 GLB 一致）

min [-0.32, 0.0, -0.35] / max [0.32, 0.38, 0.35]（米），与冻结包络逐轴一致，min Y = 0 落地。

## SHA256

| 文件 | SHA256 |
|---|---|
| crate-r1.gltf | `b363d6af9daecb9e7873368d2d28703d704a1757190380314bb5e0f0f1f4e2ac` |
| crate-r1.bin | `76f292e9691f0fea8e5f792708829e0f4ca17070bd27a14594a338777fdca69b` |
| crate-r1.glb（源目录） | `07c1b3b65d9298735ae8673239942a84dde2070f80daa6f48e4e01a3e7503d68` |
| prototype 私有副本 crate-r1.glb | `07c1b3b65d9298735ae8673239942a84dde2070f80daa6f48e4e01a3e7503d68` |

## 自检与验证实录（本机 stdlib 实测）

- 写前自查：合法索引、有限数、逐零件外向闭合体积（signed volume = 尺寸积）、冻结包络、落地 Y=0；失败非零退出。
- 负例：`python3 generate_crate_r1.py --selftest` —— 错包络 / 负尺寸 / 非有限坐标 / 未知角色 / 越界索引 / 单面翻绕序均被拒绝。
- 确定性：同一工作树从不同 cwd 重跑，GLB 字节一致（SHA 相同）；无时间戳/随机数进 GLB。
- 独立回读：交付前用单独脚本解析 GLB 二进制块，从原始 buffer 重算逐 surface AABB、闭合体积、角色与三角数，与 facts 一致。

重跑方式：`cd <任意目录> && python3 art/source/props/crate-r1/generate_crate_r1.py`（会重写本目录四件并同步 prototype 私有 GLB 副本）。本轮耗时 0.001s。

## 本生成器未运行（主控实测另见双线证据索引）（本票未做、不做宣称）

- Godot 4 导入实测（角色/AABB 实机复核）、冻结白昼 normal/close 取证渲染：主控执行。
- manifest 公共表接入、preview 场景接线、碰撞体、LOD、动画、保存/库存接入：不在本票。
- 母票 ART-P01 回收连接件、最终量产比例：母票保持 DRAFT。
