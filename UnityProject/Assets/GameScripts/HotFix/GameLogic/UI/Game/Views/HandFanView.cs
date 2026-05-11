using System;
using System.Collections.Generic;
using EF.Debugger;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 手牌容器视图。装配 CardItemView × N + CardDragController + CardPreviewController，
    /// 订阅 IHandContext.Hand.Changed 全量重建，对外暴露三个事件供上层（GameView / BattlePanelView）订阅：
    /// CardClicked / CardDroppedOnZone(needsManualTarget) / CardDragCancelled。
    /// 内部实现 IDragSurface + IPreviewSurface 把 _handFan / _dropZone / _previewLayer / _cardItems 操作集中。
    /// </summary>
    public sealed class HandFanView : IDisposable, IHandGhostSurface
    {
        private VisualElement _handFan;
        private VisualElement _dropZone;
        private VisualElement _previewLayer;
        private VisualElement _previewDismissRoot;
        private IHandContext _context;
        private VisualTreeAsset _cardItemTemplate;
        private HandFanLayoutOptions _options;

        private readonly List<CardItemView> _cardItems = new();

        // 子控制器
        private CardDragController _dragController;
        private CardPreviewController _previewController;

        // 内部 surface 实现
        private DragSurfaceImpl _dragSurface;
        private PreviewSurfaceImpl _previewSurface;
        private DragHostCallbacksImpl _callbacks;

        // Ghost / 占位卡（surface 实现持有）
        private VisualElement _dragGhost;
        private VisualElement _insertSlotElement;

        // 订阅引用（解绑用）
        private Action<IReadOnlyList<CardRuntime>> _onHandChanged;
        private EventCallback<GeometryChangedEvent> _onGeometryChanged;
        private EventCallback<PointerDownEvent> _onPreviewDismissPointerDown;

        // 当前正在按下并接管 Move/Up/CaptureOut 事件的卡（注册在其 Root 上）
        private CardItemView _capturedView;

        // hover 跟踪
        private CardItemView _hoveringView;

        private bool _disposed;

        // ── 公开事件 ──

        /// <summary>单击某卡时触发（位移 ≤ DragThreshold），参数 handIdx。</summary>
        public event Action<int> CardClicked;

        /// <summary>拖到 drop-zone 内松手时触发，参数 (handIdx, needsManualTarget)。</summary>
        public event Action<int, bool> CardDroppedOnZone;

        /// <summary>中间地带松手 / PointerCaptureOut 触发，参数 handIdx。</summary>
        public event Action<int> CardDragCancelled;

        /// <summary>构造 HandFanView。</summary>
        public HandFanView(
            VisualElement handFan,
            VisualElement dropZone,
            VisualElement previewLayer,
            VisualElement previewDismissRoot,
            IHandContext context,
            VisualTreeAsset cardItemTemplate,
            HandFanLayoutOptions options)
        {
            _handFan = handFan;
            _dropZone = dropZone;
            _previewLayer = previewLayer;
            _previewDismissRoot = previewDismissRoot;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cardItemTemplate = cardItemTemplate;
            _options = options ?? new HandFanLayoutOptions();

            _callbacks = new DragHostCallbacksImpl(this);
            _dragSurface = new DragSurfaceImpl(this);
            _previewSurface = new PreviewSurfaceImpl(this);

            _dragController = new CardDragController(_dragSurface, _context, _options);
            _previewController = new CardPreviewController(_previewSurface, _options);

            // 订阅 Hand 变化
            _onHandChanged = _ => RefreshCards();
            _context.Hand.Changed += _onHandChanged;

            // 注册 hand-fan 几何变化
            if (_handFan != null)
            {
                _onGeometryChanged = _ => _dragController?.OnGeometryChanged();
                _handFan.RegisterCallback(_onGeometryChanged);
            }

            if (_previewDismissRoot != null)
            {
                _onPreviewDismissPointerDown = OnPreviewDismissPointerDown;
                _previewDismissRoot.RegisterCallback(_onPreviewDismissPointerDown, TrickleDown.TrickleDown);
            }

            // 首次同步
            RefreshCards();
        }

        // ── 上层 API ──

        /// <summary>
        /// 立即销毁 ghost（不触发回弹动画）；卡牌 visual state 由后续 Hand.Changed 触发 RefreshCards 自然重建。
        /// 供 TargetSelector 确认怪物点击后调用。幂等。
        /// </summary>
        public void RequestGhostCleanup()
        {
            if (_disposed) return;
            _dragController?.RequestGhostCleanup();
        }

        /// <summary>
        /// 启动协同回弹动画：ghost 立即销毁、被拖卡 opacity 立即恢复、其他卡 transition 0.15s 回到 N 张布局，
        /// options.ReboundDurationMs 后内部状态归 Idle。
        /// 供 TargetSelector 取消选目标时调用，handIdx 用于在 _cardItems 中查找视觉索引（reorder 后仍可定位）。
        /// 非预期状态（无对应卡 / 已 disposed）通过 Log.Warning 记录并幂等返回。
        /// </summary>
        public void RequestGhostRebound(int handIdx)
        {
            if (_disposed) return;

            int visualIdx = -1;
            for (int i = 0; i < _cardItems.Count; i++)
            {
                if (_cardItems[i].HandIndex == handIdx) { visualIdx = i; break; }
            }
            if (visualIdx < 0)
            {
                Log.Warning($"[HandFanView] RequestGhostRebound: 未找到 handIdx={handIdx} 对应的视觉索引，简化为 DestroyGhost");
                _dragController?.RequestGhostCleanup();
                return;
            }

            _dragController?.BeginExternalRebound(visualIdx);
        }

        /// <summary>
        /// 尝试按 pointer target 关闭当前预览。返回 true 表示调用方应消费该 pointer 事件。
        /// 卡牌自身及其子节点不在此处关闭，继续交给 TogglePreview 处理同卡关闭 / 别卡切换。
        /// </summary>
        private bool TryDismissPreviewFromPointerTarget(VisualElement target)
        {
            if (_disposed || _previewController == null || !_previewController.IsPreviewing) return false;
            if (target != null && IsAnyCardOrDescendant(target)) return false;

            _previewController.ExitPreview();
            return true;
        }

        // ── 内部刷新 ──

        /// <summary>全量重建 _cardItems：先 Dispose 旧项 + 强制退出残留交互态，再按 Hand 列表新建。</summary>
        private void RefreshCards()
        {
            if (_disposed) return;

            // 强制清掉残留交互态：拖拽/预览中如果手牌变化，立即退出
            // （Dispose 会触发 ExitDragging 等清理）
            if (_dragController != null && _dragController.State == CardInteractionState.Dragging)
            {
                _dragController.OnPointerCaptureOut(-1); // 用一个 sentinel pointerId 强制重置
                // 由于 _capturedPointerId 可能不是 -1，这里直接走 reset 路径
            }
            _previewController?.ExitPreview();

            // 销毁旧卡视图
            foreach (var view in _cardItems)
            {
                view.Root?.RemoveFromHierarchy();
                view.Dispose();
            }
            _cardItems.Clear();

            if (_handFan == null || _cardItemTemplate == null) return;
            var hand = _context?.Hand?.Value;
            if (hand == null) return;

            for (int i = 0; i < hand.Count; i++)
            {
                var template = _cardItemTemplate.CloneTree();
                var root = template.Q(className: "card-item");
                if (root == null) continue;
                root.RemoveFromHierarchy();

                var view = new CardItemView(root, handIndex: i, card: hand[i]);
                view.PointerDown += OnCardPointerDown;
                view.PointerEnter += OnCardPointerEnter;
                view.PointerLeave += OnCardPointerLeave;

                _handFan.Add(root);
                _cardItems.Add(view);
            }

            // 按 N 张紧凑布局排列 + sibling 同步
            ApplyInitialFanLayout();
            SyncSiblingOrderInternal();
        }

        /// <summary>构造完毕后按 N 张紧凑布局应用初始 transform（无 active）。</summary>
        private void ApplyInitialFanLayout()
        {
            int n = _cardItems.Count;
            if (n == 0) return;
            float fanW = _dragSurface.HandFanWidth;
            float fanH = _dragSurface.HandFanHeight;
            for (int i = 0; i < n; i++)
            {
                var slot = FanLayoutCalc.ComputeSlot(i, n, fanW, fanH, _options);
                _dragSurface.ApplyFanTransform(i, slot);
            }
        }

        /// <summary>按 _cardItems 列表顺序同步 hand-fan sibling，占位卡始终在最上。</summary>
        private void SyncSiblingOrderInternal()
        {
            foreach (var view in _cardItems)
            {
                view.Root?.BringToFront();
            }
            _insertSlotElement?.BringToFront();
        }

        // ── CardItemView 事件 ──

        private void OnCardPointerDown(CardItemView view, PointerDownEvent evt)
        {
            int visualIdx = _cardItems.IndexOf(view);
            if (visualIdx < 0) return;

            // 注册 Move/Up/CaptureOut（直接在被按下的卡上）
            view.Root.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
            view.Root.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
            view.Root.RegisterCallback<PointerCaptureOutEvent>(OnCardPointerCaptureOut);
            _capturedView = view;

            _dragController.OnPointerDown(view.HandIndex, visualIdx, evt.pointerId, evt.position);
            evt.StopPropagation();
        }

        private void OnCardPointerMove(PointerMoveEvent evt)
        {
            _dragController?.OnPointerMove(evt.pointerId, evt.position);
            evt.StopPropagation();
        }

        private void OnCardPointerUp(PointerUpEvent evt)
        {
            // 单击且未拖拽时上层 CardClicked 回调里需触发预览 toggle
            int handIdx = _dragController?.ActiveHandIndex ?? -1;
            int visualIdx = _dragController?.ActiveVisualIndex ?? -1;
            CardItemView clicked = (visualIdx >= 0 && visualIdx < _cardItems.Count) ? _cardItems[visualIdx] : null;
            // 缓存 clicked.Root 引用（供 CardClicked 回调中触发预览）
            _pendingClickSource = clicked?.Root;

            _dragController?.OnPointerUp(evt.pointerId, evt.position);
            UnregisterCardPointerCallbacks(_capturedView);
            _capturedView = null;
            _pendingClickSource = null;
            evt.StopPropagation();
        }

        private void OnCardPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _dragController?.OnPointerCaptureOut(evt.pointerId);
            UnregisterCardPointerCallbacks(_capturedView);
            _capturedView = null;
        }

        private void UnregisterCardPointerCallbacks(CardItemView view)
        {
            if (view?.Root == null) return;
            view.Root.UnregisterCallback<PointerMoveEvent>(OnCardPointerMove);
            view.Root.UnregisterCallback<PointerUpEvent>(OnCardPointerUp);
            view.Root.UnregisterCallback<PointerCaptureOutEvent>(OnCardPointerCaptureOut);
        }

        // 用于 CardClicked 回调内访问被点击卡的 source（preview 锚点计算需要）
        private VisualElement _pendingClickSource;

        private void OnPreviewDismissPointerDown(PointerDownEvent evt)
        {
            if (!(evt.target is VisualElement target)) return;
            if (!TryDismissPreviewFromPointerTarget(target)) return;
            evt.StopPropagation();
        }

        private bool IsAnyCardOrDescendant(VisualElement target)
        {
            foreach (var view in _cardItems)
            {
                if (IsSameOrAncestor(view.Root, target)) return true;
            }
            return false;
        }

        private static bool IsSameOrAncestor(VisualElement ancestor, VisualElement target)
        {
            if (ancestor == null || target == null) return false;
            var current = target;
            while (current != null)
            {
                if (current == ancestor) return true;
                current = current.parent;
            }
            return false;
        }

        private void OnCardPointerEnter(CardItemView view)
        {
            if (_dragController == null) return;
            // 仅 Idle 态响应 hover
            if (_dragController.State != CardInteractionState.Idle) return;
            view.SetHovering(true);
            _hoveringView = view;
        }

        private void OnCardPointerLeave(CardItemView view)
        {
            view.SetHovering(false);
            if (_hoveringView == view) _hoveringView = null;
        }

        // ── Ghost / 占位卡 实现细节 ──

        private VisualElement CreateGhostInternal(int sourceCardIdx, Vector2 pos)
        {
            if (_previewLayer == null) return null;
            if (sourceCardIdx < 0 || sourceCardIdx >= _cardItems.Count) return null;
            var sourceRoot = _cardItems[sourceCardIdx].Root;

            var ghost = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            ghost.AddToClassList("card-ghost");

            var srcName = sourceRoot?.Q<Label>("card-name");
            if (srcName != null)
            {
                var ghostName = new Label(srcName.text);
                ghostName.AddToClassList("card-name");
                ghost.Add(ghostName);
            }

            var srcCost = sourceRoot?.Q<Label>("card-cost");
            if (srcCost != null)
            {
                var ghostCost = new Label(srcCost.text);
                ghostCost.AddToClassList("card-cost");
                ghost.Add(ghostCost);
            }

            ghost.style.left = pos.x - _options.CardWidth / 2f;
            ghost.style.top = pos.y - _options.CardHeight / 2f;

            _previewLayer.Add(ghost);
            _dragGhost = ghost;
            return ghost;
        }

        private void DestroyGhostInternal()
        {
            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;
        }

        private VisualElement CreateInsertSlotInternal(int sourceCardIdx)
        {
            if (_handFan == null || _cardItemTemplate == null) return null;
            if (sourceCardIdx < 0 || sourceCardIdx >= _cardItems.Count) return null;

            var template = _cardItemTemplate.CloneTree();
            var slot = template.Q(className: "card-item");
            if (slot == null) return null;
            slot.RemoveFromHierarchy();
            slot.AddToClassList("card-item--insert-slot");
            // 占位卡 transitionDuration 0：拖拽中位移立即生效
            SetInlineTransitionDuration(slot, 0f);
            slot.pickingMode = PickingMode.Ignore;

            // 复制源卡文本
            var sourceRoot = _cardItems[sourceCardIdx].Root;
            var srcName = sourceRoot?.Q<Label>("card-name");
            var slotName = slot.Q<Label>("card-name");
            if (srcName != null && slotName != null) slotName.text = srcName.text;
            var srcCost = sourceRoot?.Q<Label>("card-cost");
            var slotCost = slot.Q<Label>("card-cost");
            if (srcCost != null && slotCost != null) slotCost.text = srcCost.text;

            _handFan.Add(slot);
            _insertSlotElement = slot;
            return slot;
        }

        private void DestroyInsertSlotInternal()
        {
            _insertSlotElement?.RemoveFromHierarchy();
            _insertSlotElement = null;
        }

        private static void SetInlineTransitionDuration(VisualElement el, float seconds)
        {
            var list = new List<TimeValue> { new TimeValue(seconds, TimeUnit.Second) };
            el.style.transitionDuration = new StyleList<TimeValue>(list);
        }

        private static void ClearInlineTransitionDuration(VisualElement el)
        {
            el.style.transitionDuration = new StyleList<TimeValue>(StyleKeyword.Null);
        }

        private static float ResolveSize(VisualElement element, bool width, float fallback)
        {
            if (element == null) return fallback;
            float resolved = width ? element.resolvedStyle.width : element.resolvedStyle.height;
            if (resolved > 0) return resolved;
            float layoutVal = width ? element.layout.width : element.layout.height;
            if (layoutVal > 0) return layoutVal;
            return fallback;
        }

        // ── Dispose ──

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_context != null && _onHandChanged != null)
            {
                _context.Hand.Changed -= _onHandChanged;
            }
            _onHandChanged = null;

            if (_handFan != null && _onGeometryChanged != null)
            {
                _handFan.UnregisterCallback(_onGeometryChanged);
            }
            _onGeometryChanged = null;

            if (_previewDismissRoot != null && _onPreviewDismissPointerDown != null)
            {
                _previewDismissRoot.UnregisterCallback(_onPreviewDismissPointerDown, TrickleDown.TrickleDown);
            }
            _onPreviewDismissPointerDown = null;

            if (_capturedView != null) UnregisterCardPointerCallbacks(_capturedView);
            _capturedView = null;

            _dragController?.Dispose();
            _dragController = null;
            _previewController?.Dispose();
            _previewController = null;

            DestroyGhostInternal();
            DestroyInsertSlotInternal();

            foreach (var view in _cardItems)
            {
                view.Root?.RemoveFromHierarchy();
                view.Dispose();
            }
            _cardItems.Clear();

            CardClicked = null;
            CardDroppedOnZone = null;
            CardDragCancelled = null;
            _context = null;
            _handFan = null;
            _dropZone = null;
            _previewLayer = null;
            _previewDismissRoot = null;
            _cardItemTemplate = null;
        }

        // ── 内部 IDragSurface 实现 ──

        private sealed class DragSurfaceImpl : IDragSurface
        {
            private readonly HandFanView _owner;
            public DragSurfaceImpl(HandFanView owner) { _owner = owner; }

            public IDragHostCallbacks Callbacks => _owner._callbacks;
            public int CardCount => _owner._cardItems.Count;
            public Rect DropZoneWorldBound => _owner._dropZone?.worldBound ?? Rect.zero;
            public Rect HandFanWorldBound => _owner._handFan?.worldBound ?? Rect.zero;
            public bool DropZoneAvailable => _owner._dropZone != null;
            public float HandFanWidth => ResolveSize(_owner._handFan, true, 800f);
            public float HandFanHeight => ResolveSize(_owner._handFan, false, 280f);

            public Rect GetCardWorldBound(int cardIdx)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return Rect.zero;
                return _owner._cardItems[cardIdx].Root?.worldBound ?? Rect.zero;
            }

            public void ApplyFanTransform(int cardIdx, FanSlotAssignment slot)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                var card = _owner._cardItems[cardIdx].Root;
                if (card == null) return;
                card.style.left = slot.Left;
                card.style.top = slot.Top;
                card.style.translate = new StyleTranslate(new Translate(0, slot.TranslateY, 0));
                card.style.rotate = new StyleRotate(new Rotate(new Angle(slot.RotateDegrees, AngleUnit.Degree)));
            }

            public void SetCardOpacity(int cardIdx, float opacity)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                _owner._cardItems[cardIdx].Root.style.opacity = opacity;
            }

            public void ResetCardOpacity(int cardIdx)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                _owner._cardItems[cardIdx].Root.style.opacity = StyleKeyword.Null;
            }

            public void SetCardPickingMode(int cardIdx, bool pickable)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                _owner._cardItems[cardIdx].Root.pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore;
            }

            public void SetCardTransitionDuration(int cardIdx, float seconds)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                SetInlineTransitionDuration(_owner._cardItems[cardIdx].Root, seconds);
            }

            public void ClearCardTransitionDuration(int cardIdx)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                ClearInlineTransitionDuration(_owner._cardItems[cardIdx].Root);
            }

            public void ReorderCardItem(int from, int to)
            {
                int n = _owner._cardItems.Count;
                if (from < 0 || from >= n) return;
                if (to < 0) to = 0;
                if (to >= n) to = n - 1;
                if (from == to) return;

                var dragged = _owner._cardItems[from];
                _owner._cardItems.RemoveAt(from);
                _owner._cardItems.Insert(to, dragged);

                _owner.SyncSiblingOrderInternal();
            }

            public void SyncSiblingOrder() => _owner.SyncSiblingOrderInternal();

            public void SetDropZoneActive(bool active)
            {
                if (_owner._dropZone == null) return;
                if (active) _owner._dropZone.AddToClassList("active");
                else _owner._dropZone.RemoveFromClassList("active");
            }

            public void CreateGhost(int sourceCardIdx, Vector2 pos) => _owner.CreateGhostInternal(sourceCardIdx, pos);

            public void UpdateGhostPosition(Vector2 pos)
            {
                if (_owner._dragGhost == null) return;
                _owner._dragGhost.style.left = pos.x - _owner._options.CardWidth / 2f;
                _owner._dragGhost.style.top = pos.y - _owner._options.CardHeight / 2f;
            }

            public void DestroyGhost() => _owner.DestroyGhostInternal();
            public void CreateInsertSlot(int sourceCardIdx) => _owner.CreateInsertSlotInternal(sourceCardIdx);
            public void DestroyInsertSlot() => _owner.DestroyInsertSlotInternal();

            public void ApplyInsertSlotTransform(FanSlotAssignment slot)
            {
                var el = _owner._insertSlotElement;
                if (el == null) return;
                el.style.left = slot.Left;
                el.style.top = slot.Top;
                el.style.translate = new StyleTranslate(new Translate(0, slot.TranslateY, 0));
                el.style.rotate = new StyleRotate(new Rotate(new Angle(slot.RotateDegrees, AngleUnit.Degree)));
            }

            public void Schedule(Action action, long delayMs)
            {
                if (_owner._handFan == null) return;
                _owner._handFan.schedule.Execute(action).StartingIn(delayMs);
            }

            public void CapturePointer(int cardIdx, int pointerId)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                var root = _owner._cardItems[cardIdx].Root;
                root?.CapturePointer(pointerId);
            }

            public void ReleasePointer(int cardIdx, int pointerId)
            {
                if (cardIdx < 0 || cardIdx >= _owner._cardItems.Count) return;
                var root = _owner._cardItems[cardIdx].Root;
                if (root != null && root.HasPointerCapture(pointerId)) root.ReleasePointer(pointerId);
            }
        }

        // ── 内部 IPreviewSurface 实现 ──

        private sealed class PreviewSurfaceImpl : IPreviewSurface
        {
            private readonly HandFanView _owner;
            public PreviewSurfaceImpl(HandFanView owner) { _owner = owner; }

            public VisualElement ClonePreviewElement(VisualElement source)
            {
                if (_owner._cardItemTemplate == null) return null;
                var template = _owner._cardItemTemplate.CloneTree();
                var clone = template.Q(className: "card-item");
                if (clone == null) return null;
                clone.RemoveFromHierarchy();

                // 复制源卡文本
                var srcName = source?.Q<Label>("card-name");
                var cloneName = clone.Q<Label>("card-name");
                if (srcName != null && cloneName != null) cloneName.text = srcName.text;
                var srcCost = source?.Q<Label>("card-cost");
                var cloneCost = clone.Q<Label>("card-cost");
                if (srcCost != null && cloneCost != null) cloneCost.text = srcCost.text;

                return clone;
            }

            public void AddToPreviewLayer(VisualElement element)
            {
                _owner._previewLayer?.Add(element);
            }

            public void RemoveFromPreviewLayer(VisualElement element)
            {
                element?.RemoveFromHierarchy();
            }

            public Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan)
            {
                if (_owner._handFan == null || _owner._previewLayer == null) return sourceTopCenterInHandFan;
                var world = _owner._handFan.LocalToWorld(sourceTopCenterInHandFan);
                return _owner._previewLayer.WorldToLocal(world);
            }

            public void ClearAllHoverState()
            {
                foreach (var view in _owner._cardItems) view.SetHovering(false);
            }
        }

        // ── 内部 IDragHostCallbacks 实现：转发到上层 event 与预览触发 ──

        private sealed class DragHostCallbacksImpl : IDragHostCallbacks
        {
            private readonly HandFanView _owner;
            public DragHostCallbacksImpl(HandFanView owner) { _owner = owner; }

            public void CardClicked(int handIdx)
            {
                // 找到对应 source 元素并触发预览 toggle（保留单击预览这条公开行为）
                VisualElement source = null;
                foreach (var view in _owner._cardItems)
                {
                    if (view.HandIndex == handIdx) { source = view.Root; break; }
                }
                if (source != null)
                {
                    _owner._previewController?.TogglePreview(handIdx, source);
                }
                _owner.CardClicked?.Invoke(handIdx);
            }

            public void CardDroppedOnZone(int handIdx, bool needsManualTarget) =>
                _owner.CardDroppedOnZone?.Invoke(handIdx, needsManualTarget);

            public void CardDragCancelled(int handIdx) =>
                _owner.CardDragCancelled?.Invoke(handIdx);
        }
    }
}
