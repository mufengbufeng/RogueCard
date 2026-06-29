# Structure Validation Reference

Use this reference when `validate_structure.py` reports errors or warnings.

## Command

```bash
py -B <skill>/scripts/validate_structure.py --structure out/<design-stem>/ui_structure.json --design design.png --assets sprite_dir --report out/<design-stem>/validate_report.json
```

Default stdout is compact. Read `validate_report.json` for full errors and
warnings. Add `--json` only when the full report must be printed to stdout.
When `assets_inventory.json` exists next to the task assets, the validator uses
it first and only falls back to `.meta` scanning when the inventory is missing
or unreadable.

Run the final workflow gate after bbox and comparison review:

```bash
py -B <skill>/scripts/validate_workflow.py --task out/<design-stem> --report out/<design-stem>/workflow_report.json
```

`validate_structure.py` checks the JSON itself. `validate_workflow.py` checks
the handoff artifacts: alignment review coverage, final comparison review,
required generated files, and layout/free-positioning stats.

## Fix Priority

1. Fix all errors before bbox alignment.
2. Fix warnings that affect the current design.
3. Use `--warnings-as-errors` for strict handoff.

## Common Issues

- Canvas mismatch: set `canvas.width` / `canvas.height` to the design's native
  size or rerun `scale_structure.py`.
- Missing or ambiguous asset: copy the exact path from `assets_inventory.json`.
- Missing or mismatched `assetGuid`: rerun `inventory_assets.py` against the
  Unity sprite folder and copy the selected item's `guid` into `assetGuid`.
  Keep `asset` as the readable fallback and sub-sprite hint.
- Missing `spriteName` for a multi-sprite texture: add the selected entry's
  `spriteName` from `assets_inventory.json` before generating the prefab; the
  validator and Unity builder reject ambiguous multi-sprite references.
- Mismatched `spriteName`: make sure it matches the chosen sub-sprite in the
  inventory entry for the referenced `asset` or `assetGuid`; this is an error.
- Position inside layout: remove child `position` and use `offset`, or remove
  the parent layout if the child needs an independent position.
- All elements use explicit positions: refactor obvious rows, columns, and
  centered groups to use `layout` / `align` / `vAlign`, or document why free
  positioning is intentional in `comparison_review.md`.
- Image without asset: add `asset`, change the element to `rect` if it is an
  engine-generated colored rectangle, or remove the non-visual node.
- Rect without color: add `color`, or change the element type if it is only a
  logical grouping node.
- Text without `fontSize`: keep the element as `type: "text"`, verify its
  `position` and `size` from the grid, then author `fontSize` directly or run
  `suggest_text_metrics.py` to fill missing text metrics.
- Text overflow warning in comparison: inspect the text element's box first.
  If the box is correct, reduce `fontSize`, adjust `lineHeight`, or increase
  the text box size according to the design.
- Missing `unity` object: add top-level `unity.schemaVersion`,
  `unity.outputPrefabPath`, and `unity.spriteRootFolder`.
- Invalid `unity.outputPrefabPath`: use a prompt-derived path that starts with
  `Assets/` and ends with `.prefab`.
- Invalid `unity.spriteRootFolder`: use the prompt's sliced Sprite directory,
  normalized to `Assets/...` or `Packages/...` when it is inside a Unity
  project. Do not copy sprites to satisfy this field.
- Removed Unity config field: delete `reportPath`, `wrapWithCanvas`, or
  `addButtonComponents`; the Unity tool handles these by default.
