#!/usr/bin/env python3
"""LIFE-01-AUDIT · 运行产物与退出证据审计器（离线，仅 Python 标准库）。

用法:
    python3 tools/lifecycle_audit/audit.py --manifest MANIFEST.json --output REPORT.json

manifest: schema_version=1，runs 为非空列表；每项含唯一 label 与 frames_csv /
summary_json / exit_record 三个路径（非空字符串；相对路径一律按 manifest 所在
目录解析，与 cwd 无关，相对 --output 同样按 manifest 目录解析）；可选 exit_case
（字符串）。缺文件保留为缺证，不按近似名补齐。不覆盖任何已存在的输出。

退出码: overall PASS → 0；INCOMPLETE/FAIL → 1；manifest 结构无效或输出已存在 →
2（stderr 具体错误、不写报告）。统计一律复用 tools/measure_baseline.recompute()
（按仓库布局定位导入），不复制统计算法。summary 必需统计字段的结构/类型/有限性
独立检查，与完整 CSV 数值比较解耦（截断时也执行）；数值判定一律不抛异常。零帧
摘要（producer 仅 Frames>0 时写 frame_time_ms）允许缺 frame_time_ms，其余类型
检查不放宽。复算比较所依赖的统计值必须有限，非有限按 frames_csv.recompute.*
字段 FAIL。输出以 O_CREAT|O_EXCL 独占创建：已存在文件或任何符号链接（含悬空）
一律拒绝，无检查-写入竞态。诊断只含 role/field 与 basename/相对路径，不泄露调用
方绝对路径，任何输入不产生 traceback；报告以 ASCII 转义写出，孤立代理项等任意
字符串数据不致编码失败。PASS 仅代表产物一致且有相符退出证据，不代表 GUI 关闭、
无输出覆盖、存档恢复或权限检查已实测。
"""
import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path

CSV_COLUMNS = "frame,time_s,frame_ms,fps"
STATUSES = {"completed": 0, "interrupted": 130}


class CliError(Exception):
    """manifest/环境无效或输出冲突：stderr 具体错误、exit 2、不写报告。"""


def _is_int(v):
    return isinstance(v, int) and not isinstance(v, bool)


def _is_num(v):
    # 大整数（如 10**400）float() 会抛 OverflowError：判为非有限而非让检查崩溃
    if isinstance(v, bool) or not isinstance(v, (int, float)):
        return False
    try:
        return math.isfinite(float(v))
    except OverflowError:
        return False


def _display_path(p):
    """诊断里绝对路径只留 basename，相对路径原样：不泄露调用方目录。"""
    s = str(p)
    return os.path.basename(s) if os.path.isabs(s) else s


def _safe_io(e):
    """OSError → 'strerror (basename/相对路径)'，不带绝对路径。"""
    where = getattr(e, "filename", None)
    msg = getattr(e, "strerror", None) or type(e).__name__
    return f"{msg} ({_display_path(where)})" if where else msg


def load_measure_baseline():
    """按仓库布局定位 tools/measure_baseline.py 并导入（统计必须复用 recompute）。"""
    tools_dir = Path(__file__).resolve().parents[1]
    if str(tools_dir) not in sys.path:
        sys.path.insert(0, str(tools_dir))
    try:
        import measure_baseline
    except ImportError as e:
        raise CliError(f"无法导入 tools/measure_baseline.py（{e}）；拒绝复制统计算法")
    return measure_baseline


