## 1. 配置与生成模型

- [x] 1.1 在 Luban 配置源中为 `TbCard` 增加 `CardReleaseKind` 与 `TargetCount` 字段，并新增 `CardReleaseKind` 枚举（Melee / Projectile / Spell）
- [x] 1.2 在 Luban 配置源中为 `TbCardEffect` 增加 `TriggerTiming` 字段，并新增 `EffectTriggerTiming` 枚举（Immediate / EnemyTurnStart / EnemyTurnEnd）
- [x] 1.3 更新基础玩家卡牌配置，使近战、投射、法术、能量、护盾卡满足 `basic-card-config` delta spec 的字段与数值要求
- [x] 1.4 重新生成 `GameConfig.card` 代码与配置 bytes，并同步所有反射构造 `Card` / `CardEffect` 的 EditMode 测试辅助方法

## 2. 释放调度测试先行

- [x] 2.1 新增 `CardReleaseResolver` 或等价释放调度服务的 EditMode 测试：近战优先选择包含 `Damage` / `DamageDot` PendingCard 的存活敌人
- [x] 2.2 新增投射目标测试：`TargetCount` 足够时只选攻击意图敌人，不足时从其他存活敌人随机补足且不重复选择死亡敌人
- [x] 2.3 新增投射全体测试：`TargetCount <= 0` 时命中所有存活敌人，并保持攻击意图目标排在前面
- [x] 2.4 新增法术触发时机测试：`Immediate` 效果立即执行，`EnemyTurnStart` / `EnemyTurnEnd` 效果被登记并等待对应结算点
- [x] 2.5 新增攻击意图推导测试：只有 `Shield` / `EnergyGain` 的 PendingCards 不算攻击意图，`Damage` / `DamageDot` 算攻击意图

## 3. 释放调度实现

- [x] 3.1 实现释放调度服务，负责读取 `CardReleaseKind`、`TargetMode`、`TargetCount` 与 `TriggerTiming`
- [x] 3.2 实现攻击意图推导逻辑，复用 `TbCardEffect` 解析并忽略死亡怪物
- [x] 3.3 实现近战目标解析：优先攻击意图敌人，无攻击意图时回退到其他存活敌人
- [x] 3.4 实现投射目标解析：优先攻击意图敌人，不足时从其他存活敌人随机补足，随机源可在测试中确定化
- [x] 3.5 实现法术效果拆分：立即效果同步执行，敌人回合开始/结束效果登记为延迟结算项

## 4. 系统接入

- [x] 4.1 修改 `CardSystem.Play`，通过释放调度服务处理玩家出牌效果，并保持失败校验、扣能量、移手牌、进弃牌堆和 `CardPlayedEvent` 顺序符合 specs
- [x] 4.2 调整 `CardEffectExecutor` 边界，使其继续执行已解析目标和已到期效果，但不承载近战优先、投射补足或法术延迟规则
- [x] 4.3 修改 `BattleSystem.ExecuteMonsterTurn`，在怪物行动前结算 `EnemyTurnStart` 效果，在怪物行动后、`Check` 前结算 `EnemyTurnEnd` 效果
- [x] 4.4 保持现有 DoT 行为兼容：EnemyTurnStart 可覆盖当前 `TickBuffs` 语义，玩家被开始结算击杀时跳过怪物行动和 EnemyTurnEnd

## 5. 回归与验证

- [x] 5.1 更新 `CardEffectExecutorTests`，将目标优先级与触发时机相关断言迁移到释放调度测试，保留伤害/护盾/DoT/能量执行器行为测试
- [x] 5.2 更新 `BattleSystemBuffTickTests` 或新增战斗时序测试，覆盖 EnemyTurnStart 杀死玩家立即失败、EnemyTurnEnd 在怪物行动后进入 Check
- [x] 5.3 更新 `MonsterItemViewTests` 中卡牌/效果反射构造与意图渲染数据，使新增字段不会破坏 UI 意图展示测试
- [ ] 5.4 运行聚焦 EditMode 测试：`CardReleaseResolverTests`、`CardEffectExecutorTests`、`BattleSystemBuffTickTests`、`MonsterSystemFlowTests`
- [ ] 5.5 运行项目级脚本编译检查：`python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`，若 Unity Skills 不可用则回退 `dotnet build UnityProject.slnx --no-restore`
