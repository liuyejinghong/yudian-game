# tutorial-r2 批1 · 工程验收记录

2026-10-14｜进行中。实施/验证pass分开，以下为主控实际独立执行的结果；实施者自测不代替本表。原生图形、真实输入与D001无提示真人轨尚未运行。候选未固定前不作为交付完成记录。

## 已实际执行

| 检查 | 实际输出 | 范围/限制 |
|---|---|---|
| ledger/planner首轮 | `MATERIAL_LEDGER_PASS checks=39`；`PRODUCTION_PLAN_PASS checks=92` | 固定SDK10.0.401、net8 console以`DOTNET_ROLL_FORWARD=Major`用本机CLR10.0.12运行；v1测试按其真实初值，不拿v2当v1 |
| 目录非法输入修正后 | `MATERIAL_LEDGER_PASS checks=44` | null条目前置拒绝，配置单产出/唯一产出与planner对齐；玩家元数据去内部矿点ID；没有新增实际物资或kit配方 |
| navigation | 两种culture共92项PASS，`ALL CHECKS PASSED` | 原导航/坡角/碰撞未改；不代B002原目标恢复 |
| 构建工具首轮 | `Ran 18 tests ... OK`，exit0 | 模板指纹/版本负例、缓存保护、PNG/UID完整指纹、preset失败/成功恢复；真实导出另验 |
| 真实安装模板提取首轮 | `REAL_TEMPLATE_THIN_PASS`，安装模板hash保持不变 | 有debug/release已有arm64被重复追加警告，原输出保留；该结果不作最终无警告模板通过 |
| 模板修正后 | `Ran 21 tests ... OK`；`REAL_TEMPLATE_UNIQUE_PASS entries=12 warnings=0 installed_unchanged=true` | 主控独立重验；副本SHA256 `e5a13245d8b42af1f6072e1b3f3fd489a4d3c87e29b044acbf0f38db3f3d9841`，已有arm64与thin结果一致、无重复entry |
| 私有验收入口局部 | `Ran 6 tests ... OK` | 路径/覆盖/正式槽/链接与尺寸映射边界；本批新候选克隆/启动仍另验 |
| 私有入口真实旧包副本 | prepare exit0、codesign strict valid、arm64通过；193个比较文件中无内容改变 | 原r1包只读复制；新wrapper替代旧启动器、外层plist/签名改变，engine/PCK/Resources同字节；没有启动，不签后台/输入通过 |
| 原内容保护 | 原美术资源121/121同字节；原v1/v2配置、project.godot、export preset、Main.Navigation五项同基线 | 私有原始逐项指纹保留；配置原数值/hash未改 |
| 教学火星地景静态 | `tutorial-mars-r1 landmark static verification — 46/46 PASS` | 原地景加载/范围/资产合同保护；静态几何与源检查，不证明本批新HUD视觉或性能 |
| 旧包保护 | r1 PCK仍为`8cf854790304c9ceaa7e0901c00fd13082df9e04c3a53de0676dea889346ca86` | 旧包未覆盖；默认入口与正式槽不作为本次测试入口 |
| 集成编译 | 第2/3轮及局部返工第4/5轮均`Build succeeded`，0 Warning(s)，0 Error(s)，exit0 | 新ID/轮次DTO、确认焦点和当前测试调度已可编译；仍须最终布局修改后固定源复验 |
| Godot资源导入 | first_scan_filesystem / update_scripts_classes / loading_editor_layout DONE，exit0；无ERROR/异常匹配 | headless资源导入，新增UID生成；不代原生输入或视觉通过 |
| R2专用权威合同重计划后 | `R2_AUTHORITY PASS detached definitions objects inventory reservations batch_expected progress management_user_false_true timers world_intent epoch stale_cancel double_submit stable_build_id reservation_task_goal legacy_paused_waiting failed_load_revision overwrite_confirmation load_home_newgame natural_night schema_hash`，exit0、ERROR=0 | 第3轮按版本/阶段深核，实际运行完整自然链及门禁/恢复；原两超时保留，不改变模拟步长、资源或导航 |
| 最终源R2复验 | 同上22项 `R2_AUTHORITY PASS`，exit0、ERROR=0（controller-authority-05） | build-07最终源DLL重跑（Main.Player/Development/Tests晚于三轮DLL有改动）。authority-04为调用侧漏带`DOTNET_ROOT`致hostfxr缺失的启动失败，非合同失败，原件保留 |
| 最终源编译与UI终轮 | `Build succeeded` 0 warning/0 error（build-07）；`PLAYER_UI_SELFTEST summary checks=1100 failures=0 blocked=0` exit0、ERROR=0（controller-ui-03，主控独立复现，非worker scratch） | 前两轮187/762项失败全部消除且断言扩展未删（检查数1100>762）；37处dummy截图CAPTURE NOT_RUN如实保留 |
| 原教学生命周期回归 | `TUTORIAL_BOUNDARIES PASS menu_pause settings duplicate_fields projection_rollback save_failure newgame_reset double_submit close_confirmation detached_dto`；`TUTORIAL_WORLD_TESTS PASS processes=1`，exit0、ERROR=0 | 真实Main工程驱动，含投影故障回滚与实际读回，不代玩家键鼠 |
| 完整缺料链与冷载 | `TUTORIAL_CANCEL_LINEAGE PASS cancelled=goal-8 completed=goal-9 phase=first-direction mines=4 recipes=6`；`TUTORIAL_PREPARED PASS ... time=960.0`；`TUTORIAL_FULL PASS direction=storage natural_health=true kits=8 mines=12 recipes=18 time=2489.6`；`TUTORIAL_WORLD_TESTS PASS processes=2` | prepare/resume两进程均exit0、ERROR=0；自然真实采矿/加工/运输/整地/施工、取消改选回执、冷载和后续接线。工程驱动不代玩家无提示体验 |
| 文档/补丁 | verify_documents.py PASS；git diff --check无错误 | 仅文档/格式，不代游戏结果 |

