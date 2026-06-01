using System;
using EF.Debugger;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 局内界面控制器。
    /// </summary>
    public class GameController : UIController
    {
        private GameView _gameView;
        private GameViewModel _viewModel;

        /// <summary>
        /// 初始化局内界面控制器。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            _gameView = GetView<GameView>();
            Log.Info("[GameController] 局内界面控制器初始化完成");
        }

        /// <summary>
        /// 进入局内界面并绑定命令。
        /// </summary>
        protected override void OnEnter(object userData)
        {
            base.OnEnter(userData);
            _viewModel = userData as GameViewModel;
            if (_viewModel == null)
            {
                Log.Warning("[GameController] 未收到 GameViewModel，局内界面只显示空状态。");
                return;
            }

            BindReactiveProperties();
            BindViewEvents();
            _gameView?.Render(_viewModel);
            Log.Info("[GameController] 局内界面已进入");
        }

        /// <summary>
        /// 刷新局内界面。
        /// </summary>
        protected override void OnRefresh(object userData)
        {
            base.OnRefresh(userData);
            _gameView?.Render(_viewModel);
        }

        private void BindReactiveProperties()
        {
            _viewModel.Phase.Changed += OnAnyStateChanged;
            _viewModel.Monsters.Changed += OnAnyStateChanged;
            _viewModel.Hand.Changed += OnAnyStateChanged;
            _viewModel.Energy.Changed += OnAnyStateChanged;
            _viewModel.MaxEnergy.Changed += OnAnyStateChanged;
            _viewModel.PlayerHp.Changed += OnAnyStateChanged;
            _viewModel.PlayerMaxHp.Changed += OnAnyStateChanged;
            _viewModel.PlayerArmor.Changed += OnAnyStateChanged;
            _viewModel.IsAwaitingWaveConfirmation.Changed += OnAnyStateChanged;
            _viewModel.CurrentWaveTitle.Changed += OnAnyStateChanged;
            _viewModel.CurrentWaveContinueText.Changed += OnAnyStateChanged;
            _viewModel.CardPlayFailed += OnCardPlayFailed;
        }

        private void BindViewEvents()
        {
            if (_gameView == null)
            {
                return;
            }

            BindEvent<Action>(
                h => _gameView.EndTurnClicked += h,
                h => _gameView.EndTurnClicked -= h,
                () => _viewModel?.EndTurn());

            BindEvent<Action>(
                h => _gameView.RewardConfirmClicked += h,
                h => _gameView.RewardConfirmClicked -= h,
                () => _viewModel?.SelectReward());

            BindEvent<Action<int>>(
                h => _gameView.HandCardClicked += h,
                h => _gameView.HandCardClicked -= h,
                OnHandCardClicked);

            BindEvent<Action<int, int>>(
                h => _gameView.MonsterTargetSelected += h,
                h => _gameView.MonsterTargetSelected -= h,
                OnMonsterTargetSelected);
        }

        private void OnHandCardClicked(int handIndex)
        {
            _viewModel?.UseCard(handIndex);
        }

        private void OnMonsterTargetSelected(int handIndex, int monsterIndex)
        {
            _viewModel?.UseCardOnMonster(handIndex, monsterIndex);
        }

        private void OnAnyStateChanged<T>(T _)
        {
            _gameView?.Render(_viewModel);
        }

        private void OnCardPlayFailed(string reason)
        {
            _gameView?.ShowCardPlayFailed(reason);
        }
    }
}
