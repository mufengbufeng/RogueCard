#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// AiVisionClient 纯逻辑单元测试，覆盖 ExtractJson JSON 提取、
    /// CalculateScaledDimensions 缩放尺寸计算和 AI 服务配置归一化。
    /// </summary>
    [TestFixture]
    public class AiVisionClientTests
    {
        // ──────────────────────── AiServiceConfig ────────────────────────

        [TestCase(0)]
        [TestCase(-5)]
        public void NormalizeTimeoutSeconds_NonPositiveValue_ReturnsDefault(int timeoutSeconds)
        {
            int result = AiServiceConfig.NormalizeTimeoutSeconds(timeoutSeconds);
            Assert.AreEqual(AiServiceConfig.DefaultTimeoutSeconds, result);
        }

        [Test]
        public void NormalizeTimeoutSeconds_BelowMinimum_ClampsToMinimum()
        {
            int result = AiServiceConfig.NormalizeTimeoutSeconds(AiServiceConfig.MinTimeoutSeconds - 1);
            Assert.AreEqual(AiServiceConfig.MinTimeoutSeconds, result);
        }

        [Test]
        public void NormalizeTimeoutSeconds_AboveMaximum_ClampsToMaximum()
        {
            int result = AiServiceConfig.NormalizeTimeoutSeconds(AiServiceConfig.MaxTimeoutSeconds + 1);
            Assert.AreEqual(AiServiceConfig.MaxTimeoutSeconds, result);
        }

        [Test]
        public void NormalizeTimeoutSeconds_ValidValue_ReturnsValue()
        {
            const int timeoutSeconds = 600;
            int result = AiServiceConfig.NormalizeTimeoutSeconds(timeoutSeconds);
            Assert.AreEqual(timeoutSeconds, result);
        }

        // ──────────────────────── ExtractJson ────────────────────────

        /// <summary>
        /// 输入已经是合法 JSON 字符串时，应原样返回。
        /// </summary>
        [Test]
        public void ExtractJson_DirectJson_ReturnsAsIs()
        {
            const string input = "{\"canvas\":{\"width\":1080}}";
            string result = AiVisionClient.ExtractJson(input);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// JSON 被包裹在 ```json 代码块中时，应正确提取出 JSON 内容。
        /// </summary>
        [Test]
        public void ExtractJson_JsonInCodeBlock_ExtractsCorrectly()
        {
            const string input = "Here is the result:\n```json\n{\"canvas\":{\"width\":1080}}\n```";
            const string expected = "{\"canvas\":{\"width\":1080}}";
            string result = AiVisionClient.ExtractJson(input);
            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// JSON 前后有空白字符时，应去除首尾空白后返回。
        /// </summary>
        [Test]
        public void ExtractJson_JsonWithLeadingWhitespace_ExtractsCorrectly()
        {
            const string input = "\n\n  {\"canvas\":{\"width\":1080}}  \n";
            const string expected = "{\"canvas\":{\"width\":1080}}";
            string result = AiVisionClient.ExtractJson(input);
            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// 输入文本中不包含任何 JSON 结构时，应返回空字符串。
        /// </summary>
        [Test]
        public void ExtractJson_NoJson_ReturnsEmpty()
        {
            const string input = "This is just plain text with no JSON";
            string result = AiVisionClient.ExtractJson(input);
            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>
        /// 没有 ``` 代码块但文本中嵌有花括号 JSON 时，应回退到花括号匹配提取。
        /// </summary>
        [Test]
        public void ExtractJson_MalformedCodeBlock_FallsBackToBraceMatching()
        {
            const string input = "Result: {\"canvas\": {\"width\": 1080, \"height\": 1920}} done";
            string result = AiVisionClient.ExtractJson(input);
            Assert.That(result, Does.Contain("canvas"),
                "花括号匹配提取结果应包含 canvas 字段");
        }

        /// <summary>
        /// Responses API 返回 content.type=text 的文本片段时，应按 Responses 解析器提取内容。
        /// </summary>
        [Test]
        public void ExtractContentAuto_ResponsesTextContent_ReturnsText()
        {
            const string expected = "{\"canvas\":{\"width\":1080}}";
            const string response = "{\"object\":\"response\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"{\\\"canvas\\\":{\\\"width\\\":1080}}\"}]}]}";

            string result = AiVisionClient.ExtractContentAuto(response, preferResponsesApi: true);

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Responses API 代理省略 object 字段时，应通过 output 结构自动识别为 Responses 格式。
        /// </summary>
        [Test]
        public void ExtractContentAuto_OutputWithoutObject_ReturnsText()
        {
            const string expected = "{\"canvas\":{\"width\":1080}}";
            const string response = "{\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"canvas\\\":{\\\"width\\\":1080}}\"}]}]}";

            string result = AiVisionClient.ExtractContentAuto(response, preferResponsesApi: false);

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Responses API 返回顶层 output_text 时，应直接提取该字段。
        /// </summary>
        [Test]
        public void ExtractContentAuto_TopLevelOutputText_ReturnsText()
        {
            const string expected = "{\"canvas\":{\"width\":1080}}";
            const string response = "{\"output_text\":\"{\\\"canvas\\\":{\\\"width\\\":1080}}\",\"output\":[]}";

            string result = AiVisionClient.ExtractContentAuto(response, preferResponsesApi: false);

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Responses API 返回完整元数据且文本在较深层时，应通过字段扫描兜底提取 JSON 文本。
        /// </summary>
        [Test]
        public void ExtractContentAuto_MetadataRichResponses_ReturnsNestedText()
        {
            const string expected = "```json\n{\"canvas\":{\"width\":1080},\"root\":{\"type\":\"container\"}}\n```";
            const string response = "{\"id\":\"resp_test\",\"object\":\"response\",\"created_at\":1780719312,\"status\":\"completed\",\"background\":false,\"completed_at\":1780719391,\"error\":null,\"frequency_penalty\":0,\"output\":[{\"id\":\"rs_test\",\"type\":\"reasoning\",\"summary\":[]},{\"id\":\"msg_test\",\"type\":\"message\",\"status\":\"completed\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"annotations\":[],\"text\":\"```json\\n{\\\"canvas\\\":{\\\"width\\\":1080},\\\"root\\\":{\\\"type\\\":\\\"container\\\"}}\\n```\"}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}";

            string result = AiVisionClient.ExtractContentAuto(response, preferResponsesApi: true);

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// Responses API 代理将消息内容放在 content 字符串时，应提取该内容。
        /// </summary>
        [Test]
        public void ExtractContentAuto_ResponseContentString_ReturnsContent()
        {
            const string expected = "{\"canvas\":{\"width\":768},\"root\":{\"type\":\"container\"}}";
            const string response = "{\"id\":\"resp_test\",\"object\":\"response\",\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"{\\\"canvas\\\":{\\\"width\\\":768},\\\"root\\\":{\\\"type\\\":\\\"container\\\"}}\"}]}";

            string result = AiVisionClient.ExtractContentAuto(response, preferResponsesApi: true);

            Assert.AreEqual(expected, result);
        }

        /// <summary>
        /// UI 结构分析请求应由底层固定生成 Image-To-UI 约束，不读取可配置提示词。
        /// </summary>
        [Test]
        public void BuildUiStructureResponsesRequestBody_UsesBuiltInInstructions()
        {
            var config = ScriptableObject.CreateInstance<AiServiceConfig>();
            config.Model = "gpt-5";
            config.UseJsonMode = true;

            string body = AiVisionClient.BuildUiStructureResponsesRequestBody(
                config,
                "base64-image",
                768,
                1376);

            Assert.That(body, Does.Contain("\"model\":\"gpt-5\""));
            Assert.That(body, Does.Contain("\"instructions\":"));
            Assert.That(body, Does.Contain("ui_structure.json"));
            Assert.That(body, Does.Contain("\"unity\""));
            Assert.That(body, Does.Contain("assetGuid"));
            Assert.That(body, Does.Contain("spriteName"));
            Assert.That(body, Does.Contain("画布尺寸为 768x1376"));
            Assert.That(body, Does.Contain("\"text\":{\"format\":{\"type\":\"json_object\"}}"));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// UI 结构分析请求应把 Image-To-UI 的 prefab/sprite handoff 上下文写进用户输入，
        /// 让底层实现稳定约束 unity.outputPrefabPath 与 unity.spriteRootFolder。
        /// </summary>
        [Test]
        public void BuildUiStructureResponsesRequestBody_UsesHandoffContext()
        {
            var config = ScriptableObject.CreateInstance<AiServiceConfig>();
            config.Model = "gpt-5";
            config.UseJsonMode = true;

            var context = new UiStructureRequestContext
            {
                OutputPrefabPath = "Assets/UI/Main/MainView.prefab",
                SpriteRootFolder = "Assets/AssetRaw/Image/DraftWorkbenchGenerated/MainView",
                CanvasName = "MainView"
            };

            string body = AiVisionClient.BuildUiStructureResponsesRequestBody(
                config,
                "base64-image",
                768,
                1376,
                context);

            Assert.That(body, Does.Contain("Assets/UI/Main/MainView.prefab"));
            Assert.That(body, Does.Contain("Assets/AssetRaw/Image/DraftWorkbenchGenerated/MainView"));
            Assert.That(body, Does.Contain("canvas.name 建议使用 MainView"));
            Assert.That(body, Does.Contain("duplicate basenames"));
            Assert.That(body, Does.Contain("button_or_panel"));
            Assert.That(body, Does.Contain("TransparentForeground"));
            Assert.That(body, Does.Contain("PreserveSourceAlpha"));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// Chat Completions UI 结构分析请求同样应由底层固定生成 Image-To-UI 约束，并输出合法 JSON 模式配置。
        /// </summary>
        [Test]
        public void BuildUiStructureChatRequestBody_UsesBuiltInInstructions()
        {
            var config = ScriptableObject.CreateInstance<AiServiceConfig>();
            config.Model = "gpt-5";
            config.UseJsonMode = true;
            config.UseResponsesApiFormat = false;

            string body = AiVisionClient.BuildUiStructureChatRequestBody(
                config,
                "base64-image",
                768,
                1376);

            Assert.That(body, Does.Contain("\"model\":\"gpt-5\""));
            Assert.That(body, Does.Contain("\"messages\":["));
            Assert.That(body, Does.Contain("\"role\":\"system\""));
            Assert.That(body, Does.Contain("ui_structure.json"));
            Assert.That(body, Does.Contain("\"unity\""));
            Assert.That(body, Does.Contain("assetGuid"));
            Assert.That(body, Does.Contain("spriteName"));
            Assert.That(body, Does.Contain("画布尺寸为 768x1376"));
            Assert.That(body, Does.Contain("\"response_format\":{\"type\":\"json_object\"}"));
            Assert.That(body, Does.Not.Contain("\"response_format\":{{"));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// 未提供 handoff 上下文时，底层应按画布名推导 Image-To-UI 默认 prefab 输出路径。
        /// </summary>
        [Test]
        public void BuildUiStructureChatRequestBody_DefaultsOutputPrefabPathFromCanvasName()
        {
            var config = ScriptableObject.CreateInstance<AiServiceConfig>();
            config.Model = "gpt-5";

            var context = new UiStructureRequestContext
            {
                CanvasName = "Battle HUD"
            };

            string body = AiVisionClient.BuildUiStructureChatRequestBody(
                config,
                "base64-image",
                1920,
                1080,
                context);

            Assert.That(body, Does.Contain("Assets/Image-To-UI/Battle_HUD.prefab"));
            Assert.That(body, Does.Contain("spriteRootFolder 未指定时输出空字符串"));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// Responses image_generation 请求体应携带 tools 与 tool_choice 配置。
        /// </summary>
        [Test]
        public void BuildImageGenerationResponsesRequestBody_IncludesToolConfiguration()
        {
            var config = ScriptableObject.CreateInstance<AiServiceConfig>();
            config.Model = "gpt-5";

            var options = new ImageGenerationOptions
            {
                Instructions = "You are an image generator.",
                Size = "1536x1024",
                Quality = "high",
                Background = "opaque",
                OutputFormat = "png",
                ForceToolChoice = true
            };

            string body = AiVisionClient.BuildImageGenerationResponsesRequestBody(
                config,
                "Generate a blue square.",
                options);

            Assert.That(body, Does.Contain("\"model\":\"gpt-5\""));
            Assert.That(body, Does.Contain("\"tools\":[{\"type\":\"image_generation\""));
            Assert.That(body, Does.Contain("\"size\":\"1536x1024\""));
            Assert.That(body, Does.Contain("\"quality\":\"high\""));
            Assert.That(body, Does.Contain("\"background\":\"opaque\""));
            Assert.That(body, Does.Contain("\"format\":\"png\""));
            Assert.That(body, Does.Contain("\"tool_choice\":{\"type\":\"image_generation\"}"));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// Responses API 返回 image_generation_call 时，应能提取 Base64 图片并解码为贴图。
        /// </summary>
        [Test]
        public void ExtractGeneratedImageResultFromResponsesApi_DecodesTexture()
        {
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            source.SetPixel(0, 0, Color.blue);
            source.Apply();
            string onePixelPngBase64 = System.Convert.ToBase64String(source.EncodeToPNG());
            string response = $"{{\"output\":[{{\"type\":\"image_generation_call\",\"revised_prompt\":\"blue square\",\"result\":\"{onePixelPngBase64}\"}}]}}";

            GeneratedImageResult result = AiVisionClient.ExtractGeneratedImageResultFromResponsesApi(response);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Image);
            Assert.AreEqual(onePixelPngBase64, result.ImageBase64);
            Assert.AreEqual("blue square", result.RevisedPrompt);
            Assert.AreEqual(1, result.Image.width);
            Assert.AreEqual(1, result.Image.height);

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result.Image);
        }

        // ──────────────────────── CalculateScaledDimensions ────────────────────────

        /// <summary>
        /// 纹理尺寸未超过 maxSize 时，应返回原始尺寸不做缩放。
        /// </summary>
        [Test]
        public void CalculateScaledDimensions_SmallSize_ReturnsOriginal()
        {
            var (w, h) = AiVisionClient.CalculateScaledDimensions(100, 100, 2048);
            Assert.AreEqual(100, w, "宽度应保持不变");
            Assert.AreEqual(100, h, "高度应保持不变");
        }

        /// <summary>
        /// 纹理宽度超过 maxSize 时，应按比例缩放使最大边等于 maxSize。
        /// </summary>
        [Test]
        public void CalculateScaledDimensions_LargeWidth_ScalesDown()
        {
            // 3000x2000, maxSize=2048 → scale=2048/3000 → newW=2048, newH≈1365
            var (w, h) = AiVisionClient.CalculateScaledDimensions(3000, 2000, 2048);
            Assert.AreEqual(2048, w, "宽度应缩放到 maxSize");
            Assert.AreEqual(1365, h, "高度应按比例缩放");
        }

        /// <summary>
        /// 纹理高度超过 maxSize 时，应按比例缩放使最大边等于 maxSize。
        /// </summary>
        [Test]
        public void CalculateScaledDimensions_LargeHeight_ScalesDown()
        {
            // 2000x3000, maxSize=2048 → scale=2048/3000 → newW≈1365, newH=2048
            var (w, h) = AiVisionClient.CalculateScaledDimensions(2000, 3000, 2048);
            Assert.AreEqual(1365, w, "宽度应按比例缩放");
            Assert.AreEqual(2048, h, "高度应缩放到 maxSize");
        }

        /// <summary>
        /// 极端小尺寸输入时，缩放结果仍应至少为 1x1。
        /// </summary>
        [Test]
        public void CalculateScaledDimensions_MinSize_ClampsToOne()
        {
            var (w, h) = AiVisionClient.CalculateScaledDimensions(1, 1, 2048);
            Assert.AreEqual(1, w, "最小宽度应为 1");
            Assert.AreEqual(1, h, "最小高度应为 1");
        }
    }
}

#endif
