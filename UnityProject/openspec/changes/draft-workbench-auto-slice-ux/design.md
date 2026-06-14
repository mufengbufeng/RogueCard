## Context

Draft Workbench 当前以 AI Vision 生成 `ui_structure.json`，再转换为 UGUI 节点并应用到 prefab。它解决了“结构搭建”问题，但还没有产品化的“视觉资产生产”能力：设计图中的 icon、按钮背景、面板装饰仍需要人工截图、导入 Sprite、命名、寻找 prefab Image 节点并手动替换。用户本机已安装 ComfyUI 与 SAM3 workflow（`E:/Documents/ComfyUI/user/default/workflows/Sam3.json`），具备本地 AI 分割基础。

现有 `Sam3.json` 是 ComfyUI 前端 workflow，包含 `LoadImage`、SAM3 分割子图、`MaskPreview`、`JoinImageWithAlpha` 和 `PreviewImage`，更偏向人工预览，不是直接满足 Draft Workbench 自动化消费的 manifest 输出。因此本设计需要同时覆盖：产品化 UX、ComfyUI API/workflow 适配、Unity 本地裁剪导入、Prefab 安全匹配与可回滚应用。

## Goals / Non-Goals

**Goals:**

- 在 Draft Workbench 中提供步骤式 Auto Slice 体验：输入 → 检测 → 审查 regions → 生成 Sprites → 匹配 prefab → 应用/回滚。
- 支持本地 ComfyUI 连接检测、workflow 选择、SAM3 参数/preset 配置与错误提示。
- 将 SAM3 结果转成可审查的 region 数据，包括 bbox、mask、marker、类型、置信度和 include/ignore 状态。
- 将选中 region 裁剪为独立 PNG 并导入为 Unity UI Sprite，输出到可被 prefab 引用的 `Assets/AssetRaw/Image/DraftWorkbenchGenerated/...`。
- 将生成 Sprite 与 prefab Image 节点进行候选匹配，展示匹配原因和覆盖风险，并只应用用户批准的匹配。
- 扩展 Draft Workbench metadata/maintenance record，使 Sprite 变更可审计、可回滚。
- 保持全部能力 Editor-only，不向运行时程序集、HotFix 程序集或 YooAsset runtime API 引入依赖。

**Non-Goals:**

- 不在 MVP 中实现无人审查的一键全自动覆盖 prefab。
- 不在 MVP 中实现多 Sprite 图集切片、SpriteAtlas 打包策略或自动修改 YooAsset Collector。
- 不在 MVP 中实现复杂 bbox 拖拽编辑、缩略图拖拽到节点、批量多图处理或视觉 diff。
- 不要求 ComfyUI/SAM3 成为项目运行时依赖；本功能只服务 Unity Editor 生产流程。
- 不替代现有 AI Vision 结构生成流程，而是与其并行并在匹配阶段复用结构节点信息。

## Decisions

### 1. 使用步骤式 Auto Slice 工作流，而不是暴露一排技术按钮

用户目标是“安全切图并应用到 prefab”，不是操作 ComfyUI API。UI 应围绕阶段状态机组织：

```csharp
enum AutoSliceStage
{
    Idle,
    CheckingConnection,
    Detecting,
    ReviewingRegions,
    GeneratingSprites,
    ReviewingMatches,
    Applying,
    Applied,
    Failed
}
```

主按钮根据状态变化：

- `Idle` → “开始智能切图”
- `ReviewingRegions` → “生成选中 Sprite”
- `ReviewingMatches` → “应用选中修改”
- `Applied` → “回滚上次应用”
- `Failed` → “重试”

**Alternatives considered:** 保留 `Test ComfyUI / Generate Regions / Import Sprites / Match Sprites / Apply Sprites` 的按钮列表。该方式实现简单，但更像调试面板，用户容易跳步、误解当前状态或直接应用未审查结果，因此只适合作为高级/调试模式。

### 2. ComfyUI workflow 通过适配层接入，而不是硬编码节点图

新增 `ComfyUiClient` 负责 HTTP API，`ComfyWorkflowAdapter`/`ComfyWorkflowRunner` 负责把前端 workflow 转换/patch 为 API prompt 或在必要时执行适配逻辑。适配层需要识别或配置：

- `LoadImage` 输入节点；
- SAM3 prompt / threshold / refine iterations / individual masks 参数；
- 输出 mask 或 preview image；
- 可选 manifest 输出位置。

当前用户 workflow 可作为默认 profile 的来源，但 Draft Workbench 不应假设节点 ID 永远固定。配置中允许显式指定节点 ID，并提供自动检测兜底。

**Alternatives considered:** 直接要求用户手工导出 ComfyUI API prompt。这样减少 Unity 侧复杂度，但产品体验差，且与用户已提供的前端 workflow 路径不匹配。

### 3. Unity 侧生成 manifest 和 bbox 是必要兜底

理想 workflow 输出 manifest JSON，但当前 `Sam3.json` 主要输出 mask preview。因此 MVP 设计允许两种模式：

