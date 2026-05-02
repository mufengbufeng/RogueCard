# 解决方案：子弹命中扣血与死亡状态机联动

## 问题
在飞机战斗链路中，子弹命中、生命值变更、死亡动画与实体回收之间缺少统一约束，导致以下风险：
1. 命中后子弹回收时机不一致，存在重复碰撞窗口。
2. 玩家/敌人死亡后可能仍短暂执行输入、移动或攻击逻辑。
3. 角色死亡时无法只清理“自己发射”的在场子弹。
4. 对象池复用下，延迟隐藏回调可能误作用到复用后的实体实例。

## 根因
1. 子弹系统没有统一的“命中即收口”行为规范，回收与扣血逻辑分散。
2. 玩家与敌人死亡态缺少完整的行为阻断与状态机切换约束。
3. 子弹数据未记录发射者实体标识，无法按发射者精确筛选清理。
4. 延迟隐藏逻辑最初缺少生命周期校验，复用时存在时序竞态。

## 修复方案
1. 子弹命中收口：
   - `BulletEntity` 按 `OwnerType` 只对敌对目标生效。
   - 目标实现 `IHealth` 且未死亡时执行 `TakeDamage`。
   - 命中后立即 `HideEntity(Id)` 回收子弹自身。
2. 子弹复用安全：
   - `OnHide` 显式停用 `Handle`。
   - `OnHide` / `OnRecycle` 重置方向、速度、归属、伤害、发射者 ID 等运行时字段。
3. 发射者维度清弹：
   - `BulletData` 新增 `SourceEntityId`。
   - `IBulletModule` 新增 `ClearBulletsBySource(int sourceEntityId)`。
   - `BulletModule` 遍历在场 `BulletEntity` 并按发射者 ID 执行隐藏回收。
4. 玩家死亡联动：
   - `TakeDamage` 进入死亡后禁用碰撞器、阻断输入与自动攻击、播放 `Boom`。
   - 调用 `ClearBulletsBySource(Id)` 清理玩家自身子弹。
   - 延迟隐藏增加生命周期 token + 实体 ID 校验，避免复用误隐藏。
5. 敌人死亡联动：
   - 状态机新增并使用 `Dead` 状态，死亡后不再执行移动/停留/攻击/边界逻辑。
   - 死亡时禁用碰撞器、播放 `EnemyDead`、清理自身子弹并延迟隐藏。
   - 延迟隐藏同样增加生命周期 token + 实体 ID 校验。

## 验证结果
1. `opence validate update-bullet-hit-state-machine --strict` 通过。
2. `tasks.md` 全部任务已标记为 `[x]`。
3. 用户已在 Unity PlayMode 验证关键场景通过：
   - 玩家/敌人命中扣血后子弹立即回收。
   - 玩家/敌人死亡后行为阻断生效。
   - 死亡清弹仅影响自身发射子弹。
   - 延迟隐藏不再误作用于复用实体。

## 技能检查（Q1-Q4）
1. Q1: 解决什么问题？
   - 解决“战斗实体命中-扣血-死亡-回收”链路的一致性与复用安全问题。
   - 已通过 `opence skill list` 确认现有技能主要覆盖 opence 流程，不直接覆盖该具体战斗链路修复。
2. Q2: 谁在什么时候使用？
   - 目标用户：维护 Gameplay 战斗逻辑的开发者。
   - 典型触发语句：
     - “子弹命中后为什么会重复伤害？”
     - “角色死亡后还会开火，怎么收口？”
     - “如何只清理某个实体发射的子弹？”
     - “对象池复用下延迟回收怎么避免串号？”
3. Q3: 描述应如何编写？
   - 若沉淀为技能，描述应包含动作词与关键词，例如：
   - `Validates 子弹命中与死亡状态机收口，并 Reviews 对象池延迟回收竞态；用于排查重复受击、死亡后行为未阻断、死亡清弹失效问题。`
4. Q4: 是否值得创建？
   - 预计未来 6 个月复用频率低于 3 次，且当前问题已通过本解决方案文档完整沉淀。
   - 结论：暂不新增技能，后续若出现重复需求再升级为专用 skill。

## 关键文件
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Bullet/BulletData.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Bullet/IBulletModule.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Bullet/BulletModule.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Bullet/BulletEntity.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarEntity.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Enemy/EnemyEntity.cs`
- `opence/changes/update-bullet-hit-state-machine/tasks.md`
- `opence/changes/update-bullet-hit-state-machine/validation.md`

## 日期
- 2026-02-21
