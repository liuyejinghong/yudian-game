# ART-F02 STATE-A · processor r1 源动画小片报告

**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 2与本轮实测见[mars-r2-report](mars-r2-report.md)。

日期：2026-10-04。执行：GLM 几何工人；独立接受：主控。状态：**SUBMITTED（工人交件，未自签接受）**。
范围：只在既有 `generate_processor_r1.py` 的 `export()` 追加 3 个源 clips，**不改任何既有顶点/节点/尺寸/角色**
（生成期自检与 GEO 相同全过：静态 AABB 仍 [-1.8,0,-2.4]..[1.8,2.4,2.4]、1140 tris、27 surface 不变；
bin 31,920→32,152B，增量全部为动画 accessors）。

## 源 clips（全部只动子件局部 TRS，根/整栋不动，pivot 与正向沿 GEO 冻结值）

| clip 名 | 时长/循环 | 通道 | 关键帧（times → 值） |
|---|---|---|---|
| `work-loop` | 2s，loop=true（extras 标注；glTF 无标准 loop 字段，Godot loop_mode 由导入侧核/设） | PressRam/translation + FeedGate/rotation | t=[0,.45,.5,.55,1,1.45,1.5,1.55,2]；压头 Y=[1.90,1.55,1.55,1.55,1.90,1.725,1.725,1.725,1.90]（偏移 [0,-.35,-.35,-.35,0,-.175,-.175,-.175,0]）；挡板绕局部 X=[0°,25°,25°,25°,0°,12.5°,12.5°,12.5°,0°]。**极值平台**：主控实导发现 Godot 默认 30fps 重采样把单键 t=.5 落在 .5333、行程只到 Y1.59375/21.875°，故极值改为三键平台，采样损失后仍达冻结 -0.35m/25°；**t=0.5（全压+全开）与 t=1.5（半压+半开）位姿仍不同**，2s 周期不变 |
| `disabled` | 1s，loop=false | StopGate/translation | t=[0,1]；(0,1.75,-2.29)→(0,0.95,-2.29)，垂直落下 0.80 |
| `maintenance` | 1s，loop=false | ServiceCover/rotation | t=[0,1]；identity→绕局部 Y +70°（四元数 (0,sin35°,0,cos35°)，铰轴 COVER_PIVOT） |

技术口径：sampler 全 LINEAR；times 为 SCALAR float accessor 且其 bufferView **无 target**；translation 值 VEC3、
rotation 值 VEC4；同一 work-loop 两通道共用同一 times accessor；动画只写入 gltf/glb 文档顶层 `animations`，
几何 buffer/accessor 布局未重排。offline 仍只是 disabled 的展示词，**没有第八态**；move/charge/towed 为固定
设施 N/A，未造任何占位动作。四通道目标节点均经 node 名→索引映射写入 `target.node`。

## 交付物与 hash（SHA256；生成器重跑 2 次字节一致，源/资产 GLB 同字节）

> r1 版（单键极值，7f818c2）已被主控实测 30fps 重采样损失否决；下表为 **r2 平台键版（当前）**。

| 文件 | SHA256 |
|---|---|
| `generate_processor_r1.py`（export 追加动画导出，几何段零改动） | `58815088f3f7d704a4df12f1d58d09bc575c43e19db5a01766f7a7a845a0eaab` |
| `processor-r1.gltf` / `processor-r1.bin` | `43a71c974015f6b1c7095abdb2bc8eaea87a5c8985572af4c847ffe781be9958` / `8f570f53e77bbefe9ca74076067342c4d34e1d676b12ea25d828cd1f99cfde8f` |
| `processor-r1.glb`（自包含，复制到 `prototype/assets/facilities/processor-r1/`，两份同 hash） | `773575a5e7bcc946864680e5cbead04b53e3a815bebed2ab15519ed3de4ebc5a` |
| `verify_processor_gltf.py`（追加动画核验段） | `914e5bccfedc25229a2119e3018e644301762b0848ff3a8db88d0bca8e1b02af` |
| `geometry-facts.json`（新增 `animations` 段） | `458301a5025e25078ef9a760304e1807a2476f27314cedee4614b9efe152c2b6` |

GEO 报告中 3e98c33 的旧 hash 保留为历史；当前源以本表为准。

## 实际命令与结果

```
python3 art/source/facilities/processor-r1/generate_processor_r1.py
→ OK static=[[-1.8,0.0,-2.4],[1.8,2.4,2.4]] tris=1140 bin=32152B   （重跑 2 次 hash 全同）
python3 art/source/facilities/processor-r1/verify_processor_gltf.py
→ 16 项 GEO 检查 + 14 项动画检查全 PASS，VERIFY_RESULT PASS
   （clip 名/通道目标/LINEAR/times 无 target/压头 9 键平台 [.45-.55]=1.55、[1.45-1.55]=1.725/挡板四元数单位化且
    t=.5 平台 25°、t=1.5 平台 12.5°/停机挡板 1.75→0.95/检修盖 rotY70°/源与 prototype GLB 同字节）
```

工具：Python 3.14.3 标准库；无第三方依赖、无网络、无付费 API。

## NOT_RUN（本小片范围外，未制作不冒充）

- Godot 实际导入动画（clip 名到 Godot Animation 的映射、loop_mode=1 设置、播放/插值/quarter-pose 抽查）——主控独立核。
- GUI/截图/任何镜头渲染；preview wrapper、最终 canonical manifest、M01 材质接线（STATE 后续片）。
- 七态完整矩阵与完整复位/幂等检查、idle/work/disabled/maintenance 之外的态（move/charge/towed 为 N/A 无动作）、
  2 秒循环 t=0.5/1.5 实拍对照。
- 动画全程扫掠 SAT/穿插复核（端点区间检查已在 GEO 报告；中间插值姿态未跑 Godot）。