def load_manifest(manifest_path):
    try:
        text = manifest_path.read_text(encoding="utf-8")
    except OSError as e:
        raise CliError(f"manifest 读取失败: {_safe_io(e)}")
    except UnicodeDecodeError:
        raise CliError(f"manifest: 非 UTF-8 文本（{_display_path(manifest_path)}）")
    try:
        data = json.loads(text)
    except ValueError as e:
        raise CliError(f"manifest JSON 解析失败: {e}")
    if not isinstance(data, dict):
        raise CliError("manifest 顶层必须是 JSON 对象")
    if not _is_int(data.get("schema_version")) or data["schema_version"] != 1:
        raise CliError("manifest.schema_version: 必须是整数 1（布尔不算整数）")
    runs = data.get("runs")
    if not isinstance(runs, list) or not runs:
        raise CliError("manifest.runs: 必须是非空列表")
    labels = set()
    for i, run in enumerate(runs):
        where = f"manifest.runs[{i}]"
        if not isinstance(run, dict):
            raise CliError(f"{where}: 必须是对象")
        for key in ("label", "frames_csv", "summary_json", "exit_record"):
            v = run.get(key)
            if not isinstance(v, str) or not v:
                raise CliError(f"{where}.{key}: 必须是非空字符串")
        if run["label"] in labels:
            raise CliError(f"{where}.label: 重复 label {run['label']!r}")
        labels.add(run["label"])
        ec = run.get("exit_case")
        if ec is not None and (not isinstance(ec, str) or not ec):
            raise CliError(f"{where}.exit_case: 可选，必须是字符串")
    return runs


def read_frames_csv(csv_path):
    """解析 frames CSV。

    返回 dict: sha256 / rows（完整且合法的行 (line_no, time_s, frame_ms, fps)）/
    frames_read（已读完整帧行数，不含截断末行）/ truncated / reasons（FAIL）/
    incomplete（INCOMPLETE）。末行截断仅指：文件末尾无换行且末行不足四列；
    完整四列无末尾换行仍可有效；末行四列却类型非法按 FAIL，不以截断掩盖。
    """
    out = {"sha256": None, "rows": [], "frames_read": 0, "truncated": False,
           "reasons": [], "incomplete": []}
    try:
        raw = csv_path.read_bytes()
    except OSError:
        out["incomplete"].append("frames_csv: 文件缺失或不可读")
        return out
    out["sha256"] = hashlib.sha256(raw).hexdigest()
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        out["reasons"].append("frames_csv: 非 UTF-8 文本")
        return out
    if not text.strip():
        out["incomplete"].append("frames_csv: 零帧（文件为空）")
        return out
    ends_newline = text.endswith("\n")
    lines = text.split("\n")
    if ends_newline:
        lines.pop()
    lines = [ln[:-1] if ln.endswith("\r") else ln for ln in lines]
    if lines[0] != CSV_COLUMNS:
        out["reasons"].append(f"frames_csv.header: 必须是 {CSV_COLUMNS}")
        return out
    rows = lines[1:]
    if not rows:
        out["incomplete"].append("frames_csv: 零帧")
        return out
    prev_time = None
    for i, line in enumerate(rows, start=2):
        tag = f"frames_csv.line{i}"
        last = i == len(lines) and not ends_newline
        fields = line.split(",")
        if len(fields) != 4:
            if last and len(fields) < 4:
                out["truncated"] = True
                out["incomplete"].append(
                    f"{tag}: 末行截断（不足四列且文件末尾无换行），仅校验此前完整行")
            else:
                out["reasons"].append(f"{tag}: 每行必须恰好四列")
            break
        err = None
        try:
            frame = int(fields[0])
        except ValueError:
            err = f"{tag}.frame: 必须是整数"
        if err is None and frame != len(out["rows"]) + 1:
            err = f"{tag}.frame: 序号必须从 1 连续递增，实际 {fields[0]!r}"
        if err is None:
            try:
                time_s, frame_ms, fps = (float(fields[1]), float(fields[2]), float(fields[3]))
            except ValueError:
                err = f"{tag}: time_s/frame_ms/fps 必须是数值"
        if err is None and not math.isfinite(time_s):
            err = f"{tag}.time_s: 必须是有限数值"
        if err is None and prev_time is not None and not time_s > prev_time:
            err = f"{tag}.time_s: 必须严格递增"
        if err is None and (not math.isfinite(frame_ms) or frame_ms <= 0):
            err = f"{tag}.frame_ms: 必须是有限正值"
        if err is None and (not math.isfinite(fps) or fps <= 0):
            err = f"{tag}.fps: 必须是有限正值"
        if err:
            out["reasons"].append(err)
            break
        out["rows"].append((i, time_s, frame_ms, fps))
        out["frames_read"] += 1
        prev_time = time_s
    return out


