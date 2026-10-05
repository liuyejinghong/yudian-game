# ART-F02-GEO · 加工设施 processor r1 静态几何报告

**版本提示：** 下文保留原r1/r9记录，其哈希/计数不描述当前GLB。当前manifest revision 2与本轮实测见[mars-r2-report](mars-r2-report.md)。

日期：2026-10-04。执行：GLM 几何工人（GEO-WRITE 缩片）；独立接受：主控。状态：**SUBMITTED（工人交件，未自签接受）**。
本票只交静态 GEO：idle 默认位姿、四活动件独立父节点+铰轴/行程锚点、端点顶点 AABB；动画/STATE/包装未制作（NOT_RUN）。

> **STATE-A 追加（当前源状态）**：`export()` 已在同一生成器内追加 3 个源 clips（work-loop/disabled/maintenance），
> 几何/节点/角色零改动（静态 AABB、1140 tris、27 surface、全部自检与下文实测不变）。下表 hash 是 **3e98c33 的
> GEO 历史 hash，已被覆盖**；当前源 hash 以[state-source-report.md](state-source-report.md)为准：
> gltf `43a71c97…`、bin `8f570f53…`、glb `773575a5…`、facts `458301a5…`、生成器 `58815088…`。

## 交付物与 hash（SHA256，3 次重跑字节一致；本表为 3e98c33 GEO 历史）

| 文件 | SHA256 |
|---|---|
| `generate_processor_r1.py`（原创生成器，Python 3.14 标准库） | `f6fc78b1605343976a3f76aa2c1666499f58bce767b0480928dd820cfdd16eee` |
| `processor-r1.gltf`（引用 .bin） | `16fe33eb2bf576d5d00dcff51654d3b5e043909e6dc712753deb6a2c20ccea1f` |
| `processor-r1.bin` | `e50f58c399a7ecae74d9e3ca8757c59f52a23f64b0c0121d222ddc7a2f4378fc` |
| `processor-r1.glb`（自包含，buffer 无 uri；同字节复制到 `prototype/assets/facilities/processor-r1/`） | `303c1d6ea0510ef80bae3c4276430d2b5c267f140e174f3441760a89bf363728` |
| `geometry-facts.json`（生成期实测） | `566b92e0eda5788199466e2927537dc05ee3ce104409e4ab8ee7e68e3ce3b355` |
| `verify_processor_gltf.py`（独立核验脚本） | `b61cadb0bdd847602941a7a8413c7d7248ca30f43bf67833a9ebd28beb5fab1c` |

## 坐标与结构

米制、+Y 上、前方 -Z、根 identity、落 Y0；全部 mesh 节点仅平移（socket 另带烘入旋转）。
静态顶点 AABB 实测 `[[-1.8,0,-2.4],[1.8,2.4,2.4]]` ＝ 3.6×4.8×2.4，恰在冻结外包络内、接地 Y=0。

节点树：`processor-r1 → Model → {Base, Chamber, PressGuide, PressRam, FeedBed, FeedGate, Gantry, StopGate, OutputRack, ServicePort, PowerPort, ServiceCover, InputCrate, OutputCrate} + 4×Socket_*`。14 mesh 节点、27 surface、1140 tris、3 占位材质（角色名）、0 纹理；bin 31,920B / GLB 53,608B。

| 分区 | 实测（米） |
|---|---|
| 底座 | 平板 X±1.8 Z±2.4 Y0..0.16 + 腔下基座台/两条支承条/4 角垫/4 螺栓垫（Y 至 0.24） |
| 主腔 | 结构面包络 X±1.15、Z-0.80..1.00、Y0.20..2.40（深下框 0.30..1.10 + 浅上壳 1.10..2.36 + 平顶环 2.36..2.40 深色内板）；后饰条凸出 Z 面 0.012（腔节点 Z 至 1.012，静态整件包络仍由底座 ±2.4 决定） |
| +X 压头 | `PressRam` 独立节点，默认上位 X1.23..1.40 / Y1.60..2.20 / Z±0.40（宽 0.8）；静导轨+顶梁 `PressGuide` |
| 进料区 -Z | 实体低床 X±0.60、Z-2.40..-0.80、床面 Y0.48，3 条朝内粗槽（凹 0.06，槽底 Y0.42）；腔前喉道口 X±0.60、Y0.48..1.02、深 0.35 |
| 出料区 +Z | 短架 X±0.45、Z1.00..2.40、床面 Y0.48，4 腿镂空 +2 条通到外沿 Z2.40 的朝外粗槽 |
| 私有摆件 | 进/出各一独立节点 `InputCrate`(Z-1.90..-1.20) / `OutputCrate`(Z1.30..2.00)，外包络实测均 0.64×0.70×0.38、底 Y0.48、深框+浅面+深沿；常驻可见，不构成库存；箱位避开挡板/压头行程，不占用 socket 标记 |

