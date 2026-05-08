using System;
using System.Collections.Generic;
using EF.Model;

namespace GameLogic
{
    /// <summary>
    /// 局内数据模型只读接口，供 View 访问。
    /// </summary>
    public interface IGameModelData
    {
        /// <summary>
        /// 当前战斗阶段。
        /// </summary>
        BattlePhase Phase { get; }

        /// <summary>
        /// 当前能量。
        /// </summary>
        int CurrentEnergy { get; }

        /// <summary>
        /// 最大能量。
        /// </summary>
        int MaxEnergy { get; }

        /// <summary>
        /// 手牌上限。
        /// </summary>
        int HandLimit { get; }

        /// <summary>
        /// 玩家当前血量。
        /// </summary>
        int PlayerHp { get; }

        /// <summary>
        /// 玩家最大血量。
        /// </summary>
        int PlayerMaxHp { get; }

        /// <summary>
        /// 玩家当前护甲。
        /// </summary>
        int PlayerArmor { get; }

        /// <summary>
        /// 当前在场怪物列表。
        /// </summary>
        IReadOnlyList<MonsterRuntime> Monsters { get; }

        /// <summary>
        /// 当前手牌列表。
        /// </summary>
        IReadOnlyList<CardRuntime> Hand { get; }

        /// <summary>
        /// 关卡是否已完成。
        /// </summary>
        bool IsLevelComplete { get; }

        /// <summary>
        /// 玩家是否已死亡。
        /// </summary>
        bool IsPlayerDead { get; }
    }

    /// <summary>
    /// 局内数据模型，管理关卡、波次、战斗回合、能量和手牌等运行时状态。
    /// </summary>
    public class GameModel : ModelBase<IGameModelData>
    {
        /// <summary>
        /// 默认怪物血量（配置表中暂无血量字段）。
        /// </summary>
        public const int DefaultMonsterHp = 30;

        /// <summary>
        /// 默认玩家血量（配置表中暂无血量字段）。
        /// </summary>
        public const int DefaultPlayerHp = 50;

        private readonly ModelValue<BattlePhase> _phase;
        private readonly ModelValue<int> _currentEnergy;
        private readonly ModelValue<int> _maxEnergy;
        private readonly ModelValue<int> _handLimit;
        private readonly ModelValue<int> _playerHp;
        private readonly ModelValue<int> _playerMaxHp;
        private readonly ModelValue<int> _playerArmor;
        private readonly ModelValue<bool> _isLevelComplete;
        private readonly ModelValue<bool> _isPlayerDead;

        private GameConfig.level.Level _currentLevel;
        private List<GameConfig.level.LevelWave> _waves;
        private int _waveIndex;
        private int _batchIndex;
        private List<GameConfig.battle.BattleWaveSpawnBatch> _currentBatches;
        private List<MonsterRuntime> _monsters;
        private List<CardRuntime> _hand;
        private List<CardRuntime> _discardPile;

        /// <summary>
        /// 当前战斗阶段。
        /// </summary>
        public BattlePhase Phase => GetValue(_phase);

        /// <summary>
        /// 当前能量。
        /// </summary>
        public int CurrentEnergy => GetValue(_currentEnergy);

        /// <summary>
        /// 最大能量。
        /// </summary>
        public int MaxEnergy => GetValue(_maxEnergy);

        /// <summary>
        /// 手牌上限。
        /// </summary>
        public int HandLimit => GetValue(_handLimit);

        /// <summary>
        /// 玩家当前血量。
        /// </summary>
        public int PlayerHp => GetValue(_playerHp);

        /// <summary>
        /// 玩家最大血量。
        /// </summary>
        public int PlayerMaxHp => GetValue(_playerMaxHp);

        /// <summary>
        /// 玩家当前护甲。
        /// </summary>
        public int PlayerArmor => GetValue(_playerArmor);

        /// <summary>
        /// 关卡是否已完成。
        /// </summary>
        public bool IsLevelComplete => GetValue(_isLevelComplete);

        /// <summary>
        /// 玩家是否已死亡。
        /// </summary>
        public bool IsPlayerDead => GetValue(_isPlayerDead);

