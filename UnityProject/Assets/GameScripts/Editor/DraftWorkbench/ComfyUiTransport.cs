#if UNITY_EDITOR

using System.Threading.Tasks;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// ComfyUI 调用抽象，便于将运行器与具体 HTTP 实现解耦。
    /// </summary>
    public interface IComfyUiTransport
    {
        /// <summary>测试连接。</summary>
        Task<string> TestConnectionAsync(ComfyUiServiceConfig config);

        /// <summary>上传输入图片。</summary>
        Task<string> UploadInputImageAsync(ComfyUiServiceConfig config, string imagePath, string uploadedFileName);

        /// <summary>提交 prompt。</summary>
        Task<string> QueuePromptAsync(ComfyUiServiceConfig config, string workflowJson, string clientId);

        /// <summary>等待 prompt 完成并返回 history JSON。</summary>
        Task<string> WaitForHistoryAsync(ComfyUiServiceConfig config, string promptId);

        /// <summary>下载 ComfyUI 输出文件。</summary>
        Task<byte[]> DownloadViewFileAsync(ComfyUiServiceConfig config, string fileName, string subfolder, string type);
    }

    /// <summary>
    /// 默认 ComfyUI HTTP transport，直接转发到 <see cref="ComfyUiClient"/>。
    /// </summary>
    public sealed class ComfyUiClientTransport : IComfyUiTransport
    {
        public Task<string> TestConnectionAsync(ComfyUiServiceConfig config)
        {
            return ComfyUiClient.TestConnectionAsync(config);
        }

        public Task<string> UploadInputImageAsync(ComfyUiServiceConfig config, string imagePath, string uploadedFileName)
        {
            return ComfyUiClient.UploadInputImageAsync(config, imagePath, uploadedFileName);
        }

        public Task<string> QueuePromptAsync(ComfyUiServiceConfig config, string workflowJson, string clientId)
        {
            return ComfyUiClient.QueuePromptAsync(config, workflowJson, clientId);
        }

        public Task<string> WaitForHistoryAsync(ComfyUiServiceConfig config, string promptId)
        {
            return ComfyUiClient.WaitForHistoryAsync(config, promptId);
        }

        public Task<byte[]> DownloadViewFileAsync(ComfyUiServiceConfig config, string fileName, string subfolder, string type)
        {
            return ComfyUiClient.DownloadViewFileAsync(config, fileName, subfolder, type);
        }
    }
}

#endif
