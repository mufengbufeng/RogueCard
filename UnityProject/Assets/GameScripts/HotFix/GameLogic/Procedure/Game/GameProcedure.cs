using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Procedure;
using EF.UI;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 局内游戏流程。
    /// </summary>
    public class GameProcedure : ProcedureBase
    {
        private IUIManager _uiManager;

        /// <summary>
        /// 初始化局内游戏流程。
        /// </summary>
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            _uiManager = GameLogicEntry.UI;
            Log.Info("[GameProcedure] OnInit");
        }

        /// <summary>
        /// 进入局内游戏流程。
        /// </summary>
        protected override async void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            Log.Info("[GameProcedure] OnEnter - 打开局内界面");

            try
            {
                await _uiManager.OpenWindowAsync<GameView, GameController>(
                    location: "GameView",
                    layer: UILayer.Normal,
                    cacheOnClose: false,
                    allowMultiple: false);
                Log.Info("[GameProcedure] 局内界面已打开");
            }
            catch (System.Exception e)
            {
                Log.Error($"[GameProcedure] 打开局内界面失败：{e.Message}");
            }
        }

        /// <summary>
        /// 离开局内游戏流程。
        /// </summary>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            Log.Info("[GameProcedure] OnLeave - 关闭局内界面");

            try
            {
                _uiManager.CloseWindowAsync("GameView").Forget();
            }
            catch (System.Exception e)
            {
                Log.Warning($"[GameProcedure] 关闭局内界面时出错：{e.Message}");
            }
        }

        /// <summary>
        /// 销毁局内游戏流程。
        /// </summary>
        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
            Log.Info("[GameProcedure] OnDestroy");
        }
    }
}
