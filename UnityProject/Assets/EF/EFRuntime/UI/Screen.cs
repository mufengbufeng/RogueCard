using System;
using UnityEngine.UIElements;

namespace EF.UI
{
    /// <summary>
    /// Screen 非泛型基类。Navigator 通过此基类引用 Screen 实例，
    /// 避免泛型协变带来的转型问题。子类通过 Screen&lt;TViewModel&gt; 获取强类型 ViewModel。
    /// </summary>
    public abstract class Screen : VisualElement
    {
        /// <summary>
        /// 创建 Screen，默认撑满父容器。
        /// </summary>
        protected Screen()
        {
            // Screen 撑满所属 layer（layer 是绝对定位全屏容器）。
            // 用 absolute 0,0,0,0 而不是 flex-grow，因为 layer 是 absolute 定位，
            // 它的子元素需要绝对定位才能正确填充 layer 区域。
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
        }

        /// <summary>
        /// UXML 资源 addressable 名。默认按 `{Stem}View → {Stem}Uxml` 命名约定推导，
        /// 子类可 override 指向自定义资源（如非标准命名或共享模板）。
        /// </summary>
        public virtual string UxmlLocation => DeriveAssetName("Uxml");

        /// <summary>
        /// USS 资源 addressable 名（可选）。默认按 `{Stem}View → {Stem}Uss` 命名约定推导。
        /// 资源不存在时 Navigator 不抛异常，仅在 DEBUG 构建打印一次警告。
        /// </summary>
        public virtual string UssLocation => DeriveAssetName("Uss");

        /// <summary>
        /// 由具体类型名按命名约定推导 addressable 名。
        /// `MainView` → `MainUxml` / `MainUss`；
        /// 未以 `View` 结尾的类型直接附加后缀（罕见情况）。
        /// </summary>
        private string DeriveAssetName(string suffix)
        {
            var typeName = GetType().Name;
            if (typeName.EndsWith("View", StringComparison.Ordinal))
            {
                typeName = typeName.Substring(0, typeName.Length - 4);
            }
            return typeName + suffix;
        }

        /// <summary>
        /// 框架调用：加载 UXML 模板并挂载为子节点。
        /// </summary>
        public void LoadContent(VisualTreeAsset vta)
        {
            if (vta == null) throw new ArgumentNullException(nameof(vta));
            var clone = vta.CloneTree();
            // TemplateContainer 撑满 Screen，让 UXML 根元素的尺寸正确传播
            clone.style.flexGrow = 1;
            Add(clone);
        }

        /// <summary>
        /// 框架调用：挂载额外的 StyleSheet 到本 Screen 根元素。
        /// 为约定加载的 USS 提供入口；UXML 内嵌的 `&lt;Style src=...&gt;` 仍由引擎自动处理。
        /// </summary>
        public void AttachStyleSheet(StyleSheet styleSheet)
        {
            if (styleSheet == null) return;
            // styleSheets 是 IList，重复 attach 同一 StyleSheet 引用是幂等的
            styleSheets.Add(styleSheet);
        }

        /// <summary>
        /// 框架调用：注入 ViewModel（任意 ViewModelBase 子类）并触发子类绑定逻辑。
        /// 子类负责验证类型并在内部转型。
        /// </summary>
        public abstract void Setup(ViewModelBase viewModel);

        /// <summary>
        /// Screen 显示时调用。
        /// </summary>
        public virtual void OnShow() { }

        /// <summary>
        /// Screen 隐藏时调用。
        /// </summary>
        public virtual void OnHide() { }

        /// <summary>
        /// Screen 释放时调用。子类负责销毁 ViewModel 并自脱树。
        /// </summary>
        public abstract void OnDispose();
    }

    /// <summary>
    /// Screen 强类型基类。继承 VisualElement，UXML 内容作为子节点挂载。
    /// 子类重写 OnSetup 执行 UQuery 和数据绑定。
    /// </summary>
    /// <typeparam name="TViewModel">对应的 ViewModel 类型。</typeparam>
    public abstract class Screen<TViewModel> : Screen where TViewModel : ViewModelBase
    {
        /// <summary>
        /// 当前绑定的 ViewModel。
        /// </summary>
        protected TViewModel ViewModel { get; private set; }

        /// <inheritdoc />
        public sealed override void Setup(ViewModelBase viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (viewModel is not TViewModel typed)
                throw new ArgumentException(
                    $"Screen<{typeof(TViewModel).Name}> 接收到错误的 ViewModel 类型：{viewModel.GetType().Name}",
                    nameof(viewModel));

            ViewModel = typed;
            OnSetup();
        }

        /// <summary>
        /// 子类重写：执行 UQuery 查找元素 + 订阅 ReactiveProperty + 注册事件。
        /// </summary>
        protected abstract void OnSetup();

        /// <inheritdoc />
        public override void OnDispose()
        {
            ViewModel?.Dispose();
            ViewModel = null;
            this.RemoveFromHierarchy();
        }
    }
}
