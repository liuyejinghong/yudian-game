#!/usr/bin/env python3
"""PERF-01-COMPARE 自测试（标准库 unittest）。

人工 fixture：KNOWN_FRAME_MS 为已知 nearest-rank 小样（手算期望，非实现生成）。
真实三轮金样测试在证据目录缺失时 skip。所有输入/输出在临时目录，仓库保持只读。
"""
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import compare

COMPARE_PY = Path(__file__).resolve().parent / 'compare.py'
REPO_ROOT = compare.REPO_ROOT
EVIDENCE = REPO_ROOT / 'docs/engineering/evidence/2026-10-04-takeover/metal-native'
DELETE = object()  # fixture 变更哨兵：删除字段

# 手算 nearest-rank 小样：n=7，排序后 [10,20,30,40,50,60,200]
# p50 -> 第 ceil(7*50/100)=4 个 -> 40；p95 -> 第 ceil(7*95/100)=7 个 -> 200；
# p99 -> 第 ceil(7*99/100)=7 个 -> 200；max=200。
KNOWN_FRAME_MS = [10, 20, 30, 40, 50, 60, 200]
KNOWN_EXPECTED = {'p50_ms': 40, 'p95_ms': 200, 'p99_ms': 200, 'max_ms': 200}

GPU = 'Apple M3 Pro (Apple9)'
OS_STRING = 'Darwin 27.0.0 test'


