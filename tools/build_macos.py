#!/usr/bin/env python3
"""从原始 .tpz 或已安装 macos.zip 建立本地 arm64 副本、导出并签名；不修改已安装模板。"""
import argparse
import copy
from contextlib import contextmanager
import hashlib
import json
import os
import plistlib
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'prototype'
TEMPLATE_VERSION = '4.7.2.stable.mono'
TEMPLATE_SHA = '92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d'
INSTALLED_MACOS_ZIP_SHA = 'e6120d492650ad677c3a50dfded836928671049552e09c41e0850beb425ace8c'
LEGACY_SOURCE_SUFFIXES = ('.cs', '.tscn', '.json', '.glb', '.gltf', '.bin', '.cfg', '.godot', '.csproj', '.import', '.gd', '.gdshader', '.tres', '.svg')
# 完整指纹：顶层缓存/构建目录不计入；生成信息与系统元数据任意层不计入
RAW_EXCLUDED_TOP_NAMES = ('.git', '.godot', 'bin', 'obj', 'export', 'benchmarks')
RAW_EXCLUDED_FILE_NAMES = ('build-info.json', '.DS_Store')


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def sha_bytes(data):
    return hashlib.sha256(data).hexdigest()


def run(args, env=None, cwd=ROOT):
    subprocess.run([str(x) for x in args], check=True, env=env, cwd=cwd)


def thin_macos_zip(original, output):
    """从 macos.zip 字节提取 debug/release 的 arm64 副本。源含重复 entry 则拒绝；源已有 arm64 entry 须与 universal 薄化结果逐字节一致并原样保留，不一致则停止；缺失才追加。"""
    with tempfile.TemporaryDirectory(dir=output.parent) as temp:
        source = Path(temp) / 'macos.zip'
        source.write_bytes(original)
        target = Path(temp) / 'patched.zip'
        with zipfile.ZipFile(source) as src:
            names = src.namelist()
            duplicates = sorted({n for n in names if names.count(n) > 1})
            if duplicates:
                raise ValueError(f'模板 zip 含重复 entry {duplicates}；停止，不挑选其一')
            with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as dst:
                for item in src.infolist():
                    dst.writestr(copy.copy(item), src.read(item))
                for mode in ('debug', 'release'):
                    name = next(n for n in names if n.endswith(f'godot_macos_{mode}.universal'))
                    arm_name = name.replace('.universal', '.arm64')
                    universal = Path(temp) / f'{mode}.universal'
                    arm = Path(temp) / f'{mode}.arm64'
                    universal.write_bytes(src.read(name))
                    run(['/usr/bin/lipo', universal, '-thin', 'arm64', '-output', arm])
                    thinned = arm.read_bytes()
                    if arm_name in names:
                        if src.read(arm_name) != thinned:
                            raise ValueError(f'已有 {arm_name} 与 {name} 薄化结果不同；停止，不写输出')
                        continue  # 原 entry 已由上方循环原样复制，不追加重名 entry
                    info = zipfile.ZipInfo(arm_name, (2026, 8, 18, 0, 0, 0))
                    info.external_attr = 0o100755 << 16
                    info.compress_type = zipfile.ZIP_DEFLATED
                    dst.writestr(info, thinned)
        digest = sha(target)
        if output.exists():
            if sha(output) != digest:
                raise ValueError('已有模板副本与确定性重建结果不同；停止，保留两者')
        else:
            output.write_bytes(target.read_bytes())
    return digest


def patch_template(archive, output):
    """原始 .tpz 路径：SHA256 与版本门禁保持不变。"""
    if sha(archive) != TEMPLATE_SHA:
        raise ValueError('原始 .tpz SHA256 不符，停止；不下载或修改它')
    with zipfile.ZipFile(archive) as tpz:
        if tpz.read('templates/version.txt').decode().strip() != TEMPLATE_VERSION:
            raise ValueError('模板版本不符')
        original = tpz.read('templates/macos.zip')
    digest = thin_macos_zip(original, output)
    return {'template_source': 'tpz', 'original_tpz_sha256': TEMPLATE_SHA, 'original_macos_zip_sha256': sha_bytes(original), 'patched_macos_zip_sha256': digest}


