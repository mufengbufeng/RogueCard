using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    /// <summary>
    /// TargetSelector 单元测试：验证 Enter/MonsterClick/Cancel/Dispose 流程。
    /// 通过 FakeMonsterTargetSurface / FakeHandGhostSurface / FakeTargetContext 模拟跨模块依赖。
    /// </summary>
    [TestFixture]
    public class TargetSelectorTests
    {
        private VisualElement _root;
        private FakeMonsterTargetSurface _monsterList;
        private FakeHandGhostSurface _handFan;
        private FakeTargetContext _ctx;
        private TargetSelector _selector;

        [SetUp]
        public void SetUp()
        {
            _root = new VisualElement();
            _monsterList = new FakeMonsterTargetSurface();
            _handFan = new FakeHandGhostSurface();
            _ctx = new FakeTargetContext();
            _selector = new TargetSelector(_root, _monsterList, _handFan, _ctx);
        }

        [TearDown]
        public void TearDown() => _selector?.Dispose();

        [Test]
        public void Enter后_IsActive为真_且MonsterList进入TargetMode()
        {
            _selector.Enter(2);

            Assert.IsTrue(_selector.IsActive);
            Assert.AreEqual(1, _monsterList.EnterTargetModeCallCount);
            Assert.NotNull(_monsterList.CapturedClickHandler);
        }

        [Test]
        public void 怪物点击_触发UseCardOnMonster_并退出选目标态()
        {
            _selector.Enter(handIdx: 2);
            _monsterList.CapturedClickHandler(1);

            CollectionAssert.AreEqual(new[] { (2, 1) }, _ctx.UseCardOnMonsterLog);
            Assert.IsFalse(_selector.IsActive);
            Assert.AreEqual(1, _monsterList.ExitTargetModeCallCount);
            Assert.AreEqual(1, _handFan.RequestGhostCleanupCallCount, "confirmed 路径 SHALL 调 RequestGhostCleanup");
            Assert.AreEqual(0, _handFan.RequestGhostReboundLog.Count, "confirmed 路径 SHALL NOT 调 RequestGhostRebound");
        }

        [Test]
        public void Cancel_触发GhostRebound_退出MonsterListTargetMode()
        {
            _selector.Enter(handIdx: 3);
            _selector.Cancel();

            Assert.IsFalse(_selector.IsActive);
            Assert.AreEqual(1, _monsterList.ExitTargetModeCallCount);
            CollectionAssert.AreEqual(new[] { 3 }, _handFan.RequestGhostReboundLog,
                "cancelled 路径 SHALL 调 RequestGhostRebound(handIdx)");
            Assert.AreEqual(0, _handFan.RequestGhostCleanupCallCount, "cancelled 路径 SHALL NOT 调 RequestGhostCleanup");
        }

        [Test]
        [Ignore("UI Toolkit 的 KeyDownEvent 派发依赖 Panel，EditMode 无 Panel 时 SendEvent 不会触发 RegisterCallback。" +
                "ESC 取消契约通过手动验证：选目标态按 ESC → ghost 协同回弹、怪物高亮取消、能量未消耗。")]
        public void ESC按键_触发取消()
        {
            // 见 [Ignore] 原因。生产路径：键盘 → Panel.dispatcher → TrickleDown 到 _rootElement → OnKeyDown。
            // EditMode 下无 Panel；同等逻辑由 Cancel() 直接调用的用例覆盖。
        }

        [Test]
        public void Active态Dispose_等价于Cancel()
        {
            _selector.Enter(handIdx: 4);
            _selector.Dispose();

            Assert.IsFalse(_selector.IsActive);
            Assert.AreEqual(1, _monsterList.ExitTargetModeCallCount);
            CollectionAssert.AreEqual(new[] { 4 }, _handFan.RequestGhostReboundLog);
        }

        [Test]
        public void 重复Enter_第二次被忽略()
        {
            _selector.Enter(2);
            _selector.Enter(3);

            Assert.IsTrue(_selector.IsActive);
            Assert.AreEqual(1, _monsterList.EnterTargetModeCallCount, "重复 Enter SHALL NOT 重复调用 EnterTargetMode");
        }

        [Test]
        public void 重复Dispose_安全()
        {
            _selector.Enter(0);
            _selector.Dispose();
            Assert.DoesNotThrow(() => _selector.Dispose());
        }
    }
}