1. workflow 输出 manifest：Unity 直接读取 region/bbox/mask/label；
2. workflow 只输出 mask/cutout：Unity 根据 mask alpha 自行计算 bbox、扩张 bbox，并生成内部 manifest。

**Alternatives considered:** 强制 ComfyUI 输出 manifest。长期最稳定，但会要求用户安装额外自定义节点或修改 workflow，MVP 门槛过高。

### 4. 生成独立 PNG + Single Sprite，而不是多 Sprite 切片

MVP 每个 region 输出独立 PNG，并导入为 Single Sprite。优势：

- GUID 稳定，prefab 引用和回滚简单；
- 避免 Unity 版本间 Sprite slicing API 差异；
- 多人协作冲突更小；
- 输出资产可单独审查、删除和替换。

**Alternatives considered:** 输出一张大图并写入 multiple sprite metadata。文件更少，但 metadata 冲突大、API 更复杂、回滚更脆弱，不适合作为第一版。

### 5. 匹配必须可解释且默认人审

匹配算法可以综合以下证据：

1. `UiElement.asset` / `marker` / `spriteHint` 与 region marker 精确匹配；
2. UI 节点 source bounds 与 region bbox 的 IoU；
3. prefab 节点名与 marker/label 的 token 相似度；
4. 用户手动选择。

每条 assignment 必须展示：目标节点、旧 Sprite、新 Sprite、置信度、匹配原因、是否覆盖已有 Sprite。已有 Sprite 或低置信度匹配默认需要人工确认。

**Alternatives considered:** 高置信度自动应用。虽然效率更高，但 AI 分割不等于 UI 语义识别，直接覆盖 prefab 风险过高。

### 6. Apply/Revert 扩展现有 Draft Workbench 记录体系

Sprite 应用不应绕过现有 metadata 与 sidecar maintenance JSON。`DraftApplyRecord` 增加 `SpriteChanges`，每条记录 old/new sprite GUID/path、object path、region id、marker。回滚时先验证当前 Sprite 仍为应用后的值，再恢复旧 Sprite。

**Alternatives considered:** 单独维护 Auto Slice history 文件。分离度更高，但用户会面对两套回滚模型，且现有 Draft Workbench report/revert 已经是合理承载点。

### 7. 输出目录区分缓存与最终资产

- ComfyUI 原始响应、mask、history、debug overlay：`Library/DraftWorkbench/ComfyCache/<SourceHash>/`
- Prefab 可引用的最终 PNG/Sprite：`Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`

**Alternatives considered:** 全部输出到 `Assets`。便于查看但会污染 AssetDatabase；中间 mask/history 不应成为项目资产。

## Risks / Trade-offs

- **ComfyUI 未启动或端口不同** → 提供连接状态卡片、测试连接、明确错误提示，并允许配置 URL。
- **前端 workflow 与 API prompt 格式不同** → 增加 workflow adapter，支持自动检测常见节点和显式节点 ID 配置。
- **SAM3 分割不具备 UI 语义理解** → region 必须可视化审查，低置信度和已有 Sprite 默认需要人工确认。
- **bbox 扩张可能包含邻近元素** → 支持固定像素 + 百分比扩张，保留 mask alpha；后续可按 Icon/Button/Panel 类型配置扩张策略。
- **透明边缘被 Unity Sprite mesh 裁掉** → MVP 使用独立 PNG 并尽量保持 Full Rect/透明边缘，UI 尺寸以 bbox/RectTransform 为准。
- **Prefab 被误覆盖** → Apply 前显示修改摘要；默认不覆盖已有 Sprite，除非用户确认；回滚只恢复 prefab 引用，不删除生成资产。
- **生成资产越来越多** → 输出路径包含 prefab/source hash，后续增加清理未引用 generated sprites 的独立功能。
- **AIBridge / dotnet 编译状态与 Unity AssetDatabase 刷新不一致** → 实现后验证应优先 AIBridge `compile unity` + Console errors，并以 dotnet build 作辅助信号。

## Migration Plan

1. 新增 Auto Slice 配置与服务，不改变现有 AI Vision 生成、Parse & Preview、Apply/Revert 默认路径。
2. 在 Draft Workbench UI 中以折叠/Tab 方式引入 Auto Slice，不阻塞已有工作流。
3. 扩展 schema/model/maintenance record 时保持旧字段兼容：旧记录没有 `SpriteChanges` 时视为空列表。
4. 第一版只新增 `Image.sprite` 引用修改，不自动删除旧资产、不修改 YooAsset Collector。
5. 如果出现问题，可通过禁用 Auto Slice UI 入口或不配置 workflow 回退到原 Draft Workbench 行为。

## Open Questions

- 用户的 ComfyUI 实例最终监听地址是否固定为 `http://127.0.0.1:8188`，还是需要支持 Desktop 自定义端口？
- `Sam3.json` 是否可以接受新增 SaveImage/manifest 输出节点，还是必须由 Unity 从 mask 输出计算 bbox？
- 是否需要在 MVP 中支持“只切 icon”与“切按钮/面板背景”的不同 preset，还是先提供一个 UI Complete preset？
- 低置信度阈值和覆盖已有 Sprite 的默认策略是否需要项目级配置？
