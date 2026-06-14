#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// JSON-driven Auto Slice 的原始切图服务。
    /// 负责把已抽取的 JSON region 裁成 raw PNG，供后续 ComfyUI refinement 使用。
    /// </summary>
    public static class DraftJsonRawCropService
    {
        /// <summary>raw PNG 的默认缓存根目录。</summary>
        public const string DefaultRawCropRoot = "Library/DraftWorkbench/JsonRawCrops";

        /// <summary>
        /// 将 JSON-driven regions 裁剪成 raw PNG 文件，并更新 region 记录。
        /// </summary>
        /// <param name="sourceImage">源设计图。</param>
        /// <param name="manifest">JSON-driven region 清单。</param>
        /// <returns>更新后的 manifest。</returns>
        public static DraftSegmentationManifest GenerateRawCrops(Texture2D sourceImage, DraftSegmentationManifest manifest, ComfyUiServiceConfig config = null)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            config?.Normalize();

            if (manifest.regions == null)
            {
                manifest.regions = new List<DraftSegmentRegion>();
                return manifest;
            }

            manifest.sourceHash = string.IsNullOrWhiteSpace(manifest.sourceHash)
                ? DraftImageSegmentationService.ComputeSourceHash(sourceImage)
                : DraftImageSegmentationService.SanitizeFileName(manifest.sourceHash);

            string outputRoot = BuildOutputRoot(manifest.sourceHash);
            string outputAbsoluteRoot = DraftPathUtility.ToAbsolutePath(outputRoot);
            Directory.CreateDirectory(outputAbsoluteRoot);

            foreach (var region in manifest.regions)
            {
                if (region == null)
                    continue;

                if (!region.included || region.reviewState == DraftRegionReviewState.Ignored)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Skipped;
                    region.generationMessage = string.IsNullOrEmpty(region.reviewReason)
                        ? "用户忽略该 region。"
                        : region.reviewReason;
                    continue;
                }

                Rect baseCropRect = ResolveCropRect(region);
                Rect cropRect = ResolveExpandedCropRect(region, baseCropRect, sourceImage.width, sourceImage.height, config);
                if (cropRect.width <= 0f || cropRect.height <= 0f)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Failed;
                    region.generationMessage = "raw crop bounds 无效，跳过 raw PNG 生成。";
                    region.requiresReview = true;
                    if (region.reviewState != DraftRegionReviewState.Ignored)
                    {
                        region.reviewState = DraftRegionReviewState.NeedsReview;
                    }
                    continue;
                }

                string marker = DraftImageSegmentationService.SanitizeMarker(region.marker, region.label, region.id);
                string fileName = $"{DraftImageSegmentationService.SanitizeFileName(region.id)}_{marker}.png";
                string rawAssetPath = (outputRoot.TrimEnd('/') + "/" + fileName).Replace('\\', '/');
                region.rawPngPath = rawAssetPath;
                region.expandedBbox = DraftSegmentBounds.FromRect(cropRect);
                SetRawOutputMetadata(region, cropRect);

                try
                {
                    DraftSpriteCropper.CropRegionToPng(sourceImage, manifest, region, rawAssetPath, config);
                    region.generationStatus = DraftRegionGenerationStatus.Generated;
                    region.generationMessage = "raw PNG 生成成功。";
                }
                catch (Exception ex)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Failed;
                    region.generationMessage = ex.Message;
                    region.requiresReview = true;
                    if (region.reviewState != DraftRegionReviewState.Ignored)
                    {
                        region.reviewState = DraftRegionReviewState.NeedsReview;
                    }

                    Debug.LogWarning($"[DraftJsonRawCropService] region '{region.id}' raw crop failed: {ex.Message}");
                }
            }

            DraftGenerationReportStore.SaveSnapshot(manifest, null, null);
            return manifest;
        }

        /// <summary>
        /// 构造 raw PNG 输出目录。
        /// </summary>
        private static string BuildOutputRoot(string sourceHash)
        {
            return (DefaultRawCropRoot.TrimEnd('/') + "/" + DraftImageSegmentationService.SanitizeFileName(sourceHash)).Replace('\\', '/');
        }

        /// <summary>
        /// 解析本次 raw crop 使用的裁剪框，优先使用 JSON-driven cropBounds。
        /// </summary>
        private static Rect ResolveCropRect(DraftSegmentRegion region)
        {
            if (region == null)
                return Rect.zero;

            if (region.cropBounds != null && region.cropBounds.width > 0 && region.cropBounds.height > 0)
                return region.cropBounds.ToRect();

            if (region.bbox != null && region.bbox.width > 0 && region.bbox.height > 0)
                return region.bbox.ToRect();

            if (region.sourceBounds != null && region.sourceBounds.width > 0 && region.sourceBounds.height > 0)
                return region.sourceBounds.ToRect();

            return Rect.zero;
        }

        /// <summary>
        /// 根据透明前景语义或显式 padding 扩展 crop，并限制在源图范围内。
        /// </summary>
        private static Rect ResolveExpandedCropRect(
            DraftSegmentRegion region,
            Rect baseCropRect,
            int sourceWidth,
            int sourceHeight,
            ComfyUiServiceConfig config)
        {
            Rect clampedBase = ClampBounds(baseCropRect, sourceWidth, sourceHeight);
            if (clampedBase.width <= 0f || clampedBase.height <= 0f)
                return Rect.zero;

            if (region == null)
                return clampedBase;

            float maxSide = Mathf.Max(clampedBase.width, clampedBase.height);
            bool transparentForeground = region.alphaMode == DraftAlphaMode.TransparentForeground;
            bool hasExplicitPadding = region.paddingPixels > 0 || region.paddingPercent > 0f;
            if (!transparentForeground && !hasExplicitPadding)
                return clampedBase;

            int fixedPadding = region.paddingPixels > 0 ? region.paddingPixels : 0;
            float percentPadding = region.paddingPercent > 0f ? region.paddingPercent : 0f;
            if (transparentForeground && !hasExplicitPadding && config != null)
            {
                fixedPadding = config.RegionExpandPixels;
                percentPadding = config.RegionExpandPercent;
            }

            float padding = fixedPadding + maxSide * Mathf.Clamp01(percentPadding);
            if (padding <= 0f)
                return clampedBase;

            return ClampBounds(new Rect(
                clampedBase.x - padding,
                clampedBase.y - padding,
                clampedBase.width + padding * 2f,
                clampedBase.height + padding * 2f), sourceWidth, sourceHeight);
        }

        /// <summary>
        /// 记录 raw 输出尺寸和方形透明画布偏移。
        /// </summary>
        private static void SetRawOutputMetadata(DraftSegmentRegion region, Rect cropRect)
        {
            if (region == null)
                return;

            int width = Mathf.RoundToInt(cropRect.width);
            int height = Mathf.RoundToInt(cropRect.height);
            bool useSquare = region.alphaMode == DraftAlphaMode.TransparentForeground && region.makeSquare;
            int outputSize = useSquare ? Mathf.Max(width, height) : 0;
            region.rawOutputWidth = useSquare ? outputSize : width;
            region.rawOutputHeight = useSquare ? outputSize : height;
            region.rawCanvasOffsetX = useSquare ? Mathf.Max(0, (outputSize - width) / 2) : 0;
            region.rawCanvasOffsetY = useSquare ? Mathf.Max(0, (outputSize - height) / 2) : 0;
        }

        /// <summary>
        /// 将裁剪框限制在源图范围内。
        /// </summary>
        private static Rect ClampBounds(Rect bounds, int sourceWidth, int sourceHeight)
        {
            float x = Mathf.Clamp(bounds.x, 0f, Mathf.Max(0f, sourceWidth - 1f));
            float y = Mathf.Clamp(bounds.y, 0f, Mathf.Max(0f, sourceHeight - 1f));
            float maxWidth = Mathf.Max(0f, sourceWidth - x);
            float maxHeight = Mathf.Max(0f, sourceHeight - y);
            float width = Mathf.Min(bounds.width, maxWidth);
            float height = Mathf.Min(bounds.height, maxHeight);
            return new Rect(x, y, width, height);
        }
    }
}

#endif
