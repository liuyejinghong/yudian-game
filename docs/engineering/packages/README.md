# 产研模块包与并行领取

代码按功能模块组织，任务包按独立验收边界组织。一个模块可分多个包；有依赖的包分波次，同一波可并行实现、集中验收。GLM负责冻结包的执行，Codex握公共接口、关键状态、review与集成。

| 模块 | 当前已接受 | 当前执行/可并行包 | 后续依赖 |
|---|---|---|---|
| 地形数据 | immutable Snapshot/Patch、严格codec | 已完成，不重做 | 单区域内存提交已接受，生产存档未实现 |
| 地形CPU几何 | Stage、Heightfield两个独立生成器 | 已完成，曾两工作树并行 | 阶段/局部更新未实现 |
| 引擎地形显示 | ArrayMesh、独立候选探针 | 已完成 | Main单区域显示已接受，完整建设/存档未实现 |
| 静态地形碰撞 | 静态adapter/候选分帧/M01接线受限接受 | **T3-COLLISION本批已完成复验**；不得重复派单 | 机器人通行/导航/save不在本票 |
| 性能证据工具 | 单轮复算/原始三轮证据 | **PERF-01-COMPARE ACCEPTED，限定离线比较器** | 实机受控采样/原因裁决主控负责 |
| 生命周期证据工具 | Recorder/中断原始证据 | **LIFE-01-AUDIT ACCEPTED，限定离线审计器** | 主控已限定接受固定包GUI关窗/重开；不签存档恢复 |
| 美术接口 | I00校准、M01材质技术接受 | 美术组在做PREVIEW/正式首样；工程soil_mars联调 | U01、T01/T02正式素材缺口见ART-LINK |
| 世界提交 | 单区域内存提交限定接受 | T3d-COMMIT-POLICY与Codex T3d-MEM已验收；不重复领取 | Main显示/静态物理同步与单机器人整平另票已接受；完整导航/经济/保存未实现 |
| 导航/存档、目标委托 | 尚未实现 | DRAFT/BLOCKED，不给GLM自行决定架构 | 先冻结权威状态、输入输出、失败/重载金样 |

## 当前并行波次

