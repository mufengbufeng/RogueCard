## Why

Draft Workbench 已能把设计图转换为 UGUI 结构，但从设计图中切出 icon、按钮背景、面板等可复用 Sprite 仍需要人工截图、命名、导入和手动绑定，效率低且容易误覆盖 prefab。用户已在本机安装 ComfyUI 与 SAM3，因此可以把 AI 分割能力产品化为一条“候选切图 → 人审 → 导入 Sprite → 匹配 prefab → 可回滚应用”的安全工作流。

## What Changes

- 在 Draft Workbench 中新增 Auto Slice 工作流，以步骤式体验引导用户完成输入、检测、候选区域审查、Sprite 生成、Prefab 匹配、应用和回滚。
- 支持连接本地 ComfyUI，并适配 SAM3 workflow，对设计图执行分割检测。
- 将 AI/SAM 输出转换为可审查的 region 列表，显示 bbox、marker、类型、置信度和是否需要人工确认。
- 将选中 region 裁剪为独立 PNG，并导入 Unity 项目为 UI Sprite。
- 将生成 Sprite 与 prefab Image 节点进行候选匹配，展示匹配原因、置信度、旧 Sprite、新 Sprite 和覆盖风险。
- 仅对用户确认的匹配执行 `Image.sprite` 应用，并记录可回滚的 Sprite 变更。
- 保留“只生成资产、不应用 prefab”的模式，避免用户必须在一次流程中完成全部步骤。
- 不引入运行时依赖；ComfyUI、SAM3、切图和审查 UI 均限定为 Editor-only 能力。

## Capabilities

### New Capabilities

- `draft-workbench-auto-slice`: 定义 Draft Workbench 中基于 ComfyUI/SAM3 的自动切图、候选区域审查、Sprite 生成、Prefab 匹配和安全应用体验。

### Modified Capabilities

- `image-to-ugui-draft-workbench`: 扩展 Draft Workbench 的 apply/revert/report 行为，使 Sprite 应用记录与回滚成为维护记录的一部分。
- `ui-structure-converter`: 扩展 `ui_structure.json` 与转换描述符对 `asset`、`marker`、`spriteHint`、`regionId` 的语义传递，供自动切图匹配使用。

## Impact

- 影响 Editor 工具：`Assets/GameScripts/Editor/DraftWorkbench/` 下的窗口、UXML/USS、配置资产、ComfyUI 客户端、workflow 适配、region 模型、裁剪导入、匹配和应用服务。
- 影响 Draft Workbench 维护记录：需要记录 generated sprite、sprite assignment、old/new sprite GUID/path 与回滚状态。
- 影响 UI 结构 schema：需要把资源/marker 语义从 AI JSON 保留到 `UguiNodeDescriptor`/视觉描述中。
- 影响资源目录约定：生成的最终 Sprite 默认放入 `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`，ComfyUI 中间缓存放入 `Library/DraftWorkbench/ComfyCache/<SourceHash>/`。
- 不修改运行时代码、不要求玩家构建包含 ComfyUI/SAM3 配置或 Draft Workbench 类型。
