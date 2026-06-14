from __future__ import annotations

import re
from pathlib import Path
from typing import Any


GUID_RE = re.compile(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$")
BORDER_RE = re.compile(r"spriteBorder:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^,]+),\s*w:\s*([^}]+)\}")
SPRITES_BODY_RE = re.compile(
    r"(?ms)^\s*sprites:[ \t]*\r?\n(?P<body>.*?)(?=^\s*(?:outline|physicsShape|bones|spriteID|internalIDToNameTable|nameFileIdTable):|\Z)"
)


def read_meta_text(path: Path) -> str | None:
    meta_path = Path(str(path) + ".meta")
    if not meta_path.exists():
        return None
    try:
        return meta_path.read_text(encoding="utf-8")
    except OSError:
        return None


def read_unity_asset_metadata(path: Path) -> dict[str, Any]:
    text = read_meta_text(path)
    if not text:
        return {}

    metadata: dict[str, Any] = {}

    match = GUID_RE.search(text)
    if match:
        metadata["guid"] = match.group(1).lower()

    border = _read_border_from_text(text)
    if border is not None:
        metadata["spriteBorder"] = border

    sub_sprites = _read_sub_sprites_from_text(text)
    if sub_sprites:
        metadata["sub_sprites"] = sub_sprites

    return metadata


def read_unity_asset_guid(path: Path) -> str | None:
    guid = read_unity_asset_metadata(path).get("guid")
    return guid if isinstance(guid, str) else None


def read_unity_sprite_border(path: Path) -> dict[str, int] | None:
    border = read_unity_asset_metadata(path).get("spriteBorder")
    return border if isinstance(border, dict) else None


def read_unity_sub_sprites(path: Path) -> list[dict[str, Any]]:
    metadata = read_unity_asset_metadata(path)
    sub_sprites = metadata.get("sub_sprites")
    if isinstance(sub_sprites, list):
        return sub_sprites
    return []


def _read_border_from_text(text: str) -> dict[str, int] | None:
    match = BORDER_RE.search(text)
    if not match:
        return None
    left, bottom, right, top = (int(float(v.strip())) for v in match.groups())
    if left == top == right == bottom == 0:
        return None
    return {"left": left, "top": top, "right": right, "bottom": bottom}


def _read_sub_sprites_from_text(text: str) -> list[dict[str, Any]]:
    body_match = SPRITES_BODY_RE.search(text)
    if body_match:
        body = body_match.group("body") or ""
    elif re.search(r"(?m)^\s*sprites:\s*\[\s*\]\s*$", text):
        body = ""
    else:
        return []

    name_to_file_id = _parse_name_file_id_table(text)
    name_to_file_id.update(_parse_internal_id_table(text))

    blocks = _split_sprite_blocks(body)
    entries: list[dict[str, Any]] = []
    for block in blocks:
        entry = _parse_sprite_block(block)
        if entry is None:
            continue
        name = entry.get("spriteName")
        if name and "fileId" not in entry:
            file_id = name_to_file_id.get(str(name))
            if file_id is not None:
                entry["fileId"] = file_id
        entries.append(entry)

    if not entries and name_to_file_id:
        for name, file_id in name_to_file_id.items():
            entries.append({
                "spriteName": name,
                "fileId": file_id,
            })

    return entries


def _split_sprite_blocks(body: str) -> list[str]:
    lines = body.splitlines()
    blocks: list[str] = []
    current: list[str] = []
    item_indent: int | None = None

    for line in lines:
        stripped = line.strip()
        if not stripped:
            if current:
                current.append(line)
            continue

        indent = len(line) - len(line.lstrip(" "))
        is_item_start = stripped.startswith("- ")
        if is_item_start and (item_indent is None or indent == item_indent):
            if current:
                blocks.append("\n".join(current))
            current = [line]
            item_indent = indent
            continue

        if current:
            current.append(line)

    if current:
        blocks.append("\n".join(current))
    return blocks


def _parse_sprite_block(block: str) -> dict[str, Any] | None:
    name = _first_match(block, r"(?m)^\s*(?:-\s*)?name:\s*(.+?)\s*$")
    if not name:
        return None

    entry: dict[str, Any] = {"spriteName": name}

    file_id = _first_match(block, r"(?m)^\s*(?:-\s*)?internalID:\s*(\d+)\s*$")
    if file_id is not None:
        try:
            entry["fileId"] = int(file_id)
        except ValueError:
            pass

    rect = _parse_rect(block)
    if rect is not None:
        entry["rect"] = rect

    border = _parse_border(block)
    if border is not None and any(border.values()):
        entry["spriteBorder"] = border

    return entry


def _parse_rect(text: str) -> dict[str, int] | None:
    inline = re.search(
        r"(?ms)^\s*rect:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*width:\s*([^,]+),\s*height:\s*([^}]+)\}",
        text,
    )
    if inline:
        return _rect_from_groups(inline.groups())

    block = re.search(
        r"(?ms)^\s*rect:\s*(?:\n\s*serializedVersion:\s*\d+\s*)?\n\s*x:\s*([-.0-9]+)\s*\n\s*y:\s*([-.0-9]+)\s*\n\s*width:\s*([-.0-9]+)\s*\n\s*height:\s*([-.0-9]+)",
        text,
    )
    if block:
        return _rect_from_groups(block.groups())
    return None


def _rect_from_groups(groups: tuple[str, str, str, str]) -> dict[str, int] | None:
    try:
        x, y, width, height = (int(float(v.strip())) for v in groups)
    except ValueError:
        return None
    return {"x": x, "y": y, "width": width, "height": height}


def _parse_border(text: str) -> dict[str, int] | None:
    match = BORDER_RE.search(text)
    if not match:
        return None
    try:
        left, bottom, right, top = (int(float(v.strip())) for v in match.groups())
    except ValueError:
        return None
    return {"left": left, "top": top, "right": right, "bottom": bottom}


def _parse_name_file_id_table(text: str) -> dict[str, int]:
    body = _extract_table_block(text, "nameFileIdTable")
    if not body:
        return {}
    table: dict[str, int] = {}
    inline = body.strip()
    if inline.startswith("{") and inline.endswith("}") and inline != "{}":
        inner = inline[1:-1].strip()
        for part in inner.split(","):
            if ":" not in part:
                continue
            name, value = part.split(":", 1)
            name = name.strip()
            try:
                table[name] = int(value.strip())
            except ValueError:
                continue
        return table
    for match in re.finditer(r"(?m)^\s*([^:\n]+):\s*(\d+)\s*$", body):
        name = match.group(1).strip()
        try:
            table[name] = int(match.group(2))
        except ValueError:
            continue
    return table


def _parse_internal_id_table(text: str) -> dict[str, int]:
    body = _extract_table_block(text, "internalIDToNameTable")
    if not body:
        return {}
    table: dict[str, int] = {}
    for block in re.split(r"(?m)^\s*-\s*first:\s*", body):
        if not block.strip():
            continue
        first = re.match(r"(\d+)\s*\n\s*second:\s*(.+?)\s*$", block, re.S)
        if not first:
            continue
        try:
            file_id = int(first.group(1))
        except ValueError:
            continue
        name = first.group(2).strip()
        if name:
            table[name] = file_id
    return table


def _extract_table_block(text: str, key: str) -> str:
    lines = text.splitlines()
    body: list[str] = []
    capturing = False
    base_indent = 0

    for line in lines:
        stripped = line.strip()
        if not capturing:
            match = re.match(rf"^(\s*){re.escape(key)}:\s*(.*)$", line)
            if not match:
                continue
            capturing = True
            base_indent = len(match.group(1))
            tail = match.group(2).strip()
            if tail and tail not in ("{}", "[]"):
                body.append(tail)
            continue

        if not stripped:
            body.append(line)
            continue

        indent = len(line) - len(line.lstrip(" "))
        if indent <= base_indent:
            break
        body.append(line)

    return "\n".join(body)


def _first_match(text: str, pattern: str) -> str | None:
    match = re.search(pattern, text)
    if not match:
        return None
    return match.group(1).strip()
