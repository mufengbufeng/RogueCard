## 1. 测试基线与 Prefab 契约

- [x] 1.1 新增 `GameView` Prefab 绑定契约测试或编辑器校验脚本，覆盖 `ReferenceCollector` 必需 key：BattlePanel、RewardPanel、PlayerStatusPanel、InfoText、PlayerHpFill、PlayerHpText、PlayerArmorText、PlayerEnergyFill、PlayerEnergyText、PlayerBuffBar、MonsterRect、CardSc、DropZone、PreviewLayer、EndBtn、FailToast、RewardConfirmBtn、HandCardTemplate、MonsterItemTemplate、BuffIconTemplate、IntentIconTemplate。
- [x] 1.2 新增 `GameView` 不再创建临时摘要 UI 的测试或静态检查，确认生产路径不依赖 `PhaseTextRuntime`、`HandCommandPanelRuntime`、`MonsterCommandPanelRuntime`。
- [x] 1.3 新增或更新运行时代码无 UITK 依赖检查，目标命令为 `rg "UnityEngine.UIElements|VisualElement|UIDocument|VisualTreeAsset|StyleSheet|UXML|USS|Uxml|Uss" Assets/GameScripts Assets/EF/EFRuntime`，允许编辑器工具目录例外。

## 2. UGUI 状态与怪物渲染

- [x] 2.1 先写 `PlayerStatusView` EditMode 测试，覆盖阶段文本、HP/能量比例、护甲文本、玩家 Buff 渲染和 Dispose 后不再刷新。
- [x] 2.2 实现 UGUI `PlayerStatusView` 与必要绑定结构，使用 `TextMeshProUGUI`、`Image`/`RectTransform` 和 Buff 图标模板刷新玩家状态。
- [x] 2.3 先写 `MonsterItemView` / `MonsterListView` EditMode 测试，覆盖存活怪物过滤、怪物名称/HP/护甲、PendingCard 意图、SplitAcrossAll 平分、DoT Buff、Dispose 后不刷新。
- [x] 2.4 实现 UGUI `MonsterItemView`、`MonsterListView` 和共享 Buff/意图图标渲染辅助，确保运行时怪物项保留原始 monster index。

## 3. UGUI 手牌、预览与拖拽适配

- [x] 3.1 先写 UGUI `CardItemView` / `HandFanView` EditMode 测试，覆盖卡名费用、HandIndex 闭包语义、首次刷新生成卡牌项、扇形布局应用到 `RectTransform`、Dispose 清理。
- [x] 3.2 实现 UGUI `CardItemView` 与 `HandFanView`，从 `HandCardTemplate` 实例化卡牌项并接入 `FanLayoutCalc`。
- [x] 3.3 先写 UGUI `IDragSurface` 适配测试，覆盖 hand-fan/drop-zone 命中、opacity/picking、ghost 创建销毁、insert slot 创建销毁、transitionDuration 调用记录。
- [x] 3.4 实现 UGUI `IDragSurface` 生产适配层，复用现有 `CardDragController`，用 `RectTransformUtility` 或等价方式完成坐标转换和命中判断。
- [x] 3.5 先写 `CardPreviewController` UGUI surface 测试，覆盖单击同卡关闭、点击别卡切换、拖拽开始关闭、非卡区域关闭且消费事件、克隆卡文本和不参与 raycast。
- [x] 3.6 实现 UGUI 卡牌预览层和 `IPreviewSurface` 适配，预览对象使用手牌模板克隆或独立预览模板。

## 4. 目标选择、回合控制与 BattlePanel 协调

