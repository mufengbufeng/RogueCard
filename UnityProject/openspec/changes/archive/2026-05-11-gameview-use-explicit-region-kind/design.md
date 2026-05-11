## Context

`GameView` is the top-level in-game screen. It owns a `Region` slot and reacts to `GameViewModel.Phase` changes to load either `BattlePanel` or `RewardPanel`.

The current implementation uses `BattlePhase` for both gameplay phase and active Region tracking. `MapPhaseToRegion` returns `BattlePhase.PlayerTurn` for `Prepare`, `MonsterTurn`, and `Check` because those phases should display `BattlePanel`. This works mechanically but makes the code read as if non-player phases are being converted into `PlayerTurn`.

`BattlePanelView` already owns battle UI composition and target selection. `BattleSystem` owns gameplay phase progression. This change should only clean up the screen-level route naming and lifecycle handling in `GameView`.

## Goals / Non-Goals

**Goals:**

- Make `GameView` route state explicit and UI-specific.
- Preserve existing visual behavior for all `BattlePhase` values.
- Dispose `BattlePanelView` when leaving battle Region content.
- Unsubscribe `GameView` from phase changes during disposal.
- Add a small async switch guard if needed so stale Region loads cannot bind outdated content.

**Non-Goals:**

- Do not change `BattlePhase`.
- Do not change `BattleSystem` phase progression.
- Do not introduce `FsmManager` for `GameView` routing.
- Do not change `Region` API.
- Do not change `BattlePanelView`, `RewardPanel`, or UXML structure unless required by tests.

## Decisions

### Decision 1: Introduce a GameView-local UI route enum

Use a private enum in `GameView`:

```csharp
private enum GameRegionKind
{
    None,
    Battle,
    Reward
}
```

`_activeRegionPhase` becomes `_activeRegion` and stores `GameRegionKind`. This keeps the route concept local to the screen and avoids expanding public API for a two-panel routing decision.

Alternative considered: create a public/shared enum. Rejected because no other system needs this route state, and making it public would imply a broader contract than the current change needs.

### Decision 2: Rename mapping semantics from phase mapping to route selection

Replace the current `MapPhaseToRegion(BattlePhase) -> BattlePhase` with a method that returns `GameRegionKind`. The mapping remains:

- `Prepare`, `PlayerTurn`, `MonsterTurn`, `Check` -> `Battle`
- `Reward` -> `Reward`
- everything else -> `None`

The important change is semantic: `MonsterTurn` no longer appears to become `PlayerTurn`; it becomes `Battle` UI content.

Alternative considered: remove the mapping method and inline the switch in `OnPhaseChanged`. Rejected because keeping the mapping isolated makes the route contract easy to read and test.

### Decision 3: Keep routing simple rather than using FsmManager

`FsmManager` is appropriate for flows with meaningful enter/update/leave state behavior. `GameView` routing only chooses one of two Region templates based on phase. A local enum and switch are smaller and easier to audit.

### Decision 4: Dispose panel ownership at Region boundary

When routing to `Reward`, `GameView` should dispose any active `BattlePanelView` before or during the switch. This prevents the old battle panel coordinator from holding UI element references and event subscriptions after battle content is no longer active.

`OnDispose` should also unsubscribe from `ViewModel.Phase.Changed`, dispose `BattlePanelView`, dispose `PlayerStatusView`, and clear the Region.

### Decision 5: Guard async Region switching with a version token

`OnPhaseChanged` is `async void` because it is an event callback. A monotonic integer version can be incremented on each route attempt and checked after `await _mainRegion.ShowAsync(...)`. If a newer route has started, the old continuation returns without binding content.

This keeps the existing event shape while reducing stale continuation risk.

## Risks / Trade-offs

- **Risk:** The new route enum may look like extra ceremony for two panels.
  **Mitigation:** Keep it private to `GameView` and only use it to replace the misleading domain enum reuse.

- **Risk:** Disposing `BattlePanelView` before `RewardPanel` loads could briefly remove battle handlers if the async load fails.
  **Mitigation:** This is acceptable because `Reward` means battle content is no longer the active UI route; failures are already logged by `GameView`.

- **Risk:** Async version guards can hide a stale route load without visible feedback.
  **Mitigation:** The current desired behavior is to ignore stale loads; only the latest phase should bind content.
