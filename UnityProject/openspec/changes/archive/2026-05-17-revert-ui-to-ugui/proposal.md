## Why

当前运行时 UI 已迁移到 UI Toolkit，但项目希望回到基于 UGUI / Canvas / Prefab 的 UI 框架，以继续使用旧版 `UIManager`、`UIView`、`UIController`、`ReferenceCollector` 与 UHub 自动绑定工作流。

这次回退需要先形成明确规格，因为 UITK 已经进入导航、主界面、局内战斗 UI、资源和测试契约，直接恢复历史代码会与现有 OpenSpec 主规格冲突。

## What Changes

- **BREAKING**：移除运行时 UITK UI 框架契约，废弃 `Navigator`、`Shell`、`Screen<TViewModel>`、`Popup<TViewModel>`、`Region`、运行时 UXML/USS 加载和 `UIDocument` 初始化路径。
- **BREAKING**：将 UI 导航契约改回 UGUI `IUIManager.OpenWindowAsync<TView,TController>()`，通过 Prefab addressable 加载窗口，并按 `UILayer` 挂到 Canvas 层级根节点。
- **BREAKING**：将主界面和局内界面的入口流程从 `MainViewModel/GameViewModel + Navigator.OpenAsync` 改为 `UIView + UIController + UIManager.OpenWindowAsync`。
- 从 Git 历史或当前 `_OLD` 备份 Prefab 恢复旧 UGUI 框架代码与资源，并在此基础上适配当前卡牌 Rogue 战斗系统。
- 移除 `Assets/AssetRaw/UI/**/*.uxml`、`*.uss` 运行时资源，以及依赖 `VisualElement` 的运行时 UI 视图代码和测试。
- 保留当前玩法系统的非 UI 逻辑边界：`GameModel`、`CardSystem`、`MonsterSystem`、`BattleSystem`、`WaveSystem`、本地事件总线和配置表读取应继续由流程层管理。
- 编辑器工具中的 UIElements 使用不纳入本变更默认范围；本变更只移除游戏运行时 UI Toolkit 路径。

## Capabilities

### New Capabilities

- `ugui-ui-framework`: 定义恢复后的 UGUI UI 框架契约，包括 UIManager、UIView、UIController、UILayer、Prefab 加载、层级根节点注册和生命周期。

### Modified Capabilities

- `ui-navigation`: 将 Navigator / Shell / Screen 导航要求替换为 UGUI UIManager 窗口导航要求。
- `ui-screen-conventions`: 将 Screen / ViewModel / UXML / USS 四件套命名约定替换为 UIView / UIController / Prefab / ReferenceCollector 约定。
- `ui-region`: 移除 UITK Region 的运行时要求，避免继续要求 VisualElement 插槽和 UXML 子区域加载。
- `ui-framework-docs`: README 必须反映 UGUI MVC 架构，并移除运行时 UITK 框架说明。
- `single-main-ui-entry`: 主界面仍是唯一入口，但打开方式改为 UGUI `MainView` 窗口。
- `main-to-game-view-flow`: 主菜单到局内流程仍保留，但 UI 打开/关闭方式改为 `IUIManager`，且 Controller/View 承接 UGUI 交互。
- `auto-bind-ui-script`: 自动绑定继续服务于 UGUI `UIView` + `ReferenceCollector`，不再服务于 UXML/VisualElement。

## Impact

- 影响 AOT Runtime：`GameEntry` 需要重新注册 `IUIManager`，恢复 `UIManager` 对 `IResourceManager` 和 `ModelManager` 的依赖。
- 影响 HotFix 入口：`GameLogicEntry` 需要从 Entry/ReferenceCollector 读取 Background、Normal、Popup、Overlay、UICamera 层级并注册到 UIManager。
- 影响 EF.UI：恢复或重建 `UIManager`、`UIView`、`UIController`、`UIRuntimeContext`、`UIWindowDescriptor`、`UIWindowHandle`、`UIWindowState`、`UIBindingCollection`、`ControllerEventBinder`、`UHub` 相关类型。
- 影响资源：运行时 UI 使用 UGUI Prefab，不再使用 `Root.uxml`、`MainUxml`、`GameUxml`、`BattlePanel`、`RewardPanel`、`CardItem`、`MonsterItem` 等 UXML/USS 资源。
- 影响测试：删除或改写依赖 UI Toolkit 的 EditMode 测试，新增 UGUI UIManager、主菜单 Controller、主菜单到局内流程和关键战斗 UI 绑定测试。
- 影响文档与 OpenSpec 主规格：当前仍描述 UITK 的规格需要在本变更归档时同步为 UGUI 契约。
