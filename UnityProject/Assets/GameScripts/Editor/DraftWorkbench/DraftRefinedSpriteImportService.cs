#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// refined PNG 到 Unity Sprite 资产的导入服务。
    /// </summary>
    public static class DraftRefinedSpriteImportService
    {
        /// <summary>
        /// 将 refined PNG 输出复制到 final Sprite 目录，并导入为 Single Sprite。
        /// </summary>
        /// <param name="manifest">JSON-driven region 清单。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="prefabName">目标 prefab 名称；仅用于输出目录分组。</param>
        /// <returns>导入报告。</returns>
        public static DraftSpriteImportReport ImportRefinedSprites(
            DraftSegmentationManifest manifest,
            ComfyUiServiceConfig config,
            string prefabName)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();

            if (manifest.regions == null)
            {
                manifest.regions = new List<DraftSegmentRegion>();
            }

            string sourceHash = string.IsNullOrWhiteSpace(manifest.sourceHash)
                ? "json"
                : DraftImageSegmentationService.SanitizeFileName(manifest.sourceHash);
            string safePrefabName = string.IsNullOrWhiteSpace(prefabName)
                ? "Standalone"
                : DraftImageSegmentationService.SanitizeFileName(prefabName);
            string outputRoot = BuildOutputRoot(config, safePrefabName, sourceHash);
            string outputAbsoluteRoot = DraftPathUtility.ToAbsolutePath(outputRoot);
            Directory.CreateDirectory(outputAbsoluteRoot);

            var report = new DraftSpriteImportReport
            {
                PrefabName = safePrefabName,
                HasTargetPrefab = !string.IsNullOrWhiteSpace(prefabName),
                PrefabModified = false,
                OutputRoot = outputRoot
            };

            if (!report.HasTargetPrefab)
            {
                report.Messages.Add("未选择 Target Prefab，仅生成 refined Sprite 资产，不修改 prefab。");
            }

            foreach (var region in manifest.regions.Take(config.MaxRegions))
            {
                if (region == null)
                    continue;

                if (!region.included || region.reviewState == DraftRegionReviewState.Ignored)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Skipped;
                    region.generationMessage = string.IsNullOrEmpty(region.reviewReason) ? "用户忽略该 region。" : region.reviewReason;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(region.refinedPngPath))
                {
                    region.generationStatus = DraftRegionGenerationStatus.Failed;
                    region.generationMessage = "缺少 refined PNG 路径。";
                    report.Messages.Add($"{region.id}: {region.generationMessage}");
                    continue;
                }

                string refinedAbsolutePath = DraftPathUtility.ToAbsolutePath(region.refinedPngPath);
                if (!File.Exists(refinedAbsolutePath))
                {
                    region.generationStatus = DraftRegionGenerationStatus.Failed;
                    region.generationMessage = "refined PNG 文件不存在。";
                    region.requiresReview = true;
                    if (region.reviewState != DraftRegionReviewState.Ignored)
                    {
                        region.reviewState = DraftRegionReviewState.NeedsReview;
                    }
                    report.Messages.Add($"{region.id}: {region.generationMessage}");
                    continue;
                }

                bool requiresTransparentAlpha = region.requiresTransparentAlpha
                                                || region.alphaMode == DraftAlphaMode.TransparentForeground;
                region.alphaValidation = DraftAlphaValidationService.ValidatePng(refinedAbsolutePath, requiresTransparentAlpha, config);
                if (requiresTransparentAlpha && region.alphaValidation.Status != DraftAlphaValidationStatus.Passed)
                {
                    region.generationStatus = region.alphaValidation.Status == DraftAlphaValidationStatus.NeedsReview
                        ? DraftRegionGenerationStatus.Failed
                        : DraftRegionGenerationStatus.Failed;
                    region.generationMessage = "alpha validation failed: " + region.alphaValidation.Message;
                    region.requiresReview = true;
                    if (region.reviewState != DraftRegionReviewState.Ignored)
                    {
                        region.reviewState = DraftRegionReviewState.NeedsReview;
                    }

                    report.Messages.Add($"{region.id}: {region.generationMessage}; transparentRatio={region.alphaValidation.TransparentRatio:0.###}, borderOpaqueRatio={region.alphaValidation.BorderOpaqueRatio:0.###}, nonOpaquePixelRatio={region.alphaValidation.NonOpaquePixelRatio:0.###}");
                    continue;
                }

                string marker = DraftImageSegmentationService.SanitizeMarker(region.marker, region.label, region.id);
                string fileName = $"DW_{sourceHash}_{DraftImageSegmentationService.SanitizeFileName(region.id)}_{marker}.png";
                string assetPath = (outputRoot.TrimEnd('/') + "/" + fileName).Replace('\\', '/');
                string absoluteAssetPath = DraftPathUtility.ToAbsolutePath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteAssetPath) ?? outputAbsoluteRoot);

                try
                {
                    File.Copy(refinedAbsolutePath, absoluteAssetPath, true);
                    var sprite = DraftGeneratedSpriteImporter.ImportSprite(assetPath, region, config);
                    if (sprite == null)
                    {
                        region.generationStatus = DraftRegionGenerationStatus.Failed;
                        region.generationMessage = "Sprite 导入失败。";
                        report.Messages.Add($"{region.id}: {region.generationMessage}");
                        continue;
                    }

                    sprite.RefinedPngPath = region.refinedPngPath;
                    sprite.GenerationStatus = DraftRegionGenerationStatus.Generated;
                    sprite.GenerationMessage = "Refined Sprite 导入成功。";
                    sprite.AssetKind = region.assetKind;
                    sprite.AlphaMode = region.alphaMode;
                    sprite.AlphaValidation = region.alphaValidation;
                    report.GeneratedSprites.Add(sprite);

                    region.croppedAssetPath = assetPath;
                    region.spriteAssetPath = sprite.SpriteAssetPath;
                    region.spriteGuid = sprite.SpriteGuid;
                    region.generationStatus = DraftRegionGenerationStatus.Generated;
                    region.generationMessage = sprite.GenerationMessage;
                }
                catch (Exception ex)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Failed;
                    region.generationMessage = ex.Message;
                    report.Messages.Add($"{region.id}: {ex.Message}");
                }
            }

            DraftGenerationReportStore.SaveSnapshot(manifest, null, report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        /// <summary>
        /// 构建 final Sprite 输出根目录。
        /// </summary>
        private static string BuildOutputRoot(ComfyUiServiceConfig config, string prefabName, string sourceHash)
        {
            string root = string.IsNullOrWhiteSpace(config.GeneratedSpriteOutputRoot)
                ? ComfyUiServiceConfig.DefaultGeneratedSpriteOutputRoot
                : config.GeneratedSpriteOutputRoot;

            return (root.TrimEnd('/') + "/" + prefabName + "/" + sourceHash).Replace('\\', '/');
        }
    }
}

#endif
