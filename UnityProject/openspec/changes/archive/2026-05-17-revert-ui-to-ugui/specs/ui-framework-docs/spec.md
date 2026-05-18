## REMOVED Requirements

### Requirement: README 必须反映当前 UITK + MVVM 架构

**Reason**: 运行时 UI 框架回退到 UGUI MVC，README 不应继续描述 UITK + MVVM 作为当前架构。

**Migration**: README SHALL 描述 UGUI `UIManager`、`UIView`、`UIController`、`UIWindowDescriptor`、`UIRuntimeContext`、`UHub` 与 ReferenceCollector。

### Requirement: README 必须解释 Shell 三层布局与 Root.uxml 约定

**Reason**: Shell 与 Root.uxml 被移除。

**Migration**: README SHALL 解释 UGUI Canvas 下 Background / Normal / Popup / Overlay 四层 Transform 约定。

### Requirement: README 必须给出 Navigator 完整生命周期

**Reason**: Navigator 被移除。

**Migration**: README SHALL 给出 UIManager 打开、刷新、关闭、缓存、释放窗口的生命周期顺序。

### Requirement: README 必须演示 ReactiveProperty 数据绑定模式

**Reason**: UGUI MVC 不再以 Screen/ViewModel/ReactiveProperty 作为标准 UI 绑定模式。

**Migration**: README SHALL 演示 UIView 读取 UHub 绑定组件、UIController 订阅 View 事件、ModelManager 提供数据模型的标准模式。

### Requirement: README 必须给出从 Procedure 到 Screen 的最小可运行路径

**Reason**: Screen 注册和 Navigator 路径被移除。

**Migration**: README SHALL 给出从 Procedure 调用 `IUIManager.OpenWindowAsync<TView,TController>()` 到 Controller/View 初始化的最小路径。

### Requirement: README 必须区分活代码与遗留代码

**Reason**: UGUI 类型恢复为活代码，不再是遗留说明。

**Migration**: README SHALL 标注运行时 UI 使用 UGUI，并说明编辑器工具或第三方包中的 UIElements 不属于运行时 UI 框架。

### Requirement: README 必须提供测试入口指引

**Reason**: UITK 相关测试入口会被删除或改写。

**Migration**: README SHALL 列出 UGUI UIManager、UIView/UIController、主菜单流程和 ReferenceCollector 相关 EditMode 测试入口。

### Requirement: README 必须保持可读体量与中文行文

**Reason**: 该要求仍然适用，但 README 内容主题改变。

**Migration**: 在新增 UGUI 文档要求中保留中文和体量约束。

## ADDED Requirements

### Requirement: README 必须反映当前 UGUI MVC 架构

`Assets/EF/EFRuntime/UI/README.md` SHALL 描述目录下当前实际存在的 UGUI MVC 框架，至少覆盖 `IUIManager`、`UIManager`、`UIView`、`UIController`、`UIWindowDescriptor`、`UIWindowHandle`、`UIRuntimeContext`、`UILayer`、`UHub`、`ReferenceCollector` 与 `UIBindingCollection`。

#### Scenario: README 覆盖 UGUI 核心类型
- **WHEN** 在 README 中搜索 UGUI UI 框架核心类型名
- **THEN** 每个核心类型 SHALL 至少出现一次
- **AND** 其上下文 SHALL 给出该类型职责说明

#### Scenario: README 不再把 UITK 作为运行时框架
- **WHEN** 在 README 中搜索 `Navigator`、`Screen<TViewModel>`、`Shell`、`Root.uxml`、`UIDocument`
- **THEN** README SHALL NOT 将这些标识符描述为当前运行时 UI 框架的必需路径

### Requirement: README 必须说明 UGUI 层级和生命周期

README SHALL 说明 UGUI Canvas 下 Background / Normal / Popup / Overlay 四层 Transform 约定，并给出 UIManager 打开窗口、关闭窗口、缓存窗口和释放窗口的生命周期。

#### Scenario: README 列出四个层级职责
- **WHEN** 读者阅读 README 的层级章节
- **THEN** README SHALL 同时出现 Background、Normal、Popup、Overlay
- **AND** SHALL 说明这些层级通过 `RegisterLayerRoot` 注册

#### Scenario: README 描述窗口生命周期顺序
- **WHEN** 读者阅读 README 的生命周期章节
- **THEN** README SHALL 描述 Controller.Initialize、View.Initialize、View.Open、Controller.Enter、Controller.Exit、View.Close、Release 的顺序

### Requirement: README 必须保持中文和可读体量

README 的自然语言 SHALL 使用中文（简体），整体长度 SHALL 控制在合理范围内。

#### Scenario: 自然语言使用中文
- **WHEN** 阅读 README 章节标题与正文段落
- **THEN** 所有自然语言内容 MUST 使用中文（简体）
- **AND** 类名、方法名、字段名和资源地址 SHALL 保留英文原样

#### Scenario: 文件体量控制
- **WHEN** 测量 `Assets/EF/EFRuntime/UI/README.md` 行数
- **THEN** 总行数 MUST ≤ 500 行
