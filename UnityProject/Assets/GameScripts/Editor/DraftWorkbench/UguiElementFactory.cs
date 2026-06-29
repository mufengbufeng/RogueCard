#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// UGUI 元素工厂，集中创建和更新 Draft Workbench 生成的基础 UI 节点。
    /// <para>本类只消费已经计算好的 <see cref="UguiNodeDescriptor"/>，不解析 JSON，不处理层级遍历。</para>
    /// </summary>
    public static class UguiElementFactory
    {
        private const string ImageTypeName = "UnityEngine.UI.Image";
        private const string ButtonTypeName = "UnityEngine.UI.Button";
        private const string LegacyTextTypeName = "UnityEngine.UI.Text";
        private const string TmpTextTypeName = "TMPro.TextMeshProUGUI";

        /// <summary>
        /// 根据描述符自动选择容器、图片、按钮或 TextMeshProUGUI 创建方法。
        /// </summary>
        /// <param name="name">新节点名称。</param>
        /// <param name="parent">父级 Transform。</param>
        /// <param name="desc">节点描述符。</param>
        /// <returns>创建出的 GameObject。</returns>
        public static GameObject CreateElement(string name, Transform parent, UguiNodeDescriptor desc)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));

            if (HasComponentType(desc, ButtonTypeName))
                return CreateButton(name, parent, desc).gameObject;

            if (HasComponentType(desc, TmpTextTypeName) || HasComponentType(desc, LegacyTextTypeName))
                return CreateText(name, parent, desc).gameObject;

            if (HasComponentType(desc, ImageTypeName))
                return CreateImage(name, parent, desc).gameObject;

            return CreateContainer(name, parent, desc).gameObject;
        }

        /// <summary>
        /// 创建仅包含 RectTransform 的容器节点。
        /// </summary>
        /// <param name="name">新节点名称。</param>
        /// <param name="parent">父级 Transform。</param>
        /// <param name="desc">节点描述符。</param>
        /// <returns>创建出的 RectTransform。</returns>
        public static RectTransform CreateContainer(string name, Transform parent, UguiNodeDescriptor desc)
        {
            var go = CreateBaseGameObject(name, parent, desc);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 创建带 Image 组件的节点，并应用描述符中的布局和视觉属性。
        /// </summary>
        /// <param name="name">新节点名称。</param>
        /// <param name="parent">父级 Transform。</param>
        /// <param name="desc">节点描述符。</param>
        /// <returns>创建出的 Image 组件。</returns>
        public static Image CreateImage(string name, Transform parent, UguiNodeDescriptor desc)
        {
            var go = CreateBaseGameObject(name, parent, desc);
            var image = go.AddComponent<Image>();
            ApplyVisuals(go, desc.Visuals);
            return image;
        }

        /// <summary>
        /// 创建带 Image 和 Button 组件的节点，并应用描述符中的布局和视觉属性。
        /// </summary>
        /// <param name="name">新节点名称。</param>
        /// <param name="parent">父级 Transform。</param>
        /// <param name="desc">节点描述符。</param>
        /// <returns>创建出的 Button 组件。</returns>
        public static Button CreateButton(string name, Transform parent, UguiNodeDescriptor desc)
        {
            var go = CreateBaseGameObject(name, parent, desc);
            go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            ApplyVisuals(go, desc.Visuals);
            return button;
        }

        /// <summary>
        /// 创建带 TextMeshProUGUI 组件的文本节点，并应用描述符中的布局和文字属性。
        /// </summary>
        /// <param name="name">新节点名称。</param>
        /// <param name="parent">父级 Transform。</param>
        /// <param name="desc">节点描述符。</param>
        /// <returns>创建出的 TextMeshProUGUI 组件。</returns>
        public static TextMeshProUGUI CreateText(string name, Transform parent, UguiNodeDescriptor desc)
        {
            var go = CreateBaseGameObject(name, parent, desc);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            ApplyVisuals(go, desc.Visuals);
            return text;
        }

        /// <summary>
        /// 确保目标 GameObject 拥有描述符声明的核心组件。
        /// <para>用于更新已存在节点时补齐 Image、Button、TextMeshProUGUI 等组件。</para>
        /// </summary>
        /// <param name="go">目标 GameObject。</param>
        /// <param name="desc">节点描述符。</param>
        public static void EnsureComponents(GameObject go, UguiNodeDescriptor desc)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));
            if (desc == null) throw new ArgumentNullException(nameof(desc));

            EnsureRectTransform(go);

            if (HasComponentType(desc, ImageTypeName) && go.GetComponent<Image>() == null)
                go.AddComponent<Image>();

            if (HasComponentType(desc, ButtonTypeName) && go.GetComponent<Button>() == null)
                go.AddComponent<Button>();

            if (HasComponentType(desc, TmpTextTypeName) && go.GetComponent<TextMeshProUGUI>() == null)
                go.AddComponent<TextMeshProUGUI>();

            if (HasComponentType(desc, LegacyTextTypeName) && go.GetComponent<Text>() == null && !HasComponentType(desc, TmpTextTypeName))
                go.AddComponent<Text>();

            AddUnknownComponents(go, desc);
        }

        /// <summary>
        /// 确保目标 GameObject 拥有 RectTransform。
        /// </summary>
        /// <param name="go">目标 GameObject。</param>
        /// <returns>目标节点上的 RectTransform。</returns>
        public static RectTransform EnsureRectTransform(GameObject go)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));
            return go.transform as RectTransform ?? go.AddComponent<RectTransform>();
        }

        /// <summary>
        /// 将描述符中的布局参数一次性写入 RectTransform。
        /// </summary>
        /// <param name="rect">目标 RectTransform。</param>
        /// <param name="desc">节点描述符。</param>
        public static void ApplyRectTransform(RectTransform rect, UguiNodeDescriptor desc)
        {
            if (rect == null) throw new ArgumentNullException(nameof(rect));
            if (desc == null) throw new ArgumentNullException(nameof(desc));

            rect.anchorMin = desc.AnchorMin;
            rect.anchorMax = desc.AnchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = desc.AnchoredPosition;
            rect.sizeDelta = desc.SizeDelta;
        }

        /// <summary>
        /// 将视觉属性应用到 Image、TextMeshProUGUI 或旧版 Text 组件。
        /// </summary>
        /// <param name="go">目标 GameObject。</param>
        /// <param name="visuals">视觉属性，可为 null。</param>
        public static void ApplyVisuals(GameObject go, UiNodeVisuals visuals)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));
            if (visuals == null)
                return;

            ApplyImageVisuals(go.GetComponent<Image>(), visuals);
            ApplyTmpTextVisuals(go.GetComponent<TextMeshProUGUI>(), visuals);
            ApplyLegacyTextVisuals(go.GetComponent<Text>(), visuals);
        }

        /// <summary>
        /// 判断描述符是否包含指定组件类型名。
        /// </summary>
        /// <param name="desc">节点描述符。</param>
        /// <param name="componentTypeName">组件类型全名。</param>
        /// <returns>包含该组件类型时返回 true。</returns>
        public static bool HasComponentType(UguiNodeDescriptor desc, string componentTypeName)
        {
            if (desc?.ComponentTypeNames == null || string.IsNullOrEmpty(componentTypeName))
                return false;

            return desc.ComponentTypeNames.Contains(componentTypeName);
        }

        /// <summary>
        /// 创建基础 GameObject，并写入 RectTransform 布局值。
        /// </summary>
        private static GameObject CreateBaseGameObject(string name, Transform parent, UguiNodeDescriptor desc)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (desc == null) throw new ArgumentNullException(nameof(desc));

            var go = new GameObject(string.IsNullOrEmpty(name) ? "GeneratedNode" : name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ApplyRectTransform(go.GetComponent<RectTransform>(), desc);
            return go;
        }

        /// <summary>
        /// 为 Image 组件应用颜色、不透明度和 Sprite 引用。
        /// </summary>
        private static void ApplyImageVisuals(Image image, UiNodeVisuals visuals)
        {
            if (image == null)
                return;

            Sprite sprite = DraftSpriteResolver.Resolve(visuals, out string resolvedPath);
            if (sprite != null)
            {
                image.sprite = sprite;
                if (string.IsNullOrEmpty(visuals.SpriteAssetPath))
                    visuals.SpriteAssetPath = resolvedPath;
                if (string.IsNullOrEmpty(visuals.SpriteGuid) && !string.IsNullOrEmpty(resolvedPath))
                    visuals.SpriteGuid = AssetDatabase.AssetPathToGUID(resolvedPath);
            }
            else if (HasSpriteReference(visuals))
            {
                Debug.LogWarning($"[UguiElementFactory] 找不到 Sprite 资源: {GetSpriteReferenceLabel(visuals)}");
            }

            if (visuals.UseSlicedImage)
            {
                image.type = Image.Type.Sliced;
            }

            if (visuals.NodeColor.HasValue)
            {
                image.color = visuals.NodeColor.Value;
            }

            if (visuals.Opacity.HasValue)
            {
                var color = image.color;
                color.a = Mathf.Clamp01(visuals.Opacity.Value);
                image.color = color;
            }
        }

        private static bool HasSpriteReference(UiNodeVisuals visuals)
        {
            return visuals != null
                   && (!string.IsNullOrEmpty(visuals.SpriteAssetPath)
                       || !string.IsNullOrEmpty(visuals.SpriteGuid)
                       || !string.IsNullOrEmpty(visuals.AssetMarker));
        }

        private static string GetSpriteReferenceLabel(UiNodeVisuals visuals)
        {
            if (visuals == null)
                return "<null>";

            if (!string.IsNullOrEmpty(visuals.SpriteGuid))
                return visuals.SpriteGuid;

            if (!string.IsNullOrEmpty(visuals.SpriteAssetPath))
                return visuals.SpriteAssetPath;

            return visuals.AssetMarker ?? "<empty>";
        }

        /// <summary>
        /// 为 TextMeshProUGUI 组件应用文字内容、字号、颜色和对齐。
        /// </summary>
        private static void ApplyTmpTextVisuals(TextMeshProUGUI text, UiNodeVisuals visuals)
        {
            if (text == null)
                return;

            if (visuals.TextContent != null)
            {
                text.text = visuals.TextContent;
            }

            if (visuals.FontSize > 0)
            {
                text.fontSize = visuals.FontSize;
            }

            if (visuals.NodeColor.HasValue)
            {
                text.color = visuals.NodeColor.Value;
            }

            if (visuals.HasTextAlignment)
            {
                text.alignment = ConvertTextAnchorToTmpAlignment(visuals.TextAlignment);
            }
        }

        /// <summary>
        /// 为旧版 Text 组件应用文字内容、字号、颜色和对齐，用于兼容旧描述符。
        /// </summary>
        private static void ApplyLegacyTextVisuals(Text text, UiNodeVisuals visuals)
        {
            if (text == null)
                return;

            if (visuals.TextContent != null)
            {
                text.text = visuals.TextContent;
            }

            if (visuals.FontSize > 0)
            {
                text.fontSize = visuals.FontSize;
            }

            if (visuals.NodeColor.HasValue)
            {
                text.color = visuals.NodeColor.Value;
            }

            if (visuals.HasTextAlignment)
            {
                text.alignment = visuals.TextAlignment;
            }
        }

        /// <summary>
        /// 将 UGUI TextAnchor 转换为 TextMeshProUGUI 对齐枚举。
        /// </summary>
        private static TextAlignmentOptions ConvertTextAnchorToTmpAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        /// <summary>
        /// 通过反射补齐描述符中的未知组件，保留旧数据兼容能力。
        /// </summary>
        private static void AddUnknownComponents(GameObject go, UguiNodeDescriptor desc)
        {
            if (desc.ComponentTypeNames == null)
                return;

            foreach (string typeName in desc.ComponentTypeNames)
            {
                if (string.IsNullOrEmpty(typeName) || IsKnownComponentType(typeName))
                    continue;

                Type componentType = FindType(typeName);
                if (componentType == null)
                {
                    Debug.LogWarning($"[UguiElementFactory] 找不到组件类型 '{typeName}'，跳过。");
                    continue;
                }

                if (go.GetComponent(componentType) == null)
                {
                    go.AddComponent(componentType);
                }
            }
        }

        /// <summary>
        /// 判断类型名是否已由强类型分支处理。
        /// </summary>
        private static bool IsKnownComponentType(string typeName)
        {
            return typeName == ImageTypeName
                   || typeName == ButtonTypeName
                   || typeName == LegacyTextTypeName
                   || typeName == TmpTextTypeName;
        }

        /// <summary>
        /// 通过全名查找组件类型。
        /// </summary>
        private static Type FindType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
#endif
