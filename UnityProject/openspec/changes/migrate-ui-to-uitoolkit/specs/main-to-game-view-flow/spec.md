## MODIFIED Requirements

### Requirement: GameView 不再需要 MonoBehaviour 动态挂载
GameView SHALL 为纯 C# 类（继承 UIView），不再需要 MonoBehaviour 组件挂载。UIManager 通过 `Activator.CreateInstance(typeof(GameView))` 创建实例，不再需要从 GameObject 获取或 AddComponent。

#### Scenario: 打开 GameView 窗口
- **WHEN** GameProcedure 调用 UIManager.OpenWindowAsync<GameView, GameController> 并传入 UXML 资源路径
- **THEN** UIManager SHALL 通过 Activator.CreateInstance 创建 GameView 实例
- **AND** 通过 CloneTree 加载 UXML 资源，将 VisualElement Root 传入 GameView.InternalInitialize

### Requirement: GameController 接收 int 类型关卡标识符
GameController.OnEnter SHALL 接收 int 类型关卡标识符（通过 userData），用于构建关卡运行时上下文。此行为与之前版本保持一致，不因 UIView 类型变更而改变。

#### Scenario: 正常关卡进入
- **WHEN** GameProcedure 传入有效的 levelId
- **THEN** GameController.OnEnter SHALL 使用 levelId 从配置表加载关卡数据，创建 WaveSystem

#### Scenario: 无效关卡标识符
- **WHEN** GameProcedure 传入无效的 levelId
- **THEN** GameController SHALL 记录错误日志，但 GameView 仍可打开
