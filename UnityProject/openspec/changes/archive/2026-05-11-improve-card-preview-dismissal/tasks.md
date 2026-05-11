## 1. Test-First Coverage

- [x] 1.1 Add or update EditMode coverage proving an active preview exits when the hand fan receives a non-card battle-panel pointer down.
- [x] 1.2 Add or update EditMode coverage proving non-card dismissal consumes the pointer event so the same gesture cannot activate a battle control.
- [x] 1.3 Add or update EditMode coverage proving card-root pointer targets are excluded from non-card dismissal, preserving same-card close and different-card switch through `TogglePreview`.

## 2. Runtime Wiring

- [x] 2.1 Pass the battle panel root `VisualElement` from `BattlePanelView` into `HandFanView` so preview dismissal can observe clicks outside `hand-fan`.
- [x] 2.2 Register a `PointerDownEvent` callback on the battle panel root with `TrickleDown.TrickleDown` during `HandFanView` construction, and unregister it during `Dispose`.
- [x] 2.3 Implement the root pointer handler so it returns when no preview is active, returns when the target is any hand card or card descendant, and otherwise calls `ExitPreview()` plus `StopPropagation()`.
- [x] 2.4 Keep preview clones and `preview-layer` non-pickable; do not add pointer handlers to preview clones.

## 3. Verification

- [x] 3.1 Run the focused Unity EditMode tests for `CardPreviewControllerTests` and the hand fan preview dismissal coverage.
- [x] 3.2 Run the broader affected UI EditMode tests covering card drag, card preview, target selection, and turn control.
- [x] 3.3 Manually verify in Play mode: click card A to preview, click empty battle panel area to close, click card A again to preview, click card B to switch, and click end-turn while preview is open to confirm the first click only dismisses preview.
