## ADDED Requirements

### Requirement: Rect-centric schema DTO

系统 SHALL 提供以 `rect` 为核心的 `ui_structure.json` 反序列化模型，并使用 Unity 原生术语描述布局。

#### Scenario: 反序列化 fixed rect 节点

- **WHEN** 输入节点包含 `rect: { mode: "fixed", anchorPreset, anchoredPosition, sizeDelta, pivot }`
- **THEN** 系统 SHALL 将这些字段反序列化为结构化 DTO
- **AND** 缺失的可选字段 SHALL 使用合理默认值而不抛出异常

#### Scenario: 反序列化 stretch rect 节点

- **WHEN** 输入节点包含 `rect: { mode: "stretch", anchorPreset, inset, pivot }`
- **THEN** 系统 SHALL 反序列化 stretch 所需字段
- **AND** 非拉伸轴上的可选 `sizeDelta` SHALL 被保留

#### Scenario: 兼容旧像素框字段

- **WHEN** 输入节点仍包含旧字段 `position`、`align`、`vAlign` 或 `offset`
- **THEN** 系统 SHALL 正常加载该节点
- **AND** 后续转换 SHALL 产生兼容或升级警告而不是直接失败

### Requirement: Fixed and stretch rect conversion

转换器 SHALL 根据 `rect.mode` 生成 Unity RectTransform 导向的描述符数据。

#### Scenario: fixed rect 使用 Unity anchoredPosition 语义

- **WHEN** 节点 `rect.mode` 为 `fixed` 且提供 `anchoredPosition`
- **THEN** 转换器 SHALL 按 `anchorPreset` 计算 `anchorMin` 和 `anchorMax`
- **AND** 描述符 SHALL 保留 Unity `anchoredPosition` 语义而不再执行设计图坐标系翻转
- **AND** `sizeDelta` 与 `pivot` SHALL 与输入一致

#### Scenario: stretch rect 使用 inset 语义

- **WHEN** 节点 `rect.mode` 为 `stretch` 且提供 `inset`
- **THEN** 转换器 SHALL 按 `anchorPreset` 生成拉伸锚点
- **AND** 描述符 SHALL 依据 `inset` 计算布局结果
- **AND** 可选单轴 `sizeDelta` SHALL 只作用于未拉伸轴

### Requirement: Layout container and child sizing semantics

转换器 SHALL 将容器布局和子节点尺寸提示映射为 Unity 布局组件语义。

#### Scenario: horizontal、vertical 与 grid 容器转换

- **WHEN** 容器声明 `layout.type` 为 `horizontal`、`vertical` 或 `grid`
- **THEN** 转换器 SHALL 产生对应的 `HorizontalLayoutGroup`、`VerticalLayoutGroup` 或 `GridLayoutGroup` 组件意图
- **AND** `spacing`、`padding`、`childAlignment`、`constraint` 等字段 SHALL 被保留

#### Scenario: layout 子节点使用 LayoutElement 尺寸提示

- **WHEN** 节点父级声明了 `layout`
- **THEN** 子节点 SHALL 仅保留 `size` 与少量对齐/拉伸提示
- **AND** 转换器 SHALL 优先输出 `LayoutElement` 所需的 `min`、`preferred` 与 `flexible` 尺寸提示
- **AND** 子节点 SHALL NOT 再依赖自由 `anchoredPosition` 或 `stretch inset` 进行最终摆放

### Requirement: Advanced Unity UI semantics

转换器 SHALL 支持安全区、滚动容器和 fitter 相关语义块。

#### Scenario: safe area 语义被保留

- **WHEN** 节点声明 `safeArea` block
- **THEN** 转换器 SHALL 记录该 safe area 应用意图供工作台预览和 prefab apply 使用

#### Scenario: scroll 容器语义被保留

- **WHEN** 节点声明 `scroll` block
- **THEN** 转换器 SHALL 产生 ScrollView、viewport 与 content 所需的结构或组件意图
- **AND** `horizontal`、`vertical` 与 movement 设置 SHALL 被保留

#### Scenario: fitter 语义被保留

- **WHEN** 节点声明 `ContentSizeFitter` 或 `AspectRatioFitter` 相关字段
- **THEN** 转换器 SHALL 输出对应 fitter 组件意图
- **AND** 非法字段组合 SHALL 产生警告

### Requirement: Validation and deterministic warnings

转换器 SHALL 对不合法组合、未知节点和名称冲突提供稳定的验证结果。

#### Scenario: 不支持的字段组合产生警告

- **WHEN** 输入同时声明冲突的 `rect`、`layout`、`layoutElement`、`safeArea`、`scroll` 或 fitter 字段
- **THEN** 转换器 SHALL 阻止不安全的描述符输出或执行可预测的降级处理
- **AND** SHALL 记录可供工作台展示的验证警告

#### Scenario: 重复名称保持确定性

- **WHEN** 同级节点名称重复
- **THEN** 转换器 SHALL 使用稳定可预测的后缀重命名
- **AND** SHALL 在转换报告中记录原名与新名

#### Scenario: 未知节点类型不终止整体转换

- **WHEN** 节点 `type` 超出当前支持范围
- **THEN** 转换器 SHALL 跳过该节点并记录警告
- **AND** 其他可转换节点 SHALL 继续生成描述符

### Requirement: Stable conversion output

对于相同输入，转换器 SHALL 生成稳定的输出顺序与结果。

#### Scenario: 同一 JSON 多次转换结果一致

- **WHEN** 同一份有效 schema JSON 被重复转换
- **THEN** 描述符顺序、层级路径、自动重命名结果与验证警告 SHALL 保持一致
