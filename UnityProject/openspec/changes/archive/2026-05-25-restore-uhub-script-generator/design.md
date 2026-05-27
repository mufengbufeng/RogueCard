# 设计：恢复 UHub 字段生成器按钮

## 上下文

- 已归档变更 `2026-05-07-auto-bind-ui-script` 曾实现 `ReferenceCollectorUiScriptBinder.cs`，但在后续多次 UI 框架重构（UIToolkit → 回退 UGUI → MVVM）中被整文件删除。
- 规范 `openspec/specs/auto-bind-ui-script/spec.md` 完整保留，且与当前 `GameView.cs` 的 `[UHubBind("Key")]` 形态匹配。
- 当前 `ReferenceCollectorEditor.BuildAutoCollectOperations` 末尾留有占位注释，正是新按钮要落脚的位置。
- 旧 `ReferenceCollectorScriptGenerator.cs` 仍在仓库内、未被任何按钮调用，针对的是旧 `UIWindow / BindMemberProperty` 模式，本次不动它。

## 关键设计决策

### 1. 目标 `.cs` 文件定位

**决策（已修订）**：按 GameObject 名称推导类名，在 `Assets/GameScripts/HotFix/GameLogic/UI/` 下递归搜索同名 `.cs` 资产，不要求脚本挂载到 GameObject。

具体做法：
- `SanitizeClassName(GameObject.name)` 去除非字母数字下划线、数字起始补 `_`
- `AssetDatabase.FindAssets($"{className} t:MonoScript", new[] { "Assets/GameScripts/HotFix/GameLogic/UI" })`
- 过滤出文件名（不含扩展名）严格等于 `className`（区分大小写）的资产
- 命中 0 个 → 弹窗提示文件名约定
- 命中 1 个 → 用作生成目标
- 命中 ≥2 个 → 弹窗列出冲突路径并中止

**理由**：
- 开发新视图时常先建好 Prefab 再写脚本，要求挂载会拖慢节奏，也阻碍未来"先生成代码再挂"的工作流。
- Prefab 名称与脚本类名一致已是项目里约定俗成的命名（`GameView.prefab` ↔ `GameView.cs`），用文件名匹配比 MonoBehaviour 反射更直白。
- 搜索范围限定在 `GameLogic/UI`，避免命中 EFRuntime 或其它模块的同名文件。

**已废弃方案**：扫描 GameObject 上的 MonoBehaviour 找 `EF.UI.UIView` 子类，用 `MonoScript.FromMonoBehaviour` 拿路径。该方案要求脚本已挂载到 GameObject，与开发节奏不符。

### 2. region 标识

**决策**：使用 `#region 自动生成` / `#endregion`，正则匹配 `#region\s+自动生成[\s\S]*?#endregion`。

**理由**：
- 规范明确要求该标识。
- 与旧 `#region 脚本工具生成(?:的)?代码` 在同一文件中可独立共存（如果将来有人同时跑两条生成路径，互不覆盖）。
- 每次生成"整块替换"——先删旧 region 再写新内容——满足用户的"不要重复定义"诉求。

### 3. `OnInitialize` 注入策略

**决策**：region 只包含字段声明；`OnInitialize` 的 `UHub.Initialize();` 单独维护，**不**纳入 region。

具体做法：
- 用正则匹配类体内的 `OnInitialize` 方法（兼容签名 `protected override void OnInitialize()`）。
- 在方法体内搜 `UHub.Initialize();`，已存在则跳过。
- 否则在 `base.OnInitialize();` 行之后插入一行 `UHub.Initialize();`，保持缩进对齐。
- 方法不存在时按规范"不自动创建"，仅生成字段并在控制台 `Debug.LogWarning` 提示用户手动补 `OnInitialize`。

**理由**：用户在 `OnInitialize` 里可能写了别的初始化代码（如 `EnsureRuntimeTextComponents()`），把整个方法塞进 region 会丢失这些。单独维护 `UHub.Initialize();` 一行最安全。

### 4. 字段命名风格

**决策**：`key` → `_camelCase`，保留后缀，不剥离。

| key | 字段名 | 类型推断 |
|------|--------|----------|
| `EndBtn` | `_endBtn` | `Button`（规则） |
| `InfoText` | `_infoText` | `TextMeshProUGUI`（规则） |
| `HandCardTemplate` | `_handCardTemplate` | `GameObject`（兜底） |
| `startGameBtn` | `_startGameBtn` | `Button` |

**理由**：与现有规范 `Scenario: 从 ReferenceCollector key 生成字段名` 一致，沿用 `ReferenceCollectorScriptGenerator.SanitizeFieldName` 的代码风格 `_xxxXxx`。

### 5. 现有手写字段的迁移（已修订：默认弹窗询问）

**问题**：`GameView.cs` / `MainView.cs` 已有手写 `[UHubBind]` 字段或 `public XXX _foo;` 字段，名字按现有规则推导后会与生成器输出**完全重名**。原始决策是「静默跳过」，导致 region 永远为空，用户难以察觉自己需要清理 region 外字段。

