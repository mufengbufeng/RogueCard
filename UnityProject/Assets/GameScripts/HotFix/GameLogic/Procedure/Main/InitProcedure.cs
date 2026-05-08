using EF.Debugger;
using EF.Procedure;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 初始化流程。
    /// </summary>
    public class InitProcedure : ProcedureBase
    {
        /// <summary>
        /// 初始化流程实例。
        /// </summary>
        protected internal override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            Log.Info("[InitProcedure] OnInit");
        }

        /// <summary>
        /// 进入初始化流程。
        /// </summary>
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            Log.Info("[InitProcedure] 初始化完成，切换到主界面流程");
            ChangeState<MainMenuProcedure>(procedureOwner);
        }

        /// <summary>
        /// 离开初始化流程。
        /// </summary>
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            Log.Info("[InitProcedure] OnLeave");
        }

        /// <summary>
        /// 销毁初始化流程。
        /// </summary>
        protected internal override void OnDestroy(ProcedureOwner procedureOwner)
        {
            base.OnDestroy(procedureOwner);
            Log.Info("[InitProcedure] OnDestroy");
        }
    }
}
