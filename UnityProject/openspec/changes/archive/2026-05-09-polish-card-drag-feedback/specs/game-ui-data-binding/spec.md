## ADDED Requirements

### Requirement: 拖拽出牌失败必须有可见反馈

GameScreen SHALL 订阅 `GameViewModel.CardPlayFailed` 事件，当 `CardSystem.Play` 因任意原因（不在玩家回合 / 卡牌索引无效 / 能量不足 / 无效目标）拒绝出牌时，SHALL 在 drop-zone 上方显示 fail toast，含中文原因文本，1.2 秒后自动淡出。

#### Scenario: 能量不足显示 toast
- **WHEN** 玩家拖拽一张 Cost 大于当前能量的卡到 drop-zone 释放
- **THEN** `CardSystem.Play` SHALL 通过事件总线发布 `CardPlayFailedEvent { Reason = "InsufficientEnergy" }`
- **AND** GameScreen SHALL 在 drop-zone 上方显示 toast，文本 "能量不足"
- **AND** toast SHALL 在 1.2 秒后通过 USS opacity transition 淡出
- **AND** 卡片 SHALL 走原有回弹动画回到手牌

#### Scenario: 不在玩家回合显示 toast
- **WHEN** 玩家在非 PlayerTurn 阶段成功完成一次拖拽释放命中 drop-zone
- **THEN** GameScreen SHALL 显示 toast，文本 "现在不是你的回合"

#### Scenario: 多个 toast 覆盖
- **WHEN** 1 秒内连续发生两次出牌失败
- **THEN** 后一次失败的 toast 文本 SHALL 立即覆盖前一次
- **AND** 计时 SHALL 从最新一次失败开始计 1.2 秒

### Requirement: 法术单目标卡必须支持目标选择 UI

`TargetMode == SingleManual` 的卡 SHALL 在拖入 drop-zone 后进入"目标选择"模式，玩家 SHALL 必须点击一只存活怪物完成出牌。在该模式期间，SHALL 显式展示可选目标，且 SHALL 支持 ESC 或点击空白区域取消。

#### Scenario: 进入目标选择模式
- **WHEN** 玩家拖拽一张 `TargetMode == SingleManual` 的卡释放到 drop-zone
- **THEN** GameScreen SHALL 进入 `SelectingTarget` 状态
- **AND** SHALL 保留卡片 ghost 浮在 drop-zone 上方
- **AND** SHALL 给所有 `IsDead == false` 的怪物 UI item 添加 `.target-selectable.active` 类
- **AND** SHALL NOT 立即调用 `ViewModel.UseCard`

#### Scenario: 选定目标完成出牌
- **WHEN** `SelectingTarget` 状态下玩家点击某只怪物
- **THEN** GameScreen SHALL 调用 `ViewModel.UseCard(handIndex, monsterIndex)`
- **AND** SHALL 销毁 ghost 卡片
- **AND** SHALL 移除所有怪物的 `.target-selectable.active` 类
- **AND** SHALL 回到 `Idle` 状态

#### Scenario: 取消目标选择
- **WHEN** `SelectingTarget` 状态下玩家按 ESC 键或点击非怪物的空白区域
- **THEN** GameScreen SHALL NOT 调用 `ViewModel.UseCard`
- **AND** SHALL 启动卡片回弹动画到原扇形位置
- **AND** SHALL 移除所有 `.target-selectable.active` 类
- **AND** SHALL 回到 `Idle` 状态

#### Scenario: 阶段切换强制取消
- **WHEN** `SelectingTarget` 状态下战斗阶段从 `PlayerTurn` 切换到其他阶段
- **THEN** GameScreen SHALL 立即取消选择并执行回弹

### Requirement: 怪物意图必须基于 PendingCards 渲染

GameScreen SHALL 在每只怪物的 UI item 内根据 `MonsterRuntime.PendingCards` 渲染本回合行动意图。每张 Pending Card SHALL 渲染为一个 `intent-card` 容器，按其 `Effects` 列表渲染若干 `intent-icon` 子元素，每个 icon 按 `EffectKind` 决定颜色类和文本格式。

