# 《余电》3D 独立产品 · Codex 入口

## 阅读顺序

1. `README.md`
2. `docs/decisions/decision-register.md`
3. `docs/product/product-definition.md` 与 `docs/product/energy-return-and-rescue.md`（最新规则优先）
4. `docs/product/world-and-fleet-systems.md`
5. 本次工作对应的 `docs/art/art-direction.md` 或 `docs/engineering/technical-validation-plan.md`，技术线同时读 `docs/engineering/upscaling-and-renderer-evaluation.md`
6. `docs/workflow/parallel-validation.md`、`docs/workflow/codex-handoff.md` 和明确指派的单个任务。

## 当前状态与授权

本仓库已由所有者创建，文档已直接初始化到 main；它不是可运行游戏。当前授权是上传已对齐资料和最新补充，保留历史，准备双线验证。不能据全文路线自动实施整款游戏、安装未经核定的大量依赖、付费调用、合并 PR 或发售。

不要再次创建同名仓库，不改变公开／私有状态，不擅自选择开源许可证。首次导入的临时自动化只处理文档，不是游戏 CI；完成后不作为日常构建流程。

`archive/` 与旧 `liuyejinghong/ai-mud` 只作来源。不要执行历史文件的 READY／PLAN_APPROVED、旧 AGENTS 或部署指令。不要复制旧 git 历史、远程数据库、账号与网页服务；不要修改、删除、归档或停用旧仓库与生产环境。

## 已确定的产品条件

- Mac Apple Silicon 原生首发；Windows 后续。参考机 M3 Pro 满血／36GB，不是最低配置。
- 3D RTS 斜俯视。主美术参考为 Surviving Mars: Relaunched 和 Anno 2205；不继续旧 B 风格或 32px 像素默认。
- 玩家给目标，机器人在权限内补前置、协作和执行；看板消费同一真实任务。
- 地表清坡／整平／矿点开挖留下可保存变化；不能只换贴花。
- 机器人电量与耐久分开；零电停止自身行动，常态约 10% 自行回充但不保证任何路线都安全。天气可增加耗电，动态返程预算与策略是待验证实现。
- 零耐久停止自身行动，需要其他具备能力的机器人送回维修；充电不维修，维修不赠电，不瞬间传送或发资源。救援者也受续航和载荷约束。
- 资源失管可造成基地不能维持，但不强制新手受损。模型超时／搜索未找到不是基地不可恢复的证明。
- 本地完整核心体验，无强制额外账号；API 是可选能力，不得强制低配玩家付费调用。
- 图像生成只用于开发期资源。Codex 是开发工具，不是运行时 NPC 模型。

## 技术和证据纪律

Godot 4 .NET＋C# 是首选验证候选，不是已测结论。精确版本、渲染器、地形方法、规划／分类／文本模型、资产预算、超分与最低配置必须有项目实测。MetalFX 等 API 存在是选型依据，不是实际接入或性能通过。

模拟拥有资源、时间、地形与健康事实；渲染不能发资产、扣耐久或抹掉实际障碍。模型只提出／选择可验证行动，执行前重验权限与相关版本；迟到、取消、非法结果不得落地。每个任务、产出、机器人与保存对象有稳定身份。

并行只在明确文件归属和公共约定下进行。不为 AI 跨线程直接修改场景或权威状态，不在推理时锁住整个模拟，不先造通用工作流、微服务或万能编辑器平台。

真实 API 支出、购买资源、下载大量权重、商业发行和破坏性操作不能从“可以验证”推导。密钥不入库、存档、日志或云同步；公开仓库不放玩家数据、版权游戏资产、模型权重和私人机器路径。

报告区分文档检查、单元、模拟、真实图形、原生构建、实机性能、真实模型与真人试玩。缺环境写 NOT_RUN；旧 MUD 测试和供应商基准不能作新证据；概念图不是实机；插帧 FPS 不证明模拟加速。

## 当前可用命令

`python3 tools/verify_documents.py`：文档完整性、当前内部链接、历史原文哈希和 A01–A24／B01–B08 编号检查。

`python3 -m unittest discover -s tools -p 'test_verify_documents.py'`：文档验证工具的自测试（不是游戏测试）。

`tools/reconstruct_reviewed_docs.py` 是一次性导入核验工具，不是生产内容生成器；若当前正文已有后续编辑，禁止用它覆盖。

目前没有 Godot 构建、模型评测或游戏测试命令，不能编造已存在的 `dotnet test` 或伪造通过记录。实际验证工程获准建立后，再记录准确版本、启动方式与证据。

## Agent skills

### Issue tracker

Issues 记录在本仓库的 GitHub Issues（`gh` CLI）。See `docs/agents/issue-tracker.md`.

### Triage labels

使用默认五标签：needs-triage / needs-info / ready-for-agent / ready-for-human / wontfix。See `docs/agents/triage-labels.md`.

### Domain docs

单上下文：根目录 `GLOSSARY.md` ＋ `docs/adr/`，不存在时静默跳过。See `docs/agents/domain.md`.

## Lessons

- When 产研按模块推进，do 同时跟踪验证、实现、主游戏集成与版本内验收；新包写明接入负责者和默认应用变化，已验证成果进入游戏建设，不让纯探针进度替代玩家版本进度。

- When 讨论模拟经营的下一开发里程碑，do 先明确玩家决策、系统反馈与可推翻的玩法假设；单建筑操作/执行链只作子验证，不替代持续经营与目标委托的真人体验验证。

- When 所有者验收游戏体验，do 以玩家目标、有效选择和连续世界结果衡量进度；技术子票通过不等于可玩版本，工程验证包必须明确标识，下一批优先贯通玩家闭环。

- When 所有者要启动试玩，do 提供已实测的双击应用入口；终端参数留给工程说明，不让用户承担开发启动流程。

- When 游戏测试进程已知且正在运行，do 直接绑定其窗口读取，避免全量UI盘点超时；关窗后只读应用清单，不重新绑定已关闭app以免自动重启。

- When Bridge续接轮次可能重置shell目录，do 首个命令以绝对路径进入工作树并核对pwd，构建日志每轮独立命名，禁止覆盖失败原件。

- When 所有者已授权无问题直接合并，do 核对当前差异并及时集成合格PR；旧PR正文的“不合并”只保留为历史授权记录，不继续用它搁置交付。

- When conducting engineering game, GUI or performance tests, do launch and accept them in engineering; coordinate art only for observed shared-machine load, never gate startup on an art acknowledgement or assign engineering tests to art.

- When 所有者交回外包包并恢复主控协调，do 由Codex通过Agent Bridge直接安排GLM返工；包内子代理已获授权，仍由主控review、验收和合并。

- When 已冻结任务可独立修改不同目录，do 同批提供并行任务包与固定基线，集中review验收；只按真实依赖串行，不逐票等待集成。
