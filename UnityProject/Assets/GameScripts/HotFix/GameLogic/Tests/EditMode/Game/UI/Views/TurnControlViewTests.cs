using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// TurnControlView 单元测试：验证 endTurnBtn 启用状态按 Phase 切换、点击转发 EndTurn、
    /// fail-toast 中文映射、Dispose 后不再响应。版本号 1.2s 自动隐藏机制依赖 schedule.Execute，
    /// 在 EditMode 不易精确触发，留给手动验证。
    /// </summary>
    [TestFixture]
    public class TurnControlViewTests
    {
        private Button _endTurnBtn;
        private Label _failToast;
        private FakeTurnContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _endTurnBtn = new Button();
            _failToast = new Label();
            _ctx = new FakeTurnContext();
        }

        [Test]
        public void Phase为PlayerTurn_按钮启用()
        {
            _ctx.Phase.Value = BattlePhase.PlayerTurn;
            using var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);

            Assert.IsTrue(_endTurnBtn.enabledSelf);
        }

        [Test]
        public void Phase为MonsterTurn_按钮禁用()
        {
            _ctx.Phase.Value = BattlePhase.MonsterTurn;
            using var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);

            Assert.IsFalse(_endTurnBtn.enabledSelf);
        }

        [Test]
        public void Phase从MonsterTurn变为PlayerTurn_按钮启用()
        {
            _ctx.Phase.Value = BattlePhase.MonsterTurn;
            using var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            Assert.IsFalse(_endTurnBtn.enabledSelf);

            _ctx.Phase.Value = BattlePhase.PlayerTurn;
            Assert.IsTrue(_endTurnBtn.enabledSelf);
        }

        [Test]
        [Ignore("UI Toolkit 的 ClickEvent 派发依赖 Panel，EditMode 无 Panel 时 SendEvent 不会触发 RegisterCallback。" +
                "此契约通过手动验证：进入战斗 → 点击 end-turn-btn → 怪物回合开始。")]
        public void 点击按钮_转发EndTurn调用()
        {
            // 见 [Ignore] 原因。生产路径：Button → Clickable manipulator → PointerUp → ClickEvent → 注册的回调。
            // EditMode 下需要 PanelSettings + 真实 Panel 才能跑通；移到 PlayMode 或手动验证。
        }

        [Test]
        public void CardPlayFailed_InsufficientEnergy_映射为能量不足()
        {
            using var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            _ctx.NotifyCardPlayFailed("InsufficientEnergy");

            Assert.AreEqual("能量不足", _failToast.text);
            Assert.IsTrue(_failToast.ClassListContains("fail-toast--visible"));
        }

        [Test]
        public void CardPlayFailed_NotPlayerTurn_映射为现在不是你的回合()
        {
            using var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            _ctx.NotifyCardPlayFailed("NotPlayerTurn");

            Assert.AreEqual("现在不是你的回合", _failToast.text);
        }

        [Test]
        public void CardPlayFailed_未知Reason_映射为出牌失败()
        {
            using var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            _ctx.NotifyCardPlayFailed("SomeUnknownReason");

            Assert.AreEqual("出牌失败", _failToast.text);
        }

        [Test]
        public void Dispose后_Phase变化不再操作按钮()
        {
            _ctx.Phase.Value = BattlePhase.PlayerTurn;
            var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            Assert.IsTrue(_endTurnBtn.enabledSelf);

            view.Dispose();
            _ctx.Phase.Value = BattlePhase.MonsterTurn;

            // Dispose 后状态不再被改动
            Assert.IsTrue(_endTurnBtn.enabledSelf);
        }

        [Test]
        public void Dispose后_CardPlayFailed不再修改toast()
        {
            var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            view.Dispose();
            _failToast.text = "before";
            _ctx.NotifyCardPlayFailed("InsufficientEnergy");

            Assert.AreEqual("before", _failToast.text);
            Assert.IsFalse(_failToast.ClassListContains("fail-toast--visible"));
        }

        [Test]
        public void 重复Dispose_安全()
        {
            var view = new TurnControlView(_endTurnBtn, _failToast, _ctx);
            view.Dispose();
            Assert.DoesNotThrow(() => view.Dispose());
        }
    }
}
