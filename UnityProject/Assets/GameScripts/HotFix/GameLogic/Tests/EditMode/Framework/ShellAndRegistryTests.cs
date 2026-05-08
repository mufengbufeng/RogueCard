using System;
using System.Collections.Generic;
using EF.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameLogic.Tests
{
    [TestFixture]
    public class ShellAndRegistryTests
    {
        // ── Shell 测试 ──

        /// <summary>
        /// 构造一个含有三个层级 child 的 root，用于模拟 UIDocument 加载 Root.uxml 后的状态。
        /// </summary>
        private static VisualElement BuildRoot()
        {
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "screen-layer" });
            root.Add(new VisualElement { name = "popup-layer" });
            root.Add(new VisualElement { name = "system-layer" });
            return root;
        }

        [Test]
        public void Shell_从root解析三个层级()
        {
            var root = BuildRoot();
            var shell = new Shell(root);

            Assert.AreEqual("screen-layer", shell.ScreenLayer.name);
            Assert.AreEqual("popup-layer", shell.PopupLayer.name);
            Assert.AreEqual("system-layer", shell.SystemLayer.name);
        }

        [Test]
        public void Shell_缺少层级时抛出明确异常()
        {
            var rootMissingPopup = new VisualElement();
            rootMissingPopup.Add(new VisualElement { name = "screen-layer" });
            rootMissingPopup.Add(new VisualElement { name = "system-layer" });

            Assert.Throws<InvalidOperationException>(() => new Shell(rootMissingPopup));
        }

        [Test]
        public void Shell_root为null时抛出ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Shell(null));
        }

        // ── ScreenRegistry 测试 ──

        [Test]
        public void ScreenRegistry_注册并查询()
        {
            var registry = new ScreenRegistry();
            registry.Register<TestScreen, TestViewModel>("test", "UI/Test");

            var desc = registry.Get("test");
            Assert.AreEqual("test", desc.Name);
            Assert.AreEqual("UI/Test", desc.Location);
            Assert.AreEqual(typeof(TestScreen), desc.ScreenType);
            Assert.AreEqual(typeof(TestViewModel), desc.ViewModelType);
            Assert.IsFalse(desc.IsPopup);
        }

        [Test]
        public void ScreenRegistry_注册为弹窗()
        {
            var registry = new ScreenRegistry();
            registry.Register<TestScreen, TestViewModel>("popup", "UI/Popup", isPopup: true);

            var desc = registry.Get("popup");
            Assert.IsTrue(desc.IsPopup);
        }

        [Test]
        public void ScreenRegistry_重复注册抛异常()
        {
            var registry = new ScreenRegistry();
            registry.Register<TestScreen, TestViewModel>("test", "UI/Test");

            Assert.Throws<InvalidOperationException>(() =>
                registry.Register<TestScreen, TestViewModel>("test", "UI/Test2"));
        }

        [Test]
        public void ScreenRegistry_查询未注册的抛异常()
        {
            var registry = new ScreenRegistry();

            Assert.Throws<KeyNotFoundException>(() => registry.Get("unknown"));
        }

        [Test]
        public void ScreenRegistry_名称不区分大小写()
        {
            var registry = new ScreenRegistry();
            registry.Register<TestScreen, TestViewModel>("MainMenu", "UI/MainMenu");

            var desc = registry.Get("mainmenu");
            Assert.AreEqual("MainMenu", desc.Name);
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

        private class TestScreen : Screen<TestViewModel>
        {
            public bool SetupCalled { get; private set; }

            protected override void OnSetup()
            {
                SetupCalled = true;
            }
        }
    }
}
