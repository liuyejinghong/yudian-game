# 真实火星视觉依据、场景框架与Astra单素材试片

2026-10-07；用户明确授权联网调研真实火星照片/探测器资料、完善所有素材前置需求、模拟环境风格并搭场景框架，挑一件交Astra xhigh制作；默认Blender。基线main718905d，独立分支art/mars-research-r1-20261007。PR43旧候选保留REWORK，不将其当目标质量。

目标：有一手照片/地质依据的视觉brief与全资产需求矩阵，一个可重复同条件看样的火星环境框架，一件按冻结brief实际制作并核验的Astra素材。事实、设计推论、游戏抽象分记。

范围：docs/art/art-direction.md（最新依据入口/默认Blender工具两处更新）；docs/art/production/mars-research-r1/**；art/environment/mars-lookdev-r1/**（独立Godot看样项目、场景源/材质/生成脚本）；Astra试片独占art/source/environment/mars-outcrop-r1/**；支撑地形worker独占lookdev/ground/**；root写父目录场景/材质与资产副本。根AGENTS仅按所有者Lessons要求追加本次纠正记录，不改Lessons以上；根PLAN/TODO、美术总TODO、Main/C#/工程文档、共享M01、现役canonical单位/设施、模拟resourceIDs/配方/保存均不改。矿物学与资源改名先交有依据的视觉建议，不借调研写新经济规则。

1. [x] primary-source调研：NASA/JPL/ESA等地面照片、彩色处理、地质矿物与探测器工况。证明：直接读sourcepage/图片，按地点/mission/处理方式/credit登记，资料链可复查；研究worker各自独占一文件。
2. [x] architect前置审查；root整合MARS-BRIEF.md及全资产矩阵，选择一个一致的地区参考，冻结正常/近景/尺度/光照/形体/材质/失败图标准。证明：需求能直接派单，哪些仍需工程事实/性能或产品决策有明确边界。
3. [x] 搭独立RTS看样场景框架：近中远层次、风化表土/砂砾/露岩/地平线，已有设备尺度参照；不复刻真实着陆点，不对照片RGB作精确反射率认证。证明：实际Godot白昼normal/near/低画质图，固定输入、相机/源hash，区别独立样景与默认游戏。
4. [x] 派Astra（gpt-6-astra/xhigh、明确工人角色、fork none）制作一件原创风化层状基岩露头。证明：任务包含研究、实际参考、冻结brief、独占源/.blend/GLB/必要材质纹理/manifest与可复跑check；不派无资料空白题，不人为限制成上批低面数块。
5. [x] root独立验收Astra源重开/再导出/轴向尺度/形体材质/资源身份、同场景normal+near+低画质读图。证明：运行输出与可复跑检查；必要局部返工，不用面数/技术PASS代替真实风格品质。
6. [x] architect收尾、文档/范围/身份检查、提交与交付；完整游戏接入/所有者最终视觉/整场性能各自留未验。证明：精确HEAD、资料来源链接、场景/素材可定位，未改主控占有文件。

同一步两失败记录失败并重新计划；源/旧图不覆盖。持续推进，不因新票/追问替换目标。付费资产与大量遥感下载不在本批。NASA照片仅作带处理说明的参考，不默认当运行贴图；正式资产原创。

## Lessons
- When 用户要求真实火星场景，do 先以一手地面照片/地质和设备工况确定视觉目标，再冻结素材/场景制作；不要把橙色滤镜、水平条纹或任意规则岩块当火星风格。

## 已执行记录
- 研究worker两份来源文件已交；主控实际查看4张参考并登记hash/处理。architect前审完成，要求全资产矩阵、处理标签、固定camera与uncached导入，均进入brief。
- Astra实际派单：mars_outcrop_astra，角色实现工人，gpt-6-astra/xhigh，fork none，独占source/environment/mars-outcrop-r1；2026-10-07已开始。
- 场景支撑worker：mars_scene_ground，独占lookdev/ground；root同时搭父目录看样。正式模型默认Blender。

## 局部失败与重新计划
- 场景初次sandbox启动因userdata权限/.NET路径失败；改为已授权原生启动并显式设置已有DOTNET_ROOT/PATH，未安装依赖。
- 反射来源枚举先后误写REFLECTED_SOURCE_SKY和REFLECTED_LIGHT_SOURCE_SKY，两次parse失败。停止猜测枚举，读取本机GodotSharp API与官方Environment文档；文档确认REFLECTION_SOURCE_SKY。先单独修正并跑parse检查，再集成网格/渲染；不把退出0当parse通过。

- 独立project同时包含可编辑blend，Godot headless因未配置editor Blender路径中止GLB导入；已依据官方ProjectSettings关闭该project自动blend转换，运行资源只用显式GLB。Blender源保留并独立重开，不装新工具或修改全局editor配置。

- 场景Godot native Forward+/Metal已实际渲染；首轮地面过亮/风纹过多，root按同镜头调色与局部纹理修正。框架键盘事件、画质/说明切换、拖动/复位自检PASS。支撑地表源主控独立重开，finite/meter/四锚点邻近顶点/导出字节一致PASS。
- Astra首图规则柱墙/纹理条带否决并返工；Blender pack_islands重复崩溃后asset局部architect审查、改已知岛矩形排版。当时最终源/同场图核验待修正；最终结果见末条记录。

- 收尾architect核对参考和Godot图，确认贴图无错版/资源接线正确；提出局部P1：厚床边过圆、背面缺薄片截断，与冻结brief矛盾。Astra第二次形体验收未过，保留031a6候选/hash与图，重新计划只补尖薄悬缘/剥落破面，不重开调研/框架。P2岩性误称“片岩”已改“片状砂岩碎屑”。

- 最终Astra局部薄片/斜楔返修：14138013...cacf，47,998tri。root无旧.godot/抽取PNG真实导入与5native图通过，贴图字节一致、LOD0固定原网格；主控source重开/原始打包PBR/GLB再导出及独立导入通过。切线仅1个xyz分量约1e-4舍入，其余JSON/数据严格相同，exact_binary=false。architect复核原P1关闭到首件看样候选，owner视觉仍NOT_RUN；质量候选非生产最终接受。

- 交付：源与证据提交 `d24736bc8b818105c67bb28e31ca49f1ebbbc5dd` 已推送；[Draft PR44](https://github.com/liuyejinghong/yudian-game/pull/44) 已创建并挂到本会话。独立看样与完整需求可直接审阅；所有者最终视觉/Main接入/整场性能继续NOT_RUN。
