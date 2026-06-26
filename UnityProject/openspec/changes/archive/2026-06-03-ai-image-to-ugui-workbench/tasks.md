## 1. 数据模型与基础架构

- [x] [static] 1.1 创建 `UiStructureSchema.cs`：定义 `UiStructure`、`UiCanvas`、`UiElement` 等 C# 类，映射 ui_structure.json 的完整字段（type/name/position/size/align/vAlign/layout/offset/asset/color/opacity/text/fontSize/nineSlice/children）
- [x] [static] 1.2 创建 `AiServiceConfig.cs`：ScriptableObject，字段包含 endpoint (string)、apiKey (string)、model (string)、systemPrompt (string)、useJsonMode (bool)，放在 Editor 目录下
- [x] [tdd] 1.3 扩展 `DraftWorkbenchModels.cs` 中 `UguiNodeDescriptor`：新增 LayoutGroupType (enum: None/Horizontal/Vertical)、LayoutSpacing (string)、LayoutPadding (Vector2)、Color (Color?)、Text (string)、FontSize (int)、Opacity (float?) 字段
  - 实现说明：采用组合而非直接字段——视觉属性（NodeColor/TextContent/FontSize/Opacity）收敛到 `UiNodeVisuals`，布局属性（GroupType/Spacing/Padding）收敛到 `LayoutGroupInfo`，由 `UguiNodeDescriptor.Visuals` / `LayoutInfo` 引用。承载同样的数据，结构更内聚。

## 2. AI Vision 客户端

- [x] [tdd] 2.1 创建 `AiVisionClient.cs`：异步方法将图片 base64 编码，构造 OpenAI 兼容 chat completions 请求，发送并返回响应文本
  - 实现说明：采用回调式 `GenerateUiStructure(Texture2D, AiServiceConfig, int canvasWidth, int canvasHeight, Action<string> onSuccess, Action<Exception> onError)`，基于 `UnityWebRequest` + `EditorApplication.update` 轮询（Editor 内通用异步范式），而非 `UniTask<string>`，避免 Editor 程序集依赖 UniTask。额外支持 Responses API 格式。
- [x] [tdd] 2.2 实现 JSON 响应解析：优先直接反序列化，失败则正则提取 ` ```json ... ``` ` 代码块后重试，再失败返回错误信息
  - 实现说明：`ExtractJson` 实现四层兜底（直接 → ```json 代码块 → 通用 ``` 代码块 → 花括号匹配），并由 `ExtractContentFromChatCompletions` / `ExtractContentFromResponsesApi` 按 API 格式从响应中提取内容。
- [ ] [tdd] 2.3 实现图片预处理：发送前将 Texture2D 缩放至最大 2048px（保持比例），导出为 PNG 后 base64 编码
  - 已知差异（待跟进）：`ScaleTexture` / `CalculateScaledDimensions` 已实现且有单元测试覆盖，但**未接入实时发送路径**——当前直接读取磁盘原图字节（绕过 Texture2D Read/Write 限制）发送。仅影响 token 成本，不影响正确性。后续迭代可将缩放/PNG 导出接入 `GenerateUiStructure`。
- [x] [tdd] 2.4 实现连接测试方法验证端点和密钥有效性
  - 实现说明：采用回调式 `TestConnection(AiServiceConfig, Action<bool> onResult)`，与 2.1 同一异步范式，而非 `UniTask<bool>`。

## 3. UI Structure 转换器

- [x] [tdd] 3.1 创建 `UiStructureConverter.cs` 入口方法：`List<ConvertedNode> Convert(UiStructure structure)`，从 canvas 和 root 开始递归转换
- [x] [tdd] 3.2 实现 align/vAlign → 锚点映射：center→(0.5,0.5)、left→(0,0)、right→(1,1)、top→(1,1)、middle→(0.5,0.5)、bottom→(0,0)，计算对应 AnchoredPosition 偏移
  - 实现说明：水平/垂直轴独立计算，未指定的轴回退为拉伸模式（AnchorMin/Max=0/1），与规格 scenarios（单轴对齐 + 无定位信息→stretch）一致。
- [x] [tdd] 3.3 实现 Y 轴翻转：对非 layout 的显式 position 元素，使用 top-left 锚点 (0,1)→(0,1)，翻转 Y 轴
  - 实现说明：实际公式为 `AnchoredPosition.y = -position.y`（配合 top-left (0,1) 锚点），与单元测试 `Convert_Position_FlipsYAxis`（断言 -200）一致且几何自洽。tasks 原文 `-(canvasHeight - y - height)` 隐含的是 bottom-left 锚点约定，与实现选择的 top-left 锚点不同；保留实现约定。
