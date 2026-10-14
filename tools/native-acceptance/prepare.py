#!/usr/bin/env python3
"""从已签候选 Yudian.app 克隆出独立私有验收包：新 Info.plist、新包装器，仅外层重签。

候选的引擎、PCK 与 Resources 字节保持不变；不启动 app、不启动 GUI、不运行 selftest driver。
用法与边界见同目录 README.md。
"""
import argparse
import hashlib
import json
import os
import plistlib
import shutil
import stat
import subprocess
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

PRODUCT = 'Yudian'
TOOL_DIR = Path(__file__).resolve().parent
LAUNCHER_SOURCE = TOOL_DIR / 'launcher.c'
PLIST = Path('Contents/Info.plist')
ENGINE = Path('Contents/MacOS') / PRODUCT
LEGACY_LAUNCHER = Path('Contents/MacOS') / 'YudianLauncher'
PCK = Path('Contents/Resources') / f'{PRODUCT}.pck'
RUNTIME = Path('Contents/Resources/data_Yudian_macos_arm64/Yudian.runtimeconfig.json')
WRAPPER_NAME = 'YudianAcceptance'
WRAPPER = Path('Contents/MacOS') / WRAPPER_NAME
APP_NAME = 'YudianAcceptance.app'
MANIFEST_NAME = 'acceptance-manifest.json'
CANDIDATE_MANIFEST = 'build-manifest.json'
LOG_NAME = 'native.log'
CAPTURE_NAME = 'captures'
SAVE_NAME = 'player-test-save.json'
SCHEMA = 'yudian-native-acceptance-clone-v1'
SIZES = ('1280x800', '1920x1080', '1920x1200')
# 与 Main.cs ApplyWindowAndQuality 中 YUDIAN_PLAYER_GUI_SIZE 白名单一致；test_prepare.py 静态核对二者同步。
SOURCE_GUI_SIZES = ('1280x800', '1920x1080', '1920x1200')
ENGINE_ARGS = ['--rendering-driver', 'metal', '--accessibility', 'always', '--max-fps', '60']


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, 'rb') as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def within(path, parent):
    return path == parent or path.startswith(parent.rstrip(os.sep) + os.sep)


def run(args):
    proc = subprocess.run([str(a) for a in args], capture_output=True, text=True)
    text = (proc.stdout + proc.stderr).strip()
    if proc.returncode != 0:
        raise RuntimeError(f'命令失败（{proc.returncode}）：{" ".join(str(a) for a in args)}\n{text}')
    return text


def repo_roots():
    """当前 worktree 根与主仓库根；两者及其下全部内容都不允许作私有输出或槽根。"""
    root = TOOL_DIR.parents[1]
    proc = subprocess.run(['git', 'rev-parse', '--git-common-dir'], cwd=str(root), capture_output=True, text=True)
    if proc.returncode != 0 or not proc.stdout.strip():
        raise RuntimeError('无法确认仓库根（git 不可用或不在仓库内），拒绝继续')
    common = Path(root, proc.stdout.strip()).resolve()
    return sorted({str(root.resolve()), str(common.parent)})


def godot_appdata(home):
    return os.path.join(home, 'Library', 'Application Support', 'Godot')


def formal_userdata(home):
    godot = godot_appdata(home)
    return [os.path.join(godot, 'app_userdata', PRODUCT), os.path.join(godot, 'app_userdata', PRODUCT, 'saves')]


def assert_private_location(real, label, home, repos):
    areas = [
        ('仓库或 worktree', repos),
        ('缓存', [os.path.join(home, 'Library', 'Caches'), os.path.join(home, '.cache')]),
        ('Godot 应用数据（含正式 user://saves）', [godot_appdata(home)]),
        ('正式用户数据', formal_userdata(home)),
    ]
    for name, places in areas:
        for place in places:
            if within(real, os.path.realpath(place)):
                raise ValueError(f'{label}位于禁止区域（{name}）：{real}')


