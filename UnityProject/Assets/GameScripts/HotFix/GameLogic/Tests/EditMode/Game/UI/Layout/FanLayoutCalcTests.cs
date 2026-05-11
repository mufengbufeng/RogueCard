using NUnit.Framework;
using UnityEngine;

namespace GameLogic.Tests
{
    /// <summary>
    /// FanLayoutCalc 单元测试：纯函数验证扇形布局公式与 ComputeInsertSlot 命中规则。
    /// 不依赖 Unity PlayerLoop / VisualElement，可在 EditMode 即时运行。
    /// </summary>
    [TestFixture]
    public class FanLayoutCalcTests
    {
        // ── ComputeSlot ──

        [Test]
        public void ComputeSlot_FiveCards_CenterCardAtZeroOffset()
        {
            var opts = new HandFanLayoutOptions();
            var slot = FanLayoutCalc.ComputeSlot(2, 5, 800f, 280f, opts);

            Assert.AreEqual(0f, slot.RotateDegrees, 0.001f);
            Assert.AreEqual(0f, slot.TranslateY, 0.001f);
        }

        [Test]
        public void ComputeSlot_FiveCards_LeftAndRightSymmetric()
        {
            var opts = new HandFanLayoutOptions();
            var leftEdge = FanLayoutCalc.ComputeSlot(0, 5, 800f, 280f, opts);
            var rightEdge = FanLayoutCalc.ComputeSlot(4, 5, 800f, 280f, opts);

            Assert.AreEqual(leftEdge.RotateDegrees, -rightEdge.RotateDegrees, 0.001f);
            Assert.AreEqual(leftEdge.TranslateY, rightEdge.TranslateY, 0.001f);
        }

        [Test]
        public void ComputeSlot_LargeFanWidth_SpacingClampedToMaxCardSpacing()
        {
            var opts = new HandFanLayoutOptions { MaxCardSpacing = 120f, CardWidth = 150f };
            // fanWidth=2000, slotCount=2 → raw spacing = (2000-150)/1 = 1850, but clamped to 120
            var slot0 = FanLayoutCalc.ComputeSlot(0, 2, 2000f, 280f, opts);
            var slot1 = FanLayoutCalc.ComputeSlot(1, 2, 2000f, 280f, opts);

            // |slot1.Left - slot0.Left| 应为 spacing = 120
            Assert.AreEqual(120f, slot1.Left - slot0.Left, 0.001f);
        }

        [Test]
        public void ComputeSlot_SingleCard_SpacingZero_LeftCentered()
        {
            var opts = new HandFanLayoutOptions { CardWidth = 150f };
            var slot = FanLayoutCalc.ComputeSlot(0, 1, 800f, 280f, opts);

            // slotCount=1: spacing=0, offset=0, left=400-75=325
            Assert.AreEqual(325f, slot.Left, 0.001f);
            Assert.AreEqual(0f, slot.RotateDegrees, 0.001f);
        }

        [Test]
        public void ComputeSlot_FanHeightLessThanCardHeight_TopClampedToZero()
        {
            var opts = new HandFanLayoutOptions { CardHeight = 230f, HandFanBottomPadding = 20f };
            var slot = FanLayoutCalc.ComputeSlot(0, 3, 800f, 100f, opts);

            // 100 - 230 - 20 = -150 → max(0, -150) = 0
            Assert.AreEqual(0f, slot.Top, 0.001f);
        }

        [Test]
        public void ComputeSlot_NullOptions_UsesDefaults()
        {
            // 不传 options 时退化为默认值（不抛异常）
            var slot = FanLayoutCalc.ComputeSlot(2, 5, 800f, 280f, null);
            Assert.AreEqual(0f, slot.RotateDegrees, 0.001f);
        }

        // ── ComputeInsertSlot ──

        [Test]
        public void ComputeInsertSlot_PointerInLeftHalfOfNearest_InsertsBefore()
        {
            // 三张其它卡（不含被拖卡），中心 x = 125, 225, 325
            var bounds = new[]
            {
                new Rect(100, 0, 50, 100),
                new Rect(200, 0, 50, 100),
                new Rect(300, 0, 50, 100),
            };
            var visualIndices = new[] { 0, 1, 2 };
            int slot = FanLayoutCalc.ComputeInsertSlot(new Vector2(220f, 50f), bounds, visualIndices);

            // pointer 220 → 距 225 最近，左半 → 槽 1
            Assert.AreEqual(1, slot);
        }

        [Test]
        public void ComputeInsertSlot_PointerInRightHalfOfNearest_InsertsAfter()
        {
            var bounds = new[]
            {
                new Rect(100, 0, 50, 100),
                new Rect(200, 0, 50, 100),
                new Rect(300, 0, 50, 100),
            };
            var visualIndices = new[] { 0, 1, 2 };
            int slot = FanLayoutCalc.ComputeInsertSlot(new Vector2(240f, 50f), bounds, visualIndices);

            // pointer 240 → 距 225 最近，右半 → 槽 2
            Assert.AreEqual(2, slot);
        }

        [Test]
        public void ComputeInsertSlot_SingleCard_ReturnsZero()
        {
            // N=1（被拖卡是唯一卡，otherBounds 为空）→ 返回 0
            int slot = FanLayoutCalc.ComputeInsertSlot(new Vector2(500f, 50f), System.Array.Empty<Rect>(), null);
            Assert.AreEqual(0, slot);
        }

        [Test]
        public void ComputeInsertSlot_PointerFarRight_ClampedToNMinus1()
        {
            // N=3，pointer 在最右卡右半 → 计算结果 = bestVisualIdx + 1 = 2 + 1 = 3 → Clamp 到 2
            var bounds = new[]
            {
                new Rect(100, 0, 50, 100),
                new Rect(200, 0, 50, 100),
            };
            var visualIndices = new[] { 0, 1 };
            int slot = FanLayoutCalc.ComputeInsertSlot(new Vector2(500f, 50f), bounds, visualIndices);

            Assert.AreEqual(2, slot); // Clamp(N-1=2)
        }

        [Test]
        public void ComputeInsertSlot_NullVisualIndices_FallsBackToBoundsIndex()
        {
            var bounds = new[]
            {
                new Rect(100, 0, 50, 100),
                new Rect(300, 0, 50, 100),
            };
            int slot = FanLayoutCalc.ComputeInsertSlot(new Vector2(120f, 50f), bounds, null);

            // 距 125（bounds[0].center.x）最近，左半 → 槽 0
            Assert.AreEqual(0, slot);
        }
    }
}
