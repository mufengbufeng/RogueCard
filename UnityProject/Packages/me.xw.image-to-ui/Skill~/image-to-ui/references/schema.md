# UI Structure Schema

Use this reference when creating or validating fields beyond the core
workflow in `SKILL.md`.

## Full Example

```json
{
  "canvas": { "width": 1080, "height": 1920, "name": "MainUI" },
  "unity": {
    "schemaVersion": 1,
    "outputPrefabPath": "Assets/UI/MainUI/MainUI.prefab",
    "spriteRootFolder": "Assets/UI/Sprites"
  },
  "root": {
    "type": "container",
    "name": "root",
    "position": { "x": 0, "y": 0 },
    "size": { "width": 1080, "height": 1920 },
    "children": [
      {
        "type": "image",
        "name": "background",
        "position": { "x": 0, "y": 0 },
        "size": { "width": 1080, "height": 1920 },
        "asset": "bg_main.png",
        "nineSlice": true
      },
      {
        "type": "container",
        "name": "rewards_row",
        "position": { "x": 100, "y": 600 },
        "size": { "width": 880, "height": 200 },
        "layout": {
          "type": "row",
          "spacing": "even",
          "padding": { "x": 30, "y": 25 },
          "align": "space-evenly",
          "vAlign": "middle"
        },
        "children": [
          { "type": "image", "name": "gem", "size": { "width": 120, "height": 120 }, "asset": "icon_gem.png" },
          { "type": "image", "name": "coin", "size": { "width": 120, "height": 120 }, "asset": "icon_coin.png" },
          { "type": "image", "name": "key", "size": { "width": 120, "height": 120 }, "asset": "icon_key.png" }
        ]
      },
      {
        "type": "container",
        "name": "play_button",
        "position": { "x": 0, "y": 1620 },
        "size": { "width": 300, "height": 100 },
        "align": "center",
        "children": [
          { "type": "image", "name": "base", "size": { "width": 300, "height": 100 }, "asset": "btn_play.png", "nineSlice": true },
          { "type": "text", "name": "label", "size": { "width": 300, "height": 100 }, "align": "center", "vAlign": "middle", "text": "PLAY", "fontSize": 32, "color": "#FFFFFF" }
        ]
      }
    ]
  },
  "metadata": { "total_elements": 0, "notes": "" }
}
```

## Element Types

- `container`: logical group, no visual on its own.
- `image`: visual element with `asset`.
- `rect`: engine-generated solid rectangle with `color` and optional `opacity`.
- `text`: rendered text element with its own UI box and text metrics.
- `button`: interactive composite. The Unity prefab builder adds a Unity
  `Button` component for this type by default. Use `container` for visual
  button-like groups that should not receive a `Button` component.
- `overlay`: visual parent container for modal scrims, dim masks, and blockers.
  It draws its own `color` or `asset` first, then renders children on top.

## Required Fields

`type`, `name`, and `size { width, height }` are required. `position` is
required unless the element's position is fully derived from `align`/`vAlign`
or from the parent's `layout`.

## Unity Build Config

Every Unity handoff JSON must include a top-level `unity` object:

```json
{
  "unity": {
    "schemaVersion": 1,
    "outputPrefabPath": "Assets/UI/Tutorial/TutorialUI.prefab",
    "spriteRootFolder": "Assets/UI/Sprites"
  }
}
```

- `schemaVersion`: integer, currently `1`.
- `outputPrefabPath`: prefab output path. It must start with `Assets/` and end
  with `.prefab`. Generate it from the user's prompt; do not use a fixed global
  output directory. If older JSON omits it, the Unity editor falls back to
  `Assets/Image-To-UI/<canvasName>.prefab`.
- `spriteRootFolder`: the prompt's sliced Sprite directory. Prefer `Assets/...`
  or `Packages/...`; absolute project-local paths should be normalized to Unity
  project paths before final handoff.

Do not include `reportPath`, `wrapWithCanvas`, or `addButtonComponents`.
The Unity tool shows build results in a dialog/Console, always wraps generated
UI with a Canvas, and always adds Button components for `type: "button"`.

## Optional Fields

- `asset`: filename in the assets directory, matched case-insensitively. Nested asset directories are indexed. If duplicate basenames exist, use a relative path such as `icons/coin.png`.
- `assetGuid`: Unity asset GUID for image-like elements. Copy it from
  `assets_inventory.json` when available; keep `asset` as a readable fallback
  and sub-sprite hint.
