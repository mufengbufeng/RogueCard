## Context

`CardDragController` already separates two important concepts:

- `ActiveHandIndex`: the stable index in `IHandContext.Hand`, used for `UseCard` and target-mode lookup.
- `ActiveVisualIndex`: the current index in `HandFanView._cardItems`, used for UI layout and reorder.

During `Dragging.InsertSlot`, `RecomputeFanLayout(activeIdx, InsertSlot, insertSlot)` skips the dragged card and places the insert-slot placeholder at the candidate slot. On release, the current implementation calls `ReorderCardItem(from, to)` and then `ExitDragging()`. `ExitDragging()` restores visibility and removes transient elements, but it does not recompute the final N-card fan layout after `_cardItems` has changed order. The dragged card therefore becomes visible at its old inline `left/top/translate/rotate`, which can be the same slot now occupied by its neighbor.

There is also a capture-order hazard: `ReleasePointerCapture(visualIdx, pointerId)` currently runs after `ReorderCardItem`. Because `ReleasePointerCapture` indexes through `_cardItems`, releasing by the old index after reorder can address the wrong card.

## Goals / Non-Goals

**Goals:**

- Treat releasing in `Dragging.InsertSlot` as an explicit commit operation with a readable method boundary.
- Guarantee that every visible card receives exactly one final N-card `FanSlotAssignment` after reorder, including `from == to` and clamped insert-slot cases.
- Release pointer capture from the dragged card before `_cardItems` order changes, or otherwise use a captured-card identity that cannot be invalidated by reorder.
- Keep UI-only reorder semantics: changing visual order SHALL NOT mutate `IHandContext.Hand`.
- Preserve existing drag/drop behavior outside InsertSlot: click preview, detached rebound, AutoTarget play, SingleManual target selection, and capture-out cleanup.
- Cover the regression with EditMode tests at the `CardDragController` / `IDragSurface` boundary.

**Non-Goals:**

- Do not change `BattlePanel.uxml`, `CardItem.uxml`, or USS structure.
- Do not add persistent hand reordering to the battle model or card system.
- Do not change fan-layout formulas, card spacing, drag threshold, or rebound timing.
- Do not introduce a broad state-pattern rewrite unless implementation shows the current method-cluster design is still unclear after extracting the commit path.

## Decisions

### Decision 1: Extract an explicit InsertSlot commit path

Add a focused controller method, conceptually:

```csharp
private void CommitInsertSlotRelease(int pointerId)
```

The method owns the entire successful reorder release sequence:

1. Snapshot and normalize `from = _activeVisualIndex` and `to = Clamp(_insertSlotIndex, 0, n - 1)`.
2. Release pointer capture while `from` still points at the dragged card.
3. Reorder the surface from `from` to `to`.
4. Set `_activeVisualIndex` to the final visual index for internal consistency until reset.
5. Apply final layout with `RecomputeFanLayout(activeIdx: -1, Detached, -1)`.
6. Call `ExitDragging()` for ghost / insert-slot / opacity / picking / transition cleanup.
7. Reset state to `Idle`.

Why this over a one-line recompute after reorder: the bug is caused by an incomplete state transition, not by the layout formula. A named commit method makes ordering constraints visible and gives tests one behavior-sized surface to protect.

### Decision 2: Keep layout responsibility in CardDragController

`IDragSurface.ReorderCardItem(from, to)` will continue to only reorder the visual list and sibling order. `CardDragController` remains responsible for applying the resulting layout through `RecomputeFanLayout`.

Why this over moving layout into `HandFanView.ReorderCardItem`: keeping reorder and layout separate preserves the existing `IDragSurface` contract, makes tests assert the full call sequence, and avoids hiding state-machine decisions inside the view adapter.

### Decision 3: Normalize indices before side effects

The controller will clamp `insertSlot` before calling the surface, rather than relying only on `HandFanView.ReorderCardItem` to clamp. This gives `CardDragController` a known final visual index and avoids mismatches between `_activeVisualIndex` and the actual list order.

If the surface still clamps defensively, both layers should use the same `[0, n - 1]` range and the controller-side value remains the expected final index.

### Decision 4: Preserve from==to as a real commit

When a card is dragged within `hand-fan` and released back to its original slot, the controller still must run the final layout and cleanup sequence. `ReorderCardItem(from, to)` may no-op, but the dragged card was hidden and skipped during InsertSlot layout, so final N-card layout is still required.

### Decision 5: Test through MockDragSurface call records

The primary regression is observable without a live UI Toolkit panel:

- `ReorderCardItem(from, to)` occurs.
- `ApplyFanTransform` is called after reorder for every card index in the final card count.
- The final slots assigned after reorder are unique and match `FanLayoutCalc.ComputeSlot(i, n, ...)`.
- pointer release occurs before reorder for the dragged visual index.
- transient state returns to `Idle`, ghost and insert slot are destroyed, and opacity / picking / transition cleanup still runs.

`MockDragSurface` may need small read helpers to group call order and final per-card slot assignments. This is test support, not production behavior.

## Risks / Trade-offs

- [Risk] Pointer capture may still be wrong if future surface implementations index through a reordered list.
  -> Mitigation: release capture before reorder in the controller, and add a call-order regression test.

- [Risk] Applying final layout before cleanup may briefly animate from an unintended transition baseline.
  -> Mitigation: InsertSlot drag keeps transition duration at `0f`; final layout should be written before `ExitDragging()` clears inline transition duration, so positions settle immediately and visibility restoration reveals the correct slot.

- [Risk] A future refactor could move final layout into `HandFanView` and duplicate behavior.
  -> Mitigation: keep the spec requirement at the state-machine level and verify through `IDragSurface` calls.

- [Risk] Live UI inspection cannot currently report per-card `left/top/worldBound`.
  -> Mitigation: cover the deterministic state-machine behavior with EditMode tests, then do a short Unity play-mode manual verification for the visual result.

## Migration Plan

No data or asset migration is required. The change is runtime UI behavior only.

Implementation should proceed test-first:

1. Add failing EditMode tests for InsertSlot release final layout and pointer-release ordering.
2. Refactor `CardDragController` to extract the commit path and satisfy the tests.
3. Run the focused EditMode tests.
4. Use Unity play mode to manually drag a card left and right across neighboring cards and confirm no visual overlap after release.

## Open Questions

None. The desired behavior is a UI-only visual reorder that always ends in a consistent final fan layout.