def install_from_macos_zip(installed, output):
    """已安装 macos.zip 路径：只读取字节并复制、thin 到本地缓存；不修改、不下载安装模板，也不冒称 TPZ 检查通过。"""
    original = installed.read_bytes()
    if sha_bytes(original) != INSTALLED_MACOS_ZIP_SHA:
        raise ValueError('已安装 macos.zip SHA256 不符，停止；不修改或下载模板')
    version = installed.with_name('version.txt')
    if not version.is_file() or version.read_text().strip() != TEMPLATE_VERSION:
        raise ValueError('已安装模板版本不符或缺少同目录 version.txt')
    digest = thin_macos_zip(original, output)
    return {'template_source': 'installed-macos-zip', 'original_tpz_sha256': None, 'installed_macos_zip_sha256': INSTALLED_MACOS_ZIP_SHA, 'original_macos_zip_sha256': sha_bytes(original), 'patched_macos_zip_sha256': digest}


def legacy_fingerprint(project):
    """旧 source/assets 口径原样保留，便于与既有清单逐项比较。"""
    source = {}
    assets = {}
    for f in sorted(project.rglob('*')):
        rel = f.relative_to(project)
        if f.is_file() and rel.parts[0] not in ('.godot', 'export', 'benchmarks') and f.name != 'build-info.json':
            if f.suffix in LEGACY_SOURCE_SUFFIXES:
                source[str(rel)] = sha(f)
                if rel.parts[0] == 'assets':
                    assets[str(rel)] = sha(f)
    return source, assets


def raw_fingerprint(project):
    """完整源/资产指纹：遍历文件系统而非 git ls-files，含 PNG/UID/sln，排除缓存与生成信息，拒绝符号链接。"""
    files = {}
    for dirpath, dirnames, filenames in os.walk(project, followlinks=False):
        here = Path(dirpath)
        if here == project:
            dirnames[:] = [d for d in dirnames if d not in RAW_EXCLUDED_TOP_NAMES]
            filenames = [f for f in filenames if f not in RAW_EXCLUDED_TOP_NAMES]
        for name in dirnames:
            if (here / name).is_symlink():
                raise ValueError(f'拒绝符号链接目录: {(here / name).relative_to(project)}')
        for name in filenames:
            path = here / name
            rel = str(path.relative_to(project))
            if path.is_symlink():
                raise ValueError(f'拒绝符号链接文件: {rel}')
            if name in RAW_EXCLUDED_FILE_NAMES:
                continue
            if not path.is_file():
                raise ValueError(f'拒绝非普通文件: {rel}')
            files[rel] = sha(path)
    assets = {k: v for k, v in files.items() if k.startswith('assets/')}
    return {
        'raw_files_sha256': files,
        'raw_snapshot_sha256': sha_bytes(json.dumps(files, sort_keys=True).encode()),
        'raw_assets_snapshot_sha256': sha_bytes(json.dumps(assets, sort_keys=True).encode()),
        'raw_file_count': len(files),
    }


def check_fresh_output(output):
    """新输出只接受全新目录；已有 app 或 manifest 都是旧证据，不覆盖。"""
    if (output / 'Yudian.app').exists():
        raise ValueError('输出应用已存在；换新目录，不覆盖旧证据')
    if (output / 'build-manifest.json').exists():
        raise ValueError('输出清单已存在；换新目录，不覆盖旧证据')


@contextmanager
def temporary_export_preset(preset, backup, patch, expected_sha):
    """临时把两个模板指向本地副本；成功或失败都按字节还原，并校验还原后的指纹。"""
    original = preset.read_bytes()
    if sha_bytes(original) != expected_sha:
        raise ValueError('export_presets.cfg 在指纹后被改动；停止，不修改导出预设')
    text = original.decode()
    replacements = {
        'custom_template/debug=""': f'custom_template/debug="{patch}"',
        'custom_template/release=""': f'custom_template/release="{patch}"',
    }
    for old in replacements:
        if text.count(old) != 1:
            raise ValueError(f'export_presets.cfg 必须恰有一处 {old} 占位；停止，不修改导出预设')
    patched = text
    for old, new in replacements.items():
        patched = patched.replace(old, new)
    backup.parent.mkdir(parents=True, exist_ok=True)
    backup.write_bytes(original)
    try:
        preset.write_bytes(patched.encode())
        yield
    finally:
        preset.write_bytes(original)
        if sha_bytes(preset.read_bytes()) != expected_sha:
            raise RuntimeError('export_presets.cfg 还原校验失败；请用备份核对，不继续')


def build_parser():
    p = argparse.ArgumentParser(description=__doc__)
    source = p.add_mutually_exclusive_group(required=True)
    source.add_argument('--templates', type=Path, help='原始 .tpz；SHA256 与版本门禁保持不变')
    source.add_argument('--installed-macos-zip', type=Path, help='已安装 macos.zip；同目录 version.txt 必须为 4.7.2.stable.mono；只读取并复制 thin 到本地缓存')
    p.add_argument('--godot', type=Path, required=True)
    p.add_argument('--dotnet', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True, help='新的输出目录；不覆盖旧 app 或 build-manifest.json')
    p.add_argument('--playable', action='store_true', help='双击进入默认玩家场景；原引擎命令行入口保留')
    return p


