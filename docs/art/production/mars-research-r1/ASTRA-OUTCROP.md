# ART-MARS-OUTCROP-R1 · Astra冻结制作单

2026-10-07｜IN_PROGRESS：mars_outcrop_astra已真实派单并制作；最后技术/视觉核验与状态见本目录README。用户指定Astra xhigh，默认Blender。只制作这一件，非全套高保真重制。

## 目标与参考

原创风化交错层状砂岩露头，与本轮Gale/Stimson风积砂岩—沙砾场景一致。它是自然环境物，不是“铁矿/铜矿”，无资源/通行/障碍/采量语义。

必须先读 [MARS-BRIEF](MARS-BRIEF.md)、[VISUAL-SOURCES](VISUAL-SOURCES.md) 与 [参考来源/处理](references/sources.json)，实际view_image看本地stimson-close.jpg、murray-buttes-shape.jpg、color-processing.jpg。PIA21044/21042处理未声明：用形体不用精确色值。自然色只是近似观感，正式网格/贴图原创，不能把NASA照片贴到网格上。

看图要保留：主岩面薄层与斜交层理，被少量纵向节理截断；层边突出/凹蚀，有磨损和破口；岩脚角状薄片与沙积关系。参考图是风积砂岩，不叫Murray泥岩。禁止规律同宽水平槽、规则环圈叠出石头、光滑噪声球、彩色千层糕。

## 冻结形体与材质

- 单件组合约5–7m宽、3–4.5m深、1.5–2.4m高；根原点XY中心、Z地面（Blender），GLB+Y上、米制scale1。主露头可有自然不规则外包络，不强行整齐6×4×2长方体。
- 一个不对称低矮主露头，2–4道主要破裂/分块，不是几个同形石球；两处侧/背面露层，顶面局部风化台阶/缺口，破面切断层理。少量接壤折断岩板和8–18个原创薄碎片作为同一件构图；各块大小/方向有因果差别。
- 厚床组与薄纹理两层尺度；部分薄层倾斜/被截断；每个块不要全周一致刻线。形体边沿略风化但大轮廓棱角仍清楚，不搞等高圆圈堆叠。
- 岩面粗糙非金属，弱暖灰褐/浅灰褐变化；断面和顶部/底脚尘覆有所区别。细粒与侵蚀用实际可导出的PBR纹理或足够网格detail；不是Blender专用节点导出后纯色。
- 允许原创程序纹理烘焙，建议1024–2048px、GLB内嵌basecolor/normal/roughness。不用新依赖、不拿NASA照片做runtime贴图；若不用纹理，必须解释近景表面如何仍有可信粒度且在Godot实际导入可见。
- 面数是质量/性能折中目标，不是硬性低面竞赛；首轮预估12k–50k三角面，必要时合理说明。细碎毫米粒不做百万几何；技术PASS不能取代真实质感。
- 不含自带地面/天空/日光，根base可有0.1–0.2m埋入余量，manifest写明；root负责场景与接地。不给小石头metalness/发光作稀有资源。

## 交付、文件归属与验证

独占 art/source/environment/mars-outcrop-r1/**。交可编辑 mars-outcrop-r1.blend、mars-outcrop-r1.glb、必要原创新纹理、构建/再导出脚本、manifest.json（实际bounds、单位轴、材质/纹理、三角面、hash、原创声明/参考）、check.py（检查GLB结构/纹理/AABB并可在保存blend重开检查）。仅此目录，可写自身README与渲染图。不要改root看样场景、其他资产、总TODO、Main/C#、M01、canonical材料。

本轮lookdev由root集成同场景normal/near/reverse/low。Astra先在Blender实际渲染至少正面/背面，对照形体/纹理观察，再宣布交件；记录具体限制。固定Godotnormal位置(25.12,28.8,26.3)、target(4,0,-2.5)、FOV50；near约(12,4.2,9)、target(6,0.8,0)。hero建议放(6,terrain_y,0)，root最后确定接地。

工具本机已有Blender4.5.14：tools-bin在工作树上层的workspace。原生GPU若sandbox Metal失败，使用require_escalated本机已授权原生渲染/导出，禁止把失败当完成。导出先应用transform/法线检查，再真实保存重开导出对照。不能伪造视觉/性能接受。

五行报告：选择的形体与参考；交付路径；实际Blender/导出验证；面数/纹理/bounds和限制；等待root什么检查。完成不commit其他人的文件，不push/merge；root独立验收、提交。
