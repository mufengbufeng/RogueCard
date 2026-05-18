# ui-navigation Specification

## Purpose

定义 UGUI UI 框架的窗口导航契约。运行时界面通过 `IUIManager.OpenWindowAsync<TView,TController>()` 打开 UGUI Prefab 窗口，并通过 Background / Normal / Popup / Overlay 四层 Transform 管理显示顺序；流程离开时通过 `CloseWindowAsync` 或 `CloseAllAsync` 清理窗口。
## Requirements
### Requirement: UI 导航必须通过 IUIManager 打开和关闭窗口

运行时 UI 导航 SHALL 通过 `IUIManager` 完成。调用方 SHALL 使用 `OpenWindowAsync<TView,TController>()` 打开窗口，并在流程离开或关闭弹窗时使用 `CloseWindowAsync(windowName)` 或 `CloseAllAsync()` 清理窗口。

#### Scenario: 主菜单流程打开 MainView
- **WHEN** `MainMenuProcedure.OnEnter` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.OpenWindowAsync<MainView, MainController>("MainView", UILayer.Normal, ...)`
- **AND** SHALL NOT 调用 `Navigator.OpenAsync`

#### Scenario: 局内流程打开 GameView
- **WHEN** `GameProcedure.OnEnter` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.OpenWindowAsync<GameView, GameController>("GameView", UILayer.Normal, ...)`
- **AND** SHALL NOT 加载 `GameUxml`

#### Scenario: 流程离开时关闭所属窗口
- **WHEN** `MainMenuProcedure.OnLeave` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.CloseWindowAsync("MainView")`
- **WHEN** `GameProcedure.OnLeave` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.CloseWindowAsync("GameView")`
