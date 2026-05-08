## MODIFIED Requirements

### Requirement: 主界面必须展示默认关卡入口信息
主界面入口 MUST 在默认可交互状态下展示一个可开始的默认关卡信息。默认关卡标识 MUST 从 TbLevel 配置表读取 IsDefault=true 的记录，展示名称和说明 MUST 来源于配置表而非硬编码常量。

#### Scenario: 打开主界面后显示配置表中的默认关卡
- **WHEN** 系统完成热更新初始化并打开主界面
- **THEN** MainModel MUST 从 TbLevel 查找 IsDefault=true 的关卡记录
- **AND** 主界面 MUST 展示该关卡配置中的 Name 作为关卡名称
- **AND** 主界面 MUST 展示该关卡配置中的 Desc 作为关卡说明
- **AND** 开始按钮 MUST 处于可交互状态

#### Scenario: 配置表中无默认关卡时回退到安全状态
- **WHEN** TbLevel 中不存在 IsDefault=true 的记录
- **THEN** 系统 MUST 记录警告日志
- **AND** 主界面 MUST 展示占位提示信息
- **AND** 系统 MUST NOT 因缺少默认关卡配置而阻断主界面显示

### Requirement: 主界面开始按钮必须发起默认关卡进入请求
主界面入口 MUST 在用户点击开始按钮时发起默认关卡进入请求。请求 MUST 携带 int 类型的关卡标识，与 Luban 表主键类型一致。

#### Scenario: 点击开始按钮后携带 int 类型关卡标识进入局内
- **WHEN** 用户在主界面点击开始按钮
- **THEN** 系统 MUST 发起 StartLevelRequestedEvent
- **AND** 事件 MUST 携带 int 类型的 LevelId（从 TbLevel 配置获取）
- **AND** 主菜单流程 MUST 承接该请求并切换到局内流程
- **AND** 系统 MUST 打开 GameView 并传递关卡标识
