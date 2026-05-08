## Context

ReferenceCollectorEditor 已有"自动收集"功能（根据命名规则收集子节点引用到 ReferenceCollector），也有 `ReferenceCollectorScriptGenerator`（生成 UIWindow 脚本的 BindMemberProperty 模式）。但 UIView（热更新层）使用 UHub 自动绑定模式，需要手动编写字段声明和 `UHub.Initialize()` 调用。

UHub 的 `ComponentBinder` 通过字段名推断 ReferenceCollector key：`_startGameBtn` → `StartGameBtn`（去 `_` 后首字母大写）。反向生成时：key `StartGameBtn` → 字段 `_startGameBtn`（加 `_` 后首字母小写）。

现有文件位置：
- 编辑器入口：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorEditor.cs`
- 已有代码生成器：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorScriptGenerator.cs`
- 规则服务：`Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorRuleService.cs`

## Goals / Non-Goals

**Goals:**
- 在 ReferenceCollectorEditor 的自动收集操作区添加"自动绑定UI脚本"按钮
- 点击后读取 ReferenceCollector 数据，生成 UIView 脚本的字段声明和 UHub 初始化代码
- 支持增量更新：已有字段不重复生成，region 块整体替换
- 自动推断组件类型（基于 ReferenceCollectorRuleService 规则和实际对象类型）

**Non-Goals:**
- 不修改 `ReferenceCollectorScriptGenerator`（两者共存）
- 不修改运行时代码（纯编辑器工具）
- 不自动查找目标脚本文件（由用户手动指定或从 GameObject 上的 MonoBehaviour 推断）

## Decisions

### 1. 新建独立的代码生成类

创建 `ReferenceCollectorUiScriptBinder.cs`，不修改已有的 `ReferenceCollectorScriptGenerator`。原因：
- 两者服务不同的基类（UIView vs UIWindow）和绑定模式（UHub vs BindMemberProperty）
- region 标记不同（`自动生成` vs `脚本工具生成的代码`）
- 独立文件便于维护和测试

### 2. 目标脚本定位策略

通过 `MonoScript.FromGameObject` 查找 GameObject 上继承自 UIView 的脚本，获取其文件路径。如果找不到 UIView 子类，则提示用户。

### 3. 类型推断策略

与 `ReferenceCollectorScriptGenerator.CreateBinding()` 一致：
1. 首先查询 `ReferenceCollectorRuleService.FindFirstMatchingRule(key)` 获取规则
2. 如果规则指定了组件类型，从 GameObject 上 `GetComponent` 获取实际类型
3. 如果引用对象本身是 Component，直接使用其类型
4. 默认 fallback 为 `GameObject`

### 4. 代码注入策略

使用正则匹配 `#region 自动生成` / `#endregion` 块进行整体替换。如果脚本中没有该 region，则在类体顶部插入。同时检查 `OnInitialize()` 方法中是否已有 `UHub.Initialize()` 调用，避免重复。

### 5. using 命名空间处理

收集所有字段类型所需的命名空间，与脚本中已有的 using 比对，仅添加缺失的。

## Risks / Trade-offs

- **[目标脚本不存在]** → 提供清晰提示，要求用户先创建 UIView 子类脚本
- **[命名冲突]** → 字段名去重机制，同名 key 跳过不生成
- **[partial 类不支持]** → C# 不支持对非 partial 类添加方法，OnInitialize 生成需要类是 partial 或用户手动添加方法签名。决定：仅在已有 OnInitialize 方法中插入 `UHub.Initialize()` 调用，如方法不存在则仅生成字段声明
- **[region 与手动代码混合]** → region 块整体替换，用户不应在 region 内手动编辑代码
