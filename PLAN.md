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
5. [x] 文档分支/PR与GitHub总索引，清楚依赖未合并PR16；不改main、不关闭旧工单、不实施新功能。

文档验收：GLM单文件原提交3961cc7独立接受，15美术行加18产研行共33个唯一ID；状态/优先级/链接/私人路径检查通过，显式依赖无环。architect另指出T3a合同设计自我前置，已把领域合同改成设计产出并明确设计票与实现票READY条件。仅Markdown变化，未重跑游戏检查、未启动后续实现。

文档PR18已创建并附本会话，base为未合并PR16分支；总索引Issue17开放作为长期入口。旧T3/T4/T5的ready-for-agent标签按当前仅整理阶段移除，原Issue范围/认领/开放状态保留，未来按子票重新派单。main与PR16代码不变。

## art-brief-r1 · 主控需求与下一批GLM编排

所有者授权：提交TODO、完成时更新、编排可交GLM的任务，并由主控做美术侧需求。不合并、不量产整批模型，本轮只额外执行需求所需的只读工具预检。

1. [x] 核对文档分支已提交PR18且依赖PR16、main仍902a662；证明：当前Git/PR记录，不把“提交”解释为自动合并。
2. [x] 主控定义M01六材质与U01/F01/F02首样要求、比较镜头和状态边界；证明：首批需求与M01票，不把候选配色/比例冒称视觉接受。
3. [x] GLM预检现有工具/source/hash；证明：独立工作树仅两证据文件；第一版误判两处路径，限定一次返工后主控版本/hash独立匹配，原始提交保留。
4. [x] architect独立审查；发现M01工具入口、F01/F02的U01前置和offline映射与权威表不同步，已逐项修正。共享材质是下一张READY票，其余生产票仍DRAFT或BLOCKED。
5. [x] 文档/链接、任务ID与旧证据不变检查；提交推送并更新PR18/Issue17；证明：需求提交ad1a52c已推送，PR18远端head一致、OPEN且base仍PR16开发分支，Issue17已附交付入口。未制作资产，不重跑游戏性能。

工具预检只接受探测报告：Godot4.7.2/Python3.14.3/.NET SDK10.0.401可用、Blender在已查路径未找到，GLB/生成器hash匹配；生产流水线未复验。ART-BRIEF-01只接受需求交付，所有者视觉效果NOT_RUN。下一批按M01→预览/U01→F01与F02分目录；T3a由主控定合同再外包数据/候选网格/测试。

最终检查重计划：临时检查第一版正则漏掉ART-TOOLS/BRIEF/PREVIEW的带连字符ID，第二版将旧证据内容数103误用为目录全部受控文件数（实际104，包含清单本身）。停止原合并断言脚本，先独立列出35个ID/9个受限ACCEPTED/唯一READY=M01和Git固定基线的完整文件集合，再按该集合逐字节比较；不更改旧证据来迎合计数。

重计划检查通过：35个唯一任务/9个受限ACCEPTED/唯一READY=M01、104个旧受控证据文件逐字节未变、无私人路径；既有文档验证器/链接/档案hash和git diff --check通过。architect最终复核无文档交付阻碍；main仍902a662且干净。实际工具预检会话已结束，后续生产票均未派单。


## terrain-data-r1 · 合并与GLM产研执行

授权：所有者明确允许没问题直接合并，并要求继续推进GLM产研。先只合并已独立审查PR16/18，不顺带合并13/14。

1. [x] 精确head/冲突核对，PR16合并c2113ca、PR18改main后合并707258f；原repo/main干净快进，旧工作树/历史证据保留。
2. [x] 主控冻结局部高度合同及金样，architect审查；修正字段计数/版本名称和独立运行时事实：net8编译、net10运行，不安装运行时。
3. [x] 独立工作树实际GLM实现纯数据接缝/测试；证明：Bridge模型确认、task/session、限定commit/diff与工具读数。
4. [x] 主控运行新测试、金样/异常/只读独立检查；独立reviewer审持久化接缝；处理具体发现后整合，不把codec当游戏保存。
5. [x] 更新TODO/证据、必要检查、精确提交并发布开发PR；证明：远端head与本地提交一致、完整证据可审查。完整T3、模型/碰撞/导航/资源状态不在本数据票。

发布后的集成动作：无新问题则按已有授权合并，原main仅干净快进；实际合并时间/提交以本批PR的GitHub状态为准，不把发布检查当作已合并。

