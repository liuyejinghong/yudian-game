#!/usr/bin/env python3
"""Finalize the documentation-only import. No network and no game execution.

Reviewed source hashes are fixed from the user attachments, not from whatever
happens to be in Git. Refuse publication if an original cannot be reconstructed.
"""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCES = {
    "yudian-3d-product-definition-v0.1.md": (38048, "be466348a896a4448a34206b505534f106a668b4e4e777f1c7a396ff4da75e24"),
    "yudian-3d-product-definition-v0.2.md": (43841, "87f4492ec1ec55bb36a105230cbaf03c886c684995d63c55d6e7e1494497527a"),
    "yudian-3d-product-definition-v0.3.md": (53662, "9b0522f970d677d63725671c981ac0e385356ea9a869c9e9fa61843ea7ce8f54"),
    "yudian-art-direction-brief-v0.1.md": (8169, "820c1186425cdac4dfeafb60c27063f166b6c04b6bb8ebe8f70f97fa526da40a"),
    "yudian-art-direction-v0.2.md": (7091, "dddd6a3d0bd16eebaf08b3ecd5ddec9f7fadd00371f07d3fbeb4effae47a664c"),
    "07-external-review-prompt.md": (19641, "977ea892f3ab93559d987cea5930dd1bdd3aeb88edeae39927ad6fc4b911a55a"),
}


def check(name: str, raw: bytes) -> None:
    size, expected = SOURCES[name]
    actual = hashlib.sha256(raw).hexdigest()
    if len(raw) != size or actual != expected:
        raise ValueError(f"Original mismatch: {name}, bytes={len(raw)}, SHA256={actual}")


def main() -> None:
    pending: dict[Path, bytes] = {}
    art_name = "yudian-art-direction-v0.2.md"
    art = (ROOT / "docs/art/art-direction.md").read_bytes()
    check(art_name, art)
    art_path = ROOT / "archive/conversation" / art_name
    if art_path.exists() and art_path.read_bytes() != art:
        raise ValueError("Refusing to overwrite existing art archive")
    pending[art_path] = art

    prompt_name = "07-external-review-prompt.md"
    prompt_path = ROOT / "archive/conversation" / prompt_name
    raw = prompt_path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != SOURCES[prompt_name][1]:
        # Recorded transcription corrections restore the uploaded original;
        # they do not fix the historical prompt's factual or design claims.
        text = raw.decode("utf-8")
        pairs = [
            ("欢迎逐个核验真伪", "欢迎逐个验证真伪"),
            ("（接单/交付/入账全链是否真的已建成）", "（订单接单/交付/入账全链是否真的已建成）"),
            ("与 `BaseIntroModal.tsx`", "与 `apps/web/src/features/base/BaseIntroModal.tsx`"),
            ("交互健康问题出现的机率", "交互健康问题出现的时机"),
            ("R5 内容 40 分钟见底＋探索零信息", "R5 内容 40 分钟见底＋勘探是零信息揭晓"),
            ("R8 已写好的叙事与身份资产被门禁下线", "R8 已写好的开场叙事被界面门禁下线"),
            ("C 重立循环）", "C 重立核心循环）"),
            ("小概率意外池（≤20%", "小概率意外池（富矿/伴生/风化层，≤20%"),
            ("、尘暴、远征站。", "、尘暴、远征补给站。"),
        ]
        for old, new in pairs:
            text = text.replace(old, new)
        raw = text.encode("utf-8")
    check(prompt_name, raw)
    pending[prompt_path] = raw

    records = []
    for name, (size, digest) in SOURCES.items():
        path = ROOT / "archive/conversation" / name
        content = pending[path] if path in pending else path.read_bytes()
        check(name, content)
        records.append({"source_filename": name, "archive_path": path.relative_to(ROOT).as_posix(),
                        "bytes": size, "sha256": digest,
                        "handling": "byte-for-byte historical attachment; not current instructions"})
        print(f"VERIFIED {name}: {size} bytes SHA256={digest}")
    check("yudian-3d-product-definition-v0.3.md", (ROOT / "docs/product/product-definition.md").read_bytes())
    for path, content in pending.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
    manifest = {"archive_policy": "原文不改写。当前要求见 README、决定登记、产品正文和 v0.4 返航救援补充。", "sources": records}
    (ROOT / "archive/source-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("All six source attachments and current v0.3 base are byte-exact. No game tests were run.")


if __name__ == "__main__":
    main()
