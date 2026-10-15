"""build_macos 纯单元测试：只用临时目录与替身 lipo，不运行 Godot、导出、签名，也不触碰共享缓存。"""
import contextlib
import io
import os
import tempfile
import unittest
import warnings
import zipfile
from pathlib import Path
from unittest import mock

import build_macos as bm

VERSION = '4.7.2.stable.mono'
UNIVERSAL_ENTRIES = {'godot_macos_debug.universal': b'DEBUG', 'godot_macos_release.universal': b'RELEASE'}
PRESET_TEXT = (
    b'[preset.0]\n\n[preset.0.options]\n\n'
    b'custom_template/debug=""\n'
    b'custom_template/release=""\n'
    b'application/bundle_identifier="com.yudian.prototype"\n'
)
PROJECT_FILES = {
    'Yudian.sln': 'sln',
    'Yudian.csproj': '<Project/>',
    'project.godot': 'config',
    'export_presets.cfg': 'preset',
    'scripts/Main.cs': 'class Main {}',
    'scripts/Main.cs.uid': 'uid://main',
    'assets/icon.png': 'PNG',
    'assets/icon.png.import': 'import',
    '.godot/uid_cache.bin': 'cache',
    'bin/Debug/Yudian.dll': 'bin',
    'obj/project.assets.json': 'obj',
    'export/macos/Yudian.app/Contents/Info.plist': 'app',
    'benchmarks/run.json': '{}',
    '.git': 'gitdir: elsewhere',
    'build-info.json': '{}',
    '.DS_Store': 'finder',
}


def zip_bytes(entries):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, 'w', zipfile.ZIP_DEFLATED) as z:
        for name, data in entries.items():
            z.writestr(name, data)
    return buf.getvalue()


def fake_run(args, env=None, cwd=None):
    """替身命令：只接受 lipo -thin，把输入字节标记为 arm64 后写入 -output，不调用系统工具。"""
    if str(args[0]) != '/usr/bin/lipo':
        raise AssertionError(f'未预期的命令: {args}')
    Path(args[-1]).write_bytes(b'ARM64:' + Path(args[1]).read_bytes())


def temp_root():
    """临时目录：显式 TMPDIR 优先，其次 CLAUDE_JOB_DIR/tmp，不默认落到共享 /tmp；两者都没有时交给 tempfile 默认位置；不改 HOME。"""
    if os.environ.get('TMPDIR') or not os.environ.get('CLAUDE_JOB_DIR'):
        return None
    root = Path(os.environ['CLAUDE_JOB_DIR']) / 'tmp'
    root.mkdir(parents=True, exist_ok=True)
    return str(root)


class BuildMacosTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory(dir=temp_root())
        self.tmp = Path(self._tmp.name).resolve()

    def tearDown(self):
        self._tmp.cleanup()

    def write_tree(self, root, files):
        for rel, text in files.items():
            path = root / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)
        return root

    def installed_template(self, version=VERSION):
        folder = self.tmp / 'export_templates' / VERSION
        folder.mkdir(parents=True)
        zip_path = folder / 'macos.zip'
        zip_path.write_bytes(zip_bytes(UNIVERSAL_ENTRIES))
        (folder / 'version.txt').write_text(version)
        return zip_path

    def tpz(self, version=VERSION):
        path = self.tmp / 'Godot.tpz'
        path.write_bytes(zip_bytes({'templates/version.txt': version, 'templates/macos.zip': zip_bytes(UNIVERSAL_ENTRIES)}))
        return path

    def test_tpz_hash_mismatch_is_rejected_before_writing_output(self):
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with self.assertRaisesRegex(ValueError, '原始 .tpz SHA256 不符'):
            bm.patch_template(self.tpz(), out)
        self.assertFalse(out.exists())

    def test_tpz_version_mismatch_is_rejected(self):
        tpz = self.tpz(version='4.7.1.stable.mono')
        with mock.patch.object(bm, 'TEMPLATE_SHA', bm.sha(tpz)):
            with self.assertRaisesRegex(ValueError, '模板版本不符'):
                bm.patch_template(tpz, self.tmp / 'macos.zip')

    def test_tpz_route_records_checked_hash_and_thins_arm64(self):
        tpz = self.tpz()
        out = self.tmp / 'macos.zip'
        with mock.patch.object(bm, 'TEMPLATE_SHA', bm.sha(tpz)), mock.patch.object(bm, 'run', side_effect=fake_run):
            info = bm.patch_template(tpz, out)
        self.assertEqual(info['template_source'], 'tpz')
        self.assertEqual(info['original_tpz_sha256'], bm.sha(tpz))
        self.assertEqual(info['patched_macos_zip_sha256'], bm.sha(out))
        with zipfile.ZipFile(out) as z:
            names = z.namelist()
        self.assertIn('godot_macos_debug.arm64', names)
        self.assertIn('godot_macos_release.arm64', names)

    def test_installed_hash_mismatch_is_rejected_before_writing_output(self):
        zip_path = self.installed_template()
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with self.assertRaisesRegex(ValueError, '已安装 macos.zip SHA256 不符'):
            bm.install_from_macos_zip(zip_path, out)
        self.assertFalse(out.exists())

    def test_installed_version_mismatch_is_rejected(self):
        zip_path = self.installed_template(version='4.7.1.stable.mono')
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with mock.patch.object(bm, 'INSTALLED_MACOS_ZIP_SHA', bm.sha(zip_path)):
            with self.assertRaisesRegex(ValueError, '已安装模板版本不符'):
                bm.install_from_macos_zip(zip_path, out)
        self.assertFalse(out.exists())

    def test_installed_route_copies_and_thins_without_touching_installed_files(self):
        zip_path = self.installed_template()
        folder = zip_path.parent
        before = {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in folder.iterdir()}
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with mock.patch.object(bm, 'INSTALLED_MACOS_ZIP_SHA', bm.sha(zip_path)), mock.patch.object(bm, 'run', side_effect=fake_run):
            info = bm.install_from_macos_zip(zip_path, out)
            again = bm.install_from_macos_zip(zip_path, out)  # 同字节副本可复用
        after = {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in folder.iterdir()}
        self.assertEqual(before, after)
        self.assertEqual(sorted(after), ['macos.zip', 'version.txt'])
        self.assertEqual(info['template_source'], 'installed-macos-zip')
        self.assertIsNone(info['original_tpz_sha256'])
        self.assertEqual(info['installed_macos_zip_sha256'], bm.sha(zip_path))
        self.assertEqual(info['patched_macos_zip_sha256'], bm.sha(out))
        self.assertEqual(info, again)
        with zipfile.ZipFile(out) as z:
            names = z.namelist()
        self.assertIn('godot_macos_debug.arm64', names)
        self.assertIn('godot_macos_release.arm64', names)

    def test_existing_cache_copy_that_differs_is_not_overwritten(self):
        zip_path = self.installed_template()
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        out.write_bytes(b'old-cache')
        with mock.patch.object(bm, 'INSTALLED_MACOS_ZIP_SHA', bm.sha(zip_path)), mock.patch.object(bm, 'run', side_effect=fake_run):
            with self.assertRaisesRegex(ValueError, '已有模板副本'):
                bm.install_from_macos_zip(zip_path, out)
        self.assertEqual(out.read_bytes(), b'old-cache')

    def test_existing_matching_arm64_entries_are_kept_once_without_duplicate_warning(self):
        entries = {**UNIVERSAL_ENTRIES, 'godot_macos_debug.arm64': b'ARM64:DEBUG', 'godot_macos_release.arm64': b'ARM64:RELEASE'}
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with warnings.catch_warnings(record=True) as caught, mock.patch.object(bm, 'run', side_effect=fake_run):
            warnings.simplefilter('always')
            bm.thin_macos_zip(zip_bytes(entries), out)
        self.assertEqual([str(w.message) for w in caught if 'Duplicate name' in str(w.message)], [])
        with zipfile.ZipFile(out) as z:
            names = z.namelist()
            self.assertEqual(len(names), len(set(names)))
            self.assertEqual(names.count('godot_macos_debug.arm64'), 1)
            self.assertEqual(names.count('godot_macos_release.arm64'), 1)
            self.assertEqual(z.read('godot_macos_debug.arm64'), b'ARM64:DEBUG')

    def test_existing_arm64_entry_differing_from_thinned_universal_is_refused(self):
        entries = {**UNIVERSAL_ENTRIES, 'godot_macos_debug.arm64': b'OLD', 'godot_macos_release.arm64': b'ARM64:RELEASE'}
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with mock.patch.object(bm, 'run', side_effect=fake_run):
            with self.assertRaisesRegex(ValueError, '薄化结果不同'):
                bm.thin_macos_zip(zip_bytes(entries), out)
        self.assertFalse(out.exists())

    def test_source_with_duplicated_entry_is_refused_without_picking_one(self):
        buf = io.BytesIO()
        with warnings.catch_warnings():
            warnings.simplefilter('ignore', UserWarning)
            with zipfile.ZipFile(buf, 'w', zipfile.ZIP_DEFLATED) as z:
                for name, data in UNIVERSAL_ENTRIES.items():
                    z.writestr(name, data)
                z.writestr('godot_macos_debug.arm64', b'ARM64:DEBUG')
                z.writestr('godot_macos_debug.arm64', b'ARM64:OTHER')
        out = self.tmp / 'cache' / 'macos.zip'
        out.parent.mkdir()
        with mock.patch.object(bm, 'run', side_effect=fake_run):
            with self.assertRaisesRegex(ValueError, '重复 entry'):
                bm.thin_macos_zip(buf.getvalue(), out)
        self.assertFalse(out.exists())

    def test_template_source_is_exactly_one_of_tpz_or_installed_zip(self):
        base = ['--godot', 'godot', '--dotnet', 'dotnet', '--output', 'out']
        parser = bm.build_parser()
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                parser.parse_args(base + ['--templates', 'a.tpz', '--installed-macos-zip', 'macos.zip'])
            with self.assertRaises(SystemExit):
                parser.parse_args(base)
        self.assertEqual(parser.parse_args(base + ['--templates', 'a.tpz']).templates, Path('a.tpz'))
        self.assertEqual(parser.parse_args(base + ['--installed-macos-zip', 'macos.zip']).installed_macos_zip, Path('macos.zip'))

    def test_raw_fingerprint_keeps_png_uid_sln_and_skips_caches(self):
        root = self.write_tree(self.tmp / 'prototype', PROJECT_FILES)
        raw = bm.raw_fingerprint(root)
        keys = set(raw['raw_files_sha256'])
        for kept in ('Yudian.sln', 'Yudian.csproj', 'project.godot', 'export_presets.cfg', 'scripts/Main.cs', 'scripts/Main.cs.uid', 'assets/icon.png', 'assets/icon.png.import'):
            self.assertIn(kept, keys)
        for skipped in ('.godot/uid_cache.bin', 'bin/Debug/Yudian.dll', 'obj/project.assets.json', 'export/macos/Yudian.app/Contents/Info.plist', 'benchmarks/run.json', '.git', 'build-info.json', '.DS_Store'):
            self.assertNotIn(skipped, keys)
        self.assertEqual(raw['raw_file_count'], len(keys))
        self.assertIn('raw_assets_snapshot_sha256', raw)

    def test_raw_fingerprint_rejects_symlinks(self):
        root = self.write_tree(self.tmp / 'prototype', PROJECT_FILES)
        os.symlink(root / 'scripts' / 'Main.cs', root / 'scripts' / 'Link.cs')
        with self.assertRaisesRegex(ValueError, '拒绝符号链接文件'):
            bm.raw_fingerprint(root)

    def test_raw_fingerprint_sees_new_untracked_source(self):
        root = self.write_tree(self.tmp / 'prototype', PROJECT_FILES)
        before = bm.raw_fingerprint(root)['raw_snapshot_sha256']
        (root / 'scripts' / 'New.cs').write_text('class New {}')
        after = bm.raw_fingerprint(root)['raw_snapshot_sha256']
        self.assertNotEqual(before, after)

    def test_legacy_fingerprint_keeps_old_suffix_scope(self):
        root = self.write_tree(self.tmp / 'prototype', PROJECT_FILES)
        source, assets = bm.legacy_fingerprint(root)
        self.assertIn('scripts/Main.cs', source)
        self.assertIn('Yudian.csproj', source)
        self.assertNotIn('assets/icon.png', assets)
        self.assertIn('assets/icon.png.import', assets)
        self.assertNotIn('Yudian.sln', source)
        self.assertNotIn('scripts/Main.cs.uid', source)
        self.assertNotIn('.godot/uid_cache.bin', source)
        self.assertNotIn('benchmarks/run.json', source)
        self.assertNotIn('build-info.json', source)

    def make_preset(self, text=PRESET_TEXT):
        preset = self.tmp / 'export_presets.cfg'
        preset.write_bytes(text)
        backup = self.tmp / 'qa' / 'export_presets.backup.cfg'
        patch = self.tmp / 'qa' / 'macos.zip'
        return preset, backup, patch

    def test_preset_is_patched_inside_and_restored_byte_for_byte_after_failure(self):
        preset, backup, patch = self.make_preset()
        original = preset.read_bytes()
        with self.assertRaisesRegex(RuntimeError, 'export failed'):
            with bm.temporary_export_preset(preset, backup, patch, bm.sha_bytes(original)):
                self.assertIn(f'custom_template/debug="{patch}"'.encode(), preset.read_bytes())
                self.assertIn(f'custom_template/release="{patch}"'.encode(), preset.read_bytes())
                raise RuntimeError('export failed')
        self.assertEqual(preset.read_bytes(), original)
        self.assertEqual(backup.read_bytes(), original)

    def test_preset_is_restored_after_success(self):
        preset, backup, patch = self.make_preset()
        original = preset.read_bytes()
        with bm.temporary_export_preset(preset, backup, patch, bm.sha_bytes(original)):
            pass
        self.assertEqual(preset.read_bytes(), original)

    def test_preset_without_unique_placeholder_is_refused_unchanged(self):
        text = PRESET_TEXT.replace(b'custom_template/release=""\n', b'')
        preset, backup, patch = self.make_preset(text)
        original = preset.read_bytes()
        with self.assertRaisesRegex(ValueError, 'custom_template/release'):
            with bm.temporary_export_preset(preset, backup, patch, bm.sha_bytes(original)):
                self.fail('不应进入导出体')
        self.assertEqual(preset.read_bytes(), original)
        self.assertFalse(backup.exists())

    def test_preset_left_patched_by_a_previous_crash_is_refused(self):
        leftover = PRESET_TEXT.replace(b'custom_template/debug=""', b'custom_template/debug="/old/macos.zip"')
        preset, backup, patch = self.make_preset(leftover)
        original = preset.read_bytes()
        with self.assertRaisesRegex(ValueError, 'custom_template/debug'):
            with bm.temporary_export_preset(preset, backup, patch, bm.sha_bytes(original)):
                self.fail('不应进入导出体')
        self.assertEqual(preset.read_bytes(), original)

    def test_preset_changed_after_fingerprint_is_refused(self):
        preset, backup, patch = self.make_preset()
        stale = bm.sha_bytes(preset.read_bytes())
        preset.write_bytes(PRESET_TEXT + b'# edited\n')
        current = preset.read_bytes()
        with self.assertRaisesRegex(ValueError, '指纹后被改动'):
            with bm.temporary_export_preset(preset, backup, patch, stale):
                self.fail('不应进入导出体')
        self.assertEqual(preset.read_bytes(), current)

    def test_fresh_output_refuses_existing_app_or_manifest(self):
        empty = self.tmp / 'empty'
        empty.mkdir()
        bm.check_fresh_output(empty)
        with_app = self.tmp / 'with_app'
        (with_app / 'Yudian.app').mkdir(parents=True)
        with self.assertRaisesRegex(ValueError, '输出应用已存在'):
            bm.check_fresh_output(with_app)
        with_manifest = self.tmp / 'with_manifest'
        with_manifest.mkdir()
        (with_manifest / 'build-manifest.json').write_text('{}')
        with self.assertRaisesRegex(ValueError, '输出清单已存在'):
            bm.check_fresh_output(with_manifest)


if __name__ == '__main__':
    unittest.main()