def require_fresh_output(raw, *, home, repos):
    if not os.path.isabs(raw):
        raise ValueError('--output 必须是绝对路径')
    if os.path.lexists(raw):
        raise ValueError('输出目录已存在；换新目录，不覆盖旧 app、清单或日志')
    real = os.path.realpath(raw)
    assert_private_location(real, '输出目录', home, repos)
    return real


def validate_slot_root(raw, *, home, repos, output_real):
    if raw.startswith(('user://', 'res://')):
        raise ValueError('槽根不得使用 user:// 或 res://，必须是绝对私有路径')
    if not os.path.isabs(raw):
        raise ValueError('槽根必须是绝对路径')
    if any(ord(ch) < 32 for ch in raw):
        raise ValueError('槽根含控制字符')
    real = os.path.realpath(raw)  # 跟随符号链接：链接指向禁止区域同样拒绝
    if os.path.lexists(real) and not os.path.isdir(real):
        raise ValueError('槽根已存在但不是目录')
    assert_private_location(real, '槽根', home, repos)
    for formal in formal_userdata(home):
        if within(os.path.realpath(formal), real):
            raise ValueError('槽根不得包含正式存档路径')
    if within(real, output_real) or within(output_real, real):
        raise ValueError('槽根与验收输出目录不得互相包含')
    return real


def gui_size_env(size):
    if size not in SIZES:
        raise ValueError(f'--size 只接受 {"|".join(SIZES)}，收到 {size!r}')
    return size


def resolve_candidate(raw):
    real = Path(os.path.realpath(raw))  # 先读取符号链接目标，再按真实路径处理
    if real.suffix != '.app' or not real.is_dir():
        raise ValueError('候选必须是已存在的 .app 目录')
    for rel in (PLIST, ENGINE, PCK, RUNTIME):
        if not (real / rel).is_file():
            raise ValueError(f'候选缺少 {rel}')
    manifest = real.parent / CANDIDATE_MANIFEST
    if not manifest.is_file():
        raise ValueError('候选所在目录缺少 build-manifest.json，无法核对来源')
    return real, manifest


def verify_candidate(real, manifest_path):
    data = json.loads(manifest_path.read_text(encoding='utf-8'))
    checks = {
        'binary_sha256': sha256_file(real / ENGINE) == data.get('binary_sha256'),
        'pck_sha256': sha256_file(real / PCK) == data.get('pck_sha256'),
        'export_runtimeconfig': json.loads((real / RUNTIME).read_text(encoding='utf-8')) == data.get('export_runtimeconfig'),
    }
    if 'launcher_binary_sha256' in data:
        checks['launcher_binary_sha256'] = sha256_file(real / LEGACY_LAUNCHER) == data['launcher_binary_sha256']
        checks['launcher_plist_sha256'] = sha256_file(real / PLIST) == data.get('launcher_plist_sha256')
    bad = sorted(name for name, ok in checks.items() if not ok)
    if bad:
        raise ValueError('候选与其 build-manifest.json 不一致：' + ', '.join(bad))
    info = plistlib.loads((real / PLIST).read_bytes())
    if not (real / 'Contents/MacOS' / str(info.get('CFBundleExecutable', ''))).is_file():
        raise ValueError('候选 Info.plist 的 CFBundleExecutable 指向的文件不存在')
    return {'build_manifest_sha256': sha256_file(manifest_path), 'build_git_head': data.get('git_head'), 'checks': checks}


def verify_signature(app):
    return run(['/usr/bin/codesign', '--verify', '--deep', '--strict', '-v', app])


def verify_arm64(binary):
    run(['/usr/bin/lipo', binary, '-verify_arch', 'arm64'])


def scan_bundle(root):
    """逐项扫描：符号链接必须读取目标并确认仍在包内，特殊文件拒绝。返回 ({相对路径: sha256}, {链接: 目标})。"""
    root_text = os.path.realpath(str(root))
    files, links = {}, {}
    for dirpath, dirnames, filenames in os.walk(root_text, followlinks=False):
        for name in dirnames + filenames:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root_text)
            mode = os.lstat(full).st_mode
            if stat.S_ISLNK(mode):
                target = os.readlink(full)
                resolved = os.path.realpath(os.path.join(dirpath, target))
                if not within(resolved, root_text):
                    raise ValueError(f'候选内符号链接指向包外，拒绝：{rel} -> {target}')
                links[rel] = target
            elif stat.S_ISREG(mode):
                files[rel] = sha256_file(full)
            elif not stat.S_ISDIR(mode):
                raise ValueError(f'候选含特殊文件，拒绝：{rel}')
    return files, links