所有原始日志在本次任务私有目录；公开仓库只保摘要，不提交玩家档或私人路径。

## 诊断与局部返工

- 首轮运行中的B工作流被消息续接产生额外上下文，Main.Player.cs出现写入冲突。主控停止首轮实施工作流和续接，保有效DTO/局部代码，重新派指定唯一writer；后续不向运行workflow发送续接消息。详PLAN.md执行重计划。
- 目录验证先做成员查询再null检查会抛NullReferenceException；已修为前置非法条目拒绝并补反例，主控重跑44项PASS。
- 已安装官方同版macos.zip包含arm64条目；原提取逻辑复制全部后再append导致重复entry。主控核原arm64与thin字节相同，修为校验已有、缺失才追加；主控21项合同与真实无警告副本已通过，原失败副本保留。
- 当前源码中途整合编译实际失败：0 warning/3 errors，全部在PlayerUiSelfTest.cs（整数使用`!`，新版PlayerObjectDetailView需从Robot/Mine嵌套值取State/Remaining）。原失败日志保留；HUD实施中局部修正后第2轮及实施结束第3轮均0 warning/0 error。中途编译不作原生验收。
- R2 authority首轮600秒墙钟超时，exit124；最后step16/time约479秒仍在首套接线送料，没有合同失败或ERROR日志。第二轮1800秒仍超时124、ERROR0，推进到step32/time718.4秒运输。两次未完成后停止延长时限盲试，按intentVersion/阶段变化与定期兜底调度昂贵DTO/规划/深复制验证；真实模拟步长/物料/健康/路线不改。两轮原件保留，不按超时推游戏死锁。
- 独立Sol审已确认管理遮罩、退出后的临停同步、首次建设误走整地、失败读取重用草案、兼容建设ID、预约关联、旧暂停CargoWaiting理由、目标卡、诊断/警报及两处口径问题；integration-fixes.md固定局部返工。HUD已落地，权威返工因502中断无结果后重新派Sol；第4轮跨接口编译已0 warning/0 error。真实UI第二轮新增取消确认独占、失败load尝试轮次等子项已通过，但整体762项/17失败/1阻断exit1、ERROR0；4项为首次空蓝图测试放在已选蓝图之后，13项为建设卡内容堆叠挤出主按钮/收益。两轮失败后按ui-layout-replan.md重规划固定操作区与内部成本滚动，原失败保留，不删断言或改BLOCKED。

