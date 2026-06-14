## Why

Draft Workbench 当前把 `ui_structure.json` 的顶层 root 当成真实 UGUI 节点应用到目标 Prefab，导致目标根 `RootCanvas` 之外额外生成 `LevelPreviewMenu` / `MainMenuScreen` 等无意义 wrapper。JSON-driven Auto Slice 也只按原始 bbox 裁出矩形 raw PNG，当前 ComfyUI refinement 是 passthrough，无法生成带透明 alpha 的可用 icon。

## What Changes

- 将 Draft Workbench 默认 JSON 转换策略改为 `VirtualRoot`：JSON root 只作为坐标父框和语义容器，不创建到目标 Prefab 下。
- 增加历史 wrapper 清理能力：只清理 metadata/apply record 可确认由 Draft Workbench 创建的顶层 wrapper，无法确认来源时不自动删除。
- 为 JSON-driven region 增加资产意图和透明度意图，区分 background、panel、button、icon、decoration 等输出策略。
- 修正 JSON raw crop 外扩策略，按资产意图给 icon/decoration 留出更大 padding，并支持透明留白/方形画布。
- 将透明 icon refinement 从 `LoadImage -> SaveImage` passthrough 改为可配置背景移除工作流：默认 BiRefNet/RMBG，失败或低质量时可用 Sam2 bbox/point 工作流兜底。
- 增加 Unity 侧 alpha 质量门：透明 icon 没有透明像素或边缘质量异常时，不导入为 final Sprite，改为 NeedsReview/Failed。
- 保持 Editor-only，不修改运行时代码、HotFix 程序集、YooAsset runtime 打包或 EF UI runtime API。

## Capabilities

### New Capabilities

- `draft-workbench-transparent-icon-refinement`: 定义 Draft Workbench 中透明 icon / decoration 的背景移除、alpha 校验、Sam2 兜底和 final Sprite 导入门禁。

### Modified Capabilities

- `image-to-ugui-draft-workbench`: 修改 JSON apply、清理、Auto Slice 状态和报告行为，使 JSON root 不生成 wrapper，并记录透明 icon refinement 质量状态。
- `ui-structure-converter`: 修改转换器 root 处理和 JSON 语义传递，使默认转换支持 VirtualRoot、资产意图、透明度意图和 crop padding 元数据。

## Impact

- 影响 Editor 工具：`Assets/GameScripts/Editor/DraftWorkbench/` 下的转换器、Prefab 预览/应用、metadata、Auto Slice region 模型、raw crop、ComfyUI refinement、Sprite 导入、匹配服务和窗口 UI。
- 影响 OpenSpec 既有变更：本变更建立在 `draft-workbench-json-driven-slicing` 的 JSON-driven raw/refined 两阶段链路之上，并修复其 root 映射、crop padding 和透明 icon 质量问题。
- 影响生成资源：final Sprite 仍输出到 `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`，raw/refined 中间产物继续位于 `Library/DraftWorkbench/...`。
- 需要新增/更新 EditMode 测试，并在实现后执行 Unity 编译与 DraftWorkbench 相关测试。
