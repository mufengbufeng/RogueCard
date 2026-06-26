## MODIFIED Requirements

### Requirement: GameView Prefab 必须声明完整局内 UGUI 绑定

`Assets/AssetRaw/UI/Game/GameView.prefab` SHALL 在根对象上挂载 `GameView`、`Canvas`、`CanvasScaler`、`GraphicRaycaster` 与 `ReferenceCollector`。`ReferenceCollector` SHALL 至少包含以下 key：`BgImage`、`BattlePanel`、`RewardPanel`、`PlayerStatusPanel`、`InfoText`、`PlayerHpFill`、`PlayerHpText`、`PlayerArmorText`、`PlayerEnergyFill`、`PlayerEnergyText`、`PlayerBuffBar`、`MonsterRect`、`CardSc`、`DropZone`、`PreviewLayer`、`EndBtn`、`FailToast`、`RewardConfirmBtn`、`HandCardTemplate`、`MonsterItemTemplate`、`BuffIconTemplate`、`IntentIconTemplate`。这些 key SHALL 与 Prefab 节点名或被绑定组件所在节点名保持一致，并 SHALL 能由 `GameView.cs` 中同名驼峰字段通过 `[UHubBind("Key")]` 绑定；不得通过旧名、别名或运行时 `transform.Find` 兜底替代这些必需绑定。

#### Scenario: Prefab 引用完整

- **WHEN** Unity 加载 `Assets/AssetRaw/UI/Game/GameView.prefab`
- **THEN** Prefab 根对象 SHALL 存在 `ReferenceCollector`
- **AND** `ReferenceCollector` SHALL 能通过所有必需 key 取到非空对象或组件

#### Scenario: 绑定命名与代码字段一致

- **WHEN** `ReferenceCollector` 中存在 key `PlayerHpFill`、`DropZone`、`HandCardTemplate` 或 `BattlePanel`
- **THEN** `GameView.cs` SHALL 存在对应 `[UHubBind("PlayerHpFill")] private Image _playerHpFill;`、`[UHubBind("DropZone")] private RectTransform _dropZone;`、`[UHubBind("HandCardTemplate")] private GameObject _handCardTemplate;`、`[UHubBind("BattlePanel")] private GameObject _battlePanel;` 等字段
- **AND** 字段名 SHALL 由 key 首字母小写并添加 `_` 前缀得到

#### Scenario: 运行时不依赖动态兜底 UI

- **WHEN** `GameView.OnOpen` 执行
- **THEN** `GameView` SHALL 使用 Prefab 引用初始化子视图
- **AND** SHALL NOT 为正式战斗 UI 临时创建 `PhaseTextRuntime`、`HandCommandPanelRuntime` 或 `MonsterCommandPanelRuntime`