def read_summary_json(path):
    out = {"sha256": None, "data": None, "reasons": [], "incomplete": []}
    try:
        raw = path.read_bytes()
    except OSError:
        out["incomplete"].append("summary_json: 文件缺失或不可读")
        return out
    out["sha256"] = hashlib.sha256(raw).hexdigest()
    try:
        data = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, ValueError) as e:
        out["reasons"].append(f"summary_json: JSON 解析失败 ({e})")
        return out
    if not isinstance(data, dict):
        out["reasons"].append("summary_json: 顶层必须是对象")
        return out
    out["data"] = data
    if not _is_int(data.get("schema_version")) or data["schema_version"] != 1:
        out["reasons"].append("summary_json.schema_version: 必须是整数 1（布尔不算整数）")
    run_id = data.get("run_id")
    if not isinstance(run_id, str) or not run_id:
        out["reasons"].append("summary_json.run_id: 必须是非空字符串")
    status = data.get("status")
    if not isinstance(status, str) or status not in STATUSES:
        out["reasons"].append(
            f"summary_json.status: 未知状态 {status!r}，必须是 completed/interrupted")
    return out


def read_exit_record(path, exit_case):
    """读取退出证据。exit_case 有值按 results.json 的 cases 精确唯一匹配取 exit；
    无值则必须含整数 exit_code（布尔不算整数，重复匹配拒绝）。"""
    out = {"sha256": None, "exit_code": None, "reasons": [], "incomplete": []}
    try:
        raw = path.read_bytes()
    except OSError:
        out["incomplete"].append("exit_record: 文件缺失或不可读")
        return out
    out["sha256"] = hashlib.sha256(raw).hexdigest()
    try:
        data = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, ValueError) as e:
        out["reasons"].append(f"exit_record: JSON 解析失败 ({e})")
        return out
    if not isinstance(data, dict):
        out["reasons"].append("exit_record: 顶层必须是对象")
        return out
    if exit_case is None:
        v = data.get("exit_code")
        if not _is_int(v):
            out["reasons"].append("exit_record.exit_code: 必须是整数（布尔不算整数）")
        else:
            out["exit_code"] = v
        return out
    cases = data.get("cases")
    if not isinstance(cases, list):
        out["reasons"].append("exit_record.cases: 必须是列表")
        return out
    matches = [c for c in cases if isinstance(c, dict) and c.get("case") == exit_case]
    if not matches:
        out["incomplete"].append(
            f"exit_record.case_not_found: cases 中无 case={exit_case!r}，缺退出记录")
    elif len(matches) > 1:
        out["reasons"].append(
            f"exit_record.exit_case: {exit_case!r} 重复匹配 {len(matches)} 个 case，拒绝")
    else:
        v = matches[0].get("exit")
        if not _is_int(v):
            out["reasons"].append("exit_record.exit: 匹配 case 的 exit 必须是整数（布尔不算整数）")
        else:
            out["exit_code"] = v
    return out


