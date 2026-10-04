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
2. [ ] GLM独立实现静态shape适配与真实物理引擎检查。证明：原生GLM确认、限定commit、Debug/loadedMVID/native ray/headless退出与失败记录。
3. [ ] 主控增加可选碰撞候选模式/队列/独立分帧检查及GUI，和美术组按接受commit固定M01联调；缺正式素材留工程TODO。证明：显示与body/shape同源、三排队序列/失败保持/释放/真实射线/材质逐surface与拒绝/PNG。
4. [ ] reviewer/最终architect审查，更新TODO/证据，发布和核对精确head后合并；干净原main快进。证明：实际源/素材/PNG哈希、PR/Issue状态，完整T3保持未完成。

architect明确早退不能丢弃撤销pending，同帧非法最新请求要清掉先前候选；四属性赋值不证明native同步，下一物理帧核对象/位置/法线；非顶点斜坡及shape独立寿命、明确mask/单面；材质等待其主控接受，不能抢写目录。本合同已纳入。
