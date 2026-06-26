#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// ComfyUI frontend workflow inspection 与 patching 的 EditMode 测试。
    /// </summary>
    [TestFixture]
    public class ComfyWorkflowAdapterTests
    {
        /// <summary>
        /// InspectWorkflowJson 应识别 LoadImage、SAM3/subgraph 与 preview/output 节点。
        /// </summary>
        [Test]
        public void InspectWorkflowJson_识别前端Workflow核心节点()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            string workflowJson = @"{
  ""nodes"": [
    { ""id"": 79, ""type"": ""LoadImage"", ""widgets_values"": [""old.png"", ""image""] },
    { ""id"": 99, ""type"": ""6e7ab3ea-96aa-470f-9b94-3d9d0e01f481"", ""title"": ""Image Segment (SAM3)"", ""widgets_values"": [""background tree"", 0.3, 1, false, ""sam3.1_multiplex_fp16.safetensors""] },
    { ""id"": 95, ""type"": ""MaskPreview"" },
    { ""id"": 106, ""type"": ""PreviewImage"" }
  ]
}";

            var result = ComfyWorkflowAdapter.InspectWorkflowJson(workflowJson, config);

            Assert.IsTrue(result.HasLoadImageNode, string.Join("\n", result.Diagnostics));
            Assert.IsTrue(result.HasSam3Node, string.Join("\n", result.Diagnostics));
            Assert.IsTrue(result.HasOutputNode, string.Join("\n", result.Diagnostics));
            Assert.AreEqual("79", result.LoadImageNodeId);
            Assert.AreEqual("99", result.Sam3NodeId);
            Assert.IsTrue(result.OutputNodeIds.Contains("95"));
            Assert.IsTrue(result.IsUsableForDetection);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// PatchWorkflowJson 应按配置更新上传图片、SAM3 prompt、阈值、迭代次数、individual masks 与输出前缀。
        /// </summary>
        [Test]
        public void PatchWorkflowJson_更新LoadImage和SAM3参数()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.LoadImageNodeId = "79";
            config.Sam3NodeId = "99";
            config.OutputNodeId = "106";
            config.DetectionPrompt = "icon coin button";
            config.MaskThreshold = 0.72f;
            config.RefineIterations = 3;
            config.IndividualMasks = true;

            string workflowJson = @"{
  ""nodes"": [
    { ""id"": 79, ""type"": ""LoadImage"", ""widgets_values"": [""old.png"", ""image""] },
    { ""id"": 99, ""type"": ""SAM3_Detect"", ""widgets_values"": [""old prompt"", 0.3, 1, false, ""sam3.1_multiplex_fp16.safetensors""] },
    { ""id"": 106, ""type"": ""SaveImage"", ""widgets_values"": [""old_prefix""] }
  ]
}";

            var context = new ComfyWorkflowPatchContext
            {
                InputImagePath = "C:/temp/design.png",
                InputAssetPath = "Assets/Art/design.png",
                OutputPrefix = "DW_hash_prefab",
                OutputDirectory = "C:/cache/hash",
                ManifestPath = "C:/cache/hash/DW_hash_prefab.manifest.json",
                SourceHash = "abc123",
                SourceWidth = 1920,
                SourceHeight = 1080,
                MinRegionArea = 64
            };

            string patched = ComfyWorkflowAdapter.PatchWorkflowJson(workflowJson, config, context);

            StringAssert.Contains("C:/temp/design.png", patched);
            StringAssert.Contains("icon coin button", patched);
            StringAssert.Contains("0.72", patched);
            StringAssert.Contains("3", patched);
            StringAssert.Contains("true", patched);
            StringAssert.Contains("DW_hash_prefab", patched);
            Assert.IsFalse(patched.Contains("old.png"));
            Assert.IsFalse(patched.Contains("old prompt"));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// InspectWorkflowJson 在缺少关键节点时应返回可操作诊断，而不是静默失败。
        /// </summary>
        [Test]
        public void InspectWorkflowJson_缺失LoadImage返回诊断()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            string workflowJson = @"{ ""nodes"": [ { ""id"": 1, ""type"": ""PreviewImage"" } ] }";

            var result = ComfyWorkflowAdapter.InspectWorkflowJson(workflowJson, config);

            Assert.IsFalse(result.IsUsableForDetection);
            Assert.IsTrue(result.Diagnostics.Any(message => message.Contains("LoadImage")));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// refinement workflow 只需要 LoadImage 和 SaveImage，不应要求 SAM3 节点。
        /// </summary>
        [Test]
        public void InspectWorkflowJson_RefinementWorkflow_IsUsableForRefinement()
        {
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            string workflowJson = @"{
  ""nodes"": [
    { ""id"": 1, ""type"": ""LoadImage"", ""widgets_values"": [""raw.png"", ""image""] },
    { ""id"": 2, ""type"": ""SaveImage"", ""widgets_values"": [""refined_prefix""] }
  ]
}";

            var result = ComfyWorkflowAdapter.InspectWorkflowJson(workflowJson, config);

            Assert.IsTrue(result.HasLoadImageNode);
            Assert.IsFalse(result.HasSam3Node);
            Assert.IsTrue(result.HasOutputNode);
            Assert.IsFalse(result.IsUsableForDetection);
            Assert.IsTrue(result.IsUsableForRefinement);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// refinement prompt 只应包含 raw crop 输入和 SaveImage 输出，不应出现 SAM3 检测链路。
        /// </summary>
        [Test]
        public void BuildRefinementApiPromptJson_OnlyContainsLoadImageAndSaveImage()
        {
            string promptJson = ComfyWorkflowAdapter.BuildRefinementApiPromptJson("raw.png", "DW_refine_test");

            StringAssert.Contains("LoadImage", promptJson);
            StringAssert.Contains("SaveImage", promptJson);
            Assert.IsFalse(promptJson.Contains("SAM3", System.StringComparison.Ordinal));
        }
    }
}
#endif
