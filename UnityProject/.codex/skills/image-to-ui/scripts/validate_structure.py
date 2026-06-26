"""
Validate ui_structure.json before bbox annotation and rendering.

Checks:
  - JSON shape, required fields, positive sizes, and duplicate element paths
  - canvas size against the design image
  - asset references against the sliced asset directory
  - layout, alignment, color, opacity, nine-slice, and text metric sanity

Usage:
    py -B validate_structure.py --structure ui_structure.json --design design.png --assets sprite_dir --inventory assets_inventory.json --report out/validate_report.json
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

from PIL import Image
from asset_inventory import load_asset_index


IMAGE_EXTS = {".png", ".jpg", ".jpeg"}
ELEMENT_TYPES = {"container", "image", "text", "button", "overlay", "rect"}
LAYOUT_TYPES = {"row", "column"}
LAYOUT_SPACING = {"even"}
LAYOUT_ALIGN = {
    "start", "center", "middle", "end", "left", "right", "top", "bottom",
    "space-between", "space-around", "space-evenly",
}
ALIGN_X = {"left", "center", "right", "start", "end"}
ALIGN_Y = {"top", "middle", "bottom", "start", "end", "center"}
TEXT_ALIGN = {"left", "center", "right"}
TEXT_VALIGN = {"top", "middle", "bottom", "start", "end", "center"}
COLOR_RE = re.compile(r"^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")
UNITY_GUID_RE = re.compile(r"^[0-9a-fA-F]{32}$")
UNITY_CONFIG_KEYS = {"schemaVersion", "outputPrefabPath", "spriteRootFolder"}
REMOVED_UNITY_CONFIG_KEYS = {"reportPath", "wrapWithCanvas", "addButtonComponents"}


class Reporter:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, path: str, message: str) -> None:
        self.errors.append(f"{path}: {message}")

    def warn(self, path: str, message: str) -> None:
        self.warnings.append(f"{path}: {message}")


def is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def positive_number(value: Any) -> bool:
    return is_number(value) and value > 0


def non_negative_number(value: Any) -> bool:
    return is_number(value) and value >= 0


def normalize_unity_path(value: str) -> str:
    return value.replace("\\", "/").strip().rstrip("/")


def is_unity_asset_path(value: str) -> bool:
    normalized = normalize_unity_path(value)
    return (
        normalized == "Assets"
        or normalized.startswith("Assets/")
        or normalized == "Packages"
        or normalized.startswith("Packages/")
    )


def is_unity_project_folder(value: str) -> bool:
    normalized = normalize_unity_path(value)
    return (
        is_unity_asset_path(normalized)
        or "/Assets/" in normalized
        or normalized.endswith("/Assets")
        or "/Packages/" in normalized
        or normalized.endswith("/Packages")
    )


def read_json(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"Invalid JSON in {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise SystemExit("Structure root must be a JSON object")
    return data


def build_asset_index(
    assets_dir: Path,
    inventory_path: Path | None = None,
) -> tuple[
    dict[str, Path],
    set[str],
    dict[str, dict[str, int]],
    dict[str, Path],
    dict[str, str],
    dict[Path, set[str]],
]:
    indexed = load_asset_index(assets_dir, inventory_path)
    return (
        indexed.index,
        indexed.duplicate_basenames,
        indexed.borders,
        indexed.guid_index,
        indexed.guid_by_key,
        indexed.sprite_names_by_path,
    )

def check_xy_dict(obj: Any, path: str, field: str, reporter: Reporter) -> None:
    if not isinstance(obj, dict):
        reporter.error(path, f"{field} must be an object")
        return
    for key in ("x", "y"):
        if key in obj and not is_number(obj[key]):
            reporter.error(path, f"{field}.{key} must be a number")


def check_size(obj: Any, path: str, reporter: Reporter) -> None:
    if not isinstance(obj, dict):
        reporter.error(path, "size must be an object")
        return
    for key in ("width", "height"):
        if key not in obj:
            reporter.error(path, f"size.{key} is required")
        elif not positive_number(obj[key]):
            reporter.error(path, f"size.{key} must be a positive number")


def check_color(value: Any, path: str, field: str, reporter: Reporter) -> None:
    if not isinstance(value, str) or not COLOR_RE.match(value):
        reporter.warn(path, f"{field} should be #RRGGBB or #RRGGBBAA")


def check_layout(layout: Any, path: str, reporter: Reporter) -> None:
    if not isinstance(layout, dict):
        reporter.error(path, "layout must be an object")
        return
    ltype = layout.get("type", "row")
    if ltype not in LAYOUT_TYPES:
        reporter.error(path, "layout.type must be row or column")
    spacing = layout.get("spacing", 0)
    if isinstance(spacing, str) and spacing not in LAYOUT_SPACING:
        reporter.error(path, "layout.spacing string must be even")
    elif not isinstance(spacing, str) and not is_number(spacing):
        reporter.error(path, "layout.spacing must be a number or even")
    if "padding" in layout:
        padding = layout["padding"]
        if is_number(padding):
            pass
        elif isinstance(padding, dict):
            for key in ("x", "y"):
                if key in padding and not is_number(padding[key]):
                    reporter.error(path, f"layout.padding.{key} must be a number")
        else:
            reporter.error(path, "layout.padding must be a number or {x,y}")
    if "align" in layout and layout["align"] not in LAYOUT_ALIGN:
        reporter.warn(path, f"unknown layout.align: {layout['align']}")
    if "vAlign" in layout and layout["vAlign"] not in LAYOUT_ALIGN:
        reporter.warn(path, f"unknown layout.vAlign: {layout['vAlign']}")


def check_unity_config(structure: dict[str, Any], reporter: Reporter) -> None:
    unity = structure.get("unity")
    if not isinstance(unity, dict):
        reporter.error("unity", "unity object is required for Unity prefab generation")
        return

    for key in unity:
        if key in REMOVED_UNITY_CONFIG_KEYS:
            reporter.error("unity", f"{key} is no longer supported; the Unity tool handles this by default")
        elif key not in UNITY_CONFIG_KEYS:
            reporter.warn("unity", f"unknown unity config field: {key}")

    schema_version = unity.get("schemaVersion", 1)
    if not isinstance(schema_version, int) or isinstance(schema_version, bool):
        reporter.error("unity.schemaVersion", "schemaVersion must be an integer")

    output_path = unity.get("outputPrefabPath")
    if not isinstance(output_path, str) or not output_path.strip():
        reporter.error("unity.outputPrefabPath", "outputPrefabPath is required")
    else:
        normalized_output = output_path.replace("\\", "/").strip()
        if not normalized_output.startswith("Assets/"):
            reporter.error("unity.outputPrefabPath", "outputPrefabPath must start with Assets/")
        if not normalized_output.lower().endswith(".prefab"):
            reporter.error("unity.outputPrefabPath", "outputPrefabPath must end with .prefab")

    sprite_root = unity.get("spriteRootFolder")
    if not isinstance(sprite_root, str) or not sprite_root.strip():
        reporter.error("unity.spriteRootFolder", "spriteRootFolder is required and should come from the prompt's sliced asset directory")
    elif not is_unity_project_folder(sprite_root):
        reporter.error("unity.spriteRootFolder", "spriteRootFolder must be a Unity Assets/ or Packages/ folder path")


def validate_tree(
    elem: Any,
    path: str,
    parent_layout_type: str | None,
    reporter: Reporter,
    asset_index: dict[str, Path],
    duplicate_basenames: set[str],
    asset_borders: dict[str, dict[str, int]],
    asset_guid_index: dict[str, Path],
    asset_guid_by_key: dict[str, str],
    sprite_names_by_path: dict[Path, set[str]],
    seen_paths: set[str],
    stats: dict[str, int],
) -> None:
    if not isinstance(elem, dict):
        reporter.error(path, "element must be an object")
        return

    name = elem.get("name")
    if not isinstance(name, str) or not name:
        reporter.error(path, "name is required")
        name = "?"
    etype = elem.get("type")
    if etype not in ELEMENT_TYPES:
        reporter.error(path, f"type must be one of {sorted(ELEMENT_TYPES)}")
    if "size" not in elem:
        reporter.error(path, "size is required")
    else:
        check_size(elem["size"], path, reporter)

    if path in seen_paths:
        reporter.error(path, "duplicate element path")
    seen_paths.add(path)
    stats["elements"] += 1
    if etype == "text":
        stats["texts"] += 1
    if elem.get("layout"):
        stats["layouts"] += 1
    is_derived_position = parent_layout_type is not None or bool(elem.get("align") or elem.get("vAlign"))
    if is_derived_position:
        stats["derived_positioned"] += 1
    if "position" in elem:
        stats["positioned"] += 1
        if not is_derived_position:
            stats["free_positioned"] += 1
        check_xy_dict(elem["position"], path, "position", reporter)
    elif parent_layout_type is None and path != "root" and not (elem.get("align") or elem.get("vAlign")):
        reporter.warn(path, "missing position outside layout/alignment derivation")

    if parent_layout_type == "row" and "align" in elem:
        reporter.warn(path, "child align is ignored in row layout; use vAlign or offset")
    if parent_layout_type == "column" and "vAlign" in elem:
        reporter.warn(path, "child vAlign is ignored in column layout; use align or offset")
    if parent_layout_type is not None and "position" in elem:
        reporter.warn(path, "position is ignored inside parent layout; use offset")

    if "align" in elem and elem["align"] not in ALIGN_X:
        reporter.warn(path, f"unknown align: {elem['align']}")
    if "vAlign" in elem and elem["vAlign"] not in ALIGN_Y:
        reporter.warn(path, f"unknown vAlign: {elem['vAlign']}")
    if "offset" in elem:
        check_xy_dict(elem["offset"], path, "offset", reporter)
    if "opacity" in elem:
        opacity = elem["opacity"]
        if not is_number(opacity) or opacity < 0 or opacity > 1:
            reporter.error(path, "opacity must be between 0 and 1")
    if "color" in elem:
        check_color(elem["color"], path, "color", reporter)
    if etype == "overlay" and not (elem.get("asset") or elem.get("color")):
        reporter.error(path, "overlay must have color or asset")
    if etype == "rect":
        if not elem.get("color"):
            reporter.error(path, "rect must have color")
        if elem.get("asset"):
            reporter.warn(path, "rect should not use asset; use image for sliced sprites")

    asset = elem.get("asset")
    asset_guid = elem.get("assetGuid")
    sprite_name = elem.get("spriteName")
    asset_path: Path | None = None
    guid_path: Path | None = None
    if asset:
        stats["asset_refs"] += 1
        if not isinstance(asset, str):
            reporter.error(path, "asset must be a string")
        else:
            key = asset.replace("\\", "/").lower()
            if "/" not in key and key in duplicate_basenames:
                reporter.error(path, f"asset basename is ambiguous, use relative path: {asset}")
            elif key not in asset_index:
                reporter.error(path, f"asset not found: {asset}")
            else:
                asset_path = asset_index[key]
            nine_slice = elem.get("nineSlice")
            if nine_slice in ("meta", "auto", True) and key not in asset_borders:
                reporter.warn(path, f"nineSlice {nine_slice!r} has no Unity spriteBorder metadata; renderer will infer margins")
            if not asset_guid:
                stats["asset_refs_without_guid"] += 1
                if isinstance(asset, str) and not is_unity_asset_path(asset):
                    stats["asset_refs_needing_sprite_root"] += 1
    if sprite_name is not None and (not isinstance(sprite_name, str) or not sprite_name.strip()):
        reporter.error(path, "spriteName must be a non-empty string")
    if asset_guid:
        stats["asset_guid_refs"] += 1
        if not isinstance(asset_guid, str):
            reporter.error(path, "assetGuid must be a string")
        else:
            guid_key = asset_guid.strip().lower()
            if not UNITY_GUID_RE.match(guid_key):
                reporter.warn(path, f"assetGuid should be a 32-character Unity GUID: {asset_guid}")
            guid_path = asset_guid_index.get(guid_key)
            if guid_path is None:
                reporter.error(path, f"assetGuid not found in Unity metadata: {asset_guid}")
            if asset_path is not None and guid_path is not None and asset_path != guid_path:
                reporter.warn(path, "asset and assetGuid refer to different files")
            if asset_path is not None and isinstance(asset, str):
                asset_key = asset.replace("\\", "/").lower()
                asset_guid_for_key = asset_guid_by_key.get(asset_key)
                if asset_guid_for_key and asset_guid_for_key != guid_key:
                    reporter.warn(path, "assetGuid does not match the selected asset path")
    elif etype == "image" and not asset:
        reporter.warn(path, "image element has no asset")

    candidate_paths = [p for p in (asset_path, guid_path) if p is not None]
    unique_paths: list[Path] = []
    for candidate_path in candidate_paths:
        if candidate_path not in unique_paths:
            unique_paths.append(candidate_path)

    if sprite_name:
        if candidate_paths:
            if len(unique_paths) > 1:
                reporter.warn(path, "spriteName will be checked against both asset and assetGuid paths")
            matched_path = None
            checked_any = False
            for candidate_path in unique_paths:
                available = sprite_names_by_path.get(candidate_path, set())
                if not available:
                    continue
                checked_any = True
                if sprite_name in available:
                    matched_path = candidate_path
                    break
            if matched_path is None:
                if checked_any:
                    reporter.error(path, f"spriteName not found for any referenced asset: {sprite_name}")
                else:
                    reporter.warn(path, "spriteName provided but no sub-sprite metadata was found for the referenced asset")
        else:
            reporter.error(path, "spriteName provided without an asset or assetGuid reference")
    else:
        ambiguous_paths = [
            candidate_path
            for candidate_path in unique_paths
            if len(sprite_names_by_path.get(candidate_path, set())) > 1
        ]
        if ambiguous_paths:
            reporter.error(path, "asset has multiple sub-sprites; add spriteName to select one explicitly")

    if "nineSlice" in elem:
        ns = elem["nineSlice"]
        if isinstance(ns, bool) or isinstance(ns, (int, float)) or ns in ("auto", "meta"):
            pass
        elif isinstance(ns, dict):
            for key in ("left", "top", "right", "bottom"):
                if key in ns and not is_number(ns[key]):
                    reporter.error(path, f"nineSlice.{key} must be a number")
        else:
            reporter.error(path, "nineSlice must be bool, number, auto/meta, or margins object")

    if etype == "text":
        if "text" not in elem:
            reporter.warn(path, "text element has no text field")
        elif not isinstance(elem["text"], str):
            reporter.error(path, "text must be a string")
        elif elem["text"] == "":
            reporter.warn(path, "text field is empty")
        if "fontSize" not in elem:
            stats["texts_missing_font_size"] += 1
            reporter.warn(path, "text element has no fontSize; run suggest_text_metrics.py or author it explicitly")
        elif not positive_number(elem["fontSize"]):
            reporter.error(path, "fontSize must be positive")
        else:
            stats["texts_with_font_size"] += 1
        if "lineHeight" in elem and not positive_number(elem["lineHeight"]):
            reporter.error(path, "lineHeight must be positive")
        if "strokeWidth" in elem and not non_negative_number(elem["strokeWidth"]):
            reporter.error(path, "strokeWidth must be a non-negative number")
        if "strokeColor" in elem:
            check_color(elem["strokeColor"], path, "strokeColor", reporter)
        if "fontFamily" in elem and not isinstance(elem["fontFamily"], str):
            reporter.error(path, "fontFamily must be a string")
        if "font" in elem and not isinstance(elem["font"], str):
            reporter.error(path, "font must be a string")
        if "alignment" in elem and elem["alignment"] not in TEXT_ALIGN:
            reporter.warn(path, f"unknown text alignment: {elem['alignment']}")
        if "textVAlign" in elem and elem["textVAlign"] not in TEXT_VALIGN:
            reporter.warn(path, f"unknown textVAlign: {elem['textVAlign']}")

    layout_type = None
    if "layout" in elem:
        check_layout(elem["layout"], path, reporter)
        if isinstance(elem["layout"], dict):
            layout_type = elem["layout"].get("type", "row")

    children = elem.get("children") or []
    if not isinstance(children, list):
        reporter.error(path, "children must be a list")
        return

    sibling_names: dict[str, int] = defaultdict(int)
    for child in children:
        if isinstance(child, dict):
            child_name = child.get("name")
            if isinstance(child_name, str):
                sibling_names[child_name] += 1
    for child_name, count in sibling_names.items():
        if count > 1:
            reporter.error(path, f"duplicate child name: {child_name}")

    for child in children:
        child_name = child.get("name", "?") if isinstance(child, dict) else "?"
        validate_tree(
            child,
            f"{path}/{child_name}",
            layout_type,
            reporter,
            asset_index,
            duplicate_basenames,
            asset_borders,
            asset_guid_index,
            asset_guid_by_key,
            sprite_names_by_path,
            seen_paths,
            stats,
        )


def validate_structure(
    structure: dict[str, Any],
    design_path: Path,
    assets_dir: Path,
    inventory_path: Path | None = None,
) -> tuple[Reporter, dict[str, int]]:
    reporter = Reporter()
    stats = defaultdict(int)

    canvas = structure.get("canvas")
    if not isinstance(canvas, dict):
        reporter.error("canvas", "canvas object is required")
    else:
        for key in ("width", "height"):
            if key not in canvas or not positive_number(canvas[key]):
                reporter.error("canvas", f"{key} must be a positive number")
        if design_path.exists() and positive_number(canvas.get("width")) and positive_number(canvas.get("height")):
            with Image.open(design_path) as img:
                if (int(canvas["width"]), int(canvas["height"])) != img.size:
                    reporter.error(
                        "canvas",
                        f"canvas {canvas['width']}x{canvas['height']} does not match design {img.width}x{img.height}",
                    )

    check_unity_config(structure, reporter)

    if "root" not in structure:
        reporter.error("root", "root object is required")
        return reporter, stats

    asset_index, duplicate_basenames, asset_borders, asset_guid_index, asset_guid_by_key, sprite_names_by_path = build_asset_index(
        assets_dir,
        inventory_path,
    )
    validate_tree(
        structure["root"],
        "root",
        None,
        reporter,
        asset_index,
        duplicate_basenames,
        asset_borders,
        asset_guid_index,
        asset_guid_by_key,
        sprite_names_by_path,
        set(),
        stats,
    )
    for key in (
        "elements", "asset_refs", "asset_guid_refs", "layouts", "texts", "positioned",
        "derived_positioned", "free_positioned", "texts_with_font_size",
        "texts_missing_font_size", "asset_refs_without_guid", "asset_refs_needing_sprite_root",
    ):
        stats[key] = stats[key]
    if stats["elements"] >= 8 and stats["layouts"] == 0 and stats["derived_positioned"] == 0:
        reporter.warn(
            "structure",
            "all elements use explicit positions; use layout/align for repeated "
            "or centered groups, or document why free positioning is intentional",
        )
    metadata = structure.get("metadata")
    if isinstance(metadata, dict):
        expected_stats = {
            "total_elements": stats["elements"],
            "derived_positioned_elements": stats["derived_positioned"],
            "free_positioned_elements": stats["free_positioned"],
        }
        for key, expected in expected_stats.items():
            if key in metadata and metadata[key] != expected:
                reporter.warn("metadata", f"{key} is {metadata[key]} but current count is {expected}")
    return reporter, stats


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--structure", required=True, help="Path to ui_structure.json")
    ap.add_argument("--design", required=True, help="Path to the source design image")
    ap.add_argument("--assets", required=True, help="Directory containing sliced PNG assets")
    ap.add_argument("--inventory", help="Default: <task>/assets/assets_inventory.json")
    ap.add_argument("--json", action="store_true", help="Print machine-readable validation result")
    ap.add_argument("--report", help="Path to write the full validation JSON report")
    ap.add_argument("--warnings-as-errors", action="store_true",
                    help="Exit non-zero when warnings are present")
    args = ap.parse_args()

    structure_path = Path(args.structure)
    design_path = Path(args.design)
    assets_dir = Path(args.assets)
    if not structure_path.exists():
        raise SystemExit(f"Structure not found: {structure_path}")
    if not design_path.exists():
        raise SystemExit(f"Design image not found: {design_path}")
    if not assets_dir.exists():
        raise SystemExit(f"Assets directory not found: {assets_dir}")

    structure = read_json(structure_path)
    inventory_path = Path(args.inventory) if args.inventory else structure_path.parent / "assets" / "assets_inventory.json"
    reporter, stats = validate_structure(structure, design_path, assets_dir, inventory_path)
    result = {
        "valid": not reporter.errors and not (args.warnings_as_errors and reporter.warnings),
        "error_count": len(reporter.errors),
        "warning_count": len(reporter.warnings),
        "stats": dict(stats),
        "errors": reporter.errors,
        "warnings": reporter.warnings,
    }

    if args.report:
        report_path = Path(args.report)
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(
            json.dumps(result, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print(f"Validated {structure_path}")
        print(f"  elements: {stats['elements']}")
        print(f"  asset refs: {stats['asset_refs']}")
        print(f"  asset GUID refs: {stats['asset_guid_refs']}")
        print(f"  asset refs without GUID: {stats['asset_refs_without_guid']}")
        print(f"  asset refs needing spriteRootFolder: {stats['asset_refs_needing_sprite_root']}")
        print(f"  layouts: {stats['layouts']}")
        print(f"  derived positioned: {stats['derived_positioned']}")
        print(f"  free positioned: {stats['free_positioned']}")
        print(f"  text elements: {stats['texts']}")
        print(f"  texts with fontSize: {stats['texts_with_font_size']}")
        print(f"  texts missing fontSize: {stats['texts_missing_font_size']}")
        print(f"  errors: {len(reporter.errors)}")
        print(f"  warnings: {len(reporter.warnings)}")
        if args.report:
            print(f"  report: {args.report}")
        for item in reporter.errors[:10]:
            print(f"  [error] {item}")
        if len(reporter.errors) > 10:
            print(f"  ... {len(reporter.errors) - 10} more errors in report")
        for item in reporter.warnings[:10]:
            print(f"  [warn] {item}")
        if len(reporter.warnings) > 10:
            print(f"  ... {len(reporter.warnings) - 10} more warnings in report")
        if result["valid"]:
            print("Structure is valid")

    sys.exit(0 if result["valid"] else 1)


if __name__ == "__main__":
    main()
