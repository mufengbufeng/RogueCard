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
- **THEN** ghost SHALL 立即销毁（不做飞回 transition；因 ghost 没有 rotate transform，与下方扇形卡牌旋转视觉不一致）
- **AND** 被拖卡 SHALL 立即恢复 `opacity = 1` 与 `pickingMode = Position`（无 fade-in，因 ghost 已销毁，必须立即可见以避免空白帧）
- **AND** 剩余 N−1 张卡 SHALL 以 0.15s transition 协同动回 N 张布局（被拖卡重新占据原槽位）
- **AND** 协同动画期间，所有参与卡 SHALL 临时启用 transition；动画结束后 SHALL 清除临时 transition 设置
- **AND** SHALL NOT 调用 `ViewModel.UseCard`

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

### Requirement: 手牌必须支持区域内拖拽调整顺序（UI 层）

GameScreen SHALL 在玩家拖拽手牌进入 `hand-fan.worldBound` 内（且不在 `drop-zone` 内）时进入"区域内插槽"子态：剩余 N−1 张卡 SHALL 按"留出一个空槽"的 N 张布局排列，空槽位置 SHALL 显示一张半透明 `card-item--insert-slot` 占位卡（视觉上替代被拖卡）；插槽位置 SHALL 由指针 x 坐标按"距最近卡 + 左右半判定"算法每帧实时计算。释放（PointerUp）时若指针位于 `hand-fan.worldBound` 内（且不在 `drop-zone` 内）SHALL 调整 GameScreen 内部 `_cardItems` 列表顺序（**仅 UI 层**），按新顺序应用扇形 transform，并销毁 ghost 和占位卡。GameScreen SHALL NOT 因为顺序调整调用任何 `ViewModel` 命令、SHALL NOT 修改 `ViewModel.Hand` 或 `GameModel.Hand`。

#### Scenario: 进入 hand-fan 区域显示插槽
- **WHEN** 拖拽态期间指针进入 `hand-fan.worldBound` 内（且不在 `drop-zone` 内）
- **THEN** GameScreen SHALL 在 `hand-fan` 内创建一张克隆卡作为占位（`card-item--insert-slot` 类，opacity 较低，`pickingMode = Ignore`）
- **AND** 剩余 N−1 张卡 SHALL 按 N 槽留空一格的扇形布局排列（被跳过的槽 = 当前插槽位置）
- **AND** 插槽位置 SHALL 由"剩余卡中距指针 x 最近的一张 + 鼠标在其左半还是右半"决定

#### Scenario: 区域内移动时插槽位置实时更新
- **WHEN** 在区域内拖拽态期间 PointerMove，且新计算出的插槽位置与当前插槽位置不同
- **THEN** 占位卡 SHALL 移动到新槽位（按 N 槽留空规则）
- **AND** 剩余 N−1 张卡 SHALL 按新的"被跳过槽"重新计算 transform
- **AND** 该过程 SHALL NOT 启用 transition

#### Scenario: 离开 hand-fan 区域回到脱离态
- **WHEN** 拖拽态期间指针从 `hand-fan` 内移出到中间地带或 `drop-zone`
- **THEN** GameScreen SHALL 销毁占位卡
- **AND** 剩余 N−1 张卡 SHALL 切回 N−1 张紧凑扇形布局

#### Scenario: 松手在 hand-fan 内调整顺序
- **WHEN** 拖拽态期间 PointerUp 且指针位于 `hand-fan.worldBound` 内（且不在 `drop-zone` 内）
- **THEN** GameScreen SHALL 将 `_cardItems` 列表中被拖卡从原位置移动到当前插槽位置（list reorder）
- **AND** SHALL 按新的 `_cardItems` 顺序对所有卡应用 N 张扇形布局（`RecomputeHandLayout`）
- **AND** SHALL 销毁占位卡和 ghost
- **AND** SHALL NOT 调用 ViewModel.UseCard、SHALL NOT 调用任何修改 `Hand` 的 ViewModel 命令
- **AND** SHALL NOT 修改 `ViewModel.Hand.Value` 或 `GameModel.Hand`

