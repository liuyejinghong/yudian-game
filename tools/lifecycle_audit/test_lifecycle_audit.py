"""LIFE-01-AUDIT 审计器测试（标准库 unittest，不依赖 cwd）。

覆盖：金样三对真实证据 PASS / run_id 互异 / 输入 SHA 不变 / 输出无绝对路径；
人工反例：completed/130、interrupted/0、帧数/时长/分位矛盾、重复 run_id、
缺 summary、缺退出记录、截断末行、零帧、NaN、未知状态、坏 JSON、覆盖输出；
以及 manifest 结构无效（exit 2 不写报告）、相对路径解析、布尔当整数、
exit_case 找不到/重复匹配。返工回归：超大整数统计判定不抛异常、截断时 summary
必需字段独立判 FAIL、输出独占创建拒绝符号链接、manifest 非 UTF-8、IO 错误
诊断不含绝对路径且无 traceback。
"""
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import audit

AUDIT_PY = Path(__file__).resolve().parent / "audit.py"
GOLDEN_MANIFEST = Path(__file__).resolve().parent / "fixtures" / "lifecycle-r2-manifest.json"
TOOLS_DIR = Path(__file__).resolve().parents[1]
EVIDENCE = TOOLS_DIR.parent / "docs" / "engineering" / "evidence" / "2026-10-04-takeover" / "lifecycle-r2"
MB = audit.load_measure_baseline()

BASE_ROWS = [(0.001, 1.0, 1000.0), (0.007, 6.0, 166.666667), (0.013, 6.0, 166.666667)]


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build_csv(rows, trailing_newline=True, truncate_last=False):
    lines = ["frame,time_s,frame_ms,fps"]
    lines += [f"{i},{t!r},{ms!r},{fps!r}" for i, (t, ms, fps) in enumerate(rows, 1)]
    text = "\n".join(lines)
    if truncate_last:  # 砍掉末行最后一列：末行不足四列且无换行
        text = text[: text.rfind(",")]
    elif trailing_newline:
        text += "\n"
    return text


def write_csv(path, rows, **kw):
    Path(path).write_text(build_csv(rows, **kw), encoding="utf-8")


def base_summary(calc, run_id, status="completed"):
    return {
        "schema_version": 1, "run_id": run_id, "status": status,
        "frames": calc["frames"], "duration_actual_s": calc["duration_csv_s"],
        "avg_fps": calc["avg_fps"],
        "frame_time_ms": {"p50": calc["nearest_rank_ms"]["50"],
                          "p95": calc["nearest_rank_ms"]["95"],
                          "p99": calc["nearest_rank_ms"]["99"],
                          "max": calc["max_ms"], "over_33ms": calc["over_33ms"],
                          "method": "nearest_rank_all_frames_including_startup"},
    }


