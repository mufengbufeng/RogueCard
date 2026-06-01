## ADDED Requirements

### Requirement: 自动收集规则必须覆盖 GameView 规范命名节点

ReferenceCollector 自动收集与脚本绑定生成 SHALL 支持 `GameView.prefab` 使用的项目命名规范。对于符合规范的 UGUI 节点后缀，系统 SHALL 能收集并推断适合的绑定类型：`Panel`、`Template` 为 `GameObject`；`Fill` 为 `Image`；`Bar`、`Layer`、`Zone`、`Rect`、`Sc` 为 `RectTransform`；`Btn` 为 `Button`；`Text` 为 `TextMeshProUGUI` 或项目现行文本类型规则。自动收集 SHALL NOT 因这些 key 不匹配旧的通用后缀而从 `ReferenceCollector` 中删除 `GameView` 必需绑定。

#### Scenario: GameView 关键节点可自动收集

- **WHEN** 对包含 `BattlePanel`、`RewardPanel`、`PlayerHpFill`、`PlayerBuffBar`、`DropZone`、`PreviewLayer`、`CardSc`、`HandCardTemplate` 的 `GameView.prefab` 执行自动收集
- **THEN** `ReferenceCollector` SHALL 包含这些 key
- **AND** 每个 key SHALL 引用非空对象或组件

#### Scenario: 自动绑定生成符合 UHub 字段类型

- **WHEN** ReferenceCollector 中包含 `PlayerHpFill`、`DropZone`、`PreviewLayer`、`BattlePanel`、`HandCardTemplate`
- **THEN** 自动绑定 SHALL 生成或保留 UGUI 字段类型 `Image _playerHpFill`、`RectTransform _dropZone`、`RectTransform _previewLayer`、`GameObject _battlePanel`、`GameObject _handCardTemplate`
- **AND** SHALL NOT 生成 UI Toolkit 字段或旧命名别名字段

#### Scenario: 重新生成不会删除 GameView 必需绑定字段

- **WHEN** `GameView.prefab` 的 ReferenceCollector 已包含 `gameview-ugui-prefab-composition` 规定的必需 key
- **AND** 用户点击“添加变量到UI代码”重新生成 `GameView.cs` 自动生成区
- **THEN** 自动生成区 SHALL 保留所有必需 key 对应的 `[UHubBind]` 字段
- **AND** Unity C# 编译 SHALL NOT 出现这些字段名的 CS0103 错误
