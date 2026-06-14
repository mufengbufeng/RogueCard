---
name: image-to-ui
description: "Extract Unity-ready structured UI data from design images. Use this skill when the user provides a UI screenshot or design mockup plus a Unity sliced Sprite directory and wants layout hierarchy, element positions, text content, asset references, Unity asset GUIDs, sub-sprite names, prefab output settings, and ui_structure.json for the Image To UI Unity prefab builder. Trigger when users mention \"UI analysis\", \"extract UI structure\", \"parse design\", \"UI to JSON\", \"analyze layout\", \"screenshot to UI JSON\", \"design image plus sprites\", \"\u622a\u56fe\u8f6c UI JSON\", \"\u751f\u6210 ui_structure.json\", \"Unity UI \u914d\u7f6e\", \"Unity prefab\", or \"\u6839\u636e\u8bbe\u8ba1\u56fe\u548c Sprite \u76ee\u5f55\u8fd8\u539f UI\"."
---

# Image to UI Structure Extractor

Extract Unity-ready `ui_structure.json` from a UI design image and an existing
Unity sliced Sprite directory. The workflow is asset inventory in place, grid
reading, JSON authoring with Unity prefab settings, JSON validation, bbox
verification, final reconstruction comparison, and workflow gating.

## Inputs

1. **Design image** - full UI mockup, PNG/JPG.
2. **Unity sliced Sprite directory** - the prompt's existing cut-image folder.
   Prefer a Unity `Assets/...` or `Packages/...` folder with `.png.meta` files.
   Do not copy or move sliced assets during the workflow.
3. **Prefab output intent** - full prefab path or prefab output directory from
   the prompt. If omitted, use `Assets/Image-To-UI/<canvas.name or design stem>.prefab`.

## Workflow

```
0. inventory_assets.py    -> assets_inventory.json + assets_contact_sheet.png
1. annotate_grid.py       -> design_grid.png + design_grid_metrics.json
2. Write ui_structure.json from the grid, asset inventory, text boxes, and Unity settings
3. validate_structure.py  -> field, asset, canvas, layout, and Unity handoff checks
4. annotate_element.py    -> all-elements bbox overview, then targeted bboxes
5. render_comparison.py   -> final design/reconstruction comparison
6. validate_workflow.py   -> final artifact and review gate
```

Use the prompt's sliced Sprite directory directly for every `--assets`
argument. Do not copy sliced assets into the task folder.

Use `py -B` for workflow commands so Python does not write `__pycache__`
inside the skill. If sandboxed `py` reports
`No installed Python found!`, retry the same `py` command once with escalation;
if it still fails, report the exact error and stop.

Verify interpreter and dependencies before the workflow:

```bash
py -B -c "import sys; print(sys.executable); print(sys.version)"
py -B -c "import PIL, numpy; print('deps ok')"
```

Install deps once if needed:

```bash
py -B -m pip install pillow numpy
```

Commands below are single-line so they can be pasted into PowerShell. Use one
task folder per design, such as `out/<design-stem>`, and keep all generated
artifacts for that design there. Do not reuse old outputs from another design.
Quote real paths that contain spaces.
Use script interfaces and reference files first; read script source only when
debugging the scripts themselves.

## Step 0 - Inventory Assets

```bash
py -B <skill>/scripts/inventory_assets.py --assets sprite_dir --output out/<design-stem>/assets
```

`sprite_dir` must be the sliced asset directory from the prompt. Keep that
folder as the Unity source of truth and write it to
`unity.spriteRootFolder` later; do not copy these sprites into `out/`.

Use grouped contact sheets first, especially
`assets_contact_sheet_usage_button_or_panel.png` and
`assets_contact_sheet_usage_icon.png`; use the full sheet only as fallback.
When `assets_inventory.json` lists `sub_sprites`, use that data to fill
`spriteName` for the chosen image element.
For asset path, duplicate basename, and nine-slice rules, read
`references/assets.md`.

## Step 1 - Grid the Design

```bash
py -B <skill>/scripts/annotate_grid.py --design design.png --output out/<design-stem>/design_grid.png --metrics out/<design-stem>/design_grid_metrics.json
```

Read `out/<design-stem>/design_grid_metrics.json` for `px_per_cell_x`,
`px_per_cell_y`, and native design size. `ui_structure.json`
`canvas.width` / `canvas.height` must match the design.

If scaling an older or downsampled draft:

```bash
py -B <skill>/scripts/scale_structure.py --structure draft_ui_structure.json --target-design design.png --output out/<design-stem>/ui_structure.json
```

## Step 2 - Write `ui_structure.json`

View `out/<design-stem>/design_grid.png` and the relevant grouped contact
sheets. Do not write positions from the bare design or guess assets from
filenames before checking the inventory.

For image elements, copy the selected asset's `path` and Unity `guid` from
`assets_inventory.json` when available:

