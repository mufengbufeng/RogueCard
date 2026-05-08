# game-ui-data-binding Specification

## Purpose

定义 GameScreen 的数据绑定规则。GameScreen 通过订阅 GameViewModel 的 ReactiveProperty 驱动所有 UI 更新（怪物列表、手牌列表、玩家状态、战斗阶段），并通过 ViewModel 命令意图（UseCard、EndTurn、SelectReward）转发用户操作。GameScreen 内嵌一个 Region 用于在 BattlePanel 与 RewardPanel 之间切换主区域内容。
## Requirements
### Requirement: GameScreen 必须通过 ReactiveProperty 驱动所有 UI 更新
GameScreen SHALL 在 OnSetup() 中订阅 GameViewModel 的所有 ReactiveProperty.Changed 事件。任何 UI 更新 SHALL 由 ViewModel 属性变化驱动，Screen SHALL NOT 直接访问 Model 或 Config。

#### Scenario: 怪物列表变化时刷新 UI
- **WHEN** ViewModel.Monsters.Value 被设置为新的怪物列表
- **THEN** GameScreen SHALL 收到 Changed 回调
- **AND** GameScreen SHALL 清空旧怪物元素并重新实例化怪物子项

#### Scenario: 手牌列表变化时刷新 UI
- **WHEN** ViewModel.Hand.Value 被设置为新的手牌列表
- **THEN** GameScreen SHALL 收到 Changed 回调
- **AND** GameScreen SHALL 刷新手牌区域

### Requirement: GameScreen 必须通过 ViewModel 命令意图转发用户操作
GameScreen SHALL 将用户交互（拖拽手牌到 drop-zone、点击结束回合）转发为 ViewModel 的命令意图调用（UseCard、EndTurn）。GameScreen SHALL NOT 包含游戏逻辑。单击手牌仅切换预览态，SHALL NOT 调用任何 ViewModel 命令。

#### Scenario: 拖拽手牌到 drop-zone 转发到 ViewModel
- **WHEN** 玩家拖拽某张手牌并在 drop-zone 内释放
- **THEN** GameScreen SHALL 调用 ViewModel.UseCard(handIndex)
- **AND** SHALL NOT 直接调用 CardSystem 或修改 Model

#### Scenario: 单击手牌不调用 ViewModel
- **WHEN** 玩家在手牌上完成一次单击（位移 ≤ 10px）
- **THEN** GameScreen SHALL 仅切换预览态
- **AND** SHALL NOT 调用 ViewModel.UseCard

#### Scenario: 点击结束回合转发到 ViewModel
- **WHEN** 用户点击结束回合按钮
- **THEN** GameScreen SHALL 调用 ViewModel.EndTurn()
- **AND** SHALL NOT 直接调用 BattleSystem

### Requirement: GameScreen 必须支持 Region 切换 Battle 和 Reward 视图
GameScreen SHALL 包含一个 Region 用于主区域切换。当 ViewModel.Phase 变化时，GameScreen SHALL 通过 Region 切换显示 BattlePanel 或 RewardPanel。Phase 类型为 BattlePhase 枚举（Idle/Prepare/PlayerTurn/MonsterTurn/Check/Reward）。

#### Scenario: Phase 变为 PlayerTurn 时显示战斗面板
- **WHEN** ViewModel.Phase.Value 变为 BattlePhase.PlayerTurn（或 Prepare/MonsterTurn/Check）
- **THEN** GameScreen SHALL 通过 Region 加载并显示 BattlePanel UXML

#### Scenario: Phase 变为 Reward 时显示奖励面板
- **WHEN** ViewModel.Phase.Value 变为 BattlePhase.Reward
- **THEN** GameScreen SHALL 通过 Region 加载并显示 RewardPanel UXML

### Requirement: 手牌区域必须使用扇形布局
GameScreen SHALL 将手牌渲染在固定尺寸的 `hand-fan` 容器中（不使用 ScrollView），并按当前手牌数量为每张卡牌动态计算 inline transform 形成轻微弧形扇形。每张卡牌 SHALL `position: absolute`，`transform-origin` SHALL 为底部中心（`50% 100%`），rotate 角度 SHALL 与 `index - centerIndex` 成正比（每张约 ±3°），translateY SHALL 与 `(index - centerIndex)²` 成正比形成抛物线下沉，相邻卡牌的水平间距 SHALL 自适应容器宽度（最大 120px）。

#### Scenario: 5 张手牌呈现扇形
- **WHEN** ViewModel.Hand.Value 包含 5 张卡牌
- **THEN** GameScreen SHALL 按 index 计算 rotate 与 translateY，使中间一张水平且最高、左右两端最倾斜且最低
- **AND** 所有卡牌 SHALL 通过 absolute 定位排布在 `hand-fan` 容器内
- **AND** 卡牌之间 SHALL 通过自适应间距（最大 120px）形成视觉层叠

#### Scenario: 手牌数量变化时扇形重排
- **WHEN** ViewModel.Hand.Value 从 5 张变为 4 张
- **THEN** GameScreen SHALL 重新计算 4 张卡的 transform，仍以新 centerIndex 居中
- **AND** SHALL NOT 出现卡牌溢出 `hand-fan` 容器或互相遮挡过半的情况

