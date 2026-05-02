namespace EF.Timer
{
    /// <summary>
    /// 定义计时器运行的时间来源模式。
    /// </summary>
    public enum TimerMode
    {
        /// <summary>
        /// 本地时间模式，使用本地累积的真实时间。
        /// </summary>
        Local = 0,

        /// <summary>
        /// 服务器时间模式，需要先同步服务器时间戳。
        /// </summary>
        Server = 1
    }
}
