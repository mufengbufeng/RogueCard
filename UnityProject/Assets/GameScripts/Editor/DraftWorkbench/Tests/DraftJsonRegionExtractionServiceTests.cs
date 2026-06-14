#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// JSON-driven Auto Slice region 提取服务测试。
    /// </summary>
    [TestFixture]
    public class DraftJsonRegionExtractionServiceTests
    {
        /// <summary>
        /// ExtractFromPreviewChanges 应从 Image descriptor 提取稳定 region，并保留 marker / spriteHint / regionId 语义。
        /// </summary>
        [Test]
        public void ExtractFromPreviewChanges_提取ImageDescriptor并保留语义()
        {
            var changes = new List<UguiNodeChange>
            {
                CreateChange(
                    "CoinIcon",
                    "Root",
                    new List<string> { "UnityEngine.UI.Image" },
                    true,
                    new Rect(100, 120, 64, 64),
                    new Vector2(1920, 1080),
                    "coin_marker",
                    "coin_hint",
                    "coin_region")
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hash123",
                "Assets/Art/design.png",
                1920,
                1080);

            Assert.AreEqual(1, result.Manifest.regions.Count);
            Assert.AreEqual(0, result.SkippedDescriptors.Count);
            var region = result.Manifest.regions.Single();
            Assert.AreEqual(DraftRegionSourceKind.JsonDescriptor, region.sourceKind);
            Assert.AreEqual("coin_region", region.id);
            Assert.AreEqual("coin_marker", region.marker);
            Assert.AreEqual("coin_hint", region.spriteHint);
            Assert.AreEqual("Root/CoinIcon", region.nodePath);
            Assert.AreEqual(1920, region.sourceCanvasWidth);
            Assert.AreEqual(1080, region.sourceCanvasHeight);
            Assert.AreEqual(100, region.sourceBounds.x);
            Assert.AreEqual(120, region.sourceBounds.y);
            Assert.AreEqual(64, region.sourceBounds.width);
            Assert.AreEqual(64, region.sourceBounds.height);
            Assert.AreEqual(100, region.cropBounds.x);
            Assert.AreEqual(64, region.cropBounds.width);
            Assert.IsTrue(region.included);
            Assert.AreEqual(DraftRegionReviewState.Included, region.reviewState);
            Assert.AreEqual(DraftRegionGenerationStatus.Pending, region.generationStatus);
        }

        /// <summary>
        /// ExtractFromPreviewChanges 应跳过非 Image、无 source bounds 和冲突 descriptor，并记录原因。
        /// </summary>
        [Test]
        public void ExtractFromPreviewChanges_跳过不可切图Descriptor并记录原因()
        {
            var changes = new List<UguiNodeChange>
            {
                CreateChange("TitleText", "Root", new List<string> { "UnityEngine.UI.Text" }, true, new Rect(0, 0, 100, 20), new Vector2(1920, 1080), "", "", ""),
                CreateChange("BoundlessImage", "Root", new List<string> { "UnityEngine.UI.Image" }, false, Rect.zero, new Vector2(1920, 1080), "", "", ""),
                CreateChange("ConflictImage", "Root", new List<string> { "UnityEngine.UI.Image" }, true, new Rect(0, 0, 20, 20), new Vector2(1920, 1080), "", "", "", true)
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hash123",
                "Assets/Art/design.png",
                1920,
                1080);

            Assert.AreEqual(0, result.Manifest.regions.Count);
            Assert.AreEqual(3, result.SkippedDescriptors.Count);
            StringAssert.Contains("Image", result.SkippedDescriptors[0].Reason);
            StringAssert.Contains("source bounds", result.SkippedDescriptors[1].Reason);
            StringAssert.Contains("冲突", result.SkippedDescriptors[2].Reason);
        }

        /// <summary>
        /// 创建测试用预览变更。
        /// </summary>
        private static UguiNodeChange CreateChange(
            string name,
            string parentPath,
            List<string> componentTypes,
            bool hasSourceBounds,
            Rect sourceBounds,
            Vector2 sourceCanvasSize,
            string marker,
            string spriteHint,
            string regionId,
            bool hasConflict = false)
        {
            return new UguiNodeChange
            {
                HasConflict = hasConflict,
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
    }
}
#endif