## 尚未执行与审查限制

最终验证链已闭合：build-07 编译0/0 → UI终轮1100项0失败0阻断 → R2最终源22项PASS。两轮UI失败按ui-layout-replan.md重计划后消除，断言未删。

审查限制（如实记录）：独立Sol增量复核两次未能完成——首次API 502中断无报告；续接尝试因会话压缩丢失agent派发通道失败。改由主控以独立verifier角色（实施均由owner agent完成，角色分离成立）按review-delta-checklist 10项抽查代审：兼容ID/revision递增/CargoWaiting投影/管理门禁/测试调度无注入/布局固定区与内滚动/null蓝图顺序/revision消费/几何断言扩展在位/无fakeSelected，未发现新缺陷。该复核深度不及Sol全量审，作为残余风险保留，建议后续会话补一次Sol增量复核。

原生多尺寸/缩放、真实鼠标键盘、后台computer use、B002正常UI复现与同目标恢复：此为2026-10-14基线交付当时的NOT_RUN记录，后续run01/run02与下节分别补证，不覆盖旧结论。D001真人无提示G001：NOT_RUN，仍为确认P1。

## 2026-10-15 · 原生发现修复轮

本轮按所有者交回run02优先修N003/N004并保留N001/N002，详[native-fixes.md](native-fixes.md)。原正式台账不变，新增建议独立登记。原生已证首套保障/缺料经营/教学8/8/同世界冷载成果保留，但含取消换址，不签全量通过。

