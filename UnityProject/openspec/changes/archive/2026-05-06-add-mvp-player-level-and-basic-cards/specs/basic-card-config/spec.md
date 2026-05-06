## ADDED Requirements

### Requirement: 基础卡牌配置表必须定义 MVP 卡牌字段
系统 MUST 在配置数据目录中提供基础卡牌配置表结构，用于描述卡牌标识、名称、描述、能量消耗、效果类型、效果数值、基础卡牌标记和资源标识，并且卡牌标识 MUST 使用 `int`。

#### Scenario: 创建基础卡牌配置表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbCard` 的基础卡牌配置表结构
- **AND** 该结构 MUST 至少包含卡牌标识、名称、描述、能量消耗、效果类型、效果数值、基础卡牌标记和资源标识字段
- **AND** 卡牌标识字段 MUST 使用 `int`

### Requirement: 基础卡牌配置必须提供三张 MVP 基础卡牌
系统 MUST 在基础卡牌配置表中提供攻击、防御和能量回复三张基础卡牌记录，使后续战斗牌堆和出牌效果可以引用稳定卡牌数据。

#### Scenario: 填写三张基础卡牌记录
- **WHEN** 检查 `card.xlsx` 的数据行
- **THEN** 系统 MUST 存在一张攻击基础卡牌记录
- **AND** 系统 MUST 存在一张防御基础卡牌记录
- **AND** 系统 MUST 存在一张能量回复基础卡牌记录
- **AND** 每张基础卡牌记录 MUST 使用 `int` 卡牌标识
- **AND** 每张基础卡牌记录 MUST 填写名称、描述、能量消耗、效果类型、效果数值、基础卡牌标记和资源标识
- **AND** 每张基础卡牌记录的基础卡牌标记 MUST 为 true

### Requirement: MVP 卡牌效果类型必须限制为基础效果集合
系统 MUST 让 MVP 基础卡牌只使用后续基础出牌逻辑需要支持的效果类型，避免配置出运行时无法解释的效果。

#### Scenario: 限制基础卡牌效果类型
- **WHEN** 检查 `card.xlsx` 中三张基础卡牌记录的效果类型
- **THEN** 攻击卡牌的效果类型 MUST 为 `Attack`
- **AND** 防御卡牌的效果类型 MUST 为 `Defense`
- **AND** 能量回复卡牌的效果类型 MUST 为 `EnergyRecover`

### Requirement: 基础卡牌配置表必须注册到 Luban 表清单
系统 MUST 在 Luban 表清单中注册基础卡牌配置表，使配置生成流程能够识别该表。

#### Scenario: 注册基础卡牌配置表
- **WHEN** 检查 `__tables__.xlsx` 的表注册数据
- **THEN** 系统 MUST 存在 `card.TbCard` 注册记录
- **AND** 该注册记录 MUST 使用 `Card` 作为记录类名
- **AND** 该注册记录 MUST 从 `card.xlsx` 读取结构和数据
