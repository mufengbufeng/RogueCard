#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// DraftSpriteAssignmentService 的匹配与审批规则测试。
    /// </summary>
    [TestFixture]
    public class DraftSpriteAssignmentServiceTests
    {
        /// <summary>
        /// BuildAssignments 应选择最佳未使用候选，而不是因最佳候选已被使用而跳过节点。
        /// </summary>
        [Test]
        public void BuildAssignments_选择最佳未使用候选()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.AssignmentAutoApproveConfidence = 0.1f;
            var changes = new List<UguiNodeChange>
            {
                CreateImageChange("CoinIconImg", "coin", "r001", new Rect(0, 0, 10, 10)),
                CreateImageChange("GemIconImg", "gem", "r002", new Rect(1, 1, 10, 10))
            };
            var sprites = new List<DraftGeneratedSprite>
            {
                new DraftGeneratedSprite { RegionId = "r001", Marker = "coin", SpriteAssetPath = "Assets/coin.png", Confidence = 1f, SourceBounds = new Rect(0, 0, 10, 10) },
                new DraftGeneratedSprite { RegionId = "r002", Marker = "gem", SpriteAssetPath = "Assets/gem.png", Confidence = 1f, SourceBounds = new Rect(1, 1, 10, 10) }
            };

            var assignments = DraftSpriteAssignmentService.BuildAssignments(null, changes, sprites, config);

            Assert.AreEqual(2, assignments.Count);
            Assert.AreEqual("r001", assignments[0].RegionId);
            Assert.AreEqual("r002", assignments[1].RegionId);
            StringAssert.Contains("regionId", assignments[1].MatchReason);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// 已有 Sprite 或低置信度匹配应要求人工批准。
        /// </summary>
        [Test]
        public void BuildAssignments_低置信度需要复核且未批准()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.AssignmentAutoApproveConfidence = 0.9f;
            var changes = new List<UguiNodeChange>
            {
                CreateImageChange("CoinIconImg", "coin", "", new Rect(0, 0, 10, 10))
            };
            var sprites = new List<DraftGeneratedSprite>
            {
                new DraftGeneratedSprite { RegionId = "r001", Marker = "coin", SpriteAssetPath = "Assets/coin.png", Confidence = 0.5f, SourceBounds = new Rect(0, 0, 10, 10) }
            };

            var assignment = DraftSpriteAssignmentService.BuildAssignments(null, changes, sprites, config).Single();

            Assert.IsTrue(assignment.RequiresReview);
            Assert.IsFalse(assignment.Approved);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, assignment.ReviewState);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// JSON-driven 切图生成的稳定 regionId 应回写到 descriptor，避免泛 marker 和缩放坐标导致应用到错误节点。
        /// </summary>
        [Test]
        public void BuildAssignments_JsonDrivenRegionId保持切图对应位置()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.AssignmentAutoApproveConfidence = 0.1f;
            var changes = new List<UguiNodeChange>
            {
                CreateImageChange("LeftIcon", "image", string.Empty, new Rect(100, 100, 100, 100), new Vector2(1920, 1080)),
                CreateImageChange("RightIcon", "image", string.Empty, new Rect(1200, 700, 100, 100), new Vector2(1920, 1080))
            };

            var extraction = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(
                changes,
                "hash_scaled",
                "Assets/Design.png",
                960,
                540);
            var sprites = new List<DraftGeneratedSprite>
            {
                new DraftGeneratedSprite
                {
                    RegionId = extraction.Manifest.regions[1].id,
                    Marker = extraction.Manifest.regions[1].marker,
                    SpriteAssetPath = "Assets/right.png",
                    Confidence = 1f,
                    SourceBounds = extraction.Manifest.regions[1].bbox.ToRect()
                },
                new DraftGeneratedSprite
                {
                    RegionId = extraction.Manifest.regions[0].id,
                    Marker = extraction.Manifest.regions[0].marker,
                    SpriteAssetPath = "Assets/left.png",
                    Confidence = 1f,
                    SourceBounds = extraction.Manifest.regions[0].bbox.ToRect()
                }
            };

            var assignments = DraftSpriteAssignmentService.BuildAssignments(null, changes, sprites, config);

            Assert.AreEqual("r001", changes[0].Descriptor.Visuals.RegionId);
            Assert.AreEqual("r002", changes[1].Descriptor.Visuals.RegionId);
            Assert.AreEqual("r001", assignments.Single(a => a.NodePath == "LeftIcon").RegionId);
            Assert.AreEqual("r002", assignments.Single(a => a.NodePath == "RightIcon").RegionId);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// 创建包含 Image 组件和 Auto Slice 语义的预览变更。
        /// </summary>
        private static UguiNodeChange CreateImageChange(string name, string marker, string regionId, Rect sourceBounds)
        {
            return CreateImageChange(name, marker, regionId, sourceBounds, Vector2.zero);
        }

        /// <summary>
        /// 创建包含 Image 组件、Auto Slice 语义和 source canvas 尺寸的预览变更。
        /// </summary>
        private static UguiNodeChange CreateImageChange(string name, string marker, string regionId, Rect sourceBounds, Vector2 sourceCanvasSize)
        {
            return new UguiNodeChange
            {
                Descriptor = new UguiNodeDescriptor
                {
                    Name = name,
                    ComponentTypeNames = new List<string> { "UnityEngine.UI.Image" },
                    Visuals = new UiNodeVisuals
                    {
                        AssetMarker = marker,
                        SpriteHint = marker,
                        RegionId = regionId
                    },
                    SourceBounds = sourceBounds,
                    HasSourceBounds = true,
                    SourceCanvasSize = sourceCanvasSize
                }
            };
        }
    }
}
#endif
