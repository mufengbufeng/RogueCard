## 1. 新建 ReferenceCollectorUiScriptBinder

- [x] 1.1 新建 `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorUiScriptBinder.cs`，定义 `internal static class ReferenceCollectorUiScriptBinder`，入口 `Generate(ReferenceCollector collector)`
- [x] 1.2 实现 `ResolveTargetScript(GameObject)`：按 GameObject 名称（清洗为合法 C# 类名）在 `Assets/GameScripts/HotFix/GameLogic/UI/` 下用 `AssetDatabase.FindAssets("<ClassName> t:MonoScript")` 搜索同名 `.cs`；不要求脚本挂载到 GameObject；0 命中 / 多命中均弹 `EditorUtility.DisplayDialog`
- [x] 1.3 实现 `BuildFieldBindings(ReferenceCollector)`：按 key 字典序遍历 `data`，跳过 `gameObject == null` 的条目，调用类型推断生成 `(fieldName, typeName, namespaceName, key)` 列表
- [x] 1.4 实现类型推断：优先 `entry.gameObject is Component` 则用该组件类型；否则 `ReferenceCollectorRuleService.FindFirstMatchingRule(key)` + `ResolveRule`；否则退化为 `UnityEngine.GameObject`
- [x] 1.5 实现 `SanitizeFieldName(key)`：清洗非字母数字字符为 `_`、数字开头补 `_`、首字母小写、加 `_` 前缀；保持去重计数 `_xxx_1`、`_xxx_2`

## 2. 源代码改写器（纯函数）

实现位置：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/UiScriptBinder/UiScriptBinderTextRewriter.cs`，独立 asmdef `EF.Editor.UiScriptBinder.TextRewriter`（无 UnityEditor/UnityEngine 依赖，便于纯文本单测）。

- [x] 2.1 实现 `EnsureUsings(content, requiredUsings)`：正则扫现有 using、按字典序插入缺失项
- [x] 2.2 实现 `BuildAutoRegionBlock(fields)`：拼接 `#region 自动生成\n[UHubBind("Key")] private Type _name;\n...\n#endregion` 字符串，8 空格缩进（类体内字段层级）
- [x] 2.3 实现 `ReplaceOrInsertRegion(content, regionBlock)`：正则 `#region\s+自动生成[\s\S]*?#endregion` 匹配存在则整体替换；不存在则在 `class XXX : UIView {` 后插入
- [x] 2.4 实现 `EnsureUHubInitializeCall(content)`：正则匹配 `OnInitialize` 方法体；若方法存在但缺少 `UHub.Initialize();` 则在 `base.OnInitialize();` 之后插入；方法不存在则返回 `MethodFound=false`（由调用方决定是否 `Debug.LogWarning`）
- [x] 2.5 实现 `DetectExternalFields(content)`：扫描类体内 region 外的字段声明，返回已存在的字段名集合，用于跳过冲突项

## 3. 编辑器集成

- [x] 3.1 修改 `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorEditor.cs` 的 `BuildAutoCollectOperations`：移除注释行 `// UHub 自动绑定已移除（UI 框架重设计为 MVVM 模式）`，添加 `row.Add(CreateButton("添加变量到UI代码", () => { ReferenceCollectorUiScriptBinder.Generate(referenceCollector); }));`
- [x] 3.2 写入前打印审计日志：`Debug.Log` 输出"将生成 N 个字段，跳过 M 个已有 region 外字段"（在 `Generate` 内实现）
- [x] 3.3 写入后调用 `AssetDatabase.ImportAsset(assetPath)` + `AssetDatabase.Refresh()` 触发重编译（仅在内容确实变化时）

## 4. EditMode 测试

