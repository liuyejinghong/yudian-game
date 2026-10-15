# tutorial-r2 批1 · 执行计划

2026-10-14｜用户已明确授权批1实施、验证、GitHub任务、独立分支提交推送与draft PR；不合并，不修改默认应用或正式存档。

目标：新玩家看懂一件缺料工程，授权机器人补前置，观察采矿→加工→运输→施工→建成接线，并能解释资源、工程状态与世界结果。

沿用上级目录 remediation-plan.md 与 hud-spec.md。本计划不覆盖根目录历史 PLAN，也不重判原发现状态；D001 仍需无提示玩家验收。

## 1. 固定并验证完整 r1 基线

- [x] 建立独立工作树和实施分支，从研究文档提交 fc32f13 起步。分支 feat/tutorial-r2-batch1-20261014。
- [x] 只读定位原 r1 源码与候选包。共享工作区登记HEAD为953573f，教学源码未提交；研究分支不是完整r1源码。原r1包PCK历史指纹8cf854790304c9ceaa7e0901c00fd13082df9e04c3a53de0676dea889346ca86。
- [x] 将完整必要 prototype 源码、场景、配置和资产复制到本工作树，覆盖前备份本树文件，保留原工作区不变。证明：prototype 294/294 与补充工具/合同 10/10 独立哈希0差，旧包清单207项0差，源前后快照稳定；生成缓存和导出不入库。旧清单外87项本轮另固定raw指纹，不外推旧包字节。
- [x] 实际检查Godot/.NET版本、构建、原生启动。证明：SDK10.0.401/Godot4.7.2实测，build exit0/0警告0错误，import第二次exit0无ERROR，Metal真实主页与地形12机器人就绪；使用私有槽根。正常键鼠进入新游戏未取得证据，工具焦点/投递失败已保留，不代签玩家体验；导航与无画面生命周期合同均exit0。见 VERIFICATION.md。
- [x] 保存基线与证据。独立提交 9c85b98 已固定源码、身份与验证记录；推送 origin/feat/tutorial-r2-batch1-20261014 实际 exit0 成功。可重复构建启动命令见 VERIFICATION.md；原包、默认应用与原档保留。计划完成记录另作纯文档提交。

## 2. 收敛规格与依赖任务

2026-10-14 所有者最新指令：按前面定好的修复方案进行修复，需要自动化验收时整理prompt交Codex；同时启用Workflow。此为现有批1范围的直接执行授权，常规实施合同与文件所有权由主控固定，不再次访谈已认可方向。

- [x] 在已认可remediation-plan.md/hud-spec.md与已冻结r1源码上固定批1公共协议和文件所有权，记录到implementation-contract.md；GitHub #56–#61已发布，依赖与所有权见task-graph.md。
- 证明：合同与任务图恰好覆盖批1，数据来源、状态轴、对象stableID、面板输入暂停与存档边界清楚，每票有实际可执行验收。
- 旧工具拒绝记录保留为历史：此前to-spec被Skill入口拒绝，故当轮只完成基线。最新工作按所有者直接修复指令推进现有方案，不再把该skill的直接调用作为本次前置；未调用或冒称运行该skill。

## 3. 委托实施、集成与交付

- [x] 按真实依赖用所有者启用的Workflow委托：定义/纯函数由GLM，HUD独立worker，权威状态/交易/保存恢复由Sol；明确agentType与文件所有权。主控保规格、根因、公共协议和最终验收。DTO经主控核对后HUD已开写，提交交付仍待实际检查。
- [x] 尽早集成定义→权威投影→HUD纵向链路到同一版本；B002正常UI复现与同目标恢复取得根因后修复并复验PASS（原计划交Codex，所有者2026-10-15新授权由主控run-02亲自执行，见native-fixes.md）；取得根因前不改寻路/坡度。
- [x] 实施和验证分开；先规格，再质量，集成后交易/持久化/恢复一次Sol整体审查（含新增站保存兼容P1与列表预算P2返工后的增量复审）；主控独立执行实际非GUI合同、类型/编译与构建检查。
- [x] 输出新双击候选、独立分支/draft PR与准确工程结果；固定源码/包/私有测试入口并写Codex原生自动化验收prompt。未跑GUI记NOT_RUN，未取得无提示真人证据不关闭D001。
- 证明：账实一致、预览零消耗、双提交一次、取消保真物料；面板空态/重复入口/遮罩/Esc/暂停/epoch恢复；真实缺料建设与接线；未跑项明确NOT_RUN。

## 已知事实与执行边界

