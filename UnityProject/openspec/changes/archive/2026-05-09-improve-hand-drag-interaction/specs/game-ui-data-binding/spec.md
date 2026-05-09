## MODIFIED Requirements

### Requirement: 手牌必须支持拖拽出牌

GameScreen SHALL 在玩家 PointerDown 后通过 `CapturePointer` 捕获指针，并在 PointerMove 累计位移超过 10px 时进入拖拽态。进入拖拽态 SHALL 创建跟随鼠标的 ghost 卡片、将被拖卡完全从扇形布局中移除（不参与剩余卡的 transform 计算）、显示 `drop-zone`。拖拽态下，剩余 N−1 张卡 SHALL 按 N−1 张的扇形参数实时重新计算 transform，且这些卡 transform 变更 SHALL NOT 启用 USS transition（避免跟手延迟）。释放（PointerUp）时若指针位于 `drop-zone` 内 SHALL 调用 `ViewModel.UseCard(handIndex)`；若指针不在 `drop-zone` 也不在 `hand-fan` 内 SHALL 启动协同回弹（见独立 Scenario）。整个过程 SHALL 通过 `ReleasePointer` 释放捕获。

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

#### Scenario: 释放在 drop-zone 内出牌
- **WHEN** 拖拽态期间 PointerUp 且指针位于 `drop-zone` 的 worldBound 内
- **THEN** GameScreen SHALL 调用 ViewModel.UseCard(handIndex)
- **AND** SHALL 立即销毁 ghost，不做回弹动画

#### Scenario: 释放在中间地带回弹（既不在 drop-zone 也不在 hand-fan）
- **WHEN** 拖拽态期间 PointerUp 且指针既不在 `drop-zone.worldBound` 内也不在 `hand-fan.worldBound` 内
- **THEN** ghost 的 left/top SHALL 通过 0.15s USS transition 过渡到拖拽前被拖卡所在的扇形位置（N 张布局下的目标位置）
- **AND** 剩余 N−1 张卡 SHALL 同时（同一帧切到回弹模式）以 0.15s USS transition 协同动回 N 张布局（被拖卡重新占据原槽位）
- **AND** 协同动画期间，所有参与卡 SHALL 临时启用 transition；动画结束后 SHALL 移除临时 transition 类
- **AND** 过渡完成后 ghost SHALL 被销毁
- **AND** SHALL NOT 调用 ViewModel.UseCard

#### Scenario: 拖拽期间不触发滚动或界面位移
- **WHEN** 玩家在卡牌上 PointerDown 并拖动
- **THEN** GameView 的任何容器 SHALL NOT 发生整体上移或滚动
- **AND** PointerMove/PointerUp 事件 SHALL 因 `CapturePointer` 持续派发到拖拽源

## ADDED Requirements

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

### Requirement: 拖拽中其他卡的过渡控制

GameScreen SHALL 在拖拽进行中（含 Detached、InsertSlot、OverDropZone 子态）保证 `_handFan` 内剩余卡的 inline transform 变更 **不** 触发 USS transition；仅在松手回弹时才临时启用 transition。该机制 SHALL 通过给参与回弹的 `.card-item` 添加临时类（如 `card-item--rebounding`）实现，回弹动画结束（约 0.15s）后 SHALL 移除该类。

#### Scenario: 拖拽中重排无 transition
- **WHEN** 拖拽态期间剩余卡的 transform 因子态切换或插槽变化被改写
- **THEN** 这些卡的 transform 变更 SHALL 立即生效（无 USS transition 平滑过渡）

#### Scenario: 松手回弹临时启用 transition
- **WHEN** 拖拽态结束且需要回弹（中间地带松手）
- **THEN** GameScreen SHALL 给参与回弹的所有 `.card-item` 添加 `card-item--rebounding` 类
- **AND** SHALL 写入目标 transform 触发 0.15s USS transition
- **AND** SHALL 在约 0.15s 后移除 `card-item--rebounding` 类
- **AND** ghost SHALL 同样使用 `card-ghost--rebounding` 启用 transition 后销毁
