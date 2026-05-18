using System;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// UGUI 单击卡牌预览控制器，使用源卡引用判断同卡切换。
    /// </summary>
    public sealed class CardPreviewController : IDisposable
    {
        private readonly IPreviewSurface _surface;
        private readonly HandFanLayoutOptions _options;

        private GameObject _previewClone;
        private CardItemView _previewSource;
        private bool _disposed;

        /// <summary>
        /// 当前是否处于预览态。
        /// </summary>
        public bool IsPreviewing => _previewClone != null;

        /// <summary>
        /// 创建预览控制器。
        /// </summary>
        public CardPreviewController(IPreviewSurface surface, HandFanLayoutOptions options)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _options = options ?? new HandFanLayoutOptions();
        }

        /// <summary>
        /// 单击切换预览态：同卡关闭，别卡替换。
        /// </summary>
        public void TogglePreview(int handIdx, CardItemView source)
        {
            if (_disposed || source == null)
            {
                return;
            }

            if (_previewClone != null && ReferenceEquals(_previewSource, source))
            {
                ExitPreview();
                return;
            }

            ExitPreview();
            EnterPreview(handIdx, source);
        }

        /// <summary>
        /// 进入预览态。
        /// </summary>
        public void EnterPreview(int handIdx, CardItemView source)
        {
            if (_disposed || source == null)
            {
                return;
            }

            _surface.ClearAllHoverState();
            GameObject clone = _surface.ClonePreviewElement(source);
            if (clone == null)
            {
                return;
            }

            Vector2 sourceTopCenter = _surface.GetSourceTopCenterInHandFanLocal(source);
            Vector2 previewTopCenter = _surface.ConvertHandFanLocalToPreviewLocal(sourceTopCenter);
            _surface.ApplyPreviewTransform(clone, previewTopCenter, _options);
            _surface.AddToPreviewLayer(clone);

            _previewClone = clone;
            _previewSource = source;
        }

        /// <summary>
        /// 退出预览态。
        /// </summary>
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
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ExitPreview();
        }
    }
}