## 活动件（独立父节点，idle 默认位；端点为真实顶点 AABB，动画 NOT_RUN）

| 件 | pivot（节点锚） | 未来动作 | 端点 AABB 实测 |
|---|---|---|---|
| PressRam | (1.315,1.90,0) | 垂直下行 0.35 | X1.23..1.40, Y1.25..1.85, Z±0.40（全在包络内） |
| FeedGate | (0,0.50,-0.84) 铰 X | 内摆 25° | X±0.55, Y0.487..0.946, Z-0.885..-0.614（入喉道，与侧壁留 0.01） |
| StopGate | (0,1.75,-2.29) | 落下 0.80 | X±0.58, Y0.50..1.40, Z-2.334..-2.246（罩进料口，双面外露 45° 斜条） |
| ServiceCover | (-1.17,1.60,0.90) 铰 Y | 外开 70° | X-2.211..-1.163, Y1.20..2.00, Z0.505..0.919（**超出基座 X1.8 至 -2.21，属活动包络**，与所有静件实测留隙） |

停机大挡板 idle 收起于进料口门架（Y1.30..2.20），门架顶梁齐 Y2.40；盖后为深 0.40 检修腔（X-1.10..-0.70，内衬深色+腔内机组）。

## Socket（位置冻结；forward 烘成节点真实旋转，up +Y，独立核验脚本逐个回读通过）

| 节点 | 位置 | forward（局部 -Z 实际指向） |
|---|---|---|
| Socket_Input | (0,0.48,-2.2) | -Z（identity） |
| Socket_Output | (0,0.48,2.2) | +Z（rotY 180°） |
| Socket_PowerIn | (1.6,0.35,0) | +X（rotY -90°），电源法兰外端面 X1.60 |
| Socket_Service | (-1.3,1.0,0.4) | -X（rotY +90°），服务口面板外端面 X-1.30 |

料床中心标记不构成停靠规则；机械对接由技术线另行确认。

## 生成期自检（AABB 区间法，非 SAT）

每个基元闭合正符号体积（外向 CCW，无双面遮错）；冻结尺寸断言 8 条全过；28 组关键静/端点件对
区间检查全过：净空对最小 0.01（挡板内摆 vs 喉道侧壁），就位接触对（床-腔、箱-床面等 7 对）恰 0.0 无穿透。
**这是轴对齐区间判定，不是 SAT**：连续扫掠、斜条与旋转件全程穿透、以及 Godot 实际导入均 NOT_RUN（本缩片不跑 Godot）。

## 实际命令与结果

```
python3 art/source/facilities/processor-r1/generate_processor_r1.py
→ OK static=[[-1.8,0.0,-2.4],[1.8,2.4,2.4]] tris=1140 bin=31920B   （重跑 3 次 hash 全同）
python3 art/source/facilities/processor-r1/verify_processor_gltf.py
→ 16/16 PASS（GLB 自包含、BIN 块=.bin、27 primitive 绕向正体积、socket 旋转回读、两份 GLB 字节相同）VERIFY_RESULT PASS
```

工具：Python 3.14.3（本机 python3）；无第三方依赖、无网络、无付费 API。

## 来源与简化

原创：按本仓需求冻结表逐参数程序化生成；导出器沿用本仓已接受的 U01 glTF/GLB 写法模式（项目惯例），
几何与结构为新设计，未复制 U01/共享道具。简化方案（未实施）：料床/出料架 land 并条、斜条 6→4、
腔墙分件合并，预计 ~700 tris，三级体量/摆件/挡板差异保留。

## NOT_RUN（本票范围外，未制作不冒充）

- Godot 实际导入/节点/mesh 读取（主控独立做；本缩片未跑 Godot/GUI）。
- SAT 连续扫掠与旋转件全程穿透（仅端点 AABB+区间法；STATE 片补）。
- 动画/2 秒循环 t0.5/1.5、七态、offline 映射、0°/90°/180° 实拍、preview wrapper、最终 manifest、M01 材质接线。
- 正常/近景镜头渲染、所有者视觉确认、性能/LOD 预算。
