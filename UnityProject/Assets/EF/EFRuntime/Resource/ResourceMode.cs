namespace EF.Resource
{
    /// <summary>
    /// 资源运行模式，对齐 YooAssets 的播放模式枚举。
    /// </summary>
    public enum ResourceMode
    {
        /// <summary>
        /// 编辑器模拟模式，便于在 Unity 编辑器环境下调试资源。
        /// </summary>
        EditorSimulate,

        /// <summary>
        /// 离线模式，从本地内置资源中加载内容。
        /// </summary>
        OfflinePlay,

        /// <summary>
        /// 联机模式，通过远程服务器拉取资源并支持缓存。
        /// </summary>
        HostPlay,

        /// <summary>
        /// Web 模式，面向 WebGL 等无本地文件系统的平台。
        /// </summary>
        WebPlay
    }

}
