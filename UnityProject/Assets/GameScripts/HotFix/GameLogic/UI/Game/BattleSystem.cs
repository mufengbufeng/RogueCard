using System;
using System.Collections.Generic;
using System.Linq;
using EF.Event;
using GameConfig.card;

namespace GameLogic
{
    /// <summary>
    /// 战斗系统，管理战斗阶段循环和胜负判定。
    /// </summary>
    public class BattleSystem : IDisposable, IBattleEventSink
    {
        private CardSystem _cardSystem;
        private MonsterSystem _monsterSystem;
        private CardReleaseResolver _releaseResolver;
        private GameModel _model;
        private IEventPublisher _events;
        private List<GameConfig.battle.BattleWaveSpawnBatch> _currentBatches;
        private bool _isDisposed;

        /// <summary>
        /// 初始化战斗系统。
        /// </summary>
        public void Init(GameModel model, IEventPublisher events)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>
        /// 注入依赖的 CardSystem 和 MonsterSystem。
        /// </summary>
        public void Initialize(CardSystem cardSystem, MonsterSystem monsterSystem)
        {
            _cardSystem = cardSystem;
            _monsterSystem = monsterSystem;
            _releaseResolver = cardSystem?.ReleaseResolver;
        }

        /// <summary>
        /// 注入依赖的 CardSystem 和 MonsterSystem（保留 MonsterCardSystem 形参以让上层显式传入并维持 Init 链清晰）。
        /// 实际 MonsterCardSystem 注入由 MonsterSystem.Initialize 处理；此重载只保证调用顺序与可读性。
        /// </summary>
        public void Initialize(CardSystem cardSystem, MonsterSystem monsterSystem, MonsterCardSystem monsterCardSystem)
        {
            _cardSystem = cardSystem;
            _monsterSystem = monsterSystem;
            _releaseResolver = cardSystem?.ReleaseResolver;
            // monsterCardSystem 已经在 GameProcedure 中通过 monsterSystem.Initialize 注入
            _ = monsterCardSystem;
        }

        /// <summary>
        /// 进入战斗，初始化玩家属性、生成怪物、开始准备阶段。
        /// </summary>
        public void EnterBattle(List<GameConfig.battle.BattleWaveSpawnBatch> batches)
        {
            _currentBatches = batches;
            _model.SetCurrentBatches(batches);
            _model.SetBatchIndex(0);

            InitPlayerAttributes();

            if (_currentBatches != null && _currentBatches.Count > 0)
            {
                _monsterSystem.SpawnBatch(_currentBatches[0]);
            }

            SetPhase(BattlePhase.Prepare);
        }

        /// <summary>
        /// 玩家结束回合。
        /// </summary>
        public void EndTurn()
        {
            if (_model.Phase != BattlePhase.PlayerTurn) return;
            SetPhase(BattlePhase.MonsterTurn);
        }

        /// <summary>
        /// 切换战斗阶段并驱动对应逻辑。
        /// </summary>
        private void SetPhase(BattlePhase phase)
        {
            _model.SetPhase(phase);

            switch (phase)
            {
                case BattlePhase.Prepare:
                    ExecutePreparePhase();
                    break;
                case BattlePhase.MonsterTurn:
                    ExecuteMonsterTurn();
                    break;
                case BattlePhase.Check:
                    ExecuteCheckPhase();
                    break;
            }
        }

        /// <summary>
        /// 执行准备阶段：恢复玩家能量、抽玩家牌、调用 MonsterSystem.BeginMonsterPrepare 让怪物准备 PendingCards。
        /// </summary>
        private void ExecutePreparePhase()
        {
            _model.RestoreEnergy();
            _cardSystem.Draw(_model.HandLimit);
            _monsterSystem.BeginMonsterPrepare();
            SetPhase(BattlePhase.PlayerTurn);
        }

        /// <summary>
        /// 执行怪物回合。先结算敌人回合开始效果，玩家死亡则立即失败；
        /// 怪物行动后再结算敌人回合结束效果，最后进入 Check。
        /// </summary>
        private void ExecuteMonsterTurn()
        {
            ResolveEnemyTurnStartEffects();

            if (_model.PlayerHp <= 0)
            {
                _model.SetPlayerDead(true);
                _events.GetChannel<BattleEndedEvent>().Publish(new BattleEndedEvent(false));
                return;
            }

            _monsterSystem.ExecuteTurn();
            ResolveEnemyTurnEndEffects();
            _events.GetChannel<TurnEndedEvent>().Publish(new TurnEndedEvent());
            SetPhase(BattlePhase.Check);
        }

        /// <summary>
        /// 兼容旧测试入口：默认按 EnemyTurnStart 结算 Buffs。
        /// </summary>
        private void TickBuffs()
        {
            TickBuffs(EffectTriggerTiming.EnemyTurnStart);
        }

        /// <summary>
        /// 统一 tick 指定触发时机的玩家和怪物 Buffs。
        /// </summary>
        private void TickBuffs(EffectTriggerTiming timing)
        {
            int playerBuffCountBefore = _model.PlayerBuffs.Count;
            TickActorBuffs(new PlayerActor(_model), timing);

            for (int i = 0; i < _model.Monsters.Count; i++)
            {
                var monster = _model.Monsters[i];
                if (monster == null) continue;
                bool wasAlive = !monster.IsDead;
                TickActorBuffs(monster, timing);
                if (wasAlive && monster.IsDead)
                {
                    _events.GetChannel<MonsterDeathEvent>().Publish(new MonsterDeathEvent(i));
                }
            }

            // 玩家 Buff 列表始终通知（DoT 扣血即使数量未变也需要刷新 RemainingTurns 文本）
            _model.NotifyPlayerBuffsChanged();

            // 怪物列表通知（包括 HP、护甲、buff RemainingTurns 变化）
            _model.NotifyMonstersChanged();
        }

