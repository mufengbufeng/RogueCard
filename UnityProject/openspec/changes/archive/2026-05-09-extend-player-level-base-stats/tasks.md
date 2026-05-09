## 1. 配置表与数据

- [x] 1.1 在 `Configs/GameConfig/Datas/player_level.xlsx` 中给 `TbPlayerLevel` 新增 `BaseHp` (int) 字段
- [x] 1.2 填入 5 级 MVP 数据：(Id=1, BaseHp=100, BaseEnergy=3, HandLimit=10, ExpToLevelUp=0) / (2, 110, 3, 10, 50) / (3, 120, 3, 10, 100) / (4, 130, 3, 10, 200) / (5, 140, 3, 10, 350) — 经验值阶梯仅占位，后续奖励变更可调
- [x] 1.3 跑 Luban 生成代码，确认 `GameConfig.player.PlayerLevel.BaseHp` 字段存在

## 2. GameModel 调整

- [x] 2.1 在 `GameModel` 中删除 `DefaultPlayerHp` 常量
- [x] 2.2 修改 `GameModel.InitBattleAttributes` 签名为 `(int maxEnergy, int handLimit, int maxHp)`，内部把 `PlayerHp = maxHp` / `PlayerMaxHp = maxHp`
- [x] 2.3 在 `GameModel` 增加 `CurrentLevel : int` 字段，默认 1，提供 `SetCurrentLevel(int)`
- [x] 2.4 全代码库 grep `DefaultPlayerHp`，删除所有引用

## 3. BattleSystem 重写 InitPlayerAttributes

- [x] 3.1 修改 `BattleSystem.InitPlayerAttributes`：读 `tables.TbPlayerLevel.GetOrDefault(_model.CurrentLevel)`，若为 null 再 fallback 到 `GetOrDefault(1)`，仍为 null 则抛 `InvalidOperationException("缺少 1 级 PlayerLevel 数据")`
- [x] 3.2 把 `BaseHp` / `BaseEnergy` / `HandLimit` 三个字段全部传给 `_model.InitBattleAttributes`
- [x] 3.3 移除 `GameModel.DefaultPlayerHp` 相关 fallback 路径

## 4. 测试

- [x] 4.1 `BattleSystemTests` 添加用例："1 级玩家进入战斗后 PlayerHp == 100 / MaxEnergy == 3 / HandLimit == 10"
- [x] 4.2 `BattleSystemTests` 添加用例："5 级玩家进入战斗后 PlayerHp == 140"
- [x] 4.3 `BattleSystemTests` 添加用例："等级表无 1 级数据时进入战斗抛异常"
- [x] 4.4 调整既有测试中所有硬编码 `50` 或旧 `DefaultPlayerHp` 字面量的断言
- [x] 4.5 跑 unity-compile-check 通过；EditMode 全部测试绿

## 5. 文档与归档

- [x] 5.1 在 `add-card-rogue-core-loop/tasks.md` 中标记本变更已完成
- [x] 5.2 通过 `/opsx:verify` 校验本变更交付
- [x] 5.3 通过 `/opsx:archive` 归档本变更
