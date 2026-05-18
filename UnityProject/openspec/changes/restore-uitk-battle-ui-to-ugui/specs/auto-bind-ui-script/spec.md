## MODIFIED Requirements

### Requirement: 自动绑定必须继续面向 UGUI UIView

ReferenceCollectorEditor 的"自动绑定UI脚本"能力 SHALL 继续定位并修改继承自 `UIView` 的脚本，生成字段、using 和 `UHub.Initialize()` 调用。该能力 SHALL NOT 生成 UXML、USS、VisualElement 查询代码或 UI Toolkit 回调代码。新增或调整 `GameView.prefab` 局内 UGUI 引用后，自动绑定 SHALL 能按 ReferenceCollector key 生成 UGUI 组件字段或允许手写字段通过 `[UHubBind]` 绑定。

#### Scenario: 生成 UGUI Button 字段
- **WHEN** ReferenceCollector 中存在 key 为 `StartGameBtn`、引用为 UGUI Button 的条目
- **THEN** 自动绑定 SHALL 生成 `private Button _startGameBtn;`
- **AND** SHALL 补充 `using UnityEngine.UI;`

#### Scenario: 不生成 VisualElement 查询
- **WHEN** 用户点击"自动绑定UI脚本"
- **THEN** 生成结果 SHALL NOT 包含 `UnityEngine.UIElements`
- **AND** SHALL NOT 包含 `Root.Q`、`VisualElement` 或 `RegisterCallback<ClickEvent>`

#### Scenario: GameView 新增战斗 UI 引用仍使用 UGUI 类型
- **WHEN** ReferenceCollector 中存在 `EndBtn`、`MonsterRect`、`CardSc`、`PreviewLayer`、`RewardConfirmBtn`
- **THEN** 自动绑定或手写绑定 SHALL 使用 `Button`、`RectTransform`、`CanvasGroup`、`TextMeshProUGUI`、`Image` 等 UGUI/TMP 类型
- **AND** SHALL NOT 生成 UXML、USS 或 `VisualTreeAsset` 字段
