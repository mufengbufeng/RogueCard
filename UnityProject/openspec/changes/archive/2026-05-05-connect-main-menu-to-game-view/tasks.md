## 1. 局内 UI 最小骨架

- [x] 1.1 创建 `GameView : UIView`，提供可被 EF UI 系统实例化和初始化的最小局内窗口 View 类型
- [x] 1.2 创建 `GameController : UIController`，提供可被 EF UI 系统创建和释放的最小局内窗口 Controller 类型
- [x] 1.3 确认 `GameView`/`GameController` 位于 HotFix `GameLogic` 程序集路径下，且不引入 Runtime 到 HotFix 的反向依赖

## 2. 局内流程接入

- [x] 2.1 新增 `GameProcedure`，在进入流程时通过 EF UI 打开 `GameView`，资源地址使用 `"GameView"`
- [x] 2.2 在 `GameProcedure` 离开流程时关闭 `GameView`，保持窗口生命周期由流程管理
- [x] 2.3 在 `GameLogicEntry.InitializeProcedures()` 中注册 `GameProcedure`

## 3. 主菜单到局内流程切换

- [x] 3.1 在 `MainMenuProcedure.OnEnter()` 中订阅 `StartLevelRequestedEvent`
- [x] 3.2 在收到默认关卡进入请求后使用流程状态切换进入 `GameProcedure`，不得重新调用流程启动接口
- [x] 3.3 在 `MainMenuProcedure.OnLeave()` 或销毁阶段解除事件订阅，并继续关闭 `MainView`
- [x] 3.4 调整 `MainController.HandleStartGame()` 的反馈逻辑，避免切换成功路径仍显示“只发起请求”的临时占位文案

## 4. 验证与测试

- [x] 4.1 更新或新增 EditMode 测试，验证开始按钮仍发布携带默认关卡标识的 `StartLevelRequestedEvent`
- [x] 4.2 新增流程级测试或可测试封装，验证主菜单流程收到请求后会切换到局内流程
- [x] 4.3 新增或更新 UI/流程测试，验证局内流程会请求打开 `GameView` 且主菜单流程离开时请求关闭 `MainView`
- [ ] 4.4 运行可用的 EditMode 测试或 Unity 编译检查，确认新增脚本无编译错误
- [ ] 4.5 在 Unity 编辑器中人工验证点击主界面开始按钮后关闭 `MainView` 并打开 `GameView`
