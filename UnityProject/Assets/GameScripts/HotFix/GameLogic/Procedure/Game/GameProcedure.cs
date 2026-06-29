using System;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Procedure;
using EF.UI;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 局内游戏流程。创建 GameViewModel、GameSystem，
    /// 通过 UGUI UIManager 打开 GameView，订阅 ViewModel 命令意图转发到 System。
    /// </summary>
    public class GameProcedure : ProcedureBase
    {
        private IUIManager _uiManager;
        private ProcedureOwner _procedureOwner;
        private GameViewModel _viewModel;
        private CardSystem _cardSystem;
        private MonsterSystem _monsterSystem;
        private MonsterCardSystem _monsterCardSystem;
        private BattleSystem _battleSystem;
        private WaveSystem _waveSystem;
        private LocalEventBus _localEventBus;
        private bool _isLevelCompleteAwaitingConfirm;

        /// <summary>
        /// 待进入的关卡标识（由 MainMenuProcedure 在切换前设置）。
        /// </summary>
        public static int PendingLevelId { get; set; }

        /// <inheritdoc />
        protected internal override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            Log.Info("[GameProcedure] OnInit");
        }

        /// <inheritdoc />
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _procedureOwner = procedureOwner;
            _uiManager = GameLogicEntry.UI;
            EnterAsync(procedureOwner).Forget();
        }

        private async UniTaskVoid EnterAsync(ProcedureOwner procedureOwner)
        {
            try
            {
                int levelId = PendingLevelId;
                PendingLevelId = 0;
                Log.Info($"[GameProcedure] OnEnter - 关卡标识：{levelId}");

                // 获取 GameModel（按需懒注册——ModelManager.TryGetModel 在首次访问时自动构造）
                var gameModel = GameLogicEntry.Model.TryGetModel<GameModel>();

                // 创建局部事件总线
                _localEventBus = new LocalEventBus();

                // 创建 ViewModel 并绑定 Model
                _viewModel = new GameViewModel { LevelId = levelId };
                _viewModel.BindModel(gameModel);
                _viewModel.CardUsed += OnCardUsed;
                _viewModel.EndTurnRequested += OnEndTurnRequested;
                _viewModel.RewardSelected += OnRewardSelected;

                // 订阅卡牌出牌失败事件，转发到 ViewModel 让 GameView 显示 toast
                _localEventBus.GetChannel<CardPlayFailedEvent>().Subscribe(OnCardPlayFailed);

                // 订阅关卡完成事件：所有波次完成后由 WaveSystem 发布，触发流程切回主菜单
                _localEventBus.GetChannel<LevelCompleteEvent>().Subscribe(OnLevelComplete);

                // 创建并初始化 System
                _cardSystem = new CardSystem();
                _monsterSystem = new MonsterSystem();
                _monsterCardSystem = new MonsterCardSystem();
                _battleSystem = new BattleSystem();
                _waveSystem = new WaveSystem();

                _cardSystem.Init(gameModel, _localEventBus);
                _monsterSystem.Init(gameModel, _localEventBus);
                _battleSystem.Init(gameModel, _localEventBus);
                _waveSystem.Init(gameModel, _localEventBus);

                _monsterSystem.Initialize(_monsterCardSystem);
                _battleSystem.Initialize(_cardSystem, _monsterSystem, _monsterCardSystem);
                _waveSystem.Initialize(_battleSystem);

                _waveSystem.StartLevel(levelId);

                await _uiManager.OpenWindowAsync<GameView>(
                    "GameView",
                    UILayer.Normal,
                    cacheOnClose: false,
                    allowMultiple: false,
                    userData: _viewModel);
                Log.Info($"[GameProcedure] 局内界面已打开，关卡标识：{levelId}");
            }
            catch (Exception e)
            {
                Log.Error($"[GameProcedure] 进入局内流程失败：{e}");
            }
        }

        /// <inheritdoc />
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            _uiManager?.CloseWindowAsync(nameof(GameView)).Forget();
            Cleanup();
            Log.Info("[GameProcedure] OnLeave");
        }

        /// <inheritdoc />
        protected internal override void OnDestroy(ProcedureOwner procedureOwner)
        {
            Cleanup();
            base.OnDestroy(procedureOwner);
        }

        /// <summary>
        /// 转发使用卡牌操作到 CardSystem。targetIndex 仅 SingleManual 卡使用。
        /// </summary>
        private void OnCardUsed(int handIndex, int targetIndex) => _cardSystem.Play(handIndex, targetIndex);

        /// <summary>
        /// 转发结束回合操作到 BattleSystem。
        /// </summary>
        private void OnEndTurnRequested() => _battleSystem.EndTurn();

        /// <summary>
        /// 转发确认命令：优先处理事件波次确认，其次处理关卡完成确认。
        /// </summary>
        private void OnRewardSelected()
        {
            // 优先：事件波次等待确认 → 推进到下一波次
            if (_waveSystem != null && _viewModel != null && _viewModel.IsAwaitingWaveConfirmation.Value)
            {
                Log.Info("[GameProcedure] 事件波次确认，推进到下一波次");
                _waveSystem.ConfirmCurrentWave();
                return;
            }

            // 其次：关卡完成等待确认 → 切回主菜单
            if (_isLevelCompleteAwaitingConfirm)
            {
                if (_procedureOwner == null)
                {
                    Log.Warning("[GameProcedure] 关卡完成确认但流程状态机未就绪，无法切回主菜单");
                    return;
                }

                Log.Info("[GameProcedure] 关卡完成确认，切回主菜单流程");
                ChangeState<MainMenuProcedure>(_procedureOwner);
                return;
            }

            Log.Warning("[GameProcedure] 收到确认命令但无待处理的确认状态");
        }

        /// <summary>
        /// 转发卡牌出牌失败事件到 ViewModel。
        /// </summary>
        private void OnCardPlayFailed(CardPlayFailedEvent evt)
        {
            _viewModel?.NotifyCardPlayFailed(evt.Reason);
        }

        /// <summary>
        /// 关卡完成回调：记录关卡完成等待确认状态，不再立即切回主菜单。
        /// </summary>
        private void OnLevelComplete(LevelCompleteEvent evt)
        {
            Log.Info($"[GameProcedure] 关卡完成（LevelId={evt.LevelId}），等待玩家确认");
            _isLevelCompleteAwaitingConfirm = true;
        }

        /// <summary>
        /// 清理所有 System。ViewModel 由 UIController 解除绑定，
        /// 此处仅取消订阅以避免残留事件回调；多次调用安全（取订与 Dispose 在 null 检查与幂等字段之后）。
        /// </summary>
        private void Cleanup()
        {
            _isLevelCompleteAwaitingConfirm = false;

            if (_localEventBus != null)
            {
                _localEventBus.GetChannel<CardPlayFailedEvent>().Unsubscribe(OnCardPlayFailed);
                _localEventBus.GetChannel<LevelCompleteEvent>().Unsubscribe(OnLevelComplete);
            }

            if (_viewModel != null)
            {
                _viewModel.CardUsed -= OnCardUsed;
                _viewModel.EndTurnRequested -= OnEndTurnRequested;
                _viewModel.RewardSelected -= OnRewardSelected;
                _viewModel = null;
            }

            _waveSystem?.Dispose();
            _battleSystem?.Dispose();
            _monsterCardSystem?.Dispose();
            _monsterSystem?.Dispose();
            _cardSystem?.Dispose();
            _localEventBus?.Dispose();

            _waveSystem = null;
            _battleSystem = null;
            _monsterCardSystem = null;
            _monsterSystem = null;
            _cardSystem = null;
            _localEventBus = null;
            _uiManager = null;
            _procedureOwner = null;
        }
    }
}
