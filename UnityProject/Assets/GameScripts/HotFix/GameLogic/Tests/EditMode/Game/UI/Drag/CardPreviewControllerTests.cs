using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// CardPreviewController 单元测试：通过 MockPreviewSurface 验证 TogglePreview 的同卡退出 / 别卡切换 / Dispose 行为。
    /// 重点验证 _previewSource 引用比较语义（reorder 后仍正确识别同卡）。
    /// </summary>
    [TestFixture]
    public class CardPreviewControllerTests
    {
        private MockPreviewSurface _surface;
        private HandFanLayoutOptions _options;
        private CardPreviewController _controller;

        [SetUp]
        public void SetUp()
        {
            _surface = new MockPreviewSurface();
            _options = new HandFanLayoutOptions();
            _controller = new CardPreviewController(_surface, _options);
        }

        [TearDown]
        public void TearDown() => _controller?.Dispose();

        [Test]
        public void TogglePreview_SameCard_ExitsPreview()
        {
            var sourceA = new VisualElement();

            _controller.TogglePreview(handIdx: 1, source: sourceA);
            Assert.IsTrue(_controller.IsPreviewing);
            Assert.AreEqual(1, _surface.AddToPreviewLayerCallCount);

            _controller.TogglePreview(handIdx: 1, source: sourceA);
            Assert.IsFalse(_controller.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveFromPreviewLayerCallCount);
        }

        [Test]
        public void TogglePreview_DifferentCard_SwitchesPreview()
        {
            var sourceA = new VisualElement();
            var sourceB = new VisualElement();

            _controller.TogglePreview(1, sourceA);
            _controller.TogglePreview(2, sourceB);

            // 切换：先 Remove A clone，再 Add B clone
            Assert.AreEqual(2, _surface.AddToPreviewLayerCallCount);
            Assert.AreEqual(1, _surface.RemoveFromPreviewLayerCallCount);
            Assert.IsTrue(_controller.IsPreviewing);
        }

        [Test]
        public void TogglePreview_AfterReorder_SameSourceReferenceStillExits()
        {
            // sourceA 在 reorder 前 handIdx=2，reorder 后 handIdx=0；引用不变
            var sourceA = new VisualElement();
            _controller.TogglePreview(handIdx: 2, source: sourceA);
            Assert.IsTrue(_controller.IsPreviewing);

            // reorder 后 handIdx 改为 0，但 source 引用不变 → SHALL 识别为同卡 → ExitPreview
            _controller.TogglePreview(handIdx: 0, source: sourceA);
            Assert.IsFalse(_controller.IsPreviewing);
        }

        [Test]
        public void EnterPreview_ClearsAllHoverState_BeforeCloning()
        {
            var sourceA = new VisualElement();
            _controller.EnterPreview(0, sourceA);

            Assert.AreEqual(1, _surface.ClearAllHoverStateCallCount);
        }

        [Test]
        public void EnterPreview_AppliesPreviewClassAndPickingModeIgnore()
        {
            var sourceA = new VisualElement();
            _controller.EnterPreview(0, sourceA);

            var clone = _surface.LastClone;
            Assert.NotNull(clone);
            Assert.IsTrue(clone.ClassListContains("card-item--preview"));
            Assert.AreEqual(PickingMode.Ignore, clone.pickingMode);
        }

        [Test]
        public void Dispose_DuringPreview_DestroysClone_AndIsIdempotent()
        {
            var sourceA = new VisualElement();
            _controller.EnterPreview(0, sourceA);
            Assert.IsTrue(_controller.IsPreviewing);

            _controller.Dispose();
            Assert.IsFalse(_controller.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveFromPreviewLayerCallCount);

            Assert.DoesNotThrow(() => _controller.Dispose());
        }
    }

    /// <summary>测试用 IPreviewSurface 实现：每次 ClonePreviewElement 返回新 VisualElement，记录调用计数。</summary>
    public class MockPreviewSurface : IPreviewSurface
    {
        public int AddToPreviewLayerCallCount;
        public int RemoveFromPreviewLayerCallCount;
        public int ClearAllHoverStateCallCount;
        public Vector2 ConvertResult = Vector2.zero;
        public VisualElement LastClone;

        public VisualElement ClonePreviewElement(VisualElement source)
        {
            LastClone = new VisualElement();
            return LastClone;
        }

        public void AddToPreviewLayer(VisualElement element) => AddToPreviewLayerCallCount++;
        public void RemoveFromPreviewLayer(VisualElement element) => RemoveFromPreviewLayerCallCount++;
        public Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan) => ConvertResult;
        public void ClearAllHoverState() => ClearAllHoverStateCallCount++;
    }
}