def make_frames_dir(directory, frame_ms=KNOWN_FRAME_MS, run_id='run-a',
                    summary_mutations=None, verification_mutations=None):
    """写入一套自洽的 frames.csv/summary.json/verification.json，返回目录。"""
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    rows = []
    elapsed = 0.0
    for index, ms in enumerate(frame_ms, start=1):
        elapsed += ms / 1000
        rows.append(f'{index},{elapsed:.6f},{ms:.6f},{1000 / ms:.6f}')
    csv_path = directory / 'frames.csv'
    csv_path.write_text('frame,time_s,frame_ms,fps\n' + '\n'.join(rows) + '\n', encoding='utf-8')

    baseline = compare.load_measure_baseline()
    recomputed = baseline.recompute(csv_path)
    summary = {
        'schema_version': 1,
        'run_id': run_id,
        'fixture': 's_small',
        'fixture_sha256': '81c4f0072c4bf223afd0db84ad588ac1cd5bd524a119596dd62214b273bd46ee',
        'seed': 20261004,
        'duration_requested_s': 45,
        'requested': {'resolution': {'width': 1920, 'height': 1200},
                      'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
                      'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6, 'ring_radius': 14}},
        'applied': {'msaa_3d': 'Msaa4X', 'fxaa': 'Disabled', 'scaling_3d_scale': 1,
                    'vsync': 'Disabled', 'max_fps': 0},
        'observed': {'headless': False, 'rendering_device': True,
                     'rendering_method': 'forward_plus', 'rendering_driver': 'metal',
                     'gpu': GPU, 'gpu_api_version': '4.0',
                     'window_pixels': [1920, 1200], 'viewport_size': [1920, 1200],
                     'robots': 12, 'facilities': 6},
        'graphical_performance_eligible': True,
        'godot_version': '4.7.2-stable (official)',
        'runtime': {'framework': '.NET 8.0.31', 'version': '8.0.31',
                    'architecture': 'Arm64', 'os': OS_STRING},
        'assembly_sha256': 'e12648754ce2c79f3ddf72d55b8a7d7da581c53a59d798c6926984dd708f66c0',
        'build': {'git_head': '7341ea83a3ad879ba98a496792d43eb9cd2d70ab',
                  'source_snapshot_sha256': 'e178163e2f7eee356cdf039f9a791dff01a856cd126043cc415d0d9df122d7c5',
                  'assets_snapshot_sha256': 'a82e1f56a1d436b3adf9334ffa7b73244ebf69184408a60a50c598469dffc6dd'},
        'memory': 'EVIDENCE_MISSING: external process measurement required',
        'load_scope': 'gray-r1 rendering, fixed patrol and animation only',
        'status': 'completed',
        'frames': recomputed['frames'],
        'duration_actual_s': recomputed['duration_csv_s'],
        'avg_fps': recomputed['avg_fps'],
        'frame_time_ms': {'p50': recomputed['nearest_rank_ms']['50'],
                          'p95': recomputed['nearest_rank_ms']['95'],
                          'p99': recomputed['nearest_rank_ms']['99'],
                          'max': recomputed['max_ms'],
                          'over_33ms': recomputed['over_33ms'],
                          'method': 'nearest_rank_all_frames_including_startup'},
    }
    if summary_mutations:
        for key, value in summary_mutations.items():
            if value is DELETE:
                summary.pop(key, None)
            else:
                summary[key] = value
    (directory / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')

    verification = {
        'exit_code': 0,
        'memory': {'maximum_resident_set_size_bytes': 291766272,
                   'peak_memory_footprint_bytes': 684082520,
                   'method': 'macOS /usr/bin/time -l; separate metrics'},
        'recomputed': recomputed,
        'launch': 'Yudian --rendering-driver metal -- --benchmark --duration N '
                  '--frames-csv <new-directory>/frames.csv --summary-json <new-directory>/summary.json',
        'environment': 'DOTNET_ROOT and DOTNET_ROOT_ARM64 nonexistent',
        'app_binary_sha256': '1f8594bfcebb09f30ea10ba72d6aa169e6dcdf5154ff43d37edd4c9ef2684bb9',
    }
    if verification_mutations:
        for key, value in verification_mutations.items():
            if value is DELETE:
                verification.pop(key, None)
            else:
                verification[key] = value
    (directory / 'verification.json').write_text(json.dumps(verification, indent=2) + '\n', encoding='utf-8')
    return directory


class FixtureTestMixin:
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def make_run(self, name, **kwargs):
        return make_frames_dir(self.tmp / name, **kwargs)

    def run_cli(self, runs, output):
        argv = [str(COMPARE_PY), '--runs']
        argv += [str(run) for run in runs]
        argv += ['--output', str(output)]
        return subprocess.run([sys.executable, *argv], capture_output=True, text=True, cwd=self.tmp)


class ComparePositiveTests(FixtureTestMixin, unittest.TestCase):

    def test_known_nearest_rank_small_sample(self):
        """人工已知 nearest-rank 小样：分组指标必须等于手算值。"""
        run_a = self.make_run('a', run_id='run-a')
        run_b = self.make_run('b', run_id='run-b')
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a, run_b], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(len(report['groups']), 1)
        metrics = report['groups'][0]['metrics']
        for name, expected in KNOWN_EXPECTED.items():
            self.assertEqual(metrics[name], {'min': expected, 'max': expected,
                                             'median': expected, 'samples': 2}, name)
        self.assertEqual(metrics['avg_fps']['samples'], 2)

    def test_report_shape_and_memory_from_verification(self):
        run_a = self.make_run('a', run_id='run-a')
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(report['schema_version'], 1)
        entry = report['runs'][0]
        self.assertEqual(entry['run_id'], 'run-a')
        for name in ('frames.csv', 'summary.json', 'verification.json'):
            self.assertRegex(entry['input_sha256'][name], r'^[0-9a-f]{64}$')
        self.assertEqual(entry['memory'], {'maximum_resident_set_size_bytes': 291766272,
                                           'peak_memory_footprint_bytes': 684082520})
        self.assertEqual(entry['recomputed']['max_ms'], KNOWN_EXPECTED['max_ms'])
        self.assertEqual(len(report['groups'][0]['run_ids']), 1)
        self.assertTrue(report['limitations'])

    def test_summary_memory_placeholder_never_used_as_process_memory(self):
        """summary.memory 缺证字符串/伪数字不得进入报告；实际内存只来自 verification。"""
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'memory': {'maximum_resident_set_size_bytes': 1,
                                                            'peak_memory_footprint_bytes': 1}})
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        entry = json.loads(output.read_text())['runs'][0]
        self.assertEqual(entry['memory']['maximum_resident_set_size_bytes'], 291766272)
        self.assertEqual(entry['memory']['peak_memory_footprint_bytes'], 684082520)

    def test_report_has_no_absolute_paths(self):
        run_a = self.make_run('a', run_id='run-a')
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        text = output.read_text()
        self.assertNotIn(str(REPO_ROOT), text)
        self.assertNotIn(os.path.expanduser('~'), text)
        self.assertNotIn(str(self.tmp), text)

    def test_inputs_unchanged_after_run(self):
        run_a = self.make_run('a', run_id='run-a')
        before = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in sorted(run_a.iterdir())}
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        after = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                 for p in sorted(run_a.iterdir())}
        self.assertEqual(before, after)

    def test_output_overwrite_rejected_and_file_untouched(self):
        run_a = self.make_run('a', run_id='run-a')
        output = self.tmp / 'report.json'
        output.write_text('SENTINEL', encoding='utf-8')
        result = self.run_cli([run_a], output)
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn('report.json', result.stderr)
        self.assertNotIn(os.path.expanduser('~'), result.stderr)
        self.assertEqual(output.read_text(), 'SENTINEL')


