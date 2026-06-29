#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// ComfyUI 自动切图服务配置，保存本地服务地址、workflow、缓存目录和 Sprite 导入参数。
    /// </summary>
    [CreateAssetMenu(fileName = "ComfyUiServiceConfig", menuName = "Tools/RogueCard/ComfyUI Service Config")]
    public class ComfyUiServiceConfig : ScriptableObject
    {
        /// <summary>默认配置资产路径。</summary>
        public const string DefaultAssetPath = "Assets/GameScripts/Editor/DraftWorkbench/ComfyUiServiceConfig.asset";

        /// <summary>默认 ComfyUI 本地地址。</summary>
        public const string DefaultBaseUrl = "http://127.0.0.1:8188";

        /// <summary>默认 ComfyUI workflow 资源路径。</summary>
        public const string DefaultWorkflowJsonAssetPath = "Assets/GameScripts/Editor/DraftWorkbench/ComfyWorkflows/Sam3.json";

        /// <summary>默认最终 Sprite 输出根目录。</summary>
        public const string DefaultGeneratedSpriteOutputRoot = "Assets/AssetRaw/Image/DraftWorkbenchGenerated";

        /// <summary>默认 ComfyUI 中间缓存根目录。</summary>
        public const string DefaultCacheRoot = "Library/DraftWorkbench/ComfyCache";

        /// <summary>默认 SAM3 检测 preset 名称。</summary>
        public const string DefaultDetectionPreset = "UI Complete";

        /// <summary>默认 SAM3 文本提示词。</summary>
        public const string DefaultDetectionPrompt = "icon button panel background decoration";

        /// <summary>ComfyUI HTTP 服务基础地址。</summary>
        public string BaseUrl = DefaultBaseUrl;

        /// <summary>ComfyUI workflow JSON 资源。</summary>
        public TextAsset WorkflowJsonAsset;

        /// <summary>workflow 资源路径兜底字段，未指定资源对象时使用。</summary>
        public string WorkflowJsonAssetPath;

        /// <summary>HTTP 请求超时时间（秒）。</summary>
        public int TimeoutSeconds = 60;

        /// <summary>轮询 ComfyUI history 的间隔（秒）。</summary>
        public float PollIntervalSeconds = 1f;

        /// <summary>等待 ComfyUI 任务完成的最长时间（秒）。</summary>
        public int MaxPollSeconds = 300;

        /// <summary>裁剪区域固定扩张像素。</summary>
        public int RegionExpandPixels = 4;

        /// <summary>裁剪区域按最大边长扩张的比例。</summary>
        public float RegionExpandPercent = 0.04f;

        /// <summary>小于该面积的 region 会被忽略。</summary>
        public int MinRegionArea = 64;

        /// <summary>单次最多导入的 region 数量。</summary>
        public int MaxRegions = 64;

        /// <summary>检测 preset，用于 UI 展示和 workflow adapter patch。</summary>
        public string DetectionPreset = DefaultDetectionPreset;

        /// <summary>SAM3 检测文本提示词。</summary>
        public string DetectionPrompt = DefaultDetectionPrompt;

        /// <summary>SAM3 mask 阈值，范围 0~1。</summary>
        public float MaskThreshold = 0.5f;

        /// <summary>SAM3 refine iterations。</summary>
        public int RefineIterations = 1;

        /// <summary>是否要求 SAM3 输出独立 masks。</summary>
        public bool IndividualMasks = true;

        /// <summary>可选 LoadImage 节点 ID 覆盖。</summary>
        public string LoadImageNodeId;

        /// <summary>可选 SAM3/subgraph 节点 ID 覆盖。</summary>
        public string Sam3NodeId;

        /// <summary>可选输出节点 ID 覆盖。</summary>
        public string OutputNodeId;

        /// <summary>低于该置信度的自动匹配需要人工复核。</summary>
        public float AssignmentAutoApproveConfidence = 0.75f;

        /// <summary>最终可被 prefab 引用的 Sprite 输出根目录。</summary>
        public string GeneratedSpriteOutputRoot = DefaultGeneratedSpriteOutputRoot;

        /// <summary>ComfyUI 原始输出和调试文件缓存根目录。</summary>
        public string CacheRoot = DefaultCacheRoot;

        /// <summary>导入 Sprite 的 Pixels Per Unit。</summary>
        public float SpritePixelsPerUnit = 100f;

        /// <summary>导入 Sprite 时使用 FullRect mesh，避免透明边缘被裁掉。</summary>
        public bool UseFullRectMesh = true;

        /// <summary>透明前景 refinement 默认使用的工作流。</summary>
        public DraftRefinementWorkflowKind TransparentWorkflowKind = DraftRefinementWorkflowKind.BiRefNetRmbg;

        /// <summary>透明前景默认 workflow alpha 校验失败后是否尝试 Sam2 fallback。</summary>
        public bool EnableSam2Fallback;

        /// <summary>透明前景远端 workflow 失败后是否启用本地边缘背景抠除兜底。</summary>
        public bool EnableLocalTransparentFallback = true;

        /// <summary>本地透明抠除判断背景颜色的最大 RGB 距离。</summary>
        public float LocalCutoutColorDistance = 72f;

        /// <summary>透明前景 raw crop 默认使用方形透明画布。</summary>
        public bool TransparentForegroundMakeSquare = true;

        /// <summary>alpha 小于等于该值视为透明像素。</summary>
        public int AlphaTransparentThreshold = 5;

        /// <summary>alpha 大于等于该值视为不透明像素。</summary>
        public int AlphaOpaqueThreshold = 250;

        /// <summary>透明前景通过校验所需的最小透明像素比例。</summary>
        public float MinimumTransparentPixelRatio = 0.001f;

        /// <summary>透明前景边缘允许的不透明像素比例上限。</summary>
        public float MaximumBorderOpaqueRatio = 0.95f;

        /// <summary>
        /// 加载或创建默认 ComfyUI 配置资产。
        /// </summary>
        /// <returns>配置资产实例。</returns>
        public static ComfyUiServiceConfig GetOrCreate()
        {
            var config = AssetDatabase.LoadAssetAtPath<ComfyUiServiceConfig>(DefaultAssetPath);
            if (config != null)
                return config;

            config = CreateInstance<ComfyUiServiceConfig>();
            AssetDatabase.CreateAsset(config, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// 返回规范化后的 Base URL，不带末尾斜杠。
        /// </summary>
        /// <returns>规范化 URL。</returns>
        public string GetNormalizedBaseUrl()
        {
            string value = string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.Trim();
            return value.TrimEnd('/');
        }

        /// <summary>
        /// 规范化配置中的数值范围，避免非法参数进入切图流程。
        /// </summary>
        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl)) BaseUrl = DefaultBaseUrl;
            if (WorkflowJsonAsset == null && string.IsNullOrWhiteSpace(WorkflowJsonAssetPath)) WorkflowJsonAssetPath = DefaultWorkflowJsonAssetPath;
            if (string.IsNullOrWhiteSpace(GeneratedSpriteOutputRoot)) GeneratedSpriteOutputRoot = DefaultGeneratedSpriteOutputRoot;
            if (string.IsNullOrWhiteSpace(CacheRoot)) CacheRoot = DefaultCacheRoot;

            TimeoutSeconds = Mathf.Clamp(TimeoutSeconds, 5, 600);
            PollIntervalSeconds = Mathf.Clamp(PollIntervalSeconds, 0.25f, 10f);
            MaxPollSeconds = Mathf.Clamp(MaxPollSeconds, 10, 3600);
            if (!GeneratedSpriteOutputRoot.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                GeneratedSpriteOutputRoot = DefaultGeneratedSpriteOutputRoot;
            if (string.IsNullOrWhiteSpace(DetectionPreset)) DetectionPreset = DefaultDetectionPreset;
            if (string.IsNullOrWhiteSpace(DetectionPrompt)) DetectionPrompt = DefaultDetectionPrompt;

            RegionExpandPixels = Mathf.Max(0, RegionExpandPixels);
            RegionExpandPercent = Mathf.Clamp01(RegionExpandPercent);
            MinRegionArea = Mathf.Max(1, MinRegionArea);
            MaxRegions = Mathf.Clamp(MaxRegions, 1, 512);
            MaskThreshold = Mathf.Clamp01(MaskThreshold);
            RefineIterations = Mathf.Clamp(RefineIterations, 0, 16);
            AssignmentAutoApproveConfidence = Mathf.Clamp01(AssignmentAutoApproveConfidence);
            SpritePixelsPerUnit = Mathf.Max(1f, SpritePixelsPerUnit);
            LocalCutoutColorDistance = Mathf.Clamp(LocalCutoutColorDistance, 16f, 160f);
            AlphaTransparentThreshold = Mathf.Clamp(AlphaTransparentThreshold, 0, 254);
            AlphaOpaqueThreshold = Mathf.Clamp(AlphaOpaqueThreshold, AlphaTransparentThreshold + 1, 255);
            MinimumTransparentPixelRatio = Mathf.Clamp01(MinimumTransparentPixelRatio);
            MaximumBorderOpaqueRatio = Mathf.Clamp01(MaximumBorderOpaqueRatio);
        }
    }
}
#endif
