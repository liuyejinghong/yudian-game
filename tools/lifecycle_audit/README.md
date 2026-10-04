# tools/lifecycle_audit — LIFE-01-AUDIT 运行产物与退出证据审计器

离线审计既有生命周期证据（frames CSV / summary JSON / 退出记录）。只用 Python
标准库 + 复用 `tools/measure_baseline.py` 的 `recompute()`（按仓库布局定位导入，
不复制统计算法）。不启动引擎/GUI、不杀进程、不联网、不覆盖任何已存在文件。

## 用法

```sh
python3 tools/lifecycle_audit/audit.py --manifest MANIFEST.json --output REPORT.json
python3 -m unittest discover -s tools/lifecycle_audit -p 'test_*.py' -v
```

- 退出码：overall PASS → 0；INCOMPLETE/FAIL → 1；manifest 结构无效或输出已存在
  （含任何符号链接，悬空也算）→ 2（stderr 具体错误，不写报告、不覆盖原记录）。
  输出以独占创建（O_CREAT|O_EXCL）落盘，检查即创建，无覆盖竞态。
- 相对路径一律按 manifest 所在目录解析（含相对 `--output`），与 cwd 无关。
- 报告不含输入绝对路径，只保留 label / 字段名 / SHA256 / 数值。

## manifest（schema_version=1）

```json
{"schema_version": 1, "runs": [
  {"label": "…", "frames_csv": "…", "summary_json": "…", "exit_record": "…",
   "exit_case": "…（可选）"}]}
```

- `exit_case` 有值：`exit_record` 为 results.json，按其 `cases[].case` 精确唯一
  匹配取 `exit`（0 匹配=缺退出记录，>1 匹配=拒绝 FAIL）。
- 无 `exit_case`：`exit_record` 必须含整数 `exit_code`（布尔不算整数）。
- 缺文件保留为缺证（INCOMPLETE），不找近似名补齐。

## 判定（FAIL 优先于 INCOMPLETE；全部 PASS 才 overall PASS）

- FAIL：completed 配 exit≠0、interrupted 配 exit≠130、其他/未知 status、
  帧数/时长/avg_fps/分位/over_33ms 与独立复算矛盾、行断裂、非法数值（NaN/非正）、
  坏 JSON/类型错、布尔当整数、exit_case 重复匹配、run_id 全批重复、summary 必需
  统计字段缺失/类型错/非有限（NaN、inf、超出浮点范围的整数也判非有限；与 CSV
  完整性无关，可独立判定）、复算统计非有限（如 frame_ms 极小使 avg_fps=inf，
  按 `frames_csv.recompute.*` 字段 FAIL，不以无限容差放过矛盾值）。数值判定一律
  不抛异常，诊断不含绝对路径；报告以 ASCII 转义写出，任意字符串数据（含孤立
  代理项）不致编码失败。
- INCOMPLETE：缺 summary/CSV/exit 证据、零帧、末行截断（末尾无换行且末行不足
  四列；完整四列无末尾换行仍可有效）。截断只校验此前完整行并跳过依赖完整 CSV 的
  汇总数值比较（summary 必需字段的独立类型/有限性判定仍执行）；exit/status 矛盾
  与重复 run_id 仍 FAIL。零帧摘要允许缺 `frame_time_ms`（真实 producer 仅
  Frames>0 时写该字段；已存在时类型错/非有限仍 FAIL）。
- 容差：时长 1e-5 s；avg_fps max(1e-4, |复算|×1e-6)；p50/p95/p99/max 1e-5 ms；
  over_33ms 精确。

## 边界

PASS 只代表产物一致且有相符退出证据，不代表 GUI 关闭、无输出覆盖、存档恢复或
权限检查已实测——这些归主控实机验证。`fixtures/lifecycle-r2-manifest.json` 为
独立金样，相对引用 `docs/engineering/evidence/2026-10-04-takeover/lifecycle-r2/`
的 default-1、default-2、interrupted 三对 CSV/summary 与 results.json（exit_case
对应 case 字段），三项 PASS 且 run_id 互异。