class CompareGroupingTests(FixtureTestMixin, unittest.TestCase):

    def test_identity_difference_splits_groups(self):
        run_a = self.make_run('a', run_id='run-a')
        run_b = self.make_run('b', run_id='run-b', summary_mutations={'seed': 20261005})
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a, run_b], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(len(report['groups']), 2)
        self.assertEqual([len(g['run_ids']) for g in report['groups']], [1, 1])
        self.assertNotEqual(report['groups'][0]['group_id'], report['groups'][1]['group_id'])

    def test_repeated_runs_flags_accumulate(self):
        """重复 --runs 不得静默覆盖，必须累加。"""
        run_a = self.make_run('a', run_id='run-a')
        run_b = self.make_run('b', run_id='run-b')
        output = self.tmp / 'report.json'
        argv = [str(COMPARE_PY), '--runs', str(run_a), '--runs', str(run_b), '--output', str(output)]
        result = subprocess.run([sys.executable, *argv], capture_output=True, text=True, cwd=self.tmp)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(len(report['runs']), 2)

    def test_same_identity_same_group(self):
        run_a = self.make_run('a', run_id='run-a')
        run_b = self.make_run('b', run_id='run-b')
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a, run_b], output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(len(report['groups']), 1)
        self.assertEqual(report['groups'][0]['run_ids'], ['run-a', 'run-b'])


