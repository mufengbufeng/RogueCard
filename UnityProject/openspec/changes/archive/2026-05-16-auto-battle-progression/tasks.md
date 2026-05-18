## 1. BattleSystem 自动结束玩家回合

- [x] 1.1 在 `BattleSystem` 中新增私有方法 `TryAutoEndPlayerTurnIfCleared()`：仅当 `_model.Phase == BattlePhase.PlayerTurn` 且 `_model.Monsters.All(m => m.Hp <= 0)` 才生效；命中条件时调用 `SetPhase(BattlePhase.Check)`。
- [x] 1.2 在 `BattleSystem.Init` 中订阅本地 `CardPlayedEvent`，回调里调用 `TryAutoEndPlayerTurnIfCleared()`；保存 handler 以便 `Dispose` 时取消订阅。
- [x] 1.3 在 `BattleSystem.Dispose` 中通过保存的 handler 调用 `_events.GetChannel<CardPlayedEvent>().Unsubscribe(...)`，保证多次 Dispose 安全。
- [x] 1.4 评估 Buff Tick 路径是否需要额外触发点：在 `TickBuffs(EffectTriggerTiming.EnemyTurnStart/EnemyTurnEnd)` 之后尾部不直接触发自动结束（清场判定只走 `CardPlayedEvent`），避免影响 `MonsterTurn` 内部时序；如有 DoT 自杀场景的回归需求，再单独评估。

## 2. WaveSystem 关卡完成事件契约清理

- [x] 2.1 在 `WaveSystem.AdvanceToNextWave` 中删除 `globalHub.StartLevelRequestedEvent.Publish(...)` 与对应 `globalHub`/`tables` 取值；只保留本地 `LevelCompleteEvent` 的发布与 `_model.SetLevelComplete(true)` 调用。
- [x] 2.2 复查 `Assets/GameScripts/HotFix/GameLogic` 下是否还有其它代码把 `StartLevelRequestedEvent` 当作"关卡完成"使用，必要时一并更正。

## 3. GameProcedure 关卡完成自动返回主菜单

- [x] 3.1 在 `GameProcedure` 中新增私有字段 `private ProcedureOwner _procedureOwner;` 并在 `OnEnter` 中保存 `procedureOwner`，供回调使用。
- [x] 3.2 在 `EnterAsync` 创建 `_localEventBus` 之后立即订阅 `LevelCompleteEvent`：`_localEventBus.GetChannel<LevelCompleteEvent>().Subscribe(OnLevelComplete)`。
- [x] 3.3 实现 `private void OnLevelComplete(LevelCompleteEvent evt)`：调用 `ChangeState<MainMenuProcedure>(_procedureOwner)`，注释说明语义。
- [x] 3.4 在 `Cleanup` 中新增 `_localEventBus.GetChannel<LevelCompleteEvent>().Unsubscribe(OnLevelComplete)`，并清空 `_procedureOwner` 字段。
- [x] 3.5 保证 `Cleanup` 被多次调用（`OnLeave` + `OnDestroy`）时不重复取消订阅出错（参考现有 `CardPlayFailedEvent` 取订模式做对齐）。

## 4. EditMode 测试

- [x] 4.1 在 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/` 下新增 `BattleSystemAutoEndTurnTests.cs`，覆盖：玩家回合内出牌清空怪物后自动进 `Check`、未清空时不切相位、AOE 多目标只触发一次切相位。
- [x] 4.2 在 `BattleSystemBuffTickTests.cs` 中补一条断言：自动结束回合路径下不会执行 `EnemyTurnStart` / `EnemyTurnEnd` 的 Buff Tick。
- [x] 4.3 新增或更新 `WaveSystem` 相关 EditMode 测试，断言关卡完成时 `LevelCompleteEvent` 在本地总线发布、`StartLevelRequestedEvent` **不**在全局总线发布。
- [x] 4.4 新增 `GameProcedure` 行为契约测试（或在现有适配测试中追加），覆盖：订阅 `LevelCompleteEvent` 后收到事件触发流程切换、Cleanup 后再次发布不会回调。若直接测试 `GameProcedure` 困难，可用一个 fake `ProcedureOwner` 与本地总线组合验证回调路径。

## 5. 验证与编译

- [x] 5.1 运行 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` 触发项目级编译检查；若失败回退到 `dotnet build UnityProject.slnx --no-restore`。
- [x] 5.2 通过 Unity Test Runner（EditMode）运行所有新增 / 修改测试，全部通过。
- [x] 5.3 在 Unity 编辑器中手动跑一次：进入战斗 → 一张大牌清空当前批次 → 验证自动进入下一批次或胜利结算；走完所有波次 → 验证自动回到 `MainView`。
- [x] 5.4 运行 `openspec validate auto-battle-progression --strict` 通过校验。
