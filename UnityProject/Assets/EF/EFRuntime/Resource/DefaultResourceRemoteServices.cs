using YooAsset;

namespace EF.Resource
{
    /// <summary>
    /// 默认的远程资源地址查询服务，实现 YooAssets 所需的 IRemoteServices 接口。
    /// </summary>
    internal sealed class DefaultResourceRemoteServices : IRemoteServices
    {
        private readonly string _mainServer;
        private readonly string _fallbackServer;

        public DefaultResourceRemoteServices(string mainServer, string fallbackServer)
        {
            _mainServer = Normalize(mainServer);
            _fallbackServer = Normalize(fallbackServer);
        }

        /// <inheritdoc />
        public string GetRemoteMainURL(string fileName)
        {
            if (string.IsNullOrEmpty(_mainServer))
            {
                return string.Empty;
            }

            return _mainServer + fileName;
        }

        /// <inheritdoc />
        public string GetRemoteFallbackURL(string fileName)
        {
            if (string.IsNullOrEmpty(_fallbackServer))
            {
                return GetRemoteMainURL(fileName);
            }

            return _fallbackServer + fileName;
        }

        private static string Normalize(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return string.Empty;
            }

            string trimmed = host.Trim();
            return trimmed.EndsWith("/") ? trimmed : trimmed + "/";
        }
    }
}
