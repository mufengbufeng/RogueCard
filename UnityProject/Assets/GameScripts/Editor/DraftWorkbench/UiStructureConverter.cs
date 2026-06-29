#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// JSON root 到 Prefab root 的转换策略。
    /// </summary>
    public enum UiStructureRootMode
    {
        /// <summary>JSON root 只作为坐标父框和语义容器，不输出 UGUI 节点。</summary>
        VirtualRoot = 0,

        /// <summary>兼容旧行为，把 JSON root 作为真实 UGUI 节点输出。</summary>
        IncludeRootNode = 1,

        /// <summary>保留给显式 root 映射场景；当前转换侧按 VirtualRoot 输出，避免创建同名子 root。</summary>
        MapRootToPrefabRoot = 2
    }

    /// <summary>
    /// UI 结构转换选项。
    /// </summary>
    [Serializable]
    public class UiStructureConversionOptions
    {
        /// <summary>默认 Draft Workbench 转换选项：JSON root 为虚拟根。</summary>
        public static UiStructureConversionOptions Default => new UiStructureConversionOptions();

        /// <summary>兼容旧版完整 root 节点输出。</summary>
        public static UiStructureConversionOptions IncludeRootNode => new UiStructureConversionOptions
        {
            RootMode = UiStructureRootMode.IncludeRootNode
        };

        /// <summary>Draft Workbench 使用的默认选项。</summary>
        public static UiStructureConversionOptions DraftWorkbenchDefault => Default;

        /// <summary>root 处理模式。</summary>
        public UiStructureRootMode RootMode = UiStructureRootMode.VirtualRoot;

        /// <summary>目标 Prefab root 名称，仅供 MapRootToPrefabRoot 调用方记录意图。</summary>
        public string TargetRootName;

        /// <summary>是否尝试把 JSON root 的 rect 应用到目标 root；当前转换器不直接修改 prefab root。</summary>
        public bool ApplyRootRectToTarget;
    }

    /// <summary>
    /// 转换后的节点数据，将 <see cref="UguiNodeDescriptor"/> 与其关联的视觉信息和布局信息打包。
    /// </summary>
    [Serializable]
    public class ConvertedNode
    {
        /// <summary>
        /// UGUI 节点描述符，包含布局参数与组件列表。
        /// </summary>
        public UguiNodeDescriptor Descriptor;

        /// <summary>
        /// 节点视觉信息，包含颜色、文本、字体大小和不透明度。
        /// </summary>
        public UiNodeVisuals Visuals;

        /// <summary>
        /// 布局组信息，描述该节点作为布局容器时的排列参数。为 null 表示非布局容器。
        /// </summary>
        public LayoutGroupInfo LayoutInfo;
    }

    /// <summary>
    /// 将 <see cref="UiStructure"/>（Image-To-UI 工具输出的 ui_structure.json 结构）
    /// 转换为 <see cref="ConvertedNode"/> 列表，供 <see cref="PrefabDraftBuilder"/> 消费。
    /// <para>
    /// 转换过程包括：
    /// <list type="bullet">
    ///   <item>元素名称去重（自动追加 _1, _2 后缀）</item>
    ///   <item>锚点/位置计算（支持对齐、绝对定位和拉伸三种模式）</item>
    ///   <item>Y 轴翻转（Image-To-UI 原点在左上角，UGUI 原点在左下角）</item>
    ///   <item>元素类型到 UGUI 组件的映射</item>
    ///   <item>布局组信息提取</item>
    /// </list>
    /// </para>
    /// </summary>
    public class UiStructureConverter
    {
        private string _spriteRootFolder;

        /// <summary>
        /// 最近一次转换操作的报告，记录转换过程中的警告和重命名信息。
        /// </summary>
        public ConversionReport Report { get; private set; }

        /// <summary>
        /// 将 <see cref="UiStructure"/> 转换为 <see cref="ConvertedNode"/> 列表。
        /// <para>
        /// 返回的列表为深度优先顺序（根节点在前，子节点紧随其后）。
        /// 每次调用会重置 <see cref="Report"/>。
        /// </para>
        /// </summary>
        /// <param name="structure">Image-To-UI 工具输出的 UI 结构数据。</param>
        /// <returns>转换后的节点列表。如果 <paramref name="structure"/> 为 null 或 root 为 null，返回空列表。</returns>
        public List<ConvertedNode> Convert(UiStructure structure)
        {
            return Convert(structure, UiStructureConversionOptions.Default);
        }

        /// <summary>
        /// 使用指定选项将 <see cref="UiStructure"/> 转换为 <see cref="ConvertedNode"/> 列表。
        /// </summary>
        /// <param name="structure">Image-To-UI 工具输出的 UI 结构数据。</param>
        /// <param name="options">转换选项；为 null 时使用 Draft Workbench 默认 VirtualRoot。</param>
        /// <returns>转换后的节点列表。</returns>
        public List<ConvertedNode> Convert(UiStructure structure, UiStructureConversionOptions options)
        {
            Report = new ConversionReport();
            options = options ?? UiStructureConversionOptions.Default;
            _spriteRootFolder = structure?.unity?.spriteRootFolder;

            if (structure?.root == null)
            {
                Report?.AddWarning("UiStructure 或其 root 为 null，跳过转换。");
                return new List<ConvertedNode>();
            }

            int canvasWidth = structure.canvas?.width ?? 1920;
            int canvasHeight = structure.canvas?.height ?? 1080;

            var results = new List<ConvertedNode>();
            var rootSiblingNames = new HashSet<string>(StringComparer.Ordinal);

            var canvasSourceRect = new Rect(0f, 0f, canvasWidth, canvasHeight);
            if (options.RootMode == UiStructureRootMode.IncludeRootNode)
            {
                ConvertElement(structure.root, "", canvasWidth, canvasHeight, canvasSourceRect, false, rootSiblingNames, results);
            }
            else
            {
                ConvertVirtualRootChildren(structure.root, canvasWidth, canvasHeight, canvasSourceRect, results);
            }

            return results;
        }

        // ─────────────────────────── 核心递归转换 ───────────────────────────

        /// <summary>
        /// VirtualRoot 模式下跳过 JSON root 自身，仅转换其直接子节点。
        /// </summary>
        private void ConvertVirtualRootChildren(
            UiElement root,
            int canvasWidth,
            int canvasHeight,
            Rect canvasSourceRect,
            List<ConvertedNode> results)
        {
            if (root?.children == null || root.children.Count == 0)
                return;

            Rect rootSourceRect = ResolveVirtualRootSourceRect(root, canvasSourceRect);
            bool rootHasLayout = root.layout != null && !HasPositionedChildren(root);
            var siblingNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in root.children)
            {
                ConvertElement(child, "", canvasWidth, canvasHeight, rootSourceRect, rootHasLayout, siblingNames, results);
            }
        }

        /// <summary>
        /// 解析 JSON root 作为坐标父框时使用的源图区域。
        /// </summary>
        private static Rect ResolveVirtualRootSourceRect(UiElement root, Rect canvasSourceRect)
        {
            float width = root?.size?.width ?? canvasSourceRect.width;
            float height = root?.size?.height ?? canvasSourceRect.height;
            if (root?.position == null || width <= 0f || height <= 0f)
                return canvasSourceRect;

            return new Rect(root.position.x, root.position.y, width, height);
        }

        /// <summary>
        /// 递归转换单个 <see cref="UiElement"/> 为 <see cref="ConvertedNode"/>，
        /// 并将其自身及所有子节点的转换结果追加到 <paramref name="results"/> 中。
        /// </summary>
        /// <param name="elem">待转换的 UI 元素。</param>
        /// <param name="parentPath">父节点在层级中的路径。根节点为空字符串。</param>
        /// <param name="canvasWidth">画布宽度，用于锚点计算。</param>
        /// <param name="canvasHeight">画布高度，用于锚点计算。</param>
        /// <param name="parentSourceRect">父元素在源图全局坐标系中的识别框。</param>
        /// <param name="hasParentLayout">父节点是否包含布局组。</param>
        /// <param name="siblingNames">同级兄弟节点的名称集合，用于去重。</param>
        /// <param name="results">转换结果累加列表。</param>
        private void ConvertElement(
            UiElement elem,
            string parentPath,
            int canvasWidth,
            int canvasHeight,
            Rect parentSourceRect,
            bool hasParentLayout,
            HashSet<string> siblingNames,
            List<ConvertedNode> results)
        {
            if (elem == null)
                return;

            // 1. 创建描述符
            var descriptor = new UguiNodeDescriptor();
            string resolvedName = ResolveName(elem.name ?? "Node", siblingNames);
            descriptor.Name = resolvedName;
            descriptor.SourceCanvasSize = new Vector2(canvasWidth, canvasHeight);

            // 2. 设置父路径
            descriptor.ParentPath = parentPath;

            // 3. 提取尺寸
            float width = elem.size?.width ?? 0;
            float height = elem.size?.height ?? 0;
            ApplySourceBounds(elem, width, height, descriptor);

            // 4. 创建视觉信息
            var visuals = new UiNodeVisuals();

            // 5. 计算锚点和位置
            if (hasParentLayout && elem.position == null)
            {
                // 父节点有布局组且当前子节点没有显式 position 时，才交给布局组控制。
                descriptor.AnchorMin = Vector2.zero;
                descriptor.AnchorMax = Vector2.one;
                descriptor.AnchoredPosition = Vector2.zero;
                descriptor.SizeDelta = new Vector2(width, height);
            }
            else
            {
                ComputeAnchoring(elem, width, height, parentSourceRect, descriptor);
            }

            // 6. 元素类型 → 组件映射
            MapElementTypeToComponents(elem, descriptor, visuals);

            // 7. 提取视觉属性
            ApplyVisuals(elem, visuals);
            ApplyAssetSemantics(elem, visuals);

            // 8. 处理布局信息
            LayoutGroupInfo layoutInfo = null;
            if (elem.layout != null)
            {
                layoutInfo = BuildLayoutGroupInfo(elem);
            }

            descriptor.Visuals = visuals;
            descriptor.LayoutInfo = layoutInfo;

            // 9. 构建转换节点并追加
            var node = new ConvertedNode
            {
                Descriptor = descriptor,
                Visuals = visuals,
                LayoutInfo = layoutInfo
            };
            results.Add(node);

            // 10. 递归处理子节点
            if (elem.children != null && elem.children.Count > 0)
            {
                string currentPath = ComputeParentPath(parentPath, resolvedName);
                bool thisHasLayout = layoutInfo != null && layoutInfo.UseUnityLayoutGroup;
                var childSiblingNames = new HashSet<string>(StringComparer.Ordinal);

                Rect childParentSourceRect = ResolveChildParentSourceRect(descriptor, parentSourceRect, width, height);
                foreach (var child in elem.children)
                {
                    ConvertElement(child, currentPath, canvasWidth, canvasHeight, childParentSourceRect, thisHasLayout, childSiblingNames, results);
                }
            }
        }

        /// <summary>
        /// 计算子节点用于全局源图坐标换算的父框。
        /// </summary>
        private static Rect ResolveChildParentSourceRect(UguiNodeDescriptor descriptor, Rect fallbackParentSourceRect, float width, float height)
        {
            if (descriptor.HasSourceBounds)
                return descriptor.SourceBounds;

            return new Rect(fallbackParentSourceRect.x, fallbackParentSourceRect.y, width, height);
        }

        /// <summary>
        /// 记录元素在原始设计图坐标系中的识别框，用于工作台预览绘制。
        /// </summary>
        /// <param name="elem">源元素。</param>
        /// <param name="width">元素宽度。</param>
        /// <param name="height">元素高度。</param>
        /// <param name="descriptor">目标描述符。</param>
        private void ApplySourceBounds(UiElement elem, float width, float height, UguiNodeDescriptor descriptor)
        {
            if (elem.position == null || width <= 0f || height <= 0f)
                return;

            descriptor.HasSourceBounds = true;
            descriptor.SourceBounds = new Rect(elem.position.x, elem.position.y, width, height);
        }

        // ─────────────────────────── 锚点与位置计算 ───────────────────────────

        /// <summary>
        /// 根据 <paramref name="elem"/> 的源图坐标或对齐属性计算锚点参数，
        /// 并将结果写入 <paramref name="descriptor"/>。
        /// <para>
        /// 三种定位模式（优先级从高到低）：
        /// <list type="number">
        ///   <item>position 源图全局绝对定位模式（含父源框换算与 Y 轴翻转）</item>
        ///   <item>align / vAlign 对齐模式（仅在缺少 position 时兜底）</item>
        ///   <item>拉伸模式（默认兜底）</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="elem">源元素。</param>
        /// <param name="width">元素宽度。</param>
        /// <param name="height">元素高度。</param>
        /// <param name="parentSourceRect">父元素在源图全局坐标系中的识别框。</param>
        /// <param name="descriptor">目标描述符。</param>
        private void ComputeAnchoring(UiElement elem, float width, float height, Rect parentSourceRect, UguiNodeDescriptor descriptor)
        {
            if (elem.position != null)
            {
                // 源 position 表示源图全局左上角；RectTransform 挂到父节点后需要先换算为父本地坐标。
                float localX = elem.position.x - parentSourceRect.x;
                float localY = elem.position.y - parentSourceRect.y;

                descriptor.AnchorMin = new Vector2(0f, 1f);
                descriptor.AnchorMax = new Vector2(0f, 1f);
                descriptor.AnchoredPosition = new Vector2(
                    localX + width * 0.5f,
                    -(localY + height * 0.5f));
                descriptor.SizeDelta = new Vector2(width, height);
                return;
            }

            bool hasAlign = !string.IsNullOrEmpty(elem.align);
            bool hasVAlign = !string.IsNullOrEmpty(elem.vAlign);

            if (hasAlign || hasVAlign)
            {
                // 对齐模式仅作为缺少像素框时的兜底；带 position 的 AI2UI 节点不能被 align/vAlign 改写位置。
                ComputeAlignment(elem.align, elem.vAlign, width, height, descriptor);
            }
            else
            {
                // 拉伸模式（默认兜底）
                descriptor.AnchorMin = Vector2.zero;
                descriptor.AnchorMax = Vector2.one;
                descriptor.SizeDelta = new Vector2(width, height);
                descriptor.AnchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// 根据 align 和 vAlign 值计算锚点参数。
        /// <para>
        /// 对齐逻辑：
        /// <list type="bullet">
        ///   <item>水平: left(0,0) / center(0.5,0.5) / right(1,1)</item>
        ///   <item>垂直: top(1,1) / middle(0.5,0.5) / bottom(0,0)</item>
        ///   <item>未指定的轴默认拉伸: AnchorMin=0, AnchorMax=1, SizeDelta=实际尺寸</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="align">水平对齐: left / center / right。null 表示该轴拉伸。</param>
        /// <param name="vAlign">垂直对齐: top / middle / bottom。null 表示该轴拉伸。</param>
        /// <param name="width">元素宽度。</param>
        /// <param name="height">元素高度。</param>
        /// <param name="descriptor">目标描述符。</param>
        private void ComputeAlignment(string align, string vAlign, float width, float height, UguiNodeDescriptor descriptor)
        {
            // 水平轴
            if (!string.IsNullOrEmpty(align))
            {
                switch (align)
                {
                    case "left":
                        descriptor.AnchorMin = new Vector2(0f, descriptor.AnchorMin.y);
                        descriptor.AnchorMax = new Vector2(0f, descriptor.AnchorMax.y);
                        descriptor.AnchoredPosition = new Vector2(width / 2f, descriptor.AnchoredPosition.y);
                        break;
                    case "center":
                        descriptor.AnchorMin = new Vector2(0.5f, descriptor.AnchorMin.y);
                        descriptor.AnchorMax = new Vector2(0.5f, descriptor.AnchorMax.y);
                        descriptor.AnchoredPosition = new Vector2(0f, descriptor.AnchoredPosition.y);
                        break;
                    case "right":
                        descriptor.AnchorMin = new Vector2(1f, descriptor.AnchorMin.y);
                        descriptor.AnchorMax = new Vector2(1f, descriptor.AnchorMax.y);
                        descriptor.AnchoredPosition = new Vector2(-width / 2f, descriptor.AnchoredPosition.y);
                        break;
                    default:
                        Report.AddWarning($"未识别的水平对齐值 '{align}'，水平轴使用拉伸模式。");
                        // 拉伸：使用当前值（默认 0,1），在下面设置 SizeDelta
                        descriptor.SizeDelta = new Vector2(width, descriptor.SizeDelta.y);
                        break;
                }

                descriptor.SizeDelta = new Vector2(width, descriptor.SizeDelta.y);
            }
            else
            {
                // 水平轴拉伸
                descriptor.AnchorMin = new Vector2(0f, descriptor.AnchorMin.y);
                descriptor.AnchorMax = new Vector2(1f, descriptor.AnchorMax.y);
                descriptor.AnchoredPosition = new Vector2(0f, descriptor.AnchoredPosition.y);
                descriptor.SizeDelta = new Vector2(width, descriptor.SizeDelta.y);
            }

            // 垂直轴
            if (!string.IsNullOrEmpty(vAlign))
            {
                switch (vAlign)
                {
                    case "top":
                        descriptor.AnchorMin = new Vector2(descriptor.AnchorMin.x, 1f);
                        descriptor.AnchorMax = new Vector2(descriptor.AnchorMax.x, 1f);
                        descriptor.AnchoredPosition = new Vector2(descriptor.AnchoredPosition.x, -height / 2f);
                        break;
                    case "middle":
                        descriptor.AnchorMin = new Vector2(descriptor.AnchorMin.x, 0.5f);
                        descriptor.AnchorMax = new Vector2(descriptor.AnchorMax.x, 0.5f);
                        descriptor.AnchoredPosition = new Vector2(descriptor.AnchoredPosition.x, 0f);
                        break;
                    case "bottom":
                        descriptor.AnchorMin = new Vector2(descriptor.AnchorMin.x, 0f);
                        descriptor.AnchorMax = new Vector2(descriptor.AnchorMax.x, 0f);
                        descriptor.AnchoredPosition = new Vector2(descriptor.AnchoredPosition.x, height / 2f);
                        break;
                    default:
                        Report.AddWarning($"未识别的垂直对齐值 '{vAlign}'，垂直轴使用拉伸模式。");
                        descriptor.SizeDelta = new Vector2(descriptor.SizeDelta.x, height);
                        break;
                }

                descriptor.SizeDelta = new Vector2(descriptor.SizeDelta.x, height);
            }
            else
            {
                // 垂直轴拉伸
                descriptor.AnchorMin = new Vector2(descriptor.AnchorMin.x, 0f);
                descriptor.AnchorMax = new Vector2(descriptor.AnchorMax.x, 1f);
                descriptor.AnchoredPosition = new Vector2(descriptor.AnchoredPosition.x, 0f);
                descriptor.SizeDelta = new Vector2(descriptor.SizeDelta.x, height);
            }
        }

        // ─────────────────────────── 元素类型映射 ───────────────────────────

        /// <summary>
        /// 根据 <paramref name="elem"/> 的 type 字段映射到 UGUI 组件，
        /// 同时设置对应的视觉属性。
        /// <para>
        /// 映射规则：
        /// <list type="bullet">
        ///   <item>image → UnityEngine.UI.Image</item>
        ///   <item>rect → UnityEngine.UI.Image + 背景色</item>
        ///   <item>text → TMPro.TextMeshProUGUI + 文本/字号/颜色</item>
        ///   <item>button → UnityEngine.UI.Image + UnityEngine.UI.Button</item>
        ///   <item>overlay → UnityEngine.UI.Image + 颜色/不透明度</item>
        ///   <item>container → 无额外组件</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="elem">源元素。</param>
        /// <param name="descriptor">目标描述符。</param>
        /// <param name="visuals">目标视觉信息。</param>
        private void MapElementTypeToComponents(UiElement elem, UguiNodeDescriptor descriptor, UiNodeVisuals visuals)
        {
            string type = elem.type ?? "container";

            switch (type)
            {
                case "image":
                    descriptor.ComponentTypeNames.Add("UnityEngine.UI.Image");
                    break;

                case "rect":
                    descriptor.ComponentTypeNames.Add("UnityEngine.UI.Image");
                    if (!string.IsNullOrEmpty(elem.color))
                    {
                        visuals.NodeColor = ParseColor(elem.color);
                    }
                    break;

                case "text":
                    descriptor.ComponentTypeNames.Add("TMPro.TextMeshProUGUI");
                    string textContent = GetTextContent(elem);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        visuals.TextContent = textContent;
                    }
                    if (elem.fontSize > 0)
                    {
                        visuals.FontSize = elem.fontSize;
                    }
                    if (!string.IsNullOrEmpty(elem.color))
                    {
                        visuals.NodeColor = ParseColor(elem.color);
                    }
                    ApplyTextAlignment(elem, visuals);
                    break;

                case "button":
                    descriptor.ComponentTypeNames.Add("UnityEngine.UI.Image");
                    descriptor.ComponentTypeNames.Add("UnityEngine.UI.Button");
                    break;

                case "overlay":
                    descriptor.ComponentTypeNames.Add("UnityEngine.UI.Image");
                    if (!string.IsNullOrEmpty(elem.color))
                    {
                        visuals.NodeColor = ParseColor(elem.color);
                    }
                    if (elem.opacity >= 0f)
                    {
                        visuals.Opacity = elem.opacity;
                    }
                    break;

                case "container":
                    // 容器类型不需要额外组件
                    break;

                default:
                    Report.AddWarning($"未知的元素类型 '{type}'，元素 '{elem.name}' 跳过组件映射。");
                    break;
            }
        }

        // ─────────────────────────── 视觉属性提取 ───────────────────────────

        /// <summary>
        /// 从 <paramref name="elem"/> 中提取通用视觉属性到 <paramref name="visuals"/>。
        /// <para>
        /// 提取不透明度、颜色（非 text/rect 场景下的兜底颜色）。
        /// 此方法在 <see cref="MapElementTypeToComponents"/> 之后调用，
        /// 不会覆盖类型特定映射中已设置的值。
        /// </para>
        /// </summary>
        /// <param name="elem">源元素。</param>
        /// <param name="visuals">目标视觉信息。</param>
        private void ApplyVisuals(UiElement elem, UiNodeVisuals visuals)
        {
            // 不透明度（仅在类型映射未处理时兜底）
            if (elem.opacity >= 0f && visuals.Opacity == null)
            {
                visuals.Opacity = elem.opacity;
            }

            // 颜色（仅在类型映射未处理时兜底）
            if (!string.IsNullOrEmpty(elem.color) && visuals.NodeColor == null)
            {
                visuals.NodeColor = ParseColor(elem.color);
            }

            // Sprite / marker 语义，供自动切图匹配与 Image.sprite 应用使用。
            visuals.AssetMarker = elem.asset;
            visuals.SpriteHint = elem.spriteHint;
            visuals.RegionId = elem.regionId;
            visuals.SpriteGuid = elem.assetGuid;
            visuals.SpriteName = elem.spriteName;
            visuals.SpriteRootFolder = _spriteRootFolder;
            visuals.UseSlicedImage = elem.nineSlice;
            if (!string.IsNullOrEmpty(elem.marker))
            {
                visuals.AssetMarker = elem.marker;
            }

            // 文本属性兜底
            string textContent = GetTextContent(elem);
            if (!string.IsNullOrEmpty(textContent) && visuals.TextContent == null)
            {
                visuals.TextContent = textContent;
            }

            if (elem.fontSize > 0 && visuals.FontSize == 0)
            {
                visuals.FontSize = elem.fontSize;
            }

            ApplyTextAlignment(elem, visuals);
        }

        /// <summary>
        /// 提取 assetKind / alphaMode / crop padding 等 JSON-driven 切图语义。
        /// </summary>
        private void ApplyAssetSemantics(UiElement elem, UiNodeVisuals visuals)
        {
            if (elem == null || visuals == null)
                return;

            if (TryParseAssetKind(elem.assetKind, out DraftAssetKind assetKind))
            {
                visuals.AssetKind = assetKind;
                visuals.HasAssetKind = true;
            }
            else if (!string.IsNullOrWhiteSpace(elem.assetKind))
            {
                Report.AddWarning($"未识别的 assetKind '{elem.assetKind}'，将由后续切图阶段推断。");
            }

            if (TryParseAlphaMode(elem.alphaMode, out DraftAlphaMode alphaMode))
            {
                visuals.AlphaMode = alphaMode;
                visuals.HasAlphaMode = true;
            }
            else if (!string.IsNullOrWhiteSpace(elem.alphaMode))
            {
                Report.AddWarning($"未识别的 alphaMode '{elem.alphaMode}'，将由后续切图阶段推断。");
            }

            if (elem.paddingPixels > 0)
            {
                visuals.PaddingPixels = elem.paddingPixels;
                visuals.HasPaddingPixels = true;
            }

            if (elem.paddingPercent > 0f)
            {
                visuals.PaddingPercent = Mathf.Clamp01(elem.paddingPercent);
                visuals.HasPaddingPercent = true;
            }

            if (elem.makeSquare)
            {
                visuals.MakeSquare = true;
                visuals.HasMakeSquare = true;
            }

            if (elem.requiresTransparentAlpha)
            {
                visuals.RequiresTransparentAlpha = true;
                visuals.HasRequiresTransparentAlpha = true;
            }
        }

        /// <summary>
        /// 解析资产语义枚举，忽略大小写、空格、连字符和下划线。
        /// </summary>
        private static bool TryParseAssetKind(string value, out DraftAssetKind assetKind)
        {
            assetKind = DraftAssetKind.Unknown;
            string normalized = NormalizeEnumToken(value);
            if (string.IsNullOrEmpty(normalized))
                return false;

            switch (normalized)
            {
                case "background":
                case "bg":
                    assetKind = DraftAssetKind.Background;
                    return true;
                case "panel":
                    assetKind = DraftAssetKind.Panel;
                    return true;
                case "button":
                case "buttonplate":
                    assetKind = DraftAssetKind.Button;
                    return true;
                case "icon":
                    assetKind = DraftAssetKind.Icon;
                    return true;
                case "decoration":
                case "decor":
                case "ornament":
                    assetKind = DraftAssetKind.Decoration;
                    return true;
                case "composite":
                    assetKind = DraftAssetKind.Composite;
                    return true;
                case "unknown":
                    assetKind = DraftAssetKind.Unknown;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 解析 alpha 输出策略枚举。
        /// </summary>
        private static bool TryParseAlphaMode(string value, out DraftAlphaMode alphaMode)
        {
            alphaMode = DraftAlphaMode.OpaqueRect;
            string normalized = NormalizeEnumToken(value);
            if (string.IsNullOrEmpty(normalized))
                return false;

            switch (normalized)
            {
                case "opaquerect":
                case "opaque":
                case "rect":
                    alphaMode = DraftAlphaMode.OpaqueRect;
                    return true;
                case "transparentforeground":
                case "transparent":
                case "foreground":
                    alphaMode = DraftAlphaMode.TransparentForeground;
                    return true;
                case "preservesourcealpha":
                case "preservealpha":
                    alphaMode = DraftAlphaMode.PreserveSourceAlpha;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 规范化 JSON 枚举文本。
        /// </summary>
        private static string NormalizeEnumToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }

        /// <summary>
        /// 兼容 AI2UI 默认输出的 textContent 字段和旧版 text 字段。
        /// </summary>
        private static string GetTextContent(UiElement elem)
        {
            if (elem == null)
                return null;

            return !string.IsNullOrEmpty(elem.textContent)
                ? elem.textContent
                : elem.text;
        }

        /// <summary>
        /// 将文本水平/垂直对齐字段解析为 UGUI TextAnchor。
        /// </summary>
        private void ApplyTextAlignment(UiElement elem, UiNodeVisuals visuals)
        {
            if (elem == null || visuals == null)
                return;

            string horizontal = !string.IsNullOrEmpty(elem.alignment) ? elem.alignment : elem.align;
            string vertical = !string.IsNullOrEmpty(elem.textVAlign) ? elem.textVAlign : elem.vAlign;

            if (string.IsNullOrEmpty(horizontal) && string.IsNullOrEmpty(vertical))
                return;

            visuals.TextAlignment = ParseTextAlignment(horizontal, vertical);
            visuals.HasTextAlignment = true;
        }

        /// <summary>
        /// 解析文本框内对齐方式。
        /// </summary>
        private TextAnchor ParseTextAlignment(string horizontal, string vertical)
        {
            int column = ParseTextAlignmentColumn(horizontal);
            int row = ParseTextAlignmentRow(vertical);

            return (TextAnchor)(row * 3 + column);
        }

        /// <summary>
        /// 解析文本水平对齐列：left/center/right → 0/1/2。
        /// </summary>
        private int ParseTextAlignmentColumn(string horizontal)
        {
            switch ((horizontal ?? string.Empty).ToLowerInvariant())
            {
                case "center":
                case "middle":
                    return 1;
                case "right":
                case "end":
                    return 2;
                case "left":
                case "start":
                case "":
                    return 0;
                default:
                    Report.AddWarning($"未知的文本水平对齐 '{horizontal}'，使用 left。");
                    return 0;
            }
        }

        /// <summary>
        /// 解析文本垂直对齐行：top/middle/bottom → 0/1/2。
        /// </summary>
        private int ParseTextAlignmentRow(string vertical)
        {
            switch ((vertical ?? string.Empty).ToLowerInvariant())
            {
                case "middle":
                case "center":
                    return 1;
                case "bottom":
                case "end":
                    return 2;
                case "top":
                case "start":
                case "":
                    return 0;
                default:
                    Report.AddWarning($"未知的文本垂直对齐 '{vertical}'，使用 top。");
                    return 0;
            }
        }

        // ─────────────────────────── 布局组信息 ───────────────────────────

        /// <summary>
        /// 从 <see cref="UiElement"/> 构建 <see cref="LayoutGroupInfo"/>。
        /// <para>如果子节点已经提供显式 position，则仅保留 layout 语义，不添加 Unity LayoutGroup 组件。</para>
        /// </summary>
        /// <param name="elem">UI 元素。</param>
        /// <returns>布局组信息实例。</returns>
        private LayoutGroupInfo BuildLayoutGroupInfo(UiElement elem)
        {
            var layout = elem.layout;
            var info = new LayoutGroupInfo
            {
                UseUnityLayoutGroup = !HasPositionedChildren(elem)
            };

            // 布局类型
            switch (layout.type)
            {
                case "row":
                    info.GroupType = LayoutGroupType.Horizontal;
                    break;
                case "column":
                    info.GroupType = LayoutGroupType.Vertical;
                    break;
                default:
                    info.GroupType = LayoutGroupType.None;
                    Report.AddWarning($"未识别的布局类型 '{layout.type}'，默认为 None。");
                    break;
            }

            // 间距
            if (layout.spacing != null)
            {
                info.Spacing = layout.spacing.ToString();
            }

            // 内边距
            if (layout.padding != null)
            {
                info.Padding = new Vector2(layout.padding.x, layout.padding.y);
            }

            // 子元素对齐
            string childAlign = ResolveChildAlignment(layout.align, layout.vAlign);
            info.ChildAlignment = childAlign;

            return info;
        }

        /// <summary>
        /// 判断元素子节点是否包含显式源图坐标。
        /// </summary>
        /// <param name="elem">UI 元素。</param>
        /// <returns>任一子节点包含 position 时返回 true。</returns>
        private static bool HasPositionedChildren(UiElement elem)
        {
            if (elem?.children == null)
                return false;

            foreach (var child in elem.children)
            {
                if (child?.position != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 根据布局的 align 和 vAlign 综合判断子元素对齐方式。
        /// <para>
        /// 返回值：start / center / end / middle。
        /// 默认为 start。
        /// </para>
        /// </summary>
        /// <param name="align">水平对齐。</param>
        /// <param name="vAlign">垂直对齐。</param>
        /// <returns>子元素对齐方式字符串。</returns>
        private string ResolveChildAlignment(string align, string vAlign)
        {
            // 优先使用 align，其次 vAlign
            if (!string.IsNullOrEmpty(align))
            {
                switch (align)
                {
                    case "left":
                    case "start":
                        return "start";
                    case "center":
                    case "middle":
                        return "center";
                    case "right":
                    case "end":
                        return "end";
                }
            }

            if (!string.IsNullOrEmpty(vAlign))
            {
                switch (vAlign)
                {
                    case "top":
                    case "start":
                        return "start";
                    case "middle":
                    case "center":
                        return "middle";
                    case "bottom":
                    case "end":
                        return "end";
                }
            }

            return "start";
        }

        // ─────────────────────────── 名称处理 ───────────────────────────

        /// <summary>
        /// 解决同级兄弟节点中的名称冲突。
        /// <para>
        /// 如果 <paramref name="name"/> 已存在于 <paramref name="siblingNames"/> 中，
        /// 依次尝试追加 _1、_2、... 直到找到不冲突的名称。
        /// 冲突解决后会记录到 <see cref="Report"/>。
        /// </para>
        /// </summary>
        /// <param name="name">原始名称。</param>
        /// <param name="siblingNames">同级兄弟节点名称集合。</param>
        /// <returns>最终确定的不冲突名称。</returns>
        private string ResolveName(string name, HashSet<string> siblingNames)
        {
            if (!siblingNames.Contains(name))
            {
                siblingNames.Add(name);
                return name;
            }

            // 追加数字后缀解决冲突
            int suffix = 1;
            string candidate;
            do
            {
                candidate = $"{name}_{suffix}";
                suffix++;
            } while (siblingNames.Contains(candidate));

            siblingNames.Add(candidate);
            Report.AddRename(name, candidate);
            return candidate;
        }

        // ─────────────────────────── 路径计算 ───────────────────────────

        /// <summary>
        /// 计算当前元素在层级中的完整路径。
        /// <para>
        /// 如果 <paramref name="currentParentPath"/> 为空，则直接返回 <paramref name="elementName"/>；
        /// 否则返回 "parentPath/elementName" 格式的路径。
        /// </para>
        /// </summary>
        /// <param name="currentParentPath">当前父路径。</param>
        /// <param name="elementName">当前元素名称。</param>
        /// <returns>拼接后的层级路径。</returns>
        private static string ComputeParentPath(string currentParentPath, string elementName)
        {
            if (string.IsNullOrEmpty(currentParentPath))
                return elementName;

            return currentParentPath + "/" + elementName;
        }

        // ─────────────────────────── 颜色解析 ───────────────────────────

        /// <summary>
        /// 将十六进制颜色字符串解析为 Unity <see cref="Color"/>。
        /// <para>
        /// 支持格式：
        /// <list type="bullet">
        ///   <item>#RRGGBB</item>
        ///   <item>RRGGBB（无 # 前缀）</item>
        ///   <item>#RRGGBBAA（含透明度）</item>
        /// </list>
        /// 解析失败时返回 <see cref="Color.white"/> 并记录警告。
        /// </para>
        /// </summary>
        /// <param name="hex">十六进制颜色字符串。</param>
        /// <returns>解析后的 Unity Color。</returns>
        private Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                Report.AddWarning("颜色值为空，使用白色。");
                return Color.white;
            }

            // 去掉 # 前缀
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
            }

            try
            {
                if (hex.Length == 6)
                {
                    // #RRGGBB
                    byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    return new Color32(r, g, b, 255);
                }

                if (hex.Length == 8)
                {
                    // #RRGGBBAA
                    byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    byte a = System.Convert.ToByte(hex.Substring(6, 2), 16);
                    return new Color32(r, g, b, a);
                }

                Report.AddWarning($"颜色值 '{hex}' 格式不正确（期望 6 或 8 位十六进制），使用白色。");
                return Color.white;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentOutOfRangeException)
            {
                Report.AddWarning($"颜色值 '{hex}' 解析失败: {ex.Message}，使用白色。");
                return Color.white;
            }
        }
    }
}

#endif