class Fixture:
    """在临时目录里拼装 runs 并运行 audit.main()（相对路径按 manifest 目录解析）。"""

    def __init__(self, root):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        self.entries = []

    def valid_calc(self, name="probe"):
        """写一份合法 BASE_ROWS CSV 并 recompute，用于构造合法 summary。"""
        write_csv(self.root / f"{name}-frames.csv", BASE_ROWS)
        return MB.recompute(self.root / f"{name}-frames.csv")

    def add_run(self, label, *, status="completed", exit_code=0, run_id=None,
                rows=BASE_ROWS, csv_text=None, summary_text=None, exit_text=None,
                exit_case=None, omit=()):
        """omit 中的角色仍写入 manifest 键，但指向不存在的文件（缺证=文件缺失）。"""
        entry = {"label": label}
        calc = None
        if "frames_csv" in omit:
            entry["frames_csv"] = f"{label}-frames.csv"
        else:
            p = self.root / f"{label}-frames.csv"
            p.write_text(csv_text if csv_text is not None
                         else build_csv(rows), encoding="utf-8")
            entry["frames_csv"] = p.name
        if "summary_json" in omit:
            entry["summary_json"] = f"{label}-summary.json"
        else:
            if summary_text is None:
                # summary 需要与 CSV 一致的统计：CSV 合法时用其 recompute，
                # 否则（CSV 故意非法/缺失）退回合法 BASE_ROWS 基线。
                if "frames_csv" not in omit and csv_text is None:
                    calc = MB.recompute(self.root / entry["frames_csv"])
                else:
                    calc = self.valid_calc()
                summary_text = json.dumps(base_summary(calc, run_id or f"rid-{label}",
                                                       status))
            (self.root / f"{label}-summary.json").write_text(summary_text,
                                                             encoding="utf-8")
            entry["summary_json"] = f"{label}-summary.json"
        if "exit_record" in omit:
            entry["exit_record"] = f"{label}-exit.json"
        else:
            if exit_text is None:
                exit_text = json.dumps({"exit_code": exit_code})
            (self.root / f"{label}-exit.json").write_text(exit_text, encoding="utf-8")
            entry["exit_record"] = f"{label}-exit.json"
        if exit_case is not None:
            entry["exit_case"] = exit_case
        self.entries.append(entry)
        return entry

    def add_and_audit(self, label, output="report.json", **kw):
        self.add_run(label, **kw)
        return self.run(output=output)

    def manifest(self):
        path = self.root / "manifest.json"
        path.write_text(json.dumps({"schema_version": 1, "runs": self.entries}),
                        encoding="utf-8")
        return path

    def run(self, output="report.json", pre_create_output=None):
        out = self.root / output
        if pre_create_output is not None:
            out.write_text(pre_create_output, encoding="utf-8")
        rc = audit.main(["--manifest", str(self.manifest()), "--output", str(out)])
        report = (json.loads(out.read_text(encoding="utf-8"))
                  if rc != 2 and out.exists() else None)
        return rc, report


