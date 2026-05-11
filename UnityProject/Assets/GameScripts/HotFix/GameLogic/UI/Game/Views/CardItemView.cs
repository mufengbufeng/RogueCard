using System;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 单张手牌视图：从 CardItem.uxml CloneTree 出 .card-item 内层 VisualElement，
    /// 设置 card-name / card-cost 文本，注册 PointerDown / Enter / Leave 转发到上层 HandFanView。
    /// HandIndex 闭包语义：构造时传入，reorder 后保持不变（用于 UseCard）。
    /// </summary>
    public sealed class CardItemView : IDisposable
    {
        private VisualElement _root;
        private EventCallback<PointerDownEvent> _onPointerDown;
        private EventCallback<PointerEnterEvent> _onPointerEnter;
        private EventCallback<PointerLeaveEvent> _onPointerLeave;
        private bool _disposed;

        /// <summary>卡牌根 VisualElement（含 .card-item 类）。HandFanView 添加到 hand-fan 容器。</summary>
        public VisualElement Root => _root;

        /// <summary>卡牌在 ViewModel.Hand 中的索引（构造时捕获，reorder 后不变）。</summary>
        public int HandIndex { get; }

        /// <summary>本卡的 CardRuntime 数据快照，供上层查 TargetMode 等配置。</summary>
        public CardRuntime Card { get; }

        /// <summary>PointerDown 事件，参数 (sender, evt)。</summary>
        public event Action<CardItemView, PointerDownEvent> PointerDown;

        /// <summary>PointerEnter 事件，参数 sender。</summary>
        public event Action<CardItemView> PointerEnter;

        /// <summary>PointerLeave 事件，参数 sender。</summary>
        public event Action<CardItemView> PointerLeave;

        /// <summary>构造单卡视图。</summary>
        /// <param name="clonedRoot">已 CloneTree 的根元素（带 .card-item 类的内层节点）。</param>
        /// <param name="handIndex">在 ViewModel.Hand 中的索引（闭包捕获）。</param>
        /// <param name="card">卡牌运行时数据。</param>
        public CardItemView(VisualElement clonedRoot, int handIndex, CardRuntime card)
        {
            _root = clonedRoot ?? throw new ArgumentNullException(nameof(clonedRoot));
            HandIndex = handIndex;
            Card = card;

            // 设置文本
            var nameLabel = _root.Q<Label>("card-name");
            if (nameLabel != null && card?.Config != null) nameLabel.text = card.Config.Name;

            var costLabel = _root.Q<Label>("card-cost");
            if (costLabel != null && card?.Config != null) costLabel.text = card.Config.Cost.ToString();

            // 注册事件转发（缓存委托引用便于对称解绑）
            _onPointerDown = evt => PointerDown?.Invoke(this, evt);
            _onPointerEnter = _ => PointerEnter?.Invoke(this);
            _onPointerLeave = _ => PointerLeave?.Invoke(this);

            _root.RegisterCallback(_onPointerDown);
            _root.RegisterCallback(_onPointerEnter);
            _root.RegisterCallback(_onPointerLeave);
        }

        /// <summary>切换 card-item--hovering CSS 类（仅 Idle 态下由 HandFanView 调用）。</summary>
        public void SetHovering(bool hovering)
        {
            if (_root == null) return;
            if (hovering) _root.AddToClassList("card-item--hovering");
            else _root.RemoveFromClassList("card-item--hovering");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_root != null)
            {
                if (_onPointerDown != null) _root.UnregisterCallback(_onPointerDown);
                if (_onPointerEnter != null) _root.UnregisterCallback(_onPointerEnter);
                if (_onPointerLeave != null) _root.UnregisterCallback(_onPointerLeave);
            }

            _onPointerDown = null;
            _onPointerEnter = null;
            _onPointerLeave = null;
            PointerDown = null;
            PointerEnter = null;
            PointerLeave = null;
            _root = null;
        }
    }
}
