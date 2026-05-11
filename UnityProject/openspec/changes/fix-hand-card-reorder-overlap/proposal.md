## Why

Dragging a hand card into another hand slot can leave the moved card visually overlapping its neighbor after release. The drag state machine already distinguishes hand index from visual index, but the InsertSlot commit path does not establish a final, consistent N-card layout after reordering.

## What Changes

- Make hand-card reorder commit an explicit, robust state-machine operation rather than a branch-local cleanup.
- Ensure releasing in `Dragging.InsertSlot` atomically commits visual reorder, applies the final N-card fan layout, removes the insert slot and ghost, restores opacity / picking / transition state, releases pointer capture against the correct element, and returns to `Idle`.
- Preserve existing behavior for click preview, detached rebound, drop-zone play, SingleManual target selection, hand index callback semantics, and UI-only visual reorder.
- Add focused EditMode regression coverage for successful reorder, no-overlap final slot assignment, and pointer-capture release ordering.
- No UXML structure change is planned.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `gameview-card-drag-state-machine`: tighten InsertSlot release semantics so completed hand reorders must leave every visible card in a unique final fan slot and all transient drag state cleared.

## Impact

- Affected runtime code:
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/CardDragController.cs`
  - potentially `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/HandFanView.cs` / `IDragSurface.cs` if the final design benefits from a clearer reorder commit surface
- Affected tests:
  - `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/Drag/CardDragControllerTests.cs`
  - `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/Drag/MockDragSurface.cs`
- No changes to card data, battle rules, target selection rules, UXML hierarchy, or persisted game state.