#### Scenario: 单 Damage 效果意图渲染
- **WHEN** 某只怪物的 PendingCards 包含一张近战卡（一条 Damage 6 effect）
- **THEN** 该怪物 UI item 内 SHALL 出现一个 `intent-card`
- **AND** 该 `intent-card` 内 SHALL 包含一个 `.intent-icon-damage`
- **AND** 该 icon 文本 SHALL 为 "6"

#### Scenario: 投射卡分散意图渲染
- **WHEN** 怪物的 PendingCards 包含一张投射卡（Damage 6, TargetMode SplitAcrossAll），且当前有 2 只存活敌方
- **THEN** 渲染的 `intent-icon-damage` 文本 SHALL 为分散后的值 "3"

> 说明：怪物面对单一玩家时 SplitAcrossAll 退化为单目标全额 6（targets.Count == 1），文本仍为 "6"。

#### Scenario: 法术 DoT 意图渲染
- **WHEN** 怪物的 PendingCards 包含一张法术卡（Damage 8 + DamageDot 2 Duration 3）
- **THEN** 该 `intent-card` 内 SHALL 出现两个 icon：一个 `.intent-icon-damage` 文本 "8" + 一个 `.intent-icon-dot` 文本 "2×3"

#### Scenario: 护盾意图渲染
- **WHEN** 怪物的 PendingCards 包含一张护盾卡（Shield 3, TargetMode Self）
- **THEN** 该 `intent-card` 内 SHALL 出现一个 `.intent-icon-shield` 文本 "3"

#### Scenario: PendingCards 为空时清空意图区
- **WHEN** 某只怪物 PendingCards 列表为空（如 MonsterTurn 完成后到下一次 Prepare 之间）
- **THEN** 该怪物 UI item 的 `intent-container` 内 SHALL 不显示任何 `intent-card`

### Requirement: 玩家与怪物必须显示 Buff 状态条

GameScreen SHALL 为玩家和每只存活怪物显示 buff 状态条 `.buff-bar`，每条 `BuffRuntime` 渲染为一个 `.buff-icon`，文本格式 `{Value}×{RemainingTurns}`。

#### Scenario: 玩家方 buff 渲染
- **WHEN** 玩家身上有任意 buff（DoT 等）
- **THEN** GameScreen SHALL 在 `player-status` 区域旁的 `.buff-bar` 内渲染对应数量的 `.buff-icon`
- **AND** DoT buff 的 icon class SHALL 包含 `.buff-icon-dot`
- **AND** 文本 SHALL 为 `{Value}×{RemainingTurns}` 格式

#### Scenario: 怪物方 buff 渲染
- **WHEN** 某只怪物身上有任意 buff
- **THEN** 该怪物 UI item 内的 `.buff-bar` SHALL 渲染对应 icon

#### Scenario: Buff 倒计时归零移除
- **WHEN** BattleSystem DoT tick 把某条 buff 的 `RemainingTurns` 减到 0 并从列表移除
- **THEN** 下一次 RefreshMonsters / RefreshPlayer 渲染时该 icon SHALL 不再显示

## MODIFIED Requirements

### Requirement: 手牌必须支持拖拽出牌

GameScreen SHALL 在玩家 PointerDown 后通过 `CapturePointer` 捕获指针，并在 PointerMove 累计位移超过 10px 时进入拖拽态。进入拖拽态 SHALL 创建跟随鼠标的 ghost 卡片、将被拖卡完全从扇形布局中移除（不参与剩余卡的 transform 计算）、显示 `drop-zone`。拖拽态下，剩余 N−1 张卡 SHALL 按 N−1 张的扇形参数实时重新计算 transform，且这些卡 transform 变更 SHALL NOT 启用 USS transition。释放（PointerUp）时若指针位于 `drop-zone` 内：

