## MODIFIED Requirements

### Requirement: ui_structure.json 反序列化模型

系统 SHALL 提供 `UiStructureSchema` C# 模型类，完整映射 `ui_structure.json` 的所有字段。

#### Scenario: 反序列化完整 JSON

- **WHEN** 输入为符合 Image-To-UI schema 的 `ui_structure.json` 字符串
- **THEN** 系统 SHALL 将其反序列化为 `UiStructure` 对象
- **AND** 对象 SHALL 包含 canvas（width/height/name）、root 层级树、元素类型（container/image/rect/text/button/overlay）、定位字段（position/size/align/vAlign/layout/offset）、视觉字段（asset/color/opacity/text/fontSize/nineSlice）
- **AND** 对象 SHALL 包含自动切图匹配字段（marker/spriteHint/regionId） when present

#### Scenario: 反序列化缺失字段的 JSON

- **WHEN** 输入 JSON 缺少可选字段（如 align、vAlign、layout、asset、marker、spriteHint、regionId）
- **THEN** 系统 SHALL 正常反序列化，缺失字段使用合理默认值（空字符串、null、0）
- **AND** 系统 SHALL NOT 抛出异常

### Requirement: 元素类型到组件映射

转换器 SHALL 将 Image-To-UI 的元素类型映射为 Unity UGUI 组件。

#### Scenario: image 类型

- **WHEN** 元素 type 为 "image"
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Image`
- **AND** 转换器 SHALL preserve asset、marker、spriteHint、regionId semantics in the generated descriptor visuals for Auto Slice matching
- **AND** Image.sprite SHALL remain unchanged unless a later approved Sprite assignment supplies a concrete Sprite asset

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
- **AND** 转换器 SHALL preserve asset、marker、spriteHint、regionId semantics in the generated descriptor visuals for Auto Slice matching

#### Scenario: overlay 类型

- **WHEN** 元素 type 为 "overlay" 且有 color 和 opacity
- **THEN** 转换器 SHALL 在组件列表中添加 `UnityEngine.UI.Image`
- **AND** 在描述符中记录 color 和 opacity 值

#### Scenario: 未知类型

- **WHEN** 元素 type 不是上述任何已知类型
- **THEN** 转换器 SHALL 跳过该元素并记录警告
- **AND** 转换器 SHALL NOT 中断整个转换流程
