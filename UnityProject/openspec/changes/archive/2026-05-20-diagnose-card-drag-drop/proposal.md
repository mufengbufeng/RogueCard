## Why

战斗 UI 从 UI Toolkit 迁移到 UGUI 后（commit 6ecee8c），玩家无法通过拖拽卡牌到出牌区域出牌。在没有运行时证据前直接修代码风险很高，本变更为后续修复 `fix-card-drag-drop-not-played` 提供权威的现场日志，把状态机的真实走向、坐标取值和 Canvas 配置一次性打到 Console，便于精确定位断点（指针未到达 / 判定坐标错配 / 分支错走 / 回弹被误触发等）。

## What Changes

- 在 `CardDragController.cs` 的拖拽流程上挂 5 个**临时性**诊断打点（统一 `[CardDrag]` 前缀），用 `EF.Debugger.Log.Info`：
  - **PointerDown 入口**：`handIdx`、`visualIdx`、`pointerId`、`pos`、当前 `Phase`、当前 `_state`
  - **PointerMove 入口**：`pointerId`、`pos`、`dist = (pos - _pointerStartPos).magnitude`、是否达阈值
  - **EnterDragging 入口**：`pos`、`_activeVisualIndex`、`CardCount`
  - **DetermineDragMode**（最关键，一次性 dump 坐标空间证据）：
    - `pointerPos`
    - `DropZoneAvailable` / `DropZoneWorldBound` 的 xMin/xMax/yMin/yMax
    - `HandFanWorldBound` 的 xMin/xMax/yMin/yMax
    - `_dropZone` 的父级 `Canvas.renderMode`（取自 `Canvas.GetComponentInParent`）与 `Canvas.worldCamera` 是否为 null
    - 最终返回的 `DragMode`
  - **OnPointerUp 三个 case 分支**：分别打出 `→ OverDropZone branch` / `→ InsertSlot branch` / `→ Detached branch (rebound)`，外加分支内调用的 callback 名
- 打点必须是**可回退的**：所有新增 `Log.Info` 集中在 `CardDragController.cs` 内，归档前由 `fix-card-drag-drop-not-played` 完成验证后统一清除。
- 不修改任何业务行为、状态转移规则或公共接口。不新增字段、不改方法签名。

## Capabilities

### New Capabilities

- `card-drag-diagnostic-logging`：定义"诊断阶段卡牌拖拽流程必须输出哪些字段、在哪些状态转移点输出"，作为本变更的实施约束。归档时与本变更一同归档/移除，不进入长期 spec。

### Modified Capabilities

（无 — 不改任何现有需求）

## Impact

- **代码**：`Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/CardDragController.cs`（仅 `Log.Info` 新增，逻辑不动）
- **测试**：现有 EditMode 测试不受影响；新增日志不写入 mock 接口，不破坏 `MockDragSurface` 契约
- **运行时行为**：无副作用（`Log.Info` 在 Release 配置下可由 `EF.Debugger.Log` 等级控制）
- **依赖**：无新依赖
- **临时性**：本变更归档后，`card-drag-diagnostic-logging` spec 同步删除；日志在后续 fix change 完成验证后由该 fix change 清除