def check_stats(summary, calc, fails, compare):
    """summary 必需统计字段检查。结构/类型/有限性独立判定（NaN/超大整数/缺字段
    都在具体 field 上 FAIL，与 CSV 完整性无关）；compare=True 时（CSV 完整且
    recompute 成功）再与复算核对数值，容差按包文档。数值判定不抛异常。"""
    bad = "缺失、非数值或非有限（超出浮点范围的整数也判非有限）"
    if not _is_int(summary.get("frames")):
        fails.append("summary_json.frames: 缺失或非整数（布尔不算整数）")
    elif compare and summary["frames"] != calc["frames"]:
        fails.append(
            f"summary_json.frames: summary={summary['frames']} 复算={calc['frames']}")
    if not _is_num(summary.get("duration_actual_s")):
        fails.append(f"summary_json.duration_actual_s: {bad}")
    elif compare and abs(summary["duration_actual_s"] - calc["duration_csv_s"]) > 1e-5:
        fails.append(
            f"summary_json.duration_actual_s: summary={summary['duration_actual_s']}"
            f" 复算={calc['duration_csv_s']} (容差 1e-5s)")
    if not _is_num(summary.get("avg_fps")):
        fails.append(f"summary_json.avg_fps: {bad}")
    elif compare:
        tol = max(1e-4, abs(calc["avg_fps"]) * 1e-6)
        if abs(summary["avg_fps"] - calc["avg_fps"]) > tol:
            fails.append(
                f"summary_json.avg_fps: summary={summary['avg_fps']}"
                f" 复算={calc['avg_fps']} (容差 {tol})")
    ftm = summary.get("frame_time_ms")
    # 真实 producer（BenchmarkRecorder.Finish）仅 Frames>0 时写 frame_time_ms：
    # 零帧摘要缺该字段合法；但已存在的 ftm 类型错/字段非有限仍 FAIL。
    zero_frames = _is_int(summary.get("frames")) and summary["frames"] == 0
    if not isinstance(ftm, dict):
        if not (zero_frames and "frame_time_ms" not in summary):
            fails.append("summary_json.frame_time_ms: 缺失或非对象")
        return  # 零帧合法缺 ftm 时无子字段可查；非法则已 FAIL
    pairs = (("p50", "nearest_rank_ms", "50"), ("p95", "nearest_rank_ms", "95"),
             ("p99", "nearest_rank_ms", "99"), ("max", "max_ms", None))
    for key, ck, sub in pairs:
        v = ftm.get(key)
        if not _is_num(v):
            fails.append(f"summary_json.frame_time_ms.{key}: {bad}")
        elif compare:
            expected = calc[ck][sub] if sub else calc[ck]
            if abs(v - expected) > 1e-5:
                fails.append(
                    f"summary_json.frame_time_ms.{key}: summary={v} 复算={expected}"
                    f" (容差 1e-5ms)")
    ov = ftm.get("over_33ms")
    if not _is_int(ov):
        fails.append("summary_json.frame_time_ms.over_33ms: 缺失或非整数（布尔不算整数）")
    elif compare and ov != calc["over_33ms"]:
        fails.append(
            f"summary_json.frame_time_ms.over_33ms: summary={ov} 复算={calc['over_33ms']}")


def audit_run(run, manifest_dir, mb):
    rep = {"label": run["label"], "run_id": None, "summary_status": None,
           "exit_code": None, "verdict": None, "reasons": [],
           "file_sha256": {"frames_csv": None, "summary_json": None, "exit_record": None},
           "frames_read": 0, "_fails": [], "_incomplete": []}

    def resolve(p):
        pp = Path(p)
        return pp if pp.is_absolute() else manifest_dir / pp

    fr = read_frames_csv(resolve(run["frames_csv"]))
    rep["file_sha256"]["frames_csv"] = fr["sha256"]
    rep["frames_read"] = fr["frames_read"]
    rep["_fails"] += fr["reasons"]
    rep["_incomplete"] += fr["incomplete"]

    sm = read_summary_json(resolve(run["summary_json"]))
    rep["file_sha256"]["summary_json"] = sm["sha256"]
    rep["_fails"] += sm["reasons"]
    rep["_incomplete"] += sm["incomplete"]
    sdata = sm["data"]
    if sdata is not None:
        if isinstance(sdata.get("run_id"), str) and sdata["run_id"]:
            rep["run_id"] = sdata["run_id"]
        if isinstance(sdata.get("status"), str):
            rep["summary_status"] = sdata["status"]

    ex = read_exit_record(resolve(run["exit_record"]), run.get("exit_case"))
    rep["file_sha256"]["exit_record"] = ex["sha256"]
    rep["_fails"] += ex["reasons"]
    rep["_incomplete"] += ex["incomplete"]
    rep["exit_code"] = ex["exit_code"]

    # 数值比较仅在 CSV 完整可读（非截断、非零帧、无格式 FAIL）且 summary 可读时执行；
    # summary 必需字段的结构/类型/有限性独立检查（compare=False）不受此限制，
    # 截断/零帧/缺证据/recompute 失败时也执行——NaN/超大整数/坏类型可独立判 FAIL。
    calc = None
    if (not fr["truncated"] and fr["frames_read"] >= 1 and not fr["reasons"]
            and sdata is not None):
        try:
            calc = mb.recompute(resolve(run["frames_csv"]))
        except Exception as e:
            rep["_fails"].append(f"frames_csv.recompute: 独立复算失败 ({e})")
        else:
            # 比较所依赖的复算值必须有限：avg_fps=inf 会让容差变 inf 放过任意
            # summary 值；非有限按具体 recompute 字段 FAIL，跳过数值比较。
            calc_fields = [("avg_fps", calc["avg_fps"]),
                           ("duration_csv_s", calc["duration_csv_s"]),
                           ("max_ms", calc["max_ms"])] + [
                (f"nearest_rank_ms.{p}", calc["nearest_rank_ms"][p])
                for p in ("50", "95", "99")]
            bad = [f for f, v in calc_fields if not _is_num(v)]
            for f in bad:
                rep["_fails"].append(f"frames_csv.recompute.{f}: 复算结果非有限，无法比较")
            if bad:
                calc = None
    if sdata is not None:
        check_stats(sdata, calc, rep["_fails"], compare=calc is not None)

    if rep["summary_status"] in STATUSES and rep["exit_code"] is not None:
        want = STATUSES[rep["summary_status"]]
        if rep["exit_code"] != want:
            rep["_fails"].append(
                f"exit_record.exit_code: status={rep['summary_status']} 需要 exit={want}，"
                f"实际 exit={rep['exit_code']}")
    return rep


