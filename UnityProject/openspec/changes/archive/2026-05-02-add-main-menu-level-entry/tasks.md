## 1. AI 完成：主界面入口代码

- [x] 1.1 在主界面模型中增加默认关卡标识、展示名称和简短说明，并通过只读数据接口暴露给 View
- [x] 1.2 调整主界面进入逻辑，使打开主界面时展示默认关卡信息并保持开始按钮可交互
- [x] 1.3 定义轻量的默认关卡进入请求数据或事件，确保请求包含稳定默认关卡标识
- [x] 1.4 将开始按钮处理逻辑从旧占位反馈改为发起默认关卡进入请求
- [x] 1.5 在关卡流程尚未实现时保留可见的正向反馈或日志，表明默认关卡进入请求已发出

## 2. AI 完成：测试与规格验证

- [x] 2.1 更新主界面控制器 EditMode 测试，验证打开主界面后默认关卡信息和按钮状态正确
- [x] 2.2 增加或更新开始按钮测试，验证点击后会发出包含默认关卡标识的请求
- [x] 2.3 移除测试中对“未接入玩法逻辑”旧占位反馈的断言
- [x] 2.4 运行相关 EditMode 测试或记录无法运行的原因
  - 记录：已尝试运行 `Unity.exe -runTests -testPlatform EditMode -testFilter "GameLogic.Tests.EditMode.MainControllerTests"`，但当前 shell 找不到 `Unity.exe`；已尝试 `dotnet build "GameLogic.csproj" --no-restore -v:minimal`，但被现有 `Assets/GameScripts/HotFix/GameProto/ConfigSystem.cs` 中 `EF.Resrouce` 命名空间拼写错误阻断，未继续修改该非本变更范围文件。

## 3. 用户完成：Unity 编辑器与资源验证

- [X] 3.1 在 `MainView.prefab` 中保留或添加一个进入游戏按钮，建议对象名为 `StartGameButton`，按钮文案为“开始游戏”或“进入关卡”
- [X] 3.2 在 `MainView.prefab` 中添加默认关卡名称文本，建议对象名为 `LevelNameText`，用于显示类似“默认关卡”或“第一关：试炼”的标题
- [X] 3.3 在 `MainView.prefab` 中添加默认关卡说明文本，建议对象名为 `LevelDescriptionText`，用于显示当前关卡的简短说明
- [X] 3.4 在 `MainView.prefab` 中添加开始请求反馈文本，建议对象名为 `FeedbackText`，用于点击开始后显示“已发起默认关卡进入请求”等临时反馈
- [X] 3.5 将开始按钮、关卡名称文本、关卡说明文本和反馈文本绑定到 `MainView` 脚本可访问的字段或项目使用的引用收集组件中
- [X] 3.6 调整上述 UI 元素的位置、字号和锚点，确保 Play 模式下可见、可读、不会互相遮挡，也不会跑出屏幕
- [X] 3.7 确认 YooAsset 地址 `MainView` 仍可加载主界面 Prefab
- [X] 3.8 Play 验证从启动到主界面再点击开始按钮的路径，确认没有 Prefab 绑定错误或阻断性异常

## 4. 收尾

- [x] 4.1 对照 `single-main-ui-entry` delta spec 检查所有新增场景均已覆盖
- [x] 4.2 回到总路书 `add-card-rogue-core-loop`，在该模块完成后更新第一阶段对应路线项
