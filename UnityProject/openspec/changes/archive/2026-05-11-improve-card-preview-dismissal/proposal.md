## Why

Card preview currently closes only when the player clicks the original card again. This makes the preview feel sticky: players expect clicking outside the card to dismiss it, and clicking another card to replace the current preview directly.

## What Changes

- Extend card preview dismissal so a preview can be closed by clicking any non-card area in the battle panel.
- Preserve the existing card click behavior: clicking the same card closes preview, and clicking a different card switches to that card's preview.
- Consume non-card dismissal clicks while preview is active so closing preview does not accidentally trigger other battle controls.
- Keep preview clones non-pickable so the enlarged preview itself never blocks card or backdrop input.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `gameview-card-preview`: add backdrop dismissal behavior for active card previews, while preserving same-card close and other-card switch semantics.

## Impact

- Affected runtime code: `BattlePanelView`, `HandFanView`, and possibly `CardPreviewController` public surface for explicit dismissal state.
- Affected tests: EditMode tests around card preview toggle/dismissal and hand fan pointer routing.
- No data table, asset format, or gameplay model changes.