class CompareRejectionTests(FixtureTestMixin, unittest.TestCase):
    """每个负例：exit 2、stderr 含具体信息、不留下输出文件。"""

    def assert_rejected(self, runs, fragment):
        output = self.tmp / 'report.json'
        result = self.run_cli(runs, output)
        self.assertEqual(result.returncode, 2, f'期望 exit 2，实际 0：{result.stdout}')
        self.assertIn(fragment, result.stderr)
        self.assertFalse(output.exists(), '失败时不得留下看似成功的报告')

    def test_headless_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'observed': {'headless': True, 'rendering_device': True,
                                                              'rendering_method': 'forward_plus',
                                                              'rendering_driver': 'metal', 'gpu': GPU,
                                                              'gpu_api_version': '4.0',
                                                              'window_pixels': [1920, 1200],
                                                              'viewport_size': [1920, 1200],
                                                              'robots': 12, 'facilities': 6}})
        self.assert_rejected([run_a], 'headless')

    def test_graphics_ineligible_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'graphical_performance_eligible': False})
        self.assert_rejected([run_a], 'graphical_performance_eligible')

    def test_not_available_driver_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'observed': {'headless': False, 'rendering_device': True,
                                                              'rendering_method': 'forward_plus',
                                                              'rendering_driver': 'NOT_AVAILABLE', 'gpu': GPU,
                                                              'gpu_api_version': '4.0',
                                                              'window_pixels': [1920, 1200],
                                                              'viewport_size': [1920, 1200],
                                                              'robots': 12, 'facilities': 6}})
        self.assert_rejected([run_a], 'rendering_driver')

    def test_missing_field_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'observed': {'headless': False, 'rendering_device': True,
                                                              'rendering_method': 'forward_plus',
                                                              'rendering_driver': 'metal',
                                                              'gpu_api_version': '4.0',
                                                              'window_pixels': [1920, 1200],
                                                              'viewport_size': [1920, 1200],
                                                              'robots': 12, 'facilities': 6}})
        self.assert_rejected([run_a], 'observed.gpu')

    def test_missing_build_identity_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'build': {'git_head': '',
                                                           'source_snapshot_sha256': 'e178163e',
                                                           'assets_snapshot_sha256': 'a82e1f56'}})
        self.assert_rejected([run_a], 'build.git_head')

    def test_frames_summary_contradiction_rejected(self):
        run_a = self.make_run('a', run_id='run-a', summary_mutations={'frames': 6})
        self.assert_rejected([run_a], 'frames')

    def test_truncated_csv_rejected(self):
        run_a = self.make_run('a', run_id='run-a')
        csv_path = run_a / 'frames.csv'
        lines = csv_path.read_text().splitlines()
        csv_path.write_text('\n'.join(lines[:-1]) + '\n', encoding='utf-8')
        self.assert_rejected([run_a], 'frames')

    def test_csv_summary_percentile_contradiction_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              summary_mutations={'frame_time_ms': {'p50': 1.0, 'p95': 2.0, 'p99': 3.0,
                                                                   'max': 4.0, 'over_33ms': 0,
                                                                   'method': 'x'}})
        self.assert_rejected([run_a], 'frame_time_ms.p50')

    def test_hash_mismatch_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              verification_mutations={'recomputed': {
                                  'frames': 7, 'duration_csv_s': 0.41,
                                  'avg_fps': 17.073170731707317,
                                  'nearest_rank_ms': {'50': 40, '95': 200, '99': 200},
                                  'max_ms': 200, 'over_33ms': 0,
                                  'csv_sha256': '0' * 64}})
        self.assert_rejected([run_a], 'csv_sha256')

    def test_duplicate_run_id_rejected(self):
        run_a = self.make_run('a', run_id='same-id')
        run_b = self.make_run('b', run_id='same-id')
        self.assert_rejected([run_a, run_b], 'run_id')

    def test_invalid_numeric_rejected(self):
        run_a = self.make_run('a', run_id='run-a')
        csv_path = run_a / 'frames.csv'
        text = csv_path.read_text().replace(',30.000000,', ',-30.000000,')
        csv_path.write_text(text, encoding='utf-8')
        self.assert_rejected([run_a], 'frame_ms')

    def test_non_increasing_time_s_rejected(self):
        run_a = self.make_run('a', run_id='run-a')
        csv_path = run_a / 'frames.csv'
        lines = csv_path.read_text().splitlines()
        parts = lines[2].split(',')
        parts[1] = '0.000000'
        lines[2] = ','.join(parts)
        csv_path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
        self.assert_rejected([run_a], 'time_s')

    def test_verification_exit_code_rejected(self):
        run_a = self.make_run('a', run_id='run-a', verification_mutations={'exit_code': 1})
        self.assert_rejected([run_a], 'exit_code')

    def test_bad_memory_type_rejected(self):
        run_a = self.make_run('a', run_id='run-a',
                              verification_mutations={'memory': {'maximum_resident_set_size_bytes': -5,
                                                                 'peak_memory_footprint_bytes': 684082520}})
        self.assert_rejected([run_a], 'memory')

    def test_missing_input_file_rejected(self):
        run_a = self.make_run('a', run_id='run-a')
        (run_a / 'verification.json').unlink()
        output = self.tmp / 'report.json'
        result = self.run_cli([run_a], output)
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn('verification.json', result.stderr)
        self.assertFalse(output.exists())

    def test_missing_run_dir_rejected(self):
        self.assert_rejected([self.tmp / 'no-such-dir'], 'RUN_DIR')


