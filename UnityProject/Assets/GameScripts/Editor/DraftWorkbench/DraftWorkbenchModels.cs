#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// 草稿叠加层适配模式。
    /// </summary>
    public enum OverlayFitMode
    {
        /// <summary>
        /// 等比缩放并裁剪填满目标区域。
        /// </summary>
        Fill,

        /// <summary>
        /// 等比缩放，宽度适配。
        /// </summary>
        FitWidth,

        /// <summary>
        /// 等比缩放，高度适配。
        /// </summary>
        FitHeight,

        /// <summary>
        /// 拉伸至目标区域完整尺寸（不保持比例）。
        /// </summary>
        Stretch
    }

    /// <summary>
    /// 草稿叠加层显示设置。
    /// </summary>
    [Serializable]
    public class DraftOverlaySettings
    {
        /// <summary>
        /// 叠加层不透明度，范围 0~1。
        /// </summary>
        public float Opacity = 0.5f;

        /// <summary>
        /// 叠加层适配模式。
        /// </summary>
        public OverlayFitMode FitMode = OverlayFitMode.FitWidth;

        /// <summary>
        /// 画布参考分辨率，用于计算叠加层缩放。
        /// </summary>
        public Vector2 CanvasReferenceResolution = new Vector2(1920f, 1080f);
    }

    /// <summary>
    /// 草稿应用操作记录，用于撤销与审计。
    /// </summary>
    [Serializable]
    public class DraftApplyRecord
    {
        /// <summary>
        /// 记录的唯一标识。
        /// </summary>
        public string RecordGuid;

        /// <summary>
        /// 应用操作的时间戳。
        /// </summary>
        public DateTime AppliedAt;

        /// <summary>
        /// 本次操作创建的 GameObject 层级路径列表。
        /// </summary>
        public List<string> CreatedObjectPaths = new List<string>();

        /// <summary>
        /// 本次操作修改的 RectTransform 变更记录。
        /// </summary>
        public List<RectChangeRecord> ModifiedRects = new List<RectChangeRecord>();

        /// <summary>
        /// 本次操作新增的 ReferenceCollector key 列表。
        /// </summary>
        public List<string> AddedCollectorKeys = new List<string>();

        /// <summary>
        /// 本次操作新增的脚本字段名列表。
        /// </summary>
        public List<string> AddedScriptFields = new List<string>();

        /// <summary>
        /// 本次操作修改的 Image.sprite 变更记录。
        /// </summary>
        public List<SpriteChangeRecord> SpriteChanges = new List<SpriteChangeRecord>();
    }

    /// <summary>
    /// 单个 RectTransform 的变更前后快照。
    /// </summary>
    [Serializable]
    public class RectChangeRecord
    {
        /// <summary>
        /// GameObject 在层级中的路径。
        /// </summary>
        public string ObjectPath;

        /// <summary>
        /// 变更前的锚点位置。
        /// </summary>
        public Vector2 OldAnchoredPosition;

        /// <summary>
        /// 变更后的锚点位置。
        /// </summary>
        public Vector2 NewAnchoredPosition;

        /// <summary>
        /// 变更前的尺寸增量。
        /// </summary>
        public Vector2 OldSizeDelta;

        /// <summary>
        /// 变更后的尺寸增量。
        /// </summary>
        public Vector2 NewSizeDelta;

        /// <summary>
        /// 变更前的最小锚点。
        /// </summary>
        public Vector2 OldAnchorMin;

        /// <summary>
        /// 变更后的最小锚点。
        /// </summary>
        public Vector2 NewAnchorMin;

        /// <summary>
        /// 变更前的最大锚点。
        /// </summary>
        public Vector2 OldAnchorMax;

        /// <summary>
        /// 变更后的最大锚点。
        /// </summary>
        public Vector2 NewAnchorMax;

        /// <summary>
        /// 是否记录 pivot 变更。旧版本记录未包含 pivot 时保持 false。
        /// </summary>
        public bool HasPivot;

        /// <summary>
        /// 变更前的 pivot。
        /// </summary>
        public Vector2 OldPivot;

        /// <summary>
        /// 变更后的 pivot。
        /// </summary>
        public Vector2 NewPivot;
    }

    /// <summary>
    /// 单个 Image.sprite 的变更前后快照。
    /// </summary>
    [Serializable]
    public class SpriteChangeRecord
    {
        /// <summary>
        /// GameObject 在层级中的路径。
        /// </summary>
        public string ObjectPath;

        /// <summary>
        /// 变更前的 Sprite GUID。
        /// </summary>
        public string OldSpriteGuid;

        /// <summary>
        /// 变更前的 Sprite 资源路径。
        /// </summary>
        public string OldSpriteAssetPath;

        /// <summary>
        /// 变更后的 Sprite GUID。
        /// </summary>
        public string NewSpriteGuid;

        /// <summary>
        /// 变更后的 Sprite 资源路径。
        /// </summary>
        public string NewSpriteAssetPath;

        /// <summary>
        /// 来源分割区域 ID。
        /// </summary>
        public string RegionId;

        /// <summary>
        /// 来源 marker。
        /// </summary>
        public string Marker;
    }

    /// <summary>
    /// 绑定操作结果报告，汇总新增、跳过和冲突条目。
    /// </summary>
    [Serializable]
    public class DraftBindingReport
    {
        /// <summary>
        /// 成功新增的绑定条目。
        /// </summary>
        public List<BindingReportEntry> AddedEntries = new List<BindingReportEntry>();

        /// <summary>
        /// 跳过的绑定条目。
        /// </summary>
        public List<BindingReportEntry> SkippedEntries = new List<BindingReportEntry>();

        /// <summary>
        /// 冲突的绑定条目。
        /// </summary>
        public List<BindingReportEntry> ConflictEntries = new List<BindingReportEntry>();

        /// <summary>
        /// 新增的脚本字段名列表。
        /// </summary>
        public List<string> AddedScriptFields = new List<string>();

        /// <summary>
        /// 缺少组件的警告信息列表。
        /// </summary>
        public List<string> MissingComponentWarnings = new List<string>();
    }

    /// <summary>
    /// 单条绑定报告条目，记录 key、目标对象、组件类型及原因。
    /// </summary>
    [Serializable]
    public class BindingReportEntry
    {
        /// <summary>
        /// 绑定的 ReferenceCollector key。
        /// </summary>
        public string Key;

        /// <summary>
        /// 目标 GameObject 的层级路径。
        /// </summary>
        public string ObjectPath;

        /// <summary>
        /// 绑定的组件类型全名。
        /// </summary>
        public string ComponentType;

        /// <summary>
        /// 跳过或冲突的原因说明。
        /// </summary>
        public string Reason;
    }

    /// <summary>
    /// 布局组件类型枚举，标识 UGUI 布局容器的排列方式。
    /// </summary>
    public enum LayoutGroupType
    {
        /// <summary>
        /// 无布局组。
        /// </summary>
        None = 0,

        /// <summary>
        /// 水平布局（HorizontalLayoutGroup）。
        /// </summary>
        Horizontal = 1,

        /// <summary>
        /// 垂直布局（VerticalLayoutGroup）。
        /// </summary>
        Vertical = 2
    }

    /// <summary>
    /// JSON-driven 切图的资产语义分类。
    /// </summary>
    public enum DraftAssetKind
    {
        Unknown = 0,
        Background = 1,
        Panel = 2,
        Button = 3,
        Icon = 4,
        Decoration = 5,
        Composite = 6
    }

    /// <summary>
    /// JSON-driven 切图的 alpha 输出策略。
    /// </summary>
    public enum DraftAlphaMode
    {
        OpaqueRect = 0,
        TransparentForeground = 1,
        PreserveSourceAlpha = 2
    }

    /// <summary>
    /// UI 节点视觉信息，记录颜色、文本内容、字体大小和不透明度。
    /// </summary>
    [Serializable]
    public class UiNodeVisuals
    {
        /// <summary>
        /// 节点颜色，用于矩形/叠加层颜色或 Image 色调。null 表示未指定。
        /// </summary>
        public Color? NodeColor;

        /// <summary>
        /// 文本内容，仅对 Text 元素有效。null 表示非文本元素。
        /// </summary>
        public string TextContent;

        /// <summary>
        /// 字体大小，仅对 Text 元素有效。0 表示使用默认值。
        /// </summary>
        public int FontSize;

        /// <summary>
        /// 不透明度，用于叠加层/矩形元素。null 表示未指定。
        /// </summary>
        public float? Opacity;

        /// <summary>
        /// 是否指定文本框内对齐方式。
        /// </summary>
        public bool HasTextAlignment;

        /// <summary>
        /// 文本框内对齐方式，仅对 Text 元素有效。
        /// </summary>
        public TextAnchor TextAlignment;

        /// <summary>
        /// 目标 Sprite 的资源路径。为空时不修改 Image.sprite。
        /// </summary>
        public string SpriteAssetPath;

        /// <summary>
        /// 目标 Sprite 的 GUID。
        /// </summary>
        public string SpriteGuid;

        /// <summary>
        /// 多 Sprite 贴图中的子 Sprite 名称。
        /// </summary>
        public string SpriteName;

        /// <summary>
        /// 从 Image-To-UI 顶层 unity 配置继承的 Sprite 根目录。
        /// </summary>
        public string SpriteRootFolder;

        /// <summary>
        /// 是否使用 Sprite 已配置的九宫格切片。
        /// </summary>
        public bool UseSlicedImage;

        /// <summary>
        /// AI 或分割系统给出的资源标记。
        /// </summary>
        public string AssetMarker;

        /// <summary>
        /// AI 或分割系统给出的 Sprite 匹配提示。
        /// </summary>
        public string SpriteHint;

        /// <summary>
        /// 来源分割区域 ID。
        /// </summary>
        public string RegionId;

        /// <summary>
        /// JSON 显式给出的资产语义分类。
        /// </summary>
        public DraftAssetKind AssetKind = DraftAssetKind.Unknown;

        /// <summary>
        /// 是否显式指定了资产语义分类。
        /// </summary>
        public bool HasAssetKind;

        /// <summary>
        /// JSON 显式给出的 alpha 输出策略。
        /// </summary>
        public DraftAlphaMode AlphaMode = DraftAlphaMode.OpaqueRect;

        /// <summary>
        /// 是否显式指定了 alpha 输出策略。
        /// </summary>
        public bool HasAlphaMode;

        /// <summary>
        /// JSON 显式给出的固定裁剪留白像素。
        /// </summary>
        public int PaddingPixels;

        /// <summary>
        /// 是否显式指定了固定裁剪留白像素。
        /// </summary>
        public bool HasPaddingPixels;

        /// <summary>
        /// JSON 显式给出的按最大边比例裁剪留白。
        /// </summary>
        public float PaddingPercent;

        /// <summary>
        /// 是否显式指定了比例裁剪留白。
        /// </summary>
        public bool HasPaddingPercent;

        /// <summary>
        /// 是否把透明前景 raw crop 放到方形透明画布。
        /// </summary>
        public bool MakeSquare;

        /// <summary>
        /// 是否显式指定方形画布策略。
        /// </summary>
        public bool HasMakeSquare;

        /// <summary>
        /// 是否要求 refined 输出必须包含透明 alpha。
        /// </summary>
        public bool RequiresTransparentAlpha;

        /// <summary>
        /// 是否显式指定透明 alpha 校验要求。
        /// </summary>
        public bool HasRequiresTransparentAlpha;
    }

    /// <summary>
    /// 布局组信息，描述 UGUI 布局容器的排列参数。
    /// </summary>
    [Serializable]
    public class LayoutGroupInfo
    {
        /// <summary>
        /// 布局组类型。
        /// </summary>
        public LayoutGroupType GroupType = LayoutGroupType.None;

        /// <summary>
        /// 子元素间距，可以是数字字符串或 "even"（均匀分布）。
        /// </summary>
        public string Spacing = "0";

        /// <summary>
        /// 布局区域内边距。
        /// </summary>
        public Vector2 Padding = Vector2.zero;

        /// <summary>
        /// 子元素对齐方式，可选值：start / center / end / middle。
        /// </summary>
        public string ChildAlignment = "start";

        /// <summary>
        /// 是否真实添加 Unity LayoutGroup 组件。
        /// <para>当 AI2UI 子节点已经提供显式 position 时，仅保留 layout 语义，不让 LayoutGroup 接管坐标。</para>
        /// </summary>
        public bool UseUnityLayoutGroup = true;
    }

    /// <summary>
    /// 转换操作报告，汇总转换过程中的警告和重命名记录。
    /// </summary>
    [Serializable]
    public class ConversionReport
    {
        /// <summary>
        /// 转换过程中产生的警告信息列表。
        /// </summary>
        public List<string> Warnings = new List<string>();

        /// <summary>
        /// 元素重命名记录列表。
        /// </summary>
        public List<string> RenamedElements = new List<string>();

        /// <summary>
        /// 添加一条警告信息。
        /// </summary>
        /// <param name="warning">警告内容。</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// 添加一条元素重命名记录。
        /// </summary>
        /// <param name="oldName">原始名称。</param>
        /// <param name="newName">新名称。</param>
        public void AddRename(string oldName, string newName)
        {
            RenamedElements.Add($"{oldName} -> {newName}");
        }
    }

    /// <summary>
    /// 结构框预览布局计算工具，负责 pane 自适配、手动缩放和 source size 回退策略。
    /// </summary>
    internal static class StructurePreviewLayoutUtility
    {
        /// <summary>
        /// 根据预览 pane 尺寸、原始 source 尺寸和手动缩放倍率计算绘制结果。
        /// </summary>
        /// <param name="paneSize">预览 pane 可用宽高。</param>
        /// <param name="sourceSize">原始设计图或 source canvas 宽高。</param>
        /// <param name="zoomMultiplier">相对自动适配结果的缩放倍率，1 表示 Fit。</param>
        /// <returns>可用于绘制和滚动区域布局的纯数据。</returns>
        public static StructurePreviewLayoutResult CalculateLayout(
            Vector2 paneSize,
            Vector2 sourceSize,
            float zoomMultiplier)
        {
            if (paneSize.x <= 0f || paneSize.y <= 0f || sourceSize.x <= 0f || sourceSize.y <= 0f)
                return default;

            float fitScale = Mathf.Min(paneSize.x / sourceSize.x, paneSize.y / sourceSize.y);
            if (fitScale <= 0f || float.IsNaN(fitScale) || float.IsInfinity(fitScale))
                return default;

            float safeZoom = Mathf.Max(0.01f, zoomMultiplier);
            float scale = fitScale * safeZoom;
            float imageWidth = Mathf.Max(1f, sourceSize.x * scale);
            float imageHeight = Mathf.Max(1f, sourceSize.y * scale);

            return new StructurePreviewLayoutResult
            {
                IsValid = true,
                Scale = scale,
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                ContentWidth = Mathf.Max(paneSize.x, imageWidth),
                ContentHeight = Mathf.Max(paneSize.y, imageHeight)
            };
        }

        /// <summary>
        /// 解析结构框预览的原始坐标系尺寸：优先使用当前设计图尺寸，其次节点 source canvas，最后回退到当前画布尺寸。
        /// </summary>
        /// <param name="drawableChanges">包含可绘制 bounds 的节点列表。</param>
        /// <param name="draftImageSize">当前设计图尺寸，没有设计图时传 Vector2.zero。</param>
        /// <param name="fallbackCanvasSize">最终回退的画布尺寸。</param>
        /// <returns>用于结构框换算的 source size。</returns>
        public static Vector2 ResolveSourceSize(
            IReadOnlyList<UguiNodeChange> drawableChanges,
            Vector2 draftImageSize,
            Vector2 fallbackCanvasSize)
        {
            if (draftImageSize.x > 0f && draftImageSize.y > 0f)
                return draftImageSize;

            if (drawableChanges != null)
            {
                foreach (var change in drawableChanges)
                {
                    Vector2 canvasSize = change?.Descriptor?.SourceCanvasSize ?? Vector2.zero;
                    if (canvasSize.x > 0f && canvasSize.y > 0f)
                        return canvasSize;
                }
            }

            return new Vector2(
                Mathf.Max(1f, fallbackCanvasSize.x),
                Mathf.Max(1f, fallbackCanvasSize.y));
        }
    }
}
#endif
