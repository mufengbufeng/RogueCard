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
        /// 执行准备阶段：刷新怪物意图、恢复能量、抽牌。
        /// </summary>
        private void ExecutePreparePhase()
        {
            _monsterSystem.RefreshIntents();
            _model.RestoreEnergy();
            _cardSystem.Draw(_model.HandLimit);
            SetPhase(BattlePhase.PlayerTurn);
        }

        /// <summary>
        /// 执行怪物回合。
        /// </summary>
        private void ExecuteMonsterTurn()
        {
            _monsterSystem.ExecuteTurn();
            _events.GetChannel<TurnEndedEvent>().Publish(new TurnEndedEvent());
            SetPhase(BattlePhase.Check);
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
        /// 从配置表初始化玩家战斗属性和初始牌库。
        /// </summary>
        private void InitPlayerAttributes()
        {
            var tables = GameLogicEntry.Config.Tables;
            var playerLevel = tables.TbPlayerLevel.DataList.Count > 0
                ? tables.TbPlayerLevel.DataList[0]
                : null;

            int maxEnergy = playerLevel?.BaseEnergy ?? 3;
            int handLimit = playerLevel?.HandLimit ?? 5;

            _model.InitBattleAttributes(maxEnergy, handLimit, GameModel.DefaultPlayerHp);
            _cardSystem.InitDeck();
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