class GoldenManifestTest(unittest.TestCase):
    def test_gate_cli_from_other_cwd(self):
        """门禁同款：任意 cwd 用绝对路径跑金样 CLI，三项 PASS、run_id 互异。"""
        with tempfile.TemporaryDirectory() as td:
            out = Path(td) / "report.json"
            before = {p.name: sha256(p) for p in EVIDENCE.iterdir()}
            proc = subprocess.run(
                [sys.executable, str(AUDIT_PY), "--manifest", str(GOLDEN_MANIFEST),
                 "--output", str(out)],
                cwd=td, capture_output=True, text=True)
            after = {p.name: sha256(p) for p in EVIDENCE.iterdir()}
            self.assertEqual(proc.returncode, 0, proc.stderr)
            self.assertEqual(proc.stdout, "")
            text = out.read_text(encoding="utf-8")
            rep = json.loads(text)
            self.assertEqual(rep["overall"], "PASS")
            self.assertEqual([r["verdict"] for r in rep["runs"]], ["PASS"] * 3)
            run_ids = [r["run_id"] for r in rep["runs"]]
            self.assertEqual(len(set(run_ids)), 3, run_ids)
            self.assertEqual([r["frames_read"] for r in rep["runs"]], [32, 32, 2])
            self.assertNotIn(str(EVIDENCE), text)
            self.assertNotIn(td, text)
            self.assertNotIn("/Users/", text)
            self.assertEqual(before, after, "审计不得改动输入证据")

    def test_golden_manifest_relative_paths_resolve_from_other_cwd(self):
        cwd = os.getcwd()
        try:
            os.chdir(tempfile.gettempdir())
            with tempfile.TemporaryDirectory() as td:
                out = Path(td) / "report.json"
                rc = audit.main(["--manifest", str(GOLDEN_MANIFEST), "--output", str(out)])
                self.assertEqual(rc, 0)
                self.assertEqual(json.loads(out.read_text())["overall"], "PASS")
        finally:
            os.chdir(cwd)

    def test_report_run_keys(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("ok")
            self.assertEqual(rc, 0, rep)
            self.assertEqual(set(rep), {"schema_version", "overall", "runs"})
            self.assertEqual(rep["schema_version"], 1)
            for run in rep["runs"]:
                self.assertEqual(
                    set(run), {"label", "run_id", "summary_status", "exit_code",
                               "verdict", "reasons", "file_sha256", "frames_read"})
                self.assertEqual(set(run["file_sha256"]),
                                 {"frames_csv", "summary_json", "exit_record"})


class SyntheticPassTest(unittest.TestCase):
    def test_consistent_run_passes(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("ok", status="completed", exit_code=0)
            self.assertEqual(rc, 0, rep)
            self.assertEqual(rep["runs"][0]["verdict"], "PASS")
            self.assertEqual(rep["runs"][0]["exit_code"], 0)

    def test_interrupted_130_passes(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("ok", status="interrupted", exit_code=130)
            self.assertEqual(rc, 0, rep)
            self.assertEqual(rep["runs"][0]["verdict"], "PASS")

    def test_complete_last_line_without_newline_is_valid(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            write_csv(f.root / "ok-frames.csv", BASE_ROWS, trailing_newline=False)
            rc, rep = f.run()
            self.assertEqual(rc, 0, rep)
            self.assertEqual(rep["runs"][0]["verdict"], "PASS")

    def test_exit_case_results_json_pass(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "c1", exit_case="c1",
                exit_text=json.dumps({"cases": [{"case": "c1", "exit": 0,
                                                 "status": "completed"}]}))
            self.assertEqual(rc, 0, rep)
            self.assertEqual(rep["runs"][0]["exit_code"], 0)


class CounterexampleTest(unittest.TestCase):
    def assert_fail(self, rep, run_index, fragment):
        run = rep["runs"][run_index]
        self.assertEqual(run["verdict"], "FAIL", run)
        self.assertTrue(any(fragment in r for r in run["reasons"]),
                        (fragment, run["reasons"]))

    def assert_incomplete(self, rep, fragment):
        self.assertEqual(rep["overall"], "INCOMPLETE", rep)
        self.assertEqual(rep["runs"][0]["verdict"], "INCOMPLETE")
        self.assertTrue(any(fragment in r for r in rep["runs"][0]["reasons"]),
                        (fragment, rep["runs"][0]["reasons"]))

    def mutate_summary(self, root, name, mutate):
        p = Path(root) / f"{name}-summary.json"
        s = json.loads(p.read_text())
        mutate(s)
        p.write_text(json.dumps(s), encoding="utf-8")

    def test_completed_with_exit_130_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("bad", status="completed", exit_code=130)
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "exit_record.exit_code")

    def test_interrupted_with_exit_0_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("bad", status="interrupted", exit_code=0)
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "exit_record.exit_code")

    def test_frames_mismatch_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad", lambda s: s.update(frames=99))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json.frames")

    def test_duration_mismatch_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad",
                                lambda s: s.update(duration_actual_s=s["duration_actual_s"] + 0.01))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json.duration_actual_s")

    def test_percentile_mismatch_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad",
                                lambda s: s["frame_time_ms"].update(p95=s["frame_time_ms"]["p95"] + 1.0))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json.frame_time_ms.p95")

    def test_over_33ms_mismatch_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad",
                                lambda s: s["frame_time_ms"].update(over_33ms=2))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json.frame_time_ms.over_33ms")

    def test_duplicate_run_id_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("a", run_id="same-rid")
            f.add_run("b", run_id="same-rid")
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assertEqual(rep["overall"], "FAIL")
            for i in (0, 1):
                self.assert_fail(rep, i, "run_id.duplicate")

    def test_missing_summary_incomplete(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("r", omit=("summary_json",))
            self.assertEqual(rc, 1)
            self.assert_incomplete(rep, "summary_json")
            self.assertIsNone(rep["runs"][0]["run_id"])

    def test_missing_exit_record_incomplete(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("r", omit=("exit_record",))
            self.assertEqual(rc, 1)
            self.assert_incomplete(rep, "exit_record")
            self.assertIsNone(rep["runs"][0]["exit_code"])

    def test_missing_frames_csv_incomplete(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("r", omit=("frames_csv",))
            self.assertEqual(rc, 1)
            self.assert_incomplete(rep, "frames_csv")
            self.assertEqual(rep["runs"][0]["frames_read"], 0)

    def test_truncated_last_line_incomplete_and_stats_skipped(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("r")
            write_csv(f.root / "r-frames.csv", BASE_ROWS, truncate_last=True)
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_incomplete(rep, "末行截断")
            self.assertEqual(rep["runs"][0]["frames_read"], 2)
            # 统计比较被跳过：summary 仍声称 3 帧，不产生 FAIL
            self.assertTrue(all("summary_json" not in r
                                for r in rep["runs"][0]["reasons"]))

    def test_truncated_but_exit_contradiction_still_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("r", status="completed", exit_code=130)
            write_csv(f.root / "r-frames.csv", BASE_ROWS, truncate_last=True)
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "exit_record.exit_code")

    def test_truncated_last_line_with_four_columns_illegal_fails(self):
        # 末行四列却类型非法：按 FAIL，不以截断掩盖
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("r")
            write_csv(f.root / "r-frames.csv", BASE_ROWS, trailing_newline=False)
            with open(f.root / "r-frames.csv", "a", encoding="utf-8") as fh:
                fh.write("\n4,abc,1.0,10.0")
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "frames_csv.line5")

    def test_zero_frames_incomplete(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            probe = f.valid_calc()
            rc, rep = f.add_and_audit("r", csv_text="frame,time_s,frame_ms,fps\n",
                                      summary_text=json.dumps(base_summary(probe, "rid-r")))
            self.assertEqual(rc, 1)
            self.assert_incomplete(rep, "零帧")

    def test_nan_frame_ms_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            probe = f.valid_calc()
            rows = [(0.001, 1.0, 1000.0), (0.007, float("nan"), 166.0), (0.013, 6.0, 166.0)]
            rc, rep = f.add_and_audit("r", rows=rows,
                                      summary_text=json.dumps(base_summary(probe, "rid-r")))
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "frames_csv.line3.frame_ms")

    def test_nan_in_summary_stats_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad", lambda s: s.update(avg_fps=float("nan")))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json.avg_fps")

    def test_unknown_status_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("r", status="crashed", exit_code=1)
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json.status")
            self.assertEqual(rep["runs"][0]["summary_status"], "crashed")

    def test_bad_summary_json_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("r", summary_text="{oops")
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "summary_json: JSON 解析失败")

    def test_bad_exit_json_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit("r", exit_text="not json")
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "exit_record: JSON 解析失败")

    def test_bool_exit_code_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "r", exit_text=json.dumps({"exit_code": True}))
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "exit_record.exit_code")

    def test_exit_case_not_found_incomplete(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "r", exit_case="nope",
                exit_text=json.dumps({"cases": [{"case": "other", "exit": 0}]}))
            self.assertEqual(rc, 1)
            self.assert_incomplete(rep, "case_not_found")

    def test_exit_case_duplicate_match_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "r", exit_case="dup",
                exit_text=json.dumps({"cases": [{"case": "dup", "exit": 0},
                                                {"case": "dup", "exit": 0}]}))
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "exit_record.exit_case")

    def test_frame_gap_fails(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("r")
            (f.root / "r-frames.csv").write_text(
                "frame,time_s,frame_ms,fps\n1,0.001,1.0,1000.0\n2,0.007,6.0,166.0\n"
                "4,0.013,6.0,166.0\n", encoding="utf-8")
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "frames_csv.line4.frame")

    def test_time_not_increasing_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "r", rows=[(0.007, 6.0, 166.0), (0.007, 6.0, 166.0), (0.013, 6.0, 166.0)])
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "frames_csv.line3.time_s")

    def test_negative_fps_fails(self):
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "r", rows=[(0.001, 1.0, -5.0), (0.007, 6.0, 166.0), (0.013, 6.0, 166.0)])
            self.assertEqual(rc, 1)
            self.assert_fail(rep, 0, "frames_csv.line2.fps")

    def test_overwrite_output_refused(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            rc, rep = f.run(output="report.json", pre_create_output="ORIGINAL")
            self.assertEqual(rc, 2)
            self.assertEqual((f.root / "report.json").read_text(), "ORIGINAL")

    def test_synthetic_inputs_unchanged_after_fail_audit(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad", status="completed", exit_code=130)
            files = [f.root / n for n in
                     ("bad-frames.csv", "bad-summary.json", "bad-exit.json")]
            before = [sha256(p) for p in files]
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assertEqual(before, [sha256(p) for p in files])

    def test_report_has_no_absolute_paths(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad", status="completed", exit_code=130)
            rc, rep = f.run()
            text = json.dumps(rep, ensure_ascii=False)
            self.assertNotIn(td, text)
            self.assertNotIn(str(TOOLS_DIR.parent), text)


class InvalidManifestTest(unittest.TestCase):
    RUN = {"label": "a", "frames_csv": "f", "summary_json": "s", "exit_record": "e"}
    CASES = {
        "missing_file": None,
        "bad_json": "{",
        "top_level_list": "[]",
        "bad_schema_version": json.dumps({"schema_version": 2, "runs": [RUN]}),
        "schema_version_bool": json.dumps({"schema_version": True, "runs": [RUN]}),
        "empty_runs": json.dumps({"schema_version": 1, "runs": []}),
        "runs_not_list": json.dumps({"schema_version": 1, "runs": {"a": 1}}),
        "run_not_object": json.dumps({"schema_version": 1, "runs": ["a"]}),
        "missing_label": json.dumps({"schema_version": 1,
                                     "runs": [{"frames_csv": "a"}]}),
        "empty_path": json.dumps({"schema_version": 1, "runs": [
            {"label": "a", "frames_csv": "", "summary_json": "b", "exit_record": "c"}]}),
        "duplicate_labels": json.dumps({"schema_version": 1, "runs": [RUN, RUN]}),
        "exit_case_not_string": json.dumps({"schema_version": 1, "runs": [
            dict(RUN, exit_case=5)]}),
    }

    def test_all_invalid_manifests_exit_2_without_report(self):
        for name, text in self.CASES.items():
            with self.subTest(name):
                with tempfile.TemporaryDirectory() as td:
                    out = Path(td) / "report.json"
                    if text is None:
                        mpath = Path(td) / "no-such-manifest.json"
                    else:
                        mpath = Path(td) / "manifest.json"
                        mpath.write_text(text, encoding="utf-8")
                    rc = audit.main(["--manifest", str(mpath), "--output", str(out)])
                    self.assertEqual(rc, 2, name)
                    self.assertFalse(out.exists(), name)

    def test_stderr_specific_on_invalid(self):
        with tempfile.TemporaryDirectory() as td:
            mpath = Path(td) / "manifest.json"
            mpath.write_text('{"schema_version": 1, "runs": []}', encoding="utf-8")
            proc = subprocess.run(
                [sys.executable, str(AUDIT_PY), "--manifest", str(mpath),
                 "--output", str(Path(td) / "r.json")],
                capture_output=True, text=True)
            self.assertEqual(proc.returncode, 2)
            self.assertIn("runs", proc.stderr)


class PathResolutionTest(unittest.TestCase):
    def test_relative_output_resolves_against_manifest_dir(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            other = tempfile.mkdtemp()
            rc = audit.main(["--manifest", str(f.manifest()),
                             "--output", "report-rel.json"])
            self.assertEqual(rc, 0)
            self.assertTrue((f.root / "report-rel.json").exists())
            self.assertFalse((Path(other) / "report-rel.json").exists())

    def test_absolute_run_paths_allowed(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            (f.root / "abs-manifest.json").write_text(json.dumps({
                "schema_version": 1,
                "runs": [{"label": "ok",
                          "frames_csv": str(f.root / "ok-frames.csv"),
                          "summary_json": str(f.root / "ok-summary.json"),
                          "exit_record": str(f.root / "ok-exit.json")}]}), encoding="utf-8")
            out = f.root / "report-abs.json"
            rc = audit.main(["--manifest", str(f.root / "abs-manifest.json"),
                             "--output", str(out)])
            self.assertEqual(rc, 0)
            self.assertEqual(json.loads(out.read_text())["overall"], "PASS")


class ReworkRegressionTest(unittest.TestCase):
    """返工回归：数值判定不抛异常（超大整数）、截断时 summary 字段独立判 FAIL、
    输出 O_EXCL 独占创建（悬空/指向已存在文件的符号链接均拒绝）、manifest 非
    UTF-8、IO 错误诊断不含绝对路径、无 traceback。"""

    def mutate_summary(self, root, name, mutate):
        p = Path(root) / f"{name}-summary.json"
        s = json.loads(p.read_text())
        mutate(s)
        p.write_text(json.dumps(s), encoding="utf-8")

    def run_cli(self, root, out="report.json", manifest="manifest.json"):
        return subprocess.run(
            [sys.executable, str(AUDIT_PY), "--manifest", str(Path(root) / manifest),
             "--output", str(Path(root) / out)],
            cwd=root, capture_output=True, text=True)

    def test_huge_int_avg_fps_fails_with_report(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad", lambda s: s.update(avg_fps=10 ** 400))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assertEqual(rep["overall"], "FAIL")
            run = rep["runs"][0]
            self.assertEqual(run["verdict"], "FAIL")
            self.assertTrue(any("summary_json.avg_fps" in r for r in run["reasons"]),
                            run["reasons"])

    def test_huge_int_frames_and_duration_fail_not_crash(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            self.mutate_summary(td, "bad",
                                lambda s: s.update(frames=10 ** 400,
                                                   duration_actual_s=10 ** 400))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            run = rep["runs"][0]
            self.assertEqual(run["verdict"], "FAIL")
            self.assertTrue(any("summary_json.frames" in r for r in run["reasons"]),
                            run["reasons"])
            self.assertTrue(any("summary_json.duration_actual_s" in r
                                for r in run["reasons"]), run["reasons"])

    def test_truncated_csv_nan_avg_fps_fails_independently(self):
        # 末行截断：数值比较跳过，但必需字段类型/有限性独立判定，NaN 不被缺证掩盖
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("r")
            write_csv(f.root / "r-frames.csv", BASE_ROWS, truncate_last=True)
            self.mutate_summary(td, "r", lambda s: s.update(avg_fps=float("nan")))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            run = rep["runs"][0]
            self.assertEqual(run["verdict"], "FAIL")
            self.assertTrue(any("summary_json.avg_fps" in r for r in run["reasons"]),
                            run["reasons"])
            self.assertTrue(any("末行截断" in r for r in run["reasons"]), run["reasons"])
            self.assertEqual(run["frames_read"], 2)

    def test_truncated_csv_missing_stat_field_fails_independently(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("r")
            write_csv(f.root / "r-frames.csv", BASE_ROWS, truncate_last=True)
            self.mutate_summary(td, "r",
                                lambda s: s["frame_time_ms"].pop("p99"))
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assertTrue(any("summary_json.frame_time_ms.p99" in r
                                for r in rep["runs"][0]["reasons"]), rep)

    def test_dangling_symlink_output_refused(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            target = f.root / "untouched.json"
            f.manifest()
            out = f.root / "report.json"
            out.symlink_to(target)
            proc = self.run_cli(td)
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertTrue(out.is_symlink(), "符号链接必须原样保留")
            self.assertEqual(os.readlink(out), str(target))
            self.assertFalse(target.exists(), "悬空目标不得被创建")
            self.assertIn("输出已存在", proc.stderr)
            self.assertNotIn("Traceback", proc.stderr)

    def test_symlink_to_existing_file_output_refused(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            keep = f.root / "keep.json"
            keep.write_text("KEEP-ORIGINAL", encoding="utf-8")
            f.manifest()
            out = f.root / "report.json"
            out.symlink_to(keep)
            proc = self.run_cli(td)
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertEqual(keep.read_text(), "KEEP-ORIGINAL")
            self.assertTrue(out.is_symlink())

    def test_manifest_non_utf8_exit_2_no_report(self):
        with tempfile.TemporaryDirectory() as td:
            (Path(td) / "manifest.json").write_bytes(b'\xff\xfe{"schema_version": 1}')
            out = Path(td) / "report.json"
            proc = self.run_cli(td)
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertFalse(out.exists())
            self.assertIn("非 UTF-8", proc.stderr)
            self.assertNotIn("Traceback", proc.stderr)

    def test_output_io_error_sanitized(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("ok")
            f.manifest()
            proc = self.run_cli(td, out="no-such-dir/report.json")
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertIn("报告创建失败", proc.stderr)
            self.assertNotIn("Traceback", proc.stderr)
            for secret in (td, "/Users/", "/private/"):
                self.assertNotIn(secret, proc.stderr)

    def test_missing_manifest_stderr_sanitized(self):
        with tempfile.TemporaryDirectory() as td:
            proc = self.run_cli(td, manifest="no-such.json")
            self.assertEqual(proc.returncode, 2, proc.stderr)
            self.assertIn("manifest 不存在", proc.stderr)
            self.assertIn("no-such.json", proc.stderr)
            self.assertNotIn(td, proc.stderr)
            self.assertNotIn("Traceback", proc.stderr)


class FinalReworkRegressionTest(unittest.TestCase):
    """第二轮返工回归：真实 producer 零帧摘要缺 frame_time_ms 为 INCOMPLETE；
    复算统计非有限（avg_fps=inf）按 recompute 字段 FAIL 不放过矛盾值；
    run_id 含孤立代理项时报告仍完整可解析。前轮已关闭行为不退回。"""

    ZERO_SUMMARY = json.dumps(
        {"schema_version": 1, "run_id": "rid-zero", "status": "interrupted",
         "frames": 0, "duration_actual_s": 0, "avg_fps": 0})

    def test_real_zero_frame_interruption_incomplete_not_fail(self):
        # BenchmarkRecorder.Finish 仅 Frames>0 时写 frame_time_ms
        with tempfile.TemporaryDirectory() as td:
            rc, rep = Fixture(td).add_and_audit(
                "r", csv_text="frame,time_s,frame_ms,fps\n",
                summary_text=self.ZERO_SUMMARY, status="interrupted", exit_code=130)
            self.assertEqual(rc, 1)
            self.assertEqual(rep["overall"], "INCOMPLETE", rep)
            run = rep["runs"][0]
            self.assertEqual(run["verdict"], "INCOMPLETE")
            self.assertTrue(any("零帧" in r for r in run["reasons"]), run["reasons"])
            self.assertFalse([r for r in run["reasons"] if r.startswith("summary_json")],
                             run["reasons"])

    def test_zero_frame_with_bad_ftm_type_still_fails(self):
        with tempfile.TemporaryDirectory() as td:
            s = json.loads(self.ZERO_SUMMARY)
            s["frame_time_ms"] = 5
            rc, rep = Fixture(td).add_and_audit(
                "r", csv_text="frame,time_s,frame_ms,fps\n",
                summary_text=json.dumps(s), status="interrupted", exit_code=130)
            self.assertEqual(rc, 1)
            reasons = rep["runs"][0]["reasons"]
            self.assertTrue(any("summary_json.frame_time_ms" in r for r in reasons),
                            reasons)

    def test_zero_frame_with_explicit_null_ftm_fails(self):
        with tempfile.TemporaryDirectory() as td:
            summary = json.loads(self.ZERO_SUMMARY)
            summary["frame_time_ms"] = None
            rc, report = Fixture(td).add_and_audit(
                "r", csv_text="frame,time_s,frame_ms,fps\n",
                summary_text=json.dumps(summary), exit_code=130)
            self.assertEqual(rc, 1)
            self.assertEqual(report["overall"], "FAIL")
            self.assertTrue(any("summary_json.frame_time_ms" in reason
                                for reason in report["runs"][0]["reasons"]))

    def test_zero_frame_with_nonfinite_ftm_value_still_fails(self):
        with tempfile.TemporaryDirectory() as td:
            s = json.loads(self.ZERO_SUMMARY)
            s["frame_time_ms"] = {"p50": float("nan"), "p95": 0.0, "p99": 0.0,
                                  "max": 0.0, "over_33ms": 0}
            rc, rep = Fixture(td).add_and_audit(
                "r", csv_text="frame,time_s,frame_ms,fps\n",
                summary_text=json.dumps(s), status="interrupted", exit_code=130)
            self.assertEqual(rc, 1)
            reasons = rep["runs"][0]["reasons"]
            self.assertTrue(any("summary_json.frame_time_ms.p50" in r for r in reasons),
                            reasons)

    def test_nonzero_summary_missing_ftm_still_fails(self):
        # 零帧豁免不外溢：非零帧摘要缺 frame_time_ms 仍 FAIL
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("bad")
            s = json.loads((f.root / "bad-summary.json").read_text())
            del s["frame_time_ms"]
            (f.root / "bad-summary.json").write_text(json.dumps(s), encoding="utf-8")
            rc, rep = f.run()
            self.assertEqual(rc, 1)
            self.assertTrue(any("summary_json.frame_time_ms" in r
                                for r in rep["runs"][0]["reasons"]), rep)

    def test_recompute_infinite_avg_fps_fails_specific_field(self):
        # frame_ms=1e-308 → recompute.avg_fps=inf，无限容差不得放过 summary=1
        with tempfile.TemporaryDirectory() as td:
            summary = json.dumps({
                "schema_version": 1, "run_id": "rid-r", "status": "completed",
                "frames": 1, "duration_actual_s": 0.0001, "avg_fps": 1,
                "frame_time_ms": {"p50": 1e-308, "p95": 1e-308, "p99": 1e-308,
                                  "max": 1e-308, "over_33ms": 0}})
            rc, rep = Fixture(td).add_and_audit(
                "r", rows=[(0.0001, 1e-308, 1.0)], summary_text=summary)
            self.assertEqual(rc, 1)
            self.assertEqual(rep["overall"], "FAIL", rep)
            run = rep["runs"][0]
            self.assertEqual(run["verdict"], "FAIL")
            self.assertTrue(any("frames_csv.recompute.avg_fps" in r
                                for r in run["reasons"]), run["reasons"])

    def test_surrogate_run_id_report_parses(self):
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("a", run_id="\ud800")
            rc, rep = f.run()
            self.assertEqual(rc, 0, rep)
            self.assertEqual(rep["runs"][0]["run_id"], "\ud800")

    def test_surrogate_run_id_duplicate_fail_report_parses(self):
        # 重复 run_id 原因串含孤立代理项：报告仍需完整写出并可解析
        with tempfile.TemporaryDirectory() as td:
            f = Fixture(td)
            f.add_run("a", run_id="\ud800")
            f.add_run("b", run_id="\ud800")
            out = f.root / "report.json"
            rc = audit.main(["--manifest", str(f.manifest()), "--output", str(out)])
            self.assertEqual(rc, 1)
            self.assertGreater(out.stat().st_size, 0)
            rep = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual(rep["overall"], "FAIL")
            self.assertTrue(any("run_id.duplicate" in r for r in rep["runs"][0]["reasons"]))


if __name__ == "__main__":
    unittest.main()
