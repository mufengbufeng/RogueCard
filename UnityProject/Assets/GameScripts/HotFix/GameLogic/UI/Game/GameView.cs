using EF.Debugger;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 局内界面视图。
    /// </summary>
    public class GameView : UIView
    {
        /// <summary>
        /// 初始化局内界面视图。
        /// </summary>
        protected override void OnInitialize()
        {
            base.OnInitialize();
            Log.Info("[GameView] 局内界面视图初始化完成");
        }

        /// <summary>
        /// 打开局内界面视图。
        /// </summary>
        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            Log.Info("[GameView] 局内界面已打开");
        }

        /// <summary>
        /// 释放局内界面视图。
        /// </summary>
        protected override void OnRelease()
        {
            Log.Info("[GameView] 局内界面视图已释放");
            base.OnRelease();
        }
    }
}
