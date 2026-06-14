#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// Draft Workbench 图像分割编排服务，负责运行 ComfyUI、规范化 region，并生成 Unity Sprite 资产。
    /// </summary>
    public static class DraftImageSegmentationService
    {
        /// <summary>
        /// 通过 ComfyUI 生成分割 manifest，并完成基础规范化。
        /// </summary>
        /// <param name="sourceImage">源设计图。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="prefabName">目标 prefab 名称。</param>
        /// <returns>规范化后的分割 manifest。</returns>
        public static async Task<DraftSegmentationManifest> GenerateManifestAsync(
            Texture2D sourceImage,
            ComfyUiServiceConfig config,
            string prefabName)
        {
            var manifest = await ComfyWorkflowRunner.RunSegmentationAsync(sourceImage, config, prefabName);
            NormalizeManifest(manifest, sourceImage, config);
            return manifest;
        }

        /// <summary>
        /// 将 manifest 中的有效 region 裁剪并导入为 Unity Sprite。
        /// </summary>
        /// <param name="sourceImage">源设计图。</param>
        /// <param name="manifest">分割结果清单。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="prefabName">目标 prefab 名称。</param>
        /// <returns>生成的 Sprite 列表。</returns>
        public static List<DraftGeneratedSprite> ImportSprites(
            Texture2D sourceImage,
            DraftSegmentationManifest manifest,
            ComfyUiServiceConfig config,
            string prefabName)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();

            NormalizeManifest(manifest, sourceImage, config);
            string outputRoot = BuildOutputRoot(config, prefabName, manifest.sourceHash);
            string outputAbsoluteRoot = DraftPathUtility.ToAbsolutePath(outputRoot);
            Directory.CreateDirectory(outputAbsoluteRoot);

            var generatedSprites = new List<DraftGeneratedSprite>();
            foreach (var region in manifest.regions.Take(config.MaxRegions))
            {
                if (region == null || region.bbox == null)
                    continue;

                if (!region.included || region.reviewState == DraftRegionReviewState.Ignored)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Skipped;
                    region.generationMessage = string.IsNullOrEmpty(region.reviewReason) ? "用户忽略该 region。" : region.reviewReason;
                    continue;
                }

                if (region.bbox.width <= 0 || region.bbox.height <= 0)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Skipped;
                    region.generationMessage = "bbox 尺寸无效，跳过生成。";
                    continue;
                }

                try
                {
                    string marker = SanitizeMarker(region.marker, region.label, region.id);
                    string fileName = $"DW_{manifest.sourceHash}_{SanitizeFileName(region.id)}_{marker}.png";
                    string assetPath = (outputRoot.TrimEnd('/') + "/" + fileName).Replace('\\', '/');

                    DraftSpriteCropper.CropRegionToPng(sourceImage, manifest, region, assetPath, config);
                    var sprite = DraftGeneratedSpriteImporter.ImportSprite(assetPath, region, config);
                    if (sprite == null)
                    {
                        region.generationStatus = DraftRegionGenerationStatus.Failed;
                        region.generationMessage = "Sprite 导入失败。";
                        continue;
                    }

                    region.croppedAssetPath = assetPath;
                    region.spriteAssetPath = sprite.SpriteAssetPath;
                    region.spriteGuid = sprite.SpriteGuid;
                    region.generationStatus = DraftRegionGenerationStatus.Generated;
                    region.generationMessage = "Sprite 生成成功。";
                    sprite.GenerationStatus = DraftRegionGenerationStatus.Generated;
                    sprite.GenerationMessage = region.generationMessage;
                    generatedSprites.Add(sprite);
                }
                catch (Exception ex)
                {
                    region.generationStatus = DraftRegionGenerationStatus.Failed;
                    region.generationMessage = ex.Message;
                    Debug.LogWarning($"[DraftImageSegmentationService] region '{region.id}' 生成 Sprite 失败: {ex.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return generatedSprites;
        }

        /// <summary>
        /// 计算源图文件的短 SHA-256 哈希，用于输出目录和缓存键。
        /// </summary>
        /// <param name="sourceImage">源图。</param>
        /// <returns>12 位十六进制短哈希。</returns>
        public static string ComputeSourceHash(Texture2D sourceImage)
        {
            if (sourceImage == null)
                return "noimage";

            string assetPath = AssetDatabase.GetAssetPath(sourceImage);
            byte[] bytes;
            if (!string.IsNullOrEmpty(assetPath))
            {
                string absolutePath = DraftPathUtility.ToAbsolutePath(assetPath);
                bytes = File.Exists(absolutePath)
                    ? File.ReadAllBytes(absolutePath)
                    : Encoding.UTF8.GetBytes(assetPath + sourceImage.width + "x" + sourceImage.height);
            }
            else
            {
                bytes = Encoding.UTF8.GetBytes(sourceImage.name + sourceImage.width + "x" + sourceImage.height);
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder();
                for (int i = 0; i < hash.Length && builder.Length < 12; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// 规范化 manifest 中的尺寸、哈希、region id、marker 和扩张 bbox。
        /// </summary>
        internal static void NormalizeManifest(DraftSegmentationManifest manifest, Texture2D sourceImage, ComfyUiServiceConfig config)
        {
            if (manifest == null || sourceImage == null || config == null)
                return;

            config.Normalize();

            manifest.sourceHash = string.IsNullOrEmpty(manifest.sourceHash)
                ? ComputeSourceHash(sourceImage)
                : SanitizeFileName(manifest.sourceHash);
            manifest.sourceWidth = manifest.sourceWidth > 0 ? manifest.sourceWidth : sourceImage.width;
            manifest.sourceHeight = manifest.sourceHeight > 0 ? manifest.sourceHeight : sourceImage.height;
            manifest.sourceAssetPath = string.IsNullOrEmpty(manifest.sourceAssetPath)
                ? AssetDatabase.GetAssetPath(sourceImage)
                : manifest.sourceAssetPath;
            if (manifest.regions == null)
            {
                manifest.regions = new List<DraftSegmentRegion>();
            }

            var normalizedRegions = new List<DraftSegmentRegion>();
            int index = 1;
            foreach (var region in manifest.regions)
            {
                if (region == null || region.bbox == null)
                    continue;

                region.id = string.IsNullOrEmpty(region.id) ? $"r{index:000}" : SanitizeFileName(region.id);
                region.marker = SanitizeMarker(region.marker, region.label, region.id);
                region.confidence = Mathf.Clamp01(region.confidence);
                region.bbox = ClampBounds(region.bbox, manifest.sourceWidth, manifest.sourceHeight);
                int area = region.bbox.width * region.bbox.height;
                if (area < config.MinRegionArea)
                {
                    region.included = false;
                    region.requiresReview = true;
                    region.reviewState = DraftRegionReviewState.Ignored;
                    region.reviewReason = $"区域面积 {area} 小于最小阈值 {config.MinRegionArea}";
                    region.generationStatus = DraftRegionGenerationStatus.Skipped;
                    region.generationMessage = region.reviewReason;
                }
                else if (region.confidence < config.AssignmentAutoApproveConfidence)
                {
                    region.included = true;
                    region.requiresReview = true;
                    region.reviewState = DraftRegionReviewState.NeedsReview;
                    region.reviewReason = $"置信度 {region.confidence:0.00} 低于自动批准阈值 {config.AssignmentAutoApproveConfidence:0.00}";
                }
                else if (region.reviewState == DraftRegionReviewState.Pending)
                {
                    region.included = true;
                    region.reviewState = DraftRegionReviewState.Included;
                }

                region.expandedBbox = DraftSegmentBounds.FromRect(ExpandBounds(
                    region.bbox.ToRect(), manifest.sourceWidth, manifest.sourceHeight, config));
                normalizedRegions.Add(region);
                index++;
                if (normalizedRegions.Count >= config.MaxRegions)
                    break;
            }

            manifest.regions = normalizedRegions;
        }

        /// <summary>
        /// 从 cache 目录内的 mask PNG 构建内部 manifest，用于 workflow 未输出 manifest 的兜底模式。
        /// </summary>
        /// <param name="cacheDirectory">mask 所在目录。</param>
        /// <param name="sourceImage">源设计图。</param>
        /// <param name="sourceHash">源图哈希。</param>
        /// <param name="sourceAssetPath">源图资产路径。</param>
        /// <returns>由 mask alpha bbox 生成的 manifest。</returns>
        internal static DraftSegmentationManifest BuildManifestFromMaskFiles(
            string cacheDirectory,
            Texture2D sourceImage,
            string sourceHash,
            string sourceAssetPath)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            var manifest = new DraftSegmentationManifest
            {
                workflowVersion = "mask-only-fallback",
                sourceHash = string.IsNullOrEmpty(sourceHash) ? ComputeSourceHash(sourceImage) : SanitizeFileName(sourceHash),
                sourceWidth = sourceImage.width,
                sourceHeight = sourceImage.height,
                sourceAssetPath = sourceAssetPath,
                manifestDirectory = (cacheDirectory ?? string.Empty).Replace('\\', '/'),
                regions = new List<DraftSegmentRegion>()
            };

            if (string.IsNullOrEmpty(cacheDirectory) || !Directory.Exists(cacheDirectory))
                return manifest;

            string[] maskFiles = Directory.GetFiles(cacheDirectory, "*.png", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            int index = 1;
            foreach (string maskFile in maskFiles)
            {
                var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!mask.LoadImage(File.ReadAllBytes(maskFile)))
                        continue;

                    if (!DraftSpriteCropper.TryCalculateAlphaBounds(mask, out DraftSegmentBounds bounds, 1))
                        continue;

                    string id = $"r{index:000}";
                    string label = Path.GetFileNameWithoutExtension(maskFile);
                    manifest.regions.Add(new DraftSegmentRegion
                    {
                        id = id,
                        marker = SanitizeMarker(label, label, id),
                        label = label,
                        confidence = 1f,
                        bbox = bounds,
                        maskPath = maskFile.Replace('\\', '/'),
                        included = true,
                        requiresReview = true,
                        reviewState = DraftRegionReviewState.NeedsReview,
                        reviewReason = "mask-only fallback：请确认 Unity 根据 alpha bbox 推导的区域。"
                    });
                    index++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mask);
                }
            }

            return manifest;
        }

        /// <summary>
        /// 构建最终 Sprite 输出根目录。
        /// </summary>
        internal static string BuildOutputRoot(ComfyUiServiceConfig config, string prefabName, string sourceHash)
        {
            config.Normalize();
            string safePrefabName = SanitizeFileName(string.IsNullOrEmpty(prefabName) ? "Prefab" : prefabName);
            return (config.GeneratedSpriteOutputRoot.TrimEnd('/') + "/" + safePrefabName + "/" + sourceHash).Replace('\\', '/');
        }

        /// <summary>
        /// 将 bbox 按配置扩张，并裁剪到源图范围内。
        /// </summary>
        internal static Rect ExpandBounds(Rect bounds, int sourceWidth, int sourceHeight, ComfyUiServiceConfig config)
        {
            float expand = config.RegionExpandPixels + Mathf.Max(bounds.width, bounds.height) * config.RegionExpandPercent;
            float xMin = Mathf.Clamp(bounds.xMin - expand, 0f, sourceWidth);
            float yMin = Mathf.Clamp(bounds.yMin - expand, 0f, sourceHeight);
            float xMax = Mathf.Clamp(bounds.xMax + expand, 0f, sourceWidth);
            float yMax = Mathf.Clamp(bounds.yMax + expand, 0f, sourceHeight);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin + 1f, xMax), Mathf.Max(yMin + 1f, yMax));
        }

        /// <summary>
        /// 将 bounds 限制在源图范围内。
        /// </summary>
        private static DraftSegmentBounds ClampBounds(DraftSegmentBounds bounds, int sourceWidth, int sourceHeight)
        {
            int x = Mathf.Clamp(bounds.x, 0, Mathf.Max(0, sourceWidth - 1));
            int y = Mathf.Clamp(bounds.y, 0, Mathf.Max(0, sourceHeight - 1));
            int width = Mathf.Clamp(bounds.width, 1, Mathf.Max(1, sourceWidth - x));
            int height = Mathf.Clamp(bounds.height, 1, Mathf.Max(1, sourceHeight - y));
            return new DraftSegmentBounds { x = x, y = y, width = width, height = height };
        }

        /// <summary>
        /// 规范化 marker，确保适合用于文件名和匹配 token。
        /// </summary>
        internal static string SanitizeMarker(string marker, string label, string fallback)
        {
            string value = !string.IsNullOrWhiteSpace(marker)
                ? marker
                : !string.IsNullOrWhiteSpace(label) ? label : fallback;
            value = value ?? "region";
            value = value.Trim().ToLowerInvariant();

            var builder = new StringBuilder(value.Length);
            bool lastWasSeparator = false;
            foreach (char c in value)
            {
                bool isWord = char.IsLetterOrDigit(c);
                if (isWord)
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "region" : result;
        }

        /// <summary>
        /// 生成适合作为文件名片段的字符串。
        /// </summary>
        internal static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "value";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 || c == '/' || c == '\\' ? '_' : c);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// JSON-driven region 提取结果，包含可切图 region manifest 与被跳过的 descriptor 说明。
    /// </summary>
    public class DraftJsonRegionExtractionResult
    {
        /// <summary>从 JSON / descriptor 提取出的分割清单。</summary>
        public DraftSegmentationManifest Manifest = new DraftSegmentationManifest();

        /// <summary>被跳过的 descriptor 及原因。</summary>
        public List<DraftSkippedJsonDescriptor> SkippedDescriptors = new List<DraftSkippedJsonDescriptor>();

        /// <summary>是否发生解析或转换错误。</summary>
        public bool HasError;

        /// <summary>解析或转换错误信息。</summary>
        public string ErrorMessage;
    }

    /// <summary>
    /// JSON-driven region 提取过程中被跳过的 descriptor 说明。
    /// </summary>
    public class DraftSkippedJsonDescriptor
    {
        /// <summary>descriptor 对应的层级路径。</summary>
        public string DescriptorPath;

        /// <summary>descriptor 节点名。</summary>
        public string NodeName;

        /// <summary>跳过原因。</summary>
        public string Reason;
    }

    /// <summary>
    /// 从 ui_structure.json 转换后的预览 descriptor 中提取确定性切图 region。
    /// </summary>
    public static class DraftJsonRegionExtractionService
    {
        /// <summary>
        /// 从当前 Draft Workbench 预览变更中提取 JSON-driven Auto Slice regions。
        /// </summary>
        /// <param name="previewChanges">当前预览节点变更列表。</param>
        /// <param name="sourceHash">源图哈希。</param>
        /// <param name="sourceAssetPath">源图资产路径。</param>
        /// <param name="sourceTextureWidth">源图像素宽度。</param>
        /// <param name="sourceTextureHeight">源图像素高度。</param>
        /// <returns>提取结果，包含 manifest 和 skipped descriptor 列表。</returns>
        public static DraftJsonRegionExtractionResult ExtractFromPreviewChanges(
            IReadOnlyList<UguiNodeChange> previewChanges,
            string sourceHash,
            string sourceAssetPath,
            int sourceTextureWidth,
            int sourceTextureHeight)
        {
            var result = new DraftJsonRegionExtractionResult
            {
                Manifest = new DraftSegmentationManifest
                {
                    workflowVersion = "json-driven-descriptor",
                    sourceHash = DraftImageSegmentationService.SanitizeFileName(string.IsNullOrEmpty(sourceHash) ? "json" : sourceHash),
                    sourceWidth = Mathf.Max(0, sourceTextureWidth),
                    sourceHeight = Mathf.Max(0, sourceTextureHeight),
                    sourceAssetPath = sourceAssetPath,
                    regions = new List<DraftSegmentRegion>()
                }
            };

            if (previewChanges == null)
                return result;

            var changeList = previewChanges as IList<UguiNodeChange> ?? previewChanges.ToList();
            var validImageDescriptorPaths = CollectValidImageDescriptorPaths(changeList);
            int index = 1;
            foreach (var change in changeList)
            {
                var descriptor = change?.Descriptor;
                if (descriptor == null)
                    continue;

                string descriptorPath = PrefabDraftBuilder.GetDescriptorPath(descriptor);
                if (change.HasConflict)
                {
                    AddSkipped(result, descriptor, descriptorPath, "descriptor 存在布局冲突，跳过 JSON-driven 切图。");
                    continue;
                }

                if (!HasImageComponent(descriptor))
                {
                    AddSkipped(result, descriptor, descriptorPath, "descriptor 未包含 UnityEngine.UI.Image，跳过切图。");
                    continue;
                }

                if (!descriptor.HasSourceBounds || descriptor.SourceBounds.width <= 0f || descriptor.SourceBounds.height <= 0f)
                {
                    AddSkipped(result, descriptor, descriptorPath, "descriptor 缺少有效 source bounds，跳过切图。");
                    continue;
                }

                string regionId = ResolveRegionId(descriptor, index);
                var visuals = EnsureDescriptorRegionId(descriptor, regionId);
                var sourceBounds = DraftSegmentBounds.FromRect(descriptor.SourceBounds);
                int sourceCanvasWidth = Mathf.RoundToInt(descriptor.SourceCanvasSize.x > 0f ? descriptor.SourceCanvasSize.x : sourceTextureWidth);
                int sourceCanvasHeight = Mathf.RoundToInt(descriptor.SourceCanvasSize.y > 0f ? descriptor.SourceCanvasSize.y : sourceTextureHeight);
                DraftSegmentBounds cropBounds = ScaleBoundsToTexture(
                    descriptor.SourceBounds,
                    sourceCanvasWidth,
                    sourceCanvasHeight,
                    sourceTextureWidth,
                    sourceTextureHeight);
                bool hasAspectMismatch = HasAspectMismatch(sourceCanvasWidth, sourceCanvasHeight, sourceTextureWidth, sourceTextureHeight);
                DraftAssetKind assetKind = ResolveAssetKind(descriptor, visuals, cropBounds, sourceCanvasWidth, sourceCanvasHeight);
                DraftAlphaMode alphaMode = ResolveAlphaMode(visuals, assetKind);
                bool requiresTransparentAlpha = visuals != null && visuals.HasRequiresTransparentAlpha
                    ? visuals.RequiresTransparentAlpha
                    : alphaMode == DraftAlphaMode.TransparentForeground;
                bool makeSquare = visuals != null && visuals.HasMakeSquare
                    ? visuals.MakeSquare
                    : alphaMode == DraftAlphaMode.TransparentForeground;
                if (ShouldSkipImplicitCompositeButtonContainer(
                        descriptor,
                        descriptorPath,
                        visuals,
                        assetKind,
                        validImageDescriptorPaths))
                {
                    AddSkipped(result, descriptor, descriptorPath, "隐式 Button 容器包含子 Image region，跳过父级复合矩形切图。");
                    continue;
                }

                var region = new DraftSegmentRegion
                {
                    id = regionId,
                    marker = DraftImageSegmentationService.SanitizeMarker(visuals?.AssetMarker, visuals?.SpriteHint, descriptor.Name),
                    spriteHint = visuals?.SpriteHint,
                    assetKind = assetKind,
                    alphaMode = alphaMode,
                    paddingPixels = visuals != null && visuals.HasPaddingPixels ? Mathf.Max(0, visuals.PaddingPixels) : 0,
                    paddingPercent = visuals != null && visuals.HasPaddingPercent ? Mathf.Clamp01(visuals.PaddingPercent) : 0f,
                    makeSquare = makeSquare,
                    requiresTransparentAlpha = requiresTransparentAlpha,
                    label = descriptor.Name,
                    sourceKind = DraftRegionSourceKind.JsonDescriptor,
                    nodePath = descriptorPath,
                    sourceCanvasWidth = sourceCanvasWidth,
                    sourceCanvasHeight = sourceCanvasHeight,
                    sourceBounds = sourceBounds,
                    cropBounds = cropBounds,
                    confidence = 1f,
                    bbox = cropBounds,
                    included = true,
                    reviewState = hasAspectMismatch ? DraftRegionReviewState.NeedsReview : DraftRegionReviewState.Included,
                    requiresReview = hasAspectMismatch,
                    reviewReason = hasAspectMismatch
                        ? $"source canvas 与 texture 宽高比不一致：canvas={sourceCanvasWidth}x{sourceCanvasHeight}, texture={sourceTextureWidth}x{sourceTextureHeight}。"
                        : string.Empty,
                    generationStatus = DraftRegionGenerationStatus.Pending
                };
                result.Manifest.regions.Add(region);
                index++;
            }

            return result;
        }

        /// <summary>
        /// 收集当前预览中可用于切图的 Image descriptor 路径。
        /// </summary>
        private static HashSet<string> CollectValidImageDescriptorPaths(IEnumerable<UguiNodeChange> previewChanges)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (previewChanges == null)
                return paths;

            foreach (var change in previewChanges)
            {
                var descriptor = change?.Descriptor;
                if (descriptor == null || change.HasConflict || !HasImageComponent(descriptor))
                    continue;

                if (!descriptor.HasSourceBounds || descriptor.SourceBounds.width <= 0f || descriptor.SourceBounds.height <= 0f)
                    continue;

                paths.Add(PrefabDraftBuilder.GetDescriptorPath(descriptor));
            }

            return paths;
        }

        /// <summary>
        /// 从可编辑 ui_structure.json 文本转换 descriptor 并提取 JSON-driven Auto Slice regions。
        /// </summary>
        /// <param name="jsonText">ui_structure.json 文本，允许包含 markdown JSON 代码块。</param>
        /// <param name="sourceHash">源图哈希。</param>
        /// <param name="sourceAssetPath">源图资产路径。</param>
        /// <param name="sourceTextureWidth">源图像素宽度。</param>
        /// <param name="sourceTextureHeight">源图像素高度。</param>
        /// <returns>提取结果；非法 JSON 时 HasError 为 true 且 regions 为空。</returns>
        public static DraftJsonRegionExtractionResult ExtractFromJson(
            string jsonText,
            string sourceHash,
            string sourceAssetPath,
            int sourceTextureWidth,
            int sourceTextureHeight)
        {
            string json = AiVisionClient.ExtractJson(jsonText);
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateErrorResult(sourceHash, sourceAssetPath, sourceTextureWidth, sourceTextureHeight, "无法从输入中提取 JSON。");
            }

            UiStructure structure;
            try
            {
                structure = JsonUtility.FromJson<UiStructure>(json);
            }
            catch (Exception ex)
            {
                return CreateErrorResult(sourceHash, sourceAssetPath, sourceTextureWidth, sourceTextureHeight, $"JSON 反序列化失败: {ex.Message}");
            }

            if (structure?.root == null)
            {
                return CreateErrorResult(sourceHash, sourceAssetPath, sourceTextureWidth, sourceTextureHeight, "JSON 解析结果为空：UiStructure.root 为 null。");
            }

            var converter = new UiStructureConverter();
            List<ConvertedNode> convertedNodes = converter.Convert(structure, UiStructureConversionOptions.DraftWorkbenchDefault);
            var changes = convertedNodes
                .Where(node => node?.Descriptor != null)
                .Select(node =>
                {
                    node.Descriptor.Visuals = node.Visuals;
                    node.Descriptor.LayoutInfo = node.LayoutInfo;
                    return new UguiNodeChange { Descriptor = node.Descriptor };
                })
                .ToList();

            var result = ExtractFromPreviewChanges(changes, sourceHash, sourceAssetPath, sourceTextureWidth, sourceTextureHeight);
            if (converter.Report?.Warnings != null)
            {
                foreach (string warning in converter.Report.Warnings)
                {
                    result.SkippedDescriptors.Add(new DraftSkippedJsonDescriptor
                    {
                        Reason = warning
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 创建 JSON 提取错误结果。
        /// </summary>
        private static DraftJsonRegionExtractionResult CreateErrorResult(
            string sourceHash,
            string sourceAssetPath,
            int sourceTextureWidth,
            int sourceTextureHeight,
            string errorMessage)
        {
            return new DraftJsonRegionExtractionResult
            {
                Manifest = new DraftSegmentationManifest
                {
                    workflowVersion = "json-driven-descriptor",
                    sourceHash = DraftImageSegmentationService.SanitizeFileName(string.IsNullOrEmpty(sourceHash) ? "json" : sourceHash),
                    sourceWidth = Mathf.Max(0, sourceTextureWidth),
                    sourceHeight = Mathf.Max(0, sourceTextureHeight),
                    sourceAssetPath = sourceAssetPath,
                    regions = new List<DraftSegmentRegion>()
                },
                HasError = true,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 将 descriptor source bounds 从 source canvas 坐标系缩放到 texture 像素坐标系。
        /// </summary>
        private static DraftSegmentBounds ScaleBoundsToTexture(
            Rect sourceBounds,
            int sourceCanvasWidth,
            int sourceCanvasHeight,
            int sourceTextureWidth,
            int sourceTextureHeight)
        {
            float scaleX = sourceCanvasWidth > 0 ? sourceTextureWidth / (float)sourceCanvasWidth : 1f;
            float scaleY = sourceCanvasHeight > 0 ? sourceTextureHeight / (float)sourceCanvasHeight : 1f;
            float xMin = sourceBounds.xMin * scaleX;
            float yMin = sourceBounds.yMin * scaleY;
            float xMax = sourceBounds.xMax * scaleX;
            float yMax = sourceBounds.yMax * scaleY;

            // 缩放后的浮点边界必须向外取整，否则半像素区域会被 RoundToInt 吃掉。
            int x = Mathf.Clamp(Mathf.FloorToInt(xMin), 0, Mathf.Max(0, sourceTextureWidth - 1));
            int y = Mathf.Clamp(Mathf.FloorToInt(yMin), 0, Mathf.Max(0, sourceTextureHeight - 1));
            int right = Mathf.Clamp(Mathf.CeilToInt(xMax), x + 1, Mathf.Max(x + 1, sourceTextureWidth));
            int bottom = Mathf.Clamp(Mathf.CeilToInt(yMax), y + 1, Mathf.Max(y + 1, sourceTextureHeight));

            return new DraftSegmentBounds
            {
                x = x,
                y = y,
                width = Mathf.Max(1, right - x),
                height = Mathf.Max(1, bottom - y)
            };
        }

        /// <summary>
        /// 判断 source canvas 与 texture 的宽高比是否明显不一致。
        /// </summary>
        private static bool HasAspectMismatch(
            int sourceCanvasWidth,
            int sourceCanvasHeight,
            int sourceTextureWidth,
            int sourceTextureHeight)
        {
            if (sourceCanvasWidth <= 0 || sourceCanvasHeight <= 0 || sourceTextureWidth <= 0 || sourceTextureHeight <= 0)
                return false;

            float canvasAspect = sourceCanvasWidth / (float)sourceCanvasHeight;
            float textureAspect = sourceTextureWidth / (float)sourceTextureHeight;
            return Mathf.Abs(canvasAspect - textureAspect) > 0.01f;
        }

        /// <summary>
        /// 判断 descriptor 是否会生成 Image 组件。
        /// </summary>
        /// <param name="descriptor">待检查 descriptor。</param>
        /// <returns>包含 Image 组件时返回 true。</returns>
        private static bool HasImageComponent(UguiNodeDescriptor descriptor)
        {
            return descriptor?.ComponentTypeNames != null
                   && descriptor.ComponentTypeNames.Any(type => string.Equals(type, "UnityEngine.UI.Image", StringComparison.Ordinal));
        }

        /// <summary>
        /// 判断 descriptor 是否包含 Button 组件。
        /// </summary>
        private static bool HasButtonComponent(UguiNodeDescriptor descriptor)
        {
            return descriptor?.ComponentTypeNames != null
                   && descriptor.ComponentTypeNames.Any(type => string.Equals(type, "UnityEngine.UI.Button", StringComparison.Ordinal));
        }

        /// <summary>
        /// 判断隐式复合 Button 父节点是否应跳过切图，避免把包含文字/背景的父矩形导入 final icon。
        /// </summary>
        private static bool ShouldSkipImplicitCompositeButtonContainer(
            UguiNodeDescriptor descriptor,
            string descriptorPath,
            UiNodeVisuals visuals,
            DraftAssetKind assetKind,
            IEnumerable<string> validImageDescriptorPaths)
        {
            if (descriptor == null || assetKind != DraftAssetKind.Button || !HasButtonComponent(descriptor))
                return false;

            if (visuals != null && visuals.HasAssetKind)
                return false;

            if (string.IsNullOrEmpty(descriptorPath) || validImageDescriptorPaths == null)
                return false;

            string descendantPrefix = descriptorPath + "/";
            return validImageDescriptorPaths.Any(path =>
                !string.Equals(path, descriptorPath, StringComparison.Ordinal)
                && path.StartsWith(descendantPrefix, StringComparison.Ordinal));
        }

        /// <summary>
        /// 解析 JSON-driven region 的资产语义，显式 metadata 优先，缺失时按名称和面积启发式推断。
        /// </summary>
        private static DraftAssetKind ResolveAssetKind(
            UguiNodeDescriptor descriptor,
            UiNodeVisuals visuals,
            DraftSegmentBounds cropBounds,
            int sourceCanvasWidth,
            int sourceCanvasHeight)
        {
            if (visuals != null && visuals.HasAssetKind)
                return visuals.AssetKind;

            string text = ((descriptor?.Name ?? string.Empty) + " "
                           + (visuals?.AssetMarker ?? string.Empty) + " "
                           + (visuals?.SpriteHint ?? string.Empty)).ToLowerInvariant();

            if (ContainsAny(text, "background", "mainbg", " bg "))
                return DraftAssetKind.Background;

            if (ContainsAny(text, "preview", "panel", "frame", "bar", "shelf"))
                return DraftAssetKind.Panel;

            if (descriptor?.ComponentTypeNames != null
                && descriptor.ComponentTypeNames.Any(type => string.Equals(type, "UnityEngine.UI.Button", StringComparison.Ordinal)))
            {
                return DraftAssetKind.Button;
            }

            if (ContainsAny(text, "button", "btn", "plate"))
                return DraftAssetKind.Button;

            if (ContainsAny(text, "decoration", "decor", "divider", "ornament", "vine"))
                return DraftAssetKind.Decoration;

            if (ContainsAny(text, "icon", "coin", "gem", "heart", "star", "trophy", "sword", "map", "broom", "castle", "acorn"))
                return DraftAssetKind.Icon;

            float sourceArea = Mathf.Max(1f, sourceCanvasWidth * sourceCanvasHeight);
            float areaRatio = cropBounds != null ? cropBounds.width * cropBounds.height / sourceArea : 0f;
            if (areaRatio >= 0.5f)
                return DraftAssetKind.Background;
            if (areaRatio >= 0.15f)
                return DraftAssetKind.Panel;

            return DraftAssetKind.Unknown;
        }

        /// <summary>
        /// 根据显式 alphaMode 或资产语义决定 alpha 输出策略。
        /// </summary>
        private static DraftAlphaMode ResolveAlphaMode(UiNodeVisuals visuals, DraftAssetKind assetKind)
        {
            if (visuals != null && visuals.HasAlphaMode)
                return visuals.AlphaMode;

            return assetKind == DraftAssetKind.Icon || assetKind == DraftAssetKind.Decoration
                ? DraftAlphaMode.TransparentForeground
                : DraftAlphaMode.OpaqueRect;
        }

        /// <summary>
        /// 判断文本是否包含任一关键词。
        /// </summary>
        private static bool ContainsAny(string text, params string[] keywords)
        {
            if (string.IsNullOrEmpty(text) || keywords == null)
                return false;

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrEmpty(keyword) && text.Contains(keyword))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 解析稳定 region id，优先使用 JSON / descriptor 中传入的 regionId。
        /// </summary>
        /// <param name="descriptor">来源 descriptor。</param>
        /// <param name="index">顺序 fallback 索引。</param>
        /// <returns>稳定 region id。</returns>
        private static string ResolveRegionId(UguiNodeDescriptor descriptor, int index)
        {
            string regionId = descriptor?.Visuals?.RegionId;
            return string.IsNullOrWhiteSpace(regionId)
                ? $"r{index:000}"
                : DraftImageSegmentationService.SanitizeFileName(regionId);
        }

        /// <summary>
        /// 将 JSON-driven 提取阶段生成的稳定 regionId 回写到 descriptor，供后续 Sprite assignment 精确匹配。
        /// </summary>
        /// <param name="descriptor">来源 descriptor。</param>
        /// <param name="regionId">已解析的稳定 regionId。</param>
        /// <returns>descriptor 上的视觉信息。</returns>
        private static UiNodeVisuals EnsureDescriptorRegionId(UguiNodeDescriptor descriptor, string regionId)
        {
            if (descriptor == null)
                return null;

            if (descriptor.Visuals == null)
            {
                descriptor.Visuals = new UiNodeVisuals();
            }

            descriptor.Visuals.RegionId = regionId;
            return descriptor.Visuals;
        }

        /// <summary>
        /// 记录被跳过的 descriptor。
        /// </summary>
        /// <param name="result">提取结果。</param>
        /// <param name="descriptor">被跳过 descriptor。</param>
        /// <param name="descriptorPath">descriptor 层级路径。</param>
        /// <param name="reason">跳过原因。</param>
        private static void AddSkipped(
            DraftJsonRegionExtractionResult result,
            UguiNodeDescriptor descriptor,
            string descriptorPath,
            string reason)
        {
            result.SkippedDescriptors.Add(new DraftSkippedJsonDescriptor
            {
                DescriptorPath = descriptorPath,
                NodeName = descriptor?.Name,
                Reason = reason
            });
        }
    }
}
#endif