- 本会话工作树守卫拒绝把Git重定向共享checkout；不绕过，不委托他人代理被拒动作。源码身份可从本工作树worktree登记与只读文件证据交叉核对，源内容用文件快照哈希固定。
- agent-desktop 当前Accessibility、Screen Recording查询granted；Peekaboo本地同项granted。automation unknown，只用已获所需权限的授权游戏路径，不修改系统权限。
- B002已有两组对照与CargoWaiting采样；FreeStation/OrderDelivery失败原因未可靠落入工程状态，具体障碍仍未证。取消旧货用于另一工程不等于原目标恢复。
- 不新增依赖、地图、玩家背包、M/B机制、科研/kit新配方或批2地形扩展；不付费，不修改默认应用、正式存档，不合并、不force-push。
- 同一步两次失败，停止盲试，记录证据重新规划。

## 执行重计划

- 首轮实施Workflow中，主控向运行中的B执行者发续接消息后出现额外执行上下文，B报告Main.Player.cs的两次mtime/内容冲突。主控停止首轮A/B/D工作流及续接上下文，保留已落地DTO与Main.Player/Lifecycle/PlayerSave局部成果；核journal/执行记录后从现有差异重新派单一writer，不回滚有效成果。HUD独写PlayerUI继续，无重叠文件。
- 后续运行中的workflow执行者不再中途SendMessage续接；完成后需要修正才开新pass。原失败/停止记录保留，不能算完成结果。
- 中途编译3个UI自测适配错误修正后，实施结束编译0 warning/0 error；首轮headlessUI仍187项中2失败/1阻断（接线ID断言、布局选点），先诊断再返对应owner，不降低标准。R2authority首轮600秒限时仅到自然step16/time479秒、ERROR0后超时124，保原件；固定可复跑私有runner调整墙钟时限，不改变模拟步长/物料/健康/路线。原教学生命周期边界已完整exit0 PASS。
- R2专用authority第二轮1800秒仍超时124、ERROR0，已到自然step32/time718.4秒运输；两次未完成后停止加时盲试，重计划昂贵DTO/route/深复制检查按intentVersion/阶段变化与定期兜底调度，真实模拟步长/资源/健康/路线不改。另完整原full-storage prepare/resume自然合同已两进程exit0 PASS、ERROR0。
- 独立Sol整体审确认管理层级/生命周期/首次建设/稳定ID/旧等待理由/目标卡/诊断入口等合同缺口，integration-fixes.md冻结局部返工。HUD已落地；权威返工因API502中断无结果，重派Sol续接保局部成果，未将缺失结果当通过。
- UI第二轮762项/17失败/1阻断，ERROR0：4项测试顺序使空蓝图前提不成立，13项建设卡主按钮/收益溢出已由真实矩形确认。两失败后固定ui-layout-replan.md：主操作/阻塞常驻，完整成本/来源内部滚动；最小局部调整与新证据目录重验。

## 2026-10-15 · 原生缺陷修复轮

当前授权：先修批1 N001–N004，再谈批2/批3。起点 HEAD `bc00e1d78627fdf84e35f5e67f3b143a77fe3005`，工作树干净；新修复分支 `fix/tutorial-r2-batch1-native-20261015`，保留原分支及原生 run01/run02 全部证据。所有者2026-10-15再授权：run-02原生复验由主控亲自操作、核验与记录。

