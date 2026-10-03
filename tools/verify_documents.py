#!/usr/bin/env python3
"""Documentation-only checks. Passing is not game, model, GPU or playtest evidence."""
from __future__ import annotations
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = (
    "README.md", "AGENTS.md", "archive/README.md", "archive/source-manifest.json",
    "docs/product/product-definition.md", "docs/product/energy-return-and-rescue.md",
    "docs/product/world-and-fleet-systems.md", "docs/art/art-direction.md",
    "docs/decisions/decision-register.md", "docs/engineering/technical-validation-plan.md",
    "docs/engineering/upscaling-and-renderer-evaluation.md",
    "docs/workflow/parallel-validation.md", "docs/workflow/codex-handoff.md",
    "docs/reference/mud-relationship.md",
)


def numbered_rows(text: str, prefix: str) -> list[str]:
    return re.findall(r"^\|\s*(" + re.escape(prefix) + r"\d{2})\s*\|", text, re.M)


def verify(root: Path) -> list[str]:
    root = root.resolve()
    errors: list[str] = []
    for name in REQUIRED:
        if not (root / name).is_file():
            errors.append(f"Missing required file: {name}")
    manifest_path = root / "archive/source-manifest.json"
    if manifest_path.is_file():
        try:
            records = json.loads(manifest_path.read_text(encoding="utf-8"))["sources"]
            if len(records) != 6 or len({r["archive_path"] for r in records}) != 6:
                errors.append("Source manifest must contain six unique reviewed attachments")
            for record in records:
                path = (root / record["archive_path"]).resolve()
                if not path.is_relative_to(root / "archive/conversation"):
                    errors.append("Source path escapes archive")
                    continue
                if not path.is_file():
                    errors.append(f"Missing source: {record['archive_path']}")
                    continue
                raw = path.read_bytes()
                if len(raw) != record["bytes"] or hashlib.sha256(raw).hexdigest() != record["sha256"]:
                    errors.append(f"Source hash mismatch: {record['archive_path']}")
        except (ValueError, KeyError, TypeError) as exc:
            errors.append(f"Invalid source manifest: {exc}")
    for rel, prefix, count in (
        ("docs/product/product-definition.md", "A", 24),
        ("docs/product/energy-return-and-rescue.md", "B", 8),
    ):
        path = root / rel
        if path.is_file():
            actual = numbered_rows(path.read_text(encoding="utf-8"), prefix)
            expected = [f"{prefix}{n:02}" for n in range(1, count + 1)]
            if actual != expected:
                errors.append(f"Acceptance numbering mismatch: {rel}: {actual}")
    # Immutable historical bodies may retain their original relative links.
    # Validate current documents plus the archive index, not those old links.
    files = [root / "README.md", root / "AGENTS.md", root / "archive/README.md"]
    files += sorted((root / "docs").rglob("*.md")) if (root / "docs").is_dir() else []
    for path in files:
        if not path.is_file():
            continue
        text = re.sub(r"(?ms)^```.*?^```[^\n]*", "", path.read_text(encoding="utf-8"))
        for link in re.findall(r"\[[^\]]*\]\(([^)]+)\)", text):
            link = link.strip().strip("<>")
            parsed = urlsplit(link)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            target = (path.parent / unquote(parsed.path)).resolve()
            if not target.is_relative_to(root) or not target.exists():
                errors.append(f"Broken local link: {path.relative_to(root)} -> {link}")
    return errors


def main() -> None:
    errors = verify(ROOT)
    if errors:
        for error in errors:
            print("FAIL:", error)
        raise SystemExit(1)
    print("PASS: required documentation, current local links, six archived source hashes, A01-A24 and B01-B08.")
    print("Game/native/model/performance/playtest results: NOT_RUN.")


if __name__ == "__main__":
    main()
