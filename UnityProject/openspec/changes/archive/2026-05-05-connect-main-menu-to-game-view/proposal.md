## Why

当前主界面开始按钮只发布默认关卡进入请求，运行时没有订阅者承接该请求，用户点击后不会真正离开主界面或进入局内 UI。现在 `GameView.prefab` 已存在，且局内表现骨架变更需要一个从主菜单进入局内窗口的稳定入口，因此需要补齐主菜单流程到局内流程的衔接。

## What Changes

- 主界面点击开始游戏后必须触发实际流程切换，而不再只停留在“请求已发出”的临时反馈。
- 主菜单流程负责承接默认关卡进入请求，并切换到局内流程。
- 主菜单流程离开时关闭 `MainView`，避免主界面与局内 UI 同时显示。
- 新增局内流程打开 `GameView` 的最小能力，并使用现有 EF MVC UI 打开约定加载 `Assets/AssetRaw/UI/Game/GameView.prefab`。
- 新增最小 `GameView`/`GameController` 脚本骨架，使 `GameView.prefab` 能被 EF UI 系统作为窗口打开。
- 不在本变更中实现完整战斗规则、卡牌效果、怪物 AI、波次推进或局内数据同步。

## Capabilities

### New Capabilities
- `main-to-game-view-flow`: 定义从主界面开始按钮进入局内流程、关闭 `MainView` 并打开 `GameView` 的最小运行时行为。

### Modified Capabilities
- `single-main-ui-entry`: 将开始按钮行为从“只发起可验证请求”升级为“请求被主菜单流程承接并进入局内 UI”。

## Impact

- 影响 HotFix 层主界面控制器、主菜单流程、流程注册、局内 UI 脚本和相关 EditMode 测试。
- 影响 `Assets/AssetRaw/UI/Game/GameView.prefab` 的运行时打开契约，但不要求修改资源收集规则；现有 UI 资源组已使用 `AddressByFileName`。
- 需要与现有 `add-game-scene-battle-presentation` 变更保持边界一致：本变更只补齐入口流程，不实现局内舞台和战斗表现细节。