class CompareSchemaDepthTests(FixtureTestMixin, unittest.TestCase):
    """requested/applied 嵌套结构与顶层/编码/极大整数负例：exit2、具体 role.field、无 traceback。"""

    def setUp(self):
        super().setUp()
        self.output = self.tmp / 'report.json'

    def run_cli_expect(self, run, fragment=None, absent=()):
        result = self.run_cli([run], self.output)
        self.assertEqual(result.returncode, 2, f'期望 exit 2，实际 {result.returncode}：{result.stdout}')
        self.assertNotIn('Traceback', result.stderr)
        if fragment:
            self.assertIn(fragment, result.stderr)
        for text in absent:
            self.assertNotIn(text, result.stderr)
        self.assertFalse(self.output.exists())
        return result

    def test_empty_requested_rejected(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={'requested': {}})
        self.run_cli_expect(run, 'summary.requested')

    def test_requested_width_string_rejected(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'requested': {'resolution': {'width': '1920', 'height': 1200},
                          'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
                          'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6, 'ring_radius': 14}}})
        self.run_cli_expect(run, 'summary.requested.resolution.width')

    def test_requested_missing_quality_rejected(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'requested': {'resolution': {'width': 1920, 'height': 1200},
                          'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6, 'ring_radius': 14}}})
        self.run_cli_expect(run, 'summary.requested.quality')

    def test_applied_nan_rejected(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'applied': {'msaa_3d': 'Msaa4X', 'fxaa': 'Disabled', 'scaling_3d_scale': float('nan'),
                        'vsync': 'Disabled', 'max_fps': 0}})
        self.run_cli_expect(run, 'summary.applied.scaling_3d_scale')

    def test_applied_bool_not_accepted_as_int(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'applied': {'msaa_3d': 'Msaa4X', 'fxaa': 'Disabled', 'scaling_3d_scale': 1,
                        'vsync': 'Disabled', 'max_fps': True}})
        self.run_cli_expect(run, 'summary.applied.max_fps')

    def test_requested_extra_key_huge_int_rejected(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'requested': {'resolution': {'width': 1920, 'height': 1200},
                          'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
                          'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6, 'ring_radius': 14},
                          'extra': 10 ** 400}})
        self.run_cli_expect(run, 'summary.requested.extra')

    def test_ring_radius_fractional_accepted(self):
        """源码 RingRadius 为 float（RangeFloat），14.5 是合法记录，不得收窄为整数。"""
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'requested': {'resolution': {'width': 1920, 'height': 1200},
                          'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
                          'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6,
                                    'ring_radius': 14.5}}})
        result = self.run_cli([run], self.output)
        self.assertEqual(result.returncode, 0, result.stderr)
        group = json.loads(self.output.read_text())['groups'][0]
        self.assertEqual(group['identity']['summary.requested']['scale']['ring_radius'], 14.5)

    def test_ring_radius_bad_type_rejected(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'requested': {'resolution': {'width': 1920, 'height': 1200},
                          'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
                          'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6,
                                    'ring_radius': '14.5'}}})
        self.run_cli_expect(run, 'summary.requested.scale.ring_radius')
        run = self.make_run('b', run_id='run-b', summary_mutations={
            'requested': {'resolution': {'width': 1920, 'height': 1200},
                          'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
                          'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6,
                                    'ring_radius': True}}})
        self.run_cli_expect(run, 'summary.requested.scale.ring_radius')

    def test_full_object_kept_in_identity(self):
        """额外键合法时身份仍含完整对象（不丢完整 requested）。"""
        run_a = self.make_run('a', run_id='run-a', summary_mutations={'requested': {
            'resolution': {'width': 1920, 'height': 1200},
            'quality': {'msaa_3d': 4, 'fxaa': False, 'scaling_3d_scale': 1, 'shadows': True},
            'scale': {'robots_total': 12, 'robots_per_type': 4, 'facilities': 6, 'ring_radius': 14},
            'extra_note': 'hi'}})
        run_b = self.make_run('b', run_id='run-b')
        result = self.run_cli([run_a, run_b], self.output)
        self.assertEqual(result.returncode, 0, result.stderr)
        groups = json.loads(self.output.read_text())['groups']
        self.assertEqual(len(groups), 2, '额外键改变完整身份，必须分两组')

    def test_summary_top_level_list_rejected_without_traceback(self):
        run = self.make_run('a', run_id='run-a')
        (run / 'summary.json').write_text('[]', encoding='utf-8')
        self.run_cli_expect(run, 'field=summary 顶层必须是对象', absent=('Traceback', str(self.tmp)))

    def test_verification_top_level_list_rejected_without_traceback(self):
        run = self.make_run('a', run_id='run-a')
        (run / 'verification.json').write_text('[]', encoding='utf-8')
        self.run_cli_expect(run, 'field=verification 顶层必须是对象', absent=('Traceback', str(self.tmp)))

    def test_summary_invalid_utf8_rejected_without_traceback(self):
        run = self.make_run('a', run_id='run-a')
        (run / 'summary.json').write_bytes(b'\xff\xfe{"schema_version": 1}')
        self.run_cli_expect(run, 'summary.json: 非法 UTF-8', absent=('Traceback', str(self.tmp)))

    def test_bad_json_error_names_role(self):
        run = self.make_run('a', run_id='run-a')
        (run / 'summary.json').write_text('{bad', encoding='utf-8')
        self.run_cli_expect(run, 'summary.json: JSON 解析失败')
        (run / 'summary.json').write_text('{}', encoding='utf-8')
        (run / 'verification.json').write_text('{bad', encoding='utf-8')
        self.run_cli_expect(run, 'verification.json: JSON 解析失败')

    def test_huge_int_rejected_without_overflow(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={'avg_fps': 10 ** 400})
        self.run_cli_expect(run, 'field=avg_fps 必须为有限数值', absent=('OverflowError', 'Traceback'))

    def test_huge_int_percentile_rejected_without_overflow(self):
        run = self.make_run('a', run_id='run-a', summary_mutations={
            'frame_time_ms': {'p50': 10 ** 400, 'p95': 200.0, 'p99': 200.0, 'max': 200.0,
                              'over_33ms': 0, 'method': 'x'}})
        self.run_cli_expect(run, 'field=frame_time_ms.p50 必须为有限数值', absent=('OverflowError', 'Traceback'))

    @unittest.skipIf(hasattr(os, 'geteuid') and os.geteuid() == 0, 'root 不受 0o000 限制')
    def test_unreadable_frames_csv_rejected_without_traceback(self):
        run = self.make_run('a', run_id='run-a')
        (run / 'frames.csv').chmod(0o000)
        self.addCleanup(lambda: (run / 'frames.csv').chmod(0o644))
        self.run_cli_expect(run, 'frames.csv: 读取失败', absent=('Traceback', str(self.tmp)))

    def test_output_parent_is_file_rejected_without_traceback(self):
        run = self.make_run('a', run_id='run-a')
        blocker = self.tmp / 'blocker'
        blocker.write_text('x', encoding='utf-8')
        result = self.run_cli([run], blocker / 'report.json')
        self.assertEqual(result.returncode, 2, result.stdout)
        self.assertNotIn('Traceback', result.stderr)
        self.assertNotIn(str(self.tmp), result.stderr)
        self.assertIn('report.json', result.stderr)


