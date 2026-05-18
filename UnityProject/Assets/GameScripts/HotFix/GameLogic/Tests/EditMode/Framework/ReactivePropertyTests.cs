using EF.UI;
using NUnit.Framework;

namespace GameLogic.Tests
{
    [TestFixture]
    public class ReactivePropertyTests
    {
        // ── ReactiveProperty 测试 ──

        [Test]
        public void Value_变化时触发Changed()
        {
            var prop = new ReactiveProperty<int>(5);
            int received = 0;
            prop.Changed += v => received = v;

            prop.Value = 10;

            Assert.AreEqual(10, received);
            Assert.AreEqual(10, prop.Value);
        }

        [Test]
        public void Value_未变化时不触发Changed()
        {
            var prop = new ReactiveProperty<int>(5);
            int callCount = 0;
            prop.Changed += _ => callCount++;

            prop.Value = 5;

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void 构造函数设置初始值()
        {
            var prop = new ReactiveProperty<string>("hello");
            Assert.AreEqual("hello", prop.Value);
        }

        [Test]
        public void ClearListeners后不再触发回调()
        {
            var prop = new ReactiveProperty<int>(5);
            int callCount = 0;
            prop.Changed += _ => callCount++;

            prop.ClearListeners();
            prop.Value = 10;

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void 默认初始值为default()
        {
            var prop = new ReactiveProperty<int>();
            Assert.AreEqual(0, prop.Value);
        }

    }
}
