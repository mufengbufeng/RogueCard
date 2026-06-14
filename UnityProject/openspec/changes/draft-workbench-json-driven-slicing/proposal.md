## Why

Draft Workbench 当前 Auto Slice 规划以 ComfyUI/SAM3 先检测 regions 为默认入口，导致切图边界由 AI 分割结果决定，和 `ui_structure.json` 已经识别出的 UI 元素结构脱节。用户需要把自动切图拆成确定性的两段：先按 JSON/预览节点脚本切出 raw PNG，再把 raw PNG 交给 ComfyUI 做可用化处理。

## What Changes

- 将 Auto Slice 默认主流程调整为 JSON-driven：从 `ui_structure.json` 或 Draft Workbench 预览节点读取 `position`、`size`、`asset`、`marker`、`spriteHint`、`regionId` 等信息，生成稳定的切图 region。
- 新增本地 raw PNG 生成阶段：Unity Editor 脚本按 JSON/descriptor bounds 从设计图裁出多个 raw PNG，并保留 region id、marker、源 bounds、输出路径和审查状态。
- 新增 ComfyUI refinement 阶段：将选中的 raw PNG 发送给 ComfyUI，生成透明 PNG / cleaned PNG / refined PNG，再导入为 Unity UI Sprite。
- ComfyUI/SAM3 不再默认负责决定 bbox/region；现有 SAM3 manifest/mask 检测能力可保留为高级兜底或后续非 MVP 能力。
- Auto Slice UI 表达两个明确阶段：“根据 JSON 生成原始切图”和“使用 ComfyUI 生成可用切图”，允许用户在 ComfyUI 未启动时先验证 raw crop 是否正确。
- Prefab Sprite 匹配、Apply、Revert 继续复用现有能力；本变更不扩大到一键无人审查覆盖 prefab。
- 全部能力保持 Editor-only，不向运行时、HotFix 程序集或 YooAsset runtime API 引入依赖。

## Capabilities

### New Capabilities

- `draft-workbench-json-driven-slicing`: 定义 Draft Workbench 中基于 `ui_structure.json` / 预览节点的确定性 raw PNG 切分，以及 raw PNG 经 ComfyUI refinement 后导入 Unity Sprite 的工作流。

### Modified Capabilities

- `image-to-ugui-draft-workbench`: 扩展 Draft Workbench 编辑器工作流，使自动切图阶段以 JSON/预览节点为默认 region 来源，并展示 raw/refined Sprite 生成状态。
- `ui-structure-converter`: 扩展 `ui_structure.json` 反序列化和 descriptor 语义传递，使 `asset`、`marker`、`spriteHint`、`regionId`、source bounds 能作为 JSON-driven slicing 的输入。

## Impact

- 影响 Editor 工具：`Assets/GameScripts/Editor/DraftWorkbench/` 下的 Auto Slice 窗口逻辑、UXML/USS、region 模型、裁剪服务、ComfyUI 客户端/runner、Sprite 导入服务和相关 EditMode 测试。
- 影响 Draft Workbench 数据流：默认从 JSON/descriptor bounds 生成 region，而不是从 ComfyUI/SAM3 manifest/mask 生成 region。
- 影响输出目录约定：raw PNG 和 refined PNG 需要区分保存，最终可引用 Sprite 仍应位于 `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`，中间 ComfyUI 缓存仍位于 `Library/DraftWorkbench/ComfyCache/<SourceHash>/`。
- 不修改运行时代码、不修改 YooAsset Collector、不引入 SpriteAtlas 自动打包、不支持多设计图批处理或无人审查全自动覆盖 prefab。
