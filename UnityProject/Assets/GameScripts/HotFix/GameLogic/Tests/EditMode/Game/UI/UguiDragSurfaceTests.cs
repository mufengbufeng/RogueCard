using System;
using System.Collections.Generic;
using GameConfig.card;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI IDragSurface 适配层测试。
    /// </summary>
    [TestFixture]
    public sealed class UguiDragSurfaceTests
    {
        private GameObject _root;
        private RectTransform _handFan;
        private RectTransform _dropZone;
        private RectTransform _previewLayer;
        private List<CardItemView> _cards;
        private List<(Action action, long delayMs)> _scheduled;

        [SetUp]
        public void SetUp()
        {
            _root = UguiTestFactory.CreateRectObject("DragSurfaceRoot");
            _handFan = UguiTestFactory.CreateRectObject("CardSc", _root.transform).GetComponent<RectTransform>();
            _handFan.sizeDelta = new Vector2(800f, 280f);
            _dropZone = UguiTestFactory.CreateRectObject("DropZone", _root.transform).GetComponent<RectTransform>();
            _dropZone.sizeDelta = new Vector2(300f, 100f);
            _dropZone.gameObject.AddComponent<Image>();
            _previewLayer = UguiTestFactory.CreateRectObject("PreviewLayer", _root.transform).GetComponent<RectTransform>();
            _cards = new List<CardItemView>
            {
                new CardItemView(UguiTestFactory.CreateCardTemplate("Card0", _handFan), 0, new CardRuntime { Config = UguiTestFactory.NewCard(1, "打击", 1, TargetMode.SingleAuto) }),
                new CardItemView(UguiTestFactory.CreateCardTemplate("Card1", _handFan), 1, new CardRuntime { Config = UguiTestFactory.NewCard(2, "防御", 1, TargetMode.Self) }),
            };
            _scheduled = new List<(Action action, long delayMs)>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// 适配层暴露 hand-fan / drop-zone 几何命中矩形。
        /// </summary>
        [Test]
        public void 几何命中_返回非空世界矩形()
        {
            var surface = CreateSurface();

            Assert.Greater(surface.HandFanWorldBound.width, 0f);
            Assert.Greater(surface.DropZoneWorldBound.width, 0f);
            Assert.IsTrue(surface.DropZoneAvailable);
        }

        /// <summary>
        /// opacity / picking 会映射到 CanvasGroup 和 Graphic raycast。
        /// </summary>
        [Test]
        public void 透明度和点击状态_映射到UGUI组件()
        {
            var surface = CreateSurface();

            surface.SetCardOpacity(0, 0f);
            surface.SetCardPickingMode(0, false);

            Assert.AreEqual(0f, _cards[0].Root.GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.IsFalse(_cards[0].Root.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.IsFalse(_cards[0].Root.GetComponentInChildren<Graphic>(true).raycastTarget);
        }

        /// <summary>
        /// ghost 和 insert slot 会克隆并可销毁。
        /// </summary>
        [Test]
        public void Ghost和占位卡_创建销毁()
        {
            var surface = CreateSurface();

            surface.CreateGhost(0, Vector2.zero);
            surface.CreateInsertSlot(1);

            Assert.IsNotNull(_previewLayer.Find("CardGhost"));
            Assert.IsNotNull(_handFan.Find("InsertSlot"));

            surface.DestroyGhost();
            surface.DestroyInsertSlot();

            Assert.IsNull(_previewLayer.Find("CardGhost"));
            Assert.IsNull(_handFan.Find("InsertSlot"));
        }

        /// <summary>
        /// 延迟调度会记录指定时长。
        /// </summary>
        [Test]
        public void Schedule_使用指定延迟()
        {
            var surface = CreateSurface();

            surface.Schedule(() => { }, 150);

            Assert.AreEqual(1, _scheduled.Count);
            Assert.AreEqual(150, _scheduled[0].delayMs);
        }

        private UguiDragSurface CreateSurface()
        {
            return new UguiDragSurface(
                _handFan,
                _dropZone,
                _previewLayer,
                _cards,
                new HandFanLayoutOptions(),
                new CapturingDragHostCallbacks(),
                (action, delayMs) => _scheduled.Add((action, delayMs)));
        }
    }
}