本批验证：GLM首轮实现d1f7cd6；reviewer发现两个真实缺陷，主控在旧代码新增回归得到28通过/11失败。GLM限定返工1a0ec1c，主控集成80b8148后388工人检查+39独立检查通过，两TFM编译0警告0错误；reviewer追加12项复核通过。首次临时主控工程缺apphost包，以UseAppHost=false解决，无下载。补齐主控测试的全部字段往返、patch/base不可变与解码回归；原失败/初通过/最终通过均保留。只接受数据接缝，游戏图形/导航/保存/性能NOT_RUN。

发布入口：https://github.com/liuyejinghong/yudian-game/pull/20 。已发布完整数据/测试/证据/TODO，精确远端head与本地一致、MERGEABLE/CLEAN；GitHub未配置本PR的自动检查（空检查列表），接受依据为本地实际检查与独立审查。最终architect指出下一批说明仍列已接受T3a，已改为冻结T3b/T3c候选票。

## terrain-mesh-r1 · 两个GLM生成子件

授权：所有者要求继续产研，沿用无问题直接合并授权；美术需求由新会话独立维护。起点main3999d30，只在本产研独立工作树写工程目录，共享输出由主控唯一编写，不改美术或原Main/fixture。

1. [x] 冻结两种网格输入/精度/布局/插值/边界与共享输出；architect复核。证明：非对称/扭曲/非零邻边金样和编译检查；不把纯三角化叫作完整两方案比较。
2. [x] T3b-MESH/T3c-MESH分工作树/源码/测试实际派GLM；记录原生模型确认、Bridge任务与精确合同base。证明：限定diff/提交/真实离线build/run。
3. [x] 主控独立几何金样、输出只读、候选版本和邻边检查；独立审查、处理具体发现。证明：真实命令与原日志/源码hash，不沿用数据票检查充数。
4. [x] 更新产研TODO和证据、发布PR、核验精确远端head与必要检查。证明：GitHub开发PR和真实检查记录。T3b/c父票仍等待候选集成/更新验证；美术/导航/保存/性能不冒称完成。

发布后集成：按已有授权合并没有问题的精确head；实际合并时间/提交以本批GitHub PR状态为准，原main只有干净时快进，不把本地发布检查当作已合并。

architect前置结论：本批仅两个高度样本三角化子件；必须分清顶点双线性与三角面平面插值，固定输入版本验证/顶点索引计数/绕序/边界容差，并保留焊接/法线接缝等后续范围。均写入合同r1。

合同最终architect复核无冻结阻碍；非对称/扭曲cell金样与计数、正Y绕序、共享防御复制及分目录归属一致。共享输出net8/net10离线编译作为派单前检查，实际编译结果另记录。

共享编译诊断：首次临时工程net8已输出后，net10共享编译停顿；主控只终止该临时build进程，不能将中断当通过。改为-m:1、UseSharedCompilation=false、nodeReuse:false后两TFM编译exit0、0警告0错误，无工具链安装或源码修改。

GLM实际交付：T3b原82fa292→集成69d5256，T3c原11bb78a→集成bb8e240。主控重跑66/82工人检查和89独立检查全通过；两TFM及原Godot C#工程编译0警告0错误。reviewer静态审主控测试指出水平中点/候选细分点漏测，已补15/35完整手写金样并通过；彼时最后实现审查仍待结论，未提前标最终接受；最终结果见下方记录。

最终reviewer未发现实质缺陷，独立执行验收产物89项通过；两子票受限接受，父票仍DRAFT。simplify复核实现的注释/结构符合当前小子件范围，未另加框架或行为修改。

最终architect核对66/82/89及源码hash/父子票范围一致，仅发现TODO页首沿用历史707258f；已改成本次3999d30并明确PR20前置已合并。

开发PR23已发布：https://github.com/liuyejinghong/yudian-game/pull/23 。远端head与本地一致、MERGEABLE/CLEAN，自动检查列表为空（不冒称CI通过）；接受依据是本批实际编译、测试和独立审查。合并状态以PR为准。

## terrain-render-r1 · 独立Godot候选显示

授权：所有者“好，推进吧”；沿用无问题直接合并。起点main0a1e54e，本轮一个GLM适配子票+主控独立探针，不改美术生产/旧Main/fixture和CPU合同。

