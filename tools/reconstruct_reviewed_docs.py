#!/usr/bin/env python3
"""One-time, offline reconstruction of full reviewed Markdown documents.

The input is data, never executable code. All outputs must match the original
attachment hashes before any Markdown is written. No game/build dependencies.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATCH = ROOT / "tools/reviewed-document-patches.json"
EXPECTED = {
    "archive/conversation/yudian-3d-product-definition-v0.1.md":
        (38048, "be466348a896a4448a34206b505534f106a668b4e4e777f1c7a396ff4da75e24"),
    "archive/conversation/yudian-3d-product-definition-v0.2.md":
        (43841, "87f4492ec1ec55bb36a105230cbaf03c886c684995d63c55d6e7e1494497527a"),
    "archive/conversation/yudian-3d-product-definition-v0.3.md":
        (53662, "9b0522f970d677d63725671c981ac0e385356ea9a869c9e9fa61843ea7ce8f54"),
    "docs/product/product-definition.md":
        (53662, "9b0522f970d677d63725671c981ac0e385356ea9a869c9e9fa61843ea7ce8f54"),
}


def checked(name: str, raw: bytes) -> bytes:
    if name not in EXPECTED:
        raise ValueError(f"Unapproved output path: {name}")
    size, digest = EXPECTED[name]
    actual = hashlib.sha256(raw).hexdigest()
    if len(raw) != size or actual != digest:
        raise ValueError(f"Original attachment mismatch: {name}: {len(raw)} bytes, {actual}")
    return raw


def main() -> None:
    data = json.loads(PATCH.read_text(encoding="utf-8"))
    if data.get("format") != "line-edits-v1" or len(data.get("steps", [])) != 2:
        raise ValueError("Unexpected patch format")
    # Correct a recorded import transcription error in patch DATA only. The
    # reviewed document's immutable hash below remains the acceptance authority.
    corrections = 0
    for step in data["steps"]:
        for edit in step["edits"]:
            if "不预先承诺诺任意" in edit["text"]:
                edit["text"] = edit["text"].replace("不预先承诺诺任意", "不预先承诺任意")
                corrections += 1
    outputs: dict[str, bytes] = {}
    for step in data["steps"]:
        base, target = step["base"], step["target"]
        if base not in EXPECTED or target not in EXPECTED:
            raise ValueError("Unapproved patch path")
        raw = outputs.get(base)
        if raw is None:
            raw = (ROOT / base).read_bytes()
        checked(base, raw)
        if hashlib.sha256(raw).hexdigest() != step["base_sha256"]:
            raise ValueError("Patch base mismatch")
        lines = raw.decode("utf-8").splitlines(keepends=True)
        position, parts = 0, []
        for edit in step["edits"]:
            start, end = edit["start"], edit["end"]
            if not isinstance(start, int) or not isinstance(end, int):
                raise ValueError("Non-integer line offset")
            if not 0 <= position <= start <= end <= len(lines):
                raise ValueError("Overlapping or out-of-range edit")
            parts.extend(lines[position:start])
            parts.append(edit["text"])
            position = end
        parts.extend(lines[position:])
        candidate = checked(target, "".join(parts).encode("utf-8"))
        if len(candidate) != step["target_bytes"] or hashlib.sha256(candidate).hexdigest() != step["target_sha256"]:
            raise ValueError("Patch target metadata mismatch")
        outputs[target] = candidate
    copy = data["copy"]
    if copy["from"] not in outputs or copy["to"] != "docs/product/product-definition.md":
        raise ValueError("Unapproved copy operation")
    outputs[copy["to"]] = checked(copy["to"], outputs[copy["from"]])
    # Refuse to overwrite later product edits; this script is an import tool,
    # not a source generator that runs on every future product change.
    for name, raw in outputs.items():
        target = ROOT / name
        if target.exists() and target.read_bytes() != raw:
            raise ValueError(f"Refusing to overwrite existing changes: {name}")
    for name, raw in outputs.items():
        target = ROOT / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        print(f"VERIFIED {name}: {len(raw)} bytes SHA256={hashlib.sha256(raw).hexdigest()}")
    if corrections:
        PATCH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Corrected {corrections} documented patch transcription error(s); original document hashes unchanged.")


if __name__ == "__main__":
    main()
