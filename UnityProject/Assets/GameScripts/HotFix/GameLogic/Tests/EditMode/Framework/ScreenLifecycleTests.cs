using System;
using EF.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using EFScreen = EF.UI.Screen;

namespace GameLogic.Tests
{
    /// <summary>
    /// Screen 生命周期测试，覆盖非泛型基类与泛型派生协作路径。
    /// </summary>
    [TestFixture]
    public class ScreenLifecycleTests
    {
        /// <summary>
        /// 验证 Activator.CreateInstance + 非泛型 Screen 引用 + Setup(ViewModelBase) 链路可工作，
        /// 这是 Navigator 的真实路径，避免泛型协变导致的 InvalidCastException。
        /// </summary>
        [Test]
        public void Screen_可通过非泛型引用驱动完整生命周期()
        {
            var instance = Activator.CreateInstance(typeof(TestScreen));
            Assert.IsInstanceOf<EFScreen>(instance, "TestScreen 必须可以转为非泛型 Screen 基类");

            EFScreen screen = (EFScreen)instance;
            var vta = ScriptableObject.CreateInstance<VisualTreeAsset>();
            screen.LoadContent(vta);

            var vm = new TestViewModel();
            screen.Setup(vm);

            screen.OnShow();
            screen.OnHide();
            screen.OnDispose();

            var typedScreen = (TestScreen)screen;
            Assert.IsTrue(typedScreen.SetupCalled, "OnSetup 必须被触发");
            Assert.IsTrue(typedScreen.ShowCalled, "OnShow 必须被触发");
            Assert.IsTrue(typedScreen.HideCalled, "OnHide 必须被触发");
            Assert.IsTrue(typedScreen.DisposeCalled, "OnDispose 必须被触发");
        }

        /// <summary>
        /// Setup 注入的 ViewModel 必须以正确的强类型暴露给子类。
        /// </summary>
        [Test]
        public void Setup_注入的ViewModel类型对子类可见()
        {
            var screen = new TestScreen();
            var vm = new TestViewModel();
            vm.Title.Value = "abc";

            screen.Setup(vm);

            Assert.AreSame(vm, screen.GetViewModel(), "强类型 ViewModel 必须可访问");
            Assert.AreEqual("abc", screen.GetViewModel().Title.Value);
        }

        /// <summary>
        /// 错误类型 ViewModel 注入应当快速失败而不是延迟到使用时崩溃。
        /// </summary>
        [Test]
        public void Setup_错误ViewModel类型抛出明确异常()
        {
            var screen = new TestScreen();
            var wrong = new OtherViewModel();

            Assert.Throws<ArgumentException>(() => screen.Setup(wrong));
        }

        /// <summary>
        /// OnDispose 必须自动 Dispose ViewModel 并把 Screen 从树中移除。
        /// </summary>
        [Test]
        public void OnDispose_销毁ViewModel并脱树()
        {
            var parent = new VisualElement();
            var screen = new TestScreen();
            parent.Add(screen);

            var vm = new TestViewModel();
            int callCount = 0;
            vm.Title.Changed += _ => callCount++;
            screen.Setup(vm);

            screen.OnDispose();

            vm.Title.Value = "after-dispose";
            Assert.AreEqual(0, callCount, "Dispose 后 ViewModel 监听者必须被清理");
            Assert.AreEqual(0, parent.childCount, "Screen 必须从父节点移除");
        }

        // ── 测试辅助类型 ──

        private class TestViewModel : ViewModelBase
        {
            public ReactiveProperty<string> Title { get; private set; }

            public TestViewModel()
            {
                Title = Prop<string>();
            }
        }

        private class OtherViewModel : ViewModelBase { }

        private class TestScreen : Screen<TestViewModel>
        {
            public bool SetupCalled { get; private set; }
            public bool ShowCalled { get; private set; }
            public bool HideCalled { get; private set; }
            public bool DisposeCalled { get; private set; }

            protected override void OnSetup() => SetupCalled = true;
            public override void OnShow() { ShowCalled = true; base.OnShow(); }
            public override void OnHide() { HideCalled = true; base.OnHide(); }
            public override void OnDispose() { DisposeCalled = true; base.OnDispose(); }

            public TestViewModel GetViewModel() => ViewModel;
        }
    }
}
