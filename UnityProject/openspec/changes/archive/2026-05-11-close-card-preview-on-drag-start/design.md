## Context

`BattlePanelView` delegates hand card interaction to `HandFanView`. `HandFanView` owns both `CardDragController` and `CardPreviewController`, while `CardDragController` only knows about drag state and UI side effects through `IDragSurface`.

Active previews already close through `CardPreviewController.ExitPreview()` for explicit toggle, disposal, hand refresh, and non-card battle panel clicks. The missing case is the transition from a pressed card to a committed drag after the pointer crosses `HandFanLayoutOptions.DragThreshold`.

## Goals / Non-Goals

**Goals:**

- Close an active card preview exactly when a hand card interaction becomes a drag.
- Preserve threshold-based click behavior so small pointer movement still triggers existing preview toggle behavior.
- Keep preview ownership in `HandFanView` / `CardPreviewController`, not in the drag state machine.
- Cover the behavior with a focused EditMode test.

**Non-Goals:**

- Change drag threshold math, drag modes, ghost creation, reorder, drop-zone play, target selection, or rebound behavior.
- Add new public battle UI APIs or new dependencies.
- Change card preview positioning, scaling, or click/backdrop dismissal behavior.

## Decisions

1. **Close preview from `HandFanView` after observing drag state transition.**

   `HandFanView.OnCardPointerMove` will compare `CardDragController.State` before and after forwarding the pointer move. If the previous state was not `Dragging` and the new state is `Dragging`, it will call `_previewController.ExitPreview()`.

   Rationale: `HandFanView` already coordinates drag and preview controllers and owns the preview controller reference. This keeps `CardDragController` independent from preview concerns.

   Alternative considered: add an `IDragHostCallbacks.DragStarted` callback. This would make drag start explicit but expands the drag callback contract for a single local coordination rule.

2. **Do not close preview on `PointerDown`.**

   The preview remains active while the gesture is still a possible click. If the pointer is released within the drag threshold, the existing `CardClicked -> TogglePreview` path keeps same-card close and different-card switch semantics intact.

   Alternative considered: close preview immediately on card pointer down. This would make a normal click close before `TogglePreview` runs, causing same-card and different-card clicks to lose the current preview semantics.

## Risks / Trade-offs

- [Risk] EditMode UI Toolkit event dispatch may not reliably simulate a full pointer sequence without a live panel. -> Mitigation: use the existing reflection-based HandFanView preview tests style to exercise the helper/method boundary that owns the behavior, and keep production pointer dispatch unchanged.
- [Risk] Closing after `EnterDragging` means the preview clone may exist for the same pointer move that creates the ghost. -> Mitigation: both actions occur synchronously in the same `OnCardPointerMove` call before the next frame is rendered, so users should not see an intermediate overlap frame.