| 检查 | 主控实际结果 | 范围 |
|---|---|---|
| 原生根因复现 | 独立根正常UI加载E248与E140原字节、原目标重试；四卸货端点终点接网失败，具名障碍/地形拒因；两实例正常退出、ERROR0 | 诊断包，非修复恢复PASS；未改原档 |
| 集成编译与导入 | fixed-build03：0警告0错误；import exit0、ERROR0 | 中途UI新增测试float/int错误原件保留，实施者已修 |
| 导航与工位 | 100项PASS、ALL CHECKS PASSED | 原四轴优先、有限确定候选、float半径上限、严格路由保持 |
| HUD终轮 | 主控独立1130 checks / 0 failures / 0 blocked、exit0、ERROR0 | 补活动接线列表末项可达、关联详情直接关闭再重开、取消确认/去重；dummy截图不代原生 |
| 通用R2权威 | R2_AUTHORITY完整PASS、exit0、ERROR0 | 保存修正前同物流运行版，专门新增站保存由下项另验，不偷换覆盖 |
| 新工位专项（主控独立） | N003、B002各自原goal/build/center/yaw保持、完成；各运输中与完成后公共保存/恢复PASS；取消路径运输中与取消后保存恢复PASS，三轮ERROR0 | 隔离真实Main工程驱动，新增诊断harness仅测读取/合法公共命令与detached非法存档反例；不是原生输入 |
| 取消事实 | 实际before Delivering→Cancelled，Containers/Operations/Mining/Recipes/Terrain全等；cable2/kit1预约两行释放 | 不要求Reservations字段全等；原货未瞬返；再利用新目标原生未跑 |
| 独立审查 | Sol发现新增站保存旧四轴校验P1及列表预算P2，均局部返工，增量复审未发现同合同新阻塞 | 取消ID/版本/epoch、保存阶段兼容与原strictroute边界保留 |
| 固定候选 | 源4f311e6、clean、304raw逐项一致、arm64/严格签名、内嵌CLR8.0.31；导出ERROR0 | PCK f28a71696ad982160b95071b48983c58b18afab8e68673d6160e29007777c267 |
| 新包原生尝试 | 启动到主页；CUA两次timeout/cgWindowNotFound，未载档，终止自有PID；原种子不变 | BLOCKED；终止退出日志34条Godot清理/渲染ERROR，保原件，不签无ERROR |
| run-02主控原生复验 | N003 goal-9/build-9与B002 goal-9/build-8原字节现场正常UI续行，同ID/中心/朝向完成、设施Built=true；各运输保存→冷载续行→Completed→完成后冷载，Terrain保持；E001–E012六组退出日志全ERROR0、进程退出 | 所有者新授权主控亲自执行；run-02独立于run-01，旧工具阻断与34ERROR历史保留；仅签原目标恢复与正常保存冷载，不代N001/N002/N004、取消/再利用、精确尺寸与A/B |
| run-02 N004关联取消 | 原E350正常UI载入，真实建设goal-1/build-1至运输阶段；物资预约查看关联→取消点名确认，Esc仅退确认层、关联直接Close再重开无旧详情（截图留证）；确认后真实取消E013→E014同Time、Delivering→Cancelled，Terrain与Ledger四字段全等；E015退出ERROR0 | 只签N004原生行为与该次普通工程取消事实；不代N001接线（初启动工具超时调查中）与N002原生 |
| run-02 取消保货/保地形/预约释放 | 原E248初Blocked预约已释放（非有预约样本）；手动暂停重试至Fetching真实预约cable2/kit1（E017）→物资预约详情取消确认→E018；E019判据time/terrain/Containers/Operations/Mining/Recipes全等、预约两行→[]、货舱铁4/结构4保持；E020退出ERROR0 | 取消事实与预约释放原生签认；货物再利用仍未跑，不代签 |
| run-02 N001接线原生反例 | 普通CoreAudio两次启动（26421/35903）主线程样本均停于AudioComponentInstanceNew/AudioDeviceGetProperty/HALC_ProxyObject同步IPC，未进场景、日志0字节、captures空：BLOCKED，不判游戏逻辑；仅新私有clone加官方 `--audio-driver Dummy`（Metal/正常CUA/同源4f311e6与PCK不变，audio不在PASS）正常UI 1280×800/UI150%：确认/关闭可见（E027），滚动末端最后pair被裁（E028），点选末pair更长拒因后列表高度0（E029），E030活动态正常保存、E031退出ERROR0 | N001本轮FAIL返修中；headless1130 PASS与AX点击可达不代完整可读；普通启动不签 |
| run-02 货物再利用与保存失败 | 正常载入E248→暂停取消→保存E032（旧valid档槽与其字节完全相同）；合法新storage约(-5.3,10.8)提交，旧货真实退仓（仓储铁4/结构4出现），新build-10 storage Completed（E033截图）；完成后normal save失败（E034），拒因实有记录：E035第39行 `PLAYER_COMMAND_REJECTED`“采矿任务阶段或工位无效”（此前搜索漏关键字）；不保存正常退出E035 ERROR0 | 再利用链事实成立但无持久化对比，整链不签PASS；reuse-save建议P1（不改正式P0/P1/P2账）；根因已核定：Main.DevelopmentSave.cs:123历史Completed mine AtDock用当前terrain高度、整平v13→14差0.216343477m、throw早于写文件，非磁盘/权限；Sol按冻结修法实施中（仅Completed mine免地表高度相等，其余校验/严格性/schema/旧档/模拟不变）；不改已过的物流/取消结论 |
| run-03-controller 工程验收（reuse-save修复） | 最小修复commit `e080363`（Main.DevelopmentSave唯一7+3 diff，带决策trailers）后主控独立复跑 `run_fixed.py --run-name run-03-controller` exit0；亲读stdout 39–83：28 cases、公共保存/载入/保存PASS、snapshot全等、slot3与.bak字节相等、ERROR0/command_rejected0 | 仅Completed mine免当前地表高度相等的隔离工程验收；不代原生，待HUD完成后导新候选原生复验 |
| run-02 接线专用合同与full终轮（N001修法） | 修法commit `262033d`（Hud.cs+SelfTest）；主控独立编译0警告0错误；专用合同conn log 39/0/0、ERROR0：8pairs、longList98px、last59px、scrollMax500、inListTrue、短/长list 98/98；full终轮主控亲读1130/0/0、ERROR0、后台exit0 | 首次主控测试传归档路径被私有root门拒（ERROR保原件、已停进程），原字节复制允许根后完成；Sol静态复审无可证新增阻断，首帧/多pair三倍率/长reason内滚待新包原生优先补100/125/150；工程/合同数字不代完整可读，N001原生复验未过前仍记FAIL；红例原件（主控白名单/root门调用失败、writer layout旧红例、E030原备份同样486/1）全保留 |
| run-02 较早生产起点 | 自然从夜加工等待推进到采集/运输/施工：E039 goal-9/build-9 Completed、原中心/yaw0、Built=true、Time 6910.266666650256（约6558.43→6910.27），新增mining1/recipes4、configHash与基线同；正常保存成功、E040退出ERROR0 | PASS（主控签认）：自然生产→运输→施工完成/保存；无单独冷载轨，同目标冷载证在N003恢复轨另列；音频Dummy |
| run-02 精确大尺寸 | E041/E042请求1920×1080/1920×1200均被钳制到逻辑1860×889（像素3720×1778、scale2、dpi163）；主页与设置三倍率实操真实执行 | BLOCKED：钳制后游戏布局未测不签；系统display设置未改 |
| run-02 A/B独立根 | 两根原seed hash同cb347b8a。A组（直接solar）：E044 goal-9/build-8 Completed、center(12.505365,0,-11.181097)/yaw0、Time 2045.9166666651347、正常保存成功，E045退出ERROR0；B组（先整平再solar）：E046手动整平Completed并保存（center(12.52281,0,-11.197887)、terrain v5、“整平已提交，物理验证完成”），E047 goal-9/build-8 Completed/Built=true、center(12.43391,0,-11.311815)/yaw0、Time 2210.4500000005346、正常保存成功，E048退出ERROR0 | 两流程PASS（音频Dummy）；不作严格同点同相效率比较 |

