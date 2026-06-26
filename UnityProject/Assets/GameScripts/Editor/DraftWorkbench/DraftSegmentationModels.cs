#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench 自动切图工作流阶段。
    /// </summary>
    public enum AutoSliceStage
    {
        /// <summary>尚未开始或等待输入。</summary>
        Idle,

        /// <summary>正在检查 ComfyUI 连接。</summary>
        CheckingConnection,

        /// <summary>正在通过 ComfyUI/SAM3 检测候选区域。</summary>
        Detecting,

        /// <summary>等待用户审查候选区域。</summary>
        ReviewingRegions,

        /// <summary>正在生成 PNG 并导入 Sprite。</summary>
        GeneratingSprites,

        /// <summary>等待用户审查 Sprite 与 prefab 节点匹配。</summary>
        ReviewingMatches,

        /// <summary>正在应用已批准的 Sprite 引用。</summary>
        Applying,

        /// <summary>最近一次应用已完成。</summary>
        Applied,

        /// <summary>流程失败，等待用户重试或调整配置。</summary>
        Failed
    }

    /// <summary>
    /// 分割区域的人工审查状态。
    /// </summary>
    public enum DraftRegionReviewState
    {
        /// <summary>尚未审查。</summary>
        Pending,

        /// <summary>自动通过，可参与 Sprite 生成。</summary>
        Included,

        /// <summary>已忽略，不参与 Sprite 生成。</summary>
        Ignored,

        /// <summary>需要用户人工复核。</summary>
        NeedsReview
    }

    /// <summary>
    /// 单个 region 生成 Sprite 的状态。
    /// </summary>
    public enum DraftRegionGenerationStatus
    {
        /// <summary>尚未生成。</summary>
        Pending,

        /// <summary>已跳过。</summary>
        Skipped,

        /// <summary>生成成功。</summary>
        Generated,

        /// <summary>生成失败。</summary>
        Failed
    }

    /// <summary>
    /// 自动切图 region 的来源类型。
    /// </summary>
    public enum DraftRegionSourceKind
    {
        /// <summary>来源未知或旧版 ComfyUI/SAM 输出。</summary>
        Unknown,

        /// <summary>来源为 ComfyUI/SAM manifest 或 mask fallback。</summary>
        ComfySegmentation,

        /// <summary>来源为 ui_structure.json 转换后的 descriptor。</summary>
        JsonDescriptor,

        /// <summary>来源为用户手动创建或调整的区域。</summary>
        Manual
    }

    /// <summary>
    /// ComfyUI refinement 工作流类型。
    /// </summary>
    public enum DraftRefinementWorkflowKind
    {
        Passthrough = 0,
        BiRefNetRmbg = 1,
        Sam2 = 2,
        LocalBackgroundCutout = 3
    }

    /// <summary>
    /// alpha 校验状态。
    /// </summary>
    public enum DraftAlphaValidationStatus
    {
        NotRequired = 0,
        Passed = 1,
        NeedsReview = 2,
        Failed = 3
    }

    /// <summary>
    /// refined PNG alpha 质量统计。
    /// </summary>
    [Serializable]
    public class DraftAlphaValidationResult
    {
        public DraftAlphaValidationStatus Status = DraftAlphaValidationStatus.NotRequired;
        public int MinAlpha;
        public int MaxAlpha;
        public float TransparentRatio;
        public float BorderOpaqueRatio;
        public float NonOpaquePixelRatio;
        public string Message;
    }

    /// <summary>
    /// Draft Workbench 自动切图面板的轻量状态快照。
    /// </summary>
    [Serializable]
    public class DraftAutoSliceState
    {
        /// <summary>当前工作流阶段。</summary>
        public AutoSliceStage Stage = AutoSliceStage.Idle;

        /// <summary>当前状态说明。</summary>
        public string StatusMessage;

        /// <summary>下一步主操作说明。</summary>
        public string PrimaryActionLabel;

        /// <summary>最近一次错误说明。</summary>
        public string ErrorMessage;
    }

    /// <summary>
    /// Draft Workbench 自动切图阶段的分割结果清单。
    /// </summary>
    [Serializable]
    public class DraftSegmentationManifest
    {
        /// <summary>清单格式版本。</summary>
        public int schemaVersion = 1;

        /// <summary>生成该清单的 workflow 版本。</summary>
        public string workflowVersion;

        /// <summary>源图哈希，用于缓存和输出目录隔离。</summary>
        public string sourceHash;

        /// <summary>源图宽度。</summary>
        public int sourceWidth;

        /// <summary>源图高度。</summary>
        public int sourceHeight;

        /// <summary>源图资产路径。</summary>
        public string sourceAssetPath;

        /// <summary>清单所在目录，用于解析相对 mask 路径。</summary>
        public string manifestDirectory;

        /// <summary>分割区域列表。</summary>
        public List<DraftSegmentRegion> regions = new List<DraftSegmentRegion>();
    }

    /// <summary>
    /// 单个图像分割区域，描述 bbox、mask、marker、审查状态和生成结果。
    /// </summary>
    [Serializable]
    public class DraftSegmentRegion
    {
        /// <summary>区域稳定 ID，例如 r001。</summary>
        public string id;

        /// <summary>人类可读标记，例如 icon_coin。</summary>
        public string marker;

        /// <summary>从 JSON / descriptor 传入的 Sprite 匹配提示。</summary>
        public string spriteHint;

        /// <summary>区域资产语义分类。</summary>
        public DraftAssetKind assetKind = DraftAssetKind.Unknown;

        /// <summary>区域 alpha 输出策略。</summary>
        public DraftAlphaMode alphaMode = DraftAlphaMode.OpaqueRect;

        /// <summary>固定裁剪留白像素。</summary>
        public int paddingPixels;

        /// <summary>比例裁剪留白。</summary>
        public float paddingPercent;

        /// <summary>透明前景是否输出方形画布。</summary>
        public bool makeSquare;

        /// <summary>是否要求 refined 输出包含透明 alpha。</summary>
        public bool requiresTransparentAlpha;

        /// <summary>区域语义标签。</summary>
        public string label;

        /// <summary>自动切图区域来源。</summary>
        public DraftRegionSourceKind sourceKind = DraftRegionSourceKind.ComfySegmentation;

        /// <summary>来源 descriptor 对应的 prefab 层级路径。</summary>
        public string nodePath;

        /// <summary>来源 JSON / descriptor 坐标系的画布宽度。</summary>
        public int sourceCanvasWidth;

        /// <summary>来源 JSON / descriptor 坐标系的画布高度。</summary>
        public int sourceCanvasHeight;

        /// <summary>JSON / descriptor 原始坐标系中的区域框。</summary>
        public DraftSegmentBounds sourceBounds = new DraftSegmentBounds();

        /// <summary>映射到设计图像素坐标后的裁剪区域框。</summary>
        public DraftSegmentBounds cropBounds = new DraftSegmentBounds();

        /// <summary>本地脚本生成的原始 PNG 路径。</summary>
        public string rawPngPath;

        /// <summary>ComfyUI refinement 生成的 PNG 路径。</summary>
        public string refinedPngPath;

        /// <summary>ComfyUI refinement 输出宽度。</summary>
        public int refinedWidth;

        /// <summary>ComfyUI refinement 输出高度。</summary>
        public int refinedHeight;

        /// <summary>raw PNG 输出宽度。</summary>
        public int rawOutputWidth;

        /// <summary>raw PNG 输出高度。</summary>
        public int rawOutputHeight;

        /// <summary>透明方形画布中原始裁剪内容的 X 偏移。</summary>
        public int rawCanvasOffsetX;

        /// <summary>透明方形画布中原始裁剪内容的 Y 偏移。</summary>
        public int rawCanvasOffsetY;

        /// <summary>分割置信度，范围 0~1。</summary>
        public float confidence = 1f;

        /// <summary>源图左上角坐标系中的区域框。</summary>
        public DraftSegmentBounds bbox = new DraftSegmentBounds();

        /// <summary>mask 图片路径，可为绝对路径或相对 manifest 目录路径。</summary>
        public string maskPath;

        /// <summary>是否纳入后续 Sprite 生成。</summary>
        public bool included = true;

        /// <summary>区域人工审查状态。</summary>
        public DraftRegionReviewState reviewState = DraftRegionReviewState.Included;

        /// <summary>是否需要人工复核。</summary>
        public bool requiresReview;

        /// <summary>需要复核或被跳过的原因。</summary>
        public string reviewReason;

        /// <summary>扩张后的区域框，由 Unity 本地切图阶段填充。</summary>
        public DraftSegmentBounds expandedBbox = new DraftSegmentBounds();

        /// <summary>生成的 PNG 资产路径。</summary>
        public string croppedAssetPath;

        /// <summary>导入后的 Sprite 资产路径。</summary>
        public string spriteAssetPath;

        /// <summary>导入后的 Sprite GUID。</summary>
        public string spriteGuid;

        /// <summary>该 region 的 Sprite 生成状态。</summary>
        public DraftRegionGenerationStatus generationStatus = DraftRegionGenerationStatus.Pending;

        /// <summary>生成结果或错误说明。</summary>
        public string generationMessage;

        /// <summary>最近一次 alpha 校验结果。</summary>
        public DraftAlphaValidationResult alphaValidation = new DraftAlphaValidationResult();

        /// <summary>该 region 的 refinement 尝试记录。</summary>
        public List<DraftCropRefinementRecord> refinementAttempts = new List<DraftCropRefinementRecord>();
    }

    /// <summary>
    /// 以源图左上角为原点的矩形区域。
    /// </summary>
    [Serializable]
    public class DraftSegmentBounds
    {
        /// <summary>X 坐标。</summary>
        public int x;

        /// <summary>Y 坐标。</summary>
        public int y;

        /// <summary>宽度。</summary>
        public int width;

        /// <summary>高度。</summary>
        public int height;

        /// <summary>
        /// 转换为 Unity Rect。
        /// </summary>
        /// <returns>Rect 表示。</returns>
        public Rect ToRect()
        {
            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 从 Rect 创建区域框。
        /// </summary>
        /// <param name="rect">源 Rect。</param>
        /// <returns>区域框。</returns>
        public static DraftSegmentBounds FromRect(Rect rect)
        {
            return new DraftSegmentBounds
            {
                x = Mathf.RoundToInt(rect.x),
                y = Mathf.RoundToInt(rect.y),
                width = Mathf.RoundToInt(rect.width),
                height = Mathf.RoundToInt(rect.height)
            };
        }
    }

    /// <summary>
    /// 自动切图生成的 Sprite 资产信息。
    /// </summary>
    [Serializable]
    public class DraftGeneratedSprite
    {
        /// <summary>来源 region id。</summary>
        public string RegionId;

        /// <summary>来源 marker。</summary>
        public string Marker;

        /// <summary>来源 label。</summary>
        public string Label;

        /// <summary>Sprite 资产路径。</summary>
        public string SpriteAssetPath;

        /// <summary>Sprite GUID。</summary>
        public string SpriteGuid;

        /// <summary>来源 refined PNG 路径。</summary>
        public string RefinedPngPath;

        /// <summary>原始 bbox。</summary>
        public Rect SourceBounds;

        /// <summary>扩张后的 bbox。</summary>
        public Rect ExpandedBounds;

        /// <summary>来源置信度。</summary>
        public float Confidence;

        /// <summary>生成状态。</summary>
        public DraftRegionGenerationStatus GenerationStatus = DraftRegionGenerationStatus.Generated;

        /// <summary>生成结果说明。</summary>
        public string GenerationMessage;

        /// <summary>资产语义分类。</summary>
        public DraftAssetKind AssetKind = DraftAssetKind.Unknown;

        /// <summary>alpha 输出策略。</summary>
        public DraftAlphaMode AlphaMode = DraftAlphaMode.OpaqueRect;

        /// <summary>alpha 校验结果。</summary>
        public DraftAlphaValidationResult AlphaValidation = new DraftAlphaValidationResult();
    }

    /// <summary>
    /// refined Sprite 导入结果报告。
    /// </summary>
    [Serializable]
    public class DraftSpriteImportReport
    {
        /// <summary>目标 prefab 名称；若为空则表示 asset-only 导入。</summary>
        public string PrefabName;

        /// <summary>是否提供了 target prefab。</summary>
        public bool HasTargetPrefab;

        /// <summary>本次导入是否修改了 prefab。</summary>
        public bool PrefabModified;

        /// <summary>final Sprite 输出根目录。</summary>
        public string OutputRoot;

        /// <summary>导入成功的 Sprite 列表。</summary>
        public List<DraftGeneratedSprite> GeneratedSprites = new List<DraftGeneratedSprite>();

        /// <summary>导入过程中的说明或警告。</summary>
        public List<string> Messages = new List<string>();
    }

    /// <summary>
    /// 单个 raw crop 的 ComfyUI refinement 记录。
    /// </summary>
    [Serializable]
    public class DraftCropRefinementRecord
    {
        /// <summary>来源 region id。</summary>
        public string RegionId;

        /// <summary>来源 marker。</summary>
        public string Marker;

        /// <summary>raw PNG 路径。</summary>
        public string RawPngPath;

        /// <summary>refined PNG 路径。</summary>
        public string RefinedPngPath;

        /// <summary>refined PNG 宽度。</summary>
        public int RefinedWidth;

        /// <summary>refined PNG 高度。</summary>
        public int RefinedHeight;

        /// <summary>本次尝试使用的 refinement 工作流。</summary>
        public DraftRefinementWorkflowKind WorkflowKind = DraftRefinementWorkflowKind.Passthrough;

        /// <summary>工作流显示名称。</summary>
        public string WorkflowName;

        /// <summary>进入 fallback 的原因。</summary>
        public string FallbackReason;

        /// <summary>失败原因。</summary>
        public string FailureReason;

        /// <summary>本次尝试的 alpha 校验结果。</summary>
        public DraftAlphaValidationResult AlphaValidation = new DraftAlphaValidationResult();

        /// <summary>生成状态。</summary>
        public DraftRegionGenerationStatus GenerationStatus = DraftRegionGenerationStatus.Pending;

        /// <summary>生成结果或错误说明。</summary>
        public string GenerationMessage;

        /// <summary>是否需要人工复核。</summary>
        public bool RequiresReview;
    }

    /// <summary>
    /// Sprite 到 prefab 节点的候选应用计划。
    /// </summary>
    [Serializable]
    public class DraftSpriteAssignment
    {
        /// <summary>Prefab 根节点下的目标对象路径。</summary>
        public string NodePath;

        /// <summary>目标节点名称。</summary>
        public string NodeName;

        /// <summary>来源 region id。</summary>
        public string RegionId;

        /// <summary>来源 marker。</summary>
        public string Marker;

        /// <summary>新 Sprite 资产路径。</summary>
        public string SpriteAssetPath;

        /// <summary>新 Sprite GUID。</summary>
        public string SpriteGuid;

        /// <summary>匹配置信度。</summary>
        public float Confidence;

        /// <summary>匹配原因说明。</summary>
        public string MatchReason;

        /// <summary>当前节点是否已有 Sprite。</summary>
        public bool HasExistingSprite;

        /// <summary>当前旧 Sprite 资产路径。</summary>
        public string ExistingSpriteAssetPath;

        /// <summary>当前旧 Sprite GUID。</summary>
        public string ExistingSpriteGuid;

        /// <summary>是否需要人工复核。</summary>
        public bool RequiresReview;

        /// <summary>人工复核状态。</summary>
        public DraftRegionReviewState ReviewState = DraftRegionReviewState.Pending;

        /// <summary>人工复核说明。</summary>
        public string ReviewReason;

        /// <summary>是否已批准应用。</summary>
        public bool Approved;
    }
}
#endif
