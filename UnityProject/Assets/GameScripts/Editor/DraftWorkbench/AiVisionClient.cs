#if UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// UI 结构分析请求的底层上下文，用于把 Image-To-UI handoff 约束写入请求体。
    /// </summary>
    public sealed class UiStructureRequestContext
    {
        /// <summary>
        /// 期望写入 ui_structure.json 的 canvas.name；为空时由输出路径或默认值推断。
        /// </summary>
        public string CanvasName;

        /// <summary>
        /// 期望写入 unity.outputPrefabPath 的 Prefab 输出路径。
        /// </summary>
        public string OutputPrefabPath;

        /// <summary>
        /// 期望写入 unity.spriteRootFolder 的 Sprite 根目录。
        /// </summary>
        public string SpriteRootFolder;
    }

    /// <summary>
    /// Responses API `image_generation` 工具的调用选项。
    /// </summary>
    public sealed class ImageGenerationOptions
    {
        /// <summary>
        /// 可选的 Responses API instructions；为空时不发送。
        /// </summary>
        public string Instructions = string.Empty;

        /// <summary>
        /// 期望输出尺寸，例如 1024x1024 / 1024x1536。
        /// </summary>
        public string Size = "1024x1024";

        /// <summary>
        /// 输出质量：low / medium / high / auto。
        /// </summary>
        public string Quality = "medium";

        /// <summary>
        /// 背景模式：opaque / transparent / auto。
        /// </summary>
        public string Background = "opaque";

        /// <summary>
        /// 输出格式：png / jpeg / webp。
        /// </summary>
        public string OutputFormat = "png";

        /// <summary>
        /// 是否强制模型调用 image_generation 工具。
        /// </summary>
        public bool ForceToolChoice = true;
    }

    /// <summary>
    /// 生图请求的结果对象。
    /// </summary>
    public sealed class GeneratedImageResult
    {
        /// <summary>
        /// 生成出的贴图。
        /// </summary>
        public Texture2D Image { get; }

        /// <summary>
        /// 原始 Base64 图片数据。
        /// </summary>
        public string ImageBase64 { get; }

        /// <summary>
        /// 模型改写后的提示词；如果响应未返回则为空字符串。
        /// </summary>
        public string RevisedPrompt { get; }

        /// <summary>
        /// 原始 Responses API 响应文本。
        /// </summary>
        public string RawResponse { get; }

        /// <summary>
        /// 构造一个生图结果对象。
        /// </summary>
        public GeneratedImageResult(Texture2D image, string imageBase64, string revisedPrompt, string rawResponse)
        {
            Image = image;
            ImageBase64 = imageBase64 ?? string.Empty;
            RevisedPrompt = revisedPrompt ?? string.Empty;
            RawResponse = rawResponse ?? string.Empty;
        }
    }

    /// <summary>
    /// HTTP 客户端，用于在 Unity Editor 中调用 OpenAI 兼容的视觉 AI API，
    /// 将设计图发送给 AI 并返回 ui_structure JSON。
    /// <para>使用 EditorApplication.update 轮询网络请求，不依赖 UniTask PlayerLoop。</para>
    /// </summary>
    public static class AiVisionClient
    {
        /// <summary>
        /// 最大自动重试次数。
        /// </summary>
        private const int MaxRetries = 3;

        /// <summary>
        /// 基础重试延迟（秒），实际延迟为 BaseRetryDelaySeconds × 2^retryCount（指数退避）。
        /// </summary>
        private const float BaseRetryDelaySeconds = 2f;

        /// <summary>
        /// Responses API 生图的默认尺寸。
        /// </summary>
        private const string DefaultImageGenerationSize = "1024x1024";

        /// <summary>
        /// Responses API 生图的默认质量。
        /// </summary>
        private const string DefaultImageGenerationQuality = "medium";

        /// <summary>
        /// Responses API 生图的默认背景模式。
        /// </summary>
        private const string DefaultImageGenerationBackground = "opaque";

        /// <summary>
        /// Responses API 生图的默认输出格式。
        /// </summary>
        private const string DefaultImageGenerationOutputFormat = "png";

        /// <summary>
        /// Responses API 响应根对象，仅提取本客户端需要的字段。
        /// </summary>
        [Serializable]
        private sealed class ResponsesApiResponse
        {
            public ResponsesApiOutputItem[] output;
            public string output_text;
        }

        /// <summary>
        /// Responses API output 数组元素，仅提取文本输出和 image_generation_call 需要的字段。
        /// </summary>
        [Serializable]
        private sealed class ResponsesApiOutputItem
        {
            public string type;
            public string text;
            public string result;
            public string revised_prompt;
            public ResponsesApiContentItem[] content;
        }

        /// <summary>
        /// Responses API message.content 元素，仅提取 output_text 所需字段。
        /// </summary>
        [Serializable]
        private sealed class ResponsesApiContentItem
        {
            public string type;
            public string text;
        }

        /// <summary>
        /// 判断 HTTP 状态码是否为可重试的瞬态错误（503/502/429/504）。
        /// </summary>
        private static bool IsTransientHttpError(long statusCode)
        {
            return statusCode == 429 || statusCode == 502 || statusCode == 503 || statusCode == 504;
        }

        /// <summary>
        /// 在 Editor 中延迟执行指定操作（基于 EditorApplication.update 轮询计时）。
        /// </summary>
        private static void ScheduleDelayedAction(float delaySeconds, Action action)
        {
            double targetTime = EditorApplication.timeSinceStartup + delaySeconds;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (EditorApplication.timeSinceStartup < targetTime) return;
                EditorApplication.update -= wait;
                action();
            };
            EditorApplication.update += wait;
        }

        /// <summary>
        /// 将设计图发送给 AI 视觉模型，获取 ui_structure JSON 字符串（回调模式）。
        /// <para>
        /// 流程：缩放图片 → 编码为 base64 → 构造请求体 → POST 发送 →
        /// 通过 EditorApplication.update 轮询等待 → 解析响应 → 回调返回。
        /// </para>
        /// </summary>
        /// <param name="image">待分析的设计图 Texture2D。</param>
        /// <param name="config">AI 服务配置。</param>
        /// <param name="canvasWidth">画布宽度（像素）。</param>
        /// <param name="canvasHeight">画布高度（像素）。</param>
        /// <param name="onSuccess">成功回调，参数为 AI 返回的 JSON 字符串。</param>
        /// <param name="onError">失败回调，参数为异常对象。</param>
        public static void GenerateUiStructure(
            Texture2D image,
            AiServiceConfig config,
            int canvasWidth,
            int canvasHeight,
            Action<string> onSuccess,
            Action<Exception> onError)
        {
            GenerateUiStructure(image, config, canvasWidth, canvasHeight, null, onSuccess, onError);
        }

        /// <summary>
        /// 将设计图发送给 AI 视觉模型，获取 ui_structure JSON 字符串，并带入 Image-To-UI handoff 上下文。
        /// </summary>
        /// <param name="image">待分析的设计图 Texture2D。</param>
        /// <param name="config">AI 服务配置。</param>
        /// <param name="canvasWidth">画布宽度（像素）。</param>
        /// <param name="canvasHeight">画布高度（像素）。</param>
        /// <param name="context">Image-To-UI handoff 上下文；为 null 时使用底层默认值。</param>
        /// <param name="onSuccess">成功回调，参数为 AI 返回的 JSON 字符串。</param>
        /// <param name="onError">失败回调，参数为异常对象。</param>
        public static void GenerateUiStructure(
            Texture2D image,
            AiServiceConfig config,
            int canvasWidth,
            int canvasHeight,
            UiStructureRequestContext context,
            Action<string> onSuccess,
            Action<Exception> onError)
        {
            if (image == null) { onError?.Invoke(new ArgumentNullException(nameof(image))); return; }
            if (config == null) { onError?.Invoke(new ArgumentNullException(nameof(config))); return; }
            if (string.IsNullOrEmpty(config.ApiKey))
            { onError?.Invoke(new Exception("AI 服务配置错误：ApiKey 为空。")); return; }
            if (string.IsNullOrEmpty(config.Endpoint))
            { onError?.Invoke(new Exception("AI 服务配置错误：Endpoint 为空。")); return; }
            if (string.IsNullOrEmpty(config.Model))
            { onError?.Invoke(new Exception("AI 服务配置错误：Model 为空。")); return; }

            string base64;
            try
            {
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(image);
                if (string.IsNullOrEmpty(assetPath))
                    throw new Exception("无法获取图片资源路径。");

                byte[] fileBytes = LoadImageBytes(assetPath);
                if (fileBytes == null || fileBytes.Length == 0)
                    throw new Exception("图片文件读取结果为空。");

                base64 = Convert.ToBase64String(fileBytes);
                Debug.Log($"[AiVisionClient] 从磁盘读取图片: {assetPath}, 大小: {fileBytes.Length} bytes");
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
                return;
            }

            string url = config.Endpoint.TrimEnd('/');
            bool isResponsesApi = config.UseResponsesApiFormat;
            int timeoutSeconds = AiServiceConfig.NormalizeTimeoutSeconds(config.TimeoutSeconds);

            string body = isResponsesApi
                ? BuildUiStructureResponsesRequestBody(config, base64, canvasWidth, canvasHeight, context)
                : BuildUiStructureChatRequestBody(config, base64, canvasWidth, canvasHeight, context);

            string bodyPreview = body.Length > 500 ? body.Substring(0, 500) + "..." : body;
            Debug.Log($"[AiVisionClient] 发送请求到: {url}, 模式: {(isResponsesApi ? "Responses" : "Chat Completions")}, timeout: {timeoutSeconds}s, body长度: {body.Length}\n请求体预览: {bodyPreview}");

            SendJsonRequest(
                url,
                config.ApiKey,
                timeoutSeconds,
                body,
                responseText => ExtractContentAuto(responseText, isResponsesApi),
                onSuccess,
                onError);
        }

        /// <summary>
        /// 通过 Responses API 的 <c>image_generation</c> 工具生成图片。
        /// </summary>
        /// <param name="prompt">用户提示词。</param>
        /// <param name="config">AI 服务配置，仅使用 Endpoint / ApiKey / Model / TimeoutSeconds。</param>
        /// <param name="onSuccess">成功回调，返回生成的贴图与响应元数据。</param>
        /// <param name="onError">失败回调。</param>
        public static void GenerateImage(
            string prompt,
            AiServiceConfig config,
            Action<GeneratedImageResult> onSuccess,
            Action<Exception> onError)
        {
            GenerateImage(prompt, config, null, onSuccess, onError);
        }

        /// <summary>
        /// 通过 Responses API 的 <c>image_generation</c> 工具生成图片。
        /// </summary>
        /// <param name="prompt">用户提示词。</param>
        /// <param name="config">AI 服务配置，仅使用 Endpoint / ApiKey / Model / TimeoutSeconds。</param>
        /// <param name="options">生图工具选项。</param>
        /// <param name="onSuccess">成功回调，返回生成的贴图与响应元数据。</param>
        /// <param name="onError">失败回调。</param>
        public static void GenerateImage(
            string prompt,
            AiServiceConfig config,
            ImageGenerationOptions options,
            Action<GeneratedImageResult> onSuccess,
            Action<Exception> onError)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                onError?.Invoke(new ArgumentException("prompt 不能为空。", nameof(prompt)));
                return;
            }

            if (config == null)
            {
                onError?.Invoke(new ArgumentNullException(nameof(config)));
                return;
            }

            if (string.IsNullOrEmpty(config.ApiKey))
            {
                onError?.Invoke(new Exception("AI 服务配置错误：ApiKey 为空。"));
                return;
            }

            if (string.IsNullOrEmpty(config.Endpoint))
            {
                onError?.Invoke(new Exception("AI 服务配置错误：Endpoint 为空。"));
                return;
            }

            if (string.IsNullOrEmpty(config.Model))
            {
                onError?.Invoke(new Exception("AI 服务配置错误：Model 为空。"));
                return;
            }

            ValidateImageGenerationModel(config.Model);

            options ??= new ImageGenerationOptions();

            string url = config.Endpoint.TrimEnd('/');
            int timeoutSeconds = AiServiceConfig.NormalizeTimeoutSeconds(config.TimeoutSeconds);
            string body = BuildImageGenerationResponsesRequestBody(config, prompt, options);
            string bodyPreview = body.Length > 500 ? body.Substring(0, 500) + "..." : body;

            Debug.Log($"[AiVisionClient] 发送 image_generation 请求到: {url}, timeout: {timeoutSeconds}s, body长度: {body.Length}\n请求体预览: {bodyPreview}");

            SendJsonRequest(
                url,
                config.ApiKey,
                timeoutSeconds,
                body,
                ExtractGeneratedImageResultFromResponsesApi,
                onSuccess,
                onError);
        }

        /// <summary>
        /// 发送 JSON POST 请求并解析响应，支持瞬态错误重试与统一的 HTML/空响应校验。
        /// </summary>
        private static void SendJsonRequest<T>(
            string url,
            string apiKey,
            int timeoutSeconds,
            string body,
            Func<string, T> responseParser,
            Action<T> onSuccess,
            Action<Exception> onError)
        {
            int retryCount = 0;

            Action sendRequest = null;
            sendRequest = () =>
            {
                UnityWebRequest request;
                try
                {
                    request = new UnityWebRequest(url, "POST");
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    request.timeout = timeoutSeconds;
                }
                catch (Exception e)
                {
                    onError?.Invoke(new Exception($"构造请求失败: {e.Message}", e));
                    return;
                }

                UnityWebRequestAsyncOperation operation;
                try
                {
                    operation = request.SendWebRequest();
                }
                catch (Exception e)
                {
                    request.Dispose();
                    onError?.Invoke(new Exception($"发送请求失败: {e.Message}", e));
                    return;
                }

                EditorApplication.CallbackFunction poll = null;
                poll = () =>
                {
                    if (!operation.isDone) return;

                    EditorApplication.update -= poll;
                    Debug.Log($"[AiVisionClient] 请求完成: result={request.result}, code={request.responseCode}");

                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            if (IsTransientHttpError(request.responseCode) && retryCount < MaxRetries)
                            {
                                retryCount++;
                                float delay = BaseRetryDelaySeconds * (float)Math.Pow(2, retryCount - 1);
                                Debug.LogWarning($"[AiVisionClient] 瞬态错误 {(int)request.responseCode}，{delay:F1}s 后自动重试 ({retryCount}/{MaxRetries})...");
                                ScheduleDelayedAction(delay, sendRequest);
                                return;
                            }

                            string errorMsg = string.IsNullOrEmpty(request.error)
                                ? $"HTTP {(int)request.responseCode}"
                                : request.error;
                            onError?.Invoke(new Exception(
                                $"AI API 请求失败：{errorMsg}\n响应：{request.downloadHandler?.text}"));
                            return;
                        }

                        string responseText = request.downloadHandler?.text;
                        if (string.IsNullOrEmpty(responseText))
                        {
                            onError?.Invoke(new Exception("AI API 返回了空响应。"));
                            return;
                        }

                        string trimmedResponse = responseText.TrimStart();
                        if (trimmedResponse.StartsWith("<!", StringComparison.OrdinalIgnoreCase) ||
                            trimmedResponse.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                        {
                            onError?.Invoke(new Exception(
                                $"Endpoint 返回了 HTML 网页而非 API 响应，请检查 URL 配置。\n" +
                                $"当前请求 URL: {url}\n" +
                                $"提示: 请填写完整 API 路径，例如:\n" +
                                $"  OpenAI Chat Completions: https://api.openai.com/v1/chat/completions\n" +
                                $"  OpenAI Responses: https://api.openai.com/v1/responses\n" +
                                $"  本地代理 Responses: http://127.0.0.1:8080/responses"));
                            return;
                        }

                        T parsedResponse = responseParser(responseText);
                        onSuccess?.Invoke(parsedResponse);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke(e);
                    }
                    finally
                    {
                        request.Dispose();
                    }
                };

                EditorApplication.update += poll;
            };

            sendRequest();
        }

        /// <summary>
        /// 测试与 AI 服务的连接是否正常（回调模式）。
        /// </summary>
        /// <param name="config">AI 服务配置。</param>
        /// <param name="onResult">结果回调，参数为是否连接成功。</param>
        public static void TestConnection(AiServiceConfig config, Action<bool> onResult)
        {
            if (config == null || string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.ApiKey))
            {
                onResult?.Invoke(false);
                return;
            }

            string url = config.Endpoint.TrimEnd('/') + "/models";
            int timeoutSeconds = AiServiceConfig.NormalizeTimeoutSeconds(config.TimeoutSeconds);

            UnityWebRequest request;
            try
            {
                request = new UnityWebRequest(url, "GET");
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", $"Bearer {config.ApiKey}");
                request.timeout = timeoutSeconds;
            }
            catch
            {
                onResult?.Invoke(false);
                return;
            }

            var operation = request.SendWebRequest();

            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!operation.isDone) return;
                EditorApplication.update -= poll;

                try
                {
                    onResult?.Invoke(request.responseCode == 200);
                }
                catch
                {
                    onResult?.Invoke(false);
                }
                finally
                {
                    request.Dispose();
                }
            };

            EditorApplication.update += poll;
        }

        /// <summary>
        /// 从 AI 响应字符串中提取 JSON 内容。
        /// <para>
        /// 依次尝试：直接作为 JSON → 提取 markdown 代码块 → 查找首尾花括号。
        /// </para>
        /// </summary>
        /// <param name="aiResponse">AI 返回的原始响应字符串。</param>
        /// <returns>提取到的 JSON 字符串；若提取失败则返回空字符串。</returns>
        public static string ExtractJson(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse))
                return string.Empty;

            string trimmed = aiResponse.Trim();

            // 1. 尝试直接作为 JSON（以 '{' 开头）
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return trimmed;
            }

            // 2. 提取 ```json ... ``` 代码块
            var jsonBlockMatch = Regex.Match(
                trimmed,
                @"```json\s*\n?(.*?)\n?\s*```",
                RegexOptions.Singleline);
            if (jsonBlockMatch.Success)
            {
                return jsonBlockMatch.Groups[1].Value.Trim();
            }

            // 3. 提取任意 ``` ... ``` 代码块
            var codeBlockMatch = Regex.Match(
                trimmed,
                @"```\s*\n?(.*?)\n?\s*```",
                RegexOptions.Singleline);
            if (codeBlockMatch.Success)
            {
                string content = codeBlockMatch.Groups[1].Value.Trim();
                if (content.StartsWith("{") || content.StartsWith("["))
                    return content;
            }

            // 4. 查找第一个 '{' 和最后一个 '}'
            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return string.Empty;
        }

        /// <summary>
        /// 根据最大边长计算缩放后的纹理尺寸，保持宽高比。
        /// </summary>
        public static (int width, int height) CalculateScaledDimensions(int width, int height, int maxSize)
        {
            if (width <= maxSize && height <= maxSize)
                return (width, height);

            float scale = (float)maxSize / Mathf.Max(width, height);
            int newWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int newHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));

            return (newWidth, newHeight);
        }

        /// <summary>
        /// 缩放 Texture2D，使较长边不超过 maxSize，保持宽高比。
        /// </summary>
        private static Texture2D ScaleTexture(Texture2D source, int maxSize)
        {
            if (source == null) return null;

            int width = source.width;
            int height = source.height;
            var (newWidth, newHeight) = CalculateScaledDimensions(width, height, maxSize);

            if (newWidth == width && newHeight == height)
                return source;

            var renderTexture = RenderTexture.GetTemporary(newWidth, newHeight, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            renderTexture.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, renderTexture);

            var prevRT = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(renderTexture);

            return result;
        }

        /// <summary>
        /// 构造 Chat Completions API 请求体。
        /// </summary>
        private static string BuildChatRequestBody(
            AiServiceConfig config,
            string builtInInstructions,
            string base64,
            string userText)
        {
            var sb = new StringBuilder(2048);
            sb.Append('{');
            sb.Append($"\"model\":\"{EscapeJsonString(config.Model)}\",");
            sb.Append("\"messages\":[");

            // Chat Completions 需要把底层固定约束放在 system role 中。
            sb.Append($"{{\"role\":\"system\",\"content\":\"{builtInInstructions}\"}},");

            // user message（多模态：图片 + 文本）
            sb.Append("{\"role\":\"user\",\"content\":[");
            sb.Append($"{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/png;base64,{base64}\"}}}}");
            sb.Append(',');
            sb.Append($"{{\"type\":\"text\",\"text\":\"{userText}\"}}");
            sb.Append("]}");

            sb.Append("],");
            sb.Append("\"max_tokens\":4096");

            if (config.UseJsonMode)
            {
                sb.Append(",\"response_format\":{\"type\":\"json_object\"}");
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 构造 Responses API 请求体。
        /// <para>底层固定约束放顶层 instructions 字段（非 input 数组），图片用 input_image 类型。</para>
        /// <para>参考: https://github.com/Wei-Shaw/sub2api/issues/1066</para>
        /// </summary>
        private static string BuildResponsesRequestBody(
            AiServiceConfig config,
            string builtInInstructions,
            string base64,
            string userText)
        {
            var sb = new StringBuilder(2048);
            sb.Append('{');
            sb.Append($"\"model\":\"{EscapeJsonString(config.Model)}\",");

            // 顶层 instructions 字段用于承载底层固定约束。
            sb.Append($"\"instructions\":\"{builtInInstructions}\",");

            // input 数组：只含 user 消息
            sb.Append("\"input\":[");
            sb.Append("{\"role\":\"user\",\"content\":[");

            // 图片（input_image 类型）
            sb.Append($"{{\"type\":\"input_image\",\"image_url\":\"data:image/png;base64,{base64}\"}}");
            sb.Append(',');

            // 文本（input_text 类型）
            sb.Append($"{{\"type\":\"input_text\",\"text\":\"{userText}\"}}");

            sb.Append("]}");
            sb.Append(']');

            if (config.UseJsonMode)
            {
                sb.Append(",\"text\":{\"format\":{\"type\":\"json_object\"}}");
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 构造 UI 结构分析专用的 Responses API 请求体。
        /// </summary>
        internal static string BuildUiStructureResponsesRequestBody(
            AiServiceConfig config,
            string base64,
            int canvasWidth,
            int canvasHeight)
        {
            return BuildUiStructureResponsesRequestBody(config, base64, canvasWidth, canvasHeight, null);
        }

        /// <summary>
        /// 构造 UI 结构分析专用的 Responses API 请求体，并带入 Image-To-UI handoff 上下文。
        /// </summary>
        internal static string BuildUiStructureResponsesRequestBody(
            AiServiceConfig config,
            string base64,
            int canvasWidth,
            int canvasHeight,
            UiStructureRequestContext context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            string builtInInstructions = EscapeJsonString(BuildUiStructureInstructions());
            string userText = EscapeJsonString(BuildUiStructureUserText(canvasWidth, canvasHeight, context));
            string body = BuildResponsesRequestBody(config, builtInInstructions, base64 ?? string.Empty, userText);
            return InsertUiStructureMetadata(body);
        }

        /// <summary>
        /// 构造 UI 结构分析专用的 Chat Completions 请求体。
        /// </summary>
        internal static string BuildUiStructureChatRequestBody(
            AiServiceConfig config,
            string base64,
            int canvasWidth,
            int canvasHeight)
        {
            return BuildUiStructureChatRequestBody(config, base64, canvasWidth, canvasHeight, null);
        }

        /// <summary>
        /// 构造 UI 结构分析专用的 Chat Completions 请求体，并带入 Image-To-UI handoff 上下文。
        /// </summary>
        internal static string BuildUiStructureChatRequestBody(
            AiServiceConfig config,
            string base64,
            int canvasWidth,
            int canvasHeight,
            UiStructureRequestContext context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            string builtInInstructions = EscapeJsonString(BuildUiStructureInstructions());
            string userText = EscapeJsonString(BuildUiStructureUserText(canvasWidth, canvasHeight, context));
            string body = BuildChatRequestBody(config, builtInInstructions, base64 ?? string.Empty, userText);
            return InsertUiStructureMetadata(body);
        }

        /// <summary>
        /// 返回 Draft Workbench UI 结构分析的内置约束，避免运行时配置 prompt 漂移破坏 schema。
        /// </summary>
        private static string BuildUiStructureInstructions()
        {
            return @"你是一个 Unity UGUI 布局分析专家。请分析提供的设计图，识别所有活跃 UI 元素及其层级关系，然后只输出 ui_structure.json。

要求：
1. 只输出 JSON，不要添加任何解释或 markdown 代码块标记。
2. 画布坐标系：左上角为原点 (0,0)，X 向右增长，Y 向下增长。
3. canvas.width 和 canvas.height 必须等于输入设计图实际像素尺寸。
4. 每个元素必须包含 type、name 和 size。position 是可选字段，但只要能稳定定位就应填写。
5. 优先把可见 UI 写成显式 rect/position，而不是完全依赖推导布局；layout 只用于 row / column 的结构语义。
6. container 用于逻辑分组，image 用于 Sprite 图片，rect 用于纯色矩形，text 用于文字，button 用于可交互按钮，overlay 用于半透明遮罩层。
7. text 元素使用 text 或 textContent 与 fontSize；必要时设置 color、alignment、textVAlign、lineHeight、strokeColor、strokeWidth。
8. image/button/overlay 可以使用 nineSlice: true 标记应使用 Sprite border 切片。
9. 只建模活跃的 UI 表面，跳过背后的游戏场景内容。

额外输出要求：
- 顶层必须包含 ""unity"" 对象，字段包括 schemaVersion、outputPrefabPath、spriteRootFolder。
- outputPrefabPath 为空时，按 Assets/Image-To-UI/<canvasName>.prefab 推导；canvasName 优先使用画布 name，其次使用目标 Prefab 名称。
- spriteRootFolder 为空时，保留为空字符串，但如果存在 assetGuid、asset 或 spriteName，仍应尽量写出原始 Unity 资源身份。
- image 节点可包含 assetGuid、spriteName、nineSlice、marker、spriteHint、regionId。
- assetGuid 表示 Unity 资源 GUID；spriteName 表示多 Sprite 贴图中的子 Sprite 名称。
- 需要切图的 icon / decoration 节点应尽量给出精确 source bounds、assetKind、alphaMode、paddingPixels 或 paddingPercent。
- duplicate basenames 存在时，asset 应使用相对路径或更长的可区分路径片段。
- button_or_panel 类似的可拉伸资产应优先使用 nineSlice 或可追溯的 Sprite border 元数据。
- 保存 alpha 通道的前景元素应使用 TransparentForeground；纯背景、面板、bar、frame、button plate 可使用 OpaqueRect。
- 不要依赖外部可编辑提示词，schema 约束必须由底层实现固定。

输出 JSON 示例结构：
{
  ""canvas"": { ""width"": 768, ""height"": 1376, ""name"": ""MainMenu"" },
  ""unity"": { ""schemaVersion"": 1, ""outputPrefabPath"": ""Assets/Image-To-UI/MainMenu.prefab"", ""spriteRootFolder"": ""Assets/UI/Sprites"" },
  ""root"": {
    ""type"": ""container"",
    ""name"": ""Root"",
    ""position"": { ""x"": 0, ""y"": 0 },
    ""size"": { ""width"": 768, ""height"": 1376 },
    ""children"": []
  }
}";
        }

        /// <summary>
        /// 为 UI 结构分析请求附加可检索的 schema 元数据。
        /// </summary>
        private static string InsertUiStructureMetadata(string body)
        {
            const string Metadata = ",\"metadata\":{\"unity\":\"ui_structure\",\"assetGuid\":\"supported\",\"spriteName\":\"supported\"}";
            if (string.IsNullOrEmpty(body) || body[body.Length - 1] != '}')
                return body;

            return body.Substring(0, body.Length - 1) + Metadata + "}";
        }

        /// <summary>
        /// 构造 UI 结构分析请求的用户输入文本，把 Image-To-UI handoff 约束固定进底层实现。
        /// </summary>
        private static string BuildUiStructureUserText(
            int canvasWidth,
            int canvasHeight,
            UiStructureRequestContext context)
        {
            string canvasName = ResolveCanvasName(context);
            string outputPrefabPath = ResolveOutputPrefabPath(context, canvasName);
            string spriteRootFolder = ResolveSpriteRootFolder(context);

            var sb = new StringBuilder(512);
            sb.Append("请分析这张设计图并只输出 ui_structure.json。");
            sb.Append(" 画布尺寸为 ");
            sb.Append(canvasWidth);
            sb.Append('x');
            sb.Append(canvasHeight);
            sb.Append("。");
            if (!string.IsNullOrWhiteSpace(canvasName))
            {
                sb.Append(" canvas.name 建议使用 ");
                sb.Append(canvasName.Trim());
                sb.Append("。");
            }
            sb.Append(" unity.outputPrefabPath = ");
            sb.Append(outputPrefabPath);
            sb.Append("。");
            sb.Append(" unity.spriteRootFolder = ");
            sb.Append(string.IsNullOrEmpty(spriteRootFolder) ? "(empty)" : spriteRootFolder);
            sb.Append("。");
            if (string.IsNullOrEmpty(spriteRootFolder))
            {
                sb.Append(" spriteRootFolder 未指定时输出空字符串。");
            }
            sb.Append(" 请优先输出 unity.schemaVersion = 1，并保留 assetGuid、spriteName、asset、marker、spriteHint、regionId、nineSlice。");
            sb.Append(" duplicate basenames 时 asset 应使用更长的可区分路径。");
            sb.Append(" button_or_panel 类似的可拉伸资产应优先使用 nineSlice 或 Sprite border 元数据。");
            sb.Append(" TransparentForeground、OpaqueRect、PreserveSourceAlpha 语义应直接落在输出里。");
            sb.Append(" 所有约束由底层实现固定，不依赖外部可编辑提示词。");

            return sb.ToString();
        }

        /// <summary>
        /// 推导 canvas name。
        /// </summary>
        private static string ResolveCanvasName(UiStructureRequestContext context)
        {
            if (context == null)
            {
                return "ImageToUI";
            }

            if (!string.IsNullOrWhiteSpace(context.CanvasName))
            {
                return context.CanvasName.Trim();
            }

            string outputPrefabPath = NormalizeProjectPath(context.OutputPrefabPath, false);
            if (!string.IsNullOrWhiteSpace(outputPrefabPath) && outputPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                string prefabName = Path.GetFileNameWithoutExtension(outputPrefabPath);
                if (!string.IsNullOrWhiteSpace(prefabName))
                {
                    return prefabName;
                }
            }

            return "ImageToUI";
        }

        /// <summary>
        /// 推导输出 prefab 路径。
        /// </summary>
        private static string ResolveOutputPrefabPath(UiStructureRequestContext context, string canvasName)
        {
            if (context != null)
            {
                string explicitPath = NormalizeProjectPath(context.OutputPrefabPath, false);
                if (!string.IsNullOrWhiteSpace(explicitPath))
                {
                    return explicitPath;
                }
            }

            return GetDefaultOutputPrefabPath(canvasName);
        }

        /// <summary>
        /// 推导 Sprite 根目录。
        /// </summary>
        private static string ResolveSpriteRootFolder(UiStructureRequestContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            return NormalizeProjectPath(context.SpriteRootFolder, true);
        }

        /// <summary>
        /// 生成默认的 prefab 输出路径。
        /// </summary>
        internal static string GetDefaultOutputPrefabPath(string canvasName)
        {
            string resolvedName = SanitizeName(string.IsNullOrWhiteSpace(canvasName) ? "ImageToUI" : canvasName);
            return "Assets/Image-To-UI/" + resolvedName + ".prefab";
        }

        /// <summary>
        /// 规范化 Unity 项目路径；绝对项目内路径会转换为 Assets/ 或 Packages/ 相对路径。
        /// </summary>
        private static string NormalizeProjectPath(string path, bool trimTrailingSlash)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace("\\", "/").Trim();
            if (trimTrailingSlash)
            {
                normalized = normalized.TrimEnd('/');
            }
            if (IsUnityProjectPath(normalized))
            {
                return normalized;
            }

            try
            {
                if (Path.IsPathRooted(normalized))
                {
                    string full = Path.GetFullPath(normalized).Replace("\\", "/");
                    string projectRoot = Directory.GetParent(Application.dataPath).FullName
                        .Replace("\\", "/")
                        .TrimEnd('/');
                    if (full.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string relative = full.Substring(projectRoot.Length + 1);
                        return trimTrailingSlash ? relative.TrimEnd('/') : relative;
                    }
                }
            }
            catch (Exception)
            {
                return normalized;
            }

            return normalized;
        }

        /// <summary>
        /// 判断路径是否为 Unity 项目路径。
        /// </summary>
        private static bool IsUnityProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path == "Assets"
                   || path.StartsWith("Assets/", StringComparison.Ordinal)
                   || path == "Packages"
                   || path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        /// <summary>
        /// 清理可用于 Unity 资源文件名的名称。
        /// </summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "ImageToUI";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            name = Regex.Replace(name.Trim(), @"\s+", "_");
            return string.IsNullOrEmpty(name) ? "ImageToUI" : name;
        }

        /// <summary>
        /// 构造 Responses API 的 image_generation 请求体。
        /// </summary>
        internal static string BuildImageGenerationResponsesRequestBody(
            AiServiceConfig config,
            string prompt,
            ImageGenerationOptions options)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("prompt 不能为空。", nameof(prompt));

            options ??= new ImageGenerationOptions();

            string escapedPrompt = EscapeJsonString(prompt.Trim());
            string escapedInstructions = EscapeJsonString(options.Instructions?.Trim());
            string normalizedSize = EscapeJsonString(NormalizeImageGenerationSize(options.Size));
            string normalizedQuality = EscapeJsonString(NormalizeImageGenerationQuality(options.Quality));
            string normalizedBackground = EscapeJsonString(NormalizeImageGenerationBackground(options.Background));
            string normalizedOutputFormat = EscapeJsonString(NormalizeImageGenerationOutputFormat(options.OutputFormat));

            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append($"\"model\":\"{EscapeJsonString(config.Model)}\"");

            if (!string.IsNullOrEmpty(escapedInstructions))
            {
                sb.Append($",\"instructions\":\"{escapedInstructions}\"");
            }

            sb.Append(",\"input\":[");
            sb.Append("{\"role\":\"user\",\"content\":[");
            sb.Append($"{{\"type\":\"input_text\",\"text\":\"{escapedPrompt}\"}}");
            sb.Append("]}");
            sb.Append(']');

            sb.Append(",\"tools\":[{");
            sb.Append("\"type\":\"image_generation\"");
            sb.Append($",\"size\":\"{normalizedSize}\"");
            sb.Append($",\"quality\":\"{normalizedQuality}\"");
            sb.Append($",\"background\":\"{normalizedBackground}\"");
            sb.Append($",\"format\":\"{normalizedOutputFormat}\"");
            sb.Append("}]");

            if (options.ForceToolChoice)
            {
                sb.Append(",\"tool_choice\":{\"type\":\"image_generation\"}");
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 自动检测响应格式并提取文本内容。
        /// 优先根据响应中的 <c>"object"</c> / <c>"output"</c> 等结构字段判断实际返回格式，
        /// 避免因请求格式与响应格式不匹配导致的解析失败。
        /// </summary>
        /// <param name="responseText">原始响应 JSON 文本。</param>
        /// <param name="preferResponsesApi">当无法自动检测时，优先使用 Responses API 解析。</param>
        /// <returns>提取的文本内容。</returns>
        internal static string ExtractContentAuto(string responseText, bool preferResponsesApi)
        {
            bool? isResponses = DetectResponseFormat(responseText);
            if (isResponses == true)
            {
                return ExtractContentFromResponsesApi(responseText);
            }
            if (isResponses == false)
            {
                return ExtractContentFromChatCompletions(responseText);
            }

            // 无法自动检测时，回退到配置首选项
            return preferResponsesApi
                ? ExtractContentFromResponsesApi(responseText)
                : ExtractContentFromChatCompletions(responseText);
        }

        /// <summary>
        /// 根据响应中的结构字段检测 API 响应格式。
        /// </summary>
        /// <returns>
        /// <c>true</c> = Responses API（"response"），
        /// <c>false</c> = Chat Completions（"chat.completion"），
        /// <c>null</c> = 无法判断。
        /// </returns>
        internal static bool? DetectResponseFormat(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
                return null;

            string trimmed = responseText.TrimStart();

            // Responses API 格式: {"object":"response",...}
            if (responseText.Contains("\"object\":\"response\"", StringComparison.Ordinal) ||
                responseText.Contains("\"object\": \"response\"", StringComparison.Ordinal))
            {
                return true;
            }

            // Chat Completions 格式: {"object":"chat.completion",...}
            if (responseText.Contains("\"object\":\"chat.completion\"", StringComparison.Ordinal) ||
                responseText.Contains("\"object\": \"chat.completion\"", StringComparison.Ordinal))
            {
                return false;
            }

            // 部分代理会去掉 object 字段，但仍保留 Responses API 的 output / output_text 顶层结构。
            if (trimmed.StartsWith("{", StringComparison.Ordinal) &&
                (ContainsTopLevelKey(responseText, "output") ||
                 ContainsTopLevelKey(responseText, "output_text")))
            {
                return true;
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal) &&
                ContainsTopLevelKey(responseText, "choices"))
            {
                return false;
            }

            return null;
        }

        /// <summary>
        /// 检查 JSON 根对象是否包含指定顶层字段名。
        /// </summary>
        private static bool ContainsTopLevelKey(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int valueEnd = FindClosingQuote(json, i + 1);
                    if (valueEnd < 0)
                    {
                        return false;
                    }

                    if (depth == 1)
                    {
                        int colonIdx = valueEnd + 1;
                        while (colonIdx < json.Length && char.IsWhiteSpace(json[colonIdx]))
                        {
                            colonIdx++;
                        }

                        if (colonIdx < json.Length && json[colonIdx] == ':')
                        {
                            string fieldName = UnescapeJsonString(json.Substring(i + 1, valueEnd - i - 1));
                            if (string.Equals(fieldName, key, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }

                    i = valueEnd;
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                }
                else if (c == '}' || c == ']')
                {
                    depth = Mathf.Max(0, depth - 1);
                }
            }

            return false;
        }

        /// <summary>
        /// 从 Chat Completions 响应中提取 choices[0].message.content。
        /// </summary>
        private static string ExtractContentFromChatCompletions(string responseText)
        {
            // 响应可能实际是 Responses API 格式，给出更明确的错误提示
            if (DetectResponseFormat(responseText) == true)
            {
                throw new Exception(
                    "响应为 Responses API 格式，但尝试用 Chat Completions 解析器处理。\n" +
                    "请在 DraftWorkbench 窗口中勾选 \"Use Responses API\" 选项后重试。");
            }

            // 查找 "content" 字段值
            const string contentKey = "\"content\"";
            int contentIdx = responseText.IndexOf(contentKey, StringComparison.Ordinal);
            if (contentIdx < 0)
                throw new Exception($"Chat Completions 响应中未找到 content 字段。响应前200字符：{(responseText.Length > 200 ? responseText.Substring(0, 200) : responseText)}");

            int colonIdx = responseText.IndexOf(':', contentIdx + contentKey.Length);
            if (colonIdx < 0)
                throw new Exception("Chat Completions 响应中 content 字段格式异常。");

            int valueStart = colonIdx + 1;
            while (valueStart < responseText.Length && responseText[valueStart] != '"')
                valueStart++;

            if (valueStart >= responseText.Length)
                throw new Exception("Chat Completions 响应中 content 值起始位置异常。");

            valueStart++;
            int valueEnd = FindClosingQuote(responseText, valueStart);
            if (valueEnd < 0)
                throw new Exception("Chat Completions 响应中 content 值结束位置异常。");

            return UnescapeJsonString(responseText.Substring(valueStart, valueEnd - valueStart));
        }

        /// <summary>
        /// 从 Responses API 响应中提取 output[0].content[0].text。
        /// <para>Responses API 格式: {"output": [{"type": "message", "content": [{"type": "output_text", "text": "..."}]}]}</para>
        /// </summary>
        private static string ExtractContentFromResponsesApi(string responseText)
        {
            var response = ParseResponsesApiResponse(responseText);
            if (!string.IsNullOrEmpty(response?.output_text))
            {
                return response.output_text;
            }

            var outputItems = response?.output;
            if (outputItems != null)
            {
                foreach (var outputItem in outputItems)
                {
                    if (outputItem == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(outputItem.text) &&
                        (string.Equals(outputItem.type, "message", StringComparison.Ordinal) ||
                         string.Equals(outputItem.type, "output_text", StringComparison.Ordinal) ||
                         string.IsNullOrEmpty(outputItem.type)))
                    {
                        return outputItem.text;
                    }

                    var contentItems = outputItem.content;
                    if (contentItems == null)
                    {
                        continue;
                    }

                    foreach (var contentItem in contentItems)
                    {
                        if (!string.IsNullOrEmpty(contentItem?.text) &&
                            (string.Equals(contentItem.type, "output_text", StringComparison.Ordinal) ||
                             string.Equals(contentItem.type, "text", StringComparison.Ordinal) ||
                             string.IsNullOrEmpty(contentItem.type)))
                        {
                            return contentItem.text;
                        }
                    }
                }
            }

            if (TryExtractResponsesTextByFieldScan(responseText, out string fallbackText))
            {
                return fallbackText;
            }

            if (DetectResponseFormat(responseText) == true)
            {
                throw new Exception($"Responses API 响应中未找到可用文本内容。响应前200字符：{(responseText.Length > 200 ? responseText.Substring(0, 200) : responseText)}");
            }

            // 兜底：无法判断为 Responses API 时，尝试按 Chat Completions 格式解析。
            Debug.LogWarning("[AiVisionClient] Responses API 响应中未找到 output_text.text，尝试 Chat Completions 格式解析。");
            return ExtractContentFromChatCompletions(responseText);
        }

        /// <summary>
        /// 使用轻量字段扫描从 Responses API 变体中提取文本，避开 JsonUtility 对复杂 JSON 的限制。
        /// </summary>
        private static bool TryExtractResponsesTextByFieldScan(string responseText, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(responseText))
            {
                return false;
            }

            if (TryReadStringFieldValue(responseText, "output_text", out text) && !string.IsNullOrEmpty(text))
            {
                return true;
            }

            return TryReadBestStringFieldAfterOutput(responseText, out text);
        }

        /// <summary>
        /// 从 output 字段之后读取最像模型输出的字符串字段。
        /// </summary>
        private static bool TryReadBestStringFieldAfterOutput(string responseText, out string text)
        {
            text = null;
            int outputIndex = responseText.IndexOf("\"output\"", StringComparison.Ordinal);
            int searchStart = outputIndex >= 0 ? outputIndex : 0;
            int bestScore = 0;
            int bestPosition = -1;

            TryScoreStringFieldsAfterOutput(responseText, "content", searchStart, ref text, ref bestScore, ref bestPosition);
            TryScoreStringFieldsAfterOutput(responseText, "text", searchStart, ref text, ref bestScore, ref bestPosition);

            return !string.IsNullOrEmpty(text);
        }

        /// <summary>
        /// 扫描并评分 output 后的字符串字段，优先选择 ui_structure JSON，其次选择 Responses 输出文本字段。
        /// </summary>
        private static void TryScoreStringFieldsAfterOutput(
            string responseText,
            string fieldName,
            int searchStart,
            ref string bestText,
            ref int bestScore,
            ref int bestPosition)
        {
            int searchIndex = searchStart;
            while (TryFindStringField(responseText, fieldName, searchIndex, out int valueStart, out int valueEnd, out int fieldStart))
            {
                searchIndex = valueEnd + 1;
                string candidate = UnescapeJsonString(responseText.Substring(valueStart, valueEnd - valueStart));
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                int score = 1;
                if (IsResponsesOutputTextField(responseText, fieldStart))
                {
                    score += 30;
                }
                if (LooksLikeUiStructureContent(candidate))
                {
                    score += 100;
                }
                if (string.Equals(fieldName, "content", StringComparison.Ordinal))
                {
                    score += 5;
                }

                if (score > bestScore || (score == bestScore && fieldStart > bestPosition))
                {
                    bestText = candidate;
                    bestScore = score;
                    bestPosition = fieldStart;
                }
            }
        }

        /// <summary>
        /// 读取 JSON 文本中的指定字符串字段值。
        /// </summary>
        private static bool TryReadStringFieldValue(string json, string fieldName, out string value)
        {
            value = null;
            return TryFindStringField(json, fieldName, 0, out int valueStart, out int valueEnd, out _) &&
                   SetUnescapedJsonValue(json, valueStart, valueEnd, out value);
        }

        /// <summary>
        /// 查找 JSON 文本中的指定字符串字段。
        /// </summary>
        private static bool TryFindStringField(
            string json,
            string fieldName,
            int startIndex,
            out int valueStart,
            out int valueEnd,
            out int fieldStart)
        {
            valueStart = -1;
            valueEnd = -1;
            fieldStart = -1;

            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            string fieldPattern = $"\"{fieldName}\"";
            int index = Mathf.Max(0, startIndex);
            while (index < json.Length)
            {
                fieldStart = json.IndexOf(fieldPattern, index, StringComparison.Ordinal);
                if (fieldStart < 0)
                {
                    return false;
                }

                int colonIdx = fieldStart + fieldPattern.Length;
                while (colonIdx < json.Length && char.IsWhiteSpace(json[colonIdx]))
                {
                    colonIdx++;
                }

                if (colonIdx >= json.Length || json[colonIdx] != ':')
                {
                    index = fieldStart + fieldPattern.Length;
                    continue;
                }

                valueStart = colonIdx + 1;
                while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                {
                    valueStart++;
                }

                if (valueStart >= json.Length || json[valueStart] != '"')
                {
                    index = fieldStart + fieldPattern.Length;
                    continue;
                }

                valueStart++;
                valueEnd = FindClosingQuote(json, valueStart);
                if (valueEnd < 0)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 反转义 JSON 字符串字段值。
        /// </summary>
        private static bool SetUnescapedJsonValue(string json, int valueStart, int valueEnd, out string value)
        {
            value = null;
            if (valueStart < 0 || valueEnd < valueStart || valueEnd > json.Length)
            {
                return false;
            }

            value = UnescapeJsonString(json.Substring(valueStart, valueEnd - valueStart));
            return true;
        }

        /// <summary>
        /// 判断候选文本是否像 ui_structure JSON 或 Markdown JSON 块。
        /// </summary>
        private static bool LooksLikeUiStructureContent(string candidate)
        {
            string json = ExtractJson(candidate);
            return !string.IsNullOrEmpty(json) &&
                   (json.Contains("\"canvas\"", StringComparison.Ordinal) ||
                    json.Contains("\"root\"", StringComparison.Ordinal));
        }

        /// <summary>
        /// 判断 text/content 字符串字段附近是否属于 Responses 输出文本片段。
        /// </summary>
        private static bool IsResponsesOutputTextField(string json, int fieldStart)
        {
            int windowStart = Mathf.Max(0, fieldStart - 320);
            int windowEnd = Math.Min(json.Length, fieldStart + 320);
            string nearby = json.Substring(windowStart, windowEnd - windowStart);
            return nearby.Contains("\"type\":\"output_text\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"type\": \"output_text\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"type\":\"text\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"type\": \"text\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"type\":\"message\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"type\": \"message\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"role\":\"assistant\"", StringComparison.Ordinal) ||
                   nearby.Contains("\"role\": \"assistant\"", StringComparison.Ordinal);
        }

        /// <summary>
        /// 从 Responses API 响应中提取 image_generation_call 的图片结果。
        /// </summary>
        internal static GeneratedImageResult ExtractGeneratedImageResultFromResponsesApi(string responseText)
        {
            var outputItems = ParseResponsesApiResponse(responseText)?.output;
            if (outputItems != null)
            {
                foreach (var outputItem in outputItems)
                {
                    if (!string.Equals(outputItem?.type, "image_generation_call", StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(outputItem.result))
                    {
                        continue;
                    }

                    Texture2D texture = DecodeGeneratedImageTexture(outputItem.result);
                    texture.name = "AiGeneratedImage";
                    return new GeneratedImageResult(texture, outputItem.result, outputItem.revised_prompt, responseText);
                }
            }

            throw new Exception("Responses API 响应中未找到 image_generation_call.result。");
        }

        /// <summary>
        /// 解析 Responses API 响应根对象；解析失败时返回 null。
        /// </summary>
        private static ResponsesApiResponse ParseResponsesApiResponse(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<ResponsesApiResponse>(responseText);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AiVisionClient] 解析 Responses API 响应失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将 Base64 图片数据解码为 Texture2D。
        /// </summary>
        private static Texture2D DecodeGeneratedImageTexture(string imageBase64)
        {
            if (string.IsNullOrEmpty(imageBase64))
            {
                throw new ArgumentException("imageBase64 不能为空。", nameof(imageBase64));
            }

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(imageBase64);
            }
            catch (Exception e)
            {
                throw new Exception($"图片 Base64 解码失败: {e.Message}", e);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(imageBytes))
            {
                throw new Exception("Unity 无法从 image_generation_call.result 解码图片。");
            }

            return texture;
        }

        /// <summary>
        /// 规范化生图尺寸配置。
        /// </summary>
        private static string NormalizeImageGenerationSize(string size)
        {
            return string.IsNullOrWhiteSpace(size) ? DefaultImageGenerationSize : size.Trim();
        }

        /// <summary>
        /// 规范化生图质量配置。
        /// </summary>
        private static string NormalizeImageGenerationQuality(string quality)
        {
            if (string.IsNullOrWhiteSpace(quality))
            {
                return DefaultImageGenerationQuality;
            }

            string normalized = quality.Trim().ToLowerInvariant();
            return normalized == "low" || normalized == "medium" || normalized == "high" || normalized == "auto"
                ? normalized
                : DefaultImageGenerationQuality;
        }

        /// <summary>
        /// 规范化生图背景配置。
        /// </summary>
        private static string NormalizeImageGenerationBackground(string background)
        {
            if (string.IsNullOrWhiteSpace(background))
            {
                return DefaultImageGenerationBackground;
            }

            string normalized = background.Trim().ToLowerInvariant();
            return normalized == "opaque" || normalized == "transparent" || normalized == "auto"
                ? normalized
                : DefaultImageGenerationBackground;
        }

        /// <summary>
        /// 规范化生图输出格式配置。
        /// </summary>
        private static string NormalizeImageGenerationOutputFormat(string outputFormat)
        {
            if (string.IsNullOrWhiteSpace(outputFormat))
            {
                return DefaultImageGenerationOutputFormat;
            }

            string normalized = outputFormat.Trim().ToLowerInvariant();
            return normalized == "png" || normalized == "jpeg" || normalized == "webp"
                ? normalized
                : DefaultImageGenerationOutputFormat;
        }

        /// <summary>
        /// 校验 Responses API 顶层 model 是否可用于 image_generation 工具。
        /// </summary>
        private static void ValidateImageGenerationModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("model 不能为空。", nameof(model));
            }

            if (model.StartsWith("gpt-image-", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Responses API + image_generation 工具的顶层 model 应填写主模型，例如 gpt-5 / gpt-4.1，而不是 gpt-image-*。");
            }
        }

        /// <summary>
        /// 从指定位置开始，查找配对的结束双引号（跳过转义引号 \"）。
        /// </summary>
        private static int FindClosingQuote(string s, int startIndex)
        {
            for (int i = startIndex; i < s.Length; i++)
            {
                if (s[i] == '\\')
                {
                    i++; // 跳过转义字符
                    continue;
                }

                if (s[i] == '"')
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 从磁盘读取图片文件字节。
        /// <para>优先读取 Assets/ 下的原始文件；如果失败则尝试通过 Unity 资源路径加载。</para>
        /// </summary>
        private static byte[] LoadImageBytes(string assetPath)
        {
            // 拼接项目的绝对路径
            string projectPath = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectPath, assetPath));

            if (System.IO.File.Exists(absolutePath))
            {
                return System.IO.File.ReadAllBytes(absolutePath);
            }

            throw new Exception($"图片文件不存在: {absolutePath}");
        }

        /// <summary>
        /// 对字符串进行 JSON 转义。
        /// </summary>
        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 对 JSON 字符串值进行反转义。
        /// </summary>
        private static string UnescapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '"':  sb.Append('"');  i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case '/':  sb.Append('/');  i++; break;
                        case 'b':  sb.Append('\b'); i++; break;
                        case 'f':  sb.Append('\f'); i++; break;
                        case 'n':  sb.Append('\n'); i++; break;
                        case 'r':  sb.Append('\r'); i++; break;
                        case 't':  sb.Append('\t'); i++; break;
                        case 'u':
                            if (i + 5 < s.Length)
                            {
                                string hex = s.Substring(i + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                        System.Globalization.CultureInfo.InvariantCulture, out int codePoint))
                                {
                                    sb.Append((char)codePoint);
                                    i += 5;
                                }
                                else
                                {
                                    sb.Append(s[i]);
                                }
                            }
                            else
                            {
                                sb.Append(s[i]);
                            }
                            break;
                        default:
                            sb.Append(s[i]);
                            break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }

            return sb.ToString();
        }
    }
}

#endif
