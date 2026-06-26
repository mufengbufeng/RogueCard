from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from unity_meta import read_unity_asset_metadata


IMAGE_EXTS = {".png", ".jpg", ".jpeg"}


@dataclass(slots=True)
class AssetScanRecord:
    path: Path
    rel_path: str
    basename: str
    guid: str | None
    sprite_border: dict[str, int] | None
    sub_sprites: list[dict[str, Any]]


def scan_asset_records(assets_dir: Path) -> list[AssetScanRecord]:
    paths = sorted(
        (
            p
            for p in assets_dir.rglob("*")
            if p.is_file() and p.suffix.lower() in IMAGE_EXTS
        ),
        key=lambda p: p.relative_to(assets_dir).as_posix().lower(),
    )

    records: list[AssetScanRecord] = []
    for path in paths:
        rel_path = path.relative_to(assets_dir).as_posix()
        metadata = read_unity_asset_metadata(path)
        guid = metadata.get("guid")
        border = metadata.get("spriteBorder")
        sub_sprites = metadata.get("sub_sprites")
        records.append(
            AssetScanRecord(
                path=path,
                rel_path=rel_path,
                basename=path.name,
                guid=guid if isinstance(guid, str) else None,
                sprite_border=border if isinstance(border, dict) else None,
                sub_sprites=sub_sprites if isinstance(sub_sprites, list) else [],
            )
        )
    return records
