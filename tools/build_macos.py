#!/usr/bin/env python3
"""从原始模板建立本地 arm64 副本、导出并签名；不修改已安装模板。"""
import argparse
import copy
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
TEMPLATE_SHA = '92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d'


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def run(args, env=None, cwd=ROOT):
    subprocess.run([str(x) for x in args], check=True, env=env, cwd=cwd)


def patch_template(archive, output):
    if sha(archive) != TEMPLATE_SHA:
        raise ValueError('原始 .tpz SHA256 不符，停止；不下载或修改它')
    with zipfile.ZipFile(archive) as tpz:
        if tpz.read('templates/version.txt').decode().strip() != '4.7.2.stable.mono':
            raise ValueError('模板版本不符')
        original = tpz.read('templates/macos.zip')
    with tempfile.TemporaryDirectory(dir=output.parent) as temp:
        source = Path(temp) / 'macos.zip'
        source.write_bytes(original)
        target = Path(temp) / 'patched.zip'
        with zipfile.ZipFile(source) as src, zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as dst:
            for item in src.infolist():
                dst.writestr(copy.copy(item), src.read(item))
            for mode in ('debug', 'release'):
                name = next(n for n in src.namelist() if n.endswith(f'godot_macos_{mode}.universal'))
                universal = Path(temp) / f'{mode}.universal'
                arm = Path(temp) / f'{mode}.arm64'
                universal.write_bytes(src.read(name))
                run(['/usr/bin/lipo', universal, '-thin', 'arm64', '-output', arm])
                info = zipfile.ZipInfo(name.replace('.universal', '.arm64'), (2026, 8, 18, 0, 0, 0))
                info.external_attr = 0o100755 << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                dst.writestr(info, arm.read_bytes())
        digest = sha(target)
        if output.exists():
            if sha(output) != digest:
                raise ValueError('已有模板副本与确定性重建结果不同；停止，保留两者')
        else:
            output.write_bytes(target.read_bytes())
    return {'original_tpz_sha256': TEMPLATE_SHA, 'original_macos_zip_sha256': hashlib.sha256(original).hexdigest(), 'patched_macos_zip_sha256': digest}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--godot', type=Path, required=True)
    p.add_argument('--dotnet', type=Path, required=True)
    p.add_argument('--templates', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True, help='新的输出目录；不覆盖旧导出')
    p.add_argument('--playable', action='store_true', help='双击进入当前整平试玩场景；原引擎入口保留')
    a = p.parse_args()
    a.output = a.output.resolve()
    a.output.mkdir(parents=True, exist_ok=True)
    app = a.output / 'Yudian.app'
    if app.exists():
        raise ValueError('输出应用已存在；换新目录，不覆盖旧证据')
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(a.dotnet.resolve().parent), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    env['PATH'] = str(a.dotnet.resolve().parent) + os.pathsep + env.get('PATH', '')
    sdk = subprocess.check_output([str(a.dotnet), '--version'], cwd=ROOT, env=env, text=True).strip()
    godot = subprocess.check_output([str(a.godot), '--version'], env=env, text=True).strip()
    if sdk != '10.0.401' or godot != '4.7.2.stable.mono.official.ed1daf0bf':
        raise ValueError(f'工具版本与已核定组合不符: SDK={sdk}, Godot={godot}')
    patch = PROJECT / '.godot/qa-export/macos.zip'
    patch.parent.mkdir(parents=True, exist_ok=True)
    hashes = patch_template(a.templates, patch)
    run([a.godot, '--headless', '--path', PROJECT, '--import'], env)
    source = {}
    assets = {}
    for f in sorted(PROJECT.rglob('*')):
        rel = f.relative_to(PROJECT)
        if f.is_file() and rel.parts[0] not in ('.godot', 'export', 'benchmarks') and f.name != 'build-info.json':
            if f.suffix in ('.cs', '.tscn', '.json', '.glb', '.gltf', '.bin', '.cfg', '.godot', '.csproj', '.import'):
                source[str(rel)] = sha(f)
                if rel.parts[0] == 'assets': assets[str(rel)] = sha(f)
    identity = {
        'git_head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'working_tree_dirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).strip()),
        'source_files_sha256': source,
        'source_snapshot_sha256': hashlib.sha256(json.dumps(source, sort_keys=True).encode()).hexdigest(),
        'assets_snapshot_sha256': hashlib.sha256(json.dumps(assets, sort_keys=True).encode()).hexdigest(),
        'sdk': sdk, 'godot': godot, 'tfm': 'net8.0', 'build_script_sha256': sha(Path(__file__)), **hashes,
    }
    (PROJECT / 'build-info.json').write_text(json.dumps(identity, indent=2) + '\n')
    preset = PROJECT / 'export_presets.cfg'
    original = preset.read_bytes()
    backup = PROJECT / '.godot/qa-export/export_presets.backup.cfg'
    backup.write_bytes(original)
    try:
        config = original.decode().replace('custom_template/debug=""', f'custom_template/debug="{patch}"').replace('custom_template/release=""', f'custom_template/release="{patch}"')
        preset.write_text(config)
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
            identity['double_click_mode'] = 'live-terrain'
            identity['launcher_source_sha256'] = sha(ROOT / 'tools/macos_launcher.c')
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
    finally:
        preset.write_bytes(original)
    print('BUILD_OK: arm64 export, ad-hoc signature, manifest; GUI/runtime isolation remain separate checks')


if __name__ == '__main__':
    main()
