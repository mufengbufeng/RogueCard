## Why

`GameView` currently maps detailed `BattlePhase` values to another `BattlePhase` value to decide which UI Region content to show. This makes the routing logic read incorrectly, especially `MonsterTurn` and `Check` mapping to `PlayerTurn` only because they share the `BattlePanel`.

This change separates gameplay phase semantics from UI Region routing semantics so the code clearly expresses whether the game screen should show battle content, reward content, or no routed content.

## What Changes

- Replace `GameView`'s `BattlePhase`-based active Region tracking with a UI-specific Region kind.
- Replace `MapPhaseToRegion(BattlePhase) -> BattlePhase` with a mapping from `BattlePhase` to explicit UI Region kind.
- Keep all battle phases (`Prepare`, `PlayerTurn`, `MonsterTurn`, `Check`) routed to `BattlePanel`.
- Keep `Reward` routed to `RewardPanel`.
- Dispose the active `BattlePanelView` when leaving battle content for reward content.
- Unsubscribe `GameView` from `ViewModel.Phase.Changed` during disposal.
- Optionally guard async Region switching so stale `ShowAsync` continuations cannot bind outdated content.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `ui-region`: Clarify that screen-level Region routing should use UI-specific route state instead of reusing gameplay/domain enums as Region identifiers.

## Impact

- Affected code: `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameView.cs`.
- No changes to `BattlePhase`, `BattleSystem`, `BattlePanelView`, `Region`, or UXML assets are intended.
- No new runtime dependencies.
- Observable UI behavior should remain unchanged: battle phases show `BattlePanel`, reward phase shows `RewardPanel`.
