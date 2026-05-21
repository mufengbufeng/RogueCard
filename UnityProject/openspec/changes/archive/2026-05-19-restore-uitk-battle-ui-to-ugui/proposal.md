## Why

当前运行时 UI 已回退到 UGUI 框架，但局内 `GameView` 只保留了文本摘要和运行时动态按钮，丢失了 UITK 阶段已经实现的手牌扇形布局、拖拽出牌、卡牌预览、怪物意图、目标选择、失败 toast、奖励面板等核心战斗 UI 体验。现在需要把这些已验证的功能恢复到 UGUI Prefab / `UIView` / `UIController` / `ReferenceCollector` 工作流中，避免继续依赖运行时 UI Toolkit。

## What Changes

- 将 `GameView.prefab` 从最小 UGUI 占位界面扩展为完整局内战斗 UI：玩家状态条、怪物区、手牌区、出牌区域、预览层、失败提示、奖励面板和可复用条目模板。
- 将 UITK 阶段的 `PlayerStatusView`、`MonsterListView`、`MonsterItemView`、`HandFanView`、`CardItemView`、`TargetSelector`、`TurnControlView`、`BattlePanelView` 行为恢复为 UGUI 适配层。
- 复用现存纯逻辑 `CardDragController`、`FanLayoutCalc`、`HandFanLayoutOptions`，新增 UGUI `IDragSurface` 生产实现，而不是恢复 `UnityEngine.UIElements`。
- 将卡牌预览、拖拽 ghost、插入占位、drop-zone 出牌、手动选目标、取消回弹等交互映射到 `RectTransform`、UGUI `Button/Image/TextMeshProUGUI`、`CanvasGroup` 和 Unity pointer 事件。
- 将怪物意图、怪物 Buff、玩家 Buff、HP/能量进度条和阶段文本从 UITK Label/VisualElement 渲染迁移到 UGUI 组件渲染。
- 移除 `GameView` 中为迁移临时存在的运行时文本和运行时按钮兜底路径，优先使用 Prefab 与 ReferenceCollector 绑定。
- 保持主菜单 `MainView.prefab` 的现有 UGUI 流程，只补齐必要绑定检查，不重做主菜单视觉。
- 不重新引入运行时 UXML、USS、`VisualTreeAsset`、`VisualElement` 或 `UIDocument` 依赖。

## Capabilities

### New Capabilities

- `gameview-ugui-prefab-composition`: 定义 UGUI `GameView.prefab` 的 ReferenceCollector、面板、容器、条目模板和运行时绑定契约。

### Modified Capabilities

- `game-ui-data-binding`: 将局内 UI 数据绑定要求从 UITK Screen/Region 子模块改为 UGUI `UIView` 子模块与 Prefab 组件绑定。
- `gameview-player-status-view`: 将玩家状态视图从 `VisualElement`/Label/USS 进度条迁移为 UGUI 组件渲染。
- `gameview-monster-list-view`: 将怪物列表和怪物项从 UXML 模板克隆迁移为 UGUI 条目 Prefab/组件渲染。
- `gameview-hand-fan-view`: 将手牌与卡牌项从 `VisualElement` 事件转发迁移为 UGUI `RectTransform`、pointer handler 和条目 Prefab。
- `gameview-card-preview`: 将卡牌预览从 UITK `IPreviewSurface` 迁移为 UGUI 预览层和克隆卡牌视图。
- `gameview-card-drag-state-machine`: 保留状态机行为，但生产 `IDragSurface` 语义改为 UGUI 坐标、命中、透明度、指针捕获和动画适配。
- `gameview-target-selection-flow`: 将手动目标选择从 UITK 点击/ESC/Backdrop 监听迁移为 UGUI 怪物按钮高亮、取消区域和回弹协作。
- `gameview-turn-control-view`: 将结束回合按钮和失败 toast 从 UITK `Button`/`Label` 迁移为 UGUI Button/TextMeshProUGUI/CanvasGroup。
- `gameview-battle-panel-coordinator`: 将 BattlePanel 装配从 `VisualElement content` 和 UXML 区域迁移为 UGUI 面板、容器和子视图生命周期。
- `main-to-game-view-flow`: 保持主菜单到局内流程不变，但要求打开的 UGUI `GameView` 承载完整战斗 UI，而不是临时摘要界面。
- `auto-bind-ui-script`: 确保新增局内 UGUI 引用继续通过 `ReferenceCollector` / UHub 绑定，不生成任何 UI Toolkit 代码。

## Impact

- 影响 HotFix 层 UI：`Assets/GameScripts/HotFix/GameLogic/UI/Game/` 下新增或改写 UGUI 子视图适配层，并改造 `GameView` / `GameController`。
- 影响资源：`Assets/AssetRaw/UI/Game/GameView.prefab` 需要补齐完整 UGUI 节点、容器、条目模板和 ReferenceCollector key；`Assets/AssetRaw/UI/Main/MainView.prefab` 仅做绑定校验或小修。
- 影响测试：新增或改写 EditMode 测试覆盖 UGUI 子视图渲染、拖拽状态机适配、命令转发和流程契约；实际 Prefab、Canvas、GraphicRaycaster 交互需要 Unity 编辑器或 PlayMode 手动验证。
- 影响文档与规格：多个 `gameview-*` 主规格目前仍描述 UITK 类型，本变更需要用 delta spec 明确 UGUI 后的运行时契约。