- 卡的 `TargetMode != SingleManual` → SHALL 调用 `ViewModel.UseCard(handIndex, -1)` 由后端按 TargetMode 决策目标
- 卡的 `TargetMode == SingleManual` → SHALL 进入 `SelectingTarget` 状态（见独立 Requirement）

若指针不在 `drop-zone` 也不在 `hand-fan` 内 SHALL 启动协同回弹。整个过程 SHALL 通过 `ReleasePointer` 释放捕获。

> 本 Requirement 是 `improve-hand-drag-interaction` → Change 4 (`polish-card-drag-feedback`) 两次 MODIFIED 的累积态：前者引入"被拖卡完全脱离扇形 / 协同回弹 / 不触发滚动"等基础交互；本变更追加"按 TargetMode 分支调用 UseCard 或进入选目标态"。两者的 Scenarios 必须共存。

#### Scenario: 越过位移阈值进入拖拽
- **WHEN** 玩家在某张手牌 PointerDown 后移动 ≥ 10px
- **THEN** GameScreen SHALL 创建一张跟随鼠标的 ghost VisualElement
- **AND** 被拖卡 SHALL 从扇形布局中移除（不参与剩余卡的 transform 计算，视觉上从 hand-fan 中"消失"）
- **AND** 剩余 N−1 张卡 SHALL 按 N−1 张的扇形参数（centerIndex、间距、rotate、translateY）重新计算并写入 inline transform
- **AND** `drop-zone` SHALL 显示（添加 `active` 类）
- **AND** SHALL NOT 给被拖卡或其他卡添加 `card-item--placeholder` 类

#### Scenario: ghost 跟随鼠标
- **WHEN** 拖拽态期间 PointerMove
- **THEN** ghost 的 left/top SHALL 实时更新到指针 panel 局部坐标（中心对齐）
- **AND** 剩余 N−1 张卡的 transform 在拖拽中变更时 SHALL NOT 启用 USS transition（保持跟手响应）

#### Scenario: 释放在 drop-zone 内出非选目标卡
- **WHEN** 拖拽态期间 PointerUp 且指针位于 `drop-zone` 的 worldBound 内，且当前手牌的 `TargetMode != SingleManual`
- **THEN** GameScreen SHALL 调用 `ViewModel.UseCard(handIndex, -1)`
- **AND** SHALL 立即销毁 ghost，不做回弹动画

#### Scenario: 释放在 drop-zone 内出选目标卡
- **WHEN** 拖拽态期间 PointerUp 且指针位于 `drop-zone` 的 worldBound 内，且当前手牌的 `TargetMode == SingleManual`
- **THEN** GameScreen SHALL 进入 `SelectingTarget` 状态
- **AND** SHALL NOT 立即调用 `ViewModel.UseCard`
- **AND** SHALL 保留 ghost 卡片浮在 drop-zone 上方

#### Scenario: 释放在中间地带回弹（既不在 drop-zone 也不在 hand-fan）
- **WHEN** 拖拽态期间 PointerUp 且指针既不在 `drop-zone.worldBound` 内也不在 `hand-fan.worldBound` 内
- **THEN** ghost 的 left/top SHALL 通过 0.15s USS transition 过渡到拖拽前被拖卡所在的扇形位置（N 张布局下的目标位置）
- **AND** 剩余 N−1 张卡 SHALL 同时（同一帧切到回弹模式）以 0.15s USS transition 协同动回 N 张布局（被拖卡重新占据原槽位）
- **AND** 协同动画期间，所有参与卡 SHALL 临时启用 transition；动画结束后 SHALL 移除临时 transition 类
- **AND** 过渡完成后 ghost SHALL 被销毁
- **AND** SHALL NOT 调用 `ViewModel.UseCard`

#### Scenario: 拖拽期间不触发滚动或界面位移
- **WHEN** 玩家在卡牌上 PointerDown 并拖动
- **THEN** GameView 的任何容器 SHALL NOT 发生整体上移或滚动
- **AND** PointerMove/PointerUp 事件 SHALL 因 `CapturePointer` 持续派发到拖拽源
