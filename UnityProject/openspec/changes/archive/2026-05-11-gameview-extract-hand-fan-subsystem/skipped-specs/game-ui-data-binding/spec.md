## MODIFIED Requirements

### Requirement: GameScreen 必须通过 ViewModel 命令意图转发用户操作

`GameView` 与 `HandFanView` SHALL 将用户交互（拖拽手牌到 drop-zone、点击结束回合）转发为 `GameViewModel` 的命令意图调用（`UseCard`、`EndTurn`）。SHALL NOT 包含游戏逻辑。手牌单击仅由 `CardPreviewController` 切换预览态，SHALL NOT 调用任何 `ViewModel` 命令。

#### Scenario: 拖拽手牌到 drop-zone（AutoTarget）转发到 ViewModel

- **WHEN** 玩家拖拽某张 `TargetMode != SingleManual` 的手牌并在 drop-zone 内释放
- **THEN** `HandFanView.CardDroppedOnZone(handIdx, false)` SHALL 触发
- **AND** 上层 SHALL 调 `IHandContext.UseCard(handIdx)`
- **AND** SHALL NOT 直接调用 `CardSystem` 或修改 `Model`

#### Scenario: 拖拽 SingleManual 卡到 drop-zone 进入选目标态

- **WHEN** 玩家拖拽 `TargetMode == SingleManual` 的手牌并在 drop-zone 内释放
- **THEN** `HandFanView.CardDroppedOnZone(handIdx, true)` SHALL 触发
- **AND** 上层 SHALL NOT 立即调 `UseCard`，而是进入 `SelectingTarget` 态

#### Scenario: 单击手牌不调用 ViewModel

- **WHEN** 玩家在手牌上完成一次单击（位移 ≤ 10px）
- **THEN** `HandFanView.CardClicked(handIdx)` SHALL 触发
- **AND** 上层 `CardPreviewController` SHALL 仅切换预览态
- **AND** SHALL NOT 调用 `ViewModel.UseCard`

#### Scenario: 点击结束回合转发到 ViewModel

- **WHEN** 用户点击结束回合按钮
- **THEN** SHALL 调 `ViewModel.EndTurn()`
- **AND** SHALL NOT 直接调用 `BattleSystem`

## REMOVED Requirements

### Requirement: 手牌区域必须使用扇形布局

**Reason**: 行为已迁移到 `gameview-fan-layout-calc` 与 `gameview-hand-fan-view` capability。
**Migration**: 见 `gameview-fan-layout-calc/spec.md` 中 "ComputeSlot 必须按现有公式输出扇形 transform"，以及 `gameview-hand-fan-view/spec.md` 中 "HandFanView 必须按 Hand 列表全量重建 CardItemView" / "HandFanView 必须响应 hand-fan 几何变化"。

### Requirement: 手牌必须支持点击放大预览

**Reason**: 行为已迁移到 `gameview-card-preview` capability。
**Migration**: 见 `gameview-card-preview/spec.md` 中 "CardPreviewController 必须支持单击切换预览态" / "CardPreviewController 必须按 hand-fan 局部坐标定位预览克隆"。

### Requirement: 手牌必须支持悬停抬升

**Reason**: 行为已迁移到 `gameview-hand-fan-view` capability，由 `CardItemView.SetHovering(bool)` 实现，仅在 `Idle` 态生效（`Dragging` / `Previewing` 态不响应 hover）。
**Migration**: 见 `gameview-hand-fan-view/spec.md` 中 "CardItemView 必须封装单卡视图与事件转发" 的 hover 类切换 scenario。
