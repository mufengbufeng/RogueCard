using System.Collections.Generic;
using GameConfig.card;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI CardItemView / HandFanView 行为测试。
    /// </summary>
    [TestFixture]
    public sealed class HandFanViewTests
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
            _root = UguiTestFactory.CreateRectObject("HandRoot");
            _context = new FakeHandContext();
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
        /// 手牌刷新会按 Hand 生成卡牌项，并保留 HandIndex 闭包语义。
        /// </summary>
        [Test]
        public void 刷新手牌_生成卡牌项并应用扇形布局()
        {
            _context.SetHand(new[]
            {
                UguiTestFactory.NewCard(1, "打击", 1, TargetMode.SingleAuto),
                UguiTestFactory.NewCard(2, "防御", 2, TargetMode.Self),
            });

            using var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());

            Assert.AreEqual(2, view.CardItems.Count);
            Assert.AreEqual(0, view.CardItems[0].HandIndex);
            Assert.AreEqual(1, view.CardItems[1].HandIndex);
            Assert.AreNotEqual(Vector2.zero, view.CardItems[0].RectTransform.sizeDelta);
        }

        /// <summary>
        /// Dispose 会清理运行时卡牌项。
        /// </summary>
        [Test]
        public void Dispose_清理卡牌项()
        {
            _context.SetHand(new[] { UguiTestFactory.NewCard(1, "打击", 1, TargetMode.SingleAuto) });
            var view = new HandFanView(_handContainer, _dropZone, _previewLayer, _context, _cardTemplate, new HandFanLayoutOptions());

            view.Dispose();

            Assert.AreEqual(1, _handContainer.childCount, "容器中应只剩手牌模板。");
        }
    }
}