- [x] [tdd] 3.4 实现 layout → LayoutGroup 映射：row→HorizontalLayoutGroup、column→VerticalLayoutGroup，映射 spacing/padding/align 参数
- [x] [tdd] 3.5 实现元素类型 → 组件映射：image→Image、rect+color→Image、text→Text、button→Image+Button、overlay→Image、container→无组件、未知类型跳过+警告
- [x] [tdd] 3.6 实现层级树递归和 ParentPath 计算，同名元素自动加后缀（_1, _2），记录重命名警告到转换报告

## 4. PrefabDraftBuilder 重构

- [x] [static] 4.1 将 `GameViewDraftBuilder.cs` 重命名为 `PrefabDraftBuilder.cs`，类名同步修改，去除所有 GameView 特定命名
  - 备注：本次补修了 `UiStructureConverter.cs` 中遗留的 `<see cref="GameViewDraftBuilder"/>` 文档注释引用。
- [x] [tdd] 4.2 扩展 `ApplyChanges`：支持新增的 LayoutGroup 组件添加、颜色/文本/透明度属性设置
- [x] [static] 4.3 更新现有 DraftWorkbenchTests 中对 GameViewDraftBuilder 的引用为 PrefabDraftBuilder，确保现有测试仍然通过

## 5. DraftWorkbenchWindow 重写

- [x] [manual] 5.1 重写窗口布局：顶部 Settings 折叠面板（AI 端点/密钥/模型/提示词/测试连接按钮），中部 Input 面板（设计图选择、目标 Prefab 选择、切图目录、Canvas 尺寸），下部 Workflow 面板（AI 生成/预览/应用/绑定/回滚按钮）
- [x] [tdd] 5.2 实现 AI 设置加载/保存：从 AiServiceConfig ScriptableObject 读写配置，不存在时自动创建默认资产
- [x] [tdd] 5.3 实现 AI 生成工作流：点击按钮 → 调用 AiVisionClient → 显示进度 → 展示返回的 JSON（可编辑文本框）
- [x] [tdd] 5.4 实现转换预览工作流：点击按钮 → UiStructureConverter.Convert → 在窗口中展示生成的节点树列表
- [x] [tdd] 5.5 实现应用/回滚工作流：调用 PrefabDraftBuilder.BuildPreview/ApplyChanges/RevertChanges，显示报告
- [x] [manual] 5.6 目标 Prefab 选择支持任意 Prefab 路径，自动读取 CanvasScaler 的 referenceResolution

## 6. 测试与验证

- [x] [tdd] 6.1 新增 `UiStructureConverterTests.cs`：测试 align→anchor、vAlign→anchor、Y 轴翻转、layout→LayoutGroup、元素类型映射、名称冲突处理、未知类型跳过
- [x] [tdd] 6.2 新增 `AiVisionClientTests.cs`：测试 JSON 解析（直接解析、代码块提取、失败处理）、图片缩放逻辑
- [x] [repl] 6.3 运行编译检查：`python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`
  - 说明：本机 shell 无法启动 python；改用文档回退命令 `dotnet build UnityProject.slnx --no-restore`，结果 0 错误（含 Assembly-CSharp-Editor 与 GameLogic.Tests.EditMode）。
- [x] [repl] 6.4 运行现有 DraftWorkbench EditMode 测试，确保重构后全部通过
  - 首轮（用户 Test Runner）发现 1 处范围内测试 bug：`Convert_NestedHierarchy_ComputesParentPaths` 断言 panel 的 ParentPath 为 ""，与规格（直接子节点应为 "root"）及测试自身（bg 断言 "root/panel"）矛盾。已修正断言为 "root"，用户重跑确认范围内测试全绿。
- [x] [repl] 6.5 运行新增 EditMode 测试
  - 用户重跑确认范围内新增测试全绿。首轮失败 `ExtractGeneratedImageResultFromResponsesApi_DecodesTexture`（Texture2D.LoadImage 解码失败）属于 **AI 图片生成功能**，不在本变更（vision→ui_structure.json）规格范围内，按决策超范围、本变更不处理，不阻塞勾选。

