# 产研 TODO

更新：2026-10-04；依据真实Issue #5–#8、PR #13与本轮复验。主控Codex。下表拟执行槽见[总入口](../../../TODO.md)；PR16/18/20已按所有者授权合并，本网格批次起点main=3999d30。T3a-r1与实际GLM数据子件已接受；本轮两个实际GLM纯网格子件已受限接受，父票仍需候选集成/更新验证。ACCEPTED的QA行仍只是原受限技术接受，不扩张为性能稳定/游戏模拟完成。

| ID | 优先级 | 状态 | 依赖/解除条件 | 拟执行槽 | 交付与接受条件 | 来源/证据 |
|---|---|---|---|---|---|---|
| QA-01 | P0 | ACCEPTED | QA-03/QA-04/QA-05本轮底座 | Codex（上批实际） | 旧原件复算、新三轮Metal/有效内存可获取；性能稳定性转PERF-01 | [复验](../reports/2026-10-04-takeover-verification.md) / [#15](https://github.com/liuyejinghong/yudian-game/issues/15) |
| QA-02 | P0 | ACCEPTED | 冻结输入合同 | GLM（上批实际） | 109合同用例+实际Godot异常矩阵；限定commit已集成并随PR16合并 | [交付票](QA-02.md) |
| QA-03 | P0 | ACCEPTED | QA-02 | Codex（上批实际） | 只读移包/不同cwd/默认输出/引擎中断/失败；GUI关窗另列LIFE-01 | [复验](../reports/2026-10-04-takeover-verification.md) |
| QA-04 | P0 | ACCEPTED | QA-03/QA-05 | Codex（上批实际） | 实际Forward+/Metal、Mobile/Metal、运行身份；尺寸明确estimated | [复验](../reports/2026-10-04-takeover-verification.md) |
| QA-05 | P0 | ACCEPTED | 既有工具、原始模板 | Codex（上批实际） | 干净检出构建/启动、模板重复hash；新系统验证另列ENV-01 | [构建证据](../evidence/2026-10-04-takeover/clean-build-manifest.json) |
| QA-06 | P0 | ACCEPTED | QA-02 / ART-I00 | Codex（上批实际） | 纯输入/统计组件与独立视觉接缝；场景仍是探针，不是核心模拟 | [复验](../reports/2026-10-04-takeover-verification.md) |
| PERF-01 | P0 | DRAFT | 冻结前后台/节奏采样合同；用指定PR16提交或其集成版本 | GLM-Eng-A工具与复算；Codex实机调度/裁决 | 原因分类所需同条件日志/帧/内存，保留启动尖刺；确认可重复性，不能直接沿用75–172FPS作稳定预算 | [#5](https://github.com/liuyejinghong/yudian-game/issues/5) / [#15](https://github.com/liuyejinghong/yudian-game/issues/15) |
| LIFE-01 | P0 | DRAFT | 现有Recorder；独立GUI关窗用例与恢复定义 | GLM-Eng-A用例/局部修正；Codex真实GUI验收 | 关窗exit/status、无覆盖、重启独立run-id；硬杀只允许部分CSV，不冒称游戏恢复 | [NOT_RUN](../reports/2026-10-04-takeover-verification.md) |
| ENV-01 | P2 | BLOCKED | 合法可用的无SDK隔离系统/设备；禁止为本票擅装系统 | GLM-Eng-A清单/脚本；Codex环境与裁决 | 同一包在无SDK环境启动，真实runtime/身份；未有环境不影响合同与独立样件 | [复验边界](../reports/2026-10-04-takeover-verification.md) |
| T3a | P1 | ACCEPTED | r1矩形数据接缝/边界/版本与提交取消职责已冻结；实际世界提交系统在T3d | Codex | 接受r1数据合同设计：绝对高度/row-major、不可变base与candidate、外围同值/版本重验和取消保留已发生结果；完整世界/邻区接入未实现，不直接开全套T3 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3a-DATA | P1 | ACCEPTED | T3a-r1冻结2a37f4b；GLM原提交d1f7cd6/修正1a0ec1c，主控独立验收 | GLM-Eng-A | 388工人检查+39主控独立检查通过，reviewer12项复核关闭两缺陷；net8/net10编译0警告0错误，net10执行；只接受数据子件，不批准世界提交/游戏保存 | [复验](../reports/2026-10-04-terrain-data-verification.md) / [合同r1](../contracts/terrain-patch-r1.md) / [GLM票](T3a-DATA.md) / [#19](https://github.com/liuyejinghong/yudian-game/issues/19) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3b | P1 | DRAFT | T3b-MESH接受后由主控冻结阶段候选接入/更新与实际Godot验收 | GLM-Eng-A | 局部阶段网格子件，边界连续、同输入输出；不把网格变化当持久世界已成立 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3c | P1 | DRAFT | T3c-MESH接受后由主控冻结高度场候选接入/更新与实际Godot验收 | GLM-Eng-B | 第二候选子件与边界/局部更新证据；不和T3b争写公共状态 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3b-MESH | P1 | ACCEPTED | T3a-DATA；网格合同r1、共享输出/金样/独占目录已冻结；base8c3e117；实际GLM交付/主控独立验收/reviewer复核已完成 | GLM-Eng-A | 66工人检查+89共用主控检查通过；固定对角线、候选先验current；只接受纯网格，不做阶段管理/更新 | [复验](../reports/2026-10-04-terrain-mesh-verification.md) / [#21](https://github.com/liuyejinghong/yudian-game/issues/21) / [执行票](T3b-MESH.md) / [合同](../contracts/terrain-mesh-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3c-MESH | P1 | ACCEPTED | T3a-DATA；网格合同r1、共享输出/金样/独占目录已冻结；base8c3e117；实际GLM交付/主控独立验收/reviewer复核已完成 | GLM-Eng-B | 82工人检查+89共用主控检查通过；2×2细分采样顶点、面内仍平面；不做高度场更新系统 | [复验](../reports/2026-10-04-terrain-mesh-verification.md) / [#22](https://github.com/liuyejinghong/yudian-game/issues/22) / [执行票](T3c-MESH.md) / [合同](../contracts/terrain-mesh-r1.md) / [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3d | P1 | BLOCKED | T3b/T3c提交；明确碰撞/通行/保存的最小范围与验收案例，派实现子票前冻结对应契约 | Codex合同/集成；GLM确定适配子件 | 几何/碰撞/导航/重载与取消同源；邻工程并发、无过期提交/重复收益，跨模块独立reviewer | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T3e | P1 | BLOCKED | PERF-01可用对照；T3d；ART-T01/T02仅为视觉对照需接口与样件 | GLM-Eng-B采集工具/复算；Codex实机/选择 | 两候选同条件更新范围/主线程峰值/导航耗时/保存增长/视觉成本对比；所有者签视觉，主控选技术 | [#6](https://github.com/liuyejinghong/yudian-game/issues/6) |
| T4a | P2 | DRAFT | 现役产品/共同场景与权限案例，明确本轮候选输入范围 | Codex | 冻结世界事实/行动/权限输入；将原T4大票拆成领域基线、校验器、候选适配与统计小票；真实完成与拦截分计，模型非权威 | [#7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| T4-HARNESS | P2 | BLOCKED | T4a；领域计划/错误分类/模拟提供者接口 | GLM-Eng-A / GLM-Eng-B分目录 | 纯harness/适配/测试优先外包；主控握权限与提交；不自动下载权重或开付费API | [#7](https://github.com/liuyejinghong/yudian-game/issues/7) |
| T5-SAMPLE | P2 | BLOCKED | PERF-01；支持路径预检；同资源/镜头/输出/回退合同 | GLM-Eng-A采样与统计；Codex后端/画质裁决 | 原生/已支持超分模式真实数据与回退；不可用标缺证，不伪造集成，所有者判画质 | [#8](https://github.com/liuyejinghong/yudian-game/issues/8) |

## 进入下一执行批次时

先按实际授权范围冻结T3b/T3c候选子票，或PERF-01/LIFE-01中的一项，不用“整个T3”派单。合同/高风险状态由主控定，后续实现拆成能独测的GLM票；只把需要实机的部分串行交主控，统计/测试/清单让GLM完成。尚无文件归属的DRAFT/BLOCKED票不是可转发完整Spec。

T4与T5沿用已存在Issue范围，不重新创建宏观路线；目标委托、机器人保障和生产存档仍未完成，本轮也不排成假装已经READY的大包。低配/Windows/发行是后续范围，当前不因缺设备宣布最低配置，也不把新系统留项扩成购买或部署任务。
