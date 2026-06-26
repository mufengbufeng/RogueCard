#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace RogueCard.Editor.DraftWorkbench
{
    /// <summary>
    /// AI 服务配置 ScriptableObject，用于持久化存储 AI 服务连接参数。
    /// </summary>
    [CreateAssetMenu(fileName = "AiServiceConfig", menuName = "Tools/RogueCard/AI Service Config")]
    public class AiServiceConfig : ScriptableObject
    {
        public const int DefaultTimeoutSeconds = 300;
        public const int MinTimeoutSeconds = 10;
        public const int MaxTimeoutSeconds = 1800;

        /// <summary>
        /// API 请求的完整 URL。
        /// <para>OpenAI: https://api.openai.com/v1/chat/completions</para>
        /// <para>OpenAI Responses: https://api.openai.com/v1/responses</para>
        /// <para>Responses API: http://127.0.0.1:8080/responses</para>
        /// <para>Ollama: http://localhost:11434/v1/chat/completions</para>
        /// </summary>
        [Header("连接配置")]
        [Tooltip("API 完整请求 URL（含路径，如 /v1/chat/completions 或 /responses）")]
        public string Endpoint = "https://api.openai.com/v1/chat/completions";

        /// <summary>
        /// API 密钥。
        /// </summary>
        [Tooltip("API 密钥")]
        public string ApiKey = "";

        /// <summary>
        /// 要使用的模型名称。
        /// <para>如果走 Responses API + image_generation 工具，这里应填写主模型（如 gpt-5 / gpt-4.1 / gpt-4o），而不是 gpt-image-*。</para>
        /// </summary>
        [Tooltip("要使用的模型名称。Responses API + image_generation 工具请填写主模型（如 gpt-5 / gpt-4.1 / gpt-4o），不要直接填写 gpt-image-*")]
        public string Model = "gpt-4o";

        /// <summary>
        /// 是否使用 JSON 模式（response_format: json_object）。
        /// </summary>
        [Header("高级选项")]
        [Tooltip("是否使用 JSON 模式（response_format: json_object）")]
        public bool UseJsonMode = false;

        /// <summary>
        /// 是否使用 Responses API 请求格式。
        /// <para>false = Chat Completions 格式（messages + image_url），适用于大多数服务和代理。</para>
        /// <para>true = Responses API 格式（input + input_image），适用于 OpenAI 官方 Responses API。</para>
        /// </summary>
        [Tooltip("请求体格式：关闭=Chat Completions（通用），开启=Responses API（OpenAI 新格式）")]
        public bool UseResponsesApiFormat = false;

        /// <summary>
        /// 请求超时时间（秒）。
        /// </summary>
        [Tooltip("请求超时时间（秒）")]
        public int TimeoutSeconds = DefaultTimeoutSeconds;

        public static int NormalizeTimeoutSeconds(int timeoutSeconds)
        {
            return Mathf.Clamp(timeoutSeconds <= 0 ? DefaultTimeoutSeconds : timeoutSeconds,
                MinTimeoutSeconds,
                MaxTimeoutSeconds);
        }

        /// <summary>
        /// 从指定路径加载 AiServiceConfig 资源；若不存在则创建新实例。
        /// </summary>
        /// <returns>已加载或新创建的 AiServiceConfig 实例。</returns>
        public static AiServiceConfig GetOrCreate()
        {
            const string assetPath = "Assets/GameScripts/Editor/DraftWorkbench/AiServiceConfig.asset";

            var config = AssetDatabase.LoadAssetAtPath<AiServiceConfig>(assetPath);
            if (config != null)
            {
                return config;
            }

            config = CreateInstance<AiServiceConfig>();
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}

#endif
