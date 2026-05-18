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
        private readonly IBattleContext _context;
        private Action<BattlePhase> _onPhaseChanged;
        private bool _suppressNextEndTurnClick;
        private bool _disposed;

        /// <summary>
        /// 怪物列表视图。
        /// </summary>
        public MonsterListView MonsterListView { get; private set; }

        /// <summary>
        /// 手牌视图。
        /// </summary>
        public HandFanView HandFanView { get; private set; }

        /// <summary>
        /// 回合控制视图。
        /// </summary>
        public TurnControlView TurnControlView { get; private set; }

        /// <summary>
        /// 目标选择器。
        /// </summary>
        public TargetSelector TargetSelector { get; private set; }

        /// <summary>
        /// 创建战斗面板并装配子视图。
        /// </summary>
        public BattlePanelView(BattlePanelBindings bindings, IBattleContext context, HandFanLayoutOptions options)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            if (!bindings.HasRequiredBindings)
            {
                Log.Error("[BattlePanelView] 关键绑定缺失，仍会尝试按可用绑定装配。");
            }

            MonsterListView = new MonsterListView(bindings.MonsterContainer, _context, bindings.MonsterItemTemplate, bindings.BuffIconTemplate, bindings.IntentIconTemplate);
            HandFanView = new HandFanView(bindings.HandContainer, bindings.DropZone, bindings.PreviewLayer, _context, bindings.HandCardTemplate, options);
            TurnControlView = new TurnControlView(bindings.EndTurnButton, bindings.FailToastText, bindings.FailToastGroup, _context, ShouldSuppressEndTurn);
            TargetSelector = new TargetSelector(MonsterListView, HandFanView, _context, bindings.CancelTargetButton);

            HandFanView.CardDroppedOnZone += OnCardDroppedOnZone;
            HandFanView.CardClicked += OnCardClicked;
            HandFanView.CardDragCancelled += OnCardDragCancelled;
            if (bindings.EndTurnButton != null)
            {
                var relay = bindings.EndTurnButton.GetComponent<PreviewDismissBeforeClickRelay>() ??
                            bindings.EndTurnButton.gameObject.AddComponent<PreviewDismissBeforeClickRelay>();
                relay.Initialize(HandFanView, () => _suppressNextEndTurnClick = true);
            }

            _onPhaseChanged = OnPhaseChanged;
            ((ITurnContext)_context).Phase.Changed += _onPhaseChanged;
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
            if (_context != null && _onPhaseChanged != null)
            {
                ((ITurnContext)_context).Phase.Changed -= _onPhaseChanged;
            }

            if (HandFanView != null)
            {
                HandFanView.CardDroppedOnZone -= OnCardDroppedOnZone;
                HandFanView.CardClicked -= OnCardClicked;
                HandFanView.CardDragCancelled -= OnCardDragCancelled;
            }

            TargetSelector?.Dispose();
            TurnControlView?.Dispose();
            HandFanView?.Dispose();
            MonsterListView?.Dispose();
            TargetSelector = null;
            TurnControlView = null;
            HandFanView = null;
            MonsterListView = null;
        }

        /// <summary>
        /// 显示出牌失败提示。
        /// </summary>
        public void ShowCardPlayFailed(string reason)
        {
            TurnControlView?.ShowCardPlayFailed(reason);
        }

        private void OnCardDroppedOnZone(int handIdx, bool needsManualTarget)
        {
            if (needsManualTarget)
            {
                TargetSelector?.Enter(handIdx);
                return;
            }

            _context.UseCard(handIdx);
        }

        private void OnCardClicked(int handIdx)
        {
            // 点击预览由 HandFanView 内部处理，BattlePanel 只保留事件订阅边界。
        }

        private void OnCardDragCancelled(int handIdx)
        {
            // 拖拽取消后的回弹和清理由 HandFanView/CardDragController 负责。
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (phase != BattlePhase.PlayerTurn && TargetSelector != null && TargetSelector.IsActive)
            {
                TargetSelector.Cancel();
            }
        }

        private bool ShouldSuppressEndTurn()
        {
            if (_suppressNextEndTurnClick)
            {
                _suppressNextEndTurnClick = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 在 Button.onClick 前尝试关闭预览，并标记跳过本次按钮命令。
        /// </summary>
        private sealed class PreviewDismissBeforeClickRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
        {
            private HandFanView _handFanView;
            private Action _onConsumed;

            public void Initialize(HandFanView handFanView, Action onConsumed)
            {
                _handFanView = handFanView;
                _onConsumed = onConsumed;
            }

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (_handFanView != null && _handFanView.TryDismissPreviewFromPointerTarget(gameObject))
                {
                    _onConsumed?.Invoke();
                }
            }
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
