# gameview-card-preview Specification

## Purpose
TBD - created by archiving change gameview-extract-hand-fan-subsystem. Update Purpose after archive.
## Requirements
### Requirement: CardPreviewController 必须通过 IPreviewSurface 间接操作 UI

`CardPreviewController` SHALL 通过 `IPreviewSurface` 接口执行所有预览相关 UI 副作用：克隆模板、计算定位坐标（`hand-fan` → `preview-layer` 局部坐标转换）、添加/移除克隆元素到 `preview-layer`。SHALL NOT 直接持有 `VisualElement` 引用。

#### Scenario: 测试用 mock IPreviewSurface

- **WHEN** 测试用 `MockPreviewSurface` 调 `TogglePreview(2, sourceCardSnapshot)` / `ExitPreview()`
- **THEN** 调用序列 SHALL 通过 mock 记录可断言

### Requirement: CardPreviewController 必须支持单击切换预览态

`CardPreviewController` SHALL 维护 `_previewSource` 字段（当前正在预览的源卡引用）。当 `TogglePreview(handIdx, source)` 被调用：

- 若当前已在预览且 `_previewSource == source`（同一张卡）→ `ExitPreview()`
- 否则 → 先 `ExitPreview()`（若已在预览）再 `EnterPreview(handIdx, source)`

`source` SHALL 用引用判断而非索引比较（reorder 后索引不可靠）。

#### Scenario: 单击同卡退出预览

- **WHEN** 当前预览卡 A，再次 `TogglePreview(handA, sourceA)`
- **THEN** SHALL 调 `ExitPreview()`（销毁克隆）
- **AND** `_previewSource` SHALL 置 null

#### Scenario: 单击别卡切换预览

- **WHEN** 当前预览卡 A，调 `TogglePreview(handB, sourceB)`
- **THEN** SHALL 先 `ExitPreview()` 销毁 A 克隆
- **AND** 再 `EnterPreview(handB, sourceB)` 创建 B 克隆

#### Scenario: reorder 后仍能识别同卡

- **WHEN** 预览卡 A，A 的视觉索引在 reorder 后从 2 变为 0，再次 `TogglePreview(handA, sourceA)`
- **THEN** SHALL 识别为同卡 `_previewSource == sourceA`，SHALL `ExitPreview()`

### Requirement: Active card preview must dismiss on non-card battle panel clicks

When a card preview is active, the hand fan UI SHALL allow the player to dismiss it by clicking any non-card area in the battle panel. Non-card dismissal SHALL call `CardPreviewController.ExitPreview()` and SHALL consume the pointer event so the same gesture does not activate another non-card control.

Card roots and their descendants SHALL be excluded from non-card dismissal. Clicking a card while preview is active SHALL continue to use `TogglePreview(handIdx, source)` semantics: the same card closes the preview, and a different card replaces the preview.

#### Scenario: Clicking battle panel backdrop closes active preview

- **WHEN** card A is currently previewed and the player clicks a battle panel area that is not any hand card
- **THEN** the preview for card A SHALL be removed
- **AND** the pointer event SHALL be consumed

#### Scenario: Clicking the same card still closes through toggle

- **WHEN** card A is currently previewed and the player clicks card A
- **THEN** non-card dismissal SHALL NOT run for that pointer event
- **AND** `TogglePreview(handA, sourceA)` SHALL close the preview

#### Scenario: Clicking a different card switches preview

- **WHEN** card A is currently previewed and the player clicks card B
- **THEN** non-card dismissal SHALL NOT run for that pointer event
- **AND** `TogglePreview(handB, sourceB)` SHALL remove card A's preview and create card B's preview

#### Scenario: Clicking a non-card control does not activate it while dismissing preview

- **WHEN** card A is currently previewed and the player clicks a non-card battle control such as the end-turn button
- **THEN** the preview SHALL close
- **AND** the control SHALL NOT receive the same pointer gesture as an activation

### Requirement: Active card preview must dismiss when hand card drag starts

