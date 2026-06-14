## Context

Draft Workbench 已具备 AI 生成 `ui_structure.json`、Parse & Preview、Prefab apply/revert、JSON-driven raw crop、ComfyUI refinement 和 Sprite import 的基础链路。但当前实现暴露出两个系统性问题：

- JSON root 被当作真实节点应用到目标 Prefab，导致 `RootCanvas` 下额外创建 `LevelPreviewMenu` 等 wrapper。
- JSON-driven Auto Slice 只裁出矩形 raw crop，ComfyUI refinement 当前是 `LoadImage -> SaveImage` passthrough，无法生成透明 icon。

本设计建立在 `draft-workbench-json-driven-slicing` 的两阶段方案之上，补齐 root 映射、crop padding、透明背景移除和 alpha 质量门。

## Goals / Non-Goals

**Goals:**

- 默认使用 `VirtualRoot`，让 JSON root 只作为坐标父框和语义容器，不创建到目标 Prefab。
- 提供安全清理历史 wrapper 的能力，只处理可追溯到 DraftWorkbench apply record 的对象。
- 为 JSON region 增加 `assetKind` / `alphaMode` / padding 语义，使 icon、decoration、panel、background 走不同生成策略。
- 用 BiRefNet/RMBG 作为默认透明背景输出路径，Sam2 bbox/point workflow 作为失败兜底。
- 在 Unity 侧验证透明 icon 的 alpha 质量，失败时阻止导入 final Sprite 并进入 review。
- 用 EditMode 测试覆盖转换、裁剪、refinement prompt、alpha 校验和导入门禁。

**Non-Goals:**

- 不重做运行时 UI 框架、不修改 HotFix 运行时代码。
- 不把所有 UI 切图都透明化；背景、预览图、长条面板仍可保持矩形 Sprite。
- 不自动删除无法通过 metadata 确认来源的历史节点。
- 不默认接入有非商业限制的 BRIA RMBG-2.0 模型。
- 不实现无人审查的一键覆盖 Prefab；匹配和应用仍需用户批准。

## Decisions

### Decision 1: 默认 VirtualRoot，保留兼容模式

新增 `UiStructureConversionOptions`：

- `RootMode = VirtualRoot | IncludeRootNode | MapRootToPrefabRoot`
- `TargetRootName`
- `ApplyRootRectToTarget`

Draft Workbench 默认使用 `VirtualRoot`。转换器以 JSON root 的 `position/size` 作为子节点坐标父框，但不输出 root descriptor；root children 的 `ParentPath` 改为 `""`，直接挂到目标 Prefab 根下。`IncludeRootNode` 保留给测试和兼容场景。

选择理由：

- 目标 Prefab 根对象已经是 EF/UGUI 需要的真实根。
- JSON root 通常只是 AI 对页面的命名，不应污染 Prefab 层级。
- 直接重命名目标 root 风险更高，会影响已有引用、绑定和运行时加载约定。

### Decision 2: 历史 wrapper 清理必须依赖记录

新增清理入口只处理满足以下条件的顶层对象：

- 位于目标 Prefab 根的直接子级。
- 对象路径存在于 DraftWorkbench metadata/apply record 的 `CreatedObjectPaths`。
- 名称匹配当前或历史 JSON root，例如 `LevelPreviewMenu` / `MainMenuScreen`。

无法确认来源时只报告候选，不执行删除。

选择理由：

- Prefab 可能已有用户手动创建的同名节点。
- 直接基于名称删除属于不可逆资源风险。
- apply record 已经是当前工具的回滚边界。

### Decision 3: Region 语义驱动 crop 和 alpha 策略

在 `DraftSegmentRegion` / `UiNodeVisuals` 中新增语义：

- `assetKind`: `Background`, `Panel`, `Button`, `Icon`, `Decoration`, `Composite`, `Unknown`
- `alphaMode`: `OpaqueRect`, `TransparentForeground`, `PreserveSourceAlpha`
- `paddingPixels`, `paddingPercent`, `makeSquare`, `requiresTransparentAlpha`

