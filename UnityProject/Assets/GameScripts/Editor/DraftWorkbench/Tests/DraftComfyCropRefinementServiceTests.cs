#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="DraftComfyCropRefinementService"/> 的 EditMode 单元测试。
    /// </summary>
    [TestFixture]
    public class DraftComfyCropRefinementServiceTests
    {
        private const string SourceHash = "refine_test";

        [TearDown]
        public void TearDown()
        {
            DeleteDirectory(DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash);
            DeleteDirectory(ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash);
        }

        /// <summary>
        /// 验证 raw crop 能通过 refinement transport 生成 refined PNG，并记录尺寸变化与复核状态。
        /// </summary>
        [Test]
        public async Task RefineSelectedRawCrops_WritesRefinedPngAndMarksReviewOnSizeChange()
        {
            string rawRelativePath = DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash + "/r001_icon_coin.png";
            string rawAbsolutePath = DraftPathUtility.ToAbsolutePath(rawRelativePath);
            WritePng(rawAbsolutePath, 2, 2, new Color32(255, 128, 64, 255));

            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                label = "coin",
                rawPngPath = rawRelativePath,
                cropBounds = new DraftSegmentBounds { x = 0, y = 0, width = 2, height = 2 },
                included = true,
                reviewState = DraftRegionReviewState.Included
            };

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                regions = new List<DraftSegmentRegion> { region }
            };

            var transport = new TestComfyUiTransport
            {
                HistoryJson = "{\"2\":{\"outputs\":{\"images\":[{\"filename\":\"refined.png\",\"subfolder\":\"output\",\"type\":\"output\"}]}}}",
                OutputBytes = CreatePngBytes(4, 3, new Color32(0, 255, 0, 255))
            };

            List<DraftCropRefinementRecord> records = await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(
                manifest,
                ScriptableObject.CreateInstance<ComfyUiServiceConfig>(),
                transport);

            string expectedRelativePath = ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash + "/refined/DW_" + SourceHash + "_r001_icon_coin_refined.png";

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("r001_icon_coin.png", Path.GetFileName(transport.LastUploadedPath));
            StringAssert.Contains("LoadImage", transport.LastPromptJson);
            StringAssert.Contains("SaveImage", transport.LastPromptJson);
            Assert.IsFalse(transport.LastPromptJson.Contains("SAM3"));

            Assert.AreEqual(DraftRegionGenerationStatus.Generated, region.generationStatus);
            Assert.AreEqual(expectedRelativePath, region.refinedPngPath);
            Assert.AreEqual(4, region.refinedWidth);
            Assert.AreEqual(3, region.refinedHeight);
            Assert.IsTrue(region.requiresReview);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, region.reviewState);
            StringAssert.Contains("不一致", region.reviewReason);

            Assert.AreEqual(expectedRelativePath, records[0].RefinedPngPath);
            Assert.AreEqual(4, records[0].RefinedWidth);
            Assert.AreEqual(3, records[0].RefinedHeight);
            Assert.IsTrue(records[0].RequiresReview);
            StringAssert.Contains("不一致", records[0].GenerationMessage);
        }

        /// <summary>
        /// 验证 refinement history 解析会忽略 prompt 输入里的 images 参数，只读取 outputs 段的输出图片。
        /// </summary>
        [Test]
        public async Task RefineSelectedRawCrops_UsesOutputImagesFromHistory()
        {
            string rawRelativePath = DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash + "/r003_icon_sun.png";
            string rawAbsolutePath = DraftPathUtility.ToAbsolutePath(rawRelativePath);
            WritePng(rawAbsolutePath, 2, 2, new Color32(255, 200, 0, 255));

            var region = new DraftSegmentRegion
            {
                id = "r003",
                marker = "icon_sun",
                label = "sun",
                rawPngPath = rawRelativePath,
                cropBounds = new DraftSegmentBounds { x = 0, y = 0, width = 2, height = 2 },
                included = true,
                reviewState = DraftRegionReviewState.Included
            };

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                regions = new List<DraftSegmentRegion> { region }
            };

            var transport = new TestComfyUiTransport
            {
                HistoryJson = @"{""prompt-1"": {""prompt"": [32, ""prompt-1"", {""1"": {""class_type"": ""LoadImage"", ""inputs"": {""image"": ""input.png""}}, ""2"": {""class_type"": ""SaveImage"", ""inputs"": {""images"": [""1"", 0], ""filename_prefix"": ""DW_debug_r003""}}}, {""client_id"": ""client"", ""create_time"": 0}, [""2""]], ""outputs"": {""2"": {""images"": [{""filename"": ""refined.png"", ""subfolder"": ""output"", ""type"": ""output""}]}}, ""status"": {""status_str"": ""success"", ""completed"": true, ""messages"": [[""execution_start"", {""prompt_id"": ""prompt-1""}], [""execution_success"", {""prompt_id"": ""prompt-1""}]]}}}",
                OutputBytes = CreatePngBytes(2, 2, new Color32(0, 128, 255, 255))
            };

            List<DraftCropRefinementRecord> records = await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(
                manifest,
                ScriptableObject.CreateInstance<ComfyUiServiceConfig>(),
                transport);

            string expectedRelativePath = ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash + "/refined/DW_" + SourceHash + "_r003_icon_sun_refined.png";

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, region.generationStatus);
            Assert.AreEqual(expectedRelativePath, region.refinedPngPath);
            Assert.AreEqual(expectedRelativePath, records[0].RefinedPngPath);
            Assert.AreEqual(2, region.refinedWidth);
            Assert.AreEqual(2, region.refinedHeight);
            Assert.IsFalse(string.IsNullOrWhiteSpace(region.refinedPngPath));
            Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(expectedRelativePath)));
        }

        /// <summary>
        /// 验证 transport 失败不会清空已有 raw crop 记录。
        /// </summary>
        [Test]
        public async Task RefineSelectedRawCrops_TransportFailurePreservesRawCropData()
        {
            string rawRelativePath = DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash + "/r002_icon_star.png";
            string rawAbsolutePath = DraftPathUtility.ToAbsolutePath(rawRelativePath);
            WritePng(rawAbsolutePath, 2, 2, new Color32(255, 128, 64, 255));

            var region = new DraftSegmentRegion
            {
                id = "r002",
                marker = "icon_star",
                label = "star",
                rawPngPath = rawRelativePath,
                cropBounds = new DraftSegmentBounds { x = 0, y = 0, width = 2, height = 2 },
                included = true,
                reviewState = DraftRegionReviewState.Included
            };

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                regions = new List<DraftSegmentRegion> { region }
            };

            var transport = new TestComfyUiTransport
            {
                FailOnQueuePrompt = true
            };

            List<DraftCropRefinementRecord> records = await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(
                manifest,
                ScriptableObject.CreateInstance<ComfyUiServiceConfig>(),
                transport);

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(DraftRegionGenerationStatus.Failed, region.generationStatus);
            Assert.AreEqual(rawRelativePath, region.rawPngPath);
            Assert.IsTrue(region.requiresReview);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, region.reviewState);
            StringAssert.Contains("boom", region.generationMessage);
            Assert.AreEqual(DraftRegionGenerationStatus.Failed, records[0].GenerationStatus);
        }

        private static void WritePng(string absolutePath, int width, int height, Color32 color)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            byte[] bytes = CreatePngBytes(width, height, color);
            File.WriteAllBytes(absolutePath, bytes);
        }

        private static byte[] CreatePngBytes(int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return bytes;
        }

        private static void DeleteDirectory(string relativePath)
        {
            string absolutePath = DraftPathUtility.ToAbsolutePath(relativePath);
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, true);
            }
        }

    }
}

#endif
