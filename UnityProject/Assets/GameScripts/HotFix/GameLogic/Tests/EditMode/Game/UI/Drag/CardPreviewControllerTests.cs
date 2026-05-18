using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI CardPreviewController 行为测试。
    /// </summary>
    [TestFixture]
    public sealed class CardPreviewControllerTests
    {
        private MockPreviewSurface _surface;
        private HandFanLayoutOptions _options;
        private CardPreviewController _controller;
        private GameObject _sourceRoot;
        private CardItemView _sourceA;
        private CardItemView _sourceB;

        [SetUp]
        public void SetUp()
        {
            _surface = new MockPreviewSurface();
            _options = new HandFanLayoutOptions { CardWidth = 120f, CardHeight = 180f };
            _controller = new CardPreviewController(_surface, _options);
            _sourceRoot = UguiTestFactory.CreateRectObject("SourceRoot");
            _sourceA = new CardItemView(UguiTestFactory.CreateCardTemplate("CardA", _sourceRoot.transform), 0, new CardRuntime { Config = UguiTestFactory.NewCard(1, "火球", 2, GameConfig.card.TargetMode.SingleAuto) });
            _sourceB = new CardItemView(UguiTestFactory.CreateCardTemplate("CardB", _sourceRoot.transform), 1, new CardRuntime { Config = UguiTestFactory.NewCard(2, "冰锥", 1, GameConfig.card.TargetMode.SingleAuto) });
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            _sourceA.Dispose();
            _sourceB.Dispose();
            Object.DestroyImmediate(_sourceRoot);
        }

        /// <summary>
        /// 同一张卡再次点击会关闭预览。
        /// </summary>
        [Test]
        public void TogglePreview_同卡关闭()
        {
            _controller.TogglePreview(0, _sourceA);
            Assert.IsTrue(_controller.IsPreviewing);

            _controller.TogglePreview(0, _sourceA);

            Assert.IsFalse(_controller.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveCallCount);
        }

        /// <summary>
        /// 点击另一张卡会替换预览。
        /// </summary>
        [Test]
        public void TogglePreview_别卡切换()
        {
            _controller.TogglePreview(0, _sourceA);
            _controller.TogglePreview(1, _sourceB);

            Assert.IsTrue(_controller.IsPreviewing);
            Assert.AreEqual(2, _surface.CloneCallCount);
            Assert.AreEqual(1, _surface.RemoveCallCount);
        }

        /// <summary>
        /// 进入预览会清 hover、定位并关闭 raycast。
        /// </summary>
        [Test]
        public void EnterPreview_清Hover并创建不可点击克隆()
        {
            _controller.EnterPreview(0, _sourceA);

            Assert.AreEqual(1, _surface.ClearHoverCallCount);
            Assert.AreEqual(new Vector2(10f, 20f), _surface.LastAppliedTopCenter);
            Assert.IsNotNull(_surface.LastClone);
            foreach (Graphic graphic in _surface.LastClone.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget);
            }
        }

        /// <summary>
        /// Dispose 会销毁残留预览，且幂等。
        /// </summary>
        [Test]
        public void Dispose_销毁预览且幂等()
        {
            _controller.EnterPreview(0, _sourceA);

            _controller.Dispose();

            Assert.IsFalse(_controller.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveCallCount);
            Assert.DoesNotThrow(() => _controller.Dispose());
        }
    }

    /// <summary>
    /// 测试用 IPreviewSurface。
    /// </summary>
    public sealed class MockPreviewSurface : IPreviewSurface
    {
        public int CloneCallCount;
        public int RemoveCallCount;
        public int ClearHoverCallCount;
        public GameObject LastClone;
        public Vector2 LastAppliedTopCenter;

        public GameObject ClonePreviewElement(CardItemView source)
        {
            CloneCallCount++;
            LastClone = Object.Instantiate(source.Root);
            foreach (Graphic graphic in LastClone.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            return LastClone;
        }

        public void AddToPreviewLayer(GameObject element)
        {
        }

        public void RemoveFromPreviewLayer(GameObject element)
        {
            RemoveCallCount++;
            UguiViewUtil.DestroyObject(element);
        }

        public Vector2 GetSourceTopCenterInHandFanLocal(CardItemView source) => new Vector2(3f, 4f);

        public Vector2 ConvertHandFanLocalToPreviewLocal(Vector2 sourceTopCenterInHandFan) => new Vector2(10f, 20f);

        public void ApplyPreviewTransform(GameObject element, Vector2 topCenterInPreviewLayer, HandFanLayoutOptions options)
        {
            LastAppliedTopCenter = topCenterInPreviewLayer;
        }

        public void ClearAllHoverState() => ClearHoverCallCount++;
    }
}