1. [x] 只读复核原生报告/截图/原字节现场，对 N003/B002 取得每个卸货候选、工位与路线失败的运行证据，再冻结最小修法。证明：两原档独立私有根正常UI载入并重试，同目标四候选真实终点接网失败，具名机器人/设施与地形三角原因见 native-fixes.md；诊断不修改世界、不松坡角/碰撞、不赠资源。
2. [x] 独写 HUD 修 N001 活动接线操作区溢出、N002 相同拒因重复、N004 关联取消按钮无反馈。工程实施与返工已完成：主控独立终轮headless1130/0/0、exit0、ERROR0，Sol增量复核两处发现已修妥。最终包原生复验：N004关联取消run-02原生PASS；N001接线原生本轮FAIL——普通CoreAudio两次启动主线程样本卡在AudioComponentInstanceNew/HALC_ProxyObject同步IPC（不判游戏逻辑、不签普通启动），仅新私有clone加官方 `--audio-driver Dummy`（Metal/正常CUA/同源4f311e6不变，audio不在PASS）后正常UI 1280×800/UI150%复现：确认/关闭可见，但候选列表滚动末端最后pair被裁、点选更长拒因后列表高度0（E027–E029，E030/E031保存退出ERROR0）。writer修法已交（Hud.cs+SelfTest，自测多pair39PASS/full1130PASS），主控独立编译0警告0错误、专用合同conn log 39/0/0、ERROR0（8pairs、longList98px、last59px、scrollMax500、inListTrue、短/长list98/98）；首次主控测试传归档路径被私有root门拒（该ERROR保原件、已停进程），原字节复制允许根后完成；Sol静态复审无可证新增阻断，首帧/多pair三倍率/长reason内滚待新包原生优先补100/125/150；full终轮主控已亲读1130/0/0、ERROR0、后台exit0，修法commit `262033d`（与保存 `e080363` 合计只三个raw源码相较4f311e6改变、其余301个raw含配置/美术/导航相同；两commit尚未push），原生复验未过前N001仍记FAIL，headless/合同数字不代完整可读。N002待最终图形复验。证明：原生原步骤；不同拒因均保留，Esc/管理临停/版本门禁保持。2026-10-15解锁后run-04新包（4bd52ff）原生复验N001 PASS（限逻辑1280×800、Retina 2560×1600 scale2）：正常设置150%载原E030活动build-12 Delivering手动暂停，真实8 pairs/滚条/末项选择，长短拒因均响应；同world 150%/125%/100%分别实测滚底402/253/186，末项9.8m完整可读可选，target/source/费用线缆4/净缺口无/拒因/确认（按规则禁用）/关闭完整在面板且关闭实际可用；四份GUI_TEST新帧已读（E012–E015）；正常UI保存退出PID56836、退出日志E011（44行）ERROR0/rejected0/saveOK2；E001与E030同Time 10540.633333264095/Paused=true，差异仅正常载入归零的Velocity与HealthTravel，不称snapshot全等。不宣称未观察的首渲染帧与超长拒因内部scroll。
3. [x] 委托物流根因修复，实施和验证分离，集成后一次独立 Sol 核交易/取消/恢复边界，主控跑必要编译/合同。工程实施、Sol审查（含保存兼容P1返工后增量复审）与主控编译/导航100项/R2权威合同已完成；原生原目标恢复N003/B002与取消保货/保地形/预约释放（含N004关联取消）已PASS。货物再利用原生已跑：取消→合法新storage(-5.3,10.8)→旧货真实退仓→build-10 Completed成立，但完成后normal save失败（E034；拒因实有记录：E035第39行 `PLAYER_COMMAND_REJECTED`“采矿任务阶段或工位无效”，此前搜索漏关键字），整链不签PASS；reuse-save记建议P1。根因主控已核定：Main.DevelopmentSave.cs:123历史Completed mine AtDock用当前terrain高度，新仓储整平v13→14致历史站高-0.21634348对地面0差0.216343477m，throw早于写文件、非磁盘/权限；修法已冻结并落地为最小修复commit `e080363`（Main.DevelopmentSave唯一7+3 diff，仅Completed mine免当前地表高度相等，finite/轴向半径/场界/robot/id/work/paid/receipt/patch与live/Blocked/Cancelled严格性、schema/旧档/模拟不变），主控独立复跑 `run-03-controller` exit0全PASS（亲读stdout 39–83：28 cases、公共保存/载入/保存PASS、snapshot全等、slot3=.bak字节相等、ERROR0/command_rejected0；不代原生，待新候选复验）。证明：分别记录，保已有通过流程。2026-10-15解锁后run-04新包原生复验reuse整链PASS：正常UI载E032原字节（Cancelled/Paused，E002=E032），blueprint仓储点击[650,1000]与原失败位置(-5.286148,0,10.807249)/yaw0完全相同，真实退仓/整平/物流/施工至build-10 Completed→正常保存E003→正常退出E004（29行、载v13）→同根冷载Continue→保存E005→正常退出E006（18行、载v14，与E004不同日志）；E003/E005同Time 7139.98333331358且冷载后仅2个HealthTravel归零；terrain v13→14而历史矿站prod1/2/3/15/16/17站高-0.21634348保留（当前地面0）；原35任务JSON语义相同、仅新增haul36/37（cargo铁4/结构件4→build-8），真实build-10消耗铁4/结构件4/套件1，Mining/Recipes不变、无新增采矿配方不赠料（spent是已消耗账不叫库存）；主控独立asserts实际输出 CONTROLLER_NATIVE_REUSE_CONTRACT_PASS original35=true new_haul2=true no_mining_recipe=true same_site=true cold_time_terrain_ledger_development=true。不照搬子代理早先“identical”误记（E007已返工修正、pre-correction原件.bak保留）。reuse-save由e080363修复并经新包原生闭合。
4. [x] 提交推送修复分支，重新导出固定 commit/manifest/PCK/runtime，生成新独立私有根。源 `4f311e6`、PCK `f28a716…`、arm64/严格签名、内嵌CLR8.0.31；分支已推送（修复代码 `52f4750`，现文档head `26d4c41`，后续纯文档提交不需重导包），draft PR #63；原候选/正式档/默认入口字节保留，首次TLS失败原件保留。
5. [x] 新 run 正常 UI 原生复验；精确尺寸受屏幕钳制和 A/B 两根不足的旧限制如实补证或记 BLOCKED。run-02按所有者新授权由主控亲自执行：N003与B002原目标恢复、运输保存冷载与完成后冷载均PASS（E001–E012）；N004关联取消与取消保货/保地形/预约释放PASS（E013–E020），退出日志全ERROR0，独立于run-01旧工具阻断与34ERROR历史；N001接线原生本轮FAIL（E027–E031反例：列表末端裁切与更长拒因致高度0；普通CoreAudio启动另记BLOCKED），GLM HUD writer冻结预算返修中；货物再利用整链本轮FAIL（reuse-save：build-10 Completed后normal save失败E034，拒因E035:39 `PLAYER_COMMAND_REJECTED` 有记录、旧档字节保，E032–E035；根因Main.DevelopmentSave.cs:123历史Completed mine用当前地表高度、throw早于写文件，非磁盘/权限），建议P1、Sol按冻结修法实施中。b002-active已正常unpause（无需retry）至Completed并正常保存E036、E037退出ERROR0（Metal+Dummy，非普通音频；冷载未取不另签）。较早生产起点已PASS（E039 goal-9/build-9原中心yaw0 Completed、Time 6910.266666650256、新增mining1/recipes4、configHash同、正常保存成功，E040退出ERROR0；无单独冷载轨，同目标冷载证另列，音频Dummy）。精确1920×1080/1920×1200实测均被屏幕钳制到逻辑1860×889（E041/E042，像素3720×1778/scale2/dpi163，主页设置三倍率实操）：BLOCKED，不签游戏布局、不改display。A/B两独立根（原seed hash同cb347b8a）两流程PASS（音频Dummy）：A组直接建设/保存（E044 Completed、center(12.505365,0,-11.181097)/yaw0，E045退出ERROR0）；B组先手动整平（E046 Completed并保存，center(12.52281,0,-11.197887)、terrain v5）再solar（E047 Completed/Built=true、center(12.43391,0,-11.311815)/yaw0、Time 2210.4500000005346、正常保存成功，E048退出ERROR0）；不作严格同点同相效率比较。N002待最终图形复验，逐项PASS/FAIL/BLOCKED/NOT_RUN，不以headless/另址成功签原目标恢复；新候选待导出（`262033d`/`e080363`尚未push），N001/reuse原生仍FAIL待修后复验。2026-10-15解锁后run-04终态：用户提示锁屏并解锁后同旧实例AX截图恢复，run-04正常载档操作，run-03 BLOCKED仅保历史、不作当前结论，未改系统权限/服务/信任；新包三入口独立clone官方 `--audio-driver Dummy`+Metal：N001 PASS（三倍率，见第2项）、reuse PASS（整链含冷载，见第3项）、N002 PASS——原E350正常载入，manual pause保存E008基线后solar预览合法(2,7.6)按钮可用，非法(-4.9,24.5)“设施与作业站须位于场地内部”仅一次、(-15.6,9.3)“建设不得覆盖已知矿点与采集包络”仅一次，回合法按钮可用，未提交任何工程退出预览正常保存E009，E008==E009完整JSON全等（Time 136.61666666666753，无预约消耗/地形变化），E010退出ERROR0/rejected0/saveOK3（PID96337）；不宣称同帧两个不同拒因原生覆盖（Main.Planning 115–151只构造一个非空候选理由）。run-02旧候选4f311e6已PASS证据继承保留不重跑，旧候选N001/reuse FAIL保为历史已修复反例；“新包只3 raw改变”指追加两fix相较4f，非PR整体仅3文件。仍BLOCKED：精确1920×1080/1200钳成1860×889、普通CoreAudio启动（音频IPC，与锁屏无关的独立问题）；G001 NOT_RUN、D001仍P1、旧正式台账不改；e080363/262033d已push、候选4bd52ff已导出并推送（PR #63 draft OPEN，base/head实核正确）。本轮可执行原生复验与证据归档完成（run-04 E001–E022，主控终判E021/E022），音频/精确大尺寸/真人验收仍分列。

