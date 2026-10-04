#!/usr/bin/env python3
"""PERF-01-COMPARE · 多轮性能证据比较器。

用法：
    python3 tools/perf_compare/compare.py --runs RUN_DIR [RUN_DIR ...] --output NEW_REPORT.json

RUN_DIR 相对路径相对仓库根（由脚本位置定位），--output 相对路径同样相对仓库根；
绝对路径照常。输出必须是尚不存在的新文件。输入只读，不复制原始帧，
保留所有帧（含启动尖刺），不输出稳定/达标结论。
统计经仓库根 tools/measure_baseline.py 的 recompute() 复算，本工具不复制其算法。
"""
import argparse
import csv
import hashlib
import importlib.util
import json
import math
import os
import statistics
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CSV_COLUMNS = ['frame', 'time_s', 'frame_ms', 'fps']
GRAPHICS_STR_FIELDS = ('rendering_method', 'rendering_driver', 'gpu', 'gpu_api_version')
NOT_AVAILABLE = 'NOT_AVAILABLE'
DURATION_TOLERANCE_S = 1e-5
MS_TOLERANCE = 1e-5

# 按真实 summary（docs/engineering/evidence/2026-10-04-takeover/metal-native/run-1..3）
# 的 requested/applied 嵌套结构定义必需字段与类型；值域不做限制（不臆造配置约束）。
# dict=嵌套对象；bool/int/str=精确类型（int 拒绝布尔冒充）；float=有限数值（int/float 均可）。
CONFIG_SHAPE = {
    'requested': {
        'resolution': {'width': int, 'height': int},
        'quality': {'msaa_3d': int, 'fxaa': bool, 'scaling_3d_scale': float, 'shadows': bool},
        'scale': {'robots_total': int, 'robots_per_type': int, 'facilities': int, 'ring_radius': int},
    },
    'applied': {
        'msaa_3d': str, 'fxaa': str, 'scaling_3d_scale': float, 'vsync': str, 'max_fps': int,
    },
}


class InvalidInput(Exception):
    """无效输入/输出，带具体 path/field，CLI 以 exit 2 报告。"""


def display(path):
    """路径只以相对定位呈现，不泄露用户目录绝对路径。"""
    path = Path(path)
    try:
        rel = path.resolve().relative_to(REPO_ROOT)
        return str(rel)
    except ValueError:
        return path.name


def resolve_arg(value):
    path = Path(value)
    return path if path.is_absolute() else REPO_ROOT / path


def sha256_file(path):
    try:
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()
    except OSError as exc:
        raise InvalidInput(f'{display(path)}: 读取失败 ({exc.strerror})') from exc


