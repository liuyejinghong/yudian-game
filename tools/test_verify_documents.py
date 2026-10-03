from __future__ import annotations
import json
from pathlib import Path
import shutil
import tempfile
import unittest

from verify_documents import ROOT, verify


class VerifyDocumentsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "repo"
        shutil.copytree(ROOT, self.root, ignore=shutil.ignore_patterns(".git", "__pycache__", ".pytest_cache"))

    def test_complete_documentation(self) -> None:
        self.assertEqual(verify(self.root), [])

    def test_missing_required_document(self) -> None:
        (self.root / "docs/workflow/codex-handoff.md").unlink()
        self.assertTrue(any("Missing required" in e for e in verify(self.root)))

    def test_modified_historical_source(self) -> None:
        path = self.root / "archive/conversation/yudian-3d-product-definition-v0.1.md"
        path.write_bytes(path.read_bytes() + b"\nchanged\n")
        self.assertTrue(any("Source hash mismatch" in e for e in verify(self.root)))

    def test_broken_current_link(self) -> None:
        path = self.root / "README.md"
        path.write_text(path.read_text(encoding="utf-8") + "\n[broken](docs/not-here.md)\n", encoding="utf-8")
        self.assertTrue(any("Broken local link" in e for e in verify(self.root)))

    def test_missing_original_acceptance(self) -> None:
        path = self.root / "docs/product/product-definition.md"
        path.write_text(path.read_text(encoding="utf-8").replace("| A24 |", "| A25 |"), encoding="utf-8")
        self.assertTrue(any("Acceptance numbering" in e for e in verify(self.root)))

    def test_missing_rescue_acceptance(self) -> None:
        path = self.root / "docs/product/energy-return-and-rescue.md"
        path.write_text(path.read_text(encoding="utf-8").replace("| B08 |", "| B09 |"), encoding="utf-8")
        self.assertTrue(any("Acceptance numbering" in e for e in verify(self.root)))

    def test_empty_source_manifest(self) -> None:
        path = self.root / "archive/source-manifest.json"
        path.write_text(json.dumps({"sources": []}), encoding="utf-8")
        self.assertTrue(any("six unique" in e for e in verify(self.root)))


if __name__ == "__main__":
    unittest.main()
