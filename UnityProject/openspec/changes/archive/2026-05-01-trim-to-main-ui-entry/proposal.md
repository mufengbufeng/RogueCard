## Why

当前项目是从框架示例克隆而来，默认携带登录、体力、关卡、战斗等样板游戏逻辑，会干扰它作为新游戏项目起点的使用。需要先将工程裁剪为“可启动、可显示、可继续扩展”的最小骨架，只保留一个主界面入口，降低后续接入真实业务逻辑的改造成本。

## What Changes

- 将启动后的用户可见入口收敛为单一主界面，移除 `EntryView -> MainView` 的双层入口路径。
- 停用并清理与示例玩法强耦合的流程、控制器逻辑、模块注册和资源依赖，包括登录流程、开始战斗切换、体力/关卡/战斗相关活跃路径。
- 保留 `GameEntry -> GameLogicEntry` 的 Runtime/HotFix 启动骨架，以及 EF 的 UI 管理能力，确保项目仍能正常进入热更新并打开主界面。
- 删除或更新不再需要的示例代码、测试和资源引用，避免残留失效入口或无效依赖。

## Capabilities

### New Capabilities
- `single-main-ui-entry`: 项目启动后仅保留 `MainView` 作为唯一可见入口，并且该入口不再依赖示例玩法流程或模块。

### Modified Capabilities
（无）

## Impact

- HotFix 启动与流程链路：`GameLogicEntry`、`InitProcedure`、`LoginProcedure`、`MainMenuProcedure`、`GamePlayProcedure`
- 主界面与入口 UI：`UI/Main/*`、`UI/Entry/*` 及对应 Prefab/资源路径
- 示例玩法相关模块：体力、关卡、战斗、场景与实体相关代码
- 受影响测试与配置：依赖上述示例逻辑的 EditMode 测试、资源引用与启动配置
