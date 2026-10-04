# 接管批次 2026-10-04

授权：所有者本轮接手请求。基线 902a662392e8047364ace7c0ce93d151945dbb59；不合并、不发版、不扩至完整 T3。

1. [x] 核查 remote/HEAD/工作树/PR/Issue/工具；证明：接手复验报告与旧证据副本/hash。
2. [x] QA-01：旧 CSV/summary已归档复算；新Metal三次含有效内存/截图。帧率差异显著，不签性能稳定；原文与限制见复验记录。
3. [x] QA-02：实际GLM交付→109独立用例→限定commit集成；实际Godot19错误矩阵均非零且不建半场景。
4. [x] QA-03/04：移动只读包/不同cwd/重复默认输出/引擎中断/不可写输出通过；实际Forward+/Metal与Mobile/Metal身份。GUI关窗中断NOT_RUN。
5. [x] QA-05：精确SDK、原始模板两次处理同hash、干净检出构建启动、签名和.NET8实际runtime通过；新OS无SDK验证NOT_RUN。
6. [x] QA-06/ART-I00：实际GLB导入与独立GUI；原点/尺度/前向/附件/七态桥通过，旧生成区段和fixture不变。生产美术与所有者视觉验收NOT_RUN。
7. [x] 独立 architect/reviewer 审查、必要检查、提交开发分支 PR；不自动合并。报告按 QA 子票独立标接受/待复验。

文件归属：GLM 仅 prototype/scripts/Configuration/*.cs 和 tools/qa02-tests/*；主控其余文件。两轮同接口失败先缩票重计划。旧本机导出与测量不得覆盖。

构建诊断：首次Godot构建回调失败；直接dotnet build定位SHA256命名空间引入造成RandomNumberGenerator歧义。改为仅导入SHA256别名，编译前直接显示诊断；未改引擎/依赖。独立审查发现极小正数的派生量下溢/溢出：由主控增加建场景前派生量检查，纯QA-02合同交付未违约。

两轮整套构建未完整通过，先停止原路径并拆解复核：C#编译已0警告/0错误，新应用已导出且签名有效；第二轮终止在lipo参数顺序错误，已对现有产物用正确顺序独立验arm64。下一轮使用提交后的干净工作树、新输出目录，不覆盖两轮日志或旧包。

重计划结果：代码7341ea8的干净检出整套流程通过；两次失败日志保留。原始旧包、旧benchmark与旧报告未覆盖。验收口径、外包归属、当前限制与下一批仅T3a建议见docs/engineering/reports/2026-10-04-takeover-verification.md。独立reviewer/architect审查与整改均完成；开发PR #16已创建并附本会话，任务#15追踪性能复核，不自动合并/关闭。

公开前脱敏：保留原内部分支/commit，发布分支qa/t1-t2-reviewed-20261004从基线整理为9128523；只改变测试命令注释。运行文件与已测7341ea8包逐项hash一致，证据不伪改构建SHA。

交付入口：https://github.com/liuyejinghong/yudian-game/pull/16 ；https://github.com/liuyejinghong/yudian-game/issues/15 。原repo/main仍902a662且干净，本轮初始脚手架移入可恢复的private/tmp备份；旧导出与测量不动。

## todo-r1 · 当前只整理两线

所有者新指令：先建立产研/美术结构化TODO，尽可能使用GLM外包；暂不推进后续实现。不会由TODO优先级触发生产派单。

1. [x] 读取当前Issue/PR、既有14项美术清单及QA实际结果；证明：TODO来源/快照日期/未合并状态。
2. [x] 主控冻结状态/依赖/执行槽、产研拆分和最小GLM文档票；architect独立指出历史快照/材质写者/阶段授权边界，已纳入。
3. [x] GLM独立工作树交付美术TODO；证明：原ART ID集合、状态和文件归属核对；主控独立接受，不制作资产。
4. [x] 整合根索引/两线权威表和已有Issue链接；证明：唯一ID、依赖引用、无循环、链接/文档diff通过；无需重复运行游戏性能。
5. [ ] 文档分支/PR与GitHub总索引，清楚依赖未合并PR16；不改main、不关闭旧工单、不实施新功能。

文档验收：GLM单文件原提交3961cc7独立接受，15美术行加18产研行共33个唯一ID；状态/优先级/链接/私人路径检查通过，显式依赖无环。architect另指出T3a合同设计自我前置，已把领域合同改成设计产出并明确设计票与实现票READY条件。仅Markdown变化，未重跑游戏检查、未启动后续实现。
