using System;

namespace EF.Fsm
{
    /// <summary>
    /// 有限状态机中的状态基类，提供生命周期回调与辅助方法。
    /// </summary>
    /// <typeparam name="TOwner">宿主类型。</typeparam>
    public abstract class FsmState<TOwner> where TOwner : class
    {
        /// <summary>
        /// 状态显示名称，默认返回类型名。
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 初始化状态时触发，一般用于缓存引用。
        /// </summary>
        protected internal virtual void OnInit(IFsm<TOwner> fsm)
        {
        }

        /// <summary>
        /// 状态被激活时触发。
        /// </summary>
        protected internal virtual void OnEnter(IFsm<TOwner> fsm)
        {
        }

        /// <summary>
        /// 状态每帧更新时触发。
        /// </summary>
        protected internal virtual void OnUpdate(IFsm<TOwner> fsm, float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 状态被离开时触发。
        /// </summary>
        /// <param name="isShutdown">状态机是否处于彻底关闭流程。</param>
        protected internal virtual void OnLeave(IFsm<TOwner> fsm, bool isShutdown)
        {
        }

        /// <summary>
        /// 状态被销毁时触发，确保释放引用。
        /// </summary>
        protected internal virtual void OnDestroy(IFsm<TOwner> fsm)
        {
        }

        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        protected void ChangeState<TState>(IFsm<TOwner> fsm) where TState : FsmState<TOwner>
        {
            if (fsm == null)
            {
                throw new ArgumentNullException(nameof(fsm), "状态机引用不能为空。");
            }

            fsm.ChangeState<TState>();
        }

        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        protected void ChangeState(IFsm<TOwner> fsm, Type stateType)
        {
            if (fsm == null)
            {
                throw new ArgumentNullException(nameof(fsm), "状态机引用不能为空。");
            }

            if (stateType == null)
            {
                throw new ArgumentNullException(nameof(stateType), "状态类型不能为空。");
            }

            fsm.ChangeState(stateType);
        }

        /// <summary>
        /// 主动停止状态机运行。
        /// </summary>
        protected void Stop(IFsm<TOwner> fsm)
        {
            if (fsm == null)
            {
                throw new ArgumentNullException(nameof(fsm), "状态机引用不能为空。");
            }

            fsm.Stop();
        }
    }
}
