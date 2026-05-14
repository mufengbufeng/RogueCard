using System;
using System.Collections.Generic;
using System.Reflection;
using EF.UI;
using GameConfig.card;
using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// CardDragController 状态机单元测试。通过 MockDragSurface 记录 UI 调用序列与 CapturingDragHostCallbacks
    /// 记录三类回调，验证 PointerDown / Move / Up 序列在不同子态下的行为契约。
    /// 不依赖 Unity PlayerLoop / VisualElement。
    /// </summary>
    [TestFixture]
    public class CardDragControllerTests
    {
        private FakeHandContext _ctx;
        private CapturingDragHostCallbacks _callbacks;
        private MockDragSurface _surface;
        private HandFanLayoutOptions _options;
        private CardDragController _controller;

        [SetUp]
        public void SetUp()
        {
            _ctx = new FakeHandContext();
            _ctx.Phase.Value = BattlePhase.PlayerTurn;
            _callbacks = new CapturingDragHostCallbacks();
            _surface = new MockDragSurface(_callbacks)
            {
                ConfiguredCardCount = 3,
                CardBounds = new[]
                {
                    new Rect(50, 400, 100, 200),
                    new Rect(200, 400, 100, 200),
                    new Rect(350, 400, 100, 200),
                },
                ConfiguredHandFanBound = new Rect(0, 350, 800, 250),
                ConfiguredDropZoneBound = new Rect(300, 100, 200, 200),
                ConfiguredDropZoneAvailable = true,
                ConfiguredHandFanWidth = 800f,
                ConfiguredHandFanHeight = 280f,
            };
            _options = new HandFanLayoutOptions { DragThreshold = 10f, ReboundDurationMs = 160 };
            _controller = new CardDragController(_surface, _ctx, _options);
        }

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
        }

        [Test]
        public void PointerDown_ThenSmallMove_ThenUp_TriggersCardClicked_NotEnterDragging()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleAuto) });

            _controller.OnPointerDown(handIdx: 1, visualIdx: 1, pointerId: 0, pos: new Vector2(250, 500));
            _controller.OnPointerMove(0, new Vector2(252, 502)); // 位移 ~2.8，未达 threshold
            _controller.OnPointerUp(0, new Vector2(252, 502));

            CollectionAssert.AreEqual(new[] { 1 }, _callbacks.CardClickedLog);
            Assert.AreEqual(0, _callbacks.CardDroppedOnZoneLog.Count);
            Assert.AreEqual(0, _callbacks.CardDragCancelledLog.Count);
            Assert.AreEqual(CardInteractionState.Idle, _controller.State);
        }

        [Test]
        public void PointerDown_ThenLargeMove_EntersDragging_CreatesGhost_SetsOpacityZero()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleAuto) });

            _controller.OnPointerDown(1, 1, 0, new Vector2(250, 500));
            _controller.OnPointerMove(0, new Vector2(270, 500)); // 位移 20 > threshold 10

            Assert.AreEqual(CardInteractionState.Dragging, _controller.State);
            Assert.AreEqual(1, _surface.CreateGhostCallLog.Count);
            Assert.AreEqual((1, new Vector2(270, 500)), _surface.CreateGhostCallLog[0]);
            // 被拖卡 opacity 0
            Assert.IsTrue(_surface.OpacityCallLog.Contains((1, 0f)),
                "EnterDragging SHALL set active card opacity=0");
            Assert.IsTrue(_surface.PickingModeCallLog.Contains((1, false)),
                "EnterDragging SHALL set active card pickingMode=Ignore");
        }

        [Test]
        public void Dragging_DroppedOnDropZone_AutoTarget_TriggersCallback_FalseFlag()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;
            _surface.CardBounds = new[] { new Rect(50, 400, 100, 200), new Rect(200, 400, 100, 200) };

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500));
            // 拖到 drop-zone 内（300-500, 100-300）
            _controller.OnPointerMove(0, new Vector2(400, 200));
            _controller.OnPointerUp(0, new Vector2(400, 200));

            CollectionAssert.AreEqual(new[] { (0, false) }, _callbacks.CardDroppedOnZoneLog);
            Assert.GreaterOrEqual(_surface.DestroyGhostCallCount, 1, "ghost SHALL be destroyed for AutoTarget");
            Assert.AreEqual(CardInteractionState.Idle, _controller.State);
        }

        [Test]
        public void Dragging_DroppedOnDropZone_SingleManual_TriggersCallback_TrueFlag_KeepsGhost()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleManual, CardReleaseKind.Melee), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;
            _surface.CardBounds = new[] { new Rect(50, 400, 100, 200), new Rect(200, 400, 100, 200) };

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500));
            _controller.OnPointerMove(0, new Vector2(400, 200));
            _controller.OnPointerUp(0, new Vector2(400, 200));

            CollectionAssert.AreEqual(new[] { (0, true) }, _callbacks.CardDroppedOnZoneLog);
            // SingleManual SHALL NOT 在 controller 内销毁 ghost（保留给 TargetSelector）
            Assert.AreEqual(0, _surface.DestroyGhostCallCount, "ghost SHALL be retained for SingleManual");
        }

        [Test]
        public void Dragging_DroppedOnDropZone_SpellSingleManual_自动释放不进入手选()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleManual, CardReleaseKind.Spell), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;
            _surface.CardBounds = new[] { new Rect(50, 400, 100, 200), new Rect(200, 400, 100, 200) };

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500));
            _controller.OnPointerMove(0, new Vector2(400, 200));
            _controller.OnPointerUp(0, new Vector2(400, 200));

            CollectionAssert.AreEqual(new[] { (0, false) }, _callbacks.CardDroppedOnZoneLog);
            Assert.GreaterOrEqual(_surface.DestroyGhostCallCount, 1);
        }

        [Test]
        public void Dragging_InsertSlotRelease_TriggersReorderCardItem()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleAuto) });

            _controller.OnPointerDown(2, 2, 0, new Vector2(400, 500));
            _controller.OnPointerMove(0, new Vector2(420, 500));   // EnterDragging
            // 移到 hand-fan 内（y=500 在 350-600 范围）但远离 drop-zone(x>300, y<300)
            _controller.OnPointerMove(0, new Vector2(60, 500));   // hand-fan 内最左卡附近
            _controller.OnPointerUp(0, new Vector2(60, 500));

            Assert.AreEqual(1, _surface.ReorderCallLog.Count);
            Assert.AreEqual(2, _surface.ReorderCallLog[0].from); // _activeVisualIndex 起点
            Assert.AreEqual(CardInteractionState.Idle, _controller.State);
        }

        [Test]
        public void Dragging_InsertSlotRelease_AppliesFinalNCardLayoutAfterReorder()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleAuto) });

            _controller.OnPointerDown(2, 2, 0, new Vector2(400, 500));
            _controller.OnPointerMove(0, new Vector2(420, 500));
            _controller.OnPointerMove(0, new Vector2(60, 500));
            _controller.OnPointerUp(0, new Vector2(60, 500));

            var finalTransforms = _surface.GetApplyFanTransformsAfter("ReorderCardItem:2:0");
            Assert.AreEqual(3, finalTransforms.Count,
                "InsertSlot commit SHALL apply final N-card layout after visual reorder.");

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, finalTransforms[i].cardIdx);
                AssertSlotEquals(
                    FanLayoutCalc.ComputeSlot(i, 3, _surface.ConfiguredHandFanWidth, _surface.ConfiguredHandFanHeight, _options),
                    finalTransforms[i].slot);
            }
        }

        [Test]
        public void Dragging_InsertSlotRelease_SameSlotStillAppliesFinalNCardLayout()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleAuto) });

            _controller.OnPointerDown(1, 1, 0, new Vector2(250, 500));
            _controller.OnPointerMove(0, new Vector2(270, 500));
            _controller.OnPointerMove(0, new Vector2(240, 500));
            _controller.OnPointerUp(0, new Vector2(240, 500));

            var finalTransforms = _surface.GetApplyFanTransformsAfter("ReorderCardItem:1:1");
            Assert.AreEqual(3, finalTransforms.Count,
                "Dropping back to the original slot SHALL still restore final N-card layout.");
            Assert.AreEqual(CardInteractionState.Idle, _controller.State);
        }

        [Test]
        public void Dragging_InsertSlotRelease_ReleasesPointerBeforeReorder()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleAuto) });

            _controller.OnPointerDown(2, 2, 0, new Vector2(400, 500));
            _controller.OnPointerMove(0, new Vector2(420, 500));
            _controller.OnPointerMove(0, new Vector2(60, 500));
            _controller.OnPointerUp(0, new Vector2(60, 500));

            int releaseIndex = _surface.IndexOfOperation("ReleasePointer:2:0");
            int reorderIndex = _surface.IndexOfOperation("ReorderCardItem:2:0");
            Assert.GreaterOrEqual(releaseIndex, 0, "Dragged pre-reorder visual card SHALL release pointer capture.");
            Assert.GreaterOrEqual(reorderIndex, 0, "InsertSlot release SHALL reorder the visual list.");
            Assert.Less(releaseIndex, reorderIndex,
                "Pointer capture SHALL be released before visual list reorder invalidates the old visual index.");
        }

        [Test]
        public void Dragging_DetachedRelease_StartsRebound_ScheduledExitAfterReboundDuration()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500));
            // 移到 detached 区域（hand-fan 上方、drop-zone 之外）
            _controller.OnPointerMove(0, new Vector2(50, 50));
            _controller.OnPointerUp(0, new Vector2(50, 50));

            Assert.GreaterOrEqual(_surface.DestroyGhostCallCount, 1, "rebound 立即销毁 ghost");
            CollectionAssert.AreEqual(new[] { 0 }, _callbacks.CardDragCancelledLog);
            Assert.IsTrue(_surface.ScheduledActions.Count > 0,
                "rebound SHALL schedule ExitDragging after ReboundDurationMs");
            // 触发 schedule → ExitDragging 完成
            _surface.FlushScheduled();
            Assert.AreEqual(CardInteractionState.Idle, _controller.State);
        }

        [Test]
        public void PointerCaptureOut_DuringDragging_ResetsState_DestroysGhost_TriggersCancelled()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500)); // EnterDragging
            _controller.OnPointerCaptureOut(0);

            Assert.AreEqual(CardInteractionState.Idle, _controller.State);
            Assert.GreaterOrEqual(_surface.DestroyGhostCallCount, 1);
            CollectionAssert.AreEqual(new[] { 0 }, _callbacks.CardDragCancelledLog);
        }

        [Test]
        public void HandIndex_RemainsStable_AfterReorderDuringDrag()
        {
            // SingleManual 卡在位置 2，拖拽中 visual reorder 可能改 _activeVisualIndex；
            // _activeHandIndex 保持初始 hand 位置（用于 UseCard）。
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto), NewCard(3, TargetMode.SingleManual, CardReleaseKind.Melee) });
            _surface.ConfiguredCardCount = 3;
            _surface.CardBounds = new[]
            {
                new Rect(50, 400, 100, 200),
                new Rect(200, 400, 100, 200),
                new Rect(350, 400, 100, 200),
            };

            _controller.OnPointerDown(handIdx: 2, visualIdx: 2, pointerId: 0, pos: new Vector2(400, 500));
            _controller.OnPointerMove(0, new Vector2(420, 500)); // EnterDragging
            // 拖到 drop-zone 内
            _controller.OnPointerMove(0, new Vector2(400, 200));
            _controller.OnPointerUp(0, new Vector2(400, 200));

            // CardDroppedOnZone(handIdx=2, needsManualTarget=true)
            CollectionAssert.AreEqual(new[] { (2, true) }, _callbacks.CardDroppedOnZoneLog);
        }

        [Test]
        public void EnterDragging_UsesOpacityZero_NotVisibility()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500));

            // 必须有 SetCardOpacity(0, 0) 调用。MockDragSurface 没有 visibility log（未提供 API），
            // 通过 IDragSurface 接口本身就缺 visibility 即可证明合规。
            Assert.IsTrue(_surface.OpacityCallLog.Contains((0, 0f)));
        }

        [Test]
        public void EnterDragging_UsesInlineTransitionDuration_NotUssClass()
        {
            _ctx.SetHand(new[] { NewCard(1, TargetMode.SingleAuto), NewCard(2, TargetMode.SingleAuto) });
            _surface.ConfiguredCardCount = 2;

            _controller.OnPointerDown(0, 0, 0, new Vector2(100, 500));
            _controller.OnPointerMove(0, new Vector2(120, 500));

            // 所有卡都被设置 transitionDuration=0
            Assert.IsTrue(_surface.TransitionDurationCallLog.Contains((0, 0f)));
            Assert.IsTrue(_surface.TransitionDurationCallLog.Contains((1, 0f)));
            // IDragSurface 没有 AddToClassList/RemoveFromClassList 接口，进一步保证不可能用 USS 类切换
        }

        // ── 辅助 ──

        private static Card NewCard(int id, TargetMode mode, CardReleaseKind releaseKind = CardReleaseKind.Spell)
        {
            var card = (Card)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Card));
            SetField(card, "Id", id);
            SetField(card, "Name", $"Card_{id}");
            SetField(card, "Desc", string.Empty);
            SetField(card, "Cost", 1);
            SetField(card, "OwnerKind", OwnerKind.Player);
            SetField(card, "CardReleaseKind", releaseKind);
            SetField(card, "TargetMode", mode);
            SetField(card, "TargetCount", 1);
            SetField(card, "IsBasic", true);
            SetField(card, "AssetId", string.Empty);
            return card;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"未找到字段 {fieldName}");
            field.SetValue(target, value);
        }

        private static void AssertSlotEquals(FanSlotAssignment expected, FanSlotAssignment actual)
        {
            Assert.AreEqual(expected.Left, actual.Left, 0.001f);
            Assert.AreEqual(expected.Top, actual.Top, 0.001f);
            Assert.AreEqual(expected.TranslateY, actual.TranslateY, 0.001f);
            Assert.AreEqual(expected.RotateDegrees, actual.RotateDegrees, 0.001f);
        }
    }

    /// <summary>测试用 IHandContext 实现。</summary>
    public class FakeHandContext : IHandContext
    {
        public ReactiveProperty<IReadOnlyList<CardRuntime>> Hand { get; } =
            new ReactiveProperty<IReadOnlyList<CardRuntime>>(Array.Empty<CardRuntime>());
        public ReactiveProperty<BattlePhase> Phase { get; } = new ReactiveProperty<BattlePhase>(BattlePhase.PlayerTurn);
        public event Action<string> CardPlayFailed;

        public readonly List<(int handIdx, int targetIdx)> UseCardLog = new();
        public void UseCard(int handIdx, int targetIdx = -1) => UseCardLog.Add((handIdx, targetIdx));

        public void NotifyCardPlayFailed(string reason) => CardPlayFailed?.Invoke(reason);

        public void SetHand(IEnumerable<Card> cards)
        {
            var list = new List<CardRuntime>();
            foreach (var c in cards) list.Add(new CardRuntime { Config = c });
            Hand.Value = list;
        }
    }
}
