# PERF/LIFE · 实机用例 r1

2026-10-04，Codex执行。复用已验收比较器/审计器，不改Recorder/灰模fixture。固定现有只读移动导出包：binary SHA256 `1f8594bfcebb09f30ea10ba72d6aa169e6dcdf5154ff43d37edd4c9ef2684bb9`、PCK `5b6eaf3bda9591450f79e26ed43ed4163379412dad76a1b459f27b8ab1c939e7`。同已测7341ea8源包，不是本批main新地形模块的导出；签名实核有效，实际runtime另从summary验证，runtimeconfig为.NET8.0.31/arm64。

主控与美术组预约同机窗口；美术GUI/截图/编译与本批实机采样错开。GLM本机编译也不得混入采样区间。包/原始证据/既有user目录不覆盖，输出均为新run-id或新目录。记录AC供电、OS热压力诊断、实际前台进程；没有温度传感读数就写NOT_MEASURED，不把“无告警”说成温度受控。

LIFE：同包直接Popen，设置DOTNET_ROOT/ARM64不存在，以保留包自带runtime；外部捕获真实exit，不用拒绝非零exit的measure_baseline。先一次短正常运行completed/0，再一次duration120秒的GUI运行，在超时前真实点击自有窗口关闭按钮，必须interrupted/130；再直接重开同一包短正常运行completed/0。至少两次使用实际默认user://输出，证明新ID/新目录、旧产物和包SHA不变。关闭不以--quit-after/信号/杀进程替代；硬杀只有超时清理本次自有进程，失败保留而不签GUI。审计器复算清单三项；只签进程/产物/重开，不称游戏存档恢复或已公证商业发布。

PERF：同包/fixture/seed/画质/duration45秒，六轮顺序前台1、后台1、后台2、前台2、前台3、后台3。前台为实际Yudian进程，后台为主控Codex窗口；每轮独立进程、原始CSV/summary/内存time原文和verification。所有启动帧与尖刺保留，前台/后台分别调用perf_compare生成组内报告；不同身份不得算提升。每轮启动/退出及运行期间记录前台状态，外部时间轴不冒充Recorder内部原点；启动阶段切换窗口的未受控区间单列，不能签全生命周期受控。

轮间至少15秒，记录供电/热压力和本机重负载进程。条件失守的轮次保留并说明，不事后择优删帧；不足以控制时只报告观察，不签稳定预算。比较前后台三对分位/最大值与RSS/footprint，不能由单轮平均FPS推断根因。无温度实测、重复性差或没有profiler归因时，父PERF-01原因/稳定预算继续未完成。

本轮L/M/Windows/联合导航/模型/保存负载不在范围，NOT_RUN。GUI工具/权限无法可靠操作时保留具体失败并暂停对应实机步骤，继续离线合同/GLM子票；不得伪造点击或关窗证据。