### Requirement: 手牌必须支持点击放大预览
GameScreen SHALL 在玩家单击（PointerDown 与 PointerUp 之间位移 ≤ 10px）某张卡牌时进入预览态：将该卡克隆到独立的 `preview-layer` 容器中，以 1.6 倍尺寸显示在原卡正上方，原卡 SHALL 保留在扇形里位置不变。再次单击同一张卡或单击空白区 SHALL 退出预览态；单击另一张卡 SHALL 切换预览目标。预览克隆卡 SHALL `pointer-events: none`，不抢占拖拽事件。

#### Scenario: 单击卡牌进入预览
- **WHEN** 玩家在某张手牌上 PointerDown 后 PointerUp，且 down/up 位移 ≤ 10px
- **THEN** GameScreen SHALL 在 `preview-layer` 中克隆一张该卡的放大版（1.6×）显示在原卡正上方
- **AND** 原卡 SHALL 保留在扇形中位置不变
- **AND** SHALL NOT 调用 ViewModel.UseCard

#### Scenario: 再次单击同卡退出预览
- **WHEN** 当前处于预览态且玩家再次单击同一张卡
- **THEN** GameScreen SHALL 销毁 `preview-layer` 中的克隆卡

#### Scenario: 单击另一张卡切换预览
- **WHEN** 当前正在预览卡 A，玩家单击卡 B
- **THEN** GameScreen SHALL 销毁 A 的克隆卡并克隆 B 显示在 B 正上方

### Requirement: 手牌必须支持悬停抬升
GameScreen SHALL 在鼠标悬停某张手牌时让该卡 translateY 上移 20px、scale 1.05，并通过 0.15s USS transition 平滑过渡；鼠标离开 SHALL 在 0.15s 内回到扇形原位置。在拖拽态或预览态时 SHALL 禁用悬停抬升效果。

#### Scenario: 悬停抬升
- **WHEN** 鼠标进入某张手牌（PointerEnter）且当前不在拖拽态/预览态
- **THEN** 该卡 SHALL 在 0.15s 内 translateY 上移 20px 并 scale 1.05

#### Scenario: 离开回弹
- **WHEN** 鼠标离开正在抬升的手牌（PointerLeave）
- **THEN** 该卡 SHALL 在 0.15s 内回到扇形计算出的原 transform

#### Scenario: 拖拽中禁用悬停
- **WHEN** 当前处于拖拽态
- **THEN** 任何卡牌的 PointerEnter SHALL NOT 触发抬升

### Requirement: 手牌必须支持拖拽出牌
GameScreen SHALL 在玩家 PointerDown 后通过 `CapturePointer` 捕获指针，并在 PointerMove 累计位移超过 10px 时进入拖拽态。进入拖拽态 SHALL 创建跟随鼠标的 ghost 卡片、给原卡加 `card-item--placeholder` 类（opacity 0.3 占位）、显示 `drop-zone`。释放（PointerUp）时若指针位于 drop-zone 内 SHALL 调用 `ViewModel.UseCard(index)`；否则 ghost SHALL 通过 0.15s USS transition 平滑回弹到原卡位置后销毁。整个过程 SHALL 通过 `ReleasePointer` 释放捕获。

#### Scenario: 越过位移阈值进入拖拽
- **WHEN** 玩家在某张手牌 PointerDown 后移动 ≥ 10px
- **THEN** GameScreen SHALL 创建一张跟随鼠标的 ghost VisualElement
- **AND** 原卡 SHALL 加上 `card-item--placeholder` 类保留位置占位
- **AND** `drop-zone` SHALL 显示（添加 `active` 类）

#### Scenario: ghost 跟随鼠标
- **WHEN** 拖拽态期间 PointerMove
- **THEN** ghost 的 left/top SHALL 实时更新到指针 panel 局部坐标（中心对齐）

#### Scenario: 释放在 drop-zone 内出牌
- **WHEN** 拖拽态期间 PointerUp 且指针位于 drop-zone 的 worldBound 内
- **THEN** GameScreen SHALL 调用 ViewModel.UseCard(handIndex)
- **AND** SHALL 立即销毁 ghost，不做回弹动画

#### Scenario: 释放在 drop-zone 外回弹
- **WHEN** 拖拽态期间 PointerUp 且指针不在 drop-zone 内
- **THEN** ghost 的 left/top SHALL 通过 0.15s transition 过渡到原卡 worldBound 中心
- **AND** 过渡完成后 ghost SHALL 被销毁
- **AND** 原卡 SHALL 移除 `card-item--placeholder` 类
- **AND** SHALL NOT 调用 ViewModel.UseCard

#### Scenario: 拖拽期间不触发滚动或界面位移
- **WHEN** 玩家在卡牌上 PointerDown 并拖动
- **THEN** GameView 的任何容器 SHALL NOT 发生整体上移或滚动
- **AND** PointerMove/PointerUp 事件 SHALL 因 `CapturePointer` 持续派发到拖拽源

### Requirement: 拖拽与预览必须互斥
GameScreen SHALL 在进入拖拽态时强制清除当前预览态（销毁 preview-layer 中的克隆卡）。预览态下 SHALL 允许直接对该卡发起拖拽（无需先单击退出预览）。

#### Scenario: 预览态下拖拽该卡
- **WHEN** 当前正在预览卡 A，玩家在卡 A 上 PointerDown 并移动 ≥ 10px
- **THEN** GameScreen SHALL 进入拖拽态
- **AND** SHALL 同时销毁 preview-layer 中 A 的克隆卡

#### Scenario: 进入拖拽态强制取消预览
- **WHEN** 任意卡进入拖拽态
- **THEN** preview-layer SHALL 被清空