def c_string(value):
    if any(ord(ch) < 32 or ord(ch) == 127 for ch in value):
        raise ValueError(f'路径含控制字符，拒绝写入包装器：{value!r}')
    return '"' + value.replace('\\', '\\\\').replace('"', '\\"') + '"'


def compile_wrapper(dest, values):
    args = ['/usr/bin/xcrun', 'clang', '-arch', 'arm64', '-mmacosx-version-min=13.0', '-Wall', '-Wextra', '-Werror']
    args += [f'-D{name}={c_string(value)}' for name, value in values.items()]
    args += [LAUNCHER_SOURCE, '-o', dest]
    run(args)


def stamp_plist(app, bundle_id):
    path = app / PLIST
    info = plistlib.loads(path.read_bytes())
    display = info.get('CFBundleDisplayName') or info.get('CFBundleName') or PRODUCT
    info['CFBundleIdentifier'] = bundle_id
    info['CFBundleExecutable'] = WRAPPER_NAME
    info['CFBundleDisplayName'] = f'{display} 验收'
    path.write_bytes(plistlib.dumps(info))


def compare_with_candidate(app, cand_files, cand_links, removed):
    """除 Info.plist 与 _CodeSignature 外，克隆必须与候选逐字节一致；只允许新增包装器，且缺失项必须恰为已显式移除的文件。"""
    clone_files, clone_links = scan_bundle(app)

    def kept(files):
        return {rel: digest for rel, digest in files.items()
                if rel != str(PLIST) and not rel.startswith('Contents/_CodeSignature/')}

    cand, clone = kept(cand_files), kept(clone_files)
    extra = sorted(set(clone) - set(cand))
    missing = sorted(set(cand) - set(clone))
    changed = sorted(rel for rel in cand if rel in clone and clone[rel] != cand[rel])
    if extra != [str(WRAPPER)] or missing != sorted(removed) or changed or cand_links != clone_links:
        raise ValueError(f'克隆与候选不一致：extra={extra} missing={missing} removed={removed} changed={changed} links_equal={cand_links == clone_links}')
    return {'compared_files': len(cand), 'extra': extra, 'missing': missing, 'changed': changed, 'links_equal': True}


def write_manifest(path, data):
    with open(path, 'x', encoding='utf-8') as handle:  # 独占创建，绝不覆盖
        json.dump(data, handle, ensure_ascii=False, indent=2)
        handle.write('\n')


