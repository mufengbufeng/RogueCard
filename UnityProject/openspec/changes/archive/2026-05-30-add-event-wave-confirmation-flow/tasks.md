## 1. Model and ViewModel State

- [x] 1.1 Add current wave display state and awaiting-confirmation state to `GameModel` / `IGameModelData`.
- [x] 1.2 Mirror the new state in `GameViewModel` with `ReactiveProperty` values and include it in `SyncAll()` / `OnModelPropertyChanged()`.
- [x] 1.3 Add focused EditMode coverage for setting, clearing, and synchronizing event wave display state.

## 2. Wave Runtime Flow

- [x] 2.1 Update `WaveSystem.StartCurrentWave()` so Chest / Shop waves enter a waiting confirmation state instead of auto-advancing.
- [x] 2.2 Add a public confirmation method on `WaveSystem` that clears waiting state and advances to the next wave only when confirmation is valid.
- [x] 2.3 Preserve Battle wave behavior and existing `BattleEndedEvent` victory progression.
- [x] 2.4 Add EditMode tests proving non-battle waves wait, confirm advances, and battle waves still start battle normally.

## 3. Procedure Command Routing

- [x] 3.1 Change `GameProcedure` reward/confirm command handling to route event wave confirmation to `WaveSystem`.
- [x] 3.2 Change `LevelCompleteEvent` handling so it records completion/waits for confirmation rather than immediately switching procedure.
- [x] 3.3 Route confirm to `MainMenuProcedure` only when the level is complete and no event wave is awaiting confirmation.
- [x] 3.4 Update lifecycle tests for cleanup, event unsubscription, and confirm-after-completion behavior.

## 4. UI Binding

- [x] 4.1 Bind RewardPanel title, description, and continue button text from `GameViewModel` wave display state.
- [x] 4.2 Drive BattlePanel / RewardPanel visibility from battle phase, awaiting-confirmation, and level-complete state.
- [x] 4.3 Update prefab/reference collector bindings if new text references are required.
- [x] 4.4 Add/adjust UI tests for event wave display and confirm command dispatch.

## 5. Validation

- [x] 5.1 Run `openspec validate add-event-wave-confirmation-flow --strict`.
- [x] 5.2 Run the relevant Unity EditMode test subset for wave runtime, GameProcedure command flow, and GameView binding.
