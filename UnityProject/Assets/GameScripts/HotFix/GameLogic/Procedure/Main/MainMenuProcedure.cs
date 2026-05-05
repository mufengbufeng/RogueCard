using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Procedure;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 主菜单流程。
    /// </summary>
    public class MainMenuProcedure : ProcedureBase
    {
        private EF.UI.IUIManager _uiManager;
        private ProcedureOwner _procedureOwner;

        /// <summary>
        /// 初始化主菜单流程。
        /// </summary>
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            _uiManager = GameLogicEntry.UI;
            Log.Info("[MainMenuProcedure] OnInit");
        }

        /// <summary>
        /// 进入主菜单流程。
        /// </summary>
        protected override async void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _procedureOwner = procedureOwner;
            GameLogicEntry.Event?.StartLevelRequestedEvent.Subscribe(HandleStartLevelRequested);
            Log.Info("[MainMenuProcedure] OnEnter - 打开主界面");

            try
            {
                await _uiManager.OpenWindowAsync<MainView, MainController>(
                    location: "MainView",
                    layer: EF.UI.UILayer.Normal,
                    cacheOnClose: false,
                    allowMultiple: false);
                Log.Info("[MainMenuProcedure] 主界面已打开");
            }
            catch (System.Exception e)
            {
                Log.Error($"[MainMenuProcedure] 打开主界面失败：{e.Message}");
            }
        }

        /// <summary>
        /// 离开主菜单流程。
        /// </summary>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            UnsubscribeStartLevelRequestedEvent();
            Log.Info("[MainMenuProcedure] OnLeave - 关闭主界面");

            try
            {
                _uiManager.CloseWindowAsync("MainView").Forget();
            }
            catch (System.Exception e)
            {
                Log.Warning($"[MainMenuProcedure] 关闭主界面时出错：{e.Message}");
            }
        }

        /// <summary>
        /// 销毁主菜单流程。
        /// </summary>
        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            UnsubscribeStartLevelRequestedEvent();
            base.OnDestroy(procedureOwner);
            Log.Info("[MainMenuProcedure] OnDestroy");
        }

        /// <summary>
        /// 处理默认关卡进入请求。
        /// </summary>
        private void HandleStartLevelRequested(StartLevelRequestedEvent requestEvent)
        {
            if (_procedureOwner == null)
            {
                Log.Warning("[MainMenuProcedure] 流程状态机未就绪，无法进入局内流程");
                return;
            }

            Log.Info($"[MainMenuProcedure] 收到默认关卡进入请求：{requestEvent.LevelId}，切换到局内流程");
            ChangeState<GameProcedure>(_procedureOwner);
        }

        /// <summary>
        /// 取消订阅默认关卡进入请求。
        /// </summary>
        private void UnsubscribeStartLevelRequestedEvent()
        {
            GameLogicEntry.Event?.StartLevelRequestedEvent.Unsubscribe(HandleStartLevelRequested);
            _procedureOwner = null;
        }
    }
}
