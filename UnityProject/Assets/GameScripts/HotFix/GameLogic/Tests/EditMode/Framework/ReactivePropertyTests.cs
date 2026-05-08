using System;
using System.Collections.Generic;
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

        // ── ViewModelBase 测试 ──

        [Test]
        public void Prop创建并追踪属性()
        {
            var vm = new TestViewModel();
            Assert.AreEqual("初始文本", vm.StatusText.Value);
            Assert.AreEqual(true, vm.CanStart.Value);
        }

        [Test]
        public void Dispose清理所有追踪属性的监听者()
        {
            var vm = new TestViewModel();
            int statusCallCount = 0;
            int canStartCallCount = 0;
            vm.StatusText.Changed += _ => statusCallCount++;
            vm.CanStart.Changed += _ => canStartCallCount++;

            vm.Dispose();

            vm.StatusText.Value = "新文本";
            vm.CanStart.Value = false;

            Assert.AreEqual(0, statusCallCount);
            Assert.AreEqual(0, canStartCallCount);
        }

        [Test]
        public void Dispose是幂等的()
        {
            var vm = new TestViewModel();
            vm.StatusText.Changed += _ => { };

            vm.Dispose();
            vm.Dispose(); // 不应抛异常

            // 属性仍可访问，只是监听者被清理
            vm.StatusText.Value = "安全";
            Assert.AreEqual("安全", vm.StatusText.Value);
        }

        [Test]
        public void Dispose后Prop仍可读写()
        {
            var vm = new TestViewModel();
            vm.Dispose();

            vm.StatusText.Value = "写入安全";
            Assert.AreEqual("写入安全", vm.StatusText.Value);
        }

        /// <summary>
        /// 测试用 ViewModel 子类。
        /// </summary>
        private class TestViewModel : ViewModelBase
        {
            public ReactiveProperty<string> StatusText { get; private set; }
            public ReactiveProperty<bool> CanStart { get; private set; }

            public TestViewModel()
            {
                StatusText = Prop<string>("初始文本");
                CanStart = Prop(true);
            }
        }
    }
}