def main():
    a = build_parser().parse_args()
    a.output = a.output.resolve()
    check_fresh_output(a.output)
    a.output.mkdir(parents=True, exist_ok=True)
    app = a.output / 'Yudian.app'
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(a.dotnet.resolve().parent), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    env['PATH'] = str(a.dotnet.resolve().parent) + os.pathsep + env.get('PATH', '')
    sdk = subprocess.check_output([str(a.dotnet), '--version'], cwd=ROOT, env=env, text=True).strip()
    godot = subprocess.check_output([str(a.godot), '--version'], env=env, text=True).strip()
    if sdk != '10.0.401' or godot != '4.7.2.stable.mono.official.ed1daf0bf':
        raise ValueError(f'工具版本与已核定组合不符: SDK={sdk}, Godot={godot}')
    patch = PROJECT / '.godot/qa-export/macos.zip'
    patch.parent.mkdir(parents=True, exist_ok=True)
    if a.templates is not None:
        hashes = patch_template(a.templates, patch)
    else:
        hashes = install_from_macos_zip(a.installed_macos_zip, patch)
    run([a.godot, '--headless', '--path', PROJECT, '--import'], env)
    source, assets = legacy_fingerprint(PROJECT)
    raw = raw_fingerprint(PROJECT)
    preset = PROJECT / 'export_presets.cfg'
    preset_sha = sha(preset)
    identity = {
        'git_head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'working_tree_dirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).strip()),
        'source_files_sha256': source,
        'source_snapshot_sha256': sha_bytes(json.dumps(source, sort_keys=True).encode()),
        'assets_snapshot_sha256': sha_bytes(json.dumps(assets, sort_keys=True).encode()),
        **raw,
        'export_presets_sha256_before_patch': preset_sha,
        'launcher_source_sha256': sha(ROOT / 'tools/macos_launcher.c'),
        'sdk': sdk, 'godot': godot, 'tfm': 'net8.0', 'build_script_sha256': sha(Path(__file__)), **hashes,
    }
    (PROJECT / 'build-info.json').write_text(json.dumps(identity, indent=2) + '\n')
    backup = PROJECT / '.godot/qa-export/export_presets.backup.cfg'
    with temporary_export_preset(preset, backup, patch, preset_sha):
        run([a.godot, '--headless', '--path', PROJECT, '--build-solutions', '--quit'], env)
        run([a.godot, '--headless', '--path', PROJECT, '--export-release', 'macOS', app], env)
        if a.playable:
            run(['/usr/bin/xcrun', 'clang', '-arch', 'arm64', '-mmacosx-version-min=13.0',
                 '-Wall', '-Wextra', '-Werror', ROOT / 'tools/macos_launcher.c',
                 '-o', app / 'Contents/MacOS/YudianLauncher'])
            plist = app / 'Contents/Info.plist'
            info = plistlib.loads(plist.read_bytes())
            info.update(CFBundleExecutable='YudianLauncher', CFBundleDisplayName='余电')
            plist.write_bytes(plistlib.dumps(info))
            identity['double_click_mode'] = 'd1-player'
        run(['/usr/bin/codesign', '--force', '--deep', '-s', '-', app])
        run(['/usr/bin/codesign', '--verify', '--deep', '--strict', '-v', app])
        run(['/usr/bin/lipo', app / 'Contents/MacOS/Yudian', '-verify_arch', 'arm64'])
        if a.playable:
            identity['launcher_binary_sha256'] = sha(app / 'Contents/MacOS/YudianLauncher')
            identity['launcher_plist_sha256'] = sha(app / 'Contents/Info.plist')
        runtime = app / 'Contents/Resources/data_Yudian_macos_arm64/Yudian.runtimeconfig.json'
        identity['export_runtimeconfig'] = json.loads(runtime.read_text())
        identity['binary_sha256'] = sha(app / 'Contents/MacOS/Yudian')
        identity['pck_sha256'] = sha(app / 'Contents/Resources/Yudian.pck')
    (a.output / 'build-manifest.json').write_text(json.dumps(identity, indent=2) + '\n')
    print('BUILD_OK: arm64 export, ad-hoc signature, manifest; GUI/runtime isolation remain separate checks')


if __name__ == '__main__':
    main()
