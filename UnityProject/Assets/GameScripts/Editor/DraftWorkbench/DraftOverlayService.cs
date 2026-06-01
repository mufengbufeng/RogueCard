#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 草稿叠加层管理服务，负责创建、更新、查找和移除编辑器内的草稿叠加 Image 对象。
    /// </summary>
    public static class DraftOverlayService
    {
        /// <summary>
        /// 叠加层 GameObject 的固定名称。
        /// </summary>
        private const string OverlayName = "__DraftOverlay";

        /// <summary>
        /// 在指定 Canvas 根节点下创建草稿叠加层 Image 对象。
        /// </summary>
        /// <param name="canvasRoot">Canvas 根节点，叠加层将作为其子对象。</param>
        /// <param name="draftImage">草稿图片纹理，用于生成 Sprite。</param>
        /// <param name="settings">叠加层显示设置。</param>
        /// <returns>创建的叠加层 GameObject。</returns>
        public static GameObject CreateOverlay(GameObject canvasRoot, Texture2D draftImage, DraftOverlaySettings settings)
        {
            RemoveOverlay(canvasRoot);

            var overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(canvasRoot.transform, false);
            overlay.tag = "EditorOnly";

            var image = overlay.GetComponent<Image>();
            var sprite = Sprite.Create(
                draftImage,
                new Rect(0, 0, draftImage.width, draftImage.height),
                new Vector2(0.5f, 0.5f)
            );
            sprite.name = draftImage.name;
            image.sprite = sprite;
            image.raycastTarget = false;

            ApplyOverlayLayout(overlay, draftImage, settings);
            ApplyOpacity(image, settings);

            return overlay;
        }

        /// <summary>
        /// 更新已有叠加层的 RectTransform 布局和不透明度。
        /// </summary>
        /// <param name="overlay">要更新的叠加层 GameObject。</param>
        /// <param name="settings">新的叠加层显示设置。</param>
        public static void UpdateOverlay(GameObject overlay, DraftOverlaySettings settings)
        {
            if (overlay == null)
                return;

            var image = overlay.GetComponent<Image>();
            if (image == null || image.sprite == null)
                return;

            var texture = image.sprite.texture;
            ApplyOverlayLayout(overlay, texture, settings);
            ApplyOpacity(image, settings);
        }

        /// <summary>
        /// 在指定 Canvas 根节点下查找并销毁名为 "__DraftOverlay" 的子对象。
        /// </summary>
        /// <param name="canvasRoot">Canvas 根节点。</param>
        public static void RemoveOverlay(GameObject canvasRoot)
        {
            var existing = FindOverlay(canvasRoot);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        /// <summary>
        /// 在指定 Canvas 根节点下查找名为 "__DraftOverlay" 的子对象。
        /// </summary>
        /// <param name="canvasRoot">Canvas 根节点。</param>
        /// <returns>找到的叠加层 GameObject；未找到时返回 null。</returns>
        public static GameObject FindOverlay(GameObject canvasRoot)
        {
            if (canvasRoot == null)
                return null;

            var transform = canvasRoot.transform;
            var found = transform.Find(OverlayName);
            return found != null ? found.gameObject : null;
        }

        /// <summary>
        /// 根据 OverlayFitMode 将 RectTransform 布局应用到叠加层对象上。
        /// </summary>
        /// <param name="overlay">叠加层 GameObject。</param>
        /// <param name="texture">草稿纹理，用于计算宽高比。</param>
        /// <param name="settings">叠加层显示设置。</param>
        private static void ApplyOverlayLayout(GameObject overlay, Texture2D texture, DraftOverlaySettings settings)
        {
            var rectTransform = overlay.GetComponent<RectTransform>();
            float imageAspect = (float)texture.width / texture.height;
            float canvasAspect = settings.CanvasReferenceResolution.x / settings.CanvasReferenceResolution.y;

            switch (settings.FitMode)
            {
                case OverlayFitMode.Fill:
                    ApplyStretchAnchors(rectTransform);
                    break;

                case OverlayFitMode.FitWidth:
                    // 垂直方向拉伸，水平方向按图片宽高比居中
                    rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    float fitWidthSizeX = settings.CanvasReferenceResolution.y * imageAspect;
                    rectTransform.sizeDelta = new Vector2(fitWidthSizeX, 0f);
                    rectTransform.anchoredPosition = Vector2.zero;
                    break;

                case OverlayFitMode.FitHeight:
                    // 水平方向拉伸，垂直方向按图片宽高比居中
                    rectTransform.anchorMin = new Vector2(0f, 0.5f);
                    rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    float fitHeightSizeY = settings.CanvasReferenceResolution.x / imageAspect;
                    rectTransform.sizeDelta = new Vector2(0f, fitHeightSizeY);
                    rectTransform.anchoredPosition = Vector2.zero;
                    break;

                case OverlayFitMode.Stretch:
                    ApplyStretchAnchors(rectTransform);
                    break;
            }
        }

        /// <summary>
        /// 将 RectTransform 设置为完全拉伸填满父节点（锚点 0,0 ~ 1,1）。
        /// </summary>
        /// <param name="rectTransform">目标 RectTransform。</param>
        private static void ApplyStretchAnchors(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 将 Image 的 color alpha 设置为 settings.Opacity。
        /// </summary>
        /// <param name="image">目标 Image 组件。</param>
        /// <param name="settings">叠加层显示设置。</param>
        private static void ApplyOpacity(Image image, DraftOverlaySettings settings)
        {
            var color = image.color;
            color.a = Mathf.Clamp01(settings.Opacity);
            image.color = color;
        }
    }
}
#endif
