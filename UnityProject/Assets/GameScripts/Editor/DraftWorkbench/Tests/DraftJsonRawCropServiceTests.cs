#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="DraftJsonRawCropService"/> 的 EditMode 单元测试。
    /// </summary>
    [TestFixture]
    public class DraftJsonRawCropServiceTests
    {
        private const string SourceHash = "unit_test_raw_crop";
        private const string SecondarySourceHash = "unit_test_raw_crop_marker";

        [TearDown]
        public void TearDown()
        {
            DeleteRawCropDirectory(SourceHash);
            DeleteRawCropDirectory(SecondarySourceHash);
        }

        /// <summary>
        /// 验证 raw crop 会写入 Library 缓存目录，并把 region 的 raw 路径和生成状态记录回来。
        /// </summary>
        [Test]
        public void GenerateRawCrops_WritesRawPngAndRecordsPath()
        {
            var sourceTexture = CreateSourceTexture(4, 4);
            var region = CreateRegion(
                "r001",
                "icon_coin",
                new DraftSegmentBounds { x = 1, y = 1, width = 2, height = 2 });
            var manifest = CreateManifest(SourceHash, region);

            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);

            string expectedPath = CombineRawPath(SourceHash, "r001_icon_coin.png");
            string absolutePath = DraftPathUtility.ToAbsolutePath(expectedPath);

            Assert.IsTrue(File.Exists(absolutePath), "raw PNG 应写入 Library 缓存目录");
            Assert.AreEqual(expectedPath, region.rawPngPath, "region 应记录 raw PNG 路径");
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, region.generationStatus);
            Assert.AreEqual("raw PNG 生成成功。", region.generationMessage);

            var rawTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(rawTexture.LoadImage(File.ReadAllBytes(absolutePath)), "raw PNG 应可重新载入");
            Assert.AreEqual(2, rawTexture.width);
            Assert.AreEqual(2, rawTexture.height);

            Object.DestroyImmediate(rawTexture);
            Object.DestroyImmediate(sourceTexture);
        }

        /// <summary>
        /// 验证忽略区域会被跳过，而失败区域会保留既有 review 数据并记录失败原因。
        /// </summary>
        [Test]
        public void GenerateRawCrops_SkipsIgnoredRegionAndKeepsReviewDataOnFailure()
        {
            var sourceTexture = CreateSourceTexture(4, 4);
            var ignoredRegion = CreateRegion(
                "rIgnore",
                "icon_ignore",
                new DraftSegmentBounds { x = 0, y = 0, width = 2, height = 2 });
            ignoredRegion.included = false;
            ignoredRegion.reviewState = DraftRegionReviewState.Ignored;
            ignoredRegion.reviewReason = "用户忽略该 region。";

            var failedRegion = CreateRegion(
                "rFailed",
                "icon_failed",
                new DraftSegmentBounds { x = 0, y = 0, width = 0, height = 2 });
            failedRegion.reviewState = DraftRegionReviewState.NeedsReview;
            failedRegion.reviewReason = "existing review reason";

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                sourceWidth = 4,
                sourceHeight = 4,
                regions = new List<DraftSegmentRegion> { ignoredRegion, failedRegion }
            };

            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);

            Assert.AreEqual(DraftRegionGenerationStatus.Skipped, ignoredRegion.generationStatus);
            Assert.AreEqual("用户忽略该 region。", ignoredRegion.generationMessage);
            Assert.IsFalse(File.Exists(DraftPathUtility.ToAbsolutePath(CombineRawPath(SourceHash, "rIgnore_icon_ignore.png"))));

            Assert.AreEqual(DraftRegionGenerationStatus.Failed, failedRegion.generationStatus);
            StringAssert.Contains("无效", failedRegion.generationMessage);
            Assert.AreEqual("existing review reason", failedRegion.reviewReason, "失败不应覆盖既有 reviewReason");
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, failedRegion.reviewState);

            Object.DestroyImmediate(sourceTexture);
        }

        /// <summary>
        /// 验证 marker 编辑会改变后续 raw 输出文件名。
        /// </summary>
        [Test]
        public void GenerateRawCrops_MarkerEditChangesOutputFileName()
        {
            var sourceTexture = CreateSourceTexture(4, 4);
            var region = CreateRegion(
                "r001",
                "icon_coin",
                new DraftSegmentBounds { x = 1, y = 1, width = 2, height = 2 });
            var manifest = CreateManifest(SecondarySourceHash, region);

            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);
            Assert.AreEqual(CombineRawPath(SecondarySourceHash, "r001_icon_coin.png"), region.rawPngPath);

            region.marker = "icon_star";
            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);

            string expectedPath = CombineRawPath(SecondarySourceHash, "r001_icon_star.png");
            Assert.AreEqual(expectedPath, region.rawPngPath, "marker 修改后输出名应同步变化");
            Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(expectedPath)));

            Object.DestroyImmediate(sourceTexture);
        }

        /// <summary>
        /// 验证重叠 region 也会各自生成独立的 raw PNG 文件。
        /// </summary>
        [Test]
        public void GenerateRawCrops_OverlappingRegionsProduceDistinctRawFiles()
        {
            var sourceTexture = CreateSourceTexture(4, 4);
            var firstRegion = CreateRegion(
                "r001",
                "icon_coin",
                new DraftSegmentBounds { x = 0, y = 0, width = 2, height = 2 });
            var secondRegion = CreateRegion(
                "r002",
                "icon_star",
                new DraftSegmentBounds { x = 1, y = 1, width = 2, height = 2 });

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SecondarySourceHash,
                sourceWidth = 4,
                sourceHeight = 4,
                regions = new List<DraftSegmentRegion> { firstRegion, secondRegion }
            };

            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);

            string firstPath = CombineRawPath(SecondarySourceHash, "r001_icon_coin.png");
            string secondPath = CombineRawPath(SecondarySourceHash, "r002_icon_star.png");

            Assert.AreEqual(DraftRegionGenerationStatus.Generated, firstRegion.generationStatus);
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, secondRegion.generationStatus);
            Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(firstPath)));
            Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(secondPath)));

            Object.DestroyImmediate(sourceTexture);
        }

        /// <summary>
        /// 非透明 JSON region 显式设置 padding 时，应扩大 raw crop，但不覆盖 UI 放置用 cropBounds。
        /// </summary>
        [Test]
        public void GenerateRawCrops_ExplicitPaddingExpandsOpaqueRawCropWithoutChangingPlacementBounds()
        {
            var sourceTexture = CreateSourceTexture(8, 8);
            var region = CreateRegion(
                "r001",
                "panel",
                new DraftSegmentBounds { x = 3, y = 3, width = 2, height = 2 });
            region.assetKind = DraftAssetKind.Panel;
            region.alphaMode = DraftAlphaMode.OpaqueRect;
            region.paddingPixels = 1;
            var manifest = CreateManifest(SourceHash, region);

            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);

            Assert.AreEqual(new Rect(2, 2, 4, 4), region.expandedBbox.ToRect());
            Assert.AreEqual(new Rect(3, 3, 2, 2), region.cropBounds.ToRect());
            Assert.AreEqual(4, region.rawOutputWidth);
            Assert.AreEqual(4, region.rawOutputHeight);

            Object.DestroyImmediate(sourceTexture);
        }

        private static DraftSegmentationManifest CreateManifest(string sourceHash, DraftSegmentRegion region)
        {
            return new DraftSegmentationManifest
            {
                sourceHash = sourceHash,
                sourceWidth = 4,
                sourceHeight = 4,
                regions = new List<DraftSegmentRegion> { region }
            };
        }

        private static DraftSegmentRegion CreateRegion(string id, string marker, DraftSegmentBounds bounds)
        {
            return new DraftSegmentRegion
            {
                id = id,
                marker = marker,
                label = "Coin",
                sourceKind = DraftRegionSourceKind.JsonDescriptor,
                nodePath = "Root/Icon",
                sourceCanvasWidth = 4,
                sourceCanvasHeight = 4,
                sourceBounds = new DraftSegmentBounds { x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height },
                cropBounds = new DraftSegmentBounds { x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height },
                bbox = new DraftSegmentBounds { x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height },
                expandedBbox = new DraftSegmentBounds { x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height },
                included = true,
                reviewState = DraftRegionReviewState.Included
            };
        }

        private static Texture2D CreateSourceTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 128, 64, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static string CombineRawPath(string sourceHash, string fileName)
        {
            return (DraftJsonRawCropService.DefaultRawCropRoot.TrimEnd('/') + "/" + sourceHash + "/" + fileName).Replace('\\', '/');
        }

        private static void DeleteRawCropDirectory(string sourceHash)
        {
            string absolutePath = DraftPathUtility.ToAbsolutePath(DraftJsonRawCropService.DefaultRawCropRoot + "/" + sourceHash);
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, true);
            }
        }
    }
}

#endif
