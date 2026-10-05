# 双线执行证据 · 2026-10-05

本页为上一批记录；HF当前源已更新revision3，[后续形体切片](../dual-r2/README.md)覆盖当前版本，以下r2计数/图保持历史身份。

计划和任务票已落库，两线已有真实交件：主控制作U01 Blender静态几何试片，真实GLM制作P01货箱/F03仓储，主控独立返修与实机验收。三子票REVIEW，完整高保真、最终视觉与游戏接入尚未完成。

## 当前版本

| 项 | 可核验结果 | 接受边界 |
|---|---|---|
| Blender工具 | [tool.json](tool.json)，官方4.5.14包SHA匹配，build62c1db4208e8 | 工具可运行；完整高保真流水线未通过 |
| U01几何revision2 | .blend/构建/重开脚本/GLB；[真实网格](hf-r2-audit.json)，16504tri/28mesh/51surface/4socket；[重开](reopen-r2.json) | 结构/索引相同，浮点差≤1e-6，字节不同；静态，无完整动作 |
| U01当前实机 | [16张旧新对照图](images-hf-r2-crate/captures.json)中的u01条目，Metal Forward+ | normal/close、yaw0/90、idle、空/有示意；该目录4张crate是历史旧版 |
| P01主控共面修订 | [实际引擎网格](crate-r2-audit.json)，48tri/1mesh/4surface/3角色，.64×.70×.38/底Y0；[当前4图](images-crate-storage-r3/captures.json)中的crate条目 | 静态货箱，不包括回收件或库存接入 |
| F03主控修订r3 | [实际网格](storage-r3-audit.json)，756tri/16mesh/52surface/12箱实际世界落位，4.5×3.2×2.2m；[当前8图](images-crate-storage-r3/captures.json)中的storage条目 | 空/有在normal0/90可区别；仅局部遮护货架，不宣称整仓防尘密封 |
| 当前LF核验 | [可运行检查](verify_storage.py)、[实际hash/位置/角色/图](storage-r3-verification.json) | 核两件当前源身份；未覆盖完整运行状态与玩法 |

HF当前GLB/hash追踪仍见[verification.json](verification.json)，其中crate字段对应最初GLM版，当前货箱以storage-r3-verification为准。所有图均为Godot实际PNG，未使用概念图或离线渲染代替。旧三件/M01/公共预览、根PLAN/TODO、Main/project/fixture与产研合同未修改。main7f6667e于本轮只读确认且干净。

## 制作者与真实派单

- U01 Blender脚本、源、接线、截图和验收：Codex主控。
- P01：ZCode原生/model确认GLM-5.3-Flash；[交付](P01-result.json)，task_94dc7ef663 / 原561d896→集成9aa0b2b；Bridge599秒。
- F03：同会话[原交付](F03-result.json)，task_c11baae139 / 原eb024eb→集成0e67e8e；Bridge493秒；[窄板返修](F03-r2-result.json)，task_083dce1c82 / 原e14a907→集成b9d8489；Bridge117秒，源报告称r1.1，本证据称r2。
- 主控随后修两件箱身/顶盖共面：body中心Y.23/高.18，使body顶.32低于盖顶.38；仓储箱位/包络和现役U01不改。原GLM任务结果不覆盖主控改动。
- usage.used是Bridge工具计数，不等于计费；reported model在Bridge为null，原生确认单独记录。files_changed基于协调cwd，混入主控并行修改；作者归属以工人提交和独立工作树diff确认。会话已结束，无活动任务。

## 看样与保留的失败版本

HF近景可见盖板/紧固件/提拉支架；normal微细节收益有限。全轮倒角65967tri代价不合适，当前仅细化可见外壳至16504tri；未测FPS收益。首版[网格](hf-audit.json)与[16原图](images-u01/captures.json)保留，不混入当前计数。

F03首版[实际网格](storage-audit.json)及[8图](images-storage/captures.json)显示顶/后板遮挡，空有几乎一致，视觉REWORK。窄板版[网格](storage-r2-audit.json)/[8图](images-storage-r2/captures.json)解除遮挡，但箱顶共面产生三角明暗伪影。主控缩短箱身后，同相机当前12图确认货物可辨且箱顶伪影消失；配色/精细质量/完整所有者验收未代签。当前低保真仍保留基础盒体造型。

下一关：U01主要形体层次→私有材质/UV/必要烘焙→完整动作/净空→实机成本与质量看样；低保真先冻结F04泊位/接口，再排U02/F05。两线独立。

NOT_RUN：完整高保真UV/烘焙/动画/LOD、设施完整STATE与正式socket、所有者最终接受、游戏库存/补能/维修/保存接入、整场性能、同任务Blender与纯Python速度对照。两个生成器当前各6个异常检查已实际运行；主控导入/拍图成功不代签上述项目。

日志仅替换本机私人路径，数值/错误/退出结果未改；原件保留在仓库外，原始SHA记[路径处理清单](private-path-redactions.json)。初次沙箱GPU初始化/.NET环境失败保留，修配置后日志另记。

architect收口独立核源副本/12图hash/12箱世界落位/材质与指定normal/close图：无新增阻塞，局部可读性改善成立；正常距离不要求逐箱数清全部12位。主控仍保留三子票REVIEW与最终验收NOT_RUN。
