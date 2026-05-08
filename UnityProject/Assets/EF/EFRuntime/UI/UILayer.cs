namespace EF.UI
{
    /// <summary>
    /// 旧 UI 工具链遗留枚举，仅供 ReferenceCollectorScriptGenerator 模板字符串引用。
    /// 新 MVVM UI 框架使用 Shell 内部的 ScreenLayer / PopupLayer / SystemLayer，无需此枚举。
    /// </summary>
    public enum UILayer
    {
        /// <summary>
        /// 背景层，多用于主界面背景、全屏渲染。
        /// </summary>
        Background = 0,

        /// <summary>
        /// 常规层，用于大部分交互界面。
        /// </summary>
        Normal = 10,

        /// <summary>
        /// 弹窗层，用于模态弹窗或浮层。
        /// </summary>
        Popup = 20,

        /// <summary>
        /// 最高层级的遮罩、提示或加载界面。
        /// </summary>
        Overlay = 30
    }
}
