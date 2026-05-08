### Requirement: 自动绑定UI脚本按钮
ReferenceCollectorEditor 的自动收集操作区 SHALL 包含一个"自动绑定UI脚本"按钮，位于"自动收集"和"清除自动收集"按钮同一行或新行。

#### Scenario: 按钮显示在编辑器中
- **WHEN** 选中一个挂载了 ReferenceCollector 组件的 GameObject
- **THEN** Inspector 面板的"自动收集（基于项目规则）"区域显示"自动绑定UI脚本"按钮

#### Scenario: 点击按钮触发代码生成
- **WHEN** 用户点击"自动绑定UI脚本"按钮
- **THEN** 系统读取当前 ReferenceCollector 中所有有效引用数据，对目标 UIView 脚本执行代码注入

### Requirement: 字段声明生成
系统 SHALL 根据 ReferenceCollector 中每条有效数据生成对应的字段声明，格式为 `private 类型 _xxxXxx;`，其中字段名由 key 加 `_` 前缀并首字母小写得到。

#### Scenario: 从 ReferenceCollector key 生成字段名
- **WHEN** ReferenceCollector 中存在 key 为 `StartGameBtn`、引用为 Button 组件的条目
- **THEN** 生成字段声明 `private Button _startGameBtn;`

#### Scenario: key 首字母已经为小写
- **WHEN** ReferenceCollector 中存在 key 为 `startGameBtn` 的条目
- **THEN** 生成字段名 `_startGameBtn`（加 `_` 前缀，首字母不变）

#### Scenario: 引用对象为 GameObject 且无匹配规则
- **WHEN** ReferenceCollector 中 key 为 `PlayerGo` 的条目引用了普通 GameObject 且无匹配的 ReferenceCollectorRuleService 规则
- **THEN** 生成字段声明 `private GameObject _playerGo;`

### Requirement: 类型推断
系统 SHALL 按以下优先级推断字段类型：实际组件类型 > ReferenceCollectorRuleService 规则 > GameObject。

#### Scenario: 引用对象为 Component 子类
- **WHEN** ReferenceCollector 中 key 为 `StartGameBtn` 的条目直接引用了 Button 组件
- **THEN** 字段类型推断为 `Button`

#### Scenario: 引用对象为 GameObject 且有匹配规则
- **WHEN** ReferenceCollector 中 key 为 `StartGameBtn` 的条目引用了 GameObject，且 ReferenceCollectorRuleService 有后缀为 `Btn` 匹配到 `Button` 的规则
- **THEN** 字段类型推断为 `Button`，生成 `private Button _startGameBtn;`

#### Scenario: 引用对象为 null 时跳过
- **WHEN** ReferenceCollector 中某条目的 gameObject 为 null
- **THEN** 跳过该条目，不生成字段

### Requirement: UHub.Initialize 注入
系统 SHALL 在目标脚本的 `OnInitialize()` 方法中插入 `UHub.Initialize()` 调用，如已存在则不重复添加。

#### Scenario: OnInitialize 中不存在 UHub.Initialize 调用
- **WHEN** 目标脚本的 `OnInitialize()` 方法中没有 `UHub.Initialize()` 调用
- **THEN** 在 `base.OnInitialize()` 调用之后插入 `UHub.Initialize();`

#### Scenario: OnInitialize 中已存在 UHub.Initialize 调用
- **WHEN** 目标脚本的 `OnInitialize()` 方法中已有 `UHub.Initialize()` 调用
- **THEN** 跳过，不重复添加

#### Scenario: OnInitialize 方法不存在
- **WHEN** 目标脚本没有 `OnInitialize()` 方法
- **THEN** 不自动创建方法，仅在 `#region 自动生成` 块中生成字段声明（用户需手动添加 OnInitialize 并调用 UHub.Initialize）

### Requirement: 自动生成 region 包围
所有自动生成的字段声明 SHALL 使用 `#region 自动生成` / `#endregion` 块包围。

#### Scenario: 脚本中不存在 region 块
- **WHEN** 目标脚本中没有 `#region 自动生成` 块
- **THEN** 在类体顶部（已有字段之前）插入新的 `#region 自动生成` 块，包含所有生成的字段声明

#### Scenario: 脚本中已存在 region 块
- **WHEN** 目标脚本中已有 `#region 自动生成` 块
- **THEN** 整体替换该 region 块的内容为最新生成的字段声明

### Requirement: 增量更新与去重
系统 SHALL 避免重复生成已有字段，仅生成脚本中不存在的字段声明。

#### Scenario: 部分字段已存在于 region 外
- **WHEN** 脚本中已有 `private Button _startGameBtn;`（不在 region 内），ReferenceCollector 中也有该 key
- **THEN** 不在 region 块中重复生成该字段

#### Scenario: 所有字段都已存在
- **WHEN** ReferenceCollector 中所有条目对应的字段都已在脚本中声明
- **THEN** region 块中内容为空或仅保留块标记，不重复生成

### Requirement: using 命名空间自动补充
系统 SHALL 自动检测字段类型所需的命名空间，如脚本中缺失则补充对应的 using 声明。

#### Scenario: 字段类型需要新的 using
- **WHEN** 生成字段 `private Button _startGameBtn;`，且脚本中没有 `using UnityEngine.UI;`
- **THEN** 在脚本顶部 using 区域添加 `using UnityEngine.UI;`

#### Scenario: 所需 using 已存在
- **WHEN** 字段类型所需的命名空间已在脚本 using 区域中
- **THEN** 不重复添加

### Requirement: 目标脚本定位
系统 SHALL 自动定位 GameObject 上继承自 UIView 的脚本文件作为代码注入目标。

#### Scenario: GameObject 上有 UIView 子类脚本
- **WHEN** 当前 GameObject 上挂载了继承自 UIView 的脚本
- **THEN** 自动定位到该脚本文件路径并执行代码注入

#### Scenario: GameObject 上没有 UIView 子类脚本
- **WHEN** 当前 GameObject 上没有挂载 UIView 子类脚本
- **THEN** 显示警告对话框，提示用户先创建 UIView 子类脚本
