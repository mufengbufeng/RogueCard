## ADDED Requirements

### Requirement: Region 必须支持动态加载 UXML 子模板
Region SHALL 持有一个 VisualElement 插槽引用。ShowAsync(uxmlLocation) SHALL 清空当前内容，通过 IResourceManager 加载 VisualTreeAsset，克隆后添加到插槽中。Show(element) SHALL 直接将 VisualElement 添加到插槽。

#### Scenario: 动态加载子模板
- **WHEN** 调用 region.ShowAsync("UI/BattlePanel")
- **THEN** Region 的插槽 SHALL 包含 BattlePanel 的克隆内容
- **AND** 克隆内容的 flexGrow SHALL 为 1

#### Scenario: 直接放置 VisualElement
- **WHEN** 调用 region.Show(visualElement)
- **THEN** Region 的插槽 SHALL 包含该 VisualElement

#### Scenario: 连续调用 ShowAsync 替换内容
- **WHEN** Region 当前显示 BattlePanel，调用 ShowAsync("UI/RewardPanel")
- **THEN** BattlePanel SHALL 从插槽中移除
- **AND** RewardPanel 的克隆 SHALL 替代显示

### Requirement: Region 必须支持清空内容
Region.Clear() SHALL 从插槽中移除当前内容 VisualElement。

#### Scenario: 清空已有内容
- **WHEN** Region 当前显示内容，调用 Clear()
- **THEN** 插槽 SHALL 为空
- **AND** 之前的 VisualElement SHALL 不在元素树中

#### Scenario: 空 Region 调用 Clear
- **WHEN** Region 插槽为空，调用 Clear()
- **THEN** SHALL 不抛异常

### Requirement: Region 必须暴露当前内容供 UQuery
Region.CurrentContent SHALL 返回当前显示的 VisualElement，用于 Screen 在 Region 内容上进行 UQuery 查找元素。

#### Scenario: 获取当前内容用于 UQuery
- **WHEN** Region 显示了 BattlePanel
- **THEN** CurrentContent SHALL 返回 BattlePanel 的根 VisualElement
- **AND** Screen SHALL 能通过 CurrentContent.Q<T>() 查找子元素

#### Scenario: 空时获取内容
- **WHEN** Region 未显示任何内容
- **THEN** CurrentContent SHALL 返回 null