#### Scenario: 手牌只剩 1 张时区域内拖拽
- **WHEN** 手牌只有 1 张且玩家在该卡上发起拖拽并在 `hand-fan` 内移动
- **THEN** 占位卡 SHALL 显示在 `slotIndex = 0` 位置
- **AND** 松手 SHALL 等同于回到原位（视觉无变化）
- **AND** SHALL NOT 调用 ViewModel.UseCard

#### Scenario: UI 层调整不持久化
- **WHEN** 玩家在 `hand-fan` 内调整完顺序后，发生任意触发 `ViewModel.Hand.Changed` 的操作（如出牌、抽牌、回合结束）
- **THEN** GameScreen 的 `RefreshCards` SHALL 按 `ViewModel.Hand.Value` 中的 Model 顺序重建手牌
- **AND** 上一次 UI 层调整的视觉顺序 SHALL 被重置（这是 UI-only 重排的预期副作用）

#### Scenario: 调整顺序后再次操作仍指向被拖卡
- **WHEN** 玩家在 `hand-fan` 内调整一张卡 X 的视觉位置后，再次拖拽、单击预览、或拖到 drop-zone 出牌操作 X
- **THEN** GameScreen SHALL 区分两种索引：visual index（`_cardItems.IndexOf(source)` 实时查找，用于 layout 跳过、reorder、回弹定位）与 hand index（PointerDown 注册时 closure 捕获 `ViewModel.Hand` 中的位置，用于 `UseCard` / `EnterPreview` / `GetHandCardAt` / `EnterSelectingTarget`）
- **AND** 视觉操作 SHALL 用 visual index、数据操作 SHALL 用 hand index，两个语义 SHALL NOT 共用同一变量（避免 reorder 后操作错张卡）
- **AND** 单击预览的"是否同一张卡"判断 SHALL 用 source `VisualElement` 引用比较，而非索引比较

### Requirement: 拖拽中其他卡的过渡控制

GameScreen SHALL 在拖拽进行中（含 Detached、InsertSlot、OverDropZone 子态）保证 `_handFan` 内剩余卡的 inline transform 变更 **不** 触发 USS transition；仅在松手回弹时才临时启用 transition。该机制 SHALL 通过给每张 `.card-item` 设置 inline `transitionDuration`（拖拽中 = 0s，回弹时 = 0.15s）实现 — 之所以用 inline style 而非 USS class（如曾经设计的 `card-item--no-transition` / `card-item--rebounding`），是因为 USS class 切换与 inline transform 写入在同一帧时 transition baseline 失效，导致回弹首帧 rotate 错乱；inline style 立即生效避免该 quirk。回弹动画结束后 SHALL 通过 `StyleKeyword.Null` 清除 inline 设置，恢复 USS 默认值。

#### Scenario: 拖拽中重排无 transition
- **WHEN** 拖拽态期间剩余卡的 transform 因子态切换或插槽变化被改写
- **THEN** 这些卡的 inline `transitionDuration` SHALL 为 0s，transform 变更 SHALL 立即生效（无平滑过渡）

#### Scenario: 松手回弹启用 transition
- **WHEN** 拖拽态结束且需要回弹（中间地带松手）
- **THEN** GameScreen SHALL 通过 inline 设置每张 `.card-item` 的 `transitionDuration` 为 0.15s
- **AND** SHALL 写入目标 transform（N 张布局）触发 0.15s 平滑过渡
- **AND** SHALL 在约 0.15s 后通过 `StyleKeyword.Null` 清除 inline `transitionDuration`，恢复 USS 默认值
- **AND** ghost 在松手瞬间 SHALL 立即销毁，**不**参与 transition

#### Scenario: 被拖卡的可见性控制
- **WHEN** 进入拖拽态
- **THEN** 被拖卡 SHALL 通过 `opacity = 0` + `pickingMode = Ignore` 隐藏（而非 `visibility = Hidden`，避免 layout 重算干扰 transition baseline）
- **WHEN** 松手回弹（中间地带）
- **THEN** 被拖卡 SHALL 立即恢复 `opacity = 1` + `pickingMode = Position`，与 ghost 销毁同步衔接

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