```json
{ "type": "image", "asset": "Button01_l_Yellow 1.png", "assetGuid": "3695d3d93fb063441b7fd401abe8b650" }
```

`assetGuid` is the Unity resource identity used by downstream prefab tools.
Keep `asset` as a readable fallback and sub-sprite hint.
If the chosen inventory item exposes `sub_sprites`, copy the selected entry's
`spriteName` into the element too.

Add a top-level Unity build config to every final `ui_structure.json`:

```json
{
  "unity": {
    "schemaVersion": 1,
    "outputPrefabPath": "Assets/UI/Tutorial/TutorialUI.prefab",
    "spriteRootFolder": "Assets/UI/Sprites"
  }
}
```

Follow `references/schema.md` for the `unity` block. Keep only
`schemaVersion`, `outputPrefabPath`, and `spriteRootFolder`; do not add the
removed Unity config fields.

For each visible active UI element:

1. Read top-left and bottom-right grid coordinates.
2. Convert with both axis scales:
   `x = col * px_per_cell_x`, `y = row * px_per_cell_y`,
   `width = (right_col - left_col) * px_per_cell_x`,
   `height = (bottom_row - top_row) * px_per_cell_y`.
3. Convert to parent-relative coordinates.
4. Prefer `layout` for repeated rows/columns.
5. Prefer `align` / `vAlign` for centered or edge-aligned elements. Inside a
   parent `layout`, child alignment only affects the cross axis; see
   `references/schema.md`.
6. Use explicit `position` only for genuinely free placements.

Text is a first-class UI element, not a sliced image. Model visible labels,
titles, counters, and body copy as `type: "text"` with the same `position`,
`size`, `align` / `vAlign`, and parent layout rules used by image elements.
The text element's box should cover the rendered text area or the stable text
slot that the engine will own. Always set `text` and `fontSize`; add
`lineHeight`, `strokeColor`, `strokeWidth`, `alignment`, and `textVAlign` when
they affect the visible result. Use the metric suggestion helper only after the
text box and content are authored:

```bash
py -B <skill>/scripts/suggest_text_metrics.py --structure out/<design-stem>/ui_structure.json --write --report out/<design-stem>/text_metrics_report.json
```

Skip non-UI scene content behind the active UI surface, such as gameplay,
decorative scenery, or faded screens under a popup. Keep UI-owned
semi-transparent modal scrims as `overlay` elements, with the popup or active
foreground surface nested as children. Decompose composite controls into layered
children: button base + label, slot background + icon + border, avatar base +
portrait + rim.

Do not skip flat colored UI shapes just because they have no sliced asset. If a
rectangle can be generated by the engine, model it as a `rect` with `color`,
optional `opacity`, and an explicit bbox. Use `overlay` instead when that
colored layer is also the parent of a popup or foreground surface.

For schema fields, element types, nine-slice options, and text element fields,
read `references/schema.md`.

## Step 3 - Validate `ui_structure.json`

```bash
py -B <skill>/scripts/validate_structure.py --structure out/<design-stem>/ui_structure.json --design design.png --assets sprite_dir --report out/<design-stem>/validate_report.json
```

Fix all validation errors before bbox alignment. Review warnings and fix any
that affect the current design. `validate_structure.py` checks the Unity
handoff and `spriteName` against the inventory; read
`references/validation.md` for triage.

## Step 4 - BBox Alignment

Step 4 is an AI review-and-fix loop, not just an artifact generation step.
The agent must inspect bbox outputs, decide whether each active foreground
element is aligned, edit `ui_structure.json` when it is not, and rerun the
checks until accepted or explicitly skipped.

Start with one all-elements overview:

```bash
py -B <skill>/scripts/annotate_element.py --design design.png --structure out/<design-stem>/ui_structure.json --all-elements --output out/<design-stem>/all_elements.png --legend out/<design-stem>/all_elements_legend.json
```

This draws every non-full-canvas resolved bbox on the full design image with
the grid overlay. Full-canvas `overlay` and `rect` elements are also included
because they are active UI layers. Labels are numeric; read the legend JSON for
label-to-path mapping instead of printing it to stdout.

Use the overview to catch parent offsets, obvious size errors, missing groups,
and elements in the wrong area. For every visible active foreground element,
record one final review status in `out/<design-stem>/alignment_review.md`:
`aligned` or `skipped`. Use `adjusted` and `needs-targeted-check` only as
intermediate rows while working; the last row for each element path must end as
`aligned` or `skipped`. Include the element path, observed issue, JSON fields
changed, recheck output, and skip reason when applicable.

Run targeted checks for any element marked `needs-targeted-check`, any adjusted
element, and any crowded or overlapping foreground region where the overview is
not enough to judge edges or parent/child relationships:

```bash
py -B <skill>/scripts/annotate_element.py --design design.png --structure out/<design-stem>/ui_structure.json --element-path "root/level_popup/popup_header/close_button" --output out/<design-stem>/annotated.png --legend out/<design-stem>/annotated_legend.json
```

