using System;
using System.Collections.Generic;
using System.ComponentModel;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 局内 ViewModel。订阅 GameModel 的 PropertyChanged 事件，
    /// 将变化镜像到 ReactiveProperty 供 GameScreen 绑定。
    /// </summary>
    public class GameViewModel : ViewModelBase
    {
        private GameModel _model;

        /// <summary>
        /// 当前关卡标识。
        /// </summary>
        public int LevelId { get; set; }

        /// <summary>
        /// 当前战斗阶段。
        /// </summary>
        public ReactiveProperty<BattlePhase> Phase { get; private set; }

        /// <summary>
        /// 当前在场怪物列表。
        /// </summary>
        public ReactiveProperty<IReadOnlyList<MonsterRuntime>> Monsters { get; private set; }

        /// <summary>
        /// 当前手牌列表。
        /// </summary>
        public ReactiveProperty<IReadOnlyList<CardRuntime>> Hand { get; private set; }

        /// <summary>
        /// 当前能量。
        /// </summary>
        public ReactiveProperty<int> Energy { get; private set; }

        /// <summary>
        /// 最大能量。
        /// </summary>
        public ReactiveProperty<int> MaxEnergy { get; private set; }

        /// <summary>
        /// 玩家当前血量。
        /// </summary>
        public ReactiveProperty<int> PlayerHp { get; private set; }

        /// <summary>
        /// 玩家最大血量。
        /// </summary>
        public ReactiveProperty<int> PlayerMaxHp { get; private set; }

        /// <summary>
        /// 玩家当前护甲。
        /// </summary>
        public ReactiveProperty<int> PlayerArmor { get; private set; }

        /// <summary>
        /// 关卡是否已完成。
        /// </summary>
        public ReactiveProperty<bool> IsLevelComplete { get; private set; }

        /// <summary>
        /// 玩家是否已死亡。
        /// </summary>
        public ReactiveProperty<bool> IsPlayerDead { get; private set; }

        /// <summary>
        /// 创建局内 ViewModel。
        /// </summary>
        public GameViewModel()
        {
            Phase = Prop(BattlePhase.Idle);
            Monsters = Prop<IReadOnlyList<MonsterRuntime>>(Array.Empty<MonsterRuntime>());
            Hand = Prop<IReadOnlyList<CardRuntime>>(Array.Empty<CardRuntime>());
            Energy = Prop(0);
            MaxEnergy = Prop(0);
            PlayerHp = Prop(0);
            PlayerMaxHp = Prop(0);
            PlayerArmor = Prop(0);
            IsLevelComplete = Prop(false);
            IsPlayerDead = Prop(false);
        }

        // ── 命令意图事件 ──

        /// <summary>
        /// 使用卡牌（参数为手牌索引）。
        /// </summary>
        public event Action<int> CardUsed;

        /// <summary>
        /// 结束回合。
        /// </summary>
        public event Action EndTurnRequested;

        /// <summary>
        /// 选择奖励。
        /// </summary>
        public event Action RewardSelected;

        /// <summary>
        /// 绑定 GameModel，订阅其 PropertyChanged 并镜像到 ReactiveProperty。
        /// </summary>
        public void BindModel(GameModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _model.PropertyChanged += OnModelPropertyChanged;
            SyncAll();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (_model != null)
            {
                _model.PropertyChanged -= OnModelPropertyChanged;
                _model = null;
            }

            CardUsed = null;
            EndTurnRequested = null;
            RewardSelected = null;

            base.Dispose();
        }

        /// <summary>
        /// 触发使用卡牌意图。
        /// </summary>
        public void UseCard(int handIndex) => CardUsed?.Invoke(handIndex);

        /// <summary>
        /// 触发结束回合意图。
        /// </summary>
        public void EndTurn() => EndTurnRequested?.Invoke();

        /// <summary>
        /// 触发选择奖励意图。
        /// </summary>
        public void SelectReward() => RewardSelected?.Invoke();

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GameModel.Phase):
                    Phase.Value = _model.Phase;
                    break;
                case nameof(GameModel.CurrentEnergy):
                    Energy.Value = _model.CurrentEnergy;
                    break;
                case nameof(GameModel.MaxEnergy):
                    MaxEnergy.Value = _model.MaxEnergy;
                    break;
                case nameof(GameModel.PlayerHp):
                    PlayerHp.Value = _model.PlayerHp;
                    break;
                case nameof(GameModel.PlayerMaxHp):
                    PlayerMaxHp.Value = _model.PlayerMaxHp;
                    break;
                case nameof(GameModel.PlayerArmor):
                    PlayerArmor.Value = _model.PlayerArmor;
                    break;
                case nameof(GameModel.IsLevelComplete):
                    IsLevelComplete.Value = _model.IsLevelComplete;
                    break;
                case nameof(GameModel.IsPlayerDead):
                    IsPlayerDead.Value = _model.IsPlayerDead;
                    break;
                case "Monsters":
                    Monsters.Value = _model.Monsters;
                    break;
                case "Hand":
                    Hand.Value = _model.Hand;
                    break;
            }
        }

        private void SyncAll()
        {
            Phase.Value = _model.Phase;
            Energy.Value = _model.CurrentEnergy;
            MaxEnergy.Value = _model.MaxEnergy;
            PlayerHp.Value = _model.PlayerHp;
            PlayerMaxHp.Value = _model.PlayerMaxHp;
            PlayerArmor.Value = _model.PlayerArmor;
            IsLevelComplete.Value = _model.IsLevelComplete;
            IsPlayerDead.Value = _model.IsPlayerDead;
            Monsters.Value = _model.Monsters;
            Hand.Value = _model.Hand;
        }
    }
}
