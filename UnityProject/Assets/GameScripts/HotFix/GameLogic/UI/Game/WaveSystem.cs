using System;
using System.Collections.Generic;
using System.Linq;
using EF.Event;

namespace GameLogic
{
    /// <summary>
    /// 波次系统，管理关卡加载和波次推进。
    /// </summary>
    public class WaveSystem : IDisposable
    {
        private BattleSystem _battleSystem;
        private List<GameConfig.level.LevelWave> _waves;
        private int _levelId;
        private GameModel _model;
        private IEventPublisher _events;
        private Action<BattleEndedEvent> _battleEndedHandler;
        private bool _isDisposed;

        /// <summary>
        /// 初始化波次系统。
        /// </summary>
        public void Init(GameModel model, IEventPublisher events)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _battleEndedHandler = OnBattleEnded;
            _events.GetChannel<BattleEndedEvent>().Subscribe(_battleEndedHandler);
        }

        /// <summary>
        /// 注入依赖的 BattleSystem。
        /// </summary>
        public void Initialize(BattleSystem battleSystem)
        {
            _battleSystem = battleSystem;
        }

        /// <summary>
        /// 启动指定关卡，从配置表加载波次数据。
        /// </summary>
        public void StartLevel(int levelId)
        {
            _levelId = levelId;
            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null) return;

            var level = tables.TbLevel.GetOrDefault(levelId);
            if (level == null) return;

            _waves = new List<GameConfig.level.LevelWave>();
            foreach (var waveRef in level.WaveIds_Ref ?? Enumerable.Empty<GameConfig.level.LevelWave>())
            {
                if (waveRef != null)
                    _waves.Add(waveRef);
            }

            _waves.Sort((a, b) => a.Order.CompareTo(b.Order));
            _model.SetLevel(level, _waves);
            _model.SetWaveIndex(0);

            EnterCurrentWave();
        }

        /// <summary>
        /// 进入当前波次。
        /// </summary>
        private void EnterCurrentWave()
        {
            var wave = _model.CurrentWave;
            if (wave == null) return;

            if (wave.Type == GameConfig.level.WaveType.Battle)
            {
                EnterBattleWave(wave);
            }
            else
            {
                AdvanceToNextWave();
            }
        }

        /// <summary>
        /// 进入战斗波次，加载刷怪方案并委托给 BattleSystem。
        /// </summary>
        private void EnterBattleWave(GameConfig.level.LevelWave wave)
        {
            var tables = GameLogicEntry.Config.Tables;
            int? spawnId = wave.PayloadId;
            if (!spawnId.HasValue) return;

            var spawn = tables.TbBattleWaveSpawn.GetOrDefault(spawnId.Value);
            if (spawn == null) return;

            var batches = new List<GameConfig.battle.BattleWaveSpawnBatch>();
            foreach (var batchRef in spawn.BatchIds_Ref ?? Enumerable.Empty<GameConfig.battle.BattleWaveSpawnBatch>())
            {
                if (batchRef != null)
                    batches.Add(batchRef);
            }

            batches.Sort((a, b) => a.Order.CompareTo(b.Order));
            _battleSystem.EnterBattle(batches);
        }

        /// <summary>
        /// 推进到下一波次，所有波次完成则发布关卡完成事件。
        /// </summary>
        private void AdvanceToNextWave()
        {
            if (_waves != null && _model.WaveIndex + 1 < _waves.Count)
            {
                _model.SetWaveIndex(_model.WaveIndex + 1);
                EnterCurrentWave();
                return;
            }

            _model.SetLevelComplete(true);
            _events.GetChannel<LevelCompleteEvent>().Publish(new LevelCompleteEvent(_levelId));

            var globalHub = GameLogicEntry.Event;
            if (globalHub != null)
            {
                globalHub.StartLevelRequestedEvent.Publish(new StartLevelRequestedEvent(_levelId, ""));
            }
        }

        private void OnBattleEnded(BattleEndedEvent e)
        {
            if (e.IsVictory)
            {
                AdvanceToNextWave();
            }
        }

        /// <summary>
        /// 释放波次系统资源。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_events != null)
            {
                _events.GetChannel<BattleEndedEvent>().Unsubscribe(_battleEndedHandler);
            }

            _model = null;
            _events = null;
        }
    }
}
