#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// ComfyUiServiceConfig 与 Auto Slice 状态模型的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class ComfyUiServiceConfigTests
    {
        /// <summary>
        /// Normalize 应补齐自动切图所需参数，并把非法值限制到安全范围。
        /// </summary>
        [Test]
        public void Normalize_补齐AutoSlice参数并限制范围()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.BaseUrl = "  ";
            config.GeneratedSpriteOutputRoot = "Library/BadOutput";
            config.CacheRoot = "";
            config.DetectionPreset = "  ";
            config.DetectionPrompt = "  ";
            config.MaskThreshold = -0.25f;
            config.RefineIterations = -5;
            config.TimeoutSeconds = 1;
            config.PollIntervalSeconds = 100f;
            config.MaxPollSeconds = 1;
            config.AssignmentAutoApproveConfidence = 3f;

            config.Normalize();

            Assert.AreEqual(ComfyUiServiceConfig.DefaultBaseUrl, config.BaseUrl);
            Assert.AreEqual(ComfyUiServiceConfig.DefaultGeneratedSpriteOutputRoot, config.GeneratedSpriteOutputRoot);
            Assert.AreEqual(ComfyUiServiceConfig.DefaultCacheRoot, config.CacheRoot);
            Assert.AreEqual(ComfyUiServiceConfig.DefaultDetectionPreset, config.DetectionPreset);
            Assert.AreEqual(ComfyUiServiceConfig.DefaultDetectionPrompt, config.DetectionPrompt);
            Assert.AreEqual(0f, config.MaskThreshold);
            Assert.AreEqual(0, config.RefineIterations);
            Assert.GreaterOrEqual(config.TimeoutSeconds, 5);
            Assert.LessOrEqual(config.PollIntervalSeconds, 10f);
            Assert.GreaterOrEqual(config.MaxPollSeconds, 10);
            Assert.AreEqual(1f, config.AssignmentAutoApproveConfidence);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// GetNormalizedBaseUrl 应移除末尾斜杠并保留协议与端口。
        /// </summary>
        [Test]
        public void GetNormalizedBaseUrl_移除末尾斜杠()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.BaseUrl = "http://127.0.0.1:8188///";

            Assert.AreEqual("http://127.0.0.1:8188", config.GetNormalizedBaseUrl());

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// DraftSegmentRegion 应持久化 include/review 状态，供区域审查 UI 使用。
        /// </summary>
        [Test]
        public void DraftSegmentRegion_序列化保留审核状态()
        {
            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                included = false,
                reviewState = DraftRegionReviewState.NeedsReview,
                requiresReview = true,
                reviewReason = "low confidence",
                generationStatus = DraftRegionGenerationStatus.Failed,
                generationMessage = "mask empty"
            };

            string json = JsonUtility.ToJson(region);
            var restored = JsonUtility.FromJson<DraftSegmentRegion>(json);

            Assert.IsFalse(restored.included);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, restored.reviewState);
            Assert.IsTrue(restored.requiresReview);
            Assert.AreEqual("low confidence", restored.reviewReason);
            Assert.AreEqual(DraftRegionGenerationStatus.Failed, restored.generationStatus);
            Assert.AreEqual("mask empty", restored.generationMessage);
        }

        /// <summary>
        /// DraftSegmentRegion 应持久化 JSON-driven 切图阶段所需的来源、坐标和输出路径。
        /// </summary>
        [Test]
        public void DraftSegmentRegion_序列化保留JsonDriven切图字段()
        {
            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                sourceKind = DraftRegionSourceKind.JsonDescriptor,
                nodePath = "Root/Icon",
                sourceCanvasWidth = 1920,
                sourceCanvasHeight = 1080,
                sourceBounds = new DraftSegmentBounds { x = 100, y = 120, width = 64, height = 64 },
                cropBounds = new DraftSegmentBounds { x = 50, y = 60, width = 32, height = 32 },
                rawPngPath = "Library/DraftWorkbench/JsonRawCrops/hash/r001_icon_coin.png",
                refinedPngPath = "Library/DraftWorkbench/ComfyCache/hash/r001_icon_coin_refined.png",
                spriteAssetPath = "Assets/AssetRaw/Image/DraftWorkbenchGenerated/Test/hash/r001_icon_coin.png",
                generationStatus = DraftRegionGenerationStatus.Generated
            };

            string json = JsonUtility.ToJson(region);
            var restored = JsonUtility.FromJson<DraftSegmentRegion>(json);

            Assert.AreEqual(DraftRegionSourceKind.JsonDescriptor, restored.sourceKind);
            Assert.AreEqual("Root/Icon", restored.nodePath);
            Assert.AreEqual(1920, restored.sourceCanvasWidth);
            Assert.AreEqual(1080, restored.sourceCanvasHeight);
            Assert.AreEqual(100, restored.sourceBounds.x);
            Assert.AreEqual(64, restored.sourceBounds.width);
            Assert.AreEqual(50, restored.cropBounds.x);
            Assert.AreEqual(32, restored.cropBounds.width);
            Assert.AreEqual("Library/DraftWorkbench/JsonRawCrops/hash/r001_icon_coin.png", restored.rawPngPath);
            Assert.AreEqual("Library/DraftWorkbench/ComfyCache/hash/r001_icon_coin_refined.png", restored.refinedPngPath);
            Assert.AreEqual("Assets/AssetRaw/Image/DraftWorkbenchGenerated/Test/hash/r001_icon_coin.png", restored.spriteAssetPath);
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, restored.generationStatus);
        }
    }
}
#endif
