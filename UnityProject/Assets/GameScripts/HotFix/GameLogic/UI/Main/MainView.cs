using System;
using EF.Debugger;
using EF.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 主界面视图。
    /// </summary>
    public class MainView : UIView
    {
                #region 自动生成
        [UHubBind("FeedbackText")] private TextMeshProUGUI _feedbackText;
        [UHubBind("LevelDescriptionText")] private TextMeshProUGUI _levelDescriptionText;
        [UHubBind("LevelNameText")] private TextMeshProUGUI _levelNameText;
        [UHubBind("StartGameBtn")] private Button _startGameBtn;
        #endregion

        // public Button _startGameBtn;
        // public TextMeshProUGUI _statusText;
        // public TextMeshProUGUI _levelNameText;
        // public TextMeshProUGUI _levelDescriptionText;
        // public TextMeshProUGUI _feedbackText;

        /// <summary>
        /// 主界面按钮点击事件。
        /// </summary>
        public event Action OnStartGameRequested;

        /// <summary>
        /// 初始化主界面视图。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            UHub.Initialize();
        }

        /// <summary>
        /// 绑定主界面 UGUI 事件。
        /// </summary>
        protected override void OnBindings()
        {
            base.OnBindings();

            if (_startGameBtn != null)
            {
                BindEvent(_startGameBtn.onClick, OnStartGameButtonClicked);
            }

            Log.Info("[MainView] 主界面绑定完成");
        }

        /// <summary>
        /// 打开主界面。
        /// </summary>
        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            EnsureRuntimeTextComponents();
            Log.Info("[MainView] 主界面已显示");
        }

        /// <summary>
        /// 设置主界面状态文本。
        /// </summary>
        public void SetStatusText(string message)
        {
            EnsureRuntimeTextComponents();
            // SetText(_statusText, message);
        }

        /// <summary>
        /// 设置默认关卡展示信息。
        /// </summary>
        public void SetLevelInfo(string levelName, string levelDescription)
        {
            EnsureRuntimeTextComponents();
            SetText(_levelNameText, levelName);
            SetText(_levelDescriptionText, levelDescription);
        }

        /// <summary>
        /// 设置反馈文本。
        /// </summary>
        public void SetFeedbackText(string message)
        {
            EnsureRuntimeTextComponents();
            SetText(_feedbackText, message);
        }

        /// <summary>
        /// 设置开始按钮是否可交互。
        /// </summary>
        public void SetStartButtonInteractable(bool interactable)
        {
            if (_startGameBtn != null)
            {
                _startGameBtn.interactable = interactable;
            }
        }

        /// <summary>
        /// 释放主界面视图。
        /// </summary>
        protected override void OnRelease()
        {
            OnStartGameRequested = null;
            base.OnRelease();
        }

        private void OnStartGameButtonClicked()
        {
            Log.Info("[MainView] 主界面开始按钮被点击");
            OnStartGameRequested?.Invoke();
        }

        /// <summary>
        /// 测试入口：模拟开始按钮点击。
        /// </summary>
        internal void NotifyStartGameRequestedForTests()
        {
            OnStartGameRequested?.Invoke();
        }

        private void EnsureRuntimeTextComponents()
        {
            var root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            _levelNameText ??= CreateRuntimeText(root, "LevelNameTextRuntime", new Vector2(0f, 170f), new Vector2(720f, 64f), 38, Color.white);
            _levelDescriptionText ??= CreateRuntimeText(root, "LevelDescriptionTextRuntime", new Vector2(0f, 105f), new Vector2(760f, 56f), 26, new Color(0.86f, 0.86f, 0.86f, 1f));
            // _statusText ??= CreateRuntimeText(root, "StatusTextRuntime", new Vector2(0f, 45f), new Vector2(600f, 52f), 28, Color.white);
            _feedbackText ??= CreateRuntimeText(root, "FeedbackTextRuntime", new Vector2(0f, 120f), new Vector2(760f, 48f), 26, new Color(0.9f, 0.9f, 0.9f, 1f));
        }

        private static TextMeshProUGUI CreateRuntimeText(RectTransform parent, string objectName, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
