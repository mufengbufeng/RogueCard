## Why

`GameView` 中"手牌交互"子系统是当前文件的最大痛点：800+ 行集中处理扇形布局公式、`CardInteractionState` 四态状态机、`DragMode` 三子态、`InsertSlot` 占位卡、ghost 创建/回弹、单击预览克隆与 hover 类切换。这块逻辑相互耦合却又缺少单元测试，每次调拖拽手感（如 `RebounDuration` / `DragThreshold` / 占位卡时序）都需要靠手动战斗回归——成本高且容易回归。

本 change 抽出一整个手牌子系统：单卡视图、扇形布局纯函数、拖拽状态机、预览控制器，并为可单测的纯逻辑（布局公式、`ComputeInsertSlot` 命中、状态转移）补 EditMode 用例。`SelectingTarget` 子态由本 change 提供"出口事件"（`CardDroppedOnZone(handIdx, needsManualTarget)`），具体跨模块编排留给 change 3。

## What Changes

- **新增** `CardItemView` 单卡视图，封装名称/费用 Label 设置、hover 类切换、PointerDown/Enter/Leave 事件转发到 `HandFanView`
- **新增** `FanLayoutCalc` + `HandFanLayoutOptions`：扇形 transform 公式（`CardWidth` / `CardHeight` / `MaxCardSpacing` / `RotatePerStep` / `TranslateYCoeff` / `HandFanBottomPadding` 全部参数化），与 `ComputeInsertSlot` 命中函数。两者均为纯函数，零 UI 依赖，可单测
- **新增** `CardDragController`：保留现有 `enum CardInteractionState` 状态字段但重构为"每态一组方法"组织，依赖 `IDragSurface` 接口（包装 ghost 创建、其他卡 transform 应用、`worldBound` 命中等 UI 操作）便于 mock
- **新增** `CardPreviewController`：单击放大克隆 / 切换 / 退出，依赖 `IPreviewSurface` 接口（包装 preview-layer 容器与坐标转换）
- **新增** `HandFanView`：装配 `CardItemView × N` + `FanLayoutCalc` + `CardDragController` + `CardPreviewController`，订阅 `IHandContext.Hand.Changed` 重建，对外暴露事件 `CardDroppedOnZone(int handIdx, bool needsManualTarget)` / `CardDragCancelled(int handIdx)` / `CardClicked(int handIdx)`
- **新增** `IHandContext` 切片接口（`Hand` / `Phase` / `UseCard` / `CardPlayFailed` 等手牌相关字段命令事件）
- **保留** `SelectingTarget` 流程在 `GameView` 中（change 3 才抽出 `TargetSelector`）；`HandFanView` 通过 `CardDroppedOnZone(needsManualTarget=true)` 把决策权交给上层
- **测试** 新增 EditMode 用例：`FanLayoutCalc` 计算公式、`ComputeInsertSlot` 命中规则（鼠标在最近卡左/右半）、`CardDragController` 状态机转移（用 mock 的 `IDragSurface` 验证 PointerDown→Move→Up 的状态序列与回调时序）

## Capabilities

### New Capabilities

- `gameview-fan-layout-calc`: 扇形布局公式与插入槽位命中规则的纯函数契约，可单测
- `gameview-hand-fan-view`: 手牌容器装配子模块（`CardItemView` 集合 + 布局应用 + sibling 顺序同步 + 几何变化响应）+ `IHandContext` 切片
- `gameview-card-drag-state-machine`: 拖拽四态状态机（Idle/Hovering/Previewing/Dragging）+ 三子态（Detached/InsertSlot/OverDropZone），依赖 `IDragSurface` 接口
- `gameview-card-preview`: 单击放大预览的克隆/切换/退出契约，依赖 `IPreviewSurface` 接口

### Modified Capabilities

- `game-ui-data-binding`: 把"手牌扇形布局"、"手牌点击放大预览"、"手牌悬停抬升"、"手牌拖拽到 drop-zone 转发 UseCard"、"单击手牌不调用 ViewModel"等要求迁出，改写为"`GameView` / `HandFanView` 通过子模块装配实现，行为契约见新 capability"

## Impact

- **框架层** 不变
- **游戏层** (`Assets/GameScripts/HotFix/GameLogic/UI/Game/`) — 新增 ~10 个文件：
  - `Context/IHandContext.cs`
  - `Views/CardItemView.cs`、`HandFanView.cs`
  - `Layout/FanLayoutCalc.cs`、`HandFanLayoutOptions.cs`
  - `Drag/CardDragController.cs`、`IDragSurface.cs`、`DragSurface.cs`（生产实现）
  - `Drag/CardPreviewController.cs`、`IPreviewSurface.cs`、`PreviewSurface.cs`
  - `GameView.cs` 减少约 800 行（从 1100 降至约 400）
- **资源** 不变
- **测试** — 新增 `Tests/EditMode/UI/Game/Layout/FanLayoutCalcTests.cs`、`Drag/CardDragControllerTests.cs`、`Drag/CardPreviewControllerTests.cs`，配合 mock 的 `IDragSurface` / `IPreviewSurface`
- **风险** — 中-高。状态机重构最容易回归 `PointerCapture` 释放时机、`InsertSlot` 占位卡 USS transition baseline、`opacity 0` vs `visibility Hidden` 的 1 帧时序。需配套测试 + 手动验证完整拖拽场景（5 种典型路径）
