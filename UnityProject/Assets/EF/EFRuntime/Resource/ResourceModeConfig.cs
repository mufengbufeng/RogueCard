using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF.Resource
{
    /// <summary>
    /// 资源模块的基础配置，通过 ScriptableObject 控制运行模式与包裹信息。
    /// </summary>
    [CreateAssetMenu(menuName = "EF/资源/资源模块配置", fileName = "EFResourceModeConfig")]
    public sealed class ResourceModeConfig : ScriptableObject
    {
        /// <summary>
        /// Resources.Load 时约定的默认路径。
        /// </summary>
        public const string DefaultResourcesPath = "EFResourceModeConfig";

        [SerializeField]
        private ResourceMode _mode = ResourceMode.EditorSimulate;

        [SerializeField]
        [Tooltip("同时加载 AssetBundle 的最大并发数，合理设置可避免 IO 峰值")]
        private int _bundleLoadingMaxConcurrency = 8;

        [SerializeField]
        [Tooltip("资源包配置列表，至少需要配置一个包裹")]
        private List<ResourcePackageEntry> _packages = new();

        /// <summary>
        /// 当前资源运行模式。
        /// </summary>
        public ResourceMode Mode => _mode;

        /// <summary>
        /// AssetBundle 并发加载上限。
        /// </summary>
        public int BundleLoadingMaxConcurrency => Mathf.Clamp(_bundleLoadingMaxConcurrency, 1, 1024);

        /// <summary>
        /// 所有包裹配置。
        /// </summary>
        public IReadOnlyList<ResourcePackageEntry> Packages => _packages;

        /// <summary>
        /// 返回默认包裹配置，若未显式标记则取列表第一项。
        /// </summary>
        public ResourcePackageEntry GetDefaultPackage()
        {
            if (_packages == null || _packages.Count == 0)
            {
                return null;
            }

            foreach (ResourcePackageEntry entry in _packages)
            {
                if (entry != null && entry.IsDefault)
                {
                    return entry;
                }
            }

            return _packages[0];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_packages == null)
            {
                _packages = new List<ResourcePackageEntry>();
                return;
            }

            bool hasDefault = false;
            for (int i = 0; i < _packages.Count; i++)
            {
                ResourcePackageEntry entry = _packages[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.IsDefault)
                {
                    if (!hasDefault)
                    {
                        hasDefault = true;
                    }
                    else
                    {
                        entry.SetDefault(false);
                    }
                }
            }

            if (!hasDefault && _packages.Count > 0 && _packages[0] != null)
            {
                _packages[0].SetDefault(true);
            }
        }
#endif
    }

    /// <summary>
    /// 单个资源包裹的配置项。
    /// </summary>
    [Serializable]
    public sealed class ResourcePackageEntry
    {
        [SerializeField]
        [Tooltip("资源包名称，需要与 YooAssets 构建时的包裹名称保持一致")]
        private string _packageName = "DefaultPackage";

        [SerializeField]
        [Tooltip("是否作为默认包裹，用于未指定包名的加载请求")]
        private bool _isDefault = true;

        [SerializeField]
        [Tooltip("主资源服地址，例如 https://cdn.example.com/bundles")]
        private string _remoteMainServer = string.Empty;

        [SerializeField]
        [Tooltip("备用资源服地址，可选项，用于主服异常时回退")]
        private string _remoteFallbackServer = string.Empty;

        [SerializeField]
        [Tooltip("在 Web 平台上禁用 Unity 自带缓存，避免部分浏览器的缓存问题")]
        private bool _disableUnityWebCache;

        /// <summary>
        /// 包裹名称。
        /// </summary>
        public string PackageName => string.IsNullOrWhiteSpace(_packageName) ? "DefaultPackage" : _packageName.Trim();

        /// <summary>
        /// 是否默认包裹。
        /// </summary>
        public bool IsDefault => _isDefault;

        /// <summary>
        /// 主资源服地址。
        /// </summary>
        public string RemoteMainServer => _remoteMainServer;

        /// <summary>
        /// 备用资源服地址。
        /// </summary>
        public string RemoteFallbackServer => _remoteFallbackServer;

        /// <summary>
        /// 是否禁用 Unity Web 缓存。
        /// </summary>
        public bool DisableUnityWebCache => _disableUnityWebCache;

        /// <summary>
        /// 归一化后的主资源服地址。
        /// </summary>
        public string GetSanitizedMainServer()
        {
            return SanitizeUrl(_remoteMainServer);
        }

        /// <summary>
        /// 归一化后的备用资源服地址。
        /// </summary>
        public string GetSanitizedFallbackServer()
        {
            return SanitizeUrl(_remoteFallbackServer);
        }

        internal void SetDefault(bool value)
        {
            _isDefault = value;
        }

        private static string SanitizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string trimmed = url.Trim();
            return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
        }
    }
}