        /// <summary>
        /// 当前关卡配置。
        /// </summary>
        public GameConfig.level.Level CurrentLevel => _currentLevel;

        /// <summary>
        /// 当前波次列表。
        /// </summary>
        public IReadOnlyList<GameConfig.level.LevelWave> Waves => _waves;

        /// <summary>
        /// 当前波次索引。
        /// </summary>
        public int WaveIndex => _waveIndex;

        /// <summary>
        /// 当前批次索引。
        /// </summary>
        public int BatchIndex => _batchIndex;

        /// <summary>
        /// 当前刷怪批次列表。
        /// </summary>
        public IReadOnlyList<GameConfig.battle.BattleWaveSpawnBatch> CurrentBatches => _currentBatches;

        /// <summary>
        /// 当前在场怪物列表。
        /// </summary>
        public IReadOnlyList<MonsterRuntime> Monsters => _monsters;

        /// <summary>
        /// 当前手牌列表。
        /// </summary>
        public IReadOnlyList<CardRuntime> Hand => _hand;

        /// <summary>
        /// 弃牌堆。
        /// </summary>
        public IReadOnlyList<CardRuntime> DiscardPile => _discardPile;

        /// <summary>
        /// 当前波次配置。
        /// </summary>
        public GameConfig.level.LevelWave CurrentWave =>
            _waves != null && _waveIndex >= 0 && _waveIndex < _waves.Count
                ? _waves[_waveIndex]
                : null;

        /// <summary>
        /// 创建局内数据模型。
        /// </summary>
        public GameModel()
        {
            _phase = CreateValue(BattlePhase.Idle);
            _currentEnergy = CreateValue(0);
            _maxEnergy = CreateValue(0);
            _handLimit = CreateValue(5);
            _playerHp = CreateValue(DefaultPlayerHp);
            _playerMaxHp = CreateValue(DefaultPlayerHp);
            _playerArmor = CreateValue(0);
            _isLevelComplete = CreateValue(false);
            _isPlayerDead = CreateValue(false);

            _monsters = new List<MonsterRuntime>();
            _hand = new List<CardRuntime>();
            _discardPile = new List<CardRuntime>();
        }

        /// <summary>
        /// 创建只读数据接口实例。
        /// </summary>
        protected override IGameModelData CreateData()
        {
            return new GameModelData(this);
        }

        /// <summary>
        /// 设置关卡上下文。
        /// </summary>
        public void SetLevel(
            GameConfig.level.Level level,
            List<GameConfig.level.LevelWave> waves)
        {
            _currentLevel = level;
            _waves = waves;
            _waveIndex = 0;
            _batchIndex = 0;
        }

        /// <summary>
        /// 设置波次索引。
        /// </summary>
        public void SetWaveIndex(int index)
        {
            _waveIndex = index;
        }

        /// <summary>
        /// 设置批次索引。
        /// </summary>
        public void SetBatchIndex(int index)
        {
            _batchIndex = index;
        }

        /// <summary>
        /// 设置当前刷怪批次列表。
        /// </summary>
        public void SetCurrentBatches(List<GameConfig.battle.BattleWaveSpawnBatch> batches)
        {
            _currentBatches = batches;
        }

        /// <summary>
        /// 设置战斗阶段。
        /// </summary>
        public void SetPhase(BattlePhase phase)
        {
            SetValue(_phase, phase, nameof(Phase));
        }

        /// <summary>
        /// 初始化战斗属性。
        /// </summary>
        public void InitBattleAttributes(int maxEnergy, int handLimit, int playerHp)
        {
            SetValue(_maxEnergy, maxEnergy, nameof(MaxEnergy));
            SetValue(_handLimit, handLimit, nameof(HandLimit));
            SetValue(_playerMaxHp, playerHp, nameof(PlayerMaxHp));
            SetValue(_playerHp, playerHp, nameof(PlayerHp));
            SetValue(_playerArmor, 0, nameof(PlayerArmor));
        }

        /// <summary>
        /// 恢复能量到最大值。
        /// </summary>
        public void RestoreEnergy()
        {
            SetValue(_currentEnergy, MaxEnergy, nameof(CurrentEnergy));
        }

