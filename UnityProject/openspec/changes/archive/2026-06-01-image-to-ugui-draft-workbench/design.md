## Context

`GameView.prefab` 已经迁移到 UGUI + `GameController` + 引用收集器的组合，但从草图或参考截图回填界面仍主要靠手工拖拽、改 RectTransform、绑定字段，再跑测试兜底。当前项目已经有三块可复用基础：

- `GameViewPrefabBuilder` 能以代码方式生成/修复 GameView 的 UGUI 层级与 ReferenceCollector 绑定。
- `ReferenceCollectorRuleService` 与 `UiScriptBinderTextRewriter` 已把 UI 节点命名、字段规则、脚本重写收束在编辑器工具链内。
- `GameViewIntegrationTests`、`BattlePanelViewTests`、`ReferenceCollectorRuleTests` 覆盖了 GameView 绑定、布局对象存在性与 UI 控制器生命周期。

本变更把这些基础串成一个编辑器内的 draft workbench：导入一张静态草图图像，在 Unity Editor 中创建草图覆盖层、生成/修复 UGUI 节点、绑定字段、输出可审查的变更报告，并用现有测试确保生成结果不会破坏 GameView 契约。

## Goals / Non-Goals

**Goals:**

- 提供一个 Unity Editor workbench，从本地图像创建 GameView 草图参照覆盖层，并保存草图源、尺寸、锚点、透明度等可复现元数据。
- 复用现有 `GameViewPrefabBuilder` 与 ReferenceCollector 规则生成或修复 UGUI 节点，不引入运行时依赖。
- 自动绑定新增或修复的 UGUI 节点到 ReferenceCollector，并在字段缺失、重名、类型不匹配时给出编辑器内报告。
- 支持 apply / preview / revert 工作流，避免工具直接把草图覆盖层或临时元数据带进运行时界面。
- 补齐 EditMode 测试，覆盖元数据解析、节点生成、引用绑定、报告输出与 GameView prefab 契约。

**Non-Goals:**

- 不做图像识别、OCR、自动切图、自动视觉布局推断或 AI 生成 UI。
- 不替代设计工具，也不把草图图像作为最终游戏 UI 资源打包。
- 不重构 GameView 运行时控制器或战斗 UI 行为。
- 不更改 YooAsset/HybridCLR 资源加载流程。
- 不为所有 UI prefab 泛化一套完整低代码系统；本次只面向 GameView 工作流，抽象边界为后续扩展保留。

## Decisions

### 决策一：Workbench 仅运行在 Unity Editor

- **选择**：所有入口放在 `Assets/GameScripts/Editor` 或 `Assets/EF/EFEditor/Editor`，使用 `#if UNITY_EDITOR` 与 EditorWindow/MenuItem 暴露，不向 HotFix 或运行时程序集添加依赖。
- **理由**：草图参照、preview、revert 都是生产工具行为；运行时只需要最终 prefab 与绑定结果。把入口限制在 Editor 可以避免 ILRuntime/HybridCLR、AOT、资源包构建链路被工具依赖污染。

### 决策二：草图元数据独立保存，prefab 只保存必要节点

- **选择**：为导入的草图创建编辑器元数据 asset 或同目录 sidecar，记录 source image GUID、目标 prefab GUID、画布尺寸、参照层透明度、导入时间与应用状态；prefab preview 期间可插入草图覆盖层，apply 后必须移除或标记为 EditorOnly。
- **替代方案**：把所有元数据塞进 prefab 上的 MonoBehaviour。
- **理由**：sidecar/asset 更便于 revert 与审查，也能避免临时草图对象随 prefab 进入运行时。Prefab 上只保留最终 UGUI 结构与 ReferenceCollector 数据。

### 决策三：节点生成复用 `GameViewPrefabBuilder`

- **选择**：把 GameView 节点创建、RectTransform 默认值、组件类型、命名规则沉到现有 builder 或其可测试 helper；workbench 调用 builder 生成/修复，而不是另写一套层级创建逻辑。
- **理由**：GameView 当前 prefab 已由 builder 维护，重复生成逻辑会让测试和实际工具分叉。复用 builder 能让现有 `GameViewPrefabBuilder` 测试继续作为主防线。

