## Why

`ReferenceCollectorEditor` 在 MVVM 重构期间移除了"从 ReferenceCollector 反向生成 UIView 脚本字段"的能力，只留下一行注释 `// UHub 自动绑定已移除（UI 框架重设计为 MVVM 模式）`。当前手写 `[UHubBind("Key")] private XxxX _xxx;` 字段易遗漏、易拼写错误，且 `GameView.cs` 已有 19 个手写字段佐证了痛点。规范 `auto-bind-ui-script` 仍存在并准确描述了所需行为，仅缺少实现。

## What Changes

- 在 `ReferenceCollectorEditor.BuildAutoCollectOperations` 移除旧注释，重新加入按钮，按钮标签为 **"添加变量到UI代码"**（沿用现有规范的行为，仅文案改为更贴近开发者直觉的描述）。
- 新增编辑器类 `ReferenceCollectorUiScriptBinder`（位于 `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/`），实现：
  - 按 GameObject 上挂载的 `UIView` 子类脚本定位目标 `.cs` 文件
  - 按 `ReferenceCollector.data` 生成 `[UHubBind("Key")] private 类型 _字段名;` 声明，整体放入 `#region 自动生成` / `#endregion` 块
  - 整块替换：每次重新生成都先删除旧 region 内容再写新内容，避免重复定义
  - 检测并在 `OnInitialize()` 的 `base.OnInitialize();` 之后注入 `UHub.Initialize();`（已存在则跳过）
  - 类型推断优先级：实际组件类型 > `ReferenceCollectorRuleService` 规则 > `GameObject`
  - 自动补充缺失的 `using`（如 `UnityEngine.UI`、`TMPro`）
- 现有 `ReferenceCollectorScriptGenerator`（针对旧 `UIWindow` / `BindMemberProperty` 模式）保留不动，两条生成路径互不干扰。

## Capabilities

### Modified Capabilities

- `auto-bind-ui-script`: 按钮标签从 "自动绑定UI脚本" 调整为 "添加变量到UI代码"；生成的字段声明 SHALL 携带 `[UHubBind("Key")]` 特性（沿用现状），与 `GameView.cs` 现有手写形态一致。其余要求保持不变。

## Impact

- **编辑器代码**：
  - `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorEditor.cs` — 在 `BuildAutoCollectOperations` 增加按钮行（替换旧注释）
  - 新增 `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorUiScriptBinder.cs`
- **运行时代码**：无影响
- **首次启用迁移风险**：`GameView.cs` 已有手写 `[UHubBind]` 字段，字段名按现有规则推导后会与生成器输出**完全重名**。首次点击按钮前需要人工清空旧的手写 region 外字段，或生成器需弹出冲突预览对话框让用户确认。
- **共存约束**：`ReferenceCollectorScriptGenerator`（旧 UIWindow）与新 binder 互不引用、各自的 region 标识不同（旧 `#region 脚本工具生成的代码`，新 `#region 自动生成`），不会互相覆盖。
