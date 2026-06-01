using System;
using EF.Debugger;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 战斗面板协调器，统一装配怪物、手牌、回合控制和目标选择子视图。
    /// </summary>
    public sealed class BattlePanelView : IDisposable
    {
        private BattlePanelComposition _composition;
        private bool _disposed;

        /// <summary>
        /// 怪物列表视图。
        /// </summary>
        public MonsterListView MonsterListView => _composition?.MonsterListView;

        /// <summary>
        /// 手牌视图。
        /// </summary>
        public HandFanView HandFanView => _composition?.HandFanView;

        /// <summary>
        /// 回合控制视图。
        /// </summary>
        public TurnControlView TurnControlView => _composition?.TurnControlView;

        /// <summary>
        /// 目标选择器。
        /// </summary>
        public TargetSelector TargetSelector => _composition?.TargetSelector;

        /// <summary>
        /// 创建战斗面板并装配子视图。
        /// </summary>
        public BattlePanelView(BattlePanelBindings bindings, IBattleContext context, HandFanLayoutOptions options)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!bindings.HasRequiredBindings)
            {
                Log.Error("[BattlePanelView] 关键绑定缺失，仍会尝试按可用绑定装配。");
            }

            _composition = new BattlePanelComposition(bindings, context, options);
        }

        /// <summary>
        /// 释放子视图。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _composition?.Dispose();
            _composition = null;
        }

        /// <summary>
        /// 显示出牌失败提示。
        /// </summary>
        public void ShowCardPlayFailed(string reason)
        {
            TurnControlView?.ShowCardPlayFailed(reason);
        }
    }

    /// <summary>
    /// 战斗面板 UGUI 绑定集合。
    /// </summary>
    public struct BattlePanelBindings
    {
        public RectTransform MonsterContainer;
        public RectTransform HandContainer;
        public RectTransform DropZone;
        public RectTransform PreviewLayer;
        public Button EndTurnButton;
        public TextMeshProUGUIProxy FailToast;
        public Button CancelTargetButton;
        public GameObject HandCardTemplate;
        public GameObject MonsterItemTemplate;
        public GameObject BuffIconTemplate;
        public GameObject IntentIconTemplate;

        public TMPro.TextMeshProUGUI FailToastText => FailToast.Text;
        public CanvasGroup FailToastGroup => FailToast.Group;

        public bool HasRequiredBindings =>
            MonsterContainer != null &&
            HandContainer != null &&
            DropZone != null &&
            PreviewLayer != null &&
            EndTurnButton != null &&
            HandCardTemplate != null &&
            MonsterItemTemplate != null;
    }

    /// <summary>
    /// 失败提示文本和 CanvasGroup 绑定。
    /// </summary>
    public struct TextMeshProUGUIProxy
    {
        public TMPro.TextMeshProUGUI Text;
        public CanvasGroup Group;
    }
}