class CompareOutputExclusivityTests(FixtureTestMixin, unittest.TestCase):
    """输出最终目标独占创建：悬空/实体 symlink 拒绝且原样保留；并发恰一成功。"""

    def setUp(self):
        super().setUp()
        self.output = self.tmp / 'report.json'

    def test_dangling_symlink_output_rejected_and_untouched(self):
        run = self.make_run('a', run_id='run-a')
        target = self.tmp / 'never-created.json'
        self.output.symlink_to(target)
        result = self.run_cli([run], self.output)
        self.assertEqual(result.returncode, 2, result.stdout)
        self.assertIn('report.json', result.stderr)
        self.assertNotIn(os.path.expanduser('~'), result.stderr)
        self.assertTrue(self.output.is_symlink())
        self.assertEqual(os.readlink(self.output), str(target))
        self.assertFalse(target.exists())

    def test_symlink_to_existing_file_rejected_and_target_untouched(self):
        run = self.make_run('a', run_id='run-a')
        target = self.tmp / 'real-target.json'
        target.write_text('SENTINEL', encoding='utf-8')
        self.output.symlink_to(target)
        result = self.run_cli([run], self.output)
        self.assertEqual(result.returncode, 2, result.stdout)
        self.assertEqual(target.read_text(), 'SENTINEL')
        self.assertTrue(self.output.is_symlink())

    def test_concurrent_writers_exactly_one_success(self):
        run_a = self.make_run('a', run_id='run-a')
        run_b = self.make_run('b', run_id='run-b')
        argv = [sys.executable, str(COMPARE_PY), '--runs']
        procs = [subprocess.Popen(argv + [str(run), '--output', str(self.output)],
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, cwd=self.tmp)
                 for run in (run_a, run_b)]
        results = [proc.communicate() + (proc.returncode,) for proc in procs]
        self.assertEqual(sorted(code for _, _, code in results), [0, 2],
                         f'必须恰一成功一拒绝: {results}')
        report = json.loads(self.output.read_text())
        self.assertEqual(len(report['runs']), 1)
        for _, stderr, _ in results:
            self.assertNotIn('Traceback', stderr)