**修订决策**：检测到冲突时弹出 `EditorUtility.DisplayDialogComplex` 三选一对话框：

- **覆盖**：删除所有冲突字段的整行声明（含同行属性如 `[SerializeField]`），region 内重新生成全部 prefab 字段。
- **跳过**：保持冲突字段不变，region 内仅生成未冲突字段（原始行为）。
- **取消**：立即返回，不写入目标脚本。

**实现要点**：
- 纯函数 `UiScriptBinderTextRewriter.RemoveExternalFields(content, fieldNames)`：按字段名逐行清除整行声明。匹配模式 `^\s*(?:\[[^\]]*\]\s*)*(?:private|public|protected|internal)(?:\s+(?:readonly|static))*\s+\S+\s+<name>\s*[=;]`，每个名字最多删除 1 行（避免误伤同名局部变量；当前生成器只处理类成员字段）。
- 边界情况：属性在独立行（`[SerializeField]\nprivate Button _x;`）只删字段行，留下孤儿属性；用户在编辑器中可见编译错误后手动清理。
- 调用顺序：`DetectExternalFields` → 检测冲突 → 若有冲突弹窗 → 根据用户选择决定调用 `RemoveExternalFields` 还是过滤 `keptFields`。
- 日志：覆盖/跳过/取消都打印对应的审计行，便于回放。

**首次启用建议**：现有 `GameView.cs` / `MainView.cs` 的 region 外手写字段不再需要人工预清理，直接点按钮在弹窗里选「覆盖」即可一步迁移；若有自定义名称（如 `_endTurnButton`）与 key 不重名，可选「跳过」保留它们。

### 6. `using` 命名空间补充

**决策**：沿用旧 `ReferenceCollectorScriptGenerator.EnsureUsings` 逻辑（正则扫现有 `using`、按需补缺、按字典序插入到 using 块末尾）。新 binder 复用该工具方法（或独立实现一份，避免跨 internal 边界）。

**预计需要的命名空间**：
- `EF.UI`（`UHubBind` 特性）
- `UnityEngine`（`GameObject`, `RectTransform`）
- `UnityEngine.UI`（`Button`, `Image`）
- `TMPro`（`TextMeshProUGUI`）

### 7. 写文件方式与编译器反应

- 使用 `File.WriteAllText` + `UTF8Encoding(false)`（无 BOM）。
- 写完调用 `AssetDatabase.ImportAsset(assetPath)` + `AssetDatabase.Refresh()`，由 Unity 触发热更新程序集重新编译。
- HybridCLR DLL 重打包不需要在此步骤介入，开发者按平时流程跑 HybridCLR Build。

### 8. 不实现的事项（明确否决）

- ❌ 不读取/写入 `.prefab` 文件
- ❌ 不操作场景内 GameObject
- ❌ 不修改 `ReferenceCollector` 本身的 `data` 列表（生成只读 `data`）
- ❌ 不自动创建 `OnInitialize` 方法
- ❌ 不生成 UI Toolkit / UXML / VisualElement 相关代码（规范 `Requirement: 自动绑定必须继续面向 UGUI UIView` 明确禁止）

## 风险与缓解

| 风险 | 缓解 |
|------|------|
| 写文件无 Undo | 生成前以 `Debug.Log` 输出 region 旧/新内容差异，依赖 git 兜底 |
| 重名脚本（`GameView.cs` 项目里有重名） | 通过 GameObject 上挂载的具体 MonoScript 拿到唯一 AssetPath，不靠类名搜索 |
| 用户在 region 内手写代码被覆盖 | 文档明确 region 内部是只读区；首次启用前提示用户备份 |
| 现有 `GameView.cs` 手写字段名冲突 | 第 5 节的"region 外字段已存在则跳过" + 写入前打印审计列表 |
| ScriptableObject `ReferenceCollectorRuleSettings` 不存在 | 沿用 `ReferenceCollectorAutoCollectService` 已有的容错路径 |

## 与现有代码的接触面

- 复用：`ReferenceCollectorRuleService.FindFirstMatchingRule` 做类型推断
- 复用（拷贝或提炼共享 helper）：`ReferenceCollectorScriptGenerator.SanitizeFieldName` / `EnsureUsings` / `ReplaceRegion` 的核心算法
- 新增：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorUiScriptBinder.cs`
- 修改：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorEditor.cs` — `BuildAutoCollectOperations` 末尾移除占位注释、添加按钮

## 测试策略

- EditMode 测试（`GameLogic.Tests.EditMode` 程序集已存在）：
  - 给定一段假源代码字符串，验证 region 块插入/替换、`UHub.Initialize()` 注入、`using` 补充的纯函数行为
  - 不依赖 Unity AssetDatabase；用接口隔离文件读写
- 手动验证：
  - 在 `GameView.prefab` 选中根节点 → 点击 "添加变量到UI代码" → 检查 `GameView.cs` 的 region 块内容与 ReferenceCollector data 一致
  - 重复点击多次，region 内容不应叠加、不应出现重复字段
