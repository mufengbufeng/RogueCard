namespace EF.Fsm
{
    /// <summary>
    /// FSM 内部控制接口，管理器使用。
    /// </summary>
    internal interface IFsmInternal : IFsm
    {
        /// <summary>
        /// 每帧更新。
        /// </summary>
        void InternalUpdate(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 彻底销毁并回收资源。
        /// </summary>
        void InternalShutdown();
    }
}
