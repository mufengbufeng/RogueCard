# ui-framework-docs Specification

## Purpose

把 `Assets/EF/EFRuntime/UI/README.md` 必须解释的 UGUI UI 框架知识建模为可验证的文档契约：覆盖架构概览、核心类型 API、窗口生命周期、UHub / ReferenceCollector 绑定模式、Procedure 协作示例和测试入口。后续如果新增 / 重命名 UI 框架类型，可以通过修改本 capability 的 requirement / scenario 触发 README 同步。
## Requirements
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
