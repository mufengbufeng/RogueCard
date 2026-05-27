## ADDED Requirements

### Requirement: UguiDragSurface 必须用与 PointerEventData.position 同坐标空间的矩形做命中检测

`UguiDragSurface` 实现 `IDragSurface.DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound(int)` 时，SHALL 返回与 `CardDragController.OnPointerMove(pointerId, pos)` 入参 `pos`（即 `UnityEngine.EventSystems.PointerEventData.position`，单位为屏幕像素）处于**同一坐标空间**的 `Rect`。具体地，SHALL NOT 直接使用 `RectTransform.GetWorldCorners` 得到的世界坐标矩形；MUST 将世界角通过 `RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldCorner)` 转换为屏幕像素坐标后再构造 `Rect`，其中 `canvas` 为 `_dropZone` 父链上最近的 `Canvas` 组件。

#### Scenario: Screen Space - Camera 画布下命中检测使用屏幕像素

- **WHEN** `_dropZone` 所在 Canvas `renderMode == ScreenSpaceCamera` 且 `worldCamera != null`
- **THEN** `DropZoneWorldBound` 返回的 `Rect` 的四角坐标 SHALL 与 `RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, dropZoneCorner)` 在同一像素值域
- **AND** 当 `PointerEventData.position` 落在 drop-zone 视觉范围内时，`Rect.Contains(pos)` SHALL 返回 `true`

#### Scenario: Screen Space - Overlay 画布下命中检测正确（Overlay 兼容）

- **WHEN** `_dropZone` 所在 Canvas `renderMode == ScreenSpaceOverlay`（`worldCamera` 可能为 null）
- **THEN** `DropZoneWorldBound` 返回的 `Rect` SHALL 与指针屏幕像素位置在同一坐标空间
- **AND** `RectTransformUtility.WorldToScreenPoint(null, worldCorner)` 在 Overlay 下返回 `worldCorner.xy` 等价于世界坐标（已是屏幕像素）的行为 SHALL 被使用

#### Scenario: World Space 画布下命中检测使用投影后的屏幕像素

- **WHEN** `_dropZone` 所在 Canvas `renderMode == WorldSpace` 且 `worldCamera != null`
- **THEN** `DropZoneWorldBound` 返回的 `Rect` 的四角 SHALL 是世界角经 `worldCamera` 投影后的屏幕像素

#### Scenario: Canvas 不可获取时返回零矩形不抛异常

- **WHEN** `_dropZone == null` 或 `_dropZone.GetComponentInParent<Canvas>() == null`
- **THEN** `DropZoneWorldBound` SHALL 返回 `Rect.zero`
- **AND** SHALL NOT 抛出 `NullReferenceException`
- **AND** `DropZoneAvailable` SHALL 已经返回 `false`，避免进入命中检测路径

#### Scenario: HandFan 与 Card 矩形遵循同一坐标空间约定

- **WHEN** `HandFanWorldBound` 或 `GetCardWorldBound(idx)` 被调用
- **THEN** 返回 `Rect` SHALL 使用与 `DropZoneWorldBound` 相同的屏幕像素坐标空间
- **AND** `CardDragController.ComputeInsertSlot` 使用这些矩形与屏幕像素 `pointerPos` 比较时 SHALL 得到与人眼对齐的视觉命中结果

### Requirement: UguiDragSurface 必须用屏幕像素坐标定位拖拽 ghost

`UguiDragSurface.UpdateGhostPosition(Vector2 pos)` 和 `CreateGhost(int sourceCardIdx, Vector2 pos)` SHALL 把入参 `pos`（屏幕像素）通过 `RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, pos, canvas.worldCamera, out localPoint)` 转换为 `_previewLayer` 的本地坐标后赋给 `ghost.GetComponent<RectTransform>().localPosition` 的 X/Y 分量（Z 保持原值）。SHALL NOT 把屏幕像素 `pos` 直接赋给 `rect.position`（在 Screen Space - Camera / World Space 下会把 ghost 丢到错误的世界坐标导致渲染不可见）。SHALL NOT 用 `anchoredPosition = localPoint`（当 ghost 继承源卡的 anchor≠(0.5,0.5) 或 `_previewLayer.pivot≠(0.5,0.5)` 时，anchor 反推会引入与 `localPoint` 不一致的偏移）。