        /// <summary>
        /// 对单个 Actor 的 Buffs 执行指定时机 Tick：DoT 扣血 + RemainingTurns 倒数 + 归零移除。
        /// </summary>
        private static void TickActorBuffs(IBattleActor actor, EffectTriggerTiming timing)
        {
            if (actor == null) return;
            var buffs = actor.Buffs;
            if (buffs == null || buffs.Count == 0) return;

            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                var buff = buffs[i];
                if (buff == null)
                {
                    buffs.RemoveAt(i);
                    continue;
                }

                if (!ShouldTickBuff(buff, timing))
                {
                    continue;
                }

                if (buff.Kind == GameConfig.card.EffectKind.DamageDot && !actor.IsDead)
                {
                    actor.TakeDamage(buff.Value);
                }

                buff.RemainingTurns--;
                if (buff.RemainingTurns <= 0)
                {
                    buffs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 判断 Buff 是否应该在当前触发时机结算；Immediate 兼容旧 DoT Tick 语义，归入 EnemyTurnStart。
        /// </summary>
        private static bool ShouldTickBuff(BuffRuntime buff, EffectTriggerTiming timing)
        {
            if (buff.TriggerTiming == timing) return true;
            return timing == EffectTriggerTiming.EnemyTurnStart
                && buff.TriggerTiming == EffectTriggerTiming.Immediate;
        }

        /// <summary>
        /// 结算敌人回合开始效果。
        /// </summary>
        private void ResolveEnemyTurnStartEffects()
        {
            TickBuffs(EffectTriggerTiming.EnemyTurnStart);
            var resolver = _releaseResolver ?? _cardSystem?.ReleaseResolver;
            resolver?.ResolveDelayedEffects(EffectTriggerTiming.EnemyTurnStart, this);
            _model.NotifyMonstersChanged();
        }

        /// <summary>
        /// 结算敌人回合结束效果。
        /// </summary>
        private void ResolveEnemyTurnEndEffects()
        {
            TickBuffs(EffectTriggerTiming.EnemyTurnEnd);
            var resolver = _releaseResolver ?? _cardSystem?.ReleaseResolver;
            resolver?.ResolveDelayedEffects(EffectTriggerTiming.EnemyTurnEnd, this);
            _model.NotifyMonstersChanged();
        }

        /// <summary>
        /// 执行检查阶段：判断胜负条件。
        /// </summary>
        private void ExecuteCheckPhase()
        {
            if (_model.PlayerHp <= 0)
            {
                _model.SetPlayerDead(true);
                _events.GetChannel<BattleEndedEvent>().Publish(new BattleEndedEvent(false));
                return;
            }

            bool allMonstersDead = _model.Monsters.All(m => m.Hp <= 0);
            if (!allMonstersDead)
            {
                SetPhase(BattlePhase.Prepare);
                return;
            }

            if (_currentBatches != null && _model.BatchIndex + 1 < _currentBatches.Count)
            {
                _model.SetBatchIndex(_model.BatchIndex + 1);
                _monsterSystem.SpawnBatch(_currentBatches[_model.BatchIndex]);
                SetPhase(BattlePhase.Prepare);
                return;
            }

            _events.GetChannel<BattleEndedEvent>().Publish(new BattleEndedEvent(true));
        }

        /// <summary>
        /// 从 TbPlayerLevel 表按 GameModel.PlayerLevelId 初始化玩家战斗属性。
        /// 若当前等级数据缺失，回退到 1 级；若 1 级也缺失则抛 InvalidOperationException。
        /// </summary>
        private void InitPlayerAttributes()
        {
            var tables = GameLogicEntry.Config.Tables;
            var playerLevel = ResolvePlayerLevel(tables.TbPlayerLevel, _model.PlayerLevelId);
            ApplyPlayerLevelAttributes(_model, playerLevel);
            _cardSystem.InitDeck();
        }

        /// <summary>
        /// 按等级查表，缺当前等级时回退到 1 级，1 级也缺则抛异常。可测试入口。
        /// </summary>
        public static GameConfig.player.PlayerLevel ResolvePlayerLevel(
            GameConfig.player.TbPlayerLevel tbPlayerLevel,
            int level)
        {
            if (tbPlayerLevel == null)
            {
                throw new InvalidOperationException("TbPlayerLevel 表为空：无法初始化玩家战斗属性");
            }

            var data = tbPlayerLevel.GetOrDefault(level) ?? tbPlayerLevel.GetOrDefault(1);
            if (data == null)
            {
                throw new InvalidOperationException("缺少 1 级 PlayerLevel 数据：无法初始化玩家战斗属性");
            }
            return data;
        }

        /// <summary>
        /// 把 PlayerLevel 数据三个字段写入 GameModel。可测试入口。
        /// </summary>
        public static void ApplyPlayerLevelAttributes(GameModel model, GameConfig.player.PlayerLevel data)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (data == null) throw new ArgumentNullException(nameof(data));
            model.InitBattleAttributes(
                maxEnergy: data.BaseEnergy,
                handLimit: data.HandLimit,
                maxHp: data.BaseHp);
        }

        /// <inheritdoc />
        public void OnActorDied(IBattleActor actor)
        {
            if (actor is MonsterRuntime monster)
            {
                int index = -1;
                var list = _model.Monsters;
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], monster))
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    _events.GetChannel<MonsterDeathEvent>().Publish(new MonsterDeathEvent(index));
                }
            }
        }

        /// <summary>
        /// 释放战斗系统资源。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _releaseResolver = null;
            _model = null;
            _events = null;
        }
    }
}