1. [x] 冻结Godot位置/绕序/平面法线/精度/所有权与实际更新语义，architect只读复核。证明：明确CPU正Y→Godot顺时针转换、float溢出退化拒绝、实际验收案例。
2. [x] 独立GLM实现适配器和引擎headless测试，主控写单独probe/新fixture。证明：模型确认、限定提交、实际build/headless，而非直接运行native测试DLL。
3. [x] 主控复验headless、GUI输入/重复/拒绝/重开、真实渲染器同条件截图与背面剔除；独立reviewer处理实际发现。证明：日志、PNG、source hash、未运行项明确。
4. [x] 更新TODO/证据、发布PR、核对精确head与必要检查。合并状态以GitHub PR为准，按既有授权合并，原main只有干净时快进。

前置architect指出必须固定主线程/展开顺序与法线、处理float退化、两资源成功才替换/失败保持、释放生命周期；“撤销候选预览”不得表述为撤销已发生世界改造。全部纳入合同。更新只完整重建替换，不能冒称局部GPU/导航/保存更新或稳定性能。

合同复核补齐两点：Godot引擎测试统一Debug构建/显式DOTNET_ROOT并核对程序集位置与SHA；归一化后必须接近单位法线，有限叉积的长度平方溢出也明确拒绝。实际CLR以启动输出为准，不推定Godot与独立dotnet runtime相同。

本轮主控复验：10适配器命名项、35探针日志检查，Debug0警告/错误；Metal真实GUI三态/重复/拒绝/撤销/冷开通过；back-cull top141512粉像素/bottom0。未引用vertex float溢出先真实复现exit1再GLM修复，失败日志保留。最终reviewer无剩功能/资源缺陷；两处MVID注释已修，architect最终复核通过；完整T3不关闭。证据见docs/engineering/reports/2026-10-04-terrain-render-verification.md。

显示PR25已发布：https://github.com/liuyejinghong/yudian-game/pull/25 。发布head1ce732b947c3f4cb6837bddeb9130082ebffcd8c与本地一致；GitHub MERGEABLE/CLEAN，自动检查列表为空，不声称CI通过。此次后续只记录发布事实、未改源码；最终head须再次核对后按所有者授权合并，合并状态以GitHub PR为准。

## terrain-collision-r1 · 显示/静态碰撞同源与美术联调

授权：所有者持续推进并与美术组联调，缺素材/不合格挂TODO；合格直接合并授权沿用。起点main991b78e，独立集成工作树；美术production/materials保持其独占。

1. [x] 核当前现场、冻结单GLM碰撞票与请求/同步合同，architect计划审查。证明：同帧level→dig/base/非法的定义、全部新资源成功再换、下一物理查询帧同步证明，不冒称引擎事务。
2. [x] GLM独立实现静态shape适配与真实物理引擎检查。证明：原生GLM确认、限定commit、Debug/loadedMVID/native ray/headless退出与失败记录。
3. [x] 主控增加可选碰撞候选模式/队列/独立分帧检查及GUI，和美术组按接受commit固定M01联调；缺正式素材留工程TODO。证明：显示与body/shape同源、三排队序列/失败保持/释放/真实射线/材质逐surface与拒绝/PNG。
4. [x] reviewer/最终architect审查，更新TODO/证据，发布和核对精确head后合并；干净原main快进。证明：实际源/素材/PNG哈希、PR/Issue状态，完整T3保持未完成。

architect明确早退不能丢弃撤销pending，同帧非法最新请求要清掉先前候选；四属性赋值不证明native同步，下一物理帧核对象/位置/法线；非顶点斜坡及shape独立寿命、明确mask/单面；材质等待其主控接受，不能抢写目录。本合同已纳入。

## 用户ZCode workflow并行包

所有者指出线性编排，要求可自行开worktree并发GLM、主控统一review/接受合并。architect只读复核后冻结PERF-01-COMPARE与LIFE-01-AUDIT：独占新增工具目录，复用既有复算/真实证据，不相互等待、不争写公共文件；当前T3-COLLISION已领取不得重复。只接受离线工具，父票实机采样/GUI部分保持未完成。步骤：写明接口/金样与退出规则→固定基线→提供独立workflow prompt→实际交付后再review/复验/更新状态。

