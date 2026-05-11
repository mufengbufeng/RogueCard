## 1. Regression Tests

- [x] 1.1 Extend `MockDragSurface` test support to record relative call order for `ReleasePointer`, `ReorderCardItem`, `ApplyFanTransform`, `DestroyInsertSlot`, `DestroyGhost`, and cleanup calls without changing production interfaces.
- [x] 1.2 Add a failing EditMode test proving `Dragging.InsertSlot` release from visual index 2 to an earlier slot applies final N-card fan layout to every card after reorder.
- [x] 1.3 Add a failing EditMode test proving `Dragging.InsertSlot` release with `insertSlotIdx == activeVisualIdx` still applies final N-card fan layout and returns to `Idle`.
- [x] 1.4 Add a failing EditMode test proving pointer capture for the dragged pre-reorder visual index is released before `ReorderCardItem`.

## 2. Drag Commit Refactor

- [x] 2.1 Extract a readable InsertSlot release commit method in `CardDragController` that owns pointer release, index normalization, visual reorder, final layout, transient cleanup, and state reset.
- [x] 2.2 Normalize and clamp the target insert slot in the controller before calling `IDragSurface.ReorderCardItem`, and update `_activeVisualIndex` consistently until reset.
- [x] 2.3 Apply final N-card layout after reorder with the existing `RecomputeFanLayout(activeIdx: -1, DragMode.Detached, insertSlot: -1)` path before restoring the dragged card's visible state.
- [x] 2.4 Preserve existing AutoTarget drop-zone, SingleManual target selection, detached rebound, click preview, and capture-out behavior while reducing duplicated cleanup logic where practical.

## 3. Verification

- [x] 3.1 Run the focused Unity EditMode tests for `GameLogic.Tests.CardDragControllerTests` and confirm the new regression tests pass.
- [x] 3.2 Run the broader affected EditMode UI drag/layout tests covering `CardDragControllerTests`, `FanLayoutCalcTests`, and `CardPreviewControllerTests`.
- [x] 3.3 In Unity Play mode, manually drag cards left and right across adjacent hand slots and confirm the released card never overlaps a neighbor after the reorder completes.
