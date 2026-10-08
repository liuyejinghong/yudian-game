# 余电 · Yudian

2026-10-08：D1.2源码已接入有限采矿、四配方、真实运输、补维修耗材与两个发展方向，r3原生25进程与真实鼠标功能链LIMITED PASS；实窗发现净缺口/HUD需要返修，普通试玩入口与保存退出正在补齐，默认入口继续使用D1.1 r5。[D1.2复验与留项](docs/engineering/reports/2026-10-08-d12-production-verification.md)。

2026-10-07：本机「余电.app」已切换到通过真实鼠标保存/关闭/重开验收的D1.1自举建设包；真实运料、施工、付费供电与充电维修已接通。[试玩操作](prototype/README.md) / [D1.1证据与范围](docs/engineering/reports/2026-10-07-d11-bootstrap-verification.md)。

镜头鼠标入口已验，默认应用已更新为r5；底栏提供平移、缩放、旋转和复位，暂停及保存重开后仍可操作。物理触控板另验。[镜头复验与留项](docs/engineering/reports/2026-10-07-camera-verification.md)。

**目标委托式的单人 3D 火星基地经营与生存游戏。**

> 我在荒地上建立自己的基地。机器人理解目标，在授权内补齐前置并开展工作；设施运转，地表留下开发痕迹。我不仅决定建什么，也要让有限电力、会磨损的工程队持续运转。

## 当前状态

任务跟踪从[双线 TODO](TODO.md)进入：产研与美术分别维护任务、依赖、拟执行agent及验收证据。当前按所有者授权逐批推进冻结子票，美术在独立线制作；默认优先GLM执行实现，Codex负责合同、审查、实机验收与集成。

当前项目协调目录的 **余电.app** 是D1.1本地试玩包：从着陆器和有限套件开始，实际运料建设、付费接线、充电维修与保存重开已接通；旧D1.0包及存档保留，资源采集/加工经营仍待D1.2。[启动与工程说明](prototype/README.md)。

已有 `prototype/` Godot/C# 原型，具名 S 测试档包含六类设施与十二台机器人；正式新档只预建着陆器。显式`--live-terrain`已限定接受主场景权威地形改造、碰撞同步与原生贴地巡逻，显式benchmark沿用灰模负载；[历史复验](docs/engineering/reports/2026-10-05-main-ground-verification.md)。默认玩家应用已接有限导航、建设、基础供电保障与schema2保存；完整目标委托、真实生产、救援与经营闭环仍待后续。macOS Apple Silicon 原生首发，Windows 后续；最低配置和联合负载仍需项目实测。

2026-10-04 接手整改保留旧灰模；输入校验、可写输出、真实运行身份、导出复现与独立 ART-I00 校准的证据见 [本轮复验](docs/engineering/reports/2026-10-04-takeover-verification.md)。准确构建/启动入口见 [原型运行说明](prototype/README.md)。旧 T1/T2 报告原样保留，成绩不能外推完整游戏。

公开仓库不改变可见性或许可证，不提交密钥、玩家存档、模型权重或第三方游戏资源；合格小票按所有者已有授权审查后合并；商业发行不在当前授权内。

## 阅读入口

| 文档 | 作用 |
|---|---|
| [产品定义](docs/product/product-definition.md) | 已对齐的目标委托、任务看板、权限、平台与首个可玩版本 |
| [返航、停机与救援补充 v0.4](docs/product/energy-return-and-rescue.md) | 最新明确规则：零电停机、约 10% 常态回充、动态返航预算、零耐久回收维修 |
| [地表与机群保障](docs/product/world-and-fleet-systems.md) | 推平、开挖、充电、维修和基地存续 |
| [美术方向](docs/art/art-direction.md) | Surviving Mars: Relaunched 与 Anno 2205 主参考，真实三维与持久地表变化 |
| [决定登记](docs/decisions/decision-register.md) | 已确认、建议、待验证及被替代提案 |
| [技术验证计划](docs/engineering/technical-validation-plan.md) | 引擎、地形、模拟、本地 AI 与较低配置联合验证 |
| [游戏模块与开发里程碑](docs/engineering/module-roadmap.md) | UI、探索、资源、建设等九模块职责与可玩版本集成，验证成果进入游戏建设 |
| [双线独立审查与可执行路线](docs/roadmap/README.md) | 产研／美术详细检查点、34个规划工作包、逐项验收及Agent交接；提案不等于派工或实现 |
| [超分辨率与平台加速调研](docs/engineering/upscaling-and-renderer-evaluation.md) | MetalFX、FSR、DLSS、XeSS 的已查证能力、接入边界与验收 |
| [双线验证](docs/workflow/parallel-validation.md) | 美术与技术并行的工作范围 |
| [Codex 交接](docs/workflow/codex-handoff.md) | 从当前仓库接手，不再创建同名仓库、不自动实施全路线 |
| [与旧 MUD 的关系](docs/reference/mud-relationship.md) | 旧项目仅为参考，非代码／服务／指令依赖 |
| [历史原文](archive/README.md) | 旧稿用于追溯，不作为当前实施授权 |

## 产品基线与最新补充的关系

产品定义保留已核对的 v0.3 完整正文；v0.4 的新增规则以返航与救援补充和决定登记为准。历史文件不重写，不把旧稿中“尚未确认零电／救援”继续解释成当前未知。

零电与零耐久都停止自身移动和工作，但原因与恢复条件不同。正常情况下约 10% 电量开始自行回充；10% 不是任何距离、载荷和天气下都能安全返回的保证。动态预算和有限决策提供者用于更早调整，不负责凭空恢复能源。

首阶段不强制新手受损，但资源管理失误、设备损耗及恶劣环境可以产生真实停机与恢复任务。降低图形设置不能删除这些玩法。

## 旧项目只作参考

旧 MUD：<https://github.com/liuyejinghong/ai-mud>。

本项目不是它的换皮或下一部署分支。不复制旧 `.git`、数据库、账号服务，不要求玩家连接旧 VPS，不继承浏览器／Phaser／32px 像素／旧 M1–M4 排期。可引用旧设定、产品教训与规则测试反例，但不把历史通过记录当作新项目证据。此次不修改旧仓库或其 PR。

## 技术态度

Godot 4 .NET＋C# 仍为首选验证候选。MetalFX 等超分能力纳入引擎选择验证，不预先承诺所有厂商功能；图像超分和生成帧数不能掩盖模拟、导航、规划和存档瓶颈。参考机为 M3 Pro 满血／36GB，不是最低配置。
