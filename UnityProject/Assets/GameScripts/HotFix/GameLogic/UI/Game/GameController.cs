using EF.Debugger;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 局内界面控制器。
    /// </summary>
    public class GameController : UIController
    {
        /// <summary>
        /// 初始化局内界面控制器。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            Log.Info("[GameController] 局内界面控制器初始化完成");
        }

        /// <summary>
        /// 进入局内界面。
        /// </summary>
        protected override void OnEnter(object userData)
        {
            base.OnEnter(userData);
            Log.Info("[GameController] 局内界面已进入");
        }

        /// <summary>
        /// 退出局内界面。
        /// </summary>
        protected override void OnExit()
        {
            base.OnExit();
            Log.Info("[GameController] 局内界面已退出");
        }
    }
}