本批复验：GLM2ea7a23→9768674限定4新文件；主控12适配命名项、118条碰撞检查、36条默认检查与各自SELF_TEST PASS、exit0；MetalGUI及M01/debug三态PNG通过，最终日志确认GodotPhysicsDirectSpaceState3D（不能只用DEFAULT断言）。GUI/PNG DLL0E60与最终加日志字段DLLFDEB分别记录，几何/状态/材质行为未改。reviewer最终无剩实质缺陷；最终architect唯一索引P2已修，无其他发布前必修；发布合并仍待完成。

碰撞PR27已发布：https://github.com/liuyejinghong/yudian-game/pull/27 。发布head1c5bf805d661cd894d391faaf54da4bc9192f16b与本地一致、MERGEABLE/CLEAN；GitHub自动检查为空，不声称CI通过。本次只补发布链接，无源码变化；最终head须再次核对后按授权合并。

PR27已于2026-10-04T12:11:24Z合并，精确head0d837ed48b7cb701da17c5cdf141c3c65702a6d9，merge43daffe88e5040df46134c5a5f09b0fea6cc336f。实际子票26 CLOSED，完整T3票6 OPEN；原main已从干净991b78e快进43daffe。两并行包READY未领取、固定基线1ba4aa6不变；本条只记录已发生事实，不改源码/证据。

## evidence-tools-review-r1 · 两包独立验收

授权：所有者交回PERF-01-COMPARE（ebfb877）与LIFE-01-AUDIT（7f72c19），均从1ba4aa6冻结基线独立实现；合格直接合并。恢复Codex通过Agent Bridge协调ZCode GLM，允许外包使用原生子代理，文件仍单写者。原交付工作树/commit保留。

1. [x] 核原提交、diff归属、现有门禁/日志，PERF独立review、LIFE主控负例；证明：限定目录与实际失败输入，不重复无关全量检查。
2. [x] 冻结各包返工范围并独立派GLM；证明：模型原生确认、两session/task、包内测试及新增提交。子代理授权不等于已实际使用。
3. [x] 分别审查修复与最终集成head，复验原失败点与原始金样；证明：日志/退出码/输入SHA、无覆盖/诊断/类型边界。源变后才重新跑该包完整测试。
4. [x] 每包合格独立PR合并，更新限定ACCEPTED；证明：PR精确head/merge、主分支干净快进。父PERF受控采样/原因/稳定性能和LIFE真实GUI关闭/发行包重开继续未完成。

实际派单：PERF sess_ce7639b6d6 / task_03d935ac1f；LIFE sess_731d81f2f0 / task_67642905bc；均原生回执model=GLM-5.3-Flash，Bridge observed_model未提供，费用未知。初次手动workflow交付不是Bridge派单，不造账。LIFE初轮门禁路径少斜杠属派单方事故，工人拒绝绕过；与代码审查发现分开记录。

PERF独立验收通过：原ebfb877→修复e1df119/440b686/3926518→集成cf55502；50项完整unit、9主控实际复验、3独立解析复核通过，启动尖刺金样不变。PERF限定ACCEPTED；LIFE仍独立处理最后具体发现，不用PERF通过替代其裁决。源码固定，发布/合并事实待GitHub回执记录。

PERF PR28于2026-10-04T13:24:04Z合并，head21df8353da953641989c939515d20989f1f7fcdc，merge58346b83aa623289507befffd1cf5da1da8c61c7；原main干净快进。LIFE原7f72c19→修复a0c2f19/93ef427→主控3493ddb，56完整unit/11主控案例及最终独立review通过，限定ACCEPTED；发布与合并事实等待GitHub回执，不先填完成。

两包已分别合并：PERF PR28 head21df8353da953641989c939515d20989f1f7fcdc / merge58346b83aa623289507befffd1cf5da1da8c61c7；LIFE PR29于2026-10-04T13:32:21Z合并，head7a49e759e0796a9fda02759d5f0dff0b1a5d88ea / merge4c28391202bd2fad26d8f7507be09e2a4f380207。GitHub自动检查列表均为空，不声称CI通过；合并依据为实际测试、源码SHA与独立review。最终architect唯一根摘要旧READY已修；原main两次干净快进，Issue17已更新，父#5/#15保持OPEN。只补合并事实，不改源码/旧证据/美术，不重跑已验证模块。

## runtime-world-r1 · 实机用例与单区域提交

授权：所有者明确继续推进产研，合格交付直接合并；main3541b09为本批起点。主控独立worktree，不改美术生产/旧Main/fixture/原始证据，GLM只实现已冻结的纯判定子件。

