# storage-r1 · geometry-report（ART-F03-GEO）

由 `generate_storage_r1.py` 生成；本报告为末次真实运行的实录。

## 冻结范围与来源

- 米制，+Y 上，前 -Z，原点 = 地基底面中心，落地 Y=0；无动画、无 socket、无游戏状态。
- 外沿 X±2.25 / Y 0…2.20 / Z±1.60：地基 4.5×0.12×3.2（frame_dark），托板顶 Y0.20/Y1.10 厚 0.06，
  四角柱中心 X±2.17/Z±1.52 截面 0.12，顶侧梁最高 2.20；浅色后侧窄横服务板（center Y1.85）与
  两侧窄遮护板（X±1.91，中部敞开）（body_light）；前面 -Z 开放；accent_warm 前取放标记仅为几何服务方向。
- 修订 r1.1（主控 Godot 实拍 REWORK 后冻结）：原整面后服务板+满顶遮护在 yaw0 固定机位遮挡全部示意货物，
  空/有图几乎一致；改为后侧窄横服务板与两侧窄遮护。地基、柱/梁/托板、12 箱位、单位/包络/角色不变。
- 箱示意：RackPayloadDemo 父下 12 个 P01 规格视觉箱（X -1.35/0/1.35 × Z -0.70/0.60 × 底 Y0.20/1.10），
  每箱独立节点可整体隐藏；形体最高 Y1.48。槽位非仓容量/库存。
- 箱体尺寸/角色复用现役 P01 crate-r1 冻结候选（只读依据，未改其文件）；导出辅助为最小 stdlib
  做法（沿用 U01/P01 方式）。原创程序化几何，无第三方/版权/图像生成资产。
- 所有网格为轴对齐盒，逐面平法线（硬表面）。

## 节点与 surface 角色

根 Storage → Structure（frame_dark 合并 11 盒）、RearPanel（body_light 窄横板）、TopCanopy
（body_light，两块窄遮护合并单 surface）、FrontMark（accent_warm）、RackPayloadDemo（空父，
12 箱各 4 surface：frame_dark/body_light/frame_dark/accent_warm）。
节点 18 个，mesh 16 个，surface 52 个，
三角面合计 756；GLB 材质数 3（三角色，正式接线用共享 art-r1 材质）。
逐 surface 明细见 geometry-facts.json surface_table；12 箱槽位见 payload_slots。

## 包络（源与 GLB 一致）

- 空架 static AABB：min [-2.25, 0.0, -1.6] / max [2.25, 2.2, 1.6]，与冻结外沿逐轴一致，min Y=0。
- 含示意 loaded AABB：min [-2.25, 0.0, -1.6] / max [2.25, 2.2, 1.6]（payload 全部位于结构包络内，两者重合）。

## SHA256

| 文件 | SHA256 |
|---|---|
| storage-r1.gltf | `ca4039a0b61ac511234d72b3db3c607e1a3737aedde80e42fcb1bc6a4848e7b1` |
| storage-r1.bin | `d2c8105bd3c4ceed1c00603a33363291349c940ac10d6ea5eb92a47bf0eca18a` |
| storage-r1.glb（源目录） | `dcaac7f78d9d11f2aa0b5fd9745f68d4ec4c8f6d4860c30e95a14586210d90ed` |
| prototype 私有副本 storage-r1.glb | `dcaac7f78d9d11f2aa0b5fd9745f68d4ec4c8f6d4860c30e95a14586210d90ed` |

## 自检与验证实录（本机 stdlib 实测）

- 写前自查：逐零件合法索引、有限数、外向闭合体积、冻结包络（货架/箱各自口径）、落地 Y0；
  结构自查：RackPayloadDemo 唯一、12 箱、逐箱 P01 surface 角色、空/有示意包络关系。失败非零退出。
- 负例：`python3 generate_storage_r1.py --selftest` —— 越包络 / 非有限 / 未知角色 / 缺 Payload 父 /
  越界索引 / 单面翻绕序均被拒绝。
- 确定性：从不同 cwd 重跑 GLB 字节一致（SHA 相同）；无时间戳/随机数进 GLB。
- 独立回读：单独脚本解析 GLB 二进制块，重算逐 surface AABB、闭合体积、角色、三角数与
  逐顶点法线轴向（硬表面），与 facts 一致。

重跑方式：`cd <任意目录> && python3 art/source/facilities/storage-r1/generate_storage_r1.py`
（重写本目录四件并同步 prototype 私有 GLB 副本）。本轮耗时 0.007s。

## 本生成器未运行（主控实测另见双线证据索引）

- Godot 4 导入实测、冻结白昼 normal/close 空/有对照取证：主控执行。
- STATE/preview 接线、仓容量/库存/存档/正式场景接入、动画、运行状态、正式 socket：未制作。
