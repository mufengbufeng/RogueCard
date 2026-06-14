## Why

DraftWorkbench 已有预览/应用/回滚/绑定的基础设施，但从设计图到 UGUI 节点的生成仍依赖手动构建 `UguiNodeDescriptor`，未接入 AI 视觉分析能力。接入 OpenAI 兼容 Vision API 后，用户可直接在编辑器窗口内从设计图自动生成 UGUI 层级，将 AI 分析结果与已有的 Preview/Apply/Revert/Bind 流水线衔接，大幅减少手动拖拽和参数调整的工作量。

## What Changes

- 新增 AI 服务配置（OpenAI 兼容端点、API Key、模型名、System Prompt），以 ScriptableObject 形式持久化，在 DraftWorkbench 窗口中可编辑。
- 新增 AI Vision 客户端，将设计图以 base64 发送给 OpenAI 兼容 Vision API，要求返回 `ui_structure.json` 格式。
- 新增 `ui_structure.json` 的 C# 反序列化模型（`UiStructureSchema`），覆盖 canvas、root、container/image/rect/text/button/overlay 元素类型及 align/vAlign/layout/position 定位模型。
- 新增 `UiStructureConverter`，将 `ui_structure.json` 转换为 `UguiNodeDescriptor[]`，处理 align→锚点、layout→LayoutGroup、Y 轴翻转、组件映射。
- 重构 `GameViewDraftBuilder` 为通用的 `PrefabDraftBuilder`，去除 GameView 特定耦合，支持任意 Prefab 根节点（新建或追加）。
- 重写 `DraftWorkbenchWindow`，增加 AI 设置面板、设计图选择、目标 Prefab 选择（任意 Prefab）、AI 生成按钮、JSON 预览编辑、应用/回滚工作流。
- 扩展 `DraftWorkbenchModels`，为 `UguiNodeDescriptor` 增加 LayoutGroup 类型和颜色/文本属性，支持 Image-To-UI schema 的完整元素类型映射。
- 不引入运行时依赖，所有新增代码仅存在于 Editor 程序集。

## Capabilities

### New Capabilities
- `ai-vision-ui-generation`: AI 视觉分析设计图并生成 ui_structure.json，通过 OpenAI 兼容 Vision API 调用，支持配置端点/密钥/模型/提示词。
- `ui-structure-converter`: 将 ui_structure.json schema 转换为 Unity UGUI 节点描述符（UguiNodeDescriptor[]），处理定位模型映射、Y 轴翻转、元素类型到组件的映射。

### Modified Capabilities
- `image-to-ugui-draft-workbench`: 从 GameView 专用扩展为通用任意 Prefab 的 draft workbench，builder 去除 GameView 耦合，窗口增加 AI 生成工作流。

## Impact

- 影响目录：`Assets/GameScripts/Editor/DraftWorkbench/`（重构 + 新增文件）。
- 新增依赖：`System.Net.Http`（Unity Editor 内置，用于 HTTP 调用 AI API）。
- 不影响运行时程序集（GameLogic / GameProto / EF.Runtime）。
- 不改变 YooAsset / HybridCLR / VContainer 资源加载链路。
- 现有 DraftWorkbench 的 EditMode 测试需要适配新的通用 builder 接口。
