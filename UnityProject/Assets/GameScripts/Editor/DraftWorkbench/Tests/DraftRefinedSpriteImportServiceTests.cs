#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// <see cref="DraftRefinedSpriteImportService"/> 的 EditMode 单元测试。
    /// </summary>
    [TestFixture]
    public class DraftRefinedSpriteImportServiceTests
    {
        private const string SourceHash = "sprite_import_test";

        [TearDown]
        public void TearDown()
        {
            DeleteLibraryDirectory(DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash);
            DeleteLibraryDirectory(ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash);
            DeleteLibraryDirectory(DraftGenerationReportStore.DefaultReportRoot + "/" + SourceHash);
            DeleteAssetPath("Assets/AssetRaw/Image/DraftWorkbenchGenerated/TestPrefab/" + SourceHash);
            DeleteAssetPath("Assets/AssetRaw/Image/DraftWorkbenchGenerated/Standalone/" + SourceHash);
        }

        /// <summary>
        /// 验证 refined PNG 会被复制到 final Sprite 输出目录并成功导入为 Single Sprite。
        /// </summary>
        [Test]
        public void ImportRefinedSprites_WritesFinalSpriteAsset()
        {
            string refinedRelativePath = ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash + "/refined/r001_icon_coin_refined.png";
            string refinedAbsolutePath = DraftPathUtility.ToAbsolutePath(refinedRelativePath);
            WritePng(refinedAbsolutePath, 3, 2, new Color32(0, 255, 0, 255));

            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                label = "coin",
                refinedPngPath = refinedRelativePath,
                sourceBounds = new DraftSegmentBounds { x = 10, y = 20, width = 3, height = 2 },
                bbox = new DraftSegmentBounds { x = 10, y = 20, width = 3, height = 2 },
                expandedBbox = new DraftSegmentBounds { x = 8, y = 18, width = 7, height = 6 },
                confidence = 1f,
                included = true,
                reviewState = DraftRegionReviewState.Included
            };

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                regions = new List<DraftSegmentRegion> { region }
            };

            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            DraftSpriteImportReport report = DraftRefinedSpriteImportService.ImportRefinedSprites(manifest, config, "TestPrefab");

            string expectedAssetPath = "Assets/AssetRaw/Image/DraftWorkbenchGenerated/TestPrefab/" + SourceHash + "/DW_" + SourceHash + "_r001_icon_coin.png";

            Assert.IsTrue(report.HasTargetPrefab);
            Assert.IsFalse(report.PrefabModified);
            Assert.AreEqual("TestPrefab", report.PrefabName);
            Assert.AreEqual(expectedAssetPath.Replace('\\', '/'), report.GeneratedSprites[0].SpriteAssetPath);
            Assert.AreEqual(refinedRelativePath, report.GeneratedSprites[0].RefinedPngPath);
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, report.GeneratedSprites[0].GenerationStatus);
            Assert.AreEqual(expectedAssetPath.Replace('\\', '/'), region.spriteAssetPath);
            Assert.IsFalse(string.IsNullOrEmpty(region.spriteGuid));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(expectedAssetPath));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// 验证 asset-only 导入会显式报告没有 prefab 被修改。
        /// </summary>
        [Test]
        public void ImportRefinedSprites_AssetOnlyModeReportsNoPrefabModification()
        {
            string refinedRelativePath = ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash + "/refined/r002_icon_star_refined.png";
            string refinedAbsolutePath = DraftPathUtility.ToAbsolutePath(refinedRelativePath);
            WritePng(refinedAbsolutePath, 2, 2, new Color32(0, 255, 0, 255));

            var region = new DraftSegmentRegion
            {
                id = "r002",
                marker = "icon_star",
                label = "star",
                refinedPngPath = refinedRelativePath,
                sourceBounds = new DraftSegmentBounds { x = 1, y = 2, width = 2, height = 2 },
                bbox = new DraftSegmentBounds { x = 1, y = 2, width = 2, height = 2 },
                expandedBbox = new DraftSegmentBounds { x = 0, y = 1, width = 4, height = 4 },
                confidence = 1f,
                included = true,
                reviewState = DraftRegionReviewState.Included
            };

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                regions = new List<DraftSegmentRegion> { region }
            };

            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            DraftSpriteImportReport report = DraftRefinedSpriteImportService.ImportRefinedSprites(manifest, config, null);

            string expectedAssetPath = "Assets/AssetRaw/Image/DraftWorkbenchGenerated/Standalone/" + SourceHash + "/DW_" + SourceHash + "_r002_icon_star.png";

            Assert.IsFalse(report.HasTargetPrefab);
            Assert.IsFalse(report.PrefabModified);
            StringAssert.Contains("不修改 prefab", string.Join("\n", report.Messages));
            Assert.AreEqual(expectedAssetPath.Replace('\\', '/'), report.GeneratedSprites[0].SpriteAssetPath);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(expectedAssetPath));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// 验证 raw / refined / Sprite 三段结果会被汇总到 editor-only 生成报告中。
        /// </summary>
        [Test]
        public async Task GenerationReport_ContainsRawRefinedAndSpriteRecords()
        {
            var sourceTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 128, 64, 255);
            }

            sourceTexture.SetPixels32(pixels);
            sourceTexture.Apply(false, false);

            var region = new DraftSegmentRegion
            {
                id = "r003",
                marker = "icon_heart",
                label = "heart",
                sourceKind = DraftRegionSourceKind.JsonDescriptor,
                nodePath = "Root/Heart",
                sourceCanvasWidth = 4,
                sourceCanvasHeight = 4,
                sourceBounds = new DraftSegmentBounds { x = 1, y = 1, width = 2, height = 2 },
                cropBounds = new DraftSegmentBounds { x = 1, y = 1, width = 2, height = 2 },
                included = true,
                reviewState = DraftRegionReviewState.Included
            };

            var manifest = new DraftSegmentationManifest
            {
                sourceHash = SourceHash,
                regions = new List<DraftSegmentRegion> { region }
            };

            DraftJsonRawCropService.GenerateRawCrops(sourceTexture, manifest);

            var transport = new TestComfyUiTransport
            {
                HistoryJson = "{\"2\":{\"outputs\":{\"images\":[{\"filename\":\"refined.png\",\"subfolder\":\"output\",\"type\":\"output\"}]}}}",
                OutputBytes = CreatePngBytes(3, 3, new Color32(0, 255, 0, 255))
            };

            await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(manifest, ScriptableObject.CreateInstance<ComfyUiServiceConfig>(), transport);
            DraftRefinedSpriteImportService.ImportRefinedSprites(manifest, ScriptableObject.CreateInstance<ComfyUiServiceConfig>(), "TestPrefab");

            string reportPath = DraftPathUtility.ToAbsolutePath(DraftGenerationReportStore.DefaultReportRoot + "/" + SourceHash + "/generation-report.json");
            Assert.IsTrue(File.Exists(reportPath));

            string json = File.ReadAllText(reportPath);
            var document = JsonUtility.FromJson<DraftGenerationReportDocument>(json);

            Assert.AreEqual(SourceHash, document.SourceHash);
            Assert.AreEqual(1, document.RawRegions.Count);
            Assert.AreEqual(1, document.RefinementRecords.Count);
            Assert.AreEqual(1, document.SpriteImportReport.GeneratedSprites.Count);
            Assert.AreEqual(region.rawPngPath, document.RawRegions[0].rawPngPath);
            Assert.AreEqual(region.refinedPngPath, document.RefinementRecords[0].RefinedPngPath);
            Assert.AreEqual(region.spriteAssetPath, document.SpriteImportReport.GeneratedSprites[0].SpriteAssetPath);

            Object.DestroyImmediate(sourceTexture);
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

        private static void DeleteLibraryDirectory(string relativePath)
        {
            string absolutePath = DraftPathUtility.ToAbsolutePath(relativePath);
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, true);
            }
        }

        private static void DeleteAssetPath(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            string absolutePath = DraftPathUtility.ToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                    // 忽略残留目录删除失败，避免测试清理阻塞。
                }
            }
        }
    }
}

#endif
