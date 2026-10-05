# 2026-10-05 · 实机复验

LIFE-01 / LIFE-01-GUI限定ACCEPTED：真实GUI关窗、默认目录不覆盖与同包重开通过。PERF-01仍IN_PROGRESS、PERF-01-CONTROLLED为REWORK：六轮采样已完成，但后台焦点失守，不能签严格受控对照、原因或稳定预算。

工程自行拉起和验收游戏测试；美术负责建模/素材，仅观察到同机负载争用时协调，不以美术回执为开测前提。未改引擎、Recorder、游戏源码、已接受工具或美术资产。

## 固定对象与方法

沿用7341ea83a3ad879ba98a496792d43eb9cd2d70ab灰模s_small历史包，seed20261004、机器人12/设施6、1920×1200，Godot4.7.2 Forward+/Metal，Apple M3 Pro，实际.NET8.0.31 Arm64；不是当前main地形模块或正式美术的性能。binary/PCK SHA见[最终检查](../evidence/2026-10-05-runtime-controlled/final-verification.json)。最终codesign deep/strict exit0、包未改、无残留Yudian。

AC供电100%、OS热压力未告警；温度NOT_MEASURED。保留观察器开销和同机其他进程负载，未停止无关应用。监测请求0.5秒，实际约0.8秒，间隙不保证焦点连续；外部Popen时间轴不当作Recorder原点，不删启动帧。

## LIFE实际结果

| 用例 | run_id | exit / status | 帧数 / CSV秒数 |
|---|---|---|---|
| 正常默认输出 | 20261005T014056135Z-30e50773 | 0 / completed | 301 / 5.008566 |
| 真实关闭自有窗口 | 20261005T014119253Z-e8f42159 | 130 / interrupted | 8916 / 70.174849 |
| 同包重开默认输出 | 20261005T014318637Z-354b853d | 0 / completed | 344 / 5.001020 |

Cua原生关闭按钮，目标自有PID21136；没有用信号/quit-after代替点击。之后AX仍见额外工具开启窗口，以独立自有进程exit为依据，并原生清理额外窗口。三个实际user://输出目录/run_id互异；每轮先前文件不变，包SHA不变。不存在DOTNET_ROOT/ARM64路径条件下实际加载包自带runtime。[动作记录](../evidence/2026-10-05-runtime-controlled/native-actions.json)与[审计PASS](../evidence/2026-10-05-runtime-controlled/life-audit-report.json)分别证明动作和产物。未测试游戏存档恢复/新系统无SDK/公证发行。

## PERF实际观察

新六轮顺序F1/B1/B2/F2/F3/B3，每轮45秒、相邻采集结束后至少15秒，独立进程。[原始轨迹与顺序](../evidence/2026-10-05-runtime-controlled/sampled/sequence.json)。

| 轮次 | 平均FPS | p50 ms | p95 ms | p99 ms | 最大 ms |
|---|---:|---:|---:|---:|---:|
| F1 | 76.697 | 13.313 | 14.357 | 21.354 | 72.528 |
| B1 | 137.470 | 6.900 | 7.630 | 13.576 | 1016.937 |
| B2 | 107.530 | 6.952 | 15.434 | 19.011 | 75.325 |
| F2 | 75.070 | 13.322 | 14.505 | 19.137 | 74.498 |
| F3 | 77.193 | 13.313 | 14.497 | 18.978 | 79.526 |
| B3 | 75.037 | 13.335 | 14.379 | 18.071 | 76.656 |

三轮前台观察约75–77FPS；后台实际前台应用在WeChat/Chrome/ZCode/Codex/QQ间切换，Finder目标未持久保持，B2/B3还观察到游戏置前区间。AppleScript返回0仅说明动作执行；[条件判定](../evidence/2026-10-05-runtime-controlled/sampled/focus-conditions.json)明确NOT_ACCEPTED。不能从这些数字推断后台改善、GPU/帧节奏原因或稳定预算。B1一秒尖刺未删除；无profiler归因。

[前台复算](../evidence/2026-10-05-runtime-controlled/foreground-compare.json) / [后台复算](../evidence/2026-10-05-runtime-controlled/background-compare.json)：各3轮、1身份组、exit0。每轮CSV/summary复算一致，RSS/footprint来自整个进程time-l实测。身份组不含焦点/负载，不把同group_id当外部条件认证。初次采集验证缺顶层app_binary_sha256，比较器拒绝；主控保留原件，透明派生适配字段，未改工具或CSV/summary。[方法与失败](../evidence/2026-10-05-runtime-controlled/README.md)。先前Cua置前/桌面点击失败两轮全留focus-failed，不参与严格受控结论。

## 验证与后续

实际执行既有lifecycle_audit一次、perf_compare两组；11组原始CSV/summary/exit/焦点轨迹及6份采集验证与本机原件逐字核对，包签名/SHA复核。仅文档与证据变更，无新增实现，未重跑无变化模块全量单测。原始本机材料保留，发布日志只替换私有路径。

下一步工程在实际固定后台目标/负载条件下补受控对照，并为尖刺做有证据的profiling；两次焦点控制失败已保留并重计划，不机械扩大采样。GLM可做离线复算/日志整理，主控负责启动、焦点条件与最终原因裁决。完整世界、导航、存档和正式美术继续按已有TODO，不扩签。

GLM-5.3-Flash经Agent Bridge完成独立标准库复算，六轮数字/适配SHA一致；[工人原报告脱敏副本](../evidence/2026-10-05-runtime-controlled/glm-crosscheck-sanitized.txt)及[主控裁决](../evidence/2026-10-05-runtime-controlled/glm-main-disposition.txt)保留。主控未接受其把后台散布归因为前台残留的表述：离散轨迹与三点关联不证明原因。B1最大帧实核为frame204/Recorder3.736257秒，启动阶段边界未定义，不据此定位根因。采样18份脱敏副本与本机原件仅路径替换核对一致；采集器本体本机保留，公共包不声称完全离线重放实机动作。

[PR31](https://github.com/liuyejinghong/yudian-game/pull/31)已合并（merge36603cdf9cd4fc593782a2f8d9350b2566ce746a）；main快进后134项本批文件与已接受head一致。GitHub检查列表为空，未声称CI通过。
