## Why

ReferenceCollectorEditor 目前只有"自动收集"按钮（根据命名规则收集子节点引用），但缺少从已收集的引用数据反向生成 UIView 脚本绑定代码的能力。开发者需要手动编写 `private Button _startGameBtn;` 字段声明、`UHub.Initialize()` 调用等样板代码，容易遗漏且效率低。

## What Changes

- 在 ReferenceCollectorEditor 的"自动收集"操作区新增"自动绑定UI脚本"按钮
- 点击后读取当前 ReferenceCollector 中所有有效引用数据，根据 key 和引用对象类型生成 UIView 脚本绑定代码：
  - 字段声明：`private 类型 _xxxXxx;`（key 首字母小写 + 加 `_` 前缀，如 key `StartGameBtn` → 字段 `_startGameBtn`）
  - `UHub.Initialize()` 调用插入 `OnInitialize()` 方法（如方法不存在则创建，如已有则跳过不重复添加）
  - 类型推断：优先使用引用对象的实际组件类型，结合 `ReferenceCollectorRuleService` 规则和 `UHubBindingConfig` 后缀规则
- 自动生成代码用 `#region 自动生成` / `#endregion` 包围，支持增量更新
- 与已有 `ReferenceCollectorScriptGenerator`（UIWindow/BindMemberProperty 模式）互不干扰

## Capabilities

### New Capabilities
- `auto-bind-ui-script`: 从 ReferenceCollector 数据生成 UIView 脚本的 UHub 自动绑定代码（字段声明 + UHub.Initialize 调用 + region 包围 + 增量更新）

### Modified Capabilities
（无）

## Impact

- **编辑器代码**：`ReferenceCollectorEditor.cs` 新增按钮；新增 `ReferenceCollectorUiScriptBinder.cs`（代码生成逻辑）
- **运行时代码**：无影响（纯编辑器工具）
- **依赖**：复用现有 `ReferenceCollectorRuleService`（类型推断规则）和 `ComponentBinder` 的命名映射逻辑（`_xxxXxx` → `XxxXxx`）
- **已有 ScriptGenerator**：不受影响，两者共存，分别服务于 UIWindow（BindMemberProperty 模式）和 UIView（UHub 模式）
