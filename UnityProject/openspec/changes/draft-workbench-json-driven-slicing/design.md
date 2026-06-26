## Context

Draft Workbench 已经具备 AI Vision → `ui_structure.json` → UGUI descriptor → 预览/应用的结构生成流程。当前 Auto Slice 相关规划和已有代码更偏向 ComfyUI/SAM3 先对整张设计图做 region 检测，再由 Unity 根据 manifest 或 mask fallback 生成 bbox 并裁图；这会让切图边界受 AI 分割波动影响，不能稳定复用 `ui_structure.json` 中已经识别出的 UI 元素位置和语义。

现有代码中，`UiStructureSchema` 已承载 `asset`、`marker`、`spriteHint`、`regionId` 等字段，`UiStructureConverter` 也已经能把元素 `position/size` 转成 descriptor source bounds，并把部分视觉语义传递到 `UiNodeVisuals`。因此新的主流程不需要让 ComfyUI 决定“切哪里”，而应让 JSON/descriptor 决定切图范围，ComfyUI 只负责把 raw crop 变成更可用的透明 PNG / refined PNG。

## Goals / Non-Goals

**Goals:**

- 将 Draft Workbench Auto Slice 默认主流程改为 JSON-driven：从 `ui_structure.json` 或当前 `_previewChanges` 中确定性提取待切图 region。
- 在不依赖 ComfyUI 的情况下，根据设计图和 JSON/descriptor bounds 先生成 raw PNG，并允许用户审查、排除和修改 marker。
- 将选中的 raw PNG 作为输入交给 ComfyUI refinement workflow，生成可用切图，再导入 Unity UI Sprite。
- 保留现有 Sprite 匹配、Apply、Revert 服务作为后续阶段可复用能力，但 MVP 不扩大应用范围。
- 保持所有新增代码和配置 Editor-only，不影响 Runtime、HotFix、YooAsset runtime 或玩家构建。

**Non-Goals:**

- 不在 MVP 中让 ComfyUI/SAM3 作为默认 region/bbox 检测来源。
- 不在 MVP 中实现无人审查的一键覆盖 prefab。
- 不在 MVP 中自动配置 YooAsset Collector、SpriteAtlas 或资源打包规则。
- 不在 MVP 中实现多设计图批处理、复杂 bbox 拖拽编辑或视觉 diff。
- 不要求 raw PNG 成为长期项目资产；raw crop 可以作为中间产物保留在缓存目录。

## Decisions

### 1. 默认 region 来源使用 descriptor source bounds，而不是 ComfyUI manifest

Auto Slice 首先从当前 Draft Workbench 预览节点提取 region：筛选带 `UnityEngine.UI.Image` 组件、具备 `HasSourceBounds` 且 bounds 有效的 descriptor，并复制 `Name`、`SourceBounds`、`SourceCanvasSize`、`Visuals.AssetMarker`、`Visuals.SpriteHint`、`Visuals.RegionId` 作为切图语义。若当前没有预览节点但 JSON 文本可解析，则先通过现有 `UiStructureConverter` 生成 descriptors，再提取 region。

这样做的优势是：同一张设计图和同一份 JSON 会稳定生成相同 region，且节点名称去重、层级路径、坐标换算已由 converter 统一处理。

**Alternatives considered:** 继续以 ComfyUI/SAM3 manifest 或 mask fallback 作为默认来源。该方式适合未知图像分割，但会弱化 JSON 结构识别结果，且难以保证按钮、icon、panel 等 UI 元素边界稳定。

### 2. eligible region 以“可产生 Image Sprite 的节点”为默认筛选条件

默认只提取会映射到 `UnityEngine.UI.Image` 的节点，例如 image、button、带颜色/视觉资源的 rect/panel 类节点。text、纯 layout container、无有效 source bounds 的节点默认跳过，并在报告中记录原因。

这能避免把文本、布局容器或无视觉资源的结构节点误裁成 Sprite。后续可以增加 preset，例如只切 icon、只切 button/panel 背景，但 MVP 先使用一个通用筛选规则。

**Alternatives considered:** 按 JSON 树的每个节点都裁图。实现简单但会产生大量无用 PNG，且嵌套元素会互相包含，审查成本高。

### 3. bbox 坐标从 JSON canvas 映射到 Texture 像素坐标

`ui_structure.json` 的 `canvas.width/height` 或 descriptor `SourceCanvasSize` 是 source bounds 的坐标系。裁剪前需要按源 Texture 实际像素尺寸换算：当 canvas 与 Texture 尺寸比例一致时按 x/y 比例缩放；当比例明显不一致时仍允许生成，但必须在 region report 中标记警告，避免用户误以为切图精确。

**Alternatives considered:** 要求 JSON canvas 与 Texture 像素完全一致。这样最严格，但 AI Vision 可能会按缩放后的图输出 JSON，强制一致会降低工具可用性。

### 4. raw crop 和 refined sprite 分离存储

