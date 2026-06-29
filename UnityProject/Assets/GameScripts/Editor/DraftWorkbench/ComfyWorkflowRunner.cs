#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// ComfyUI workflow 运行器，负责加载模板、替换占位符、提交任务并读取分割 manifest。
    /// </summary>
    public static class ComfyWorkflowRunner
    {
        private static readonly Regex MaskPreviewImagesRegex = new Regex(
            "\\\"95\\\"\\s*:\\s*\\{\\s*\\\"images\\\"\\s*:\\s*\\[(?<images>.*?)\\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex ComfyImageObjectRegex = new Regex(
            "\\{\\s*\\\"filename\\\"\\s*:\\s*\\\"(?<filename>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"subfolder\\\"\\s*:\\s*\\\"(?<subfolder>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"type\\\"\\s*:\\s*\\\"(?<type>(?:\\\\.|[^\\\"])*)\\\"\\s*\\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// 运行配置中的 ComfyUI workflow，并读取 workflow 生成的 manifest JSON。
        /// </summary>
        /// <param name="sourceImage">源设计图。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="prefabName">目标 prefab 名称，用于输出前缀。</param>
        /// <returns>分割结果清单。</returns>
        public static async Task<DraftSegmentationManifest> RunSegmentationAsync(
            Texture2D sourceImage,
            ComfyUiServiceConfig config,
            string prefabName)
        {
            if (sourceImage == null) throw new ArgumentNullException(nameof(sourceImage));
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();

            string sourceAssetPath = AssetDatabase.GetAssetPath(sourceImage);
            if (string.IsNullOrEmpty(sourceAssetPath))
                throw new InvalidOperationException("Design Image 必须是项目中的 Texture2D 资源。");

            string sourceHash = DraftImageSegmentationService.ComputeSourceHash(sourceImage);
            string cacheDirectory = DraftPathUtility.ToAbsolutePath(
                Path.Combine(config.CacheRoot, sourceHash).Replace('\\', '/'));
            Directory.CreateDirectory(cacheDirectory);

            string workflowJson = LoadWorkflowJson(config);
            string outputPrefix = $"DW_{sourceHash}_{SanitizeFileName(prefabName)}";
            string manifestPath = Path.Combine(cacheDirectory, outputPrefix + ".manifest.json");
            string comfyInputImageName = outputPrefix + Path.GetExtension(sourceAssetPath);
            string sourceAbsolutePath = DraftPathUtility.ToAbsolutePath(sourceAssetPath);
            comfyInputImageName = await ComfyUiClient.UploadInputImageAsync(config, sourceAbsolutePath, comfyInputImageName);

            var inspection = ComfyWorkflowAdapter.InspectWorkflowJson(workflowJson, config);
            if (!inspection.IsUsableForDetection)
            {
                Debug.LogWarning("[DraftWorkbench] ComfyUI workflow 检查未完全通过：" + string.Join("; ", inspection.Diagnostics));
            }

            string resolvedWorkflow = PatchWorkflowJson(
                workflowJson,
                sourceImage,
                sourceAssetPath,
                sourceHash,
                cacheDirectory,
                outputPrefix,
                manifestPath,
                comfyInputImageName,
                config);

            string apiPromptJson = ComfyWorkflowAdapter.BuildApiPromptJson(resolvedWorkflow, config, new ComfyWorkflowPatchContext
            {
                InputImagePath = sourceAbsolutePath.Replace('\\', '/'),
                ComfyInputImageName = comfyInputImageName,
                InputAssetPath = sourceAssetPath,
                OutputPrefix = outputPrefix,
                OutputDirectory = cacheDirectory.Replace('\\', '/'),
                ManifestPath = manifestPath.Replace('\\', '/'),
                SourceHash = sourceHash,
                SourceWidth = sourceImage.width,
                SourceHeight = sourceImage.height,
                MinRegionArea = config.MinRegionArea
            });

            File.WriteAllText(Path.Combine(cacheDirectory, outputPrefix + ".workflow.json"), resolvedWorkflow, Encoding.UTF8);
            File.WriteAllText(Path.Combine(cacheDirectory, outputPrefix + ".api-prompt.json"), apiPromptJson, Encoding.UTF8);

            string clientId = "draft-workbench-" + Guid.NewGuid().ToString("N");
            string promptId = await ComfyUiClient.QueuePromptAsync(config, apiPromptJson, clientId);
            string historyJson = await ComfyUiClient.WaitForHistoryAsync(config, promptId);
            File.WriteAllText(Path.Combine(cacheDirectory, outputPrefix + ".history.json"), historyJson, Encoding.UTF8);
            await DownloadMaskOutputsAsync(config, historyJson, cacheDirectory, outputPrefix);

            string discoveredManifestPath = LocateManifest(cacheDirectory, manifestPath, sourceHash);
            DraftSegmentationManifest manifest;
            if (string.IsNullOrEmpty(discoveredManifestPath))
            {
                manifest = DraftImageSegmentationService.BuildManifestFromMaskFiles(
                    cacheDirectory,
                    sourceImage,
                    sourceHash,
                    sourceAssetPath);
                if (manifest.regions == null || manifest.regions.Count == 0)
                {
                    throw new FileNotFoundException(
                        "ComfyUI 任务已完成，但未找到分割 manifest 或 mask PNG。请让 workflow 写出 *.manifest.json，或输出文件名包含 mask 的 PNG。",
                        manifestPath);
                }
            }
            else
            {
                string manifestJson = File.ReadAllText(discoveredManifestPath, Encoding.UTF8);
                manifest = JsonUtility.FromJson<DraftSegmentationManifest>(manifestJson);
                if (manifest == null)
                    throw new InvalidOperationException("分割 manifest JSON 解析失败。Path=" + discoveredManifestPath);

                manifest.manifestDirectory = Path.GetDirectoryName(discoveredManifestPath)?.Replace('\\', '/') ?? cacheDirectory;
            }

            manifest.sourceHash = string.IsNullOrEmpty(manifest.sourceHash) ? sourceHash : manifest.sourceHash;
            manifest.sourceWidth = manifest.sourceWidth > 0 ? manifest.sourceWidth : sourceImage.width;
            manifest.sourceHeight = manifest.sourceHeight > 0 ? manifest.sourceHeight : sourceImage.height;
            manifest.sourceAssetPath = sourceAssetPath;
            if (manifest.regions == null)
            {
                manifest.regions = new System.Collections.Generic.List<DraftSegmentRegion>();
            }

            return manifest;
        }

        /// <summary>
        /// 读取配置中指定的 workflow JSON 模板。
        /// </summary>
        private static string LoadWorkflowJson(ComfyUiServiceConfig config)
        {
            if (config.WorkflowJsonAsset != null)
                return config.WorkflowJsonAsset.text;

            string workflowPath = string.IsNullOrWhiteSpace(config.WorkflowJsonAssetPath)
                ? ComfyUiServiceConfig.DefaultWorkflowJsonAssetPath
                : config.WorkflowJsonAssetPath;

            if (!string.IsNullOrEmpty(workflowPath))
            {
                var workflowAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(workflowPath);
                if (workflowAsset != null)
                    return workflowAsset.text;

                string absolutePath = DraftPathUtility.ToAbsolutePath(workflowPath);
                if (File.Exists(absolutePath))
                    return File.ReadAllText(absolutePath, Encoding.UTF8);
            }

            throw new InvalidOperationException($"请先在 ComfyUiServiceConfig 中配置 workflow JSON。已检查路径：{workflowPath}");
        }

        /// <summary>
        /// 将 workflow 模板中的占位符和常见节点参数替换为本次任务参数。
        /// </summary>
        private static string PatchWorkflowJson(
            string workflowJson,
            Texture2D sourceImage,
            string sourceAssetPath,
            string sourceHash,
            string cacheDirectory,
            string outputPrefix,
            string manifestPath,
            string comfyInputImageName,
            ComfyUiServiceConfig config)
        {
            string sourceAbsolutePath = DraftPathUtility.ToAbsolutePath(sourceAssetPath).Replace('\\', '/');
            string cachePath = cacheDirectory.Replace('\\', '/');
            string manifest = manifestPath.Replace('\\', '/');
            var context = new ComfyWorkflowPatchContext
            {
                InputImagePath = sourceAbsolutePath,
                ComfyInputImageName = comfyInputImageName,
                InputAssetPath = sourceAssetPath,
                OutputPrefix = outputPrefix,
                OutputDirectory = cachePath,
                ManifestPath = manifest,
                SourceHash = sourceHash,
                SourceWidth = sourceImage.width,
                SourceHeight = sourceImage.height,
                MinRegionArea = config.MinRegionArea
            };

            return ComfyWorkflowAdapter.PatchWorkflowJson(workflowJson, config, context);
        }

        /// <summary>
        /// 从 ComfyUI history 中下载 MaskPreview 输出到 Draft Workbench 缓存目录。
        /// </summary>
        private static async Task DownloadMaskOutputsAsync(
            ComfyUiServiceConfig config,
            string historyJson,
            string cacheDirectory,
            string outputPrefix)
        {
            if (string.IsNullOrWhiteSpace(historyJson) || string.IsNullOrEmpty(cacheDirectory))
                return;

            var images = ExtractMaskPreviewImages(historyJson);
            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                byte[] bytes = await ComfyUiClient.DownloadViewFileAsync(config, image.FileName, image.Subfolder, image.Type);
                if (bytes == null || bytes.Length == 0)
                    continue;

                string outputPath = Path.Combine(cacheDirectory, $"{outputPrefix}_mask_{i + 1:000}.png");
                File.WriteAllBytes(outputPath, bytes);
            }
        }

        /// <summary>
        /// 从 ComfyUI history JSON 提取 MaskPreview 节点输出图片。
        /// </summary>
        private static List<ComfyHistoryImage> ExtractMaskPreviewImages(string historyJson)
        {
            var images = new List<ComfyHistoryImage>();
            Match maskPreviewMatch = MaskPreviewImagesRegex.Match(historyJson ?? string.Empty);
            if (!maskPreviewMatch.Success)
                return images;

            string imageArrayText = maskPreviewMatch.Groups["images"].Value;
            foreach (Match imageMatch in ComfyImageObjectRegex.Matches(imageArrayText))
            {
                string fileName = DecodeJsonString(imageMatch.Groups["filename"].Value);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                images.Add(new ComfyHistoryImage
                {
                    FileName = fileName,
                    Subfolder = DecodeJsonString(imageMatch.Groups["subfolder"].Value),
                    Type = DecodeJsonString(imageMatch.Groups["type"].Value)
                });
            }

            return images;
        }

        /// <summary>
        /// 解码少量 JSON 字符串转义，满足 ComfyUI 文件名字段使用。
        /// </summary>
        private static string DecodeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\/", "/");
        }

        /// <summary>
        /// ComfyUI history 中的图片输出记录。
        /// </summary>
        private struct ComfyHistoryImage
        {
            public string FileName;
            public string Subfolder;
            public string Type;
        }

        /// <summary>
        /// 在缓存目录内查找 workflow 写出的 manifest 文件。
        /// </summary>
        private static string LocateManifest(string cacheDirectory, string preferredPath, string sourceHash)
        {
            if (File.Exists(preferredPath))
                return preferredPath;

            if (!Directory.Exists(cacheDirectory))
                return string.Empty;

            string[] candidates = Directory.GetFiles(cacheDirectory, "*.manifest.json", SearchOption.TopDirectoryOnly);
            if (candidates.Length == 0)
            {
                candidates = Directory.GetFiles(cacheDirectory, "*manifest*.json", SearchOption.TopDirectoryOnly);
            }

            Array.Sort(candidates, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrEmpty(sourceHash) || Path.GetFileName(candidate).Contains(sourceHash))
                    return candidate;
            }

            return candidates.Length > 0 ? candidates[0] : string.Empty;
        }

        /// <summary>
        /// 生成适合作为文件名片段的字符串。
        /// </summary>
        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Prefab";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Draft Workbench 路径工具，统一 Assets/Library 相对路径与绝对路径转换。
    /// </summary>
    internal static class DraftPathUtility
    {
        /// <summary>
        /// 将项目相对路径或绝对路径转换为绝对路径。
        /// </summary>
        /// <param name="path">输入路径。</param>
        /// <returns>绝对路径。</returns>
        public static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>
        /// 将绝对路径转换为 Assets 或 Library 开头的项目相对路径。
        /// </summary>
        /// <param name="absolutePath">绝对路径。</param>
        /// <returns>项目相对路径；不在项目内时返回原路径。</returns>
        public static string ToProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return absolutePath.Replace('\\', '/');

            string fullPath = Path.GetFullPath(absolutePath);
            string fullRoot = Path.GetFullPath(projectRoot);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace('\\', '/');

            string relative = fullPath.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace('\\', '/');
        }
    }
}
#endif