1. [ ] 冻结PERF/LIFE真实GUI与采样用例、现有导出包/hash、供电/热压力/前台条件；预约美术GUI窗口。证明：实际CLI签名/运行身份、原生GUI动作、约束失守不删轮次。
2. [x] 冻结单区域内存提交契约与纯判定票，architect审查；GLM独占新判定目录/测试，主控独占权威提交入口。证明：金样、取消/重复/旧base/版本上限/无变化判定，权限与取消仍来自权威调用者。
3. [x] GLM子件交回、主控内存提交实现/独立集成用例、跨关键状态reviewer。证明：实际net8/net10编译与当前可用runtime、失败保留、真实提交版本/不可变旧快照/不同请求竞态顺序；不声称渲染/碰撞/导航/save已同步。
4. [ ] 协调窗口后进行LIFE真实关窗与发行包重开、短受控PERF复核。证明：同包新输出/run-id、exit130/0、原输入及包SHA不变、前台/后台轨迹和供电热压力缺证如实。工具不代替用户关窗；无可用GUI则明确NOT_RUN并继续离线工作。
5. [x] 独立review/architect收尾，限定内存子件已接受并合并PR30；TODO/证据已更新。实机第4步未完成；父性能稳定预算、完整世界导航/持久化和正式美术仍留项。

前置architect建议已采纳：LIFE独立Popen捕获预期130，不用拒绝非零exit的measure_baseline承载关窗；世界第一票只内存/单写者，不把候选四资源切换视为世界事务；无变化不推进版本。Cua全局窗口初次读取超时，未抢占美术窗口，后续只在自有探针已启动时按应用定位操作。

2026-10-04 runtime-world-r1：architect只读审查三份合同/任务包，无阻塞；固定此提交为纯判定派单基线。实机窗口尚未获美术组回执，GUI/性能未执行。

GLM纯判定实际派单：base6554495316cf31ad24a9ee4f6703a306d5a2396b；sess_7697b4c358/task_76fc5bf2a2；新独立worker/terrain-commit-policy-20261004。主控已写World入口及独立验收用例，等待Validation交回后编译，未假称通过。

本批限定验收：GLM原bf6da0a/cherry26cf2a4仅6归属文件，74工人检查；主控28权威状态例、双TFM及实际Godot项目离线编译通过，reviewer两轮无剩余问题。首沙盒多进程build等待/部分net8，初次net10缺DLL/exit1，原日志保留；单进程重试完整通过，未改预期或装runtime。net8执行attempt150缺runtime。LIFE/PERF实际步骤NOT_RUN，美术组仍在同机导入/截图/样件制作，尚无窗口回执；不争用、不冒签完成。

最终architect审查通过：源码/工程12项SHA匹配，74工人/28主控证据分开；仅内存子件可集成，实机明确NOT_RUN、父票未完成。下一步创建限定PR按已有授权直接合并；GUI窗口到位后继续本节第4步，冻结用例与本机一次性采集脚本保留。

PR30于2026-10-04T14:55:28Z已合并：head1d864446889081d430534b2afb11f0816c20390f，merge2001aa0ded25217c978da1a1836b7335b9152724。GitHub检查列表为空，不称CI通过；依据实际编译/用例/reviewer/architect。main干净快进后12项源码/工程SHA一致，无需重跑无变化的检查。下一步仍为本节未完成实机步骤；尚无美术窗口回执，不扩签整个批次。


## 2026-10-05 · 恢复已冻结实机验收

所有者已开机并授权继续；冻结runtime-controlled-r1不变，主控独立工作树从c76b0ea续第4步，不重做PR30。美术当前资料审查，已通知本机约10分钟采样窗口与暂停GUI/编译，结束释放；保留期间实际负载与失守记录，不以通知替代条件观测。

1. [x] 同固定历史包5秒正常默认目录、120秒中真实GUI关窗、同包5秒重开。证明：实际exit0/130/0、三ID/新目录、旧产物与包SHA不变、native close动作及审计报告。
2. [x] 按F1/B1/B2/F2/F3/B3六轮45秒、≥15秒间隔采样。证明：真实前台PID轨迹、AC/热压力/进程负载、原CSV/summary/内存、两组比较。启动负载/失控轮次不删；无温度/profiler不签稳定或原因。
3. [x] 相称复算/只读审查，更新工程TODO、记录限定接受与留项，合格范围按授权合并。证明：审计/分组身份/统计/素材缺口与未完成父票如实。

