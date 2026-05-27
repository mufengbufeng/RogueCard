## MODIFIED Requirements

### Requirement: 自动绑定UI脚本按钮

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

### Requirement: region 外字段冲突处理

当 region 外已存在与 prefab key 同名的字段声明时，系统 SHALL 弹出 `EditorUtility.DisplayDialogComplex` 让用户选择覆盖、跳过或取消，而**不**默认静默跳过。该规则覆盖（override）`auto-bind-ui-script` 主规范中「增量更新与去重 / 部分字段已存在于 region 外」场景的默认跳过行为。

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
