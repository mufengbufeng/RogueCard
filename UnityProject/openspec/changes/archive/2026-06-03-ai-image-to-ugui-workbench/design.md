## Context

`DraftWorkbench` 已实现预览/应用/回滚/绑定的完整流水线，但节点描述符（`UguiNodeDescriptor`）需要手动构建。当前 `GameViewDraftBuilder` 绑定 GameView 特定逻辑，不支持通用 Prefab。

`Image-To-UI`（XuToWei/Image-To-UI）提供了一套基于 Codex skill 的工作流：Python 脚本做网格分析和资产清单，AI 视觉分析写 `ui_structure.json`。其 JSON schema 定义了 canvas/root 层级、align/vAlign/layout 定位模型、container/image/rect/text/button/overlay 元素类型。

本变更在 DraftWorkbench 内直接调用 OpenAI 兼容 Vision API，将设计图发送给 AI 并要求返回 `ui_structure.json`，然后通过转换器生成 UGUI 节点，接入已有的 Preview/Apply/Revert/Bind 流水线。

## Goals / Non-Goals

**Goals:**

- 在编辑器窗口内配置 OpenAI 兼容 Vision API 端点、密钥、模型名和 System Prompt。
- 将设计图发送给 AI API，接收 `ui_structure.json` 格式的响应。
- 将 `ui_structure.json` 转换为 `UguiNodeDescriptor[]`，正确映射 align/vAlign/layout → Unity RectTransform / LayoutGroup。
- 支持任意 Prefab 根节点（新建 Prefab 或追加到已有 Prefab）。
- 所有新增代码仅存在于 Editor 程序集，不引入运行时依赖。

**Non-Goals:**

- 不做切图 Sprite 自动导入映射（第一版用纯色块/文字占位）。
- 不做 OCR 或文字识别。
- 不做多图像对比迭代（第一版只支持单图单次生成）。
- 不集成 Python 脚本（inventory_assets.py / annotate_grid.py 等）。
- 不改变运行时 UI 加载、YooAsset / HybridCLR 资源链路。

## Decisions

### 决策一：AI API 只支持 OpenAI 兼容格式

- **选择**：使用 `POST /v1/chat/completions` + `messages[{ role: "user", content: [{ type: "image_url", image_url: { url: "data:image/png;base64,..." } }, { type: "text", text: "..." }] }]` 格式。
- **理由**：一套格式覆盖 OpenAI / Claude proxy / Ollama / OpenRouter 等。不额外支持 Anthropic 原生格式，避免维护两套 HTTP 构造逻辑。
- **替代方案**：同时支持 Anthropic Messages API。增加复杂度但只多覆盖一个提供商，不符合"简单"原则。

### 决策二：System Prompt 整体可配置，内置默认模板

- **选择**：一个文本框存放完整 System Prompt，提供内置默认模板（包含 `ui_structure.json` schema 描述、输出格式要求、Unity UGUI 坐标系说明）。用户可整体替换或微调。
- **理由**：一个字段最简单。拆分成多块模板增加 UI 复杂度，且 AI 对 prompt 的响应是整体性的，分块管理实际收益低。
- **替代方案**：分离角色/格式/约定三块模板。更结构化但 UI 更复杂，第一版不需要。

### 决策三：JSON 返回用 prompt 约束 + 正则提取兜底

- **选择**：System Prompt 要求 AI 只返回 JSON，不添加解释。解析时先尝试直接 `JsonUtility`/`Newtonsoft.Json` 反序列化，失败则用正则提取 ```` ```json ... ``` ```` 代码块后重试。如果 AI 端点支持 `response_format: { type: "json_object" }` 则附带此参数，不支持则忽略。
- **理由**：最稳定、最兼容的方案。不需要 Function Calling 或 Structured Output 的端点支持。
- **替代方案**：用 Function Calling 强制 schema。更严格但兼容性最窄，Ollama 等本地模型不支持。

### 决策四：layout 用 Unity 内置 LayoutGroup 组件

- **选择**：`ui_structure.json` 中 `layout: { type: "row" }` → 添加 `HorizontalLayoutGroup`，`layout: { type: "column" }` → 添加 `VerticalLayoutGroup`。spacing/padding/align 映射到 LayoutGroup 属性。
- **理由**：不手算锚点位置，Unity LayoutGroup 自动处理子节点排列，稳定可靠。Image-To-UI 的 layout schema 与 Unity LayoutGroup 语义高度匹配。
- **替代方案**：手动计算每个子节点的 RectTransform。更灵活但更容易出错，且与 Unity 惯用模式不符。

### 决策五：Y 轴翻转在转换器内统一处理

- **选择**：`UiStructureConverter` 内部将 Image-To-UI 的左上角原点（Y 向下）统一翻转为 Unity UGUI 的左下角原点（Y 向上）。对于非 layout 的元素，使用 top-left 锚点 (0,1) + AnchoredPosition 偏移。
- **理由**：集中在转换器处理，下游 builder 和 binding 不需要关心坐标系差异。
- **替代方案**：在 builder 中翻转。会让 builder 逻辑变复杂，且与现有 RectTransform 直觉不一致。

### 决策六：AI 配置用 ScriptableObject 持久化

- **选择**：`AiServiceConfig : ScriptableObject` 存放端点、密钥、模型、System Prompt。保存在 `Assets/GameScripts/Editor/DraftWorkbench/AiServiceConfig.asset`。
- **理由**：Unity 原生序列化，编辑器友好，不需要额外 JSON/YAML 文件。放在 Editor 目录下确保不进运行时构建。
- **替代方案**：用 EditorPrefs。简单但不够结构化，不适合存长文本 prompt。

### 决策七：重构为 PrefabDraftBuilder，去除 GameView 耦合

- **选择**：将 `GameViewDraftBuilder` 重命名为 `PrefabDraftBuilder`，方法签名从接受 GameView 特定参数改为接受通用 `GameObject prefabRoot`。去除所有 GameView 特定命名和假设。
- **理由**：核心逻辑（Preview/Apply/Revert）本身就是操作 prefabRoot 上的 RectTransform，与 GameView 无关。重命名使意图更清晰。

## Risks / Trade-offs

- **[风险] AI 返回 JSON 格式不稳定** → 缓解：三层容错（直接解析 → 提取代码块 → 字段缺失用默认值）。转换器对未知 type 的元素跳过并警告，不中断整个流程。
- **[风险] API 密钥泄露到版本控制** → 缓解：`AiServiceConfig.asset` 加入 `.gitignore` 或使用 `.asset.meta` 标记为 EditorOnly。首次打开窗口时提示用户配置。
- **[风险] 网络请求阻塞编辑器** → 缓解：使用 `async` + `UnityWebRequest`，不阻塞主线程。显示进度指示器。
- **[风险] 大图 base64 编码体积过大** → 缓解：发送前自动缩放设计图至 2048px 以内，降低 token 消耗。
- **[Trade-off] 第一版不做切图映射** → 生成的 Image 节点用纯色块占位，用户需手动替换 Sprite。后续迭代可加资产映射。
- **[Trade-off] LayoutGroup 不与手动锚点混合** → 一个父节点如果用了 LayoutGroup，子节点的 position 会被 LayoutGroup 覆盖。这是 Unity 的正常行为，转换器应在文档中说明。
