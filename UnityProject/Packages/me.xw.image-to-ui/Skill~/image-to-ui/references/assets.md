# Asset Inventory Reference

Use this reference when selecting sliced sprites for `ui_structure.json`.

## Outputs

`inventory_assets.py` writes:

- `assets_inventory.json`: exact relative paths, dimensions, alpha bounds,
  duplicate basename data, usage hints, Unity asset GUIDs, Unity
  `spriteBorder` metadata, and `sub_sprites` entries when the `.meta` file
  defines them.
- `assets_contact_sheet.png`: all assets.
- `assets_contact_sheet_usage_*.png`: grouped sheets. Prefer these first to
  reduce visual context.

Run inventory against the prompt's existing sliced Sprite directory. Do not
copy or move sliced assets into the task folder; `unity.spriteRootFolder` in
`ui_structure.json` must point back to that original Unity folder.

## Selection Rules

1. Use grouped sheets first:
   - `assets_contact_sheet_usage_button_or_panel.png` for panels, frames,
     buttons, bubbles, bars, and stretchable backgrounds.
   - `assets_contact_sheet_usage_icon.png` for icons and item art.
   - Fall back to the full sheet only when grouped sheets do not contain the
     needed sprite.
2. If `duplicate_basenames` is non-empty, write `asset` as the relative path
   from `assets_inventory.json`, not as the basename.
3. When an inventory item has `guid`, write it to the image element as
   `assetGuid`. Keep `asset` too because it is human-readable and can
   disambiguate sub-sprites that share a Unity asset GUID.
4. When an inventory item lists `sub_sprites`, copy the chosen entry's
   `spriteName` into the JSON alongside `asset` / `assetGuid`.
   The sub-sprite record also carries `fileId`, `rect`, and optional
   `spriteBorder` metadata for inspection.
5. Prefer `"nineSlice": "meta"` or `true` when the inventory entry is
   `button_or_panel` and `has_meta_border` is true.
6. Do not use nine-slice for `icon`, `portrait`, or item art unless the design
   clearly stretches that asset.
7. If the prompt's sliced asset directory is an absolute path inside a Unity
   project, normalize it to `Assets/...` or `Packages/...` before writing
   `unity.spriteRootFolder`.
