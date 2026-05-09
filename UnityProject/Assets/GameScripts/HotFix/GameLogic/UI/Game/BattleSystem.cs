using System;
using System.Collections.Generic;
using System.Linq;
using EF.Event;

namespace GameLogic
{
    /// <summary>
    /// 战斗系统，管理战斗阶段循环和胜负判定。
    /// </summary>
    public class BattleSystem : IDisposable
    {
        private CardSystem _cardSystem;
        private MonsterSystem _monsterSystem;
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
        }

        /// <summary>
        /// 注入依赖的 CardSystem 和 MonsterSystem（保留 MonsterCardSystem 形参以让上层显式传入并维持 Init 链清晰）。
        /// 实际 MonsterCardSystem 注入由 MonsterSystem.Initialize 处理；此重载只保证调用顺序与可读性。
        /// </summary>
        public void Initialize(CardSystem cardSystem, MonsterSystem monsterSystem, MonsterCardSystem monsterCardSystem)
        {
            _cardSystem = cardSystem;
            _monsterSystem = monsterSystem;
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
        /// 执行怪物回合。先 tick 玩家与所有怪物的 Buffs（处理 DoT 扣血与倒计时），
        /// 若玩家被 DoT 击杀则立即结算并跳过怪物行动。
        /// </summary>
        private void ExecuteMonsterTurn()
        {
            TickBuffs();

            if (_model.PlayerHp <= 0)
            {
                _model.SetPlayerDead(true);
                _events.GetChannel<BattleEndedEvent>().Publish(new BattleEndedEvent(false));
                return;
            }

            _monsterSystem.ExecuteTurn();
            _events.GetChannel<TurnEndedEvent>().Publish(new TurnEndedEvent());
            SetPhase(BattlePhase.Check);
        }

        /// <summary>
        /// 统一 tick 玩家和所有怪物的 Buffs。
        /// 对 DamageDot 类型的 Buff 先扣血，再把所有 Buff 的剩余回合数减 1，归零的从列表移除。
        /// 怪物因 DoT 死亡时发布 MonsterDeathEvent。
        /// 完成后通知 PlayerBuffs / Monsters 变化，让 UI 刷新 buff 状态条。
        /// </summary>
        private void TickBuffs()
        {
            int playerBuffCountBefore = _model.PlayerBuffs.Count;
            TickActorBuffs(new PlayerActor(_model), -1);

            for (int i = 0; i < _model.Monsters.Count; i++)
            {
                var monster = _model.Monsters[i];
                if (monster == null) continue;
                bool wasAlive = !monster.IsDead;
                TickActorBuffs(monster, i);
                if (wasAlive && monster.IsDead)
                {
                    _events.GetChannel<MonsterDeathEvent>().Publish(new MonsterDeathEvent(i));
                }
            }

            // 玩家 Buff 列表始终通知（DoT 扣血即使数量未变也需要刷新 RemainingTurns 文本）
            _model.NotifyPlayerBuffsChanged();

            // 怪物列表通知（包括 buff RemainingTurns 变化）
            _model.SetMonsters(new List<MonsterRuntime>(_model.Monsters));
        }

        /// <summary>
        /// 对单个 Actor 的 Buffs 执行 Tick：DoT 扣血 + RemainingTurns 倒数 + 归零移除。
        /// </summary>
        private static void TickActorBuffs(IBattleActor actor, int monsterIndex)
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

        /// <summary>
        /// 释放战斗系统资源。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _model = null;
            _events = null;
        }
    }
}
