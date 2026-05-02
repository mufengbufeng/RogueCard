namespace EF.Common
{
    /// <summary>
    /// 定义 EF 框架管理器通用的生命周期能力。
    /// </summary>
    public interface IEFManager
    {
        /// <summary>
        /// Unity 生命周期中每帧调用，用于驱动内部逻辑。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 关闭管理器并释放内部资源。
        /// </summary>
        void Shutdown();
    }
}