def is_num(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return False
    try:
        return math.isfinite(value)
    except OverflowError:
        return False  # 超出浮点表示范围的 JSON 极大整数按非有限数值拒绝


def is_int(value):
    return isinstance(value, int) and not isinstance(value, bool)


def require(condition, message):
    if not condition:
        raise InvalidInput(message)


def load_measure_baseline():
    path = REPO_ROOT / 'tools' / 'measure_baseline.py'
    require(path.is_file(), f'仓库缺少统计模块: {display(path)}')
    spec = importlib.util.spec_from_file_location('measure_baseline', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def check_frames_csv(csv_path):
    """四列、连续 frame、有限正 frame_ms/fps、有限严格递增 time_s；只读。"""
    try:
        with csv_path.open(newline='', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            require(reader.fieldnames == CSV_COLUMNS,
                    f'{display(csv_path)}: CSV 表头必须恰为 {CSV_COLUMNS}，实际 {reader.fieldnames}')
            rows = list(reader)
    except UnicodeDecodeError as exc:
        raise InvalidInput(f'{display(csv_path)}: 非法 UTF-8 ({exc})') from exc
    except OSError as exc:
        raise InvalidInput(f'{display(csv_path)}: 读取失败 ({exc.strerror})') from exc
    require(rows, f'{display(csv_path)}: 没有帧数据')
    prev_time = None
    for index, row in enumerate(rows, start=1):
        where = f'{display(csv_path)}: 第 {index} 行'
        require(None not in row and all(row[c] not in (None, '') for c in CSV_COLUMNS),
                f'{where}: 缺列或多余列')
        try:
            frame = int(row['frame'])
            time_s = float(row['time_s'])
            frame_ms = float(row['frame_ms'])
            fps = float(row['fps'])
        except ValueError as exc:
            raise InvalidInput(f'{where}: 数值无法解析 ({exc})') from exc
        require(frame == index, f'{where}: frame 不连续，期望 {index}，实际 {frame}')
        require(math.isfinite(time_s), f'{where}: time_s 非有限')
        require(math.isfinite(frame_ms) and frame_ms > 0, f'{where}: frame_ms 必须为有限正数，实际 {row["frame_ms"]}')
        require(math.isfinite(fps) and fps > 0, f'{where}: fps 必须为有限正数，实际 {row["fps"]}')
        if prev_time is not None:
            require(time_s > prev_time, f'{where}: time_s 非严格递增 ({prev_time} -> {time_s})')
        prev_time = time_s
    return len(rows)


def approx_diff(actual, expected):
    return abs(actual - expected)


def check_summary(summary, csv_path, frames_count, recomputed):
    require(isinstance(summary, dict), f'{display(csv_path.parent / "summary.json")}: summary 不是 JSON 对象')
    where = f'{display(csv_path.parent / "summary.json")}'
    require(summary.get('schema_version') == 1 and is_int(summary.get('schema_version')),
            f'{where}: field=schema_version 必须=1')
    require(summary.get('status') == 'completed', f'{where}: field=status 必须=completed，实际 {summary.get("status")!r}')
    run_id = summary.get('run_id')
    require(isinstance(run_id, str) and run_id != '', f'{where}: field=run_id 必须为非空字符串')
    require(is_int(summary.get('frames')), f'{where}: field=frames 必须为整数')
    require(summary['frames'] == frames_count,
            f'{where}: field=frames={summary["frames"]} 与 CSV 帧数 {frames_count} 不一致')
    require(is_num(summary.get('duration_actual_s')), f'{where}: field=duration_actual_s 必须为有限数值')
    require(approx_diff(summary['duration_actual_s'], recomputed['duration_csv_s']) <= DURATION_TOLERANCE_S,
            f'{where}: field=duration_actual_s={summary["duration_actual_s"]} 与 CSV 最后 time_s='
            f'{recomputed["duration_csv_s"]} 相差超过 {DURATION_TOLERANCE_S}s')
    require(is_num(summary.get('avg_fps')), f'{where}: field=avg_fps 必须为有限数值')
    tolerance = max(1e-4, abs(summary['avg_fps']) * 1e-6)
    require(approx_diff(summary['avg_fps'], recomputed['avg_fps']) <= tolerance,
            f'{where}: field=avg_fps={summary["avg_fps"]} 与复算 {recomputed["avg_fps"]} 相差超过 {tolerance}')
    frame_time = summary.get('frame_time_ms')
    require(isinstance(frame_time, dict), f'{where}: field=frame_time_ms 必须为对象')
    for name, key in (('p50', '50'), ('p95', '95'), ('p99', '99')):
        require(is_num(frame_time.get(name)), f'{where}: field=frame_time_ms.{name} 必须为有限数值')
        require(approx_diff(frame_time[name], recomputed['nearest_rank_ms'][key]) <= MS_TOLERANCE,
                f'{where}: field=frame_time_ms.{name}={frame_time[name]} 与复算 '
                f'{recomputed["nearest_rank_ms"][key]} 相差超过 {MS_TOLERANCE}ms')
    require(is_num(frame_time.get('max')), f'{where}: field=frame_time_ms.max 必须为有限数值')
    require(approx_diff(frame_time['max'], recomputed['max_ms']) <= MS_TOLERANCE,
            f'{where}: field=frame_time_ms.max={frame_time["max"]} 与复算 {recomputed["max_ms"]} 相差超过 {MS_TOLERANCE}ms')
    require(is_int(frame_time.get('over_33ms')), f'{where}: field=frame_time_ms.over_33ms 必须为整数')
    require(frame_time['over_33ms'] == recomputed['over_33ms'],
            f'{where}: field=frame_time_ms.over_33ms={frame_time["over_33ms"]} 与复算 {recomputed["over_33ms"]} 不一致')
    return run_id


def check_verification(verification, csv_path, csv_sha256, recomputed):
    path = csv_path.parent / 'verification.json'
    where = f'{display(path)}'
    require(isinstance(verification, dict), f'{where}: verification 不是 JSON 对象')
    require(is_int(verification.get('exit_code')) and verification['exit_code'] == 0,
            f'{where}: field=exit_code 必须=0，实际 {verification.get("exit_code")!r}')
    memory = verification.get('memory')
    require(isinstance(memory, dict), f'{where}: field=memory 必须为对象')
    for field in ('maximum_resident_set_size_bytes', 'peak_memory_footprint_bytes'):
        require(is_int(memory.get(field)) and memory[field] > 0,
                f'{where}: field=memory.{field} 必须为正整数，实际 {memory.get(field)!r}')
    recomputed_recorded = verification.get('recomputed')
    require(isinstance(recomputed_recorded, dict), f'{where}: field=recomputed 必须为对象')
    require(recomputed_recorded.get('csv_sha256') == csv_sha256,
            f'{where}: field=recomputed.csv_sha256={recomputed_recorded.get("csv_sha256")!r} '
            f'与实际文件 SHA256 {csv_sha256} 不一致')
    require(is_int(recomputed_recorded.get('frames')) and recomputed_recorded['frames'] == recomputed['frames'],
            f'{where}: field=recomputed.frames 与实际不一致')
    require(is_num(recomputed_recorded.get('duration_csv_s'))
            and approx_diff(recomputed_recorded['duration_csv_s'], recomputed['duration_csv_s']) <= DURATION_TOLERANCE_S,
            f'{where}: field=recomputed.duration_csv_s 与实际不一致')
    require(is_num(recomputed_recorded.get('avg_fps')), f'{where}: field=recomputed.avg_fps 必须为有限数值')
    tolerance = max(1e-4, abs(recomputed_recorded['avg_fps']) * 1e-6)
    require(approx_diff(recomputed_recorded['avg_fps'], recomputed['avg_fps']) <= tolerance,
            f'{where}: field=recomputed.avg_fps 与实际不一致')
    recorded_ranks = recomputed_recorded.get('nearest_rank_ms')
    require(isinstance(recorded_ranks, dict), f'{where}: field=recomputed.nearest_rank_ms 必须为对象')
    for key in ('50', '95', '99'):
        require(is_num(recorded_ranks.get(key)), f'{where}: field=recomputed.nearest_rank_ms.{key} 必须为有限数值')
        require(approx_diff(recorded_ranks[key], recomputed['nearest_rank_ms'][key]) <= MS_TOLERANCE,
                f'{where}: field=recomputed.nearest_rank_ms.{key} 与实际不一致')
    require(is_num(recomputed_recorded.get('max_ms'))
            and approx_diff(recomputed_recorded['max_ms'], recomputed['max_ms']) <= MS_TOLERANCE,
            f'{where}: field=recomputed.max_ms 与实际不一致')
    require(is_int(recomputed_recorded.get('over_33ms')) and recomputed_recorded['over_33ms'] == recomputed['over_33ms'],
            f'{where}: field=recomputed.over_33ms 与实际不一致')
    return memory


def get_nonempty(mapping, key, where, label=None):
    label = label or key
    require(isinstance(mapping, dict), f'{where}: {label} 的父级必须是对象')
    value = mapping.get(key)
    require(isinstance(value, str) and value != '', f'{where}: field={label} 必须为非空字符串')
    return value


def check_graphics_eligible(summary, where):
    require(summary.get('graphical_performance_eligible') is True,
            f'{where}: field=graphical_performance_eligible 必须=true')
    observed = summary.get('observed')
    require(isinstance(observed, dict), f'{where}: field=observed 必须为对象')
    require(observed.get('headless') is False, f'{where}: field=observed.headless 必须=false')
    require(observed.get('rendering_device') is True, f'{where}: field=observed.rendering_device 必须=true')
    for field in GRAPHICS_STR_FIELDS:
        value = observed.get(field)
        require(isinstance(value, str) and value != '',
                f'{where}: field=observed.{field} 缺失或为空')
        require(value != NOT_AVAILABLE, f'{where}: field=observed.{field}={NOT_AVAILABLE}')


def check_config(config, shape, where, label):
    """嵌套必需字段/类型/有限值校验；额外键不限制，身份仍携带完整对象。"""
    require(isinstance(config, dict), f'{where}: field={label} 必须为对象')
    reject_non_finite(config, f'{label}', where)
    for key, spec in shape.items():
        require(key in config, f'{where}: field={label}.{key} 缺失')
        value = config[key]
        if isinstance(spec, dict):
            check_config(value, spec, where, f'{label}.{key}')
        elif spec is bool:
            require(isinstance(value, bool), f'{where}: field={label}.{key} 必须为布尔，实际 {value!r}')
        elif spec is int:
            require(is_int(value), f'{where}: field={label}.{key} 必须为整数，实际 {value!r}')
        elif spec is str:
            require(isinstance(value, str), f'{where}: field={label}.{key} 必须为字符串，实际 {value!r}')
        else:
            require(is_num(value), f'{where}: field={label}.{key} 必须为有限数值，实际 {value!r}')


def reject_non_finite(node, label, where):
    """requested/applied 任何位置（含额外键）出现 NaN/Inf/超浮点范围整数都拒绝。"""
    if isinstance(node, dict):
        for key, value in node.items():
            reject_non_finite(value, f'{label}.{key}', where)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            reject_non_finite(value, f'{label}[{index}]', where)
    elif isinstance(node, (int, float)) and not isinstance(node, bool):
        require(is_num(node), f'{where}: field={label} 必须为有限数值，实际 {node!r}')


def build_identity(summary, verification, where_summary, where_verification):
    """按包文档的精确字段路径组成 canonical 身份；不从目录名猜身份。"""
    observed = summary.get('observed')
    runtime = summary.get('runtime')
    build = summary.get('build')
    require(isinstance(runtime, dict), f'{where_summary}: field=runtime 必须为对象')
    require(isinstance(build, dict), f'{where_summary}: field=build 必须为对象')
    identity = {
        'summary.build.git_head': get_nonempty(build, 'git_head', where_summary, 'summary.build.git_head'),
        'summary.build.source_snapshot_sha256': get_nonempty(
            build, 'source_snapshot_sha256', where_summary, 'summary.build.source_snapshot_sha256'),
        'summary.build.assets_snapshot_sha256': get_nonempty(
            build, 'assets_snapshot_sha256', where_summary, 'summary.build.assets_snapshot_sha256'),
        'summary.assembly_sha256': get_nonempty(summary, 'assembly_sha256', where_summary, 'summary.assembly_sha256'),
        'verification.app_binary_sha256': get_nonempty(
            verification, 'app_binary_sha256', where_verification, 'verification.app_binary_sha256'),
    }
    identity['summary.fixture_sha256'] = get_nonempty(summary, 'fixture_sha256', where_summary, 'summary.fixture_sha256')
    require(is_int(summary.get('seed')), f'{where_summary}: field=seed 必须为整数')
    identity['summary.seed'] = summary['seed']
    require(is_num(summary.get('duration_requested_s')),
            f'{where_summary}: field=duration_requested_s 必须为有限数值')
    identity['summary.duration_requested_s'] = summary['duration_requested_s']
    for field in ('requested', 'applied'):
        check_config(summary.get(field), CONFIG_SHAPE[field], where_summary, f'summary.{field}')
        identity[f'summary.{field}'] = summary[field]
    for field in GRAPHICS_STR_FIELDS:
        identity[f'summary.observed.{field}'] = observed[field]
    for field in ('window_pixels', 'viewport_size'):
        value = observed.get(field)
        require(isinstance(value, list) and len(value) == 2 and all(is_num(v) for v in value),
                f'{where_summary}: field=observed.{field} 必须为两个有限数值的列表')
        identity[f'summary.observed.{field}'] = value
    for field in ('robots', 'facilities'):
        require(is_int(observed.get(field)), f'{where_summary}: field=observed.{field} 必须为整数')
        identity[f'summary.observed.{field}'] = observed[field]
    identity['summary.godot_version'] = get_nonempty(summary, 'godot_version', where_summary, 'summary.godot_version')
    for field in ('framework', 'version', 'architecture', 'os'):
        identity[f'summary.runtime.{field}'] = get_nonempty(
            runtime, field, where_summary, f'summary.runtime.{field}')
    return identity


def group_id_for(identity):
    canonical = json.dumps(identity, sort_keys=True, separators=(',', ':'), ensure_ascii=True)
    return 'grp-' + hashlib.sha256(canonical.encode('utf-8')).hexdigest()


def load_json(path):
    """读 JSON：坏 JSON/非法 UTF-8/IO 都以带具体文件的 InvalidInput 报告，不泄露绝对路径。"""
    try:
        return json.loads(path.read_text(encoding='utf-8'))
    except UnicodeDecodeError as exc:
        raise InvalidInput(f'{display(path)}: 非法 UTF-8 ({exc})') from exc
    except json.JSONDecodeError as exc:
        raise InvalidInput(f'{display(path)}: JSON 解析失败 ({exc})') from exc
    except OSError as exc:
        raise InvalidInput(f'{display(path)}: 读取失败 ({exc.strerror})') from exc


def load_run(run_arg, baseline):
    """返回 (run 记录, canonical identity)；输入只读。"""
    run_dir = resolve_arg(run_arg)
    require(run_dir.is_dir(), f'RUN_DIR 不存在或不是目录: {display(run_dir)}')
    run_dir = run_dir.resolve()
    csv_path = run_dir / 'frames.csv'
    summary_path = run_dir / 'summary.json'
    verification_path = run_dir / 'verification.json'
    for path in (csv_path, summary_path, verification_path):
        require(path.is_file(), f'缺少输入文件: {display(path)}')

    frames_count = check_frames_csv(csv_path)
    try:
        recomputed = baseline.recompute(csv_path)
    except OSError as exc:
        raise InvalidInput(f'{display(csv_path)}: 复算读取失败 ({exc.strerror})') from exc
    except ValueError as exc:
        raise InvalidInput(f'{display(csv_path)}: 复算失败 ({exc})') from exc
    require(recomputed['frames'] == frames_count, f'{display(csv_path)}: 复算帧数与逐行检查不一致')

    summary = load_json(summary_path)
    verification = load_json(verification_path)

    where_summary = f'{display(summary_path)}'
    where_verification = f'{display(verification_path)}'
    require(isinstance(summary, dict),
            f'{where_summary}: field=summary 顶层必须是对象，实际 {type(summary).__name__}')
    require(isinstance(verification, dict),
            f'{where_verification}: field=verification 顶层必须是对象，实际 {type(verification).__name__}')
    check_graphics_eligible(summary, where_summary)
    run_id = check_summary(summary, csv_path, frames_count, recomputed)
    csv_sha256 = sha256_file(csv_path)
    memory = check_verification(verification, csv_path, csv_sha256, recomputed)
    identity = build_identity(summary, verification, where_summary, where_verification)

    record = {
        'run_id': run_id,
        'run_dir': display(run_dir),
        'input_sha256': {
            'frames.csv': csv_sha256,
            'summary.json': sha256_file(summary_path),
            'verification.json': sha256_file(verification_path),
        },
        'group_id': group_id_for(identity),
        'recomputed': recomputed,
        'memory': {
            'maximum_resident_set_size_bytes': memory['maximum_resident_set_size_bytes'],
            'peak_memory_footprint_bytes': memory['peak_memory_footprint_bytes'],
        },
    }
    return record, identity


def group_metrics(values):
    return {
        'min': min(values),
        'max': max(values),
        'median': statistics.median(values),
        'samples': len(values),
    }


def group_runs(runs, identities):
    groups = {}
    for run in runs:
        groups.setdefault(run['group_id'], []).append(run)
    result = []
    for group_id in sorted(groups):
        members = sorted(groups[group_id], key=lambda r: r['run_id'])
        metrics = {
            'p50_ms': group_metrics([m['recomputed']['nearest_rank_ms']['50'] for m in members]),
            'p95_ms': group_metrics([m['recomputed']['nearest_rank_ms']['95'] for m in members]),
            'p99_ms': group_metrics([m['recomputed']['nearest_rank_ms']['99'] for m in members]),
            'max_ms': group_metrics([m['recomputed']['max_ms'] for m in members]),
            'avg_fps': group_metrics([m['recomputed']['avg_fps'] for m in members]),
        }
        result.append({
            'group_id': group_id,
            'identity': identities[group_id],
            'run_ids': [m['run_id'] for m in members],
            'metrics': metrics,
        })
    return result


LIMITATIONS = [
    '同身份分组只代表可比较配置一致；现有证据不含前台/后台、温度与电源状态记录，不能声称这些变量受控。',
    '负载为渲染灰盒边界：固定巡逻与动画，无 AI/寻路/存档/动态地形（summary.load_scope），结论仅适用于该负载。',
    '内存指标来自外部 /usr/bin/time -l，覆盖进程整个生命周期（含启动）；summary.memory 仅为缺证占位字符串，未作为进程内存使用。',
    '报告不合并原始帧、不剔除启动尖刺；不同身份各自成组，禁止跨组比较、跨组声称性能改善或输出单一总体指标。',
    '本工具只做离线比较；PERF-01 受控实机采样、原因裁决与稳定性能仍未完成。',
]


def build_report_and_groups(run_args):
    baseline = load_measure_baseline()
    runs = []
    identities = {}
    for arg in run_args:
        run, identity = load_run(arg, baseline)
        runs.append(run)
        identities.setdefault(run['group_id'], identity)
    run_ids = [run['run_id'] for run in runs]
    duplicates = sorted({rid for rid in run_ids if run_ids.count(rid) > 1})
    require(not duplicates, f'重复 run_id: {duplicates}（拒绝比较）')
    return {
        'schema_version': 1,
        'runs': runs,
        'groups': group_runs(runs, identities),
        'limitations': LIMITATIONS,
    }


def write_report(report, output_path):
    """最终目标独占创建（O_EXCL）：已存在文件、悬空 symlink、并发竞态一律 EEXIST 拒绝，
    原目标/链接内容不变；写入中断则删除本次创建的文件，不留看似成功的报告。"""
    try:
        output_path.parent.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        raise InvalidInput(f'{display(output_path)}: 无法创建输出目录 ({exc.strerror})') from exc
    try:
        fd = os.open(output_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o644)
    except FileExistsError as exc:
        raise InvalidInput(f'输出已存在，不覆盖（含悬空链接/并发创建）: {display(output_path)}') from exc
    except OSError as exc:
        raise InvalidInput(f'{display(output_path)}: 无法创建输出文件 ({exc.strerror})') from exc
    try:
        with os.fdopen(fd, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
            f.write('\n')
    except OSError as exc:
        remove_quiet(output_path)
        raise InvalidInput(f'{display(output_path)}: 写入失败 ({exc.strerror})') from exc
    except BaseException:
        remove_quiet(output_path)
        raise


def remove_quiet(path):
    try:
        os.unlink(path)
    except OSError:
        pass


def main(argv=None):
    parser = argparse.ArgumentParser(description='PERF-01-COMPARE 多轮性能证据比较器')
    parser.add_argument('--runs', nargs='+', required=True, action='extend', metavar='RUN_DIR',
                        help='轮次目录（相对仓库根），各含 frames.csv/summary.json/verification.json；'
                             '一个 --runs 后跟多个 RUN_DIR，重复 --runs 也会累加')
    parser.add_argument('--output', required=True, metavar='NEW_REPORT.json',
                        help='输出报告（相对仓库根）；必须是不存在的新文件')
    args = parser.parse_args(argv)
    try:
        report = build_report_and_groups(args.runs)
        output_path = resolve_arg(args.output)
        write_report(report, output_path)
    except InvalidInput as exc:
        print(f'perf_compare: {exc}', file=sys.stderr)
        return 2
    except OSError as exc:
        # 兜底：任何未预见的文件系统错误仍按合同 exit2，且不回显可能含用户目录的消息
        print(f'perf_compare: 文件系统错误 ({exc.strerror or "未知错误"})', file=sys.stderr)
        return 2
    print(f'perf_compare: {len(report["runs"])} runs -> {len(report["groups"])} group(s); {display(output_path)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
