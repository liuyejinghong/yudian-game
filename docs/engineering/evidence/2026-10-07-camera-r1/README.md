# CAMERA-01 输入、UI与原生候选包证据

2026-10-07。报告及未完成出口见[镜头复验](../../reports/2026-10-07-camera-verification.md)。路径已替换为`<PROJECT_ROOT>`、`<DOTNET_ROOT>`、`<DOWNLOADS>`、`<USER_LIBRARY>`、`<USER_HOME>`、`<PYTHON_ROOT>`与`<TMP>`；原件私下保留，SHA256针对公开副本。不提交玩家或测试存档JSON。

- baseline三份日志：原UI38、镜头7和合计45，对照短按/长按与原生手势缺口。
- red日志：11项中3项按新要求失败；GLM两份green日志为23/62项通过。
- root日志：收紧水平旋转断言的62项、诊断23项及明确1280×800的最终62项；早期full含3条nullable编译警告，后两轮零警告，不隐藏早期输出。
- export日志与两份manifest：r4初次受限失败后成功；r5纳入测试尺寸修正，运行源880d04d。manifest如实保留导入产物导致的dirty字段。
- package-full-r4：原生CLR8.0.31两轮保障/7座建设/整平返回取消通过。
- package-replay-r5：未暂停测试档遇harness精确时间断言失败；package-replay-paused-r5为只将测试副本Paused设true后恢复通过，并确认禁用真实碰撞体仍拒绝。副本不代表真人存档验收。
- gui-first-frame-r4.png与gui-r4.log：仅渲染首帧；桌面锁屏，未操作，TERM停止退出134。最终r5真实鼠标验收、正常保存关窗重开和入口切换仍待完成。

可重复的源码UI命令（使用已核定的Godot 4.7.2 .NET/SDK10.0.401）：

```sh
YUDIAN_GODOT='<GODOT>' YUDIAN_DOTNET='<DOTNET>' tools/player-ui-tests/run-selftest.sh
YUDIAN_GODOT='<GODOT>' YUDIAN_DOTNET='<DOTNET>' tools/player-ui-tests/run-camera-diag.sh
```

原生replay用`YUDIAN_BOOTSTRAP_SELF_TEST=1 YUDIAN_BOOTSTRAP_TEST_PHASE=replay YUDIAN_PLAYER_TEST_SAVE=<PRIVATE_PAUSED_TEST_COPY>`，候选引擎`--headless --fixed-fps 60 --log-file <NEW_LOG>`；输入不公开。合成手势不证明物理触控板可用。
