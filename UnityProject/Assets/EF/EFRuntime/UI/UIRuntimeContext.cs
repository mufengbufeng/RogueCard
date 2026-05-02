using EF.Model;
using UnityEngine;

namespace EF.UI
{
    /// <summary>
    /// UI 实例在运行时可访问的上下文信息。
    /// </summary>
    public sealed class UIRuntimeContext
    {
        internal UIRuntimeContext(IUIManager manager, ModelManager modelManager, UIWindowDescriptor descriptor, Transform layerRoot)
        {
            Manager = manager;
            ModelManager = modelManager;
            Descriptor = descriptor;
            LayerRoot = layerRoot;
        }

        /// <summary>
        /// 所属的 UI 管理器实例。
        /// </summary>
        public IUIManager Manager { get; }

        /// <summary>
        /// 全局 Model 管理器，用于访问数据 Model。
        /// </summary>
        public ModelManager ModelManager { get; }

        /// <summary>
        /// 对应的界面描述信息。
        /// </summary>
        public UIWindowDescriptor Descriptor { get; }

        /// <summary>
        /// 实例当前所在的层级节点。
        /// </summary>
        public Transform LayerRoot { get; private set; }

        /// <summary>
        /// 层级节点的 RectTransform 视图。
        /// </summary>
        public RectTransform LayerRootRectTransform => LayerRoot as RectTransform;

        internal void UpdateLayerRoot(Transform layerRoot)
        {
            LayerRoot = layerRoot;
        }
    }
}