raw PNG 是中间产物，默认写入 `Library/DraftWorkbench/JsonRawCrops/<SourceHash>/` 或 Comfy cache 下的本次运行目录；refined PNG / final Sprite 写入 `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`。每条 region 记录 raw path、refined path、sprite asset path 和状态。

这样可以让项目资产目录只保留可引用的最终 Sprite，避免 raw crop、Comfy history、mask、debug overlay 污染 AssetDatabase。

**Alternatives considered:** raw 和 refined 全部写入 `Assets`。便于浏览，但会生成大量中间资产，增加版本控制和 AssetDatabase 噪音。

### 5. ComfyUI refinement 输入是单个 raw crop，而不是整张设计图

ComfyUI 阶段的输入从完整 design image 改为 raw crop PNG。workflow contract 是：给定一张 raw crop，输出一张 refined PNG，最好保持原尺寸和透明 padding；如果 workflow 输出尺寸不同，Draft Workbench 仍以原 region bounds 作为 UI 尺寸来源，并在结果中标记尺寸变化。

这能保证 ComfyUI 只负责视觉可用化，例如抠透明、清理背景、边缘 refinement，而不是重新解释 UI 布局。

**Alternatives considered:** 继续将整张设计图送入 ComfyUI，让 SAM3 决定分割对象。该方式会把“结构识别”和“图像处理”混在一起，不符合本变更的两阶段目标。

### 6. SAM3/manifest 检测保留为高级兜底，不进入默认 MVP

已有 ComfyUI/SAM3 manifest/mask fallback 可以保留，但 UI 上应明确标记为高级检测或后续能力。默认主按钮不应先触发整图检测，而应先触发 JSON raw crop。

**Alternatives considered:** 删除 SAM3 检测能力。这样可以减少复杂度，但现有工作和未来无 JSON 场景仍可能需要该能力，保留为非默认路径更稳妥。

### 7. Prefab 匹配和 Apply/Revert 复用现有服务

refined PNG 导入 Sprite 后，继续使用现有 `DraftSpriteAssignmentService` 和 `DraftSpriteApplyService` 进行匹配、批准、应用和回滚。MVP 的验收重点是 JSON → raw PNG → ComfyUI refined PNG → Sprite 导入；prefab 覆盖策略不在本变更中扩大。

**Alternatives considered:** 在同一变更中重做自动匹配和应用体验。这样会扩大风险，也会模糊本次核心问题：切图来源应先由 JSON 决定。

## Risks / Trade-offs

- **JSON canvas 与 Texture 像素比例不一致** → 使用比例缩放并在 report 中显示 scale 和 aspect mismatch 警告。
- **嵌套节点产生重叠 raw crop** → 默认只裁可产生 Image Sprite 的节点，并允许用户 include/ignore；后续再增加 preset。
- **raw crop 不经过 ComfyUI 时不够可用** → UI 明确区分 raw 和 refined，raw 只证明切分正确；最终 Sprite 默认来自 refined PNG。
- **ComfyUI workflow 输出尺寸变化** → 记录 raw/refined 尺寸，UI 尺寸仍以原 region bounds 为准，并提示用户该切图可能需要人工检查。
- **ComfyUI 未启动或配置错误** → raw crop 阶段不依赖 ComfyUI；refinement 失败不得清空已有 raw crop 和 region 审查数据。
- **生成资产数量增加** → raw crop 放入 Library 缓存，最终 Sprite 目录按 prefab/source hash 隔离。
- **与现有 SAM3 Auto Slice 规划冲突** → 将 SAM3 检测降级为高级兜底，避免默认路径同时存在两个 region 来源。

## Migration Plan

1. 保留现有 AI Vision、Parse & Preview、regular Apply/Revert 行为不变。
2. 新增 JSON-driven region extraction 和 raw crop 服务，并让 Auto Slice 默认入口先使用该服务。
3. 新增 raw crop 状态和 refined output 状态，不删除现有 segmentation manifest 模型；必要时扩展模型兼容 raw/refined 字段。
4. 将 ComfyUI runner 增加 crop refinement 模式，和整图 SAM3 detection 模式隔离。
5. 将 UI 文案和按钮从“开始智能切图 / Import Sprites”调整为“根据 JSON 生成原始切图 / 使用 ComfyUI 生成可用切图 / 导入 Sprite”。
6. 如果新流程出现问题，可回退到只生成 raw crop 或重新启用高级 SAM3 检测，不影响原 Draft Workbench 结构生成流程。

## Open Questions

- raw PNG 默认是否完全放在 `Library`，还是需要提供“导出 raw PNG 到 Assets”的显式选项？
- 默认 eligible 规则是否应包含 overlay / panel-like container，还是仅限 image、button、rect？
- ComfyUI refinement workflow 是否能保证输出与 raw crop 同尺寸；如果不能，是否需要 Unity 侧自动 pad/resize 回原尺寸？
- 是否需要在同一 UI 中保留高级 SAM3 整图检测入口，还是先仅保留服务代码、不暴露按钮？
