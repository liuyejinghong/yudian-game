# 火星高保真 Blender Agent Skills 调研

核验日期：2026-10-07。目标是改善现有火星砂岩露头与环境物的写实细节：目前大平直面明显，条纹/噪声式细节不足；高频纹理不能替代多尺度轮廓、侵蚀断面与薄层结构。项目本地要求以真实 Gale/Stimson 风积砂岩照片为形态参考、模型原创，并以 Blender→GLB 交付，质量判断需看近景和正常游戏视距（[MARS-BRIEF](../mars-research-r1/MARS-BRIEF.md)、[ASTRA-OUTCROP](../mars-research-r1/ASTRA-OUTCROP.md)）。

当前执行：已按用户授权升级至Blender **5.2.2 LTS**，固定安装Scenario五项技能并实测原生兼容性；详见文末执行更新和[计划](PLAN.md)。下面保留升级前基于4.5的调研建议，不作为当前制作版本。

## 升级前调研结论

建议只把 **MartinRapcan 的 Blender skill** 作为 Blender 4.5 的通用脚本实践参考；针对高模到低模的 UV 与 bake，可阅读 **Scenario 的 UV/Baking skill** 作为技术流程候选，但先按 4.5 官方手册逐项核对，不直接照抄其 5.2 脚本或 API。若需要可选的本地资产检查/烘焙命令行，再评估 **kajisho5/blender-skill**；它是资产流水线工具，不是高保真建模方法，也没有证据证明其支持独立高模投射到低模。

MCP 是代理调用 Blender 的操作接口（例如执行代码、读场景、截图）；MCP 本身不教如何塑造高保真岩石。技能文档提供工作顺序和判断标准；其中有的另附可执行脚本。切勿把接口功能、文本教程、自动化脚本或社区热度当作模型品质保证。

## 候选筛选

