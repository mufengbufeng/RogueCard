#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// ComfyUI HTTP 客户端，封装本地 /system_stats、/prompt 和 /history/{prompt_id} 调用。
    /// </summary>
    public static class ComfyUiClient
    {
        /// <summary>
        /// ComfyUI /prompt 响应中返回的任务标识。
        /// </summary>
        [Serializable]
        private class PromptResponse
        {
            public string prompt_id;
            public int number;
        }

        /// <summary>
        /// ComfyUI /upload/image 响应中返回的文件信息。
        /// </summary>
        [Serializable]
        private class UploadImageResponse
        {
            public string name;
            public string subfolder;
            public string type;
        }

        /// <summary>
        /// 测试 ComfyUI 服务是否可访问。
        /// </summary>
        /// <param name="config">ComfyUI 配置。</param>
        /// <returns>连接结果说明。</returns>
        public static async Task<string> TestConnectionAsync(ComfyUiServiceConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();

            string url = config.GetNormalizedBaseUrl() + "/system_stats";
            return await SendGetAsync(url, config.TimeoutSeconds);
        }

        /// <summary>
        /// 将图片上传到 ComfyUI input 目录，供 LoadImage 节点引用。
        /// </summary>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="imagePath">本地图片绝对路径。</param>
        /// <param name="uploadedFileName">期望上传后的文件名。</param>
        /// <returns>ComfyUI 实际保存的文件名。</returns>
        public static async Task<string> UploadInputImageAsync(ComfyUiServiceConfig config, string imagePath, string uploadedFileName)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("图片路径为空", nameof(imagePath));
            if (!File.Exists(imagePath)) throw new FileNotFoundException("待上传图片不存在。", imagePath);
            config.Normalize();

            string url = config.GetNormalizedBaseUrl() + "/upload/image";
            string response = await SendImageUploadAsync(url, imagePath, uploadedFileName, config.TimeoutSeconds);
            var uploadResponse = JsonUtility.FromJson<UploadImageResponse>(response);
            if (uploadResponse == null || string.IsNullOrWhiteSpace(uploadResponse.name))
                throw new InvalidOperationException("ComfyUI /upload/image 响应中缺少 name。响应：" + response);

            return string.IsNullOrWhiteSpace(uploadResponse.subfolder)
                ? uploadResponse.name
                : uploadResponse.subfolder.TrimEnd('/', '\\') + "/" + uploadResponse.name;
        }

        /// <summary>
        /// 向 ComfyUI 提交 workflow。
        /// </summary>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="workflowJson">已完成参数替换的 workflow JSON。</param>
        /// <param name="clientId">客户端 ID。</param>
        /// <returns>ComfyUI prompt id。</returns>
        public static async Task<string> QueuePromptAsync(ComfyUiServiceConfig config, string workflowJson, string clientId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(workflowJson)) throw new ArgumentException("workflow JSON 为空", nameof(workflowJson));
            config.Normalize();

            string body = "{\"prompt\":" + workflowJson + ",\"client_id\":" + EncodeJsonString(clientId) + "}";
            string url = config.GetNormalizedBaseUrl() + "/prompt";
            string response = await SendPostJsonAsync(url, body, config.TimeoutSeconds);
            var promptResponse = JsonUtility.FromJson<PromptResponse>(response);
            if (promptResponse == null || string.IsNullOrEmpty(promptResponse.prompt_id))
                throw new InvalidOperationException("ComfyUI /prompt 响应中缺少 prompt_id。响应：" + response);

            return promptResponse.prompt_id;
        }

        /// <summary>
        /// 下载 ComfyUI /view 暴露的输出或临时文件。
        /// </summary>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="fileName">ComfyUI history 中返回的文件名。</param>
        /// <param name="subfolder">ComfyUI history 中返回的子目录。</param>
        /// <param name="type">ComfyUI 文件类型，通常为 input、output 或 temp。</param>
        /// <returns>文件二进制内容。</returns>
        public static Task<byte[]> DownloadViewFileAsync(ComfyUiServiceConfig config, string fileName, string subfolder, string type)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("文件名为空", nameof(fileName));
            config.Normalize();

            string url = config.GetNormalizedBaseUrl()
                         + "/view?filename=" + UnityWebRequest.EscapeURL(fileName)
                         + "&type=" + UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(type) ? "output" : type);
            if (!string.IsNullOrWhiteSpace(subfolder))
            {
                url += "&subfolder=" + UnityWebRequest.EscapeURL(subfolder);
            }

            return SendGetBytesAsync(url, config.TimeoutSeconds);
        }

        /// <summary>
        /// 获取指定 prompt id 的 history JSON。
        /// </summary>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="promptId">ComfyUI prompt id。</param>
        /// <returns>history 原始 JSON。</returns>
        public static async Task<string> GetHistoryAsync(ComfyUiServiceConfig config, string promptId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(promptId)) throw new ArgumentException("promptId 为空", nameof(promptId));
            config.Normalize();

            string url = config.GetNormalizedBaseUrl() + "/history/" + UnityWebRequest.EscapeURL(promptId);
            return await SendGetAsync(url, config.TimeoutSeconds);
        }

        /// <summary>
        /// 轮询等待 ComfyUI prompt 完成。
        /// <para>ComfyUI 的 /history/{prompt_id} 通常在任务完成后才返回非空内容。</para>
        /// </summary>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="promptId">ComfyUI prompt id。</param>
        /// <returns>完成后的 history 原始 JSON。</returns>
        public static async Task<string> WaitForHistoryAsync(ComfyUiServiceConfig config, string promptId)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();

            int delayMs = Mathf.RoundToInt(config.PollIntervalSeconds * 1000f);
            double timeoutAt = EditorApplication.timeSinceStartup + config.MaxPollSeconds;
            string lastResponse = string.Empty;

            while (EditorApplication.timeSinceStartup < timeoutAt)
            {
                lastResponse = await GetHistoryAsync(config, promptId);
                if (IsCompletedHistory(lastResponse, promptId))
                    return lastResponse;

                await Task.Delay(delayMs);
            }

            throw new TimeoutException($"等待 ComfyUI 任务完成超时。PromptId={promptId}, LastResponse={lastResponse}");
        }

        /// <summary>
        /// 判断 history 响应是否代表任务已完成。
        /// </summary>
        private static bool IsCompletedHistory(string historyJson, string promptId)
        {
            if (string.IsNullOrWhiteSpace(historyJson))
                return false;

            string trimmed = historyJson.Trim();
            if (trimmed == "{}" || trimmed == "[]")
                return false;

            return trimmed.Contains(promptId);
        }

        /// <summary>
        /// 发送 GET 请求并返回响应正文。
        /// </summary>
        private static Task<string> SendGetAsync(string url, int timeoutSeconds)
        {
            var request = UnityWebRequest.Get(url);
            request.timeout = Mathf.Max(1, timeoutSeconds);
            return SendRequestAsync(request);
        }

        /// <summary>
        /// 发送 GET 请求并返回响应二进制内容。
        /// </summary>
        private static Task<byte[]> SendGetBytesAsync(string url, int timeoutSeconds)
        {
            var request = UnityWebRequest.Get(url);
            request.timeout = Mathf.Max(1, timeoutSeconds);
            return SendBytesRequestAsync(request);
        }

        /// <summary>
        /// 发送 JSON POST 请求并返回响应正文。
        /// </summary>
        private static Task<string> SendPostJsonAsync(string url, string jsonBody, int timeoutSeconds)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonBody ?? string.Empty);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(1, timeoutSeconds);
            return SendRequestAsync(request);
        }

        /// <summary>
        /// 发送 ComfyUI 图片上传表单请求。
        /// </summary>
        private static Task<string> SendImageUploadAsync(string url, string imagePath, string uploadedFileName, int timeoutSeconds)
        {
            string fileName = string.IsNullOrWhiteSpace(uploadedFileName) ? Path.GetFileName(imagePath) : uploadedFileName;
            byte[] bytes = File.ReadAllBytes(imagePath);
            var form = new WWWForm();
            form.AddBinaryData("image", bytes, fileName, GuessImageMimeType(imagePath));
            form.AddField("overwrite", "true");

            var request = UnityWebRequest.Post(url, form);
            request.timeout = Mathf.Max(1, timeoutSeconds);
            return SendRequestAsync(request);
        }

        /// <summary>
        /// 根据图片扩展名推断上传 MIME 类型。
        /// </summary>
        private static string GuessImageMimeType(string imagePath)
        {
            string extension = Path.GetExtension(imagePath)?.ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".webp":
                    return "image/webp";
                default:
                    return "image/png";
            }
        }

        /// <summary>
        /// 在 EditorApplication.update 中轮询 UnityWebRequest 完成状态。
        /// </summary>
        private static Task<string> SendRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<string>();
            var operation = request.SendWebRequest();

            void Tick()
            {
                if (!operation.isDone)
                    return;

                EditorApplication.update -= Tick;
                try
                {
                    bool hasError = request.result == UnityWebRequest.Result.ConnectionError
                                    || request.result == UnityWebRequest.Result.ProtocolError
                                    || request.result == UnityWebRequest.Result.DataProcessingError;
                    string text = request.downloadHandler?.text ?? string.Empty;
                    if (hasError)
                    {
                        tcs.TrySetException(new InvalidOperationException(
                            $"ComfyUI 请求失败: Url={request.url}, HTTP {request.responseCode}, {request.error}, Body={text}"));
                    }
                    else
                    {
                        tcs.TrySetResult(text);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            }

            EditorApplication.update += Tick;
            return tcs.Task;
        }

        /// <summary>
        /// 在 EditorApplication.update 中轮询 UnityWebRequest 完成状态并返回二进制内容。
        /// </summary>
        private static Task<byte[]> SendBytesRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<byte[]>();
            var operation = request.SendWebRequest();

            void Tick()
            {
                if (!operation.isDone)
                    return;

                EditorApplication.update -= Tick;
                try
                {
                    bool hasError = request.result == UnityWebRequest.Result.ConnectionError
                                    || request.result == UnityWebRequest.Result.ProtocolError
                                    || request.result == UnityWebRequest.Result.DataProcessingError;
                    string text = request.downloadHandler?.text ?? string.Empty;
                    if (hasError)
                    {
                        tcs.TrySetException(new InvalidOperationException(
                            $"ComfyUI 请求失败: Url={request.url}, HTTP {request.responseCode}, {request.error}, Body={text}"));
                    }
                    else
                    {
                        tcs.TrySetResult(request.downloadHandler?.data ?? Array.Empty<byte>());
                    }
                }
                finally
                {
                    request.Dispose();
                }
            }

            EditorApplication.update += Tick;
            return tcs.Task;
        }

        /// <summary>
        /// 将字符串编码为 JSON 字符串字面量。
        /// </summary>
        internal static string EncodeJsonString(string value)
        {
            if (value == null)
                return "null";

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }
    }
}
#endif