启动检查：固定包签名有效、AC100%、热压力无告警；开机后多进程CPU活跃，先做LIFE，采样前再次读负载。不能自行停止Nowledge/iStat或其他无关进程以伪造空闲。

LIFE实际完成0/130/0，三ID/默认目录、旧产物与固定包SHA不变，审计待运行。Cua名称定位额外旧包、关闭后AX残留额外窗口均已原生清理，PERF前无Yudian进程。PERF F1置前后大多PID21200/Codex，B1 Desktop点击AXError.illegalArgument；两轮全部保留，不计受控，按连续两次失败停下重计划。原生工具禁止其他UI技术除非用户明确指定，因此已问是否允许AppleScript仅切测试窗口焦点，等待期间只继续LIFE审计/文档，不开展依赖授权的采样。

所有者现已明确允许AppleScript仅切测试窗口焦点。重计划：保留focus-failed F1/B1全部；新独立目录从F1/B1/B2/F2/F3/B3完整重跑，统一SystemEvents按自有PID/现有Finder PID切frontmost；不绕过Cua对Codex的保护，也不改GUI关窗证据。每轮动作/监测/重负载保留，若仍失守不签受控；开始前再次预约美术6分钟，结束释放。

所有者纠正：工程自行拉起和验收游戏测试，美术主要做建模。已在AGENTS Lessons登记，调整当前合同：不以美术回执为开测前提，只对观察到的同机负载冲突协调；已通知美术正常推进建模。已验收成果与原始事实保留。

2026-10-05实际收尾：LIFE三轮审计PASS；新六轮采集/两组复算exit0，但后台目标漂移，受控项REWORK。初次比较因采集验证缺顶层binary SHA被拒，保留原件后仅适配派生字段，CSV/summary/工具不改。11组原始输入与本机原件一致，最终签名exit0、包SHA不变、无残留Yudian。审查与集成待收尾，原因/稳定预算未完成。

本轮reviewer与architect已通过限定边界，architect两处旧摘要已修；GLM sess_97ec371908/task_f97199c402只读复算完成，六轮数字/SHA一致，主控拒绝其相关性即原因表述，22次tool_call仅一次写本机报告。费用UNKNOWN，Bridge used值仅上下文读数。限定证据包准备按授权集成；PERF控制/归因留项不计完成。

PR31已于2026-10-05T02:23:26Z合并：head7c3554399483911118fd23f840a0a5a7bb2b8bac，merge36603cdf9cd4fc593782a2f8d9350b2566ce746a。GitHub检查列表为空，不称CI通过；依据实际LIFE审计、六轮复算/原件SHA、只读reviewer/architect与GLM交叉检查。main干净快进后134项本批文件与接受head一致。LIFE限定ACCEPTED；PERF控制REWORK，原因/温度/稳定预算未完成。原工作树、原始材料保留，GLM会话已结束。

## 2026-10-05 · 清理旧未合并PR

所有者要求无问题直接合并；当前main eb5ba1f，五张旧PR为1/2/3/13/14，只含历史文档/参考素材。隔离工作树逐包修正，保留现行合同/状态，不重做游戏测试。

1. [x] 固定各原head、逐项审查与architect核验；证明：固定diff、旧新权威入口与原资料保留。
2. [x] 更新旧PR历史说明/当前入口、修正冲突，逐张精确head合并；证明：文档/链接检查，27WebP、12SVG、索引blob/尺寸核对。
3. [x] 最终组合验证、同步TODO/总索引并确认open队列；证明：实际合并SHA、main一致性、文档校验输出。

PR清理实测：PR1旧执行入口冲突已保留当前TODO并标历史；PR2从零启动Spec加日期/当前入口；PR3 SVG门禁两次漏合法title/desc，停止并经architect读取实际四元素/属性重计划后完整复核通过，素材原字节未改。27WebP解码/尺寸/blob与12SVG/索引/链接通过。PR13/14历史状态不回写现行合同。两轴review无新增阻塞，准备最后工单集成。

本批五张PR已全部合并，main快进后65项接受路径一致，open队列实查0。真实head/merge与验证范围见[清理复验](docs/engineering/reports/2026-10-05-pr-cleanup-verification.md)。仅记录归档集成事实，源码/生产素材不变；待最终文档索引同步。
