using System;
using UnityEngine.UIElements;

namespace GameLogic
{
    /// <summary>
    /// 单击放大预览控制器。维护 _previewSource 字段（当前正在预览的源卡 VisualElement 引用），
    /// 用引用判断"是否同一张卡"——reorder 后索引会变，只有引用比较可靠。
    /// 通过 IPreviewSurface 间接操作 UI（克隆模板 / 加入 preview-layer / 计算坐标 / 清 hover 类）。
    /// </summary>
    public sealed class CardPreviewController : IDisposable
    {
        private readonly IPreviewSurface _surface;
        private readonly HandFanLayoutOptions _options;

        private VisualElement _previewClone;
        private VisualElement _previewSource;
        private bool _disposed;

        /// <summary>当前是否处于预览态。</summary>
        public bool IsPreviewing => _previewClone != null;

        /// <summary>构造预览控制器。</summary>
        public CardPreviewController(IPreviewSurface surface, HandFanLayoutOptions options)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _options = options ?? new HandFanLayoutOptions();
        }

        /// <summary>
        /// 单击切换预览态。同卡（_previewSource == source）→ ExitPreview；别卡 → 先 ExitPreview 再 EnterPreview。
        /// 用引用判断而非索引：reorder 后索引不可靠。
        /// </summary>
        public void TogglePreview(int handIdx, VisualElement source)
        {
            if (_disposed || source == null) return;

            if (_previewClone != null && _previewSource == source)
            {
                ExitPreview();
                return;
            }

            ExitPreview();
            EnterPreview(handIdx, source);
        }

        /// <summary>
        /// 进入预览态。先清掉所有 hover 类，再克隆模板 + 加入 preview-layer + 计算锚点位置。
        /// 锚点：源卡未旋转 layout 顶部中心 → hand-fan 局部 → preview-layer 局部坐标，
        /// 克隆卡左上 = (localX - CardWidth/2, localY - CardHeight)，配合 USS transform-origin: 50% 100% 与 scale 1.6 形成"从顶部长大"效果。
        /// </summary>
        public void EnterPreview(int handIdx, VisualElement source)
        {
            if (_disposed || source == null) return;

            _surface.ClearAllHoverState();

            var clone = _surface.ClonePreviewElement(source);
            if (clone == null) return;

            clone.AddToClassList("card-item--preview");
            clone.pickingMode = PickingMode.Ignore;

            // 锚点：源卡未旋转 layout 顶部中心
            var sourceTopCenter = new UnityEngine.Vector2(source.layout.center.x, source.layout.yMin);
            var localInPreview = _surface.ConvertHandFanLocalToPreviewLocal(sourceTopCenter);
            clone.style.left = localInPreview.x - _options.CardWidth / 2f;
            clone.style.top = localInPreview.y - _options.CardHeight;

            _surface.AddToPreviewLayer(clone);

            _previewClone = clone;
            _previewSource = source;
        }

        /// <summary>退出预览态：从 preview-layer 移除克隆并清空字段。幂等。</summary>
        public void ExitPreview()
        {
            if (_previewClone != null)
            {
                _surface.RemoveFromPreviewLayer(_previewClone);
            }
            _previewClone = null;
            _previewSource = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ExitPreview();
        }
    }
}
