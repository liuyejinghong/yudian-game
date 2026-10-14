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

原生多尺寸/缩放、真实鼠标键盘、后台computer use、B002正常UI复现与同目标恢复：NOT_RUN，由Codex原生协议承担。D001真人无提示G001：NOT_RUN，仍为确认P1。