def finalize(reps):
    by_id = {}
    for rep in reps:
        if rep["run_id"]:
            by_id.setdefault(rep["run_id"], []).append(rep["label"])
    for rep in reps:
        rid = rep["run_id"]
        if rid and len(by_id[rid]) > 1:
            others = [l for l in by_id[rid] if l != rep["label"]]
            rep["_fails"].append(f"run_id.duplicate: run_id={rid} 与 label={others} 重复")
    overall = "PASS"
    for rep in reps:
        fails = rep.pop("_fails")
        rep["reasons"] = fails + rep.pop("_incomplete")
        if fails:
            rep["verdict"] = "FAIL"
        elif rep["reasons"]:
            rep["verdict"] = "INCOMPLETE"
        else:
            rep["verdict"] = "PASS"
        if rep["verdict"] == "FAIL":
            overall = "FAIL"
        elif rep["verdict"] == "INCOMPLETE" and overall == "PASS":
            overall = "INCOMPLETE"
    return overall


def main(argv=None):
    p = argparse.ArgumentParser(description="LIFE-01-AUDIT 运行产物与退出证据审计器")
    p.add_argument("--manifest", required=True, help="清单 JSON（相对输入按其目录解析）")
    p.add_argument("--output", required=True, help="报告 JSON（相对路径按 manifest 目录解析）")
    args = p.parse_args(argv)
    try:
        mb = load_measure_baseline()
        manifest_path = Path(args.manifest)
        if not manifest_path.is_file():
            raise CliError(f"manifest 不存在: {_display_path(args.manifest)}")
        manifest_dir = manifest_path.resolve().parent
        out = Path(args.output)
        if not out.is_absolute():
            out = manifest_dir / out
        runs = load_manifest(manifest_path)
        reps = [audit_run(run, manifest_dir, mb) for run in runs]
        overall = finalize(reps)
        report = {"schema_version": 1, "overall": overall, "runs": reps}
        # 最终目标以 O_CREAT|O_EXCL 独占创建：已存在文件/任何符号链接（含悬空）一律
        # EEXIST 拒绝，检查即创建，不存在"检查后目标被创建/并发 writer"竞态窗口。
        try:
            fd = os.open(out, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o666)
        except FileExistsError:
            raise CliError(f"输出已存在（含符号链接），不覆盖原记录: {_display_path(out)}")
        except OSError as e:
            raise CliError(f"报告创建失败: {_safe_io(e)}")
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                # ensure_ascii=True：数据里的孤立代理项（如 run_id="\ud800"）以
                # \uXXXX 转义写出，报告保持完整可解析，不因编码失败留下 0 字节。
                f.write(json.dumps(report, ensure_ascii=True, indent=2) + "\n")
        except OSError as e:
            raise CliError(f"报告写入失败: {_safe_io(e)}")
    except CliError as e:
        # 诊断同样不容孤立代理项导致 print 崩溃
        msg = str(e).encode("utf-8", "replace").decode("utf-8")
        print(f"audit: {msg}", file=sys.stderr)
        return 2
    return 0 if overall == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
