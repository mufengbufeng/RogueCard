# auto-bind-ui-script Specification

## Purpose

定义旧版 ReferenceCollector 驱动的 UI 脚本自动绑定代码生成能力。
## Requirements
### Requirement: 添加变量到UI代码按钮

ReferenceCollectorEditor 的自动收集操作区 SHALL 包含一个按钮，按钮标签为 **"添加变量到UI代码"**，位于"自动收集"和"清除自动收集"按钮同一行或新行。

#### Scenario: 按钮显示在编辑器中

- **WHEN** 选中一个挂载了 ReferenceCollector 组件的 GameObject
- **THEN** Inspector 面板的"自动收集（基于项目规则）"区域显示"添加变量到UI代码"按钮

#### Scenario: 点击按钮触发代码生成

- **WHEN** 用户点击"添加变量到UI代码"按钮
- **THEN** 系统读取当前 ReferenceCollector 中所有有效引用数据，对目标 UIView 脚本执行代码注入

### Requirement: 字段声明生成

系统 SHALL 根据 ReferenceCollector 中每条有效数据生成对应的字段声明，格式为 `[UHubBind("Key")] private 类型 _xxxXxx;`，其中字段名由 key 加 `_` 前缀并首字母小写得到，特性参数 `Key` 与 `ReferenceCollectorData.key` 完全一致。

#### Scenario: 从 ReferenceCollector key 生成带 UHubBind 特性的字段

- **WHEN** ReferenceCollector 中存在 key 为 `StartGameBtn`、引用为 Button 组件的条目
- **THEN** 生成字段声明 `[UHubBind("StartGameBtn")] private Button _startGameBtn;`

#### Scenario: key 首字母已经为小写

- **WHEN** ReferenceCollector 中存在 key 为 `startGameBtn` 的条目
- **THEN** 生成字段名 `_startGameBtn`（加 `_` 前缀，首字母不变），特性 `[UHubBind("startGameBtn")]`

#### Scenario: 引用对象为 GameObject 且无匹配规则

- **WHEN** ReferenceCollector 中 key 为 `PlayerGo` 的条目引用了普通 GameObject 且无匹配的 ReferenceCollectorRuleService 规则
- **THEN** 生成字段声明 `[UHubBind("PlayerGo")] private GameObject _playerGo;`

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

所有自动生成的字段声明 SHALL 使用 `#region 自动生成` / `#endregion` 块包围。每次执行生成时，系统 SHALL **整块替换** region 内容（先删除旧 region 体再写入新内容），保证不出现重复定义。

#### Scenario: 脚本中不存在 region 块

- **WHEN** 目标脚本中没有 `#region 自动生成` 块
- **THEN** 在类体顶部（已有字段之前）插入新的 `#region 自动生成` 块，包含所有生成的字段声明

#### Scenario: 脚本中已存在 region 块

- **WHEN** 目标脚本中已有 `#region 自动生成` 块
- **THEN** 整体替换该 region 块的内容为最新生成的字段声明
- **AND** 不保留旧 region 内任何字段声明

#### Scenario: 重复点击按钮

- **WHEN** 用户对同一 prefab 连续点击"添加变量到UI代码" N 次
- **THEN** region 块内容 SHALL 保持与最近一次生成完全一致，不出现重复字段

### Requirement: 增量更新与去重
系统 SHALL 避免重复生成已有字段，仅生成脚本中不存在的字段声明。

#### Scenario: 部分字段已存在于 region 外
- **WHEN** 脚本中已有 `private Button _startGameBtn;`（不在 region 内），ReferenceCollector 中也有该 key
- **THEN** 行为遵循「region 外字段冲突处理」Requirement 定义的三选一对话框流程，而非静默跳过

#### Scenario: 所有字段都已存在
- **WHEN** ReferenceCollector 中所有条目对应的字段都已在脚本中声明
- **THEN** region 块中内容为空或仅保留块标记，不重复生成

### Requirement: region 外字段冲突处理

当 region 外已存在与 prefab key 同名的字段声明时，系统 SHALL 弹出 `EditorUtility.DisplayDialogComplex` 让用户选择覆盖、跳过或取消，而**不**默认静默跳过。该规则覆盖（override）「增量更新与去重 / 部分字段已存在于 region 外」场景的默认跳过行为。

#### Scenario: 冲突存在时弹出三选一确认框

- **WHEN** ReferenceCollector 中某 key 推导出的字段名（如 `_startGameBtn`）已存在于 region 外
- **THEN** 系统 SHALL 弹出对话框列出全部冲突字段名、对应 key、以及目标脚本路径
- **AND** 对话框 SHALL 提供「覆盖」「跳过」「取消」三个按钮

#### Scenario: 用户选择覆盖

- **WHEN** 用户在冲突对话框点击「覆盖」
- **THEN** 系统 SHALL 删除所有冲突字段的整行声明（含同行属性如 `[SerializeField]`）
- **AND** SHALL 在 `#region 自动生成` 块内重新生成全部 prefab 字段（包含被覆盖的字段）
- **AND** 日志 SHALL 输出「已覆盖 N 个 region 外字段：xxx」

#### Scenario: 用户选择跳过

- **WHEN** 用户在冲突对话框点击「跳过」
- **THEN** 系统 SHALL 保持冲突字段在 region 外不变
- **AND** SHALL 在 region 块中仅生成未冲突的字段（与改动前的默认行为一致）
- **AND** 日志 SHALL 输出「保留 N 个 region 外已有同名字段：xxx」

#### Scenario: 用户选择取消

