## MODIFIED Requirements

### Requirement: 关卡波次表必须定义三类波次分发字段
系统 MUST 在配置数据目录中提供关卡波次表结构，用于描述波次所属关卡、顺序、类型、展示文案、继续文案和可选负载标识，并且波次标识、关卡引用和 Battle 负载引用 MUST 使用 `int` 标识约定。非战斗波次的展示文案和继续文案 MUST 可被运行时读取，用于事件确认 UI 展示。

#### Scenario: 创建关卡波次表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbLevelWave` 的关卡波次表结构
- **AND** 该结构 MUST 至少包含波次标识、所属关卡标识、顺序、波次类型、标题、描述、继续文案和可选负载标识字段
- **AND** 波次标识字段 MUST 使用 `int`
- **AND** 所属关卡标识字段 MUST 使用指向 `TbLevel` 的 `int` 引用
- **AND** 可选负载标识字段 MUST 能填写 `int` 刷怪方案标识

#### Scenario: 非战斗波次文案可供运行时展示
- **WHEN** 运行时读取 Chest 或 Shop 类型的 `LevelWave`
- **THEN** 系统 MUST 能读取该波次的标题、描述和继续文案
- **AND** 这些字段 MUST 可用于局内事件确认 UI
