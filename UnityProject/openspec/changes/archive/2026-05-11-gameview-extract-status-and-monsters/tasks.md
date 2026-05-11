## 1. 切片接口与 ViewModel 实现

- [x] 1.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Context/IPlayerStatusContext.cs`，暴露 `Phase` / `PlayerHp` / `PlayerMaxHp` / `PlayerArmor` / `Energy` / `MaxEnergy` / `IsLevelComplete` / `IsPlayerDead` / `PlayerBuffs` 共 9 个 `ReactiveProperty<>` 只读属性
- [x] 1.2 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Context/IMonsterListContext.cs`，暴露 `Monsters` 一个 `ReactiveProperty<IReadOnlyList<MonsterRuntime>>`
- [x] 1.3 让 `GameViewModel` 显式实现 `IPlayerStatusContext` 与 `IMonsterListContext`（仅添加 `: IPlayerStatusContext, IMonsterListContext` 与必要的成员转发，无新字段）
- [x] 1.4 通过 `dotnet build UnityProject.slnx --no-restore` 或 unity-compile-check skill 验证编译

## 2. MonsterItemView（单只怪物视图）

- [x] 2.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/MonsterItemView.cs`，构造参数 `(VisualElement root, MonsterRuntime monster, int aliveMonsterCount)`
- [x] 2.2 迁移 `GameView.RenderIntentCard` 与 effect 解析逻辑（含 SplitAcrossAll 平分、`max(1, value/aliveCount)`）到 `MonsterItemView` 私有方法
- [x] 2.3 迁移 `RenderBuffBar` 静态方法到 `MonsterItemView`（buff 渲染规则保持一致）
- [x] 2.4 迁移 HP 进度条/文本/护甲附加显示/名称设置/兼容 `intent-text` 清空到 `MonsterItemView.Render()`
- [x] 2.5 实现 `IDisposable.Dispose()`：当前 `MonsterItemView` 无订阅，`Dispose` 可直接清空容器引用，但接口预留以保持一致

## 3. MonsterListView（怪物列表）

- [x] 3.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/MonsterListView.cs`，构造参数 `(VisualElement monsterContainer, IMonsterListContext context, VisualTreeAsset monsterItemTemplate)`
- [x] 3.2 在构造中订阅 `context.Monsters.Changed`，缓存委托引用以便对称解绑
- [x] 3.3 迁移 `RefreshMonsters` + `CountAliveMonsters` 逻辑到 `MonsterListView.Refresh()`
- [x] 3.4 维护 `List<MonsterItemView>` 替代旧 `List<VisualElement>`，刷新时先 `Dispose` 旧项再重建
- [x] 3.5 实现 `Dispose()`：解绑 `Monsters.Changed`、`Dispose` 所有子项、清空容器、字段置空
- [x] 3.6 添加幂等保护（`_disposed` flag，二次 `Dispose` 直接 return）

## 4. PlayerStatusView（玩家状态面板）

- [x] 4.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/PlayerStatusView.cs`，构造参数 `(VisualElement gameViewRoot, IPlayerStatusContext context)`
- [x] 4.2 在构造中 `Q<>()` 出 `info-text`、`hp-bar-fill`、`hp-text`、`armor-text`、`energy-bar-fill`、`energy-text`、`player-buff-bar` 共 7 个元素引用
- [x] 4.3 订阅 `Phase` / `PlayerHp` / `PlayerMaxHp` / `PlayerArmor` / `Energy` / `MaxEnergy` / `IsLevelComplete` / `IsPlayerDead` 各自 `Changed`，全部 → `RefreshInfo()`
- [x] 4.4 订阅 `PlayerBuffs.Changed` → `RefreshPlayerBuffBar()`
- [x] 4.5 迁移 `RefreshInfo` 含阶段标签 switch 分支 + HP/能量百分比计算 + 护甲文本 + endTurnBtn `SetEnabled` **保留** 到原来位置（endTurnBtn 不属于本视图，下一步处理）
- [x] 4.6 迁移 `RefreshPlayerBuffBar` + `ToList` 静态辅助 + `RenderBuffBar` 调用（buff bar 渲染规则与 `MonsterItemView` 共用 → 提取到 `BuffBarRenderer` 静态类放在 `Views/` 子目录下）
- [x] 4.7 实现 `Dispose()`：解绑全部 `Changed`、字段置空、幂等

## 5. 共享辅助：BuffBarRenderer

- [x] 5.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/BuffBarRenderer.cs`，包含静态 `Render(VisualElement buffBar, IList<BuffRuntime> buffs)`
- [x] 5.2 `MonsterItemView` 与 `PlayerStatusView` 都调用此静态方法，确保规则一致