def prepare(candidate, output, slot_root, size):
    home = os.path.realpath(os.path.expanduser('~'))
    repos = repo_roots()
    gui_size = gui_size_env(size)
    output_real = require_fresh_output(output, home=home, repos=repos)
    slot_real = validate_slot_root(slot_root, home=home, repos=repos, output_real=output_real)
    real, manifest_path = resolve_candidate(candidate)
    source = verify_candidate(real, manifest_path)
    signature_before = verify_signature(real)
    verify_arm64(real / ENGINE)
    cand_files, cand_links = scan_bundle(real)  # 复制前拒绝包外符号链接
    run_id = uuid.uuid4().hex
    bundle_id = f'com.yudian.acceptance.{run_id}'
    out = Path(output_real)
    app = out / APP_NAME
    log_file = str(out / LOG_NAME)
    capture_dir = str(out / CAPTURE_NAME)
    test_save = os.path.join(slot_real, SAVE_NAME)
    os.makedirs(output_real)  # 前面已确认不存在
    try:
        shutil.copytree(real, app, symlinks=True)
        stamp_plist(app, bundle_id)
        removed = []
        legacy = app / LEGACY_LAUNCHER
        if legacy.is_file():  # 旧启动器曾是主可执行文件，其签名绑定旧 Info.plist；包装器已取代其入口，故从克隆移除
            legacy.unlink()
            removed.append(str(LEGACY_LAUNCHER))
        compile_wrapper(app / WRAPPER, {
            'ACC_SLOT_ROOT': slot_real,
            'ACC_TEST_SAVE': test_save,
            'ACC_GUI_SIZE': gui_size,
            'ACC_CAPTURE_DIR': capture_dir,
            'ACC_LOG_FILE': log_file,
        })
        run(['/usr/bin/codesign', '--force', '-s', '-', app])  # 只重签外层，不 --deep
        signature_after = verify_signature(app)
        verify_arm64(app / ENGINE)
        verify_arm64(app / WRAPPER)
        comparison = compare_with_candidate(app, cand_files, cand_links, removed)
        manifest = {
            'schema': SCHEMA,
            'created_utc': datetime.now(timezone.utc).isoformat(),
            'run_id': run_id,
            'bundle_identifier': bundle_id,
            'output': output_real,
            'app': str(app),
            'gui_size': gui_size,
            'gui_size_supported_by_candidate_source': gui_size in SOURCE_GUI_SIZES,
            'candidate': {'app': str(real), 'build_manifest': str(manifest_path), **source,
                          'codesign_verify_before': signature_before},
            'clone': {
                'engine_sha256': sha256_file(app / ENGINE),
                'pck_sha256': sha256_file(app / PCK),
                'info_plist_sha256': sha256_file(app / PLIST),
                'wrapper_sha256': sha256_file(app / WRAPPER),
                'wrapper_source_sha256': sha256_file(LAUNCHER_SOURCE),
                'removed_from_clone': removed,
                'codesign_verify_after': signature_after,
                'compare_with_candidate': comparison,
            },
            'private': {'slot_root': slot_real, 'test_save': test_save, 'capture_dir': capture_dir, 'native_log': log_file},
            'wrapper_env': {
                'inherited_yudian_vars': 'wrapper 子进程中全部清除；父进程与系统环境不改',
                'set': {
                    'YUDIAN_PLAYER_GUI_TEST': '1',
                    'YUDIAN_TUTORIAL_TEST_ROOT': slot_real,
                    'YUDIAN_PLAYER_TEST_SAVE': test_save,
                    'YUDIAN_PLAYER_GUI_SIZE': gui_size,
                    'YUDIAN_PLAYER_CAPTURE_DIR': capture_dir,
                },
            },
            'engine_args': ENGINE_ARGS + ['--log-file', log_file],
            'launch': 'NOT_RUN',
        }
        write_manifest(out / MANIFEST_NAME, manifest)
    except Exception as error:
        raise RuntimeError(f'失败；已创建的输出目录保留以便诊断（{output_real}）：{error}') from error
    return manifest


def build_parser():
    parser = argparse.ArgumentParser(description='克隆已签候选为独立私有验收包；不启动 app。')
    parser.add_argument('--candidate', required=True, help='已签候选 Yudian.app；其所在目录须有 build-manifest.json')
    parser.add_argument('--output', required=True, help='新的绝对私有目录，必须不存在')
    parser.add_argument('--slot-root', required=True, help='绝对私有槽根，同时作设置根')
    parser.add_argument('--size', required=True, choices=SIZES, help='逻辑窗口尺寸')
    return parser


def main(argv=None):
    args = build_parser().parse_args(argv)
    manifest = prepare(args.candidate, args.output, args.slot_root, args.size)
    if not manifest['gui_size_supported_by_candidate_source']:
        print(f'警告：候选源码 YUDIAN_PLAYER_GUI_SIZE 白名单不含 {args.size}，该尺寸启动可能在 _Ready 失败；未修改源码。', file=sys.stderr)
    print(json.dumps({'app': manifest['app'], 'manifest': os.path.join(manifest['output'], MANIFEST_NAME),
                      'bundle_identifier': manifest['bundle_identifier'], 'launch': 'NOT_RUN'},
                     ensure_ascii=False, indent=2))
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, RuntimeError, OSError) as error:
        print(f'拒绝或失败：{error}', file=sys.stderr)
        sys.exit(2)