#### Scenario: Screen Space - Camera 下 ghost 跟手且对齐指针

- **WHEN** Canvas `renderMode == ScreenSpaceCamera`，玩家拖动卡牌
- **THEN** ghost 视觉位置 SHALL 实时跟随屏幕指针位置
- **AND** ghost 在 `_previewLayer` 中的 `localPosition.xy` SHALL 等于 `ScreenPointToLocalPointInRectangle(_previewLayer, pos, canvas.worldCamera)` 的输出
- **AND** ghost 的视觉中心（pivot=(0.5,0.5)）SHALL 与屏幕指针对齐，不允许出现可见的位置偏移

#### Scenario: Overlay 下 ghost 跟手

- **WHEN** Canvas `renderMode == ScreenSpaceOverlay`
- **THEN** 上述同一逻辑用 `worldCamera == null` 仍然正确（`ScreenPointToLocalPointInRectangle` 文档保证 Overlay 下传 `null` camera 等价于直接换算）
- **AND** ghost SHALL 跟手

#### Scenario: CreateGhost 时重置 ghost 的 anchor/pivot/rotation

- **WHEN** `CreateGhost(sourceCardIdx, pos)` 被调用
- **THEN** ghost 的 `RectTransform.anchorMin` / `anchorMax` SHALL 被显式重置为 `(0.5, 0.5)`
- **AND** ghost 的 `RectTransform.pivot` SHALL 被显式重置为 `(0.5, 0.5)`
- **AND** ghost 的 `RectTransform.localRotation` SHALL 被显式重置为 `Quaternion.identity`（清除手牌扇形旋转）
- **AND** 这些重置 SHALL 发生在 `UpdateGhostPosition` 首次定位之前

#### Scenario: ghost 父级保持为 PreviewLayer

- **WHEN** `UpdateGhostPosition` 被调用
- **THEN** ghost 的 `RectTransform.parent` SHALL 是 `_previewLayer`
- **AND** `sizeDelta` SHALL 保持配置的 `CardWidth × CardHeight`

### Requirement: UguiDragSurface 必须在构造时缓存 Canvas 引用避免每帧反向遍历

`UguiDragSurface` SHALL 在构造时通过 `_dropZone.GetComponentInParent<Canvas>()` 获取 Canvas 引用并存为只读字段，所有命中检测与 ghost 定位 SHALL 使用此缓存引用，SHALL NOT 在每次 `Get`-`bound` / `UpdateGhostPosition` 调用时重新遍历父级。

#### Scenario: 构造完成后单次获取 Canvas

- **WHEN** `UguiDragSurface` 构造函数被调用且 `_dropZone != null`
- **THEN** SHALL 调用 `_dropZone.GetComponentInParent<Canvas>()` 一次并缓存结果
- **AND** 后续 `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound` / `UpdateGhostPosition` / `CreateGhost` 调用 SHALL 不再触发 `GetComponentInParent`

#### Scenario: 构造时 dropZone 缺失时缓存为 null

- **WHEN** 构造时 `_dropZone == null`
- **THEN** Canvas 缓存字段 SHALL 为 `null`
- **AND** 后续命中检测 SHALL 触发 `DropZoneAvailable == false` 短路逻辑，不进入坐标变换路径

### Requirement: IDragSurface 命中矩形 getter 必须在文档中声明坐标空间契约

`IDragSurface.DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound(int)` 的 XML 文档 SHALL 显式声明"返回矩形 MUST 与 `CardDragController.OnPointerMove(pointerId, pos)` 入参 `pos` 处于同一坐标空间；UGUI 实现层为屏幕像素"，避免后续实现误用 `RectTransform.GetWorldCorners` 引入坐标空间错配。SHALL NOT 仅依赖方法命名（`WorldBound`）传达约束。

#### Scenario: 三个 getter 文档都声明坐标空间约束

- **WHEN** 检视 `IDragSurface.cs` 中 `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound` 的 XML 注释
- **THEN** 每个 getter 的 `<summary>` 或 `<remarks>` SHALL 包含"与 `OnPointerMove` 入参 `pos` 同坐标空间"或语义等价的措辞
- **AND** SHALL 明确指出"UGUI 实现层为屏幕像素，不可直接使用 `RectTransform.GetWorldCorners`"
