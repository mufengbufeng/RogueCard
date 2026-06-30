using System.Collections.Generic;
using YooAsset;

namespace EF.Resource
{
    /// <summary>
    /// 默认的远程资源地址查询服务，实现 YooAssets 所需的 IRemoteService 接口。
    /// </summary>
    internal sealed class DefaultResourceRemoteServices : IRemoteService
    {
        private readonly string _mainServer;
        private readonly string _fallbackServer;

        public DefaultResourceRemoteServices(string mainServer, string fallbackServer)
        {
            _mainServer = Normalize(mainServer);
            _fallbackServer = Normalize(fallbackServer);
        }

        /// <summary>
        /// 根据文件名生成远端资源候选地址，优先使用主服务器，其次使用备用服务器。
        /// </summary>
        /// <param name="fileName">资源文件名或相对路径。</param>
        /// <returns>按主服务器、备用服务器顺序排列的候选地址；未配置服务器时返回原始文件名。</returns>
        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            var urls = new List<string>(2);

            if (!string.IsNullOrEmpty(_mainServer))
            {
                urls.Add(_mainServer + fileName);
            }

            if (!string.IsNullOrEmpty(_fallbackServer))
            {
                string fallbackUrl = _fallbackServer + fileName;
                if (!urls.Contains(fallbackUrl))
                {
                    urls.Add(fallbackUrl);
                }
            }

            if (urls.Count == 0)
            {
                urls.Add(fileName);
            }

            return urls;
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
