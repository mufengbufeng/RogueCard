using System;
using EF.Debugger;
using EF.UI;
using VContainer;

namespace GameLogic
{
    /// <summary>
    /// UGUI 主界面控制器。
    /// </summary>
    public class MainController : UIController
    {
        private MainView _mainView;
        private MainModel _mainModel;

        [Inject]
        private EventHub _eventHub;

        /// <summary>
        /// 初始化主界面控制器。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            _mainView = GetView<MainView>();
            _mainModel = TryGetModel<MainModel>();
            _mainModel.LoadDefaultLevelFromConfig();
            Log.Info("[MainController] 主界面控制器初始化完成");
        }

        /// <summary>
        /// 进入主界面并绑定按钮事件。
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
            RefreshViewFromModel();
            _mainView?.SetFeedbackText(string.Empty);
            Log.Info("[MainController] 主界面已进入");
        }

        /// <summary>
        /// 刷新主界面显示。
        /// </summary>
        protected override void OnRefresh(object userData)
        {
            base.OnRefresh(userData);
            RefreshViewFromModel();
        }

        private void HandleStartGame()
        {
            int levelId = _mainModel?.DefaultLevelId ?? MainModel.FallbackLevelId;
            string levelName = _mainModel?.DefaultLevelName ?? MainModel.FallbackLevelName;

            _mainModel?.SetStatusText($"正在进入：{levelName}");
            _mainModel?.SetInteractable(false);
            RefreshViewFromModel();
            _mainView?.SetFeedbackText($"正在进入默认关卡：{levelId}");

            (_eventHub ?? GameLogicEntry.Event)?.StartLevelRequestedEvent.Publish(new StartLevelRequestedEvent(levelId, levelName));
            Log.Info($"[MainController] 已请求进入默认关卡：{levelId}");
        }

        private void RefreshViewFromModel()
        {
            if (_mainModel == null || _mainView == null)
            {
                return;
            }

            _mainView.SetStartButtonInteractable(_mainModel.IsInteractable);
            _mainView.SetStatusText(_mainModel.StatusText);
            _mainView.SetLevelInfo(_mainModel.DefaultLevelName, _mainModel.DefaultLevelDescription);
        }
    }
}
