namespace EF.UI
{
    /// <summary>
    /// 弹窗类型 marker 基类。继承此类的 Screen 派生类型会被 Navigator 识别为弹窗，
    /// 走 PopupLayer 栈式管理而非替换 ScreenLayer。
    ///
    /// 该类型本身不携带任何状态或行为差异——栈管理 / 遮罩 / 动画 等弹窗专属逻辑
    /// 由 Navigator 在打开时按基类继承关系分流实现。后续如有 Popup 专属生命周期钩子
    /// （OnCancel / OnBackdropClicked 等），SHALL 添加到本类型中。
    /// </summary>
    /// <typeparam name="TViewModel">对应的 ViewModel 类型。</typeparam>
    public abstract class Popup<TViewModel> : Screen<TViewModel>
        where TViewModel : ViewModelBase
    {
    }
}
