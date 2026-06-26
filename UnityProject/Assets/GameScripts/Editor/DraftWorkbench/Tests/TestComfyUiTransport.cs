#if UNITY_EDITOR

using System.Threading.Tasks;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// 给 EditMode 测试使用的 ComfyUI transport 假实现。
    /// </summary>
    internal sealed class TestComfyUiTransport : IComfyUiTransport
    {
        public string LastUploadedPath;
        public string LastPromptJson;
        public string HistoryJson;
        public byte[] OutputBytes;
        public bool FailOnQueuePrompt;

        public Task<string> TestConnectionAsync(ComfyUiServiceConfig config)
        {
            return Task.FromResult("ok");
        }

        public Task<string> UploadInputImageAsync(ComfyUiServiceConfig config, string imagePath, string uploadedFileName)
        {
            LastUploadedPath = imagePath;
            return Task.FromResult(uploadedFileName);
        }

        public Task<string> QueuePromptAsync(ComfyUiServiceConfig config, string workflowJson, string clientId)
        {
            LastPromptJson = workflowJson;
            if (FailOnQueuePrompt)
            {
                throw new System.InvalidOperationException("boom");
            }

            return Task.FromResult("prompt-1");
        }

        public Task<string> WaitForHistoryAsync(ComfyUiServiceConfig config, string promptId)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(HistoryJson)
                ? "{\"2\":{\"outputs\":{\"images\":[{\"filename\":\"refined.png\",\"subfolder\":\"output\",\"type\":\"output\"}]}}}"
                : HistoryJson);
        }

        public Task<byte[]> DownloadViewFileAsync(ComfyUiServiceConfig config, string fileName, string subfolder, string type)
        {
            return Task.FromResult(OutputBytes ?? CreatePngBytes(1, 1, new Color32(0, 255, 0, 255)));
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
    }
}

#endif
