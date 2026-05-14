## 1. Test Coverage

- [x] 1.1 Add a focused EditMode test showing an active preview remains visible after hand card pointer down while movement has not crossed `DragThreshold`.
- [x] 1.2 Add a focused EditMode test showing an active preview is removed when `HandFanView` observes `CardDragController.State` transition into `Dragging` on pointer move.
- [x] 1.3 Add or preserve coverage showing threshold-limited card clicks still route through `CardClicked -> TogglePreview` semantics instead of drag-start dismissal.

## 2. Implementation

- [x] 2.1 Update `HandFanView.OnCardPointerMove` to compare drag state before and after forwarding the pointer move to `CardDragController`.
- [x] 2.2 Call `_previewController.ExitPreview()` only when the state changes from non-`Dragging` to `Dragging`.
- [x] 2.3 Keep `CardDragController`, `IDragHostCallbacks`, `BattlePanelView`, drag ghost behavior, and existing preview toggle/backdrop dismissal behavior unchanged unless tests reveal a necessary local adjustment.

## 3. Verification

- [x] 3.1 Run the targeted Unity EditMode tests for `GameLogic.Tests` hand fan/card preview behavior via Unity Test Runner or the project test command.
- [x] 3.2 Run `openspec status --change "close-card-preview-on-drag-start"` and confirm the change is apply-ready with tasks tracked.
