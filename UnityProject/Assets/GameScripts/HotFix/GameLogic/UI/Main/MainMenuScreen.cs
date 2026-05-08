using EF.Debugger;
using EF.UI;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 主界面 Screen。继承 VisualElement，绑定 MainViewModel 的 ReactiveProperty。
    /// </summary>
    public class MainMenuScreen : Screen<MainViewModel>
    {
        private Label _statusLabel;
        private Label _levelNameLabel;
        private Label _levelDescLabel;
        private Label _feedbackLabel;
        private Button _startBtn;

        /// <inheritdoc />
        protected override void OnSetup()
        {
            _statusLabel = this.Q<Label>("status-text");
            _levelNameLabel = this.Q<Label>("level-name");
            _levelDescLabel = this.Q<Label>("level-desc");
            _feedbackLabel = this.Q<Label>("feedback-text");
            _startBtn = this.Q<Button>("start-btn");

            // 数据绑定：ViewModel → VisualElement
            ViewModel.StatusText.Changed += v => SetText(_statusLabel, v);
            ViewModel.LevelName.Changed += v => SetText(_levelNameLabel, v);
            ViewModel.LevelDesc.Changed += v => SetText(_levelDescLabel, v);
            ViewModel.CanStart.Changed += v =>
            {
                if (_startBtn != null) _startBtn.SetEnabled(v);
            };

            // 命令绑定：VisualElement → ViewModel
            if (_startBtn != null)
            {
                _startBtn.RegisterCallback<ClickEvent>(_ => ViewModel.RequestStart());
            }

            // 初始值同步
            SetText(_statusLabel, ViewModel.StatusText.Value);
            SetText(_levelNameLabel, ViewModel.LevelName.Value);
            SetText(_levelDescLabel, ViewModel.LevelDesc.Value);

            Log.Info("[MainMenuScreen] 主界面绑定完成");
        }

        /// <inheritdoc />
        public override void OnShow()
        {
            Log.Info("[MainMenuScreen] 主界面已显示");
        }

        /// <summary>
        /// 设置反馈文本。
        /// </summary>
        public void SetFeedbackText(string message)
        {
            SetText(_feedbackLabel, message);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