旧正式台账 P0=0/P1=1/P2=0；N003 建议 P1、N001/N002/N004 建议 P2 独立分诊。D001 仍须无提示真人 G001。常规实施授权已具备，无需再访谈方向；两次失败仍先诊断重计划。

## 实际验证记录

基线复制、构建、资源导入、Metal原生主页、导航合同、生命周期工程边界已实际通过；准确结果、失败回退、输入工具阻断和可重复命令见 [VERIFICATION.md](VERIFICATION.md)。
批1公共协议已固定、DTO字段主控已核、任务图已回读。新增文档检查实际PASS；批1工程实施、Sol审查、固定候选导出与draft PR已完成，GUI工程回归终轮1130/0/0。原生复验进行中：run-02主控已亲自PASS N003/B002原目标恢复与正常保存冷载、N004关联取消与取消保货/保地形/预约释放、较早生产起点自然生产→运输→施工完成/保存、A/B之A组直接建设/保存；N001接线原生本轮FAIL（writer修法commit `262033d`：主控专用合同39/0/0 ERROR0与full终轮1130/0/0、ERROR0均过，原生复验未过前仍记FAIL；普通CoreAudio启动另记BLOCKED不判游戏逻辑），货物再利用保存失败（reuse-save建议P1，根因已核定、最小修复commit `e080363`已过主控run-03-controller复验，原生仍FAIL待修后复验），N002待最终图形复验，精确尺寸BLOCKED（钳制1860×889），A/B两流程已PASS，无提示玩家验收（D001）未执行；新候选待导出（两commit尚未push，只三raw源码相较4f311e6改变）。