Decision rules:

- Position/size drift: adjust `position` / `size`, then rerun overview if a
  parent changed or targeted bbox if one leaf changed.
- Whole group drift: adjust the parent container before changing children.
- Missing bbox for a real design element: add the element to
  `ui_structure.json`, rerun Step 3, then rerun Step 4.
- Wrong bbox crowding or occlusion: use targeted bbox; skip only if still
  impossible to judge, and record the skip reason in `alignment_review.md`.
- Layout group drift: check the layout parent and adjust the parent
  `position`, `layout.spacing`, padding, or alignment rather than each child.
- If bbox edges align but `comparison.png` later looks wrong, treat it as a
  render issue first: inspect asset transparency, `nineSlice`, layer order,
  `opacity`, `color`, text metrics, and asset choice before changing
  coordinates.

After any JSON edit in this step, rerun Step 3 before regenerating bbox images.
Cap targeted rechecks at 3 iterations per element. Do not proceed to Step 5
until every path in `all_elements_legend.json` has a final `aligned` or
`skipped` row in `alignment_review.md`.

For detailed alignment heuristics, precision expectations, and structural
debugging, read `references/alignment.md`.

## Step 5 - Final Comparison

Choose the command before the first comparison:

```bash
py -B <skill>/scripts/render_comparison.py --design design.png --structure out/<design-stem>/ui_structure.json --assets sprite_dir --output out/<design-stem>/comparison.png
```

If the structure intentionally skips scene context behind an active popup, use
transparent background from the first comparison. This still applies when the
UI-owned modal scrim itself is modeled as an `overlay`:

```bash
py -B <skill>/scripts/render_comparison.py --design design.png --structure out/<design-stem>/ui_structure.json --assets sprite_dir --output out/<design-stem>/comparison.png --transparent-bg
```

Inspect `comparison.png` against the grid. For debugging order and precision
expectations, read `references/alignment.md`. Use `--no-grid` only for
presentation output.

Create `out/<design-stem>/comparison_review.md` after inspecting the final
comparison. Use this compact format so `validate_workflow.py` can check it:

```markdown
# Comparison Review

Final status: accepted

| Status | Area | Observed difference | Resolution |
| --- | --- | --- | --- |
| accepted | active foreground UI | none blocking | n/a |
```

Allowed final statuses are `accepted`, `accepted-with-notes`, and
`rework-required`. Do not use `accepted` when a visible active-foreground
mismatch remains unexplained. Use `accepted-with-notes` only for documented
asset/font/color limitations that are acceptable for the handoff.

## Step 6 - Workflow Gate

Run the final artifact and review gate:

```bash
py -B <skill>/scripts/validate_workflow.py --task out/<design-stem> --report out/<design-stem>/workflow_report.json
```

Fix all errors. Review all warnings; for strict handoff, rerun with
`--warnings-as-errors`. A warning about all elements using explicit positions
means the structure did not apply `layout` / `align` / `vAlign`; either refactor
obvious rows, columns, and centered groups, or document why free positioning is
intentional in `comparison_review.md`.

After this gate, Unity generation is performed in the Editor by selecting only
`ui_structure.json`. The Unity tool reads `unity.outputPrefabPath` and
`unity.spriteRootFolder`, creates the Canvas by default, adds `Button`
components for `type: "button"` by default, and reports success/warnings/errors
with an Editor dialog plus Console logs.

## Do Not

- Do not use template matching or automatic pixel correction.
- Do not add a local crop/zoom verification step.
- Do not write coordinates from the bare design image.
- Do not guess asset names before checking the generated inventory/contact
  sheet.
- Do not copy sliced assets into the task folder; use the prompt's Sprite
  directory in place.
- Do not calibrate an old example JSON; calibrate the current draft for the
  current design.
- Do not reuse `out/` artifacts from another design.
- Do not treat structural problems as coordinate drift.
- Do not write removed Unity config fields: `reportPath`, `wrapWithCanvas`, or
  `addButtonComponents`.

## Output Delivery

1. Save `ui_structure.json`.
2. Provide `comparison.png`.
3. Keep `assets_inventory.json`, `assets_contact_sheet*.png`,
   `design_grid_metrics.json`, `validate_report.json`, `workflow_report.json`,
   bbox outputs, and legend JSON files in the task folder.
4. Keep `alignment_review.md` with per-element Step 4 statuses, edits, recheck
   artifacts, and skipped/uncertain reasons.
5. Keep `comparison_review.md` with final comparison status and accepted visual
   differences.
6. Confirm the JSON contains `unity.outputPrefabPath` and
   `unity.spriteRootFolder` using the prompt or the default prefab path when
   omitted.
7. Summarize total elements, derived vs free-positioned elements, alignment
   iterations, asset inventory findings, sub-sprite usage, Unity prefab path,
   Sprite root folder, and anything skipped or uncertain.