### 决策四：自动绑定走规则服务，不手写字段映射

- **选择**：新增节点的 ReferenceCollector key、字段名、组件类型由 `ReferenceCollectorRuleService` 和 `UiScriptBinderTextRewriter` 决定；workbench 只收集候选节点并展示绑定报告。
- **理由**：项目已经把绑定规则集中在 ReferenceCollector 编辑器工具链中。workbench 若绕开规则服务，会让命名、字段重写、重复 key 检查出现第二个真相源。

### 决策五：Preview 与 Apply 必须可审查

- **选择**：preview 只在打开的 prefab stage 或临时实例上展示草图覆盖层与建议节点；apply 前展示将新增/修改/删除的对象、绑定 key、脚本字段与潜在风险；apply 后输出报告 asset 或日志条目。
- **理由**：草图到 UGUI 的人工决策很多，本变更提供的是半自动工作台而不是黑箱生成器。可审查报告能让用户知道工具具体动了哪些 UI 对象和绑定。

### 决策六：Revert 以本次 apply 记录为边界

- **选择**：每次 apply 写入一条变更记录，记录创建的 GameObject 路径、修改的 RectTransform 字段、ReferenceCollector key 与脚本字段变更；revert 只撤销该记录里工具创建或修改的内容。
- **理由**：仓库可能已经有用户手工修改，revert 不能粗暴重建整个 prefab。按记录撤销能降低误删手工改动的风险。

## Risks / Trade-offs

- **[风险] Prefab YAML 对象路径不稳定** -> 缓解：变更记录同时保存 GameObject hierarchy path、ReferenceCollector key、local fileID（若可得），revert 时先做一致性校验，冲突则要求用户手工确认。
- **[风险] 草图比例与 CanvasScaler 设计分辨率不一致** -> 缓解：导入时记录源图尺寸与目标 canvas reference resolution，preview 显示 scale/fit 模式；测试覆盖常用 16:9 与非 16:9 尺寸。
- **[风险] 自动绑定改写脚本导致无关格式变化** -> 缓解：继续使用 `UiScriptBinderTextRewriter` 的局部 rewrite 能力，并在测试里断言只新增所需字段/属性。
- **[风险] 工具生成层级与手工 GameView 改动冲突** -> 缓解：preview 报告必须列出冲突项；apply 默认跳过冲突对象，除非用户明确选择覆盖。
- **[Trade-off] 先只支持 GameView** -> 范围更窄，但能贴合当前痛点并复用现有测试；后续若要扩展到其它 prefab，可把 draft metadata 与 binding report 抽成通用层。

## Implementation Plan

1. 定义 draft workbench 元数据与报告模型：草图源、目标 prefab、preview 参数、apply 记录、binding diff 与 conflict 列表。
2. 新增 EditorWindow/MenuItem 入口：选择 GameView prefab 与本地图像，创建 preview 覆盖层，保存元数据。
3. 抽出/扩展 `GameViewPrefabBuilder` 的可复用节点生成 API，让 workbench 能按候选节点生成或修复 UGUI 对象。
4. 接入 `ReferenceCollectorRuleService` 与 `UiScriptBinderTextRewriter`：生成 binding plan、执行字段绑定、输出 binding report。
5. 实现 apply/revert：apply 写 prefab 与报告，revert 只撤销对应 apply 记录。
6. 补齐 EditMode 测试：metadata roundtrip、preview 不污染运行时 prefab、builder 生成对象、binding plan 冲突、revert 边界、GameView 集成测试继续通过。
7. 运行 `openspec validate image-to-ugui-draft-workbench --strict`、Unity 编译检查、相关 EditMode 测试。

## Open Questions

- 草图图像是否放入 `Assets/AssetRaw/UI/...` 还是作为纯 Editor-only asset 放入工具目录？建议默认 Editor-only，apply 后不进入运行时资源。
- Preview 覆盖层是否需要支持多张草图（例如局部迭代）？本次建议先支持单张，元数据结构保留列表扩展空间。
- 生成节点的候选区域由用户手工框选，还是先只修复现有 builder 已知节点？本次建议从“修复/生成已知 GameView 节点 + 手工指定新增节点”开始。
