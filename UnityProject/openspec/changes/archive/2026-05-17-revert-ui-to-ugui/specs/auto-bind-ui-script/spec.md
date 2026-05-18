## ADDED Requirements

### Requirement: 自动绑定必须继续面向 UGUI UIView

ReferenceCollectorEditor 的"自动绑定UI脚本"能力 SHALL 继续定位并修改继承自 `UIView` 的脚本，生成字段、using 和 `UHub.Initialize()` 调用。该能力 SHALL NOT 生成 UXML、USS、VisualElement 查询代码或 UI Toolkit 回调代码。

#### Scenario: 生成 UGUI Button 字段
- **WHEN** ReferenceCollector 中存在 key 为 `StartGameBtn`、引用为 UGUI Button 的条目
- **THEN** 自动绑定 SHALL 生成 `private Button _startGameBtn;`
- **AND** SHALL 补充 `using UnityEngine.UI;`

#### Scenario: 不生成 VisualElement 查询
- **WHEN** 用户点击"自动绑定UI脚本"
- **THEN** 生成结果 SHALL NOT 包含 `UnityEngine.UIElements`
- **AND** SHALL NOT 包含 `Root.Q`、`VisualElement` 或 `RegisterCallback<ClickEvent>`
