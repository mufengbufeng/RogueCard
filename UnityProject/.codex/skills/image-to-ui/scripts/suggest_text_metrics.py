"""
Suggest missing text metrics for ui_structure.json text elements.

Text remains a first-class UI element. This helper only fills text rendering
fields on `type: "text"` nodes; it does not create image assets or resource
references.

Usage:
    py -B suggest_text_metrics.py --structure ui_structure.json --report text_metrics_report.json
    py -B suggest_text_metrics.py --structure ui_structure.json --write --report text_metrics_report.json
    py -B suggest_text_metrics.py --structure ui_structure.json --write --output-structure ui_structure_text.json

The estimate works best when each text element's `size` comes from the same
grid/bbox workflow used for image elements.
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


FONT_SEARCH_DIRS: list[Path] = []


def is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def to_int(value: Any, default: int = 0) -> int:
    if is_number(value):
        return int(round(float(value)))
    return default


def find_font(size: int, preferred: str | None = None) -> ImageFont.FreeTypeFont:
    candidates: list[str] = []
    if preferred:
        preferred_path = Path(preferred)
        candidates.append(str(preferred_path))
        for root in FONT_SEARCH_DIRS:
            candidates.append(str(root / preferred))
        if preferred_path.suffix:
            stem = preferred_path.stem
        else:
            stem = preferred
            for root in FONT_SEARCH_DIRS:
                candidates.extend(str(p) for p in root.glob(f"{stem}*.ttf"))
                candidates.extend(str(p) for p in root.glob(f"{stem}*.otf"))

    candidates.extend([
        "C:/Windows/Fonts/arialbd.ttf",
        "C:/Windows/Fonts/arial.ttf",
        "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/msyh.ttc",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
    ])

    for candidate in candidates:
        if os.path.exists(candidate):
            try:
                return ImageFont.truetype(candidate, size)
            except Exception:
                continue
    return ImageFont.load_default()


def read_json(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"Invalid JSON in {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise SystemExit("Structure root must be a JSON object")
    return data


def text_lines(text: str) -> list[str]:
    lines = text.split("\n")
    return lines if lines else [""]


def measure_text(text: str, font: ImageFont.FreeTypeFont,
                 line_height: int, stroke_width: int) -> tuple[int, int]:
    draw = ImageDraw.Draw(Image.new("RGBA", (1, 1), (0, 0, 0, 0)))
    min_left = 0
    min_top: int | None = None
    max_right = 0
    max_bottom = 0

    for index, line in enumerate(text_lines(text)):
        probe = line if line else " "
        try:
            bbox = draw.textbbox(
                (0, 0),
                probe,
                font=font,
                stroke_width=max(0, stroke_width),
            )
        except Exception:
            fallback_size = max(1, int(getattr(font, "size", 12)))
            width = max(0, len(line)) * fallback_size // 2
            bbox = (0, 0, width, fallback_size)

        y = index * line_height
        min_left = min(min_left, bbox[0])
        max_right = max(max_right, bbox[2])
        top = y + bbox[1]
        bottom = y + bbox[3]
        min_top = top if min_top is None else min(min_top, top)
        max_bottom = max(max_bottom, bottom)

    width = max(0, max_right - min_left)
    height = max(0, max_bottom - (min_top or 0))
    return width, height


def iter_elements(elem: Any, path: str):
    if not isinstance(elem, dict):
        return
    yield path, elem
    for child in elem.get("children") or []:
        child_name = child.get("name", "?") if isinstance(child, dict) else "?"
        yield from iter_elements(child, f"{path}/{child_name}")


def text_box(elem: dict[str, Any]) -> tuple[int, int]:
    size = elem.get("size") or {}
    if not isinstance(size, dict):
        return 0, 0
    return to_int(size.get("width")), to_int(size.get("height"))


def candidate_line_height(elem: dict[str, Any], font_size: int,
                          line_gap: int) -> int:
    existing = elem.get("lineHeight")
    if is_number(existing) and existing > 0:
        return to_int(existing)
    return max(1, font_size + line_gap)


def suggest_for_text(elem: dict[str, Any], min_size: int, max_size: int,
                     line_gap: int) -> dict[str, Any] | None:
    text = elem.get("text")
    if not isinstance(text, str) or not text:
        return None

    box_w, box_h = text_box(elem)
    if box_w <= 0 or box_h <= 0:
        return None

    lines = text_lines(text)
    stroke_width = to_int(elem.get("strokeWidth"), 0)
    preferred = elem.get("fontFamily") or elem.get("font")
    if preferred is not None and not isinstance(preferred, str):
        preferred = None

    # Cap the search by the authored text box height. Width fitting can reduce
    # the result further for long labels.
    max_by_height = max(
        min_size,
        (box_h - max(0, len(lines) - 1) * line_gap) // max(1, len(lines)),
    )
    high = max(min_size, min(max_size, max_by_height))
    low = max(1, min_size)
    best: dict[str, Any] | None = None

    while low <= high:
        mid = (low + high) // 2
        line_height = candidate_line_height(elem, mid, line_gap)
        font = find_font(mid, preferred)
        measured_w, measured_h = measure_text(text, font, line_height, stroke_width)
        if measured_w <= box_w and measured_h <= box_h:
            best = {
                "fontSize": mid,
                "lineHeight": line_height,
                "measured": {"width": measured_w, "height": measured_h},
            }
            low = mid + 1
        else:
            high = mid - 1

    if best is None:
        font = find_font(min_size, preferred)
        line_height = candidate_line_height(elem, min_size, line_gap)
        measured_w, measured_h = measure_text(text, font, line_height, stroke_width)
        best = {
            "fontSize": min_size,
            "lineHeight": line_height,
            "measured": {"width": measured_w, "height": measured_h},
        }
    return best


def apply_suggestion(elem: dict[str, Any], suggestion: dict[str, Any],
                     add_line_height: bool) -> None:
    elem["fontSize"] = suggestion["fontSize"]
    if add_line_height and "lineHeight" not in elem and "\n" in elem.get("text", ""):
        elem["lineHeight"] = suggestion["lineHeight"]


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--structure", required=True, help="Input ui_structure.json")
    ap.add_argument("--output-structure",
                    help="Where to write the updated structure. Defaults to --structure when --write is used.")
    ap.add_argument("--report", help="Path to write text_metrics_report.json")
    ap.add_argument("--write", action="store_true",
                    help="Write suggestions into the structure")
    ap.add_argument("--force", action="store_true",
                    help="Recompute fontSize even when the text element already has one")
    ap.add_argument("--add-line-height", action="store_true",
                    help="Add lineHeight for multi-line text when it is missing")
    ap.add_argument("--min-font-size", type=int, default=8)
    ap.add_argument("--max-font-size", type=int, default=180)
    ap.add_argument("--line-gap", type=int, default=4,
                    help="Default gap used when suggesting lineHeight")
    ap.add_argument("--font-search-dir", action="append", default=[],
                    help="Optional directory to search when fontFamily/font is present")
    args = ap.parse_args()

    structure_path = Path(args.structure)
    if not structure_path.exists():
        raise SystemExit(f"Structure not found: {structure_path}")
    if args.min_font_size <= 0:
        raise SystemExit("--min-font-size must be positive")
    if args.max_font_size < args.min_font_size:
        raise SystemExit("--max-font-size must be >= --min-font-size")

    global FONT_SEARCH_DIRS
    FONT_SEARCH_DIRS = [
        structure_path.parent,
        structure_path.parent.parent,
        Path.cwd(),
        *(Path(p) for p in args.font_search_dir),
    ]

    structure = read_json(structure_path)
    root = structure.get("root")
    items: list[dict[str, Any]] = []
    text_total = 0
    updated = 0

    if isinstance(root, dict):
        root_name = root.get("name", "root")
        for path, elem in iter_elements(root, root_name):
            if elem.get("type") != "text":
                continue
            text_total += 1
            box_w, box_h = text_box(elem)
            item: dict[str, Any] = {
                "path": path,
                "text": elem.get("text", ""),
                "box": {"width": box_w, "height": box_h},
                "existing": {
                    "fontSize": elem.get("fontSize"),
                    "lineHeight": elem.get("lineHeight"),
                    "strokeWidth": elem.get("strokeWidth"),
                },
            }

            if "fontSize" in elem and not args.force:
                item["action"] = "skipped"
                item["reason"] = "fontSize already present"
                items.append(item)
                continue

            suggestion = suggest_for_text(
                elem,
                args.min_font_size,
                args.max_font_size,
                args.line_gap,
            )
            if suggestion is None:
                item["action"] = "skipped"
                item["reason"] = "missing text or valid text box"
                items.append(item)
                continue

            item["action"] = "updated" if args.write else "suggested"
            item["suggested"] = {
                "fontSize": suggestion["fontSize"],
                "lineHeight": suggestion["lineHeight"],
            }
            item["measured"] = suggestion["measured"]
            items.append(item)

            if args.write:
                apply_suggestion(elem, suggestion, args.add_line_height)
                updated += 1

    result = {
        "structure": str(structure_path),
        "write": bool(args.write),
        "force": bool(args.force),
        "text_total": text_total,
        "updated_count": updated,
        "suggested_count": sum(1 for item in items if item.get("action") == "suggested"),
        "skipped_count": sum(1 for item in items if item.get("action") == "skipped"),
        "items": items,
    }

    if args.write:
        output_path = Path(args.output_structure) if args.output_structure else structure_path
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(
            json.dumps(structure, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    if args.report:
        report_path = Path(args.report)
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(
            json.dumps(result, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    print(f"Scanned text elements: {text_total}")
    if args.write:
        print(f"Updated text elements: {updated}")
    else:
        print(f"Suggested text elements: {result['suggested_count']}")
    print(f"Skipped text elements: {result['skipped_count']}")
    if args.report:
        print(f"Report: {args.report}")


if __name__ == "__main__":
    main()
