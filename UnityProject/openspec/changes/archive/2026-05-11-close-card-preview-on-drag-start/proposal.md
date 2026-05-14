## Why

When a card preview is active, starting to drag a hand card leaves the enlarged preview visible while the drag ghost and hand fan layout move underneath it. This creates overlapping feedback during a committed drag gesture and makes it unclear which card interaction is currently active.

## What Changes

- Close any active card preview when a hand card pointer move crosses the drag threshold and the hand interaction enters dragging.
- Preserve existing click preview behavior: clicking the same card closes preview, clicking a different card switches preview, and non-card battle panel clicks dismiss preview.
- Keep the drag state machine behavior unchanged for click detection, drag threshold, drop-zone play, reorder, cancel, and rebound paths.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `gameview-card-preview`: add dismissal behavior for active card previews when a hand card drag starts.

## Impact

- Affected runtime code: `HandFanView` integration between `CardDragController` state changes and `CardPreviewController.ExitPreview()`.
- Affected tests: EditMode tests around hand fan preview dismissal when drag starts.
- No data table, asset format, gameplay model, or public battle command changes.
