from __future__ import annotations

import json
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from asset_scan import scan_asset_records


@dataclass(slots=True)
class AssetIndex:
    index: dict[str, Path] = field(default_factory=dict)
    duplicate_basenames: set[str] = field(default_factory=set)
    borders: dict[str, dict[str, int]] = field(default_factory=dict)
    guid_index: dict[str, Path] = field(default_factory=dict)
    guid_by_key: dict[str, str] = field(default_factory=dict)
    sprite_names_by_path: dict[Path, set[str]] = field(default_factory=dict)
    sub_sprites_by_path: dict[Path, list[dict[str, Any]]] = field(default_factory=dict)
    file_count: int = 0
    border_file_count: int = 0
    sub_sprite_count: int = 0
    source: str = "scan"


def load_asset_index(assets_dir: Path, inventory_path: Path | None = None) -> AssetIndex:
    if inventory_path and inventory_path.exists():
        try:
            payload = json.loads(inventory_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            payload = None
        if isinstance(payload, dict):
            indexed = _build_from_inventory(assets_dir, payload)
            indexed.source = "inventory"
            return indexed
    return _build_from_scan(assets_dir)


def _build_from_inventory(assets_dir: Path, payload: dict[str, Any]) -> AssetIndex:
    items = payload.get("assets")
    if not isinstance(items, list):
        return AssetIndex()

    records: list[tuple[Path, str, dict[str, Any]]] = []
    by_basename: dict[str, list[Path]] = defaultdict(list)

    for item in items:
        if not isinstance(item, dict):
            continue
        rel = item.get("path")
        if not isinstance(rel, str) or not rel.strip():
            continue
        rel_key = rel.replace("\\", "/").strip()
        path = assets_dir / rel_key
        records.append((path, rel_key, item))
        by_basename[path.name.lower()].append(path)

    return _finalize_index(records, by_basename)


def _build_from_scan(assets_dir: Path) -> AssetIndex:
    records: list[tuple[Path, str, dict[str, Any]]] = []
    by_basename: dict[str, list[Path]] = defaultdict(list)

    for scanned in scan_asset_records(assets_dir):
        metadata: dict[str, Any] = {}
        if scanned.guid is not None:
            metadata["guid"] = scanned.guid
        if scanned.sprite_border is not None:
            metadata["spriteBorder"] = scanned.sprite_border
        if scanned.sub_sprites:
            metadata["sub_sprites"] = scanned.sub_sprites
        records.append((scanned.path, scanned.rel_path, metadata))
        by_basename[scanned.basename.lower()].append(scanned.path)

    return _finalize_index(records, by_basename)


def _finalize_index(records: list[tuple[Path, str, dict[str, Any]]],
                    by_basename: dict[str, list[Path]]) -> AssetIndex:
    index = AssetIndex()
    index.duplicate_basenames = {name for name, paths in by_basename.items() if len(paths) > 1}

    for path, rel_key, item in records:
        rel_key = rel_key.replace("\\", "/").lower()
        name_key = path.name.lower()
        border = item.get("spriteBorder") if isinstance(item, dict) else None
        guid = item.get("guid") if isinstance(item, dict) else None
        sub_sprites = item.get("sub_sprites") if isinstance(item, dict) else None
        sub_sprite_list = sub_sprites if isinstance(sub_sprites, list) else []

        index.index[rel_key] = path
        if border:
            index.borders[rel_key] = border
            index.border_file_count += 1
        if isinstance(guid, str) and guid.strip():
            guid_key = guid.strip().lower()
            index.guid_index[guid_key] = path
            index.guid_by_key[rel_key] = guid_key
        if sub_sprite_list:
            index.sub_sprites_by_path[path] = sub_sprite_list
            names: set[str] = index.sprite_names_by_path.setdefault(path, set())
            for sprite in sub_sprite_list:
                if not isinstance(sprite, dict):
                    continue
                sprite_name = sprite.get("spriteName")
                if isinstance(sprite_name, str) and sprite_name.strip():
                    names.add(sprite_name)
            index.sub_sprite_count += len(sub_sprite_list)
        index.file_count += 1

        if name_key not in index.duplicate_basenames:
            index.index[name_key] = path
            if border:
                index.borders[name_key] = border
            if isinstance(guid, str) and guid.strip():
                index.guid_by_key[name_key] = guid.strip().lower()

    return index