| 用途 / 建议 | 原始仓库固定版本与准确技能路径 | 许可证、适用性与实证边界 |
|---|---|---|
| **优先阅读：通用 Blender/bpy 生产实践** | [MartinRapcan/blender-claude-skill @ `964cfe73bb1d15ff5b1603c625ecf067fb8d11bc`](https://github.com/MartinRapcan/blender-claude-skill/tree/964cfe73bb1d15ff5b1603c625ecf067fb8d11bc)；[`blender/SKILL.md`](https://github.com/MartinRapcan/blender-claude-skill/blob/964cfe73bb1d15ff5b1603c625ecf067fb8d11bc/blender/SKILL.md)，搭配 `blender/references/{geometry,materials,rendering,gotchas}.md`。 | MIT。原 README 明确称测试于 Blender 4.5 LTS 与 5.x，内容涵盖 bpy/bmesh、几何、PBR/UV、光照、渲染与版本发现；形式是纯 Markdown 加示例脚本，无新增依赖。当前只有 2 GitHub stars、0 forks、2 commits；技能页未检索到可核实的安装统计。与本机版本最接近，但低使用量、单维护者和缺少独立高模→低模 bake 配方意味着应当把它当提示，不视作经过生产验证的管线。 |
| **参考候选：UV 与高模细节烘焙** | [scenario-labs/skills @ `91caa011e13774a220aba7136309b49964104040`](https://github.com/scenario-labs/skills/tree/91caa011e13774a220aba7136309b49964104040)；[`scenario-blender-uv-baking/SKILL.md`](https://github.com/scenario-labs/skills/blob/91caa011e13774a220aba7136309b49964104040/skills/dcc/blender/scenario-blender-uv-baking/SKILL.md)，需配套 [`scenario-blender-expert/SKILL.md`](https://github.com/scenario-labs/skills/blob/91caa011e13774a220aba7136309b49964104040/skills/dcc/blender/scenario-blender-expert/SKILL.md)；材质另有 `skills/dcc/blender/scenario-blender-texturing-shading/SKILL.md`。 | 仓库根目录 MIT（以该 SHA 的 `LICENSE` 为准）。技能文档有测量 UV 拉伸/texel density、padding、投射距离及 bake 诊断的具体流程；仓库介绍称测试通过 Blender 5.2.1 headless，expert 文档也要求 5.2 API delta。skills.sh 当日页面显示 UV baking 112 installs、retopology 113、expert 139；仓库总计约 20.8K installs、882 stars（全仓库统计，不是这几个 Blender 技能的用户数）。这是新近发布/低单项安装量信号，不是低质量证明；但 4.5.14 兼容性尚未核实，所以只建议当参考资料，迁移前用 4.5 手册核验 API 与脚本。 |
| **可选工具：资产检查、渲染、简易 bake** | [kajisho5/blender-skill @ `39547d9e118451891c3ff68e087e20c5bde1599c`](https://github.com/kajisho5/blender-skill/tree/39547d9e118451891c3ff68e087e20c5bde1599c)；[`SKILL.md`](https://github.com/kajisho5/blender-skill/blob/39547d9e118451891c3ff68e087e20c5bde1599c/SKILL.md)，代码在同级 `scripts/`、说明在 `references/`。 | MIT。该版自述 Blender 4.2+、stdlib-only、无需云/API，支持检查、渲染、UV 可视化、优化和 AO/normal/roughness/diffuse/combined bake；明确说不代替交互建模/MCP，也不负责替调用者决定细节。GitHub API 当日显示 0 stars、0 forks；skills.sh 未检索到独立页面/安装数。其 `bake.py` 的可见接口是材质/资产 bake，没有证明支持 Selected-to-Active 高模向独立低模投射。只在确有这些辅助检查需求时进一步审代码；本次没有运行或安装。 |

### MCP 操作接口不等于建模工作流

Scenario expert skill 说明两种执行通道：MCP bridge 操作当前 GUI Blender，以及 headless Blender 脚本；技能另行规定阶段审查、测量与截图复核。`blender-mcp` 一类技能主要解释 `get_scene_info`、`execute_python`、截图等工具如何调用，适合已有 bridge 的交互操作，却不能单凭接通 MCP 解决形体层次、UV、PBR 或烘焙问题。Martin 的脚本 skill 不要求 MCP。对本项目，优先复用已有 Blender 与 Godot；不要因读到 MCP 技能就引入另一套 server 或模型服务。

## Blender 4.5 官方方法：高模 → 受控低模 → bake

以下方法由 [Blender 4.5 LTS Render Baking 手册](https://docs.blender.org/manual/en/4.5/render/cycles/baking.html) 与 [Multiresolution 手册](https://docs.blender.org/manual/en/4.5/modeling/modifiers/generate/multiresolution.html) 支持，手册要求 Cycles bake、目标网格 UV、活动 Image Texture 节点/图像作为写入目标。`Selected to Active` 将选中的来源表面投射到活动目标；低模作 active target，射线从低模向高模内部发射。根据穿透/误投情况调 Max Ray Distance；使用 cage 时设置 Cage/Cage Object，并注意 cage 与低模需有相同拓扑、面数和面序。手册还警告每个来源物体有 CPU 固定内存开销，必要时可先合并高模来源对象。

如果高低细节来自同一底网格的雕刻层级，可用 Multires 的 **Bake from Multires**：Viewport level 作为低分辨率端、Render level 作为高细节端；官方提示 Viewport Level 需为 0 才能把原始底网格作为 target，并先做好 UV、选中目标图像。独立构造的高低网格则使用 Selected to Active。

适配当前火星岩体时，先在近景参考约束下形成大轮廓、倾斜/不等厚层床、裂隙截断、侵蚀台阶、破面与岩脚碎片；避免把等间距槽线或均匀噪声当作细节。以较简洁但保留主要断面与剪影的低模承载实际形状，再以高模雕刻/几何记录颗粒、微裂隙和风蚀表面，投射切线空间法线；Base Color、Roughness 等材质图按需要另 bake。UV seam/硬边、图像分辨率、边缘 padding、投射距离和正反面漏射均要实测；官方明确 margin 可减少 UV seam 在过滤和 mip-mapping 下的不连续。烘焙后看模型渲染与近景，不以“脚本成功”或贴图存在代替视觉验收。

法线图空间应与材质 Normal Map 节点设置一致；目标主要用于 Godot/GLB 时，应在 Godot 实际导入查看 normal、roughness、色彩空间与 seams。项目的自然岩石要求非金属、弱暖灰褐/浅灰褐；噪声强度须在固定光照和正常 RTS 距离下不闪烁，不能用统一橙染或重复横线做假层理（项目本地需求，非 Blender 技术保证）。官方 bake 手册支持工作流，不会决定正确岩相、拓扑质量或贴图审美。

## 可用性核验与不确定性

- GitHub 原始文件按不可变 commit SHA 查看，确认 SKILL.md 与许可证路径存在；Scenario 技能自身要求配套 expert/领域技能，不宜只抽走一份文档后假设依赖都齐全。skills.sh 数字是网页抓取时的展示统计，变化快，且不等于活跃用户/成功案例；不要把短期高 star、安装数当成质量测试。
- 本轮只读源文件与元数据，没有安装技能、下载资产、运行其脚本或调用付费 API。因此第三方代码安全、Blender 4.5 执行通过率、输出模型品质均未验证。GitHub 最新提交 API 返回的签名状态：Scenario `verified=true`；kajisho `true`；Martin `false/unsigned`。签名验证表示提交签名状态，不代表代码审计或模型质量。
- 交互 shell 中 `blender`、`godot` 均不在 PATH；项目任务说明称本机已有 Blender 4.5.14 LTS / Godot 4.7.2，但其可执行文件路径尚未在本次环境核实。故本文只确认文本和官方流程可读，未确认本机 CLI 能直接运行这些技能。
- 官方 Blender 4.5 文档确认常规 Selected-to-Active、cage 与 Multires bake 流程；没有为这三套候选做基准测试，也没有证据证明任一 skill 能自动把当前“大平直面”变成达到成品要求的砂岩。最终形体和材质仍需按本地 NASA/JPL 图片参考评审。

## 五行交接

1. **所选技能：** Martin bpy skill 优先作为 4.5 通用参考；Scenario UV/Baking 只作为待适配的工作流资料；Kajisho 可选做资产检查/简易 bake。
2. **原始来源：** 三项均锁定 GitHub SHA 与 SKILL.md 路径，许可证均为 MIT；Blender bake 方法引自 4.5 LTS 官方手册。
3. **可用性核验：** 原始文本、许可证和版本说明已读；没安装或运行第三方代码。shell 的 blender/godot CLI 当前未在 PATH。
4. **风险/缺口：** Scenario 针对 Blender 5.2.1，Martin/Kajisho 使用统计很低或不可查；无技能能保证高保真成品，Kajisho 没证实独立高低模投射。
5. **文件与下一步：** 本报告为 `docs/art/production/mars-hifi-r2/SKILL-RESEARCH.md`。如进入制作，先核对 Blender 实际 CLI 路径与 4.5 API，再以本地形体验收要求执行高模→受控低模→烘焙并在 Godot 检视。

## 主控执行更新（用户后续授权升级）

上文为研究阶段记录，随后用户明确要求升级。已核对[官方当前下载](https://www.blender.org/download/)与[5.2LTS版本页](https://www.blender.org/releases/5-2/)，实际安装Apple Silicon **5.2.2 LTS**。官方SHA-256与macOS应用签名通过，原生旧源只读打开/高模到低模normal bake/保存重开/GLB回读通过，见[evidence/blender52-smoke.json](evidence/blender52-smoke.json)。4.5.14保留回查，当前制作采用5.2.2，不再把4.5兼容当阻碍。

主控选择并固定安装Scenario的expert、sculpting、retopology、uv-baking、texturing-shading至工作树`.agents/skills/`；版本为上述91caa011…固定SHA，根MIT许可证随安装保留。选择原因是分阶段形体检查、真高低模投射和分区材质；角色解剖/stylized默认不用于自然砂岩。实际具体工具函数是否通过与高保真质量还须制作/独立视觉检查，native小测试仅证明Blender基础能力，未宣称整套第三方脚本测试或性能收益。
