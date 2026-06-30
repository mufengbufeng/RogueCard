using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// CardDragController 的 UGUI 生产适配层，封装 RectTransform 命中、ghost、占位卡和视觉状态。
    /// </summary>
    public sealed class UguiDragSurface : IDragSurface
    {
        private readonly RectTransform _handFan;
        private readonly RectTransform _dropZone;
        private readonly RectTransform _previewLayer;
        private readonly List<CardItemView> _cardItems;
        private readonly HandFanLayoutOptions _options;
        private readonly IDragHostCallbacks _callbacks;
        private readonly Action<Action, long> _scheduleAction;
        private readonly Canvas _canvas;

        private GameObject _ghost;
        private GameObject _insertSlot;

        /// <summary>
        /// 创建 UGUI 拖拽适配层。
        /// </summary>
        public UguiDragSurface(
            RectTransform handFan,
            RectTransform dropZone,
            RectTransform previewLayer,
            List<CardItemView> cardItems,
            HandFanLayoutOptions options,
            IDragHostCallbacks callbacks)
            : this(handFan, dropZone, previewLayer, cardItems, options, callbacks, ScheduleAsync)
        {
        }

        /// <summary>
        /// 创建 UGUI 拖拽适配层，并允许测试替换延迟调度。
        /// </summary>
        internal UguiDragSurface(
            RectTransform handFan,
            RectTransform dropZone,
            RectTransform previewLayer,
            List<CardItemView> cardItems,
            HandFanLayoutOptions options,
            IDragHostCallbacks callbacks,
            Action<Action, long> scheduleAction)
        {
            _handFan = handFan;
            _dropZone = dropZone;
            _previewLayer = previewLayer;
            _cardItems = cardItems ?? throw new ArgumentNullException(nameof(cardItems));
            _options = options ?? new HandFanLayoutOptions();
            _callbacks = callbacks;
            _scheduleAction = scheduleAction ?? ScheduleAsync;
            _canvas = _dropZone != null ? _dropZone.GetComponentInParent<Canvas>() : null;
        }

        /// <summary>
        /// 当前手牌列表中的卡牌视图数量。
        /// </summary>
        public int CardCount => _cardItems.Count;

        /// <summary>
        /// 出牌区域在屏幕坐标中的命中矩形。
        /// </summary>
        public Rect DropZoneWorldBound => ToScreenRect(_dropZone);

        /// <summary>
        /// 手牌扇形容器在屏幕坐标中的命中矩形。
        /// </summary>
        public Rect HandFanWorldBound => ToScreenRect(_handFan);

        /// <summary>
        /// 出牌区域是否存在且处于激活层级中。
        /// </summary>
        public bool DropZoneAvailable => _dropZone != null && _dropZone.gameObject.activeInHierarchy;

        /// <summary>
        /// 手牌扇形容器宽度，未就绪时使用默认宽度。
        /// </summary>
        public float HandFanWidth => _handFan != null && _handFan.rect.width > 0f ? _handFan.rect.width : 800f;

        /// <summary>
        /// 手牌扇形容器高度，未就绪时使用默认高度。
        /// </summary>
        public float HandFanHeight => _handFan != null && _handFan.rect.height > 0f ? _handFan.rect.height : 280f;

        /// <summary>
        /// 拖拽控制器向上层 UI 派发事件的回调桥。
        /// </summary>
        public IDragHostCallbacks Callbacks => _callbacks;

        /// <summary>
        /// 取得指定卡牌视图在屏幕坐标中的命中矩形。
        /// </summary>
        public Rect GetCardWorldBound(int cardIdx)
        {
            return cardIdx >= 0 && cardIdx < _cardItems.Count ? ToScreenRect(_cardItems[cardIdx].RectTransform) : Rect.zero;
        }

        /// <summary>
        /// 将扇形布局槽位应用到指定卡牌的锚点、位置、旋转和尺寸。
        /// </summary>
        public void ApplyFanTransform(int cardIdx, FanSlotAssignment slot)
        {
            if (!TryGetCard(cardIdx, out CardItemView card))
            {
                return;
            }

            card.RectTransform.anchorMin = new Vector2(0f, 1f);
            card.RectTransform.anchorMax = new Vector2(0f, 1f);
            card.RectTransform.pivot = new Vector2(0f, 1f);
            card.RectTransform.anchoredPosition = new Vector2(slot.Left, -slot.Top - slot.TranslateY);
            card.RectTransform.localRotation = Quaternion.Euler(0f, 0f, slot.RotateDegrees);
            card.RectTransform.sizeDelta = new Vector2(_options.CardWidth, _options.CardHeight);
        }

        /// <summary>
        /// 设置指定卡牌视图的透明度。
        /// </summary>
        public void SetCardOpacity(int cardIdx, float opacity)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.SetOpacity(opacity);
            }
        }

        /// <summary>
        /// 将指定卡牌视图的透明度恢复为完全不透明。
        /// </summary>
        public void ResetCardOpacity(int cardIdx) => SetCardOpacity(cardIdx, 1f);

        /// <summary>
        /// 设置指定卡牌视图是否接收指针射线。
        /// </summary>
        public void SetCardPickingMode(int cardIdx, bool pickable)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.SetPicking(pickable);
            }
        }

        /// <summary>
        /// 在卡牌根对象名称中记录过渡时长标记。
        /// </summary>
        public void SetCardTransitionDuration(int cardIdx, float seconds)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.Root.name = $"{card.Root.name.Split('#')[0]}#transition={seconds:0.###}";
            }
        }

        /// <summary>
        /// 清除卡牌根对象名称中的过渡时长标记。
        /// </summary>
        public void ClearCardTransitionDuration(int cardIdx)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.Root.name = card.Root.name.Split('#')[0];
            }
        }

        /// <summary>
        /// 调整手牌视图列表顺序，并同步 Transform sibling 顺序。
        /// </summary>
        public void ReorderCardItem(int from, int to)
        {
            int count = _cardItems.Count;
            if (from < 0 || from >= count)
            {
                return;
            }

            to = Mathf.Clamp(to, 0, Mathf.Max(0, count - 1));
            CardItemView item = _cardItems[from];
            _cardItems.RemoveAt(from);
            _cardItems.Insert(to, item);
            SyncSiblingOrder();
        }

        /// <summary>
        /// 按手牌视图列表顺序刷新卡牌在手牌容器中的 sibling 索引。
        /// </summary>
        public void SyncSiblingOrder()
        {
            for (int i = 0; i < _cardItems.Count; i++)
            {
                _cardItems[i].RectTransform.SetSiblingIndex(i);
            }
        }

        /// <summary>
        /// 切换出牌区域的拖拽高亮透明度。
        /// </summary>
        public void SetDropZoneActive(bool active)
        {
            if (_dropZone == null)
            {
                return;
            }

            var image = _dropZone.GetComponent<Image>();
            if (image != null)
            {
                Color color = image.color;
                color.a = active ? 0.7f : 0.28f;
                image.color = color;
            }
        }

        /// <summary>
        /// 克隆源卡牌到预览层，创建半透明且不接收射线的拖拽 ghost。
        /// </summary>
        public void CreateGhost(int sourceCardIdx, Vector2 pos)
        {
            DestroyGhost();
            if (!TryGetCard(sourceCardIdx, out CardItemView source) || _previewLayer == null)
            {
                return;
            }

            _ghost = UnityEngine.Object.Instantiate(source.Root, _previewLayer, false);
            _ghost.name = "CardGhost";

            // ghost 视觉以指针为中心：覆盖源卡从 ApplyFanTransform 继承的 anchor=(0,1)/pivot=(0,1)，
            // 否则 UpdateGhostPosition 的局部坐标会落到卡牌左上角而非中心，呈现"偏移跟随"。
            var rect = _ghost.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;

            SetRaycast(_ghost, false);
            EnsureCanvasGroup(_ghost).alpha = 0.72f;
            UpdateGhostPosition(pos);
        }

        /// <summary>
        /// 将拖拽 ghost 移动到指针在预览层中的本地坐标。
        /// </summary>
        public void UpdateGhostPosition(Vector2 pos)
        {
            if (_ghost == null)
            {
                return;
            }

            var rect = _ghost.GetComponent<RectTransform>();
            rect.SetParent(_previewLayer, false);
            rect.sizeDelta = new Vector2(_options.CardWidth, _options.CardHeight);
            Camera cam = _canvas != null ? _canvas.worldCamera : null;
            // 用 localPosition（直接在父级局部坐标系定位 pivot）而非 anchoredPosition，
            // 后者依赖 anchor 反推、parent.pivot 非 (0.5,0.5) 时会引入额外偏移。
            if (_previewLayer != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, pos, cam, out Vector2 local))
            {
                rect.localPosition = new Vector3(local.x, local.y, rect.localPosition.z);
            }
        }

        /// <summary>
        /// 销毁当前拖拽 ghost，并清空引用。
        /// </summary>
        public void DestroyGhost()
        {
            UguiViewUtil.DestroyObject(_ghost);
            _ghost = null;
        }

        /// <summary>
        /// 克隆源卡牌到手牌容器，创建半透明且不接收射线的插入占位卡。
        /// </summary>
        public void CreateInsertSlot(int sourceCardIdx)
        {
            DestroyInsertSlot();
            if (!TryGetCard(sourceCardIdx, out CardItemView source) || _handFan == null)
            {
                return;
            }

            _insertSlot = UnityEngine.Object.Instantiate(source.Root, _handFan, false);
            _insertSlot.name = "InsertSlot";
            SetRaycast(_insertSlot, false);
            EnsureCanvasGroup(_insertSlot).alpha = 0.35f;
        }

        /// <summary>
        /// 销毁当前插入占位卡，并清空引用。
        /// </summary>
        public void DestroyInsertSlot()
        {
            UguiViewUtil.DestroyObject(_insertSlot);
            _insertSlot = null;
        }

        /// <summary>
        /// 将扇形布局槽位应用到插入占位卡的锚点、位置、旋转和尺寸。
        /// </summary>
        public void ApplyInsertSlotTransform(FanSlotAssignment slot)
        {
            if (_insertSlot == null)
            {
                return;
            }

            var rect = _insertSlot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(slot.Left, -slot.Top - slot.TranslateY);
            rect.localRotation = Quaternion.Euler(0f, 0f, slot.RotateDegrees);
            rect.sizeDelta = new Vector2(_options.CardWidth, _options.CardHeight);
        }

        /// <summary>
        /// 使用注入的调度器延迟执行指定操作。
        /// </summary>
        public void Schedule(Action action, long delayMs)
        {
            _scheduleAction(action, delayMs);
        }

        /// <summary>
        /// UGUI 适配层不需要显式捕获指针，此方法保留为空实现。
        /// </summary>
        public void CapturePointer(int cardIdx, int pointerId)
        {
        }

        /// <summary>
        /// UGUI 适配层不需要显式释放指针，此方法保留为空实现。
        /// </summary>
        public void ReleasePointer(int cardIdx, int pointerId)
        {
        }

        private bool TryGetCard(int cardIdx, out CardItemView card)
        {
            if (cardIdx >= 0 && cardIdx < _cardItems.Count)
            {
                card = _cardItems[cardIdx];
                return true;
            }

            card = null;
            return false;
        }

        private Rect ToScreenRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return Rect.zero;
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Camera cam = _canvas != null ? _canvas.worldCamera : null;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return new Rect(bottomLeft.x, bottomLeft.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            return target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>();
        }

        private static void SetRaycast(GameObject target, bool enabled)
        {
            foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = enabled;
            }

            CanvasGroup group = EnsureCanvasGroup(target);
            group.blocksRaycasts = enabled;
            group.interactable = enabled;
        }

        private static void ScheduleAsync(Action action, long delayMs)
        {
            RunScheduleAsync(action, delayMs).Forget();
        }

        private static async UniTaskVoid RunScheduleAsync(Action action, long delayMs)
        {
            await UniTask.Delay((int)Mathf.Max(0, delayMs));
            action?.Invoke();
        }
    }
}
