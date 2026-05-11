## 1. GameView Route Semantics

- [x] 1.1 Add a private `GameRegionKind` enum to `GameView` with `None`, `Battle`, and `Reward`.
- [x] 1.2 Replace `_activeRegionPhase` with `_activeRegion` using `GameRegionKind`.
- [x] 1.3 Replace `MapPhaseToRegion(BattlePhase) -> BattlePhase` with `MapPhaseToRegion(BattlePhase) -> GameRegionKind`.
- [x] 1.4 Ensure `Prepare`, `PlayerTurn`, `MonsterTurn`, and `Check` map to `GameRegionKind.Battle`.
- [x] 1.5 Ensure `Reward` maps to `GameRegionKind.Reward` and all other phases map to `GameRegionKind.None`.

## 2. Region Switching Lifecycle

- [x] 2.1 Update `OnPhaseChanged` to switch on `GameRegionKind` instead of `BattlePhase`.
- [x] 2.2 Add a private helper to dispose and null `_battlePanelView`.
- [x] 2.3 Dispose active `BattlePanelView` before binding `RewardPanel`.
- [x] 2.4 Keep battle Region binding behavior unchanged when routing to `Battle`.
- [x] 2.5 Unsubscribe `ViewModel.Phase.Changed -= OnPhaseChanged` in `OnDispose`.

## 3. Async Switch Guard

- [x] 3.1 Add a monotonic Region switch version field to `GameView`.
- [x] 3.2 Increment the version at the start of each route switch.
- [x] 3.3 After each awaited `ShowAsync`, return without binding content if a newer route switch has started.

## 4. Verification

- [x] 4.1 Compile the project and fix any errors caused by the route enum change.
- [x] 4.2 Verify battle phases still display `BattlePanel`.
- [x] 4.3 Verify `Reward` displays `RewardPanel`.
- [x] 4.4 Verify leaving battle for reward disposes `BattlePanelView`.
- [x] 4.5 Verify disposing `GameView` releases the phase subscription.
- [x] 4.6 Run `openspec validate gameview-use-explicit-region-kind`.
