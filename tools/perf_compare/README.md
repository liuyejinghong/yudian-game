# tools/perf_compare · PERF-01-COMPARE 多轮性能证据比较器

离线只读工具：把多轮 Recorder 基准证据按 canonical 身份分组，逐轮复算统计并生成一份比较报告。
不合并原始帧、不剔除启动尖刺、不输出稳定/达标结论、不跨身份组比较。

## 用法

```
python3 tools/perf_compare/compare.py --runs RUN_DIR [RUN_DIR ...] --output NEW_REPORT.json
```

- 每个 RUN_DIR 必须含 `frames.csv`、`summary.json`、`verification.json`；相对路径相对仓库根
  （由本脚本位置定位，与 cwd 无关），绝对路径照常。`--output` 同样相对仓库根。
- 重复 `--runs` 会累加（不会静默覆盖）。
- 统计复算通过仓库根 `tools/measure_baseline.py` 的 `recompute()`，本工具不复制其算法。
- 报告与 stderr 中的路径只保留仓库相对定位或文件名，不泄露用户主目录绝对路径。

## 每轮校验（任一失败：stderr 报具体 path/field，exit 2，不写输出）

- frames.csv：恰四列 `frame,time_s,frame_ms,fps`；frame 从 1 连续；frame_ms/fps 有限且为正；
  time_s 有限且严格递增。
- summary.json：`schema_version=1`、`status=completed`、非空唯一 `run_id`；`frames` 与 CSV 一致；
  `duration_actual_s` 与 CSV 最后 time_s 相差 ≤1e-5s；`avg_fps` 与复算差 ≤max(1e-4, |值|×1e-6)；
  `frame_time_ms` 的 p50/p95/p99/max 与复算差 ≤1e-5ms，`over_33ms` 一致。
- verification.json：`exit_code=0`；`recomputed.csv_sha256` 与 frames.csv 实际 SHA256 一致；
  `recomputed` 主要统计一致；`memory.maximum_resident_set_size_bytes` 与
  `memory.peak_memory_footprint_bytes` 为正整数。实际进程内存只取自 verification；
  summary.memory（缺证占位字符串）永不作为内存指标。
- 图形门禁：`graphical_performance_eligible=true`、`observed.headless=false`、
  `rendering_device=true`、`observed.rendering_method/rendering_driver/gpu/gpu_api_version`
  非空且非 `NOT_AVAILABLE`。
- 构建身份五字段非空：`summary.build.git_head`、`summary.build.source_snapshot_sha256`、
  `summary.build.assets_snapshot_sha256`、`summary.assembly_sha256`、
  `verification.app_binary_sha256`。身份只来自这些记录字段，不从目录名猜。
- 多轮之间 `run_id` 重复 → 整体拒绝。

## 身份分组

canonical 身份由以下精确字段组成（JSON sort_keys 后 sha256 → `grp-<hash>`）：
构建身份五字段、`summary.fixture_sha256/seed`、`summary.duration_requested_s`、
`summary.requested`（完整）、`summary.applied`（完整）、`summary.observed` 的
rendering_method/rendering_driver/gpu/gpu_api_version/window_pixels/viewport_size/robots/facilities、
`summary.godot_version`、`summary.runtime` 的 framework/version/architecture/os。

不同身份合法但各自成组；报告 groups 只给组内 p50/p95/p99/max/avg_fps 的 min/max/median 与样本数。
同身份不等于前台/后台、温度、电源受控（现有证据没有这些记录），报告 limitations 明确这一点。

## 输出

`schema_version=1`；`runs[]` 含 run_id、三个输入文件 SHA256、group_id、recompute() 全部指标、
两个外部内存指标；`groups[]` 含 identity、run_ids、组内指标统计；`limitations[]` 为固定声明。
输入只读，输出必须是不存在的新文件（不覆盖，独占创建）。

退出码：0 成功；2 无效输入/输出已存在（argparse 用法错误也为 2）。

## 测试

```
python3 -m unittest discover -s tools/perf_compare -p 'test_*.py' -v
```

与 cwd 无关。人工 fixture 含已知 nearest-rank 小样（手算期望）；真实三轮金样测试
（单组、max=1053.338/73.745/77.351）在证据目录缺失时自动 skip。全部负例
（headless、缺字段、CSV/summary 矛盾、哈希错误、重复 run_id、非法数值、输出覆盖等）在
`test_compare.py`。

## 限制

- 只接受离线工具；PERF-01 受控实机采样、原因裁决与稳定性能仍未完成。
- 负载为渲染灰盒：固定巡逻与动画，无 AI/寻路/存档/动态地形（summary.load_scope）。
- 内存为 `/usr/bin/time -l` 外部测量，含进程整个生命周期（含启动），两个指标相互独立。