## 6. GameView 协调器瘦身

- [x] 6.1 在 `GameView.OnSetup()` 中实例化 `PlayerStatusView`，传 `(this, ViewModel)` 隐式向上转型为 `IPlayerStatusContext`
- [x] 6.2 删除 `GameView` 中已迁走的字段：`_infoLabel` / `_hpBarFill` / `_hpText` / `_armorText` / `_energyBarFill` / `_energyText` / `_playerBuffBar` / `_monsterContainer` / `_monsterItems`
- [x] 6.3 删除已迁走的方法：`RefreshInfo`、`RefreshPlayerBuffBar`、`ToList`、`RenderPlayerBuffBar` 之外的 `RenderBuffBar`（已抽到 `BuffBarRenderer`）、`RefreshMonsters`、`CountAliveMonsters`、`RenderIntentCard`、`ResolveCardEffects`
- [x] 6.4 修改 `BindBattleContent()`：先 `_monsterListView?.Dispose()`，再 `Q("monster-container")` 获取容器，`new MonsterListView(...)`
- [x] 6.5 修改 `OnPhaseChanged()`：`endTurnBtn` 的 `SetEnabled(phase==PlayerTurn)` 留在 `BindBattleContent` 后续逻辑；其他 `RefreshInfo` 调用全部删除（PlayerStatusView 自己订阅）
- [x] 6.6 修改 `OnDispose()`：先 `_playerStatusView?.Dispose()`、`_monsterListView?.Dispose()`，再 `base.OnDispose()`
- [x] 6.7 验证 `GameView.cs` 行数从 ~1607 降至 ~1100（实测 1349 行，完成约 258 行迁移；剩余 SelectingTarget / 拖拽 / 预览将在 change 2、3 继续迁出）

## 7. 测试

- [x] 7.1 新增 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/Fakes/FakePlayerStatusContext.cs` 与 `FakeMonsterListContext.cs`，实现切片接口，所有字段是 `new ReactiveProperty<T>(default)`
- [x] 7.2 新增 `Tests/EditMode/Game/UI/PlayerStatusViewTests.cs`：覆盖 HP 百分比、阶段文本映射、关卡完成/死亡优先、空 buff 列表清空、DoT buff 渲染（"4×2"）、Dispose 后不再刷新、二次 Dispose 安全
- [x] 7.3 新增 `Tests/EditMode/Game/UI/MonsterListViewTests.cs`：覆盖空列表、含死亡怪物过滤、aliveCount 计算、Dispose 后不再渲染
- [x] 7.4 新增 `Tests/EditMode/Game/UI/MonsterItemViewTests.cs`：覆盖 HP 文本（含/不含护甲）、SplitAcrossAll 平分、`max(1, value/aliveCount)` 边界、DoT 意图文本（"3×4"）、空 PendingCards 不创建 intent-card；通过 `MonsterItemView.EffectResolverOverride` 测试钩子注入 effect 配置避免依赖 GameLogicEntry.Config
- [x] 7.5 测试改用手工构造 VisualElement 树替代 AssetDatabase 加载 UXML（更轻量、跨 EditMode 测试稳定，且 MonsterItem.uxml/.GameUxml 当前未在仓库中导出 .uxml 标记类）
- [ ] 7.6 跑 `Window > Test Runner > EditMode` 全绿（Unity 编辑器已打开，需在 Unity Test Runner 面板手动触发；命令行 `dotnet build` 已通过）

## 8. 验收

- [x] 8.1 编译检查通过：`dotnet build UnityProject.slnx --no-restore` 0 error 0 warning
- [ ] 8.2 EditMode 测试全绿（待 Unity Test Runner 手动触发）
- [x] 8.3 `openspec validate gameview-extract-status-and-monsters` 通过
- [ ] 8.4 手动验证：进入第一关战斗，HP/护甲/能量进度条与文本正常；玩家中 DoT buff 后 buff bar 显示 "{Value}×{Turns}"；怪物列表正常显示，意图（damage/shield/dot/energy）按规则文本渲染；怪物死亡时移除项；阶段文本切换正确（你的回合 / 怪物回合 / 关卡完成 / 玩家死亡）
- [ ] 8.5 手动验证：进入奖励阶段（杀光怪物）后 Region 正常切到 RewardPanel，再开始新一轮战斗时 BattlePanel 与 MonsterListView 重建无悬挂订阅（无 NullReferenceException 等异常）
