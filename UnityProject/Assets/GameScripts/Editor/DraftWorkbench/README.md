# Draft Workbench JSON-Driven Slicing 契约

本文档描述 Draft Workbench 的 Editor-only 自动切图流程。默认路径是：

1. 从 `ui_structure.json` 或预览 descriptor 提取 region；
2. 生成 raw PNG；
3. 选择 raw PNG 送入 ComfyUI refinement；
4. 导入 refined PNG 为 Unity Sprite；
5. 需要时再做 prefab 匹配与应用。

## 输入约定

- `ui_structure.json` 和预览 descriptor 都可作为 region 来源。
- Draft Workbench 默认使用 `VirtualRoot`：JSON 顶层 `root` 只作为坐标父框和语义容器，不会创建到目标 Prefab 根节点下。
- 需要旧层级行为的测试或兼容调用必须显式使用 `UiStructureConversionOptions.IncludeRootNode`。
- 默认只选择可映射到 `UnityEngine.UI.Image` 的节点。
- `text`、纯 layout container、无有效 source bounds 的节点默认跳过。
- `asset`、`marker`、`spriteHint`、`regionId` 会沿着转换链保留。
- `assetKind`、`alphaMode`、`paddingPixels`、`paddingPercent`、`makeSquare`、`requiresTransparentAlpha` 会优先使用 JSON 显式值；缺失时由 marker、spriteHint、节点名、组件和面积推断。

## Prefab root 与历史 wrapper

- 将 `LevelPreviewMenu`、`MainMenuScreen` 等 JSON root 应用到 `RootCanvas` 时，root children 会直接落在 Prefab root 下。
- `RootCanvas` 的名称和根级组件不会因 VirtualRoot 映射被重命名或删除。
- 历史 wrapper 清理只删除 Draft Workbench apply record 中 `CreatedObjectPaths` 可证明创建过的顶层对象。
- 名称相似但没有记录的顶层对象只会作为候选报告，不会自动删除。

## raw / refined / Sprite 输出

- raw PNG：`Library/DraftWorkbench/JsonRawCrops/<SourceHash>/`
- refined PNG：`Library/DraftWorkbench/ComfyCache/<SourceHash>/refined/`
- final Sprite：`Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/`

raw 和 refined 都是中间产物，最终可引用资产只保留 Sprite 输出目录。

## 两段式工作流

### 阶段 1

“根据 JSON 生成原始切图”

- 不依赖 ComfyUI。
- 会记录 region id、node path、source bounds、crop bounds、marker、review state、generation status。
- icon / decoration 默认按 `TransparentForeground` 处理，会使用配置的固定像素和比例 padding 扩展 raw crop，并可输出方形透明画布。
- background / preview / panel / bar / frame / button plate 默认按 `OpaqueRect` 处理，不要求透明 alpha。
- 生成后可直接在 region 列表里 include / ignore / 改名。

### 阶段 2

“使用 ComfyUI 生成可用切图”

- 输入是单个 raw PNG crop。
- `TransparentForeground` 默认走 BiRefNet/RMBG 类背景移除 prompt，期望输出带 alpha 的 PNG。
- 配置启用 Sam2 fallback 时，BiRefNet/RMBG 输出未通过 alpha 校验后，会把 JSON-derived bbox / center point 作为提示传给 Sam2 prompt。
- `OpaqueRect` 可继续使用 passthrough refinement，但报告不会把它描述为背景移除结果。
- 失败不会清空 raw 记录。
- refined 尺寸与 raw 不一致时会标记需要复核。
- 透明前景没有透明像素或边缘污染超阈值时会标记失败/需复核，并阻止复制到 final Sprite 目录。

## 预览与状态

- 设计图预览会显示 region overlay。
- 颜色含义：
  - 已选中
  - 已忽略
  - 需复核
  - 已生成
  - 失败

## 高级兜底

- SAM3 / manifest / mask 检测仍然保留，但不是默认入口。
- 如果 workflow 没有 manifest，工具会尝试使用 mask-only fallback。
- 这类结果默认需要人工复核。

## 故障排查

### raw 阶段失败

- 确认 Design Image 是项目内 `Texture2D` 资源。
- 确认 JSON 可解析。
- 确认 region 具有有效 source bounds / crop bounds。

### refinement 失败

- 确认 ComfyUI 已启动并可访问。
- 检查 workflow 是否是 raw crop refinement 版本，而不是整图 SAM3 检测版本。
- 查看对应 region 的 `generationMessage`。

### Sprite 未导入

- 确认 refined PNG 路径存在。
- 确认 `GeneratedSpriteOutputRoot` 以 `Assets/` 开头。
- 对 icon / decoration，确认 refined PNG 包含透明 alpha；完全不透明输出会被 alpha gate 拦截。
- 检查 Console 中的导入错误。

### 预览 / 匹配异常

- 检查 `marker` 是否被手动改坏。
- 检查 `regionId` 是否与预览 descriptor 保持一致。
- 需要应用前，确认 assignment 已批准。
