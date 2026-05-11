using System.Threading;
using Cysharp.Threading.Tasks;

namespace EF.UI
{
    /// <summary>
    /// 导航服务接口。统一入口 OpenAsync 按目标类型自动分流：
    /// 派生自 <see cref="Popup{TViewModel}"/> 的类型走 PopupLayer 栈式管理，
    /// 其余 <see cref="Screen{TViewModel}"/> 派生类型替换 ScreenLayer。
    /// </summary>
    public interface INavigator
    {
        /// <summary>
        /// 按类型打开 Screen 或 Popup。ViewModel 可由调用方注入；
        /// 传入 null 时 Navigator 通过 <c>Activator.CreateInstance</c> 自动创建。
        /// </summary>
        /// <typeparam name="TScreen">目标 Screen 类型，必须有无参构造。</typeparam>
        /// <param name="viewModel">可选的 ViewModel 实例。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        UniTask OpenAsync<TScreen>(ViewModelBase viewModel = null, CancellationToken cancellationToken = default)
            where TScreen : Screen, new();

        /// <summary>
        /// 按字符串名打开 Screen 或 Popup。Navigator 通过反射查找名为 <paramref name="viewName"/>
        /// 的非抽象 Screen 派生类型并缓存。供配置表 / 数据驱动场景使用。
        /// </summary>
        /// <param name="viewName">Screen 类型名（不含命名空间）。</param>
        /// <param name="viewModel">可选的 ViewModel 实例。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        UniTask OpenAsync(string viewName, ViewModelBase viewModel = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 关闭最顶层 Popup。若无 Popup，方法静默返回——不影响 ScreenLayer。
        /// </summary>
        void Close();

        /// <summary>
        /// 关闭所有 Popup（保留当前 ScreenLayer 内容）。
        /// </summary>
        void CloseAll();

        /// <summary>
        /// 关闭所有 Screen 和 Popup，清理层级容器。
        /// </summary>
        void Shutdown();
    }
}
