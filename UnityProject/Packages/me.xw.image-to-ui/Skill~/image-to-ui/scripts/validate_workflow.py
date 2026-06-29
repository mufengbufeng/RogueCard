"""
Validate the final image-to-ui workflow handoff artifacts.

This script checks the parts that can be enforced mechanically after the
human/AI visual review has happened:
  - required workflow outputs exist in the task folder
  - validate_report.json has no structure errors
  - all_elements_legend.json paths are covered by alignment_review.md
  - no final alignment status is left as adjusted or needs-targeted-check
  - adjusted rows name at least one recheck artifact
  - comparison_review.md records an accepted final comparison status
  - the structure exposes basic layout-vs-free-positioning stats

Usage:
    py -B validate_workflow.py --task out/design-stem --report out/design-stem/workflow_report.json
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any


ALIGNMENT_FINAL_STATUSES = {"aligned", "skipped"}
ALIGNMENT_INTERMEDIATE_STATUSES = {"adjusted", "needs-targeted-check"}
ALIGNMENT_STATUSES = ALIGNMENT_FINAL_STATUSES | ALIGNMENT_INTERMEDIATE_STATUSES
COMPARISON_ACCEPTED_STATUSES = {"accepted", "accepted-with-notes"}


class Reporter:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, message: str) -> None:
        self.errors.append(message)

    def warn(self, message: str) -> None:
        self.warnings.append(message)


def read_json(path: Path, reporter: Reporter) -> dict[str, Any] | None:
    if not path.exists():
        reporter.error(f"missing file: {path}")
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        reporter.error(f"invalid JSON in {path}: {exc}")
        return None
    if not isinstance(data, dict):
        reporter.error(f"JSON root must be an object: {path}")
        return None
    return data


def strip_markdown(value: str) -> str:
    value = value.strip()
    value = value.strip("`")
    if value.startswith("<") and value.endswith(">"):
        value = value[1:-1].strip()
    return value.strip()


def is_separator_row(cells: list[str]) -> bool:
    return all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells)


def parse_alignment_rows(path: Path, reporter: Reporter) -> list[dict[str, str]]:
    if not path.exists():
        reporter.error(f"missing file: {path}")
        return []
    rows: list[dict[str, str]] = []
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line.startswith("|") or not line.endswith("|"):
            continue
        cells = [strip_markdown(cell) for cell in line.strip("|").split("|")]
        if not cells or is_separator_row(cells):
            continue
        status = cells[0].lower()
        if status == "status":
            continue
        if status not in ALIGNMENT_STATUSES:
            continue
        rows.append({
            "status": status,
            "path": cells[1] if len(cells) > 1 else "",
            "issue": cells[2] if len(cells) > 2 else "",
            "fields": cells[3] if len(cells) > 3 else "",
            "recheck": cells[4] if len(cells) > 4 else "",
            "notes": cells[5] if len(cells) > 5 else "",
        })
    if not rows:
        reporter.error(f"no alignment status table rows found: {path}")
    return rows


def parse_recheck_artifacts(value: str) -> list[str]:
    value = strip_markdown(value)
    if not value or value.lower() in {"n/a", "na", "none", "not modeled"}:
        return []
    parts = re.split(r"[,;]", value)
    return [strip_markdown(part) for part in parts if strip_markdown(part)]


def check_recheck_artifacts(task_dir: Path, rows: list[dict[str, str]],
                            reporter: Reporter) -> None:
    for row in rows:
        if row["status"] != "adjusted":
            continue
        artifacts = parse_recheck_artifacts(row["recheck"])
        if not artifacts:
            reporter.error(f"adjusted row has no recheck artifact: {row['path']}")
            continue
        for artifact in artifacts:
            candidate = Path(artifact)
            if not candidate.is_absolute():
                candidate = task_dir / candidate
            if not candidate.exists():
                reporter.warn(f"recheck artifact not found for {row['path']}: {artifact}")


def check_alignment_coverage(
    task_dir: Path,
    legend: dict[str, Any] | None,
    review_path: Path,
    reporter: Reporter,
) -> dict[str, int]:
    rows = parse_alignment_rows(review_path, reporter)
    check_recheck_artifacts(task_dir, rows, reporter)

    final_by_path: dict[str, str] = {}
    adjusted_paths: set[str] = set()
    for row in rows:
        path = row["path"]
        if not path:
            reporter.error("alignment row has empty element path")
            continue
        if row["status"] == "adjusted":
            adjusted_paths.add(path)
        final_by_path[path] = row["status"]

    targets = []
    if legend is not None:
        raw_targets = legend.get("targets")
        if not isinstance(raw_targets, list):
            reporter.error("all-elements legend must contain a targets list")
        else:
            for item in raw_targets:
                if isinstance(item, dict) and isinstance(item.get("path"), str):
                    targets.append(item["path"])

    for target in targets:
        status = final_by_path.get(target)
        if status is None:
            reporter.error(f"alignment_review.md does not cover legend path: {target}")
        elif status not in ALIGNMENT_FINAL_STATUSES:
            reporter.error(f"final alignment status is not complete for {target}: {status}")

    for path in adjusted_paths:
        if final_by_path.get(path) not in ALIGNMENT_FINAL_STATUSES:
            reporter.error(f"adjusted path was not later finalized as aligned/skipped: {path}")

    for row in rows:
        if row["status"] == "skipped" and not (row["issue"] or row["notes"]):
            reporter.warn(f"skipped row should include a reason: {row['path']}")

    return {
        "alignment_rows": len(rows),
        "legend_targets": len(targets),
        "adjusted_rows": len(adjusted_paths),
    }


def parse_comparison_status(text: str) -> str | None:
    match = re.search(
        r"(?im)^\s*(?:final\s+)?status\s*:\s*`?([a-z][a-z-]*)`?\s*$",
        text,
    )
    if match:
        return match.group(1).lower()
    for raw in text.splitlines():
        line = raw.strip()
        if not line.startswith("|") or not line.endswith("|"):
            continue
        cells = [strip_markdown(cell).lower() for cell in line.strip("|").split("|")]
        if cells and cells[0] in COMPARISON_ACCEPTED_STATUSES | {"rework-required"}:
            return cells[0]
    return None


def check_comparison_review(path: Path, require: bool, reporter: Reporter) -> dict[str, Any]:
    if not path.exists():
        message = f"missing file: {path}"
        if require:
            reporter.error(message)
        else:
            reporter.warn(message)
        return {"comparison_status": None}

    text = path.read_text(encoding="utf-8")
    status = parse_comparison_status(text)
    if status is None:
        reporter.error(f"comparison review has no final status: {path}")
    elif status not in COMPARISON_ACCEPTED_STATUSES:
        reporter.error(f"comparison review is not accepted: {status}")
    elif status == "accepted-with-notes" and "note" not in text.lower():
        reporter.warn("comparison review is accepted-with-notes but has no notes section")
    return {"comparison_status": status}


def walk_structure(elem: Any, path: str, parent_layout: bool,
                   stats: dict[str, int]) -> None:
    if not isinstance(elem, dict):
        return
    stats["elements"] += 1
    if elem.get("layout"):
        stats["layouts"] += 1
    has_position = "position" in elem
    has_alignment = bool(elem.get("align") or elem.get("vAlign"))
    if parent_layout or has_alignment:
        stats["derived_positioned"] += 1
    elif has_position:
        stats["free_positioned"] += 1

    layout = bool(elem.get("layout"))
    for child in elem.get("children") or []:
        if isinstance(child, dict):
            child_name = child.get("name", "?")
            walk_structure(child, f"{path}/{child_name}", layout, stats)


def check_structure_stats(structure: dict[str, Any] | None,
                          reporter: Reporter) -> dict[str, int]:
    stats: dict[str, int] = defaultdict(int)
    if structure is None:
        return {}
    root = structure.get("root")
    if isinstance(root, dict):
        walk_structure(root, "root", False, stats)
    for key in ("elements", "layouts", "derived_positioned", "free_positioned"):
        stats[key] = stats[key]
    out = dict(stats)
    metadata = structure.get("metadata")
    if isinstance(metadata, dict):
        if metadata.get("total_elements") not in (None, out.get("elements")):
            reporter.warn("metadata.total_elements does not match current structure count")
        if metadata.get("derived_positioned_elements") not in (None, out.get("derived_positioned")):
            reporter.warn("metadata.derived_positioned_elements does not match current structure count")
        if metadata.get("free_positioned_elements") not in (None, out.get("free_positioned")):
            reporter.warn("metadata.free_positioned_elements does not match current structure count")
    if out.get("elements", 0) >= 8 and out.get("layouts", 0) == 0 and out.get("derived_positioned", 0) == 0:
        reporter.warn(
            "structure uses only explicit positions; use layout/align for repeated "
            "or centered groups, or document why free positioning is intentional"
        )
    return out


def check_required_file(path: Path, reporter: Reporter) -> None:
    if not path.exists():
        reporter.error(f"missing file: {path}")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--task", required=True, help="Task output folder, e.g. out/design-stem")
    ap.add_argument("--structure", help="Default: <task>/ui_structure.json")
    ap.add_argument("--validate-report", help="Default: <task>/validate_report.json")
    ap.add_argument("--legend", help="Default: <task>/all_elements_legend.json")
    ap.add_argument("--alignment-review", help="Default: <task>/alignment_review.md")
    ap.add_argument("--comparison-review", help="Default: <task>/comparison_review.md")
    ap.add_argument("--comparison", help="Default: <task>/comparison.png")
    ap.add_argument("--design-metrics", help="Default: <task>/design_grid_metrics.json")
    ap.add_argument("--assets-inventory", help="Default: <task>/assets/assets_inventory.json")
    ap.add_argument("--allow-missing-comparison-review", action="store_true",
                    help="Legacy mode for old outputs. New workflow runs should not use this.")
    ap.add_argument("--warnings-as-errors", action="store_true",
                    help="Exit non-zero when warnings are present")
    ap.add_argument("--json", action="store_true", help="Print full JSON result")
    ap.add_argument("--report", help="Write JSON report to this path")
    args = ap.parse_args()

    reporter = Reporter()
    task_dir = Path(args.task)
    if not task_dir.exists():
        reporter.error(f"missing task folder: {task_dir}")

    structure_path = Path(args.structure) if args.structure else task_dir / "ui_structure.json"
    validate_report_path = Path(args.validate_report) if args.validate_report else task_dir / "validate_report.json"
    legend_path = Path(args.legend) if args.legend else task_dir / "all_elements_legend.json"
    alignment_review_path = Path(args.alignment_review) if args.alignment_review else task_dir / "alignment_review.md"
    comparison_review_path = Path(args.comparison_review) if args.comparison_review else task_dir / "comparison_review.md"
    comparison_path = Path(args.comparison) if args.comparison else task_dir / "comparison.png"
    design_metrics_path = Path(args.design_metrics) if args.design_metrics else task_dir / "design_grid_metrics.json"
    assets_inventory_path = Path(args.assets_inventory) if args.assets_inventory else task_dir / "assets" / "assets_inventory.json"

    for required_path in (comparison_path, design_metrics_path, assets_inventory_path):
        check_required_file(required_path, reporter)

    structure = read_json(structure_path, reporter)
    validate_report = read_json(validate_report_path, reporter)
    legend = read_json(legend_path, reporter)

    if validate_report is not None:
        if not validate_report.get("valid"):
            reporter.error("validate_report.json is not valid")
        if validate_report.get("error_count", 0):
            reporter.error(f"validate_report.json has {validate_report.get('error_count')} errors")
        if validate_report.get("warning_count", 0):
            reporter.warn(f"validate_report.json has {validate_report.get('warning_count')} warnings")

    alignment_stats = check_alignment_coverage(task_dir, legend, alignment_review_path, reporter)
    comparison_stats = check_comparison_review(
        comparison_review_path,
        require=not args.allow_missing_comparison_review,
        reporter=reporter,
    )
    structure_stats = check_structure_stats(structure, reporter)

    result = {
        "valid": not reporter.errors and not (args.warnings_as_errors and reporter.warnings),
        "error_count": len(reporter.errors),
        "warning_count": len(reporter.warnings),
        "errors": reporter.errors,
        "warnings": reporter.warnings,
        "stats": {
            **alignment_stats,
            **comparison_stats,
            **structure_stats,
        },
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
        print(f"Validated workflow task: {task_dir}")
        print(f"  errors: {len(reporter.errors)}")
        print(f"  warnings: {len(reporter.warnings)}")
        for item in reporter.errors[:10]:
            print(f"  [error] {item}")
        if len(reporter.errors) > 10:
            print(f"  ... {len(reporter.errors) - 10} more errors")
        for item in reporter.warnings[:10]:
            print(f"  [warn] {item}")
        if len(reporter.warnings) > 10:
            print(f"  ... {len(reporter.warnings) - 10} more warnings")
        if result["valid"]:
            print("Workflow handoff is valid")

    sys.exit(0 if result["valid"] else 1)


if __name__ == "__main__":
    main()