## 本轮收束（2026-10-15最终；历史快照，最新状态见下节run-04）

任务停在可证阻断，逐项终态表见 native-fixes.md“本轮最终状态”。要点：run-02旧候选PASS成果保留（N003/B002、N004/取消、较早生产、A/B）；旧候选N001/reuse-save FAIL保留。新修复 `e080363`/`262033d` 工程全过；新候选 `4bd52ff` 导出成功并已push（`e6e5318..4bd52ff`，PR #63；首次TLS失败保史实；PCK `d86046fb…`/DLL `01bb1f9e…`/304raw/strict arm64/CLR8.0.31/导出ERROR0；仅3 raw源码差异为本次追加返修相较4f311e6（PR整体另含早前原物流/HUD多文件修复），纯文档HEAD前进不改候选身份）。新包N001/reuse原生BLOCKED（未载档0输入，绑定cg，jsreset/exact/唯一running name/fresh same bundle均试；34清理ERROR保；不归因用户权限或新修死锁，枚举原因未证）；普通CoreAudio启动BLOCKED独立记录；N002 NOT_RUN、精确尺寸BLOCKED、G001真人验收NOT_RUN、D001仍为确认P1、旧P1台账不改。不改服务/信任/OS；证据index由主控最后生成。

## 2026-10-15 解锁后run04最新状态（当前总口径）

上节收束为锁屏阻断下的历史快照，原件全保。用户提示“应该是电脑锁屏了，你现在再试试？”并解锁后，同旧实例AX截图恢复，run-04在最终候选（源4bd52ff、PCK d86046fb…、DLL 01bb1f9e…、engine 7b9bf…、完整raw快照304项且manifest所列212项本轮再次0差、strict/arm64、CLR8.0.31、导出ERROR0；HEAD cbca52e（run04开始时文档HEAD）仅docs；修复commits e080363/262033d已push）三入口独立clone（官方Dummy音频+Metal）正常载档操作。批1本轮游戏内缺陷复验通过：N001接线三倍率PASS（E001/E011/E012–E015）、reuse-save整链含冷载PASS（E002–E007、E016–E017）、N002预览拒因PASS（E008==E009全等、E010、E018–E020）；四份退出日志全ERROR0/rejected0、原种子档SHA实测不变、RUN04运行进程NONE（主控终判E021/E022）。继承run-02旧候选4f311e6已PASS证据与旧候选N001/reuse FAIL历史反例。音频/精确大尺寸/真人验收分列不并签：普通CoreAudio启动仍BLOCKED（独立于锁屏）、精确1920×1080/1200仍钳制1860×889 BLOCKED、G001真人验收NOT_RUN、D001仍为确认P1、旧正式P0=0/P1=1/P2=0不改；批2/3未开、未合并main。私有详报run-04/report.md、results.json、findings.md；证据index由主控最后生成。
