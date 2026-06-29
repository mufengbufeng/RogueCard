#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// DraftImageSegmentationService 的 manifest 规范化与 mask fallback 测试。
    /// </summary>
    [TestFixture]
    public class DraftImageSegmentationServiceTests
    {
        private const string TempCacheDirectory = "Library/DraftWorkbench/ComfyCache/test_mask_fallback";
        private const string TempGeneratedRoot = "Assets/AssetRaw/Image/DraftWorkbenchGenerated/__AutoSliceTests";

        /// <summary>
        /// 测试结束后清理临时 mask 缓存。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            string absolutePath = DraftPathUtility.ToAbsolutePath(TempCacheDirectory);
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, true);
            }

            if (AssetDatabase.IsValidFolder(TempGeneratedRoot))
            {
                AssetDatabase.DeleteAsset(TempGeneratedRoot);
            }
        }

        /// <summary>
        /// NormalizeManifest 应保留可审查 region，并标记小面积或低置信度原因。
        /// </summary>
        [Test]
        public void NormalizeManifest_标记小面积和低置信度Region()
        {
            var source = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "source" };
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.MinRegionArea = 16;
            config.AssignmentAutoApproveConfidence = 0.75f;
            config.Normalize();

            var manifest = new DraftSegmentationManifest
            {
                regions =
                {
                    new DraftSegmentRegion
                    {
                        label = "Tiny Icon",
                        confidence = 0.95f,
                        bbox = new DraftSegmentBounds { x = 1, y = 2, width = 2, height = 2 }
                    },
                    new DraftSegmentRegion
                    {
                        marker = "coin",
                        confidence = 0.3f,
                        bbox = new DraftSegmentBounds { x = 4, y = 5, width = 8, height = 8 }
                    }
                }
            };

            DraftImageSegmentationService.NormalizeManifest(manifest, source, config);

            Assert.AreEqual(2, manifest.regions.Count);
            Assert.AreEqual("r001", manifest.regions[0].id);
            Assert.AreEqual("tiny_icon", manifest.regions[0].marker);
            Assert.IsFalse(manifest.regions[0].included);
            Assert.AreEqual(DraftRegionReviewState.Ignored, manifest.regions[0].reviewState);
            StringAssert.Contains("面积", manifest.regions[0].reviewReason);

            Assert.AreEqual("coin", manifest.regions[1].marker);
            Assert.IsTrue(manifest.regions[1].included);
            Assert.IsTrue(manifest.regions[1].requiresReview);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, manifest.regions[1].reviewState);
            StringAssert.Contains("置信度", manifest.regions[1].reviewReason);

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(source);
        }

        /// <summary>
        /// mask-only fallback 应从 alpha bbox 构建内部 manifest。
        /// </summary>
        [Test]
        public void BuildManifestFromMaskFiles_从AlphaMask生成Region()
        {
            string absoluteDirectory = DraftPathUtility.ToAbsolutePath(TempCacheDirectory);
            Directory.CreateDirectory(absoluteDirectory);
            string maskPath = Path.Combine(absoluteDirectory, "icon_coin_mask.png");
            WriteMask(maskPath);

            var source = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = "source" };
            var manifest = DraftImageSegmentationService.BuildManifestFromMaskFiles(
                absoluteDirectory,
                source,
                "hash123",
                "Assets/Art/source.png");

            Assert.IsNotNull(manifest);
            Assert.AreEqual("hash123", manifest.sourceHash);
            Assert.AreEqual(1, manifest.regions.Count);
            var region = manifest.regions.First();
            Assert.AreEqual("r001", region.id);
            Assert.AreEqual("icon_coin_mask", region.marker);
            Assert.AreEqual(2, region.bbox.x);
            Assert.AreEqual(4, region.bbox.y);
            Assert.AreEqual(3, region.bbox.width);
            Assert.AreEqual(3, region.bbox.height);
            Assert.IsTrue(region.requiresReview);
            StringAssert.Contains("mask-only", region.reviewReason);

            Object.DestroyImmediate(source);
        }

        /// <summary>
        /// ImportSprites 应跳过 ignored region，并把成功结果写回 region。
        /// </summary>
        [Test]
        public void ImportSprites_只生成IncludedRegion并记录结果()
        {
            var source = CreateSourceTexture();
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.GeneratedSpriteOutputRoot = TempGeneratedRoot;
            config.MinRegionArea = 1;
            config.RegionExpandPixels = 0;
            config.RegionExpandPercent = 0f;
            config.Normalize();

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = "hashimport",
                sourceWidth = source.width,
                sourceHeight = source.height,
                regions =
                {
                    new DraftSegmentRegion
                    {
                        id = "r001",
                        marker = "included",
                        included = true,
                        reviewState = DraftRegionReviewState.Included,
                        confidence = 1f,
                        bbox = new DraftSegmentBounds { x = 1, y = 1, width = 3, height = 3 }
                    },
                    new DraftSegmentRegion
                    {
                        id = "r002",
                        marker = "ignored",
                        included = false,
                        reviewState = DraftRegionReviewState.Ignored,
                        confidence = 1f,
                        bbox = new DraftSegmentBounds { x = 4, y = 4, width = 2, height = 2 }
                    }
                }
            };

            var generated = DraftImageSegmentationService.ImportSprites(source, manifest, config, "TestPrefab");

            Assert.AreEqual(1, generated.Count);
            Assert.AreEqual("r001", generated[0].RegionId);
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, manifest.regions[0].generationStatus);
            Assert.IsTrue(manifest.regions[0].spriteAssetPath.StartsWith(TempGeneratedRoot));
            Assert.AreEqual(DraftRegionGenerationStatus.Skipped, manifest.regions[1].generationStatus);
            StringAssert.Contains("忽略", manifest.regions[1].generationMessage);
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(manifest.regions[0].spriteAssetPath));

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(source);
        }

        /// <summary>
        /// JSON-driven region 提取应从 Image descriptor 保留语义并跳过不可切图节点。
        /// </summary>
        [Test]
        public void ExtractJsonRegions_从PreviewDescriptor提取并记录跳过原因()
        {
            var changes = new System.Collections.Generic.List<UguiNodeChange>
            {
                CreatePreviewChange(
                    "CoinIcon",
                    "Root",
                    new System.Collections.Generic.List<string> { "UnityEngine.UI.Image" },
                    true,
                    new Rect(100, 120, 64, 64),
                    new Vector2(1920, 1080),
                    "coin_marker",
                    "coin_hint",
                    "coin_region"),
                CreatePreviewChange(
                    "TitleText",
                    "Root",
                    new System.Collections.Generic.List<string> { "UnityEngine.UI.Text" },
                    true,
                    new Rect(0, 0, 100, 20),
                    new Vector2(1920, 1080),
                    string.Empty,
                    string.Empty,
                    string.Empty),
                CreatePreviewChange(
                    "BoundlessImage",
                    "Root",
                    new System.Collections.Generic.List<string> { "UnityEngine.UI.Image" },
                    false,
                    Rect.zero,
                    new Vector2(1920, 1080),
                    string.Empty,
                    string.Empty,
                    string.Empty)
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hash123",
                "Assets/Art/design.png",
                1920,
                1080);

            Assert.AreEqual(1, result.Manifest.regions.Count);
            Assert.AreEqual(2, result.SkippedDescriptors.Count);
            var region = result.Manifest.regions.Single();
            Assert.AreEqual(DraftRegionSourceKind.JsonDescriptor, region.sourceKind);
            Assert.AreEqual("coin_region", region.id);
            Assert.AreEqual("coin_marker", region.marker);
            Assert.AreEqual("coin_hint", region.spriteHint);
            Assert.AreEqual("Root/CoinIcon", region.nodePath);
            Assert.AreEqual(1920, region.sourceCanvasWidth);
            Assert.AreEqual(1080, region.sourceCanvasHeight);
            Assert.AreEqual(100, region.sourceBounds.x);
            Assert.AreEqual(64, region.sourceBounds.width);
            Assert.AreEqual(100, region.cropBounds.x);
            Assert.AreEqual(64, region.cropBounds.width);
            StringAssert.Contains("Image", result.SkippedDescriptors[0].Reason);
            StringAssert.Contains("source bounds", result.SkippedDescriptors[1].Reason);
        }

        /// <summary>
        /// JSON-driven region 提取应按 source canvas 到 texture 像素比例缩放 crop bounds。
        /// </summary>
        [Test]
        public void ExtractJsonRegions_按Texture尺寸缩放CropBounds()
        {
            var changes = new System.Collections.Generic.List<UguiNodeChange>
            {
                CreatePreviewChange(
                    "CoinIcon",
                    "Root",
                    new System.Collections.Generic.List<string> { "UnityEngine.UI.Image" },
                    true,
                    new Rect(100, 120, 64, 80),
                    new Vector2(1920, 1080),
                    "coin_marker",
                    string.Empty,
                    "coin_region")
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hashscaled",
                "Assets/Art/design.png",
                960,
                540);

            var region = result.Manifest.regions.Single();
            Assert.AreEqual(100, region.sourceBounds.x);
            Assert.AreEqual(120, region.sourceBounds.y);
            Assert.AreEqual(50, region.cropBounds.x);
            Assert.AreEqual(60, region.cropBounds.y);
            Assert.AreEqual(32, region.cropBounds.width);
            Assert.AreEqual(40, region.cropBounds.height);
            Assert.IsFalse(region.requiresReview);
        }

        /// <summary>
        /// JSON-driven region 缩放遇到小数边界时应向外包住完整像素区域，避免切图范围被取整缩小。
        /// </summary>
        [Test]
        public void ExtractJsonRegions_FractionalScaledBoundsRoundOutward()
        {
            var changes = new System.Collections.Generic.List<UguiNodeChange>
            {
                CreatePreviewChange(
                    "CoinIcon",
                    "Root",
                    new System.Collections.Generic.List<string> { "UnityEngine.UI.Image" },
                    true,
                    new Rect(10.25f, 20.25f, 10.5f, 10.5f),
                    new Vector2(100, 100),
                    "coin_marker",
                    string.Empty,
                    "coin_region")
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hashfractional",
                "Assets/Art/design.png",
                33,
                33);

            var region = result.Manifest.regions.Single();
            Assert.AreEqual(3, region.cropBounds.x);
            Assert.AreEqual(6, region.cropBounds.y);
            Assert.AreEqual(4, region.cropBounds.width);
            Assert.AreEqual(5, region.cropBounds.height);
        }

        /// <summary>
        /// JSON-driven region 提取遇到 canvas / texture 宽高比不一致时应标记需要复核。
        /// </summary>
        [Test]
        public void ExtractJsonRegions_宽高比不一致标记需要复核()
        {
            var changes = new System.Collections.Generic.List<UguiNodeChange>
            {
                CreatePreviewChange(
                    "PanelBg",
                    "Root",
                    new System.Collections.Generic.List<string> { "UnityEngine.UI.Image" },
                    true,
                    new Rect(100, 120, 64, 80),
                    new Vector2(1920, 1080),
                    "panel_bg",
                    string.Empty,
                    "")
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hashscaled",
                "Assets/Art/design.png",
                1000,
                540);

            var region = result.Manifest.regions.Single();
            Assert.IsTrue(region.requiresReview);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, region.reviewState);
            StringAssert.Contains("宽高比", region.reviewReason);
        }

        /// <summary>
        /// JSON-driven region 提取应在没有预览节点时从可编辑 JSON 转换 descriptor。
        /// </summary>
        [Test]
        public void ExtractJsonRegions_从EditableJson转换PreviewDescriptor()
        {
            const string json = @"{
                ""canvas"": { ""width"": 1920, ""height"": 1080, ""name"": ""Canvas"" },
                ""root"": {
                    ""type"": ""container"",
                    ""name"": ""Root"",
                    ""size"": { ""width"": 1920, ""height"": 1080 },
                    ""children"": [
                        {
                            ""type"": ""image"",
                            ""name"": ""CoinIcon"",
                            ""position"": { ""x"": 100, ""y"": 120 },
                            ""size"": { ""width"": 64, ""height"": 64 },
                            ""marker"": ""coin_marker"",
                            ""spriteHint"": ""coin_hint"",
                            ""regionId"": ""coin_region""
                        }
                    ]
                }
            }";

            var result = DraftJsonRegionExtractionService.ExtractFromJson(
                json,
                "hashjson",
                "Assets/Art/design.png",
                1920,
                1080);

            Assert.IsFalse(result.HasError, result.ErrorMessage);
            Assert.AreEqual(1, result.Manifest.regions.Count);
            var region = result.Manifest.regions.Single();
            Assert.AreEqual("coin_region", region.id);
            Assert.AreEqual("coin_marker", region.marker);
            Assert.AreEqual("coin_hint", region.spriteHint);
            Assert.AreEqual("CoinIcon", region.nodePath);
        }

        /// <summary>
        /// JSON-driven region 提取遇到非法 JSON 应返回可读错误并保持 region 为空。
        /// </summary>
        [Test]
        public void ExtractJsonRegions_非法Json返回错误且不创建Region()
        {
            var result = DraftJsonRegionExtractionService.ExtractFromJson(
                "{ invalid json",
                "hashjson",
                "Assets/Art/design.png",
                1920,
                1080);

            Assert.IsTrue(result.HasError);
            StringAssert.Contains("JSON", result.ErrorMessage);
            Assert.AreEqual(0, result.Manifest.regions.Count);
        }

        /// <summary>
        /// 创建 JSON-driven region 提取测试用预览变更。
        /// </summary>
        private static UguiNodeChange CreatePreviewChange(
            string name,
            string parentPath,
            System.Collections.Generic.List<string> componentTypes,
            bool hasSourceBounds,
            Rect sourceBounds,
            Vector2 sourceCanvasSize,
            string marker,
            string spriteHint,
            string regionId)
        {
            return new UguiNodeChange
            {
                Descriptor = new UguiNodeDescriptor
                {
                    Name = name,
                    ParentPath = parentPath,
                    ComponentTypeNames = componentTypes,
                    HasSourceBounds = hasSourceBounds,
                    SourceBounds = sourceBounds,
                    SourceCanvasSize = sourceCanvasSize,
                    Visuals = new UiNodeVisuals
                    {
                        AssetMarker = marker,
                        SpriteHint = spriteHint,
                        RegionId = regionId
                    }
                }
            };
        }

        /// <summary>
        /// 创建测试用源图。
        /// </summary>
        private static Texture2D CreateSourceTexture()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = "AutoSliceSource" };
            var pixels = Enumerable.Repeat(new Color32(128, 64, 32, 255), 64).ToArray();
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// 写入一个 8x8 alpha mask，其中有效区域为 x=2..4, bottom-y=1..3。
        /// </summary>
        private static void WriteMask(string path)
        {
            var mask = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), 64).ToArray();
            for (int y = 1; y <= 3; y++)
            {
                for (int x = 2; x <= 4; x++)
                {
                    pixels[y * 8 + x] = new Color32(255, 255, 255, 255);
                }
            }
            mask.SetPixels32(pixels);
            mask.Apply(false, false);
            File.WriteAllBytes(path, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
        }
    }
}
#endif
