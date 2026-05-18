using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UGUI 卡牌预览适配层，负责克隆、坐标转换、raycast 关闭和非卡区域点击遮罩。
    /// </summary>
    public sealed class UguiPreviewSurface : IPreviewSurface
    {
        private readonly RectTransform _handFan;
        private readonly RectTransform _previewLayer;
        private readonly IReadOnlyList<CardItemView> _cardItems;

        /// <summary>
        /// 创建 UGUI 预览适配层。
        /// </summary>
        public UguiPreviewSurface(RectTransform handFan, RectTransform previewLayer, IReadOnlyList<CardItemView> cardItems)
        {
            _handFan = handFan;
            _previewLayer = previewLayer;
            _cardItems = cardItems;
        }

        /// <inheritdoc />
        public GameObject ClonePreviewElement(CardItemView source)
        {
            if (source == null || source.Root == null || _previewLayer == null)
            {
                return null;
            }

            GameObject clone = Object.Instantiate(source.Root, _previewLayer, false);
            clone.name = "CardPreview";
            clone.SetActive(true);
            foreach (CardPointerRelay relay in clone.GetComponentsInChildren<CardPointerRelay>(true))
            {
                relay.Initialize(null);
            }

            SetRaycast(clone, false);
            CanvasGroup group = EnsureCanvasGroup(clone);
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return clone;
        }

        /// <inheritdoc />
        public void AddToPreviewLayer(GameObject element)
        {
            if (element == null || _previewLayer == null)
            {
                return;
            }

            element.transform.SetParent(_previewLayer, false);
            element.transform.SetAsLastSibling();
        }

        /// <inheritdoc />
        public void RemoveFromPreviewLayer(GameObject element)
        {
            UguiViewUtil.DestroyObject(element);
        }

        /// <inheritdoc />
        public Vector2 GetSourceTopCenterInHandFanLocal(CardItemView source)
        {
            if (source == null || source.RectTransform == null || _handFan == null)
            {
                return Vector2.zero;
            }

            var corners = new Vector3[4];
            source.RectTransform.GetWorldCorners(corners);
            Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;
            return _handFan.InverseTransformPoint(topCenterWorld);
        }

        /// <inheritdoc />
        public Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan)
        {
            if (_handFan == null || _previewLayer == null)
            {
                return sourceTopCenterInHandFan;
            }

            Vector3 world = _handFan.TransformPoint(sourceTopCenterInHandFan);
            return _previewLayer.InverseTransformPoint(world);
        }

        /// <inheritdoc />
        public void ApplyPreviewTransform(GameObject element, Vector2 topCenterInPreviewLayer, HandFanLayoutOptions options)
        {
            if (element == null)
            {
                return;
            }

            options ??= new HandFanLayoutOptions();
            RectTransform rect = element.GetComponent<RectTransform>() ?? element.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = topCenterInPreviewLayer + new Vector2(0f, 12f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = new Vector3(1.35f, 1.35f, 1f);
            rect.sizeDelta = new Vector2(options.CardWidth, options.CardHeight);
        }

        /// <inheritdoc />
        public void ClearAllHoverState()
        {
            if (_cardItems == null)
            {
                return;
            }

            foreach (CardItemView item in _cardItems)
            {
                item?.SetHover(false);
            }
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
        }
    }
}
