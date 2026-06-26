using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    /// <summary>
    /// 战斗面板子视图组合，集中管理装配、阶段订阅和释放顺序。
    /// </summary>
    internal sealed class BattlePanelComposition : IDisposable
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
        /// 创建战斗面板子视图组合并完成阶段监听。
        /// </summary>
        public BattlePanelComposition(BattlePanelBindings bindings, IBattleContext context, HandFanLayoutOptions options)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            MonsterListView = new MonsterListView(bindings.MonsterContainer, _context, bindings.MonsterItemTemplate, bindings.BuffIconTemplate, bindings.IntentIconTemplate);
            HandFanView = new HandFanView(bindings.HandContainer, bindings.DropZone, bindings.PreviewLayer, _context, bindings.HandCardTemplate, options);
            TurnControlView = new TurnControlView(bindings.EndTurnButton, bindings.FailToastText, bindings.FailToastGroup, _context, ShouldSuppressEndTurn);
            TargetSelector = new TargetSelector(MonsterListView, HandFanView, _context, bindings.CancelTargetButton);

            HandFanView.CardDroppedOnZone += OnCardDroppedOnZone;
            HandFanView.CardClicked += OnCardClicked;
            HandFanView.CardDragCancelled += OnCardDragCancelled;
            AttachPreviewDismissBeforeClickRelay(bindings.EndTurnButton);

            _onPhaseChanged = OnPhaseChanged;
            ((ITurnContext)_context).Phase.Changed += _onPhaseChanged;
        }

        /// <summary>
        /// 释放子视图和阶段监听。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_onPhaseChanged != null)
            {
                ((ITurnContext)_context).Phase.Changed -= _onPhaseChanged;
                _onPhaseChanged = null;
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
        /// 阶段离开玩家回合时取消目标选择。
        /// </summary>
        private void OnPhaseChanged(BattlePhase phase)
        {
            if (phase != BattlePhase.PlayerTurn && TargetSelector != null && TargetSelector.IsActive)
            {
                TargetSelector.Cancel();
            }
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
        }

        private void OnCardDragCancelled(int handIdx)
        {
        }

        private bool ShouldSuppressEndTurn()
        {
            bool shouldSuppress = _suppressNextEndTurnClick;
            _suppressNextEndTurnClick = false;
            return shouldSuppress;
        }

        private void AttachPreviewDismissBeforeClickRelay(UnityEngine.UI.Button button)
        {
            if (button == null)
            {
                return;
            }

            var relay = button.GetComponent<PreviewDismissBeforeClickRelay>() ?? button.gameObject.AddComponent<PreviewDismissBeforeClickRelay>();
            relay.Initialize(this);
        }

        private sealed class PreviewDismissBeforeClickRelay : MonoBehaviour, IPointerDownHandler
        {
            private BattlePanelComposition _owner;

            public void Initialize(BattlePanelComposition owner)
            {
                _owner = owner;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (_owner == null || _owner.HandFanView == null)
                {
                    return;
                }

                GameObject target = eventData.pointerPressRaycast.gameObject;
                if (_owner.HandFanView.TryDismissPreviewFromPointerTarget(target))
                {
                    _owner._suppressNextEndTurnClick = true;
                }
            }
        }
    }
}