        /// <summary>
        /// 修改能量值。
        /// </summary>
        public void ModifyEnergy(int delta)
        {
            SetValue(_currentEnergy, Math.Max(0, CurrentEnergy + delta), nameof(CurrentEnergy));
        }

        /// <summary>
        /// 修改玩家血量。
        /// </summary>
        public void ModifyPlayerHp(int delta)
        {
            int newHp = Math.Max(0, Math.Min(PlayerMaxHp, PlayerHp + delta));
            SetValue(_playerHp, newHp, nameof(PlayerHp));
            if (newHp <= 0)
            {
                SetValue(_isPlayerDead, true, nameof(IsPlayerDead));
            }
        }

        /// <summary>
        /// 修改玩家护甲。
        /// </summary>
        public void ModifyPlayerArmor(int delta)
        {
            SetValue(_playerArmor, Math.Max(0, PlayerArmor + delta), nameof(PlayerArmor));
        }

        /// <summary>
        /// 设置怪物运行时列表。
        /// </summary>
        public void SetMonsters(List<MonsterRuntime> monsters)
        {
            _monsters = monsters ?? new List<MonsterRuntime>();
            RaisePropertyChanged(nameof(Monsters));
        }

        /// <summary>
        /// 设置手牌列表。
        /// </summary>
        public void SetHand(List<CardRuntime> hand)
        {
            _hand = hand ?? new List<CardRuntime>();
            RaisePropertyChanged(nameof(Hand));
        }

        /// <summary>
        /// 设置弃牌堆。
        /// </summary>
        public void SetDiscardPile(List<CardRuntime> discardPile)
        {
            _discardPile = discardPile ?? new List<CardRuntime>();
        }

        /// <summary>
        /// 将卡牌加入弃牌堆。
        /// </summary>
        public void AddToDiscardPile(CardRuntime card)
        {
            _discardPile.Add(card);
        }

        /// <summary>
        /// 将当前手牌全部移入弃牌堆。
        /// </summary>
        public void DiscardHand()
        {
            _discardPile.AddRange(_hand);
            _hand.Clear();
        }

        /// <summary>
        /// 清空弃牌堆。
        /// </summary>
        public void ClearDiscardPile()
        {
            _discardPile.Clear();
        }

        /// <summary>
        /// 标记关卡完成。
        /// </summary>
        public void SetLevelComplete(bool complete)
        {
            SetValue(_isLevelComplete, complete, nameof(IsLevelComplete));
        }

        /// <summary>
        /// 标记玩家死亡。
        /// </summary>
        public void SetPlayerDead(bool dead)
        {
            SetValue(_isPlayerDead, dead, nameof(IsPlayerDead));
        }

        /// <summary>
        /// 模型释放，清理运行时状态。
        /// </summary>
        protected override void OnModelReleased()
        {
            _monsters.Clear();
            _hand.Clear();
            _discardPile.Clear();
            SetValue(_phase, BattlePhase.Idle, nameof(Phase));
            SetValue(_isLevelComplete, false, nameof(IsLevelComplete));
            SetValue(_isPlayerDead, false, nameof(IsPlayerDead));
            base.OnModelReleased();
        }

        /// <summary>
        /// 局内数据模型只读接口实现。
        /// </summary>
        private class GameModelData : IGameModelData
        {
            private readonly GameModel _model;

            public GameModelData(GameModel model)
            {
                _model = model ?? throw new ArgumentNullException(nameof(model));
            }

            public BattlePhase Phase => _model.Phase;
            public int CurrentEnergy => _model.CurrentEnergy;
            public int MaxEnergy => _model.MaxEnergy;
            public int HandLimit => _model.HandLimit;
            public int PlayerHp => _model.PlayerHp;
            public int PlayerMaxHp => _model.PlayerMaxHp;
            public int PlayerArmor => _model.PlayerArmor;
            public IReadOnlyList<MonsterRuntime> Monsters => _model.Monsters;
            public IReadOnlyList<CardRuntime> Hand => _model.Hand;
            public bool IsLevelComplete => _model.IsLevelComplete;
            public bool IsPlayerDead => _model.IsPlayerDead;
        }
    }
}