- `spriteName`: sub-sprite name from `assets_inventory.json`. Use it when the
  referenced `asset` or `assetGuid` resolves to multiple sprites, and keep it
  exactly aligned with the inventory entry. Multi-sprite resources without
  `spriteName` are validation errors.
- `color`: `"#RRGGBB"`; fill color on `rect` / `overlay`, multiplicative tint
  on images, text color on text.
- `opacity`: `0.0` to `1.0`.
- `text`, `fontSize`, `lineHeight`, `strokeColor`, `strokeWidth`, `alignment`,
  `textVAlign`: text element fields. `alignment` and `textVAlign` align text
  within the text box; `align` and `vAlign` position the text box inside its
  parent.
- `nineSlice`: stretchable asset handling.
- `layout`: declare this container as a row/column group.
- `align`, `vAlign`, `offset`: derived positioning relative to parent.

Inside a parent `layout`, child-level alignment only overrides the cross axis:
`vAlign` in a row layout and `align` in a column layout. The layout keeps
ownership of the main axis. Use `offset` for small per-child nudges.

## Generated Rectangles

Use `rect` for flat rectangular UI that has no sliced asset because the engine
can generate it directly: fills, rules, progress-bar segments, simple panels,
or semi-transparent blocks that are not parents. Do not skip these just because
they are absent from the asset inventory.

```json
{
  "type": "rect",
  "name": "progress_fill",
  "position": { "x": 24, "y": 8 },
  "size": { "width": 180, "height": 18 },
  "color": "#45D86A",
  "opacity": 1
}
```

## Overlay Containers

Use `overlay` when a semi-transparent UI layer is the parent of a popup or
foreground surface. Keep scene content behind the overlay out of the structure;
model only the UI-owned scrim and its children.

```json
{
  "type": "overlay",
  "name": "modal_overlay",
  "position": { "x": 0, "y": 0 },
  "size": { "width": 1080, "height": 1920 },
  "color": "#000000",
  "opacity": 0.55,
  "children": [
    {
      "type": "container",
      "name": "level_start_popup",
      "position": { "x": 87, "y": 490 },
      "size": { "width": 906, "height": 980 },
      "children": []
    }
  ]
}
```

## Stretchable Assets

Background panels, frames, buttons, bars, popup bodies, bubbles, and slot
backgrounds should declare `nineSlice` so the renderer preserves corner detail.
Atomic icons, portraits, and character sprites should not.

```json
"nineSlice": true
"nineSlice": 30
"nineSlice": { "left": 20, "right": 20 }
"nineSlice": { "left": 30, "top": 24, "right": 30, "bottom": 40 }
```

When sliced assets come from Unity and include sidecar `.png.meta` files,
`render_comparison.py` reads `spriteBorder` automatically for
`"nineSlice": true`, `"nineSlice": "auto"`, and `"nineSlice": "meta"`. Prefer
that over hand-guessing margins; use an explicit object only when overriding
importer metadata.

## Text Elements

Text is modeled as `type: "text"`, not as an image asset. A text element uses
the same `position`, `size`, layout, alignment, bbox annotation, scaling, and
comparison workflow as image and rect elements. Author the text element's box
from the grid before tuning its text metrics.

`text` and `fontSize` should be present for every text element. `lineHeight`,
`strokeColor`, `strokeWidth`, `alignment`, and `textVAlign` are optional but
should be authored when they affect the visible result. `fontFamily` is
optional; the renderer falls back to common system fonts when it is omitted.

```json
{
  "type": "text",
  "name": "play_label",
  "position": { "x": 0, "y": 0 },
  "size": { "width": 220, "height": 84 },
  "text": "PLAY",
  "fontSize": 72,
  "color": "#FFFFFF",
  "strokeColor": "#111111",
  "strokeWidth": 5,
  "lineHeight": 56,
  "alignment": "center",
  "textVAlign": "middle"
}
```

`fontFamily` is searched relative to the structure file, assets directory,
design directory, and current workspace. `alignment` and `textVAlign` align
text within the text box; `align` and `vAlign` still position the text box
within its parent.

When `fontSize` is missing after the text box is authored, run:

```bash
py -B <skill>/scripts/suggest_text_metrics.py --structure out/<design-stem>/ui_structure.json --write --report out/<design-stem>/text_metrics_report.json
```
