## ADDED Requirements

### Requirement: 关卡主表必须定义第一阶段关卡骨架字段
系统 MUST 在配置数据目录中提供关卡主表结构，用于描述关卡稳定标识、展示名称、简短说明、默认入口标记和波次列表。

#### Scenario: 创建关卡主表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbLevel` 的关卡主表结构
- **AND** 该结构 MUST 至少包含关卡标识、名称、描述、默认入口标记和波次标识列表字段

### Requirement: 关卡波次表必须定义三类波次分发字段
系统 MUST 在配置数据目录中提供关卡波次表结构，用于描述波次所属关卡、顺序、类型、展示文案、继续文案和可选负载标识。

#### Scenario: 创建关卡波次表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbLevelWave` 的关卡波次表结构
- **AND** 该结构 MUST 至少包含波次标识、所属关卡标识、顺序、波次类型、标题、描述、继续文案和可选负载标识字段

### Requirement: 波次类型枚举必须覆盖第一阶段节点类型
系统 MUST 提供波次类型枚举结构，用于限制关卡波次只能配置为第一阶段支持的节点类型。

#### Scenario: 创建波次类型枚举结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于表示波次类型的枚举结构
- **AND** 该枚举 MUST 包含战斗、宝箱和商店三类枚举值

### Requirement: 配置表结构创建不得要求填写正式玩法数据
系统 MUST 允许本变更只创建表头和字段定义，不要求立即填写完整关卡、战斗、奖励或商店数据。

#### Scenario: 表格仅包含字段结构
- **WHEN** 新增配置表完成
- **THEN** 关卡主表、关卡波次表和波次类型枚举 MUST 可以只包含字段定义或示例表头
- **AND** 系统 MUST 不要求同时新增怪物、卡牌、奖励、玩家初始状态或商店商品配置表