测试位置：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/UiScriptBinder/Tests/UiScriptBinderTextRewriterTests.cs`，独立测试 asmdef `EF.Editor.UiScriptBinder.TextRewriter.Tests`（`includePlatforms=Editor`、`UNITY_INCLUDE_TESTS` 约束、引用 `EF.Editor.UiScriptBinder.TextRewriter`）。

- [x] 4.1 选择并配置测试程序集（创建 `EF.Editor.UiScriptBinder.TextRewriter.Tests.asmdef`）
- [x] 4.2 测试 region 插入：纯文本输入"无 region 的类源" → 验证 region 块出现在类体顶部、字段顺序与输入一致（`ReplaceOrInsertRegion_NoExistingRegion_InsertsAfterClassBrace`）
- [x] 4.3 测试 region 替换：纯文本输入"已有 region 的类源" → 验证新 region 整体替换、不重复字段（`ReplaceOrInsertRegion_ExistingRegion_ReplacedWholesale`、`ReplaceOrInsertRegion_RepeatedReplace_IsIdempotent`）
- [x] 4.4 测试 `UHub.Initialize()` 注入：方法存在 + 缺少调用 → 验证插入位置；调用已存在 → 验证幂等；方法不存在 → 验证不报错且字段仍生成（三个 `EnsureUHubInitializeCall_*` 用例）
- [x] 4.5 测试 using 补充：缺少多个命名空间 → 验证按字典序插入到 using 块末尾（`EnsureUsings_MissingNamespaces_AddedAlphabetically`、`EnsureUsings_AllAlreadyPresent_LeavesContentUnchanged`）
- [x] 4.6 测试 region 外字段冲突识别（`DetectExternalFields_RegionExcluded_ReturnsOnlyExternal`）
- [x] 4.7 集成场景测试：多次执行完整生成流程，验证 region/UHub.Initialize/using 三类改动幂等（`IntegratedScenario_RepeatedGenerate_RegionStableNoDuplicateFields`）

## 5. 手动验证（待用户在 Unity Editor 中执行）

> 当前 Unity Editor 未打开本项目，5.1–5.4 需在重新打开后由用户验证。

- [ ] 5.1 在 Unity 中打开 `Assets/AssetRaw/UI/Game/GameView.prefab`，根节点 ReferenceCollector 上点击"添加变量到UI代码"
- [ ] 5.2 检查 `GameView.cs` 内 `#region 自动生成` 块出现、字段名与 prefab key 对应、类型符合规则
- [ ] 5.3 重复点击 3 次，区块内容应稳定一致，无字段重复
- [ ] 5.4 在 `Assets/AssetRaw/UI/Main/MainView.prefab`（或同类 prefab）上重复验证
- [x] 5.5 编译检查：python 入口在 Windows 上指向 Store Stub，已通过 `dotnet build Assembly-CSharp-Editor.csproj --no-restore`（临时把新文件加入 csproj 验证）确认 0 错误；csproj 已恢复原状，Unity 重开后会基于 asmdef 重新生成

## 6. 文档

- [x] 6.1 更新 `Assets/EF/EFRuntime/Common/ReferenceCollector/README_AutoCollection.md`，记录"添加变量到UI代码"按钮行为与 region 标识

## 7. 冲突弹窗增强（响应 MainView 验证反馈）

> 起因：5.4 验证发现 `MainView.cs` 已有 4 个 region 外 `public` 字段与 prefab key 撞名，默认静默跳过导致 region 始终为空，用户难以察觉。改为弹窗让用户选择覆盖/跳过/取消。

- [x] 7.1 在 `UiScriptBinderTextRewriter.DetectExternalFields` 应用 fieldRegex 前先剥离 `//` 单行注释与 `/* */` 块注释，避免把注释字段误认作外部字段
- [x] 7.2 新增 `UiScriptBinderTextRewriter.RemoveExternalFields(content, fieldNames)` 纯函数：按字段名逐行清除整行声明（含同行属性）
- [x] 7.3 `ReferenceCollectorUiScriptBinder.Generate` 接入 `EditorUtility.DisplayDialogComplex` 三选一弹窗（覆盖 / 跳过 / 取消），按选择决定调用 `RemoveExternalFields` 还是过滤 `keptFields`，并打印对应审计日志
- [x] 7.4 EditMode 测试：注释豁免（`DetectExternalFields_CommentedFields_AreIgnored`、`DetectExternalFields_CommentBlockSpanningMultipleLines_IsStripped`）+ `RemoveExternalFields` 三个场景（public/private/带 SerializeField/未命中名字）
- [x] 7.5 `dotnet build UnityProject.slnx --no-restore` 通过 0 错误
- [ ] 7.6 手动验证：在 Unity 中重新打开 `MainView.prefab` → 点击「添加变量到UI代码」→ 弹窗选「覆盖」→ 检查 `MainView.cs` 的 4 个 public 字段被删除、region 内出现 `[UHubBind("Key")] private Type _name;` 形式
- [ ] 7.7 手动验证：再次点击按钮 → 弹窗不再出现（无冲突）→ 文件内容无变化（幂等）