两包已独立review、返工与验收完成，均限定ACCEPTED；[PERF PR28](https://github.com/liuyejinghong/yudian-game/pull/28)与[LIFE PR29](https://github.com/liuyejinghong/yudian-game/pull/29)已分别合并。主控直接协调GLM，原冻结实现不再重复领取；各自worktree从同一固定SHA建立。固定基线：`1ba4aa64573bc484578c03340e304787062dd127`（已包含两包与原始证据）。不要用随时变动的main取代固定输入。每个工作流只分配一包，文件归属见各包；内部子工人也不得越过目录。共享TODO/合同/PLAN由Codex维护，GLM不得争写。

Codex默认通过Agent Bridge承担派单、返工和交回协调，所有者无需转述。GLM可用原生子代理；每包内每个文件仍只安排一个写者，最终验收/共享TODO/合并由Codex负责。

开新ZCode会话先执行`/model GLM-5.3-Flash`并确认原生回执，再发送任务。用户手动打开的会话不在Agent Bridge任务账中，不伪造dispatch_id/费用；交付以真实git diff和日志验收。不要把正在进行的T3-COLLISION或美术组已领取票再次派发。

### PERF工作流prompt

你是《余电》外包执行者。当前用户已授权本包实现，请进入自己的独立worktree（基线使用主控给定冻结SHA），阅读AGENTS.md与docs/engineering/packages/PERF-01-COMPARE.md，按该冻结包完成。唯一可新增目录tools/perf_compare/；其他文件、公共文档、Recorder、Godot、美术都禁止修改。不装依赖、不联网、不启动GUI/引擎、不push/merge；使用Python标准库，复用既有measure_baseline.recompute。先核三轮真实证据，再实现CLI/独立金样/负例并运行完整测试，保留失败日志；同一步两次失败停止诊断而非改expected。完成限定干净commit，提供5行：范围、实际验证命令/日志、限制、commit SHA、用量未知如实。不要接受自己为最终验收，不自动领取下一包。主控后续review、独立复验、更新TODO并合并。

### LIFE工作流prompt

你是《余电》外包执行者。当前用户已授权本包实现，请进入自己的独立worktree（基线使用主控给定冻结SHA），阅读AGENTS.md与docs/engineering/packages/LIFE-01-AUDIT.md，按该冻结包完成。唯一可新增目录tools/lifecycle_audit/；其他文件、公共文档、Recorder、Godot、美术都禁止修改。不装依赖、不联网、不启动GUI/引擎/杀进程、不push/merge；使用Python标准库，复用既有measure_baseline.recompute。先读取lifecycle-r2真实证据，再实现manifest/CLI/独立金样/反例与测试；产物缺证不能声称真实GUI关闭或游戏恢复完成。保留失败日志；同一步两次失败停止诊断。完成限定干净commit，提供5行：范围、实际验证命令/日志、限制、commit SHA、用量未知如实。主控负责最终review与集成，不自动领取下一包。

## 交回与集成

每包交工作树绝对路径、branch与commit SHA、验证命令/完整日志及未完成项。用户开始某包时通知Codex包ID/工作树即可登记IN_PROGRESS；没有回执仍保持READY，不冒称已领取。主控检查diff归属与依赖，必要独立reviewer，只对实际变更做相称复验；存在问题退回同包，不阻塞无依赖的合格包。合格后按既有授权直接合并，TODO更新为限定ACCEPTED；父票剩余实机/产品职责保持未完成。

2026-10-04下一波：[T3d-COMMIT-POLICY](T3d-COMMIT-POLICY.md)已由GLM交回并限定接受；主控World/28例独立验收通过。主控按[r1用例](../contracts/runtime-controlled-r1.md)自行启动测试；LIFE限定接受，PERF条件返工见[实机复验](../reports/2026-10-05-runtime-verification.md)。它们不重复领取已ACCEPTED工具包。

T3d内存子件已合并[PR30](https://github.com/liuyejinghong/yudian-game/pull/30)，完整T3d与PERF受控/原因/稳定预算继续未完成；LIFE实机范围已限定接受。[验收边界](../reports/2026-10-04-terrain-commit-verification.md)。


2026-10-05后续波次：T3d-PROJECTION-RESOURCES / T3d-VIEW已限定接受，[合同](../contracts/terrain-view-r1.md) / [复验](../reports/2026-10-05-terrain-view-verification.md)。GLM资源束冻结905458c，执行task_8b2f8b3248→一次返工task_b772ddba6e；主控握Evaluate/Commit与实际场景、GUI与故障恢复，工人不得重复领取。独立单区域已同步，旧Main机器人/导航/save/建设闭环仍须新票冻结；PERF与正式美术留项按权威TODO跟踪。

本波已合并[PR32](https://github.com/liuyejinghong/yudian-game/pull/32)，merge `8b693447e4e466f1baf0ad2deec710e642c28895`；冻结资源束会话已结束，旧工作树和失败证据保留。

2026-10-05 MAIN-GROUND波次：冻结89cb3fb，[合同](../contracts/main-ground-r1.md) / [复验](../reports/2026-10-05-main-ground-verification.md)。参数与运动两GLM包不同工作树并行；原生运动经两次review返工补真实下坡、空中暂停与正确射线oracle，主控最终21参数/10运动/七步Main与Metal GUI通过。三子票限定ACCEPTED，不重复派发；下一票须新冻结建设或导航范围，保存/正式美术/性能留项未扩大接受。

MAIN-GROUND本波[PR33](https://github.com/liuyejinghong/yudian-game/pull/33)已MERGED，精确head8e1ddde、mergef041e3e；main干净同步与开发程序集重建/窄烟测通过。两个GLM会话已结束，保留worker分支/工作树与原日志。

2026-10-05 LEVEL-JOB波次：[合同](../contracts/level-job-r1.md) / [复验](../reports/2026-10-05-level-job-verification.md)。两GLM包GROUND-ORDER/WORK-METER同4cbdf3c独立工作树并行；单目标native7与旧10、纯计时36、主控Main15与Metal实际取消/完成通过，三子票限定接受，不重复领取。下一票新冻结导航/资源边界，父T3d继续未完成。

LEVEL-JOB交回已集成[PR34](https://github.com/liuyejinghong/yudian-game/pull/34)：head b79860a、merge 8bcefb1，主工作树同步/重编译/15步窄烟测通过；两GLM会话已结束，原树与证据保留。
