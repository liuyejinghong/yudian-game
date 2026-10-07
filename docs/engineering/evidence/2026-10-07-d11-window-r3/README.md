# D1.1 解锁后窗口验收与角点恢复修复

2026-10-07。源码 `975916deef138360114cc15c8e2fb28634c4c934`；修复包r3。公开文本仅脱敏私人机器路径为 `<PROJECT_ROOT>`、`<DOTNET_ROOT>`、`<DOWNLOADS>`、`<TMP>`，其余运行身份、失败和结论保留；原件私下保留。hash针对公开副本。真实存档仅留私下，公开只给测试档hash和必要的验收摘要，不提交存档内容。

`gui-r2-construction.log` 与 `gui-r2` 是修复前包中真实鼠标建维修站、卸下接线材料并暂停保存的证据。`gui-r2-failed-load.log`、`replay-red.log` 记录同一保存点重开后(-30,30)射线无命中、加载回滚；诊断探针 `corner-probe.log` 确认原span边界失败，而同体的另一span及微移内点命中。临时探针已从源码删除。

`replay-source-green.log` / `replay-package-green.log` 验同一档时间/暂停/阶段/物料/操作恢复；禁用真实碰撞体的反例仍被拒绝。`gui-r3` 图来自修复包真实鼠标读取、继续接线、暂停保存，`gui-r3-reopen` 是关窗后真实重开读取完成态。`default-entry.log` 与 `app-entry.json` 记录无测试环境变量的根应用正常启动、读取原schema2槽、关窗及旧存档字节不变。默认入口检查使用原生应用路径打开；本轮没有另做访达双击手势。

`package-world/runner.log` 九独立进程全PASS；各进程MVID/hash相同、CLR8.0.31。`source-full.log` 两轮保障/围挡阻塞恢复通过；`compat-player` 五进程、`level-compat.log` 15步、`ground-compat.log` 7步通过。`ground-wrong-entry.log` 保留误用玩家默认入口运行历史巡逻检查导致失败的记录；补正确 `-- --live-terrain` 后通过。受限headless命令不能写默认user日志，stdout仍完整保存；该日志写入限制不计玩法检查失败，也不隐去错误行。

可重复的公开命令：

```sh
python3 tools/bootstrap-world-tests/run.py --engine '<APP>/Contents/MacOS/Yudian' --output '<NEW_OUTPUT>'
```

真实档replay另用 `YUDIAN_BOOTSTRAP_SELF_TEST=1 YUDIAN_BOOTSTRAP_TEST_PHASE=replay YUDIAN_PLAYER_TEST_SAVE=<PRIVATE_TEST_SAVE>`、同一引擎 `--headless --fixed-fps 60`；输入未公开。测试低健康注入不证明自然平衡；本轮没有G1、真人意愿、联合性能、所有者视觉、救援、真实生产或发行验收。
