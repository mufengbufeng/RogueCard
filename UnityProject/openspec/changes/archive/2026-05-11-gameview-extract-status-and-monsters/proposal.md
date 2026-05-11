## Why

`GameView.cs` 已累积至 1607 行，单一类内部混合了玩家状态、怪物渲染、手牌扇形布局、四态拖拽状态机、预览克隆、目标选择、结束回合、失败 toast 等至少七类相互独立的职责。任何局部改动都需要跨章节定位，回归测试覆盖率接近零。

第一步先抽出最稳定、与手牌交互无耦合的两个子模块——玩家状态面板与怪物列表——把 `GameView` 体积压到约 1100 行，为后续手牌子系统重构（change 2）与协调器抽象（change 3）腾出空间。本 change 不改可观察行为，只重排实现结构与契约。

## What Changes

- **新增** `PlayerStatusView` 子模块（纯 C# 控制器，非 Screen），封装 `info-bar` + `player-status`（HP/护甲/能量进度条）+ 玩家 buff bar 的查询与渲染
- **新增** `MonsterListView` 子模块，封装 `monster-container` 内动态怪物项的实例化与刷新
- **新增** `MonsterItemView`，封装单只怪物视图（名称/HP/意图/buff）与 PendingCard 意图渲染
- **新增** `IPlayerStatusContext`、`IMonsterListContext` 切片接口；`GameViewModel` 显式实现这两个接口（零运行时开销，编译期约束）
- `GameView` 中的 `RefreshInfo`、`RefreshPlayerBuffBar`、`RefreshMonsters`、`RenderIntentCard`、`RenderBuffBar` 方法迁出到对应子模块；`GameView` 不再直接 `Q<>()` 这两个区域的元素
- 子模块在 `OnSetup` / `BindBattleContent` 中实例化；在 `OnDispose` / `Region` 切换时主动 `Dispose` 解订阅
- 保留 `RewardPanel` 的按钮处理在 `GameView` 中（按设计仅一个按钮，不值得拆出）
- **测试** EditMode 用例覆盖：HP 百分比计算、Buff bar 元素数量、意图文本格式（含 SplitAcrossAll 平分）、空列表清理

## Capabilities

### New Capabilities

- `gameview-player-status-view`: 玩家状态面板（info-bar 文本、HP/护甲/能量条与文本、玩家 buff bar）的子模块渲染契约，订阅 `IPlayerStatusContext` 切片
- `gameview-monster-list-view`: 怪物列表与单只怪物视图（HP 条/意图渲染/buff bar/死亡过滤）的子模块渲染契约，订阅 `IMonsterListContext` 切片

### Modified Capabilities

- `game-ui-data-binding`: 把"GameScreen 信息区域显示"、"GameScreen 怪物意图显示"、"GameView 动态怪物项实例化"等要求迁出，改写为"GameView 协调器装配 PlayerStatusView 与 MonsterListView 子模块，子模块通过切片接口订阅 ViewModel"

## Impact

- **框架层** (`Assets/EF/EFRuntime/UI/`) — 不变
- **游戏层** (`Assets/GameScripts/HotFix/GameLogic/UI/Game/`) — `GameView.cs` 减少约 500 行；新增 5 个文件：
  - `IPlayerStatusContext.cs`、`PlayerStatusView.cs`
  - `IMonsterListContext.cs`、`MonsterListView.cs`、`MonsterItemView.cs`
- **ViewModel** — `GameViewModel : IPlayerStatusContext, IMonsterListContext` 接口声明，无字段变化
- **资源** (`Assets/AssetRaw/UI/Game/`) — `GameUxml.uxml`、`BattlePanel.uxml`、`MonsterItem.uxml`、对应 USS 全部不变
- **测试** — 新增 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/UI/Game/PlayerStatusViewTests.cs`、`MonsterListViewTests.cs`，使用 mock 切片接口验证渲染分支
- **风险** — 低。两个子模块自包含，与手牌交互无任何共享元素或状态机耦合。回归只需手动验证：进入战斗后 HP/能量/护甲/玩家 buff/怪物 HP/意图/怪物 buff 全部正常显示；怪物死亡时移除
