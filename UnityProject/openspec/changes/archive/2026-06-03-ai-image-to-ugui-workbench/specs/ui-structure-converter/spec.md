## ADDED Requirements

### Requirement: ui_structure.json 反序列化模型

系统 SHALL 提供 `UiStructureSchema` C# 模型类，完整映射 `ui_structure.json` 的所有字段。

#### Scenario: 反序列化完整 JSON

- **WHEN** 输入为符合 Image-To-UI schema 的 `ui_structure.json` 字符串
- **THEN** 系统 SHALL 将其反序列化为 `UiStructure` 对象
- **AND** 对象 SHALL 包含 canvas（width/height/name）、root 层级树、元素类型（container/image/rect/text/button/overlay）、定位字段（position/size/align/vAlign/layout/offset）、视觉字段（asset/color/opacity/text/fontSize/nineSlice）

#### Scenario: 反序列化缺失字段的 JSON

- **WHEN** 输入 JSON 缺少可选字段（如 align、vAlign、layout、asset）
- **THEN** 系统 SHALL 正常反序列化，缺失字段使用合理默认值（空字符串、null、0）
- **AND** 系统 SHALL NOT 抛出异常

### Requirement: align/vAlign 到锚点映射

转换器 SHALL 将 Image-To-UI 的 align/vAlign 定位模型映射为 Unity UGUI 锚点。

#### Scenario: 水平居中

- **WHEN** 元素有 `align: "center"` 且无父级 layout
- **THEN** 转换器 SHALL 设置 AnchorMin.x = 0.5, AnchorMax.x = 0.5
- **AND** AnchoredPosition.x 偏移为 0

#### Scenario: 左对齐

- **WHEN** 元素有 `align: "left"` 且无父级 layout
- **THEN** 转换器 SHALL 设置 AnchorMin.x = 0, AnchorMax.x = 0
- **AND** AnchoredPosition.x 为元素宽度的一半

#### Scenario: 右对齐

- **WHEN** 元素有 `align: "right"` 且无父级 layout
- **THEN** 转换器 SHALL 设置 AnchorMin.x = 1, AnchorMax.x = 1
- **AND** AnchoredPosition.x 为负元素宽度的一半

#### Scenario: 垂直居中

- **WHEN** 元素有 `vAlign: "middle"` 且无父级 layout
- **THEN** 转换器 SHALL 设置 AnchorMin.y = 0.5, AnchorMax.y = 0.5

#### Scenario: 顶部对齐

- **WHEN** 元素有 `vAlign: "top"` 且无父级 layout
- **THEN** 转换器 SHALL 设置 AnchorMin.y = 1, AnchorMax.y = 1

#### Scenario: 底部对齐

- **WHEN** 元素有 `vAlign: "bottom"` 且无父级 layout
- **THEN** 转换器 SHALL 设置 AnchorMin.y = 0, AnchorMax.y = 0

#### Scenario: 无定位信息的元素

- **WHEN** 元素没有 align、vAlign、layout 和 position
- **THEN** 转换器 SHALL 使用 stretch 模式（AnchorMin = (0,0), AnchorMax = (1,1), SizeDelta = 元素实际尺寸）

### Requirement: layout 到 LayoutGroup 映射

转换器 SHALL 将 Image-To-UI 的 layout 配置映射为 Unity LayoutGroup 组件。

#### Scenario: 水平行布局

- **WHEN** 父元素有 `layout: { type: "row" }`
- **THEN** 转换器 SHALL 在父节点描述符中标记添加 `HorizontalLayoutGroup`
- **AND** spacing SHALL 映射为 LayoutGroup.spacing（"even" 映射为 0 + controlChildSize + useChildScale）

#### Scenario: 垂直列布局

- **WHEN** 父元素有 `layout: { type: "column" }`
- **THEN** 转换器 SHALL 在父节点描述符中标记添加 `VerticalLayoutGroup`

#### Scenario: layout 内子节点不设手动位置

- **WHEN** 子节点的父级有 layout 配置
- **THEN** 转换器 SHALL 不为该子节点设置锚点和 AnchoredPosition
- **AND** 子节点的定位 SHALL 由 LayoutGroup 自动管理

#### Scenario: layout padding 映射

- **WHEN** layout 包含 `padding: { x: 20, y: 10 }`
- **THEN** 转换器 SHALL 设置 LayoutGroup.padding 为对应值

### Requirement: Y 轴坐标翻转

转换器 SHALL 正确处理 Image-To-UI（左上原点，Y 向下）与 Unity UGUI（左下原点，Y 向上）的坐标系差异。

#### Scenario: 显式 position 翻转

- **WHEN** 元素有 `position: { x: 100, y: 200 }`（无 align/vAlign/layout）
- **THEN** 转换器 SHALL 使用 top-left 锚点 (0,1)→(0,1)
- **AND** AnchoredPosition.y SHALL 为 `-(canvasHeight - y - height)` 的计算结果

### Requirement: 元素类型到组件映射

转换器 SHALL 将 Image-To-UI 的元素类型映射为 Unity UGUI 组件。

#### Scenario: image 类型

- **WHEN** 元素 type 为 "image"
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Image`
- **AND** 第一版不映射 asset 字段，Image 组件保持默认白色

#### Scenario: rect 类型

- **WHEN** 元素 type 为 "rect" 且有 color 字段
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Image`
- **AND** 在描述符中记录 color 值供后续应用时设置

#### Scenario: text 类型

- **WHEN** 元素 type 为 "text"
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Text`
- **AND** 在描述符中记录 text、fontSize、color 值

#### Scenario: container 类型

- **WHEN** 元素 type 为 "container"
- **THEN** 转换器 SHALL 不添加额外组件（仅有 RectTransform）

#### Scenario: button 类型

- **WHEN** 元素 type 为 "button"
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Image` 和 `UnityEngine.UI.Button`

#### Scenario: overlay 类型

- **WHEN** 元素 type 为 "overlay" 且有 color 和 opacity
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Image`
- **AND** 在描述符中记录 color 和 opacity 值

#### Scenario: 未知类型

- **WHEN** 元素 type 不是上述任何已知类型
- **THEN** 转换器 SHALL 跳过该元素并记录警告
- **AND** 转换器 SHALL NOT 中断整个转换流程

### Requirement: 层级树递归转换

转换器 SHALL 递归处理 ui_structure.json 的 children 层级树，为每个节点生成对应的 UguiNodeDescriptor。

#### Scenario: 嵌套层级正确映射

- **WHEN** JSON 中 root > container > image 的三级嵌套
- **THEN** 转换器 SHALL 生成三个 UguiNodeDescriptor
- **AND** ParentPath SHALL 正确反映层级关系（"root"、"root/container"）

#### Scenario: 名称冲突自动处理

- **WHEN** 同一层级下存在同名元素
- **THEN** 转换器 SHALL 自动为重复名称添加后缀（_1, _2）
- **AND** 转换器 SHALL 在转换报告中记录重命名信息