When a card preview is active and the player starts dragging any hand card, the hand fan UI SHALL dismiss the active preview during the same pointer move that transitions the hand interaction into `Dragging`.

The preview SHALL NOT dismiss merely because a hand card received `PointerDown`. A hand card gesture that remains within `HandFanLayoutOptions.DragThreshold` and resolves as a click SHALL continue to use existing `TogglePreview(handIdx, source)` semantics.

#### Scenario: Pointer move crossing drag threshold closes active preview

- **WHEN** card A is currently previewed and the player presses a hand card then moves farther than `DragThreshold`
- **THEN** the hand interaction SHALL enter `Dragging`
- **AND** the active preview SHALL be removed during that drag-start pointer move

#### Scenario: Pointer down alone keeps preview until gesture is classified

- **WHEN** card A is currently previewed and the player presses a hand card without moving beyond `DragThreshold`
- **THEN** the active preview SHALL remain visible
- **AND** no drag ghost SHALL be created

#### Scenario: Threshold-limited click still uses preview toggle semantics

- **WHEN** card A is currently previewed and the player clicks a hand card with movement less than or equal to `DragThreshold`
- **THEN** the gesture SHALL be handled as a card click
- **AND** preview behavior SHALL be determined by `TogglePreview(handIdx, source)`

### Requirement: CardPreviewController 必须按 hand-fan 局部坐标定位预览克隆

`CardPreviewController.EnterPreview` SHALL 通过 `IPreviewSurface` 完成以下定位：

1. 取源卡未旋转 layout 顶部中心（`source.layout.center.x`、`source.layout.yMin`）
2. `hand-fan.LocalToWorld` 转世界坐标
3. `preview-layer.WorldToLocal` 转 preview-layer 局部坐标
4. 设置克隆卡 `style.left = localX - CardWidth/2`、`style.top = localY - CardHeight`

克隆卡 SHALL 应用 CSS 类 `card-item--preview`（USS 已定义 `transform-origin: 50% 100%` + `scale: 1.6`），SHALL `pickingMode = Ignore`。

#### Scenario: 克隆卡的左上锚点

- **WHEN** 源卡在 `hand-fan` 内 layout 顶部中心 = `(150, 50)`
- **THEN** 克隆卡 `style.left` SHALL 为 `localX - CardWidth/2`
- **AND** `style.top` SHALL 为 `localY - CardHeight`

#### Scenario: 克隆卡不抢点击

- **WHEN** 克隆卡显示在 preview-layer
- **THEN** `clone.pickingMode` SHALL 为 `PickingMode.Ignore`

#### Scenario: 克隆卡 UI 文本与源卡一致

- **WHEN** 源卡 `card-name="火球"`、`card-cost="2"`
- **THEN** 克隆卡 `card-name` Label `text` SHALL 为 `"火球"`
- **AND** `card-cost` Label `text` SHALL 为 `"2"`

### Requirement: CardPreviewController 必须支持 ClearAllHoverState

`CardPreviewController.EnterPreview` SHALL 在创建克隆前清掉所有卡的 `card-item--hovering` 类（通过上层回调 `IPreviewSurface.ClearAllHoverState()`），避免预览时 hover 类残留。

#### Scenario: 进入预览清掉 hover 类

- **WHEN** 当前某卡有 `card-item--hovering` 类，玩家单击该卡进入预览
- **THEN** `IPreviewSurface.ClearAllHoverState()` SHALL 被调用

### Requirement: CardPreviewController 必须支持 Dispose

`CardPreviewController` SHALL 实现 `IDisposable`，`Dispose()` SHALL 调 `ExitPreview()` 销毁残留克隆，多次调用安全（幂等）。

#### Scenario: 处于预览态时 Dispose 销毁克隆

- **WHEN** 当前正预览卡 A 时调 `controller.Dispose()`
- **THEN** A 的预览克隆 SHALL 被移除
- **AND** `_previewSource` SHALL 置 null

#### Scenario: 重复 Dispose 安全

- **WHEN** 同一控制器实例的 `Dispose()` 被调用两次
- **THEN** 第二次调用 SHALL NOT 抛出异常