原目标恢复与正常保存冷载（N003、B002）、N004关联取消与取消保货/保地形/预约释放、较早生产起点自然生产→运输→施工完成/保存、A/B之A组直接建设/保存已由主控run-02原生PASS（详[native-fixes.md](native-fixes.md)）；b002-active已正常unpause（无需retry）至Completed并正常保存E036、E037退出ERROR0（Metal+Dummy，非普通音频；冷载未取不另签）；N001接线原生与reuse-save原生仍FAIL待修后新包复验（工程侧262033d/e080363均已过主控独立验证：专用合同39/0/0+full1130/0/0 ERROR0、run-03-controller 28cases全PASS；两commit尚未push，只三raw源码相较4f311e6改变、其余301个raw含配置/美术/导航相同）；N002待最终图形复验；精确1920×1080/1920×1200实测钳制到1860×889记BLOCKED（不签游戏布局）；A/B两独立根两流程PASS（E044–E048，不作严格同点同相效率比较）；新候选待导出，新入口和自包含Codex复验prompt已准备；所有红例原件保留不改写。

## 本轮最终状态（2026-10-15收束）

上文各行与失败原件为历史记录，本节为当前总口径；状态表与关键证据见 [native-fixes.md](native-fixes.md)“本轮最终状态”。要点：run-02旧候选（4f311e6）原生PASS保留（N003/B002恢复与冷载、N004/取消、较早生产、A/B）；旧候选N001与reuse-save FAIL保留不删。新修复 `e080363`/`262033d` 工程全过（run-03-controller 28cases、专用合同39/0/0、full 1130/0/0、ERROR0）。新候选 `4bd52ff` 已导出并push成功（PCK `d86046fb…`、DLL `01bb1f9e…`、304raw match、strict/arm64、CLR8.0.31、导出ERROR0；首次TLS失败保史实；后续纯文档提交不改候选身份）。新包N001/reuse原生BLOCKED（connection两次启动、reuse一次启动，均到home未载档、0输入；三次终止各34条清理ERROR保；工具尝试path timeout/bundle cg/jsreset/exact bundle/唯一running name/fresh same bundle均cg，全局未分错case；系统AX/screen权限granted，不归因用户权限或新修死锁，CUA枚举原因未证）；普通CoreAudio启动BLOCKED独立记录；N002 NOT_RUN、精确尺寸BLOCKED、D001/G001 NOT_RUN、旧P1台账不改。run-03三入口种子SHA实测不变；任务停在可证阻断，不改服务/信任/OS；证据index由主控生成。D001真人无提示轨继续NOT_RUN。首次推送与API均遇TLS连接失败，网络恢复后已正常推送修复分支并开draft PR [#63](https://github.com/liuyejinghong/yudian-game/pull/63)，原失败保留。当前新候选固定4bd52ff，旧run02候选为4f311e6；后续纯文档不改新包身份；未合并或推main。