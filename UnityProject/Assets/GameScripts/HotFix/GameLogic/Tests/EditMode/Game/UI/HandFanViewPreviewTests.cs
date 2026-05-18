using System.Reflection;
using GameConfig.card;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI HandFanView 预览关闭行为测试。
    /// </summary>
    [TestFixture]
    public sealed class HandFanViewPreviewTests
    {
        private GameObject _root;
        private FakeHandContext _context;
        private RectTransform _handContainer;
        private RectTransform _dropZone;
        private RectTransform _previewLayer;
        private GameObject _cardTemplate;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("HandPreviewRoot");
            _context = new FakeHandContext();
            _context.SetHand(new[]
            {
                UguiTestFactory.NewCard(1, "打击", 1, TargetMode.SingleAuto),
                UguiTestFactory.NewCard(2, "防御", 1, TargetMode.Self),
            });
            _handContainer = UguiTestFactory.CreateRectObject("CardSc", _root.transform).GetComponent<RectTransform>();
            _handContainer.sizeDelta = new Vector2(800f, 280f);
            _dropZone = UguiTestFactory.CreateRectObject("DropZone", _root.transform).GetComponent<RectTransform>();
            _previewLayer = UguiTestFactory.CreateRectObject("PreviewLayer", _root.transform).GetComponent<RectTransform>();
            _cardTemplate = UguiTestFactory.CreateCardTemplate("HandCardTemplate", _handContainer);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 卡牌点击由 HandFanView 内部转成预览切换。
        /// </summary>
        [Test]
        public void 卡牌点击_切换预览()
        {
            using var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());

            InvokeCardClicked(view, 0);
            Assert.IsTrue(view.IsPreviewing);

            InvokeCardClicked(view, 0);
            Assert.IsFalse(view.IsPreviewing);
        }

        /// <summary>
        /// 预览层必须克隆卡牌文本，并关闭所有射线命中。
        /// </summary>
        [Test]
        public void 卡牌点击_创建不可点击预览克隆()
        {
            using var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());

            InvokeCardClicked(view, 0);

            Transform preview = _previewLayer.Find("CardPreview");
            Assert.NotNull(preview, "预览层必须创建 CardPreview 克隆。");
            Assert.AreEqual("打击", preview.Find("CardNameText").GetComponent<TMPro.TextMeshProUGUI>().text);
            Assert.AreEqual("1", preview.Find("CardCostText").GetComponent<TMPro.TextMeshProUGUI>().text);
            foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget, $"{graphic.name} 不应拦截点击。");
            }
        }

        /// <summary>
        /// 点击非卡区域会关闭预览并返回已消费。
        /// </summary>
        [Test]
        public void 非卡区域_关闭预览并消费()
        {
            using var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());
            InvokeCardClicked(view, 0);

            bool consumed = view.TryDismissPreviewFromPointerTarget(_dropZone.gameObject);

            Assert.IsTrue(consumed);
            Assert.IsFalse(view.IsPreviewing);
        }

        /// <summary>
        /// 卡牌自身不会走非卡关闭，仍由 toggle 语义处理。
        /// </summary>
        [Test]
        public void 卡牌区域_不触发非卡关闭()
        {
            using var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());
            InvokeCardClicked(view, 0);

            bool consumed = view.TryDismissPreviewFromPointerTarget(view.CardItems[0].Root);

            Assert.IsFalse(consumed);
            Assert.IsTrue(view.IsPreviewing);
        }

        /// <summary>
        /// 拖拽开始时关闭当前预览。
        /// </summary>
        [Test]
        public void 拖拽开始_关闭预览()
        {
            using var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());
            InvokeCardClicked(view, 0);

            SetDragControllerState(view, CardInteractionState.Dragging);
            view.ExitPreviewIfDragStarted(CardInteractionState.Idle);

            Assert.IsFalse(view.IsPreviewing);
        }

        private static void InvokeCardClicked(HandFanView view, int handIdx)
        {
            object callbacks = typeof(HandFanView).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            callbacks.GetType().GetMethod("CardClicked").Invoke(callbacks, new object[] { handIdx });
        }

        private static void SetDragControllerState(HandFanView view, CardInteractionState state)
        {
            object controller = typeof(HandFanView).GetField("_dragController", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            typeof(CardDragController).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, state);
        }
    }
}
