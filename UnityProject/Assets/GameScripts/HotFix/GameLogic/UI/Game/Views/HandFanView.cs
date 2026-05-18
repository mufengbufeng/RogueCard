using System;
using System.Collections.Generic;
using EF.Debugger;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    /// <summary>
    /// UGUI 手牌扇形视图，装配卡牌项、扇形布局和拖拽控制器。
    /// </summary>
    public sealed class HandFanView : IDisposable
    {
        private readonly RectTransform _handFan;
        private readonly RectTransform _dropZone;
        private readonly RectTransform _previewLayer;
        private readonly IHandContext _context;
        private readonly GameObject _cardTemplate;
        private readonly HandFanLayoutOptions _options;
        private readonly List<CardItemView> _cardItems = new();
        private readonly DragCallbacks _callbacks;

        private UguiDragSurface _dragSurface;
        private CardDragController _dragController;
        private CardPreviewController _previewController;
        private Action<IReadOnlyList<CardRuntime>> _onHandChanged;
        private bool _disposed;

        /// <summary>
        /// 当前卡牌项。
        /// </summary>
        public IReadOnlyList<CardItemView> CardItems => _cardItems;

        /// <summary>
        /// 单击卡牌事件。
        /// </summary>
        public event Action<int> CardClicked;

        /// <summary>
        /// 卡牌释放到出牌区域事件。
        /// </summary>
        public event Action<int, bool> CardDroppedOnZone;

        /// <summary>
        /// 卡牌拖拽取消事件。
        /// </summary>
        public event Action<int> CardDragCancelled;

        /// <summary>
        /// 当前是否有卡牌预览。
        /// </summary>
        public bool IsPreviewing => _previewController != null && _previewController.IsPreviewing;

        /// <summary>
        /// 测试专用：ghost 立即清理请求次数。
        /// </summary>
        internal int GhostCleanupRequestCount { get; private set; }

        /// <summary>
        /// 测试专用：ghost 回弹请求次数。
        /// </summary>
        internal int GhostReboundRequestCount { get; private set; }

        /// <summary>
        /// 创建手牌视图并完成首帧刷新。
        /// </summary>
        public HandFanView(
            RectTransform handFan,
            RectTransform dropZone,
            RectTransform previewLayer,
            IHandContext context,
            GameObject cardTemplate,
            HandFanLayoutOptions options)
        {
            _handFan = handFan;
            _dropZone = dropZone;
            _previewLayer = previewLayer;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cardTemplate = cardTemplate;
            _options = options ?? new HandFanLayoutOptions();
            _callbacks = new DragCallbacks(this);
            _onHandChanged = _ => RefreshCards();
            _context.Hand.Changed += _onHandChanged;
            AttachPreviewDismissRelay(_handFan);
            AttachPreviewDismissRelay(_dropZone);
            AttachPreviewDismissRelay(_previewLayer);
            RefreshCards();
        }

        /// <summary>
        /// 立即清理拖拽 ghost。
        /// </summary>
        public void RequestGhostCleanup()
        {
            GhostCleanupRequestCount++;
            _dragController?.RequestGhostCleanup();
            _previewController?.ExitPreview();
        }

        /// <summary>
        /// 按手牌索引启动外部回弹。
        /// </summary>
        public void RequestGhostRebound(int handIdx)
        {
            GhostReboundRequestCount++;
            int visualIdx = FindVisualIndex(handIdx);
            if (visualIdx < 0)
            {
                Log.Warning($"[HandFanView] 未找到 handIdx={handIdx} 的卡牌，无法回弹。");
                _dragController?.RequestGhostCleanup();
                return;
            }

            _dragController?.BeginExternalRebound(visualIdx);
        }

        /// <summary>
        /// 释放卡牌项和上下文订阅。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _context.Hand.Changed -= _onHandChanged;
            _previewController?.Dispose();
            _previewController = null;
            _dragController?.Dispose();
            ClearCards();
            CardClicked = null;
            CardDroppedOnZone = null;
            CardDragCancelled = null;
        }

        private void RefreshCards()
        {
            if (_disposed)
            {
                return;
            }

            ClearCards();
            IReadOnlyList<CardRuntime> hand = _context.Hand.Value;
            if (_handFan == null || _cardTemplate == null || hand == null)
            {
                return;
            }

            for (int i = 0; i < hand.Count; i++)
            {
                GameObject itemObject = UguiViewUtil.InstantiateTemplate(_cardTemplate, _handFan, $"HandCard_{i}");
                var itemView = new CardItemView(itemObject, i, hand[i]);
                itemView.PointerDown += OnCardPointerDown;
                itemView.PointerMove += OnCardPointerMove;
                itemView.PointerUp += OnCardPointerUp;
                _cardItems.Add(itemView);
            }

            _dragSurface = new UguiDragSurface(_handFan, _dropZone, _previewLayer, _cardItems, _options, _callbacks);
            _dragController = new CardDragController(_dragSurface, _context, _options);
            _previewController?.Dispose();
            _previewController = new CardPreviewController(new UguiPreviewSurface(_handFan, _previewLayer, _cardItems), _options);
            _dragController.OnGeometryChanged();
        }

        private void ClearCards()
        {
            _dragController?.Dispose();
            _dragController = null;
            _previewController?.Dispose();
            _previewController = null;

            for (int i = _cardItems.Count - 1; i >= 0; i--)
            {
                CardItemView item = _cardItems[i];
                item.PointerDown -= OnCardPointerDown;
                item.PointerMove -= OnCardPointerMove;
                item.PointerUp -= OnCardPointerUp;
                GameObject root = item.Root;
                item.Dispose();
                UguiViewUtil.DestroyObject(root);
            }

            _cardItems.Clear();
        }

        private void OnCardPointerDown(CardItemView item, PointerEventData eventData)
        {
            int visualIdx = _cardItems.IndexOf(item);
            if (visualIdx < 0)
            {
                return;
            }

            _dragController?.OnPointerDown(item.HandIndex, visualIdx, eventData.pointerId, eventData.position);
        }

        private void OnCardPointerMove(CardItemView item, PointerEventData eventData)
        {
            CardInteractionState previousState = _dragController?.State ?? CardInteractionState.Idle;
            _dragController?.OnPointerMove(eventData.pointerId, eventData.position);
            ExitPreviewIfDragStarted(previousState);
        }

        private void OnCardPointerUp(CardItemView item, PointerEventData eventData)
        {
            _dragController?.OnPointerUp(eventData.pointerId, eventData.position);
        }

        private int FindVisualIndex(int handIdx)
        {
            for (int i = 0; i < _cardItems.Count; i++)
            {
                if (_cardItems[i].HandIndex == handIdx)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnCardClicked(CardItemView item)
        {
            _previewController?.TogglePreview(item.HandIndex, item);
            CardClicked?.Invoke(item.HandIndex);
        }

        internal bool TryDismissPreviewFromPointerTarget(GameObject target)
        {
            if (_previewController == null || !_previewController.IsPreviewing)
            {
                return false;
            }

            if (IsCardTarget(target))
            {
                return false;
            }

            _previewController.ExitPreview();
            return true;
        }

        internal void ExitPreviewIfDragStarted(CardInteractionState previousState)
        {
            if (previousState != CardInteractionState.Dragging &&
                _dragController != null &&
                _dragController.State == CardInteractionState.Dragging)
            {
                _previewController?.ExitPreview();
            }
        }

        private bool IsCardTarget(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            foreach (CardItemView item in _cardItems)
            {
                if (item != null && item.Root != null && target.transform.IsChildOf(item.Root.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void AttachPreviewDismissRelay(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            var relay = target.GetComponent<PreviewDismissRelay>() ?? target.gameObject.AddComponent<PreviewDismissRelay>();
            relay.Initialize(this);
        }

        private sealed class DragCallbacks : IDragHostCallbacks
        {
            private readonly HandFanView _owner;

            public DragCallbacks(HandFanView owner)
            {
                _owner = owner;
            }

            public void CardClicked(int handIdx)
            {
                CardItemView item = null;
                foreach (CardItemView candidate in _owner._cardItems)
                {
                    if (candidate.HandIndex == handIdx)
                    {
                        item = candidate;
                        break;
                    }
                }

                if (item != null)
                {
                    _owner.OnCardClicked(item);
                }
            }

            public void CardDroppedOnZone(int handIdx, bool needsManualTarget) =>
                _owner.CardDroppedOnZone?.Invoke(handIdx, needsManualTarget);

            public void CardDragCancelled(int handIdx) => _owner.CardDragCancelled?.Invoke(handIdx);
        }

        /// <summary>
        /// 挂在 UGUI 背景区域上的预览关闭桥。
        /// </summary>
        private sealed class PreviewDismissRelay : MonoBehaviour, IPointerDownHandler
        {
            private HandFanView _owner;

            public void Initialize(HandFanView owner)
            {
                _owner = owner;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                _owner?.TryDismissPreviewFromPointerTarget(eventData.pointerPressRaycast.gameObject);
            }
        }
    }
}
