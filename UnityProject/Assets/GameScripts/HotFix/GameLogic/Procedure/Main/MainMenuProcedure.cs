using System;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Procedure;
using EF.UI;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 主菜单流程。通过 UGUI UIManager 打开 MainView，订阅开始关卡事件。
    /// </summary>
    public class MainMenuProcedure : ProcedureBase
    {
        private IUIManager _uiManager;
        private ProcedureOwner _procedureOwner;

        /// <summary>
        /// 初始化主菜单流程并记录初始化日志。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            Log.Info("[MainMenuProcedure] OnInit");
        }

        /// <summary>
        /// 进入主菜单流程，缓存流程持有者和 UI 管理器，订阅开始关卡事件并异步打开主界面。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _procedureOwner = procedureOwner;
            _uiManager = GameLogicEntry.UI;
            GameLogicEntry.Event.StartLevelRequestedEvent.Subscribe(OnStartRequested);
            EnterAsync().Forget();
        }

        private async UniTaskVoid EnterAsync()
        {
            try
            {
                if (_uiManager == null)
                {
                    Log.Error("[MainMenuProcedure] UIManager 未初始化（GameLogicEntry.UI 为 null）。");
                    return;
                }

                await _uiManager.OpenWindowAsync<MainView>(
                    "MainView",
                    UILayer.Normal);
                Log.Info("[MainMenuProcedure] 主界面已打开");
            }
            catch (Exception e)
            {
                Log.Error($"[MainMenuProcedure] 进入主界面失败：{e}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 离开主菜单流程，关闭主界面并清理事件订阅和缓存引用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        /// <param name="isShutdown">是否因状态机关闭而离开。</param>
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            _uiManager?.CloseWindowAsync(nameof(MainView)).Forget();
            Cleanup();
            Log.Info("[MainMenuProcedure] OnLeave");
        }

        /// <summary>
        /// 销毁主菜单流程时清理事件订阅和缓存引用。
        /// </summary>
        /// <param name="procedureOwner">流程持有者。</param>
        protected override void OnDestroy(ProcedureOwner procedureOwner)
        {
            Cleanup();
            base.OnDestroy(procedureOwner);
        }

        /// <summary>
        /// 处理开始游戏意图。
        /// </summary>
        private void OnStartRequested(StartLevelRequestedEvent evt)
        {
            if (_procedureOwner == null)
            {
                Log.Warning("[MainMenuProcedure] 流程状态机未就绪");
                return;
            }

            int levelId = evt.LevelId;
            Log.Info($"[MainMenuProcedure] 请求进入关卡：{levelId}，切换到局内流程");
            GameProcedure.PendingLevelId = levelId;
            ChangeState<GameProcedure>(_procedureOwner);
        }

        /// <summary>
        /// 清理 ViewModel 订阅。
        /// </summary>
        private void Cleanup()
        {
            GameLogicEntry.Event?.StartLevelRequestedEvent.Unsubscribe(OnStartRequested);

            _uiManager = null;
            _procedureOwner = null;
        }
    }
}
