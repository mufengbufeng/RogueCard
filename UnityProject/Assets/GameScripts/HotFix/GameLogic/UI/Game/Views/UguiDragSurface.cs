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
        }

        /// <inheritdoc />
        public int CardCount => _cardItems.Count;

        /// <inheritdoc />
        public Rect DropZoneWorldBound => ToWorldRect(_dropZone);

        /// <inheritdoc />
        public Rect HandFanWorldBound => ToWorldRect(_handFan);

        /// <inheritdoc />
        public bool DropZoneAvailable => _dropZone != null && _dropZone.gameObject.activeInHierarchy;

        /// <inheritdoc />
        public float HandFanWidth => _handFan != null && _handFan.rect.width > 0f ? _handFan.rect.width : 800f;

        /// <inheritdoc />
        public float HandFanHeight => _handFan != null && _handFan.rect.height > 0f ? _handFan.rect.height : 280f;

        /// <inheritdoc />
        public IDragHostCallbacks Callbacks => _callbacks;

        /// <inheritdoc />
        public Rect GetCardWorldBound(int cardIdx)
        {
            return cardIdx >= 0 && cardIdx < _cardItems.Count ? ToWorldRect(_cardItems[cardIdx].RectTransform) : Rect.zero;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void SetCardOpacity(int cardIdx, float opacity)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.SetOpacity(opacity);
            }
        }

        /// <inheritdoc />
        public void ResetCardOpacity(int cardIdx) => SetCardOpacity(cardIdx, 1f);

        /// <inheritdoc />
        public void SetCardPickingMode(int cardIdx, bool pickable)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.SetPicking(pickable);
            }
        }

        /// <inheritdoc />
        public void SetCardTransitionDuration(int cardIdx, float seconds)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.Root.name = $"{card.Root.name.Split('#')[0]}#transition={seconds:0.###}";
            }
        }

        /// <inheritdoc />
        public void ClearCardTransitionDuration(int cardIdx)
        {
            if (TryGetCard(cardIdx, out CardItemView card))
            {
                card.Root.name = card.Root.name.Split('#')[0];
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void SyncSiblingOrder()
        {
            for (int i = 0; i < _cardItems.Count; i++)
            {
                _cardItems[i].RectTransform.SetSiblingIndex(i);
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void CreateGhost(int sourceCardIdx, Vector2 pos)
        {
            DestroyGhost();
            if (!TryGetCard(sourceCardIdx, out CardItemView source) || _previewLayer == null)
            {
                return;
            }

            _ghost = UnityEngine.Object.Instantiate(source.Root, _previewLayer, false);
            _ghost.name = "CardGhost";
            SetRaycast(_ghost, false);
            EnsureCanvasGroup(_ghost).alpha = 0.72f;
            UpdateGhostPosition(pos);
        }

        /// <inheritdoc />
        public void UpdateGhostPosition(Vector2 pos)
        {
            if (_ghost == null)
            {
                return;
            }

            var rect = _ghost.GetComponent<RectTransform>();
            rect.SetParent(_previewLayer, false);
            rect.sizeDelta = new Vector2(_options.CardWidth, _options.CardHeight);
            rect.position = pos;
        }

        /// <inheritdoc />
        public void DestroyGhost()
        {
            UguiViewUtil.DestroyObject(_ghost);
            _ghost = null;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void DestroyInsertSlot()
        {
            UguiViewUtil.DestroyObject(_insertSlot);
            _insertSlot = null;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void Schedule(Action action, long delayMs)
        {
            _scheduleAction(action, delayMs);
        }

        /// <inheritdoc />
        public void CapturePointer(int cardIdx, int pointerId)
        {
        }

        /// <inheritdoc />
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

        private static Rect ToWorldRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return Rect.zero;
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector3 bottomLeft = corners[0];
            Vector3 topRight = corners[2];
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
