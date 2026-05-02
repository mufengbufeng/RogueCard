using System;
using EF.Debugger;
using EF.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 游戏主界面视图。
    /// </summary>
    public class MainView : UIView
    {
        public Button _startGameBtn;
        public TextMeshProUGUI _statusText;
        public TextMeshProUGUI _levelNameText;
        public TextMeshProUGUI _levelDescriptionText;
        public TextMeshProUGUI _feedbackText;

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
        /// 绑定主界面事件。
        /// </summary>
        protected override void OnBindings()
        {
            base.OnBindings();

            if (_startGameBtn != null)
            {
                BindEvent(_startGameBtn.onClick, OnStartGameButtonClicked);
            }

            Log.Info($"[MainView] UHub 初始化完成，绑定了 {UHub.EventBindingCount} 个事件");
        }

        /// <summary>
        /// 打开主界面。
        /// </summary>
        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            EnsureRuntimeTextComponents();

            if (_startGameBtn != null)
            {
                Log.Info("[MainView] 主界面按钮组件绑定成功");
            }
            else
            {
                Log.Warning("[MainView] 主界面按钮组件绑定失败，请检查 ReferenceCollector 配置");
            }

            SetStatusText(MainModel.ReadyStatusText);
            SetLevelInfo(MainModel.DefaultLevelDisplayName, MainModel.DefaultLevelSummary);
            SetFeedbackText(string.Empty);
        }

        /// <summary>
        /// 刷新主界面。
        /// </summary>
        protected override void OnRefresh(object userData)
        {
            base.OnRefresh(userData);

            if (TryGetModelData<IMainModelData>(out var modelData))
            {
                SetStartButtonInteractable(modelData.IsInteractable);
                SetStatusText(modelData.StatusText);
                SetLevelInfo(modelData.DefaultLevelName, modelData.DefaultLevelDescription);
            }
        }

        /// <summary>
        /// 设置主界面状态文本。
        /// </summary>
        public void SetStatusText(string message)
        {
            EnsureRuntimeTextComponents();
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
            }
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
        /// 设置主界面反馈文本。
        /// </summary>
        public void SetFeedbackText(string message)
        {
            EnsureRuntimeTextComponents();
            if (_feedbackText != null)
            {
                _feedbackText.text = message ?? string.Empty;
            }
        }

        /// <summary>
        /// 设置主界面按钮交互状态。
        /// </summary>
        public void SetStartButtonInteractable(bool interactable)
        {
            if (_startGameBtn != null)
            {
                _startGameBtn.interactable = interactable;
            }
        }

        private void OnStartGameButtonClicked()
        {
            Log.Info("[MainView] 主界面按钮被点击");
            OnStartGameRequested?.Invoke();
        }

        private void EnsureRuntimeTextComponents()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            if (_levelNameText == null)
            {
                _levelNameText = CreateRuntimeText(
                    root,
                    "LevelNameTextRuntime",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 170f),
                    new Vector2(720f, 64f),
                    38,
                    Color.white,
                    TextAlignmentOptions.Center);
            }

            if (_levelDescriptionText == null)
            {
                _levelDescriptionText = CreateRuntimeText(
                    root,
                    "LevelDescriptionTextRuntime",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 105f),
                    new Vector2(760f, 56f),
                    26,
                    new Color(0.86f, 0.86f, 0.86f, 1f),
                    TextAlignmentOptions.Center);
            }

            if (_statusText == null)
            {
                _statusText = CreateRuntimeText(
                    root,
                    "StatusTextRuntime",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 45f),
                    new Vector2(600f, 52f),
                    28,
                    Color.white,
                    TextAlignmentOptions.Center);
            }

            if (_feedbackText == null)
            {
                _feedbackText = CreateRuntimeText(
                    root,
                    "FeedbackTextRuntime",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 120f),
                    new Vector2(760f, 48f),
                    26,
                    new Color(0.9f, 0.9f, 0.9f, 1f),
                    TextAlignmentOptions.Center);
            }
        }

        private static TextMeshProUGUI CreateRuntimeText(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        /// <summary>
        /// 设置文本组件内容。
        /// </summary>
        private static void SetText(TextMeshProUGUI text, string message)
        {
            if (text != null)
            {
                text.text = message ?? string.Empty;
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
    }
}
