## MODIFIED Requirements

### Requirement: 层级树递归转换

转换器 SHALL 递归处理 ui_structure.json 的 children 层级树，为每个节点生成对应的 UguiNodeDescriptor，并 SHALL 支持通过转换选项决定 JSON root 是虚拟根还是实际节点。

#### Scenario: 默认 VirtualRoot 映射
- **WHEN** JSON 中 root > container > image 的三级嵌套使用默认 Draft Workbench 转换选项
- **THEN** 转换器 SHALL NOT 为 JSON root 生成 UguiNodeDescriptor
- **AND** root 的直接子节点 ParentPath SHALL 为空字符串
- **AND** 更深层子节点 ParentPath SHALL 从 root 子节点开始计算

#### Scenario: VirtualRoot 保留 root 坐标父框
- **WHEN** JSON root 有 position 和 size
- **THEN** 转换器 SHALL 使用 root source bounds 作为直接子节点的坐标父框
- **AND** 子节点显式 position SHALL 继续按源图全局坐标换算为相对目标父节点的 UGUI 坐标

#### Scenario: IncludeRootNode 兼容旧层级
- **WHEN** 转换调用显式请求 IncludeRootNode 模式
- **THEN** 转换器 SHALL 为 root > container > image 生成三个 UguiNodeDescriptor
- **AND** ParentPath SHALL 正确反映完整层级关系（"root"、"root/container"）

#### Scenario: 名称冲突自动处理
- **WHEN** 同一层级下存在同名元素
- **THEN** 转换器 SHALL 自动为重复名称添加后缀（_1, _2）
- **AND** 转换器 SHALL 在转换报告中记录重命名信息

## ADDED Requirements

### Requirement: Asset and crop semantics preservation

转换器 SHALL preserve asset intent and crop metadata from ui_structure.json for downstream slicing and transparent icon refinement.

#### Scenario: Preserve explicit asset semantics
- **WHEN** an element includes asset kind, alpha mode, crop padding, square-canvas, asset, marker, spriteHint, or regionId metadata
- **THEN** the converted descriptor visuals SHALL preserve that metadata
- **AND** downstream JSON-driven slicing SHALL be able to read it without reparsing raw JSON

#### Scenario: Missing optional metadata does not fail conversion
- **WHEN** an element omits asset intent or crop metadata
- **THEN** conversion SHALL continue successfully
- **AND** downstream slicing MAY infer asset intent from node name, marker, spriteHint, size, and component type
