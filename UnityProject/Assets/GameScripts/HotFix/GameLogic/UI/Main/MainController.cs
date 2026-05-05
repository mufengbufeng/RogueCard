using System;
using EF.Debugger;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 游戏主界面控制器。
    /// </summary>
    public class MainController : UIController
    {
        private MainView _mainView;
        private MainModel _mainModel;

        /// <summary>
        /// 初始化主界面控制器。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            _mainView = GetView<MainView>();
            _mainModel = GetModel<MainModel>();
            Log.Info("[MainController] 主界面控制器初始化完成");
        }

        /// <summary>
        /// 进入主界面。
        /// </summary>
        protected override void OnEnter(object userData)
        {
            base.OnEnter(userData);

            if (_mainView != null)
            {
                BindEvent<Action>(
                    h => _mainView.OnStartGameRequested += h,
                    h => _mainView.OnStartGameRequested -= h,
                    HandleStartGame);
            }

            _mainModel?.SetInteractable(true);
            _mainModel?.SetStatusText(MainModel.ReadyStatusText);
            _mainModel?.SetDefaultLevelInfo(
                MainModel.DefaultLevelIdentifier,
                MainModel.DefaultLevelDisplayName,
                MainModel.DefaultLevelSummary);
            RefreshViewFromModel();
            _mainView?.SetFeedbackText(string.Empty);
            _mainView?.SetStartButtonInteractable(true);

            Log.Info("[MainController] 主界面已打开");
        }

        /// <summary>
        /// 退出主界面。
        /// </summary>
        protected override void OnExit()
        {
            base.OnExit();
            Log.Info("[MainController] 主界面已关闭");
        }

        /// <summary>
        /// 处理主界面按钮请求。
        /// </summary>
        private void HandleStartGame()
        {
            string levelId = _mainModel?.DefaultLevelId ?? MainModel.DefaultLevelIdentifier;
            string levelName = _mainModel?.DefaultLevelName ?? MainModel.DefaultLevelDisplayName;
            var requestEvent = new StartLevelRequestedEvent(levelId, levelName);

            _mainModel?.SetStatusText($"正在进入：{levelName}");
            RefreshViewFromModel();
            _mainView?.SetFeedbackText($"正在进入默认关卡：{levelId}");
            _mainView?.SetStartButtonInteractable(false);

            GameLogicEntry.Event?.StartLevelRequestedEvent.Publish(requestEvent);

            Log.Info($"[MainController] 已请求进入默认关卡：{levelId}");
        }

        /// <summary>
        /// 从模型刷新主界面显示内容。
        /// </summary>
        private void RefreshViewFromModel()
        {
            if (_mainModel == null || _mainView == null)
            {
                return;
            }

            _mainView.SetStatusText(_mainModel.StatusText);
            _mainView.SetLevelInfo(_mainModel.DefaultLevelName, _mainModel.DefaultLevelDescription);
        }
    }
}