优先使用 JSON 明确字段；缺失时从 node name、marker、spriteHint、面积比例和组件类型推断。

默认策略：

- background / large preview：矩形输出，少量或无扩边。
- panel / bar / frame / button：矩形输出，适度扩边。
- icon / decoration：透明前景输出，较大扩边，并可补方形透明画布。

### Decision 4: BiRefNet 默认，Sam2 兜底

透明 icon refinement 使用两级策略：

1. 默认 `BiRefNet/RMBG` workflow：适合批量输入单个 crop 并直接输出 RGBA/mask。
2. 失败兜底 `Sam2` workflow：使用 JSON crop 中的 bbox 或中心点 prompt 分割目标，再 `JoinImageWithAlpha` 输出透明 PNG。

选择理由：

- Sam2 更适合有 prompt 的精确分割，但批处理时需要可靠的 box/point 提示。
- BiRefNet/RMBG 对“一个 crop 里抠主物体”的默认体验更稳定，适合作为 first pass。
- BRIA RMBG-2.0 有非商业免费限制，不适合作为游戏项目默认方案。

### Decision 5: Alpha 校验是导入 final Sprite 的门禁

新增 `DraftAlphaValidationService`，对 transparent icon 输出统计：

- `minAlpha`, `maxAlpha`
- `transparentRatio`
- `borderOpaqueRatio`
- `nonOpaquePixelRatio`

若 `requiresTransparentAlpha == true` 且输出没有透明像素、边缘几乎全不透明，或主体面积异常，则 region 标记 `NeedsReview` / `Failed`，不导入 final Sprite。矩形资产不强制透明。

### Decision 6: 中间产物与 final Sprite 分离

raw crop 和 refined PNG 保持在 `Library/DraftWorkbench/...`；只有通过 alpha 校验的 final PNG 才复制到 `Assets/AssetRaw/Image/DraftWorkbenchGenerated/<PrefabName>/<SourceHash>/` 并导入为 Sprite。

## Risks / Trade-offs

- [Risk] VirtualRoot 会改变现有测试和路径记录。  
  Mitigation: 保留 `IncludeRootNode` 兼容模式，并更新 metadata 路径迁移/清理逻辑。

- [Risk] assetKind 推断可能误判按钮背景和图标。  
  Mitigation: JSON 字段优先，UI 中保留人工 review 和 marker 编辑，匹配时显示原因。

- [Risk] BiRefNet/Sam2 workflow 在用户本机缺少节点或模型时失败。  
  Mitigation: workflow 路径可配置，失败只影响 refinement，不清空 raw crop 状态。

- [Risk] 透明 alpha 校验过严导致可用图被拦截。  
  Mitigation: 阈值进入配置，失败状态保留 refined 文件路径供人工确认。

- [Risk] 历史 wrapper 清理误删用户内容。  
  Mitigation: 只清理 apply record 记录的对象；无记录只列出候选。

## Migration Plan

1. 新增转换选项并默认在 Draft Workbench 使用 `VirtualRoot`。
2. 更新 preview/apply/report 路径，确认新生成不再出现 root wrapper。
3. 添加历史 wrapper 清理入口，先实现 dry-run/preview，再执行 recorded cleanup。
4. 扩展 region 模型和 JSON schema 语义。
5. 修正 raw crop 外扩和透明 padding。
6. 接入 BiRefNet/Sam2 refinement workflow selection。
7. 加入 alpha 校验和 final Sprite 导入门禁。
8. 更新 UI 状态、报告、测试和文档。

Rollback strategy:

- 保留 `IncludeRootNode` 可临时恢复旧 root descriptor 行为。
- 清理操作通过 apply record 记录，可使用现有 revert 边界恢复。
- 透明 refinement 失败不会覆盖 Prefab，raw/refined 中间文件保留用于人工处理。

## Open Questions

- Sam2 workflow 模板的具体节点 ID/输入字段需要以用户最终选定的 ComfyUI workflow JSON 为准。
- BiRefNet workflow 是使用 ComfyUI 原生节点还是用户本地已有 custom node，需要在实现前通过配置和一次本机 dry-run 确认。
