## 1. 收敛默认启动路径

- [x] 1.1 调整 `InitProcedure`，移除体力、关卡和配置表样例初始化，只保留项目启动所需的最小初始化。
- [x] 1.2 将初始化完成后的流程切换改为直接进入主界面流程，不再进入 `LoginProcedure`。
- [x] 1.3 调整 `GameLogicEntry` 的模型与流程初始化，确保默认流程链只依赖主界面所需类型。

## 2. 简化主界面入口

- [x] 2.1 调整 `MainMenuProcedure`，使其只负责打开和关闭 `MainView`，不再切换到 `GamePlayProcedure`。
- [x] 2.2 简化 `MainController`，移除体力、关卡、计时器和玩法流程依赖，使主界面可独立初始化。
- [x] 2.3 简化 `MainView` 显示内容与按钮反馈，确保未接入真实业务时仍能作为占位主页使用。
- [x] 2.4 删除或停用 `EntryView`、`EntryController`、`LoginProcedure` 及对应入口资源引用。

## 3. 清理示例玩法代码

- [x] 3.1 删除或停用体力、关卡、战斗、敌人、子弹、玩法场景等示例模块及其默认引用。
- [x] 3.2 删除或更新依赖示例玩法逻辑的 EditMode 测试，保留仍验证 EF 框架能力的测试。
- [x] 3.3 清理无用 using、asmdef 引用、资源路径和 Prefab 脚本引用，确保编译链路不再引用被移除类型。

## 4. 验证

- [x] 4.1 运行全局搜索，确认默认启动链和主界面入口不再活跃引用 `EntryView`、`LoginProcedure`、`GamePlayProcedure`、体力或关卡示例模块。
- [x] 4.2 运行 Unity EditMode 测试或等效编译验证，确认 HotFix 程序集和测试程序集编译通过。
- [x] 4.3 在 Unity 中启动入口场景，确认项目完成热更初始化后直接打开 `MainView`，且无示例玩法错误日志。
