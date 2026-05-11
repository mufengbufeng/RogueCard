## Context

The battle UI already separates card preview behavior into `CardPreviewController`, with `HandFanView` routing card click callbacks into `TogglePreview`. Existing behavior covers same-card close and other-card switch. The missing behavior is a backdrop dismissal path for clicks that do not originate from a hand card.

The current `preview-layer` and preview clone use `PickingMode.Ignore`, so the enlarged clone does not receive pointer events. This is a useful constraint: dismissal should be based on the actual clicked UI element beneath the preview, not the clone itself.

`TargetSelector` already uses a root-level `PointerDownEvent` callback registered with `TrickleDown.TrickleDown` to catch outside clicks before child callbacks can stop propagation. Card preview dismissal should use the same event phase, but with card-specific filtering so card clicks continue to drive `TogglePreview`.

## Goals / Non-Goals

**Goals:**

- Allow an active card preview to close when the player clicks any non-card area in the battle panel.
- Preserve the current card click semantics: same card closes, different card switches.
- Prevent a backdrop dismissal click from also activating the clicked non-card control in the same gesture.
- Keep preview dismissal isolated to UI interaction code.

**Non-Goals:**

- Do not change card play, drag, reorder, target selection, or battle model behavior.
- Do not make preview clones pickable or interactive.
- Do not redesign the hand fan layout or card visual style.

## Decisions

### 1. Register backdrop dismissal at the battle panel root

`HandFanView` should receive a root `VisualElement` for preview dismissal, likely the `BattlePanel` content already owned by `BattlePanelView`. It should register a `PointerDownEvent` callback on that root using `TrickleDown.TrickleDown`.

Rationale: registering on `hand-fan` only would miss clicks on monsters, drop-zone, end-turn button, and other panel areas. Registering on root matches the existing `TargetSelector` pattern and catches clicks before child handlers call `StopPropagation`.

Alternative considered: register on `preview-layer`. This does not work with the current design because `preview-layer` is intentionally `PickingMode.Ignore`.

### 2. Filter hand card clicks before dismissing

The root handler must return without dismissing when the event target is the same as, or a descendant of, any current card root in `_cardItems`.

Rationale: card clicks must continue through `CardDragController.OnPointerUp` and `DragHostCallbacksImpl.CardClicked`, where `TogglePreview` already handles same-card close and other-card switch. If the root handler closes preview first, clicking the same card would reopen it on pointer up.

Alternative considered: dismiss on root `PointerUp` after card logic. This is harder to reason about because pointer capture and drag cancellation already use pointer up, and it risks racing with card click callbacks.

### 3. Consume non-card dismissal clicks

When preview is active and the root handler dismisses it, it should call `StopPropagation` on the pointer event.

Rationale: a player clicking outside to close preview should not accidentally trigger end-turn or other non-card actions under the pointer in the same gesture. A second click can then operate the target control intentionally.

Alternative considered: allow the click to continue after dismissal. This is faster for expert users but has a higher misclick cost in battle UI.

### 4. Keep controller logic small and testable

`CardPreviewController` may expose its existing `IsPreviewing` and `ExitPreview` for the root handler. It does not need to know about root elements, card collections, or event propagation.

Rationale: `CardPreviewController` should remain a preview state/UI side-effect controller. Hit testing belongs in `HandFanView`, which owns card item roots and battle panel wiring.

## Risks / Trade-offs

- [Risk] Root-level trickle-down dismissal could conflict with target selection's own root-level outside-click handler. → Mitigation: dismissal should only act when `CardPreviewController.IsPreviewing`; preview is exited on drag/hand refresh, so target selection after a drag should normally have no active preview.
- [Risk] Consuming non-card clicks changes the first-click behavior for controls while preview is active. → Mitigation: this is intentional and should be covered by a manual verification task because it protects against accidental battle actions.
- [Risk] `HandFanView` constructor signature changes. → Mitigation: only `BattlePanelView` should construct it currently, so the blast radius is small.