- **WHEN** 用户在冲突对话框点击「取消」
- **THEN** 系统 SHALL 立即返回，不写入目标脚本
- **AND** 日志 SHALL 输出「用户取消，未修改 {assetPath}」

#### Scenario: 无冲突时不弹窗

- **WHEN** ReferenceCollector 中所有字段名均未与 region 外字段冲突
- **THEN** 系统 SHALL 不弹出冲突对话框，按既有流程直接生成 region 内容

### Requirement: using 命名空间自动补充
系统 SHALL 自动检测字段类型所需的命名空间，如脚本中缺失则补充对应的 using 声明。

#### Scenario: 字段类型需要新的 using
- **WHEN** 生成字段 `private Button _startGameBtn;`，且脚本中没有 `using UnityEngine.UI;`
- **THEN** 在脚本顶部 using 区域添加 `using UnityEngine.UI;`

#### Scenario: 所需 using 已存在
- **WHEN** 字段类型所需的命名空间已在脚本 using 区域中
- **THEN** 不重复添加

### Requirement: 目标脚本定位

系统 SHALL 按 GameObject 名称推导类名，并在 `Assets/GameScripts/HotFix/GameLogic/UI/` 目录下递归查找 `<ClassName>.cs` 资产，**不**要求脚本挂载到 GameObject 上。

#### Scenario: 按 Prefab 名称匹配同名脚本

- **WHEN** ReferenceCollector 挂载在名为 `GameView` 的 Prefab 根节点上
- **AND** `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameView.cs` 存在
- **THEN** 系统 SHALL 定位到 `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameView.cs` 作为生成目标

#### Scenario: GameObject 名称含非法字符时清洗

- **WHEN** GameObject 名称为 `Game View 1`
- **THEN** 系统 SHALL 清洗为 `GameView1` 后再搜索

#### Scenario: 未找到对应脚本

- **WHEN** UI 目录下不存在与类名匹配的 `.cs` 文件
- **THEN** 系统 SHALL 弹出 `EditorUtility.DisplayDialog` 提示用户文件名需与 Prefab 名称一致，并中止生成

#### Scenario: 找到多个同名脚本

- **WHEN** UI 目录下存在两个或以上同名 `.cs` 文件
- **THEN** 系统 SHALL 弹出对话框列出冲突路径，并中止生成

#### Scenario: 脚本不要求挂载在 GameObject

- **WHEN** Prefab 根节点上没有任何 MonoBehaviour 继承自 `EF.UI.UIView`
- **AND** 同名 `.cs` 存在于 UI 目录
- **THEN** 系统 SHALL 仍然定位到该脚本并执行生成（不依赖 MonoBehaviour 关联）

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
- **AND** 用户点击"添加变量到UI代码"重新生成 `GameView.cs` 自动生成区
- **THEN** 自动生成区 SHALL 保留所有必需 key 对应的 `[UHubBind]` 字段
- **AND** Unity C# 编译 SHALL NOT 出现这些字段名的 CS0103 错误

### Requirement: Draft workbench ReferenceCollector binding plan

The UI script auto-binding tooling SHALL support generating a ReferenceCollector binding plan for GameView nodes proposed or created by the draft workbench.

#### Scenario: Binding plan uses project rules

- **WHEN** the draft workbench asks for bindings for generated or selected GameView UGUI nodes
- **THEN** the auto-binding tooling SHALL resolve ReferenceCollector keys and component types from the project ReferenceCollector rule configuration
- **AND** it SHALL NOT use a separate suffix-to-component rule table

#### Scenario: Duplicate key is reported

- **WHEN** a proposed binding key already exists in the target ReferenceCollector
- **THEN** the binding plan SHALL mark the entry as skipped or conflicting
- **AND** the binding plan SHALL include the existing key in its report

#### Scenario: Missing component is reported

- **WHEN** a proposed node name matches a configured rule but the required component is missing
- **THEN** the binding plan SHALL skip that node
- **AND** the binding plan SHALL report the missing component type

### Requirement: Draft workbench script field update

The UI script auto-binding tooling SHALL update generated UI script fields for draft workbench bindings through the existing text rewriting path.

#### Scenario: New binding requires script field

- **WHEN** an applied draft workbench binding introduces a ReferenceCollector key that needs a corresponding UI script field
- **THEN** the auto-binding tooling SHALL use the existing UI script binder text rewriter to add the field
- **AND** the generated field type SHALL match the component type resolved from the ReferenceCollector rule configuration

#### Scenario: Existing script field is preserved

- **WHEN** the required UI script field already exists with a compatible type
- **THEN** the auto-binding tooling SHALL preserve the existing field
- **AND** it SHALL NOT emit a duplicate field for the same ReferenceCollector key

#### Scenario: Incompatible script field is reported

- **WHEN** the required UI script field already exists with an incompatible type
- **THEN** the auto-binding tooling SHALL report a conflict
- **AND** it SHALL NOT overwrite the existing field without explicit user confirmation

### Requirement: Draft workbench binding report

The UI script auto-binding tooling SHALL return a structured binding report to the draft workbench after preview or apply.

#### Scenario: Preview report is generated

- **WHEN** the draft workbench previews binding changes
- **THEN** the auto-binding tooling SHALL return the proposed ReferenceCollector entries, script field changes, skipped entries, and conflicts without modifying files

#### Scenario: Apply report is generated

- **WHEN** the draft workbench applies binding changes
- **THEN** the auto-binding tooling SHALL return the applied ReferenceCollector entries, script field changes, skipped entries, warnings, and conflicts

