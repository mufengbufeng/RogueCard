## 1. 配置表结构与数据

- [x] 1.1 在 `Configs/GameConfig/Datas/card.xlsx` 中重写表结构：保留 `Id` (int) / `Name` / `Description` / `Cost` (int) / `IsBasic` (bool) / `ResourceId`，移除 `EffectType` / `Value`，新增 `OwnerKind` (枚举) / `TargetMode` (枚举)
- [x] 1.2 在 `Configs/GameConfig/Datas/` 新建 `card_effect.xlsx`，结构包含 `Id` (int) / `CardId` (int#ref=card.TbCard) / `Kind` (枚举 EffectKind) / `Value` (int) / `Duration` (int)
- [x] 1.3 在 `__tables__.xlsx` 注册 `card_effect.TbCardEffect`，记录类名 `CardEffect`，从 `card_effect.xlsx` 读取
- [x] 1.4 在 Luban 枚举定义文件中新增 `OwnerKind { Player, Monster, Both }`、`TargetMode { SingleAuto, SingleManual, All, SplitAcrossAll, Self }`、`EffectKind { Damage, Shield, DamageDot, EnergyGain }`
- [x] 1.5 在 `card.xlsx` 写入 5 张 MVP 基础卡：近战 / 投射 / 法术 / 能量 / 护盾，全部 `IsBasic=true`、`OwnerKind=Player`，`TargetMode` 按设计文档表
- [x] 1.6 在 `card_effect.xlsx` 写入对应效果行：近战(Damage 6) / 投射(Damage 6) / 法术(Damage 8 + DamageDot 2 Duration 3) / 能量(EnergyGain 2) / 护盾(Shield 5)
- [x] 1.7 跑 Luban 生成代码到 `Assets/GameScripts/HotFix/GameProto/`，确认 `GameConfig.card.Card`、`GameConfig.card.CardEffect`、`OwnerKind`、`TargetMode`、`EffectKind` 类型都生成

## 2. 运行时抽象与执行器

- [x] 2.1 新建 `Assets/GameScripts/HotFix/GameLogic/UI/Game/IBattleActor.cs`，接口包含 `Hp / MaxHp / Armor / CurrentEnergy / MaxEnergy / Buffs / IsDead / TakeDamage(int) / AddArmor(int) / GainEnergy(int) / AddBuff(BuffRuntime)`
- [x] 2.2 新建 `BuffRuntime.cs`，字段 `Kind (EffectKind) / Value (int) / RemainingTurns (int) / SourceActor (IBattleActor)`
- [x] 2.3 新建 `PlayerActor.cs`，构造接收 `GameModel`，所有写入方法走 `GameModel.Modify*` 系列以保留 `PropertyChanged`
- [x] 2.4 改造 `MonsterRuntime.cs` 使其实现 `IBattleActor`，新增字段 `CurrentEnergy / MaxEnergy / Buffs`（Buffs 在本变更只是个空 List）
- [x] 2.5 新建 `CardEffectExecutor.cs`，静态方法 `Execute(Card, IBattleActor caster, IList<IBattleActor> targets, IBattleEventSink events)`
- [x] 2.6 在 `CardEffectExecutor` 内根据 `card.TargetMode` 解析最终目标列表，再遍历 `card.Effects`（Luban 引用 / 运行时 join）按 `Kind` 分发
- [x] 2.7 实现 4 种 Kind 的处理：Damage（先扣盾再扣血）/ Shield / DamageDot（写入 target.Buffs）/ EnergyGain（修改 caster）
- [x] 2.8 SplitAcrossAll 模式下，对每个 Damage Effect 把 Value 替换为 `Math.Max(1, value / targets.Count)` 后再分发

## 3. CardSystem 接入

- [x] 3.1 移除 `CardSystem.ApplyEffect` 中的字符串 switch
- [x] 3.2 `CardSystem.Play` 在校验通过后构造 `caster=PlayerActor(_model)`，按 `card.TargetMode` 计算 `targets`，调用 `CardEffectExecutor.Execute`
- [x] 3.3 移除 `CardRuntime` 中已不用的字段，确保它依然只持有 `Card Config`（运行时状态由 Buff 列表承担）
- [x] 3.4 `CardSystem.InitDeck` 仍只筛 `IsBasic == true && OwnerKind in {Player, Both}` 的卡进入抽牌堆

## 4. BattleSystem DoT Tick

- [x] 4.1 在 `BattleSystem.SetPhase(BattlePhase.MonsterTurn)` 入口处，在 `_monsterSystem.ExecuteTurn()` 调用前，统一 tick 玩家和所有怪物的 Buffs
- [x] 4.2 Tick 流程：对每个 Actor 的 Buffs，处理 `Kind == DamageDot` 的扣血、所有 Kind 的 `RemainingTurns--`、归零的从列表移除
- [x] 4.3 玩家因 DoT 死亡时同样发布 `BattleEndedEvent(IsVictory=false)`

## 5. 测试

- [x] 5.1 新建 EditMode 测试 `CardEffectExecutorTests.cs`：覆盖 5 种 TargetMode × 4 种 Kind 的代表性场景
- [x] 5.2 测试用例：Damage 先扣盾再扣血；Shield 累加；DamageDot 写入 buff 后下一回合 tick 扣血；EnergyGain 加 caster 能量；SplitAcrossAll 在 N=2 时每个 3 伤
- [x] 5.3 调整既有 `CardSystemTests`：原有 `Attack` / `Defend` 用例改为新模型下的等价 case
- [x] 5.4 运行 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` 通过（Unity Skills 服务暂不可用，回退到 `dotnet build UnityProject.slnx --no-restore` 验证 0 警告 0 错误）
- [x] 5.5 运行 EditMode 测试套件全绿（238/238 passed）

## 6. 文档与归档

- [x] 6.1 在 `add-card-rogue-core-loop/tasks.md` 中标记本变更已完成
- [x] 6.2 通过 `/opsx:verify` 校验本变更交付（`openspec validate --strict` 通过）
- [x] 6.3 通过 `/opsx:archive` 归档本变更（已归档为 `2026-05-09-rework-card-effect-model`，主 specs 已更新：basic-card-config / card-effect-runtime / game-systems）
