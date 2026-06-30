using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 单张手牌视图，负责文本渲染和指针事件转发。
    /// </summary>
    public sealed class CardItemView : IDisposable
    {
        private readonly CardPointerRelay _pointerRelay;
        private readonly CanvasGroup _canvasGroup;
        private readonly Graphic _rootGraphic;
        private bool _disposed;

        /// <summary>
        /// 卡牌根对象。
        /// </summary>
        public GameObject Root { get; }

        /// <summary>
        /// 卡牌 RectTransform。
        /// </summary>
        public RectTransform RectTransform { get; }

        /// <summary>
        /// 构造时捕获的手牌索引，拖拽重排后不变。
        /// </summary>
        public int HandIndex { get; }

        /// <summary>
        /// 卡牌运行时数据。
        /// </summary>
        public CardRuntime Card { get; }

        /// <summary>
        /// 指针按下事件。
        /// </summary>
        public event Action<CardItemView, PointerEventData> PointerDown;

        /// <summary>
        /// 指针拖动事件。
        /// </summary>
        public event Action<CardItemView, PointerEventData> PointerMove;

        /// <summary>
        /// 指针抬起事件。
        /// </summary>
        public event Action<CardItemView, PointerEventData> PointerUp;

        /// <summary>
        /// 指针移入事件。
        /// </summary>
        public event Action<CardItemView> PointerEnter;

        /// <summary>
        /// 指针移出事件。
        /// </summary>
        public event Action<CardItemView> PointerExit;

        /// <summary>
        /// 创建卡牌视图并刷新文本。
        /// </summary>
        public CardItemView(GameObject root, int handIndex, CardRuntime card)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            RectTransform = root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>();
            HandIndex = handIndex;
            Card = card;
            _canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            _rootGraphic = root.GetComponent<Graphic>();
            _pointerRelay = root.GetComponent<CardPointerRelay>() ?? root.AddComponent<CardPointerRelay>();
            _pointerRelay.Initialize(this);

            UguiViewUtil.SetText(UguiViewUtil.FindText(root, "CardNameText") ?? UguiViewUtil.FindText(root, "NameText"), card?.Config?.Name ?? string.Empty);
            UguiViewUtil.SetText(UguiViewUtil.FindText(root, "CardCostText") ?? UguiViewUtil.FindText(root, "CostText"), card?.Config != null ? card.Config.Cost.ToString() : string.Empty);
        }

        /// <summary>
        /// 设置透明度。
        /// </summary>
        public void SetOpacity(float opacity)
        {
            _canvasGroup.alpha = opacity;
        }

        /// <summary>
        /// 设置是否接收射线。
        /// </summary>
        public void SetPicking(bool pickable)
        {
            _canvasGroup.blocksRaycasts = pickable;
            foreach (Graphic graphic in Root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = pickable;
            }
        }

        /// <summary>
        /// 设置 hover 视觉，预览前统一清掉。
        /// </summary>
        public void SetHover(bool hovered)
        {
            if (_rootGraphic == null)
            {
                return;
            }

            Color color = _rootGraphic.color;
            color.a = hovered ? 0.92f : 1f;
            _rootGraphic.color = color;
        }

        /// <summary>
        /// 释放事件。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_pointerRelay != null)
            {
                _pointerRelay.Initialize(null);
            }

            PointerDown = null;
            PointerMove = null;
            PointerUp = null;
            PointerEnter = null;
            PointerExit = null;
        }

        internal void NotifyPointerDown(PointerEventData eventData) => PointerDown?.Invoke(this, eventData);
        internal void NotifyPointerMove(PointerEventData eventData) => PointerMove?.Invoke(this, eventData);
        internal void NotifyPointerUp(PointerEventData eventData) => PointerUp?.Invoke(this, eventData);
        internal void NotifyPointerEnter()
        {
            SetHover(true);
            PointerEnter?.Invoke(this);
        }

        internal void NotifyPointerExit()
        {
            SetHover(false);
            PointerExit?.Invoke(this);
        }
    }

    /// <summary>
    /// UGUI 指针事件桥，挂在卡牌项根对象上。
    /// </summary>
    public sealed class CardPointerRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private CardItemView _owner;

        /// <summary>
        /// 初始化所属卡牌项。
        /// </summary>
        public void Initialize(CardItemView owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 处理指针按下并转发给所属卡牌视图。
        /// </summary>
        public void OnPointerDown(PointerEventData eventData) => _owner?.NotifyPointerDown(eventData);

        /// <summary>
        /// 处理指针拖动并转发给所属卡牌视图。
        /// </summary>
        public void OnDrag(PointerEventData eventData) => _owner?.NotifyPointerMove(eventData);

        /// <summary>
        /// 处理指针抬起并转发给所属卡牌视图。
        /// </summary>
        public void OnPointerUp(PointerEventData eventData) => _owner?.NotifyPointerUp(eventData);

        /// <summary>
        /// 处理指针移入并通知所属卡牌视图。
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData) => _owner?.NotifyPointerEnter();

        /// <summary>
        /// 处理指针移出并通知所属卡牌视图。
        /// </summary>
        public void OnPointerExit(PointerEventData eventData) => _owner?.NotifyPointerExit();
    }
}
