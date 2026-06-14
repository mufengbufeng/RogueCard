#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// ComfyUI frontend workflow 的节点检查结果。
    /// </summary>
    [Serializable]
    public class ComfyWorkflowInspectionResult
    {
        /// <summary>是否找到 LoadImage 节点。</summary>
        public bool HasLoadImageNode;

        /// <summary>是否找到 SAM3/subgraph 节点。</summary>
        public bool HasSam3Node;

        /// <summary>是否找到 preview、mask 或 save 输出节点。</summary>
        public bool HasOutputNode;

        /// <summary>LoadImage 节点 ID。</summary>
        public string LoadImageNodeId;

        /// <summary>SAM3/subgraph 节点 ID。</summary>
        public string Sam3NodeId;

        /// <summary>输出节点 ID 列表。</summary>
        public List<string> OutputNodeIds = new List<string>();

        /// <summary>检查诊断信息。</summary>
        public List<string> Diagnostics = new List<string>();

        /// <summary>该 workflow 是否具备检测所需的关键节点。</summary>
        public bool IsUsableForDetection => HasLoadImageNode && HasSam3Node && HasOutputNode;

        /// <summary>该 workflow 是否具备 raw crop refinement 所需的关键节点。</summary>
        public bool IsUsableForRefinement => HasLoadImageNode && HasOutputNode;
    }

    /// <summary>
    /// ComfyUI workflow patch 所需的本次运行参数。
    /// </summary>
    [Serializable]
    public class ComfyWorkflowPatchContext
    {
        /// <summary>上传或本地输入图片绝对路径。</summary>
        public string InputImagePath;

        /// <summary>上传到 ComfyUI input 目录后的图片文件名。</summary>
        public string ComfyInputImageName;

        /// <summary>Unity 项目内输入图片路径。</summary>
        public string InputAssetPath;

        /// <summary>输出文件名前缀。</summary>
        public string OutputPrefix;

        /// <summary>输出目录绝对路径。</summary>
        public string OutputDirectory;

        /// <summary>manifest 输出绝对路径。</summary>
        public string ManifestPath;

        /// <summary>源图哈希。</summary>
        public string SourceHash;

        /// <summary>源图宽度。</summary>
        public int SourceWidth;

        /// <summary>源图高度。</summary>
        public int SourceHeight;

        /// <summary>最小 region 面积。</summary>
        public int MinRegionArea;
    }

    /// <summary>
    /// ComfyUI workflow 适配器，负责前端 workflow 的节点识别与参数 patch。
    /// </summary>
    public static class ComfyWorkflowAdapter
    {
        private static readonly Regex IdRegex = new Regex(@"""id""\s*:\s*""?(?<id>[^"",}]+)""?", RegexOptions.Compiled);
        private static readonly Regex TypeRegex = new Regex(@"""type""\s*:\s*""(?<type>[^""]+)""", RegexOptions.Compiled);
        private static readonly Regex WidgetArrayRegex = new Regex(@"""widgets_values""\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex NodesKeyRegex = new Regex(@"""nodes""\s*:", RegexOptions.Compiled);

        private readonly struct JsonSpan
        {
            public readonly int Start;
            public readonly int Length;
            public readonly string Text;

            public JsonSpan(int start, int length, string text)
            {
                Start = start;
                Length = length;
                Text = text;
            }
        }

        /// <summary>
        /// 检查 ComfyUI workflow JSON，识别 LoadImage、SAM3 和输出节点。
        /// </summary>
        /// <param name="workflowJson">ComfyUI frontend workflow JSON。</param>
        /// <param name="config">ComfyUI 配置，可提供节点 ID 覆盖。</param>
        /// <returns>节点检查结果。</returns>
        public static ComfyWorkflowInspectionResult InspectWorkflowJson(string workflowJson, ComfyUiServiceConfig config)
        {
            var result = new ComfyWorkflowInspectionResult();
            if (string.IsNullOrWhiteSpace(workflowJson))
            {
                result.Diagnostics.Add("workflow JSON 为空。");
                return result;
            }

            config?.Normalize();
            foreach (JsonSpan span in ExtractNodeSpans(workflowJson))
            {
                string nodeJson = span.Text;
                string id = GetMatchValue(IdRegex.Match(nodeJson), "id");
                string type = GetMatchValue(TypeRegex.Match(nodeJson), "type");
                string searchable = BuildSearchableNodeHeader(nodeJson, type);
                if (searchable.Contains("markdownnote"))
                    continue;

                if (!result.HasLoadImageNode && IsConfiguredOrDetected(id, config?.LoadImageNodeId, searchable.Contains("loadimage")))
                {
                    result.HasLoadImageNode = true;
                    result.LoadImageNodeId = id;
                }

                bool looksLikeSam3 = searchable.Contains("sam3") || searchable.Contains("image segment");
                if (!result.HasSam3Node && IsConfiguredOrDetected(id, config?.Sam3NodeId, looksLikeSam3))
                {
                    result.HasSam3Node = true;
                    result.Sam3NodeId = id;
                }

                bool looksLikeOutput = searchable.Contains("previewimage")
                                       || searchable.Contains("saveimage")
                                       || searchable.Contains("maskpreview")
                                       || searchable.Contains("joinimagewithalpha")
                                       || searchable.Contains("manifest");
                if (IsConfiguredOrDetected(id, config?.OutputNodeId, looksLikeOutput))
                {
                    result.HasOutputNode = true;
                    if (!string.IsNullOrEmpty(id) && !result.OutputNodeIds.Contains(id))
                    {
                        result.OutputNodeIds.Add(id);
                    }
                }
            }

            if (!result.HasLoadImageNode)
                result.Diagnostics.Add("未找到 LoadImage 节点；请在 workflow 中加入 LoadImage，或在 ComfyUiServiceConfig.LoadImageNodeId 中指定节点 ID。");
            if (!result.HasSam3Node)
                result.Diagnostics.Add("未找到 SAM3/subgraph 节点；请确认 workflow 包含 SAM3_Detect 或 Image Segment (SAM3)，或指定 Sam3NodeId。");
            if (!result.HasOutputNode)
                result.Diagnostics.Add("未找到 mask/preview/output 节点；请添加 MaskPreview、PreviewImage、SaveImage 或 manifest 输出节点，或指定 OutputNodeId。");

            return result;
        }

        /// <summary>
        /// 将 patch 后的 ComfyUI frontend workflow 转换为 /prompt API 需要的 prompt JSON。
        /// </summary>
        /// <param name="workflowJson">已 patch 的 workflow JSON。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="context">本次运行参数。</param>
        /// <returns>ComfyUI API prompt JSON。</returns>
        public static string BuildApiPromptJson(string workflowJson, ComfyUiServiceConfig config, ComfyWorkflowPatchContext context)
        {
            if (string.IsNullOrWhiteSpace(workflowJson))
                throw new ArgumentException("workflow JSON 为空", nameof(workflowJson));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));
            config.Normalize();

            if (!workflowJson.Contains("\"nodes\"", StringComparison.Ordinal) && workflowJson.Contains("\"class_type\"", StringComparison.Ordinal))
                return workflowJson;

            string inputImageName = string.IsNullOrWhiteSpace(context.ComfyInputImageName)
                ? context.InputImagePath
                : context.ComfyInputImageName;

            return "{"
                   + "\"77\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"sam3.1_multiplex_fp16.safetensors\"}},"
                   + "\"78\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"77\",1],\"text\":" + JsonString(config.DetectionPrompt) + "}},"
                   + "\"79\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":" + JsonString(inputImageName) + "}},"
                   + "\"75\":{\"class_type\":\"SAM3_Detect\",\"inputs\":{"
                   + "\"model\":[\"77\",0],"
                   + "\"image\":[\"79\",0],"
                   + "\"conditioning\":[\"78\",0],"
                   + "\"threshold\":" + config.MaskThreshold.ToString("0.###", CultureInfo.InvariantCulture) + ","
                   + "\"refine_iterations\":" + config.RefineIterations.ToString(CultureInfo.InvariantCulture) + ","
                   + "\"individual_masks\":" + (config.IndividualMasks ? "true" : "false")
                   + "}},"
                   + "\"95\":{\"class_type\":\"MaskPreview\",\"inputs\":{\"mask\":[\"75\",0]}},"
                   + "\"111\":{\"class_type\":\"JoinImageWithAlpha\",\"inputs\":{\"image\":[\"79\",0],\"alpha\":[\"75\",0]}},"
                   + "\"106\":{\"class_type\":\"PreviewImage\",\"inputs\":{\"images\":[\"111\",0]}}"
                   + "}";
        }

        /// <summary>
        /// 构建 raw crop refinement 的最小 ComfyUI prompt。
        /// 默认只负责读取单个 raw PNG 并写出单个 refined PNG，不会走 SAM3 检测链路。
        /// </summary>
        /// <param name="inputImageName">ComfyUI input 目录中的图片名。</param>
        /// <param name="outputPrefix">输出前缀。</param>
        /// <returns>ComfyUI API prompt JSON。</returns>
        public static string BuildRefinementApiPromptJson(string inputImageName, string outputPrefix)
        {
            return "{"
                   + "\"1\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":" + JsonString(inputImageName) + "}},"
                   + "\"2\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"1\",0],\"filename_prefix\":" + JsonString(outputPrefix) + "}}"
                   + "}";
        }

        /// <summary>
        /// 构建透明前景默认背景移除 prompt。该 prompt 表达 BiRefNet/RMBG 类工作流意图。
        /// </summary>
        public static string BuildTransparentRefinementApiPromptJson(string inputImageName, string outputPrefix)
        {
            return "{"
                   + "\"1\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":" + JsonString(inputImageName) + "}},"
                   + "\"2\":{\"class_type\":\"BiRefNetRMBG\",\"inputs\":{\"image\":[\"1\",0]}},"
                   + "\"3\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"2\",0],\"filename_prefix\":" + JsonString(outputPrefix) + "}}"
                   + "}";
        }

        /// <summary>
        /// 构建 Sam2 fallback prompt，并传入 JSON-derived bbox 和中心点提示。
        /// </summary>
        public static string BuildSam2RefinementApiPromptJson(string inputImageName, string outputPrefix, DraftSegmentRegion region)
        {
            DraftSegmentBounds bounds = region?.cropBounds != null && region.cropBounds.width > 0 && region.cropBounds.height > 0
                ? region.cropBounds
                : region?.expandedBbox;
            int x = bounds != null ? bounds.x : 0;
            int y = bounds != null ? bounds.y : 0;
            int width = bounds != null ? bounds.width : 1;
            int height = bounds != null ? bounds.height : 1;
            int centerX = x + Mathf.Max(1, width) / 2;
            int centerY = y + Mathf.Max(1, height) / 2;

            return "{"
                   + "\"1\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":" + JsonString(inputImageName) + "}},"
                   + "\"2\":{\"class_type\":\"SAM2Segment\",\"inputs\":{\"image\":[\"1\",0],\"bbox\":[" + x.ToString(CultureInfo.InvariantCulture) + ","
                   + y.ToString(CultureInfo.InvariantCulture) + ","
                   + width.ToString(CultureInfo.InvariantCulture) + ","
                   + height.ToString(CultureInfo.InvariantCulture) + "],\"point\":["
                   + centerX.ToString(CultureInfo.InvariantCulture) + ","
                   + centerY.ToString(CultureInfo.InvariantCulture) + "]}},"
                   + "\"3\":{\"class_type\":\"JoinImageWithAlpha\",\"inputs\":{\"image\":[\"1\",0],\"alpha\":[\"2\",0]}},"
                   + "\"4\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"3\",0],\"filename_prefix\":" + JsonString(outputPrefix) + "}}"
                   + "}";
        }

        /// <summary>
        /// 根据运行上下文 patch workflow JSON。支持占位符替换与常见 frontend 节点 widgets_values 更新。
        /// </summary>
        /// <param name="workflowJson">原始 workflow JSON。</param>
        /// <param name="config">ComfyUI 配置。</param>
        /// <param name="context">本次运行参数。</param>
        /// <returns>patch 后的 workflow JSON。</returns>
        public static string PatchWorkflowJson(string workflowJson, ComfyUiServiceConfig config, ComfyWorkflowPatchContext context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));
            config.Normalize();

            string patched = ReplacePlaceholders(workflowJson ?? string.Empty, config, context);
            patched = PatchNodes(patched, config, context);
            return patched;
        }

        /// <summary>
        /// 替换 workflow 模板中的 Draft Workbench 占位符。
        /// </summary>
        private static string ReplacePlaceholders(string workflowJson, ComfyUiServiceConfig config, ComfyWorkflowPatchContext context)
        {
            return workflowJson
                .Replace("{{input_image}}", context.InputImagePath ?? string.Empty)
                .Replace("{{input_asset_path}}", context.InputAssetPath ?? string.Empty)
                .Replace("{{output_prefix}}", context.OutputPrefix ?? string.Empty)
                .Replace("{{output_directory}}", context.OutputDirectory ?? string.Empty)
                .Replace("{{manifest_path}}", context.ManifestPath ?? string.Empty)
                .Replace("{{source_hash}}", context.SourceHash ?? string.Empty)
                .Replace("{{source_width}}", context.SourceWidth.ToString(CultureInfo.InvariantCulture))
                .Replace("{{source_height}}", context.SourceHeight.ToString(CultureInfo.InvariantCulture))
                .Replace("{{min_region_area}}", context.MinRegionArea.ToString(CultureInfo.InvariantCulture))
                .Replace("{{mask_threshold}}", config.MaskThreshold.ToString("0.###", CultureInfo.InvariantCulture))
                .Replace("{{detection_preset}}", config.DetectionPreset ?? string.Empty)
                .Replace("{{label_prompt}}", config.DetectionPrompt ?? string.Empty)
                .Replace("{{detection_prompt}}", config.DetectionPrompt ?? string.Empty)
                .Replace("{{refine_iterations}}", config.RefineIterations.ToString(CultureInfo.InvariantCulture))
                .Replace("{{individual_masks}}", config.IndividualMasks ? "true" : "false");
        }

        /// <summary>
        /// patch 常见 ComfyUI frontend 节点的 widgets_values。
        /// </summary>
        private static string PatchNodes(string workflowJson, ComfyUiServiceConfig config, ComfyWorkflowPatchContext context)
        {
            var spans = ExtractNodeSpans(workflowJson);
            var builder = new StringBuilder(workflowJson);
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                JsonSpan span = spans[i];
                string patched = PatchSingleNode(span.Text, config, context);
                if (patched == span.Text)
                    continue;

                builder.Remove(span.Start, span.Length);
                builder.Insert(span.Start, patched);
            }

            return builder.ToString();
        }

        /// <summary>
        /// patch 单个节点 JSON。
        /// </summary>
        private static string PatchSingleNode(string nodeJson, ComfyUiServiceConfig config, ComfyWorkflowPatchContext context)
        {
            string id = GetMatchValue(IdRegex.Match(nodeJson), "id");
            string type = GetMatchValue(TypeRegex.Match(nodeJson), "type");
            string searchable = BuildSearchableNodeHeader(nodeJson, type);
            bool isLoadImage = IsConfiguredOrDetected(id, config.LoadImageNodeId, searchable.Contains("loadimage"));
            bool isSam3 = IsConfiguredOrDetected(id, config.Sam3NodeId,
                searchable.Contains("sam3") || searchable.Contains("image segment") || searchable.Contains("cliptextencode"));
            bool isOutput = IsConfiguredOrDetected(id, config.OutputNodeId,
                searchable.Contains("saveimage") || searchable.Contains("previewimage") || searchable.Contains("maskpreview") || searchable.Contains("manifest"));

            if (!isLoadImage && !isSam3 && !isOutput)
                return nodeJson;

            var widgetMatch = WidgetArrayRegex.Match(nodeJson);
            if (!widgetMatch.Success)
                return nodeJson;

            var values = SplitJsonArrayItems(widgetMatch.Groups["values"].Value);
            if (isLoadImage && values.Count > 0)
            {
                values[0] = JsonString(context.InputImagePath);
            }

            if (isSam3)
            {
                PatchSam3Widgets(values, config);
            }

            if (isOutput && values.Count > 0 && searchable.Contains("saveimage"))
            {
                values[0] = JsonString(context.OutputPrefix);
            }

            string replacement = "\"widgets_values\": [" + string.Join(", ", values) + "]";
            return WidgetArrayRegex.Replace(nodeJson, replacement, 1);
        }

        /// <summary>
        /// patch SAM3/subgraph widgets_values 的常见参数顺序。
        /// </summary>
        private static void PatchSam3Widgets(List<string> values, ComfyUiServiceConfig config)
        {
            if (values.Count == 0)
                return;

            values[0] = JsonString(config.DetectionPrompt);
            int numericSeen = 0;
            for (int i = 1; i < values.Count; i++)
            {
                string trimmed = values[i].Trim();
                if (IsBooleanToken(trimmed))
                {
                    values[i] = config.IndividualMasks ? "true" : "false";
                    continue;
                }

                if (IsNumberToken(trimmed))
                {
                    numericSeen++;
                    if (numericSeen == 1)
                    {
                        values[i] = config.MaskThreshold.ToString("0.###", CultureInfo.InvariantCulture);
                    }
                    else if (numericSeen == 2)
                    {
                        values[i] = config.RefineIterations.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        /// <summary>
        /// 构建用于节点类型识别的有限头部文本，避免 MarkdownNote 正文干扰自动检测。
        /// </summary>
        private static string BuildSearchableNodeHeader(string nodeJson, string type)
        {
            int widgetIndex = nodeJson.IndexOf("\"widgets_values\"", StringComparison.Ordinal);
            string header = widgetIndex >= 0 ? nodeJson.Substring(0, widgetIndex) : nodeJson;
            return (type + " " + header).ToLowerInvariant();
        }

        /// <summary>
        /// 从所有 nodes 数组中提取节点对象范围，支持 ComfyUI frontend workflow 的嵌套 subgraph。
        /// </summary>
        private static List<JsonSpan> ExtractNodeSpans(string workflowJson)
        {
            var spans = new List<JsonSpan>();
            if (string.IsNullOrEmpty(workflowJson))
                return spans;

            foreach (Match match in NodesKeyRegex.Matches(workflowJson))
            {
                int arrayStart = workflowJson.IndexOf('[', match.Index + match.Length);
                if (arrayStart < 0)
                    continue;

                int arrayEnd = FindMatchingBracket(workflowJson, arrayStart, '[', ']');
                if (arrayEnd < 0)
                    continue;

                ExtractObjectSpansFromArray(workflowJson, arrayStart + 1, arrayEnd, spans);
            }

            spans.Sort((left, right) => left.Start.CompareTo(right.Start));
            return spans;
        }

        /// <summary>
        /// 从 JSON 数组正文中提取顶层对象范围。
        /// </summary>
        private static void ExtractObjectSpansFromArray(string json, int start, int end, List<JsonSpan> spans)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            int objectStart = -1;
            for (int i = start; i < end; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{')
                {
                    if (depth == 0)
                        objectStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        int length = i - objectStart + 1;
                        spans.Add(new JsonSpan(objectStart, length, json.Substring(objectStart, length)));
                        objectStart = -1;
                    }
                }
            }
        }

        /// <summary>
        /// 查找与指定起点匹配的括号位置。
        /// </summary>
        private static int FindMatchingBracket(string json, int start, char open, char close)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 将简单 JSON 数组内容拆为 item 列表，保留字符串字面量。
        /// </summary>
        private static List<string> SplitJsonArrayItems(string valuesText)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(valuesText))
                return values;

            var builder = new StringBuilder();
            bool inString = false;
            bool escaped = false;
            foreach (char c in valuesText)
            {
                if (escaped)
                {
                    builder.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    builder.Append(c);
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    builder.Append(c);
                    continue;
                }

                if (c == ',' && !inString)
                {
                    values.Add(builder.ToString().Trim());
                    builder.Length = 0;
                    continue;
                }

                builder.Append(c);
            }

            if (builder.Length > 0 || valuesText.EndsWith(",", StringComparison.Ordinal))
                values.Add(builder.ToString().Trim());

            return values;
        }

        /// <summary>
        /// 判断节点是否被配置 ID 命中，或满足自动检测条件。
        /// </summary>
        private static bool IsConfiguredOrDetected(string id, string configuredId, bool detected)
        {
            return !string.IsNullOrWhiteSpace(configuredId)
                ? string.Equals(id, configuredId.Trim(), StringComparison.OrdinalIgnoreCase)
                : detected;
        }

        /// <summary>
        /// 获取命名捕获组字符串。
        /// </summary>
        private static string GetMatchValue(Match match, string groupName)
        {
            return match.Success ? match.Groups[groupName].Value.Trim() : string.Empty;
        }

        /// <summary>
        /// 判断字符串是否为 JSON boolean 字面量。
        /// </summary>
        private static bool IsBooleanToken(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断字符串是否为数字字面量。
        /// </summary>
        private static bool IsNumberToken(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        /// <summary>
        /// 创建 JSON 字符串字面量。
        /// </summary>
        private static string JsonString(string value)
        {
            return ComfyUiClient.EncodeJsonString(value ?? string.Empty);
        }
    }
}
#endif
