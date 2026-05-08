using System;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Event;
using EF.Procedure;
using EF.UI;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 局内游戏流程。创建 GameViewModel、GameSystem，
    /// 通过 Navigator 打开 GameScreen，订阅 ViewModel 命令意图转发到 System。
    /// </summary>
    public class GameProcedure : ProcedureBase
    {
        private INavigator _navigator;
        private GameViewModel _viewModel;
        private CardSystem _cardSystem;
        private MonsterSystem _monsterSystem;
        private BattleSystem _battleSystem;
        private WaveSystem _waveSystem;
        private LocalEventBus _localEventBus;

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
            // 延迟到 OnEnter 才读 Navigator
            _navigator = GameLogicEntry.Navigator;
            EnterAsync(procedureOwner).Forget();
        }

        private async UniTaskVoid EnterAsync(ProcedureOwner procedureOwner)
        {
            try
            {
                int levelId = PendingLevelId;
                PendingLevelId = 0;
                Log.Info($"[GameProcedure] OnEnter - 关卡标识：{levelId}");

                // 获取 GameModel（由 GameLogicEntry 在 ModelManager 中注册）
                var gameModel = GameLogicEntry.Model.GetModel<GameModel>();

                // 创建局部事件总线
                _localEventBus = new LocalEventBus();

                // 创建 ViewModel 并绑定 Model
                _viewModel = new GameViewModel { LevelId = levelId };
                _viewModel.BindModel(gameModel);
                _viewModel.CardUsed += OnCardUsed;
                _viewModel.EndTurnRequested += OnEndTurnRequested;

                // 创建并初始化 System
                _cardSystem = new CardSystem();
                _monsterSystem = new MonsterSystem();
                _battleSystem = new BattleSystem();
                _waveSystem = new WaveSystem();

                _cardSystem.Init(gameModel, _localEventBus);
                _monsterSystem.Init(gameModel, _localEventBus);
                _battleSystem.Init(gameModel, _localEventBus);
                _waveSystem.Init(gameModel, _localEventBus);

                _battleSystem.Initialize(_cardSystem, _monsterSystem);
                _waveSystem.Initialize(_battleSystem);

                _waveSystem.StartLevel(levelId);

                await _navigator.NavigateToAsync("Game", _viewModel);
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
        /// 转发使用卡牌操作到 CardSystem。
        /// </summary>
        private void OnCardUsed(int handIndex) => _cardSystem.Play(handIndex);

        /// <summary>
        /// 转发结束回合操作到 BattleSystem。
        /// </summary>
        private void OnEndTurnRequested() => _battleSystem.EndTurn();

        /// <summary>
        /// 清理所有 System。ViewModel 由 Screen.OnDispose 负责销毁，
        /// 此处仅取消订阅以避免残留事件回调。
        /// </summary>
        private void Cleanup()
        {
            if (_viewModel != null)
            {
                _viewModel.CardUsed -= OnCardUsed;
                _viewModel.EndTurnRequested -= OnEndTurnRequested;
                _viewModel = null;
            }

            _waveSystem?.Dispose();
            _battleSystem?.Dispose();
            _monsterSystem?.Dispose();
            _cardSystem?.Dispose();
            _localEventBus?.Dispose();

            _waveSystem = null;
            _battleSystem = null;
            _monsterSystem = null;
            _cardSystem = null;
            _localEventBus = null;
        }
    }
}
