# gameview-fan-layout-calc Specification

## Purpose
TBD - created by archiving change gameview-extract-hand-fan-subsystem. Update Purpose after archive.
## Requirements
### Requirement: FanLayoutCalc 必须为纯函数零 UI 依赖

`FanLayoutCalc` SHALL 是静态类，其方法 SHALL 仅依赖入参与 `HandFanLayoutOptions` 配置对象，SHALL NOT 引用 `VisualElement` / `UnityEngine.UIElements` 类型，SHALL NOT 读取或修改任何全局状态。

#### Scenario: EditMode 单元测试无需 Unity 运行时

- **WHEN** 测试代码在 EditMode 中调用 `FanLayoutCalc.ComputeSlot(...)` 或 `ComputeInsertSlot(...)`
- **THEN** SHALL 不依赖 `VisualElement` 实例，SHALL 仅传入数值参数与 `Rect` 数组

### Requirement: ComputeSlot 必须按现有公式输出扇形 transform

`FanLayoutCalc.ComputeSlot(slotIdx, slotCount, fanWidth, fanHeight, options)` SHALL 返回 `FanSlotAssignment { Left, Top, TranslateY, RotateDegrees }`，公式如下：

- `center = (slotCount - 1) / 2f`
- `offset = slotIdx - center`
- `spacing = slotCount > 1 ? min(options.MaxCardSpacing, (fanWidth - options.CardWidth) / (slotCount - 1)) : 0`
- `Left = fanWidth / 2f + offset * spacing - options.CardWidth / 2f`
- `Top = max(0f, fanHeight - options.CardHeight - options.HandFanBottomPadding)`
- `TranslateY = offset * offset * options.TranslateYCoeff`
- `RotateDegrees = offset * options.RotatePerStep`

#### Scenario: 5 张卡中间卡 offset=0

- **WHEN** `ComputeSlot(slotIdx=2, slotCount=5, fanWidth=800, fanHeight=280, defaultOptions)`
- **THEN** 返回 `RotateDegrees == 0`
- **AND** `TranslateY == 0`

#### Scenario: 5 张卡两端 offset 对称

- **WHEN** 在同一参数下分别 `ComputeSlot(0, 5, ...)` 与 `ComputeSlot(4, 5, ...)`
- **THEN** 两次结果的 `RotateDegrees` 互为相反数（绝对值相等）
- **AND** `TranslateY` 完全相等（抛物线对称）

#### Scenario: spacing 受 MaxCardSpacing 截断

- **WHEN** `fanWidth=2000, slotCount=2, options.MaxCardSpacing=120, options.CardWidth=150`
- **THEN** 实际 `spacing = min(120, (2000-150)/1) = 120`（受上限保护）

#### Scenario: slotCount=1 spacing 为 0

- **WHEN** `slotCount=1`
- **THEN** `spacing = 0`，`Left = fanWidth/2 - CardWidth/2`，无 offset 影响

#### Scenario: Top 不为负

- **WHEN** `fanHeight < options.CardHeight + options.HandFanBottomPadding`
- **THEN** `Top` SHALL 为 0（不为负）

### Requirement: ComputeInsertSlot 必须按"距最近卡 + 左/右半"算法返回插入位置

`FanLayoutCalc.ComputeInsertSlot(pointerPos, otherCardWorldBounds, activeIdxInVisualOrder)` SHALL 在剩余 N-1 张卡（不含 active）中找到距 `pointerPos.x` 最近的卡，按鼠标位于其左半 / 右半决定返回的插入槽位（基于 N 槽布局）。

- 总卡数 N ≤ 1 时返回 0
- 总卡数 N > 1 时：
  - 找最近卡的视觉索引 `bestVisualIdx`（在 `_cardItems` 中的位置）
  - `pointerPos.x < bestCenterX` → `slot = bestVisualIdx`
  - `pointerPos.x >= bestCenterX` → `slot = bestVisualIdx + 1`
  - 最终 `Clamp(slot, 0, N-1)`

#### Scenario: 鼠标在最近卡左半插在前面

- **WHEN** 三张卡分别中心在 `x=125, 225, 325`，`pointerPos.x = 220`，`activeIdx=-1`
- **THEN** 返回 `1`（鼠标在 x=225 卡左半，插入到该卡之前的槽 1）

#### Scenario: 鼠标在最近卡右半插在后面

- **WHEN** 同上但 `pointerPos.x = 240`
- **THEN** 返回 `2`（鼠标在 x=225 卡右半，插入到该卡之后的槽 2）

#### Scenario: 单卡返回 0

- **WHEN** N=1
- **THEN** SHALL 返回 0（无插入位置可言）

#### Scenario: 越界 Clamp

- **WHEN** N=3，鼠标在最右卡右半（计算结果会是 3）
- **THEN** SHALL Clamp 到 `N-1 = 2`

### Requirement: HandFanLayoutOptions 必须可注入

`HandFanLayoutOptions` SHALL 为可变 POCO（非 `static readonly`），所有字段（`DragThreshold`、`MaxCardSpacing`、`RotatePerStep`、`TranslateYCoeff`、`CardWidth`、`CardHeight`、`HandFanBottomPadding`、`ReboundDurationMs`）SHALL 有公开 setter 与默认值（与现有 `GameView` 常量一致）。

#### Scenario: 默认值与现有常量一致

- **WHEN** `new HandFanLayoutOptions()`
- **THEN** `DragThreshold=10f`、`MaxCardSpacing=120f`、`RotatePerStep=3f`、`TranslateYCoeff=3.5f`、`CardWidth=150f`、`CardHeight=230f`、`HandFanBottomPadding=20f`、`ReboundDurationMs=160`

#### Scenario: 测试可注入自定义参数

- **WHEN** 测试构造 `new HandFanLayoutOptions { MaxCardSpacing = 0f }` 并传给 `ComputeSlot`
- **THEN** `spacing` 计算 SHALL 为 0，所有卡叠加在 `fanWidth/2 - CardWidth/2` 位置
