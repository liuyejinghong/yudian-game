"""native-acceptance 局部反例测试：拒覆盖、正式/相对/仓库槽根、符号链接逃逸，以及 source 哈希与尺寸映射。

只使用临时目录，不运行 codesign、xcrun、Godot、git 或 app。
"""
import hashlib
import os
import re
import tempfile
import unittest
from pathlib import Path

import prepare


class PrepareNegativeTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(os.path.realpath(self._tmp.name))
        self.home = str(self.tmp / 'home')
        self.repo = str(self.tmp / 'repo')
        os.makedirs(self.home)
        os.makedirs(self.repo)
        self.output = str(self.tmp / 'out')

    def tearDown(self):
        self._tmp.cleanup()

    def slot(self, raw):
        return prepare.validate_slot_root(raw, home=self.home, repos=[self.repo], output_real=self.output)

    def test_existing_output_is_refused_and_left_untouched(self):
        existing = self.tmp / 'existing'
        existing.mkdir()
        (existing / prepare.MANIFEST_NAME).write_text('old evidence', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, '已存在'):
            prepare.require_fresh_output(str(existing), home=self.home, repos=[self.repo])
        self.assertEqual((existing / prepare.MANIFEST_NAME).read_text(encoding='utf-8'), 'old evidence')

    def test_relative_and_user_scheme_slot_roots_are_refused(self):
        for raw in ('saves', './slot', 'user://saves', 'res://slot'):
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                self.slot(raw)

    def test_formal_repo_cache_and_formal_ancestor_slot_roots_are_refused(self):
        godot = Path(self.home, 'Library', 'Application Support', 'Godot')
        cases = {
            'formal saves': godot / 'app_userdata' / 'Yudian' / 'saves',
            'formal userdata': godot / 'app_userdata' / 'Yudian',
            'repo worktree': Path(self.repo) / 'slot',
            'cache': Path(self.home, 'Library', 'Caches', 'slot'),
            'ancestor of formal saves': Path(self.home, 'Library', 'Application Support'),
        }
        for label, path in cases.items():
            with self.subTest(label=label), self.assertRaises(ValueError):
                self.slot(str(path))

    def test_new_private_slot_root_is_accepted(self):
        raw = str(self.tmp / 'private-slot')
        self.assertEqual(self.slot(raw), os.path.realpath(raw))

    def test_symlink_escape_is_refused_and_inside_link_is_recorded(self):
        outside = self.tmp / 'outside.bin'
        outside.write_bytes(b'private')
        escaping = self.tmp / 'escape' / 'Yudian.app'
        (escaping / 'Contents').mkdir(parents=True)
        os.symlink(outside, escaping / 'Contents' / 'Resources')
        with self.assertRaisesRegex(ValueError, '包外'):
            prepare.scan_bundle(escaping)

        inside = self.tmp / 'inside' / 'Yudian.app'
        (inside / 'Contents' / 'MacOS').mkdir(parents=True)
        (inside / 'Contents' / 'MacOS' / 'Yudian').write_bytes(b'engine')
        os.symlink('MacOS/Yudian', inside / 'Contents' / 'alias')
        files, links = prepare.scan_bundle(inside)
        self.assertEqual(links, {'Contents/alias': 'MacOS/Yudian'})
        self.assertEqual(files['Contents/MacOS/Yudian'], hashlib.sha256(b'engine').hexdigest())

    def test_source_hash_and_cli_size_mapping(self):
        sample = self.tmp / 'sample.bin'
        sample.write_bytes(b'yudian-acceptance')
        self.assertEqual(prepare.sha256_file(sample), hashlib.sha256(b'yudian-acceptance').hexdigest())
        for size in ('1280x800', '1920x1080', '1920x1200'):
            self.assertEqual(prepare.gui_size_env(size), size)
        with self.assertRaisesRegex(ValueError, '只接受'):
            prepare.gui_size_env('1024x768')

    def test_source_gui_whitelist_matches_tool_constant(self):
        main_cs = prepare.TOOL_DIR.parents[1] / 'prototype' / 'scripts' / 'Main.cs'
        self.assertTrue(main_cs.is_file(), f'缺少 Main.cs：{main_cs}')
        text = main_cs.read_text(encoding='utf-8')
        start = text.index('YUDIAN_PLAYER_GUI_SIZE") switch')
        block = text[start:text.index('};', start)]
        self.assertEqual(sorted(re.findall(r'"(\d+x\d+)" => new Vector2I', block)), sorted(prepare.SOURCE_GUI_SIZES))
        self.assertIn('GUI test size must be 1280x800, 1920x1080 or 1920x1200', block)


if __name__ == '__main__':
    unittest.main()
