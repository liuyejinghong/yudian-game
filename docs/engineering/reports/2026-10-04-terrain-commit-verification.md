# T3d-MEM / COMMIT-POLICY · 限定技术验收

2026-10-04，Codex主控。接受单区域、单写者的内存权威提交与纯判定子件：候选提交前重验完整base，成功才推进版本；无变化/拒绝不消费请求，成功请求重放返回原AppliedVersion及当前Snapshot。取消不会撤回已提交结果。没有接Main、渲染/碰撞/导航、资源结算或存档，完整T3d仍未完成。

## 交付与验证

冻结base `6554495316cf31ad24a9ee4f6703a306d5a2396b`。architect审查合同无阻塞。Bridge原生确认GLM-5.3-Flash（task_1ca536ef5a），实现sess_7697b4c358/task_76fc5bf2a2，原提交 `bf6da0a575a5ab7d740310f61b9bedceede301b3`，cherry-pick `26cf2a4`。工人干净工作树、恰一提交、仅Validation与policy自测六文件，主控核对实际diff。Bridge的协调根目录变动名单还包含同期美术工作树；不以该名单归因工人越界，提交差异才是本包归属证据。

- GLM：37例×InvariantCulture/de-DE，共74检查PASS，双TFM离线编译通过。主控读取全部实现、自测与原日志；没有重跑该全套。
- Codex：独立权威入口28例PASS，实际运行.NET10.0.12；金样全部12高度/布局/version10、旧快照、先提交后旧base拒绝、新base到11、取消保留、语义重放/冲突、权限恢复重试、NoChange/版本上限、参数及只读面均验证。
- 主控独立工程net8/net10编译及实际Godot.NET.Sdk4.7.2/net8项目编译均PASS，0警告0错误；全程空本地NuGet源，无联网/安装。
- net8执行尝试exit150，缺8.0运行时，不计执行PASS。首轮主控沙盒多进程build等待，仅net8产物；停止已定位的自有编译进程后首次net10调用缺DLL/exit1，失败日志保留。单进程离线重试双TFM完整编译及执行通过，不把首轮当成功。
- 关键状态reviewer只读两轮审查：World预审及Policy集成补审均无剩余问题；最终architect另记于PLAN。

[证据/源码SHA/回执](../evidence/2026-10-04-terrain-commit/results.json)。公开日志仅替换私有路径，原日志保留在本机；不覆盖原交付。Bridge耗时566秒、usage.used=61246为工具读数，准确账单/token费用UNKNOWN；observed_model=null，模型依据原生确认，未声称使用了内部子代理。会话已结束。

## 实机与美术联调留项

[实机合同r1](../contracts/runtime-controlled-r1.md)已冻结固定历史包（源7341ea8）与三次LIFE/六次PERF用例，包签名和binary/PCK SHA已预检。它不能代表本轮新地形模块性能。

本轮实际GUI关窗/移动发行包重开、受控PERF均NOT_RUN：已向美术会话协调，两次询问时段，其仍在同机导入/截图与样件制作，尚无可用窗口回执。第一次Cua全局读取超时且无GUI操作；不是关窗证据或权限拒绝。原生供电读取为AC、热压力无告警，温度NOT_MEASURED；不据此宣称温度受控或稳定预算。用例/一次性采集准备留在主控工作目录，下一窗口继续。

正式U01动作包装/接受commit尚未交回；T01/T02正式地形素材也未交付，ART-LINK仍按权威TODO保留。内存回执不代表美术、物理或导航同步；成功去重只在对象生存期有效，后续恢复/保留策略另冻结。

PR [#30](https://github.com/liuyejinghong/yudian-game/pull/30)于2026-10-04T14:55:28Z合并，head1d864446889081d430534b2afb11f0816c20390f，merge2001aa0ded25217c978da1a1836b7335b9152724。检查列表为空，接受依据为上述实际证据。main快进后12项源码/工程SHA一致；原GLM工作树/commit/日志保留。