@unittest.skipUnless(EVIDENCE.is_dir(), '仓库内真实三轮证据不存在')
class RealEvidenceTests(FixtureTestMixin, unittest.TestCase):
    """真实三轮：同一分组，保留金样 max=1053.338/73.745/77.351，报告不泄露绝对路径。"""

    def test_real_three_runs_single_group_with_golden_maxima(self):
        run_dirs = [EVIDENCE / f'run-{n}' for n in (1, 2, 3)]
        golden_max = {'run-1': 1053.338, 'run-2': 73.745, 'run-3': 77.351}
        output = self.tmp / 'real-report.json'
        result = self.run_cli(run_dirs, output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(len(report['groups']), 1)
        self.assertEqual(len(report['groups'][0]['run_ids']), 3)
        for name, expected in golden_max.items():
            matching = [e for e in report['runs'] if e['run_dir'].endswith(name)]
            self.assertEqual(len(matching), 1, name)
            self.assertEqual(matching[0]['recomputed']['max_ms'], expected)
            self.assertGreater(matching[0]['memory']['maximum_resident_set_size_bytes'], 0)
            self.assertGreater(matching[0]['memory']['peak_memory_footprint_bytes'], 0)
        text = output.read_text()
        self.assertNotIn(str(REPO_ROOT), text)
        self.assertNotIn(os.path.expanduser('~'), text)

    def test_real_runs_relative_paths_from_foreign_cwd(self):
        """门禁条件：从仓库外 cwd 用相对仓库根的 --runs 运行真实三轮。"""
        run_args = [f'docs/engineering/evidence/2026-10-04-takeover/metal-native/run-{n}' for n in (1, 2, 3)]
        output = self.tmp / 'real-report-rel.json'
        argv = [str(COMPARE_PY), '--runs', *run_args, '--output', str(output)]
        result = subprocess.run([sys.executable, *argv], capture_output=True, text=True, cwd=self.tmp)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text())
        self.assertEqual(len(report['groups']), 1)
        self.assertEqual([e['run_dir'] for e in report['runs']],
                         [f'docs/engineering/evidence/2026-10-04-takeover/metal-native/run-{n}' for n in (1, 2, 3)])
        self.assertEqual(sorted(e['recomputed']['max_ms'] for e in report['runs']),
                         [73.745, 77.351, 1053.338])


if __name__ == '__main__':
    unittest.main()
