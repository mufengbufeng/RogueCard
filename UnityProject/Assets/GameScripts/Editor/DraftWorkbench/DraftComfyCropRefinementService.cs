#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// ComfyUI raw crop refinement 服务。
    /// 负责把 raw PNG crop 送进 ComfyUI，并将 refined PNG 写回本地缓存目录。
    /// </summary>
    public static class DraftComfyCropRefinementService
    {
        private static readonly Regex HistoryImagesRegex = new Regex(
            "\\\"images\\\"\\s*:\\s*\\[(?<images>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex HistoryImageObjectRegex = new Regex(
            "\\{\\s*\\\"filename\\\"\\s*:\\s*\\\"(?<filename>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"subfolder\\\"\\s*:\\s*\\\"(?<subfolder>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"type\\\"\\s*:\\s*\\\"(?<type>(?:\\\\.|[^\\\"])*)\\\"\\s*\\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// 将 manifest 中已选择的 raw crops 送入 ComfyUI refinement workflow。
        /// </summary>
        /// <param name="manifest">JSON-driven region 清单。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="transport">可选 transport，默认使用 <see cref="ComfyUiClientTransport"/>。</param>
        /// <returns>每个 crop 的 refinement 记录。</returns>
        public static async Task<List<DraftCropRefinementRecord>> RefineSelectedRawCropsAsync(
            DraftSegmentationManifest manifest,
            ComfyUiServiceConfig config,
            IComfyUiTransport transport = null)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (config == null) throw new ArgumentNullException(nameof(config));

            transport = transport ?? new ComfyUiClientTransport();
            config.Normalize();

            if (manifest.regions == null)
            {
                manifest.regions = new List<DraftSegmentRegion>();
                return new List<DraftCropRefinementRecord>();
            }

            string sourceHash = string.IsNullOrWhiteSpace(manifest.sourceHash)
                ? "json"
                : DraftImageSegmentationService.SanitizeFileName(manifest.sourceHash);
            string outputRoot = BuildOutputRoot(config, sourceHash);
            string outputAbsoluteRoot = DraftPathUtility.ToAbsolutePath(outputRoot);
            Directory.CreateDirectory(outputAbsoluteRoot);

            var records = new List<DraftCropRefinementRecord>();
            foreach (var region in manifest.regions)
            {
                if (region == null)
                    continue;

                var record = new DraftCropRefinementRecord
                {
                    RegionId = region.id,
                    Marker = region.marker,
                    RawPngPath = region.rawPngPath,
                    GenerationStatus = DraftRegionGenerationStatus.Pending
                };

                if (!region.included || region.reviewState == DraftRegionReviewState.Ignored)
                {
                    record.GenerationStatus = DraftRegionGenerationStatus.Skipped;
                    record.GenerationMessage = string.IsNullOrEmpty(region.reviewReason)
                        ? "用户忽略该 region。"
                        : region.reviewReason;
                    region.generationStatus = record.GenerationStatus;
                    region.generationMessage = record.GenerationMessage;
                    records.Add(record);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(region.rawPngPath))
                {
                    record.GenerationStatus = DraftRegionGenerationStatus.Failed;
                    record.GenerationMessage = "缺少 raw PNG 路径。";
                    region.generationStatus = record.GenerationStatus;
                    region.generationMessage = record.GenerationMessage;
                    region.requiresReview = true;
                    if (region.reviewState != DraftRegionReviewState.Ignored)
                    {
                        region.reviewState = DraftRegionReviewState.NeedsReview;
                    }
                    records.Add(record);
                    continue;
                }

                string rawAbsolutePath = DraftPathUtility.ToAbsolutePath(region.rawPngPath);
                if (!File.Exists(rawAbsolutePath))
                {
                    record.GenerationStatus = DraftRegionGenerationStatus.Failed;
                    record.GenerationMessage = "raw PNG 文件不存在。";
                    region.generationStatus = record.GenerationStatus;
                    region.generationMessage = record.GenerationMessage;
                    region.requiresReview = true;
                    if (region.reviewState != DraftRegionReviewState.Ignored)
                    {
                        region.reviewState = DraftRegionReviewState.NeedsReview;
                    }
                    records.Add(record);
                    continue;
                }

                Texture2D rawTexture = LoadTextureFromPng(rawAbsolutePath);
                try
                {
                    int rawWidth = rawTexture != null ? rawTexture.width : Mathf.Max(1, region.cropBounds != null ? region.cropBounds.width : 1);
                    int rawHeight = rawTexture != null ? rawTexture.height : Mathf.Max(1, region.cropBounds != null ? region.cropBounds.height : 1);
                    string sanitizedMarker = DraftImageSegmentationService.SanitizeMarker(region.marker, region.label, region.id);
                    string rawFileName = Path.GetFileName(rawAbsolutePath);
                    string uploadedFileName = string.IsNullOrWhiteSpace(rawFileName)
                        ? $"{DraftImageSegmentationService.SanitizeFileName(region.id)}_{sanitizedMarker}.png"
                        : rawFileName;
                    string outputPrefix = BuildOutputPrefix(sourceHash, region.id, sanitizedMarker);
                    DraftRefinementWorkflowKind workflowKind = ResolveWorkflowKind(region, config);

                    var attempts = new List<DraftCropRefinementRecord>();
                    try
                    {
                        string uploadedImageName = await transport.UploadInputImageAsync(config, rawAbsolutePath, uploadedFileName);
                        var firstAttempt = await RunRefinementAttemptAsync(
                            region,
                            config,
                            transport,
                            uploadedImageName,
                            outputRoot,
                            outputAbsoluteRoot,
                            outputPrefix,
                            workflowKind,
                            string.Empty,
                            rawWidth,
                            rawHeight);
                        attempts.Add(firstAttempt);

                        if (ShouldRunSam2Fallback(region, firstAttempt, config))
                        {
                            var fallbackAttempt = await RunRefinementAttemptAsync(
                                region,
                                config,
                                transport,
                                uploadedImageName,
                                outputRoot,
                                outputAbsoluteRoot,
                                outputPrefix + "_sam2",
                                DraftRefinementWorkflowKind.Sam2,
                                firstAttempt.GenerationMessage,
                                rawWidth,
                                rawHeight);
                            attempts.Add(fallbackAttempt);
                        }
                    }
                    catch (Exception ex)
                    {
                        attempts.Add(CreateFailedAttempt(region, workflowKind, rawAbsolutePath, ex.Message));
                    }

                    if (ShouldRunLocalTransparentFallback(region, attempts[attempts.Count - 1], config))
                    {
                        var localAttempt = RunLocalTransparentCutoutAttempt(
                            region,
                            config,
                            rawAbsolutePath,
                            outputRoot,
                            outputAbsoluteRoot,
                            outputPrefix + "_local",
                            attempts[attempts.Count - 1].GenerationMessage,
                            rawWidth,
                            rawHeight);
                        attempts.Add(localAttempt);
                    }

                    region.refinementAttempts = attempts;
                    ApplyFinalAttemptToRegion(region, attempts[attempts.Count - 1]);
                    records.AddRange(attempts);
                }
                finally
                {
                    if (rawTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(rawTexture);
                    }
                }
            }

            DraftGenerationReportStore.SaveSnapshot(manifest, records, null);
            return records;
        }

        /// <summary>
        /// 根据 region alpha 语义选择 refinement 工作流。
        /// </summary>
        private static DraftRefinementWorkflowKind ResolveWorkflowKind(DraftSegmentRegion region, ComfyUiServiceConfig config)
        {
            bool transparent = region != null
                               && (region.alphaMode == DraftAlphaMode.TransparentForeground || region.requiresTransparentAlpha);
            if (!transparent)
                return DraftRefinementWorkflowKind.Passthrough;

            return config.TransparentWorkflowKind == DraftRefinementWorkflowKind.Passthrough
                ? DraftRefinementWorkflowKind.BiRefNetRmbg
                : config.TransparentWorkflowKind;
        }

        /// <summary>
        /// 判断是否需要执行 Sam2 fallback。
        /// </summary>
        private static bool ShouldRunSam2Fallback(
            DraftSegmentRegion region,
            DraftCropRefinementRecord attempt,
            ComfyUiServiceConfig config)
        {
            if (region == null || attempt == null || config == null || !config.EnableSam2Fallback)
                return false;

            bool transparent = region.alphaMode == DraftAlphaMode.TransparentForeground || region.requiresTransparentAlpha;
            if (!transparent || attempt.WorkflowKind == DraftRefinementWorkflowKind.Sam2)
                return false;

            return attempt.GenerationStatus == DraftRegionGenerationStatus.Failed
                   || attempt.AlphaValidation.Status == DraftAlphaValidationStatus.Failed
                   || attempt.AlphaValidation.Status == DraftAlphaValidationStatus.NeedsReview;
        }

        /// <summary>
        /// 判断是否需要启用本地透明抠图兜底。
        /// </summary>
        private static bool ShouldRunLocalTransparentFallback(
            DraftSegmentRegion region,
            DraftCropRefinementRecord attempt,
            ComfyUiServiceConfig config)
        {
            if (region == null || attempt == null || config == null || !config.EnableLocalTransparentFallback)
                return false;

            bool transparent = region.alphaMode == DraftAlphaMode.TransparentForeground || region.requiresTransparentAlpha;
            if (!transparent || attempt.WorkflowKind == DraftRefinementWorkflowKind.LocalBackgroundCutout)
                return false;

            return attempt.GenerationStatus == DraftRegionGenerationStatus.Failed
                   || attempt.AlphaValidation.Status == DraftAlphaValidationStatus.Failed
                   || attempt.AlphaValidation.Status == DraftAlphaValidationStatus.NeedsReview
                   || string.IsNullOrWhiteSpace(attempt.RefinedPngPath);
        }

        /// <summary>
        /// 执行一次 refinement attempt，并返回该次尝试的完整记录。
        /// </summary>
        private static async Task<DraftCropRefinementRecord> RunRefinementAttemptAsync(
            DraftSegmentRegion region,
            ComfyUiServiceConfig config,
            IComfyUiTransport transport,
            string uploadedImageName,
            string outputRoot,
            string outputAbsoluteRoot,
            string outputPrefix,
            DraftRefinementWorkflowKind workflowKind,
            string fallbackReason,
            int rawWidth,
            int rawHeight)
        {
            var record = new DraftCropRefinementRecord
            {
                RegionId = region.id,
                Marker = region.marker,
                RawPngPath = region.rawPngPath,
                WorkflowKind = workflowKind,
                WorkflowName = GetWorkflowName(workflowKind),
                FallbackReason = fallbackReason,
                GenerationStatus = DraftRegionGenerationStatus.Pending
            };

            try
            {
                string promptJson = BuildPromptJson(workflowKind, uploadedImageName, outputPrefix, region);
                string clientId = "draft-workbench-refine-" + Guid.NewGuid().ToString("N");
                string promptId = await transport.QueuePromptAsync(config, promptJson, clientId);
                string historyJson = await transport.WaitForHistoryAsync(config, promptId);
                ComfyHistoryImage image = ExtractFirstHistoryImage(historyJson);
                if (image == null || string.IsNullOrWhiteSpace(image.FileName))
                {
                    throw new InvalidOperationException("ComfyUI refinement completed but no output image was found in history.");
                }

                byte[] refinedBytes = await transport.DownloadViewFileAsync(config, image.FileName, image.Subfolder, image.Type);
                if (refinedBytes == null || refinedBytes.Length == 0)
                {
                    throw new InvalidOperationException("ComfyUI refinement output download returned empty bytes.");
                }

                string refinedAssetPath = Path.Combine(outputRoot, outputPrefix + "_refined.png").Replace('\\', '/');
                string refinedAbsolutePath = DraftPathUtility.ToAbsolutePath(refinedAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(refinedAbsolutePath) ?? outputAbsoluteRoot);
                File.WriteAllBytes(refinedAbsolutePath, refinedBytes);

                Texture2D refinedTexture = LoadTextureFromPng(refinedAbsolutePath);
                try
                {
                    record.RefinedWidth = refinedTexture != null ? refinedTexture.width : 0;
                    record.RefinedHeight = refinedTexture != null ? refinedTexture.height : 0;
                    record.RefinedPngPath = refinedAssetPath;
                    record.AlphaValidation = DraftAlphaValidationService.ValidatePng(
                        refinedAbsolutePath,
                        region.requiresTransparentAlpha || region.alphaMode == DraftAlphaMode.TransparentForeground,
                        config);

                    bool transparentFailed = (region.requiresTransparentAlpha || region.alphaMode == DraftAlphaMode.TransparentForeground)
                                             && record.AlphaValidation.Status != DraftAlphaValidationStatus.Passed;
                    if (transparentFailed)
                    {
                        record.GenerationStatus = DraftRegionGenerationStatus.Failed;
                        record.RequiresReview = true;
                        record.GenerationMessage = record.AlphaValidation.Message;
                        record.FailureReason = record.AlphaValidation.Message;
                        return record;
                    }

                    record.GenerationStatus = DraftRegionGenerationStatus.Generated;
                    bool sizeChanged = record.RefinedWidth != rawWidth || record.RefinedHeight != rawHeight;
                    if (sizeChanged)
                    {
                        record.RequiresReview = true;
                        record.GenerationMessage = $"refined 尺寸 {record.RefinedWidth}x{record.RefinedHeight} 与 raw crop {rawWidth}x{rawHeight} 不一致。";
                    }
                    else
                    {
                        record.GenerationMessage = workflowKind == DraftRefinementWorkflowKind.Passthrough
                            ? "passthrough refinement 成功；未声明为背景移除。"
                            : "background removal refinement 成功。";
                    }
                }
                finally
                {
                    if (refinedTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(refinedTexture);
                    }
                }
            }
            catch (Exception ex)
            {
                record.GenerationStatus = DraftRegionGenerationStatus.Failed;
                record.GenerationMessage = ex.Message;
                record.FailureReason = ex.Message;
                record.RequiresReview = true;
            }

            return record;
        }

        /// <summary>
        /// 执行本地透明背景抠除 attempt。
        /// </summary>
        private static DraftCropRefinementRecord RunLocalTransparentCutoutAttempt(
            DraftSegmentRegion region,
            ComfyUiServiceConfig config,
            string rawAbsolutePath,
            string outputRoot,
            string outputAbsoluteRoot,
            string outputPrefix,
            string fallbackReason,
            int rawWidth,
            int rawHeight)
        {
            var record = new DraftCropRefinementRecord
            {
                RegionId = region.id,
                Marker = region.marker,
                RawPngPath = region.rawPngPath,
                WorkflowKind = DraftRefinementWorkflowKind.LocalBackgroundCutout,
                WorkflowName = GetWorkflowName(DraftRefinementWorkflowKind.LocalBackgroundCutout),
                FallbackReason = fallbackReason,
                GenerationStatus = DraftRegionGenerationStatus.Pending,
                RefinedWidth = rawWidth,
                RefinedHeight = rawHeight
            };

            string refinedAssetPath = Path.Combine(outputRoot, outputPrefix + "_refined.png").Replace('\\', '/');
            string refinedAbsolutePath = DraftPathUtility.ToAbsolutePath(refinedAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(refinedAbsolutePath) ?? outputAbsoluteRoot);

            DraftAlphaValidationResult validation;
            string message;
            bool success = DraftLocalTransparentCutoutService.TryCreateCutoutPng(
                rawAbsolutePath,
                refinedAbsolutePath,
                config,
                out validation,
                out message);

            record.AlphaValidation = validation ?? new DraftAlphaValidationResult();
            record.RefinedPngPath = success ? refinedAssetPath : string.Empty;
            record.GenerationMessage = message;
            if (success)
            {
                record.GenerationStatus = DraftRegionGenerationStatus.Generated;
                return record;
            }

            record.GenerationStatus = DraftRegionGenerationStatus.Failed;
            record.RequiresReview = true;
            record.FailureReason = message;
            return record;
        }

        /// <summary>
        /// 创建远端工作流异常时的失败 attempt 记录。
        /// </summary>
        private static DraftCropRefinementRecord CreateFailedAttempt(
            DraftSegmentRegion region,
            DraftRefinementWorkflowKind workflowKind,
            string rawAbsolutePath,
            string message)
        {
            return new DraftCropRefinementRecord
            {
                RegionId = region.id,
                Marker = region.marker,
                RawPngPath = region.rawPngPath,
                WorkflowKind = workflowKind,
                WorkflowName = GetWorkflowName(workflowKind),
                GenerationStatus = DraftRegionGenerationStatus.Failed,
                GenerationMessage = string.IsNullOrWhiteSpace(message)
                    ? "远端 refinement 失败。"
                    : message,
                FailureReason = string.IsNullOrWhiteSpace(message)
                    ? "远端 refinement 失败。"
                    : message,
                RequiresReview = true,
                RefinedWidth = 0,
                RefinedHeight = 0,
                AlphaValidation = DraftAlphaValidationService.ValidatePng(rawAbsolutePath, true, null)
            };
        }

        /// <summary>
        /// 把最终 attempt 回写到 region。
        /// </summary>
        private static void ApplyFinalAttemptToRegion(DraftSegmentRegion region, DraftCropRefinementRecord finalAttempt)
        {
            if (region == null || finalAttempt == null)
                return;

            region.refinedPngPath = finalAttempt.RefinedPngPath;
            region.refinedWidth = finalAttempt.RefinedWidth;
            region.refinedHeight = finalAttempt.RefinedHeight;
            region.alphaValidation = finalAttempt.AlphaValidation ?? new DraftAlphaValidationResult();
            region.generationStatus = finalAttempt.GenerationStatus;
            region.generationMessage = finalAttempt.GenerationMessage;

            if (finalAttempt.RequiresReview || finalAttempt.GenerationStatus == DraftRegionGenerationStatus.Failed)
            {
                region.requiresReview = true;
                if (region.reviewState != DraftRegionReviewState.Ignored)
                {
                    region.reviewState = DraftRegionReviewState.NeedsReview;
                }

                if (string.IsNullOrWhiteSpace(region.reviewReason))
                {
                    region.reviewReason = finalAttempt.GenerationMessage;
                }
            }
            else if (region.reviewState == DraftRegionReviewState.Pending)
            {
                region.reviewState = DraftRegionReviewState.Included;
            }
        }

        /// <summary>
        /// 构建指定工作流的 ComfyUI prompt。
        /// </summary>
        private static string BuildPromptJson(
            DraftRefinementWorkflowKind workflowKind,
            string uploadedImageName,
            string outputPrefix,
            DraftSegmentRegion region)
        {
            switch (workflowKind)
            {
                case DraftRefinementWorkflowKind.BiRefNetRmbg:
                    return ComfyWorkflowAdapter.BuildTransparentRefinementApiPromptJson(uploadedImageName, outputPrefix);
                case DraftRefinementWorkflowKind.Sam2:
                    return ComfyWorkflowAdapter.BuildSam2RefinementApiPromptJson(uploadedImageName, outputPrefix, region);
                case DraftRefinementWorkflowKind.LocalBackgroundCutout:
                    return ComfyWorkflowAdapter.BuildRefinementApiPromptJson(uploadedImageName, outputPrefix);
                case DraftRefinementWorkflowKind.Passthrough:
                default:
                    return ComfyWorkflowAdapter.BuildRefinementApiPromptJson(uploadedImageName, outputPrefix);
            }
        }

        /// <summary>
        /// 工作流显示名称。
        /// </summary>
        private static string GetWorkflowName(DraftRefinementWorkflowKind workflowKind)
        {
            switch (workflowKind)
            {
                case DraftRefinementWorkflowKind.BiRefNetRmbg:
                    return "BiRefNet/RMBG";
                case DraftRefinementWorkflowKind.Sam2:
                    return "Sam2 fallback";
                case DraftRefinementWorkflowKind.LocalBackgroundCutout:
                    return "Local background cutout";
                case DraftRefinementWorkflowKind.Passthrough:
                default:
                    return "Passthrough";
            }
        }

        /// <summary>
        /// 构建 refined 输出根目录。
        /// </summary>
        private static string BuildOutputRoot(ComfyUiServiceConfig config, string sourceHash)
        {
            string root = string.IsNullOrWhiteSpace(config.CacheRoot)
                ? ComfyUiServiceConfig.DefaultCacheRoot
                : config.CacheRoot;
            return (root.TrimEnd('/') + "/" + DraftImageSegmentationService.SanitizeFileName(sourceHash) + "/refined").Replace('\\', '/');
        }

        /// <summary>
        /// 构建单个 region 的 refined 输出前缀。
        /// </summary>
        private static string BuildOutputPrefix(string sourceHash, string regionId, string marker)
        {
            return "DW_" + DraftImageSegmentationService.SanitizeFileName(sourceHash) + "_"
                   + DraftImageSegmentationService.SanitizeFileName(regionId) + "_"
                   + DraftImageSegmentationService.SanitizeFileName(marker);
        }

        /// <summary>
        /// 从 history JSON 中提取第一张输出图片。
        /// </summary>
        private static ComfyHistoryImage ExtractFirstHistoryImage(string historyJson)
        {
            if (string.IsNullOrWhiteSpace(historyJson))
                return null;

            int outputsIndex = historyJson.IndexOf("\"outputs\"", StringComparison.Ordinal);
            if (outputsIndex < 0)
                return null;

            // 只在 outputs 段里找 images，避免误命中 prompt 输入里的 images 参数。
            string outputsJson = historyJson.Substring(outputsIndex);
            Match arrayMatch = HistoryImagesRegex.Match(outputsJson);
            if (!arrayMatch.Success)
                return null;

            string imageArrayText = arrayMatch.Groups["images"].Value;
            foreach (Match imageMatch in HistoryImageObjectRegex.Matches(imageArrayText))
            {
                string fileName = DecodeJsonString(imageMatch.Groups["filename"].Value);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                return new ComfyHistoryImage
                {
                    FileName = fileName,
                    Subfolder = DecodeJsonString(imageMatch.Groups["subfolder"].Value),
                    Type = DecodeJsonString(imageMatch.Groups["type"].Value)
                };
            }

            return null;
        }

        /// <summary>
        /// 将少量 JSON 字符串转义解码为可用路径。
        /// </summary>
        private static string DecodeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\\\", "\\").Replace("\\\"", "\"").Replace("\\/", "/");
        }

        /// <summary>
        /// 从 PNG 文件读取 Texture2D，便于获取宽高。
        /// </summary>
        private static Texture2D LoadTextureFromPng(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(absolutePath)))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        /// <summary>
        /// ComfyUI history 图片记录。
        /// </summary>
        private sealed class ComfyHistoryImage
        {
            public string FileName;
            public string Subfolder;
            public string Type;
        }
    }
}

#endif
