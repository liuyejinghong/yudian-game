# PERF-01-COMPARE · 多轮性能证据比较器

状态：ACCEPTED（限定离线比较器）；冻结技术要求保留，交付/返工/验收见[复验记录](../reports/2026-10-04-perf-compare-verification.md)。

## 唯一交付

只新增`tools/perf_compare/`内的`compare.py`、标准库unittest、README及必要小型人工fixture。不修改其他任何文件（包括measure_baseline.py、Recorder、工程TODO、PLAN、Godot场景、美术）；不复制整批已有证据。不安装依赖、不运行Godot/GUI、不做网络/付费调用、不push/merge。

`python3 tools/perf_compare/compare.py --runs RUN_DIR [RUN_DIR ...] --output NEW_REPORT.json`

RUN_DIR均已有frames.csv、summary.json、verification.json，RUN_DIR相对路径一律相对仓库根（由脚本位置定位），输出相对路径也相对仓库根；绝对路径照常。输出必须新文件，不覆盖。只读输入，保留所有帧/启动尖刺。通过仓库位置导入既有`tools/measure_baseline.py`的recompute()，不要复制其统计算法。

检查CSV四列、连续frame、有限正frame_ms/fps、有限递增time_s；summary schema_version=1、status=completed、非空唯一run_id、frames与CSV一致、duration_actual_s与最后time_s相差<=1e-5s；avg_fps与复算差<=max(1e-4,abs(expected)*1e-6)，p50/p95/p99/max相差<=1e-5ms、over_33ms一致。verification.exit_code=0、recomputed.csv_sha256与实际文件一致、recomputed主要统计一致；外部内存两个byte数需正整数。禁止把summary.memory缺证字符串当实际进程内存。

只有summary.graphical_performance_eligible=true、observed.headless=false、rendering_device=true、observed.rendering_method/rendering_driver/gpu/gpu_api_version非空且不是NOT_AVAILABLE的轮次进入图形比较。headless/缺字段/坏类型/非有限值/截断CSV/同run_id/矛盾报告拒绝。构建身份要求summary.build.git_head、summary.build.source_snapshot_sha256、summary.build.assets_snapshot_sha256、summary.assembly_sha256、verification.app_binary_sha256非空；不从目录名猜身份。

按上述构建身份（前句五个精确路径）、summary.fixture_sha256/seed、duration_requested_s、requested（完整）、applied（完整）、observed.rendering_method/rendering_driver/gpu/gpu_api_version/window_pixels/viewport_size/robots/facilities、godot_version、runtime.framework/version/architecture/os组成canonical身份分组。不同身份合法但只能各自成组，不能跨组输出性能改善或单一总体指标。不能声称同身份意味着前后台/温度/电源受控，因为现有记录没有这些证据。

## 输出与验收

JSON schema_version=1，runs逐项含run_id、输入三文件SHA256、group_id、recomputed全部指标与两个外部内存指标；groups含identity、run_ids、每个p50/p95/p99/max/avg_fps的min/max/median与样本数。不混并原始帧，不删异常尖刺，不输出“稳定/达标”结论；limitations明确现有环境缺证与渲染灰盒负载边界。文件路径只保留输入名/相对定位，不泄露用户目录。成功写报告exit0；无效输入/输出已存在时stderr带具体path/field并exit2，不留下看似成功的报告；无覆盖和输入hash不变要测试。

完成命令：`python3 -m unittest discover -s tools/perf_compare -p 'test_*.py' -v`；从不同cwd运行真实三轮`docs/engineering/evidence/2026-10-04-takeover/metal-native/run-{1,2,3}`并复算对照；三轮应一组且保留max=1053.338、73.745、77.351（先读真实输入，如数字冲突报告，不改金样）。反例至少身份不同时分组、headless、字段缺失、CSV/summary矛盾、哈希错误、重复run_id、非法数值、输出覆盖。人工fixture含至少一个已知nearest-rank小样，不只测试实现自己生成的数据。

只接受离线工具；PERF-01受控实机采样、原因裁决和稳定性能仍未完成。工人交付一个限定干净commit及五行报告：范围、命令/完整日志、限制、commit、用量未知如实。不自动领取其他票。主控负责review、独立复验、TODO和合并。
