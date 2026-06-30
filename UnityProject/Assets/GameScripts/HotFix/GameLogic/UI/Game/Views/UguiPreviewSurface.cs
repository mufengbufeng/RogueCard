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

        /// <summary>
        /// 克隆源卡牌为不可交互的预览对象，并关闭射线响应与指针转发。
        /// </summary>
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

        /// <summary>
        /// 将预览对象移动到 preview-layer，并置于最上层显示。
        /// </summary>
        public void AddToPreviewLayer(GameObject element)
        {
            if (element == null || _previewLayer == null)
            {
                return;
            }

            element.transform.SetParent(_previewLayer, false);
            element.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 销毁预览对象以从 preview-layer 移除。
        /// </summary>
        public void RemoveFromPreviewLayer(GameObject element)
        {
            UguiViewUtil.DestroyObject(element);
        }

        /// <summary>
        /// 计算源卡牌顶部中心在 hand-fan 本地坐标中的位置。
        /// </summary>
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

        /// <summary>
        /// 将 hand-fan 本地坐标经世界坐标转换为 preview-layer 本地坐标。
        /// </summary>
        public Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan)
        {
            if (_handFan == null || _previewLayer == null)
            {
                return sourceTopCenterInHandFan;
            }

            Vector3 world = _handFan.TransformPoint(sourceTopCenterInHandFan);
            return _previewLayer.InverseTransformPoint(world);
        }

        /// <summary>
        /// 将预览对象放在源卡顶部上方，并应用尺寸、复位旋转和放大缩放。
        /// </summary>
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

        /// <summary>
        /// 关闭当前手牌列表中所有卡牌的 hover 视觉状态。
        /// </summary>
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
