using System;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Resource;
using UnityEngine.UIElements;

namespace EF.UI
{
    /// <summary>
    /// Screen 内的可切换内容区域。
    /// 持有一个 VisualElement 插槽，按需加载/替换子内容。
    /// </summary>
    public sealed class Region
    {
        private readonly VisualElement _slot;
        private readonly IResourceManager _resources;
        private VisualElement _currentContent;

        /// <summary>
        /// 当前显示的内容根节点，用于 UQuery。
        /// </summary>
        public VisualElement CurrentContent => _currentContent;

        /// <summary>
        /// 创建 Region。
        /// </summary>
        /// <param name="slot">UXML 中预留的空容器 VisualElement。</param>
        /// <param name="resources">资源管理器（可选，动态加载时需要）。</param>
        public Region(VisualElement slot, IResourceManager resources = null)
        {
            _slot = slot ?? throw new ArgumentNullException(nameof(slot));
            _resources = resources;
        }

        /// <summary>
        /// 动态加载 UXML 模板到区域。加载失败时记录警告并返回空内容。
        /// </summary>
        public async UniTask ShowAsync(string uxmlLocation)
        {
            Clear();

            if (_resources == null)
            {
                Log.Warning($"[Region] ShowAsync({uxmlLocation}) 跳过：未配置 IResourceManager");
                return;
            }

            var handle = await _resources.LoadAssetAsync<VisualTreeAsset>(uxmlLocation);
            var vta = handle.AssetObject as VisualTreeAsset;
            if (vta == null)
            {
                Log.Warning($"[Region] ShowAsync({uxmlLocation}) 失败：资源不是有效的 VisualTreeAsset");
                return;
            }

            _currentContent = vta.CloneTree();
            _currentContent.style.flexGrow = 1;
            _slot.Add(_currentContent);
        }

        /// <summary>
        /// 直接放置一个已创建的 VisualElement。
        /// </summary>
        public void Show(VisualElement content)
        {
            Clear();
            _currentContent = content;
            _slot.Add(content);
        }

        /// <summary>
        /// 清空区域内容。
        /// </summary>
        public void Clear()
        {
            _currentContent?.RemoveFromHierarchy();
            _currentContent = null;
        }
    }
}
