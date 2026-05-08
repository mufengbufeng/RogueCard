using System;
using UnityEngine.UIElements;

namespace EF.UI
{
    /// <summary>
    /// UI 层级容器。从 UIDocument.rootVisualElement 解析三个预定义的层级容器：
    /// ScreenLayer / PopupLayer / SystemLayer。
    ///
    /// 使用方式：UIDocument 的 SourceAsset 设为 Root.uxml（或代码加载后 Add 到 rootVisualElement），
    /// Root.uxml 内必须包含 name="screen-layer" / "popup-layer" / "system-layer" 的 VisualElement。
    /// 这样 PanelSettings 的 ScaleMode 能直接驱动 root 的尺寸，无需框架代码额外撑满。
    /// </summary>
    public sealed class Shell
    {
        /// <summary>
        /// 主界面层级（内容替换，同一时刻只有一个 Screen）。
        /// </summary>
        public VisualElement ScreenLayer { get; }

        /// <summary>
        /// 弹窗层级（栈式管理，后进先出）。
        /// </summary>
        public VisualElement PopupLayer { get; }

        /// <summary>
        /// 系统层级（Toast、Loading 遮罩等）。
        /// </summary>
        public VisualElement SystemLayer { get; }

        /// <summary>
        /// 从 root VisualElement 中解析三个层级容器。
        /// </summary>
        /// <param name="root">UIDocument.rootVisualElement 或其子容器，必须包含三个命名层级。</param>
        /// <exception cref="ArgumentNullException">root 为 null。</exception>
        /// <exception cref="InvalidOperationException">root 缺少必需的层级容器。</exception>
        public Shell(VisualElement root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            ScreenLayer = root.Q<VisualElement>("screen-layer")
                ?? throw new InvalidOperationException("Root.uxml 缺少 name=\"screen-layer\" 元素");
            PopupLayer = root.Q<VisualElement>("popup-layer")
                ?? throw new InvalidOperationException("Root.uxml 缺少 name=\"popup-layer\" 元素");
            SystemLayer = root.Q<VisualElement>("system-layer")
                ?? throw new InvalidOperationException("Root.uxml 缺少 name=\"system-layer\" 元素");
        }
    }
}
