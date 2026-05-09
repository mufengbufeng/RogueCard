## Why

当前 `GameScreen.cs` 的手牌拖拽采用"占位 + ghost"模式：拖拽时原卡仍占着扇形位置（仅 opacity 0.3），其他卡不动，且无法通过拖拽调整手牌顺序。这种交互对玩家来说反馈较弱、且缺少"整理手牌"的能力。本次改造把单一的"拖拽出牌"升级成更直观的三态拖拽（脱离 / 区域内插入 / 出牌），让被拖卡视觉上真正离开扇形、其他卡自动让位、并支持在 hand-fan 区域内拖拽以调整顺序。

## What Changes

- 拖拽进入态：被拖卡完全脱离扇形布局，剩余 N−1 张卡按 N−1 张的扇形参数重新计算 transform
- 拖拽中三态切换（按指针位置每帧判定）：DraggingDetached（中间地带）/ DraggingInsertSlot（hand-fan 内）/ DraggingOverDropZone（drop-zone 内）
- 区域内拖拽显示半透明插槽：鼠标在 `hand-fan.worldBound` 内时，剩余 N−1 张卡按"留出一个空槽"的 N 张布局排列，空槽位置显示一张半透明 `card-item--insert-slot` 占位卡
- 松手在 hand-fan 内 → 调整 `_cardItems` 列表顺序（**仅 UI 层，不调用 ViewModel**）
- 松手在中间地带 → ghost 回弹原位 + 其他卡协同动回 N 张布局（与原行为不同：原来其他卡不动）
- 松手在 drop-zone 内 → 出牌（行为不变）
- USS：新增 `.card-item--insert-slot`；为 `.card-item` 增加 transition 控制类（拖拽中无 transition、松手回弹时临时启用）
- **BREAKING（视觉行为）**：移除拖拽中"原卡保留 placeholder（opacity 0.3）"的视觉效果。`card-item--placeholder` 类会被弃用或重新解释（拖拽中"被拖卡"是从扇形里被移除而不是占位）

## Capabilities

### New Capabilities
（无新 capability；本次改动不引入新能力域，全部落在 game-ui-data-binding 内）

### Modified Capabilities
- `game-ui-data-binding`：修改"手牌必须支持拖拽出牌"的拖拽进入与回弹场景；新增"手牌必须支持区域内拖拽调整顺序（UI 层）"和"拖拽中其他卡的过渡控制"两个 requirement

## Impact

- **代码**：`Assets/GameScripts/HotFix/GameLogic/UI/Game/GameScreen.cs`（状态机、扇形布局、PointerMove/Up 分发）
- **样式**：`Assets/AssetRaw/UI/Game/GameViewStyles.uss`（新增 `.card-item--insert-slot`，调整 `.card-item` transition 控制）
- **不影响**：`GameViewModel`、`CardSystem`、`GameModel`、`BattlePanel.uxml`、`drop-zone` 行为、preview / hover 互斥逻辑、`PointerCaptureOut` 异常恢复路径
- **测试**：现有 `GameLogic.Tests.EditMode` 不会因为本次改动失败（不动 GameLogic）；如能从 GameScreen 解耦插入位置算法，可新增一个 EditMode 单测
- **平行变更**：与 in-progress 的 `migrate-ui-to-uitoolkit`（39/45）独立，不与其合并；因都涉及 GameScreen，开始实现前需关注 migrate 剩余 task 是否会触及相同文件
