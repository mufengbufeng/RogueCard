using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// HandFanView preview dismissal tests. The root PointerDown path is registered in production,
    /// while these tests call the same helper directly because EditMode has no stable UI Toolkit Panel dispatch.
    /// </summary>
    [TestFixture]
    public class HandFanViewPreviewDismissalTests
    {
        private HandFanView _view;
        private MockPreviewSurface _surface;
        private CardPreviewController _previewController;

        [SetUp]
        public void SetUp()
        {
            _view = new HandFanView(
                handFan: new VisualElement(),
                dropZone: new VisualElement(),
                previewLayer: new VisualElement(),
                previewDismissRoot: new VisualElement(),
                context: new FakeHandContext(),
                cardItemTemplate: null,
                options: new HandFanLayoutOptions());

            _surface = new MockPreviewSurface();
            _previewController = new CardPreviewController(_surface, new HandFanLayoutOptions());
            SetPrivateField(_view, "_previewController", _previewController);
        }

        [TearDown]
        public void TearDown()
        {
            _view?.Dispose();
            _previewController = null;
            _surface = null;
        }

        [Test]
        public void NonCardPointerTarget_ExitsActivePreview_AndRequestsEventConsumption()
        {
            var source = new VisualElement();
            _previewController.EnterPreview(0, source);
            Assert.IsTrue(_previewController.IsPreviewing);

            bool consumed = TryDismissPreviewFromPointerTarget(_view, new VisualElement());

            Assert.IsTrue(consumed);
            Assert.IsFalse(_previewController.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveFromPreviewLayerCallCount);
        }

        [Test]
        public void NoActivePreview_DoesNotRequestEventConsumption()
        {
            bool consumed = TryDismissPreviewFromPointerTarget(_view, new VisualElement());

            Assert.IsFalse(consumed);
        }

        [Test]
        public void CardRootPointerTarget_IsExcluded_FromBackdropDismissal()
        {
            var cardRoot = AddCardItemRoot();
            _previewController.EnterPreview(0, cardRoot);

            bool consumed = TryDismissPreviewFromPointerTarget(_view, cardRoot);

            Assert.IsFalse(consumed);
            Assert.IsTrue(_previewController.IsPreviewing);
            Assert.AreEqual(0, _surface.RemoveFromPreviewLayerCallCount);
        }

        [Test]
        public void CardDescendantPointerTarget_IsExcluded_FromBackdropDismissal()
        {
            var cardRoot = AddCardItemRoot();
            var label = new Label("Card");
            cardRoot.Add(label);
            _previewController.EnterPreview(0, cardRoot);

            bool consumed = TryDismissPreviewFromPointerTarget(_view, label);

            Assert.IsFalse(consumed);
            Assert.IsTrue(_previewController.IsPreviewing);
            Assert.AreEqual(0, _surface.RemoveFromPreviewLayerCallCount);
        }

        [Test]
        public void PreviewRemainsVisible_WhenPressedCardHasNotCrossedDragThreshold()
        {
            var dragController = CreateDragController();
            SetPrivateField(_view, "_dragController", dragController);
            _previewController.EnterPreview(0, new VisualElement());

            dragController.OnPointerDown(handIdx: 0, visualIdx: 0, pointerId: 1, pos: new Vector2(100, 100));

            ExitPreviewIfDragStarted(_view, CardInteractionState.Idle);

            Assert.AreEqual(CardInteractionState.Idle, dragController.State);
            Assert.IsTrue(_previewController.IsPreviewing);
            Assert.AreEqual(0, _surface.RemoveFromPreviewLayerCallCount);
        }

        [Test]
        public void PreviewExits_WhenPointerMoveTransitionsIntoDragging()
        {
            var dragController = CreateDragController();
            SetPrivateField(_view, "_dragController", dragController);
            _previewController.EnterPreview(0, new VisualElement());

            dragController.OnPointerDown(handIdx: 0, visualIdx: 0, pointerId: 1, pos: new Vector2(100, 100));
            dragController.OnPointerMove(pointerId: 1, pos: new Vector2(120, 100));

            ExitPreviewIfDragStarted(_view, CardInteractionState.Idle);

            Assert.AreEqual(CardInteractionState.Dragging, dragController.State);
            Assert.IsFalse(_previewController.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveFromPreviewLayerCallCount);
        }

        [Test]
        public void PreviewRemainsToggleControlled_WhenCardGestureStaysWithinDragThreshold()
        {
            var source = AddCardItemRoot();
            _previewController.EnterPreview(0, source);
            Assert.IsTrue(_previewController.IsPreviewing);

            ExitPreviewIfDragStarted(_view, CardInteractionState.Idle);
            InvokeCardClickedCallback(_view, handIdx: 0);

            Assert.IsFalse(_previewController.IsPreviewing);
            Assert.AreEqual(1, _surface.RemoveFromPreviewLayerCallCount);
        }

        private VisualElement AddCardItemRoot()
        {
            var cardRoot = new VisualElement();
            cardRoot.AddToClassList("card-item");
            var item = new CardItemView(cardRoot, handIndex: 0, card: new CardRuntime());
            GetCardItems(_view).Add(item);
            return cardRoot;
        }

        private static CardDragController CreateDragController()
        {
            var surface = new MockDragSurface(new CapturingDragHostCallbacks())
            {
                ConfiguredCardCount = 1,
                ConfiguredHandFanWidth = 800f,
                ConfiguredHandFanHeight = 280f,
            };
            var ctx = new FakeHandContext();
            ctx.Phase.Value = BattlePhase.PlayerTurn;
            return new CardDragController(surface, ctx, new HandFanLayoutOptions { DragThreshold = 10f });
        }

        private static List<CardItemView> GetCardItems(HandFanView view)
        {
            var field = typeof(HandFanView).GetField("_cardItems", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (List<CardItemView>)field.GetValue(view);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        private static bool TryDismissPreviewFromPointerTarget(HandFanView view, VisualElement target)
        {
            var method = typeof(HandFanView).GetMethod(
                "TryDismissPreviewFromPointerTarget",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (bool)method.Invoke(view, new object[] { target });
        }

        private static void ExitPreviewIfDragStarted(HandFanView view, CardInteractionState previousState)
        {
            var method = typeof(HandFanView).GetMethod(
                "ExitPreviewIfDragStarted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(view, new object[] { previousState });
        }

        private static void InvokeCardClickedCallback(HandFanView view, int handIdx)
        {
            var field = typeof(HandFanView).GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var callbacks = field.GetValue(view);
            Assert.NotNull(callbacks);

            var method = callbacks.GetType().GetMethod("CardClicked", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(callbacks, new object[] { handIdx });
        }
    }
}
