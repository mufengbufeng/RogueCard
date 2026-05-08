using System.Threading;
using Cysharp.Threading.Tasks;

namespace EF.UI
{
    /// <summary>
    /// 导航服务接口。管理 Screen 内容替换和 Popup 栈式导航。
    /// </summary>
    public interface INavigator
    {
        /// <summary>
        /// 导航到指定 Screen（替换 ScreenLayer 内容）。
        /// </summary>
        UniTask NavigateToAsync(string screenName, ViewModelBase viewModel, CancellationToken cancellationToken = default);

        /// <summary>
        /// 推入弹窗到 PopupLayer（栈式管理）。
        /// </summary>
        UniTask PushPopupAsync(string popupName, ViewModelBase viewModel, CancellationToken cancellationToken = default);

        /// <summary>
        /// 弹出最顶层弹窗。
        /// </summary>
        void PopPopup();

        /// <summary>
        /// 关闭所有 Screen 和 Popup，清理层级容器。
        /// </summary>
        void Shutdown();
    }
}