- [x] 4.1 先写 UGUI `TargetSelector` 测试，覆盖进入目标选择高亮存活怪物、点击怪物调用 `UseCardOnMonster`、点击空白或外部 `Cancel` 调用 `RequestGhostRebound`、Dispose 清理。
- [x] 4.2 实现 UGUI `TargetSelector`，把怪物项按钮/点击区域与取消区域接入手动选目标流程。
- [x] 4.3 先写 UGUI `TurnControlView` 测试，覆盖 `Phase` 控制 `EndBtn.interactable`、点击转发 `EndTurn`、失败 reason 中文映射、1.2 秒隐藏和新失败覆盖旧失败。
- [x] 4.4 实现 UGUI `TurnControlView`，使用 `Button.onClick`、`TextMeshProUGUI`、`CanvasGroup`/active 状态显示失败 toast。
- [x] 4.5 先写 UGUI `BattlePanelView` 测试，覆盖子模块装配顺序、缺失关键绑定时报错不抛异常、AutoTarget 直接 `UseCard`、SingleManual 进入目标选择、Phase 离开 PlayerTurn 取消目标选择、Dispose 反序释放。
- [x] 4.6 实现 UGUI `BattlePanelView`，统一装配 `MonsterListView`、`HandFanView`、`TurnControlView`、`TargetSelector` 并负责事件路由。

## 5. GameView / GameController 集成

- [x] 5.1 先写 `GameView` 集成测试，覆盖打开时装配 `PlayerStatusView`、按 Phase 显隐 BattlePanel/RewardPanel、RewardConfirmBtn 调用 `GameViewModel.SelectReward`、切到 Reward 时释放 `BattlePanelView`。
- [x] 5.2 改造 `GameView`，移除临时摘要文本/按钮兜底路径，改为使用 ReferenceCollector/UHub 绑定的 UGUI 面板和子视图。
- [x] 5.3 先写 `GameController` 命令流测试，覆盖 `GameViewModel` 变化触发 `GameView` 刷新、出牌失败事件进入 `TurnControlView`、关闭释放时事件解绑。
- [x] 5.4 改造 `GameController` 与 `GameView` 的交互边界，保留 `GameProcedure` 对玩法 System 生命周期和 ViewModel 命令订阅的所有权。
- [x] 5.5 更新 `main-to-game-view-flow` 相关测试，确认 `GameProcedure` 打开的 `GameView` 是完整 UGUI 战斗 UI，并且关卡完成返回主菜单仍关闭 `GameView`。

## 6. Prefab 与资源落地

- [x] 6.1 在 `Assets/AssetRaw/UI/Game/GameView.prefab` 补齐 BattlePanel、RewardPanel、状态条、怪物容器、手牌容器、drop-zone、preview-layer、toast、模板节点和 `ReferenceCollector` key。
- [x] 6.2 检查 `Assets/AssetRaw/UI/Main/MainView.prefab` 的现有绑定，只修复缺失或错误引用，不重做主菜单视觉。
- [x] 6.3 检查 YooAsset / Addressable 地址，确保 `"GameView"` 和 `"MainView"` 指向 UGUI Prefab，不指向 `_OLD` 资源或已删除 UITK 资源。
- [x] 6.4 更新 `Assets/EF/EFRuntime/UI/README.md` 或相关文档，说明局内 UI 使用 UGUI 子视图和 Prefab 模板恢复战斗交互。

## 7. 最终验证

- [x] 7.1 运行项目脚本编译检查：`python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`；若 Unity Skills 不可用，则运行 `dotnet build UnityProject.slnx --no-restore`。
- [x] 7.2 运行相关 EditMode 测试：UGUI PlayerStatusView、MonsterListView、HandFanView、CardPreviewController、TargetSelector、TurnControlView、BattlePanelView、GameView/GameController 命令流。
- [x] 7.3 运行无 UITK 运行时依赖检查：`rg "UnityEngine.UIElements|VisualElement|UIDocument|VisualTreeAsset|StyleSheet|UXML|USS|Uxml|Uss" Assets/GameScripts Assets/EF/EFRuntime` 并确认只剩允许的编辑器或文档例外。
- [ ] 7.4 在 Unity 编辑器中手动验证完整流程：启动进入 MainView、点击开始进入 GameView、查看玩家状态和怪物意图、拖拽卡牌到 drop-zone、手动选目标卡确认/取消、结束回合、失败 toast、奖励确认、关卡完成返回 MainView。
